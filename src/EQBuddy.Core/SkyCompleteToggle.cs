namespace EQBuddy.Core;

/// <summary>
/// "I turned this Plane of Sky reward in" — restored 2026-08-18.
///
/// **This capability was lost, not deliberately dropped.** The widget's Sky card carried a
/// per-reward turn-in check; when the card became a launcher on 2026-08-16 and the tracker
/// was rebuilt around a list and a detail pane, the per-ITEM ticks survived and the
/// per-REWARD one did not. <see cref="AppSettings.SkyQuestCompleted"/> kept being READ —
/// by this layout, by both desktops and by EQBuddy Mobile — and after that day the only
/// thing that could WRITE it was the achievements import.
///
/// So a player who turned in a Sky reward and had no achievements export to paste had no
/// way to say so. Every piece ticked, the reward permanently "ready", and the Sky counter
/// never able to move past it. Holding the pieces and having handed them over are
/// different states, and telling them apart is the entire reason Sky groups by reward.
///
/// The rules are the old card's, kept verbatim because they were right:
///
///  * Turning in **acquires every item in the reward** — you had them, and then you did
///    not, and the checklist should not ask you to tick six boxes to record one turn-in.
///  * Turning in **resolves any parked auto-tick** (the <c>*</c> rows): the player
///    deciding IS the resolution, the same contract a manual tick has always had.
///  * Un-marking **reopens the reward and leaves the item boxes exactly as they are.**
///    The player knows what they still hold; silently clearing six ticks because they
///    corrected one mis-click would be the destructive half of a toggle.
///
/// It lived in UI.Shared beside <c>EpicCompleteToggle</c>, so all three surfaces got it from
/// one place — the asymmetry between the two checklists is what let this go missing in the
/// first place. **It is in Core since DRA-47** (Delivery 2 N3): the ticks it writes persist in
/// the per-character quest ledger now, and the reason it had to sit above Core went with the
/// per-profile store. It is also the ONE door for a catalog quest's completion
/// (<see cref="CompletedQuests"/> / <see cref="SetQuestCompleted"/>), which the Quests tab
/// used to decide for itself by a name pattern.
/// </summary>
public static class SkyCompleteToggle
{
    /// <summary>Mark a reward turned in. Idempotent: a second call changes nothing, which
    /// matters because the achievements import and a click can both arrive at it.
    ///
    /// <paramref name="ledger"/>/<paramref name="characterKey"/> are the ✔ that was
    /// promised (#241, PR 2): the log never records a hand-in, so this button IS the log,
    /// the same contract <see cref="QuestLedgerStore.RecordCompletion"/>'s own doc comment
    /// already states for the non-Sky quest detail's turn-in button. Optional so a caller
    /// (or a test) that only wants the checklist side effect can omit them; every SHIPPING
    /// call site passes both.</summary>
    public static void MarkTurnedIn(AppSettings settings, string rewardKey,
        IEnumerable<SkyQuestChecklistItem> rewardItems,
        QuestLedgerStore? ledger = null, string characterKey = "")
    {
        var items = rewardItems.ToList();
        // The settings half lives in Core (QuestChecklistLayout.MarkRewardTurnedIn) so the
        // inventory-driven auto-complete and this click share one definition of "turned in".
        // Its return is the transition INTO turned-in, and the consume below must fire only
        // then — a second call (achievements import racing a click, a re-render) would
        // consume the reward's items twice.
        var newlyTurnedIn = QuestChecklistLayout.MarkRewardTurnedIn(settings, rewardKey, items);
        if (newlyTurnedIn && ledger is not null && characterKey.Length > 0 && items.Count > 0)
            ledger.RecordCompletion(characterKey,
                SkyTestSplit.QuestName(items[0].ClassName, items[0].Reward),
                items.Select(i => new QuestItemNeed { Name = i.QuestItem }));
    }

    /// <summary>Reopen a reward. Deliberately does NOT untick its items — see the class
    /// note. A mis-click on the turn-in must cost one click to undo, not six.
    ///
    /// <para>With a <paramref name="ledger"/>, it also takes back the completion
    /// <see cref="MarkTurnedIn"/> recorded under the quest's NAME. Without that, the Quests
    /// tab — which reads the name — kept the reward completed after it was reopened, and
    /// un-marking a Sky test there was a click that visibly did nothing (DRA-47 found it
    /// moving this into Core). The items the turn-in consumed are not put back: that is the
    /// same "one click to undo, not six" rule, applied to the counts.</para></summary>
    public static void Reopen(AppSettings settings, string rewardKey,
        QuestLedgerStore? ledger = null, string characterKey = "")
    {
        settings.SkyQuestCompleted.RemoveAll(k =>
            k.Equals(rewardKey, StringComparison.OrdinalIgnoreCase));
        if (ledger is not null && characterKey.Length > 0 && SkyTestSplit.QuestNameFor(rewardKey) is { Length: > 0 } name)
            ledger.SetCompleted(characterKey, name, false);
    }

    /// <summary>
    /// A character's catalog-quest completions with its Sky turn-ins folded in — what the
    /// Quests tab and the phone's quest list read (it was <c>SkyTestSplit.WithTurnIns</c>,
    /// called by each of them). A Sky test's quest is complete when its reward is turned in,
    /// in the one store that says so; the ledger's own count wins where it has one, because
    /// a player who marked it there said something this cannot improve on.
    /// </summary>
    public static Dictionary<string, int> CompletedQuests(AppSettings settings,
        QuestLedgerStore? ledger, string characterKey)
    {
        var merged = ledger is not null && characterKey.Length > 0
            ? ledger.CompletedFor(characterKey)
            : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in settings.SkyQuestCompleted)
            if (SkyTestSplit.QuestNameFor(key) is { Length: > 0 } name)
                merged.TryAdd(name, 1);
        return merged;
    }

    /// <summary>
    /// Mark or unmark a catalog quest. A Plane of Sky test goes to the turn-in above — so it
    /// acquires its pieces and resolves any parked auto-tick exactly as the Sky tab's own
    /// button does — and anything else to the ledger's catch-up mark. One door, so the two
    /// tabs cannot disagree about which store a Sky test's completion lives in. The caller
    /// saves.
    /// </summary>
    public static void SetQuestCompleted(AppSettings settings, QuestLedgerStore? ledger,
        string characterKey, string questName, bool done)
    {
        var rewardKey = SkyTestSplit.RewardKeyFor(questName);
        if (rewardKey.Length == 0)
        {
            ledger?.SetCompleted(characterKey, questName, done);
            return;
        }
        if (done)
            MarkTurnedIn(settings, rewardKey, ItemsFor(settings.SkyQuestChecklist, rewardKey),
                ledger, characterKey);
        else
            Reopen(settings, rewardKey, ledger, characterKey);
    }

    /// <summary>The items a reward key names, so a caller can hand them to
    /// <see cref="MarkTurnedIn"/> without re-deriving the grouping the layout already
    /// does.</summary>
    public static List<SkyQuestChecklistItem> ItemsFor(
        IEnumerable<SkyQuestChecklistItem> all, string rewardKey) =>
    [
        .. all.Where(i => QuestChecklistLayout.RewardKey(i.ClassName, i.Reward)
            .Equals(rewardKey, StringComparison.OrdinalIgnoreCase)),
    ];

    public static bool IsTurnedIn(AppSettings settings, string rewardKey) =>
        settings.SkyQuestCompleted.Contains(rewardKey, StringComparer.OrdinalIgnoreCase);

    /// <summary>What the control says. A reward you hold every piece of invites the
    /// turn-in; one you have turned in offers the way back. The not-yet half is an
    /// IMPERATIVE and has to stay one — see <c>EpicCompleteToggle.LabelIsAnAct</c>
    /// for what the status-shaped version of this cost on the Epic band.</summary>
    public static string ButtonLabel(bool completed) =>
        completed ? "Reopen" : "Mark turned in";
}
