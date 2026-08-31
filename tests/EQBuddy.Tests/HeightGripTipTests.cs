using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The height grip's tooltip. It branches because a control that silently does nothing
/// reads as broken (David's 1.66.1 retest) — and since #250 there are TWO things a drag
/// can buy, so the branch that used to say "the widget has already given you everything"
/// had to learn about the second one or start lying.
/// </summary>
public class HeightGripTipTests
{
    /// <summary>The state #250 (Paineless) was actually in: every card visible, the open
    /// theme's list cut off by the body cap. The old line said the widget was sizing
    /// itself automatically and everything selected was shown — true about CARDS, and the
    /// exact opposite of what the player could see, which is why they went looking for a
    /// window-size control and reported that it did nothing.</summary>
    [Fact]
    public void EverythingVisibleButTheOpenListIsCutOffOffersTheDragThatHelps()
    {
        var tip = HeightGripTip.For(
            listIsScrolling: false, anExpandedBodyIsCapped: true, resetGesture: "Double-click");

        Assert.Contains("Drag down", tip);
        Assert.Contains("open card's list", tip);
        // The sentence that would now be false must be gone, not merely joined.
        Assert.DoesNotContain("sizing itself automatically", tip);
        Assert.DoesNotContain("everything you've selected", tip);
    }

    /// <summary>Both wins available: name both. A player who came for one should not have
    /// to discover the other by accident.</summary>
    [Fact]
    public void CardsBelowTheFoldAndACappedListNameBothWins()
    {
        var tip = HeightGripTip.For(true, true, "Double-click");
        Assert.Contains("more cards", tip);
        Assert.Contains("open card's list", tip);
    }

    /// <summary>The two states that predate #250 keep their old promises exactly — this
    /// change adds a branch, it does not re-word the ones that were already true.</summary>
    [Fact]
    public void TheTwoStatesThatPredate250KeepTheirOldWording()
    {
        var scrolling = HeightGripTip.For(true, false, "Double-click");
        Assert.Contains("Drag down to show more cards (the list is scrolling)", scrolling);

        var settled = HeightGripTip.For(false, false, "Double-click");
        Assert.Contains("sizing itself automatically", settled);
        Assert.Contains("everything you've selected in Options is shown", settled);
        // No empty promise: with nothing hidden and nothing capped, a drag DOWN buys
        // nothing, and the line must not offer one.
        Assert.DoesNotContain("Drag down", settled);
    }

    /// <summary>Every branch says how to get back to automatic, in the gesture the lane
    /// actually has. A reset nobody is told about is a setting the player cannot leave —
    /// and the two widgets differ here in the one way a toolkit is allowed to.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EveryBranchSaysHowToGetBackToAutomaticInThisLanesGesture(
        bool scrolling, bool capped)
    {
        Assert.Contains("Double-click", HeightGripTip.For(scrolling, capped, "Double-click"));
        Assert.Contains("Double-tap", HeightGripTip.For(scrolling, capped, "Double-tap"));
        Assert.Contains("automatic", HeightGripTip.For(scrolling, capped, "Double-tap"));
    }

    /// <summary>Dragging UP always works — the list scrolls — so no branch may drop it.
    /// It is the only half of the control that has never had a state where it does
    /// nothing.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EveryBranchStillOffersTheShorteningDrag(bool scrolling, bool capped)
    {
        Assert.Contains("drag up",
            HeightGripTip.For(scrolling, capped, "Double-click").ToLowerInvariant());
    }
}
