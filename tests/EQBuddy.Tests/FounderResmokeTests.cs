using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// **WHAT THE FOUNDER WILL SEE WHEN HE RE-SMOKES, PREDICTED AND PINNED** (DRA-149 D5, plan P6).
///
/// <para>Every number here is taken by running the REAL engine over the Founder's own committed
/// dump (<c>tests/fixtures/inventory/dranak.txt</c>) and the REAL shipped catalog. A fixture in
/// the wrong shape produces a state that is real and is not the one under test (trap 23), and a
/// two-record catalog would photograph the thing being measured as absent — acceptance 1 is
/// precisely that the answers come from full item knowledge.</para>
///
/// <para><b>This file is the checklist's arithmetic.</b>
/// <c>docs/ops/dra149-founder-resmoke.md</c> tells him what each screen should SAY; these are the
/// numbers behind those sentences, so a wiki refresh that moves one reddens here rather than
/// making the checklist quietly wrong. The E2E row
/// <c>TheFoundersOwnDumpReachesTheHelperWithCandidatesAndRefusals</c> asserts the same
/// relationships from a launched app, because "the engine computed it" and "the screen shows it"
/// are different claims (trap 56).</para>
///
/// <para><b>The prediction that matters most is a NEGATIVE one.</b> Before D1 the sweep returned
/// zero candidates for every gear anchor he owns — the tier rule asked
/// <c>UpgradeTier(candidate) &gt;= UpgradeTier(worn)</c> against a catalog where 0 of 11,196
/// names carry a "+N", so the answer was decided before the inputs were read. A re-smoke that
/// still shows an empty gear list is only a failure if the candidate count is ALSO zero; a
/// large candidate count with an empty list is the band gate doing its job, which is a different
/// screen with a different sentence on it.</para>
/// </summary>
public class FounderResmokeTests
{
    private static List<InventoryFile.Entry> Dump() =>
        InventoryFile.ParseEntries(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "inventory", "dranak.txt")));

    private static WornSheet Sheet() =>
        GearUpgrades.WornFrom(Dump(), n => ItemCatalog.Default.Find(n)?.ToStatsBlock());

    /// <summary>The Helper's inputs for this character at a stated level — everything
    /// <c>Rank</c> reads, with no session history at all. A brand-new profile with one
    /// <c>/outputfile inventory</c> behind it is exactly what the re-smoke starts from.</summary>
    private static HelperInputs Inputs(int level, GearIntent intent = GearIntent.UpgradeWorn,
                                       bool includeQuests = false)
    {
        var sheet = Sheet();
        return new HelperInputs(
            ZoneHistory.Fold([], []), [], null, [], [], [], [], false, [], [], null,
            new ResolvedLevel(level, LevelSource.Stated, DateTime.Now))
        {
            Items = ItemCatalog.Default,
            Bands = ZoneLevels.Default,
            Worn = sheet.Worn,
            UnreadWorn = sheet.Unread,
            GearIntent = intent,
            IncludeQuests = includeQuests,
        };
    }

    // ---- FAIL 1: the bow ------------------------------------------------------------------

    /// <summary>
    /// **The Founder's bow anchors.** The game prints `Deterioriated Ancient Faydark Longbow +2`
    /// and eqlwiki titles the page `Deteriorated Ancient Faydark Longbow`; D2's curated alias is
    /// the one row that joins them. Without it this anchor does not exist and the room shows 19
    /// worn items with nothing saying why the twentieth vanished.
    /// </summary>
    [Fact]
    public void TheBowIsAnAnchorAndIsNotInTheUnreadList()
    {
        var sheet = Sheet();

        Assert.Contains(sheet.Worn, w =>
            w.Name.Contains("Faydark Longbow", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(sheet.Unread, n =>
            n.Contains("Faydark Longbow", StringComparison.OrdinalIgnoreCase));
        // And his whole dump now reads: nothing is left unread, so the D2 caption is absent on
        // HIS screen. The caption's own coverage lives in its own tests; what this pins is that
        // the re-smoke should NOT see it, which is a prediction the checklist has to make or a
        // correct blank reads as a missing feature.
        Assert.Empty(sheet.Unread);
    }

    /// <summary>
    /// **THE PLAN'S WORKED EXAMPLE, MEASURED: four base-better items for the bow — three drops
    /// and one quest.**
    ///
    /// <para>P6 predicted *"4 base-better items — Temple of Veeshan `60+` and Sleeper's Tomb
    /// `55+` refused against the staged level, 1 quest row behind the toggle"*. That is exactly
    /// what the shipped catalog holds, and it is pinned here rather than left in the plan: two
    /// Velium Reinforced bows in Sleeper's Tomb, the Bow of the Silver Fang in Temple of
    /// Veeshan, and <c>Rune Shafted Harpoon</c> from <i>The Mighty Snowfang Hero</i> — which
    /// appears ONLY with the include-quests toggle on, because a quest is not a camp.</para>
    ///
    /// <para><b>This is the single clearest refutation of FAIL 2.</b> Before D1 this anchor
    /// produced zero, and the reason was not the catalog: the tier rule compared a worn "+2"
    /// against catalog names that never carry one.</para>
    ///
    /// <para><b>IT IS A FACT ABOUT THE CATALOG, NOT ABOUT ANY CHARACTER, AND THE STAGED SHOT IS
    /// WHAT MADE THAT VISIBLE.</b> This runs with NO class lock, so four is the number a
    /// character who was somehow both a Ranger and a Shaman would see. The class lock decides
    /// the rest: <c>Bow of the Silver Fang</c> (Temple of Veeshan) is <c>RNG</c> only and
    /// <c>Rune Shafted Harpoon</c> is <c>SHM</c> only, while the two Sleeper's Tomb bows are
    /// <c>WAR|PAL|RNG|SHD|ROG</c>. <c>shell-helper-founder-bow</c> photographs the fixture
    /// character getting ONE refused zone and no quest row, which is correct and is a strict
    /// subset of this. A reader taking the four below as "what the Founder will see" would be
    /// reading a catalog fact as a personal one.</para>
    /// </summary>
    [Fact]
    public void TheBowHasFourBaseBetterItemsAndTheFourthIsOnlyThereWithQuestsOn()
    {
        var bow = Sheet().Worn.First(w =>
            w.Name.Contains("Faydark Longbow", StringComparison.OrdinalIgnoreCase));

        var drops = GearUpgrades.Sweep(
            GearIntent.UpgradeWorn, [bow], [], ItemCatalog.Default, [], includeQuests: false);
        var withQuests = GearUpgrades.Sweep(
            GearIntent.UpgradeWorn, [bow], [], ItemCatalog.Default, [], includeQuests: true);

        Assert.Equal(3, drops.Upgrades.Count);
        Assert.Equal(4, withQuests.Upgrades.Count);

        // Two zones and no third — and they are the two the checklist tells him will be REFUSED
        // at his level, which is what makes that screen predictable rather than alarming.
        Assert.Equal(["Sleeper's Tomb", "Temple of Veeshan"],
            drops.Upgrades.SelectMany(u => u.Zones).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(z => z, StringComparer.Ordinal));

        // The quest row is the one that is NOT a camp, so no zone and no creature is asked of it.
        var quest = Assert.Single(withQuests.Upgrades.Where(u => u.Quests.Count > 0));
        Assert.Equal("Rune Shafted Harpoon", quest.Item);
        Assert.Equal(["The Mighty Snowfang Hero"], quest.Quests);
        Assert.Empty(quest.Zones);
    }

    // ---- FAIL 2: the sweep answers a plussed character -------------------------------------

    /// <summary>
    /// **The engine finds candidates for his dump, and the count is the FAIL's own number.**
    ///
    /// <para>Ranged at 29 — his level on the failing build — so what the checklist predicts is
    /// what he will actually meet. The assertion is a FLOOR rather than an equality: a weekly
    /// wiki refresh adds and removes catalog rows, and a gate that reddens on churn is a gate
    /// nobody believes (trap 74's lesson).</para>
    /// </summary>
    [Fact]
    public void TheSweepFindsCandidatesForTheDumpThatUsedToReturnNone()
    {
        var set = Recommendations.Rank(Inputs(29), [HelperGoal.FarmGear]);

        Assert.True(set.GearCandidates > 0,
            "the sweep found nothing for the Founder's dump — this is FAIL 2 back");
        // MEASURED at 155 against the shipped catalog. The floor is 50 rather than an equality
        // because a weekly wiki refresh moves catalog rows, and a gate that reddens on churn is
        // one the next person re-runs until green (trap 74's lesson).
        Assert.True(set.GearCandidates >= 50,
            $"only {set.GearCandidates} candidates — expected the sweep to answer broadly");

        // And the sweep's own per-anchor cap held a great deal back, which is the OTHER half of
        // "it answers now": 1,791 at the time of writing. A cap with nothing under it would mean
        // the sweep barely cleared its floor.
        Assert.True(set.GearWithheld > set.GearCandidates,
            $"the per-anchor cap held back {set.GearWithheld} against {set.GearCandidates} shown");
    }

    /// <summary>
    /// **A band refusal is the fix WORKING, and the checklist has to say so.**
    ///
    /// <para>At 29 the band gate refuses the high-level planes his best candidates drop in, and
    /// the room says which zones and quotes their bands. That screen reads like a failure and is
    /// not one — so the count is asserted alongside the candidate count, which is the pair that
    /// tells "refused" from "found nothing".</para>
    /// </summary>
    [Fact]
    public void AtTwentyNineTheBandGateRefusesAndSaysWhichZonesAndWhichBands()
    {
        var set = Recommendations.Rank(Inputs(29), [HelperGoal.FarmGear]);

        // MEASURED at 16 zones here and 18 through the launched app — the app knows his CLASS
        // from the log and this fixture does not, so the class lock changes which candidates
        // survive and therefore which zones are in front of the gate at all. Both are right
        // about their own inputs, which is why this is a floor and the E2E row carries its own.
        Assert.True(set.GearBandRefusals.Count >= 10,
            $"only {set.GearBandRefusals.Count} zones refused at 29");
        Assert.All(set.GearBandRefusals, r =>
        {
            Assert.NotEmpty(r.Zone);
            Assert.NotEmpty(r.Verbatim);
            Assert.Equal(29, r.Level);
        });

        // The two the bow's own upgrades drop in, by name AND by arm — this is the pair the
        // checklist tells him to expect, so a screen that refuses them reads as the gate working
        // rather than as the fail repeating. `BottomOver` is asserted per zone rather than over
        // the whole list: at 29 he has also OUTGROWN a low zone or two, and a blanket claim
        // would be a fact about this fixture's class filter rather than about the gate.
        Assert.Contains(set.GearBandRefusals,
            r => r.Zone == "Temple of Veeshan" && r.Arm == GearBandArm.BottomOver);
        Assert.Contains(set.GearBandRefusals,
            r => r.Zone == "Sleeper's Tomb" && r.Arm == GearBandArm.BottomOver);

        // And rows SURVIVED it — the refusals are a filter, not a wipe. Measured: Kael Drakkel,
        // Great Divide, Blackburrow. Places a 29 can actually stand in.
        Assert.NotEmpty(set.Top.Where(r => r.Why.OfType<GearUpgradeFact>().Any()));

        // The sentence quotes the level AND every band, so a player can check the arithmetic
        // (trap 50: a rule that removed rows says so in its own voice).
        var said = HelperPresentation.BandRefused(
            set.GearBandRefusals, HelperPresentation.BandRefusedUpgrades);
        Assert.NotEqual("", said);
        Assert.Contains(set.GearBandRefusals.Count.ToString(), said, StringComparison.Ordinal);
        Assert.Contains("your level 29", said, StringComparison.Ordinal);
        // The caption NAMES some and counts the rest — its own cap saying so (trap 50), which is
        // why this asserts the first NAMED one rather than the first refusal.
        Assert.Contains(set.GearBandRefusals[0].Zone, said, StringComparison.Ordinal);
        Assert.Contains("and ", said, StringComparison.Ordinal);
        // **Only the arms that actually FIRED are quoted**, which is the property under test
        // rather than a claim about this fixture: a sentence that named a threshold nothing was
        // refused on would be quoting a rule that decided nothing.
        Assert.Contains("starts", said, StringComparison.Ordinal);
        Assert.Equal(
            set.GearBandRefusals.Any(r => r.Arm == GearBandArm.TopUnder),
            said.Contains("tops out", StringComparison.Ordinal));
    }

    /// <summary>
    /// **THE GATE WORKS IN BOTH DIRECTIONS, AND ONLY A SECOND LEVEL CAN SHOW IT.**
    ///
    /// <para>At 29 every refusal is <c>BottomOver</c> — he is under all of them — and the row
    /// above would pass for a gate that had no top arm at all. At 60 the picture inverts: Temple
    /// of Veeshan and Sleeper's Tomb become the top three, and what gets refused is a zone he has
    /// OUTGROWN (<c>Great Divide</c>, `30-45`, <c>TopUnder</c>). One character, one dump, two
    /// levels, opposite answers.</para>
    ///
    /// <para>It also pins the open-top ruling behaviourally: none of the `60+` zones can be
    /// refused on the top arm at any level, because an open-topped band has no maximum to be
    /// under (DRA-84 D2, Helm option (a)).</para>
    /// </summary>
    [Fact]
    public void AtSixtyTheSameDumpRefusesWhatHeHasOutgrownInstead()
    {
        var set = Recommendations.Rank(Inputs(60), [HelperGoal.FarmGear]);

        Assert.Contains(set.GearBandRefusals, r => r.Arm == GearBandArm.TopUnder);
        Assert.DoesNotContain(set.GearBandRefusals, r => r.Zone == "Temple of Veeshan");
        // An open top is never refused from above — the `+` is the absence of a maximum, not a
        // number, so the top arm stands down for exactly those zones.
        Assert.DoesNotContain(set.GearBandRefusals,
            r => r.Verbatim.EndsWith('+') && r.Arm == GearBandArm.TopUnder);
    }

    /// <summary>
    /// **THE PAIR THAT TELLS THE TWO EMPTY SCREENS APART.** Whatever the band gate does at a
    /// given level, the candidate count is non-zero — so an empty list is always attributable.
    /// Run at three levels, because "it answers at 29" and "it answers at all" are different
    /// claims and only the second survives him dinging.
    /// </summary>
    [Theory]
    [InlineData(29)]
    [InlineData(45)]
    [InlineData(60)]
    public void AtEveryLevelTheCandidateCountIsNonZeroSoAnEmptyListIsAlwaysExplained(int level)
    {
        var set = Recommendations.Rank(Inputs(level), [HelperGoal.FarmGear]);

        Assert.True(set.GearCandidates > 0, $"no candidates at level {level}");
        // Either there are rows, or something REMOVED them and said so. A screen with neither
        // is the one the Founder failed.
        var rows = set.Top.Count(r => r.Why.OfType<GearUpgradeFact>().Any());
        Assert.True(rows > 0 || set.GearBandRefusals.Count > 0 || set.GearWhoWithheld > 0
                    || set.Gaps.Any(g => g.Goal == HelperGoal.FarmGear),
            $"level {level}: {set.GearCandidates} candidates, no rows, and nothing said why");
    }

    // ---- FAIL 3: the two tradeskill answers ------------------------------------------------

    /// <summary>
    /// **Farm Materials answers from the shipped catalog with no session history at all.**
    ///
    /// <para>The drop half needs nothing stored — the materials, their zones and their creatures
    /// are catalog facts — so the re-smoke should see rows on first paint. The gates may still
    /// refuse them at a given level, and that is what the second half asserts: whatever happens,
    /// something says why.</para>
    /// </summary>
    [Fact]
    public void FarmMaterialsAnswersOrSaysWhyOnAProfileWithNoPlayHistory()
    {
        var set = Recommendations.Rank(Inputs(29), [HelperGoal.FarmMaterials]);

        var rows = set.Top.Count(r => r.Why.OfType<TradeskillMaterialFact>().Any());
        Assert.True(rows > 0 || set.MaterialBandRefusals.Count > 0
                    || set.MaterialWhoWithheld > 0
                    || set.Gaps.Any(g => g.Goal == HelperGoal.FarmMaterials),
            "Farm Materials drew nothing and said nothing about why");
        // It is not Deferred any more — the deferral sentence leaving is half of FAIL 3a.
        Assert.DoesNotContain(HelperGoal.FarmMaterials, set.NotAnsweredYet);
    }

    /// <summary>
    /// **The vendor half needs no profile at all, and that is the checklist's sharpest
    /// prediction.** Every one of the eight professions has at least one shop on screen the
    /// moment the room opens, on ANY character — so an empty vendor block at re-smoke is a real
    /// failure rather than "no data yet", which is the opposite of how every other block in this
    /// room reads.
    /// </summary>
    [Fact]
    public void EveryProfessionHasAShopOnScreenBeforeAnythingIsPlayed()
    {
        foreach (var skill in Enum.GetValues<Tradeskill>())
        {
            var shown = HelperPresentation.MerchantsShown(ZoneMerchants.Default, skill);
            Assert.True(shown.Count > 0,
                $"{skill} draws its empty state — the checklist predicts a shop for all eight");
            Assert.All(shown, m => Assert.NotEmpty(HelperPresentation.MerchantRow(m)));
        }
    }

    /// <summary>The jewelcrafting answer the Founder asked for BY NAME, on both halves: gems
    /// have somewhere to be farmed and somewhere to be bought.</summary>
    [Fact]
    public void JewelcraftingAnswersOnBothHalvesBecauseHeAskedForItByName()
    {
        // The buy half: shops, in the wiki's own words.
        Assert.True(ZoneMerchants.Default.ZonesFor(Tradeskill.Jewelcrafting) >= 3);

        // The farm half: materials with somewhere real to farm them.
        var materials = TradeskillMaterials.From(
            ItemCatalog.Default, [Tradeskill.Jewelcrafting]);
        Assert.NotEmpty(materials);
        Assert.All(materials, m => Assert.NotEmpty(m.Zones));
    }
}
