using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>
/// Durable per-character ledger of quest-relevant items (quest-ledger.json in appdata),
/// same shape of promise as <see cref="AaLedgerStore"/>: the session rebuilds from log
/// replay, but the janitor truncates logs — this store is what still knows you looted
/// four Bone Chips in July. Two buckets per item:
///
///   Looted — accumulated from the log, replay-safe via a per-item time high-water mark
///   (a record only lands when its log timestamp is strictly newer than the last one
///   accepted, so the full-log replay every launch re-offers the same events and they
///   all bounce). Cost: a second identical loot in the same one-second log stamp is
///   dropped — rare, and strictly better than doubling on every restart.
///
///   Manual — "I already had this before EQBuddy" (David's spec, 2026-08-07): the user
///   types a count in the Quest Tracker; set to zero to forget it.
///
/// Only items the filter admits are stored (the UI wires the quest catalog's
/// IsQuestItem), so the file stays quest-sized instead of hoarding every rat whisker.
/// </summary>
public sealed class QuestLedgerStore
{
    public sealed class Entry
    {
        public int Looted { get; set; }
        public int Manual { get; set; }
        /// <summary>Items the log saw leave: merchant sales, destroys, and merges (two
        /// become one). Hand-ins still aren't logged — that stays the ✔ click.</summary>
        public int Consumed { get; set; }
        public DateTime LastTime { get; set; }
        public int Total => Math.Max(0, Looted + Manual - Consumed);
    }

    /// <summary>One character's slice: owned items plus the quests they chose to 📌-track
    /// (tracked quests show in the Quest Tracker even before any item overlaps).</summary>
    public sealed class CharacterLedger
    {
        public Dictionary<string, Entry> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Tracked { get; set; } = [];
        /// <summary>Quests dismissed as "not interested" — excluded from the overlap
        /// view, and items only THEY want stop tinting green in the Loot views.</summary>
        public List<string> Hidden { get; set; } = [];
        /// <summary>Quest name → how many times it's been marked completed.</summary>
        public Dictionary<string, int> Completed { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>The character's classes for quest filtering — Legends allows up to
        /// three active classes (David, 2026-08-07), and a character's classes don't
        /// change per session, so the selection belongs to the character.</summary>
        public List<string> Classes { get; set; } = [];

        /// <summary>Classes the character's achievements dump says they HOLD — the game's
        /// own statement, as opposed to <see cref="Classes"/>, which is the player's
        /// filter. Written by both import paths (the ⚙ menu's Import achievements and the
        /// automatic one that fires when the game announces a dump); read through
        /// <see cref="CharacterClasses.Resolve"/>. Two writers named on purpose — trap 20
        /// is what happens when a setting has readers and no writer, and #204/#210/#212
        /// were all one import path being missed.</summary>
        public List<string> UnlockedClasses { get; set; } = [];
        /// <summary>Last level the log announced ("Welcome to level N!"), 0 = never
        /// seen. The log states the number only at the ding itself, so the level-unlock
        /// preview needs this to survive restarts (and log truncation).</summary>
        public int Level { get; set; }
    }

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, CharacterLedger> _byCharacter;

    /// <summary>Admits items into the ledger; default admits nothing until the UI wires
    /// the catalog in — a ledger that can't identify quest items shouldn't guess.</summary>
    public Func<string, bool> TrackFilter { get; set; } = _ => false;

    /// <summary>Canonicalizes item names before storing/matching — wired to
    /// QuestCatalog.BaseItemName so "Crushbone Shoulderpads +2" counts toward the quest
    /// that wants plain "Crushbone Shoulderpads". Identity by default.</summary>
    public Func<string, string> Normalize { get; set; } = s => s;

    /// <summary>Bump when the counting rules change enough that stored loot counters
    /// are wrong. v2: sales/merges/destroys subtract, loot-merge lines net zero (David,
    /// 2026-08-07: "ready ×17" counted every merge-consumed belt). On mismatch the
    /// LOG-DERIVED counters reset (Looted/Consumed/LastTime) so the next full-log
    /// replay rebuilds them under the current rules; manual counts, pins, hides,
    /// completions, and classes are user statements and always survive.</summary>
    private const int CountingRulesVersion = 2;

    public QuestLedgerStore(string path)
    {
        _path = path;
        _byCharacter = Load(path);
        ResetCountersIfRulesChanged();
    }

    private void ResetCountersIfRulesChanged()
    {
        var marker = _path + ".rules";
        try
        {
            if (File.Exists(marker) &&
                int.TryParse(File.ReadAllText(marker).Trim(), out var v) &&
                v >= CountingRulesVersion)
                return;
            foreach (var entry in _byCharacter.Values.SelectMany(c => c.Items.Values))
            {
                entry.Looted = 0;
                entry.Consumed = 0;
                entry.LastTime = DateTime.MinValue;
            }
            Save();
            File.WriteAllText(marker, CountingRulesVersion.ToString());
        }
        catch (Exception ex) { CoreLog.Error(ex); }
    }

    private static Dictionary<string, CharacterLedger> Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new(StringComparer.OrdinalIgnoreCase);
            var text = File.ReadAllText(path);
            if (JsonSerializer.Deserialize<Dictionary<string, CharacterLedger>>(text) is { } stored)
            {
                // The pre-tracking shape (char → item → entry) parses into this type
                // WITHOUT error — unknown item-name properties are silently ignored,
                // leaving every character empty. Empty-but-nonempty-file means old shape:
                // reparse it and carry the items over (no tracked quests existed yet).
                if (stored.Count > 0
                    && stored.Values.All(c => c.Items.Count == 0 && c.Tracked.Count == 0
                                              && c.Hidden.Count == 0 && c.Completed.Count == 0
                                              && c.Classes.Count == 0 && c.Level == 0
                                              && c.UnlockedClasses.Count == 0))
                {
                    try
                    {
                        if (JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Entry>>>(text)
                            is { } old && old.Values.Any(items => items.Count > 0))
                            return Rekey(old.ToDictionary(
                                kv => kv.Key, kv => new CharacterLedger { Items = kv.Value }));
                    }
                    catch (JsonException) { /* genuinely-empty new-shape file */ }
                }
                return Rekey(stored);
            }
        }
        catch (Exception ex) { CoreLog.Error(ex); }   // corrupt store: start over, don't crash
        return new(StringComparer.OrdinalIgnoreCase);

        static Dictionary<string, CharacterLedger> Rekey(Dictionary<string, CharacterLedger> stored) =>
            new(stored.ToDictionary(
                    kv => kv.Key,
                    kv => new CharacterLedger
                    {
                        Items = new Dictionary<string, Entry>(kv.Value.Items, StringComparer.OrdinalIgnoreCase),
                        Tracked = kv.Value.Tracked,
                        Hidden = kv.Value.Hidden,
                        Completed = new Dictionary<string, int>(kv.Value.Completed, StringComparer.OrdinalIgnoreCase),
                        Classes = kv.Value.Classes,
                        UnlockedClasses = kv.Value.UnlockedClasses,
                        Level = kv.Value.Level,
                    }),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Offer a loot event. Ignored unless the filter admits the item and the
    /// timestamp beats the item's high-water mark (see class remarks).</summary>
    public void RecordLoot(string characterKey, string item, int count, DateTime time)
    {
        item = Normalize(item);
        if (characterKey.Length == 0 || count <= 0 || !TrackFilter(item)) return;
        lock (_lock)
        {
            var entry = EntryFor(characterKey, item);
            if (time <= entry.LastTime) return;
            entry.Looted += count;
            entry.LastTime = time;
            Save();
        }
    }

    /// <summary>The item left the world: a merchant sale, a destroy, or a merge (two
    /// tiers became one). Same filter, normalization, and replay-safe time gate as
    /// <see cref="RecordLoot"/> — the startup replay re-offers these too.</summary>
    public void RecordConsumed(string characterKey, string item, int count, DateTime time)
    {
        item = Normalize(item);
        if (characterKey.Length == 0 || count <= 0 || !TrackFilter(item)) return;
        lock (_lock)
        {
            var entry = EntryFor(characterKey, item);
            if (time <= entry.LastTime) return;
            entry.Consumed += count;
            entry.LastTime = time;
            Save();
        }
    }

    /// <summary>Set the manual adjustment: positive = "already had these before EQBuddy",
    /// negative = a hand-in offset against the looted history (clamped so Total never
    /// goes below zero — you can't owe the ledger items). Zero removes an entry with no
    /// looted history. Manual entries bypass the filter — the user typing a name is its
    /// own statement of relevance.</summary>
    public void SetManual(string characterKey, string item, int count)
    {
        item = Normalize(item);
        if (characterKey.Length == 0 || item.Trim().Length == 0) return;
        lock (_lock)
        {
            var entry = EntryFor(characterKey, item.Trim());
            entry.Manual = Math.Max(count, -entry.Looted);
            if (entry is { Manual: 0, Looted: 0 })
                _byCharacter[characterKey].Items.Remove(item.Trim());
            Save();
        }
    }

    /// <summary>Item → owned counts for one character (copy; empty when unknown).</summary>
    public Dictionary<string, Entry> For(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c)
                ? c.Items.ToDictionary(kv => kv.Key,
                    kv => new Entry
                    {
                        Looted = kv.Value.Looted, Manual = kv.Value.Manual,
                        Consumed = kv.Value.Consumed, LastTime = kv.Value.LastTime,
                    },
                    StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Quests this character 📌-tracks (copy; empty when unknown).</summary>
    public HashSet<string> TrackedFor(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c)
                ? new HashSet<string>(c.Tracked, StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.OrdinalIgnoreCase);
    }

    public void SetTracked(string characterKey, string questName, bool tracked)
        => SetMembership(characterKey, questName, tracked, c => c.Tracked,
            removeFrom: c => c.Hidden);   // pinning a quest un-hides it — they contradict

    /// <summary>Quests this character dismissed (copy; empty when unknown).</summary>
    public HashSet<string> HiddenFor(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c)
                ? new HashSet<string>(c.Hidden, StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.OrdinalIgnoreCase);
    }

    public void SetHidden(string characterKey, string questName, bool hidden)
        => SetMembership(characterKey, questName, hidden, c => c.Hidden,
            removeFrom: c => c.Tracked);  // hiding a quest un-pins it

    private void SetMembership(string characterKey, string questName, bool member,
        Func<CharacterLedger, List<string>> list, Func<CharacterLedger, List<string>> removeFrom)
    {
        if (characterKey.Length == 0 || questName.Length == 0) return;
        lock (_lock)
        {
            var c = CharacterFor(characterKey);
            var target = list(c);
            var has = target.Contains(questName, StringComparer.OrdinalIgnoreCase);
            if (member == has) return;
            if (member)
            {
                target.Add(questName);
                removeFrom(c).RemoveAll(q => q.Equals(questName, StringComparison.OrdinalIgnoreCase));
            }
            else target.RemoveAll(q => q.Equals(questName, StringComparison.OrdinalIgnoreCase));
            Save();
        }
    }

    /// <summary>Quest → completion count for one character (copy; empty when unknown).</summary>
    public Dictionary<string, int> CompletedFor(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c)
                ? new Dictionary<string, int>(c.Completed, StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Mark one completion: consumes one set of turn-ins (each item's manual
    /// offset drops by its quantity, clamped so Total never goes negative) and bumps the
    /// quest's completed count. The hand-in and the bookkeeping are one gesture — the
    /// log never records turn-ins, so this button is the log (David, 2026-08-07).</summary>
    public void RecordCompletion(string characterKey, string questName,
        IEnumerable<QuestItemNeed> consume)
    {
        if (characterKey.Length == 0 || questName.Length == 0) return;
        lock (_lock)
        {
            var c = CharacterFor(characterKey);
            foreach (var item in consume)
            {
                var entry = EntryFor(characterKey, item.Name);
                entry.Manual = Math.Max(entry.Manual - item.Qty, -entry.Looted);
            }
            c.Completed[questName] = c.Completed.TryGetValue(questName, out var n) ? n + 1 : 1;
            Save();
        }
    }

    /// <summary>Catch-up marking and its undo (David, 2026-08-11): a returning player
    /// checks off history from any card — no turn-in items consumed, unlike
    /// <see cref="RecordCompletion"/> — and unmarking backs a misclick out.</summary>
    public void SetCompleted(string characterKey, string questName, bool done)
    {
        if (characterKey.Length == 0 || questName.Length == 0) return;
        lock (_lock)
        {
            var c = CharacterFor(characterKey);
            if (done) c.Completed[questName] = Math.Max(1, c.Completed.GetValueOrDefault(questName));
            else c.Completed.Remove(questName);
            Save();
        }
    }

    /// <summary>The character's selected classes for quest filtering (copy).</summary>
    public List<string> ClassesFor(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c) ? [.. c.Classes] : [];
    }

    /// <summary>What the achievements dump said this character holds (copy).</summary>
    public List<string> UnlockedClassesFor(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c) ? [.. c.UnlockedClasses] : [];
    }

    /// <summary>Record the dump's class list. Empty is IGNORED rather than stored: a dump
    /// that parsed badly, or one taken before any unlock completed, must not erase a list
    /// the game gave us earlier — the same reasoning that keeps manual counts and picks
    /// surviving a counting-rules reset.</summary>
    public void SetUnlockedClasses(string characterKey, IEnumerable<string> classes)
    {
        if (characterKey.Length == 0) return;
        var list = classes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count == 0) return;
        lock (_lock)
        {
            CharacterFor(characterKey).UnlockedClasses = list;
            Save();
        }
    }

    public void SetClasses(string characterKey, IEnumerable<string> classes)
    {
        if (characterKey.Length == 0) return;
        lock (_lock)
        {
            CharacterFor(characterKey).Classes =
                classes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Save();
        }
    }

    /// <summary>Last announced level for this character (0 = unknown).</summary>
    public int LevelFor(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c) ? c.Level : 0;
    }

    /// <summary>Record the level the log just announced. Stores what the log said, not
    /// a max — the announcement line only fires on gains, so it's already monotonic
    /// per character. Idempotent on the same level (launch replay re-offers dings).</summary>
    public void SetLevel(string characterKey, int level)
    {
        if (characterKey.Length == 0 || level <= 0) return;
        lock (_lock)
        {
            var c = CharacterFor(characterKey);
            if (c.Level == level) return;
            c.Level = level;
            Save();
        }
    }

    private CharacterLedger CharacterFor(string characterKey)
    {
        if (!_byCharacter.TryGetValue(characterKey, out var c))
            _byCharacter[characterKey] = c = new CharacterLedger();
        return c;
    }

    private Entry EntryFor(string characterKey, string item)
    {
        var c = CharacterFor(characterKey);
        if (!c.Items.TryGetValue(item, out var entry))
            c.Items[item] = entry = new Entry();
        return entry;
    }

    private int _savePending;

    /// <summary>Mark dirty and schedule ONE write ~2 s out (perf audit #3: every
    /// quest loot used to serialize the whole ledger synchronously inside the ingest
    /// lock — N full-file writes during a big replay, and a Defender-scan hitch at
    /// the exact "you looted the thing" moment live). The ledger is replay-safe by
    /// design, so a crash inside the window loses nothing the next launch doesn't
    /// rebuild. Callers already hold <c>_lock</c>; this only flips a flag.</summary>
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

    /// <summary>Write now — hosts call this at exit so the last debounce window
    /// isn't left to the next replay. Serializes under the lock, writes outside it.</summary>
    public void Flush()
    {
        try
        {
            string json;
            lock (_lock)
                json = JsonSerializer.Serialize(_byCharacter, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex) { CoreLog.Error(ex); }
    }
}
