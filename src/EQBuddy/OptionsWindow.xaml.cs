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
        MobileSoundsCheck.IsChecked = _vm.MobileSounds;
        MobileSoundsLabel.Text = EQBuddy.UI.Shared.MobileAlertSounds.Label;
        MobileSoundsNote.Text = EQBuddy.UI.Shared.MobileAlertSounds.HelperText
            + " " + EQBuddy.UI.Shared.MobileAlertSounds.ScopeNote;
        HideUnfocusedCheck.IsChecked = _vm.HideWhenGameUnfocused;
        HideNotRunningCheck.IsChecked = _vm.HideWhenGameNotRunning;
        HideAltTabCheck.IsChecked = _vm.HideFromAltTab;
        // The cost is stated where the choice is made: one flag, both effects.
        HideAltTabNote.Text = string.Join(" ",
            new[] { EQBuddy.UI.Shared.AltTabPolicy.TaskbarWarning,
                    EQBuddy.UI.Shared.AltTabPolicy.UnavailableNote }
                .Where(s => s.Length > 0));
        KeepAboveCheck.IsChecked = _vm.KeepAboveOverlays;
        DoubleClickChipsCheck.IsChecked = _main.Settings.DoubleClickChipsToggleBreakouts;
        CursorRingCheck.IsChecked = _main.Settings.ShowCursorRing;
        PerfStatsCheck.IsChecked = _main.Settings.ShowPerfStats;
        SelectTab(_main.Settings.OptionsTab);
        BuildHotkeyRows();
        RegenPerTickBox.Text = _vm.RegenPerTickOverride > 0 ? _vm.RegenPerTickOverride.ToString() : "";

        foreach (var choice in OptionsViewModel.WindowChoices) WindowCombo.Items.Add(choice);
        WindowCombo.SelectedIndex = _vm.RecentWindowIndex;

        UpdateGearImportStatus();
        UpdateCustomColorsPanel();
        // The Cards & windows tab's three editors, lifted into OptionsCardsView.cs when
        // the mini-dashboard list pushed this file past its ratchet — CLAUDE.md's rule is
        // to lift a surface, never to raise the ceiling.
        _cardsView = new OptionsCardsView(_main, _vm, () => _ready,
            CardsPanel, MiniStatsPanel, BreakoutsPanel, BreakoutsBlurb, FindResource);
        _cardsView.RenderAll();
        BuildAlertsTabs();

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

    private void OnDoubleClickChipsToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _main.Settings.DoubleClickChipsToggleBreakouts = DoubleClickChipsCheck.IsChecked == true;
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

    /// <summary>MainWindow calls this when the breakout editor writes the same storage,
    /// so an edit made there appears here immediately too. Forwarded: the editor is
    /// SettingsAlertsView's now, and this window is one of its two hosts.</summary>
    internal void RefreshBuffSetEditor() => _alerts?.RefreshBuffSetEditor();

    // ---- the four alert blocks (SR-4) ----

    /// <summary>
    /// This window's own instance of the alert blocks — never a shared one. A WPF
    /// <c>UIElement</c> has exactly one parent, so a block borrowed from another host would
    /// be torn out of whichever painted it last, silently (trap 45).
    /// </summary>
    private SettingsAlertsView? _alerts;

    /// <summary>
    /// The v1 arrangement, rebuilt from the lifted blocks: Watch keeps its own tab (with the
    /// <c>PinWatchChips</c> row XAML still declares beneath it), and the Alerts tab stacks the
    /// shared sound header over Buffs, Spawns and Crowd.
    ///
    /// **Order and headings come from <see cref="AlertSurface"/>, not from this file** — the
    /// first spend of a definition that has been sitting unused since before the pivot, and
    /// the reason the shell's Settings room cannot end up showing a different set of tabs in
    /// a different order from the window it replaces. The badges are real counts: rules
    /// written, buff buckets assembled, timers running.
    /// </summary>
    private void BuildAlertsTabs()
    {
        _alerts = new SettingsAlertsView(_main, _vm, () => _ready, FindResource, () => this);

        WatchBlockHost.Children.Add(_alerts.Block(AlertTab.Watch));

        TabAlertsPanel.Children.Add(_alerts.Header);
        foreach (var tab in _alerts.Tabs())
        {
            // Watch is the one that has a tab of its own here — it is the biggest editor in
            // Options and it had its own tab before the lift. Nothing else is skipped.
            if (tab.Tab == AlertTab.Watch) continue;
            TabAlertsPanel.Children.Add(_alerts.Heading(tab));
            TabAlertsPanel.Children.Add(_alerts.Block(tab.Tab));
        }
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

    /// <summary>#208. No sample plays on the flip — Bevel's lock is explicit about that,
    /// and a demo noise from a PC while the phone is the surface under discussion would be
    /// answering a different question.</summary>
    private void OnMobileSoundsToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.MobileSounds = MobileSoundsCheck.IsChecked == true;
    }

    private void OnHideUnfocusedToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.HideWhenGameUnfocused = HideUnfocusedCheck.IsChecked == true;
    }

    private void OnHideNotRunningToggled(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.HideWhenGameNotRunning = HideNotRunningCheck.IsChecked == true;
    }

    /// <summary>Applied to every open window immediately, not on the next launch — a
    /// tick-box whose effect waits for a relaunch is indistinguishable from a broken
    /// one, and this one has a visible answer the moment it lands.</summary>
    private void OnHideAltTabToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _vm.HideFromAltTab = HideAltTabCheck.IsChecked == true;
        _main.ApplyAltTabStyle();
    }

    /// <summary>
    /// One checkbox per breakout kind, and ticking one now actually TURNS IT ON.
    ///
    /// It used to only clear the ✕-dismissal (discussion #45), while the switch that
    /// decides whether the window ever opens was the ★ on a card — so someone who came
    /// here, found "🐾 Pet", ticked it and saw nothing had to go and ask. That question
    /// kept coming back on Reddit (David, 2026-08-20), and the answer was always "yes,
    /// but also star it somewhere else", which is a tick box that lies.
    ///
    /// Unticking is deliberately NOT symmetric: it stops the window and leaves the star
    /// alone. For every kind but Buffs that same key is also a cell in the minimised
    /// pill, and quietly removing someone's pill cell because they closed a window would
    /// be a second silent surprise in the opposite direction.
    /// </summary>

    private void OnRegenPerTickChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        // Blank or unparseable = back to the wiki base; the box shows any clamp.
        _vm.RegenPerTickOverride = int.TryParse(RegenPerTickBox.Text.Trim(), out var v) ? v : 0;
        RegenPerTickBox.Text = _vm.RegenPerTickOverride > 0 ? _vm.RegenPerTickOverride.ToString() : "";
    }

    /// <summary>Called back by MainWindow.SetTrackSpawns so closing the Spawns window
    /// (or toggling the menu) updates the box while Options sits open. Forwarded to the
    /// block that owns it.</summary>
    internal void SyncTrackSpawns(bool on) => _alerts?.SyncTrackSpawns(on);

    private void OnPinChipsChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) _vm.PinWatchChips = PinChipsCheck.IsChecked == true;
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
        _cardsView?.BuildCards();
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
            _cardsView?.BuildCards();
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


    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    /// <summary>The Cards &amp; windows editors, lifted into their own file for the same
    /// reason as the mez one below.</summary>
    private OptionsCardsView? _cardsView;

    private void OnSecondScreen(object sender, RoutedEventArgs e) => _main.OpenCompanionWindow();

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
