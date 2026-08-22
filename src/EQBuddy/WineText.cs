using System.Windows;
using System.Windows.Media;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Applies <see cref="TextRenderingPolicy"/> to WPF. The reasoning lives with the policy;
/// this file is only the wiring, the same split as <see cref="WineFonts"/>.
///
/// **It does the job twice, on purpose.** The obvious one line — override the metadata
/// default on <see cref="Window"/> and let the inherited attached property carry it down
/// — is not reliable, and the first build of this fix shipped exactly that and did not
/// change what the reporter saw. Property-value inheritance propagates a value that has
/// been SET; a metadata default is not a set value, so each descendant goes on resolving
/// its own default from its own type's metadata, which is still Ideal. The window changes
/// and nothing inside it does, which is indistinguishable from the fix not being in the
/// build (and cost a round trip proving it was).
///
/// So:
///   1. the default is overridden on <see cref="FrameworkElement"/>, which makes Display
///      the answer for every element on its own account, with no inheritance walk needed;
///   2. and a class handler SETS it on each window as it loads, which is the one form
///      inheritance is guaranteed to carry to children.
/// Either alone would probably do. Both cost one call each, and the failure they prevent
/// is invisible from this side of the machine.
/// </summary>
internal static class WineText
{
    /// <summary>Must run before any Window is constructed — metadata overrides only
    /// affect instances created afterwards, so a window built earlier would silently
    /// keep the old default. App.OnStartup is the only place that is guaranteed.</summary>
    public static void ApplyIfNeeded()
    {
        if (Resolve() != TextLayoutMode.Display) return;

        // Text cosmetics must never stop startup (the WineFonts rule), and these two are
        // independent: if the metadata override is refused, the class handler still lands.
        Attempt(() => TextOptions.TextFormattingModeProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                TextFormattingMode.Display,
                FrameworkPropertyMetadataOptions.Inherits)));

        Attempt(() => EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window)
                    TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
            })));
    }

    /// <summary>The mode this process should be using, for the probe to report. Kept
    /// beside the application of it so the diagnostic can never drift from the decision
    /// it is meant to be checking.</summary>
    public static TextLayoutMode Resolve() => TextRenderingPolicy.Decide(
        WineFonts.IsRunningUnderWine(),
        Environment.GetEnvironmentVariable(TextRenderingPolicy.OverrideVariable));

    private static void Attempt(Action action)
    {
        try { action(); }
        catch (Exception) { /* see above: never fatal */ }
    }
}
