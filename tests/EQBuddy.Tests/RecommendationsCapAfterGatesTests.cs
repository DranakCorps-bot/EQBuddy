using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE PER-ANCHOR CAP IS SPENT AFTER THE GATES, NOT BEFORE THEM** (DRA-180 D5a, Helm ADOPT
/// Ask 1).
///
/// <para>Measured on the Founder's own dump at level 29 with the era gate lit: 15 anchors read
/// "Nothing in reach beats this item's base", and with the cap lifted only 6 did. The sweep
/// ranks each anchor's list by stats improved and kept the top <see cref="GearUpgrades.MaxPerAnchor"/>;
/// later-era items took all eight, the era gate refused all eight, and the reachable Classic
/// upgrade ranked ninth was never offered. Every fixture below puts EIGHT refused candidates
/// ahead of one reachable one in the sweep's own order — the sweep breaks an equal metric count
/// by NAME, so the "Aa" prefix is what ranks the refused ones first — and asserts the reachable
/// one is the answer.</para>
/// </summary>
public class RecommendationsCapAfterGatesTests
{
    // ---- fixtures (RecommendationsAnchorRemovedTests' own) -----------------------------

    private static ItemCatalog.Record Record(
        string name, string slot, int ac, string[] zones, bool named = true) =>
        new()
        {
            Name = name, StatsText = $"Slot: {slot}\nAC: {ac}", Slots = [slot], Ac = ac,
            DropZones = zones.ToList(),
            DropMobs = !named
                ? null
                : zones.ToDictionary(z => z, z => new List<string> { $"a {z.ToLowerInvariant()} dweller" },
                    StringComparer.OrdinalIgnoreCase),
        };

    private static WornItem Worn(string name, string slot, int ac) =>
        new(name, name, slot, ItemStatsBlock.Parse([$"Slot: {slot}", $"AC: {ac}"]));

    private static ZoneEras Eras(params (string Zone, string Era)[] rows) =>
        new(rows.ToDictionary(r => r.Zone, r => new ZoneEras.Banner(r.Era, $"{{{{{r.Era} Era}}}}")),
            new Dictionary<string, string>());

    private static ZoneLevels Bands(params (string Zone, int Min, int? Max)[] rows) =>
        new(rows.ToDictionary(
                r => r.Zone,
                r => new ZoneLevels.Band(
                    r.Min, r.Max, r.Max is { } m ? $"{r.Min}-{m}" : $"{r.Min}+")),
            new Dictionary<string, string>());

    private static HelperInputs Gear(
        IReadOnlyList<WornItem> worn, ItemCatalog catalog,
        ZoneEras? eras = null, string world = "", int? level = null, ZoneLevels? bands = null) =>
        new([], [], null, [], [], [], [], false, [], [], null,
            level is { } l
                ? new ResolvedLevel(l, LevelSource.Observed, new DateTime(2026, 9, 23, 18, 0, 0))
                : ResolvedLevel.Unknown)
        {
            Worn = worn, Items = catalog, GearIntent = GearIntent.UpgradeWorn,
            Bands = bands, Eras = eras, World = world,
        };

    private static RecommendationSet Rank(HelperInputs inputs) =>
        Recommendations.Rank(inputs, [HelperGoal.FarmGear]);

    /// <summary>Eight candidates the sweep ranks first, all dropping in one zone.</summary>
    private static IEnumerable<ItemCatalog.Record> EightAhead(string zone, bool named = true) =>
        Enumerable.Range(1, GearUpgrades.MaxPerAnchor)
            .Select(i => Record($"Aa Velium Helm {i:00}", "HEAD", 20 + i, [zone], named));

    private static readonly WornItem Helm = Worn("Rusty Helm", "HEAD", 4);

    // ---- the three gates, each with eight refused ahead of one reachable -----------------

    /// <summary>
    /// **THE FOUNDER'S DEFECT, EXACTLY.** Eight Velious items outrank one Classic item; the
    /// world is Classic. Before D5a the cap took the eight, the era gate refused them, and the
    /// helm read "nothing in reach". Prove-failed: restoring the sweep's own cap in
    /// <c>FarmGear</c> reddens every assertion after the first.
    /// </summary>
    [Fact]
    public void AReachableUpgradeRankedNinthSurvivesEightTheEraGateRefuses()
    {
        var set = Rank(Gear([Helm],
            new ItemCatalog([.. EightAhead("Kael Drakkel"),
                Record("Zz Bronze Helm", "HEAD", 10, ["Lower Guk"])]),
            eras: Eras(("Kael Drakkel", "Velious"), ("Lower Guk", "Classic")),
            world: "Classic"));

        Assert.True(set.EraGateLive);
        Assert.Equal(GearUpgrades.MaxPerAnchor + 1, set.GearCandidates);
        Assert.Empty(set.GearAnchorsRemoved);
        var row = Assert.Single(set.Top);
        Assert.Equal("Lower Guk", row.Zone);
        Assert.Contains("Kael Drakkel", set.GearEraRefusals.Select(r => r.Subject));
        // The cap held back nothing: the eight were the ERA gate's, and they are its to report.
        Assert.Equal(0, set.GearWithheld);
    }

    /// <summary>The band gate's version: the eight drop where eqlwiki lists creatures at 55+,
    /// the character is 29, and no world era is set.</summary>
    [Fact]
    public void AReachableUpgradeRankedNinthSurvivesEightTheBandGateRefuses()
    {
        var set = Rank(Gear([Helm],
            new ItemCatalog([.. EightAhead("Sleeper's Tomb"),
                Record("Zz Bronze Helm", "HEAD", 10, ["Lower Guk"])]),
            level: 29, bands: Bands(("Sleeper's Tomb", 55, null), ("Lower Guk", 7, 30))));

        Assert.Empty(set.GearAnchorsRemoved);
        Assert.Equal("Lower Guk", Assert.Single(set.Top).Zone);
        Assert.Single(set.GearBandRefusals);
        Assert.Equal(0, set.GearWithheld);
    }

    /// <summary>The who rule's version: the eight drop where the page names no creature.</summary>
    [Fact]
    public void AReachableUpgradeRankedNinthSurvivesEightTheWhoRuleWithholds()
    {
        var set = Rank(Gear([Helm],
            new ItemCatalog([.. EightAhead("Unrest", named: false),
                Record("Zz Bronze Helm", "HEAD", 10, ["Lower Guk"])])));

        Assert.Empty(set.GearAnchorsRemoved);
        Assert.Equal("Lower Guk", Assert.Single(set.Top).Zone);
        Assert.Equal(GearUpgrades.MaxPerAnchor, set.GearWhoWithheld);
        Assert.Equal(0, set.GearWithheld);
    }

    // ---- and the cap still caps -----------------------------------------------------------

    /// <summary>
    /// **THE CAP STILL CAPS, AND ONLY COUNTS SURVIVORS.** Twelve reachable candidates and five
    /// refused ones: eight are shown, four held back — never nine, which is what counting the
    /// refused five against the cap would have produced. The rows' weights are the SHOWN count,
    /// so a candidate the cap held back cannot set a row's bar.
    /// </summary>
    [Fact]
    public void TheCapCountsOnlyWhatTheGatesLetThrough()
    {
        var reachable = Enumerable.Range(1, GearUpgrades.MaxPerAnchor + 4)
            .Select(i => Record($"Mm Bronze Helm {i:00}", "HEAD", 10 + i, ["Lower Guk"]));
        var refused = Enumerable.Range(1, 5)
            .Select(i => Record($"Aa Velium Helm {i:00}", "HEAD", 30 + i, ["Kael Drakkel"]));

        var set = Rank(Gear([Helm], new ItemCatalog([.. refused, .. reachable]),
            eras: Eras(("Kael Drakkel", "Velious"), ("Lower Guk", "Classic")),
            world: "Classic"));

        Assert.Equal(17, set.GearCandidates);
        Assert.Equal(4, set.GearWithheld);
        var row = Assert.Single(set.Top);
        Assert.Equal("Lower Guk", row.Zone);
        Assert.Equal(GearUpgrades.MaxPerAnchor, row.Why.OfType<GearUpgradeFact>().Count()
            + row.WithheldWhy);
    }

    /// <summary>The cap is one rule in one place: the sweep's own capped answer and
    /// <see cref="GearUpgrades.CapPerAnchor"/> over its uncapped answer are the same list.</summary>
    [Fact]
    public void TheSweepsCapAndCapPerAnchorAreOneRule()
    {
        var catalog = new ItemCatalog(Enumerable.Range(1, GearUpgrades.MaxPerAnchor + 3)
            .Select(i => Record($"helm {i:00}", "HEAD", 4 + i, ["Lower Guk"])));
        WornItem[] worn = [Helm, Worn("Rusty Cap", "HEAD", 5)];

        var capped = GearUpgrades.Sweep(GearIntent.UpgradeWorn, worn, [], catalog, [], false);
        var all = GearUpgrades.Sweep(
            GearIntent.UpgradeWorn, worn, [], catalog, [], false, GearUpgrades.Uncapped);
        var recapped = GearUpgrades.CapPerAnchor(all.Upgrades, GearUpgrades.MaxPerAnchor, out var held);

        Assert.Equal(0, all.Withheld);
        Assert.Equal(capped.Upgrades.Select(u => (u.Over, u.Slot, u.Item)),
            recapped.Select(u => (u.Over, u.Slot, u.Item)));
        Assert.Equal(capped.Withheld, held);
        Assert.True(held > 0);
    }
}
