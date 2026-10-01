using EQBuddy.UI.Shared;

namespace EQBuddy.Companion;

public static partial class CompanionProjection
{
    /// <summary>
    /// WHILE YOU'RE HERE on the wire (DRA-42 D1): the Guide room's answer, arranged by the SAME
    /// <see cref="WhileHerePresentation.Groups"/> the room walks and worded by the same file, so
    /// the phone decides no word and no cap (trap 32). Null in, null out — a host that sent no
    /// answer gets no block rather than an empty one that would claim "nothing here".
    /// D2 adds the "before you leave" line and the departure notice, from the same file.
    /// </summary>
    internal static CompanionWhileHere? BuildWhileHere(WhileHereAnswer? answer, WhileHereDeparture? left = null) =>
        answer is null
            ? null
            : new CompanionWhileHere(
                Heading: WhileHerePresentation.HeadingFor(answer),
                Note: WhileHerePresentation.SourceNote,
                Empty: WhileHerePresentation.Empty(answer),
                Groups: Groups(answer),
                Unplaced: WhileHerePresentation.UnplacedLine(answer),
                Filtered: WhileHerePresentation.FilteredLine(answer),
                LeaveHeading: WhileHerePresentation.LeaveLine(answer) is null
                    ? null : WhileHerePresentation.LeaveHeading(answer.Zone),
                LeaveLine: WhileHerePresentation.LeaveLine(answer),
                Departed: left is null
                    ? null
                    : new CompanionWhileHereDeparture(
                        WhileHerePresentation.Departed(left),
                        WhileHerePresentation.DepartedQuests(left),
                        Groups(left.Left),
                        WhileHerePresentation.DismissOnPc));

    private static IReadOnlyList<CompanionWhileHereGroup> Groups(WhileHereAnswer answer) =>
        [.. WhileHerePresentation.Groups(answer).Select(g => new CompanionWhileHereGroup(
            g.Label, [.. g.Rows.Select(r => new CompanionWhileHereRow(r.Title, r.Detail))], g.More))];
}
