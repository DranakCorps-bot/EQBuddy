using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Zone level bands (DRA-84 D1, plan P1) — eqlwiki's <c>Level of Monsters</c> row, promoted
/// from the COMMITTED wikitext cache by <c>scripts/harvests/eqlwiki/zonelevels-transform.py</c>.
///
/// <para>This slice ships an INSTRUMENT and a measurement; no engine reads a band yet. So
/// what these tests hold is the two things a later gate will rest on and cannot check for
/// itself: <b>that a band is the wiki's number and never ours</b>, and <b>that a lookup
/// never hands one zone another zone's band</b>.</para>
///
/// <para>The lookup half is written as a must-list PAIRED with committed negatives (trap 34,
/// trap 78). The negatives are not hypothetical: every one of them is a real
/// <c>DropZones</c> spelling in the shipped item catalog that the longest-containment rule
/// <see cref="ZoneGraph.Resolve"/> uses for travel WOULD have matched to a band. A guard that
/// only proves the matches is a guard that would have passed on the looser rule.</para>
///
/// <para>Byte-reproducibility is the Python <c>--check</c> in <c>check.ps1</c> and CI, not
/// here — spawning Python from xunit would fail on a machine without it, and a test that
/// skips instead is vacuous coverage (same call <c>HarvestedGuidesTests</c> made).</para>
/// </summary>
public class ZoneLevelsTests
{
    private static readonly ZoneLevels Shipped = ZoneLevels.LoadEmbedded();

    private static string Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    // ---- The file is really there, and it is really the cache's answer ---------------------

    [Fact]
    public void TheShippedCatalogLoadsAndIsNotEmpty()
    {
        Assert.True(Shipped.BandCount > 0,
            "ZoneLevelBands.json did not load — an embedded resource that silently answers " +
            "nothing is a guard aimed at nothing (trap 78).");
        Assert.Equal(46, Shipped.BandCount);
        Assert.Equal(72, Shipped.AbsentCount);
        Assert.Equal(118, Shipped.BandCount + Shipped.AbsentCount);
    }

    /// <summary>The plan's two cited exhibits, quoted from the pages this session read.
    /// Crushbone is the Founder's Farm Gear complaint in one row; Rathe Mountains is the one
    /// a level rule can never refuse, and it is here so nobody later mistakes the band for
    /// the answer to that half.</summary>
    [Theory]
    [InlineData("Crushbone", 5, 20, "5-20")]
    [InlineData("Rathe Mountains", 13, 45, "13-45")]
    [InlineData("Greater Faydark", 1, 12, "1-12")]
    [InlineData("Najena", 8, 35, "8-35")]
    public void TheCitedBandsAreWhatTheWikiPageSays(string zone, int min, int max, string verbatim)
    {
        var band = Shipped.BandFor(zone);
        Assert.NotNull(band);
        Assert.Equal(min, band!.Min);
        Assert.Equal(max, band.Max);
        Assert.Equal(verbatim, band.Verbatim);
    }

    /// <summary>Every shipped band is arithmetically a band, AND its numbers re-derive from
    /// the verbatim it cites. A band whose numbers do not come out of its own quoted row is
    /// a number we made up with a citation stapled to it (trap 73).</summary>
    [Fact]
    public void EveryBandReDerivesFromTheRowItQuotes()
    {
        foreach (var zone in Shipped.BandedZones)
        {
            var band = Shipped.BandFor(zone)!;
            Assert.InRange(band.Min, 1, 99);
            Assert.InRange(band.Max, band.Min, 99);

            var parts = band.Verbatim.Split('-');
            var expected = parts.Length switch
            {
                1 => (int.Parse(parts[0]), int.Parse(parts[0])),
                2 => (int.Parse(parts[0]), int.Parse(parts[1])),
                _ => (-1, -1),
            };
            Assert.Equal(expected, (band.Min, band.Max));
        }
    }

    /// <summary>The refusal rule, asserted on its observable consequence: no shipped band was
    /// read out of an open top or a multi-range. These are the shapes the transform refuses,
    /// and refusing them is the whole reason the coverage is 46 of 118 rather than 103.</summary>
    [Fact]
    public void NoShippedBandCameFromAShapeTheTransformRefuses()
    {
        foreach (var zone in Shipped.BandedZones)
        {
            var verbatim = Shipped.BandFor(zone)!.Verbatim;
            Assert.DoesNotContain("+", verbatim, StringComparison.Ordinal);
            Assert.DoesNotContain(",", verbatim, StringComparison.Ordinal);
            Assert.DoesNotContain(" ", verbatim, StringComparison.Ordinal);
        }
    }

    /// <summary>…and the committed negatives that prove the refusal FIRES on real pages,
    /// each carrying the verbatim it refused so a later slice can see what it is deciding
    /// about. Without these the test above passes on a transform that reads nothing at
    /// all.</summary>
    [Theory]
    [InlineData("Plane of Sky", "50+")]
    [InlineData("Plane of Fear", "48+")]
    [InlineData("Temple of Veeshan", "60+")]
    [InlineData("Butcherblock Mountains", "1-15, 35")]
    [InlineData("Lesser Faydark", "10-30, 40-50")]
    [InlineData("The Arena", "n/a")]
    [InlineData("Surefall Glade", "?")]
    // The one that matters most: Qeynos Aqueducts has its OWN page, refused, and it sits one
    // containment hop from Qeynos's 1-9. Refused — not the city's band, and not Unknown
    // either, because we DID read its page.
    [InlineData("Qeynos Aqueducts", "1-15, 33-38")]
    public void ARefusedRowIsAbsentAndSaysWhatItRefused(string zone, string verbatim)
    {
        var answer = Shipped.Lookup(zone);
        Assert.Equal(ZoneLevels.Source.Refused, answer.Source);
        Assert.Null(answer.Band);
        Assert.Equal(verbatim, answer.Verbatim);
        Assert.Null(Shipped.BandFor(zone));
    }

    /// <summary>All four outcomes are reachable, each named with a real zone. "Absent" is
    /// three different facts and a surface that cannot tell them apart will say the wrong
    /// one out loud (trap 34's must-list).</summary>
    [Theory]
    [InlineData("Crushbone", ZoneLevels.Source.Banded)]
    [InlineData("Plane of Sky", ZoneLevels.Source.Refused)]
    [InlineData("Freeport", ZoneLevels.Source.NoRow)]
    [InlineData("Neriak", ZoneLevels.Source.NoRow)]
    [InlineData("Plane of Knowledge", ZoneLevels.Source.Unknown)]
    [InlineData("", ZoneLevels.Source.Unknown)]
    public void EveryOutcomeIsReachableWithARealName(string zone, ZoneLevels.Source expected) =>
        Assert.Equal(expected, Shipped.Lookup(zone).Source);

    [Fact]
    public void APageWithNoRowCarriesNoVerbatim()
    {
        var answer = Shipped.Lookup("Freeport");
        Assert.Equal(ZoneLevels.Source.NoRow, answer.Source);
        Assert.Equal("", answer.Verbatim);
    }

    // ---- The lookup fold: what it bridges, and what it must NOT -----------------------------

    /// <summary>Spellings an item record uses that are the same zone as a page we read. Each
    /// of these is a real <c>DropZones</c> value in the shipped catalog.</summary>
    [Theory]
    [InlineData("Estate of Unrest", "The Estate of Unrest")]
    [InlineData("The Great Divide", "Great Divide")]
    [InlineData("Warrens", "The Warrens")]
    [InlineData("Cazic-Thule", "Cazic Thule (Zone)")]
    [InlineData("Cazic Thule", "Cazic Thule (Zone)")]
    [InlineData("Gorge of King Xorbb (Beholder's Maze)", "Gorge of King Xorbb")]
    public void TheFoldBridgesASpellingToItsPage(string spelling, string title)
    {
        var expected = Shipped.Lookup(title);
        Assert.NotEqual(ZoneLevels.Source.Unknown, expected.Source);
        Assert.Equal(expected, Shipped.Lookup(spelling));
    }

    /// <summary>THE COMMITTED NEGATIVES. Every string here is a real <c>DropZones</c> value in
    /// the shipped item catalog that longest-containment matched to a band — measured, not
    /// imagined. Four are sub-zones or neighbours inheriting a band that is not theirs; the
    /// rest are markup and free prose sitting in a field that is supposed to hold a zone
    /// name. A band that is wrong is a number a surface will state as fact, so the fold
    /// answers Unknown and the gate that reads it does nothing.</summary>
    [Theory]
    [InlineData("Commonlands")]                     // containment → West Commonlands, 6-30
    [InlineData("North Qeynos")]                    // containment → Qeynos, 1-9
    [InlineData("South Qeynos")]
    [InlineData("Qeynos Catacombs")]
    [InlineData("Splitpaw")]                        // containment → Splitpaw Lair, 20-40
    [InlineData("Kaesora, Droga, Nurga")]           // three zones in one field
    [InlineData("Frontier Mountains, Mines of Nurga")]
    [InlineData("This drop is super ultra rare from any spider in kaesora.")]
    [InlineData("Kobolds in The Warrens and possibly Stonebrunt Mountains")]
    [InlineData("{{VeliousGray| Skyshrine }}")]
    [InlineData("{{VeliousGray|Crystal Caverns}}")]
    [InlineData("Timorous Deep {{Era|Kunark}}")]
    [InlineData("Greater Faydark<br>")]
    [InlineData("Burning Woods")]                   // a real alias we do NOT bridge — see below
    public void TheFoldRefusesWhatContainmentWouldHaveMatched(string spelling)
    {
        Assert.Equal(ZoneLevels.Source.Unknown, Shipped.Lookup(spelling).Source);
        Assert.Null(Shipped.BandFor(spelling));
    }

    /// <summary>"Burning Woods" IS "Burning Wood" — the map alias table already says so, and
    /// this fold does not read it. It is pinned as a KNOWN miss rather than quietly widened:
    /// bridging it means bringing a curated alias table into a band lookup, which is a
    /// decision for the slice that has a reason to make it. Delete this test when that
    /// happens; do not let it rot into a claim that the fold is complete.</summary>
    [Fact]
    public void AKnownAliasIsAKnownMissAndIsWrittenDownAsOne()
    {
        Assert.Equal(ZoneLevels.Source.Banded, Shipped.Lookup("Burning Wood").Source);
        Assert.Equal(ZoneLevels.Source.Unknown, Shipped.Lookup("Burning Woods").Source);
        Assert.Equal("burningwood", ZoneMapFiles.ExpectedShortname("Burning Woods"));
        Assert.Equal("burningwood", ZoneMapFiles.ExpectedShortname("Burning Wood"));
    }

    /// <summary>Two titles that fold onto one key and DISAGREE answer nothing. Built from a
    /// fixture rather than the shipped file, because the shipped collision
    /// (Chardok pre/post-revamp) happens to agree — a rule proven only where it cannot
    /// fire is not proven.</summary>
    [Fact]
    public void AnAmbiguousFoldAnswersNothing()
    {
        var levels = new ZoneLevels(
            new Dictionary<string, ZoneLevels.Band>
            {
                ["Chardok (Pre-Revamp)"] = new(30, 45, "30-45"),
                ["Chardok (Post-Revamp)"] = new(50, 60, "50-60"),
                ["Najena"] = new(20, 40, "20-40"),
            },
            new Dictionary<string, string>());

        Assert.Equal(ZoneLevels.Source.Unknown, levels.Lookup("Chardok").Source);
        // …but the exact titles still answer, and an unambiguous neighbour is untouched.
        Assert.Equal(30, levels.BandFor("Chardok (Pre-Revamp)")!.Min);
        Assert.Equal(20, levels.BandFor("Najena")!.Min);
    }

    [Fact]
    public void TwoTitlesThatFoldTogetherAndAgreeStillAnswer()
    {
        // The real shipped case: both Chardok pages give "50+", so both are Refused with the
        // same verbatim and the folded key is not ambiguous at all.
        var answer = Shipped.Lookup("Chardok");
        Assert.Equal(ZoneLevels.Source.Refused, answer.Source);
        Assert.Equal("50+", answer.Verbatim);
    }

    // ---- Is the data a per-zone fact, or a template? ----------------------------------------

    /// <summary>The distinct-count telltale (trap 73). 48 authored guide steps carrying ten
    /// distinct sentences between them was a template nobody noticed by reading. A per-zone
    /// band should be nearly as varied as the zones carrying it; a floor of two-thirds is
    /// loose enough for the genuine repeats (three zones really are 30-40) and tight enough
    /// that a parse which latched onto some shared infobox default would fail.</summary>
    [Fact]
    public void TheBandsAreAPerZoneFactRatherThanATemplate()
    {
        var distinct = Shipped.BandedZones
            .Select(z => Shipped.BandFor(z)!)
            .Select(b => (b.Min, b.Max))
            .Distinct()
            .Count();
        Assert.True(distinct * 3 >= Shipped.BandCount * 2,
            $"only {distinct} distinct bands across {Shipped.BandCount} zones — that reads " +
            "like a template, not the wiki's own per-zone numbers.");
    }

    // ---- The report ------------------------------------------------------------------------

    /// <summary>The report is the deliverable this slice was asked for as much as the data is:
    /// the gate P2 plans should be written against its numbers. So its headline counts are
    /// held to the committed file.
    ///
    /// <para>Its JOIN numbers are deliberately NOT asserted here. They are measured against
    /// <c>ItemCatalog.json.gz</c>, which DRA-84 D3's refresh rebuilds in a parallel seat;
    /// asserting them would redden that PR on a file it did not touch. The report says in its
    /// own text that the join is a snapshot and how to re-take it.</para></summary>
    [Fact]
    public void TheReportIsThereAndItsCountsMatchTheCommittedFile()
    {
        var path = Path.Combine(Root, "scripts", "harvests", "eqlwiki", "zonelevels-report.md");
        Assert.True(File.Exists(path),
            "zonelevels-transform.py writes zonelevels-report.md; it is not there.");
        var report = File.ReadAllText(path);

        // Every bucket, from the catalog's own numbers — the three ABSENT kinds are exactly
        // where a rule change moves rows without moving a total (trap 74's report half).
        Assert.Contains($"- Bands shipped in `ZoneLevelBands.json`: **{Shipped.BandCount}**",
            report, StringComparison.Ordinal);
        Assert.Contains($"- ABSENT — row present but not `N-M` or `N`: **{Shipped.RefusedCount}**",
            report, StringComparison.Ordinal);
        Assert.Contains($"- ABSENT — page has no `Level of Monsters` row: **{Shipped.NoRowCount}**",
            report, StringComparison.Ordinal);

        Assert.Contains("## Distinct-count telltale (trap 73)", report, StringComparison.Ordinal);
        Assert.Contains("## Refused verbatims", report, StringComparison.Ordinal);
        Assert.Contains("## The join", report, StringComparison.Ordinal);
        Assert.Contains("This half is a snapshot.", report, StringComparison.Ordinal);
    }
}
