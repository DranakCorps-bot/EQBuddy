using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Shapes;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// An Epic step's DONE control: an empty circle that becomes a green check (Founder,
/// 2026-09-29 — "it's very confusing having the track checkbox next to every line").
///
/// <para><b>A <see cref="CheckBox"/> with its own template, and that is the whole
/// design.</b> It keeps everything the square box was: keyboard focus, Space to toggle,
/// the UI Automation Toggle pattern, <c>Checked</c>/<c>Unchecked</c> (never <c>Click</c> —
/// a UIA toggle raises none), the row text as clickable content, and the same
/// <c>IsChecked</c> every existing sweep and dump fact reads. Only the PICTURE changes: a
/// ring rather than a square, so it cannot be mistaken for the section heading's square
/// Track tick. Enter toggles too, which a stock CheckBox does not do.</para>
///
/// <para><b>The TYPE is its identity</b> (trap 39): the <c>Tag</c> is already spoken for by
/// the guide-row tag the dump counts rows with, so a sweep asks <c>is StepMark</c> rather than
/// reading a second meaning into a string.</para>
///
/// <para>Every word and ink comes from <see cref="QuestPresentation"/>; this class only
/// draws. The ring is a vector ellipse and the check is <see cref="IconPaths"/>' own path —
/// never a glyph. The mark's target is <see cref="DesignTokens.IconInlineHit"/> square on a
/// transparent ground, so it hit-tests where it is NOT painted too (trap 16), and the whole
/// row stays clickable the way the box's content was.</para>
/// </summary>
internal sealed class StepMark : CheckBox
{
    private static readonly ControlTemplate MarkTemplate = BuildTemplate();

    private Shape? _ring;
    private Shape? _check;
    private FrameworkElement? _mark;

    public StepMark()
    {
        Template = MarkTemplate;
        Checked += (_, _) => Apply();
        Unchecked += (_, _) => Apply();
        MouseEnter += (_, _) => Apply();
        MouseLeave += (_, _) => Apply();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _ring = GetTemplateChild("PART_Ring") as Shape;
        _check = GetTemplateChild("PART_Check") as Shape;
        _mark = GetTemplateChild("PART_Mark") as FrameworkElement;
        Apply();
    }

    /// <summary>Enter toggles as Space does. A stock CheckBox ignores Enter, and a player
    /// walking a thirty-step list by keyboard should not have to know which key a round
    /// control answers to.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && IsEnabled)
        {
            e.Handled = true;
            OnToggle();
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>Paint the mark for the state it is in. Resource REFERENCES, never fetched
    /// brushes, so a theme switch repaints it (a fetched brush is a snapshot).</summary>
    private void Apply()
    {
        var done = IsChecked == true;
        if (_ring is not null)
            _ring.SetResourceReference(Shape.StrokeProperty, done
                ? QuestPresentation.StepMarkDoneInk
                : IsMouseOver && IsEnabled ? "TextBrush" : QuestPresentation.StepMarkOpenInk);
        if (_check is not null)
        {
            _check.SetResourceReference(Shape.FillProperty, QuestPresentation.StepMarkDoneInk);
            _check.Visibility = done ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_mark is not null) _mark.ToolTip = QuestPresentation.StepMarkTip(done);
    }

    private static ControlTemplate BuildTemplate()
    {
        var inv = CultureInfo.InvariantCulture;
        var hit = DesignTokens.IconInlineHit;
        var ring = hit - 2;
        var check = Math.Round(DesignTokens.IconInline * 0.8, 1);
        var xaml =
            "<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" "
            + "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"CheckBox\">"
            + "<Grid Background=\"Transparent\">"
            + "<Grid.ColumnDefinitions><ColumnDefinition Width=\"Auto\"/>"
            + "<ColumnDefinition Width=\"*\"/></Grid.ColumnDefinitions>"
            + string.Format(inv,
                "<Grid x:Name=\"PART_Mark\" Width=\"{0}\" Height=\"{0}\" Background=\"Transparent\" "
                + "VerticalAlignment=\"Top\" Margin=\"0,1,0,0\">", hit)
            + string.Format(inv,
                "<Ellipse x:Name=\"PART_Ring\" Width=\"{0}\" Height=\"{0}\" StrokeThickness=\"1.5\"/>",
                ring)
            + string.Format(inv,
                "<Path x:Name=\"PART_Check\" Width=\"{0}\" Height=\"{0}\" Stretch=\"Uniform\" "
                + "Data=\"{1}\"/>", check, IconPaths.Path(QuestPresentation.StepMarkIcon))
            + "</Grid>"
            + string.Format(inv,
                "<ContentPresenter Grid.Column=\"1\" Margin=\"{0},0,0,0\" "
                + "VerticalAlignment=\"Top\" RecognizesAccessKey=\"False\"/>", DesignTokens.SpaceXs)
            + "</Grid></ControlTemplate>";
        return (ControlTemplate)XamlReader.Parse(xaml);
    }
}
