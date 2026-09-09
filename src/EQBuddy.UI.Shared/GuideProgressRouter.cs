using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>Where an objective's "done" actually lives.</summary>
public enum GuideProgressHome
{
    /// <summary>The per-character guide ledger (<see cref="QuestLedgerStore.GuideProgress"/>) —
    /// the only home for a step nothing else in EQBuddy has an opinion about.</summary>
    GuideLedger,

    /// <summary>The Sky turn-in store the classic checklist, the phone and the achievements
    /// import have always shared (<c>AppSettings.SkyQuestCompleted</c>).</summary>
    SkyTurnIn,
}

/// <summary>
/// The one door between a guide's objective rows and the two stores behind them
/// (Fable plan §4, P1b).
///
/// <para><b>Why a router at all.</b> A guide objective that carries a <c>RewardKey</c> is not
/// a new fact — it is the Plane of Sky turn-in the checklist has tracked since 1.x, wearing a
/// guide row's clothes. If the guide kept its own tick for it there would be two sources for
/// one fact (trap 4), and the visible cost is specific: turn a reward in on the phone, open
/// the guide on the desktop, and the guide still asks you to do it. So the rule is
/// <b>read FROM and write THROUGH the existing path</b> — <see cref="SkyCompleteToggle"/>,
/// the same call a click on the classic checklist makes, consuming the reward's items in the
/// quest ledger exactly once — and <see cref="QuestLedgerStore.GuideProgress"/> never holds a
/// reward objective's done id.</para>
///
/// <para><b>Skip is the asymmetry, and it is deliberate.</b> "I am not doing this step" has no
/// home in the Sky store and is a different statement from "I turned this in", so it lands in
/// the guide ledger for EVERY objective. One fact, one store, still — just a different fact.
/// A skipped reward objective can perfectly well be turned in later; the two answers do not
/// contradict across stores the way done/skipped do inside one.</para>
///
/// <para><b>Nothing here infers.</b> At MVP there is no log-driven guide progress: every verb
/// on this class begins with a player's click. <c>ItemNames</c> exists on the objective for a
/// Phase-5 auto-tick and is not read by anything yet — see the plan's §4 rejected list.</para>
///
/// <para>Framework-free and in UI.Shared rather than Core because the write side needs
/// <see cref="SkyCompleteToggle"/>, which is where the "turning in acquires every item in the
/// reward" rule lives. Desktop, shell and phone all call this — parity by shared module.</para>
/// </summary>
public static class GuideProgressRouter
{
    /// <summary>Which store owns this objective's done state. The whole routing decision,
    /// pure and separately testable: a reward key means the Sky store owns it.</summary>
    public static GuideProgressHome HomeFor(GuideObjective objective) =>
        objective.RewardKey.Length > 0 ? GuideProgressHome.SkyTurnIn : GuideProgressHome.GuideLedger;

    /// <summary>Has the player done this objective? Reward objectives answer from the Sky
    /// turn-in store, so a turn-in recorded anywhere — the classic checklist, the phone, the
    /// achievements import, the inventory-driven auto-complete — reads as done inside the
    /// guide the same tick.</summary>
    public static bool IsDone(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective) =>
        HomeFor(objective) == GuideProgressHome.SkyTurnIn
            ? SkyCompleteToggle.IsTurnedIn(settings, objective.RewardKey)
            : ledger.GuideProgressFor(characterKey, guideId).DoneObjectiveIds
                .Contains(objective.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Tick or untick an objective, into whichever store owns it.
    ///
    /// <para>The Sky path is <see cref="SkyCompleteToggle.MarkTurnedIn"/> / <see
    /// cref="SkyCompleteToggle.Reopen"/> verbatim — including the quest ledger's completion
    /// record and the item consume, which is why <paramref name="ledger"/> and
    /// <paramref name="characterKey"/> are handed over on both paths. Un-ticking a reward
    /// reopens it and leaves its item boxes alone, exactly as a mis-click on the classic
    /// checklist does; that asymmetry is the toggle's, not this class's.</para>
    ///
    /// <para>Ticking clears a skip on the same objective, whichever store the tick went to —
    /// the guide row cannot show struck-out and done at once.</para></summary>
    public static void SetDone(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective, bool done)
    {
        if (HomeFor(objective) == GuideProgressHome.SkyTurnIn)
        {
            if (done)
                SkyCompleteToggle.MarkTurnedIn(settings, objective.RewardKey,
                    SkyCompleteToggle.ItemsFor(settings.SkyQuestChecklist, objective.RewardKey),
                    ledger, characterKey);
            else
                SkyCompleteToggle.Reopen(settings, objective.RewardKey);

            // The done fact stayed in the Sky store; only the contradicting skip is ours to
            // clear, and only when there is one — an untouched guide must not gain a row
            // because a reward was turned in on another screen.
            if (done && IsSkipped(ledger, characterKey, guideId, objective))
                ledger.SetObjectiveSkipped(characterKey, guideId, objective.Id, false);
            return;
        }

        ledger.SetObjectiveDone(characterKey, guideId, objective.Id, done);
    }

    /// <summary>Is this objective struck out? Always the guide ledger — see the class
    /// note.</summary>
    public static bool IsSkipped(QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective) =>
        ledger.GuideProgressFor(characterKey, guideId).SkippedObjectiveIds
            .Contains(objective.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Strike an objective out, or take the strike back. Striking out a NON-reward
    /// objective clears its tick (the store does that); striking out a reward objective
    /// leaves the turn-in alone, because a turn-in that happened is a fact about the past and
    /// a skip is a statement about the future.</summary>
    public static void SetSkipped(QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective, bool skipped) =>
        ledger.SetObjectiveSkipped(characterKey, guideId, objective.Id, skipped);

    /// <summary>How far through a guide the player is: objectives done, objectives skipped,
    /// and the total. Counts through <see cref="IsDone"/>, so the Sky-owned rows count from
    /// the Sky store rather than from a copy that could disagree with it.</summary>
    public static GuideProgressCounts Counts(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, Guide guide)
    {
        var done = 0;
        var skipped = 0;
        var total = 0;
        foreach (var objective in guide.AllObjectives)
        {
            total++;
            if (IsDone(settings, ledger, characterKey, guide.Id, objective)) done++;
            else if (IsSkipped(ledger, characterKey, guide.Id, objective)) skipped++;
        }
        return new GuideProgressCounts(done, skipped, total);
    }
}

/// <summary>A guide's progress in three numbers. <paramref name="Skipped"/> excludes anything
/// already counted as <paramref name="Done"/>, so the two never double-count a row and
/// Done + Skipped ≤ Total.</summary>
public readonly record struct GuideProgressCounts(int Done, int Skipped, int Total)
{
    /// <summary>Rows still asking something of the player.</summary>
    public int Remaining => Total - Done - Skipped;
}
