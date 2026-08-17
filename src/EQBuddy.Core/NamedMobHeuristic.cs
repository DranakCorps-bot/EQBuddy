namespace EQBuddy.Core;

/// <summary>
/// "Does this log line's creature name look like a NAMED mob?" — from the article
/// convention EverQuest has always followed: trash is referenced with an article ("an
/// ogre shaman"), a named or unique mob by its proper name ("Chief Goonda").
///
/// elderbit's observation (discussion #185): the curated spawn catalog has gaps that a
/// hand-maintained list will never stop having — every named in The Hole, Chief Goonda in
/// West Karana — and the log itself already says which mobs are named.
///
/// IT MUST BE FED THE RAW NAME. <see cref="LogParser.Normalize"/> strips leading articles
/// so "an orc pawn", "An orc pawn" and "orc pawn" count as one mob, which is exactly the
/// signal this reads — by the time a name reaches SessionStats it is gone. That is why
/// <see cref="KillEvent.ProperName"/> is decided at parse time and carried, rather than
/// re-derived downstream from a name that can no longer answer.
///
/// Deliberately conservative: a false positive invents a respawn timer for something
/// that has none, and "a wrong respawn timer is worse than none". Everything it is not
/// sure about is trash.
/// </summary>
public static class NamedMobHeuristic
{
    private static readonly string[] Articles = ["a ", "an ", "the "];

    /// <summary>True when the raw name carries no leading article AND reads like a proper
    /// name. Callers must still exclude pets and players — see <see cref="IsExcluded"/>;
    /// this answers only the question the article convention can answer.</summary>
    public static bool LooksProperName(string? rawName)
    {
        var name = (rawName ?? "").Trim();
        if (name.Length == 0) return false;

        // An article settles it: trash. "The" included — "the Spiroc Lord" is written
        // both ways on the wiki, so it is not evidence either way, and treating it as
        // trash only costs a timer we never had.
        foreach (var article in Articles)
            if (name.Length > article.Length
                && name.StartsWith(article, StringComparison.OrdinalIgnoreCase))
                return false;

        // A named mob's own name is capitalised. A log line that begins a sentence
        // capitalises the first word regardless ("A skeleton tries to punch YOU"), which
        // is why this is only ever asked of the TARGET of a slain line — mid-sentence,
        // where the game's own capitalisation is meaningful.
        if (!char.IsUpper(name[0])) return false;

        // "Corpse" and possessives are the remains, not the mob.
        if (name.Contains("'s corpse", StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    /// <summary>Names that look proper but must never start a respawn timer: your pet,
    /// anyone's pet, and players. elderbit raised the pet case himself — "Lonn slashes an
    /// ogre shaman" — and a charmed or summoned pet dying is not a spawn cycle. Players
    /// matter too: a group member's death is a proper-named "kill" in the log.</summary>
    public static bool IsExcluded(string name, IReadOnlyCollection<string> pets,
        IReadOnlyCollection<string> players)
    {
        name = name.Trim();
        return pets.Contains(name, StringComparer.OrdinalIgnoreCase)
            || players.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The whole question in one call: a named mob worth timing.</summary>
    public static bool IsTimeableNamed(string? rawName, string normalizedName,
        IReadOnlyCollection<string> pets, IReadOnlyCollection<string> players) =>
        LooksProperName(rawName) && !IsExcluded(normalizedName, pets, players);
}
