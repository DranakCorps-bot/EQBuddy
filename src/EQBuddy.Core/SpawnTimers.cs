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
    private LocationEvent? _lastLoc;

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

    public SpawnTimers(SpawnCatalog catalog, SpawnOverrides overrides, string? persistPath = null)
    {
        _catalog = catalog;
        _overrides = overrides;
        _persistPath = persistPath;
        LoadPersisted();
    }

    /// <summary>Fed alongside SessionStats.Apply from the watcher thread.</summary>
    public void Apply(GameEvent evt)
    {
        switch (evt)
        {
            case ZoneEvent z:
                lock (_lock)
                {
                    _currentZone = _catalog.FindZone(z.Zone);
                    _currentZoneInstanced = SpawnCatalog.IsInstancedZoneName(z.Zone);
                    _lastLoc = null;
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
                // The chip completes either way — the mob is provably up — but only
                // a clock the named's OWN death started measures a cycle. On a
                // placeholder-started clock the named may have been up the whole
                // time (Sol A, 2026-08-16), so its elapsed teaches nothing.
                if (StartedByNamedKill(t, entry)) LearnFromSighting(zone, t.Name, elapsed);
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
                        { CampLocY = cy, CampLocX = cx, KilledName = k.Target });
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
                        { CampLocY = ccy, CampLocX = ccx, KilledName = k.Target });
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

    /// <summary>The widest re-kill gap a DISCOVERED named will accept as a spawn cycle.
    /// Above this the gap is far likelier to be "you went to bed and came back" than a
    /// respawn, and a discovery has no catalog value to be sanity-checked against the
    /// way <see cref="LearnFromRekill"/> does. Six hours comfortably covers the long
    /// dungeon named while refusing an overnight gap.</summary>
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
        if (!k.ProperName || k.Killer != "You") return;
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
            { CampLocY = cy, CampLocX = cx, KilledName = k.Target });
    }

    /// <summary>The second death of a discovered named measures its cycle. Same
    /// never-loosens discipline as <see cref="LearnFromRekill"/>: a later, shorter gap
    /// tightens the value, a longer one is ignored as "you weren't watching".</summary>
    private double? LearnDiscovered(string zone, string name, SpawnOverride o, DateTime killedAt)
    {
        // A player who typed a duration outranks anything measured here.
        if (o is { Learned: false, RespawnSeconds: not null }) return o.RespawnSeconds;

        var previous = _timers.TryGetValue(Key(Server, zone, name), out var t) ? t.KilledAt : (DateTime?)null;
        if (previous is not { } was) return o.RespawnSeconds;
        var elapsed = (killedAt - was).TotalSeconds;
        if (elapsed < MinLearnSeconds || elapsed > MaxDiscoverSeconds) return o.RespawnSeconds;
        if (o.RespawnSeconds is { } current && elapsed >= current) return o.RespawnSeconds;

        o.RespawnSeconds = Math.Floor(elapsed);
        o.Learned = true;
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
        if (currentDuration is not { } d) return currentDuration;
        if (!_timers.TryGetValue(Key(Server, zone.Zone, entry.Name), out var prev)) return currentDuration;
        if (!StartedByNamedKill(prev, entry)) return currentDuration;
        var gap = (killedAt - prev.KilledAt).TotalSeconds;
        if (gap < MinLearnSeconds || gap >= d) return currentDuration;

        var o = _overrides.GetOrAdd(zone.Zone, entry.Name);
        if (o.RespawnSeconds is not null && !o.Learned) return currentDuration; // manual edit wins
        o.RespawnSeconds = Math.Floor(gap);
        o.Learned = true;
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
            Upsert(new SpawnTimerState(Server, zone, name, DateTime.Now - elapsed, durationSeconds)
                { KilledName = name });
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

    public void Clear(string zone, string name)
    {
        lock (_lock)
        {
            if (_timers.Remove(Key(Server, zone, name))) SavePersisted();
        }
    }

    /// <summary>How long a timer stays visible after coming due. One minute (David's
    /// call): long enough to see DUE and react, short enough that a camp you walked
    /// away from cleans up after itself instead of nagging.</summary>
    public static readonly TimeSpan DueLinger = TimeSpan.FromSeconds(60);

    /// <summary>Current timers for this server, expired ones pruned. A due timer shows
    /// DUE for <see cref="DueLinger"/>, then drops on its own — if nobody clicked it
    /// away within a minute, they've moved on.</summary>
    public List<SpawnTimerState> Snapshot(DateTime now)
    {
        lock (_lock)
        {
            var stale = _timers.Values.Where(t => IsStale(t, now)).ToList();
            if (stale.Count > 0)
            {
                foreach (var t in stale) _timers.Remove(Key(t.Server, t.Zone, t.Name));
                SavePersisted();
            }
            return _timers.Values
                .Where(t => string.Equals(t.Server, Server, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.DueAt ?? DateTime.MaxValue)
                .ToList();
        }
    }

    private static bool IsStale(SpawnTimerState t, DateTime now)
    {
        if (t.DueAt is not { } due)
            // No duration known: the row only says "killed N ago" — keep it a day.
            return now - t.KilledAt > TimeSpan.FromHours(24);
        return now - due > DueLinger;
    }

    private static string Key(string server, string zone, string name) => $"{server}|{zone}|{name}";

    /// <summary>A duration the player TYPED (not learned) — the one signal that
    /// outranks raid-instance suppression, exactly as it outranks learning.</summary>
    private static bool IsManual(SpawnOverride? o) =>
        o?.RespawnSeconds is not null && !o.Learned;

    /// <summary>True when a countdown for this zone/name shouldn't exist at all:
    /// a raid-instance boss with no player-typed duration. Persisted timers from
    /// before #109 heal through this at load.</summary>
    private bool SuppressedRaidInstance(string zoneName, string name)
    {
        var zone = _catalog.FindZone(zoneName);
        var entry = zone?.Named.FirstOrDefault(e =>
            e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return entry is { RaidInstanced: true } && !IsManual(_overrides.Find(zoneName, name));
    }

    private void Upsert(SpawnTimerState t)
    {
        var key = Key(t.Server, t.Zone, t.Name);
        // Replays hand us the same kill again — identical state must not thrash the
        // persistence file. An OLDER kill never overwrites a newer one (a truncated log
        // replayed after a manual start, for example).
        if (_timers.TryGetValue(key, out var existing))
        {
            if (existing == t) return;
            if (t.KilledAt < existing.KilledAt) return;
        }
        _timers[key] = t;
        SavePersisted();
    }

    // -- persistence: for timers that outlive the log (raid targets, auto-emptied logs) --

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private void LoadPersisted()
    {
        if (_persistPath is null || !File.Exists(_persistPath)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<SpawnTimerState>>(
                File.ReadAllText(_persistPath), JsonOpts);
            if (list is null) return;
            foreach (var t in list)
            {
                // Countdowns persisted before raid-instance suppression (#109) —
                // Frank's Maestro at "8:13:38" — drop here instead of running for
                // days more. Manual-duration timers stay: the player asked for them.
                if (SuppressedRaidInstance(t.Zone, t.Name)) continue;
                _timers[Key(t.Server, t.Zone, t.Name)] = t;
            }
        }
        catch { /* corrupt file loses timers, not the feature */ }
    }

    private void SavePersisted()
    {
        if (_persistPath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_persistPath)!);
            File.WriteAllText(_persistPath, JsonSerializer.Serialize(_timers.Values.ToList(), JsonOpts));
        }
        catch { /* read-only disk: timers just won't survive a restart */ }
    }
}
