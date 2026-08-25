using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EQBuddy;

/// <summary>
/// Makes an over-the-game window incapable of taking keyboard focus, ever:
/// WS_EX_NOACTIVATE (a click lands without activating) plus WS_EX_TOOLWINDOW
/// (stays out of Alt-Tab). ShowActivated="False" in the XAML only covers Show()
/// itself — without the ex-style, the first click or drag on a chip still hands
/// the game's keyboard to EQBuddy mid-fight. Mouse interaction is unaffected:
/// DragMove, clicks and the wheel all work on a never-activated window (the
/// alert tile has dragged this way since 1.8.11).
///
/// The alert tile, grid overlay and cursor ring carry the same styles inline
/// because each also toggles WS_EX_TRANSPARENT; this helper is for windows that
/// only need the focus half.
/// </summary>
internal static class NoActivate
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x80;

    /// <summary>Call from the constructor; the styles land as soon as the HWND
    /// exists, before the window is first shown.</summary>
    public static void Attach(Window window)
    {
        window.ShowActivated = false;
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            SetWindowLong(hwnd, GwlExStyle,
                GetWindowLong(hwnd, GwlExStyle) | WsExNoActivate | WsExToolWindow);
        };
    }

    /// <summary>
    /// The Alt+Tab half on its own, for windows that must still take focus.
    ///
    /// <see cref="Attach"/> sets WS_EX_TOOLWINDOW together with WS_EX_NOACTIVATE, which is
    /// right for a chip nobody types into and wrong for every window a player clicks in:
    /// NOACTIVATE would leave the widget's search boxes and the Options tabs unable to
    /// take the keyboard. So this sets the one bit.
    ///
    /// **The hide/show is not optional when the window is already up.** Windows samples a
    /// window's taskbar and switcher membership when it is SHOWN, so flipping the style on
    /// a visible window changes the bit and nothing the player can see — which reads as a
    /// dead tick-box (trap 42's shape: present in the build, not in effect at runtime). It
    /// costs one frame, and only on the click that flips the setting.
    /// </summary>
    public static void SetToolWindow(Window window, bool on)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLong(hwnd, GwlExStyle);
        var wanted = on ? style | WsExToolWindow : style & ~WsExToolWindow;
        if (wanted == style) return;

        var visible = window.IsVisible;
        // Never re-show something that is deliberately hidden: the focus-hide feature owns
        // the widget's visibility on its own tick and would be fighting us for it.
        if (visible) window.Hide();
        SetWindowLong(hwnd, GwlExStyle, wanted);
        if (visible) window.Show();
    }

    /// <summary>Is the style actually ON the window right now? For the E2E dump, which
    /// has to report the EFFECT rather than the intent — the whole lesson of trap 42 is
    /// that a feature can be in the binary and not in force, and the two look identical
    /// from anywhere except the HWND.</summary>
    public static bool IsToolWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return hwnd != IntPtr.Zero
            && (GetWindowLong(hwnd, GwlExStyle) & WsExToolWindow) != 0;
    }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
