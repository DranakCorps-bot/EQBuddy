using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The Evolved shell's navigation and layout decisions, which live in `UI.Shared` for the
/// reason every window sum in this repo does: the WPF layer has no unit tests, so a
/// threshold or a room list left inline is one nothing can check.
///
/// The rows here are not a description of the code — they are the four things Helm's
/// E-3 sign and Bevel's signed pre-design actually require, each written so it FAILS if
/// the requirement stops being met:
///
///  1. One navigation address grammar, and unknown addresses refuse rather than default.
///  2. No rail row for a room that has not landed.
///  3. Two degrade axes with DIFFERENT thresholds, and a floor.
///  4. `ShellPage` is the single source the phone's screen registry also reads.
/// </summary>
public class ShellNavigationTests
{
    // ---- 1. one address grammar ------------------------------------------------

    [Theory]
    [InlineData("progress", ShellPage.Progress, null)]
    [InlineData("progress:raids", ShellPage.Progress, "raids")]
    [InlineData("  Progress:Wealth  ", ShellPage.Progress, "Wealth")]
    [InlineData("settings", ShellPage.Settings, null)]
    public void AnAddressParsesIntoAPageAndAnOptionalRoom(
        string address, ShellPage page, string? room)
    {
        var parsed = ShellPages.ParseAddress(address);
        Assert.NotNull(parsed);
        Assert.Equal(page, parsed!.Value.Page);
        Assert.Equal(room, parsed.Value.Room);
    }

    /// <summary>Unknown pages answer null. Snapping to a default is how a caller lands
    /// somewhere it did not ask for, silently — the refusal ProgressWindow.SetTab already
    /// makes, one level up.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nowhere")]
    [InlineData("nowhere:raids")]
    [InlineData(null)]
    public void AnUnknownAddressResolvesToNothing(string? address) =>
        Assert.Null(ShellPages.ParseAddress(address));

    /// <summary>Round-trip: every page's address parses back to that page, so the rail,
    /// the palette and the env hook cannot be spelling it three ways.</summary>
    [Fact]
    public void EveryPageRoundTripsThroughItsAddress()
    {
        foreach (var page in ShellPages.RailOrder)
        {
            Assert.Equal((page, (string?)null), ShellPages.ParseAddress(ShellPages.Address(page)));
            Assert.Equal((page, (string?)"raids"),
                ShellPages.ParseAddress(ShellPages.Address(page, "raids")));
        }
    }

    // ---- 2. no dead rail rows --------------------------------------------------

    /// <summary>**Every landed room is a real page, and the rail order holds all of
    /// them.** The failure this catches is a room shipping without joining the rail, or a
    /// rail row for a room that does not exist — the second of which is the disabled-row
    /// shape the pre-design refused: *"an affordance that opens nothing is a trap."*
    /// </summary>
    [Fact]
    public void EveryLandedRoomHasARailPositionAndNothingIsListedTwice()
    {
        Assert.Equal(ShellPages.RailOrder.Distinct().Count(), ShellPages.RailOrder.Count);
        Assert.Equal(ShellPages.Landed.Distinct().Count(), ShellPages.Landed.Count);
        Assert.All(ShellPages.Landed, p => Assert.Contains(p, ShellPages.RailOrder));
        Assert.Equal(Enum.GetValues<ShellPage>().Length, ShellPages.RailOrder.Count);
    }

    /// <summary>E-3 PR 1 landed one room; PR 2 landed two more; PR 3 landed Quests; PR 4
    /// landed Home, at the TOP.
    /// **This row is meant to be edited** — a room joins `Landed` in the PR that lands it,
    /// and this assertion is the reminder that doing so is a deliberate act rather than a
    /// line someone slid in. It asserts the ORDER too, because `Landed` is filtered through
    /// `RailOrder` when the rail is drawn and a list that agreed on membership while
    /// disagreeing on order would be a rail nobody could predict from this file.
    ///
    /// **Quests sits between Gear and World here, and that is the whole assertion about
    /// rail position.** `BuildRail` walks `RailOrder` filtering by this list, so a room
    /// joining inserts itself in the fixed order automatically — correct by construction,
    /// and invisible if it silently is not. A build that appended new rooms at the bottom
    /// would look identical in every way except this line and the `shell-quests`
    /// screenshot, which is why the pre-design asked for both.
    ///
    /// **Home is FIRST here, and it did not have to be arranged.** `RailOrder` has had Home
    /// at the top since PR 1, so the room joining `Landed` put its row above Progress on its
    /// own — which is the property this assertion exists to prove rather than assume.</summary>
    [Fact]
    public void FiveRoomsHaveLandedSoFar() =>
        Assert.Equal(
            [ShellPage.Home, ShellPage.Progress, ShellPage.Gear, ShellPage.Quests, ShellPage.World],
            ShellPages.Landed);

    /// <summary>The two that have NOT landed, named — because "which rooms are missing"
    /// is the question a reader of `Landed` actually has, and a positive list cannot
    /// answer it. Each is held back by a decision rather than by effort: Live does not
    /// exist yet (and gets its own Bevel pass rather than riding another room's PR, per the
    /// Helm-signed Quests → Home → Live order), and Settings is a room whose whole job is
    /// not being a launcher.</summary>
    [Theory]
    [InlineData(ShellPage.Live)]
    [InlineData(ShellPage.Settings)]
    public void TheRoomsThatHaveNotLandedDrawNoRailRow(ShellPage page) =>
        Assert.DoesNotContain(page, ShellPages.Landed);

    /// <summary>Settings is the one page under the rail's gap, at every width.</summary>
    [Fact]
    public void SettingsIsTheOnlyPageBelowTheGap() =>
        Assert.Equal([ShellPage.Settings],
            ShellPages.RailOrder.Where(ShellPages.BelowTheGap).ToArray());

    /// <summary>Every page has a label, a description and an icon the table actually
    /// holds. A rail row asking IconPaths for a name it does not have would throw at the
    /// moment the rail is built, which is a launch, not a test — and an icon that renders
    /// as nothing photographs as an unremarkable gap (trap 29).</summary>
    [Fact]
    public void EveryPageHasWordsAndARealIcon()
    {
        foreach (var page in ShellPages.RailOrder)
        {
            Assert.NotEmpty(ShellPages.Key(page));
            Assert.NotEmpty(ShellPages.Label(page));
            Assert.NotEmpty(ShellPages.Describe(page));
            Assert.NotNull(IconPaths.Path(ShellPages.IconName(page)));
        }
    }

    // ---- 2b. the rooms INSIDE a room, spelled once ------------------------------

    /// <summary>
    /// **Every room the shell can address round-trips through the SURFACE's own key
    /// table.** This is the assertion behind `ShellPages.Rooms` mapping rather than
    /// translating, and the failure it catches is a shell that invents a second spelling
    /// for one destination — `world:path` beside the surface's `world:travel` — which is
    /// trap 33 lifted from data into navigation.
    ///
    /// It matters because the real keys look like typos and are not:
    /// `ProgressSurface.KeyFor(Experience)` is `"progress"` (the card key five folded
    /// surfaces collapsed into), `WorldTab.Travels` is `"misc"` (the old card's settings
    /// key, kept so the fold needed no migration at all), and `WorldTab.Routes` is
    /// `"travel"` while its label reads "Path". Every one of those is load-bearing
    /// history that a well-meaning rename would destroy silently.
    /// </summary>
    [Fact]
    public void EveryRoomKeyResolvesBackThroughItsOwnSurface()
    {
        foreach (var page in ShellPages.Landed)
        {
            var rooms = ShellPages.Rooms(page);
            // Home is the exception, and it is a real one rather than a gap: four blocks on
            // one page IS the room, so there is nothing for an address's room half to name.
            // Asserted rather than skipped silently — a landed room that lost its tabs by
            // accident would otherwise slip through this loop the same way.
            if (page == ShellPage.Home) { Assert.Empty(rooms); continue; }
            Assert.NotEmpty(rooms);
            foreach (var (label, key) in rooms)
            {
                Assert.NotEmpty(label);
                Assert.NotEmpty(key);
                var resolved = page switch
                {
                    ShellPage.Progress => ProgressSurface.TabForKey(key) is { } t
                        ? ProgressSurface.KeyFor(t) : null,
                    ShellPage.Gear => LootSurface.TabForKey(key) is { } t
                        ? LootSurface.KeyFor(t) : null,
                    ShellPage.Quests => QuestSurface.TabForKey(key) is { } t
                        ? QuestSurface.KeyFor(t) : null,
                    ShellPage.World => WorldSurface.TabForKey(key) is { } t
                        ? WorldSurface.KeyFor(t) : null,
                    _ => null,
                };
                Assert.Equal(key, resolved);
                // And the whole address parses, which is what the rail, the palette and
                // EQBUDDY_SHELL all hand to one Navigate().
                Assert.Equal((page, (string?)key),
                    ShellPages.ParseAddress(ShellPages.Address(page, key)));
            }
        }
    }

    /// <summary>The negative that keeps the row above from going vacuous (trap 39): a
    /// page with no room list answers empty rather than answering something.</summary>
    [Fact]
    public void APageWithNoRoomsInsideItAnswersEmpty() =>
        Assert.Empty(ShellPages.Rooms(ShellPage.Settings));

    /// <summary>The room list is the surface's, not a copy of it — asserted against the
    /// COUNT each Core definition reports, so a room added to a surface reaches the
    /// palette without anyone editing the shell.</summary>
    [Fact]
    public void TheRoomListsComeFromTheSurfacesThemselves()
    {
        Assert.Equal(ProgressSurface.Tabs().Count, ShellPages.Rooms(ShellPage.Progress).Count);
        Assert.Equal(LootSurface.Tabs().Count, ShellPages.Rooms(ShellPage.Gear).Count);
        Assert.Equal(QuestSurface.Tabs().Count, ShellPages.Rooms(ShellPage.Quests).Count);
        Assert.Equal(WorldSurface.Tabs().Count, ShellPages.Rooms(ShellPage.World).Count);
    }

    // ---- 2c. two hosts, one flat dump namespace --------------------------------

    /// <summary>
    /// **The re-key that stops two hosts writing over each other in the dump.** The shell
    /// asks `MapView` for the same string `WorldWindow` asks it for, so the two can never
    /// report different facts — and then renames the keys, because the dump is one flat
    /// namespace and with both windows open the later writer would silently win.
    /// </summary>
    [Theory]
    [InlineData("shellWorld", "mapZones=4", "shellWorldMapZones=4")]
    [InlineData("shellWorld", "a=1 b=2", "shellWorldA=1 shellWorldB=2")]
    [InlineData("shellWorld", "spawnsRows=0 spawnsFollow=1",
        "shellWorldSpawnsRows=0 shellWorldSpawnsFollow=1")]
    // **The convention is `shell` + THE VIEW'S OWN KEY, and the two spellings above and
    // below both mean that.** `MapView` reports `mapZones`, which carries no room name, so
    // the World room passes `shellWorld`. `QuestsView` reports `questsTab`, which already
    // does, so the Quests room passes `shell` — and passing `shellQuests` produced
    // `shellQuestsQuestsTab`, which the E2E suite caught on its first run. Pinned here so
    // the next room with a self-naming view does not rediscover it the same way.
    [InlineData("shell", "questsTab=general questsRows=21",
        "shellQuestsTab=general shellQuestsRows=21")]
    public void FactsAreRekeyedUnderTheHostsPrefix(string prefix, string facts, string expected) =>
        Assert.Equal(expected, ShellDumpFacts.Prefixed(prefix, facts));

    /// <summary>A token that is not a `key=value` pair is passed through untouched. The
    /// dump has no such token today; a reader that silently renamed something it did not
    /// understand would be the more expensive of the two mistakes.</summary>
    [Fact]
    public void ATokenThatIsNotAPairIsLeftAlone() =>
        Assert.Equal("shellWorldA=1 loose", ShellDumpFacts.Prefixed("shellWorld", "a=1 loose"));

    [Fact]
    public void AnEmptyFactStringStaysEmpty() =>
        Assert.Equal("", ShellDumpFacts.Prefixed("shellWorld", ""));

    // ---- 3. two degrade axes, and a floor --------------------------------------

    /// <summary>**The two axes have different thresholds, and that is the point.**
    /// Conflating them is how a resize bug hides: the rail can have plenty of room for
    /// labels while the room under it is already too narrow to split, and vice versa. A
    /// single threshold would make this assertion impossible to write.</summary>
    [Fact]
    public void TheRailAndTheRoomDegradeAtDifferentWidths() =>
        Assert.NotEqual(ShellLayoutPolicy.SplitRoomWidth, ShellLayoutPolicy.RailLabelWidth);

    [Fact]
    public void AWideWindowShowsLabelsAndSplitsTheRoom()
    {
        var wide = ShellLayoutPolicy.For(1200);
        Assert.True(wide.RailLabelsVisible);
        Assert.Equal(DesignTokens.RailWidthExpanded, wide.RailWidth);
        Assert.False(wide.RoomSinglePane);
    }

    [Fact]
    public void AtTheFloorTheRailIsIconsOnlyAndTheRoomIsOnePane()
    {
        var floor = ShellLayoutPolicy.For(ShellLayoutPolicy.MinWidth);
        Assert.False(floor.RailLabelsVisible);
        Assert.Equal(DesignTokens.RailWidthCollapsed, floor.RailWidth);
        Assert.True(floor.RoomSinglePane);
    }

    /// <summary>The threshold is inclusive on the labelled side and exclusive one unit
    /// below it — the boundary is where a resize bug lives, so it is asserted rather than
    /// implied by the two cases above.</summary>
    [Fact]
    public void TheRailLabelThresholdIsExactAtItsOwnWidth()
    {
        Assert.True(ShellLayoutPolicy.For(ShellLayoutPolicy.RailLabelWidth).RailLabelsVisible);
        Assert.False(ShellLayoutPolicy.For(ShellLayoutPolicy.RailLabelWidth - 1).RailLabelsVisible);
    }

    /// <summary>
    /// Axis 2's boundary, asserted exactly — and it was worth writing the day the axis got
    /// its first consumer (E-3 PR 3's Quests room), because until then
    /// `RoomSinglePane` was a formula nothing exercised.
    ///
    /// **Derived from the two constants rather than typed**, so it follows a change to
    /// either. The room's share is what is left after the rail, which is exactly the
    /// arithmetic a room measuring itself would get wrong — and the boundary is where a
    /// resize bug lives, so it is asserted rather than implied by a wide case and a floor
    /// case.
    /// </summary>
    [Fact]
    public void TheRoomSplitThresholdIsExactAtItsOwnWidth()
    {
        // Wide enough to leave the room exactly SplitRoomWidth once the rail has taken its
        // share. One unit narrower and the room can no longer hold two panes.
        var atThreshold = ShellLayoutPolicy.SplitRoomWidth + DesignTokens.RailWidthExpanded;
        Assert.False(ShellLayoutPolicy.For(atThreshold).RoomSinglePane);
        Assert.True(ShellLayoutPolicy.For(atThreshold - 1).RoomSinglePane);
        // And the rail is expanded on BOTH sides of it, which is what makes this a test of
        // one axis rather than of two moving together — the whole reason the thresholds
        // are separate numbers.
        Assert.True(ShellLayoutPolicy.For(atThreshold).RailLabelsVisible);
        Assert.True(ShellLayoutPolicy.For(atThreshold - 1).RailLabelsVisible);
    }

    /// <summary>A layout pass can run before the window has a measured size. It must
    /// produce the SMALL state — never an exception, and never a wrong big one.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(0)]
    [InlineData(-40)]
    public void AnUnmeasuredWidthFallsToTheFloorRatherThanThrowing(double width)
    {
        var layout = ShellLayoutPolicy.For(width);
        Assert.Equal(ShellLayoutPolicy.For(ShellLayoutPolicy.MinWidth), layout);
    }

    /// <summary>The floor leaves the room its minimum AFTER the rail takes its share.
    /// The rail is chrome the room does not get, which is why it is added rather than
    /// absorbed — a floor that forgot it would clip the room at its own minimum, and
    /// trap 14 and trap 25 are both a fixed-width assumption meeting content it could
    /// not measure.</summary>
    [Fact]
    public void TheFloorStillLeavesTheRoomItsMinimumWidth() =>
        Assert.Equal(ShellLayoutPolicy.MinRoomWidth,
            ShellLayoutPolicy.MinWidth - ShellLayoutPolicy.For(ShellLayoutPolicy.MinWidth).RailWidth);

    // ---- 4. one source for the rooms, on both surfaces -------------------------

    /// <summary>
    /// **The single-source join Helm required, asserted as TOTALITY rather than as
    /// sameness.** The rail has seven rooms and the phone has eleven screens, and the
    /// difference is a signed product decision — `CompanionSurfaces.Travel` says in as
    /// many words that the phone does NOT fold to match the desktop (World PR 4). So what
    /// must hold is that every phone screen names a room that exists, which is what makes
    /// the two lists impossible to drift apart (trap 55's shape, new surface).
    /// </summary>
    [Fact]
    public void EveryPhoneScreenBelongsToARoomTheShellHas()
    {
        foreach (var surface in CompanionSurfaces.All)
            Assert.Contains(CompanionSurfaces.PageFor(surface), ShellPages.RailOrder);
    }

    /// <summary>The two tick-only routes that left `All` when the quest surface absorbed
    /// them are mapped too — a route resolves to the room its rows are drawn in, and a
    /// route that resolved nowhere would be the one case this join could not see.
    /// </summary>
    [Theory]
    [InlineData(CompanionSurfaces.Epics)]
    [InlineData(CompanionSurfaces.Sky)]
    public void TheTickOnlyRoutesResolveToTheQuestsRoom(string surface) =>
        Assert.Equal(ShellPage.Quests, CompanionSurfaces.PageFor(surface));

    /// <summary>**The negative that keeps the join from going vacuous** (trap 39's
    /// lesson: every equality assertion deserves one negative). `PageFor` falls back to
    /// Home for an unknown name, so a mapping that quietly stopped matching would answer
    /// Home for everything — and no real phone screen belongs to Home, which is a door
    /// rather than a data surface.</summary>
    [Fact]
    public void NoRealPhoneScreenFallsThroughToHome() =>
        Assert.DoesNotContain(ShellPage.Home, CompanionSurfaces.All.Select(CompanionSurfaces.PageFor));

    [Fact]
    public void AnUnknownSurfaceNameIsTheOnlyThingThatAnswersHome() =>
        Assert.Equal(ShellPage.Home, CompanionSurfaces.PageFor("not-a-surface"));
}
