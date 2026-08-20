using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using EQBuddy.Core;

namespace EQBuddy.Avalonia;

/// <summary>The once-per-update release notes popup. Content is supplied by Core's
/// embedded catalog, so this window is presentation only and never needs the network.</summary>
public sealed class WhatsNewWindow : Window
{
    public WhatsNewWindow(IReadOnlyList<WhatsNewEntry> entries)
    {
        Title = "What's new in EQBuddy";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Content = BuildContent(entries);
        PointerPressed += OnDrag;
    }

    private Control BuildContent(IReadOnlyList<WhatsNewEntry> entries)
    {
        var title = new TextBlock
        {
            Text = entries.Count == 1
                ? $"What's new in EQBuddy {entries[0].Version}"
                : "What's new since your last version",
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            Foreground = AppTheme.AccentBrush,
        };

        var notes = new StackPanel();
        foreach (var entry in entries)
        {
            if (entries.Count > 1)
                notes.Children.Add(new TextBlock
                {
                    Text = $"EQBuddy {entry.Version}",
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = AppTheme.AccentBrush,
                    Margin = new Thickness(0, 6, 0, 2),
                });

            foreach (var line in entry.Highlights)
            {
                // A MOVE is marked, and a change is not (David, 2026-08-20) — same call as
                // the WPF twin, from the same Core parse, in the same change. See
                // WhatsNewNotes for why a relocation is not just another bullet.
                var note = WhatsNewNotes.Parse(line);

                // Auto,* with the badge in column 0 — never a horizontal StackPanel, which
                // measures with INFINITE width and clips wrapping text silently (trap 14).
                var row = new Grid { Margin = new Thickness(0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                row.Children.Add(note.Kind == WhatsNewKind.Moved
                    ? MovedBadge(note.Label)
                    : new TextBlock
                    {
                        Text = "•", FontSize = 12, Foreground = AppTheme.AccentBrush,
                        Margin = new Thickness(2, 0, 8, 0),
                    });
                var body = new TextBlock
                {
                    Text = note.Text, FontSize = 12, Foreground = AppTheme.TextBrush,
                    TextWrapping = TextWrapping.Wrap,
                };
                Grid.SetColumn(body, 1);
                row.Children.Add(body);
                notes.Children.Add(row);
            }
        }

        Control MovedBadge(string label) => new Border
        {
            // Theme brushes, never literal colours — this popup opens on every palette
            // including the one light one. WarnBrush on WarnWashBrush is the app's existing
            // "read this" pair, so it reads as attention rather than as an error.
            //
            // Top-aligned, because the sentence beside it wraps to several lines and a
            // badge centred against a paragraph floats in the middle of nothing.
            Child = new TextBlock
            {
                Text = label,
                FontSize = 9,
                FontWeight = FontWeight.SemiBold,
                Foreground = AppTheme.WarnBrush,
            },
            Background = AppTheme.WarnWashBrush,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1),
            Margin = new Thickness(0, 1, 8, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };

        var close = AppTheme.IconButton("Nice — got it", "Close release notes");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Padding = new Thickness(14, 4);
        close.FontSize = 12;
        close.Background = AppTheme.PanelBrush;
        close.Foreground = AppTheme.TextBrush;
        close.BorderBrush = AppTheme.BorderBrush;
        close.BorderThickness = new Thickness(1);
        close.Click += (_, _) => Close();

        var content = new Grid { Margin = new Thickness(18, 16, 18, 14) };
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        content.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        content.Children.Add(title);
        var scroll = new ScrollViewer
        {
            Content = notes,
            Margin = new Thickness(0, 8, 0, 10),
            MaxHeight = 420,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(scroll, 1);
        content.Children.Add(scroll);
        Grid.SetRow(close, 2);
        content.Children.Add(close);

        return new Border
        {
            Background = AppTheme.BgBrush,
            BorderBrush = AppTheme.HairlineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = content,
        };
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(v => v is Button))
            return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
