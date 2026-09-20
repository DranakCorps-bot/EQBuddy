using EQBuddy.Core;

namespace EQBuddy.E2E;

/// <summary>
/// **THE MAP'S TARGET LAYER, FROM A LAUNCHED APP** (DRA-216 D5, S13/S14).
///
/// <para>The unit suite proves the join and the words; only a real run proves the chain a
/// player actually walks: a goal in <c>settings.json</c> → the widget's memo → the map
/// window's tick → a ring on a dot that a kill line put there seconds earlier. Every link in
/// that chain lives in the WPF layer, which has no unit tests (<c>docs/TestPlan.md</c> §5), so
/// this file is the only thing standing between it and a silent regression.</para>
///
/// <para><b>And it carries the S27 regression bar for this slice in the SAME moment</b>
/// (trap 56) — map load, the <c>/loc</c> marker, and a learned spawn timer. Those three are
/// what D5 puts at risk: it added a term to the circle layer's rebuild stamp, a block above the
/// named panel, and a new read on every map tick. Asserting them beside the new facts, off one
/// dump, is what makes "the rings appeared" and "nothing else stopped" one claim rather than
/// two runs.</para>
/// </summary>
[Collection("e2e")]
public sealed class MapTargetLayerTests
{
    private static string Key() =>
        $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();

    private static TrackedUpgrade Goal(string item) => new(
        item, "PRIMARY", "Rusty Dagger +2",
        new DateTime(2026, 9, 15, 20, 14, 0, DateTimeKind.Local));

    /// <summary>
    /// **THE WHOLE CHAIN, ON THE SHIPPED CATALOGS.** <i>Blackened Wand</i>'s item page names
    /// <i>Priest Amiaz</i> in Befallen and the spawn catalog knows him as one of Befallen's
    /// nameds — so killing him with a fresh <c>/loc</c> archives a point, the point is ringed,
    /// and its countdown is a real learned timer rather than the zone's projection (S14).
    ///
    /// <para>The zone, the position and the kill all arrive as LOG LINES through the app's own
    /// parser and its own ledger (trap 23): a hand-written archive file would photograph a
    /// state this app never enters, and it is the clustering — kill near a fresh /loc — that
    /// decides whether a dot exists at all.</para>
    /// </summary>
    [Fact]
    public void TheMapRingsTheArchivedPointThatDropsWhatYouAreGoingAfter()
    {
        using var app = new AppHarness(
            configureSettings: s => s.TrackedUpgrades[Key()] = [Goal("Blackened Wand")],
            environment: new Dictionary<string, string> { ["EQBUDDY_MAP"] = "1" });
        app.SeedZoneMap("befallen");
        app.Launch();
        app.WaitForWindow("mapShown", "the Map window to open and dump its first facts");

        // Into the zone, stand somewhere, kill the named. One /loc covers the kill because the
        // ledger's freshness window is three minutes — the same window camp pins use.
        app.AppendLogLines(
            "You have entered Befallen.",
            "Your Location is 100.00, 200.00, 5.00",
            "You have slain Priest Amiaz!");

        // The goal reached the map's own read, under the key the widget writes under.
        app.WaitForDump("mapTargetGoals", "BlackenedWand",
            "the tracked goal to reach the map window through the widget's join");
        // …and the map DREW the ring. Three claims from one moment (trap 56): the join found
        // it, the canvas has it, and the panel says something about it. A ring with no row is
        // a mark nobody can read; a row with no ring is a promise the map did not keep.
        app.WaitForDump("mapTargetRings", 1, "the archived point to wear a target ring");
        Assert.True(app.DumpValue("mapTargetRows") >= 1,
            "the panel should carry at least the goal's own row");

        // ---- S27's regression bar, from the same dump -------------------------------
        // Map load: the seeded pack resolved and the picture is up.
        Assert.Equal(1, app.DumpValue("mapShown"));
        Assert.True(app.DumpValue("mapZones") >= 1, "the seeded map should be in the dropdown");
        // /loc: the position line placed the marker. This is the assertion the pre-D5 map
        // suite could not make, because it configured no maps folder and the marker is gated
        // on a loaded map (trap 22).
        Assert.Equal(1, app.DumpValue("mapMarkerVisible"));
        // Learned spawn timers: the kill started a countdown and the named panel drew it —
        // header plus at least one row, which is what tells a running timer from the empty
        // state's single "no running timers" line.
        Assert.True(app.DumpValue("mapNamedRows") >= 2,
            "killing a catalog named should start its timer and put a row in the named panel");
        // And the point itself is archived and drawn, which is what the ring is drawn ON.
        Assert.True(app.DumpValue("mapCircles") >= 1);
    }

    /// <summary>
    /// **THE COMMITTED NEGATIVE, AND IT IS THE STATE ALMOST EVERY PLAYER IS IN.** Nothing
    /// tracked draws no block, no rings and no goals — and everything else about the map is
    /// unchanged, which is the half a new layer is most likely to break.
    ///
    /// <para>It is the same fixture as the test above in every respect but the one setting, so
    /// a difference between the two is the feature and not the staging.</para>
    /// </summary>
    [Fact]
    public void AProfileWithNothingTrackedDrawsNoTargetLayerAndAnOtherwiseNormalMap()
    {
        using var app = new AppHarness(
            environment: new Dictionary<string, string> { ["EQBUDDY_MAP"] = "1" });
        app.SeedZoneMap("befallen");
        app.Launch();
        app.WaitForWindow("mapShown", "the Map window to open and dump its first facts");

        app.AppendLogLines(
            "You have entered Befallen.",
            "Your Location is 100.00, 200.00, 5.00",
            "You have slain Priest Amiaz!");

        // Wait on the thing that proves the map TICKED after the append — otherwise the three
        // zeroes below are true of a window that has not looked yet (trap 62).
        app.WaitForDumpAtLeast("mapCircles", 1,
            "the kill to archive a spawn point and the map to draw its circle");

        Assert.Equal("", app.DumpText("mapTargetGoals"));
        Assert.Equal(0, app.DumpValue("mapTargetRings"));
        Assert.Equal(0, app.DumpValue("mapTargetRows"));

        // The rest of the map is exactly the other test's map.
        Assert.Equal(1, app.DumpValue("mapShown"));
        Assert.Equal(1, app.DumpValue("mapMarkerVisible"));
        Assert.True(app.DumpValue("mapNamedRows") >= 2);
    }

    /// <summary>
    /// **A GOAL THAT DROPS SOMEWHERE ELSE RINGS NOTHING HERE AND STILL SAYS WHERE TO GO.**
    ///
    /// <para>This is the state the whole "drops somewhere else" half exists for, and it is the
    /// one a flag-shaped feature would get wrong by drawing nothing at all. The goal is real
    /// and placeable — its page names one zone — and that zone is not this one, so the answer
    /// is a row and no ring rather than a silence.</para>
    /// </summary>
    [Fact]
    public void AGoalThatDropsElsewhereGetsARowAndNoRing()
    {
        using var app = new AppHarness(
            configureSettings: s => s.TrackedUpgrades[Key()] = [Goal("A Froglok Hex Doll")],
            environment: new Dictionary<string, string> { ["EQBUDDY_MAP"] = "1" });
        app.SeedZoneMap("befallen");
        app.Launch();
        app.WaitForWindow("mapShown", "the Map window to open and dump its first facts");

        app.AppendLogLines(
            "You have entered Befallen.",
            "Your Location is 100.00, 200.00, 5.00",
            "You have slain Priest Amiaz!");
        app.WaitForDumpAtLeast("mapCircles", 1,
            "the kill to archive a spawn point and the map to draw its circle");

        // No goal for THIS zone, so nothing is ringed…
        Assert.Equal("", app.DumpText("mapTargetGoals"));
        Assert.Equal(0, app.DumpValue("mapTargetRings"));
        // …and the block still drew, because the elsewhere row is the answer here.
        Assert.True(app.DumpValue("mapTargetRows") >= 1,
            "a goal that drops in another zone should still get a row naming that zone");
    }
}
