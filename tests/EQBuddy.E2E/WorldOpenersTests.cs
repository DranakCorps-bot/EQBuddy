namespace EQBuddy.E2E;

/// <summary>
/// World PR 1's "pin behaviour in E2E before the move" step (docs/Themes.md's recipe,
/// step applied one theme early), extended by World PR 2: the three standalone windows
/// (Map, Spawns, Travel) retired into one <c>WorldWindow</c> with four tabs, and every
/// key these tests already asserted still has to read the same — <c>DumpValue</c> takes
/// the FIRST match in the dump, and <c>WorldWindow.DebugFacts()</c> reports all four
/// sub-surfaces' facts regardless of which tab is selected (the same shape
/// <c>CreatureWindow.DebugFacts()</c> already uses for Kills+Drops). The WPF layer has
/// no unit tests (docs/TestPlan.md §5), so this is the only thing standing between the
/// fold and a silent regression.
/// </summary>
[Collection("e2e")]
public sealed class WorldOpenersTests
{
    /// <summary>The Map window opens via <c>EQBUDDY_MAP=1</c>, same family as
    /// EQBUDDY_PROGRESS/EQBUDDY_SPAWNS. The fixture zones into Befallen and West
    /// Commonlands during replay, so a maps folder need not exist for the window's own
    /// facts (zone list, named panel, circles) to be assertable — <c>mapZones</c> comes
    /// from disk enumeration and is legitimately 0 with no maps folder configured; what
    /// matters is that the window opened and the dump carries its keys at all.</summary>
    [Fact]
    public void TheMapWindowOpensAndReportsItsState()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_MAP"] = "1",
        });
        app.Launch();
        app.WaitForWindow("mapShown", "the Map window to open and dump its first facts");

        // Present, not necessarily populated — this harness sets no MapFolder, so 0 is
        // the honest answer for a zone list built from disk. The point pinned here is
        // that the window opened and these keys exist, which is what a lift could break.
        Assert.True(app.DumpValue("mapZones") >= 0);
        Assert.True(app.DumpValue("mapNamedRows") >= 0);
        Assert.True(app.DumpValue("mapCircles") >= 0);
        Assert.True(app.DumpValue("mapCampPins") >= 0);
        Assert.True(app.DumpValue("mapMarkerVisible") is 0 or 1);
        // World PR 2: EQBUDDY_MAP opens the shared WorldWindow landed on the Map tab.
        Assert.Equal("map", app.DumpText("worldTab"));
        Assert.Equal(4, app.DumpValue("worldTabs"));
    }

    /// <summary>The Travel window opens via <c>EQBUDDY_TRAVEL=1</c> and lists every zone
    /// <c>ZoneGraph</c> knows — hundreds, since the embedded graph loads regardless of
    /// any log or settings state.</summary>
    [Fact]
    public void TheTravelWindowOpensAndListsZones()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_TRAVEL"] = "1",
        });
        app.Launch();
        app.WaitForWindow("travelZones", "the Travel window to open and dump its first facts");

        Assert.True(app.DumpValue("travelZones") > 0,
            "the embedded ZoneGraph should populate the destination dropdown");
        // No destination has been picked yet, so no route panel has rendered.
        Assert.Equal(0, app.DumpValue("travelRouteShown"));
        // World PR 2: EQBUDDY_TRAVEL opens the shared WorldWindow landed on the Path tab
        // (Bevel-signed label; the enum member and wire key stay Routes/"travel").
        Assert.Equal("travel", app.DumpText("worldTab"));
    }

    /// <summary>The Spawns window opens via <c>EQBUDDY_SPAWNS=1</c> on whatever zone the
    /// replay last saw (the fixture ends in Befallen) and lists that zone's named.</summary>
    [Fact]
    public void TheSpawnsWindowOpensOnTheCurrentZone()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SPAWNS"] = "1",
        });
        app.Launch();
        app.WaitForWindow("spawnsRows", "the Spawns window to open and dump its first facts");

        Assert.True(app.DumpValue("spawnsZones") > 0,
            "the zone combo should list every zone the spawn catalog knows");
        Assert.True(app.DumpValue("spawnsRows") >= 0);
        // World PR 2: EQBUDDY_SPAWNS opens the shared WorldWindow landed on the Camps tab.
        Assert.Equal("spawns", app.DumpText("worldTab"));
    }

    /// <summary>The Travels &amp; Deaths card body (the misc card, <c>TravelsView</c> as
    /// of this PR) — EQBUDDY_EXPAND=1 already expands it, so this needs no extra hook.
    /// The fixture zones twice during replay; a death line is appended here because the
    /// fixture has none on its own, and an empty deaths list would prove nothing about
    /// the row underneath (trap 22).</summary>
    [Fact]
    public void TheTravelsCardDrawsZonesAndDeaths()
    {
        using var app = new AppHarness();
        app.Launch();
        // EQBUDDY_EXPAND=1 (set by every harness launch) already expands MiscSection,
        // and Launch() already waited for the dump to exist (killsTotal > 0), so zones
        // and deaths are both readable the moment this line runs.
        Assert.True(app.DumpValue("zones") > 0,
            "the fixture zones into Befallen and West Commonlands during replay");
        Assert.Equal(0, app.DumpValue("deaths"));

        app.AppendLogLines("You have been slain by a training dummy!");
        app.WaitForDump("deaths", 1, "the appended death to reach the Travels card");
        Assert.True(app.DumpValue("travelsMarkers") >= 0);
    }
}
