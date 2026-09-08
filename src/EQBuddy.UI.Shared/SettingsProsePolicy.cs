namespace EQBuddy.UI.Shared;

/// <summary>
/// **Which of Settings' instructional paragraphs are BODY and which hang on an ⓘ.**
///
/// The owner's complaint is the shape of the screen rather than the words on it: nearly
/// every Options tab had grown a paragraph under each control explaining it, so a screen
/// whose job is "find the switch you came for and flip it" reads as an essay you scroll
/// past. Bevel's prose-to-tooltip faces are the ruling; this is the arithmetic under them,
/// so the decision is the same one on every tab and on both hosts instead of a taste call
/// re-made per surface.
///
/// **The rule is a WORD COUNT, deliberately, and not a character count or a judgement.**
/// A screen-reading rate is what makes it checkable: the question a hover has to answer is
/// "could somebody finish this before the tooltip closes", and the tooltip's close is a
/// number this app already owns (<see cref="ToolTipPolicy.ShowDurationMs"/>, 30 s, bounded
/// because WPF's own default froze the app — trap 63). So the two ends of the policy are
/// the same measurement seen from opposite sides:
///
/// <list type="bullet">
/// <item>Longer than <see cref="BodyWordCeiling"/> words and it is an EXPLANATION, not a
///   label — it belongs on hover (<see cref="BelongsOnHover"/>).</item>
/// <item>Longer than the bounded tooltip can be READ in and it does not fit ONE hover
///   (<see cref="FitsOneHover"/>) — the paragraph needs splitting across the controls it
///   is actually about, or it needs to stay in the body where nothing takes it away.</item>
/// </list>
///
/// **What this policy does NOT decide, and must not be read as deciding: whether a
/// paragraph has a control to hang on at all.** A sentence explaining a switch has an
/// obvious owner; a sentence answering "where did my switch GO" has none, and hiding it
/// behind an ⓘ nobody knows to hover is the same defect as deleting it — an absent
/// sentence photographs as an unremarkable list (traps 29/34). That call is made per
/// paragraph, at the surface, and the ones exempted are enumerated with their reason in
/// `SettingsProsePolicyTests` rather than left to whoever edits the block next.
/// </summary>
public static class SettingsProsePolicy
{
    /// <summary>Above this many words a line has stopped being a caption and started being
    /// an explanation. 20 is roughly two rendered lines at the width Settings runs at, which
    /// is the point where the eye starts skipping the paragraph rather than reading it —
    /// and it is chosen so that the shortest helper lines on these screens (the recent-rate
    /// window's ten-word "which figures this changes") stay where they are. A ceiling that
    /// swept up EVERY line would leave a screen of bare labels, which is the opposite
    /// failure and just as bad.</summary>
    public const int BodyWordCeiling = 20;

    /// <summary>The silent screen-reading rate this policy assumes, in words per minute.
    /// 200 is the conventional adult figure for prose on a screen; it is an ASSUMPTION
    /// stated as a constant rather than a measurement of anybody, and it is only ever used
    /// to answer <see cref="FitsOneHover"/> — nothing in the app is timed by it.</summary>
    public const int ReadingWordsPerMinute = 200;

    /// <summary>Words, counted the way a reader counts them: whitespace-separated tokens
    /// that contain a letter or a digit. The dashes, arrows and ✕ our copy uses as
    /// punctuation are not words, and counting them would make a paragraph's budget depend
    /// on its typography.</summary>
    public static int Words(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var words = 0;
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var c in token)
                if (char.IsLetterOrDigit(c)) { words++; break; }
        }
        return words;
    }

    /// <summary>How long <paramref name="text"/> takes to read at
    /// <see cref="ReadingWordsPerMinute"/>, in milliseconds — the same unit
    /// <see cref="ToolTipPolicy.ShowDurationMs"/> is in, so the comparison in
    /// <see cref="FitsOneHover"/> is a comparison rather than a conversion.</summary>
    public static int ReadingMs(string? text) =>
        (int)(Words(text) * 60_000L / ReadingWordsPerMinute);

    /// <summary>Explanation rather than caption: this belongs on an ⓘ, not under the
    /// control.</summary>
    public static bool BelongsOnHover(string? text) => Words(text) > BodyWordCeiling;

    /// <summary>Can a reader finish this before the bounded tooltip takes it away? A
    /// paragraph that cannot is not made hoverable by wanting it to be — the tooltip
    /// closes mid-sentence and the player has no way to ask for the rest.</summary>
    public static bool FitsOneHover(string? text) =>
        ReadingMs(text) <= ToolTipPolicy.ShowDurationMs;

    /// <summary>What the ⓘ calls itself to a screen reader and to UI Automation. The
    /// visible affordance is a drawn icon with no text of its own, so without this the
    /// control announces as "button" — which is trap 17's family: an affordance that is
    /// present for the eye and absent for everything else.</summary>
    public const string HoverAffordanceName = "Explain this";
}
