using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

public sealed class OptionsWindow : Window
{
    private readonly MainWindow _main;
    private readonly TextBlock _scaleLabel = LabelValue();
    private readonly TextBlock _bgOpacityLabel = LabelValue();
    private readonly TextBlock _opacityLabel = LabelValue();
    private readonly Slider _scaleSlider = Slider(0.8, 1.6, 0.05);
    private readonly Slider _bgOpacitySlider = Slider(0.15, 1.0, 0.05);
    private readonly Slider _opacitySlider = Slider(0.5, 1.0, 0.02);
    private readonly Slider _alertVolumeSlider = Slider(0.1, 1.0, 0.05);
    private readonly TextBlock _alertVolumeLabel = LabelValue();
    private readonly CheckBox _truncateCheck = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly CheckBox _tutorialCheck = new() { Margin = new Thickness(0, 10, 0, 0) };
    private readonly CheckBox _trackSpawnsCheck = new() { Margin = new Thickness(0, 10, 0, 0) };
    private readonly CheckBox _targetDropsCheck = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly CheckBox _pinChipsCheck = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly ComboBox _themeCombo = new() { Width = 130, FontSize = 12 };
    private readonly StackPanel _customColorsPanel = new() { IsVisible = false };
    private readonly ComboBox _windowCombo = new() { Width = 90, FontSize = 12 };
    private readonly ComboBox _soundCombo = new() { Width = 120, FontSize = 12 };
    private readonly TextBlock _soundFileNote = AppTheme.DimText("");
    private readonly ScrollViewer _contentScroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };
    private readonly StackPanel _rulesPanel = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly Button _guideToggle = AppTheme.IconButton("> Show examples", "Worked examples for every rule kind");
    private readonly Border _guidePanel = new()
    {
        IsVisible = false,
        Margin = new Thickness(0, 4, 0, 2),
        Background = AppTheme.PanelBrush,
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(8, 6),
    };
    private readonly StackPanel _cardsPanel = new();
    private bool _ready;

    private static readonly string[] SoundNames = Array.ConvertAll(MainWindow.AlertSounds, x => x.Name);

    public OptionsWindow(MainWindow main)
    {
        _main = main;
        _main.RegisterOptionsWindow(this);
        Title = "EQBuddy Options";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _contentScroll.Content = BuildContent();
        Content = new Border
        {
            Background = AppTheme.BgBrush,
            CornerRadius = new CornerRadius(10),
            BorderBrush = AppTheme.BorderBrush,
            BorderThickness = new Thickness(1),
            Child = _contentScroll,
        };
        Opened += (_, _) => UpdateHeightLimit();
        PointerPressed += OnDrag;
        _scaleSlider.Value = main.UiScale;
        _opacitySlider.Value = main.WidgetOpacity;
        _bgOpacitySlider.Value = main.BackgroundOpacityValue;
        _alertVolumeSlider.Value = Math.Clamp(main.Settings.AlertVolume, 0.1, 1.0);
        Subscribe(_scaleSlider, () => _main.SetUiScale(_scaleSlider.Value));
        Subscribe(_bgOpacitySlider, () => _main.SetBackgroundOpacity(_bgOpacitySlider.Value));
        Subscribe(_opacitySlider, () => _main.SetWindowOpacity(_opacitySlider.Value));
        Subscribe(_alertVolumeSlider, () =>
        {
            _main.Settings.AlertVolume = _alertVolumeSlider.Value;
            _alertVolumeLabel.Text = $"{_alertVolumeSlider.Value:P0}";
            _main.PersistSettings();
        });
        _alertVolumeLabel.Text = $"{_alertVolumeSlider.Value:P0}";

        _truncateCheck.Content = new TextBlock
        {
            Text = "Auto-empty finished-session logs",
            FontSize = 12,
            Foreground = AppTheme.TextBrush,
        };
        _truncateCheck.IsChecked = main.TruncateLogsValue;
        _truncateCheck.IsCheckedChanged += (_, _) =>
        {
            if (_ready) _main.SetTruncateLogs(_truncateCheck.IsChecked == true);
        };

        _tutorialCheck.Content = new TextBlock
        {
            Text = "Show quick tutorial at launch",
            FontSize = 12,
            Foreground = AppTheme.TextBrush,
        };
        _tutorialCheck.IsChecked = main.Settings.ShowTutorial;
        _tutorialCheck.IsCheckedChanged += (_, _) =>
        {
            if (!_ready) return;
            _main.Settings.ShowTutorial = _tutorialCheck.IsChecked == true;
            _main.PersistSettings();
        };

        _trackSpawnsCheck.Content = new TextBlock
        {
            Text = "🕒 Track spawns (named respawn timers)",
            FontSize = 12,
            Foreground = AppTheme.TextBrush,
        };
        _trackSpawnsCheck.IsChecked = main.Settings.TrackSpawns;
        _trackSpawnsCheck.IsCheckedChanged += (_, _) =>
        {
            if (_ready) _main.SetTrackSpawns(_trackSpawnsCheck.IsChecked == true);
        };

        _targetDropsCheck.Content = new TextBlock
        {
            Text = "Show known drops for your current target",
            FontSize = 12,
            Foreground = AppTheme.TextBrush,
        };
        _targetDropsCheck.IsChecked = main.Settings.ShowTargetDrops;
        _targetDropsCheck.IsCheckedChanged += (_, _) =>
        {
            if (!_ready) return;
            _main.Settings.ShowTargetDrops = _targetDropsCheck.IsChecked == true;
            _main.PersistSettings();
        };

        _pinChipsCheck.Content = new TextBlock
        {
            Text = "📌 Show watch chips in the mini dashboard",
            FontSize = 12,
            Foreground = AppTheme.TextBrush,
        };
        _pinChipsCheck.IsChecked = main.Settings.PinWatchChips;
        _pinChipsCheck.IsCheckedChanged += (_, _) =>
        {
            if (!_ready) return;
            _main.Settings.PinWatchChips = _pinChipsCheck.IsChecked == true;
            _main.PersistSettings();
        };

        foreach (var label in OptionsViewModel.ThemeLabels) _themeCombo.Items.Add(label);
        _themeCombo.SelectedIndex = ThemeCatalog.IndexOf(main.Settings.Theme);
        _themeCombo.SelectionChanged += OnThemeChanged;

        foreach (var m in (int[])[5, 15, 30]) _windowCombo.Items.Add($"{m} min");
        _windowCombo.SelectedIndex = main.Settings.RecentWindowMinutes switch { 5 => 0, 30 => 2, _ => 1 };
        _windowCombo.SelectionChanged += (_, _) =>
        {
            if (!_ready) return;
            _main.Settings.RecentWindowMinutes = _windowCombo.SelectedIndex switch { 0 => 5, 2 => 30, _ => 15 };
            _main.PersistSettings();
        };

        foreach (var name in SoundNames) _soundCombo.Items.Add($"{name}{(name == "Ding" ? " (default)" : "")}");
        _soundCombo.Items.Add("Custom file...");
        var current = main.Settings.AlertSound switch
        {
            "Asterisk" or "" => "Ding",
            "Beep" => "Chord",
            "Hand" => "Chimes",
            "Question" => "Notify",
            { } other => other,
        };
        var idx = Array.IndexOf(SoundNames, current);
        _soundCombo.SelectedIndex = idx >= 0 ? idx : SoundNames.Length;
        _soundCombo.SelectionChanged += OnSoundChanged;
        UpdateSoundFileNote();

        BuildRulesEditor();
        BuildCardsEditor();
        UpdateCustomColorsPanel();
        // Restore before _ready so this doesn't count as the user changing it.
        ToggleGuide(main.Settings.ShowWatchGuide, persist: false);
        UpdateLabels();
        _ready = true;
    }

    private void UpdateHeightLimit()
    {
        var screen = Screens.ScreenFromWindow(this);
        if (screen is null) return;

        // Size to the content while it fits, then give the ScrollViewer a real viewport
        // once the options grow beyond the current monitor. Without this bound the window
        // simply kept measuring taller and the lower controls became unreachable.
        var workingHeight = screen.WorkingArea.Height / screen.Scaling;
        var availableHeight = Math.Max(240, workingHeight - 40);
        MaxHeight = availableHeight;
        _contentScroll.MaxHeight = availableHeight - 2; // outer border
    }

    private Control BuildContent()
    {
        var panel = new StackPanel { Margin = new Thickness(16), Width = 520 };
        var title = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        title.Children.Add(new TextBlock
        {
            Text = "Options",
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            Foreground = AppTheme.AccentBrush,
        });
        var close = AppTheme.IconButton("x", "Close");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => Close();
        title.Children.Add(close);
        panel.Children.Add(title);

        panel.Children.Add(Row("Theme", _themeCombo, new Thickness(0, 0, 0, 12)));
        panel.Children.Add(_customColorsPanel);

        AddSlider(panel, "Widget size", _scaleLabel, _scaleSlider);
        AddSlider(panel, "Background see-through", _bgOpacityLabel, _bgOpacitySlider,
            "Only the dark panel fades; text stays sharp.");
        AddSlider(panel, "Whole-widget opacity", _opacityLabel, _opacitySlider,
            "Fades everything, text included.");
        panel.Children.Add(_truncateCheck);
        panel.Children.Add(AppTheme.DimText(
            "Turn off if you use GINA/GamParse or upload your logs elsewhere - they will grow forever, so clean them up yourself occasionally. Cleanup already stands down whenever the game, GINA, or GamParse is running.",
            new Thickness(20, 2, 0, 0)));
        panel.Children.Add(_tutorialCheck);
        panel.Children.Add(_trackSpawnsCheck);
        panel.Children.Add(AppTheme.DimText(
            "Kill a named - or its placeholder - and a compact countdown chip appears. Chips stack, move together, show timers from every zone, and turn DUE for one minute (click to dismiss sooner). Double-click one (or use right-click -> Spawn timers) for the full zone list. Community timers are editable and your value wins through updates.",
            new Thickness(20, 2, 0, 0)));
        panel.Children.Add(_targetDropsCheck);
        panel.Children.Add(AppTheme.DimText(
            "Looks up the selected mob on the EQ community wiki and lists its known loot in the Loot card.",
            new Thickness(20, 2, 0, 0)));

        panel.Children.Add(Row("Recent-rate window", _windowCombo, new Thickness(0, 12, 0, 0)));
        panel.Children.Add(AppTheme.DimText("The Last Xm figures on Combat, Kills, Money, and Progress."));

        panel.Children.Add(Heading("Watch rules", new Thickness(0, 14, 0, 2)));
        panel.Children.Add(AppTheme.DimText("Watch loot, kills, skill-ups, deaths, milestones, your spells wearing off, or any text in the log. Match is a case-insensitive substring, e.g. 'mote'; when empty, the display name is used. Spell fade rules can pick a whole class (Any crowd control, Charm, Mez, Root, Lull, Stun, HoT) instead of a named spell, needing no match text. Delay holds the alert back that many seconds so it lands as a cue. B shows a banner and S plays a sound."));

        // Collapsed by default — the examples answer the questions people actually ask, and
        // are noise for anyone who already knows the answers.
        _guideToggle.Click += (_, _) => ToggleGuide(!_guidePanel.IsVisible, persist: true);
        panel.Children.Add(_guideToggle);
        panel.Children.Add(_guidePanel);

        panel.Children.Add(_rulesPanel);
        var add = AppTheme.IconButton("+ Add watch rule", "Add watch rule");
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.FontSize = 12;
        add.Click += (_, _) =>
        {
            _main.Settings.TrackedRules.Add(new TrackedRule { Name = "", Pattern = "" });
            _main.PersistSettings();
            BuildRulesEditor();
        };
        panel.Children.Add(add);
        panel.Children.Add(_pinChipsCheck);

        var soundRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        soundRow.Children.Add(_soundCombo);
        var test = AppTheme.IconButton(">", "Play the alert sound");
        test.Margin = new Thickness(4, 0, 0, 0);
        test.Click += (_, _) => _main.PlayAlertSound();
        soundRow.Children.Add(test);
        panel.Children.Add(Row("Alert sound", soundRow, new Thickness(0, 8, 0, 0)));
        AddSlider(panel, "Alert volume", _alertVolumeLabel, _alertVolumeSlider);
        panel.Children.Add(_soundFileNote);
        panel.Children.Add(AppTheme.DimText(
            "While Options is open, the ★ alert banner tile is visible — drag it to where alerts should appear. During play it's click-through and never steals focus.",
            new Thickness(0, 4, 0, 0)));

        panel.Children.Add(Heading("Overlay cards", new Thickness(0, 14, 0, 2)));
        panel.Children.Add(_cardsPanel);
        panel.Children.Add(AppTheme.DimText("Size also scales all text. Changes apply instantly and are saved.",
            new Thickness(0, 8, 0, 0)));
        return panel;
    }

    /// <summary>Keep an open Options window synchronized with the menu or tracker close.</summary>
    internal void SyncTrackSpawns(bool on)
    {
        var ready = _ready;
        _ready = false;
        _trackSpawnsCheck.IsChecked = on;
        _ready = ready;
    }

    /// <summary>Show or hide the worked examples, remembering the choice. Built on first
    /// expand rather than up front — most people never open it.</summary>
    private void ToggleGuide(bool open, bool persist)
    {
        _guidePanel.IsVisible = open;
        _guideToggle.Content = open ? "v Hide examples" : "> Show examples";
        if (open && _guidePanel.Child is null) _guidePanel.Child = BuildGuide();
        if (persist && _ready)
        {
            _main.Settings.ShowWatchGuide = open;
            _main.PersistSettings();
        }
    }

    private static Control BuildGuide()
    {
        var panel = new StackPanel();
        TextBlock Line(string text, IBrush brush, double top, bool bold = false) => new()
        {
            Text = text, FontSize = 11, Foreground = brush, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, top, 0, 0),
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
        };

        panel.Children.Add(Line("How matching works", AppTheme.AccentBrush, 0, bold: true));
        foreach (var basic in WatchGuide.Basics)
            panel.Children.Add(Line("- " + basic, AppTheme.DimBrush, 2));

        panel.Children.Add(Line("Examples", AppTheme.AccentBrush, 8, bold: true));
        foreach (var ex in WatchGuide.Examples)
        {
            var match = ex.Match.Length > 0 ? $"Match \"{ex.Match}\"" : "no match text";
            var delay = ex.Delay.Length > 0 ? $" - Delay {ex.Delay}" : "";
            panel.Children.Add(Line(
                $"{OptionsViewModel.KindNames[(int)ex.Kind]} - \"{ex.Name}\" - {match}{delay}",
                AppTheme.TextBrush, 8));
            panel.Children.Add(Line(ex.What, AppTheme.DimBrush, 1));
        }
        return panel;
    }

    private void BuildRulesEditor()
    {
        _rulesPanel.Children.Clear();
        foreach (var rule in _main.Settings.TrackedRules)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(92)));
            row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(115)));
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var i = 0; i < 6; i++)   // pin, banner, color, sound, delay, delete
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var kind = new ComboBox { FontSize = 11, Margin = new Thickness(0, 0, 4, 0) };
            foreach (var k in OptionsViewModel.KindNames) kind.Items.Add(k);
            kind.SelectedIndex = (int)rule.Kind;
            ToolTip.SetTip(kind, "What this rule watches");
            row.Children.Add(kind);

            var name = DarkBox(rule.Name, "Display name (also used as match text when the optional filter is empty)");
            name.PlaceholderText = "Display name";
            name.Margin = new Thickness(0, 0, 4, 0);
            name.LostFocus += (_, _) => { rule.Name = (name.Text ?? "").Trim(); _main.PersistSettings(); };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);

            // Spell-fade rules use this cell for a class picker plus optional match text.
            // A class-wide filter ignores match text, so hide the box without clearing it:
            // switching back to By name should restore exactly what the user entered.
            var matchArea = new Grid();
            matchArea.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            matchArea.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(matchArea, 2);
            row.Children.Add(matchArea);

            var spellFilter = new ComboBox
            {
                FontSize = 11,
                MinWidth = 104,
                Margin = new Thickness(0, 0, 4, 0),
            };
            foreach (var filter in OptionsViewModel.SpellFilterNames) spellFilter.Items.Add(filter);
            spellFilter.SelectedIndex = (int)rule.SpellFilter;
            ToolTip.SetTip(spellFilter,
                "Spell class: watch one named spell (By name + match text), or a whole class with no match text needed");
            matchArea.Children.Add(spellFilter);

            var pattern = DarkBox(rule.Pattern,
                "Optional case-insensitive match text; uses the display name when empty, and may be empty for Death or Milestone");
            pattern.PlaceholderText = "Match text (optional)";
            pattern.Margin = new Thickness(0, 0, 4, 0);
            pattern.LostFocus += (_, _) => { rule.Pattern = (pattern.Text ?? "").Trim(); _main.PersistSettings(); };
            Grid.SetColumn(pattern, 1);
            matchArea.Children.Add(pattern);

            void SyncMatchArea()
            {
                var isFade = rule.Kind == WatchKind.SpellFade;
                var byName = rule.SpellFilter == SpellFilter.ByName;
                spellFilter.IsVisible = isFade;
                pattern.IsVisible = !isFade || byName;
                Grid.SetColumnSpan(spellFilter, isFade && !byName ? 2 : 1);
            }
            SyncMatchArea();

            kind.SelectionChanged += (_, _) =>
            {
                if (!_ready || kind.SelectedIndex < 0) return;
                rule.Kind = (WatchKind)kind.SelectedIndex;
                SyncMatchArea();
                _main.PersistSettings();
            };
            spellFilter.SelectionChanged += (_, _) =>
            {
                if (!_ready || spellFilter.SelectedIndex < 0) return;
                rule.SpellFilter = (SpellFilter)spellFilter.SelectedIndex;
                SyncMatchArea();
                _main.PersistSettings();
            };

            row.Children.Add(RuleToggle("P", "Show this rule as a chip in the mini dashboard", 3,
                rule.Pinned, v => rule.Pinned = v));
            row.Children.Add(RuleToggle("B", "Banner alert on match", 4, rule.AlertBanner, v => rule.AlertBanner = v));

            var colorDot = AppTheme.IconButton("●", "Banner color");
            colorDot.Padding = new Thickness(2, 0);
            colorDot.Margin = new Thickness(2, 0, 2, 0);
            void PaintDot()
            {
                var hex = AlertColors.Hex(rule.AlertColor);
                colorDot.Foreground = hex.Length > 0
                    ? new SolidColorBrush(Color.Parse(hex))
                    : AppTheme.AccentBrush;
                var choice = AlertColors.Choices[AlertColors.IndexOf(rule.AlertColor)].Name;
                ToolTip.SetTip(colorDot, $"Banner color: {choice} - click to change");
            }
            PaintDot();
            colorDot.Click += (_, _) =>
            {
                var next = (AlertColors.IndexOf(rule.AlertColor) + 1) % AlertColors.Choices.Length;
                var picked = AlertColors.Choices[next].Name;
                rule.AlertColor = picked == "Default" ? "" : picked;
                PaintDot();
                _main.PersistSettings();
            };
            Grid.SetColumn(colorDot, 5);
            row.Children.Add(colorDot);

            // Per-rule sound, replacing the old on/off toggle. Telling rules apart by ear is
            // the entire point — and it matters most for delayed alerts, where the usual
            // setup is two rules on one match ("heard it" now, "cast now" later) that are
            // indistinguishable if they share a sound.
            var sound = new ComboBox
            {
                FontSize = 11,
                MinWidth = 86,
                Margin = new Thickness(0, 0, 4, 0),
            };
            foreach (var choice in AlertSoundCatalog.RuleChoices) sound.Items.Add(choice);
            sound.SelectedIndex = AlertSoundCatalog.RuleChoiceIndex(rule);
            ToolTip.SetTip(sound, AlertSoundCatalog.IsCustom(rule.AlertSoundName) && rule.AlertSoundName.Length > 0
                ? $"Custom: {rule.AlertSoundName}"
                : "Sound for this rule - pick a different one per rule to tell them apart by ear");
            sound.SelectionChanged += async (_, _) =>
            {
                if (!_ready || sound.SelectedIndex < 0) return;
                if (AlertSoundCatalog.ApplyRuleChoice(rule, sound.SelectedIndex))
                {
                    var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = $"Choose a sound for \"{(rule.Name.Length > 0 ? rule.Name : rule.Pattern)}\"",
                        AllowMultiple = false,
                        FileTypeFilter = [new FilePickerFileType("Sound files") { Patterns = ["*.wav", "*.mp3", "*.ogg"] }],
                    });
                    if (picked.FirstOrDefault()?.TryGetLocalPath() is { } path)
                    {
                        rule.AlertSoundName = path;
                        ToolTip.SetTip(sound, $"Custom: {path}");
                    }
                    else
                    {
                        // Cancelled — snap back to what the rule already had.
                        _ready = false;
                        sound.SelectedIndex = AlertSoundCatalog.RuleChoiceIndex(rule);
                        _ready = true;
                        return;
                    }
                }
                _main.PersistSettings();
                // Play it straight away, so picking a sound is a decision you can hear.
                if (AlertSoundCatalog.Resolve(rule, _main.Settings.AlertSound) is { } preview)
                    _main.PlayAlertSound(preview);
            };
            Grid.SetColumn(sound, 6);
            row.Children.Add(sound);

            // Seconds to hold the alert back — 0 (or empty) is the immediate behaviour.
            // Turns a rule into a cue: sound 2.5 s after a heal-chain call to say "cast
            // now", or 25 s after a mez to say "recast before it breaks".
            var delay = DarkBox(DelayText.Format(rule.AlertDelaySeconds),
                "Wait this long before alerting (empty = at once, up to 30 minutes). " +
                "Seconds by default; add m for minutes - 2.5, 25, 8m, 1:30. " +
                "The count updates immediately either way - only the alert waits.");
            delay.PlaceholderText = "0s";
            delay.Width = 48;
            delay.Margin = new Thickness(0, 0, 4, 0);
            delay.TextAlignment = global::Avalonia.Media.TextAlignment.Right;
            delay.LostFocus += (_, _) =>
            {
                rule.AlertDelaySeconds = DelayText.Parse(delay.Text);
                delay.Text = DelayText.Format(rule.AlertDelaySeconds);
                _main.PersistSettings();
            };
            Grid.SetColumn(delay, 7);
            row.Children.Add(delay);

            var del = AppTheme.IconButton("x", "Delete rule");
            del.Click += (_, _) =>
            {
                _main.Settings.TrackedRules.Remove(rule);
                _main.PersistSettings();
                BuildRulesEditor();
            };
            Grid.SetColumn(del, 8);
            row.Children.Add(del);
            _rulesPanel.Children.Add(row);
        }
    }

    private ToggleButton RuleToggle(string glyph, string tip, int column, bool initial, Action<bool> apply)
    {
        var t = AppTheme.IconToggle(glyph, tip);
        t.IsChecked = initial;
        t.IsCheckedChanged += (_, _) =>
        {
            apply(t.IsChecked == true);
            _main.PersistSettings();
        };
        Grid.SetColumn(t, column);
        return t;
    }

    private void BuildCardsEditor()
    {
        _cardsPanel.Children.Clear();
        var order = _main.Settings.SectionOrder
            .Where(k => MainWindow.SectionCatalog.Any(c => c.Key == k))
            .ToList();
        foreach (var (key, _) in MainWindow.SectionCatalog)
            if (!order.Contains(key)) order.Add(key);
        _main.Settings.SectionOrder = order;

        foreach (var key in order)
        {
            var title = MainWindow.SectionCatalog.First(c => c.Key == key).Title;
            var row = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var i = 0; i < 3; i++) row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var hidden = _main.Settings.HiddenSections.Contains(key);
            row.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = hidden ? AppTheme.DimBrush : AppTheme.TextBrush,
            });
            row.Children.Add(CardButton("^", "Move up", 1, () => MoveCard(key, -1), compact: true));
            row.Children.Add(CardButton("v", "Move down", 2, () => MoveCard(key, 1), compact: true));
            row.Children.Add(CardButton(hidden ? "Show" : "Hide", hidden ? "Show card" : "Hide card", 3, () =>
            {
                if (!_main.Settings.HiddenSections.Remove(key))
                    _main.Settings.HiddenSections.Add(key);
                ApplyCards();
            }));
            _cardsPanel.Children.Add(row);
        }
    }

    private Button CardButton(string text, string tip, int column, Action action, bool compact = false)
    {
        var b = AppTheme.IconButton(text, tip);
        b.FontSize = 12;
        b.Width = compact ? 28 : 48;
        b.MinWidth = b.Width;
        b.Height = 26;
        b.MinHeight = 26;
        b.Padding = new Thickness(0);
        b.Margin = new Thickness(column == 1 ? 0 : 4, 0, 0, 0);
        b.HorizontalContentAlignment = HorizontalAlignment.Center;
        b.VerticalContentAlignment = VerticalAlignment.Center;
        b.VerticalAlignment = VerticalAlignment.Center;
        b.Click += (_, _) => action();
        Grid.SetColumn(b, column);
        return b;
    }

    private void MoveCard(string key, int delta)
    {
        var order = _main.Settings.SectionOrder;
        var i = order.IndexOf(key);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= order.Count) return;
        (order[i], order[j]) = (order[j], order[i]);
        ApplyCards();
    }

    private void ApplyCards()
    {
        _main.PersistSettings();
        _main.ApplySectionLayout();
        BuildCardsEditor();
    }

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _themeCombo.SelectedIndex < 0) return;
        _main.Settings.Theme = ThemeCatalog.Themes[_themeCombo.SelectedIndex].Key;
        _main.PersistSettings();
        AppTheme.Apply(_main.Settings);
        _main.RefreshTheme();
        UpdateCustomColorsPanel();
    }

    private static readonly string[] SwatchColors =
    [
        "#000000", "#1A1A1A", "#20242B", "#26211A", "#002B36", "#FDF6E3", "#FFFFFF",
        "#EAEAEA", "#E3B341", "#FFD24D", "#5FA8D3", "#3FCFBE", "#7FBF5F", "#E0654A",
        "#C080D0", "#9C9C9C",
    ];

    private void UpdateCustomColorsPanel()
    {
        _customColorsPanel.IsVisible = _main.Settings.Theme == CustomTheme.Key;
        _customColorsPanel.Children.Clear();
        if (!_customColorsPanel.IsVisible) return;

        _customColorsPanel.Margin = new Thickness(0, -7, 0, 10);
        _customColorsPanel.Children.Add(ColorRow("Background",
            _main.Settings.CustomThemeBg ?? CustomTheme.DefaultBg,
            value => _main.Settings.CustomThemeBg = value));
        _customColorsPanel.Children.Add(ColorRow("Text",
            _main.Settings.CustomThemeText ?? CustomTheme.DefaultText,
            value => _main.Settings.CustomThemeText = value));
        _customColorsPanel.Children.Add(ColorRow("Accent",
            _main.Settings.CustomThemeAccent ?? CustomTheme.DefaultAccent,
            value => _main.Settings.CustomThemeAccent = value));
    }

    private Control ColorRow(string label, string initial, Action<string> store)
    {
        var current = initial;
        var row = new Grid { Margin = new Thickness(0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(78)));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(76)));
        row.Children.Add(new TextBlock
        {
            Text = label, FontSize = 11, Foreground = AppTheme.TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var hex = DarkBox(current, "#RRGGBB");
        hex.Width = 74;
        void Commit(string? value)
        {
            if (CustomTheme.Valid(value) is not { } valid)
            {
                hex.Text = current;
                return;
            }
            current = valid;
            hex.Text = valid;
            store(valid);
            _main.PersistSettings();
            AppTheme.Apply(_main.Settings);
            _main.RefreshTheme();
        }

        var swatches = new WrapPanel { Margin = new Thickness(2, 0, 6, 0) };
        foreach (var color in SwatchColors)
        {
            var swatch = new Border
            {
                Width = 15, Height = 15, Margin = new Thickness(1),
                CornerRadius = new CornerRadius(2), BorderThickness = new Thickness(1),
                BorderBrush = AppTheme.DimBrush,
                Background = new SolidColorBrush(Color.Parse(color)),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(swatch, color);
            swatch.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(swatch).Properties.IsLeftButtonPressed) return;
                Commit(color);
                e.Handled = true;
            };
            swatches.Children.Add(swatch);
        }
        Grid.SetColumn(swatches, 1);
        row.Children.Add(swatches);

        hex.LostFocus += (_, _) => Commit(hex.Text);
        hex.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Commit(hex.Text);
            e.Handled = true;
        };
        Grid.SetColumn(hex, 2);
        row.Children.Add(hex);
        return row;
    }

    private async void OnSoundChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (_soundCombo.SelectedIndex < SoundNames.Length)
        {
            _main.Settings.AlertSound = SoundNames[_soundCombo.SelectedIndex];
        }
        else
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose an alert sound",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Sound files") { Patterns = ["*.wav", "*.mp3", "*.ogg"] },
                    FilePickerFileTypes.All,
                ],
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is not null)
                _main.Settings.AlertSound = path;
        }
        _main.PersistSettings();
        UpdateSoundFileNote();
        _main.PlayAlertSound();
    }

    private void UpdateSoundFileNote()
    {
        var custom = Array.IndexOf(SoundNames, _main.Settings.AlertSound) < 0;
        _soundFileNote.Text = custom ? $"Custom: {_main.Settings.AlertSound}" : "";
        _soundFileNote.IsVisible = custom;
    }

    private static TextBox DarkBox(string text, string tip)
    {
        var box = new TextBox
        {
            Text = text,
            FontSize = 12,
            Background = AppTheme.ComboBoxBrush,
            Foreground = AppTheme.TextBrush,
            BorderBrush = AppTheme.BorderBrush,
            Padding = new Thickness(4, 2),
        };
        ToolTip.SetTip(box, tip);
        return box;
    }

    private static TextBlock Heading(string text, Thickness margin) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = AppTheme.AccentBrush,
        Margin = margin,
    };

    private static Control Row(string label, Control control, Thickness? margin = null)
    {
        var row = new Grid { Margin = margin ?? default };
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = AppTheme.TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private static void AddSlider(Panel panel, string label, TextBlock value, Slider slider, string? hint = null)
    {
        panel.Children.Add(Row(label, value));
        if (hint is not null) panel.Children.Add(AppTheme.DimText(hint));
        slider.Margin = new Thickness(0, 4, 0, 12);
        panel.Children.Add(slider);
    }

    private void Subscribe(Slider slider, Action apply)
    {
        slider.PropertyChanged += (_, args) =>
        {
            if (args.Property != RangeBase.ValueProperty || !_ready) return;
            apply();
            UpdateLabels();
        };
    }

    private void UpdateLabels()
    {
        _scaleLabel.Text = $"{_scaleSlider.Value:P0}";
        _opacityLabel.Text = $"{_opacitySlider.Value:P0}";
        _bgOpacityLabel.Text = $"{_bgOpacitySlider.Value:P0}";
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(IsInteractiveControl))
            return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private static bool IsInteractiveControl(Visual visual) => visual is
        Button or TextBox or ComboBox or global::Avalonia.Controls.Slider or CheckBox or ToggleButton or ScrollBar;

    private static TextBlock LabelValue() => new()
    {
        FontSize = 12,
        Foreground = AppTheme.AccentBrush,
    };

    private static Slider Slider(double min, double max, double tick) => new()
    {
        Minimum = min,
        Maximum = max,
        TickFrequency = tick,
        IsSnapToTickEnabled = true,
    };
}
