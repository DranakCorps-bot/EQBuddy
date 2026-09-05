namespace EQBuddy.UI.Shared;

/// <summary>
/// The hide-the-overlay decision, pure so tests can walk the truth table without a
/// real foreground window. Two independent opt-ins share it (#41, #114):
///
///  - "Hide while the game runs unfocused": alt-tab away and the overlay yields the
///    corner; the game closed entirely keeps it visible (reviewing history, drops,
///    quests between sessions must stay possible).
///  - "Hide while the game isn't running": the overlay exists only while playing
///    (Frankthetankk's #114). Launching the game brings it back; so does launching
///    EQBuddy again — the second copy asks the running one to surface.
///
/// EQBuddy's own windows having focus always wins: hiding the thing the player is
/// clicking reads as broken, and it's also the escape hatch that keeps Options
/// reachable while the game is closed.
/// </summary>
public static class FocusHide
{
    public static bool Decide(
        bool hideWhenUnfocused, bool hideWhenNotRunning,
        bool foregroundIsSelf, bool foregroundIsGame, bool gameRunning)
    {
        if (!hideWhenUnfocused && !hideWhenNotRunning) return false;
        if (foregroundIsSelf) return false;   // the player is using EQBuddy itself
        if (foregroundIsGame) return false;   // playing — showing is the overlay's job
        return gameRunning ? hideWhenUnfocused : hideWhenNotRunning;
    }

    /// <summary>
    /// Does this window follow the widget when the widget hides?
    ///
    /// The answer is YES by default and the exceptions are named, deliberately that way
    /// round: wizen opened #189 because the Quest Tracker stayed on screen after the
    /// widget vanished, and the reason it did is that nothing ever said it should — the
    /// hide took the widget and the surfaces with their own per-tick gate, and every
    /// window opened from a menu was simply never considered. A list of what DOES follow
    /// would have the same defect the moment the next window is written.
    ///
    /// The exceptions are the surfaces that are already driven by the widget's own tick,
    /// where a second hand on the switch is the bug: the breakouts (which re-derive their
    /// visibility from settings plus this same flag every tick, so hiding one here and
    /// showing it back could resurrect one the player dismissed with its ✕), the one chip
    /// row (whose families are gated per tick by ChipStackPlan, which reads this same
    /// flag), and the transient alert tile. The click-through and cursor-ring overlays
    /// belong to the GAME window rather than to the widget, so the widget's visibility is
    /// not theirs to answer.
    ///
    /// Compared on the window's type NAME rather than its type, so the rule lives here
    /// with the rest of the decision and both UIs — whose windows are different classes
    /// entirely — read one list (#122 and #152 reached Linux through exactly that gap).
    /// </summary>
    public static bool FollowsWidgetHide(string windowTypeName) =>
        windowTypeName is not (
            "MainWindow"            // the widget itself; hidden directly
            or "BreakoutWindow"     // re-derived every tick from DisabledBreakouts + this flag
            or "HudChipRowWindow"  // the one chip row; ChipStackPlan gates it per family
            or "ClickThroughChip"
            or "AlertWindow"        // transient, dismisses itself
            or "CursorRingWindow"   // draws on the GAME, not beside the widget
            or "GridOverlayWindow");

    /// <summary>
    /// Can this platform answer "which window is in front?" at all? Windows and macOS
    /// can; X11 and Wayland have no portable probe, so <see cref="Decide"/> is never
    /// even reached there and both tick-boxes do nothing.
    ///
    /// This exists so the UI can SAY that (David, 2026-08-16, on #169). The settings
    /// save correctly now that Linux stopped running two copies of EQBuddy — which
    /// means without this note they would tick, persist, and still hide nothing, and a
    /// setting that keeps its state while doing nothing is the silent no-op CLAUDE.md
    /// treats as broken. Implementing the probe is the other answer, and is not ruled
    /// out; this is the honest interim.
    /// </summary>
    public static bool ForegroundProbeAvailable =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    /// <summary>What Options prints under the two tick-boxes where the platform can't
    /// answer — empty where it can, so the note never appears on Windows or macOS.
    /// Names the reason rather than just refusing: a player who knows it is X11's
    /// missing answer, not a bug in EQBuddy, doesn't spend an evening on it.</summary>
    public static string UnavailableNote =>
        ForegroundProbeAvailable
            ? ""
            : "Not available on Linux yet — X11 and Wayland offer no way to ask which "
              + "window is in front, so the widget stays visible. Your choice is saved "
              + "and will start working if that changes.";
}
