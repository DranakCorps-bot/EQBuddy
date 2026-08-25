using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The fight timeline: the whole pull on one canvas, a lane per skill, a DPS graph on
/// top. Marks are drawn immediate-mode (one visual, DrawingContext) — a long raid fight
/// is thousands of events, and a WPF shape per event would crawl. Scroll zooms around
/// the cursor, dragging the plot pans, Fit snaps back; while the fight is live the
/// window follows it, until the user zooms — their viewport is then theirs.
/// </summary>
public partial class FightTimelineWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Func<(LastFightInfo? Fight, List<GameEvent> Events, string Pet)> _source;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TimelineViewport _view = new();
    private string _signature = "";
    private long _sourceVersion = -1;

    /// <summary>The stats version, polled before pulling the source: a quiet second
    /// (nobody swung, nothing dropped) used to cost a full snapshot plus a journal
    /// copy just to learn nothing changed (2026-08-12 tuning pass).</summary>
    internal Func<long>? SourceVersion { get; set; }

    public FightTimelineWindow(AppSettings settings,
        Func<(LastFightInfo?, List<GameEvent>, string)> source)
    {
        InitializeComponent();
        _settings = settings;
        // Resizable and REMEMBERED (David, 2026-08-25: "allow all the pop out windows to be
        // resized"). Content is complete at first render — the timeline is built from the
        // fight handed in — so AllowResize's height sample is a true one.
        WindowZoom.AllowResize(this, "timeline", settings);
        _source = source;

        Chrome.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "BgBrush");
        Chrome.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "HairlineBrush");
        TipChrome.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "BgBrush");
        TipChrome.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "AccentBrush");
        TitleText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextBrush");
        foreach (var dim in new[] { SubTitle, PeakText, TipText, LegendText })
            dim.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "DimBrush");

        Graph.View = _view;
        Lanes.View = _view;
        Lanes.HoverChanged += OnHover;

        var restored = ScreenGuard.OnScreen(_settings.TimelineLeft, _settings.TimelineTop,
            Math.Max(420, _settings.TimelineWidth), 200);
        if (restored)
        {
            Left = _settings.TimelineLeft; Top = _settings.TimelineTop;
            if (_settings.TimelineWidth >= MinWidth) Width = _settings.TimelineWidth;
            if (_settings.TimelineHeight >= MinHeight)
            {
                Height = _settings.TimelineHeight;
                _userSized = true;   // a saved height is a choice; fit-to-fight defers to it
            }
        }
        else
        {
            var area = SystemParameters.WorkArea;
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
        }
        var (placedLeft, placedTop) = (Left, Top);

        Closed += (_, _) =>
        {
            _tick.Stop();
            // Never let an unmoved fallback overwrite a real saved spot (#117).
            (_settings.TimelineLeft, _settings.TimelineTop) = WindowPlacement.PositionToPersist(
                restored, placedLeft, placedTop, Left, Top,
                _settings.TimelineLeft, _settings.TimelineTop);
            _settings.TimelineWidth = Width; _settings.TimelineHeight = Height;
            _settings.Save();
        };
        SizeChanged += (_, _) => { if (_view.Fit) ApplyFit(); Redraw(); };
        MouseWheel += OnZoom;
        _tick.Tick += (_, _) => Refresh();
        _tick.Start();
        Loaded += (_, _) => Refresh();
    }

    /// <summary>Re-pull the fight; rebuild only when it moved (new events or a new
    /// pull). Fit-mode keeps refitting as a live fight grows; a user viewport holds.</summary>
    private void Refresh()
    {
        var version = SourceVersion?.Invoke() ?? -1;
        if (version >= 0 && version == _sourceVersion && _view.Timeline is not null) return;
        _sourceVersion = version;
        var (fight, events, pet) = _source();
        if (fight is null || fight.Start == DateTime.MinValue)
        {
            TitleText.Text = "Fight timeline";
            SubTitle.Text = "no fight yet — pull something";
            _view.Timeline = null;
            Redraw();
            return;
        }

        var signature = $"{fight.Name}|{fight.Start.Ticks}|{events.Count}|{(int)fight.DurationSeconds}";
        if (signature == _signature) return;
        _signature = signature;

        _view.Timeline = TimelineBuilder.Build(events, fight.Start, fight.DurationSeconds, pet);
        FitWindowToFight(_view.Timeline.Lanes.Count);
        TitleText.Text = fight.Name;
        SubTitle.Text = $"{Clock(fight.DurationSeconds)} · {_view.Timeline.EventCount:N0} events"
            + (fight.InProgress ? " · live" : "");
        PeakText.Text = $"peak {_view.Timeline.PeakDps:N0} dps @ {Clock(_view.Timeline.PeakSec)}";
        if (_view.Fit) ApplyFit();
        Redraw();
    }

    private void ApplyFit()
    {
        _view.Fit = true;
        _view.OffsetSec = 0;
        _view.PixelsPerSec = _view.Timeline is { } t
            ? Math.Max(0.5, (Lanes.ActualWidth - LanesPanel.LabelWidth) / t.DurationSeconds)
            : 1;
    }

    private void Redraw() { Graph.InvalidateVisual(); Lanes.Refit(); }

    private void OnZoom(object sender, MouseWheelEventArgs e)
    {
        if (_view.Timeline is not { } t) return;
        var pos = e.GetPosition(Lanes).X - LanesPanel.LabelWidth;
        if (pos < 0) return;
        var anchor = _view.OffsetSec + pos / _view.PixelsPerSec;
        var factor = e.Delta > 0 ? 1.25 : 1 / 1.25;
        var fitPps = Math.Max(0.5, (Lanes.ActualWidth - LanesPanel.LabelWidth) / t.DurationSeconds);
        _view.PixelsPerSec = Math.Clamp(_view.PixelsPerSec * factor, fitPps, 120);
        _view.Fit = Math.Abs(_view.PixelsPerSec - fitPps) < 0.001;
        _view.OffsetSec = _view.Fit ? 0
            : Math.Clamp(anchor - pos / _view.PixelsPerSec, 0, t.DurationSeconds);
        Redraw();
        e.Handled = true;
    }

    internal void Pan(double deltaPixels)
    {
        if (_view.Timeline is not { } t || _view.Fit) return;
        _view.OffsetSec = Math.Clamp(_view.OffsetSec - deltaPixels / _view.PixelsPerSec,
            0, t.DurationSeconds);
        Redraw();
    }

    private void OnHover(TimelineMark? mark, TimelineLane? lane, Point at)
    {
        if (mark is null || lane is null) { MarkTip.IsOpen = false; return; }
        var what = mark.Hollow ? mark.Label
            : $"{mark.Amount:N0}{(mark.Crit ? " · Critical" : "")}"
              + (mark.Label.Length > 0 && !mark.Crit ? $" · {mark.Label}" : "");
        TipText.Text = $"{lane.Name} · {what} · {Clock(mark.Sec)}";
        MarkTip.HorizontalOffset = at.X + 14;
        MarkTip.VerticalOffset = at.Y + 10;
        MarkTip.IsOpen = true;
    }

    internal static string Clock(double seconds) =>
        $"{(int)seconds / 60}:{(int)seconds % 60:00}";

    private void OnDragWindow(object sender, MouseButtonEventArgs e) { try { DragMove(); } catch { } }
    private void OnFit(object sender, MouseButtonEventArgs e) { ApplyFit(); Redraw(); }
    private void OnClose(object sender, MouseButtonEventArgs e) => Close();

    private void OnGripDrag(object sender, DragDeltaEventArgs e)
    {
        _userSized = true;   // their size wins from here; content keeps adapting to it
        Width = Math.Max(MinWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinHeight, Height + e.VerticalChange);
    }

    private bool _userSized;

    /// <summary>Fit the window to the fight (David: "scale dynamically and fit, but
    /// be able to be resized") — a 5-lane skirmish opens compact, a raid brawl opens
    /// tall, and the first grip drag (or a saved size) ends the auto-fitting. Lanes
    /// then stretch or scroll inside whatever the user chose.</summary>
    private void FitWindowToFight(int laneCount)
    {
        if (_userSized) return;
        const double chrome = 96 + 60 + 34;   // graph + header/legend rows + padding
        var desired = chrome + laneCount * 42 + 18;
        Height = Math.Clamp(desired, MinHeight, SystemParameters.WorkArea.Height * 0.85);
    }
}

/// <summary>Shared viewport: where in the fight we're looking and how magnified.
/// Both render panels read it; the window writes it.</summary>
internal sealed class TimelineViewport
{
    public FightTimeline? Timeline;
    public double OffsetSec;
    public double PixelsPerSec = 1;
    /// <summary>True until the user zooms: the view tracks the whole (possibly still
    /// growing) fight. Fit is a mode, not a moment — a live fight keeps refitting.</summary>
    public bool Fit = true;

    public Brush Text = Brushes.White, Dim = Brushes.Gray, Accent = Brushes.CornflowerBlue,
        Good = Brushes.SeaGreen, Bad = Brushes.IndianRed, Warn = Brushes.Orange,
        Bg = Brushes.Black;
    // Chart-series steps (the 2026-08-13 approved pass): deeper cuts than the ambient
    // accents, colorblind-validated on the default theme. Fallbacks keep old custom
    // palettes painting.
    public Brush ChartYou = Brushes.DarkGoldenrod, ChartPet = Brushes.SeaGreen,
        ChartIncoming = Brushes.SteelBlue, ChartCrit = Brushes.Orange;

    public void CaptureTheme(FrameworkElement from)
    {
        Text = Find(from, "TextBrush", Text); Dim = Find(from, "DimBrush", Dim);
        Accent = Find(from, "AccentBrush", Accent); Good = Find(from, "GoodBrush", Good);
        Bad = Find(from, "BadBrush", Bad); Warn = Find(from, "WarnBrush", Warn);
        Bg = Find(from, "BgBrush", Bg);
        ChartYou = Find(from, "ChartYouBrush", Accent);
        ChartPet = Find(from, "ChartPetBrush", Good);
        ChartIncoming = Find(from, "ChartIncomingBrush", Bad);
        ChartCrit = Find(from, "ChartCritBrush", Warn);
    }

    private static Brush Find(FrameworkElement e, string key, Brush fallback) =>
        e.TryFindResource(key) as Brush ?? fallback;
}

/// <summary>The three-line DPS graph: you+pet (accent), pet alone (dim), incoming (bad),
/// with the peak flagged. Scales to the visible window so zooming the lanes zooms this too.</summary>
internal sealed class DpsGraphPanel : FrameworkElement
{
    internal TimelineViewport? View;

    protected override void OnRender(DrawingContext dc)
    {
        if (View is not { Timeline: { } t } v) return;
        v.CaptureTheme(this);
        var w = ActualWidth - LanesPanel.LabelWidth;
        var top = 14.0;   // legend row
        var h = ActualHeight - 2;
        if (w <= 10 || h - top <= 10) return;

        var max = Math.Max(1, new[] { t.DpsSeries, t.PetDpsSeries, t.IncomingDpsSeries }
            .SelectMany(s => s).DefaultIfEmpty(1).Max());
        double YOf(double dps) => h - dps / max * (h - top - 4);

        // Recessive grid: dotted quarter-lines, labels right-aligned INTO the label
        // gutter so they never sit on data (the approved mockup's rule).
        foreach (var frac in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            var gy = YOf(max * frac);
            dc.DrawLine(new Pen(v.Dim, 0.4) { DashStyle = DashStyles.Dot },
                new Point(LanesPanel.LabelWidth, gy), new Point(LanesPanel.LabelWidth + w, gy));
            var label = Caption($"{max * frac:N0}", v.Dim, 9);
            dc.DrawText(label, new Point(LanesPanel.LabelWidth - label.Width - 6, gy - label.Height / 2));
        }

        dc.PushTransform(new TranslateTransform(LanesPanel.LabelWidth, 0));

        // Smooth series: monotone-cubic through every real sample — curved, but
        // mathematically unable to overshoot a value that never happened. Your line
        // is the headline and gets the gradient area; pet and incoming stay strokes.
        DrawSmooth(dc, t.IncomingDpsSeries, v, w, max, YOf, v.ChartIncoming, 1.6, fill: false, h);
        DrawSmooth(dc, t.PetDpsSeries, v, w, max, YOf, v.ChartPet, 1.6, fill: false, h);
        DrawSmooth(dc, t.DpsSeries, v, w, max, YOf, v.ChartYou, 2.0, fill: true, h);

        // Legend: uppercase micro-labels with rounded swatches, top-left.
        var lx = 4.0;
        foreach (var (text, brush, present) in new[]
        {
            ("YOU + PET", v.ChartYou, true),
            ("INCOMING", v.ChartIncoming, t.IncomingDpsSeries.Any(p => p > 0)),
            ("PET", v.ChartPet, t.PetDpsSeries.Any(p => p > 0)),
        })
        {
            if (!present) continue;
            dc.DrawRoundedRectangle(brush, null, new Rect(lx, 5.5, 10, 3), 1.5, 1.5);
            var key = Caption(text, v.Dim, 8.5);
            dc.DrawText(key, new Point(lx + 14, 1));
            lx += 14 + key.Width + 12;
        }

        // The peak is a marked POINT — an emphasized dot with a surface ring and a
        // label that dodges the plot edges, not a dashed wall through the data.
        var peakX = (t.PeakSec - v.OffsetSec) * v.PixelsPerSec;
        if (peakX >= 0 && peakX <= w && t.PeakDps > 0)
        {
            var py = YOf(t.PeakDps);
            dc.DrawEllipse(v.ChartYou, new Pen(v.Bg, 2), new Point(peakX, py), 4, 4);
            var label = Caption($"{t.PeakDps:N0} peak", v.Text, 9.5, bold: true);
            var labelX = peakX + 8 + label.Width <= w ? peakX + 8 : peakX - 8 - label.Width;
            // A peak near the top would put its label in the legend row — drop it
            // BELOW the dot there instead of on top of the series key.
            var labelY = py - label.Height - 4 < top ? py + 6 : py - label.Height - 4;
            dc.DrawText(label, new Point(labelX, labelY));
        }
        dc.Pop();
    }

    private static void DrawSmooth(DrawingContext dc, double[] series, TimelineViewport v,
        double w, double max, Func<double, double> yOf, Brush brush, double thickness,
        bool fill, double baseline)
    {
        if (series.Length < 2) return;
        // Visible slice with a sample of margin each side so the curve enters and
        // leaves the viewport smoothly. Display buckets keep drawn points ≥4px
        // apart (zoomed out, per-second samples sit sub-pixel and the "curve" reads
        // as the old jaggedness) — and each bucket contributes its MAXIMUM sample at
        // that sample's true position, so a one-second burst can never vanish
        // between drawn points and the peak dot always sits ON the curve
        // (2026-08-13 review: a plain stride skipped spikes and left the dot
        // floating). The cost is honest and stated: between-bucket minima smooth
        // away at low zoom; maxima never do.
        var stride = Math.Max(1, (int)Math.Ceiling(4 / v.PixelsPerSec));
        var first = Math.Max(0, ((int)Math.Floor(v.OffsetSec) / stride - 1) * stride);
        var lastVisible = Math.Min(series.Length - 1,
            (int)Math.Ceiling(v.OffsetSec + w / v.PixelsPerSec) + stride);
        var count = (lastVisible - first) / stride + 1;
        if (count < 2) return;
        var xs = new double[count];
        var ys = new double[count];
        for (var k = 0; k < count; k++)
        {
            var bucketStart = first + k * stride;
            var best = Math.Min(series.Length - 1, bucketStart);
            for (var i = bucketStart + 1; i < bucketStart + stride && i <= lastVisible && i < series.Length; i++)
                if (series[i] > series[best]) best = i;
            xs[k] = (best - v.OffsetSec) * v.PixelsPerSec;
            ys[k] = yOf(series[best]);
        }
        var tangents = EQBuddy.UI.Shared.MonotoneCurve.Tangents(xs, ys);

        var stroke = new StreamGeometry();
        using (var ctx = stroke.Open())
            AppendCurve(ctx, xs, ys, tangents, close: false, baseline);
        stroke.Freeze();

        if (fill)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
                AppendCurve(ctx, xs, ys, tangents, close: true, baseline);
            area.Freeze();
            dc.DrawGeometry(GradientFor(brush), null, area);
        }
        dc.DrawGeometry(null, new Pen(brush, thickness)
        { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
            stroke);
    }

    // Gradient per series color, cached — rebuilt-per-render brushes were churn at
    // pan-mousemove rate (2026-08-13 review). Bounded by theme colors ever seen.
    private static readonly Dictionary<Color, LinearGradientBrush> GradientCache = [];

    private static LinearGradientBrush GradientFor(Brush stroke)
    {
        var c = stroke is SolidColorBrush s ? s.Color : Colors.Gray;
        if (GradientCache.TryGetValue(c, out var cached)) return cached;
        var grad = new LinearGradientBrush(
            Color.FromArgb(56, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B),
            new Point(0, 0), new Point(0, 1));
        grad.Freeze();
        return GradientCache[c] = grad;
    }

    private static void AppendCurve(StreamGeometryContext ctx, double[] xs, double[] ys,
        double[] t, bool close, double baseline)
    {
        ctx.BeginFigure(new Point(xs[0], ys[0]), close, close);
        for (var i = 0; i < xs.Length - 1; i++)
        {
            var dx = xs[i + 1] - xs[i];
            ctx.BezierTo(
                new Point(xs[i] + dx / 3, ys[i] + t[i] * dx / 3),
                new Point(xs[i + 1] - dx / 3, ys[i + 1] - t[i + 1] * dx / 3),
                new Point(xs[i + 1], ys[i + 1]), true, false);
        }
        if (close)
        {
            ctx.LineTo(new Point(xs[^1], baseline), false, false);
            ctx.LineTo(new Point(xs[0], baseline), false, false);
        }
    }

    private FormattedText Caption(string text, Brush brush, double size = 10, bool bold = false) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
}

/// <summary>
/// The lanes: label column left, marks right. Solid bars are damage (height tracks
/// the hit's share of the lane's biggest), hollow outlines are the attempts that
/// produced nothing — miss, resist, fizzle, absorb. Time axis along the bottom.
/// </summary>
internal sealed class LanesPanel : FrameworkElement
{
    /// <summary>Name column + right-aligned totals column, the mockup's fixed-gutter
    /// rule: a lane name can never sit on top of its own total again.</summary>
    public const double LabelWidth = 176;
    private const double MinLaneHeight = 24;
    private const double MaxLaneHeight = 68;
    private const double AxisHeight = 16;

    internal TimelineViewport? View;
    internal event Action<TimelineMark?, TimelineLane?, Point>? HoverChanged;

    /// <summary>This render's lane height: lanes STRETCH to fill the window (David's
    /// pass on a 5-lane, 28-second fight: 24px lanes huddled at the top of a half-empty
    /// window). A crowded fight falls back to the minimum and scrolls.</summary>
    private double _laneHeight = MinLaneHeight;

    public LanesPanel()
    {
        MouseMove += OnMove;
        MouseLeave += (_, _) => HoverChanged?.Invoke(null, null, default);
        MouseLeftButtonDown += (_, e) => { _panFrom = e.GetPosition(this).X; CaptureMouse(); };
        MouseLeftButtonUp += (_, _) => { _panFrom = null; ReleaseMouseCapture(); };
    }

    private double? _panFrom;

    /// <summary>Height claims the lanes' room so the ScrollViewer can scroll a tall
    /// fight; call after the timeline changes or the window resizes.</summary>
    public void Refit()
    {
        var lanes = Math.Max(1, View?.Timeline?.Lanes.Count ?? 0);
        var available = Parent is System.Windows.Controls.ScrollViewer sv && sv.ViewportHeight > 0
            ? sv.ViewportHeight : ActualHeight;
        _laneHeight = Math.Clamp((available - AxisHeight - 2) / lanes, MinLaneHeight, MaxLaneHeight);
        Height = Math.Max(60, lanes * _laneHeight + AxisHeight + 2);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        // Hit-test background: pan-drags on empty plot need the element to BE there.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (View is not { Timeline: { } t } v) return;
        v.CaptureTheme(this);
        var plotW = ActualWidth - LabelWidth;
        if (plotW <= 10) return;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var face = new Typeface("Segoe UI");
        var hairline = new Pen(v.Dim, 0.35);

        // Type scales gently with the lane height — tall lanes on a short fight
        // shouldn't whisper their own names.
        var nameSize = Math.Clamp(11 + (_laneHeight - MinLaneHeight) / 14, 11, 13.5);
        // Hoisted out of the lane loop (2026-08-13 review): OnRender runs at
        // pan-mousemove rate, and these depend only on theme brushes.
        var critGlow = v.ChartCrit is SolidColorBrush glowSrc
            ? new SolidColorBrush(Color.FromArgb(64, glowSrc.Color.R, glowSrc.Color.G, glowSrc.Color.B))
            : null;
        critGlow?.Freeze();
        var phasePen = new Pen(v.Dim, 0.7) { DashStyle = DashStyles.Dot };
        for (var i = 0; i < t.Lanes.Count; i++)
        {
            var lane = t.Lanes[i];
            var y = i * _laneHeight;
            dc.DrawLine(hairline, new Point(0, y + _laneHeight), new Point(ActualWidth, y + _laneHeight));

            var name = new FormattedText(lane.Name, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, face, nameSize,
                lane.Kind == LaneKind.Incoming ? v.ChartIncoming : v.Text, dpi)
            { MaxTextWidth = LabelWidth - 76, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
            dc.DrawText(name, new Point(2, y + _laneHeight / 2 - name.Height / 2));
            if (lane.Total > 0)
            {
                // Say what the number IS — "307" alone read as a mystery (David's pass).
                var total = new FormattedText(
                    Short(lane.Total) + (lane.Kind == LaneKind.Incoming ? " taken" : " dmg"),
                    CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 10, v.Dim, dpi);
                dc.DrawText(total, new Point(LabelWidth - total.Width - 12,
                    y + _laneHeight / 2 - total.Height / 2));
            }

            var laneMax = Math.Max(1, lane.Marks.Max(m => m.Amount));
            // Chart-series steps, not ambient accents (2026-08-13 pass): incoming
            // wears the cool chart blue — red stays reserved for genuine status.
            var solid = lane.Kind switch
            {
                LaneKind.Pet => v.ChartPet,
                LaneKind.Incoming => v.ChartIncoming,
                _ => v.ChartYou,
            };
            // Hollow = a failed attempt. Neutral outline on purpose: it used to share
            // the crit's warn color, and two meanings in one hue read as one meaning.
            var hollowPen = new Pen(v.Dim, 1.1);
            var markW = Math.Clamp(v.PixelsPerSec * 0.25, 3, 3 + _laneHeight / 12);
            var barRoom = _laneHeight - 6;
            var hollowH = Math.Round(barRoom * 0.45);
            foreach (var m in lane.Marks)
            {
                var x = LabelWidth + (m.Sec - v.OffsetSec) * v.PixelsPerSec;
                if (x < LabelWidth - 4 || x > ActualWidth + 4) continue;
                if (m.Hollow)
                    dc.DrawRoundedRectangle(null, hollowPen,
                        new Rect(x, y + (_laneHeight - hollowH) / 2, markW + 1, hollowH), 1.5, 1.5);
                else
                {
                    var bar = barRoom * (0.25 + 0.75 * Math.Sqrt((double)m.Amount / laneMax));
                    var rect = new Rect(x, y + _laneHeight - 3 - bar, markW, bar);
                    if (m.Crit && critGlow is not null)   // the one bright accent, softly haloed
                        dc.DrawRoundedRectangle(critGlow, null,
                            new Rect(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4), 3, 3);
                    dc.DrawRoundedRectangle(m.Crit ? v.ChartCrit : solid, null, rect, 1.5, 1.5);
                }
            }
        }

        // Phase markers: stance/invocation changes as thin dotted boundaries across
        // the lanes with a small label at the top — a mode change has no amount, so
        // it gets a line, not a lane.
        foreach (var phase in t.Phases)
        {
            var px = LabelWidth + (phase.Sec - v.OffsetSec) * v.PixelsPerSec;
            if (px < LabelWidth - 2 || px > ActualWidth + 2) continue;
            dc.DrawLine(phasePen,
                new Point(px, 0), new Point(px, t.Lanes.Count * _laneHeight));
            var pl = new FormattedText($"→ {phase.Label}", CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, face, 9, v.Dim, dpi);
            dc.DrawText(pl, new Point(
                Math.Min(px + 3, ActualWidth - pl.Width - 2), 1));
        }

        DrawAxis(dc, v, t, plotW, t.Lanes.Count * _laneHeight, face, dpi);
    }

    private void DrawAxis(DrawingContext dc, TimelineViewport v, FightTimeline t,
        double plotW, double y, Typeface face, double dpi)
    {
        var visible = plotW / v.PixelsPerSec;
        var step = new[] { 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 }
            .First(s => visible / s <= 12 || s == 600);
        for (var sec = Math.Ceiling(v.OffsetSec / step) * step;
             sec <= Math.Min(t.DurationSeconds, v.OffsetSec + visible); sec += step)
        {
            var x = LabelWidth + (sec - v.OffsetSec) * v.PixelsPerSec;
            dc.DrawLine(new Pen(v.Dim, 0.35), new Point(x, y), new Point(x, y + 4));
            var label = new FormattedText(FightTimelineWindow.Clock(sec),
                CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, 9.5, v.Dim, dpi);
            dc.DrawText(label, new Point(x - label.Width / 2, y + 4));
        }
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (_panFrom is { } from && e.LeftButton == MouseButtonState.Pressed)
        {
            ((FightTimelineWindow)Window.GetWindow(this)!).Pan(pos.X - from);
            _panFrom = pos.X;
            return;
        }
        if (View is not { Timeline: { } t } v || pos.X < LabelWidth)
        { HoverChanged?.Invoke(null, null, default); return; }

        var laneIx = (int)(pos.Y / _laneHeight);
        if (laneIx < 0 || laneIx >= t.Lanes.Count)
        { HoverChanged?.Invoke(null, null, default); return; }
        var lane = t.Lanes[laneIx];
        var sec = v.OffsetSec + (pos.X - LabelWidth) / v.PixelsPerSec;
        var slack = 6 / v.PixelsPerSec;   // 6 px of grace either side
        TimelineMark? best = null;
        foreach (var m in lane.Marks)   // sorted by Sec; small enough to walk
        {
            if (m.Sec > sec + slack) break;
            if (m.Sec >= sec - slack &&
                (best is null || Math.Abs(m.Sec - sec) < Math.Abs(best.Sec - sec)))
                best = m;
        }
        HoverChanged?.Invoke(best, best is null ? null : lane, pos);
    }

    internal static string Short(long n) => n switch
    {
        >= 1_000_000 => $"{n / 1_000_000.0:0.#}m",
        >= 10_000 => $"{n / 1000.0:0.#}k",
        >= 1_000 => $"{n / 1000.0:0.##}k",
        _ => n.ToString("N0"),
    };
}
