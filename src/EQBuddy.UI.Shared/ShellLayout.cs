namespace EQBuddy.UI.Shared;

/// <summary>What the shell looks like at one window width — the answer, not the reasoning.</summary>
/// <param name="RailWidth">How wide the rail is drawn.</param>
/// <param name="RailLabelsVisible">False once the rail is icons-only.</param>
/// <param name="RoomSinglePane">
/// True once a list+detail room must collapse to one pane with a back affordance.
/// Quests is the first consumer (E-3 PR 3); Progress stays single-column.
/// It is decided here because the two axes have DIFFERENT thresholds and
/// conflating them is how a resize bug hides: the rail can have plenty of room for
/// labels while the room under it is already too narrow to split, and vice versa.
/// </param>
public readonly record struct ShellLayout(double RailWidth, bool RailLabelsVisible, bool RoomSinglePane);

/// <summary>
/// How the shell degrades as its window narrows — **two independent axes and a floor**,
/// per Bevel's Helm-signed shell nav pre-design (2026-09-04 ~9:25 PM CT) §4.
///
/// It is arithmetic, so it lives here rather than in the window. That is this repo's
/// standing move for window bugs that are SUMS rather than pixels: the WPF layer has no
/// unit tests, so a threshold left inline is a threshold nothing can check. Both bugs
/// that reached players on 2026-08-14 were sums.
///
/// **The floor is a real measured pair, not a fresh guess.** <see cref="MinRoomWidth"/>
/// is <c>ProgressWindow</c>'s shipped width — the narrowest this codebase has ever
/// actually drawn this content at — and <see cref="MinHeight"/> is
/// <c>HistoryWindow</c>'s proven 400, the one existing native-chrome window in the repo.
/// Below the floor the window simply stops shrinking: **the rail and the room must never
/// clip silently**, because trap 14 and trap 25 are both, in the end, a fixed-width
/// assumption meeting content it could not measure.
/// </summary>
public static class ShellLayoutPolicy
{
    /// <summary>
    /// The narrowest a room's content is drawn — <c>ProgressWindow</c>'s shipped 520,
    /// which is the width the Progress tabs were designed and screenshotted against.
    ///
    /// **PR 2 added two rooms whose v1 windows are WIDER, and this number deliberately did
    /// not move.** <c>WorldWindow</c> opens at 640 and <c>GearLootWindow</c> at 880 — but
    /// those are OPENING widths, not measured minimums: both windows have been resizable
    /// down to <c>WindowSizing.MinWidth</c> (320) since 2026-08-21, and their content is
    /// vertical lists that reflow. Taking the maximum instead would put the shell's floor
    /// at 940, which is within 20 units of the width it OPENS at — the degrade axis Bevel
    /// designed would then be unreachable on any window a player could make, and a
    /// collapsed rail would be a state that exists only in a unit test.
    ///
    /// **So this is a claim, and the claim is tested by a picture rather than believed.**
    /// <c>scripts/shoot.ps1</c>'s <c>shell-gear-narrow</c> shoots the WIDEST room at this
    /// floor precisely because it is the one that can disprove the number. If a room clips
    /// horizontally there, this constant is what moves — not the shot, and not a
    /// horizontal scrollbar, which would hide a layout failure behind an affordance.
    /// </summary>
    public const double MinRoomWidth = 520;

    /// <summary>The window's floor width: the room's minimum plus the rail collapsed to
    /// icons. The rail is chrome the room does not get, so it is added rather than
    /// absorbed.</summary>
    public static double MinWidth => MinRoomWidth + DesignTokens.RailWidthCollapsed;

    /// <summary><c>HistoryWindow</c>'s proven height floor.</summary>
    public const double MinHeight = 400;

    /// <summary>
    /// Axis 1's threshold: the rail keeps its labels only while an expanded rail AND a
    /// full-width room both fit. Derived from the two numbers rather than typed, so a
    /// change to either moves the threshold with it.
    /// </summary>
    public static double RailLabelWidth => MinRoomWidth + DesignTokens.RailWidthExpanded;

    /// <summary>
    /// Axis 2's threshold: a list+detail room collapses to one pane once the room itself
    /// is under the width its two panes need.
    ///
    /// **640 → 700 in E-3 PR 3, and a PICTURE is what moved it.** The old number came from
    /// <c>HistoryWindow</c>'s shipped 330-wide list beside its detail — a real measured
    /// pair, and the wrong room's. This axis had no consumer at all from PR 1 until the
    /// Quests room arrived, and that room's list is <b>400</b> wide (Gate 2's shipped
    /// number, not something the shell may re-decide), so at a 640 room the detail pane got
    /// what was left: about 190 units. The shot at that width broke a quest title mid-word
    /// — *"Bone / Chips / (Kaladi / m)"* — and clipped the reward tiles, which are capped
    /// at 220 and are the widest thing that pane draws.
    ///
    /// **700 is the measured pair for the room that actually consumes it**: 400 for the
    /// list, and ~300 for a detail pane that can hold a 220-unit reward tile plus the
    /// card's own padding and border. It stays clear of
    /// <see cref="RailLabelWidth"/> (720) on purpose — the two axes must not collapse into
    /// one number, which is the whole reason they are separate constants.
    ///
    /// **The rule this follows is <see cref="MinRoomWidth"/>'s own**: the constant is a
    /// CLAIM, the screenshot is what tests it, and when a room clips at the threshold it is
    /// the number that moves — not the shot, and not a horizontal scrollbar, which would
    /// hide a layout failure behind an affordance. Widening the room was the only fix in
    /// scope; making the 400 list proportional would have been redesigning a surface this
    /// PR was signed to lift unchanged.
    /// </summary>
    public const double SplitRoomWidth = 700;

    /// <summary>Decide both axes for a window width. Callers pass the WINDOW width; the
    /// room's share is what is left after the rail, which is why the two answers cannot
    /// be computed independently by two callers.</summary>
    public static ShellLayout For(double windowWidth)
    {
        // NaN before the first measure — the same guard the 320-cap plan settled on, and
        // for the same reason: a layout pass that runs before a window has a size must
        // produce the SMALL state, never an exception and never a wrong big one.
        if (double.IsNaN(windowWidth) || windowWidth <= 0) windowWidth = MinWidth;

        var labels = windowWidth >= RailLabelWidth;
        var railWidth = labels ? DesignTokens.RailWidthExpanded : DesignTokens.RailWidthCollapsed;
        var roomWidth = windowWidth - railWidth;
        return new ShellLayout(railWidth, labels, roomWidth < SplitRoomWidth);
    }
}
