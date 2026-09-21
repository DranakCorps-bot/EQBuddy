using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **HOW MANY HANDS, AND WHICH SPELLINGS SAY SO** (DRA-222 D6, S7.3).
///
/// <para>The rule reads the wiki's own <c>2H</c> prefix and nothing else, so the tests that
/// matter are the ones over the SHIPPED catalog: that the prefix actually finds the
/// two-handers, that the spellings it does not admit are enumerated by name rather than
/// silently bucketed (trap 78), and that the admitted set is not empty.</para>
/// </summary>
public class WeaponHandsTests
{
    [Theory]
    // The wiki's own words, all five 2H spellings that reach the catalog.
    [InlineData("2H Slashing", WeaponHands.Two)]
    [InlineData("2H Blunt", WeaponHands.Two)]
    [InlineData("2H Piercing", WeaponHands.Two)]
    [InlineData("1H Slashing", WeaponHands.One)]
    [InlineData("1H Blunt", WeaponHands.One)]
    [InlineData("Piercing", WeaponHands.One)]
    [InlineData("Hand to Hand", WeaponHands.One)]
    [InlineData("Archery", WeaponHands.One)]
    // Promoter debris that means the same hand count as the clean spelling beside it —
    // the prefix admits both without a second table.
    [InlineData("1H Slashing /", WeaponHands.One)]
    [InlineData("1H Slash", WeaponHands.One)]
    [InlineData("Throwing", WeaponHands.One)]
    [InlineData("Throwingv1", WeaponHands.One)]
    [InlineData("Throwingv2", WeaponHands.One)]
    // Case and whitespace: one side is a file and the other is a parse.
    [InlineData("  2h blunt  ", WeaponHands.Two)]
    [InlineData("PIERCING", WeaponHands.One)]
    // No skill line at all is the common case — 5,242 of the catalog's wearable records.
    [InlineData("", WeaponHands.NotAWeapon)]
    [InlineData("   ", WeaponHands.NotAWeapon)]
    [InlineData(null, WeaponHands.NotAWeapon)]
    // A word this rule has not measured is its own answer, and it refuses nothing.
    [InlineData("Alteration", WeaponHands.Unadmitted)]
    [InlineData("SHIELD", WeaponHands.Unadmitted)]
    public void TheSkillWordDecidesTheHandCount(string? skill, WeaponHands expected) =>
        Assert.Equal(expected, WeaponSkills.Hands(skill));

    /// <summary>A whole-string match and not a containment one — a page whose skill word merely
    /// CONTAINS a weapon skill is not that weapon (the <c>Tradeskills.Match</c> rule, and for
    /// its reason).</summary>
    [Theory]
    [InlineData("Piercing Gaze")]
    [InlineData("Archery Manual")]
    public void ASkillWordIsMatchedWholeOrNotAtAll(string skill) =>
        Assert.Equal(WeaponHands.Unadmitted, WeaponSkills.Hands(skill));

    /// <summary>
    /// **THE SHIPPED CATALOG, COUNTED** — the measurement the rule was written from, kept
    /// executable so the day the promoter emits a new skill spelling is a day this suite says
    /// so.
    ///
    /// <para>Floors rather than equalities on the counts, because the catalog is regenerated
    /// weekly from eqlwiki and an exact total would redden on churn that says nothing about the
    /// rule (trap 74). The <b>unadmitted spellings are an exact set</b>, which is the opposite
    /// choice and the deliberate one: that list is the whole guard against a hand weapon being
    /// silently filed as "not a weapon", and today it is two strings long.</para>
    ///
    /// <para><b>Wearable records only</b>, because those are the only ones a comparison can
    /// reach. The five spell schools the promoter lifts off caster items (<c>Alteration</c>,
    /// <c>Evocation</c>, <c>Conjuration</c>, <c>Abjuration</c>, <c>Divination</c> — 29 records)
    /// all sit on pages with no <c>Slot:</c> line at all, which is asserted below so that a
    /// promoter change making one of them wearable reddens here rather than silently reaching
    /// the sweep.</para>
    /// </summary>
    [Fact]
    public void TheCatalogsOwnSkillWordsAreAdmittedOrNamed()
    {
        var byHands = new Dictionary<WeaponHands, int>();
        var unadmitted = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var record in ItemCatalog.Default.All)
        {
            if (record.Slots is not { Count: > 0 }) continue;
            var hands = WeaponSkills.Hands(record.Skill);
            byHands[hands] = byHands.GetValueOrDefault(hands) + 1;
            if (hands == WeaponHands.Unadmitted) unadmitted.Add(record.Skill.Trim());
        }

        // 441 two-handed and 1,159 one-handed wearable records, measured 2026-09-19.
        Assert.True(byHands.GetValueOrDefault(WeaponHands.Two) >= 350,
            $"only {byHands.GetValueOrDefault(WeaponHands.Two)} two-handed records — the `2H` "
            + "prefix has stopped finding the weapons the off-hand rule is about");
        Assert.True(byHands.GetValueOrDefault(WeaponHands.One) >= 900,
            $"only {byHands.GetValueOrDefault(WeaponHands.One)} one-handed records");
        Assert.True(byHands.GetValueOrDefault(WeaponHands.NotAWeapon) >= 4000);

        // Both are shields, and neither carries a DMG or a Delay — which is why Unadmitted is
        // allowed to refuse nothing.
        Assert.Equal(["SHIELD", "Shield"], unadmitted);
        Assert.All(
            ItemCatalog.Default.All.Where(r =>
                r.Slots is { Count: > 0 }
                && WeaponSkills.Hands(r.Skill) == WeaponHands.Unadmitted),
            r => Assert.True(r.Dmg is null && r.Delay is null,
                $"{r.Name} carries a weapon's numbers under an unadmitted skill word"));

        // The spell schools never reach a comparison, because they are not gear. If a promoter
        // change ever makes one wearable, the exact set above is what says so.
        Assert.All(
            ItemCatalog.Default.All.Where(r =>
                r.Skill is "Alteration" or "Evocation" or "Conjuration"
                        or "Abjuration" or "Divination"),
            r => Assert.Empty(r.Slots));
    }

    /// <summary>
    /// The off-hand slot is asked through <c>GearUpgrades.NormalizeSlot</c>, so the catalog's
    /// own <c>SECONDAY</c> typo and the dump's <c>Secondary</c> reach the same answer. A second
    /// spelling of this word is what makes a rule stand down on exactly the characters it was
    /// written for.
    /// </summary>
    [Theory]
    [InlineData("Secondary", true)]
    [InlineData("SECONDARY", true)]
    [InlineData("SECONDAY", true)]
    [InlineData("secondary ", true)]
    [InlineData("PRIMARY", false)]
    [InlineData("RANGE", false)]
    // The Founder's shield is an "Any Slot" row, and it is deliberately NOT an off-hand here:
    // the rule only removes offers, so a hand nobody has measured leaves it stood down.
    [InlineData("Any Slot", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TheOffHandSlotHasOneSpelling(string? slot, bool expected) =>
        Assert.Equal(expected, WeaponSkills.IsOffHand(slot));
}
