using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The WPF composition of <see cref="DesignTokens"/> and <see cref="IconPaths"/> — the
/// same job <see cref="ThemeManager"/> does for colour, for the other four axes.
///
/// Nothing here decides anything. Every number and every path comes out of UI.Shared, so
/// the Avalonia lane composes the SAME values into its own styles and the two cannot
/// drift. That is not a theoretical worry: the Avalonia chip stacks once shipped a
/// hand-copied older version of the WPF anchor and carried #122 and #152 to Linux and
/// macOS after Windows had already paid for both.
///
/// Token resources are static — a theme switch repaints, it doesn't re-scale — so this
/// dictionary is built once and merged at startup rather than swapped like the palette.
/// </summary>
internal static class DesignSystem
{
    /// <summary>Builds the token ResourceDictionary: every numeric token as a
    /// <c>double</c> under its own name, plus the <c>CornerRadius</c> and
    /// <c>Thickness</c> shapes XAML can't compute from a double.</summary>
    public static ResourceDictionary Tokens()
    {
        var d = new ResourceDictionary();
        foreach (var (key, value) in DesignTokens.Numbers) d[key] = value;

        // Radii as CornerRadius, so a style says {StaticResource CornerCard} rather than
        // re-typing the number. Named Corner* rather than Radius* so the double and the
        // shape can coexist under obvious names.
        d["CornerPanel"] = new CornerRadius(DesignTokens.RadiusPanel);
        d["CornerCard"] = new CornerRadius(DesignTokens.RadiusCard);
        d["CornerControl"] = new CornerRadius(DesignTokens.RadiusControl);
        d["CornerPill"] = new CornerRadius(DesignTokens.RadiusPill);

        // The spacing scale as Thickness, uniform and per-edge in the combinations the
        // migrated surfaces actually use. Anything not here is a new decision and should
        // be added by name rather than typed inline (that is how 174 distinct Thickness
        // tuples happened).
        d["PadCard"] = new Thickness(DesignTokens.SpaceL, DesignTokens.SpaceM,
            DesignTokens.SpaceL, DesignTokens.SpaceM);
        d["PadRow"] = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS,
            DesignTokens.SpaceM, DesignTokens.SpaceS);
        d["PadControl"] = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXs,
            DesignTokens.SpaceM, DesignTokens.SpaceXs);
        d["PadPill"] = new Thickness(DesignTokens.SpaceL, DesignTokens.SpaceXxs,
            DesignTokens.SpaceL, DesignTokens.SpaceXxs);
        d["PadWindow"] = new Thickness(DesignTokens.SpaceXl);
        d["GapXs"] = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
        d["GapS"] = new Thickness(0, 0, DesignTokens.SpaceS, 0);
        // Theme.xaml's two template arrows (Gate 5d). Named rather than typed inline
        // because they live in shared ControlTemplates and belong to no single card.
        d["ComboArrowInset"] = new Thickness(0, 0, DesignTokens.SpaceM, 0);
        d["SubmenuArrowInset"] = new Thickness(DesignTokens.SpaceL, 0, 0, 0);
        // The SectionLink arrow sits to the RIGHT of its text, so its air is on the LEFT.
        // Reaching for GapS (a right margin) instead jammed it against "Sky 0/222".
        d["LinkArrowInset"] = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        d["StackXs"] = new Thickness(0, 0, 0, DesignTokens.SpaceXs);
        d["StackS"] = new Thickness(0, 0, 0, DesignTokens.SpaceS);
        d["StackM"] = new Thickness(0, 0, 0, DesignTokens.SpaceM);
        d["StackL"] = new Thickness(0, 0, 0, DesignTokens.SpaceL);

        // The widget's own rhythms, named because they were typed inline 30+ times
        // between them and are the reason MainWindow.xaml could not join the ratchet.
        // "A list block" is a row of content with a hairline of air above and a little
        // more below — the single most repeated tuple in the file (17 of them).
        d["ListBlock"] = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXs);
        d["StackXxs"] = new Thickness(0, 0, 0, DesignTokens.SpaceXxs);
        d["LeadXxs"] = new Thickness(0, DesignTokens.SpaceXxs, 0, 0);
        d["LeadXs"] = new Thickness(0, DesignTokens.SpaceXs, 0, 0);
        d["LeadM"] = new Thickness(0, DesignTokens.SpaceM, 0, 0);
        d["IndentM"] = new Thickness(DesignTokens.SpaceM, 0, 0, 0);
        // The four KPI tiles at the top of the widget. Was 11,6,4,7 — hand-nudged, on no
        // scale, and repeated four times; the asymmetry is real (the tiles butt against
        // their dividers) so it is kept, snapped to the scale.
        d["PadKpi"] = new Thickness(DesignTokens.SpaceL, DesignTokens.SpaceS,
            DesignTokens.SpaceXs, DesignTokens.SpaceS);
        d["PadWidget"] = new Thickness(DesignTokens.SpaceM);
        d["KpiRow"] = new Thickness(DesignTokens.SpaceXxs, DesignTokens.SpaceM,
            DesignTokens.SpaceXxs, DesignTokens.SpaceXs);
        // The title bar's little inline gaps. Three near-identical tuples (2,0,8,0 /
        // 2,0,7,0 / 10,0,6,0) became one: a 1px difference between two icons on the same
        // row is drift, not a decision.
        d["TitleGap"] = new Thickness(DesignTokens.SpaceXxs, 0, DesignTokens.SpaceS, 0);
        d["CharGap"] = new Thickness(DesignTokens.SpaceM, 0, DesignTokens.SpaceS, 0);
        // 1px verticals are rendering facts, not rhythm — the ratchet allows 0 and 1 for
        // exactly this, and they stay.
        d["RowTight"] = new Thickness(0, 1, 0, DesignTokens.SpaceXxs);
        d["BadgePad"] = new Thickness(DesignTokens.SpaceXs, 0, DesignTokens.SpaceXs, 1);
        d["SectionLead"] = new Thickness(0, DesignTokens.SpaceS, 0, DesignTokens.SpaceXxs);
        // The two resize grips and the grip's hairline.
        d["GripInset"] = new Thickness(0, 0, DesignTokens.SpaceXxs, DesignTokens.SpaceXxs);
        d["GripBar"] = new Thickness(DesignTokens.SpaceL, 0, DesignTokens.Indent, 0);
        d["GripLine"] = new Thickness(DesignTokens.SpaceXl, 0, DesignTokens.SpaceXl,
            DesignTokens.SpaceXxs);

        // The breakout windows' chrome.
        d["PadBreakout"] = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceS,
            DesignTokens.SpaceM, DesignTokens.SpaceM);
        d["PadScope"] = new Thickness(DesignTokens.SpaceS, 1, DesignTokens.SpaceS, 1);
        d["PadXxs"] = new Thickness(DesignTokens.SpaceXxs);
        d["InsetXs"] = new Thickness(DesignTokens.SpaceXs, 0, 0, 0);
        d["InsetS"] = new Thickness(DesignTokens.SpaceS, 0, 0, 0);
        d["FlankXs"] = new Thickness(DesignTokens.SpaceXs, 0, DesignTokens.SpaceXs, 0);
        d["FlankXxs"] = new Thickness(DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXxs, 0);
        d["FlankS"] = new Thickness(DesignTokens.SpaceS, 0, DesignTokens.SpaceS, 0);
        d["InsetXxsLeft"] = new Thickness(DesignTokens.SpaceXxs, 0, 0, 0);
        d["RowTightLeft"] = new Thickness(1, 0, 0, DesignTokens.SpaceXxs);
        return d;
    }

    // ---- typography ----

    private static readonly Dictionary<DesignTokens.TypeWeight, FontWeight> Weights = new()
    {
        [DesignTokens.TypeWeight.Regular] = FontWeights.Normal,
        [DesignTokens.TypeWeight.SemiBold] = FontWeights.SemiBold,
        [DesignTokens.TypeWeight.Bold] = FontWeights.Bold,
    };

    /// <summary>A TextBlock wearing a type ROLE — size, weight and default ink together,
    /// because "secondary" rendered in the primary ink is not secondary. Callers override
    /// the ink for STATE (a ready row goes GoodBrush) via <see cref="Ink"/>; overriding it
    /// for emphasis is how 612 independent size decisions happened in the first place.</summary>
    public static TextBlock Text(DesignTokens.TypeRole role, string text = "")
    {
        var spec = DesignTokens.Spec(role);
        var block = new TextBlock
        {
            Text = text,
            FontSize = spec.Size,
            FontWeight = Weights[spec.Weight],
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, spec.ColorKey);
        return block;
    }

    /// <summary>Repoints an element's foreground at a palette key, keeping it live across
    /// a theme switch. <c>SetResourceReference</c> rather than a fetched brush: a fetched
    /// brush is a snapshot, and the window would keep the old theme's colour.</summary>
    public static T Ink<T>(this T element, string colorKey) where T : DependencyObject
    {
        switch (element)
        {
            case TextBlock t: t.SetResourceReference(TextBlock.ForegroundProperty, colorKey); break;
            case Control c: c.SetResourceReference(Control.ForegroundProperty, colorKey); break;
            case Shape s: s.SetResourceReference(Shape.FillProperty, colorKey); break;
        }
        return element;
    }

    // ---- icons ----

    /// <summary>One icon from <see cref="IconPaths"/>, as a vector — never a glyph.
    ///
    /// Emoji and dingbats render at a size and weight the app does not control, and PRs
    /// #148 and #166 exist because they failed to render at all in Wine prefixes, on the
    /// platforms that are EQBuddy's only uncontested ground. A Path takes the palette as
    /// its fill and the size we ask for, on every platform.</summary>
    public static Path Icon(string name, string colorKey = "DimBrush",
        double size = 14, double opacity = 1.0)
    {
        var icon = new Path
        {
            Tag = name,
        Data = Geometry.Parse(IconPaths.Path(name)),
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            Opacity = opacity,
            VerticalAlignment = VerticalAlignment.Center,
        };
        icon.SetResourceReference(Shape.FillProperty, colorKey);
        return icon;
    }

    /// <summary>The clickable list-element contract, in one home for rows and badges:
    /// swallow the mouse-down so it can't start a window DragMove and eat the up (#46),
    /// and act on the up ONLY if this element saw the press — a mouse-down handler
    /// elsewhere (a fold header, a chip) can rebuild the list mid-gesture and drop the
    /// up on a brand-new element that was never pressed (LW's live report, 2026-08-17).
    /// The up is Handled even when unmatched: an element's release is never any
    /// ancestor's business.</summary>
    public static void WireClick(FrameworkElement element, Action onClick)
    {
        var pressed = false;
        element.Cursor = Cursors.Hand;
        element.MouseLeftButtonDown += (_, e) => { pressed = true; e.Handled = true; };
        element.MouseLeave += (_, _) => pressed = false;
        element.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (!pressed) return;
            pressed = false;
            onClick();
        };
    }

    /// <summary>An icon that behaves like a button but reads like a glyph — the close /
    /// pin / report family. A real <see cref="Button"/>, so it is keyboard-reachable and
    /// has a hit area, rather than the click-handled TextBlocks these used to be.</summary>
    public static Button IconButton(string name, string tip, RoutedEventHandler onClick,
        string colorKey = "DimBrush", double opacity = 1.0)
    {
        var button = new Button
        {
            Style = (Style)Application.Current.FindResource("EqIconButton"),
            Content = Icon(name, colorKey, opacity: opacity),
            ToolTip = tip,
        };
        button.Click += onClick;
        return button;
    }

    /// <summary>A clickable icon that sits INSIDE a line of text — the loot row's quest
    /// badge and its family. <see cref="IconButton"/> is the same idea at
    /// <see cref="DesignTokens.IconButtonSize"/>, which on the widget would make every
    /// loot row a third taller; this keeps the drawn size and widens only the target.
    ///
    /// The point is the transparent ground the button template paints: a bare
    /// <see cref="Path"/> only receives a click where it is painted, so the map-pin badge
    /// had holes you could click straight through (#211). It is a real Button rather than
    /// a handled Path so it is keyboard-reachable and shows the same hover as the rest of
    /// the icon family.</summary>
    public static Button InlineIconButton(string name, string tip, RoutedEventHandler onClick,
        string colorKey = "DimBrush", double size = DesignTokens.IconInline)
    {
        var button = IconButton(name, tip, onClick, colorKey);
        button.Width = button.Height = DesignTokens.IconInlineHit;
        if (button.Content is Path icon) icon.Width = icon.Height = size;
        return button;
    }

    /// <summary>
    /// **The ⓘ that carries a paragraph Settings used to print underneath a control**
    /// (<see cref="SettingsProsePolicy"/>). One builder rather than a hand-built icon per
    /// surface, because the prose-to-hover pass reaches every Settings tab in turn and the
    /// second copy of this shape is where the two start disagreeing about hit size, ink and
    /// what a click does.
    ///
    /// **The click is not decoration.** A drawn button that answers only a hover is a
    /// silent no-op for anybody clicking it — touch, a trackpad user who taps, or the
    /// keyboard, which cannot hover at all — so the click OPENS the same tooltip the hover
    /// shows. It is one paragraph with two ways in, never a second copy of the text.
    ///
    /// The tooltip is a real <see cref="ToolTip"/> rather than a bare string precisely so
    /// the click has something to open; its duration is still the app-wide bounded one
    /// (<c>ToolTipDefaults</c>, trap 63), because that is a metadata default on
    /// <see cref="DependencyObject"/> and this is one.
    /// </summary>
    public static Button InfoHint(string prose, string colorKey = "DimBrush")
    {
        // StaysOpen=false so the click-opened one is dismissed by the next click anywhere,
        // the way a menu is. Without it a tooltip opened by hand outlives the hover rules
        // that would have closed it.
        var tip = new ToolTip { Content = prose, StaysOpen = false };
        var button = InlineIconButton("Info", prose, (_, _) => tip.IsOpen = true, colorKey);
        button.ToolTip = tip;
        button.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(button, SettingsProsePolicy.HoverAffordanceName);
        return button;
    }

    /// <summary>Re-points an <see cref="InfoHint"/> at a different paragraph, for the ones
    /// whose text a surface rebuilds rather than declares once. Both ways in read the SAME
    /// <see cref="ToolTip"/>, so there is no second copy to leave behind.</summary>
    public static void SetHintProse(Button hint, string prose)
    {
        if (hint.ToolTip is ToolTip tip) tip.Content = prose;
    }

    /// <summary>
    /// **The row an <see cref="InfoHint"/> sits in: the thing it explains, then the ⓘ.**
    /// Pass 1 built this shape privately inside the HUD block; Pass 2 reaches three more
    /// blocks, and a fourth hand-built copy is exactly what <see cref="InfoHint"/>'s own
    /// comment warns about — the place two surfaces start disagreeing about alignment and
    /// wrapping. The HUD block's private helpers now delegate here, so there is ONE
    /// implementation rather than a Pass 1 shape and a Pass 2 shape.
    ///
    /// **A WrapPanel, not a horizontal StackPanel** (trap 25). Both children have
    /// content-driven widths, and a horizontal stack measures with infinite width in its
    /// stacking direction — so at Settings' 390-unit minimum a long label pushes the ⓘ past
    /// the edge with no ellipsis and no error, which is precisely how the Progress window
    /// once shipped three visible tabs out of four. Wrapping puts it on the next line, where
    /// it is still there to hover.
    ///
    /// **The margin belongs to the ROW, never to the explained control.** A checkbox offset
    /// ten units down inside the row would sit ten units below its own ⓘ, which is the one
    /// thing an explanation attached to a control must not look like.
    /// </summary>
    public static UIElement HintRow(FrameworkElement explained, Button hint, Thickness margin)
    {
        var row = new WrapPanel { Margin = margin };
        explained.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(explained);
        row.Children.Add(hint);
        return row;
    }
}

/// <summary>
/// THE selectable pill (gate 2b, docs/DesignSystem.md §11.2). Tabs, the class lens, the
/// quest mode strip, the loot view filter and the loot sort toggle are one shape doing one
/// job, and every one of them was hand-built — 16 across MainWindow.xaml and
/// BreakoutWindow.xaml, the most recent pair arriving in #198 six hours after Gate 2
/// deleted the pattern from the Quest Tracker.
///
/// Geometry and the selected-state vocabulary come from <see cref="ChipStyle"/>, so the
/// Avalonia chip is the same chip rather than a copy that drifts.
/// </summary>
internal sealed class EqChip : Border
{
    private readonly TextBlock _label;
    private readonly TextBlock? _badge;
    private readonly bool _compact;

    /// <summary>What this chip selects — a mode string, a QuestTab, a class name.
    /// Compared by the strip, never interpreted here.</summary>
    public object Key { get; }

    public EqChip(string text, object key, string? badge = null, string? tip = null,
        Action? onClick = null, bool compact = false)
    {
        Key = key;
        _compact = compact;
        // Centered both ways inside the padding box — a segment whose text sits high
        // or leans left reads as misaligned the moment a divider gives it edges.
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _label = DesignSystem.Text(compact ? ChipStyle.CompactLabelRole : ChipStyle.LabelRole, text);
        _label.VerticalAlignment = VerticalAlignment.Center;
        content.Children.Add(_label);
        if (badge is { Length: > 0 })
        {
            _badge = DesignSystem.Text(ChipStyle.BadgeRole, badge);
            _badge.Margin = new Thickness(DesignTokens.SpaceS, 1, 0, 0);
            content.Children.Add(_badge);
        }
        Child = content;
        // Compact is the scope toggle's shape: flush segments (no gap, no edge of
        // their own) inside the strip's shared hairline frame.
        CornerRadius = new CornerRadius(compact ? ChipStyle.CompactRadius : ChipStyle.Radius);
        var pad = compact ? ChipStyle.CompactPadding : ChipStyle.Padding;
        Padding = new Thickness(pad.Left, pad.Top, pad.Right, pad.Bottom);
        Margin = compact
            ? new Thickness(0)
            : new Thickness(0, 0, ChipStyle.Gap.Right, ChipStyle.Gap.Bottom);
        BorderThickness = new Thickness(compact ? 0 : ChipStyle.BorderThickness);
        Cursor = Cursors.Hand;
        if (tip is not null) ToolTip = tip;
        // Handled, or the window's own drag-to-move swallows the click.
        if (onClick is not null)
            MouseLeftButtonDown += (_, e) => { e.Handled = true; onClick(); };
        SetSelected(false);
    }

    public void SetSelected(bool on)
    {
        var ink = _compact ? ChipStyle.ForCompact(on) : ChipStyle.For(on);
        Paint(this, BackgroundProperty, ink.Background);
        Paint(this, BorderBrushProperty, ink.Border);
        _label.Ink(ink.Label);
        if (_badge is null) return;
        _badge.Ink(ink.Badge);
        _badge.Opacity = ink.BadgeOpacity;

        // ChipStyle.None means no brush at all — the compact segments' unfilled state;
        // a resource reference would look the empty key up and paint nothing forever.
        static void Paint(Border b, DependencyProperty prop, string key)
        {
            if (key == ChipStyle.None) b.SetValue(prop, Brushes.Transparent);
            else b.SetResourceReference(prop, key);
        }
    }
}

/// <summary>A row of <see cref="EqChip"/> where exactly one is selected — the segmented
/// control. Owns the "which one is on" bookkeeping every hand-built strip wrote its own
/// copy of, usually as a foreach over a list of tuples.</summary>
internal sealed class EqSegmentedStrip(Panel host, bool compact = false)
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
        var chip = new EqChip(text, key, badge, tip, onClick, compact);
        if (compact && _chips.Count > 0)
        {
            // The hairline divider between segments (LW, 2026-08-18) — the split the
            // group frame implies, drawn, inset so it never touches the frame's
            // rounded corners. Its visibility FOLLOWS its segment's: a strip that
            // withholds a segment (the Loot card withholds "recent") must not strand
            // a doubled divider where it stood.
            var divider = new Border
            {
                Width = ChipStyle.BorderThickness,
                Margin = new Thickness(0, ChipStyle.CompactDividerInset,
                    0, ChipStyle.CompactDividerInset),
            };
            divider.SetResourceReference(Border.BackgroundProperty, "HairlineBrush");
            divider.SetBinding(UIElement.VisibilityProperty,
                new System.Windows.Data.Binding(nameof(Visibility)) { Source = chip });
            host.Children.Add(divider);
        }
        host.Children.Add(chip);
        _chips.Add(chip);
        return chip;
    }

    /// <summary>One chip by its key, or null. A strip sometimes has to hide a segment
    /// rather than disable it — the Loot card withholds "recent" when nothing on screen
    /// carries a timestamp — and reaching for it by key beats every caller keeping its
    /// own field.</summary>
    public EqChip? Chip(object key) => _chips.FirstOrDefault(c => Equals(c.Key, key));

    /// <summary>Paints the selection. Compared with <see cref="object.Equals(object?,
    /// object?)"/> so strips keyed on strings, enums or null all work without the caller
    /// casting.</summary>
    public void Select(object? key)
    {
        foreach (var chip in _chips) chip.SetSelected(Equals(chip.Key, key));
    }
}

/// <summary>One row inside an <see cref="EqMultiPicker"/>.</summary>
/// <param name="Key">What the row selects — a class name, a <c>HelperGoal</c>, a faction's
/// name in the dump's own spelling. Handed straight back to the picker's toggle callback and
/// never interpreted here, the same contract <see cref="EqChip.Key"/> keeps.</param>
/// <param name="Label">What the row reads. Comes from a presentation file; this control
/// writes no words.</param>
/// <param name="Checked">Whether it is ticked when the row is built.</param>
/// <param name="Tip">Hover copy, where the noun has any. The phone has no hover, so a picker
/// that PORTS later re-picks this control rather than copying it (trap 35).</param>
internal readonly record struct PickerRow(
    object Key, string Label, bool Checked, string? Tip = null);

/// <summary>
/// **THE multi-select dropdown** (DRA-71 D2, Fable plan P1; Founder smoke item 1) — a face
/// button that says what is picked, and a themed popup of check rows behind it.
///
/// <para><b>The sibling rule to <see cref="EqChip"/>'s, and it is the same rule.</b> CLAUDE.md
/// has said "the selectable pill is <c>EqChip</c> — never hand-build another one" since gate 2b,
/// because sixteen hand-built strips had drifted apart before anyone counted them. Multi-select
/// had no such primitive at all: the app's one dropdown multi-select was a <c>Popup</c> of
/// <c>CheckBox</c>es typed into <c>QuestsView.xaml</c>, and the Helper's goals were raw chips in
/// a <c>WrapPanel</c> — the "flat checkbox soup" the Founder named when he smoked D1. So:
/// <b>the multi-select dropdown is <see cref="EqMultiPicker"/> — never hand-build another
/// one.</b> The quest window's class picker is migrated onto it in the same slice that adds it,
/// which is the only way that sentence starts out true.</para>
///
/// <para><b>NOTHING HERE DECIDES A WORD OR A COLOUR.</b> The face text comes from
/// <see cref="PickerFace"/>, the row labels from a presentation file, and every brush is an
/// existing theme key looked up by reference — so a theme switch repaints the popup and the
/// eight palettes cannot disagree about it. The chrome is deliberately the SAME four values
/// the hand-built class popup used (<c>PopupBrush</c>, <c>CornerCard</c>, <c>BorderBrush</c>,
/// <c>PadCard</c>): a migration that also restyled the control would make "behaves identically"
/// unprovable by eye.</para>
///
/// <para><b>Two ways in, because two hosts need different ones.</b> A host that has already
/// placed its face in a layout (the quest window's <c>ClassBtn</c>, which sits in a fixed Grid
/// column beside the mode strip) hands it in; a host building a block in code gets one made for
/// it. Either way the picker owns the popup, and <see cref="Host"/> is the single element the
/// caller adds to its tree — a popup left out of a logical tree resolves none of its theme
/// brushes and would paint as an unstyled box in all eight palettes.</para>
///
/// <para><b>Ticks sync without rebuilding.</b> <see cref="SetRows"/> builds the list;
/// <see cref="SetChecked"/> paints the ticks. They are separate because the quest window syncs
/// the saved selection on every refresh, and a rebuild there would throw away a popup the
/// player had open mid-click. The re-entrancy guard lives here rather than in each host, which
/// is where it was before and where only one of the two hosts had one.</para>
/// </summary>
internal sealed class EqMultiPicker
{
    /// <summary>How tall the row list may get before it scrolls. Twelve rows is the Helper's
    /// own faction cap and comfortably more than the nine goals; the class list is sixteen and
    /// scrolls. The wheel belongs to the popup here and there is no trap-36 conflict — a popup
    /// is its own top-level surface, not a view lifted into a host that already scrolls.</summary>
    private static readonly double MaxRows = DesignTokens.RowHeight * 12;

    private readonly System.Windows.Controls.Primitives.Popup _popup = new();
    private readonly StackPanel _rows = new();
    private readonly List<CheckBox> _checks = [];
    private readonly Action<object> _onToggle;
    private bool _syncing;

    /// <summary>The button the player clicks. Owned by this picker when it built one, and
    /// merely wired when the host handed one in.</summary>
    public Button Face { get; }

    /// <summary>The one element a host adds to its own tree. It carries the popup always, and
    /// the face too when this picker made it — so a caller cannot add one and forget the
    /// other, which is a picker that opens nothing.</summary>
    public Panel Host { get; }

    public bool IsOpen => _popup.IsOpen;

    /// <summary>How many rows the popup holds. Reported in the <c>EQBUDDY_EXPAND</c> dump by
    /// the rooms that draw one: a control that is ABSENT photographs as an unremarkable panel,
    /// so only a launched app can say the rows are there (trap 29).</summary>
    public int RowCount => _checks.Count;

    /// <param name="onToggle">Called with the row's <see cref="PickerRow.Key"/> when the player
    /// ticks or unticks it. Never called by <see cref="SetChecked"/>.</param>
    /// <param name="face">An already-placed face to adopt, or null to have one built.</param>
    /// <param name="tip">Hover copy for a built face. Ignored for an adopted one, whose host
    /// owns its tooltip.</param>
    public EqMultiPicker(Action<object> onToggle, Button? face = null, string? tip = null)
    {
        _onToggle = onToggle;

        if (face is not null) Face = face;
        else
        {
            Face = new Button
            {
                Content = "",
                FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Caption).Size,
                Height = DesignTokens.ControlHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(DesignTokens.SpaceL, DesignTokens.SpaceXxs,
                    DesignTokens.SpaceL, DesignTokens.SpaceXxs),
            };
            // The same style the quest window's face has worn since #184, so the two pickers
            // are one control rather than two that look alike.
            Face.SetResourceReference(FrameworkElement.StyleProperty, "ActionButton");
            if (tip is { Length: > 0 }) Face.ToolTip = tip;
        }
        Face.Click += (_, _) => _popup.IsOpen = !_popup.IsOpen;

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = MaxRows,
            Content = _rows,
        };
        var frame = new Border
        {
            CornerRadius = new CornerRadius(DesignTokens.RadiusCard),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(DesignTokens.SpaceL, DesignTokens.SpaceM,
                DesignTokens.SpaceL, DesignTokens.SpaceM),
            Child = scroll,
        };
        frame.SetResourceReference(Panel.BackgroundProperty, "PopupBrush");
        frame.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        _popup.PlacementTarget = Face;
        _popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        // A click anywhere else closes it. The alternative — a popup that stays until its own
        // face is clicked again — strands an open panel over the room the player just moved to.
        _popup.StaysOpen = false;
        _popup.AllowsTransparency = true;
        _popup.Child = frame;

        var host = new StackPanel { Orientation = Orientation.Horizontal };
        if (face is null) host.Children.Add(Face);
        host.Children.Add(_popup);
        Host = host;
    }

    /// <summary>Builds (or rebuilds) the row list.</summary>
    public void SetRows(IReadOnlyList<PickerRow> rows)
    {
        _syncing = true;
        _rows.Children.Clear();
        _checks.Clear();
        foreach (var row in rows)
        {
            var check = new CheckBox
            {
                Margin = new Thickness(0, 1, 0, 1),
                Content = DesignSystem.Text(DesignTokens.TypeRole.Body, row.Label),
                IsChecked = row.Checked,
                Tag = row.Key,
            };
            if (row.Tip is { Length: > 0 }) check.ToolTip = row.Tip;
            check.Checked += OnRowChanged;
            check.Unchecked += OnRowChanged;
            _checks.Add(check);
            _rows.Children.Add(check);
        }
        _syncing = false;
    }

    /// <summary>Paints the ticks from the caller's own store, without rebuilding the rows and
    /// without calling back — the sync half of trap 20's writer/reader pair.</summary>
    public void SetChecked(Func<object, bool> isOn)
    {
        _syncing = true;
        foreach (var check in _checks) check.IsChecked = isOn(check.Tag!);
        _syncing = false;
    }

    /// <summary>What is ticked, in ROW order rather than click order — so a caller that stores
    /// the result gets the list its own rows are in, whichever way the player got there.</summary>
    public IReadOnlyList<object> Checked =>
        [.. _checks.Where(c => c.IsChecked == true).Select(c => c.Tag!)];

    /// <summary>The face's words. Capped by <see cref="PickerFace"/> at the call site, which is
    /// where the noun is.</summary>
    public void SetFace(string text, string? tip = null)
    {
        Face.Content = text;
        if (tip is not null) Face.ToolTip = tip;
    }

    /// <summary>Opens the popup. The review hook: a dropdown that is SHUT photographs as a
    /// button, so the state this control exists for can only be staged (trap 22).</summary>
    public void Open() => _popup.IsOpen = true;

    private void OnRowChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _onToggle(((CheckBox)sender).Tag!);
    }
}
