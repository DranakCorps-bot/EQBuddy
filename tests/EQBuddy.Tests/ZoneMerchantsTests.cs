using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// **THE VENDOR HALF: what shipped, what it refuses, and the two ways it could be useless**
/// (DRA-149 D4, plan P5 — the Founder's FAIL item 3, second half).
///
/// <para>Two failure modes are guarded here rather than one. The obvious one is a WRONG match:
/// a Meat Pies merchant filed under Jewelcrafting. The one that goes quiet is a DEAD keyword —
/// a curated row that matches nothing at all, which reports a clean, empty, perfectly
/// well-formed list forever (trap 78). Every keyword below is asserted to fire against the
/// shipped catalog.</para>
///
/// <para>The counts are floors and ranges, not equalities: the transform's own <c>--check</c>
/// gate in <c>check.ps1</c> and CI is what pins the file byte for byte, and a weekly refresh
/// that adds a merchant line to a zone page should not redden a behavioural suite.</para>
/// </summary>
public class ZoneMerchantsTests
{
    private static ZoneMerchants Catalog => ZoneMerchants.Default;

    /// <summary>The shipped file loads, and it is not the empty answer <c>LoadEmbedded</c>
    /// returns when the resource is missing. Without this every assertion below could pass on a
    /// catalog that never shipped.</summary>
    [Fact]
    public void TheCommittedCatalogLoadsWithZonesAndLinesInIt()
    {
        Assert.InRange(Catalog.ZoneCount, 40, 118);
        Assert.InRange(Catalog.LineCount, 300, 600);
        // Every zone page that was read and says nothing still ships, so "we read it and it is
        // silent" and "we have never read it" are different answers.
        Assert.InRange(Catalog.SilentZoneCount, 40, 100);
        Assert.Equal(118, Catalog.ZoneCount + Catalog.SilentZoneCount);
    }

    /// <summary>
    /// **THE THREE OUTCOMES A ZONE NAME CAN HAVE**, which is <c>ZoneLevels.Lookup</c>'s rule one
    /// catalog along: a zone with lines, a zone read and silent, and a zone never read. A
    /// surface that could not tell the last two apart would say "no shops here" about a place
    /// EQBuddy has never opened a page for.
    /// </summary>
    [Fact]
    public void AZoneWeReadAndAZoneWeNeverReadAreDifferentAnswers()
    {
        Assert.True(Catalog.HasRead("Kaladim"));
        Assert.NotEmpty(Catalog.LinesFor("Kaladim"));

        // Plane of Sky is in the committed zone titles and its map key names no merchant.
        var silent = Catalog.LinesFor("Plane of Sky");
        Assert.Empty(silent);

        Assert.False(Catalog.HasRead("Nowhere At All"));
        Assert.Empty(Catalog.LinesFor("Nowhere At All"));
    }

    /// <summary>
    /// **THE COMMITTED POSITIVE: Kaladim's gems line lands under Jewelcrafting, in the wiki's
    /// own words.**
    ///
    /// <para>It is asserted as a SUBSTRING of a shipped line rather than as the whole line,
    /// because the whole line is the wiki's and a wiki edit is allowed to lengthen it. What must
    /// not change is that the transcription is the page's own sentence — so the fragment
    /// asserted is one nobody here wrote.</para>
    /// </summary>
    [Fact]
    public void KaladimsGemMerchantsLandUnderJewelcrafting()
    {
        var hits = Catalog.For(Tradeskill.Jewelcrafting);

        Assert.Contains(hits, m =>
            m.Zone == "Kaladim" && m.Line.Contains("Jewelry Metal and Rare Gems", StringComparison.Ordinal));
        // And the vendor's NAME survives inside the sentence rather than in a field of its own.
        Assert.Contains(hits, m => m.Line.Contains("Bndainy Everhot", StringComparison.Ordinal));
    }

    /// <summary>
    /// **THE COMMITTED NEGATIVE THE PLAN NAMED: a Meat Pies merchant does not land under
    /// Jewelcrafting.**
    ///
    /// <para>Freeport's tavern line is a real shipped line and it is about food. It is asserted
    /// to BE in the catalog first — a negative that passes because the line is missing proves
    /// nothing (trap 78) — and then asserted absent from the jewelcrafting list.</para>
    /// </summary>
    [Fact]
    public void AMeatPiesMerchantIsNotAJewelcraftingShop()
    {
        var pies = Catalog.LinesFor("Freeport")
            .FirstOrDefault(l => l.Contains("Meat Pies", StringComparison.Ordinal));
        Assert.NotNull(pies);

        Assert.DoesNotContain(Catalog.For(Tradeskill.Jewelcrafting), m => m.Line == pies);
        Assert.DoesNotContain(Catalog.For(Tradeskill.Blacksmithing), m => m.Line == pies);
        Assert.DoesNotContain(Catalog.For(Tradeskill.Fletching), m => m.Line == pies);
    }

    /// <summary>
    /// **THE WORD-BOUNDARY RULE, PROVED ON THE LINE THAT BROKE IT.**
    ///
    /// <para>With a plain substring rule Blacksmithing's <c>ore</c> matched *"Cooking and
    /// L<b>ore</b> Books"* and filed a cookbook merchant under smithing. Both halves are
    /// asserted: the false positive stays refused, and the TRUE positive it would have been
    /// tempting to fix by deleting the keyword still fires.</para>
    /// </summary>
    [Theory]
    [InlineData("Merchant selling Cooking and Lore Books", false)]
    [InlineData("Merchants selling Food, Cloth Armor, Gems, Mithril Ore and Chainmail Patterns.", true)]
    [InlineData("Merchant selling Brellium, Ore, Smithy Hammers, Sharpening Stones", true)]
    [InlineData("Shop with a folklore Merchant", false)]
    public void AKeywordMatchesAtAWordStartAndNeverInsideOne(string line, bool expected) =>
        Assert.Equal(expected, ZoneMerchants.Names(line, ZoneMerchants.Keywords(Tradeskill.Blacksmithing)));

    /// <summary>A keyword may still run into the REST of a word — plurals and compounds are the
    /// whole reason the rule is a prefix rather than a whole word.</summary>
    [Theory]
    [InlineData("Merchant selling Gems")]
    [InlineData("Merchant selling Gemstones")]
    [InlineData("Merchant selling Jewelry supplies")]
    public void APluralIsStillTheSameKeyword(string line) =>
        Assert.True(ZoneMerchants.Names(line, ZoneMerchants.Keywords(Tradeskill.Jewelcrafting)));

    /// <summary>
    /// **EVERY PROFESSION HAS KEYWORDS, AND EVERY KEYWORD FIRES** (trap 78).
    ///
    /// <para>This is the guard that matters most and the one easiest to leave out. A curated row
    /// that matches nothing is not a bug any surface can show: the block draws its honest empty
    /// state, the counts are consistent, and a trade quietly has no shops forever. An empty list
    /// is the same failure one step earlier.</para>
    /// </summary>
    [Fact]
    public void EveryCuratedKeywordMatchesSomethingInTheShippedCatalog()
    {
        var lines = AllLines();
        Assert.NotEmpty(lines);

        foreach (var skill in Enum.GetValues<Tradeskill>())
        {
            var keywords = ZoneMerchants.Keywords(skill);
            Assert.NotEmpty(keywords);
            foreach (var keyword in keywords)
                Assert.True(lines.Any(l => ZoneMerchants.Names(l, [keyword])),
                    $"{skill}'s keyword '{keyword}' matches no shipped merchant line.");
        }
    }

    /// <summary>
    /// **ALL EIGHT PROFESSIONS HAVE SOMEWHERE TO SHOP, AND JEWELCRAFTING HAS MORE THAN THE
    /// PLAN'S PARK FLOOR.**
    ///
    /// <para>P5 said: *"if Jewelcrafting matches fewer than 3 zones the face parks with that
    /// number and the wiki door"*. The opening survey measured 17, so the face shipped. This
    /// pins the condition that decision rested on, in the suite, against the shipped file — the
    /// day a refresh takes it under three, the park is back on the table and this is what says
    /// so.</para>
    /// </summary>
    [Fact]
    public void EveryProfessionMatchesAZoneAndJewelcraftingClearsTheParkFloor()
    {
        foreach (var skill in Enum.GetValues<Tradeskill>())
            Assert.True(Catalog.ZonesFor(skill) > 0, $"{skill} matches no zone.");

        Assert.InRange(Catalog.ZonesFor(Tradeskill.Jewelcrafting), 3, 118);
    }

    /// <summary>
    /// **THE CRAFTING STATIONS ARE NOT KEYWORDS**, and the reason is a number: they sit on
    /// roughly a third of these lines, so matching on one would file most of Norrath's shops
    /// under most professions. A station that rides a line matched for another reason is still
    /// on screen, because the line is shown whole.
    /// </summary>
    [Theory]
    [InlineData("oven")]
    [InlineData("kiln")]
    [InlineData("forge")]
    [InlineData("loom")]
    [InlineData("barrel")]
    [InlineData("alcohol")]
    public void NoProfessionMatchesOnACraftingStationOrAFinishedProduct(string station)
    {
        foreach (var skill in Enum.GetValues<Tradeskill>())
            Assert.DoesNotContain(station, ZoneMerchants.Keywords(skill), StringComparer.OrdinalIgnoreCase);
    }

    // ---- what a surface shows (HelperPresentation) ---------------------------------------

    /// <summary>
    /// **ONE LINE PER ZONE, AND NEVER MORE THAN THE CAP.**
    ///
    /// <para>Freeport is the busiest map key in the cache, so without the per-zone rule it fills
    /// a profession's whole list on its own and the "where do I go" answer becomes one
    /// place.</para>
    /// </summary>
    [Fact]
    public void TheShownListIsCappedAndNeverRepeatsAZone()
    {
        foreach (var skill in Enum.GetValues<Tradeskill>())
        {
            var shown = HelperPresentation.MerchantsShown(Catalog, skill);
            Assert.True(shown.Count <= HelperPresentation.MerchantLineCap);
            Assert.Equal(shown.Count,
                shown.Select(m => m.Zone).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            // And it never shows more than the catalog has.
            Assert.True(shown.Count <= Catalog.ZonesFor(skill));
        }
    }

    /// <summary>The surviving cap SAYS what it held back, and says nothing when it held nothing
    /// (trap 50).</summary>
    [Fact]
    public void TheCapNamesWhatItHeldBackAndIsSilentWhenItHeldNothing()
    {
        Assert.Equal("", HelperPresentation.MerchantsCapped(3, 3));
        Assert.Equal("", HelperPresentation.MerchantsCapped(3, 2));
        Assert.Contains("1 more zone", HelperPresentation.MerchantsCapped(3, 4), StringComparison.Ordinal);
        Assert.Contains("14 more zones", HelperPresentation.MerchantsCapped(3, 17), StringComparison.Ordinal);

        // …and the real one, over the shipped catalog, is not silent for Jewelcrafting.
        var shown = HelperPresentation.MerchantsShown(Catalog, Tradeskill.Jewelcrafting);
        Assert.NotEqual("", HelperPresentation.MerchantsCapped(
            shown.Count, Catalog.ZonesFor(Tradeskill.Jewelcrafting)));
    }

    /// <summary>The row leads with the ZONE, because that is the answer to "where" and the
    /// wiki's sentence often does not carry it.</summary>
    [Fact]
    public void AMerchantRowLeadsWithTheZoneAndThenQuotesThePage()
    {
        var row = HelperPresentation.MerchantRow(new MerchantLine("Kaladim", "Merchant selling Gems"));
        Assert.StartsWith("Kaladim — ", row, StringComparison.Ordinal);
        Assert.EndsWith("Merchant selling Gems", row, StringComparison.Ordinal);
    }

    /// <summary>
    /// **THE EMPTY STATE'S SUBJECT IS eqlwiki, NEVER THE GAME.**
    ///
    /// <para>A trade with no line in this list still has shops in Norrath; what is missing is a
    /// sentence on a wiki page. The forbidden phrasings are asserted by name — this is the same
    /// discipline the unread-worn sentence keeps, where the subject is EQBuddy's catalog rather
    /// than the player's bags.</para>
    /// </summary>
    [Fact]
    public void TheEmptyStateBlamesTheWikiMapAndNotTheGame()
    {
        var said = HelperPresentation.NoMerchantsFor(Tradeskill.Fletching);

        Assert.Contains("eqlwiki", said, StringComparison.Ordinal);
        Assert.Contains("Fletching", said, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "nowhere sells", "no one sells", "does not exist", "cannot be bought" })
            Assert.DoesNotContain(forbidden, said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The block's source caption says the words are the wiki's and that EQBuddy did
    /// not re-write them — the claim the transcription rule actually supports.</summary>
    [Fact]
    public void TheBlockNoteSaysTheWordsAreTheWikis()
    {
        Assert.Contains("eqlwiki", HelperPresentation.MerchantsNote, StringComparison.Ordinal);
        Assert.Contains("own words", HelperPresentation.MerchantsNote, StringComparison.Ordinal);
    }

    // ---- the door ------------------------------------------------------------------------

    /// <summary>
    /// The zone-page door is a wiki door, so it has no shell ADDRESS and its tip says EQBuddy
    /// fetches nothing — the request policy toward eqlwiki is the player's click and nothing
    /// else (consequence-list item 7).
    /// </summary>
    [Fact]
    public void TheZonePageDoorIsAWikiDoorAndSaysEqbuddyFetchesNothing()
    {
        Assert.Null(HelperPresentation.AddressFor(HelperDoorKind.WikiZone));
        Assert.Equal("eqlwiki", HelperPresentation.DoorLabel(HelperDoorKind.WikiZone));

        var tip = HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.WikiZone, "Kaladim"));
        Assert.Contains("Kaladim", tip, StringComparison.Ordinal);
        Assert.Contains("never fetches it for you", tip, StringComparison.Ordinal);

        // The no-target arm is what the phone carries, said once over the block.
        Assert.NotEqual("", HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.WikiZone, "")));
    }

    /// <summary>A zone title is NOT an item, so its door must go through the non-item link rule
    /// — putting a page title through the item alias table is the one thing
    /// <c>WikiLinks.Page</c> exists to prevent.</summary>
    [Fact]
    public void AZoneTitleGoesThroughTheNonItemLinkRule()
    {
        Assert.Equal(WikiLinks.Page("Cazic Thule (Zone)"), WikiLinks.Page("Cazic Thule (Zone)"));
        Assert.Contains("Cazic", WikiLinks.Page("Cazic Thule (Zone)"), StringComparison.Ordinal);
    }

    /// <summary>The corpus the keyword-fires assertion runs over.
    ///
    /// <para><b>It is read off the ZONES, not off the per-profession lists.</b> Taking the union
    /// of what already matched would be circular — every keyword would be proving itself against
    /// the lines it selected, and a keyword matching nothing would contribute nothing and pass
    /// (the shape D3's own prove-fail caught in its non-place guard).</para></summary>
    private static IReadOnlyList<string> AllLines()
    {
        var corpus = new List<string>();
        foreach (var zone in Catalog.Zones) corpus.AddRange(Catalog.LinesFor(zone));
        return corpus;
    }
}
