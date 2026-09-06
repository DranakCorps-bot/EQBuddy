using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **The Look block, host-neutral** — everything Settings knows about what EQBuddy looks
/// like: the colour theme picker and its Custom rows, the four size/opacity sliders, the
/// alignment grid and its spacing, and the cursor ring.
///
/// **Blocks, not tabs, are the unit that moves** (Fable's SR series; <see cref="SettingsAlertsView"/>
/// is the precedent this file follows line for line). A block builds its own controls, carries
/// its own visibility and spacing (trap 15), and knows nothing about the window it hangs in —
/// so <c>OptionsWindow</c> keeps its five tabs in the arrangement players already have while
/// the Evolved shell's Settings room composes the SAME block under the signed four-tab IA. The
/// alternative — building the room fresh beside a live <c>OptionsWindow</c> — is two copies of
/// the same control wirings drifting until retirement day, #210's mechanism with a bigger
/// surface.
///
/// **Each host constructs its own instance** (trap 45). A WPF <c>UIElement</c> has exactly one
/// parent, so a block shared between two hosts is torn out of whichever painted it last —
/// silently on WPF, which is harder to notice than an exception, not easier.
///
/// **Both hosts wrap one <see cref="AppSettings"/>** (trap 13): the block takes the
/// <see cref="OptionsViewModel"/> its host already built over <c>MainWindow.Settings</c> and
/// never loads settings for itself, because a second snapshot clobbers the first one wholesale
/// on its next save (#169).
///
/// **What is deliberately NOT here: the window's own chrome.** "Drag either side edge to widen
/// this window" is a sentence about <c>OptionsWindow</c>'s resize grips — it is true of that
/// host and false of a shell room that has none, so it stays declared on the window, beside the
/// grips it describes. Same rule as the width persistence, the monitor clamp and the tab links.
///
/// **The vocabulary sweep ran here** (§4 of `docs/BEVEL-v2-staging-critique.md`, Helm-signed).
/// A block serving two hosts has ONE string set and it has to pass in shell scope, so lifting a
/// block IS that block's sweep: "Widget size" and "Whole-widget opacity" became EQBuddy's own
/// name. **"Theme" survives, and that is a ruling rather than an oversight** — the ban's
/// `\bthemes?\b` is written against the v1 sense (*"the Progress theme"*, a folded window
/// grouping); this label is the COLOUR theme picker, a real player-facing feature David rules
/// on (<see cref="ThemeCatalog"/>). Bevel flagged the collision before anyone could run a
/// mechanical rewrite over it (BEVEL.md I-11 §5), and the exemption is written down in
/// <c>ShellTerminologyTests.Exempt</c> where the next reader will find it.
/// </summary>
internal sealed class SettingsLookView
{
    private readonly MainWindow _main;
    private readonly OptionsViewModel _vm;
    private readonly Func<bool> _hostReady;
    private readonly Func<object, object> _resource;
    private readonly Action _repaintHost;

    /// <summary>The host's gate (false while it is still building). Every handler below is
    /// closed until it opens, because a slider assigned during construction raises
    /// <c>ValueChanged</c> exactly as a player's drag does.</summary>
    private bool Ready => _hostReady();

    /// <param name="repaintHost">The host's chance to rebuild anything that resolved a brush
    /// at CONSTRUCTION time rather than through a <c>DynamicResource</c> — the card rows in
    /// <see cref="OptionsCardsView"/> are the live example. Everything this block builds
    /// repaints itself on a theme swap; the host's siblings are not this block's to know
    /// about, so it asks rather than reaching.</param>
    public SettingsLookView(MainWindow main, OptionsViewModel vm, Func<bool> ready,
        Func<object, object> resource, Action repaintHost)
    {
        _main = main;
        _vm = vm;
        _hostReady = ready;
        _resource = resource;
        _repaintHost = repaintHost;
    }

    private UIElement? _block;

    /// <summary>This instance's body, built on first ask and kept — the host re-shows it
    /// rather than re-building, so a half-dragged slider survives a tab switch.</summary>
    public UIElement Block => _block ??= Build();

    private ComboBox _themeCombo = null!;
    private StackPanel _customColors = null!;
    private Slider _scaleSlider = null!, _chipScaleSlider = null!;
    private Slider _bgOpacitySlider = null!, _opacitySlider = null!, _gridSpacingSlider = null!;
    private TextBlock _scaleLabel = null!, _chipScaleLabel = null!;
    private TextBlock _bgOpacityLabel = null!, _opacityLabel = null!, _gridSpacingLabel = null!;
    private CheckBox _gridOverlayCheck = null!, _cursorRingCheck = null!;

    private UIElement Build()
    {
        var panel = new StackPanel();

        // ---- colour theme, and the Custom rows that only exist while Custom is picked ----

        _themeCombo = new ComboBox { Width = 130, FontSize = 12 };
        foreach (var label in OptionsViewModel.ThemeLabels) _themeCombo.Items.Add(label);
        _themeCombo.SelectedIndex = _vm.ThemeIndex;
        _themeCombo.SelectionChanged += OnThemeChanged;
        panel.Children.Add(RowWithControl("Theme", _themeCombo));

        _customColors = new StackPanel
        {
            Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0),
        };
        panel.Children.Add(_customColors);
        UpdateCustomColorsPanel();

        // ---- the four sliders ----

        _scaleLabel = AccentValue("100%");
        panel.Children.Add(LabelledValue("EQBuddy size", _scaleLabel, new Thickness(0, 12, 0, 0)));
        _scaleSlider = new Slider
        {
            Minimum = 0.8, Maximum = 1.6, TickFrequency = 0.05, IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 4, 0, 12), Value = _vm.UiScale,
        };
        _scaleSlider.ValueChanged += (_, _) =>
        {
            if (!Ready) return;
            _vm.UiScale = _scaleSlider.Value;
            _main.SetUiScale(_vm.UiScale);
            UpdateLabels();
        };
        panel.Children.Add(_scaleSlider);

        _chipScaleLabel = AccentValue("100%");
        var chipRow = LabelledValue("Chips & alerts size", _chipScaleLabel, new Thickness(0));
        ((TextBlock)chipRow.Children[0]).ToolTip = "Spawn timer chips, mez chips, and the alert banner";
        panel.Children.Add(chipRow);
        _chipScaleSlider = new Slider
        {
            Minimum = 0.8, Maximum = 2.0, TickFrequency = 0.05, IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 4, 0, 12),
        };
        _chipScaleSlider.Value = Math.Clamp(_vm.ChipScale, _chipScaleSlider.Minimum, _chipScaleSlider.Maximum);
        _chipScaleSlider.ValueChanged += (_, _) =>
        {
            if (!Ready) return;
            _vm.ChipScale = _chipScaleSlider.Value;
            _main.SetChipScale(_vm.ChipScale);
            UpdateLabels();
        };
        panel.Children.Add(_chipScaleSlider);

        _bgOpacityLabel = AccentValue("95%");
        panel.Children.Add(LabelledValue("Background see-through", _bgOpacityLabel, new Thickness(0)));
        panel.Children.Add(Dim("Only the dark panel fades — text stays sharp.", new Thickness(0)));
        _bgOpacitySlider = new Slider
        {
            Minimum = 0.15, Maximum = 1.0, TickFrequency = 0.05, IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 4, 0, 12), Value = _vm.BackgroundOpacity,
        };
        _bgOpacitySlider.ValueChanged += (_, _) =>
        {
            if (!Ready) return;
            _vm.BackgroundOpacity = _bgOpacitySlider.Value;
            _main.SetBackgroundOpacity(_vm.BackgroundOpacity);
            UpdateLabels();
        };
        panel.Children.Add(_bgOpacitySlider);

        _opacityLabel = AccentValue("96%");
        panel.Children.Add(LabelledValue("Overall opacity", _opacityLabel, new Thickness(0)));
        panel.Children.Add(Dim("Fades everything, text included.", new Thickness(0)));
        _opacitySlider = new Slider
        {
            Minimum = 0.5, Maximum = 1.0, TickFrequency = 0.02, IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 4, 0, 4), Value = _vm.Opacity,
        };
        _opacitySlider.ValueChanged += (_, _) =>
        {
            if (!Ready) return;
            _vm.Opacity = _opacitySlider.Value;
            _main.SetWindowOpacity(_vm.Opacity);
            UpdateLabels();
        };
        panel.Children.Add(_opacitySlider);

        // ---- the alignment grid ----

        _gridOverlayCheck = Check("▦ Grid overlay for aligning your game UI",
            _main.Settings.ShowGridOverlay, new Thickness(0, 10, 0, 0),
            () => { if (Ready) _main.SetGridOverlay(_gridOverlayCheck.IsChecked == true); });
        panel.Children.Add(_gridOverlayCheck);
        panel.Children.Add(Dim(
            "A faint click-through grid over the whole desk — line up your game windows, then "
            + "toggle it off here or in the right-click menu. Stronger lines every fourth square.",
            new Thickness(20, 2, 0, 0)));

        _gridSpacingLabel = AccentValue("32 px");
        panel.Children.Add(LabelledValue("Grid spacing", _gridSpacingLabel, new Thickness(20, 4, 0, 0)));
        _gridSpacingSlider = new Slider
        {
            Minimum = 16, Maximum = 128, TickFrequency = 8, IsSnapToTickEnabled = true,
            Margin = new Thickness(20, 4, 0, 4),
        };
        _gridSpacingSlider.Value = Math.Clamp(_main.Settings.GridSpacing,
            _gridSpacingSlider.Minimum, _gridSpacingSlider.Maximum);
        _gridSpacingLabel.Text = $"{_gridSpacingSlider.Value:0} px";
        _gridSpacingSlider.ValueChanged += (_, _) =>
        {
            if (!Ready) return;
            _main.Settings.GridSpacing = _gridSpacingSlider.Value;
            _gridSpacingLabel.Text = $"{_gridSpacingSlider.Value:0} px";
            _vm.Persist();
            _main.RefreshGridSpacing();   // live while the grid is up
        };
        panel.Children.Add(_gridSpacingSlider);

        // ---- the cursor ring ----

        _cursorRingCheck = Check("Cursor ring (never lose your pointer)",
            _main.Settings.ShowCursorRing, new Thickness(0, 10, 0, 0),
            () => { if (Ready) _main.SetCursorRing(_cursorRingCheck.IsChecked == true); });
        panel.Children.Add(_cursorRingCheck);
        panel.Children.Add(Dim(
            "A soft ring follows your mouse everywhere — click-through, over the game too. "
            + "Drag its edge to resize it.",
            new Thickness(20, 2, 0, 0)));

        panel.Children.Add(Dim(
            "Size also scales all text. Changes apply instantly and are saved.",
            new Thickness(0, 8, 0, 0)));

        UpdateLabels();
        return panel;
    }

    /// <summary>The four live values, in the one place that formats them. The labels come from
    /// <see cref="OptionsViewModel"/> so the block cannot invent a second way to say a
    /// percentage that the shell room then disagrees with.</summary>
    private void UpdateLabels()
    {
        _scaleLabel.Text = _vm.ScaleLabel;
        _chipScaleLabel.Text = _vm.ChipScaleLabel;
        _opacityLabel.Text = _vm.OpacityLabel;
        _bgOpacityLabel.Text = _vm.BackgroundOpacityLabel;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!Ready) return;
        _vm.ThemeIndex = _themeCombo.SelectedIndex;
        ThemeManager.Apply(_vm.Settings);
        UpdateCustomColorsPanel();
        _repaintHost();
        _main.RefreshTheme();
    }

    // ------------------------------------------------------------ the Custom palette ----

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
        _customColors.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        if (!custom) return;
        _customColors.Children.Clear();
        _customColors.Children.Add(ColorRow("Background",
            _vm.Settings.CustomThemeBg ?? CustomTheme.DefaultBg, v => _vm.Settings.CustomThemeBg = v));
        _customColors.Children.Add(ColorRow("Text",
            _vm.Settings.CustomThemeText ?? CustomTheme.DefaultText, v => _vm.Settings.CustomThemeText = v));
        _customColors.Children.Add(ColorRow("Accent",
            _vm.Settings.CustomThemeAccent ?? CustomTheme.DefaultAccent, v => _vm.Settings.CustomThemeAccent = v));
    }

    private DockPanel ColorRow(string label, string current, Action<string> store)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var name = new TextBlock
        { Text = label, FontSize = 11, Width = 72, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(name, Dock.Left);
        row.Children.Add(name);

        var hexBox = new TextBox
        { Text = current, FontSize = 11, Width = 64, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(hexBox, Dock.Right);

        void Commit(string value)
        {
            // Invalid hex is simply not committed — the palette keeps its last good color.
            if (CustomTheme.Valid(value) is not { } hex) { hexBox.Text = current; return; }
            current = hex;
            store(hex);
            _main.PersistSettings();
            hexBox.Text = hex;
            ThemeManager.Apply(_vm.Settings);
            _repaintHost();
            _main.RefreshTheme();
        }

        hexBox.LostFocus += (_, _) => Commit(hexBox.Text);
        hexBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(hexBox.Text); };
        row.Children.Add(hexBox);

        var swatches = new WrapPanel
        { Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        foreach (var hex in SwatchColors)
        {
            var swatch = new Border
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

    // ================================================================== plumbing ====

    private TextBlock Dim(string text, Thickness margin) => new()
    {
        Text = text, Style = (Style)_resource("Dim"),
        TextWrapping = TextWrapping.Wrap, Margin = margin,
    };

    private static TextBlock AccentValue(string text)
    {
        var block = new TextBlock
        {
            Text = text, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        return block;
    }

    /// <summary>Label on the left, live value on the right — a one-cell Grid, never a
    /// horizontal StackPanel, because the value's width changes as it counts (trap 14).</summary>
    private static Grid LabelledValue(string label, TextBlock value, Thickness margin)
    {
        var grid = new Grid { Margin = margin };
        grid.Children.Add(new TextBlock { Text = label, FontSize = 12 });
        grid.Children.Add(value);
        return grid;
    }

    private static Grid RowWithControl(string label, FrameworkElement right)
    {
        var grid = new Grid();
        grid.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
        });
        right.HorizontalAlignment = HorizontalAlignment.Right;
        grid.Children.Add(right);
        return grid;
    }

    private CheckBox Check(string text, bool initial, Thickness margin, Action changed)
    {
        var label = new TextBlock { Text = text, FontSize = 12 };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        var box = new CheckBox { Content = label, Margin = margin, IsChecked = initial };
        box.Checked += (_, _) => changed();
        box.Unchecked += (_, _) => changed();
        return box;
    }
}
