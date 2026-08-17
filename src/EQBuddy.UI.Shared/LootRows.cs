using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Builds the Loot card's (and its breakout's) row list from a snapshot's looted and made
/// items, honoring the show filter (all / looted / made) and the sort mode
/// (count / name / recent). One place, so the two surfaces can never drift apart.
///
/// The "show" axis (David's two-list layout was looted-then-made; LW, 2026-08-16, wanted a
/// filter instead):
///   - "looted": corpse + foraged items only.
///   - "made":   combine results only.
///   - "all":    the two MIXED into a single list — not stacked with a heading, because the
///               "made" filter already gives you the made-only list on its own.
///
/// The "recent" sort is a looted-only idea — it is arrival order (see <see cref="RawLootView"/>)
/// and made items carry no timestamp — so under "made" it falls back to count, and under
/// "all" the recent looted rows come first with the made rows appended by count.
/// </summary>
public static class LootRows
{
    public static List<(string Item, string Value)> Build(
        IReadOnlyList<LootDetail> loot,
        IReadOnlyList<NameCount> crafted,
        IReadOnlyList<LootPickup> recentLoot,
        string view,
        string mode)
    {
        if (view == "looted") return Looted(loot, recentLoot, mode).ToList();
        if (view == "made") return Made(crafted, mode).ToList();

        // "all" — mix looted and made.
        if (mode == "recent")
            // Crafts have no arrival time, so they can't interleave into the timeline;
            // show the recent looted view, then the made rows by count.
            return Looted(loot, recentLoot, "recent").Concat(Made(crafted, "count")).ToList();

        // count / name: fold both into one sequence and order it as a whole, so a made
        // item with a big count sorts among the looted stacks instead of after them.
        var combined = loot.Select(l => (Item: l.Item, l.Count))
            .Concat(crafted.Select(c => (Item: c.Name, c.Count)));
        combined = mode == "name"
            ? combined.OrderBy(x => x.Item, StringComparer.OrdinalIgnoreCase)
            : combined.OrderByDescending(x => x.Count);
        return combined.Select(x => (x.Item, $"×{x.Count}")).ToList();
    }

    private static IEnumerable<(string Item, string Value)> Looted(
        IReadOnlyList<LootDetail> loot, IReadOnlyList<LootPickup> recentLoot, string mode) =>
        mode == "recent"
            ? RawLootView.Rows(recentLoot).Select(r => (r.Item, r.Detail))
            : (mode == "name"
                    ? loot.OrderBy(l => l.Item, StringComparer.OrdinalIgnoreCase)
                    : loot.AsEnumerable())   // snapshot hands loot back count-desc already
                .Select(l => (l.Item, $"×{l.Count}"));

    private static IEnumerable<(string Item, string Value)> Made(
        IReadOnlyList<NameCount> crafted, string mode) =>
        (mode == "name"
                ? crafted.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                : crafted.AsEnumerable())    // count-desc already; "recent" has no made order
            .Select(c => (c.Name, $"×{c.Count}"));
}
