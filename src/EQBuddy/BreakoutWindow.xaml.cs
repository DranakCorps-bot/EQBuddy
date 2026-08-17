using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.Core;

namespace EQBuddy;

/// <summary>Which stat a breakout window tracks. Each kind is one singleton window with its
/// own remembered position and Fight/Session scope (Watch, Loot and Buffs have no scope —
/// their content is session/target/class shaped, so the toggle is hidden).</summary>
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
    /// kind persistently (re-enabled under Options → Breakout windows, discussion #45).</summary>
    public event Action<BreakoutKind>? Dismissed;

    private bool _fightScope;
    private string _signature = "";
    private StatsSnapshot? _lastSnapshot;

    public BreakoutWindow(AppSettings settings, BreakoutKind kind)
    {
        InitializeComponent();
        _settings = settings;
        _kind = kind;
        Title = $"EQBuddy {kind} breakout";
        _fightScope = ScopeSetting() != "session";

        Chrome.SetResourceReference(Border.BackgroundProperty, "BgBrush");
        // Hairline chrome (2026-08-11 modernization): the accent at a whisper, same
        // treatment as the main widget's cards.
        Chrome.SetResourceReference(Border.BorderBrushProperty, "HairlineBrush");
        ScopeBorder.SetResourceReference(Border.BorderBrushProperty, "HairlineBrush");
        TitleText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        SubText.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        EmptyText.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");

        // Sort links only make sense for ability-stat rows.
        SortBar.Visibility = _kind is BreakoutKind.Watch or BreakoutKind.Loot or BreakoutKind.Buffs
            ? Visibility.Collapsed : Visibility.Visible;
        if (_kind == BreakoutKind.Healing) SortRate.Text = "hps";
        _sort = ParseSort(SortSetting());
        ApplySortVisual();

        var (left, top) = PositionSetting();
        if (ScreenGuard.OnScreen(left, top, Width, 120)) { Left = left; Top = top; }
        else
        {
            // Default column on the work area's right edge, staggered per kind so three
            // fresh windows never open on top of each other.
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 40;
            Top = area.Top + 80 + 150 * (int)kind;
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
        if (_kind == BreakoutKind.Watch) ScopeBorder.Visibility = Visibility.Collapsed;
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
            // Same toggle chrome, different axis: what the TARGET can drop vs what the
            // SESSION has yielded (David, 2026-08-06).
            ScopeFight.Text = "Target";
            ScopeSession.Text = "Session";
            ApplyLootSortVisual();   // the bars themselves are shown by UpdateLoot
            ApplyLootViewVisual();
        }
        ApplyScopeVisual();
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
                var hit = EQBuddy.UI.Shared.ResizeZones.Hit(p.X, p.Y, ActualWidth, ActualHeight);
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
        var f = s.LastFight;
        var (title, rows, secs, rateLabel) = _kind switch
        {
            BreakoutKind.Damage => ("⚔ Your damage",
                _fightScope ? f?.ByAbility ?? [] : s.DamageBySource,
                _fightScope ? f?.DurationSeconds ?? 0 : s.CombatSeconds, "dps"),
            BreakoutKind.Healing => ("⚕ Your healing",
                _fightScope ? f?.HealsBySpell ?? [] : s.HealsBySpell,
                _fightScope ? f?.DurationSeconds ?? 0 : s.CombatSeconds, "hps"),
            _ => (s.PetName.Length > 0
                    ? $"🐾 Pet damage — {s.PetName}" + EQBuddy.UI.Shared.CharmHoldText.Suffix(s.CharmedSince, DateTime.Now)
                    : "🐾 Pet damage",
                _fightScope ? f?.PetAbilities ?? [] : s.PetAbilities,
                _fightScope ? f?.DurationSeconds ?? 0 : s.CombatSeconds, "dps"),
        };
        TitleText.Text = title;

        var total = rows.Sum(r => r.Total);
        var rate = total / Math.Max(1, secs);
        // Hymn/regen ticks carry no amounts in the log, so they can never join the HPS
        // rows — but a bard mid-song staring at "no healing" reads it as broken (David,
        // live test 2026-08-06). Count them where healing lives; estimate when attributed.
        var regen = _kind == BreakoutKind.Healing && s.RegenTicks > 0
            ? s.RegenEstimatedHealed > 0
                ? $" · est. ~{s.RegenEstimatedHealed:N0} regen ({s.RegenTicks} ticks)"
                : $" · {s.RegenTicks} regen ticks"
            : "";
        SubText.Text = (_fightScope
            ? f is null ? "No fights yet"
                : $"{f.Name} · {f.DurationSeconds:0}s · {f.Outcome} · {rate:0.#} {rateLabel}"
            : $"Session · {s.CombatSeconds / 60:0}m in combat · {rate:0.#} {rateLabel}") + regen;

        var empty = rows.Count == 0;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
        {
            EmptyText.Text = _kind switch
            {
                BreakoutKind.Healing when s.RegenEstimatedHealed > 0 =>
                    $"{s.RegenSpell}: est. ~{s.RegenEstimatedHealed:N0} healed over {s.RegenTicks} ticks.\n" +
                    "The game logs no amounts — this is ticks × your Options\nhp/tick (or the wiki base), so it stays labeled est.",
                BreakoutKind.Healing when s.RegenTicks > 0 =>
                    $"{s.RegenTicks} hymn/regen ticks — the game logs no amounts for these,\nso they count but can't join the HPS rows.",
                BreakoutKind.Healing => "No healing seen yet.",
                BreakoutKind.Pet => "No pet damage seen yet.",
                _ => "No damage seen yet.",
            };
            Rows.Items.Clear();
            _signature = "";
            return;
        }

        // Signature: rebuilding ten bar rows every second is cheap but pointless between
        // fights — only re-render when a number moved or the scope/fight/sort changed.
        var sig = $"{_fightScope}|{_sort}|{f?.Name}|{secs:0}|{string.Join(",", rows.Select(r => $"{r.Name}:{r.Total}"))}";
        if (sig == _signature) return;
        _signature = sig;
        // Resist % rides only the session-scope damage rows — the tallies are
        // session-wide, and stamping them on a single fight would misstate it.
        var resists = _kind == BreakoutKind.Damage && !_fightScope ? _resists : null;
        BreakdownRows.FillAbilityRowsSorted(this, Rows, rows, _sort, Math.Max(1, secs), rateLabel,
            max: 10, resists: resists, blockedBy: resists is null ? null : _blockedBy);
    }

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
            CopyFight.Text = "✓";
            var t = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (_, _) => { CopyFight.Text = "⧉"; t.Stop(); };
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
        TitleText.Text = "🎯 Watch list";
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
            EmptyText.Text = "Pin 📌 a watch rule in Options to track it here.";
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

    /// <summary>The Loot breakout, Target|Session toggled (David's spec, 2026-08-06):
    /// Target = what the creature you're fighting — or last /considered — can drop, your
    /// observed counts and % leading, wiki drops behind, values from your own sales or
    /// the wiki. Session = what you've looted. Hovering a row fetches the eqlwiki item
    /// info on the spot; clicking opens the eqlwiki page in the browser.</summary>
    private void UpdateLoot(StatsSnapshot s)
    {
        TitleText.Text = "🎒 Loot";
        List<EQBuddy.UI.Shared.LootRow> rows;
        string emptyText;
        if (_fightScope)   // = Target scope for this kind
        {
            // The show/sort bars belong to the Session view; Target is a different axis.
            LootViewBar.Visibility = Visibility.Collapsed;
            LootSortBar.Visibility = Visibility.Collapsed;
            var (header, targetRows) = Main?.TargetDropsContent(s) ?? ("", []);
            var hasTarget = header.Length > 0;
            SubText.Text = hasTarget ? header.Replace("🎯 Fighting: ", "🎯 ") : "No target";
            rows = targetRows.Select(t => new EQBuddy.UI.Shared.LootRow(t.Name, t.Value, null)).ToList();
            emptyText = hasTarget
                ? Main?.TargetEmptyNote(s) ?? "Nothing known for this creature yet."
                : "Swing at something — or /consider it — and its\npossible drops appear here.";
        }
        else
        {
            // "+N made" echoes the card header (merges + crafts), so the two agree (#131).
            var madeTotal = s.CraftedTotal + s.FashionedTotal;
            SubText.Text = $"Session · {s.LootTotal} item{(s.LootTotal == 1 ? "" : "s")} looted"
                + (madeTotal > 0 ? $" · +{madeTotal} made" : "");

            // Same show filter and row order as the Loot card, via the shared builder:
            // all / looted (corpse) / other (foraged, crafted, merged, parcel). Uncapped.
            // Auto-sells never reach the snapshot's loot at all (LW, 2026-08-17).
            var mode = _settings.LootSort;
            var view = _settings.LootView == "made" ? "other" : _settings.LootView;
            static bool IsOther(string src) =>
                src is EQBuddy.UI.Shared.LootRows.ForageSource or EQBuddy.UI.Shared.LootRows.ParcelSource;
            var hasLooted = s.Loot.Any(l => !IsOther(l.LastSource));
            var hasOther = s.Loot.Any(l => IsOther(l.LastSource))
                           || s.Crafted.Count > 0 || s.Fashioned.Count > 0;

            // The show toggle stays up whenever there's ANY loot, so the filter is
            // discoverable even with only one kind of item (LW, 2026-08-17).
            LootViewBar.Visibility = hasLooted || hasOther ? Visibility.Visible : Visibility.Collapsed;
            rows = EQBuddy.UI.Shared.LootRows.Build(s.Loot, s.Crafted, s.Fashioned, s.RecentLoot, view, mode);
            // Crafts/merges now carry timestamps too, so recent works for any non-empty view.
            var hasTimeline = view switch
            {
                "looted" => hasLooted,
                "other" => hasOther,
                _ => hasLooted || hasOther,
            };
            LootSortBar.Visibility = rows.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            // Recent means nothing without timestamped items (crafted/merged carry none).
            LootSortRecent.Visibility = hasTimeline ? Visibility.Visible : Visibility.Collapsed;

            emptyText = (hasLooted || hasOther)
                ? (view == "looted" ? "No looted items yet." : "Nothing else yet.")
                : "No loot seen yet.";
        }

        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (rows.Count == 0)
        {
            EmptyText.Text = emptyText;
            Rows.Items.Clear();
            _signature = "";
            return;
        }

        var sig = $"loot|{_fightScope}|{_settings.LootView}|{_settings.LootSort}|{SubText.Text}|"
            + string.Join(",", rows.Select(r => r.Item + r.Value + r.Tag));
        if (sig == _signature) return;
        _signature = sig;

        Rows.Items.Clear();
        var barBrush = BreakdownRows.BarBrush(this);
        foreach (var r in rows)
            Rows.Items.Add(BuildItemRow(r.Item, r.Value, barBrush, r.Tag));
    }

    /// <summary>An item row wired the way David specced the breakout: hover = the eqlwiki
    /// item info, fetched on the spot if the cache is empty (the tooltip live-updates
    /// from "Looking up…"); click = the eqlwiki page in the browser.</summary>
    private Grid BuildItemRow(string name, string value, Brush barBrush, string? tag = null)
    {
        var cachedTip = Main?.CachedItemStats(name);

        // Quest loot in the minimized Loot window carries the same 🗺 as the Loot card:
        // click → the Quest Tracker filtered to this item's quests (David, 2026-08-07 —
        // "the one we see when minimizing EQBuddy"). The row itself stays "click = the
        // item's wiki page".
        TextBlock? badge = null;
        if (Main is { } m && m.IsActiveQuestItem(name))
        {
            badge = new TextBlock
            {
                Text = "🗺", FontSize = 11, Margin = new Thickness(6, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Part of a quest — click for its quest info",
            };
            badge.SetResourceReference(TextBlock.ForegroundProperty, "GoodBrush");
            var badgeItem = name;
            badge.MouseLeftButtonDown += (_, e) => e.Handled = true;
            badge.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                m.OpenQuestInfoForItem(badgeItem);
            };
        }

        var row = BreakdownRows.Row(this, name, value, 0, barBrush, null, nameBadge: badge,
            nameNote: tag is { Length: > 0 } ? $"({tag})" : null);
        var tipText = new TextBlock
        {
            Text = cachedTip ?? "Looking up on eqlwiki…",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 340,
            FontFamily = new FontFamily("Consolas"),
        };
        var tip = new System.Windows.Controls.ToolTip { Content = tipText };
        row.ToolTip = tip;

        var fetched = false;
        tip.Opened += async (_, _) =>
        {
            // Fetch once per row lifetime; a cache hit inside FetchItemTooltip is free.
            if (fetched || Main is not { } main) return;
            fetched = true;
            var text = await main.FetchItemTooltip(name);
            tipText.Text = text ?? (cachedTip ?? "Not on the wiki.");
        };

        row.Cursor = System.Windows.Input.Cursors.Hand;
        row.MouseLeftButtonDown += (_, e) => e.Handled = true;   // don't start a window drag
        row.MouseLeftButtonUp += (_, _) => MainWindow.OpenWikiPage(name);
        return row;
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
        TitleText.Text = "⏳ Buff set";
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
        var (classes, picked) = main.BuffSetClassSource(s);
        // The class filter, visible: the combination this set is assembled for, its
        // source named honestly — the log has no /who or loadout-change line to read.
        SubText.Text = main.BuffSetCharacterName + " · " + (classes.Count > 0
            ? string.Join("/", classes.Select(QuestClassFilter.Abbrev)) + (picked ? "" : " (inferred)")
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
            + "|loss:" + losses.Count + (_lossesOpen ? "▾" : "▸")
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
                    Text = "nothing picked for this class yet", FontSize = 11,
                    FontStyle = FontStyles.Italic, Margin = new Thickness(4, 0, 0, 2),
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
        var row = new Grid { Margin = new Thickness(4, 3, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new TextBlock
        {
            Text = $"new at your level — add {sug.Spell} to {sug.Class}?",
            FontSize = 11, FontStyle = FontStyles.Italic,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Your level-up made this buff available. ✓ adds it to that class's "
                + "bucket; ✕ never asks again for this character. A new RANK of a set "
                + "buff folds into the same slot and is never suggested.",
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        row.Children.Add(text);
        var add = new Button
        {
            Style = (Style)FindResource("IconButton"), Content = "✓", FontSize = 11,
            Margin = new Thickness(4, 0, 0, 0), ToolTip = $"Add {sug.Spell} to your {sug.Class} set",
        };
        add.Click += (_, _) => main.AcceptBuffSuggestion(sug);
        Grid.SetColumn(add, 1);
        row.Children.Add(add);
        var dismiss = new Button
        {
            Style = (Style)FindResource("IconButton"), Content = "✕", FontSize = 11,
            Margin = new Thickness(4, 0, 0, 0),
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
        var head = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock
        {
            Text = $"{(_lossesOpen ? "▾" : "▸")} lost this session ({losses.Count})",
            FontSize = 11, Cursor = System.Windows.Input.Cursors.Hand,
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
        head.Children.Add(label);
        var copy = new TextBlock
        {
            Text = "⧉", FontSize = 11, Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(4, 0, 0, 0),
            ToolTip = "Copy the list as plain text — evidence for a bug report to the game devs.",
        };
        copy.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        copy.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            try
            {
                Clipboard.SetText(main.BuffLosses.ExportText(main.BuffSetCharacterName));
                copy.Text = "✓";
                var t = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromSeconds(1.5) };
                t.Tick += (_, _) => { copy.Text = "⧉"; t.Stop(); };
                t.Start();
            }
            catch (Exception ex) { App.LogError(ex); }
        };
        Grid.SetColumn(copy, 1);
        head.Children.Add(copy);
        Rows.Items.Add(head);
        if (!_lossesOpen) return;
        foreach (var loss in losses)
        {
            var row = new TextBlock
            {
                Text = $"{loss.Time:h:mm:ss tt}  {loss.Spell} — {loss.Cause}",
                FontSize = 10.5, Margin = new Thickness(8, 0, 0, 1),
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
        var row = new Grid { Margin = new Thickness(4, 1, 0, 1) };
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
            Text = entry.Spell, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (italic) name.FontStyle = FontStyles.Italic;
        name.SetResourceReference(TextBlock.ForegroundProperty, brush);
        row.Children.Add(name);
        var clock = new TextBlock
        {
            Text = BuffStatusText(entry), FontSize = 11, Margin = new Thickness(6, 0, 0, 0),
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
            Style = (Style)FindResource("IconButton"), Content = "✕", FontSize = 11,
            Margin = new Thickness(4, 0, 0, 0), ToolTip = $"Remove {entry.Spell} from {cls}",
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
        Highlight(ScopeFight, _fightScope);
        Highlight(ScopeSession, !_fightScope);
        _signature = "";

        void Highlight(TextBlock t, bool on)
        {
            t.SetResourceReference(TextBlock.ForegroundProperty, on ? "AccentBrush" : "DimBrush");
            if (on) t.SetResourceReference(TextBlock.BackgroundProperty, "ToggleHighlightBrush");
            else t.Background = Brushes.Transparent;
        }
    }

    private void OnScopeFight(object sender, MouseButtonEventArgs e)
    {
        _fightScope = true; SetScopeSetting("fight"); ApplyScopeVisual(); e.Handled = true;
    }

    private void OnScopeSession(object sender, MouseButtonEventArgs e)
    {
        _fightScope = false; SetScopeSetting("session"); ApplyScopeVisual(); e.Handled = true;
    }

    /// <summary>Count / Name / Recent for the Loot Session view — writes the same
    /// _settings.LootSort the main Loot card reads, so the two windows stay in lockstep.</summary>
    private void OnLootSortClick(object sender, MouseButtonEventArgs e)
    {
        _settings.LootSort = (string)((FrameworkElement)sender).Tag;
        _settings.Save();
        ApplyLootSortVisual();
        _signature = "";
        // Repaint now from the last snapshot rather than waiting up to a second for the
        // next tick — the reorder should feel instant.
        if (_lastSnapshot is { } s) UpdateLoot(s);
        e.Handled = true;
    }

    private void ApplyLootSortVisual()
    {
        // "count" is the default: anything that isn't name/recent lights it, matching the
        // Loot card's own read of the shared setting.
        var mode = _settings.LootSort;
        LootSortCount.SetResourceReference(TextBlock.ForegroundProperty,
            mode is "name" or "recent" ? "DimBrush" : "AccentBrush");
        LootSortName.SetResourceReference(TextBlock.ForegroundProperty,
            mode == "name" ? "AccentBrush" : "DimBrush");
        LootSortRecent.SetResourceReference(TextBlock.ForegroundProperty,
            mode == "recent" ? "AccentBrush" : "DimBrush");
    }

    /// <summary>all / looted / made for the Loot Session view — writes the same
    /// _settings.LootView the main Loot card reads, so the two windows stay in step.</summary>
    private void OnLootViewClick(object sender, MouseButtonEventArgs e)
    {
        _settings.LootView = (string)((FrameworkElement)sender).Tag;
        _settings.Save();
        ApplyLootViewVisual();
        _signature = "";
        if (_lastSnapshot is { } s) UpdateLoot(s);   // repaint now, not next tick
        e.Handled = true;
    }

    private void ApplyLootViewVisual()
    {
        var view = _settings.LootView == "made" ? "other" : _settings.LootView;
        LootViewAll.SetResourceReference(TextBlock.ForegroundProperty,
            view is "looted" or "other" ? "DimBrush" : "AccentBrush");
        LootViewLooted.SetResourceReference(TextBlock.ForegroundProperty,
            view == "looted" ? "AccentBrush" : "DimBrush");
        LootViewOther.SetResourceReference(TextBlock.ForegroundProperty,
            view == "other" ? "AccentBrush" : "DimBrush");
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
