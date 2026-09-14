namespace EQBuddy.Core;

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
    /// </summary>
    public static bool Dominates(
        string bName, ItemStatsBlock? b, string aName, ItemStatsBlock? a,
        IReadOnlyList<string> myClasses)
    {
        if (b is not { } bs || a is not { } asb) return false;
        if (bs.Classes.Count > 0 && myClasses is { Count: > 0 }
            && !bs.Classes.Intersect(myClasses, StringComparer.OrdinalIgnoreCase).Any())
            return false;   // the better item is for somebody else's class
        if (bName.Equals(aName, StringComparison.OrdinalIgnoreCase)) return false;

        var strictly = false;
        foreach (var (_, bv, av) in MetricPairs(bs, asb))
        {
            if (bv < av) return false;
            if (bv > av) strictly = true;
        }
        return strictly;
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
    /// <para><b>It is the rule the Helper's catalog sweep keeps too</b>, and there it matters
    /// more than it does in the Locker: the Locker is comparing two things the player can see
    /// side by side, while the Helper is asking them to go and spend an evening.</para>
    /// </summary>
    public static bool CanClaimUpgrade(
        string candidateName, ItemStatsBlock? candidate,
        string wornName, ItemStatsBlock? worn, IReadOnlyList<string> myClasses) =>
        Dominates(candidateName, candidate, wornName, worn, myClasses)
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
    /// </summary>
    public static (string Metric, double By)? Gain(ItemStatsBlock candidate, ItemStatsBlock worn)
    {
        (string Metric, double By)? best = null;
        foreach (var (metric, b, a) in MetricPairs(candidate, worn))
            if (b > a && (best is null || b - a > best.Value.By)) best = (metric, b - a);
        return best;
    }
}
