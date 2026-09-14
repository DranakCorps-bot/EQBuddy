using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **EVERY WORD THE UNLOCK PICKER SAYS, ON BOTH SURFACES** (DRA-71 D5, Fable plan P11).
///
/// <para>The pick is one store (<see cref="UnlockPickStore"/>) read by the Helper room and by
/// the Quests window's Unlocks tab. If the two rooms each spelled the face, the row labels and
/// the withheld note themselves, they would be two pickers that happen to share a dictionary —
/// and the copy that goes stale is always the newer one. The same rule
/// <c>HelperPresentation</c>, <c>LootPresentation</c> and <c>QuestPresentation</c> already
/// carry, paid for once in #184 when three surfaces had already drifted.</para>
///
/// <para>Framework-free, so it is unit-tested rather than eyeballed — and so the phone draws
/// the same face from the same rule when the Helper reaches it (plan P15) rather than growing
/// a second copy.</para>
/// </summary>
public static class UnlockPickReadout
{
    /// <summary>Above the picker. It says what the control DOES and, crucially, what its
    /// OFF-state means: a filter whose empty reading is a mystery is a filter people avoid
    /// touching. This is the sentence that makes "absent = all" visible rather than
    /// documented.</summary>
    public const string Note =
        "Which unlocks are you working on? Nothing picked means EQBuddy shows every one.";

    /// <summary>Hover copy on the face, same job as <c>HelperPresentation.GoalPickerTip</c>:
    /// a button reading "Any unlock" gives a player no reason to suspect thirty rows are
    /// behind it.</summary>
    public const string Tip =
        "Pick the races and classes you are working on — any number of them.";

    /// <summary>The picker's own empty state: the achievements dump is what produces the
    /// list, so with no dump there is nothing to pick from and saying so beats an empty
    /// popup.</summary>
    public const string NoDump =
        "No achievements dump yet, so there is nothing to pick from.";

    /// <summary>
    /// What the face reads.
    /// </summary>
    /// <param name="picked">The picked SUBJECTS that this face's offer actually holds — never
    /// the raw store, which may still carry a name from a dump this character does not have.
    /// A face counting a row nobody can see is a selection the player cannot undo.</param>
    /// <param name="offered">How many rows the picker holds, so ticking all of them says
    /// "All unlocks" by name. The offer is never capped here — unlike the faction picker,
    /// whose list is hundreds long — so this is always a number the face may trust.</param>
    /// <param name="maxChars">The face's width budget. The Quests window's picker SHARES its
    /// row with the section strip, the state lens and the mode strip — the very geometry #184
    /// was about — so it takes <c>PickerFace.MaxChars</c>; the Helper's owns its row and is
    /// given the roomier budget its goals face already uses.</param>
    public static string Face(IReadOnlyList<string> picked, int offered, int maxChars) =>
        PickerFace.For(picked, "unlock", "unlocks", offered: offered, maxChars: maxChars);

    /// <summary>
    /// One picker row: the unlock and how far along it is, which is what makes the list
    /// pickable rather than alphabetical.
    ///
    /// <para>Three states and no fourth, because <c>UnlockProgress.Score</c> has three: done,
    /// counted, and nothing to count. Half Elf is the last of them — its only criterion is
    /// Derived, so "0 of 0" would read as a stalled checklist and the row says the subject's
    /// name alone instead (trap 73: an unanswerable question draws nothing).</para>
    /// </summary>
    public static string Row(UnlockProgress unlock) =>
        unlock.Complete ? $"{unlock.Subject} — unlocked"
        : unlock.Score is { } score ? $"{unlock.Subject} — {score.Done} of {score.Total} done"
        : unlock.Subject;

    /// <summary>
    /// Said under a section the pick is narrowing. <b>A surviving filter says what it
    /// withheld</b> (trap 50) — and here the player did the withholding, so the sentence names
    /// the way back rather than apologising.
    ///
    /// <para>Empty at 0, which is both "nothing picked" and "picked only from the other
    /// section" — neither hid anything here, and a note claiming otherwise would be the
    /// control describing a state it is not in.</para>
    /// </summary>
    public static string HiddenNote(int hidden) => hidden <= 0
        ? ""
        : $"{hidden} more {(hidden == 1 ? "unlock is" : "unlocks are")} hidden by your pick. "
          + "Untick them all to see every one.";
}
