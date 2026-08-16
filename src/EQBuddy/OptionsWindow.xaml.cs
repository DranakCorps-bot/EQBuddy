using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Thin WPF view over the shared OptionsViewModel (EQBuddy.UI.Shared) — all
/// mappings/mutations live there; this class builds controls, forwards input, and
/// applies the visual side effects (scale/opacity/layout) to the main window.
/// </summary>
public partial class OptionsWindow : Window
{
    private readonly MainWindow _main;
    private readonly OptionsViewModel _vm;
    private bool _ready;

    public OptionsWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        _vm = new OptionsViewModel(main.Settings, main.PersistSettings);
        Owner = main;
        Width = Math.Clamp(_vm.OptionsWidth, MinWidth, MaxWidth);
        // The handle only exists once the window is sourced; re-clamp on move because the
        // user may drag it to a monitor with a different size or DPI.
        WindowZoom.Attach(this, "options", _main.Settings);
        SourceInitialized += (_, _) => ClampToMonitor();
        LocationChanged += (_, _) => ClampToMonitor();

        foreach (var label in OptionsViewModel.ThemeLabels) ThemeCombo.Items.Add(label);
        ThemeCombo.SelectedIndex = _vm.ThemeIndex;

        ScaleSlider.Value = _vm.UiScale;
        ChipScaleSlider.Value = Math.Clamp(_vm.ChipScale, ChipScaleSlider.Minimum, ChipScaleSlider.Maximum);
        OpacitySlider.Value = _vm.Opacity;
        BgOpacitySlider.Value = _vm.BackgroundOpacity;
        TruncateCheck.IsChecked = _vm.TruncateLogs;
        ArchiveCheck.IsChecked = _vm.ArchiveLogs;
        PinChipsCheck.IsChecked = _vm.PinWatchChips;
        TutorialCheck.IsChecked = _vm.ShowTutorial;
        GridOverlayCheck.IsChecked = _main.Settings.ShowGridOverlay;
        GridSpacingSlider.Value = Math.Clamp(_main.Settings.GridSpacing,
            GridSpacingSlider.Minimum, GridSpacingSlider.Maximum);
        GridSpacingLabel.Text = $"{GridSpacingSlider.Value:0} px";
        TargetDropsCheck.IsChecked = _vm.ShowTargetDrops;
        HideUnfocusedCheck.IsChecked = _vm.HideWhenGameUnfocused;
        HideNotRunningCheck.IsChecked = _vm.HideWhenGameNotRunning;
        KeepAboveCheck.IsChecked = _vm.KeepAboveOverlays;
        SpawnGrowUpCheck.IsChecked = _vm.SpawnChipsGrowUp;
        MezGrowUpCheck.IsChecked = _vm.MezChipsGrowUp;
        MezChipsCheck.IsChecked = _main.Settings.MezChipsEnabled;
        DoubleClickChipsCheck.IsChecked = _main.Settings.DoubleClickChipsToggleBreakouts;
        SlowAlertCheck.IsChecked = _main.Settings.SlowAlertEnabled;
        SlowSpokenCheck.IsChecked = _main.Settings.SlowAlertSpoken;
        SlowRaidOnlyCheck.IsChecked = _main.Settings.SlowAlertRaidOnly;
        BuffExpiringOnlyCheck.IsChecked = _main.Settings.BuffTimersExpiringOnly;
        BuffWarnBox.Text = _main.Settings.BuffWarnSeconds.ToString("0");
        CursorRingCheck.IsChecked = _main.Settings.ShowCursorRing;
        PerfStatsCheck.IsChecked = _main.Settings.ShowPerfStats;
        SelectTab(_main.Settings.OptionsTab);
        BuildHotkeyRows();
        RegenPerTickBox.Text = _vm.RegenPerTickOverride > 0 ? _vm.RegenPerTickOverride.ToString() : "";
        TrackSpawnsCheck.IsChecked = _main.Settings.TrackSpawns;

        foreach (var choice in OptionsViewModel.WindowChoices) WindowCombo.Items.Add(choice);
        WindowCombo.SelectedIndex = _vm.RecentWindowIndex;

        foreach (var choice in OptionsViewModel.SoundChoices) SoundCombo.Items.Add(choice);
        SoundCombo.SelectedIndex = _vm.SoundIndex;
        AlertVolumeSlider.Value = Math.Clamp(_vm.Settings.AlertVolume, 0.1, 1.0);
        AlertVolumeLabel.Text = $"{AlertVolumeSlider.Value:P0}";
        UpdateSoundFileNote();

        // Enumerated once per Options open — voices install with language packs, not
        // mid-session, and the SAPI walk isn't free.
        _installedVoices = EQBuddy.UI.Shared.SpokenAlerts.InstalledVoiceNames();
        foreach (var choice in OptionsViewModel.VoiceChoices(_installedVoices)) VoiceCombo.Items.Add(choice);
        VoiceCombo.SelectedIndex = _vm.VoiceIndex(_installedVoices);
        SpeechRateSlider.Value = _vm.SpeechRate;
        SpeechRateLabel.Text = _vm.SpeechRateLabel;
        SpeechVolumeSlider.Value = _vm.SpeechVolume;
        SpeechVolumeLabel.Text = _vm.SpeechVolumeLabel;

        BuildRulesEditor();
        BuildCardsEditor();
        BuildBuffSetPanel();
        UpdateGearImportStatus();
        UpdateCustomColorsPanel();
        BuildBreakoutChecks();

        // Restore the examples panel without persisting — this isn't the user changing it.
        ApplyGuideOpen(_main.Settings.ShowWatchGuide, persist: false);

        UpdateLabels();
        _ready = true;

        // CenterOwner + SizeToContent positions before the size is known and can land
        // off-screen next to an edge-docked widget — place ourselves once measured:
        // beside the widget (left if room, else right), clamped to the work area.
        Loaded += (_, _) =>
        {
            var wa = SystemParameters.WorkArea;
            var left = _main.Left - ActualWidth - 12;
            if (left < wa.Left + 8) left = _main.Left + _main.ActualWidth + 12;
            Left = Math.Max(wa.Left + 8, Math.Min(left, wa.Right - ActualWidth - 8));
            Top = Math.Max(wa.Top + 8, Math.Min(_main.Top, wa.Bottom - ActualHeight - 8));
            Activate();
        };
    }

    private void UpdateLabels()
    {
        ScaleLabel.Text = _vm.ScaleLabel;
        ChipScaleLabel.Text = _vm.ChipScaleLabel;
        OpacityLabel.Text = _vm.OpacityLabel;
        BgOpacityLabel.Text = _vm.BackgroundOpacityLabel;
    }

    private void OnChipScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _vm.ChipScale = ChipScaleSlider.Value;
        _main.SetChipScale(_vm.ChipScale);
        UpdateLabels();
    }

    private void OnScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _vm.UiScale = ScaleSlider.Value;
        _main.SetUiScale(_vm.UiScale);
        UpdateLabels();
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _vm.Opacity = OpacitySlider.Value;
        _main.SetWindowOpacity(_vm.Opacity);
        UpdateLabels();
    }

    private void OnBgOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _vm.BackgroundOpacity = BgOpacitySlider.Value;
        _main.SetBackgroundOpacity(_vm.BackgroundOpacity);
        UpdateLabels();
    }

    private void OnTruncateChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.TruncateLogs = TruncateCheck.IsChecked == true;
    }

    private void OnArchiveChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.ArchiveLogs = ArchiveCheck.IsChecked == true;
    }

    private void OnTutorialToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.ShowTutorial = TutorialCheck.IsChecked == true;
    }

    private void OnTargetDropsToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.ShowTargetDrops = TargetDropsCheck.IsChecked == true;
    }

    private void OnGridOverlayToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _main.SetGridOverlay(GridOverlayCheck.IsChecked == true);
    }

    private void OnGridSpacingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _main.Settings.GridSpacing = GridSpacingSlider.Value;
        GridSpacingLabel.Text = $"{GridSpacingSlider.Value:0} px";
        _vm.Persist();
        _main.RefreshGridSpacing();   // live while the grid is up
    }

    private void OnKeepAboveToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.KeepAboveOverlays = KeepAboveCheck.IsChecked == true;
    }

    private void OnMezChipsToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _main.Settings.MezChipsEnabled = MezChipsCheck.IsChecked == true;
        _main.Settings.Save();
    }

    private void OnDoubleClickChipsToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _main.Settings.DoubleClickChipsToggleBreakouts = DoubleClickChipsCheck.IsChecked == true;
        _main.Settings.Save();
    }

    private void OnSlowAlertToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _main.Settings.SlowAlertEnabled = SlowAlertCheck.IsChecked == true;
        _main.Settings.SlowAlertSpoken = SlowSpokenCheck.IsChecked == true;
        _main.Settings.SlowAlertRaidOnly = SlowRaidOnlyCheck.IsChecked == true;
        _main.Settings.Save();
    }

    private void OnCursorRingToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _main.SetCursorRing(CursorRingCheck.IsChecked == true);
    }

    private void OnPerfStatsToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _main.Settings.ShowPerfStats = PerfStatsCheck.IsChecked == true;
        _main.Settings.Save();
    }

    private void OnBuffDisplayChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _main.Settings.BuffTimersExpiringOnly = BuffExpiringOnlyCheck.IsChecked == true;
        if (double.TryParse(BuffWarnBox.Text, out var seconds))
            _main.Settings.BuffWarnSeconds = Math.Clamp(seconds, 10, 3600);
        BuffWarnBox.Text = _main.Settings.BuffWarnSeconds.ToString("0");   // shows any clamp
        _main.Settings.Save();
    }

    // ---- buff set (#120, Frankthetankk — the missing line's editor) ----
    // Stage 2: the set lives PER CLASS in Settings.BuffSetsByClass (BuffSetStore owns
    // the shape) and assembles from the active class combination plus "(any class)".
    // This editor shows every bucket — active or parked — because it is the one place
    // a stored pick can always be removed. Every edit routes through
    // MainWindow.OnBuffSetEdited: card, breakout window and this panel repaint at
    // once — an edit whose effect waits for the next tick reads as a silent no-op.

    /// <summary>The breakout editor writes the same storage; MainWindow calls this so
    /// its edits appear here immediately too.</summary>
    internal void RefreshBuffSetEditor() => BuildBuffSetPanel();

    private void BuildBuffSetPanel()
    {
        var key = _main.BuffSetKey;
        var (classes, picked) = _main.BuffSetClassSource(_main.CurrentSnapshot());
        BuffSetCharNote.Text = key.Length > 0
            ? $"Saved for {_main.BuffSetCharacterName}, per class — the live set is "
              + "(any class) plus "
              + (classes.Count > 0
                  ? $"{string.Join(", ", classes.Select(QuestClassFilter.Abbrev))} "
                    + (picked
                        ? "(picked in the Quest Tracker)."
                        : "(inferred from your combat log — pick classes in the Quest Tracker to override).")
                  : "your classes — none known yet: pick them in the Quest Tracker, or use (any class).")
            : "No character detected yet — once today's log names one, reopen Options and the editor unlocks.";
        BuffSetAddBox.IsEnabled = key.Length > 0;
        BuffSetClassBox.IsEnabled = key.Length > 0;
        RefreshBuffSetClassChoices();
        BuffSetPanel.Children.Clear();
        if (key.Length == 0) return;

        var stored = _main.Settings.BuffSetsByClass.GetValueOrDefault(key);
        // Active buckets first, in assembly order; then parked ones (stored picks
        // whose class isn't in the current combination) — visible and editable, so a
        // swap never strands a pick out of reach. That parked picks SURVIVE the swap
        // is the requester's whole design.
        // Shared with the breakout (BuffSetStore.EditableSections) so the two editors
        // cannot disagree about which buckets are visible — they did, and a pick added
        // to a parked class vanished from the breakout entirely (#120, Frankthetankk).
        // Options additionally hides EMPTY active sections; the breakout keeps them,
        // because there they are the place you add.
        var sections = BuffSetStore.EditableSections(stored, classes)
            .Where(r => r.Section.Spells.Count > 0)
            .Select(r => (r.Section.Class, Spells: r.Section.Spells, r.Parked))
            .ToList();
        if (sections.Count == 0)
        {
            var none = new TextBlock
            {
                Text = "Nothing picked yet — pick a class bucket and search below to build the set.",
                FontSize = 11, Margin = new Thickness(0, 2, 0, 0),
            };
            none.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            BuffSetPanel.Children.Add(none);
            return;
        }
        foreach (var (cls, spells, parked) in sections)
        {
            var header = new TextBlock
            {
                Text = cls + (parked ? "  · not in your current classes — kept for the swap back" : ""),
                FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0),
            };
            header.SetResourceReference(TextBlock.ForegroundProperty, parked ? "DimBrush" : "AccentBrush");
            BuffSetPanel.Children.Add(header);
            foreach (var spell in spells)
            {
                var row = new Grid { Margin = new Thickness(6, 2, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = new TextBlock
                {
                    Text = spell, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                label.SetResourceReference(TextBlock.ForegroundProperty, parked ? "DimBrush" : "TextBrush");
                row.Children.Add(label);
                var remove = new Button
                {
                    Style = (Style)FindResource("IconButton"), Content = "✕", FontSize = 11,
                    Margin = new Thickness(4, 0, 0, 0), ToolTip = $"Remove {spell} from {cls}",
                };
                var (doomedClass, doomed) = (cls, spell);
                remove.Click += (_, _) =>
                {
                    BuffSetStore.Remove(_main.Settings.BuffSetsByClass, key, doomedClass, doomed);
                    _main.Settings.Save();
                    _main.OnBuffSetEdited();   // repaints card + breakout + this panel
                };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                BuffSetPanel.Children.Add(row);
            }
        }
    }

    /// <summary>Add-target buckets: "(any class)" plus the FULL class list — unlike
    /// the breakout's active-only list, so a coming swap can be configured here in
    /// advance. Selection survives rebuilds.</summary>
    private void RefreshBuffSetClassChoices()
    {
        var keep = BuffSetClassBox.SelectedItem as string;
        if (BuffSetClassBox.Items.Count == 0)
        {
            BuffSetClassBox.Items.Add(BuffSetStore.AnyClass);
            foreach (var cls in QuestClassFilter.Classes) BuffSetClassBox.Items.Add(cls);
        }
        BuffSetClassBox.SelectedItem = keep ?? BuffSetStore.AnyClass;
    }

    private string SelectedBuffSetClass =>
        BuffSetClassBox.SelectedItem as string ?? BuffSetStore.AnyClass;

    private void OnBuffSetSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        var query = BuffSetAddBox.Text.Trim();
        if (query.Length < 2) { BuffSetPopup.IsOpen = false; return; }
        BuffSetChrome.SetResourceReference(Border.BackgroundProperty, "PopupBrush");
        BuffSetChrome.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
        BuffSetMatches.SetResourceReference(Control.BackgroundProperty, "PopupBrush");
        BuffSetMatches.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        BuffSetMatches.Items.Clear();

        // Seen first (the buffs this player demonstrably casts), then the whole buff
        // catalog — BuffSetSearch, shared with the breakout editor. Both draw from
        // BuffDurationCatalog's attributable spells, so nothing can be added that
        // would sit at "not seen" forever. Only the TARGET bucket's picks are
        // excluded: the same buff under another class is a legitimate pick.
        var inBucket = BuffSetStore.SpellsFor(
            _main.Settings.BuffSetsByClass.GetValueOrDefault(_main.BuffSetKey), SelectedBuffSetClass);
        foreach (var (s, seen) in BuffSetSearch.Rank(query, _main.SeenBuffCasts(),
                     inBucket, BuffDurationCatalog.Default.SpellNames))
            BuffSetMatches.Items.Add(new ListBoxItem
            {
                Content = seen ? s + "   · seen this session" : s,
                Tag = s,
            });
        if (BuffSetMatches.Items.Count == 0)
            BuffSetMatches.Items.Add(new ListBoxItem
            {
                Content = "No buff in the catalog matches — check the spelling?",
                IsEnabled = false,
            });
        BuffSetPopup.IsOpen = true;
    }

    private void OnBuffSetMatchPicked(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || BuffSetMatches.SelectedItem is not ListBoxItem { Tag: string spell }) return;
        BuffSetPopup.IsOpen = false;
        BuffSetMatches.SelectedItem = null;
        var key = _main.BuffSetKey;
        if (key.Length == 0) return;
        BuffSetStore.Add(_main.Settings.BuffSetsByClass, key, SelectedBuffSetClass, spell);
        _main.Settings.Save();
        BuffSetAddBox.Text = "";   // TextChanged with an empty box closes the popup
        _main.OnBuffSetEdited();   // repaints card + breakout + this panel
    }

    // ---- tabs (1.67.0, David: "a wall of options... needs serious reorganization") ----

    private void OnTabClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBlock { Tag: string tab }) return;
        SelectTab(tab);
        if (_ready) { _main.Settings.OptionsTab = tab; _main.Settings.Save(); }
    }

    private void SelectTab(string tab)
    {
        var panels = new (string Key, System.Windows.Controls.TextBlock Link, UIElement Panel)[]
        {
            ("look", TabLook, TabLookPanel), ("alerts", TabAlerts, TabAlertsPanel),
            ("watch", TabWatch, TabWatchPanel), ("cards", TabCards, TabCardsPanel),
            ("behavior", TabBehavior, TabBehaviorPanel),
        };
        if (panels.All(p => p.Key != tab)) tab = "look";   // stale setting → home
        foreach (var (key, link, panel) in panels)
        {
            var active = key == tab;
            panel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            link.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            link.TextDecorations = active ? TextDecorations.Underline : null;
            link.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
                active ? "AccentBrush" : "DimBrush");
        }
    }

    private void OnChipGrowToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _vm.SpawnChipsGrowUp = SpawnGrowUpCheck.IsChecked == true;
        _vm.MezChipsGrowUp = MezGrowUpCheck.IsChecked == true;
    }

    // ---- global hotkeys, opt-in only (#100 — see HotkeyManager) ----

    private string? _recordingAction;

    private void BuildHotkeyRows()
    {
        HotkeysPanel.Children.Clear();
        foreach (var (key, label) in HotkeyManager.Actions)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            name.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            row.Children.Add(name);

            var bound = _main.Settings.Hotkeys.GetValueOrDefault(key, "");
            var recorder = new Button
            {
                Style = (Style)FindResource("ActionButton"), FontSize = 11,
                // The recording prompt names the rule instead of hiding it: a bare key
                // is rejected on purpose (a global "G" would eat chat typing), and the
                // 1.66 field test proved silent rejection reads as a dead recorder.
                Content = _recordingAction == key
                    ? _recordingHint ?? "press Ctrl/Alt/Shift + a key…"
                    : bound.Length > 0 ? bound : "not bound — click to set",
                Tag = key,
            };
            recorder.Click += (_, _) =>
            {
                _recordingAction = _recordingAction == key ? null : key;
                _recordingHint = null;
                BuildHotkeyRows();
            };
            Grid.SetColumn(recorder, 1);
            row.Children.Add(recorder);

            var clear = new Button
            {
                Style = (Style)FindResource("IconButton"), Content = "✕", FontSize = 11,
                Margin = new Thickness(4, 0, 0, 0), ToolTip = "Unbind",
                Visibility = bound.Length > 0 ? Visibility.Visible : Visibility.Hidden,
            };
            clear.Click += (_, _) =>
            {
                _main.Settings.Hotkeys.Remove(key);
                _main.Settings.Save();
                _main.ApplyHotkeys();
                _recordingAction = null;
                BuildHotkeyRows();
            };
            Grid.SetColumn(clear, 2);
            row.Children.Add(clear);
            HotkeysPanel.Children.Add(row);
        }
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (_recordingAction is not { } action) { base.OnPreviewKeyDown(e); return; }
        e.Handled = true;
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (key == System.Windows.Input.Key.Escape) { _recordingAction = null; BuildHotkeyRows(); return; }
        // A bare modifier press isn't a gesture yet — wait for the real key.
        if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
            or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt
            or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
            or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin) return;
        var mods = System.Windows.Input.Keyboard.Modifiers;
        var parts = new List<string>();
        if (mods.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(System.Windows.Input.ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(System.Windows.Input.ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(System.Windows.Input.ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        var gesture = string.Join("+", parts);
        // Modifier required — a bare global letter would eat the game's chat typing.
        // Say so on the button itself: a silent return looks like a dead recorder.
        if (HotkeyManager.Parse(gesture) is null)
        {
            _recordingHint = $"{key} alone won't do — add Ctrl, Alt or Shift";
            BuildHotkeyRows();
            return;
        }
        _main.Settings.Hotkeys[action] = gesture;
        _main.Settings.Save();
        _main.ApplyHotkeys();
        _recordingAction = null;
        _recordingHint = null;
        BuildHotkeyRows();
    }

    /// <summary>Transient message shown in the recording button after a rejected press.</summary>
    private string? _recordingHint;

    private void OnHideUnfocusedToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.HideWhenGameUnfocused = HideUnfocusedCheck.IsChecked == true;
    }

    private void OnHideNotRunningToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.HideWhenGameNotRunning = HideNotRunningCheck.IsChecked == true;
    }

    /// <summary>One checkbox per breakout kind — the re-enable path for a window that was
    /// ✕-closed (discussion #45: the star should keep its chip without forcing the
    /// window).</summary>
    private void BuildBreakoutChecks()
    {
        BreakoutsPanel.Children.Clear();
        foreach (var kind in Enum.GetValues<BreakoutKind>())
        {
            var name = kind.ToString();
            var check = new System.Windows.Controls.CheckBox
            {
                IsChecked = !_main.Settings.DisabledBreakouts.Contains(name),
                Margin = new Thickness(0, 2, 14, 0),
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = kind switch
                    {
                        BreakoutKind.Damage => "⚔ Damage",
                        BreakoutKind.Healing => "⚕ Healing",
                        BreakoutKind.Pet => "🐾 Pet",
                        BreakoutKind.Watch => "🎯 Watch",
                        BreakoutKind.Buffs => "⏳ Buff set",
                        _ => "🎒 Loot",
                    },
                    FontSize = 12,
                },
            };
            ((System.Windows.Controls.TextBlock)check.Content).SetResourceReference(
                System.Windows.Controls.TextBlock.ForegroundProperty, "TextBrush");
            check.Checked += (_, _) => SetBreakout(name, enabled: true);
            check.Unchecked += (_, _) => SetBreakout(name, enabled: false);
            BreakoutsPanel.Children.Add(check);
        }

        void SetBreakout(string name, bool enabled)
        {
            if (!_ready) return;
            if (enabled) _main.Settings.DisabledBreakouts.Remove(name);
            else if (!_main.Settings.DisabledBreakouts.Contains(name))
                _main.Settings.DisabledBreakouts.Add(name);
            _vm.Persist();
        }
    }

    private void OnRegenPerTickChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        // Blank or unparseable = back to the wiki base; the box shows any clamp.
        _vm.RegenPerTickOverride = int.TryParse(RegenPerTickBox.Text.Trim(), out var v) ? v : 0;
        RegenPerTickBox.Text = _vm.RegenPerTickOverride > 0 ? _vm.RegenPerTickOverride.ToString() : "";
    }

    /// <summary>Called back by MainWindow.SetTrackSpawns so closing the Spawns window
    /// (or toggling the menu) updates this checkbox while Options sits open.</summary>
    internal void SyncTrackSpawns(bool on)
    {
        var wasReady = _ready;
        _ready = false;
        TrackSpawnsCheck.IsChecked = on;
        _ready = wasReady;
    }

    private void OnTrackSpawnsToggled(object sender, RoutedEventArgs e)
    {
        // Routed through MainWindow, not the view model: the setting, the right-click
        // menu check, and the window itself all have to move together.
        if (_ready) _main.SetTrackSpawns(TrackSpawnsCheck.IsChecked == true);
    }

    private void OnPinChipsChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.PinWatchChips = PinChipsCheck.IsChecked == true;
    }

    /// <summary>Show or hide the worked examples, remembering the choice. Content is built on
    /// first expand rather than at construction — most people never open it.</summary>
    private void OnGuideToggled(object sender, RoutedEventArgs e) =>
        ApplyGuideOpen(GuidePanel.Visibility != Visibility.Visible, persist: true);

    private void ApplyGuideOpen(bool open, bool persist)
    {
        GuideToggle.Content = open ? "▾ Hide examples" : "▸ Show examples";
        GuidePanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open && GuideContent.Children.Count == 0) BuildGuide();
        if (persist)
        {
            _main.Settings.ShowWatchGuide = open;
            _vm.Persist();
        }
    }

    private void BuildGuide()
    {
        System.Windows.Controls.TextBlock Line(
            string text, double size, System.Windows.Media.Brush brush, double top, bool bold = false) => new()
        {
            Text = text, FontSize = size, Foreground = brush,
            TextWrapping = System.Windows.TextWrapping.Wrap,
            Margin = new Thickness(0, top, 0, 0),
            FontWeight = bold ? System.Windows.FontWeights.SemiBold : System.Windows.FontWeights.Normal,
        };

        var accent = (System.Windows.Media.Brush)FindResource("AccentBrush");
        var text = (System.Windows.Media.Brush)FindResource("TextBrush");
        var dim = (System.Windows.Media.Brush)FindResource("DimBrush");

        GuideContent.Children.Add(Line("How matching works", 11, accent, 0, bold: true));
        foreach (var basic in WatchGuide.Basics)
            GuideContent.Children.Add(Line("• " + basic, 11, dim, 2));

        GuideContent.Children.Add(Line("Examples", 11, accent, 8, bold: true));
        foreach (var ex in WatchGuide.Examples)
        {
            // Kind · name · what to type, then what it gets you. Two lines per example reads
            // better than a table in a panel this narrow.
            var match = ex.Match.Length > 0 ? $"Match \"{ex.Match}\"" : "no match text";
            var delay = ex.Delay.Length > 0 ? $" · Delay {ex.Delay}" : "";
            GuideContent.Children.Add(Line(
                $"{OptionsViewModel.KindNames[(int)ex.Kind]} · \"{ex.Name}\" · {match}{delay}",
                11, text, 8));
            GuideContent.Children.Add(Line(ex.What, 11, dim, 1));
        }
    }

    private void OnWindowChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_ready) _vm.RecentWindowIndex = WindowCombo.SelectedIndex;
    }

    private void OnThemeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _vm.ThemeIndex = ThemeCombo.SelectedIndex;
        ThemeManager.Apply(_vm.Settings);
        // The card rows pick Foreground (dim vs. normal) via FindResource at construction
        // time rather than a binding, so they need an explicit rebuild to pick up the new
        // palette — everything else in the window repaints on its own via DynamicResource.
        BuildCardsEditor();
        UpdateCustomColorsPanel();
        _main.RefreshTheme();
    }

    /// <summary>Preset swatches for the Custom theme rows: the built-in themes'
    /// backgrounds and accents plus a few brights — hex entry covers everything else.</summary>
    private static readonly string[] SwatchColors =
    [
        "#000000", "#1A1A1A", "#20242B", "#26211A", "#002B36", "#FDF6E3", "#FFFFFF",
        "#EAEAEA", "#E3B341", "#FFD24D", "#5FA8D3", "#3FCFBE", "#7FBF5F", "#E0654A",
        "#C080D0", "#9C9C9C",
    ];

    private void UpdateCustomColorsPanel()
    {
        var custom = _vm.Settings.Theme == CustomTheme.Key;
        CustomColorsPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        if (!custom) return;
        CustomColorsPanel.Children.Clear();
        CustomColorsPanel.Children.Add(ColorRow("Background",
            _vm.Settings.CustomThemeBg ?? CustomTheme.DefaultBg, v => _vm.Settings.CustomThemeBg = v));
        CustomColorsPanel.Children.Add(ColorRow("Text",
            _vm.Settings.CustomThemeText ?? CustomTheme.DefaultText, v => _vm.Settings.CustomThemeText = v));
        CustomColorsPanel.Children.Add(ColorRow("Accent",
            _vm.Settings.CustomThemeAccent ?? CustomTheme.DefaultAccent, v => _vm.Settings.CustomThemeAccent = v));
    }

    private System.Windows.Controls.DockPanel ColorRow(string label, string current, Action<string> store)
    {
        var row = new System.Windows.Controls.DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var name = new System.Windows.Controls.TextBlock
        { Text = label, FontSize = 11, Width = 72, VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Controls.DockPanel.SetDock(name, System.Windows.Controls.Dock.Left);
        row.Children.Add(name);

        var hexBox = new System.Windows.Controls.TextBox
        { Text = current, FontSize = 11, Width = 64, VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Controls.DockPanel.SetDock(hexBox, System.Windows.Controls.Dock.Right);

        void Commit(string value)
        {
            // Invalid hex is simply not committed — the palette keeps its last good color.
            if (CustomTheme.Valid(value) is not { } hex) { hexBox.Text = current; return; }
            current = hex;
            store(hex);
            _main.PersistSettings();
            hexBox.Text = hex;
            ThemeManager.Apply(_vm.Settings);
            BuildCardsEditor();
            _main.RefreshTheme();
        }

        hexBox.LostFocus += (_, _) => Commit(hexBox.Text);
        hexBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(hexBox.Text); };
        row.Children.Add(hexBox);

        var swatches = new System.Windows.Controls.WrapPanel
        { Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        foreach (var hex in SwatchColors)
        {
            var swatch = new System.Windows.Controls.Border
            {
                Width = 14,
                Height = 14,
                Margin = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                BorderThickness = new Thickness(1),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!),
                Cursor = Cursors.Hand,
                ToolTip = hex,
            };
            swatch.MouseLeftButtonUp += (_, _) => Commit(hex);
            swatches.Children.Add(swatch);
        }
        row.Children.Add(swatches);
        return row;
    }

    private void OnSoundChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (!_vm.IsCustomSoundIndex(SoundCombo.SelectedIndex))
        {
            _vm.SelectNamedSound(SoundCombo.SelectedIndex);
        }
        else
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose an alert sound",
                Filter = "Sound files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*",
            };
            if (dlg.ShowDialog(this) == true)
                _vm.SetCustomSound(dlg.FileName);
            else if (!_vm.IsCustomSoundIndex(_vm.SoundIndex))
            {
                _ready = false; SoundCombo.SelectedIndex = _vm.SoundIndex; _ready = true;   // cancelled — revert
            }
        }
        UpdateSoundFileNote();
        _main.PlayAlertSound();   // instant feedback on the new choice
    }

    private void OnSoundTest(object sender, RoutedEventArgs e) => _main.PlayAlertSound();

    private IReadOnlyList<string> _installedVoices = [];

    private void OnVoiceChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ready || VoiceCombo.SelectedIndex < 0) return;
        _vm.SelectVoice(_installedVoices, VoiceCombo.SelectedIndex);
        SpeakSample();   // a voice choice you can hear, like the sound picker's instant play
    }

    private void OnVoiceTest(object sender, RoutedEventArgs e) => SpeakSample();

    // Real alert text, × and all, so the sample demonstrates exactly what an alert
    // will sound like (SpokenAlerts.Speakable rewrites the × for the voice).
    private static void SpeakSample() =>
        EQBuddy.UI.Shared.SpokenAlerts.SpeakSample("Rusty Sword ×3");

    private void OnSpeechRateChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _vm.SpeechRate = (int)Math.Round(SpeechRateSlider.Value);
        SpeechRateLabel.Text = _vm.SpeechRateLabel;
    }

    private void OnSpeechVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _vm.SpeechVolume = (int)Math.Round(SpeechVolumeSlider.Value);
        SpeechVolumeLabel.Text = _vm.SpeechVolumeLabel;
    }

    private void OnAlertVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _vm.Settings.AlertVolume = AlertVolumeSlider.Value;
        _main.PersistSettings();
        AlertVolumeLabel.Text = $"{AlertVolumeSlider.Value:P0}";
    }

    private void UpdateSoundFileNote()
    {
        SoundFileNote.Text = _vm.SoundFileNote;
        SoundFileNote.Visibility = _vm.SoundFileNote.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // Resize state captured at drag start. Deriving each frame from the cursor's absolute
    // position rather than accumulating DragDelta avoids the feedback jitter you get when
    // the thumb moves with the window (which the left grip does).
    private double _dragCursorX, _dragLeft, _dragWidth;

    private void OnResizeStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _dragCursorX = CursorX();
        _dragLeft = Left;
        _dragWidth = Width;
    }

    private void OnResizeRightDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e) =>
        Width = Math.Clamp(_dragWidth + (CursorX() - _dragCursorX), MinWidth, MaxWidth);

    /// <summary>Left edge: grow leftwards, keeping the right edge where it is.</summary>
    private void OnResizeLeftDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        var width = Math.Clamp(_dragWidth - (CursorX() - _dragCursorX), MinWidth, MaxWidth);
        Left = _dragLeft + (_dragWidth - width);
        Width = width;
    }

    private void OnResizeCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
        _vm.OptionsWidth = Width;

    /// <summary>Cursor X in device-independent units (the space Left/Width live in).</summary>
    private double CursorX()
    {
        Native.GetCursorPos(out var p);
        return p.X * DipScale().X;
    }

    private (double X, double Y) DipScale()
    {
        var m = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
        return m is { } t ? (t.M11, t.M22) : (1.0, 1.0);
    }

    /// <summary>
    /// Cap the window to the work area of whichever monitor it is on. At high Windows
    /// scaling (a tester runs 300%) the full options panel is taller than the screen, so
    /// without this the bottom is simply unreachable — the ScrollViewer only helps once
    /// the window itself is bounded. Recomputed on move because monitors differ in both
    /// size and DPI.
    /// </summary>
    private void ClampToMonitor()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var monitor = Native.MonitorFromWindow(hwnd, Native.MonitorDefaultToNearest);
        var info = new Native.MonitorInfo { cbSize = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(monitor, ref info)) return;

        var scale = DipScale();
        var workHeight = (info.rcWork.bottom - info.rcWork.top) * scale.Y;
        var workWidth = (info.rcWork.right - info.rcWork.left) * scale.X;
        // Leave a little breathing room so the rounded border isn't flush to the edge.
        MaxHeight = Math.Max(MinHeight + 1, workHeight - 24);
        MaxWidth = Math.Max(MinWidth + 1, Math.Min(900, workWidth - 24));
        if (Width > MaxWidth) Width = MaxWidth;
    }

    private static class Native
    {
        public const uint MonitorDefaultToNearest = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MonitorInfo
        {
            public int cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point { public int X, Y; }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    }

    private void OnAddRule(object sender, RoutedEventArgs e)
    {
        _vm.AddRule();
        BuildRulesEditor();
    }

    /// <summary>"That thing that just happened — alert on it." The picker lists the
    /// last few log lines; clicking one mints a Text rule with the line as its match,
    /// ready to trim. Nobody should have to remember a log line to watch for it
    /// (Companion-parity idea, our picker).</summary>
    private void OnAddRuleFromLog(object sender, RoutedEventArgs e)
    {
        RecentLinesChrome.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "PopupBrush");
        RecentLinesChrome.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "AccentBrush");
        RecentLinesList.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "PopupBrush");
        RecentLinesList.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "TextBrush");

        RecentLinesHideChat.IsChecked = _main.Settings.RecentLinesHideChat;
        FillRecentLines();
        RecentLinesPopup.IsOpen = true;
    }

    private void FillRecentLines()
    {
        var lines = _main.RecentLogLines();
        RecentLinesList.Items.Clear();
        var hideChat = RecentLinesHideChat.IsChecked == true;
        // Newest first: "just happened" is the whole point of the picker.
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var (time, message) = lines[i];
            // Chat lines quote their body (", '") — a busy General channel drowns the
            // combat lines the picker exists for (David's field note), but chat stays
            // one untick away: a "WTS" watch is a legitimate rule too.
            if (hideChat && message.Contains(", '", StringComparison.Ordinal)) continue;
            RecentLinesList.Items.Add(new System.Windows.Controls.ListBoxItem
            {
                Content = $"{time:HH:mm:ss}  {(message.Length <= 96 ? message : message[..95] + "…")}",
                Tag = message,
                ToolTip = message,
            });
        }
        if (RecentLinesList.Items.Count == 0)
            RecentLinesList.Items.Add(new System.Windows.Controls.ListBoxItem
            {
                Content = hideChat ? "Nothing but chat seen lately — untick the filter."
                    : "No log lines seen yet — play a little first.",
                IsEnabled = false,
            });
    }

    private void OnRecentLinesFilterChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _main.Settings.RecentLinesHideChat = RecentLinesHideChat.IsChecked == true;
        _main.Settings.Save();
        FillRecentLines();
    }

    private void OnRecentLinePicked(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ready || RecentLinesList.SelectedItem is not System.Windows.Controls.ListBoxItem
            { Tag: string message }) return;
        RecentLinesPopup.IsOpen = false;
        RecentLinesList.SelectedItem = null;
        var rule = _vm.AddRule();
        rule.Kind = EQBuddy.Core.WatchKind.Text;
        rule.Pattern = message;
        rule.Name = message.Length <= 28 ? message : message[..27] + "…";
        _vm.Persist();
        BuildRulesEditor();
    }

    /// <summary>
    /// Column layout for both the header and every rule row. Auto columns are matched by
    /// SharedSizeGroup (the panel is a shared-size scope) so the header labels stay lined
    /// up with the controls no matter how wide the combo boxes render.
    /// </summary>
    private static System.Windows.Controls.Grid RuleGrid()
    {
        var grid = new System.Windows.Controls.Grid();
        void Auto(string group) => grid.ColumnDefinitions.Add(
            new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = group });
        void Star(double w) => grid.ColumnDefinitions.Add(
            new System.Windows.Controls.ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });

        // Kind and name were fixed at 58/60 px, which clipped their content even before
        // the spell-class picker existed. Name and match text share the free width, so
        // widening the window grows the fields that actually hold free text.
        Auto("RuleKind");
        Star(1);
        Star(1.4);
        Auto("RulePin");
        Auto("RuleBanner");
        Auto("RuleColor");
        Auto("RuleSpeech");
        Auto("RulePhrase");
        Auto("RuleSound");
        Auto("RuleDelay");
        Auto("RuleShare");
        Auto("RuleDelete");
        Auto("RuleArrange");
        return grid;
    }

    private void BuildRulesEditor()
    {
        RulesPanel.Children.Clear();

        var header = RuleGrid();
        header.Margin = new Thickness(0, 2, 0, 2);
        var headings = new[] { ("Watch", 0), ("Name", 1), ("Match", 2), ("Delay", 9) };
        foreach (var (text, column) in headings)
        {
            var label = new System.Windows.Controls.TextBlock
            {
                Text = text,
                FontSize = 10,
                Opacity = 0.7,
                Margin = new Thickness(column == 0 ? 0 : 6, 0, 0, 0),
            };
            System.Windows.Controls.Grid.SetColumn(label, column);
            header.Children.Add(label);
        }
        RulesPanel.Children.Add(header);

        foreach (var rule in _vm.Rules)
        {
            var row = RuleGrid();
            row.Margin = new Thickness(0, 3, 0, 0);

            var kind = new System.Windows.Controls.ComboBox { FontSize = 11, ToolTip = "What this rule watches" };
            foreach (var k in OptionsViewModel.KindNames) kind.Items.Add(k);
            kind.SelectedIndex = (int)rule.Kind;
            row.Children.Add(kind);

            var name = DarkBox(rule.Name, "name");
            name.Margin = new Thickness(4, 0, 0, 0);
            name.LostFocus += (_, _) => { rule.Name = name.Text.Trim(); _vm.Persist(); };
            System.Windows.Controls.Grid.SetColumn(name, 1);
            row.Children.Add(name);

            // Column 2 holds the match text, preceded (for Spell fade rules) by a class
            // picker: one named spell, or a whole class that keeps working as the
            // character levels into new spells and ranks.
            var matchArea = new System.Windows.Controls.Grid();
            matchArea.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            matchArea.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            System.Windows.Controls.Grid.SetColumn(matchArea, 2);
            row.Children.Add(matchArea);

            var spellFilter = new System.Windows.Controls.ComboBox
            {
                FontSize = 11,
                MinWidth = 104,
                Margin = new Thickness(4, 0, 0, 0),
                // chaosrah (Reddit): the unlabeled dropdown read as a mystery — say
                // plainly that it's the spell CLASS and that it replaces match text.
                ToolTip = "Spell class: watch one named spell (\"By name\" + match text), " +
                    "or a whole class — Charm, Mez, HoT… — with no match text needed",
            };
            foreach (var f in OptionsViewModel.SpellFilterNames) spellFilter.Items.Add(f);
            spellFilter.SelectedIndex = (int)rule.SpellFilter;
            matchArea.Children.Add(spellFilter);

            const string patternTip = "match text (uses the name if left empty; optional for Death/Milestone)";
            var pattern = DarkBox(rule.Pattern, patternTip);
            pattern.Margin = new Thickness(4, 0, 0, 0);
            System.Windows.Controls.Grid.SetColumn(pattern, 1);
            matchArea.Children.Add(pattern);

            // Regex mode (#83, KentCarmine): the same Match box, upgraded. Invalid
            // patterns match nothing and say why on the box's own tooltip.
            void ShowRegexState() => pattern.ToolTip =
                rule.RegexError is { } err ? $"Regex error: {err}" : patternTip;
            matchArea.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            var regexToggle = RuleToggle(".*",
                "Treat Match as a regular expression (.NET syntax, case-insensitive). " +
                "An invalid pattern matches nothing — the Match box's tooltip shows the error.",
                2, rule.UseRegex, v => { rule.UseRegex = v; ShowRegexState(); });
            matchArea.Children.Add(regexToggle);
            pattern.LostFocus += (_, _) => { rule.Pattern = pattern.Text.Trim(); ShowRegexState(); _vm.Persist(); };
            ShowRegexState();

            var buffChoices = EQBuddy.Core.FadeMessageCatalog.Default.BuffSpellChoices.ToArray();
            var spellName = DarkBox(rule.Pattern,
                "Start typing a known buff/spell fade, then pick one. Free typing still works.");
            spellName.Margin = new Thickness(4, 0, 0, 0);
            var spellMatches = new System.Windows.Controls.ListBox
            {
                MaxHeight = 260,
                MinWidth = 220,
                FontSize = 12,
            };
            spellMatches.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "PopupBrush");
            spellMatches.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "TextBrush");
            var spellPopup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = spellName,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = new System.Windows.Controls.Border
                {
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(2),
                    Child = spellMatches,
                },
            };
            ((System.Windows.Controls.Border)spellPopup.Child).SetResourceReference(
                System.Windows.Controls.Control.BorderBrushProperty, "AccentBrush");
            ((System.Windows.Controls.Border)spellPopup.Child).SetResourceReference(
                System.Windows.Controls.Control.BackgroundProperty, "PopupBrush");
            matchArea.Children.Add(spellPopup);

            var choosingSpell = false;
            void UpdateSpellChoices(string text, bool open)
            {
                var q = (text ?? "").Trim();
                spellMatches.ItemsSource = q.Length == 0
                    ? buffChoices
                    : buffChoices
                        .Where(s => s.Contains(q, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                spellPopup.IsOpen = open && spellMatches.Items.Count > 0;
            }
            void PickSpell(string picked)
            {
                choosingSpell = true;
                rule.Pattern = picked;
                spellName.Text = picked;
                pattern.Text = picked;
                spellName.CaretIndex = spellName.Text.Length;
                spellPopup.IsOpen = false;
                _vm.Persist();
                choosingSpell = false;
            }
            UpdateSpellChoices(rule.Pattern, open: false);
            spellName.TextChanged += (_, _) =>
            {
                if (choosingSpell) return;
                rule.Pattern = spellName.Text.Trim();
                pattern.Text = rule.Pattern;
                UpdateSpellChoices(spellName.Text, spellName.IsKeyboardFocusWithin);
            };
            spellName.GotKeyboardFocus += (_, _) => UpdateSpellChoices(spellName.Text, open: true);
            spellName.LostKeyboardFocus += (_, _) =>
            {
                rule.Pattern = (spellName.Text ?? "").Trim();
                pattern.Text = rule.Pattern;
                _vm.Persist();
            };
            spellName.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    spellPopup.IsOpen = false;
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && spellPopup.IsOpen
                    && spellMatches.Items.Count > 0)
                {
                    PickSpell((spellMatches.SelectedItem as string) ?? (string)spellMatches.Items[0]!);
                    e.Handled = true;
                }
            };
            spellMatches.SelectionChanged += (_, _) =>
            {
                if (!_ready || spellMatches.SelectedItem is not string picked) return;
                PickSpell(picked);
            };
            System.Windows.Controls.Grid.SetColumn(spellName, 1);
            matchArea.Children.Add(spellName);

            // A class filter needs no match text, so the box goes away rather than sitting
            // there inviting input that would be ignored.
            void SyncMatchArea()
            {
                var isFade = rule.Kind == EQBuddy.Core.WatchKind.SpellFade;
                var byName = rule.SpellFilter == EQBuddy.Core.SpellFilter.ByName;
                spellFilter.Visibility = isFade ? Visibility.Visible : Visibility.Collapsed;
                pattern.Visibility = !isFade ? Visibility.Visible : Visibility.Collapsed;
                // Regex pairs with the free-text Match box; the fade picker flow has
                // its own exact-name semantics.
                regexToggle.Visibility = !isFade ? Visibility.Visible : Visibility.Collapsed;
                spellName.Visibility = isFade && byName ? Visibility.Visible : Visibility.Collapsed;
                // With no match box beside it the combo takes the whole cell, so its text
                // and drop arrow stay inside the row instead of running under the toggles.
                System.Windows.Controls.Grid.SetColumnSpan(spellFilter, isFade && !byName ? 2 : 1);
                if (isFade && byName) spellName.Text = rule.Pattern;
                else pattern.Text = rule.Pattern;
            }
            SyncMatchArea();

            kind.SelectionChanged += (_, _) =>
            {
                if (!_ready || kind.SelectedIndex < 0) return;
                rule.Kind = (EQBuddy.Core.WatchKind)kind.SelectedIndex;
                SyncMatchArea();
                _vm.Persist();
            };
            spellFilter.SelectionChanged += (_, _) =>
            {
                if (!_ready || spellFilter.SelectedIndex < 0) return;
                rule.SpellFilter = (EQBuddy.Core.SpellFilter)spellFilter.SelectedIndex;
                SyncMatchArea();
                _vm.Persist();
            };

            row.Children.Add(RuleToggle("📌", "Show this rule as a chip in the mini dashboard", 3,
                rule.Pinned, v => rule.Pinned = v));

            row.Children.Add(RuleToggle("🔔", "Banner alert on match", 4, rule.AlertBanner,
                v => rule.AlertBanner = v));

            // Banner color: one small dot cycling the palette on click (Chaosrah's
            // color-coded alerts) — a combo box would not fit the row.
            var colorDot = new System.Windows.Controls.Button
            {
                Padding = new Thickness(2, 0, 2, 0),
                Margin = new Thickness(2, 0, 0, 0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            void PaintDot()
            {
                var hex = EQBuddy.UI.Shared.AlertColors.Hex(rule.AlertColor);
                var choiceName = EQBuddy.UI.Shared.AlertColors
                    .Choices[EQBuddy.UI.Shared.AlertColors.IndexOf(rule.AlertColor)].Name;
                colorDot.Content = new System.Windows.Controls.TextBlock
                {
                    Text = "●",
                    FontSize = 12,
                    Foreground = hex.Length > 0
                        ? new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex))
                        : (System.Windows.Media.Brush)FindResource("AccentBrush"),
                };
                colorDot.ToolTip = $"Banner color: {choiceName} — click to change";
            }
            PaintDot();
            colorDot.Click += (_, _) =>
            {
                var next = (EQBuddy.UI.Shared.AlertColors.IndexOf(rule.AlertColor) + 1)
                    % EQBuddy.UI.Shared.AlertColors.Choices.Length;
                var picked = EQBuddy.UI.Shared.AlertColors.Choices[next].Name;
                rule.AlertColor = picked == "Default" ? "" : picked;
                PaintDot();
                _vm.Persist();
            };
            System.Windows.Controls.Grid.SetColumn(colorDot, 5);
            row.Children.Add(colorDot);

            // Custom spoken phrase, beside the S toggle and only while it's on — a
            // phrase box on a rule that never speaks is dead weight in a tight row.
            // Empty speaks the alert's own label, exactly as before the box existed.
            var phrase = DarkBox(rule.SpokenPhrase,
                "What the voice says for this rule (empty = the alert text itself).\n" +
                "Say the instruction, not the event: \"Recast charm now\" instead of\n" +
                "\"Befriend Animal faded off a bear\".");
            phrase.Width = 76;
            phrase.FontSize = 11;
            phrase.Margin = new Thickness(4, 0, 0, 0);
            phrase.Visibility = rule.AlertSpeech ? Visibility.Visible : Visibility.Collapsed;
            phrase.LostFocus += (_, _) => { rule.SpokenPhrase = phrase.Text.Trim(); _vm.Persist(); };
            System.Windows.Controls.Grid.SetColumn(phrase, 7);

            row.Children.Add(RuleToggle("S", "Speak this alert with the Windows voice", 6,
                rule.AlertSpeech, v =>
                {
                    rule.AlertSpeech = v;
                    phrase.Visibility = v ? Visibility.Visible : Visibility.Collapsed;
                }));
            row.Children.Add(phrase);

            // Per-rule sound, so you can tell what happened from the audio alone.
            // Replaces the old on/off toggle: "Off" mutes, "Default" follows the shared
            // choice below, anything else is this rule's own sound.
            var sound = new System.Windows.Controls.ComboBox
            {
                FontSize = 11,
                MinWidth = 76,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = "Sound for this rule — pick a different one per rule to tell them apart by ear",
            };
            foreach (var s in AlertSoundCatalog.RuleChoices) sound.Items.Add(s);
            sound.SelectedIndex = AlertSoundCatalog.RuleChoiceIndex(rule);
            if (AlertSoundCatalog.IsCustom(rule.AlertSoundName) && rule.AlertSoundName.Length > 0)
                sound.ToolTip = $"Custom: {rule.AlertSoundName}";
            sound.SelectionChanged += (_, _) =>
            {
                if (!_ready || sound.SelectedIndex < 0) return;
                if (AlertSoundCatalog.ApplyRuleChoice(rule, sound.SelectedIndex))
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = $"Choose a sound for \"{(rule.Name.Length > 0 ? rule.Name : rule.Pattern)}\"",
                        Filter = "Sound files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*",
                    };
                    if (dlg.ShowDialog(this) == true)
                    {
                        rule.AlertSoundName = dlg.FileName;
                        sound.ToolTip = $"Custom: {dlg.FileName}";
                    }
                    else
                    {
                        // Cancelled — snap back to whatever the rule already had.
                        _ready = false;
                        sound.SelectedIndex = AlertSoundCatalog.RuleChoiceIndex(rule);
                        _ready = true;
                        return;
                    }
                }
                _vm.Persist();
                // Play it straight away so picking a sound is a decision you can hear.
                if (AlertSoundCatalog.Resolve(rule, _main.Settings.AlertSound) is { } preview)
                    _main.PlayAlertSound(preview);
            };
            System.Windows.Controls.Grid.SetColumn(sound, 8);
            row.Children.Add(sound);

            // Seconds to hold the alert back — 0 (or empty) is the immediate behaviour.
            // Turns a rule into a cue: sound 2.5 s after a heal-chain call to say "cast
            // now", or 25 s after a mez to say "recast before it breaks".
            var delay = DarkBox(DelayText.Format(rule.AlertDelaySeconds),
                "Wait this long before alerting (empty = at once, up to 30 minutes).\n" +
                "Seconds by default; add m for minutes — 2.5, 25, 8m, 1:30.\n" +
                "Use it as a cue: 2.5 after a heal-chain call, 25 into a 30s mez,\n" +
                "or 8m for a respawn. The count updates immediately either way.");
            delay.Width = 40;
            delay.Margin = new Thickness(4, 0, 0, 0);
            delay.TextAlignment = TextAlignment.Right;
            delay.LostFocus += (_, _) =>
            {
                // Unparseable means 0 rather than an error: the box is a few characters wide
                // and the failure is obvious the moment it snaps back.
                rule.AlertDelaySeconds = DelayText.Parse(delay.Text);
                delay.Text = DelayText.Format(rule.AlertDelaySeconds);   // shows any clamp
                _vm.Persist();
            };
            System.Windows.Controls.Grid.SetColumn(delay, 9);
            row.Children.Add(delay);

            // Share: the rule as a guild-chat string (WatchRuleShare). The ✓ flash is
            // the only feedback a clipboard write can honestly give.
            var share = new System.Windows.Controls.Button
            {
                Content = "⤴", Style = (Style)FindResource("IconButton"), FontSize = 11,
                ToolTip = "Copy this rule as a share string — paste it in guild chat or Discord,\n" +
                          "and any EQBuddy imports it from the box below the rule list",
            };
            share.Click += (_, _) =>
            {
                try { Clipboard.SetText(WatchRuleShare.Encode([rule])); }
                catch (Exception ex) { CoreLog.Error(ex); return; }
                share.Content = "✓";
                var revert = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.5),
                };
                revert.Tick += (_, _) => { share.Content = "⤴"; revert.Stop(); };
                revert.Start();
            };
            System.Windows.Controls.Grid.SetColumn(share, 10);
            row.Children.Add(share);

            var del = new System.Windows.Controls.Button
            {
                Content = "✕", Style = (Style)FindResource("IconButton"), FontSize = 11,
            };
            del.Click += (_, _) =>
            {
                _vm.RemoveRule(rule);
                BuildRulesEditor();
            };
            System.Windows.Controls.Grid.SetColumn(del, 11);
            row.Children.Add(del);

            // Arrange (#105, wizen): this order IS the Tracked card's "manual" sort.
            // Stacked ▲▼ in one cell — precise where drag would fight the text boxes.
            var arrange = new System.Windows.Controls.StackPanel { Margin = new Thickness(2, 0, 0, 0) };
            foreach (var (glyph, delta) in new[] { ("▲", -1), ("▼", +1) })
            {
                var move = new System.Windows.Controls.Button
                {
                    Content = glyph, Style = (Style)FindResource("IconButton"),
                    FontSize = 7, Padding = new Thickness(2, 0, 2, 0),
                    ToolTip = "Move this rule " + (delta < 0 ? "up" : "down") +
                              " — the watch display's \"manual\" sort follows this order",
                };
                var d = delta;
                move.Click += (_, _) => { _vm.MoveRule(rule, d); BuildRulesEditor(); };
                arrange.Children.Add(move);
            }
            System.Windows.Controls.Grid.SetColumn(arrange, 12);
            row.Children.Add(arrange);

            RulesPanel.Children.Add(row);
        }
    }

    // ---- share-string import: paste → preview → confirm (nothing lands unseen) ----

    private List<TrackedRule>? _pendingImport;

    private void OnImportShare(object sender, RoutedEventArgs e)
    {
        _pendingImport = WatchRuleShare.TryDecode(ImportBox.Text, out var error);
        ImportPreview.Visibility = Visibility.Visible;
        if (_pendingImport is null)
        {
            ImportPreview.Text = error;
            ImportConfirmBtn.Visibility = Visibility.Collapsed;
            return;
        }
        ImportPreview.Text = "This will add:\n" +
            string.Join("\n", _pendingImport.Select(r => "  • " + WatchRuleShare.Describe(r)));
        ImportConfirmBtn.Content = _pendingImport.Count == 1
            ? "✔ Add this rule" : $"✔ Add these {_pendingImport.Count} rules";
        ImportConfirmBtn.Visibility = Visibility.Visible;
    }

    private void OnImportConfirm(object sender, RoutedEventArgs e)
    {
        if (_pendingImport is null) return;
        _vm.ImportRules(_pendingImport);
        _pendingImport = null;
        ImportBox.Text = "";
        ImportPreview.Visibility = Visibility.Collapsed;
        ImportConfirmBtn.Visibility = Visibility.Collapsed;
        BuildRulesEditor();
    }

    private System.Windows.Controls.Primitives.ToggleButton RuleToggle(
        string glyph, string tip, int column, bool initial, Action<bool> apply)
    {
        var t = new System.Windows.Controls.Primitives.ToggleButton
        {
            Content = glyph, ToolTip = tip, IsChecked = initial, FontSize = 11,
            Style = (Style)FindResource("IconToggle"),
        };
        t.Checked += (_, _) => { apply(true); _vm.Persist(); };
        t.Unchecked += (_, _) => { apply(false); _vm.Persist(); };
        System.Windows.Controls.Grid.SetColumn(t, column);
        return t;
    }

    private System.Windows.Controls.TextBox DarkBox(string text, string tip)
    {
        var box = new System.Windows.Controls.TextBox
        {
            Text = text, ToolTip = tip, FontSize = 12,
            Padding = new Thickness(4, 2, 4, 2),
        };
        // SetResourceReference (not FindResource) so an in-place theme switch repaints
        // these rows too, not just the chrome built from XAML.
        box.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ComboBoxBrush");
        box.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "TextBrush");
        box.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "BorderBrush");
        return box;
    }

    private void BuildCardsEditor()
    {
        CardsPanel.Children.Clear();
        foreach (var card in _vm.Cards)
        {
            var row = new System.Windows.Controls.Grid { Margin = new Thickness(0, 2, 0, 0) };
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 3; i++)
                row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            // Since 1.66.3 every unhidden card shows (with an empty state when it has
            // nothing yet) — Options is the whole truth, no self-hiding asterisks.
            row.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = card.Title, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource(card.Hidden ? "DimBrush" : "TextBrush"),
            });

            row.Children.Add(CardButton("↑", "Move up", 1, () => { _vm.MoveCard(card.Key, -1); ApplyCards(); }));
            row.Children.Add(CardButton("↓", "Move down", 2, () => { _vm.MoveCard(card.Key, +1); ApplyCards(); }));
            row.Children.Add(CardButton(card.Hidden ? "🙈" : "👁",
                card.Hidden ? "Show card" : "Hide card (data still collected)", 3,
                () => { _vm.ToggleCard(card.Key); ApplyCards(); }));
            CardsPanel.Children.Add(row);
        }
    }

    private void ApplyCards()
    {
        _main.ApplySectionLayout();
        BuildCardsEditor();
    }

    private void UpdateGearImportStatus()
    {
        var total = _main.Settings.GearChecklist.Count;
        if (total == 0)
        {
            GearImportStatus.Text = "No gear list imported.";
            GearClearBtn.IsEnabled = false;
            return;
        }

        var done = _main.Settings.GearChecklist.Count(i => i.Acquired);
        var name = _main.Settings.GearChecklistName.Length > 0
            ? _main.Settings.GearChecklistName
            : "Imported gear list";
        GearImportStatus.Text = $"{name}: {done}/{total} checked.";
        GearClearBtn.IsEnabled = true;
    }

    private void OnImportGearChecklist(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import EQ Legends Tools shopping list",
            Filter = "HTML files (*.html;*.htm)|*.html;*.htm|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var import = GearChecklistImporter.ImportFile(dlg.FileName);
            if (import.Items.Count == 0)
            {
                GearImportStatus.Text = "No gear items found in that file.";
                return;
            }

            _main.ImportGearChecklist(import);
            UpdateGearImportStatus();
        }
        catch (Exception ex)
        {
            GearImportStatus.Text = $"Import failed: {ex.Message}";
        }
    }

    private void OnOpenGearTools(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://eqlegendstools.com/char-sheet/") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            GearImportStatus.Text = $"Could not open EQ Legends Tools: {ex.Message}";
        }
    }

    private void OnClearGearChecklist(object sender, RoutedEventArgs e)
    {
        _main.ClearGearChecklist();
        UpdateGearImportStatus();
    }

    private System.Windows.Controls.Button CardButton(string glyph, string tip, int column, Action action)
    {
        var b = new System.Windows.Controls.Button
        {
            Content = glyph, ToolTip = tip, FontSize = 11,
            Style = (Style)FindResource("IconButton"), Margin = new Thickness(6, 0, 0, 0),
        };
        b.Click += (_, _) => action();
        System.Windows.Controls.Grid.SetColumn(b, column);
        return b;
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void OnSecondScreen(object sender, RoutedEventArgs e) => _main.OpenCompanionWindow();

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
