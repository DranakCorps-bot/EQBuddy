using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Composes the PROGRESS theme's inline card — which rooms it draws, what each one shows
/// when it is drawn under the widget rather than in its window, and what the Glance room
/// says instead of drawing.
///
/// It lives here rather than in <c>MainWindow</c> for the reason CLAUDE.md gives for every
/// lift: the widget is the hotspot the ratchet guards, and a surface migrated INSIDE it is
/// guarded by nothing. <see cref="ThemeCardView{TTab}"/> is the mechanism for all four
/// themes; this is the Progress theme's opinions, and PR 2 and PR 3 get their own files
/// beside it rather than growing either.
/// </summary>
internal static class ProgressThemeCard
{
    /// <summary>
    /// **Wealth inline is COIN ONLY** — Bevel's table with Helm's correction, 2026-08-22.
    /// The four <see cref="MoneyPresentation.SummaryLines"/> and nothing else: no sold
    /// ledger, and no mote rate. #227 settled that Wealth is coin and the Motes card owns
    /// the rate, so putting motes here would be re-answering a question that is already
    /// answered somewhere a player can see.
    ///
    /// Nothing is LOST by that (trap 20/26 is what this note exists to refuse): the sold
    /// ledger and the motes rows are both in the Progress window, which the ⧉ on this
    /// card's header opens in one click. A fold that drops a surface with no way back is
    /// the defect; a fold that shortens a body and keeps the door is the feature.
    /// </summary>
    private sealed class CoinBody
    {
        public readonly StackPanel Panel = new();
        private readonly TextBlock _lines = CardParts.Summary();

        public CoinBody() => Panel.Children.Add(_lines);

        public void Render(StatsSnapshot s) =>
            _lines.Text = string.Join(Environment.NewLine, MoneyPresentation.SummaryLines(s));
    }

    public static ThemeCardView<ProgressTab> Build(
        Expander section,
        ContentControl bodyHost,
        ContentControl popOutHost,
        ThemeHost<ProgressTab> host,
        Func<ProgressSurfaceSet> newSurfaces,
        Func<StatsSnapshot, int> dingUnlocks,
        Func<int> raidsDefeated,
        Action popOut,
        Action bringWindowForward,
        Func<double, double> bodyCap)
    {
        // Built on the FIRST expand and kept — a player who never opens the theme pays
        // nothing for it. The same factory the window uses, deliberately: the card and the
        // window constructing their surfaces differently is exactly how two hosts of one
        // theme start disagreeing about what a surface is.
        ProgressSurfaceSet? surfaces = null;
        ProgressSurfaceSet Surfaces() => surfaces ??= newSurfaces();
        CoinBody? coin = null;
        CoinBody Coin() => coin ??= new CoinBody();

        var card = new ThemeCardView<ProgressTab>(
            section, bodyHost, host,
            // **The inline card draws no History tab.** A 320-unit card cannot hold a list
            // beside a detail pane, and nothing is lost by the refusal: the career browse is
            // the Evolved Progress room's and the studio window is one context-menu click
            // away. `ProgressSurface.DesktopShellOnly` is the one place that decides, so
            // this host and the phone and the v1 window cannot answer it three ways
            // (trap 55).
            tabs: s => ProgressTheme
                .Tabs(s, dingUnlocks(s), raidsDefeated(), RaidTargetCatalog.Default.BossCount)
                .Where(t => !ProgressSurface.DesktopShellOnly(t.Tab))
                .Select(t => new ThemeCardTab<ProgressTab>(t.Tab, t.Label, t.Value))
                .ToList(),
            modeFor: ProgressSurface.InlineModeFor,
            bodyFor: tab => tab switch
            {
                ProgressTab.Wealth => Coin().Panel,
                ProgressTab.Faction => Surfaces().Faction.Body,
                _ => Surfaces().Experience.Body,
            },
            // The whole body of the room: Raids is a cleared/total ledger over six zones,
            // which READS as a line and DRAWS as 29 rows. The words are in UI.Shared so the
            // Avalonia card says them too when it lands.
            glanceFor: (_, _) =>
                ProgressTheme.RaidsGlance(raidsDefeated(), RaidTargetCatalog.Default.BossCount),
            render: (tab, s) =>
            {
                switch (tab)
                {
                    case ProgressTab.Wealth: Coin().Render(s); break;
                    case ProgressTab.Faction: Surfaces().Faction.Render(s); break;
                    // Raids never reaches here — a Glance tab has no body to paint, which
                    // is the guarantee InlineMode exists to make.
                    case ProgressTab.Raids: break;
                    default: Surfaces().Experience.Render(s); break;
                }
            },
            popOut: popOut,
            bringWindowForward: bringWindowForward,
            popOutTip: "Open the Progress window — the full rooms, on your second monitor",
            bodyCap: bodyCap);

        popOutHost.Content = card.PopOutButton;
        return card;
    }
}
