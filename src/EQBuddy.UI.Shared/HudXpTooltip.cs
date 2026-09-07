using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>What the collapsed HUD's xp chip says on hover, and the two facts a dump can
/// assert about it. A record rather than a bare string because "the tooltip mentions a
/// level" and "the tooltip carries the ETA" are two separable ways this can go wrong, and
/// a test that could only read the finished prose would have to parse it back out.</summary>
/// <param name="Text">The hover text itself, newline-separated.</param>
/// <param name="Level">The level the tooltip states, or 0 when it says none is known yet.
/// The dump fact, so a wrong fallback ORDER is visible rather than merely plausible.</param>
/// <param name="HasEta">Whether the forecast sentence is on it. False is a real state —
/// <see cref="StatsSnapshot.HoursToLevel"/> is null below 0.05%/hr — and the tooltip says
/// so in words rather than dropping the line.</param>
public readonly record struct HudXpTip(string Text, int Level, bool HasEta);

/// <summary>
/// The xp chip's hover text on the collapsed HUD — the next-level ETA and the character's
/// tracked level (OE-3; `BEVEL.md` item 2, Helm-signed in the #347 sign's item 2).
///
/// **Neither number is new, and that is the whole finding.** `SessionStats` has computed
/// `XpPerHour` and `HoursToLevel` all along and `ProgressPresentation` has worded the
/// forecast all along — one hop away, in the Progress window. The level has been persisted
/// per character in `quest-ledger.json` since the reward-preview memo needed it. What was
/// missing was a surface: the compact chip a farmer actually reads showed `%/hr` and
/// nothing else, and the only path from it to the sentence was a double-click gated on
/// `DoubleClickChipsToggleBreakouts`, which has no initializer and so defaults OFF. So the
/// owner's "is my level even being tracked?" was an accurate report of an app with **no
/// screen anywhere showing their level as a number**, not of a missing calculation.
///
/// **It reuses the wording rather than re-deriving it** —
/// <see cref="ProgressPresentation.NextLevelSentence"/> is the one source, so the hover and
/// the Progress room cannot forecast the same session differently (trap 4).
///
/// **A decision with no window in it**, for the reason every rule in this folder is: the
/// WPF layer has no unit tests (docs/TestPlan.md §5), so a tooltip composed in a view is a
/// tooltip nothing can check. <c>HudXpTooltipTests</c> pins every line, including both
/// empty states.
///
/// **Trap 12 does not reach here and it is worth saying why**, since everything else about
/// this bar is governed by it: a WPF tooltip is a popup, not part of the widget's measured
/// content, so text that changes on the one-second tick cannot resize a
/// <c>SizeToContent</c> window over a fullscreen game. <see cref="HudGlance"/>'s fixed
/// shapes are load-bearing precisely because they ARE in the layout; this is not.
/// </summary>
public static class HudXpTooltip
{
    /// <summary>Line one, unchanged from OE-1: what the chip is, and what the click does.
    /// It stays FIRST — a hover that opens with a bare "Level 27" over a chip reading
    /// "12.4%/hr" has stopped identifying itself, and the gesture hint is the only place
    /// the peek/pin interaction is explained at all.</summary>
    public const string GestureLine =
        "Experience per hour — hover to peek, click to keep it open";

    /// <summary>Said in words rather than by leaving the line out. "Unclear whether level
    /// is persisted" is the report this whole item answers, and a tooltip that silently
    /// omits the level is indistinguishable from the app not tracking one — the silent
    /// no-op rule with the switch on the other side.</summary>
    public const string NoLevelLine = "Level not seen yet — the log names it when you ding";

    /// <summary>Same reasoning as <see cref="NoLevelLine"/>. The Progress card OMITS its
    /// ETA line at this point, which is right for a list of tallies and wrong for a hover
    /// whose whole job this turn is to answer "when do I level".</summary>
    public const string NoEtaLine = "Next level: not enough xp yet to estimate";

    /// <summary>The hover text for the xp slot.</summary>
    /// <param name="ledgerLevel">The durable per-character level, or null when the ledger
    /// has never recorded one. Preferred over the snapshot's, per the signed fallback
    /// order: the ledger survives a restart and a truncated log, and the widget writes it
    /// from the same ding the snapshot reads, so it is never staler.</param>
    public static HudXpTip For(StatsSnapshot s, int? ledgerLevel)
    {
        var level = ledgerLevel ?? s.LastLevel ?? 0;
        var eta = s.HoursToLevel;
        return new HudXpTip(
            string.Join("\n",
                GestureLine,
                level > 0 ? $"Level {level}" : NoLevelLine,
                eta is { } hours ? ProgressPresentation.NextLevelSentence(hours) : NoEtaLine),
            level,
            eta is not null);
    }
}
