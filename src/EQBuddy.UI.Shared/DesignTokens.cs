namespace EQBuddy.UI.Shared;

/// <summary>
/// Typography, spacing, shape and control size as DATA — the other half of what
/// <see cref="ThemePalettes"/> already does for colour, and for the same reason.
///
/// The Gate 1 audit (docs/DesignSystem.md §1) counted 13 distinct font sizes across 612
/// assignments, 174 distinct Thickness tuples and 7 corner radii, with zero named roles.
/// Half-point sizes (9.5, 10.5, 11.5, 12.5) are the tell: each was nudged to make one
/// row fit, and nothing in the codebase could detect that two rows had disagreed. This
/// table is the vocabulary that replaces them.
///
/// It lives here, framework-free, so WPF composes it into a ResourceDictionary and
/// Avalonia into Styles from the SAME numbers. The alternative — each UI holding its own
/// copy — is exactly how the Avalonia chip stacks shipped a hand-copied older anchor and
/// carried #122 and #152 to Linux after Windows had already paid for both.
///
/// Sizes are in device-independent units (WPF and Avalonia agree on those). Nothing here
/// is a screen pixel: on the widget, content sits under a UI-scale LayoutTransform and
/// that conversion belongs to <see cref="WidgetMetrics"/> (trap 1, #144).
/// </summary>
public static class DesignTokens
{
    // ---- typography: 7 roles, replacing 13 sizes ----

    /// <summary>What a piece of text IS, not how big it is. The colour is part of the
    /// role — "secondary" that renders in the primary ink is not secondary — so a role
    /// names a palette key rather than leaving each call site to pick one.</summary>
    public enum TypeRole
    {
        /// <summary>The window title row.</summary>
        TitleWindow,
        /// <summary>Card and group headings.</summary>
        TitleSection,
        /// <summary>The one number a surface exists to show.</summary>
        Metric,
        /// <summary>Rows and list content.</summary>
        Body,
        /// <summary>Detail lines under a row (NPC · drop location).</summary>
        BodySecondary,
        /// <summary>Filters, chips, counts.</summary>
        Caption,
        /// <summary>Footnotes, provenance, the accuracy contract.</summary>
        Metadata,
    }

    /// <summary>Weight, named rather than numbered, because WPF and Avalonia spell the
    /// numbers differently and neither spelling belongs in a shared table.</summary>
    public enum TypeWeight { Regular, SemiBold, Bold }

    /// <param name="Size">Device-independent units.</param>
    /// <param name="Weight">See <see cref="TypeWeight"/>.</param>
    /// <param name="ColorKey">A <see cref="ThemePalettes"/> key — the role's default ink.
    /// A call site may override it for STATE (a ready row goes GoodBrush); overriding it
    /// for emphasis is how 612 independent decisions happened.</param>
    public readonly record struct TypeSpec(double Size, TypeWeight Weight, string ColorKey);

    /// <summary>The scale. Every text site in a migrated surface resolves to one of
    /// these seven; the half-point sizes, and 7pt (below the readable floor), do not
    /// survive a migration.</summary>
    public static readonly IReadOnlyDictionary<TypeRole, TypeSpec> Type =
        new Dictionary<TypeRole, TypeSpec>
        {
            [TypeRole.TitleWindow] = new(14, TypeWeight.SemiBold, "TextBrush"),
            [TypeRole.TitleSection] = new(12.5, TypeWeight.SemiBold, "TextBrush"),
            [TypeRole.Metric] = new(16, TypeWeight.Bold, "AccentBrush"),
            [TypeRole.Body] = new(12, TypeWeight.Regular, "TextBrush"),
            [TypeRole.BodySecondary] = new(11.5, TypeWeight.Regular, "DimBrush"),
            [TypeRole.Caption] = new(11, TypeWeight.Regular, "DimBrush"),
            [TypeRole.Metadata] = new(10, TypeWeight.Regular, "DimBrush"),
        };

    public static TypeSpec Spec(TypeRole role) => Type[role];

    // ---- spacing: a 6-step scale ----
    //
    // Chosen to absorb the head of the existing distribution (1, 4, 6, 8, 10, 12
    // dominate) with the least visual disturbance. Density is a feature here — these
    // surfaces get read mid-pull — so the scale tops out low on purpose.

    public const double SpaceXxs = 2;
    public const double SpaceXs = 4;
    public const double SpaceS = 6;
    public const double SpaceM = 8;
    public const double SpaceL = 12;
    public const double SpaceXl = 16;

    /// <summary>The ad-hoc left indent that appeared 14 times as <c>(20,2,0,0)</c>,
    /// named so the fifteenth use is the same as the first.</summary>
    public const double Indent = 20;

    // ---- shape: 4 radii ----

    /// <summary>Windows and popups.</summary>
    public const double RadiusPanel = 10;
    /// <summary>Cards, list rows, detail panels.</summary>
    public const double RadiusCard = 6;
    /// <summary>Buttons, inputs, combos.</summary>
    public const double RadiusControl = 6;
    /// <summary>Chips, badges, filter pills. David's call, 2026-08-11: pills yes, ovals
    /// no — this is a rounded rect at the height of one, not a capsule.</summary>
    public const double RadiusPill = 11;

    // ---- content bounds — widths that cap CONTENT, not rhythm values ----

    /// <summary>How wide a stat-block tooltip may get before it wraps — the width at
    /// which a monospace item block stops being a column and starts being a paragraph.
    /// It was declared as a private 340 in four files at once (review catch,
    /// 2026-08-18), and the magic-number scan can't see MaxWidth.</summary>
    public const double TipWidth = 340;

    // ---- control sizes ----

    public const double RowHeight = 24;
    public const double ControlHeight = 26;
    public const double IconButtonSize = 24;

    /// <summary>An icon that sits INSIDE a line of text — the quest badge on a loot row,
    /// the marker beside a heading. It is not a button and must not set the row's height:
    /// <see cref="IconButtonSize"/> would make every loot row a third taller, and on the
    /// widget row height is window height.</summary>
    public const double IconInline = 12;

    /// <summary>The hit target of an inline icon that is CLICKABLE, which is larger than
    /// the icon drawn inside it.
    ///
    /// A vector only hit-tests where it is painted. The emoji these replaced were
    /// TextBlocks, and a TextBlock hit-tests over its whole layout rect — so converting
    /// the loot row's quest badge from a glyph to a <see cref="IconInline"/> Path turned
    /// a solid square into the green strokes of a map pin, with dead space between them
    /// (#211, n3cr0nk1tt3n). Nothing about that shows in a diff: the icon is in the right
    /// place, the right colour and the right size, and the handler is attached.
    ///
    /// 16 rather than <see cref="IconButtonSize"/> for the reason above — it fits inside
    /// one line of body text, so the target grows and the row does not.</summary>
    public const double IconInlineHit = 16;

    /// <summary>The state rule down a list row's leading edge — the substitute for the
    /// quest-type icon the mockups drew, which cannot be sourced (docs/DesignSystem.md
    /// §8a). Carries <c>ready</c> / <c>in progress</c> / <c>done</c>, which is real.</summary>
    public const double StateRuleWidth = 3;

    /// <summary>Name → value for every numeric token above, so a UI can compose the whole
    /// set in a loop and a test can assert the two UIs got the same list rather than
    /// trusting that someone typed it twice correctly. Names are the resource keys.</summary>
    public static readonly IReadOnlyDictionary<string, double> Numbers =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["SpaceXxs"] = SpaceXxs,
            ["SpaceXs"] = SpaceXs,
            ["SpaceS"] = SpaceS,
            ["SpaceM"] = SpaceM,
            ["SpaceL"] = SpaceL,
            ["SpaceXl"] = SpaceXl,
            ["Indent"] = Indent,
            ["RadiusPanel"] = RadiusPanel,
            ["RadiusCard"] = RadiusCard,
            ["RadiusControl"] = RadiusControl,
            ["RadiusPill"] = RadiusPill,
            ["RowHeight"] = RowHeight,
            ["ControlHeight"] = ControlHeight,
            ["IconButtonSize"] = IconButtonSize,
            ["IconInline"] = IconInline,
            ["IconInlineHit"] = IconInlineHit,
            ["StateRuleWidth"] = StateRuleWidth,
            ["FontTitleWindow"] = 14,
            ["FontTitleSection"] = 12.5,
            ["FontMetric"] = 16,
            ["FontBody"] = 12,
            ["FontBodySecondary"] = 11.5,
            ["FontCaption"] = 11,
            ["FontMetadata"] = 10,
        };

    /// <summary>The resource key a UI composes for a type role's size — the one place
    /// the <c>Font</c> + role-name convention is spelled, so neither UI invents its
    /// own.</summary>
    public static string FontKey(TypeRole role) => "Font" + role;
}
