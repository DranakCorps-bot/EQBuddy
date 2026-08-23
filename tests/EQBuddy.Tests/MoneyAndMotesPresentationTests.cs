using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// What the Money, Motes and Faction cards say (Gate 5b) — three more cards whose content
/// is asserted without launching a window. Coin formatting itself is Core's and tested
/// there; what was untested is which numbers appear, when a line is worth printing, and
/// what an empty card says.
/// </summary>
public class MoneyAndMotesPresentationTests
{
    // ---- money ----

    [Fact]
    public void TheMoneySummaryNamesBothSourcesAndBothRates()
    {
        var lines = MoneyPresentation.SummaryLines(new StatsSnapshot
        {
            CorpseCopper = 12345, CoinDrops = 7, BiggestDrop = 5000,
            VendorCopper = 60000, SalesCount = 3,
            CopperPerHour = 30000, CopperPerActiveHour = 45000,
        });

        Assert.Equal(3, lines.Count);   // no recent window → no fourth line
        Assert.Contains("Corpses", lines[0]);
        Assert.Contains("7 drops", lines[0]);
        Assert.Contains("Merchant sales", lines[1]);
        Assert.Contains("3 sales", lines[1]);
        Assert.Contains("per hour", lines[2]);
        Assert.Contains("per active hour", lines[2]);
    }

    /// <summary>"Last 0m: 0" reads as a dead session rather than as a measurement nobody
    /// has taken yet, so the line is absent instead of empty.</summary>
    [Fact]
    public void TheRecentLineAppearsOnlyWhenThereIsARecentWindow()
    {
        var withWindow = MoneyPresentation.SummaryLines(new StatsSnapshot
        {
            Recent = new RecentRates(TimeSpan.FromMinutes(15), true, 0, 0, 0, 4200, 0, 0),
        });
        Assert.Equal(4, withWindow.Count);
        Assert.StartsWith("Last 15m:", withWindow[3], StringComparison.Ordinal);
    }

    /// <summary>Sold items are drops too (#74), so they are ITEMS — clickable, hoverable,
    /// quest-badgeable — exactly like the Loot card's rows.</summary>
    [Fact]
    public void SoldItemsAreItemRows()
    {
        var rows = MoneyPresentation.SoldRows(new StatsSnapshot
        {
            SoldItems = [new SoldDetail("Rusty Dagger", 1, 210)],
        });
        Assert.True(Assert.Single(rows).Item);
    }

    /// <summary>A stack of one prints no count: "×1" on every row is noise, and the name
    /// has to stay a clean lookup key.</summary>
    [Fact]
    public void ASingleSoldItemPrintsNoCount()
    {
        var rows = MoneyPresentation.SoldRows(new StatsSnapshot
        {
            SoldItems = [new SoldDetail("Rusty Dagger", 1, 210), new SoldDetail("Bone Chips", 4, 80)],
        });
        Assert.DoesNotContain("×", rows[0].Value);
        Assert.Contains("×4", rows[1].Value);
    }

    // ---- motes ----

    /// <summary>An empty card explains itself. "0 motes/hr" is a measurement; this card is
    /// legitimately empty for long stretches and must not read as broken.</summary>
    [Fact]
    public void AnEmptyMotesCardExplainsItselfRatherThanReportingZero()
    {
        var text = MotesPresentation.Summary(new MotesSummary(0, 0, []));
        Assert.DoesNotContain("0 motes/hr", text);
        Assert.Contains("No motes yet", text);
    }

    [Fact]
    public void MotesReportTheirRateOnceThereAreAny()
    {
        var text = MotesPresentation.Summary(new MotesSummary(12, 3.45, []));
        Assert.Contains("3.5 motes/hr", text);
    }

    /// <summary>
    /// The one-line form the Progress Experience room shows (David, 2026-08-23) and the
    /// Motes card's own header both come from here.
    ///
    /// **Null rather than "0 motes/hr" when nothing has dropped.** The Progress summary
    /// block already omits the AA line and the ETA on that principle, and a rate of
    /// nothing reads as a measurement of the camp rather than as "none yet" — which is the
    /// same argument <see cref="AnEmptyMotesCardExplainsItselfRatherThanReportingZero"/>
    /// wins for the card, where the answer is a sentence instead of an omission because a
    /// card cannot omit itself.
    /// </summary>
    [Fact]
    public void TheRateLineCarriesCountAndRateAndSaysNothingWhenThereAreNone()
    {
        Assert.Null(MotesPresentation.RateLine(MotesSummary.Empty));
        Assert.Equal("1 mote · 0.9/hr", MotesPresentation.RateLine(new MotesSummary(1, 0.9, [])));
        Assert.Equal("12 motes · 3.5/hr", MotesPresentation.RateLine(new MotesSummary(12, 3.45, [])));
    }

    /// <summary>One formatter, not four. The Motes card header and the Progress line are
    /// the same string — two formatters for one rate is how the Wealth chip came to name a
    /// rate the body underneath refused to show (Bevel, 2026-08-22).</summary>
    [Fact]
    public void TheProgressLineAndTheMotesHeaderAreTheSameString()
    {
        var snap = new StatsSnapshot
        {
            Elapsed = TimeSpan.FromHours(2),
            Loot = [new LootDetail("Mote of Lesser Potential", 3, "a ghoul")],
        };

        var line = Assert.Single(ProgressPresentation.SummaryLines(snap), l => l.Contains("mote"));
        Assert.Equal(ProgressTheme.MoteRate(snap), line);
        Assert.Equal("3 motes · 1.5/hr", line);
        // And it is absent, not zeroed, on a session that has looted none.
        Assert.DoesNotContain(ProgressPresentation.SummaryLines(new StatsSnapshot()),
            l => l.Contains("mote"));
    }

    /// <summary>Motes are items — they click through to the wiki like any other drop.</summary>
    [Fact]
    public void MoteTiersAreItemRows()
    {
        var rows = MotesPresentation.Rows(
            new MotesSummary(3, 1, [new MoteTierCount("Mote of Lesser Potential", 3)]));
        var row = Assert.Single(rows);
        Assert.True(row.Item);
        Assert.Equal("×3", row.Value);
    }

    // ---- faction ----

    /// <summary>A gain reads Good and a loss reads Bad, as palette KEYS — so the card can
    /// never go off-palette, and the rule is assertable with no window.</summary>
    [Fact]
    public void FactionValuesCarryTheirStateAsAPaletteKey()
    {
        var rows = FactionFormat.Rows(
        [
            new FactionDetail("Guards of Qeynos", Hits: 2, Net: 12),
            new FactionDetail("Crushbone Orcs", Hits: 5, Net: -30),
        ]);

        Assert.Equal("GoodBrush", rows[0].ValueInk);
        Assert.Equal("BadBrush", rows[1].ValueInk);
    }

    /// <summary>A faction that never moved is not a loss. The sign test reads the string a
    /// player actually sees, so a "+0" can never be painted red.</summary>
    [Fact]
    public void AnUnmovedFactionIsNotALoss()
    {
        var row = Assert.Single(FactionFormat.Rows(
            [new FactionDetail("Antonican Guards", Hits: 0, Net: 0)]));
        Assert.Equal("GoodBrush", row.ValueInk);
    }
}
