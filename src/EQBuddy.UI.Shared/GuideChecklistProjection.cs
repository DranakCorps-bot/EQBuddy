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

    /// <summary>The guide and objective a row id names, or null. Scans rather than parses:
    /// see <see cref="RowId"/>.</summary>
    public static (Guide Guide, GuideObjective Objective)? Resolve(GuideCatalog catalog, string rowId)
    {
        if (!IsGuideRowId(rowId)) return null;
        foreach (var guide in catalog.Guides)
            foreach (var objective in guide.AllObjectives)
                if (string.Equals(RowId(guide.Id, objective.Id), rowId, StringComparison.Ordinal))
                    return (guide, objective);
        return null;
    }

    /// <summary>The guide that claims a reward key, or null — the ONE matching rule.
    ///
    /// <para>Matched on the turn-in objective's <see cref="GuideObjective.RewardKey"/> and
    /// nothing else. Not on <see cref="Guide.QuestName"/> (that is the
    /// <c>SkyTestSplit</c> runtime name and stays the catalog link) and not on a page title:
    /// the reward key is the string the checklist, the phone, the achievements import and the
    /// loot auto-tick already all speak.</para></summary>
    public static Guide? GuideFor(GuideCatalog catalog, string? rewardKey)
    {
        if (rewardKey is null || rewardKey.Length == 0) return null;
        foreach (var guide in catalog.Guides)
            foreach (var objective in guide.AllObjectives)
                if (objective.RewardKey.Length > 0
                    && string.Equals(objective.RewardKey, rewardKey, StringComparison.OrdinalIgnoreCase))
                    return guide;
        return null;
    }

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

    /// <summary>Replace every guided group's rows with its guide's objectives, and hand back
    /// every other group untouched.
    ///
    /// <para>Called from exactly where <c>QuestsView.RenderChecklist</c> and
    /// <c>CompanionProjection.BuildSky</c> get their groups, so the two screens cannot show
    /// different guides — or a guide on one and the classic list on the other.</para></summary>
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
                : Project(group, guide, settings, ledger, characterKey, statsFor));
        }
        return projected;
    }

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
        foreach (var objective in guide.AllObjectives)
        {
            var home = GuideProgressRouter.HomeFor(objective, items, out var backing);
            var done = GuideProgressRouter.IsDone(
                settings, ledger, characterKey, guide.Id, objective, items);
            var skipped = !done
                && GuideProgressRouter.IsSkipped(ledger, characterKey, guide.Id, objective);
            if (objective.Authoring == GuideAuthoring.Stub) stubs++;

            rows.Add(new QuestChecklistRow(
                RowId(guide.Id, objective.Id),
                group.ClassName,
                // The short instruction, not the title: the row IS the instruction now.
                GuidePresentation.StepTitle(objective),
                DetailFor(objective, byId, settings, ledger, characterKey, guide, items, done),
                done,
                // An item-backed row inherits the "we guessed which class earned this" mark
                // from the box it reads. Dropping it would lose the one signal that tells a
                // player a tick was placed by inference and can be moved (#106).
                home == GuideProgressHome.SkyItem && backing is not null && backing.AcquiredUnassigned,
                stageOf.GetValueOrDefault(objective.Id, ""),
                objective.Authoring == GuideAuthoring.Stub ? objective.StubNote : "",
                guide.Id,
                home == GuideProgressHome.SkyTurnIn,
                GuidePresentation.RowTooltip(objective),
                skipped));
        }

        var counts = GuideProgressRouter.Counts(settings, ledger, characterKey, guide, items);
        var expanded = group.CompletionKey is { } key
            && settings.GuideExpanded.Contains(key, StringComparer.OrdinalIgnoreCase);
        return group with
        {
            Collapsed = !expanded,
            RewardSummary = GuidePresentation.RewardSummary(group.Title, items),
            RewardCard = GuidePresentation.RewardCard(statsFor(group.Title), items),
            Rows = rows,
            GuideId = guide.Id,
            GuideCaption = GuidePresentation.GuidedCaption(counts.Skipped, stubs),
            GuideCard = Card(group, guide, settings, ledger, characterKey, items),
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
        QuestLedgerStore ledger, string characterKey, IReadOnlyList<SkyQuestChecklistItem> items)
    {
        bool Done(GuideObjective o) =>
            GuideProgressRouter.IsDone(settings, ledger, characterKey, guide.Id, o, items);
        bool Skipped(GuideObjective o) =>
            GuideProgressRouter.IsSkipped(ledger, characterKey, guide.Id, o);

        var next = GuidePresentation.NextObjective(guide, Done, Skipped);
        if (next is null)
            return new QuestChecklistCard("", GuidePresentation.NoNextStep(guide, Done, Skipped));

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
            Why: GuidePresentation.CardWhy(group.Title),
            BeforeLeaving: GuidePresentation.BeforeLeaving(guide, next, Done, Skipped),
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
        IReadOnlyList<SkyQuestChecklistItem> items, bool done)
    {
        if (!done && objective.PrerequisiteObjectiveIds.Count > 0)
        {
            var outstanding = objective.PrerequisiteObjectiveIds
                .Select(id => byId.GetValueOrDefault(id))
                .Where(p => p is not null)
                .Where(p => !GuideProgressRouter.IsDone(
                    settings, ledger, characterKey, guide.Id, p!, items))
                .Select(p => p!.Title);
            var after = GuidePresentation.AfterDetail(outstanding);
            if (after.Length > 0) return after;
        }
        return GuidePresentation.RowDetail(objective);
    }
}
