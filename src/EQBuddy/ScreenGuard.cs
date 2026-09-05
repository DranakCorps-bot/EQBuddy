using System.Windows;
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
}
