using System.Reflection;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>
/// Raid-target tracking (Companion-parity, our honest cut): the boss list comes from
/// the game's own achievements dump (the "EverQuest: Raids" Conqueror criteria), a
/// per-character ledger records every kill the log witnesses with its date, and the
/// achievements import marks bosses cleared before EQBuddy existed. Difficulty tiers
/// are deliberately ABSENT: neither the log nor the dump names the instance tier, and
/// a badge the data can't back would be a fabrication — if a tier signal turns up in
/// a real log, it slots in then.
/// </summary>
public sealed class RaidTargetCatalog
{
    public sealed class ZoneEntry
    {
        public string Zone { get; set; } = "";
        public string[] Bosses { get; set; } = [];
    }

    private sealed class Root { public List<ZoneEntry> Zones { get; set; } = []; }

    public IReadOnlyList<ZoneEntry> Zones { get; }
    private readonly Dictionary<string, string> _canonicalByFold;

    public RaidTargetCatalog(IEnumerable<ZoneEntry> zones)
    {
        Zones = zones.ToList();
        _canonicalByFold = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var boss in Zones.SelectMany(z => z.Bosses))
            _canonicalByFold[Fold(boss)] = boss;
    }

    public int BossCount => Zones.Sum(z => z.Bosses.Length);

    public bool IsRaidBoss(string creature) =>
        ResolveBoss(creature) is not null;

    /// <summary>The catalog's canonical name for a creature, or null when it is no
    /// raid boss. EQ titles bosses in the log as "Name, Title" — "Innoruuk, Prince
    /// of Hate" (discussion #140, AkevoTheBard) — while the achievements dump keeps
    /// the bare name, so when the full fold misses, the pre-comma head is tried.
    /// The head consults only the catalog: "a servant, of nothing" matches nothing
    /// unless "servant" itself were a listed boss. Every ledger boundary must key
    /// on the name returned here, never the raw log form, or a titled kill and the
    /// Raids card's catalog row split into two records.</summary>
    public string? ResolveBoss(string creature)
    {
        if (_canonicalByFold.TryGetValue(Fold(creature), out var canonical)) return canonical;
        var comma = creature.IndexOf(',');
        return comma > 0 && _canonicalByFold.TryGetValue(Fold(creature[..comma]), out canonical)
            ? canonical
            : null;
    }

    /// <summary>The achievements dump hyphenates where logs and the wiki use spaces
    /// ("Cazic-Thule" vs "Cazic Thule") — fold the difference so a witnessed kill and
    /// the #109 spawn-catalog cross-mark both land regardless of which form arrives.</summary>
    private static string Fold(string name) =>
        LogParser.Normalize(name).Replace('-', ' ');

    public static RaidTargetCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.RaidTargets.json")
            ?? throw new InvalidOperationException("RaidTargets.json missing from resources");
        var root = JsonSerializer.Deserialize<Root>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("RaidTargets.json unreadable");
        return new RaidTargetCatalog(root.Zones);
    }

    public static RaidTargetCatalog Default { get; } = LoadEmbedded();
}

/// <summary>One boss's personal record: kills the log saw, plus the achievement flag
/// the dump import set (covers clears from before EQBuddy).</summary>
public sealed class RaidBossRecord
{
    public int Kills { get; set; }
    public DateTime? FirstKill { get; set; }
    public DateTime? LastKill { get; set; }
    public bool AchievementComplete { get; set; }
    /// <summary>Witnessed kills by instance difficulty, keyed by
    /// <see cref="InstanceTier.StoreKey"/> ("d0".."d4", "open", "instance",
    /// "unknown"). Kills recorded before tiers existed aren't here at all — the
    /// replay high-water skips them, and backfilling would be guessing. The badge
    /// shows the highest difficulty PROVEN, never inferred.</summary>
    public Dictionary<string, int> TierKills { get; set; } = new();

    /// <summary>Highest difficulty (0–4) with a witnessed kill, or null when no
    /// tiered kill has been recorded (old records, open-world kills, imports).</summary>
    public int? HighestDifficulty()
    {
        for (var t = 4; t >= 0; t--)
            if (TierKills.TryGetValue($"d{t}", out var n) && n > 0) return t;
        return null;
    }
}

/// <summary>
/// Per-character raid-kill ledger, fed the parsed event stream like every tracker
/// (replay-safe: each record's own LastKill is its high-water mark, so the startup
/// replay never double-counts kills already recorded). A kill counts when the log
/// SEES the boss die — you were there; who landed the blow is a raid's business,
/// not a scoreboard's.
/// </summary>
public sealed class RaidKillLedger
{
    private readonly RaidTargetCatalog _catalog;
    private readonly string? _path;
    private Dictionary<string, RaidBossRecord> _records = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Written for downgrade safety only (older builds gate on it); the
    /// guard itself is per record — see <see cref="Apply"/> (audit finding 2).</summary>
    private DateTime _highWater = DateTime.MinValue;
    /// <summary>Difficulty of the zone the log is currently inside, from the last
    /// zone-enter line — <see cref="InstanceTier.Unknown"/> until one is seen.
    /// Rebuilt by replay like everything else; only KILLS are high-water gated.</summary>
    private int _currentTier = InstanceTier.Unknown;
    private readonly object _lock = new();

    public event Action? Changed;

    /// <summary>Whose log is being followed — records key on "character|boss" so the
    /// family sharing one install each keep their own clears. Empty = not yet known
    /// (kills land unscoped-but-unshown rather than on the wrong character).</summary>
    public Func<string>? CharacterKey { get; set; }

    private string Key(string boss) =>
        $"{CharacterKey?.Invoke() ?? ""}|{LogParser.Normalize(boss)}";

    public RaidKillLedger(string? path, RaidTargetCatalog? catalog = null)
    {
        _catalog = catalog ?? RaidTargetCatalog.Default;
        _path = path;
        try
        {
            if (path is not null && File.Exists(path))
            {
                var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));
                if (stored is not null)
                {
                    _records = new Dictionary<string, RaidBossRecord>(
                        stored.Records, StringComparer.OrdinalIgnoreCase);
                    _highWater = stored.HighWater;
                    if (MigrateKeysToCanonical()) Save();   // one-time; canonical keys map to themselves
                }
            }
        }
        catch { /* corrupt store: rebuilt from the log's own replay */ }
    }

    /// <summary>One-time load migration (#140 follow-up): records stored before
    /// canonical keying — a log's "Cazic Thule" kill filed under a key the
    /// catalog's "Cazic-Thule" row could never read back — sat invisible to
    /// For(), a silent no-op. Each key's boss segment (after the LAST '|'; the
    /// character key is "name|server") re-keys through ResolveBoss; a key the
    /// catalog cannot resolve is not this migration's to rename and stays put.
    /// When the canonical record already exists the two merge: Kills sum (the
    /// duplicate keys held DISJOINT witnessed events — one kill is only ever
    /// recorded once, under one key), FirstKill takes the earliest, LastKill the
    /// latest, the achievement flag ORs, tiers take the max per tier.</summary>
    private bool MigrateKeysToCanonical()
    {
        var migrated = new Dictionary<string, RaidBossRecord>(StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var (key, rec) in _records)
        {
            var target = key;
            var cut = key.LastIndexOf('|');
            if (cut >= 0 && _catalog.ResolveBoss(key[(cut + 1)..]) is { } canonical)
                target = key[..(cut + 1)] + LogParser.Normalize(canonical);
            if (!string.Equals(target, key, StringComparison.OrdinalIgnoreCase))
                changed = true;
            if (!migrated.TryGetValue(target, out var into))
            {
                migrated[target] = rec;
                continue;
            }
            into.Kills += rec.Kills;
            into.FirstKill = into.FirstKill is { } f && rec.FirstKill is { } g
                ? (f <= g ? f : g) : into.FirstKill ?? rec.FirstKill;
            into.LastKill = into.LastKill is { } a && rec.LastKill is { } b
                ? (a >= b ? a : b) : into.LastKill ?? rec.LastKill;
            into.AchievementComplete |= rec.AchievementComplete;
            foreach (var (tier, n) in rec.TierKills)
                into.TierKills[tier] = Math.Max(into.TierKills.GetValueOrDefault(tier), n);
        }
        if (changed) _records = migrated;
        return changed;
    }

    private sealed class Stored
    {
        public Dictionary<string, RaidBossRecord> Records { get; set; } = new();
        public DateTime HighWater { get; set; }
    }

    public void Apply(GameEvent evt)
    {
        if (evt is ZoneEvent z)
        {
            lock (_lock) _currentTier = InstanceTier.FromZoneName(z.Zone);
            return;
        }
        if (evt is not KillEvent kill) return;
        // Record under the CANONICAL catalog name (#140): the log's "Innoruuk,
        // Prince of Hate" and the Raids card's "Innoruuk" row must share one record.
        if (_catalog.ResolveBoss(kill.Target) is not { } boss) return;
        var key = Key(boss);
        lock (_lock)
        {
            // Replay gate PER character-and-boss record, not one global timestamp
            // (audit finding 2): the global mark dropped a second character's older
            // replayed kills after a switch, and the second of two catalog bosses
            // dying in the same log second even live. This record's own LastKill is
            // exactly "the newest kill of THIS boss THIS ledger already holds" —
            // idempotent under any number of replays. (The one kill it can't tell
            // apart is the same boss dying twice within one log second.) A null
            // LastKill — an achievement-only import row — gates nothing.
            var rec = _records.TryGetValue(key, out var r) ? r : null;
            if (rec is not null && kill.Time <= rec.LastKill) return;   // replayed history
            if (kill.Time > _highWater) _highWater = kill.Time;
            rec ??= _records[key] = new RaidBossRecord();
            rec.Kills++;
            rec.FirstKill ??= kill.Time;
            rec.LastKill = kill.Time;
            var tierKey = InstanceTier.StoreKey(_currentTier);
            rec.TierKills[tierKey] = rec.TierKills.GetValueOrDefault(tierKey) + 1;
            Save();
        }
        Changed?.Invoke();
    }

    /// <summary>Mark bosses the achievements dump says are cleared. Import only ever
    /// adds — an achievement can't be un-earned, and neither can a witnessed kill.</summary>
    public int MarkAchievements(IEnumerable<AchievementEntry> achievements)
    {
        var marked = 0;
        lock (_lock)
        {
            foreach (var a in achievements)
            {
                if (a.Section != "EverQuest: Raids"
                    || !a.Name.StartsWith("Conqueror of ", StringComparison.Ordinal)) continue;
                foreach (var (boss, complete) in a.Criteria)
                {
                    if (!complete) continue;
                    // Same canonical key as witnessed kills — the dump is the
                    // catalog's own source, but folding through ResolveBoss keeps
                    // one record per boss whichever spelling arrives.
                    var key = Key(_catalog.ResolveBoss(boss) ?? boss);
                    var rec = _records.TryGetValue(key, out var r) ? r : _records[key] = new RaidBossRecord();
                    if (!rec.AchievementComplete) { rec.AchievementComplete = true; marked++; }
                }
            }
            if (marked > 0) Save();
        }
        if (marked > 0) Changed?.Invoke();
        return marked;
    }

    /// <summary>Every record currently marked from an achievements dump, by canonical key.
    /// Taken either side of an import so the undo knows exactly which records IT created —
    /// a blanket "unmark everything" would also erase clears from an earlier import the
    /// player is not undoing.</summary>
    public IReadOnlyList<string> AchievementCompleteKeys()
    {
        lock (_lock)
            return [.. _records.Where(kv => kv.Value.AchievementComplete).Select(kv => kv.Key)];
    }

    /// <summary>Undo for <see cref="MarkAchievements"/>: clears the achievement flag on
    /// exactly the keys named, and NEVER touches <see cref="RaidBossRecord.Kills"/> —
    /// a witnessed kill is a thing the log saw happen, and no import may take it away.
    ///
    /// Exists because an import that runs without being asked has to be reversible
    /// (David, 2026-08-20). The manual import needed no undo: the player picked the file.</summary>
    public int UnmarkAchievements(IEnumerable<string> keys)
    {
        var cleared = 0;
        lock (_lock)
        {
            foreach (var key in keys)
                if (_records.TryGetValue(key, out var rec) && rec.AchievementComplete)
                {
                    rec.AchievementComplete = false;
                    cleared++;
                }
            if (cleared > 0) Save();
        }
        if (cleared > 0) Changed?.Invoke();
        return cleared;
    }

    /// <summary>A SNAPSHOT of the boss's record, cloned under the lock — the live
    /// record's TierKills dictionary mutates on the watcher thread, and handing it
    /// to the UI invited a cross-thread enumerate-during-add crash (2026-08-13
    /// review). Scalars alone were torn-read-safe; the dictionary is not.</summary>
    public RaidBossRecord? For(string boss)
    {
        lock (_lock)
        {
            if (!_records.TryGetValue(Key(_catalog.ResolveBoss(boss) ?? boss), out var r)) return null;
            return new RaidBossRecord
            {
                Kills = r.Kills,
                FirstKill = r.FirstKill,
                LastKill = r.LastKill,
                AchievementComplete = r.AchievementComplete,
                TierKills = new Dictionary<string, int>(r.TierKills),
            };
        }
    }

    /// <summary>Defeated = the log saw it die or the achievement says so —
    /// for the current character only.</summary>
    public int DefeatedCount()
    {
        var prefix = $"{CharacterKey?.Invoke() ?? ""}|";
        lock (_lock)
            return _records.Count(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && (kv.Value.Kills > 0 || kv.Value.AchievementComplete));
    }

    private void Save()
    {
        if (_path is not { } path) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(
                new Stored { Records = _records, HighWater = _highWater }));
        }
        catch { /* best-effort */ }
    }
}
