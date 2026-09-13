using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **THE RESOLVED LEVEL, IN WORDS — one producer for every surface** (DRA-71 D3, plan
/// P3/P4).
///
/// <para><b>It is its own file because it has more than one reader on the day it lands.</b>
/// The Character room draws it as an identity row and owns the editor beside it; the Helper
/// draws it to say which number it ranked with; the phone will draw it when D9's projection
/// lands. Three surfaces wording one fact is trap 4 with the sentence as the entry — and the
/// copy that goes stale is always the newest one, which on a shared fact is whichever
/// surface was written last.</para>
///
/// <para><b>The SOURCE half is not worded here.</b> <c>CharacterLevel.SourceLabel</c> is one
/// table in Core, the same ruling <c>CharacterClasses.SourceLabel</c> carries (Bevel,
/// Helm-signed 2026-08-23: *"do not grow a phone-only string"*, *"the phone must not compose
/// a second verb around SourceLabel"*). This file composes the LINE and reads that table for
/// the parenthetical; it never spells a source itself.</para>
///
/// <para><b>Nothing here tells the player what their level means.</b> No band, no "you are
/// ready for", no camp suggestion — those are the Helper's arithmetic and they carry their
/// own evidence labels. This says what EQBuddy thinks the number is and where it got it,
/// which is the whole of what a player needs to decide whether to argue with it.</para>
/// </summary>
public static class LevelReadout
{
    /// <summary>
    /// The level line: the number and where it came from.
    ///
    /// <para>Parallel to the class line one row up — "Level 30 — set by you" beside
    /// "Warrior · Druid (set by you)" — because the two answer the same kind of question
    /// about the same character and a player should read one voice. The DASH rather than the
    /// class line's parentheses is the one difference, and it is because a number followed by
    /// a bracket reads as a footnote marker.</para>
    ///
    /// <para><see cref="Unknown"/> when nothing knows, which is a real answer: the room says
    /// what would fill it rather than drawing a blank row.</para>
    /// </summary>
    public static string Line(ResolvedLevel level) =>
        !level.Known
            ? Unknown
            : CharacterLevel.SourceLabel(level.Source) is { Length: > 0 } from
                ? $"Level {level.Level} — {from}"
                : $"Level {level.Level}";

    /// <summary>What the line says before anything knows. It names how the answer arrives on
    /// its own AND that the player can say it now, because the log only ever states a level
    /// at the ding — a character who has not levelled in a week has never told EQBuddy
    /// anything, and a room that only said "keep playing" would be asking them to grind for a
    /// number they already know.</summary>
    public const string Unknown =
        "Level not known yet — EQBuddy reads it from the log when you ding, and you can set "
        + "it here meanwhile.";

    /// <summary>The door into the level editor. "Set", not "correct" or "override" — the same
    /// ruling that worded <c>HomeReadout.EditClasses</c>: being told to override your own
    /// character is a strange thing for an app to say.</summary>
    public const string Edit = "Set level…";

    /// <summary>The door's label while the editor is open. It COMMITS what is in the box and
    /// closes the row — see <see cref="EditorNote"/> for why this one does real work where
    /// the class editor's "Done" only closes a strip.</summary>
    public const string EditDone = "Done";

    /// <summary>
    /// Over the editor: what setting a level does, and how to commit it.
    ///
    /// <para><b>It says "press Enter" out loud, and that is a deliberate cost.</b> A box that
    /// committed on focus loss would write a half-typed number — a player typing "30" is at
    /// "3" for a moment, and this room rebuilds itself on a timer while a session is running,
    /// so the moment is reachable without them doing anything. An announced key is a smaller
    /// price than a silent wrong statement that then outranks their next ding.</para>
    /// </summary>
    public const string EditorNote =
        "Type the level this character actually is and press Enter. EQBuddy keeps using it "
        + "until your next ding says otherwise.";

    /// <summary>Placeholder-ish hover copy on the box itself, saying what happens next rather
    /// than repeating the note.</summary>
    public const string EditorTip =
        "Your own statement about this character's level. A later ding in the log replaces "
        + "it; an earlier one does not.";

    /// <summary>The way back, offered only while a statement stands — the same words and the
    /// same reason as the class editor's (<c>HomeReadout.ClearStated</c>): without it a
    /// correction is one-way and a player who typed it wrong once is telling EQBuddy forever.
    /// Clearing returns the line to the log's own reading.</summary>
    public const string ClearStated = "Let EQBuddy work it out";

    /// <summary>What the room says when the box held something it could not read. It names
    /// the refusal rather than silently reverting, because a control that appears to accept
    /// input and changes nothing is the silent no-op rule broken in the smallest possible
    /// way.</summary>
    public const string Refused =
        "That is not a level EQBuddy can use — type a whole number above zero.";

    /// <summary>
    /// What the Helper says about the number it ranked with — <b>the plan's "the Helper names
    /// the level it used"</b>.
    ///
    /// <para>It is a separate sentence from <see cref="Line"/> because the job is different:
    /// Character states a fact about the character, and the Helper discloses an INPUT to an
    /// answer the player is about to act on. A recommendation that quietly weighed a level
    /// the player disagrees with, and never said which, is the shape that makes somebody
    /// distrust the whole room.</para>
    /// </summary>
    public static string UsedByHelper(ResolvedLevel level) =>
        !level.Known
            ? HelperUnknown
            : $"Weighed at level {level.Level}, {CharacterLevel.SourceLabel(level.Source)}.";

    /// <summary>What the Helper says when it has no level. It says what it did anyway —
    /// because the answers above it are real and are ranked from the player's own play — and
    /// then points at the one room that can fix it. Naming the consequence rather than
    /// apologising: a player who does not care about the missing half should not be made to
    /// feel the room is broken.</summary>
    public const string HelperUnknown =
        "EQBuddy does not know this character's level yet, so nothing below is weighed "
        + "against one. The answers are still ranked from your own stored play.";
}
