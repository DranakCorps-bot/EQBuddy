namespace EQBuddy.Core;

/// <summary>
/// One guide step's REFERENCE, answered by the Helper (DRA-83, the DRA-70 plan's D5).
///
/// <para><see cref="Attachment"/> is the curated catalog's own <c>{ Kind, Key }</c> — a
/// subject and nothing else. <see cref="Answer"/> is the recommendation the Helper's own
/// engine produced about that subject, <b>untouched</b>: same record, same facts, same
/// evidence tags, so a surface drawing it says what the Helper room says and cannot drift
/// from it.</para>
///
/// <para><see cref="Why"/> is the subset of <see cref="Answer"/>'s lines that are about the
/// KEY. For a zone-keyed reference that is every line on the row — the row IS the place. For
/// a <c>GearUpgrade</c> it is the lines naming that item, because a Farm Gear row can name
/// three upgrades and a guide step that pointed at one reward while printing two others would
/// be answering a question nobody asked.</para>
/// </summary>
public sealed record GuideAttachmentAnswer(
    GuideAttachment Attachment,
    HelperGoal Goal,
    Recommendation Answer,
    IReadOnlyList<WhyFact> Why);

public static partial class Recommendations
{
    /// <summary>
    /// Which goal's engine owns a reference kind — <b>the must-list half of trap 34 for
    /// <see cref="GuideAttachment.KnownKinds"/></b>.
    ///
    /// <para>Null means nobody has decided, which is only reachable by adding a kind to that
    /// list; <c>GuideAttachmentTests</c> walks it and fails on a null. A default arm answering
    /// "no goal" would have swallowed exactly that case, and "a later slice owns this kind"
    /// and "somebody added a string nobody wired up" would have looked identical on screen:
    /// both draw nothing.</para>
    ///
    /// <para><b>Two of the three share an engine and that is the point.</b> A zone that can
    /// still give this character something to wear and an item that beats what they are
    /// wearing are one question asked from two ends, and <see cref="FarmGear"/> has answered
    /// it since DRA-71 D6. A second sweep for the guide surface would be trap 4 with a
    /// reference field in front of it.</para>
    /// </summary>
    public static HelperGoal? GoalFor(string kind) => kind switch
    {
        GuideAttachment.XpFarm => HelperGoal.LevelUp,
        GuideAttachment.GearFarm => HelperGoal.FarmGear,
        GuideAttachment.GearUpgrade => HelperGoal.FarmGear,
        _ => null,
    };

    /// <summary>
    /// **What the Helper has to say about the subjects a guide's steps point at** — the
    /// <c>GuideAttachment</c> hookup (DRA-83; the DRA-70 plan's D5, and the day
    /// <c>NoShippedGuideCarriesAnAttachmentYet</c> changed with it).
    ///
    /// <para><b>It ranks nothing and words nothing.</b> It runs the SAME engines
    /// <see cref="Rank"/> runs, over the SAME <see cref="HelperInputs"/> its caller assembled
    /// through <c>HelperSources</c>, and hands back the engine's own
    /// <see cref="Recommendation"/> records. That is the single-source requirement read
    /// literally: not "the guide asks a similar question", but "the guide asks
    /// <see cref="LevelUp"/> and <see cref="FarmGear"/>, the two methods the room asks".</para>
    ///
    /// <para><b>THE GUIDE NEVER SAYS MORE THAN THE ROOM WOULD.</b> Both engines shortlist
    /// (<see cref="PerEngineCandidates"/>) and a gear row names <see cref="GearNamedPerRow"/>
    /// items, so a subject those caps did not reach gets NO answer here either. That is a
    /// decision and not an oversight: a guide row that out-claimed the Helper room would be a
    /// second answer to one question, louder than the first, and the room is where a player
    /// goes to argue with it. Silence is trap 73's own rule — an unanswered question draws
    /// nothing.</para>
    ///
    /// <para><b>The engines' GAPS are dropped on purpose.</b> "EQBuddy has never read an
    /// inventory dump" is a sentence about the player's whole evening and the Helper room says
    /// it once, with the command that fixes it. Ninety-five guide rows repeating it would be
    /// the room's empty state wallpapered across a checklist, which is not help — so a step
    /// whose reference cannot be answered draws nothing and the room keeps the sentence.</para>
    ///
    /// <para>No join (<see cref="Join"/>) and no <see cref="DefaultCap"/>: both exist to build
    /// an evening's short list out of competing candidates, and this is a lookup of one named
    /// subject. <see cref="Trim"/> IS applied, because <see cref="WhyCap"/> is how many
    /// sentences one row may spend and that is as true on a guide step as in the room.</para>
    /// </summary>
    /// <param name="attachments">The references on the steps a surface is about to draw —
    /// <c>GuideObjective.Attachments</c> and <c>GuideStage.Attachments</c>. Duplicates are
    /// answered independently; the caller decides what to do with two identical lines.</param>
    public static IReadOnlyList<GuideAttachmentAnswer> Attached(
        HelperInputs? inputs, IReadOnlyList<GuideAttachment>? attachments)
    {
        if (attachments is not { Count: > 0 }) return [];
        inputs ??= HelperInputs.Nothing;

        // One engine run per goal, however many references ask for it: 95 Sky guides pointing
        // at one zone must not be 95 folds of the same history (trap 4's shape in a loop, and
        // this one is on a surface that repaints).
        var perGoal = new Dictionary<HelperGoal, List<Recommendation>>();
        var answers = new List<GuideAttachmentAnswer>();
        foreach (var attachment in attachments)
        {
            // An empty key is refused by `GuideCatalog.Validate`, so this is unreachable from
            // the shipped catalog — and a fixture or a hand-edited profile still gets silence
            // rather than a match against every row whose zone is "".
            if (attachment.Key.Length == 0) continue;
            if (GoalFor(attachment.Kind) is not { } goal) continue;

            if (!perGoal.TryGetValue(goal, out var candidates))
                perGoal[goal] = candidates = Candidates(inputs, goal);

            if (Match(attachment, goal, candidates) is { } answer) answers.Add(answer);
        }
        return answers;
    }

    /// <summary>
    /// Every answer one engine has today, unranked and unjoined.
    /// </summary>
    /// <remarks>
    /// The <see cref="GoalGap"/> list is deliberately thrown away — see
    /// <see cref="Attached"/> for why the room keeps those sentences. The <c>default</c> arm
    /// THROWS rather than returning an empty list: a kind that <see cref="GoalFor"/> maps to a
    /// goal with no engine arm here is a wiring bug, and an empty list would have shipped it
    /// as "the Helper has nothing to say about your gear" (trap 34 — a guard that cannot see a
    /// missing thing). <c>GuideAttachmentTests.EveryKnownKindsGoalHasAnEngineBehindIt</c> runs
    /// this for all three kinds, so the throw is reachable from a test rather than from a
    /// player.
    /// </remarks>
    private static List<Recommendation> Candidates(HelperInputs inputs, HelperGoal goal)
    {
        var into = new List<Recommendation>();
        var discarded = new List<GoalGap>();
        switch (goal)
        {
            case HelperGoal.LevelUp: LevelUp(inputs, into, discarded); break;
            case HelperGoal.FarmGear: FarmGear(inputs, into, discarded); break;
            default:
                throw new InvalidOperationException(
                    $"GoalFor maps a reference kind to {goal}, which has no engine in Attached.");
        }
        return [.. into.Select(Trim)];
    }

    /// <summary>
    /// The engine's answer about this exact subject, or null.
    ///
    /// <para>Two rules, one per shape of key, and the shape is what the KIND decides:</para>
    /// <list type="bullet">
    /// <item><b>A zone</b> (<c>XpFarm</c>, <c>GearFarm</c>) matches
    /// <see cref="Recommendation.Zone"/>, case-insensitively for the reason
    /// <see cref="Join"/> groups that way: two sources capitalise a zone differently and
    /// neither spelling is wrong.</item>
    /// <item><b>An item</b> (<c>GearUpgrade</c>) matches a LINE, because the Farm Gear engine's
    /// rows are places and quests — an item is never a row's subject. The first candidate that
    /// names it wins, and "first" is the engine's own order (most of your open upgrades first),
    /// so the row a guide step points at is the row the room would have put highest.</item>
    /// </list>
    /// </summary>
    private static GuideAttachmentAnswer? Match(
        GuideAttachment attachment, HelperGoal goal, List<Recommendation> candidates)
    {
        if (attachment.Kind is GuideAttachment.XpFarm or GuideAttachment.GearFarm)
        {
            var place = candidates.FirstOrDefault(c => c.Zone.Length > 0
                && c.Zone.Equals(attachment.Key, StringComparison.OrdinalIgnoreCase));
            return place is null ? null : new GuideAttachmentAnswer(attachment, goal, place, place.Why);
        }

        foreach (var candidate in candidates)
        {
            var about = candidate.Why.Where(w => Names(w, attachment.Key)).ToList();
            if (about.Count > 0)
                return new GuideAttachmentAnswer(attachment, goal, candidate, about);
        }
        return null;
    }

    /// <summary>
    /// Is this why-line about that item?
    ///
    /// <para>Both of the Farm Gear engine's item-naming facts are admitted, and the pair is
    /// the answer rather than one of them: <see cref="GearUpgradeFact"/> is what the catalog
    /// says the item would improve, <see cref="GearDropSeenFact"/> is the player's own kills
    /// that produced it. Dropping the second would throw away the only PERSONAL evidence a
    /// gear answer ever carries, and HOME-003 exists to make that the line that outranks.</para>
    /// </summary>
    private static bool Names(WhyFact fact, string item) => fact switch
    {
        GearUpgradeFact g => g.Item.Equals(item, StringComparison.OrdinalIgnoreCase),
        GearDropSeenFact d => d.Item.Equals(item, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };
}
