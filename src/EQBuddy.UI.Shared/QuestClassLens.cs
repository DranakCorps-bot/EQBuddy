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

    /// <summary>
    /// **What a "My classes" quick-select TICKS (DRA-216 D1, requirement S4.3).**
    ///
    /// <para>Selecting every class the character actually plays is three or four clicks
    /// through a sixteen-row popup, and the player is re-entering something the app already
    /// knows. This is the one call that joins the two: the resolved identity list
    /// (<see cref="EQBuddy.Core.CharacterClasses.Resolve"/>, via the host's own
    /// <c>ClassSourceFor</c>) expressed as the lens's own row keys.</para>
    ///
    /// <para><b>It takes no <c>picks</c> parameter, and that is the design rather than an
    /// omission.</b> S4.3 says this answers from <c>Resolve</c> and never from stale
    /// class-filter picks, so the only way to keep that true under later edits is for the
    /// picks not to be in the room: a member that cannot see them cannot quietly grow a
    /// second copy of <see cref="Offered"/>'s ternary (traps 4 and 33). What is already
    /// ticked is the CALLER's business — the action replaces the selection, which is what
    /// "select my classes" means, and the player adds or removes afterwards through the same
    /// rows they always have (S22 AC 6).</para>
    ///
    /// <para><b>It moves no identity.</b> <c>Resolve</c> is read and never written: picks
    /// widen identity and never remove from it (S3.3), so a player who quick-selects and then
    /// unticks a class is narrowing a LENS, and the identity line above the list keeps saying
    /// who they are — Bevel's Helm-signed lock, unchanged by this.</para>
    /// </summary>
    /// <param name="resolved">The character's identity, already resolved. Never re-resolved
    /// here — this file chooses between lists and derives none.</param>
    /// <param name="rows">The keys the lens actually offers (<c>QuestClassFilter.Classes</c>).
    /// **The answer ships the ROW's spelling**, case-insensitively matched, because the tick
    /// is painted by key: an identity carrying "shadow knight" has to light the
    /// "Shadow Knight" row or the action reports success and changes nothing.
    ///
    /// <para><b>A resolved class with NO row is DROPPED, and that case is reachable rather
    /// than theoretical.</b> Two of the three identity sources cannot produce one —
    /// <c>ClassInference</c> gates its output on this very list, and the Character room's
    /// stated chips are built from it — but the DUMP deliberately can:
    /// <c>AchievementsImport.UnlockedClasses</c> runs <c>QuestClassFilter.Canonical</c> and
    /// then KEEPS a name it could not resolve, on the stated grounds that *"a class we do not
    /// recognise is still a class the dump says they hold"*. That is right for identity and
    /// impossible for a LENS: there is no row to tick, and storing a pick no row shows is a
    /// selection the player cannot see to undo. So the identity line keeps naming the class
    /// and the quick-select quietly cannot select it — the one honest split, because the
    /// filter genuinely has nothing to narrow to.</para>
    ///
    /// <para>Which is also why matching is a plain case-insensitive compare and not another
    /// <c>Canonical</c> pass: every producer of identity has already been through it or is
    /// drawn from these rows, so a name that misses here is one <c>Canonical</c> itself could
    /// not place.</para></param>
    /// <returns>Row keys in ROW order, empty when nothing resolves. Row order because the
    /// picker reports its ticks that way, so answering in identity order would store one
    /// spelling of a selection and read back another the moment the player touched anything
    /// else. **Empty means the action has nothing to do** — a character with no dump, no
    /// qualifying log evidence and no statement — and the caller HIDES the control rather
    /// than offering one that silently does nothing.</returns>
    public static IReadOnlyList<string> MyClasses(
        IReadOnlyList<string>? resolved, IReadOnlyList<string>? rows)
    {
        if (resolved is not { Count: > 0 } || rows is not { Count: > 0 }) return [];
        var mine = new HashSet<string>(resolved, StringComparer.OrdinalIgnoreCase);
        return [.. rows.Where(mine.Contains)];
    }
}
