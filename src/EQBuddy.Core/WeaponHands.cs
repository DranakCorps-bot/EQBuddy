namespace EQBuddy.Core;

/// <summary>
/// How many hands an item's own <c>Skill:</c> line says it takes — <b>the one fact the metric
/// table cannot price</b> (DRA-222 D6, S7.3).
///
/// <para>Four values because four things are true of a stats block, and none of them is a
/// guess. <see cref="Unadmitted"/> is the one that exists so a spelling nobody has measured
/// cannot be silently filed under one of the other three (trap 78: a rule aimed at nothing is
/// green).</para>
/// </summary>
public enum WeaponHands
{
    /// <summary>The block carries no <c>Skill:</c> line at all. A helm, a ring, a shield with
    /// no skill word — 5,242 of the shipped catalog's 6,844 wearable records.</summary>
    NotAWeapon,

    /// <summary>A weapon whose skill word does not begin <c>2H</c>. 1,159 records.</summary>
    One,

    /// <summary><b>The wiki's own <c>2H</c> prefix.</b> 441 records — and the reason this file
    /// exists. Equipping one forecloses the off-hand, which is a cost no number in
    /// <see cref="ItemDominance.MetricPairs"/> carries.</summary>
    Two,

    /// <summary>
    /// A <c>Skill:</c> word this rule does not admit — <b>reported, never bucketed</b>.
    ///
    /// <para>TWO records in the shipped catalog's wearable half, named one by one in
    /// <c>WeaponHandsTests</c>: <c>SHIELD</c> and <c>Shield</c>, one each, and both carry
    /// neither <c>Dmg</c> nor <c>Delay</c>. (A further 29 records carry a spell school —
    /// <c>Alteration</c> 12, <c>Evocation</c> 6, <c>Conjuration</c> 5, <c>Abjuration</c> 4,
    /// <c>Divination</c> 2 — and every one of them has no <c>Slot:</c> line at all, so no
    /// comparison in this repo can ever reach them.)</para>
    ///
    /// <para><b>It does not refuse anything</b>, deliberately — the off-hand rule fires on
    /// <see cref="Two"/> and on nothing else, so a spelling nobody has read cannot remove a row
    /// the player would want. The guard is the test that names the exact set: the day the
    /// promoter emits a third, that suite says so rather than a surface quietly changing.</para>
    /// </summary>
    Unadmitted,
}

/// <summary>
/// **THE ONE READER OF A STATS BLOCK'S <c>Skill:</c> LINE** (DRA-222 D6, S7.3).
///
/// <para>Two surfaces compare gear — the Gear Locker over your bags and the Helper's catalog
/// sweep over the game — and both were handed a metric table that prices AC, HP, Mana, damage,
/// ratio and every attribute, and prices no SLOT. So a two-handed weapon that wins on every
/// one of those numbers "beats" the one-hander you are wielding, and taking the advice empties
/// the hand your shield or your second weapon is in. The numbers were right and the claim was
/// wrong.</para>
///
/// <para>Of the shipped catalog's 6,844 wearable records, 441 are two-handed, 1,159 are
/// one-handed, 5,242 carry no skill word at all and 2 carry one this rule does not admit.</para>
///
/// <para><b>Measured on the Founder's own committed dump, against the shipped catalog:</b> his
/// PRIMARY anchor (<c>Enchanted Fine Steel Morning Star +6</c>, <c>1H Blunt</c>, and he wields
/// a second one in SECONDARY) produced <b>64 dominating candidates, 29 of them two-handed</b>
/// — and because <c>GearUpgrades.MaxPerAnchor</c> is 8, the eight rows he actually saw were
/// <b>seven two-handers and one one-hander</b>. Seven of the eight offers for his weapon hand
/// cost him his off-hand, and the sentence under each said only that it was a better base
/// item.</para>
///
/// <para><b>The rule reads the wiki's own word and invents nothing.</b> Two-handedness is the
/// <c>2H</c> prefix the item block itself prints (<c>2H Slashing</c>, <c>2H Blunt</c>,
/// <c>2H Piercing</c>) — not a lookup table of weapon names, not a weight, not an inference
/// from delay. Everything else that carries a skill word this file admits is
/// <see cref="WeaponHands.One"/>; a skill word it does not admit is
/// <see cref="WeaponHands.Unadmitted"/> and decides nothing.</para>
///
/// <para><b>What is deliberately NOT modelled, because the block does not say it.</b> A bow is
/// held in two hands in the game and its skill word is <c>Archery</c>, with nothing in the
/// stats block distinguishing it from a dagger on this question. Archery and Throwing also sit
/// in RANGE rather than PRIMARY, so the off-hand question is not the one a RANGE row asks. So
/// they read as <see cref="WeaponHands.One"/> — which is the permissive answer, and the right
/// one for a rule that only ever REMOVES rows: refusing on a hand count nobody has measured
/// would hide real upgrades on a guess (trap 73).</para>
/// </summary>
public static class WeaponSkills
{
    /// <summary>Skill words that are a weapon and take one hand, exactly as the wiki spells
    /// them. Matched whole-string, case-insensitively — the same shape
    /// <c>Tradeskills.Match</c> keeps, and for its reason: a containment test would file
    /// <c>Hand to Hand Combat Manual</c> as a weapon.</summary>
    private static readonly HashSet<string> OneHanded = new(StringComparer.OrdinalIgnoreCase)
    {
        "Piercing", "Hand to Hand", "Archery",
    };

    /// <summary>How many hands this item takes, from its skill word alone.</summary>
    public static WeaponHands Hands(ItemStatsBlock? stats) => Hands(stats?.Skill);

    /// <summary>
    /// How many hands the skill word names.
    ///
    /// <para>The <c>1H</c> and <c>2H</c> arms are PREFIXES rather than a list, which is what
    /// admits the shipped catalog's own debris without a second table: <c>1H Slashing /</c> and
    /// <c>1H Slash</c> are both real spellings on real pages, and both mean the same hand
    /// count as <c>1H Slashing</c>. It is also what makes the rule survive a weapon skill
    /// nobody has seen yet — a <c>2H Bashing</c> arriving next week is caught by the word the
    /// wiki already uses, not by a list somebody has to remember to grow.</para>
    ///
    /// <para><c>Throwing</c> is a prefix too, and that one is not elegance: the catalog carries
    /// <c>Throwing</c>, <c>Throwingv1</c> and <c>Throwingv2</c>, 38 records between them.</para>
    /// </summary>
    public static WeaponHands Hands(string? skill)
    {
        var word = skill?.Trim() ?? "";
        if (word.Length == 0) return WeaponHands.NotAWeapon;
        if (word.StartsWith("2H", StringComparison.OrdinalIgnoreCase)) return WeaponHands.Two;
        if (word.StartsWith("1H", StringComparison.OrdinalIgnoreCase)) return WeaponHands.One;
        if (word.StartsWith("Throwing", StringComparison.OrdinalIgnoreCase)) return WeaponHands.One;
        return OneHanded.Contains(word) ? WeaponHands.One : WeaponHands.Unadmitted;
    }

    /// <summary>
    /// **THE SLOT THE OFF-HAND RULE IS ABOUT**, spelled once.
    ///
    /// <para>Through <see cref="GearUpgrades.NormalizeSlot"/> rather than against a literal, so
    /// the catalog's own <c>SECONDAY</c> typo and the dump's <c>Secondary</c> both answer the
    /// same question — a second spelling of this word is the bug that makes a rule stand down
    /// on exactly the characters it was written for.</para>
    /// </summary>
    public static bool IsOffHand(string? slot) =>
        GearUpgrades.NormalizeSlot(slot) is "SECONDARY";
}
