using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One open guide step a spawn point answers: which step, of which quest, and which of
/// its droppers was killed at the point. Both name halves are in the sentence for
/// <see cref="GearTargetHit"/>'s reason — a mark over a point that has seen five mobs has to say
/// which one to wait for.</summary>
public sealed record GuideTargetHit(string Quest, string Step, string Creature);

/// <summary>
/// **DOES THIS DOT SERVE A GUIDE STEP I AM ON?** — the map's guide layer (DRA-42 D3,
/// requirements §20). A READER over two answers that already exist, and never a store.
///
/// <para><b>Which steps are "mine, here" is <see cref="WhileHere.For"/>'s answer, not a second
/// one</b> (trap 4). The host hands the map the SAME answer the Guide room and the phone's quests
/// section draw (<c>MainWindow.WhileHereNow</c>, the one builder of its inputs — trap 33), so a
/// step that leaves the block leaves the map in the same tick. Only the player's OWN work is
/// read — the Required and Relevant groups, D2's departure rule — because the Optional group is
/// quests nobody has started, and a mark is a place a player walks to.</para>
///
/// <para><b>NO SECOND MAP ENGINE</b> (S13.1/S20, the <see cref="GearTargets"/> rule verbatim).
/// <see cref="SpawnPointLedger"/> archives the points and the map already draws them; the one new
/// question is asked once per point, by <see cref="AtPoint"/>, and nothing here computes a
/// coordinate or loads a file.</para>
///
/// <para><b>Only a DROPPER can be the creature at a dot.</b> A spawn point is where the log saw
/// a kill, so a step done at a person (talk to, hand in) is never matched even when its giver's
/// name is on a point — that point is where somebody killed the person you are meant to speak
/// to. Such a step is still counted for the panel (<see cref="Unmarkable"/>), because a step
/// that is "here" and can never wear a mark would otherwise read as a mark the map forgot.</para>
///
/// <para>Deliberately separate from <see cref="GearTargets"/>: that layer answers "is this one
/// of the upgrades I decided to go get", this one "is this where a step I am on is done". Two
/// meanings, two marks, two switches (plan §D3).</para>
/// </summary>
public static class GuideTargets
{
    /// <summary>
    /// The player's own open steps in <paramref name="shownZone"/> whose source names a creature
    /// to kill — the ones a spawn point can answer. Empty unless <paramref name="answer"/> is
    /// about the zone the map shows: <b>exact title, then <see cref="ZoneMapFiles.IdentityKey"/>,
    /// and NOTHING looser</b> (<see cref="WhileHerePlaces.SameZone"/>), so a map of West
    /// Commonlands never marks a dot for a step placed in Commonlands.
    /// </summary>
    public static IReadOnlyList<WhileHereStep> Here(WhileHereAnswer? answer, string? shownZone) =>
        [.. Own(answer, shownZone).Where(s => s.WhoDrops && s.Who.Count > 0)];

    /// <summary>The player's own open steps in <paramref name="shownZone"/> that NO dot can
    /// answer — done at a person, or whose page named nobody to kill here. Counted so the panel
    /// can say why they carry no mark.</summary>
    public static int Unmarkable(WhileHereAnswer? answer, string? shownZone) =>
        Own(answer, shownZone).Count(s => !s.WhoDrops || s.Who.Count == 0);

    private static IEnumerable<WhileHereStep> Own(WhileHereAnswer? answer, string? shownZone)
    {
        if (answer is null || answer.State != WhileHereState.Answered) return [];
        if (!WhileHerePlaces.SameZone(answer.Zone, shownZone)) return [];
        return [.. answer.Required, .. answer.Relevant];
    }

    /// <summary>
    /// Which of <paramref name="here"/>'s steps a spawn point answers — asked once per archived
    /// point, with the names archived there.
    ///
    /// <para><b>The match is <see cref="SpawnCatalog.NameMatches"/> and deliberately NOT its
    /// fuzzy sibling</b>, for <see cref="GearTargets.AtPoint"/>'s reason verbatim: a false hit is
    /// a mark on a dot that does not serve the step, and a player travels to it. A miss draws no
    /// mark, which is the direction a rule that only ever ADDS a mark may fail in.</para>
    ///
    /// <para>Every hit is returned, not the first — one point can be the camp for two steps.</para>
    /// </summary>
    public static IReadOnlyList<GuideTargetHit> AtPoint(
        IReadOnlyList<WhileHereStep>? here, IEnumerable<string>? mobsSeen)
    {
        if (here is not { Count: > 0 } || mobsSeen is null) return [];
        var seen = mobsSeen.Where(m => m is { Length: > 0 }).ToList();
        if (seen.Count == 0) return [];

        var hits = new List<GuideTargetHit>();
        foreach (var step in here)
        {
            if (!step.WhoDrops) continue;
            foreach (var creature in step.Who)
                if (seen.Any(m => SpawnCatalog.NameMatches(creature, m))
                    && !hits.Any(h => h.Quest.Equals(step.Quest, StringComparison.OrdinalIgnoreCase)
                                      && h.Step.Equals(step.Step, StringComparison.OrdinalIgnoreCase)
                                      && h.Creature.Equals(creature, StringComparison.OrdinalIgnoreCase)))
                    hits.Add(new GuideTargetHit(step.Quest, step.Step, creature));
        }
        return hits;
    }

    /// <summary>
    /// The layer's one switch, applied in ONE place (the <c>ShowGearTargetsOnMap</c> shape, D5
    /// review finding D5-1): off answers <see cref="WhileHereAnswer.None"/>, which every reader
    /// already draws nothing on, so neither view needs a branch of its own (trap 33). The answer
    /// is a func so switching off never builds it.
    /// </summary>
    public static WhileHereAnswer Gate(AppSettings? settings, Func<WhileHereAnswer> answer) =>
        settings?.ShowGuideTargetsOnMap == true ? answer() : WhileHereAnswer.None;
}
