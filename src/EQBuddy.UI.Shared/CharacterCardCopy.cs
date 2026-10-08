namespace EQBuddy.UI.Shared;

/// <summary>
/// **Every sentence of the Options "Character card" block** (DRA-1288 D2,
/// <c>docs/plans/DRA-1288.md</c>) — <see cref="TelemetryCopy"/>'s idiom: the words live here,
/// <c>EQBuddy/SettingsCharacterCardView</c> draws them, and nothing else spells them.
///
/// <para><b>The lead is the privacy disclosure and is used VERBATIM</b> (plan, D2): it says
/// where the file stays, who can carry it off the machine, and how to stop it. It is printed
/// under the toggle in both states, never moved behind an ⓘ — the same reason the
/// telemetry row's copy is printed.</para>
///
/// <para>The card file's own words are <see cref="CharacterCardPresentation"/>'s; these are
/// only the words ABOUT the file.</para>
/// </summary>
public static class CharacterCardCopy
{
    public const string ToggleLabel = "Keep a character card file for the character you are playing";

    /// <summary>The disclosure, verbatim from the plan.</summary>
    public const string Lead =
        "EQBuddy keeps this file on your PC and sends it nowhere. If you let an AI app read it, "
        + "that app may send it to its own online service. Turn this off at any time to stop "
        + "updating it.";

    /// <summary>What the file holds, and what it never holds — the values line, said where the
    /// player decides.</summary>
    public const string Contents =
        "One markdown file per character: classes and levels, tradeskills, quests, tracked "
        + "upgrades, gear, factions, recent sessions and EQBuddy's own per-zone totals. It is "
        + "rewritten a few seconds after one of those changes. Other players are never in it.";

    public const string FolderLabel = "Folder:";

    public const string OpenFolderButton = "Open folder";
    public const string WriteNowButton = "Write it now";
    public const string DeleteButton = "Delete card files";

    public const string WriteNowTip =
        "Writes the current character's card once, now — also while the switch above is off.";

    public const string DeleteTip =
        "Removes only the card files EQBuddy wrote. Anything else in the folder stays.";

    /// <summary>The line after <see cref="WriteNowButton"/> wrote a file: where it went.</summary>
    public static string WrittenTo(string path) => $"Written to {path}";

    public const string NothingToWrite =
        "No character is being followed yet, so there is no card to write.";

    public const string NotWhileReviewing =
        "Not while an archived log is being reviewed. Go back to the live log first.";

    public const string WriteFailed =
        "EQBuddy could not write the file. error.log has the reason.";

    /// <summary>The line after <see cref="DeleteButton"/>. <paramref name="stillOn"/> says the
    /// switch is on, so the current character's card will come back — deleting is not turning
    /// it off, and a player who meant "stop" needs to be told which control does that.</summary>
    public static string Deleted(int count, bool stillOn)
    {
        var head = count switch
        {
            0 => "There were no card files to delete.",
            1 => "Deleted 1 card file. Anything else in the folder was left alone.",
            _ => $"Deleted {count} card files. Anything else in the folder was left alone.",
        };
        return stillOn
            ? head + " The switch is still on, so the current character's card will be written again."
            : head;
    }
}
