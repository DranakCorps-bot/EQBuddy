using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Discussion #966 (CryfaceCorpse): the Guide window opened on a third monitor with its
/// title bar out of reach, and a moved position was never remembered. These pin the
/// arithmetic the shell now runs on open (<see cref="ShellPlacement.Fit"/>) and on close
/// (<see cref="ShellPlacement.ToPersist"/>). Every number is in DIPs with the primary's
/// top-left at the origin — the unit space <c>ScreenGuard</c> hands in.
/// </summary>
public class ShellPlacementTests
{
    // A 1440p primary, taskbar at the bottom.
    private static readonly ScreenArea Primary = new(0, 0, 2560, 1400);

    // A 1080p monitor to its RIGHT, bottom-aligned — so its top edge is 360 down. This is
    // the arrangement the shipped SecondaryOrigin could not see: the virtual screen is
    // 4480×1440 from (0,0), which says nothing about where this monitor's top is.
    private static readonly ScreenArea LowSide = new(2560, 360, 1920, 1040);

    private static ShellBounds Open(double left, double top) =>
        new(left, top, ShellLayoutPolicy.OpenWidth, ShellLayoutPolicy.OpenHeight);

    [Fact]
    public void The966DeskPutsTheSecondaryGuessesTitleBarAboveTheSideMonitor()
    {
        // The defect, measured on the shipped arithmetic: SecondaryOrigin answers the
        // primary's row + 60, and that row is dead space above the side monitor.
        var origin = WindowPlacement.SecondaryOrigin(0, 0, 4480, 1440, 2560,
            ShellLayoutPolicy.OpenWidth, ShellLayoutPolicy.OpenHeight);
        Assert.NotNull(origin);
        Assert.Equal(2620, origin.Value.Left);
        Assert.Equal(60, origin.Value.Top);
        Assert.True(origin.Value.Top < LowSide.Top,
            "the title bar row sits above the monitor the window was sent to");

        // The fix: fitted onto that monitor, title bar on it, column unchanged.
        var fit = ShellPlacement.Fit(Open(origin.Value.Left, origin.Value.Top), [Primary, LowSide]);
        Assert.NotNull(fit);
        Assert.Equal(2620, fit.Value.Left);
        Assert.Equal(LowSide.Top, fit.Value.Top);
        AssertInside(fit.Value, LowSide);
    }

    [Fact]
    public void AWindowAlreadyOnAMonitorIsNotMovedAUnit()
    {
        var here = Open(3000, 500);
        Assert.Equal(here, ShellPlacement.Fit(here, [Primary, LowSide]));
    }

    [Fact]
    public void OverlappingNoMonitorTheColumnWinsOverStraightLineDistance()
    {
        // A short side monitor whose top is 800 down. The window at the secondary guess
        // (2620, 60) touches no monitor at all. The PRIMARY is nearer in a straight line
        // (60 to its right edge, against 100 to the side monitor's top) — and handing the
        // window back to it would drop the shell over the game.
        var shortSide = new ScreenArea(2560, 800, 1280, 680);
        var fit = ShellPlacement.Fit(Open(2620, 60), [Primary, shortSide]);
        Assert.NotNull(fit);
        AssertInside(fit.Value, shortSide);
    }

    [Fact]
    public void ASpotSavedOnAMonitorThatIsGoneComesBackToTheNearestOne()
    {
        // Saved on a third monitor that has since been unplugged: off to the right of both.
        var fit = ShellPlacement.Fit(Open(5200, 400), [Primary, LowSide]);
        Assert.NotNull(fit);
        AssertInside(fit.Value, LowSide);
    }

    [Fact]
    public void TheMonitorItOverlapsMostIsTheOneItIsFittedTo()
    {
        // Straddling the seam, mostly on the primary: it stays on the primary.
        var fit = ShellPlacement.Fit(Open(2000, 400), [Primary, LowSide]);
        Assert.NotNull(fit);
        AssertInside(fit.Value, Primary);
        Assert.Equal(Primary.Right - ShellLayoutPolicy.OpenWidth, fit.Value.Left);
    }

    [Fact]
    public void ASavedSizeBiggerThanTheMonitorIsCappedToItsWorkArea()
    {
        var small = new ScreenArea(0, 0, 800, 500);
        var fit = ShellPlacement.Fit(new ShellBounds(-100, -300, 1400, 900), [small]);
        Assert.NotNull(fit);
        Assert.Equal(new ShellBounds(0, 0, 800, 500), fit.Value);
    }

    [Fact]
    public void AMonitorSmallerThanTheShellsFloorKeepsTheTitleBarCorner()
    {
        // The floor is the shell's own minimum; the corner with the title bar in it wins.
        var tiny = new ScreenArea(100, 50, 500, 350);
        var fit = ShellPlacement.Fit(Open(900, 900), [tiny]);
        Assert.NotNull(fit);
        Assert.Equal(new ShellBounds(100, 50, ShellLayoutPolicy.MinWidth, ShellLayoutPolicy.MinHeight),
            fit.Value);
    }

    [Fact]
    public void NoMonitorsOrNoRealBoundsIsNoAnswer()
    {
        Assert.Null(ShellPlacement.Fit(Open(0, 0), []));
        Assert.Null(ShellPlacement.Fit(Open(double.NaN, 0), [Primary]));
        Assert.Null(ShellPlacement.Fit(new ShellBounds(0, 0, 0, 0), [Primary]));
        Assert.Null(ShellPlacement.Fit(Open(0, 0), [new ScreenArea(0, 0, double.NaN, 100)]));
    }

    [Fact]
    public void TheOpeningSizeIsTheSavedOneFlooredOrTheDefault()
    {
        Assert.Equal((ShellLayoutPolicy.OpenWidth, ShellLayoutPolicy.OpenHeight),
            ShellPlacement.OpeningSize(double.NaN, double.NaN));
        // A half-written pair is not a size.
        Assert.Equal((ShellLayoutPolicy.OpenWidth, ShellLayoutPolicy.OpenHeight),
            ShellPlacement.OpeningSize(1200, double.NaN));
        Assert.Equal((1200.0, 800.0), ShellPlacement.OpeningSize(1200, 800));
        Assert.Equal((ShellLayoutPolicy.MinWidth, ShellLayoutPolicy.MinHeight),
            ShellPlacement.OpeningSize(100, 100));
    }

    // ---- what a close writes back ----------------------------------------------------

    private static readonly ShellBounds Saved = new(3000, 500, 1100, 700);
    private static readonly ShellBounds Placed = new(2620, 360, 960, 640);
    private static readonly ShellBounds Unsaved = new(double.NaN, double.NaN, double.NaN, double.NaN);

    [Fact]
    public void AFirstOpenThatWasNeverMovedStillRemembersWhereItWas()
    {
        Assert.Equal(Placed, ShellPlacement.ToPersist(false, Placed, Placed, Unsaved));
    }

    [Fact]
    public void AMoveIsRemembered()
    {
        var moved = Placed with { Left = 3100, Top = 420 };
        Assert.Equal(moved, ShellPlacement.ToPersist(false, Placed, moved, Unsaved));
        Assert.Equal(moved, ShellPlacement.ToPersist(false, Placed, moved, Saved));
    }

    [Fact]
    public void AResizeAloneIsRemembered()
    {
        var resized = Placed with { Width = 1200 };
        Assert.Equal(resized, ShellPlacement.ToPersist(false, Placed, resized, Saved));
    }

    [Fact]
    public void ARestoredSpotIsWrittenBackAsItNowIs()
    {
        // Including the case where the fit had to move it: the corrected spot is the one
        // that works on this desk.
        Assert.Equal(Placed, ShellPlacement.ToPersist(true, Placed, Placed, Saved));
    }

    [Fact]
    public void AnUntouchedFallbackKeepsTheSavedSpotForWhenTheMonitorComesBack()
    {
        // #117: a sleeping display rejected the saved spot; the player did nothing.
        Assert.Equal(Saved, ShellPlacement.ToPersist(false, Placed, Placed, Saved));
        // Pixel rounding at a non-integer scale is not the player moving it.
        var rounded = Placed with { Width = Placed.Width + 0.4, Top = Placed.Top - 0.6 };
        Assert.Equal(Saved, ShellPlacement.ToPersist(false, Placed, rounded, Saved));
    }

    private static void AssertInside(ShellBounds b, ScreenArea a)
    {
        Assert.True(b.Left >= a.Left && b.Top >= a.Top
            && b.Left + b.Width <= a.Right && b.Top + b.Height <= a.Bottom,
            $"{b} is not inside {a}");
    }
}
