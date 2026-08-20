namespace EQBuddy.UI.Shared;

/// <summary>
/// Reading and writing mez durations. Distinct from <see cref="SpawnDurationText"/> for
/// one reason, and it is the reason that matters: **a bare number here is SECONDS.**
///
/// Spawn cycles live in minutes and every wiki writes them that way, so "22" there means
/// twenty-two minutes. Mezzes live in seconds — the whole shipped catalog runs from 6s to
/// 96s — so "24" here means twenty-four seconds. Sharing one parser between them would
/// make the same keystrokes mean two things a factor of sixty apart, and the wrong one is
/// a chip that says a 24-minute mez.
///
/// Everything else is the spawn parser's, delegated rather than re-implemented: suffixes
/// (<c>90s</c>, <c>2m</c>), compounds (<c>1m30s</c>), colon forms (<c>1:30</c>), and the
/// decimal handling #124 paid for.
/// </summary>
public static class MezDurationText
{
    /// <summary>Seconds, or null when the text holds no usable duration. A bare number
    /// is seconds; anything carrying a unit parses exactly as it does for spawns.</summary>
    public static double? Parse(string? input)
    {
        var text = (input ?? "").Trim();
        if (text.Length == 0) return null;
        // Bare number (including a decimal): seconds, not minutes. Anything else — a
        // unit, a colon, punctuation — is unambiguous already, so it goes to the shared
        // parser and means there exactly what it means on a spawn row.
        if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var bare))
            return bare > 0 ? bare : null;
        return SpawnDurationText.Parse(text);
    }

    /// <summary>"24s", "1m 30s" — the spawn formatter, which already reads in seconds
    /// for values this small.</summary>
    public static string Format(double? seconds) => SpawnDurationText.Format(seconds);
}
