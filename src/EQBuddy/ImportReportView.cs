using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The "EQBuddy just read a dump you wrote" line, and its Undo.
///
/// **Lifted out of <see cref="GearCardView"/> on 2026-08-22, because the second surface
/// that needed it never got one.** <c>MainWindow.LastAchievementsImport</c> shipped on
/// 2026-08-20 documented as *"read by the Raids surface"* and was WRITTEN AND NEVER READ,
/// in both UIs — so an unprompted achievements import marked Sky rewards turned in and
/// raid clears complete, silently, with no report and no Undo, while the inventory half
/// of the same commit reported itself on the Gear tab. Trap 20's shape with the polarity
/// flipped: a property with a producer and no consumer, which no compiler, test or
/// screenshot can see, because an absent control photographs as an unremarkable card.
///
/// It is a class rather than a copied method for the reason the Avalonia twin
/// (<c>EQBuddy.Avalonia/ImportReportView.cs</c>) already is: the rule *"offer Undo only
/// when the import actually changed something"* is a decision, and a decision copied into
/// two cards is a decision that will disagree with itself. A <c>UIElement</c> has one
/// parent, so each host builds its own instance — the same rule as the cards themselves.
/// </summary>
internal sealed class ImportReportView
{
    private readonly StackPanel _panel = new() { Visibility = Visibility.Collapsed };
    private readonly Func<AutoImportOutcome?> _outcome;
    private readonly Action _changed;
    private AutoImportOutcome? _shown;

    /// <param name="outcome">The host's last import, re-read on every render — never
    /// captured, because a new dump replaces the record rather than mutating it.</param>
    /// <param name="changed">Run after an Undo, so the card that owns the rows this
    /// import touched can repaint them. The Raids surface only renders on kills and
    /// imports, so without this the un-marked bosses would sit there looking cleared.</param>
    public ImportReportView(Func<AutoImportOutcome?> outcome, Action changed)
    {
        _outcome = outcome;
        _changed = changed;
    }

    public UIElement Body => _panel;

    public void Render()
    {
        var outcome = _outcome();
        if (outcome is null) { _panel.Visibility = Visibility.Collapsed; return; }
        if (ReferenceEquals(outcome, _shown)) return;   // don't rebuild every tick
        _shown = outcome;

        _panel.Children.Clear();
        _panel.Visibility = Visibility.Visible;

        var line = DesignSystem.Text(Tok.TypeRole.Caption, outcome.Summary);
        line.TextWrapping = TextWrapping.Wrap;
        line.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
        // Ink by what the report SAYS, not by what it is: a run that skipped or could not
        // match something is not the same good news as a clean one, and the player has to
        // be able to tell at a glance which of the two they are looking at.
        line.Ink(outcome.Noted > 0 ? "WarnBrush" : "GoodBrush");
        // The WHY, on hover (Bevel, Helm-signed 2026-08-23). The glance says what happened;
        // a player who wants to know why something was skipped asks for it. Wrapped, because
        // an unwrapped two-paragraph tooltip is a line the width of the monitor.
        if (outcome.Detail is { Length: > 0 } detail)
            line.ToolTip = new TextBlock
            {
                Text = detail,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360,
            };
        _panel.Children.Add(line);

        // Offered only when the import actually changed something. A button that would put
        // back nothing is the same silent no-op this whole change is about — and `Noted`
        // deliberately does not count toward it: nothing was applied, so there is nothing
        // to reverse.
        if (outcome.Undo is not { } undo) return;

        var b = Theming.Button("Undo");
        b.FontSize = Tok.Spec(Tok.TypeRole.Caption).Size;
        b.HorizontalAlignment = HorizontalAlignment.Left;
        b.Margin = new Thickness(0, Tok.SpaceXxs, 0, 0);
        b.ToolTip = "Put back exactly what this import ticked — anything you ticked "
            + "yourself is left alone.";
        b.Click += (_, _) =>
        {
            undo();
            _shown = null;
            _panel.Visibility = Visibility.Collapsed;
            _changed();
        };
        _panel.Children.Add(b);
    }
}
