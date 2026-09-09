using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// The guided Plane of Sky rows, in the running app.
///
/// <para>The WPF layer has no unit tests (docs/TestPlan.md §5), so the only way to claim the
/// guide REACHED THE SCREEN is a launched app reporting its own structure. Every fact
/// asserted here is counted off the real visual tree by the Tag its element carries
/// (trap 39), not from the projection's return — a count taken from the thing that produced
/// the rows cannot fail the way the screen can.</para>
///
/// <para><b>The fixture character reads as a Warrior</b>, so the Sky tab's class lens narrows
/// to Warrior on its own and the six Warrior rewards are what draws. The expected numbers are
/// therefore DERIVED from the shipped catalog rather than typed in: a literal would drift the
/// day a class is re-authored and would then be photographing a real state of something else
/// (trap 23).</para>
///
/// <para>Founder lock 5 — a class with no guide is untouched — is proved in the unit suite
/// (<c>GuideChecklistProjectionTests</c>) by REFERENCE EQUALITY, which is a stronger claim
/// than any count here could make, and cannot be staged in this fixture because the lens
/// follows the character's own class.</para>
/// </summary>
public class GuideRowsTests
{
    private static IReadOnlyList<Guide> WarriorGuides =>
        GuideCatalog.Default.ForClass("Warrior");

    /// <summary>Every objective of every Warrior guide — the rows the tab must draw.</summary>
    private static int WarriorRows => WarriorGuides.Sum(g => g.AllObjectives.Count());

    private static int WarriorStubs => WarriorGuides.Sum(g => g.StubCount);

    /// <summary>A box already ticked before launch reads as a done guide step. The other
    /// half of the item-backed rule: the guide does not need to have SEEN the tick happen,
    /// it reads the box every time it draws.</summary>
    [Fact]
    public void AnAlreadyTickedBoxReadsAsADoneGuideStep()
    {
        using var app = new AppHarness(
            s =>
            {
                s.SkyQuestChecklist.AddRange(SkyQuestDefaults.Items.Select(i => i.Clone()));
                s.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired = true;
            },
            new Dictionary<string, string> { ["EQBUDDY_SHELL"] = "quests:sky" });
        app.Launch();

        app.WaitForDump("shellQuestsTab", "sky", "the shell to reach the Plane of Sky tab");
        app.WaitForDump("shellQuestsGuideDone", 1, "a pre-ticked box to read as done");
        Assert.Equal(1, app.DumpValue("questsSkyAcquired"));
    }

    private static AppHarness Fixture() =>
        new(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "quests:sky",
            ["EQBUDDY_QUESTS"] = "sky",
        });

    /// <summary>
    /// The Warrior's six Plane of Sky rewards draw as guides, and every hollow step says so.
    ///
    /// <para>Predicted before the run: six guided groups (one per reward), one row per
    /// objective, and one stub for each of the six wind runes plus the Gem of Invigoration,
    /// whose two sources disagree. The share-back door is on EVERY row, not only the stubs —
    /// an authored step can be wrong too.</para>
    ///
    /// <para><b>And no classic rows at all.</b> That is the trap-4 claim made visible: the
    /// guide REPLACED this class's item boxes rather than appearing beside them, so a Warrior
    /// never sees "Stone Amulet" twice with two ticks.</para>
    /// </summary>
    [Fact]
    public void TheWarriorsSixRewardsDrawAsGuidesWithEveryHollowStepSayingSo()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsTab", "sky", "the shell to reach the Plane of Sky tab");
        app.WaitForDump("shellQuestsGuideRows", WarriorRows, "every guide step to be drawn");

        Assert.Equal(WarriorGuides.Count, app.DumpValue("shellQuestsGuideGroups"));
        Assert.Equal(WarriorStubs, app.DumpValue("shellQuestsGuideStubs"));
        Assert.Equal(0, app.DumpValue("shellQuestsGuideDone"));
        Assert.Equal(WarriorRows, app.DumpValue("shellQuestsGuideImprove"));
        // The whole class is guided, so nothing is left drawing raw item boxes.
        Assert.Equal(0, app.DumpValue("shellQuestsSkyRows"));
        // A floor, so none of the above can pass over an empty tab.
        Assert.True(app.DumpValue("shellQuestsGuideRows") >= 6,
            $"the tab drew no guide rows at all; dump was: {app.Artifacts()}");
    }

    /// <summary>
    /// The item-backed rule, end to end: the log sees the drop, the loot auto-tick writes the
    /// CHECKLIST BOX, and the guide step lights from that box rather than from a second copy
    /// of the fact (trap 4).
    ///
    /// <para>The wait is on the done count REACHING 1 — a positive event that can only happen
    /// after the item-backed routing has run. <c>AppendLogLines</c> returns when the tail has
    /// read the bytes, not when the app has acted (trap 62), so asserting immediately after
    /// it would pass on a build where nothing was wired at all.</para>
    /// </summary>
    [Fact]
    public void ALootedStoneAmuletTicksItsGuideStepThroughTheChecklistBox()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsGuideRows", WarriorRows, "the guide steps before the loot");
        app.WaitForDump("shellQuestsGuideDone", 0, "and nothing ticked yet");

        app.AppendLogLines(
            "--You have looted a Stone Amulet from a sky drake's corpse.--");

        app.WaitForDump("questsSkyAcquired", 1, "the loot auto-tick to write the box");
        app.WaitForDump("shellQuestsGuideDone", 1,
            "the looted amulet to light its guide step");
        // The row count did not move: one item, one row, one tick — the guide did not gain
        // a box and the checklist did not keep one.
        Assert.Equal(WarriorRows, app.DumpValue("shellQuestsGuideRows"));
        Assert.Equal(0, app.DumpValue("shellQuestsSkyRows"));
        Assert.Equal(WarriorStubs, app.DumpValue("shellQuestsGuideStubs"));
    }

    /// <summary>
    /// The active-step card names the next step, and MOVES when that step is done.
    ///
    /// <para><c>questsGuideNext</c> is the next row id's LENGTH, never its text: the dump is
    /// one flat space-separated namespace (trap 58), and the assertion is that the answer
    /// CHANGED, which a length carries. The two ids differ in length by construction here —
    /// <c>stone-amulet</c> against <c>wind-rune-azia</c>.</para>
    ///
    /// <para>The wait is on the length changing, a positive event that can only happen after
    /// the loot line has been read AND the card re-selected (trap 62).</para>
    /// </summary>
    [Fact]
    public void TheCardNamesTheNextStepAndMovesWhenItIsDone()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsGuideRows", WarriorRows, "the guide steps");
        app.WaitForDumpAtLeast("shellQuestsGuideCards", 1, "a card per guided group in view");
        Assert.Equal(WarriorGuides.Count, app.DumpValue("shellQuestsGuideCards"));
        Assert.Equal(0, app.DumpValue("shellQuestsGuideSkipped"));

        var before = app.DumpValue("shellQuestsGuideNext");
        Assert.True(before > 0, $"no card named a next step; dump was: {app.Artifacts()}");

        // PREDICTED before the run: the Runed Wind Amulet card moves from "stone-amulet" to
        // "wind-rune-azia", so the summed length grows by exactly the difference between
        // those two ids. Derived from the catalog rather than typed, so re-authoring the
        // guide fails this loudly instead of drifting past it (trap 23).
        var amulet = GuideCatalog.Default.Guides
            .Single(g => g.Id == "pos-warrior-runed-wind-amulet");
        var delta = amulet.AllObjectives.Single(o => o.Id == "wind-rune-azia").Id.Length
            - amulet.AllObjectives.Single(o => o.Id == "stone-amulet").Id.Length;
        Assert.Equal(2, delta);

        app.AppendLogLines(
            "--You have looted a Stone Amulet from a sky drake's corpse.--");

        app.WaitForDump("shellQuestsGuideNext", before + delta,
            "the card to move from the amulet to the wind rune");
        Assert.Equal(1, app.DumpValue("shellQuestsGuideDone"));
    }

    /// <summary>
    /// Both hosts draw the same guide. On WPF a shared view does not throw — it silently
    /// vanishes from whichever host drew it first (trap 45), and these counts are what would
    /// catch a guide surface handed between the two instead of built twice.
    /// </summary>
    [Fact]
    public void TheShellAndTheQuestsWindowDrawTheSameGuide()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsTab", "sky", "both hosts to reach the Plane of Sky tab");
        app.WaitForDump("questsTab", "sky", "and the v1 window with them");
        app.WaitForDump("shellQuestsGuideRows", WarriorRows, "the shell's guide rows");
        app.WaitForDump("questsGuideRows", WarriorRows, "the window's guide rows");

        foreach (var key in new[]
                 { "GuideGroups", "GuideRows", "GuideStubs", "GuideDone", "GuideImprove",
                   "SkyRows", "GuideCards", "GuideNext", "GuideSkipped" })
            Assert.Equal(app.DumpValue("quests" + key), app.DumpValue("shellQuests" + key));
    }
}
