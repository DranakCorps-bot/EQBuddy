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
/// **NO GEOMETRY OF ITS OWN, AND NOTHING PERSISTED.** <see cref="HudChipRow.Placement"/>
/// recomputes where it goes from the widget every tick. That is what retires
/// <c>ChipStackAnchor</c>, <c>ChipAnchor</c> and eight settings with it: the whole subject
/// of that machinery was persisting a chip stack's own position across reopens, which is
/// where #122 and #152 both lived, and there is no longer a position to persist.
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

        // One row. A WrapPanel and not a horizontal StackPanel: a stack measures with
        // INFINITE width in the stacking direction, so a fifth chicklet would be clipped at
        // the panel's edge with no ellipsis and no overflow — correct, and not on screen
        // (trap 25, which shipped the Progress window's fourth tab invisible).
        _panel = new WrapPanel { Orientation = Orientation.Horizontal };
        Content = _panel;
        ChipScale.Apply(this, main.Settings.ChipScale);
        WindowZoom.Route(this, () => main.Settings.ChipScale, main.SetChipScale);
    }

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

    /// <summary>Recompute where the slaved companion sits, from the widget, this tick. Shared
    /// by the live path and the edit path: the row follows the HUD in both, because a row you
    /// are reordering that stopped following the window it belongs to would be a fifth
    /// independently-placed float for as long as the mode is open.</summary>
    private void Park()
    {
        // The row may not run off the monitor the widget is on. MaxWidth makes the
        // WrapPanel wrap instead of growing a window wider than the screen; the arithmetic
        // for WHERE it goes is HudChipRow.Placement's, tested without a window.
        var area = SystemParameters.WorkArea;
        MaxWidth = Math.Max(120, area.Width);
        UpdateLayout();
        // THE UNDER-BAR PANEL IS SLAVED TO THE SAME EDGE (OE-1), so the row parks below it
        // rather than on top of it. Handed to Placement as part of the HUD's own height
        // because that is exactly what it is to a chicklet: the widget and whatever is
        // hanging off it are one block, and the flip-above-the-widget rule has to treat them
        // as one or it will flip the row into the panel. Zero whenever no panel is up.
        var occupied = _main.ActualHeight + _main._hudExpandBar.OccupiedHeight;
        var (left, top) = HudChipRow.Placement(
            _main.Left, _main.Top, occupied, ActualHeight, area.Top, area.Bottom);
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
