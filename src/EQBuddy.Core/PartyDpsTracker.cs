namespace EQBuddy.Core;

/// <summary>Best-effort classification of an attacker name — see PartyDpsTracker's
/// CategoryOf for the heuristic. Not a verified roster; a same-named collision (a charmed
/// pet sharing a hostile mob's exact name) can still misclassify, and an attacker with no
/// positive evidence either way defaults to Enemy (most named EQ raid/group mobs — "Elite
/// dragoon", "Knight of Innoruuk" — carry no leading article, so "no article" alone isn't a
/// safe signal for Player; unrecognized names are far more often mobs than people).</summary>
public enum AttackerCategory { Player, Pet, Enemy }

/// <summary>Snapshot of party DPS for the current pull.</summary>
/// <param name="Active">True while the idle gap (PartyDpsTracker.PullGap) hasn't elapsed
/// since the last hit.</param>
/// <param name="DurationSeconds">Seconds since the pull's first hit (at least 1).</param>
/// <param name="TotalDamage">Sum of every row's <see cref="SourceDamage.Total"/>.</param>
/// <param name="Rows">Per-attacker totals, sorted by damage descending.</param>
public record PartyDpsSnapshot(bool Active, double DurationSeconds, long TotalDamage,
    IReadOnlyList<SourceDamage> Rows);

/// <summary>
/// Best-effort live DPS by attacker for the current pull, kept deliberately separate from
/// <see cref="SessionStats"/>. Tracks damage dealt by anyone visible in your own log — your
/// own hits, "third-party" lines (&lt;attacker&gt; hits &lt;target&gt; for N...) for whoever else is
/// fighting near you, AND whatever's hitting you directly — there's no group/raid roster
/// anywhere in this codebase, so every visible attacker is included, not just verified group
/// members. This is also how a mob's own dps ends up in the list: it's just another
/// attacker, whether it's landing hits on you or a groupmate. Treat this as "nearby damage,"
/// not a roster feature.
///
/// Your own client's charm state (SessionStats's own tracking) isn't consulted here — a
/// GROUPMATE's charmed pet is invisible to that anyway. Instead, <see cref="CategoryOf"/>
/// gives a best-effort Player/Pet/Enemy grouping from log evidence alone (field report
/// 2026-08-05: a global pull clock that never idled out because a groupmate's charmed pet
/// kept fighting on its own schedule) — see the AttackerCategory doc and the private fields
/// below for exactly what evidence drives it.
///
/// Pull boundary: any hit more than <see cref="PullGap"/> after the previous one starts a
/// fresh pull, clearing the live per-pull rows — the same idle-gap rule EncounterGrouping
/// uses to split pulls (EncounterTypes.cs), applied locally so this tracker doesn't depend
/// on it. Alongside that, a second set of totals accumulates across pulls (never auto-reset,
/// only <see cref="ResetTotals"/>) for whichever names the window's user has chosen to
/// track — see <see cref="TotalsSnapshot"/>.
/// </summary>
public sealed class PartyDpsTracker
{
    // Short on purpose (was 10s): every attacker sharing this ONE global clock means a
    // groupmate's charmed pet fighting on its own schedule could keep a fight alive
    // indefinitely, and a narrower window also shrinks the odds that a charmed pet and a
    // same-named hostile mob are both mid-swing in the same pull, which is what actually
    // causes their damage to merge into one row (categorization only sorts a name into a
    // section — it can't split one name's row apart). Field report 2026-08-05.
    private static readonly TimeSpan PullGap = TimeSpan.FromSeconds(3);

    private sealed class Row
    {
        public int Hits;
        public long Total;
        public int Crits;
        public DateTime First;
        public DateTime Last;
    }

    private readonly Dictionary<string, Row> _rows = new(StringComparer.OrdinalIgnoreCase);
    private DateTime? _pullStart;
    private DateTime? _lastActivity;

    // Running totals: cumulative damage since the tracker was created or ResetTotals() was
    // last called. Never cleared by the pull-gap Reset().
    private readonly Dictionary<string, Row> _totals = new(StringComparer.OrdinalIgnoreCase);

    // Seconds actually spent fighting, summed across every pull since the last
    // ResetTotals() — NOT wall-clock time since then, which would keep diluting the dps
    // number every second you're standing around between pulls. Closed-out pulls land here
    // when Reset() fires; TotalsSnapshot adds whatever's accrued in _totalsSegmentStart (the
    // still-open pull) on top. Tracked separately from _pullStart so that clicking Reset
    // mid-fight doesn't leave the pre-reset portion of the current pull still counting
    // toward the new denominator.
    private double _totalsActiveSeconds;
    private DateTime? _totalsSegmentStart;

    // Best-effort Player/Pet/Enemy classification (see CategoryOf).
    //
    // _hasArticle: EQ creature names conventionally carry a leading article ("a shadowed
    // man", "an orc pawn") that real player names never do — captured from the RAW attacker
    // text before Normalize() strips it. Many named mobs (raid adds, unique NPCs) DON'T use
    // an article though, so this alone only ever helps confirm "generic creature," never
    // "definitely a player" — see CategoryOf's default.
    //
    // _confirmedEnemies: unambiguous hostiles only — something that hit you, or something
    // you hit. Never populated from a Third* event's target, since that could just as
    // easily be a groupmate.
    //
    // _confirmedPets: the field-reported tell — an article-style name caught attacking
    // something already in _confirmedEnemies is a charmed pet, not a second hostile mob
    // (real mobs essentially never fight each other). Gated on the attacker ALSO being
    // article-style, or a real player fighting alongside you (who is, by definition,
    // attacking the same confirmed enemies you are) would land here too.
    //
    // _attackedByConfirmedEnemy: a weak positive signal for Player — something a known
    // hostile chose to attack that ISN'T itself article-style is very likely a person (mobs
    // predominantly aggro onto players, not other mobs' pets).
    private readonly HashSet<string> _hasArticle = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _confirmedEnemies = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _confirmedPets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _attackedByConfirmedEnemy = new(StringComparer.OrdinalIgnoreCase);

    // LogWatcher calls Apply() from its background polling timer/thread, while the window
    // reads Snapshot()/TotalsSnapshot() from its own UI-thread DispatcherTimer — same
    // cross-thread shape SessionStats and MezTracker already guard with a lock.
    private readonly object _lock = new();

    public void Apply(GameEvent evt)
    {
        lock (_lock)
        {
            switch (evt)
            {
                case DamageDealtEvent d:
                    // You hit it, so it's unambiguously hostile — feeds the pet-detection
                    // rule below, not just display.
                    _confirmedEnemies.Add(LogParser.Normalize(d.Target));
                    Add("You", d.Time, d.Amount, d.Critical);
                    break;
                case ThirdMeleeEvent tm:
                    NoteThirdPartyAttacker(tm.Attacker, tm.Target);
                    Add(tm.Attacker, tm.Time, tm.Amount, tm.Critical);
                    break;
                case ThirdDotEvent td:
                    NoteThirdPartyAttacker(td.Caster, td.Target);
                    Add(td.Caster, td.Time, td.Amount, td.Critical);
                    break;
                case ThirdSchoolEvent ts:
                    NoteThirdPartyAttacker(ts.Attacker, ts.Target);
                    Add(ts.Attacker, ts.Time, ts.Amount, ts.Critical);
                    break;
                case ThirdMissEvent tmiss:
                    Touch(tmiss.Time);
                    break;
                // A mob hitting a groupmate is a Third* event above, but EQ logs a mob
                // hitting YOU specifically as a completely different line/event ("Orc
                // centurion hits YOU for..."). Without this, the enemy's own dps would
                // silently drop out of Current Pull the moment it's attacking you instead
                // of someone else — exactly the gap a field report caught (2026-08-05:
                // "mixture of me getting hit and my friend"). Self-inflicted damage (HP-cost
                // casting, falling, drowning) isn't an attacker at all, so it's excluded.
                //
                // One more exclusion: LogParser's NonMeleeInRx is a catch-all for lines EQ
                // doesn't cleanly separate attacker from effect ("YOU are burned by orc
                // centurion's flames...") — its Attacker field is really a descriptive label,
                // not a name (field report 2026-08-05: "Burned by a gust of wind's flames"
                // showing up as if it were an attacker). It's the only DamageTakenEvent
                // source that's non-melee with no Ability set; every other source
                // (melee, or spell/DoT with a known caster) keeps one or the other.
                case DamageTakenEvent { Self: false } dt when dt.Melee || dt.Ability.Length > 0:
                    // It hit you, so it's unambiguously hostile.
                    _confirmedEnemies.Add(dt.Attacker); // already Normalize()-d by LogParser
                    Add(dt.Attacker, dt.Time, dt.Amount, false);
                    break;
            }
        }
    }

    /// <summary>Records the leading-article tell and checks the pet-detection and
    /// attacked-by-enemy rules for a Third* attacker. Must run before Add() normalizes the
    /// name away.</summary>
    private void NoteThirdPartyAttacker(string rawAttacker, string normalizedTarget)
    {
        var key = LogParser.Normalize(rawAttacker);
        var hasArticle = StartsWithArticle(rawAttacker);
        if (hasArticle) _hasArticle.Add(key);
        // Only a generic-named attacker fighting a confirmed enemy is pet evidence — a real
        // player fighting alongside you is, by definition, also attacking things you've
        // confirmed hostile, and must not be swept into this bucket too. Also gated on NOT
        // already being independently confirmed hostile itself: hostile mobs occasionally
        // clip each other in the log (fear/confusion effects, AI pathing bumping into a
        // neighbor) without either one being charmed — that's noise, not a charm tell, and
        // must never override direct evidence (field report 2026-08-06: several named
        // "Innoruuk" adds swinging at their own leader all read as pets).
        if (hasArticle && !_confirmedEnemies.Contains(key) && _confirmedEnemies.Contains(normalizedTarget))
            _confirmedPets.Add(key);
        if (_confirmedEnemies.Contains(key)) _attackedByConfirmedEnemy.Add(normalizedTarget);
    }

    private static bool StartsWithArticle(string raw)
    {
        var t = raw.TrimStart();
        foreach (var article in (string[])["a ", "an ", "the "])
            if (t.Length > article.Length && t.StartsWith(article, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>Best-effort Player/Pet/Enemy classification for the window's grouped
    /// display — see the class remarks on <see cref="_hasArticle"/> and friends for the
    /// heuristic. Not a verified roster.</summary>
    public AttackerCategory CategoryOf(string name)
    {
        lock (_lock)
        {
            var key = LogParser.Normalize(name);
            if (string.Equals(key, "You", StringComparison.OrdinalIgnoreCase)) return AttackerCategory.Player;
            // Direct evidence (it hit you, or you hit it) always wins over the pet-detection
            // rule, checked first here because the two can be added in either order — e.g. a
            // hostile add clips another hostile early in the fight (flagging it as a
            // "pet") and only later hits the player directly (confirming it hostile).
            if (_confirmedEnemies.Contains(key)) return AttackerCategory.Enemy;
            if (_confirmedPets.Contains(key)) return AttackerCategory.Pet;
            // A known hostile chose to attack this name — likely a person, unless it's
            // ALSO a generic creature name, in which case it reads more like a pet caught
            // in the crossfire than a player.
            if (_attackedByConfirmedEnemy.Contains(key))
                return _hasArticle.Contains(key) ? AttackerCategory.Pet : AttackerCategory.Player;
            // No positive evidence either way: default to Enemy. Most unrecognized names in
            // group/raid combat are mobs, named or not (see AttackerCategory's remarks) —
            // "no article" alone isn't reliable enough to default to Player.
            return AttackerCategory.Enemy;
        }
    }

    private void Touch(DateTime t)
    {
        if (_lastActivity is { } last && t - last > PullGap)
            Reset();
        _pullStart ??= t;
        _totalsSegmentStart ??= t;
        _lastActivity = t;
    }

    private void Add(string name, DateTime t, int amount, bool crit)
    {
        Touch(t);
        var key = LogParser.Normalize(name);
        AddTo(_rows, key, t, amount, crit);
        AddTo(_totals, key, t, amount, crit);
    }

    private static void AddTo(Dictionary<string, Row> dict, string key, DateTime t, int amount, bool crit)
    {
        if (!dict.TryGetValue(key, out var row))
            dict[key] = row = new Row { First = t };
        row.Hits++;
        row.Total += amount;
        if (crit) row.Crits++;
        row.Last = t;
    }

    private void Reset()
    {
        // The pull that's ending banks its combat-active seconds before its rows are
        // cleared. _totalsSegmentStart is only non-null while a pull is actually open, so a
        // Reset() triggered by a second idle Snapshot() call in the same quiet stretch
        // (nothing new to close) is a no-op here.
        if (_totalsSegmentStart is { } start && _lastActivity is { } last)
            _totalsActiveSeconds += (last - start).TotalSeconds;
        _rows.Clear();
        _pullStart = null;
        _totalsSegmentStart = null;
    }

    /// <summary>Clears the running totals (not the live per-pull view) — the window's
    /// "Reset" button.</summary>
    public void ResetTotals()
    {
        lock (_lock)
        {
            _totals.Clear();
            _totalsActiveSeconds = 0;
            // If a pull is still going, restart its segment here rather than leaving
            // _totalsSegmentStart pointing at the pull's original (pre-reset) start —
            // otherwise the next TotalsSnapshot would count time that happened before
            // the reset.
            if (_pullStart is not null) _totalsSegmentStart = _lastActivity;
        }
    }

    public PartyDpsSnapshot Snapshot(DateTime now)
    {
        lock (_lock)
        {
            // Combat's been quiet for a full gap — the pull is over; clear stale numbers
            // before rendering rather than waiting for the next hit to trigger Reset().
            if (_lastActivity is { } last && now - last > PullGap)
                Reset();

            var active = _lastActivity is { } l && now - l <= PullGap;
            var start = _pullStart ?? now;
            var elapsed = Math.Max(1, (now - start).TotalSeconds);
            var rows = _rows
                .Select(kv => new SourceDamage(kv.Key, kv.Value.Hits, kv.Value.Total, kv.Value.Crits,
                    Math.Max(1, (kv.Value.Last - kv.Value.First).TotalSeconds)))
                .OrderByDescending(r => r.Total)
                .ToList();
            return new PartyDpsSnapshot(active, elapsed, rows.Sum(r => r.Total), rows);
        }
    }

    /// <summary>Running totals for just the given roster (normalized names, case-insensitive)
    /// — everyone else seen is still tallied internally (so checking a name later doesn't
    /// lose what they already did) but left out of the returned rows. DurationSeconds is
    /// combat-active time only (closed-out pulls' banked seconds plus whatever the still-open
    /// one has racked up so far) — wall-clock time spent idle between pulls doesn't count,
    /// so dps doesn't quietly drain away while nothing is happening.</summary>
    public PartyDpsSnapshot TotalsSnapshot(DateTime now, IReadOnlySet<string> roster)
    {
        lock (_lock)
        {
            var ongoing = _totalsSegmentStart is { } start ? (now - start).TotalSeconds : 0;
            var elapsed = Math.Max(1, _totalsActiveSeconds + ongoing);
            var rows = _totals
                .Where(kv => roster.Contains(kv.Key))
                .Select(kv => new SourceDamage(kv.Key, kv.Value.Hits, kv.Value.Total, kv.Value.Crits,
                    Math.Max(1, (kv.Value.Last - kv.Value.First).TotalSeconds)))
                .OrderByDescending(r => r.Total)
                .ToList();
            return new PartyDpsSnapshot(true, elapsed, rows.Sum(r => r.Total), rows);
        }
    }
}
