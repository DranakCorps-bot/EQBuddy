using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The strip vocabularies (docs/DesignSystem.md §11.2). These are the WORDS half of the
/// app's most-rebuilt control — which options a surface offers, in what order, and what
/// each is called — and until Gate 5 every one of them was typed inline in each UI.
///
/// The watch-rule strip is the case that had actually drifted into a bug shape: both UIs
/// held the same four <c>(mode, label)</c> tuples and compared them to a STRING setting,
/// so a key typed differently in one lane would light no chip at all and the strip would
/// silently offer four unselected options.
/// </summary>
public class SortStripTests
{
    [Fact]
    public void EveryWatchOptionKeyIsOneTheSettingCanHold()
    {
        // The keys the render code branches on, in both UIs. "manual" is the default
        // branch rather than a case, which is exactly why it has to be spelled the same.
        string[] understood = ["manual", "alpha", "total", "recent"];
        Assert.Equal(understood, SortStrip.ForWatchRules.Select(o => o.Key));
    }

    [Fact]
    public void TheStoredDefaultSelectsAChip()
    {
        // A strip whose stored value matches no key paints nothing as selected — the
        // silent no-op failure, on a control whose whole job is saying which one is on.
        var stored = new AppSettings().WatchSortMode;
        Assert.Contains(SortStrip.ForWatchRules, o => o.Key == stored);
    }

    [Fact]
    public void WatchOptionLabelsAreLowerCaseAndGlyphFree()
    {
        foreach (var option in SortStrip.ForWatchRules)
        {
            Assert.Equal(option.Label.ToLowerInvariant(), option.Label);
            // Including the tooltips: the "manual" tip used to point at ▲▼, which is
            // tofu on the Wine prefixes the Linux/macOS build runs under (#148, #166).
            foreach (var rune in (option.Label + (option.Tip ?? "")).EnumerateRunes())
                Assert.False(rune.Value is >= 0x2190 and <= 0x2BFF or >= 0x1F300 and <= 0x1FAFF
                        && rune.Value is not (0x2013 or 0x2014 or 0x2026),
                    $"'{option.Label}' carries the glyph U+{rune.Value:X4}");
        }
    }

    [Fact]
    public void MetricStripsKeepHealingsOwnWording()
    {
        // Healing counts CASTS and rates in HPS. Both UIs derived that from a substring
        // test on the heading text, in two places each.
        Assert.Contains(SortStrip.ForHealing, o => o.Label == "casts");
        Assert.Contains(SortStrip.ForHealing, o => o.Label == "hps");
        Assert.DoesNotContain(SortStrip.ForDamage, o => o.Label == "casts");
    }

    [Fact]
    public void DamageTakenOffersNoRate()
    {
        // Incoming damage ÷ your own combat time is a number with no meaning, and
        // offering it invites the reading that it is somebody's DPS on you.
        Assert.DoesNotContain(SortStrip.ForDamageTaken, o => o.Metric == SortStrip.Metric.Rate);
    }

    [Theory]
    [InlineData("hits", SortStrip.Metric.Hits)]
    [InlineData("avg", SortStrip.Metric.Avg)]
    [InlineData("rate", SortStrip.Metric.Rate)]
    [InlineData("total", SortStrip.Metric.Total)]
    [InlineData("nonsense", SortStrip.Metric.Total)]
    [InlineData(null, SortStrip.Metric.Total)]
    public void UnrecognisedStoredMetricsFallBackToTotal(string? stored, SortStrip.Metric expected)
    {
        Assert.Equal(expected, SortStrip.Parse(stored));
        Assert.Equal(SortStrip.Parse(stored), SortStrip.Parse(SortStrip.Key(SortStrip.Parse(stored))));
    }
}
