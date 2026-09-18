namespace EQBuddy.Core;

/// <summary>
/// Takes back the Sky checklist's own GUESSES when the count of what the character holds
/// no longer covers them (Hateborne, 2026-09-18: High Quality Raiment and Wind Rune Meda
/// ticked on the Berserker, Ranger and Shaman rows with none of either held).
///
/// A guess is a tick <see cref="SkyLootAutoCheck"/> PLACED under its rule 3 - several
/// classes want the item and none passed the class lens - and it wears a * for exactly
/// that reason. Nothing ever took one back: a hand-in, sale or destroy left it ticked
/// forever, and until the loot auto-tick keyed on the ledger's persisted gate the launch
/// replay parked a fresh one on the next class's row every restart.
///
/// **Only guesses are touched.** A tick the player made, one on a class they picked, or
/// one an import proved is a statement this has no business second-guessing; the *
/// is EQBuddy saying "I wasn't sure", and this is it being honest when it turns out
/// wrong. Ticks on rewards already turned in are left alone too - those items were
/// consumed by the hand-in, which is the tick's whole meaning there.
/// </summary>
public static class SkyGuessReconcile
{
    /// <summary>
    /// Untick the guesses <paramref name="held"/> no longer covers.
    ///
    /// Per item, on OPEN rewards: the non-guess ticks claim held copies first, and the
    /// guesses share what is left in list order - the last-listed are the ones taken back,
    /// matching the order rule 3 parked them in.
    /// </summary>
    /// <param name="held">How many of an item (base name) the character holds, or null
    /// when that is not known - an unknown count leaves the item's guesses alone.</param>
    /// <param name="onlyItems">Limit the pass to these items (a hand-in names what left);
    /// null checks every item.</param>
    /// <returns>The rows unticked, for a report and an Undo.</returns>
    public static IReadOnlyList<SkyQuestChecklistItem> Apply(
        IEnumerable<SkyQuestChecklistItem> checklist,
        IReadOnlyCollection<string> completedRewardKeys,
        Func<string, int?> held,
        IEnumerable<string>? onlyItems = null)
    {
        var completed = new HashSet<string>(completedRewardKeys, StringComparer.OrdinalIgnoreCase);
        var only = onlyItems is null ? null
            : new HashSet<string>(onlyItems.Select(QuestCatalog.BaseItemName), StringComparer.OrdinalIgnoreCase);
        var cleared = new List<SkyQuestChecklistItem>();

        foreach (var item in checklist
                     .Where(i => i.Acquired
                         && !completed.Contains(QuestChecklistLayout.RewardKey(i.ClassName, i.Reward)))
                     .GroupBy(i => QuestCatalog.BaseItemName(i.QuestItem), StringComparer.OrdinalIgnoreCase))
        {
            if (only is not null && !only.Contains(item.Key)) continue;
            var guesses = item.Where(i => i.AcquiredUnassigned).ToList();
            if (guesses.Count == 0 || held(item.Key) is not { } have) continue;

            var allowance = Math.Max(0, have - item.Count(i => !i.AcquiredUnassigned));
            foreach (var guess in guesses.Skip(allowance))
            {
                guess.Acquired = false;
                guess.AcquiredUnassigned = false;
                cleared.Add(guess);
            }
        }
        return cleared;
    }

    /// <summary>
    /// Take back up to <paramref name="count"/> guesses for one item - the rule for an item
    /// whose held count cannot be trusted, which is every currency item: no dump can see
    /// one, and every scan made before EQBuddy knew that recorded the player's runes as zero
    /// (<see cref="CurrencyItems"/>). A logged hand-in of N copies is still certain evidence
    /// that N copies left, so it takes back N guesses and never reconciles to a count.
    /// Last-listed first, like <see cref="Apply"/>.
    /// </summary>
    public static IReadOnlyList<SkyQuestChecklistItem> TakeBack(
        IEnumerable<SkyQuestChecklistItem> checklist,
        IReadOnlyCollection<string> completedRewardKeys, string item, int count)
    {
        var completed = new HashSet<string>(completedRewardKeys, StringComparer.OrdinalIgnoreCase);
        var name = QuestCatalog.BaseItemName(item);
        var taken = checklist
            .Where(i => i.Acquired && i.AcquiredUnassigned
                && !completed.Contains(QuestChecklistLayout.RewardKey(i.ClassName, i.Reward))
                && QuestCatalog.BaseItemName(i.QuestItem).Equals(name, StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .Take(Math.Max(0, count))
            .ToList();
        foreach (var guess in taken)
        {
            guess.Acquired = false;
            guess.AcquiredUnassigned = false;
        }
        return taken;
    }

    /// <summary>Put back exactly the guesses <see cref="Apply"/> took, still marked as
    /// guesses. By row id against the CURRENT checklist, the same contract the import
    /// Undos keep - a catalog refresh may have replaced the row objects since.</summary>
    public static void Undo(IEnumerable<SkyQuestChecklistItem> checklist, IReadOnlyCollection<string> clearedIds)
    {
        var ids = new HashSet<string>(clearedIds, StringComparer.Ordinal);
        foreach (var row in checklist.Where(r => ids.Contains(r.Id)))
        {
            row.Acquired = true;
            row.AcquiredUnassigned = true;
        }
    }

    /// <summary>"Class · Item" - how a report names a taken-back guess.</summary>
    public static string Describe(SkyQuestChecklistItem row) => row.ClassName + " · " + row.QuestItem;
}
