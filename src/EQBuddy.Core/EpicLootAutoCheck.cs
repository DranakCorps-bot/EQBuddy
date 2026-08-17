namespace EQBuddy.Core;

/// <summary>
/// The Epic checklist's loot auto-tick (#121): SkyLootAutoCheck's layered rules applied
/// to prose steps. A step carries the catalog items its text mentions (ItemNames,
/// resolved in EpicQuestDefaults); looting one of them ticks the step. Same three
/// layers as Sky:
///
/// 1. SHARED items (Red Dragon Scales feed both the Bard and Warrior epics) tick only
///    the player's own classes — the quest tracker's class filter, or the active Epic
///    tab when no filter is set. One physical drop cannot honestly tick two epics the
///    player doesn't work.
/// 2. UNAMBIGUOUS items — mentioned by exactly ONE class's checklist — tick that class
///    no matter what tab or filter is active ("loot outranks the class lens").
/// 3. MULTI-CLASS items where NO owning class passes the lens: the item was physically
///    looted, so losing it to tracking is worse than guessing — the FIRST owning
///    class gets the tick, flagged AcquiredUnassigned so the row wears a * and the
///    player can move it (the Sky #106 contract, verbatim).
///
/// Two epic-specific wrinkles: the looted name folds its +N upgrade tier off first
/// (QuestCatalog.BaseItemName — ItemNames come from the catalog, which stores base
/// names), and within a class the EARLIEST open step gets the tick — "loot X" precedes
/// "give X to …" in every walkthrough, and one drop is progress on the loot step, not
/// the turn-in.
/// </summary>
public static class EpicLootAutoCheck
{
    /// <summary>Ticks up to <paramref name="newlyLooted"/> open steps per eligible
    /// class mentioning <paramref name="itemName"/>. Returns true if anything ticked.</summary>
    public static bool Apply(IReadOnlyList<EpicQuestChecklistItem> checklist, string itemName,
        int newlyLooted, IReadOnlyList<string> myClasses, string activeTab)
    {
        if (newlyLooted <= 0) return false;
        var looted = QuestCatalog.BaseItemName(itemName);

        // NO lens at all means no class passes — NOT every class passes. Same defect and
        // same reasoning as SkyLootAutoCheck (#193): an empty tab was a wildcard, and
        // AppSettings.EpicQuestClass has had no writer since the 2026-08-16
        // consolidation deleted the widget's Epic card.
        bool ClassTicks(string className) => myClasses.Count > 0
            ? myClasses.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase))
            : activeTab.Length > 0 && string.Equals(className, activeTab, StringComparison.Ordinal);

        var slots = checklist
            .Where(i => i.ItemNames.Any(n => n.Equals(looted, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var owningClasses = slots.Select(i => i.ClassName)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var changed = false;
        foreach (var classGroup in slots
                     .Where(i => !i.Acquired && (ClassTicks(i.ClassName) || owningClasses.Count == 1))
                     .GroupBy(i => i.ClassName))
            foreach (var item in classGroup.OrderBy(i => i.Order).Take(newlyLooted))
            {
                item.Acquired = true;
                item.AcquiredUnassigned = false;
                changed = true;
            }

        // Rule 3: several classes mention it and NONE of them passes the lens — park
        // the tick on the first owning class's earliest open step, flagged for the
        // player to move. The explicit no-lens-match test matters (Sky's 2026-08-13
        // review): when a lens-passing class simply has its steps done, "nothing
        // ticked" must mean nothing ticked — not a guessed tick on a class the
        // player doesn't play.
        if (!changed && owningClasses.Count > 1 && !owningClasses.Any(ClassTicks))
            foreach (var item in slots.Where(i => !i.Acquired).Take(newlyLooted))
            {
                item.Acquired = true;
                item.AcquiredUnassigned = true;
                changed = true;
            }
        return changed;
    }
}
