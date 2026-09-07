using System.Windows;
using System.Windows.Media;
using EQBuddy.Core;

namespace EQBuddy;

/// <summary>WPF adapter for <see cref="WindowPlacement"/>: checks saved positions
/// against the virtual screen (all monitors), not just the primary work area.</summary>
internal static class ScreenGuard
{
    public static bool OnScreen(double left, double top,
        double width = double.NaN, double height = double.NaN) =>
        WindowPlacement.IsReachable(left, top,
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight,
            width, height);

    /// <summary>Where a window of this size opens when the desk has a monitor beside the
    /// primary one, or null when it does not — see <see cref="WindowPlacement.SecondaryOrigin"/>
    /// for which arrangements answer and which refuse. The adapter reads the same
    /// <c>SystemParameters</c> the guard above does, so both are in <c>Window.Left</c>'s own
    /// unit space and neither does any pixel arithmetic of its own (trap 1).</summary>
    public static (double Left, double Top)? SecondaryOrigin(double width, double height) =>
        WindowPlacement.SecondaryOrigin(
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight,
            SystemParameters.PrimaryScreenWidth, width, height);

    /// <summary>
    /// THE WORK AREA OF THE MONITOR A GIVEN POINT IS ON, in <c>Window.Left</c>'s own units —
    /// OE-8's named implement check (the plan's §2.2).
    ///
    /// <c>SystemParameters.WorkArea</c> is the PRIMARY monitor's, and a parked companion
    /// window clamped against it would be yanked off a secondary-monitor park the first time
    /// a chicklet arrived. That is the failure <c>WidgetMetrics.RightAnchoredLeft</c>
    /// refuses to clamp at all to avoid; a park has an owner's chosen corner to protect, so
    /// it clamps — against the right monitor.
    ///
    /// **THE UNIT CONVERSION IS THE WHOLE OF THIS METHOD, AND IT IS TRAP 1'S SUBJECT.**
    /// <c>Screen.WorkingArea</c> is in PHYSICAL PIXELS; <c>Window.Left</c>, <c>ActualWidth</c>
    /// and everything <c>HudChipRow</c> computes are DIPs. Mixing them is invisible at 100%
    /// and wrong everywhere else (#144). Both directions go through the SAME
    /// <c>CompositionTarget</c> transform, so a desk at 150% converts consistently; before a
    /// window has a presentation source there is no transform to ask, and the primary work
    /// area — which is what the caller had before OE-8 — is the honest fallback rather than a
    /// guess.
    ///
    /// The arithmetic that USES this is <c>HudChipRow.ParkedPlacement</c>'s, unit-tested
    /// without a window; nothing here does a sum of its own beyond the conversion.
    /// </summary>
    public static Rect WorkAreaAt(Window window, double left, double top)
    {
        if (PresentationSource.FromVisual(window)?.CompositionTarget is not { } target)
            return SystemParameters.WorkArea;
        if (!double.IsFinite(left) || !double.IsFinite(top)) return SystemParameters.WorkArea;

        var device = target.TransformToDevice.Transform(new Point(left, top));
        var area = System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point((int)Math.Round(device.X), (int)Math.Round(device.Y)))
            .WorkingArea;
        var topLeft = target.TransformFromDevice.Transform(new Point(area.Left, area.Top));
        var bottomRight = target.TransformFromDevice.Transform(new Point(area.Right, area.Bottom));
        return new Rect(topLeft, bottomRight);
    }
}
