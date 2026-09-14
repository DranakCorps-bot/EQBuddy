using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **ONE METRIC TABLE, ASSERTED AS ONE** (DRA-71 D6, Fable plan P8).
///
/// <para><c>GearLocker</c>'s comparison moved into Core so the Helper's catalog sweep could
/// use it "with the same metric table" — and "the same" is a claim that needs a test, because
/// the cheapest way for it to stop being true is for somebody to add a stat to one of the two
/// files. <see cref="TheLockerAndTheCoreTableCannotDisagree"/> runs both over the same pairs;
/// <see cref="EveryMetricThePairsTableNamesIsWeighed"/> is the must-list half (trap 34), since
/// a metric silently dropped from the table makes dominance EASIER and nothing else in the
/// repo would notice.</para>
///
/// <para>The behaviour rows that already existed live on in <c>GearLockerTests</c> and are
/// deliberately untouched: a lift that also changed an answer would make "behaviour-preserving"
/// unprovable by eye.</para>
/// </summary>
public class ItemDominanceTests
{
    private static ItemStatsBlock Block(params string[] lines) => ItemStatsBlock.Parse(lines);

    private static GearRow Row(string name, ItemStatsBlock stats) =>
        new(name, name, "worn · HEAD", 1, stats, "", "", "");

    // ---- the lift preserved the answer -------------------------------------------------

    /// <summary>
    /// **The Locker and Core answer identically, over every arrangement that has ever
    /// mattered.**
    ///
    /// <para>Both directions of each pair, because dominance is not symmetric and a delegation
    /// that swapped its arguments would pass a one-directional check perfectly.</para>
    /// </summary>
    [Fact]
    public void TheLockerAndTheCoreTableCannotDisagree()
    {
        (string Name, ItemStatsBlock Stats)[] items =
        [
            ("Plain Helm", Block("Slot: HEAD", "AC: 4")),
            ("Better Helm", Block("Slot: HEAD", "AC: 9")),
            ("Mixed Helm", Block("Slot: HEAD", "AC: 9", "STR: -2")),
            ("Statted Helm", Block("Slot: HEAD", "AC: 9", "STR: +5")),
            ("Paladin Helm", Block("Slot: HEAD", "AC: 20", "Class: PAL")),
            ("Sword", Block("Slot: PRIMARY", "Skill: 1H Slashing", "DMG: 10", "Atk Delay: 26")),
            ("Better Sword", Block("Slot: PRIMARY", "Skill: 1H Slashing", "DMG: 14", "Atk Delay: 26")),
            ("Plain Helm +5", Block("Slot: HEAD", "AC: 4")),
        ];
        string[][ ] classSets = [[], ["PAL"], ["WAR", "RNG"]];

        var compared = 0;
        foreach (var classes in classSets)
            foreach (var a in items)
                foreach (var b in items)
                {
                    Assert.Equal(
                        GearLocker.Dominates(Row(b.Name, b.Stats), Row(a.Name, a.Stats), classes),
                        ItemDominance.Dominates(b.Name, b.Stats, a.Name, a.Stats, classes));
                    Assert.Equal(
                        GearLocker.CanClaimUpgrade(
                            Row(b.Name, b.Stats), Row(a.Name, a.Stats), classes),
                        ItemDominance.CanClaimUpgrade(
                            b.Name, b.Stats, a.Name, a.Stats, classes));
                    compared++;
                }

        // The sweep's own trap 78: a loop that compared nothing would agree perfectly.
        Assert.Equal(items.Length * items.Length * classSets.Length, compared);
        Assert.True(compared > 100);
    }

    /// <summary>The "+N" rule is one function now and the Locker still asks the same
    /// question of it.</summary>
    [Theory]
    [InlineData("Crushbone Belt", 0)]
    [InlineData("Crushbone Belt +5", 5)]
    [InlineData("Crushbone Belt +12 ", 12)]
    [InlineData("Belt of +Something", 0)]
    public void TheTierSuffixIsOneReading(string name, int tier)
    {
        Assert.Equal(tier, ItemDominance.UpgradeTier(name));
        Assert.Equal(tier, GearLocker.UpgradeTier(name));
    }

    // ---- the must-list half ------------------------------------------------------------

    /// <summary>
    /// **Every number the pairs table names is actually weighed, proved one metric at a
    /// time.**
    ///
    /// <para>A forbid-rule cannot see a metric that went MISSING, and a dropped metric makes
    /// dominance strictly easier — the comparison would still return true and false, just
    /// about the wrong thing. So each metric is driven on its own: an item that is worse on
    /// only THAT number must not dominate, and an item better on only that number must.</para>
    /// </summary>
    [Fact]
    public void EveryMetricThePairsTableNamesIsWeighed()
    {
        (string Line, string Metric)[] metrics =
        [
            ("AC: {0}", "AC"), ("HP: {0}", "HP"), ("Mana: {0}", "Mana"),
            ("STR: {0}", "STR"), ("WIS: {0}", "WIS"), ("SV FIRE: {0}", "SV FIRE"),
        ];

        foreach (var (line, metric) in metrics)
        {
            var low = Block("Slot: HEAD", string.Format(line, 4));
            var high = Block("Slot: HEAD", string.Format(line, 9));

            Assert.True(ItemDominance.Dominates("high", high, "low", low, []),
                $"{metric} is not being weighed at all — a better {metric} did not dominate.");
            Assert.False(ItemDominance.Dominates("low", low, "high", high, []),
                $"{metric} is not being weighed at all — a worse {metric} dominated anyway.");
            Assert.Equal(metric, ItemDominance.Gain(high, low)?.Metric);
        }

        // And the named table covers the block's own vocabulary rather than a subset somebody
        // typed: every metric asserted above appears in MetricPairs for a pair that carries it.
        var names = ItemDominance.MetricPairs(
                Block("Slot: HEAD", "AC: 1", "HP: 1", "Mana: 1", "STR: 1", "WIS: 1", "SV FIRE: 1"),
                Block("Slot: HEAD"))
            .Select(p => p.Metric).ToList();
        foreach (var (_, metric) in metrics) Assert.Contains(metric, names);
        Assert.Contains("ratio", names);
        Assert.Contains("DMG", names);
    }

    // ---- Gain --------------------------------------------------------------------------

    /// <summary>The LARGEST improvement, named — and the metric's own spelling, so the
    /// sentence cannot name something the comparison did not weigh.</summary>
    [Fact]
    public void GainNamesTheBiggestImprovementAndItsOwnMetric()
    {
        var worn = Block("Slot: HEAD", "AC: 4", "STR: +2");
        var candidate = Block("Slot: HEAD", "AC: 6", "STR: +14");

        var gain = ItemDominance.Gain(candidate, worn);
        Assert.NotNull(gain);
        Assert.Equal("STR", gain!.Value.Metric);
        Assert.Equal(12, gain.Value.By);
    }

    /// <summary>Weapon ratio is a fraction and is reported as one — an upgrade whose only
    /// improvement is 0.15 of a ratio must not come back as "0".</summary>
    [Fact]
    public void GainCarriesTheWeaponRatioAsAFraction()
    {
        var worn = Block("Slot: PRIMARY", "Skill: 1H Slashing", "DMG: 20", "Atk Delay: 26");
        var candidate = Block("Slot: PRIMARY", "Skill: 1H Slashing", "DMG: 24", "Atk Delay: 26");

        var gain = ItemDominance.Gain(candidate, worn);
        Assert.NotNull(gain);
        // DMG moved by 4 and the ratio by ~0.15, so the LARGEST is DMG — which is the point:
        // the ratio is in the table and is not the one being reported here.
        Assert.Equal("DMG", gain!.Value.Metric);
        Assert.Contains(ItemDominance.MetricPairs(candidate, worn),
            p => p.Metric == "ratio" && p.B > p.A);
    }

    /// <summary>Nothing improved answers null, so a caller cannot be handed a row with no
    /// reason on it.</summary>
    [Fact]
    public void GainIsNullWhenNothingImproved() =>
        Assert.Null(ItemDominance.Gain(
            Block("Slot: HEAD", "AC: 4"), Block("Slot: HEAD", "AC: 4")));
}
