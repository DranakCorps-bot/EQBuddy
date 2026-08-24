namespace EQBuddy.UI.Shared;

/// <summary>
/// What the "Import achievements — preview" window's action row says.
///
/// **Written because a working button was reported as broken** (#235, LeBigNasty,
/// 2026-08-23: *"Import achievements button does not function."*). His own screenshot shows
/// the preview working perfectly: 502 achievements read, 76 Sky rewards recognized, and
/// *"Everything recognized is already marked — nothing to apply."* The Apply button was
/// correctly disabled because there was nothing to apply.
///
/// **The explanation was four screens above the button.** That sentence sits at the top of
/// the panel; then come 76 reward rows; then, at the bottom of a scrolled dialog, a greyed
/// `Apply (0)`. The eye lands on the control, not on the paragraph that explains it — trap
/// 44 (a message that has to be read on arrival, placed after the rows) and trap 17 (a
/// disabled control that does not say why) arriving together. Nothing about it is visible
/// in a diff, a test or a build.
///
/// So the BUTTON carries its own reason, and a short line sits beside it. In UI.Shared
/// because both desktops draw this dialog separately and a fix in one lane is exactly how
/// #210 happened.
/// </summary>
public static class AchievementsPreviewText
{
    /// <summary>The Apply button's words. **"Nothing to apply" rather than "Apply (0)"** —
    /// a count of zero is a number to work out; the disabled state should read as an answer
    /// instead. A player who scrolls to the bottom and sees this knows the import ran and
    /// found their rewards already marked.</summary>
    public static string ApplyLabel(int fresh) =>
        fresh > 0 ? $"Apply ({fresh})" : "Nothing to apply";

    /// <summary>The line beside the buttons, where the eye actually lands. Null when there
    /// IS something to apply — the button says the count and needs no help.
    ///
    /// It repeats what the top of the panel already says, deliberately: this is the
    /// "notifications go where the eye lands" rule (trap 44), and the top line is not
    /// visible from the bottom of a scrolled 76-row dialog.</summary>
    public static string? WhyDisabled(int fresh, int recognized) =>
        fresh > 0 ? null
        : recognized > 0
            ? $"Your import worked — all {recognized} recognized rewards are already marked, "
              + "so there is nothing left to change."
            : "Nothing in this file matched a Sky reward, so there is nothing to change.";

    /// <summary>Hover text for the disabled button, which is where a player who does not
    /// believe the line goes next (trap 17: say why in the tooltip).</summary>
    public static string ApplyTooltip(int fresh, int recognized) =>
        fresh > 0
            ? $"Mark {fresh} Sky reward{(fresh == 1 ? "" : "s")} as turned in. Applying only "
              + "ADDS — nothing currently tracked gets unchecked."
            : WhyDisabled(fresh, recognized)!;
}
