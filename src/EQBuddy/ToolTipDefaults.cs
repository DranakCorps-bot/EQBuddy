using System.Windows;
using System.Windows.Controls;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Applies <see cref="ToolTipPolicy"/> to WPF. The reasoning lives with the policy; this
/// file is only the wiring, the same split as <see cref="WineText"/> and <see cref="WineFonts"/>.
///
/// **One override, not a list of controls.** The alternative was setting
/// <c>ToolTipService.ShowDuration</c> on the tooltips that matter, and there is no such
/// set: the trigger is any tooltip opening while a tick is running late, and there are
/// dozens across the app with no reason to prefer one. A hand-maintained list stops
/// covering the set the day someone adds a tooltip (trap 30) and the failure mode is a
/// player's app going quiet, not a red test.
///
/// **The override is on <see cref="DependencyObject"/> on purpose.**
/// <c>ToolTipService.ShowDuration</c> is an ATTACHED property that WPF reads straight off
/// the owning element (<c>PopupControlService</c> calls <c>GetShowDuration(owner)</c>), and
/// metadata lookup walks the element's type hierarchy up to <see cref="DependencyObject"/>
/// — so one override at the root answers for every element WPF can hang a tooltip on,
/// including the ones built in code long after startup.
///
/// **And "present in the build" is not "in effect at runtime" — trap 42 cost two releases
/// learning that on an <c>OverrideMetadata</c> call of exactly this shape.** That one
/// relied on property-value INHERITANCE, which propagates a value that has been SET and
/// never a metadata default; this one does not, because nothing inherits here. That is a
/// reason to expect it to work, not evidence that it does, so <see cref="InForce"/> reports
/// what a control actually resolves and <c>ToolTipTimerTests</c> asserts it from a launched
/// app.
/// </summary>
internal static class ToolTipDefaults
{
    private static bool _applied;

    /// <summary>Call before any window is constructed, so the first tooltip of the session
    /// is already bounded rather than the one that gets through.</summary>
    public static void ApplyOnce()
    {
        if (_applied) return;
        _applied = true;
        try
        {
            ToolTipService.ShowDurationProperty.OverrideMetadata(
                typeof(DependencyObject),
                new FrameworkPropertyMetadata(ToolTipPolicy.ShowDurationMs));
        }
        catch (Exception)
        {
            // A tooltip duration must never stop startup — the WineFonts rule. The dump
            // fact below is what makes this catch visible instead of silent: a swallowed
            // failure here reports itself as the WPF default in the next dump rather than
            // as nothing at all.
        }
    }

    /// <summary>What a control ACTUALLY resolves for the property, read the same way WPF's
    /// own <c>PopupControlService</c> reads it. Freshly built rather than cached, because
    /// the question this answers is "what would a tooltip owner get right now" and a probe
    /// held from startup could only ever answer for the moment it was made.</summary>
    public static int InForce() => ToolTipService.GetShowDuration(new Button());
}
