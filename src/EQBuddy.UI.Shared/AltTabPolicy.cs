namespace EQBuddy.UI.Shared;

/// <summary>
/// Whether "keep EQBuddy out of Alt+Tab" can do anything here, and what to say when it
/// cannot.
///
/// It is one flag on Windows — `WS_EX_TOOLWINDOW` — and that flag removes the window
/// from the Alt+Tab switcher AND from the taskbar. The two are not separable, so the
/// setting has to say so: the tray icon becomes the only way back to a hidden widget,
/// and a control that quietly removes someone's way back is the kind of thing this repo
/// treats as broken rather than as a trade-off.
///
/// Written beside <see cref="FocusHide"/> and to the same rule it set for #169: a
/// platform that cannot honour a tick-box gets a sentence naming the reason, not a box
/// that persists a choice and does nothing. macOS and Linux have no Alt+Tab in the
/// Windows sense — the compositor owns the switcher and there is no per-window opt-out
/// to set — so the answer there is "no", said out loud.
/// </summary>
public static class AltTabPolicy
{
    public static bool Available => OperatingSystem.IsWindows();

    /// <summary>What Options prints under the tick-box. Empty where it works, so the
    /// note never appears on Windows.</summary>
    public static string UnavailableNote =>
        Available
            ? ""
            : "Not available on this platform — the window switcher is the desktop's to "
              + "decide, with no per-window opt-out for an app to set. Your choice is "
              + "saved and will start working if that changes.";

    /// <summary>The cost, said where the player is choosing. One flag, both effects.</summary>
    public const string TaskbarWarning =
        "Also removes EQBuddy's taskbar button — Windows treats the two as one setting. "
        + "The tray icon is how you get the widget back.";
}
