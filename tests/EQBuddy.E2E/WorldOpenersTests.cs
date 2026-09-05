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

    /// <summary>
    /// The Travels &amp; Deaths body — <c>TravelsView</c>, in the World window's Travels
    /// tab.
    ///
    /// **This test read the WIDGET's copy until 2026-09-05.** Travels was the one World
    /// room the widget drew itself, on the `misc` card, and `EQBUDDY_EXPAND=1` reached it
    /// for free — so it was also the one room with no `EQBUDDY_*` hook of its own. HUD
    /// subtraction cut 2 removed that card, which would have left the surface with no way
    /// to be put on screen by a test or a shot at all (trap 22, a surface with no fixture
    /// state reading as reviewed anyway). `EQBUDDY_WORLD` landed with the cut for exactly
    /// this, and it is the same `ShowWorldWindow` call the `World…` menu row makes.
    ///
    /// The keys are unchanged (`zones`/`deaths`/`travelsMarkers`) because the VIEW is
    /// unchanged — one class, now with one owner instead of two, which is the parity
    /// Bevel's I-5 check one verified by construction rather than by resemblance. It also
    /// removes a two-host dump-key collision before it could happen (trap 58).
    ///
    /// The fixture zones twice during replay; a death line is appended here because the
    /// fixture has none on its own, and an empty deaths list would prove nothing about the
    /// row underneath.
    /// </summary>
    [Fact]
    public void TheTravelsTabDrawsZonesAndDeaths()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_WORLD"] = "1",
        });
        app.Launch();
        app.WaitForWindow("zones", "the World window to open on Travels and dump its facts");

        // "1" lands on the theme's default room, which is Travels — the room the card was.
        Assert.Equal("misc", app.DumpText("worldTab"));
        Assert.True(app.DumpValue("zones") > 0,
            "the fixture zones into Befallen and West Commonlands during replay");
        Assert.Equal(0, app.DumpValue("deaths"));

        app.AppendLogLines("You have been slain by a training dummy!");
        app.WaitForDump("deaths", 1, "the appended death to reach the Travels tab");
        Assert.True(app.DumpValue("travelsMarkers") >= 0);
    }

    /// <summary>A named room, in the same grammar the other hooks use — and "camps" rather
    /// than "spawns" on purpose: the hook resolves through <c>WorldSurface.TabForKey</c>,
    /// so it answers every alias that method does, and an assertion on the canonical word
    /// alone could not tell that apart from a hook that only matched wire keys. An
    /// unrecognised word falls back to the default room rather than throwing, which is what
    /// keeps a typo in a shot fixture from stopping the whole batch (trap 53).</summary>
    [Fact]
    public void TheWorldHookOpensARoomNamedByAnyOfItsAliases()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_WORLD"] = "camps",
        });
        app.Launch();
        // The TEXT overload: `worldTab` is a word, and `WaitForWindow`/`DumpValue` parse
        // integers and answer -1 for anything else — so waiting for it that way can never
        // be satisfied, however correct the app is. That is exactly how this test failed
        // its first run, against a dump plainly reading `worldTab=spawns`.
        app.WaitForDump("worldTab", "spawns", "the World window to open on the named room");
    }
}
