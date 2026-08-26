using System.IO;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>One running (or just-expired) spawn countdown.</summary>
public sealed record SpawnTimerState(
    string Server, string Zone, string Name,
    DateTime KilledAt, double? DurationSeconds)
{
    public DateTime? DueAt => DurationSeconds is { } d ? KilledAt.AddSeconds(d) : null;
    public bool IsDue(DateTime now) => DueAt is { } due && now >= due;

    /// <summary>The camp, learned from YOUR /loc at kill time (map pins — the
    /// "ShowEQ Lite" panel, 2026-08-10): you were standing at the fight, so your
    /// position IS the camp, near enough to find it again. Null until a kill lands
    /// with a fresh /loc in the log; timers persisted before this existed
    /// deserialize null. Values are the /loc's own (Y, X) order.</summary>
    public double? CampLocY { get; init; }
    public double? CampLocX { get; init; }

    /// <summary>Which continuous stay in the zone this kill happened during — a
    /// counter bumped on every zone-enter line, never a wall clock. Two kills sharing
    /// it means the player never left between them.
    ///
    /// It is NOT persisted (see the <c>JsonIgnore</c>), for the same reason
    /// <see cref="KilledName"/> answers null across a restart: a timer recovered from
    /// disk carries no evidence about where the player was, and no evidence must never
    /// read as agreement.
    ///
    /// Only the no-known-duration paths consult it, and deliberately. A cross-stay gap
    /// is still a TRUE upper bound — the mob died, and was dead again, so it respawned
    /// in between — and wherever a duration already exists the <c>gap &lt; d</c> rule
    /// keeps such a gap harmless: it can only tighten toward the truth. But with no
    /// duration to check it against, the first accepted gap BECOMES the countdown, and
    /// "killed it, went to Freeport, came back five hours later" prints a confident
    /// five-hour timer that nothing on screen contradicts.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int? ZoneStay { get; init; }

    /// <summary>The creature whose death actually started this clock, as the kill
    /// line named it — the named itself, an alias, or a placeholder. Learning needs
    /// the distinction (2026-08-16, Sol A: a trash kill that matched CWG Model EXG
    /// started his clock, and killing the REAL EXG 93s later "measured" a 93-second
    /// respawn — the gap between two different mobs is walk time, not a cycle).
    /// Timers persisted before this existed deserialize null: no evidence either
    /// way, so nothing learns from them.</summary>
    public string? KilledName { get; init; }
}

/// <summary>
/// Tracks when named mobs (or their placeholders) were seen killed and counts down to
/// their respawn (SPAWN-003). Fed the same parsed event stream as SessionStats, so
/// timestamps come from the log: a restart replays the log and re-derives running
/// countdowns exactly like delayed watch cues do. Timers longer than a log's lifetime
/// (raid targets; auto-emptied logs) survive via a persistence file instead.
///
/// Kill matching is zone-gated: names repeat across zones ("an ice giant"), and a kill
/// line names no zone, so the current zone comes from the "You have entered" lines the
/// same way the Travels card learns them. No zone seen yet means no automatic matching —
/// the ▶ button in the Spawns window is the fallback, not a guess.
///
/// Timers are per-server (freeport's Frenzy is not qeynos's), keyed server|zone|name.
/// A repeat kill restarts the clock; replaying the same kill is a no-op.
/// </summary>
public sealed class SpawnTimers
{
    private readonly SpawnCatalog _catalog;
    private readonly SpawnOverrides _overrides;
    private readonly string? _persistPath;
    private readonly object _lock = new();
    private readonly Dictionary<string, SpawnTimerState> _timers =
        new(StringComparer.OrdinalIgnoreCase);

    private SpawnZone? _currentZone;
    /// <summary>The raw zone-enter name said this zone is an INSTANCE (#109) — kills
    /// here start no automatic countdowns; see SpawnCatalog.IsInstancedZoneName.</summary>
    private bool _currentZoneInstanced;

    /// <summary>The zone a "creating instance" line just named, until the enter line that
    /// follows it spends the fact (#109, Frankthetankk's Sky sequence). Plane of Sky's
    /// enter line is byte-identical to the open-world one, so without this the zone gate
    /// could never fire there. Spent on the FIRST enter line, matching or not — a stale
    /// announcement must not make some later zone an instance.</summary>
    private string? _pendingInstanceZone;
    private LocationEvent? _lastLoc;
    /// <summary>Which continuous stay in a zone we are on, bumped by every zone-enter
    /// line. Stamped onto each new timer as <see cref="SpawnTimerState.ZoneStay"/>.</summary>
    private int _zoneStay;
    /// <summary>A /loc within this window of a kill counts as the camp's position —
    /// long enough for "tap the hotbutton, pull, kill", short enough that a stale
    /// reading from across the zone doesn't pin the wrong hillside.</summary>
    private static readonly TimeSpan CampLocWindow = TimeSpan.FromMinutes(3);

    /// <summary>The camp position for a new timer: a fresh /loc wins; otherwise the
    /// previous timer's learned camp carries forward (re-kills without a /loc must
    /// not erase what an earlier kill taught).</summary>
    private (double? Y, double? X) CampFor(string zone, string name, DateTime killTime)
    {
        if (_lastLoc is { } loc && killTime - loc.Time <= CampLocWindow && killTime >= loc.Time)
            return (loc.LocY, loc.LocX);
        return _timers.TryGetValue(Key(Server, zone, name), out var prior)
            ? (prior.CampLocY, prior.CampLocX)
            : (null, null);
    }

    public string Server { get; set; } = "";
    public SpawnZone? CurrentZone { get { lock (_lock) return _currentZone; } }

    public SpawnTimers(SpawnCatalog catalog, SpawnOverrides overrides, string? persistPath = null,
        SpawnCycleLedger? cycles = null)
    {
        _catalog = catalog;
        _overrides = overrides;
        _persistPath = persistPath;
        _cycles = cycles;
        LoadPersisted();
        HealSuppressedOverrides();
        PurgePetTimers();
    }

    /// <summary>The respawn-cycle evidence store behind the pack's wiki suggestions —
    /// written only here, at the points a gap passes the honesty gates, so the gates
    /// apply to the ledger by construction. Null in tests that don't care.</summary>
    private readonly SpawnCycleLedger? _cycles;

    /// <summary>
    /// Remove spawn entries for PETS that an older build discovered as named mobs (David,
    /// 2026-08-22: *"we were starting to see named's pet"*).
    ///
    /// `NamedMobHeuristic` refuses them now, but that only stops NEW ones — and the player
    /// who noticed is the one whose list is already full of them. A fix the reporter cannot
    /// see is a fix they will report again.
    ///
    /// Both stores, because a pet reached both: the learned override that remembers it, and
    /// the live timer counting down to a respawn that will never happen.
    /// </summary>
    private void PurgePetTimers()
    {
        // DISCOVERED ONLY. `Discovered` exists precisely so "a discovery can be discarded
        // without touching the player's own additions" (its own doc), and the first cut of
        // this purge matched on the name alone — which would have deleted a player's
        // hand-added `Custom` entry, and any duration they had TYPED, for anything they
        // named "… pet". Found by Fable 5 in the v1.99.5 release review; the test could not
        // see it because it never set Custom. A cleanup that removes the player's own work
        // is not a cleanup.
        var gone = _overrides.PurgeNames((name, o) => IsPetName(name) && o.Discovered);
        if (gone > 0) _overrides.Save();

        lock (_lock)
        {
            // Same rule for the live timer: a MANUAL duration is the player's word, and it
            // outranks our inference everywhere else in this file (see IsManual).
            var doomed = _timers
                .Where(kv => IsPetName(kv.Value.Name)
                             && !IsManual(_overrides.Find(kv.Value.Zone, kv.Value.Name)))
                .Select(kv => kv.Key)
                .ToList();
            foreach (var k in doomed) _timers.Remove(k);
            if (doomed.Count > 0) SavePersisted();
        }
    }

    /// <summary>The pet test, in one place so the purge and the heuristic cannot disagree
    /// about what a pet is.</summary>
    private static bool IsPetName(string name) =>
        name.Trim().EndsWith(" pet", StringComparison.OrdinalIgnoreCase)
        || name.Trim().Equals("pet", StringComparison.OrdinalIgnoreCase);

    /// <summary>Fed alongside SessionStats.Apply from the watcher thread.</summary>
    public void Apply(GameEvent evt)
    {
        switch (evt)
        {
            case InstanceCreatedEvent ic:
                lock (_lock) _pendingInstanceZone = ic.Zone;
                break;
            case ZoneEvent z:
                lock (_lock)
                {
                    _currentZone = _catalog.FindZone(z.Zone);
                    var announced = _pendingInstanceZone is { } pz
                        && (_currentZone?.MatchesZoneName(pz) ?? pz.Equals(z.Zone, StringComparison.OrdinalIgnoreCase));
                    _pendingInstanceZone = null;
                    _currentZoneInstanced = SpawnCatalog.IsInstancedZoneName(z.Zone) || announced;
                    _lastLoc = null;
                    // Every zone line ends the current stay, including one that names
                    // the zone you are already in: a zone-enter line you did not travel
                    // for is a gate, a boat or a summon, and either way the player was
                    // somewhere else for the gap that follows.
                    _zoneStay++;
                }
                break;
            case LocationEvent loc:
                // Kill-time /loc = the camp's location (map pins). Zoning clears it.
                lock (_lock) _lastLoc = loc;
                break;
            case KillEvent k:
                OnKill(k);
                break;
            // Lines that prove a creature EXISTS right now — the signal re-kill
            // learning can never see (David camping Baron Telyx, 2026-08-08: a
            // kill-to-kill gap includes the time it takes to notice and kill the
            // spawn, so a timer 25s too long never meets a gap shorter than itself;
            // the mob swinging at you before its chip says DUE is the proof).
            case DamageDealtEvent d:
                OnSighting(d.Target, d.Time);
                break;
            case DamageTakenEvent { Self: false, OverTime: false } dt:
                OnSighting(dt.Attacker, dt.Time);
                break;
            case ThirdMeleeEvent tm:
                OnSighting(tm.Attacker, tm.Time);
                OnSighting(tm.Target, tm.Time);
                break;
            case ConsiderEvent c:
                OnSighting(c.Name, c.Time);
                // The game said it: this mob is rare (#185). Remembered so a later kill
                // can start a discovered timer even when the name fails the article
                // heuristic — "A ghoul executioner - a rare creature -" is articled AND
                // named, and only the game can say so. Session memory, deliberately: a
                // persisted claim would outlive patches that rename or de-rare a mob.
                if (c.Rare) _rareConsidered.Add(Key(Server, _currentZone?.Zone ?? "", c.Name));
                break;
        }
    }

    /// <summary>Only the last stretch of a countdown counts for sightings: several
    /// mobs can share a catalog name (Crushbone taskmasters), and a same-named
    /// stranger acting mid-window must not finish a camp's clock. A sighting inside
    /// the final fifth means the countdown had nearly run anyway — that's this
    /// spawn cycle completing, not a twin.</summary>
    public const double SightingFinalFraction = 0.8;

    /// <summary>A creature with a RUNNING timer was seen acting before its due time:
    /// the respawn provably already happened, so the countdown completes now (the
    /// chip flips DUE and the alert fires through the normal path), and the observed
    /// cycle length becomes a learned override where the precedence rules allow —
    /// manual edits and measured catalog clocks stay untouched, same as re-kill
    /// learning. Exact name matches only: fuzzy is for typo'd kill lines, and a
    /// near-miss name is exactly the false evidence this must never accept.</summary>
    private void OnSighting(string seen, DateTime time)
    {
        lock (_lock)
        {
            if (_currentZone is not { } zone) return;
            foreach (var t in _timers.Values)
            {
                if (!string.Equals(t.Zone, zone.Zone, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(t.Server, Server, StringComparison.OrdinalIgnoreCase)) continue;
                if (t.DurationSeconds is not { } d || t.IsDue(time)) continue;
                var elapsed = (time - t.KilledAt).TotalSeconds;
                if (elapsed < Math.Max(MinLearnSeconds, d * SightingFinalFraction)) continue;
                if (!SpawnCatalog.NameMatches(t.Name, seen)) continue;
                // Multi-spawn names (Royal Guard pops in a number of places — David,
                // 2026-08-09) get NO sighting treatment at all: the acting creature
                // may be any of its siblings, so only kills drive their clocks.
                var entry = zone.Named.FirstOrDefault(e =>
                    e.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase));
                if (entry is { MultiSpawn: true }) return;

                Upsert(t with { DurationSeconds = Math.Floor(elapsed) });
                // The chip completes either way — the mob is provably up where the
                // player is standing — but only a clock the named's OWN death started
                // measures a cycle. On a placeholder-started clock the named may have
                // been up the whole time (Sol A, 2026-08-16), so its elapsed teaches
                // nothing; and across an instance boundary the creature acting now is a
                // different copy from the one that died, so neither does that.
                if (StartedByNamedKill(t, entry) && NeverLeftSince(t))
                    LearnFromSighting(zone, t.Name, elapsed);
                return;
            }
        }
    }

    /// <summary>Sighting evidence outranks every lock except the player's own: a
    /// manual edit is never touched, but a TRUSTED measured clock yields — the mob
    /// provably acting inside the final stretch is a fresher measurement than the
    /// one in the catalog (David's call, 2026-08-09: "for actual nameds I don't want
    /// to lock the timers if we actually observe them being lower"). The Sighted
    /// flag marks the value so the trusted self-heal, which exists to purge re-kill
    /// noise, knows to leave it alone.</summary>
    private void LearnFromSighting(SpawnZone zone, string name, double elapsed)
    {
        // The call site's gates (the named's own kill started the clock, same stay,
        // final stretch) are what make this a cycle observation; the manual-edit and
        // never-loosens returns below are about the COUNTDOWN, not about whether the
        // mob was really seen up at this elapsed.
        if (elapsed >= MinLearnSeconds && elapsed <= MaxDiscoverSeconds)
            _cycles?.Record(Server, zone.Zone, name, Math.Floor(elapsed), "Sighting", DateTime.Now);
        var entry = zone.Named.FirstOrDefault(e =>
            e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var o = _overrides.Find(zone.Zone, name);
        if (o?.RespawnSeconds is not null && !o.Learned) return;   // manual edit wins
        var current = o?.RespawnSeconds
            ?? (entry is not null ? SpawnCatalog.EffectiveSeconds(zone, entry) : null);
        if (current is { } cur && elapsed >= cur) return;          // never loosens

        var ov = _overrides.GetOrAdd(zone.Zone, name);
        ov.RespawnSeconds = Math.Floor(elapsed);
        ov.Learned = true;
        ov.Sighted = true;
        ov.Imported = false;   // your own eyes, on this camp — no longer a stranger's number
        _overrides.Save();
    }

    private void OnKill(KillEvent k)
    {
        lock (_lock)
        {
            if (_currentZone is not { } zone) return;

            // Two passes: every exact candidate before any fuzzy one, so a typo'd
            // catalog entry can never steal a kill from a correctly-spelled neighbour.
            foreach (var fuzzy in (bool[])[false, true])
            {
                foreach (var entry in zone.Named)
                {
                    var o = _overrides.Find(zone.Zone, entry.Name);
                    var placeholder = o?.Placeholder ?? entry.Placeholder;
                    // Whether the named ITSELF died (name or alias) or only its
                    // placeholder decides what this kill may teach below.
                    var namedKill = Matches(entry.Name, k.Target, fuzzy)
                        || entry.Aliases.Any(a => Matches(a, k.Target, fuzzy));
                    if (!namedKill && !MatchesAnyPlaceholder(placeholder, k.Target, fuzzy)) continue;

                    // Raid-instance bosses, and ANY kill inside an INSTANCED RAID
                    // zone (#109): the kill is real — the Raids card records it — but
                    // it runs on the lockout, not a respawn clock, so no timer starts.
                    // The zone gate catches what the achievements dump can't: minis
                    // and named the dump never lists. Ordinary dungeon instances keep
                    // their timers — mobs respawn there, measurably. A player-typed
                    // duration is the one exception: their edit outranks the
                    // suppression like it outranks everything else.
                    if ((entry.RaidInstanced || (_currentZoneInstanced && zone.RaidZone))
                        && !IsManual(o)) return;

                    // A TRIGGERED spawn (eqlwiki's own word) has no cycle to count and —
                    // the half a suppression alone would miss — no cycle to LEARN. Bee
                    // Island spawns several Bzzzt per clear; two kills three minutes apart
                    // used to "measure" a three-minute respawn, write it to the overrides
                    // file as Learned, and count every later kill down to DUE. That is the
                    // Sky report (#109 follow-up, Frankthetankk), and it is why this sits
                    // BEFORE the learning below rather than beside EffectiveSeconds. A
                    // learned value already in the file heals here, as re-kill noise does.
                    // The player's typed duration still wins, as it wins over everything.
                    if (entry.IsTriggered && !IsManual(o))
                    {
                        if (o is { Learned: true, RespawnSeconds: not null })
                        {
                            o.RespawnSeconds = null;
                            o.Learned = false;
                            o.Imported = false;
                            _overrides.Save();
                        }
                        return;
                    }

                    var trusted = IsTrusted(zone, entry);
                    // Self-heal: a LEARNED override sitting under a measured clock came
                    // from multi-spawn re-kill noise (two taskmasters at different camps
                    // look like one fast respawn) — drop it, the measurement wins.
                    // Sighting-learned values are exempt: those were the mob itself
                    // acting before the measured clock ran out, and an observation
                    // outranks a lock (David, 2026-08-09). On a multiSpawn entry ANY
                    // learned value is noise by definition (siblings poison every
                    // automatic signal — a trainee-restarted clock "measured" the
                    // Trainer at 111s), so those heal unconditionally.
                    if (o is { Learned: true, Sighted: false, RespawnSeconds: { } bad }
                        && (entry.MultiSpawn
                            || (trusted && bad < SpawnCatalog.EffectiveSeconds(zone, entry))))
                    {
                        o.RespawnSeconds = null;
                        o.Learned = false;
                        _overrides.Save();
                        o = _overrides.Find(zone.Zone, entry.Name);
                    }
                    var duration = o?.RespawnSeconds ?? SpawnCatalog.EffectiveSeconds(zone, entry);
                    // Re-kill gaps teach nothing about multi-spawn names either: the
                    // "re"-kill may be a sibling across the zone, not this camp again.
                    // And only a named-kill-to-named-kill gap is a cycle at all — a
                    // placeholder death on either end makes the gap walk time between
                    // two different mobs (Sol A's 93-second EXG, 2026-08-16).
                    if (!trusted && !entry.MultiSpawn && namedKill)
                        duration = LearnFromRekill(zone, entry, k.Time, duration);
                    var (cy, cx) = CampFor(zone.Zone, entry.Name, k.Time);
                    Upsert(new SpawnTimerState(Server, zone.Zone, entry.Name, k.Time, duration)
                        { CampLocY = cy, CampLocX = cx, KilledName = k.Target, ZoneStay = _zoneStay });
                    return;
                }

                foreach (var (name, o) in _overrides.CustomFor(zone.Zone))
                {
                    if (!Matches(name, k.Target, fuzzy)
                        && !Matches(o.Placeholder ?? "", k.Target, fuzzy)) continue;
                    var (ccy, ccx) = CampFor(zone.Zone, name, k.Time);
                    var seconds = o.Discovered
                        ? LearnDiscovered(zone.Zone, name, o, k.Time)
                        : o.RespawnSeconds;
                    Upsert(new SpawnTimerState(Server, zone.Zone, name, k.Time, seconds)
                        { CampLocY = ccy, CampLocX = ccx, KilledName = k.Target, ZoneStay = _zoneStay });
                    return;
                }
            }

            // Nothing in the catalog and nothing the player added — but the log itself
            // says this was a NAMED mob (discussion #185, elderbit: the curated list will
            // always have gaps, and "Chief Goonda" versus "a skeleton" is the game's own
            // convention). Record it so its SECOND death can measure the cycle.
            DiscoverNamed(zone, k);
        }

        static bool Matches(string catalogName, string killed, bool fuzzy) =>
            fuzzy ? SpawnCatalog.NameMatchesFuzzy(catalogName, killed)
                  : SpawnCatalog.NameMatches(catalogName, killed);

        // Some spawn cycles run several placeholders (Queen Dracnia's webmaster /
        // lurker / purifier rotation) — the field holds them '/'-separated, and any
        // one of them dying restarts the named's clock.
        static bool MatchesAnyPlaceholder(string placeholders, string killed, bool fuzzy) =>
            placeholders.Length > 0 && placeholders.Split('/')
                .Any(p => p.Trim() is { Length: > 0 } ph && Matches(ph, killed, fuzzy));
    }

    /// <summary>
    /// True when nothing has happened between this timer starting and now that could
    /// make the two ends different mobs — which comes down to one question: **did the
    /// player leave the zone?**
    ///
    /// Two quite different failures collapse into that one rule.
    ///
    /// **Instances.** Every difficulty of a zone shares one timer key, so killing a named
    /// at D0, changing to your own instance and killing it at the spawn point looks like
    /// a twelve-minute respawn — and the mob never respawned at all, a different copy of
    /// it was standing there (David, 2026-08-20). Changing instance means zoning, so this
    /// catches it without EQBuddy having to reason about which copy is which, or pretend
    /// the zone line names one when it does not.
    ///
    /// **Gaps that simply are not evidence.** "Killed it, went to sell, came back an hour
    /// later, killed it" bounds the respawn at an hour, which is true and worth nothing.
    ///
    /// It costs the one honest case it also refuses — a bank trip in a zone with no
    /// instances at all, where the gap really is a true upper bound. That is a small loss
    /// by construction: such a gap has a whole errand added to it, so it is rarely the
    /// tightest bound seen and rarely the one that would have won. And it errs the right
    /// way. The whole subsystem runs on one asymmetry: **cost a measurement, never a
    /// camp.**
    ///
    /// A timer recovered from the persist file has no stay at all, and no evidence must
    /// never read as agreement — the same rule <see cref="SpawnTimerState.KilledName"/>
    /// follows across a restart.
    ///
    /// Note this decides only what may be LEARNED. Countdowns keep running across a zone
    /// line, because they are still true: the named goes on respawning while you are at
    /// the bank, and your own instance keeps its state while you are away.
    /// </summary>
    private bool NeverLeftSince(SpawnTimerState prev) => prev.ZoneStay == _zoneStay;

    /// <summary>
    /// The first duration a named gets from watching, when nothing — catalog entry, zone
    /// default, player edit — has ever said how long its cycle is. Returns null when this
    /// gap must not become one.
    ///
    /// Two named have no duration to check a gap against: one EQBuddy discovered itself
    /// (#185), and one the catalog lists WITHOUT a respawn in a zone with no default.
    /// There are 126 of the latter shipped today, including all 38 in High Keep and 47 in
    /// Western Wastes — Princess Lenia among them. Until now they behaved worse than an
    /// unlisted mob: a discovered named measures its cycle on the second kill, while
    /// being in the catalog with a blank respawn meant never learning one at all, because
    /// <see cref="LearnFromRekill"/> returns before it starts when there is no current
    /// duration. Being known was worse than being unknown.
    ///
    /// The two bounds are what stand in for the missing sanity check:
    ///
    /// <b>Same stay.</b> Both kills during one continuous visit to the zone. A gap across
    /// a zone trip is still a true upper bound, but here it BECOMES the countdown with
    /// nothing to contradict it, so "killed it, went to Freeport, came back five hours
    /// later" would print a confident five-hour timer. Where a duration already exists
    /// this is not required, and must not be: there the <c>gap &lt; d</c> rule already
    /// keeps a loose bound harmless, and refusing it would throw away real evidence.
    ///
    /// <b>Inside the floor and the ceiling.</b> <see cref="MinLearnSeconds"/> below (that
    /// is multi-spawn noise) and <see cref="MaxDiscoverSeconds"/> above.
    /// </summary>
    private double? FirstDurationFromGap(string zone, string name, DateTime killedAt)
    {
        if (!_timers.TryGetValue(Key(Server, zone, name), out var prev)) return null;
        if (!NeverLeftSince(prev)) return null;
        var gap = (killedAt - prev.KilledAt).TotalSeconds;
        if (gap < MinLearnSeconds || gap > MaxDiscoverSeconds) return null;
        return Math.Floor(gap);
    }

    /// <summary>The widest re-kill gap a named with NO known duration will accept as a
    /// spawn cycle.
    /// Above this the gap is far likelier to be "you went to bed and came back" than a
    /// respawn, and there is no catalog value to sanity-check it against the way
    /// <see cref="LearnFromRekill"/> does once a duration exists. Six hours comfortably
    /// covers the long dungeon named while refusing an overnight gap — and paired with
    /// the same-stay rule in <see cref="FirstDurationFromGap"/> it now means something
    /// stronger than it used to: a five-hour gap is only accepted from a player who
    /// actually sat in the zone for five hours.</summary>
    public const double MaxDiscoverSeconds = 6 * 60 * 60;

    /// <summary>
    /// A proper-named mob the catalog doesn't know (discussion #185). Recorded on its
    /// first death with NO duration — a chip that says "killed 4m ago" and nothing more
    /// is honest, and the second death is what measures the cycle.
    ///
    /// Only YOUR OWN kills seed one. That single gate removes the whole class elderbit
    /// warned about without needing a pet registry: a pet or a player dying arrives as
    /// "X has been slain by Y", never as "You have slain X!", so neither can be mistaken
    /// for a named. It costs group kills where someone else lands the blow, which is the
    /// right way to be wrong here — a missing timer is recoverable, an invented one
    /// teaches you to walk to a camp that isn't up.
    /// </summary>
    private void DiscoverNamed(SpawnZone zone, KillEvent k)
    {
        // Two ways in: the article convention, or the game's own " - a rare creature - "
        // consider marker seen this session (#185, bjstrange). The marker outranks the
        // convention because it is the game speaking rather than a heuristic — an
        // articled rare ("A ghoul executioner") is exactly the gap the convention
        // cannot see. The your-own-kill gate still applies to both.
        var saidRare = _rareConsidered.Contains(Key(Server, zone.Zone, k.Target));
        if ((!k.ProperName && !saidRare) || k.Killer != "You") return;
        // The #109 fence applies here too. It was checked at exactly one line — inside
        // the catalog-entry loop — so a named the catalog does not list, killed inside an
        // instanced raid zone, walked straight around it and was discovered and timed.
        // Found by Fable 5 planning the Sky item (FABLE.md, 2026-08-21), not by a report.
        if (_currentZoneInstanced && zone.RaidZone) return;
        if (_overrides.Find(zone.Zone, k.Target) is not null) return;   // already known here

        // A serial-named trash mob reads exactly like a named: "CWG Model XA" has no
        // article either. But the catalog listing CWG Model EXG and not his siblings IS
        // the statement that the siblings are trash — the same family fact #181 needed
        // to stop them bridging onto his clock. Without this, farming Sol A's clockworks
        // invents a timer per serial number.
        foreach (var entry in zone.Named)
        {
            if (SpawnCatalog.SharesNameFamily(entry.Name, k.Target)) return;
            if (entry.Aliases.Any(a => SpawnCatalog.SharesNameFamily(a, k.Target))) return;
        }

        var ov = _overrides.GetOrAdd(zone.Zone, k.Target);
        ov.Custom = true;          // so CustomFor() finds it on the next kill
        ov.Discovered = true;      // ...and the UI can say EQBuddy added it, not you
        _overrides.Save();
        var (cy, cx) = CampFor(zone.Zone, k.Target, k.Time);
        Upsert(new SpawnTimerState(Server, zone.Zone, k.Target, k.Time, null)
            { CampLocY = cy, CampLocX = cx, KilledName = k.Target, ZoneStay = _zoneStay });
    }

    /// <summary>The second death of a discovered named measures its cycle. Same
    /// never-loosens discipline as <see cref="LearnFromRekill"/>: a later, shorter gap
    /// tightens the value, a longer one is ignored as "you weren't watching".</summary>
    private double? LearnDiscovered(string zone, string name, SpawnOverride o, DateTime killedAt)
    {
        // A player who typed a duration outranks anything measured here.
        if (o is { Learned: false, RespawnSeconds: not null }) return o.RespawnSeconds;

        if (FirstDurationFromGap(zone, name, killedAt) is not { } elapsed) return o.RespawnSeconds;
        // A cycle for the ledger whether or not it tightens the countdown (see
        // LearnFromRekill) — the honesty gates all passed inside FirstDurationFromGap.
        _cycles?.Record(Server, zone, name, elapsed, "Discovered", killedAt);
        if (o.RespawnSeconds is { } current && elapsed >= current) return o.RespawnSeconds;

        o.RespawnSeconds = elapsed;
        o.Learned = true;
        o.Imported = false;
        _overrides.Save();
        return o.RespawnSeconds;
    }

    /// <summary>A MEASURED timer (entry or zone clock) outranks re-kill learning:
    /// shorter gaps against a measurement are multi-spawn noise, not evidence
    /// (David's rule, 2026-08-04). Player-typed edits still outrank everything —
    /// they're checked before this ever matters.</summary>
    private static bool IsTrusted(SpawnZone zone, SpawnEntry entry) =>
        entry.RespawnSeconds is not null ? entry.Trusted : zone.NamedDefaultTrusted;

    /// <summary>Re-kill gaps shorter than the learning floor are treated as multi-spawn
    /// noise (several mobs sharing a name), not as evidence of a faster respawn.</summary>
    public const double MinLearnSeconds = 90;

    /// <summary>
    /// Timers tighten themselves from play (requested by David after a Splitpaw player
    /// reported 22-minute catalog timers against 2–5-minute Legends reality): killing
    /// the same named again SOONER than its timer says is possible proves the respawn
    /// is at most that gap, so the gap becomes a learned override. Manual edits are
    /// never touched, learning never loosens, and learned values keep tightening as
    /// better evidence arrives. Callers only send named kills here; the running timer
    /// must ALSO have been started by the named's own death, or the "gap" spans two
    /// different mobs and measures nothing.
    /// </summary>
    private double? LearnFromRekill(SpawnZone zone, SpawnEntry entry, DateTime killedAt, double? currentDuration)
    {
        if (!_timers.TryGetValue(Key(Server, zone.Zone, entry.Name), out var prev)) return currentDuration;
        if (!StartedByNamedKill(prev, entry)) return currentDuration;

        double learned;
        if (currentDuration is { } d)
        {
            // Not merely loose — meaningless. Every instance of a zone shares one timer
            // key, so a gap spanning an instance change measures two different copies
            // of the mob (David, 2026-08-20).
            if (!NeverLeftSince(prev)) return currentDuration;
            var gap = (killedAt - prev.KilledAt).TotalSeconds;
            if (gap < MinLearnSeconds) return currentDuration;
            // An honest cycle for the LEDGER even when it does not tighten the
            // countdown below: a 12:04 gap against a learned 12:03 is a real observed
            // cycle, and refusing it would keep a stable timer from ever reaching
            // three agreeing cycles. The discovery ceiling keeps out "went to bed".
            if (gap <= MaxDiscoverSeconds)
                _cycles?.Record(Server, zone.Zone, entry.Name, Math.Floor(gap), "Rekill", killedAt);
            if (gap >= d) return currentDuration;
            learned = Math.Floor(gap);
        }
        // Nothing has ever said how long this cycle is — the catalog lists the named
        // with a blank respawn and its zone has no default. Measure it the way a
        // discovered named is measured, under the stricter bounds that stand in for the
        // sanity check a known duration would have provided.
        else if (FirstDurationFromGap(zone.Zone, entry.Name, killedAt) is { } first)
        {
            _cycles?.Record(Server, zone.Zone, entry.Name, first, "Rekill", killedAt);
            learned = first;
        }
        else return currentDuration;

        var o = _overrides.GetOrAdd(zone.Zone, entry.Name);
        if (o.RespawnSeconds is not null && !o.Learned) return currentDuration; // manual edit wins
        o.RespawnSeconds = learned;
        o.Learned = true;
        o.Imported = false;   // measured here, from these kills
        _overrides.Save();
        return o.RespawnSeconds;
    }

    /// <summary>True when this timer's clock was started by the named creature's own
    /// death (name or alias) rather than a placeholder's. Only such clocks measure
    /// the named's cycle; a null <see cref="SpawnTimerState.KilledName"/> (persisted
    /// before the field existed) is no evidence, so it answers false.</summary>
    private static bool StartedByNamedKill(SpawnTimerState t, SpawnEntry? entry) =>
        t.KilledName is { } killed
        && (SpawnCatalog.NameMatches(t.Name, killed)
            || entry?.Aliases.Any(a => SpawnCatalog.NameMatches(a, killed)) == true);

    /// <summary>The ▶ button: the player saw (or heard about) the kill themselves.
    /// <paramref name="elapsed"/> covers "it died five minutes ago".</summary>
    public void StartManual(string zone, string name, double? durationSeconds, TimeSpan elapsed = default)
    {
        lock (_lock)
        {
            // Drop any running timer first: Upsert's replay guard refuses older kill
            // times, which would silently swallow a backdated manual start whenever a
            // (possibly placeholder-started) clock was already running. The player's
            // word always wins. Their attested kill counts as the named's own.
            _timers.Remove(Key(Server, zone, name));
            // …and for the same reason it drops a dismissal: a backdated manual start
            // ("it died five minutes ago") can land before a kill they cleared earlier,
            // and Upsert would swallow it without a word. Silent no-ops are broken.
            _dismissed.Remove(Key(Server, zone, name));
            Upsert(new SpawnTimerState(Server, zone, name, DateTime.Now - elapsed, durationSeconds)
                { KilledName = name, ZoneStay = _zoneStay });
        }
    }

    /// <summary>Re-derives the countdown after a duration edit, from the original kill.</summary>
    public void SetDuration(string zone, string name, double? durationSeconds)
    {
        lock (_lock)
        {
            if (_timers.TryGetValue(Key(Server, zone, name), out var t))
                Upsert(t with { DurationSeconds = durationSeconds });
        }
    }

    /// <summary>The ✕ button. Removing the row is only half of it: the player has
    /// DISMISSED a particular kill, and that decision has to outlive the row.
    ///
    /// #228 (joeymavity), "respawn timers randomly re-open after they've been cleared."
    /// `LogWatcher.Select` is a full-file ingest, so every kill line in the log replays
    /// through <see cref="Apply"/> — and <see cref="Upsert"/> had nothing to consult, so
    /// it rebuilt the timer from the very kill that had just been cleared. "Randomly" is
    /// a restart, a character switch, or anything else that re-selects the log. Trap 20's
    /// family: the state was removed and the decision was not kept.
    ///
    /// So the kill's own timestamp is remembered, and persisted — the replay that does
    /// the damage is the one at STARTUP, so a dismissal held in memory would be forgotten
    /// at exactly the wrong moment. A LATER kill is a real new cycle and still starts a
    /// timer; only the dismissed kill and anything before it stay gone.</summary>
    public void Clear(string zone, string name)
    {
        lock (_lock)
        {
            var key = Key(Server, zone, name);
            _dueSeenAt.Remove(key);
            if (_timers.Remove(key, out var dismissed))
            {
                // The kill being dismissed, not the wall clock: a replay hands us that
                // same KilledAt, and comparing against "when they clicked ✕" would let a
                // kill from earlier in the same log walk back in.
                _dismissed[key] = new Dismissal(dismissed.KilledAt, DateTime.Now);
                SavePersisted();
            }
        }
    }

    /// <summary>How long a timer stays visible after coming due. One minute (David's
    /// call): long enough to see DUE and react, short enough that a camp you walked
    /// away from cleans up after itself instead of nagging.</summary>
    public static readonly TimeSpan DueLinger = TimeSpan.FromSeconds(60);

    /// <summary>A timer whose due moment slid further back than this while nobody was
    /// looking never revives as DUE: past an hour the camp is ancient history, and a
    /// restart (or a laptop waking from a long sleep) should clean it up silently
    /// instead of flashing DUE for something long gone.</summary>
    public static readonly TimeSpan DueRevivalCap = TimeSpan.FromHours(1);

    /// <summary>When each due timer was FIRST returned as due. The linger runs from
    /// observation, not from the due moment alone: the UI ticks once a second while
    /// the process is actually scheduling, and a gap longer than the linger (laptop
    /// sleep, an OS-throttled background process) used to prune a timer that came
    /// due mid-gap before any snapshot ever showed it DUE — the chip vanished
    /// silently and the due alert never fired. In-memory only.</summary>
    private readonly Dictionary<string, DateTime> _dueSeenAt = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One ✕: WHICH kill was dismissed, and WHEN the player said so.
    ///
    /// Both dates are needed and they answer different questions. `KilledAt` is what a
    /// replayed kill line is compared against — the wall clock at the moment of the click
    /// would let an earlier kill from the same log walk straight back in. `DismissedAt`
    /// is what the record is aged out on, because "remember this decision for 30 days"
    /// is a statement about the decision, not about the mob: pruning on the kill time
    /// would throw away a dismissal of an old kill the instant it was made.</summary>
    private sealed record Dismissal(DateTime KilledAt, DateTime DismissedAt);

    /// <summary>Names the game itself called " - a rare creature - " in a consider this
    /// session, by timer key (#185). Read by <see cref="DiscoverNamed"/>.</summary>
    private readonly HashSet<string> _rareConsidered = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Kills the player has dismissed with ✕, by timer key (#228). Persisted
    /// beside the timers, because the replay this defends against is the full-file
    /// ingest at startup.</summary>
    private readonly Dictionary<string, Dismissal> _dismissed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How long a dismissal is worth keeping after it is made. Long enough that
    /// no realistic replay outlives it, short enough that a long-lived profile stays
    /// tidy.</summary>
    private static readonly TimeSpan DismissalLifetime = TimeSpan.FromDays(30);

    /// <summary>Current timers for this server, expired ones pruned. A due timer shows
    /// DUE for <see cref="DueLinger"/> from the first snapshot that saw it due, then
    /// drops on its own — if nobody clicked it away within a minute, they've moved on.</summary>
    public List<SpawnTimerState> Snapshot(DateTime now)
    {
        lock (_lock)
        {
            var stale = _timers.Values.Where(t => IsStale(t, now)).ToList();
            if (stale.Count > 0)
            {
                foreach (var t in stale)
                {
                    _timers.Remove(Key(t.Server, t.Zone, t.Name));
                    _dueSeenAt.Remove(Key(t.Server, t.Zone, t.Name));
                }
                SavePersisted();
            }
            var list = _timers.Values
                .Where(t => string.Equals(t.Server, Server, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.DueAt ?? DateTime.MaxValue)
                .ToList();
            foreach (var t in list)
                if (t.IsDue(now)) _dueSeenAt.TryAdd(Key(t.Server, t.Zone, t.Name), now);
            return list;
        }
    }

    private bool IsStale(SpawnTimerState t, DateTime now)
    {
        if (t.DueAt is not { } due)
            // No duration known: the row only says "killed N ago" — keep it a day.
            return now - t.KilledAt > TimeSpan.FromHours(24);
        if (now - due <= DueLinger) return false;      // the normal linger, always honored
        if (now - due > DueRevivalCap) return true;    // ancient — never revives
        // Past the linger but inside the cap: only prune once the DUE state has been
        // VISIBLE for a full linger, so a tick gap can't swallow the chip and alert.
        return _dueSeenAt.TryGetValue(Key(t.Server, t.Zone, t.Name), out var seen)
            && now - seen > DueLinger;
    }

    private static string Key(string server, string zone, string name) => $"{server}|{zone}|{name}";

    /// <summary>A duration the player TYPED (not learned) — the one signal that
    /// outranks raid-instance suppression, exactly as it outranks learning.</summary>
    private static bool IsManual(SpawnOverride? o) =>
        o?.RespawnSeconds is not null && !o.Learned;

    /// <summary>True when a countdown for this zone/name shouldn't exist at all: a
    /// raid-instance boss or a triggered spawn, with no player-typed duration. Persisted
    /// timers from before either rule heal through this at load.</summary>
    private bool SuppressedByCatalog(string zoneName, string name)
    {
        var zone = _catalog.FindZone(zoneName);
        var entry = zone?.Named.FirstOrDefault(e =>
            e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return entry is { RaidInstanced: true } or { IsTriggered: true }
            && !IsManual(_overrides.Find(zoneName, name));
    }

    private void Upsert(SpawnTimerState t)
    {
        var key = Key(t.Server, t.Zone, t.Name);
        // A kill the player dismissed stays dismissed, however many times the log is
        // read again (#228). Strictly older kills go too — a full-file ingest replays
        // the whole camp, not just the last one.
        if (_dismissed.TryGetValue(key, out var dismissal))
        {
            if (t.KilledAt <= dismissal.KilledAt) return;
            // A newer kill is a real cycle: the dismissal has done its job and must not
            // outlive it, or it would sit in the file arguing with every future kill.
            _dismissed.Remove(key);
        }
        // Replays hand us the same kill again — identical state must not thrash the
        // persistence file. An OLDER kill never overwrites a newer one (a truncated log
        // replayed after a manual start, for example).
        if (_timers.TryGetValue(key, out var existing))
        {
            if (existing == t) return;
            if (t.KilledAt < existing.KilledAt) return;
        }
        _timers[key] = t;
        // A fresh countdown starts unobserved — the previous cycle's DUE sighting
        // must not shorten this one's linger.
        _dueSeenAt.Remove(key);
        SavePersisted();
    }

    // -- persistence: for timers that outlive the log (raid targets, auto-emptied logs) --

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The dismissals file, beside the timers one. A SIBLING rather than a new
    /// shape for `spawn-timers.json`, so an older build reading this profile still finds
    /// exactly the list it expects and simply does not know about dismissals.</summary>
    private string? DismissedPath => _persistPath is null
        ? null
        : Path.Combine(Path.GetDirectoryName(_persistPath)!, "spawn-dismissed.json");

    private void LoadDismissed()
    {
        if (DismissedPath is not { } path || !File.Exists(path)) return;
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, Dismissal>>(
                File.ReadAllText(path), JsonOpts);
            if (map is null) return;
            var cutoff = DateTime.Now - DismissalLifetime;
            foreach (var (key, d) in map)
                if (d.DismissedAt > cutoff) _dismissed[key] = d;
        }
        catch { /* corrupt file loses dismissals, not the feature */ }
    }

    private void SaveDismissed()
    {
        if (DismissedPath is not { } path) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(_dismissed, JsonOpts));
        }
        catch { /* read-only disk: dismissals just won't survive a restart */ }
    }

    private void LoadPersisted()
    {
        LoadDismissed();
        if (_persistPath is null || !File.Exists(_persistPath)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<SpawnTimerState>>(
                File.ReadAllText(_persistPath), JsonOpts);
            if (list is null) return;
            foreach (var t in list)
            {
                // Countdowns persisted before raid-instance suppression (#109) —
                // Frank's Maestro at "8:13:38" — or before triggered spawns existed
                // (the Sky follow-up) — drop here instead of running for days more.
                // Manual-duration timers stay: the player asked for them.
                if (SuppressedByCatalog(t.Zone, t.Name)) continue;
                _timers[Key(t.Server, t.Zone, t.Name)] = t;
            }
        }
        catch { /* corrupt file loses timers, not the feature */ }
    }

    /// <summary>Drop LEARNED durations sitting on entries that can have no cycle at all —
    /// raid-instanced or triggered. <see cref="OnKill"/> already heals one when the mob
    /// next dies, and <see cref="LoadPersisted"/> drops the persisted TIMER, but neither
    /// touched the override itself: until that next kill the row printed the poisoned
    /// value ("3m") in its duration box beside the word "triggered", contradicting itself
    /// on screen. Frankthetankk's own file is that case, so waiting for a kill to tidy it
    /// is waiting on the person who reported it.
    ///
    /// Fable 5 raised this as a labelled hypothesis in the H4 last-look; it reproduced.
    /// A player-TYPED duration is untouched, here as everywhere.
    ///
    /// **MULTI-SPAWN entries were added on 2026-08-23 (#109's four-bee follow-up), and the
    /// argument is the same one a step further.** The learner already refuses these — a
    /// gap between two kills of a name that several creatures share is two creatures, not a
    /// respawn — so a `Learned` value sitting on one is, by definition, a number the current
    /// code could not produce. It came from an older build, and it is the exact failure this
    /// discussion opened with: two Bzzzt three minutes apart became a three-minute timer that
    /// went DUE forever. Bzzazzt needed it specifically, because it is the one bee eqlwiki
    /// gives a real clock (12 hours) — so it is NOT triggered, and the two rules above would
    /// have left its poisoned value in place.
    ///
    /// The cost is bounded and worth naming: a player who learned a genuinely useful number
    /// for a multi-spawn camp loses it and falls back to the catalog's own figure, or to the
    /// zone default where there is none. That is the right way round — a wrong respawn timer
    /// is worse than none, and their own typed duration still outranks all of this.</summary>
    private void HealSuppressedOverrides()
    {
        var healed = false;
        foreach (var zone in _catalog.Zones)
            foreach (var entry in zone.Named)
            {
                if (entry is not ({ RaidInstanced: true } or { IsTriggered: true }
                        or { MultiSpawn: true })) continue;
                if (_overrides.Find(zone.Zone, entry.Name) is not
                    { Learned: true, RespawnSeconds: not null } o) continue;
                o.RespawnSeconds = null;
                o.Learned = false;
                o.Imported = false;
                healed = true;
            }
        if (healed) _overrides.Save();
    }

    private void SavePersisted()
    {
        // Written together so the two files can never disagree about a kill: every path
        // that removes or adds a timer is also the path that settles its dismissal.
        SaveDismissed();
        if (_persistPath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_persistPath)!);
            File.WriteAllText(_persistPath, JsonSerializer.Serialize(_timers.Values.ToList(), JsonOpts));
        }
        catch { /* read-only disk: timers just won't survive a restart */ }
    }
}
