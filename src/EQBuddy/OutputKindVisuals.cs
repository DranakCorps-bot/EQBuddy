using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **The ONE builder of a meter's kind marks** — the colour square before a row's name, the
/// stacked mix strip and its legend (Founder's option A, 2026-09-29). Every desktop meter —
/// the widget's Combat and Healing cards, the breakout floats, the Live room and the HUD's
/// expand panel — draws them through here, so the surfaces cannot drift apart in size, order
/// or words; what they SAY and in what order is <see cref="OutputKindPresentation"/>'s, and
/// the colours are the derived <c>Kind*Brush</c> keys of <see cref="ThemeTones"/>.
///
/// Every brush is a RESOURCE REFERENCE rather than a resolved brush, so a theme swap repaints
/// the marks without the meter having to rebuild (and the light/dark kind set is the theme's
/// own choice, made once in <see cref="ThemeTones.Derive"/>).
/// </summary>
internal static class OutputKindVisuals
{
    /// <summary>The square's edge. Small enough to sit inside a 11.5px row without pushing it
    /// taller; large enough that the colour reads as a colour and not a stray pixel.</summary>
    private const double SquareSize = 8;

    /// <summary>The mix strip's height — thin, because it summarises the rows under it and must
    /// never out-shout them.</summary>
    private const double StripHeight = 4;

    /// <summary>The square that stands before a meter row's name. Its <c>Tag</c> is the kind's
    /// token, so a dump or a test can ask which kind a drawn row wears (trap 39).</summary>
    public static FrameworkElement Square(OutputKind kind)
    {
        var sq = new Border
        {
            Width = SquareSize, Height = SquareSize,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, Tok.SpaceS, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = OutputKindPresentation.Token(kind),
            ToolTip = OutputKindPresentation.Label(kind),
        };
        sq.SetResourceReference(Border.BackgroundProperty, OutputKindPresentation.BrushKey(kind));
        return sq;
    }

    /// <summary>The kind tokens a strip built by <see cref="Mix"/> drew, in its order — read
    /// off the segments' tags, for a dump that must describe what is on screen.</summary>
    public static IEnumerable<string> StripTokens(FrameworkElement mix) =>
        mix is Panel host && host.Children.Count > 0 && host.Children[0] is Grid strip
            ? strip.Children.OfType<FrameworkElement>().Select(e => (string)e.Tag)
            : [];

    /// <summary>
    /// The strip and its legend for a meter's rows, or null when <see cref="OutputKindPresentation.Mix"/>
    /// has nothing to explain (no rows, or every row unclassified — an archived session).
    /// The segments are STAR columns sized by share, so the strip is exact at any width
    /// without a SizeChanged pass; the legend wraps (trap 25).
    /// </summary>
    public static FrameworkElement? Mix(IEnumerable<SourceDamage> rows)
    {
        var segments = OutputKindPresentation.Mix(rows);
        if (segments.Count == 0) return null;

        var host = new StackPanel { Margin = new Thickness(0, Tok.SpaceXxs, 2, Tok.SpaceXs) };
        // Square-ended on purpose: a WPF Border's CornerRadius does not clip its children, so
        // "rounded" would mean rounding each segment — a row of beads rather than one bar.
        var strip = new Grid { Height = StripHeight };
        for (var i = 0; i < segments.Count; i++)
        {
            strip.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(segments[i].Share, GridUnitType.Star) });
            var seg = new Border { Tag = OutputKindPresentation.Token(segments[i].Kind) };
            seg.SetResourceReference(Border.BackgroundProperty,
                OutputKindPresentation.BrushKey(segments[i].Kind));
            Grid.SetColumn(seg, i);
            strip.Children.Add(seg);
        }
        host.Children.Add(strip);

        var legend = new WrapPanel { Margin = new Thickness(0, Tok.SpaceXs, 0, 0) };
        var caption = Tok.Spec(Tok.TypeRole.Caption);
        foreach (var s in segments)
        {
            var item = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, Tok.SpaceM, Tok.SpaceXxs),
            };
            item.Children.Add(Square(s.Kind));
            var text = new TextBlock
            {
                Text = OutputKindPresentation.LegendText(s),
                FontSize = caption.Size,
                VerticalAlignment = VerticalAlignment.Center,
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, caption.ColorKey);
            item.Children.Add(text);
            legend.Children.Add(item);
        }
        host.Children.Add(legend);
        // The legend's words are also the strip's hover, for the eye that lands on the strip.
        strip.ToolTip = string.Join(" · ", segments.Select(OutputKindPresentation.LegendText));
        return host;
    }
}
