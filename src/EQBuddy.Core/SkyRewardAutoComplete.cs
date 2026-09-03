namespace EQBuddy.Core;

/// <summary>
/// "Your own bags already prove this Sky reward was turned in" — the inventory dump's
/// missing half (Hateborne, 2026-09-03: Ivory Mask sitting in the bank while the Sky tab
/// still listed it as ready to turn in).
///
/// The checklist tracks INGREDIENTS; the finished reward item was never consulted by
/// anything. But owning the reward is decisive evidence on its own, regardless of
/// ingredient state or how the class was unlocked: the game's own unlock criterion is
/// "Obtain X", and the only way the finished item exists in a player's bags or bank is
/// that the obtain happened — possibly long before EQBuddy was ever running, which is
/// exactly the case the log can never see and the #101 guard rightly refuses to take an
/// auto-granted achievement's word for.
///
/// Bags and bank BOTH count: a turned-in reward is a keepsake, and where it is stored
/// proves nothing either way. The mark goes through the same Core primitive a player's
/// own click uses (<see cref="QuestChecklistLayout.MarkRewardTurnedIn"/>), so an
/// inventory-triggered mark and a hand-click cannot define "turned in" two different
/// ways — and the player's way back is the same too (Reopen / right-click).
/// </summary>
public static class SkyRewardAutoComplete
{
    /// <summary>One reward the dump proves turned in. Class + reward, the same pair
    /// <see cref="QuestChecklistLayout.RewardKey"/> is built from.</summary>
    public sealed record Match(string ClassName, string Reward);

    /// <summary>
    /// Every not-yet-completed Sky reward whose finished item the dump shows the
    /// character owning. Already-completed keys are excluded HERE, so every match this
    /// returns is newly markable and a report can name the list directly.
    ///
    /// Matching is by the reward's name through <see cref="InventoryFile.Snapshot.CountOf"/>,
    /// which folds "+N" upgrade tiers onto the base name the checklist uses. An
    /// "(Exaltation)" aug copy deliberately does NOT match — <see cref="QuestCatalog.BaseItemName"/>
    /// never strips that suffix, so it is a structural non-match, not a guarded one.
    /// </summary>
    public static IReadOnlyList<Match> FindTurnedIn(
        InventoryFile.Snapshot? held,
        IEnumerable<SkyQuestChecklistItem> checklist,
        IReadOnlyCollection<string>? completedRewardKeys)
    {
        if (held is null) return [];
        var completed = new HashSet<string>(completedRewardKeys ?? [], StringComparer.OrdinalIgnoreCase);
        return
        [
            .. checklist
                .Select(i => (i.ClassName, i.Reward))
                .Distinct()
                .Where(g => g.Reward.Length > 0
                    && !completed.Contains(QuestChecklistLayout.RewardKey(g.ClassName, g.Reward))
                    && held.CountOf(g.Reward) > 0)
                .OrderBy(g => g.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Reward, StringComparer.OrdinalIgnoreCase)
                .Select(g => new Match(g.ClassName, g.Reward)),
        ];
    }

    /// <summary>Mark each match turned in. Returns how many were NEWLY marked (a repeat
    /// call is a no-op — the completed key makes <see cref="FindTurnedIn"/> skip it next
    /// time, and <see cref="QuestChecklistLayout.MarkRewardTurnedIn"/> is idempotent
    /// besides). The ledger completion is recorded only on the transition, the same
    /// contract the player's own turn-in button keeps.</summary>
    public static int Apply(IEnumerable<Match> matches, AppSettings settings,
        QuestLedgerStore? ledger = null, string characterKey = "")
    {
        var marked = 0;
        foreach (var m in matches)
        {
            var items = settings.SkyQuestChecklist
                .Where(i => i.ClassName.Equals(m.ClassName, StringComparison.OrdinalIgnoreCase)
                    && i.Reward.Equals(m.Reward, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var key = QuestChecklistLayout.RewardKey(m.ClassName, m.Reward);
            if (!QuestChecklistLayout.MarkRewardTurnedIn(settings, key, items)) continue;
            marked++;
            if (ledger is not null && characterKey.Length > 0 && items.Count > 0)
                ledger.RecordCompletion(characterKey,
                    SkyTestSplit.QuestName(items[0].ClassName, items[0].Reward),
                    items.Select(i => new QuestItemNeed { Name = i.QuestItem }));
        }
        return marked;
    }
}
