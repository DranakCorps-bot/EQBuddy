namespace EQBuddy.Core;

/// <summary>What kind of thing an unlock achievement's criterion asks of the player.</summary>
public enum UnlockNeed
{
    /// <summary>"Get maximum faction with X." — answered by the faction dump's
    /// PointsToMax, and by nothing in the log.</summary>
    MaxFaction,
    /// <summary>"Obtain X." — a Plane of Sky reward, on the class side.</summary>
    Obtain,
    /// <summary>"Complete the 'Aid the Kerrans of Kerra Isle' Task." — real work with no
    /// data source EQBuddy can read, so it is shown and never guessed at.</summary>
    Task,
    /// <summary>"This achievement will autocomplete when you unlock Human or Wood Elf."
    /// Real, but it is a consequence of other rows rather than something to do. Half Elf
    /// has ONLY this, so a surface that counted requirements would show it as 0/0.</summary>
    Derived,
    /// <summary>"…will autocomplete if your character was created as a Barbarian." /
    /// "…can be bypassed using a Race Unlock Token." Neither is something a player does,
    /// and counting them would put "3/5" on a race with three requirements. They are also
    /// the TELL for an inherited completion — see
    /// <see cref="UnlockProgress.Inherited"/>.</summary>
    Bypass,
}

/// <summary>One line under an unlock achievement, classified.</summary>
/// <param name="Done">The dump's own C/I flag. Trustworthy per-criterion only while the
/// PARENT is incomplete — see <see cref="UnlockProgress.Inherited"/>.</param>
public sealed record UnlockCriterion(UnlockNeed Need, string Text, string Subject, bool Done);

/// <summary>
/// One race or class unlock, with its criteria and what the dumps say about them.
/// </summary>
/// <param name="Inherited">
/// The completion was GRANTED, not earned, so every child flag under it is meaningless.
///
/// Proven in David's own pair, 2026-08-25: "Race Unlock - Dark Elf" is complete with all
/// three of its faction lines flagged complete, while the faction dump for the same
/// character reads 0/2000, 5/1995 and 0/2000 for those very factions. He was created a
/// Dark Elf; the game marked the children when the parent completed. The same shape on
/// the class side is #101 and #193, and this is that guard generalised rather than a
/// second copy of it.
/// </param>
public sealed record UnlockProgress(
    string Section, string Name, string Subject, bool Complete, bool Inherited,
    IReadOnlyList<UnlockCriterion> Criteria)
{
    /// <summary>The criteria that are actually work. Bypass and Derived lines are facts
    /// about how the unlock can happen, not steps — counting them is how "3 of 5" appears
    /// beside three requirements.</summary>
    public IReadOnlyList<UnlockCriterion> Actionable =>
        [.. Criteria.Where(c => c.Need is UnlockNeed.MaxFaction or UnlockNeed.Obtain or UnlockNeed.Task)];

    /// <summary>How many of the actionable criteria are done, ignoring the dump's flags
    /// where they were inherited. Null when there is nothing to count — Half Elf, whose
    /// only rows are Derived — because "0 of 0" reads as a stalled checklist and the
    /// honest answer is a sentence instead.</summary>
    public (int Done, int Total)? Score
    {
        get
        {
            var rows = Actionable;
            if (rows.Count == 0) return null;
            if (Complete) return (rows.Count, rows.Count);
            return (Inherited ? 0 : rows.Count(c => c.Done), rows.Count);
        }
    }

    /// <summary>The "unlocks itself when…" line, for an unlock with no work of its own.</summary>
    public string? DerivedNote =>
        Actionable.Count == 0
            ? Criteria.FirstOrDefault(c => c.Need == UnlockNeed.Derived)?.Text
            : null;
}

/// <summary>
/// The `Untapped Potential: Races` and `Untapped Potential: Classes` sections of
/// `/outputfile achievements`, read for what they plainly say.
///
/// **The dump carries the requirements, so EQBuddy curates no catalog of them.** Each race
/// unlock names its own factions; each class unlock names its own rewards. Writing that
/// table out here would create a second source of truth competing with eqlwiki, which is
/// the first thing CLAUDE.md rules out for shared game facts — and it would be a table
/// somebody has to maintain forever, wrong in a different way than the game is.
///
/// Deity is deliberately not read. Sixteen of its seventeen entries in a real dump say
/// "Future Placeholder for &lt;God&gt; Requirements"; there is nothing to show yet, and the
/// section will parse with no change to this file on the day there is (David's call,
/// 2026-08-25).
/// </summary>
public static class UnlockRequirements
{
    public const string RaceSection = "Untapped Potential: Races";
    public const string ClassSection = "Untapped Potential: Classes";

    private const string MaxFactionPrefix = "Get maximum faction with ";
    private const string ObtainPrefix = "Obtain ";

    public static List<UnlockProgress> Races(IEnumerable<AchievementEntry> achievements) =>
        Read(achievements, RaceSection, "Race Unlock");

    public static List<UnlockProgress> Classes(IEnumerable<AchievementEntry> achievements) =>
        Read(achievements, ClassSection, "Class Unlock");

    private static List<UnlockProgress> Read(
        IEnumerable<AchievementEntry> achievements, string section, string marker)
    {
        var result = new List<UnlockProgress>();
        foreach (var a in achievements)
        {
            if (!a.Section.Equals(section, StringComparison.OrdinalIgnoreCase)) continue;
            if (!a.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)) continue;
            var dash = a.Name.LastIndexOf(" - ", StringComparison.Ordinal);
            if (dash < 0) continue;

            var subject = a.Name[(dash + 3)..].Trim();
            if (subject.Length == 0) continue;
            // Classes go through the canonical spelling for the reason in
            // QuestClassFilter.Canonical: the dump writes "Shadowknight" and every catalog
            // here writes "Shadow Knight". Races are left as the dump spells them —
            // "Human (Freeport)" and "Human (Qeynos)" are two different unlocks and there
            // is no canonical race list to fold them into.
            if (marker == "Class Unlock"
                && QuestClassFilter.Canonical(subject) is { Length: > 0 } canonical)
                subject = canonical;

            var criteria = a.Criteria.Select(c => Classify(c.Text, c.Complete)).ToList();
            // The parent completed AND a bypass line under it is flagged: the children
            // were marked by the game rather than earned by the player.
            var inherited = a.Complete
                && criteria.Any(c => c.Need == UnlockNeed.Bypass && c.Done);

            result.Add(new UnlockProgress(
                a.Section, a.Name, subject, a.Complete, inherited, criteria));
        }
        return result;
    }

    /// <summary>
    /// Which kind of row this is, and what it is about.
    ///
    /// Order matters: the two Bypass shapes are checked before Derived, because
    /// "will autocomplete if your character was created as a Barbarian" and "will
    /// autocomplete when you unlock Human or Wood Elf" both begin the same way and mean
    /// opposite things — one is a way the unlock can be handed to you, the other is a real
    /// consequence of real work.
    ///
    /// A trailing period is optional. The dump writes one on most lines and not on
    /// Beastlord's or Berserker's, which is a difference no rule should depend on.
    /// </summary>
    public static UnlockCriterion Classify(string rawText, bool done)
    {
        var text = (rawText ?? "").Trim();
        var bare = text.TrimEnd('.').Trim();

        if (bare.Contains("can be bypassed", StringComparison.OrdinalIgnoreCase)
            || bare.Contains("if your character was created", StringComparison.OrdinalIgnoreCase)
            || bare.Contains("if you chose to confirm", StringComparison.OrdinalIgnoreCase))
            return new UnlockCriterion(UnlockNeed.Bypass, text, "", done);

        if (bare.StartsWith(MaxFactionPrefix, StringComparison.OrdinalIgnoreCase))
            return new UnlockCriterion(UnlockNeed.MaxFaction, text,
                bare[MaxFactionPrefix.Length..].Trim(), done);

        if (bare.StartsWith(ObtainPrefix, StringComparison.OrdinalIgnoreCase))
            return new UnlockCriterion(UnlockNeed.Obtain, text,
                bare[ObtainPrefix.Length..].Trim(), done);

        if (bare.Contains("will autocomplete", StringComparison.OrdinalIgnoreCase))
            return new UnlockCriterion(UnlockNeed.Derived, text, "", done);

        return new UnlockCriterion(UnlockNeed.Task, text, bare, done);
    }
}
