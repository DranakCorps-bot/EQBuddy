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
