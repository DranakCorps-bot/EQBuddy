using System.IO;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>One observed respawn cycle: how long, how it was measured, and when.</summary>
/// <param name="Kind">"Rekill" (the named's own death to its next death, same stay),
/// "Sighting" (seen acting inside the final stretch of its own clock), or "Discovered"
/// (the second death of a named the catalog does not list).</param>
public sealed record SpawnCycle(double DurationSeconds, string Kind, DateTime At);

/// <summary>
/// The per-named respawn-cycle ledger (`spawn-cycles.json`) — the evidence store behind
/// the wiki pack's respawn suggestions (Fable 5's plan, 2026-08-22; David's direction:
/// eqlwiki is the SOURCE and EQBuddy is the tool that helps it update).
///
/// **Why it exists at all: the app never kept a SAMPLE.** `SpawnOverride.RespawnSeconds`
/// is the tightest value ever accepted — one number, no count, no spread — and
/// `SuggestRarity` can hold its 10-kill bar only because `MobSummary.Kills` exists. This
/// is the spawn side's equivalent: what `RespawnSuggestion`'s agreement bar counts.
///
/// **Written ONLY by <see cref="SpawnTimers"/>, at the three places a gap passes the
/// honesty gates** (the named's own death started the clock, the player never left the
/// zone, the floor and the discovery ceiling, never a triggered or raid-instanced entry)
/// — so every gate applies by construction. Deliberately INCLUDING gaps the never-loosens
/// rule rejects for the countdown: a 12:04 gap against a learned 12:03 is not a tighter
/// timer, but it is a real observed cycle, and refusing it would make a perfectly stable
/// timer unable to ever reach three agreeing cycles. Imports never write here — a
/// stranger's number is not an observation.
///
/// Its own file with its own lock, never merged into `spawn-overrides.json` (trap 13's
/// shape: it is written from the watcher thread while the overrides file has its own
/// writers). Capped at the last 20 cycles per named, newest kept.
/// </summary>
public sealed class SpawnCycleLedger
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public const int Cap = 20;

    private readonly Dictionary<string, List<SpawnCycle>> _byKey =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _path;
    private readonly object _sync = new();

    public SpawnCycleLedger(string? path = null)
    {
        _path = path;
        if (path is null || !File.Exists(path)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<Dictionary<string, List<SpawnCycle>>>(
                File.ReadAllText(path), JsonOpts);
            if (loaded is not null)
                foreach (var (k, v) in loaded)
                    _byKey[k] = v;
        }
        catch (Exception ex) { CoreLog.Error(ex); }   // a torn file loses history, never the app
    }

    public static string Key(string server, string zone, string name) =>
        $"{server}|{zone}|{name}";

    public void Record(string server, string zone, string name,
        double durationSeconds, string kind, DateTime at)
    {
        lock (_sync)
        {
            var key = Key(server, zone, name);
            if (!_byKey.TryGetValue(key, out var list)) _byKey[key] = list = [];
            list.Add(new SpawnCycle(durationSeconds, kind, at));
            if (list.Count > Cap) list.RemoveRange(0, list.Count - Cap);
            Save();
        }
    }

    public IReadOnlyList<SpawnCycle> For(string server, string zone, string name)
    {
        lock (_sync)
            return _byKey.TryGetValue(Key(server, zone, name), out var list)
                ? [.. list] : [];
    }

    private void Save()
    {
        if (_path is null) return;
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_byKey, JsonOpts)); }
        catch (Exception ex) { CoreLog.Error(ex); }
    }
}
