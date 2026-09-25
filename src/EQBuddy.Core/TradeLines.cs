using System.Text.RegularExpressions;

namespace EQBuddy.Core;

/// <summary>
/// The hand-in trio, verbatim from Hateborne's log (2026-09-03 and 2026-09-18) - read by
/// <see cref="HandInTracker"/>:
///
/// <code>
/// You offered 1 Wind Rune Meda to Cilin Spellsinger.
/// You complete the trade with Cilin Spellsinger.
/// Wizard Schrock says, 'I have no need for this, Hateborne. You can have it back.'
/// </code>
///
/// Its own file rather than three more regexes in <see cref="LogParser"/>, which is a
/// ratchet-watched hotspot. Each line is gated on a literal before its regex runs, so the
/// cost to every other line is two prefix checks and one substring probe.
/// </summary>
internal static partial class TradeLines
{
    // The item capture is GREEDY so the split lands on the last " to " - item names say
    // "to" far more often than NPC names do.
    [GeneratedRegex(@"^You offered (?<n>\d+) (?<item>.+) to (?<target>.+?)\.$")]
    private static partial Regex OfferRx();

    [GeneratedRegex(@"^You complete the trade with (?<target>.+?)\.$")]
    private static partial Regex CompleteRx();

    [GeneratedRegex(@"^(?<npc>.+?) says, 'I have no need for this, .+?\. You can have it back\.'$")]
    private static partial Regex RefusedRx();

    /// <summary>The trade event this line is, or null.</summary>
    public static GameEvent? Parse(DateTime ts, string msg)
    {
        Match r;
        if (msg.StartsWith("You offered ", StringComparison.Ordinal) && (r = OfferRx().Match(msg)).Success)
            return new TradeOfferEvent(ts, r.Groups["item"].Value, int.Parse(r.Groups["n"].Value),
                r.Groups["target"].Value);
        if (msg.StartsWith("You complete the trade with ", StringComparison.Ordinal)
            && (r = CompleteRx().Match(msg)).Success)
            return new TradeCompleteEvent(ts, r.Groups["target"].Value);
        if (msg.Contains(", 'I have no need for this, ", StringComparison.Ordinal)
            && (r = RefusedRx().Match(msg)).Success)
            return new TradeRefusedEvent(ts, r.Groups["npc"].Value);
        return null;
    }
}
