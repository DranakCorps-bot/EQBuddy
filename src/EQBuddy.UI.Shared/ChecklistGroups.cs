using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// THE EPIC AND SKY GROUPS, built once for anyone who needs them: the Guide's Epic and Plane
/// of Sky tabs, and the minimized bar's Tracked quests peek (2026-09-29).
///
/// **Lifted out of <c>QuestsView.RenderChecklist</c> when the peek became a second reader.**
/// The tab built its groups inline — layout, then the guide projection — and a peek that
/// rebuilt them a second way could say "4/13" where the tab says "5/13" (trap 4). Both now
/// call this. EQBuddy Mobile's <c>CompanionProjection.Checklists</c> runs the same two steps
/// over its own request and is untouched here.
/// </summary>
public static class ChecklistGroups
{
    /// <summary>The Epic rows the tab is SHOWING: the classic-era lens is applied here and
    /// nowhere else, so every reader agrees which objectives have a box.</summary>
    public static List<EpicQuestChecklistItem> EpicRows(AppSettings settings) =>
        [.. settings.EpicQuestChecklist.Where(i => !settings.EpicQuestClassicOnly || i.AvailableInClassic)];

    /// <summary>The Epic tab's groups — one per guided class after the projection, its
    /// stages as the rows' headings. <paramref name="ledger"/> null leaves the layout
    /// unprojected, which is what the tab did with no ledger.</summary>
    public static IReadOnlyList<QuestChecklistGroup> Epic(
        AppSettings settings, QuestLedgerStore? ledger, string characterKey,
        IReadOnlyList<EpicQuestChecklistItem> epicRows, GuideAttachmentLines? helper = null)
    {
        var groups = QuestChecklistLayout.Epic(epicRows);
        return ledger is null
            ? groups
            : GuideChecklistProjection.ApplyEpic(groups, epicRows, GuideCatalog.Default,
                settings, ledger, characterKey, helper: helper);
    }

    /// <summary>The Plane of Sky tab's groups — one per class and reward, keyed by
    /// <see cref="QuestChecklistGroup.CompletionKey"/>.</summary>
    public static IReadOnlyList<QuestChecklistGroup> Sky(
        AppSettings settings, QuestLedgerStore? ledger, string characterKey,
        GuideAttachmentLines? helper = null)
    {
        var groups = QuestChecklistLayout.Sky(settings.SkyQuestChecklist, settings.SkyQuestCompleted,
            settings.SkyStepsUnderEveryIsland);
        return ledger is null
            ? groups
            : GuideChecklistProjection.Apply(groups, GuideCatalog.Default,
                settings, ledger, characterKey, helper: helper);
    }
}

/// <summary>
/// A TRACKED EPIC SECTION's key — "&lt;guideId&gt;/&lt;stageId&gt;", e.g.
/// "epic-warrior/the-blades" (2026-09-29). The stage ID and not its name: stage ids are stable
/// and unique per guide, while three classes' section text differs from their stage name.
/// </summary>
public static class EpicSection
{
    public static string Key(string guideId, string stageId) => guideId + "/" + stageId;

    /// <summary>The guide and stage a key names, or null when the catalog no longer has
    /// either (a renamed stage, a retired guide) — which the peek reports rather than drops.
    /// </summary>
    public static (Guide Guide, GuideStage Stage)? Resolve(GuideCatalog catalog, string key)
    {
        var slash = key.IndexOf('/');
        if (slash <= 0 || slash == key.Length - 1) return null;
        if (catalog.Find(key[..slash]) is not { } guide) return null;
        var stageId = key[(slash + 1)..];
        return guide.Stages.FirstOrDefault(s => s.Id.Equals(stageId, StringComparison.OrdinalIgnoreCase))
            is { } stage ? (guide, stage) : null;
    }

    /// <summary>The key for the section a row sits under on the Epic tab: its guide, and the
    /// stage whose NAME the projection stamped on the row as its heading. Null for a row with
    /// no guide or no stage — an unguided row has no section to track.</summary>
    public static string? KeyFor(GuideCatalog catalog, QuestChecklistRow row)
    {
        if (row.GuideRowKey.Length == 0 || row.IslandHeading.Length == 0) return null;
        if (catalog.Find(row.GuideRowKey) is not { } guide) return null;
        return guide.Stages.FirstOrDefault(s => s.Name == row.IslandHeading) is { } stage
            ? Key(guide.Id, stage.Id)
            : null;
    }

    /// <summary>The rows of one section in its class's projected group — the rows the tab
    /// draws under that heading, each once.</summary>
    public static List<QuestChecklistRow> Rows(QuestChecklistGroup group, GuideStage stage) =>
        [.. group.Rows.Where(r => r.IslandHeading == stage.Name)
            .DistinctBy(r => r.Id, StringComparer.Ordinal)];
}
