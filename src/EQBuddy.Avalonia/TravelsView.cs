using Avalonia.Controls;
using Avalonia.Layout;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The Travels and Deaths card body: deaths, zones visited, camp markers.
/// Drop camp marker (Helm-signed World pre-design question 6) lives here so the
/// inline Full Travels card calls the same handler WorldWindow chrome already uses.
/// Glance tabs never reach this body.
/// </summary>
internal sealed class TravelsView
{
    private readonly IZoneHost _host;
    private readonly ItemsControl _deathList = new();
    private readonly ItemsControl _zoneList = new();
    private readonly TextBlock _markersLabel = AppTheme.Heading("Camp markers");
    private readonly ItemsControl _markerList = new();
    private readonly StackPanel _body;

    public TravelsView(IZoneHost host)
    {
        _host = host;
        _body = new StackPanel();
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var marker = DesignSystem.IconButton("Location",
            "Drop a marker at your current zone - see it on the Travels tab and on your phone map",
            () => { _host.DropCampMarker(); Render(_host.CurrentSnapshot()); }, "AccentBrush");
        row.Children.Add(marker);
        var markerLabel = DesignSystem.Text(DesignTokens.TypeRole.Caption, "Drop camp marker");
        markerLabel.Foreground = AppTheme.DimBrush;
        markerLabel.VerticalAlignment = VerticalAlignment.Center;
        markerLabel.Margin = new global::Avalonia.Thickness(DesignTokens.SpaceS, 0, 0, 0);
        row.Children.Add(markerLabel);
        _body.Children.Add(row);
        _body.Children.Add(AppTheme.Heading("Deaths", AppTheme.BadBrush));
        _body.Children.Add(_deathList);
        _body.Children.Add(AppTheme.Heading("Zones visited"));
        _body.Children.Add(_zoneList);
        _markersLabel.Margin = new global::Avalonia.Thickness(0, DesignTokens.SpaceS, 0, 0);
        _body.Children.Add(_markersLabel);
        _body.Children.Add(_markerList);
    }

    public Control Body => _body;

    public void Render(StatsSnapshot s)
    {
        CardParts.FillList(_deathList, s.Deaths.Select(d => (d.Text, d.Time.ToString("h:mm tt"))));
        CardParts.FillList(_zoneList, s.Zones.Select(z => (z.Text, z.Time.ToString("h:mm tt"))));
        _markersLabel.IsVisible = s.Markers.Count > 0;
        CardParts.FillList(_markerList, s.Markers.Select(m => (m.Text, m.Time.ToString("h:mm tt"))));
    }

    public void SetMarkersVisible(bool visible) => _markersLabel.IsVisible = visible;

    public string DebugFacts() =>
        $"zones={_zoneList.Items.Count} deaths={_deathList.Items.Count} travelsMarkers={_markerList.Items.Count}";
}