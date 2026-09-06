using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>Which stat a breakout window tracks. Each kind is one singleton window with
/// its own remembered position and Fight/Session scope (Watch, Loot, Buffs and Progress
/// have no scope — their content is session/target/class shaped, so the toggle is
/// hidden).</summary>
public enum BreakoutKind { Damage, Healing, Pet, Watch, Loot, Buffs }

/// <summary>
/// A small floating bar-chart window for one stat — your damage, your healing, or the pet's
/// damage — by ability/spell, scoped to the current pull or the whole session (BREAKOUT-*,
/// David 2026-08-06). Opens automatically while the widget is minimized when the matching
/// section star is set: the stars already mean "this is what I watch when minimized", and
/// the breakout is the full-size version of that promise. ✕ hides it until the next
/// minimize, so an unwanted window never needs its star removed to go away.
///
/// Same chrome family as the spawn/mez chips: frameless, topmost, drag anywhere,
/// ScreenGuard-checked position persisted per kind, theme via resource references so a
/// live theme swap repaints it.
/// </summary>
public partial class BreakoutWindow : Window
{
    private readonly AppSettings _settings;
    private readonly BreakoutKind _kind;

    /// <summary>The owning widget — the Loot kind reads target-drops content and item
    /// click/hover behavior through it (same shared builder the Loot card uses).</summary>
    public MainWindow? Main { get; set; }

    /// <summary>Raised when the user ✕-dismisses the window — the owner disables this
    /// kind persistently (re-enabled under <see cref="BreakoutPresentation.ReEnableRoute"/>,
    /// discussion #45).</summary>
    public event Action<BreakoutKind>? Dismissed;

    private bool _fightScope;
    private string _signature = "";
    private StatsSnapshot? _lastSnapshot;

    /// <summary>The Loot kind's own surface, lifted out for Gate 4 — the window keeps the
    /// chrome all six kinds share and this owns everything Loot-specific.</summary>
    private LootBreakoutView? _lootView;

    /// <summary>Repaint gate, shared with the lifted Loot view: an unchanged row set must
    /// not rebuild, and a filter click must be able to force the next paint through.</summary>
    internal string Signature { get => _signature; set => _signature = value; }

    /// <summary>For Loot this is TARGET scope, not Fight — same toggle chrome, different
    /// axis (David, 2026-08-06).</summary>
    internal bool TargetScope => _fightScope;

    /// <summary>The tick this window last painted from, so a filter click repaints now
    /// rather than up to a second later.</summary>
    internal StatsSnapshot? LastSnapshot => _lastSnapshot;

    public BreakoutWindow(AppSettings settings, BreakoutKind kind)
    {
        InitializeComponent();
        _settings = settings;
        _kind = kind;
        Title = $"EQBuddy {kind} breakout";
        _fightScope = ScopeSetting() != "session";
        DismissIcon.ToolTip = BreakoutPresentation.DismissTip;

        Chrome.SetResourceReference(Border.BackgroundProperty, "BgBrush");
        // Hairline chrome (2026-08-11 modernization): the accent at a whisper, same
        // treatment as the main widget's cards.
        Chrome.SetResourceReference(Border.BorderBrushProperty, "HairlineBrush");
        ScopeBorder.SetResourceReference(Border.BorderBrushProperty, "HairlineBrush");
        TitleText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        SubText.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        EmptyText.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");

        // Sort links only make sense for ability-stat rows.
        SortBar.Visibility = _kind is BreakoutKind.Watch or BreakoutKind.Loot
            or BreakoutKind.Buffs
            ? Visibility.Collapsed : Visibility.Visible;
        if (_kind == BreakoutKind.Healing) SortRate.Text = "hps";
        _sort = ParseSort(SortSetting());
        ApplySortVisual();

        var (left, top) = PositionSetting();
        if (ScreenGuard.OnScreen(left, top, Width, 120)) { Left = left; Top = top; }
        else
        {
            // Default position staggered per kind so fresh windows never open on top
            // of each other: a column down the work area's right edge, six slots tall
            // (slot seven of a 1080p work area is off the bottom), then a second
            // column to its left — a plain modulo wrap would land kind 6 exactly on
            // kind 0's spot, hiding one window behind the other. Clamped to the work
            // area's left edge: on a narrow (portrait) monitor the second column's
            // arithmetic would otherwise walk the window clean off the screen.
            var area = SystemParameters.WorkArea;
            Left = Math.Max(area.Left + 10, area.Right - Width - 40 - (Width + 30) * ((int)kind / 6));
            Top = area.Top + 80 + 150 * ((int)kind % 6);
        }

        // Auto-size mode still caps the list so a 40-item session can't run the window
        // off the screen; a manual size (grip-dragged, persisted) takes over entirely.
        RowsScroll.MaxHeight = SystemParameters.WorkArea.Height * 0.6;
        var (savedW, savedH) = SizeSetting();
        if (savedW is >= MinManualWidth and <= 900 && savedH is >= MinManualHeight)
        {
            SizeToContent = SizeToContent.Manual;
            Width = savedW;
            Height = Math.Min(savedH, SystemParameters.WorkArea.Height);
            RowsScroll.MaxHeight = double.PositiveInfinity;
        }

        Closed += (_, _) => SavePosition();
        WindowZoom.Attach(this, $"breakout:{kind}", settings);
        if (_kind == BreakoutKind.Damage)
        {
            CopyFight.Visibility = Visibility.Visible;
            OpenTimeline.Visibility = Visibility.Visible;
        }
        if (_kind == BreakoutKind.Watch)
            ScopeBorder.Visibility = Visibility.Collapsed;
        if (_kind == BreakoutKind.Buffs)
        {
            // No Fight/Session axis here — the axis is the class combination, named
            // in the subheader. The in-place editor is this kind's whole point
            // (#120 stage 2): configuring the set never requires Options.
            ScopeBorder.Visibility = Visibility.Collapsed;
            BuffEditor.Visibility = Visibility.Visible;
        }
        if (_kind == BreakoutKind.Loot)
        {
            _lootView = new LootBreakoutView(this, settings);
            LootStrips.Content = _lootView.Strips;   // shown by the view's own render
        }
        // The scope toggle, on the same compact segmented strip as the Loot filters —
        // its hand-rolled predecessor washed the selected TextBlock square, which poked
        // outside ScopeBorder's own rounded corners (LW, 2026-08-18). Loot rides the
        // same chrome on a different axis: TARGET drops vs the SESSION's yield
        // (David, 2026-08-06).
        _scope = new EqSegmentedStrip(ScopeHost, compact: true);
        var loot = _kind == BreakoutKind.Loot;
        _scope.Add(loot ? "Target" : "Fight", true,
            tip: loot ? "What the creature you're fighting — or last /considered — can drop"
                : "The current (or last) pull's numbers",
            onClick: () => SetScope(fight: true));
        _scope.Add("Session", false,
            tip: loot ? "Everything this session has yielded" : "The whole session's numbers",
            onClick: () => SetScope(fight: false));
        ApplyScopeVisual();
    }

    private EqSegmentedStrip? _scope;

    private void SetScope(bool fight)
    {
        _fightScope = fight;
        SetScopeSetting(fight ? "fight" : "session");
        ApplyScopeVisual();
        // Repaint now, not on the next tick — the old toggle waited up to a second,
        // which is long enough to read as a click that did nothing.
        if (_lastSnapshot is { } s) Update(s);
    }

    private string ScopeSetting() => _kind switch
    {
        BreakoutKind.Damage => _settings.BreakoutDamageScope,
        BreakoutKind.Healing => _settings.BreakoutHealingScope,
        BreakoutKind.Loot => _settings.BreakoutLootScope == "session" ? "session" : "fight",
        _ => _settings.BreakoutPetScope,
    };

    private void SetScopeSetting(string v)
    {
        switch (_kind)
        {
            case BreakoutKind.Damage: _settings.BreakoutDamageScope = v; break;
            case BreakoutKind.Healing: _settings.BreakoutHealingScope = v; break;
            case BreakoutKind.Pet: _settings.BreakoutPetScope = v; break;
            case BreakoutKind.Loot:
                _settings.BreakoutLootScope = v == "session" ? "session" : "target"; break;
        }
        _settings.Save();
    }

    private (double Left, double Top) PositionSetting() => _kind switch
    {
        BreakoutKind.Damage => (_settings.BreakoutDamageLeft, _settings.BreakoutDamageTop),
        BreakoutKind.Healing => (_settings.BreakoutHealingLeft, _settings.BreakoutHealingTop),
        BreakoutKind.Pet => (_settings.BreakoutPetLeft, _settings.BreakoutPetTop),
        BreakoutKind.Watch => (_settings.BreakoutWatchLeft, _settings.BreakoutWatchTop),
        BreakoutKind.Buffs => (_settings.BreakoutBuffsLeft, _settings.BreakoutBuffsTop),
        _ => (_settings.BreakoutLootLeft, _settings.BreakoutLootTop),
    };

    private StatSort _sort = StatSort.Total;

    private static StatSort ParseSort(string v) => v switch
    {
        "hits" => StatSort.Hits, "avg" => StatSort.Avg, "rate" => StatSort.Rate,
        _ => StatSort.Total,
    };

    private string SortSetting() => _kind switch
    {
        BreakoutKind.Healing => _settings.BreakoutHealingSort,
        BreakoutKind.Pet => _settings.BreakoutPetSort,
        _ => _settings.BreakoutDamageSort,
    };

    private void OnSortClick(object sender, MouseButtonEventArgs e)
    {
        var key = (string)((FrameworkElement)sender).Tag;
        _sort = ParseSort(key);
        switch (_kind)
        {
            case BreakoutKind.Healing: _settings.BreakoutHealingSort = key; break;
            case BreakoutKind.Pet: _settings.BreakoutPetSort = key; break;
            default: _settings.BreakoutDamageSort = key; break;
        }
        _settings.Save();
        ApplySortVisual();
        e.Handled = true;
    }

    private void ApplySortVisual()
    {
        foreach (var (tb, key) in new[]
            { (SortTotal, "total"), (SortHits, "hits"), (SortAvg, "avg"), (SortRate, "rate") })
            tb.SetResourceReference(TextBlock.ForegroundProperty,
                ParseSort(key) == _sort ? "AccentBrush" : "DimBrush");
        _signature = "";   // force a repaint in the new order on the next tick
    }

    private const double MinManualWidth = 200;
    private const double MinManualHeight = 120;

    private (double W, double H) SizeSetting() => _kind switch
    {
        BreakoutKind.Damage => (_settings.BreakoutDamageWidth, _settings.BreakoutDamageHeight),
        BreakoutKind.Healing => (_settings.BreakoutHealingWidth, _settings.BreakoutHealingHeight),
        BreakoutKind.Pet => (_settings.BreakoutPetWidth, _settings.BreakoutPetHeight),
        BreakoutKind.Watch => (_settings.BreakoutWatchWidth, _settings.BreakoutWatchHeight),
        BreakoutKind.Buffs => (_settings.BreakoutBuffsWidth, _settings.BreakoutBuffsHeight),
        _ => (_settings.BreakoutLootWidth, _settings.BreakoutLootHeight),
    };

    private void SetSizeSetting(double w, double h)
    {
        switch (_kind)
        {
            case BreakoutKind.Damage:
                _settings.BreakoutDamageWidth = w; _settings.BreakoutDamageHeight = h; break;
            case BreakoutKind.Healing:
                _settings.BreakoutHealingWidth = w; _settings.BreakoutHealingHeight = h; break;
            case BreakoutKind.Pet:
                _settings.BreakoutPetWidth = w; _settings.BreakoutPetHeight = h; break;
            case BreakoutKind.Watch:
                _settings.BreakoutWatchWidth = w; _settings.BreakoutWatchHeight = h; break;
            case BreakoutKind.Buffs:
                _settings.BreakoutBuffsWidth = w; _settings.BreakoutBuffsHeight = h; break;
            default:
                _settings.BreakoutLootWidth = w; _settings.BreakoutLootHeight = h; break;
        }
    }

    /// <summary>First resize gesture of any kind: freeze the current auto size and take
    /// manual control, so the resize isn't immediately undone by SizeToContent.</summary>
    private void EnterManualSize()
    {
        if (SizeToContent == SizeToContent.Manual) return;
        var w = ActualWidth; var h = ActualHeight;
        SizeToContent = SizeToContent.Manual;
        Width = w; Height = h;
        RowsScroll.MaxHeight = double.PositiveInfinity;
    }

    private void OnGripDrag(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        EnterManualSize();
        Width = Math.Clamp(Width + e.HorizontalChange, MinManualWidth, 900);
        Height = Math.Clamp(Height + e.VerticalChange, MinManualHeight,
            SystemParameters.WorkArea.Height);
    }

    // ---- native edge-resize (discussion feedback via David, 2026-08-06: "I still can't
    // resize the loot window" — a frameless window has no resize borders, and a corner
    // glyph nobody finds isn't an affordance). WM_NCHITTEST maps the right/bottom edges
    // to resize zones so the window behaves like windows do; the grip stays as the
    // visible hint. ----

    private const int WmNcHitTest = 0x84;
    private const int WmNcLButtonDown = 0xA1;
    private const int WmExitSizeMove = 0x232;
    private const int HtLeft = 10, HtRight = 11, HtTop = 12, HtTopLeft = 13,
        HtTopRight = 14, HtBottom = 15, HtBottomLeft = 16, HtBottomRight = 17;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is System.Windows.Interop.HwndSource src)
            src.AddHook(ResizeHook);
    }

    private IntPtr ResizeHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WmNcHitTest:
            {
                // lParam: screen coords, low word X, high word Y (signed for multi-monitor).
                var x = (short)((long)lParam & 0xFFFF);
                var y = (short)(((long)lParam >> 16) & 0xFFFF);
                var p = PointFromScreen(new Point(x, y));
                // Any side, any corner (David: resize like a normal window). Zone math is
                // pure and unit-tested in EQBuddy.UI.Shared.ResizeZones.
                var hit = EQBuddy.UI.Shared.ResizeZones.Hit(p.X, p.Y, ActualWidth, ActualHeight,
                    EQBuddy.UI.Shared.BreakdownRowLayout.ResizeEdge,
                    EQBuddy.UI.Shared.BreakdownRowLayout.ResizeCorner);
                if (hit != 0) { handled = true; return hit; }
                break;
            }
            case WmNcLButtonDown when (long)wParam is >= HtLeft and <= HtBottomRight:
                // The native size loop is about to start — leave SizeToContent first or
                // the height snaps back the moment layout runs.
                EnterManualSize();
                break;
            case WmExitSizeMove when SizeToContent == SizeToContent.Manual:
                // ActualWidth/Height, not Width/Height: the native size loop moves the
                // window without writing the dependency properties. SavePosition too — a
                // top/left resize moves the window's origin.
                Width = ActualWidth;
                Height = ActualHeight;
                SetSizeSetting(ActualWidth, ActualHeight);
                SavePosition();
                break;
        }
        return IntPtr.Zero;
    }

    private void OnGripDone(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        SetSizeSetting(Width, Height);
        _settings.Save();
    }

    private void OnGripReset(object sender, MouseButtonEventArgs e)
    {
        // Back to auto-size: forget the manual size and let content drive height again.
        SetSizeSetting(double.NaN, double.NaN);
        _settings.Save();
        Width = 272;
        ClearValue(HeightProperty);
        RowsScroll.MaxHeight = SystemParameters.WorkArea.Height * 0.6;
        SizeToContent = SizeToContent.Height;
        e.Handled = true;
    }

    /// <summary>Persist the spot on hide as well as close — the window is hidden and
    /// re-shown across minimize cycles, and only the last Closed would otherwise count.</summary>
    public void SavePosition()
    {
        switch (_kind)
        {
            case BreakoutKind.Damage:
                _settings.BreakoutDamageLeft = Left; _settings.BreakoutDamageTop = Top; break;
            case BreakoutKind.Healing:
                _settings.BreakoutHealingLeft = Left; _settings.BreakoutHealingTop = Top; break;
            case BreakoutKind.Pet:
                _settings.BreakoutPetLeft = Left; _settings.BreakoutPetTop = Top; break;
            case BreakoutKind.Watch:
                _settings.BreakoutWatchLeft = Left; _settings.BreakoutWatchTop = Top; break;
            case BreakoutKind.Buffs:
                _settings.BreakoutBuffsLeft = Left; _settings.BreakoutBuffsTop = Top; break;
            default:
                _settings.BreakoutLootLeft = Left; _settings.BreakoutLootTop = Top; break;
        }
        _settings.Save();
    }

    /// <summary>Refresh from the 1 s snapshot tick. Rebuilds rows only when the numbers
    /// actually changed (same signature idiom as the chip windows).</summary>
    // ---- background see-through (#96, badly-developed): breakouts follow the main
    // widget's setting — only the panel fades, text stays sharp, same rule as the
    // widget. Re-checked on the shared tick so Options changes and theme switches
    // reach an already-open breakout without a rebuild.
    private (double Opacity, Color Tint) _appliedBg = (-1, default);

    private void ApplyBackgroundOpacity()
    {
        var opacity = _settings.BackgroundOpacity;
        var tint = ((SolidColorBrush)FindResource("BgBrush")).Color;
        if (_appliedBg == (opacity, tint)) return;
        _appliedBg = (opacity, tint);
        Chrome.Background = new SolidColorBrush(
            Color.FromArgb((byte)(opacity * 255), tint.R, tint.G, tint.B));
    }

    public void Update(StatsSnapshot s)
    {
        _lastSnapshot = s;   // kept so a sort click can repaint now, not on the next tick
        ApplyBackgroundOpacity();
        if (_kind == BreakoutKind.Watch) { UpdateWatch(s); return; }
        if (_kind == BreakoutKind.Loot) { UpdateLoot(s); return; }
        if (_kind == BreakoutKind.Buffs) { UpdateBuffs(s); return; }
        _lastFight = s.LastFight;
        _deaths = s.Deaths;
        _resists = MainWindow.SpellResistLookup(s);
        _blockedBy = Main?.BlockedByLookup(s);

        // **WHAT this window shows is decided in UI.Shared since E-3 PR 5**, because the
        // Evolved shell's Live room shows the same three meters and will for as long as HUD
        // subtraction stays gated per item. Two hosts asking "which rows does Fight scope
        // mean" is trap 33's shape — not a stale answer and a fresh one, but two answers,
        // each current, that a later change has to be taught twice. The decision moved; the
        // drawing stayed here, which is the split every window sum in this repo takes.
        var kind = MeterKind();
        var meter = LivePresentation.Meter(kind, s, _fightScope, DateTime.Now);
        TitleText.Text = meter.Title;
        TitleIcon.Glyph = BreakoutPresentation.Icon(kind);
        SubText.Text = meter.Subtext;

        var empty = meter.Empty is not null;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
        {
            EmptyText.Text = meter.Empty!;
            Rows.Items.Clear();
            _signature = "";
            return;
        }

        // Signature: rebuilding ten bar rows every second is cheap but pointless between
        // fights — only re-render when a number moved or the scope/fight/sort changed.
        var sig = LivePresentation.MeterSignature(kind, _fightScope, _sort.ToString(), meter);
        if (sig == _signature) return;
        _signature = sig;
        // Resist % rides only the session-scope damage rows — the tallies are
        // session-wide, and stamping them on a single fight would misstate it.
        var resists = _kind == BreakoutKind.Damage && !_fightScope ? _resists : null;
        BreakdownRows.FillAbilityRowsSorted(this, Rows, meter.Rows, _sort,
            Math.Max(1, meter.Seconds), meter.RateLabel,
            max: 10, resists: resists, blockedBy: resists is null ? null : _blockedBy);
    }

    /// <summary>This window's kind as the shared decision spells it. <c>BreakoutKind</c> is
    /// a WPF enum and <see cref="BreakoutPresentation"/> is keyed by string for exactly this
    /// reason — see that file's own note on why it does not take a side.</summary>
    private string MeterKind() => _kind switch
    {
        BreakoutKind.Healing => BreakoutPresentation.Healing,
        BreakoutKind.Pet => BreakoutPresentation.Pet,
        _ => BreakoutPresentation.Damage,
    };

    private LastFightInfo? _lastFight;
    private IReadOnlyList<TimedDetail> _deaths = [];
    private IReadOnlyDictionary<string, (int Casts, int Resists, int Blocked)>? _resists;
    private IReadOnlyDictionary<string, string>? _blockedBy;

    /// <summary>#102 (jeremycranfill): the Combat card's fight export without leaving
    /// the minimized view — same Discord-ready text, same clipboard.</summary>
    private void OnOpenTimeline(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;   // not a window drag
        Main?.OpenFightTimeline();
    }

    private void OnCopyFight(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_lastFight is not { } f) return;
        try
        {
            Clipboard.SetText(EQBuddy.UI.Shared.FightExport.ToText(
                f, Main?.Identity.Character ?? "", $"v{UpdateChecker.CurrentVersion}",
                EQBuddy.UI.Shared.FightExport.DeathsDuring(f.Start, f.DurationSeconds, _deaths)));
            // The tick IS the confirmation — the clipboard gives no other feedback.
            CopyFight.Glyph = "Check";
            var t = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (_, _) => { CopyFight.Glyph = "Copy"; t.Stop(); };
            t.Start();
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>The Watch breakout: every 📌-pinned rule as a bar row — count, last match,
    /// per-hour rate. "Search an item and add it to the window" is exactly what adding and
    /// pinning a watch rule already does, so the window rides that instead of inventing a
    /// second tracking system (CrispyPigeon131's mote window, discussion #44).</summary>
    private void UpdateWatch(StatsSnapshot s)
    {
        TitleText.Text = BreakoutPresentation.Title(BreakoutPresentation.Watch);
        TitleIcon.Glyph = BreakoutPresentation.Icon(BreakoutPresentation.Watch);
        var pinnedIds = _settings.TrackedRules
            .Where(r => r.Enabled && r.Pinned).Select(r => r.Id)
            .ToHashSet(StringComparer.Ordinal);
        var rows = s.Tracked.Where(t => pinnedIds.Contains(t.Id)).ToList();

        var total = rows.Sum(r => r.TotalQuantity);
        SubText.Text = $"Session · {rows.Count} pinned rule{(rows.Count == 1 ? "" : "s")} · {total} total";

        var empty = rows.Count == 0;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
        {
            EmptyText.Text = "Pin a watch rule in Options to track it here.";
            Rows.Items.Clear();
            _signature = "";
            return;
        }

        var sig = "watch|" + string.Join(",", rows.Select(r => $"{r.Id}:{r.TotalQuantity}:{r.LastItem}"));
        if (sig == _signature) return;
        _signature = sig;

        Rows.Items.Clear();
        var top = Math.Max(1, rows.Max(r => r.TotalQuantity));
        var barBrush = BreakdownRows.BarBrush(this);
        foreach (var r in rows.OrderByDescending(x => x.TotalQuantity))
        {
            var value = $"{r.TotalQuantity} · {r.PerHour:0.#}/hr";
            var tooltip = r.LastItem is { Length: > 0 } li ? $"last: {li}" : null;
            Rows.Items.Add(BreakdownRows.Row(this, r.Name, value,
                (double)r.TotalQuantity / top, barBrush, tooltip));
        }
    }

    /// <summary>The Loot kind. The title is chrome — every kind writes one here — and
    /// everything below it belongs to <see cref="LootBreakoutView"/>.</summary>
    private void UpdateLoot(StatsSnapshot s)
    {
        TitleText.Text = BreakoutPresentation.Title(BreakoutPresentation.Loot);
        TitleIcon.Glyph = BreakoutPresentation.Icon(BreakoutPresentation.Loot);
        _lootView?.Render(s);
    }



    // ---- the Buff Set breakout (#120 stage 2, Frankthetankk) ----

    /// <summary>Per-tick in-place clocks for the buff rows (the Buffs card's idiom):
    /// an unchanged set of spells+statuses updates countdown text without rebuilding.</summary>
    private readonly List<TextBlock> _buffSetClocks = [];
    private string _buffBucketsMemo = "";

    /// <summary>
    /// The requester's window, "in line with the other breakout windows": the ASSEMBLED
    /// buff set, one section per class in the active combination with the "(any class)"
    /// bucket first, each entry wearing the card's live honesty brush (Active/Expiring/
    /// Missing/NotSeen), and add/remove in place so configuration never requires
    /// Options. Both editors write the same per-class storage; every edit routes
    /// through MainWindow.OnBuffSetEdited so card, Options and this window repaint at
    /// once — a change that waits for the next tick reads as a silent no-op.
    /// </summary>
    private void UpdateBuffs(StatsSnapshot s)
    {
        TitleText.Text = BreakoutPresentation.Title(BreakoutPresentation.Buffs);
        TitleIcon.Glyph = BreakoutPresentation.Icon(BreakoutPresentation.Buffs);
        if (Main is not { } main || main.BuffSetKey is not { Length: > 0 } key)
        {
            SubText.Text = "No character detected yet";
            EmptyText.Text = "Once today's log names your character,\nthe set unlocks here.";
            EmptyText.Visibility = Visibility.Visible;
            Rows.Items.Clear();
            _buffSetClocks.Clear();
            BuffAddBox.IsEnabled = false;
            BuffClassBox.IsEnabled = false;
            _signature = "";
            return;
        }
        BuffAddBox.IsEnabled = true;
        BuffClassBox.IsEnabled = true;

        var now = DateTime.Now;
        // The RESOLVED source, not a hardcoded "inferred" — Bevel, Helm-signed
        // 2026-08-23: passing ClassSource.Inferred always meant "a dump-sourced trio still
        // reads as a guess", which is the exact fact-vs-guess distinction these words exist
        // to carry.
        var (classes, classSource) = main.ClassSourceFor(s);
        // The class filter, visible: the combination this set is assembled for, its
        // source named honestly — the log has no /who or loadout-change line to read.
        SubText.Text = main.BuffSetCharacterName + " · " + (classes.Count > 0
            // Where the classes came from, in the shared words — "(inferred)" said only
            // one of the three things this can now be, and said nothing at all when the
            // GAME had told us via an achievements dump.
            ? string.Join("/", classes.Select(QuestClassFilter.Abbrev))
                + $" ({CharacterClasses.SourceLabel(classSource)})"
            : "no classes known yet");
        SubText.ToolTip = "Classes come from your Quest Tracker picks, falling back to the class "
            + "inferred from your combat log — EQ Legends logs announce no loadout changes, so "
            + "this is the signal EQBuddy honestly has. (any class) picks always apply. "
            + "Swap a class and the other classes' picks stay put.";
        RefreshBuffClassChoices(classes);

        var sections = main.BuffSetSectionStates(s, now);
        // Stage 3 (#120): the card's suggestion rows mirror here, and the lost-buff
        // history folds at the bottom — both live content, both in the signature.
        var suggestions = main.BuffSuggestionsFor(s, main.AssembledBuffSet(classes));
        var losses = main.BuffLosses.Snapshot();
        if (sections.All(sec => sec.Entries.Count == 0) && suggestions.Count == 0 && losses.Count == 0)
        {
            EmptyText.Text = "Nothing picked yet — choose a class bucket below\nand type a buff to build the set.";
            EmptyText.Visibility = Visibility.Visible;
            Rows.Items.Clear();
            _buffSetClocks.Clear();
            _signature = "";
            return;
        }
        EmptyText.Visibility = Visibility.Collapsed;

        // Signature covers spells and STATUSES, not countdown text — clocks update in
        // place on a match, so a ticking timer never forces a rebuild. Losses key on
        // the newest entry too: at the cap the count alone stops moving.
        var flat = sections.SelectMany(sec => sec.Entries).ToList();
        var sig = "buffs|" + SubText.Text + "|" + string.Join(";", sections.Select(sec =>
                sec.Class + ":" + string.Join(",", sec.Entries.Select(e => $"{e.Spell}·{e.Status}"))))
            + "|sug:" + string.Join(",", suggestions.Select(x => x.Spell + "@" + x.Class))
            + "|loss:" + losses.Count + (_lossesOpen ? "-open" : "-shut")
            + (losses.Count > 0 ? losses[0].Time.Ticks + losses[0].Spell : "");
        if (sig == _signature)
        {
            for (var i = 0; i < _buffSetClocks.Count && i < flat.Count; i++)
                _buffSetClocks[i].Text = BuffStatusText(flat[i]);
            return;
        }
        _signature = sig;
        _buffSetClocks.Clear();

        Rows.Items.Clear();
        foreach (var (cls, entries) in sections)
        {
            var header = new TextBlock { Text = cls };
            header.Style = (Style)FindResource("SectionLabel");
            Rows.Items.Add(header);
            if (entries.Count == 0)
            {
                // The empty section is deliberate furniture: it shows the bucket a
                // freshly swapped-in class gets, right where adding happens.
                var none = new TextBlock
                {
                    Text = "nothing picked for this class yet", FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Caption).Size,
                    FontStyle = FontStyles.Italic, Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, DesignTokens.SpaceXxs),
                };
                none.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
                Rows.Items.Add(none);
                continue;
            }
            foreach (var entry in entries) Rows.Items.Add(BuffSetRow(main, key, cls, entry));
        }
        foreach (var sug in suggestions) Rows.Items.Add(BuffSuggestionRow(main, sug));
        AddBuffLossFold(main, losses);
    }

    /// <summary>The card's suggestion row, mirrored (#120 stage 3): dim, ✓ add to the
    /// gaining class's bucket / ✕ dismiss for good — never auto-added.</summary>
    private Grid BuffSuggestionRow(MainWindow main, BuffSuggestion sug)
    {
        var row = new Grid { Margin = new Thickness(DesignTokens.SpaceXs, DesignTokens.StateRuleWidth, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new TextBlock
        {
            Text = $"new at your level — add {sug.Spell} to {sug.Class}?",
            FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Caption).Size, FontStyle = FontStyles.Italic,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Your level-up made this buff available. The tick adds it to that "
                + "class's bucket; the cross never asks again for this character. A new "
                + "RANK of a set buff folds into the same slot and is never suggested.",
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        row.Children.Add(text);
        var add = new Button
        {
            Style = (Style)FindResource("IconButton"),
            Content = DesignSystem.Icon("Check", "GoodBrush", size: DesignTokens.IconInline),
            Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0), ToolTip = $"Add {sug.Spell} to your {sug.Class} set",
        };
        add.Click += (_, _) => main.AcceptBuffSuggestion(sug);
        Grid.SetColumn(add, 1);
        row.Children.Add(add);
        var dismiss = new Button
        {
            Style = (Style)FindResource("IconButton"),
            Content = DesignSystem.Icon("Close", size: DesignTokens.IconInline),
            Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0),
            ToolTip = "Dismiss — never suggest this buff for this character again",
        };
        dismiss.Click += (_, _) => main.DismissBuffSuggestion(sug);
        Grid.SetColumn(dismiss, 2);
        row.Children.Add(dismiss);
        return row;
    }

    private bool _lossesOpen;

    /// <summary>The lost-buff history fold (#120 stage 3, Frankthetankk): "▸ lost this
    /// session (N)" at the bottom of the breakout — time · buff · cause per row, the
    /// AA-list fold idiom. ⧉ on the header copies the list as plain text: the
    /// requester's dev-report evidence ("Buff X was active, NPC Y cast debuff Z,
    /// Buff X was gone"), same content-copy style as the fight export.</summary>
    private void AddBuffLossFold(MainWindow main, List<BuffLossEntry> losses)
    {
        if (losses.Count == 0) return;
        var head = new Grid { Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        // The chevron was "▾"/"▸" typed into the label. It is a vector now, and it stays
        // a chevron: dropping it would take away the only thing that says whether the
        // fold is open, which is the affordance rather than decoration.
        var chevron = new EqIcon
        {
            Glyph = _lossesOpen ? "ChevronDown" : "ChevronRight",
            Size = DesignTokens.IconInline,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0),
        };
        head.Children.Add(chevron);
        var label = new TextBlock
        {
            Text = $"lost this session ({losses.Count})",
            FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Caption).Size, Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Every set buff that went missing this session, newest first, with "
                + "the best cause the log names: expired (the countdown ran out; est = "
                + "the duration was still the wiki-base estimate), faded (the wear-off "
                + "line), \"lost as X landed\" (a hostile spell landed on you within "
                + "2 s before the fade), lost on death.",
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        label.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _lossesOpen = !_lossesOpen;
            if (Main is { } m) RefreshBuffSet(m.CurrentSnapshot());   // repaint now, not next tick
        };
        chevron.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            _lossesOpen = !_lossesOpen;
            if (Main is { } m) RefreshBuffSet(m.CurrentSnapshot());
        };
        Grid.SetColumn(label, 1);
        head.Children.Add(label);
        var copy = new EqIcon
        {
            Glyph = "Copy", Size = DesignTokens.IconInline,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0),
            ToolTip = "Copy the list as plain text — evidence for a bug report to the game devs.",
        };
        copy.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            try
            {
                Clipboard.SetText(main.BuffLosses.ExportText(main.BuffSetCharacterName));
                copy.Glyph = "Check";
                var t = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromSeconds(1.5) };
                t.Tick += (_, _) => { copy.Glyph = "Copy"; t.Stop(); };
                t.Start();
            }
            catch (Exception ex) { App.LogError(ex); }
        };
        Grid.SetColumn(copy, 2);
        head.Children.Add(copy);
        Rows.Items.Add(head);
        if (!_lossesOpen) return;
        foreach (var loss in losses)
        {
            var row = new TextBlock
            {
                Text = $"{loss.Time:h:mm:ss tt}  {loss.Spell} — {loss.Cause}",
                FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Metadata).Size, Margin = new Thickness(DesignTokens.SpaceM, 0, 0, 1),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = $"{loss.Spell} — {loss.Cause} at {loss.Time:h:mm:ss tt}",
            };
            row.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
            Rows.Items.Add(row);
        }
    }

    /// <summary>One set entry: name and clock in the stage-1 state brushes (Active =
    /// good, Expiring = accent, Missing = warn, NotSeen = dim italic), ✕ = remove
    /// from THIS class bucket only.</summary>
    private Grid BuffSetRow(MainWindow main, string key, string cls, BuffSetEntryState entry)
    {
        var row = new Grid { Margin = new Thickness(DesignTokens.SpaceXs, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var (brush, italic) = entry.Status switch
        {
            BuffSetStatus.Active => ("GoodBrush", false),
            BuffSetStatus.Expiring => ("AccentBrush", false),
            BuffSetStatus.Missing => ("WarnBrush", false),
            _ => ("DimBrush", true),
        };
        var name = new TextBlock
        {
            Text = entry.Spell, FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Body).Size, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (italic) name.FontStyle = FontStyles.Italic;
        name.SetResourceReference(TextBlock.ForegroundProperty, brush);
        row.Children.Add(name);
        var clock = new TextBlock
        {
            Text = BuffStatusText(entry), FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Caption).Size, Margin = new Thickness(DesignTokens.SpaceS, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = entry.Status switch
            {
                BuffSetStatus.Missing => "Seen fading this session, or its timer ran out — rebuff.",
                BuffSetStatus.Expiring => "Still up, but inside the warn window.",
                BuffSetStatus.NotSeen => "No landing line this session — it may be up from before "
                    + "EQBuddy was watching; the log can't tell, so this stays its own honest state.",
                _ => "Up, counting down.",
            },
        };
        clock.SetResourceReference(TextBlock.ForegroundProperty, brush);
        Grid.SetColumn(clock, 1);
        row.Children.Add(clock);
        _buffSetClocks.Add(clock);
        var remove = new Button
        {
            Style = (Style)FindResource("IconButton"),
            Content = DesignSystem.Icon("Close", size: DesignTokens.IconInline),
            Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0), ToolTip = $"Remove {entry.Spell} from {cls}",
        };
        remove.Click += (_, _) =>
        {
            BuffSetStore.Remove(_settings.BuffSetsByClass, key, cls, entry.Spell);
            _settings.Save();
            main.OnBuffSetEdited();
        };
        Grid.SetColumn(remove, 2);
        row.Children.Add(remove);
        return row;
    }

    private static string BuffStatusText(BuffSetEntryState entry) => entry.Status switch
    {
        BuffSetStatus.Missing => "missing",
        BuffSetStatus.NotSeen => "not seen",
        _ => entry.RemainingSeconds is { } r ? $"{(int)r / 60}:{(int)r % 60:00}" : "up",
    };

    /// <summary>The add-target buckets: "(any class)", then the active combination, then
    /// every remaining class.
    ///
    /// It used to stop after the active combination, on the reasoning that parked classes
    /// were the Options editor's business. That made a GUESS load-bearing: class detection
    /// is best-effort (Quest Tracker picks, else combat inference), so a character read as
    /// Berserker alone could only ever build a Berserker set — which is precisely the
    /// per-class assembly the feature exists to offer (#120, Frankthetankk, who was blocked
    /// from testing it). Your own classes stay at the top where they are one click away;
    /// the rest are simply reachable now instead of hidden behind a correct inference.</summary>
    private void RefreshBuffClassChoices(IReadOnlyList<string> classes)
    {
        var memo = string.Join("|", classes);
        if (memo == _buffBucketsMemo && BuffClassBox.Items.Count > 0) return;
        _buffBucketsMemo = memo;
        var keep = BuffClassBox.SelectedItem as string;
        BuffClassBox.Items.Clear();
        BuffClassBox.Items.Add(BuffSetStore.AnyClass);
        foreach (var cls in classes) BuffClassBox.Items.Add(cls);
        foreach (var cls in EQBuddy.Core.QuestClassFilter.Classes)
            if (!classes.Contains(cls, StringComparer.OrdinalIgnoreCase))
                BuffClassBox.Items.Add(cls);
        BuffClassBox.SelectedItem = keep is not null && BuffClassBox.Items.Contains(keep)
            ? keep : BuffSetStore.AnyClass;
    }

    private string SelectedBuffBucket => BuffClassBox.SelectedItem as string ?? BuffSetStore.AnyClass;

    private void OnBuffSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (Main is not { } main || main.BuffSetKey is not { Length: > 0 } key) return;
        var query = BuffAddBox.Text.Trim();
        if (query.Length < 2) { BuffPopup.IsOpen = false; return; }
        BuffPopupChrome.SetResourceReference(Border.BackgroundProperty, "PopupBrush");
        BuffPopupChrome.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
        BuffMatches.SetResourceReference(Control.BackgroundProperty, "PopupBrush");
        BuffMatches.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        BuffMatches.Items.Clear();
        var inBucket = BuffSetStore.SpellsFor(
            _settings.BuffSetsByClass.GetValueOrDefault(key), SelectedBuffBucket);
        foreach (var (spell, seen) in BuffSetSearch.Rank(query, main.SeenBuffCasts(),
                     inBucket, BuffDurationCatalog.Default.SpellNames))
            BuffMatches.Items.Add(new ListBoxItem
                { Content = seen ? spell + "   · seen this session" : spell, Tag = spell });
        if (BuffMatches.Items.Count == 0)
            BuffMatches.Items.Add(new ListBoxItem
                { Content = "No buff in the catalog matches — check the spelling?", IsEnabled = false });
        BuffPopup.IsOpen = true;
    }

    private void OnBuffMatchPicked(object sender, SelectionChangedEventArgs e)
    {
        if (BuffMatches.SelectedItem is not ListBoxItem { Tag: string spell }) return;
        BuffPopup.IsOpen = false;
        BuffMatches.SelectedItem = null;
        if (Main is not { } main || main.BuffSetKey is not { Length: > 0 } key) return;
        BuffSetStore.Add(_settings.BuffSetsByClass, key, SelectedBuffBucket, spell);
        _settings.Save();
        BuffAddBox.Text = "";   // TextChanged with an empty box closes the popup
        main.OnBuffSetEdited();
    }

    /// <summary>An edit arrived from the other editor — repaint now, not next tick.</summary>
    public void RefreshBuffSet(StatsSnapshot s)
    {
        if (_kind != BreakoutKind.Buffs) return;
        _signature = "";
        UpdateBuffs(s);
    }

    private void ApplyScopeVisual()
    {
        _scope?.Select(_fightScope);
        _signature = "";   // force a repaint in the new scope on the next tick
    }

    private void OnDismiss(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        SavePosition();
        Hide();
        Dismissed?.Invoke(_kind);
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
