using Avalonia;
using Avalonia.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The Kills surface as its own view — the Avalonia twin of
/// <c>EQBuddy/KillsCardView.cs</c>, born in Inline themes PR 2 when the inline card and
/// the Kills &amp; Drops window each needed their OWN instance (one control, one visual
/// parent on this toolkit — the crash class PR A retired for Progress).
///
/// **It also closes a quiet parity drift**: the widget's old kills panel hand-rolled its
/// summary and rows while the WPF twin read <see cref="KillsPresentation"/>, so the two
/// lanes could disagree about wording nobody was comparing. Both read the shared module
/// now — #210's rule, applied at lift time rather than after the report.
/// </summary>
internal sealed class KillsCardView : IWidgetCard
{
    private readonly TextBlock _summary = AppTheme.DimText("");
    private readonly ItemsControl _kills = new();
    private readonly TextBlock _farmingLabel;
    private readonly ItemsControl _farming = new();
    private readonly TextBlock _partyLabel;
    private readonly ItemsControl _party = new();
    private StackPanel? _body;

    public string Key => "kills";

    public Control Body => _body ??= Build();

    /// <summary>Rendered row counts, for the render tests — this lane's only cover.</summary>
    public int KillRowCount => _kills.Items.Count;
    public int PartyRowCount => _party.Items.Count;

    public KillsCardView()
    {
        _farmingLabel = AppTheme.Heading(KillsPresentation.FarmingLabel);
        _partyLabel = AppTheme.Heading(KillsPresentation.PartyKillsLabel);
    }

    private StackPanel Build()
    {
        var panel = new StackPanel();
        _summary.Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXs);
        panel.Children.Add(_summary);
        panel.Children.Add(_kills);
        _farmingLabel.Margin = new Thickness(0, DesignTokens.SpaceS, 0, 0);
        panel.Children.Add(_farmingLabel);
        panel.Children.Add(_farming);
        _partyLabel.Margin = new Thickness(0, DesignTokens.SpaceS, 0, 0);
        panel.Children.Add(_partyLabel);
        panel.Children.Add(_party);
        return panel;
    }

    public void Render(StatsSnapshot s)
    {
        _summary.Text = KillsPresentation.Summary(s);
        CardParts.FillList(_kills, KillsPresentation.YourKills(s).Select(r => (r.Name, r.Value)));

        _farmingLabel.IsVisible = KillsPresentation.ShowFarming(s);
        CardParts.FillList(_farming, KillsPresentation.Farming(s).Select(r => (r.Name, r.Value)));

        _partyLabel.IsVisible = KillsPresentation.ShowPartyKills(s);
        CardParts.FillList(_party, KillsPresentation.PartyKills(s).Select(r => (r.Name, r.Value)));
    }
}
