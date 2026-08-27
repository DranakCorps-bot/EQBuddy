using Avalonia.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The Travels &amp; Deaths card's body: deaths, zones visited, camp markers. Lifted out
/// of <c>MainWindow</c> for World PR 1 (docs/Themes.md theme 6) — the small,
/// player-driven lists that make this the theme's one Full-inline tab (Travels).
///
/// Takes <see cref="IZoneHost"/> in the constructor for the same reason every other
/// World view does, though this one currently reads nothing from it beyond what its
/// caller already has — future tabs on the same theme (Camps, Routes) share the host,
/// and this keeps the four views' constructors uniform.
/// </summary>
internal sealed class TravelsView
{
    private readonly ItemsControl _deathList = new();
    private readonly ItemsControl _zoneList = new();
    private readonly TextBlock _markersLabel = AppTheme.Heading("Camp markers");
    private readonly ItemsControl _markerList = new();
    private readonly StackPanel _body;

    public TravelsView(IZoneHost host)
    {
        _ = host;   // unused today; kept for constructor uniformity across the theme's four views
        _body = new StackPanel();
        _body.Children.Add(AppTheme.Heading("Deaths", AppTheme.BadBrush));
        _body.Children.Add(_deathList);
        _body.Children.Add(AppTheme.Heading("Zones visited"));
        _body.Children.Add(_zoneList);
        _markersLabel.Margin = new global::Avalonia.Thickness(0, DesignTokens.SpaceS, 0, 0);
        _body.Children.Add(_markersLabel);
        _body.Children.Add(_markerList);
    }

    public Control Body => _body;

    /// <summary>Paints the three lists — called only while the card is expanded, the
    /// same gate <c>MainWindow.RefreshUi</c> always used.</summary>
    public void Render(StatsSnapshot s)
    {
        // ctx is null: deaths/zones/markers use neither questBadges nor item clicks,
        // so this depends on nothing beyond the rows (CardParts.FillList's own contract).
        CardParts.FillList(_deathList, s.Deaths.Select(d => (d.Text, d.Time.ToString("h:mm tt"))));
        CardParts.FillList(_zoneList, s.Zones.Select(z => (z.Text, z.Time.ToString("h:mm tt"))));
        _markersLabel.IsVisible = s.Markers.Count > 0;
        CardParts.FillList(_markerList, s.Markers.Select(m => (m.Text, m.Time.ToString("h:mm tt"))));
    }

    /// <summary>The markers label's visibility is also refreshed independent of the
    /// card's own expand gate (<c>RefreshOptionalSectionVisibility</c> ran this every
    /// tick before the lift, alongside every other optional-section label) — kept as
    /// its own entry point so that second call site still reaches it.</summary>
    public void SetMarkersVisible(bool visible) => _markersLabel.IsVisible = visible;

    /// <summary>Facts for a debug/E2E-style dump, mirroring the WPF view's shape.</summary>
    public string DebugFacts() =>
        $"zones={_zoneList.Items.Count} deaths={_deathList.Items.Count} travelsMarkers={_markerList.Items.Count}";
}
