using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The minimized bar's contents (Gate 5c) — what each cell shows and in what order.
///
/// Both widgets carried this table by hand, identically, down to the comments. Nothing
/// tested it on either side, because testing it used to mean launching a window. It is
/// data now, so it can simply be asked.
/// </summary>
public class MiniBarPresentationTests
{
    private static StatsSnapshot Snapshot(double currentDps = 55) => new()
    {
        YourKillCount = 82,
        SessionDps = 41,
        CurrentDps = currentDps,
        Hps = 12.25,
        LootTotal = 39,
        Copper = 5_01_04_08,
        Deaths = [],
        CombatSeconds = 120,
    };

    [Fact]
    public void OnlyTheStatsYouStarredAppear()
    {
        var cells = MiniBarPresentation.Cells(Snapshot(), ["kills", "loot"]);
        Assert.Equal(["kills", "loot"], cells.Select(c => c.Key));
    }

    [Fact]
    public void CellsFollowTheFixedOrderNotTheOrderYouPickedThem()
    {
        // A bar that reshuffles as you toggle stats is a bar you re-read every time.
        var cells = MiniBarPresentation.Cells(Snapshot(), ["money", "kills", "loot"]);
        Assert.Equal(["kills", "loot", "money"], cells.Select(c => c.Key));
    }

    [Fact]
    public void EveryCellNamesAVectorThatActuallyExists()
    {
        // The whole point of the conversion: a name that IconPaths does not know would
        // fall back to a blank shape, which on the minimized bar reads as nothing at all.
        Assert.All(MiniBarPresentation.Cells(Snapshot(), MiniBarPresentation.Order),
            c => Assert.Contains(c.Icon, IconPaths.Names));
    }

    [Fact]
    public void NoCellCarriesAGlyph()
    {
        // #148/#166: a glyph can fail to render outright under Wine, and this surface is
        // up for the whole session.
        Assert.All(MiniBarPresentation.Icons.Values,
            name => Assert.All(name, ch => Assert.True(ch < 0x2190,
                $"'{name}' is a glyph, not an IconPaths name.")));
    }

    [Fact]
    public void BuffsIsAValidStatAndNeverDrawsACell()
    {
        // It gates the Buffs breakout window and nothing else. Drawing it would put an
        // empty cell on the bar.
        Assert.DoesNotContain("buffs", MiniBarPresentation.Order);
        Assert.Empty(MiniBarPresentation.Cells(Snapshot(), ["buffs"]));
    }

    [Fact]
    public void AKeyFromALaterVersionIsSkippedRatherThanDrawnBlank()
    {
        var cells = MiniBarPresentation.Cells(Snapshot(), ["kills", "somethingNew"]);
        Assert.Equal(["kills"], cells.Select(c => c.Key));
    }

    /// <summary>The three keys Surface A / SA-1 PROMOTED draw no cell here at all.
    ///
    /// A negative assertion on purpose (trap 39): every one of the positive tests above
    /// would still pass with "dps" quietly back in the table drawing a second, differently
    /// formatted damage number beside the HUD trio's. The fallback rule those keys used to
    /// carry moved to HudGlance and is asserted there — the current rate while a fight is
    /// live, the session rate between pulls.</summary>
    [Theory]
    [InlineData("dps")]
    [InlineData("hps")]
    [InlineData("xp")]
    public void ThePromotedHudNumbersDrawNoCellHere(string key)
    {
        Assert.DoesNotContain(key, MiniBarPresentation.Order);
        Assert.Empty(MiniBarPresentation.Cells(Snapshot(), [key]));
        Assert.Equal("", MiniBarPresentation.Text(Snapshot(), key));
    }

    [Fact]
    public void EveryOrderedStatFormatsSomething()
    {
        // A cell that renders an icon and an empty string is worse than an absent one.
        Assert.All(MiniBarPresentation.Order,
            key => Assert.NotEqual("", MiniBarPresentation.Text(Snapshot(), key)));
    }

    [Fact]
    public void KillsAndDeathsShareTheirIconAndThatIsDeliberate()
    {
        // They are never both a surprise: deaths is yours, kills is theirs, and the two
        // are only ever read with their number attached. Documented so a later pass does
        // not "fix" it into two shapes that mean the same thing.
        Assert.Equal(MiniBarPresentation.Icons["kills"], MiniBarPresentation.Icons["deaths"]);
    }
}
