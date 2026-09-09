using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EQBuddy.Core;

/// <summary>What KIND of progression a guide walks. The requirements doc's set minus the
/// members nobody authors at MVP — adding one later is additive, and a member that exists
/// with no content behind it is schema cosplay (Fable, plan §2).</summary>
public enum GuideType
{
    NormalQuest,
    EpicQuest,
    PlaneOfSkyQuest,
    ZoneProgression,
    KeyingAccess,
    ClassQuest,
}

/// <summary>
/// How complete OUR DATA is for an objective — never how far the player has got.
///
/// The two axes are never conflated: authoring completeness is a fact about us, player
/// progress (open/ready/done) is a fact about the character. A guide can be 100% done while
/// half-stubbed, and fully authored at 0%. That is why this does not live on
/// <c>QuestPresentation.State</c> and never will (Founder lock 4a).
/// </summary>
public enum GuideAuthoring
{
    /// <summary>We can answer who, where and what, and we can say where we got it.</summary>
    Authored,
    /// <summary>We cannot, and <see cref="GuideObjective.StubNote"/> says so out loud. A
    /// hollow step renders as a stub with the share-back door, never as a finished one.</summary>
    Stub,
}

/// <summary>
/// Where a fact came from. <see cref="Title"/> is an EXACT eqlwiki page title, and that is
/// load-bearing twice over: it is the provenance a player can check, and it is the string
/// <c>refresh.py</c>'s <c>curated_flags</c> intersects with the week's changed pages to flag
/// this catalog for re-authoring. One field, two jobs (plan §2/§3).
/// </summary>
public sealed class GuideSource
{
    public string Url { get; set; } = "";
    /// <summary>The page title as SERVED, not as asked for (trap 3).</summary>
    public string Title { get; set; } = "";
    /// <summary>ISO date the fact was taken from that page — by an author, or by the harvest
    /// that carried the link. A source with no date cannot be aged, so validation requires it.</summary>
    public string RetrievedAt { get; set; } = "";
}

/// <summary>
/// The fundamental unit of guided progression: one thing the player does, with enough
/// structure to answer who / where / what without opening a browser.
///
/// <para><b>Empty string means absent</b>, as everywhere else in the curated catalogs —
/// there is no difference here between "null" and "nobody wrote it", and one representation
/// keeps the validation rules readable.</para>
/// </summary>
public sealed class GuideObjective
{
    public string Id { get; set; } = "";
    /// <summary>Position within the stage. Order plus <see cref="PrerequisiteObjectiveIds"/>
    /// answer "what is next" between them; there is deliberately no derived
    /// <c>NextObjectiveIds</c> field, because a second producer of sequence is trap 4's
    /// shape (plan §2, rejected list).</summary>
    public int Order { get; set; }

    /// <summary>One of <see cref="KnownObjectiveTypes"/>. A STRING and not an enum, on
    /// purpose and for the same reason as <c>SpawnEntry.SpawnType</c>: this is a
    /// hand-authored file, the requirements doc's type list will grow as PoS content lands,
    /// and a typo must fail a TEST rather than the catalog load.</summary>
    public string ObjectiveType { get; set; } = "";

    public string Title { get; set; } = "";
    /// <summary>The one line the active-step card leads with.</summary>
    public string ShortInstruction { get; set; } = "";

    // THE SIX QUESTIONS (David, 2026-09-09): *"who, what, where, when, why, how should be
    // the maximal number of things. We don't want to be redundant, but all must be
    // addressed."* Each one is DRAWN in exactly one place, because a row that says the same
    // fact three ways is the failure the first staged shot of this surface actually showed.
    //
    // **The six are a SCHEMA, not a licence to fill a blank** (Fable last-look, #480, and it
    // was catching a real one). The first cut of this catalog answered all six on all 48
    // authored steps — and did it with ten template sentences, nineteen of them asserting
    // *"one named on a spawn cycle … nobody has recorded a solo kill for us"* against a wiki
    // page that says nothing about spawn cycles, group size or soloing. Cited to that page.
    // Then fed verbatim into the share-back draft as "EQBuddy shows:", asking players to
    // correct our own invention. That is fabricated certainty wearing provenance, which is
    // the one thing this catalog exists to prevent.
    //
    // So: WHO, WHERE and WHAT are required of an Authored step, exactly as the signed §2 had
    // it. WHEN, WHY and HOW are OPTIONAL, and a step that leaves one empty simply does not
    // draw that line. Answering a question the sources do not answer is worse than leaving
    // it open — the share-back door is how it gets answered, by someone who was there.

    /// <summary>WHO: the npc, mob or mobs this step is about.</summary>
    public string Who { get; set; } = "";
    /// <summary>WHERE: zone plus prose directions. Never coordinates alone — a number pair
    /// is not an answer to "where do I go" for a player who is not already there.</summary>
    public string Where { get; set; } = "";
    /// <summary>WHAT: the action, with item names and quantities.</summary>
    public string What { get; set; } = "";
    /// <summary>WHEN: the timing that decides whether this step can be done NOW — a spawn
    /// cycle, an ordering against the guide's other steps, a thing to finish before leaving
    /// an island. Not a clock: "any time you are on the isle" is a real answer and the most
    /// common one.</summary>
    public string When { get; set; } = "";
    /// <summary>WHY: what this step BUYS, in the guide's own terms — which piece of which
    /// reward, or what it unlocks. Per STEP and not per guide: the reward's name is already
    /// the heading above the rows, and repeating it on each of them is the redundancy the
    /// six questions are supposed to remove.</summary>
    public string Why { get; set; } = "";
    /// <summary>HOW: the method, and what it costs — kill and loot, a hand-in with no
    /// combat, a long camp, a full group. Absorbed the old <c>EffortNote</c>, which had no
    /// reader and asked half of this question. Still deliberately NOT a ★ rating: authors
    /// must not invent stars before real ratings earn a model (Helm carry).</summary>
    public string How { get; set; } = "";

    /// <summary>Objectives IN THE SAME GUIDE that must be done first. Cross-stage is fine —
    /// PoS islands are stages and a turn-in depends on drops from several.</summary>
    public List<string> PrerequisiteObjectiveIds { get; set; } = [];

    /// <summary>For a Sky turn-in: the existing <c>QuestChecklistLayout.RewardKey</c>
    /// ("Class|Reward"). An objective carrying one renders its state FROM and writes THROUGH
    /// the existing turn-in store — the guide never keeps a second copy of that tick
    /// (trap 4, plan §4). Validation refuses a key <c>SkyQuestDefaults</c> does not know.</summary>
    public string RewardKey { get; set; } = "";

    /// <summary>Items this step is about, by name. The whole Phase-5 auto-detect surface
    /// area, paid for now: the <c>SkyLootAutoCheck</c> family can tick non-reward objectives
    /// later without a schema change. Nothing reads it for detection at MVP.</summary>
    public List<string> ItemNames { get; set; } = [];

    public GuideAuthoring Authoring { get; set; } = GuideAuthoring.Stub;

    /// <summary>Required on a Stub: what we do not know, in the player's terms, so the row
    /// can say it and offer the share-back door. "Incomplete" with no sentence behind it is
    /// the fabricated certainty the locks forbid, wearing a humble face.</summary>
    public string StubNote { get; set; } = "";

    public List<GuideSource> Sources { get; set; } = [];

    /// <summary>Every value the curated file may use, from the requirements doc §5.1. The
    /// validation holds the shipped catalog to this list.</summary>
    public static readonly string[] KnownObjectiveTypes =
    [
        "TalkToNpc", "Travel", "Explore", "Kill", "Loot", "Farm", "TurnIn", "Combine",
        "UseItem", "Activate", "ObtainKey", "UnlockAccess", "SurviveEncounter", "SpawnNamed",
        "TriggerEvent", "Collect", "ChooseReward", "ReturnToNpc", "Verify", "Custom",
    ];
}

/// <summary>A chapter of a guide. Plane of Sky islands are stages.</summary>
public sealed class GuideStage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Order { get; set; }
    /// <summary>What to know on ARRIVAL — the hazard or the shape of the place — as opposed
    /// to anything the player must do, which is an objective.</summary>
    public string ArrivalNote { get; set; } = "";
    public List<GuideObjective> Objectives { get; set; } = [];
}

/// <summary>
/// One curated progression guide. Sits BESIDE <see cref="QuestCatalog"/>, keyed to it by
/// <see cref="QuestName"/> — it is not a rewrite of the harvested index, which keeps its own
/// jobs (search, loot badging, the Companion index).
/// </summary>
public sealed class Guide
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public GuideType GuideType { get; set; } = GuideType.NormalQuest;
    /// <summary>The harvested <c>QuestEntry.Name</c> this guide walks, or "" where no wiki
    /// quest page corresponds. The link, not a copy: rewards and items stay over there.</summary>
    public string QuestName { get; set; } = "";
    public List<string> ZoneNames { get; set; } = [];
    public List<string> ApplicableClasses { get; set; } = [];
    /// <summary>Null when nobody has written it down — which is the honest answer far more
    /// often than a number is.</summary>
    public int? MinLevel { get; set; }
    public List<GuideStage> Stages { get; set; } = [];
    public List<GuideSource> Sources { get; set; } = [];

    /// <summary>Every objective, stage order then objective order — the reading order.</summary>
    public IEnumerable<GuideObjective> AllObjectives =>
        Stages.OrderBy(s => s.Order).SelectMany(s => s.Objectives.OrderBy(o => o.Order));

    public bool IsFullyAuthored =>
        AllObjectives.Any() && AllObjectives.All(o => o.Authoring == GuideAuthoring.Authored);

    public int StubCount => AllObjectives.Count(o => o.Authoring == GuideAuthoring.Stub);
}

/// <summary>
/// The shipped guide catalog — hand-authored, embedded, and <b>never auto-written</b>.
///
/// <para><b>Curated like the spawn catalog, and for the same reason.</b> The weekly wiki
/// refresh may FLAG a guide whose source page moved (that is what <see cref="GuideSource.Title"/>
/// is for); it may not rewrite one. A wrong instruction — "kill X on isle 4" when X is on
/// isle 3 — costs a player an evening, which is worse than a stub that says we do not know.</para>
///
/// <para><b>Validation is the product rule, not a convention.</b> <see cref="Validate"/> is
/// the executable form of Founder lock 4a: a hollow guide can never render as fully guided.
/// Every <see cref="GuideAuthoring.Authored"/> objective answers who + where + what and cites
/// a source; every <see cref="GuideAuthoring.Stub"/> says what is missing; prerequisites form
/// no cycle; every <see cref="GuideObjective.RewardKey"/> resolves against the Sky checklist
/// this layers on top of. <c>GuideCatalogTests</c> runs it over the shipped file AND over a
/// hollow fixture that must fail — a guard only proved green is vacuous (trap 34).</para>
///
/// <para>Nothing here fetches. The engine reads this embedded catalog only; the on-demand
/// wiki paths (<see cref="EqlWikiMobs"/>, <see cref="EqlWikiItems"/>) are untouched.</para>
/// </summary>
public sealed class GuideCatalog
{
    public List<Guide> Guides { get; set; } = [];

    private sealed class CatalogFile
    {
        public string Note { get; set; } = "";
        public List<Guide> Guides { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static GuideCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.GuideCatalog.json")
            ?? throw new InvalidOperationException("GuideCatalog.json missing from resources");
        var file = JsonSerializer.Deserialize<CatalogFile>(stream, JsonOpts)
            ?? throw new InvalidOperationException("GuideCatalog.json unreadable");
        return new GuideCatalog { Guides = file.Guides };
    }

    /// <summary>Parses catalog JSON in the shipped file's shape. Exists so a test can hold
    /// the validation to a fixture written the way an author would write one — a hollow
    /// guide has to fail from the same door the real catalog comes through.</summary>
    public static GuideCatalog FromJson(string json)
    {
        var file = JsonSerializer.Deserialize<CatalogFile>(json, JsonOpts)
            ?? throw new InvalidOperationException("guide catalog JSON unreadable");
        return new GuideCatalog { Guides = file.Guides };
    }

    public static GuideCatalog Default { get; } = LoadEmbedded();

    public Guide? Find(string guideId) =>
        Guides.FirstOrDefault(g => string.Equals(g.Id, guideId, StringComparison.OrdinalIgnoreCase));

    /// <summary>Guides that apply to a class, in catalog order. A class with none renders
    /// exactly today's checklist — that is the progressive cutover, and it is why this
    /// returns an empty list rather than anything clever.</summary>
    public IReadOnlyList<Guide> ForClass(string className) =>
        [.. Guides.Where(g => g.ApplicableClasses
            .Any(c => string.Equals(c, className, StringComparison.OrdinalIgnoreCase)))];

    /// <summary>Every Sky turn-in key the classic checklist knows, "Class|Reward". The set a
    /// guide's <see cref="GuideObjective.RewardKey"/> has to land in: a guide references the
    /// existing keys rather than minting its own, which is what keeps the checklist, the
    /// phone, achievements import and loot auto-tick all true while guides layer on top
    /// (plan §3 — <c>SkyQuestDefaults</c> is not touched at MVP).</summary>
    public static IReadOnlyCollection<string> SkyRewardKeys { get; } =
        new HashSet<string>(
            SkyQuestDefaults.Items.Select(i => QuestChecklistLayout.RewardKey(i.ClassName, i.Reward)),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sentences this catalog invented once and must never carry again.
    ///
    /// <para>Every one of these was written into 48 authored steps on 2026-09-09, cited to an
    /// eqlwiki page that says nothing of the kind, and then fed into the share-back draft as
    /// "EQBuddy shows:" — asking players to correct our own guess. Fable's last-look on #480
    /// caught it; the survey that proved it was ten distinct <c>when</c> values across 48
    /// steps.</para>
    ///
    /// <para>Substrings, matched case-insensitively, so a reworded template still trips. This
    /// is a NAMED and deliberately incomplete list: it stops these from coming back, it
    /// cannot stop the next invention, and the thing that actually prevents that is leaving a
    /// question empty when no source answers it.</para>
    /// </summary>
    public static readonly string[] FabricatedProse =
    [
        "on a spawn cycle",
        "nobody has recorded a solo kill",
        "Bring whatever it takes to hold a Sky named",
        "rather than a drop rate",
        "waits on the earliest of three spawns",
        "usually not the step that holds a reward up",
        "patience rather than a fight you have to plan",
    ];

    /// <summary>Everything wrong with this catalog, in the words a reviewer needs. Empty is
    /// the only shippable answer.</summary>
    public IReadOnlyList<string> Validate() => Validate(SkyRewardKeys);

    /// <summary>The rules, against a supplied reward-key set so the Sky coupling can be
    /// exercised from both sides in tests.</summary>
    public IReadOnlyList<string> Validate(IReadOnlyCollection<string> knownRewardKeys)
    {
        var problems = new List<string>();
        var rewardKeys = new HashSet<string>(knownRewardKeys, StringComparer.OrdinalIgnoreCase);
        var guideIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var guide in Guides)
        {
            var who = guide.Id.Length > 0 ? guide.Id : $"(unnamed guide '{guide.Name}')";

            if (guide.Id.Length == 0) problems.Add($"{who}: no id");
            else if (!guideIds.Add(guide.Id)) problems.Add($"{who}: duplicate guide id");
            if (guide.Name.Length == 0) problems.Add($"{who}: no name");
            if (guide.ApplicableClasses.Count == 0) problems.Add($"{who}: no applicable classes");
            if (guide.ZoneNames.Count == 0) problems.Add($"{who}: no zone names");
            if (guide.Stages.Count == 0) problems.Add($"{who}: no stages");
            problems.AddRange(SourceProblems(guide.Sources, who, requireAtLeastOne: true));

            ValidateStages(guide, who, rewardKeys, problems);
        }

        return problems;
    }

    private static void ValidateStages(
        Guide guide, string who, HashSet<string> rewardKeys, List<string> problems)
    {
        var stageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stageOrders = new HashSet<int>();
        var objectivesById = new Dictionary<string, GuideObjective>(StringComparer.OrdinalIgnoreCase);

        foreach (var stage in guide.Stages)
        {
            var stageWho = $"{who}/{(stage.Id.Length > 0 ? stage.Id : "(unnamed stage)")}";
            if (stage.Id.Length == 0) problems.Add($"{stageWho}: no id");
            else if (!stageIds.Add(stage.Id)) problems.Add($"{stageWho}: duplicate stage id");
            if (stage.Name.Length == 0) problems.Add($"{stageWho}: no name");
            // Order decides the reading order; two stages claiming one position makes
            // "what is next" a coin toss.
            if (!stageOrders.Add(stage.Order)) problems.Add($"{stageWho}: duplicate stage order {stage.Order}");
            if (stage.Objectives.Count == 0) problems.Add($"{stageWho}: no objectives");

            var objectiveOrders = new HashSet<int>();
            foreach (var objective in stage.Objectives)
            {
                var oWho = $"{stageWho}/{(objective.Id.Length > 0 ? objective.Id : "(unnamed objective)")}";
                if (objective.Id.Length == 0) problems.Add($"{oWho}: no id");
                else if (objectivesById.ContainsKey(objective.Id))
                    problems.Add($"{oWho}: duplicate objective id within the guide");
                else objectivesById[objective.Id] = objective;

                if (!objectiveOrders.Add(objective.Order))
                    problems.Add($"{oWho}: duplicate objective order {objective.Order} in its stage");

                if (!GuideObjective.KnownObjectiveTypes.Contains(objective.ObjectiveType, StringComparer.Ordinal))
                    problems.Add($"{oWho}: objective type '{objective.ObjectiveType}' is not one of "
                        + string.Join(", ", GuideObjective.KnownObjectiveTypes));

                if (objective.Title.Length == 0) problems.Add($"{oWho}: no title");
                if (objective.ShortInstruction.Length == 0) problems.Add($"{oWho}: no short instruction");

                problems.AddRange(AuthoringProblems(objective, oWho));

                if (objective.RewardKey.Length > 0 && !rewardKeys.Contains(objective.RewardKey))
                    problems.Add($"{oWho}: reward key '{objective.RewardKey}' is not a Sky checklist key "
                        + "(QuestChecklistLayout.RewardKey over SkyQuestDefaults) — a guide references "
                        + "the existing turn-in, it does not mint a new one");
            }
        }

        foreach (var objective in guide.AllObjectives)
            foreach (var prerequisite in objective.PrerequisiteObjectiveIds)
                if (!objectivesById.ContainsKey(prerequisite))
                    problems.Add($"{who}/{objective.Id}: prerequisite '{prerequisite}' is not an "
                        + "objective of this guide");

        problems.AddRange(CycleProblems(who, objectivesById));
    }

    /// <summary>
    /// The must-list (trap 34): an Authored objective ANSWERS who + where + what and says
    /// where that came from; a Stub says what is missing instead. Forbidding the wrong thing
    /// cannot see a missing thing, so this is written as the positive claim.
    ///
    /// Blunt on purpose. A step whose "who" is genuinely nobody is a schema conversation —
    /// not a blank field and not the word "n/a", both of which read as authored and are not.
    /// </summary>
    private static IEnumerable<string> AuthoringProblems(GuideObjective objective, string who)
    {
        if (objective.Authoring == GuideAuthoring.Authored)
        {
            // WHO, WHERE, WHAT — required, as the signed §2 had it. WHEN/WHY/HOW are the
            // schema's other three and stay OPTIONAL: see the note on the six questions.
            if (objective.Who.Length == 0) yield return $"{who}: authored but does not say WHO";
            if (objective.Where.Length == 0) yield return $"{who}: authored but does not say WHERE";
            if (objective.What.Length == 0) yield return $"{who}: authored but does not say WHAT";

            // The regression guard for what went wrong on #480 — a curated must-NOT list,
            // paired with the must-list above (trap 34 works in both directions). These are
            // the exact sentences the first cut invented and cited to pages that do not
            // contain them; a template that reappears on 19 steps is the SHAPE of the bug,
            // and naming the strings is the only version of it a test can be sure about.
            foreach (var claim in new[] { objective.When, objective.How, objective.Why })
                foreach (var banned in FabricatedProse)
                    if (claim.Contains(banned, StringComparison.OrdinalIgnoreCase))
                        yield return $"{who}: says \"{banned}\" — no source we cite says it. "
                            + "WHEN/HOW are optional; an unanswerable one is left empty and the "
                            + "share-back door is how it gets filled in (Fable last-look, #480)";
            if (objective.Sources.Count == 0)
                yield return $"{who}: authored but cites no source — provenance is what separates "
                    + "curation from invention, and it is the weekly refresh's flag";
            if (objective.StubNote.Length > 0)
                yield return $"{who}: authored but carries a stub note — one of those is a lie";
        }
        else
        {
            if (objective.StubNote.Length == 0)
                yield return $"{who}: stub with no note — \"incomplete\" with no sentence behind it "
                    + "tells the player nothing and gives the share-back door nothing to carry";
        }

        foreach (var problem in SourceProblems(objective.Sources, who, requireAtLeastOne: false))
            yield return problem;
    }

    private static IEnumerable<string> SourceProblems(
        List<GuideSource> sources, string who, bool requireAtLeastOne)
    {
        if (requireAtLeastOne && sources.Count == 0)
            yield return $"{who}: no sources";

        foreach (var source in sources)
        {
            if (source.Url.Length == 0) yield return $"{who}: a source has no url";
            if (source.Title.Length == 0)
                yield return $"{who}: a source has no page title — the title IS the string the "
                    + "weekly refresh intersects with changed wiki pages, so a blank one is a row "
                    + "no wiki correction can ever reach";
            if (!DateOnly.TryParseExact(source.RetrievedAt, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                yield return $"{who}: source '{source.Title}' has no yyyy-MM-dd retrievedAt "
                    + $"(got '{source.RetrievedAt}') — a source with no date cannot be aged";
        }
    }

    /// <summary>Prerequisite cycles, by three-colour DFS. A cycle is not a data smell here:
    /// "next" is computed as the first objective whose prerequisites are done, so a cycle is
    /// a guide that can never name a next step and never says why.</summary>
    private static IEnumerable<string> CycleProblems(
        string who, Dictionary<string, GuideObjective> objectivesById)
    {
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();

        foreach (var id in objectivesById.Keys)
            Walk(id, []);

        return problems;

        void Walk(string id, List<string> path)
        {
            if (state.TryGetValue(id, out var seen))
            {
                if (seen == 1)
                    problems.Add($"{who}: prerequisite cycle "
                        + string.Join(" → ", path.SkipWhile(p => !string.Equals(p, id, StringComparison.OrdinalIgnoreCase)))
                        + $" → {id}");
                return;
            }

            state[id] = 1;
            path.Add(id);
            if (objectivesById.TryGetValue(id, out var objective))
                foreach (var prerequisite in objective.PrerequisiteObjectiveIds)
                    if (objectivesById.ContainsKey(prerequisite))
                        Walk(prerequisite, path);
            path.RemoveAt(path.Count - 1);
            state[id] = 2;
        }
    }
}
