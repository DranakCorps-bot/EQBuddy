using System.IO;
using System.Text.RegularExpressions;

namespace EQBuddy.Core;

/// <summary>
/// Does this filename look like a log EverQuest Legends wrote, as opposed to a file the
/// PLAYER made in the same folder?
///
/// The janitor's glob was <c>eqlog_*.txt</c>, which is the shape of the game's logs and
/// also the shape of every copy a player keeps beside them. StrIIker-TV (Reddit,
/// 2026-08-23) kept their history by renaming each log as it grew — the obvious way to do
/// it, and the result is <c>eqlog_StrIIker_erollisi_2026-08-01.txt</c>, which the glob
/// matched and <c>SetLength(0)</c> emptied. EQBuddy is allowed to empty the file the game
/// is writing for it. It is not allowed to empty the player's own archive, and a glob
/// cannot tell those apart.
///
/// **The discriminator is the CHARACTER SET, not the segment count** — and that is the
/// whole subtlety here. The obvious rule ("exactly two parts after eqlog_") is wrong:
/// <c>eqlog_Aenari_erollisi_marr.txt</c> is a real log on a real server whose short name
/// contains an underscore, and it has three parts. So segment counting would refuse to
/// sweep a legitimate log forever, which is the failure that made the feature exist.
///
/// What separates them is what a RENAME adds: digits (a date), a dash, a space, a dot,
/// "(1)", " - Copy". The game writes an alphabetic character name and an alphabetic
/// server short name, nothing else. So: letters for the character, letters and
/// underscores for the server, and no other character anywhere.
///
/// **The residual gap is stated rather than papered over.** A rename that adds only
/// letters — <c>eqlog_StrIIker_erollisi_old.txt</c> — is indistinguishable from a
/// character on a server called "erollisi_old" and is still swept. Nothing in the
/// filename can settle that one. It is a much narrower target than "anything starting
/// eqlog_", and archiving (on by default since 1.84.0) is the net underneath it.
/// </summary>
public static class GameWrittenLog
{
    // Anchored, and case is deliberately NOT a discriminator on either segment.
    //
    // This comment used to say the opposite — "deliberately not IgnoreCase on the server:
    // the game writes the server short name in lower case" — while the pattern below spells
    // [A-Za-z], which accepts either case by construction. The claim and the regex
    // disagreed, and on a gate that DESTROYS a player's file the comment is the thing a
    // reader trusts. Narrowing to [a-z] was the other way to reconcile them and is not
    // taken: no server list exists anywhere to say that every short name is lower case
    // (searched on eqlwiki, 2026-08-29 — there is no Servers page), so tightening would
    // rest on an assumption, and the one it replaced is the same assumption written down.
    //
    // What actually separates a game-written name from a rename is the character SET, not
    // its case: a rename adds digits, a dash, a space, a dot, "(1)", " - Copy". Case buys
    // nothing against any of those. The residual letters-only-rename gap is unchanged and
    // is stated in the type's own summary above.
    private static readonly Regex Shape = new(
        @"^eqlog_[A-Za-z]+_[A-Za-z][A-Za-z_]*\.txt$", RegexOptions.CultureInvariant);

    /// <summary>True when <paramref name="path"/> (a full path or a bare filename) has the
    /// shape EverQuest Legends itself writes. Callers that DESTROY content must gate on
    /// this; callers that merely read (history import, the folder picker) should not —
    /// a player is entitled to open their own renamed copy.</summary>
    public static bool IsGameWritten(string path)
    {
        var name = Path.GetFileName(path);
        return !string.IsNullOrEmpty(name) && Shape.IsMatch(name);
    }
}
