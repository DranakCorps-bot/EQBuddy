using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// The guided Epic 1.0 rows, in the running app (DRA-41, Delivery 3).
///
/// <para>The WPF layer has no unit tests (docs/TestPlan.md §5), so the only way to claim the
/// epic guide REACHED THE SCREEN is a launched app reporting its own structure. Every number
/// below is counted off the real visual tree by the Tag its element carries (trap 39) and
/// DERIVED from the shipped catalog rather than typed — a literal would drift the day the epic
/// checklist is re-harvested and would then be photographing a real state of something else
/// (trap 23).</para>
///
/// <para><b>The fixture character reads as a Warrior</b>, so the tab's class lens narrows to
/// the Warrior epic on its own: 30 rows across five sections, which the guide draws as one
/// group with five stage headings.</para>
/// </summary>
public class EpicGuideRowsTests
{
    private const string GuideId = "epic-warrior";

    private static Guide Warrior =>
        GuideChecklistProjection.EpicGuideFor(GuideCatalog.Default, "Warrior")
        ?? throw new InvalidOperationException("no shipped epic guide for Warrior");

    private static int WarriorRows => Warrior.AllObjectives.Count();

    /// <summary>The first step in reading order — what the card must name before anything is
    /// ticked. The card reports its next step's row id LENGTH and never its text, because the
    /// dump is one flat space-separated namespace and these sentences have spaces in them
    /// (trap 58).</summary>
    private static int FirstRowIdLength =>
        GuideChecklistProjection.RowId(GuideId, Warrior.AllObjectives.First().Id).Length;

    /// <summary>The earliest Warrior epic step the loot auto-tick can actually prove — one
    /// whose text names a catalog item. Derived, so a re-harvest that renames the item fails
    /// this loudly rather than silently sending a log line nothing recognises.</summary>
    private static EpicQuestChecklistItem FirstLootableRow =>
        EpicQuestDefaults.Items()
            .Where(i => i.ClassName == "Warrior" && i.ItemNames.Count > 0)
            .OrderBy(i => i.Order)
            .First();

    /// <summary>The fixture, with the Warrior's epic EXPANDED — guided quests start folded, so
    /// a fixture that expands nothing draws no rows and every count would be asserting the
    /// fold rather than the guide. Expanded through the SAME key the fold control writes
    /// (<c>GuideChecklistProjection.FoldKey</c>), which for an epic group is the guide id.</summary>
    private static AppHarness Fixture() =>
        new(s => s.GuideExpanded.Add(GuideId),
            new Dictionary<string, string>
            {
                ["EQBUDDY_SHELL"] = "quests:epic",
                ["EQBUDDY_QUESTS"] = "epic",
            });

    /// <summary>
    /// The Warrior's epic draws as ONE guide, and the card names the first step.
    ///
    /// <para>Predicted before the run: one guided group where the classic tab drew five
    /// sections, one row per objective, no stubs (every step is Transcribed, which is not a
    /// stub), and the share-back door on every row — the sentence is the page's, so the page is
    /// where a wrong one gets fixed.</para>
    ///
    /// <para><b>And no classic rows at all.</b> That is the trap-4 claim made visible: the
    /// guide REPLACED this class's checklist rows rather than appearing beside them, so a
    /// Warrior never sees one step twice with two boxes.</para></summary>
    [Fact]
    public void TheWarriorsEpicDrawsAsOneGuideWhoseCardNamesTheFirstStep()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsTab", "epic", "the shell to reach the Epic 1.0 tab");
        app.WaitForDump("shellQuestsGuideRows", WarriorRows, "every epic step to be drawn");

        Assert.Equal(1, app.DumpValue("shellQuestsEpicGuideGroups"));
        Assert.Equal(1, app.DumpValue("shellQuestsGuideGroups"));
        Assert.Equal(1, app.DumpValue("shellQuestsGuideCards"));
        // NEXT is row 0: nothing is ticked, no prerequisite is invented, so reading order is
        // the whole of the sequence.
        Assert.Equal(FirstRowIdLength, app.DumpValue("shellQuestsGuideNext"));
        Assert.Equal(0, app.DumpValue("shellQuestsGuideDone"));
        // Transcribed is NOT a stub — 486 rows that ARE directions must not tell the player we
        // cannot give them directions.
        Assert.Equal(0, app.DumpValue("shellQuestsGuideStubs"));
        Assert.Equal(0, app.DumpValue("shellQuestsGuideCaptions"));
        Assert.Equal(WarriorRows, app.DumpValue("shellQuestsGuideImprove"));
        // The class is guided, so nothing is left drawing raw epic rows.
        Assert.Equal(0, app.DumpValue("shellQuestsSkyRows"));
        // A floor, so none of the above can pass over an empty tab.
        Assert.True(app.DumpValue("shellQuestsGuideRows") >= 14,
            $"the tab drew no epic guide rows at all; dump was: {app.Artifacts()}");
    }

    /// <summary>
    /// The fourth home, end to end: the log sees the drop, the loot auto-tick writes the EPIC
    /// CHECKLIST ROW, and the guide step lights from that row rather than from a second copy of
    /// the fact (trap 4).
    ///
    /// <para>The wait is on each count REACHING its value — a positive event that can only
    /// happen after the routing has run. <c>AppendLogLines</c> returns when the tail has read
    /// the bytes, not when the app has acted (trap 62), so asserting immediately after it would
    /// pass on a build where nothing was wired at all.</para>
    ///
    /// <para>Both numbers, from one moment: <c>questsEpicAcquired</c> is what the STORE says
    /// and <c>shellQuestsGuideDone</c> is what the SCREEN says, and a repaint gate sits between
    /// them — that gap is exactly what trap 72 cost on the Sky tab, where the box was ticked
    /// and the tab kept drawing the moment before for the whole session.</para>
    /// </summary>
    [Fact]
    public void AnEpicLootLineLightsItsGuideStepThroughTheChecklistRow()
    {
        var row = FirstLootableRow;
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsGuideRows", WarriorRows, "the epic steps before the loot");
        app.WaitForDump("shellQuestsGuideDone", 0, "and nothing ticked yet");
        Assert.Equal(0, app.DumpValue("questsEpicAcquired"));

        app.AppendLogLines(
            $"--You have looted a {row.ItemNames[0]} from a dragon's corpse.--");

        app.WaitForDump("questsEpicAcquired", 1, "the loot auto-tick to write the row");
        app.WaitForDump("shellQuestsGuideDone", 1, "the looted piece to light its guide step");

        // The row count did not move: one drop, one row, one tick — the guide did not gain a
        // box and the checklist did not keep one.
        Assert.Equal(WarriorRows, app.DumpValue("shellQuestsGuideRows"));
        Assert.Equal(0, app.DumpValue("shellQuestsSkyRows"));
        // …and the card did NOT move, because the step it names is the one before this and is
        // still open. A card that jumped here would mean the tick had landed on the wrong row.
        Assert.Equal(FirstRowIdLength, app.DumpValue("shellQuestsGuideNext"));
    }

    /// <summary>
    /// THE LANDING STATE: with nothing expanded, a guided class draws its heading and NO steps
    /// and NO card — which is what lets a 66-row Druid epic be read at a glance.
    ///
    /// <para>This is the default every other test in this file opts out of. Asserting it means
    /// the next person to see zero rows learns it is the fold rather than a broken guide — and
    /// <c>shellQuestsEpicGuideGroups</c> is what says the guide is THERE while folded.</para>
    /// </summary>
    [Fact]
    public void WithNothingExpandedTheEpicGuideDrawsItsHeadingAndNoSteps()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "quests:epic",
        });
        app.Launch();

        app.WaitForDump("shellQuestsTab", "epic", "the shell to reach the Epic 1.0 tab");
        app.WaitForDump("shellQuestsEpicGuideGroups", 1, "the guided class to draw its heading");

        Assert.Equal(0, app.DumpValue("shellQuestsGuideRows"));
        Assert.Equal(0, app.DumpValue("shellQuestsGuideCards"));
        // …and the classic rows are not drawn in their place either: a folded guided quest
        // shows its heading, not the list the guide replaced.
        Assert.Equal(0, app.DumpValue("shellQuestsSkyRows"));
    }

    /// <summary>
    /// Both hosts draw the same epic guide. On WPF a shared view does not throw — it silently
    /// vanishes from whichever host drew it first (trap 45), and these counts are what would
    /// catch a guide surface handed between the two instead of built twice.
    /// </summary>
    [Fact]
    public void TheShellAndTheQuestsWindowDrawTheSameEpicGuide()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsTab", "epic", "both hosts to reach the Epic 1.0 tab");
        app.WaitForDump("questsTab", "epic", "and the v1 window with them");
        app.WaitForDump("shellQuestsGuideRows", WarriorRows, "the shell's epic guide rows");
        app.WaitForDump("questsGuideRows", WarriorRows, "the window's epic guide rows");

        foreach (var key in new[]
                 { "EpicGuideGroups", "GuideGroups", "GuideCaptions", "GuideRows", "GuideStubs",
                   "GuideDone", "GuideImprove", "SkyRows", "GuideCards", "GuideNext",
                   "GuideSkipped" })
            Assert.Equal(app.DumpValue("quests" + key), app.DumpValue("shellQuests" + key));
    }
}
