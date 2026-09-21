using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The Founder's committed inventory dump, resolved the way the Helper room resolves
/// it. Shared by the D6 suites so the off-hand rule and the relevance rule are measured against
/// the same character rather than against two paraphrases of him.</summary>
internal static class FounderFixture
{
    public static List<InventoryFile.Entry> Entries() =>
        InventoryFile.ParseEntries(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "inventory", "dranak.txt")));

    public static List<WornItem> Worn() =>
        [.. GearUpgrades.WornFrom(
            Entries(), n => ItemCatalog.Default.Find(n)?.ToStatsBlock()).Worn];

    /// <summary>His PRIMARY anchor — a 1H Blunt morning star, with a second one in his
    /// SECONDARY hand, which is what makes every two-handed offer cost him something.</summary>
    public static WornItem PrimaryHand() =>
        Worn().Single(w => GearUpgrades.NormalizeSlot(w.Slot) == "PRIMARY");
}

/// <summary>
/// **THE OFF-HAND THE NUMBERS COULD NOT SEE** (DRA-222 D6, S7.3).
///
/// <para>Every rule in the metric table prices a number on the item. None of them prices a
/// SLOT — so a two-handed weapon that wins on damage, ratio and every attribute "beat" the
/// one-hander in the player's hand, and the row said so in a sentence that was true about the
/// numbers and wrong about the swap. The unit rows below prove the rule one condition at a
/// time; the fixture row is the one that says it mattered.</para>
/// </summary>
public class GearUpgradesWeaponTests
{
    private static ItemStatsBlock Block(params string[] lines) => ItemStatsBlock.Parse(lines);

    private static readonly ItemStatsBlock OneHander =
        Block("Slot: PRIMARY", "Skill: 1H Blunt", "DMG: 7", "Atk Delay: 30");
    private static readonly ItemStatsBlock TwoHander =
        Block("Slot: PRIMARY", "Skill: 2H Slashing", "DMG: 20", "Atk Delay: 40", "STR: +5");
    private static readonly ItemStatsBlock BetterOneHander =
        Block("Slot: PRIMARY", "Skill: 1H Slashing", "DMG: 12", "Atk Delay: 30", "STR: +5");

    /// <summary>
    /// **THE REFUSAL, AND ITS THREE CONDITIONS** — each one driven on its own, because a rule
    /// that fired on any two of them would pass a single end-to-end check.
    /// </summary>
    [Fact]
    public void ATwoHanderIsRefusedOnlyWhenItActuallyCostsTheOffHand()
    {
        // It wins on the numbers — that is what makes this a refusal rather than a loss.
        Assert.Equal(DominanceVerdict.Yes,
            ItemDominance.Compare("greatsword", TwoHander, "morning star", OneHander, []));

        // …and with the off-hand occupied, it is refused, with its reason.
        Assert.Equal(DominanceVerdict.CostsTheOffHand,
            ItemDominance.Compare("greatsword", TwoHander, "morning star", OneHander, [],
                offHandInUse: true));

        // A one-hander over a one-hander is untouched: no slot is spent.
        Assert.Equal(DominanceVerdict.Yes,
            ItemDominance.Compare("sword", BetterOneHander, "morning star", OneHander, [],
                offHandInUse: true));

        // A two-hander over a two-hander is untouched too — that hand is already spent, so
        // swapping one greatsword for a better one costs nothing new.
        var betterTwoHander = Block(
            "Slot: PRIMARY", "Skill: 2H Slashing", "DMG: 30", "Atk Delay: 40", "STR: +9");
        Assert.Equal(DominanceVerdict.Yes,
            ItemDominance.Compare("better greatsword", betterTwoHander, "greatsword", TwoHander,
                [], offHandInUse: true));

        // And a candidate that loses on the numbers is still just No — the refusal must never
        // become the answer for something that was not being offered anyway, or the count a
        // surface prints stops meaning "offers this rule took from you".
        Assert.Equal(DominanceVerdict.No,
            ItemDominance.Compare("morning star", OneHander, "greatsword", TwoHander, [],
                offHandInUse: true));
    }

    /// <summary>
    /// **THE OFF-HAND IS A FACT, NOT A PROXY** (trap 64b). The convenient reading — "the worn
    /// item is one-handed, so a two-hander costs a slot" — is wrong for the player whose
    /// off-hand is EMPTY, where a greatsword costs nothing at all. So the rule takes the
    /// occupancy as its own parameter, and a caller that cannot prove it stands the rule down.
    /// </summary>
    [Fact]
    public void AnEmptyOffHandCostsNothingToFill() =>
        Assert.Equal(DominanceVerdict.Yes,
            ItemDominance.Compare("greatsword", TwoHander, "morning star", OneHander, [],
                offHandInUse: false));

    /// <summary>A skill word the rule does not admit refuses nothing — both such records in the
    /// shipped catalog are shields carrying no weapon numbers at all, and a spelling nobody has
    /// measured must not take a row off the player's screen (trap 73).</summary>
    [Fact]
    public void AnUnadmittedSkillWordRefusesNothing()
    {
        var odd = Block("Slot: PRIMARY", "Skill: SHIELD", "DMG: 20", "Atk Delay: 40", "STR: +5");
        Assert.Equal(WeaponHands.Unadmitted, WeaponSkills.Hands(odd));
        Assert.Equal(DominanceVerdict.Yes,
            ItemDominance.Compare("odd", odd, "morning star", OneHander, [], offHandInUse: true));
    }

    /// <summary>
    /// **THE SWEEP READS THE CHARACTER'S OFF-HAND, NOT THE ANCHOR'S.**
    ///
    /// <para>The anchors may have been narrowed to the player's picks, and a player who picked
    /// only their helm has not emptied their shield hand by doing so. So the fact is taken off
    /// the whole worn sheet — which is also what makes this rule survive the
    /// <c>UpgradeWorn</c> / <c>ReplaceSlot</c> split without a second reading.</para>
    /// </summary>
    [Fact]
    public void TheSweepAsksTheWholeWornSheetEvenWhenThePicksNarrowIt()
    {
        var catalog = new ItemCatalog([
            new ItemCatalog.Record
            {
                Name = "Greatsword", Slots = ["PRIMARY"], Skill = "2H Slashing",
                Dmg = 20, Delay = 40, Attributes = new() { ["STR"] = 5 },
                DropZones = ["Somewhere"],
            },
        ]);
        var worn = new List<WornItem>
        {
            new("Morning Star +6", "Morning Star", "PRIMARY", OneHander),
            new("Buckler +1", "Buckler", "SECONDARY", Block("Slot: SECONDARY", "AC: 9")),
        };

        // Picked the primary hand only — the secondary row is not an anchor, and is still the
        // reason the offer is refused.
        var sweep = GearUpgrades.Sweep(
            GearIntent.UpgradeWorn, worn, ["Morning Star +6"], catalog, [], includeQuests: false);
        Assert.Empty(sweep.Upgrades);
        Assert.Equal(1, sweep.OffHandRefusals);

        // The same character with an empty off-hand gets the offer, which is what proves the
        // assertion above is about the shield and not about the weapon.
        var oneHanded = GearUpgrades.Sweep(
            GearIntent.UpgradeWorn, [worn[0]], [], catalog, [], includeQuests: false);
        Assert.Single(oneHanded.Upgrades);
        Assert.Equal(0, oneHanded.OffHandRefusals);
    }

    /// <summary>
    /// **THE FOUNDER'S OWN WEAPON HAND, AGAINST THE SHIPPED CATALOG.**
    ///
    /// <para>He wields <c>Enchanted Fine Steel Morning Star +6</c> in both hands. Measured
    /// 2026-09-19, before the rule: his PRIMARY anchor produced <b>64 dominating candidates, 29
    /// of them two-handed</b> — and because <see cref="GearUpgrades.MaxPerAnchor"/> is 8, the
    /// eight rows he could actually see were <b>seven two-handers and one one-hander</b>. Seven
    /// of the eight offers for his weapon hand cost him his off-hand, silently.</para>
    ///
    /// <para>After it: 35 survive, the eight shown are all one-handed, and the 29 are reported
    /// rather than dropped (trap 50). Floors rather than equalities on the counts, because the
    /// catalog is regenerated weekly (trap 74) — but <b>zero two-handers among the rows drawn
    /// is an equality</b>, because that one is the rule itself and churn cannot excuse it.</para>
    /// </summary>
    [Fact]
    public void TheFoundersWeaponHandIsNoLongerOfferedSevenGreatswords()
    {
        var anchor = FounderFixture.PrimaryHand();
        Assert.Equal(WeaponHands.One, WeaponSkills.Hands(anchor.Stats));
        Assert.Contains(FounderFixture.Worn(), w => WeaponSkills.IsOffHand(w.Slot));

        var sweep = GearUpgrades.Sweep(
            GearIntent.UpgradeWorn, FounderFixture.Worn(), [anchor.Name],
            ItemCatalog.Default, [], includeQuests: false);

        var forHand = sweep.Upgrades
            .Where(u => GearUpgrades.NormalizeSlot(u.Slot) == "PRIMARY").ToList();
        Assert.NotEmpty(forHand);

        // THE RULE. Not one row he is shown for his weapon hand may be two-handed.
        var offered = forHand
            .Select(u => ItemCatalog.Default.Find(u.Item)?.ToStatsBlock())
            .Select(WeaponSkills.Hands)
            .ToList();
        Assert.DoesNotContain(WeaponHands.Two, offered);

        // …and the 29 it removed are counted rather than dropped. The anchor is picked, so
        // every refusal in this sweep is his weapon hand's.
        Assert.True(sweep.OffHandRefusals >= 20,
            $"only {sweep.OffHandRefusals} two-handed offers were refused for the Founder's "
            + "primary hand — 29 were measured, and a number this low means the rule has "
            + "stopped reaching the corpus it was written for");

        // The feature is not merely quieter: he is still offered real upgrades.
        Assert.True(forHand.Count >= 5,
            $"the off-hand rule left only {forHand.Count} rows for his weapon hand");
    }
}
