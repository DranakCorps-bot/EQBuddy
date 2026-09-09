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

    /// <summary>The label on the row-end share-back door, and the whole promise it makes.</summary>
    public const string ImproveLabel = "Improve this step";

    /// <summary>The tooltip. Says what opens and what does NOT happen, because the door is
    /// one click from a public post and a player who is unsure will not press it.</summary>
    public const string ImproveTip =
        ImproveLabel + " — opens a prefilled discussion; nothing is sent until you post.";

    /// <summary>The caption under a guided group's heading: how many steps are done, and how
    /// many of them we could not fully write down.
    ///
    /// <para>"Guide · 0 of 3 · 1 stub". The stub count is on the SAME line as the progress
    /// deliberately: a player reading "0 of 3" is entitled to know in the same glance that
    /// one of those three is a step we cannot actually give directions for. Hiding it one
    /// level down is how a hollow guide reads as a finished one (Founder lock 4a).</para>
    ///
    /// <para>Skipped is named only when there is one, because "0 skipped" is noise on the
    /// overwhelming majority of rows.</para></summary>
    public static string GuidedCaption(int done, int skipped, int total, int stubs)
    {
        var caption = $"Guide · {done} of {total}";
        if (skipped > 0) caption += $" · {skipped} skipped";
        if (stubs > 0) caption += $" · {stubs} {(stubs == 1 ? "stub" : "stubs")}";
        return caption;
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

        var lines = new List<string>();
        Add("What", objective.What);
        Add("Who", objective.Who);
        Add("Where", objective.Where);
        Add("When", objective.When);
        Add("Why", objective.Why);
        Add("How", objective.How);
        return string.Join("\n", lines);

        void Add(string label, string value)
        {
            if (value.Trim().Length > 0) lines.Add(label + ": " + value.Trim());
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
        Guide guide, Func<GuideObjective, bool> isDone, Func<GuideObjective, bool> isSkipped)
    {
        var done = new HashSet<string>(
            guide.AllObjectives.Where(isDone).Select(o => o.Id), StringComparer.OrdinalIgnoreCase);

        foreach (var objective in guide.AllObjectives)
        {
            if (done.Contains(objective.Id) || isSkipped(objective)) continue;
            if (objective.PrerequisiteObjectiveIds.All(done.Contains)) return objective;
        }
        return null;
    }

    /// <summary>What the card says when <see cref="NextObjective"/> names nothing: whether the
    /// guide is finished or merely put down. A guide with steps left that are all blocked by
    /// skipped prerequisites reads as skipped, because that is what the player chose.</summary>
    public static string NoNextStep(
        Guide guide, Func<GuideObjective, bool> isDone, Func<GuideObjective, bool> isSkipped) =>
        guide.AllObjectives.All(isDone) ? AllDone : AllSkipped;

    /// <summary>"for: Runed Wind Amulet" — WHY, in the card's terms. The reward name and not
    /// the step's own <see cref="GuideObjective.Why"/>: the card is lifted OUT of its group,
    /// so the heading that would have said which reward this is for is not beside it.</summary>
    public static string CardWhy(string reward) => "for: " + reward;

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
        Func<GuideObjective, bool> isSkipped)
    {
        var nextStage = guide.Stages.FirstOrDefault(s => s.Objectives.Any(o =>
            string.Equals(o.Id, next.Id, StringComparison.OrdinalIgnoreCase)));
        if (nextStage is null) return "";

        var stranded = guide.Stages
            .Where(s => s.Order < nextStage.Order)
            .SelectMany(s => s.Objectives)
            .Where(o => !isDone(o) && !isSkipped(o))
            .Select(o => o.Title)
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
            "&title=" + Uri.EscapeDataString($"Guide step: {guide.Name} / {objective.Title}") +
            "&body=" + Uri.EscapeDataString(body);
    }
}
