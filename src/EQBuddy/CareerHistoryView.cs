using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **THE CAREER HALF of <c>HistoryWindow</c>'s merge** — the Progress room's History tab.
/// Bevel's History pre-design §1/§4, Helm-signed 2026-09-05 ~10:10 AM CT.
///
/// It answers the two career questions the signed disposition table names outright: *which
/// past sittings are there* (browse, one row each, with the sitting's own numbers beside
/// it) and *what have they added up to* (the level and AA ladders across every stored
/// session). The this-session half is Live's, and it is a different surface reading a
/// different source — see <see cref="LiveSessionPanes"/>.
///
/// **THE LIVE ROW IS EXCLUDED, AND THAT IS THE WHOLE POINT OF THE SPLIT.** The archiver
/// checkpoints the running sitting into the store under
/// <c>SessionRepository.ActiveEndReason</c>, so it IS in <c>StoredSessions()</c> and the v1
/// studio will happily let you select it — showing a picture up to five minutes old that
/// never reloads (Bevel §2). A career browse that offered that row would be offering the
/// stale copy of a sitting whose live copy is one room away, so it does not: the filter is
/// <see cref="SessionSummary.IsTheLiveSession"/>, the same predicate Home and Live already
/// merge on, rather than a second spelling of the end-reason check (trap 33).
///
/// **WHAT THIS TAB DOES NOT DO, AND WHY IT IS NAMED RATHER THAN ABSENT.** The per-session
/// deep detail and the four studio jobs — compare, notes/tags, export JSON, delete, import —
/// all need <c>SessionRepository</c> itself, and the shell reaches the widget only through
/// <c>MainWindow</c>'s existing accessors. Standing up a SECOND repository on the same
/// SQLite file would be a second writer to a database the archiver is checkpointing into,
/// which is trap 13's shape with a different file. So they stay in the studio for this pass
/// — which Helm signed KEPT (item 5) — and <see cref="HistoryPresentation.StudioPointer"/>
/// is the sentence that says so on screen, because a browse that silently shows less than
/// the studio is #234's "trimmed list that looks complete".
///
/// **It is the first Progress tab that is not one column of arithmetic** (Bevel §4 predicted
/// exactly this), so it is also the first consumer of <c>ShellLayout.RoomSinglePane</c>
/// outside Quests: below <c>SplitRoomWidth</c> the list takes the whole room and a picked
/// sitting replaces it with a way back. The threshold is the shell's and arrives through
/// <see cref="SinglePane"/>; nothing here measures a width (trap 33).
/// </summary>
internal sealed class CareerHistoryView : Grid
{
    private readonly Func<IReadOnlyList<SessionRow>> _stored;
    private readonly Func<IReadOnlyList<SessionRepository.ProgressPoint>> _ladders;
    private readonly Func<string> _characterKey;

    private readonly ColumnDefinition _masterColumn = new() { Width = new GridLength(MasterWidth) };
    private readonly ColumnDefinition _detailColumn = new() { Width = new GridLength(1, GridUnitType.Star) };

    private readonly Grid _master = new();
    private readonly Grid _detail = new();
    private readonly TextBlock _count;
    private readonly StackPanel _rows = new();
    private readonly Button _back;
    private readonly TextBlock _heading;
    private readonly TextBlock _body;
    private readonly StackPanel _ladderBlock = new();
    private readonly Canvas _levelCanvas = new() { Height = LadderHeight };
    private readonly Canvas _aaCanvas = new() { Height = LadderHeight };
    private readonly TextBlock _levelCaption;
    private readonly TextBlock _aaCaption;
    private readonly TextBlock _pointer;
    private readonly Border _levelFrame;
    private readonly Border _aaFrame;

    /// <summary>A step chart's ground. The studio draws its two charts inside a bordered
    /// panel and the first shot of this tab did not — two bare polylines on the room's own
    /// background read as lines that had escaped something rather than as charts, and the
    /// Pace graph next door already had the frame.</summary>
    private Border Frame(Canvas canvas) => new()
    {
        Background = (Brush)FindResource("PanelBrush"),
        CornerRadius = new CornerRadius(Tok.RadiusCard),
        Padding = new Thickness(Tok.SpaceS, Tok.SpaceXs, Tok.SpaceS, Tok.SpaceXs),
        Margin = new Thickness(0, Tok.SpaceXxs, 0, Tok.SpaceM),
        Child = canvas,
    };

    /// <summary>The list pane's width when both panes are up. Quests' catalog uses 400 and
    /// this list carries the same shape of row (a title line over a facts line), so it takes
    /// the same measure rather than a fresh guess — two list-beside-detail rooms disagreeing
    /// about a column width is the drift a shared token exists to stop.</summary>
    private const double MasterWidth = 400;

    /// <summary>Short on purpose: two ladders and a caption each have to fit above the
    /// pointer sentence at the 520-unit floor, and a step chart's whole job is the SHAPE.
    /// The studio's are 60; these are a little taller because a room is not a pane.</summary>
    private const double LadderHeight = 80;

    private readonly List<SessionRow> _list = [];
    private long _selected;
    private string _signature = "";
    private bool _singlePane;
    private bool _paneDetail;

    /// <summary>Rows drawn, for the dump. The number the tab's own badge is about, reported
    /// so a browse that silently stopped listing is visible from outside — the WPF layer has
    /// no unit tests to say it any other way.</summary>
    public int RowCount => _list.Count;

    /// <summary>Whether the two cross-session ladders are on screen. It is a real state that
    /// they are not (one ding is not a ladder), so it is reported rather than assumed.
    /// </summary>
    public bool LaddersShown { get; private set; }

    public CareerHistoryView(
        Func<IReadOnlyList<SessionRow>> stored,
        Func<IReadOnlyList<SessionRepository.ProgressPoint>> ladders,
        Func<string> characterKey)
    {
        _stored = stored;
        _ladders = ladders;
        _characterKey = characterKey;

        ColumnDefinitions.Add(_masterColumn);
        ColumnDefinitions.Add(_detailColumn);

        // ---- the list ------------------------------------------------------------
        _master.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _master.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _count = DesignSystem.Text(Role.Caption, "");
        _count.Ink("DimBrush");
        SetRow(_count, 0);
        _master.Children.Add(_count);
        // **Its OWN scroller, and the room disables its own for this tab** — the pattern
        // `LiveRoom` set for the Timeline canvas. A child scroller inside a host scroller is
        // measured with infinite height, so it never overflows, never scrolls, and still
        // eats the wheel (trap 36); the room hands this tab the viewport instead, and then
        // the two panes each have a real overflow of their own.
        var listScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _rows,
        };
        SetRow(listScroll, 1);
        _master.Children.Add(listScroll);
        SetColumn(_master, 0);
        Children.Add(_master);

        // ---- the detail ----------------------------------------------------------
        _detail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _detail.Margin = new Thickness(Tok.SpaceL, 0, 0, 0);

        var head = new StackPanel { Orientation = Orientation.Horizontal };
        _back = new Button
        {
            Style = (Style)FindResource("IconButton"),
            FontSize = 11,
            Content = "‹ All sittings",
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, Tok.SpaceS, 0),
        };
        _back.Click += (_, _) => { _paneDetail = false; ApplyPanes(); };
        head.Children.Add(_back);
        _heading = DesignSystem.Text(Role.TitleSection, "");
        _heading.TextWrapping = TextWrapping.Wrap;
        _heading.Ink("AccentBrush");
        _heading.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(_heading);
        SetRow(head, 0);
        _detail.Children.Add(head);

        var bodyStack = new StackPanel();
        _body = DesignSystem.Text(Role.BodySecondary, "");
        _body.TextWrapping = TextWrapping.Wrap;
        bodyStack.Children.Add(_body);

        // The two ladders, and the caption that introduces them, as ONE collapsible block:
        // a heading over an empty canvas reads as a chart that failed (trap 17), and three
        // switches for one state is how that happens.
        _ladderBlock.Children.Add(CardParts.BlockLabel(
            HistoryPresentation.CareerLaddersCaption, hidden: false));
        _levelCaption = DesignSystem.Text(Role.Caption, "");
        _levelCaption.TextWrapping = TextWrapping.Wrap;
        _levelCaption.Ink("DimBrush");
        _ladderBlock.Children.Add(_levelCaption);
        _levelFrame = Frame(_levelCanvas);
        _ladderBlock.Children.Add(_levelFrame);
        _aaCaption = DesignSystem.Text(Role.Caption, "");
        _aaCaption.TextWrapping = TextWrapping.Wrap;
        _aaCaption.Ink("DimBrush");
        _ladderBlock.Children.Add(_aaCaption);
        _aaFrame = Frame(_aaCanvas);
        _ladderBlock.Children.Add(_aaFrame);
        bodyStack.Children.Add(_ladderBlock);

        // **ABOVE nothing and BELOW the content, deliberately, and it is the one placement
        // call in this file.** Trap 44 says a report about something that just happened goes
        // where the eye lands; this is the opposite kind of sentence — it is read once, on
        // the way OUT, when a player has looked at the browse and wants the depth. Putting
        // it above the numbers would make every visit start with a paragraph about another
        // window.
        _pointer = DesignSystem.Text(Role.Caption, HistoryPresentation.StudioPointer);
        _pointer.TextWrapping = TextWrapping.Wrap;
        _pointer.Ink("DimBrush");
        _pointer.Margin = new Thickness(0, Tok.SpaceL, 0, 0);
        bodyStack.Children.Add(_pointer);

        var detailScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = bodyStack,
        };
        SetRow(detailScroll, 1);
        _detail.Children.Add(detailScroll);
        SetColumn(_detail, 1);
        Children.Add(_detail);

        _levelCanvas.SizeChanged += (_, e) => { if (e.NewSize.Width > 0) DrawLadders(); };
        ApplyPanes();
    }

    /// <summary>
    /// The shell says this room is too narrow to hold a list beside a detail pane.
    ///
    /// Forwarded from <c>ProgressRoom.ApplyLayout</c> without being cached or re-derived —
    /// the threshold is about the ROOM's share of the window after the rail, arithmetic only
    /// the host has both halves of, and a view measuring itself would be a second producer
    /// of one answer that disagrees exactly where a resize bug lives (trap 33).
    /// </summary>
    public bool SinglePane
    {
        get => _singlePane;
        set
        {
            if (_singlePane == value) return;
            _singlePane = value;
            // Widening back to two panes ends the detour: the list is on screen again, so
            // "you are looking at one sitting" stops being a mode to leave. The same rule
            // `QuestsView` makes, because it is the same layout.
            if (!value) _paneDetail = false;
            ApplyPanes();
        }
    }

    /// <summary>Re-read the store on the next paint whatever the memo says — called when the
    /// tab is entered. The memo key below cannot see an IMPORT: the studio can add rows
    /// without a session rolling, and a browse that went on showing the old list until the
    /// next ding would be wrong in the one way a player would notice immediately.</summary>
    public void Invalidate() => _signature = "";

    /// <summary>
    /// Paint. The store is read at most once per session roll, not once per tick.
    ///
    /// **The memo key is what can add a row**, the same shape <see cref="LevelHistoryMemo"/>
    /// uses and for the same measured reason: <c>StoredLevelDings()</c> probes up to a
    /// thousand stored snapshots, and this room paints every second it is open. A sitting
    /// joins the store when it ENDS, and a sitting ending is exactly a change of
    /// <c>SessionStart</c>; a character switch changes who the rows are about.
    /// <see cref="Invalidate"/> covers the one event neither of those sees.
    /// </summary>
    public void Render(StatsSnapshot s)
    {
        var signature = $"{_characterKey()}|{s.SessionStart:O}";
        if (signature == _signature) return;
        _signature = signature;

        _list.Clear();
        // NEWEST FIRST — `SessionRepository.Query` already orders that way and a browse of
        // many sittings is read backwards, which is the opposite of the Encounters list
        // inside ONE sitting. Both orders are right for their own question.
        foreach (var row in _stored())
            if (!SessionSummary.IsTheLiveSession(row, s.SessionStart))
                _list.Add(row);

        _count.Text = HistoryPresentation.BuildCount(_list.Count);
        _rows.Children.Clear();
        foreach (var row in _list) _rows.Children.Add(BuildRow(row));
        if (_list.All(r => r.Id != _selected)) _selected = 0;

        DrawLadders();
        ApplyDetail();
        ApplyPanes();
    }

    /// <summary>
    /// One sitting as a clickable two-line row.
    ///
    /// **A <c>Border</c> with <see cref="DesignSystem.WireClick"/> and NOT an
    /// <c>IconButton</c>, and the first screenshot is why.** That style's template hardcodes
    /// <c>HorizontalAlignment="Center"</c> on its <c>ContentPresenter</c>, so
    /// <c>HorizontalContentAlignment</c> on the button is not aliased and does nothing — the
    /// three rows rendered centred in a 400-unit column, which reads as a layout accident
    /// and is invisible to a diff, a build and every test in the suite. Same family as
    /// <c>RoomEmptyState</c>'s own note about <c>ContentControl</c>'s defaults: a hardcoded
    /// alignment inside a template beats the property that looks like it sets it.
    /// </summary>
    private UIElement BuildRow(SessionRow row)
    {
        var career = HistoryPresentation.BuildCareerRow(row);
        var stack = new StackPanel();
        var title = DesignSystem.Text(Role.Body, career.Title);
        title.TextWrapping = TextWrapping.Wrap;
        stack.Children.Add(title);
        var detail = DesignSystem.Text(Role.Caption, career.Detail);
        detail.TextWrapping = TextWrapping.Wrap;
        detail.Ink("DimBrush");
        stack.Children.Add(detail);

        var border = new Border
        {
            CornerRadius = new CornerRadius(Tok.RadiusCard),
            Padding = new Thickness(Tok.SpaceS, Tok.SpaceXs, Tok.SpaceS, Tok.SpaceXs),
            Margin = new Thickness(0, 0, Tok.SpaceS, Tok.SpaceXxs),
            Background = System.Windows.Media.Brushes.Transparent,
            Child = stack,
            Tag = row.Id,
        };
        border.MouseEnter += (_, _) => Paint(border, hover: true);
        border.MouseLeave += (_, _) => Paint(border, hover: false);
        DesignSystem.WireClick(border, () =>
        {
            _selected = row.Id;
            // In one pane, picking a sitting IS the navigation — there is nowhere else for
            // the detail to appear, and a click that changed a pane you cannot see would be
            // the silent no-op rule with the switch on the other side.
            if (_singlePane) _paneDetail = true;
            ApplyDetail();
            ApplyPanes();
        });
        return border;
    }

    /// <summary>Which row reads as picked: the panel ground, which is what every other
    /// selected thing in this app uses. It was an opacity dim in the first build and that
    /// was wrong in the state that matters most — with NOTHING picked, every row was full
    /// opacity, so the list gave no hint that a row was a thing you could pick.</summary>
    private void Highlight()
    {
        foreach (var child in _rows.Children.OfType<Border>()) Paint(child, hover: false);
    }

    private void Paint(Border row, bool hover) =>
        row.Background = (long?)row.Tag == _selected
            ? (System.Windows.Media.Brush)FindResource("PanelHoverBrush")
            : hover
                ? (System.Windows.Media.Brush)FindResource("PanelBrush")
                : System.Windows.Media.Brushes.Transparent;

    private void ApplyDetail()
    {
        var row = _list.FirstOrDefault(r => r.Id == _selected);
        if (row is null)
        {
            _heading.Text = _list.Count == 0
                ? HistoryPresentation.CareerEmptyHeading
                : HistoryPresentation.CareerSelectPrompt;
            _body.Text = _list.Count == 0 ? HistoryPresentation.CareerEmptyExplanation : "";
            _body.Visibility = _list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            // The ladders are the DEFAULT state of this pane, exactly as they are in the
            // studio: with nothing picked, "what has this character done" is the question
            // the surface is answering. Picking a sitting replaces them with that sitting.
            _ladderBlock.Visibility = LaddersShown ? Visibility.Visible : Visibility.Collapsed;
            Highlight();
            return;
        }

        _heading.Text = HistoryPresentation.BuildCareerHeading(row);
        _body.Text = HistoryPresentation.BuildCareerDetail(row);
        _body.Visibility = Visibility.Visible;
        _ladderBlock.Visibility = Visibility.Collapsed;
        Highlight();
    }

    /// <summary>
    /// The two cross-session ladders — the studio's charts, over the studio's own series,
    /// through the studio's own <see cref="HistoryPresentation.BuildStepGraph"/>.
    ///
    /// Values HOLD until the next observation: a level is a fact until the next ding, so the
    /// line is a staircase and never a slope. That rule is the shared builder's and is not
    /// re-decided here.
    /// </summary>
    private void DrawLadders()
    {
        var (dings, aa) = HistoryPresentation.CareerLadders(_ladders());
        var width = _levelCanvas.ActualWidth > 0 ? _levelCanvas.ActualWidth : 300;
        // Eight units of slack, four of which the draw below adds back as an offset — the
        // same breathing room the Pace graph takes. The first shot of this tab drew the top
        // step flush against the frame's edge, which reads as a chart that has been clipped
        // rather than one that reached its maximum.
        var levels = HistoryPresentation.BuildStepGraph(dings, width, LadderHeight - 8);
        var aaGraph = HistoryPresentation.BuildStepGraph(aa, width, LadderHeight - 8);
        LaddersShown = levels is not null || aaGraph is not null;

        Step(_levelCanvas, _levelCaption, levels, "AccentBrush",
            HistoryPresentation.CareerLevelCaption(levels, dings));
        Step(_aaCanvas, _aaCaption, aaGraph, "GoodBrush",
            HistoryPresentation.CareerAaCaption(aaGraph, aa));
        if (_selected == 0)
            _ladderBlock.Visibility = LaddersShown ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Step(Canvas canvas, TextBlock caption, HistoryGraph? graph, string brushKey,
        string? text)
    {
        canvas.Children.Clear();
        caption.Text = text ?? "";
        // Caption and canvas move together — one switch, not two (trap 17).
        // Caption, canvas AND its frame — three things, one state (trap 17).
        var frame = ReferenceEquals(canvas, _levelCanvas) ? _levelFrame : _aaFrame;
        caption.Visibility = canvas.Visibility = frame.Visibility =
            graph is null ? Visibility.Collapsed : Visibility.Visible;
        if (graph is null) return;
        var line = new Polyline
        {
            Stroke = (Brush)FindResource(brushKey),
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Miter,
        };
        foreach (var (x, y) in graph.Points) line.Points.Add(new Point(x, y + 4));
        canvas.Children.Add(line);
    }

    /// <summary>
    /// List beside detail, or one of them with a way back.
    ///
    /// **A collapsed pane keeps its COLUMN rather than being torn out**, so crossing the
    /// threshold in either direction is a width change and not a rebuild — the selection,
    /// the scroll position and the detail text all survive it. `QuestsView.ApplyPanes` is
    /// the precedent and this is deliberately the same arithmetic.
    /// </summary>
    private void ApplyPanes()
    {
        var detail = _singlePane && _paneDetail;
        _master.Visibility = detail ? Visibility.Collapsed : Visibility.Visible;
        _detail.Visibility = !_singlePane || detail ? Visibility.Visible : Visibility.Collapsed;
        _back.Visibility = detail ? Visibility.Visible : Visibility.Collapsed;

        _masterColumn.Width = !_singlePane ? new GridLength(MasterWidth)
            : detail ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        _detailColumn.Width = !_singlePane || detail
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        _detail.Margin = new Thickness(_singlePane ? 0 : Tok.SpaceL, 0, 0, 0);
    }

    /// <summary>The tab's facts for the <c>EQBUDDY_EXPAND</c> dump. Prefixed by the room,
    /// mechanically (<see cref="ShellDumpFacts"/>), so a second host of this view could not
    /// write the same keys into the one flat namespace (trap 58).</summary>
    public string DebugFacts() =>
        $"careerRows={RowCount} " +
        $"careerSelected={(_selected != 0 ? 1 : 0)} " +
        $"careerLadders={(LaddersShown ? 1 : 0)} " +
        $"careerSinglePane={(_singlePane ? 1 : 0)} " +
        $"careerPaneDetail={(_paneDetail ? 1 : 0)}";
}
