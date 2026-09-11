using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

// The state lens, the cross-class Ready band, the per-class counts and the actionability
// sort — the four things the widget's Sky card carried and the Quest Tracker did not get
// when that card became a launcher (66f6abc). #203, #205, #209 and #210 are four people
// reporting the same hole, so these are pinned rather than eyeballed: the rules came back
// verbatim from the old card, and a test is the only thing that keeps them that way.
public class QuestChecklistStateTests
{
    private static SkyQuestChecklistItem Item(
        string id, string cls, string reward, string item, bool acquired = false,
        string npc = "Cilin Spellsinger") => new()
    {
        Id = id, ClassName = cls, Npc = npc, Reward = reward, QuestItem = item,
        Source = "Isle 3", Acquired = acquired,
    };

    /// <summary>Bard: one reward turned in, one ready, one half done, one untouched.
    /// Ranger: one ready. Every state the lens names, and a second class so "across all
    /// classes" is a real claim rather than a list of one.</summary>
    private static SkyQuestChecklistItem[] Corpus =>
    [
        Item("a1", "Bard", "Amulet of the Fae", "Amulet piece", acquired: true),
        Item("b1", "Bard", "Mask of Song", "Woolen Mask", acquired: true),
        Item("b2", "Bard", "Mask of Song", "Wind Rune Meda", acquired: true),
        Item("c1", "Bard", "Mantle of the Songweaver", "Woolen Mantle", acquired: true),
        Item("c2", "Bard", "Mantle of the Songweaver", "Wind Rune Azia"),
        Item("d1", "Bard", "Spear of Harmony", "Spear shaft"),
        Item("d2", "Bard", "Spear of Harmony", "Spear tip"),
        Item("r1", "Ranger", "Bow of Sky", "Bow stave", acquired: true, npc: "Efreeti Lord Djarn"),
    ];

    private static IReadOnlyList<QuestChecklistGroup> Groups() =>
        QuestChecklistLayout.Sky(Corpus,
            [QuestChecklistLayout.RewardKey("Bard", "Amulet of the Fae")]);

    private static QuestChecklistGroup Named(string reward) =>
        Groups().Single(g => g.Title == reward);

    // ---- the four states ----

    [Fact]
    public void TurnedInReadsAsDone()
    {
        var g = Named("Amulet of the Fae");
        Assert.Equal(QuestChecklistLayout.StateDone, g.State);
        Assert.Equal("done", g.Note);
        Assert.False(g.ReadyToTurnIn);
    }

    [Fact]
    public void EveryPieceHeldButNotHandedOverReadsAsReady()
    {
        // The distinction the whole screen exists to make: holding the pieces and having
        // handed them over are different states.
        var g = Named("Mask of Song");
        Assert.Equal(QuestChecklistLayout.StateReady, g.State);
        Assert.True(g.ReadyToTurnIn);
    }

    [Fact]
    public void PartlyCollectedAndUntouchedAreBothOpen()
    {
        Assert.Equal(QuestChecklistLayout.StateOpen, Named("Mantle of the Songweaver").State);
        Assert.Equal(QuestChecklistLayout.StateOpen, Named("Spear of Harmony").State);
    }

    [Fact]
    public void NoteIsFinerThanTheLens()
    {
        // Both are "open" to the filter, but a half-done reward says so on its heading
        // and an untouched one says nothing at all.
        Assert.Equal("in progress", Named("Mantle of the Songweaver").Note);
        Assert.Null(Named("Spear of Harmony").Note);
    }

    [Fact]
    public void AnEpicSectionWithEveryPieceIsDoneNotReady()
    {
        // An Epic section has no hand-in of its own, so "every piece collected" IS its
        // terminal state. Offering "ready" there would promise a turn-in button that
        // must never appear — Epic completion is per CLASS.
        var epic = QuestChecklistLayout.Epic([
            new EpicQuestChecklistItem
            {
                Id = "e1", ClassName = "Bard", Section = "Part 1",
                QuestItem = "Songblade", Acquired = true,
            },
        ]);

        Assert.Equal(QuestChecklistLayout.StateDone, epic.Single().State);
        Assert.Null(epic.Single().CompletionKey);
    }

    // ---- the lens itself ----

    [Fact]
    public void InStateNarrowsToOneSlice()
    {
        Assert.Equal(["Mask of Song", "Bow of Sky"],
            QuestChecklistLayout.InState(Groups(), QuestChecklistLayout.StateReady)
                .Select(g => g.Title));
        Assert.Equal(["Amulet of the Fae"],
            QuestChecklistLayout.InState(Groups(), QuestChecklistLayout.StateDone)
                .Select(g => g.Title));
        Assert.Equal(2,
            QuestChecklistLayout.InState(Groups(), QuestChecklistLayout.StateOpen).Count());
    }

    [Theory]
    [InlineData(QuestChecklistLayout.StateAny)]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    public void AFilterNobodySetNeverEmptiesTheScreen(string? state)
    {
        // "any state" is the absence of a filter, and so is a value this build has never
        // heard of — a lens that empties the tracker because a saved string drifted is
        // indistinguishable from a broken tracker.
        Assert.Equal(Groups().Count, QuestChecklistLayout.InState(Groups(), state).Count());
    }

    [Fact]
    public void TheLensOffersAnyFirst()
    {
        Assert.Equal(QuestChecklistLayout.StateAny, QuestChecklistLayout.States[0]);
        Assert.Equal(
            ["any state", "open", "ready", "done"], QuestChecklistLayout.States);
    }

    // ---- the cross-class Ready band (#129) ----

    [Fact]
    public void ReadyBandSpansEveryClass()
    {
        var ready = QuestChecklistLayout.ReadyToTurnIn(Groups());

        Assert.Equal(["Mask of Song", "Bow of Sky"],
            ready.Select(g => g.Title));
        Assert.Equal(["Bard", "Ranger"], ready.Select(g => g.ClassName));
    }

    [Fact]
    public void ReadyBandNamesWhoTakesTheHandIn()
    {
        // "What can I turn in right now" is only actionable with "and to whom" attached.
        Assert.Equal("Efreeti Lord Djarn",
            QuestChecklistLayout.ReadyToTurnIn(Groups())
                .Single(g => g.ClassName == "Ranger").TurnInNpc);
    }

    [Fact]
    public void ReadyBandExcludesWhatIsAlreadyTurnedIn()
    {
        Assert.DoesNotContain(QuestChecklistLayout.ReadyToTurnIn(Groups()),
            g => g.Title == "Amulet of the Fae");
    }

    [Fact]
    public void ReadyBandIsEmptyWhenNothingIsReady()
    {
        var none = QuestChecklistLayout.Sky([Item("x", "Bard", "Reward", "Piece")]);
        Assert.Empty(QuestChecklistLayout.ReadyToTurnIn(none));
    }

    // ---- per-class counts (#136) ----

    [Fact]
    public void ClassCountsSplitDoneReadyAndPartial()
    {
        var bard = QuestChecklistLayout.ClassCounts(Groups()).Single(c => c.ClassName == "Bard");

        Assert.Equal(1, bard.Done);       // Amulet, turned in
        Assert.Equal(1, bard.Ready);      // Mask, every piece held
        Assert.Equal(1, bard.Partial);    // Mantle, one of two
        Assert.Equal(4, bard.Total);      // + Spear, untouched
    }

    [Fact]
    public void DoneReadyAndPartialDeliberatelyDoNotSumToTotal()
    {
        // #136: bjstrange read three numbers that didn't add up and reasonably concluded
        // they were wrong. A reward nobody has started sits in no bucket, which is why
        // the total is shown — it turns a puzzle into a subtraction.
        var bard = QuestChecklistLayout.ClassCounts(Groups()).Single(c => c.ClassName == "Bard");
        Assert.True(bard.Done + bard.Ready + bard.Partial < bard.Total);
    }

    [Fact]
    public void ClassCountsCoverEveryClassPresent()
    {
        Assert.Equal(["Bard", "Ranger"],
            QuestChecklistLayout.ClassCounts(Groups()).Select(c => c.ClassName));
    }

    // ---- the actionability sort ----

    [Fact]
    public void UnfinishedLeadsAndClosestToDoneLeadsTheUnfinished()
    {
        // The old card's rule, restored verbatim: ready first, then closest to done,
        // then untouched, with the turned-in sunk to the bottom as trophies.
        Assert.Equal(
            ["Mask of Song", "Mantle of the Songweaver", "Spear of Harmony", "Amulet of the Fae"],
            Groups().Where(g => g.ClassName == "Bard").Select(g => g.Title));
    }

    [Fact]
    public void ClassStillLeadsTheOrdering()
    {
        // Actionability sorts WITHIN a class — the tracker groups by class, and a list
        // that interleaved sixteen of them would answer no question at all.
        Assert.Equal(["Bard", "Bard", "Bard", "Bard", "Ranger"],
            Groups().Select(g => g.ClassName));
    }

    [Fact]
    public void EqualProgressFallsBackToTheRewardName()
    {
        // Two untouched rewards have nothing to rank them by; alphabetical is stable,
        // and a list that reshuffles between ticks is worse than one that is arbitrary.
        var groups = QuestChecklistLayout.Sky([
            Item("z", "Bard", "Zephyr", "Piece"),
            Item("a", "Bard", "Anvil", "Piece"),
        ]);
        Assert.Equal(["Anvil", "Zephyr"], groups.Select(g => g.Title));
    }

    // ---- the heading, which is no longer parsed back apart ----

    [Fact]
    public void HeadingJoinsClassAndTitleAndTitleSurvivesASeparatorInTheName()
    {
        var g = QuestChecklistLayout.Sky([Item("s", "Bard", "Horn · of Disaster", "Piece")]).Single();

        Assert.Equal("Bard · Horn · of Disaster", g.Heading);
        // The window used to recover the reward by splitting the heading on the
        // separator, which this name would have silently truncated to "of Disaster".
        Assert.Equal("Horn · of Disaster", g.Title);
    }

    /// <summary>
    /// <b>The heading's word and the state lens agree about the same group.</b>
    ///
    /// <para><c>Note</c>'s doc has claimed since it was written that it is derived from the
    /// same fields as <c>State</c> "so the label and the filter cannot disagree", and on the
    /// Epic tab that was not true: an Epic group has no turn-in of its own, so every piece
    /// collected IS its terminal state — <c>State</c> said <c>done</c> and the heading said
    /// "ready", about a hand-in that does not exist. Guiding the Epic tab is what made it
    /// visible (one heading per class instead of one per short section), which is why it is
    /// fixed here rather than noticed here.</para>
    ///
    /// <para>The SKY half is asserted beside it, because narrowing the rule to Epic is only
    /// safe if "ready" still means something where a hand-in genuinely is outstanding.</para>
    /// </summary>
    [Fact]
    public void AGroupWithNoTurnInOfItsOwnReadsDoneRatherThanReadyWhenEveryPieceIsHeld()
    {
        var epic = QuestChecklistLayout.Epic([
            new EpicQuestChecklistItem
            {
                Id = "e1", ClassName = "Warrior", Section = "Checklist",
                QuestItem = "Jagged Blade", Order = 1, Acquired = true,
            },
        ]).Single();

        Assert.Null(epic.CompletionKey);
        Assert.Equal(QuestChecklistLayout.StateDone, epic.State);
        Assert.Equal("done", epic.Note);

        // Sky, where the hand-in IS a separate act, still reads "ready" — holding the pieces
        // and having handed them over are different states and telling them apart is the
        // whole job of that screen.
        var sky = QuestChecklistLayout.Sky([Item("s", "Bard", "Mask of Song", "Piece", true)]).Single();

        Assert.NotNull(sky.CompletionKey);
        Assert.Equal(QuestChecklistLayout.StateReady, sky.State);
        Assert.Equal("ready", sky.Note);
    }
}
