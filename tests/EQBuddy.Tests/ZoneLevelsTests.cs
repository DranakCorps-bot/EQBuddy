using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Zone level bands (DRA-84 D1, plan P1; open top learned in D2) — eqlwiki's
/// <c>Level of Monsters</c> row, promoted from the COMMITTED wikitext cache by
/// <c>scripts/harvests/eqlwiki/zonelevels-transform.py</c>.
///
/// <para>What these tests hold is the two things the D2 gate rests on and cannot check for
/// itself: <b>that a band is the wiki's number and never ours</b>, and <b>that a lookup
/// never hands one zone another zone's band</b>. <c>RecommendationsGearTests</c> owns what the
/// gate does with them.</para>
///
/// <para><b>D2 widened the admitted shapes to four, and the widening is guarded from both
/// sides.</b> The open top (`50+`, `30-50+`) is now a band with a null <c>Max</c> on Helm's
/// named ruling; what stayed refused — multi-range, range-plus-prose, `Quest Only` — is held
/// by committed negatives that are all real pages, because a scope that quietly widened to
/// coalesce `1-13+, 35-50` would be inventing the continuity the page denies.</para>
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
        // 46 closed + 41 open tops. D1 shipped 46/72; the split is asserted below, so a
        // rule change that moved rows between the two buckets without moving either total
        // cannot pass here (trap 74's report half, one file over).
        Assert.Equal(87, Shipped.BandCount);
        Assert.Equal(31, Shipped.AbsentCount);
        Assert.Equal(118, Shipped.BandCount + Shipped.AbsentCount);
        Assert.Equal(16, Shipped.RefusedCount);
        Assert.Equal(15, Shipped.NoRowCount);
    }

    private static IEnumerable<ZoneLevels.Band> AllBands =>
        Shipped.BandedZones.Select(z => Shipped.BandFor(z)!);

    /// <summary>The two buckets, counted. The open tops are the class D1 refused and D2
    /// learned, and 41 of 87 is too large a share to leave uncounted: a caller that forgot
    /// <c>Max</c> can be null would be wrong about nearly half the shipped data.</summary>
    [Fact]
    public void TheOpenTopsAreCountedSeparatelyFromTheClosedBands()
    {
        var open = AllBands.Count(b => b.OpenTop);
        Assert.Equal(41, open);
        Assert.Equal(46, Shipped.BandCount - open);
        Assert.All(AllBands.Where(b => b.OpenTop), b => Assert.Null(b.Max));
        Assert.All(AllBands.Where(b => !b.OpenTop), b => Assert.NotNull(b.Max));
    }

    /// <summary>The plan's cited exhibits, quoted from the pages this session read. Crushbone
    /// is the Founder's Farm Gear complaint in one row; Rathe Mountains is the one a level rule
    /// can never refuse, and it is here so nobody later mistakes the band for the answer to
    /// that half. The last four are open tops — Plane of Sky is the verbatim Helm's ruling
    /// named, and Lower Guk the heaviest drop zone it reaches.</summary>
    [Theory]
    [InlineData("Crushbone", 5, 20, "5-20")]
    [InlineData("Rathe Mountains", 13, 45, "13-45")]
    [InlineData("Greater Faydark", 1, 12, "1-12")]
    [InlineData("Najena", 8, 35, "8-35")]
    [InlineData("Plane of Sky", 50, null, "50+")]
    [InlineData("Lower Guk", 30, null, "30-50+")]
    [InlineData("Temple of Veeshan", 60, null, "60+")]
    [InlineData("Karnor's Castle", 40, null, "40-55+")]
    public void TheCitedBandsAreWhatTheWikiPageSays(string zone, int min, int? max, string verbatim)
    {
        var band = Shipped.BandFor(zone);
        Assert.NotNull(band);
        Assert.Equal(min, band!.Min);
        Assert.Equal(max, band.Max);
        Assert.Equal(verbatim, band.Verbatim);
        Assert.Equal(max is null, band.OpenTop);
    }

    /// <summary>
    /// Every shipped band is arithmetically a band, AND its numbers re-derive from the verbatim
    /// it cites. A band whose numbers do not come out of its own quoted row is a number we made
    /// up with a citation stapled to it (trap 73).
    ///
    /// <para><b>The open-top arm asserts the top is DROPPED</b>, which is the half of Helm's
    /// option (a) a reading could quietly get wrong: `30-50+` must not ship 50 as a maximum,
    /// because the `+` is the page saying creatures above 50 are there.</para>
    /// </summary>
    [Fact]
    public void EveryBandReDerivesFromTheRowItQuotes()
    {
        foreach (var band in AllBands)
        {
            Assert.InRange(band.Min, 1, 99);
            if (band.Max is { } max) Assert.InRange(max, band.Min, 99);

            var open = band.Verbatim.EndsWith('+');
            Assert.Equal(open, band.OpenTop);

            var parts = band.Verbatim.TrimEnd('+').Split('-');
            Assert.Equal(int.Parse(parts[0]), band.Min);
            if (open)
            {
                // The stated top, where there was one, is evidence the row was well formed and
                // is deliberately NOT the Max.
                Assert.InRange(parts.Length, 1, 2);
                if (parts.Length == 2) Assert.InRange(int.Parse(parts[1]), band.Min, 99);
            }
            else
            {
                Assert.Equal(parts.Length == 2 ? int.Parse(parts[1]) : band.Min, band.Max);
            }
        }
    }

    /// <summary>
    /// The admitted set is exactly FOUR shapes, asserted as a whole-string match on every
    /// shipped verbatim.
    ///
    /// <para>Written as a pattern rather than as "contains no comma" (which is what D1 had)
    /// because D2 admits a `+` and a character-by-character denial can no longer express the
    /// rule: `20-40+ (50+ inside pit)` contains a `+` too, and the difference is that the `+`
    /// is not the ONLY defect. A whole-string match is the rule itself, so a scope that widened
    /// to prose or to a multi-range fails here rather than needing a new clause.</para>
    /// </summary>
    [Fact]
    public void EveryShippedVerbatimIsOneOfTheFourAdmittedShapes()
    {
        foreach (var band in AllBands)
            Assert.Matches(@"^\d{1,2}(-\d{1,2})?\+?$", band.Verbatim);
    }

    /// <summary>…and the committed negatives that prove the refusal still FIRES on real pages,
    /// each carrying the verbatim it refused. Without these the test above passes on a
    /// transform that reads nothing at all (trap 78).
    ///
    /// <para><b>Every row here is a page whose `+` was NOT the only thing wrong with it</b>, or
    /// a page declining to answer. The first three are the scope boundary Helm drew by name:
    /// `1-13+, 35-50` has a trailing-`+` range INSIDE it, and coalescing it to "1 and above"
    /// would assert a continuity the page contradicts by printing the gap.</para></summary>
    [Theory]
    [InlineData("Kithicor Forest", "1-13+, 35-50")]
    [InlineData("The Overthere", "20-40+ (50+ inside pit)")]
    [InlineData("Thurgadin", "30-35 (in caves), 30-45 (dwarves)")]
    [InlineData("Butcherblock Mountains", "1-15, 35")]
    [InlineData("Lesser Faydark", "10-30, 40-50")]
    [InlineData("Temple of Droga", "29-34 Droga Main, 33-38 Inner Sanctum")]
    [InlineData("The Arena", "n/a")]
    [InlineData("Surefall Glade", "?")]
    [InlineData("The Temple of Solusek Ro", "Quest Only")]
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
    [InlineData("Plane of Sky", ZoneLevels.Source.Banded)]   // was Refused until D2
    [InlineData("Butcherblock Mountains", ZoneLevels.Source.Refused)]
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
        // The real shipped case: both Chardok pages give "50+", so both carry the SAME band and
        // the folded key is not ambiguous at all. Until D2 both were Refused with the same
        // verbatim and this asserted the agreement on that; the agreement is now over a band,
        // which is the stronger half — Agrees() compares the Band record too.
        var answer = Shipped.Lookup("Chardok");
        Assert.Equal(ZoneLevels.Source.Banded, answer.Source);
        Assert.Equal("50+", answer.Verbatim);
        Assert.Equal(50, answer.Band!.Min);
        Assert.Null(answer.Band.Max);
    }

    // ---- Is the data a per-zone fact, or a template? ----------------------------------------

    /// <summary>
    /// The distinct-count telltale (trap 73). 48 authored guide steps carrying ten distinct
    /// sentences between them was a template nobody noticed by reading. A per-zone band should
    /// be nearly as varied as the zones carrying it; the two-thirds floor is loose enough for
    /// the genuine repeats (three zones really are 30-40) and tight enough that a parse which
    /// latched onto a shared infobox default would fail.
    ///
    /// <para><b>D2 re-derived which population the floor is applied to, and that is a change
    /// worth reading rather than a threshold being relaxed to fit.</b> An open top DISCARDS its
    /// maximum by design, so (Min, Max) over all 87 bands measures a deliberately coarser fact
    /// than D1's 46 did — it comes out at 53/87 = 0.61, under the floor, and NOT because the
    /// data got worse. So the floor is applied where it was calibrated (the closed bands, on the
    /// parsed pair) and to the measure a template would actually collapse (the verbatim row,
    /// over everything). Both clear it: 36/46 and 64/87.</para>
    /// </summary>
    [Fact]
    public void TheBandsAreAPerZoneFactRatherThanATemplate()
    {
        var closed = AllBands.Where(b => !b.OpenTop).ToList();
        var closedDistinct = closed.Select(b => (b.Min, b.Max)).Distinct().Count();
        Assert.True(closedDistinct * 3 >= closed.Count * 2,
            $"only {closedDistinct} distinct bands across {closed.Count} closed-band zones — " +
            "that reads like a template, not the wiki's own per-zone numbers.");

        var verbatims = AllBands.Select(b => b.Verbatim).Distinct().Count();
        Assert.True(verbatims * 3 >= Shipped.BandCount * 2,
            $"only {verbatims} distinct `Level of Monsters` rows across {Shipped.BandCount} " +
            "banded zones — a shared infobox default would look exactly like this.");
    }

    /// <summary>
    /// The open tops repeat MORE than the closed bands do, and that is measured and written
    /// down rather than smoothed over: 41 zones carry only 17 distinct bottoms.
    ///
    /// <para>It is the wiki's own repetition — five plane pages print `50+` and three print
    /// `48+` — so it is not the template signal the floor above hunts, and holding it to that
    /// floor would fail a true reading of real pages. It is pinned as an OBSERVATION instead, so
    /// the day the shape of this class changes somebody has to look at it.</para>
    /// </summary>
    [Fact]
    public void TheOpenTopsRepeatMoreThanTheClosedBandsAndTheNumberIsPinned()
    {
        var open = AllBands.Where(b => b.OpenTop).ToList();
        Assert.Equal(41, open.Count);
        Assert.Equal(17, open.Select(b => b.Min).Distinct().Count());
        Assert.Equal(5, open.Count(b => b.Verbatim == "50+"));
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
        var open = AllBands.Count(b => b.OpenTop);
        Assert.Contains($"- Bands shipped in `ZoneLevelBands.json`: **{Shipped.BandCount}**",
            report, StringComparison.Ordinal);
        Assert.Contains($"  - of those, CLOSED (`N-M` / `N`): **{Shipped.BandCount - open}**",
            report, StringComparison.Ordinal);
        Assert.Contains(
            $"  - of those, OPEN TOP (`N+` / `N-M+`, `Max` is null): **{open}**",
            report, StringComparison.Ordinal);
        Assert.Contains(
            "- ABSENT — row present and in none of the four admitted shapes: "
            + $"**{Shipped.RefusedCount}**", report, StringComparison.Ordinal);
        Assert.Contains($"- ABSENT — page has no `Level of Monsters` row: **{Shipped.NoRowCount}**",
            report, StringComparison.Ordinal);

        Assert.Contains("## Distinct-count telltale (trap 73)", report, StringComparison.Ordinal);
        Assert.Contains("## Refused verbatims", report, StringComparison.Ordinal);
        // Every open top listed one zone at a time, which is what makes the class auditable
        // rather than a count somebody has to take on trust.
        Assert.Contains("## Open tops learned", report, StringComparison.Ordinal);
        foreach (var zone in Shipped.BandedZones.Where(z => Shipped.BandFor(z)!.OpenTop))
            Assert.Contains($"| {zone} | ", report, StringComparison.Ordinal);
        Assert.Contains("## The join", report, StringComparison.Ordinal);
        Assert.Contains("This half is a snapshot.", report, StringComparison.Ordinal);
    }
}
