using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// #942's anchor STATE, lifted out of MainWindow: what the bar is compared against, and the
/// rule that the mode swap's own re-measure never moves the window a second time. The
/// arithmetic itself is <see cref="WidgetMetrics.MiniBarLeft"/>'s and is tested there.
/// </summary>
public class MiniBarAnchorTests
{
    [Fact]
    public void AWideningBarKeepsItsRightEdgeAgainstTheWidthItLastSaw()
    {
        var anchor = new MiniBarAnchor(200);
        Assert.Equal(1520, anchor.LeftFor(280, minimized: true, growsLeft: true, left: 1600));
        // The next change is measured from 280, not from the seed.
        Assert.Equal(1540, anchor.LeftFor(260, true, true, 1520));
    }

    [Fact]
    public void AWidthSeenDuringTheSwapMovesNothingAndIsNotMovedAgainAfterIt()
    {
        var anchor = new MiniBarAnchor(90);
        using (anchor.Swap())
        {
            Assert.True(anchor.Swapping);
            Assert.Equal(1600, anchor.LeftFor(300, true, true, 1600));
        }
        Assert.False(anchor.Swapping);
        anchor.Saw(300);
        // The late SizeChanged for the same width: a zero delta, not a second move.
        Assert.Equal(1600, anchor.LeftFor(300, true, true, 1600));
    }

    [Fact]
    public void AnUnseededAnchorLeavesTheFirstRealWidthAlone()
    {
        // MiniBarAnchorSeed's 0 is "no right edge to go back to" (first launch).
        var anchor = new MiniBarAnchor(0);
        Assert.Equal(1600, anchor.LeftFor(280, true, true, 1600));
        Assert.Equal(1580, anchor.LeftFor(300, true, true, 1600));
    }

    [Fact]
    public void OffOrExpandedItOnlyRemembers()
    {
        var anchor = new MiniBarAnchor(200);
        Assert.Equal(1600, anchor.LeftFor(280, true, growsLeft: false, 1600));
        Assert.Equal(1600, anchor.LeftFor(320, minimized: false, true, 1600));
        // Remembered even while off, so turning it on anchors from the width on screen.
        Assert.Equal(1580, anchor.LeftFor(340, true, true, 1600));
    }
}
