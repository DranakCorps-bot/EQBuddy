using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Zone eras (DRA-180 D1, plan P1) — eqlwiki's <c>{{&lt;Era&gt; Era}}</c> banner, promoted
/// from the COMMITTED wikitext cache by
/// <c>scripts/harvests/eqlwiki/zone-eras-transform.py</c>.
///
/// <para>What these tests hold is the two things D2's era gate will rest on and cannot check
/// for itself: <b>that an era is the wiki's word and never ours</b>, and <b>that a lookup
/// never hands one zone another zone's era</b>. The gate itself is D2's, and nothing in this
/// slice reads this catalog.</para>
///
/// <para><b>The guard is deliberately NOT a count.</b> `ZoneLevelsTests` can hold a
/// distinctness floor because a band is a per-zone fact; an era is a CATEGORY every zone of
/// an expansion shares, so 57 pages saying Classic is 57 pages agreeing rather than one
/// template parsed 57 times, and a floor would fail a true reading of real pages (the
/// transform's report says so at length). What is asserted instead is the plan's own bar:
/// <b>the mapping of each known era spelling to a NAMED zone, and the exact ABSENT list</b>.
/// A parse that latched onto something shared has to move a named zone to be wrong, and that
/// is a thing a human can check.</para>
///
/// <para><b>Both refusal arms are unreachable in the current corpus</b> — 0 pages carry an
/// off-ladder word and 0 carry two eras — so they are proven against fixtures here and over
/// synthetic wikitext in <c>zone-eras-transform.py --selftest</c>. A refusal that has never
/// fired on anything is a guard aimed at nothing (trap 78), and that is the whole reason the
/// selftest exists and runs in <c>check.ps1</c> and CI.</para>
///
/// <para>Byte-reproducibility is the Python <c>--check</c> in <c>check.ps1</c> and CI, not
/// here — spawning Python from xunit would fail on a machine without it, and a test that
/// skips instead is vacuous coverage (the call <c>ZoneLevelsTests</c> and
/// <c>HarvestedGuidesTests</c> already made).</para>
/// </summary>
public class ZoneErasTests
{
    private static readonly ZoneEras Shipped = ZoneEras.LoadEmbedded();

    private static string Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    // ---- The file is really there, and it is really the cache's answer ---------------------

    [Fact]
    public void TheShippedCatalogLoadsAndIsNotEmpty()
    {
        Assert.True(Shipped.DatedCount > 0,
            "ZoneEras.json did not load — an embedded resource that silently answers " +
            "nothing is a guard aimed at nothing (trap 78).");
        Assert.Equal(104, Shipped.DatedCount);
        Assert.Equal(14, Shipped.AbsentCount);
        Assert.Equal(118, Shipped.DatedCount + Shipped.AbsentCount);
        // Every absence in this corpus is a page with no banner. Both refusal arms are
        // empty, which is a fact about the corpus and not about the arms — see the fixture
        // tests below, which is where they are proven to fire.
        Assert.Equal(14, Shipped.NoTemplateCount);
        Assert.Equal(0, Shipped.RefusedCount);
    }

    /// <summary>Every shipped era word is one of ours. This is the pin on the Python side's
    /// mirrored <c>LADDER</c> (the same one-directional pin <c>zonelevels-transform.py</c>'s
    /// <c>identity_key</c> carries): a word the transform admits that the C# ladder does not
    /// carry reddens here, because <c>QuestEraLadder.Allowed</c> would fail OPEN on it and an
    /// era gate reading it would silently do nothing.</summary>
    [Fact]
    public void EveryShippedEraIsOnTheQuestLadder()
    {
        foreach (var zone in Shipped.DatedZones)
        {
            var era = Shipped.EraFor(zone);
            Assert.NotNull(era);
            Assert.True(QuestEraLadder.IndexOf(era!) >= 0,
                $"{zone} ships era '{era}', which is not on QuestEraLadder.Eras — " +
                "Allowed() would fail open on it.");
            // The ladder's own spelling, not the page's case. A gate compares strings.
            Assert.Equal(QuestEraLadder.Eras[QuestEraLadder.IndexOf(era!)], era);
        }
    }

    /// <summary>THE MAPPING GUARD the plan asked for, in place of a row-count floor: one
    /// NAMED zone per era spelling the corpus carries, with the template it was read from.
    ///
    /// <para>Kael Drakkel and Old Sebilis are the exhibits from DRA-180's §0 — the Kunark and
    /// Velious raid zones whose loot the band gate passes for a level-29 character because
    /// Kael's own <c>Level of Monsters</c> row says <c>30-60+</c>. Befallen is where the
    /// Founder's worn sword actually drops, and it is Classic.</para></summary>
    [Theory]
    [InlineData("Befallen", "Classic", "{{Classic Era}}")]
    [InlineData("Crushbone", "Classic", "{{Classic Era}}")]
    [InlineData("Plane of Sky", "Classic", "{{Classic Era}}")]
    [InlineData("Paineel", "Paineel", "{{Paineel Era}}")]
    [InlineData("The Temple of Solusek Ro", "Temple", "{{Temple Era}}")]
    [InlineData("Old Sebilis", "Kunark", "{{Kunark Era}}")]
    [InlineData("Karnor's Castle", "Kunark", "{{Kunark Era}}")]
    [InlineData("Chardok (Post-Revamp)", "Chardok Revamp", "{{Chardok Revamp Era}}")]
    [InlineData("Kael Drakkel", "Velious", "{{Velious Era}}")]
    [InlineData("Temple of Veeshan", "Velious", "{{Velious Era}}")]
    // The two pages whose banner is NOT on line 1. The parse is not positional (trap 66) and
    // these are the committed proof of it.
    [InlineData("Mines of Nurga", "Kunark", "{{Kunark Era}}")]
    [InlineData("Permafrost", "Classic", "{{Classic Era}}")]
    public void TheCitedErasAreWhatTheWikiPageSays(string zone, string era, string verbatim)
    {
        var answer = Shipped.Lookup(zone);
        Assert.Equal(ZoneEras.Source.Dated, answer.Source);
        Assert.Equal(era, answer.Era);
        Assert.Equal(verbatim, answer.Verbatim);
        Assert.Equal(era, Shipped.EraFor(zone));
    }

    /// <summary>Every era on the ladder that the corpus uses at all is reachable by name, so
    /// a fold that quietly collapsed two spellings into one would fail here rather than show
    /// up as a count nobody reads. Sky, Epics and Luclin are absent from the ZONE corpus —
    /// asserted as absent, so nobody later reads their absence as a parse bug.</summary>
    [Fact]
    public void TheCorpusUsesSixOfTheLaddersNineErasAndTheOtherThreeAreAbsentOnPurpose()
    {
        var used = Shipped.DatedZones.Select(z => Shipped.EraFor(z)!).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new[] { "Classic", "Paineel", "Temple", "Kunark", "Chardok Revamp", "Velious" }
                .ToHashSet(StringComparer.Ordinal), used);
        // Sky/Epics are quest-page eras (the Plane of Sky ZONE page says Classic); Luclin is
        // content eqlwiki's zone pages do not file anything under.
        Assert.DoesNotContain("Sky", used);
        Assert.DoesNotContain("Epics", used);
        Assert.DoesNotContain("Luclin", used);
    }

    // ---- ABSENT is an answer, and it is not Classic ----------------------------------------

    /// <summary>THE EXACT ABSENT LIST. Named one at a time rather than counted, because
    /// "absent means Classic" is the fold this data exists to refuse and the names are the
    /// argument: Stonebrunt Mountains, The Warrens and Kerra Island are the Paineel-adjacent
    /// set, in a corpus where exactly ONE page carries <c>{{Paineel Era}}</c>. A default
    /// would put a level-45 Warrens camp in a pre-Paineel world on the strength of a template
    /// nobody wrote (trap 73).</summary>
    [Fact]
    public void TheAbsentListIsExactlyTheseFourteenPages()
    {
        string[] expected =
        [
            "Grobb", "Halas", "Highpass Hold", "Kaladim", "Kerra Island", "Lower Guk",
            "Misty Thicket", "Oasis of Marr", "Oggok", "Plane of Hate cleanupproject",
            "Rivervale", "Stonebrunt Mountains", "The Warrens", "Upper Guk",
        ];
        foreach (var zone in expected)
        {
            var answer = Shipped.Lookup(zone);
            Assert.Equal(ZoneEras.Source.NoTemplate, answer.Source);
            Assert.Equal("", answer.Era);
            Assert.Equal("", answer.Verbatim);
            Assert.Null(Shipped.EraFor(zone));
        }
        // …and nothing else is absent, so a page that loses its banner to a wiki edit shows
        // up here rather than as a silent stand-down in D2's gate.
        Assert.Equal(expected.Length, Shipped.AbsentCount);
    }

    /// <summary>The three Paineel-adjacent names, called out on their own. If a later slice
    /// ever wants a default for the absent list, this is the test it has to argue with.</summary>
    [Theory]
    [InlineData("Stonebrunt Mountains")]
    [InlineData("The Warrens")]
    [InlineData("Kerra Island")]
    public void AnAbsentPageIsNotQuietlyClassic(string zone)
    {
        Assert.Null(Shipped.EraFor(zone));
        Assert.NotEqual("Classic", Shipped.Lookup(zone).Era);
        // The one page that really does carry the era these three sit next to.
        Assert.Equal("Paineel", Shipped.EraFor("Paineel"));
    }

    /// <summary>All four outcomes are reachable. "Absent" is three different facts and a
    /// surface that cannot tell them apart will say the wrong one out loud (trap 34's
    /// must-list). <see cref="ZoneEras.Source.Refused"/> has no instance in the shipped
    /// corpus and takes a fixture — it is in the list precisely so its emptiness is stated
    /// rather than assumed.</summary>
    [Theory]
    [InlineData("Kael Drakkel", ZoneEras.Source.Dated)]
    [InlineData("Halas", ZoneEras.Source.NoTemplate)]
    [InlineData("Plane of Knowledge", ZoneEras.Source.Unknown)]
    [InlineData("", ZoneEras.Source.Unknown)]
    public void EveryOutcomeIsReachableWithARealName(string zone, ZoneEras.Source expected) =>
        Assert.Equal(expected, Shipped.Lookup(zone).Source);

    [Fact]
    public void TheRefusedOutcomeIsReachableAndCarriesWhatItRefused()
    {
        var eras = new ZoneEras(
            new Dictionary<string, ZoneEras.Banner>
            {
                ["Befallen"] = new("Classic", "{{Classic Era}}"),
            },
            new Dictionary<string, string>
            {
                // An era word the ladder does not carry. The transform refuses it BY NAME
                // rather than folding it — there is no rename map on the zone side, because
                // the zone corpus has never needed one.
                ["Somewhere"] = "{{Prophecy Era}}",
                // Two claims on one page. Picking either would be a coin toss with a citation.
                ["Elsewhere"] = "{{Kunark Era}} {{Velious Era}}",
            });

        foreach (var (zone, verbatim) in new[]
                 {
                     ("Somewhere", "{{Prophecy Era}}"),
                     ("Elsewhere", "{{Kunark Era}} {{Velious Era}}"),
                 })
        {
            var answer = eras.Lookup(zone);
            Assert.Equal(ZoneEras.Source.Refused, answer.Source);
            Assert.Equal("", answer.Era);
            Assert.Equal(verbatim, answer.Verbatim);
            Assert.Null(eras.EraFor(zone));
        }
        Assert.Equal(2, eras.RefusedCount);
        Assert.Equal(0, eras.NoTemplateCount);
    }

    // ---- The lookup fold: what it bridges, and what it must NOT -----------------------------

    /// <summary>Spellings an item record uses that are the same zone as a page we read. Each
    /// of these is a real <c>DropZones</c> value in the shipped catalog.</summary>
    [Theory]
    [InlineData("Estate of Unrest", "The Estate of Unrest")]
    [InlineData("The Great Divide", "Great Divide")]
    [InlineData("Cazic-Thule", "Cazic Thule (Zone)")]
    [InlineData("Gorge of King Xorbb (Beholder's Maze)", "Gorge of King Xorbb")]
    public void TheFoldBridgesASpellingToItsPage(string spelling, string title)
    {
        var expected = Shipped.Lookup(title);
        Assert.NotEqual(ZoneEras.Source.Unknown, expected.Source);
        Assert.Equal(expected, Shipped.Lookup(spelling));
    }

    /// <summary>THE COMMITTED NEGATIVES. Every string here is a real <c>DropZones</c> value in
    /// the shipped item catalog that longest-containment would have matched to a page — the
    /// same measured list <c>ZoneLevelsTests</c> carries, because it is the same fold and the
    /// same catalog. An era that is wrong is worse here than a band that is wrong: this is
    /// the claim that REFUSES a row, so a fiction removes a real answer from the player's
    /// screen.</summary>
    [Theory]
    [InlineData("Commonlands")]                     // containment → West Commonlands
    [InlineData("North Qeynos")]                    // containment → Qeynos
    [InlineData("Qeynos Catacombs")]
    [InlineData("Splitpaw")]                        // containment → Splitpaw Lair
    [InlineData("Kaesora, Droga, Nurga")]           // three zones in one field
    [InlineData("Frontier Mountains, Mines of Nurga")]
    [InlineData("This drop is super ultra rare from any spider in kaesora.")]
    [InlineData("Kobolds in The Warrens and possibly Stonebrunt Mountains")]
    [InlineData("{{VeliousGray| Skyshrine }}")]
    [InlineData("{{VeliousGray|Crystal Caverns}}")]
    // The one worth staring at: an era TEMPLATE sitting inside a DropZones value. The
    // transform's own regex refuses this shape on a page, and the fold refuses it here.
    [InlineData("Timorous Deep {{Era|Kunark}}")]
    [InlineData("Greater Faydark<br>")]
    public void TheFoldRefusesWhatContainmentWouldHaveMatched(string spelling)
    {
        Assert.Equal(ZoneEras.Source.Unknown, Shipped.Lookup(spelling).Source);
        Assert.Null(Shipped.EraFor(spelling));
    }

    // ---- The Chardok collision, decided out loud --------------------------------------------

    /// <summary>The corpus's one collision, and the decision the plan named. Two Chardok
    /// pages fold to one identity while the item catalog's <c>DropZones</c> just says
    /// "Chardok" (155 mentions — the transform's report measures it), so the key has to
    /// answer something.
    ///
    /// <para><b>It answers the EARLIER era.</b> Content that exists from Kunark on exists in
    /// a Chardok-Revamp world too; answering Chardok Revamp would have D2's gate refuse a
    /// zone that is in the game. The exact titles still answer their own pages.</para></summary>
    [Fact]
    public void TheFoldedChardokAnswersTheEarlierEra()
    {
        Assert.Equal("Kunark", Shipped.EraFor("Chardok"));
        Assert.Equal(ZoneEras.Source.Dated, Shipped.Lookup("Chardok").Source);
        Assert.Equal("Kunark", Shipped.EraFor("Chardok (Pre-Revamp)"));
        Assert.Equal("Chardok Revamp", Shipped.EraFor("Chardok (Post-Revamp)"));
        Assert.True(QuestEraLadder.IndexOf("Kunark") < QuestEraLadder.IndexOf("Chardok Revamp"),
            "the rule is 'earlier on the ladder wins' — if this ordering ever moves, the " +
            "answer above moves with it and somebody should notice here.");
    }

    /// <summary>…and the rule does not depend on which title the dictionary happened to visit
    /// first. Both insertion orders, one answer.</summary>
    [Fact]
    public void EarliestWinsRegardlessOfInsertionOrder()
    {
        foreach (var reversed in new[] { false, true })
        {
            var rows = new List<KeyValuePair<string, ZoneEras.Banner>>
            {
                new("Chardok (Pre-Revamp)", new("Kunark", "{{Kunark Era}}")),
                new("Chardok (Post-Revamp)", new("Chardok Revamp", "{{Chardok Revamp Era}}")),
            };
            if (reversed) rows.Reverse();
            var eras = new ZoneEras(new Dictionary<string, ZoneEras.Banner>(rows),
                                    new Dictionary<string, string>());
            Assert.Equal("Kunark", eras.EraFor("Chardok"));
        }
    }

    /// <summary>The other arm of the fold rule, which the corpus cannot reach: a
    /// disagreement where either side is ABSENT answers NOTHING. An absence is not an era
    /// and cannot be compared, so folding a dated title with an absent one would be inventing
    /// the "absent means Classic" default the transform refuses by name.</summary>
    [Fact]
    public void AFoldBetweenADatedTitleAndAnAbsentOneAnswersNothing()
    {
        var eras = new ZoneEras(
            new Dictionary<string, ZoneEras.Banner>
            {
                ["Chardok (Pre-Revamp)"] = new("Kunark", "{{Kunark Era}}"),
                ["Najena"] = new("Classic", "{{Classic Era}}"),
            },
            new Dictionary<string, string> { ["Chardok (Post-Revamp)"] = "" });

        Assert.Equal(ZoneEras.Source.Unknown, eras.Lookup("Chardok").Source);
        Assert.Null(eras.EraFor("Chardok"));
        // The exact titles still answer, and an unambiguous neighbour is untouched.
        Assert.Equal("Kunark", eras.EraFor("Chardok (Pre-Revamp)"));
        Assert.Equal("Classic", eras.EraFor("Najena"));
    }

    /// <summary>An off-ladder era cannot be ranked, so two of them cannot be picked between
    /// either. The transform can never emit one, but this constructor is public and the rule
    /// should not quietly answer "whichever sorted first".</summary>
    [Fact]
    public void AFoldBetweenTwoErasWeCannotRankAnswersNothing()
    {
        var eras = new ZoneEras(
            new Dictionary<string, ZoneEras.Banner>
            {
                ["Chardok (Pre-Revamp)"] = new("Kunark", "{{Kunark Era}}"),
                ["Chardok (Post-Revamp)"] = new("Prophecy", "{{Prophecy Era}}"),
            },
            new Dictionary<string, string>());

        Assert.Equal(ZoneEras.Source.Unknown, eras.Lookup("Chardok").Source);
        Assert.Equal("Prophecy", eras.EraFor("Chardok (Post-Revamp)"));
    }

    /// <summary>Two titles that fold together and AGREE still answer — otherwise the rule
    /// above would be indistinguishable from "any collision answers nothing".</summary>
    [Fact]
    public void TwoTitlesThatFoldTogetherAndAgreeStillAnswer()
    {
        var eras = new ZoneEras(
            new Dictionary<string, ZoneEras.Banner>
            {
                ["Befallen"] = new("Classic", "{{Classic Era}}"),
                ["Befallen 4 (Refined)"] = new("Classic", "{{Classic Era}}"),
            },
            new Dictionary<string, string>());
        Assert.Equal("Classic", eras.EraFor("Befallen"));
    }

    // ---- The report -------------------------------------------------------------------------

    /// <summary>The report is as much the deliverable of this slice as the data is — D2's
    /// gate should be written against its numbers, and its join table is the measurement of
    /// whether that gate can reach anything at all. So its headline counts are held to the
    /// committed file.
    ///
    /// <para>Its JOIN numbers are deliberately NOT asserted here. They are measured against
    /// <c>ItemCatalog.json.gz</c>, which a refresh rebuilds; asserting them would redden a PR
    /// on a file it did not touch. The report says in its own text that the join is a
    /// snapshot and how to re-take it.</para></summary>
    [Fact]
    public void TheReportIsThereAndItsCountsMatchTheCommittedFile()
    {
        var path = Path.Combine(Root, "scripts", "harvests", "eqlwiki", "zone-eras-report.md");
        Assert.True(File.Exists(path),
            "zone-eras-transform.py writes zone-eras-report.md; it is not there.");
        var report = File.ReadAllText(path);

        Assert.Contains($"- Eras shipped in `ZoneEras.json`: **{Shipped.DatedCount}**",
            report, StringComparison.Ordinal);
        Assert.Contains(
            $"- ABSENT — page carries no `{{{{... Era}}}}` banner: **{Shipped.NoTemplateCount}**",
            report, StringComparison.Ordinal);
        Assert.Contains(
            "- ABSENT — banner REFUSED (era word not on the ladder): "
            + $"**{Shipped.RefusedCount}**", report, StringComparison.Ordinal);

        // Every era in the histogram, from the catalog's own numbers — the bucket check trap
        // 74's report half asks for, one file over.
        foreach (var group in Shipped.DatedZones.GroupBy(z => Shipped.EraFor(z)!))
            Assert.Contains($"| {group.Key} | {group.Count()} |", report, StringComparison.Ordinal);

        // Every absent page named, which is what makes the list auditable rather than a count
        // somebody has to take on trust.
        Assert.Contains("## Pages with no era banner at all", report, StringComparison.Ordinal);
        foreach (var zone in new[] { "Stonebrunt Mountains", "The Warrens", "Kerra Island" })
            Assert.Contains($"- {zone}", report, StringComparison.Ordinal);

        Assert.Contains("why there is NO floor here", report, StringComparison.Ordinal);
        Assert.Contains("## Era words NOT on the ladder", report, StringComparison.Ordinal);
        Assert.Contains("## Pages carrying two DIFFERENT era banners", report,
            StringComparison.Ordinal);
        Assert.Contains("## The identity fold", report, StringComparison.Ordinal);
        Assert.Contains("answers the EARLIER era", report, StringComparison.Ordinal);
        Assert.Contains("## The join", report, StringComparison.Ordinal);
        Assert.Contains("This half is a snapshot.", report, StringComparison.Ordinal);
    }
}
