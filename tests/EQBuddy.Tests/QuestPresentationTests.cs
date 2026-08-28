using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The Gate 2 status vocabulary. Both desktops read these answers, so a disagreement here
/// is a disagreement on two operating systems at once — which is exactly how #122 and
/// #152 reached Linux after Windows had already fixed them.
/// </summary>
public class QuestPresentationTests
{
    private static QuestMatch Match(int have, int need, bool repeatable = false,
        bool collection = false, int items = 1)
    {
        var quest = new QuestEntry
        {
            Name = "Test Quest", Repeatable = repeatable, Collection = collection,
            Items = [.. Enumerable.Range(0, items).Select(i => new QuestItemNeed
            {
                Name = $"Thing {i}", Qty = need,
            })],
        };
        var progress = quest.Items.Select(i => new QuestItemProgress(i.Name, i.Qty, have)).ToList();
        return new QuestMatch(quest, progress.Count(p => p.Have > 0), progress.Count, progress);
    }

    [Fact]
    public void EveryTurnInInHandReadsReady() =>
        Assert.Equal(QuestPresentation.State.Ready, QuestPresentation.BadgeFor(Match(2, 2), 0).State);

    /// <summary>The fraction is the information; "in progress" is not — 1/2 and 9/10 are
    /// the same word and very different situations. It counts item TYPES with any copies
    /// over total types, which is what the card it replaces counted; changing that would
    /// be a behaviour change, not a restyle.</summary>
    [Fact]
    public void PartialProgressShowsTheFractionRatherThanAWord()
    {
        var quest = new QuestEntry
        {
            Name = "Q",
            Items = [new QuestItemNeed { Name = "A", Qty = 1 }, new QuestItemNeed { Name = "B", Qty = 1 }],
        };
        var progress = new List<QuestItemProgress>
        {
            new("A", 1, 1),
            new("B", 1, 0),
        };
        var badge = QuestPresentation.BadgeFor(new QuestMatch(quest, 1, 2, progress), 0);
        Assert.Equal(QuestPresentation.State.InProgress, badge.State);
        Assert.Equal("1/2", badge.Label);
    }

    [Fact]
    public void NothingHeldReadsOpen() =>
        Assert.Equal(QuestPresentation.State.Open, QuestPresentation.BadgeFor(Match(0, 1), 0).State);

    [Fact]
    public void NoParsedTurnInsReadsSteps() =>
        Assert.Equal(QuestPresentation.State.Steps,
            QuestPresentation.BadgeFor(Match(0, 0, items: 0), 0).State);

    [Fact]
    public void ACollectionPageRefusesToShowAFraction() =>
        Assert.Equal(QuestPresentation.State.Collection,
            QuestPresentation.BadgeFor(Match(1, 1, collection: true), 0).State);

    [Fact]
    public void AFinishedNonRepeatableIsDone() =>
        Assert.Equal(QuestPresentation.State.Done, QuestPresentation.BadgeFor(Match(0, 1), 1).State);

    /// <summary>The trap this ordering exists for: a repeatable you have finished before
    /// and whose turn-ins are in your bags RIGHT NOW must read "ready", not "done".
    /// Reading "done" over a full set is the kind of wrong that costs trust in the whole
    /// tracker rather than in one row.</summary>
    [Fact]
    public void AFinishedRepeatableWithTurnInsInHandStillReadsReady()
    {
        var badge = QuestPresentation.BadgeFor(Match(2, 2, repeatable: true), 3);
        Assert.Equal(QuestPresentation.State.Ready, badge.State);
        Assert.Equal("ready", badge.Label);
    }

    [Fact]
    public void MultipleSetsInHandOnARepeatableSayHowMany() =>
        Assert.Equal("ready ×3", QuestPresentation.BadgeFor(Match(6, 2, repeatable: true), 0).Label);

    /// <summary>One fact, two encodings — the rule and the badge must never disagree, and
    /// "open" gets no rule at all: a list where every row is highlighted highlights
    /// nothing.</summary>
    [Fact]
    public void TheRuleAgreesWithTheBadgeAndOpenRowsHaveNone()
    {
        Assert.Equal("GoodBrush", QuestPresentation.RuleColorKey(QuestPresentation.State.Ready));
        Assert.Equal("AccentBrush", QuestPresentation.RuleColorKey(QuestPresentation.State.InProgress));
        Assert.Null(QuestPresentation.RuleColorKey(QuestPresentation.State.Open));
        Assert.Null(QuestPresentation.RuleColorKey(QuestPresentation.State.Steps));
        Assert.Null(QuestPresentation.RuleColorKey(QuestPresentation.State.Collection));
    }

    /// <summary>Every rule colour a state can name has to be a real palette key, or it
    /// paints an invisible strip in all eight themes.</summary>
    [Fact]
    public void EveryRuleAndBadgeColourIsAPaletteKey()
    {
        foreach (var state in Enum.GetValues<QuestPresentation.State>())
            if (QuestPresentation.RuleColorKey(state) is { } key)
                Assert.Contains(key, ThemePalettes.Keys);
        foreach (var (have, need, done) in new[] { (0, 1, 0), (1, 2, 0), (2, 2, 0), (0, 1, 1) })
            Assert.Contains(QuestPresentation.BadgeFor(Match(have, need), done).ColorKey,
                ThemePalettes.Keys);
    }

    /// <summary>Nothing ready says NOTHING. "0 quests ready to turn in" reads as a fault
    /// in the tracker rather than as an ordinary afternoon.</summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "1 quest ready to turn in")]
    [InlineData(8, "8 quests ready to turn in")]
    public void TheReadySummaryStaysQuietWhenThereIsNothingToSay(int count, string? expected) =>
        Assert.Equal(expected, QuestPresentation.ReadySummary(count));

    [Theory]
    [InlineData(null, "")]
    [InlineData(0, "you're here")]
    [InlineData(1, "1 zone away")]
    [InlineData(6, "6 zones away")]
    public void DistanceReadsTheSameOnBothDesktops(int? hops, string expected) =>
        Assert.Equal(expected, QuestPresentation.DistanceText(hops));

    /// <summary>The meta line is ellipsized, so its ORDER is the design: the class list
    /// is the longest fragment and the only one that can afford to vanish.</summary>
    [Fact]
    public void TheMetaLinePutsWhatMustSurviveFirstAndClassesLast()
    {
        var quest = new QuestEntry
        {
            Name = "Q", StartZone = "Kaladim", QuestGiver = "Gunlok Jure", MinLevel = 7,
            Classes = "All (Paladin?)",
        };
        var line = QuestPresentation.MetaLine(quest, completedCount: 0, distance: "6 zones away");
        Assert.Equal("Kaladim · from Gunlok Jure · lvl 7+ · 6 zones away · All (Paladin?)", line);
    }

    /// <summary>A finished non-repeatable's badge already says "done", so the meta line
    /// must not spend its scarcest resource repeating it. A finished repeatable's badge
    /// cannot say it — that row is showing ready/progress — so its count stays.</summary>
    [Fact]
    public void CompletionsAppearInTheMetaLineOnlyWhereTheBadgeCannotCarryThem()
    {
        var once = new QuestEntry { Name = "Q", StartZone = "Kaladim" };
        Assert.DoesNotContain("done", QuestPresentation.MetaLine(once, 1, ""));

        var again = new QuestEntry { Name = "Q", StartZone = "Kaladim", Repeatable = true };
        Assert.Contains("done ×2", QuestPresentation.MetaLine(again, 2, ""));
    }

    // ---- #241 PR 3: the Turn-ins provenance sentence (Bevel-signed 2026-08-27) ----

    private static readonly DateTime Now = new(2026, 8, 27, 12, 0, 0);

    private static Dictionary<string, QuestLedgerStore.Entry> Owned(
        params (string Name, QuestLedgerStore.Entry Entry)[] entries) =>
        new(entries.Select(e => new KeyValuePair<string, QuestLedgerStore.Entry>(e.Name, e.Entry)),
            StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void AnItemNeverDumpedReadsAsALogTally()
    {
        var items = new List<QuestItemProgress> { new("Sphinx Claw", 1, 0) };
        var text = QuestPresentation.TurnInProvenanceText(items, Owned(), Now);
        Assert.Equal("from your log — hand-ins aren't in the log", text);
    }

    [Fact]
    public void ADumpWithNothingLoggedSinceNamesOnlyTheAge()
    {
        var items = new List<QuestItemProgress> { new("Sphinx Claw", 1, 0) };
        var owned = Owned(("Sphinx Claw", new QuestLedgerStore.Entry { VerifiedAt = Now.AddHours(-2) }));
        Assert.Equal("from your inventory dump, 2h ago",
            QuestPresentation.TurnInProvenanceText(items, owned, Now));
    }

    [Fact]
    public void LootAfterTheDumpAddsThePlusLootSinceClause()
    {
        var items = new List<QuestItemProgress> { new("Sphinx Claw", 1, 1) };
        var owned = Owned(("Sphinx Claw",
            new QuestLedgerStore.Entry { VerifiedAt = Now.AddMinutes(-30), Looted = 1 }));
        Assert.Equal("from your inventory dump, 30m ago · plus loot since",
            QuestPresentation.TurnInProvenanceText(items, owned, Now));
    }

    /// <summary>A hand-in offset (Manual goes negative after a ✔) is log movement exactly
    /// as much as fresh loot is — both mean the dump is no longer the whole story.</summary>
    [Fact]
    public void AManualAdjustmentAfterTheDumpAlsoCountsAsMovement()
    {
        var items = new List<QuestItemProgress> { new("Sphinx Claw", 1, 0) };
        var owned = Owned(("Sphinx Claw",
            new QuestLedgerStore.Entry { VerifiedAt = Now.AddHours(-1), Manual = -1 }));
        Assert.Equal("from your inventory dump, 1h ago · plus loot since",
            QuestPresentation.TurnInProvenanceText(items, owned, Now));
    }

    /// <summary>One sentence for the whole pane, not one per item (Bevel's explicit
    /// "not per-item"): a quest with several turn-ins reads by whichever items HAVE been
    /// dumped, not split three ways.</summary>
    [Fact]
    public void OneSentenceCoversAllOfAQuestsTurnInsNotOnePerItem()
    {
        var items = new List<QuestItemProgress> { new("A", 1, 0), new("B", 1, 0) };
        // A was dumped an hour ago; B has never been seen at all (no entry). The pane
        // still reads as dump-sourced, using the item(s) it has an answer for.
        var owned = Owned(("A", new QuestLedgerStore.Entry { VerifiedAt = Now.AddHours(-1) }));
        Assert.Equal("from your inventory dump, 1h ago",
            QuestPresentation.TurnInProvenanceText(items, owned, Now));
    }
}
