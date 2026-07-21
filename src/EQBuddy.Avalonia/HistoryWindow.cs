using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using EQBuddy.Core;

namespace EQBuddy.Avalonia;

public sealed class HistoryWindow : Window
{
    private readonly SessionRepository _repo;
    private readonly ComboBox _charFilter = new() { Width = 180 };
    private readonly TextBox _searchBox = TextBox("");
    private readonly TextBlock _countText = AppTheme.DimText("");
    private readonly StackPanel _sessionRows = new();
    private readonly TextBlock _detailText = new()
    {
        Text = "Select a session.",
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Foreground = AppTheme.TextBrush,
        FontFamily = FontFamily.Parse("Consolas, monospace"),
    };
    private readonly TextBox _noteBox = TextBox("");
    private readonly TextBox _tagsBox = TextBox("");
    private List<SessionRow> _rows = [];
    private readonly HashSet<int> _selectedIndexes = [];
    private SessionRow? _selected;
    private StatsSnapshot? _selectedSnapshot;

    public HistoryWindow(SessionRepository repo)
    {
        _repo = repo;
        Title = "EQBuddy - Session History";
        Width = 860;
        Height = 560;
        MinWidth = 640;
        MinHeight = 400;
        Background = AppTheme.BgBrush;
        StyleDarkCombo(_charFilter);
        Content = BuildContent();
        RefreshFilters();
        RefreshList();
    }

    private Control BuildContent()
    {
        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        root.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(330)));
        root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var filter = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        filter.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(180)));
        filter.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        filter.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        filter.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _charFilter.SelectionChanged += (_, _) => RefreshList();
        filter.Children.Add(_charFilter);
        _searchBox.Margin = new Thickness(8, 0, 0, 0);
        _searchBox.TextChanged += (_, _) => RefreshList();
        Grid.SetColumn(_searchBox, 1);
        filter.Children.Add(_searchBox);
        _countText.VerticalAlignment = VerticalAlignment.Center;
        _countText.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(_countText, 2);
        filter.Children.Add(_countText);
        var import = AppTheme.IconButton("Import log...", "Parse an existing eqlog file into session history");
        import.FontSize = 12;
        import.Margin = new Thickness(8, 0, 0, 0);
        import.Click += OnImportLog;
        Grid.SetColumn(import, 3);
        filter.Children.Add(import);
        Grid.SetColumnSpan(filter, 2);
        root.Children.Add(filter);

        var sessionScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = new SolidColorBrush(Color.FromArgb(0x44, 0x1C, 0x19, 0x17)),
            Content = _sessionRows,
        };
        Grid.SetRow(sessionScroll, 1);
        root.Children.Add(sessionScroll);

        var detail = new Grid { Margin = new Thickness(10, 0, 0, 0) };
        detail.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        detail.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _detailText };
        detail.Children.Add(scroll);
        var meta = BuildMetaPanel();
        Grid.SetRow(meta, 1);
        detail.Children.Add(meta);
        Grid.SetRow(detail, 1);
        Grid.SetColumn(detail, 1);
        root.Children.Add(detail);
        return root;
    }

    private Control BuildMetaPanel()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(LabeledBox("Notes:", _noteBox));
        panel.Children.Add(LabeledBox("Tags:", _tagsBox, new Thickness(0, 4, 0, 0)));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(ActionButton("Save notes", OnSaveMeta));
        buttons.Children.Add(ActionButton("Copy summary", OnCopySummary, new Thickness(8, 0, 0, 0)));
        buttons.Children.Add(ActionButton("Export JSON", OnExportJson, new Thickness(8, 0, 0, 0)));
        var delete = ActionButton("Delete session", OnDelete, new Thickness(24, 0, 0, 0));
        delete.Foreground = AppTheme.BadBrush;
        buttons.Children.Add(delete);
        panel.Children.Add(buttons);
        return panel;
    }

    private void RefreshFilters()
    {
        var current = _charFilter.SelectedItem as string;
        _charFilter.Items.Clear();
        _charFilter.Items.Add("All characters");
        foreach (var (server, character) in _repo.Characters())
            _charFilter.Items.Add($"{character} ({server})");
        _charFilter.SelectedItem = current is not null && _charFilter.Items.Contains(current)
            ? current : "All characters";
    }

    private (string? Server, string? Character) SelectedFilter()
    {
        if (_charFilter.SelectedItem is not string sel || sel == "All characters")
            return (null, null);
        var open = sel.LastIndexOf(" (", StringComparison.Ordinal);
        return (sel[(open + 2)..^1], sel[..open]);
    }

    private void RefreshList()
    {
        var (server, character) = SelectedFilter();
        _rows = _repo.Query(server, character, _searchBox.Text);
        _selectedIndexes.Clear();
        _selected = null;
        _selectedSnapshot = null;
        _sessionRows.Children.Clear();
        for (var i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            var dur = TimeSpan.FromSeconds(r.ElapsedSeconds);
            _sessionRows.Children.Add(SessionRow(i,
                $"{r.StartLocal:MMM d h:mm tt} - {r.Character}\n" +
                $"   {(r.PrimaryZone.Length > 0 ? r.PrimaryZone : "-")} - {(int)dur.TotalHours}h {dur.Minutes}m - " +
                $"{r.Kills} kills - {r.XpPercent:0.#}% xp - {StatsSnapshot.FormatCoin(r.Copper)}" +
                (r.EndReason == "RecoveredAfterCrash" ? " - (recovered)" : "") +
                (r.EndReason == "Active" ? " - (in progress)" : "")));
        }
        _countText.Text = $"{_rows.Count} session{(_rows.Count == 1 ? "" : "s")}";
    }

    private void OnSessionSelected(int index, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            if (!_selectedIndexes.Remove(index))
                _selectedIndexes.Add(index);
        }
        else
        {
            _selectedIndexes.Clear();
            _selectedIndexes.Add(index);
        }
        RefreshRowSelectionVisuals();

        if (_selectedIndexes.Count == 2)
        {
            var idx = _selectedIndexes.OrderBy(x => x).ToList();
            _selected = null;
            _selectedSnapshot = null;
            _detailText.Text = BuildComparison(_rows[idx[0]], _rows[idx[1]]);
            return;
        }

        if (index < 0 || index >= _rows.Count) { _selected = null; return; }
        _selected = _rows[index];
        _selectedSnapshot = _repo.LoadSnapshot(_selected.Id);
        _noteBox.Text = _selected.Note;
        _tagsBox.Text = _selected.Tags;
        _detailText.Text = _selectedSnapshot is null
            ? "Could not load session detail."
            : BuildOverview(_selected, _selectedSnapshot);
    }

    private void RefreshRowSelectionVisuals()
    {
        for (var i = 0; i < _sessionRows.Children.Count; i++)
        {
            if (_sessionRows.Children[i] is not Border row) continue;
            var selected = _selectedIndexes.Contains(i);
            row.Background = selected
                ? new SolidColorBrush(Color.FromArgb(0x88, 0x5A, 0x45, 0x13))
                : new SolidColorBrush(Color.FromArgb(0x55, 0x2A, 0x25, 0x1F));
            row.BorderBrush = selected ? AppTheme.AccentBrush : AppTheme.BorderBrush;
        }
    }

    internal static string BuildOverview(SessionRow r, StatsSnapshot s)
    {
        var sb = new StringBuilder();
        var dur = TimeSpan.FromSeconds(r.ElapsedSeconds);
        var act = TimeSpan.FromSeconds(r.ActiveSeconds);
        sb.AppendLine($"{r.Character} ({r.Server}) - {r.StartLocal:dddd MMM d, h:mm tt}");
        sb.AppendLine($"Duration {(int)dur.TotalHours}h {dur.Minutes}m - active {(int)act.TotalMinutes}m - ended: {r.EndReason}");
        sb.AppendLine();
        sb.AppendLine($"Kills      {s.YourKillCount} (+{s.PartyKillCount} group) - {s.KillsPerHour:0.0}/hr");
        sb.AppendLine($"XP         {s.XpPercent:0.0}% - {s.XpPerHour:0.0}%/hr" +
                      (s.Levels.Count > 0 ? $" - {string.Join(", ", s.Levels.Select(l => l.Text))}" : "") +
                      (s.AaGained > 0 ? $" - {s.AaGained} AA" : ""));
        sb.AppendLine($"Damage     {s.DamageDealt:N0} dealt - {s.SessionDps:0.0} dps - taken {s.DamageTaken:N0}");
        if (s.HealingDone > 0)
            sb.AppendLine($"Healing    {s.HealingDone:N0} done - {s.Hps:0.#} hps");
        sb.AppendLine($"Money      {StatsSnapshot.FormatCoin(s.Copper)} ({StatsSnapshot.FormatCoin(s.CopperPerHour)}/hr)");
        sb.AppendLine($"Deaths     {s.Deaths.Count}");
        sb.AppendLine();
        if (s.DamageBySource.Count > 0)
        {
            sb.AppendLine("Top damage sources:");
            var grand = Math.Max(1, s.DamageBySource.Sum(x => x.Total));
            var top = Math.Max(1, s.DamageBySource.Max(x => x.Total));
            foreach (var d in s.DamageBySource.Take(8))
                sb.AppendLine($"  {d.Name,-24} {ShareBar((double)d.Total / top),-10} {d.Total,8:N0}" +
                    $" - {100.0 * d.Total / grand,3:0}% - {d.Hits} hits - avg {(double)d.Total / Math.Max(1, d.Hits):0.#}" +
                    (d.ActiveSeconds > 0 ? $" - {d.Total / d.ActiveSeconds:0.#} dps" : "") +
                    (d.Crits > 0 ? $" - {100.0 * d.Crits / Math.Max(1, d.Hits):0}% crit" : ""));
            sb.AppendLine();
        }
        if (s.HealsBySpell.Count > 0)
        {
            sb.AppendLine("Top heals:");
            var grand = Math.Max(1, s.HealsBySpell.Sum(x => x.Total));
            var top = Math.Max(1, s.HealsBySpell.Max(x => x.Total));
            foreach (var h in s.HealsBySpell.Take(6))
                sb.AppendLine($"  {h.Name,-24} {ShareBar((double)h.Total / top),-10} {h.Total,8:N0}" +
                    $" - {100.0 * h.Total / grand,3:0}% - {h.Hits} cast{(h.Hits == 1 ? "" : "s")}" +
                    $" - avg {(double)h.Total / Math.Max(1, h.Hits):0.#}" +
                    (h.ActiveSeconds > 0 ? $" - {h.Total / h.ActiveSeconds:0.#} hps" : ""));
            sb.AppendLine();
        }
        if (s.YourKills.Count > 0)
        {
            sb.AppendLine("Kills by creature:");
            foreach (var k in s.YourKills.Take(10))
                sb.AppendLine($"  {k.Name,-28} x{k.Count}");
            sb.AppendLine();
        }
        if (s.Loot.Count > 0)
        {
            sb.AppendLine("Loot:");
            foreach (var l in s.Loot.Take(15))
                sb.AppendLine($"  {l.Item,-34} x{l.Count}");
            sb.AppendLine();
        }
        var farmed = s.Mobs.Where(m => m.Kills > 0).Take(8).ToList();
        if (farmed.Count > 0)
        {
            sb.AppendLine("Mob farming (observed personal rates):");
            foreach (var m in farmed)
            {
                sb.AppendLine($"  {m.Name} - {m.Kills} kills - avg fight {m.AvgFightSeconds:0}s - " +
                              $"{m.XpPercent:0.0}% xp - {StatsSnapshot.FormatCoin(m.Copper)}");
                foreach (var l in m.Loot.Take(4))
                    sb.AppendLine($"      {l.Item,-30} x{l.Count}" +
                        (l.DropRatePct is { } pct ? $"  {pct:0.#}% ({l.Count}/{m.Kills})" : ""));
            }
            sb.AppendLine();
        }
        if (s.Stances.Count > 0)
            sb.AppendLine("Stances: " + string.Join(" - ",
                s.Stances.Select(x => $"{x.Name} {x.Damage:N0} dmg over {(int)x.CombatSeconds}s ({x.Dps:0.#} dps)")));
        if (s.Zones.Count > 0)
            sb.AppendLine("Zones: " + string.Join(" -> ", s.Zones.Select(z => z.Text)));
        if (s.Markers.Count > 0)
            sb.AppendLine("Markers: " + string.Join(" - ", s.Markers.Select(m => $"{m.Text} ({m.Time:h:mm tt})")));
        return sb.ToString();
    }

    private static string ShareBar(double fraction) =>
        new('█', Math.Clamp((int)Math.Round(fraction * 10), 1, 10));

    private string BuildComparison(SessionRow ra, SessionRow rb)
    {
        var sa = _repo.LoadSnapshot(ra.Id);
        var sb2 = _repo.LoadSnapshot(rb.Id);
        if (sa is null || sb2 is null) return "Could not load one of the sessions.";
        var b = new StringBuilder();
        b.AppendLine("SESSION COMPARISON");
        b.AppendLine($"A: {ra.Character} - {ra.StartLocal:MMM d h:mm tt} - {ra.PrimaryZone}");
        b.AppendLine($"B: {rb.Character} - {rb.StartLocal:MMM d h:mm tt} - {rb.PrimaryZone}");
        if (ra.Character != rb.Character || ra.PrimaryZone != rb.PrimaryZone)
            b.AppendLine("(different character/zone - rates may not compare directly)");
        b.AppendLine();
        b.AppendLine($"{"",-16}{"A",14}{"B",14}");
        void Row(string label, string a, string c) => b.AppendLine($"{label,-16}{a,14}{c,14}");
        Row("Duration", $"{ra.ElapsedSeconds / 3600:0.0}h", $"{rb.ElapsedSeconds / 3600:0.0}h");
        Row("Active", $"{ra.ActiveSeconds / 60:0}m", $"{rb.ActiveSeconds / 60:0}m");
        Row("XP/hr", $"{sa.XpPerHour:0.0}%", $"{sb2.XpPerHour:0.0}%");
        Row("Kills/hr", $"{sa.KillsPerHour:0.0}", $"{sb2.KillsPerHour:0.0}");
        Row("Money/hr", StatsSnapshot.FormatCoin(sa.CopperPerHour), StatsSnapshot.FormatCoin(sb2.CopperPerHour));
        Row("DPS", $"{sa.SessionDps:0.0}", $"{sb2.SessionDps:0.0}");
        Row("HPS", $"{sa.Hps:0.0}", $"{sb2.Hps:0.0}");
        Row("Damage taken", $"{sa.DamageTaken:N0}", $"{sb2.DamageTaken:N0}");
        Row("Deaths", $"{sa.Deaths.Count}", $"{sb2.Deaths.Count}");
        Row("Loot items", $"{sa.LootTotal}", $"{sb2.LootTotal}");
        return b.ToString();
    }

    private async void OnImportLog(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import an existing log into session history",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("EverQuest logs") { Patterns = ["eqlog_*.txt", "*.txt"] },
                FilePickerFileTypes.All,
            ],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;
        var info = CharacterLog.FromPath(path);
        var character = info?.Character ?? Path.GetFileNameWithoutExtension(path);
        var server = info?.Server ?? "imported";
        _detailText.Text = $"Importing {Path.GetFileName(path)}...";
        _ = Task.Run(() =>
        {
            var imported = 0;
            try
            {
                var stats = new SessionStats { CharacterName = character, ServerName = server };
                stats.SessionEnding += snap =>
                {
                    if (SessionRepository.IsMeaningful(snap))
                    {
                        _repo.Checkpoint(0, snap, server, character, "ImportedBoundary");
                        imported++;
                    }
                };
                foreach (var line in File.ReadLines(path))
                {
                    var evt = LogParser.Parse(line);
                    if (evt is not null) stats.Apply(evt);
                }
                var final = stats.Snapshot();
                if (SessionRepository.IsMeaningful(final))
                {
                    _repo.Checkpoint(0, final, server, character, "ImportedBoundary");
                    imported++;
                }
            }
            catch (Exception ex) { CoreLog.Error(ex); }
            Dispatcher.UIThread.Post(() =>
            {
                _detailText.Text = $"Imported {imported} session{(imported == 1 ? "" : "s")} from {Path.GetFileName(path)}.";
                RefreshFilters();
                RefreshList();
            });
        });
    }

    private void OnSaveMeta(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_selected is null) return;
        _repo.SetNoteTags(_selected.Id, (_noteBox.Text ?? "").Trim(), (_tagsBox.Text ?? "").Trim());
        RefreshList();
    }

    private async void OnCopySummary(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_selected is null || _selectedSnapshot is null) return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(BuildOverview(_selected, _selectedSnapshot));
    }

    private async void OnExportJson(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_selected is null || _selectedSnapshot is null) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export session JSON",
            SuggestedFileName = $"eqbuddy-{_selected.Character}-{_selected.StartLocal:yyyyMMdd-HHmm}.json",
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
        });
        var path = file?.TryGetLocalPath();
        if (path is null) return;
        await File.WriteAllTextAsync(path,
            System.Text.Json.JsonSerializer.Serialize(_selectedSnapshot,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private void OnDelete(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_selected is null) return;
        _repo.Delete(_selected.Id);
        _selected = null;
        _selectedSnapshot = null;
        _detailText.Text = "Select a session.";
        RefreshFilters();
        RefreshList();
    }

    private static Control LabeledBox(string label, TextBox box, Thickness? margin = null)
    {
        var row = new Grid { Margin = margin ?? default };
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.Children.Add(AppTheme.DimText(label, new Thickness(0, 0, 6, 0)));
        Grid.SetColumn(box, 1);
        row.Children.Add(box);
        return row;
    }

    private static Button ActionButton(string text, EventHandler<global::Avalonia.Interactivity.RoutedEventArgs> action, Thickness? margin = null)
    {
        var button = AppTheme.IconButton(text, text);
        button.FontSize = 12;
        button.Margin = margin ?? default;
        button.Click += action;
        return button;
    }

    private static TextBox TextBox(string text)
    {
        var box = new TextBox
        {
            Text = text,
            FontSize = 12,
            Padding = new Thickness(6, 4),
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F)),
            Foreground = AppTheme.TextBrush,
            BorderBrush = AppTheme.BorderBrush,
            CaretBrush = AppTheme.AccentBrush,
            SelectionBrush = new SolidColorBrush(Color.FromArgb(0x77, 0xE3, 0xB3, 0x41)),
        };
        box.GotFocus += (_, _) => ApplyDarkInput(box);
        box.LostFocus += (_, _) => ApplyDarkInput(box);
        return box;
    }

    private static void ApplyDarkInput(TextBox box)
    {
        box.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F));
        box.Foreground = AppTheme.TextBrush;
        box.BorderBrush = AppTheme.BorderBrush;
    }

    private Border SessionRow(int index, string text)
    {
        var row = new Border
        {
            Margin = new Thickness(0, 0, 0, 2),
            Background = new SolidColorBrush(Color.FromArgb(0x55, 0x2A, 0x25, 0x1F)),
            BorderBrush = AppTheme.BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 6),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = AppTheme.TextBrush,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        row.PointerPressed += (_, args) =>
        {
            OnSessionSelected(index, args.KeyModifiers);
            args.Handled = true;
        };
        row.PointerEntered += (_, _) =>
        {
            if (!_selectedIndexes.Contains(index))
                row.Background = new SolidColorBrush(Color.FromArgb(0x77, 0x2F, 0x2A, 0x22));
        };
        row.PointerExited += (_, _) => RefreshRowSelectionVisuals();
        return row;
    }

    private static void StyleDarkCombo(ComboBox combo)
    {
        combo.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F));
        combo.Foreground = AppTheme.TextBrush;
        combo.BorderBrush = AppTheme.BorderBrush;
        combo.FontSize = 12;
        combo.GotFocus += (_, _) =>
        {
            combo.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F));
            combo.Foreground = AppTheme.TextBrush;
        };
        combo.LostFocus += (_, _) =>
        {
            combo.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F));
            combo.Foreground = AppTheme.TextBrush;
        };
    }
}
