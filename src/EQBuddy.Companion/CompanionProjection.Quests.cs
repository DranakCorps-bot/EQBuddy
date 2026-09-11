using EQBuddy.Core;
using EQBuddy.UI.Shared;

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

    /// <summary>How many pinned quests ship their walkthrough. See
    /// <see cref="CompanionQuestsSection.Guides"/> for the measurement this rests on: ~6 KB a
    /// guide, so twelve is ~74 KB beside a catalog that is already a few hundred — and a
    /// player with more than twelve quests pinned at once is not reading twelve walkthroughs
    /// on a phone. Never a silent cap (<see cref="CompanionQuestsSection.GuidesMore"/>).</summary>
    private const int MaxGuides = 12;

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

        var (guides, guidesMore) = BuildQuestGuides(settings, req);

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
            Epics: BuildEpics(settings, req),
            // The Sky tab is the only checklist that reads anything outside settings: its
            // leftover bands (#243) are a join against the inventory dump, the character's
            // classes and the quest catalog, and all three live on the request.
            Sky: BuildSky(settings, req),
            Guides: guides,
            GuidesMore: guidesMore);
    }

    /// <summary>
    /// The pinned quests' walkthroughs, through the same
    /// <see cref="GuideChecklistProjection.ApplyQuest"/> the desktop's detail pane calls.
    ///
    /// <para>This is the whole of "the phone got quest guides": one call to the module both
    /// screens already share, from the same point, so the two cannot disagree about what a
    /// step says or whether it is done (David, 2026-08-18 — parity by shared module). What the
    /// phone decides for itself is WHICH quests it carries, the way it already decides how many
    /// <c>Mine</c> rows to ship — see <see cref="CompanionQuestsSection.Guides"/> for the byte
    /// measurement that number rests on.</para>
    ///
    /// <para>Order is the player's own pin order, made stable so the section fingerprint does
    /// not move on a re-enumeration that changed nothing (trap 8).</para></summary>
    private static (List<CompanionQuestGuide> Guides, int More) BuildQuestGuides(
        AppSettings? settings, CompanionQuestRequest req)
    {
        if (settings is null || req.Ledger is null || req.Catalog is null) return ([], 0);

        var byName = req.Catalog.Quests.ToDictionary(q => q.Name, StringComparer.OrdinalIgnoreCase);
        var guides = new List<CompanionQuestGuide>();
        var more = 0;
        foreach (var name in req.Tracked.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!byName.TryGetValue(name, out var quest)) continue;
            var group = GuideChecklistProjection.ApplyQuest(
                quest, GuideCatalog.Default, settings, req.Ledger, req.CharacterKey);
            if (group is null) continue;
            if (guides.Count >= MaxGuides) { more++; continue; }

            guides.Add(new CompanionQuestGuide(quest.Name, new CompanionChecklistGroup(
                // The quest's own name. The page draws this guide under that quest's card, so
                // the heading is the fold control's label rather than an identity — but it is
                // sent, because `checklistBody` is the renderer and a group with no heading is
                // a control with nothing on it.
                Heading: quest.Name,
                Note: group.GuideCaption.Length > 0 ? group.GuideCaption : null,
                Rows: GuideRows(group),
                // No class: a catalog quest is not per-class the way a Sky reward is, and the
                // page's class chips must not narrow a guide the player explicitly pinned.
                Class: null,
                Title: quest.Name,
                Card: GuideCard(group),
                Collapsed: group.Collapsed,
                // THE REWARD LINE ONLY WHEN THERE IS A BLOCK BEHIND IT TO OPEN, which is the
                // opposite of the Sky and Epic tabs and for a reason the harness showed:
                // there, the group's heading is the only thing naming the reward, so the line
                // is the answer to "what do I get". HERE the quest CARD already draws
                // "Rewards: Choker of the Wretched, …" two lines above, and its own turn-in
                // lines are the "Needs …" half — so the summary came out as the same sentence
                // printed twice, one line apart. That is the caption double-count Bevel caught
                // on the desktop, arriving on the phone by a different door.
                //
                // What survives is the line as a CONTROL: when the quest pays exactly one item
                // we hold a stats block for, the line is how a phone opens it (it cannot
                // hover — trap 35), and the page shrinks it to the item's name once open.
                Reward: group.RewardCard.Length > 0 && group.RewardSummary.Length > 0
                    ? group.RewardSummary : null,
                RewardCard: group.RewardCard.Length > 0 ? group.RewardCard : null,
                Fold: FoldKey(group))));
        }
        return (guides, more);
    }
}
