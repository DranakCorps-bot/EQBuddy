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
    /// name — pets included, since 2026-08-22 (see the trailing-"pet" rule below).
    ///
    /// There used to be an `IsExcluded`/`IsTimeableNamed` pair here promising to filter
    /// "your pet, anyone's pet, and players". **Nothing outside its own tests ever called
    /// it**, so the promise had never once run, and pets were filtered by the article
    /// convention alone — which is how "Xanthus`s pet" earned a respawn clock. Deleted on
    /// Fable 5's ruling (v1.99.5 review) rather than wired: the suffix rule covers every
    /// possessive pet the log prints, and a player's death is never "You have slain", so
    /// the `Killer == "You"` gate already closes the players case. **A promise with no
    /// caller is worse than no promise.**</summary>
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

        // A PET IS NOT A NAMED, however proper the name in front of it looks (David,
        // 2026-08-22, from his own spawn list: *"we were starting to see named's pet"*).
        // "Xanthus`s pet" carries no article and is capitalised, so the article convention
        // waves it straight through and it earns a respawn timer — for a thing that has no
        // spawn cycle at all, only a summoner. An invented timer teaches you to walk to a
        // camp that is not up, which is the failure this whole heuristic is built to avoid.
        //
        // The test is the LAST WORD, not a substring: "pet" inside a name is ordinary
        // ("Petrifier", "Petras", "a petrified golem") and matching it would delete real
        // named mobs from the list, which is the expensive direction to be wrong in. Every
        // possessive form the game writes — `s, 's, curly ’s — ends in " pet" regardless,
        // so one check covers them all.
        if (name.EndsWith(" pet", StringComparison.OrdinalIgnoreCase)
            || name.Equals("pet", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
