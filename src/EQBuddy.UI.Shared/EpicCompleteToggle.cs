using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// #138 (aodgizmo): the "Epic complete" master check flips a whole class's rows in one
/// click — and one stray click left unchecking every row by hand as the only way back.
/// The bulk check snapshots the state it overwrites so unchecking the master restores it.
/// This owns the state; the confirmation dialog stays in the views.
///
/// **Restored 2026-08-19, and it had been gone longer than the Sky one.** When the
/// widget's Epic card became a launcher (66f6abc) the master check went with it, and the
/// helpers below survived with tests and NO CALLER — <see cref="AppSettings.EpicQuestCompleted"/>
/// had a reader in <c>QuestChecklistView</c> that nothing called and a writer nowhere at
/// all. liminalwarmth found it by reading both sides of the move (#210) after #203 and
/// #205 reported the Sky half; the Sky turn-in was restored on 2026-08-18 and this was
/// still outstanding.
///
/// That is the same signature twice: **the data survived the move and the write path did
/// not**, which no test and no ratchet can see — a passing test suite over an unreachable
/// helper is exactly as green as one over a working feature. The lesson is in CLAUDE.md;
/// this file is the second entry in its evidence.
///
/// <see cref="SkyCompleteToggle"/> is the same job for the other checklist and the two
/// are deliberately shaped alike, because the ASYMMETRY between them is what let the Sky
/// turn-in go missing unnoticed for two days.
/// </summary>
public static class EpicCompleteToggle
{
    /// <summary>Mark a class's epic complete: every remaining row acquired, and a
    /// snapshot of what was already ticked so <see cref="Reopen"/> can put it back.
    /// Idempotent, like its Sky twin — a second call re-snapshots nothing new because
    /// every row is already acquired.</summary>
    public static void MarkComplete(AppSettings settings, string className,
        IReadOnlyList<EpicQuestChecklistItem> classItems)
    {
        if (IsComplete(settings, className)) return;
        settings.EpicQuestCompleted.Add(className);
        // BEFORE the bulk check, or the snapshot records the state it was meant to
        // preserve and the undo restores nothing.
        settings.EpicQuestPreCompleteAcquired[className] = Snapshot(classItems);
        CheckAll(classItems);
    }

    /// <summary>Reopen a class. Unlike the Sky turn-in — which deliberately leaves its
    /// item boxes alone — this DOES put the rows back, because the master check is what
    /// moved them: restoring the snapshot returns the player's own ticks rather than
    /// discarding them. A class completed before the snapshot existed has no entry and
    /// its rows are left as they are, which is the old fallback.</summary>
    public static void Reopen(AppSettings settings, string className)
    {
        settings.EpicQuestCompleted.RemoveAll(k =>
            k.Equals(className, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The half of <see cref="Reopen"/> that touches rows — separate because the
    /// caller holds the class's items and this needs the snapshot removed either way.
    /// Returns true when a snapshot was found and applied.</summary>
    public static bool RestoreFrom(AppSettings settings, string className,
        IReadOnlyList<EpicQuestChecklistItem> classItems)
    {
        var key = settings.EpicQuestPreCompleteAcquired.Keys
            .FirstOrDefault(k => k.Equals(className, StringComparison.OrdinalIgnoreCase));
        if (key is null) return false;
        var acquired = settings.EpicQuestPreCompleteAcquired[key];
        settings.EpicQuestPreCompleteAcquired.Remove(key);
        Restore(classItems, acquired);
        return true;
    }

    public static bool IsComplete(AppSettings settings, string className) =>
        settings.EpicQuestCompleted.Contains(className, StringComparer.OrdinalIgnoreCase);

    /// <summary>The class's own rows, honouring the classic-era lens the way the tracker
    /// does — the master check must flip exactly what the player can see, or it ticks
    /// rows that are not on screen.</summary>
    public static List<EpicQuestChecklistItem> ItemsFor(
        IEnumerable<EpicQuestChecklistItem> all, string className, bool classicOnly) =>
    [
        .. all.Where(i => i.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            .Where(i => !classicOnly || i.AvailableInClassic),
    ];

    /// <summary>What the control says, matching <see cref="SkyCompleteToggle.ButtonLabel"/>
    /// so the two checklists offer one vocabulary.
    ///
    /// **"Epic complete" until the Founder smoke of 2026-09-11.** A green primary button
    /// reading "Epic complete" is a STATUS, not an act: on a class band showing Bard 0/31
    /// and Warrior 3/30 it read as a badge claiming the epic WAS complete, next to a
    /// progress row saying it plainly was not. The label a control carries has to name
    /// what the CLICK does, because the button's colour cannot — and the green here is
    /// the primary-action green, which the not-yet state needs too. Sky's twin never had
    /// the bug because "Mark turned in" was already a verb; this is the same grammar
    /// (<see cref="LabelIsAnAct"/> now holds both to it).</summary>
    public static string ButtonLabel(bool completed) =>
        completed ? "Reopen" : "Mark as complete";

    /// <summary>The rule the 2026-09-11 smoke bought, executable: a checklist's
    /// not-yet-done master button says what the CLICK does. "Mark …" is an imperative a
    /// player reads as an offer; "Epic complete" / "Turned in" is a state a player reads
    /// as a verdict — and on a green button beside a 0/31 progress row, the wrong one of
    /// those two wins. Applied to both checklists by
    /// <c>EpicCompleteWritePathTests.NeitherMasterButtonNamesAStateItCannotBeIn</c>, which
    /// is why this predicate lives here rather than in the test: the next checklist to
    /// grow a master button gets the rule for free.</summary>
    public static bool LabelIsAnAct(string label) =>
        label.StartsWith("Mark ", StringComparison.Ordinal);

    /// <summary>The confirmation a view must ask before a bulk flip, or null when nothing
    /// would be overwritten — every row already ticked by hand means no dialog. The views
    /// own the dialog; the DECISION to show one is the same on every surface.</summary>
    public static string? ConfirmPrompt(string className,
        IReadOnlyList<EpicQuestChecklistItem> classItems)
    {
        var remaining = CountUnchecked(classItems);
        return remaining == 0 ? null
            : $"Mark all {remaining} remaining {className} "
              + (remaining == 1 ? "step" : "steps") + " complete?";
    }

    /// <summary>Rows the bulk check would actually flip — the number the confirmation
    /// names. Zero means nothing gets overwritten and no dialog is warranted.</summary>
    public static int CountUnchecked(IEnumerable<EpicQuestChecklistItem> items) =>
        items.Count(i => !i.Acquired);

    /// <summary>Capture BEFORE the bulk check: ids of the rows already acquired.
    /// Ids, not indexes — catalog refreshes reorder and reseed rows.</summary>
    public static List<string> Snapshot(IEnumerable<EpicQuestChecklistItem> items) =>
        [.. items.Where(i => i.Acquired).Select(i => i.Id)];

    public static void CheckAll(IEnumerable<EpicQuestChecklistItem> items)
    {
        // Marking the class complete IS the player deciding, so parked auto-ticks
        // (the * rows) stop being provisional — same contract as a manual toggle.
        foreach (var i in items) { i.Acquired = true; i.AcquiredUnassigned = false; }
    }

    /// <summary>Put every row back the way the snapshot saw it — the snapshot wins
    /// over any edit made since the bulk check (rows are disabled while complete, so
    /// only the bulk check itself can have moved them). A row the snapshot never saw
    /// (added by a catalog refresh in between) restores to unchecked: it was not
    /// acquired when the master was checked.</summary>
    public static void Restore(IEnumerable<EpicQuestChecklistItem> items, List<string> acquiredIds)
    {
        var acquired = new HashSet<string>(acquiredIds, StringComparer.Ordinal);
        // The snapshot stores ids only, so a restored tick comes back without its
        // provisional * — the tick is what matters; the auto-check never re-parks
        // a row that is already acquired.
        foreach (var i in items)
        {
            i.Acquired = acquired.Contains(i.Id);
            if (!i.Acquired) i.AcquiredUnassigned = false;
        }
    }
}
