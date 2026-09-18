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
        /// <summary>Items the log saw leave: merchant sales, destroys, merges (two become
        /// one), and hand-ins - EQL does log those, as "You offered ... / You complete the
        /// trade with ..." (Hateborne, 2026-09-18).</summary>
        public int Consumed { get; set; }
        /// <summary>The log has seen this item auto-stored somewhere an inventory dump cannot
        /// see (currency, the tradeskill depot). <see cref="ReconcileInventory"/> leaves such
        /// an entry alone when the dump does not list it - "absent from the dump" means
        /// nothing for an item that never lives in your bags. Since 2026-09-16 that is every
        /// Wind Rune.</summary>
        public bool OffDump { get; set; }
        public DateTime LastTime { get; set; }
        /// <summary>What the player's own <c>/outputfile inventory</c> dump said this
        /// character held, as of <see cref="VerifiedAt"/> (#241, DasGud) — the game's own
        /// statement of possession, strictly better information than a log tally that
        /// cannot see off-log acquisitions or mail. Set only by <see cref="ReconcileInventory"/>, which zeroes
        /// <see cref="Looted"/>, <see cref="Manual"/> and <see cref="Consumed"/> in the same
        /// stroke: the dump supersedes everything derived before it, not just this field.</summary>
        public int Verified { get; set; }
        /// <summary>When the dump behind <see cref="Verified"/> was written — 0001-01-01
        /// (default) means never reconciled.</summary>
        public DateTime VerifiedAt { get; set; }
        public int Total => Math.Max(0, Verified + Looted + Manual - Consumed);
    }

    /// <summary>
    /// One profession skill's standing: the highest value the log has announced, and the
    /// LOG's own timestamp for that announcement (never the moment EQBuddy read the line —
    /// the same discipline <see cref="CharacterLedger.LevelAt"/> keeps).
    /// </summary>
    public sealed class SkillEntry
    {
        public int Value { get; set; }
        public DateTime At { get; set; }
    }

    /// <summary>
    /// One character's manual progress through one <see cref="Guide"/> — the player's own
    /// statement about steps that no log line and no inventory dump can decide.
    ///
    /// <para><b>What is NOT in here is the point.</b> An objective carrying a
    /// <c>RewardKey</c> is a Sky turn-in, and its tick lives where it has always lived
    /// (<c>AppSettings.SkyQuestCompleted</c>, through <c>QuestChecklistLayout.MarkRewardTurnedIn</c>).
    /// The guide reads that store and writes through it; it never copies the tick down here,
    /// because one fact with two sources is trap 4 and the losing side would be whichever
    /// screen the player used second. <c>UI.Shared/GuideProgressRouter</c> is the one door
    /// that decides which store a verb lands in — see its remarks (Fable plan §4).</para>
    ///
    /// <para><b>Done and Skipped contradict.</b> "I did this" and "I am not doing this" are
    /// answers to the same question, so setting either clears the other — the same rule as
    /// <see cref="CharacterLedger.Tracked"/>/<see cref="CharacterLedger.Hidden"/>.</para>
    /// </summary>
    public sealed class GuideProgress
    {
        /// <summary>Objectives the player ticked. Non-reward objectives only.</summary>
        public List<string> DoneObjectiveIds { get; set; } = [];

        /// <summary>Objectives the player struck out — "not doing this one". Kept for reward
        /// objectives TOO, and that is not a duplicate of the turn-in tick: "turned in" and
        /// "skipping this step" are different facts, and only one of them has another
        /// home.</summary>
        public List<string> SkippedObjectiveIds { get; set; } = [];

        /// <summary>When this guide's progress last changed, <b>UTC</b> — a player action's
        /// wall-clock, not a log timestamp like <see cref="Entry.LastTime"/>. Anything that
        /// renders it converts; 0001-01-01 means never touched.</summary>
        public DateTime LastUpdated { get; set; }
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

        /// <summary>Classes the PLAYER set on Character Setup (DRA-66) — their statement
        /// about who the character is, which is neither <see cref="Classes"/> (the quest
        /// filter: may hold a friend's class, #104) nor <see cref="UnlockedClasses"/> (the
        /// game's). While non-empty it silences inference in
        /// <see cref="CharacterClasses.Resolve"/>; empty means "EQBuddy's own reading",
        /// which is why — unlike the dump list — clearing it IS storable.</summary>
        public List<string> StatedClasses { get; set; } = [];
        /// <summary>Last level the log announced ("Welcome to level N!"), 0 = never
        /// seen. The log states the number only at the ding itself, so the level-unlock
        /// preview needs this to survive restarts (and log truncation).</summary>
        public int Level { get; set; }

        /// <summary>When the log announced <see cref="Level"/> — <b>the LOG's own
        /// timestamp</b>, not the moment EQBuddy read the line. 0001-01-01 means a level
        /// stored before this field existed, which <see cref="CharacterLevel.Resolve"/>
        /// treats as the oldest claim there is (see its own note on why that is the right
        /// migration).</summary>
        public DateTime LevelAt { get; set; }

        /// <summary>The level the PLAYER set on the Character room (DRA-71 D3) — their own
        /// statement, which is a different fact from <see cref="Level"/> (the game's). 0
        /// means no statement stands, which is what "Let EQBuddy work it out" writes: unlike
        /// the dump-sourced class list, clearing this IS storable, because the statement is
        /// the thing being cleared rather than the evidence under it.</summary>
        public int StatedLevel { get; set; }

        /// <summary>When the player made that statement — <b>their wall clock, LOCAL</b>,
        /// because <see cref="CharacterLevel.Resolve"/> compares it directly against
        /// <see cref="LevelAt"/>, which is a log timestamp. See <see cref="LevelReading.At"/>
        /// for why this one field is not UTC while <see cref="GuideProgress.LastUpdated"/>
        /// is.</summary>
        public DateTime StatedLevelAt { get; set; }

        /// <summary>Guide id → that guide's manual progress for this character. <b>Per
        /// character</b>, which is where progress always belonged — the per-profile Sky ticks
        /// are a known wart this deliberately does not copy (Fable plan §4).</summary>
        public Dictionary<string, GuideProgress> Guides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// **What the log says this character's PROFESSION skills stand at** — skill name in
        /// the log's own spelling → the highest value seen and when (DRA-71 D8, plan P13).
        ///
        /// <para><b>It exists because a skill value died with the session.</b>
        /// <c>StatsSnapshot.SkillUps</c> has always carried this evening's skill-ups and
        /// nothing has ever remembered them, so a player who raised Blacksmithing to 122 last
        /// week and opened EQBuddy today had a tool that knew nothing about it. This is the
        /// same promise the class holds for the announced level one field up: the log states a
        /// number once, and a store is what makes it survive the restart and the janitor.</para>
        ///
        /// <para><b>Only the eight professions land here</b>
        /// (<see cref="Tradeskills.IsProfessionSkill"/>), which is
        /// <see cref="QuestLedgerStore.TrackFilter"/>'s rule applied to a second kind of row:
        /// a ledger admits what a surface can answer about, so the file stays
        /// profession-sized rather than storing sixty combat skills nothing reads. Widening it
        /// is a decision for the slice that builds the surface — writing rows now for a reader
        /// that does not exist is the app doing something and telling nobody (trap 43).</para>
        /// </summary>
        public Dictionary<string, SkillEntry> Skills { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The <c>writtenAt</c> of the last inventory dump reconciled onto this
        /// character — the watermark <see cref="ReconcileInventory"/> checks so a replayed
        /// or repeated announcement (launch replay, a second `/outputfile inventory` with
        /// nothing new) is a no-op rather than a second reset. 0001-01-01 = never.</summary>
        public DateTime LastInventoryReconcile { get; set; }
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
    /// completions, classes and guide progress are user statements and always survive.
    /// A guide tick is not a counter the replay can rebuild — nothing in the log knows
    /// the player walked to the isle — so a rules bump that erased one would be a
    /// silent loss with no way back.</summary>
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
                                              && c.StatedLevel == 0
                                              && c.UnlockedClasses.Count == 0
                                              && c.StatedClasses.Count == 0
                                              && c.Guides.Count == 0
                                              && c.Skills.Count == 0))
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

        // Rebuilds every character so the name-keyed dictionaries become case-insensitive
        // ones. It is a HAND-WRITTEN copy, which means a property added to CharacterLedger
        // and forgotten here is silently dropped on the next launch — the player's tick
        // "just not there" after a restart, with nothing logged. LedgerRoundTripTests walks
        // the type by reflection so the next field cannot go missing quietly.
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
                        StatedClasses = kv.Value.StatedClasses,
                        Level = kv.Value.Level,
                        LevelAt = kv.Value.LevelAt,
                        StatedLevel = kv.Value.StatedLevel,
                        StatedLevelAt = kv.Value.StatedLevelAt,
                        Guides = new Dictionary<string, GuideProgress>(kv.Value.Guides, StringComparer.OrdinalIgnoreCase),
                        Skills = new Dictionary<string, SkillEntry>(kv.Value.Skills, StringComparer.OrdinalIgnoreCase),
                        LastInventoryReconcile = kv.Value.LastInventoryReconcile,
                    }),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Offer a loot event. Ignored unless the filter admits the item and the
    /// timestamp beats the item's high-water mark (see class remarks).</summary>
    /// <param name="offDump">The line said the item was auto-stored where an inventory
    /// dump cannot see it. Learned BEFORE the time gate, so a replayed line teaches it
    /// too.</param>
    /// <returns>True only when the loot was NEW - the replay-safe signal the Sky and Epic
    /// loot auto-ticks key on, so a re-read log cannot tick a checklist twice.</returns>
    public bool RecordLoot(string characterKey, string item, int count, DateTime time,
        bool offDump = false)
    {
        item = Normalize(item);
        if (characterKey.Length == 0 || count <= 0 || !TrackFilter(item)) return false;
        lock (_lock)
        {
            var entry = EntryFor(characterKey, item);
            var learned = offDump && !entry.OffDump;
            if (learned) entry.OffDump = true;
            if (time <= entry.LastTime)
            {
                if (learned) Save();
                return false;
            }
            entry.Looted += count;
            entry.LastTime = time;
            Save();
            return true;
        }
    }

    /// <summary>The item left the world: a merchant sale, a destroy, a merge (two tiers
    /// became one), or a hand-in. Same filter, normalization, and replay-safe time gate as
    /// <see cref="RecordLoot"/> — the startup replay re-offers these too.</summary>
    /// <returns>True only when the exit was NEW, so the Sky tab can take back guesses the
    /// lower count no longer covers.</returns>
    public bool RecordConsumed(string characterKey, string item, int count, DateTime time)
    {
        item = Normalize(item);
        if (characterKey.Length == 0 || count <= 0 || !TrackFilter(item)) return false;
        lock (_lock)
        {
            var entry = EntryFor(characterKey, item);
            if (time <= entry.LastTime) return false;
            entry.Consumed += count;
            entry.LastTime = time;
            Save();
            return true;
        }
    }

    /// <summary>Square this character's ledger against their own <c>/outputfile
    /// inventory</c> dump (#241, DasGud: a Sky reward showed 4 Sphinx Claws held when he
    /// had none, and 15 Izah runes instead of 17, because the log never sees a hand-in
    /// or off-log acquisition — the dump is the game's own statement of what remains).
    ///
    /// Reconciles the STORE, not the readers: every dump item the <see cref="TrackFilter"/>
    /// admits, plus every item this character already tracks, is squared to what the dump
    /// says — present = its count, absent = zero — and <see cref="Entry.Looted"/>,
    /// <see cref="Entry.Manual"/> and <see cref="Entry.Consumed"/> all reset to zero,
    /// because the dump supersedes everything derived before it. A Manual count meaning "my
    /// mule holds two" is truthfully wrong about what THIS character carries, and the dump
    /// wins — the cost is one +1 click if that was ever a real statement.
    ///
    /// The one exception is an off-dump item (<see cref="IsOffDump(string, string)"/>) the dump
    /// does not list: it lives in currency or the depot, which the dump never shows, so
    /// absence proves nothing and the entry is left exactly as the log built it.
    ///
    /// Idempotent by a per-character watermark: a <paramref name="writtenAt"/> at or before
    /// the last reconcile is a no-op, so the launch replay (which re-offers the same
    /// <c>OutputfileEvent</c> every restart) and a re-announced dump cannot re-apply. An
    /// empty <paramref name="counts"/> is also a no-op — a dump that failed to parse must
    /// not erase what the ledger already knew (the <see cref="SetUnlockedClasses"/>
    /// precedent).
    ///
    /// Call this from the ingest, at the <c>OutputfileEvent</c> case, in log order — never
    /// from a UI-thread hop. In ingest order a loot line seconds after the announcement
    /// lands AFTER this call and survives untouched; everything logged before the dump is
    /// squared by it, and the launch replay reproduces the identical sequence.</summary>
    public (int Trued, Action? Undo) ReconcileInventory(
        string characterKey, IReadOnlyDictionary<string, int> counts, DateTime writtenAt)
    {
        if (characterKey.Length == 0 || counts.Count == 0) return (0, null);
        lock (_lock)
        {
            var c = CharacterFor(characterKey);
            if (writtenAt <= c.LastInventoryReconcile) return (0, null);

            var union = new HashSet<string>(
                counts.Keys.Where(TrackFilter), StringComparer.OrdinalIgnoreCase);
            foreach (var key in c.Items.Keys) union.Add(key);

            var priorWatermark = c.LastInventoryReconcile;
            var before = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            var trued = 0;
            foreach (var item in union)
            {
                // Currency and depot items never appear in the dump, so their absence is
                // not a zero - squaring them would erase runes the player holds.
                if (!counts.ContainsKey(item) && IsOffDump(c, item)) continue;
                var entry = EntryFor(characterKey, item);
                var verified = counts.TryGetValue(item, out var n) ? n : 0;
                var totalBefore = entry.Total;
                var changed = entry.Verified != verified || entry.Looted != 0
                    || entry.Manual != 0 || entry.Consumed != 0 || entry.VerifiedAt != writtenAt;
                if (!changed) continue;

                before[item] = new Entry
                {
                    Verified = entry.Verified, Looted = entry.Looted, Manual = entry.Manual,
                    Consumed = entry.Consumed, LastTime = entry.LastTime, VerifiedAt = entry.VerifiedAt,
                };
                entry.Verified = verified;
                entry.Looted = 0;
                entry.Manual = 0;
                entry.Consumed = 0;
                entry.VerifiedAt = writtenAt;
                if (writtenAt > entry.LastTime) entry.LastTime = writtenAt;
                if (entry.Total != totalBefore) trued++;
            }
            c.LastInventoryReconcile = writtenAt;
            Save();

            Action? undo = before.Count == 0 ? null : () =>
            {
                lock (_lock)
                {
                    foreach (var (item, prior) in before)
                    {
                        var entry = EntryFor(characterKey, item);
                        entry.Verified = prior.Verified;
                        entry.Looted = prior.Looted;
                        entry.Manual = prior.Manual;
                        entry.Consumed = prior.Consumed;
                        entry.LastTime = prior.LastTime;
                        entry.VerifiedAt = prior.VerifiedAt;
                    }
                    c.LastInventoryReconcile = priorWatermark;
                    Save();
                }
            };
            return (trued, undo);
        }
    }

    /// <summary>Set the manual adjustment: positive = "already had these before EQBuddy",
    /// negative = a hand-in offset against the looted (and, since #241, verified) history
    /// — clamped so Total never goes below zero, you can't owe the ledger items. Zero
    /// removes an entry with no looted or verified history. Manual entries bypass the
    /// filter — the user typing a name is its own statement of relevance.</summary>
    public void SetManual(string characterKey, string item, int count)
    {
        item = Normalize(item);
        if (characterKey.Length == 0 || item.Trim().Length == 0) return;
        lock (_lock)
        {
            var entry = EntryFor(characterKey, item.Trim());
            entry.Manual = Math.Max(count, -(entry.Verified + entry.Looted));
            if (entry is { Manual: 0, Looted: 0, Verified: 0 })
                _byCharacter[characterKey].Items.Remove(item.Trim());
            Save();
        }
    }

    /// <summary>A hand-in happened: zero this item's WHOLE count. Looted and Verified are
    /// history we can't re-earn, so the clear becomes a negative manual offset against
    /// both — net zero now, and future loot counts up from there. Lives in the store
    /// because both windows used to hand-roll it as <c>SetManual(-Looted)</c>, and #241's
    /// reconcile (which moves the count into <see cref="Entry.Verified"/> and zeroes
    /// <see cref="Entry.Looted"/>) turned that into a silent no-op on every row an
    /// inventory dump had verified — the exact rows the Turn-ins provenance sentence
    /// points the player at. A no-op for an item the ledger does not hold.</summary>
    public void ClearCount(string characterKey, string item)
    {
        item = Normalize(item);
        if (characterKey.Length == 0 || item.Trim().Length == 0) return;
        lock (_lock)
        {
            if (!_byCharacter.TryGetValue(characterKey, out var c)
                || !c.Items.TryGetValue(item.Trim(), out var entry)) return;
            entry.Manual = -(entry.Verified + entry.Looted);
            if (entry is { Manual: 0, Looted: 0, Verified: 0 })
                c.Items.Remove(item.Trim());
            Save();
        }
    }

    /// <summary>Whether an inventory dump is blind to this item: the log has shown it
    /// auto-stored off the bags, or it is a known currency item
    /// (<see cref="CurrencyItems"/>). Such an item's count comes from the log alone, so a
    /// dump must not square it and a scan must not act on it.</summary>
    public bool IsOffDump(string characterKey, string item)
    {
        item = Normalize(item);
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c) ? IsOffDump(c, item) : CurrencyItems.IsKnown(item);
    }

    private static bool IsOffDump(CharacterLedger c, string item) =>
        CurrencyItems.IsKnown(item) || c.Items.TryGetValue(item, out var e) && e.OffDump;

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
                        Verified = kv.Value.Verified, VerifiedAt = kv.Value.VerifiedAt,
                        OffDump = kv.Value.OffDump,
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
                entry.Manual = Math.Max(entry.Manual - item.Qty, -(entry.Verified + entry.Looted));
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

    /// <summary>What the player told Character Setup this character IS (copy; empty =
    /// they have said nothing, and EQBuddy's own reading stands).</summary>
    public List<string> StatedClassesFor(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c) ? [.. c.StatedClasses] : [];
    }

    /// <summary>Record the player's statement. **Empty is STORED here, deliberately the
    /// opposite of <see cref="SetUnlockedClasses"/>**: an empty dump list is a parse
    /// failure erasing the game's answer, but an empty statement is the player choosing
    /// "go back to EQBuddy's own reading" — the one way to undo a correction, so it must
    /// be writable. Capped at <see cref="CharacterClasses.Max"/> because that is the
    /// game's own limit, not ours.</summary>
    public void SetStatedClasses(string characterKey, IEnumerable<string> classes)
    {
        if (characterKey.Length == 0) return;
        lock (_lock)
        {
            CharacterFor(characterKey).StatedClasses = classes
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(CharacterClasses.Max)
                .ToList();
            Save();
        }
    }

    // ---- Guided progression: manual objective state (Fable plan §4, P1b) --------------
    //
    // The low-level store half. Everything here is written by ONE caller,
    // UI.Shared/GuideProgressRouter, which is where the "does this verb belong to the
    // guide ledger or to the Sky turn-in store?" question is answered. Calling
    // SetObjectiveDone directly from a surface would put a reward objective's tick in two
    // places, and the second screen the player touched would be the one telling the truth.
    // GuideProgressRoutingTests scans for that, in both directions (trap 34).

    /// <summary>This character's progress through one guide (copy; an untouched guide comes
    /// back empty rather than null — "no progress" is a state, not an absence).</summary>
    public GuideProgress GuideProgressFor(string characterKey, string guideId)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c)
                   && c.Guides.TryGetValue(guideId, out var g)
                ? new GuideProgress
                {
                    DoneObjectiveIds = [.. g.DoneObjectiveIds],
                    SkippedObjectiveIds = [.. g.SkippedObjectiveIds],
                    LastUpdated = g.LastUpdated,
                }
                : new GuideProgress();
    }

    /// <summary>Every guide this character has touched (copy of the ids; empty when
    /// unknown). The catalog decides what exists — this only says where they have been.</summary>
    public IReadOnlyList<string> GuidesTouchedBy(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c) ? [.. c.Guides.Keys] : [];
    }

    /// <summary>Tick or untick an objective the ledger owns, REFUSING one it does not.
    ///
    /// <para>Returns false — writing nothing — for an objective carrying a
    /// <c>RewardKey</c>, because that fact is a Sky turn-in and already has a store with four
    /// writers behind it (trap 4). The caller marks those through
    /// <c>GuideProgressRouter.SetDone</c>, which turns them in for real.</para>
    ///
    /// <para><b>Why this overload can tell and the id one cannot.</b> The objective is handed
    /// IN, so the store still never reads the catalog — no second producer of "what kind of
    /// objective is this", which is the trap that kept this refusal out of the store at
    /// first. Prefer this overload; the id form below is the primitive it delegates to.</para>
    ///
    /// <para>True means the ledger owns it and the store now holds the asked-for state,
    /// including when it already did — a repaint is not a change.</para></summary>
    public bool SetObjectiveDone(string characterKey, string guideId, GuideObjective objective, bool done)
    {
        if (objective.RewardKey.Length > 0) return false;
        SetObjectiveDone(characterKey, guideId, objective.Id, done);
        return true;
    }

    /// <summary>Tick or untick a non-reward objective by id. Ticking clears any skip on the
    /// same objective — see <see cref="GuideProgress"/>.
    ///
    /// <para><b>Not for an objective carrying a <c>RewardKey</c>.</b> This form takes only an
    /// id, so it cannot tell; the overload above can, and the read side never reads the
    /// ledger for a reward objective anyway, so a tick that lands here by mistake is
    /// unreadable rather than merely wrong. Route through <c>GuideProgressRouter</c>.</para></summary>
    public void SetObjectiveDone(string characterKey, string guideId, string objectiveId, bool done)
        => SetObjectiveMembership(characterKey, guideId, objectiveId, done,
            g => g.DoneObjectiveIds, removeFrom: g => g.SkippedObjectiveIds);

    /// <summary>Strike an objective out — "not doing this one" — or take the strike back.
    /// Skipping clears any tick. Legitimate on reward objectives too: the skip has no other
    /// home, and it is a different fact from the turn-in.</summary>
    public void SetObjectiveSkipped(string characterKey, string guideId, string objectiveId, bool skipped)
        => SetObjectiveMembership(characterKey, guideId, objectiveId, skipped,
            g => g.SkippedObjectiveIds, removeFrom: g => g.DoneObjectiveIds);

    private void SetObjectiveMembership(
        string characterKey, string guideId, string objectiveId, bool member,
        Func<GuideProgress, List<string>> list, Func<GuideProgress, List<string>> removeFrom)
    {
        if (characterKey.Length == 0 || guideId.Length == 0 || objectiveId.Length == 0) return;
        lock (_lock)
        {
            var c = CharacterFor(characterKey);
            if (!c.Guides.TryGetValue(guideId, out var g))
            {
                // Only a real change may CREATE the guide's row: un-ticking something that
                // was never ticked must leave the file exactly as it was, so a render pass
                // that reasserts state cannot grow the ledger a row per guide on screen.
                if (!member) return;
                c.Guides[guideId] = g = new GuideProgress();
            }

            var target = list(g);
            var contradiction = removeFrom(g);
            var has = target.Contains(objectiveId, StringComparer.OrdinalIgnoreCase);
            var contradicted = member
                && contradiction.RemoveAll(o => o.Equals(objectiveId, StringComparison.OrdinalIgnoreCase)) > 0;
            if (member == has && !contradicted) return;

            if (member) { if (!has) target.Add(objectiveId); }
            else target.RemoveAll(o => o.Equals(objectiveId, StringComparison.OrdinalIgnoreCase));

            g.LastUpdated = DateTime.UtcNow;
            Save();
        }
    }

    /// <summary>Last announced level for this character (0 = unknown). <b>The OBSERVED
    /// half only</b> — a surface asking "what level is this character" wants
    /// <see cref="ResolvedLevelFor"/>, which weighs this against the player's own statement.
    /// This one exists for the ding gate, which has to compare the log's newest number
    /// against the log's stored number and nothing else.</summary>
    public int LevelFor(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c) ? c.Level : 0;
    }

    /// <summary>What the log announced, with the moment it announced it, or null when it
    /// never has.</summary>
    public LevelReading? ObservedLevelFor(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c)
                ? CharacterLevel.Reading(c.Level, c.LevelAt)
                : null;
    }

    /// <summary>What the player stated, with the moment they stated it, or null when no
    /// statement stands.</summary>
    public LevelReading? StatedLevelFor(string characterKey)
    {
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c)
                ? CharacterLevel.Reading(c.StatedLevel, c.StatedLevelAt)
                : null;
    }

    /// <summary>
    /// **The one answer** — the fresher of the two claims
    /// (<see cref="CharacterLevel.Resolve"/>).
    ///
    /// <para>Resolved HERE rather than by each caller, for the reason trap 33 names: two
    /// callers with different arguments produce two current answers and whichever ran last
    /// wins. The Character room, the Helper, the level-unlock preview and the xp tooltip all
    /// ask this, so "the widget says 30 and the Helper says 28" is not a state the app can
    /// reach. <b>Both readings are taken under ONE lock</b>, so the pair being weighed is
    /// the pair that existed at one moment (trap 56).</para>
    /// </summary>
    public ResolvedLevel ResolvedLevelFor(string characterKey)
    {
        lock (_lock)
        {
            if (!_byCharacter.TryGetValue(characterKey, out var c)) return ResolvedLevel.Unknown;
            return CharacterLevel.Resolve(
                CharacterLevel.Reading(c.Level, c.LevelAt),
                CharacterLevel.Reading(c.StatedLevel, c.StatedLevelAt));
        }
    }

    /// <summary>
    /// Record the level the log just announced, with <b>the LOG's own timestamp</b>. Stores
    /// what the log said, not a max — the announcement line only fires on gains, so it's
    /// already monotonic per character.
    ///
    /// <para>Idempotent on the same level AND the same stamp (launch replay re-offers every
    /// ding in the file). It is NOT idempotent on the level alone: a ding re-read from a
    /// fresher log line is the same number carrying a newer moment, and the moment is the
    /// whole of what <see cref="CharacterLevel.Resolve"/> weighs.</para>
    /// </summary>
    public void SetLevel(string characterKey, int level, DateTime at)
    {
        if (characterKey.Length == 0 || level <= 0) return;
        lock (_lock)
        {
            var c = CharacterFor(characterKey);
            if (c.Level == level && c.LevelAt == at) return;
            c.Level = level;
            c.LevelAt = at;
            Save();
        }
    }

    /// <summary>
    /// The player's own statement about their level, stamped with their wall clock.
    /// <b>A level of 0 or less CLEARS it</b> — that is "Let EQBuddy work it out", the same
    /// idiom the class statement uses, and it is why the undo is one click rather than a
    /// number the player has to guess their way back to.
    /// </summary>
    public void SetStatedLevel(string characterKey, int level)
    {
        if (characterKey.Length == 0) return;
        lock (_lock)
        {
            var c = CharacterFor(characterKey);
            var wanted = Math.Max(0, level);
            // Re-stating the SAME level is not a no-op: the player re-affirming a number
            // after a ding is exactly how they say "no, the ding was my other class" a
            // second time, and only the stamp can carry that.
            if (c.StatedLevel == wanted && wanted == 0) return;
            c.StatedLevel = wanted;
            c.StatedLevelAt = wanted > 0 ? DateTime.Now : default;
            Save();
        }
    }

    /// <summary>
    /// **Where this character's professions stand** — the log's spelling → value and moment,
    /// copied out under the lock (DRA-71 D8).
    ///
    /// <para>A COPY rather than the live dictionary, for the reason every other reader here
    /// takes one: the room reads this on a one-second tick while the log thread writes it, and
    /// handing out the store's own object would be an enumeration racing an insert. Empty
    /// means the log has never announced a profession skill-up for this character — which is
    /// the state a player who has never crafted is in, and the state a player whose skill-ups
    /// all happened before EQBuddy existed is ALSO in. The surface says so rather than
    /// drawing a zero (<see cref="Tradeskills.Standings"/>).</para>
    /// </summary>
    public IReadOnlyList<(string Skill, int Value, DateTime At)> SkillsFor(string characterKey)
    {
        if (string.IsNullOrEmpty(characterKey)) return [];
        lock (_lock)
            return _byCharacter.TryGetValue(characterKey, out var c)
                ? [.. c.Skills.Select(kv => (kv.Key, kv.Value.Value, kv.Value.At))]
                : [];
    }

    /// <summary>
    /// Offer what the log has said about this character's skills. Returns true when anything
    /// actually moved.
    ///
    /// <para><b>A batch and not one call per skill, because the alternative writes the profile
    /// file eight times a second.</b> The caller hands over the whole live session's skill
    /// list every tick — that list only changes when the game announces a skill-up — so the
    /// save happens once, and only when a value really rose.</para>
    ///
    /// <para><b>The highest value wins, which is what makes this replay-safe.</b> The
    /// full-log replay every launch re-offers every skill-up in the file; each one carries the
    /// TOTAL the game printed rather than an increment, so re-offering them lands on the same
    /// number and changes nothing. That is a different rule from
    /// <see cref="RecordLoot"/>'s time high-water mark, and deliberately so: loot accumulates
    /// and a repeat would double it, while a skill value is a statement of where you are.</para>
    ///
    /// <para><b>Only the eight professions are admitted</b> —
    /// <see cref="Tradeskills.IsProfessionSkill"/>. See <see cref="CharacterLedger.Skills"/>
    /// for why the filter is here rather than at the surface.</para>
    /// </summary>
    public bool SetSkills(
        string characterKey, IEnumerable<(string Skill, int Value, DateTime At)> seen)
    {
        if (string.IsNullOrEmpty(characterKey) || seen is null) return false;
        var changed = false;
        lock (_lock)
        {
            foreach (var (skill, value, at) in seen)
            {
                if (value <= 0 || !Tradeskills.IsProfessionSkill(skill)) continue;
                var c = CharacterFor(characterKey);
                if (c.Skills.TryGetValue(skill, out var entry))
                {
                    if (entry.Value >= value) continue;
                    entry.Value = value;
                    entry.At = at;
                }
                else
                {
                    c.Skills[skill] = new SkillEntry { Value = value, At = at };
                }
                changed = true;
            }
            if (changed) Save();
        }
        return changed;
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
            ProfileJson.Write(_path, json);
        }
        catch (Exception ex) { CoreLog.Error(ex); }
    }
}
