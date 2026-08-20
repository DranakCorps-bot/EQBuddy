using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

internal static class AppTheme
{
    // Every theme color is a single, never-replaced SolidColorBrush instance. Controls
    // hold a reference to these (not a copy), so Apply() mutating .Color repaints
    // everything already on screen — Avalonia brushes raise Invalidated on change, no
    // resource-dictionary lookup or rebuild required. The colors themselves come from
    // EQBuddy.UI.Shared.ThemePalettes, the same table the WPF app builds its resource
    // dictionary from, so the two UIs cannot drift apart.
    public static readonly SolidColorBrush BgBrush = new();
    public static readonly SolidColorBrush PanelBrush = new();
    public static readonly SolidColorBrush PanelHoverBrush = new();
    public static readonly SolidColorBrush BorderBrush = new();
    public static readonly SolidColorBrush TextBrush = new();
    public static readonly SolidColorBrush DimBrush = new();
    public static readonly SolidColorBrush AccentBrush = new();
    public static readonly SolidColorBrush GoodBrush = new();
    public static readonly SolidColorBrush BadBrush = new();
    public static readonly SolidColorBrush WarnBrush = new();
    public static readonly SolidColorBrush PopupBrush = new();
    public static readonly SolidColorBrush ComboBoxBrush = new();
    public static readonly SolidColorBrush GoodWashBrush = new();
    public static readonly SolidColorBrush WarnWashBrush = new();

    // Chart-series brushes (the 2026-08-13 approved chart pass): deeper cuts than the
    // ambient accents, colorblind-validated, one row per theme in ThemePalettes (and
    // fixed steps under the Custom theme). Live-mutated like every other brush here, so
    // the timeline and the sparkline repaint on a theme switch.
    public static readonly SolidColorBrush ChartYouBrush = new();
    public static readonly SolidColorBrush ChartPetBrush = new();
    public static readonly SolidColorBrush ChartIncomingBrush = new();
    public static readonly SolidColorBrush ChartCritBrush = new();

    // Derived tones of the 2026-08-11 WPF modernization — alpha/level variations of
    // palette keys, recomputed on every Apply so all themes (and Custom) get them for
    // free. Same formulas as the WPF ThemeManager, so the two UIs render alike:
    //   Hairline — card borders: the accent at a whisper instead of a solid line.
    //   Track    — the empty part of a stat bar, under the accent-filled part.
    //   Raised   — chips and tiles, one step above panel.
    //   AccentDeep — gradient start for bar fills, accent pulled toward the ground.
    public static readonly SolidColorBrush HairlineBrush = new();
    public static readonly SolidColorBrush TrackBrush = new();
    public static readonly SolidColorBrush RaisedBrush = new();
    public static readonly SolidColorBrush AccentDeepBrush = new();

    /// <summary>Palette key → the brush it drives. Keys this UI doesn't style (scrollbar
    /// thumbs, toggle highlights — Avalonia's own control themes handle those) are simply
    /// absent, and <see cref="Apply"/> skips them.</summary>
    private static readonly Dictionary<string, SolidColorBrush> ByKey = new()
    {
        ["BgBrush"] = BgBrush,
        ["PanelBrush"] = PanelBrush,
        ["PanelHoverBrush"] = PanelHoverBrush,
        ["BorderBrush"] = BorderBrush,
        ["TextBrush"] = TextBrush,
        ["DimBrush"] = DimBrush,
        ["AccentBrush"] = AccentBrush,
        ["GoodBrush"] = GoodBrush,
        ["BadBrush"] = BadBrush,
        ["WarnBrush"] = WarnBrush,
        ["PopupBrush"] = PopupBrush,
        ["ComboBoxBrush"] = ComboBoxBrush,
        ["GoodWashBrush"] = GoodWashBrush,
        ["WarnWashBrush"] = WarnWashBrush,
        ["ChartYouBrush"] = ChartYouBrush,
        ["ChartPetBrush"] = ChartPetBrush,
        ["ChartIncomingBrush"] = ChartIncomingBrush,
        ["ChartCritBrush"] = ChartCritBrush,
    };

    /// <summary>Palette key → the live brush, by NAME. The design-system layer names its
    /// inks the way UI.Shared does (a type role carries a "DimBrush", a quest badge a
    /// "GoodBrush"), and this is what turns one of those names into the brush this UI
    /// actually mutates on a theme switch — the counterpart of WPF's
    /// SetResourceReference. A key this UI doesn't style falls back to the body ink
    /// rather than painting nothing: an invisible control is the one failure mode the
    /// whole palette table exists to prevent.</summary>
    public static IBrush BrushFor(string key) =>
        ByKey.TryGetValue(key, out var brush) ? brush
        : Derived.TryGetValue(key, out var tone) ? tone
        : TextBrush;

    /// <summary>The ThemeTones derivations, addressable by the same key the WPF resource
    /// dictionary uses. They are not in <see cref="ByKey"/> because no palette ROW
    /// carries them — <see cref="ApplyPalette"/> computes them.</summary>
    private static readonly Dictionary<string, SolidColorBrush> Derived = new(StringComparer.Ordinal)
    {
        ["HairlineBrush"] = HairlineBrush,
        ["TrackBrush"] = TrackBrush,
        ["RaisedBrush"] = RaisedBrush,
        ["AccentDeepBrush"] = AccentDeepBrush,
    };

    static AppTheme() => Apply("ParchmentBrass");

    /// <summary>Repaints every control holding one of the brushes above. An unrecognized
    /// key (e.g. from an older settings.json) falls back to the first theme rather than
    /// throwing — same behavior as the WPF app's ThemeManager.</summary>
    public static void Apply(string themeKey) => ApplyPalette(themeKey, ThemePalettes.For(themeKey));

    /// <summary>Settings-aware overload: applies the Custom theme's derived palette when
    /// it's selected (colors are edited in either UI's Options; both follow the stored
    /// values), otherwise the selected catalog theme.</summary>
    public static void Apply(Core.AppSettings settings) =>
        ApplyPalette(settings.Theme, CustomTheme.PaletteFor(settings));

    /// <summary>Raised after every swap with the theme key and its full palette (the
    /// derived tones included) — the WPF ThemeManager's event, same name and same
    /// payload. EQBuddy Mobile listens so a paired phone repaints with the desktop
    /// instead of waiting for a reconnect; nothing else subscribes yet.</summary>
    public static event Action<string, IReadOnlyList<(string Key, string Hex)>>? PaletteApplied;

    private static void ApplyPalette(string themeKey, IEnumerable<(string Key, string Hex)> palette)
    {
        // No-op unless EQBUDDY_OPAQUE=1 (scripts/shoot.ps1): makes the window ground
        // opaque so a capture photographs the UI, not the desktop behind it. Same call,
        // same place, as the WPF ThemeManager — the fix has to reach both UIs or it is
        // a second product (CLAUDE.md).
        var rows = CaptureTheme.IfEnabled(palette).ToList();
        foreach (var (key, hex) in rows)
            if (ByKey.TryGetValue(key, out var brush)) brush.Color = Color.Parse(hex);

        // The four derived tones come from UI.Shared rather than being recomputed here.
        // They were an inline copy of ThemeTones.Derive's arithmetic until the phone
        // needed the same list to ship — and a hand-copied twin of a shared decision is
        // the exact shape of the bug that carried #122 and #152 to Linux (CLAUDE.md).
        var derived = ThemeTones.Derive(rows).ToList();
        foreach (var (key, hex) in derived)
            if (Derived.TryGetValue(key, out var brush)) brush.Color = Color.Parse(hex);

        PaletteApplied?.Invoke(themeKey, [.. rows, .. derived]);
    }

    // Tint comes from the current theme's BgBrush rather than a fixed color, so this
    // still reads right after a theme switch — only the alpha is opacity's to control.
    // Returns a fresh brush each call (opacity is a slider, not a theme), so callers that
    // want it to track a live theme switch must re-invoke this after AppTheme.Apply.
    public static IBrush BgWithOpacity(double opacity)
    {
        var c = BgBrush.Color;
        return new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0.15, 1.0) * 255), c.R, c.G, c.B));
    }

    public static Button IconButton(AppIcon icon, string tip)
    {
        var button = IconButtonContent(CreateIcon(icon, DimBrush), tip);
        button.Padding = new Thickness(5);
        return button;
    }

    public static Button IconButton(string text, string tip)
    {
        return IconButtonContent(text, tip);
    }

    private static Button IconButtonContent(object content, string tip)
    {
        var button = new Button
        {
            Content = content,
            Background = Brushes.Transparent,
            Foreground = DimBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2),
            FontSize = 13,
            Cursor = new Cursor(StandardCursorType.Hand),
            MinWidth = 26,
            MinHeight = 24,
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    public static ToggleButton IconToggle(string text, string tip)
    {
        var button = new ToggleButton
        {
            Content = text,
            Background = Brushes.Transparent,
            Foreground = AccentBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2),
            FontSize = 13,
            Cursor = new Cursor(StandardCursorType.Hand),
            MinWidth = 26,
            MinHeight = 24,
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    public static Button StarButton(string key, string tip)
    {
        var button = IconButtonContent(CreateIcon(AppIcon.Star, DimBrush, 13), tip);
        button.Tag = key;
        button.Margin = new Thickness(8, 0, 0, 0);
        return button;
    }

    public static PathIcon Icon(AppIcon icon, IBrush? brush = null, double size = 14) =>
        CreateIcon(icon, brush ?? DimBrush, size);

    /// <summary>An icon by its <see cref="IconPaths"/> NAME, for the shared tables that
    /// name their own icons (<see cref="BreakoutPresentation.Icon"/>). The enum covers
    /// this UI's own chrome; a shared table cannot reach it and should not have to.</summary>
    public static PathIcon Icon(string iconName, IBrush? brush = null, double size = 14) =>
        new()
        {
            Data = StreamGeometry.Parse(IconPaths.Path(iconName)),
            Foreground = brush ?? DimBrush,
            Width = size,
            Height = size,
        };

    public static TextBlock DimText(string text, Thickness? margin = null) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = DimBrush,
        TextWrapping = TextWrapping.Wrap,
        Margin = margin ?? default,
    };

    public static TextBlock StatValue(string text = "") => new()
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        Foreground = AccentBrush,
    };

    public static SectionPanel Section(Control header, Control content) => new(header, content);

    /// <summary>A card that OPENS something instead of unfolding (David, 2026-08-16: one
    /// Quests card replaced the two tabbed checklists, and the Quest Tracker window is
    /// the real surface). Deliberately the same panel, corner, margin and padding as
    /// <see cref="Section"/> — the stack has to keep reading as one row of cards; the
    /// only honest difference is ↗ where the chevron would be, because this one leaves
    /// rather than unfolds.</summary>
    public static SectionLinkPanel SectionLink(Control header, Action onClick) => new(header, onClick);

    public static TextBlock Heading(string text, IBrush? brush = null) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = brush ?? AccentBrush,
    };

    /// <summary>Micro-label: the small-caps section eyebrow ("DAMAGE BY SOURCE") that
    /// organizes dense data without spending a heading's height. WPF uses AllSmallCaps;
    /// Avalonia has no Typography knob, so uppercase text plus a little tracking carries
    /// the same look.</summary>
    public static TextBlock SectionLabel(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 10.5,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 0.5,
        Foreground = DimBrush,
        Margin = new Thickness(0, 6, 0, 2),
    };

    /// <summary>Labeled dialog button, the WPF Theming.Button counterpart: default
    /// buttons render pale-gray-on-pale-gray against the dark themes (David's contrast
    /// pass, 2026-08-10), so labeled buttons pull from the live palette instead.</summary>
    public static Button ActionButton(string label, string? tip = null)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(12, 2),
            BorderThickness = new Thickness(1),
            FontSize = 12,
            Background = PanelBrush,
            Foreground = TextBrush,
            BorderBrush = AccentBrush,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        if (tip is not null) ToolTip.SetTip(button, tip);
        return button;
    }

    /// <summary>Raised tile chrome (2026-08-11 modernization; David's call: pills yes,
    /// ovals/capsules no): a rounded-RECT card one step above panel with a hairline
    /// border. Callers recolor BorderBrush for due/alarm states.</summary>
    public static Border RaisedCard(Control child) => new()
    {
        Child = child,
        Background = RaisedBrush,
        BorderBrush = HairlineBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(9),
        Padding = new Thickness(9, 6, 9, 7),
    };

    /// <summary>Small countdown/status pill, matching the WPF MapWindow chrome: radius 4
    /// (a rounded rect, never an oval), track-wash background, bold accent text.
    /// Callers restyle Background/Foreground for due states.</summary>
    public static Border Pill(string text)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 1, 7, 2),
            Background = TrackBrush,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10.5,
                FontWeight = FontWeight.Bold,
                Foreground = AccentBrush,
            },
        };
    }

    public static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private static PathIcon CreateIcon(AppIcon icon, IBrush brush, double size = 14)
    {
        // The path data comes from UI.Shared's IconPaths, by name — the enum member IS
        // the key. It was a hand-copied table here until 2026-08-20, byte-identical to
        // the shared one across all fifteen entries, which is the twin that "if a fix
        // exists in UI.Shared, both UIs must use it" (CLAUDE.md) is written about: the
        // Avalonia chip stacks shipped an older copy of the WPF anchor and carried #122
        // and #152 to Linux after Windows had already paid for both. Path() throws on an
        // unknown name rather than drawing nothing, which is what a missing enum arm
        // used to do here anyway.
        var data = IconPaths.Path(icon.ToString());

        return new PathIcon
        {
            Data = StreamGeometry.Parse(data),
            Foreground = brush,
            Width = size,
            Height = size,
        };
    }
}

internal enum AppIcon
{
    Settings,
    Refresh,
    Minimize,
    Expand,
    Close,
    Star,
    StarFilled,
    ChevronRight,
    ChevronDown,
    Map,
    Quest,
    Gear,
    Timeline,
    Tray,
    Chart,
    /// <summary>The title bar's EQBuddy Mobile button. Gate 5c drew this vector for
    /// exactly that control and it went unused, because the button it was drawn for was
    /// invisible on Windows and absent here.</summary>
    Phone,
}

/// <summary>What the widget's card stack is allowed to hold. Cards differ in what a
/// click does — unfold in place, or leave for a window — but the stack, the Options
/// show/hide pass and the render gate treat them alike, and that gate asks IsExpanded
/// before it fills a body. A launcher answering "never open" is what keeps the question
/// answerable at every one of those call sites without a type test.</summary>
internal abstract class SectionCard : Border
{
    /// <summary>Fires whenever the card opens or closes — MainWindow uses the open
    /// edge to render a just-expanded card immediately instead of waiting out the
    /// full-render gate (WPF's Expander.Expanded hook, integration pass).</summary>
    public event Action<bool>? ExpandedChanged;

    public virtual bool IsExpanded
    {
        get => false;
        set { }
    }

    protected void RaiseExpandedChanged(bool expanded) => ExpandedChanged?.Invoke(expanded);
}

internal sealed class SectionPanel : SectionCard
{
    private readonly Border _body;
    private readonly PathIcon _chevron;

    public override bool IsExpanded
    {
        get => _body.IsVisible;
        set
        {
            var changed = _body.IsVisible != value;
            _body.IsVisible = value;
            _chevron.Data = StreamGeometry.Parse(value
                ? "M7.41 8.59 12 13.17l4.59-4.58L18 10l-6 6-6-6 1.41-1.41Z"
                : "M8.59 16.59 13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41Z");
            if (changed) RaiseExpandedChanged(value);
        }
    }

    public SectionPanel(Control header, Control content)
    {
        Background = AppTheme.PanelBrush;
        CornerRadius = new CornerRadius(6);
        Margin = new Thickness(0, 2, 0, 0);

        _chevron = AppTheme.Icon(AppIcon.ChevronRight, AppTheme.DimBrush, 15);
        _chevron.VerticalAlignment = VerticalAlignment.Center;
        _chevron.Margin = new Thickness(6, 0, 0, 0);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        headerGrid.Children.Add(header);
        Grid.SetColumn(_chevron, 1);
        headerGrid.Children.Add(_chevron);

        var headerBorder = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = headerGrid,
        };
        headerBorder.PointerPressed += (_, args) =>
        {
            if (args.Source is Button or ToggleButton) return;
            IsExpanded = !IsExpanded;
            args.Handled = true;
        };

        _body = new Border
        {
            Padding = new Thickness(10, 0, 10, 8),
            Child = content,
            IsVisible = false,
        };

        Child = new StackPanel
        {
            Children =
            {
                headerBorder,
                _body,
            },
        };
    }
}

/// <summary>The launcher card — it sits in the stack looking like every other card and
/// behaves like a button. Built from a Border with a pointer handler rather than a real
/// Button because that is exactly how <see cref="SectionPanel"/>'s own header takes its
/// click: a Button would carry Avalonia's control theme, whose hover wash comes from the
/// system palette and not ours, and would need a template override just to say the same
/// thing. Keyboard reach matches the sibling cards, which have none either.</summary>
internal sealed class SectionLinkPanel : SectionCard
{
    public SectionLinkPanel(Control header, Action onClick)
    {
        Background = AppTheme.PanelBrush;
        CornerRadius = new CornerRadius(6);
        Margin = new Thickness(0, 2, 0, 0);

        var arrow = new TextBlock
        {
            Text = "↗",
            Foreground = AppTheme.DimBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.Children.Add(header);
        Grid.SetColumn(arrow, 1);
        grid.Children.Add(arrow);

        var body = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = grid,
        };
        body.PointerEntered += (_, _) => body.Background = AppTheme.PanelHoverBrush;
        body.PointerExited += (_, _) => body.Background = Brushes.Transparent;
        body.PointerPressed += (_, args) =>
        {
            onClick();
            args.Handled = true;
        };

        Child = body;
    }
}
