using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace EQBuddy.Avalonia;

/// <summary>
/// Win32 sibling of <see cref="MacClickThrough"/>/<see cref="X11ClickThrough"/>: the
/// extended-style recipe the WPF app uses (WS_EX_TRANSPARENT + WS_EX_LAYERED drops the
/// window out of hit-testing). The <see cref="ClickThrough"/> dispatcher predates a
/// Windows Avalonia build and still logs "unavailable" there — folding this in is a
/// one-line change flagged for the integration pass (that file is outside this package).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WinClickThrough
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x20;
    private const long WsExToolWindow = 0x80;
    private const long WsExLayered = 0x80000;
    private const long WsExNoActivate = 0x08000000;
    private const uint LwaAlpha = 0x2;

    // Windows whose WS_EX_LAYERED bit THIS code added (WPF never needs this bookkeeping:
    // its windows are already layered, so it toggles only WS_EX_TRANSPARENT). Tracking it
    // lets Set(false) restore exactly the pre-toggle style instead of stripping a layered
    // bit Avalonia's transparency machinery may own.
    private static readonly HashSet<IntPtr> _layeredAddedByUs = [];

    public static bool Set(Window window, bool enabled)
    {
        if (Handle(window) is not { } hwnd) return false;
        try
        {
            var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            if (enabled)
            {
                var addLayered = (style & WsExLayered) == 0;
                SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style | WsExTransparent | WsExLayered));
                if (addLayered)
                {
                    // A window newly made layered via SetWindowLong stops rendering until
                    // its layered attributes are set; full alpha keeps it painting as-is.
                    SetLayeredWindowAttributes(hwnd, 0, 255, LwaAlpha);
                    _layeredAddedByUs.Add(hwnd);
                }
            }
            else
            {
                style &= ~WsExTransparent;
                if (_layeredAddedByUs.Remove(hwnd)) style &= ~WsExLayered;
                SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));
            }
            return true;
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            return false;
        }
    }

    /// <summary>The one-way overlay recipe (cursor ring, alignment grid — same styles
    /// WPF's AlertWindow uses): transparent to the mouse, never activated, invisible
    /// to Alt-Tab. These windows only ever close, so there is no undo path.</summary>
    public static bool SetOverlay(Window window)
    {
        if (Handle(window) is not { } hwnd) return false;
        try
        {
            var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            var addLayered = (style & WsExLayered) == 0;
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(
                style | WsExTransparent | WsExLayered | WsExNoActivate | WsExToolWindow));
            // Same newly-layered rendering trap as Set: without attributes the overlay
            // would go invisible the moment SetWindowLong adds the layered bit.
            if (addLayered) SetLayeredWindowAttributes(hwnd, 0, 255, LwaAlpha);
            return true;
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            return false;
        }
    }

    /// <summary>
    /// The Alt+Tab bit on its own, and reversible — <see cref="SetOverlay"/> also blocks
    /// activation, which is right for a cursor ring and wrong for every window a player
    /// clicks in.
    ///
    /// Windows samples switcher and taskbar membership when a window is SHOWN, so a live
    /// flip needs a hide/show to take. Without it the style changes and the player sees
    /// nothing, which reads as a dead tick-box (trap 42).
    /// </summary>
    public static bool SetToolWindow(Window window, bool on)
    {
        if (Handle(window) is not { } hwnd) return false;
        try
        {
            var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            var wanted = on ? style | WsExToolWindow : style & ~WsExToolWindow;
            if (wanted == style) return true;

            var visible = window.IsVisible;
            if (visible) window.Hide();
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(wanted));
            if (visible) window.Show();
            return true;
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            return false;
        }
    }

    private static IntPtr? Handle(Window window)
    {
        if (window.TryGetPlatformHandle() is { Handle: not 0 } handle) return handle.Handle;
        App.LogError("Click-through unavailable: Avalonia did not expose a native window handle.");
        return null;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);
}
