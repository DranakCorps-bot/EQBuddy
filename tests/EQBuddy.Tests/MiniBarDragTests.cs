using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The mini bar's drag-to-reorder, as arithmetic (#191, TheMegaSage; owner lock 2026-09-07).
///
/// **The window cannot be unit-tested** (docs/TestPlan.md §5), so a reorder whose decisions
/// lived in <c>HudBarView</c> would have exactly one guard: an E2E run driving a synthetic
/// pointer, which nothing in this repo has ever managed for a control inside the widget.
/// Every decision the gesture makes is here instead, and what is left in the view is
/// hit-testing, capture and ink.
///
/// The thresholds are passed in rather than read from <c>SystemParameters</c>, which is what
/// lets the rules be stated in numbers here and still be the player's own numbers in the app.
/// </summary>
public class MiniBarDragTests
{
    // The system's defaults on a stock Windows desk, near enough: 4 DIPs each way.
    private const double H = 4;
    private const double V = 4;

    private static MiniBarGesture Classify(double dx, double dy)
        => MiniBarDrag.Classify(dx, dy, H, V);

    // ---- WHICH GESTURE ---------------------------------------------------------------

    /// <summary>Inside the threshold the press is still nobody's — it is allowed to become
    /// the chip's click, which is the whole reason the click moved from the mouse-down to
    /// the mouse-up.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    [InlineData(-3.9, 3.9)]
    public void BelowTheThresholdTheGestureIsStillAClick(double dx, double dy)
        => Assert.Equal(MiniBarGesture.Pending, Classify(dx, dy));

    [Theory]
    [InlineData(12, 0)]
    [InlineData(-12, 0)]
    [InlineData(4, 0)]
    public void PastTheThresholdSidewaysIsACarry(double dx, double dy)
        => Assert.Equal(MiniBarGesture.Reorder, Classify(dx, dy));

    /// <summary>A diagonal that is plainly horizontal is a carry. A chip dragged along a
    /// one-row bar is rarely level, and refusing that gesture would read as a chip that will
    /// not move.</summary>
    [Fact]
    public void ADiagonalThatIsMostlySidewaysIsStillACarry()
        => Assert.Equal(MiniBarGesture.Reorder, Classify(20, 9));

    /// <summary>Straight down means NOTHING on this bar — not a reorder, and not the
    /// widget move that the ground behind the chips still does. It is dead on purpose.
    ///
    /// **But dead is not the same as pending**, and that is the assertion that matters: a
    /// player who wiped the pointer down off the bar must not pin a peek on the way out, so
    /// the gesture has to be able to say "I am over, and nobody gets a click".</summary>
    [Theory]
    [InlineData(0, 12)]
    [InlineData(0, -12)]
    [InlineData(5, 20)]
    public void StraightDownMeansNothingAndIsNotAClickEither(double dx, double dy)
        => Assert.Equal(MiniBarGesture.Dead, Classify(dx, dy));

    // ---- WHERE IT LANDS --------------------------------------------------------------
    //
    // Four chips whose centres sit at 10, 30, 50 and 70.

    private static readonly double[] Mids = [10, 30, 50, 70];

    [Theory]
    [InlineData(0, 0)]      // still left of everything
    [InlineData(35, 1)]     // carried just past the second chip's centre
    [InlineData(55, 2)]
    [InlineData(999, 3)]    // off the right-hand end
    public void ACarriedChipLandsWhereTheOthersLeaveRoom(double x, int expected)
        => Assert.Equal(expected, MiniBarDrag.DropIndex(Mids, from: 0, x));

    /// <summary>Dragging LEFT counts the same way, and the chip's own midpoint is never one
    /// of the votes — otherwise a chip carried a few pixels would displace itself.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(25, 1)]
    [InlineData(49, 2)]
    [InlineData(71, 3)]
    public void TheCarriedChipDoesNotCountItself(double x, int expected)
        => Assert.Equal(expected, MiniBarDrag.DropIndex(Mids, from: 3, x));

    /// <summary>The answer is always a slot the list HAS. A negative here would be an
    /// exception on drop and an out-of-range one would silently write an order with a hole
    /// in it.</summary>
    [Theory]
    [InlineData(-10_000)]
    [InlineData(10_000)]
    public void TheLandingIsAlwaysARealSlot(double x)
    {
        var to = MiniBarDrag.DropIndex(Mids, from: 1, x);
        Assert.InRange(to, 0, Mids.Length - 1);
    }

    [Fact]
    public void AnEmptyBarHasNowhereToLand()
        => Assert.Equal(0, MiniBarDrag.DropIndex([], from: 0, 50));

    // ---- WHAT THE ORDER READS AFTERWARDS ---------------------------------------------

    private static readonly string[] Canonical =
        ["kills", "pet", "procs", "loot", "motes", "money", "deaths", "buffs"];

    [Fact]
    public void CarryingAChipToTheFrontPutsItAtTheFront()
    {
        // All eight starred, so drawn == the full order and the two indexings agree.
        var moved = MiniBarDrag.Move(Canonical, Canonical, from: 5, to: 0);
        Assert.Equal(
            ["money", "kills", "pet", "procs", "loot", "motes", "deaths", "buffs"], moved);
    }

    /// <summary>A drag is not a SWAP, and the negative is the point (trap 39): a swap-based
    /// implementation passes every "did it move" assertion while sending the chip that was in
    /// the target slot all the way to the other end, which is not what the player did with
    /// their hand.</summary>
    [Fact]
    public void CarryingIsNotSwapping()
    {
        var moved = MiniBarDrag.Move(Canonical, Canonical, from: 0, to: 3);
        Assert.Equal(
            ["pet", "procs", "loot", "kills", "motes", "money", "deaths", "buffs"], moved);
        Assert.NotEqual("kills", moved[3 - 1]);   // a swap would have left "loot" at 0
        Assert.Equal("pet", moved[0]);
    }

    /// <summary>
    /// **THE UN-STARRED KEYS KEEP THEIR PLACES.** What a player can grab is the drawn
    /// list — three chips, say — while what gets written is every key there is. Re-indexing
    /// the drawn list and saving THAT would strip the five keys with no ★ out of the setting,
    /// and <c>ResolveOrder</c> would append them canonically the next time one was starred: a
    /// stat you had placed once would come back somewhere else, with nothing naming the loss.
    /// </summary>
    [Fact]
    public void MovingAVisibleChipLeavesTheUnstarredKeysWhereTheyWere()
    {
        // The player sees kills, loot, money. They carry "money" to the front.
        string[] drawn = ["kills", "loot", "money"];
        var moved = MiniBarDrag.Move(Canonical, drawn, from: 2, to: 0);
        Assert.Equal(
            ["money", "kills", "pet", "procs", "loot", "motes", "deaths", "buffs"], moved);
        // Every key survives, exactly once — the setting is still a whole order.
        Assert.Equal(Canonical.Length, moved.Count);
        Assert.Equal(moved.Count, moved.Distinct().Count());
    }

    /// <summary>The other direction, which is where "insert before or after the neighbour"
    /// earns itself: carrying the first visible chip to the last visible slot has to put it
    /// AFTER that neighbour, not before it.</summary>
    [Fact]
    public void CarryingAVisibleChipRightLandsAfterItsNewNeighbour()
    {
        string[] drawn = ["kills", "loot", "money"];
        var moved = MiniBarDrag.Move(Canonical, drawn, from: 0, to: 2);
        Assert.Equal(
            ["pet", "procs", "loot", "motes", "money", "kills", "deaths", "buffs"], moved);
        Assert.Equal(["loot", "money", "kills"], moved.Where(drawn.Contains));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(9, 0)]
    [InlineData(1, 1)]
    public void ANoOpMoveChangesNothing(int from, int to)
        => Assert.Equal(Canonical, MiniBarDrag.Move(Canonical, Canonical, from, to));
}
