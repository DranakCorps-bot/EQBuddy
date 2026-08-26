using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Universal per-window text zoom: Ctrl + mouse wheel over any EQBuddy window scales its
/// content, persisted per window kind (discussion #59; David 2026-08-07: one permanent
/// mechanism instead of a slider per window). SizeToContent windows grow to fit; fixed
/// windows let their ScrollViewers absorb the overflow. Windows with a dedicated scale
/// (the widget's UiScale, the chips' ChipScale) route their wheel to that setting
/// instead, so there's exactly one number per surface.
/// </summary>
internal static class WindowZoom
{
    /// <summary><paramref name="baseWidth"/> makes the WINDOW shrink with its content,
    /// not just the text inside it. Without it a zoomed-out fixed-width window keeps its
    /// full footprint and merely renders smaller type in the same rectangle — which is
    /// why "there is no way for me to shrink the window" was a fair description of a
    /// window that already had a zoom (#186, Kemble-Kemble). Pass the XAML Width.</summary>
    public static void Attach(Window window, string key, AppSettings settings,
        double baseWidth = double.NaN)
    {
        if (window.Content is not FrameworkElement root) return;
        Apply(root, settings.WindowZooms.TryGetValue(key, out var saved) ? saved : 1.0);
        // A width the player dragged to becomes the new base; the zoom still multiplies it.
        baseWidth = WindowSizing.BaseWidth(
            settings.WindowBaseWidths.TryGetValue(key, out var storedW) ? storedW : null, baseWidth);
        ApplyWidth(window, baseWidth, settings.WindowZooms.TryGetValue(key, out var w) ? w : 1.0);
        window.PreviewMouseWheel += (_, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            e.Handled = true;
            var current = settings.WindowZooms.TryGetValue(key, out var c) ? c : 1.0;
            var next = WindowZoomMath.Step(current, e.Delta);
            settings.WindowZooms[key] = next;
            Apply(root, next);
            ApplyWidth(window, baseWidth, next);
            settings.Save();
        };
    }

    private static void ApplyWidth(Window window, double baseWidth, double zoom)
    {
        if (double.IsNaN(baseWidth)) return;
        window.Width = Math.Round(baseWidth * zoom);
    }

    /// <summary>
    /// Let a theme window be RESIZED, and remember the size.
    ///
    /// All four shipped `ResizeMode="NoResize"` because they draw their own chrome, and
    /// nobody put the resize back — David found it on 2026-08-21 trying to make the Kills
    /// &amp; Drops tab short enough to scroll. A review surface that decides for you how
    /// much screen it is worth is the wrong shape for a review surface.
    ///
    /// **Setting `CanResize` was not enough and nobody noticed for four days.** A
    /// WindowStyle=None + AllowsTransparency window has no non-client area, so there is no
    /// border to grab: the mode said resizable and the mouse could not do it. The
    /// WM_NCHITTEST hook below is what actually makes it true.
    ///
    /// **It lives here because this class already owns width.** Ctrl+wheel sets
    /// `Width = baseWidth x zoom` on every step, so a second thing writing Width would be
    /// two owners of one value. A drag therefore stores a new BASE width and the zoom goes
    /// on multiplying it; UI.Shared/WindowSizing does the arithmetic and the sanity checks,
    /// where they are unit-tested.
    ///
    /// **The height FOLLOWS the content until the player grabs an edge**, and not one moment
    /// sooner. It used to be pinned at `ContentRendered` — the first frame — which for a
    /// replay-fed body is a frame with every child collapsed: the Progress Experience tab
    /// pinned ~203px and scrolled forever after, for three releases. The honest trigger is
    /// WM_NCLBUTTONDOWN on a resize border, because a window nobody has grabbed yet has no
    /// player-chosen size to remember. That also makes the saved height meaningful: it is
    /// written on close ONLY once SizeToContent is Manual, so a window you never dragged
    /// cannot come back owned.
    ///
    /// Persisted on CLOSE, not per drag: a save writes the whole settings file from the
    /// snapshot taken at load (trap 13), and doing that on every mouse-move would be a
    /// writer fighting every other writer in the app.
    /// </summary>
    public static void AllowResize(Window window, string key, AppSettings settings)
    {
        window.ResizeMode = ResizeMode.CanResize;
        window.MinWidth = Math.Max(window.MinWidth, WindowSizing.MinWidth);
        window.MinHeight = Math.Max(window.MinHeight, WindowSizing.MinHeight);

        if (settings.WindowHeights.TryGetValue(key, out var savedHeight)
            && WindowSizing.IsSaneHeight(savedHeight))
        {
            window.SizeToContent = SizeToContent.Manual;
            window.Height = savedHeight;
        }
        // **A frameless window has NO resize borders, so CanResize alone does nothing.**
        // These windows are WindowStyle=None + AllowsTransparency=True: WPF gives them no
        // non-client area at all, so there is nothing for the mouse to grab and every one
        // of them has been "resizable" on paper and immovable in practice. David, 2026-08-25,
        // testing 1.99.11: *"Please let me resize the Progress popout window."*
        //
        // The fix is not new — `BreakoutWindow` has carried it since 2026-08-06 (his identical
        // report then: *"I still can't resize the loot window"*). It is lifted here so every
        // window that opts into AllowResize gets it, instead of one window knowing the trick.
        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is HwndSource src) src.AddHook(Hook);
        };

        window.Closed += (_, _) =>
        {
            var zoom = settings.WindowZooms.TryGetValue(key, out var z) && z > 0 ? z : 1.0;
            if (WindowSizing.BaseWidthToStore(window.Width, zoom) is { } basis)
                settings.WindowBaseWidths[key] = basis;
            // Only once the player has actually taken the size. Persisting a
            // content-driven height would make the next launch open OWNED at whatever the
            // content happened to measure at close — the old pin coming back through the
            // settings file.
            if (window.SizeToContent == SizeToContent.Manual
                && WindowSizing.HeightToStore(window.ActualHeight) is { } h)
                settings.WindowHeights[key] = h;
            settings.Save();
        };

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WmNcHitTest:
                {
                    // lParam: screen coords, low word X, high word Y (signed for multi-monitor).
                    var x = (short)((long)lParam & 0xFFFF);
                    var y = (short)(((long)lParam >> 16) & 0xFFFF);
                    var p = window.PointFromScreen(new Point(x, y));
                    var hit = ResizeZones.Hit(p.X, p.Y, window.ActualWidth, window.ActualHeight,
                        BreakdownRowLayout.ResizeEdge, BreakdownRowLayout.ResizeCorner);
                    if (hit != 0) { handled = true; return hit; }
                    break;
                }
                case WmNcLButtonDown when (long)wParam is >= HtLeft and <= HtBottomRight:
                    // **This is where the height stops following the content and becomes the
                    // player's** — the honest trigger, and the reason the ContentRendered pin
                    // is gone. The old code sampled a height on the FIRST FRAME, which for a
                    // replay-fed body is a frame with every child collapsed; the Progress
                    // Experience tab pinned ~203px and scrolled forever after. Taking the size
                    // at the moment the player grabs an edge cannot sample an empty window,
                    // and it must happen BEFORE the native size loop starts or layout snaps
                    // the height back the instant it runs.
                    if (window.SizeToContent != SizeToContent.Manual)
                    {
                        var w = window.ActualWidth;
                        var h = window.ActualHeight;
                        window.SizeToContent = SizeToContent.Manual;
                        window.Width = w;
                        window.Height = h;
                    }
                    break;
            }
            return IntPtr.Zero;
        }
    }

    private const int WmNcHitTest = 0x84;
    private const int WmNcLButtonDown = 0xA1;
    private const int HtLeft = 10, HtBottomRight = 17;

    /// <summary>For windows whose scale already lives in a named setting: wheel just
    /// drives that setter (which applies, clamps, and persists on its own).</summary>
    public static void Route(Window window, Func<double> get, Action<double> set)
    {
        window.PreviewMouseWheel += (_, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            e.Handled = true;
            set(WindowZoomMath.Step(get(), e.Delta));
        };
    }

    private static void Apply(FrameworkElement root, double zoom) =>
        root.LayoutTransform = Math.Abs(zoom - 1.0) < 0.001
            ? null
            : new ScaleTransform(zoom, zoom);
}
