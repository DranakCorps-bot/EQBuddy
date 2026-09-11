using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// Delivery 2 N2 (DRA-46): a harvested guide on the GENERAL tab's detail pane, in the
/// running app.
///
/// <para>The WPF layer has no unit tests (docs/TestPlan.md §5), so the only way to claim the
/// walkthrough REACHED THE SCREEN is a launched app reporting its own structure. Every count
/// asserted here is taken off the real visual tree by the Tag its element carries (trap 39),
/// never from the projection's return — a number read from the thing that produced the rows
/// cannot fail the way the screen can. The projection's own totals are dumped BESIDE them,
/// from the same moment, so "the store says so" and "the screen says so" are two claims
/// rather than one (trap 56).</para>
///
/// <para><b>Every expected number is DERIVED from the shipped catalog</b>, not typed: a
/// literal would drift the day a refresh re-transcribes the page, and the test would then be
/// photographing a real state of something else (trap 23).</para>
/// </summary>
public class QuestGuidePaneTests
{
    /// <summary>The fixture quest — a real shipped one with a <c>== Checklist ==</c> section,
    /// picked by surveying the harvested file (the survey is written out in
    /// <c>QuestGuideProjectionTests</c>, which owns the same fixture).</summary>
    private const string FixtureQuest = "Red Dragonscale Armor Quest";

    private static Guide Guide =>
        GuideChecklistProjection.QuestGuideFor(GuideCatalog.Default, FixtureQuest)
        ?? throw new InvalidOperationException(
            $"no shipped guide walks {FixtureQuest} — the N2 pane fixture needs one");

    /// <summary>The page's own bullet count: the Checklist section's Transcribed steps. This
    /// is the number the plan asks the pane's rows to equal, plus the hand-in, which is the
    /// one walkthrough row the skeleton stage contributes.</summary>
    private static int Bullets =>
        Guide.AllObjectives.Count(o => o.Authoring == GuideAuthoring.Transcribed);

    /// <summary>Rows the guide BODY draws — the bullets and the hand-in. The Collect steps
    /// are NOT here; they are the turn-in item rows the pane already draws.</summary>
    private static int WalkthroughRows =>
        Bullets + Guide.AllObjectives.Count(o => o.ObjectiveType == "TurnIn");

    private static int ItemRows =>
        Guide.AllObjectives.Count(o => o.ObjectiveType == "Collect");

    /// <summary>
    /// The fixture: the General tab, with the quest SEARCHED for so it is the only row and
    /// the pane therefore selects it, and the guide EXPANDED.
    ///
    /// <para>A search rather than an owned item, deliberately: a search reads the whole
    /// catalog and needs no ledger state at all, so the one test below that IS about the
    /// ledger stages that state itself and nothing else in this file can pass because of
    /// it.</para>
    ///
    /// <para>Expanded through the same key the "+" writes — the guide id, because a normal
    /// quest's group has no completion key — derived from the catalog rather than typed, so a
    /// re-authored guide cannot leave this seeding silently pointing at nothing (trap 23).</para>
    /// </summary>
    private static AppHarness Fixture(bool expanded = true) =>
        new(s =>
            {
                if (expanded) s.GuideExpanded.Add(Guide.Id);
            },
            new Dictionary<string, string>
            {
                ["EQBUDDY_QUESTS"] = "general:" + FixtureQuest,
            });

    /// <summary>
    /// <b>The prediction, written before the run (2026-09-11).</b> The shipped guide for this
    /// quest has three Transcribed Checklist bullets, two Collect stubs and one Authored
    /// hand-in. So the pane must draw: one guide block, one NEXT card, four walkthrough rows
    /// (three bullets + the hand-in), a pencil on every one of them, two stage headings' worth
    /// of structure — and the two Collect steps must appear NOWHERE as guide rows, because
    /// they are the turn-in item rows the pane has always drawn.
    /// </summary>
    [Fact]
    public void ASearchedQuestsPaneDrawsItsGuideAndTheBulletsBecomeRows()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("questsDetailGuide", 1, "the selected quest's guide to be projected");
        app.WaitForDump("questsDetailGuideRows", WalkthroughRows,
            "every walkthrough step to be drawn");

        Assert.Equal(0, app.DumpValue("questsDetailGuideFolded"));
        Assert.Equal(1, app.DumpValue("questsDetailGuideCards"));
        // The pencil is on EVERY row, not only the stubs: an authored step can be wrong too.
        Assert.Equal(WalkthroughRows, app.DumpValue("questsDetailGuideImprove"));
        Assert.True(app.DumpValue("questsDetailGuideNext") > 0,
            $"the NEXT card named no step; dump was: {app.Artifacts()}");

        // THE "not a second list" CLAIM, made as arithmetic the screen cannot fake: the
        // guide HAS six steps, the body drew four of them, and the other two are the item
        // rows. Read from one moment, so the two numbers are about one picture (trap 56).
        var one = app.DumpValues(
            "questsDetailGuideTotal", "questsDetailGuideRows", "questsDetailGuideItemRows");
        Assert.Equal(WalkthroughRows + ItemRows, one[0]);
        Assert.Equal(WalkthroughRows, one[1]);
        Assert.Equal(ItemRows, one[2]);
    }

    /// <summary>
    /// <b>A Collect row lights when the ledger's owned count reaches Need</b> — the
    /// <c>LedgerItem</c> home, on the screen. The fixture holds one Red Dragon Scale (the
    /// quest needs one) and nothing else, so exactly one turn-in row must draw lit and the
    /// guide's own Done must be 1.
    ///
    /// <para>The two numbers come from ONE read: "the ledger says so" and "the screen says
    /// so" are different claims, and taking them a poll apart is how a passing test describes
    /// two different moments (trap 56).</para>
    ///
    /// <para>Prove-failed by its own second half: the same run asserts the OTHER turn-in row
    /// is NOT lit, so "everything is lit" cannot satisfy it.</para></summary>
    [Fact]
    public void ACollectRowLightsWhenTheOwnedCountReachesNeed()
    {
        using var app = Fixture();
        app.WriteLedgerOwned(("Red Dragon Scales", 1));
        app.Launch();

        app.WaitForDump("questsDetailGuide", 1, "the selected quest's guide to be projected");
        app.WaitForDump("questsDetailItemsLit", 1, "the held piece to light its turn-in row");

        var one = app.DumpValues(
            "questsDetailGuideDone", "questsDetailItemsLit", "questsDetailGuideItemRows");
        Assert.Equal(1, one[0]);            // the guide counts it done
        Assert.Equal(1, one[1]);            // the screen draws it lit
        Assert.Equal(ItemRows, one[2]);     // …and the other piece is still outstanding
        Assert.True(one[2] > one[1],
            $"every turn-in row drew lit, so nothing here is being tested: {app.Artifacts()}");
    }

    /// <summary>
    /// Folded by default, and <b>the fold hides the WALKTHROUGH and never the turn-in rows</b>
    /// — the question this tab has answered since the tracker existed.
    ///
    /// <para>The floor under every count above: with no <c>GuideExpanded</c> entry the pane
    /// draws the guide's heading and nothing under it, which is also what proves the expanded
    /// fixture is doing the work rather than the guide simply always being open.</para></summary>
    [Fact]
    public void AFoldedGuideHidesItsStepsAndKeepsTheTurnInRows()
    {
        using var app = Fixture(expanded: false);
        app.WriteLedgerOwned(("Red Dragon Scales", 1));
        app.Launch();

        app.WaitForDump("questsDetailGuideFolded", 1, "the guide to arrive folded");

        var one = app.DumpValues(
            "questsDetailGuide", "questsDetailGuideRows", "questsDetailGuideCards",
            "questsDetailItemsLit");
        Assert.Equal(1, one[0]);            // the guide is there…
        Assert.Equal(0, one[1]);            // …and none of its steps are drawn
        Assert.Equal(0, one[2]);            // …nor its card
        Assert.Equal(1, one[3]);            // but the turn-in row is still lit
    }

    /// <summary>Founder lock 5 on this surface: a quest no guide walks draws no guide chrome
    /// at all, and its detail pane is exactly what it was before N2. Without this every count
    /// above could pass over a pane that guides everything.</summary>
    [Fact]
    public void AQuestNoGuideWalksDrawsNoGuideChrome()
    {
        var unguided = QuestCatalog.LoadEmbedded().Quests.First(q =>
            GuideChecklistProjection.QuestGuideFor(GuideCatalog.Default, q.Name) is null);

        using var app = new AppHarness(null,
            new Dictionary<string, string> { ["EQBUDDY_QUESTS"] = "general:" + unguided.Name });
        app.Launch();

        app.WaitForDump("questsSelected", 1, "the searched quest to be selected");
        Assert.Equal(0, app.DumpValue("questsDetailGuide"));
        Assert.Equal(0, app.DumpValue("questsDetailGuideRows"));
        Assert.Equal(0, app.DumpValue("questsDetailGuideCards"));
        // …and the pane is not merely empty: it drew the quest.
        Assert.True(app.DumpValue("questsDetailBlocks") > 0,
            $"the detail pane drew nothing at all; dump was: {app.Artifacts()}");
    }
}
