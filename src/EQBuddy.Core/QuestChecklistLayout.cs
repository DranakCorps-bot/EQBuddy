namespace EQBuddy.Core;

/// <summary>One row of an Epic or Plane of Sky checklist, as any UI should draw it.
/// <see cref="Detail"/> already carries the turn-in NPC and the drop location, because
/// "where does this come from" is the question a checklist row exists to answer.</summary>
public sealed record QuestChecklistRow(
    string Id,
    string ClassName,
    string Title,
    string Detail,
    bool Acquired,
    bool Unassigned);

/// <summary>A group of rows under one heading, with the state of the reward as a whole.</summary>
public sealed record QuestChecklistGroup(
    string ClassName,
    string Heading,
    string? Note,
    IReadOnlyList<QuestChecklistRow> Rows)
{
    public int Done => Rows.Count(r => r.Acquired);
    public int Total => Rows.Count;
}

/// <summary>
/// How the Epic and Sky checklists are grouped and labelled — for every surface.
///
/// This exists because the three surfaces had already drifted (#184, bjstrange). The
/// 2026-08-16 rewrite that folded the widget's Epic and Sky cards into the Quest Tracker
/// re-grouped Sky by turn-in NPC, which puts every reward that one NPC hands out into a
/// single undifferentiated list — so "which pieces does THIS reward still need" stopped
/// being answerable, and the drop location stopped being drawn at all even though it was
/// sitting in <see cref="SkyQuestChecklistItem.Source"/> the whole time. EQBuddy Mobile
/// kept grouping by reward and kept showing the unassigned mark; the two desktop UIs
/// showed neither. Core's own comments promised "the row wears a *" and only the phone
/// was telling the truth.
///
/// Pure, so it is unit-tested rather than eyeballed, and shared, so a fix cannot reach
/// one window and miss the other two — the lesson #122 and #152 already charged us for.
/// </summary>
public static class QuestChecklistLayout
{
    /// <summary>The mark on a tick the loot auto-checker placed itself because several
    /// classes want the same item and it could not tell which of them earned it. The
    /// player moving it is the resolution; any manual toggle clears the flag.</summary>
    public const string UnassignedMark = " *";

    /// <summary>Sky, grouped by the REWARD you are working toward — the unit of "am I
    /// done", and the unit the player turns in. The NPC is still shown, on every row,
    /// where it belongs next to the drop location.</summary>
    public static IReadOnlyList<QuestChecklistGroup> Sky(
        IEnumerable<SkyQuestChecklistItem> items,
        IReadOnlyCollection<string>? completedRewardKeys = null)
    {
        var completed = new HashSet<string>(completedRewardKeys ?? [], StringComparer.OrdinalIgnoreCase);
        return
        [
            .. items
                .GroupBy(i => (i.ClassName, i.Reward))
                .OrderBy(g => g.Key.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.Reward, StringComparer.OrdinalIgnoreCase)
                .Select(g => new QuestChecklistGroup(
                    g.Key.ClassName,
                    $"{g.Key.ClassName} · {g.Key.Reward}",
                    completed.Contains(RewardKey(g.Key.ClassName, g.Key.Reward)) ? "done"
                        : g.All(i => i.Acquired) ? "ready"
                        : g.Any(i => i.Acquired) ? "in progress"
                        : null,
                    [
                        .. g.OrderBy(i => i.QuestItem, StringComparer.OrdinalIgnoreCase)
                            .Select(i => new QuestChecklistRow(
                                i.Id,
                                i.ClassName,
                                i.QuestItem.Length > 0 ? i.QuestItem : i.Reward,
                                Detail(i.Npc, i.Source),
                                i.Acquired,
                                i.AcquiredUnassigned)),
                    ])),
        ];
    }

    /// <summary>Epic, grouped by the quest's own sections — the order the wiki walks
    /// them, which is the order the player does them.</summary>
    public static IReadOnlyList<QuestChecklistGroup> Epic(IEnumerable<EpicQuestChecklistItem> items)
    {
        return
        [
            .. items
                .GroupBy(i => (i.ClassName, Section: i.Section.Length > 0 ? i.Section : "Checklist"))
                .OrderBy(g => g.Key.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Min(i => i.Order))
                .ThenBy(g => g.Key.Section, StringComparer.OrdinalIgnoreCase)
                .Select(g => new QuestChecklistGroup(
                    g.Key.ClassName,
                    $"{g.Key.ClassName} · {g.Key.Section}",
                    g.All(i => i.Acquired) ? "ready"
                        : g.Any(i => i.Acquired) ? "in progress"
                        : null,
                    [
                        .. g.OrderBy(i => i.Order)
                            .ThenBy(i => i.QuestItem, StringComparer.OrdinalIgnoreCase)
                            .Select(i => new QuestChecklistRow(
                                i.Id,
                                i.ClassName,
                                i.QuestItem.Length > 0 ? i.QuestItem : i.Reward,
                                // Source, or the quest name when there is none — the
                                // rule EQBuddy Mobile already uses. NOT both: an epic's
                                // quest name is the same on every row of the tab, so
                                // joining it to each one is noise, not detail.
                                i.Source.Length > 0 ? i.Source : i.QuestName,
                                i.Acquired,
                                i.AcquiredUnassigned)),
                    ])),
        ];
    }

    /// <summary>The desktop's reward key (class + reward), so "done" means the same
    /// thing on every screen.</summary>
    public static string RewardKey(string className, string reward) => className + "|" + reward;

    /// <summary>"Cilin Spellsinger · Isle 6: Bazzt Zzzt" — whichever halves exist. The
    /// drop location is the half #184 asked for back, and the half that was never drawn.</summary>
    private static string Detail(string npc, string source)
    {
        npc = npc.Trim();
        source = source.Trim();
        if (npc.Length == 0) return source;
        if (source.Length == 0) return npc;
        return npc + " · " + source;
    }
}
