using System.Windows;
using System.Windows.Media;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Applies <see cref="TextRenderingPolicy"/> to WPF. The reasoning lives with the policy;
/// this file is only the wiring, the same split as <see cref="WineFonts"/>.
///
/// It overrides the metadata rather than setting the property on each window because
/// TextOptions.TextFormattingMode is an INHERITED attached property: give Window a new
/// default and every window, card, breakout and popup in the app inherits it, including
/// the ones built in code after startup. A per-window setter would have to be remembered
/// by every future window — which is the shape of defect this codebase keeps paying for
/// (traps 15, 29, 36), and it is avoidable here for one call.
/// </summary>
internal static class WineText
{
    /// <summary>Must run before any Window is constructed — metadata overrides only
    /// affect instances created afterwards, so a window built earlier would silently
    /// keep the old default. App.OnStartup is the only place that is guaranteed.</summary>
    public static void ApplyIfNeeded()
    {
        var mode = TextRenderingPolicy.Decide(
            WineFonts.IsRunningUnderWine(),
            Environment.GetEnvironmentVariable(TextRenderingPolicy.OverrideVariable));

        // Ideal is already WPF's default; overriding metadata to the value it already
        // has would be a no-op with a small risk of throwing, so don't.
        if (mode != TextLayoutMode.Display) return;

        try
        {
            TextOptions.TextFormattingModeProperty.OverrideMetadata(
                typeof(Window),
                new FrameworkPropertyMetadata(
                    TextFormattingMode.Display,
                    FrameworkPropertyMetadataOptions.Inherits));
        }
        catch (Exception)
        {
            // Text cosmetics must never stop startup — same rule as WineFonts. A failure
            // here leaves the app on Ideal, which is the behaviour it shipped with.
        }
    }
}
