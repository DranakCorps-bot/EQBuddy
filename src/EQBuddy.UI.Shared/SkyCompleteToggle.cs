using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

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
/// It lives here, framework-free and beside <see cref="EpicCompleteToggle"/>, so all three
/// surfaces get it from one place — the asymmetry between the two checklists is what let
/// this go missing in the first place.
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
    /// note. A mis-click on the turn-in must cost one click to undo, not six.</summary>
    public static void Reopen(AppSettings settings, string rewardKey) =>
        settings.SkyQuestCompleted.RemoveAll(k =>
            k.Equals(rewardKey, StringComparison.OrdinalIgnoreCase));

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
    /// turn-in; one you have turned in offers the way back.</summary>
    public static string ButtonLabel(bool completed) =>
        completed ? "Reopen" : "Mark turned in";
}
