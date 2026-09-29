namespace EQBuddy.Core;

/// <summary>
/// **The Plane of Sky checklist rows, read off the Sky guides** (DRA-47, Delivery 2 N3).
///
/// <para>Until DRA-47 these 222 rows were a hand-kept table, <c>SkyQuestDefaults.cs</c>, and
/// the 95 curated Sky guides in <c>GuideCatalog.json</c> described the same pieces a second
/// time — each guide step naming the item its row named, joined by name at runtime. Two
/// curated copies of one fact is trap 4 waiting for an edit to reach one of them. Measured
/// before the move: the join was exact, 222 rows onto 222 item objectives, none missing, none
/// ambiguous, and every row's NPC equal to its guide's turn-in <c>who</c>. So the table
/// retired into the guides: each item objective now carries its row's
/// <see cref="GuideObjective.ChecklistId"/> (the key every tick is stored under) and its
/// <see cref="GuideObjective.ChecklistSource"/> (the drop line <see cref="SkyIslands"/>
/// parses), the table's provenance comments became that objective's
/// <see cref="GuideObjective.Sources"/>, and this class reads the rows back out.</para>
///
/// <para><b>Order.</b> Rows come out in guide order, then step reading order. Reward groups
/// are in the order the table had them; inside 22 of the 95 rewards the pieces now come in the
/// guide's walking order rather than the table's. Nothing ranks within a reward — the loot
/// auto-tick's first-match rule picks ACROSS rewards, and a reward's items have distinct
/// names — so this moves no tick.</para>
///
/// <para>The CURATED file only, never the harvested half: a Sky row is curated knowledge, and
/// the harvested guides are the machine's (curated wins, <see cref="GuideCatalog.Merge"/>).</para>
/// </summary>
public static class SkyChecklistRows
{
    private static readonly Lazy<SkyQuestChecklistItem[]> _items = new(() => From(GuideCatalog.LoadCurated()));

    /// <summary>Every row, fresh objects each time a caller clones them — callers that keep
    /// state (<see cref="AppSettings.SkyQuestChecklist"/>) take <see cref="SkyQuestChecklistItem.Clone"/>.</summary>
    public static SkyQuestChecklistItem[] Items => _items.Value;

    /// <summary>The rows <paramref name="catalog"/>'s Sky guides stand for. A guide with no
    /// turn-in, or a step with no <see cref="GuideObjective.ChecklistId"/>, contributes nothing
    /// — the guard that every shipped piece HAS an id is a test, not a silent skip here.</summary>
    public static SkyQuestChecklistItem[] From(GuideCatalog catalog)
    {
        var rows = new List<SkyQuestChecklistItem>();
        foreach (var guide in catalog.Guides.Where(g => g.GuideType == GuideType.PlaneOfSkyQuest))
        {
            var turnIn = guide.AllObjectives.FirstOrDefault(o => o.RewardKey.Length > 0);
            var bar = turnIn?.RewardKey.IndexOf('|') ?? -1;
            if (turnIn is null || bar <= 0) continue;
            var className = turnIn.RewardKey[..bar];
            var reward = turnIn.RewardKey[(bar + 1)..];
            foreach (var step in guide.AllObjectives.Where(o => o.ChecklistId.Length > 0))
                rows.Add(new SkyQuestChecklistItem
                {
                    Id = step.ChecklistId,
                    ClassName = className,
                    Npc = turnIn.Who,
                    Reward = reward,
                    QuestItem = step.ItemNames.FirstOrDefault() ?? "",
                    Source = step.ChecklistSource,
                });
        }
        return [.. rows];
    }
}
