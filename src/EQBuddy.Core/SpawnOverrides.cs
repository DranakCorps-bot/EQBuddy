using System.IO;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>A player's edits to one named-mob entry. Null/empty means "no opinion —
/// use the catalog"; only what the player actually changed is stored.</summary>
public sealed class SpawnOverride
{
    public double? RespawnSeconds { get; set; }
    public string? Placeholder { get; set; }
    /// <summary>Sound the alert when this named's timer hits zero. Default OFF, matching
    /// watch rules' per-rule 🔊 — the chicklet flipping to DUE is always there; the bell
    /// opts this camp into being heard too.</summary>
    public bool Alert { get; set; }
    /// <summary>What "Default" MEANS for spawn sounds: Alarm (David's call). A camp
    /// popping is the most time-critical thing this app announces, and the gentle
    /// watch-rule ding undersells it — so spawns get their own fixed default rather
    /// than following the Options alert sound.</summary>
    public const string DefaultSound = "Alarm";

    /// <summary>This named's own due sound: "" (the default) maps to
    /// <see cref="DefaultSound"/>, "Off" stays silent, else a built-in name or a
    /// custom file path — per-named so you learn WHICH camp popped without looking
    /// away from the game.</summary>
    public string SoundName { get; set; } = "";
    /// <summary>True for named the player added themselves (not in the catalog) —
    /// zones change, wikis lag, and a family member camping something undocumented
    /// shouldn't have to wait for a release.</summary>
    public bool Custom { get; set; }

    /// <summary>True when EQBUDDY added this entry, not the player: a proper-named mob
    /// the log saw die that the curated catalog does not list (discussion #185, elderbit
    /// — every named in The Hole, Chief Goonda in West Karana). It lives here in the
    /// LEARNED store and never in the shipped catalog, which is still only ever written
    /// by hand. Kept distinct from <see cref="Custom"/> so the UI can say which of you
    /// put it there, and so a discovery can be discarded without touching the player's
    /// own additions.</summary>
    public bool Discovered { get; set; }
    /// <summary>True when <see cref="RespawnSeconds"/> was tightened automatically from
    /// an observed re-kill gap rather than typed by the player. Learned values may keep
    /// tightening as more kills come in; a manual edit (Learned=false with a value) is
    /// never touched — the player's word outranks the app's inference.</summary>
    public bool Learned { get; set; }

    /// <summary>True when the learned value came from a pre-due SIGHTING (the mob
    /// provably acting before its clock ran out) rather than a re-kill gap. Sighted
    /// values survive the trusted-entry self-heal that purges re-kill noise: an
    /// observation outranks a lock (David's call, 2026-08-09 — "for actual nameds I
    /// don't want to lock the timers if we actually observe them being lower").</summary>
    public bool Sighted { get; set; }
}

/// <summary>
/// Player edits to the spawn catalog, persisted separately (spawn-overrides.json) so
/// catalog updates in a release never collide with what the player fixed by observation
/// (SPAWN-002). Keyed "zone|name", case-insensitive.
/// </summary>
public sealed class SpawnOverrides
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly Dictionary<string, SpawnOverride> _byKey =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _path;
    // The dictionary is touched from the watcher thread (SpawnTimers learning on
    // kill/sighting) AND the UI thread (Spawns editor, ZoneShare import) — callers
    // hold different outer locks, so the store guards itself (review 2026-08-13).
    private readonly object _sync = new();

    public SpawnOverrides(string? path = null) => _path = path;

    public static string Key(string zone, string name) => $"{zone}|{name}";

    public SpawnOverride? Find(string zone, string name)
    {
        lock (_sync) return _byKey.TryGetValue(Key(zone, name), out var o) ? o : null;
    }

    public SpawnOverride GetOrAdd(string zone, string name)
    {
        var key = Key(zone, name);
        lock (_sync)
        {
            if (!_byKey.TryGetValue(key, out var o)) _byKey[key] = o = new SpawnOverride();
            return o;
        }
    }

    public void Remove(string zone, string name)
    {
        lock (_sync) _byKey.Remove(Key(zone, name));
    }

    /// <summary>Player-added named for a zone, as (name, override) pairs. Snapshot
    /// semantics — safe to enumerate while another thread edits.</summary>
    public IEnumerable<(string Name, SpawnOverride Override)> CustomFor(string zone)
    {
        var prefix = zone + "|";
        lock (_sync)
            return _byKey
                .Where(kv => kv.Value.Custom
                    && kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(kv => (kv.Key[prefix.Length..], kv.Value))
                .ToList();
    }

    public static SpawnOverrides Load(string path)
    {
        var result = new SpawnOverrides(path);
        try
        {
            if (File.Exists(path))
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, SpawnOverride>>(
                    File.ReadAllText(path), JsonOpts);
                if (map is not null)
                    foreach (var (k, v) in map) result._byKey[k] = v;
            }
        }
        catch
        {
            // A corrupt overrides file costs the edits, not the feature.
        }
        return result;
    }

    public void Save()
    {
        if (_path is null) return;
        try
        {
            string json;
            lock (_sync) json = JsonSerializer.Serialize(_byKey, JsonOpts);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Read-only disk shouldn't crash the widget; edits just won't persist.
        }
    }
}
