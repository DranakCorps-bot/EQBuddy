using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **WHAT A VENDOR HAS ACTUALLY PAID THIS CHARACTER** (DRA-71 D7, plan P9).
///
/// <para>The fold the money engines rank on, and the reason they rank on it rather than on the
/// catalog: a vendor's price in EQ moves with the seller's Charisma and faction, so the wiki's
/// number is a quote somebody was given and this one is a measurement of the player reading
/// it.</para>
/// </summary>
public class SaleHistoryTests
{
    private static SessionSales Row(long id, params (string Item, int Count, long Copper)[] sales) =>
        new(id, [.. sales.Select(s => new SoldDetail(s.Item, s.Count, s.Copper))]);

    /// <summary>Sales pool across sessions, and the per-item price is the total over the
    /// total — never a mean of per-session means, which would let one sale of one item weigh
    /// as much as forty (<see cref="ZoneHistory"/>'s own pooling rule).</summary>
    [Fact]
    public void SalesPoolAcrossSessionsAndThePriceIsTheTotalOverTheTotal()
    {
        var sales = SaleHistory.Fold(
            [Row(1, ("Froglok Blood", 4, 320)), Row(2, ("Froglok Blood", 6, 540))],
            null, liveRowId: 0);

        var roll = Assert.Single(sales);
        Assert.Equal("Froglok Blood", roll.Item);
        Assert.Equal(10, roll.Count);
        Assert.Equal(860, roll.Copper);
        Assert.Equal(86, roll.CopperEach);
    }

    /// <summary>
    /// **"+N" FOLDS TO THE BASE NAME, WHICH IS WHAT MAKES THE JOIN POSSIBLE AT ALL.**
    ///
    /// <para><see cref="MobHistory"/> folds looted items the same way, so a sale and a drop meet
    /// on one key. It loses the premium a "+N" fetches — an honest cost, and the alternative is
    /// a sale that can never be matched to the drop it came from, which is the whole
    /// answer.</para>
    /// </summary>
    [Fact]
    public void UpgradeSuffixesFoldToTheBaseNameTheDropIsKeyedOn()
    {
        var roll = Assert.Single(SaleHistory.Fold(
            [Row(1, ("Rusty Long Sword +3", 1, 100), ("Rusty Long Sword", 1, 60))],
            null, liveRowId: 0));

        Assert.Equal("Rusty Long Sword", roll.Item);
        Assert.Equal(2, roll.Count);
        Assert.Equal(80, roll.CopperEach);
    }

    /// <summary>The live session's own sales are folded in, and its CHECKPOINTED row is skipped
    /// — <see cref="MobHistory.Pool"/>'s rule, because a checkpoint that has already landed
    /// would otherwise count the same sale twice.</summary>
    [Fact]
    public void TheLiveSessionIsFoldedInAndItsCheckpointedRowIsNotCountedTwice()
    {
        var live = new StatsSnapshot { SoldItems = [new SoldDetail("Froglok Blood", 2, 160)] };

        var doubled = SaleHistory.Fold([Row(7, ("Froglok Blood", 2, 160))], live, liveRowId: 0);
        Assert.Equal(4, Assert.Single(doubled).Count);

        var once = SaleHistory.Fold([Row(7, ("Froglok Blood", 2, 160))], live, liveRowId: 7);
        Assert.Equal(2, Assert.Single(once).Count);
    }

    /// <summary>A sale with no count cannot produce a per-item price, so it is dropped rather
    /// than folded — the price is the one number this fold exists to produce, and a wrong one
    /// is worse than a missing one.</summary>
    [Fact]
    public void ASaleWithNoCountIsDroppedRatherThanDividedBy()
    {
        Assert.Empty(SaleHistory.Fold([Row(1, ("Froglok Blood", 0, 320))], null, 0));
        Assert.Empty(SaleHistory.Fold([Row(1, ("Froglok Blood", -1, 320))], null, 0));
        Assert.Empty(SaleHistory.Fold([Row(1, ("", 4, 320))], null, 0));
    }

    /// <summary>
    /// **NULL IS THE IMPORTANT HALF OF THE LOOKUP.**
    ///
    /// <para>"You sell these for 8 silver" and "EQBuddy has never seen you sell one" are
    /// different facts, and only the second one is allowed to fall through to the catalog's
    /// estimate. A lookup that answered 0 for both would price every unsold item at
    /// nothing.</para>
    /// </summary>
    [Fact]
    public void AnItemYouHaveNeverSoldAnswersNullRatherThanZero()
    {
        IReadOnlyList<SaleRoll> sales = [new SaleRoll("Froglok Blood", 4, 320)];

        Assert.Equal(80, SaleHistory.CopperEachFor(sales, "Froglok Blood"));
        Assert.Equal(80, SaleHistory.CopperEachFor(sales, "Froglok Blood +2"));
        Assert.Null(SaleHistory.CopperEachFor(sales, "Rusty Axe"));
        Assert.Null(SaleHistory.CopperEachFor([], "Froglok Blood"));
        Assert.Null(SaleHistory.CopperEachFor(null, "Froglok Blood"));
    }

    /// <summary>Most valuable first, and ties broken by name so the order is stable rather than
    /// whatever the dictionary produced.</summary>
    [Fact]
    public void TheMostValuableSaleSortsFirst()
    {
        var sales = SaleHistory.Fold(
            [Row(1, ("Rusty Axe", 40, 400), ("Froglok Blood", 4, 3200), ("Bone Chips", 4, 400))],
            null, 0);

        Assert.Equal(["Froglok Blood", "Bone Chips", "Rusty Axe"], sales.Select(s => s.Item));
    }
}
