using EQBuddy.Core;

namespace EQBuddy.Companion;

// The quest surface: the desktop quest window's three tabs, projected. The tab strip
// comes from Core's QuestSurface, the general list's membership and order from Core's
// QuestMatcher, and the Epic/Sky tabs reuse the existing checklist builders verbatim —
// nothing here invents a second definition of anything.
public static partial class CompanionProjection
{
    /// <summary>General-tab rows shipped per tick. The matcher's list is bounded by
    /// what you own, but a bank full of Bone Chips can still fan wide — capped with a
    /// count, never silently (<see cref="CompanionQuestsSection.MineMore"/>).</summary>
    private const int MaxMineRows = 120;

    private static CompanionQuestsSection BuildQuests(
        AppSettings? settings, CompanionQuestRequest? request, CompanionQuestCatalog? index)
    {
        var epicItems = settings?.EpicQuestChecklist ?? [];
        var skyItems = settings?.SkyQuestChecklist ?? [];
        // Whole-checklist counts, matching the desktop tab badges — NOT the scoped
        // counts the sections below carry, which honor the per-checklist class lenses.
        var tabs = QuestSurface.Tabs(
                epicItems.Count == 0 ? null : (epicItems.Count(i => i.Acquired), epicItems.Count),
                skyItems.Count == 0 ? null : (skyItems.Count(i => i.Acquired), skyItems.Count))
            .Select(h => new CompanionQuestTab(h.Key, h.Label, h.Badge))
            .ToList();

        var req = request ?? new CompanionQuestRequest();
        List<string> mine = [];
        var more = 0;
        if (req.Catalog is { } catalog)
        {
            // The desktop's "mine" exclusions: dismissed quests, and completed
            // non-repeatables (those live under its done view, not in front of you).
            var exclude = new HashSet<string>(req.Hidden, StringComparer.OrdinalIgnoreCase);
            foreach (var (name, count) in req.Completed)
                if (count > 0 && catalog.Quests.FirstOrDefault(q =>
                        q.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { Repeatable: false })
                    exclude.Add(name);
            var matches = QuestMatcher.Match(catalog, req.Owned, req.Tracked, exclude);
            mine = matches.Take(MaxMineRows).Select(m => m.Quest.Name).ToList();
            more = Math.Max(0, matches.Count - mine.Count);
        }

        return new CompanionQuestsSection(
            Tabs: tabs,
            CatalogStamp: index?.Stamp ?? "",
            Catalog: index,
            Mine: mine,
            MineMore: more,
            Owned: req.Owned
                .Where(kv => kv.Value.Total > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value.Total, StringComparer.OrdinalIgnoreCase),
            Tracked: [.. req.Tracked.Order(StringComparer.OrdinalIgnoreCase)],
            Hidden: [.. req.Hidden.Order(StringComparer.OrdinalIgnoreCase)],
            Completed: req.Completed,
            Classes: [.. req.Classes.Select(c => new CompanionQuestClass(c, QuestClassFilter.Abbrev(c)))],
            InferredClass: req.Classes.Count == 0 && req.InferredClass.Length > 0
                ? req.InferredClass : null,
            CharacterClasses: req.CharacterClassNames.Count > 0 ? req.CharacterClassNames : null,
            ClassSourceLabel: req.CharacterClassNames.Count > 0
                ? EQBuddy.Core.CharacterClasses.SourceLabel(req.ClassSource) : null,
            Epics: BuildEpics(settings),
            // The Sky tab is the only checklist that reads anything outside settings: its
            // leftover bands (#243) are a join against the inventory dump, the character's
            // classes and the quest catalog, and all three live on the request.
            Sky: BuildSky(settings, req));
    }
}
