namespace EQBuddy.Core;

/// <summary>
/// **READING A COIN AMOUNT THAT SOMEBODY ELSE WROTE** (DRA-71 D7, plan P9).
///
/// <para>The inverse of <see cref="StatsSnapshot.FormatCoin"/>, which is the one formatter
/// every surface in this repo prints coin through. It lives in its own file rather than beside
/// it because <c>SessionStats*.cs</c> is a ratcheted hotspot and fifty-odd lines of new grammar
/// is exactly what the ratchet exists to keep out; the pairing is kept honest by
/// <c>CoinTextTests</c>, which round-trips every shape the formatter can produce back through
/// this parser rather than trusting the two to stay in step because they were adjacent.</para>
///
/// <para><b>Its input is eqlwiki's <c>merchant_value</c> field, and the survey of that field is
/// why it is strict.</b> Of the 975 cached item pages that state one, 268 are plain coin text,
/// 262 are an HTML block whose own heading reads "VALUE TO VENDOR with CHA : 80 and faction at
/// Indifferently", and 435 are something else: prose ("absolutely nothing"), approximations
/// ("~2pp"), qualified figures ("197.6p Max") and conditions written inline ("2p 1g 8s 3c with
/// 111 Charisma"). <b>Anything this cannot read exactly is ABSENT rather than guessed</b> —
/// deciding what a tilde means, or dropping the word "Max", is trap 73 with arithmetic instead
/// of prose.</para>
/// </summary>
public static class CoinText
{
    /// <summary>
    /// Compact coin text to copper, or null when the text is not compact coin.
    ///
    /// <para>Both EQ spellings of each unit are taken (<c>5p</c> and <c>5pp</c>) because they
    /// are the same word and the wiki uses both. A repeated unit is REFUSED rather than summed:
    /// a page that states platinum twice is a page nobody here can read, and summing it would
    /// invent an amount out of an editing mistake.</para>
    ///
    /// <para><b>Zero is a real answer and null is not.</b> "0cp" is a page saying the item is
    /// worthless; null is a page that said nothing this code understands. A reader that treated
    /// them alike would price a worthless item the same as an unread one.</para>
    /// </summary>
    public static long? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        long total = 0;
        var seen = 0;
        var units = 0;
        foreach (var part in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var digits = 0;
            while (digits < part.Length && char.IsAsciiDigit(part[digits])) digits++;
            if (digits == 0 || !long.TryParse(part[..digits], out var n)) return null;

            // A BIT per denomination, so a repeat is caught rather than added. Unit 0 is "not a
            // denomination at all", which covers "4.2p" (the dot ends the digits, leaving
            // ".2p"), "197.6p", "~2pp" (no leading digit) and every prose value in the survey.
            var bit = part[digits..].ToLowerInvariant() switch
            {
                "p" or "pp" => 1,
                "g" or "gp" => 2,
                "s" or "sp" => 4,
                "c" or "cp" => 8,
                _ => 0,
            };
            if (bit == 0 || (units & bit) != 0) return null;
            units |= bit;

            total += n * bit switch { 1 => 1000, 2 => 100, 4 => 10, _ => 1 };
            seen++;
        }
        return seen > 0 ? total : null;
    }
}
