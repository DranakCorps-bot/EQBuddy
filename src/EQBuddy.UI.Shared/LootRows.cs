using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One loot row: the item, its value column, and an optional provenance tag
/// ("Foraged" / "Crafted" / "Merged" / "Parcel") the UI renders muted after the name.
/// A null tag is plain corpse loot.</summary>
public sealed record LootRow(string Item, string Value, string? Tag);

/// <summary>
/// Builds the Loot card's (and its breakout's) rows from a snapshot, honoring the show
/// filter (all / looted / other) and the sort mode (count / name / recent). One place, so
/// the two surfaces can't drift.
///
/// The "show" axis:
///   - "looted": corpse drops only.
///   - "other":  everything else you acquired — foraged, crafted, merged, parcel.
///   - "all":    the two mixed into one ordered list.
///
/// Auto-sold pickups ("looted … and sold it for …") never reach these rows at all:
/// dismissed at the corpse means not interesting as loot (LW, 2026-08-17 — a (Sold)
/// tag + filter was tried first and crowded the views). They are vendor income and a
/// per-creature drop-ledger entry; the snapshot's Loot/RecentLoot exclude them.
///
/// Provenance drives both the filter and the muted inline tag. Corpse loot is untagged;
/// forage and parcel ride the loot list (LootDetail.LastSource); crafted (fashioned) and
/// merged come in as their own lists. "recent" is arrival order (see <see cref="RawLootView"/>)
/// and only looted/foraged/parcel carry a timestamp, so under "recent" the made rows
/// (crafted+merged) append by count.
/// </summary>
public static class LootRows
{
    public const string ForageSource = "Forage";
    public const string ParcelSource = "Parcel";
    public const string FashionSource = "Fashion";   // crafts, in the recent timeline
    public const string MergeSource = "Merge";        // item-merges, in the recent timeline

    // Everything that isn't a corpse drop is "other".
    private static bool IsOther(string source) =>
        source is ForageSource or ParcelSource or FashionSource or MergeSource;
    private static string? LootTag(string source) => source switch
    {
        ForageSource => "Foraged",
        ParcelSource => "Parcel",
        FashionSource => "Crafted",
        MergeSource => "Merged",
        _ => null,   // corpse loot
    };

    public static List<LootRow> Build(
        IReadOnlyList<LootDetail> loot,
        IReadOnlyList<NameCount> merged,      // snapshot's Crafted — the "(Merged)" provenance
        IReadOnlyList<NameCount> fashioned,   // snapshot's Fashioned — the "(Crafted)" provenance
        IReadOnlyList<LootPickup> recentLoot,
        string view,
        string mode)
    {
        var lootFor = view switch
        {
            "looted" => loot.Where(l => !IsOther(l.LastSource)),
            "other" => loot.Where(l => IsOther(l.LastSource)),
            _ => loot.AsEnumerable(),
        };
        var includeMade = view != "looted";   // crafted + merged live under other / all

        if (mode == "recent")
        {
            var picks = view switch
            {
                "looted" => recentLoot.Where(p => !IsOther(p.Source)),
                "other" => recentLoot.Where(p => IsOther(p.Source)),
                _ => recentLoot.AsEnumerable(),
            };
            // The raw timeline: ONE row per acquisition with its own timestamp, newest first
            // (RecentLoot order) — deliberately NOT run-collapsed like RawLootView.Rows.
            // Aggregating multiples is what the count view is for (LW, 2026-08-17). Crafts and
            // merges ride here too (source Fashion/Merge → the (Crafted)/(Merged) tag).
            return picks
                .Select(p => new LootRow(p.Item, RawLootView.Detail(p), LootTag(p.Source)))
                .ToList();
        }

        // count / name: fold looted (+ made under other/all) into one sequence and order it
        // as a whole. Count ties break alphabetically so a page of ×1 drops reads ordered.
        var items = lootFor.Select(l => (Item: l.Item, l.Count, Tag: LootTag(l.LastSource)));
        if (includeMade)
            items = items
                .Concat(merged.Select(m => (Item: m.Name, m.Count, Tag: (string?)"Merged")))
                .Concat(fashioned.Select(f => (Item: f.Name, f.Count, Tag: (string?)"Crafted")));
        items = mode == "name"
            ? items.OrderBy(x => x.Item, StringComparer.OrdinalIgnoreCase)
            : items.OrderByDescending(x => x.Count).ThenBy(x => x.Item, StringComparer.OrdinalIgnoreCase);
        return items.Select(x => new LootRow(x.Item, $"×{x.Count}", x.Tag)).ToList();
    }
}
