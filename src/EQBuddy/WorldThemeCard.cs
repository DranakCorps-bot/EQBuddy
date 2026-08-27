using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Composes the WORLD theme's inline card — the fifth <see cref="ThemeCardView{TTab}"/>
/// instance, and the machinery needs nothing new (FABLE.md's own note on PR 3).
///
/// Only **Travels is Full**; Map, Camps and Path are Glance (Bevel-signed pre-design) —
/// a live map canvas, a timer list with its own bell pickers, and a destination picker all
/// carry chrome that must not shrink-wrap onto a SizeToContent always-on-top panel. So this
/// card never builds a <c>MapView</c>/<c>SpawnsView</c>/<c>TravelView</c> at all; the only
/// body it ever draws is <see cref="TravelsView"/>, built through the same World PR 1
/// factory the window uses (each host its own instance — trap 45).
/// </summary>
internal static class WorldThemeCard
{
    public static ThemeCardView<WorldTab> Build(
        Expander section,
        ContentControl bodyHost,
        ContentControl popOutHost,
        ThemeHost<WorldTab> host,
        Func<TravelsView> newTravels,
        Func<string?> currentZone,
        Func<int> runningTimers,
        Action popOut,
        Action bringWindowForward,
        double bodyMaxHeight)
    {
        // Built on the first expand and kept — a player who never opens the theme pays
        // nothing for it, same as every other theme's body.
        TravelsView? travels = null;
        TravelsView Travels() => travels ??= newTravels();

        var card = new ThemeCardView<WorldTab>(
            section, bodyHost, host,
            tabs: s => WorldTheme.Tabs(currentZone(), s.Deaths.Count)
                .Select(t => new ThemeCardTab<WorldTab>(t.Tab, t.Label, t.Value))
                .ToList(),
            modeFor: WorldSurface.InlineModeFor,
            // Only ever called for Travels — the three Glance tabs never reach a body.
            bodyFor: _ => Travels().Body,
            glanceFor: (tab, _) =>
                WorldTheme.GlanceFor(tab, currentZone(), runningTimers(), from: null, destination: null),
            render: (tab, s) =>
            {
                if (tab == WorldTab.Travels) Travels().Render(s);
            },
            popOut: popOut,
            bringWindowForward: bringWindowForward,
            popOutTip: "Open the World window — the map, camp timers and travel routes, on your second monitor",
            bodyMaxHeight: bodyMaxHeight);

        popOutHost.Content = card.PopOutButton;
        return card;
    }
}
