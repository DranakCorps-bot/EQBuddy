using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Gear Locker (#104): stats-block parsing, slot grouping, and the dominance
/// rule. "Outclassed" must be arithmetic — at least as good on every number both
/// items carry, strictly better on one — and class-locked items never outclass
/// anything the player can't trade them for.
/// </summary>
public class GearLockerTests
{
    // ---- the stats-block parser ----

    [Fact]
    public void ParsesAWeaponBlock()
    {
        var s = ItemStatsBlock.Parse(
        [
            "MAGIC ITEM LORE ITEM NO DROP",
            "Slot: PRIMARY",
            "Skill: 1H Slashing Atk Delay: 26",
            "DMG: 20",
            "STR: +15 WIS: +15",
            "WT: 3.0 Size: MEDIUM",
            "Class: PAL",
            "Race: ALL",
        ]);
        Assert.Equal(["PRIMARY"], s.Slots);
        Assert.Equal(20, s.Dmg);
        Assert.Equal(26, s.Delay);
        Assert.Equal(0.77, s.Ratio!.Value, 2);
        Assert.Equal("1H Slashing", s.Skill);
        Assert.Equal(15, s.Attributes["STR"]);
        Assert.Equal(["PAL"], s.Classes);
        Assert.True(s.Magic);
        Assert.True(s.Lore);
        Assert.True(s.NoDrop);
        Assert.True(s.Wearable);
    }

    [Fact]
    public void ParsesArmorAndMultiSlotJewelry()
    {
        var s = ItemStatsBlock.Parse(
        [
            "Slot: EAR",
            "AC: 4 HP: +25 MANA: +25",
            "SV FIRE: +5 SV COLD: +5",
            "Class: ALL",
        ]);
        Assert.Equal(4, s.Ac);
        Assert.Equal(25, s.Hp);
        Assert.Equal(25, s.Mana);
        Assert.Equal(5, s.Attributes["SV FIRE"]);
        Assert.Empty(s.Classes);            // ALL = unrestricted

        var multi = ItemStatsBlock.Parse(["Slot: PRIMARY SECONDARY"]);
        Assert.Equal(["PRIMARY", "SECONDARY"], multi.Slots);
    }

    [Fact]
    public void AFlagsLineWithoutABreakDoesNotHideTheSlotLine()
    {
        // Real wiki shape (Fine Steel Long Sword): the block opens with a bare
        // "Attunable, Placeable" line separated only by a newline, not <br>. The
        // item-page parser must still yield "Slot: …" as its own line.
        var info = EqlWikiItemService.Parse(
            "<onlyinclude>{{Itempage\n|itemname = Fine Steel Long Sword\n|statsblock  = \n"
            + "Attunable, Placeable\nSlot: PRIMARY SECONDARY<br>\n"
            + "Skill: 1H Slashing  Atk Delay: 28<br>\nDMG: 6 <br>\n}}</onlyinclude>",
            "Fine Steel Long Sword");
        var stats = ItemStatsBlock.Parse(info.StatsLines);
        Assert.Equal(["PRIMARY", "SECONDARY"], stats.Slots);
        Assert.Equal(6, stats.Dmg);
        Assert.True(stats.Wearable);
    }

    [Fact]
    public void ASlotlessItemIsNotWearable()
    {
        var s = ItemStatsBlock.Parse(["QUEST ITEM", "WT: 0.1 Size: SMALL"]);
        Assert.False(s.Wearable);
        Assert.Null(s.Ac);
    }

    // ---- the Locker's grouping and dominance ----

    private static InventoryFile.Entry E(string loc, string name, int count = 1) =>
        new(loc, name, count);

    private static readonly Dictionary<string, ItemStatsBlock> Stats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Rusty Broad Sword"] = ItemStatsBlock.Parse(["Slot: PRIMARY", "Skill: 1H Slashing Atk Delay: 40", "DMG: 4"]),
        ["Fine Steel Long Sword"] = ItemStatsBlock.Parse(["Slot: PRIMARY", "Skill: 1H Slashing Atk Delay: 32", "DMG: 8"]),
        ["Cloth Cap"] = ItemStatsBlock.Parse(["Slot: HEAD", "AC: 2"]),
        ["Bronze Helm"] = ItemStatsBlock.Parse(["Slot: HEAD", "AC: 8", "STR: +2"]),
        ["Sages Circlet"] = ItemStatsBlock.Parse(["Slot: HEAD", "AC: 3", "MANA: +30", "Class: ENC MAG NEC WIZ"]),
        ["Bone Chips"] = ItemStatsBlock.Parse(["QUEST ITEM"]),
    };

    private static List<GearSlotGroup> Build(IReadOnlyList<string> classes, params InventoryFile.Entry[] entries) =>
        GearLocker.Build(entries, n => Stats.GetValueOrDefault(n), classes);

    [Fact]
    public void GroupsBySlotDropsUnwearablesAndFoldsDuplicates()
    {
        var groups = Build([],
            E("Primary", "Fine Steel Long Sword"),
            E("General 1-Slot1", "Rusty Broad Sword"),
            E("General 2-Slot3", "Rusty Broad Sword"),
            E("General 1-Slot2", "Bone Chips", 40));

        var primary = Assert.Single(groups, g => g.Slot == "PRIMARY");
        Assert.Equal(2, primary.Rows.Count);
        var rusty = primary.Rows.Single(r => r.Name == "Rusty Broad Sword");
        Assert.Equal(2, rusty.Count);                       // two bag copies, one row
        Assert.DoesNotContain(groups, g => g.Rows.Any(r => r.Name == "Bone Chips"));
    }

    [Fact]
    public void AStrictlyBetterWeaponOutclassesTheWorse()
    {
        var groups = Build([],
            E("Primary", "Fine Steel Long Sword"),
            E("General 1-Slot1", "Rusty Broad Sword"));

        var rows = groups.Single(g => g.Slot == "PRIMARY").Rows;
        Assert.Equal("", rows.Single(r => r.Name == "Fine Steel Long Sword").OutclassedBy);
        Assert.Equal("Fine Steel Long Sword",
            rows.Single(r => r.Name == "Rusty Broad Sword").OutclassedBy);
    }

    [Fact]
    public void DifferentStrengthsMeanNobodyDominates()
    {
        // Bronze Helm has the AC; Sage's Circlet has the mana. Neither wins on
        // everything, so neither is a dump candidate.
        var groups = Build([],
            E("Head", "Bronze Helm"),
            E("Bank 1", "Sages Circlet"));

        Assert.All(groups.Single(g => g.Slot == "HEAD").Rows,
            r => Assert.Equal("", r.OutclassedBy));
    }

    [Fact]
    public void AClassLockedItemNeverOutclassesForTheWrongClass()
    {
        // The Circlet beats Cloth Cap on every number — but a Warrior can't wear it,
        // so the Cap survives for a Warrior and falls for an Enchanter.
        var forWarrior = Build(["WAR"], E("Head", "Cloth Cap"), E("Bank 1", "Sages Circlet"));
        Assert.Equal("", forWarrior.Single(g => g.Slot == "HEAD")
            .Rows.Single(r => r.Name == "Cloth Cap").OutclassedBy);

        var forEnchanter = Build(["ENC"], E("Head", "Cloth Cap"), E("Bank 1", "Sages Circlet"));
        Assert.Equal("Sages Circlet", forEnchanter.Single(g => g.Slot == "HEAD")
            .Rows.Single(r => r.Name == "Cloth Cap").OutclassedBy);
    }

    [Fact]
    public void UnfetchedItemsLandInTheirOwnHonestGroup()
    {
        var groups = Build([], E("Primary", "Mystery Blade +3"));
        var unknown = Assert.Single(groups, g => g.Slot == "STATS NOT FETCHED YET");
        var row = Assert.Single(unknown.Rows);
        Assert.Equal("Mystery Blade +3", row.Name);
        Assert.Equal("Mystery Blade", row.BaseName);        // +N folds for the wiki key
    }

    [Fact]
    public void WhereLabelsReadLikeThePlayerThinks()
    {
        Assert.Equal("worn · Primary", GearLocker.WhereLabel(E("Primary", "x")));
        Assert.Equal("General 2", GearLocker.WhereLabel(E("General 2-Slot5", "x")));
        Assert.Equal("Bank 3", GearLocker.WhereLabel(E("Bank 3-Slot1", "x")));
    }

    [Fact]
    public void ClassNamesFoldToItemBlockCodes()
    {
        Assert.Equal("PAL", GearLocker.Code("Paladin"));
        Assert.Equal("SHD", GearLocker.Code("Shadow Knight"));
        Assert.Equal("PAL", GearLocker.Code("PAL"));
        Assert.Equal("NEWCLASS", GearLocker.Code("NewClass"));   // unknown passes through
    }

    // ---- "what should I swap in" (#145) ----

    [Fact]
    public void TheWornItemInASlotIsKnown()
    {
        var head = Build([], E("Head", "Cloth Cap"), E("General 1", "Bronze Helm"))
            .Single(g => g.Slot == "HEAD");

        Assert.True(head.Rows.Single(r => r.Name == "Cloth Cap").Worn);
        Assert.False(head.Rows.Single(r => r.Name == "Bronze Helm").Worn);
    }

    [Fact]
    public void ABagItemThatBeatsWhatYouAreWearingIsMarkedAnUpgrade()
    {
        var head = Build([], E("Head", "Cloth Cap"), E("General 1", "Bronze Helm"))
            .Single(g => g.Slot == "HEAD");

        // Bronze Helm is AC 8 STR +2 against a worn AC 2 — better on every number.
        Assert.Equal("Cloth Cap", head.Rows.Single(r => r.Name == "Bronze Helm").UpgradeOver);
        // The worn item never claims to upgrade itself.
        Assert.Equal("", head.Rows.Single(r => r.Name == "Cloth Cap").UpgradeOver);
    }

    [Fact]
    public void NothingIsAnUpgradeWhenTheBetterItemIsAlreadyWorn()
    {
        var head = Build([], E("Head", "Bronze Helm"), E("General 1", "Cloth Cap"))
            .Single(g => g.Slot == "HEAD");

        Assert.All(head.Rows, r => Assert.Equal("", r.UpgradeOver));
    }

    [Fact]
    public void WithNothingWornInTheSlotNoUpgradeIsClaimed()
    {
        // Conservative on purpose: an absent worn row may mean the slot is empty, or
        // may mean the dump didn't say. Neither is evidence that a swap helps.
        var head = Build([], E("General 1", "Cloth Cap"), E("General 2", "Bronze Helm"))
            .Single(g => g.Slot == "HEAD");

        Assert.All(head.Rows, r => Assert.Equal("", r.UpgradeOver));
    }

    [Fact]
    public void AClassLockedItemIsNeverAnUpgradeForSomebodyElse()
    {
        var head = Build(["WAR"], E("Head", "Cloth Cap"), E("General 1", "Sages Circlet"))
            .Single(g => g.Slot == "HEAD");

        Assert.Equal("", head.Rows.Single(r => r.Name == "Sages Circlet").UpgradeOver);
    }

    /// <summary>
    /// The refusal that matters. Wiki stats are BASE values, so a worn "+9" reads as its
    /// unimproved self and loses comparisons it would win in game. Telling a player to
    /// unequip their best item is a worse failure than staying quiet, so a candidate at
    /// a LOWER upgrade tier never claims a swap.
    /// </summary>
    [Fact]
    public void APlainItemNeverClaimsToBeatAWornUpgradedOne()
    {
        var head = Build([], E("Head", "Cloth Cap +9"), E("General 1", "Bronze Helm"))
            .Single(g => g.Slot == "HEAD");

        Assert.Equal("", head.Rows.Single(r => r.Name == "Bronze Helm").UpgradeOver);
    }

    [Fact]
    public void AnEquallyUpgradedItemStillClaimsTheSwap()
    {
        var head = Build([], E("Head", "Cloth Cap +9"), E("General 1", "Bronze Helm +9"))
            .Single(g => g.Slot == "HEAD");

        Assert.Equal("Cloth Cap +9", head.Rows.Single(r => r.Name == "Bronze Helm +9").UpgradeOver);
    }

    [Fact]
    public void UpgradeTierReadsTheDumpsSuffix()
    {
        Assert.Equal(0, GearLocker.UpgradeTier("Bronze Helm"));
        Assert.Equal(9, GearLocker.UpgradeTier("Bronze Helm +9"));
        Assert.Equal(12, GearLocker.UpgradeTier("Bronze Helm +12"));
    }

    // ---- the shared bank IS a bank (#243 track, 2026-09-02) ----

    /// <summary>Both of GearLocker's location rules used to spell "is this in the bank"
    /// as `StartsWith("Bank")` for themselves, and the dump writes the shared bank as
    /// "SharedBank1". So a shared-bank row fell through to rank 0 — the WORN slot, the
    /// most prominent location — and then labelled itself `worn · SharedBank1`: an item
    /// the character is not wearing, presented as the thing they have equipped. The rule
    /// is now <see cref="InventoryFile.Entry.InBank"/>, asked rather than re-spelled.</summary>
    [Fact]
    public void ASharedBankRowIsLabelledAsTheBankAndNotAsWornGear()
    {
        Assert.Equal("SharedBank1", GearLocker.WhereLabel(E("SharedBank1", "Bronze Helm")));
        Assert.Equal("Bank 2", GearLocker.WhereLabel(E("Bank 2", "Bronze Helm")));
        // The one that was already right, kept as the negative: a real worn slot must
        // still say so, or the fix has simply moved the wrongness.
        Assert.Equal("worn · Head", GearLocker.WhereLabel(E("Head", "Bronze Helm")));
    }

    /// <summary>The rank half of the same bug, and the half a label test cannot see: the
    /// locker shows the MOST PROMINENT location for an item held in several places, worn
    /// beating bags beating bank. A shared-bank copy ranking as worn meant the row's whole
    /// "where" answer came from the bank while the character had one on.</summary>
    [Fact]
    public void ASharedBankCopyDoesNotOutrankTheOneActuallyWorn()
    {
        var head = Build([], E("SharedBank1", "Bronze Helm"), E("Head", "Bronze Helm"))
            .Single(g => g.Slot == "HEAD");

        var row = head.Rows.Single(r => r.Name == "Bronze Helm");
        Assert.Equal("worn · Head", row.Where);
        Assert.True(row.Worn);
    }

    [Fact]
    public void InBankKnowsBothSpellingsAndBothDepths()
    {
        Assert.True(new InventoryFile.Entry("SharedBank1", "x", 1).InBank);
        Assert.True(new InventoryFile.Entry("SharedBank1-Slot4", "x", 1).InBank);
        Assert.True(new InventoryFile.Entry("Bank 2", "x", 1).InBank);
        Assert.True(new InventoryFile.Entry("Bank2-Slot1", "x", 1).InBank);
        Assert.False(new InventoryFile.Entry("General 1", "x", 1).InBank);
        Assert.False(new InventoryFile.Entry("Head", "x", 1).InBank);
    }
}
