using System.Windows;
using System.Windows.Input;

namespace EQBuddy;

public partial class OptionsWindow : Window
{
    private readonly MainWindow _main;
    private bool _ready;

    public OptionsWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        Owner = main;
        ScaleSlider.Value = main.UiScale;
        OpacitySlider.Value = main.Opacity;
        BgOpacitySlider.Value = main.BackgroundOpacityValue;
        TruncateCheck.IsChecked = main.TruncateLogsValue;

        foreach (var m in (int[])[5, 15, 30])
            WindowCombo.Items.Add($"{m} min");
        WindowCombo.SelectedIndex = main.Settings.RecentWindowMinutes switch
        {
            5 => 0, 30 => 2, _ => 1,
        };

        foreach (var name in SoundNames) SoundCombo.Items.Add($"{name}{(name == "Ding" ? " (default)" : "")}");
        SoundCombo.Items.Add("Custom file…");
        var current = main.Settings.AlertSound switch
        {
            // legacy SystemSounds names saved by earlier builds
            "Asterisk" or "" => "Ding", "Beep" => "Chord", "Hand" => "Chimes", "Question" => "Notify",
            { } other => other,
        };
        var idx = Array.IndexOf(SoundNames, current);
        SoundCombo.SelectedIndex = idx >= 0 ? idx : SoundNames.Length;   // custom slot
        UpdateSoundFileNote();

        PinChipsCheck.IsChecked = main.Settings.PinWatchChips;
        TutorialCheck.IsChecked = main.Settings.ShowTutorial;

        BuildRulesEditor();
        BuildCardsEditor();
        HotkeyNote.Text =
            $"{main.Settings.HotkeyToggleOverlay} show/hide · {main.Settings.HotkeyClickThrough} click-through · " +
            $"{main.Settings.HotkeyMiniMode} mini · {main.Settings.HotkeyCampMarker} camp marker";

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

    private void OnTruncateChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _main.SetTruncateLogs(TruncateCheck.IsChecked == true);
    }

    private void OnWindowChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _main.Settings.RecentWindowMinutes = WindowCombo.SelectedIndex switch { 0 => 5, 2 => 30, _ => 15 };
        _main.PersistSettings();
    }

    private static readonly string[] SoundNames =
        Array.ConvertAll(MainWindow.AlertSounds, x => x.Name);

    private void UpdateSoundFileNote()
    {
        var custom = Array.IndexOf(SoundNames, _main.Settings.AlertSound) < 0;
        SoundFileNote.Text = custom ? $"Custom: {_main.Settings.AlertSound}" : "";
        SoundFileNote.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSoundChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (SoundCombo.SelectedIndex < SoundNames.Length)
        {
            _main.Settings.AlertSound = SoundNames[SoundCombo.SelectedIndex];
        }
        else
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose an alert sound",
                Filter = "Sound files (*.wav;*.mp3)|*.wav;*.mp3|All files (*.*)|*.*",
            };
            if (dlg.ShowDialog(this) == true)
            {
                _main.Settings.AlertSound = dlg.FileName;
            }
            else if (Array.IndexOf(SoundNames, _main.Settings.AlertSound) is >= 0 and var prev)
            {
                _ready = false; SoundCombo.SelectedIndex = prev; _ready = true;   // cancelled — revert
            }
        }
        _main.PersistSettings();
        UpdateSoundFileNote();
        _main.PlayAlertSound();   // instant feedback on the new choice
    }

    private void OnSoundTest(object sender, RoutedEventArgs e) => _main.PlayAlertSound();

    private void OnTutorialToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _main.Settings.ShowTutorial = TutorialCheck.IsChecked == true;
        _main.PersistSettings();
    }

    private void OnPinChipsChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _main.Settings.PinWatchChips = PinChipsCheck.IsChecked == true;
        _main.PersistSettings();
    }

    private void OnAddRule(object sender, RoutedEventArgs e)
    {
        _main.Settings.TrackedRules.Add(new EQBuddy.Core.TrackedRule { Name = "", Pattern = "" });
        _main.PersistSettings();
        BuildRulesEditor();
    }

    private void BuildRulesEditor()
    {
        RulesPanel.Children.Clear();
        foreach (var rule in _main.Settings.TrackedRules)
        {
            var row = new System.Windows.Controls.Grid { Margin = new Thickness(0, 3, 0, 0) };
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(58) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 4; i++)
                row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            var kind = new System.Windows.Controls.ComboBox { FontSize = 11, ToolTip = "What this rule watches" };
            foreach (var k in Enum.GetNames<EQBuddy.Core.WatchKind>()) kind.Items.Add(k);
            kind.SelectedIndex = (int)rule.Kind;
            kind.SelectionChanged += (_, _) =>
            {
                if (!_ready || kind.SelectedIndex < 0) return;
                rule.Kind = (EQBuddy.Core.WatchKind)kind.SelectedIndex;
                _main.PersistSettings();
            };
            row.Children.Add(kind);

            var name = DarkBox(rule.Name, "name");
            name.Margin = new Thickness(4, 0, 0, 0);
            name.LostFocus += (_, _) => { rule.Name = name.Text.Trim(); _main.PersistSettings(); };
            System.Windows.Controls.Grid.SetColumn(name, 1);
            row.Children.Add(name);

            var pattern = DarkBox(rule.Pattern, "match text (uses the name if left empty; optional for Death/Milestone)");
            pattern.Margin = new Thickness(4, 0, 0, 0);
            pattern.LostFocus += (_, _) => { rule.Pattern = pattern.Text.Trim(); _main.PersistSettings(); };
            System.Windows.Controls.Grid.SetColumn(pattern, 2);
            row.Children.Add(pattern);

            row.Children.Add(RuleToggle("🔔", "Banner alert on match", 3, rule.AlertBanner,
                v => rule.AlertBanner = v));
            row.Children.Add(RuleToggle("🔊", "Sound alert on match", 4, rule.AlertSound,
                v => rule.AlertSound = v));

            var del = new System.Windows.Controls.Button
            {
                Content = "✕", Style = (Style)FindResource("IconButton"), FontSize = 11,
            };
            del.Click += (_, _) =>
            {
                _main.Settings.TrackedRules.Remove(rule);
                _main.PersistSettings();
                BuildRulesEditor();
            };
            System.Windows.Controls.Grid.SetColumn(del, 5);
            row.Children.Add(del);

            RulesPanel.Children.Add(row);
        }
    }

    private System.Windows.Controls.Primitives.ToggleButton RuleToggle(
        string glyph, string tip, int column, bool initial, Action<bool> apply)
    {
        var t = new System.Windows.Controls.Primitives.ToggleButton
        {
            Content = glyph, ToolTip = tip, IsChecked = initial, FontSize = 11,
            Style = (Style)FindResource("IconToggle"),
        };
        t.Checked += (_, _) => { apply(true); _main.PersistSettings(); };
        t.Unchecked += (_, _) => { apply(false); _main.PersistSettings(); };
        System.Windows.Controls.Grid.SetColumn(t, column);
        return t;
    }

    private System.Windows.Controls.TextBox DarkBox(string text, string tip) => new()
    {
        Text = text, ToolTip = tip, FontSize = 12,
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x2A, 0x25, 0x1F)),
        Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
        BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
        Padding = new Thickness(4, 2, 4, 2),
    };

    private void BuildCardsEditor()
    {
        CardsPanel.Children.Clear();
        var order = _main.Settings.SectionOrder.ToList();
        foreach (var (key, _) in MainWindow.SectionCatalog)
            if (!order.Contains(key)) order.Add(key);
        _main.Settings.SectionOrder = order;

        foreach (var key in order)
        {
            var title = MainWindow.SectionCatalog.First(c => c.Key == key).Title;
            var row = new System.Windows.Controls.Grid { Margin = new Thickness(0, 2, 0, 0) };
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 3; i++)
                row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            var hidden = _main.Settings.HiddenSections.Contains(key);
            row.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = title, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource(hidden ? "DimBrush" : "TextBrush"),
            });

            row.Children.Add(CardButton("↑", "Move up", 1, () => MoveCard(key, -1)));
            row.Children.Add(CardButton("↓", "Move down", 2, () => MoveCard(key, +1)));
            row.Children.Add(CardButton(hidden ? "🙈" : "👁", hidden ? "Show card" : "Hide card (data still collected)", 3, () =>
            {
                if (!_main.Settings.HiddenSections.Remove(key))
                    _main.Settings.HiddenSections.Add(key);
                ApplyCards();
            }));
            CardsPanel.Children.Add(row);
        }
    }

    private System.Windows.Controls.Button CardButton(string glyph, string tip, int column, Action action)
    {
        var b = new System.Windows.Controls.Button
        {
            Content = glyph, ToolTip = tip, FontSize = 11,
            Style = (Style)FindResource("IconButton"),
        };
        b.Click += (_, _) => action();
        System.Windows.Controls.Grid.SetColumn(b, column);
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

    private void UpdateLabels()
    {
        ScaleLabel.Text = $"{ScaleSlider.Value:P0}";
        OpacityLabel.Text = $"{OpacitySlider.Value:P0}";
        BgOpacityLabel.Text = $"{BgOpacitySlider.Value:P0}";
    }

    private void OnBgOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _main.SetBackgroundOpacity(BgOpacitySlider.Value);
        UpdateLabels();
    }

    private void OnScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _main.SetUiScale(ScaleSlider.Value);
        UpdateLabels();
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _main.SetWindowOpacity(OpacitySlider.Value);
        UpdateLabels();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
