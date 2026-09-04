using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>One tab as this card needs to draw it — the strip's label and the badge the
/// theme window already computes. A flat shape rather than each theme's own header record
/// so one card class can serve all four (<c>ProgressTabHeader</c> and its siblings differ
/// only in the enum they carry).</summary>
internal sealed record ThemeCardTab<TTab>(TTab Tab, string Label, string? Value)
    where TTab : struct, Enum;

/// <summary>
/// A THEME CARD on the widget: the launcher line, and under it — when the player expands
/// it — the theme's tab strip and the selected room's body. Inline themes (David,
/// 2026-08-21: *"expandable sub-categories under them with an option to pop out the
/// window"*; Bevel's ruling and host rules, Helm-signed 2026-08-22).
///
/// **The card never decides where the body lives.** <see cref="ThemeHost{TTab}"/> does,
/// and this class is one of its two outputs. That is not tidiness: the two UIs own theme
/// bodies differently, and on Avalonia drawing one surface in the card and the window at
/// once THROWS (one control, one visual parent). Keeping the decision in a tested,
/// framework-free state machine is what stops each click handler having an opinion —
/// trap 15, which shipped a correct-and-invisible filter strip because a control and its
/// host both had a switch for one state.
///
/// **A Glance tab never renders its full view.** <see cref="InlineMode.Glance"/> means one
/// line and a pop-out, so expanding a theme can never cost what opening its window costs —
/// Bevel's host rule: *"do not shrink-wrap the full window onto a SizeToContent
/// always-on-top panel."* Raids is the Progress theme's Glance tab, and Drops is Glance
/// because it READS THE WIKI, which an expanded card over a running game must not do.
///
/// Bodies are built LAZILY, on the first expand. A player who never opens a theme pays
/// nothing for it, which is the rule the widget's collapsed cards have always followed.
/// </summary>
internal sealed class ThemeCardView<TTab> where TTab : struct, Enum
{
    private readonly Expander _section;
    private readonly ThemeHost<TTab> _host;
    private readonly Func<StatsSnapshot, IReadOnlyList<ThemeCardTab<TTab>>> _tabs;
    private readonly Func<TTab, InlineMode> _modeFor;
    private readonly Func<TTab, UIElement> _bodyFor;
    private readonly Func<TTab, StatsSnapshot, string> _glanceFor;
    private readonly Action<TTab, StatsSnapshot> _render;
    private readonly Action _popOut;
    private readonly Action _bringWindowForward;

    /// <summary>Asked for this body's cap, given how much of the card is NOT the body.
    /// The widget owns the answer because only it can see the rest of the stack; this
    /// class owns the one measurement the widget cannot reach (#250).</summary>
    private readonly Func<double, double> _bodyCap;

    private readonly StackPanel _root = new();
    private readonly WrapPanel _stripHost = new();
    private readonly EqSegmentedStrip _strip;
    private readonly ContentControl _bodyHost = new();
    private readonly ScrollViewer _bodyScroll;
    private readonly TextBlock _glance = CardParts.Summary();

    /// <summary>Full bodies, kept once built — a tab switch must not rebuild element trees
    /// that nothing changed about (the same rule <c>ProgressWindow</c> follows for its
    /// Wealth panel).</summary>
    private readonly Dictionary<TTab, UIElement> _built = [];

    public ThemeCardView(
        Expander section,
        ContentControl bodyHost,
        ThemeHost<TTab> host,
        Func<StatsSnapshot, IReadOnlyList<ThemeCardTab<TTab>>> tabs,
        Func<TTab, InlineMode> modeFor,
        Func<TTab, UIElement> bodyFor,
        Func<TTab, StatsSnapshot, string> glanceFor,
        Action<TTab, StatsSnapshot> render,
        Action popOut,
        Action bringWindowForward,
        string popOutTip,
        Func<double, double> bodyCap)
    {
        (_section, _host, _tabs, _modeFor, _bodyFor, _glanceFor, _render) =
            (section, host, tabs, modeFor, bodyFor, glanceFor, render);
        (_popOut, _bringWindowForward, _bodyCap) = (popOut, bringWindowForward, bodyCap);

        _strip = new EqSegmentedStrip(_stripHost);
        _stripHost.Margin = new Thickness(0, 0, 0, Tok.SpaceS);

        // The cap Bevel asked for, WITH a scroller, because a MaxHeight without one is a
        // clip: the body would simply stop, with nothing on screen saying there is more.
        //
        // It starts at the FLOOR and follows the height grip from the first render
        // (ApplyBodyCap). A widget nobody has dragged never leaves this number.
        _bodyScroll = new ScrollViewer
        {
            Content = _bodyHost,
            MaxHeight = WidgetMetrics.ThemeBodyMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        PassWheelUpWhenItCannotScroll(_bodyScroll);

        _glance.Visibility = Visibility.Collapsed;
        _root.Children.Add(_stripHost);
        _root.Children.Add(_glance);
        _root.Children.Add(_bodyScroll);
        // The host ContentControl gets no Visibility and no Margin of its own — the
        // lifted control carries both (trap 15).
        bodyHost.Content = _root;

        // The POP-OUT, on the expanded header only (Bevel). ArrowUpRight rather than a new
        // glyph: it is the mark the launcher used for "this opens a window", and the card
        // is now the only place that meaning still needs saying.
        PopOutButton = DesignSystem.InlineIconButton("ArrowUpRight", popOutTip, (_, e) =>
        {
            // A Button inside the Section template's header sits INSIDE its ToggleButton,
            // so an unhandled click reaches the toggle and collapses the card underneath
            // the window that just opened.
            e.Handled = true;
            _popOut();
        });
        PopOutButton.Visibility = Visibility.Collapsed;

        _section.Expanded += (_, _) => OnHeaderToggled();
        _section.Collapsed += (_, _) => OnHeaderToggled();
    }

    /// <summary>The ⧉ for the card's header row. The caller places it, because the header
    /// is XAML the widget owns — this class only decides when it shows.</summary>
    public Button PopOutButton { get; }

    /// <summary>The strip's chip count and the selected room, for the
    /// <c>EQBUDDY_EXPAND</c> dump. They answer for the CARD what
    /// <c>ProgressWindow.DebugFacts</c> answers for the window, and only one of the two
    /// hosts is ever reporting (see MainWindow's dump).</summary>
    public int TabCount => _strip.Count;

    public TTab SelectedTab => _host.SelectedTab;

    /// <summary>The body's cap right now, for the <c>EQBUDDY_EXPAND</c> dump — 320 on a
    /// widget nobody has dragged, more once they have (#250). Read off the CONTROL, so it
    /// is what the body is actually held to rather than what was computed for it.</summary>
    public double BodyCap => _bodyScroll.MaxHeight;

    /// <summary>The measurement this card last handed the widget: everything the card
    /// occupies except the body being capped. One of the two inputs behind
    /// <see cref="BodyCap"/>, exposed so the dump can report what the arithmetic was fed —
    /// a cap on its own can only be checked against a constant, and the constant would be
    /// a claim about the monitor (see <c>ThemeBodyCapHost.RoomFor</c>).</summary>
    public double BodyChrome { get; private set; } = double.NaN;

    /// <summary>The open room's list is longer than the cap allows, so there IS something
    /// a taller widget would show. The height grip's tooltip asks, because "everything you
    /// selected is on screen" stopped being the same statement as "a drag buys you
    /// nothing" the moment the cap started following the drag.</summary>
    public bool BodyIsCapped =>
        _host.IsInline && _bodyScroll.Visibility == Visibility.Visible
        && _bodyScroll.ScrollableHeight > 0.5;

    /// <summary>
    /// Point the body's cap at the height the player dragged (#250, Paineless).
    ///
    /// Runs on every render of an expanded card — not on a clock, which matters: trap 12
    /// is a timer that changes a measured size on a <c>SizeToContent</c> always-on-top
    /// window, and #173 is what that cost a player. This number moves when the layout
    /// moves. The assignment is skipped unless it actually changed, and
    /// <see cref="WidgetMetrics.ThemeBodyCap"/> answers in whole units, so a sub-pixel
    /// wobble cannot reach the windowing system at all.
    ///
    /// The measurement handed over is everything this CARD occupies except the body
    /// itself — header, tab strip, padding. Subtracting the body is what stops the cap
    /// feeding back into its own input: the difference does not move when the cap does.
    /// </summary>
    private void ApplyBodyCap()
    {
        var ownChromeAndHeader = _section.ActualHeight
                                 + _section.Margin.Top + _section.Margin.Bottom
                                 - _bodyScroll.ActualHeight;
        BodyChrome = ownChromeAndHeader;
        var cap = _bodyCap(ownChromeAndHeader);
        if (Math.Abs(_bodyScroll.MaxHeight - cap) > 0.5) _bodyScroll.MaxHeight = cap;
    }

    /// <summary>Drive the expander FROM the host, rather than reading it as truth.
    ///
    /// <see cref="Expander.IsExpanded"/> is an output here, never an input: the state
    /// machine says whether this card owns the body, and everything on screen follows.
    /// Setting it re-enters the Expanded/Collapsed handlers, so the guard below keeps the
    /// two from arguing.</summary>
    private bool _syncing;

    public void Sync()
    {
        _syncing = true;
        try
        {
            _section.IsExpanded = _host.IsInline;
            PopOutButton.Visibility = _host.IsInline ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _syncing = false; }
    }

    private void OnHeaderToggled()
    {
        if (_syncing) return;
        _host.ToggleCard();
        // "They clicked the card while its window is up." The host answers with a
        // bring-forward rather than a second copy of the surface — on Avalonia the second
        // copy is an exception, and here it would be one surface in two hosts with only
        // one of them owning its state.
        if (_host.ShouldBringWindowForward) _bringWindowForward();
        Sync();
    }

    /// <summary>Paint the expanded card from this tick. Called only while the card owns
    /// the body — a collapsed theme costs nothing, and a theme whose body is in its window
    /// is that window's to paint.</summary>
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
        // Chips first, THEN the paint — colouring before rebuilding the chip list leaves
        // every fresh chip unstyled, including the selected one, which is the whole signal.
        _strip.Select(_host.SelectedTab);

        var selected = _host.SelectedTab;
        if (_modeFor(selected) == InlineMode.Glance)
        {
            // One line and the ⧉ that is already on the header. The full view is never
            // built, let alone rendered, which is the point of the mode.
            _glance.Text = _glanceFor(selected, s);
            _glance.Visibility = Visibility.Visible;
            _bodyScroll.Visibility = Visibility.Collapsed;
            _bodyHost.Content = null;
            return;
        }

        _glance.Visibility = Visibility.Collapsed;
        _bodyScroll.Visibility = Visibility.Visible;
        if (!_built.TryGetValue(selected, out var body))
            _built[selected] = body = _bodyFor(selected);
        _bodyHost.Content = body;
        _render(selected, s);
        ApplyBodyCap();
    }

    /// <summary>
    /// Give the wheel back to the card stack when this body has nothing to scroll.
    ///
    /// Trap 36: a child <see cref="ScrollViewer"/> HANDLES the wheel whether or not it can
    /// act on it, so a capped body that happens to be shorter than its cap silently stops
    /// the widget's own <c>SectionScroll</c> from moving under the pointer. The Inventory
    /// tab shipped exactly that and could only be moved by dragging the outer slider —
    /// and nothing shows it: not a diff, not a test, not a screenshot. You find it by
    /// putting a mouse on it.
    ///
    /// The boundary cases are included deliberately: a body that CAN scroll but is already
    /// at its top still owes an upward wheel to the stack behind it, which is the half of
    /// the problem <c>GearCardView</c>'s scroller still has.
    /// </summary>
    internal static void PassWheelUpWhenItCannotScroll(ScrollViewer scroll)
    {
        scroll.PreviewMouseWheel += (_, e) =>
        {
            var atTop = scroll.VerticalOffset <= 0;
            var atBottom = scroll.VerticalOffset >= scroll.ScrollableHeight;
            var stuck = scroll.ScrollableHeight <= 0
                        || (e.Delta > 0 && atTop)
                        || (e.Delta < 0 && atBottom);
            if (!stuck) return;
            if (scroll.Parent is not UIElement parent) return;

            e.Handled = true;
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = scroll,
            });
        };
    }
}
