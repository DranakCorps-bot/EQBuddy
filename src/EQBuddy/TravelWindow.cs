using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;

namespace EQBuddy;

/// <summary>
/// "How do I get there from here?" — the ZoneGraph has answered this for quest
/// sorting since 1.39; this window finally says it out loud (competitive gap #2,
/// 2026-08-10: the data was shipped, only the presentation was missing). Pick a
/// destination, get the hop list from wherever the log last saw you. Zone lines
/// come from the eqltools atlas (client-mined walking connections) plus the wiki's
/// boat and port adjacencies, so a hop may be a zone line, a boat, or a port —
/// the wiki page for a zone names which.
/// </summary>
public sealed class TravelWindow : Window
{
    private readonly MainWindow _main;
    private readonly ComboBox _dest = new() { FontSize = 12, IsEditable = true, MinWidth = 240 };
    private readonly TextBlock _fromLabel = new() { FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };
    private readonly StackPanel _route = new();

    public TravelWindow(MainWindow main)
    {
        _main = main;
        Title = "Travel route";
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.CanResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "BgBrush");
        WindowZoom.Attach(this, "travel", main.Settings);
        // Resizable and REMEMBERED (David, 2026-08-25). The route is computed before the
        // window opens, so the height sampled at first render is a true one.
        WindowZoom.AllowResize(this, "travel", main.Settings);

        _fromLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        foreach (var zone in _main.ZoneGraph.Zones) _dest.Items.Add(zone);
        _dest.SelectionChanged += (_, _) => RenderRoute();
        var go = Theming.Button("Route", isDefault: true);
        go.Margin = new Thickness(6, 0, 0, 0);
        go.Click += (_, _) => RenderRoute();

        var pickRow = new StackPanel { Orientation = Orientation.Horizontal };
        pickRow.Children.Add(_dest);
        pickRow.Children.Add(go);

        var root = new StackPanel { Margin = new Thickness(12), MinWidth = 300 };
        root.Children.Add(_fromLabel);
        root.Children.Add(pickRow);
        root.Children.Add(_route);
        Content = root;
        RenderRoute();
    }

    /// <summary>Re-run against the current zone — called on open and when you zone.</summary>
    public void RenderRoute()
    {
        var from = _main.CurrentZoneName;
        _fromLabel.Text = from.Length > 0 ? $"From: {from}" : "From: (no zone seen in the log yet)";
        _route.Children.Clear();
        var dest = (_dest.SelectedItem as string) ?? _dest.Text.Trim();
        if (from.Length == 0 || dest.Length == 0) return;

        var note = new TextBlock { FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 340 };
        if (_main.ZoneGraph.Distance(from, dest) is not { } route)
        {
            note.Text = $"No known route from {from} to \"{dest}\" — if a connection is missing, " +
                        "its wiki zone page probably lacks the adjacency.";
            note.SetResourceReference(TextBlock.ForegroundProperty, "WarnBrush");
            _route.Children.Add(note);
            return;
        }

        note.Text = route.Hops == 0 ? "You're already there." : $"{route.Hops} zone{(route.Hops == 1 ? "" : "s")} away:";
        note.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        _route.Children.Add(note);
        for (var i = 0; i < route.Path.Count; i++)
        {
            var step = new TextBlock
            {
                Text = i == 0 ? $"  📍 {route.Path[i]}" : $"  {i}. {route.Path[i]}",
                FontSize = 12, Margin = new Thickness(4, 2, 0, 0),
            };
            step.SetResourceReference(TextBlock.ForegroundProperty,
                i == route.Path.Count - 1 ? "GoodBrush" : "TextBrush");
            _route.Children.Add(step);
        }
    }
}
