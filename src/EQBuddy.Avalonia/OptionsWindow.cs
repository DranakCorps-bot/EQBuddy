using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using EQBuddy.Core;

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
    private readonly CheckBox _truncateCheck = new() { Margin = new Thickness(0, 12, 0, 0) };
    private readonly ComboBox _windowCombo = new() { Width = 90, FontSize = 12 };
    private readonly ComboBox _soundCombo = new() { Width = 120, FontSize = 12 };
    private readonly TextBlock _soundFileNote = AppTheme.DimText("");
    private readonly StackPanel _rulesPanel = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly StackPanel _cardsPanel = new();
    private bool _ready;

    private static readonly string[] SoundNames = Array.ConvertAll(MainWindow.AlertSounds, x => x.Name);

    public OptionsWindow(MainWindow main)
    {
        _main = main;
        Title = "EQBuddy Options";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = new Border
        {
            Background = AppTheme.BgBrush,
            CornerRadius = new CornerRadius(10),
            BorderBrush = AppTheme.BorderBrush,
            BorderThickness = new Thickness(1),
            Child = BuildContent(),
        };
        PointerPressed += OnDrag;
        _scaleSlider.Value = main.UiScale;
        _opacitySlider.Value = main.WidgetOpacity;
        _bgOpacitySlider.Value = main.BackgroundOpacityValue;
        Subscribe(_scaleSlider, () => _main.SetUiScale(_scaleSlider.Value));
        Subscribe(_bgOpacitySlider, () => _main.SetBackgroundOpacity(_bgOpacitySlider.Value));
        Subscribe(_opacitySlider, () => _main.SetWindowOpacity(_opacitySlider.Value));

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
        UpdateLabels();
        _ready = true;
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

        AddSlider(panel, "Widget size", _scaleLabel, _scaleSlider);
        AddSlider(panel, "Background see-through", _bgOpacityLabel, _bgOpacitySlider,
            "Only the dark panel fades; text stays sharp.");
        AddSlider(panel, "Whole-widget opacity", _opacityLabel, _opacitySlider,
            "Fades everything, text included.");
        panel.Children.Add(_truncateCheck);
        panel.Children.Add(AppTheme.DimText(
            "Turn off if you upload your log files elsewhere - they will grow forever, so clean them up yourself occasionally.",
            new Thickness(20, 2, 0, 0)));

        panel.Children.Add(Row("Recent-rate window", _windowCombo, new Thickness(0, 12, 0, 0)));
        panel.Children.Add(AppTheme.DimText("The Last Xm figures on Combat, Kills, Money, and Progress."));

        panel.Children.Add(Heading("Watch rules", new Thickness(0, 14, 0, 2)));
        panel.Children.Add(AppTheme.DimText("Watch loot, kills, skill-ups, deaths, milestones, or your spells wearing off (SpellFade — the mez/charm-break alarm). Match is a case-insensitive substring, e.g. 'mote' or 'Befriend'; when empty, the display name is used. P pins a mini chip, B shows a banner, and S plays a sound."));
        panel.Children.Add(_rulesPanel);
        var add = AppTheme.IconButton("+ Add tracked item", "Add tracked item");
        add.HorizontalAlignment = HorizontalAlignment.Left;
        add.FontSize = 12;
        add.Click += (_, _) =>
        {
            _main.Settings.TrackedRules.Add(new TrackedRule { Name = "", Pattern = "" });
            _main.PersistSettings();
            BuildRulesEditor();
        };
        panel.Children.Add(add);

        var soundRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        soundRow.Children.Add(_soundCombo);
        var test = AppTheme.IconButton(">", "Play the alert sound");
        test.Margin = new Thickness(4, 0, 0, 0);
        test.Click += (_, _) => _main.PlayAlertSound();
        soundRow.Children.Add(test);
        panel.Children.Add(Row("Alert sound", soundRow, new Thickness(0, 8, 0, 0)));
        panel.Children.Add(_soundFileNote);
        panel.Children.Add(AppTheme.DimText(
            "While Options is open, the ★ alert banner tile is visible — drag it to where alerts should appear. During play it's click-through and never steals focus.",
            new Thickness(0, 4, 0, 0)));

        panel.Children.Add(Heading("Overlay cards", new Thickness(0, 14, 0, 2)));
        panel.Children.Add(_cardsPanel);
        panel.Children.Add(AppTheme.DimText(
            $"Hotkeys (global, editable in settings.json):\n{_main.Settings.HotkeyToggleOverlay} show/hide - {_main.Settings.HotkeyClickThrough} click-through - {_main.Settings.HotkeyMiniMode} mini - {_main.Settings.HotkeyCampMarker} camp marker",
            new Thickness(0, 14, 0, 0)));
        panel.Children.Add(AppTheme.DimText("Size also scales all text. Changes apply instantly and are saved.",
            new Thickness(0, 8, 0, 0)));
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
            for (var i = 0; i < 4; i++)
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var kind = new ComboBox { FontSize = 11, Margin = new Thickness(0, 0, 4, 0) };
            foreach (var k in Enum.GetNames<WatchKind>()) kind.Items.Add(k);
            kind.SelectedIndex = (int)rule.Kind;
            ToolTip.SetTip(kind, "What this rule watches");
            kind.SelectionChanged += (_, _) =>
            {
                if (!_ready || kind.SelectedIndex < 0) return;
                rule.Kind = (WatchKind)kind.SelectedIndex;
                _main.PersistSettings();
            };
            row.Children.Add(kind);

            var name = DarkBox(rule.Name, "Display name (also used as match text when the optional filter is empty)");
            name.PlaceholderText = "Display name";
            name.Margin = new Thickness(0, 0, 4, 0);
            name.LostFocus += (_, _) => { rule.Name = (name.Text ?? "").Trim(); _main.PersistSettings(); };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);

            var pattern = DarkBox(rule.Pattern, "Optional case-insensitive match text; uses the display name when empty, and may be empty for Death or Milestone");
            pattern.PlaceholderText = "Match text (optional)";
            pattern.Margin = new Thickness(0, 0, 4, 0);
            pattern.LostFocus += (_, _) => { rule.Pattern = (pattern.Text ?? "").Trim(); _main.PersistSettings(); };
            Grid.SetColumn(pattern, 2);
            row.Children.Add(pattern);

            row.Children.Add(RuleToggle("P", "Pin to mini dashboard", 3, rule.Pinned, v => rule.Pinned = v));
            row.Children.Add(RuleToggle("B", "Banner alert on match", 4, rule.AlertBanner, v => rule.AlertBanner = v));
            row.Children.Add(RuleToggle("S", "Sound alert on match", 5, rule.AlertSound, v => rule.AlertSound = v));

            var del = AppTheme.IconButton("x", "Delete rule");
            del.Click += (_, _) =>
            {
                _main.Settings.TrackedRules.Remove(rule);
                _main.PersistSettings();
                BuildRulesEditor();
            };
            Grid.SetColumn(del, 6);
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
        var order = _main.Settings.SectionOrder.ToList();
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
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x25, 0x1F)),
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
