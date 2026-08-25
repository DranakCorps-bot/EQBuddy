namespace EQBuddy.Core;

/// <summary>
/// Race and class unlocks arranged as checklist groups, so the two desktop windows and
/// EQBuddy Mobile draw them from one decision.
///
/// It reuses <see cref="QuestChecklistGroup"/> deliberately rather than minting a shape:
/// every surface already knows how to render one, the state lens and the actionability
/// sort come for free, and the parity test can compare the phone against the same call
/// the windows make — which is the arrangement #184 cost a release to learn.
///
/// **A row's tick comes from the dump that can actually answer it, never from the
/// achievement's own flag where a better source exists.** A completed unlock marks all its
/// children complete whether or not they were earned, so for a race the FACTION dump is
/// the answer and the achievement is only the question. That is not a theory: David's
/// Dark Elf unlock reads complete with three completed faction criteria while his faction
/// dump, three minutes younger, puts those three at 0/2000, 5/1995 and 0/2000.
/// </summary>
public static class UnlockLayout
{
    public const string RacesHeading = "Races";
    public const string ClassesHeading = "Classes";

    /// <summary>The "show me one section" lens. "All" first, because the tab's job is the
    /// whole picture and narrowing is the exception. Deity is deliberately absent: the game
    /// has not defined its requirements, so a fourth entry here would filter to nothing
    /// (Hateborne, 2026-08-25, asked directly).</summary>
    public const string SectionAll = "All";

    public static readonly string[] Sections = [SectionAll, RacesHeading, ClassesHeading];

    /// <summary>Should this section be drawn under the current lens?</summary>
    public static bool InSection(string heading, string lens) =>
        lens.Length == 0
        || lens.Equals(SectionAll, StringComparison.OrdinalIgnoreCase)
        || heading.Equals(lens, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Groups for one section.
    /// </summary>
    /// <param name="factions">The faction dump, or null when the player has not run
    /// `/outputfile faction`. Null is a real state and a different one from "nothing is
    /// maxed" — <see cref="NeedsFactionDump"/> is how a surface tells them apart, so it
    /// can ask for the dump instead of showing sixteen empty checklists.</param>
    public static List<QuestChecklistGroup> Groups(
        IEnumerable<UnlockProgress> unlocks, FactionsFile.Snapshot? factions, string heading)
    {
        var groups = new List<QuestChecklistGroup>();
        foreach (var u in unlocks)
        {
            var rows = new List<QuestChecklistRow>();
            foreach (var c in u.Actionable)
            {
                var (done, detail) = Resolve(u, c, factions);
                rows.Add(new QuestChecklistRow(
                    Id: $"unlock|{heading}|{u.Subject}|{c.Subject}",
                    ClassName: u.Subject,
                    Title: c.Subject.Length > 0 ? c.Subject : c.Text,
                    Detail: detail,
                    Acquired: done,
                    Unassigned: false));
            }
            groups.Add(new QuestChecklistGroup(
                ClassName: heading,
                Title: u.Subject,
                Rows: rows,
                CompletionKey: null,   // the GAME decides an unlock; there is nothing to tick
                Completed: u.Complete,
                TurnInNpc: null));
        }
        return groups;
    }

    /// <summary>Is a faction dump the missing piece? True only when something actually
    /// wants one — a player with no race unlocks in view is not missing anything.</summary>
    public static bool NeedsFactionDump(
        IEnumerable<UnlockProgress> races, FactionsFile.Snapshot? factions) =>
        factions is null
        && races.Any(r => r.Actionable.Any(c => c.Need == UnlockNeed.MaxFaction));

    /// <summary>
    /// How an unlock explains itself in one line, under its heading.
    ///
    /// The granted case is the one that has to be said out loud. "You have this race" and
    /// "you did this work" are different facts, and a player looking at a completed unlock
    /// whose factions sit near zero deserves to know which one they are looking at rather
    /// than concluding the tracker is broken.
    /// </summary>
    public static string? Note(UnlockProgress u) =>
        u.DerivedNote is { Length: > 0 } derived ? derived
        : u.Inherited ? "unlocked without the requirements — created as this, or a token"
        : u.Complete ? "unlocked"
        : null;

    private static (bool Done, string Detail) Resolve(
        UnlockProgress u, UnlockCriterion c, FactionsFile.Snapshot? factions)
    {
        if (c.Need == UnlockNeed.MaxFaction)
        {
            var standing = FactionNames.Resolve(factions, c.Subject);
            if (standing is null)
            {
                // Said, never dropped. Two different reasons land here and the wording
                // separates them, because one is the player's move and the other is ours.
                return (false, factions is null
                    ? "run /outputfile faction to see where you stand"
                    : "not in your faction dump — tell us and we will add the name");
            }
            return (standing.Maxed, StandingText(standing));
        }

        // Nothing else has a second source, so the achievement's own flag is all there is
        // — and it is worthless under a granted unlock.
        return (!u.Inherited && c.Done, c.Need == UnlockNeed.Task ? c.Text : "");
    }

    /// <summary>"1,535 / 2,000 — 465 to go", or "maxed". Negative standing says how far
    /// the wrong way, because "0 / 2,000" would hide that the work is longer than it
    /// looks: Keepers of the Art at -950 is 2,950 points away, not 2,000.</summary>
    public static string StandingText(FactionsFile.Standing s) =>
        s.Maxed
            ? "maxed"
            : $"{s.Value:N0} / {FactionsFile.Cap:N0} — {s.PointsToMax:N0} to go";
}
