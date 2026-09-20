using EQBuddy.Core;
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
    /// **THE DEFAULT LANDING, walked the way nothing walked it before E-3 PR 4.**
    ///
    /// `EQBUDDY_SHELL=1` is the bare form of the hook: no address, open on whatever the
    /// window's own default is. Every other assertion in this file navigates to an EXPLICIT
    /// address, which means that until this test existed there was no coverage at all of the
    /// one path that could catch `ShellHost` disagreeing with `ShellWindow` about what the
    /// default room is — and they disagreed by construction, because the fact was written in
    /// three places (the field, the constructor's own `Navigate` call, and the hook's
    /// literal). The hook now passes no address at all, so the window's constructor is the
    /// only place the answer exists; this is what says so from outside.
    ///
    /// **It was Progress until PR 4 and it is Home now**, which is the flip Bevel's
    /// pre-design named and Helm signed: Progress was an explicit placeholder for a room
    /// nobody had built, and `HomeRoom` is the room designed to answer "where do I stand".
    /// </summary>
    [Fact]
    public void TheShellOpensOnHomeWhenTheHookNamesNoRoomAtAll()
    {
        using var app = new AppHarness(environment: OpenOn("1"));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the bare hook to land on the shell's default room");
        Assert.Equal(ShellPages.Landed.Count, app.DumpValue("shellRail"));
        // The room actually painted, rather than merely being selected on the rail.
        Assert.Equal(3, app.DumpValue("shellHomeBlocks"));
    }

    /// <summary>
    /// **EVERY LAUNCH IN THIS SUITE OPENS THE EVOLVED SHELL, and this is the row that
    /// says so.** The owner's standing order while E-3 is being built is that a suite run
    /// must not pop a bare v1 widget: `AppHarness.Launch` therefore sets
    /// `EQBUDDY_SHELL=1` for every test that does not name an address of its own.
    ///
    /// A default nobody asserts is a default that comes back off — silently, because
    /// every other test in this file passes its own address and would go on passing with
    /// the harness line deleted, and every test in `EndToEndTests` asks about the widget
    /// and would not notice either. Trap 34's shape: the thing to check is the one nobody
    /// is looking at, and it is checked by asking for NOTHING and finding the shell there
    /// anyway.
    ///
    /// The opt-out is asserted beside it, for the same reason a "no X may do Y" guard
    /// needs its "these must do Y" list — an empty value is what a scenario that wants
    /// the widget alone passes, and if it stopped working nothing else would say so.
    /// </summary>
    [Fact]
    public void TheHarnessOpensTheEvolvedShellWithNoScenarioAskingForIt()
    {
        using (var app = new AppHarness())
        {
            app.Launch();
            app.WaitForDump("shellPage", "home",
                "the harness's own default to bring the Evolved shell up beside the widget");
        }

        using var widgetOnly = new AppHarness(
            environment: new Dictionary<string, string> { ["EQBUDDY_SHELL"] = "" });
        widgetOnly.Launch();
        // The widget is up (Launch waited for a live session), the shell is not: the hook
        // reads `is { Length: > 0 }`, so an empty value is the opt-out.
        Assert.Equal("", widgetOnly.DumpText("shellPage"));
    }

    /// <summary>
    /// **The shell opens beside the game, not on top of it — and this asserts the
    /// RELATIONSHIP rather than a monitor.** The XAML's `CenterScreen` centres on the
    /// PRIMARY screen, which is where EverQuest is; the constructor overrides it whenever
    /// the desk has a display beside the primary one.
    ///
    /// `WindowPlacement.SecondaryOrigin` is unit-tested, so the arithmetic is covered
    /// without a screen. What no unit test can say is whether the WINDOW applied it —
    /// "present in the build" and "in effect at runtime" are different claims and only the
    /// second is the feature (trap 42), and a placement shows up in neither a diff nor a
    /// screenshot. So this reads the same `SystemParameters` the app did and asserts that
    /// the answer follows from the desk: wide desk → placed, single screen → the XAML's
    /// default left alone. On a 1024×768 hosted runner it proves the fallback; on a
    /// two-monitor desk it proves the feature. Neither run asserts a number.
    /// </summary>
    [Fact]
    public void TheShellOpensOffThePrimaryScreenWhenTheDeskHasRoomBesideIt()
    {
        using var app = new AppHarness(environment: OpenOn("1"));
        app.Launch();
        app.WaitForDump("shellPage", "home", "the shell to open");

        // The same question the window asked, asked again from the same desk — with the
        // size out of `ShellLayoutPolicy` rather than retyped, because a band that cannot
        // hold the window is not a place to open one and the window's size is therefore
        // part of the question.
        var expected = EQBuddy.Core.WindowPlacement.SecondaryOrigin(
            System.Windows.SystemParameters.VirtualScreenLeft,
            System.Windows.SystemParameters.VirtualScreenTop,
            System.Windows.SystemParameters.VirtualScreenWidth,
            System.Windows.SystemParameters.VirtualScreenHeight,
            System.Windows.SystemParameters.PrimaryScreenWidth,
            ShellLayoutPolicy.OpenWidth, ShellLayoutPolicy.OpenHeight) is not null;

        Assert.Equal(expected ? 1 : 0, app.DumpValue("shellSecondary"));
    }

    /// <summary>
    /// The host opens, the rail draws, the Search affordance is there, and the Progress
    /// room paints — the four things E-3 PR 1 claims to have built.
    ///
    /// **This is an ADDRESSED case and its name now says so.** It opens on
    /// `EQBUDDY_SHELL=progress`, which is an explicit address and has always been one; the
    /// old name (*"TheShellOpensOnProgress…"*) read as coverage of the DEFAULT landing and
    /// was not, which is how the default could have flipped underneath it with everything
    /// green. The bare-hook default is the test above.
    ///
    /// **`shellRail` is the assertion with teeth.** The signed pre-design refuses a
    /// disabled row for a room that has not shipped (*"an affordance that opens nothing
    /// is a trap"*), so this number is the count of rows DRAWN and it must equal the
    /// number of rooms that exist. The day a seventh row appears without a room behind
    /// it — or a room lands without joining the rail — this is what says so.
    /// </summary>
    [Fact]
    public void TheProgressRoomIsReachableByItsOwnAddressWithARailRowPerLandedRoom()
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
        // The room actually painted. FOUR tabs since E-3 S3 — Core's ProgressSurface names
        // five, the Progress WINDOW draws four, and this room draws five minus what the
        // reshape moved (Raids). Written as the subtraction rather than as a literal, so a
        // sixth Progress tab does not have to be typed here twice.
        Assert.Equal(
            ProgressSurface.Tabs().Count(h => !ProgressSurface.MovedToLive(h.Tab)),
            app.DumpValue("shellProgressTabs"));
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
        using var app = new AppHarness(environment: OpenOn("progress:faction"));
        app.Launch();

        app.WaitForDump("shellProgressTab", "faction", "the address's room half to be honoured");
        app.WaitForDump("shellPage", "progress", "and its page half");
    }

    /// <summary>
    /// **`progress:raids` is a dead address now, and it must land NOWHERE rather than
    /// somewhere wrong.** E-3 PR 5 moved Raids to the Live room; `ProgressSurface.TabForKey`
    /// still resolves `"raids"` (an old saved tab choice has to land somewhere true), so the
    /// room's own refusal is the only thing between that key and a Progress room lighting no
    /// chip over a body it did not change. This asserts the refusal from outside, which is
    /// the only place it can be seen: nothing in a diff, a build or a screenshot shows a
    /// `SetTab` that returned early.
    ///
    /// The shell still opens — an unrecognised room half leaves the page alone rather than
    /// refusing the whole address, which is what `Navigate` has always done.
    /// </summary>
    [Fact]
    public void TheOldRaidsAddressUnderProgressLandsOnNoTabRatherThanTheWrongOne()
    {
        using var app = new AppHarness(environment: OpenOn("progress:raids"));
        app.Launch();

        app.WaitForDump("shellPage", "progress", "the page half to still be honoured");
        // The room's DEFAULT tab, untouched by a key it no longer draws.
        Assert.Equal("progress", app.DumpText("shellProgressTab"));
        Assert.Equal(
            ProgressSurface.Tabs().Count(h => !ProgressSurface.MovedToLive(h.Tab)),
            app.DumpValue("shellProgressTabs"));
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
            ["EQBUDDY_SHELL"] = "progress:faction",
            ["EQBUDDY_PROGRESS"] = "faction",
        });
        app.Launch();

        app.WaitForDump("shellProgressTab", "faction", "both hosts to reach the Faction room");
        Assert.Equal(app.DumpValue("progressFaction"), app.DumpValue("shellProgressFaction"));
        Assert.Equal(app.DumpValue("progressMotesRows"), app.DumpValue("shellProgressMotesRows"));
        Assert.Equal(app.DumpValue("progressSkills"), app.DumpValue("shellProgressSkills"));

        // **The tab counts deliberately differ, in BOTH directions now, and asserting each
        // difference from its own predicate is the point.** E-3 PR 5 moved Raids to Live, so
        // the v1 window has one the room does not; E-3 S3 added History to the room alone, so
        // the room has one the window does not. The two happen to cancel to the same NUMBER
        // today, which is exactly why neither is asserted as a count: a `-1` or an equality
        // here would pass while either filter silently stopped working.
        Assert.Equal(
            ProgressSurface.Tabs().Count(h => !ProgressSurface.MovedToLive(h.Tab)),
            app.DumpValue("shellProgressTabs"));
        Assert.Equal(
            ProgressSurface.Tabs().Count(h => !ProgressSurface.DesktopShellOnly(h.Tab)),
            app.DumpValue("progressTabs"));
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
    // E-3 lane S, S2's tab, and the first test of the `page:room` grammar past the four
    // rooms World launched with. Nothing in `ShellPages` or `ShellHost` was edited to make
    // this address resolve — `Rooms(World)` reads `WorldSurface.Tabs()` live, so a fifth tab
    // in Core IS a fifth address. Asserted rather than assumed, because "it should just
    // work" is how a grammar quietly stops covering its own surface (trap 55).
    [InlineData("world:drops", "shellWorldTab", "drops")]
    [InlineData("gear", "shellGearTab", "loot")]
    [InlineData("gear:gear", "shellGearTab", "gear")]
    [InlineData("gear:inventory", "shellGearTab", "inventory")]
    // E-3 PR 3's room. `general` is the catalog and the only tab with a detail pane;
    // `unlocks` is the one whose own window could not be opened for review at all until
    // SetTab started resolving through Core's key table, which is why it is asserted here
    // rather than assumed to come along with the other three.
    [InlineData("quests", "shellQuestsTab", "general")]
    [InlineData("quests:epic", "shellQuestsTab", "epic")]
    [InlineData("quests:sky", "shellQuestsTab", "sky")]
    [InlineData("quests:unlocks", "shellQuestsTab", "unlocks")]
    // E-3 PR 4's room, and the only one whose page key IS the assertion: Home has no rooms
    // inside it (`ShellPages.Rooms(Home)` is empty — three blocks on one page IS the room),
    // so there is no tab key to land on and `shellPage` is what there is to check.
    [InlineData("home", "shellPage", "home")]
    // SR-5's room, and the last row of the rail. The four keys are `SettingsSurface`'s;
    // `settings:cards` is the v1 Options tab TAG kept as an alias, so a saved `OptionsTab`,
    // an old script line and an old habit all land on the tab that content is actually on
    // rather than nowhere — the same courtesy `gear:wishlist` and `world:misc` already carry,
    // and the reason those two rows above look like typos and are not.
    [InlineData("settings", "shellSettingsTab", "look")]
    [InlineData("settings:look", "shellSettingsTab", "look")]
    [InlineData("settings:alerts", "shellSettingsTab", "alerts")]
    [InlineData("settings:hud", "shellSettingsTab", "hud")]
    [InlineData("settings:cards", "shellSettingsTab", "hud")]
    [InlineData("settings:behavior", "shellSettingsTab", "behavior")]
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
        // **The tab COUNTS deliberately differ since S2, and asserting the difference is the
        // point** — the same shape the Progress pair above already carries, from the other
        // direction: Progress lost a tab to Live, World GAINED one from Kills & Drops. The
        // v1 window cannot draw it (`_ => _travels.Body`), so `WorldSurface.ShellOnly` keeps
        // it off that strip; an equality here would have to be "fixed" either by breaking
        // the window or by taking the room's fifth tab away. Written as the predicate's own
        // count rather than as a 1, so a sixth shell-only tab needs no edit here.
        Assert.Equal(
            app.DumpValue("worldTabs") + WorldSurface.Tabs().Count(h => WorldSurface.ShellOnly(h.Tab)),
            app.DumpValue("shellWorldTabs"));
        Assert.Equal(app.DumpValue("spawnsRows"), app.DumpValue("shellWorldSpawnsRows"));
        Assert.Equal(app.DumpValue("spawnsZones"), app.DumpValue("shellWorldSpawnsZones"));
        Assert.Equal(app.DumpValue("mapZones"), app.DumpValue("shellWorldMapZones"));
        Assert.Equal(app.DumpValue("travelZones"), app.DumpValue("shellWorldTravelZones"));
    }

    /// <summary>
    /// **The Drops surface, from its two live hosts at once** — the shell's World room and
    /// the v1 `CreatureWindow`, each with its own `DropsCardView` (a UIElement has one
    /// parent, trap 45). Every number behind them is shared, so a divergence would be a real
    /// defect and an invisible one: both windows render, both look right.
    ///
    /// Two doors, not one: `EQBUDDY_SHELL=world:drops` is the room's address through the
    /// `page:room` grammar, `EQBUDDY_DROPS=1` is the v1 window's own hook, and the two stay
    /// independent on purpose. This is the assertion those five hand-written
    /// `shellWorldDrops*` facts exist FOR — `dropsRows` beside `shellWorldDropsRows` is the
    /// comparison trap 58's per-host prefixing keeps possible instead of colliding.
    ///
    /// `dropsRecheck` is the row a screenshot could never supply: the wiki re-check ↻ on
    /// every creature heading (#226) is a control, and an absent control photographs as an
    /// unremarkable header (trap 29/34). Nothing but a launched app comparing two hosts can
    /// say the new one kept it.
    ///
    /// **The room paints Drops on every tick rather than only when it is the visible tab**,
    /// which is what makes this assertion hold from a room sitting on Camps as readily as
    /// from one sitting on Drops — the same reason `CreatureWindow` renders both of its own
    /// tabs, written down there as "the inactive tab's BADGE has to stay true".
    /// </summary>
    [Fact]
    public void TheShellAndTheCreatureWindowAgreeAboutTheDropsTheyBothShow()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "world:drops",
            ["EQBUDDY_DROPS"] = "1",
        });
        app.Launch();

        app.WaitForDump("shellWorldTab", "drops", "the shell to land on World's Drops tab");
        app.WaitForDump("creatureTab", "drops", "and the v1 window with it");
        // The negative that stops the five rows below going vacuous (trap 39): two hosts
        // that both showed NOTHING would agree perfectly and prove nothing. The fixture
        // kills things and loots them, so this is a wait rather than a hope — and it has to
        // be a wait, because the replay lands after the windows do.
        app.WaitForDumpAtLeast("shellWorldDropsMobs", 1,
            "the fixture replay to land at least one creature with a drop in the shell's room");

        Assert.Equal(app.DumpValue("dropsMobs"), app.DumpValue("shellWorldDropsMobs"));
        Assert.Equal(app.DumpValue("dropsRows"), app.DumpValue("shellWorldDropsRows"));
        Assert.Equal(app.DumpValue("dropsItems"), app.DumpValue("shellWorldDropsItems"));
        Assert.Equal(app.DumpValue("dropsFilterLen"), app.DumpValue("shellWorldDropsFilterLen"));
        Assert.Equal(app.DumpValue("dropsRecheck"), app.DumpValue("shellWorldDropsRecheck"));
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
        // The import block SR-2 moved off Options → Cards & windows, for the same reason
        // the ⧉ row above is here: it is the only route into the import, and "both hosts
        // got it" is a claim a screenshot of either one cannot make.
        Assert.Equal(app.DumpValue("gearImport"), app.DumpValue("shellGearImport"));
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

    // ---- E-3 PR 3: the Quests room ---------------------------------------------

    /// <summary>
    /// **Two hosts of the LIFTED surface must report the same numbers**, and this is the
    /// row that makes the lift a lift rather than a rewrite.
    ///
    /// World and Gear were a MOVE: their v1 windows were already compositions of shared
    /// views, so "both hosts agree" mostly asserted that nothing had been retyped. Quests
    /// was 2,481 lines of window-owned rendering with no view at all, and the whole claim
    /// of E-3 PR 3 is that those lines came out INTACT — same rules, same wording, same
    /// counts, now with two hosts instead of one. Every key below is the SAME string
    /// <c>QuestsView.DebugFacts</c> hands both of them, re-prefixed for the shell
    /// (<c>ShellDumpFacts</c>), so this comparison could not have been written at all if
    /// the room had reported hand-written copies of the numbers (trap 58).
    ///
    /// Both hosts are open at once and both are on the catalog tab — the only tab with a
    /// list to count. On WPF a shared view would not throw, it would silently vanish from
    /// whichever host drew it first (trap 45), which these counts would catch.
    /// </summary>
    [Fact]
    public void TheShellAndTheQuestsWindowAgreeAboutTheSameRoom()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "quests:general",
            ["EQBUDDY_QUESTS"] = "general",
        });
        app.Launch();

        app.WaitForDump("shellQuestsTab", "general", "both hosts to reach the catalog room");
        app.WaitForDump("questsTab", "general", "and the v1 window with them");
        // A floor before the comparisons, per trap 39: two hosts that both rendered
        // NOTHING would agree perfectly, and that is the failure a lift is most likely to
        // produce. The number itself is the fixture's and is not asserted.
        Assert.True(app.DumpValue("shellQuestsRows") >= 1,
            $"the room rendered no quest rows at all; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("questsTabs"), app.DumpValue("shellQuestsTabs"));
        Assert.Equal(app.DumpValue("questsModes"), app.DumpValue("shellQuestsModes"));
        Assert.Equal(app.DumpValue("questsRows"), app.DumpValue("shellQuestsRows"));
        Assert.Equal(app.DumpValue("questsSuppressed"), app.DumpValue("shellQuestsSuppressed"));
        Assert.Equal(app.DumpValue("questsSelected"), app.DumpValue("shellQuestsSelected"));
        Assert.Equal(app.DumpValue("questsReadySummary"),
            app.DumpValue("shellQuestsReadySummary"));
    }

    /// <summary>
    /// The five Helm-signed presentation rules, asserted to have survived the lift into the
    /// second host — which is the thing Bevel's pre-design flagged as the risk of a LIFT
    /// over a MOVE (*"a rule with a home and no reader"*, trap 20's mirror).
    ///
    /// **A picture cannot say any of this.** Four of the five are on the Sky tab, and an
    /// absent control photographs as an unremarkable panel (trap 29): the ⧉ copies of
    /// <c>/outputfile achievements</c> and <c>/outputfile inventory</c> that feed the tab,
    /// and the two #243 leftover bands whose fold state is session-only. So they are
    /// counted off the real visual tree in BOTH hosts and compared.
    ///
    /// The counts themselves are whatever the fixture produces — asserting a number would
    /// be asserting the profile this was written against. What must hold is that the two
    /// hosts see the same thing.
    /// </summary>
    [Fact]
    public void TheSkyTabsRulesSurvivedTheLiftIntoTheSecondHost()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "quests:sky",
            ["EQBUDDY_QUESTS"] = "sky",
        });
        app.Launch();

        app.WaitForDump("shellQuestsTab", "sky", "both hosts to reach the Sky room");
        app.WaitForDump("questsTab", "sky", "and the v1 window with them");
        // The two commands the tab is FED by. `GameCommandsTests` proves the source is
        // shared; only a launched app can say the buttons are on screen in both hosts.
        //
        // **Asserted as PRESENT and then as equal, in that order** — trap 39's lesson:
        // `Assert.Equal(0, 0)` passes forever and reads as coverage, and 0 is exactly what
        // a room that had silently lost the affordance would report. These two rows have a
        // must-list guarantee behind them (`GameCommandsTests.SurfacesNeedingACommand`), so
        // there is a floor to assert; the band counts below have none and stay equality-only.
        Assert.True(app.DumpValue("shellQuestsSkyCopyCmd") >= 1,
            $"the room's ⧉ /outputfile achievements is missing; dump was: {app.Artifacts()}");
        Assert.True(app.DumpValue("shellQuestsSkyInvCopyCmd") >= 1,
            $"the room's ⧉ /outputfile inventory is missing; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("questsSkyCopyCmd"), app.DumpValue("shellQuestsSkyCopyCmd"));
        Assert.Equal(app.DumpValue("questsSkyInvCopyCmd"),
            app.DumpValue("shellQuestsSkyInvCopyCmd"));
        // The three #243/#129 bands and their session-only folds.
        Assert.Equal(app.DumpValue("questsSkyReady"), app.DumpValue("shellQuestsSkyReady"));
        Assert.Equal(app.DumpValue("questsSkyLeftoverA"),
            app.DumpValue("shellQuestsSkyLeftoverA"));
        Assert.Equal(app.DumpValue("questsSkyLeftoverB"),
            app.DumpValue("shellQuestsSkyLeftoverB"));
        Assert.Equal(app.DumpValue("questsSkyReadyOpen"),
            app.DumpValue("shellQuestsSkyReadyOpen"));
        // A checklist tab has nothing to select, so BOTH hosts collapse the detail pane
        // and give its width to the rows — the Gate 2 rule, unchanged by the lift and
        // unchanged by single-pane, which only ever applies to the catalog.
        Assert.Equal(0, app.DumpValue("shellQuestsDetailShown"));
        Assert.Equal(1, app.DumpValue("shellQuestsListShown"));
        // And the caption that replaced the window's title row still says something. Its
        // LENGTH, not its text: the name in it is the player's character.
        Assert.True(app.DumpValue("shellQuestsHeading") > 0,
            $"the room's character caption is empty; dump was: {app.Artifacts()}");
    }

    /// <summary>
    /// **Degrade axis 2, reaching a room for the first time since PR 1 decided it.**
    /// `ShellLayout.RoomSinglePane`'s own comment has said *"no room expresses this yet"*
    /// for two PRs; the Quests catalog is a list beside a detail pane, so it is the first
    /// thing that can disprove the formula.
    ///
    /// **Asserted as the RELATIONSHIP, never as a picture.** A hosted runner is 1024×768,
    /// so "the room shows one pane" would be an assertion about the desk this was written
    /// on. The dump carries the INPUT (`shellWidth`) beside the shell's answer
    /// (`shellRoomSinglePane`) beside the room's state (`shellQuestsSinglePane`), and what
    /// is asserted is that all three follow from each other — which holds at any size the
    /// window is actually given.
    ///
    /// **The third of those is the one with teeth**, and it is trap 42's shape: PR 1's
    /// arithmetic was correct, green and unwired, and the gap between "the policy says so"
    /// and "the room did it" is exactly where a fix can sit in the binary for two builds
    /// without being in effect.
    /// </summary>
    [Theory]
    // Both sides of the threshold: the room's share is the window minus the 200-wide rail,
    // so at 900 it is exactly SplitRoomWidth (700) and splits, and one unit narrower it
    // cannot. The rail is expanded at both (RailLabelWidth is 720), which is what makes
    // this a test of ONE axis. The pair moved from 840/839 when the first screenshot at
    // the old 640 threshold broke a quest title mid-word — see ShellLayoutPolicy.
    [InlineData("900x640")]
    [InlineData("899x640")]
    [InlineData(null)]
    public void TheQuestsRoomFollowsTheSplitThresholdTheWindowActuallyHas(string? size)
    {
        using var app = new AppHarness(environment: OpenOn("quests:general", size));
        app.Launch();
        app.WaitForDump("shellQuestsTab", "general", "the catalog room to paint");

        var width = app.DumpValue("shellWidth");
        var expected = ShellLayoutPolicy.For(width).RoomSinglePane;
        Assert.Equal(expected ? 1 : 0, app.DumpValue("shellRoomSinglePane"));
        // The ROOM did it, not just the policy.
        Assert.Equal(expected ? 1 : 0, app.DumpValue("shellQuestsSinglePane"));
        // And the arrangement follows: split shows both panes, single shows the list and
        // no way back (there is nothing to come back FROM until a row is clicked).
        Assert.Equal(1, app.DumpValue("shellQuestsListShown"));
        Assert.Equal(expected ? 0 : 1, app.DumpValue("shellQuestsDetailShown"));
        Assert.Equal(0, app.DumpValue("shellQuestsBackShown"));
        // **The two axes moved independently**, which is the claim the separate thresholds
        // exist to make and the one a single number could not. At 839 the room has already
        // collapsed while the rail still has room for its labels; asserting the rail's
        // answer from the same width is what says the two were decided apart rather than
        // together.
        Assert.Equal(ShellLayoutPolicy.For(width).RailLabelsVisible ? 1 : 0,
            app.DumpValue("shellRailLabels"));
    }

    // ---- E-3 PR 4: the Home room ------------------------------------------------

    /// <summary>
    /// The three blocks Home draws, and its refusal to offer a room that does not exist.
    ///
    /// **THREE since DRA-63** (Founder smoke 2026-09-11). Bevel's door 1 locked four, the
    /// fourth being a "Go to" list of every landed room — written when the rail had one row
    /// on it, and by the time all seven had landed a second copy of the rail under the fold
    /// in the same window. The rail owns the doors.
    ///
    /// **`shellHomeDeadLinks` is the row with teeth, and it did NOT leave with the block it
    /// was written for.** The rail cannot draw a row for an unlanded room because
    /// `BuildRail` filters `ShellPages.Landed` — but Home's body is a SECOND navigation
    /// surface, inside a room, where the rail's guard cannot see it, and it still is: a
    /// readiness row whose dump has landed offers "Open" into the surface that uses it. A
    /// hand-written address there would compile, render, photograph perfectly and open
    /// nothing, which is the exact shape this codebase already ruled on (*"an empty class
    /// row gets no chevron — an affordance that opens nothing is a trap"*). Deleting the
    /// assertion with the block would have retired a guard whose subject survived it.
    ///
    /// **This launch stages no dumps**, so `shellHomeLinks` is 0 here and the teeth are in
    /// <see cref="ReadinessAsksForTheDumpsThatAreMissingAndStillOffersTheCatchUpForTheOneThatLanded"/>,
    /// which stages one and asserts the link appears AND is not dead. Both are needed: this
    /// row is the one that can see a room drawing links out of nothing at all.
    /// </summary>
    [Fact]
    public void TheHomeRoomDrawsThreeBlocksAndOffersNoLinkThatOpensNothing()
    {
        using var app = new AppHarness(environment: OpenOn("home"));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to land on the Home room");
        Assert.Equal(3, app.DumpValue("shellHomeBlocks"));
        // The room-level empty is for a profile with NO character; this one is following.
        Assert.Equal(0, app.DumpValue("shellHomeEmpty"));
        Assert.Equal(1, app.DumpValue("shellHomeIdentity"));

        // No dump has landed on this profile, so no row has an "Open" to offer — and a room
        // that grew a second door list would be caught here rather than by the count below.
        Assert.Equal(0, app.DumpValue("shellHomeLinks"));
        Assert.Equal(0, app.DumpValue("shellHomeDeadLinks"));
        // The floor that stops the zeroes above reading as coverage (trap 39): the room DID
        // render, and it rendered the readiness rows the "Open" would have hung off.
        Assert.Equal(4, app.DumpValue("shellHomeReadiness"));

        // The class reading (DRA-66) is ON the identity block of the launched app — the
        // unit suite owns the words and the precedence, this owns "the line is there".
        // The SOURCE is whatever the fixture log earned (a profile with no dumps and no
        // statement can only be unknown or inferred — a "picked" or "stated" here would
        // mean state leaked into a fresh profile), nobody has STATED anything, and the
        // editor's sixteen chips are not built while its door is shut (trap 29 is why the
        // count exists; 0 is this state's honest number).
        var source = app.DumpText("shellHomeClassSource");
        Assert.True(source is "unknown" or "inferred",
            $"a fresh profile's class source was '{source}'; dump was: {app.Artifacts()}");
        Assert.Equal(0, app.DumpValue("shellHomeStated"));
        Assert.Equal(0, app.DumpValue("shellHomeClassChips"));
    }

    /// <summary>
    /// **The class line moves to the game's own answer when the achievements dump LANDS**
    /// (DRA-66, plan A3's E2E half) — lands meaning the game ANNOUNCES it in the log,
    /// because a file sitting on disk reaches the readiness row's date and never the
    /// ledger's class list: the auto-import is announcement-driven, and the first cut of
    /// this test staged the file, watched the source stay <c>inferred</c> for 90 s, and
    /// taught this comment. The class the dump names is deliberately NOT the fixture's
    /// own inferred Warrior, so the source flip is visible in the CLASS too — a
    /// Warrior-naming dump would flip the parenthetical while the line's text stood
    /// still, which is half an assertion. Waited on as a positive event (trap 62); the
    /// unit suite owns the precedence table, this owns "the running app reaches that
    /// state" (trap 42's gap).
    /// </summary>
    [Fact]
    public void TheClassLineReadsTheAchievementsDumpWhenOneLands()
    {
        using var app = new AppHarness(environment: OpenOn("home"));
        app.WriteAchievementsDump(
            "Untapped Potential: Classes",
            "C\tPrimary Class Unlock - Cleric",
            "C\t\tThis achievement will autocomplete.");
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to land on the Character room");
        // The game's own announcement is what turns the file into an import.
        app.AppendLogLines("Outputfile Complete: Testchar_test-Achievements.txt");

        app.WaitForDump("shellHomeClassSource", "achievements",
            "the class line to read from the announced achievements dump");
        // Cleric FIRST (the dump leads), the fixture's inferred Warrior still unioned in
        // behind it — the dump is a snapshot and must not silence live evidence.
        app.WaitForDump("shellHomeClass", "Cleric,Warrior",
            "the dump's class to lead the line with the log's own still behind it");
        // Nobody has stated anything, and the editor is collapsed to the dump sentence —
        // no chip strip exists to have been built.
        Assert.Equal(0, app.DumpValue("shellHomeStated"));
        Assert.Equal(0, app.DumpValue("shellHomeClassChips"));
    }

    /// <summary>
    /// Readiness: the ⧉ copies, and the one row whose dump has actually landed.
    ///
    /// **Only a launched app can say a control EXISTS.** A surface that asks the player for
    /// an output file and hands them no way to run it is the defect David reported on
    /// 2026-08-20, and an absent control photographs as an unremarkable panel (trap 29).
    /// `GameCommandsTests.SurfacesNeedingACommand` proves the source NAMES the four
    /// commands; this proves the buttons are on screen.
    ///
    /// **DRA-63 inverted the half with teeth, and that is the point of this row's new
    /// shape.** The ⧉ used to be drawn only for a never-scanned row, so `shellHomeCopyCmd`
    /// and `shellHomeReadinessWaiting` were the same number by construction — a dump key
    /// restating the condition it was derived from rather than measuring the tree (trap 64b's
    /// shape: a proxy standing in for the fact). Now the copies must equal the ROW count
    /// whatever each row's state is, and `copies > waiting` is what could not have been true
    /// before this change — **the strict inequality is the prove-fail**: against the old
    /// build it read 3 == 3 and this assertion reddens.
    ///
    /// `shellHomeLinks` is the other half the staged dump buys: the landed row offers "Open",
    /// and the dead count stays 0. The dump is staged in the game's own tab-separated shape
    /// through the harness, so it goes through the real finder and the real parser (trap 23).
    /// </summary>
    [Fact]
    public void ReadinessAsksForTheDumpsThatAreMissingAndStillOffersTheCatchUpForTheOneThatLanded()
    {
        using var app = new AppHarness(environment: OpenOn("home"));
        app.WriteInventoryDump(("General1", "Bone Chips", 12));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to land on the Home room");
        // Four dumps reported on: bags, achievements, factions, and the optional spellbook
        // OE-5 added — one row, in `HomeReadout.Readiness`, which is what this count is
        // really asserting: the ONE place both hosts of that list read.
        Assert.Equal(4, app.DumpValue("shellHomeReadiness"));
        // Bags landed; the other three never have. This is also the floor that proves the
        // staged dump was SEEN — without it the numbers below are about a room that never
        // read the file.
        Assert.True(app.DumpValue("shellHomeReadinessWaiting") == 3,
            $"the staged inventory dump was not seen; dump was: {app.Artifacts()}");

        // DRA-63 ask 1: every row carries the catch-up, including the one that has landed.
        Assert.Equal(4, app.DumpValue("shellHomeCopyCmd"));
        Assert.True(app.DumpValue("shellHomeCopyCmd") > app.DumpValue("shellHomeReadinessWaiting"),
            "the ⧉ catch-up is still an empty-state-only affordance — a scanned row lost its "
            + $"button; dump was: {app.Artifacts()}");

        // The one landed row's "Open", and the dead-affordance question asked of it.
        Assert.Equal(1, app.DumpValue("shellHomeLinks"));
        Assert.Equal(0, app.DumpValue("shellHomeDeadLinks"));
    }

    /// <summary>
    /// **The Home/Live boundary, asserted where it is most tempting to cross.** The fixture
    /// log is a live session with kills in it, so Home is drawn in exactly the state Bevel's
    /// §5 warns about: the meters exist, they are moving, and `CurrentSnapshot()` is one
    /// property access away in the room's own file. Home reports the session as in progress
    /// and nothing else about it.
    ///
    /// The unit suite proves `RecentSession` has no combat field to render
    /// (`HomeRoomTests.TheRecentSessionRecordCarriesNoCombatNumbersToRender`); this proves
    /// the running app reaches that state rather than some other one — which is the gap
    /// between "correct in the diff" and "in effect at runtime" that trap 42 cost two
    /// builds.
    /// </summary>
    [Fact]
    public void AliveSessionIsReportedAsInProgressAndNotAsWhereYouLeftOff()
    {
        using var app = new AppHarness(environment: OpenOn("home"));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to land on the Home room");
        // The fixture replays a session with kills, so the widget IS following one.
        Assert.True(app.DumpValue("killsTotal") > 0,
            $"the fixture did not produce a live session; dump was: {app.Artifacts()}");
        app.WaitForDump("shellHomeSession", "inprogress",
            "Home to report the running session as in progress rather than as history");
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

    // ---- E-3 PR 5: the Live room -----------------------------------------------

    /// <summary>
    /// Every one of Live's six rooms, reachable by its own address.
    ///
    /// **`live:raids` is the row that matters most here**, because it is the destination
    /// half of a MOVE: `progress:raids` stopped resolving in the same commit, and a move
    /// where only the departure lands is a surface dropped on the floor between two rooms.
    /// The pair of assertions — this one and
    /// `TheOldRaidsAddressUnderProgressLandsOnNoTabRatherThanTheWrongOne` — is what says the
    /// surface arrived rather than merely left.
    /// </summary>
    [Theory]
    [InlineData("live", "damage")]
    [InlineData("live:damage", "damage")]
    [InlineData("live:healing", "healing")]
    [InlineData("live:pet", "pet")]
    [InlineData("live:timeline", "timeline")]
    // E-3 S3: the History merge's two rooms. `live:pace` is the one that must NOT be
    // reachable as `live:timeline` — the signed §3 refusal, from outside.
    [InlineData("live:pace", "pace")]
    [InlineData("live:encounters", "encounters")]
    [InlineData("live:kills", "kills")]
    [InlineData("live:raids", "raids")]
    // The old names, which have to keep landing: `combat` is what the widget's card and the
    // phone's screen are called, and a script or a habit reaching for it should land
    // somewhere true rather than nowhere.
    [InlineData("live:combat", "damage")]
    [InlineData("live:fight", "timeline")]
    [InlineData("live:pulls", "encounters")]
    [InlineData("live:dpsovertime", "pace")]
    public void EveryLiveRoomIsReachableByItsOwnAddress(string address, string room)
    {
        using var app = new AppHarness(environment: OpenOn(address));
        app.Launch();

        app.WaitForDump("shellLiveTab", room, $"the shell to land on {address}");
        Assert.Equal("live", app.DumpText("shellPage"));
        Assert.Equal(ShellPages.Landed.Count, app.DumpValue("shellRail"));
        Assert.Equal(Enum.GetValues<LiveTab>().Length, app.DumpValue("shellLiveTabs"));
    }

    /// <summary>
    /// **Live is a SECOND host for surfaces the widget still draws, and this is the
    /// comparison that says the two agree.** Nothing was subtracted from the widget by this
    /// PR — that is gated per item on a HUD chip and a screenshot — so `CreatureWindow` and
    /// `ProgressWindow` are open here alongside the room, on purpose, which is the exact
    /// condition trap 45's exemption note calls out. On WPF the symptom of getting it wrong
    /// is not a crash but a surface silently vanishing from whichever host drew it first,
    /// and these row counts are the only thing that can see it.
    ///
    /// The room builds its OWN `KillsCardView` and `RaidsCardView` through
    /// `MainWindow.NewLiveSurfaces()`; a shared instance would be torn out of one of the two
    /// parents, and one of these numbers would go to zero.
    ///
    /// **It takes two launches rather than one, and the reason is trap 56.** Only the
    /// VISIBLE tab paints — a room that painted its idle tabs would put a second MOMENT in a
    /// dump whose whole contract is to describe one — so a single run opened on Kills
    /// reports `shellLiveRaidsRows=0` for a Raids card that is correct and simply has not
    /// been drawn. The first version of this test did exactly that and read as a lost
    /// surface; the honest fix is one launch per comparison, not a room that paints more.
    /// </summary>
    [Fact]
    public void TheLiveRoomAndTheKillsWindowAgreeAboutTheSessionsKills()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "live:kills",
            ["EQBUDDY_CREATURE"] = "kills",
        });
        app.Launch();

        app.WaitForDump("shellLiveTab", "kills", "both hosts to reach the session kills");
        // The fixture replays a real session, so this is a comparison of numbers that are
        // not both zero — which is what keeps it from passing vacuously (trap 39).
        Assert.True(app.DumpValue("killKinds") > 0,
            $"the fixture produced no kills; dump was: {app.Artifacts()}");
        Assert.True(app.DumpValue("shellLiveKillRows") > 0,
            $"the Live room drew no kill rows; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("kills"), app.DumpValue("shellLiveKillRows"));
        Assert.Equal(app.DumpValue("party"), app.DumpValue("shellLivePartyRows"));
    }

    /// <summary>
    /// The Raids half of the same comparison — and the half that also proves the MOVE, since
    /// `progressRaidsRows` is the key the shell's PROGRESS room used to answer and now does
    /// not. The v1 `ProgressWindow` still draws the tab, which is what makes it available to
    /// compare against at all.
    /// </summary>
    [Fact]
    public void TheLiveRoomAndTheProgressWindowAgreeAboutTheRaidLedger()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "live:raids",
            ["EQBUDDY_PROGRESS"] = "raids",
        });
        app.Launch();

        app.WaitForDump("shellLiveTab", "raids", "both hosts to reach the raid ledger");
        Assert.True(app.DumpValue("progressRaidsRows") > 0,
            $"the v1 window drew no raid rows; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("progressRaidsRows"), app.DumpValue("shellLiveRaidsRows"));
        Assert.Equal(app.DumpValue("progressRaidsDefeated"), app.DumpValue("shellLiveRaidsDefeated"));
        // And the key it left: the shell's Progress room does not report raid rows any more,
        // which is the assertion that says the departure happened rather than being assumed
        // from the arrival. `DumpText` (not `DumpValue`) because an absent key is "" rather
        // than 0, and 0 is what a room that still drew an empty ledger would report.
        Assert.Equal("", app.DumpText("shellProgressRaidsRows"));
    }

    /// <summary>
    /// **The leak check Bevel asked for by name, asserted rather than promised.**
    ///
    /// Live is the room most likely to want its own redraw cadence — it is the one whose
    /// content genuinely changes every second, and `FightTimelineWindow`, one of its five
    /// sources, owns exactly such a `DispatcherTimer`. It starts none: the shell already
    /// ticks the visible room once a second, so the room takes that instead. `Release()` is
    /// therefore empty, and this is what stops that being a claim nobody can check — a
    /// leaked timer shows in nothing else, not a diff, not a build, not a screenshot.
    ///
    /// Opened on the Timeline tab specifically, because that is the tab where a timer would
    /// have gone in.
    /// </summary>
    [Fact]
    public void TheLiveRoomStartsNoTickOfItsOwn()
    {
        using var app = new AppHarness(environment: OpenOn("live:timeline"));
        app.Launch();

        app.WaitForDump("shellLiveTab", "timeline", "the shell to land on the fight timeline");
        Assert.Equal(0, app.DumpValue("shellLiveTimers"));
        // And it is still painting, which is what makes the line above mean "it takes the
        // shell's tick" rather than "it does nothing".
        Assert.True(app.DumpValue("tick") > 0,
            $"the widget stopped ticking; dump was: {app.Artifacts()}");
    }

    /// <summary>
    /// **The Home/Live boundary from LIVE's side.** Home reports the running session and
    /// refuses its numbers; Live reports the same session and draws them. Both read one
    /// `SessionSummary.Pick`, so the state must MATCH while the content differs — a
    /// disagreement here is the drift the sibling record exists to prevent, and it is
    /// invisible from either room alone.
    /// </summary>
    [Fact]
    public void HomeAndLiveDescribeTheSameSittingAndOnlyLiveCountsIt()
    {
        using var app = new AppHarness(environment: OpenOn("live"));
        app.Launch();

        app.WaitForDump("shellLiveSession", "inprogress",
            "Live to report the running session as in progress");
        Assert.True(app.DumpValue("killsTotal") > 0,
            $"the fixture did not produce a live session; dump was: {app.Artifacts()}");
        // Live counts the kills Home is not allowed to. Read from the session record rather
        // than from the snapshot, so this is the boundary being crossed on purpose and not
        // a second path to the same number.
        Assert.Equal(app.DumpValue("killsTotal"), app.DumpValue("shellLiveKills"));
        // And the room is not showing its whole-room empty over a session that has fights
        // in it — the state that would make every assertion above pass while the player saw
        // "nothing has happened yet".
        Assert.Equal(0, app.DumpValue("shellLiveEmpty"));
    }

    // ---- E-3 S3: HistoryWindow's this-session half -------------------------------

    /// <summary>
    /// **THE SIGNED DATA-SOURCE RULE, asserted from outside the app.**
    ///
    /// Bevel's History pre-design §2 is that the studio's version of these two surfaces is
    /// up to five minutes stale by construction: the archiver checkpoints every five minutes
    /// and `HistoryViewModel` loads the row's snapshot ONCE, on selection. Helm's item (2)
    /// signs the fix — the Live room builds them from `CurrentSnapshot()` and never touches
    /// the checkpoint.
    ///
    /// **Nothing in a diff, a build or a screenshot can tell those two apart**: a graph off a
    /// five-minute-old snapshot renders perfectly. `shellLiveHistorySource` is a literal the
    /// room has to edit to keep true, and this is the assertion that makes editing it
    /// visible — the same device `shellLiveTimers=0` already uses.
    ///
    /// The pull COUNT beside the ROW count is the second half: one is what the grouping
    /// found, the other is what the list drew, and a list that silently stopped taking rows
    /// would otherwise look like a short session (#234's shape).
    /// </summary>
    [Fact]
    public void TheLiveEncountersTabReadsTheSnapshotAndDrawsEveryPull()
    {
        using var app = new AppHarness(environment: OpenOn("live:encounters"));
        app.Launch();

        app.WaitForDump("shellLiveTab", "encounters", "the shell to land on the pull list");
        Assert.Equal("snapshot", app.DumpText("shellLiveHistorySource"));
        // The fixture replays a real session, so this is not a comparison of two zeros —
        // which is what keeps it from passing vacuously (trap 39).
        Assert.True(app.DumpValue("shellLivePulls") > 0,
            $"the fixture produced no finished pulls; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("shellLivePulls"), app.DumpValue("shellLivePullRows"));
        // And the room is not showing its whole-room empty over a session with fights in it.
        Assert.Equal(0, app.DumpValue("shellLiveEmpty"));
    }

    /// <summary>
    /// **The session graph draws, and it is NOT the Timeline tab.**
    ///
    /// Two claims, and the second is the one the sign is about: `live:pace` and
    /// `live:timeline` are different rooms with different content, so an address that
    /// resolved one to the other would be the collision Bevel §3 refused, and it would look
    /// entirely healthy — a graph is a graph in a screenshot.
    ///
    /// `shellLivePacePoints` is a real state at zero (a sitting under two minutes long
    /// cannot be plotted), so it is asserted with the fixture's own session behind it rather
    /// than asserted away.
    /// </summary>
    [Fact]
    public void ThePaceTabDrawsTheSessionGraphAndIsNotTheTimeline()
    {
        using var app = new AppHarness(environment: OpenOn("live:pace"));
        app.Launch();

        app.WaitForDump("shellLiveTab", "pace", "the shell to land on the session graph");
        Assert.NotEqual(LiveSurface.KeyFor(LiveTab.Timeline), app.DumpText("shellLiveTab"));
        Assert.True(app.DumpValue("shellLivePacePoints") > 1,
            $"the session graph plotted nothing; dump was: {app.Artifacts()}");
        // The timeline's own lane count is NOT what this tab reports — two surfaces, two
        // numbers, and reading one where the other was meant is exactly trap 58's failure.
        Assert.Equal(0, app.DumpValue("shellLiveTimers"));
    }

    /// <summary>
    /// **The career half: the Progress room's History tab, and the row it must not offer.**
    ///
    /// The archiver checkpoints the RUNNING sitting into the store, so it is in
    /// `StoredSessions()` — and the browse excludes it, because the picture behind it is up
    /// to five minutes old while the live copy is one room away. `careerRows` is the count
    /// after that filter; `killsTotal` proves there IS a running session to have excluded,
    /// which is what stops this passing on a profile with nothing in it.
    ///
    /// **`shellProgressCareerSinglePane` is asserted as a RELATIONSHIP, never a number** —
    /// a hosted runner is 1024×768 and a test that expected two panes would be asserting the
    /// desk it was written on. The input (`shellWidth`) and the answer come from the same
    /// `ShellLayoutPolicy` the window used.
    /// </summary>
    [Fact]
    public void TheProgressCareerTabBrowsesStoredSittingsAndNotTheRunningOne()
    {
        using var app = new AppHarness(environment: OpenOn("progress:history"));
        app.Launch();

        app.WaitForDump("shellProgressTab", "history", "the shell to land on the career browse");
        Assert.Equal("progress", app.DumpText("shellPage"));
        // There IS a live session — so an empty browse here would mean the filter took
        // everything rather than that the profile is fresh.
        Assert.True(app.DumpValue("killsTotal") > 0,
            $"the fixture produced no live session; dump was: {app.Artifacts()}");
        // The running sitting is checkpointed into the store and must not be listed. The
        // fixture's profile has no ENDED sittings, so the honest expectation is zero rows —
        // which is also the empty state, and the room-level empty must NOT have fired over
        // it (Progress has a character and three other tabs full of numbers).
        Assert.Equal(0, app.DumpValue("shellProgressCareerRows"));
        Assert.Equal(0, app.DumpValue("shellProgressEmpty"));
        Assert.Equal(0, app.DumpValue("shellProgressCareerSelected"));

        var width = app.DumpValue("shellWidth");
        Assert.Equal(
            ShellLayoutPolicy.For(width).RoomSinglePane ? 1 : 0,
            app.DumpValue("shellProgressCareerSinglePane"));
    }

    /// <summary>
    /// **`HistoryWindow` is NOT retired by this PR, and its door still works** — Helm's item
    /// (5), soft lean, asserted rather than assumed. The four studio jobs the career tab does
    /// not carry (compare, notes, export, delete) live behind that one context-menu entry, so
    /// a PR that quietly broke it would take them with it and nothing else would say so.
    ///
    /// Both hosts are open at once on purpose: that is the two-hosts condition trap 45 is
    /// about, and on WPF the symptom of getting it wrong is a surface vanishing from
    /// whichever one drew it first rather than an exception.
    /// </summary>
    [Fact]
    public void TheHistoryStudioStillOpensBesideTheCareerTab()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "progress:history",
            ["EQBUDDY_HISTORY"] = "1",
        });
        app.Launch();

        app.WaitForDump("shellProgressTab", "history", "both hosts to reach session history");
        app.WaitForDump("historySessions", 1, "the studio window to open and list the sitting");
        // Its detail pane is populated — the hook selects the newest row, so a studio that
        // opened and held nothing would be trap 22's "reviewed anyway" state.
        Assert.Equal(1, app.DumpValue("historyDetail"));
        // The room is still painting beside it — the half that would go silently wrong.
        Assert.Equal(0, app.DumpValue("shellProgressEmpty"));

        // **AND THIS IS THE EXCLUSION, PROVEN RATHER THAN ASSERTED AGAINST A ZERO.** Two
        // hosts, one store, one running sitting: the studio lists it (it always has — the
        // archiver checkpoints it under `ActiveEndReason`, and the studio will happily show
        // you a picture up to five minutes old) and the career browse does not. A career tab
        // that had simply failed to render would report 0 too, which is why the pair is the
        // assertion and neither number alone is.
        Assert.True(app.DumpValue("historySessions") > 0,
            $"the store held no sitting to exclude; dump was: {app.Artifacts()}");
        Assert.Equal(0, app.DumpValue("shellProgressCareerRows"));
    }

    // ---- E-3 S1: the room-level empty-state wrapper ------------------------------

    /// <summary>
    /// **The two halves of the room-level empty that only a launched app can see, asserted
    /// together on every one of the six rooms.**
    ///
    /// `shellRoomFills` is the PRECONDITION. `RoomEmptyState` centres with
    /// `VerticalAlignment.Center`, which centres inside the slack a parent gives you — so
    /// the whole wrapper rests on the room being handed the shell's whole content cell. It
    /// is today. **This is the guard against the day it stops**, and it is asked against the
    /// CELL rather than against the room's own `ContentControl`: the host shrinks onto its
    /// content, so a room-vs-host comparison agrees at 100×600 as contentedly as at 800×600
    /// and answers 1 forever — a guard that cannot fail, which reads as coverage and is not
    /// (trap 34). Measured before this was written: with `HorizontalAlignment.Left` on the
    /// host the room-vs-host form still says 1 and this one says 0. It is a relationship,
    /// never a number, so it holds on a 1024×768 hosted runner as well as on a desk.
    ///
    /// `shell*Empty=0` is the GUARD. A room-level empty COLLAPSES the tab strip and the
    /// body, so a predicate that fired while the room had content would not be a cosmetic
    /// slip — it would take away the tabs, the Sky tab's two ⧉ commands, Gear's ⧉ copy of
    /// `/outputfile inventory` and World's "Drop camp marker" button.
    /// `ShellRoomEmptyTests` proves each predicate's clauses in isolation; this is the
    /// populated profile they must all say NO to, which is the half a unit test cannot stand
    /// in for.
    ///
    /// **What is deliberately NOT here: the empty state itself.** The harness seeds a
    /// character, so the state these predicates fire in cannot be reached from this suite at
    /// all — the gap #303's ask 2 already named and Fable's I-15 already carries as an
    /// empty-profile harness. Asserting the negative against a real app is what there is.
    /// </summary>
    [Theory]
    [InlineData("home", "shellPage", "home", "shellHomeEmpty")]
    [InlineData("live", "shellLiveTab", "damage", "shellLiveEmpty")]
    [InlineData("progress", "shellProgressTab", "progress", "shellProgressEmpty")]
    [InlineData("gear", "shellGearTab", "loot", "shellGearEmpty")]
    [InlineData("world", "shellWorldTab", "misc", "shellWorldEmpty")]
    [InlineData("quests", "shellQuestsTab", "general", "shellQuestsEmpty")]
    public void EveryRoomFillsItsCellAndNoneOfThemHidesItselfOverApopulatedProfile(
        string address, string key, string room, string emptyKey)
    {
        using var app = new AppHarness(environment: OpenOn(address));
        app.Launch();

        app.WaitForDump(key, room, $"the shell to land on {address}");
        app.WaitForDump("shellRoomFills", "1",
            $"the {address} room to fill the cell the host gave it");
        Assert.Equal(0, app.DumpValue(emptyKey));
    }

    // ---- SR-5: the Settings room --------------------------------------------------

    /// <summary>
    /// **The seventh room, and the only one with NO whole-room empty — which is why it gets
    /// its own row rather than joining the theory above.**
    ///
    /// Every other room can be about nothing (no character, no session, no bags) and
    /// collapses to an explanation; Settings configures the tool rather than the character,
    /// so every control on it is meaningful on a profile that has never seen a log line. A
    /// `shell*Empty` key would have nothing to report, and asserting a key that does not
    /// exist is `Assert.Equal(0, 0)` wearing a room's name (trap 39).
    ///
    /// `shellRoomFills` still applies and is the half worth keeping: it is asked against the
    /// shell's CELL rather than the room's own host, so it is a relationship rather than a
    /// number and holds on a 1024×768 runner as readily as on a desk.
    /// </summary>
    [Fact]
    public void TheSettingsRoomFillsItsCellAndDrawsAllFourTabsWithTheAlertFamiliesUnderThem()
    {
        using var app = new AppHarness(environment: OpenOn("settings"));
        app.Launch();

        app.WaitForDump("shellSettingsTab", "look", "the shell to land on the Settings room");
        app.WaitForDump("shellRoomFills", "1",
            "the Settings room to fill the cell the host gave it");
        Assert.Equal(ShellPages.Landed.Count, app.DumpValue("shellRail"));
        Assert.Equal(SettingsSurface.Tabs().Count, app.DumpValue("shellSettingsTabs"));
        // **The sub-strip is BUILT from a tab that is not showing it**, which is the
        // assertion that says it exists rather than that Alerts happened to be selected. A
        // strip built lazily would report 0 here and 4 one address later, and only the
        // second number would ever be looked at.
        Assert.Equal(AlertSurface.Tabs().Count, app.DumpValue("shellSettingsFamilies"));
        // ONE room built, not seven — the lazy dictionary's own claim, and it matters most
        // for this room: it is the most expensive to construct (four blocks, the whole of
        // what opening Options costs).
        Assert.Equal(1, app.DumpValue("shellRooms"));
    }

    /// <summary>
    /// **The two-level address, walked**: `settings:crowd` is an `AlertSurface` key, not a
    /// `SettingsSurface` one, and it has to land on the Alerts tab AND on the Crowd family.
    ///
    /// That reuse — rather than a third address level (`settings:alerts:crowd`) — is only
    /// sound while the two key tables are disjoint, which `ShellNavigationTests` asserts
    /// without an app. What only a launched app can say is that the ROOM did it: "the tables
    /// do not collide" and "the fallthrough is wired" are different claims, and the gap
    /// between them is trap 42, which cost two builds to learn once already.
    ///
    /// The failure it catches photographs perfectly — a room on the right tab showing the
    /// wrong family looks exactly like a room on the right tab.
    /// </summary>
    [Theory]
    [InlineData("settings:crowd", "crowd")]
    [InlineData("settings:buffs", "buffs")]
    [InlineData("settings:spawns", "spawns")]
    // `tracked` is the Watch family's key and is NOT a typo: it is the settings key the
    // Watch card has always used, kept so the fold that made it a tab needed no migration.
    [InlineData("settings:tracked", "tracked")]
    [InlineData("settings:watch", "tracked")]
    public void AnAlertFamilyKeyLandsOnTheAlertsTabAndInsideIt(string address, string family)
    {
        using var app = new AppHarness(environment: OpenOn(address));
        app.Launch();

        app.WaitForDump("shellSettingsFamily", family, $"the shell to land on {address}");
        // The page half AND the tab half: an address that set the family without moving to
        // Alerts would leave the player looking at Look.
        Assert.Equal("settings", app.DumpText("shellPage"));
        Assert.Equal("alerts", app.DumpText("shellSettingsTab"));
    }

    /// <summary>
    /// **Two hosts of one settings surface must report the same numbers — and this pair is
    /// the reason the SR series lifted blocks instead of building a room beside a live
    /// window.**
    ///
    /// `OptionsWindow` and the Settings room compose the SAME four blocks. Every key below is
    /// the SAME string those blocks hand both hosts, re-keyed mechanically
    /// (`ShellDumpFacts.Prefixed`) under `options*` and `shellSettings*` — so this comparison
    /// could not have been written at all if either side reported hand-written copies of the
    /// numbers (trap 58, and trap 33 one level up). The alternative the plan rejected — two
    /// copies of roughly forty control wirings — would render perfectly on both hosts and
    /// drift from the first commit that touched one of them, which is #210's mechanism with a
    /// bigger surface.
    ///
    /// **Both are open at once, on purpose.** That is trap 45's condition: a WPF `UIElement`
    /// has exactly one parent, so a block shared between the two would be torn out of
    /// whichever painted it last — silently, with no exception to point at — and one side of
    /// each equality below would go to zero.
    ///
    /// A floor before the equalities, per trap 39: two hosts that both built NOTHING would
    /// agree perfectly and prove nothing, and zero is precisely what a lost block reports.
    /// </summary>
    [Fact]
    public void TheShellAndTheOptionsWindowAgreeAboutTheSameSettings()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "settings:hud",
            ["EQBUDDY_OPTIONS"] = "1",
        });
        app.Launch();

        app.WaitForDump("shellSettingsTab", "hud", "the shell to land on the HUD tab");
        app.WaitForDumpAtLeast("optionsHudPanels", 1,
            "the v1 Options window to build its own copy of the same block");

        // The HUD block: the panel list, the "no longer on the widget" companion, the mini
        // dashboard and the floating-window list.
        Assert.Equal(app.DumpValue("optionsHudPanels"), app.DumpValue("shellSettingsHudPanels"));
        Assert.Equal(app.DumpValue("optionsHudStats"), app.DumpValue("shellSettingsHudStats"));
        Assert.Equal(app.DumpValue("optionsHudWindows"), app.DumpValue("shellSettingsHudWindows"));
        // **`hudRetired` is the row a screenshot could never supply.** #335's "no longer on
        // the widget" list is the ONLY thing on either host naming the six surfaces that left
        // the HUD, and an absent panel photographs as an unremarkable list (trap 29/34). It
        // is counted as DRAWN on both sides, not read off a static, so two hosts that had
        // both failed to render it would disagree with the floor rather than with each other.
        Assert.True(app.DumpValue("shellSettingsHudRetired") >= 1,
            $"the room drew no retired rows; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("optionsHudRetired"),
            app.DumpValue("shellSettingsHudRetired"));

        // **`hudHints` is the same row for the prose pass (2026-09-08).** Five explanations on
        // this screen now exist ONLY behind an ⓘ — the panel list's, the mini dashboard's, the
        // floating-window list's, and the two under the double-click and target-drops switches
        // — so an ⓘ that failed to build on one host is a paragraph a player can no longer
        // reach at all, and it photographs as an unremarkable panel (traps 29/34). Counted off
        // BUILT buttons on both sides, with a floor before the equality: two hosts that had
        // both built none would agree perfectly and prove nothing (trap 39).
        Assert.True(app.DumpValue("shellSettingsHudHints") >= 5,
            $"the room built fewer than the five ⓘ this block hangs; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("optionsHudHints"),
            app.DumpValue("shellSettingsHudHints"));

        // The Look block.
        Assert.True(app.DumpValue("shellSettingsLookPalettes") >= 1,
            $"the room's palette picker is empty; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("optionsLookPalettes"),
            app.DumpValue("shellSettingsLookPalettes"));
        Assert.Equal(app.DumpValue("optionsLookSwatches"),
            app.DumpValue("shellSettingsLookSwatches"));
        // `lookHints` — the prose pass reached this tab in Pass 2 (2026-09-08). Exactly one
        // paragraph moved (the grid overlay's), and one is enough for the row to be worth
        // having: an ⓘ that failed to build on one host is that explanation gone from the
        // product on that host, with nothing on screen to say so. Floor first, then equality,
        // because two hosts that had both built none would agree perfectly (trap 39).
        Assert.True(app.DumpValue("shellSettingsLookHints") >= 1,
            $"the room built no ⓘ on Look; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("optionsLookHints"),
            app.DumpValue("shellSettingsLookHints"));

        // The Behavior block — and `behaviorHotkeys` is the one with a wiring behind it: the
        // hotkey rows are the only piece of this surface a HOST has to help with (the key
        // route), so a host that composed the block and forgot the route would have rows on
        // screen that silently never record.
        Assert.True(app.DumpValue("shellSettingsBehaviorHotkeys") >= 1,
            $"the room drew no hotkey rows; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("optionsBehaviorHotkeys"),
            app.DumpValue("shellSettingsBehaviorHotkeys"));

        // **`behaviorHints` is the prose pass's row here, and it is deliberately NOT an
        // equality** — this is the one block whose two hosts legitimately hold a different
        // number of ⓘ. Eight explanations moved onto a hover in Pass 2 and one of them is
        // Setup's, which only the SHELL draws (`OptionsWindow` has nowhere to put the
        // screen). So the honest statement is that the difference is EXACTLY the Setup row:
        // written as an equality against a plain 8 it would have failed on the window and
        // been "fixed" by dropping the assertion, and written as a bare >= it could not see
        // seven ⓘ silently becoming three.
        Assert.True(app.DumpValue("shellSettingsBehaviorHints") >= 8,
            $"the room built fewer than the eight ⓘ this block hangs; dump was: {app.Artifacts()}");
        Assert.Equal(
            app.DumpValue("shellSettingsBehaviorHints") - app.DumpValue("optionsBehaviorHints"),
            app.DumpValue("shellSettingsBehaviorSetup") - app.DumpValue("optionsBehaviorSetup"));

        // The Alerts block, all four families built on both hosts — the window stacks them
        // across two tabs and the room pages them behind one sub-strip, and they agree on
        // the COUNT because both compose the whole surface up front.
        Assert.Equal(AlertSurface.Tabs().Count, app.DumpValue("shellSettingsAlertsBlocks"));
        Assert.Equal(app.DumpValue("optionsAlertsBlocks"),
            app.DumpValue("shellSettingsAlertsBlocks"));
        Assert.Equal(app.DumpValue("optionsAlertsRuleRows"),
            app.DumpValue("shellSettingsAlertsRuleRows"));
        Assert.Equal(app.DumpValue("optionsAlertsRules"),
            app.DumpValue("shellSettingsAlertsRules"));
        // `alertsHints` — three explanations here exist ONLY behind an ⓘ since Pass 2 (two in
        // the shared header, one on the Buffs block). The equality is only meaningful because
        // `alertsBlocks` above has already said both hosts composed the whole surface.
        Assert.True(app.DumpValue("shellSettingsAlertsHints") >= 3,
            $"the room built fewer than the three ⓘ this view hangs; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("optionsAlertsHints"),
            app.DumpValue("shellSettingsAlertsHints"));
    }

    /// <summary>
    /// **THE DOOR EXISTS ON A PROFILE NOBODY HAS CONFIGURED — trap 59's floor, asserted.**
    ///
    /// The widget's <c>Guide…</c> row is the shell's only player entrance (OE-2; the row was
    /// <c>Open EQBuddy…</c> until the 2026-09-08 faces folded it and <c>Quests…</c> into
    /// one), and the two ways it could stop being one are both invisible: a control that is
    /// not drawn photographs as an unremarkable menu (trap 29, six days of an invisible
    /// Mobile button), and `IsEnabled=false` renders exactly like a live row under this
    /// app's styles (trap 17). `menuGuide` is 1 only when the row is present, visible AND
    /// enabled.
    ///
    /// **It is asserted on the launch with NO shell**, which is the state a stranded player
    /// is actually in: they closed it, and the row has to be there when nothing else is.
    /// </summary>
    [Fact]
    public void TheWidgetMenuCarriesTheGuideDoorWithNoShellOpen()
    {
        using var app = new AppHarness(
            environment: new Dictionary<string, string> { ["EQBUDDY_SHELL"] = "" });
        app.Launch();

        app.WaitForDump("menuGuide", 1,
            "the widget's Guide row to be present, visible and enabled");
        Assert.Equal("", app.DumpText("shellPage"));
    }

    /// <summary>
    /// **THE ≤4 LOCK, IN EFFECT RATHER THAN DECLARED** (Bevel's cog/Options IA faces §B,
    /// Helm-signed 2026-09-08).
    ///
    /// `WidgetMenuTests` reads the XAML and proves the four rows are the four that are not
    /// tagged expanded-only. That is a claim about the FILE. This is the other half and the
    /// one trap 42 exists for: the tag has to be ACTED on, by `MainWindow.ApplyMenuMode`,
    /// against the widget's real mode — a declaration nothing applies renders eleven rows
    /// over the game and passes every unit test in the suite.
    ///
    /// **Two launches, and the two counts now agree exactly** — the owner's later lock
    /// (`docs/BEVEL-gear-menu-slim-faces.md` Part A, Helm-signed, PR #460) cut click-through,
    /// Edit HUD, the data chores and Help OFF the expanded menu too, superseding the earlier
    /// call (§E) that kept them there. Both states show precisely the four doors now, so a
    /// stray fifth row on either one — a leftover left un-tagged, or a row someone re-adds
    /// without a destination — fails here on the surface that would actually show it, rather
    /// than only on `WidgetMenuTests`' read of the file.
    /// </summary>
    [Fact]
    public void TheMinimizedAndExpandedWidgetMenusBothShowExactlyTheFourDoors()
    {
        using (var mini = new AppHarness(settings => settings.Minimized = true))
        {
            mini.Launch();
            mini.WaitForDump("menuRows", WidgetMenuPolicy.MiniRows.Count,
                "the minimized widget's menu to be cut to the four doors");
            // The door among them is the one with a job beyond opening a window, so it is
            // asserted by name as well as by count.
            Assert.Equal(1, mini.DumpValue("menuGuide"));
        }

        using var full = new AppHarness(settings => settings.Minimized = false);
        full.Launch();
        full.WaitForDump("menuGuide", 1, "the expanded widget's menu to be built");
        Assert.Equal(WidgetMenuPolicy.MiniRows.Count, full.DumpValue("menuRows"));
    }


    /// <summary>
    /// **THE RECOVERY, END TO END: open → ✕ → stranded → the row → back.**
    ///
    /// This is the bug OE-2 exists for, walked rather than reasoned about. The shell has
    /// native chrome, so ✕ is an ordinary WM_CLOSE; before this row landed,
    /// <c>EQBUDDY_SHELL</c> was the only entrance and a player who closed the window had no
    /// way back short of restarting EQBuddy — a window you can close and cannot reopen,
    /// which is "silent no-ops are broken" with the switch on the other side.
    ///
    /// **The close is real and the middle state is ASSERTED, not assumed.** A test that
    /// opened the shell from a launch where none existed would prove the same thing about a
    /// state it reached by a different road, and "the ✕ leaves the same state as never
    /// opening it" is a reading of the code — which is the kind of step trap 49 spent
    /// thirteen green tests on. `shellPage` going empty is the app saying it is stranded.
    ///
    /// **It comes back on GUIDE, and that is the 2026-09-08 reversal**: the door used to
    /// pass no address, so the window's constructor decided and it landed on Home. The row
    /// is called `Guide…` now, and a row named for a room that lands somewhere else is an
    /// affordance lying about itself. The recovery — the whole of OE-2 — is unchanged.
    /// </summary>
    [Fact]
    public void TheGuideDoorBringsBackAShellTheCloseButtonTook()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "1",
            ["EQBUDDY_DOORPROBE"] = "1",
        });
        app.Launch();
        app.WaitForDump("shellPage", "home", "the shell to open on its default room");

        app.CloseShellWindow(ShellPages.Label(ShellPage.Home));
        app.WaitForDump("shellPage", "",
            "the ✕ to take the shell away — the state this door exists to recover from");

        app.ClickGuideDoor();
        app.WaitForDump("shellPage", ShellPages.Key(ShellPage.Quests),
            "the Guide row to bring the shell back, on the room it is named for");
        // Back as a WORKING window, not merely a window: the rail is whole and the room
        // painted. A shell that reopened blank would satisfy `shellPage` alone. The Home
        // block count went with the landing room — it is Home's fact, and this door no
        // longer lands there.
        Assert.Equal(ShellPages.Landed.Count, app.DumpValue("shellRail"));
    }

    /// <summary>
    /// **THE OTHER "GONE": a MINIMIZED shell, which `Activate` does not undo.**
    ///
    /// The ✕ is the reported bug and it is not the only way the window leaves the screen.
    /// A door built as "open it if it is closed, otherwise front it" is a visible no-op for
    /// the player who minimized it instead — the same defect class as the bug, reached from
    /// a state that already has a taskbar button, which is exactly why it would never be
    /// reported as one.
    ///
    /// `shellMinimized` is a STATE, not a size, so nothing here asserts the desk this was
    /// written on — and it is asserted at 1 first, because a test that only checked it was
    /// 0 at the end would pass against a window that was never minimized at all.
    /// </summary>
    [Fact]
    public void TheGuideDoorRestoresAShellTheMinimiseButtonTook()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "1",
            ["EQBUDDY_DOORPROBE"] = "1",
        });
        app.Launch();
        app.WaitForDump("shellPage", "home", "the shell to open on its default room");

        app.MinimizeShellWindow(ShellPages.Label(ShellPage.Home));
        app.WaitForDump("shellMinimized", 1, "the shell to go to the taskbar");

        app.ClickGuideDoor();
        app.WaitForDump("shellMinimized", 0, "the Guide row to bring it back up");
        // Still the SAME window — restored, not rebuilt. It has moved to Guide, which is
        // what the row is named for; what this test is about is the raise, and `Activate`
        // is what does not do it.
        Assert.Equal(ShellPages.Key(ShellPage.Quests), app.DumpText("shellPage"));
    }

    /// <summary>
    /// **The door TAKES an open shell to Guide, and that is the deliberate reversal.**
    ///
    /// This test used to assert the opposite, and the reason it did was sound for the row it
    /// was written about: <c>Open EQBuddy…</c> meant "open the app", so snapping a player
    /// out of the room they were reading would have been a second defect wearing the fix's
    /// clothes. The owner's 2026-09-08 amendment cut that row. What is left is
    /// <c>Guide…</c> — a row that names a destination — and the same argument now runs the
    /// other way: a named door that lands somewhere else is trap 35's shape in a menu.
    ///
    /// The "front it without moving" behaviour was not replaced by another row, and that is
    /// on purpose (faces §E cuts every parallel recovery label). The shell has native
    /// chrome, so its taskbar button is what fronts it — which is the product point the ✕
    /// that made OE-2 a must-fix comes from.
    ///
    /// **The moment the assertion is made at is still the whole question** (trap 62):
    /// `ClickGuideDoor` returns on `doorProbeClicks`, which the probe raises AFTER the
    /// handler has run, so this is asked of an app that has already been through the door.
    /// </summary>
    [Fact]
    public void TheGuideDoorTakesAnOpenShellToGuideFromWhereverItWas()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "progress:faction",
            ["EQBUDDY_DOORPROBE"] = "1",
        });
        app.Launch();
        app.WaitForDump("shellProgressTab", "faction", "the shell to open on Progress → Faction");
        // ONE room built so far, and this is the before-half of the discriminator below.
        Assert.Equal(1, app.DumpValue("shellRooms"));

        app.ClickGuideDoor();

        app.WaitForDump("shellPage", ShellPages.Key(ShellPage.Quests),
            "the Guide row to take the open shell to the room it names");

        // **NAVIGATED, not rebuilt — and `shellRooms` is what separates those two readings.**
        // The rooms a shell has built are its own, so two of them says the window that
        // answered is the one that already had Progress in it. A door that skipped the
        // `IsLoaded` check and constructed a SECOND shell would report ONE: a fresh window
        // builds only the room it was addressed to.
        //
        // The first attempt at this assertion used `shellProgressTab`, on the reasoning that
        // a rebuilt shell would have no remembered tab. It does not survive contact: the tab
        // fact is reported by the Progress ROOM, so it goes empty the moment the shell
        // navigates away from it, and the test failed against a perfectly healthy build.
        // Left written down because the mistake is the useful part — a dump key can stop
        // being reported for a reason that has nothing to do with the claim being made.
        Assert.Equal(2, app.DumpValue("shellRooms"));
    }

    /// <summary>
    /// **`OptionsWindow` is not retired by this PR, and its door still works** — the signed
    /// out-list, asserted rather than assumed, and the same row the History studio already
    /// carries beside the Progress room's career tab. I-9's standing rule is that landing a
    /// room is separate from, and earlier than, retiring the surface it replaces; a PR that
    /// quietly broke the v1 window would take every player's only real settings screen with
    /// it — and the shell's own door is one context-menu row old (OE-2).
    /// </summary>
    [Fact]
    public void TheV1OptionsWindowStillOpensBesideTheSettingsRoom()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "settings:behavior",
            ["EQBUDDY_OPTIONS"] = "1",
        });
        app.Launch();

        app.WaitForDump("shellSettingsTab", "behavior", "the room to land on Behavior");
        // The window opened and is on its own saved tab, which the room neither reads nor
        // writes: the v1 keys are look/alerts/watch/cards/behavior and the room's are
        // look/alerts/hud/behavior, so a room that persisted "hud" would send the WINDOW home
        // to Look on its next open — one host silently editing the other's landing.
        app.WaitForDump("optionsTab", "look", "the v1 window on its own untouched saved tab");
    }

    // ---- OE-6: the first-run Setup screen ---------------------------------------

    /// <summary>
    /// **The auto-launch, on the profile it exists for.** The harness seeds a character and
    /// no dumps, so every readiness row is never-scanned — the state Bevel's predicate names
    /// — and `SetupDismissed` is put back to the default a fresh install has.
    ///
    /// **Three assertions and none of them is redundant.** `shellSetup` says a screen is on
    /// screen; `shellSetupAuto` says the PREDICATE is what opened it, which is the only
    /// thing that separates an auto-launch from the `EQBUDDY_SETUP` hook below; and
    /// `shellSetupCopyCmd` says the ⧉ buttons are actually there, which is the half no
    /// source scan can reach — an absent control photographs as an unremarkable panel (trap
    /// 29) and `GameCommandsTests` can only prove the file NAMES the commands.
    /// `shellSetupRows` beside the count is the floor that stops it going vacuous: a screen
    /// that drew nothing would report zero buttons and satisfy a "greater than zero" reading
    /// of nothing at all (trap 39).
    /// </summary>
    [Fact]
    public void SetupOpensByItselfOnAProfileThatHasRunNoneOfTheCommands()
    {
        using var app = new AppHarness(
            configureSettings: s => s.SetupDismissed = false, environment: OpenOn("1"));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to open");
        Assert.Equal(1, app.DumpValue("shellSetup"));
        Assert.Equal(1, app.DumpValue("shellSetupAuto"));
        // FOUR since OE-5 added the optional spellbook row, and the pair of numbers is the
        // point: Setup renders whatever `HomeReadout.Readiness` returns, so these two moved
        // together with `shellHomeReadiness` above and no code in `SetupView` was touched.
        // A hand-rolled second list would have left them at three (trap 33, two hosts).
        Assert.Equal(4, app.DumpValue("shellSetupRows"));
        Assert.Equal(4, app.DumpValue("shellSetupCopyCmd"));
        // The room underneath is still the room: Setup is a layer over it, not a navigation.
        app.WaitForDump("shellPage", "home", "the room under the screen to be untouched");
    }

    /// <summary>
    /// **One dump is enough to stop it**, which is the "every row, not any row" half of the
    /// predicate reaching a running app. The inventory dump is staged in the game's own
    /// tab-separated shape so it goes through the real finder and the real parser (trap 23).
    ///
    /// **The negative is asserted at a moment on the far side of the decision** (trap 62):
    /// `shellSetup` only exists in the dump once the shell window has been constructed, and
    /// the auto-show decision is made by that constructor — so a `shellPage` that has
    /// arrived is proof the question has already been asked and answered. A `Thread.Sleep`
    /// here would have been a guess about the machine.
    ///
    /// `shellHomeReadinessWaiting` is the floor: it proves the staged dump was actually SEEN
    /// rather than that the app failed to start, which is the reading a bare "no screen"
    /// assertion could never tell apart.
    /// </summary>
    [Fact]
    public void SetupStaysAwayOnceAnyOneOfTheDumpsHasLanded()
    {
        using var app = new AppHarness(
            configureSettings: s => s.SetupDismissed = false, environment: OpenOn("1"));
        app.WriteInventoryDump(("General1", "Bone Chips", 12));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to open");
        // Three of four still waiting, since OE-5's optional spellbook row joined the list.
        Assert.True(app.DumpValue("shellHomeReadinessWaiting") == 3,
            $"the staged inventory dump was not seen; dump was: {app.Artifacts()}");
        Assert.Equal(0, app.DumpValue("shellSetupAuto"));
        Assert.Equal(0, app.DumpValue("shellSetup"));
    }

    /// <summary>
    /// **Dismissed means dismissed** — the persisted answer, read by the same predicate on
    /// the next launch. Same trap-62 timing as above, and the floor here is
    /// `shellSetupDismissed`: without it, an app that had failed to load the profile at all
    /// would report no screen and pass.
    /// </summary>
    [Fact]
    public void SetupDoesNotComeBackAfterItHasBeenClosed()
    {
        using var app = new AppHarness(
            configureSettings: s => s.SetupDismissed = true, environment: OpenOn("1"));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to open");
        Assert.Equal(1, app.DumpValue("shellSetupDismissed"));
        Assert.Equal(0, app.DumpValue("shellSetupAuto"));
        Assert.Equal(0, app.DumpValue("shellSetup"));
    }

    /// <summary>
    /// **The way back, on a profile that has already said stop** — which is the state the
    /// re-open exists for and the state the auto-launch predicate correctly refuses. Without
    /// `EQBUDDY_SETUP` the screen could not be reached by a test or a capture at all once
    /// dismissed, which is trap 22 exactly: a surface with no way to reach its state reads
    /// as reviewed anyway.
    ///
    /// **`shellSetupAuto=0` beside `shellSetup=1` is the assertion**, and it is what makes
    /// the two tests above mean anything: a hook that had accidentally cleared the dismissal
    /// (or an auto-show that fired regardless) would report 1 here, and every "it did not
    /// come back" row would be passing against a screen that comes back for a different
    /// reason. The dismissal is untouched — a re-open is not a request to be nagged again.
    /// </summary>
    [Fact]
    public void TheHookReopensSetupWithoutClearingTheDismissal()
    {
        using var app = new AppHarness(
            configureSettings: s => s.SetupDismissed = true,
            environment: new Dictionary<string, string>
            {
                ["EQBUDDY_SHELL"] = "1",
                ["EQBUDDY_SETUP"] = "1",
            });
        app.Launch();

        app.WaitForDump("shellSetup", 1, "the hook to open the first-run screen");
        Assert.Equal(0, app.DumpValue("shellSetupAuto"));
        Assert.Equal(1, app.DumpValue("shellSetupDismissed"));
        Assert.Equal(4, app.DumpValue("shellSetupCopyCmd"));
    }

    /// <summary>
    /// **The re-open ROW is on the shell's Behavior tab and not on the v1 window's**, from a
    /// launched app — the runtime half of `SettingsRoomTests.OnlyTheShellHostOffersTheFirstRunScreenAgain`.
    ///
    /// Setup is a layer of `ShellWindow`; `OptionsWindow` has no room to draw it over and is
    /// explicitly out of the owner's lock, so a button there would open nothing. That is a
    /// claim about a CONTROL, and a control that is absent photographs as an unremarkable
    /// list (trap 29) — which is why both hosts are opened at once and asked the same
    /// question under their own prefixes (trap 58).
    /// </summary>
    [Fact]
    public void OnlyTheShellsBehaviorTabCarriesTheWayBackIntoSetup()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "settings:behavior",
            ["EQBUDDY_OPTIONS"] = "1",
        });
        app.Launch();

        app.WaitForDump("shellSettingsTab", "behavior", "the room to land on Behavior");
        Assert.Equal(1, app.DumpValue("shellSettingsBehaviorSetup"));
        Assert.Equal(0, app.DumpValue("optionsBehaviorSetup"));
        // The floor: both hosts really did build the block, so the 0 above is a missing ROW
        // and not a missing window.
        Assert.True(app.DumpValue("optionsBehaviorHotkeys") > 0,
            $"the v1 window's Behavior block was never built; dump was: {app.Artifacts()}");
    }

    // ================================================================================
    // DRA-70 — the Helper room
    // ================================================================================

    /// <summary>
    /// **The room is on the rail, directly under Character, and it PAINTS.**
    ///
    /// <para><c>shellRail</c> counts what the rail built; <c>helperChips</c> counts what the
    /// room built. Two claims, and only the second can catch a room that navigated correctly
    /// and drew nothing — which is exactly the failure trap 72 shipped on the Quests tab for a
    /// whole session. The nine chips are also a trap-29 assertion: an absent control
    /// photographs as an unremarkable panel, so "all nine are there" can only be checked from
    /// a launched app.</para>
    ///
    /// <para>This launch stages no dumps and the fixture log is one live session, so the
    /// Helper is drawn in the state a new player meets: every goal weighed, nothing to
    /// recommend, and every answerable goal naming the store it is waiting for.</para>
    /// </summary>
    [Fact]
    public void TheHelperRoomLandsOnTheRailAndDrawsTheFoundersNineGoals()
    {
        using var app = new AppHarness(environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        Assert.Equal(ShellPages.Landed.Count, app.DumpValue("shellRail"));
        Assert.Equal(9, app.DumpValue("helperChips"));

        // DRA-71 D2: the nine moved INSIDE a dropdown, and the count above is now a claim
        // about rows nobody can see until the face is clicked. The face is the second claim,
        // and it is the one a player reads — "Any goal" is the empty state saying, in the
        // control's own words, that EQBuddy is weighing all nine.
        Assert.Equal("Anygoal", app.DumpText("helperGoalFace"));
        // Shut until something opens it. A dropdown that arrived open would cover the answers
        // this room exists to draw.
        Assert.Equal(0, app.DumpValue("helperPickerOpen"));

        // Nothing picked is the "weigh everything" state, so the deferred goals all say so and
        // the answerable ones name what they are missing. ONE since DRA-149 D3 — Farm Materials
        // gained an engine and moved from a deferral to a gap, after Farm Motes and Make Money
        // did the same in D7 and Farm Gear in D6. This is the row a slice that answers a goal is
        // meant to edit, and Achievements is the last one left to take it to zero.
        Assert.Equal("", app.DumpText("helperGoals"));
        Assert.Equal(1, app.DumpValue("helperNotYet"));
        Assert.True(app.DumpValue("helperGaps") > 0,
            $"no goal named the store it is waiting for; dump was: {app.Artifacts()}");

        // Every door that got built lands on a room that exists. Must be 0, always.
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **The ⧉ copies, from a launched app** — the runtime half of
    /// <c>GameCommandsTests.SurfacesNeedingACommand</c>'s two Helper rows.
    ///
    /// <para>The source scan proves the room NAMES <c>/outputfile faction</c> and
    /// <c>/outputfile achievements</c> off <c>GameCommands</c>. It cannot prove the buttons are
    /// on screen, and a surface that asks a player for a file and hands them no way to produce
    /// it is the defect David reported on 2026-08-20 — worst in the empty state, which is the
    /// only state a new player sees.</para>
    ///
    /// <para><b>FIVE on this screen, and the number is a decision rather than an
    /// accident.</b> Nothing is picked, so every goal is weighed: the faction picker's own
    /// empty state carries one (a player who opened this room to work on faction should not
    /// have to read to the bottom of the answers to find the command), Work on Faction's gap
    /// carries one, and Unlock Classes and Unlock Races carry one EACH — two rows asking for
    /// the same achievements dump. That repetition is deliberate and it is DRA-63's ruling
    /// applied one room over: a row that asks names its own answer, in every state, because a
    /// surface that hands the command over once and then takes it back is the same defect
    /// with a delay on it. Deduplicating would mean one of the two goals asks for a file and
    /// offers nothing, which is exactly the shape that gets noticed by the player who only
    /// picked that one.</para>
    ///
    /// <para><b>The FIFTH arrived with DRA-71 D5 and is the faction picker's rule, applied to
    /// the unlock picker beside it.</b> That block is the control a player uses to say which
    /// races and classes they are chasing, and with no achievements dump it has nothing to
    /// offer — so it says so and hands over the command that fills it, exactly as the faction
    /// picker has since D1. It is the same file the two gaps below ask for, and the same
    /// argument holds: this one is attached to the CONTROL rather than to an answer, and a
    /// picker that explains its own emptiness and points nowhere is the shape this room
    /// refuses.</para>
    /// </summary>
    [Fact]
    public void TheHelperHandsOverTheCommandsItsOwnEmptyStatesAskFor()
    {
        using var app = new AppHarness(environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        Assert.Equal(7, app.DumpValue("helperCopyCmd"));

        // The floor that keeps the count above from being a number about some other room. SEVEN
        // gaps since DRA-71 D7: the five D6 left plus Farm Motes' and Make Money's, which on a
        // profile with no stored session are both "EQBuddy has not stored enough of your play".
        // **The copy count does NOT move with them, and that is the point of asserting both.**
        // Neither new gap has a command that fixes it — no /outputfile writes a mote or a coin —
        // so a room that grew two buttons here would be offering a file that answers nothing.
        Assert.Equal(7, app.DumpValue("helperGaps"));
        // And the gear block really is in its no-dump state rather than offering anchors
        // nobody staged.
        Assert.Equal(0, app.DumpValue("helperWorn"));
        // And the picker really is in its no-dump state rather than offering rows nobody
        // staged: "the room drew an empty picker" and "the room drew no picker" are different
        // claims, and only the first earns the fifth button.
        Assert.Equal(0, app.DumpValue("helperUnlockChips"));
    }

    /// <summary>
    /// **A staged faction dump turns the picker on, and "no dump" becomes "no pick".**
    ///
    /// <para>Two states that look identical on a count and are different answers: with no
    /// dump the goal asks for a command, and with a dump and nothing chosen it asks for a
    /// pick. The dump is staged through the harness in the game's own tab-separated shape so
    /// it goes through the real finder and the real parser — a fixture-shaped substitute
    /// renders a state that is real and is not the one this assertion is about (trap 23).</para>
    ///
    /// <para><c>helperFactionChips</c> is the floor that proves the file was SEEN. Without it
    /// every number below would be about a room that never read it.</para>
    /// </summary>
    [Fact]
    public void AStagedFactionDumpDrawsThePickerAndTheGoalAsksForAPickRatherThanACommand()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s => s.HelperGoals[key] = [nameof(HelperGoal.WorkOnFaction)],
            environment: OpenOn("helper"));
        app.WriteFactionDump((1, "Frogloks of Guk", 500, 1500));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperGoals", nameof(HelperGoal.WorkOnFaction),
            "the stored chip selection to be read back");

        Assert.True(app.DumpValue("helperFactionChips") > 0,
            $"the staged faction dump was not seen; dump was: {app.Artifacts()}");
        // The sub-picker exists and nothing in it is ticked, which is exactly the state the
        // gap line below is about. A face reading anything else here would mean the room drew
        // a pick the engine then said it did not have (trap 4, one control apart).
        Assert.Equal("Anyfaction", app.DumpText("helperFactionFace"));
        Assert.Equal(1, app.DumpValue("helperGaps"));
        Assert.Equal(0, app.DumpValue("helperRecs"));
        // Only one chip is on, so the deferred goals are silent — a filter that still reported
        // about what it filtered out would not be a filter.
        Assert.Equal(0, app.DumpValue("helperNotYet"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **The chip selection is per character and it PERSISTS** — the writer and the reader in
    /// one assertion (trap 20: a setting only readers touch is a lost capability, and it has
    /// cost this repo three player-facing bugs).
    ///
    /// <para>Seeded through <c>configureSettings</c> under the LEDGER's own character key,
    /// which is what the room writes under. A launch that read it back under a different key
    /// would report an empty selection and look exactly like a room nobody had used.</para>
    /// </summary>
    [Fact]
    public void TheGoalSelectionIsReadBackUnderTheCharacterKeyTheRoomWritesUnder()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s => s.HelperGoals[key] =
                [nameof(HelperGoal.LevelUp), nameof(HelperGoal.FarmGear)],
            environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        // The enum's own order, not the stored order — so the "serves" line under an answer
        // reads the way the chip strip does however the player clicked.
        app.WaitForDump("helperGoals",
            $"{nameof(HelperGoal.LevelUp)},{nameof(HelperGoal.FarmGear)}",
            "the stored selection to come back in the Founder's order");

        // And the FACE says both of them — the store's claim and the screen's claim from one
        // moment (trap 56). "The setting holds two goals" and "the player can see which two"
        // are different claims, and D2's whole player-visible change is the second one.
        Assert.Equal("LevelUp·FarmGear", app.DumpText("helperGoalFace"));

        // BOTH are answered since DRA-71 D6, so neither is a deferral and each names what it
        // is waiting for: Level Up has no stored play to divide, Farm Gear has no inventory
        // dump. The row used to read "exactly one of each" and the engine Farm Gear gained is
        // what moved it.
        Assert.Equal(0, app.DumpValue("helperNotYet"));
        Assert.Equal(2, app.DumpValue("helperGaps"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **A TRACKED UPGRADE COMES BACK AFTER A RESTART, AND THE ROOM DRAWS IT** (DRA-216 D4,
    /// S12/S18.3).
    ///
    /// <para>The whole claim of the slice is that a goal outlives things — the sweep that
    /// offered it, and the process that was running when the player clicked. The unit suite
    /// proves the store and the serializer; only a launched app can prove the profile written
    /// by one run is READ by the next under the key the room writes under, which is the
    /// <c>helperGoals</c> row's own lesson one store along (trap 20).</para>
    ///
    /// <para><b>Three facts from one moment</b> (trap 56), because they are three claims:
    /// <c>helperTracked</c> is what the store held, <c>helperTrackedRows</c> is what the block
    /// DREW — an absent block photographs as an unremarkable room (trap 29) — and
    /// <c>helperTrackButtons</c> is whether the player has a way to undo it. A list you cannot
    /// get out of is the silent no-op wearing a feature.</para>
    ///
    /// <para>This fixture has no inventory dump, so the sweep offers nothing and every Track
    /// control counted here belongs to the tracked rows themselves. That is deliberate: it is
    /// the state the slice exists for — the offer is gone and the goal is not.</para>
    /// </summary>
    [Fact]
    public void ATrackedUpgradeComesBackUnderTheKeyTheRoomWritesUnderAndIsDrawn()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        var day = new DateTime(2026, 9, 15, 20, 14, 0, DateTimeKind.Local);
        using var app = new AppHarness(
            configureSettings: s =>
            {
                s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)];
                s.TrackedUpgrades[key] =
                [
                    new TrackedUpgrade("Blade of Carnage", "PRIMARY", "Rusty Short Sword +3", day),
                    new TrackedUpgrade(
                        "Wurmslayer", "SECONDARY", "Shiny Brass Shield +6", day.AddDays(2)),
                ];
            },
            environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        // Newest first, which is the store's own order and not the profile's — the room draws
        // what it is given and decides no order of its own.
        app.WaitForDump("helperTracked", "Wurmslayer,BladeofCarnage",
            "both goals to come back newest-first under the room's own character key");

        // …and the block actually drew them, with a way out of each.
        Assert.Equal(2, app.DumpValue("helperTrackedRows"));
        Assert.Equal(2, app.DumpValue("helperTrackButtons"));
        // The state this slice is FOR: no dump, so nothing in this run offers either item, and
        // the goals are there anyway.
        Assert.Equal(0, app.DumpValue("helperWorn"));
        Assert.Equal(0, app.DumpValue("helperGearWhy"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>The committed negative beside it: a profile nobody has tracked anything in
    /// draws no block at all — not a heading over an empty list, which is a control that is not
    /// there (trap 29 read the other way round).</summary>
    [Fact]
    public void AProfileWithNothingTrackedDrawsNoTrackedBlock()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s => s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)],
            environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperGoals", nameof(HelperGoal.FarmGear),
            "the gear goal to be the one this room is answering");

        Assert.Equal("", app.DumpText("helperTracked"));
        Assert.Equal(0, app.DumpValue("helperTrackedRows"));
        Assert.Equal(0, app.DumpValue("helperTrackButtons"));
    }

    /// <summary>
    /// **THE FACE COUNTS INSTEAD OF LISTING ONCE THE NAMES STOP FITTING** — #184's cap, on the
    /// Helper's own noun, from a launched app (DRA-71 D2).
    ///
    /// <para><c>PickerFaceTests</c> walks all 512 subsets of the nine goals and proves the rule.
    /// It cannot prove the ROOM passes its own budget to it, and that is the half that has gone
    /// wrong before: the class face was capped in UI.Shared for a whole release while the window
    /// rendered an uncapped label, because the cap and the call site were two decisions. Three
    /// long goals is 49 characters of face in a room whose floor is 520 wide.</para>
    /// </summary>
    [Fact]
    public void TheGoalFaceCountsRatherThanListingWhenTheNamesStopFitting()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s => s.HelperGoals[key] =
            [
                nameof(HelperGoal.UnlockClasses),
                nameof(HelperGoal.UnlockRaces),
                nameof(HelperGoal.FarmMaterials),
            ],
            environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperGoalFace", "3goals",
            "the face to count three long goal names rather than list them");

        // The floor that keeps the line above from being a number about an empty room: the
        // rows are still all nine, and the selection really was read back.
        Assert.Equal(9, app.DumpValue("helperChips"));
        Assert.Equal(
            $"{nameof(HelperGoal.UnlockClasses)},{nameof(HelperGoal.UnlockRaces)}," +
            $"{nameof(HelperGoal.FarmMaterials)}",
            app.DumpText("helperGoals"));
    }

    /// <summary>
    /// **THE POPUP OPENS, AND THE DUMP SAYS SO** — the runtime half of the screenshot hook
    /// (<c>EQBUDDY_HELPER_PICKER</c>) that stages the one state a shot of this room cannot
    /// otherwise reach.
    ///
    /// <para>A dropdown that is SHUT photographs as a button. The hook is what lets
    /// <c>shell-helper-picker</c> exist at all, and a hook that was merely spelled correctly —
    /// read from the environment, never wired to the control — would stage nothing and produce
    /// a shot identical to the closed one. That failure is invisible in a picture and visible
    /// here (trap 22 and trap 29 arriving together).</para>
    /// </summary>
    [Fact]
    public void TheReviewHookReallyOpensTheGoalPicker()
    {
        var env = OpenOn("helper");
        env["EQBUDDY_HELPER_PICKER"] = "goals";
        using var app = new AppHarness(environment: env);
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperPickerOpen", "1", "the review hook to open the goals picker");

        // Opening it changes nothing about what the room decided — the face still reads the
        // empty state and the nine rows are the nine rows.
        Assert.Equal("Anygoal", app.DumpText("helperGoalFace"));
        Assert.Equal(9, app.DumpValue("helperChips"));
    }

    // ================================================================================
    // DRA-71 D5 — the unlock pick, one store read by two rooms
    // ================================================================================

    /// <summary>
    /// **THE HELPER OFFERS THE PICK AND RANKS FROM IT** (DRA-71 D5, plan P11; acceptance A8).
    ///
    /// <para>The other half of this claim is asserted from the Quests window
    /// (<c>APickedUnlockIsTheOnlyOneItsSectionDrawsAndTheTabSaysWhatItHid</c>). They are two
    /// tests rather than one because they are two surfaces in two hosts — but they read the
    /// SAME key of the SAME profile under the same character, which is what makes
    /// "one store" a claim rather than a hope: <c>helperUnlockPicks</c> and
    /// <c>questsUnlockPicks</c> are both the store's own answer, dumped from the room that
    /// used it.</para>
    ///
    /// <para><b>Prediction.</b> The dump names two races and one class. "Barbarian" is picked,
    /// so the race engine answers about Barbarian alone — and the CLASS engine, whose section
    /// the pick names nothing in, still answers about Warrior. So the two unlock answers on
    /// screen name those two subjects and not High Elf. The picker offers all three, because
    /// both unlock goals are picked and an offer narrowed by its own filter is a tick nobody
    /// can take back.</para>
    /// </summary>
    [Fact]
    public void TheHelperOffersTheUnlockPickAndRanksFromIt()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s =>
            {
                s.HelperGoals[key] =
                    [nameof(HelperGoal.UnlockRaces), nameof(HelperGoal.UnlockClasses)];
                s.UnlockPicks[key] = ["Barbarian"];
            },
            environment: OpenOn("helper"));
        app.WriteAchievementsDump(
            "Untapped Potential: Races",
            "I\tRace Unlock - High Elf",
            "I\t\tGet maximum faction with Clerics of Tunare.",
            "I\tRace Unlock - Barbarian",
            "I\t\tGet maximum faction with Rallosian Army.",
            "Untapped Potential: Classes",
            "I\tClass Unlock - Warrior",
            "I\t\tObtain Azure Ruby Ring.");
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperUnlockPicks", "Barbarian",
            "the Helper to read the pick the Quests tab writes");

        // The popup offers ALL THREE — the way back from a pick, on the surface that made it.
        Assert.Equal(3, app.DumpValue("helperUnlockChips"));
        // One pick is always named rather than counted, whatever the budget.
        Assert.Equal("Barbarian", app.DumpText("helperUnlockFace"));
        // And the ANSWERS narrowed. Barbarian is the picked race; Warrior survives because the
        // pick names nothing in the Classes section. High Elf is the one the pick removed, and
        // naming all three in one assertion is what separates "the filter fired" from "the
        // dump only ever had two unlocks in it".
        Assert.Equal("Barbarian,Warrior", app.DumpText("helperSubjects"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    // ================================================================================
    // DRA-71 D3 — one level, two writers, ordered by time
    // ================================================================================

    /// <summary>
    /// **A REAL DING, THROUGH THE REAL PARSER, REACHES BOTH ROOMS.**
    ///
    /// <para>The fixture log carries no level line, so the launch starts in the state a fresh
    /// profile is in: nothing known, and the Character room saying so rather than drawing a
    /// blank row. Then the game's own sentence is appended and the app has to do the whole
    /// chain — parse, stamp with the LOG's time, store, resolve, redraw two rooms.</para>
    ///
    /// <para><b>Both rooms are asserted, and that is the point.</b> "The ledger has 30" and
    /// "the Helper ranked with 30" are different claims (trap 56), and the second is the one
    /// the Founder's MUST is about. A level that reached Character and not the Helper is
    /// exactly the shape trap 72 shipped on the Quests tab — a store written and a surface
    /// whose repaint gate never heard about it.</para>
    /// </summary>
    [Fact]
    public void ADingWritesTheLevelAndTheCharacterRoomNamesIt()
    {
        using var app = new AppHarness(environment: OpenOn("home"));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to land on the Character room");
        // The unknown state is a real one and it is where a fresh profile starts.
        Assert.Equal(0, app.DumpValue("shellHomeLevel"));
        Assert.Equal("unknown", app.DumpText("shellHomeLevelSource"));

        app.AppendLogLines("You have gained a level! Welcome to level 30!");

        app.WaitForDump("shellHomeLevel", "30", "the ding to reach the Character room");
        app.WaitForDump("shellHomeLevelSource", "observed",
            "the level to be labelled as the log's own statement");
        // Nobody typed anything, so there is no statement to undo.
        Assert.Equal(0, app.DumpValue("shellHomeStatedLevel"));

    }

    /// <summary>
    /// **FIXTURE ONE, FROM A LAUNCHED APP: a statement made AFTER the ding wins.**
    ///
    /// <para>The Founder's own case. A Legends character holds up to three classes at once, so
    /// the level the log printed belongs to whatever was equipped when it printed; a player who
    /// swaps and says "I am 28 on this one" is correcting a number that is still true about a
    /// different thing.</para>
    ///
    /// <para>Both claims are seeded into the real ledger file with real stamps and read back
    /// through the real store, so this is the resolution the app performs and not a rule a unit
    /// test agreed with. <c>shellHomeStatedLevel</c> is the floor that proves the seeded file
    /// was SEEN — without it every number here would be about a room that never read it.</para>
    /// </summary>
    [Fact]
    public void AStatementMadeAfterTheDingIsTheLevelTheHelperRanksWith()
    {
        var now = DateTime.Now;
        using var app = new AppHarness(environment: OpenOn("helper"));
        app.SeedQuestLedger(
            level: (31, now.AddHours(-3)),
            statedLevel: (28, now.AddHours(-1)));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperLevel", "28", "the fresher statement to be what the Helper ranks with");
        Assert.Equal("stated", app.DumpText("helperLevelSource"));
        // A known level needs no door to go and set one.
        Assert.Equal(0, app.DumpValue("helperLevelDoor"));

    }

    /// <summary>
    /// **FIXTURE TWO, THE OTHER WAY: a ding AFTER the statement wins** — and this one is the
    /// prove-fail for the row above (trap 34: green-only is vacuous coverage).
    ///
    /// <para>The same two writers, the same store, the same rooms; only the ORDER in time is
    /// different, and the answer flips. The statement is seeded with a stamp from before the
    /// launch and the ding arrives live through the log, so its stamp is genuinely later — no
    /// fixture arithmetic decides the winner, the clock does.</para>
    ///
    /// <para>A precedence table in either direction passes one of these two rows and fails the
    /// other, which is precisely why the plan asked for both.</para>
    /// </summary>
    [Fact]
    public void ADingThatArrivesAfterTheStatementTakesTheLevelBack()
    {
        using var app = new AppHarness(environment: OpenOn("home"));
        app.SeedQuestLedger(statedLevel: (28, DateTime.Now.AddHours(-1)));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to land on the Character room");
        app.WaitForDump("shellHomeLevel", "28", "the seeded statement to answer on its own");
        Assert.Equal("stated", app.DumpText("shellHomeLevelSource"));

        app.AppendLogLines("You have gained a level! Welcome to level 31!");

        app.WaitForDump("shellHomeLevel", "31", "the fresher ding to overtake the statement");
        app.WaitForDump("shellHomeLevelSource", "observed",
            "the line to say the number came from the log");
        // The statement is still STORED — it was overtaken, not deleted, so the undo row is
        // still there and a later correction does not have to be retyped from nothing.
        Assert.Equal(28, app.DumpValue("shellHomeStatedLevel"));
    }

    /// <summary>
    /// **An unknown level draws a sentence and a DOOR, never a guess** (plan P4).
    ///
    /// <para>The answers above it are real — they are ranked from the player's own stored play
    /// and do not need a level to be true — so the room says what it did anyway and points at
    /// the one place that can fill the gap. <c>helperLevelDoor</c> is trap 29's assertion: a
    /// control that is ABSENT photographs as an unremarkable panel, so only a launched app can
    /// say the way forward is on screen rather than merely implied by the sentence.</para>
    ///
    /// <para><b>Then a ding arrives and the door goes away</b>, which is the half that would
    /// have shipped broken. The Helper's repaint gate has to carry the level, and a room whose
    /// fingerprint did not fold it would keep drawing "EQBuddy does not know your level" for
    /// the rest of the session with the number sitting in the store one room away — trap 72,
    /// exactly as the Quests tab had it.</para>
    /// </summary>
    [Fact]
    public void AnUnknownLevelOffersTheCharacterDoorUntilADingFillsItIn()
    {
        using var app = new AppHarness(environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        Assert.Equal(0, app.DumpValue("helperLevel"));
        Assert.Equal("unknown", app.DumpText("helperLevelSource"));
        app.WaitForDump("helperLevelDoor", "1",
            "the unknown-level line to offer a door into the Character room");
        // And it lands somewhere: the door count went up and none of them are dead.
        Assert.True(app.DumpValue("helperDoors") > 0,
            $"no door was built at all; dump was: {app.Artifacts()}");
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));

        app.AppendLogLines("You have gained a level! Welcome to level 30!");

        app.WaitForDump("helperLevel", "30",
            "the Helper's own repaint gate to notice the level the log just gave");
        Assert.Equal("observed", app.DumpText("helperLevelSource"));
        app.WaitForDump("helperLevelDoor", "0",
            "the door to go away once there is nothing left for it to fix");
    }

    /// <summary>
    /// **THE THROUGHPUT PROBE REACHES THE SCREEN** (DRA-71 D4, plan P7; Founder smoke item 3).
    ///
    /// <para><b>This is the row the slice most needed.</b> D4's numbers come from a SECOND
    /// query over <c>history.db</c> — a <c>JsonDocument</c> probe of each stored snapshot for
    /// dps, hps and the combat seconds they were quoted against — and every unit test in the
    /// repo could pass with that query never running, its result never joined, or its facts
    /// folded and never drawn. Trap 72 is exactly that shape on the Quests tab: a store
    /// written, and a surface whose repaint gate never heard about it.</para>
    ///
    /// <para>So the session is archived through the REAL repository and the REAL snapshot
    /// type before launch, and the assertions are the ENGINE's claim and the SCREEN's claim
    /// from the same dump (trap 56): <c>helperThroughput</c> counts the drawn answers carrying
    /// the fact, <c>helperTopDps10</c> carries the measured number itself — 42.0 damage a
    /// second, seeded, so a zero or a rounding would both be visible — and
    /// <c>helperWhy</c>/<c>helperPersonalWhy</c> count what was BUILT into the tree.</para>
    ///
    /// <para>The zone is an instance, so the tier fact rides along too: its name is what a
    /// zone line prints, which is the whole of how the tier is known
    /// (<c>ZoneRoll.ObservedTier</c>) — nothing was looked up and no column was added.</para>
    /// </summary>
    [Fact]
    public void ArchivedThroughputReachesTheHelpersDrawnAnswers()
    {
        // **THE GOAL IS PICKED SINCE DRA-149 D3**, and the reason is this row's own premise.
        // It used to weigh every goal and assert "the seeded session is the only one, so it is
        // the only answer" — true while Farm Materials was Deferred, and false the moment that
        // engine landed, because it answers out of the SHIPPED CATALOG and needs no stored play
        // at all. Pinning the goal is what makes this a test about the throughput probe rather
        // than about how many other engines happen to answer today; every later slice that
        // answers a goal would otherwise move this number again.
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s => s.HelperGoals[key] = [nameof(HelperGoal.LevelUp)],
            environment: OpenOn("helper"));
        app.SeedStoredSession(
            "Najena 4 (Refined)", TimeSpan.FromHours(4), xpPercent: 32,
            dps: 42.0, hps: 0, combatSeconds: 3600, deaths: 0, activeFraction: 1.0,
            mobs: ("a bloodthirsty gnoll", 180, 28, 30, 34));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        // The seeded session is the only one, so it is the only answer — and the ENGINE saw it.
        app.WaitForDump("helperRecs", "1", "the archived session to become one recommendation");
        Assert.Equal("Najena4(Refined)", app.DumpText("helperZones"));

        // The SCREEN's claim about the throughput line, and the number in it.
        app.WaitForDump("helperThroughput", "1",
            "the throughput sentence to be drawn on the answer");
        Assert.Equal(420, app.DumpValue("helperTopDps10"));
        // One measured zone, so there is nothing to compare against and the tier rides along.
        Assert.Equal(1, app.DumpValue("helperTier"));
        // A sitting that was active throughout says nothing about downtime — the silence is
        // asserted, because a line that appeared on every camp would be furniture.
        Assert.Equal(0, app.DumpValue("helperDowntime"));

        // And the sentences were BUILT, not merely returned. Every one is the player's own
        // evidence: nothing here came from a catalog, and nothing came off anyone else's screen.
        Assert.True(app.DumpValue("helperWhy") >= 3,
            $"the room drew fewer sentences than the engine produced; dump was: {app.Artifacts()}");
        Assert.Equal(app.DumpValue("helperWhy"), app.DumpValue("helperPersonalWhy"));
        Assert.Equal(0, app.DumpValue("helperCatalogWhy"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **THE PROVE-FAIL FOR THE ROW ABOVE, and the honesty rule underneath it** (trap 34:
    /// green-only is vacuous coverage).
    ///
    /// <para>The same archived session with NO combat seconds. The probe reads the snapshot,
    /// finds nothing to divide, and skips the row — so the recommendation is still there,
    /// still ranked on the experience rate the player really earned, and draws none of D4's
    /// sentences. <b>An absent measurement is not a poor one:</b> a player upgrading into this
    /// build must not watch their best camp drop for a gap in EQBuddy's own reading, and the
    /// number the dump reports is 0 meaning "not measured" rather than 0 meaning "you did
    /// nothing".</para>
    ///
    /// <para>Without this row, the one above passes on a build where the probe returns
    /// everything unconditionally and the fold treats a missing denominator as a zero.</para>
    /// </summary>
    [Fact]
    public void ASessionWithNothingToDivideDrawsNoThroughputAndStillRanks()
    {
        // The goal this row prove-fails, picked — the same premise repair the row it guards
        // took in DRA-149 D3. "Still ranks" is a claim about the experience engine, and it
        // must not be satisfiable by some other engine answering about some other zone.
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s => s.HelperGoals[key] = [nameof(HelperGoal.LevelUp)],
            environment: OpenOn("helper"));
        app.SeedStoredSession(
            "Lower Guk", TimeSpan.FromHours(4), xpPercent: 32,
            dps: 0, hps: 0, combatSeconds: 0,
            mobs: ("a froglok tad", 180, 28, 30, 34));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperRecs", "1", "the archived session to become one recommendation");
        Assert.Equal("LowerGuk", app.DumpText("helperZones"));

        Assert.Equal(0, app.DumpValue("helperThroughput"));
        Assert.Equal(0, app.DumpValue("helperTopDps10"));
        // Open world, so no tier either — and the rate that ranked it is still on screen.
        Assert.Equal(0, app.DumpValue("helperTier"));
        Assert.True(app.DumpValue("helperWhy") >= 2,
            $"the rate and cadence lines did not reach the screen; dump was: {app.Artifacts()}");
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **THE MOTE FOLD AND THE SALE PROBE REACH THE SCREEN** (DRA-71 D7, plans P9 and P10;
    /// Founder smoke items 4c and 5).
    ///
    /// <para><b>The same row D4 most needed, for the same reason.</b> Both of this slice's
    /// answers come from things a unit test cannot see running: the mote fold joins the pooled
    /// loot to the zone rollup, and the sale price is a SECOND <c>JsonDocument</c> probe of each
    /// stored snapshot, beside D4's. Either could be built, folded, and never drawn — trap 72's
    /// shape — or never queried at all, and every unit test in the repo would still pass.</para>
    ///
    /// <para>So one session is archived through the REAL repository and the REAL snapshot type,
    /// carrying loot, coin AND a vendor sale, and the assertions are the fold's claim and the
    /// SCREEN's claim from the same dump (trap 56). The zone is an instance OUTSIDE the band the
    /// Founder named, so the tier preference is drawn too — the fact D4 deliberately reported
    /// and refused to weigh, now weighing something.</para>
    ///
    /// <para><c>helperCatalogValue</c> is asserted at 0 and that is a REPORT rather than a
    /// wish: the promoter learned <c>MerchantCopper</c> in this slice and the shipped catalog
    /// has none in it yet, so every price on this screen is one the player was actually
    /// paid.</para>
    /// </summary>
    [Fact]
    public void ArchivedMotesAndVendorSalesReachTheHelpersDrawnAnswers()
    {
        // The THREE engines this row is about, picked — see the throughput row above for why
        // (DRA-149 D3). The join it asserts is between these three and nothing else, so a
        // fourth engine answering about the same zone would make `helperTopGoals` pass for a
        // reason this test does not mean.
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s => s.HelperGoals[key] =
            [
                nameof(HelperGoal.LevelUp), nameof(HelperGoal.FarmMotes),
                nameof(HelperGoal.MakeMoney),
            ],
            environment: OpenOn("helper"));
        app.SeedStoredSession(
            "Najena - Solo", TimeSpan.FromHours(4), xpPercent: 32,
            dps: 42.0, hps: 0, combatSeconds: 3600, deaths: 0, activeFraction: 1.0,
            copper: 40_000,
            sold: [("Bone Chips", 5, 400)],
            loot:
            [
                ("a shadowed man", "Mote of Major Potential", 6),
                ("a shadowed man", "Bone Chips", 20),
            ],
            mobs: ("a shadowed man", 180, 28, 30, 34));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperRecs", "1", "the archived session to become one recommendation");
        Assert.Equal("Najena-Solo", app.DumpText("helperZones"));

        // THE FOLD's claim. The floor is what matters and not an exact count: the shared
        // fixture log has its own motes and its own sales in it, so this profile's numbers are
        // the seeded session PLUS whatever the fixture character did — and pinning an exact
        // total here would be a test about the fixture rather than about the fold. What is
        // asserted is that the seeded zone cleared both floors (4 hours, 180 kills) and that the
        // sale probe came back with something out of the stored JSON at all.
        Assert.True(app.DumpValue("helperMoteZones") >= 1,
            $"the mote fold produced no zone; dump was: {app.Artifacts()}");
        Assert.True(app.DumpValue("helperMoteRated") >= 1,
            $"no mote zone cleared the floors; dump was: {app.Artifacts()}");
        Assert.True(app.DumpValue("helperSales") >= 1,
            $"the sale probe read nothing out of the stored snapshots; dump was: {app.Artifacts()}");

        // **THE SCREEN's claims, and this row is where the merge rule earned itself.** Three
        // engines answer about this one zone — Level Up, Farm Motes and Make Money — which is the
        // cross-domain join doing exactly what the room exists for (HOME-005). It also puts ten
        // sentences on one row against a WhyCap of six, and before DRA-71 D7's interleave the
        // cap trimmed the tail, so the row's own headline said "Make Money" over six sentences
        // of which not one was about money. Every goal the headline claims is asserted to have a
        // sentence under it here, because that is the failure this arrangement reproduces.
        app.WaitForDump("helperMoteWhy", "1", "the mote rate sentence to be drawn");
        Assert.Equal(1, app.DumpValue("helperCoinWhy"));
        Assert.Equal(1, app.DumpValue("helperSellable"));
        Assert.Equal(1, app.DumpValue("helperMoneyNote"));
        Assert.True(app.DumpValue("helperTopGoals") >= 3,
            $"the three engines did not join on the zone; dump was: {app.Artifacts()}");

        // "Najena - Solo" is D0, outside the D2–D4 the Founder named, so the preference fires
        // and says so. The zone name is the only input — nothing was looked up.
        Assert.Equal(1, app.DumpValue("helperTierPref"));

        // **AND THE THING THE CAP GAVE UP IS THE ONE THAT EXPLAINS NOTHING.** The mote engine
        // emits its WHO line last precisely so it is what a loaded row loses, ahead of any
        // sentence explaining a mark-down that already happened — D4's ordering rule, applied
        // to this engine's facts. The row SAYS it held something back, which is trap 50 asserted
        // from a launched app rather than from the engine's own return value.
        Assert.Equal(0, app.DumpValue("helperMoteSource"));
        Assert.True(app.DumpValue("helperWhyWithheld") >= 1,
            $"the row trimmed a sentence and did not admit it; dump was: {app.Artifacts()}");

        // Every price on screen is one the player was PAID. The catalog carries no vendor value
        // in this build, and a row here would mean the promoter's field had data behind it.
        Assert.Equal(0, app.DumpValue("helperCatalogValue"));
        Assert.Equal(0, app.DumpValue("helperCatalogWhy"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **THE PROVE-FAIL FOR THE ROW ABOVE** (trap 34: green-only is vacuous coverage).
    ///
    /// <para>The same archived session with the same hours and the same coin, and NO mote in the
    /// loot. The money half is unchanged — it never depended on a mote — and the mote half goes
    /// completely silent: no fold row, no sentence, and the goal draws its own
    /// "no mote has dropped for you" rather than the no-history one.</para>
    ///
    /// <para>Without it, the row above passes on a build where the mote fold returns every zone
    /// it is handed and the sentence is drawn from the zone rather than from the motes.</para>
    /// </summary>
    [Fact]
    public void ASessionWithNoMotesDrawsNoMoteAnswerAndStillMakesMoney()
    {
        // The three goals this row prove-fails, picked — see the row above (DRA-149 D3). The
        // silence it asserts is about the MOTE engine, so a fourth engine's row arriving would
        // change `helperRecs` without changing anything this test means.
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s => s.HelperGoals[key] =
            [
                nameof(HelperGoal.LevelUp), nameof(HelperGoal.FarmMotes),
                nameof(HelperGoal.MakeMoney),
            ],
            environment: OpenOn("helper"));
        app.SeedStoredSession(
            "Najena - Solo", TimeSpan.FromHours(4), xpPercent: 32,
            dps: 42.0, hps: 0, combatSeconds: 3600, deaths: 0, activeFraction: 1.0,
            copper: 40_000,
            sold: [("Bone Chips", 5, 400)],
            loot: [("a shadowed man", "Bone Chips", 20)],
            mobs: ("a shadowed man", 180, 28, 30, 34));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperRecs", "1", "the archived session to become one recommendation");

        // RATED and not FOLDED: the shared fixture log has motes of its own in a zone that
        // clears neither floor, so the fold legitimately produces a row for it and quotes no
        // rate. What this slice removed from the fixture is the only mote zone that HAD one.
        Assert.Equal(0, app.DumpValue("helperMoteRated"));
        Assert.Equal(0, app.DumpValue("helperMoteWhy"));
        Assert.Equal(0, app.DumpValue("helperMoteSource"));
        // The tier preference is the mote engine's own weight, so it goes with it.
        Assert.Equal(0, app.DumpValue("helperTierPref"));

        // …and the money answer is untouched, which is what makes the silence above a
        // measurement of the motes rather than of the fixture.
        Assert.Equal(1, app.DumpValue("helperCoinWhy"));
        Assert.Equal(1, app.DumpValue("helperSellable"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **THE EDITOR BOX EXISTS, AND THE DUMP SAYS SO** — the runtime half of the screenshot
    /// hook (<c>EQBUDDY_HOME_EDITOR</c>) that stages the one state a shot of this room cannot
    /// otherwise reach.
    ///
    /// <para>A shut editor photographs as a link. The hook is what lets
    /// <c>shell-home-level</c> exist at all, and a hook that was merely spelled correctly —
    /// read from the environment, never applied to the build — would stage nothing and produce
    /// a shot identical to the closed one (trap 22 and trap 29 arriving together, the same
    /// pair D2 hit with the picker).</para>
    /// </summary>
    [Fact]
    public void TheReviewHookReallyOpensTheLevelEditor()
    {
        var env = OpenOn("home");
        env["EQBUDDY_HOME_EDITOR"] = "level";
        using var app = new AppHarness(environment: env);
        app.SeedQuestLedger(statedLevel: (30, DateTime.Now.AddHours(-1)));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to land on the Character room");
        app.WaitForDump("shellHomeLevelBox", "1", "the review hook to open the level editor");
        // **And the box holds the standing statement**, which is a different claim from "a box
        // was built". The first staged shot of this state came back EMPTY while a player
        // clicking the same link got theirs pre-filled — a picture of a real state of
        // something else (trap 23), caught by the prediction written before the run rather
        // than by anything that could fail. The hook and the click now share one opener, and
        // this is the row that says so from outside.
        Assert.Equal("30", app.DumpText("shellHomeLevelDraft"));

        // Opening it changes nothing about what the room DECIDED — the line still names the
        // same level from the same source, and nothing has been refused.
        Assert.Equal(30, app.DumpValue("shellHomeLevel"));
        Assert.Equal("stated", app.DumpText("shellHomeLevelSource"));
        Assert.Equal(0, app.DumpValue("shellHomeLevelRefused"));
        // The three blocks are still three: an editor is inside Identity, not a fourth block.
        Assert.Equal(3, app.DumpValue("shellHomeBlocks"));
    }

    // ================================================================================
    // DRA-71 D6 — Farm Gear asks the intent first
    // ================================================================================

    /// <summary>
    /// An inventory dump with two plain worn items in it — the sweep's anchors, written in the
    /// game's own tab-separated shape so they go through the real parser (trap 23).
    ///
    /// <para>Both names are REAL rows in the shipped <c>ItemCatalog</c> with no class lock on
    /// them, which is what makes the predictions below arithmetic rather than hope: "Cloth Cap"
    /// is AC 2 in the HEAD slot and "Cloth Choker" is AC 1 in the NECK slot. A made-up item
    /// would resolve to no stats, drop out of <c>WornFrom</c>, and every assertion here would
    /// be about an empty sweep.</para>
    /// </summary>
    private static void WearTwoPlainThings(AppHarness app) =>
        app.WriteInventoryDump(("Head", "Cloth Cap", 1), ("Neck", "Cloth Choker", 1));

    /// <summary>
    /// **UPGRADE WHAT I WEAR: THE PICK ANCHORS THE SWEEP** (DRA-71 D6, plan P8; acceptance A5).
    ///
    /// <para><b>Prediction, computed against the shipped catalog before the run.</b> The
    /// character infers WARRIOR from the fixture log, so the class lock is WAR. "Cloth Cap" is
    /// the only picked anchor, and 111 catalog HEAD items beat AC 2 while being usable by a
    /// warrior and having somewhere to drop. The per-anchor cap keeps 8 and reports
    /// <b>103</b> withheld. Those eight group into five zones — Temple of Veeshan (3), Clan
    /// Runnyeye (2), then Kael Drakkel, Tower of Frozen Shadow and Veeshan's Peak with one
    /// each — so the room's cap shows <b>three</b> and says it held <b>two</b> back. Nothing
    /// in the fixture log ever looted any of them, so there is no observed-drop line.</para>
    ///
    /// <para><b>Both halves from one moment</b> (trap 56): <c>helperGearWithheld</c> is what
    /// the ENGINE held back and <c>helperGearWhy</c> is how many drawn rows actually carry an
    /// upgrade sentence. A sweep that ranked correctly and drew nothing would satisfy only the
    /// first, which is exactly the shape trap 72 shipped on the Quests tab.</para>
    /// </summary>
    [Fact]
    public void UpgradeWhatIWearAnchorsOnThePickedItemAndNamesWhereItDrops()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s =>
            {
                s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)];
                s.HelperWornPicks[key] = ["Cloth Cap"];
            },
            environment: OpenOn("helper"));
        WearTwoPlainThings(app);
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperWorn", "2", "the inventory dump to become two anchors");

        // The intent strip offers all three and starts on the Founder's first.
        Assert.Equal("upgradeworn", app.DumpText("helperIntent"));
        Assert.Equal(3, app.DumpValue("helperIntentChips"));
        // The picker belongs to THIS intent, offers both worn things, and names the one pick.
        Assert.Equal(2, app.DumpValue("helperWornChips"));
        Assert.Equal("ClothCap", app.DumpText("helperWornFace"));
        // The toggle is drawn and off — "the room decided not to offer it" and "the room
        // forgot" are different claims (trap 29).
        Assert.Equal(1, app.DumpValue("helperQuestToggle"));
        Assert.Equal(0, app.DumpValue("helperQuestsOn"));

        // The answers, in rank order: a zone that feeds three of your upgrades outranks one
        // that feeds two.
        app.WaitForDump("helperZones", "TempleofVeeshan,ClanRunnyeye,KaelDrakkel",
            "the gear sweep to rank the zones by how many upgrades each one feeds");
        Assert.Equal(3, app.DumpValue("helperRecs"));
        Assert.Equal(2, app.DumpValue("helperWithheld"));
        Assert.Equal(103, app.DumpValue("helperGearWithheld"));

        // The SCREEN's claim beside the engine's, and the personal half staying silent
        // because this fixture has never looted one of these.
        Assert.Equal(3, app.DumpValue("helperGearWhy"));
        Assert.Equal(0, app.DumpValue("helperGearSeen"));
        // **DRA-84 D4: what each of those rows can now SAY.** Six item lines across the three
        // drawn zones (3 + 2 + 1), and every one of them names a creature — the half of
        // acceptance item 2 that read as empty on the Founder's build. None of the eight
        // upgrades is anonymous, so nothing is withheld here; the row two below stages a fixture
        // where the rule fires.
        Assert.Equal(6, app.DumpValue("helperWho"));
        Assert.Equal(0, app.DumpValue("helperWhoWithheld"));
        // Every gear line is catalog-sourced, so every one of them carries the estimate label.
        Assert.Equal(0, app.DumpValue("helperPersonalWhy"));
        Assert.True(app.DumpValue("helperCatalogWhy") >= 3,
            $"the catalog sentences did not reach the screen; dump was: {app.Artifacts()}");
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **THE BAND GATE, IN THE LAUNCHED APP, AGAINST A REAL LEDGER** (DRA-84 D2, plan P2;
    /// Founder acceptance 3).
    ///
    /// <para><b>The same staging and the same stored pick as the row above — only a level is
    /// added.</b> That is the whole assertion: the row above leaves the level UNKNOWN, so the
    /// gate stands down and its five zones are the ungated answer. Seed a level through the real
    /// ledger file and two of those five have to leave, which is the Founder's complaint
    /// answered on the surface he failed rather than in a unit test.</para>
    ///
    /// <para><b>Prediction, computed against the shipped bands before the run</b> (trap 23).
    /// The eight surviving upgrades group into Temple of Veeshan (3), Clan Runnyeye (2), Kael
    /// Drakkel, Tower of Frozen Shadow and Veeshan's Peak. At level 28:</para>
    /// <list type="bullet">
    /// <item>Temple of Veeshan `60+` and Veeshan's Peak `60+` — bottom 60, which is 32 over 28,
    /// so both go on the BOTTOM arm. <b>Two refused.</b></item>
    /// <item>Kael Drakkel `30-60+` — bottom 30 is 2 over, inside
    /// <c>GearBandReachAbove</c>, and an open top has no maximum to be under. Kept.</item>
    /// <item>Tower of Frozen Shadow `26-51` — 28 sits inside it. Kept.</item>
    /// <item>Clan Runnyeye — the fold does not bridge it to the wiki's "Runnyeye" page, so it
    /// has NO band and an unanswered question gates nothing (trap 73). Kept.</item>
    /// </list>
    /// <para>So the three drawn zones become Clan Runnyeye (2 upgrades), Kael Drakkel and Tower
    /// of Frozen Shadow — and the top row CHANGES, which is the gate visible in the answers and
    /// not only in a caption. <c>helperGearWithheld</c> stays <b>103</b>: the sweep's per-anchor
    /// cap is spent before the gate runs, and a gate that moved it would mean the two counts had
    /// been wired together.</para>
    ///
    /// <para><b>Three claims from one moment</b> (trap 56): the ENGINE refused two
    /// (<c>helperBandRefused</c>), the ROOM drew the sentence saying so
    /// (<c>helperBandLine</c>), and the gate was live at all (<c>helperBandGate</c>). A refusal
    /// the player is never told about is a row that vanished, and only a launched app can say
    /// the caption was drawn.</para>
    /// </summary>
    [Fact]
    public void TheBandGateRefusesTheZonesOutsideYourLevelAndSaysSo()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s =>
            {
                s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)];
                s.HelperWornPicks[key] = ["Cloth Cap"];
            },
            environment: OpenOn("helper"));
        WearTwoPlainThings(app);
        app.SeedQuestLedger(statedLevel: (28, DateTime.Now.AddHours(-1)));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperWorn", "2", "the inventory dump to become two anchors");
        app.WaitForDump("helperLevel", "28", "the seeded statement to be what the Helper ranks with");

        // The gate is LIVE — a zero refusal count below would otherwise be indistinguishable
        // from a gate that never ran (trap 78).
        Assert.Equal(1, app.DumpValue("helperBandGate"));

        app.WaitForDump("helperZones", "ClanRunnyeye,KaelDrakkel,TowerofFrozenShadow",
            "the band gate to remove the two level-60 planes and re-rank what is left");

        // What the ENGINE refused, and that the ROOM said so.
        Assert.Equal(2, app.DumpValue("helperBandRefused"));
        Assert.Equal(1, app.DumpValue("helperBandLine"));

        // **DRA-84 D5: the relationship, not the count** (plan P6). Two is equally the answer
        // of a gate that refused these two zones off the wrong band, the wrong arm or a level
        // it never read — the count moves for none of those. This asserts the comparison the
        // gate actually made: eqlwiki's own `60+` for both zones, against the 28 asserted
        // above, refused on the BOTTOM arm because 60 is 32 over 28 and `GearBandReachAbove`
        // is 5. The TOP arm cannot appear here — an open-topped band has no maximum to be
        // under — and a run that reported `TopUnder` would be the D2 ruling broken while both
        // counts stayed green.
        Assert.Equal("TempleofVeeshan:60+:BottomOver,Veeshan'sPeak:60+:BottomOver",
            app.DumpText("helperBandRefusals"));

        // **DRA-180 D2: THE ERA GATE IS WIRED AND DELIBERATELY DARK, and this is where that is
        // checked in a launched app.**
        //
        // `WorldEra.Current` ships EMPTY (plan P2) — no file in this repo states what era the
        // world is at and D2 refuses to invent one — so the gate stands down whole and this
        // fixture behaves exactly as it did before the slice. `helperEraGate` is the LIVENESS
        // fact and it is read FIRST: a zero refusal count below is the same zero on a build
        // where the gate was never wired at all, which is the assertion DRA-149 D5 item 2 was
        // caught by. `DumpValue` throws on a fact that is absent, so this line also proves the
        // room is emitting it.
        //
        // **This test is D5's prediction pack anchor.** Kael Drakkel is drawn above and eqlwiki
        // dates it to Velious. The day the curated world era is set to anything before Velious,
        // `helperEraGate` becomes 1, Kael leaves `helperZones`, `helperEraRefused` becomes 1
        // and `helperEraLine` becomes 1 — and this test reddens on all four at once, which is
        // exactly the signal D5 wants rather than a silent change to a Founder's screen.
        Assert.Equal(0, app.DumpValue("helperEraGate"));
        Assert.Equal(0, app.DumpValue("helperEraRefused"));
        Assert.Equal(0, app.DumpValue("helperMaterialEraRefused"));
        // A gate that refused nothing must not draw a caption about refusing things.
        Assert.Equal(0, app.DumpValue("helperEraLine"));

        // The sweep's own cap is untouched by the gate — two caps, two numbers, no wiring.
        Assert.Equal(103, app.DumpValue("helperGearWithheld"));
        Assert.Equal(3, app.DumpValue("helperRecs"));
        Assert.Equal(3, app.DumpValue("helperGearWhy"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **REPLACE WITH BETTER IS A DIFFERENT QUESTION, AND THE ROOM SHOWS IT** (DRA-71 D6,
    /// plan P8; acceptance A5).
    ///
    /// <para><b>The same profile, the same dump and the SAME STORED PICK as the row above</b>
    /// — only the intent differs. That is the whole point: "upgrade what I wear" reads the
    /// picks and draws a picker; "replace with better" anchors on every worn slot and draws
    /// none. An assertion that changed the picks too could not tell the two apart.</para>
    ///
    /// <para><b>Prediction.</b> Both anchors sweep, so 16 upgrades survive two per-anchor caps
    /// and <b>225</b> are withheld. The NECK half is dominated by Western Wastes, which feeds
    /// eight of them — so the top row changes from Temple of Veeshan to Western Wastes, which
    /// is the intent difference visible in the answers rather than only in the controls.</para>
    /// </summary>
    [Fact]
    public void ReplaceWithBetterDropsThePickerAndSweepsEverySlot()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s =>
            {
                s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)];
                s.HelperWornPicks[key] = ["Cloth Cap"];
                s.HelperGearIntent[key] = nameof(GearIntent.ReplaceSlot);
            },
            environment: OpenOn("helper"));
        WearTwoPlainThings(app);
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperIntent", "replaceslot", "the stored intent to reach the strip");

        // NO picker — the stored pick is still there and this intent does not read it.
        Assert.Equal(0, app.DumpValue("helperWornChips"));
        Assert.Equal("ClothCap", app.DumpText("helperWornPicks"));
        Assert.Equal(3, app.DumpValue("helperIntentChips"));

        app.WaitForDump("helperZones", "WesternWastes,TempleofVeeshan,ClanRunnyeye",
            "every worn slot to sweep rather than only the picked one");
        Assert.Equal(225, app.DumpValue("helperGearWithheld"));
        Assert.Equal(3, app.DumpValue("helperGearWhy"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));

        // **DRA-84 D4: every drawn item line can say what drops it.** Eight lines across three
        // zones (Western Wastes and Temple of Veeshan name three apiece, Clan Runnyeye two),
        // and none of the sixteen upgrades is anonymous, so the who rule withholds nothing here
        // and the room draws no sentence about it. That zero is the PREDICTION and not a
        // shrug — the row below stages a fixture where it fires.
        Assert.Equal(8, app.DumpValue("helperWho"));
        Assert.Equal(0, app.DumpValue("helperWhoWithheld"));
        Assert.Equal(0, app.DumpValue("helperWhoLine"));
    }

    /// <summary>
    /// **THE WHO RULE FIRING, IN THE LAUNCHED APP, AGAINST THE REAL CATALOG** (DRA-84 D4, plan
    /// P3; Founder acceptance items 2 and 3).
    ///
    /// <para><b>Prediction, computed against the shipped catalog before the run</b> (trap 23).
    /// A warrior in AC-2 <c>Cloth Gloves</c> with the sweep on every slot has one anchor; 97
    /// catalog HANDS items beat it, the per-anchor cap keeps 8 and reports <b>89</b> withheld.
    /// Those eight land in eleven zone buckets — and <b>five of the eleven come from one
    /// record</b>, <c>Slime Blood of Cazic-Thule</c>, whose <c>DropZones</c> the promoter parsed
    /// out of a bulleted wiki line as <c>Plane of Fear&lt;br&gt;</c>, <c>:* Fright</c>,
    /// <c>:* Dread</c>, <c>:* Terror</c> and
    /// <c>:* Cazic Thule (God) (needs confirmation)</c>.</para>
    ///
    /// <para><b>Those five are the Founder's "junk camps" arriving by a second mechanism the
    /// plan did not foresee</b>, and the who rule removes all five — not because anyone taught
    /// it to recognise a broken zone name, but because a string that is not a place has no
    /// creature under it on the page either. <c>helperWhoWithheld</c> is <b>5</b>. The six real
    /// zones survive, the top three are drawn, and all six of their item lines name a creature.
    /// The promoter defect itself is filed for Fable — this slice does not parse wikitext.</para>
    ///
    /// <para><b>Both halves from one moment</b> (trap 56): the ENGINE's count
    /// (<c>helperWhoWithheld</c>) beside whether the ROOM said so (<c>helperWhoLine</c>). A rule
    /// that silently removed five camps would satisfy the first alone, which is the shape trap
    /// 50 exists to refuse.</para>
    /// </summary>
    [Fact]
    public void AnUpgradeNothingCanNameADropperForIsWithheldAndTheRoomSaysSo()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s =>
            {
                s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)];
                s.HelperGearIntent[key] = nameof(GearIntent.ReplaceSlot);
            },
            environment: OpenOn("helper"));
        app.WriteInventoryDump(("Hands", "Cloth Gloves", 1));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperWorn", "1", "the inventory dump to become one anchor");

        app.WaitForDump("helperZones", "TempleofVeeshan,KaelDrakkel,DragonNecropolis",
            "the who rule to drop the five phantom zones and rank the real ones");

        // The ENGINE's count, and the ROOM's sentence about it.
        Assert.Equal(5, app.DumpValue("helperWhoWithheld"));
        Assert.Equal(1, app.DumpValue("helperWhoLine"));

        // The sweep's own cap is a DIFFERENT number with a different cause, and folding the two
        // together is precisely what this slice refused to do.
        Assert.Equal(89, app.DumpValue("helperGearWithheld"));

        // Every drawn item line answers WHO — three rows, six lines, six creatures.
        Assert.Equal(3, app.DumpValue("helperRecs"));
        Assert.Equal(3, app.DumpValue("helperGearWhy"));
        Assert.Equal(6, app.DumpValue("helperWho"));
        // Nothing in the fixture log looted any of these, so the personal half stays silent and
        // every one of those six creatures came from the catalog.
        Assert.Equal(0, app.DumpValue("helperGearSeen"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **THE QUEST ACQUISITION PATH, IN THE LAUNCHED APP** (DRA-219, requirements S10/S11;
    /// acceptance S25 AC 1–6).
    ///
    /// <para>A warrior in an AC-4 <c>Cape of Underfoot</c> with the include-quests toggle ON.
    /// Before this slice that toggle bought a row headed with a quest's title and nothing else in
    /// it to act on: S11.2's *"never force the player to reverse-engineer the source"*, failing
    /// silently.</para>
    ///
    /// <para><b>A DIFFERENT ANCHOR FROM THE ROW ABOVE, and choosing it was the prediction doing
    /// its job</b> (trap 23). The first draft of this row reused D4's <c>Cloth Gloves</c>,
    /// predicted from a sweep with NO class — and in the launched app, which knows the character
    /// is a warrior, the class-lock filter removes every quest-sourced glove before the
    /// per-anchor cap is reached, so the screen has no quest row on it at all. The fixture moved,
    /// not the number. Re-predicted with <c>MyClasses = ["WAR"]</c>, which reproduces the app's
    /// counts exactly on the row above.</para>
    ///
    /// <para><b>Prediction, computed against the shipped catalogs before the run.</b> Three rows:
    /// Temple of Veeshan, then <c>Aid the Dar Brood</c> (Harla Dar, Western Wastes, from level 60,
    /// 1 turn-in item — Frakadar's Talisman) and <c>Deck of Spontaneous Generation Quest</c>
    /// (Ferjeneror, Plane of Mischief, from level 46). <b>Two quest rows, both naming a giver and
    /// a zone</b> — <c>helperQuestSource</c> — which is the pair that separates "a quest row
    /// exists" from "a quest row is a direction". The second of them carries NO component clause,
    /// because its page lists none: the trap-73 half of the feature, on screen.</para>
    ///
    /// <para><b>And the two refusals are different numbers with different causes, which is why
    /// they are separate keys.</b> <c>helperQuestWithheld</c> is <b>5</b>: five (item, quest)
    /// offers name quests the shipped quest list does not hold, so they could be given a title
    /// and nothing else. <c>helperNoSource</c> is <b>6</b>: catalog cloaks that beat this anchor
    /// and whose pages name no zone and no quest at all, dropped in silence until now.</para>
    ///
    /// <para><b>Each engine count is asserted beside whether the ROOM said it</b> (trap 56, and
    /// trap 50's rule that a surviving cap says so): a rule that removed five offers in silence
    /// satisfies the first assertion alone.</para>
    /// </summary>
    [Fact]
    public void QuestSourcedUpgradesAnswerTheSixQuestionsAndTheirRefusalsAreCountedApart()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s =>
            {
                s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)];
                s.HelperGearIntent[key] = nameof(GearIntent.ReplaceSlot);
                s.HelperGearQuests[key] = true;
            },
            environment: OpenOn("helper"));
        app.WriteInventoryDump(("Back", "Cape of Underfoot", 1));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperWorn", "1", "the inventory dump to become one anchor");
        app.WaitForDump("helperQuestRows", "2",
            "the include-quests toggle to put two quest rows on the screen");

        // Both quest rows answer WHO and WHERE from the shipped quest list. A row that could
        // only print its title is exactly what the rule below withholds.
        Assert.Equal(3, app.DumpValue("helperRecs"));
        Assert.Equal(2, app.DumpValue("helperQuestSource"));

        // The quest-source rule: the ENGINE's count beside the ROOM's sentence.
        Assert.Equal(5, app.DumpValue("helperQuestWithheld"));
        Assert.Equal(1, app.DumpValue("helperQuestLine"));

        // The sweep's sourceless count, same pair.
        Assert.Equal(6, app.DumpValue("helperNoSource"));
        Assert.Equal(1, app.DumpValue("helperNoSourceLine"));

        // With quests ON nothing is hidden BY the toggle, so that caption stays off the screen —
        // a sentence about a rule that did not run is furniture.
        Assert.Equal(0, app.DumpValue("helperQuestOnly"));
        Assert.Equal(0, app.DumpValue("helperQuestOnlyLine"));

        // **THE REGRESSION HALF** (S27). The drop path is untouched — a zone row survives with
        // its creature clause, every door opens something, and the who rule is silent here
        // because every page this anchor reaches names somebody.
        Assert.Equal(0, app.DumpValue("helperWhoWithheld"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **AND WITH THE TOGGLE OFF, THE ROOM SAYS WHAT THE TOGGLE IS HIDING** (DRA-219, S10.1 —
    /// *"do not restrict recommendations to direct creature drops"*).
    ///
    /// <para>This is the DEFAULT state of the room, which is what makes it worth its own row:
    /// <c>HelperGearQuests</c> is absent unless a player turns it on, so every character starts
    /// here. <b>Predicted at 5</b> — five catalog cloaks beat this anchor and come only from a
    /// quest, and until this slice the toggle removed all five with nothing on screen saying a
    /// control had done it.</para>
    ///
    /// <para>The pair with the row above is the point: same anchor, same catalog, ONE setting,
    /// and the two quest counts trade places — <c>helperQuestWithheld</c> 5 → 0 because no quest
    /// offer reaches a bucket at all, and <c>helperQuestOnly</c> 0 → 5. <b>The sourceless count
    /// does not move</b>, which is the evidence that it is a different fact rather than the same
    /// one counted twice.</para>
    /// </summary>
    [Fact]
    public void WithQuestsOffTheRoomCountsTheUpgradesTheToggleIsHiding()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s =>
            {
                s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)];
                s.HelperGearIntent[key] = nameof(GearIntent.ReplaceSlot);
            },
            environment: OpenOn("helper"));
        app.WriteInventoryDump(("Back", "Cape of Underfoot", 1));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperQuestOnly", "5",
            "the sweep to count the quest-only upgrades the toggle is hiding");

        Assert.Equal(1, app.DumpValue("helperQuestOnlyLine"));
        // No quest offer reached a bucket, so the rule that prunes them has nothing to say.
        Assert.Equal(0, app.DumpValue("helperQuestRows"));
        Assert.Equal(0, app.DumpValue("helperQuestWithheld"));
        Assert.Equal(0, app.DumpValue("helperQuestLine"));
        // …and the sourceless count is the SAME 6 as with the toggle on, because it is not about
        // the toggle. Two numbers that move independently is why they are two numbers.
        Assert.Equal(6, app.DumpValue("helperNoSource"));
    }

    /// <summary>
    /// **"FARM TO SELL" ANSWERS, AND IT ANSWERS A DIFFERENT QUESTION FROM THE OTHER TWO**
    /// (DRA-71 D7, plan P9; the Founder's smoke item 4c).
    ///
    /// <para>In D6 this row asserted a DEFERRAL — the intent said it was not ranked yet and
    /// pointed at Progress → Wealth. Its engine landed in D7 and the assertion is inverted, but
    /// the thing it protects is the same and is now sharper: <b>the sell question must never
    /// wear the gear question's clothes.</b> This profile is WEARING two things and the catalog
    /// is full of items that beat them — the two rows above prove that, from the same fixture —
    /// so a single <c>helperGearWhy</c> here would mean the sell intent had been routed through
    /// a dominance comparison it has no anchor for, which is the one claim
    /// <c>GearUpgrades</c> exists to refuse.</para>
    ///
    /// <para>The controls that belong to the SWEEP go with it: no worn picker, and no
    /// include-quests toggle, because a quest reward is not a thing you farm to sell and a
    /// control that changed nothing would be an affordance with no effect.</para>
    /// </summary>
    [Fact]
    public void TheFarmToSellIntentAnswersFromYourOwnLootRatherThanFromTheCatalog()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s =>
            {
                s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)];
                s.HelperGearIntent[key] = nameof(GearIntent.FarmToSell);
            },
            environment: OpenOn("helper"));
        WearTwoPlainThings(app);
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperIntent", "farmtosell", "the stored intent to reach the strip");

        // The fixture log's own kills and its own vendor sales are the whole input, so the
        // intent produces real answers with no dump staged for it.
        app.WaitForDump("helperSellable", "1", "the sell engine to price a drop off your sales");
        Assert.True(app.DumpValue("helperRecs") >= 1,
            $"the sell intent ranked nothing; dump was: {app.Artifacts()}");
        Assert.True(app.DumpValue("helperSales") >= 1,
            $"the sale probe read nothing; dump was: {app.Artifacts()}");
        Assert.Equal(1, app.DumpValue("helperMoneyNote"));

        // **THE ASSERTION THAT MATTERS.** Two worn anchors are staged and the catalog beats
        // both; not one upgrade line may appear under this intent.
        Assert.Equal(0, app.DumpValue("helperGearWhy"));
        Assert.Equal(0, app.DumpValue("helperGearWithheld"));
        Assert.Equal(2, app.DumpValue("helperWorn"));

        // The sweep's controls went with the sweep.
        Assert.Equal(0, app.DumpValue("helperQuestToggle"));
        Assert.Equal(0, app.DumpValue("helperWornChips"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **NO INVENTORY DUMP IS ITS OWN STATE, AND IT SHIPS THE COMMAND** (DRA-71 D6).
    ///
    /// <para>"EQBuddy has not been told what you are wearing" and "nothing in the catalog beats
    /// it" both draw one grey sentence, and only the first has a command behind it. A surface
    /// that needs an in-game command SHIPS the command (David, 2026-08-14), and only a launched
    /// app can say the ⧉ button is actually there — an absent control photographs as an
    /// unremarkable panel (trap 29).</para>
    /// </summary>
    [Fact]
    public void WithNoInventoryDumpTheHelperAsksForOneAndHandsOverTheCommand()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s => s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)],
            environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperWorn", "0", "a profile that has never written an inventory dump");

        Assert.Equal(0, app.DumpValue("helperWornChips"));
        Assert.Equal(0, app.DumpValue("helperRecs"));
        Assert.Equal(1, app.DumpValue("helperGaps"));
        // TWO copy buttons: the picker's own empty state and the gap under the answers. Both
        // are the same constant off GameCommands, which is what GameCommandsTests asserts from
        // the other side.
        Assert.True(app.DumpValue("helperCopyCmd") >= 1,
            $"the /outputfile inventory button never reached the screen; dump was: {app.Artifacts()}");
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    // ================================================================================
    // DRA-71 D8 — the professions block, and the skill value that used to die at midnight
    // ================================================================================

    /// <summary>
    /// **THE PROFESSIONS BLOCK DRAWS ALL EIGHT, AND EVERY ROW HAS ITS TWO DOORS**
    /// (DRA-71 D8, plan P13; Founder smoke item 6).
    ///
    /// <para>Nothing is picked, so the filter's empty state is "all of them" — which is the
    /// half a source scan cannot check: <c>helperProfChips</c> is what the PICKER holds and
    /// <c>helperProfRows</c> is what the room actually built under it, and a room that read
    /// the store correctly and drew nothing satisfies only the first (trap 72's shape, trap
    /// 56's discipline).</para>
    ///
    /// <para><c>helperWatchPresets</c> is a trap-29 assertion: the watch control is the one
    /// affordance in this block that DOES something, and an absent control photographs as an
    /// unremarkable row of text. <c>helperWatched</c> is 0 on a fresh profile — the state is
    /// read from the player's own rules, so it can only be 0 before anything wrote one.</para>
    /// </summary>
    [Fact]
    public void TheHelperListsAllEightProfessionsWithAWatchPresetAndAWikiDoorOnEach()
    {
        using var app = new AppHarness(environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperProfRows", 8, "the professions block to draw the curated eight");

        Assert.Equal(8, app.DumpValue("helperProfChips"));
        Assert.Equal("Anyprofession", app.DumpText("helperProfFace"));
        // A fresh profile has never had a skill-up read into it, so every row is in its
        // unknown state — which is a sentence rather than a zero (HelperPresentationTests).
        Assert.Equal(0, app.DumpValue("helperSkills"));
        Assert.Equal(0, app.DumpValue("helperProfKnown"));
        // One per row, and none of them is already watching.
        Assert.Equal(8, app.DumpValue("helperWatchPresets"));
        Assert.Equal(0, app.DumpValue("helperWatched"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **THE FOUNDER'S OWN DUMP, THROUGH THE REAL APP, WITH THE NUMBERS THE CHECKLIST PREDICTS**
    /// (DRA-149 D5, plan P6).
    ///
    /// <para>The unit half of this lives in <c>FounderResmokeTests</c> and runs the engine
    /// directly. This is the other claim: that those numbers survive the app — the tail, the
    /// inventory watcher, the settings store, the room's own paint. "The engine computed it" and
    /// "the screen shows it" are different claims (trap 56), and the screen is the one he
    /// failed.</para>
    ///
    /// <para><b>It asserts RELATIONSHIPS, never the screen</b> (plan P6): the anchors, the unread
    /// count, the candidate count, the band refusals and the who withholdings, dumped from ONE
    /// moment, with the pair that tells the two empty gear screens apart at the centre of it.
    /// <c>helperCandidates</c> is large and <c>helperGearWhy</c> is small — the gate removing
    /// places, not the sweep failing to find any.</para>
    ///
    /// <para>Level 29 is the Founder's own, from the failing build, so the prediction is about
    /// the screen he will actually open.</para>
    /// </summary>
    [Fact]
    public void TheFoundersOwnDumpReachesTheHelperWithCandidatesAndRefusals()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s => s.HelperGoals[key] = [nameof(HelperGoal.FarmGear)],
            environment: OpenOn("helper"));
        app.WriteInventoryDumpFrom("dranak.txt");
        app.SeedQuestLedger(statedLevel: (29, DateTime.Now.AddHours(-1)));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        // TWENTY-ONE, not twenty: the room counts every worn row, and the AMMO anchor is one of
        // them. `GearUpgradesFixtureSweepTests` says 20 because it excludes AMMO deliberately —
        // that is the one anchor that ALREADY worked before D1, and a floor met by it would be
        // met by the thing that was never broken. Two numbers, two questions, both right.
        app.WaitForDump("helperWorn", "21", "the Founder's readable worn rows");
        app.WaitForDump("helperLevel", "29", "the seeded statement to be what the Helper ranks with");

        // The gate is LIVE. A zero refusal count below is otherwise indistinguishable from a
        // gate that never ran, which is exactly how this test first went green while `Bands`
        // was null (trap 78).
        Assert.Equal(1, app.DumpValue("helperBandGate"));

        // **FAIL 1.** Every row he is wearing is readable now — the bow included, via D2's
        // curated alias — so the unread caption is correctly ABSENT. Predicting the blank is
        // what stops it reading as a missing feature.
        Assert.Equal(0, app.DumpValue("helperUnreadWorn"));
        Assert.Equal(0, app.DumpValue("helperUnreadLine"));

        // **FAIL 2, and this is the pair.** The sweep found plenty; the gate removed places.
        // Before D1 the first number was 0 for all 19 gear anchors and the screen looked the
        // same as a refusal.
        var candidates = app.DumpValue("helperCandidates");
        var refused = app.DumpValue("helperBandRefused");
        Assert.True(candidates >= 50, $"only {candidates} candidates for the Founder's dump");
        Assert.True(refused >= 10, $"only {refused} zones refused at level 29");
        Assert.True(candidates > refused);

        // …and the ROOM said so, with rows left over. A gate that emptied the list would be a
        // different screen and a different sentence.
        Assert.Equal(1, app.DumpValue("helperBandLine"));
        Assert.True(app.DumpValue("helperGearWhy") > 0, "the gate refused everything");
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **THE VENDOR LINES ARE ON THE SCREEN, AND THE CAP IS WHY THERE ARE NOT MORE**
    /// (DRA-149 D4, plan P5; the Founder's FAIL item 3, second half).
    ///
    /// <para>Three numbers from ONE moment (trap 56), and the assertion is the RELATIONSHIP
    /// rather than any of them: <c>helperMerchantLines</c> is what the shipped catalog holds,
    /// <c>helperMerchantRows</c> is what the room drew, and the second is bounded by the cap
    /// times the eight rows above it. A room that read the catalog perfectly and drew nothing
    /// satisfies the first two and fails the third — which is the professions block's own
    /// <c>helperProfChips</c>/<c>helperProfRows</c> discipline, one sub-list in.</para>
    ///
    /// <para>The drop half's rows need a played session to rank; these need none, because they
    /// are the catalog's and a player who has never logged in can still be told where a shop
    /// is. That is the whole reason the vendor half is a different surface from D3's.</para>
    /// </summary>
    [Fact]
    public void TheProfessionsBlockDrawsTheWikisVendorLinesUnderEachTrade()
    {
        using var app = new AppHarness(environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperProfRows", 8, "the professions block to draw the curated eight");

        var catalogLines = app.DumpValue("helperMerchantLines");
        var catalogZones = app.DumpValue("helperMerchantZones");
        var drawn = app.DumpValue("helperMerchantRows");

        // The shipped file is not the empty one LoadEmbedded answers with when the resource is
        // missing — a launched app is the only place that can say so about the packaged build.
        Assert.True(catalogZones >= 40, $"the packaged catalog has only {catalogZones} zones");
        Assert.True(catalogLines >= 300, $"the packaged catalog has only {catalogLines} lines");

        // Drawn, capped, and fewer than the catalog holds — the cap doing its job rather than an
        // empty fold, which is the pair a single number could not tell apart.
        Assert.True(drawn > 0, "the professions block drew no vendor line");
        Assert.True(drawn <= 8 * 3, $"{drawn} rows is more than eight professions capped at three");
        Assert.True(drawn < catalogLines);
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **A SKILL-UP IN THE LOG BECOMES A STANDING ON THE SCREEN** — the writer and the reader
    /// in one assertion (trap 20), from a launched app.
    ///
    /// <para>This is the slice's whole point. <c>StatsSnapshot.SkillUps</c> has always known
    /// what you raised tonight and nothing has ever remembered it, so the value died with the
    /// session. The line goes into the real log through the real tail, the real parser and the
    /// real ledger — a fixture-shaped substitute would render a state that is real and is not
    /// the one this assertion is about (trap 23).</para>
    ///
    /// <para><b>The combat skill beside it is the negative, and it is asserted at a moment
    /// when it is meaningful</b> (trap 62). Both lines are appended together and the wait is
    /// for a POSITIVE event the profession line causes — the standing appearing — so
    /// "1H Slashing was not stored" is checked after the batch has demonstrably been
    /// processed, rather than before the tail got to it.</para>
    ///
    /// <para><c>helperSkills</c> is the STORE's claim and <c>helperProfKnown</c> is the
    /// SCREEN's, captured in one Build: a skill-up that reached quest-ledger.json and no row
    /// would satisfy the first and be the bug (trap 56).</para>
    /// </summary>
    [Fact]
    public void ASkillUpInTheLogBecomesAPersistedProfessionStanding()
    {
        using var app = new AppHarness(environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperSkills", 0, "a profile with no stored profession standing");

        app.AppendLogLines(
            "You have become better at Blacksmithing! (122)",
            "You have become better at 1H Slashing! (53)");

        app.WaitForDump("helperSkills", 1, "the profession skill-up to reach the ledger");
        // Exactly one: the ledger admits professions and refuses the sixty combat skills
        // nothing reads yet, which is what keeps the profile file profession-sized.
        Assert.Equal(1, app.DumpValue("helperSkills"));
        // And the SCREEN drew it. One of the eight rows now carries a number.
        Assert.Equal(1, app.DumpValue("helperProfKnown"));
        Assert.Equal(8, app.DumpValue("helperProfRows"));
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }

    /// <summary>
    /// **The pick narrows the list, and it is per character** (DRA-71 D8).
    ///
    /// <para>Seeded through <c>configureSettings</c> under the ledger's own character key, so
    /// the assertion covers the read path a restart takes. Two rows rather than eight is the
    /// only observable difference a filter makes, and <c>helperProfChips</c> staying at eight
    /// is the other half: <b>the OFFER is never narrowed by its own filter</b>, or a player
    /// could not undo a pick.</para>
    /// </summary>
    [Fact]
    public void PickingTwoProfessionsNarrowsTheListAndLeavesTheOfferWhole()
    {
        var key = $"{AppHarness.Character}_{AppHarness.Server}".ToLowerInvariant();
        using var app = new AppHarness(
            configureSettings: s =>
            {
                s.HelperGoals[key] = [nameof(HelperGoal.FarmMaterials)];
                s.HelperProfessions[key] =
                    [nameof(Tradeskill.Baking), nameof(Tradeskill.Pottery)];
            },
            environment: OpenOn("helper"));
        app.Launch();

        app.WaitForDump("shellPage", "helper", "the shell to land on the Helper room");
        app.WaitForDump("helperProfRows", 2, "the stored profession pick to be read back");

        Assert.Equal(8, app.DumpValue("helperProfChips"));
        Assert.Equal("Baking·Pottery", app.DumpText("helperProfFace"));
        Assert.Equal(2, app.DumpValue("helperWatchPresets"));

        // **AND THE GOAL NOW ANSWERS** (DRA-149 D3). This row used to assert
        // `helperNotYet == 1` — the ranking was PARKED on a survey of the wrong column — and
        // the flip is the player-visible half of this slice: the block above is still full of
        // the player's own professions, and under it there are now camps. Both, from one Build.
        Assert.Equal(0, app.DumpValue("helperNotYet"));
        app.WaitForDumpAtLeast("helperMaterialWhy", 1,
            "the picked professions to produce a materials row");

        // **THE PICK REACHED THE ENGINE, not just the picker.** Two rows in the block is a
        // claim about a LIST; this is the claim that the SAME store narrowed what was RANKED,
        // which is the whole of why the pick rides HelperInputs rather than stopping at the
        // room (trap 4). Two numbers from one moment, and they are different claims (trap 56).
        Assert.Equal("Baking,Pottery", app.DumpText("helperProfessions"));
        // Every materials line names a creature — the who rule reaching the shipped catalog,
        // which is the half the Founder failed the gear rows for.
        Assert.True(app.DumpValue("helperMaterialNamed") > 0,
            $"a materials row drew a line with nobody to kill; dump was: {app.Artifacts()}");
        Assert.Equal(0, app.DumpValue("helperDeadDoors"));
    }
}
