using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// **GUIDE-STEP MARKS ON THE MAP, FROM A LAUNCHED APP** (DRA-42 D3, requirements §20).
///
/// <para>The unit suite proves the reader and the words; only a real run proves the chain: a
/// tracked quest in the ledger → the widget's While-you're-here answer → the map window's tick →
/// a diamond on a dot a kill line put there seconds earlier. Every link after the first lives in
/// the WPF layer, which has no unit tests (<c>docs/TestPlan.md</c> §5).</para>
///
/// <para>The exhibit is D1's, off the shipped catalogs (trap 23): <i>Armor of Ro Quests</i>
/// needs a Nightfall Giant's Head, whose page names <i>a nightfall giant</i> in West Commonlands.
/// The zone, the /loc and the kill all arrive as LOG LINES through the app's own parser and
/// ledger — it is the clustering, kill near a fresh /loc, that decides whether a dot exists.</para>
///
/// <para>The rest of the map is asserted in the same dump moment (trap 56), because a layer that
/// switched the map off would pass every assertion about the layer.</para>
/// </summary>
[Collection("e2e")]
public sealed class GuideMapTargetTests
{
    private const string ArmorOfRo = "Armor of Ro Quests";

    private static AppHarness Launch(bool layerOn)
    {
        var app = new AppHarness(
            configureSettings: s => s.ShowGuideTargetsOnMap = layerOn,
            environment: new Dictionary<string, string> { ["EQBUDDY_MAP"] = "1" });
        app.SeedQuestLedger(tracked: [ArmorOfRo]);
        app.SeedZoneMap("commons");
        app.Launch();
        app.WaitForWindow("mapShown", "the Map window to open and dump its first facts");
        app.AppendLogLines(
            "You have entered West Commonlands.",
            "Your Location is 100.00, 200.00, 5.00",
            "You have slain a nightfall giant!");
        return app;
    }

    [Fact]
    public void TheMapMarksThePointWhereATrackedStepsDropperWasKilled()
    {
        using var app = Launch(layerOn: true);

        app.WaitForDumpAtLeast("mapGuideSteps", 1,
            "the tracked quest's West Commonlands steps to reach the map through the widget");
        app.WaitForDump("mapGuideMarks", 1, "the archived point to wear a guide diamond");
        var seen = app.DumpValues("mapGuideRows", "mapGuideToggle", "mapTargetRings",
            "mapShown", "mapMarkerVisible", "mapCircles");
        Assert.True(seen[0] >= 1, "the panel should carry the step's own row");
        // On with nothing written into the profile to say so, and the chip is painted that way.
        Assert.Equal(1, seen[1]);
        // A second meaning, a second mark: nothing is tracked in the gear layer, so no ring.
        Assert.Equal(0, seen[2]);
        // The map itself, unchanged, from the same moment.
        Assert.Equal(1, seen[3]);
        Assert.Equal(1, seen[4]);
        Assert.True(seen[5] >= 1);
    }

    /// <summary>Same fixture in every respect but the one setting, so the difference is the flag.
    /// Off draws no marks and no block, the chip is on screen painted OFF, and the map is
    /// otherwise the test above's map.</summary>
    [Fact]
    public void TheLayerSwitchedOffDrawsNoMarksAndNoBlockAndLeavesTheMapAlone()
    {
        using var app = Launch(layerOn: false);

        // Wait on the circle: it is drawn by the same rebuild the diamond would have ridden, so
        // the zeroes below are about a map that has looked (trap 62).
        app.WaitForDumpAtLeast("mapCircles", 1,
            "the kill to archive a spawn point and the map to draw its circle");
        var seen = app.DumpValues("mapGuideSteps", "mapGuideUnmarkable", "mapGuideMarks",
            "mapGuideRows", "mapGuideToggle", "mapShown", "mapMarkerVisible");
        Assert.Equal(0, seen[0]);
        Assert.Equal(0, seen[1]);
        Assert.Equal(0, seen[2]);
        Assert.Equal(0, seen[3]);
        Assert.Equal(0, seen[4]);
        Assert.Equal(1, seen[5]);
        Assert.Equal(1, seen[6]);
    }
}
