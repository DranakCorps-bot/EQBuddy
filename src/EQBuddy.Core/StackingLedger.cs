using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>
/// Durable per-character spell-stacking ledger (stacking-ledger.json in appdata).
/// Every "(Blocked by Y.)" line is a MEASURED stacking conflict: spell X could not
/// land on this character while Y held its slot — the per-user measured tier of
/// stacking knowledge, as opposed to anything curated. Same shape of promise as
/// <see cref="AaLedgerStore"/> (keyed per character, debounced save, Flush at exit)
/// and the same replay discipline as <see cref="QuestLedgerStore"/>: a per-pair time
/// high-water mark makes the startup full-log replay idempotent. Keys are base spell
/// names (ranks collapse onto one measured pair).
/// </summary>
public sealed class StackingLedgerStore
{
    public sealed class Entry
    {
        public int Count { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
    }

    // character → blocked spell → blocking spell → entry
    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, Dictionary<string, Dictionary<string, Entry>>> _byCharacter;

    public StackingLedgerStore(string path)
    {
        _path = path;
        _byCharacter = Load(path);
    }

    private static Dictionary<string, Dictionary<string, Dictionary<string, Entry>>> Load(string path)
    {
        try
        {
            // ProfileJson, so a file killed mid-write falls back to the previous good copy.
            if (ProfileJson.Read<Dictionary<string, Dictionary<string, Dictionary<string, Entry>>>>(
                    path, null, out var stored) is
                    ProfileReadOutcome.Loaded or ProfileReadOutcome.RecoveredFromBackup &&
                stored is not null)
                // Rebuild every level with case-insensitive comparers — JSON round-trips
                // lose them, and spell names arrive in whatever case the log used.
                return new(stored.ToDictionary(
                        c => c.Key,
                        c => new Dictionary<string, Dictionary<string, Entry>>(
                            c.Value.ToDictionary(
                                s => s.Key,
                                s => new Dictionary<string, Entry>(s.Value, StringComparer.OrdinalIgnoreCase)),
                            StringComparer.OrdinalIgnoreCase)),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) { CoreLog.Error(ex); }   // corrupt store: start over, don't crash
        return new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Record one measured (spell, blockedBy) observation. Ignored unless the
    /// timestamp beats the pair's high-water mark — the full-log replay every launch
    /// re-offers the same events and they all bounce. Cost: a second identical block in
    /// the same one-second log stamp is dropped (the quest-ledger trade, same reasons).</summary>
    public void Record(string characterKey, string spell, string blockedBy, DateTime time)
    {
        spell = SpellCatalog.BaseName(spell);
        blockedBy = SpellCatalog.BaseName(blockedBy);
        if (characterKey.Length == 0 || spell.Length == 0 || blockedBy.Length == 0) return;
        lock (_lock)
        {
            if (!_byCharacter.TryGetValue(characterKey, out var bySpell))
                _byCharacter[characterKey] = bySpell = new(StringComparer.OrdinalIgnoreCase);
            if (!bySpell.TryGetValue(spell, out var byBlocker))
                bySpell[spell] = byBlocker = new(StringComparer.OrdinalIgnoreCase);
            if (!byBlocker.TryGetValue(blockedBy, out var entry))
                byBlocker[blockedBy] = entry = new Entry();
            if (time <= entry.LastSeen) return;
            if (entry.Count == 0) entry.FirstSeen = time;
            entry.Count++;
            entry.LastSeen = time;
            Save();
        }
    }

    /// <summary>Everything measured for one character: blocked spell → (blocker → entry).
    /// A copy — safe to read off the UI thread. Empty when the character is unknown.</summary>
    public Dictionary<string, Dictionary<string, Entry>> For(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var bySpell)
                ? bySpell.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ToDictionary(
                        b => b.Key,
                        b => new Entry
                        {
                            Count = b.Value.Count,
                            FirstSeen = b.Value.FirstSeen,
                            LastSeen = b.Value.LastSeen,
                        },
                        StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The spells measured blocking <paramref name="spell"/> for this character,
    /// most-observed first — the "Blocked by: Chloroplast ×3" tooltip's rows.</summary>
    public List<(string BlockedBy, int Count)> BlockersFor(string characterKey, string spell)
    {
        spell = SpellCatalog.BaseName(spell);
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var bySpell)
                   && bySpell.TryGetValue(spell, out var byBlocker)
                ? byBlocker.OrderByDescending(kv => kv.Value.Count)
                    .Select(kv => (kv.Key, kv.Value.Count)).ToList()
                : [];
    }

    private int _savePending;

    /// <summary>Debounced like QuestLedgerStore.Save (perf audit #3): flag now, one
    /// write ~2 s later, replay rebuilds anything a crash loses. Callers hold _lock.</summary>
    private void Save()
    {
        if (Interlocked.Exchange(ref _savePending, 1) == 1) return;
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            Interlocked.Exchange(ref _savePending, 0);
            Flush();
        });
    }

    /// <summary>Write now — hosts call this at exit. Serializes under the lock,
    /// writes outside it.</summary>
    public void Flush()
    {
        try
        {
            string json;
            lock (_lock)
                json = JsonSerializer.Serialize(_byCharacter, new JsonSerializerOptions { WriteIndented = true });
            ProfileJson.Write(_path, json);
        }
        catch (Exception ex) { CoreLog.Error(ex); }
    }
}
