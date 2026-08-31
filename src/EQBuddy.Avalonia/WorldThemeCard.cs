using Avalonia.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// Composes the WORLD theme's inline card for this lane — the Avalonia half of World
/// PR 3. <c>EQBuddy/WorldThemeCard.cs</c> is the WPF twin and carries the fuller
/// commentary; every decision here mirrors it name for name.
///
/// Only Travels is Full; Map, Camps and Path are Glance (Bevel-signed pre-design), so this
/// card never builds a <c>MapView</c>/<c>SpawnsView</c>/<c>TravelView</c> — the only body
/// it ever draws is <see cref="TravelsView"/>, the same instance <c>MainWindow</c> already
/// built for the widget.
/// </summary>
internal static class WorldThemeCard
{
    public static ThemeCardPanel<WorldTab> Build(
        Control header,
        ThemeHost<WorldTab> host,
        Func<TravelsView> travels,
        Func<string?> currentZone,
        Func<int> runningTimers,
        Action popOut,
        Action bringWindowForward,
        Func<double, double> bodyCap)
    {
        return new ThemeCardPanel<WorldTab>(
            header, host,
            tabs: s => WorldTheme.Tabs(currentZone(), s.Deaths.Count)
                .Select(t => new ThemeCardTab<WorldTab>(t.Tab, t.Label, t.Value))
                .ToList(),
            modeFor: WorldSurface.InlineModeFor,
            // Only ever called for Travels — the three Glance tabs never reach a body.
            bodyFor: _ => travels().Body,
            glanceFor: (tab, _) =>
                WorldTheme.GlanceFor(tab, currentZone(), runningTimers(), from: null, destination: null),
            render: (tab, s) =>
            {
                if (tab == WorldTab.Travels) travels().Render(s);
            },
            popOut: popOut,
            bringWindowForward: bringWindowForward,
            popOutTip: "Open the World window — the map, camp timers and travel routes, on your second monitor",
            bodyCap: bodyCap);
    }
}
