using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// Thin view over the shared OptionsViewModel (EQBuddy.UI.Shared) — mappings and
/// mutations live there; this class builds controls, forwards input, and applies the
/// visual side effects (scale/opacity/layout) to the main window. Tabbed since the
/// WPF 1.67.0 reorganization (David: "a wall of options... needs serious
/// reorganization") — hand-rolled nav links and one visible panel, matching the WPF
/// window's sort-link idiom rather than a native TabControl.
/// </summary>
public sealed class OptionsWindow : Window
{
    private readonly MainWindow _main;
    private readonly OptionsViewModel _vm;
    private bool _ready;

    // ---- Look ----
    private readonly TextBlock _scaleLabel = LabelValue();
    private readonly TextBlock _chipScaleLabel = LabelValue();
    private readonly TextBlock _bgOpacityLabel = LabelValue();
    private readonly TextBlock _opacityLabel = LabelValue();
    private readonly TextBlock _gridSpacingLabel = LabelValue();
    private readonly Slider _scaleSlider = Slider(0.8, 1.6, 0.05);
    private readonly Slider _chipScaleSlider = Slider(0.8, 2.0, 0.05);
    private readonly Slider _bgOpacitySlider = Slider(0.15, 1.0, 0.05);
    private readonly Slider _opacitySlider = Slider(0.5, 1.0, 0.02);
    private readonly Slider _gridSpacingSlider = Slider(16, 128, 8);
    private readonly ComboBox _themeCombo = new() { Width = 130, FontSize = 12 };
    private readonly StackPanel _customColorsPanel = new() { IsVisible = false };
    private CheckBox _gridOverlayCheck = null!;
    private CheckBox _cursorRingCheck = null!;

    // ---- Alerts & chips ----
    private readonly Slider _alertVolumeSlider = Slider(0.1, 1.0, 0.05);
    private readonly TextBlock _alertVolumeLabel = LabelValue();
    private readonly ComboBox _soundCombo = new() { Width = 120, FontSize = 12 };
    private readonly TextBlock _soundFileNote = AppTheme.DimText("");
    private readonly ComboBox _voiceCombo = new() { Width = 164, FontSize = 12 };
    private readonly Slider _speechRateSlider = Slider(SpokenAlerts.MinRate, SpokenAlerts.MaxRate, 1);
    private readonly TextBlock _speechRateLabel = LabelValue();
    private readonly Slider _speechVolumeSlider = Slider(0, 100, 5);
    private readonly TextBlock _speechVolumeLabel = LabelValue();
    private IReadOnlyList<string> _installedVoices = [];
    private CheckBox _slowAlertCheck = null!;
    private CheckBox _slowSpokenCheck = null!;
    private CheckBox _slowRaidOnlyCheck = null!;
    private CheckBox _buffExpiringOnlyCheck = null!;
    private readonly TextBox _buffWarnBox;
    private CheckBox _mezChipsCheck = null!;
    private readonly StackPanel _mezDurationList = new();
    private CheckBox _trackSpawnsCheck = null!;
    private CheckBox _spawnGrowUpCheck = null!;
    private CheckBox _mezGrowUpCheck = null!;

    // ---- Buff set (#120, Frankthetankk — the missing line's editor) ----
    private readonly TextBlock _buffSetCharNote = AppTheme.DimText("", new Thickness(0, 2, 0, 0));
    private readonly StackPanel _buffSetPanel = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly ComboBox _buffSetClassBox = new()
    {
        MinWidth = 110,
        FontSize = 12,
        Margin = new Thickness(0, 0, 6, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBox _buffSetAddBox;
    private readonly ListBox _buffSetMatches = new() { MaxHeight = 240, MaxWidth = 480, FontSize = 11.5 };
    private readonly Popup _buffSetPopup;

    // ---- Watch rules ----
    private readonly StackPanel _rulesPanel = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly Button _guideToggle = AppTheme.IconButton("▸ Show examples", "Worked examples for every rule kind");
    private readonly Border _guidePanel = new()
    {
        IsVisible = false,
        Margin = new Thickness(0, 4, 0, 2),
        Background = AppTheme.PanelBrush,
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(8, 6),
    };
    private readonly TextBox _importBox;
    private readonly TextBlock _importPreview = AppTheme.DimText("", new Thickness(0, 4, 0, 0));
    private readonly Button _importConfirmBtn = AppTheme.ActionButton("");
    private List<TrackedRule>? _pendingImport;
    private readonly CheckBox _recentHideChat = new() { Margin = new Thickness(4, 3) };
    private readonly ListBox _recentLinesList = new() { MaxHeight = 300, MaxWidth = 560, FontSize = 11.5 };
    private readonly Popup _recentLinesPopup;
    private CheckBox _pinChipsCheck = null!;

    // ---- Cards & windows ----
    private readonly StackPanel _cardsPanel = new();
    private readonly WrapPanel _breakoutsPanel = new();
    private readonly TextBlock _gearImportStatus = AppTheme.DimText("", new Thickness(0, 4, 0, 0));
    private readonly Button _gearClearBtn = AppTheme.ActionButton("Clear");
    private readonly ComboBox _windowCombo = new() { Width = 90, FontSize = 12 };
    private CheckBox _targetDropsCheck = null!;

    // ---- Behavior ----
    private readonly StackPanel _hotkeysPanel = new() { Margin = new Thickness(0, 2, 0, 0) };
    private readonly TextBox _regenPerTickBox;
    private CheckBox _hideUnfocusedCheck = null!;
    private CheckBox _hideNotRunningCheck = null!;
    private CheckBox _keepAboveCheck = null!;
    private CheckBox _truncateCheck = null!;
    private CheckBox _archiveCheck = null!;
    private CheckBox _tutorialCheck = null!;
    private CheckBox _perfStatsCheck = null!;
    private string? _recordingAction;
    private string? _recordingHint;

    // ---- chrome ----
    private readonly ScrollViewer _contentScroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };
    private readonly LayoutTransformControl _zoomHost = new();
    private readonly StackPanel _lookPanel = new();
    private readonly StackPanel _alertsPanel = new() { IsVisible = false };
    private readonly StackPanel _watchPanel = new() { IsVisible = false };
    private readonly StackPanel _cardsTabPanel = new() { IsVisible = false };
    private readonly StackPanel _behaviorPanel = new() { IsVisible = false };
    private readonly List<(string Key, TextBlock Link, Panel Panel)> _tabs = [];

    /// <summary>Below this the rule editor's Name and Match boxes collapse to a character
    /// or two; the same floor and ceiling the WPF window uses.</summary>
    private const double MinOptionsWidth = 390;
    private const double MaxOptionsWidth = 900;

    private bool _resizing;
    private double _resizeStartWidth;
    private int _resizeStartLeft;      // screen pixels — the space Position lives in
    private int _resizeStartPointerX;  // ditto

    // ---- MainWindow integration hooks (set right after construction; null = the
    // setting still persists, only the live side effect waits for wiring). These exist
    // because parallel porting owns MainWindow — see the report's wiring list. ----
    internal Func<IReadOnlyList<(DateTime Time, string Message)>>? RecentLinesSource { get; set; }
    internal Action<double>? ApplyChipScale { get; set; }
    internal Action<bool>? ApplyGridOverlay { get; set; }
    internal Action? ApplyGridSpacing { get; set; }
    internal Action<bool>? ApplyCursorRing { get; set; }
    internal Action? ApplyHotkeys { get; set; }
    internal Action? RefreshGearCard { get; set; }

    public OptionsWindow(MainWindow main)
    {
        _main = main;
        _vm = new OptionsViewModel(main.Settings, main.PersistSettings);
        _main.RegisterOptionsWindow(this);
        Title = "EQBuddy Options";
        // Height follows the content, width is the user's — as on Windows. Sizing to
        // content in both directions is what made this window unresizable: the width was
        // whatever the panel measured, and turning CanResize on would not have helped,
        // because custom chrome leaves no native resize border to drag. The side grips
        // below are the affordance on both platforms.
        SizeToContent = SizeToContent.Height;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        MinWidth = MinOptionsWidth;
        MaxWidth = MaxOptionsWidth;
        Width = Math.Clamp(_vm.OptionsWidth, MinOptionsWidth, MaxOptionsWidth);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _buffWarnBox = DarkBox(main.Settings.BuffWarnSeconds.ToString("0"), "Seconds of warning");
        _buffWarnBox.Width = 44;
        _buffWarnBox.TextAlignment = global::Avalonia.Media.TextAlignment.Right;
        _regenPerTickBox = DarkBox(
            _vm.RegenPerTickOverride > 0 ? _vm.RegenPerTickOverride.ToString() : "",
            "Your real per-tick regen amount (blank = wiki base)");
        _regenPerTickBox.Width = 48;
        _regenPerTickBox.TextAlignment = global::Avalonia.Media.TextAlignment.Right;
        _importBox = DarkBox("",
            "Paste a shared watch rule (EQB1.…) from guild chat or Discord — the ⤴ on any rule row makes one");
        _recentLinesPopup = new Popup
        {
            Placement = PlacementMode.Top,
            IsLightDismissEnabled = true,
        };
        _buffSetAddBox = DarkBox("",
            "Type a few letters of a buff's name — buffs you've been seen casting list first, then the whole buff catalog");
        // Light dismiss (WPF's StaysOpen="False"): the matches are a suggestion, so a click
        // anywhere else is an answer of "none of these". It costs the search box nothing —
        // an Avalonia popup host is shown without activation, so typing keeps flowing into
        // the box behind it.
        _buffSetPopup = new Popup
        {
            Placement = PlacementMode.Bottom,
            IsLightDismissEnabled = true,
            PlacementTarget = _buffSetAddBox,
        };

        _contentScroll.Content = BuildTabsBody();
        _zoomHost.Child = BuildChrome();
        Content = new Grid
        {
            Children =
            {
                new Border
                {
                    Background = AppTheme.BgBrush,
                    CornerRadius = new CornerRadius(10),
                    BorderBrush = AppTheme.HairlineBrush,
                    BorderThickness = new Thickness(1),
                    Child = _zoomHost,
                },
                Grip(leftEdge: true),
                Grip(leftEdge: false),
            },
        };
        Opened += (_, _) =>
        {
            if (Screens.ScreenFromWindow(this) is { } screen) PlaceBesideWidget(screen);
            ClampToScreen();
        };
        // Re-clamp on move: the user may drag onto a monitor with a different size or
        // DPI, where the saved bounds no longer fit (the WPF window does the same).
        PositionChanged += (_, _) => ClampToScreen();
        PointerPressed += OnDrag;
        // Global-hotkey recording sees keys before any focused control does — tunnel is
        // Avalonia's PreviewKeyDown.
        AddHandler(KeyDownEvent, OnRecordKeyDown, RoutingStrategies.Tunnel);
        AttachWindowZoom();

        InitLook();
        InitAlerts();
        InitWatch();
        InitCards();
        InitBehavior();

        SelectTab(main.Settings.OptionsTab, persist: false);
        // Restore before _ready so this doesn't count as the user changing it.
        ToggleGuide(main.Settings.ShowWatchGuide, persist: false);
        UpdateLabels();
        _ready = true;
    }

    // ---------------------------------------------------------------- chrome & tabs

    private Control BuildChrome()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        // Title row and tab bar sit outside the scroll so ✕ and navigation are always
        // reachable, no matter how far down a tall tab the body is scrolled.
        var title = new Grid { Margin = new Thickness(16, 16, 16, 6) };
        title.Children.Add(new TextBlock
        {
            Text = "Options",
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            Foreground = AppTheme.AccentBrush,
        });
        var close = AppTheme.IconButton("✕", "Close");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => Close();
        title.Children.Add(close);
        grid.Children.Add(title);

        var tabStrip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(16, 0, 16, 8),
        };
        foreach (var (key, label, panel) in (ReadOnlySpan<(string, string, Panel)>)
        [
            ("look", "Look", _lookPanel),
            ("alerts", "Alerts & chips", _alertsPanel),
            ("watch", "Watch rules", _watchPanel),
            ("cards", "Cards & windows", _cardsTabPanel),
            ("behavior", "Behavior", _behaviorPanel),
        ])
        {
            var link = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Cursor = new Cursor(StandardCursorType.Hand),
                Padding = new Thickness(0, 0, 0, 3),
                Margin = new Thickness(0, 0, 14, 0),
                Foreground = AppTheme.DimBrush,
            };
            var tabKey = key;
            link.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(link).Properties.IsLeftButtonPressed) return;
                SelectTab(tabKey, persist: true);
                e.Handled = true;   // a tab click must not start a window drag
            };
            _tabs.Add((key, link, panel));
            tabStrip.Children.Add(link);
        }
        Grid.SetRow(tabStrip, 1);
        grid.Children.Add(tabStrip);

        Grid.SetRow(_contentScroll, 2);
        grid.Children.Add(_contentScroll);
        return grid;
    }

    private Control BuildTabsBody()
    {
        // No fixed width: the body fills whatever the window is dragged to, and the rule
        // rows' star column absorbs the difference. One panel visible at a time.
        var body = new Grid { Margin = new Thickness(16, 0, 16, 16) };
        body.Children.Add(_lookPanel);
        body.Children.Add(_alertsPanel);
        body.Children.Add(_watchPanel);
        body.Children.Add(_cardsTabPanel);
        body.Children.Add(_behaviorPanel);
        return body;
    }

    private void SelectTab(string tab, bool persist)
    {
        // Leaving the tab disarms an armed recorder: a forgotten one would silently
        // eat the next keystroke anywhere in the window as a hotkey.
        if (_recordingAction is not null)
        {
            _recordingAction = null;
            _recordingHint = null;
            BuildHotkeyRows();
        }
        if (_tabs.All(t => t.Key != tab)) tab = "look";   // stale setting → home
        foreach (var (key, link, panel) in _tabs)
        {
            var active = key == tab;
            panel.IsVisible = active;
            link.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
            link.TextDecorations = active ? TextDecorations.Underline : null;
            link.Foreground = active ? AppTheme.AccentBrush : AppTheme.DimBrush;
        }
        if (persist && _ready)
        {
            _main.Settings.OptionsTab = tab;
            _main.PersistSettings();
        }
    }

    /// <summary>Ctrl+wheel zoom (discussion #59), the WPF WindowZoom counterpart: one
    /// permanent mechanism per window, persisted in WindowZooms under "options".</summary>
    private void AttachWindowZoom()
    {
        ApplyZoom(_main.Settings.WindowZooms.GetValueOrDefault("options", 1.0));
        AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            e.Handled = true;
            var current = _main.Settings.WindowZooms.GetValueOrDefault("options", 1.0);
            var next = WindowZoomMath.Step(current, Math.Sign(e.Delta.Y));
            _main.Settings.WindowZooms["options"] = next;
            ApplyZoom(next);
            _main.PersistSettings();
        }, RoutingStrategies.Tunnel);
    }

    private void ApplyZoom(double zoom) =>
        // Layout (not Render) transform: the window must grow/shrink with the content,
        // and the scroll viewport has to measure the scaled size.
        _zoomHost.LayoutTransform = Math.Abs(zoom - 1.0) < 0.001
            ? null
            : new ScaleTransform(zoom, zoom);

    private void ClampToScreen()
    {
        var screen = Screens.ScreenFromWindow(this);
        if (screen is null) return;

        // Size to the content while it fits, then give the ScrollViewer a real viewport
        // once the options grow beyond the current monitor. Without this bound the window
        // simply kept measuring taller and the lower controls became unreachable.
        var workingHeight = screen.WorkingArea.Height / screen.Scaling;
        var availableHeight = Math.Max(240, workingHeight - 40);
        MaxHeight = availableHeight;
        // The title row and tab bar live outside the scroll now; leave them their space.
        _contentScroll.MaxHeight = Math.Max(160, availableHeight - 74);

        // Width needs the same treatment for a different reason: a width saved on a wide
        // monitor would otherwise open wider than a narrow one, putting one or both grips
        // off-screen where they cannot be grabbed to bring it back.
        var workingWidth = screen.WorkingArea.Width / screen.Scaling;
        MaxWidth = Math.Max(MinOptionsWidth + 1, Math.Min(MaxOptionsWidth, workingWidth - 24));
        if (Bounds.Width > MaxWidth) Width = MaxWidth;
    }

    /// <summary>CenterOwner can land the window off-screen next to an edge-docked
    /// widget — once measured, sit beside the widget (left if room, else right),
    /// clamped to the work area. Mirrors the WPF Loaded placement.</summary>
    private void PlaceBesideWidget(global::Avalonia.Platform.Screen screen)
    {
        var wa = screen.WorkingArea;
        var w = (int)Math.Round(Bounds.Width * RenderScaling);
        var h = (int)Math.Round(Bounds.Height * RenderScaling);
        var gap = (int)Math.Round(12 * RenderScaling);
        var mainWidth = (int)Math.Round(_main.Bounds.Width * _main.RenderScaling);
        var x = _main.Position.X - w - gap;
        if (x < wa.X + 8) x = _main.Position.X + mainWidth + gap;
        x = Math.Max(wa.X + 8, Math.Min(x, wa.Right - w - 8));
        var y = Math.Max(wa.Y + 8, Math.Min(_main.Position.Y, wa.Bottom - h - 8));
        Position = new PixelPoint(x, y);
    }

    /// <summary>An invisible strip down one side of the window. It renders as bare edge —
    /// the cursor is the whole affordance — matching the WPF grips.</summary>
    private Border Grip(bool leftEdge)
    {
        var grip = new Border
        {
            Width = 8,
            Background = Brushes.Transparent,
            HorizontalAlignment = leftEdge ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
        };
        ToolTip.SetTip(grip, "Drag to resize");
        grip.PointerPressed += (_, e) => BeginResize(grip, e);
        grip.PointerMoved += (_, e) => ResizeTo(e, leftEdge);
        grip.PointerReleased += (_, e) => EndResize(e);
        return grip;
    }

    private void BeginResize(Control grip, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
        _resizing = true;
        _resizeStartWidth = Bounds.Width;
        _resizeStartLeft = Position.X;
        _resizeStartPointerX = PointerScreenX(e);
        e.Pointer.Capture(grip);
        // Without this the window-wide handler also sees the press and starts a move drag,
        // so the window would walk off with the pointer instead of resizing.
        e.Handled = true;
    }

    private void ResizeTo(PointerEventArgs e, bool leftEdge)
    {
        if (!_resizing) return;
        var moved = (PointerScreenX(e) - _resizeStartPointerX) / RenderScaling;
        var width = Math.Clamp(leftEdge ? _resizeStartWidth - moved : _resizeStartWidth + moved,
            MinWidth, MaxWidth);
        // Growing leftwards has to move the window as well, or the left edge would stay put
        // and the window would grow out of its right side, away from the pointer.
        if (leftEdge)
            Position = Position.WithX(
                _resizeStartLeft + (int)Math.Round((_resizeStartWidth - width) * RenderScaling));
        Width = width;
    }

    private void EndResize(PointerReleasedEventArgs e)
    {
        if (!_resizing) return;
        _resizing = false;
        e.Pointer.Capture(null);
        _vm.OptionsWidth = Bounds.Width;
    }

    /// <summary>Pointer X in screen pixels. The window moves and resizes under the pointer
    /// mid-drag, so a window-relative position is a moving ruler; the screen is a fixed one.</summary>
    private int PointerScreenX(PointerEventArgs e) => this.PointToScreen(e.GetPosition(this)).X;

    // ---------------------------------------------------------------- Look

    private void InitLook()
    {
        var panel = _lookPanel;
        panel.Children.Add(Row("Theme", _themeCombo));
        panel.Children.Add(_customColorsPanel);

        AddSlider(panel, "Widget size", _scaleLabel, _scaleSlider, topMargin: 12);
        AddSlider(panel, "Chips & alerts size", _chipScaleLabel, _chipScaleSlider,
            labelTip: "Spawn timer chips, mez chips, and the alert banner");
        AddSlider(panel, "Background see-through", _bgOpacityLabel, _bgOpacitySlider,
            "Only the dark panel fades — text stays sharp.");
        AddSlider(panel, "Whole-widget opacity", _opacityLabel, _opacitySlider,
            "Fades everything, text included.");

        _gridOverlayCheck = Check("▦ Grid overlay for aligning your game UI",
            _main.Settings.ShowGridOverlay, on =>
            {
                _main.Settings.ShowGridOverlay = on;
                _main.PersistSettings();
                ApplyGridOverlay?.Invoke(on);
            });
        panel.Children.Add(_gridOverlayCheck);
        panel.Children.Add(AppTheme.DimText(
            "A faint click-through grid over the whole desk — line up your game windows, then toggle it off here or in the right-click menu. Stronger lines every fourth square.",
            new Thickness(20, 2, 0, 0)));
        var spacingRow = Row("Grid spacing", _gridSpacingLabel);
        spacingRow.Margin = new Thickness(20, 4, 0, 0);
        panel.Children.Add(spacingRow);
        _gridSpacingSlider.Margin = new Thickness(20, 4, 0, 4);
        _gridSpacingSlider.Value = Math.Clamp(_main.Settings.GridSpacing, 16, 128);
        Subscribe(_gridSpacingSlider, () =>
        {
            _main.Settings.GridSpacing = _gridSpacingSlider.Value;
            _main.PersistSettings();
            ApplyGridSpacing?.Invoke();   // live while the grid is up
        });
        panel.Children.Add(_gridSpacingSlider);

        _cursorRingCheck = Check("Cursor ring (never lose your pointer)",
            _main.Settings.ShowCursorRing, on =>
            {
                _main.Settings.ShowCursorRing = on;
                _main.PersistSettings();
                ApplyCursorRing?.Invoke(on);
            });
        panel.Children.Add(_cursorRingCheck);
        panel.Children.Add(AppTheme.DimText(
            "A soft ring follows your mouse everywhere — click-through, over the game too. Drag its edge to resize it.",
            new Thickness(20, 2, 0, 0)));

        panel.Children.Add(AppTheme.DimText(
            "Size also scales all text. Changes apply instantly and are saved. Drag either side edge to widen this window.",
            new Thickness(0, 8, 0, 0)));

        foreach (var label in OptionsViewModel.ThemeLabels) _themeCombo.Items.Add(label);
        _themeCombo.SelectedIndex = _vm.ThemeIndex;
        _themeCombo.SelectionChanged += OnThemeChanged;
        UpdateCustomColorsPanel();

        _scaleSlider.Value = _vm.UiScale;
        _chipScaleSlider.Value = Math.Clamp(_vm.ChipScale, 0.8, 2.0);
        _opacitySlider.Value = _vm.Opacity;
        _bgOpacitySlider.Value = _vm.BackgroundOpacity;
        Subscribe(_scaleSlider, () => _main.SetUiScale(_scaleSlider.Value));
        Subscribe(_chipScaleSlider, () =>
        {
            _vm.ChipScale = _chipScaleSlider.Value;
            ApplyChipScale?.Invoke(_vm.ChipScale);
        });
        Subscribe(_bgOpacitySlider, () => _main.SetBackgroundOpacity(_bgOpacitySlider.Value));
        Subscribe(_opacitySlider, () => _main.SetWindowOpacity(_opacitySlider.Value));
    }

    // ---------------------------------------------------------------- Alerts & chips

    private void InitAlerts()
    {
        var panel = _alertsPanel;

        var soundRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        soundRow.Children.Add(_soundCombo);
        var test = AppTheme.IconButton("▶", "Play the alert sound");
        test.Margin = new Thickness(4, 0, 0, 0);
        test.Click += (_, _) => _main.PlayAlertSound();
        soundRow.Children.Add(test);
        panel.Children.Add(Row("Alert sound", soundRow));
        AddSlider(panel, "Alert volume", _alertVolumeLabel, _alertVolumeSlider, topMargin: 6, bottomMargin: 4);
        panel.Children.Add(_soundFileNote);
        panel.Children.Add(AppTheme.DimText(
            "While Options is open, the ★ alert banner tile is visible — drag it to where alerts should appear. During play it's click-through and never steals focus.",
            new Thickness(0, 4, 0, 0)));

        // Enumerated once per Options open — voices install with language packs, not
        // mid-session, and the SAPI walk isn't free. Read here rather than with the rest of
        // the population below because the dim line's wording depends on what came back.
        _installedVoices = SpokenAlerts.InstalledVoiceNames();

        // Speech gets its own volume: the slider above drives only the player that plays
        // sound files — the voice never saw it, so one slider claiming both would be a lie
        // in whichever direction it didn't reach.
        var voiceRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        voiceRow.Children.Add(_voiceCombo);
        // Linux has no voice at all (SpokenAlerts.Speak no-ops there), so a ▶ that plays
        // nothing would be the dishonest kind of button — it says so and stays dead.
        var canSpeak = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        var voiceTest = AppTheme.IconButton("▶", canSpeak
            ? "Hear a sample with the current voice, rate and volume"
            : "This build has no voice on Linux — there's nothing to play");
        voiceTest.Margin = new Thickness(4, 0, 0, 0);
        voiceTest.IsEnabled = canSpeak;
        voiceTest.Click += (_, _) => SpeakSample();
        voiceRow.Children.Add(voiceTest);
        panel.Children.Add(Row("Alert voice", voiceRow, new Thickness(0, 12, 0, 0)));
        panel.Children.Add(AppTheme.DimText(VoiceNote(), new Thickness(0, 2, 0, 0)));
        AddSlider(panel, "Speech rate", _speechRateLabel, _speechRateSlider, topMargin: 6, bottomMargin: 4);
        AddSlider(panel, "Speech volume", _speechVolumeLabel, _speechVolumeSlider, bottomMargin: 4);

        _slowAlertCheck = Check("Slow alert (an attack-speed debuff lands on you)",
            _main.Settings.SlowAlertEnabled, _ => SaveSlowAlert(), new Thickness(0, 12, 0, 0));
        panel.Children.Add(_slowAlertCheck);
        panel.Children.Add(AppTheme.DimText(
            "A 🐌 chip shows the slow's % and its counters; hover it for the cure line. A silent 40% slow quietly doubles a fight.",
            new Thickness(20, 2, 0, 0)));
        _slowSpokenCheck = Check("Speak it when it lands (\"Slowed 40 percent\")",
            _main.Settings.SlowAlertSpoken, _ => SaveSlowAlert(), new Thickness(20, 6, 0, 0));
        panel.Children.Add(_slowSpokenCheck);
        _slowRaidOnlyCheck = Check("Only during raids",
            _main.Settings.SlowAlertRaidOnly, _ => SaveSlowAlert(), new Thickness(20, 6, 0, 0));
        panel.Children.Add(_slowRaidOnlyCheck);
        panel.Children.Add(AppTheme.DimText(
            "Raids are detected from raid-channel chat — the log's only raid signal. A raid nobody has typed in for 10 minutes counts as over.",
            new Thickness(40, 2, 0, 0)));

        _buffExpiringOnlyCheck = Check("Buff timers: only show buffs about to fade",
            _main.Settings.BuffTimersExpiringOnly, _ => SaveBuffDisplay(), new Thickness(0, 12, 0, 0));
        panel.Children.Add(_buffExpiringOnlyCheck);
        var warnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 4, 0, 0) };
        warnRow.Children.Add(new TextBlock
        {
            Text = "warn at", FontSize = 12, Foreground = AppTheme.TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });
        _buffWarnBox.Margin = new Thickness(6, 0);
        _buffWarnBox.LostFocus += (_, _) => SaveBuffDisplay();
        warnRow.Children.Add(_buffWarnBox);
        warnRow.Children.Add(new TextBlock
        {
            Text = "seconds left", FontSize = 12, Foreground = AppTheme.TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });
        panel.Children.Add(warnRow);
        panel.Children.Add(AppTheme.DimText(
            "Unticked, the Buffs card counts down everything that's running. Ticked, it stays quiet (with an honest count) until a buff is inside the warning window — tell me when it matters. Your own casts already include your Spell Casting Reinforcement rank; a buff's first natural fade teaches its exact duration either way.",
            new Thickness(20, 2, 0, 0)));

        InitBuffSet(panel);

        _mezChipsCheck = Check("Mez countdown chips (who's asleep, wake-up timers)",
            _main.Settings.MezChipsEnabled, on =>
            {
                _main.Settings.MezChipsEnabled = on;
                _main.PersistSettings();
            }, new Thickness(0, 12, 0, 0));
        panel.Children.Add(_mezChipsCheck);
        panel.Children.Add(AppTheme.DimText(
            "Untick if your class never mezzes — the stack stops appearing entirely.",
            new Thickness(20, 2, 0, 0)));

        // Mez durations, the same contract spawn durations have: your number outranks
        // anything EQBuddy works out. Under the mez chips box because that is where
        // someone whose mez chip is wrong already goes.
        panel.Children.Add(new TextBlock
        {
            Text = "Mez durations", FontSize = 12, FontWeight = FontWeight.SemiBold,
            Foreground = AppTheme.AccentBrush, Margin = new Thickness(20, 12, 0, 0),
        });
        panel.Children.Add(AppTheme.DimText(
            EQBuddy.UI.Shared.MezDurationRows.Blurb, new Thickness(20, 2, 0, 4)));
        _mezDurationList.Margin = new Thickness(20, 0, 0, 0);
        panel.Children.Add(_mezDurationList);
        BuildMezDurations();

        _trackSpawnsCheck = Check("🕒 Track spawns (named respawn timers)",
            _main.Settings.TrackSpawns, on => _main.SetTrackSpawns(on));
        panel.Children.Add(_trackSpawnsCheck);
        panel.Children.Add(AppTheme.DimText(
            "Kill a named — or its placeholder — and a small countdown chicklet appears — a stopwatch, the name, and the time left (\"Asaka L`Rei 3:12\"). Chicklets stack, drag anywhere as one, show every timer you have running in any zone, and flip to DUE for a minute (click to dismiss sooner). Double-click one (or right-click → Spawn timers…) for the full zone list, which follows you zone to zone. We captured the respawn times we could from community sources — if you notice a discrepancy in game, type over the duration: your number wins and survives updates.",
            new Thickness(20, 2, 0, 0)));

        _spawnGrowUpCheck = Check("Spawn chips grow upward",
            _vm.SpawnChipsGrowUp, on => _vm.SpawnChipsGrowUp = on);
        panel.Children.Add(_spawnGrowUpCheck);
        _mezGrowUpCheck = Check("Mez chips grow upward",
            _vm.MezChipsGrowUp, on => _vm.MezChipsGrowUp = on, new Thickness(0, 4, 0, 0));
        panel.Children.Add(_mezGrowUpCheck);
        panel.Children.Add(AppTheme.DimText(
            "A stack normally holds its top edge and grows downward as chips arrive. Grown-upward it holds its BOTTOM edge instead — park boss timers above mez timers and each grows away from the other.",
            new Thickness(20, 2, 0, 0)));

        foreach (var choice in OptionsViewModel.SoundChoices) _soundCombo.Items.Add(choice);
        _soundCombo.SelectedIndex = _vm.SoundIndex;
        _soundCombo.SelectionChanged += OnSoundChanged;
        UpdateSoundFileNote();
        _alertVolumeSlider.Value = Math.Clamp(_main.Settings.AlertVolume, 0.1, 1.0);
        Subscribe(_alertVolumeSlider, () =>
        {
            _main.Settings.AlertVolume = _alertVolumeSlider.Value;
            _main.PersistSettings();
        });
        _alertVolumeLabel.Text = $"{_alertVolumeSlider.Value:P0}";

        foreach (var choice in OptionsViewModel.VoiceChoices(_installedVoices)) _voiceCombo.Items.Add(choice);
        _voiceCombo.SelectedIndex = _vm.VoiceIndex(_installedVoices);
        _voiceCombo.SelectionChanged += OnVoiceChanged;
        // Enumeration only works on Windows, so off it the picker holds nothing but
        // "System default" and a one-entry dropdown is not a choice. It stays visible and
        // disabled with the dim line above saying why — hiding it would leave someone
        // hunting for a control the screenshots show.
        _voiceCombo.IsEnabled = _installedVoices.Count > 0;
        _speechRateSlider.Value = _vm.SpeechRate;
        Subscribe(_speechRateSlider, () => _vm.SpeechRate = (int)Math.Round(_speechRateSlider.Value));
        _speechVolumeSlider.Value = _vm.SpeechVolume;
        Subscribe(_speechVolumeSlider, () => _vm.SpeechVolume = (int)Math.Round(_speechVolumeSlider.Value));
        _speechRateLabel.Text = _vm.SpeechRateLabel;
        _speechVolumeLabel.Text = _vm.SpeechVolumeLabel;
    }

    /// <summary>The dim line under the voice picker. The rate and volume sliders are always
    /// live — they're stored for whichever machine reads this settings.json — but which of
    /// the three the local platform actually honours differs, and saying so is cheaper than
    /// letting someone conclude the sliders are broken.</summary>
    private string VoiceNote() => _installedVoices.Count > 0
        ? "Used wherever EQBuddy speaks — watch rules with the S toggle, and the slow alert."
        : OperatingSystem.IsMacOS()
            ? "Used wherever EQBuddy speaks — watch rules with the S toggle, and the slow alert. "
              + "macOS speaks through `say`, which uses the voice, rate and volume from System "
              + "Settings → Spoken Content; the three below are saved but only take effect on Windows."
            : "Used wherever EQBuddy speaks — watch rules with the S toggle, and the slow alert. "
              + "There's no voice on Linux, so nothing here is spoken; the settings are still saved "
              + "for the Windows build reading the same settings.json.";

    private void OnVoiceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _voiceCombo.SelectedIndex < 0) return;
        _vm.SelectVoice(_installedVoices, _voiceCombo.SelectedIndex);
        SpeakSample();   // a voice choice you can hear, like the sound picker's instant play
    }

    /// <summary>Real alert text, × and all, so the sample demonstrates exactly what an alert
    /// will sound like (SpokenAlerts.Speakable rewrites the × for the voice).</summary>
    private static void SpeakSample() => SpokenAlerts.SpeakSample("Rusty Sword ×3");

    private void SaveSlowAlert()
    {
        _main.Settings.SlowAlertEnabled = _slowAlertCheck.IsChecked == true;
        _main.Settings.SlowAlertSpoken = _slowSpokenCheck.IsChecked == true;
        _main.Settings.SlowAlertRaidOnly = _slowRaidOnlyCheck.IsChecked == true;
        _main.PersistSettings();
    }

    private void SaveBuffDisplay()
    {
        if (!_ready) return;
        _main.Settings.BuffTimersExpiringOnly = _buffExpiringOnlyCheck.IsChecked == true;
        if (double.TryParse(_buffWarnBox.Text, out var seconds))
            _main.Settings.BuffWarnSeconds = Math.Clamp(seconds, 10, 3600);
        _buffWarnBox.Text = _main.Settings.BuffWarnSeconds.ToString("0");   // shows any clamp
        _main.PersistSettings();
    }

    // ---------------------------------------------------------------- Buff set (#120)
    //
    // Frankthetankk's missing line, stage 2: the set lives PER CLASS in
    // Settings.BuffSetsByClass (BuffSetStore owns the shape) and assembles from the
    // active class combination plus the "(any class)" bucket, so swapping Warrior for
    // Rogue keeps the other classes' picks untouched. This editor shows every bucket —
    // active or parked — because it is the one place a stored pick can always be
    // removed. Every edit repaints the card and the breakout through BuffSetEdited.

    private void InitBuffSet(Panel panel)
    {
        panel.Children.Add(Heading("Buff set — the missing line", new Thickness(0, 14, 0, 2)));
        panel.Children.Add(AppTheme.DimText(
            "Pick the buffs this character never camps without — per class: each pick lands in a class bucket, and the live set assembles from the classes you're running plus (any class), so swapping one class keeps the other classes' picks. The ⏳ Buffs card grows one line ONLY when something's off: missing (seen fading, or its timer ran out), expiring (inside the warn window above), or not seen (no landing line this session — it may be up from before EQBuddy was watching; the log can't tell, so it's shown as its own honest state). Everything up = no line at all. You build the list yourself; nothing is ever added for you."));
        panel.Children.Add(_buffSetCharNote);
        panel.Children.Add(_buffSetPanel);

        // The add box targets a class bucket. The FULL class list is offered here (unlike
        // the breakout's active-only list) so a swap can be configured in advance.
        var addRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        addRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        addRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ToolTip.SetTip(_buffSetClassBox,
            "Which class bucket the next pick goes into — (any class) applies whatever combination you run");
        addRow.Children.Add(_buffSetClassBox);
        Grid.SetColumn(_buffSetAddBox, 1);
        addRow.Children.Add(_buffSetAddBox);
        panel.Children.Add(addRow);
        panel.Children.Add(_buffSetPopup);

        // Same chrome as the recent-log-lines picker: a bordered popup over a ListBox, so
        // both "box with a dropdown of matches" in this window look and dismiss alike.
        _buffSetMatches.Background = AppTheme.PopupBrush;
        _buffSetMatches.Foreground = AppTheme.TextBrush;
        _buffSetMatches.SelectionChanged += OnBuffSetMatchPicked;
        _buffSetPopup.Child = new Border
        {
            Background = AppTheme.PopupBrush,
            BorderBrush = AppTheme.AccentBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(2),
            Child = _buffSetMatches,
        };
        // The property change, not TextBox.TextChanged: that one only fires for edits the
        // user typed, and the pick handler clears the box in code — the popup would be left
        // hanging open over the panel it just repainted.
        _buffSetAddBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty) OnBuffSetSearchChanged();
        };

        BuildBuffSetPanel();
    }

    /// <summary>The breakout editor writes the same storage; MainWindow calls this so its
    /// edits appear here immediately too.</summary>
    internal void RefreshBuffSetEditor() => BuildBuffSetPanel();

    private void BuildBuffSetPanel()
    {
        var (key, character, classes, picked) = BuffSetWho();
        _buffSetCharNote.Text = key.Length > 0
            ? $"Saved for {character}, per class — the live set is (any class) plus "
              + (classes.Count > 0
                  ? $"{string.Join(", ", classes.Select(QuestClassFilter.Abbrev))} "
                    + (picked
                        ? "(picked in the Quest Tracker)."
                        : "(inferred from your combat log — pick classes in the Quest Tracker to override).")
                  : "your classes — none known yet: pick them in the Quest Tracker, or use (any class).")
            : "No character detected yet — once today's log names one, reopen Options and the editor unlocks.";
        _buffSetAddBox.IsEnabled = key.Length > 0;
        _buffSetClassBox.IsEnabled = key.Length > 0;
        RefreshBuffSetClassChoices();
        _buffSetPanel.Children.Clear();
        if (key.Length == 0) return;

        var stored = _main.Settings.BuffSetsByClass.GetValueOrDefault(key);
        // Active buckets first, in assembly order; then parked ones (stored picks whose
        // class isn't in the current combination) — visible and editable, so a swap never
        // strands a pick out of reach. That parked picks SURVIVE the swap is the
        // requester's whole design, and hiding them would make it look like a lie.
        var active = BuffSetStore.Sections(stored, classes);
        var sections = active
            .Where(sec => sec.Spells.Count > 0)
            .Select(sec => (sec.Class, Spells: sec.Spells, Parked: false))
            .ToList();
        var activeNames = active.Select(sec => sec.Class).ToHashSet(StringComparer.OrdinalIgnoreCase);
        sections.AddRange(BuffSetStore.StoredClasses(stored)
            .Where(c => !activeNames.Contains(c))
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Select(c => (Class: c, Spells: (IReadOnlyList<string>)BuffSetStore.SpellsFor(stored, c),
                Parked: true)));
        if (sections.Count == 0)
        {
            _buffSetPanel.Children.Add(AppTheme.DimText(
                "Nothing picked yet — pick a class bucket and search below to build the set.",
                new Thickness(0, 2, 0, 0)));
            return;
        }

        foreach (var (cls, spells, parked) in sections)
        {
            _buffSetPanel.Children.Add(new TextBlock
            {
                Text = cls + (parked ? "  · not in your current classes — kept for the swap back" : ""),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = parked ? AppTheme.DimBrush : AppTheme.AccentBrush,
            });
            foreach (var spell in spells)
            {
                var row = new Grid { Margin = new Thickness(6, 2, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                row.Children.Add(new TextBlock
                {
                    Text = spell,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = parked ? AppTheme.DimBrush : AppTheme.TextBrush,
                });
                var remove = AppTheme.IconButton("✕", $"Remove {spell} from {cls}");
                remove.FontSize = 11;
                remove.Margin = new Thickness(4, 0, 0, 0);
                var (doomedClass, doomed) = (cls, spell);
                remove.Click += (_, _) =>
                {
                    BuffSetStore.Remove(_main.Settings.BuffSetsByClass, key, doomedClass, doomed);
                    SaveBuffSetEdit();
                };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                _buffSetPanel.Children.Add(row);
            }
        }
    }

    /// <summary>Add-target buckets: "(any class)" plus the FULL class list — unlike the
    /// breakout's active-only list, so a coming swap can be configured here in advance.
    /// Selection survives rebuilds.</summary>
    private void RefreshBuffSetClassChoices()
    {
        var keep = _buffSetClassBox.SelectedItem as string;
        if (_buffSetClassBox.Items.Count == 0)
        {
            _buffSetClassBox.Items.Add(BuffSetStore.AnyClass);
            foreach (var cls in QuestClassFilter.Classes) _buffSetClassBox.Items.Add(cls);
        }
        _buffSetClassBox.SelectedItem = keep ?? BuffSetStore.AnyClass;
    }

    private string SelectedBuffSetClass =>
        _buffSetClassBox.SelectedItem as string ?? BuffSetStore.AnyClass;

    /// <summary>WPF's MainWindow.BuffSetKey/BuffSetCharacterName/BuffSetClassSource, read
    /// off the surface MainWindow already publishes: the Quest Tracker's picked classes,
    /// falling back to the combat-inferred one — the Gear Locker rule (#104). No /who
    /// parsing exists in the log pipeline, so this is the honest signal the app already
    /// has, and the note above says which of the two it came from.</summary>
    /// Reads MainWindow's own answer rather than recomputing one: all three surfaces
    /// (this editor, the Buffs card, the ⏳ breakout) must agree on one combination,
    /// and two implementations of "which classes" is how they'd stop agreeing.
    private (string Key, string Character, IReadOnlyList<string> Classes, bool Picked) BuffSetWho()
    {
        var (classes, picked) = _main.BuffSetClassSource(_main.CurrentSnapshot());
        return (_main.BuffSetKey, _main.BuffSetCharacterName, classes, picked);
    }

    private void SaveBuffSetEdit()
    {
        _main.PersistSettings();
        BuildBuffSetPanel();
        _main.OnBuffSetEdited();   // card + breakout; calls RefreshBuffSetEditor back, harmlessly
    }

    private void OnBuffSetSearchChanged()
    {
        if (!_ready) return;
        var query = (_buffSetAddBox.Text ?? "").Trim();
        if (query.Length < 2) { _buffSetPopup.IsOpen = false; return; }
        _buffSetMatches.Items.Clear();

        // Seen first (the buffs this player demonstrably casts), then the whole buff
        // catalog — BuffSetSearch, shared with WPF and the breakout editor. Both draw from
        // BuffDurationCatalog's attributable spells, so nothing can be added that would sit
        // at "not seen" forever. Only the TARGET bucket's picks are excluded: the same buff
        // under another class is a legitimate pick, and assembly dedups it anyway.
        var inBucket = BuffSetStore.SpellsFor(
            _main.Settings.BuffSetsByClass.GetValueOrDefault(BuffSetWho().Key), SelectedBuffSetClass);
        foreach (var (spell, seen) in BuffSetSearch.Rank(query, _main.SeenBuffCasts(),
                     inBucket, BuffDurationCatalog.Default.SpellNames))
            _buffSetMatches.Items.Add(new ListBoxItem
            {
                Content = seen ? spell + "   · seen this session" : spell,
                Tag = spell,
            });
        if (_buffSetMatches.Items.Count == 0)
            _buffSetMatches.Items.Add(new ListBoxItem
            {
                Content = "No buff in the catalog matches — check the spelling?",
                IsEnabled = false,
            });
        _buffSetPopup.IsOpen = true;
    }

    private void OnBuffSetMatchPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _buffSetMatches.SelectedItem is not ListBoxItem { Tag: string spell }) return;
        _buffSetPopup.IsOpen = false;
        _buffSetMatches.SelectedItem = null;
        if (BuffSetWho().Key is not { Length: > 0 } key) return;
        BuffSetStore.Add(_main.Settings.BuffSetsByClass, key, SelectedBuffSetClass, spell);
        _buffSetAddBox.Text = "";   // TextChanged with an empty box closes the popup
        SaveBuffSetEdit();
    }

    // ---------------------------------------------------------------- Watch rules

    private void InitWatch()
    {
        var panel = _watchPanel;
        panel.Children.Add(AppTheme.DimText(
            "Watch loot, kills, skill-ups, deaths, milestones, your spells wearing off, or any text in the log. Match is a case-insensitive substring, e.g. 'mote'. Spell fade rules can pick a whole class (Any crowd control, Charm, Mez, Root, Lull, Stun, HoT) instead of a named spell, needing no match text. Delay holds the alert back that many seconds so it lands as a cue. 🔔 shows a banner; the sound box picks this rule's own sound, so you can tell what happened by ear — 'Default' follows the shared choice on the Alerts tab, 'Off' stays silent, 'Custom…' takes your own .wav/.mp3."));

        // Collapsed by default — the examples answer the questions people actually ask, and
        // are noise for anyone who already knows the answers.
        _guideToggle.Click += (_, _) => ToggleGuide(!_guidePanel.IsVisible, persist: true);
        panel.Children.Add(_guideToggle);
        panel.Children.Add(_guidePanel);

        panel.Children.Add(_rulesPanel);
        var addRow = new StackPanel { Orientation = Orientation.Horizontal };
        var add = AppTheme.ActionButton("+ Add watch rule");
        add.Click += (_, _) =>
        {
            _vm.AddRule();
            BuildRulesEditor();
        };
        addRow.Children.Add(add);
        var addFromLog = AppTheme.ActionButton("+ From a recent log line…",
            "Pick a line that just happened in your log — it becomes a Text rule, no typing. Edit the match afterward to taste.");
        addFromLog.Margin = new Thickness(8, 0, 0, 0);
        addFromLog.Click += (_, _) => OpenRecentLinesPicker();
        _recentLinesPopup.PlacementTarget = addFromLog;
        addRow.Children.Add(addFromLog);
        addRow.Children.Add(_recentLinesPopup);
        panel.Children.Add(addRow);
        BuildRecentLinesPopup();

        var importRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        importRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        importRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        importRow.Children.Add(_importBox);
        var import = AppTheme.ActionButton("Import…",
            "Preview what the pasted share string would add — nothing is added until you confirm");
        import.Margin = new Thickness(6, 0, 0, 0);
        import.Click += (_, _) => PreviewImport();
        Grid.SetColumn(import, 1);
        importRow.Children.Add(import);
        panel.Children.Add(importRow);
        _importPreview.IsVisible = false;
        panel.Children.Add(_importPreview);
        _importConfirmBtn.IsVisible = false;
        _importConfirmBtn.HorizontalAlignment = HorizontalAlignment.Left;
        _importConfirmBtn.Margin = new Thickness(0, 4, 0, 0);
        _importConfirmBtn.Click += (_, _) => ConfirmImport();
        panel.Children.Add(_importConfirmBtn);

        _pinChipsCheck = Check("📌 Show watch chips in the mini dashboard",
            _vm.PinWatchChips, on => _vm.PinWatchChips = on, new Thickness(0, 6, 0, 0));
        panel.Children.Add(_pinChipsCheck);

        BuildRulesEditor();
    }

    /// <summary>Show or hide the worked examples, remembering the choice. Built on first
    /// expand rather than up front — most people never open it.</summary>
    private void ToggleGuide(bool open, bool persist)
    {
        _guidePanel.IsVisible = open;
        _guideToggle.Content = open ? "▾ Hide examples" : "▸ Show examples";
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
            panel.Children.Add(Line("• " + basic, AppTheme.DimBrush, 2));

        panel.Children.Add(Line("Examples", AppTheme.AccentBrush, 8, bold: true));
        foreach (var ex in WatchGuide.Examples)
        {
            var match = ex.Match.Length > 0 ? $"Match \"{ex.Match}\"" : "no match text";
            var delay = ex.Delay.Length > 0 ? $" · Delay {ex.Delay}" : "";
            panel.Children.Add(Line(
                $"{OptionsViewModel.KindNames[(int)ex.Kind]} · \"{ex.Name}\" · {match}{delay}",
                AppTheme.TextBrush, 8));
            panel.Children.Add(Line(ex.What, AppTheme.DimBrush, 1));
        }
        return panel;
    }

    // ---- "that thing that just happened — alert on it" (Companion-parity idea) ----

    private void BuildRecentLinesPopup()
    {
        _recentHideChat.Content = new TextBlock
        {
            Text = "Hide chat (say/tell/channels) — combat and system lines only",
            FontSize = 11,
            Foreground = AppTheme.DimBrush,
        };
        _recentHideChat.IsCheckedChanged += (_, _) =>
        {
            if (!_ready) return;
            _main.Settings.RecentLinesHideChat = _recentHideChat.IsChecked == true;
            _main.PersistSettings();
            FillRecentLines();
        };
        _recentLinesList.Background = AppTheme.PopupBrush;
        _recentLinesList.Foreground = AppTheme.TextBrush;
        _recentLinesList.SelectionChanged += OnRecentLinePicked;
        _recentLinesPopup.Child = new Border
        {
            Background = AppTheme.PopupBrush,
            BorderBrush = AppTheme.AccentBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(2),
            Child = new StackPanel { Children = { _recentHideChat, _recentLinesList } },
        };
    }

    private void OpenRecentLinesPicker()
    {
        var ready = _ready;
        _ready = false;
        _recentHideChat.IsChecked = _main.Settings.RecentLinesHideChat;
        _ready = ready;
        FillRecentLines();
        _recentLinesPopup.IsOpen = true;
    }

    private void FillRecentLines()
    {
        var lines = RecentLinesSource?.Invoke() ?? [];
        _recentLinesList.Items.Clear();
        var hideChat = _recentHideChat.IsChecked == true;
        // Newest first: "just happened" is the whole point of the picker.
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var (time, message) = lines[i];
            // Chat lines quote their body (", '") — a busy General channel drowns the
            // combat lines the picker exists for (David's field note), but chat stays
            // one untick away: a "WTS" watch is a legitimate rule too.
            if (hideChat && message.Contains(", '", StringComparison.Ordinal)) continue;
            var item = new ListBoxItem
            {
                Content = $"{time:HH:mm:ss}  {(message.Length <= 96 ? message : message[..95] + "…")}",
                Tag = message,
            };
            ToolTip.SetTip(item, message);
            _recentLinesList.Items.Add(item);
        }
        if (_recentLinesList.Items.Count == 0)
            _recentLinesList.Items.Add(new ListBoxItem
            {
                Content = hideChat ? "Nothing but chat seen lately — untick the filter."
                    : "No log lines seen yet — play a little first.",
                IsEnabled = false,
            });
    }

    private void OnRecentLinePicked(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _recentLinesList.SelectedItem is not ListBoxItem { Tag: string message }) return;
        _recentLinesPopup.IsOpen = false;
        _recentLinesList.SelectedItem = null;
        var rule = _vm.AddRule();
        rule.Kind = WatchKind.Text;
        rule.Pattern = message;
        rule.Name = message.Length <= 28 ? message : message[..27] + "…";
        _vm.Persist();
        BuildRulesEditor();
    }

    // ---- share-string import: paste → preview → confirm (nothing lands unseen) ----

    private void PreviewImport()
    {
        _pendingImport = WatchRuleShare.TryDecode(_importBox.Text ?? "", out var error);
        _importPreview.IsVisible = true;
        if (_pendingImport is null)
        {
            _importPreview.Text = error;
            _importConfirmBtn.IsVisible = false;
            return;
        }
        _importPreview.Text = "This will add:\n" +
            string.Join("\n", _pendingImport.Select(r => "  • " + WatchRuleShare.Describe(r)));
        _importConfirmBtn.Content = _pendingImport.Count == 1
            ? "✔ Add this rule" : $"✔ Add these {_pendingImport.Count} rules";
        _importConfirmBtn.IsVisible = true;
    }

    private void ConfirmImport()
    {
        if (_pendingImport is null) return;
        _vm.ImportRules(_pendingImport);
        _pendingImport = null;
        _importBox.Text = "";
        _importPreview.IsVisible = false;
        _importConfirmBtn.IsVisible = false;
        BuildRulesEditor();
    }

    /// <summary>Column layout for the header and every rule row. Name and match text
    /// share the free width, so widening the window grows the fields that actually hold
    /// free text.</summary>
    private static Grid RuleGrid()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(92)));         // kind
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.4, GridUnitType.Star)));
        for (var i = 0; i < 10; i++)   // pin, banner, color, speech, phrase, sound, delay, share, delete, arrange
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        return grid;
    }

    private void BuildRulesEditor()
    {
        _rulesPanel.Children.Clear();

        var header = RuleGrid();
        header.Margin = new Thickness(0, 2, 0, 2);
        foreach (var (text, column) in (ReadOnlySpan<(string, int)>)
            [("Watch", 0), ("Name", 1), ("Match", 2), ("Delay", 9)])
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 10,
                Opacity = 0.7,
                Foreground = AppTheme.TextBrush,
                Margin = new Thickness(column == 0 ? 0 : 6, 0, 0, 0),
            };
            Grid.SetColumn(label, column);
            header.Children.Add(label);
        }
        _rulesPanel.Children.Add(header);

        foreach (var rule in _vm.Rules)
        {
            var row = RuleGrid();
            row.Margin = new Thickness(0, 3, 0, 0);

            var kind = new ComboBox { FontSize = 11, Margin = new Thickness(0, 0, 4, 0) };
            foreach (var k in OptionsViewModel.KindNames) kind.Items.Add(k);
            kind.SelectedIndex = (int)rule.Kind;
            ToolTip.SetTip(kind, "What this rule watches");
            row.Children.Add(kind);

            var name = DarkBox(rule.Name, "Display name (also used as match text when the optional filter is empty)");
            name.PlaceholderText = "Display name";
            name.Margin = new Thickness(0, 0, 4, 0);
            name.LostFocus += (_, _) => { rule.Name = (name.Text ?? "").Trim(); _vm.Persist(); };
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
                "Spell class: watch one named spell (\"By name\" + match text), " +
                "or a whole class — Charm, Mez, HoT… — with no match text needed");
            matchArea.Children.Add(spellFilter);

            const string patternTip =
                "Optional case-insensitive match text; uses the display name when empty, and may be empty for Death or Milestone";
            var pattern = DarkBox(rule.Pattern, patternTip);
            pattern.PlaceholderText = "Match text (optional)";
            pattern.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(pattern, 1);
            matchArea.Children.Add(pattern);

            // Regex mode (#83) — same semantics as WPF: invalid patterns match
            // nothing, and the Match box's tooltip carries the compiler's complaint.
            void ShowRegexState() => ToolTip.SetTip(pattern,
                rule.RegexError is { } err ? $"Regex error: {err}" : patternTip);
            matchArea.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var regexToggle = RuleToggle(".*",
                "Treat Match as a regular expression (.NET syntax, case-insensitive). " +
                "An invalid pattern matches nothing — the Match box's tooltip shows the error.",
                2, rule.UseRegex, v => { rule.UseRegex = v; ShowRegexState(); });
            matchArea.Children.Add(regexToggle);
            pattern.LostFocus += (_, _) =>
            {
                rule.Pattern = (pattern.Text ?? "").Trim();
                ShowRegexState();
                _vm.Persist();
            };
            ShowRegexState();

            var spellName = new AutoCompleteBox
            {
                Text = rule.Pattern,
                ItemsSource = FadeMessageCatalog.Default.BuffSpellChoices,
                FilterMode = AutoCompleteFilterMode.ContainsOrdinal,
                MinimumPrefixLength = 0,
                IsTextCompletionEnabled = true,
                PlaceholderText = "Buff/spell name",
                FontSize = 12,
                Margin = new Thickness(0, 0, 4, 0),
                MinWidth = 120,
                MaxDropDownHeight = 260,
            };
            ToolTip.SetTip(spellName,
                "Start typing a known buff/spell fade, then pick one. Free typing still works.");
            spellName.TextChanged += (_, _) =>
            {
                if (!_ready) return;
                rule.Pattern = (spellName.Text ?? "").Trim();
                pattern.Text = rule.Pattern;
                _vm.Persist();
            };
            spellName.SelectionChanged += (_, _) =>
            {
                if (!_ready || spellName.SelectedItem is not string picked) return;
                rule.Pattern = picked;
                spellName.Text = picked;
                pattern.Text = picked;
                _vm.Persist();
            };
            Grid.SetColumn(spellName, 1);
            matchArea.Children.Add(spellName);

            void SyncMatchArea()
            {
                var isFade = rule.Kind == WatchKind.SpellFade;
                var byName = rule.SpellFilter == SpellFilter.ByName;
                spellFilter.IsVisible = isFade;
                pattern.IsVisible = !isFade;
                regexToggle.IsVisible = !isFade;   // pairs with the free-text box only
                spellName.IsVisible = isFade && byName;
                Grid.SetColumnSpan(spellFilter, isFade && !byName ? 2 : 1);
                if (isFade && byName) spellName.Text = rule.Pattern;
                else pattern.Text = rule.Pattern;
            }
            SyncMatchArea();

            kind.SelectionChanged += (_, _) =>
            {
                if (!_ready || kind.SelectedIndex < 0) return;
                rule.Kind = (WatchKind)kind.SelectedIndex;
                SyncMatchArea();
                _vm.Persist();
            };
            spellFilter.SelectionChanged += (_, _) =>
            {
                if (!_ready || spellFilter.SelectedIndex < 0) return;
                rule.SpellFilter = (SpellFilter)spellFilter.SelectedIndex;
                SyncMatchArea();
                _vm.Persist();
            };

            row.Children.Add(RuleToggle("📌", "Show this rule as a chip in the mini dashboard", 3,
                rule.Pinned, v => rule.Pinned = v));
            row.Children.Add(RuleToggle("🔔", "Banner alert on match", 4, rule.AlertBanner, v => rule.AlertBanner = v));

            // Banner color: one small dot cycling the palette on click (Chaosrah's
            // color-coded alerts) — a combo box would not fit the row.
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
                ToolTip.SetTip(colorDot, $"Banner color: {choice} — click to change");
            }
            PaintDot();
            colorDot.Click += (_, _) =>
            {
                var next = (AlertColors.IndexOf(rule.AlertColor) + 1) % AlertColors.Choices.Length;
                var picked = AlertColors.Choices[next].Name;
                rule.AlertColor = picked == "Default" ? "" : picked;
                PaintDot();
                _vm.Persist();
            };
            Grid.SetColumn(colorDot, 5);
            row.Children.Add(colorDot);

            // Custom spoken phrase, beside the S toggle and only while it's on — a phrase
            // box on a rule that never speaks is dead weight in a tight row. Empty speaks
            // the alert's own label, exactly as before the box existed.
            var phrase = DarkBox(rule.SpokenPhrase,
                "What the voice says for this rule (empty = the alert text itself).\n" +
                "Say the instruction, not the event: \"Recast charm now\" instead of\n" +
                "\"Befriend Animal faded off a bear\".");
            phrase.Width = 76;
            phrase.FontSize = 11;
            phrase.Margin = new Thickness(0, 0, 4, 0);
            phrase.IsVisible = rule.AlertSpeech;
            phrase.LostFocus += (_, _) => { rule.SpokenPhrase = (phrase.Text ?? "").Trim(); _vm.Persist(); };
            Grid.SetColumn(phrase, 7);

            row.Children.Add(RuleToggle("S",
                "Speak this alert with the system voice (Windows and macOS; silent on Linux)", 6,
                rule.AlertSpeech, v => { rule.AlertSpeech = v; phrase.IsVisible = v; }));
            row.Children.Add(phrase);

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
                : "Sound for this rule — pick a different one per rule to tell them apart by ear");
            sound.SelectionChanged += async (_, _) =>
            {
                if (!_ready || sound.SelectedIndex < 0) return;
                if (AlertSoundCatalog.ApplyRuleChoice(rule, sound.SelectedIndex))
                {
                    var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = $"Choose a sound for \"{(rule.Name.Length > 0 ? rule.Name : rule.Pattern)}\"",
                        AllowMultiple = false,
                        FileTypeFilter = [new FilePickerFileType("Sound files") { Patterns = AlertSoundFormats.Patterns }],
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
                _vm.Persist();
                // Play it straight away, so picking a sound is a decision you can hear.
                if (AlertSoundCatalog.Resolve(rule, _main.Settings.AlertSound) is { } preview)
                    _main.PlayAlertSound(preview);
            };
            Grid.SetColumn(sound, 8);
            row.Children.Add(sound);

            // Seconds to hold the alert back — 0 (or empty) is the immediate behaviour.
            // Turns a rule into a cue: sound 2.5 s after a heal-chain call to say "cast
            // now", or 25 s after a mez to say "recast before it breaks".
            var delay = DarkBox(DelayText.Format(rule.AlertDelaySeconds),
                "Wait this long before alerting (empty = at once, up to 30 minutes).\n" +
                "Seconds by default; add m for minutes — 2.5, 25, 8m, 1:30.\n" +
                "Use it as a cue: 2.5 after a heal-chain call, 25 into a 30s mez,\n" +
                "or 8m for a respawn. The count updates immediately either way.");
            delay.PlaceholderText = "0s";
            delay.Width = 48;
            delay.Margin = new Thickness(0, 0, 4, 0);
            delay.TextAlignment = global::Avalonia.Media.TextAlignment.Right;
            delay.LostFocus += (_, _) =>
            {
                rule.AlertDelaySeconds = DelayText.Parse(delay.Text);
                delay.Text = DelayText.Format(rule.AlertDelaySeconds);
                _vm.Persist();
            };
            Grid.SetColumn(delay, 9);
            row.Children.Add(delay);

            // Share: the rule as a guild-chat string (WatchRuleShare). The ✓ flash is
            // the only feedback a clipboard write can honestly give.
            var share = AppTheme.IconButton("⤴",
                "Copy this rule as a share string — paste it in guild chat or Discord,\n" +
                "and any EQBuddy imports it from the box below the rule list");
            share.Click += async (_, _) =>
            {
                try
                {
                    if (Clipboard is { } clipboard)
                        await clipboard.SetTextAsync(WatchRuleShare.Encode([rule]));
                }
                catch (Exception ex) { App.LogError(ex); return; }
                share.Content = "✓";
                var revert = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                revert.Tick += (_, _) => { share.Content = "⤴"; revert.Stop(); };
                revert.Start();
            };
            Grid.SetColumn(share, 10);
            row.Children.Add(share);

            var del = AppTheme.IconButton("✕", "Delete rule");
            del.Click += (_, _) =>
            {
                _vm.RemoveRule(rule);
                BuildRulesEditor();
            };
            Grid.SetColumn(del, 11);
            row.Children.Add(del);

            // Arrange (#105, wizen): this order IS the Tracked card's "manual" sort.
            // Stacked ▲▼ in one cell — precise where drag would fight the text boxes.
            var arrange = new StackPanel { Margin = new Thickness(2, 0, 0, 0) };
            foreach (var (glyph, delta) in (ReadOnlySpan<(string, int)>)[("▲", -1), ("▼", +1)])
            {
                var move = AppTheme.IconButton(glyph,
                    "Move this rule " + (delta < 0 ? "up" : "down") +
                    " — the watch display's \"manual\" sort follows this order");
                move.FontSize = 7;
                move.MinHeight = 12;
                move.Padding = new Thickness(2, 0);
                var d = delta;
                move.Click += (_, _) => { _vm.MoveRule(rule, d); BuildRulesEditor(); };
                arrange.Children.Add(move);
            }
            Grid.SetColumn(arrange, 12);
            row.Children.Add(arrange);

            _rulesPanel.Children.Add(row);
        }
    }

    private ToggleButton RuleToggle(string glyph, string tip, int column, bool initial, Action<bool> apply)
    {
        var t = AppTheme.IconToggle(glyph, tip);
        t.FontSize = 11;
        t.IsChecked = initial;
        t.IsCheckedChanged += (_, _) =>
        {
            apply(t.IsChecked == true);
            _vm.Persist();
        };
        Grid.SetColumn(t, column);
        return t;
    }

    // ---------------------------------------------------------------- Cards & windows

    private void InitCards()
    {
        var panel = _cardsTabPanel;
        panel.Children.Add(Heading("Overlay cards", new Thickness(0, 0, 0, 2)));
        panel.Children.Add(AppTheme.DimText(
            "Every card you leave visible shows on the widget — one with nothing yet says so in a line and fills in as it happens.",
            new Thickness(0, 0, 0, 2)));

        panel.Children.Add(Heading("Gear checklist", new Thickness(0, 12, 0, 2)));
        panel.Children.Add(AppTheme.DimText(
            "Import the exported shopping-list HTML from EQ Legends Tools, then show it as a checklist in the Gear overlay card."));
        var gearRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var gearTools = AppTheme.ActionButton("Open EQ Legends Tools",
            "Open the EQ Legends Tools character sheet/shopping-list page");
        gearTools.Click += (_, _) => OpenGearTools();
        gearRow.Children.Add(gearTools);
        var gearImport = AppTheme.ActionButton("Import gear list...");
        gearImport.Margin = new Thickness(6, 0, 0, 0);
        gearImport.Click += async (_, _) => await ImportGearChecklist();
        gearRow.Children.Add(gearImport);
        _gearClearBtn.Margin = new Thickness(6, 0, 0, 0);
        _gearClearBtn.Click += (_, _) => ClearGearChecklist();
        gearRow.Children.Add(_gearClearBtn);
        panel.Children.Add(gearRow);
        panel.Children.Add(_gearImportStatus);
        UpdateGearImportStatus();

        panel.Children.Add(_cardsPanel);
        BuildCardsEditor();

        panel.Children.Add(Heading("Breakout windows", new Thickness(0, 14, 0, 2)));
        panel.Children.Add(AppTheme.DimText(
            "Which floating windows may open while minimized (each still needs its ⭐ star — or a 📌 pinned rule for Watch). Unticking one here is the same as its ✕.",
            new Thickness(0, 0, 0, 2)));
        panel.Children.Add(_breakoutsPanel);
        BuildBreakoutChecks();

        _targetDropsCheck = Check("🎯 Show target drops in the Loot card",
            _vm.ShowTargetDrops, on => _vm.ShowTargetDrops = on, new Thickness(0, 12, 0, 0));
        panel.Children.Add(_targetDropsCheck);
        panel.Children.Add(AppTheme.DimText(
            "While you fight, the Loot card lists what the creature can drop (eqlwiki) with your own observed counts this session. Hover an item for its stats; click for full info.",
            new Thickness(20, 2, 0, 0)));

        panel.Children.Add(Row("Recent-rate window", _windowCombo, new Thickness(0, 12, 0, 0)));
        panel.Children.Add(AppTheme.DimText("The \"Last Xm\" figures on Combat, Kills, Money, and Progress."));
        foreach (var choice in OptionsViewModel.WindowChoices) _windowCombo.Items.Add(choice);
        _windowCombo.SelectedIndex = _vm.RecentWindowIndex;
        _windowCombo.SelectionChanged += (_, _) =>
        {
            if (_ready) _vm.RecentWindowIndex = _windowCombo.SelectedIndex;
        };
    }

    private void BuildCardsEditor()
    {
        _cardsPanel.Children.Clear();
        foreach (var card in _vm.Cards)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var i = 0; i < 3; i++) row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            // Since 1.66.3 every unhidden card shows (with an empty state when it has
            // nothing yet) — Options is the whole truth, no self-hiding asterisks.
            row.Children.Add(new TextBlock
            {
                Text = card.Title,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = card.Hidden ? AppTheme.DimBrush : AppTheme.TextBrush,
            });
            row.Children.Add(CardButton("↑", "Move up", 1, () => { _vm.MoveCard(card.Key, -1); ApplyCards(); }));
            row.Children.Add(CardButton("↓", "Move down", 2, () => { _vm.MoveCard(card.Key, +1); ApplyCards(); }));
            row.Children.Add(CardButton(card.Hidden ? "🙈" : "👁",
                card.Hidden ? "Show card" : "Hide card (data still collected)", 3,
                () => { _vm.ToggleCard(card.Key); ApplyCards(); }));
            _cardsPanel.Children.Add(row);

            // "Money · Motes · Faction · Raids are tabs in here now" — #219. A fold is
            // invisible by construction: the row that would have told you where a card
            // went is the row that was removed, and this is the screen someone opens when
            // a card is missing. Same words as the WPF twin, from the same shared table.
            if (card.Absorbed is { } absorbed)
            {
                _cardsPanel.Children.Add(new TextBlock
                {
                    Text = absorbed,
                    FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Metadata).Size,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 2),
                    Foreground = AppTheme.DimBrush,
                });
            }
        }
    }

    private Button CardButton(string text, string tip, int column, Action action)
    {
        var b = AppTheme.IconButton(text, tip);
        b.FontSize = 11;
        b.Margin = new Thickness(6, 0, 0, 0);
        b.VerticalAlignment = VerticalAlignment.Center;
        b.Click += (_, _) => action();
        Grid.SetColumn(b, column);
        return b;
    }

    private void ApplyCards()
    {
        _main.ApplySectionLayout();
        BuildCardsEditor();
    }

    /// <summary>One checkbox per breakout kind — the re-enable path for a window that was
    /// ✕-closed (discussion #45: the star should keep its chip without forcing the
    /// window). Labels are keyed by name so this stays correct as the Avalonia
    /// BreakoutKind catches up to WPF's (Watch, Loot).</summary>
    private void BuildBreakoutChecks()
    {
        _breakoutsPanel.Children.Clear();
        foreach (var kind in Enum.GetValues<BreakoutKind>())
        {
            var name = kind.ToString();
            var check = new CheckBox
            {
                IsChecked = !_main.Settings.DisabledBreakouts.Contains(name),
                Margin = new Thickness(0, 2, 14, 0),
                Content = new TextBlock
                {
                    Text = name switch
                    {
                        "Damage" => "⚔ Damage",
                        "Healing" => "⚕ Healing",
                        "Pet" => "🐾 Pet",
                        "Watch" => "🎯 Watch",
                        _ => "🎒 Loot",
                    },
                    FontSize = 12,
                    Foreground = AppTheme.TextBrush,
                },
            };
            check.IsCheckedChanged += (_, _) =>
            {
                if (!_ready) return;
                if (check.IsChecked == true) _main.Settings.DisabledBreakouts.Remove(name);
                else if (!_main.Settings.DisabledBreakouts.Contains(name))
                    _main.Settings.DisabledBreakouts.Add(name);
                _vm.Persist();
            };
            _breakoutsPanel.Children.Add(check);
        }
    }

    private void UpdateGearImportStatus()
    {
        var total = _main.Settings.GearChecklist.Count;
        if (total == 0)
        {
            _gearImportStatus.Text = "No gear list imported.";
            _gearClearBtn.IsEnabled = false;
            return;
        }
        var done = _main.Settings.GearChecklist.Count(i => i.Acquired);
        var name = _main.Settings.GearChecklistName.Length > 0
            ? _main.Settings.GearChecklistName
            : "Imported gear list";
        _gearImportStatus.Text = $"{name}: {done}/{total} checked.";
        _gearClearBtn.IsEnabled = true;
    }

    private async Task ImportGearChecklist()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import EQ Legends Tools shopping list",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("HTML files") { Patterns = ["*.html", "*.htm"] },
                FilePickerFileTypes.All,
            ],
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
        try
        {
            var import = GearChecklistImporter.ImportFile(path);
            if (import.Items.Count == 0)
            {
                _gearImportStatus.Text = "No gear items found in that file.";
                return;
            }
            _main.Settings.GearChecklist = import.Items;
            _main.Settings.GearChecklistName = import.Name;
            _main.PersistSettings();
            RefreshGearCard?.Invoke();
            UpdateGearImportStatus();
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            _gearImportStatus.Text = $"Import failed: {ex.Message}";
        }
    }

    private void ClearGearChecklist()
    {
        _main.Settings.GearChecklist.Clear();
        _main.Settings.GearChecklistName = "";
        _main.PersistSettings();
        RefreshGearCard?.Invoke();
        UpdateGearImportStatus();
    }

    private void OpenGearTools()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://eqlegendstools.com/char-sheet/") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            _gearImportStatus.Text = $"Could not open EQ Legends Tools: {ex.Message}";
        }
    }

    /// <summary>
    /// The mez-duration rows: spell, an editable duration, and one line saying where the
    /// number came from. Rows come from <see cref="EQBuddy.UI.Shared.MezDurationRows"/>,
    /// the same builder the WPF window uses, so the two cannot come to different words
    /// about the precedence (#210's rule).
    /// </summary>
    private void BuildMezDurations()
    {
        _mezDurationList.Children.Clear();
        foreach (var row in EQBuddy.UI.Shared.MezDurationRows.Build(_main.MezTracker))
        {
            // Two columns, never a horizontal StackPanel: a stack measures with infinite
            // width in the stacking direction, so a long spell name would be CLIPPED
            // against the box with no ellipsis to say so (trap 14).
            var grid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var name = new TextBlock
            {
                Text = row.Spell, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = row.Source == MezDurationSource.Typed
                    ? AppTheme.AccentBrush : AppTheme.TextBrush,
            };
            ToolTip.SetTip(name, row.SourceNote);
            grid.Children.Add(name);

            var spell = row.Spell;   // one capture per row, not the loop variable's last
            var box = new TextBox
            {
                Text = row.DurationText, Width = 76, FontSize = 12,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Background = AppTheme.ComboBoxBrush, Foreground = AppTheme.TextBrush,
                BorderBrush = AppTheme.BorderBrush,
            };
            ToolTip.SetTip(box, "A bare number here is SECONDS — \"44\" is 44 seconds, "
                + "because mezzes are short. Clear the box to hand this spell back to EQBuddy.");
            box.LostFocus += (_, _) => CommitMezDuration(spell, box.Text);
            box.KeyDown += (_, e) =>
            {
                if (e.Key == global::Avalonia.Input.Key.Enter) CommitMezDuration(spell, box.Text);
            };
            Grid.SetColumn(box, 1);
            grid.Children.Add(box);
            _mezDurationList.Children.Add(grid);

            var note = AppTheme.DimText(row.SourceNote, new Thickness(0, 1, 0, 0));
            note.TextWrapping = TextWrapping.Wrap;
            _mezDurationList.Children.Add(note);
        }
    }

    /// <summary>A typed duration lands on commit. An empty box CLEARS it — the spell goes
    /// back to whatever EQBuddy has learned since, or the catalog.</summary>
    private void CommitMezDuration(string spell, string? text)
    {
        var typed = EQBuddy.UI.Shared.MezDurationText.Parse(text);
        if (typed == _main.MezDurations.Find(spell)) return;   // nothing moved
        _main.MezDurations.Set(spell, typed);
        BuildMezDurations();
    }

    // ---------------------------------------------------------------- Behavior

    private void InitBehavior()
    {
        var panel = _behaviorPanel;
        // FIRST in the tab, not buried at the bottom — the primary way in is the
        // title-bar phone button; this is the explanation that sits beside it. Same
        // position, same words, as the WPF Behavior tab's.
        panel.Children.Add(new TextBlock
        {
            Text = "EQBuddy Mobile (Beta)", FontSize = 12, FontWeight = FontWeight.SemiBold,
            Foreground = AppTheme.AccentBrush,
        });
        panel.Children.Add(AppTheme.DimText(
            "Show EQBuddy on a phone or tablet on your Wi-Fi: scan the code once, then pick which windows that device shows. LAN-only and off by default — nothing leaves your network. The phone button in the title bar opens it any time.",
            new Thickness(0, 2, 0, 4)));
        var mobileBtn = AppTheme.ActionButton("EQBuddy Mobile…");
        mobileBtn.HorizontalAlignment = HorizontalAlignment.Left;
        mobileBtn.Margin = new Thickness(0, 0, 0, 14);
        mobileBtn.Click += (_, _) => _main.OpenCompanionWindow();
        panel.Children.Add(mobileBtn);

        _hideUnfocusedCheck = Check("Hide the widget while the game is running but not focused",
            _vm.HideWhenGameUnfocused, on => _vm.HideWhenGameUnfocused = on, new Thickness(0));
        panel.Children.Add(_hideUnfocusedCheck);
        panel.Children.Add(AppTheme.DimText(
            "Alt-tab to a browser and the widget (and its chips and breakout windows) gets out of the way; alt-tab back to the game and everything returns. This one always shows the widget when the game isn't running — the next box covers that.",
            new Thickness(20, 2, 0, 0)));

        _hideNotRunningCheck = Check("Hide the widget while the game isn't running at all",
            _vm.HideWhenGameNotRunning, on => _vm.HideWhenGameNotRunning = on, new Thickness(0, 8, 0, 0));
        panel.Children.Add(_hideNotRunningCheck);
        panel.Children.Add(AppTheme.DimText(
            "The overlay exists only while you play: quit the game and everything disappears, launch it and everything returns (within a few seconds). Need it back without the game — say, to browse session history? Launch EQBuddy again: the running copy surfaces, and stays visible while any of its windows has focus.",
            new Thickness(20, 2, 0, 0)));

        // Both boxes above need a foreground-window probe, and X11/Wayland has none, so
        // on Linux they save and do nothing (David's call on #169, 2026-08-16: say so
        // for now rather than leave it looking broken). The wording comes from
        // UI.Shared so this can never disagree with the code that makes the decision.
        if (FocusHide.UnavailableNote is { Length: > 0 } note)
        {
            var warn = AppTheme.DimText(note, new Thickness(20, 6, 0, 0));
            warn.Foreground = AppTheme.WarnBrush;
            panel.Children.Add(warn);
        }

        _keepAboveCheck = Check("Keep EQBuddy above fullscreen overlays (Lossless Scaling and kin)",
            _vm.KeepAboveOverlays, on => _vm.KeepAboveOverlays = on);
        panel.Children.Add(_keepAboveCheck);
        panel.Children.Add(AppTheme.DimText(
            "Overlay apps created after EQBuddy land above it in the always-on-top pile, hiding the widget; this quietly re-lifts every EQBuddy window every few seconds. Untick if your screen-capture setup shows the widget twice (a real copy plus the captured one).",
            new Thickness(20, 2, 0, 0)));

        panel.Children.Add(new TextBlock
        {
            Text = "Global hotkeys",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = AppTheme.TextBrush,
            Margin = new Thickness(0, 14, 0, 0),
        });
        panel.Children.Add(AppTheme.DimText(
            "Nothing is bound until you bind it. A bound key is claimed system-wide while EQBuddy runs — it will stop reaching the game and every other app — so pick combos nothing else uses (Ctrl+Alt+… is usually safe). Click a box, press your keys; ✕ unbinds.",
            new Thickness(0, 2, 0, 4)));
        panel.Children.Add(_hotkeysPanel);
        BuildHotkeyRows();

        var regenRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        regenRow.Children.Add(new TextBlock
        {
            Text = "Regen heals about", FontSize = 12, Foreground = AppTheme.TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });
        _regenPerTickBox.Margin = new Thickness(6, 0);
        _regenPerTickBox.LostFocus += (_, _) =>
        {
            if (!_ready) return;
            // Blank or unparseable = back to the wiki base; the box shows any clamp.
            _vm.RegenPerTickOverride = int.TryParse((_regenPerTickBox.Text ?? "").Trim(), out var v) ? v : 0;
            _regenPerTickBox.Text = _vm.RegenPerTickOverride > 0 ? _vm.RegenPerTickOverride.ToString() : "";
        };
        regenRow.Children.Add(_regenPerTickBox);
        regenRow.Children.Add(new TextBlock
        {
            Text = "hp per tick (blank = wiki base)", FontSize = 12, Foreground = AppTheme.TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });
        panel.Children.Add(regenRow);
        panel.Children.Add(AppTheme.DimText(
            "Hymn of Restoration and similar regen ticks never log an amount, so their healing is estimated. The wiki knows the unamplified base (Hymn: 9), but instruments and ranks raise the real number — read yours off the heal text over your head and type it here. Your number wins.",
            new Thickness(20, 2, 0, 0)));

        _truncateCheck = Check("Auto-empty finished-session logs",
            _main.TruncateLogsValue, on => _main.SetTruncateLogs(on), new Thickness(0, 12, 0, 0));
        panel.Children.Add(_truncateCheck);
        panel.Children.Add(AppTheme.DimText(
            "Turn off if you use GINA/GamParse or upload your log files elsewhere — they will grow forever, so clean them up yourself occasionally. (Cleanup already stands down whenever the game, GINA, or GamParse is running.)",
            new Thickness(20, 2, 0, 0)));
        _archiveCheck = Check("Keep a timestamped copy before emptying (Logs/archive)",
            _vm.ArchiveLogs, on => _vm.ArchiveLogs = on, new Thickness(20, 6, 0, 0));
        panel.Children.Add(_archiveCheck);
        panel.Children.Add(AppTheme.DimText(
            "On by default: each finished session is saved as eqlog_name_server_YYYYMMDDHHMMSS.txt — the stamp is when the session ended — and Reset session splits the log here rather than letting it run on. Archives are yours to keep or clean up; EQBuddy never deletes them. Untick if you would rather have the disk space back.",
            new Thickness(40, 2, 0, 0)));

        _tutorialCheck = Check("Show quick tutorial at launch",
            _main.Settings.ShowTutorial, on => _vm.ShowTutorial = on);
        panel.Children.Add(_tutorialCheck);

        _perfStatsCheck = Check("Show EQBuddy's own CPU & memory in the title bar",
            _main.Settings.ShowPerfStats, on =>
            {
                _main.Settings.ShowPerfStats = on;
                _main.PersistSettings();
            });
        panel.Children.Add(_perfStatsCheck);
        panel.Children.Add(AppTheme.DimText(
            "A small dim readout (\"0.3% · 84 MB\") refreshed every few seconds — CPU is the share of ALL cores. Diagnostic honesty: if EQBuddy ever hogs your machine, this is how you catch it and tell us.",
            new Thickness(20, 2, 0, 0)));
    }

    // ---- global hotkeys, opt-in only (#100 — see the WPF HotkeyManager) ----

    /// <summary>Mirrors WPF HotkeyManager.Actions; flag for consolidation once a
    /// cross-platform hotkey layer exists. Gestures share the same text form
    /// ("Ctrl+Alt+M") — Avalonia's Key names match WPF's, so settings round-trip.</summary>
    private static readonly (string Key, string Label)[] HotkeyActions =
    [
        ("toggleAll", "Show / hide all of EQBuddy"),
        ("toggleMinimize", "Minimize / restore the dashboard"),
        ("toggleMap", "Zone map"),
        ("toggleQuests", "Quest tracker"),
        ("toggleSpawns", "Spawn timers"),
        ("toggleClickThrough", "Click-through"),
    ];

    private void BuildHotkeyRows()
    {
        _hotkeysPanel.Children.Clear();
        foreach (var (key, label) in HotkeyActions)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(180)));
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            row.Children.Add(new TextBlock
            {
                Text = label, FontSize = 12, Foreground = AppTheme.TextBrush,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var bound = _main.Settings.Hotkeys.GetValueOrDefault(key, "");
            var recorder = AppTheme.ActionButton(
                // The recording prompt names the rule instead of hiding it: a bare key
                // is rejected on purpose (a global "G" would eat chat typing), and the
                // 1.66 field test proved silent rejection reads as a dead recorder.
                _recordingAction == key
                    ? _recordingHint ?? "press Ctrl/Alt/Shift + a key…"
                    : bound.Length > 0 ? bound : "not bound — click to set");
            recorder.FontSize = 11;
            recorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            recorder.HorizontalContentAlignment = HorizontalAlignment.Center;
            recorder.Click += (_, _) =>
            {
                _recordingAction = _recordingAction == key ? null : key;
                _recordingHint = null;
                BuildHotkeyRows();
            };
            Grid.SetColumn(recorder, 1);
            row.Children.Add(recorder);

            var clear = AppTheme.IconButton("✕", "Unbind");
            clear.FontSize = 11;
            clear.Margin = new Thickness(4, 0, 0, 0);
            clear.IsVisible = bound.Length > 0;
            clear.Click += (_, _) =>
            {
                _main.Settings.Hotkeys.Remove(key);
                _main.PersistSettings();
                ApplyHotkeys?.Invoke();
                _recordingAction = null;
                BuildHotkeyRows();
            };
            Grid.SetColumn(clear, 2);
            row.Children.Add(clear);
            _hotkeysPanel.Children.Add(row);
        }
    }

    private void OnRecordKeyDown(object? sender, KeyEventArgs e)
    {
        if (_recordingAction is not { } action) return;
        e.Handled = true;
        var key = e.Key;
        if (key == Key.Escape) { _recordingAction = null; BuildHotkeyRows(); return; }
        // A bare modifier press isn't a gesture yet — wait for the real key.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
        parts.Add(key.ToString());
        var gesture = string.Join("+", parts);
        // Modifier required — a bare global letter would eat the game's chat typing —
        // and the key itself must map to a registrable virtual key. Parse is the same
        // gate Apply uses, so nothing unregistrable is ever stored (WPF parity). Say so
        // on the button itself: a silent return looks like a dead recorder.
        if (HotkeyManager.Parse(gesture) is null)
        {
            _recordingHint = $"{key} alone won't do — add Ctrl, Alt or Shift";
            BuildHotkeyRows();
            return;
        }
        _main.Settings.Hotkeys[action] = gesture;
        _main.PersistSettings();
        ApplyHotkeys?.Invoke();
        _recordingAction = null;
        _recordingHint = null;
        BuildHotkeyRows();
    }

    // ---------------------------------------------------------------- shared plumbing

    /// <summary>Keep an open Options window synchronized with the menu or tracker close.</summary>
    internal void SyncTrackSpawns(bool on)
    {
        var ready = _ready;
        _ready = false;
        _trackSpawnsCheck.IsChecked = on;
        _ready = ready;
    }

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _themeCombo.SelectedIndex < 0) return;
        _vm.ThemeIndex = _themeCombo.SelectedIndex;
        AppTheme.Apply(_main.Settings);
        _main.RefreshTheme();
        // No card-row rebuild needed here, unlike WPF: every row holds references to the
        // live AppTheme brushes, which Apply just mutated in place.
        UpdateCustomColorsPanel();
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
        _customColorsPanel.IsVisible = _main.Settings.Theme == CustomTheme.Key;
        _customColorsPanel.Children.Clear();
        if (!_customColorsPanel.IsVisible) return;

        _customColorsPanel.Margin = new Thickness(0, 1, 0, 2);
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
            // Invalid hex is simply not committed — the palette keeps its last good color.
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
        if (!_ready || _soundCombo.SelectedIndex < 0) return;
        if (!_vm.IsCustomSoundIndex(_soundCombo.SelectedIndex))
        {
            _vm.SelectNamedSound(_soundCombo.SelectedIndex);
        }
        else
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose an alert sound",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Sound files") { Patterns = AlertSoundFormats.Patterns },
                    FilePickerFileTypes.All,
                ],
            });
            if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            {
                _vm.SetCustomSound(path);
            }
            else if (!_vm.IsCustomSoundIndex(_vm.SoundIndex))
            {
                _ready = false; _soundCombo.SelectedIndex = _vm.SoundIndex; _ready = true;   // cancelled — revert
            }
        }
        UpdateSoundFileNote();
        _main.PlayAlertSound();   // instant feedback on the new choice
    }

    private void UpdateSoundFileNote()
    {
        _soundFileNote.Text = _vm.SoundFileNote;
        _soundFileNote.IsVisible = _vm.SoundFileNote.Length > 0;
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

    private CheckBox Check(string label, bool initial, Action<bool> apply, Thickness? margin = null)
    {
        var check = new CheckBox
        {
            Margin = margin ?? new Thickness(0, 10, 0, 0),
            IsChecked = initial,
            Content = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = AppTheme.TextBrush,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        check.IsCheckedChanged += (_, _) =>
        {
            if (_ready) apply(check.IsChecked == true);
        };
        return check;
    }

    private static TextBlock Heading(string text, Thickness margin) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = AppTheme.AccentBrush,
        Margin = margin,
    };

    private static Grid Row(string label, Control control, Thickness? margin = null, string? labelTip = null)
    {
        var row = new Grid { Margin = margin ?? default };
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var text = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = AppTheme.TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (labelTip is not null) ToolTip.SetTip(text, labelTip);
        row.Children.Add(text);
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private void AddSlider(Panel panel, string label, TextBlock value, Slider slider,
        string? hint = null, double topMargin = 0, double bottomMargin = 12, string? labelTip = null)
    {
        panel.Children.Add(Row(label, value, new Thickness(0, topMargin, 0, 0), labelTip));
        if (hint is not null) panel.Children.Add(AppTheme.DimText(hint));
        slider.Margin = new Thickness(0, 4, 0, bottomMargin);
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
        _scaleLabel.Text = _vm.ScaleLabel;
        _chipScaleLabel.Text = _vm.ChipScaleLabel;
        _opacityLabel.Text = _vm.OpacityLabel;
        _bgOpacityLabel.Text = _vm.BackgroundOpacityLabel;
        _alertVolumeLabel.Text = $"{_alertVolumeSlider.Value:P0}";
        _speechRateLabel.Text = _vm.SpeechRateLabel;
        _speechVolumeLabel.Text = _vm.SpeechVolumeLabel;
        _gridSpacingLabel.Text = $"{_gridSpacingSlider.Value:0} px";
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(IsInteractiveControl))
            return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private static bool IsInteractiveControl(Visual visual) => visual is
        Button or TextBox or ComboBox or global::Avalonia.Controls.Slider or CheckBox
        or ToggleButton or ScrollBar or ListBox or AutoCompleteBox;

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
