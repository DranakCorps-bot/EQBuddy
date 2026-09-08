using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The ranked-duration ledger and the arithmetic that turns a length into a countdown.
///
/// **This suite is the thing that keeps "measured, not invented" true.** Helm's standing
/// posture from #414 is *do not invent ranked durations / mote multipliers*, and a posture
/// that lives only in a channel file is one nobody can check at merge time. So every row in
/// `RankedBuffDurations.json` has to reproduce its OWN recorded observation through the same
/// <see cref="BuffDurationModel.WithReinforcement"/> the tracker uses: a row somebody typed
/// from a hunch cannot pass, because there is no observation for it to land on.
/// </summary>
public class RankedBuffDurationTests
{
    private static readonly RankedBuffDurationLedger Shipped = RankedBuffDurationLedger.Default;

    // ---- The model ------------------------------------------------------------------

    /// <summary>
    /// The owner's two measurements, as the arithmetic that produces them (level 50 Druid,
    /// Spell Casting Reinforcement rank 1 of 4, 2026-09-08):
    ///
    ///   Shield of Thorns V — 1,350 × 1.05 = 1,417.5 → floor to a 6 s tick → 1,416 = 23:36
    ///   Chloroplast V      — 1,200 × 1.05 = 1,260 exactly                → 1,260 = 21:00
    ///
    /// The tick floor is not decoration: without it Thorns comes out at 1,417.5, which is a
    /// second and a half no server ever counted and a number the owner's stopwatch never saw.
    /// </summary>
    [Theory]
    [InlineData(1350d, 1, 1416d)]   // Shield of Thorns V, owner-measured 23:36
    [InlineData(1200d, 1, 1260d)]   // Chloroplast V, owner-measured 21:00
    public void ReinforcementStretchesThenFloorsToAServerTick(double baseSeconds, int rank, double expected)
        => Assert.Equal(expected, BuffDurationModel.WithReinforcement(baseSeconds, rank));

    /// <summary>Rank 0 is "the AA ledger has never seen this ability", and a rank past the
    /// top of the table is a value we have no number for. Both hand the base straight back —
    /// guessing a multiplier is exactly what this file is not allowed to do.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5)]
    public void ARankWeHaveNoNumberForLeavesTheLengthAlone(int rank)
        => Assert.Equal(1350d, BuffDurationModel.WithReinforcement(1350d, rank));

    /// <summary>SCR is spent ONCE. Feeding a stretched length back through the model is the
    /// double-application the owner's kick asked us to rule out, and it is a different
    /// number — so if anything ever starts doing it, this says so.</summary>
    [Fact]
    public void ApplyingReinforcementTwiceIsNotTheSameAsApplyingItOnce()
    {
        var once = BuffDurationModel.WithReinforcement(1350d, 1);
        Assert.NotEqual(once, BuffDurationModel.WithReinforcement(once, 1));
        Assert.Equal(1416d, once);                                           // 23:36, measured
        Assert.Equal(1482d, BuffDurationModel.WithReinforcement(once, 1));   // 24:42, 66 s long
    }

    // ---- The shipped ledger ---------------------------------------------------------

    [Fact]
    public void TheShippedLedgerCarriesTheTwoOwnerMeasuredRows()
    {
        Assert.Equal(1350d, Shipped.BaseSeconds("Shield of Thorns V"));
        Assert.Equal(1200d, Shipped.BaseSeconds("Chloroplast V"));
    }

    /// <summary>
    /// EVERY row reproduces its own measurement. This is the guard, and it is why a row is a
    /// record of an observation rather than a number in a file: base × the measurer's rank,
    /// floored, must equal what the measurer actually saw.
    /// </summary>
    [Fact]
    public void EveryRowReproducesTheObservationItClaimsToComeFrom()
    {
        Assert.NotEmpty(Shipped.Entries);
        foreach (var e in Shipped.Entries)
        {
            var m = e.Measured;
            Assert.True(m is not null, $"{e.Name}: no measurement — a ranked duration may not be invented");
            Assert.False(string.IsNullOrWhiteSpace(m!.By), $"{e.Name}: measurement names nobody");
            Assert.False(string.IsNullOrWhiteSpace(m.On), $"{e.Name}: measurement is undated");
            Assert.True(m.ObservedSeconds > 0, $"{e.Name}: measurement observed nothing");
            Assert.Equal(m.ObservedSeconds,
                BuffDurationModel.WithReinforcement(e.BaseSeconds, m.ReinforcementRank));
        }
    }

    /// <summary>PROVE-FAIL for the guard above (trap 34: a green-only guard is vacuous
    /// coverage). A row whose base does not land on its own observation is refused, and so is
    /// one with no observation at all.</summary>
    [Fact]
    public void ARowThatDoesNotReproduceItsMeasurementWouldBeCaught()
    {
        var wrong = new RankedBuffDurationLedger.Entry
        {
            Name = "Shield of Thorns V",
            BaseSeconds = 900,   // the wiki's rank-I number, wearing a rank-V name
            Measured = new RankedBuffDurationLedger.Measurement
            {
                By = "David (owner)", On = "2026-09-08", ObservedSeconds = 1416, ReinforcementRank = 1,
            },
        };
        Assert.NotEqual(wrong.Measured.ObservedSeconds,
            BuffDurationModel.WithReinforcement(wrong.BaseSeconds, wrong.Measured.ReinforcementRank));

        var invented = new RankedBuffDurationLedger.Entry { Name = "Shield of Thorns V", BaseSeconds = 1350 };
        Assert.Null(invented.Measured);
    }

    /// <summary>
    /// Every row is keyed on a name the LOG can write, and on a RANKED one.
    ///
    /// Both halves matter. A parenthetical wiki title can never meet a cast line
    /// (<see cref="SpellCatalog.IsLogWritableName"/> — that was #414's whole defect), and an
    /// UNRANKED name here would be a second source for a length `BuffDurations.json` already
    /// owns, which is trap 4 with the two producers one folder apart.
    /// </summary>
    [Fact]
    public void EveryRowIsALogWritableRankedName()
    {
        foreach (var e in Shipped.Entries)
        {
            Assert.True(SpellCatalog.IsLogWritableName(e.Name), $"{e.Name}: not a name the log writes");
            Assert.NotEqual(e.Name, SpellCatalog.BaseName(e.Name));   // i.e. it carries a rank
        }
    }

    /// <summary>A ranked row whose BASE name is not in the buff catalog can never be reached:
    /// the landing line is what opens a countdown, and only `BuffDurations.json` maps a
    /// landing to a spell. A row nothing can reach is a wrong duration waiting for the day
    /// someone adds the landing (trap 43's shape — written, never read).</summary>
    [Fact]
    public void EveryRowsBaseNameIsReachableFromALandingLine()
    {
        foreach (var e in Shipped.Entries)
            Assert.True(BuffDurationCatalog.Default.IsBuffSpell(e.Name),
                $"{e.Name}: no landing line in BuffDurations.json maps to it");
    }

    /// <summary>The lookup is EXACT. Handing rank V's measurement to a rank II cast is the
    /// same defect this ledger fixes, pointing the other way — an unmeasured rank falls back
    /// to the wiki base rather than borrowing a neighbour's number.</summary>
    [Theory]
    [InlineData("Shield of Thorns")]
    [InlineData("Shield of Thorns II")]
    [InlineData("Chloroplast")]
    [InlineData("Chloroplast IV")]
    public void AnUnmeasuredRankBorrowsNothing(string spell)
        => Assert.Null(Shipped.BaseSeconds(spell));

    /// <summary>Whitespace and case are the log's business, not a reason to miss a row.</summary>
    [Theory]
    [InlineData("  Shield of Thorns V  ")]
    [InlineData("shield of thorns v")]
    public void TheLookupIsForgivingAboutCaseAndPadding(string spell)
        => Assert.Equal(1350d, Shipped.BaseSeconds(spell));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Armor of Faith")]
    public void AnAbsentNameIsNull(string spell) => Assert.Null(Shipped.BaseSeconds(spell));
}
