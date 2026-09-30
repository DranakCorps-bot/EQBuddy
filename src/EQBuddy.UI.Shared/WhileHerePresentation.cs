namespace EQBuddy.UI.Shared;

/// <summary>One drawn row: a step (or, in the Optional group, a quest's name) and the line
/// under it.</summary>
public sealed record WhileHereRowView(string Title, string Detail);

/// <summary>One drawn group: which, its label, the rows under the cap, and the cap's own
/// sentence when it held something back (trap 50).</summary>
public sealed record WhileHereGroupView(
    WhileHereGroup Group, string Label, IReadOnlyList<WhileHereRowView> Rows, string? More);

/// <summary>
/// **Every word the "while you're here" block says** (DRA-42 D1) — one file, so the Guide room
/// and the phone cannot word one fact two ways, and so the page-side must-list has one place to
/// read the sentences it must never have learned (trap 32).
///
/// <para><b>The subject of every empty or refused sentence is EQBuddy's catalogs or its log
/// reading, never the game.</b> "Nothing here" would be a claim about the world; what is true is
/// that no step EQBuddy can place is placed here. And nothing here calls a camp safe, easy or
/// survivable (HOME-006's ban) — the block names steps and who the pages name, and stops.</para>
/// </summary>
public static class WhileHerePresentation
{
    /// <summary>The block's heading, with the zone the log last entered.</summary>
    public static string Heading(string zone) => $"While you're in {zone}";

    /// <summary>The heading before the log has named a zone.</summary>
    public const string HeadingNoZone = "While you're here";

    /// <summary>Where the block's answer comes from — one caption, said once.</summary>
    public const string SourceNote =
        "Steps placed by eqlwiki's drop pages and each quest's start zone, in the zone your log last entered.";

    /// <summary>§18's three group labels, in its order.</summary>
    public static string GroupLabel(WhileHereGroup group) => group switch
    {
        WhileHereGroup.Required => "Required — quests you track",
        WhileHereGroup.RelevantReward => "Relevant rewards — quests you've started",
        _ => "Optional — other quests with a step here",
    };

    /// <summary>How many step rows one group draws before it counts the rest. Four, because the
    /// block sits ABOVE the Guide's tabs: at six a busy zone pushed the tab strip past the
    /// middle of a 1000-high room (measured on the first take of the shot).</summary>
    public const int StepsPerGroup = 4;

    /// <summary>How many optional quest NAMES the block draws before it counts the rest.</summary>
    public const int OptionalShown = 5;

    /// <summary>How many creatures a row names before it counts the rest — the Helper's
    /// <c>GearMobsPerItem</c> (3), for the same reason: a page listing nine droppers is a
    /// paragraph, not a row.</summary>
    public const int WhoShown = 3;

    /// <summary>The line under a step: its quest, then who the source names here.</summary>
    public static string StepDetail(WhileHereStep step)
    {
        var who = WhoLine(step.Who);
        return who.Length > 0 ? $"{step.Quest} · {who}" : step.Quest;
    }

    /// <summary>Up to <see cref="WhoShown"/> names in the source's order, the rest counted as
    /// the page's own ("and 4 more on its page") — never ranked, never dropped silently.</summary>
    public static string WhoLine(IReadOnlyList<string> who)
    {
        if (who.Count == 0) return "";
        var shown = string.Join(", ", who.Take(WhoShown));
        var rest = who.Count - WhoShown;
        return rest > 0 ? $"{shown} and {rest} more on its page" : shown;
    }

    /// <summary>The cap line under a group of steps (trap 50): how many it held back, and
    /// where every one of them is.</summary>
    public static string MoreSteps(int hidden) =>
        hidden == 1
            ? "…and 1 more step here — the Guide's tabs have every step"
            : $"…and {hidden} more steps here — the Guide's tabs have every step";

    /// <summary>The cap line under the optional names.</summary>
    public static string MoreQuests(int hidden) =>
        hidden == 1
            ? "…and 1 more quest — the Quests tab's zone view lists every quest here"
            : $"…and {hidden} more quests — the Quests tab's zone view lists every quest here";

    /// <summary>Tracked steps that place nowhere EQBuddy can read — said, so a tracked step
    /// never just vanishes from a block about your tracked work (trap 50).</summary>
    public static string Unplaced(int count) =>
        count == 1
            ? "1 open step of what you track names no place EQBuddy can read — its item's page names no drop zone, or it is an Epic 1.0 step in the wiki's own words — so it is not listed here."
            : $"{count} open steps of what you track name no place EQBuddy can read — their items' pages name no drop zone, or they are Epic 1.0 steps in the wiki's own words — so they are not listed here.";

    /// <summary>
    /// The groups a surface draws, in §18's order, each capped with its own sentence — <b>the
    /// one arrangement</b> the Guide room and the phone both walk, so a cap cannot be six on one
    /// screen and eight on the other. A group with nothing in it is not returned.
    /// </summary>
    public static IReadOnlyList<WhileHereGroupView> Groups(WhileHereAnswer answer)
    {
        var groups = new List<WhileHereGroupView>();
        foreach (var group in new[] { WhileHereGroup.Required, WhileHereGroup.RelevantReward })
        {
            var steps = answer.Steps(group);
            if (steps.Count == 0) continue;
            groups.Add(new(group, GroupLabel(group),
                [.. steps.Take(StepsPerGroup).Select(s => new WhileHereRowView(s.Step, StepDetail(s)))],
                steps.Count > StepsPerGroup ? MoreSteps(steps.Count - StepsPerGroup) : null));
        }
        if (answer.Optional.Count > 0)
            groups.Add(new(WhileHereGroup.Optional, GroupLabel(WhileHereGroup.Optional),
                [.. answer.Optional.Take(OptionalShown).Select(n => new WhileHereRowView(n, ""))],
                answer.Optional.Count > OptionalShown ? MoreQuests(answer.Optional.Count - OptionalShown) : null));
        return groups;
    }

    /// <summary>The unplaced sentence, or null when nothing tracked is unplaced.</summary>
    public static string? UnplacedLine(WhileHereAnswer answer) =>
        answer.UnplacedTracked > 0 ? Unplaced(answer.UnplacedTracked) : null;

    /// <summary>Quests with a step here that the class lens or era filter kept out — said, so
    /// the block never reads as "no quest has a step here" when a filter chose it (trap 73).</summary>
    public static string Filtered(int count) =>
        count == 1
            ? "1 quest with a step here is outside your classes or the General tab's era filter, so it is not listed."
            : $"{count} quests with a step here are outside your classes or the General tab's era filter, so they are not listed.";

    /// <summary>The filtered sentence, or null when the filters kept nothing out.</summary>
    public static string? FilteredLine(WhileHereAnswer answer) =>
        answer.Filtered > 0 ? Filtered(answer.Filtered) : null;

    /// <summary>The counted sentences under the groups, in order — drawn in EVERY state,
    /// the empty ones included, because what they count is exactly what the empty sentence
    /// does not cover. The one order both surfaces draw them in.</summary>
    public static IReadOnlyList<string> TrailingLines(WhileHereAnswer answer) =>
        [.. new[] { UnplacedLine(answer), FilteredLine(answer) }.OfType<string>()];

    /// <summary>The heading for this answer.</summary>
    public static string HeadingFor(WhileHereAnswer answer) =>
        answer.Zone.Length > 0 ? Heading(answer.Zone) : HeadingNoZone;

    /// <summary>The empty state, per reason — three different facts, never one silence
    /// (trap 73). Null for <see cref="WhileHereState.Answered"/>.</summary>
    public static string? Empty(WhileHereAnswer answer) => answer.State switch
    {
        WhileHereState.ZoneUnknown =>
            "EQBuddy has not seen you enter a zone yet — it learns where you are from the log's \"You have entered\" line.",
        WhileHereState.NotAPlace =>
            $"\"{answer.Zone}\" is not a zone EQBuddy can place quest steps in.",
        WhileHereState.NothingOpenHere =>
            $"No open step of a quest you have not hidden or finished is placed in {answer.Zone} for your classes and era filter, in EQBuddy's catalogs.",
        _ => null,
    };
}
