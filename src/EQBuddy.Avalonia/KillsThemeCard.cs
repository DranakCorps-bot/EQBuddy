using Avalonia.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// Composes the KILLS &amp; DROPS theme's inline card for this lane — the Avalonia half
/// of Inline themes PR 2; <c>EQBuddy/KillsThemeCard.cs</c> is the WPF twin. Kills is the
/// Full room; Drops is the GLANCE (it reads the wiki, which an expanded card over a
/// running game must not do — Bevel's move, recorded on
/// <see cref="CreatureSurface.InlineModeFor"/>).
/// </summary>
internal static class KillsThemeCard
{
    public static ThemeCardPanel<CreatureTab> Build(
        Control header,
        ThemeHost<CreatureTab> host,
        Func<CreatureSurfaceSet> newSurfaces,
        Action popOut,
        Action bringWindowForward,
        Func<double, double> bodyCap)
    {
        CreatureSurfaceSet? surfaces = null;
        CreatureSurfaceSet Surfaces() => surfaces ??= newSurfaces();

        return new ThemeCardPanel<CreatureTab>(
            header, host,
            tabs: s => CreatureTheme.Tabs(s)
                .Select(t => new ThemeCardTab<CreatureTab>(t.Tab, t.Label, t.Value))
                .ToList(),
            modeFor: CreatureSurface.InlineModeFor,
            bodyFor: _ => Surfaces().Kills.Body,
            glanceFor: (_, s) => CreatureTheme.DropsGlance(s),
            render: (tab, s) =>
            {
                if (tab == CreatureTab.Kills) Surfaces().Kills.Render(s);
                // Drops never reaches here — a Glance tab has no body to paint.
            },
            popOut: popOut,
            bringWindowForward: bringWindowForward,
            popOutTip: "Open the Kills & Drops window — the full lists, on your second monitor",
            bodyCap: bodyCap);
    }
}
