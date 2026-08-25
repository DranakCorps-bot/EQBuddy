namespace EQBuddy.Core;

/// <summary>
/// The achievements dump and the faction dump do not spell every faction the same way, so
/// one has to be translated into the other before a standing can be looked up.
///
/// **Four of the forty-two race-unlock requirement lines miss**, measured against David's
/// own pair on 2026-08-25 — both files written by the same game, for the same character,
/// three minutes apart:
///
///   achievements text            faction dump                 id
///   ---------------------------- ---------------------------- ----
///   Coalition of Tradesfolk      Coalition of Tradefolk        229
///   Freeport Militia             The Freeport Militia          330
///   Corrupt Qeynos Guard         Corrupt Qeynos Guards         230
///   Da Bashers                   DaBashers                     235
///
/// **A rule was tried first and does not reach them.** Squashing punctuation and case —
/// what <see cref="QuestClassFilter.Canonical"/> does for class names — closes "Da
/// Bashers"/"DaBashers" and nothing else; `AchievementsImport.NamesMatch`'s token-fuzzy
/// compare closes none of them ("tradesfolk"/"tradefolk" share a five-character prefix
/// and fail its tail test, which is the test stopping "Wind Rune Azia" from claiming
/// "Wind Rune Fana"). Loosening that would trade four known misses for an unknown number
/// of wrong matches, on data a player reads as fact. So: the rule first, then a curated
/// list for what the rule cannot reach, each row carrying the pair it was written for —
/// the shape `DeadSettingTests.Known` uses.
///
/// **An unresolved name is REPORTED, never dropped.** That is the whole lesson of the
/// Shadow Knight hole fixed the same day: a lookup that silently finds nothing looks
/// exactly like a requirement that is not met.
/// </summary>
public static class FactionNames
{
    /// <summary>Achievement spelling -> faction-dump spelling, for the pairs no rule
    /// reaches. Add a row only with the two real strings that produced it.</summary>
    private static readonly Dictionary<string, string> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // "Tradesfolk" vs "Tradefolk" — one letter, and the achievement is the one
            // with the extra s.
            ["Coalition of Tradesfolk"] = "Coalition of Tradefolk",
            // The dump carries the article and the achievement does not.
            ["Freeport Militia"] = "The Freeport Militia",
            // Singular in the achievement, plural in the dump.
            ["Corrupt Qeynos Guard"] = "Corrupt Qeynos Guards",
            // Spaced in the achievement, closed up in the dump. The squash rule below
            // would reach this one; it is listed anyway so the four that were MEASURED
            // stay together and a future change to the rule cannot quietly drop it.
            ["Da Bashers"] = "DaBashers",
        };

    /// <summary>
    /// The faction the achievements dump means, or null when nothing matches.
    ///
    /// Three passes, cheapest first: exact, then the curated alias, then a squash of
    /// everything that is not a letter or digit. Null rather than a guess — a wrong
    /// standing on a race-unlock row is worse than an honest "not found", the same
    /// judgement as a wrong respawn timer.
    /// </summary>
    public static FactionsFile.Standing? Resolve(
        FactionsFile.Snapshot? factions, string achievementName)
    {
        if (factions is null || string.IsNullOrWhiteSpace(achievementName)) return null;

        if (factions[achievementName] is { } exact) return exact;
        if (Aliases.TryGetValue(achievementName.Trim(), out var alias)
            && factions[alias] is { } aliased) return aliased;

        var key = Squash(achievementName);
        if (key.Length == 0) return null;
        return factions.Standings.FirstOrDefault(s => Squash(s.Name) == key);
    }

    private static string Squash(string s) =>
        new([.. s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
}
