using EQBuddy.Core;
using EQBuddy.UI.Shared;
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

    /// <summary>
    /// **THE TIER RULE, SPLIT BY WHERE ITS PREMISE IS TRUE** (DRA-149 D1, plan P1 — this test
    /// REWORKED rather than deleted).
    ///
    /// <para>It used to assert that the sweep refuses a plussed worn item outright, and it
    /// passed for a reason that was not the one written above it. The rule compares
    /// <c>UpgradeTier(candidate) &gt;= UpgradeTier(worn)</c>, and <b>no catalog name carries a
    /// "+N"</b> — so the candidate's tier is always 0 and the test is <c>0 &gt;= N</c>, false
    /// for every plussed item in the game. That is not a careful refusal of a bad
    /// recommendation; it is an answer fixed before the inputs are read, and it made the whole
    /// feature return nothing for any character whose gear is plussed at all.</para>
    ///
    /// <para>So the LOCKER keeps the rule, where both names come off one dump and the premise
    /// holds, and the SWEEP drops to base-vs-base and narrows what its rows may claim to
    /// match. Both halves are asserted here, in one place, because the interesting fact is
    /// that they now DIFFER on the same pair of items and that the difference is deliberate.
    /// The prove-fail is restoring the tier gate in <c>GearUpgrades.Sweep</c>: the sweep half
    /// goes red.</para>
    /// </summary>
    [Fact]
    public void AWornUpgradeTierIsNeverToldToUnequipItself()
    {
        var catalog = Catalog(Record("Froglok Bone Helm", "HEAD", 9, zones: ["Lower Guk"]));

        // THE SWEEP'S HALF, FLIPPED: a better base item is offered against a plussed worn one,
        // because the alternative is offering nothing at all — which is what the Founder hit.
        var plussed = Assert.Single(Sweep(catalog, [Worn("Rusty Helm +5", "HEAD", "AC: 4")]).Upgrades);
        Assert.Equal("Froglok Bone Helm", plussed.Item);
        // …and the plain worn item still answers too, so the row above is the policy change
        // rather than a fixture that matches anything put in front of it.
        Assert.Single(Sweep(catalog, [Worn("Rusty Helm", "HEAD", "AC: 4")]).Upgrades);

        // THE LOCKER'S HALF, UNCHANGED: the same pair, asked the Locker's question, is still
        // refused. Its premise is intact there — both names carry the dump's "+N" — and its
        // never-BiS scope lock is untouched by this slice.
        var candidate = new ItemStatsBlock { Ac = 9, Slots = ["HEAD"] };
        var worn = new ItemStatsBlock { Ac = 4, Slots = ["HEAD"] };
        Assert.False(ItemDominance.CanClaimUpgrade(
            "Froglok Bone Helm", candidate, "Rusty Helm +5", worn, []));
        Assert.True(ItemDominance.CanClaimUpgrade(
            "Froglok Bone Helm", candidate, "Rusty Helm", worn, []));
        // The arithmetic both sides read is one table, and it says the same thing to both.
        Assert.True(ItemDominance.Dominates(
            "Froglok Bone Helm", candidate, "Rusty Helm +5", worn, []));
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

    /// <summary>
    /// **"FARM TO SELL" SWEEPS NOTHING, AND NOW THAT IT HAS AN ENGINE THAT IS THE POINT**
    /// (DRA-71 D7).
    ///
    /// <para>Until D7 this asserted a DEFERRAL: the intent had no engine, so an empty sweep was
    /// the honest state. It is Answered now — by <c>Recommendations.FarmToSell</c>, whose anchor
    /// is the player's own loot — and this file still refuses it, which is the assertion that
    /// matters. Every candidate here has a worn item behind it; an intent that cannot supply one
    /// would produce the game's items ranked against each other, the single claim this class
    /// exists not to make. The shape and the sweep are asserted TOGETHER so a later edit cannot
    /// let a third intent into the dominance comparison by flipping one of them.</para>
    /// </summary>
    [Fact]
    public void FarmToSellIsAnsweredElsewhereAndSweepsNothingHere()
    {
        var sweep = Sweep(
            Catalog(Record("Froglok Bone Helm", "HEAD", 9, zones: ["Lower Guk"])),
            [Worn("Rusty Helm", "HEAD", "AC: 4")],
            intent: GearIntent.FarmToSell);

        Assert.Same(GearSweep.Nothing, sweep);
        Assert.Equal(GearIntentShape.Answered, GearUpgrades.ShapeFor(GearIntent.FarmToSell));
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

    /// <summary>A ring in two FINGER rows is ONE anchor: the dump prints "Finger" and
    /// "Finger2", both of which are the FINGER slot, and the upgrade for the two of them is
    /// the same upgrade. The trailing ordinal is an index, not a kind of slot.</summary>
    [Fact]
    public void OneItemInTwoIdenticalSlotsIsOneAnchor()
    {
        var worn = GearUpgrades.WornFrom(
            [new("Finger", "Plain Band", 1), new("Finger2", "Plain Band", 1)],
            _ => ItemStatsBlock.Parse(["Slot: FINGER", "AC: 1"]));

        Assert.Single(worn);
        Assert.Equal("FINGER", worn[0].Slot);
    }

    /// <summary>…but TWO DIFFERENT rings in those two rows are two anchors. Both are FINGER
    /// and each is its own item to beat — the de-duplication is on (item, slot) and not on
    /// the slot alone, or a character with two rings could only ever be offered one
    /// upgrade.</summary>
    [Fact]
    public void TwoDifferentRingsInTheTwoFingerRowsAreTwoAnchors()
    {
        var worn = GearUpgrades.WornFrom(
            [new("Finger", "Plain Band", 1), new("Finger2", "Gold Band", 1)],
            _ => ItemStatsBlock.Parse(["Slot: FINGER", "AC: 1"]));

        Assert.Equal(["Plain Band", "Gold Band"], worn.Select(w => w.Name));
        Assert.All(worn, w => Assert.Equal("FINGER", w.Slot));
    }

    /// <summary>
    /// **THE FOUNDER'S SMOKE, first half: a one-handed weapon in the primary hand is ONE
    /// anchor, not a PRIMARY and a SECONDARY** (DRA-81).
    ///
    /// <para>This used to expand the CATALOG's <c>Slot:</c> line, which for 821 of the
    /// shipped records reads "PRIMARY SECONDARY" — a statement about where the item MAY go.
    /// So one sword produced two anchors, the worn picker listed the same item twice with
    /// different detail lines, and the sweep compared it against the whole secondary index
    /// as though the character were dual-wielding it. The dump prints where it actually is,
    /// once, per row.</para>
    /// </summary>
    [Fact]
    public void AOneHandedWeaponInThePrimaryHandIsOneAnchorAndNotTwo()
    {
        var worn = GearUpgrades.WornFrom(
            [new("Primary", "Short Sword", 1)],
            _ => ItemStatsBlock.Parse(["Slot: PRIMARY SECONDARY", "DMG: 8", "Atk Delay: 24"]));

        Assert.Equal(["PRIMARY"], worn.Select(w => w.Slot));
        Assert.Single(worn);
    }

    /// <summary>…and the same weapon worn in BOTH hands is two anchors, one per hand —
    /// which is the case the old expansion was accidentally right about, and the reason the
    /// assertion above is not just "always one".</summary>
    [Fact]
    public void TheSameWeaponInBothHandsIsOneAnchorPerHand()
    {
        var worn = GearUpgrades.WornFrom(
            [new("Primary", "Short Sword", 1), new("Secondary", "Short Sword", 1)],
            _ => ItemStatsBlock.Parse(["Slot: PRIMARY SECONDARY", "DMG: 8", "Atk Delay: 24"]));

        Assert.Equal(["PRIMARY", "SECONDARY"], worn.Select(w => w.Slot));
    }

    /// <summary>
    /// **THE FOUNDER'S SMOKE, second half: the RANGE row is there** (DRA-81) — and it is the
    /// same bug as the one above, seen from the other side.
    ///
    /// <para>124 shipped catalog records name RANGE beside PRIMARY, SECONDARY or AMMO,
    /// because a wiki page lists every slot an item is legal in. Expanding that line anchored
    /// a worn bow as a hand weapon and never produced a RANGE anchor at all, so the Range row
    /// the Founder went looking for could not appear however many bows they equipped. Reading
    /// the dump's location gives exactly one anchor and it is the right one.</para>
    /// </summary>
    [Fact]
    public void ABowWornInTheRangeSlotAnchorsOnRange()
    {
        var worn = GearUpgrades.WornFrom(
            [new("Range", "Willow Bow", 1)],
            _ => ItemStatsBlock.Parse(
                ["Slot: RANGE PRIMARY SECONDARY", "DMG: 6", "Atk Delay: 30"]));

        Assert.Equal(["RANGE"], worn.Select(w => w.Slot));
        Assert.DoesNotContain("PRIMARY", worn.Select(w => w.Slot));
    }

    /// <summary>The slot is the dump's word, upper-cased, so it lands in
    /// <c>GearLocker.SlotOrder</c>'s vocabulary — which is what the Helper's picker sorts its
    /// rows by, and a slot that sorted last because it was spelled "Range" would read as a
    /// missing row rather than a misplaced one.</summary>
    [Theory]
    [InlineData("Range", "RANGE")]
    [InlineData("Ear2", "EAR")]
    [InlineData("Wrist2", "WRIST")]
    [InlineData("Charm", "CHARM")]
    [InlineData("Shoulders", "SHOULDERS")]
    public void TheAnchorsSlotIsTheDumpsLocationInTheLockersVocabulary(string location, string slot)
    {
        var worn = GearUpgrades.WornFrom(
            [new(location, "Some Thing", 1)],
            _ => ItemStatsBlock.Parse(["Slot: CHEST", "AC: 4"]));

        Assert.Equal([slot], worn.Select(w => w.Slot));
        Assert.Contains(slot, GearLocker.SlotOrder);
    }

    /// <summary>
    /// **THE WORN PICKER, as the Founder sees it**: a realistic character sheet gives ONE row
    /// per worn item, the Range row is in it, and no item appears twice.
    ///
    /// <para>The Helper's picker keys its rows on the item NAME
    /// (<c>HelperRoom.BuildWornPicker</c>), so two anchors for one item are two rows wearing
    /// the same label — which is the duplicate the Founder counted. It sorts by
    /// <c>GearLocker.SlotOrder</c>, so a slot that never became an anchor is a row that is
    /// simply not on the list. Both symptoms are assertions about this one list, which is why
    /// they are asserted together: the per-shape tests above each prove a rule, and this
    /// proves the rules add up to the screen.</para>
    ///
    /// <para>Every catalog block here is one a real page would carry — the bow legal in three
    /// slots, the sword legal in two, the ring legal in one — so the fixture is the situation
    /// rather than a reduction of it (trap 23).</para>
    /// </summary>
    [Fact]
    public void ARealCharacterSheetGivesOneRowPerWornItemIncludingRange()
    {
        InventoryFile.Entry[] sheet =
        [
            new("Primary", "Short Sword", 1),
            new("Secondary", "Wooden Shield", 1),
            new("Range", "Willow Bow", 1),
            new("Finger", "Plain Band", 1),
            new("Finger2", "Gold Band", 1),
            new("Chest", "Bronze Breastplate", 1),
            new("General1", "Spare Sword", 1),        // a bag row is not worn
        ];

        var worn = GearUpgrades.WornFrom(sheet, name => name switch
        {
            "Short Sword" => ItemStatsBlock.Parse(["Slot: PRIMARY SECONDARY", "DMG: 8", "Atk Delay: 24"]),
            "Wooden Shield" => ItemStatsBlock.Parse(["Slot: SECONDARY", "AC: 9"]),
            "Willow Bow" => ItemStatsBlock.Parse(["Slot: RANGE PRIMARY SECONDARY", "DMG: 6", "Atk Delay: 30"]),
            "Plain Band" or "Gold Band" => ItemStatsBlock.Parse(["Slot: FINGER", "AC: 1"]),
            "Bronze Breastplate" => ItemStatsBlock.Parse(["Slot: CHEST", "AC: 20"]),
            _ => ItemStatsBlock.Parse(["Slot: PRIMARY", "DMG: 2", "Atk Delay: 30"]),
        });

        // One row per worn row of the dump, and the bag row is not one of them.
        Assert.Equal(6, worn.Count);
        // The picker's own key: no label appears twice. Before DRA-81 "Short Sword" and
        // "Willow Bow" each produced two or three rows.
        Assert.Equal(worn.Count, worn.Select(w => w.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // …and each is anchored where it is actually worn.
        Assert.Equal("PRIMARY", worn.Single(w => w.Name == "Short Sword").Slot);
        Assert.Equal("SECONDARY", worn.Single(w => w.Name == "Wooden Shield").Slot);
        Assert.Equal("RANGE", worn.Single(w => w.Name == "Willow Bow").Slot);

        // THE ROW THE FOUNDER WENT LOOKING FOR, in the order the picker draws it.
        var rows = worn
            .OrderBy(w => Array.IndexOf(GearLocker.SlotOrder, w.Slot) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(w => w.Name, StringComparer.OrdinalIgnoreCase)
            .Select(w => w.Slot)
            .ToList();
        Assert.Contains("RANGE", rows);
        Assert.Equal(["PRIMARY", "SECONDARY", "RANGE", "CHEST", "FINGER", "FINGER"], rows);
        // Nothing sorted last for want of a known slot — an unknown spelling is how a row
        // goes missing at the bottom of a list rather than loudly.
        Assert.All(worn, w => Assert.Contains(w.Slot, GearLocker.SlotOrder));
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
