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

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
