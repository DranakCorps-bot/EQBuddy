using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// A fold heading: a chevron that points down when open and right when shut, beside its
/// words. The Linux/macOS twin of <c>EQBuddy/EqFoldLabel.cs</c>, kept API-compatible with
/// it (<see cref="Open"/>, <see cref="Text"/>, <see cref="Section"/>, <see cref="Set"/>)
/// so the two call sites read the same.
///
/// **The chevrons it replaces were "▾"/"▸" typed into strings**, which is the rule's whole
/// target: a glyph renders at a size and weight the app does not control, and PRs #148 and
/// #166 exist because some failed to render AT ALL in Wine prefixes. That argument is at
/// its strongest right here — this is the build that RUNS on those prefixes, and it kept
/// the glyphs for months after the Windows one gave them up.
///
/// Two things carried over from the WPF version because they were learned the hard way:
/// the heading look is expressed in <see cref="DesignTokens"/> rather than borrowed from a
/// style (converting the WPF folds without it rendered them as body text — bigger and
/// brighter than every other heading, visible only in a screenshot), and the chevron takes
/// the same ink as the words, or a dim heading grows a bright arrow.
/// </summary>
internal sealed class EqFoldLabel : StackPanel
{
    private readonly PathIcon _chevron;
    private readonly TextBlock _label;
    private bool _open = true;
    private string _ink = "TextBrush";

    public EqFoldLabel()
    {
        Orientation = Orientation.Horizontal;
        // Safe as a StackPanel: a fold heading is a few words that never wrap, so the
        // infinite-width measure that CLIPS wrapping text (trap 14) cannot bite. Put
        // wrapping text beside an icon and it must be a two-column Grid instead.
        _chevron = DesignSystem.Icon("ChevronDown", _ink, DesignTokens.IconInline);
        _chevron.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
        _chevron.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_chevron);
        _label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        Children.Add(_label);
    }

    /// <summary>Open folds point down, shut folds point right.</summary>
    public bool Open
    {
        get => _open;
        set
        {
            _open = value;
            _chevron.Data = StreamGeometry.Parse(
                IconPaths.Path(value ? "ChevronDown" : "ChevronRight"));
        }
    }

    public string Text
    {
        get => _label.Text ?? "";
        set => _label.Text = value;
    }

    /// <summary>A <see cref="ThemePalettes"/> key applied to BOTH halves, so the chevron
    /// cannot end up a different colour from the words beside it.</summary>
    public string Ink
    {
        get => _ink;
        set
        {
            _ink = value;
            _chevron.Foreground = AppTheme.BrushFor(value);
            _label.Foreground = AppTheme.BrushFor(value);
        }
    }

    /// <summary>Wear the folded-section heading look rather than body text. Expressed in
    /// tokens rather than by borrowing a style — see the class note.</summary>
    public bool Section
    {
        get;
        set
        {
            field = value;
            if (!value) return;
            var spec = DesignTokens.Spec(DesignTokens.TypeRole.Metadata);
            _label.FontSize = spec.Size;
            _label.FontWeight = FontWeight.SemiBold;
            Ink = "DimBrush";   // both halves, or a dim heading grows a bright arrow
        }
    }

    /// <summary>Set both at once — the shape every call site actually wants, and the one
    /// that makes "open" and the words impossible to disagree.</summary>
    public void Set(bool open, string text)
    {
        Open = open;
        Text = text;
    }
}
