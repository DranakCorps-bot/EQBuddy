using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// What a quest LOOKS like, decided once for both desktops.
///
/// The Gate 2 rebuild gives every quest row a status badge and a state-coloured leading
/// rule. Neither is decoration: they replace the per-quest TYPE icon the mockups drew
/// (scroll / mushroom / castle / potion), which cannot be built — quests have no type
/// taxonomy anywhere in the data, so an icon per quest would be invented art on the one
/// surface whose entire value is being trustworthy (docs/DesignSystem.md §8a).
///
/// It lives here rather than in either window for the reason every other UI.Shared module
/// does: two hand-written copies of "which colour is ready" is how the same bug reaches
/// Linux six weeks after Windows fixed it.
///
/// This computes NOTHING new. Every input is a value <see cref="QuestMatch"/> and the
/// ledger already hold; the output is a label and a palette key. That is the line the
/// brief draws — a presentation module may name a token, it may not decide a fact.
/// </summary>
public static class QuestPresentation
{
    /// <summary>The states a quest row can be in, in the order they matter to a reader.
    /// The set is deliberately small: a badge with nine values is a legend, not a
    /// glance.</summary>
    public enum State
    {
        /// <summary>Every turn-in is in hand. The one state worth interrupting for.</summary>
        Ready,
        /// <summary>Some turn-ins held, not all.</summary>
        InProgress,
        /// <summary>Catalogued, nothing held yet.</summary>
        Open,
        /// <summary>Marked completed, and not repeatable — history.</summary>
        Done,
        /// <summary>No turn-in items parsed: a dialogue, kill or exploration chain.</summary>
        Steps,
        /// <summary>A wiki page documenting several quests at once, so a fraction over the
        /// union would lie (CatalogHygiene).</summary>
        Collection,
    }

    public readonly record struct Badge(State State, string Label, string ColorKey);

    /// <summary>The badge for one row. <paramref name="completedCount"/> is the ledger's
    /// completion count for this quest and this character.</summary>
    public static Badge BadgeFor(QuestMatch match, int completedCount)
    {
        var quest = match.Quest;
        if (quest.Collection) return new(State.Collection, "set of quests", "DimBrush");
        // A finished non-repeatable is history and says so. A finished REPEATABLE is not:
        // it can be ready again right now, and reading "done" on a quest whose turn-ins
        // are in your bags is the kind of wrong that costs trust in the whole tracker.
        if (completedCount > 0 && !quest.Repeatable) return new(State.Done, "done", "GoodBrush");
        if (match.ItemsTotal == 0) return new(State.Steps, "steps", "DimBrush");
        if (match.Complete)
            return new(State.Ready,
                quest.Repeatable && match.ReadyCount > 1 ? $"ready ×{match.ReadyCount}" : "ready",
                "GoodBrush");
        return match.ItemsHave > 0
            ? new(State.InProgress, $"{match.ItemsHave}/{match.ItemsTotal}", "AccentBrush")
            : new(State.Open, "open", "DimBrush");
    }

    /// <summary>The leading rule down a row's edge. It carries the SAME state as the
    /// badge, which is the point — one fact, two encodings, so the list is scannable
    /// without reading and unambiguous when read. Open rows get no rule at all rather
    /// than a grey one: an "everything is highlighted" list highlights nothing.</summary>
    public static string? RuleColorKey(State state) => state switch
    {
        State.Ready => "GoodBrush",
        State.InProgress => "AccentBrush",
        State.Done => "GoodBrush",
        _ => null,
    };

    /// <summary>The rule is drawn at reduced strength for history — done is worth
    /// finding, not worth interrupting for.</summary>
    public static double RuleOpacity(State state) => state == State.Done ? 0.45 : 1.0;

    /// <summary>The one-line summary above the list: how many of what is showing are
    /// actionable right now. Answers the question the window exists for before the reader
    /// scans a single row — and says nothing at all when nothing is ready, rather than
    /// "0 quests ready", which reads as a fault in the tracker.</summary>
    public static string? ReadySummary(int readyCount) => readyCount switch
    {
        0 => null,
        1 => "1 quest ready to turn in",
        _ => $"{readyCount} quests ready to turn in",
    };

    /// <summary>The meta line under a quest name: zone · giver · level · repeatable ·
    /// completions · distance · classes.
    ///
    /// Classes go LAST and distance second-to-last because this line is ellipsized: the
    /// class list is the longest fragment and the one that can afford to vanish, and
    /// "done ×2" never can. That ordering is carried over from the card this replaces —
    /// it was learned, not chosen.
    ///
    /// A finished NON-repeatable says "done" in its badge, so repeating it here would
    /// spend the line's scarcest resource on a fact already on the row. A finished
    /// repeatable's badge is showing ready/progress instead — it can be turned in again —
    /// so its count has nowhere else to go and stays.</summary>
    public static string MetaLine(QuestEntry quest, int completedCount, string distance)
    {
        var parts = new[]
        {
            quest.StartZone,
            quest.QuestGiver.Length > 0 ? $"from {quest.QuestGiver}" : "",
            quest.MinLevel > 0 ? $"lvl {quest.MinLevel}+" : "",
            quest.Repeatable ? "repeatable" : "",
            completedCount > 0 && quest.Repeatable ? $"done ×{completedCount}" : "",
            distance,
            quest.Classes,
        };
        return string.Join(" · ", parts.Where(p => p.Length > 0));
    }

    /// <summary>"you're here" / "3 zones away" / "" — the BFS result rendered. The hop
    /// count is the caller's (it owns the ZoneGraph); this only decides the words, so
    /// both desktops say them the same way.</summary>
    public static string DistanceText(int? hops) => hops switch
    {
        null => "",
        0 => "you're here",
        1 => "1 zone away",
        _ => $"{hops} zones away",
    };

    /// <summary>The ONE sentence PR 3 adds under Turn-ins (#241, Bevel-signed
    /// 2026-08-27): which source today's have-counts came from, and how stale it is. One
    /// line for the whole pane, never per item — <see cref="QuestLedgerStore.ReconcileInventory"/>
    /// always re-stamps every existing entry together, so "the dump" is one moment even
    /// though <see cref="QuestLedgerStore.Entry.VerifiedAt"/> is stored per item; an item
    /// with no entry, or one never reconciled, is simply not counted toward it.
    ///
    /// Three states, Bevel's words: reconciled with nothing logged since; reconciled with
    /// loot or a hand-in landing after; never reconciled at all, so the number is a log
    /// tally that cannot see hand-ins.</summary>
    public static string TurnInProvenanceText(
        IReadOnlyList<QuestItemProgress> items,
        IReadOnlyDictionary<string, QuestLedgerStore.Entry> owned, DateTime now)
    {
        var dumpAt = DateTime.MinValue;
        var everDumped = false;
        var movedSince = false;
        foreach (var item in items)
        {
            if (!owned.TryGetValue(item.Name, out var e) || e.VerifiedAt == default) continue;
            everDumped = true;
            if (e.VerifiedAt > dumpAt) dumpAt = e.VerifiedAt;
            if (e.Looted != 0 || e.Manual != 0 || e.Consumed != 0) movedSince = true;
        }
        if (!everDumped) return "from your log — hand-ins aren't in the log";
        var age = WikiFreshness.Ago(now - dumpAt);
        return movedSince
            ? $"from your inventory dump, {age} · plus loot since"
            : $"from your inventory dump, {age}";
    }
}
