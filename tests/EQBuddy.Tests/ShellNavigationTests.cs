using EQBuddy.Companion;
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

    /// <summary>E-3 PR 1 landed exactly one room. **This row is meant to be edited** — a
    /// room joins `Landed` in the PR that lands it, and this assertion is the reminder
    /// that doing so is a deliberate act rather than a line someone slid in.</summary>
    [Fact]
    public void OnlyProgressHasLandedSoFar() =>
        Assert.Equal([ShellPage.Progress], ShellPages.Landed);

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
