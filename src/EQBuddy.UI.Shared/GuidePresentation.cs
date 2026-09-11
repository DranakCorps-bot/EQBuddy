using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Every word the guide surface says, in one place.
///
/// <para>The desktop draws WPF, the shell draws WPF, the phone draws HTML — and all three
/// have to say the same thing about the same step or the guide is three products. So no
/// guide string is spelled in XAML or in <c>index.html</c>; they call in here. The same rule
/// <see cref="LootPresentation"/> and <see cref="QuestPresentation"/> already carry, for the
/// same reason (#184: the three surfaces had already drifted once).</para>
///
/// <para>Framework-free, so it is unit-tested rather than eyeballed.</para>
/// </summary>
public static class GuidePresentation
{
    /// <summary>What leads a stub row's caption. The sentence after it is the author's own
    /// <see cref="GuideObjective.StubNote"/> — this only says whose fault it is, and it is
    /// ours rather than the player's.</summary>
    public const string StubLead = "Wiki incomplete —";

    /// <summary>
    /// What a reward PAYS, in ONE LINE: the item you end up with and the pieces it costs.
    ///
    /// <para>The question a player scanning a folded list is actually asking is "what do I
    /// get" (David, 2026-09-09), and on a collapsed heading the item rows that used to answer
    /// it are not on screen. Built from the reward's own checklist rows, so it says what the
    /// quest costs rather than a number.</para>
    ///
    /// <para>This is the SHORT answer, and it survives beside <see cref="RewardCard"/>
    /// because the two are different facts rather than two copies of one: a line that fits
    /// under every heading on a phone, and the item's own stats block.</para></summary>
    public static string RewardSummary(string reward, IReadOnlyList<SkyQuestChecklistItem> items)
    {
        var head = "Rewards the " + reward + ".";
        var cost = RewardCost(items);
        return cost.Length == 0 ? head : head + " " + cost;
    }

    /// <summary>
    /// The reward item's OWN STATS BLOCK, and under it the pieces it costs — the answer to
    /// "is this worth doing".
    ///
    /// <para>The Founder, 2026-09-10, on what he had meant by "the hover should show the
    /// reward": *"show the reward as it does in EQLWiki or when we mouse over any item in
    /// EQBuddy."* The item window, not a description of it. "AC: 15, STR +16, Class: WAR" is
    /// what decides whether a Sky quest is worth the evening; "Rewards the Azure Ruby Ring"
    /// only repeats the heading the cursor is already sitting on.</para>
    ///
    /// <para><b>The block is quoted verbatim and never composed.</b> It is the game's own
    /// item window as wiki editors transcribed it, and the shipped <c>ItemCatalog</c> already
    /// carries it for exactly this purpose ("stats on hover", 2026-08-13) — so this costs no
    /// request to eqlwiki, works offline, and cannot drift from what a live lookup of the
    /// same revision would say. Re-wording it here would be inventing game data with a
    /// citation attached (trap 73).</para>
    ///
    /// <para>Empty when we have no block, and the caller falls back to
    /// <see cref="RewardSummary"/> rather than showing a blank. Two of the 95 Sky rewards are
    /// in that state today and both are OUR naming bugs, not gaps in the wiki.</para></summary>
    public static string RewardCard(string? statsText, IReadOnlyList<SkyQuestChecklistItem> items)
    {
        var block = (statsText ?? "").Trim();
        if (block.Length == 0) return "";

        var cost = RewardCost(items);
        return cost.Length == 0 ? block : block + "\n\n" + cost;
    }

    /// <summary>
    /// What a guided EPIC group is called, and the reason it is not a reward name.
    ///
    /// <para>A Sky group is one reward, so its heading can be one. <b>An epic is not:</b>
    /// eqlwiki's own <c>== Rewards ==</c> section lists three items for the Warrior, four for
    /// the Shadow Knight and six for the Necromancer, and no page names one of them as "the
    /// epic". Picking one would be EQBuddy departing from the wiki on game data by choosing —
    /// which is the one thing the standing rule forbids (David, 2026-08-14), and which David
    /// cannot check for us at level 29.</para>
    ///
    /// <para>So the heading says what the tab says, and the hover
    /// (<see cref="EpicRewardSummary"/>) lists every reward the page does.</para></summary>
    public const string EpicTitle = "Epic 1.0";

    /// <summary>Every reward the class's epic page lists, verbatim as the catalog joined them —
    /// not one picked out. Empty when the catalog has none and the hover falls back to
    /// nothing at all, which is honest where "Rewards ." is not.</summary>
    public static string EpicRewardSummary(string rewards) =>
        rewards.Trim().Length == 0 ? "" : "Rewards " + rewards.Trim() + ".";

    /// <summary>WHY, for an epic guide's card. <see cref="CardWhy"/> names the reward, and an
    /// epic has several (see <see cref="EpicTitle"/>) — so this names the thing every one of
    /// those rewards belongs to, which is a structural fact about the quest and not a
    /// choice.</summary>
    public static string EpicCardWhy(string className) =>
        className.Trim().Length == 0 ? "" : "Works toward your " + className.Trim() + " epic.";

    /// <summary>The pieces a reward costs, as a sentence — the one part of the hover that is
    /// about the QUEST rather than about the item.</summary>
    private static string RewardCost(IReadOnlyList<SkyQuestChecklistItem> items)
    {
        var pieces = items
            .Select(i => i.QuestItem.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return pieces.Count == 0 ? "" : "Needs " + string.Join(", ", pieces) + ".";
    }

    /// <summary>
    /// What a NORMAL quest's guide block is headed, on the General tab's detail pane and on
    /// the phone's quest card (DRA-46).
    ///
    /// <para>One word, and deliberately not the quest's name: the pane's title and the phone
    /// card's title are already that, a line or two above, and a heading that repeated it
    /// would spend the line that carries the count. It leads with the same word
    /// <see cref="GuidedCaption"/> does, so the block and its caption read as one thing.</para></summary>
    public const string QuestHeading = "Guide";

    /// <summary>What the fold control says it will do. A collapsed quest shows its heading,
    /// its counts and its caption; expanding it brings back the card and the steps.</summary>
    public static string FoldTip(bool collapsed) => collapsed
        ? "Show this quest's steps"
        : "Fold this quest away — the heading, its count and its caption stay";

    /// <summary>The fold control's face: a plus to open, a minus to close (David,
    /// 2026-09-09). A symbol rather than "Show steps" / "Hide steps" — six of those stacked
    /// down a folded class list is more text than the headings they sit under, and the whole
    /// point of folding is that the list reads at a glance. The words survive on the hover,
    /// where they cost nothing.</summary>
    public static string FoldFace(bool collapsed) => collapsed ? "+" : "−";

    /// <summary>The label on the row-end share-back door, and the whole promise it makes.</summary>
    public const string ImproveLabel = "Improve this step";

    /// <summary>The tooltip. Says what opens and what does NOT happen, because the door is
    /// one click from a public post and a player who is unsure will not press it.</summary>
    public const string ImproveTip =
        ImproveLabel + " — opens a prefilled discussion; nothing is sent until you post.";

    /// <summary>
    /// The caption under a guided group's heading: ONLY what the heading cannot already say.
    ///
    /// <para>"Guide · 1 stub". It used to lead with "Guide · 1 of 4" — the count the heading
    /// beside it already draws as "1/4 · in progress". Folding made that expensive rather
    /// than merely redundant: with the rows away, two lines saying the same number were most
    /// of what a class list contained. Bevel's SIGNED one-liner — <i>the heading owns
    /// pieces/ready; the caption draws only when it ADDS stubs or skipped</i> — and Fable's
    /// #491 defect 3, both off the folded frame.</para>
    ///
    /// <para><b>Progress left; the stub count could not.</b> A player is entitled to know in
    /// the same glance that one of these steps is one we cannot actually give directions for
    /// — that is Founder lock 4a, and it is the whole reason this line exists at all. Skipped
    /// is named only when there is one, because "0 skipped" is noise on nearly every
    /// row.</para></summary>
    public static string GuidedCaption(int skipped, int stubs) =>
        GuidedCaption(skipped, stubs, leadWithGuide: true);

    /// <summary>
    /// The same caption, for a surface whose HEADING already says "Guide" (DRA-46).
    ///
    /// <para>On Sky and Epic the heading is a reward or a class, so the caption's lead is
    /// what marks the group as guided at all. On the General tab's detail pane the pane's own
    /// title is the quest and the block's heading is <see cref="QuestHeading"/> — so the lead
    /// printed the word twice on consecutive lines, which the first staged frame showed
    /// (<c>shell-quests-general-guide</c>) and no unit test could have.</para>
    ///
    /// <para>A parameter rather than a second string: the WORDS stay in one place, and what
    /// the caller is stating is a fact about its own layout, not a different caption.</para></summary>
    public static string GuidedCaption(int skipped, int stubs, bool leadWithGuide)
    {
        if (skipped == 0 && stubs == 0) return "";

        var parts = new List<string>();
        if (leadWithGuide) parts.Add("Guide");
        if (skipped > 0) parts.Add($"{skipped} skipped");
        if (stubs > 0) parts.Add($"{stubs} {(stubs == 1 ? "stub" : "stubs")}");
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// The dim line under a guide row's title: WHO and WHERE, joined the way the classic
    /// checklist already joins its NPC and drop location.
    ///
    /// <para><b>Why only two of the six here.</b> Each question is drawn in exactly one place
    /// (David, 2026-09-09: *"we don't want to be redundant, but all must be addressed"*).
    /// WHAT is the row's own title; WHEN, WHY and HOW are on the hover. WHEN was on this line
    /// until Fable's #480 last-look: it is optional and usually empty now, and a row line that
    /// sometimes has a third clause and usually does not reads as a rendering bug.</para>
    ///
    /// <para>An empty part is dropped rather than leaving a dangling separator; a Stub has
    /// none of them and gets its note instead.</para></summary>
    public static string RowDetail(GuideObjective objective) =>
        Join(objective.Who, objective.Where);

    /// <summary>
    /// The hover: all six questions, labelled, for one step. The row shows what you need to
    /// decide; this is where the rest lives, so nothing is unanswerable and nothing is said
    /// twice on screen.
    ///
    /// <para>A Stub answers the one question it can — what we do not know — because a
    /// labelled list of blanks reads as a broken row rather than an honest one.</para></summary>
    public static string RowTooltip(GuideObjective objective)
    {
        if (objective.Authoring == GuideAuthoring.Stub)
            return StubLead + " " + objective.StubNote;

        // SENTENCES, not labels (David, 2026-09-09). Every field in the schema is already
        // written as prose, so the labels added nothing but the shape of a form. All six
        // questions are still here and still each drawn once — the reader is told rather
        // than shown a table.
        var lines = new List<string>();
        Add(Directions(objective));
        Add(objective.What);
        Add(objective.Why);
        Add(objective.When);
        Add(objective.How);
        return string.Join("\n", lines);

        void Add(string value)
        {
            if (value.Trim().Length > 0) lines.Add(value.Trim());
        }
    }

    private static string Join(params string[] parts) =>
        string.Join(" · ", parts.Select(p => p.Trim()).Where(p => p.Length > 0));

    // ---- the active-step card (P1d, requirements §12) --------------------------------

    /// <summary>The card's lead-in. Upper-case in the mock and kept that way: it is the one
    /// thing on the tab that says "do this next" rather than "here is everything".</summary>
    public const string NextLead = "NEXT:";

    /// <summary>What the card says when the guide has no step left to name.</summary>
    public const string AllDone = "Every step is done.";

    /// <summary>Every remaining step struck out. Distinct from done on purpose: the player
    /// said "not doing these", and a card claiming completion would be putting words in
    /// their mouth.</summary>
    public const string AllSkipped = "Every step left is skipped.";

    public const string DoneLabel = "Done";
    public const string SkipLabel = "Skip";
    public const string SkipTip =
        "Not doing this one. It strikes through and the card moves on; you can take it back.";
    public const string BeforeLeavingLead = "⚠ Before leaving:";

    /// <summary>
    /// The next step: the first objective in reading order that is not done, not skipped, and
    /// whose prerequisites are all done.
    ///
    /// <para>Reading order and prerequisites answer this BETWEEN them, which is why the schema
    /// has no derived <c>NextObjectiveIds</c> — a second producer of sequence is trap 4's
    /// shape. <c>Validate()</c> guarantees the prerequisite graph is acyclic, so a guide can
    /// always name a next step or say why it has none.</para>
    ///
    /// <para>A step whose prerequisites are SKIPPED rather than done is deliberately not
    /// offered: skipping "loot the amulet" does not make "hand in the amulet" doable, and a
    /// card that walked you to a turn-in you cannot make is worse than one that says
    /// nothing.</para></summary>
    public static GuideObjective? NextObjective(
        Guide guide, Func<GuideObjective, bool> isDone, Func<GuideObjective, bool> isSkipped) =>
        NextObjective([.. guide.AllObjectives], isDone, isSkipped);

    /// <summary>The next step over an explicit set of objectives — the DRAWN ones
    /// (<see cref="GuideProgressRouter.Drawn"/>), which on the Epic tab is narrower than the
    /// guide when the classic-era lens is on. A card naming a step whose row the lens removed
    /// would be pointing at a box that is not on the screen.</summary>
    public static GuideObjective? NextObjective(
        IReadOnlyList<GuideObjective> objectives,
        Func<GuideObjective, bool> isDone, Func<GuideObjective, bool> isSkipped)
    {
        var done = new HashSet<string>(
            objectives.Where(isDone).Select(o => o.Id), StringComparer.OrdinalIgnoreCase);

        foreach (var objective in objectives)
        {
            if (done.Contains(objective.Id) || isSkipped(objective)) continue;
            if (objective.PrerequisiteObjectiveIds.All(done.Contains)) return objective;
        }
        return null;
    }

    /// <summary>The lead of the third sentence: a step is OPEN, and the only thing stopping
    /// the card offering it is a prerequisite the player struck out.</summary>
    public const string BlockedBySkipLead = "The hand-in waits on a step you skipped: ";

    /// <summary>
    /// What a step is CALLED on screen — its instruction where it has one, its title where it
    /// does not.
    ///
    /// <para>One producer (trap 4): the projection drew this expression for the row and again
    /// for the card's lead. It is what a step is called when it is being DRAWN; a step being
    /// REFERRED TO from somewhere else is named by <see cref="StepName"/>.</para>
    ///
    /// <para>The last fallback is <c>What</c>, and it is what a
    /// <see cref="GuideAuthoring.Transcribed"/> step lands on: the page's own sentence is the
    /// row, so that state carries no title and no short instruction at all rather than two more
    /// copies of one string (<c>GuideCatalog.Validate</c> refuses them).</para></summary>
    public static string StepTitle(GuideObjective objective) =>
        objective.ShortInstruction.Length > 0 ? objective.ShortInstruction
        : objective.Title.Length > 0 ? objective.Title
        : objective.What;

    /// <summary>
    /// What a step is called when something ELSE names it — the blocked-by-skip sentence, the
    /// turn-in row's "after: …", the before-leaving warning.
    ///
    /// <para>Its <c>Title</c> where it has one, because a cross-reference is a NAME and not an
    /// instruction: a card calling the same step "Loot a Wind Rune Azia from Plane of Sky
    /// trash." would be the surface naming one thing two ways. A step with no title falls
    /// through to what it is DRAWN as — which for a transcribed step is the page's sentence,
    /// and a sentence is a great deal better than the empty string these read before this
    /// existed.</para></summary>
    public static string StepName(GuideObjective objective) =>
        objective.Title.Length > 0 ? objective.Title : StepTitle(objective);

    /// <summary>
    /// What the card says when <see cref="NextObjective"/> names nothing. THREE answers, not
    /// two: finished, put down, or blocked.
    ///
    /// <para><b>Why the third exists</b> (Fable, #491 last-look): this returned
    /// <see cref="AllSkipped"/> for anything that was not all-done, so a guide whose turn-in
    /// was still OPEN and merely gated on a skipped piece told the player "every step left is
    /// skipped" — about a step they had not skipped. The turn-in is the one objective in the
    /// shipped catalog that carries prerequisites at all (95 of 95;
    /// <c>OnlyATurnInCarriesPrerequisitesSoTheBlockedSentenceCanNameTheHandIn</c> holds that
    /// invariant open for Delivery 3), which is what lets the sentence name it.</para>
    ///
    /// <para>The blockers are the skipped prerequisites themselves, BY TITLE — the same way
    /// <see cref="AfterDetail"/> and <see cref="BeforeLeaving"/> already name another step.
    /// A cross-reference is a NAME, not an instruction: the turn-in row five lines above this
    /// card says "after: Collect Wind Rune Azia", and a card calling the same step "Loot a
    /// Wind Rune Azia from Plane of Sky trash." would be the surface naming one thing two
    /// ways. (The ROW draws the instruction — that is a rendering choice, and
    /// <see cref="StepTitle"/> is where it lives.)</para></summary>
    public static string NoNextStep(
        Guide guide, Func<GuideObjective, bool> isDone, Func<GuideObjective, bool> isSkipped) =>
        NoNextStep([.. guide.AllObjectives], isDone, isSkipped);

    /// <summary>The finished-state sentence over an explicit set of objectives — see
    /// <see cref="NextObjective(IReadOnlyList{GuideObjective}, Func{GuideObjective, bool}, Func{GuideObjective, bool})"/>
    /// for why the set is passed rather than taken off the guide.</summary>
    public static string NoNextStep(
        IReadOnlyList<GuideObjective> objectives,
        Func<GuideObjective, bool> isDone, Func<GuideObjective, bool> isSkipped)
    {
        if (objectives.All(isDone)) return AllDone;

        var done = new HashSet<string>(
            objectives.Where(isDone).Select(o => o.Id), StringComparer.OrdinalIgnoreCase);
        var byId = new Dictionary<string, GuideObjective>(StringComparer.OrdinalIgnoreCase);
        foreach (var objective in objectives) byId[objective.Id] = objective;

        var blockers = objectives
            .Where(o => !done.Contains(o.Id) && !isSkipped(o))
            .SelectMany(o => o.PrerequisiteObjectiveIds)
            .Where(id => !done.Contains(id)
                && byId.TryGetValue(id, out var prerequisite) && isSkipped(prerequisite))
            .Select(id => StepName(byId[id]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return blockers.Count > 0
            ? BlockedBySkipLead + string.Join(", ", blockers) + "."
            : AllSkipped;
    }

    /// <summary>WHY, in the card's terms — the reward this step is working toward, said as a
    /// sentence. The reward name and not the step's own <see cref="GuideObjective.Why"/>: the
    /// card is lifted OUT of its group, so the heading that would have named the reward is not
    /// beside it.</summary>
    public static string CardWhy(string reward) => "Works toward the " + reward + ".";

    /// <summary>
    /// WHERE and WHO as one natural sentence — "Travel to Plane of Sky - Isle 5, then find
    /// The Spiroc Lord."
    ///
    /// <para><b>Why this replaced labelled fields</b> (David, 2026-09-09): the card read as a
    /// form — <c>Where:</c> / <c>What:</c> / <c>Who:</c> stacked under an instruction that had
    /// already said most of it. Labels are how the DATA is shaped; they are not how a person
    /// tells you where to go. The verb follows the objective type, because "find" is wrong for
    /// an NPC you are meant to talk to and "travel to" is wrong when you are already there.</para>
    ///
    /// <para>Empty when the step answers neither, and it drops the half it does not have
    /// rather than composing round a blank.</para></summary>
    public static string Directions(GuideObjective objective)
    {
        var where = objective.Where.Trim().TrimEnd('.');
        var who = objective.Who.Trim().TrimEnd('.');
        var verb = objective.ObjectiveType switch
        {
            "TalkToNpc" or "ReturnToNpc" or "TurnIn" => "speak to",
            "Kill" or "SpawnNamed" or "SurviveEncounter" => "fight",
            _ => "find",
        };

        if (where.Length > 0 && who.Length > 0) return $"Travel to {where}, then {verb} {who}.";
        if (where.Length > 0) return $"Travel to {where}.";
        return who.Length > 0 ? $"{char.ToUpperInvariant(verb[0])}{verb[1..]} {who}." : "";
    }

    /// <summary>
    /// WHAT, but only when it says something the instruction did not.
    ///
    /// <para>A guide's <c>ShortInstruction</c> and its <c>What</c> usually describe one action
    /// twice — "Kill The Spiroc Lord on Isle 5 and loot the Spiroc Battle Staff." against
    /// "Kill The Spiroc Lord and loot Spiroc Battle Staff (1)." Printing both is how the card
    /// became a form. So the detail line appears only when it is not already covered, judged
    /// on the words that carry meaning rather than on an exact match.</para></summary>
    public static string ExtraDetail(GuideObjective objective)
    {
        var what = objective.What.Trim();
        if (what.Length == 0) return "";

        // Against what the card's LEAD actually draws (StepTitle), not against the raw
        // ShortInstruction field. On a transcribed step those differ: the lead IS the What
        // sentence, so comparing against the empty ShortInstruction would score every word as
        // novel and print the same sentence twice, one line apart.
        var said = new HashSet<string>(
            Words(StepTitle(objective)), StringComparer.OrdinalIgnoreCase);
        var novel = Words(what).Where(w => !said.Contains(w)).ToList();
        // A handful of new words is a quantity or a caveat worth showing; one or two is
        // punctuation noise dressed as news.
        return novel.Count >= 3 ? what : "";

        static IEnumerable<string> Words(string text) =>
            text.Split([' ', ',', '.', '(', ')', ';', ':'], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2);
    }

    /// <summary>
    /// The "you are about to strand yourself" line, and the one piece of card logic that is
    /// about the STAGE rather than the step.
    ///
    /// <para>Shown only when the next step is on a DIFFERENT stage while this one still has
    /// open steps — which on Plane of Sky means the next thing to do is on another island and
    /// you have unfinished business on this one. Empty otherwise; a warning that is always on
    /// is furniture.</para>
    ///
    /// <para>This is §5.5's <c>DoBeforeLeaving</c> intent without a new field: the stages and
    /// the open steps already say it, and a hand-authored per-stage warning would be a second
    /// producer of a fact the structure already carries.</para></summary>
    public static string BeforeLeaving(
        Guide guide, GuideObjective next, Func<GuideObjective, bool> isDone,
        Func<GuideObjective, bool> isSkipped) =>
        BeforeLeaving(guide, [.. guide.AllObjectives], next, isDone, isSkipped);

    /// <summary>The stranding warning, over the DRAWN objectives. A step the classic-era lens
    /// removed cannot strand anybody: its row is not on the tab, so a warning naming it would
    /// send the player looking for a box that is not there.</summary>
    public static string BeforeLeaving(
        Guide guide, IReadOnlyList<GuideObjective> drawn, GuideObjective next,
        Func<GuideObjective, bool> isDone, Func<GuideObjective, bool> isSkipped)
    {
        var nextStage = guide.Stages.FirstOrDefault(s => s.Objectives.Any(o =>
            string.Equals(o.Id, next.Id, StringComparison.OrdinalIgnoreCase)));
        if (nextStage is null) return "";

        var visible = new HashSet<string>(drawn.Select(o => o.Id), StringComparer.OrdinalIgnoreCase);
        var stranded = guide.Stages
            .Where(s => s.Order < nextStage.Order)
            .SelectMany(s => s.Objectives)
            .Where(o => visible.Contains(o.Id))
            .Where(o => !isDone(o) && !isSkipped(o))
            .Select(StepName)
            .ToList();

        return stranded.Count == 0
            ? ""
            : BeforeLeavingLead + " " + string.Join(", ", stranded);
    }

    /// <summary>What a turn-in row says while its pieces are still outstanding. Names the
    /// steps rather than counting them: "2 steps first" tells a player nothing they can act
    /// on, and the whole point of the guide is that the next thing to do has a name.</summary>
    public static string AfterDetail(IEnumerable<string> outstandingTitles)
    {
        var names = outstandingTitles.Where(t => t.Trim().Length > 0).Select(t => t.Trim()).ToList();
        return names.Count == 0 ? "" : "after: " + string.Join(", ", names);
    }

    /// <summary>
    /// A prefilled discussion draft about ONE guide step: what EQBuddy currently shows, and
    /// a blank line for what the player saw in game.
    ///
    /// <para><b>Nothing is attached.</b> No log line, no character name, no inventory, no
    /// zone — the entire body is composed from the CATALOG and is on the player's screen in
    /// their own browser before anything is posted. Same trust shape as the quest report
    /// door and <c>FeedbackWindow</c>, and the reason this needs no new consent: it sends
    /// nothing off the machine that the player has not read (consequence 8 untouched).</para>
    ///
    /// <para><b>Wiki first.</b> eqlwiki is the source and EQBuddy is the tool that helps it
    /// update (David, 2026-08-22), so the body names the page's own edit link when the step
    /// cites a source — the strongest fix is the one that reaches every player, not just
    /// ours.</para>
    /// </summary>
    public static string ImproveUrl(Guide guide, GuideObjective objective)
    {
        var source = objective.Sources.Count > 0 ? objective.Sources[0]
            : guide.Sources.Count > 0 ? guide.Sources[0] : null;

        // All six, labelled, so the reporter can see exactly which one is wrong — and so the
        // draft says the same thing the row's hover said, rather than a summary of it.
        var shows = RowTooltip(objective);

        var body =
            $"Guide: {guide.Name}\nStep: {objective.Title}\n" +
            $"Guide id: {guide.Id} · Step id: {objective.Id}\n\n" +
            $"EQBuddy shows:\n{shows}\n\n" +
            "What you saw in game:\n\n\n" +
            "---\nNote: EQBuddy follows eqlwiki.com, so if the wiki page is silent or wrong, " +
            "editing the page is the strongest fix — it reaches every player, not just " +
            "EQBuddy's. If the page is right and EQBuddy read it wrong, this is exactly the " +
            "right place.\n";

        if (source is { Title.Length: > 0 })
            body += $"Source page: {source.Title} — edit it here: {WikiContribution.EditUrl(source.Title)}\n";

        return "https://github.com/DranakCorps-bot/EQBuddy/discussions/new?category=q-a" +
            "&title=" + Uri.EscapeDataString($"Guide step: {guide.Name} / {StepName(objective)}") +
            "&body=" + Uri.EscapeDataString(body);
    }
}
