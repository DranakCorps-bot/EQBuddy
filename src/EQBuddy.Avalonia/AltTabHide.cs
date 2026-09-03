using Avalonia.Controls;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// Platform dispatch for "keep EQBuddy out of the window switcher", the way
/// <see cref="ClickThrough"/> dispatches its own job.
///
/// Only Windows has a per-window opt-out (WS_EX_TOOLWINDOW). macOS and Linux hand the
/// switcher to the desktop, so there is nothing to set and this is a no-op — which
/// <see cref="AltTabPolicy.UnavailableNote"/> says out loud under the tick-box, rather
/// than leaving a saved choice that quietly does nothing (the #169 rule).
/// </summary>
internal static class AltTabHide
{
    public static void Apply(Window window, bool on)
    {
        if (!AltTabPolicy.Available) return;
        // The main widget is the ONE window owning a taskbar button, and ShowInTaskbar
        // is asserted as WS_EX_APPWINDOW on the HWND — which OVERRIDES the tool-window
        // style for switcher membership, so the style alone hid every satellite and
        // never the widget itself (Hateborne, 2026-09-03). First, so the tool-window
        // flip below re-samples with the taskbar bit already decided.
        if (window is MainWindow)
            window.ShowInTaskbar = AltTabPolicy.MainWindowShowsInTaskbar(on);
        if (OperatingSystem.IsWindows()) WinClickThrough.SetToolWindow(window, on);
    }

    /// <summary>Every open window at once, for the moment the setting is flipped.</summary>
    public static void ApplyAll(bool on, IEnumerable<Window> windows)
    {
        if (!AltTabPolicy.Available) return;
        foreach (var w in windows) Apply(w, on);
    }
}
