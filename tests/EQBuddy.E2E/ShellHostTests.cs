using EQBuddy.UI.Shared;

namespace EQBuddy.E2E;

/// <summary>
/// The Evolved shell host, asserted against the real launched app (E-3 Phase 2 PR 1).
///
/// **This suite is the only thing besides a screenshot that can see this window**, and
/// the two see different failures. A picture proves the rail reads well; it cannot prove
/// a control EXISTS, because an absent control photographs as an unremarkable window
/// (trap 29) — which is how the title-bar Mobile button stayed invisible for six days
/// through several releases, a compile, a test run and a diff. And the WPF layer has no
/// unit tests at all, so an assertion from a launched app is the only coverage the wiring
/// between `ShellLayoutPolicy` and the window it drives will ever have.
///
/// **Nothing here asserts the SCREEN.** A hosted runner is 1024×768, so "the rail shows
/// labels" would be an assertion about the desk this was written on. The dump carries the
/// INPUT (`shellWidth`) beside the ANSWER (`shellRailLabels`), and what is asserted is
/// that one follows from the other — a relationship, true on any monitor.
/// </summary>
public class ShellHostTests
{
    private static Dictionary<string, string> OpenOn(string address, string? size = null)
    {
        var env = new Dictionary<string, string> { ["EQBUDDY_SHELL"] = address };
        if (size is not null) env["EQBUDDY_SHELL_SIZE"] = size;
        return env;
    }

    /// <summary>
    /// The host opens, the rail draws, the Search affordance is there, and the Progress
    /// room paints — the four things E-3 PR 1 claims to have built.
    ///
    /// **`shellRail=1` is the assertion with teeth.** The signed pre-design refuses a
    /// disabled row for a room that has not shipped (*"an affordance that opens nothing
    /// is a trap"*), so this number is the count of rows DRAWN and it must equal the
    /// number of rooms that exist. The day a seventh row appears without a room behind
    /// it — or a room lands without joining the rail — this is what says so.
    /// </summary>
    [Fact]
    public void TheShellOpensOnProgressWithARailRowPerLandedRoomAndASearchAffordance()
    {
        using var app = new AppHarness(environment: OpenOn("progress"));
        app.Launch();

        app.WaitForDump("shellPage", "progress", "the shell to land on the Progress room");
        Assert.Equal(ShellPages.Landed.Count, app.DumpValue("shellRail"));
        // ONE room is built, not three: rooms are constructed on first arrival, and two of
        // them do real work when they are (a ticking timer and its ledger read, a scan of
        // the game folder). A shell opened to look at experience must not pay for either.
        Assert.Equal(1, app.DumpValue("shellRooms"));
        Assert.Equal(1, app.DumpValue("shellSearch"));
        // Search is a shortcut past the nav, not a page: it must not be OPEN on arrival.
        Assert.Equal(0, app.DumpValue("shellPalette"));
        // The room actually painted. Four tabs, from Core's ProgressSurface — the same
        // four the Progress WINDOW builds from the same definition.
        Assert.Equal(4, app.DumpValue("shellProgressTabs"));
        // "progress", not "experience": `ProgressSurface.KeyFor(Experience)` is the card
        // key the five surfaces folded into, deliberately one OF the absorbed keys rather
        // than a new one. So the Experience room's address is `progress:progress`, which
        // reads oddly and is correct — the room half is the SURFACE's vocabulary, not the
        // shell's, and re-spelling it here would be a second name for one room.
        Assert.Equal("progress", app.DumpText("shellProgressTab"));
    }

    /// <summary>
    /// **One navigation path, exercised end to end**: `page:room` lands inside the room,
    /// not merely on it. This is the grammar `EQBUDDY_EXPAND` has taken since 2026-08-26,
    /// reused so the rail, the Ctrl+K palette and a future HUD button resolve to one
    /// destination spelling — two ways to land on a room is trap 33 lifted from data into
    /// navigation.
    /// </summary>
    [Fact]
    public void AnAddressLandsInsideTheRoomAndNotJustOnIt()
    {
        using var app = new AppHarness(environment: OpenOn("progress:raids"));
        app.Launch();

        app.WaitForDump("shellProgressTab", "raids", "the address's room half to be honoured");
        app.WaitForDump("shellPage", "progress", "and its page half");
    }

    /// <summary>
    /// **Two hosts of one room must report the same numbers.** The shell builds its own
    /// instances of the Progress surfaces (a UIElement has one parent — trap 45), and
    /// every RULE behind them is shared, so a divergence between the two would be a real
    /// defect and an invisible one: both windows render, both look right, and nothing but
    /// a comparison can tell.
    ///
    /// Both are open at once here on purpose. That is the condition trap 45's exemption
    /// note calls out — *"the day one of them expands in place, it is the Progress crash
    /// again"* — and on WPF the symptom is not a crash but a surface silently vanishing
    /// from whichever host drew it first, which these row counts would catch.
    /// </summary>
    [Fact]
    public void TheShellAndTheProgressWindowAgreeAboutTheSameRoom()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "progress:raids",
            ["EQBUDDY_PROGRESS"] = "raids",
        });
        app.Launch();

        app.WaitForDump("shellProgressTab", "raids", "both hosts to reach the Raids room");
        Assert.Equal(app.DumpValue("progressTabs"), app.DumpValue("shellProgressTabs"));
        Assert.Equal(app.DumpValue("progressRaidsRows"), app.DumpValue("shellProgressRaidsRows"));
        Assert.Equal(app.DumpValue("progressFaction"), app.DumpValue("shellProgressFaction"));
        Assert.Equal(app.DumpValue("progressMotesRows"), app.DumpValue("shellProgressMotesRows"));
    }

    // ---- E-3 PR 2: the World and Gear rooms ------------------------------------

    /// <summary>
    /// The two rooms PR 2 moved in, each opened by ADDRESS and each landing inside the
    /// room rather than merely on it.
    ///
    /// **The room keys here look like typos and are not**, which is the point of asserting
    /// them from outside. `WorldTab.Camps` is `"spawns"` (the window it absorbed),
    /// `WorldTab.Travels` is `"misc"` (the old card's settings key, kept so the World fold
    /// needed no settings migration at all), and `LootTab.Gear` is `"gear"` while its label
    /// reads "Wishlist". The shell maps those; it must never re-spell one, because a second
    /// name for one destination is trap 33 lifted into navigation.
    /// </summary>
    [Theory]
    [InlineData("world", "shellWorldTab", "misc")]
    [InlineData("world:map", "shellWorldTab", "map")]
    [InlineData("world:spawns", "shellWorldTab", "spawns")]
    [InlineData("world:travel", "shellWorldTab", "travel")]
    [InlineData("gear", "shellGearTab", "loot")]
    [InlineData("gear:gear", "shellGearTab", "gear")]
    [InlineData("gear:inventory", "shellGearTab", "inventory")]
    public void EveryLandedRoomIsReachableByItsOwnAddress(string address, string key, string room)
    {
        using var app = new AppHarness(environment: OpenOn(address));
        app.Launch();

        app.WaitForDump(key, room, $"the shell to land on {address}");
        Assert.Equal(ShellPages.Landed.Count, app.DumpValue("shellRail"));
    }

    /// <summary>
    /// **Two hosts of the World room must report the same numbers.** The shell builds its
    /// OWN `MapView`, `SpawnsView`, `TravelView` and `TravelsView` (a UIElement has one
    /// parent — trap 45), and every rule behind them is shared, so a divergence would be a
    /// real defect and an invisible one: both windows render, both look right.
    ///
    /// Both are open at once and both are on Camps, which is the only tab both hooks can
    /// address. The `shellWorld*` keys are the SAME strings the views hand `WorldWindow`,
    /// re-prefixed — so this asserts that the two hosts agree, and it could not have been
    /// written at all if the shell had reported its own hand-written copies of the numbers.
    /// </summary>
    [Fact]
    public void TheShellAndTheWorldWindowAgreeAboutTheSameRoom()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "world:spawns",
            ["EQBUDDY_SPAWNS"] = "1",
        });
        app.Launch();

        app.WaitForDump("shellWorldTab", "spawns", "both hosts to reach the Camps room");
        app.WaitForDump("worldTab", "spawns", "and the v1 window with them");
        Assert.Equal(app.DumpValue("worldTabs"), app.DumpValue("shellWorldTabs"));
        Assert.Equal(app.DumpValue("spawnsRows"), app.DumpValue("shellWorldSpawnsRows"));
        Assert.Equal(app.DumpValue("spawnsZones"), app.DumpValue("shellWorldSpawnsZones"));
        Assert.Equal(app.DumpValue("mapZones"), app.DumpValue("shellWorldMapZones"));
        Assert.Equal(app.DumpValue("travelZones"), app.DumpValue("shellWorldTravelZones"));
    }

    /// <summary>
    /// The same comparison for the Gear room — and it carries one extra row that no
    /// screenshot could ever supply.
    ///
    /// **`gearCopyCmd` is the ⧉ copy of `/outputfile inventory`**, the only in-app route to
    /// the command that makes the wishlist tick itself. An absent control photographs as an
    /// unremarkable panel (trap 29), so a picture of the new host cannot say whether the
    /// affordance survived the move; a launched app comparing the two hosts can. That is
    /// trap 34's whole lesson — a missing thing is invisible to everything except a
    /// must-list or an assertion.
    /// </summary>
    [Fact]
    public void TheShellAndTheGearLootWindowAgreeAboutTheSameRoom()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "gear",
            ["EQBUDDY_GEARLOOT"] = "loot",
        });
        app.Launch();

        app.WaitForDump("shellGearTab", "loot", "both hosts to reach the Loot room");
        app.WaitForDump("gearLootTab", "loot", "and the v1 window with them");
        Assert.Equal(app.DumpValue("gearLootTabs"), app.DumpValue("shellGearTabs"));
        Assert.Equal(app.DumpValue("lootRows"), app.DumpValue("shellGearLootRows"));
        Assert.Equal(app.DumpValue("gearRows"), app.DumpValue("shellGearRows"));
        Assert.Equal(app.DumpValue("gearPivotShown"), app.DumpValue("shellGearPivotShown"));
        Assert.Equal(app.DumpValue("gearCopyCmd"), app.DumpValue("shellGearCopyCmd"));
    }

    /// <summary>
    /// The gear list's cap FOLLOWS the room's body rather than being a card-sized
    /// constant — asserted as the relationship, because a hosted runner is 1024×768 and a
    /// number would be an assertion about the desk this was written on.
    ///
    /// `#250` PR 2 made this true for the window (dragging it taller used to grow the
    /// window and leave the gear list exactly where it was, which is a resize that visibly
    /// does nothing). The room has to inherit that, and the failure it prevents is the one
    /// that reads as "the list stops one row early" — which photographs as a list.
    /// </summary>
    [Fact]
    public void TheGearListCapComesFromTheRoomsBodyAndNotFromAConstant()
    {
        using var app = new AppHarness(environment: OpenOn("gear:gear"));
        app.Launch();

        app.WaitForDump("shellGearTab", "gear", "the Wishlist room to paint");
        // A measured height is 0 until WPF has laid the room out, which happens after the
        // window is shown — so this waits for the app to have COMPUTED the number rather
        // than asserting against one it has not.
        app.WaitForDumpAtLeast("shellGearBodyCap", 1, "the room to measure its own body");
        var body = app.DumpValue("shellGearBodyCap");
        var list = app.DumpValue("shellGearListCap");
        // NestedBodyCap's own two rules: never below the 120-unit floor, and never more
        // than the host body it is nested inside.
        Assert.True(list >= 120 && list <= Math.Max(120, body),
            $"gear list cap {list} does not follow room body {body}; dump was: {app.Artifacts()}");
    }

    /// <summary>
    /// Degrade axis 1, asserted as the RELATIONSHIP rather than as a picture.
    ///
    /// `ShellNavigationTests` proves the arithmetic; it cannot prove the window applied
    /// it, and the gap between those two claims is trap 42 — a fix that was genuinely in
    /// the binary, correct in the diff, green in the tests, and not in effect at runtime,
    /// for two builds. So this reads the width the window actually has and asserts the
    /// answer the policy gives for THAT width, which holds at 1024×768 or at 4K.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("580x480")]
    public void TheRailsLabelsFollowTheWidthTheWindowActuallyHas(string? size)
    {
        using var app = new AppHarness(environment: OpenOn("progress", size));
        app.Launch();
        app.WaitForDump("shellPage", "progress", "the shell to open");

        var width = app.DumpValue("shellWidth");
        Assert.True(width >= ShellLayoutPolicy.MinWidth,
            $"the floor must hold: shellWidth={width}; dump was: {app.Artifacts()}");
        Assert.Equal(
            ShellLayoutPolicy.For(width).RailLabelsVisible ? 1 : 0,
            app.DumpValue("shellRailLabels"));
    }
}
