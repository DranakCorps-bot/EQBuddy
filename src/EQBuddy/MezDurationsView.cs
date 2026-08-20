using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The mez-duration editor on Options → Alerts &amp; chips: one row per catalog spell,
/// each with the duration its chip actually counts down and a line saying where that
/// number came from — typed, measured from your own casts, or documented.
///
/// Lifted out of <c>OptionsWindow</c> rather than added to it. That file is a ratchet
/// hotspot and this surface is exactly the shape that belongs outside one: it touches two
/// objects and two named controls and nothing else in the window. `QuestChecklistView` is
/// the worked example (CLAUDE.md — lift a surface out, don't grow the file).
///
/// Rows come from <see cref="MezDurationRows"/>, which the Avalonia editor also calls, so
/// the two cannot come to different words about the precedence.
/// </summary>
internal sealed class MezDurationsView
{
    private readonly ContentControl _host;
    private readonly TextBlock _blurb;
    private readonly MezTracker _tracker;
    private readonly MezOverrides _overrides;

    public MezDurationsView(ContentControl host, TextBlock blurb,
        MezTracker tracker, MezOverrides overrides)
    {
        _host = host;
        _blurb = blurb;
        _tracker = tracker;
        _overrides = overrides;
    }

    public void Render()
    {
        _blurb.Text = MezDurationRows.Blurb;
        var panel = new StackPanel();
        foreach (var row in MezDurationRows.Build(_tracker))
        {
            // Two columns, never a horizontal StackPanel: a stack measures with infinite
            // width in the stacking direction, so a long spell name would be CLIPPED
            // against the box with no ellipsis to say so (trap 14).
            var grid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = row.Spell, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = row.SourceNote,
            };
            name.SetResourceReference(TextBlock.ForegroundProperty,
                row.Source == MezDurationSource.Typed ? "AccentBrush" : "TextBrush");
            grid.Children.Add(name);

            var box = new TextBox
            {
                Text = row.DurationText, Width = 76, FontSize = 12, Tag = row.Spell,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                ToolTip = "A bare number here is SECONDS — \"44\" is 44 seconds, because "
                        + "mezzes are short. Clear the box to hand this spell back to EQBuddy.",
            };
            box.LostFocus += (s, _) => Commit(s as TextBox);
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) Commit(s as TextBox); };
            Grid.SetColumn(box, 1);
            grid.Children.Add(box);
            panel.Children.Add(grid);

            var note = new TextBlock
            {
                Text = row.SourceNote, FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 0),
            };
            note.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            panel.Children.Add(note);
        }
        _host.Content = panel;
    }

    /// <summary>A typed duration lands on commit. An empty box CLEARS it — the spell goes
    /// back to whatever EQBuddy has learned since, or the catalog.</summary>
    private void Commit(TextBox? box)
    {
        if (box is not { Tag: string spell }) return;
        var typed = MezDurationText.Parse(box.Text);
        if (typed == _overrides.Find(spell)) return;   // nothing moved; don't churn the file
        _overrides.Set(spell, typed);
        Render();
    }
}
