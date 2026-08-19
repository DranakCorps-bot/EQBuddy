using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// One icon, addressable from XAML (Gate 5c): <c>&lt;local:EqIcon Glyph="Settings"/&gt;</c>.
///
/// <see cref="DesignSystem.Icon"/> already builds these in code, and the migrated windows
/// use it — but the widget's chrome is XAML, and its icons were written as
/// <c>&lt;TextBlock Text="&amp;#x2699;" FontSize="13"/&gt;</c>: a glyph and a size, in
/// markup, where neither can come from the scale. That is the last place in the app where
/// a control is drawn with a character, and it is on the surface that is always on screen
/// and on the builds where emoji have twice failed to render at all (#148, #166).
///
/// The property is called <c>Glyph</c> rather than <c>Name</c> because <c>Name</c> is
/// WPF's own, and shadowing it in XAML is a trap for whoever writes the next one.
/// </summary>
internal sealed class EqIcon : System.Windows.Controls.ContentControl
{
    // WPF's Path is sealed, so this WRAPS one rather than being one. The wrapper is also
    // what lets Size mean "square", which is the only sizing an icon on this grid has.
    private readonly Path _path = new()
    {
        Stretch = Stretch.Uniform,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public EqIcon()
    {
        Content = _path;
        Size = DesignTokens.IconInline;
        VerticalAlignment = VerticalAlignment.Center;
        Apply();
    }

    /// <summary>An <see cref="IconPaths"/> name. Unknown names fall back to a neutral
    /// marker rather than throwing: a mistyped icon should look plain, not take the widget
    /// down at startup — and <c>IconGeometryTests</c> is what actually catches the typo.
    ///
    /// **A DependencyProperty, and that is what unblocked Gate 5d.** The last glyphs in the
    /// app live inside shared <c>ControlTemplates</c> in Theme.xaml — the expander chevron
    /// that flips ▸/▾, the star that fills ☆/★ — and they are swapped by template TRIGGERS.
    /// A <c>Setter</c> can only target a DependencyProperty, so as a plain CLR property
    /// this control could replace a static glyph and not a stateful one, which is most of
    /// them. Same shape as the EqCardRows lookups that unblocked the card bodies: the
    /// migration was not stuck on effort, it was stuck on a missing capability.</summary>
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(EqIcon),
            new PropertyMetadata("Info", (d, _) => ((EqIcon)d).Apply()));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>A <see cref="ThemePalettes"/> key. Live through a theme switch: a fetched
    /// brush is a snapshot and would keep the old theme's colour.</summary>
    public static readonly DependencyProperty InkProperty =
        DependencyProperty.Register(nameof(Ink), typeof(string), typeof(EqIcon),
            new PropertyMetadata("DimBrush", (d, e) =>
                ((EqIcon)d)._path.SetResourceReference(Shape.FillProperty, (string)e.NewValue)));

    public string Ink
    {
        get => (string)GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    /// <summary>Square size in device-independent units. Defaults to the inline-icon
    /// token; the chrome's larger touch targets say so explicitly.</summary>
    public double Size
    {
        get => _path.Width;
        set { _path.Width = value; _path.Height = value; }
    }

    private void Apply()
    {
        if (IconPaths.Names.Contains(Glyph))
            _path.Data = Geometry.Parse(IconPaths.Path(Glyph));
        _path.SetResourceReference(Shape.FillProperty, Ink);
    }
}
