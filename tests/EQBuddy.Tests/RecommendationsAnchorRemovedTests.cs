using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE PER-ANCHOR ANSWER** (DRA-180 D3, Fable plan P3; Founder Desktop smoke 2026-09-17,
/// FAIL 1).
///
/// <para>The Founder asked the Helper for upgrades to a worn bow and to the Baron's Blade and
/// got an empty list both times, with no sentence either time. He filed a FAIL, and he was
/// right to: an unexplained empty is indistinguishable from a broken sweep. But for the bow
/// the list was CORRECT — exactly two catalog RANGE items dominate its base, both drop in
/// Sleeper's Tomb, and the band gate refused both at level 29. Every fact needed to say so was
/// already in the engine. None of it was on the screen.</para>
///
/// <para><b>What this file covers is the fact the refusal lists cannot carry.</b>
/// <c>GearBandRefusals</c> and <c>GearEraRefusals</c> are keyed on the PLACE; the player's
/// question is about the ITEM ON THEIR CHARACTER. The block-level gap reasons only fire when
/// the WHOLE list empties, so a character with one dead anchor and nine live ones still got
/// nothing for the dead one. <see cref="GearAnchorRemoved"/> is the record that closes that,
/// and the three causes are asserted to PARTITION the count rather than merely co-exist.</para>
///
/// <para><b>The band half of this ships LIVE and the era half ships dark</b>, because
/// <see cref="WorldEra.Current"/> is still empty until D5. That is deliberate and it is why
/// the bow's answer does not wait on the Founder: the tests below that set a world era say so
/// by passing one.</para>
/// </summary>
public class RecommendationsAnchorRemovedTests
{
    // ---- fixtures (RecommendationsEraGateTests' own, so the two files cannot drift) -----

    private static ItemCatalog.Record Record(
        string name, string slot, int ac, string[]? zones = null, bool named = true) =>
        new()
        {
            Name = name, StatsText = $"Slot: {slot}\nAC: {ac}", Slots = [slot], Ac = ac,
            DropZones = zones?.ToList(),
            // `named: false` is the who rule's own input — a page that lists the item's zones
            // and names nothing that drops it there. It is 1.8% of the shipped catalog's
            // wearable pairs, and it is the third way an anchor can be emptied.
            DropMobs = !named || zones is not { Length: > 0 }
                ? null
                : zones.Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(z => z, z => new List<string> { $"a {z.ToLowerInvariant()} dweller" },
                        StringComparer.OrdinalIgnoreCase),
        };

    private static WornItem Worn(string name, string slot, int ac) =>
        new(name, name, slot, ItemStatsBlock.Parse([$"Slot: {slot}", $"AC: {ac}"]));

    private static ZoneEras Eras(params (string Zone, string Era)[] rows) =>
        new(rows.Where(r => r.Era.Length != 0)
                .ToDictionary(r => r.Zone, r => new ZoneEras.Banner(r.Era, $"{{{{{r.Era} Era}}}}")),
            new Dictionary<string, string>());

    private static ZoneLevels Bands(params (string Zone, int Min, int? Max)[] rows) =>
        new(rows.ToDictionary(
                r => r.Zone,
                r => new ZoneLevels.Band(
                    r.Min, r.Max,
                    r.Max is { } m ? (m == r.Min ? $"{r.Min}" : $"{r.Min}-{m}") : $"{r.Min}+")),
            new Dictionary<string, string>());

    private static HelperInputs Gear(
        IReadOnlyList<WornItem> worn,
        ItemCatalog? catalog,
        ZoneEras? eras = null,
        string world = "",
        int? level = null,
        ZoneLevels? bands = null) =>
        new([], [], null, [], [], [], [], false, [], [], null,
            level is { } l
                ? new ResolvedLevel(l, LevelSource.Observed, new DateTime(2026, 9, 17, 21, 0, 0))
                : ResolvedLevel.Unknown)
        {
            Worn = worn, Items = catalog, GearIntent = GearIntent.UpgradeWorn,
            Bands = bands, Eras = eras, World = world,
        };

    private static RecommendationSet Rank(HelperInputs inputs) =>
        Recommendations.Rank(inputs, [HelperGoal.FarmGear]);

    // ---- the Founder's bow, reconstructed ----------------------------------------------

    /// <summary>
    /// **THE BOW SCREEN, AND IT IS THE ONE THIS SLICE EXISTS FOR** (plan §0's measurement).
    ///
    /// <para>Two catalog items dominate the anchor's base; both drop in one zone; eqlwiki lists
    /// that zone's creatures at 55 and up; the character is 29. The old screen said nothing at
    /// all. The new one says EQBuddy read two better base items and left both out because the
    /// only place they drop is outside this character's band — which is the strongest TRUE
    /// claim available, and it is not the same sentence as "your bow is the best bow".</para>
    ///
    /// <para><b>It runs with NO world era</b>, exactly as every shipped build does today. The
    /// era count is therefore 0 and the band count carries the whole answer — which is the
    /// evidence that this slice's value does not wait on D5.</para>
    /// </summary>
    [Fact]
    public void TheFoundersBowScreenNamesTheTwoItemsItFoundAndTheBandThatTookThem()
    {
        var set = Rank(Gear(
            [Worn("Deteriorated Ancient Faydark Longbow", "RANGE", 4)],
            new ItemCatalog([
                Record("Priceless Bow", "RANGE", 12, ["Sleeper's Tomb"]),
                Record("Primal Velium Reinforced Bow", "RANGE", 14, ["Sleeper's Tomb"]),
            ]),
            level: 29, bands: Bands(("Sleeper's Tomb", 55, null))));

        var anchor = Assert.Single(set.GearAnchorsRemoved);
        Assert.Equal("Deteriorated Ancient Faydark Longbow", anchor.Anchor);
        Assert.Equal("RANGE", anchor.Slot);
        Assert.Equal(2, anchor.Found);
        Assert.Equal(2, anchor.OutsideBand);
        Assert.Equal(0, anchor.LaterContent);
        Assert.Equal(0, anchor.NoCreature);

        // The words, from the one producer both surfaces call.
        var said = HelperPresentation.AnchorAllRemoved(anchor);
        Assert.Contains("Deteriorated Ancient Faydark Longbow", said);
        Assert.Contains("2 better base items", said);
        Assert.Contains("creature levels outside yours", said);
        Assert.Contains("Nothing in reach beats this item's base.", said);
    }

    /// <summary>
    /// **A gate that did not run is not in the sentence** (plan D3: furniture).
    ///
    /// <para>The bow screen above runs with the era gate dark, and the clause about later
    /// content must be absent rather than present and zero. A "0 come from content eqlwiki
    /// dates later…" clause would be a sentence about a rule that did not exist on this build,
    /// which is the exact shape the plan refused by name.</para>
    /// </summary>
    [Fact]
    public void WithTheEraGateDarkTheSentenceCarriesNoEraClause()
    {
        var set = Rank(Gear(
            [Worn("Deteriorated Ancient Faydark Longbow", "RANGE", 4)],
            new ItemCatalog([Record("Priceless Bow", "RANGE", 12, ["Sleeper's Tomb"])]),
            level: 29, bands: Bands(("Sleeper's Tomb", 55, null))));

        Assert.False(set.EraGateLive);
        var said = HelperPresentation.AnchorAllRemoved(Assert.Single(set.GearAnchorsRemoved));
        Assert.DoesNotContain("later than the era", said);
        Assert.DoesNotContain("0 ", said);
    }

    // ---- the three causes, one at a time -----------------------------------------------

    /// <summary>The era gate's own cause, with a world era supplied so the gate is live — the
    /// Baron's Blade half of the FAIL, which lights up the day D5 lands.</summary>
    [Fact]
    public void AnAnchorEmptiedByTheEraGateCountsUnderLaterContentAlone()
    {
        var set = Rank(Gear(
            [Worn("The Baron's Blade", "PRIMARY", 4)],
            new ItemCatalog([Record("Blade of Carnage", "PRIMARY", 15, ["Kael Drakkel"])]),
            Eras(("Kael Drakkel", "Velious")), world: "Classic",
            level: 29, bands: Bands(("Kael Drakkel", 30, 60))));

        var anchor = Assert.Single(set.GearAnchorsRemoved);
        Assert.Equal(1, anchor.Found);
        Assert.Equal(1, anchor.LaterContent);
        Assert.Equal(0, anchor.OutsideBand);
        Assert.Equal(0, anchor.NoCreature);

        var said = HelperPresentation.AnchorAllRemoved(anchor);
        Assert.Contains("The Baron's Blade", said);
        Assert.Contains("later than the era", said);
        Assert.DoesNotContain("creature levels outside yours", said);
    }

    /// <summary>The who rule's own cause — a page that lists the zone and names nobody in it.
    /// The Rathe exhibit's shape, one axis over (DRA-84 D4).</summary>
    [Fact]
    public void AnAnchorEmptiedByTheWhoRuleCountsUnderNoCreatureAlone()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Fine Steel Helm", "HEAD", 9, ["Rathe Mountains"], named: false)])));

        var anchor = Assert.Single(set.GearAnchorsRemoved);
        Assert.Equal(1, anchor.Found);
        Assert.Equal(1, anchor.NoCreature);
        Assert.Equal(0, anchor.LaterContent);
        Assert.Equal(0, anchor.OutsideBand);

        Assert.Contains("name nothing that drops them",
            HelperPresentation.AnchorAllRemoved(anchor));
    }

    /// <summary>
    /// **All three causes at once, and the three numbers PARTITION the count.**
    ///
    /// <para>That identity is what the sentence leans on — it says EQBuddy found N and then
    /// accounts for all N — so it is asserted as arithmetic rather than left to the reader. A
    /// candidate double-counted under two rules would make the sentence add up to more than it
    /// found, and no word test would notice.</para>
    /// </summary>
    [Fact]
    public void TheThreeCausesAddUpToWhatTheSweepFound()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([
                Record("Velium Helm", "HEAD", 20, ["Kael Drakkel"]),
                Record("Outgrown Helm", "HEAD", 9, ["Crushbone"]),
                Record("Anonymous Helm", "HEAD", 11, ["Rathe Mountains"], named: false),
            ]),
            Eras(("Kael Drakkel", "Velious"), ("Crushbone", "Classic"),
                 ("Rathe Mountains", "Classic")),
            world: "Classic", level: 30,
            bands: Bands(("Kael Drakkel", 30, 60), ("Crushbone", 5, 20),
                         ("Rathe Mountains", 13, 45))));

        var anchor = Assert.Single(set.GearAnchorsRemoved);
        Assert.Equal(3, anchor.Found);
        Assert.Equal(1, anchor.LaterContent);
        Assert.Equal(1, anchor.OutsideBand);
        Assert.Equal(1, anchor.NoCreature);
        Assert.Equal(
            anchor.Found, anchor.LaterContent + anchor.OutsideBand + anchor.NoCreature);

        var said = HelperPresentation.AnchorAllRemoved(anchor);
        Assert.Contains("3 better base items", said);
        Assert.Contains(" and ", said);
    }

    /// <summary>
    /// **A candidate is charged to the stage that took its LAST door, not to the first rule
    /// that touched it.**
    ///
    /// <para>One item dropping in a Velious zone AND an out-of-band Classic one is charged to
    /// the BAND gate: after the era gate ran it was still on the list, through its second zone.
    /// Charging it to the era gate would be a sentence blaming a rule the player could have
    /// waited out for an item they will still never be offered.</para>
    /// </summary>
    [Fact]
    public void ACandidateIsChargedToTheGateThatTookItsLastRemainingPlace()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Fine Steel Helm", "HEAD", 9, ["Kael Drakkel", "Crushbone"])]),
            Eras(("Kael Drakkel", "Velious"), ("Crushbone", "Classic")),
            world: "Classic", level: 30,
            bands: Bands(("Kael Drakkel", 30, 60), ("Crushbone", 5, 20))));

        var anchor = Assert.Single(set.GearAnchorsRemoved);
        Assert.Equal(1, anchor.Found);
        Assert.Equal(1, anchor.OutsideBand);
        Assert.Equal(0, anchor.LaterContent);
    }

    // ---- the negatives, which are what keep this off a working screen ------------------

    /// <summary>
    /// **AN ANCHOR WITH A SURVIVOR IS NOT REPORTED**, and this is the assertion that stops the
    /// feature becoming a caveat stapled to every working list.
    ///
    /// <para>One of the two candidates is drawable. The player has a camp to go to, on screen,
    /// and a sentence counting the other one would be EQBuddy apologising for an answer it
    /// successfully gave.</para>
    /// </summary>
    [Fact]
    public void AnAnchorWithOneSurvivingCandidateSaysNothing()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([
                Record("Velium Helm", "HEAD", 20, ["Sleeper's Tomb"]),
                Record("Fine Steel Helm", "HEAD", 9, ["Crushbone"]),
            ]),
            level: 29, bands: Bands(("Sleeper's Tomb", 55, null), ("Crushbone", 5, 20))));

        Assert.NotEmpty(set.Top);
        Assert.Empty(set.GearAnchorsRemoved);
    }

    /// <summary>
    /// **An anchor the sweep found NOTHING for is not reported either**, and that distinction
    /// is load-bearing.
    ///
    /// <para>"The catalog holds nothing better than what you wear" and "the catalog holds two
    /// better and you can reach neither" are different facts about the world.
    /// <see cref="GoalGapReason.NoCatalogUpgrade"/> has owned the first since DRA-71 D6 and
    /// keeps it; putting a number on that screen would invent a finding for a character who has
    /// genuinely topped out a slot.</para>
    /// </summary>
    [Fact]
    public void AnAnchorNothingDominatesIsLeftToNoCatalogUpgrade()
    {
        var set = Rank(Gear(
            [Worn("Peerless Helm", "HEAD", 40)],
            new ItemCatalog([Record("Rusty Helm", "HEAD", 4, ["Crushbone"])])));

        Assert.Empty(set.GearAnchorsRemoved);
        Assert.Contains(set.Gaps, g => g.Reason == GoalGapReason.NoCatalogUpgrade);
    }

    /// <summary>A run that never reached the sweep reports no anchors — no dump is a different
    /// state from a swept one, and it has a command that fixes it.</summary>
    [Fact]
    public void WithNoInventoryDumpThereAreNoAnchorsToReport()
    {
        var set = Rank(Gear([], new ItemCatalog([Record("Fine Steel Helm", "HEAD", 9, ["Crushbone"])])));

        Assert.Empty(set.GearAnchorsRemoved);
        Assert.Contains(set.Gaps, g => g.Reason == GoalGapReason.NoInventoryDump);
    }

    /// <summary>
    /// **Two emptied anchors are two rows, and the surface's cap is what decides how many are
    /// SAID** (trap 50). The engine's list is whole so the cap can count what it held back.
    /// </summary>
    [Fact]
    public void EachEmptiedAnchorIsItsOwnRowAndTheEngineListIsWhole()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4), Worn("Rusty Boots", "FEET", 3)],
            new ItemCatalog([
                Record("Velium Helm", "HEAD", 20, ["Sleeper's Tomb"]),
                Record("Velium Boots", "FEET", 18, ["Sleeper's Tomb"]),
            ]),
            level: 29, bands: Bands(("Sleeper's Tomb", 55, null))));

        Assert.Equal(2, set.GearAnchorsRemoved.Count);
        Assert.Contains(set.GearAnchorsRemoved, a => a.Slot == "HEAD");
        Assert.Contains(set.GearAnchorsRemoved, a => a.Slot == "FEET");
        // Nothing is held back at two, so the cap line says nothing at all.
        Assert.Equal("", HelperPresentation.AnchorsNotNamed(
            set.GearAnchorsRemoved.Count - HelperPresentation.GearAnchorsNamed));
    }
}
