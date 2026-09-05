using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EQBuddy.Core;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// The COLLAPSED HUD bar — the row of numbers the widget shows while minimized, which is
/// the surface on screen for the whole time a player is farming.
///
/// Lifted out of <c>MainWindow</c> for Surface A / SA-1. **A view class, not another
/// <c>MainWindow.*.xaml.cs</c> partial**: <c>ArchitectureTests</c> sums the glob's matches
/// on purpose, so a partial buys nothing and leaves exactly as much untestable window
/// logic as before. The ratchet had zero headroom (4,516 lines against 4106 × 1.1 =
/// 4,516.6) and the standing move is to lift a surface rather than raise the ceiling.
///
/// Its behaviour was pinned in <c>tests/EQBuddy.E2E</c> BEFORE the move (<c>hudCells</c>,
/// green on the pre-move tree) — the WPF layer has no unit tests (docs/TestPlan.md §5),
/// so that assertion is the only thing standing between this move and a silent
/// regression. Same discipline as <c>WatchCardView</c> and <c>TravelsView</c>.
///
/// **It is not an <see cref="IWidgetCard"/> and takes no <see cref="ICardContext"/>.** It
/// is not a card: it has no section key, hangs in no expander, and needs none of the six
/// item/wiki services that interface exists for. What it genuinely cannot answer for
/// itself is handed in — the alert scheduler's due map, and the two windows a chip's
/// double-click opens — which is the same rule <c>ICardContext</c> applies one level up.
///
/// **Visibility and spacing stay with the host** (trap 15): this fills a panel the widget
/// owns and shows or hides nothing. <c>MainWindow</c> decides when the bar is on screen.
/// </summary>
internal sealed class HudBarView
{
    private readonly Panel _host;
    private readonly AppSettings _settings;
    private readonly Func<DateTime, IReadOnlyDictionary<string, DateTime>> _cuesDue;
    private readonly Action<BreakoutKind> _toggleBreakout;
    private readonly Action _openProgress;

    // Double-click state for the breakout chips, at the level of THIS view rather than of
    // an element: the chips are rebuilt every tick, so a rebuild landing between the two
    // clicks would leave the second one on a brand-new element with ClickCount back at 1.
    // Threshold reads the user's own Windows double-click speed; floor for a stray zero.
    private string? _lastChipClickKey;
    private DateTime _lastChipClickAt = DateTime.MinValue;
    private static readonly TimeSpan DoubleClickWindow =
        TimeSpan.FromMilliseconds(Math.Max(200, GetDoubleClickTime()));

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    /// <summary>Cells currently on the bar, for the <c>EQBUDDY_EXPAND</c> dump the E2E
    /// suite asserts on. Recorded by <see cref="Render"/> rather than read back off the
    /// panel, because the empty-state hint is also a child: a panel count would answer 1
    /// for a bar with nothing on it, which is precisely the state the fact exists to
    /// distinguish.</summary>
    public int CellCount { get; private set; }

    /// <param name="cuesDue">The alert scheduler's "when does each rule's cue fire" map.
    /// The bar cannot derive this from a snapshot — a cue is scheduled by the alert path,
    /// not by the session — so it is handed in rather than reached for.</param>
    /// <param name="toggleBreakout">Show or hide a breakout window; a chip's
    /// double-click.</param>
    /// <param name="openProgress">Open the Progress window; the xp chip's
    /// double-click.</param>
    public HudBarView(Panel host, AppSettings settings,
        Func<DateTime, IReadOnlyDictionary<string, DateTime>> cuesDue,
        Action<BreakoutKind> toggleBreakout, Action openProgress)
    {
        _host = host;
        _settings = settings;
        _cuesDue = cuesDue;
        _toggleBreakout = toggleBreakout;
        _openProgress = openProgress;
    }

    /// <summary>One mini-dashboard stat (2026-08-11, take two — David: no ovals):
    /// glyph + semibold tabular value as clean text, separated from its neighbor by
    /// a thin hairline divider rather than any chip chrome. A counting-down watch
    /// rule still announces itself by color alone. A chip whose stat has a breakout
    /// window takes a double-click to toggle it.</summary>
    /// <summary>
    /// <paramref name="onDoubleClick"/> is what the gesture DOES, and it is pluggable because
    /// not every chip toggles a breakout any more: the xp chip opens the Progress WINDOW, which
    /// has the tabs (Bevel's fold, Helm-signed — "reuse existing theme window on current tab …
    /// retire tab-less 272×135 float"). The gesture is keyed on <paramref name="clickKey"/>
    /// rather than on a BreakoutKind so a chip with no breakout can still own a double-click.
    /// </summary>
    private StackPanel Chip(string iconName, string value, string valueBrush, string? edgeBrush = null,
        BreakoutKind? breakout = null, string? clickKey = null, Action? onDoubleClick = null,
        string? doubleClickHint = null)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, Tok.SpaceL, 0),
        };
        var key = clickKey ?? breakout?.ToString();
        var act = onDoubleClick ?? (breakout is { } bk ? () => _toggleBreakout(bk) : null);
        if (key is not null && act is not null && _settings.DoubleClickChipsToggleBreakouts)
        {
            // Transparent (not null) so the gaps between glyph and value are hit-testable
            // too. Two things conspired against WPF's own double-click here, so we detect it
            // ourselves at this view's level:
            //   1. The bar's OnDrag starts a modal window DragMove on the FIRST left-click
            //      anywhere on the bar; that capture disrupted the click sequence and the
            //      cursor flickered into drag mode (the tell). Eating the click stops it.
            //   2. Render rebuilds these panels every 1s tick, so a rebuild landing
            //      between the two clicks left the second click on a brand-new element and
            //      reset ClickCount to 1 — an intermittent miss.
            // Keying the double-click on (kind, time) on the VIEW survives both: the panel
            // can be replaced mid-gesture and the second click still lands. The widget is
            // still dragged from any non-chip part of the bar; when the opt-in is off the
            // chip stays inert and a double-click expands the widget as before.
            panel.Background = System.Windows.Media.Brushes.Transparent;
            panel.Cursor = Cursors.Hand;
            panel.ToolTip = doubleClickHint ?? $"Double-click to show or hide the {key} breakout";
            panel.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                var now = DateTime.Now;
                if (_lastChipClickKey == key && now - _lastChipClickAt <= DoubleClickWindow)
                {
                    _lastChipClickKey = null;   // consume, so a third click starts fresh
                    act();
                }
                else
                {
                    _lastChipClickKey = key;
                    _lastChipClickAt = now;
                }
            };
        }
        // A vector, not a glyph (#148, #166): the collapsed bar is on screen the whole
        // time a player farms, and it is exactly where a box instead of a skull would go
        // unnoticed on a Wine prefix.
        var icon = DesignSystem.Icon(iconName, "AccentBrush", size: Tok.IconInline);
        icon.Opacity = 0.9;
        icon.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
        panel.Children.Add(icon);
        var v = new TextBlock
        {
            Text = value, FontSize = Tok.Spec(Tok.TypeRole.TitleSection).Size,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        v.SetResourceReference(TextBlock.ForegroundProperty, edgeBrush ?? valueBrush);
        panel.Children.Add(v);
        var divider = new Border
        {
            Width = 1,
            Margin = new Thickness(Tok.SpaceL, Tok.SpaceXxs, 0, Tok.SpaceXxs),
        };
        divider.SetResourceReference(Border.BackgroundProperty, "HairlineBrush");
        panel.Children.Add(divider);
        return panel;
    }

    /// <summary>The last chip's divider has nothing to divide — trim it.</summary>
    private void TrimLastDivider()
    {
        if (_host.Children.Count > 0 && _host.Children[^1] is StackPanel { Children.Count: > 0 } last
            && last.Children[^1] is Border divider)
            divider.Visibility = Visibility.Collapsed;
    }

    public void Render(StatsSnapshot s)
    {
        _host.Children.Clear();
        // Which cells, in which order, with which icon and what each reads: all from
        // UI.Shared. Both widgets carried this table by hand, identically, comments and
        // all — and the Avalonia one is the lane that historically drifted.
        foreach (var cell in UI.Shared.MiniBarPresentation.Cells(s, _settings.MiniStats))
        {
            BreakoutKind? breakout = cell.Key switch
            {
                "dps" => BreakoutKind.Damage,
                "hps" => BreakoutKind.Healing,
                "pet" => BreakoutKind.Pet,
                "loot" => BreakoutKind.Loot,
                _ => null,   // kills/procs/motes/money/deaths have no breakout
            };
            // xp is the exception: it opens the PROGRESS WINDOW, which has the Experience /
            // Wealth / Faction / Raids tabs, rather than the tab-less 272x135 float it used to
            // (Bevel, Helm-signed 2026-08-24: "Fold Progress breakout into that pop-out. Retire
            // tab-less 272x135 float."). Same gesture, same one gate, a surface with tabs.
            _host.Children.Add(cell.Key == "xp"
                ? Chip(cell.Icon, cell.Text, "AccentBrush", clickKey: "xp",
                    onDoubleClick: _openProgress,
                    doubleClickHint: "Double-click to open the Progress window")
                : Chip(cell.Icon, cell.Text, "AccentBrush", breakout: breakout));
        }

        // Per-rule pins: only the rules you picked (📌 in Options), not every enabled one.
        // The master toggle still gates the lot, so turning chips off is one click.
        var due = _cuesDue(DateTime.Now);
        foreach (var rule in _settings.PinWatchChips
                     ? _settings.TrackedRules.Where(r => r.Enabled && r.Pinned)
                     : [])
        {
            var name = rule.Name.Length > 0 ? rule.Name : rule.Pattern;
            var result = s.Tracked.FirstOrDefault(t => t.Id == rule.Id);
            // A rule with a cue in flight shows time remaining instead of its count: while
            // something is counting down, when it fires is the only thing you want to know.
            var counting = due.TryGetValue(rule.Id, out var at);
            // A counting-down chip wears the warn edge too — state has a shape.
            _host.Children.Add(counting
                ? Chip("Timer", $"{name} {EQBuddy.UI.Shared.Countdown.Format(at - DateTime.Now)}",
                    "WarnBrush", edgeBrush: "WarnBrush", breakout: BreakoutKind.Watch)
                : Chip("Target", $"{name} {result?.TotalQuantity ?? 0}", "AccentBrush",
                    breakout: BreakoutKind.Watch));
        }

        TrimLastDivider();
        CellCount = _host.Children.Count;   // BEFORE the hint, which is not a cell
        // The hint belongs at the end, and only when there's genuinely nothing to show. It
        // used to return early when no stats were starred, which meant someone who pinned
        // watch rules but starred nothing got the hint instead of their chips.
        if (_host.Children.Count == 0)
        {
            // The hollow star is the CONTROL it points at — the one beside every card
            // header — so it is the same vector those wear rather than a lookalike glyph.
            // Safe as a StackPanel: one short line that never wraps (trap 14).
            var hint = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var star = DesignSystem.Icon("Star", "DimBrush", size: Tok.IconInline);
            star.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
            hint.Children.Add(star);
            hint.Children.Add(DesignSystem.Text(Tok.TypeRole.Body, "star stats in full view")
                .Ink("DimBrush"));
            _host.Children.Add(hint);
        }
    }
}
