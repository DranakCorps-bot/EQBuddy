using System.Windows;
using System.Windows.Controls;
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
    /// **The height FOLLOWS the content until the player takes it** (Fable 5's ruling,
    /// 2026-08-23). It used to be pinned on `ContentRendered`, which fires on the first
    /// frame — and for a window whose body is filled by the log replay that is a frame with
    /// nothing in it. The Progress window's Experience tab pinned ~203px and scrolled
    /// forever after, for three releases (measured: 203 pinned, 389 unpinned).
    ///
    /// Every fix that picked a BETTER instant to sample was wrong for some window, because
    /// "the content has arrived" is not an event WPF gives you. So there is no instant:
    /// `WindowHeightFollower` (UI.Shared, unit-tested — the WPF layer has no test project)
    /// tracks the content, and the first size change we did not cause hands the axis over
    /// permanently. Both halves of what this method wanted are kept: size-to-content for
    /// players who never touch the edge, a remembered height for players who drag.
    ///
    /// Persisted on CLOSE, not per drag: a save writes the whole settings file from the
    /// snapshot taken at load (trap 13), and doing that on every mouse-move would be a
    /// writer fighting every other writer in the app. **And only when OWNED** — persisting
    /// a followed height would make the next launch start owned at whatever the content
    /// happened to measure at close, which is the pin coming back through the settings file.
    /// </summary>
    public static void AllowResize(Window window, string key, AppSettings settings)
    {
        window.ResizeMode = ResizeMode.CanResize;
        window.MinWidth = Math.Max(window.MinWidth, WindowSizing.MinWidth);
        window.MinHeight = Math.Max(window.MinHeight, WindowSizing.MinHeight);

        var follower = new WindowHeightFollower();
        var selfSet = false;

        if (settings.WindowHeights.TryGetValue(key, out var savedHeight)
            && WindowSizing.IsSaneHeight(savedHeight))
        {
            window.SizeToContent = SizeToContent.Manual;
            window.Height = savedHeight;
            follower.StartOwned(savedHeight);
        }

        // One wiring point, which is what keeps this out of the four call sites. While
        // following, SizeToContent stays Height so the first pass costs nothing and there
        // is no flash; the first emit switches to Manual and takes the axis, which is the
        // only mode a vertical drag works in.
        window.LayoutUpdated += (_, _) =>
        {
            if (follower.Owned) return;
            var scroller = FirstScroller(window);
            var natural = scroller is null
                ? window.ActualHeight
                : WindowHeightFollower.Natural(
                    window.ActualHeight, scroller.ExtentHeight, scroller.ViewportHeight);
            if (follower.Desired(natural, window.MaxHeight) is not { } target) return;

            selfSet = true;
            window.SizeToContent = SizeToContent.Manual;
            window.Height = target;
            selfSet = false;
        };

        // HeightChanged only: Ctrl+wheel drives WIDTH through this same class, and letting a
        // zoom step count as "the player took the height" would end following by accident.
        window.SizeChanged += (_, e) =>
        {
            if (e.HeightChanged) follower.OnSizeChanged(e.NewSize.Height, selfSet);
        };

        window.Closed += (_, _) =>
        {
            var zoom = settings.WindowZooms.TryGetValue(key, out var z) && z > 0 ? z : 1.0;
            if (WindowSizing.BaseWidthToStore(window.Width, zoom) is { } basis)
                settings.WindowBaseWidths[key] = basis;
            if (follower.OwnedHeight is { } owned
                && WindowSizing.HeightToStore(owned) is { } h)
                settings.WindowHeights[key] = h;
            settings.Save();
        };
    }

    /// <summary>The window's scrolling body, or null. Found by walking rather than by name
    /// so the four call sites stay call sites — asking each window to hand its own scroller
    /// in is the four-site wiring this design exists to avoid, and the fourth one gets
    /// missed (trap 34).</summary>
    private static ScrollViewer? FirstScroller(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv) return sv;
            if (FirstScroller(child) is { } found) return found;
        }
        return null;
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
