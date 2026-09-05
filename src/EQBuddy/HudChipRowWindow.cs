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
    public int DueChips { get; private set; }

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
        DueChips = HudChipRow.DueCount(_row);

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

        // The row may not run off the monitor the widget is on. MaxWidth makes the
        // WrapPanel wrap instead of growing a window wider than the screen; the arithmetic
        // for WHERE it goes is HudChipRow.Placement's, tested without a window.
        var area = SystemParameters.WorkArea;
        MaxWidth = Math.Max(120, area.Width);
        UpdateLayout();
        var (left, top) = HudChipRow.Placement(
            _main.Left, _main.Top, _main.ActualHeight, ActualHeight, area.Top, area.Bottom);
        if (Left != left) Left = left;
        if (Top != top) Top = top;
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
