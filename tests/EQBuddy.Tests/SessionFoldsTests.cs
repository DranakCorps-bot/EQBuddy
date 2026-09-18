using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Hateborne, 2026-09-18: fold "No longer needed", close the Quest Tracker, reopen it - and
/// it should still be folded; restart EQBuddy and it is back to open. The store lives on
/// MainWindow for the run and is never written anywhere, which is what these pin.
/// </summary>
public class SessionFoldsTests
{
    [Fact]
    public void EverythingStartsAtItsDefault()
    {
        var folds = new SessionFolds();

        Assert.True(folds.IsOpen("skyLeftoverA"));
        Assert.False(folds.IsOpen("somethingClosedByDefault", defaultOpen: false));
        Assert.Equal(0, folds.Version);
    }

    [Fact]
    public void EachBandFoldsOnItsOwn()
    {
        var folds = new SessionFolds();
        folds.Toggle("skyLeftoverA");

        Assert.False(folds.IsOpen("skyLeftoverA"));
        Assert.True(folds.IsOpen("skyReady"));
        Assert.True(folds.IsOpen("skyLeftoverB"));
    }

    [Fact]
    public void AChangeMovesTheVersionAndANoOpDoesNot()
    {
        var folds = new SessionFolds();
        folds.Set("skyReady", false);
        var after = folds.Version;

        folds.Set("skyReady", false);   // already folded: other hosts have nothing to redraw
        Assert.Equal(after, folds.Version);

        folds.Toggle("skyReady");
        Assert.True(folds.IsOpen("skyReady"));
        Assert.True(folds.Version > after);
    }

    /// <summary>A new run is a new store - "not across all sessions".</summary>
    [Fact]
    public void ANewRunStartsOpenAgain()
    {
        new SessionFolds().Set("skyLeftoverA", false);

        Assert.True(new SessionFolds().IsOpen("skyLeftoverA"));
    }
}
