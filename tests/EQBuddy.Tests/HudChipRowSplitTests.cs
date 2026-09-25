using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// **TWO ROWS, TWO INKS** (DRA-352 D1, Founder-directed 2026-09-23: "separate respawn from
/// mez; mez counts down in blue, respawn in white").
///
/// Everything the split decides lives in <see cref="HudChipRow"/> so it can be asserted
/// with no window (docs/TestPlan.md §5): which family goes on which row, how one merge is
/// cut into two without a second producer (trap 33), how Place moves a family within its
/// own row, how a slaved spawn row stacks past the fight row, which settings each row
/// reads and writes, and the family → ink table. Every table here carries its negative —
/// an ink test that only proved "mez is blue" would pass a renderer that painted every
/// family blue (trap 34).
/// </summary>
[Collection(SettingsFileCollection.Name)]   // one row round-trips settings.json
public class HudChipRowSplitTests
{
    private static SpawnChip Chip(string name, bool due = false) =>
        new("", name, "3:12", due, "detail");

    // ---- Which row a family is on ----

    [Theory]
    [InlineData(HudChipFamily.Spawn, HudRowKind.Spawn)]
    [InlineData(HudChipFamily.Mez, HudRowKind.Fight)]
    [InlineData(HudChipFamily.WatchFire, HudRowKind.Fight)]
    [InlineData(HudChipFamily.Buff, HudRowKind.Fight)]
    public void EveryFamilyHasExactlyOneRow(HudChipFamily family, HudRowKind row) =>
        Assert.Equal(row, HudChipRow.RowOf(family));

    /// <summary>The Founder asked to separate respawn from the FIGHT, not to scatter four
    /// windows: the spawn row owns Spawn alone, and the two rows partition the families.</summary>
    [Fact]
    public void TheTwoRowsPartitionTheFamiliesAndSpawnStandsAlone()
    {
        Assert.Equal([HudChipFamily.Spawn], HudChipRow.FamiliesOf(HudRowKind.Spawn));
        Assert.Equal([HudChipFamily.Mez, HudChipFamily.WatchFire, HudChipFamily.Buff],
            HudChipRow.FamiliesOf(HudRowKind.Fight));
        Assert.Equal(Enum.GetValues<HudChipFamily>().Length,
            HudChipRow.FamiliesOf(HudRowKind.Spawn).Count + HudChipRow.FamiliesOf(HudRowKind.Fight).Count);
    }

    /// <summary>ONE merge, cut in two: each row gets its own families in the merged order,
    /// and nothing is dropped or doubled between them.</summary>
    [Fact]
    public void ForRowSplitsOneMergeWithoutLosingOrDoublingAChip()
    {
        var merged = HudChipRow.Merge(
            mez: [Chip("skeleton")], spawn: [Chip("Kizdean Gix"), Chip("Fright")],
            watchFire: [Chip("Assist call")], buff: [Chip("Clarity")]);

        var fight = HudChipRow.ForRow(merged, HudRowKind.Fight);
        var spawn = HudChipRow.ForRow(merged, HudRowKind.Spawn);

        Assert.Equal(["skeleton", "Assist call", "Clarity"], fight.Select(e => e.Chip.Name));
        Assert.Equal(["Kizdean Gix", "Fright"], spawn.Select(e => e.Chip.Name));
        Assert.DoesNotContain(fight, e => e.Family == HudChipFamily.Spawn);
        Assert.All(spawn, e => Assert.Equal(HudChipFamily.Spawn, e.Family));
        Assert.Equal(merged.Count, fight.Count + spawn.Count);
    }

    /// <summary>A muted family is still absent from its row — the split sits downstream of
    /// the one place mute is applied, so it cannot bring a family back.</summary>
    [Fact]
    public void AMutedSpawnFamilyLeavesTheSpawnRowEmptyAndTheFightRowAlone()
    {
        var settings = new AppSettings { MutedChipFamilies = ["Spawn"] };
        var merged = HudChipRow.Merge([Chip("skeleton")], [Chip("Kizdean Gix")],
            order: HudChipRow.VisibleOrder(settings));

        Assert.Empty(HudChipRow.ForRow(merged, HudRowKind.Spawn));
        Assert.Single(HudChipRow.ForRow(merged, HudRowKind.Fight));
    }

    // ---- PLACE within a row ----

    /// <summary>The stored order still interleaves all four families; a nudge on the fight
    /// row must skip Spawn and swap with the next FIGHT family, or the click changes nothing
    /// the player can see on the row they clicked.</summary>
    [Fact]
    public void NudgeWithinSwapsWithTheNextFamilyOnTheSameRowAndLeavesSpawnInItsSlot()
    {
        IReadOnlyList<HudChipFamily> order =
            [HudChipFamily.Mez, HudChipFamily.Spawn, HudChipFamily.WatchFire, HudChipFamily.Buff];

        var moved = HudChipRow.NudgeWithin(order, HudChipFamily.Mez, +1);

        Assert.Equal(
            [HudChipFamily.WatchFire, HudChipFamily.Spawn, HudChipFamily.Mez, HudChipFamily.Buff],
            moved);
        // The negative: the plain Nudge is the swap this replaces, and it swaps with Spawn.
        Assert.Equal(
            [HudChipFamily.Spawn, HudChipFamily.Mez, HudChipFamily.WatchFire, HudChipFamily.Buff],
            HudChipRow.Nudge(order, HudChipFamily.Mez, +1));
    }

    [Fact]
    public void NudgeWithinIsANoOpAtEitherEndOfItsRow()
    {
        var order = HudChipRow.DefaultOrder;
        Assert.Equal(order, HudChipRow.NudgeWithin(order, HudChipFamily.Mez, -1));
        Assert.Equal(order, HudChipRow.NudgeWithin(order, HudChipFamily.Buff, +1));
        // Spawn is alone on its row, so it has nowhere to go in either direction.
        Assert.Equal(order, HudChipRow.NudgeWithin(order, HudChipFamily.Spawn, -1));
        Assert.Equal(order, HudChipRow.NudgeWithin(order, HudChipFamily.Spawn, +1));
    }

    [Fact]
    public void OrderForIsTheStoredOrderFilteredToTheRow()
    {
        var settings = new AppSettings { HudChipOrder = ["Buff", "Spawn", "Mez", "WatchFire"] };
        Assert.Equal([HudChipFamily.Buff, HudChipFamily.Mez, HudChipFamily.WatchFire],
            HudChipRow.OrderFor(settings, HudRowKind.Fight));
        Assert.Equal([HudChipFamily.Spawn], HudChipRow.OrderFor(settings, HudRowKind.Spawn));
    }

    // ---- The family → ink table ----

    /// <summary>Mez: name AND countdown in the theme's blue resource, never a hex.</summary>
    [Fact]
    public void MezDrawsNameAndCountdownInTheMezChipBrush()
    {
        var ink = HudChipRow.InkFor(HudChipFamily.Mez);
        Assert.Equal("MezChipBrush", ink.Name);
        Assert.Equal("MezChipBrush", ink.Countdown);
        Assert.Contains(HudChipRow.MezInk, ThemePalettes.Keys);
    }

    /// <summary>Respawn: the primary text ink on both runs — white on the dark palettes.</summary>
    [Fact]
    public void SpawnDrawsNameAndCountdownInThePrimaryTextInk()
    {
        var ink = HudChipRow.InkFor(HudChipFamily.Spawn);
        Assert.Equal("TextBrush", ink.Name);
        Assert.Equal("TextBrush", ink.Countdown);
    }

    /// <summary>THE NEGATIVE: blue is the mez family's alone, and the two families this slice
    /// did not touch keep the SA-2 look exactly.</summary>
    [Theory]
    [InlineData(HudChipFamily.Spawn)]
    [InlineData(HudChipFamily.WatchFire)]
    [InlineData(HudChipFamily.Buff)]
    public void NoOtherFamilyIsDrawnInTheMezBlue(HudChipFamily family)
    {
        var ink = HudChipRow.InkFor(family);
        Assert.NotEqual(HudChipRow.MezInk, ink.Name);
        Assert.NotEqual(HudChipRow.MezInk, ink.Countdown);
    }

    [Theory]
    [InlineData(HudChipFamily.WatchFire)]
    [InlineData(HudChipFamily.Buff)]
    public void TheUntouchedFamiliesKeepTextNameAndAccentCountdown(HudChipFamily family) =>
        Assert.Equal(new HudChipInk("TextBrush", "AccentBrush"), HudChipRow.InkFor(family));

    /// <summary>Due keeps WarnBrush in every family — D1 changed the resting inks, not the
    /// alarm — and a chip that is not due never borrows it.</summary>
    [Theory]
    [InlineData(HudChipFamily.Mez)]
    [InlineData(HudChipFamily.Spawn)]
    [InlineData(HudChipFamily.WatchFire)]
    [InlineData(HudChipFamily.Buff)]
    public void ADueCountdownIsWarnInEveryFamilyAndARestingOneIsTheFamilysInk(HudChipFamily family)
    {
        Assert.Equal("WarnBrush", HudChipRow.CountdownInk(new(family, Chip("x", due: true))));
        Assert.Equal(HudChipRow.InkFor(family).Countdown,
            HudChipRow.CountdownInk(new(family, Chip("x", due: false))));
    }

    // ---- Settings: each row reads and writes its own ----

    [Fact]
    public void AnUntouchedProfileHasBothRowsSlavedAndGrowingDown()
    {
        var settings = new AppSettings();
        foreach (var row in Enum.GetValues<HudRowKind>())
        {
            var (left, top) = HudChipRow.SavedPark(settings, row);
            Assert.False(HudChipRow.IsParked(left, top));
            Assert.False(HudChipRow.GrowsUp(settings, row));
        }
    }

    /// <summary>Parking one row leaves the other slaved, and the fight row is still the
    /// SA-2 pair — no migration moved an existing player's park to the wrong window.</summary>
    [Fact]
    public void ParkingOneRowLeavesTheOtherAloneAndTheFightRowKeepsTheSa2Pair()
    {
        var settings = new AppSettings();
        HudChipRow.SetPark(settings, HudRowKind.Spawn, 120, 240);

        Assert.Equal((120.0, 240.0), HudChipRow.SavedPark(settings, HudRowKind.Spawn));
        Assert.Equal((120.0, 240.0), (settings.SpawnRowParkLeft, settings.SpawnRowParkTop));
        Assert.True(double.IsNaN(settings.HudRowParkLeft));
        Assert.False(HudChipRow.IsParked(
            HudChipRow.SavedPark(settings, HudRowKind.Fight).Left,
            HudChipRow.SavedPark(settings, HudRowKind.Fight).Top));

        HudChipRow.SetPark(settings, HudRowKind.Fight, 300, 400);
        Assert.Equal((300.0, 400.0), (settings.HudRowParkLeft, settings.HudRowParkTop));
        Assert.Equal((120.0, 240.0), HudChipRow.SavedPark(settings, HudRowKind.Spawn));
    }

    [Fact]
    public void EachRowHasItsOwnGrowFlag()
    {
        var settings = new AppSettings();
        HudChipRow.SetGrowUp(settings, HudRowKind.Spawn, true);
        Assert.True(settings.SpawnRowGrowUp);
        Assert.False(settings.HudChipRowGrowUp);
        Assert.False(HudChipRow.GrowsUp(settings, HudRowKind.Fight));

        HudChipRow.SetGrowUp(settings, HudRowKind.Fight, true);
        HudChipRow.SetGrowUp(settings, HudRowKind.Spawn, false);
        Assert.True(settings.HudChipRowGrowUp);
        Assert.False(settings.SpawnRowGrowUp);
    }

    [Fact]
    public void TheNewSettingsRoundTripThroughTheProfile()
    {
        var settings = new AppSettings
        {
            SpawnRowParkLeft = 12, SpawnRowParkTop = 34, SpawnRowGrowUp = true,
        };
        settings.Save();
        var back = AppSettings.Load();
        Assert.Equal(12, back.SpawnRowParkLeft);
        Assert.Equal(34, back.SpawnRowParkTop);
        Assert.True(back.SpawnRowGrowUp);
    }

    /// <summary>A window title is an identity the shot and drag harnesses match on (trap 24):
    /// the two rows may never share one, and the fight row keeps SA-2's so every existing
    /// recipe still finds it.</summary>
    [Fact]
    public void TheTwoRowsHaveDistinctTitlesAndTheFightRowKeepsItsOwn()
    {
        Assert.Equal("EQBuddy HUD Chips", HudChipRow.WindowTitle(HudRowKind.Fight));
        Assert.Equal("EQBuddy Spawn Chips", HudChipRow.WindowTitle(HudRowKind.Spawn));
        Assert.NotEqual(HudChipRow.RowLabel(HudRowKind.Fight), HudChipRow.RowLabel(HudRowKind.Spawn));
    }

    // ---- Stacking: a slaved spawn row follows beyond the fight row ----

    [Fact]
    public void AFightRowBelowTheWidgetPushesTheSpawnRowFurtherDown()
    {
        // Widget at top 100, 40 tall; the fight row is below it at 144, 60 tall.
        var (below, above) = HudChipRow.OtherRowOccupies(true, 144, 60, hudTop: 100);
        Assert.Equal(60 + HudChipRow.HudGap, below);
        Assert.Equal(0, above);

        var (_, top) = HudChipRow.Placement(0, 100, 40 + below, 50, 0, 2000);
        Assert.Equal(100 + 40 + HudChipRow.HudGap + 60 + HudChipRow.HudGap, top);
        Assert.True(top >= 144 + 60, "the spawn row starts at or below the fight row's bottom");
    }

    [Fact]
    public void AFightRowAboveTheWidgetLiftsAGrowUpSpawnRowAboveIt()
    {
        // The fight row grew up: it stands at 36..96 above a widget whose top is 100.
        var (below, above) = HudChipRow.OtherRowOccupies(true, 36, 60, hudTop: 100);
        Assert.Equal(0, below);
        Assert.Equal(60 + HudChipRow.HudGap, above);

        var (_, top) = HudChipRow.Placement(0, 100, 40, 20, 0, 2000,
            growUp: true, aboveOccupied: above);
        Assert.True(top + 20 <= 36, "the spawn row's bottom clears the fight row's top");
    }

    /// <summary>A parked, hidden or unmeasured fight row occupies nothing beside the widget —
    /// otherwise the spawn row would leave a hole the player cannot explain.</summary>
    [Theory]
    [InlineData(false, 144, 60)]
    [InlineData(true, 144, 0)]
    [InlineData(true, 144, double.NaN)]
    [InlineData(true, double.NaN, 60)]
    public void AFightRowThatIsNotStandingBesideTheWidgetOccupiesNothing(
        bool slavedAndVisible, double top, double height) =>
        Assert.Equal((0.0, 0.0), HudChipRow.OtherRowOccupies(slavedAndVisible, top, height, 100));

    /// <summary>The default argument is every pre-D1 call, byte for byte.</summary>
    [Fact]
    public void PlacementWithNothingAboveIsUnchanged() =>
        Assert.Equal(
            HudChipRow.Placement(10, 300, 40, 50, 0, 768, growUp: true),
            HudChipRow.Placement(10, 300, 40, 50, 0, 768, growUp: true, aboveOccupied: 0));
}
