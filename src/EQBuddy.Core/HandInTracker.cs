namespace EQBuddy.Core;

/// <summary>One trade the log says went through: what was handed to whom, and when.</summary>
public sealed record HandIn(string Target, DateTime At, IReadOnlyList<(string Item, int Count)> Items);

/// <summary>
/// Turns the log's trade lines into hand-ins - items that left the character's hands
/// (Hateborne, 2026-09-18: the Sky tab could not see a Wind Rune Meda handed to Cilin
/// Spellsinger, because EQBuddy had long believed the log never records a hand-in; it
/// does). Verbatim from his log:
///
/// <code>
/// You offered 1 Light Woolen Mask to Cilin Spellsinger.
/// You offered 1 Wind Rune Meda to Cilin Spellsinger.
/// You complete the trade with Cilin Spellsinger.
/// Wizard Schrock says, 'I have no need for this, Hateborne. You can have it back.'
/// </code>
///
/// **Offers alone are nothing** - a trade window closed without completing never leaves a
/// completion line. A completion takes the offers made to that target in the last minute
/// and holds them for a short window, because a refusal arrives AFTER the completion, one
/// line per item returned, and never names the item. A refusal from that NPC inside the
/// window drops the whole trade (conservative: the next inventory dump settles bag items,
/// and dropping beats guessing which item came back). The first event past the window
/// makes it final.
///
/// Deterministic in log order, so the launch replay settles every trade the same way; the
/// ledger's own time gate is what stops a replayed hand-in counting twice.
/// </summary>
public sealed class HandInTracker
{
    public static readonly TimeSpan OfferWindow = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan RefusalWindow = TimeSpan.FromSeconds(3);

    private readonly List<(DateTime Time, string Item, int Count, string Target)> _offers = [];
    private HandIn? _pending;

    /// <summary>
    /// Feed every event, in log order.
    /// </summary>
    /// <returns>A hand-in that became final with this event, or null. At most one per
    /// call: a completion arriving while another is still pending makes the earlier one
    /// final - two trades inside one refusal window cannot be told apart, so the first
    /// stands as given.</returns>
    public HandIn? Observe(GameEvent e)
    {
        var settled = Settle(e);
        switch (e)
        {
            case TradeOfferEvent o:
                _offers.Add((o.Time, o.Item, o.Count, o.Target));
                break;
            case TradeCompleteEvent tc:
                settled ??= TakePending();
                var handed = _offers
                    .Where(x => Same(x.Target, tc.Target) && tc.Time - x.Time <= OfferWindow)
                    .GroupBy(x => x.Item, StringComparer.OrdinalIgnoreCase)
                    .Select(g => (g.Key, g.Sum(x => x.Count)))
                    .ToList();
                // This trade's offers are spent; anything older than the window is a
                // cancelled trade nobody completed.
                _offers.RemoveAll(x => Same(x.Target, tc.Target) || tc.Time - x.Time > OfferWindow);
                if (handed.Count > 0) _pending = new HandIn(tc.Target, tc.Time, handed);
                break;
        }
        return settled;
    }

    /// <summary>Forget everything unsettled - a session rollover or a character switch.</summary>
    public void Reset()
    {
        _offers.Clear();
        _pending = null;
    }

    private HandIn? Settle(GameEvent e)
    {
        if (_pending is not { } p) return null;
        if (e is TradeRefusedEvent r && Same(r.Npc, p.Target) && e.Time - p.At <= RefusalWindow)
        {
            _pending = null;
            return null;
        }
        return e.Time - p.At <= RefusalWindow ? null : TakePending();
    }

    private HandIn? TakePending()
    {
        var p = _pending;
        _pending = null;
        return p;
    }

    private static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
}
