using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// The Plane of Sky ISLAND view, in the running app (DRA-164 D2).
///
/// <para>The WPF layer has no unit tests (docs/TestPlan.md §5), so the only way to claim the
/// island view REACHED THE SCREEN is a launched app reporting its own structure. The grouping
/// itself is proved in the unit suite over Core; what is asserted here is that the toggle is
/// on the tab, that the renderer ran, that its arithmetic adds up FROM ONE MOMENT (trap 56),
/// and that a box on an island row still ticks through the guide router.</para>
///
/// <para><b>The numbers are DERIVED from the shipped catalog, never typed.</b> A literal would
/// drift the day a class is re-authored and would then be photographing a real state of
/// something else (trap 23). The fixture character reads as a Warrior, so the Sky tab narrows
/// to Warrior on its own.</para>
/// </summary>
public class SkyIslandViewTests
{
    private static IReadOnlyList<Guide> WarriorGuides =>
        [.. GuideCatalog.Default.ForClass("Warrior")
            .Where(g => g.GuideType == GuideType.PlaneOfSkyQuest)];

    private static int WarriorRows => WarriorGuides.Sum(g => g.AllObjectives.Count());

    /// <summary>The lowest island the Warrior has gathering work on, read off the guides' own
    /// stage names — which is the ORDERING claim the ask is about ("island N before moving to
    /// next"), derived rather than asserted from memory.</summary>
    private static string LowestIslandHeading
    {
        get
        {
            var islands = WarriorGuides
                .SelectMany(g => g.Stages)
                .SelectMany(s => SkyIslands.Parse(s.Name))
                .ToList();
            Assert.NotEmpty(islands);
            // Spaces become underscores in the dump — see QuestsView.DebugFacts.
            return SkyIslands.Heading(islands.Min()).Replace(' ', '_');
        }
    }

    private static AppHarness Fixture(bool island) =>
        new(s =>
            {
                s.SkyGroupByIsland = island;
                s.GuideExpanded.AddRange(
                    WarriorGuides.Select(GuideChecklistProjection.RewardKeyOf));
            },
            new Dictionary<string, string>
            {
                ["EQBUDDY_SHELL"] = "quests:sky",
                ["EQBUDDY_QUESTS"] = "sky",
            });

    /// <summary>
    /// **KEEP is the default, and this is the row that proves it.**
    ///
    /// <para>A player who upgrades sees the class view, unchanged. `questsIslandGroups` is
    /// `-1` — "this render drew no island layout at all", which is a different claim from "it
    /// drew an empty one" and is the only way to tell that the new renderer did not run.
    /// Without this, every assertion in this file could pass on a build that had quietly
    /// replaced the class view.</para>
    /// </summary>
    [Fact]
    public void ByDefaultTheTabIsStillTheClassViewAndTheIslandRendererNeverRuns()
    {
        using var app = Fixture(island: false);
        app.Launch();

        app.WaitForDump("shellQuestsTab", "sky", "the shell to reach the Plane of Sky tab");
        app.WaitForDump("shellQuestsGuideRows", WarriorRows, "every guide step to be drawn");

        Assert.Equal(0, app.DumpValue("shellQuestsSkyIsland"));
        Assert.Equal(-1, app.DumpValue("shellQuestsIslandGroups"));
        Assert.Equal(-1, app.DumpValue("shellQuestsIslandRows"));
        // And the toggle is on screen offering the other one, or the view is unreachable.
        Assert.Equal(2, app.DumpValue("shellQuestsSkyViewChips"));
        Assert.Equal(1, app.DumpValue("shellQuestsSkyViewShown"));
    }

    /// <summary>
    /// Island view ON: the renderer runs, the lowest island leads, and **the arithmetic adds
    /// up from one moment** — every guide row this tab drew is either on an island or is a
    /// hand-in the view excluded and counted.
    ///
    /// <para>That identity is the assertion rather than a row total, for the reason the
    /// ladder asks for: it relates the dump's own inputs instead of restating a number this
    /// test could have got from the same place the app did. A row that silently vanished —
    /// dropped by the regroup rather than excluded by it — breaks it, and no count of groups
    /// would.</para>
    /// </summary>
    [Fact]
    public void IslandViewDrawsEveryGatheringRowUnderAnIslandAndCountsWhatItLeftOut()
    {
        using var app = Fixture(island: true);
        app.Launch();

        app.WaitForDump("shellQuestsTab", "sky", "the shell to reach the Plane of Sky tab");
        app.WaitForDump("shellQuestsSkyIsland", 1, "the Sky tab to be in island view");
        app.WaitForDumpAtLeast("shellQuestsIslandGroups", 1, "at least one island to be drawn");

        // ONE read of the dump, so the three numbers below are the same moment (trap 56).
        var facts = app.DumpValues(
            "shellQuestsGuideRows", "shellQuestsIslandRows",
            "shellQuestsIslandHiddenTurnIns", "shellQuestsIslandHiddenRewards");
        var (guideRows, islandRows, hiddenTurnIns, hiddenRewards) =
            (facts[0], facts[1], facts[2], facts[3]);

        // A fresh fixture has turned nothing in, so nothing may be hidden for that reason —
        // which is what makes the identity below a statement about the regroup alone.
        Assert.Equal(0, hiddenRewards);
        // One hand-in per guide, and every one of them excluded.
        Assert.Equal(WarriorGuides.Count, hiddenTurnIns);

        // THE IDENTITY, against the CATALOG: every objective the Warrior's guides hold is
        // either drawn on an island or is a hand-in the view excluded and counted. A row that
        // silently vanished — dropped by the regroup rather than excluded by it — breaks this,
        // and no count of groups would.
        Assert.Equal(WarriorRows, islandRows + hiddenTurnIns);

        // And the SCREEN agrees with the layout: `shellQuestsGuideRows` counts the guide rows
        // actually drawn, so in island view it is the island rows and nothing else. Two
        // counters, one moment, two different producers — which is the point of asserting it
        // rather than either number alone (trap 56).
        Assert.Equal(islandRows, guideRows);

        // "Everything to collect on island N before moving to next": the lowest island leads.
        Assert.Equal(LowestIslandHeading, app.DumpText("shellQuestsIslandFirst"));
    }

    /// <summary>
    /// **The row on the SCREEN prefixes its class** (Founder CLARIFY 2026-09-17 ~8:11 AM CT,
    /// signed plan P8) — `[Warrior] …`, taken from the strings the control that reached the
    /// panel was actually built from.
    ///
    /// <para>The unit suite proves Core PRODUCES a prefixed title; only a launched app can say
    /// the renderer PASSED it to the row it drew. Those are different claims and the second is
    /// the one the Founder can see (trap 56). A render that kept calling the row builder with
    /// `row.Row.Title` would pass every Core assertion there is.</para>
    ///
    /// <para><b>And the class is said ONCE.</b> The owner half beside the title is asserted to
    /// be the REWARD with no second copy of the class in it — the prefix is only an improvement
    /// if it REPLACED the owner's copy rather than joining it, which is the half a "does it
    /// start with a bracket" check cannot see.</para>
    /// </summary>
    [Fact]
    public void AnIslandRowOnTheScreenPrefixesItsClassAndSaysItOnlyOnce()
    {
        using var app = Fixture(island: true);
        app.Launch();

        app.WaitForDump("shellQuestsSkyIsland", 1, "the Sky tab to be in island view");
        app.WaitForDumpAtLeast("shellQuestsIslandRows", 1, "at least one island row on screen");

        // ONE read, so the two halves below are the same row from the same moment (trap 56).
        var title = app.DumpText("shellQuestsIslandRowTitle");
        var owner = app.DumpText("shellQuestsIslandRowOwner");

        // "-" is the dump's "no island row reached the panel" — assert against it by name, or
        // the StartsWith below would be checking a sentinel (trap 78).
        Assert.NotEqual("-", title);
        Assert.NotEqual("-", owner);

        // The fixture character reads as a Warrior, so the tab narrows to Warrior on its own —
        // the class is derived from the fixture rather than typed (trap 23).
        Assert.StartsWith("[Warrior]_", title, StringComparison.Ordinal);
        Assert.DoesNotContain("Warrior", owner, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>**Class view STANDS unprefixed**, and this is the prove-fail for the row above:
    /// without it, a build that had put `[Warrior]` on every checklist row everywhere would
    /// satisfy the prefix assertion while breaking the LEAVE the Founder wrote beside it. The
    /// dump answers "-" because no island row reached the panel at all.</summary>
    [Fact]
    public void TheClassViewDrawsNoPrefixedRowAtAll()
    {
        using var app = Fixture(island: false);
        app.Launch();

        app.WaitForDump("shellQuestsTab", "sky", "the shell to reach the Plane of Sky tab");
        app.WaitForDump("shellQuestsGuideRows", WarriorRows, "every guide step to be drawn");

        Assert.Equal("-", app.DumpText("shellQuestsIslandRowTitle"));
        Assert.Equal("-", app.DumpText("shellQuestsIslandRowOwner"));
    }

    /// <summary>
    /// **A box on an island row is the same box.** The loot auto-tick writes the Sky item
    /// store; the island view is a rearrangement of rows that route through
    /// <c>GuideProgressRouter</c>, so the guide's step must read as done and the tab must
    /// REDRAW — which is trap 72 by name, and the reason `SkyGroupByIsland` joined the refresh
    /// signature in the same commit this renderer landed.
    ///
    /// <para>Asserted on both halves from the same event: the STORE says the box is set
    /// (<c>questsSkyAcquired</c>) and the SCREEN says the guide step is done
    /// (<c>shellQuestsGuideDone</c>). Those are different claims, and a view that had rebuilt
    /// its own rows rather than reusing the class view's would satisfy the first and not the
    /// second.</para>
    /// </summary>
    [Fact]
    public void ALootTickReachesTheIslandViewAndRoutesThroughTheGuide()
    {
        using var app = Fixture(island: true);
        app.Launch();

        app.WaitForDump("shellQuestsSkyIsland", 1, "the Sky tab to be in island view");
        app.WaitForDumpAtLeast("shellQuestsIslandRows", 1, "island rows before the loot");
        app.WaitForDump("shellQuestsGuideDone", 0, "and nothing ticked yet");

        app.AppendLogLines(
            "--You have looted a Stone Amulet from a sky drake's corpse.--");

        app.WaitForDump("questsSkyAcquired", 1, "the loot auto-tick to write the box");
        app.WaitForDump("shellQuestsGuideDone", 1,
            "the island view to redraw with the guide step done");
        // Still in island view, and the row was not dropped on its way through.
        Assert.Equal(1, app.DumpValue("shellQuestsSkyIsland"));
        Assert.True(app.DumpValue("shellQuestsIslandRows") >= 1,
            $"the island view drew no rows after the loot; dump was: {app.Artifacts()}");
    }
}
