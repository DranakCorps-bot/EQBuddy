namespace EQBuddy.UI.Shared;

/// <summary>
/// When each chip FAMILY is on screen. The widget's per-second tick asks HERE, so the rules
/// cannot drift back into the window (before 2026-08-27 each MainWindow hand-rolled these
/// expressions inline and the Bevel-signed Camps rule had no unit test at all).
///
/// The SPAWN family is ambient: it exists exactly while timers do (no pop-open of the full
/// window, ever — David's design). One exception, the Bevel-signed World pre-design
/// amendment's chip hide-rule: while the World window is visible AND showing the Camps tab,
/// the same timers are already on screen there, so the family drops off the row. Map, Path,
/// Travels and a closed window all leave it up — the window is a browser, not a replacement.
///
/// The FIGHT family (mez + slow chips) lives its own life, independent of spawn tracking.
/// Optional since the 2026-08-11 Reddit ask — a non-CC class never wants it.
///
/// **This says WHETHER a family shows; <see cref="HudChipRow"/> says what the row looks like
/// once it does.** Two questions, two homes, both unit-tested with no window.
///
/// **The Options-open PLACEMENT PREVIEW retired with free placement (Surface A / SA-2).**
/// <c>FightStack</c> used to take <c>optionsOpen</c> plus the two raw enable flags so that an
/// EMPTY fight stack still appeared while Options was open, carrying a draggable "drag me —
/// chips appear here" placeholder to be parked before the first mid-fight debuff. There is
/// nothing to park any more: the one row is slaved to the HUD's position, saves no
/// coordinates and cannot be dragged, so a preview of where it will land would be a preview
/// of a decision the player no longer makes. The three parameters went with it rather than
/// being left as arguments nothing reads (trap 20's shape, caught at fold time).
/// </summary>
public static class ChipStackPlan
{
    /// <summary>Should the spawn family be on the row this tick?</summary>
    public static bool SpawnStack(bool trackSpawns, bool hiddenForFocus, bool worldOnCamps,
        bool hasActiveTimers)
        => trackSpawns && !hiddenForFocus && !worldOnCamps && hasActiveTimers;

    /// <summary>Should the fight family be on the row this tick? <paramref name="mezHasChips"/>
    /// and <paramref name="slowHasChips"/> are "enabled AND the tracker has one" — emptiness is
    /// probed cheaply by the caller so the full chip list isn't built twice a second just to
    /// learn it was empty.</summary>
    public static bool FightStack(bool hiddenForFocus, bool mezHasChips, bool slowHasChips)
        => !hiddenForFocus && (mezHasChips || slowHasChips);
}
