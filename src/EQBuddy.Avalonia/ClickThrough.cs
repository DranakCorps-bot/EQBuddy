using Avalonia.Controls;

namespace EQBuddy.Avalonia;

/// <summary>
/// Platform dispatch for click-through, so callers can ask for it without knowing which
/// windowing system is underneath. Each backend reports whether it actually engaged —
/// callers must not flip their own state on a false, or the menu ends up claiming clicks
/// pass through when they don't.
/// </summary>
internal static class ClickThrough
{
    public static bool Set(Window window, bool enabled)
    {
        if (OperatingSystem.IsMacOS()) return MacClickThrough.Set(window, enabled);
        if (OperatingSystem.IsLinux()) return X11ClickThrough.Set(window, enabled);

        App.LogError("Click-through unavailable: no implementation for this platform.");
        return false;
    }
}
