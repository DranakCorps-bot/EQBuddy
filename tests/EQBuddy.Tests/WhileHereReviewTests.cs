using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The review fold (DRA-4211). <c>shut</c> is the only value that starts the block
/// folded, and the player default — unset — does not. Prove-failed by treating every
/// value as shut: the unset row goes red.
/// </summary>
public sealed class WhileHereReviewTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("open")]
    [InlineData("SHUT")]
    [InlineData("0")]
    public void AnythingButShutLeavesTheBlockOpen(string? value) =>
        Assert.False(WhileHereReview.StartShut(value));

    [Fact]
    public void ShutStartsTheBlockFoldedAndStillDrawsIt()
    {
        Assert.True(WhileHereReview.StartShut(WhileHereReview.Shut));
        Assert.False(WhileHereReview.StartHidden(WhileHereReview.Shut));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("shut")]
    [InlineData("OFF")]
    public void AnythingButOffStillDrawsTheBlock(string? value) =>
        Assert.False(WhileHereReview.StartHidden(value));

    [Fact]
    public void OffDrawsNothingAndDoesNotCountAsTheFold()
    {
        Assert.True(WhileHereReview.StartHidden(WhileHereReview.Off));
        Assert.False(WhileHereReview.StartShut(WhileHereReview.Off));
    }
}
