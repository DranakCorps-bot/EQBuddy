using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Delivery 2 N2 (DRA-46): a NORMAL quest's harvested guide, projected into the group every
/// surface already draws — the General tab's detail pane and the phone's quest card.
///
/// <para><b>The fixture is a REAL shipped quest and that is deliberate.</b> The whole value
/// of N1 was 1,164 machine-written guides, and a test over a hand-built one would prove the
/// projection compiles while saying nothing about what a player will actually open. The
/// survey that picked it is recorded in the region below — recipe lesson 6, survey the file
/// then look at the frame.</para>
/// </summary>
public sealed class QuestGuideProjectionTests : IDisposable
{
    private const string Dranak = "dranak_freeport";

    /// <summary>
    /// The fixture quest, chosen by SURVEYING the shipped harvested file rather than by
    /// picking one that looked right (2026-09-11):
    ///
    /// <list type="bullet">
    /// <item>1,164 harvested guides. The dominant shape — 491 of them — is two stages, some
    /// Transcribed prose and a "Turn-in pieces" skeleton, which is exactly this one.</item>
    /// <item>103 of them take their prose from a <c>== Checklist ==</c> section, which is the
    /// shape the plan names for the E2E.</item>
    /// <item>This quest is the small, total end of that set: 3 Transcribed bullets, 2
    /// <c>Collect</c> stubs (the catalog's two turn-in items), and an <c>Authored</c> hand-in
    /// — the infobox answers both who ("Karam Dragonforge") and where ("Rathe Mountains").</item>
    /// </list>
    ///
    /// <para>If authoring ever writes a curated guide for it, <c>GuideCatalog.Merge</c> hands
    /// the curated one over instead and these numbers move — which is a loud failure rather
    /// than a quiet one, and the right outcome for a test whose subject is the shipped file.</para>
    /// </summary>
    private const string FixtureQuest = "Red Dragonscale Armor Quest";

    private const string ScaleItem = "Red Dragon Scales";
    private const string VialItem = "Vial of Swirling Smoke";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"quest-guide-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static readonly QuestCatalog Catalog = QuestCatalog.LoadEmbedded();

    private static QuestEntry Quest(string name) =>
        Catalog.Quests.FirstOrDefault(q => q.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"the shipped quest catalog no longer has {name}");

    private static QuestChecklistGroup Project(AppSettings s, QuestLedgerStore ledger) =>
        GuideChecklistProjection.ForQuest(
            Quest(FixtureQuest), GuideCatalog.Default, s, ledger, Dranak)
        ?? throw new InvalidOperationException(
            $"no shipped guide walks {FixtureQuest} — the N2 fixture needs one");

    /// <summary>The shape the survey found, asserted so a refresh that changes it fails HERE
    /// rather than silently making every test below about something else. Six objectives: the
    /// page's three Checklist bullets, the catalog's two turn-in items, and the hand-in.</summary>
    [Fact]
    public void TheFixtureQuestsGuideIsTheShapeTheSurveyFound()
    {
        var group = Project(new AppSettings(), Store());

        Assert.Equal(6, group.Total);
        Assert.Equal(FixtureQuest, group.Title);
        Assert.Equal("harvested-red-dragonscale-armor-quest", group.GuideId);
        Assert.Equal(
            ["Checklist", "Checklist", "Checklist",
             "Turn-in pieces", "Turn-in pieces", "Turn-in pieces"],
            group.Rows.Select(r => r.IslandHeading));
    }

    /// <summary>
    /// <b>The Collect steps are the item rows the pane already draws, and the guide body does
    /// not draw them a second time.</b> The split's one producer is
    /// <see cref="GuideChecklistProjection.WalkthroughRows"/> — both surfaces ask it, so
    /// neither can decide differently (the whole point of N2's "not a second list").
    /// </summary>
    [Fact]
    public void TheTurnInPiecesAreItemRowsAndNotAlsoWalkthroughRows()
    {
        var group = Project(new AppSettings(), Store());

        var itemRows = group.Rows.Where(r => r.LedgerItemName.Length > 0).ToList();
        Assert.Equal([ScaleItem, VialItem], itemRows.Select(r => r.LedgerItemName));

        var walkthrough = GuideChecklistProjection.WalkthroughRows(group).ToList();
        // Every row is in exactly one of the two halves, and their sum is the whole.
        Assert.Equal(group.Total, walkthrough.Count + itemRows.Count);
        Assert.DoesNotContain(walkthrough, r => r.LedgerItemName.Length > 0);
        // Three prose bullets and the hand-in — and the hand-in is under the SAME stage the
        // item rows are, which is what lets the pane draw one heading over one list.
        Assert.Equal(4, walkthrough.Count);
        Assert.Equal("Turn-in pieces", walkthrough[^1].IslandHeading);
    }

    /// <summary>A Transcribed row is the page's own sentence and NOTHING else: the sentence is
    /// the title, and there is no who·where line under it because those fields are empty by
    /// rule (<see cref="GuideAuthoring.Transcribed"/>). A dim line that was sometimes there
    /// and usually not is what the schema's refusals exist to prevent.</summary>
    [Fact]
    public void ATranscribedRowIsTheSentenceWithNoWhoWhereLine()
    {
        var group = Project(new AppSettings(), Store());
        var prose = group.Rows.Where(r => r.IslandHeading == "Checklist").ToList();

        Assert.Equal(3, prose.Count);
        Assert.All(prose, r => Assert.Equal("", r.Detail));
        Assert.All(prose, r => Assert.Equal("", r.StubNote));
        Assert.Contains(prose, r => r.Title.Contains("Red Dragon Scales", StringComparison.Ordinal));
    }

    /// <summary>The hand-in IS the hand-in, not a piece: it must never be counted among the
    /// things that gate it, or "every piece in hand" becomes unreachable for every harvested
    /// guide (<c>QuestChecklistGroup.AllPiecesInHand</c>).</summary>
    [Fact]
    public void OnlyTheHandInRowIsMarkedAsTheTurnIn()
    {
        var group = Project(new AppSettings(), Store());

        var turnIns = group.Rows.Where(r => r.IsTurnIn).ToList();
        Assert.Single(turnIns);
        Assert.Contains("Karam Dragonforge", turnIns[0].Title, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A Collect row lights when the BAGS say so</b> — the ledger's owned count reaching the
    /// quest's need, through <c>GuideProgressHome.LedgerItem</c>. Nothing about this row is a
    /// tick the player placed, which is the whole reason it has no box of its own.
    /// </summary>
    [Fact]
    public void ACollectRowLightsWhenTheOwnedCountReachesNeed()
    {
        var settings = new AppSettings();
        var ledger = Store();

        Assert.All(Project(settings, ledger).Rows.Where(r => r.LedgerItemName.Length > 0),
            r => Assert.False(r.Acquired));

        ledger.SetManual(Dranak, ScaleItem, 1);

        var after = Project(settings, ledger).Rows
            .Where(r => r.LedgerItemName.Length > 0).ToList();
        Assert.True(after.Single(r => r.LedgerItemName == ScaleItem).Acquired);
        Assert.False(after.Single(r => r.LedgerItemName == VialItem).Acquired);
    }

    /// <summary>…and the group's own count moves with it, because the caption and the rows are
    /// read through one <c>IsDone</c>. A heading reading 0/6 over a lit row is the
    /// self-contradiction the counts exist to prevent.</summary>
    [Fact]
    public void TheGroupsCountMovesWithTheBags()
    {
        var settings = new AppSettings();
        var ledger = Store();
        Assert.Equal(0, Project(settings, ledger).Done);

        ledger.SetManual(Dranak, ScaleItem, 1);
        Assert.Equal(1, Project(settings, ledger).Done);
    }

    /// <summary><b>A Collect step REFUSES a tick.</b> Its answer is the bags, and a click that
    /// could say "yes I have one" while the ledger says none is the guide and the General tab
    /// disagreeing about one fact (trap 4). The refusal is the router's, asserted here from
    /// the surface's side so the projection cannot quietly gain a second writer.</summary>
    [Fact]
    public void TickingACollectStepWritesNothing()
    {
        var settings = new AppSettings();
        var ledger = Store();
        var guide = GuideCatalog.Default.Find("harvested-red-dragonscale-armor-quest")!;
        var collect = guide.AllObjectives.First(o => o.ObjectiveType == "Collect");
        var stores = new GuideStores([], [], Quest(FixtureQuest));

        GuideProgressRouter.SetDone(settings, ledger, Dranak, guide.Id, collect, stores, true);

        Assert.False(GuideProgressRouter.IsDone(
            settings, ledger, Dranak, guide.Id, collect, stores));
        Assert.Empty(ledger.GuideProgressFor(Dranak, guide.Id).DoneObjectiveIds);
    }

    /// <summary>The hand-in writes the QUEST'S COMPLETION RECORD — the same line the pane's ✓
    /// writes, and the catch-up marking that consumes nothing. Two callers of a destructive
    /// write is trap 47's shape, so it is deliberately not <c>RecordCompletion</c>.</summary>
    [Fact]
    public void TickingTheHandInMarksTheQuestCompletedAndConsumesNothing()
    {
        var settings = new AppSettings();
        var ledger = Store();
        ledger.SetManual(Dranak, ScaleItem, 1);
        var guide = GuideCatalog.Default.Find("harvested-red-dragonscale-armor-quest")!;
        var handIn = guide.AllObjectives.First(o => o.ObjectiveType == "TurnIn");
        var stores = new GuideStores([], [], Quest(FixtureQuest));

        GuideProgressRouter.SetDone(settings, ledger, Dranak, guide.Id, handIn, stores, true);

        Assert.Equal(1, ledger.CompletedFor(Dranak)[FixtureQuest]);
        // The pieces are still in the bags: the hand-in button on the pane is what consumes
        // them, and this row is the "I did this before EQBuddy" mark.
        Assert.Equal(1, ledger.For(Dranak)[ScaleItem].Total);
        Assert.True(Project(settings, ledger).Rows.Single(r => r.IsTurnIn).Acquired);
    }

    /// <summary>Folded by default, like Sky — a quest card is already several lines and a
    /// ten-step walkthrough opened on every one of them is the list folding gives back. The
    /// fold key is the GUIDE ID here, because a normal quest's group has no completion key;
    /// one producer, so the control and the projection cannot key it differently (the Epic
    /// "+" was a silent no-op for exactly that reason).</summary>
    [Fact]
    public void TheGuideStartsFoldedAndTheExpandedListOpensIt()
    {
        var settings = new AppSettings();
        var ledger = Store();
        Assert.True(Project(settings, ledger).Collapsed);

        settings.GuideExpanded.Add(GuideChecklistProjection.FoldKey(Project(settings, ledger)));
        Assert.False(Project(settings, ledger).Collapsed);
    }

    /// <summary>Founder lock 5, one surface on: a quest no guide walks comes back NULL, and
    /// the detail pane is exactly what it was. A prove-fail for the cutover — without it
    /// every assertion above could pass over a projection that guided everything.</summary>
    [Fact]
    public void AQuestNoGuideWalksIsNotGuided()
    {
        var unguided = Catalog.Quests.FirstOrDefault(q =>
            GuideChecklistProjection.QuestGuideFor(GuideCatalog.Default, q.Name) is null);
        Assert.NotNull(unguided);
        Assert.Null(GuideChecklistProjection.ForQuest(
            unguided!, GuideCatalog.Default, new AppSettings(), Store(), Dranak));
    }

    /// <summary>The card names the next step, and it names one the pane is SHOWING — the same
    /// list the rows come from. Its WHY is empty on purpose: the group's title is the quest
    /// name and the pane's own title is already drawing it (recipe lesson 7).</summary>
    [Fact]
    public void TheNextCardNamesAStepThisQuestActuallyHas()
    {
        var group = Project(new AppSettings(), Store());
        var card = group.GuideCard;

        Assert.NotNull(card);
        Assert.NotEqual("", card!.RowId);
        Assert.Contains(group.Rows, r => r.Id == card.RowId);
        Assert.Equal("", card.Why);
        Assert.NotEqual("", card.ImproveUrl);
    }

    /// <summary>
    /// The caption does not lead with "Guide", because the block's own heading already does
    /// (<see cref="GuidePresentation.QuestHeading"/>). Found by LOOKING AT THE FRAME —
    /// <c>shell-quests-general-guide</c>'s first take read "Guide  1/6" and "Guide · 2 stubs"
    /// on two consecutive lines, which no assertion about the projection's return could have
    /// caught because both strings were individually correct.
    ///
    /// <para>It still SAYS the stubs. That is lock 4a: two of this quest's six steps are ones
    /// we cannot say where to find, and a guide that reads finished when it is not is the one
    /// thing this caption exists to prevent.</para></summary>
    [Fact]
    public void TheCaptionDoesNotRepeatTheWordTheHeadingAlreadySays()
    {
        var caption = Project(new AppSettings(), Store()).GuideCaption;

        Assert.Equal("2 stubs", caption);
        Assert.DoesNotContain(GuidePresentation.QuestHeading, caption, StringComparison.Ordinal);
        // …and the Sky/Epic form is untouched: there the heading is a reward or a class, so
        // the lead is what marks the group as guided at all.
        Assert.Equal("Guide · 2 stubs", GuidePresentation.GuidedCaption(0, 2));
    }

    /// <summary>A normal quest's group draws no reward line and no stats block: its title is a
    /// QUEST, not an item, so "Rewards the Red Dragonscale Armor Quest." would be a sentence
    /// about nothing and the stats lookup would be an item search on a quest name. The surface
    /// draws the catalog's own Rewards beside this, in full.</summary>
    [Fact]
    public void ANormalQuestsGroupCarriesNoRewardSummaryOrStatsBlock()
    {
        var group = Project(new AppSettings(), Store());

        Assert.Equal("", group.RewardSummary);
        Assert.Equal("", group.RewardCard);
    }
}
