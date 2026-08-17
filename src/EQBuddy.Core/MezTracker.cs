using System.Reflection;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>One entry in the embedded mez catalog (Data/MezSpells.json).</summary>
public sealed class MezSpellInfo
{
    public string Name { get; set; } = "";
    public double? DurationSeconds { get; set; }
    public bool Aoe { get; set; }
    public string Landing { get; set; } = "mesmerized";
    public string Source { get; set; } = "";
}

/// <summary>A currently-active (believed) mez, for the chip stack.</summary>
public sealed record MezState(
    string Target, string Spell, string Caster, DateTime LandedAt, DateTime? ExpiresAt)
{
    /// <summary>Seconds until wake-up; null while the duration is unknown (the chip
    /// shows the mez without a countdown — it still clears on break).</summary>
    public double? RemainingSeconds(DateTime now) =>
        ExpiresAt is { } e ? Math.Max(0, (e - now).TotalSeconds) : null;
}

/// <summary>
/// Tracks who is mezzed, for how much longer — from ANY group member's log, not just the
/// caster's. The landing line ("X has been mesmerized.") is bystander-visible, and other
/// players' casts log with spell name and rank ("Shack begins casting Shield of Thistles
/// IV."), so every EQBuddy in the group derives the same state from its own log; no
/// networking involved. Mirrors the charm trust rules: a landing line only counts when a
/// recent cast KNOWN to be a mez explains it — an unexplained landing is someone outside
/// the log's view and is ignored.
///
/// Durations: catalog first (Data/MezSpells.json — null until researched), overridden by
/// learned values. Only the CASTER's log sees "Your X spell has worn off of Y.", so only
/// the caster's EQBuddy can measure real durations. The learner trusts the LATEST clean
/// observation per exact spell name (rank included), in BOTH directions: a Legends mez
/// holds a fixed duration unless damage breaks it, damage-broken chips are dropped
/// before their fade line can reach the learner (plus the brokeRecently guard), so a
/// surviving natural fade IS the true duration. "Only ever learn longer" sounded safe
/// but made chain-mez artifacts immortal (Taendar's 1:10 chip on a 24s mez, #68), and
/// the 2× ceiling that replaced it clipped genuine mote-upgraded durations (rahvynn's
/// 44s Mesmerization shrank to the 24s base, #69) — an occasionally-wrong-for-one-cycle
/// value that self-heals beats a permanently wrong one under either policy. Learned
/// values persist via <see cref="AttachStore"/>.
/// </summary>
public sealed class MezTracker
{
    /// <summary>A landing this long after the cast began no longer belongs to it
    /// (covers cast time + travel + log flushing).</summary>
    public static readonly TimeSpan CastToLand = TimeSpan.FromSeconds(8);
    /// <summary>Without a known duration, a mez chip that nothing ever breaks is
    /// dropped after this long — mezzes don't last minutes.</summary>
    public static readonly TimeSpan UnknownDurationCap = TimeSpan.FromSeconds(120);
    /// <summary>An expired chip stays visible at 0:00 this long before dropping —
    /// rank-lengthened mezzes outlive the base duration, and a chip that silently
    /// vanishes mid-mez reads as a bug (issue #32).</summary>
    public static readonly TimeSpan ExpiryLinger = TimeSpan.FromSeconds(8);
    /// <summary>How long a woken creature keeps explaining damage lines for its name.
    /// Refreshed by activity; long enough to cover a fight, short enough that a name
    /// doesn't stay break-immune forever (issue #35).</summary>
    public static readonly TimeSpan AwakeMemory = TimeSpan.FromSeconds(45);

    /// <summary>ONE break prints TWO lines. Waking a mezzed mob logs both
    ///   "Your Mesmerization spell has worn off of a skeleton."
    ///   "A skeleton has been awakened by Dorr."
    /// and each of them used to drop a chip, so every wake cost two — which with a
    /// stack of same-named mobs reads exactly as "the chips for all of them disappear"
    /// (#183, TheLethean, whose second log counts it precisely: one wake, two chips).
    ///
    /// The lines are paired, not summed. Whichever arrives first drops the chip and
    /// leaves a token here; its partner spends the token instead of dropping another.
    /// The order is not fixed — his log has the fade FIRST — so both sides check.
    ///
    /// Two mobs genuinely broken in the same second still print two of EACH line, so
    /// pairing stays 1:1 and both breaks are counted. The window only has to cover
    /// message lag splitting a pair across a second boundary.</summary>
    private static readonly TimeSpan BreakPairWindow = TimeSpan.FromSeconds(2);
    private readonly Dictionary<string, (int Count, DateTime At)> _unpairedBreaks =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when this line is the OTHER half of a break already counted, in
    /// which case it must not drop a second chip. Otherwise the caller drops one and
    /// this leaves the token for the partner line.</summary>
    private bool BreakAlreadyCounted(string name, DateTime now)
    {
        if (_unpairedBreaks.TryGetValue(name, out var p)
            && p.Count > 0 && (now - p.At).Duration() <= BreakPairWindow)
        {
            _unpairedBreaks[name] = (p.Count - 1, p.At);
            return true;
        }
        _unpairedBreaks[name] = (1, now);
        return false;
    }
    /// <summary>EQ effects run on 6-second server ticks, and the worn-off message fires
    /// at the tick boundary — up to several seconds AFTER the mez actually released.
    /// True durations are tick multiples, so rounding an observation DOWN to the tick
    /// recovers the exact duration and strips the message lag (field report from
    /// Aenari: learned timers ran 2-3s past the real wake — the dangerous direction).</summary>
    public const double ServerTickSeconds = 6;


    // No AA correction on purpose: the full eqlwiki AA sweep (2026-08-06, AaCatalog)
    // found NO EQ Legends AA that extends detrimental mez/charm durations — unlike live
    // EQ's Mesmerization Mastery. Adamant Will only moves resist chance, which never
    // shifts a landed mez's clock. Learned durations here are therefore character-true
    // without reading the AA ledger. (Beneficial-duration AAs like Spell Casting
    // Reinforcement matter to future BUFF countdowns, not to this tracker.)

    private readonly Dictionary<string, MezSpellInfo> _catalog;
    private readonly Dictionary<string, double> _learned = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MezState> _active = [];
    private readonly List<(string Caster, string Spell, DateTime Time)> _recentCasts = [];
    // Latest cast per exact spell name, kept indefinitely (unlike _recentCasts' 8s
    // window): re-mezzing an already-mezzed target logs NO new landing line, so the
    // only visible evidence that a fade measures a CHAIN rather than one mez is the
    // re-cast itself (the 2026-08-10 Reddit report: an AoE mez camped on cooldown
    // learned 1+ minute and, with no clean fade ever arriving, kept it).
    private readonly Dictionary<string, DateTime> _lastCastOf = new(StringComparer.OrdinalIgnoreCase);
    // The awake ledger (issue #35): creatures of this name currently believed awake,
    // and when one last acted. Once a break wakes one twin, its ongoing fight keeps
    // generating damage lines for the shared name — those attribute HERE instead of
    // eating the still-mezzed siblings' chips. Kills decrement; inactivity expires it.
    private readonly Dictionary<string, (int Count, DateTime Last)> _awake =
        new(StringComparer.OrdinalIgnoreCase);
    // Names that resisted a mez recently (#122). A resist and a landing from the
    // SAME cast are necessarily different creatures — the cast cannot both fail and
    // land on one mob — so a landing shortly after a resist must not settle the
    // resister's awake entry the way a re-mez-after-break would.
    private readonly Dictionary<string, DateTime> _recentMezResists =
        new(StringComparer.OrdinalIgnoreCase);
    private string? _storePath;
    private readonly object _lock = new();

    /// <summary>Raised when the set of active mezzes changes (not on every tick).</summary>
    public event Action? Changed;

    public MezTracker(IEnumerable<MezSpellInfo>? catalog = null)
    {
        _catalog = (catalog ?? LoadEmbedded()).ToDictionary(
            s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
    }

    public static List<MezSpellInfo> LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.MezSpells.json")
            ?? throw new InvalidOperationException("MezSpells.json missing from resources");
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.GetProperty("spells").Deserialize<List<MezSpellInfo>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    /// <summary>Loads learned durations and saves after each new maximum —
    /// same pattern as SpellCatalog's store; tests don't attach one.</summary>
    public void AttachStore(string path)
    {
        _storePath = path;
        try
        {
            if (!File.Exists(path)) return;
            var stored = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(path));
            if (stored is null) return;
            lock (_lock)
                foreach (var (spell, seconds) in stored)
                {
                    // Heal files written before the guards existed: tick-floor values
                    // that carry message lag (Aenari's 2-3s-long timers), and reject
                    // anything below the catalog base — break lengths recorded as
                    // "durations" (the {"Mesmerize": 7} incident).
                    var ticked = Math.Floor(seconds / ServerTickSeconds) * ServerTickSeconds;
                    var floor = _catalog.TryGetValue(SpellCatalog.BaseName(spell), out var info)
                        ? info.DurationSeconds ?? 0 : 0;
                    if (ticked is > 0 and < 600 && ticked >= floor) _learned.TryAdd(spell, ticked);
                }
        }
        catch { /* corrupt store: rewritten on next learn */ }
    }

    /// <summary>Second/third consumer of the parsed event stream (like SpawnTimers):
    /// replay-safe because everything keys on log timestamps.</summary>
    public void Apply(GameEvent evt)
    {
        var changed = false;
        lock (_lock)
        {
            switch (evt)
            {
                case SpellCastEvent own when IsMezSpell(own.Spell):
                    RememberCast("You", own.Spell, own.Time);
                    break;
                case OtherCastEvent other when IsMezSpell(other.Spell):
                    RememberCast(other.Caster, other.Spell, other.Time);
                    break;
                case MezzedEvent mez:
                    changed = OnLanding(mez);
                    break;
                case MezAwakenedEvent awakened:
                    // The game's own break line (#122): drop exactly one chip of
                    // this name and record the waker's victim as awake — no
                    // inference needed, the log said it out loud.
                    changed = OnAwakened(awakened);
                    break;
                case ResistEvent { Target.Length: > 0 } resist when IsMezSpell(resist.Spell):
                    // A creature RESISTING a mez is awake right now. Pre-arming the
                    // ledger keeps a never-mezzed twin's attacks from eating a mezzed
                    // sibling's chip (Snagglefern's PoH fight: the drake that resisted
                    // clawed away while its freshly-mezzed twin's chip vanished).
                    // Touches no chips: a mezzed mob can also resist a re-application
                    // while staying asleep, so this only ever ADDS awake knowledge.
                    OnResistedMez(resist);
                    break;
                // Any damage wakes a mezzed creature — from anyone, visible to everyone.
                case DamageDealtEvent dd:
                    changed = OnCreatureActivity(dd.Target, dd.Time);
                    break;
                case ThirdMeleeEvent tm:
                    // Damage TO the target breaks it; the target ATTACKING proves it woke.
                    changed = OnCreatureActivity(tm.Target, tm.Time) | OnCreatureActivity(tm.Attacker, tm.Time);
                    break;
                case ThirdDotEvent td:
                    changed = OnCreatureActivity(td.Target, td.Time);
                    break;
                case ThirdSchoolEvent tsch:
                    changed = OnCreatureActivity(tsch.Target, tsch.Time) | OnCreatureActivity(tsch.Attacker, tsch.Time);
                    break;
                // The creature acting proves it's awake — but a DoT tick doesn't count:
                // a dot cast before the mez keeps ticking on you while the mob sleeps.
                case DamageTakenEvent { Self: false, OverTime: false } dt:
                    changed = OnCreatureActivity(dt.Attacker, dt.Time);
                    break;
                case KillEvent k:
                    changed = OnKill(k.Target, k.Time);
                    break;
                case SpellWornOffEvent { Pet: false } wo when wo.Target.Length > 0 && IsMezSpell(wo.Spell):
                    // Caster-private natural fade: the exact end, and the one signal that
                    // can teach a real duration (see class summary).
                    changed = OnWornOff(wo);
                    break;
                case ZoneEvent:
                    changed = _active.Count > 0;
                    _active.Clear();
                    _recentCasts.Clear();
                    _lastCastOf.Clear();
                    _awake.Clear();
                    _recentMezResists.Clear();
                    break;
            }
            Prune(evt.Time);
        }
        if (changed) Changed?.Invoke();
    }

    /// <summary>Cheap emptiness probe for per-tick gates — no list, no sort.</summary>
    public bool Any(DateTime now)
    {
        lock (_lock)
            return _active.Any(m => m.ExpiresAt is null || now - m.ExpiresAt < ExpiryLinger);
    }

    /// <summary>Active mezzes at <paramref name="now"/>, soonest wake-up first;
    /// unknown-duration entries sort last (nothing to warn about yet). Entries past
    /// their expiry stay visible (at 0:00) for <see cref="ExpiryLinger"/> — the mez
    /// may genuinely still hold (rank-lengthened durations) and a silent vanish
    /// mid-mez reads as a bug.</summary>
    public List<MezState> Snapshot(DateTime now)
    {
        lock (_lock)
            return _active
                .Where(m => m.ExpiresAt is null || now - m.ExpiresAt < ExpiryLinger)
                .OrderBy(m => m.ExpiresAt ?? DateTime.MaxValue)
                .ToList();
    }

    /// <summary>Learned durations (exact spell name → seconds), for display/export.</summary>
    public IReadOnlyDictionary<string, double> LearnedDurations
    {
        get { lock (_lock) return new Dictionary<string, double>(_learned); }
    }

    private bool IsMezSpell(string spell) =>
        _catalog.ContainsKey(SpellCatalog.BaseName(spell));

    private void RememberCast(string caster, string spell, DateTime t)
    {
        _recentCasts.Add((caster, spell, t));
        if (_recentCasts.Count > 32) _recentCasts.RemoveRange(0, 16);
        _lastCastOf[spell] = t;
    }

    private bool OnLanding(MezzedEvent mez)
    {
        // Newest explaining cast wins. AoE mezzes land on several targets from one cast,
        // so the cast is NOT consumed — each landing within the window claims it.
        var cast = _recentCasts.LastOrDefault(c => mez.Time - c.Time <= CastToLand);
        if (cast.Spell is null || cast.Spell.Length == 0) return false;   // nobody we can see cast a mez

        var entry = new MezState(mez.Target, cast.Spell, cast.Caster, mez.Time,
            DurationFor(cast.Spell) is { } d ? mez.Time.AddSeconds(d) : null);

        // An AWAKE creature of this name getting mezzed is the classic re-mez after a
        // break: settle its ledger entry and ADD a chip — the sleeping siblings keep
        // theirs (issue #35). EXCEPT when the name resisted within this same cast's
        // window: that resist and this landing are different creatures, so the
        // resister stays on the awake books and the landing takes the normal path.
        if (_awake.TryGetValue(entry.Target, out var awake) && awake.Count > 0
            && mez.Time - awake.Last <= AwakeMemory
            && !(_recentMezResists.TryGetValue(entry.Target, out var rt)
                && mez.Time - rt <= CastToLand))
        {
            if (awake.Count == 1) _awake.Remove(entry.Target);
            else _awake[entry.Target] = (awake.Count - 1, mez.Time);
            _active.Add(entry);
            return true;
        }

        // Same-name handling (issue #32, reworked from the original keep-earliest rule):
        // chain-mezzing ONE target is the normal workflow, so a re-landing REFRESHES the
        // earliest-expiring same-name entry. The exception is several landings in the
        // same second (an AoE catching same-named mobs): those are distinct creatures
        // and get their own entries — the UI numbers them.
        var sameName = _active.Where(m =>
            m.Target.Equals(mez.Target, StringComparison.OrdinalIgnoreCase)).ToList();
        var refresh = sameName
            .Where(m => m.LandedAt != mez.Time)
            .OrderBy(m => m.ExpiresAt ?? DateTime.MaxValue)
            .FirstOrDefault();
        if (refresh is not null) _active.Remove(refresh);
        _active.Add(entry);
        return true;
    }

    private bool OnWornOff(SpellWornOffEvent wo)
    {
        // Among same-named entries the longest-asleep one fades first.
        var name = LogParser.Normalize(wo.Target);
        // ...unless the game's explicit "has been awakened by" line already counted this
        // same break (#183). Then this is its partner, not a second creature: drop no
        // chip, and learn nothing either — a break is not a measurement of the duration.
        if (BreakAlreadyCounted(name, wo.Time)) return false;
        var entry = _active
            .Where(m => m.Target.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.LandedAt)
            .FirstOrDefault();
        if (entry is null) return false;
        _active.Remove(entry);
        // A natural fade measures the full duration; learn the longest observed per
        // exact (ranked) spell name. Guards (field report, David 2026-08-04: a single
        // 7s early break got learned as Mesmerize's duration and shrank every chip on
        // the machine): the worn-off line ALSO fires on breaks, so an observation
        // SHORTER than the catalog base is a break — ranks only lengthen mezzes —
        // and a fade right after the name's awake ledger was touched is a break too.
        // Tick-floor the raw gap (see ServerTickSeconds): 32.8s observed = a 30s mez
        // plus message lag, never a 32.8s mez.
        var observed = Math.Floor((wo.Time - entry.LandedAt).TotalSeconds / ServerTickSeconds)
            * ServerTickSeconds;
        var baseFloor = _catalog.TryGetValue(SpellCatalog.BaseName(entry.Spell), out var info)
            ? info.DurationSeconds ?? 0 : 0;
        var brokeRecently = _awake.TryGetValue(name, out var aw)
            && (wo.Time - aw.Last).Duration() <= TimeSpan.FromSeconds(3);
        // A later cast of the SAME spell contaminates the observation: re-mezzing an
        // already-mezzed target logs no fresh landing line, so this fade may span a
        // whole chain of casts — a camper re-mezzing on cooldown would learn the
        // chain length as "the duration" and never see a clean fade to heal it
        // (Reddit report, 2026-08-10: AoE mez chips claiming 1+ minute). Clickies
        // log no cast line and stay invisible; those artifacts still heal via
        // latest-clean-wins on the next honest cycle.
        var recastAfterLanding = _lastCastOf.TryGetValue(entry.Spell, out var lastCast)
            && lastCast > entry.LandedAt.AddSeconds(1);
        // Latest clean observation wins, longer OR shorter (see class summary): a
        // chain-mez artifact heals on the next single mez, a stale short value heals
        // on the next upgraded cast — either way one honest fade fixes the chip.
        if (observed is > 3 and < 600 && observed >= baseFloor && !brokeRecently
            && !recastAfterLanding
            && (!_learned.TryGetValue(entry.Spell, out var known) || Math.Abs(observed - known) > 0.5))
        {
            _learned[entry.Spell] = Math.Round(observed, 1);
            SaveStore();
        }
        return true;
    }

    private double? DurationFor(string spell) =>
        _learned.TryGetValue(spell, out var learned) ? learned
        : _catalog.TryGetValue(SpellCatalog.BaseName(spell), out var info) ? info.DurationSeconds
        : null;

    /// <summary>The explicit break line: "X has been awakened by Y." Always drops
    /// exactly one chip (earliest expiry — the likeliest-awake one) and records the
    /// newly-awake creature, even when the ledger already knew an awake twin: the
    /// line refers to a MEZZED one waking, not to the one already fighting.</summary>
    private bool OnAwakened(MezAwakenedEvent awakened)
    {
        var name = LogParser.Normalize(awakened.Target);
        _awake[name] = ((_awake.TryGetValue(name, out var prev) ? prev.Count : 0) + 1, awakened.Time);
        // The awake knowledge above is recorded either way — only the chip is paired.
        if (BreakAlreadyCounted(name, awakened.Time)) return false;
        var victim = _active
            .Where(m => m.Target.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.ExpiresAt ?? DateTime.MaxValue)
            .FirstOrDefault();
        if (victim is null) return false;
        _active.Remove(victim);
        return true;
    }

    /// <summary>A mez resist proves an awake creature of the name exists. Only ever
    /// ADDS awake knowledge (never drops a chip, never inflates an existing count —
    /// a mezzed mob can resist a re-application while staying asleep, and one mob
    /// resists many casts).</summary>
    private void OnResistedMez(ResistEvent resist)
    {
        var name = LogParser.Normalize(resist.Target);
        _recentMezResists[name] = resist.Time;
        _awake[name] = _awake.TryGetValue(name, out var a) && a.Count > 0
            ? (a.Count, resist.Time)
            : (1, resist.Time);
    }

    /// <summary>A damage/action line for this name. If a creature of the name is
    /// already believed awake (and recently active), the line is ITS fight — no chip
    /// is touched (issue #35: the woken twin's ongoing fight must not erase the
    /// still-mezzed siblings' chips). Otherwise this line IS the break: the
    /// earliest-expiring chip drops and the ledger records one awake creature.</summary>
    private bool OnCreatureActivity(string target, DateTime now)
    {
        var name = LogParser.Normalize(target);
        if (_awake.TryGetValue(name, out var a) && a.Count > 0 && now - a.Last <= AwakeMemory)
        {
            _awake[name] = (a.Count, now);   // the awake one is still fighting
            return false;
        }
        var victim = _active
            .Where(m => m.Target.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.ExpiresAt ?? DateTime.MaxValue)
            .FirstOrDefault();
        if (victim is null) return false;
        _active.Remove(victim);
        _awake[name] = ((_awake.TryGetValue(name, out var prev) ? prev.Count : 0) + 1, now);
        return true;
    }

    /// <summary>A kill for this name. An awake creature dying decrements the ledger
    /// and touches no chip (the dead one is the one that was fighting); a kill with
    /// nothing awake means a mezzed one was killed outright (AoE) — drop its chip.</summary>
    private bool OnKill(string target, DateTime now)
    {
        var name = LogParser.Normalize(target);
        if (_awake.TryGetValue(name, out var a) && a.Count > 0 && now - a.Last <= AwakeMemory)
        {
            if (a.Count == 1) _awake.Remove(name);
            else _awake[name] = (a.Count - 1, now);
            return false;
        }
        var victim = _active
            .Where(m => m.Target.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.ExpiresAt ?? DateTime.MaxValue)
            .FirstOrDefault();
        if (victim is null) return false;
        _active.Remove(victim);
        return true;
    }

    private void Prune(DateTime now)
    {
        _recentCasts.RemoveAll(c => now - c.Time > CastToLand);
        foreach (var stale in _recentMezResists
                     .Where(kv => now - kv.Value > CastToLand)
                     .Select(kv => kv.Key).ToList())
            _recentMezResists.Remove(stale);
        // Entries are RETAINED well past their visible expiry (Snapshot hides them
        // after ExpiryLinger): a rank-lengthened mez can fade long after the base
        // duration, and the natural-fade line must still find its entry to learn
        // from — pruning at the linger would make high ranks unlearnable. A stale
        // retained entry also absorbs the next break line first (earliest expiry),
        // which is the right guess: it's the likeliest-awake one. The cap only
        // bounds UNKNOWN durations: a mez KNOWN to run past 120s must outlive it to
        // ExpiresAt + the linger, or its own fade line finds nothing to teach
        // (audit finding 8 — the long mez that could never learn).
        _active.RemoveAll(m => now - m.LandedAt > UnknownDurationCap
            && (m.ExpiresAt is not { } e || now - e > ExpiryLinger));
    }

    private void SaveStore()
    {
        if (_storePath is not { } path) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(_learned));
        }
        catch { /* best-effort; in-memory learning still works */ }
    }
}
