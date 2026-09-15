using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE FARM GEAR ENGINE** (DRA-71 D6, Fable plan P8; Founder smoke items 4a/4b).
///
/// <para><c>GearUpgradesTests</c> covers the sweep — what counts as an upgrade and what is
/// refused. This file covers what the ENGINE does with what comes back: the zone is the join
/// key (HOME-005), the weight is a count of your open upgrades, the player's own observed
/// drops outrank pages (HOME-003), every catalog line carries the estimate label (HOME-004),
/// and each of the three ways to have nothing to say is a different sentence.</para>
/// </summary>
public class RecommendationsGearTests
{
    // ---- fixtures ----------------------------------------------------------------------

    private static ItemCatalog.Record Record(
        string name, string slot, int ac, string[]? zones = null, string[]? quests = null) =>
        new()
        {
            Name = name, StatsText = $"Slot: {slot}\nAC: {ac}", Slots = [slot], Ac = ac,
            DropZones = zones?.ToList(), Quests = quests?.ToList(),
        };

    private static WornItem Worn(string name, string slot, int ac) =>
        new(name, name, slot, ItemStatsBlock.Parse([$"Slot: {slot}", $"AC: {ac}"]));

    /// <summary>The default is <see cref="ResolvedLevel.Unknown"/> with no bands, which stands
    /// DRA-84 D2's gate down — so every test written before it keeps testing what it was written
    /// to test, and a test about the gate has to ask for both halves by name.</summary>
    private static HelperInputs Gear(
        IReadOnlyList<WornItem> worn,
        ItemCatalog? catalog,
        GearIntent intent = GearIntent.UpgradeWorn,
        IReadOnlyList<string>? picks = null,
        bool includeQuests = false,
        IReadOnlyList<MobSummary>? pool = null,
        IReadOnlyList<SessionRow>? sessions = null,
        int? level = null,
        ZoneLevels? bands = null) =>
        new(ZoneHistory.Fold(sessions ?? [], pool ?? []), pool ?? [], null, [], [], [], [],
            false, [], [], null,
            level is { } l
                ? new ResolvedLevel(l, LevelSource.Observed, new DateTime(2026, 9, 14, 20, 0, 0))
                : ResolvedLevel.Unknown)
        {
            Worn = worn, Items = catalog, GearIntent = intent,
            WornPicks = picks ?? [], IncludeQuests = includeQuests, Bands = bands,
        };

    /// <summary>A fixture band table. Named zones only, so a test says which band it is about
    /// and nothing else can leak in.</summary>
    private static ZoneLevels Bands(params (string Zone, int Min, int? Max)[] rows) =>
        new(rows.ToDictionary(
                r => r.Zone,
                r => new ZoneLevels.Band(
                    r.Min, r.Max,
                    r.Max is { } m ? (m == r.Min ? $"{r.Min}" : $"{r.Min}-{m}") : $"{r.Min}+")),
            new Dictionary<string, string>());

    private static RecommendationSet Rank(HelperInputs inputs) =>
        Recommendations.Rank(inputs, [HelperGoal.FarmGear]);

    // ---- the happy path -----------------------------------------------------------------

    /// <summary>A zone that drops something better than what you wear is a row, and the row
    /// names the item, the thing it beats and the slot — with the estimate label, because the
    /// claim came out of a file EQBuddy ships.</summary>
    [Fact]
    public void AZoneThatDropsAnUpgradeIsAnAnswerAndSaysWhatItBeats()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Froglok Bone Helm", "HEAD", 9, ["Lower Guk"])])));

        var top = Assert.Single(set.Top);
        Assert.Equal(RecommendationKind.Zone, top.Kind);
        Assert.Equal("Lower Guk", top.Zone);
        Assert.Equal([HelperGoal.FarmGear], top.Goals);

        var fact = Assert.Single(top.Why.OfType<GearUpgradeFact>());
        Assert.Equal("Froglok Bone Helm", fact.Item);
        Assert.Equal("Rusty Helm", fact.Over);
        Assert.Equal(Evidence.Catalog, fact.Evidence);

        var sentence = HelperPresentation.Why(fact);
        Assert.Contains("Froglok Bone Helm", sentence);
        Assert.Contains("beats the Rusty Helm", sentence);
        Assert.Contains("head", sentence);
        Assert.Contains("+5 AC", sentence);
        Assert.EndsWith(HelperPresentation.CatalogLabel, sentence);
    }

    /// <summary>Every door a gear row emits opens something: the map for a zone, the quest
    /// list for a quest, and the Gear room either way.</summary>
    [Fact]
    public void EveryGearRowCarriesDoorsThatOpenSomething()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([
                Record("Froglok Bone Helm", "HEAD", 9, ["Lower Guk"]),
                Record("Blessed Helm", "HEAD", 12, quests: ["A Blessing"]),
            ]),
            includeQuests: true));

        Assert.Contains(set.Top, r => r.Kind == RecommendationKind.Zone
            && r.Doors.Any(d => d.Kind == HelperDoorKind.World && d.Target == "Lower Guk"));
        Assert.Contains(set.Top, r => r.Kind == RecommendationKind.Quest
            && r.Doors.Any(d => d.Kind == HelperDoorKind.QuestCatalog && d.Target == "A Blessing"));
        Assert.All(set.Top,
            r => Assert.Contains(r.Doors, d => d.Kind == HelperDoorKind.Gear));
        Assert.All(set.Top, r => Assert.All(r.Doors, d =>
            Assert.True(d.Kind == HelperDoorKind.WikiFaction
                        || HelperPresentation.AddressFor(d.Kind) is not null)));
    }

    /// <summary>A quest-sourced upgrade is a QUEST row, not a place, and its headline says so
    /// — a quest title with no suffix reads as somewhere to travel to.</summary>
    [Fact]
    public void AQuestSourcedUpgradeIsAQuestRowWithItsOwnHeadline()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["A Blessing"])]),
            includeQuests: true));

        var top = Assert.Single(set.Top);
        Assert.Equal(RecommendationKind.Quest, top.Kind);
        Assert.Equal("A Blessing", top.Subject);
        Assert.Equal("", top.Zone);
        Assert.Equal("A Blessing — quest", HelperPresentation.Headline(top));
    }

    // ---- the zone is the join key (HOME-005) ---------------------------------------------

    /// <summary>
    /// **THE DIFFERENTIATOR**: a zone that pays your experience rate AND drops a gear upgrade
    /// is ONE row serving two goals, which is the first sort key.
    /// </summary>
    [Fact]
    public void AZoneThatServesLevelUpAndFarmGearBecomesOneRow()
    {
        MobSummary[] pool = [new("a froglok tad", 200, 200, 30, 0, 0, []) { Zone = "Lower Guk" }];
        SessionRow[] sessions =
        [
            new(1, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(5),
                5 * 3600, 5 * 3600, "ended", "Lower Guk", 0, 60, 0, 0, 0, 0, "", ""),
        ];

        var inputs = Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Froglok Bone Helm", "HEAD", 9, ["Lower Guk"])]),
            pool: pool, sessions: sessions);

        var set = Recommendations.Rank(inputs, [HelperGoal.LevelUp, HelperGoal.FarmGear]);

        var top = Assert.Single(set.Top);
        Assert.Equal("Lower Guk", top.Zone);
        Assert.Equal(2, top.Goals.Count);
        Assert.Contains(HelperGoal.LevelUp, top.Goals);
        Assert.Contains(HelperGoal.FarmGear, top.Goals);
        Assert.Contains(top.Why, w => w is ZoneXpRateFact);
        Assert.Contains(top.Why, w => w is GearUpgradeFact);
    }

    /// <summary>The weight is how many of your open upgrades a place accounts for, over the
    /// best one's — the same measure the Gear room's own zone rollup has ranked camps by since
    /// 1.84, and a count rather than a taste.</summary>
    [Fact]
    public void AZoneThatFeedsMoreOfYourUpgradesOutranksOneThatFeedsFewer()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4), Worn("Tattered Cloak", "BACK", 2)],
            new ItemCatalog([
                Record("Froglok Bone Helm", "HEAD", 9, ["Lower Guk"]),
                Record("a froglok silk cloak", "BACK", 8, ["Lower Guk"]),
                Record("Skeletal Cloak", "BACK", 6, ["Befallen"]),
            ])));

        Assert.Equal("Lower Guk", set.Top[0].Zone);
        Assert.Equal("Befallen", set.Top[1].Zone);
        Assert.True(set.Top[0].Weight > set.Top[1].Weight);
    }

    /// <summary>An item that drops in three zones is offered under every one of them — the
    /// question a row answers is "if I camp here tonight, what can this place still give me",
    /// and a per-zone list that hid a valid camp would make its own heading lie.</summary>
    [Fact]
    public void AnItemThatDropsInSeveralZonesAppearsUnderEachOfThem()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([
                Record("Bone Helm", "HEAD", 9, ["Lower Guk", "Befallen", "Unrest"]),
            ])));

        Assert.Equal(3, set.Top.Count + set.Withheld);
        Assert.All(set.Top, r => Assert.Contains(r.Why.OfType<GearUpgradeFact>(),
            f => f.Item == "Bone Helm"));
    }

    // ---- personal evidence (HOME-003) ----------------------------------------------------

    /// <summary>
    /// **A ZONE WHERE YOU HAVE SEEN IT DROP OUTRANKS ONE YOU HAVE ONLY READ ABOUT**, through
    /// <see cref="Recommendation.HasPersonalEvidence"/> rather than a weight this slice
    /// invented — and the sentence names the creature and its denominator.
    ///
    /// <para>The catalog-only zone is given the HEAVIER weight on purpose: personal evidence
    /// is the sort key BEFORE weight, so an arrangement where the observed zone also weighed
    /// more could not tell the two rules apart.</para>
    /// </summary>
    [Fact]
    public void AZoneWhereYouHaveSeenItDropOutranksOneYouHaveOnlyReadAbout()
    {
        MobSummary[] pool =
        [
            new("a froglok shaman", 340, 340, 30, 0, 0, [new MobLoot("Bone Helm", 2, 0.6)])
                { Zone = "Lower Guk" },
        ];

        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4), Worn("Tattered Cloak", "BACK", 2)],
            new ItemCatalog([
                Record("Bone Helm", "HEAD", 9, ["Lower Guk"]),
                // Befallen feeds TWO upgrades, so it carries the heavier weight.
                Record("Skeletal Cloak", "BACK", 8, ["Befallen"]),
                Record("Skeletal Helm", "HEAD", 7, ["Befallen"]),
            ]),
            pool: pool));

        Assert.Equal("Lower Guk", set.Top[0].Zone);
        Assert.True(set.Top[0].HasPersonalEvidence);
        Assert.True(set.Top[1].Weight > set.Top[0].Weight, "the fixture stopped proving the sort");

        var seen = Assert.Single(set.Top[0].Why.OfType<GearDropSeenFact>());
        Assert.Equal("a froglok shaman", seen.Mob);
        Assert.Equal(2, seen.Drops);
        Assert.Equal(340, seen.Kills);
        var sentence = HelperPresentation.Why(seen);
        Assert.Contains("a froglok shaman", sentence);
        Assert.Contains("2 of your 340 kills", sentence);
        Assert.DoesNotContain(HelperPresentation.CatalogLabel, sentence);
    }

    /// <summary>
    /// **THE CREATURE IS ANSWERED ONCE** (trap 4). Where the player's own kills named it, the
    /// catalog's own name is NOT also printed — two sentences naming two creatures for one
    /// item is a reconciliation nobody should have to do.
    /// </summary>
    [Fact]
    public void WhereYourOwnKillsNameTheCreatureTheCatalogDoesNotAlsoNameOne()
    {
        var record = Record("Bone Helm", "HEAD", 9, ["Lower Guk"]);
        record.DropMobs = new() { ["Lower Guk"] = ["a froglok knight"] };

        MobSummary[] pool =
        [
            new("a froglok shaman", 340, 340, 30, 0, 0, [new MobLoot("Bone Helm", 2, 0.6)])
                { Zone = "Lower Guk" },
        ];

        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)], new ItemCatalog([record]), pool: pool));

        var catalogFact = Assert.Single(set.Top[0].Why.OfType<GearUpgradeFact>());
        Assert.Equal("", catalogFact.Who);
        Assert.DoesNotContain("a froglok knight", HelperPresentation.Why(catalogFact));
        Assert.Equal("a froglok shaman",
            Assert.Single(set.Top[0].Why.OfType<GearDropSeenFact>()).Mob);
    }

    /// <summary>With no kill of your own, the catalog's creature rides the catalog line — and
    /// where the page named nobody, the clause is simply absent (trap 73).</summary>
    [Fact]
    public void TheCatalogCreatureRidesTheLineOnlyWhenThePageNamedOne()
    {
        var named = Record("Bone Helm", "HEAD", 9, ["Lower Guk"]);
        named.DropMobs = new() { ["Lower Guk"] = ["a froglok knight"] };

        var withName = Rank(Gear([Worn("Rusty Helm", "HEAD", 4)], new ItemCatalog([named])));
        Assert.Contains("a froglok knight drops it",
            HelperPresentation.Why(withName.Top[0].Why.OfType<GearUpgradeFact>().First()));

        var anonymous = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Bone Helm", "HEAD", 9, ["Lower Guk"])])));
        Assert.DoesNotContain("drops it",
            HelperPresentation.Why(anonymous.Top[0].Why.OfType<GearUpgradeFact>().First()));
    }

    // ---- the caps say what they held back (trap 50) --------------------------------------

    /// <summary>A row names a few upgrades and says how many it held back — a place that
    /// listed eleven items would be a shopping list with a heading.</summary>
    [Fact]
    public void ARowNamesAFewUpgradesAndSaysWhatItHeldBack()
    {
        var worn = Enumerable.Range(1, 6)
            .Select(i => Worn($"a rusty thing {i}", $"SLOT{i}", 1)).ToList();
        var catalog = new ItemCatalog(
            Enumerable.Range(1, 6).Select(i => Record($"a fine thing {i}", $"SLOT{i}", 9, ["Lower Guk"])));

        var set = Rank(Gear(worn, catalog));

        var top = Assert.Single(set.Top);
        Assert.Equal(Recommendations.GearNamedPerRow, top.Why.OfType<GearUpgradeFact>().Count());
        Assert.Equal(6 - Recommendations.GearNamedPerRow, top.WithheldWhy);
        Assert.NotEmpty(HelperPresentation.WithheldWhy(top.WithheldWhy));
    }

    /// <summary>The sweep's own per-anchor cap is reported on the SET, because it is spent
    /// before any row exists and a count hung on whichever row was built first would be a
    /// number pointing at the wrong thing.</summary>
    [Fact]
    public void TheSweepsOwnCapIsReportedOnTheSet()
    {
        var catalog = new ItemCatalog(
            Enumerable.Range(1, GearUpgrades.MaxPerAnchor + 3)
                .Select(i => Record($"helm {i:00}", "HEAD", 4 + i, ["Lower Guk"])));

        var set = Rank(Gear([Worn("Rusty Helm", "HEAD", 4)], catalog));

        Assert.Equal(3, set.GearWithheld);
        Assert.NotEmpty(HelperPresentation.GearWithheld(set.GearWithheld));
        Assert.Empty(HelperPresentation.GearWithheld(0));
    }

    // ---- the three ways to have nothing to say --------------------------------------------

    /// <summary>No inventory dump is a DIFFERENT state from "nothing beats it", and it is the
    /// one with a command behind it.</summary>
    [Fact]
    public void NoInventoryDumpIsItsOwnGapWithItsOwnSentence()
    {
        var set = Rank(Gear([], new ItemCatalog([])));

        var gap = Assert.Single(set.Gaps);
        Assert.Equal(GoalGapReason.NoInventoryDump, gap.Reason);
        Assert.Empty(set.Top);
        Assert.Contains("what you are wearing", HelperPresentation.Gap(gap));
    }

    /// <summary>
    /// **"NOTHING BEATS IT" NAMES THE CATALOG AND NEVER THE GAME.**
    ///
    /// <para>A best-in-slot claim with a minus sign in front of it is still a best-in-slot
    /// claim, and the Gear Locker's lock has refused to make it since #104. Asserted on the
    /// WORDS, because this is a rule about what a sentence says.</para>
    /// </summary>
    [Fact]
    public void NothingBeatsItIsAStatementAboutTheCatalog()
    {
        var set = Rank(Gear(
            [Worn("Splendid Helm", "HEAD", 40)],
            new ItemCatalog([Record("Rusty Helm", "HEAD", 4, ["Lower Guk"])])));

        var gap = Assert.Single(set.Gaps);
        Assert.Equal(GoalGapReason.NoCatalogUpgrade, gap.Reason);

        var sentence = HelperPresentation.Gap(gap);
        Assert.Contains("catalog", sentence, StringComparison.OrdinalIgnoreCase);
        foreach (var claim in new[] { "best in slot", "best-in-slot", "the best" })
            Assert.DoesNotContain(claim, sentence, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// **"FARM TO SELL" LEAVES THE DOMINANCE SWEEP ENTIRELY** (DRA-71 D7).
    ///
    /// <para>Its anchor is the player's own loot rather than what they wear, so a fixture with
    /// a worn item, a catalog full of upgrades and NO loot history answers nothing about gear
    /// — which is the assertion: a single leaked <see cref="GearUpgradeFact"/> here would mean
    /// the sell question had been routed through a comparison it has no anchor for. The gap it
    /// draws is about sell EVIDENCE and no longer about a slice that has not happened.</para>
    /// </summary>
    [Fact]
    public void FarmToSellNeverProducesAnUpgradeRowAndSaysWhatItIsMissing()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Bone Helm", "HEAD", 9, ["Lower Guk"])]),
            intent: GearIntent.FarmToSell));

        Assert.Empty(set.Top);
        Assert.Empty(set.Top.SelectMany(r => r.Why).OfType<GearUpgradeFact>());
        var gap = Assert.Single(set.Gaps);
        Assert.Equal(GoalGapReason.NoSellEvidence, gap.Reason);
        Assert.NotEmpty(HelperPresentation.Gap(gap));
    }

    /// <summary>
    /// **THE SELL ANSWER IS TWO MEASUREMENTS OF THIS PLAYER** (DRA-71 D7, plan P9; Founder
    /// smoke item 4c).
    ///
    /// <para>What the pool says dropped here, priced at what a vendor actually paid THEM — so
    /// the row is <see cref="Evidence.Personal"/> throughout and carries no estimate label. The
    /// catalog is deliberately loaded with an upgrade that would dominate the worn helm: if any
    /// of it reached the answer, the sell question would be wearing the gear question's
    /// clothes.</para>
    /// </summary>
    [Fact]
    public void FarmToSellPricesYourOwnDropsAtWhatAVendorPaidYou()
    {
        MobSummary[] pool =
        [
            new("a froglok tad", 340, 340, 30, 0, 0,
                [new MobLoot("Froglok Blood", 12, 3.5)]) { Zone = "Lower Guk" },
        ];
        var inputs = Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Bone Helm", "HEAD", 9, ["Lower Guk"])]),
            intent: GearIntent.FarmToSell,
            pool: pool) with
        {
            Sales = [new SaleRoll("Froglok Blood", 4, 320)],
        };

        var top = Assert.Single(Recommendations.Rank(inputs, [HelperGoal.FarmGear]).Top);
        Assert.Equal("Lower Guk", top.Zone);
        Assert.Empty(top.Why.OfType<GearUpgradeFact>());

        var fact = Assert.Single(top.Why.OfType<SellableDropFact>());
        Assert.Equal("Froglok Blood", fact.Item);
        Assert.Equal(12, fact.Drops);
        Assert.Equal(340, fact.Kills);
        Assert.Equal(80, fact.CopperEach);
        Assert.Equal(Evidence.Personal, fact.Evidence);

        var sentence = HelperPresentation.Why(fact);
        Assert.Contains("a froglok tad", sentence);
        Assert.Contains("8s", sentence);
        Assert.DoesNotContain(HelperPresentation.CatalogLabel, sentence);
    }

    /// <summary>
    /// **THE CATALOG'S PRICE SPEAKS ONLY WHERE YOURS CANNOT, AND IT BRINGS ITS CONDITION**
    /// (DRA-71 D7, plan P9).
    ///
    /// <para>The survey of the cached item pages is why this arm is shaped like this: eqlwiki
    /// states its vendor value at a Charisma and a faction standing that differ per page, so
    /// the number is a quote somebody was given. Printing it without the page's own condition
    /// would turn one editor's Charisma into a fact about the object — so the condition is in
    /// the sentence, and the estimate label is there by construction because the fact is
    /// tagged Catalog.</para>
    ///
    /// <para><b>It draws nothing against the catalog this build ships</b>, which is the second
    /// assertion here: the promoter learned <c>MerchantCopper</c> in this slice and the values
    /// arrive with the next weekly refresh, so a fixture is the only place this arm can be
    /// proved today.</para>
    /// </summary>
    [Fact]
    public void ACatalogPriceIsUsedOnlyWhereYouHaveNeverSoldOneAndCarriesItsCondition()
    {
        MobSummary[] pool =
        [
            new("a froglok tad", 340, 340, 30, 0, 0,
                [new MobLoot("Froglok Blood", 12, 3.5)]) { Zone = "Lower Guk" },
        ];
        var catalog = new ItemCatalog([
            new ItemCatalog.Record
            {
                Name = "Froglok Blood", MerchantCopper = 45,
                MerchantCondition = "VALUE TO VENDOR with CHA : 80 and faction at Indifferently",
            },
        ]);
        var inputs = Gear([Worn("Rusty Helm", "HEAD", 4)], catalog,
            intent: GearIntent.FarmToSell, pool: pool);

        var top = Assert.Single(Recommendations.Rank(inputs, [HelperGoal.FarmGear]).Top);
        var fact = Assert.Single(top.Why.OfType<CatalogValueFact>());
        Assert.Equal(45, fact.Copper);
        Assert.Equal(Evidence.Catalog, fact.Evidence);

        var sentence = HelperPresentation.Why(fact);
        Assert.Contains("CHA : 80", sentence);
        Assert.Contains("Charisma", sentence);
        Assert.Contains(HelperPresentation.CatalogLabel, sentence);

        // The prove-fail for the "only where yours cannot" clause: give the player a sale of
        // the same item and the catalog's quote must vanish rather than sit beside it. Two
        // prices for one object is the shape a reader has to reconcile (trap 4).
        var sold = inputs with { Sales = [new SaleRoll("Froglok Blood", 4, 320)] };
        var resold = Assert.Single(Recommendations.Rank(sold, [HelperGoal.FarmGear]).Top);
        Assert.Empty(resold.Why.OfType<CatalogValueFact>());
        Assert.Single(resold.Why.OfType<SellableDropFact>());
    }

    /// <summary>
    /// <b>The vendor-value tripwire FIRED on the 2026-09-15 refresh (DRA-84 D3)</b>, which is
    /// what it was for: the row used to assert the shipped catalog carried no
    /// <c>MerchantCopper</c> at all, "so the day the weekly refresh fills it in, this fails and
    /// somebody looks at the sentences it turns on rather than finding out from a player".
    /// It is now a survey of what actually ships, pinned so the next move is visible too.
    ///
    /// <para><b>The distinct count is the load-bearing one</b> (trap 73): 773 prices drawn from
    /// 403 distinct values is a parser reading a per-item field, not one template quoted 773
    /// times. A future refresh that collapses that ratio is a parser regression wearing a
    /// coverage gain's clothes.</para>
    ///
    /// <para><b>The open question this refresh raises, referred to Helm rather than decided
    /// here:</b> only 205 of the 773 priced pages state the Charisma/faction their quote was
    /// taken at, so <b>568 of them draw the flat sentence with no "yours will differ" clause</b>
    /// — carried only by the Catalog estimate label, because there is no condition on the page
    /// to quote. The arm above proves the conditioned shape; this names the size of the
    /// unconditioned one so it is not a silent hole.</para>
    /// </summary>
    [Fact]
    public void TheShippedCatalogsVendorValuesAreASurveyedQuoteAndNotATemplate()
    {
        var priced = ItemCatalog.Default.All.Where(r => r.MerchantCopper is not null).ToList();
        var conditioned = priced.Count(r => !string.IsNullOrEmpty(r.MerchantCondition));

        // The counts the promoter's own survey printed into items-catalog-report.md.
        Assert.Equal(773, priced.Count);
        Assert.Equal(205, conditioned);
        Assert.Equal(568, priced.Count - conditioned);
        Assert.Equal(403, priced.Select(r => r.MerchantCopper!.Value).Distinct().Count());

        // A condition without a price is an orphan caveat: a sentence qualifying a number
        // that is not there. The promoter only sets one beside a parsed value — asserted,
        // not assumed, because that pairing is what makes the quote readable as a quote.
        static bool IsOrphanCaveat(ItemCatalog.Record r) =>
            r.MerchantCopper is null && !string.IsNullOrEmpty(r.MerchantCondition);

        Assert.DoesNotContain(ItemCatalog.Default.All, IsOrphanCaveat);

        // The committed negative, because a forbid-scan that has never fired is a guard aimed
        // at nothing (trap 78): the same predicate, over a catalog that HAS one.
        Assert.Contains(
            new ItemCatalog([
                new ItemCatalog.Record
                {
                    Name = "A Condition With No Price",
                    MerchantCondition = "VALUE TO VENDOR with CHA : 80",
                },
            ]).All,
            IsOrphanCaveat);
    }

    /// <summary>A profile with no catalog at all is the "no dump" state and not a crash — a
    /// fixture without one is a test, not an error.</summary>
    [Fact]
    public void NoCatalogAnswersNothingRatherThanThrowing()
    {
        var set = Rank(Gear([Worn("Rusty Helm", "HEAD", 4)], null));

        Assert.Empty(set.Top);
        Assert.Equal(GoalGapReason.NoCatalogUpgrade, Assert.Single(set.Gaps).Reason);
    }

    // ---- HOME-006 --------------------------------------------------------------------------

    /// <summary>No gear sentence claims a place is safe, easy or survivable — the vocabulary
    /// sweep applied to this slice's own new words, over a fixture rather than over
    /// constants.</summary>
    [Fact]
    public void NoGearSentenceClaimsAPlaceIsSafe()
    {
        MobSummary[] pool =
        [
            new("a froglok shaman", 340, 340, 30, 0, 0, [new MobLoot("Bone Helm", 2, 0.6)])
                { Zone = "Lower Guk" },
        ];
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([
                Record("Bone Helm", "HEAD", 9, ["Lower Guk"]),
                Record("Blessed Helm", "HEAD", 12, quests: ["A Blessing"]),
            ]),
            includeQuests: true, pool: pool));

        string[] banned =
            ["safe", "safer", "safely", "easy", "easier", "survivab", "forgiving", "risk-free"];
        var sentences = set.Top
            .SelectMany(r => r.Why.Select(HelperPresentation.Why))
            .Concat(set.Top.Select(HelperPresentation.Headline))
            .ToList();

        Assert.NotEmpty(sentences);
        foreach (var sentence in sentences)
            foreach (var word in banned)
                Assert.DoesNotContain(word, sentence, StringComparison.OrdinalIgnoreCase);
    }

    // ---- DRA-84 D2: the band gate (plan P2, Founder acceptance 3) -------------------------
    //
    // The Founder failed this room for offering camps his level could not farm. The gate
    // REFUSES a zone row whose eqlwiki band sits outside his level, on two named distances, and
    // says what it held back. Everything below is either a boundary on one of those two numbers
    // or one of the ways the gate must stand down.

    private static ItemCatalog OneHelmIn(params string[] zones) =>
        new([Record("Bone Helm", "HEAD", 9, zones)]);

    private static RecommendationSet RankIn(
        int? level, ZoneLevels? bands, params string[] zones) =>
        Rank(Gear([Worn("Rusty Helm", "HEAD", 4)], OneHelmIn(zones),
            level: level, bands: bands));

    /// <summary>
    /// **THE FOUNDER'S EXHIBIT, AND THE FINDING THAT CAME OUT OF MEASURING IT.**
    ///
    /// <para>Crushbone really is `5-20` on eqlwiki and the Founder's own ceiling is level 29,
    /// which is the complaint in one row. <b>At 29 this gate does NOT refuse it</b>:
    /// 29 − 20 = 9, and <see cref="Recommendations.OutgrownBy"/> is 10. It refuses from 30.</para>
    ///
    /// <para>That is the plan's number doing exactly what the plan said — *"`OutgrownBy` (= 10,
    /// reused, not re-derived)"* — and it is one level short of the plan's own worked example,
    /// which asserts Crushbone is refused for a 29. Re-deriving the constant to close a
    /// one-level gap would be inventing a number to fit an anecdote, which is the opposite of
    /// what "reused, not re-derived" asks for. So the arithmetic ships, the boundary is pinned
    /// here in both directions, and the product call — whether the Founder's 29 should see
    /// Crushbone — is Helm's with this measurement in front of it.</para>
    ///
    /// <para>The band comes from <see cref="ZoneLevels.Default"/> rather than a fixture, so this
    /// fails if the shipped data stops saying it.</para>
    /// </summary>
    [Fact]
    public void TheFoundersCrushboneExhibitIsRefusedFromThirtyAndNotAtTwentyNine()
    {
        var band = ZoneLevels.Default.BandFor("Crushbone")!;
        Assert.Equal((5, 20), (band.Min, band.Max));

        // 29 - 20 = 9, under the threshold. The row the Founder saw is still there.
        var at29 = RankIn(29, ZoneLevels.Default, "Crushbone");
        Assert.Equal("Crushbone", Assert.Single(at29.Top).Zone);
        Assert.Empty(at29.GearBandRefusals);

        // 30 - 20 = 10, exactly OutgrownBy, and the rule is "or more".
        var at30 = RankIn(30, ZoneLevels.Default, "Crushbone");
        Assert.Empty(at30.Top);
        var refusal = Assert.Single(at30.GearBandRefusals);
        Assert.Equal("Crushbone", refusal.Zone);
        Assert.Equal(5, refusal.Min);
        Assert.Equal(20, refusal.Max);
        Assert.Equal("5-20", refusal.Verbatim);
        Assert.Equal(30, refusal.Level);
        Assert.Equal(GearBandArm.TopUnder, refusal.Arm);
    }

    /// <summary>
    /// **AND THE EXHIBIT THE GATE CANNOT ANSWER, asserted so nobody mistakes this slice for
    /// the whole of acceptance 3.** Rathe Mountains is `13-45`: it spans most characters, so no
    /// level rule refuses it for a 29 and the Founder's complaint about that row is about WHO
    /// drops the item, which is D4's. A slice that quietly widened the gate until the Rathe row
    /// disappeared would be answering the wrong half.
    /// </summary>
    [Fact]
    public void TheRatheRowSurvivesTheGateBecauseALevelRuleCannotRefuseIt()
    {
        var set = RankIn(29, ZoneLevels.Default, "Rathe Mountains");

        Assert.Equal("Rathe Mountains", Assert.Single(set.Top).Zone);
        Assert.Empty(set.GearBandRefusals);
    }

    /// <summary>
    /// **THE OPEN TOP, BOTH WAYS, against the shipped file** (Helm option (a)). Plane of Sky is
    /// `50+`: at 12 the BOTTOM arm refuses it, and at 60 nothing does — because there is no
    /// maximum to be ten levels under, and reading the row's absent top as one would be
    /// inventing the number the ruling refused to invent.
    /// </summary>
    [Theory]
    [InlineData(12, true)]
    [InlineData(45, true)]     // 50 - 45 = 5, exactly GearBandReachAbove
    [InlineData(46, false)]    // 50 - 46 = 4, in reach
    [InlineData(60, false)]    // 10 over the bottom, and there is no top to be under
    [InlineData(99, false)]
    public void AnOpenToppedBandIsOnlyEverRefusedByItsBottom(int level, bool refused)
    {
        var set = RankIn(level, ZoneLevels.Default, "Plane of Sky");

        Assert.Null(ZoneLevels.Default.BandFor("Plane of Sky")!.Max);
        if (refused)
        {
            Assert.Empty(set.Top);
            Assert.Equal(GearBandArm.BottomOver, Assert.Single(set.GearBandRefusals).Arm);
        }
        else
        {
            Assert.Equal("Plane of Sky", Assert.Single(set.Top).Zone);
            Assert.Empty(set.GearBandRefusals);
        }
    }

    /// <summary><see cref="Recommendations.OutgrownBy"/> is an inclusive threshold, pinned at
    /// the boundary in both directions. Off by one here silently changes which zones a player
    /// sees, and no other test would notice.</summary>
    /// <summary><see cref="Recommendations.OutgrownBy"/> is an INCLUSIVE threshold, pinned at
    /// the boundary in both directions over a fixture band of 5–20. Off by one here silently
    /// changes which zones a player sees and no other test would notice.</summary>
    [Theory]
    [InlineData(29, false)]    // 29 - 20 = 9
    [InlineData(30, true)]     // exactly 10
    [InlineData(31, true)]
    [InlineData(20, false)]    // standing in the band's own top
    [InlineData(5, false)]
    public void TheTopArmFiresAtExactlyOutgrownByLevelsUnder(int level, bool refused)
    {
        var set = RankIn(level, Bands(("Crushbone", 5, 20)), "Crushbone");
        Assert.Equal(refused, set.GearBandRefusals.Count == 1);
        Assert.Equal(refused, set.Top.Count == 0);
        if (refused)
            Assert.Equal(GearBandArm.TopUnder, set.GearBandRefusals[0].Arm);
    }

    /// <summary><see cref="Recommendations.GearBandReachAbove"/>, the same way, over a fixture
    /// band of 30–40. Five, not ten — the two arms carry different numbers on purpose and a
    /// single constant used for both would have to be wrong in one direction.</summary>
    [Theory]
    [InlineData(24, true)]     // 30 - 24 = 6
    [InlineData(25, true)]     // exactly 5
    [InlineData(26, false)]    // 4, in reach
    [InlineData(30, false)]
    public void TheBottomArmFiresAtExactlyGearBandReachAboveLevelsOver(int level, bool refused)
    {
        var set = RankIn(level, Bands(("Kaesora", 30, 40)), "Kaesora");
        Assert.Equal(refused, set.GearBandRefusals.Count == 1);
        if (refused)
            Assert.Equal(GearBandArm.BottomOver, set.GearBandRefusals[0].Arm);
    }

    /// <summary>The two constants are what the tests above say they are — read here rather than
    /// hard-coded in eleven assertions, so a veto of either number changes one place and the
    /// rows above re-derive.</summary>
    [Fact]
    public void TheTwoNamedDistancesAreTenAndFive()
    {
        Assert.Equal(10, Recommendations.OutgrownBy);
        Assert.Equal(5, Recommendations.GearBandReachAbove);
    }

    /// <summary>
    /// **IT REFUSES RATHER THAN DEMOTES, AND IT DOES IT OVER THE PLAYER'S OWN EVIDENCE.**
    ///
    /// <para>The deliberate divergence from <see cref="Recommendations.OutgrownWeight"/>, which
    /// halves a zone and keeps it. Crushbone stays refused for a 29 who farmed it at 12 and
    /// looted this very helm there — the drop is real, and the answer to "where should I go
    /// tonight" is still not Crushbone. This is the delivery's most vetoable default, so it has
    /// a test that would have to be deleted to change it quietly.</para>
    /// </summary>
    [Fact]
    public void ARefusedZoneIsRemovedEvenWhereYouHaveSeenTheItemDropThere()
    {
        MobSummary[] pool =
        [
            new("a young orc", 300, 300, 40, 0, 0, [new MobLoot("Bone Helm", 4, 1.0)])
                { Zone = "Crushbone", LevelMin = 8, LevelMax = 12 },
        ];
        // 30 rather than the Founder's 29, for the reason pinned in
        // TheFoundersCrushboneExhibitIsRefusedFromThirtyAndNotAtTwentyNine.
        var inputs = Gear([Worn("Rusty Helm", "HEAD", 4)], OneHelmIn("Crushbone"),
            pool: pool, level: 30, bands: ZoneLevels.Default);

        // The evidence really is there — without the gate this is a Personal row.
        var ungated = Rank(inputs with { Bands = null });
        Assert.True(Assert.Single(ungated.Top).HasPersonalEvidence);

        var set = Rank(inputs);
        Assert.Empty(set.Top);
        Assert.Single(set.GearBandRefusals);
    }

    /// <summary>
    /// **A GATE THAT EMPTIED THE LIST SAYS SO IN ITS OWN VOICE**, and the voice is neither of
    /// the two that already existed. <c>NoCatalogUpgrade</c> would be false — the catalog DOES
    /// have a better helm — and the whole-room empty state says EQBuddy has nothing stored,
    /// which is the opposite of what happened.
    /// </summary>
    [Fact]
    public void RefusingEveryZoneIsItsOwnGapAndNotNoCatalogUpgrade()
    {
        var set = RankIn(30, ZoneLevels.Default, "Crushbone", "Greater Faydark");

        Assert.Empty(set.Top);
        Assert.Equal(2, set.GearBandRefusals.Count);
        Assert.Equal(GoalGapReason.EveryZoneOutsideYourBand, Assert.Single(set.Gaps).Reason);

        // It must cite the bands and must NOT reach for NoCatalogUpgrade's claim, which is that
        // the catalog holds nothing better — the opposite of what happened here.
        var sentence = HelperPresentation.Gap(Assert.Single(set.Gaps));
        Assert.Contains("eqlwiki", sentence, StringComparison.Ordinal);
        Assert.Contains("found upgrades", sentence, StringComparison.Ordinal);
        Assert.DoesNotContain("beats what you are wearing", sentence, StringComparison.Ordinal);
        Assert.NotEqual(
            HelperPresentation.Gap(new GoalGap(HelperGoal.FarmGear, GoalGapReason.NoCatalogUpgrade)),
            sentence);
    }

    /// <summary>A partial refusal keeps its survivors and still says what went — the case the
    /// gap above must NOT fire in, because there are rows to read.</summary>
    [Fact]
    public void APartialRefusalKeepsTheZonesThatSurvivedAndStillReportsTheRest()
    {
        var set = RankIn(30, ZoneLevels.Default, "Crushbone", "Rathe Mountains");

        Assert.Equal("Rathe Mountains", Assert.Single(set.Top).Zone);
        Assert.Equal("Crushbone", Assert.Single(set.GearBandRefusals).Zone);
        Assert.Empty(set.Gaps);
    }

    /// <summary>
    /// A refused zone does not set the yardstick the surviving rows are measured against.
    ///
    /// <para><see cref="Recommendation.Weight"/> is a share of the best zone's upgrade count, so
    /// a camp this character cannot farm must not decide how full every other row's bar looks.
    /// The gate runs before <c>best</c> is taken; this is that ordering, asserted as a
    /// number.</para>
    /// </summary>
    [Fact]
    public void ARefusedZoneDoesNotSetTheWeightYardstick()
    {
        // Crushbone drops two upgrades, Rathe Mountains one. Refused, Crushbone must not make
        // the Rathe row a half-weight answer.
        var catalog = new ItemCatalog([
            Record("Bone Helm", "HEAD", 9, ["Crushbone", "Rathe Mountains"]),
            Record("Orcish Bracer", "ARMS", 8, ["Crushbone"]),
        ]);
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4), Worn("Cloth Sleeves", "ARMS", 2)], catalog,
            level: 30, bands: ZoneLevels.Default));

        var top = Assert.Single(set.Top);
        Assert.Equal("Rathe Mountains", top.Zone);
        Assert.Equal(1.0, top.Weight);
    }

    /// <summary>
    /// **THE THREE WAYS IT STANDS DOWN, each a different silence** (trap 73: an unanswered
    /// question gates nothing). Unknown level is the one the room already has a door for; the
    /// other two are EQBuddy not having read a page.
    /// </summary>
    [Fact]
    public void AnUnknownLevelStandsTheGateDownEntirely()
    {
        var set = RankIn(null, ZoneLevels.Default, "Crushbone");

        Assert.Equal("Crushbone", Assert.Single(set.Top).Zone);
        Assert.Empty(set.GearBandRefusals);
    }

    [Fact]
    public void NoBandTableStandsTheGateDown()
    {
        var set = RankIn(29, null, "Crushbone");

        Assert.Equal("Crushbone", Assert.Single(set.Top).Zone);
        Assert.Empty(set.GearBandRefusals);
    }

    /// <summary>All three ABSENT outcomes gate nothing, and they are named individually because
    /// "the page says something we will not read" is the one somebody might think should
    /// refuse. It must not: a row we declined to parse is not evidence about a level.</summary>
    [Theory]
    [InlineData("Butcherblock Mountains", ZoneLevels.Source.Refused)]
    [InlineData("Freeport", ZoneLevels.Source.NoRow)]
    [InlineData("Plane of Knowledge", ZoneLevels.Source.Unknown)]
    public void AZoneWithNoBandIsNeverRefused(string zone, ZoneLevels.Source expected)
    {
        Assert.Equal(expected, ZoneLevels.Default.Lookup(zone).Source);

        var set = RankIn(29, ZoneLevels.Default, zone);
        Assert.Equal(zone, Assert.Single(set.Top).Zone);
        Assert.Empty(set.GearBandRefusals);
    }

    /// <summary>**A QUEST ROW IS NOT A CAMP AND IS NOT BAND-GATED** (plan P2). The quest names
    /// no zone to look a band up for, and a hand-in is not something a band describes. Asserted
    /// with a refused zone in the same set, so this is the gate running and declining rather
    /// than the gate being off.</summary>
    [Fact]
    public void AQuestRowIsNotBandGated()
    {
        var catalog = new ItemCatalog([
            Record("Bone Helm", "HEAD", 9, ["Crushbone"]),
            Record("Blessed Helm", "HEAD", 12, quests: ["A Blessing"]),
        ]);
        var set = Rank(Gear([Worn("Rusty Helm", "HEAD", 4)], catalog,
            includeQuests: true, level: 30, bands: ZoneLevels.Default));

        var quest = Assert.Single(set.Top);
        Assert.Equal(RecommendationKind.Quest, quest.Kind);
        Assert.Equal("A Blessing", quest.Subject);
        Assert.Equal("Crushbone", Assert.Single(set.GearBandRefusals).Zone);
    }

    /// <summary>**FARM TO SELL STAYS LEVEL-EXEMPT** (plan P2, D7's coin reasoning). It ranks
    /// what the player already looted and names no camp, so there is no band to read — asserted
    /// as identical answers at two levels with the gate's own band table present.</summary>
    [Fact]
    public void FarmToSellIsUnmovedByTheBandGate()
    {
        MobSummary[] pool =
        [
            new("a young orc", 300, 300, 40, 0, 0, [new MobLoot("Orcish Axe", 5, 1.0)])
                { Zone = "Crushbone" },
        ];
        HelperInputs At(int level) => Gear(
            [Worn("Rusty Helm", "HEAD", 4)], new ItemCatalog([Record("Bone Helm", "HEAD", 9)]),
            GearIntent.FarmToSell, pool: pool, level: level, bands: ZoneLevels.Default) with
        {
            Sales = [new SaleRoll("Orcish Axe", 3, 900)],
        };

        static string[] Read(RecommendationSet s) =>
            [.. s.Top.Select(r => r.Subject + "|" + string.Join("¦",
                r.Why.Select(HelperPresentation.Why)))];

        var low = Read(Rank(At(12)));
        Assert.NotEmpty(low);
        Assert.Equal(low, Read(Rank(At(60))));
        Assert.Empty(Rank(At(12)).GearBandRefusals);
    }

    // ---- what the refusal SAYS (trap 50, HOME-006) ---------------------------------------

    /// <summary>
    /// The sentence carries the count, each band, the level and the rule — two numbers and a
    /// source, and nothing that reads as a judgement about the place or the player.
    /// </summary>
    [Fact]
    public void TheRefusalSaysHowManyAndOnWhatRule()
    {
        var set = RankIn(30, ZoneLevels.Default, "Crushbone", "Plane of Sky");
        var said = HelperPresentation.GearBandRefused(set.GearBandRefusals);

        Assert.Contains("2 zones", said, StringComparison.Ordinal);
        Assert.Contains("at your level 30", said, StringComparison.Ordinal);
        Assert.Contains("Crushbone (5–20)", said, StringComparison.Ordinal);
        // The open top reads as "and above" and never as a range with a missing end.
        Assert.Contains("Plane of Sky (50 and above)", said, StringComparison.Ordinal);
        Assert.DoesNotContain("50–", said, StringComparison.Ordinal);
        // Both arms fired here, so both thresholds are named.
        Assert.Contains("tops out 10 or more levels under you", said, StringComparison.Ordinal);
        Assert.Contains("starts 5 or more levels over you", said, StringComparison.Ordinal);
        Assert.Contains("eqlwiki", said, StringComparison.Ordinal);
    }

    /// <summary>Only the arms that FIRED are quoted. A sentence naming a threshold that decided
    /// nothing in this list is a rule the reader cannot check against what they are
    /// seeing.</summary>
    [Fact]
    public void OnlyTheRuleThatFiredIsNamed()
    {
        var said = HelperPresentation.GearBandRefused(
            Rank(Gear([Worn("Rusty Helm", "HEAD", 4)], OneHelmIn("Crushbone"),
                level: 40, bands: ZoneLevels.Default)).GearBandRefusals);

        Assert.Contains("tops out 10 or more levels under you", said, StringComparison.Ordinal);
        Assert.DoesNotContain("starts 5 or more", said, StringComparison.Ordinal);
    }

    /// <summary>The named zones are capped and the cap says so — trap 50 applied to the
    /// sentence that exists because of trap 50.</summary>
    [Fact]
    public void TheRefusalNamesAFewZonesAndCountsTheRest()
    {
        var zones = new[] { "Crushbone", "Greater Faydark", "Najena", "Erud's Crossing" };
        var said = HelperPresentation.GearBandRefused(
            RankIn(60, ZoneLevels.Default, zones).GearBandRefusals);

        Assert.Contains("4 zones", said, StringComparison.Ordinal);
        Assert.Contains($", and {4 - HelperPresentation.GearBandNamed} more",
            said, StringComparison.Ordinal);
    }

    [Fact]
    public void NoRefusalMeansNoSentence() =>
        Assert.Equal("", HelperPresentation.GearBandRefused([]));

    /// <summary>One refused zone reads as one zone. The singular is where a count-driven
    /// sentence usually breaks.</summary>
    [Fact]
    public void OneRefusedZoneReadsAsOne()
    {
        var said = HelperPresentation.GearBandRefused(
            RankIn(30, ZoneLevels.Default, "Crushbone").GearBandRefusals);

        Assert.StartsWith("1 zone EQBuddy has upgrades for is not listed at your level 30", said,
            StringComparison.Ordinal);
        Assert.DoesNotContain("1 zones", said, StringComparison.Ordinal);
    }

    /// <summary>Every shape of band renders as words, including the single level — one producer,
    /// three shapes, and the open one must never read as "5–".</summary>
    [Theory]
    [InlineData(5, 20, "5–20")]
    [InlineData(12, 12, "12")]
    [InlineData(50, null, "50 and above")]
    public void ABandRendersAsWordsInThreeShapes(int min, int? max, string expected) =>
        Assert.Equal(expected, HelperPresentation.BandPhrase(min, max));
}
