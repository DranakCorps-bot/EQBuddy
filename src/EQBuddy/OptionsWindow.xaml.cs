using System.Windows;
using System.Windows.Input;
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

        ScaleSlider.Value = _vm.UiScale;
        OpacitySlider.Value = _vm.Opacity;
        BgOpacitySlider.Value = _vm.BackgroundOpacity;
        TruncateCheck.IsChecked = _vm.TruncateLogs;
        PinChipsCheck.IsChecked = _vm.PinWatchChips;
        TutorialCheck.IsChecked = _vm.ShowTutorial;

        foreach (var choice in OptionsViewModel.WindowChoices) WindowCombo.Items.Add(choice);
        WindowCombo.SelectedIndex = _vm.RecentWindowIndex;

        foreach (var choice in OptionsViewModel.SoundChoices) SoundCombo.Items.Add(choice);
        SoundCombo.SelectedIndex = _vm.SoundIndex;
        UpdateSoundFileNote();

        BuildRulesEditor();
        BuildCardsEditor();
        HotkeyNote.Text = _vm.HotkeyNote;

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
        OpacityLabel.Text = _vm.OpacityLabel;
        BgOpacityLabel.Text = _vm.BackgroundOpacityLabel;
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

    private void OnTutorialToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.ShowTutorial = TutorialCheck.IsChecked == true;
    }

    private void OnPinChipsChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.PinWatchChips = PinChipsCheck.IsChecked == true;
    }

    private void OnWindowChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_ready) _vm.RecentWindowIndex = WindowCombo.SelectedIndex;
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

    private void UpdateSoundFileNote()
    {
        SoundFileNote.Text = _vm.SoundFileNote;
        SoundFileNote.Visibility = _vm.SoundFileNote.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAddRule(object sender, RoutedEventArgs e)
    {
        _vm.AddRule();
        BuildRulesEditor();
    }

    private void BuildRulesEditor()
    {
        RulesPanel.Children.Clear();
        foreach (var rule in _vm.Rules)
        {
            var row = new System.Windows.Controls.Grid { Margin = new Thickness(0, 3, 0, 0) };
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(58) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 3; i++)
                row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            var kind = new System.Windows.Controls.ComboBox { FontSize = 11, ToolTip = "What this rule watches" };
            foreach (var k in OptionsViewModel.KindNames) kind.Items.Add(k);
            kind.SelectedIndex = (int)rule.Kind;
            kind.SelectionChanged += (_, _) =>
            {
                if (!_ready || kind.SelectedIndex < 0) return;
                rule.Kind = (EQBuddy.Core.WatchKind)kind.SelectedIndex;
                _vm.Persist();
            };
            row.Children.Add(kind);

            var name = DarkBox(rule.Name, "name");
            name.Margin = new Thickness(4, 0, 0, 0);
            name.LostFocus += (_, _) => { rule.Name = name.Text.Trim(); _vm.Persist(); };
            System.Windows.Controls.Grid.SetColumn(name, 1);
            row.Children.Add(name);

            var pattern = DarkBox(rule.Pattern, "match text (uses the name if left empty; optional for Death/Milestone)");
            pattern.Margin = new Thickness(4, 0, 0, 0);
            pattern.LostFocus += (_, _) => { rule.Pattern = pattern.Text.Trim(); _vm.Persist(); };
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
                _vm.RemoveRule(rule);
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
        t.Checked += (_, _) => { apply(true); _vm.Persist(); };
        t.Unchecked += (_, _) => { apply(false); _vm.Persist(); };
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
        foreach (var card in _vm.Cards)
        {
            var row = new System.Windows.Controls.Grid { Margin = new Thickness(0, 2, 0, 0) };
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 3; i++)
                row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

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

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
