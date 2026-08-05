using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Live per-attacker breakdown for whatever pull is currently in progress, plus running
/// totals for a user-picked roster that survive across pulls until Reset is clicked. Click
/// or right-click a name in the current-pull list to track/untrack it. Mini/Normal split
/// mirrors MainWindow's own dashboard-minimize feature (same icons, same
/// SizeToContent="WidthAndHeight" shrink-to-fit behavior). Fed by
/// <see cref="PartyDpsTracker"/> — own timer, own window, no changes to SessionStats or the
/// History pipeline. See PartyDpsTracker for why "party" here means "everyone visible in
/// your log," not a verified group roster.
/// </summary>
public partial class PartyDpsWindow : Window
{
    private readonly PartyDpsTracker _tracker;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _tick;
    private readonly HashSet<string> _tracked = new(StringComparer.OrdinalIgnoreCase);
    private bool _mini;

    public PartyDpsWindow(MainWindow main, PartyDpsTracker tracker)
    {
        InitializeComponent();
        _tracker = tracker;
        _settings = main.Settings;
        // Running totals are scoped to "since this window has been open" — the tracker
        // itself lives for the whole app session (and gets fed the full startup replay of
        // today's log), so without this a first open could show totals inflated by
        // everything that happened before you ever looked.
        _tracker.ResetTotals();

        MaxHeight = SystemParameters.WorkArea.Height - 40;
        BodyScroll.MaxHeight = SystemParameters.WorkArea.Height - 140;
        // ActualWidth isn't known until after the first layout pass (SizeToContent means
        // there's no fixed Width to read at construction time), so position once loaded.
        Loaded += (_, _) => PositionNextTo(main);

        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => Refresh();
        _tick.Start();
        Refresh();

        Closed += (_, _) => _tick.Stop();
    }

    private void Refresh()
    {
        var now = DateTime.Now;

        // Matches the main widget's own background opacity (Options → widget transparency)
        // instead of the theme's fixed panel alpha, and stays live if it's changed there
        // while this window is open.
        var tint = ((SolidColorBrush)FindResource("BgBrush")).Color;
        RootBorderElement.Background = new SolidColorBrush(
            Color.FromArgb((byte)(_settings.BackgroundOpacity * 255), tint.R, tint.G, tint.B));

        var pull = _tracker.Snapshot(now);

        if (_mini)
        {
            UpdateMiniChips(pull);
            return;
        }

        PullSummaryText.Text = pull.Rows.Count == 0
            ? "No combat seen yet."
            : $"{pull.TotalDamage:N0} total dmg · {pull.TotalDamage / pull.DurationSeconds:0.#} dps · " +
              $"{pull.DurationSeconds:0}s{(pull.Active ? "" : " (pull ended)")}";
        FillTrackablePullRows(pull.Rows, pull.DurationSeconds);

        var totals = _tracker.TotalsSnapshot(now, _tracked);
        TotalsSummaryText.Text = _tracked.Count == 0
            ? "Right-click (or click) a name above to start tracking it."
            : totals.Rows.Count == 0
                ? "Tracked, but no damage seen from them yet."
                : $"{totals.TotalDamage:N0} total dmg · {totals.TotalDamage / totals.DurationSeconds:0.#} dps · " +
                  $"{SpawnDurationText.Format(totals.DurationSeconds)} in combat";
        BreakdownRows.FillAbilityRows(this, TotalsRowsList, totals.Rows, totals.DurationSeconds, "dps");
    }

    /// <summary>Top attackers as bar rows — same BreakdownRows.Row style (and same highest-
    /// first order, since pull.Rows is already sorted by total damage) as the full view,
    /// just condensed to a bare dps number instead of the full stat line.</summary>
    private void UpdateMiniChips(PartyDpsSnapshot pull)
    {
        MiniChips.Children.Clear();
        if (pull.Rows.Count == 0)
        {
            MiniChips.Children.Add(new TextBlock
            {
                Text = "No combat seen yet.", FontSize = 12,
                Foreground = (Brush)FindResource("DimBrush"),
            });
            return;
        }
        var secs = Math.Max(1, pull.DurationSeconds);
        var top = Math.Max(1, pull.Rows.Max(d => d.Total));
        var barBrush = BreakdownRows.BarBrush(this);
        foreach (var d in pull.Rows.Take(4))
        {
            var value = $"{d.Total / secs:0.#} dps";
            MiniChips.Children.Add(BreakdownRows.Row(this, d.Name, value, (double)d.Total / top, barBrush, null));
        }
    }

    /// <summary>Same row rendering as BreakdownRows.FillAbilityRows (total/hits/avg/rate/
    /// crit%, share bar), built locally instead of through that shared helper so each row
    /// can carry a track/untrack click and context menu, and a ✓ prefix once tracked.</summary>
    private void FillTrackablePullRows(IReadOnlyList<SourceDamage> stats, double combatSeconds)
    {
        PullRowsList.Items.Clear();
        if (stats.Count == 0) return;
        var top = Math.Max(1, stats.Max(d => d.Total));
        var secs = Math.Max(1, combatSeconds);
        var barBrush = BreakdownRows.BarBrush(this);
        foreach (var d in stats)
        {
            var tracked = _tracked.Contains(d.Name);
            var critPart = d.Crits > 0 ? $" · {100.0 * d.Crits / Math.Max(1, d.Hits):0}% crit" : "";
            var value = $"{d.Total:N0} · ×{d.Hits} · avg {(double)d.Total / Math.Max(1, d.Hits):0.#}" +
                        $" · {d.Total / secs:0.#} dps{critPart}";
            var tooltip = (tracked ? "Tracked — click to stop. " : "Click to track. ") +
                $"dps = total ÷ {secs:0}s in combat";
            var name = (tracked ? "✓ " : "") + d.Name;
            var row = BreakdownRows.Row(this, name, value, (double)d.Total / top, barBrush, tooltip);
            row.Cursor = Cursors.Hand;
            // Stop this from bubbling to the Border's OnDrag (MouseLeftButtonDown) — otherwise
            // a click here starts a DragMove and the click never reaches MouseLeftButtonUp below.
            row.MouseLeftButtonDown += (_, e2) => e2.Handled = true;
            row.MouseLeftButtonUp += (_, _) => ToggleTracked(d.Name);
            var menu = new ContextMenu();
            var item = new MenuItem { Header = tracked ? $"Stop tracking {d.Name}" : $"Track {d.Name}" };
            item.Click += (_, _) => ToggleTracked(d.Name);
            menu.Items.Add(item);
            row.ContextMenu = menu;
            PullRowsList.Items.Add(row);
        }
    }

    private void ToggleTracked(string name)
    {
        if (!_tracked.Remove(name)) _tracked.Add(name);
        Refresh();
    }

    /// <summary>Opens beside the main widget — to its right if there's room on the primary
    /// work area, otherwise its left — rather than defaulting to screen-center where it'd
    /// land nowhere near what you're actually tracking.</summary>
    private void PositionNextTo(MainWindow main)
    {
        const double gap = 10;
        var work = SystemParameters.WorkArea;
        var rightOf = main.Left + main.ActualWidth + gap;
        Left = rightOf + ActualWidth <= work.Right ? rightOf : Math.Max(work.Left, main.Left - ActualWidth - gap);
        Top = main.Top;
    }

    private void SetMode(bool mini)
    {
        _mini = mini;
        MiniRoot.Visibility = mini ? Visibility.Visible : Visibility.Collapsed;
        NormalRoot.Visibility = mini ? Visibility.Collapsed : Visibility.Visible;
        Refresh();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => SetMode(true);
    private void OnRestore(object sender, RoutedEventArgs e) => SetMode(false);

    private void OnResetTotals(object sender, RoutedEventArgs e) => _tracker.ResetTotals();

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && _mini)
        {
            SetMode(false);
            return;
        }
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not TextBox) DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
