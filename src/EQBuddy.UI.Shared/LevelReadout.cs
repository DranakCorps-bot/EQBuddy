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
    ///
    /// <para><b>The class half since DRA-356 (DRA-352 D4)</b> comes from
    /// <c>CharacterLevel.BasisLabel</c>, the same one-table rule as the source: "Level 17 —
    /// the lowest of your equipped classes (Enchanter), set by you", or, when an equipped class
    /// has no level of its own, the fallback number followed by which class it could not
    /// weigh.</para>
    /// </summary>
    public static string Line(ResolvedLevel level)
    {
        if (!level.Known) return Unknown;
        var from = CharacterLevel.SourceLabel(level.Source);
        var basis = CharacterLevel.BasisLabel(level);
        if (level.LowestClass.Length > 0)
            return from.Length > 0 ? $"Level {level.Level} — {basis}, {from}" : $"Level {level.Level} — {basis}";
        var head = from.Length > 0 ? $"Level {level.Level} — {from}" : $"Level {level.Level}";
        return basis.Length > 0 ? $"{head}; {basis}" : head;
    }

    /// <summary>What the line says before anything knows. It names how the answer arrives on
    /// its own AND that the player can say it now, because the log only ever states a level
    /// at the ding — a character who has not levelled in a week has never told EQBuddy
    /// anything, and a room that only said "keep playing" would be asking them to grind for a
    /// number they already know.</summary>
    public const string Unknown =
        "Level not known yet — EQBuddy reads it from the log when you ding, and you can set "
        + "it here meanwhile.";

    /// <summary>
    /// **The level is a DROPDOWN since DRA-356 (DRA-352 D4)** — the PoS pair's shape (a
    /// compact list beside the class pill), replacing DRA-71 D3's Set level → type → Done
    /// box. A list cannot hold a half-typed number, so the box's whole reason for an announced
    /// commit key and a refusal sentence is gone with it; both strings went with the box.
    ///
    /// <para>The FACE is the resolved number — "what EQBuddy thinks" — and the line above it
    /// says where that came from (<see cref="Line"/>). Picking a row STATES it.</para>
    /// </summary>
    public static string Choice(int level) => $"Level {level}";

    /// <summary>The face while nothing knows. It names the action rather than drawing a blank
    /// or a "Level 0" nobody claimed; the line above it already says how the answer arrives
    /// on its own.</summary>
    public const string PickFace = "Pick a level";

    /// <summary>Hover copy on the dropdown: what picking does, per class (DRA-356). It says the
    /// per-class rule in the player's words — the classes below the pick rise to it, and only
    /// the lowest comes DOWN to it — because a player with a level-50 Warrior who picks 17 for
    /// a new class must be able to read that the Warrior stays 50.</summary>
    public const string PickerTip =
        "Your own statement about this character's level. Equipped classes below it rise to "
        + "it; only your lowest class comes down to it. A later ding in the log replaces it.";

    /// <summary>The way back — the dropdown's first row, and offered as a row only while a
    /// statement stands. The same words and the same reason as the class editor's
    /// (<c>HomeReadout.ClearStated</c>): without it a correction is one-way and a player who
    /// picked wrong once is telling EQBuddy forever. Clearing returns the line to the log's
    /// own reading.</summary>
    public const string ClearStated = "Let EQBuddy work it out";

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
            : CharacterLevel.BasisLabel(level) is { Length: > 0 } basis
                ? $"Weighed at level {level.Level}, {CharacterLevel.SourceLabel(level.Source)} — {basis}."
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
