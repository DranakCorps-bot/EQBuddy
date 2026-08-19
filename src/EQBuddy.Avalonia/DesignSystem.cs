using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The Avalonia composition of <see cref="DesignTokens"/> and <see cref="IconPaths"/> —
/// the same job <see cref="AppTheme"/> does for colour, for the other four axes, and the
/// exact mirror of the WPF <c>DesignSystem</c>.
///
/// Nothing here decides anything. Every number and every path comes out of UI.Shared, so
/// the two windows cannot disagree about what "Body" means or what a card's radius is.
/// The alternative is what actually happened once: the Avalonia chip stacks shipped a
/// hand-copied older version of the WPF anchor and carried #122 and #152 to Linux and
/// macOS after Windows had already paid for both.
/// </summary>
internal static class DesignSystem
{
    private static readonly Dictionary<DesignTokens.TypeWeight, FontWeight> Weights = new()
    {
        [DesignTokens.TypeWeight.Regular] = FontWeight.Normal,
        [DesignTokens.TypeWeight.SemiBold] = FontWeight.SemiBold,
        [DesignTokens.TypeWeight.Bold] = FontWeight.Bold,
    };

    /// <summary>Just the SIZE of a type role, for the sites that already build their own
    /// TextBlock and only need the number off the scale. <see cref="Text"/> is better where
    /// a control is being created from scratch — it carries weight and ink too, and
    /// "secondary" rendered in the primary ink is not secondary.</summary>
    public static double Size(DesignTokens.TypeRole role) => DesignTokens.Spec(role).Size;

    /// <summary>A TextBlock wearing a type ROLE — size, weight and default ink together,
    /// because "secondary" rendered in the primary ink is not secondary.</summary>
    public static TextBlock Text(DesignTokens.TypeRole role, string text = "")
    {
        var spec = DesignTokens.Spec(role);
        return new TextBlock
        {
            Text = text,
            FontSize = spec.Size,
            FontWeight = Weights[spec.Weight],
            Foreground = AppTheme.BrushFor(spec.ColorKey),
        };
    }

    /// <summary>One icon from <see cref="IconPaths"/>, as a vector — never a glyph.
    /// Emoji render at a size and weight the app does not control, and PRs #148 and #166
    /// exist because they failed to render at all in Wine prefixes.</summary>
    public static PathIcon Icon(string name, string colorKey = "DimBrush",
        double size = 14, double opacity = 1.0) => new()
    {
        Data = StreamGeometry.Parse(IconPaths.Path(name)),
        Foreground = AppTheme.BrushFor(colorKey),
        Width = size,
        Height = size,
        Opacity = opacity,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>An icon that behaves like a button but reads like a glyph. A real Button,
    /// so it is keyboard-reachable and has a hit area — the controls this replaces were
    /// click-handled TextBlocks, one set per card, on every card in the list.</summary>
    /// <summary><paramref name="onClick"/> is optional: several call sites hold the
    /// button and attach <c>Click</c> themselves (they need the instance for other
    /// reasons), and forcing an empty lambda on those reads as a handler that does
    /// nothing rather than one attached elsewhere.</summary>
    public static Button IconButton(string name, string tip, Action? onClick = null,
        string colorKey = "DimBrush", double opacity = 1.0)
    {
        var button = new Button
        {
            Content = Icon(name, colorKey, opacity: opacity),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Width = DesignTokens.IconButtonSize,
            Height = DesignTokens.IconButtonSize,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        ToolTip.SetTip(button, tip);
        if (onClick is not null) button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>A clickable icon that sits INSIDE a line of text — the loot row's quest
    /// badge and its family. <see cref="IconButton"/> is the same idea at
    /// <see cref="DesignTokens.IconButtonSize"/>, which on the widget would make every
    /// loot row a third taller; this keeps the drawn size and widens only the target.
    ///
    /// The point is the transparent ground: a bare vector only receives a click where it
    /// is painted, so the map-pin badge had holes you could click straight through
    /// (#211). Windows found it first; this lane had the identical badge.</summary>
    public static Button InlineIconButton(string name, string tip, Action onClick,
        string colorKey = "DimBrush", double size = DesignTokens.IconInline)
    {
        var button = IconButton(name, tip, onClick, colorKey);
        button.Width = button.Height = DesignTokens.IconInlineHit;
        if (button.Content is PathIcon icon) icon.Width = icon.Height = size;
        return button;
    }

    /// <summary>An icon and a word on one baseline — the shape every textual button in
    /// the migrated surfaces takes.</summary>
    public static StackPanel IconLabel(string icon, string label, string colorKey = "DimBrush")
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(Icon(icon, colorKey, size: 12));
        var text = Text(DesignTokens.TypeRole.Caption, label);
        text.Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        panel.Children.Add(text);
        return panel;
    }
}

/// <summary>
/// THE selectable pill (gate 2b, docs/DesignSystem.md §11.2) — the Avalonia half of the
/// WPF <c>EqChip</c>, built from the same <see cref="ChipStyle"/> so the two cannot drift.
/// A WPF Border and an Avalonia Border are not the same object, which is exactly why the
/// SPEC is shared and the control is not (§5: do not build shared XAML).
/// </summary>
internal sealed class EqChip : Border
{
    private readonly TextBlock _label;
    private readonly TextBlock? _badge;

    /// <summary>What this chip selects — a mode string, a QuestTab, a class name.</summary>
    public object Key { get; }

    public EqChip(string text, object key, string? badge = null, string? tip = null,
        Action? onClick = null)
    {
        Key = key;
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        _label = DesignSystem.Text(ChipStyle.LabelRole, text);
        content.Children.Add(_label);
        if (badge is { Length: > 0 })
        {
            _badge = DesignSystem.Text(ChipStyle.BadgeRole, badge);
            _badge.Margin = new Thickness(DesignTokens.SpaceS, 1, 0, 0);
            content.Children.Add(_badge);
        }
        Child = content;
        CornerRadius = new CornerRadius(ChipStyle.Radius);
        Padding = new Thickness(ChipStyle.Padding.Left, ChipStyle.Padding.Top,
            ChipStyle.Padding.Right, ChipStyle.Padding.Bottom);
        Margin = new Thickness(0, 0, ChipStyle.Gap.Right, ChipStyle.Gap.Bottom);
        BorderThickness = new Thickness(ChipStyle.BorderThickness);
        Cursor = new Cursor(StandardCursorType.Hand);
        if (tip is not null) ToolTip.SetTip(this, tip);
        // Handled, or the window's own drag-to-move swallows the click.
        if (onClick is not null)
            PointerPressed += (_, e) => { e.Handled = true; onClick(); };
        SetSelected(false);
    }

    public void SetSelected(bool on)
    {
        var ink = ChipStyle.For(on);
        Background = AppTheme.BrushFor(ink.Background);
        BorderBrush = AppTheme.BrushFor(ink.Border);
        _label.Foreground = AppTheme.BrushFor(ink.Label);
        if (_badge is null) return;
        _badge.Foreground = AppTheme.BrushFor(ink.Badge);
        _badge.Opacity = ink.BadgeOpacity;
    }
}

/// <summary>A row of <see cref="EqChip"/> where exactly one is selected.</summary>
internal sealed class EqSegmentedStrip(Panel host)
{
    private readonly List<EqChip> _chips = [];

    public int Count => _chips.Count;

    public void Clear()
    {
        host.Children.Clear();
        _chips.Clear();
    }

    public EqChip Add(string text, object key, string? badge = null, string? tip = null,
        Action? onClick = null)
    {
        var chip = new EqChip(text, key, badge, tip, onClick);
        host.Children.Add(chip);
        _chips.Add(chip);
        return chip;
    }

    /// <summary>One chip by its key, or null. A strip sometimes has to hide a segment
    /// rather than disable it — the Loot card withholds "recent" when nothing on screen
    /// carries a timestamp — and reaching for it by key beats every caller keeping its
    /// own field.</summary>
    public EqChip? Chip(object key) => _chips.FirstOrDefault(c => Equals(c.Key, key));

    public void Select(object? key)
    {
        foreach (var chip in _chips) chip.SetSelected(Equals(chip.Key, key));
    }
}
