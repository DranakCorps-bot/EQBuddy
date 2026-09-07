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
/// same promise — **no geometry of its own and nothing persisted.** Where it goes is
/// recomputed from the widget every tick, so there is no saved position to go stale and
/// nothing for a <c>Closed</c> handler to record (trap 2).
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

    /// <summary>The pointer is over the panel itself. A peek must survive the trip from the
    /// chip to the panel — otherwise the panel collapses out from under the cursor that is
    /// reaching for its ⧉, which is a hover expand that cannot be used.</summary>
    public bool PointerInside { get; private set; }

    public HudExpandWindow(MainWindow main, AppSettings settings, HudExpandBar bar)
    {
        _main = main;
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

        _chrome = new Border
        {
            CornerRadius = new CornerRadius(Tok.RadiusCard),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(Tok.SpaceM, Tok.SpaceS, Tok.SpaceM, Tok.SpaceS),
            MinWidth = PanelMinWidth,
            MaxWidth = PanelMaxWidth,
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
    }

    /// <summary>Narrow enough to sit under a bare bar without looking detached, wide enough
    /// that an ability name and its number are not both ellipsed. A FIXED pair rather than a
    /// content-driven width: the panel is redrawn on the widget's one-second tick, and a
    /// window whose width tracks its longest row would jitter under the cursor every time a
    /// new ability landed.</summary>
    private const double PanelMinWidth = 260;
    private const double PanelMaxWidth = 340;

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

    /// <summary>Where the panel goes, from the widget, this tick — <see cref="HudChipRow.Placement"/>
    /// verbatim, so the panel and the chip row cannot disagree about what "under the HUD"
    /// means or about when there is no room below it.</summary>
    private void Park()
    {
        var area = SystemParameters.WorkArea;
        UpdateLayout();
        var (left, top) = HudChipRow.Placement(
            _main.Left, _main.Top, _main.ActualHeight, ActualHeight, area.Top, area.Bottom);
        if (Left != left) Left = left;
        if (Top != top) Top = top;
    }

    /// <summary>The panel's own height plus its gap, for the chip row to park BELOW rather
    /// than on top of. Zero while the panel is hidden or mid-collapse, so the row goes
    /// straight back under the bar the moment the panel is no longer claiming the space.
    /// </summary>
    public double OccupiedHeight =>
        IsVisible && !_closing && double.IsFinite(ActualHeight) ? ActualHeight + HudChipRow.HudGap : 0;

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
    /// </summary>
    private void Render(StatsSnapshot s, HudExpandTarget target)
    {
        if (target == HudExpandTarget.Progress) { RenderProgress(s); return; }

        var kind = target == HudExpandTarget.Hps
            ? BreakoutPresentation.Healing : BreakoutPresentation.Damage;
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

    /// <summary>The Progress glance: the launcher line the folded card already carries, plus
    /// the Experience room's own summary. One source for each, so the panel cannot say
    /// something the card and the window do not.</summary>
    private void RenderProgress(StatsSnapshot s)
    {
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
