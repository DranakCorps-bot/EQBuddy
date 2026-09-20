using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE ERA GATE** (DRA-180 D2, Fable plan P1/P2/P3; Founder Desktop smoke 2026-09-17).
///
/// <para>The Founder's Replace list offered a level-29 Kael Drakkel, Icewell Keep and
/// Veeshan's Peak. Every one of those rows passed the band gate CORRECTLY — Kael's published
/// band is <c>30-60+</c>, and 30 − 29 = 1, well inside <c>GearBandReachAbove</c> (5). The
/// giants really are level 30. They are level 30 in Velious, and a level band has no way to
/// say so. This file covers the axis that does.</para>
///
/// <para><b>The order is the thing most of these tests are actually about.</b> Era, then band,
/// then who — all three can remove one row, and whichever runs first owns the sentence the
/// player reads. <c>RecommendationsGearTests</c> owns the band/who pair; this file owns the
/// rule that now runs ahead of both of them.</para>
/// </summary>
public class RecommendationsEraGateTests
{
    // ---- fixtures ----------------------------------------------------------------------

    private static ItemCatalog.Record Record(
        string name, string slot, int ac, string[]? zones = null, string[]? quests = null) =>
        new()
        {
            Name = name, StatsText = $"Slot: {slot}\nAC: {ac}", Slots = [slot], Ac = ac,
            DropZones = zones?.ToList(), Quests = quests?.ToList(),
            DropMobs = zones is not { Length: > 0 }
                ? null
                : zones.Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(z => z, z => new List<string> { $"a {z.ToLowerInvariant()} dweller" },
                        StringComparer.OrdinalIgnoreCase),
        };

    private static WornItem Worn(string name, string slot, int ac) =>
        new(name, name, slot, ItemStatsBlock.Parse([$"Slot: {slot}", $"AC: {ac}"]));

    /// <summary>A fixture era table. Named zones only, so a test says which claim it is about.
    /// A zone with an empty era lands in the NO-TEMPLATE half — the 14-of-118 case.</summary>
    private static ZoneEras Eras(params (string Zone, string Era)[] rows) =>
        new(rows.Where(r => r.Era.Length != 0)
                .ToDictionary(r => r.Zone, r => new ZoneEras.Banner(r.Era, $"{{{{{r.Era} Era}}}}")),
            rows.Where(r => r.Era.Length == 0)
                .ToDictionary(r => r.Zone, _ => ""));

    private static ZoneLevels Bands(params (string Zone, int Min, int? Max)[] rows) =>
        new(rows.ToDictionary(
                r => r.Zone,
                r => new ZoneLevels.Band(
                    r.Min, r.Max,
                    r.Max is { } m ? (m == r.Min ? $"{r.Min}" : $"{r.Min}-{m}") : $"{r.Min}+")),
            new Dictionary<string, string>());

    /// <summary>The default is NO era table and NO world era — the shipped state — so a test
    /// about this gate has to ask for both halves by name, exactly as the band gate's own
    /// fixture makes a test ask for a level and a band.</summary>
    private static HelperInputs Gear(
        IReadOnlyList<WornItem> worn,
        ItemCatalog? catalog,
        ZoneEras? eras = null,
        string world = "",
        int? level = null,
        ZoneLevels? bands = null,
        bool includeQuests = false,
        QuestCatalog? quests = null) =>
        new([], [], null, [], [], [], [], false, [], [], quests,
            level is { } l
                ? new ResolvedLevel(l, LevelSource.Observed, new DateTime(2026, 9, 17, 21, 0, 0))
                : ResolvedLevel.Unknown)
        {
            Worn = worn, Items = catalog, GearIntent = GearIntent.UpgradeWorn,
            Bands = bands, Eras = eras, World = world, IncludeQuests = includeQuests,
        };

    private static RecommendationSet Rank(HelperInputs inputs) =>
        Recommendations.Rank(inputs, [HelperGoal.FarmGear]);

    /// <summary>One worn anchor and one catalog upgrade dropping in the named zones. Every
    /// test here is about which of those zones survives, so the sweep itself is held constant.
    /// </summary>
    private static HelperInputs OneUpgradeIn(
        string[] zones, ZoneEras? eras = null, string world = "",
        int? level = null, ZoneLevels? bands = null) =>
        Gear([Worn("Rusty Helm", "HEAD", 4)],
             new ItemCatalog([Record("Fine Steel Helm", "HEAD", 9, zones)]),
             eras, world, level, bands);

    // ---- P2: the shipped state is ABSENT, and absent changes nothing --------------------

    /// <summary>
    /// **The curated world era ships EMPTY, and that is the whole reason every slice of this
    /// plan can land before anyone has answered anything** (plan P2).
    ///
    /// <para>This is not a formality. The value decides whether real places are refused, and a
    /// guessed one would be trap 73 compressed into a single word — invisible, and wrong in the
    /// direction that removes content the player can actually reach. D5 sets it from named
    /// evidence. Until then this test is what stops a well-meaning edit from shipping a
    /// default.</para>
    /// </summary>
    [Fact]
    public void TheCuratedWorldEraShipsAbsentAndCarriesNoInventedSource()
    {
        Assert.Equal("", WorldEra.Current);
        Assert.Equal("", WorldEra.Source);
        Assert.False(WorldEra.Known);
    }

    /// <summary>
    /// **With no world era the gate stands down WHOLE and product behaviour is unchanged.**
    ///
    /// <para>The zone here is Velious content offered to a level-29 — the Founder's exact
    /// complaint — and it is still drawn, because nothing has told EQBuddy what era the world
    /// is at. That is the correct shipped behaviour for D2: the mechanism lands dark. A test
    /// that only proved the refusal would pass on a build that had quietly defaulted the world
    /// to Classic and started refusing half the catalog.</para>
    /// </summary>
    [Fact]
    public void WithNoWorldEraTheGateStandsDownWholeAndTheRowIsStillDrawn()
    {
        var set = Rank(OneUpgradeIn(
            ["Kael Drakkel"], Eras(("Kael Drakkel", "Velious")), world: ""));

        Assert.Equal("Kael Drakkel", Assert.Single(set.Top).Zone);
        Assert.Empty(set.GearEraRefusals);
    }

    // ---- the LIVENESS fact, asserted before any count is read --------------------------

    /// <summary>
    /// **"THE GATE RAN" IS A DIFFERENT CLAIM FROM "THE GATE REFUSED NOTHING"** (DRA-149 D5
    /// item 2's lesson, copied into the unit half this time; trap 42).
    ///
    /// <para>Zero refusals is the same number on a build where this gate does not exist, one
    /// where it stood down, and one where it ran and found everything reachable. Every other
    /// test in this file reads a count, so every one of them would pass against a gate that was
    /// never wired — this is the assertion that makes the rest of them mean something, and it
    /// is why the fact is a boolean rather than something inferred.</para>
    ///
    /// <para>All three stand-down inputs are driven, because each is a separate way to be dark
    /// and a conjunction can be broken one term at a time.</para>
    /// </summary>
    [Theory]
    // eras, world  -> armed?
    [InlineData(true, "Classic", true)]
    [InlineData(true, "", false)]          // the SHIPPED state
    [InlineData(true, "Kunar", false)]     // a word this repo cannot rank
    [InlineData(false, "Classic", false)]  // no era table
    public void TheGateReportsWhetherItWasArmedBeforeAnyCountIsRead(
        bool withEras, string world, bool armed)
    {
        var set = Rank(OneUpgradeIn(
            ["Somewhere"],
            withEras ? Eras(("Somewhere", "Classic")) : null,
            world: world));

        Assert.Equal(armed, set.EraGateLive);
    }

    /// <summary>
    /// **On every build that ships today the gate is DARK, and the fact says so out loud.**
    ///
    /// <para>This is P2's promise made checkable: the mechanism lands, the words land, the
    /// guards land, and nothing changes for a player until D5 supplies the one curated word.
    /// It reads through the REAL assembly point rather than a fixture, so a slice that quietly
    /// armed the gate by supplying a default somewhere in <c>HelperSources</c> reddens here
    /// rather than on the Founder's screen.</para>
    /// </summary>
    [Fact]
    public void OnTheShippedBuildTheGateIsDarkAndTheLivenessFactSaysSo()
    {
        var shipped = new HelperInputs([], [], null, [], [], [], [], false, [], [], null,
            ResolvedLevel.Unknown)
        {
            Eras = ZoneEras.Default,
            World = WorldEra.Current,
        };

        Assert.False(Recommendations.EraGateArmed(shipped));
        Assert.False(Recommendations.Rank(shipped, [HelperGoal.FarmGear]).EraGateLive);
    }

    // ---- P1: the refusal itself --------------------------------------------------------

    /// <summary>A place eqlwiki dates later than the world is refused, and the refusal carries
    /// both era words and the page's own banner — the <c>GearBandRefusal</c> discipline, which
    /// quotes the wiki's row rather than paraphrasing it.</summary>
    [Fact]
    public void AZoneLaterThanTheWorldIsRefusedAndQuotesBothErasAndThePage()
    {
        var set = Rank(OneUpgradeIn(
            ["Kael Drakkel"], Eras(("Kael Drakkel", "Velious")), world: "Classic"));

        Assert.Empty(set.Top);
        var refusal = Assert.Single(set.GearEraRefusals);
        Assert.Equal("Kael Drakkel", refusal.Subject);
        Assert.Equal("Velious", refusal.Era);
        Assert.Equal("{{Velious Era}}", refusal.Verbatim);
        Assert.Equal("Classic", refusal.World);
        Assert.Equal(RecommendationKind.Zone, refusal.Kind);
    }

    /// <summary>A place at or before the world's era is kept. Both arms matter: the same era is
    /// reachable (it is the era we are IN), and an earlier one obviously is. An off-by-one here
    /// would silently delete the current expansion's content from every list.</summary>
    [Theory]
    [InlineData("Classic")]
    [InlineData("Kunark")]
    public void AZoneAtOrBeforeTheWorldsEraIsKept(string zoneEra)
    {
        var set = Rank(OneUpgradeIn(
            ["Somewhere"], Eras(("Somewhere", zoneEra)), world: "Kunark"));

        Assert.Equal("Somewhere", Assert.Single(set.Top).Zone);
        Assert.Empty(set.GearEraRefusals);
    }

    // ---- P1: the ORDER, which is the decision ------------------------------------------

    /// <summary>
    /// **ERA BEFORE BAND, and the refusal a player reads is the era one.**
    ///
    /// <para>This zone is refusable by BOTH rules — Velious content whose band is far above a
    /// level-12. Only one sentence can be the explanation, and the era is the complete one: a
    /// band refusal would tell this player to come back at level 30, which is advice about a
    /// place that does not exist yet. The band list is asserted EMPTY, because "both fired" is
    /// the outcome that would let a later refactor reorder them without a test noticing.</para>
    /// </summary>
    [Fact]
    public void AZoneRefusableByBothGatesIsRefusedByTheEraGateAndNotTheBandGate()
    {
        var set = Rank(OneUpgradeIn(
            ["Kael Drakkel"], Eras(("Kael Drakkel", "Velious")), world: "Classic",
            level: 12, bands: Bands(("Kael Drakkel", 30, 60))));

        Assert.Empty(set.Top);
        Assert.Equal("Kael Drakkel", Assert.Single(set.GearEraRefusals).Subject);
        Assert.Empty(set.GearBandRefusals);
    }

    /// <summary>The mirror, and the reason the one above is not vacuous: where the era ALLOWS a
    /// zone, the band gate still gets to refuse it and still quotes its own numbers. A gate
    /// that ran first and swallowed everything would pass the test above and break this
    /// one.</summary>
    [Fact]
    public void AZoneTheEraAllowsIsStillRefusedByTheBandGateInItsOwnWords()
    {
        var set = Rank(OneUpgradeIn(
            ["Crushbone"], Eras(("Crushbone", "Classic")), world: "Classic",
            level: 30, bands: Bands(("Crushbone", 5, 20))));

        Assert.Empty(set.Top);
        Assert.Empty(set.GearEraRefusals);
        var band = Assert.Single(set.GearBandRefusals);
        Assert.Equal("Crushbone", band.Zone);
        Assert.Equal(GearBandArm.TopUnder, band.Arm);
    }

    // ---- P1: per-arm stand-down (trap 73) ----------------------------------------------

    /// <summary>
    /// A zone the table does not date is not refused — and the BAND gate still runs on it.
    ///
    /// <para>Per-arm stand-down is the whole design. 14 of the 118 committed pages carry no era
    /// banner, and three of them are the Paineel-adjacent set that makes "absent means Classic"
    /// an invented fact. An unanswered question gates nothing; it must not take a working rule
    /// down with it either.</para>
    /// </summary>
    [Fact]
    public void AZoneTheTableDoesNotDateIsNotEraRefusedAndTheBandGateStillRuns()
    {
        var set = Rank(OneUpgradeIn(
            ["Stonebrunt Mountains"], Eras(("Stonebrunt Mountains", "")), world: "Classic",
            level: 30, bands: Bands(("Stonebrunt Mountains", 5, 20))));

        Assert.Empty(set.GearEraRefusals);
        Assert.Equal("Stonebrunt Mountains", Assert.Single(set.GearBandRefusals).Zone);
    }

    /// <summary>A zone the table has never heard of is the fourth outcome and behaves the same
    /// way: unknown is not a date, and it refuses nothing.</summary>
    [Fact]
    public void AZoneTheTableHasNeverHeardOfIsNotRefused()
    {
        var set = Rank(OneUpgradeIn(
            ["Nowhere At All"], Eras(("Kael Drakkel", "Velious")), world: "Classic"));

        Assert.Equal("Nowhere At All", Assert.Single(set.Top).Zone);
        Assert.Empty(set.GearEraRefusals);
    }

    /// <summary>A world era this repo cannot rank stands the arm down rather than guessing.
    /// The alternative is refusing every dated zone in the game because one curated word was
    /// misspelled — a failure whose only symptom is content quietly disappearing.</summary>
    [Fact]
    public void AWorldEraThisRepoCannotRankStandsTheArmDownRatherThanRefusingEverything()
    {
        var set = Rank(OneUpgradeIn(
            ["Kael Drakkel"], Eras(("Kael Drakkel", "Velious")), world: "Kunar"));

        Assert.Equal("Kael Drakkel", Assert.Single(set.Top).Zone);
        Assert.Empty(set.GearEraRefusals);
    }

    /// <summary>No era table at all stands the era arm down and leaves the band gate running —
    /// the <see cref="HelperInputs.Bands"/> null contract, applied to its new neighbour.</summary>
    [Fact]
    public void NoEraTableStandsTheEraArmDownAndLeavesTheBandGateRunning()
    {
        var set = Rank(OneUpgradeIn(
            ["Crushbone"], eras: null, world: "Classic",
            level: 30, bands: Bands(("Crushbone", 5, 20))));

        Assert.Empty(set.GearEraRefusals);
        Assert.Single(set.GearBandRefusals);
    }

    // ---- P3: a gate that emptied the list says so in its own voice ---------------------

    /// <summary>
    /// **When the era gate removes everything, the gap is its OWN reason — not
    /// <c>NoCatalogUpgrade</c>, which would be false.**
    ///
    /// <para>This is the sentence the Founder's screen owed him. The catalog DOES carry
    /// something better than what he wears; every place it drops is content the world has not
    /// opened. "Nothing better exists" is a lie, and silence is the FAIL that filed this
    /// card.</para>
    /// </summary>
    [Fact]
    public void WhenTheEraGateEmptiesTheListTheGapSaysSoRatherThanNoCatalogUpgrade()
    {
        var set = Rank(OneUpgradeIn(
            ["Kael Drakkel"], Eras(("Kael Drakkel", "Velious")), world: "Classic"));

        var gap = Assert.Single(set.Gaps);
        Assert.Equal(HelperGoal.FarmGear, gap.Goal);
        Assert.Equal(GoalGapReason.EverythingIsLaterThanTheWorld, gap.Reason);
    }

    /// <summary>The era reason is asked BEFORE the band reason, matching the order the gates
    /// ran in. Both fired here — two zones, one refused by each — and the sentence names the
    /// more complete explanation.</summary>
    [Fact]
    public void TheEraGapOutranksTheBandGapWhenBothEmptiedTheList()
    {
        var set = Rank(OneUpgradeIn(
            ["Kael Drakkel", "Crushbone"],
            Eras(("Kael Drakkel", "Velious"), ("Crushbone", "Classic")), world: "Classic",
            level: 30, bands: Bands(("Crushbone", 5, 20))));

        Assert.Empty(set.Top);
        Assert.Single(set.GearEraRefusals);
        Assert.Single(set.GearBandRefusals);
        Assert.Equal(GoalGapReason.EverythingIsLaterThanTheWorld,
            Assert.Single(set.Gaps).Reason);
    }

    // ---- P1: quests are band-EXEMPT and era-GATED --------------------------------------
    //
    // **Every quest entry below names a GIVER since DRA-219, and that is not decoration.**
    // `Recommendations.QuestSourceRule` now withholds a quest offer whose catalog entry answers
    // none of who / where / when / how — a row that can only print a title is the Rathe defect
    // wearing quest clothes. A name-and-era entry is exactly that state, so without a giver
    // these fixtures would be testing the era gate through rows the rule beside it removes, and
    // the two negatives below would go green for the wrong reason.

    /// <summary>
    /// **A quest later than the world is refused, even though quest rows are exempt from the
    /// band gate.**
    ///
    /// <para>The band exemption is sound — the quest IS the path, and the levels of whatever
    /// guards it are not the question. Era is a different claim entirely: a quest in content
    /// the world has not opened cannot be started, so offering it as a way to gear up is the
    /// Kael row's lie in quest clothes.</para>
    /// </summary>
    [Fact]
    public void AQuestLaterThanTheWorldIsRefusedEvenThoughQuestsAreBandExempt()
    {
        var catalog = new QuestCatalog
        {
            Quests = [new QuestEntry
            {
                Name = "Paladin Epic Quest", Era = "Epics", QuestGiver = "a guildmaster",
            }],
        };
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Fine Steel Helm", "HEAD", 9, quests: ["Paladin Epic Quest"])]),
            eras: Eras(), world: "Classic", includeQuests: true, quests: catalog));

        Assert.Empty(set.Top);
        var refusal = Assert.Single(set.GearEraRefusals);
        Assert.Equal("Paladin Epic Quest", refusal.Subject);
        Assert.Equal("Epics", refusal.Era);
        Assert.Equal(RecommendationKind.Quest, refusal.Kind);
    }

    /// <summary>The negative that keeps the one above honest: a quest the world HAS reached is
    /// still offered. Without this, a bug that refused every quest row would pass.</summary>
    [Fact]
    public void AQuestTheWorldHasReachedIsStillOffered()
    {
        var catalog = new QuestCatalog
        {
            Quests = [new QuestEntry
            {
                Name = "A Humble Errand", Era = "Classic", QuestGiver = "a herald",
            }],
        };
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Fine Steel Helm", "HEAD", 9, quests: ["A Humble Errand"])]),
            eras: Eras(), world: "Classic", includeQuests: true, quests: catalog));

        Assert.Equal("A Humble Errand", Assert.Single(set.Top).Subject);
        Assert.Empty(set.GearEraRefusals);
    }

    /// <summary>A quest the catalog does not date refuses nothing — the quest side's half of
    /// the same trap-73 rule the zone side follows.</summary>
    [Fact]
    public void AQuestTheCatalogDoesNotDateIsNotRefused()
    {
        var catalog = new QuestCatalog
        {
            Quests = [new QuestEntry
            {
                Name = "An Undated Errand", Era = "", QuestGiver = "a herald",
            }],
        };
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Fine Steel Helm", "HEAD", 9, quests: ["An Undated Errand"])]),
            eras: Eras(), world: "Classic", includeQuests: true, quests: catalog));

        Assert.Equal("An Undated Errand", Assert.Single(set.Top).Subject);
        Assert.Empty(set.GearEraRefusals);
    }

    // ---- the Founder's own exhibit, against the SHIPPED tables -------------------------

    /// <summary>
    /// **THE FOUNDER'S KAEL DRAKKEL ROW, measured against the tables this repo actually
    /// ships** — the band table D1 of DRA-84 committed and the era table D1 of this card did.
    ///
    /// <para>Every claim in the plan's §0 is re-derived here rather than trusted: Kael's band
    /// really is open-topped from 30, a level-29 really does pass the band gate on it (30 − 29
    /// = 1, under <c>GearBandReachAbove</c>), and the era table really does date it to Velious.
    /// Those three facts together are the whole defect, and a fixture would have proved none of
    /// them. If a wiki refresh moves any of the three, this reddens on the one that
    /// moved.</para>
    /// </summary>
    [Fact]
    public void TheFoundersKaelDrakkelRowPassesTheBandGateAtTwentyNineAndTheEraGateRefusesIt()
    {
        var band = ZoneLevels.Default.BandFor("Kael Drakkel")!;
        Assert.Equal(30, band.Min);
        Assert.Null(band.Max);
        // The band gate's own judgement, asked directly: nothing is wrong with this row on
        // levels, which is exactly why the Founder saw it.
        Assert.Null(Recommendations.ArmFor(band, 29));

        var answer = ZoneEras.Default.Lookup("Kael Drakkel");
        Assert.Equal(ZoneEras.Source.Dated, answer.Source);
        Assert.Equal("Velious", answer.Era);

        var set = Rank(OneUpgradeIn(
            ["Kael Drakkel"], ZoneEras.Default, world: "Classic",
            level: 29, bands: ZoneLevels.Default));

        Assert.Empty(set.Top);
        Assert.Empty(set.GearBandRefusals);
        Assert.Equal("Velious", Assert.Single(set.GearEraRefusals).Era);
    }

    /// <summary>
    /// Lower Guk is one of the 14 committed pages with NO era banner, and it is Classic content
    /// a level-29 should absolutely be sent to. Against the SHIPPED tables it survives a
    /// Classic world — which is the practical cost of refusing to default an absent banner, and
    /// the proof that refusing to default it was the right call.
    /// </summary>
    [Fact]
    public void AnUndatedShippedZoneSurvivesTheGateAgainstTheRealTables()
    {
        Assert.Equal(ZoneEras.Source.NoTemplate, ZoneEras.Default.Lookup("Lower Guk").Source);

        var set = Rank(OneUpgradeIn(
            ["Lower Guk"], ZoneEras.Default, world: "Classic",
            level: 29, bands: ZoneLevels.Default));

        Assert.Equal("Lower Guk", Assert.Single(set.Top).Zone);
        Assert.Empty(set.GearEraRefusals);
    }

    // ---- the shared spine: Farm Materials rides the same gate, counted apart -----------

    /// <summary>
    /// The materials engine era-gates on the SAME rule with its OWN count (plan P1, DRA-149
    /// D3's discipline). A gem whose only camp is Velious is unreachable in a Classic world for
    /// exactly the reason a helm there is — so the rule is shared, and the two counts are not,
    /// because one merged number would explain neither list.
    /// </summary>
    [Fact]
    public void TheMaterialsEngineRidesTheSameEraGateWithItsOwnSeparateCount()
    {
        var record = new ItemCatalog.Record
        {
            Name = "Velium Ore",
            StatsText = "Lore Item",
            DropZones = ["Kael Drakkel"],
            DropMobs = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Kael Drakkel"] = ["a coldain miner"],
            },
            Recipes = [Tradeskills.For(Tradeskill.Blacksmithing).Name,
                       "Velium Blade (Trivial: 188)"],
        };

        var inputs = new HelperInputs([], [], null, [], [], [], [], false, [], [], null,
            ResolvedLevel.Unknown)
        {
            Items = new ItemCatalog([record]),
            Eras = Eras(("Kael Drakkel", "Velious")),
            World = "Classic",
            Professions = [Tradeskill.Blacksmithing],
        };

        var set = Recommendations.Rank(inputs, [HelperGoal.FarmMaterials]);

        Assert.Empty(set.Top);
        var refusal = Assert.Single(set.MaterialEraRefusals);
        Assert.Equal("Kael Drakkel", refusal.Subject);
        Assert.Equal("Velious", refusal.Era);
        // The gear list is a different list and stays its own number (trap 50).
        Assert.Empty(set.GearEraRefusals);
        Assert.Equal(GoalGapReason.EveryMaterialZoneLaterThanTheWorld,
            Assert.Single(set.Gaps).Reason);
    }
}
