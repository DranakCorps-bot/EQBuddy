using System.Windows;
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
    /// **It lives here because this class already owns width.** Ctrl+wheel sets
    /// `Width = baseWidth x zoom` on every step, so a second thing writing Width would be
    /// two owners of one value. A drag therefore stores a new BASE width and the zoom goes
    /// on multiplying it; UI.Shared/WindowSizing does the arithmetic and the sanity checks,
    /// where they are unit-tested.
    ///
    /// SizeToContent is cleared on the first layout pass, not at attach: these windows open
    /// at their natural content height by design, and clearing it earlier would open them
    /// at whatever XAML happened to say. After that the player owns the height.
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
        // AND A BORDER TO GRAB: CanResize creates no non-client area on a frameless
        // window, so every window through here was resizable in settings and immovable
        // under a mouse. Here rather than at the call sites, so a window cannot join the
        // feature and miss the affordance. FramelessResize has the evidence.
        if (window.WindowStyle == WindowStyle.None) FramelessResize.Attach(window);

        if (settings.WindowHeights.TryGetValue(key, out var savedHeight)
            && WindowSizing.IsSaneHeight(savedHeight))
        {
            window.SizeToContent = SizeToContent.Manual;
            window.Height = savedHeight;
            // A stored height only exists because the player dragged for it, so the window
            // opens already owning its height — otherwise the next undragged close would
            // delete the very choice this line just restored.
            FramelessResize.MarkPlayerSized(window);
        }
        else
        {
            // Open at the natural height once, then hand the axis over.
            window.ContentRendered += Release;
        }

        window.Closed += (_, _) =>
        {
            var zoom = settings.WindowZooms.TryGetValue(key, out var z) && z > 0 ? z : 1.0;
            if (WindowSizing.BaseWidthToStore(window.Width, zoom) is { } basis)
                settings.WindowBaseWidths[key] = basis;
            // ONLY a height the player dragged to. Persisting ActualHeight unconditionally
            // recorded whatever a window happened to measure, including an empty first
            // frame — four such entries in one real profile, none of them chosen. A value
            // nobody chose is REMOVED, so a profile carrying one heals itself.
            // MigrateWindowHeights explains why they all had to go.
            if (FramelessResize.PlayerTookHeight(window)
                && WindowSizing.HeightToStore(window.ActualHeight) is { } h)
                settings.WindowHeights[key] = h;
            else
                settings.WindowHeights.Remove(key);
            settings.Save();
        };

        void Release(object? sender, EventArgs e)
        {
            window.ContentRendered -= Release;
            // ActualHeight is the measured one; assigning it before clearing SizeToContent
            // is what stops the window snapping to its XAML height for a frame.
            if (window.ActualHeight > 0) window.Height = window.ActualHeight;
            window.SizeToContent = SizeToContent.Manual;
        }
    }

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
