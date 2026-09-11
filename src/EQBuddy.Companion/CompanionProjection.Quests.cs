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

    /// <summary>
    /// How many of those rows ship their GUIDE (DRA-46), in the matcher's own order — which
    /// puts pinned quests first, so "keep this in front of me" is what gets the walkthrough.
    ///
    /// <para><b>A cap because a guide is not a name.</b> A <see cref="Mine"/> row is one
    /// string the device joins against the catalog it already holds; a guide is ten-odd rows,
    /// each with its title, its dim line, its stub note and a prefilled share-back URL. A
    /// hundred and twenty of them is most of a megabyte on a surface that pushes whenever
    /// anything on it moves, and a first pairing ships every byte of it (trap 67).</para>
    ///
    /// <para>Never silently: <see cref="CompanionQuestsSection.GuidesMore"/> carries what was
    /// left off, exactly as <see cref="CompanionQuestsSection.MineMore"/> does — a cap the
    /// player cannot see is a list that is quietly wrong (trap 50).</para></summary>
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
        IReadOnlyList<CompanionChecklistGroup> guides = [];
        var guidesMore = 0;
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
            (guides, guidesMore) = BuildQuestGuides(settings, req, matches);
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
            Guides: guides,
            GuidesMore: guidesMore,
            Epics: BuildEpics(settings, req),
            // The Sky tab is the only checklist that reads anything outside settings: its
            // leftover bands (#243) are a join against the inventory dump, the character's
            // classes and the quest catalog, and all three live on the request.
            Sky: BuildSky(settings, req));
    }

    /// <summary>
    /// The guided walkthrough for the General tab's quests (DRA-46 / Fable §3 N2) — the SAME
    /// projection call <c>QuestsView.BuildDetail</c> makes, once per quest, so the phone's
    /// quest card and the PC's detail pane cannot show different guides for one quest.
    ///
    /// <para><b>Not a port.</b> Everything the page draws is worded on the way through
    /// <see cref="GuideChecklistProjection.ForQuest"/> — the card, the stage names, the stub
    /// notes, the share-back URLs. That is the whole of "the phone got normal-quest guides",
    /// and it is the shape #184 and #210 both cost us: parity by shared module, not by feature
    /// list (David, 2026-08-18).</para>
    ///
    /// <para><b>The Collect rows still come over, and the page still must not draw them.</b>
    /// A quest card has drawn a row per turn-in item, with its have/need, since the tracker
    /// existed; those rows ARE the guide's "Turn-in pieces" stage.
    /// <see cref="GuideChecklistProjection.WalkthroughRows"/> is the one producer of that
    /// split and the page asks it the same question the desktop does — here, so the answer is
    /// decided once and the page cannot invent a second rule.</para></summary>
    private static (IReadOnlyList<CompanionChecklistGroup> Guides, int More) BuildQuestGuides(
        AppSettings? settings, CompanionQuestRequest req, IReadOnlyList<QuestMatch> matches)
    {
        if (settings is null || req.Ledger is null) return ([], 0);

        var groups = new List<CompanionChecklistGroup>();
        var skipped = 0;
        foreach (var match in matches.Take(MaxMineRows))
        {
            var group = GuideChecklistProjection.ForQuest(
                match.Quest, GuideCatalog.Default, settings, req.Ledger, req.CharacterKey);
            if (group is null) continue;          // no guide walks this quest — Founder lock 5
            if (groups.Count >= MaxGuides) { skipped++; continue; }
            groups.Add(new CompanionChecklistGroup(
                // What the page DRAWS over the block, with the count the desktop's own
                // heading carries. The quest's name is Title below and never repeated here:
                // the card's own title is two lines up (GuidePresentation.QuestHeading).
                Heading: $"{GuidePresentation.QuestHeading}   {group.Done}/{group.Total}",
                Note: group.GuideCaption.Length > 0 ? group.GuideCaption : null,
                Rows:
                [
                    .. GuideChecklistProjection.WalkthroughRows(group).Select(r =>
                        new CompanionChecklistRow(
                            r.Id,
                            r.Title,
                            // The stage this step sits under rides the row, because the page
                            // draws a flat list inside a card and a stage heading of its own
                            // would be a second grouping rule (#184's lesson, one surface on).
                            r.IslandHeading.Length > 0 && r.IslandHeading != r.Detail
                                ? Detail(r) : r.Detail,
                            r.Acquired,
                            r.StubNote.Length > 0 ? GuidePresentation.StubLead + " " + r.StubNote : null,
                            // A phone has no hover, so what the desktop hangs on one rides the
                            // row (trap 35). Empty on a transcribed step, whose sentence IS
                            // the row.
                            r.GuideFacts.Length > 0 && r.StubNote.Length == 0 ? r.GuideFacts : null,
                            GuideChecklistProjection.Resolve(GuideCatalog.Default, r.Id)
                                is var (guide, objective)
                                ? GuidePresentation.ImproveUrl(guide, objective)
                                : null,
                            r.IsSkipped)),
                ],
                // The QUEST this guide walks — the key the page matches a card on. Not the
                // heading, which is words: recovering an identity by parsing a label is one
                // fact stored in one place and read out of another (trap 4).
                Title: match.Quest.Name,
                Card: GuideCard(group),
                Collapsed: group.Collapsed,
                Fold: FoldKey(group)));
        }
        return (groups, skipped);

        // "Stage · who · where", the stage first because it is what the desktop draws as a
        // heading above the row and the phone has no room for one.
        static string Detail(QuestChecklistRow r) =>
            r.Detail.Length > 0 ? r.IslandHeading + " · " + r.Detail : r.IslandHeading;
    }
}
