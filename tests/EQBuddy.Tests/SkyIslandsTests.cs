using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Reading the island out of a Sky step's own prose (David, 2026-08-23, from a Reddit ask:
/// group Sky quest steps by island, sorted numerically).
///
/// The catalog has no island FIELD — the fact is written by hand into <c>Source</c>, in five
/// shapes across 223 steps. Every case below is taken from the shipped catalog rather than
/// invented, which is what makes this a parser test and not a regex test.
/// </summary>
public class SkyIslandsTests
{
    [Theory]
    [InlineData("Isle 4: Keeper of Souls", 4)]
    [InlineData("Isle 6: Bazzt Zzzt", 6)]
    [InlineData("Isle 7: sphinxes, drakes and undine spirits", 7)]
    // Spelled out, and with a dash instead of a colon — both real, both in the catalog.
    [InlineData("Isle four - griffons and pegasus", 4)]
    [InlineData("Isle two - Protector of Sky", 2)]
    [InlineData("Isle five - The Spiroc Lord", 5)]
    [InlineData("Isle six - bees", 6)]
    public void OneIslandIsReadWhicheverWayItIsWritten(string source, double expected) =>
        Assert.Equal([expected], SkyIslands.Parse(source));

    /// <summary>Sky's second stop really is called 1.5, which is why islands are doubles and
    /// why "sorted numerically" needed saying: sorted as text, 1.5 lands after 1 and before
    /// 2 by luck, and "10" would land between 1 and 2 by the same luck.</summary>
    [Fact]
    public void TheHalfIslandSurvives()
    {
        Assert.Equal([SkyIslands.HalfIsland], SkyIslands.Parse("Isle 1.5: Noble Dojorn"));
        Assert.Equal("Island 1.5", SkyIslands.Heading(SkyIslands.HalfIsland));
        Assert.Equal("Island 8", SkyIslands.Heading(8));
    }

    /// <summary>The 22 three-island steps, verbatim from the catalog. Ascending, deduplicated,
    /// and all three kept — dropping any of them would silently narrow where a player thinks
    /// an item can be found.</summary>
    [Fact]
    public void AllThreeIslandsAreKeptAndSorted() =>
        Assert.Equal([1.5, 4, 8], SkyIslands.Parse(
            "Isle eight: the Hand of Veeshan; Isle four: Overseer of Air; Isle 1.5: Noble Dojorn"));

    /// <summary>**"No island" is a true answer, not a parse failure.** 95 of 223 steps say
    /// "Trash mobs" because Wind Runes drop anywhere on the plane. Treating that as missing
    /// data would put a confident wrong island on nearly half the checklist.</summary>
    [Theory]
    [InlineData("Trash mobs")]
    [InlineData("")]
    [InlineData(null)]
    public void NoIslandNamedMeansNoIsland(string? source) => Assert.Empty(SkyIslands.Parse(source));

    /// <summary>The negative that keeps the regex honest (trap 39). A number in prose is not
    /// an island, and neither is a word that merely follows "Isle".</summary>
    [Theory]
    [InlineData("Dropped by 2 spawns near the tower")]
    [InlineData("Isle of Dread")]
    [InlineData("6 of them patrol here")]
    public void ANumberInProseIsNotAnIsland(string source) => Assert.Empty(SkyIslands.Parse(source));

    /// <summary>**The grouping created a redundancy and this removes it.** A row under
    /// "Island 6" was reading "Josin Faithbringer · Isle 6: Bazzt Zzzt" — the island twice in
    /// eight words. Only the leading label goes; the mob that drops it is what the detail
    /// column was always for.</summary>
    [Theory]
    [InlineData("Isle 6: Bazzt Zzzt", "Bazzt Zzzt")]
    [InlineData("Isle four - griffons and pegasus", "griffons and pegasus")]
    [InlineData("Isle 7: 'drake/sphinx/spirit' type mobs", "'drake/sphinx/spirit' type mobs")]
    [InlineData("Isle two - Protector of Sky", "Protector of Sky")]
    public void TheIslandLabelComesOffARowThatSitsUnderIt(string source, string expected) =>
        Assert.Equal(expected, SkyIslands.WithoutIslePrefix(source));

    /// <summary>What it must NOT touch. A multi-island step keeps every word — it sits under
    /// "Several islands", so those three names are the only place a player learns where to
    /// go. Prose with no label keeps itself. And a source that is ONLY a label keeps itself,
    /// because an empty detail column reads as data that failed to load rather than as data
    /// that was already said.</summary>
    [Theory]
    [InlineData("Isle eight: the Hand of Veeshan; Isle four: Overseer of Air; Isle 1.5: Noble Dojorn")]
    [InlineData("Trash mobs")]
    [InlineData("Isle 6")]
    [InlineData("")]
    public void NothingElseIsStripped(string source) =>
        Assert.Equal(source, SkyIslands.WithoutIslePrefix(source));

    /// <summary>Every step in the SHIPPED catalog parses to something defensible — either an
    /// island in Sky's real range, or nothing at all. This is the assertion that would catch
    /// a future catalog edit inventing a sixth spelling, which is exactly how the existing
    /// five accumulated.</summary>
    [Fact]
    public void EveryShippedSkyStepParsesToARealIslandOrToNothing()
    {
        var items = SkyQuestDefaults.Items;
        Assert.NotEmpty(items);

        foreach (var island in items.SelectMany(i => SkyIslands.Parse(i.Source)))
        {
            Assert.True(island is >= 1 and <= 8,
                $"Island {island} is not a Plane of Sky island — check the Source prose it came from.");
            // 1.5 is the only fractional one the plane has.
            Assert.True(island % 1 == 0 || island == SkyIslands.HalfIsland,
                $"Island {island} is fractional and is not the half-island.");
        }

        // And the split is the one the design was reasoned about, so a catalog edit that
        // moves it a long way shows up here rather than in a player's screenshot.
        var grouped = items.Count(i => SkyIslands.Parse(i.Source).Count == 1);
        var several = items.Count(i => SkyIslands.Parse(i.Source).Count > 1);
        var anywhere = items.Count(i => SkyIslands.Parse(i.Source).Count == 0);
        Assert.Equal(items.Length, grouped + several + anywhere);
        Assert.True(grouped > several && grouped > 0, "most located steps name exactly one island");
        Assert.True(anywhere > 0, "the trash-mob steps must stay ungrouped");
    }
}
