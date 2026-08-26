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
    /// border to grab: the mode said resizable and the mouse could not do it.
    /// <see cref="FramelessResize"/> owns the WM_NCHITTEST hook that actually makes it
    /// true — found independently by Hateborne (#238) and by 5b0f331 on the same day; the
    /// merged shape keeps this repo's follow-until-grab ownership and #238's visible grip,
    /// drag-flag persistence and junk-height healing.
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
    /// written on close ONLY once <see cref="FramelessResize.PlayerTookHeight"/> says the
    /// player took it, so a window you never dragged cannot come back owned.
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
        window.Closed += (_, _) =>
        {
            var zoom = settings.WindowZooms.TryGetValue(key, out var z) && z > 0 ? z : 1.0;
            if (WindowSizing.BaseWidthToStore(window.Width, zoom) is { } basis)
                settings.WindowBaseWidths[key] = basis;
            // ONLY a height the player dragged to (or restored — MarkPlayerSized above).
            // Persisting a content-driven height would make the next launch open OWNED at
            // whatever the content happened to measure at close — the old pin coming back
            // through the settings file. And a value nobody chose is REMOVED, so a profile
            // carrying one from the 08-21..08-25 builds heals itself even after
            // MigrateWindowHeights has run once.
            if (FramelessResize.PlayerTookHeight(window)
                && WindowSizing.HeightToStore(window.ActualHeight) is { } h)
                settings.WindowHeights[key] = h;
            else
                settings.WindowHeights.Remove(key);
            settings.Save();
        };
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
