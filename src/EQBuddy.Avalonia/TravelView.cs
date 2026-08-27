using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.Core;

namespace EQBuddy.Avalonia;

/// <summary>
/// "How do I get there from here?" — the ZoneGraph has answered this for quest
/// sorting since 1.39; this view finally says it out loud (competitive gap #2,
/// 2026-08-10: the data was shipped, only the presentation was missing). Pick a
/// destination, get the hop list from wherever the log last saw you. Zone lines
/// come from the eqltools atlas (client-mined walking connections) plus the wiki's
/// boat and port adjacencies, so a hop may be a zone line, a boat, or a port —
/// the wiki page for a zone names which.
///
/// Lifted out of <c>TravelWindow</c> for World PR 1 (docs/Themes.md theme 6): the
/// content, not the chrome. Reads <see cref="TravelPlan"/> for the route and both
/// wordings, replacing the hand-rolled logic the window used to carry — the Routes
/// tab (PR 2) and the phone's travel surface (PR 4) now read the SAME module.
/// </summary>
internal sealed class TravelView
{
    private readonly IZoneHost _host;
    // WPF uses an editable ComboBox; Avalonia's isn't editable, and AutoCompleteBox
    // is the same gesture (type or pick) with the filtering built in.
    private readonly AutoCompleteBox _dest = new()
    {
        FontSize = 12, MinWidth = 240,
        FilterMode = AutoCompleteFilterMode.Contains,
        MinimumPrefixLength = 0,
        PlaceholderText = "Destination zone…",
    };
    private readonly TextBlock _fromLabel = new()
    {
        FontSize = 12, Margin = new Thickness(0, 0, 0, 6), Foreground = AppTheme.TextBrush,
    };
    private readonly StackPanel _route = new();
    private readonly StackPanel _body;

    public TravelView(IZoneHost host)
    {
        _host = host;

        _dest.ItemsSource = _host.ZoneGraph.Zones;
        _dest.SelectionChanged += (_, _) => Render();
        _dest.KeyDown += (_, e) => { if (e.Key == Key.Enter) Render(); };
        var go = ZoneTheming.Button("Route", isDefault: true);
        go.Margin = new Thickness(6, 0, 0, 0);
        go.Click += (_, _) => Render();

        var pickRow = new StackPanel { Orientation = Orientation.Horizontal };
        pickRow.Children.Add(_dest);
        pickRow.Children.Add(go);

        _body = new StackPanel { Margin = new Thickness(12), MinWidth = 300 };
        _body.Children.Add(_fromLabel);
        _body.Children.Add(pickRow);
        _body.Children.Add(_route);
        Render();
    }

    public Control Body => _body;

    /// <summary>Re-run against the current zone — called on open and when you zone.</summary>
    public void Render()
    {
        var from = _host.CurrentZoneName;
        _fromLabel.Text = from.Length > 0 ? $"From: {from}" : "From: (no zone seen in the log yet)";
        _route.Children.Clear();
        var dest = (_dest.SelectedItem as string) ?? (_dest.Text ?? "").Trim();
        if (from.Length == 0 || dest.Length == 0) return;

        var result = TravelPlan.Plan(_host.ZoneGraph, from, dest);
        var note = new TextBlock
        {
            FontSize = 12, Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap, MaxWidth = 340,
            Text = result.Note,
            Foreground = result.Outcome == TravelOutcome.NoRoute ? AppTheme.WarnBrush : AppTheme.AccentBrush,
        };
        _route.Children.Add(note);
        for (var i = 0; i < result.Path.Count; i++)
        {
            var step = new TextBlock
            {
                Text = i == 0 ? $"  📍 {result.Path[i]}" : $"  {i}. {result.Path[i]}",
                FontSize = 12, Margin = new Thickness(4, 2, 0, 0),
                Foreground = i == result.Path.Count - 1 ? AppTheme.GoodBrush : AppTheme.TextBrush,
            };
            _route.Children.Add(step);
        }
    }

    /// <summary>Facts for a debug/E2E-style dump, mirroring the WPF view's shape.</summary>
    public string DebugFacts() =>
        $"travelZones={_host.ZoneGraph.Zones.Count} travelRouteShown={(_route.Children.Count > 0 ? 1 : 0)}";
}
