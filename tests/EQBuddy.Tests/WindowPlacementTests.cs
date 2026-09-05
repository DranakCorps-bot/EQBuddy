using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

// Guards the off-screen-restore fix (field report 2026-08-03): a position saved on a
// since-removed monitor must be rejected so the window falls back to its default spot,
// while positions anywhere on the current layout — including secondary monitors at
// negative coordinates — must be kept.
public class WindowPlacementTests
{
    // Single 1920×1080 primary screen.
    private static bool OnSingle(double left, double top,
        double width = double.NaN, double height = double.NaN) =>
        WindowPlacement.IsReachable(left, top, 0, 0, 1920, 1080, width, height);

    [Fact]
    public void FirstLaunchNaNIsNotReachable()
    {
        Assert.False(OnSingle(double.NaN, double.NaN));
        Assert.False(OnSingle(100, double.NaN));
        Assert.False(OnSingle(double.NaN, 100));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1560, 40)]      // widget's own default corner
    [InlineData(1880, 1040)]    // bottom-right, grab margin exactly visible
    public void PositionsOnScreenAreKept(double left, double top) =>
        Assert.True(OnSingle(left, top));

    [Theory]
    [InlineData(2200, 40)]      // right of a monitor that's gone
    [InlineData(-500, 40)]      // left of the screen, size unknown
    [InlineData(40, 1200)]      // below the bottom edge
    [InlineData(40, -300)]      // above the top edge
    [InlineData(1900, 40)]      // corner on-screen but < 40px of grab area left
    public void PositionsOffScreenAreRejected(double left, double top) =>
        Assert.False(OnSingle(left, top));

    [Fact]
    public void SecondMonitorAtNegativeCoordinatesIsKept()
    {
        // 1920×1080 laptop with a monitor arranged to its left: virtual screen
        // starts at -1920. The same saved spot dies when that monitor unplugs.
        Assert.True(WindowPlacement.IsReachable(-1400, 200, -1920, 0, 3840, 1080));
        Assert.False(WindowPlacement.IsReachable(-1400, 200, 0, 0, 1920, 1080));
    }

    [Fact]
    public void KnownWidthLetsATuckedWindowKeepItsSpot()
    {
        // Widget parked with its left half past the screen edge: rejected when the
        // size is unknown (top-left corner is off-screen), kept when the width
        // proves 60px of draggable body remains visible.
        Assert.False(OnSingle(-300, 40));
        Assert.True(OnSingle(-300, 40, width: 360, height: 200));
        // But a window fully past the edge is gone no matter its size.
        Assert.False(OnSingle(-500, 40, width: 360, height: 200));
    }

    [Fact]
    public void SubMarginSizesAreTreatedAsTheGrabArea()
    {
        // A 10px-tall saved height must not shrink the required visible area
        // below the drag-rescuable minimum.
        Assert.False(OnSingle(40, 1075, width: 100, height: 10));
    }

    // ---- #117 (Snagglefern): a fallback placement must never persist ----

    [Fact]
    public void AnUnmovedFallbackKeepsTheOriginalSavedSpot()
    {
        // Saved spot (2500, 300) was on a sleeping monitor; the window fell back to
        // (40, 120) and the player never touched it. Closing must keep (2500, 300).
        var (l, t) = WindowPlacement.PositionToPersist(
            restoredFromSaved: false, placedLeft: 40, placedTop: 120,
            currentLeft: 40, currentTop: 120, savedLeft: 2500, savedTop: 300);
        Assert.Equal((2500.0, 300.0), (l, t));
    }

    [Fact]
    public void MovingTheFallbackAdoptsTheNewSpot()
    {
        // The player dragged the fallen-back window somewhere: that IS a choice.
        var (l, t) = WindowPlacement.PositionToPersist(
            restoredFromSaved: false, placedLeft: 40, placedTop: 120,
            currentLeft: 800, currentTop: 450, savedLeft: 2500, savedTop: 300);
        Assert.Equal((800.0, 450.0), (l, t));
    }

    [Fact]
    public void AGenuineRestorePersistsNormally()
    {
        // Restored from the saved spot and later moved (or not): current wins.
        var (l, t) = WindowPlacement.PositionToPersist(
            restoredFromSaved: true, placedLeft: 2500, placedTop: 300,
            currentLeft: 2600, currentTop: 350, savedLeft: 2500, savedTop: 300);
        Assert.Equal((2600.0, 350.0), (l, t));
    }

    [Fact]
    public void FirstLaunchFallbackEstablishesAPosition()
    {
        // Nothing saved yet (NaN): persisting the fallback is fine — there is no
        // player-chosen spot to protect.
        var (l, t) = WindowPlacement.PositionToPersist(
            restoredFromSaved: false, placedLeft: 40, placedTop: 120,
            currentLeft: 40, currentTop: 120, savedLeft: double.NaN, savedTop: double.NaN);
        Assert.Equal((40.0, 120.0), (l, t));
    }

    // ---- the Evolved shell opens beside the game, not on top of it ----

    // The shell's own 960×640, so the numbers below are the ones the window actually asks.
    private static (double Left, double Top)? ShellOn(
        double virtualLeft, double virtualTop, double virtualWidth, double virtualHeight,
        double primaryWidth) =>
        WindowPlacement.SecondaryOrigin(virtualLeft, virtualTop, virtualWidth, virtualHeight,
            primaryWidth, windowWidth: 960, windowHeight: 640);

    [Fact]
    public void OneMonitorLeavesThePlacementToTheCaller()
    {
        // Single 1920×1080 primary — a hosted CI runner's 1024×768 too. Null means "your
        // default", which is CenterScreen: the untouched behaviour, unchanged.
        Assert.Null(ShellOn(0, 0, 1920, 1080, 1920));
        Assert.Null(ShellOn(0, 0, 1024, 768, 1024));
    }

    [Fact]
    public void ASecondMonitorToTheRightGetsTheShell()
    {
        // David's desk: EQ on the primary, the shell on DISPLAY2. 1920 + 60 margin.
        Assert.Equal((1980.0, 60.0), ShellOn(0, 0, 3840, 1080, 1920));
    }

    [Fact]
    public void ASecondMonitorToTheLeftGetsItToo()
    {
        // Virtual screen starts at -1920, so the band left of the primary is the one
        // with room. Same margin, measured from that monitor's own left edge.
        Assert.Equal((-1860.0, 60.0), ShellOn(-1920, 0, 3840, 1080, 1920));
    }

    [Fact]
    public void ATallerSecondMonitorStillOpensOnThePrimarysOwnROW()
    {
        // The second screen reaches 300 units higher, so the virtual top is -300 — and the
        // window still starts at +60, not at -240. A negative virtual top says SOME screen
        // reaches higher, never which one: on a three-monitor desk with one stacked above,
        // -240 would be off the side-by-side display entirely. y = 60 is the primary's own
        // row, which every side-by-side arrangement shares.
        Assert.Equal((1980.0, 60.0), ShellOn(0, -300, 3840, 1380, 1920));
    }

    [Fact]
    public void ABandTooNarrowForTheWindowIsNotAPlaceToOpen()
    {
        // A 640-wide auxiliary screen cannot hold a 960-wide shell with a margin beside
        // it. Half a window on a monitor and half on nothing is worse than centred.
        Assert.Null(ShellOn(0, 0, 1920 + 640, 1080, 1920));
        // And exactly enough is enough: 1020 = the window plus its margin.
        Assert.Equal((1980.0, 60.0), ShellOn(0, 0, 1920 + 1020, 1080, 1920));
    }

    [Fact]
    public void AStackedMonitorIsRefusedRatherThanGuessedAt()
    {
        // Desk is taller than the primary, so a screen is above or below it — and nothing
        // in the virtual rectangle says which COLUMN it occupies. Refusing keeps today's
        // behaviour; guessing would put the shell half on a screen and half on nothing.
        Assert.Null(ShellOn(0, 0, 1920, 2160, 1920));
        Assert.Null(ShellOn(0, -1080, 1920, 2160, 1920));
    }

    [Fact]
    public void AShortDeskPullsTheWindowBackInsteadOfHangingItOffTheBottom()
    {
        // A 600-tall secondary beside a 1080 primary: the 60 margin would leave the last
        // 100 units of a 640-tall window below the desk, so it starts higher instead.
        Assert.Equal((1980.0, 0.0), ShellOn(0, 0, 3840, 640, 1920));
    }

    [Fact]
    public void UnmeasuredMetricsAnswerNullRatherThanThrowing()
    {
        // Before a first layout, or on a host that has no screens at all. Every one of
        // these is "you decide", never an exception on the constructor path.
        Assert.Null(ShellOn(double.NaN, 0, 3840, 1080, 1920));
        Assert.Null(ShellOn(0, 0, double.NaN, 1080, 1920));
        Assert.Null(ShellOn(0, 0, 3840, 1080, 0));
        Assert.Null(WindowPlacement.SecondaryOrigin(0, 0, 3840, 1080, 1920,
            windowWidth: double.NaN, windowHeight: 640));
        Assert.Null(WindowPlacement.SecondaryOrigin(0, 0, 3840, 1080, 1920,
            windowWidth: 960, windowHeight: 0));
    }
}
