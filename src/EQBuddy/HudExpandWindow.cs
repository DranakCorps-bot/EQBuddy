using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// THE UNDER-BAR PANEL (OE-1) — one tracker's detail, drawn directly beneath the collapsed
/// HUD bar, in a companion window slaved to the bar's position.
///
/// **Why a companion window and not a panel inside the widget — this is the whole shape of
/// the feature.** The obvious reading of "expand under the bar" is a panel in the widget's
/// own visual tree, and that is the one thing trap 12 / #173 forbids: the widget is
/// <c>SizeToContent</c>, so a body that appears on a HOVER or grows on a TIMER is a
/// geometry change on an always-on-top transparent window stacked over a fullscreen game.
/// That mechanism cost KoboldCoterie EverQuest's keyboard. <see cref="HudChipRowWindow"/>
/// (Surface A / SA-2, Helm-signed 2026-09-05) is the precedent and this is the second user
/// of it: same chrome family, same <see cref="HudChipRow.Placement"/> arithmetic, and the
/// same default — **slaved, with no geometry of its own.** Where it goes is recomputed from
/// the widget every tick, so there is no saved position to go stale and nothing for a
/// <c>Closed</c> handler to record (trap 2).
///
/// **OE-8 gives it a park and OE-1b lock 3 gives it a width, and both are the PLAYER's.**
/// <c>AppSettings.HudPanelParkLeft</c>/<c>Top</c> and <c>HudPanelWidth</c> are NaN until a
/// drag ends, and NaN means "slaved" and "the OE-7 width" respectively — so a reset profile
/// is today's app by construction, with no migration to run twice (trap 55). Both are written
/// at the END of a gesture and by nothing else: of trap 49's three actors, only the player's
/// has an end. The grip is the whole box (lock 1), the two vertical edges resize (lock 3),
/// and <see cref="HudDragGrip"/> holds the first while <see cref="OnEdgePress"/> claims the
/// press before it on an edge.
///
/// **The motion is a RenderTransform, deliberately** (owner lock 10 — "slick, smooth,
/// professional"). A <see cref="ScaleTransform"/> is post-layout: it changes what is painted
/// and never what is measured, so a 160 ms grow-down animates the panel without asking the
/// window manager to resize anything sixty times a second. Animating the window's own Height
/// would have been the same #173 mechanism one window over.
///
/// **Built in code, not XAML**, like <see cref="HudChipRowWindow"/> and
/// <see cref="ClickThroughChip"/>: there is no designer surface here worth a BAML pair, and
/// an incremental WPF build can leave a stale assembly with a fresh timestamp (trap 18),
/// which is a hazard a code-built window does not carry.
/// </summary>
internal sealed class HudExpandWindow : Window
{
    private readonly MainWindow _main;
    private readonly AppSettings _settings;
    private readonly HudExpandBar _bar;
    private readonly Border _chrome;
    private readonly ScaleTransform _grow = new(1, 0);
    private readonly TextBlock _title;
    private readonly EqIcon _icon;
    private readonly TextBlock _subtext;
    private readonly StackPanel _rows;
    private readonly Button _popOut;

    private string _signature = "";
    private HudExpandTarget? _drawn;
    private bool _closing;

    /// <summary>Rows currently drawn in the body — the <c>hudExpandRows</c> dump fact.
    /// Recorded here rather than counted off the panel because the body also carries the
    /// empty-state line, and "one row" and "one apology" are different states (trap 20's
    /// shape: the thing you are looking for is what is not there).</summary>
    public int RowCount { get; private set; }

    /// <summary>
    /// WHICH SURFACE the rows below the header came from — the <c>hudExpandBody</c> dump
    /// fact, and the one that stops <c>hudExpand</c> being a claim about the HEADER alone.
    ///
    /// The header's title and vector come from the target; the rows come from whichever
    /// builder <see cref="Render"/> picked. Those are two decisions, and OE-7 turned a
    /// two-way pick into a seven-way one — so a target routed to the wrong body renders a
    /// panel that says "Pet damage" over the Damage meter's rows, which is correct-looking
    /// on screen, correct in a screenshot, and correct on every count assertion. Trap 24's
    /// lesson (a title is not an identity) one layer in.
    ///
    /// Recorded where the rows are BUILT rather than derived from the target, or it would
    /// agree with the header by construction and prove nothing (trap 39).
    /// </summary>
    public string BodyKind { get; private set; } = "none";

    /// <summary>The pointer is over the panel itself. A peek must survive the trip from the
    /// chip to the panel — otherwise the panel collapses out from under the cursor that is
    /// reaching for its ⧉, which is a hover expand that cannot be used.</summary>
    public bool PointerInside { get; private set; }

    public HudExpandWindow(MainWindow main, AppSettings settings, HudExpandBar bar)
    {
        _main = main;
        _settings = settings;
        _bar = bar;
        // The title is an IDENTITY the screenshot harness matches on (trap 24), so it must
        // not collide with a sibling window of the same process: the widget is "EQBuddy",
        // the chip row is "EQBuddy HUD Chips" and the Evolved shell is "EQBuddy — <room>".
        Title = "EQBuddy HUD Panel";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        NoActivate.Attach(this);

        // OE-8 affordance face (Bevel): same reuse as HudChipRowWindow — the app's own
        // Cursor+ToolTip grip language (MainWindow's HeightGrip/ResizeGrip), not a drawn
        // handle competing with the header's icon/⧉/✕. SizeAll is the default; OnHoverCursor
        // swaps to SizeWE over the two vertical resize edges so the edge reads as an edge
        // before the player commits to a press.
        Cursor = Cursors.SizeAll;
        ToolTip = "Drag to place this panel anywhere. The left and right edges resize it. "
            + "Right-click the widget → Edit HUD… → Follow the HUD again brings it back.";

        _chrome = new Border
        {
            CornerRadius = new CornerRadius(Tok.RadiusCard),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(Tok.SpaceM, Tok.SpaceS, Tok.SpaceM, Tok.SpaceS),
            Width = PanelWidth,
            RenderTransform = _grow,
            RenderTransformOrigin = new Point(0.5, 0),
        };
        _chrome.SetResourceReference(Border.BackgroundProperty, "BgBrush");
        _chrome.SetResourceReference(Border.BorderBrushProperty, "HairlineBrush");
        Content = _chrome;

        var stack = new StackPanel();
        _chrome.Child = stack;

        // Header: icon · title · ⧉ · ✕ — locks 5 and 6, both on the panel where the pointer
        // already is. A two-column Grid and not a horizontal StackPanel: a stack measures
        // with INFINITE width in the stacking direction, so a long title would push the two
        // buttons off the edge with no ellipsis and nothing on screen to say so (trap 14).
        var header = new Grid { Margin = new Thickness(0, 0, 0, Tok.SpaceXs) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _icon = new EqIcon
        {
            Glyph = HudExpand.Icon(HudExpandTarget.Dps),
            Size = Tok.IconInline,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Tok.SpaceXs, 0),
        };
        header.Children.Add(_icon);
        _title = new TextBlock
        {
            FontSize = Tok.Spec(Tok.TypeRole.TitleSection).Size,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        Grid.SetColumn(_title, 1);
        header.Children.Add(_title);
        // ArrowUpRight, the same vector every theme card's ⧉ wears (ThemeCardView) — a
        // pop-out that looked different here would read as a different verb.
        _popOut = DesignSystem.InlineIconButton("ArrowUpRight",
            HudExpand.PopOutTip(HudExpandTarget.Dps), (_, _) => _bar.PopOut());
        Grid.SetColumn(_popOut, 2);
        header.Children.Add(_popOut);
        var close = DesignSystem.InlineIconButton("Close",
            "Collapse this back into the bar", (_, _) => _bar.Collapse());
        Grid.SetColumn(close, 3);
        header.Children.Add(close);
        stack.Children.Add(header);

        _subtext = new TextBlock
        {
            FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, Tok.SpaceXs),
        };
        _subtext.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        stack.Children.Add(_subtext);

        _rows = new StackPanel();
        stack.Children.Add(_rows);

        // The pointer crossing from the chip onto the panel must not read as "away".
        MouseEnter += (_, _) => { PointerInside = true; _bar.PointerOnPanel(true); };
        MouseLeave += (_, _) => { PointerInside = false; _bar.PointerOnPanel(false); };

        ChipScale.Apply(this, settings.ChipScale);
        WindowZoom.Route(this, () => settings.ChipScale, main.SetChipScale);

        // ORDER IS THE INTERLOCK between the two gestures (OE-1b locks 1 and 3). The edge
        // handler is attached FIRST and sets Handled on an edge press, and a Preview handler
        // registered afterwards with handledEventsToo:false is skipped once that has
        // happened — so a press on a vertical edge resizes and a press anywhere else moves,
        // with no shared flag between them. ResizeZones' own doc drew this line ("edges are
        // deliberately thin so the title row still drags"); this is that line, one window
        // over.
        PreviewMouseLeftButtonDown += OnEdgePress;
        PreviewMouseMove += OnEdgeMove;
        PreviewMouseLeftButtonUp += OnEdgeRelease;
        MouseMove += OnHoverCursor;
        _grip = HudDragGrip.Attach(this, (left, top) =>
        {
            _settings.HudPanelParkLeft = left;
            _settings.HudPanelParkTop = top;
            _mode = HudChipRow.HudParkMode.Parked;
            // Clamp first, then record where it landed — see HudChipRowWindow's grip for the
            // park this order exists to prevent (a corner above the work area that the
            // restore rule then correctly refuses, so the park never comes back).
            Park();
            _settings.HudPanelParkLeft = Left;
            _settings.HudPanelParkTop = Top;
            _main.PersistSettings();
            // The chip row sits BELOW this panel while both are slaved. A panel that has just
            // left that line frees the space, so the row has to be told on the same gesture
            // rather than a tick later.
            _main.RefreshHudChips();
        });
        ApplyWidth();
    }

    private readonly HudDragGrip _grip;

    // ---- FREE PLACEMENT (OE-8) -------------------------------------------------------

    /// <summary>Slaved / parked / parked-where-this-desk-cannot-show, resolved once from the
    /// profile and afterwards only by a drag. <see cref="HudChipRowWindow"/> carries the same
    /// three states for the same reasons; the two windows park independently because they are
    /// two windows, and per-WINDOW is the granularity the plan settled on (per-family would
    /// be the independently-positioned floats SA-2 was signed to end).</summary>
    private HudChipRow.HudParkMode? _mode;

    private HudChipRow.HudParkMode Mode => _mode ??= HudChipRow.ParkMode(
        _settings.HudPanelParkLeft, _settings.HudPanelParkTop,
        ScreenGuard.OnScreen(_settings.HudPanelParkLeft, _settings.HudPanelParkTop,
            ActualWidth, ActualHeight));

    /// <summary>The <c>hudPanelPark</c> dump fact — the EFFECT, off the window.</summary>
    public string ParkKey => Mode == HudChipRow.HudParkMode.Parked
        ? HudChipRow.ParkKey(Left, Top) : "slaved";

    /// <summary>The <c>hudPanelParkSaved</c> fact — what the PROFILE holds, which is a
    /// different claim (trap 42) and the only way to see the unreachable rule working.
    /// </summary>
    public string ParkSavedKey =>
        HudChipRow.ParkKey(_settings.HudPanelParkLeft, _settings.HudPanelParkTop);

    public bool IsParked => Mode == HudChipRow.HudParkMode.Parked;

    /// <summary>The grip's presses and finished drags, as "P,D" — the <c>hudPanelGrip</c>
    /// dump fact, for the reason <see cref="HudDragGrip.PressCount"/> gives.</summary>
    public string GripKey => $"{_grip.PressCount},{_grip.DragCount}";

    /// <summary>"Follow the HUD again" — clears the pair to NaN, which IS slaved, so the
    /// panel goes back to being recomputed from the bar rather than parked at wherever the
    /// bar happens to be standing this second.</summary>
    public void Unpark()
    {
        _settings.HudPanelParkLeft = double.NaN;
        _settings.HudPanelParkTop = double.NaN;
        _mode = HudChipRow.HudParkMode.Slaved;
        Park();
    }

    // ---- THE TAKEN WIDTH (OE-1b lock 3) ----------------------------------------------

    private int _edge;                 // ResizeZones.Left / .Right while a resize is running
    private double _edgeStartWidth;
    private double _edgeStartLeft;
    private Point _edgeStartScreen;

    /// <summary>The width the panel is actually drawing at — the <c>hudPanelWidth</c> dump
    /// fact. Read off the chrome rather than off the setting, because "a width is in the
    /// profile" and "the panel is that wide" are different claims (trap 42) and the clamp
    /// against the monitor sits between them.</summary>
    public double DrawnWidth => _chrome.Width;

    /// <summary>The setting, clamped to the monitor this panel is on, applied to the chrome.
    /// The window is <c>SizeToContent</c>, so setting the chrome's width IS resizing the
    /// window — and it is the only way to do it that leaves the height content-driven.
    /// </summary>
    private void ApplyWidth()
    {
        var area = Mode == HudChipRow.HudParkMode.Parked
            ? ScreenGuard.WorkAreaAt(this, _settings.HudPanelParkLeft, _settings.HudPanelParkTop)
            : SystemParameters.WorkArea;
        _chrome.Width = HudChipRow.PanelWidth(_settings.HudPanelWidth, PanelWidth, area.Width);
    }

    /// <summary>A press on a vertical edge starts a width drag and takes the press away from
    /// the move grip. The HORIZONTAL edges are deliberately not offered: this panel's height
    /// is its rows, capped at <see cref="MaxRows"/> because it is a peek and the ↗ carries
    /// the full list — a height a player could take would be a promise of more rows that the
    /// body has no way to keep.</summary>
    private void OnEdgePress(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1) return;
        var at = e.GetPosition(this);
        var zone = ResizeZones.Hit(at.X, at.Y, ActualWidth, ActualHeight);
        _edge = zone is ResizeZones.Left or ResizeZones.TopLeft or ResizeZones.BottomLeft
            ? ResizeZones.Left
            : zone is ResizeZones.Right or ResizeZones.TopRight or ResizeZones.BottomRight
                ? ResizeZones.Right : ResizeZones.None;
        if (_edge == ResizeZones.None) return;
        _edgeStartWidth = _chrome.Width;
        _edgeStartLeft = Left;
        _edgeStartScreen = PointToScreen(at);
        CaptureMouse();
        e.Handled = true;   // the interlock: HudDragGrip never sees this press
    }

    /// <summary>The edge tell, before any press — SizeWE over the two vertical resize zones
    /// (lock 3), SizeAll everywhere else on the box (OE-8 affordance face, Bevel). Skipped
    /// mid-gesture: a drag or a resize already committed to one cursor, and re-hit-testing
    /// under the moving pointer would flicker it as the pointer crosses back over an edge it
    /// is no longer negotiating.</summary>
    private void OnHoverCursor(object sender, MouseEventArgs e)
    {
        if (_grip.Dragging || _edge != ResizeZones.None) return;
        var at = e.GetPosition(this);
        var zone = ResizeZones.Hit(at.X, at.Y, ActualWidth, ActualHeight);
        Cursor = zone is ResizeZones.Left or ResizeZones.TopLeft or ResizeZones.BottomLeft
            or ResizeZones.Right or ResizeZones.TopRight or ResizeZones.BottomRight
            ? Cursors.SizeWE : Cursors.SizeAll;
    }

    private void OnEdgeMove(object sender, MouseEventArgs e)
    {
        if (_edge == ResizeZones.None) return;
        if (e.LeftButton != MouseButtonState.Pressed) { OnEdgeRelease(sender, null!); return; }
        var delta = PointToScreen(e.GetPosition(this)).X - _edgeStartScreen.X;
        var width = HudChipRow.PanelWidthFromDrag(_edgeStartWidth, delta, _settings.ChipScale,
            _edge == ResizeZones.Right ? 1 : -1);
        _chrome.Width = width;
        // A left-edge drag anchors the RIGHT edge, which is what makes that edge feel like
        // the one being held. Only meaningful while parked: a slaved panel's Left is the
        // widget's and Park() puts it straight back next tick.
        if (_edge == ResizeZones.Left && Mode == HudChipRow.HudParkMode.Parked)
            Left = _edgeStartLeft + (_edgeStartWidth - width) * _settings.ChipScale;
        e.Handled = true;
    }

    /// <summary>RESIZE END — the one moment a width may be written, for the same reason drag
    /// end is the one moment a park may be (trap 49 by construction: the toolkit's
    /// <c>SizeToContent</c> and the tick's re-render have no end to fire on).</summary>
    private void OnEdgeRelease(object sender, MouseButtonEventArgs? e)
    {
        if (_edge == ResizeZones.None) return;
        _edge = ResizeZones.None;
        if (IsMouseCaptured) ReleaseMouseCapture();
        _settings.HudPanelWidth = _chrome.Width;
        // Clamp first, record second — the same order the drag end uses, and for the same
        // reason: a left-edge drag moves the window as it narrows, so the corner it ends at
        // is a corner the monitor clamp has not judged yet.
        Park();
        if (Mode == HudChipRow.HudParkMode.Parked)
        {
            _settings.HudPanelParkLeft = Left;
            _settings.HudPanelParkTop = Top;
        }
        _main.PersistSettings();
        if (e is not null) e.Handled = true;
        _main.RefreshHudChips();   // the row sits under this panel; it just changed shape
    }

    /// <summary>
    /// Narrow enough to sit under a bare bar without looking detached, wide enough that an
    /// ability name and its number are not both ellipsed.
    ///
    /// **ONE width, not a 260–340 band (OE-7).** The band was already content-driven inside
    /// its limits, so the panel moved every time a longer ability name arrived — on a
    /// one-second tick, on an always-on-top transparent window over a fullscreen game, which
    /// is #173's mechanism at a smaller amplitude (trap 12). OE-7's buff peek is what forced
    /// the question: its value is a COUNTDOWN, so "9:59" → "10:00" would have resized the
    /// window once a second for as long as the peek was pinned. A single width means every
    /// tick repaints identical geometry, which is the same guarantee <c>HudGlance</c>'s
    /// reserved widths give the bar this panel hangs from.
    /// </summary>
    private const double PanelWidth = 300;

    /// <summary>How many rows the panel shows. It is a PEEK, not the float: the ⧉ is one
    /// click away and carries the whole list, and a panel that grew past the bar it hangs
    /// from would be a second breakout window with worse chrome.</summary>
    private const int MaxRows = 5;

    /// <summary>
    /// One tick: draw <paramref name="target"/> from <paramref name="s"/> and park under the
    /// bar.
    ///
    /// Order matters, exactly as it does for the chip row: the body is laid out BEFORE the
    /// placement is computed, because the flip-above-the-widget rule needs a real height and
    /// <see cref="Window.ActualHeight"/> is last tick's until the content has measured.
    /// </summary>
    public void Follow(StatsSnapshot s, HudExpandTarget target)
    {
        if (_drawn != target)
        {
            _drawn = target;
            _signature = "";
            _title.Text = HudExpand.Title(target);
            _icon.Glyph = HudExpand.Icon(target);
            _popOut.ToolTip = HudExpand.PopOutTip(target);
        }
        Render(s, target);
        Park();
    }

    /// <summary>Where the panel goes this tick — <see cref="HudChipRow.Placement"/> verbatim
    /// while slaved, so the panel and the chip row cannot disagree about what "under the HUD"
    /// means or about when there is no room below it; and
    /// <see cref="HudChipRow.ParkedPlacement"/> at the player's own corner once it has been
    /// dragged, read against THAT point's monitor rather than the primary (the plan's §2.2
    /// implement check).</summary>
    private void Park()
    {
        // A drag or a resize in progress owns the window: the follower re-placing it
        // mid-gesture is the follower actor reaching for geometry the player is holding.
        if (_grip.Dragging || _edge != ResizeZones.None) return;
        UpdateLayout();
        if (Mode == HudChipRow.HudParkMode.Parked)
        {
            var parked = ScreenGuard.WorkAreaAt(
                this, _settings.HudPanelParkLeft, _settings.HudPanelParkTop);
            var (pl, pt) = HudChipRow.ParkedPlacement(
                _settings.HudPanelParkLeft, _settings.HudPanelParkTop,
                ActualWidth, ActualHeight, parked.Left, parked.Top, parked.Right, parked.Bottom);
            if (Left != pl) Left = pl;
            if (Top != pt) Top = pt;
            return;
        }
        var area = SystemParameters.WorkArea;
        var (left, top) = HudChipRow.Placement(
            _main.Left, _main.Top, _main.ActualHeight, ActualHeight, area.Top, area.Bottom);
        if (Left != left) Left = left;
        if (Top != top) Top = top;
    }

    /// <summary>The panel's own height plus its gap, for the chip row to park BELOW rather
    /// than on top of. Zero while the panel is hidden or mid-collapse, so the row goes
    /// straight back under the bar the moment the panel is no longer claiming the space.
    ///
    /// **Zero while PARKED too** — the row asks this question to find out how much of the
    /// line under the widget is already taken, and a panel the player has dragged to a corner
    /// is not on that line at all. Answering with its height would leave the row floating
    /// below a gap with nothing in it, which is a defect nobody could explain from the screen.
    /// The name says which question it answers, so a future caller cannot read it as "how
    /// tall is the panel".</summary>
    public double SlavedOccupiedHeight =>
        IsVisible && !_closing && !IsParked && double.IsFinite(ActualHeight)
            ? ActualHeight + HudChipRow.HudGap : 0;

    /// <summary>Grow down (owner lock 10). A <see cref="ScaleTransform"/>, never the window's
    /// Height — see this class's own header for why that distinction is the feature rather
    /// than a flourish.</summary>
    public void Reveal()
    {
        _closing = false;
        // **Idempotent, and that is not a micro-optimisation.** `HudBarView.Render` rebuilds
        // the bar's chips every second, so the pointer resting on a chip leaves the OLD
        // element and enters the NEW one once per tick — one Away and one Hover, forever.
        // Without this guard each of those restarts the grow animation, and a panel that
        // pulses once a second under a stationary cursor is the opposite of lock 10.
        if (IsVisible && _grow.ScaleY >= 1) return;
        if (!IsVisible) Show();
        Animate(to: 1, then: null);
    }

    /// <summary>Collapse, then hide. The hide is on the animation's completion rather than
    /// on the call, or the motion the owner asked for would be a window vanishing.</summary>
    public void Dismiss()
    {
        if (!IsVisible || _closing) { if (!_closing) Hide(); return; }
        _closing = true;
        Animate(to: 0, then: () =>
        {
            if (!_closing) return;   // a re-open landed mid-collapse; keep it on screen
            _closing = false;
            Hide();
        });
    }

    /// <summary>~160 ms with a cubic ease — long enough to read as motion, short enough that
    /// a peek feels like a hover rather than a load. <c>FillBehavior.Stop</c> plus an explicit
    /// set is the idiom that leaves the property settable afterwards; a held animation would
    /// freeze the transform at 1 and the next collapse would do nothing.</summary>
    private void Animate(double to, Action? then)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };
        anim.Completed += (_, _) =>
        {
            _grow.ScaleY = to;
            _chrome.Opacity = to;
            then?.Invoke();
        };
        _chrome.BeginAnimation(OpacityProperty, new DoubleAnimation(to, anim.Duration)
        {
            EasingFunction = anim.EasingFunction, FillBehavior = FillBehavior.Stop,
        });
        _grow.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    /// <summary>
    /// The body. DPS and HPS are the SAME decision the float makes — <see cref="LivePresentation.Meter"/>
    /// — asked once and drawn twice, because two producers of "which rows does this meter
    /// mean" is trap 33's shape: not a stale answer and a fresh one, but two answers, each
    /// current, that a later change has to be taught twice.
    ///
    /// Progress shows the GLANCE (<see cref="ProgressTheme.LauncherSummary"/> and the
    /// Experience room's own summary lines), not a fifth copy of the Progress rooms: its ⧉
    /// opens the Progress WINDOW, which has the tabs. <c>Progress</c> left
    /// <c>BreakoutKind</c> by a signed fold on 2026-08-25 for exactly this reason — "reuse
    /// the existing theme window on its current tab" — and a panel that rebuilt those rooms
    /// would be the tab-less float coming back under a new name.
    ///
    /// **Watch, Loot and Buffs (OE-7) go through <see cref="HudExpandPeek"/> for the same
    /// reason Damage/Healing/Pet go through <see cref="LivePresentation"/>**: the choice of
    /// rows is a decision, this file cannot be unit-tested, and Watch's is a decision the
    /// float already makes. Everything below the <c>switch</c> is drawing.
    /// </summary>
    private void Render(StatsSnapshot s, HudExpandTarget target)
    {
        if (target == HudExpandTarget.Progress) { RenderProgress(s); return; }
        if (Peek(s, target) is { } body) { RenderPeek(body, HudExpand.KindOf(target)); return; }

        var kind = HudExpand.KindOf(target);
        BodyKind = kind;
        // SESSION scope, always. The float carries the Fight/Session toggle and this does
        // not: a peek needs one number that means one thing, and a second scope axis on a
        // panel with no room for a strip would be a state the player cannot see or change.
        var meter = LivePresentation.Meter(kind, s, fightScope: false, DateTime.Now);
        _subtext.Text = meter.Subtext;

        var rows = meter.Rows.OrderByDescending(r => r.Total).Take(MaxRows).ToList();
        var sig = LivePresentation.MeterSignature(kind, false, "panel", meter);
        if (sig == _signature) return;
        _signature = sig;

        _rows.Children.Clear();
        if (meter.Empty is { } empty)
        {
            RowCount = 0;
            _rows.Children.Add(EmptyLine(empty));
            return;
        }
        RowCount = rows.Count;
        var top = Math.Max(1, rows.Max(r => r.Total));
        var bar = BreakdownRows.BarBrush(this);
        foreach (var row in rows)
            _rows.Children.Add(BreakdownRows.Row(this, row.Name,
                $"{row.Total:N0} · {row.Total / Math.Max(1, meter.Seconds):0.#} {meter.RateLabel}",
                (double)row.Total / top, bar, tooltip: null));
        // The cap SAYS so. A trimmed list that looks complete is "silent no-ops are broken"
        // with the switch on the other side — there is no way to tell a quiet session from a
        // truncated one, which is exactly how #234 reached a player (trap 50).
        if (meter.Rows.Count > rows.Count)
            // "↗", because that is the button in this panel's own header. The first shot
            // said "⧉" — this app's copy-to-clipboard glyph — pointing at an affordance
            // that is not on this surface at all. Nothing but the picture was going to
            // catch it: the string is plausible, the test asserts a count, and the vector
            // beside it is correct.
            _rows.Children.Add(EmptyLine($"…and {meter.Rows.Count - rows.Count} more — ↗ for the full list"));
    }

    /// <summary>The three OE-7 targets that are not a meter, or null for the ones that are.
    /// Pet is deliberately NOT here — <see cref="LivePresentation.Meter"/> has always known
    /// it, and giving it a peek builder of its own would be a second producer of the rows
    /// its float draws (trap 33).</summary>
    private PeekBody? Peek(StatsSnapshot s, HudExpandTarget target) => target switch
    {
        HudExpandTarget.Watch => HudExpandPeek.Watch(_settings.TrackedRules, s.Tracked),
        HudExpandTarget.Loot => LootPeek(s),
        // ActiveCount first, so a run with no buffs never allocates a snapshot list — this
        // is on the widget's one-second tick, and BuffsCardView guards it the same way.
        HudExpandTarget.Buffs => HudExpandPeek.Buffs(
            _main._buffTracker.ActiveCount > 0 ? _main._buffTracker.Snapshot(DateTime.Now) : [],
            DateTime.Now),
        _ => null,
    };

    /// <summary>The Loot peek's target-drops read — the SAME <c>MainWindow</c> calls
    /// <see cref="LootBreakoutView.Render"/> makes for that window's own Target scope, so the
    /// chip and its pop-out describe your target's drops identically rather than one of them
    /// re-deriving "what can this creature drop" a second time (trap 33).</summary>
    private PeekBody LootPeek(StatsSnapshot s)
    {
        var (names, detail, rows) = _main.TargetDropsContent(s);
        var emptyNote = names.Length > 0 ? _main.TargetEmptyNote(s) : "";
        return HudExpandPeek.Loot(names, detail, rows, emptyNote);
    }

    /// <summary>Draw a <see cref="PeekBody"/>. The subtext is set on EVERY tick and the rows
    /// only when the signature moves — the same split the meter path uses, and it is what
    /// keeps a buff countdown out of the element tree (trap 8).</summary>
    private void RenderPeek(PeekBody body, string kind)
    {
        BodyKind = kind;
        _subtext.Text = body.Subtext;
        if (body.Signature == _signature) return;
        _signature = body.Signature;

        _rows.Children.Clear();
        if (body.Empty is { } empty)
        {
            RowCount = 0;
            _rows.Children.Add(EmptyLine(empty));
            return;
        }
        var shown = body.Rows.Take(MaxRows).ToList();
        RowCount = shown.Count;
        var bar = BreakdownRows.BarBrush(this);
        foreach (var row in shown)
            _rows.Children.Add(BreakdownRows.Row(this, row.Name, row.Value, row.Share, bar,
                row.Tooltip));
        // Same no-silent-cap rule as the meter path above (trap 50), and the same "↗"
        // because it is the button in this panel's own header.
        if (body.Rows.Count > shown.Count)
            _rows.Children.Add(EmptyLine(
                $"…and {body.Rows.Count - shown.Count} more — ↗ for the full list"));
    }

    /// <summary>The Progress glance: the launcher line the folded card already carries, plus
    /// the Experience room's own summary. One source for each, so the panel cannot say
    /// something the card and the window do not.</summary>
    private void RenderProgress(StatsSnapshot s)
    {
        BodyKind = BreakoutPresentation.Progress;
        _subtext.Text = ProgressTheme.LauncherSummary(s);
        var lines = ProgressPresentation.SummaryLines(s).Take(MaxRows).ToList();
        var sig = "progress|" + _subtext.Text + "|" + string.Join("|", lines);
        if (sig == _signature) return;
        _signature = sig;

        _rows.Children.Clear();
        RowCount = lines.Count;
        foreach (var line in lines) _rows.Children.Add(EmptyLine(line, dim: false));
    }

    private TextBlock EmptyLine(string text, bool dim = true)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 1),
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, dim ? "DimBrush" : "TextBrush");
        return block;
    }
}
