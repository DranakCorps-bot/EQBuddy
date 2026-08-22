using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// The contribution pack is text a player pastes ONTO a community wiki — the bar is
/// "meets eqlwiki house style and never overstates the data" (discussion #65). These
/// tests pin classification (what counts as new), the rarity honesty rules, and the
/// exact paste shapes for the three page situations.
/// </summary>
public class WikiContributionTests
{
    private static MobLookupResult PageWith(params string[] drops) => new(
        new MobInfo
        {
            IsCreaturePage = true,
            Name = "Ambassador Dvinn", PageTitle = "Ambassador Dvinn",
            Drops = drops.Select(d => (d, "Common")).ToList(),
        },
        ItemLookupState.Cached, DateTime.UtcNow);

    private static readonly MobLookupResult Missing = new(null, ItemLookupState.NotFound, null);
    private static readonly MobLookupResult Offline = new(null, ItemLookupState.Offline, null);

    private static MobSummary Mob(string name, int kills, params MobLoot[] loot) =>
        new(name, kills, kills, 20, 0, 0, loot.ToList());

    // ---- classification ----

    [Fact]
    public void KnownWhenPageListsIt() =>
        Assert.Equal(WikiDropStatus.Known,
            WikiContribution.Classify(PageWith("Dragoon Dirk"), "Dragoon Dirk"));

    [Fact]
    public void TierSuffixFoldsBeforeComparing() =>
        Assert.Equal(WikiDropStatus.Known,
            WikiContribution.Classify(PageWith("Dragoon Dirk"), "Dragoon Dirk +2"));

    [Fact]
    public void BackticksDropBeforeComparing() =>
        Assert.Equal(WikiDropStatus.Known,
            WikiContribution.Classify(PageWith("Bracelet of Lrodd"), "Bracelet of L`rodd"));

    /// <summary>Frankthetankk's live test (#65): the wiki lists {{:Packmasters Lash}}
    /// (no apostrophe) while the log says "Packmaster's Lash" — a false ✦ invites a
    /// duplicate wiki entry, the exact failure the flag exists to prevent.</summary>
    [Fact]
    public void ApostrophesDropBeforeComparing() =>
        Assert.Equal(WikiDropStatus.Known,
            WikiContribution.Classify(PageWith("Packmasters Lash"), "Packmaster's Lash"));

    [Fact]
    public void NewWhenPageDoesNotListIt() =>
        Assert.Equal(WikiDropStatus.NewToPage,
            WikiContribution.Classify(PageWith("Dragoon Dirk"), "Black Heart"));

    [Fact]
    public void EmptyLootListIsItsOwnStatus() =>
        Assert.Equal(WikiDropStatus.PageHasNoLoot,
            WikiContribution.Classify(PageWith(), "Black Heart"));

    [Fact]
    public void MissingPageIsItsOwnStatus() =>
        Assert.Equal(WikiDropStatus.PageMissing,
            WikiContribution.Classify(Missing, "Black Heart"));

    [Theory]
    [InlineData(true)]   // never looked up
    [InlineData(false)]  // wiki unreachable
    public void NoAnswerMeansUnknownNotNew(bool nullLookup) =>
        Assert.Equal(WikiDropStatus.Unknown,
            WikiContribution.Classify(nullLookup ? null : Offline, "Black Heart"));

    // ---- rarity honesty ----

    [Theory]
    [InlineData(100, 20, "Always")]
    [InlineData(100, 10, "Common")]      // 10 straight drops can't prove "Always"
    [InlineData(60, 10, "Common")]
    [InlineData(30, 10, "Uncommon")]
    [InlineData(10, 10, "Rare")]
    [InlineData(2, 50, "Ultra Rare")]
    public void RarityFollowsTheWikisBands(double pct, int kills, string expected) =>
        Assert.Equal(expected, WikiContribution.SuggestRarity(pct, kills));

    [Theory]
    [InlineData(50.0, 9)]   // sample too thin
    [InlineData(null, 100)] // no rate at all
    public void ThinSamplesSuggestNothing(double? pct, int kills) =>
        Assert.Null(WikiContribution.SuggestRarity(pct, kills));

    // ---- edit links ----

    [Fact]
    public void EditUrlEscapesLikeTheWiki() =>
        Assert.Equal("https://eqlwiki.com/index.php?title=Orc_Legionnaire_(Crushbone)&action=edit",
            WikiContribution.EditUrl("Orc Legionnaire (Crushbone)"));

    // ---- the three paste shapes ----

    [Fact]
    public void NewDropOnExistingPageEmitsListItems()
    {
        var mob = Mob("Ambassador Dvinn", 12,
            new MobLoot("Black Heart", 5, 41.7) { LastAt = new DateTime(2026, 8, 8, 13, 3, 12) });
        var text = WikiContribution.BuildExport(
            [new(mob, PageWith("Dragoon Dirk"))], "Dranak", "Legends", "Crushbone",
            new DateTime(2026, 8, 8, 14, 0, 0));
        Assert.Contains("https://eqlwiki.com/index.php?title=Ambassador_Dvinn&action=edit", text);
        Assert.Contains("<li> {{:Black Heart}} <span class='drare'>(Uncommon)</span></li>", text);
        // #65 round four (Frankthetankk): the edit summary is summary-field-sized,
        // the itemized detail lives in the log reference, dated — a session can
        // span midnight, so a bare clock time is ambiguous.
        Assert.Contains("Suggested edit summary: observed drops (1 item, 12 kills).", text);
        // The SUMMARY LINE does not name EQBuddy (#217 ask 4, approved 2026-08-19): the
        // edit goes up under the player's account, so it describes what they observed
        // rather than advertising the tool. Pinned because "put the name back" is an easy
        // accident.
        //
        // Scoped to that line on purpose. The pack's own header — "EQBuddy → eqlwiki
        // contribution pack — <who>" — SHOULD say it: that is the app titling a document
        // for the person reading it, not text going onto someone else's wiki. Asserting
        // the whole pack has no "EQBuddy" in it fails on the header, and the distinction
        // between "what we paste" and "what we show" is the whole point of the ask.
        var summaryLine = text.Split('\n').Single(l => l.StartsWith("Suggested edit summary:"));
        Assert.DoesNotContain("EQBuddy", summaryLine);
        Assert.Contains("Log reference (for your own records, not the wiki) — Sat Aug 8 2026:", text);
        Assert.Contains("Black Heart ×5 in 12 kills (41.7%) — last at 13:03:12", text);
        // …and the item page's dropsfrom list gets its own edit link and paste line.
        Assert.Contains("https://eqlwiki.com/index.php?title=Black_Heart&action=edit", text);
        Assert.Contains("* [[Ambassador Dvinn]]", text);
        Assert.Contains("[[Crushbone]]", text);
    }

    [Fact]
    public void EmptyLootPageEmitsWholeKnownLootBlock()
    {
        var mob = Mob("Orc Thaumaturgist", 12,
            new MobLoot("Words of Cazic-Thule", 7, 58.3), new MobLoot("Bone Chips", 2, 16.7));
        var text = WikiContribution.BuildExport(
            [new(mob, PageWith())], "Dranak", "Legends", "Crushbone",
            new DateTime(2026, 8, 8, 14, 0, 0));
        Assert.Contains("| known_loot = ", text);
        Assert.Contains("<ul><li> {{:Words of Cazic-Thule}} <span class='drare'>(Common)</span>", text);
        Assert.Contains("</li><li> {{:Bone Chips}} <span class='drare'>(Rare)</span>", text);
        Assert.Contains("</li></ul>", text);
    }

    [Fact]
    public void MissingPageEmitsFullSkeletonWithZoneAndCategory()
    {
        var mob = Mob("Gnoll Reaver", 3, new MobLoot("Gnoll Fang", 1, 33.3));
        var text = WikiContribution.BuildExport(
            [new(mob, Missing)], "Dranak", "Legends", "Blackburrow",
            new DateTime(2026, 8, 8, 14, 0, 0));
        Assert.Contains("Create page:  https://eqlwiki.com/index.php?title=Gnoll_Reaver&action=edit", text);
        Assert.Contains("{{Namedmobpage", text);
        Assert.Contains("| name          = Gnoll Reaver", text);
        Assert.Contains("| zone          = [[Blackburrow]]", text);
        Assert.Contains("[[Category:Blackburrow]]", text);
        // 3 kills is far too thin for a label — the entry ships bare.
        Assert.Contains("<ul><li> {{:Gnoll Fang}}", text);
        Assert.DoesNotContain("Gnoll Fang}} <span", text);
    }

    [Fact]
    public void TierSuffixFoldsInEmittedWikitext()
    {
        var mob = Mob("Orc Centurion", 15, new MobLoot("Crushbone Shoulderpads +2", 4, 26.7));
        var text = WikiContribution.BuildExport(
            [new(mob, PageWith("Dragoon Dirk"))], "Dranak", "Legends", "Crushbone",
            new DateTime(2026, 8, 8, 14, 0, 0));
        // The wiki catalogs base items; the +2 stays out of the transclusion but the
        // edit-summary evidence keeps the exact observed name.
        Assert.Contains("{{:Crushbone Shoulderpads}}", text);
        Assert.Contains("Crushbone Shoulderpads +2 ×4 in 15 kills", text);
    }

    [Fact]
    public void ResolvedZoneSuffixedTitleWinsOverDisplayName()
    {
        var lookup = new MobLookupResult(
            new MobInfo
        {
            IsCreaturePage = true,
                Name = "Orc Legionnaire", PageTitle = "Orc Legionnaire (Crushbone)",
                Drops = [("Dragoon Dirk", "Common")],
            },
            ItemLookupState.Live, DateTime.UtcNow);
        var mob = Mob("Orc Legionnaire", 10, new MobLoot("Black Heart", 2, 20));
        var text = WikiContribution.BuildExport(
            [new(mob, lookup)], "Dranak", "Legends", "Crushbone",
            new DateTime(2026, 8, 8, 14, 0, 0));
        Assert.Contains("title=Orc_Legionnaire_(Crushbone)&action=edit", text);
    }

    /// <summary>The observed stat block (#65 round two): zone at kill time, money as
    /// the wiki's low–high range, factions with hit counts — and the reconciliation
    /// framing plus the 10+ kill gate baked into the wording.</summary>
    [Fact]
    public void StatBlockCarriesZoneMoneyRangeAndFactions()
    {
        var mob = Mob("Packmaster Dledsh", 12, new MobLoot("Packmaster's Whip", 3, 25.0)) with
        {
            Zone = "The Warrens",
            CoinMin = 22,
            CoinMax = 100,
            Factions = [new MobFactionHit("Kobolds of Fireclaw", -5, 12)],
        };
        var text = WikiContribution.BuildExport(
            [new(mob, Missing)], "Dranak", "Legends", "The Greater Faydark",
            new DateTime(2026, 8, 9, 14, 0, 0));
        Assert.Contains("compare, don't overwrite", text);
        // The KILL zone wins over wherever the player is standing at export time.
        Assert.Contains("zone (at kill time): The Warrens", text);
        Assert.Contains("money per kill: 2s 2c – 1g", text);
        Assert.Contains("faction: Kobolds of Fireclaw -5 (12 of 12 kills)", text);
    }

    /// <summary>Levels ride /consider bounds into the stat block and the page
    /// skeleton's level field (#65 round three) — a range when cons disagreed,
    /// a single honest value when they didn't, nothing when nobody conned.</summary>
    [Fact]
    public void StatBlockAndSkeletonCarryConsideredLevels()
    {
        var ranged = Mob("Packmaster Dledsh", 12, new MobLoot("Packmaster's Whip", 3, 25.0)) with
        {
            Zone = "The Warrens", LevelMin = 12, LevelMax = 15,
        };
        var text = WikiContribution.BuildExport(
            [new(ranged, Missing)], "Dranak", "Legends", "The Warrens",
            new DateTime(2026, 8, 9, 14, 0, 0));
        Assert.Contains("level (from /consider): 12 - 15", text);
        Assert.Contains("| level         = 12 - 15", text);   // Namedmobpage skeleton

        var single = Mob("Gnoll Reaver", 12, new MobLoot("Gnoll Fang", 4, 33.3)) with
        {
            LevelMin = 8, LevelMax = 8,
        };
        text = WikiContribution.BuildExport(
            [new(single, Missing)], "Dranak", "Legends", "Blackburrow",
            new DateTime(2026, 8, 9, 14, 0, 0));
        Assert.Contains("level (from /consider): 8 (every /con this session agreed", text);
        Assert.Contains("| level         = 8", text);
    }

    [Fact]
    public void StatBlockReportsConfirmedFactionAbsenceAndThinSamples()
    {
        var mob = Mob("Gnoll Reaver", 3, new MobLoot("Gnoll Fang", 1, 33.3)) with
        {
            Zone = "Blackburrow",
        };
        var text = WikiContribution.BuildExport(
            [new(mob, Missing)], "Dranak", "Legends", "Blackburrow",
            new DateTime(2026, 8, 9, 14, 0, 0));
        Assert.Contains("thin sample", text);
        Assert.Contains("no hits observed across 3 kills", text);
        Assert.DoesNotContain("money per kill", text);   // no coin seen ≠ drops nothing
    }

    [Fact]
    public void NothingNewSaysSoInsteadOfEmittingEmptySections()
    {
        var mob = Mob("Ambassador Dvinn", 12, new MobLoot("Dragoon Dirk", 3, 25));
        var text = WikiContribution.BuildExport(
            [new(mob, PageWith("Dragoon Dirk"))], "Dranak", "Legends", "Crushbone",
            new DateTime(2026, 8, 8, 14, 0, 0));
        Assert.Contains("already on the wiki", text);
        Assert.DoesNotContain("===", text);
    }

    [Fact]
    public void PendingLookupsAreNamedNotSilentlyDropped()
    {
        var known = Mob("Ambassador Dvinn", 12, new MobLoot("Black Heart", 5, 41.7));
        var pending = Mob("Emperor Crush", 2, new MobLoot("Crown of Crush", 1, 50));
        var text = WikiContribution.BuildExport(
            [new(known, PageWith("Dragoon Dirk")), new(pending, null)],
            "Dranak", "Legends", "Crushbone", new DateTime(2026, 8, 8, 14, 0, 0));
        Assert.Contains("Not checked against the wiki yet", text);
        Assert.Contains("Emperor Crush", text);
    }

    // ---- #65 round five (Frankthetankk) ----

    /// <summary>The new-page template must name the zone the creature was KILLED in, not
    /// wherever the player is standing when the pack is generated. Frankthetankk caught an
    /// Innoruuk killed in Plane of Hate whose {{Namedmobpage}} zone and Category both said
    /// Nagafen's Lair, while the cross-references in the SAME entry said Hate — two code
    /// paths reading two sources for one fact, and the template's would have published a
    /// wrong zone if pasted as-is.</summary>
    [Fact]
    public void TheNewPageTemplateUsesTheKillZoneNotWhereYouAreStandingNow()
    {
        var mob = new MobSummary("Innoruuk, the Prince of Hate", 3, 3, 40, 0, 0,
            [new MobLoot("Hate Cloak", 1, 33)])
        { Zone = "The Plane of Hate - Solo 1 (Awakened)" };

        // NotFound (not null) is what "the wiki has no such page" looks like — a null
        // lookup means we never asked, which is Unknown and prints nothing.
        var missing = new MobLookupResult(null, ItemLookupState.NotFound, null);
        var export = WikiContribution.BuildExport(
            [new WikiContribution.MobObservation(mob, missing)],
            "Dranak", "legends",
            currentZone: "Nagafen's Lair - Solo 4 (Refined)",
            now: new DateTime(2026, 8, 14, 20, 0, 0));

        Assert.Contains("| zone          = [[The Plane of Hate - Solo 1 (Awakened)]]", export);
        Assert.Contains("[[Category:The Plane of Hate - Solo 1 (Awakened)]]", export);
        // And the zone the player happens to be in never appears anywhere in the entry.
        Assert.DoesNotContain("Nagafen", export);
    }
    /// <summary>
    /// #226 (LeBigNasty): *"Innoruk, for example, is checking against the Lore page and not
    /// against the creature page."*
    ///
    /// A lore article and an unfilled creature page BOTH parse to zero drops, and until now
    /// both classified as <c>PageHasNoLoot</c> — which is the status that means "everything
    /// you looted is news to this page" and puts the creature in the contribution pack. So
    /// the pack would have offered to paste a loot table onto an article about a god. The
    /// wiki is the shared reference and this app's whole claim on it is that it only ever
    /// suggests what the player actually observed, on the page it belongs on.
    /// </summary>
    [Fact]
    public void ALorePageIsNotAnEmptyCreaturePage()
    {
        // No {{Namedmobpage}} anywhere — a deity article, as the wiki really serves one.
        var lore = WikiContribution.Classify(
            new MobLookupResult(
                EqlWikiMobService.Parse("'''Innoruk''' is the god of hate. [[Category:Deities]]", "Innoruk"),
                ItemLookupState.Cached, DateTime.UtcNow),
            "Tainted Heart");

        Assert.Equal(WikiDropStatus.PageIsNotACreature, lore);

        // The negative that stops it going vacuous: a REAL creature page with an empty loot
        // field must still be PageHasNoLoot, because that one is the best find in the pack.
        var emptyCreature = WikiContribution.Classify(
            new MobLookupResult(
                EqlWikiMobService.Parse(
                    """
                    {{Namedmobpage
                    | name = Innoruk
                    | known_loot =
                    }}
                    """, "Innoruk (Plane of Hate)"),
                ItemLookupState.Cached, DateTime.UtcNow),
            "Tainted Heart");

        Assert.Equal(WikiDropStatus.PageHasNoLoot, emptyCreature);
    }

    /// <summary>And it must never reach the export, because the export's whole job is to
    /// produce something a player pastes onto that page.</summary>
    [Fact]
    public void ALorePageContributesNothingToTheExport()
    {
        var export = WikiContribution.BuildExport(
            [new WikiContribution.MobObservation(
                Mob("Innoruk", 4, new MobLoot("Tainted Heart", 1, null)),
                new MobLookupResult(
                    EqlWikiMobService.Parse("'''Innoruk''' is the god of hate.", "Innoruk"),
                    ItemLookupState.Cached, DateTime.UtcNow))],
            "Testchar", "test", "The Plane of Hate", new DateTime(2026, 8, 22));

        Assert.DoesNotContain("known_loot", export);
        Assert.DoesNotContain("Tainted Heart", export);
    }

}
