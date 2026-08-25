namespace EQBuddy.Core;

/// <summary>
/// #99 (wizen's diagnosis, verbatim right): the wiki documents each class's Plane of
/// Sky tests on ONE aggregate page ("Ranger Plane of Sky Tests"), and the harvest
/// faithfully turned each page into one quest — so the tracker demanded every ranger
/// item at once and reported progress against the union. The real per-test structure
/// already ships in <see cref="SkyQuestDefaults"/> (reward ↔ turn-in items ↔ NPC,
/// the Sky card's own data), so at load each aggregate entry is REPLACED with one
/// quest per reward. Pattern-based on the page names, so the weekly catalog harvest
/// keeps regenerating without re-breaking this — and if a class page is missing from
/// a harvest, its split simply doesn't run.
/// </summary>
public static class SkyTestSplit
{
    /// <summary>What separates the class from the reward in a split quest's name. Said
    /// once so <see cref="QuestName"/> and <see cref="RewardKeyFor"/> cannot drift.</summary>
    private const string Marker = " Sky Test: ";

    /// <summary>The catalog name for one class's test for one reward.</summary>
    public static string QuestName(string className, string reward) =>
        $"{className}{Marker}{reward}";

    /// <summary>
    /// The Sky checklist key a split quest stands for ("Shadow Knight|Obtenebrate Mithril
    /// Guard"), or "" for any other quest name.
    ///
    /// The two halves of the Quest Tracker were describing one fact with two stores. The
    /// Sky tab reads <see cref="AppSettings.SkyQuestCompleted"/>, which the achievements
    /// import and the turn-in button write; the Quests tab read the per-character quest
    /// ledger, keyed on the quest NAME, which nothing connected to it. So a reward the
    /// game itself said was handed in still sat on the Quests tab as live work — trap 4
    /// with the two sources one tab apart.
    /// </summary>
    public static string RewardKeyFor(string questName)
    {
        var at = questName?.IndexOf(Marker, StringComparison.Ordinal) ?? -1;
        if (at <= 0) return "";
        var className = questName![..at];
        var reward = questName[(at + Marker.Length)..];
        return reward.Length == 0 ? "" : QuestChecklistLayout.RewardKey(className, reward);
    }

    /// <summary>
    /// The quest ledger's completion counts with the Sky checklist's turn-ins folded in,
    /// so the Quests tab answers what the Sky tab already knows.
    ///
    /// Read-only and additive — the ledger's own count wins where it has one, because a
    /// player who marked a quest completed there said something this cannot improve on.
    /// The WRITE side is not here: a click on a Sky Test row goes to
    /// <c>SkyCompleteToggle</c>, so the fact keeps having exactly one store. Merging on
    /// read and writing to a second place would be the bug this fixes, inverted.
    /// </summary>
    public static Dictionary<string, int> WithTurnIns(
        IReadOnlyDictionary<string, int> completed, IEnumerable<string>? skyCompleted)
    {
        var merged = new Dictionary<string, int>(completed, StringComparer.OrdinalIgnoreCase);
        foreach (var key in skyCompleted ?? [])
        {
            var bar = key.IndexOf('|');
            if (bar <= 0 || bar == key.Length - 1) continue;
            var name = QuestName(key[..bar], key[(bar + 1)..]);
            if (!merged.ContainsKey(name)) merged[name] = 1;
        }
        return merged;
    }

    public static void Apply(QuestCatalog catalog)
    {
        foreach (var classGroup in SkyQuestDefaults.Items.GroupBy(i => i.ClassName))
        {
            var aggregate = catalog.Quests.FirstOrDefault(q =>
                q.Name.Equals($"{classGroup.Key} Plane of Sky Tests", StringComparison.OrdinalIgnoreCase));
            if (aggregate is null) continue;
            catalog.Quests.Remove(aggregate);

            foreach (var reward in classGroup.GroupBy(i => i.Reward))
                catalog.Quests.Add(new QuestEntry
                {
                    Name = QuestName(classGroup.Key, reward.Key),
                    // The aggregate page stays the walkthrough — that's where the
                    // wiki documents the individual test.
                    Url = aggregate.Url,
                    StartZone = "Plane of Sky",
                    QuestGiver = reward.First().Npc,
                    Classes = classGroup.Key,
                    Items = reward.Select(i => new QuestItemNeed { Name = i.QuestItem }).ToList(),
                    Rewards = [reward.Key],
                    Zones = ["Plane of Sky"],
                    Era = "Sky",
                });
        }
    }
}
