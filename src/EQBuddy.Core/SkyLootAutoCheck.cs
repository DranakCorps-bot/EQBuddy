namespace EQBuddy.Core;

/// <summary>
/// The Sky checklist's loot auto-tick, extracted from the widget so the class-scoping
/// rules are testable. Two rules, layered:
///
/// 1. SHARED items (five classes want a Wind Rune Azia) tick only the player's own
///    classes — the quest tracker's class filter, or the active Sky tab when no
///    filter is set (#98, bjstrange). One physical rune cannot honestly tick five
///    class plans the player doesn't play.
/// 2. UNAMBIGUOUS items — wanted by exactly ONE class in the whole checklist — tick
///    that class no matter what tab or filter is active (#106, bjstrange again: a
///    Berserker-only staff looted on the Druid tab can't be for anyone else's test).
///    Same philosophy as the tracker's "loot outranks the class lens".
/// 3. MULTI-CLASS items where NO owning class passes the lens (#106 round two, the
///    staff both Berserker and Necromancer tests want): the item was physically
///    looted, so losing it to tracking is worse than guessing — the FIRST owning
///    class gets the tick, flagged AcquiredUnassigned so the row wears a * and the
///    player can move it ("check one of them off, doesn't matter which, and let me
///    decide if it's the right one" — his words, his rule).
///
/// Names are folded through <see cref="QuestCatalog.BaseItemName"/> on BOTH sides, so
/// an upgraded drop ticks the row for its base item ("Azure Ring +1" is an Azure Ring).
/// This checker matched raw strings until 1.85.0 while its Epic and Gear siblings both
/// folded — so the same loot line ticked a gear wish and an epic step and silently
/// skipped Sky (#156, bjstrange).
/// </summary>
public static class SkyLootAutoCheck
{
    /// <summary>Ticks up to <paramref name="newlyLooted"/> unacquired slots per
    /// eligible class for <paramref name="itemName"/>. Returns true if anything ticked.</summary>
    public static bool Apply(IReadOnlyList<SkyQuestChecklistItem> checklist, string itemName,
        int newlyLooted, IReadOnlyList<string> myClasses, string activeTab)
    {
        if (newlyLooted <= 0) return false;

        // NO lens at all means no class passes — NOT every class passes (#193, wizen:
        // "it shows me as having looted a bunch of things I did not loot"). This read
        // `activeTab.Length == 0 || …`, so an empty tab was a wildcard, and one looted
        // Wind Rune Azia ticked a slot for all five classes that want it.
        //
        // It was survivable while the widget's Sky card kept activeTab populated. The
        // 2026-08-16 consolidation deleted that card and nothing has written
        // AppSettings.SkyQuestClass since — the only assignment left in the codebase is
        // its own `= ""` initializer — so for any player who had not set classes in the
        // ⚙ picker the wildcard was permanently on.
        //
        // Falling through to rule 3 is the honest answer: the item WAS looted, so park
        // one tick and flag it for the player to move, rather than invent sixteen.
        bool ClassTicks(string className) => myClasses.Count > 0
            ? myClasses.Any(c => c.Equals(className, StringComparison.OrdinalIgnoreCase))
            : activeTab.Length > 0 && string.Equals(className, activeTab, StringComparison.Ordinal);

        var looted = QuestCatalog.BaseItemName(itemName);
        var slots = checklist
            .Where(i => string.Equals(QuestCatalog.BaseItemName(i.QuestItem), looted,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var owningClasses = slots.Select(i => i.ClassName)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var changed = false;
        foreach (var classGroup in slots
                     .Where(i => !i.Acquired && (ClassTicks(i.ClassName) || owningClasses.Count == 1))
                     .GroupBy(i => i.ClassName))
            foreach (var item in classGroup.Take(newlyLooted))
            {
                item.Acquired = true;
                item.AcquiredUnassigned = false;
                changed = true;
            }

        // Rule 3: several classes want it and NONE of them passes the lens — park
        // the tick on the first owning class's open slot, flagged for the player to
        // move. The explicit no-lens-match test matters (2026-08-13 review): when a
        // lens-passing class simply has its slots full, "nothing ticked" must mean
        // nothing ticked — not a guessed tick on a class the player doesn't play.
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
