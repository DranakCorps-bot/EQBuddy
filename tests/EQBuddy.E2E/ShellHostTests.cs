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
        Assert.Equal(4, app.DumpValue("shellHomeBlocks"));
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
    // inside it (`ShellPages.Rooms(Home)` is empty — four blocks on one page IS the room),
    // so there is no tab key to land on and `shellPage` is what there is to check.
    [InlineData("home", "shellPage", "home")]
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
    /// The four blocks Bevel's door 1 locks, and the deep-links block's own refusal to
    /// offer a room that does not exist.
    ///
    /// **`shellHomeDeadLinks` is the row with teeth, and it is the rail's rule one level
    /// in.** The rail cannot draw a row for an unlanded room because `BuildRail` filters
    /// `ShellPages.Landed` — but Home's body is a SECOND navigation surface, inside a room,
    /// where the rail's guard cannot see it. A hand-written link list with a "Live" row on
    /// it would compile, render, photograph perfectly and open nothing, which is the exact
    /// shape this codebase already ruled on (*"an empty class row gets no chevron — an
    /// affordance that opens nothing is a trap"*). The count comes from `ShellPages` on
    /// both sides, so the day Live lands it is offered without anyone editing this test.
    ///
    /// **A floor before the equalities**, per trap 39: a room that rendered nothing at all
    /// would report zero links and zero dead links and agree with a naive assertion
    /// perfectly.
    /// </summary>
    [Fact]
    public void TheHomeRoomDrawsFourBlocksAndOffersNoLinkThatOpensNothing()
    {
        using var app = new AppHarness(environment: OpenOn("home"));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to land on the Home room");
        Assert.Equal(4, app.DumpValue("shellHomeBlocks"));
        // The room-level empty is for a profile with NO character; this one is following.
        Assert.Equal(0, app.DumpValue("shellHomeEmpty"));
        Assert.Equal(1, app.DumpValue("shellHomeIdentity"));

        var expected = ShellPages.RailOrder.Count(page =>
            ShellPages.Landed.Contains(page)
            && page != ShellPage.Home
            && !ShellPages.BelowTheGap(page));
        Assert.True(app.DumpValue("shellHomeLinks") >= 1,
            $"the Home room offered no deep links at all; dump was: {app.Artifacts()}");
        Assert.Equal(expected, app.DumpValue("shellHomeLinks"));
        Assert.Equal(0, app.DumpValue("shellHomeDeadLinks"));
    }

    /// <summary>
    /// Readiness, and the ⧉ copies that are the whole point of its empty rows.
    ///
    /// **Only a launched app can say a control EXISTS.** A surface that asks the player for
    /// an output file and hands them no way to run it is the defect David reported on
    /// 2026-08-20, it is worst in the empty state (the only state a new player sees), and an
    /// absent control photographs as an unremarkable panel (trap 29).
    /// `GameCommandsTests.SurfacesNeedingACommand` proves this file NAMES the three
    /// commands; this proves the buttons are on screen — and that they go away for a dump
    /// that has actually landed, which is the half a "greater than zero" assertion could
    /// never see.
    ///
    /// The inventory dump is staged in the game's own tab-separated shape through the
    /// harness, so it goes through the real finder and the real parser (trap 23).
    /// </summary>
    [Fact]
    public void ReadinessAsksForTheDumpsThatAreMissingAndStopsAskingForTheOneThatLanded()
    {
        using var app = new AppHarness(environment: OpenOn("home"));
        app.WriteInventoryDump(("General1", "Bone Chips", 12));
        app.Launch();

        app.WaitForDump("shellPage", "home", "the shell to land on the Home room");
        // Three dumps reported on: bags, achievements, factions.
        Assert.Equal(3, app.DumpValue("shellHomeReadiness"));
        // Bags landed; the other two never have. **The equality is the assertion** — a room
        // that had silently lost the affordance entirely would report 0 waiting and 0
        // buttons and pass a "some are missing" check.
        Assert.True(app.DumpValue("shellHomeReadinessWaiting") == 2,
            $"the staged inventory dump was not seen; dump was: {app.Artifacts()}");
        Assert.Equal(2, app.DumpValue("shellHomeCopyCmd"));
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
}
