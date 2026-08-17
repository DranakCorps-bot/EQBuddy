using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// Universal per-window text zoom: Ctrl + mouse wheel over any EQBuddy window scales its
/// content, persisted per window kind (discussion #59; David 2026-08-07: one permanent
/// mechanism instead of a slider per window). SizeToContent windows grow to fit; fixed
/// windows let their ScrollViewers absorb the overflow. Mirrors the WPF WindowZoom;
/// Avalonia has no LayoutTransform on plain controls, so the window's content is wrapped
/// in a LayoutTransformControl once at attach time.
/// </summary>
internal static class WindowZoom
{
    /// <summary><paramref name="baseWidth"/> makes the WINDOW shrink with its content
    /// rather than rendering smaller type in the same rectangle (#186). Pass the
    /// window's declared Width.</summary>
    public static void Attach(Window window, string key, AppSettings settings,
        double baseWidth = double.NaN)
    {
        if (window.Content is not Control root) return;
        window.Content = null;   // a control can't have two logical parents
        var host = new LayoutTransformControl { Child = root };
        window.Content = host;
        Apply(host, settings.WindowZooms.TryGetValue(key, out var saved) ? saved : 1.0);
        ApplyWidth(window, baseWidth, saved);
        // Tunnel = WPF's PreviewMouseWheel: the zoom wins over any ScrollViewer beneath.
        window.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) =>
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            e.Handled = true;
            var current = settings.WindowZooms.TryGetValue(key, out var c) ? c : 1.0;
            var next = WindowZoomMath.Step(current, Math.Sign(e.Delta.Y));
            settings.WindowZooms[key] = next;
            Apply(host, next);
            ApplyWidth(window, baseWidth, next);
            settings.Save();
        }, RoutingStrategies.Tunnel);
    }

    private static void ApplyWidth(Window window, double baseWidth, double zoom)
    {
        if (double.IsNaN(baseWidth)) return;
        window.Width = Math.Round(baseWidth * zoom);
    }

    /// <summary>For windows whose scale already lives in a named setting: wheel just
    /// drives that setter (which applies, clamps, and persists on its own).</summary>
    public static void Route(Window window, Func<double> get, Action<double> set)
    {
        window.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) =>
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            e.Handled = true;
            set(WindowZoomMath.Step(get(), Math.Sign(e.Delta.Y)));
        }, RoutingStrategies.Tunnel);
    }

    private static void Apply(LayoutTransformControl host, double zoom) =>
        host.LayoutTransform = Math.Abs(zoom - 1.0) < 0.001
            ? null
            : new ScaleTransform(zoom, zoom);
}
