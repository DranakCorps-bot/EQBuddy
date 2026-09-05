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
/// **The two surfaces `HistoryWindow`'s this-session half brings to the Live room** — the
/// session DPS graph (<see cref="LiveTab.Pace"/>) and the pull-by-pull review
/// (<see cref="LiveTab.Encounters"/>). Bevel's History pre-design §3, Helm-signed
/// 2026-09-05 ~10:10 AM CT.
///
/// **They read <c>MainWindow.CurrentSnapshot()</c> and never the checkpoint, which is the
/// whole point of the merge.** The v1 studio builds the identical two surfaces from a
/// stored row: <c>MainWindow</c> checkpoints the running session into SQLite every five
/// minutes and <c>HistoryViewModel.UpdateSelectionDetail</c> loads that row's snapshot ONCE,
/// on selection, with nothing wired to reload it. So a player who opens History mid-session
/// and clicks the top row sees a picture frozen at the last checkpoint, for as long as the
/// window stays up — in the one surface whose replacement exists because the numbers move.
/// Nothing here goes near <c>SessionRepository</c>: <c>StatsSnapshot.DamageTimeline</c> and
/// <c>StatsSnapshot.Encounters</c> are live in-memory fields recomputed every tick, which is
/// what the room's other six surfaces already read.
///
/// **Neither pane decides anything.** The words, the badge, the captions and both repaint
/// gates are <see cref="LivePresentation"/>'s; the geometry is
/// <see cref="HistoryPresentation.BuildDpsGraph"/>'s and the grouping is
/// <see cref="EncounterGrouping.Group"/>'s — the same three the studio calls. The WPF layer
/// has no unit tests (docs/TestPlan.md §5), so anything left as a rule in here is a rule
/// nothing can check.
///
/// **In their own file rather than inside <c>LiveRoom</c>**, per the E-3 lane contract: new
/// content arrives as new files, and the room is already 800 lines of six surfaces.
/// </summary>
internal static class LiveSessionPanes
{
    /// <summary>
    /// **How the whole sitting has gone** — one point per minute, drawn as a polyline.
    ///
    /// **It is NOT the Timeline tab and must never be labelled as one** (the signed §3
    /// refusal). <c>TimelinePane</c> is one PULL's per-event lanes; this is every minute of
    /// the session. Both are "a graph of damage over time" and they answer different
    /// questions, so the collision is in the WORD rather than in the surfaces.
    /// </summary>
    public sealed class PacePane
    {
        private readonly FrameworkElement _resources;
        private readonly StackPanel _panel = new();
        private readonly TextBlock _caption;
        private readonly TextBlock _empty;
        private readonly Border _frame;
        private readonly Canvas _canvas = new() { Height = GraphHeight };

        /// <summary>Tall enough to read a shape off and short enough that the caption above
        /// it is still on screen at the 520-unit floor. The studio's own graph is 96; this
        /// is a room rather than a pane in a scrolling column, so it gets a little more.
        /// </summary>
        private const double GraphHeight = 120;

        /// <summary>The last signature painted — <see cref="LivePresentation.PaceSignature"/>,
        /// which moves when the polyline's shape would and never on a clock. Redrawing a
        /// polyline is cheap; doing it every second for a session whose last minute had no
        /// damage is the churn trap 8 is about.</summary>
        private string _signature = "";

        /// <summary>The width the polyline was last laid out at. A canvas has no width until
        /// WPF has arranged it, so the first paint would draw at the 300-unit fallback and
        /// stay there — which is exactly the bug <c>HistoryWindow.OnGraphSizeChanged</c>
        /// exists for. Kept so a resize redraws and a repaint at the same width does not.
        /// </summary>
        private double _width;

        private IReadOnlyList<TimelinePoint> _timeline = [];

        public UIElement Body => _panel;

        /// <summary>Points actually plotted, for the dump. Zero means the graph declined to
        /// draw — which is a real state (a sitting under two minutes long) and not a
        /// failure, so it is reported rather than asserted away.</summary>
        public int PointCount { get; private set; }

        public PacePane(FrameworkElement resources)
        {
            _resources = resources;

            _caption = DesignSystem.Text(Role.BodySecondary, "");
            _caption.TextWrapping = TextWrapping.Wrap;
            _caption.Ink("DimBrush");
            _panel.Children.Add(_caption);

            _frame = new Border
            {
                Background = (Brush)resources.FindResource("PanelBrush"),
                CornerRadius = new CornerRadius(Tok.RadiusCard),
                Padding = new Thickness(Tok.SpaceS, Tok.SpaceXs, Tok.SpaceS, Tok.SpaceXs),
                Margin = new Thickness(0, Tok.SpaceXs, 0, 0),
                Child = _canvas,
            };
            _panel.Children.Add(_frame);

            _empty = CardParts.EmptyLine(LivePresentation.EmptyPace);
            _empty.TextWrapping = TextWrapping.Wrap;
            _panel.Children.Add(_empty);

            // Redraw at the REAL width once layout settles, and again on every resize. The
            // canvas is the thing that stretches, so it is the thing that reports.
            _canvas.SizeChanged += (_, e) =>
            {
                if (e.NewSize.Width <= 0 || Math.Abs(e.NewSize.Width - _width) < 0.5) return;
                _width = e.NewSize.Width;
                Draw();
            };
        }

        public void Render(StatsSnapshot s)
        {
            _timeline = s.DamageTimeline;
            var signature = LivePresentation.PaceSignature(_timeline);
            if (signature == _signature) return;
            _signature = signature;
            Draw();
        }

        private void Draw()
        {
            _canvas.Children.Clear();
            var width = _canvas.ActualWidth > 0 ? _canvas.ActualWidth : 300;
            var graph = HistoryPresentation.BuildDpsGraph(_timeline, width, GraphHeight - 8);
            PointCount = graph?.Points.Count ?? 0;

            // The caption, the frame and the empty line move TOGETHER — three switches for
            // one state is trap 17's shape, and a caption over a blank frame reads as a
            // graph that failed rather than a sitting that is two minutes old.
            var has = graph is not null;
            _caption.Visibility = _frame.Visibility =
                has ? Visibility.Visible : Visibility.Collapsed;
            _empty.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
            if (graph is null) return;

            _caption.Text = LivePresentation.PaceCaption(graph);
            var line = new Polyline
            {
                Stroke = (Brush)_resources.FindResource("AccentBrush"),
                StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round,
            };
            foreach (var (x, y) in graph.Points) line.Points.Add(new Point(x, y + 4));
            _canvas.Children.Add(line);
        }
    }

    /// <summary>
    /// **Every finished pull of this sitting, oldest first, each one expandable** — the
    /// review `HistoryWindow` has always had for a stored session, on the one that is
    /// running.
    ///
    /// **The expansion set is keyed on the PULL and survives a rebuild.** The room paints
    /// once a second; a list that rebuilt its children every tick would close whatever the
    /// player had just opened, which is the kind of defect that reads as "the app is
    /// fighting me" and never gets reported as a bug. Two things stop it: the signature gate
    /// (nothing is rebuilt while no pull has closed) and <see cref="_expanded"/>, which
    /// restores the open rows when one has. Belt and braces on purpose — the gate is an
    /// optimisation and the set is the correctness.
    ///
    /// **The ⧉ is a FOURTH caller of <see cref="FightExport.ToText"/>, not a new export.**
    /// The Combat card, the Damage breakout and the studio all call the same function for
    /// the same Discord-ready block; a second formatter for one paste would be trap 33 in
    /// text.
    /// </summary>
    public sealed class EncountersPane
    {
        private readonly FrameworkElement _resources;
        private readonly Func<(string Server, string Character)> _who;
        private readonly StackPanel _panel = new();
        private readonly TextBlock _empty;
        private readonly StackPanel _rows = new();

        /// <summary>Which pulls are open, by <see cref="LivePresentation.PullKey"/>. Not by
        /// index: a new pull is appended today, and an index would be the wrong key the day
        /// the order or the grouping gap changes.</summary>
        private readonly HashSet<string> _expanded = [];

        private string _signature = "";
        private IReadOnlyList<TimedDetail> _deaths = [];

        public UIElement Body => _panel;

        /// <summary>Pull rows drawn, for the dump — compared against the grouping's own
        /// count so a list that silently stopped taking rows is visible from outside.
        /// </summary>
        public int RowCount { get; private set; }

        public EncountersPane(FrameworkElement resources, Func<(string Server, string Character)> who)
        {
            _resources = resources;
            _who = who;
            _empty = CardParts.EmptyLine(LivePresentation.EmptyEncounters);
            _empty.TextWrapping = TextWrapping.Wrap;
            _panel.Children.Add(_empty);
            _panel.Children.Add(_rows);
        }

        public void Render(StatsSnapshot s, IReadOnlyList<PullInfo> pulls)
        {
            // The deaths list is refreshed every paint even when the ROWS are not: it is
            // what the ⧉ copy annotates a pull with, and a gate keyed on pulls would hand
            // the exporter a death list from whenever the last pull closed.
            _deaths = s.Deaths;

            var signature = LivePresentation.EncountersSignature(pulls);
            if (signature == _signature) return;
            _signature = signature;

            _rows.Children.Clear();
            RowCount = pulls.Count;
            _empty.Visibility = pulls.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            // OLDEST FIRST, which is the studio's order and the order the session happened
            // in. A newest-first list is the right default for a browse of many sittings and
            // the wrong one for the inside of one: "how did this camp go" is read forwards.
            foreach (var pull in pulls) _rows.Children.Add(BuildRow(pull));
        }

        private UIElement BuildRow(PullInfo pull)
        {
            var key = LivePresentation.PullKey(pull);
            var open = _expanded.Contains(key);

            var body = new StackPanel
            {
                Margin = new Thickness(Tok.SpaceL, 0, 0, Tok.SpaceXs),
                Visibility = open ? Visibility.Visible : Visibility.Collapsed,
            };
            var built = false;

            var header = new Button
            {
                Style = (Style)_resources.FindResource("IconButton"),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Left,
                Content = Chevron(open) + HistoryPresentation.BuildFightHeader(pull),
            };
            header.Click += (_, _) =>
            {
                var opening = body.Visibility != Visibility.Visible;
                body.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
                header.Content = Chevron(opening) + HistoryPresentation.BuildFightHeader(pull);
                if (opening) _expanded.Add(key); else _expanded.Remove(key);
                if (!opening || built) return;
                built = true;
                BuildDetail(body, pull);
            };
            if (open) { built = true; BuildDetail(body, pull); }

            var copy = new Button
            {
                Style = (Style)_resources.FindResource("IconButton"),
                FontSize = 11,
                Content = "⧉",
                Margin = new Thickness(Tok.SpaceS, 0, 0, 0),
                Foreground = (Brush)_resources.FindResource("DimBrush"),
                ToolTip = "Copy this encounter as Discord-ready text (a monospace block — "
                    + "the official Discord blocks images). Your numbers only, from your log.",
            };
            copy.Click += (_, _) => Copy(copy, pull);

            var line = new StackPanel { Orientation = Orientation.Horizontal };
            line.Children.Add(header);
            line.Children.Add(copy);

            var wrap = new StackPanel();
            wrap.Children.Add(line);
            wrap.Children.Add(body);
            return wrap;
        }

        private static string Chevron(bool open) => open ? "▾ " : "▸ ";

        private void Copy(Button copy, PullInfo pull)
        {
            try
            {
                Clipboard.SetText(FightExport.ToText(pull, _who().Character,
                    $"v{UpdateChecker.CurrentVersion}",
                    FightExport.DeathsDuring(pull.Start, pull.DurationSeconds, _deaths)));
                copy.Content = "✓";
                var t = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(1.5) };
                // Stops itself on its first tick, so it is not a timer the room is HOLDING —
                // `LiveRoom.Release()` says the room starts none, and this is why that stays
                // true: it exists for 1.5 seconds after a click a player made.
                t.Tick += (_, _) => { copy.Content = "⧉"; t.Stop(); };
                t.Start();
            }
            catch (Exception ex) { App.LogError(ex); }
        }

        /// <summary>One pull's breakdown, built on first expand — the studio's three
        /// sections in the studio's order, through the studio's own row builder. A
        /// multi-creature pull leads with the per-creature damage split, so "which of the
        /// three actually hurt" is answerable before any section is read.</summary>
        private void BuildDetail(StackPanel body, PullInfo pull)
        {
            if (pull.Fights.Count > 1)
            {
                var split = DesignSystem.Text(Role.BodySecondary,
                    string.Join(" · ", pull.Fights.Select(f => $"{f.Name} {f.DamageOut:N0}")));
                split.TextWrapping = TextWrapping.Wrap;
                split.Ink("DimBrush");
                split.Margin = new Thickness(0, Tok.SpaceXxs, 0, Tok.SpaceXxs);
                body.Children.Add(split);
            }

            Section("Your damage", pull.ByAbility, "dps");
            Section("Damage you took", pull.ByIncoming, "dps");
            Section("Heals during the fight", pull.HealsBySpell, "hps");
            if (body.Children.Count == 0)
                body.Children.Add(CardParts.EmptyLine(
                    "No per-fight detail — session recorded before EQBuddy 1.28."));

            void Section(string title, IReadOnlyList<SourceDamage> rows, string rate)
            {
                if (rows.Count == 0) return;
                body.Children.Add(CardParts.BlockLabel(title, hidden: false));
                var list = new ItemsControl();
                BreakdownRows.FillRows(_resources, list,
                    HistoryPresentation.BuildBreakdownRows(rows, pull.DurationSeconds, rate));
                body.Children.Add(list);
            }
        }
    }
}
