using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;

namespace EQBuddy.Avalonia;

/// <summary>
/// "EQBuddy read the dump you just made, and here is what it changed" — with an Undo.
///
/// A separate file rather than more of <c>MainWindow.cs</c>, deliberately: that file had
/// 31 lines of ratchet room left when this was written, and CLAUDE.md's rule is to lift a
/// surface out rather than grow the hotspot. This is the small version of the lift the
/// gear checklist still needs.
///
/// It exists because the auto-import is INVISIBLE without it, and an invisible import
/// reads exactly like the bug it replaces — David, 2026-08-20, ran the command, watched
/// the file appear and saw the window sit there. It reports even when nothing changed,
/// because "EQBuddy did nothing" and "EQBuddy never saw your file" look identical from
/// the outside and only one of them is a fault.
/// </summary>
internal sealed class ImportReportView
{
    private readonly StackPanel _panel = new() { IsVisible = false };
    private readonly Func<AutoImportOutcome?> _outcome;
    private readonly Action _changed;
    private AutoImportOutcome? _shown;

    public ImportReportView(Func<AutoImportOutcome?> outcome, Action changed)
    {
        _outcome = outcome;
        _changed = changed;
    }

    public Control Body => _panel;

    public void Render()
    {
        var outcome = _outcome();
        if (outcome is null) { _panel.IsVisible = false; return; }
        if (ReferenceEquals(outcome, _shown)) return;   // not every tick
        _shown = outcome;

        _panel.Children.Clear();
        _panel.IsVisible = true;
        var line = new TextBlock
        {
            Text = outcome.Summary,
            FontSize = DesignSystem.Size(Role.Caption),
            TextWrapping = TextWrapping.Wrap,
            // Ink by what the report SAYS: a run that skipped or could not match something
            // is not the same good news as a clean one, and the player should be able to
            // tell which at a glance. Same rule as the WPF twin.
            Foreground = outcome.Noted > 0 ? AppTheme.WarnBrush : AppTheme.GoodBrush,
            Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0),
        };
        // The WHY, on hover (Bevel, Helm-signed 2026-08-23). The glance says what happened;
        // a player who wants to know why something was skipped asks for it.
        if (outcome.Detail is { Length: > 0 } detail)
            ToolTip.SetTip(line, new TextBlock
            {
                Text = detail,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360,
                Foreground = AppTheme.TextBrush,
            });
        _panel.Children.Add(line);

        // Offered only when the import actually changed something. A button that would put
        // back nothing is the same silent no-op this whole change is about.
        if (outcome.Undo is not { } undo) return;
        var b = AppTheme.ActionButton("Undo",
            "Put back exactly what this import ticked — anything you ticked yourself is "
            + "left alone.");
        b.FontSize = DesignSystem.Size(Role.Caption);
        b.HorizontalAlignment = HorizontalAlignment.Left;
        b.Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, 0);
        b.Click += (_, _) =>
        {
            undo();
            _shown = null;
            _panel.IsVisible = false;
            _changed();
        };
        _panel.Children.Add(b);
    }
}
