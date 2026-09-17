using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE FARM MATERIALS ENGINE** (DRA-149 D3, Fable plan P4; the Founder's FAIL item 3a).
///
/// <para><c>TradeskillMaterialsTests</c> covers the READER — which records are ingredients and
/// which zone strings are places. This file covers what the ENGINE does with them: the zone is
/// the join key, the weight is a count of the ingredients one place feeds, your own observed
/// drops outrank the page, the SAME band gate and who rule run in the SAME order as Farm Gear's,
/// and each of the three ways to have nothing to say is a different sentence.</para>
/// </summary>
public class RecommendationsMaterialsTests
{
    // ---- fixtures ----------------------------------------------------------------------

    /// <summary>One ingredient record. <b>Every drop zone gets a creature unless the test asks
    /// for the opposite</b> — the gear file's own rule and its reason: 98.7% of the shipped
    /// catalog's (item, zone) pairs name one, so a silent fixture would make the who rule the
    /// thing every other test in this file was measuring.</summary>
    private static ItemCatalog.Record Material(
        string name, Tradeskill skill, string[] zones, bool anonymous = false,
        string recipe = "Bloodstone Earring (Trivial: 102)") =>
        new()
        {
            Name = name,
            StatsText = "Lore Item",
            Recipes = [Tradeskills.For(skill).Name, recipe],
            DropZones = [.. zones],
            DropMobs = anonymous
                ? null
                : zones.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(
                    z => z, z => new List<string> { $"a {z.ToLowerInvariant()} dweller" },
                    StringComparer.OrdinalIgnoreCase),
        };

    /// <summary>The default is <see cref="ResolvedLevel.Unknown"/> with no bands, which stands
    /// the band gate down — the gear file's own default, so a test about the gate asks for both
    /// halves by name (trap 73: an unanswered question gates nothing).</summary>
    private static HelperInputs Inputs(
        ItemCatalog? catalog,
        IReadOnlyList<Tradeskill>? professions = null,
        IReadOnlyList<MobSummary>? pool = null,
        IReadOnlyList<SessionRow>? sessions = null,
        int? level = null,
        ZoneLevels? bands = null) =>
        new(ZoneHistory.Fold(sessions ?? [], pool ?? []), pool ?? [], null, [], [], [], [],
            false, [], [], null,
            level is { } l
                ? new ResolvedLevel(l, LevelSource.Observed, new DateTime(2026, 9, 12, 20, 0, 0))
                : ResolvedLevel.Unknown)
        {
            Items = catalog,
            Professions = professions ?? [],
            Bands = bands,
        };

    private static RecommendationSet Rank(HelperInputs inputs) =>
        Recommendations.Rank(inputs, [HelperGoal.FarmMaterials]);

    private static string Why(Recommendation r) =>
        string.Join(" ", r.Why.Select(HelperPresentation.Why));

    // ---- the happy path -----------------------------------------------------------------

    /// <summary>
    /// A zone that drops something one of your professions needs is a row, and the row names
    /// the ingredient, the profession and what to kill — the three halves of the Founder's own
    /// ask ("zones/creatures where gems drop"), with the estimate label because the claim came
    /// out of a file EQBuddy ships.
    /// </summary>
    [Fact]
    public void AZoneThatDropsAnIngredientIsAnAnswerAndSaysWhatToKill()
    {
        var set = Rank(Inputs(new ItemCatalog([
            Material("Bloodstone", Tradeskill.Jewelcrafting, ["Lower Guk"])])));

        var row = Assert.Single(set.Top);
        Assert.Equal("Lower Guk", row.Subject);
        Assert.Equal(RecommendationKind.Zone, row.Kind);
        Assert.Equal([HelperGoal.FarmMaterials], row.Goals);

        var why = Why(row);
        Assert.Contains("Bloodstone", why, StringComparison.Ordinal);
        Assert.Contains("Jewelcrafting", why, StringComparison.Ordinal);
        Assert.Contains("a lower guk dweller", why, StringComparison.Ordinal);
        Assert.Contains("Bloodstone Earring (Trivial: 102)", why, StringComparison.Ordinal);

        // Every line of it is something EQBuddy READ, so HOME-004's label arrives by
        // construction rather than by a rule somebody has to remember.
        Assert.All(row.Why.OfType<TradeskillMaterialFact>(),
            f => Assert.Equal(Evidence.Catalog, f.Evidence));
    }

    /// <summary>
    /// **THE WEIGHT IS HOW MANY OF YOUR INGREDIENTS ONE PLACE FEEDS** — the gear engine's own
    /// yardstick, one noun over. A zone dropping three gems outranks one dropping a single ore,
    /// and both are drawn, because the question is where to camp tonight.
    /// </summary>
    [Fact]
    public void AZoneThatFeedsMoreOfYourProfessionsOutranksOneThatFeedsFewer()
    {
        var set = Rank(Inputs(new ItemCatalog([
            Material("Bloodstone", Tradeskill.Jewelcrafting, ["Lower Guk", "Befallen"]),
            Material("Carnelian", Tradeskill.Jewelcrafting, ["Lower Guk"]),
            Material("Amber", Tradeskill.Jewelcrafting, ["Lower Guk"]),
        ])));

        Assert.Equal("Lower Guk", set.Top[0].Subject);
        Assert.Equal("Befallen", set.Top[1].Subject);
        Assert.True(set.Top[0].Weight > set.Top[1].Weight);
    }

    /// <summary>
    /// **YOUR OWN KILLS OUTRANK THE PAGE, AND THE TWO ARE NEVER BOTH DRAWN** (HOME-003, trap 4).
    ///
    /// <para><c>WhoFor</c>'s precedence is shared code with Farm Gear rather than a second copy
    /// of the rule, and this is the row that proves materials go through it: the page names a
    /// dweller, the player has looted the gem from a froglok shaman, and only the measured
    /// sentence is drawn.</para>
    /// </summary>
    [Fact]
    public void WhatYouHaveSeenDropOutranksWhatThePageSays()
    {
        MobSummary[] pool =
        [
            new("a froglok shaman", 40, 40, 30, 0, 0, [new MobLoot("Bloodstone", 4, 0)])
            {
                Zone = "Lower Guk",
            },
        ];

        var row = Assert.Single(Rank(Inputs(
            new ItemCatalog([Material("Bloodstone", Tradeskill.Jewelcrafting, ["Lower Guk"])]),
            pool: pool)).Top);

        var why = Why(row);
        Assert.Contains("a froglok shaman", why, StringComparison.Ordinal);
        Assert.DoesNotContain("a lower guk dweller", why, StringComparison.Ordinal);
        Assert.Single(row.Why.OfType<GearDropSeenFact>());
    }

    /// <summary>The pick NARROWS the engine rather than the room — a player raising only
    /// Jewelcrafting is not offered somewhere that drops nothing but tailoring pelts.</summary>
    [Fact]
    public void ThePickNarrowsWhichZonesAreOffered()
    {
        var catalog = new ItemCatalog([
            Material("Bloodstone", Tradeskill.Jewelcrafting, ["Lower Guk"]),
            Material("Low Quality Bear Skin", Tradeskill.Tailoring, ["Everfrost Peaks"]),
        ]);

        Assert.Equal(2, Rank(Inputs(catalog)).Top.Count);

        var narrowed = Assert.Single(
            Rank(Inputs(catalog, professions: [Tradeskill.Jewelcrafting])).Top);
        Assert.Equal("Lower Guk", narrowed.Subject);
    }

    // ---- the band gate: the SAME rule, the SAME constants, its OWN count ------------------

    /// <summary>
    /// **THE BAND GATE REFUSES A MATERIALS CAMP FOR THE REASON IT REFUSES A GEAR ONE** — a camp
    /// is a camp (DRA-84 D2's rule, DRA-149 D3's second reader).
    ///
    /// <para>This is what makes <c>LevelUseFor(FarmMaterials)</c> a real <c>Consumes</c> rather
    /// than a comment. The refusal is REPORTED with its band, its arm and the level it compared
    /// against — trap 50: a rule that removed a row says so, with the numbers it used.</para>
    /// </summary>
    [Fact]
    public void AZoneOutsideYourBandIsRefusedAndTheRefusalCarriesItsNumbers()
    {
        var bands = new ZoneLevels(
            new Dictionary<string, ZoneLevels.Band> { ["Lower Guk"] = new(8, 12, "8-12") },
            new Dictionary<string, string>());
        var catalog = new ItemCatalog([
            Material("Bloodstone", Tradeskill.Jewelcrafting, ["Lower Guk"])]);

        // In reach at 12; forty-eight levels over the top at 60.
        Assert.Single(Rank(Inputs(catalog, level: 12, bands: bands)).Top);

        var set = Rank(Inputs(catalog, level: 60, bands: bands));
        Assert.Empty(set.Top);

        var refusal = Assert.Single(set.MaterialBandRefusals);
        Assert.Equal("Lower Guk", refusal.Zone);
        Assert.Equal("8-12", refusal.Verbatim);
        Assert.Equal(60, refusal.Level);
        Assert.Equal(GearBandArm.TopUnder, refusal.Arm);

        // And the gate that emptied the list says so in its OWN voice — silence here would draw
        // the room's whole-room empty state, which claims EQBuddy has nothing stored.
        Assert.Contains(set.Gaps, g =>
            g.Goal == HelperGoal.FarmMaterials
            && g.Reason == GoalGapReason.EveryMaterialZoneOutsideYourBand);
    }

    /// <summary>
    /// **THE TWO ENGINES' REFUSALS ARE NEVER SUMMED**, which is the whole reason the set carries
    /// two lists. They are the same rule over the same catalog, so a merged count would be the
    /// one number that can explain neither list — and a player reading about "zones EQBuddy has
    /// upgrades for" that had silently also counted where their gems drop could act on
    /// neither.
    /// </summary>
    [Fact]
    public void TheMaterialRefusalsAreCountedApartFromTheGearOnes()
    {
        var bands = new ZoneLevels(
            new Dictionary<string, ZoneLevels.Band> { ["Lower Guk"] = new(8, 12, "8-12") },
            new Dictionary<string, string>());
        var inputs = Inputs(
            new ItemCatalog([Material("Bloodstone", Tradeskill.Jewelcrafting, ["Lower Guk"])]),
            level: 60, bands: bands);

        var set = Recommendations.Rank(inputs, [HelperGoal.FarmMaterials]);

        Assert.Single(set.MaterialBandRefusals);
        Assert.Empty(set.GearBandRefusals);
    }

    /// <summary>Unknown level, or no band table, or no band for the zone: the gate stands down
    /// WHOLE and the row is drawn (trap 73 — an unanswered question gates nothing). Three
    /// silences and none of them may become a guess.</summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData(60, false)]
    [InlineData(null, true)]
    public void TheGateStandsDownWhereItCannotAnswer(int? level, bool withBands)
    {
        var bands = withBands
            ? new ZoneLevels(
                new Dictionary<string, ZoneLevels.Band> { ["Somewhere Else"] = new(8, 12, "8-12") },
                new Dictionary<string, string>())
            : null;

        var set = Rank(Inputs(
            new ItemCatalog([Material("Bloodstone", Tradeskill.Jewelcrafting, ["Lower Guk"])]),
            level: level, bands: bands));

        Assert.Single(set.Top);
        Assert.Empty(set.MaterialBandRefusals);
    }

    // ---- the who rule: after the gate, and for the gate's reason -------------------------

    /// <summary>
    /// **A CAMP THAT CANNOT SAY WHAT DROPS THERE IS NOT A CAMP** (DRA-84 D4's rule, this
    /// engine's second reader). It is WITHHELD with its own count rather than drawn with a
    /// silent line, and the room says so (trap 50).
    /// </summary>
    [Fact]
    public void AnIngredientNothingCanNameADropperForIsWithheldAndTheSetSaysSo()
    {
        var set = Rank(Inputs(new ItemCatalog([
            Material("Bloodstone", Tradeskill.Jewelcrafting, ["Lower Guk"], anonymous: true)])));

        Assert.Empty(set.Top);
        Assert.Equal(1, set.MaterialWhoWithheld);
        Assert.Contains(set.Gaps, g =>
            g.Goal == HelperGoal.FarmMaterials
            && g.Reason == GoalGapReason.NoMaterialNamesACreature);
    }

    /// <summary>
    /// **AND THE ORDER OF THE TWO RULES IS THE DECISION** (DRA-84 D4's own clause, kept).
    ///
    /// <para>Both rules can remove the same row, and which sentence the player gets depends on
    /// which ran first. The band refusal quotes eqlwiki's numbers and this character's level;
    /// the who rule can only say a page was silent. So a zone that is BOTH outside the band and
    /// anonymous must come back as a band refusal — running the who rule first would swallow the
    /// louder, more actionable sentence.</para>
    /// </summary>
    [Fact]
    public void AZoneThatFailsBothRulesIsReportedAsABandRefusal()
    {
        var bands = new ZoneLevels(
            new Dictionary<string, ZoneLevels.Band> { ["Lower Guk"] = new(8, 12, "8-12") },
            new Dictionary<string, string>());

        var set = Rank(Inputs(
            new ItemCatalog([
                Material("Bloodstone", Tradeskill.Jewelcrafting, ["Lower Guk"], anonymous: true)]),
            level: 60, bands: bands));

        Assert.Single(set.MaterialBandRefusals);
        Assert.Equal(0, set.MaterialWhoWithheld);
        Assert.Contains(set.Gaps, g => g.Reason == GoalGapReason.EveryMaterialZoneOutsideYourBand);
    }

    /// <summary>The who rule PRUNES rather than filtering at the door, so the bucket count stays
    /// honest — it is this engine's weight AND its yardstick, and an offer that will never be
    /// drawn must not inflate the zone it cannot be drawn under.</summary>
    [Fact]
    public void AWithheldOfferDoesNotInflateTheZoneItCannotBeDrawnUnder()
    {
        var set = Rank(Inputs(new ItemCatalog([
            Material("Bloodstone", Tradeskill.Jewelcrafting, ["Lower Guk"]),
            Material("Carnelian", Tradeskill.Jewelcrafting, ["Lower Guk"], anonymous: true),
            Material("Amber", Tradeskill.Jewelcrafting, ["Befallen"]),
        ])));

        // Lower Guk kept ONE offer, not two, so it does not outrank Befallen on a row nobody
        // will ever see.
        Assert.Equal(1, set.MaterialWhoWithheld);
        Assert.Equal(2, set.Top.Count);
        Assert.All(set.Top, r => Assert.Equal(1.0, r.Weight));
    }

    // ---- the three silences --------------------------------------------------------------

    /// <summary>
    /// **FLETCHING'S ZERO IS ITS OWN SENTENCE, AND THE SUBJECT IS THE PAGES.** "Nothing to farm
    /// for Fletching" would be a claim about the game and it is false — a fletcher farms plenty.
    /// What happened is that no page says where any of it drops.
    /// </summary>
    [Fact]
    public void AProfessionWhoseIngredientsNeverDropDrawsItsOwnSentence()
    {
        var set = Rank(Inputs(
            new ItemCatalog([
                new ItemCatalog.Record
                {
                    Name = "Wooden Shaft", StatsText = "",
                    Recipes = ["Fletching", "Ash Bow (Trivial: 21)"],
                },
            ]),
            professions: [Tradeskill.Fletching]));

        Assert.Empty(set.Top);
        var gap = Assert.Single(set.Gaps);
        Assert.Equal(GoalGapReason.NoMaterialDrops, gap.Reason);

        var said = HelperPresentation.Gap(gap);
        Assert.Contains("bought, foraged or made", said, StringComparison.Ordinal);
        // The SUBJECT is EQBuddy's own pages — never the game.
        Assert.DoesNotContain("nothing to farm", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>No catalog at all is the same silence rather than a throw — a fixture without
    /// one is a test, not an error.</summary>
    [Fact]
    public void NoCatalogAnswersTheSameSilence()
    {
        var set = Rank(Inputs(null));

        Assert.Empty(set.Top);
        Assert.Equal(GoalGapReason.NoMaterialDrops, Assert.Single(set.Gaps).Reason);
    }

    /// <summary>The three silences are three DIFFERENT sentences — a merged one could tell a
    /// player which thing to do about none of them.</summary>
    [Fact]
    public void TheThreeSilencesAreThreeDistinctSentences()
    {
        var said = new[]
            {
                GoalGapReason.NoMaterialDrops,
                GoalGapReason.EveryMaterialZoneOutsideYourBand,
                GoalGapReason.NoMaterialNamesACreature,
            }
            .Select(r => HelperPresentation.Gap(new GoalGap(HelperGoal.FarmMaterials, r)))
            .ToList();

        Assert.All(said, Assert.NotEmpty);
        Assert.Equal(3, said.Distinct(StringComparer.Ordinal).Count());
    }

    // ---- the Founder's own example, against the SHIPPED catalog --------------------------

    /// <summary>
    /// **THE FOUNDER'S FAIL ITEM 3, ANSWERED FROM THE SHIPPED FILE AND NOTHING FETCHED.**
    ///
    /// <para>*"Tradeskills (example jewelcrafting): should recommend zones/creatures where gems
    /// drop more commonly."* This runs the real engine over the real catalog with Jewelcrafting
    /// picked and asserts the shape of what comes back: real zones, and a creature named on
    /// every drawn line. The fixtures above prove the rules; this proves they answer on the data
    /// that actually ships.</para>
    /// </summary>
    [Fact]
    public void TheFoundersJewelcraftingAskIsAnsweredByTheShippedCatalog()
    {
        var set = Rank(Inputs(ItemCatalog.Default, professions: [Tradeskill.Jewelcrafting]));

        Assert.NotEmpty(set.Top);
        Assert.Empty(set.Gaps);

        foreach (var row in set.Top)
        {
            Assert.Equal(RecommendationKind.Zone, row.Kind);
            // A LITERAL rather than IsPlace's own answer: asserting the rule with the rule is
            // true of an implementation that refuses nothing (trap 78).
            Assert.DoesNotContain(row.Subject.Trim(),
                new[] { "Various Zones", "Unknown", "D3+ Zones", "}}", "/", "" },
                StringComparer.OrdinalIgnoreCase);

            var facts = row.Why.OfType<TradeskillMaterialFact>().ToList();
            Assert.NotEmpty(facts);
            // Every drawn line can say what to kill — the half the Founder failed the gear rows
            // for, and the reason the who rule runs here too.
            Assert.All(facts, f => Assert.NotEmpty(f.Who));
            Assert.All(facts, f => Assert.Equal(Tradeskill.Jewelcrafting, f.Skill));
        }
    }

    /// <summary>
    /// **AND A LEVEL-30 CHARACTER GETS A NARROWER LIST THAN AN UNKNOWN-LEVEL ONE** — the band
    /// gate reaching the shipped data, which is what <c>Consumes</c> claims. Gems drop from
    /// Temple of Veeshan to Blackburrow, so some of those camps are outside any given level and
    /// the refusals are REPORTED rather than silent.
    /// </summary>
    [Fact]
    public void TheBandGateReachesTheShippedGemCamps()
    {
        var unknown = Rank(Inputs(ItemCatalog.Default, professions: [Tradeskill.Jewelcrafting]));
        var thirty = Rank(Inputs(ItemCatalog.Default, professions: [Tradeskill.Jewelcrafting],
            level: 30, bands: ZoneLevels.Default));

        Assert.Empty(unknown.MaterialBandRefusals);
        Assert.NotEmpty(thirty.MaterialBandRefusals);
        Assert.All(thirty.MaterialBandRefusals, r => Assert.Equal(30, r.Level));

        // And the caption quotes them, in the materials voice rather than the gear one.
        var said = HelperPresentation.BandRefused(
            thirty.MaterialBandRefusals, HelperPresentation.BandRefusedMaterials);
        Assert.Contains("materials", said, StringComparison.Ordinal);
        Assert.DoesNotContain("upgrades", said, StringComparison.Ordinal);
        Assert.Contains("your level 30", said, StringComparison.Ordinal);
    }

    /// <summary>The cap says so (trap 50): a row that named more ingredients than it drew
    /// reports the rest rather than dropping them.</summary>
    [Fact]
    public void ARowThatNamesMoreThanItDrawsSaysHowManyItHeldBack()
    {
        var many = Enumerable.Range(0, Recommendations.MaterialsNamedPerRow + 2)
            .Select(i => Material($"Gem {i}", Tradeskill.Jewelcrafting, ["Lower Guk"]))
            .ToArray();

        var row = Assert.Single(Rank(Inputs(new ItemCatalog(many))).Top);

        Assert.Equal(Recommendations.MaterialsNamedPerRow,
            row.Why.OfType<TradeskillMaterialFact>().Count());
        Assert.Equal(2, row.WithheldWhy);
    }
}
