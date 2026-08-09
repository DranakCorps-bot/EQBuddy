using System.Runtime.InteropServices;
using System.Windows;
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
        RegenPerTickBox.Text = _vm.RegenPerTickOverride > 0 ? _vm.RegenPerTickOverride.ToString() : "";
        TrackSpawnsCheck.IsChecked = _main.Settings.TrackSpawns;

        foreach (var choice in OptionsViewModel.WindowChoices) WindowCombo.Items.Add(choice);
        WindowCombo.SelectedIndex = _vm.RecentWindowIndex;

        foreach (var choice in OptionsViewModel.SoundChoices) SoundCombo.Items.Add(choice);
        SoundCombo.SelectedIndex = _vm.SoundIndex;
        AlertVolumeSlider.Value = Math.Clamp(_vm.Settings.AlertVolume, 0.1, 1.0);
        AlertVolumeLabel.Text = $"{AlertVolumeSlider.Value:P0}";
        UpdateSoundFileNote();

        BuildRulesEditor();
        BuildCardsEditor();
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

    private void OnHideUnfocusedToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.HideWhenGameUnfocused = HideUnfocusedCheck.IsChecked == true;
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
        Auto("RuleSound");
        Auto("RuleDelay");
        Auto("RuleShare");
        Auto("RuleDelete");
        return grid;
    }

    private void BuildRulesEditor()
    {
        RulesPanel.Children.Clear();

        var header = RuleGrid();
        header.Margin = new Thickness(0, 2, 0, 2);
        var headings = new[] { ("Watch", 0), ("Name", 1), ("Match", 2), ("Delay", 8) };
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

            var pattern = DarkBox(rule.Pattern, "match text (uses the name if left empty; optional for Death/Milestone)");
            pattern.Margin = new Thickness(4, 0, 0, 0);
            pattern.LostFocus += (_, _) => { rule.Pattern = pattern.Text.Trim(); _vm.Persist(); };
            System.Windows.Controls.Grid.SetColumn(pattern, 1);
            matchArea.Children.Add(pattern);

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

            row.Children.Add(RuleToggle("S", "Speak this alert with the Windows voice", 6,
                rule.AlertSpeech, v => rule.AlertSpeech = v));

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
            System.Windows.Controls.Grid.SetColumn(sound, 7);
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
            System.Windows.Controls.Grid.SetColumn(delay, 8);
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
            System.Windows.Controls.Grid.SetColumn(share, 9);
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
            System.Windows.Controls.Grid.SetColumn(del, 10);
            row.Children.Add(del);

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
