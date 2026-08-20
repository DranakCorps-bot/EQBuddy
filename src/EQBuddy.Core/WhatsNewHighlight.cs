namespace EQBuddy.Core;

/// <summary>What a release note is telling you. Not decoration — the two kinds are read
/// for different reasons.</summary>
public enum WhatsNewKind
{
    /// <summary>Something is new, better, or fixed. Read it or don't.</summary>
    Change,

    /// <summary>Something you already use is now reached a DIFFERENT WAY. You have to read
    /// this one, because the thing you are looking for is not where you left it.</summary>
    Moved,
}

/// <summary>One release note, split into what kind of news it is and what it says.</summary>
/// <param name="Kind">Change or Moved.</param>
/// <param name="Label">The badge a UI draws in front of a Moved note; empty for a Change.</param>
/// <param name="Text">The note itself, marker stripped.</param>
public sealed record WhatsNewHighlight(WhatsNewKind Kind, string Label, string Text);

/// <summary>
/// Tells a MOVE apart from everything else in the what's-new popup (David, 2026-08-20:
/// *"please explicitly note, maybe in a different color, when things move from accessing
/// one way to another"*).
///
/// **Why this is worth its own kind.** The popup is the only place a near-silent
/// auto-update announces itself, and the family gets updates without asking for them
/// (NOTES-001). A note saying a feature got better is optional reading. A note saying the
/// Loot card is now a tab in a window is NOT: the reader's next action is to go looking for
/// something, fail to find it, and conclude EQBuddy removed it. That is the exact failure
/// #219 reported and the reason Options grew its "Gear is a tab in here now" line — and the
/// popup is a whole release EARLIER in a player's day than Options is.
///
/// **The marker is a prefix on the string, not a new JSON field, on purpose.** Every entry
/// ever written is still valid, `release.ps1`'s "no entry, no release" check is untouched,
/// and an unmarked note simply reads as a Change. The cost of a schema change here is that
/// every old entry has to be migrated or specially handled forever; the cost of a prefix is
/// one <see cref="Parse"/> call.
/// </summary>
public static class WhatsNewNotes
{
    /// <summary>The prefix that marks a relocation, at the start of a highlight string:
    /// <c>"MOVED: the Loot card is now a tab in Gear &amp; Loot"</c>.</summary>
    public const string MovedMarker = "MOVED:";

    /// <summary>The badge a UI draws in front of a moved note. A WORD, not a glyph — this
    /// popup is the first thing a player sees after an update, on every platform, and emoji
    /// box outright under the Wine prefixes the Linux and macOS builds run in (#148, #166).</summary>
    public const string MovedLabel = "MOVED";

    /// <summary>Split one highlight into its kind and its text. Anything without the marker
    /// is an ordinary change, which is what every note written before 2026-08-20 is.</summary>
    public static WhatsNewHighlight Parse(string? highlight)
    {
        var text = (highlight ?? "").Trim();
        if (!text.StartsWith(MovedMarker, StringComparison.OrdinalIgnoreCase))
            return new WhatsNewHighlight(WhatsNewKind.Change, "", text);

        var body = text[MovedMarker.Length..].TrimStart();
        // A marker with nothing after it is a typo, not a move. Draw it as an ordinary
        // note rather than a badge with no sentence beside it — a silent no-op is broken,
        // and so is a label pointing at nothing.
        return body.Length == 0
            ? new WhatsNewHighlight(WhatsNewKind.Change, "", text)
            : new WhatsNewHighlight(WhatsNewKind.Moved, MovedLabel, body);
    }

    /// <summary>Every highlight in one release, parsed. Named <c>ParseAll</c> rather than
    /// being a second <c>Parse</c> overload: <c>Parse(null)</c> is a case this type has to
    /// answer for, and an overload set makes that call ambiguous rather than answerable.</summary>
    public static IReadOnlyList<WhatsNewHighlight> ParseAll(WhatsNewEntry entry) =>
        [.. entry.Highlights.Select(Parse)];
}
