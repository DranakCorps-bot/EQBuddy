namespace EQBuddy.Core;

/// <summary>
/// What one comparison came to — <b>a VALUE, so a refusal can be counted</b> (DRA-222 D6, S7.3).
///
/// <para><see cref="ItemDominance.Dominates"/> has always answered a bool, which is the right
/// shape for "may I say this" and the wrong one for "why not". The off-hand rule REMOVES offers
/// a player would otherwise see, and a rule that removes rows says so out loud and by count
/// (trap 50, and the band gate's own arrangement in <c>Recommendations.GearBandRefusals</c>) —
/// which is impossible if the refusal and "it simply loses on AC" arrive as the same
/// <c>false</c>.</para>
/// </summary>
public enum DominanceVerdict
{
    /// <summary>It does not win: it loses on a number either block carries, it is locked to
    /// somebody else's class, or it is the same item.</summary>
    No,

    /// <summary>It wins on every number either block carries, and costs nothing the table
    /// cannot see.</summary>
    Yes,

    /// <summary>
    /// **It WOULD win on the numbers, and taking it empties the off-hand.**
    ///
    /// <para>The candidate is two-handed, the worn item is not, and the character has something
    /// in SECONDARY. All three are facts somebody wrote down — the wiki's <c>2H</c> prefix and
    /// the player's own inventory dump — and none of them is a number, which is exactly why
    /// this could not be a low score.</para>
    /// </summary>
    CostsTheOffHand,
}

/// <summary>
/// **THE ONE METRIC TABLE** — is this item better than that one, and may EQBuddy say so.
///
/// <para><b>This is <c>GearLocker</c>'s comparison, lifted into Core without a single number
/// changing</b> (DRA-71 D6, Fable plan P8). The Locker has owned "B outclasses A" since #104
/// and still does — <c>GearLocker.Dominates</c>, <c>CanClaimUpgrade</c> and
/// <c>UpgradeTier</c> are now three one-line calls into this file. The reason for the move is
/// the plan's own wording: the Helper's Farm Gear sweep widens the comparison from your bags
/// to the shipped <see cref="ItemCatalog"/>, <i>"with the same metric table"</i>, and "the
/// same" is only true if there is exactly one of it (trap 4). A second copy in Core would
/// have agreed with the Locker on the day it was written and drifted the first time either
/// side learned about a new stat.</para>
///
/// <para><b>Why Core and not beside the Locker.</b> <c>GearLocker</c> lives in
/// <c>UI.Shared</c>, which Core cannot reference, and the engine that needs this is
/// <see cref="Recommendations"/> — in Core, so that the phone calls the same ranker the
/// desktop does. Lifting the arithmetic down is the only arrangement in which both callers
/// read one table.</para>
///
/// <para><b>It compares NUMBERS and it decides nothing about the world.</b> Stats are the
/// wiki's BASE values, absent numbers count as zero on both sides, and a "+N" suffix raises
/// an item in-game by an amount the wiki does not state — which is why
/// <see cref="CanClaimUpgrade"/> exists as a second, stricter question. Nothing here knows
/// what a player should wear, and nothing here ranks an item against the GAME; it answers
/// only "does this one win on every number both of them carry".</para>
///
/// <para><b>…AND THE SENTENCE ABOVE IT WAS NOT TRUE OF THE CODE UNTIL DRA-222 D6.</b> The
/// claim that the Locker's three members are calls into this file has been in this summary and
/// in <c>CLAUDE.md</c> since DRA-71 D6, and <c>GearLocker</c> was still carrying its own
/// private copy of <see cref="MetricPairs"/> and its own <c>Dominates</c>. They agreed, which
/// is why nothing caught it and why it is worth a line here: two implementations of one table
/// agree right up until one of them learns something (trap 4), and D6 is that day — the
/// off-hand rule below would have reached the Helper's sweep and left the Gear room telling
/// the same player the same greatsword outclassed the sword in his hand. The delegation is now
/// real, and <c>ItemDominanceTests</c> runs both surfaces over one table.</para>
///
/// <para><b>ONE COST IT NOW PRICES, AND ONE IT REFUSES TO.</b> <see cref="Compare"/> knows that
/// a two-handed weapon forecloses the off-hand (S7.3) — a fact the wiki writes in the block's
/// own <c>Skill:</c> line and the player's dump confirms, not a number. It still knows nothing
/// about what a stat is WORTH: <see cref="ClassStatRelevance"/> can tell it which numbers a
/// class's own gear carries, and that only ever chooses which true sentence to say
/// (<see cref="Gain"/>) and which of two true rows to put first.</para>
/// </summary>
public static class ItemDominance
{
    /// <summary>
    /// Does <paramref name="bName"/> outclass <paramref name="aName"/> outright?
    ///
    /// <para>B wins only when it is at least as good on EVERY number either of them carries
    /// and strictly better on one — and B is actually usable (not class-locked away from the
    /// player, when their classes are known). Absent numbers count as zero on both sides:
    /// honest for additive stats, and the reason this is conservative by design.</para>
    ///
    /// <para>Two items with the same name never dominate each other. The Locker needs that
    /// because a bag can hold three of one thing; the catalog sweep needs it because a worn
    /// item is IN the catalog and would otherwise be recommended as an upgrade over
    /// itself.</para>
    ///
    /// <para><b>Since DRA-222 D6 it is one line over <see cref="Compare"/></b>, which is where
    /// the off-hand rule lives. A caller that needs to COUNT what the rule removed asks
    /// <see cref="Compare"/>; a caller that only needs the answer asks this. One producer, two
    /// questions (trap 4).</para>
    /// </summary>
    public static bool Dominates(
        string bName, ItemStatsBlock? b, string aName, ItemStatsBlock? a,
        IReadOnlyList<string> myClasses, bool offHandInUse = false) =>
        Compare(bName, b, aName, a, myClasses, offHandInUse) == DominanceVerdict.Yes;

    /// <summary>
    /// **THE COMPARISON, WITH ITS REASON** (DRA-222 D6, S7.3).
    ///
    /// <para>The numbers first, then the one cost the numbers cannot carry. The order matters
    /// and is deliberate: <see cref="DominanceVerdict.CostsTheOffHand"/> is only ever reached
    /// by a candidate that WOULD have been offered, so the count a surface prints is "offers
    /// this rule took from you" and not "two-handed weapons in the catalog". Asking the hand
    /// question first would have counted all 441 of the shipped catalog's wearable two-handers
    /// against a warrior's sword instead of the 29 that actually beat the Founder's morning
    /// star.</para>
    /// </summary>
    /// <param name="offHandInUse">Whether the character has something in SECONDARY, from their
    /// own inventory dump. <b>False stands the rule down entirely</b>, which is the answer for
    /// a caller that cannot prove it (no dump, a bare stats comparison, a fixture): a hand
    /// nobody has measured must not remove a real upgrade (trap 73). It is a FACT and not a
    /// proxy for one — "the worn item is one-handed" would have been the convenient reading and
    /// it is wrong for an empty off-hand, where a two-hander costs nothing at all (trap 64b).</param>
    public static DominanceVerdict Compare(
        string bName, ItemStatsBlock? b, string aName, ItemStatsBlock? a,
        IReadOnlyList<string> myClasses, bool offHandInUse = false)
    {
        if (b is not { } bs || a is not { } asb) return DominanceVerdict.No;
        if (bs.Classes.Count > 0 && myClasses is { Count: > 0 }
            && !bs.Classes.Intersect(myClasses, StringComparer.OrdinalIgnoreCase).Any())
            return DominanceVerdict.No;   // the better item is for somebody else's class
        if (bName.Equals(aName, StringComparison.OrdinalIgnoreCase)) return DominanceVerdict.No;

        var strictly = false;
        foreach (var (_, bv, av) in MetricPairs(bs, asb))
        {
            if (bv < av) return DominanceVerdict.No;
            if (bv > av) strictly = true;
        }
        if (!strictly) return DominanceVerdict.No;

        // **THE ONE COST THE TABLE ABOVE CANNOT PRICE.** A two-hander forecloses the off-hand;
        // no entry in MetricPairs is about a slot. The rule fires on the wiki's own `2H` prefix
        // and on a SECONDARY the player's own dump shows occupied, and it does NOT fire when
        // the worn item is already two-handed — that hand is spent either way, so swapping one
        // greatsword for a better one costs nothing new.
        if (offHandInUse
            && WeaponSkills.Hands(bs) == WeaponHands.Two
            && WeaponSkills.Hands(asb) != WeaponHands.Two)
            return DominanceVerdict.CostsTheOffHand;

        return DominanceVerdict.Yes;
    }

    /// <summary>
    /// Is <paramref name="candidateName"/> honestly a swap-in over what is worn?
    ///
    /// <para>Dominance plus one refusal. Stats are wiki BASE values, and a "+N" raises an item
    /// in-game by an amount the wiki does not state — so a worn "+9" whose base numbers lose
    /// is not beaten, it is merely under-described. Claiming otherwise would tell a player to
    /// unequip their best item, which is worse than saying nothing. A candidate at the same
    /// tier or higher has no such excuse against it.</para>
    ///
    /// <para><b>THE HELPER'S CATALOG SWEEP NO LONGER ASKS THIS, AND THE REASON IS THE
    /// PREMISE</b> (DRA-149 D1, plan P1). This rule assumes both names can carry a tier, which
    /// is true in the Locker — both come off one dump — and false on the catalog side, where
    /// <b>0 of 11,196 names end in "+N"</b>. There <see cref="UpgradeTier"/> returns 0 for every
    /// candidate, so the test read <c>0 &gt;= 6</c> for any plussed worn item and the sweep
    /// returned nothing for 19 of the Founder's 19 gear anchors. A refusal that cannot be
    /// satisfied is not conservative, it is silent. <c>GearUpgrades.Sweep</c> asks
    /// <see cref="Dominates"/> instead and narrows what its rows may CLAIM to match; the TIER
    /// half of this method is untouched and stays the Locker's, whose scope lock is
    /// untouched.</para>
    /// </summary>
    /// <param name="offHandInUse">Passed through to <see cref="Compare"/>, so the Locker's
    /// "⬆ upgrade over X" and the Helper's row cannot disagree about a greatsword (DRA-222
    /// D6).</param>
    public static bool CanClaimUpgrade(
        string candidateName, ItemStatsBlock? candidate,
        string wornName, ItemStatsBlock? worn, IReadOnlyList<string> myClasses,
        bool offHandInUse = false) =>
        Dominates(candidateName, candidate, wornName, worn, myClasses, offHandInUse)
        && UpgradeTier(candidateName) >= UpgradeTier(wornName);

    /// <summary>The "+N" suffix the dump prints, or 0 for a plain item.</summary>
    public static int UpgradeTier(string name)
    {
        var m = System.Text.RegularExpressions.Regex.Match(name.Trim(), @"\+(\d+)$");
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }

    /// <summary>
    /// Every number both blocks carry, paired. <b>This is the table</b> — the thing there may
    /// only be one of.
    ///
    /// <para>Public because the Helper's row has to be able to say WHICH number improved
    /// (<see cref="Gain"/>), and a second enumeration written for that sentence would be the
    /// same list maintained twice.</para>
    /// </summary>
    public static IEnumerable<(string Metric, double B, double A)> MetricPairs(
        ItemStatsBlock b, ItemStatsBlock a)
    {
        yield return ("AC", b.Ac ?? 0, a.Ac ?? 0);
        yield return ("HP", b.Hp ?? 0, a.Hp ?? 0);
        yield return ("Mana", b.Mana ?? 0, a.Mana ?? 0);
        yield return ("DMG", b.Dmg ?? 0, a.Dmg ?? 0);
        yield return ("ratio", b.Ratio ?? 0, a.Ratio ?? 0);
        foreach (var key in b.Attributes.Keys.Union(a.Attributes.Keys, StringComparer.OrdinalIgnoreCase))
            yield return (key, b.Attributes.GetValueOrDefault(key), a.Attributes.GetValueOrDefault(key));
    }

    /// <summary>
    /// The biggest single improvement, named — "AC", "+12 HP" and so on, as a metric and a
    /// delta rather than as a sentence.
    ///
    /// <para><b>Numbers here, words elsewhere</b> (the same split every Helper fact keeps):
    /// this returns the metric's own key and the difference, and
    /// <c>HelperPresentation</c> decides how to say it. Null when nothing improved, which
    /// <see cref="Dominates"/> has already ruled out for anything it admits — it is here so
    /// the caller cannot be handed a row with no reason on it.</para>
    ///
    /// <para>The LARGEST improvement and not all of them: a row that listed every moved stat
    /// would be an item tooltip, and the Gear room is where an item tooltip belongs.</para>
    ///
    /// <para><b>…AND "LARGEST" IS THE WRONG WORD FOR IT WHEN THE CLASSES ARE KNOWN</b>
    /// (DRA-222 D6, S7.2). The metrics are not on one scale: a helm carrying <c>+12 HP</c> and
    /// <c>+2 WIS</c> is a bigger HP number and a bigger deal for a cleric on the WIS, and
    /// picking the raw maximum meant a wizard's row was as likely to be about STR as about
    /// Mana. So an optional relevance set — <see cref="ClassStatRelevance"/>, which is the
    /// share of the class's own catalog items that carry each number — narrows WHICH
    /// improvements are eligible to be named. <b>It never changes whether a row exists</b>, and
    /// when nothing relevant improved, the largest overall is still named rather than the row
    /// going quiet: a sentence is owed either way.</para>
    /// </summary>
    /// <param name="relevant">The metrics this character's classes' own gear carries, or empty.
    /// <b>Empty is the pre-D6 answer exactly</b> — unknown classes rank on raw size, as they
    /// always did, rather than on a guess at who the character is (trap 73).</param>
    public static (string Metric, double By)? Gain(
        ItemStatsBlock candidate, ItemStatsBlock worn, IReadOnlySet<string>? relevant = null)
    {
        (string Metric, double By)? best = null;
        (string Metric, double By)? bestRelevant = null;
        foreach (var (metric, b, a) in MetricPairs(candidate, worn))
        {
            if (b <= a) continue;
            if (best is null || b - a > best.Value.By) best = (metric, b - a);
            if (relevant is { Count: > 0 } && relevant.Contains(metric)
                && (bestRelevant is null || b - a > bestRelevant.Value.By))
                bestRelevant = (metric, b - a);
        }
        return bestRelevant ?? best;
    }
}
