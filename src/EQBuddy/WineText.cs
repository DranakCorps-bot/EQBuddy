using System.Windows;
using System.Windows.Media;
using EQBuddy.Core;
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
/// been SET; a metadata default is not a set value, so each descendant went on resolving
/// its own default from its own type's metadata, which is still Ideal. The window changes
/// and nothing inside it does, which is indistinguishable from the fix not being in the
/// build (and cost a round trip proving it was). See CLAUDE.md trap 41.
///
/// So the mode is SET on each window — the one form inheritance is guaranteed to carry to
/// children — as windows load, and on every window already open at the moment the policy
/// is applied.
///
/// **The Options checkbox that used to flip it is gone** (E-2d, #277 clause (a),
/// Helm-signed 2026-09-05). It only ever appeared under Wine, so it was invisible on the
/// supported Windows artifact; <c>AppSettings.WineWholePixelText</c> survives as a
/// hand-edited settings.json knob, the same shape as its two Wine siblings, and
/// <c>EQBUDDY_TEXTMODE</c> is the override that works on either platform.
/// </summary>
internal static class WineText
{
    private static TextFormattingMode _mode = TextFormattingMode.Ideal;
    private static bool _hooked;

    /// <summary>Wire up before any window is constructed, so the first frame is already
    /// correct rather than repainting once it loads.</summary>
    public static void ApplyIfNeeded(AppSettings settings)
    {
        _mode = ToWpf(Resolve(settings));

        if (!_hooked)
        {
            _hooked = true;
            // Every window, including the ones built in code long after startup. A
            // per-window setter would have to be remembered by each future window, which
            // is the shape of defect this codebase keeps paying for (traps 15, 29, 36).
            Attempt(() => EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, _) =>
                {
                    if (sender is Window window)
                        TextOptions.SetTextFormattingMode(window, _mode);
                })));
        }

        ApplyToOpenWindows();
    }

    /// <summary>The mode this process should be using, for the probe to report. Kept
    /// beside the application of it so the diagnostic can never drift from the decision
    /// it is meant to be checking.</summary>
    public static TextLayoutMode Resolve(AppSettings settings) => TextRenderingPolicy.Decide(
        WineFonts.IsRunningUnderWine(),
        settings.WineWholePixelText,
        Environment.GetEnvironmentVariable(TextRenderingPolicy.OverrideVariable));

    private static void ApplyToOpenWindows()
    {
        Attempt(() =>
        {
            foreach (Window window in Application.Current.Windows)
                TextOptions.SetTextFormattingMode(window, _mode);
        });
    }

    private static TextFormattingMode ToWpf(TextLayoutMode mode) =>
        mode == TextLayoutMode.Display ? TextFormattingMode.Display : TextFormattingMode.Ideal;

    private static void Attempt(Action action)
    {
        try { action(); }
        catch (Exception)
        {
            // Text cosmetics must never stop startup — the WineFonts rule.
        }
    }
}
