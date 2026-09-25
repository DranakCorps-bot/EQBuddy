namespace EQBuddy.Core;

/// <summary>
/// Brings the Sky and Epic checklists up to date with what the quest ledger just took in
/// and let go (<see cref="QuestLedgerFeed"/>): new loot ticks rows, and an item leaving
/// takes back any Sky guess the lower count no longer covers.
///
/// **Keyed on the ledger, never on session totals** (Hateborne, 2026-09-18). The widget
/// used to diff the snapshot's loot against a high-water mark held in RAM, cleared on
/// every launch, session start, character switch and review - while the log watcher
/// re-reads the whole file each time. Every launch re-offered the same loot line, and rule
/// 3 of <see cref="SkyLootAutoCheck"/> parked one more * on the next class's row: one Wind
/// Rune Meda walked down six classes. The ledger's time gate is persisted, so a replayed
/// line bounces there and never reaches this.
///
/// In Core rather than in the widget because it is a rule, and the WPF layer has no tests.
/// </summary>
public static class ChecklistLedgerSync
{
    /// <param name="myClasses">The character's quest-filter classes (the class picker).</param>
    /// <param name="held">Ledger total for an item (base name), or null when unknown -
    /// read AFTER the delta landed, so it already counts the loot and the exits.</param>
    /// <param name="offDump">Whether no dump can vouch for the item's count (currency, the
    /// depot - <see cref="QuestLedgerStore.IsOffDump(string, string)"/>). Such an item takes back one guess
    /// per copy that left rather than reconciling to a count that may be stale.</param>
    /// <returns>True when a checklist changed and the settings need saving.</returns>
    public static bool Apply(AppSettings settings, QuestLedgerDelta delta,
        IReadOnlyList<string> myClasses, Func<string, int?> held, Func<string, bool> offDump)
    {
        if (delta.IsEmpty) return false;
        var changed = false;
        // The class-scoping rules are the auto-checks' own (#98 shared items tick your
        // classes, #106 single-class items tick their class, #121 Epic steps key on the
        // catalog items their text mentions).
        foreach (var (item, count) in delta.Gained)
        {
            changed |= SkyLootAutoCheck.Apply(settings.SkyQuestChecklist, item, count,
                myClasses, settings.SkyQuestClass);
            changed |= EpicLootAutoCheck.Apply(settings.EpicQuestChecklist, item, count,
                myClasses, settings.EpicQuestClass);
        }
        foreach (var (item, count) in delta.Lost)
            changed |= (offDump(item)
                ? SkyGuessReconcile.TakeBack(settings.SkyQuestChecklist, settings.SkyQuestCompleted, item, count)
                : SkyGuessReconcile.Apply(settings.SkyQuestChecklist, settings.SkyQuestCompleted, held, [item]))
                .Count > 0;
        return changed;
    }
}
