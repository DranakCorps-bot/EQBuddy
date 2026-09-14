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

    private static HelperInputs Gear(
        IReadOnlyList<WornItem> worn,
        ItemCatalog? catalog,
        GearIntent intent = GearIntent.UpgradeWorn,
        IReadOnlyList<string>? picks = null,
        bool includeQuests = false,
        IReadOnlyList<MobSummary>? pool = null,
        IReadOnlyList<SessionRow>? sessions = null) =>
        new(ZoneHistory.Fold(sessions ?? [], pool ?? []), pool ?? [], null, [], [], [], [],
            false, [], [], null, ResolvedLevel.Unknown)
        {
            Worn = worn, Items = catalog, GearIntent = intent,
            WornPicks = picks ?? [], IncludeQuests = includeQuests,
        };

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

    /// <summary>The shipped catalog carries no vendor value YET, and the row above is the only
    /// reason that is not a silent hole. Asserted against the real file so the day the weekly
    /// refresh fills it in, this fails and somebody looks at the sentences it turns on rather
    /// than finding out from a player.</summary>
    [Fact]
    public void TheShippedCatalogCarriesNoVendorValueYet()
    {
        var priced = ItemCatalog.Default.All.Count(r => r.MerchantCopper is not null);
        Assert.True(priced == 0,
            $"{priced:N0} shipped item records now carry a MerchantCopper. The promoter's field "
            + "has data behind it — check the survey counts in items-catalog-report.md, then "
            + "update this row and re-read the sentences it turns on.");
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
}
