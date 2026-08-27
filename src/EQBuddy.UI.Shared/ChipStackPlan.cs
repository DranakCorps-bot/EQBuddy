namespace EQBuddy.UI.Shared;

/// <summary>
/// When the two overlay chip stacks exist. Both widgets' per-second ticks ask HERE, so the
/// rules cannot drift between lanes (the #122/#152 lesson) — before 2026-08-27 each
/// MainWindow hand-rolled these expressions inline and the Bevel-signed Camps rule had no
/// unit test at all.
///
/// The SPAWN stack is ambient: it exists exactly while timers do (no pop-open of the full
/// window, ever — David's design). One exception, the Bevel-signed World pre-design
/// amendment's chip hide-rule: while the World window is visible AND showing the Camps tab,
/// the same timers are already on screen there, so the overlay stack hides. Map, Path,
/// Travels and a closed window all leave it up — the window is a browser, not a replacement.
///
/// The FIGHT stack (mez + slow chips, one window, one saved position) lives its own life,
/// independent of spawn tracking: mez chips park next to the fight, spawn chips are ambient
/// (David's call). Optional since the 2026-08-11 Reddit ask — a non-CC class never wants the
/// stack. While Options is open the stack exists even when empty, as a placement preview
/// (#94 follow-up), so it can be parked BEFORE the first mid-fight debuff.
/// </summary>
public static class ChipStackPlan
{
    /// <summary>Should the spawn-chip stack be on screen this tick?</summary>
    public static bool SpawnStack(bool trackSpawns, bool hiddenForFocus, bool worldOnCamps,
        bool hasActiveTimers)
        => trackSpawns && !hiddenForFocus && !worldOnCamps && hasActiveTimers;

    /// <summary>Should the fight-chip stack be on screen this tick? <paramref name="mezHasChips"/>
    /// and <paramref name="slowHasChips"/> are "enabled AND the tracker has one" — emptiness is
    /// probed cheaply by the caller so the full chip list isn't built twice a second just to
    /// learn it was empty. <paramref name="slowEnabled"/> is the raw setting: the placement
    /// preview shows for a raid-only slow chip even out of raid, because parking the stack is
    /// exactly what you do before the raid.</summary>
    public static bool FightStack(bool hiddenForFocus, bool optionsOpen, bool mezEnabled,
        bool slowEnabled, bool mezHasChips, bool slowHasChips)
        => !hiddenForFocus
           && ((optionsOpen && (mezEnabled || slowEnabled)) || mezHasChips || slowHasChips);

    /// <summary>The draggable placeholder an empty fight stack shows while Options is open —
    /// one home for the wording, drawn identically by both widgets.</summary>
    public static SpawnChip PlacementPreview() => new(Zone: "",
        Name: "drag me — chips appear here", CountdownText: "", IsDue: false,
        Detail: "Placement preview: mez and slow chips will stack at this "
            + "spot. Drag it where you'll notice them; it disappears when "
            + "Options closes.",
        Icon: "ChevronsDown");
}
