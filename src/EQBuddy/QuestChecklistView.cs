using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// What the Epic 1.0 and Sky quest checklists need that is NOT a card: the auto-checkers
/// that tick their steps as you loot, and the achievement import that pre-marks Sky
/// rewards you finished before EQBuddy existed.
///
/// It was two tabbed surfaces on the widget until 2026-08-16, when both cards became a
/// single Quests launcher and the tabbed rendering moved to the Quest Tracker window
/// (which had grown its own General / Epic 1.0 / Plane of Sky tabs). Roughly 780 lines
/// of rendering went with them. What is left never touches a control: it reads and
/// writes the settings lists that the Quest Tracker window, the widget's Quests count
/// and EQBuddy Mobile all draw from — which is why the auto-ticking keeps working with
/// no card open at all, exactly as it did when the card was merely collapsed.
///
/// The window is still reached for the achievement import's preview dialog, and that is
/// now the whole of the coupling.
///
/// It has no unit tests, because the WPF layer has none (docs/TestPlan.md §5).
/// </summary>
internal sealed class QuestChecklistView
{
    private readonly MainWindow _w;
    private readonly AppSettings _settings;
    // Fetched on use, not on construction: this view must exist before MainWindow
    // restores any control, and the raid ledger is built later in that constructor.
    // Only the achievement import touches it, long after everything is in place.
    private readonly Func<RaidKillLedger> _raidLedger;

    public QuestChecklistView(MainWindow w, AppSettings settings, Func<RaidKillLedger> raidLedger)
    {
        _w = w;
        _settings = settings;
        _raidLedger = raidLedger;
    }

    /// <summary>The Quests card's one line: both checklists at a glance, so folding two
    /// cards into a launcher costs no information. Empty when neither checklist has been
    /// built yet — a card reading "0/0 · 0/0" would look broken rather than unstarted.</summary>
    public string SummaryLine()
    {
        var epic = _settings.EpicQuestChecklist;
        var sky = _settings.SkyQuestChecklist;
        var parts = new List<string>(2);
        if (epic.Count > 0) parts.Add($"Epic {epic.Count(i => i.Acquired)}/{epic.Count}");
        if (sky.Count > 0) parts.Add($"Sky {sky.Count(i => i.Acquired)}/{sky.Count}");
        return string.Join(" · ", parts);
    }

    // High-water marks for the loot auto-checkers: only the newly-looted delta ticks a
    // step, so a re-render can never double-count.
    private readonly Dictionary<string, int> _skyQuestLootSeen = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _epicQuestLootSeen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A new session (or a character switch) must forget what it had already
    /// counted, or the next snapshot reads as a burst of fresh loot.</summary>
    public void ResetLootSeen()
    {
        _skyQuestLootSeen.Clear();
        _epicQuestLootSeen.Clear();
    }

    private IEnumerable<EpicQuestChecklistItem> FilterEpicQuestRows(IEnumerable<EpicQuestChecklistItem> items) =>
        _settings.EpicQuestClassicOnly ? items.Where(i => i.AvailableInClassic) : items;

    internal void UpdateEpicQuestChecklist(StatsSnapshot s)
    {
        // No card to repaint any more: the widget's Quests line recomputes its counts
        // from these same lists every tick, and the Quest Tracker window watches them
        // through its own signature. Persisting the tick is the whole job here.
        if (AutoCheckEpicQuestLoot(s)) _settings.Save();
    }

    /// <summary>Last snapshot version each auto-checker processed (perf audit #13):
    /// loot can only change with an event, and every event bumps the version — so an
    /// unchanged version means the per-tick regroup can be skipped without moving any
    /// high-water mark. Every path that clears the seen-dictionaries (session
    /// identity, review entry, character switch) also moves the version, so the
    /// re-arm pass is never skipped.</summary>
    private long _epicAutoCheckVersion = -1;
    private long _skyAutoCheckVersion = -1;

    private bool AutoCheckEpicQuestLoot(StatsSnapshot s)
    {
        if (s.Version == _epicAutoCheckVersion) return false;   // perf audit #13
        _epicAutoCheckVersion = s.Version;
        var changed = false;
        // The class-scoping rules live in Core (EpicLootAutoCheck) where they are
        // tested — the Sky rules (#98/#106) over prose steps keyed by the catalog
        // items their text mentions (#121). Same high-water diff as Sky: only the
        // newly-looted delta ticks steps, so a re-render never double-counts.
        var myClasses = _w.QuestLedger?.ClassesFor(_w.QuestCharacterKey) ?? [];
        var lootByName = s.Loot
            .GroupBy(l => l.Item, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Count), StringComparer.OrdinalIgnoreCase);

        foreach (var key in _epicQuestLootSeen.Keys.ToList())
            if (!lootByName.ContainsKey(key))
                _epicQuestLootSeen[key] = 0;

        foreach (var (name, count) in lootByName)
        {
            _epicQuestLootSeen.TryGetValue(name, out var seen);
            _epicQuestLootSeen[name] = count;
            if (count <= seen) continue;
            changed |= EpicLootAutoCheck.Apply(_settings.EpicQuestChecklist, name,
                count - seen, myClasses, _settings.EpicQuestClass);
        }

        return changed;
    }

    internal void UpdateSkyQuestChecklist(StatsSnapshot s)
    {
        if (AutoCheckSkyQuestLoot(s)) _settings.Save();
    }

    private bool AutoCheckSkyQuestLoot(StatsSnapshot s)
    {
        if (s.Version == _skyAutoCheckVersion) return false;   // perf audit #13
        _skyAutoCheckVersion = s.Version;
        var changed = false;
        // The class-scoping rules live in Core (SkyLootAutoCheck) where they are
        // tested: shared items tick your selected classes / active tab (#98),
        // single-class items tick their class unconditionally (#106 — a Berserker
        // staff looted on the Druid tab is still Berserker progress).
        var myClasses = _w.QuestLedger?.ClassesFor(_w.QuestCharacterKey) ?? [];
        var lootByName = s.Loot
            .GroupBy(l => l.Item, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Count), StringComparer.OrdinalIgnoreCase);

        foreach (var key in _skyQuestLootSeen.Keys.ToList())
            if (!lootByName.ContainsKey(key))
                _skyQuestLootSeen[key] = 0;

        foreach (var (name, count) in lootByName)
        {
            _skyQuestLootSeen.TryGetValue(name, out var seen);
            _skyQuestLootSeen[name] = count;
            if (count <= seen) continue;
            changed |= SkyLootAutoCheck.Apply(_settings.SkyQuestChecklist, name,
                count - seen, myClasses, _settings.SkyQuestClass);
        }

        return changed;
    }

    internal void OnImportAchievements(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Pick the game's achievements dump ({EQBuddy.UI.Shared.GameCommands.OutputfileAchievements})",
            Filter = "Achievements dump (*.txt)|*.txt|All files (*.*)|*.*",
        };
        // /outputfile writes beside eqgame.exe — the Logs folder's parent.
        if (_settings.LogFolder is { Length: > 0 } lf
            && System.IO.Path.GetDirectoryName(System.IO.Path.TrimEndingDirectorySeparator(lf)) is { } root
            && System.IO.Directory.Exists(root))
            dlg.InitialDirectory = root;
        if (dlg.ShowDialog(_w) != true) return;
        try
        {
            var achievements = AchievementsImport.Parse(System.IO.File.ReadLines(dlg.FileName));
            var (matches, unmatched, autoGranted) =
                AchievementsImport.SkyRewards(achievements, _settings.SkyQuestChecklist);
            // The same dump carries the Conqueror sections — the Raids card's memory
            // of clears from before EQBuddy. Marking is add-only and idempotent, so
            // it needs no preview step of its own.
            _raidLedger().MarkAchievements(achievements);
            ShowAchievementsPreview(matches, unmatched, autoGranted, achievements.Count);
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            MessageBox.Show(_w, $"Couldn't read that file — {ex.Message}", "Import achievements");
        }
    }

    /// <summary>The import needs the dump to exist first, and the Raids card (which
    /// carries the ⧉ button) hides itself on a fresh character — so the menu that
    /// offers the import offers the command too (David, 2026-08-14). A closed menu
    /// can't flip to ✓; the header says exactly what the click does instead.</summary>
    internal void OnCopyAchievementsCommand(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(EQBuddy.UI.Shared.GameCommands.OutputfileAchievements); }
        catch { /* clipboard momentarily held by another app */ }
    }

    private void ShowAchievementsPreview(List<SkyRewardMatch> matches, List<string> unmatched,
        List<string> autoGranted, int total)
    {
        var win = new Window
        {
            Title = "Import achievements — preview",
            Width = 460, Height = 480, Owner = _w,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        win.SetResourceReference(Control.BackgroundProperty, "BgBrush");
        var panel = new StackPanel { Margin = new Thickness(10) };
        void Add(string text, string brush, bool bold = false)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 1),
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, brush);
            panel.Children.Add(tb);
        }

        var fresh = matches.Where(m =>
            !IsSkyRewardCompleted(m.ClassName, m.Reward)).ToList();
        Add($"{total} achievements read · {matches.Count} Sky rewards recognized", "TextBrush", bold: true);
        Add(fresh.Count > 0
            ? $"{fresh.Count} will be marked turned-in (the rest already are):"
            : "Everything recognized is already marked — nothing to apply.", "TextBrush");
        foreach (var m in matches)
        {
            var already = !fresh.Contains(m);
            Add($"  ✓ {m.ClassName} — {m.Reward}" + (already ? "   (already marked)" : ""),
                already ? "DimBrush" : "GoodBrush");
        }
        if (autoGranted.Count > 0)
        {
            Add($"Skipped — auto-granted, not earned ({autoGranted.Count}):", "WarnBrush", bold: true);
            Add("Your primary class unlock is granted at creation, and the game marks its " +
                "reward criteria complete without the items ever existing (#101) — so these " +
                "prove nothing and are never imported. Turn them in for real and the Sky " +
                "card tracks them the normal way.", "DimBrush");
            foreach (var g in autoGranted) Add($"  ⊘ {g}", "DimBrush");
        }
        if (unmatched.Count > 0)
        {
            Add($"Completed in the file but not recognized ({unmatched.Count}) — left untouched; " +
                "tell the discussions board and matching improves:", "WarnBrush", bold: true);
            foreach (var u in unmatched) Add($"  ? {u}", "DimBrush");
        }
        Add("Applying only ADDS: nothing currently tracked gets unchecked.", "DimBrush");

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10),
        };
        var apply = Theming.Button($"Apply ({fresh.Count})");
        apply.IsEnabled = fresh.Count > 0;
        apply.Click += (_, _) =>
        {
            AchievementsImport.Apply(matches, _settings);
            _settings.Save();
            win.Close();
        };
        var cancel = Theming.Button("Cancel");
        cancel.Margin = new Thickness(8, 0, 0, 0);
        cancel.Click += (_, _) => win.Close();
        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);

        var root = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        });
        win.Content = root;
        win.ShowDialog();
    }

    private static string SkyRewardKey(string className, string reward) => className + "|" + reward;

    /// <summary>Case-INSENSITIVELY, like every other reader of this list
    /// (QuestChecklistLayout.Sky and SkyCompleteToggle both use OrdinalIgnoreCase).
    /// A plain List.Contains is case-sensitive, so this one site disagreed with the
    /// rest — and a catalog correction that changes only capitalisation (#216, the
    /// Magister's capital T) would have stranded a turn-in here while the shared
    /// layout still found it. One fact, one comparison.</summary>
    private bool IsSkyRewardCompleted(string className, string reward) =>
        _settings.SkyQuestCompleted.Contains(SkyRewardKey(className, reward),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>Reward turned in (#73): completing checks the reward's items too —
    /// they were acquired and then handed over. Unchecking reopens the quest but
    /// leaves the item boxes as they were; the player knows what they still hold.</summary>
}
