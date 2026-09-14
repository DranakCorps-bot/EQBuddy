namespace EQBuddy.Core;

/// <summary>
/// **WHICH UNLOCKS THIS CHARACTER IS WORKING ON — ONE STORE, TWO SURFACES** (DRA-71 D5,
/// Fable plan P11; Founder smoke item 7: *"unlock races AND classes, multi-select, on the
/// Helper and on the Quests tab"*).
///
/// <para>Until this slice there was no player selection over unlocks anywhere: the Quests
/// window's Unlocks tab listed every race and every class, and the Helper auto-picked the
/// top few by how close they were to done. Both answers are fine for someone browsing and
/// wrong for someone with a plan — "I am unlocking Iksar and Necromancer" is a standing
/// intent, and a surface that cannot be told it is a surface you re-scan every visit.</para>
///
/// <para><b>Writer and reader land in the same slice, on purpose</b> (trap 20): a setting
/// only READERS touch is the signature of a lost capability and it has cost this repo three
/// player-facing bugs. Both halves are here so neither can be folded away without the other
/// going with it — and it writes through <see cref="AppSettings"/> itself rather than taking
/// the dictionary by reference, which is what keeps it visible to <c>DeadSettingTests</c>'
/// scan (four entries on that test's known-list are there only because they are written the
/// other way).</para>
///
/// <para><b>It is a FILTER and not a required pick</b> — see
/// <see cref="AppSettings.UnlockPicks"/> for why this differs from the faction picker beside
/// it in the same room.</para>
/// </summary>
public static class UnlockPickStore
{
    /// <summary>
    /// The unlocks this character has picked, in the dump's own spelling, or EMPTY when they
    /// have never picked any.
    ///
    /// <para>Empty is the "show all of them" state and not a mistake. Nothing is validated
    /// against the dump here: the dump is read lazily off disk and may not have arrived yet,
    /// and a reader that dropped unknown names would quietly forget a pick every time the
    /// player launched before the game wrote the file. <see cref="Narrow"/> is where a name
    /// nobody recognises stops mattering, by matching nothing.</para>
    /// </summary>
    public static IReadOnlyList<string> Picked(AppSettings settings, string characterKey) =>
        settings is not null && !string.IsNullOrEmpty(characterKey)
        && settings.UnlockPicks.TryGetValue(characterKey, out var picked)
            ? picked
            : [];

    /// <summary>Is this subject picked? Case-insensitive, because the two files that spell an
    /// unlock's name — the achievements dump and this profile — are written by different
    /// programs.</summary>
    public static bool IsPicked(IReadOnlyList<string> picked, string subject) =>
        picked is { Count: > 0 } && subject is { Length: > 0 }
        && picked.Contains(subject, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Turn one unlock on or off.
    ///
    /// <para>An empty result REMOVES the key rather than storing an empty list, the same rule
    /// <c>HelperGoalStore.Toggle</c> follows and for the same reason: "never picked" and
    /// "unticked the last one" are one state here — both mean show everything — and two
    /// spellings of one state is a distinction a later reader would eventually act on.</para>
    /// </summary>
    public static void Toggle(AppSettings settings, string characterKey, string subject)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)
            || string.IsNullOrWhiteSpace(subject)) return;
        var picked = Picked(settings, characterKey).ToList();
        if (picked.RemoveAll(s => s.Equals(subject, StringComparison.OrdinalIgnoreCase)) == 0)
            picked.Add(subject);
        if (picked.Count == 0) settings.UnlockPicks.Remove(characterKey);
        else settings.UnlockPicks[characterKey] = picked;
    }

    /// <summary>
    /// **THE FILTER, AND IT IS APPLIED PER SECTION.**
    ///
    /// <para>One list holds both races and classes (they share no names), so this is handed
    /// one section's unlocks at a time and keeps the ones the pick names. <b>A pick that
    /// names nothing in this section narrows nothing</b> — which is the whole reason this is
    /// a method and not a <c>Where</c> at two call sites. A player working on Iksar and
    /// nothing else has picked no class; emptying the Classes half would be the pick
    /// answering a question it was never asked, and the Unlocks tab would lose a section
    /// with no control on screen able to explain it.</para>
    ///
    /// <para>It returns the SAME list object when it narrows nothing, so a caller can compare
    /// counts to know whether the pick did anything without re-deciding the rule.</para>
    /// </summary>
    public static IReadOnlyList<UnlockProgress> Narrow(
        IReadOnlyList<UnlockProgress> unlocks, IReadOnlyList<string> picked)
    {
        if (unlocks is not { Count: > 0 } || picked is not { Count: > 0 }) return unlocks ?? [];
        var kept = unlocks.Where(u => IsPicked(picked, u.Subject)).ToList();
        return kept.Count > 0 ? kept : unlocks;
    }

    /// <summary>How many of this section's unlocks the pick is hiding — 0 when it narrowed
    /// nothing, which is both "nothing picked" and "picked something from the other section".
    /// A surviving filter SAYS what it withheld (trap 50); this is the number it says it
    /// with.</summary>
    public static int Hidden(IReadOnlyList<UnlockProgress> unlocks, IReadOnlyList<string> picked) =>
        Math.Max(0, (unlocks?.Count ?? 0) - Narrow(unlocks ?? [], picked).Count);
}
