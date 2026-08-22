using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;

namespace EQBuddy;

/// <summary>
/// Zone knowledge sharing (David's map brief, 2026-08-13): export this zone's
/// spawn-point archive + learned timers as one paste-safe string, import someone
/// else's with a full preview first, or submit yours to EQBuddy through the same
/// GitHub Discussions door feedback uses — reviewed by people, shipped in a
/// release, never synced silently. Zero telemetry, always contribution: nothing
/// leaves this machine unless the player pastes it somewhere themselves.
///
/// Import safety is the deviation gate (ZoneShare.DeviationFlagFraction): a timer
/// far off the zone's established clock arrives FLAGGED and applies only if the
/// importer opts in per-import — the Befallen-is-about-4:30 test, in the UI.
/// </summary>
public sealed class ZoneShareWindow : Window
{
    private const string Repo = "https://github.com/DranakCorps-bot/EQBuddy";

    // Core collaborators only — no MainWindow reach-back, so an Avalonia port
    // passes the same three objects (review 2026-08-13).
    private readonly SpawnPointLedger _ledger;
    private readonly SpawnCatalog _catalog;
    private readonly SpawnOverrides _overrides;
    private readonly string _zone;
    private readonly TextBox _importBox = new()
    {
        FontSize = 11, AcceptsReturn = false, TextWrapping = TextWrapping.Wrap,
        MaxLines = 3, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new Thickness(4, 3, 4, 3),
    };
    private readonly StackPanel _previewPanel = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
    private readonly CheckBox _includeFlagged = new()
    {
        Content = "Also apply the flagged timers (I trust this source)",
        FontSize = 10.5, Margin = new Thickness(0, 6, 0, 0), IsChecked = false,
    };
    private readonly Button _applyBtn;
    private readonly TextBlock _statusLine = new()
    {
        FontSize = 10.5, Margin = new Thickness(0, 8, 0, 0),
        TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed,
    };
    private ZoneShare.Preview? _preview;

    public ZoneShareWindow(SpawnPointLedger ledger, SpawnCatalog catalog,
        SpawnOverrides overrides, string zone)
    {
        _ledger = ledger;
        _catalog = catalog;
        _overrides = overrides;
        _zone = zone;
        Title = $"Zone knowledge — {zone}";
        Width = 470;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        SetResourceReference(BackgroundProperty, "BgBrush");

        var root = new StackPanel { Margin = new Thickness(14, 10, 14, 12) };

        var archive = _ledger.Snapshot(zone);
        var timers = TimerCount(zone);
        var intro = Dim(
            $"Your {zone} archive: {archive.Points.Count} spawn point{S(archive.Points.Count)}, " +
            $"{archive.Points.Sum(p => p.TotalKills())} kill{S(archive.Points.Sum(p => p.TotalKills()))} observed, " +
            $"{timers} timer{S(timers)} (learned and yours both travel). Everything below is " +
            "explicit — nothing is sent or fetched unless you click it, and imports show you " +
            "every change first.");
        intro.Margin = new Thickness(0, 0, 0, 10);
        root.Children.Add(intro);

        // ---- Export ----
        var copy = Theming.Button("Copy share string");
        copy.Click += (_, _) => OnCopy();
        root.Children.Add(Section("Share with a friend",
            "One string carries the points and timers above — including timers you typed " +
            "yourself. Paste it in guild chat, Discord, anywhere — they import it below.", copy));

        // ---- Import ----
        var preview = Theming.Button("Preview…");
        preview.Click += (_, _) => OnPreview();
        _applyBtn = Theming.Button("Apply import");
        _applyBtn.Margin = new Thickness(0, 6, 0, 0);
        _applyBtn.HorizontalAlignment = HorizontalAlignment.Left;
        _applyBtn.Click += (_, _) => OnApply();
        var importBody = new StackPanel();
        var pasteRow = new DockPanel();
        DockPanel.SetDock(preview, Dock.Right);
        preview.Margin = new Thickness(6, 0, 0, 0);
        _importBox.SetResourceReference(Control.BackgroundProperty, "ComboBoxBrush");
        _importBox.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        _importBox.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        pasteRow.Children.Add(preview);
        pasteRow.Children.Add(_importBox);
        importBody.Children.Add(pasteRow);
        importBody.Children.Add(_previewPanel);
        root.Children.Add(Section("Import from a friend",
            "Paste an EQBZ string. You'll see exactly what it adds — new points, refined points, " +
            "and every timer change, with big deviations flagged — before anything applies.",
            importBody));

        // ---- Submit ----
        var submit = Theming.Button("Submit to EQBuddy…");
        submit.Click += (_, _) => OnSubmit();
        root.Children.Add(Section("Contribute to everyone",
            "Opens a prefilled GitHub Discussion with your share string — you review every word " +
            "and post it yourself. Reviewed submissions ship in a future release, credited.",
            submit));

        _statusLine.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        root.Children.Add(_statusLine);

        Content = root;
    }

    private int TimerCount(string zone)
    {
        var z = _catalog.FindZone(zone);
        var catalogTimers = z?.Named.Count(e =>
            _overrides.Find(zone, e.Name) is { RespawnSeconds: not null }) ?? 0;
        // Player-added named travel too — count them the way Export carries them.
        return catalogTimers + _overrides.CustomFor(zone).Count(c => c.Override.RespawnSeconds is not null);
    }

    private Border Section(string title, string blurb, UIElement body)
    {
        var stack = new StackPanel();
        var head = new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.SemiBold };
        head.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        stack.Children.Add(head);
        var text = Dim(blurb);
        text.Margin = new Thickness(0, 2, 0, 7);
        stack.Children.Add(text);
        stack.Children.Add(body);
        var border = new Border
        {
            Child = stack, CornerRadius = new CornerRadius(9),
            Padding = new Thickness(11, 8, 11, 10), Margin = new Thickness(0, 0, 0, 8),
            BorderThickness = new Thickness(1),
        };
        border.SetResourceReference(Border.BackgroundProperty, "RaisedBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "HairlineBrush");
        return border;
    }

    private static TextBlock Dim(string text)
    {
        var tb = new TextBlock { Text = text, FontSize = 10.5, TextWrapping = TextWrapping.Wrap };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        return tb;
    }

    private static string S(int n) => n == 1 ? "" : "s";

    private string ExportString() => ZoneShare.Export(
        _ledger.Snapshot(_zone),
        _catalog.FindZone(_zone),
        _overrides);

    private void Status(string text)
    {
        _statusLine.Text = text;
        _statusLine.Visibility = Visibility.Visible;
    }

    private void OnCopy()
    {
        try
        {
            Clipboard.SetText(ExportString());
            Status("Copied to the clipboard — paste it to a friend, they import it in this same window.");
        }
        catch (Exception ex) { App.LogError(ex); Status("Couldn't reach the clipboard — try again."); }
    }

    private void OnPreview()
    {
        // Decode once (capped + validated), then diff against the zone the string
        // itself names — pasting a Befallen string while the map shows Guk works.
        var payload = ZoneShare.TryDecode(_importBox.Text);
        if (payload is null)
        {
            _preview = null;
            _previewPanel.Visibility = Visibility.Collapsed;
            Status("That doesn't look like a healthy EQBZ share string — check the paste caught all of it.");
            return;
        }
        var zone = payload.Zone;
        _preview = ZoneShare.Preview_(payload, _ledger.Snapshot(zone), _catalog.FindZone(zone), _overrides);

        _previewPanel.Children.Clear();
        var head = new TextBlock
        {
            Text = $"{zone}: +{_preview.NewPoints} new point{S(_preview.NewPoints)}, " +
                $"{_preview.RefinedPoints} refined, +{_preview.NewObservations} observation{S(_preview.NewObservations)}",
            FontSize = 11, FontWeight = FontWeights.SemiBold,
        };
        head.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        _previewPanel.Children.Add(head);

        foreach (var t in _preview.Timers)
        {
            var cur = t.CurrentSeconds is { } c ? EQBuddy.UI.Shared.Countdown.Format(TimeSpan.FromSeconds(c)) : "—";
            var line = new TextBlock
            {
                FontSize = 10.5, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap,
                // Text-presentation warning glyph, not the color emoji — emoji
                // ignore Foreground and this line must tint BadBrush (house rule).
                // A REFUSED row is not a risky row: the catalog says this mob has no
                // cycle, so no tick-box can apply it. Dim, and it says why — the old text
                // called it "no local baseline to corroborate" under a checkbox offering
                // to apply it anyway (Fable 5, v1.99.3 release review).
                Text = t.Triggered
                    ? $"{t.Name}: no cycle to import — {EQBuddy.UI.Shared.ZoneShareText.RefusedReason(t)}"
                    : t.Flagged
                    ? $"⚠︎ {t.Name}: {cur} → {EQBuddy.UI.Shared.Countdown.Format(TimeSpan.FromSeconds(t.IncomingSeconds))} — " +
                      (t.CurrentSeconds is null ? "no local baseline to corroborate" : "big change from the known clock")
                    : $"{t.Name}: {cur} → {EQBuddy.UI.Shared.Countdown.Format(TimeSpan.FromSeconds(t.IncomingSeconds))}",
            };
            line.SetResourceReference(TextBlock.ForegroundProperty,
                t.Triggered ? "DimBrush" : t.Flagged ? "BadBrush" : "TextBrush");
            _previewPanel.Children.Add(line);
        }
        if (_preview.Timers.Count == 0)
            _previewPanel.Children.Add(Dim("No timers in this string — spawn points only."));

        _includeFlagged.Visibility = _preview.FlaggedTimers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _includeFlagged.IsChecked = false;
        _includeFlagged.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        _previewPanel.Children.Add(_includeFlagged);
        _previewPanel.Children.Add(_applyBtn);
        _previewPanel.Visibility = Visibility.Visible;
        _statusLine.Visibility = Visibility.Collapsed;
    }

    private void OnApply()
    {
        if (_preview is null) return;
        _ledger.ApplyImport(_preview, _overrides,
            includeFlagged: _includeFlagged.IsChecked == true);
        var skipped = _includeFlagged.IsChecked == true ? 0 : _preview.FlaggedTimers.Count;
        Status($"Applied to {_preview.Payload.Zone}" +
            (skipped > 0 ? $" — {skipped} flagged timer{S(skipped)} left untouched." : "."));
        _previewPanel.Visibility = Visibility.Collapsed;
        _preview = null;
        _importBox.Clear();
        // The map picks the new points up on its next tick — the circle layer
        // rebuilds whenever the archive's point or kill count changes.
    }

    private void OnSubmit()
    {
        var body =
            $"Zone knowledge submission for **{_zone}** — from EQBuddy's Share zone knowledge window.\n\n" +
            "```\n" + ExportString() + "\n```\n\n" +
            "_Import it via Map → Share zone knowledge → paste → Preview. If it checks out, " +
            "please fold it into a future release._\n\n---\n" +
            $"_EQBuddy {typeof(ZoneShareWindow).Assembly.GetName().Version?.ToString(3) ?? "?"}_";
        var url = $"{Repo}/discussions/new?category=ideas" +
            $"&title={Uri.EscapeDataString($"Zone knowledge: {_zone}")}" +
            $"&body={Uri.EscapeDataString(body)}";
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            Status("Opened on GitHub for your review — nothing posts until you do.");
        }
        catch (Exception ex) { App.LogError(ex); }
    }
}
