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
    /// The dim line under a guide row's title: WHO, WHERE, WHEN — the three that decide
    /// whether you can do this step right now, joined the way the classic checklist already
    /// joins its NPC and drop location.
    ///
    /// <para><b>Why only three of the six here.</b> Each question is drawn in exactly one
    /// place (David, 2026-09-09: *"we don't want to be redundant, but all must be
    /// addressed"*). WHAT is the row's own title. WHY and HOW are on the hover, where a
    /// player who has decided to do the step goes for the method — putting them inline made
    /// one row read as three sentences saying one thing, which is what the first staged shot
    /// of this surface showed.</para>
    ///
    /// <para>An empty part is dropped rather than leaving a dangling separator; a Stub has
    /// none of them and gets its note instead.</para></summary>
    public static string RowDetail(GuideObjective objective) =>
        Join(objective.Who, objective.Where, objective.When);

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
