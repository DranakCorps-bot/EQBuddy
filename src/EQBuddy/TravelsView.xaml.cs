using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
// `Role` went with the Drop-camp-marker row on 2026-09-05 (HUD subtraction cut 2) — it was
// that label's only use in this file.
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The Travels and Deaths body: deaths, zones visited, camp markers. Two hosts build their
/// own instance of it (trap 45) — <see cref="WorldWindow"/>'s Travels tab and the Evolved
/// shell's World room — and it was three until 2026-09-05, when HUD subtraction cut 2 took
/// the widget's World card.
///
/// **THIS VIEW'S OWN "Drop camp marker" ROW WENT WITH THAT CARD, and it was a DUPLICATE the
/// whole time.** The row was inserted at the top of this body by the constructor, and the
/// comment here said why in as many words: *"lives here so the inline Full Travels card
/// calls the same handler WorldWindow chrome already uses"*. The card was the reason; both
/// surviving hosts pin their OWN copy as chrome on every tab (<c>WorldWindow.BuildActionRow</c>,
/// <c>WorldRoom</c>'s pinned row), so on the Travels tab the affordance was drawn twice —
/// once inside the scroller and once below it.
///
/// **Nothing routine could see it, and the cut's own screenshot could.** It is not a
/// regression: it has rendered twice since the World fold, in a window no committed
/// illustration had ever photographed. `world-travels` is that picture, it landed with this
/// cut, and it showed the duplicate on its first run — which is trap 22 paying out in the
/// direction it is usually described from, a surface with no fixture state hiding a defect
/// rather than hiding a feature.
/// </summary>
public partial class TravelsView : UserControl
{
    /// <param name="host">Kept, and now unread here: the Drop-marker row was the only thing
    /// in this view that ever called back into the host, and it went with the card. The
    /// parameter is what makes <c>MainWindow.NewTravelsView()</c> a FACTORY rather than a
    /// shared accessor — one instance per host, which is the whole point of trap 45's guard
    /// and the shape <c>SurfaceOwnershipTests</c> asserts. A view that renders from a
    /// snapshot and reaches for nothing is the destination, not a loose end.</param>
    public TravelsView(IZoneHost host)
    {
        InitializeComponent();
        _ = host;
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