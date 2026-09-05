using System.Windows;
using System.Windows.Controls;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// One row of the Evolved shell's navigation rail: an icon, a label, and the selection.
///
/// **Its two widths are one object, on purpose.** The rail degrades from icon+label to
/// icon-only below a threshold (<see cref="ShellLayoutPolicy"/>), and the obvious way to
/// build that is two lists of rows swapped over — which is two hand-maintained
/// descriptions of one rail that will disagree the day a room is added to one of them.
/// Here the label is a child that hides, so a row cannot exist in one state and not the
/// other, and the row ORDER cannot change with the width. Collapsing must never reorder
/// or drop a room: that would turn a resize into a silent capability loss, which is the
/// #219/#233 shape arriving through a window edge instead of a release.
///
/// The icon is a vector from <see cref="IconPaths"/>, never a glyph — an emoji renders at
/// a size and weight the app does not control and boxes outright in some Wine prefixes
/// (#148, #166). It stays drawn in the collapsed state, which is why the row keeps a
/// tooltip: the room's name is one hover away rather than gone.
/// </summary>
internal sealed class RailRow : Border
{
    private readonly TextBlock _label;
    private readonly System.Windows.Shapes.Path _icon;

    public RailRow(ShellPage page, Action onClick)
    {
        Height = Tok.RailRowHeight;
        CornerRadius = new CornerRadius(Tok.RadiusControl);
        Margin = new Thickness(Tok.SpaceXs, Tok.SpaceXxs, Tok.SpaceXs, Tok.SpaceXxs);
        Padding = new Thickness(Tok.SpaceS, 0, Tok.SpaceS, 0);
        // The label names the room and the tooltip says what the room is FOR — the second
        // is what carries the row once the first is hidden.
        ToolTip = $"{ShellPages.Label(page)} — {ShellPages.Describe(page)}";

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _icon = DesignSystem.Icon(ShellPages.IconName(page), size: Tok.IconInlineHit);
        row.Children.Add(_icon);

        _label = DesignSystem.Text(Role.Body, ShellPages.Label(page));
        _label.Margin = new Thickness(Tok.SpaceM, 0, 0, 0);
        _label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(_label);
        Child = row;

        DesignSystem.WireClick(this, onClick);
        Select(false);
    }

    /// <summary>Paint the selection. The selected row takes the panel ground and accent
    /// ink; the rest stay dim, so the rail reads at a glance rather than by comparison.
    /// Set through resource references so a theme swap repaints it.</summary>
    public void Select(bool on)
    {
        if (on)
        {
            SetResourceReference(BackgroundProperty, "PanelHoverBrush");
            _icon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AccentBrush");
            _label.Ink("AccentBrush");
        }
        else
        {
            Background = null;
            _icon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "DimBrush");
            _label.Ink("TextBrush");
        }
    }

    /// <summary>Axis 1 of the degrade: the label goes, the icon and the tooltip stay.
    /// Reversible glance information, not lost capability — closer to a ribbon collapsing
    /// than to a card disappearing.</summary>
    public void ShowLabel(bool on) =>
        _label.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
}
