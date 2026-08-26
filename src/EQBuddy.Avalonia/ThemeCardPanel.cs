using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy.Avalonia;

/// <summary>One tab as this card needs to draw it — the WPF twin's shape verbatim, so the
/// two lanes' theme cards consume the same header data.</summary>
internal sealed record ThemeCardTab<TTab>(TTab Tab, string Label, string? Value)
    where TTab : struct, Enum;

/// <summary>
/// A THEME CARD on this widget: the launcher line, and under it — when the player expands
/// it — the theme's tab strip and the selected room's body. The Avalonia half of Inline
/// themes PR 1 (the plan's PR B); <c>EQBuddy/ThemeCardView.cs</c> is the WPF twin and
/// carries the fuller commentary.
///
/// **The card never decides where the body lives** — <see cref="ThemeHost{TTab}"/> does,
/// and <see cref="IsExpanded"/> here is an OUTPUT of that state machine with its setter
/// routed back through it. This matters more on this lane than on WPF: a control has one
/// visual parent, so a card and a window drawing one surface at once is a crash, not a
/// layout bug. Since PR A each host builds its OWN surfaces
/// (<c>IProgressHost.NewProgressSurfaces()</c>), so the card's bodies and the window's
/// bodies are different instances and the one-owner rule is about the PLAYER never seeing
/// two of one surface, exactly as on WPF.
///
/// It subclasses <see cref="SectionCard"/> so the card stack, the Options show/hide pass
/// and <c>EQBUDDY_EXPAND</c> treat it like every sibling.
/// </summary>
internal sealed class ThemeCardPanel<TTab> : SectionCard where TTab : struct, Enum
{
    private readonly ThemeHost<TTab> _host;
    private readonly Func<StatsSnapshot, IReadOnlyList<ThemeCardTab<TTab>>> _tabs;
    private readonly Func<TTab, InlineMode> _modeFor;
    private readonly Func<TTab, Control> _bodyFor;
    private readonly Func<TTab, StatsSnapshot, string> _glanceFor;
    private readonly Action<TTab, StatsSnapshot> _render;
    private readonly Action _bringWindowForward;

    private readonly PathIcon _chevron;
    private readonly Border _bodyBorder;
    private readonly WrapPanel _stripHost = new() { Margin = new Thickness(0, 0, 0, Tok.SpaceS) };
    private readonly EqSegmentedStrip _strip;
    private readonly ContentControl _bodyHost = new();
    private readonly ScrollViewer _bodyScroll;
    private readonly TextBlock _glance;
    private readonly Button _popOut;

    /// <summary>Full bodies, kept once built — a tab switch must not rebuild element
    /// trees nothing changed about.</summary>
    private readonly Dictionary<TTab, Control> _built = [];

    public ThemeCardPanel(
        Control header,
        ThemeHost<TTab> host,
        Func<StatsSnapshot, IReadOnlyList<ThemeCardTab<TTab>>> tabs,
        Func<TTab, InlineMode> modeFor,
        Func<TTab, Control> bodyFor,
        Func<TTab, StatsSnapshot, string> glanceFor,
        Action<TTab, StatsSnapshot> render,
        Action popOut,
        Action bringWindowForward,
        string popOutTip,
        double bodyMaxHeight)
    {
        (_host, _tabs, _modeFor, _bodyFor, _glanceFor, _render) =
            (host, tabs, modeFor, bodyFor, glanceFor, render);
        _bringWindowForward = bringWindowForward;

        Background = AppTheme.PanelBrush;
        CornerRadius = new CornerRadius(6);
        Margin = new Thickness(0, 2, 0, 0);

        _strip = new EqSegmentedStrip(_stripHost);
        _glance = CardParts.EmptyLine("");
        _glance.IsVisible = false;

        _bodyScroll = new ScrollViewer
        {
            Content = _bodyHost,
            MaxHeight = bodyMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        PassWheelUpWhenItCannotScroll(_bodyScroll);

        // The ⧉, on the expanded header only (Bevel's rule, same as WPF). The caller's
        // popOut (ShowProgressWindow and siblings) drives the host, exactly as on WPF —
        // this button only asks. The header's own PointerPressed guard is what keeps the
        // click from also toggling the card underneath.
        _popOut = DesignSystem.InlineIconButton("ArrowUpRight", popOutTip, popOut);
        _popOut.IsVisible = false;

        _chevron = AppTheme.Icon(AppIcon.ChevronRight, AppTheme.DimBrush, 15);
        _chevron.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        _chevron.Margin = new Thickness(6, 0, 0, 0);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        headerGrid.Children.Add(header);
        Grid.SetColumn(_popOut, 1);
        headerGrid.Children.Add(_popOut);
        Grid.SetColumn(_chevron, 2);
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
            if (args.Source is Button || (args.Source as Visual)?.FindAncestorOfType<Button>() is not null)
                return;
            OnHeaderToggled();
            args.Handled = true;
        };

        _bodyBorder = new Border
        {
            Padding = new Thickness(10, 0, 10, 8),
            Child = new StackPanel { Children = { _stripHost, _glance, _bodyScroll } },
            IsVisible = false,
        };

        Child = new StackPanel { Children = { headerBorder, _bodyBorder } };
    }

    /// <summary>Truth is the host's; the visuals follow it. The SETTER exists because the
    /// stack's shared machinery (Options show/hide, <c>EQBUDDY_EXPAND</c>) assigns
    /// IsExpanded on every card — it routes through the same transitions a header click
    /// takes, so no caller can put the card in a state the machine does not have.</summary>
    public override bool IsExpanded
    {
        get => _host.IsInline;
        set
        {
            if (value == _host.IsInline) return;
            OnHeaderToggled();
        }
    }

    /// <summary>Chip count and selected room for the debug dump — the card's half of what
    /// <c>ProgressWindow.DebugFacts()</c> answers for the window.</summary>
    public int TabCount => _strip.Count;

    public TTab SelectedTab => _host.SelectedTab;

    public void Sync()
    {
        var inline = _host.IsInline;
        var changed = _bodyBorder.IsVisible != inline;
        _bodyBorder.IsVisible = inline;
        _popOut.IsVisible = inline;
        _chevron.Data = StreamGeometry.Parse(inline
            ? "M7.41 8.59 12 13.17l4.59-4.58L18 10l-6 6-6-6 1.41-1.41Z"
            : "M8.59 16.59 13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41Z");
        // OPEN edge only. The consumer's whole purpose is "render a just-expanded card
        // now instead of waiting out the render gate"; raising on the COLLAPSE edge (a
        // pop-out, a window close) queues a live repaint that can land on top of state
        // someone else just painted — which is how this card's first cut wiped a theme
        // window's freshly rendered rows with an empty live snapshot.
        if (changed && inline) RaiseExpandedChanged(true);
    }

    private void OnHeaderToggled()
    {
        _host.ToggleCard();
        if (_host.ShouldBringWindowForward) _bringWindowForward();
        Sync();
    }

    /// <summary>Paint the expanded card from this tick — same contract as the WPF twin:
    /// only while the card owns the body, and a Glance tab draws its one line and never
    /// builds its full view.</summary>
    public void Render(StatsSnapshot s)
    {
        if (!_host.IsInline) return;

        _strip.Clear();
        foreach (var tab in _tabs(s))
        {
            var t = tab.Tab;
            _strip.Add(tab.Label, t, tab.Value, onClick: () =>
            {
                _host.SelectTab(t);
                Render(s);
            });
        }
        _strip.Select(_host.SelectedTab);

        var selected = _host.SelectedTab;
        if (_modeFor(selected) == InlineMode.Glance)
        {
            _glance.Text = _glanceFor(selected, s);
            _glance.IsVisible = true;
            _bodyScroll.IsVisible = false;
            _bodyHost.Content = null;
            return;
        }

        _glance.IsVisible = false;
        _bodyScroll.IsVisible = true;
        if (!_built.TryGetValue(selected, out var body))
            _built[selected] = body = _bodyFor(selected);
        _bodyHost.Content = body;
        _render(selected, s);
    }

    /// <summary>
    /// Trap 36, this lane's version: a child ScrollViewer eats the wheel whether or not it
    /// can act on it, so a body shorter than its cap stops the widget's own card stack
    /// from scrolling under the pointer. Tunnel-handled so the check runs BEFORE the
    /// scroller, and a wheel it cannot use moves the nearest ancestor scroller instead.
    /// </summary>
    private static void PassWheelUpWhenItCannotScroll(ScrollViewer scroll)
    {
        scroll.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) =>
        {
            var atTop = scroll.Offset.Y <= 0.5;
            var atBottom = scroll.Offset.Y >= scroll.Extent.Height - scroll.Viewport.Height - 0.5;
            var stuck = scroll.Extent.Height <= scroll.Viewport.Height + 0.5
                        || (e.Delta.Y > 0 && atTop)
                        || (e.Delta.Y < 0 && atBottom);
            if (!stuck) return;
            if (scroll.FindAncestorOfType<ScrollViewer>() is not { } outer) return;
            e.Handled = true;
            outer.Offset = outer.Offset.WithY(
                Math.Max(0, outer.Offset.Y - e.Delta.Y * 50));
        }, RoutingStrategies.Tunnel);
    }
}
