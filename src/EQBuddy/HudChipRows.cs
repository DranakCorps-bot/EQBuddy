using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// THE TWO CHIP ROWS' lifecycle (DRA-352 D1) — the FIGHT row (mez &amp; slow, watch alerts,
/// buffs; SA-2's window) and the SPAWN row (respawn countdowns), each a
/// <see cref="HudChipRowWindow"/> made on first need and hidden when it has nothing to show,
/// with nothing saved either way.
///
/// **Lifted out of <c>MainWindow</c> in the slice that made it two**, rather than growing the
/// hotspot (CLAUDE.md: when MainWindow runs out of ratchet room, lift a surface out). What
/// stayed in the widget is the one question that is genuinely about ITS windows — whether
/// World is up on Camps — and the <see cref="HudChipRow.Build"/> call that answers every
/// question about what is on screen.
///
/// **ONE build, two shares.** <see cref="Follow"/> takes the merged row and cuts it with
/// <see cref="HudChipRow.ForRow"/>; the windows never build their own (trap 33). The FIGHT
/// row is placed first every tick, because a slaved spawn row stacks beyond wherever the
/// fight row landed (<see cref="HudChipRow.OtherRowOccupies"/>).
/// </summary>
internal sealed class HudChipRows(MainWindow main, SpawnsViewModel spawns)
{
    /// <summary>The fight row — SA-2's window, title and settings.</summary>
    public HudChipRowWindow? Fight { get; private set; }

    /// <summary>The spawn row — new in D1, with its own park pair and grow flag.</summary>
    public HudChipRowWindow? Spawn { get; private set; }

    /// <summary>The window drawing <paramref name="row"/>, if it has been made yet.</summary>
    public HudChipRowWindow? Of(HudRowKind row) => row == HudRowKind.Spawn ? Spawn : Fight;

    /// <summary>Both rows that exist — what a chip-scale change re-applies to.</summary>
    public IEnumerable<HudChipRowWindow> Existing =>
        new[] { Fight, Spawn }.Where(w => w is not null)!;

    /// <summary>Is Edit HUD on? The fight row's state IS the mode's; the spawn row is only
    /// ever set to match it (<see cref="ToggleEdit"/>).</summary>
    public bool Editing => Fight is { Editing: true };

    /// <summary>One tick: split the merged row and hand each window its share.</summary>
    public void Follow(IReadOnlyList<HudChipEntry> merged)
    {
        FollowRow(HudRowKind.Fight, HudChipRow.ForRow(merged, HudRowKind.Fight));
        FollowRow(HudRowKind.Spawn, HudChipRow.ForRow(merged, HudRowKind.Spawn));
    }

    private void FollowRow(HudRowKind kind, List<HudChipEntry> row)
    {
        // An empty row goes away — unless Edit HUD is open, which is for editing families
        // that have nothing running right now (SA-4).
        var window = Of(kind);
        if (row.Count == 0 && window is not { Editing: true }) { window?.Hide(); return; }
        var chips = Ensure(kind);
        if (!chips.IsVisible) chips.Show();
        chips.Follow(row);
    }

    /// <summary>A row window, made on first need — a chip arriving, or Edit HUD.</summary>
    private HudChipRowWindow Ensure(HudRowKind kind) => kind == HudRowKind.Spawn
        ? Spawn ??= new HudChipRowWindow(main, spawns, HudRowKind.Spawn)
        : Fight ??= new HudChipRowWindow(main, spawns, HudRowKind.Fight);

    /// <summary>Edit HUD on BOTH rows (D1): each family's Place/Mute chicklet is in the
    /// window that draws it, so one pencil press edits everything on screen. The fight row
    /// toggles and the spawn row is SET to match, so the two cannot drift apart.</summary>
    public void ToggleEdit()
    {
        var fight = Ensure(HudRowKind.Fight);
        fight.ToggleEdit();
        Ensure(HudRowKind.Spawn).SetEditing(fight.Editing);
    }
}
