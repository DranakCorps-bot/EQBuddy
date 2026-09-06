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

    /// <summary>Should the watch-fire family be on the row this tick (SA-3)?
    ///
    /// Focus-hide and nothing else. The two SA-2 families each carry an Options toggle of
    /// their own; this one does not, on purpose — a watch rule is already switched on per rule
    /// (<c>TrackedRule.Enabled</c>) and already chooses its on-screen presence per rule
    /// (<c>AlertBanner</c>), so a third global switch would be a fourth answer to a question
    /// the player has answered twice. SA-4's per-family Mute is the one that is coming.
    ///
    /// It lives here rather than inline for the reason the file exists: WHETHER a family shows
    /// has one home, so a later rule (a raid-only gate, say) lands beside its siblings instead
    /// of drifting back into the window.</summary>
    public static bool WatchFireStack(bool hiddenForFocus, bool hasFires)
        => !hiddenForFocus && hasFires;

    /// <summary>Should the buff-expiring family be on the row this tick (SA-3)?
    ///
    /// <paramref name="hasActiveBuffs"/> is the cheap probe — "is anything up at all" — so the
    /// full list is not built twice a second just to learn it was empty. Whether any of those
    /// buffs is actually inside the warning window is <see cref="HudChipRow.BuffChips"/>'
    /// filter, which is where the threshold lives.
    ///
    /// **Hiding the Buffs CARD does not hide these.** A card is a desktop surface and a chip is
    /// an overlay deadline; folding one away is not a statement about the other (B3 §3), and
    /// the `buffs` card key is untouched by SA-3 in any case — it retires with the card, under
    /// the per-item gate (SA-R).</summary>
    public static bool BuffStack(bool hiddenForFocus, bool hasActiveBuffs)
        => !hiddenForFocus && hasActiveBuffs;
}
