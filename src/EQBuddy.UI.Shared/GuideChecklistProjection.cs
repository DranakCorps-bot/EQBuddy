using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Turns a curated <see cref="Guide"/> into the checklist rows every surface already draws.
///
/// <para><b>The cutover is per reward group, and it is total.</b> A group whose reward key a
/// guide claims has its item rows REPLACED by that guide's objectives, in reading order,
/// under the guide's stage names. A group no guide claims comes back the same object it went
/// in as — a class nobody has authored yet renders exactly as it did in 1.x (Founder lock 5).
/// There is no half-guided group: that would be the worst of both, an ordered walkthrough
/// interleaved with the flat list it replaces.</para>
///
/// <para><b>Why the rows and not a parallel surface.</b> The Sky tab, the shell's Guide room
/// and EQBuddy Mobile all render <see cref="QuestChecklistGroup"/>. Projecting into that
/// shape is what makes parity structural rather than a feature list somebody keeps level by
/// hand (David, 2026-08-18) — the phone got guides by calling this, not by porting them.</para>
///
/// <para><b>In UI.Shared, not Core.</b> The read side is <see cref="GuideProgressRouter"/>,
/// which lives here because its write side needs <see cref="SkyCompleteToggle"/>; Core cannot
/// reference up, and a Core projection that read the stores itself would be a second producer
/// of "is this done" sitting beside the router (trap 4). Helm ACKed the deviation from the
/// signed plan's §5 on 2026-09-09. Delivery 2 moves <see cref="SkyCompleteToggle"/> into Core,
/// and this may follow it then.</para>
/// </summary>
public static class GuideChecklistProjection
{
    /// <summary>What every guide row's id starts with. A classic Sky row's id is its
    /// <c>sky-NNN</c> catalog id, so this prefix is what keeps the two id spaces from ever
    /// colliding — which matters because <c>QuestChecklistGroup.Done</c> counts
    /// <c>DistinctBy(Id)</c> and a collision would silently miscount a reward.</summary>
    public const string RowIdPrefix = "guide:";

    /// <summary>The row id for one objective. One producer — <see cref="Resolve"/> compares
    /// against this rather than splitting the string back apart, so an id containing the
    /// separator can never resolve to the wrong objective.</summary>
    public static string RowId(string guideId, string objectiveId) =>
        RowIdPrefix + guideId + "/" + objectiveId;

    /// <summary>Is this a guide row's id? The cheap test a tick handler runs before it goes
    /// looking for an objective.</summary>
    public static bool IsGuideRowId(string rowId) =>
        rowId.StartsWith(RowIdPrefix, StringComparison.Ordinal);

    /// <summary>The guide and objective a row id names, or null. Built rather than parsed:
    /// see <see cref="RowId"/> — an id containing the separator could never resolve to the
    /// wrong objective, because the key is the id the producer made.
    ///
    /// <para><b>Indexed, and it had to be before Delivery 3 rather than after.</b> The phone
    /// calls this once per row on every snapshot pass to build that row's share-back link, and
    /// a scan is O(rows × catalog): 222 × 222 while only Plane of Sky was guided, 486 × 708
    /// with the epics beside it, and 486 × 11,000 the week Delivery 2's harvested guides land.
    /// The index is per catalog INSTANCE and held weakly, so a test's fixture catalog is
    /// indexed on its own and collected with it.</para></summary>
    public static (Guide Guide, GuideObjective Objective)? Resolve(GuideCatalog catalog, string rowId)
    {
        if (!IsGuideRowId(rowId)) return null;
        return RowIndex.GetValue(catalog, BuildRowIndex).TryGetValue(rowId, out var hit)
            ? hit
            : null;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        GuideCatalog, Dictionary<string, (Guide Guide, GuideObjective Objective)>> RowIndex = new();

    private static Dictionary<string, (Guide, GuideObjective)> BuildRowIndex(GuideCatalog catalog)
    {
        var index = new Dictionary<string, (Guide, GuideObjective)>(StringComparer.Ordinal);
        foreach (var guide in catalog.Guides)
            foreach (var objective in guide.AllObjectives)
                // First writer wins, which is the scan's own answer — and a duplicate is a
                // catalog defect `NoTwoEpicGuidesClaimTheSameClassOrTheSameRow` and the
                // per-guide duplicate-id rule in Validate() already refuse.
                index.TryAdd(RowId(guide.Id, objective.Id), (guide, objective));
        return index;
    }

    /// <summary>The guide that claims a reward key, or null — the ONE matching rule.
    ///
    /// <para>Matched on the turn-in objective's <see cref="GuideObjective.RewardKey"/> and
    /// nothing else. Not on <see cref="Guide.QuestName"/> (that is the
    /// <c>SkyTestSplit</c> runtime name and stays the catalog link) and not on a page title:
    /// the reward key is the string the checklist, the phone, the achievements import and the
    /// loot auto-tick already all speak.</para></summary>
    /// <para><b>Indexed, for the reason <see cref="Resolve"/> is</b> (DRA-45).
    /// <see cref="Apply"/> calls this once per GROUP on every render, and the scan under it
    /// walked every objective of every guide — each visit re-sorting the guide's stages and
    /// objectives, because <c>Guide.AllObjectives</c> is two <c>OrderBy</c>s. At 95 Sky
    /// groups over a 109-guide catalog that was invisible; over the 1,164 harvested guides
    /// beside them it is a million objective visits per repaint of a surface that paints
    /// every tick (trap 46). Same per-instance weak table, so a fixture catalog is indexed
    /// on its own and collected with it.</para>
    public static Guide? GuideFor(GuideCatalog catalog, string? rewardKey) =>
        rewardKey is { Length: > 0 }
        && RewardIndex.GetValue(catalog, BuildRewardIndex).TryGetValue(rewardKey, out var hit)
            ? hit
            : null;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        GuideCatalog, Dictionary<string, Guide>> RewardIndex = new();

    private static Dictionary<string, Guide> BuildRewardIndex(GuideCatalog catalog)
    {
        var index = new Dictionary<string, Guide>(StringComparer.OrdinalIgnoreCase);
        foreach (var guide in catalog.Guides)
            foreach (var objective in guide.AllObjectives)
                if (objective.RewardKey.Length > 0)
                    // First writer wins, which is the scan's own answer, and
                    // `NoTwoGuidesClaimTheSameReward` refuses a second claimant anyway.
                    index.TryAdd(objective.RewardKey, guide);
        return index;
    }

    /// <summary>Replace every guided group's rows with its guide's objectives, and hand back
    /// every other group untouched.
    ///
    /// <para>Called from exactly where <c>QuestsView.RenderChecklist</c> and
    /// <c>CompanionProjection.BuildSky</c> get their groups, so the two screens cannot show
    /// different guides — or a guide on one and the classic list on the other.</para></summary>
    /// <summary>
    /// The reward item's stats block, by item name — injected the way
    /// <c>GearLocker</c> already takes its <c>statsFor</c>, so this stays a pure function of
    /// its inputs and a test can stage an item the shipped catalog has never heard of.
    ///
    /// <para>The default is the embedded <c>ItemCatalog</c>: ~11k eqlwiki item pages parsed
    /// at build time through the same parsers a live lookup uses. It is local, so a hover
    /// costs eqlwiki nothing — which is the only reason a hover may show this at all
    /// (request-rate policy is the Founder's, not ours).</para></summary>
    public static string? ShippedItemStats(string itemName) =>
        ItemCatalog.Default.Find(itemName)?.StatsText;

    public static IReadOnlyList<QuestChecklistGroup> Apply(
        IReadOnlyList<QuestChecklistGroup> groups,
        GuideCatalog catalog,
        AppSettings settings,
        QuestLedgerStore ledger,
        string characterKey,
        Func<string, string?>? statsFor = null)
    {
        statsFor ??= ShippedItemStats;
        var projected = new List<QuestChecklistGroup>(groups.Count);
        foreach (var group in groups)
        {
            var guide = GuideFor(catalog, group.CompletionKey);
            projected.Add(guide is null
                ? group
                : Project(group, guide, settings, ledger, characterKey, [], statsFor));
        }
        return projected;
    }

    /// <summary>The <see cref="GuideType.EpicQuest"/> guide for a class, or null — the ONE
    /// matching rule for the Epic tab, and deliberately a different one from
    /// <see cref="GuideFor"/>.
    ///
    /// <para>Sky matches on a reward key because a Sky CLASS has six quests. An epic class has
    /// exactly one, the tab's groups are its SECTIONS, and epic rows carry no reward key at all
    /// (<c>QuestChecklistGroup.CompletionKey</c> is null for every one of them, which is what
    /// tells the surface there is no per-section hand-in). So the class name is the key, and
    /// the guide type is what keeps a Plane of Sky guide from ever answering here.</para></summary>
    public static Guide? EpicGuideFor(GuideCatalog catalog, string className)
    {
        if (className.Length == 0) return null;
        foreach (var guide in catalog.Guides)
            if (guide.GuideType == GuideType.EpicQuest
                && guide.ApplicableClasses.Contains(className, StringComparer.OrdinalIgnoreCase))
                return guide;
        return null;
    }

    /// <summary>
    /// The Epic tab's cutover: <b>a guided class's SECTION groups collapse into one guided
    /// group</b>, its rows the guide's objectives under the section names as stage headings.
    /// An unguided class comes back the same objects it went in as (Founder lock 5, by class
    /// rather than by reward — an epic class has one quest).
    ///
    /// <para><paramref name="rows"/> must be the SAME rows <paramref name="groups"/> was built
    /// from, classic-era lens and all. Two things hang off that: the objectives drawn are the
    /// ones whose row is in this list (<see cref="GuideProgressRouter.Drawn"/>), and the ticks
    /// are written straight into these row objects — the store the loot auto-tick, the master
    /// "Epic complete" button, the phone and the classic tab have always shared.</para>
    ///
    /// <para><b>A class whose rows are all outside the lens is handed back untouched</b>, which
    /// is also what keeps the projection off a caller holding rows the guides have never heard
    /// of: no backing row, no guided group, no walkthrough conjured over somebody else's
    /// list.</para>
    ///
    /// <para>Called from where <c>QuestsView.RenderChecklist</c> and
    /// <c>CompanionProjection.BuildEpics</c> get their groups — the same point, the same order,
    /// so the two screens cannot show different guides (David, 2026-08-18).</para></summary>
    public static IReadOnlyList<QuestChecklistGroup> ApplyEpic(
        IReadOnlyList<QuestChecklistGroup> groups,
        IReadOnlyList<EpicQuestChecklistItem> rows,
        GuideCatalog catalog,
        AppSettings settings,
        QuestLedgerStore ledger,
        string characterKey,
        Func<string, string?>? statsFor = null)
    {
        statsFor ??= ShippedItemStats;
        var projected = new List<QuestChecklistGroup>(groups.Count);
        var guided = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Per CLASS and not per group: the tab hands us one group per section (64 of them
        // across the shipped catalog), and re-deriving a class's rows for each would be
        // 64 × 486 × 66 string comparisons on a surface that redraws whenever a box moves.
        var rowsByClass = new Dictionary<string, List<EpicQuestChecklistItem>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var guide = EpicGuideFor(catalog, group.ClassName);
            if (guide is null)
            {
                projected.Add(group);
                continue;
            }

            if (!rowsByClass.TryGetValue(group.ClassName, out var classRows))
            {
                var ids = new HashSet<string>(
                    guide.AllObjectives.Select(o => o.Id), StringComparer.OrdinalIgnoreCase);
                classRows = [.. rows
                    .Where(r => r.ClassName.Equals(group.ClassName, StringComparison.OrdinalIgnoreCase))
                    .Where(r => ids.Contains(r.Id))];
                rowsByClass[group.ClassName] = classRows;
            }

            if (classRows.Count == 0)
            {
                projected.Add(group);
                continue;
            }

            // ONE group per guided class, at the position its first section held — the epic
            // is the heading now, and the sections are the stage names inside it.
            if (!guided.Add(group.ClassName)) continue;
            projected.Add(Project(
                group with
                {
                    Title = GuidePresentation.EpicTitle,
                    // The class's epic page, so the heading link still opens something real.
                    // Its title is the guide's own first source, which is the string the
                    // weekly refresh flags on — one page name, one place (trap 3/trap 4).
                    WikiPage = guide.Sources.Count > 0 ? guide.Sources[0].Title : "",
                    // Epic completion is per CLASS and lives in EpicQuestCompleted, never per
                    // section — the tab's own band carries it, and a group-level turn-in
                    // control here would be a second writer of it.
                    CompletionKey = null,
                },
                guide, settings, ledger, characterKey, classRows, statsFor));
        }

        return projected;
    }

    /// <summary>
    /// What the player's fold choice is remembered under — a Sky group's reward key, an epic
    /// group's guide id.
    ///
    /// <para>One producer, because the fold control WRITES this list and the projection READS
    /// it: spelled twice they would drift the first time a group had no completion key, which
    /// is every group on the Epic tab. (It did: the control returned early on an empty key and
    /// the "+" was a silent no-op.) The two id spaces cannot collide — a reward key is
    /// "Class|Reward" and a guide id is a slug.</para></summary>
    public static string FoldKey(string? completionKey, string guideId) =>
        completionKey is { Length: > 0 } key ? key : guideId;

    /// <summary>The fold key of a group that has already been through the projection.</summary>
    public static string FoldKey(QuestChecklistGroup group) =>
        FoldKey(group.CompletionKey, group.GuideId);

    /// <summary>The reward key a guide claims — its turn-in objective's. A guide has exactly
    /// one (<c>GuideCatalog.Validate</c> holds the key to a real Sky reward, and the class
    /// authoring rule is one guide per reward), so the first is the only.</summary>
    public static string RewardKeyOf(Guide guide) =>
        guide.AllObjectives.FirstOrDefault(o => o.RewardKey.Length > 0)?.RewardKey ?? "";

    /// <summary>The reward's own checklist rows — the only ones an item-backed objective may
    /// claim, and the ONE way to get them, so the desktop's group-shaped call and the phone's
    /// guide-shaped call cannot disagree about which boxes a guide owns.
    ///
    /// <para>Scoped to this reward and never wider: "Wind Rune Azia" is a row for the Bard and
    /// a row for the Warrior, and they are two facts about two quests.</para></summary>
    public static IReadOnlyList<SkyQuestChecklistItem> ItemsFor(AppSettings settings, string? rewardKey) =>
        rewardKey is { Length: > 0 }
            ? SkyCompleteToggle.ItemsFor(settings.SkyQuestChecklist, rewardKey)
            : [];

    private static QuestChecklistGroup Project(
        QuestChecklistGroup group, Guide guide,
        AppSettings settings, QuestLedgerStore ledger, string characterKey,
        IReadOnlyList<EpicQuestChecklistItem> epicRows,
        Func<string, string?> statsFor)
    {
        var items = ItemsFor(settings, group.CompletionKey);
        var byId = guide.AllObjectives.ToDictionary(o => o.Id, StringComparer.OrdinalIgnoreCase);
        var stageOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in guide.Stages)
            foreach (var objective in stage.Objectives)
                stageOf[objective.Id] = stage.Name;

        var rows = new List<QuestChecklistRow>();
        var stubs = 0;
        // Drawn, not AllObjectives: on the Epic tab the classic-era lens has already removed
        // rows, and an objective whose row is not on the tab has no box to tick.
        foreach (var objective in GuideProgressRouter.Drawn(guide, epicRows))
        {
            var home = GuideProgressRouter.HomeFor(
                objective, items, epicRows, out var backing, out var backingRow);
            var done = GuideProgressRouter.IsDone(
                settings, ledger, characterKey, guide.Id, objective, items, epicRows);
            var skipped = !done
                && GuideProgressRouter.IsSkipped(ledger, characterKey, guide.Id, objective);
            if (objective.Authoring == GuideAuthoring.Stub) stubs++;

            // The row already IS this sentence on a transcribed step, so the hover would be a
            // second copy of it — and on the phone, where the facts ride the row rather than a
            // hover it cannot have (trap 35), it would be the same line printed twice.
            var title = GuidePresentation.StepTitle(objective);
            var facts = GuidePresentation.RowTooltip(objective);

            rows.Add(new QuestChecklistRow(
                RowId(guide.Id, objective.Id),
                group.ClassName,
                // The short instruction, not the title: the row IS the instruction now.
                title,
                DetailFor(objective, byId, settings, ledger, characterKey, guide, items, epicRows, done),
                done,
                // An item-backed row inherits the "we guessed which class earned this" mark
                // from the box it reads. Dropping it would lose the one signal that tells a
                // player a tick was placed by inference and can be moved (#106).
                (home == GuideProgressHome.SkyItem && backing is not null && backing.AcquiredUnassigned)
                    || (home == GuideProgressHome.EpicItem && backingRow is not null
                        && backingRow.AcquiredUnassigned),
                stageOf.GetValueOrDefault(objective.Id, ""),
                objective.Authoring == GuideAuthoring.Stub ? objective.StubNote : "",
                guide.Id,
                home == GuideProgressHome.SkyTurnIn,
                string.Equals(facts, title, StringComparison.Ordinal) ? "" : facts,
                skipped));
        }

        var counts = GuideProgressRouter.Counts(
            settings, ledger, characterKey, guide, items, epicRows);
        var expanded = settings.GuideExpanded.Contains(
            FoldKey(group.CompletionKey, guide.Id), StringComparer.OrdinalIgnoreCase);
        // An EPIC pays several items and eqlwiki names none of them "the epic" — so the hover
        // lists what the page lists and there is no single item window to show. A Sky reward
        // is one item and gets both (GuidePresentation.EpicTitle says why).
        var epic = guide.GuideType == GuideType.EpicQuest;
        return group with
        {
            Collapsed = !expanded,
            RewardSummary = epic
                ? GuidePresentation.EpicRewardSummary(
                    epicRows.Select(r => r.Reward.Trim()).FirstOrDefault(r => r.Length > 0) ?? "")
                : GuidePresentation.RewardSummary(group.Title, items),
            RewardCard = epic ? "" : GuidePresentation.RewardCard(statsFor(group.Title), items),
            Rows = rows,
            GuideId = guide.Id,
            GuideCaption = GuidePresentation.GuidedCaption(counts.Skipped, stubs),
            GuideCard = Card(group, guide, settings, ledger, characterKey, items, epicRows),
        };
    }

    /// <summary>
    /// The active-step card for one guided group — one producer of "what is next", read by
    /// the desktop, the shell and the phone alike.
    ///
    /// <para>Every string is already worded here. A surface picks layout; it never decides
    /// what the card SAYS, which is the only way three renderers stay honest about one
    /// answer.</para></summary>
    private static QuestChecklistCard Card(
        QuestChecklistGroup group, Guide guide, AppSettings settings,
        QuestLedgerStore ledger, string characterKey, IReadOnlyList<SkyQuestChecklistItem> items,
        IReadOnlyList<EpicQuestChecklistItem> epicRows)
    {
        bool Done(GuideObjective o) =>
            GuideProgressRouter.IsDone(settings, ledger, characterKey, guide.Id, o, items, epicRows);
        bool Skipped(GuideObjective o) =>
            GuideProgressRouter.IsSkipped(ledger, characterKey, guide.Id, o);

        // The same list the rows are drawn from, so "what is next" can never name a step the
        // tab is not showing — and so AllDone means "every row on this tab is ticked", which is
        // what the player is looking at.
        var drawn = GuideProgressRouter.Drawn(guide, epicRows).ToList();
        var next = GuidePresentation.NextObjective(drawn, Done, Skipped);
        if (next is null)
            return new QuestChecklistCard("", GuidePresentation.NoNextStep(drawn, Done, Skipped));

        // A stub's banner REPLACES where/what: "we cannot say where" and "here is where"
        // must not both be on the card.
        var stub = next.Authoring == GuideAuthoring.Stub;
        return new QuestChecklistCard(
            RowId(guide.Id, next.Id),
            GuidePresentation.StepTitle(next),
            // One sentence for where-and-who, and the detail only when it is not already
            // in the instruction. A step that answers neither draws neither line.
            Directions: stub ? "" : GuidePresentation.Directions(next),
            Detail: stub ? "" : GuidePresentation.ExtraDetail(next),
            Why: guide.GuideType == GuideType.EpicQuest
                ? GuidePresentation.EpicCardWhy(group.ClassName)
                : GuidePresentation.CardWhy(group.Title),
            BeforeLeaving: GuidePresentation.BeforeLeaving(guide, drawn, next, Done, Skipped),
            StubNote: stub ? next.StubNote : "",
            ImproveUrl: GuidePresentation.ImproveUrl(guide, next));
    }

    /// <summary>The dim line under a row. Who and where for an ordinary step; for a turn-in
    /// whose pieces are not all in hand, the steps still outstanding BY NAME — because a
    /// player looking at a locked turn-in wants to know what unlocks it, and that is the one
    /// moment the guide can answer it without them scrolling.</summary>
    private static string DetailFor(
        GuideObjective objective, Dictionary<string, GuideObjective> byId,
        AppSettings settings, QuestLedgerStore ledger, string characterKey, Guide guide,
        IReadOnlyList<SkyQuestChecklistItem> items,
        IReadOnlyList<EpicQuestChecklistItem> epicRows, bool done)
    {
        if (!done && objective.PrerequisiteObjectiveIds.Count > 0)
        {
            var outstanding = objective.PrerequisiteObjectiveIds
                .Select(id => byId.GetValueOrDefault(id))
                .Where(p => p is not null)
                .Where(p => !GuideProgressRouter.IsDone(
                    settings, ledger, characterKey, guide.Id, p!, items, epicRows))
                .Select(p => GuidePresentation.StepName(p!));
            var after = GuidePresentation.AfterDetail(outstanding);
            if (after.Length > 0) return after;
        }
        return GuidePresentation.RowDetail(objective);
    }
}
