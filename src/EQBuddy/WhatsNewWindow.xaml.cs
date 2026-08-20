using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;

namespace EQBuddy;

/// <summary>
/// The once-per-update "What's new" popup (NOTES-001). Shown at launch when the
/// running version is newer than the last one whose notes were seen; lists every
/// skipped version, newest first, then never again. Fresh installs never see it —
/// onboarding belongs to the tutorial.
/// </summary>
public partial class WhatsNewWindow : Window
{
    public WhatsNewWindow(MainWindow main, IReadOnlyList<WhatsNewEntry> entries)
    {
        InitializeComponent();
        Owner = main;
        TitleText.Text = entries.Count == 1
            ? $"What's new in EQBuddy {entries[0].Version}"
            : $"What's new since your last version";

        foreach (var entry in entries)
        {
            if (entries.Count > 1)
            {
                var header = new TextBlock
                {
                    Text = $"EQBuddy {entry.Version}", FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 6, 0, 2),
                };
                header.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                NotesPanel.Children.Add(header);
            }
            foreach (var line in entry.Highlights)
            {
                // A MOVE is marked, and a change is not (David, 2026-08-20). The two are
                // read for different reasons: "this got better" is optional, "the thing you
                // use is somewhere else now" is the note whose absence has a player
                // concluding the feature was deleted.
                var note = WhatsNewNotes.Parse(line);

                // Auto,* with the badge in column 0 — never a horizontal StackPanel. A
                // stack measures its children with INFINITE width, so wrapping text beside
                // anything is CLIPPED at the panel edge with no ellipsis to say so
                // (CLAUDE.md trap 14), and these notes are paragraphs.
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.Children.Add(note.Kind == WhatsNewKind.Moved ? MovedBadge(note.Label) : Bullet());
                var text = new TextBlock { Text = note.Text, FontSize = 12, TextWrapping = TextWrapping.Wrap };
                text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                NotesPanel.Children.Add(row);
            }
        }
    }

    private static TextBlock Bullet()
    {
        var bullet = new TextBlock { Text = "•", FontSize = 12, Margin = new Thickness(2, 0, 8, 0) };
        bullet.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        return bullet;
    }

    /// <summary>The MOVED badge. Theme brushes, never literal colours — this popup opens on
    /// every palette including the one light one, and a hardcoded colour is invisible on
    /// exactly one of them. WarnBrush on WarnWashBrush is the app's existing "read this"
    /// pair, so it reads as attention rather than as an error.
    ///
    /// Top-aligned, because the sentence beside it wraps to several lines and a badge
    /// centred against a paragraph floats in the middle of nothing.</summary>
    private static Border MovedBadge(string label)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "WarnBrush");
        var badge = new Border
        {
            Child = text,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(0, 1, 8, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        badge.SetResourceReference(Border.BackgroundProperty, "WarnWashBrush");
        return badge;
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
