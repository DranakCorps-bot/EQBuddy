namespace EQBuddy.UI.Shared;

/// <summary>
/// **Which classes the quest surface is ABOUT — one producer, for every surface that asks
/// (DRA-181 D4, plan P5).**
///
/// <para>The answer is picks-first: the classes the player has ticked in the multi-select
/// when they have ticked any, and the RESOLVED identity list
/// (<see cref="EQBuddy.Core.CharacterClasses.Resolve"/>) otherwise. Three renderers were
/// each writing their own copy of that ternary — the desktop's render, the phone's Sky
/// leftover bands, and the desktop's chip strip, which wrote a DIFFERENT one: it read the
/// resolved list alone. Trap 33, in its plainest form — two callers with different
/// arguments produce two current answers, and whichever ran last wins.</para>
///
/// <para><b>What that cost, on the Founder's own screen (2026-09-17 Desktop smoke):</b>
/// he picked Warrior/Paladin/Cleric and the chips under the picker went on offering every
/// class the dump and the log had ever resolved. A leftover chip is also a DEAD control —
/// clicking it sets a lens to a class the render has already narrowed away, and the
/// render silently clears it again. A click that does nothing, with nothing on screen
/// saying so.</para>
///
/// <para><b>Picks WIN even where the resolved list does not carry them, and that is not a
/// bug to fix here.</b> The class picker offers all sixteen on purpose — "we may be
/// helping a friend" (David, 2026-08-15) — and
/// <see cref="EQBuddy.Core.CharacterClasses.Resolve"/> caps identity at
/// <see cref="EQBuddy.Core.CharacterClasses.Max"/> and leaves picks out entirely once the
/// player has STATED their classes. So a picked class that identity does not hold is a
/// real, reachable state, the render has always narrowed to it, and the strip must offer
/// a chip for it or the pick is a lens with no way to pull it.</para>
///
/// <para><b>The identity RULE is not this.</b> Bevel's Helm-signed lock — "identity stays
/// on screen after picks. It is not the filter" — governs the identity LINE, which keeps
/// reading the resolved list. <c>Resolve</c> is untouched: picks widen it and never
/// remove from it. This decides only which classes a view narrows to, and which chips it
/// offers to narrow further.</para>
/// </summary>
public static class QuestClassLens
{
    /// <summary>The classes the surface is about. Empty in, empty out — a caller with
    /// neither picks nor an identity gets nothing rather than a wildcard, which is what
    /// suppresses the Sky leftover bands' "only other classes want this" claim.</summary>
    /// <param name="picks">What the player ticked in the class multi-select
    /// (<c>QuestLedgerStore.ClassesFor</c>). Written by the desktop picker AND by the
    /// phone (<c>CompanionActions</c>), so a surface holding this answer has to redraw
    /// when it moves.</param>
    /// <param name="resolved">The character's identity, already resolved by
    /// <see cref="EQBuddy.Core.CharacterClasses.Resolve"/>. Never re-resolved here: this
    /// file chooses between two lists and derives neither.</param>
    public static IReadOnlyList<string> Offered(
        IReadOnlyList<string>? picks, IReadOnlyList<string>? resolved) =>
        picks is { Count: > 0 } ? picks : resolved ?? [];
}
