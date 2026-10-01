using System.Runtime.CompilerServices;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>§18's three groups, in the requirement's own order.</summary>
public enum WhileHereGroup
{
    /// <summary>A step of a quest the player TRACKS — the 📌 on the Quests tab, which is the
    /// player saying "this is what I am doing".</summary>
    Required,

    /// <summary>A step of a quest the player has STARTED and not tracked — a piece already
    /// looted, a step already ticked, an item already in the bags. Their own play, not a
    /// suggestion.</summary>
    RelevantReward,

    /// <summary>A quest neither tracked nor started that has something here. Named, never
    /// walked: the catalog has hundreds, and a list of every step of every one is not a
    /// block anybody reads.</summary>
    Optional,
}

/// <summary>Why the block has nothing to list — three different facts, never one silence
/// (trap 73: an unknown zone is not "nothing here").</summary>
public enum WhileHereState
{
    /// <summary>At least one group has something in it.</summary>
    Answered,
    /// <summary>The log has printed no "You have entered …" line yet this session.</summary>
    ZoneUnknown,
    /// <summary>The log's zone is not something the place rule reads as a place.</summary>
    NotAPlace,
    /// <summary>The zone is known and no open step the block may list is placed in it — hidden
    /// and finished quests are never listed, and the class/era-filtered ones are COUNTED in
    /// <see cref="WhileHereAnswer.Filtered"/>, so this is never "no quest has a step here".</summary>
    NothingOpenHere,
}

/// <summary>One open step that can be done in this zone.</summary>
/// <param name="Quest">The catalog quest it belongs to.</param>
/// <param name="StepId">The objective's id, so a surface can key a row without the title.</param>
/// <param name="Step">The step as its tab words it — the guide objective's title.</param>
/// <param name="Who">Who the step's source names HERE — the item page's creatures in this zone,
/// or the quest giver. Uncapped; the sentence caps.</param>
/// <param name="WhoDrops">The place's own <see cref="WhileHerePlace.WhoDrops"/>: <paramref name="Who"/>
/// is creatures the step's item drops from, not a person it is done at. Only such a step can be
/// the creature at a spawn point (DRA-42 D3).</param>
public sealed record WhileHereStep(
    string Quest, string StepId, string Step, IReadOnlyList<string> Who, bool WhoDrops = false);

/// <summary>
/// The whole answer for one zone, one character, one moment.
/// </summary>
/// <param name="Zone">The zone asked about — the log's own spelling.</param>
/// <param name="Required">Open steps of tracked quests placed here, tracked quest order then
/// the guide's reading order.</param>
/// <param name="Relevant">Open steps of started, untracked quests placed here.</param>
/// <param name="Optional">The quests with an open step here that the player has neither
/// tracked nor started, by NAME only, in name order.</param>
/// <param name="UnplacedTracked">Open steps of TRACKED work that name no place any structured
/// reference can read — an Epic 1.0 step's prose, a stub whose page named no giver. Counted so
/// the block can say it rather than let a tracked step vanish (trap 50).</param>
/// <param name="Filtered">Quests neither tracked nor started with an open step HERE that the
/// General tab's class lens or era filter keeps out of the Optional group. Counted for the same
/// reason as <paramref name="UnplacedTracked"/>: without it a Warrior in a zone whose only
/// placed quest is Paladin-locked would be told no quest has a step there (trap 73).</param>
public sealed record WhileHereAnswer(
    string Zone,
    WhileHereState State,
    IReadOnlyList<WhileHereStep> Required,
    IReadOnlyList<WhileHereStep> Relevant,
    IReadOnlyList<string> Optional,
    int UnplacedTracked,
    int Filtered = 0)
{
    /// <summary>The answer before anything has been asked — the memo's first value.</summary>
    public static readonly WhileHereAnswer None =
        new("", WhileHereState.ZoneUnknown, [], [], [], 0);

    /// <summary>The steps of one group, for a surface that walks the groups in order.</summary>
    public IReadOnlyList<WhileHereStep> Steps(WhileHereGroup group) => group switch
    {
        WhileHereGroup.Required => Required,
        WhileHereGroup.RelevantReward => Relevant,
        _ => [],
    };
}

/// <summary>
/// What the player left unresolved in the zone the log last took them OUT of (DRA-42 D2,
/// requirements §19, the log-only reading). EQBuddy learns a transition only after "You have
/// entered …" prints, so this is said AFTER the move — never a "Continue anyway?" before it.
/// </summary>
/// <param name="From">The zone departed — the log's spelling of the entry before the latest.</param>
/// <param name="At">When the log entered the zone that followed it, the log's own stamp. The
/// dismissal's key, so dismissing one departure never swallows the next one out of the same
/// zone.</param>
/// <param name="Left">The D1 producer's answer for <paramref name="From"/>, asked NOW, holding only
/// the player's own work there — the Required and Relevant groups. Optional quests were never
/// started, so they are not "left"; the unplaced and filtered counts are not about that zone.</param>
public sealed record WhileHereDeparture(string From, DateTime At, WhileHereAnswer Left)
{
    /// <summary>The steps still open, in the answer's own group order.</summary>
    public IReadOnlyList<WhileHereStep> Steps => [.. Left.Required, .. Left.Relevant];

    /// <summary>The dismissal key: one departure, never the zone.</summary>
    public string Key => $"{From}|{At:O}";
}

/// <summary>Everything the producer reads, in one value — the <see cref="GuideStores"/>
/// reason: a positional list this long is a puzzle at every call site.</summary>
/// <param name="Zone">The zone the LOG most recently entered —
/// <see cref="StatsSnapshot.CurrentZone"/>, the latest "You have entered …" line. NOT a
/// session's attributed <c>PrimaryZone</c>: that is where a session mostly WAS, and D2's
/// change notice keys on the entered line, so the two slices must read the same one
/// (Planner, DRA-42 SIGN).</param>
/// <param name="Classes">The character's classes, for the Optional group's class lens — the
/// General tab's own <see cref="QuestClassFilter.MatchesAny"/>; empty means no lens.</param>
/// <param name="Era">The General tab's era filter (<c>AppSettings.QuestEraFilter</c>).</param>
public sealed record WhileHereInputs(
    string? Zone,
    AppSettings Settings,
    QuestLedgerStore Ledger,
    string CharacterKey,
    QuestCatalog Quests,
    GuideCatalog Guides,
    ItemCatalog? Items,
    IReadOnlyCollection<string> Classes,
    string Era);

/// <summary>
/// **WHILE YOU'RE HERE** — the open steps that can be done in the zone the log says you are in
/// (DRA-42 D1, requirements §18). <b>The one producer</b>: the Guide room and the phone both
/// draw this answer, and D2's "before you leave" re-groups it rather than re-asking.
///
/// <para><b>It recomputes nothing</b> (trap 4). Which objectives exist is
/// <see cref="GuideProgressRouter.Drawn"/>; whether one is done is
/// <see cref="GuideProgressRouter.IsDone"/> against the same <see cref="GuideStores"/> the tabs
/// build; whether it is struck out is <see cref="GuideProgressRouter.IsSkipped"/>. Where a step
/// can be done is <see cref="WhileHerePlaces"/> over the item catalog and the quest's start zone.
/// A step the router would call done is never "here", whatever its place.</para>
///
/// <para><b>Actionable means its prerequisites are met.</b> A hand-in whose pieces are still
/// missing is placed at its giver, and listing it while the pieces are elsewhere would send the
/// player to a person with nothing to hand over. <see cref="GuideObjective.PrerequisiteObjectiveIds"/>
/// is the guide's own statement of order; a prerequisite done OR struck out counts as met, the
/// way the guide's own NEXT marker reads it.</para>
///
/// <para><b>Framework-free and in UI.Shared rather than Core</b> because the router it must
/// not duplicate lives here (it needs <c>SkyCompleteToggle</c>'s write side). The place RULES —
/// the zone join and what counts as a place — are Core's (<see cref="WhileHerePlaces"/>), so the
/// committed negatives pin them where every other zone join in the repo is pinned.</para>
/// </summary>
public static class WhileHere
{
    /// <summary>
    /// The answer for <see cref="WhileHereInputs.Zone"/>.
    /// </summary>
    public static WhileHereAnswer For(WhileHereInputs inputs)
    {
        var zone = inputs.Zone?.Trim() ?? "";
        if (zone.Length == 0) return WhileHereAnswer.None;
        if (!TradeskillMaterials.IsPlace(zone))
            return new(zone, WhileHereState.NotAPlace, [], [], [], 0);

        var key = inputs.CharacterKey;
        var tracked = inputs.Ledger.TrackedFor(key);
        var hidden = inputs.Ledger.HiddenFor(key);
        var completed = SkyCompleteToggle.CompletedQuests(inputs.Settings, inputs.Ledger, key);
        var owned = inputs.Ledger.For(key);
        var index = Index(inputs.Quests, inputs.Guides, inputs.Items);

        var required = new List<WhileHereStep>();
        var relevant = new List<WhileHereStep>();
        var optional = new List<string>();
        var unplaced = 0;
        var filtered = 0;

        // TRACKED work first, whole: its steps that place nowhere are counted, which needs
        // every step and not only the ones this zone's bucket would hand us.
        var byName = inputs.Quests.Quests
            .GroupBy(q => q.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var name in tracked.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!byName.TryGetValue(name, out var quest)) continue;
            if (IsFinished(quest, completed)) continue;
            foreach (var open in OpenSteps(inputs, quest))
            {
                if (open.Places.Count == 0) { unplaced++; continue; }
                if (Here(open.Places, zone) is { } place)
                    required.Add(Step(quest, open.Objective, place));
            }
        }
        unplaced += UnplacedEpicSteps(inputs);

        // Everything else placed here, through the zone's bucket of the catalog index.
        foreach (var quest in index.Here(zone))
        {
            if (tracked.Contains(quest.Name) || hidden.Contains(quest.Name)) continue;
            if (IsFinished(quest, completed)) continue;

            var steps = OpenSteps(inputs, quest).ToList();
            var here = new List<WhileHereStep>();
            foreach (var open in steps)
                if (Here(open.Places, zone) is { } place)
                    here.Add(Step(quest, open.Objective, place));
            if (here.Count == 0) continue;

            if (Started(inputs, quest, owned))
                relevant.AddRange(here);
            else if (QuestClassFilter.MatchesAny(quest.Classes, inputs.Classes)
                     && QuestEraLadder.Allowed(quest.Era, inputs.Era))
                optional.Add(quest.Name);
            else
                filtered++;
        }

        var state = required.Count + relevant.Count + optional.Count == 0
            ? WhileHereState.NothingOpenHere
            : WhileHereState.Answered;
        return new(zone, state, required,
            [.. relevant.OrderBy(s => s.Quest, StringComparer.OrdinalIgnoreCase)],
            [.. optional.Order(StringComparer.OrdinalIgnoreCase)],
            unplaced, filtered);
    }

    /// <summary>
    /// The departure from the zone before the latest entry in <paramref name="zones"/> — the
    /// snapshot's own <see cref="StatsSnapshot.Zones"/>, consecutive repeats already folded — or
    /// null when there is nothing to say: no earlier zone, one that is not a place, a re-spelling
    /// of the zone the player is in, or no open step of their own work left there.
    ///
    /// <para><b>The same producer, asked about another zone</b> (trap 4): what is open in the
    /// departed zone is <see cref="For"/>'s answer for it against today's stores, so a step
    /// ticked after leaving leaves the notice, and the notice goes when the last one does —
    /// nothing is remembered from the moment of the move. A pure function of the snapshot, so a
    /// log replayed at launch re-derives the same departure rather than stacking one.</para>
    /// </summary>
    public static WhileHereDeparture? DepartureFor(WhileHereInputs inputs, IReadOnlyList<TimedDetail> zones)
    {
        if (zones.Count < 2) return null;
        var from = zones[^2].Text.Trim();
        var to = zones[^1];
        if (from.Length == 0 || !TradeskillMaterials.IsPlace(from)) return null;
        if (WhileHerePlaces.SameZone(from, to.Text)) return null;

        var there = For(inputs with { Zone = from });
        if (there.Required.Count + there.Relevant.Count == 0) return null;
        return new WhileHereDeparture(from, to.Time,
            there with { Optional = [], UnplacedTracked = 0, Filtered = 0 });
    }

    /// <summary>A completed quest has nothing left to do here — unless it is repeatable, which
    /// is the General tab's own "mine" exclusion.</summary>
    private static bool IsFinished(QuestEntry quest, IReadOnlyDictionary<string, int> completed) =>
        !quest.Repeatable && completed.GetValueOrDefault(quest.Name) > 0;

    /// <summary>The guide that walks this quest — a Sky reward's by its reward key, anything
    /// else by quest name — the two matching rules the tabs already use, never a third.</summary>
    public static (Guide Guide, GuideStores Stores)? GuideOf(
        QuestEntry quest, GuideCatalog guides, AppSettings settings)
    {
        var reward = SkyTestSplit.RewardKeyFor(quest.Name);
        if (reward.Length > 0)
            return GuideChecklistProjection.GuideFor(guides, reward) is { } sky
                ? (sky, GuideStores.For(GuideChecklistProjection.ItemsFor(settings, reward)))
                : null;
        return GuideChecklistProjection.QuestGuideFor(guides, quest.Name) is { } guide
            ? (guide, new GuideStores([], [], quest))
            : null;
    }

    private readonly record struct OpenStep(GuideObjective Objective, IReadOnlyList<WhileHerePlace> Places);

    /// <summary>The quest's drawn objectives that are neither done nor struck out and whose
    /// prerequisites are met, each with every place it can be done. A quest no guide walks
    /// yields nothing — its Quests-tab card is still where its items are counted.</summary>
    private static IEnumerable<OpenStep> OpenSteps(WhileHereInputs inputs, QuestEntry quest)
    {
        if (GuideOf(quest, inputs.Guides, inputs.Settings) is not var (guide, stores)) yield break;
        var drawn = GuideProgressRouter.Drawn(guide, stores.EpicRows).ToList();
        var settled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in drawn)
            if (IsSettled(inputs, guide, o, stores)) settled.Add(o.Id);

        foreach (var o in drawn)
        {
            if (settled.Contains(o.Id)) continue;
            if (o.PrerequisiteObjectiveIds.Any(p => !settled.Contains(p))) continue;
            yield return new OpenStep(o, PlacesOf(inputs.Items, quest, o, stores));
        }
    }

    private static bool IsSettled(WhileHereInputs inputs, Guide guide, GuideObjective o, GuideStores stores) =>
        GuideProgressRouter.IsDone(inputs.Settings, inputs.Ledger, inputs.CharacterKey, guide.Id, o, stores)
        || GuideProgressRouter.IsSkipped(inputs.Ledger, inputs.CharacterKey, guide.Id, o);

    /// <summary>
    /// Every place ONE step can be done — <see cref="WhileHerePlaces"/>' three sources, chosen by
    /// the router's own answer about the step.
    /// </summary>
    public static IReadOnlyList<WhileHerePlace> PlacesOf(
        ItemCatalog? items, QuestEntry quest, GuideObjective objective, GuideStores stores)
    {
        var places = new List<WhileHerePlace>();
        var home = GuideProgressRouter.HomeFor(objective, stores, out _);
        // A Sky checklist piece or turn-in: the Sky quest's start zone, which SkyTestSplit
        // wrote as "Plane of Sky" because every Sky row IS one. The router decided it is a Sky
        // row; this does not re-derive that.
        if (home == GuideProgressHome.SkyItem)
        {
            // A Sky PIECE is a loot step, so the quest giver is the wrong person to name. A
            // curated piece names its dropper in Who; a trash-farm piece names nobody, and
            // draws nobody.
            var dropper = objective.Authoring == GuideAuthoring.Authored && objective.Who.Length > 0;
            foreach (var p in WhileHerePlaces.StartPlace(quest))
                places.Add(p with { Who = dropper ? [objective.Who] : [], WhoDrops = dropper });
        }
        else if (home == GuideProgressHome.SkyTurnIn || WhileHerePlaces.IsNpcStep(objective.ObjectiveType))
        {
            places.AddRange(WhileHerePlaces.StartPlace(quest));
        }
        if (GuideProgressRouter.ItemBackedObjectiveTypes.Contains(objective.ObjectiveType, StringComparer.Ordinal))
            foreach (var item in objective.ItemNames)
                foreach (var p in WhileHerePlaces.DropPlaces(items, item))
                    if (!places.Any(x => WhileHerePlaces.SameZone(x.Zone, p.Zone)))
                        places.Add(p);
        return places;
    }

    /// <summary>The place that IS this zone, or null when none is.</summary>
    private static WhileHerePlace? Here(IReadOnlyList<WhileHerePlace> places, string zone)
    {
        foreach (var p in places)
            if (WhileHerePlaces.SameZone(p.Zone, zone)) return p;
        return null;
    }

    private static WhileHereStep Step(QuestEntry quest, GuideObjective objective, WhileHerePlace place) =>
        new(quest.Name, objective.Id, objective.Title, place.Who, place.WhoDrops);

    /// <summary>Started = the player's own play has touched it: a drawn step the router calls
    /// done, or any of its turn-in items in the bags.</summary>
    private static bool Started(WhileHereInputs inputs, QuestEntry quest,
        IReadOnlyDictionary<string, QuestLedgerStore.Entry> owned)
    {
        if (quest.Items.Any(i => owned.TryGetValue(i.Name, out var e) && e.Total > 0)) return true;
        if (GuideOf(quest, inputs.Guides, inputs.Settings) is not var (guide, stores)) return false;
        return GuideProgressRouter.Drawn(guide, stores.EpicRows).Any(o =>
            GuideProgressRouter.IsDone(inputs.Settings, inputs.Ledger, inputs.CharacterKey, guide.Id, o, stores));
    }

    /// <summary>Open steps of TRACKED Epic 1.0 sections. Every one places nowhere — an epic step
    /// is the page's sentence, and reading a zone out of it is the parse
    /// <see cref="WhileHerePlaces"/> refuses — so they are counted, never listed.</summary>
    private static int UnplacedEpicSteps(WhileHereInputs inputs)
    {
        var sections = inputs.Ledger.TrackedSectionsFor(inputs.CharacterKey);
        if (sections.Count == 0) return 0;
        var rows = ChecklistGroups.EpicRows(inputs.Settings);
        var stores = GuideStores.For([], rows);
        var count = 0;
        foreach (var sectionKey in sections)
        {
            if (EpicSection.Resolve(inputs.Guides, sectionKey) is not var (guide, stage)) continue;
            var drawn = new HashSet<string>(GuideProgressRouter.Drawn(guide, rows).Select(o => o.Id),
                StringComparer.OrdinalIgnoreCase);
            count += stage.Objectives.Count(o => drawn.Contains(o.Id)
                && !IsSettled(inputs, guide, o, stores));
        }
        return count;
    }

    // ── The catalog index ────────────────────────────────────────────────────────────────

    /// <summary>Which quests have ANY step placed in a zone, by <see cref="ZoneMapFiles.IdentityKey"/>
    /// — a SUPERSET prefilter, so a zone asks about its own few dozen quests rather than walking
    /// 1,173 guides on a surface that repaints every tick (trap 46). The real answer is still
    /// <see cref="PlacesOf"/> per step against live stores; a quest in the wrong bucket costs a
    /// walk and nothing else. Per catalog INSTANCE through a weak table, the
    /// <c>GuideChecklistProjection.GuideFor</c> arrangement, so a test's fixture catalog is
    /// indexed on its own.</summary>
    private sealed class CatalogIndex(Dictionary<string, List<QuestEntry>> byZone, GuideCatalog guides, ItemCatalog? items)
    {
        public GuideCatalog Guides { get; } = guides;
        public ItemCatalog? Items { get; } = items;

        public IReadOnlyList<QuestEntry> Here(string zone) =>
            byZone.TryGetValue(ZoneMapFiles.IdentityKey(zone), out var hit) ? hit : [];
    }

    private static readonly ConditionalWeakTable<QuestCatalog, CatalogIndex> Indexes = new();
    private static readonly object IndexLock = new();

    private static CatalogIndex Index(QuestCatalog quests, GuideCatalog guides, ItemCatalog? items)
    {
        lock (IndexLock)
        {
            if (Indexes.TryGetValue(quests, out var hit)
                && ReferenceEquals(hit.Guides, guides) && ReferenceEquals(hit.Items, items))
                return hit;
            var built = Build(quests, guides, items);
            Indexes.AddOrUpdate(quests, built);
            return built;
        }
    }

    private static CatalogIndex Build(QuestCatalog quests, GuideCatalog guides, ItemCatalog? items)
    {
        var byZone = new Dictionary<string, List<QuestEntry>>(StringComparer.OrdinalIgnoreCase);
        // The STATIC Sky rows stand in for the player's list here: routing reads only their
        // item names, which are the same rows, and the index must not depend on a character.
        var skyRows = SkyChecklistRows.Items;
        foreach (var quest in quests.Quests)
        {
            var reward = SkyTestSplit.RewardKeyFor(quest.Name);
            (Guide Guide, GuideStores Stores)? walked = reward.Length > 0
                ? GuideChecklistProjection.GuideFor(guides, reward) is { } sky
                    ? (sky, GuideStores.For(SkyCompleteToggle.ItemsFor(skyRows, reward)))
                    : null
                : GuideChecklistProjection.QuestGuideFor(guides, quest.Name) is { } g
                    ? (g, new GuideStores([], [], quest))
                    : null;
            if (walked is not var (guide, stores)) continue;

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in GuideProgressRouter.Drawn(guide, stores.EpicRows))
                foreach (var p in PlacesOf(items, quest, o, stores))
                    keys.Add(ZoneMapFiles.IdentityKey(p.Zone));
            foreach (var k in keys)
            {
                if (k.Length == 0) continue;
                if (!byZone.TryGetValue(k, out var list)) byZone[k] = list = [];
                list.Add(quest);
            }
        }
        return new CatalogIndex(byZone, guides, items);
    }
}
