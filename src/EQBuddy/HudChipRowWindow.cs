using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// THE ONE CHIP ROW's host (Surface A / SA-2) — a companion window slaved to the HUD's
/// position, carrying every deadline chicklet on one line.
///
/// It replaces <c>SpawnChipsWindow</c> and <c>MezChipsWindow</c>: two always-on-top floats,
/// two saved positions, two grow-up settings, two near-copies of one renderer.
///
/// **Why a companion window rather than a panel inside the widget.** B3's ruling says "one
/// chip row inside the HUD (expanded state)"; drawn literally inside the widget's visual
/// tree, a chip arriving at spawn-due is a TIMER-DRIVEN RESIZE of a <c>SizeToContent</c>
/// always-on-top window over a fullscreen game — trap 12 / #173's exact mechanism, which
/// cost EverQuest its keyboard on X11. The slaved companion keeps every player-visible
/// property the sign wanted (one row, one place, moves with the HUD, no fourth
/// independently-positioned float, no saved x/y) and leaves the widget's measured size
/// alone. Helm signed the amendment on 2026-09-05.
///
/// **SLAVED BY DEFAULT, AND THAT DEFAULT IS THE WHOLE OF AN UNTOUCHED PROFILE.**
/// <see cref="HudChipRow.Placement"/> recomputes where it goes from the widget every tick.
/// That is what retired <c>ChipStackAnchor</c>, <c>ChipAnchor</c> and eight settings with it:
/// the whole subject of that machinery was persisting a chip stack's own position across
/// reopens, which is where #122 and #152 both lived.
///
/// **OE-8 lets the player PARK it, and deliberately reopens that architecture — on one
/// condition.** <c>AppSettings.HudRowParkLeft</c>/<c>Top</c> is NaN until a player drags the
/// row; NaN is slaved, so a reset or a fresh profile gets the SA-2 app back by construction,
/// with no migration to run twice (trap 55). What made #122/#152 was a window that MOVED
/// ITSELF and rewrote its own anchor as it went; here the anchor has exactly one writer —
/// <see cref="HudDragGrip"/>'s drag end — and neither the follower's tick nor the toolkit's
/// <c>SizeToContent</c> can reach it. Trap 49's three actors, separated by construction
/// rather than by a flag.
///
/// **Visible whenever chips exist, in BOTH HUD states.** The two stacks were visible
/// regardless of whether the widget was minimized — "the stack exists exactly while timers
/// do" — and an expanded-only row would subtract a live capability mid-pass.
///
/// **Built in code, not XAML**, like <see cref="ClickThroughChip"/>: there is no designer
/// surface here worth a BAML pair, and an incremental WPF build can leave a stale assembly
/// with a fresh timestamp (trap 18), which is a hazard a code-built window does not carry.
/// </summary>
internal sealed class HudChipRowWindow : Window
{
    private readonly MainWindow _main;
    private readonly SpawnsViewModel _spawns;
    private readonly WrapPanel _panel;
    private string _signature = "";
    private List<HudChipEntry> _row = [];
    private readonly List<HudChip.Live> _live = [];

    /// <summary>Chips currently drawn, per family, and how many are DUE — the
    /// <c>hudChips</c> dump family the E2E suite asserts. Recorded by
    /// <see cref="Follow"/> rather than counted off the panel, because the panel's children
    /// are chicklet borders and a future separator would quietly join the count.</summary>
    public int MezChips { get; private set; }
    public int SpawnChips { get; private set; }
    /// <summary>SA-3's two net-new families, counted the same way and for the same reason:
    /// a family that silently stops contributing is a 0 beside a live row rather than an
    /// absence nothing names.</summary>
    public int WatchChips { get; private set; }
    public int BuffChips { get; private set; }
    public int DueChips { get; private set; }

    /// <summary>The families actually on the row, in the order they were drawn, as one
    /// space-free token — the <c>hudChipOrder</c> dump fact. Read off the ROW rather than off
    /// the setting on purpose: "the order is in the profile" and "the order reached the
    /// screen" are different claims and only the second one is the feature (trap 42).
    /// <see cref="HudChipRow.OrderKey"/> answers "-" for an empty row.</summary>
    public string RowOrderKey { get; private set; } = "-";

    /// <summary>Edit mode is on — the row is showing one Place/Mute placeholder per family
    /// instead of live chicklets (SA-4). <c>AlertWindow._placement</c>'s shape: a flag the
    /// live path checks, set by entering the mode and cleared by leaving it.</summary>
    public bool Editing { get; private set; }

    public HudChipRowWindow(MainWindow main, SpawnsViewModel spawns)
    {
        _main = main;
        _spawns = spawns;
        // The title is an IDENTITY the screenshot harness matches on (trap 24), so it must
        // not collide with a sibling window of the same process: the widget is "EQBuddy"
        // and the Evolved shell is "EQBuddy — <room>".
        Title = "EQBuddy HUD Chips";
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

        // OE-8 affordance face (Bevel): the grip was silent — a player had no way to
        // discover "the whole box drags" short of reading Options → Alerts & chips. This
        // reuses the app's own established grip language rather than inventing a new one:
        // the widget's HeightGrip and ResizeGrip (MainWindow.xaml) are a Cursor plus a
        // ToolTip, nothing drawn over the content. A row of chicklets has no free chrome to
        // put a handle on without competing with them, so cursor + tooltip is the same
        // answer at whole-box scale. Any chicklet with its own Cursor wins for its own
        // bounds; this is only what shows over the row's background.
        Cursor = System.Windows.Input.Cursors.SizeAll;
        ToolTip = "Drag anywhere on this row to place it. Right-click the widget → "
            + "Edit HUD… → Follow the HUD again brings it back.";

        // One row. A WrapPanel and not a horizontal StackPanel: a stack measures with
        // INFINITE width in the stacking direction, so a fifth chicklet would be clipped at
        // the panel's edge with no ellipsis and no overflow — correct, and not on screen
        // (trap 25, which shipped the Progress window's fourth tab invisible).
        _panel = new WrapPanel { Orientation = Orientation.Horizontal };
        Content = _panel;
        ChipScale.Apply(this, main.Settings.ChipScale);
        WindowZoom.Route(this, () => main.Settings.ChipScale, main.SetChipScale);

        // OE-8: the grip is the whole box. The drag END is the only thing in this app that
        // writes the park pair — see HudDragGrip.
        _grip = HudDragGrip.Attach(this, (left, top) =>
        {
            _main.Settings.HudRowParkLeft = left;
            _main.Settings.HudRowParkTop = top;
            _mode = HudChipRow.HudParkMode.Parked;
            // CLAMP FIRST, THEN RECORD WHERE IT LANDED. The pair is where the window IS, not
            // where the cursor let go, and the difference is a park that cannot come back:
            // `drag-verify.ps1 -Mode park` dropped the row at Top = -15.71 (above the work
            // area), the drag-end write kept that number, and on reopen ScreenGuard
            // CORRECTLY refused it — under 40 units of grab area on screen — so the row went
            // slaved and the reporter would have blamed the restore. Two answers to one
            // question, twenty lines apart (trap 4). The dump's two keys are what showed it:
            // `hudRowPark=1780,0` beside `HudRowParkTop=-15.71`, the effect and the setting
            // disagreeing in one line.
            Park();
            _main.Settings.HudRowParkLeft = Left;
            _main.Settings.HudRowParkTop = Top;
            _main.PersistSettings();
        });
    }

    private readonly HudDragGrip _grip;

    /// <summary>
    /// Slaved / parked / parked-somewhere-this-desk-cannot-show, decided ONCE per window
    /// lifetime from the profile and then only ever by a drag.
    ///
    /// Resolved lazily rather than in the constructor because the reachability question needs
    /// the window's own presentation source to convert units, and this window is built before
    /// it is shown. <see cref="HudChipRow.HudParkMode.Unreachable"/> runs slaved FOR THE
    /// SESSION and leaves the setting alone — #117's rule: the monitors come back, and a
    /// fallback persisted over a carefully chosen point teleports it permanently.
    /// </summary>
    private HudChipRow.HudParkMode? _mode;

    private HudChipRow.HudParkMode Mode => _mode ??= HudChipRow.ParkMode(
        _main.Settings.HudRowParkLeft, _main.Settings.HudRowParkTop,
        ScreenGuard.OnScreen(_main.Settings.HudRowParkLeft, _main.Settings.HudRowParkTop,
            ActualWidth, ActualHeight));

    /// <summary>The <c>hudRowPark</c> dump fact — the EFFECT, read off the window: where the
    /// row actually is, or "slaved". Beside it <see cref="ParkSavedKey"/> reports what the
    /// PROFILE says, because OE-8's unreachable rule is precisely a disagreement between the
    /// two and one key could never show it (trap 42).</summary>
    public string ParkKey => Mode == HudChipRow.HudParkMode.Parked
        ? HudChipRow.ParkKey(Left, Top) : "slaved";

    /// <summary>The grip's presses and finished drags, as "P,D" — the <c>hudRowGrip</c> dump
    /// fact. See <see cref="HudDragGrip.PressCount"/> for why a harness reporting "the drag
    /// persisted nothing" needs these two numbers to mean anything.</summary>
    public string GripKey => $"{_grip.PressCount},{_grip.DragCount}";

    /// <summary>What the profile holds, whether or not this desk can honour it.</summary>
    public string ParkSavedKey =>
        HudChipRow.ParkKey(_main.Settings.HudRowParkLeft, _main.Settings.HudRowParkTop);

    /// <summary>"Follow the HUD again" (Edit HUD) — the way back from a park, and the only
    /// other writer of the pair. It clears to NaN rather than to a computed position: NaN IS
    /// slaved, so the row goes back to being recomputed from the widget every tick instead of
    /// being parked where the widget happens to be standing right now.</summary>
    public void Unpark()
    {
        _main.Settings.HudRowParkLeft = double.NaN;
        _main.Settings.HudRowParkTop = double.NaN;
        _mode = HudChipRow.HudParkMode.Slaved;
        Park();
    }

    /// <summary>Is this window parked right now — what the Edit-HUD un-park control reads to
    /// decide whether it has anything to undo.</summary>
    public bool IsParked => Mode == HudChipRow.HudParkMode.Parked;

    /// <summary>
    /// One tick: draw <paramref name="row"/> and park under the widget.
    ///
    /// Order matters — the chicklets are laid out BEFORE the placement is computed, because
    /// the flip-above-the-widget rule needs a real height and <see cref="Window.ActualHeight"/>
    /// is last tick's until the panel has measured.
    /// </summary>
    public void Follow(IReadOnlyList<HudChipEntry> row)
    {
        _row = [.. row];
        MezChips = HudChipRow.CountOf(_row, HudChipFamily.Mez);
        SpawnChips = HudChipRow.CountOf(_row, HudChipFamily.Spawn);
        WatchChips = HudChipRow.CountOf(_row, HudChipFamily.WatchFire);
        BuffChips = HudChipRow.CountOf(_row, HudChipFamily.Buff);
        DueChips = HudChipRow.DueCount(_row);
        // Consecutive chicklets of one family are one entry: this is the FAMILY order that
        // reached the screen, not a chip census — the counts above are the census.
        RowOrderKey = HudChipRow.OrderKey(
            _row.Select(e => e.Family).Where((f, i) => i == 0 || _row[i - 1].Family != f));

        // Edit mode owns the panel while it is on. The counts above still describe the live
        // row the player is editing — they are what the tick computed, and a dump that went
        // blank the moment the mode opened could not assert that a mute took effect.
        if (Editing) { Park(); return; }

        var signature = HudChipRow.Signature(_row);
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild();
        }
        else
        {
            for (var i = 0; i < _row.Count && i < _live.Count; i++)
                HudChip.Tick(_live[i], _row[i]);
        }

        Park();
    }

    /// <summary>Where the companion sits this tick — slaved to the widget, or held at the
    /// corner the player parked it at. Shared by the live path and the edit path: the row
    /// keeps whichever placement it has in both, because a row you are reordering that jumped
    /// somewhere else for the duration of the mode would be answering a question nobody
    /// asked.</summary>
    private void Park()
    {
        // A drag in progress owns the window. The follower re-placing it mid-gesture would
        // fight the cursor once a second, which reads as a window that will not be dragged —
        // and it is the follower actor reaching for geometry the player is holding.
        if (_grip.Dragging) return;

        if (Mode == HudChipRow.HudParkMode.Parked) { ParkAtAnchor(); return; }

        // The row may not run off the monitor the widget is on. MaxWidth makes the
        // WrapPanel wrap instead of growing a window wider than the screen; the arithmetic
        // for WHERE it goes is HudChipRow.Placement's, tested without a window.
        var area = SystemParameters.WorkArea;
        MaxWidth = HudChipRow.WrapWidth(area.Width);
        UpdateLayout();
        // THE UNDER-BAR PANEL IS SLAVED TO THE SAME EDGE (OE-1), so the row parks below it
        // rather than on top of it. Handed to Placement as part of the HUD's own height
        // because that is exactly what it is to a chicklet: the widget and whatever is
        // hanging off it are one block, and the flip-above-the-widget rule has to treat them
        // as one or it will flip the row into the panel. Zero whenever no panel is up.
        //
        // **A PARKED panel is no longer under the bar, so it no longer occupies that space**
        // — asking the bar for a height the panel is not standing in would leave a gap the
        // player can see and cannot explain.
        var occupied = _main.ActualHeight + _main._hudExpandBar.SlavedOccupiedHeight;
        var (left, top) = HudChipRow.Placement(
            _main.Left, _main.Top, occupied, ActualHeight, area.Top, area.Bottom);
        if (Left != left) Left = left;
        if (Top != top) Top = top;
    }

    /// <summary>
    /// Screen-ABSOLUTE placement at the player's corner (OE-8 §2.3). The widget is not
    /// consulted at all: the park is about where the FIGHT is on screen, not where the bar
    /// is, and a widget-relative offset would quietly drag the row off the fight the first
    /// time someone moved the bar.
    ///
    /// The wrap cap and the clamp read THE PARKED POINT'S OWN MONITOR (the plan's named
    /// implement check), not <c>SystemParameters.WorkArea</c>'s primary — a row parked on a
    /// second display would otherwise be yanked back the first time a chicklet arrived.
    /// </summary>
    private void ParkAtAnchor()
    {
        var anchorLeft = _main.Settings.HudRowParkLeft;
        var anchorTop = _main.Settings.HudRowParkTop;
        var area = ScreenGuard.WorkAreaAt(this, anchorLeft, anchorTop);
        MaxWidth = HudChipRow.WrapWidth(area.Width);
        UpdateLayout();
        var (left, top) = HudChipRow.ParkedPlacement(
            anchorLeft, anchorTop, ActualWidth, ActualHeight,
            area.Left, area.Top, area.Right, area.Bottom);
        if (Left != left) Left = left;
        if (Top != top) Top = top;
    }

    /// <summary>
    /// "Edit HUD…" — turn the row into its own editor, or turn it back.
    ///
    /// **<c>AlertWindow.EnterPlacement</c>/<c>ExitPlacement</c>'s shape**, which B3 named as
    /// the precedent worth reusing: a mode the player switches on, affordances that exist
    /// only while it is on, and the ordinary surface back the moment it is off. Two
    /// differences, both because this row is not that tile:
    ///
    /// <list type="bullet">
    /// <item>**No click-through to restore.** The alert tile is permanently click-through and
    /// has to stop being so to be dragged; the chip row has always taken clicks (a chip
    /// dismisses on one), so entering the mode changes what is drawn and nothing else.</item>
    /// <item>**Every change is persisted as it is made**, rather than on the way out.
    /// <c>ExitPlacement</c> can save on exit because a drag has one end; a nudge has no end,
    /// and a mode whose work is lost if the app closes while it is open would be a worse
    /// bargain than the file write a tick box already costs.</item>
    /// </list>
    /// </summary>
    public void ToggleEdit()
    {
        Editing = !Editing;
        _signature = HudChipRow.DismissedSignature;   // force a real rebuild either way
        if (Editing) { RebuildEdit(); Park(); if (!IsVisible) Show(); }
        // Straight back to the live row — including hiding it, if the families the player
        // was editing have nothing running.
        _main.RefreshHudChips();
    }

    /// <summary>One Place/Mute placeholder per family, in the stored order — muted ones
    /// included, dimmed, because a mute you cannot see is a mute you cannot undo.</summary>
    private void RebuildEdit()
    {
        _panel.Children.Clear();
        _live.Clear();
        var order = HudChipRow.ResolveOrder(_main.Settings);
        for (var i = 0; i < order.Count; i++)
        {
            var family = order[i];
            _panel.Children.Add(HudEditChip.Build(family,
                muted: HudChipRow.IsMuted(_main.Settings, family),
                canLeft: i > 0, canRight: i < order.Count - 1,
                onNudge: delta => Apply(() => HudChipRow.SetOrder(
                    _main.Settings, HudChipRow.Nudge(HudChipRow.ResolveOrder(_main.Settings), family, delta))),
                onMute: () => Apply(() => HudChipRow.SetMuted(
                    _main.Settings, family, !HudChipRow.IsMuted(_main.Settings, family)))));
        }
        // "Follow the HUD again" (OE-8) — the way back from a free-drag, beside mute and
        // order because one editor answers every "how do I undo what I did to the row"
        // question. It is drawn ALWAYS, disabled when there is nothing parked: a control that
        // only exists once you are lost is a control nobody has seen before they need it, and
        // Edit HUD is the one door this row has.
        //
        // It un-parks BOTH companion windows. The panel has no editor of its own, and to a
        // player "the stuff hanging off my HUD" is one object — an under-bar panel stranded
        // in a corner with no door would be the capability-with-no-way-back that trap 59
        // names.
        _panel.Children.Add(HudEditChip.Unpark(
            IsParked || _main._hudExpandBar.IsParked,
            () => Apply(() => { Unpark(); _main._hudExpandBar.Unpark(); })));
        _panel.Children.Add(HudEditChip.Hint());
    }

    /// <summary>An edit: write it, persist it, redraw the editor. The redraw is what moves the
    /// chicklet the player just nudged, so the preview of the order IS the order.</summary>
    private void Apply(Action edit)
    {
        edit();
        _main.PersistSettings();
        RebuildEdit();
        Park();
    }

    private void Rebuild()
    {
        _panel.Children.Clear();
        _live.Clear();
        foreach (var entry in _row)
        {
            _panel.Children.Add(HudChip.Build(entry, out var live,
                onClick: ClickOf(entry), onDoubleClick: DoubleClickOf(entry),
                onDismiss: DismissOf(entry)));
            _live.Add(live);
        }
    }

    /// <summary>A due SPAWN chip has said its piece — a click acknowledges it and clears
    /// the timer. Everything else is inert to a single click: the DRAG both stacks carried
    /// here died with free placement.</summary>
    private Action? ClickOf(HudChipEntry entry) =>
        entry is { Family: HudChipFamily.Spawn, Chip: { IsDue: true, Zone.Length: > 0 } chip }
            ? () => ClearTimer(chip.Zone, chip.Name)
            : null;

    /// <summary>The World window's Camps tab, opened on the chip's zone (World PR 2 —
    /// Bevel-signed chip hide-rule). Spawn chips only: a mez belongs to no zone list.
    /// </summary>
    private Action? DoubleClickOf(HudChipEntry entry) =>
        entry is { Family: HudChipFamily.Spawn, Chip.Zone.Length: > 0 } e
            ? () => _main.ShowWorldWindow(WorldTab.Camps, e.Chip.Zone)
            : null;

    /// <summary>Right-click dismisses. A spawn timer clears whether DUE or still counting —
    /// a camp abandoned mid-countdown should not haunt the row until it expires (Reddit,
    /// anyhow188). A fight chip is dismissible only when its own tracker gave it a way
    /// (a slow; a mez clears itself off the log).</summary>
    private Action? DismissOf(HudChipEntry entry) => entry switch
    {
        { Family: HudChipFamily.Spawn, Chip.Zone.Length: > 0 } e =>
            () => ClearTimer(e.Chip.Zone, e.Chip.Name),
        { Chip.OnDismiss: { } dismiss } => dismiss,
        _ => null,
    };

    /// <summary>Clearing a timer must REBUILD on the same tick, and the reset value is a
    /// SENTINEL rather than "": dismissing the last chip makes the new signature the empty
    /// string too, and a matching reset skips the rebuild and leaves a ghost chicklet
    /// painted (Don's catch porting this window, PR #67).</summary>
    private void ClearTimer(string zone, string name)
    {
        _spawns.ClearTimer(zone, name);
        _signature = HudChipRow.DismissedSignature;
        _main.RefreshHudChips();
    }
}
