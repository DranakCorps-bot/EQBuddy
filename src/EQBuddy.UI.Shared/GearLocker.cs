using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One owned item in one slot group of the Locker.</summary>
public sealed record GearRow(
    string Name,                 // as the dump printed it, "+N" and all
    string BaseName,             // wiki name — the stats lookup key
    string Where,                // "worn · Primary" / "General 3" / "Bank 2"
    int Count,
    ItemStatsBlock? Stats,       // null = not in the wiki cache yet
    string StatLine,             // compact "1H Slashing 20/26 (0.77) · STR +15"
    string OutclassedBy,         // "" or the owned item that beats it outright
    string ClassNote)            // "" or "PAL RNG" when the item is class-locked
{
    /// <summary>This is the item currently in that worn slot, not a bag copy.</summary>
    public bool Worn { get; init; }
    /// <summary>"" or the name of the WORN item this one beats outright — the Locker's
    /// answer to "what should I swap in" (discussion #145, skwayb).</summary>
    public string UpgradeOver { get; init; } = "";
}

public sealed record GearSlotGroup(string Slot, List<GearRow> Rows);

/// <summary>
/// The Gear Locker (#104, Techsteps): every wearable you OWN, grouped by the slot it
/// goes in, compared against the other things you own for that slot. The vocabulary
/// is deliberate: "outclassed" means another owned item is at least as good on EVERY
/// number both carry and better on one — a dump candidate by dominance, not taste.
/// Never "BiS": the Locker compares what's in your bags, not what exists in the game.
/// Stats are wiki BASE values; a "+N" raises them in-game by an amount the wiki
/// doesn't state, so upgrades are shown but never folded into comparisons.
///
/// From 1.84.0 it also answers the other half (discussion #145, skwayb): each
/// slot knows which row you are actually WEARING, and a bag item that outright beats
/// it is marked "⬆ upgrade over X". That is the same dominance test pointed the other
/// way — the Locker was already computing everything needed, it just never said which
/// row was on your character. It stays a statement about your own bags, and it still
/// never compares you to another player.
/// </summary>
public static class GearLocker
{
    /// <summary>Slot display order: hands-that-hurt first, then armor head-to-toe,
    /// then jewelry — the order a player mentally walks their character sheet.</summary>
    public static readonly string[] SlotOrder =
    [
        "PRIMARY", "SECONDARY", "RANGE", "AMMO",
        "HEAD", "FACE", "NECK", "SHOULDERS", "ARMS", "WRIST", "HANDS",
        "CHEST", "BACK", "WAIST", "LEGS", "FEET",
        "EAR", "FINGER", "CHARM",
    ];

    public static List<GearSlotGroup> Build(
        IEnumerable<InventoryFile.Entry> entries,
        Func<string, ItemStatsBlock?> statsFor,
        IReadOnlyList<string> myClasses)
    {
        // Fold duplicate names (same item in three bag slots = one row, ×3), keep
        // the most prominent location: worn beats bags beats bank.
        var owned = entries
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var best = g.OrderBy(e => LocationRank(e.Location)).First();
                return (best.Name, Where: WhereLabel(best), Count: g.Sum(e => e.Count),
                        Worn: LocationRank(best.Location) == 0);
            })
            .ToList();

        var rows = new List<(string Slot, GearRow Row)>();
        foreach (var (name, where, count, worn) in owned)
        {
            var baseName = QuestCatalog.BaseItemName(name);
            var stats = statsFor(baseName);
            if (stats is { Wearable: false }) continue;   // scrolls, quest bits, bags
            var classNote = stats is { Classes.Count: > 0 } s ? string.Join(" ", s.Classes) : "";
            var row = new GearRow(name, baseName, where, count, stats,
                stats is null ? "" : StatLine(stats), "", classNote) { Worn = worn };
            if (stats is null)
                rows.Add(("UNKNOWN", row));               // stats not fetched yet
            else
                foreach (var slot in stats.Slots)
                    rows.Add((slot.ToUpperInvariant(), row));
        }

        var groups = new List<GearSlotGroup>();
        foreach (var slot in SlotOrder)
        {
            var inSlot = rows.Where(r => r.Slot == slot).Select(r => r.Row).ToList();
            if (inSlot.Count == 0) continue;
            groups.Add(new GearSlotGroup(slot, MarkOutclassed(inSlot, myClasses)));
        }
        // Slots the order list doesn't know (a new expansion's word) still show —
        // silently dropping a slot reads as a lost item.
        foreach (var slot in rows.Select(r => r.Slot).Distinct()
                     .Where(s => s != "UNKNOWN" && !SlotOrder.Contains(s)).Order())
            groups.Add(new GearSlotGroup(slot,
                MarkOutclassed(rows.Where(r => r.Slot == slot).Select(r => r.Row).ToList(), myClasses)));

        var unknown = rows.Where(r => r.Slot == "UNKNOWN").Select(r => r.Row)
            .DistinctBy(r => r.BaseName, StringComparer.OrdinalIgnoreCase).ToList();
        if (unknown.Count > 0)
            groups.Add(new GearSlotGroup("STATS NOT FETCHED YET", unknown));
        return groups;
    }

    private static List<GearRow> MarkOutclassed(List<GearRow> rows, IReadOnlyList<string> myClasses)
    {
        // Best rows first: by the slot's leading metric (weapon ratio, else AC,
        // else HP), stats-less rows last.
        var ordered = rows
            .OrderByDescending(r => r.Stats?.Ratio ?? 0)
            .ThenByDescending(r => r.Stats?.Ac ?? 0)
            .ThenByDescending(r => r.Stats?.Hp ?? 0)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        // The item actually in this worn slot, if the dump showed one. A slot can hold
        // exactly one thing, so the first worn row IS the answer.
        var wornNow = ordered.FirstOrDefault(r => r.Worn);
        return ordered.Select(r =>
        {
            var beater = ordered.FirstOrDefault(other => !ReferenceEquals(other, r)
                && Dominates(other, r, myClasses));
            var upgrade = wornNow is not null && !r.Worn && CanClaimUpgrade(r, wornNow, myClasses)
                ? wornNow.Name : "";
            return r with { OutclassedBy = beater?.Name ?? "", UpgradeOver = upgrade };
        }).ToList();
    }

    /// <summary>Is <paramref name="candidate"/> honestly a swap-in over what is worn?
    ///
    /// Dominance is the same test the Locker already trusts, plus one refusal that
    /// matters here and not there. Stats are wiki BASE values, and a "+N" raises an
    /// item in-game by an amount the wiki does not state — so a worn "+9" whose base
    /// numbers lose is not beaten, it is merely under-described. Claiming otherwise
    /// would tell a player to unequip their best item, which is worse than saying
    /// nothing. A candidate at the same tier or higher has no such excuse against it.
    /// </summary>
    public static bool CanClaimUpgrade(GearRow candidate, GearRow worn, IReadOnlyList<string> myClasses)
    {
        if (!Dominates(candidate, worn, myClasses)) return false;
        return UpgradeTier(candidate.Name) >= UpgradeTier(worn.Name);
    }

    /// <summary>The "+N" suffix the dump prints, or 0 for a plain item.</summary>
    public static int UpgradeTier(string name)
    {
        var m = System.Text.RegularExpressions.Regex.Match(name.Trim(), @"\+(\d+)$");
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }

    /// <summary>B outclasses A only when B is at least as good on EVERY number either
    /// of them carries and strictly better on one — and B is actually usable (not
    /// class-locked away from the player, when their classes are known). Absent
    /// numbers count as zero on both sides: honest for additive stats, and the
    /// reason this is conservative by design.</summary>
    public static bool Dominates(GearRow b, GearRow a, IReadOnlyList<string> myClasses)
    {
        if (b.Stats is not { } bs || a.Stats is not { } asb) return false;
        if (bs.Classes.Count > 0 && myClasses.Count > 0
            && !bs.Classes.Intersect(myClasses, StringComparer.OrdinalIgnoreCase).Any())
            return false;   // the better item is for somebody else's class
        if (b.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase)) return false;

        var strictly = false;
        foreach (var (bv, av) in MetricPairs(bs, asb))
        {
            if (bv < av) return false;
            if (bv > av) strictly = true;
        }
        return strictly;
    }

    private static IEnumerable<(double B, double A)> MetricPairs(ItemStatsBlock b, ItemStatsBlock a)
    {
        yield return (b.Ac ?? 0, a.Ac ?? 0);
        yield return (b.Hp ?? 0, a.Hp ?? 0);
        yield return (b.Mana ?? 0, a.Mana ?? 0);
        yield return (b.Dmg ?? 0, a.Dmg ?? 0);
        yield return (b.Ratio ?? 0, a.Ratio ?? 0);
        foreach (var key in b.Attributes.Keys.Union(a.Attributes.Keys, StringComparer.OrdinalIgnoreCase))
            yield return (b.Attributes.GetValueOrDefault(key), a.Attributes.GetValueOrDefault(key));
    }

    public static string StatLine(ItemStatsBlock s)
    {
        var parts = new List<string>();
        if (s is { Dmg: { } d, Delay: { } dl })
            parts.Add($"{(s.Skill.Length > 0 ? s.Skill + " " : "")}{d}/{dl}"
                + (s.Ratio is { } r ? $" ({r:0.00})" : ""));
        if (s.Ac is { } ac) parts.Add($"AC {ac}");
        if (s.Hp is { } hp) parts.Add($"HP {hp:+#;-#;0}");
        if (s.Mana is { } mana) parts.Add($"Mana {mana:+#;-#;0}");
        foreach (var (key, val) in s.Attributes.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            parts.Add($"{key} {val:+#;-#;0}");
        return string.Join(" · ", parts);
    }

    /// <summary>Class names to the 3-letter codes item blocks print ("Paladin" →
    /// "PAL"); already-code input passes through. Unknown names return themselves —
    /// a wrong guess would gate comparisons on a lie.</summary>
    public static string Code(string className)
    {
        // This used to be a private 17-row map that carried BOTH "Shadow Knight" and
        // "Shadowknight" — so the knowledge that the two spellings exist lived here,
        // and the code doing the comparing in AchievementsImport did not have it.
        // One fact, two sources (trap 4). QuestClassFilter owns the names and the
        // codes, so it owns this too.
        if (QuestClassFilter.Canonical(className) is { Length: > 0 } canonical)
            return QuestClassFilter.Abbrev(canonical);
        return className.Trim().ToUpperInvariant();
    }

    // "Is this in the bank" is InventoryFile.Entry.InBank, which knows the SHARED bank is
    // a bank too. Both rules below used to spell it `StartsWith("Bank")` for themselves,
    // so a `SharedBank1` row ranked as a WORN slot (rank 0, the most prominent location)
    // and then labelled itself `worn · SharedBank1` — an item in the shared bank
    // presented as the thing the character has equipped.
    private static int LocationRank(string location) =>
        new InventoryFile.Entry(location, "", 1).InBank ? 2
        : location.Contains("-Slot", StringComparison.Ordinal) ? 1
        : location.StartsWith("General", StringComparison.OrdinalIgnoreCase) ? 1
        : 0;   // a worn slot

    public static string WhereLabel(InventoryFile.Entry e) =>
        e.InContainer ? e.ContainerSlot
        : e.InBank || e.Location.StartsWith("General", StringComparison.OrdinalIgnoreCase)
            ? e.Location
            : $"worn · {e.Location}";
}
