namespace EQBuddy.Core;

/// <summary>
/// One archived session's own vendor sales, mined from its stored snapshot — the same
/// snapshot-probe idiom <c>SessionRepository.ThroughputRows</c> and <c>MobRows</c> already
/// use, and for the same reason (DRA-71 D7, plan P9).
///
/// <para><b>Why a probe rather than columns.</b> <c>history.db</c> carries one
/// <c>Copper</c> figure per session and no breakdown; what was SOLD, and for how much, only
/// exists inside the stored <c>StatsSnapshot</c>. Adding columns is a schema migration, which
/// DRA-71 D3 filed as its own slice rather than doing quietly, and probing the snapshot is
/// what two existing folds already do for exactly this reason.</para>
/// </summary>
/// <param name="Id">The session row this describes — the join key, never a position.</param>
/// <param name="Items">What that session sold, in the snapshot's own shape.</param>
public sealed record SessionSales(long Id, IReadOnlyList<SoldDetail> Items);

/// <summary>
/// **WHAT THIS CHARACTER HAS ACTUALLY BEEN PAID FOR ONE THING.**
/// </summary>
/// <param name="Item">The item, folded to the name the wiki titles
/// (<see cref="QuestCatalog.BaseItemName"/>) — the same fold <see cref="MobHistory"/> applies
/// to loot, which is what lets a sale be joined to a drop at all.</param>
/// <param name="Count">How many of it you have sold — the denominator.</param>
/// <param name="Copper">What you were paid for them, in total.</param>
public sealed record SaleRoll(string Item, int Count, long Copper)
{
    /// <summary>What one of them fetched, on average. 0 when nothing was sold, which is a
    /// state <see cref="SaleHistory.Fold"/> does not produce — it is here so the record cannot
    /// divide by zero if somebody constructs one.</summary>
    public long CopperEach => Count > 0 ? Copper / Count : 0;
}

/// <summary>
/// **YOUR OWN VENDOR PRICES, POOLED** (DRA-71 D7, plan P9; Founder smoke item 4c).
///
/// <para><b>This is the source the copper SURVEY sent the slice to.</b> The plan asked for the
/// catalog's <c>merchant_value</c> to be un-PARKed and weighed. Surveying the cached item dump
/// first — trap 73's tell, and the reason the plan asked for a survey — found that the wiki's
/// price is not a property of the item: 262 of the 975 pages that carry one state it as
/// <i>"VALUE TO VENDOR with CHA : 80 and faction at Indifferently"</i>, and others say
/// <i>"with 111 Charisma"</i>. A vendor price in EQ moves with your Charisma and your faction,
/// so the wiki's number is a PRICE SOMEBODY WAS QUOTED and not a fact about the object. What
/// the player was actually paid has neither problem: it was their Charisma and their faction,
/// and the log recorded it.</para>
///
/// <para><b>So the ranking is built on this and the catalog is the fallback</b> — HOME-003's
/// order, arrived at from the evidence rather than assumed. The catalog's own number still
/// lands (<see cref="ItemCatalog.Record.MerchantCopper"/>) and is drawn, labelled and with the
/// condition the page stated, for an item you have never sold. It never weighs anything.</para>
///
/// <para><b>One producer, one fold.</b> Nothing else in this repo pools sales; the live
/// session's own snapshot is folded in here beside the archived ones, under the same
/// exclude-the-live-row rule <see cref="MobHistory.Pool"/> keeps, so a checkpoint that has
/// already landed is not counted twice.</para>
/// </summary>
public static class SaleHistory
{
    /// <summary>
    /// Fold stored sessions' sales and the live one into one row per item.
    /// </summary>
    /// <param name="rows">Stored sales — <c>SessionRepository.SoldRows</c>'s own output.</param>
    /// <param name="live">The live session's snapshot, or null. Its sales are taken from HERE
    /// and its checkpointed row is skipped by id, or a checkpoint that has already landed
    /// would pool the same sale twice — <see cref="MobHistory.Pool"/>'s own rule.</param>
    /// <param name="liveRowId">The row id the live session checkpoints under.</param>
    public static IReadOnlyList<SaleRoll> Fold(
        IReadOnlyList<SessionSales>? rows, StatsSnapshot? live, long liveRowId)
    {
        var acc = new Dictionary<string, (int Count, long Copper)>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows ?? [])
        {
            if (row.Id == liveRowId) continue;
            foreach (var sale in row.Items) Add(acc, sale);
        }
        foreach (var sale in live?.SoldItems ?? []) Add(acc, sale);

        return [.. acc
            .Select(kv => new SaleRoll(kv.Key, kv.Value.Count, kv.Value.Copper))
            .OrderByDescending(s => s.Copper)
            .ThenBy(s => s.Item, StringComparer.OrdinalIgnoreCase)];

        static void Add(Dictionary<string, (int, long)> into, SoldDetail sale)
        {
            // A sale with no count cannot produce a per-item price and would divide by zero
            // one layer up; a negative one is not a thing the log can print. Both are dropped
            // rather than folded, because an item's price is the one number this fold exists
            // to produce and a wrong one is worse than a missing one.
            if (sale.Count <= 0 || sale.Copper < 0) return;
            // **The BASE name, and it is what makes the join possible at all.** The pool folds
            // loot the same way, so "Rusty Long Sword +3" and "Rusty Long Sword" are one thing
            // on both sides. It does lose the premium a "+N" fetches — an honest cost, stated
            // rather than hidden: the alternative is a sale that can never be matched to the
            // drop it came from, which is the whole answer.
            var key = QuestCatalog.BaseItemName(sale.Item).Trim();
            if (key.Length == 0) return;
            var prev = into.TryGetValue(key, out var p) ? p : (0, 0L);
            into[key] = (prev.Item1 + sale.Count, prev.Item2 + sale.Copper);
        }
    }

    /// <summary>
    /// What you have been paid for one of these, or null when you have never sold one.
    ///
    /// <para>Null is the important half: it is the difference between "you sell these for 8
    /// silver" and "EQBuddy has never seen you sell one", and only the second one is allowed
    /// to fall through to the catalog's estimate.</para>
    /// </summary>
    public static long? CopperEachFor(IReadOnlyList<SaleRoll>? sales, string item)
    {
        if (sales is null || string.IsNullOrWhiteSpace(item)) return null;
        var wanted = QuestCatalog.BaseItemName(item).Trim();
        foreach (var sale in sales)
            if (sale.Item.Equals(wanted, StringComparison.OrdinalIgnoreCase) && sale.Count > 0)
                return sale.CopperEach;
        return null;
    }
}
