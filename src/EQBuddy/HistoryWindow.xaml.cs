using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;

namespace EQBuddy;

public partial class HistoryWindow : Window
{
    private readonly SessionRepository _repo;
    private List<SessionRow> _rows = [];
    private SessionRow? _selected;
    private StatsSnapshot? _selectedSnapshot;

    public HistoryWindow(SessionRepository repo)
    {
        InitializeComponent();
        _repo = repo;
        RefreshFilters();
        RefreshList();
    }

    private void RefreshFilters()
    {
        var current = CharFilter.SelectedItem as string;
        CharFilter.Items.Clear();
        CharFilter.Items.Add("All characters");
        foreach (var (server, character) in _repo.Characters())
            CharFilter.Items.Add($"{character} ({server})");
        CharFilter.SelectedItem = current is not null && CharFilter.Items.Contains(current)
            ? current : "All characters";
    }

    private (string? Server, string? Character) SelectedFilter()
    {
        if (CharFilter.SelectedItem is not string sel || sel == "All characters")
            return (null, null);
        var open = sel.LastIndexOf(" (", StringComparison.Ordinal);
        return (sel[(open + 2)..^1], sel[..open]);
    }

    private void RefreshList()
    {
        var (server, character) = SelectedFilter();
        _rows = _repo.Query(server, character, SearchBox.Text);
        SessionList.Items.Clear();
        foreach (var r in _rows)
        {
            var dur = TimeSpan.FromSeconds(r.ElapsedSeconds);
            SessionList.Items.Add(
                $"{r.StartLocal:MMM d h:mm tt} · {r.Character}\n" +
                $"   {(r.PrimaryZone.Length > 0 ? r.PrimaryZone : "—")} · {(int)dur.TotalHours}h {dur.Minutes}m · " +
                $"{r.Kills} kills · {r.XpPercent:0.#}% xp · {StatsSnapshot.FormatCoin(r.Copper)}" +
                (r.EndReason == "RecoveredAfterCrash" ? " · (recovered)" : "") +
                (r.EndReason == "Active" ? " · (in progress)" : ""));
        }
        CountText.Text = $"{_rows.Count} session{(_rows.Count == 1 ? "" : "s")}";
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => RefreshList();
    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RefreshList();

    private void OnSessionSelected(object sender, SelectionChangedEventArgs e)
    {
        // Two selected sessions → side-by-side comparison (COMPARE-*).
        if (SessionList.SelectedItems.Count == 2)
        {
            var idx = SessionList.SelectedItems.Cast<object>()
                .Select(item => SessionList.Items.IndexOf(item)).OrderBy(x => x).ToList();
            if (idx[0] >= 0 && idx[1] < _rows.Count)
            {
                _selected = null;
                ShowText(BuildComparison(_rows[idx[0]], _rows[idx[1]]));
                return;
            }
        }

        var i = SessionList.SelectedIndex;
        if (i < 0 || i >= _rows.Count) { _selected = null; return; }
        _selected = _rows[i];
        _selectedSnapshot = _repo.LoadSnapshot(_selected.Id);
        NoteBox.Text = _selected.Note;
        TagsBox.Text = _selected.Tags;
        if (_selectedSnapshot is null) ShowText("Could not load session detail.");
        else ShowSession(_selected, _selectedSnapshot);
    }

    /// <summary>Plain-text mode (comparison, import status, empty state).</summary>
    private void ShowText(string text)
    {
        DetailText.Text = text;
        DamageVisualLabel.Visibility = HealVisualLabel.Visibility = Visibility.Collapsed;
        DamageVisualList.Items.Clear();
        HealVisualList.Items.Clear();
        DetailRest.Visibility = Visibility.Collapsed;
    }

    /// <summary>Session detail: text header, then the same bar-row breakdowns as the
    /// live widget for damage sources and heals, then the remaining text sections.</summary>
    private void ShowSession(SessionRow r, StatsSnapshot s)
    {
        DetailText.Text = BuildHeaderText(r, s).TrimEnd();
        BreakdownRows.FillAbilityRows(this, DamageVisualList, s.DamageBySource, "ability", "dps", max: 10);
        DamageVisualLabel.Visibility = s.DamageBySource.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        BreakdownRows.FillAbilityRows(this, HealVisualList, s.HealsBySpell, "spell", "hps", max: 6);
        HealVisualLabel.Visibility = s.HealsBySpell.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var rest = BuildRestText(s).Trim();
        DetailRest.Text = rest;
        DetailRest.Visibility = rest.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    internal static string BuildOverview(SessionRow r, StatsSnapshot s) =>
        BuildHeaderText(r, s) + BuildBarsText(s) + BuildRestText(s);

    private static string BuildHeaderText(SessionRow r, StatsSnapshot s)
    {
        var sb = new StringBuilder();
        var dur = TimeSpan.FromSeconds(r.ElapsedSeconds);
        var act = TimeSpan.FromSeconds(r.ActiveSeconds);
        sb.AppendLine($"{r.Character} ({r.Server}) — {r.StartLocal:dddd MMM d, h:mm tt}");
        sb.AppendLine($"Duration {(int)dur.TotalHours}h {dur.Minutes}m · active {(int)act.TotalMinutes}m · ended: {r.EndReason}");
        sb.AppendLine();
        sb.AppendLine($"Kills      {s.YourKillCount} (+{s.PartyKillCount} group) · {s.KillsPerHour:0.0}/hr");
        sb.AppendLine($"XP         {s.XpPercent:0.0}% · {s.XpPerHour:0.0}%/hr" +
                      (s.Levels.Count > 0 ? $" · {string.Join(", ", s.Levels.Select(l => l.Text))}" : "") +
                      (s.AaGained > 0 ? $" · {s.AaGained} AA" : ""));
        sb.AppendLine($"Damage     {s.DamageDealt:N0} dealt · {s.SessionDps:0.0} dps · taken {s.DamageTaken:N0}");
        if (s.HealingDone > 0)
            sb.AppendLine($"Healing    {s.HealingDone:N0} done · {s.Hps:0.#} hps");
        sb.AppendLine($"Money      {StatsSnapshot.FormatCoin(s.Copper)} ({StatsSnapshot.FormatCoin(s.CopperPerHour)}/hr)");
        sb.AppendLine($"Deaths     {s.Deaths.Count}");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>Text-bar rendition of the breakdowns — used by Copy summary, where the
    /// visual rows can't travel; the detail pane shows real bar rows instead.</summary>
    private static string BuildBarsText(StatsSnapshot s)
    {
        var sb = new StringBuilder();
        if (s.DamageBySource.Count > 0)
        {
            sb.AppendLine("Top damage sources:");
            var grand = Math.Max(1, s.DamageBySource.Sum(x => x.Total));
            var top = Math.Max(1, s.DamageBySource.Max(x => x.Total));
            foreach (var d in s.DamageBySource.Take(8))
                sb.AppendLine($"  {d.Name,-24} {ShareBar((double)d.Total / top),-10} {d.Total,8:N0}" +
                    $" · {100.0 * d.Total / grand,3:0}% · {d.Hits} hits · avg {(double)d.Total / d.Hits:0.#}" +
                    (d.ActiveSeconds > 0 ? $" · {d.Total / d.ActiveSeconds:0.#} dps" : "") +
                    (d.Crits > 0 ? $" · {100.0 * d.Crits / Math.Max(1, d.Hits):0}% crit" : ""));
            sb.AppendLine();
        }
        if (s.HealsBySpell.Count > 0)
        {
            sb.AppendLine("Top heals:");
            var hGrand = Math.Max(1, s.HealsBySpell.Sum(x => x.Total));
            var hTop = Math.Max(1, s.HealsBySpell.Max(x => x.Total));
            foreach (var h in s.HealsBySpell.Take(6))
                sb.AppendLine($"  {h.Name,-24} {ShareBar((double)h.Total / hTop),-10} {h.Total,8:N0}" +
                    $" · {100.0 * h.Total / hGrand,3:0}% · {h.Hits} cast{(h.Hits == 1 ? "" : "s")}" +
                    $" · avg {(double)h.Total / Math.Max(1, h.Hits):0.#}" +
                    (h.ActiveSeconds > 0 ? $" · {h.Total / h.ActiveSeconds:0.#} hps" : ""));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildRestText(StatsSnapshot s)
    {
        var sb = new StringBuilder();
        if (s.YourKills.Count > 0)
        {
            sb.AppendLine("Kills by creature:");
            foreach (var k in s.YourKills.Take(10))
                sb.AppendLine($"  {k.Name,-28} ×{k.Count}");
            sb.AppendLine();
        }
        if (s.Loot.Count > 0)
        {
            sb.AppendLine("Loot:");
            foreach (var l in s.Loot.Take(15))
                sb.AppendLine($"  {l.Item,-34} ×{l.Count}");
            sb.AppendLine();
        }
        var farmed = s.Mobs.Where(m => m.Kills > 0).Take(8).ToList();
        if (farmed.Count > 0)
        {
            sb.AppendLine("Mob farming (observed personal rates):");
            foreach (var m in farmed)
            {
                sb.AppendLine($"  {m.Name} — {m.Kills} kills · avg fight {m.AvgFightSeconds:0}s · " +
                              $"{m.XpPercent:0.0}% xp · {StatsSnapshot.FormatCoin(m.Copper)}");
                foreach (var l in m.Loot.Take(4))
                    sb.AppendLine($"      {l.Item,-30} ×{l.Count}" +
                        (l.DropRatePct is { } pct ? $"  {pct:0.#}% ({l.Count}/{m.Kills})" : ""));
            }
            sb.AppendLine();
        }
        if (s.Stances.Count > 0)
            sb.AppendLine("Stances: " + string.Join(" · ",
                s.Stances.Select(x => $"{x.Name} {x.Damage:N0} dmg over {(int)x.CombatSeconds}s ({x.Dps:0.#} dps)")));
        if (s.Zones.Count > 0)
            sb.AppendLine("Zones: " + string.Join(" → ", s.Zones.Select(z => z.Text)));
        if (s.Markers.Count > 0)
            sb.AppendLine("Markers: " + string.Join(" · ", s.Markers.Select(m => $"{m.Text} ({m.Time:h:mm tt})")));
        return sb.ToString();
    }

    /// <summary>Monospace share bar (relative to the top entry) — survives copy-summary.</summary>
    private static string ShareBar(double frac) =>
        new('█', Math.Clamp((int)Math.Round(frac * 10), 1, 10));

    private string BuildComparison(SessionRow ra, SessionRow rb)
    {
        var sa = _repo.LoadSnapshot(ra.Id);
        var sb2 = _repo.LoadSnapshot(rb.Id);
        if (sa is null || sb2 is null) return "Could not load one of the sessions.";
        var b = new StringBuilder();
        b.AppendLine("SESSION COMPARISON");
        b.AppendLine($"A: {ra.Character} · {ra.StartLocal:MMM d h:mm tt} · {ra.PrimaryZone}");
        b.AppendLine($"B: {rb.Character} · {rb.StartLocal:MMM d h:mm tt} · {rb.PrimaryZone}");
        if (ra.Character != rb.Character || ra.PrimaryZone != rb.PrimaryZone)
            b.AppendLine("(different character/zone — rates may not compare directly)");
        b.AppendLine();
        b.AppendLine($"{"",-16}{"A",14}{"B",14}");
        void Row(string label, string a2, string b3) => b.AppendLine($"{label,-16}{a2,14}{b3,14}");
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

    private void OnImportLog(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "EverQuest logs (eqlog_*.txt)|*.txt",
            Title = "Import an existing log into session history",
        };
        if (dlg.ShowDialog(this) != true) return;
        var path = dlg.FileName;
        var info = CharacterLog.FromPath(path);
        var character = info?.Character ?? Path.GetFileNameWithoutExtension(path);
        var server = info?.Server ?? "imported";
        ShowText($"Importing {Path.GetFileName(path)}…");

        Task.Run(() =>
        {
            var imported = 0;
            try
            {
                // Same parser + session-gap model as live monitoring (IMPORT-009):
                // each 60-minute-gap rollover persists one reconstructed session.
                var stats = new SessionStats { CharacterName = character, ServerName = server };
                stats.SessionEnding += snap =>
                {
                    if (SessionRepository.IsMeaningful(snap))
                    {
                        _repo.Checkpoint(0, snap, server, character, "ImportedBoundary");
                        imported++;
                    }
                };
                foreach (var line in File.ReadLines(path))   // streamed (IMPORT-003)
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
            Dispatcher.Invoke(() =>
            {
                ShowText($"Imported {imported} session{(imported == 1 ? "" : "s")} from {Path.GetFileName(path)}. " +
                    "Re-importing the same file updates the existing rows rather than duplicating them.");
                RefreshFilters();
                RefreshList();
            });
        });
    }

    private void OnSaveMeta(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        _repo.SetNoteTags(_selected.Id, NoteBox.Text.Trim(), TagsBox.Text.Trim());
        RefreshList();
    }

    private void OnCopySummary(object sender, RoutedEventArgs e)
    {
        if (_selected is null || _selectedSnapshot is null) return;
        Clipboard.SetText(BuildOverview(_selected, _selectedSnapshot));
    }

    private void OnExportJson(object sender, RoutedEventArgs e)
    {
        if (_selected is null || _selectedSnapshot is null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"eqbuddy-{_selected.Character}-{_selected.StartLocal:yyyyMMdd-HHmm}.json",
            Filter = "JSON|*.json",
        };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllText(dlg.FileName,
            System.Text.Json.JsonSerializer.Serialize(_selectedSnapshot,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        if (MessageBox.Show(this,
                $"Delete the {_selected.StartLocal:MMM d h:mm tt} session for {_selected.Character}? This cannot be undone.",
                "Delete session", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        _repo.Delete(_selected.Id);
        _selected = null;
        _selectedSnapshot = null;
        ShowText("Select a session.");
        RefreshFilters();
        RefreshList();
    }
}
