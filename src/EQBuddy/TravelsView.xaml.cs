using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The Travels and Deaths card body: deaths, zones visited, camp markers.
/// Drop camp marker (Helm-signed World pre-design question 6) lives here so the
/// inline Full Travels card calls the same handler WorldWindow chrome already uses.
/// Glance tabs never reach this body.
/// </summary>
public partial class TravelsView : UserControl
{
    private readonly IZoneHost _host;

    public TravelsView(IZoneHost host)
    {
        InitializeComponent();
        _host = host;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var button = DesignSystem.IconButton("Location",
            "Drop a marker at your current zone - see it on the Travels tab and on your phone map",
            (_, _) => { _host.DropCampMarker(); Render(_host.CurrentSnapshot()); }, "AccentBrush");
        row.Children.Add(button);
        var label = DesignSystem.Text(Role.Caption, "Drop camp marker");
        label.Margin = new Thickness(Tok.SpaceS, 0, 0, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label);
        Root.Children.Insert(0, row);
    }

    public UIElement Body => this;

    public void Render(StatsSnapshot s)
    {
        FillList(DeathList, s.Deaths.Select(d => (d.Text, d.Time.ToString("h:mm tt"))));
        FillList(ZoneList, s.Zones.Select(z => (z.Text, z.Time.ToString("h:mm tt"))));
        MarkersLabel.Visibility = s.Markers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        FillList(MarkerList, s.Markers.Select(m => (m.Text, m.Time.ToString("h:mm tt"))));
    }

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

    public string DebugFacts() =>
        $"zones={ZoneList.Items.Count} deaths={DeathList.Items.Count} travelsMarkers={MarkerList.Items.Count}";
}