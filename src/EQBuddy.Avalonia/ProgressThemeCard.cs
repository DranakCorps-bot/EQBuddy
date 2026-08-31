using Avalonia.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// Composes the PROGRESS theme's inline card for this lane — the Avalonia half of Inline
/// themes PR 1 (the plan's PR B). <c>EQBuddy/ProgressThemeCard.cs</c> is the WPF twin and
/// carries the fuller commentary; every decision here mirrors it name for name:
///
/// <list type="bullet">
/// <item><b>Wealth inline is COIN ONLY</b> — Bevel's table with Helm's correction. The
/// four <see cref="MoneyPresentation.SummaryLines"/> and nothing else; the sold ledger
/// and the motes rows stay in the Progress window the header's ⧉ opens.</item>
/// <item><b>Raids is the Glance room</b> — <see cref="ProgressTheme.RaidsGlance"/>'s one
/// line, from UI.Shared so both lanes say the same words, and its full view is never
/// built inline.</item>
/// <item><b>Bodies are built on the first expand, through the same factory the window
/// uses</b> (<c>NewProgressSurfaces</c>) — each host its OWN instances, which on this
/// toolkit is the difference between a layout rule and a crash (see IWidgetCard).</item>
/// </list>
/// </summary>
internal static class ProgressThemeCard
{
    private sealed class CoinBody
    {
        public readonly StackPanel Panel = new();
        private readonly TextBlock _lines = CardParts.EmptyLine("");

        public CoinBody()
        {
            _lines.Foreground = AppTheme.TextBrush;
            Panel.Children.Add(_lines);
        }

        public void Render(StatsSnapshot s) =>
            _lines.Text = string.Join(Environment.NewLine, MoneyPresentation.SummaryLines(s));
    }

    public static ThemeCardPanel<ProgressTab> Build(
        Control header,
        ThemeHost<ProgressTab> host,
        Func<ProgressSurfaceSet> newSurfaces,
        Func<StatsSnapshot, int> dingUnlocks,
        Func<int> raidsDefeated,
        Action popOut,
        Action bringWindowForward,
        Func<double, double> bodyCap)
    {
        ProgressSurfaceSet? surfaces = null;
        ProgressSurfaceSet Surfaces() => surfaces ??= newSurfaces();
        CoinBody? coin = null;
        CoinBody Coin() => coin ??= new CoinBody();

        return new ThemeCardPanel<ProgressTab>(
            header, host,
            tabs: s => ProgressTheme
                .Tabs(s, dingUnlocks(s), raidsDefeated(), RaidTargetCatalog.Default.BossCount)
                .Select(t => new ThemeCardTab<ProgressTab>(t.Tab, t.Label, t.Value))
                .ToList(),
            modeFor: ProgressSurface.InlineModeFor,
            bodyFor: tab => tab switch
            {
                ProgressTab.Wealth => Coin().Panel,
                ProgressTab.Faction => Surfaces().Faction.Body,
                _ => Surfaces().Experience.Body,
            },
            glanceFor: (_, _) =>
                ProgressTheme.RaidsGlance(raidsDefeated(), RaidTargetCatalog.Default.BossCount),
            render: (tab, s) =>
            {
                switch (tab)
                {
                    case ProgressTab.Wealth: Coin().Render(s); break;
                    case ProgressTab.Faction: Surfaces().Faction.Render(s); break;
                    case ProgressTab.Raids: break;   // Glance never reaches here
                    default: Surfaces().Experience.Render(s); break;
                }
            },
            popOut: popOut,
            bringWindowForward: bringWindowForward,
            popOutTip: "Open the Progress window — the full rooms, on your second monitor",
            bodyCap: bodyCap);
    }
}
