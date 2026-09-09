namespace EQBuddy.Core;

/// <summary>Which store owns one objective's done-ness.</summary>
public enum GuideProgressWriter
{
    /// <summary>The per-character guide ledger (<see cref="QuestLedgerStore.GuideProgress"/>).</summary>
    GuideLedger,
    /// <summary>The existing Plane of Sky turn-in store — <c>AppSettings.SkyQuestCompleted</c>,
    /// through <c>SkyCompleteToggle</c> / <c>QuestChecklistLayout.MarkRewardTurnedIn</c>.</summary>
    SkyTurnIn,
}

/// <summary>
/// <b>One fact, one store</b> — the routing rule for guide progress, in one place because two
/// callers deciding it separately is how the same tick ends up in two files (trap 4).
///
/// <para>An objective carrying a <see cref="GuideObjective.RewardKey"/> IS a Sky turn-in. That
/// fact already has a store and four writers — the classic checklist, the Evolved shell,
/// EQBuddy Mobile and the achievements import — so the guide layer reads it from there and
/// writes it back through there. Everything else is a step only the guide knows about, and
/// that is the ledger's. The rule is a one-liner; what earns it a type is that BOTH the write
/// side (<see cref="QuestLedgerStore.SetGuideObjectiveDone"/> refuses what it does not own)
/// and the read side (<see cref="IsDone"/>) go through the same answer.</para>
///
/// <para><b>The read side is the one that survives a hand edit.</b> <see cref="IsDone"/>
/// routes on the reward key, not on what the ledger happens to contain — so a done id that
/// somehow reached a reward objective's list (an edited <c>quest-ledger.json</c>, a caller
/// that ignored the refusal) still cannot make a turn-in look done. The store's copy is not
/// merely discouraged; it is unreadable.</para>
///
/// <para>Nothing here is presentation. Whether a row reads Open, Ready or Done, and which
/// objective the active-step card leads with, is <c>QuestPresentation</c>/
/// <c>GuidePresentation</c>'s job (plan §5) — this answers only "is it done", "is it skipped"
/// and "who writes it".</para>
/// </summary>
public static class GuideProgressRouting
{
    /// <summary>Where this objective's done-ness lives.</summary>
    public static GuideProgressWriter WriterFor(GuideObjective objective) =>
        objective.RewardKey.Length > 0 ? GuideProgressWriter.SkyTurnIn : GuideProgressWriter.GuideLedger;

    /// <summary>Is this step done for this character? <paramref name="rewardTurnedIn"/> answers
    /// for the Sky store (<c>SkyCompleteToggle.IsTurnedIn</c> at every shipping call site) and
    /// is asked ONLY for a reward-keyed objective, so a caller with no settings to hand can
    /// pass a predicate that always says no for a guide that has no turn-in.</summary>
    public static bool IsDone(GuideObjective objective, QuestLedgerStore.GuideProgress progress,
        Func<string, bool> rewardTurnedIn) =>
        WriterFor(objective) == GuideProgressWriter.SkyTurnIn
            ? rewardTurnedIn(objective.RewardKey)
            : progress.DoneObjectiveIds.Contains(objective.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Has the player folded this step away? Always the ledger's answer — a skip has
    /// no other home (see <see cref="QuestLedgerStore.SetGuideObjectiveSkipped"/>).</summary>
    public static bool IsSkipped(GuideObjective objective, QuestLedgerStore.GuideProgress progress) =>
        progress.SkippedObjectiveIds.Contains(objective.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>How many of a guide's objectives this character has done — the PROGRESS axis,
    /// which has nothing to do with <see cref="Guide.StubCount"/>, the AUTHORING axis. A guide
    /// can be finished while half of it is stubs, and fully authored at zero (plan §2). A skip
    /// is not a done; it counts as neither.</summary>
    public static int DoneCount(Guide guide, QuestLedgerStore.GuideProgress progress,
        Func<string, bool> rewardTurnedIn) =>
        guide.AllObjectives.Count(o => IsDone(o, progress, rewardTurnedIn));
}
