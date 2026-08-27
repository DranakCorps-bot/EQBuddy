using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

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
public partial class TravelsView : UserControl
{
    public TravelsView(IZoneHost host)
    {
        InitializeComponent();
        _ = host;   // unused today; kept for constructor uniformity across the theme's four views
    }

    public UIElement Body => this;

    /// <summary>Paints the three lists — called only while the card is expanded, the
    /// same gate <c>MainWindow.RefreshUi</c> always used.</summary>
    public void Render(StatsSnapshot s)
    {
        FillList(DeathList, s.Deaths.Select(d => (d.Text, d.Time.ToString("h:mm tt"))));
        FillList(ZoneList, s.Zones.Select(z => (z.Text, z.Time.ToString("h:mm tt"))));
        MarkersLabel.Visibility = s.Markers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        FillList(MarkerList, s.Markers.Select(m => (m.Text, m.Time.ToString("h:mm tt"))));
    }

    /// <summary>The reduced shape of <c>MainWindow.FillList</c> this card ever needed —
    /// plain name/value rows, no tooltip, no click, no note. Deaths/zones/markers never
    /// passed those optional parameters, so this carries only what they used.</summary>
    private static void FillList(ItemsControl list, IEnumerable<(string Name, string Value)> rows)
    {
        list.Items.Clear();
        foreach (var (name, value) in rows)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new TextBlock
            {
                Text = name,
                FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)list.FindResource("TextBrush"),
                Margin = new Thickness(0, 1, Tok.SpaceM, 1),
            };
            var right = new TextBlock
            {
                Text = value,
                FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
                Foreground = (Brush)list.FindResource("DimBrush"),
            };
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);
            list.Items.Add(grid);
        }
    }

    /// <summary>Facts for the <c>EQBUDDY_EXPAND</c> dump — the WPF layer's only test seam.
    /// Same key names the card carried before the lift (<c>zones</c>/<c>deaths</c>), plus
    /// <c>travelsMarkers</c>, which was never pinned before this PR.</summary>
    public string DebugFacts() =>
        $"zones={ZoneList.Items.Count} deaths={DeathList.Items.Count} travelsMarkers={MarkerList.Items.Count}";
}
