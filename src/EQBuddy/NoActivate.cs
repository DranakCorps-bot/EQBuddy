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
    private const int WsExAppWindow = 0x00040000;

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
        // A NOACTIVATE window owns its own TOOLWINDOW bit: chips, the alert tile, the
        // grid overlay and the cursor ring all set both styles deliberately so they never
        // appear in Alt+Tab. This feature must only ever ADD the bit to focusable
        // windows — turning the setting OFF must not strip what those windows set for
        // themselves, or every chip joins the switcher the moment it loads.
        if (!on && (style & WsExNoActivate) != 0) return;
        var wanted = on ? style | WsExToolWindow : style & ~WsExToolWindow;
        if (wanted == style) return;

        var visible = window.IsVisible;
        // Never re-show something that is deliberately hidden: the focus-hide feature owns
        // the widget's visibility on its own tick and would be fighting us for it.
        if (visible) window.Hide();
        SetWindowLong(hwnd, GwlExStyle, wanted);
        if (visible) window.Show();
    }

    /// <summary>
    /// Arm every OTHER window as it loads, so the setting means what it says.
    ///
    /// The satellites all ship `ShowInTaskbar="False"`, which gives them a hidden owner and
    /// usually keeps them out of the switcher on its own — but "usually" is a claim about a
    /// shell behaviour nobody here has measured, and the cost of being wrong is the feature
    /// covering the widget and nothing else. Setting the bit is a fact rather than an
    /// inference.
    ///
    /// `SourceInitialized` is a plain CLR event and cannot be class-handled; `Loaded` is
    /// routed and can. Post-show is fine for these because they are not the taskbar case,
    /// and <see cref="SetToolWindow"/> re-shows if it has to.
    /// </summary>
    public static void ArmSatellites(Window main, Func<bool> wanted) =>
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window w && !ReferenceEquals(w, main)) SetToolWindow(w, wanted());
            }));

    /// <summary>
    /// The MAIN window's application: the taskbar bit first, then the style.
    ///
    /// This is the half the feature shipped without, and why it read as a dead tick-box on
    /// the widget while every satellite hid correctly (Hateborne, 2026-09-03): WPF answers
    /// ShowInTaskbar=true by asserting WS_EX_APPWINDOW on the HWND, and APPWINDOW
    /// OVERRIDES TOOLWINDOW for switcher membership. MainWindow is the one window shipping
    /// ShowInTaskbar="True", so the style landed and did nothing there. Dropping the
    /// taskbar button is the cost <see cref="UI.Shared.AltTabPolicy.TaskbarWarning"/> has
    /// promised the player all along; the satellites are always ShowInTaskbar="False" and
    /// must not be touched.
    /// </summary>
    public static void ApplyMain(Window main, bool on)
    {
        main.ShowInTaskbar = UI.Shared.AltTabPolicy.MainWindowShowsInTaskbar(on);
        SetToolWindow(main, on);
    }

    /// <summary>Apply to every open window at once, for the moment the box is flipped — a
    /// setting that waits for a relaunch is indistinguishable from a broken one.</summary>
    public static void ApplyToAll(Window main, bool on)
    {
        foreach (Window w in Application.Current.Windows) SetToolWindow(w, on);
        ApplyMain(main, on);
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

    /// <summary>Is WS_EX_APPWINDOW on the window right now? The bit WPF asserts for
    /// ShowInTaskbar=true, and the one that OVERRIDES the tool-window style for switcher
    /// membership — so `IsToolWindow=1, HasAppWindowStyle=1` is precisely the "present in
    /// the build, not in effect" state trap 42 exists for, and the E2E dump has to be
    /// able to see it.</summary>
    public static bool HasAppWindowStyle(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return hwnd != IntPtr.Zero
            && (GetWindowLong(hwnd, GwlExStyle) & WsExAppWindow) != 0;
    }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
