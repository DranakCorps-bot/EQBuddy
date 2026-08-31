using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The widget's screen-pixels-to-layout-units conversions. These exist because the two
/// units agree at 100% scale and only diverge away from it, which is a bug that reaches
/// players rather than reviewers — discussion #144 shipped exactly that way.
/// </summary>
public class WidgetMetricsTests
{
    [Fact]
    public void AtFullScaleTheCapPassesStraightThrough()
    {
        // The case that always worked, and therefore hid the bug.
        Assert.Equal(800, WidgetMetrics.SectionMaxHeight(800, double.NaN, 1.0));
    }

    [Theory]
    [InlineData(1.5)]   // zoomed in: the list must claim FEWER layout units...
    [InlineData(0.8)]   // ...zoomed out, more, for the same screen height
    public void TheCapCoversTheSameScreenHeightAtAnyScale(double scale)
    {
        const double screenCap = 900;
        var layoutUnits = WidgetMetrics.SectionMaxHeight(screenCap, double.NaN, scale);

        // The whole point: multiply back by the scale and you are covering the monitor
        // exactly. The pre-fix code returned `screenCap` regardless, so at 1.5 the list
        // believed it had 1350 screen pixels of room on a 900-pixel allowance — and
        // clipped the last card instead of offering a scrollbar (#144).
        Assert.Equal(screenCap, layoutUnits * scale, 6);
    }

    [Fact]
    public void ADraggedHeightIsClampedByTheMonitorCapInTheSameUnits()
    {
        // ContentHeight is stored PRE-SCALE so it survives scale changes; comparing it
        // against a screen-pixel cap was the same mix-up wearing a different hat.
        var cap = WidgetMetrics.SectionMaxHeight(900, contentHeight: 5000, uiScale: 1.5);
        Assert.Equal(600, cap);                        // 900 / 1.5, not 900
        Assert.Equal(400, WidgetMetrics.SectionMaxHeight(900, 400, 1.5));   // under the cap: kept
    }

    [Fact]
    public void TheListNeverCollapsesBelowOneCard()
    {
        Assert.Equal(WidgetMetrics.MinSectionHeight,
            WidgetMetrics.SectionMaxHeight(900, contentHeight: 10, uiScale: 1.0));
        // Even a monitor cap smaller than the floor cannot squeeze it out of existence.
        Assert.Equal(WidgetMetrics.MinSectionHeight,
            WidgetMetrics.SectionMaxHeight(50, contentHeight: 300, uiScale: 1.0));
    }

    [Fact]
    public void AZeroOrNegativeScaleCannotProduceInfinity()
    {
        Assert.True(double.IsFinite(WidgetMetrics.SectionMaxHeight(900, double.NaN, 0)));
        Assert.True(double.IsFinite(WidgetMetrics.SectionMaxHeight(900, double.NaN, -1)));
    }

    [Theory]
    [InlineData(1.0, 100)]   // 100 screen px of drag = 100 layout units
    [InlineData(2.0, 50)]    // ...but only 50 when everything is drawn twice as large
    public void DraggingTheBottomEdgeConvertsCursorTravelIntoLayoutUnits(
        double scale, double expectedGrowth)
    {
        Assert.Equal(400 + expectedGrowth,
            WidgetMetrics.ContentHeightFromDrag(startHeight: 400, cursorDeltaPixels: 100, uiScale: scale));
    }

    [Fact]
    public void DraggingUpwardStopsAtTheFloorRatherThanGoingNegative()
    {
        Assert.Equal(WidgetMetrics.MinSectionHeight,
            WidgetMetrics.ContentHeightFromDrag(300, cursorDeltaPixels: -9999, uiScale: 1.0));
    }

    // ---- #250 (Paineless): the theme body cap follows the height grip ----
    //
    // The complaint was two clauses and the second one is the finding: "cannot just expand
    // window size." ThemeBodyMaxHeight was a const, so dragging the widget taller grew the
    // card stack and left every expanded theme body at 320. These pin the three things the
    // signed plan promises — the untouched widget does not move, a dragged one does, and
    // neither end can run away.

    /// <summary>The floor IS the default. ContentHeight is NaN until someone drags the
    /// grip, and that case must answer exactly what the app drew before this existed —
    /// the whole #227/#228-class safety of the change is that an untouched widget is
    /// pixel-identical.</summary>
    [Fact]
    public void AWidgetNobodyHasDraggedGetsExactlyTheOldConstant()
    {
        Assert.Equal(WidgetMetrics.ThemeBodyMaxHeight,
            WidgetMetrics.ThemeBodyCap(double.NaN, otherVisibleChrome: 0));
        // ...and it stays the old constant however much chrome is around it, because the
        // formula is not consulted at all until the player has said what they want.
        Assert.Equal(WidgetMetrics.ThemeBodyMaxHeight,
            WidgetMetrics.ThemeBodyCap(double.NaN, otherVisibleChrome: 900));
    }

    /// <summary>The actual ask: a taller widget means a taller body. 700 units of stack
    /// with 180 spent on the other cards' headers leaves 520 for the room that is
    /// open.</summary>
    [Fact]
    public void DraggingTheWidgetTallerGivesTheExpandedRoomTheRoom()
    {
        Assert.Equal(520, WidgetMetrics.ThemeBodyCap(700, otherVisibleChrome: 180));
    }

    /// <summary>Never below the floor, whatever the chrome — a stack crowded with cards
    /// must not squeeze the open one below what it would have had with no drag at all.
    /// This is the direction that could have regressed every existing player.</summary>
    [Theory]
    [InlineData(700, 900)]    // more chrome than there is room: the stack scrolls instead
    [InlineData(400, 300)]    // 100 left over, which is not a body
    [InlineData(200, 0)]      // dragged SHORTER than the floor
    public void TheBodyNeverGoesBelowTheFloorHoweverCrowdedTheStackIs(
        double contentHeight, double chrome)
    {
        Assert.Equal(WidgetMetrics.ThemeBodyMaxHeight,
            WidgetMetrics.ThemeBodyCap(contentHeight, chrome));
    }

    /// <summary>Never above the ceiling, whatever the drag. One card may double; it may
    /// not eat the monitor — and the monitor is not this number's job anyway, since
    /// SectionMaxHeight still bounds the stack the body sits in.</summary>
    [Fact]
    public void TheBodyNeverGoesAboveTheCeilingHoweverFarTheGripIsDragged()
    {
        Assert.Equal(WidgetMetrics.ThemeBodyCeiling, WidgetMetrics.ThemeBodyCap(4000, 0));
        Assert.Equal(640, WidgetMetrics.ThemeBodyCeiling);
        Assert.Equal(2 * WidgetMetrics.ThemeBodyMaxHeight, WidgetMetrics.ThemeBodyCeiling);
    }

    /// <summary>A measurement that has not happened yet answers the floor. The card asks
    /// for its cap on the first render, which can land before the stack has been laid out;
    /// "we cannot tell yet" and "draw what you have always drawn" are the same instruction,
    /// and a NaN reaching a MaxHeight is a control with no cap at all.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ChromeThatHasNotBeenMeasuredYetFallsBackToTheFloor(double unmeasured)
    {
        Assert.Equal(WidgetMetrics.ThemeBodyMaxHeight,
            WidgetMetrics.ThemeBodyCap(700, unmeasured));
    }

    /// <summary>Nonsense chrome cannot BUY room. A negative measurement would otherwise
    /// add to the cap, which is a bug that only ever shows up on someone else's toolkit.</summary>
    [Fact]
    public void NegativeChromeIsTreatedAsNoneRatherThanAsExtraRoom()
    {
        Assert.Equal(WidgetMetrics.ThemeBodyCap(500, 0),
            WidgetMetrics.ThemeBodyCap(500, otherVisibleChrome: -200));
    }

    /// <summary>Whole units. This feeds a MaxHeight on a SizeToContent always-on-top
    /// window: a cap that wobbled by a fraction would ask the windowing system to resize a
    /// window stacked over a fullscreen game, which is what #173 cost a player (trap 12).
    /// Layout moves this number; nothing on a clock does.</summary>
    [Fact]
    public void TheCapIsAWholeNumberSoASubPixelWobbleCannotResizeTheWindow()
    {
        var cap = WidgetMetrics.ThemeBodyCap(700.4, otherVisibleChrome: 180.3);
        Assert.Equal(Math.Floor(cap), cap);
        Assert.Equal(WidgetMetrics.ThemeBodyCap(700.6, 180.3), cap);
    }

    // ---- #239 (disberon): the mode swap anchors the RIGHT edge, both directions ----

    [Fact]
    public void ExpandingAnchorsTheRightEdgeSoTheTogglePairStaysUnderTheCursor()
    {
        // Mini bar 180 wide at Left=1000 (right edge 1180) expands to the 320 window:
        // Left moves to 860 and the right edge does not move.
        Assert.Equal(860, WidgetMetrics.RightAnchoredLeft(1000, oldWidth: 180, newWidth: 320));
        // And back: minimizing returns Left to where the mini bar's right edge was.
        Assert.Equal(1000, WidgetMetrics.RightAnchoredLeft(860, oldWidth: 320, newWidth: 180));
    }

    /// <summary>The startup call reaches SetMode before the first layout, when ActualWidth
    /// is 0 — anchoring to a measurement that never happened would move a freshly restored
    /// window. Same answer for a width that is broken rather than merely absent.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AWidthThatIsNotRealLeavesTheWindowWhereItIs(double unreal)
    {
        Assert.Equal(500, WidgetMetrics.RightAnchoredLeft(500, unreal, 320));
        Assert.Equal(500, WidgetMetrics.RightAnchoredLeft(500, 320, unreal));
    }

    /// <summary>No work-area clamp, on purpose: a negative Left is a real place on a
    /// multi-monitor desk, and clamping against the primary would yank a secondary-monitor
    /// widget. The window was already on screen at this right edge.</summary>
    [Fact]
    public void AMultiMonitorNegativeLeftIsARealPlaceNotAnErrorToClamp()
    {
        Assert.Equal(-1500, WidgetMetrics.RightAnchoredLeft(-1360, oldWidth: 180, newWidth: 320));
    }
}
