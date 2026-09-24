using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **Does a floating window open BY ITSELF while EQBuddy is minimised — and the one writer
/// of the answer.**
///
/// Lifted out of <c>SettingsHudView.BuildBreakouts</c> verbatim for DRA-352 D2. That tick
/// list was the ONE writer of <see cref="AppSettings.DisabledBreakouts"/>, and the Founder
/// asked for the whole Floating windows section off Options → Cards &amp; windows — so the
/// write had to MOVE before the list could go (traps 20/26: a fold is where the last writer
/// of a setting goes missing). It moved to the pin on each window's own title bar, and the
/// logic moved here so the pin and the gate in <c>BreakoutHost.AutoWants</c> read one rule
/// and the rule can be unit-tested without a window.
///
/// **Both halves, exactly as the tick list had them.** On means "absent from
/// <c>DisabledBreakouts</c>" AND, for a kind that still has a ★
/// (<see cref="BreakoutPresentation.StarKey"/>), "that star is set" — so turning a kind on
/// also stars it, or the pin would say "opens by itself" over a window that never does.
/// Turning one off leaves the star alone: for every kind but Buffs that key is also a cell on
/// the minimised HUD, and closing a window must not quietly take a HUD cell with it.
/// </summary>
public static class BreakoutAutoOpen
{
    /// <summary>Would this kind open by itself? <paramref name="enumMemberName"/> is the
    /// <c>BreakoutKind</c> member's name, which is also the key
    /// <see cref="AppSettings.DisabledBreakouts"/> stores.</summary>
    public static bool IsOn(AppSettings settings, string enumMemberName) =>
        !settings.DisabledBreakouts.Contains(enumMemberName)
        && (BreakoutPresentation.StarKey(BreakoutPresentation.Kind(enumMemberName)) is not { } star
            || settings.MiniStats.Contains(star));

    /// <summary>The write. Returns true when it changed anything, so a caller saves only
    /// when there is something to save.</summary>
    public static bool Set(AppSettings settings, string enumMemberName, bool on)
    {
        var changed = false;
        if (on)
        {
            changed |= settings.DisabledBreakouts.Remove(enumMemberName);
            // The half that was missing before #45's fix. Watch has no star to set — it
            // opens for a pinned rule, which is the player's pick to make.
            if (BreakoutPresentation.StarKey(BreakoutPresentation.Kind(enumMemberName)) is { } star
                && !settings.MiniStats.Contains(star))
            {
                settings.MiniStats.Add(star);
                changed = true;
            }
        }
        else if (!settings.DisabledBreakouts.Contains(enumMemberName))
        {
            settings.DisabledBreakouts.Add(enumMemberName);
            changed = true;
        }
        return changed;
    }
}
