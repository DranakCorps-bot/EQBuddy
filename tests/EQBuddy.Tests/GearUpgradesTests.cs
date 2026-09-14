using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE CATALOG DOMINANCE SWEEP** (DRA-71 D6, Fable plan P8; Founder smoke items 4a/4b).
///
/// <para>The rows here are organised around the refusals rather than around the happy path,
/// because the refusals are what keep this off the "best in slot" side of the line the Gear
/// Locker has held since #104: an empty slot is never answered about, a "+N" is never told to
/// unequip itself, somebody else's class is never offered, and a quest reward never appears
/// unless the player asked for quest rewards.</para>
/// </summary>
public class GearUpgradesTests
{
    // ---- fixtures ----------------------------------------------------------------------

    private static ItemStatsBlock Block(params string[] lines) => ItemStatsBlock.Parse(lines);

    private static WornItem Worn(string name, string slot, params string[] lines) =>
        new(name, QuestCatalog.BaseItemName(name), slot,
            Block([$"Slot: {slot}", .. lines]));

    private static ItemCatalog.Record Record(
        string name, string slot, int ac,
        string[]? zones = null, string[]? quests = null, string[]? classes = null,
        Dictionary<string, List<string>>? mobs = null) =>
        new()
        {
            Name = name,
            StatsText = $"Slot: {slot}\nAC: {ac}",
            Slots = [slot],
            Ac = ac,
            Classes = classes?.ToList(),
            DropZones = zones?.ToList(),
            Quests = quests?.ToList(),
            DropMobs = mobs,
        };

    private static ItemCatalog Catalog(params ItemCatalog.Record[] records) => new(records);

    private static GearSweep Sweep(
        ItemCatalog catalog, IReadOnlyList<WornItem> worn,
        GearIntent intent = GearIntent.UpgradeWorn,
        IReadOnlyList<string>? picks = null,
        IReadOnlyList<string>? classes = null,
        bool includeQuests = false) =>
        GearUpgrades.Sweep(intent, worn, picks ?? [], catalog, classes ?? [], includeQuests);

    // ---- the happy path, so the refusals below are not vacuous (trap 78) ----------------

    /// <summary>A catalog item that beats what you wear comes back, with the zone it drops in
    /// and the item it beats named — an upgrade with no anchor would be a claim about the
    /// game.</summary>
    [Fact]
    public void ACatalogItemThatBeatsAWornItemIsOfferedWithItsZoneAndItsAnchor()
    {
        var sweep = Sweep(
            Catalog(Record("Froglok Bone Helm", "HEAD", 9, zones: ["Lower Guk"])),
            [Worn("Rusty Helm", "HEAD", "AC: 4")]);

        var upgrade = Assert.Single(sweep.Upgrades);
        Assert.Equal("Froglok Bone Helm", upgrade.Item);
        Assert.Equal("Rusty Helm", upgrade.Over);
        Assert.Equal("HEAD", upgrade.Slot);
        Assert.Equal(["Lower Guk"], upgrade.Zones);
        Assert.Equal("AC", upgrade.GainMetric);
        Assert.Equal(5, upgrade.GainBy);
        Assert.Equal(0, sweep.Withheld);
    }

    // ---- the refusals -------------------------------------------------------------------

    /// <summary>
    /// **AN EMPTY SLOT IS NEVER ANSWERED ABOUT**, and this is the row that keeps the whole
    /// feature off the BiS side of the line.
    ///
    /// <para>"The best thing for a slot you have nothing in" is the obvious next feature and
    /// it is exactly the claim the Locker's lock forbids: with no anchor, dominance is vacuous
    /// and EQBuddy would be ranking the game's items against each other.</para>
    /// </summary>
    [Fact]
    public void ASlotWithNothingInItYieldsNothingEvenWhenTheCatalogIsFullOfThings()
    {
        var sweep = Sweep(
            Catalog(
                Record("Fine Cloak", "BACK", 12, zones: ["Lower Guk"]),
                Record("Finer Cloak", "BACK", 20, zones: ["Lower Guk"])),
            [Worn("Rusty Helm", "HEAD", "AC: 4")]);

        Assert.Empty(sweep.Upgrades);
    }

    /// <summary>A worn "+N" is under-described rather than beaten — telling somebody to farm a
    /// replacement for their best item is worse than saying nothing
    /// (<see cref="ItemDominance.CanClaimUpgrade"/>).</summary>
    [Fact]
    public void AWornUpgradeTierIsNeverToldToUnequipItself()
    {
        var catalog = Catalog(Record("Froglok Bone Helm", "HEAD", 9, zones: ["Lower Guk"]));

        Assert.Empty(Sweep(catalog, [Worn("Rusty Helm +5", "HEAD", "AC: 4")]).Upgrades);
        // …and the same comparison with a plain worn item DOES fire, so the row above is the
        // tier rule rather than a fixture that could never have matched.
        Assert.Single(Sweep(catalog, [Worn("Rusty Helm", "HEAD", "AC: 4")]).Upgrades);
    }

    /// <summary>Somebody else's class-locked item is not an upgrade for you. An UNKNOWN class
    /// filters nothing, which is the conservative half — hiding a real upgrade is worse than
    /// showing one the player will recognise as not theirs.</summary>
    [Fact]
    public void AClassLockedItemIsOfferedOnlyToAClassThatCanWearIt()
    {
        var catalog = Catalog(
            Record("Paladin Helm", "HEAD", 20, zones: ["Lower Guk"], classes: ["PAL"]));
        var worn = new[] { Worn("Rusty Helm", "HEAD", "AC: 4") };

        Assert.Empty(Sweep(catalog, worn, classes: ["WAR"]).Upgrades);
        Assert.Single(Sweep(catalog, worn, classes: ["PAL"]).Upgrades);
        Assert.Single(Sweep(catalog, worn, classes: []).Upgrades);
    }

    /// <summary>An item with no drop zone and only a quest behind it is not offered until the
    /// player asks for quest rewards — farming a camp and running a quest chain are different
    /// evenings, which is why the Founder asked for the toggle.</summary>
    [Fact]
    public void AQuestOnlyItemArrivesOnlyBehindTheToggle()
    {
        var catalog = Catalog(
            Record("Blessed Helm", "HEAD", 15, quests: ["The Blessing of Erollisi"]));
        var worn = new[] { Worn("Rusty Helm", "HEAD", "AC: 4") };

        Assert.Empty(Sweep(catalog, worn).Upgrades);

        var on = Assert.Single(Sweep(catalog, worn, includeQuests: true).Upgrades);
        Assert.Equal(["The Blessing of Erollisi"], on.Quests);
        Assert.Empty(on.Zones);
    }

    /// <summary>An item that BOTH drops and is a quest reward stays farmable with the toggle
    /// off — dropping it for carrying a quest line too would hide a real camp. Its quest list
    /// is empty until the toggle is on, so no row can name a quest the player did not ask
    /// about.</summary>
    [Fact]
    public void AnItemThatDropsAndIsAlsoAQuestRewardStaysFarmableWithTheToggleOff()
    {
        var catalog = Catalog(
            Record("Blessed Helm", "HEAD", 15, zones: ["Lower Guk"], quests: ["A Quest"]));
        var worn = new[] { Worn("Rusty Helm", "HEAD", "AC: 4") };

        var off = Assert.Single(Sweep(catalog, worn).Upgrades);
        Assert.Equal(["Lower Guk"], off.Zones);
        Assert.Empty(off.Quests);

        var on = Assert.Single(Sweep(catalog, worn, includeQuests: true).Upgrades);
        Assert.Equal(["A Quest"], on.Quests);
    }

    /// <summary>The item you are WEARING is in the catalog too, and is never offered as an
    /// upgrade over itself.</summary>
    [Fact]
    public void TheWornItemIsNeverOfferedAsAnUpgradeOverItself() =>
        Assert.Empty(Sweep(
            Catalog(Record("Rusty Helm", "HEAD", 4, zones: ["Lower Guk"])),
            [Worn("Rusty Helm", "HEAD", "AC: 4")]).Upgrades);

    /// <summary>The DEFERRED intent sweeps nothing at all rather than quietly returning an
    /// empty list that the room would draw as "nothing beats your gear".</summary>
    [Fact]
    public void TheDeferredIntentSweepsNothing()
    {
        var sweep = Sweep(
            Catalog(Record("Froglok Bone Helm", "HEAD", 9, zones: ["Lower Guk"])),
            [Worn("Rusty Helm", "HEAD", "AC: 4")],
            intent: GearIntent.FarmToSell);

        Assert.Same(GearSweep.Nothing, sweep);
        Assert.Equal(GearIntentShape.Deferred, GearUpgrades.ShapeFor(GearIntent.FarmToSell));
    }

    // ---- the two intents anchor differently, which is the whole difference ---------------

    /// <summary>
    /// **THE ONE OBSERVABLE DIFFERENCE BETWEEN 4a AND 4b.**
    ///
    /// <para>"Upgrade what I wear" reads the picks; "replace with better" anchors on every
    /// worn slot and does not. A fixture with a pick that names ONE of two worn items is the
    /// only arrangement in which the two intents can be told apart, and the same picks are
    /// handed to both runs so the difference cannot be the argument.</para>
    /// </summary>
    [Fact]
    public void UpgradeWornReadsThePicksAndReplaceWithBetterIgnoresThem()
    {
        var catalog = Catalog(
            Record("Froglok Bone Helm", "HEAD", 9, zones: ["Lower Guk"]),
            Record("Fine Cloak", "BACK", 12, zones: ["Befallen"]));
        WornItem[] worn =
        [
            Worn("Rusty Helm", "HEAD", "AC: 4"),
            Worn("Tattered Cloak", "BACK", "AC: 2"),
        ];
        string[] picks = ["Rusty Helm"];

        var picked = Sweep(catalog, worn, GearIntent.UpgradeWorn, picks);
        Assert.Equal(["Froglok Bone Helm"], picked.Upgrades.Select(u => u.Item));

        var everything = Sweep(catalog, worn, GearIntent.ReplaceSlot, picks);
        Assert.Equal(
            ["Fine Cloak", "Froglok Bone Helm"],
            everything.Upgrades.Select(u => u.Item).Order().ToArray());
    }

    /// <summary>Nothing picked means ALL of them — filter semantics, the same reading
    /// <c>UnlockPicks</c> has and the deliberate opposite of the faction picker's.</summary>
    [Fact]
    public void NoWornPickMeansEveryWornItem()
    {
        var sweep = Sweep(
            Catalog(
                Record("Froglok Bone Helm", "HEAD", 9, zones: ["Lower Guk"]),
                Record("Fine Cloak", "BACK", 12, zones: ["Befallen"])),
            [Worn("Rusty Helm", "HEAD", "AC: 4"), Worn("Tattered Cloak", "BACK", "AC: 2")]);

        Assert.Equal(2, sweep.Upgrades.Count);
    }

    /// <summary>A pick naming something you are no longer wearing narrows NOTHING rather than
    /// emptying the answer — the same per-section rule <c>UnlockPickStore.Narrow</c> keeps, and
    /// for the same reason: no control on screen could explain the empty.</summary>
    [Fact]
    public void AStalePickNarrowsNothing()
    {
        var sweep = Sweep(
            Catalog(Record("Froglok Bone Helm", "HEAD", 9, zones: ["Lower Guk"])),
            [Worn("Rusty Helm", "HEAD", "AC: 4")],
            picks: ["Helm I Sold Last Week"]);

        Assert.Single(sweep.Upgrades);
    }

    // ---- the caps say what they held back (trap 50) --------------------------------------

    /// <summary>The per-anchor cap trims and REPORTS. The count is what the room draws beside
    /// the answers, because it is spent before any row exists.</summary>
    [Fact]
    public void ThePerAnchorCapReportsWhatItHeldBack()
    {
        var records = Enumerable.Range(1, GearUpgrades.MaxPerAnchor + 4)
            .Select(i => Record($"helm {i:00}", "HEAD", 4 + i, zones: ["Lower Guk"]))
            .ToArray();

        var sweep = Sweep(Catalog(records), [Worn("Rusty Helm", "HEAD", "AC: 4")]);

        Assert.Equal(GearUpgrades.MaxPerAnchor, sweep.Upgrades.Count);
        Assert.Equal(4, sweep.Withheld);
    }

    /// <summary>Ordered by how many NUMBERS improved, which is arithmetic — "which upgrade is
    /// better for your character" has no answer in this repo and none is invented.</summary>
    [Fact]
    public void UpgradesAreOrderedByHowManyNumbersImproved()
    {
        var many = new ItemCatalog.Record
        {
            Name = "Statted Helm", StatsText = "", Slots = ["HEAD"], Ac = 5,
            Hp = 10, Attributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                { ["STR"] = 5 },
            DropZones = ["Lower Guk"],
        };
        var one = Record("Plain Better Helm", "HEAD", 9, zones: ["Lower Guk"]);

        var sweep = Sweep(Catalog(one, many), [Worn("Rusty Helm", "HEAD", "AC: 4")]);

        Assert.Equal("Statted Helm", sweep.Upgrades[0].Item);
        Assert.Equal(3, sweep.Upgrades[0].ImprovedMetrics);
        Assert.Equal(1, sweep.Upgrades[1].ImprovedMetrics);
    }

    // ---- DropMobs ------------------------------------------------------------------------

    /// <summary>The creatures ride through where the catalog has them, keyed by zone — and a
    /// zone the page named nobody under answers EMPTY rather than a guess (trap 73).</summary>
    [Fact]
    public void NamedCreaturesRideThroughPerZoneAndAnUnnamedZoneAnswersEmpty()
    {
        var sweep = Sweep(
            Catalog(Record("Froglok Bone Helm", "HEAD", 9,
                zones: ["Lower Guk", "Befallen"],
                mobs: new() { ["Lower Guk"] = ["a froglok shaman"] })),
            [Worn("Rusty Helm", "HEAD", "AC: 4")]);

        var upgrade = Assert.Single(sweep.Upgrades);
        Assert.Equal(["a froglok shaman"], upgrade.MobsIn("Lower Guk"));
        Assert.Empty(upgrade.MobsIn("Befallen"));
        Assert.Empty(upgrade.MobsIn("a zone that is not on this item"));
    }

    // ---- WornFrom ------------------------------------------------------------------------

    /// <summary>
    /// **"Is this worn" is <c>InventoryFile.Entry.Worn</c> and nothing else.**
    ///
    /// <para>The shared bank is the arrangement that has already gone wrong once in this repo:
    /// a `SharedBank1` row ranked as worn gear and labelled itself "worn · SharedBank1". Bags,
    /// containers and both banks are excluded here through the SAME property the Gear Locker
    /// ranks locations with.</para>
    /// </summary>
    [Fact]
    public void WornFromTakesTheCharacterAndNotTheBagsOrEitherBank()
    {
        InventoryFile.Entry[] entries =
        [
            new("Head", "Rusty Helm", 1),
            new("General1", "Spare Helm", 1),
            new("General1-Slot3", "Bagged Helm", 1),
            new("Bank2", "Banked Helm", 1),
            new("SharedBank1", "Shared Helm", 1),
        ];

        var worn = GearUpgrades.WornFrom(entries,
            _ => ItemStatsBlock.Parse(["Slot: HEAD", "AC: 4"]));

        Assert.Equal(["Rusty Helm"], worn.Select(w => w.Name));
        Assert.Equal("HEAD", worn[0].Slot);
    }

    /// <summary>An item the catalog cannot describe is dropped rather than carried with no
    /// stats — an anchor that could never win or lose a comparison would read as "no upgrades
    /// exist" instead of "EQBuddy has never read about this". The same for something with a
    /// stats block and no slot in it: a spell scroll in a worn row is not gear.</summary>
    [Fact]
    public void AnItemWithNoStatsOrNoSlotIsNotAnAnchor()
    {
        Assert.Empty(GearUpgrades.WornFrom([new("Head", "Mystery Helm", 1)], _ => null));
        Assert.Empty(GearUpgrades.WornFrom(
            [new("Head", "Scroll", 1)], _ => ItemStatsBlock.Parse(["MAGIC ITEM"])));
    }

    /// <summary>A ring in two FINGER rows is ONE anchor: the dump prints two locations and the
    /// upgrade for both is the same upgrade.</summary>
    [Fact]
    public void OneItemInTwoIdenticalSlotsIsOneAnchor()
    {
        var worn = GearUpgrades.WornFrom(
            [new("Finger", "Plain Band", 1), new("Finger2", "Plain Band", 1)],
            _ => ItemStatsBlock.Parse(["Slot: FINGER", "AC: 1"]));

        Assert.Single(worn);
    }

    /// <summary>An item that goes in TWO DIFFERENT slots is two anchors — the anchor is the
    /// slot as much as the item, and a one-handed weapon that can go in either hand is being
    /// compared against two different things.</summary>
    [Fact]
    public void OneItemInTwoDifferentSlotsIsTwoAnchors()
    {
        var worn = GearUpgrades.WornFrom(
            [new("Primary", "Short Sword", 1)],
            _ => ItemStatsBlock.Parse(["Slot: PRIMARY SECONDARY", "DMG: 8", "Atk Delay: 24"]));

        Assert.Equal(["PRIMARY", "SECONDARY"], worn.Select(w => w.Slot));
    }

    // ---- the store -----------------------------------------------------------------------

    /// <summary>Reader and writer land together (trap 20), and the default is the Founder's
    /// own first intent rather than an absent one.</summary>
    [Fact]
    public void TheIntentRoundTripsAndDefaultsToUpgradeWorn()
    {
        var settings = new AppSettings();

        Assert.Equal(GearIntent.UpgradeWorn, GearIntentStore.Intent(settings, "erollisi|Dranak"));

        GearIntentStore.Choose(settings, "erollisi|Dranak", GearIntent.ReplaceSlot);
        Assert.Equal(GearIntent.ReplaceSlot, GearIntentStore.Intent(settings, "erollisi|Dranak"));
        // Another character is untouched — the selection is per character, as every Helper
        // selection beside it is.
        Assert.Equal(GearIntent.UpgradeWorn, GearIntentStore.Intent(settings, "erollisi|Alt"));

        // Back to the default REMOVES the key: "never chose" and "chose the default" are one
        // state, and two spellings of one state is a distinction a later reader would act on.
        GearIntentStore.Choose(settings, "erollisi|Dranak", GearIntent.UpgradeWorn);
        Assert.Empty(settings.HelperGearIntent);
    }

    /// <summary>A stored name nobody recognises falls back to the default rather than leaving
    /// the strip with nothing selected.</summary>
    [Fact]
    public void AnUnknownStoredIntentFallsBackToTheDefault()
    {
        var settings = new AppSettings();
        settings.HelperGearIntent["erollisi|Dranak"] = "FarmForGlory";

        Assert.Equal(GearUpgrades.DefaultIntent,
            GearIntentStore.Intent(settings, "erollisi|Dranak"));
    }

    /// <summary>The worn picks toggle both ways and an empty result removes the key.</summary>
    [Fact]
    public void WornPicksToggleBothWaysAndAnEmptyResultRemovesTheKey()
    {
        var settings = new AppSettings();

        GearIntentStore.ToggleWorn(settings, "erollisi|Dranak", "Rusty Helm");
        Assert.Equal(["Rusty Helm"], GearIntentStore.WornPicks(settings, "erollisi|Dranak"));

        // Case-insensitive, because the file that spells an item and the file that stores the
        // pick are written by different programs.
        GearIntentStore.ToggleWorn(settings, "erollisi|Dranak", "RUSTY HELM");
        Assert.Empty(GearIntentStore.WornPicks(settings, "erollisi|Dranak"));
        Assert.Empty(settings.HelperWornPicks);
    }

    /// <summary>The quest toggle is off until it is turned on, and off REMOVES the key.</summary>
    [Fact]
    public void TheQuestToggleIsOffByDefaultAndOffRemovesTheKey()
    {
        var settings = new AppSettings();

        Assert.False(GearIntentStore.IncludeQuests(settings, "erollisi|Dranak"));

        GearIntentStore.ToggleQuests(settings, "erollisi|Dranak");
        Assert.True(GearIntentStore.IncludeQuests(settings, "erollisi|Dranak"));

        GearIntentStore.ToggleQuests(settings, "erollisi|Dranak");
        Assert.False(GearIntentStore.IncludeQuests(settings, "erollisi|Dranak"));
        Assert.Empty(settings.HelperGearQuests);
    }

    /// <summary>An empty character key writes nothing — the state a first launch is in before
    /// the log has named anybody, and a write keyed on "" would be one profile's selections
    /// arriving on everybody's.</summary>
    [Fact]
    public void AnEmptyCharacterKeyWritesNothing()
    {
        var settings = new AppSettings();

        GearIntentStore.Choose(settings, "", GearIntent.ReplaceSlot);
        GearIntentStore.ToggleWorn(settings, "", "Rusty Helm");
        GearIntentStore.ToggleQuests(settings, "");

        Assert.Empty(settings.HelperGearIntent);
        Assert.Empty(settings.HelperWornPicks);
        Assert.Empty(settings.HelperGearQuests);
    }
}
