using System.IO;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>
/// Mez durations the player TYPED, keyed by base spell name ("Mesmerization", rank
/// suffix stripped). The same contract spawn timers have had since SPAWN-002: your
/// number outranks anything EQBuddy inferred, for as long as you leave it there.
///
/// **Kept apart from <c>mez-durations.json</c> on purpose, and that is the whole reason
/// this is its own file.** That store is a self-healing CACHE: it is rewritten whenever a
/// clean fade is observed, it silently discards itself when it cannot be parsed
/// ("corrupt store: rewritten on next learn"), and it re-heals suspicious values at load.
/// All of that is right for a value EQBuddy inferred and wrong for one a player sat down
/// and typed. Mixing them would put a player's correction at the mercy of a cache's
/// housekeeping, which is exactly what <see cref="SpawnOverrides"/>' own header warns
/// about for the spawn catalog.
///
/// Learning goes on in the background while a typed value is in place — it just cannot
/// win. So clearing the box falls back to whatever EQBuddy has since observed rather than
/// to whatever it knew on the day you typed over it.
/// </summary>
public sealed class MezOverrides
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly Dictionary<string, double> _bySpell = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _path;
    // Read on the watcher thread (MezTracker resolving a landing) and written on the UI
    // thread (the Options editor) — the same split SpawnOverrides guards itself for.
    private readonly object _sync = new();

    public MezOverrides(string? path = null) => _path = path;

    /// <summary>The player's duration for this spell, or null if they never set one.
    /// Takes an exact or ranked name and answers on the base, so one typed value covers
    /// every rank of the spell — a character casts one rank at a time, and asking someone
    /// to type the same number for "Mesmerization" and "Mesmerization IV" would be a
    /// worse answer than being occasionally stale after an upgrade.</summary>
    public double? Find(string spell)
    {
        var key = SpellCatalog.BaseName(spell);
        lock (_sync) return _bySpell.TryGetValue(key, out var s) ? s : null;
    }

    /// <summary>Set (or, with null, clear) the player's duration for a spell. Clearing
    /// hands the spell back to what EQBuddy has learned or the catalog ships.</summary>
    public void Set(string spell, double? seconds)
    {
        var key = SpellCatalog.BaseName(spell);
        lock (_sync)
        {
            if (seconds is { } s and > 0) _bySpell[key] = s;
            else _bySpell.Remove(key);
        }
        Save();
    }

    /// <summary>Every typed duration, for display. Snapshot semantics.</summary>
    public IReadOnlyDictionary<string, double> All
    {
        get { lock (_sync) return new Dictionary<string, double>(_bySpell, StringComparer.OrdinalIgnoreCase); }
    }

    public static MezOverrides Load(string path)
    {
        var result = new MezOverrides(path);
        try
        {
            if (File.Exists(path))
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, double>>(
                    File.ReadAllText(path), JsonOpts);
                if (map is not null)
                    foreach (var (spell, seconds) in map)
                        // No healing pass, deliberately. The learned store floors, ticks
                        // and rejects values against the catalog because it is guessing;
                        // this file is the player telling us the catalog is wrong, so
                        // "below the catalog base" is a thing they are allowed to say.
                        if (seconds > 0) result._bySpell[SpellCatalog.BaseName(spell)] = seconds;
            }
        }
        catch
        {
            // A corrupt file costs the typed values, not the feature.
        }
        return result;
    }

    public void Save()
    {
        if (_path is null) return;
        try
        {
            string json;
            lock (_sync) json = JsonSerializer.Serialize(_bySpell, JsonOpts);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Read-only disk shouldn't crash the widget; edits just won't persist.
        }
    }
}
