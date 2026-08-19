namespace EQBuddy.UI.Shared;

/// <summary>
/// The selectable pill — what it is made of, and what it looks like selected.
///
/// This is the app's most-rebuilt shape. The Gate 1 audit did not count it because it
/// does not look like a duplicate in a diff: a tab strip, a class lens and a mode strip
/// are three different features, so three people (and one of them twice) built a
/// `TextBlock` with a `Tag`, a click handler, a literal font size and a literal margin,
/// and coloured it from a hand-written `ApplyVisual`. **There are 16 of them across
/// MainWindow.xaml and BreakoutWindow.xaml alone**, and #198 added the most recent pair
/// six hours after Gate 2 removed the pattern from the Quest Tracker.
///
/// Gate 2 replaced those with one primitive and then left it as a private nested class
/// inside `QuestsWindow` — reachable by nobody, which is how the seventeenth copy gets
/// written. The geometry and the state vocabulary live here now; each UI keeps its own
/// thin control (a WPF `Border` and an Avalonia `Border` are not the same object, and
/// pretending otherwise is what shared XAML would cost us — see §5).
/// </summary>
public static class ChipStyle
{
    /// <summary>Chips, badges and filter pills. David's call, 2026-08-11: pills yes,
    /// ovals no — a rounded rect at the height of one, never a capsule.</summary>
    public const double Radius = DesignTokens.RadiusPill;

    /// <summary>Padding inside a chip: generous across, tight down. A segmented strip is
    /// read left-to-right, so horizontal room is what separates the segments and vertical
    /// room is only cost — and on the widget, cost is window height.</summary>
    public static readonly (double Left, double Top, double Right, double Bottom) Padding =
        (DesignTokens.SpaceL, DesignTokens.SpaceXxs, DesignTokens.SpaceL, DesignTokens.SpaceXxs);

    /// <summary>Gap to the next chip, and to the next ROW when a strip wraps — the class
    /// strip can hold all sixteen classes and a fixed-width window clipped it (#184).</summary>
    public static readonly (double Right, double Bottom) Gap =
        (DesignTokens.SpaceXs, DesignTokens.SpaceXs);

    public const double BorderThickness = 1;

    /// <summary>The label's type role. Captions, not body: a strip is navigation, and it
    /// must not compete with the content it filters.</summary>
    public const DesignTokens.TypeRole LabelRole = DesignTokens.TypeRole.Caption;

    /// <summary>The dense variant for the breakout windows, and it is NOT a shrunken
    /// pill: it is the Target|Session toggle's own vocabulary, the compact segmented
    /// control that chrome already established (LW, 2026-08-18 — "use the style
    /// already used by target/session"). Segments sit flush inside one hairline
    /// group frame; unselected wears no fill at all; selected gets the toggle
    /// highlight wash with accent text. Same bookkeeping, same tooltips, a fraction
    /// of the card pill's weight — right for a ~270px window where the strip must
    /// not compete with the rows it filters.</summary>
    public static readonly (double Left, double Top, double Right, double Bottom) CompactPadding =
        (DesignTokens.SpaceS, 1, DesignTokens.SpaceS, 1);

    /// <summary>Compact chips label in Metadata — the size the scope toggle's own
    /// segments render at, so the two controls read as one family.</summary>
    public const DesignTokens.TypeRole CompactLabelRole = DesignTokens.TypeRole.Metadata;

    /// <summary>The group frame's (and each segment highlight's) radius — the scope
    /// toggle's own.</summary>
    public const double CompactRadius = DesignTokens.RadiusControl;

    /// <summary>"No brush at all" — a compact segment paints no fill and no edge of
    /// its own; the group's shared hairline frame is the only chrome. Each UI maps
    /// this to its transparent brush.</summary>
    public const string None = "";

    /// <summary>Compact ink: the scope toggle's states. Selected = the toggle
    /// highlight wash under accent text (the one thing on the strip carrying accent,
    /// §2.1's discipline); unselected = dim text on nothing.</summary>
    public static Ink ForCompact(bool selected) => selected
        ? new("ToggleHighlightBrush", None, "AccentBrush", "AccentBrush", 0.85)
        : new(None, None, "DimBrush", "DimBrush", 1.0);

    /// <summary>Hairline dividers between compact segments (LW, 2026-08-18): the split
    /// the frame's own border implies, drawn — inset from the frame's rounded corners
    /// by this much so a divider never touches the curve.</summary>
    public const double CompactDividerInset = DesignTokens.SpaceXxs;

    /// <summary>The optional trailing count ("0 / 486").</summary>
    public const DesignTokens.TypeRole BadgeRole = DesignTokens.TypeRole.Metadata;

    /// <param name="Background">Fill.</param>
    /// <param name="Border">Edge.</param>
    /// <param name="Label">The chip's own text.</param>
    /// <param name="Badge">The trailing count, where there is one.</param>
    /// <param name="BadgeOpacity">Selected, the badge rides on the accent and would shout
    /// as loudly as the label; a touch of transparency keeps the hierarchy.</param>
    public readonly record struct Ink(
        string Background, string Border, string Label, string Badge, double BadgeOpacity);

    /// <summary>
    /// Selected is the ONE thing on a strip allowed to carry the accent (§2.1's discipline:
    /// accent means selected, primary action, or the single most important number). The
    /// accent fills the pill and the text inverts onto the window ground.
    ///
    /// Unselected is deliberately not "nothing": it keeps a raised fill and a hairline, so
    /// the row reads as a set of controls rather than a line of prose — which was the
    /// complaint that started this ("I couldn't tell they were tabs at first glance",
    /// David, 2026-08-15). A strip where three things are gold selects nothing; a strip
    /// where nothing is filled is a sentence.
    /// </summary>
    public static Ink For(bool selected) => selected
        ? new("AccentBrush", "AccentBrush", "BgBrush", "BgBrush", 0.85)
        : new("RaisedBrush", "HairlineBrush", "TextBrush", "DimBrush", 1.0);
}
