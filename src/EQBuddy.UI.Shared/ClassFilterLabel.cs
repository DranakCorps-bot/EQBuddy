using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The face of the quest window's class multi-select, and the width it is allowed to
/// claim.
///
/// The button used to join every picked class with " · ". Legends lets a character have
/// three, so that read fine — until someone ticked all sixteen to browse another class's
/// list and the label became "BRD · BST · BER · CLR · DRU · ENC · MAG · MNK · NEC · PAL ·
/// RNG · ROG · SHD · SHM · WAR · WIZ". In a fixed-width window that button then ate the
/// whole filter row and pushed the mine/zone/held/done/all strip clean off the edge
/// (#184, bjstrange — his screenshot shows it gone entirely).
///
/// So the label is capped. Past <see cref="MaxNamed"/> it counts instead of listing,
/// because a list you cannot read is worth less than a number you can.
///
/// <para><b>THE RULE ITSELF MOVED TO <see cref="PickerFace"/> (DRA-71 D2).</b> This file
/// is now the class picker's NOUN and nothing else. The Helper's goals and factions are
/// the same control with different words, and #184's lesson re-typed per surface is a
/// lesson that holds on some of them — so the arithmetic is shared and this is where the
/// class-shaped facts live: the noun, the abbreviations, and the fact that sixteen is all
/// of them. Behaviour is unchanged in every case
/// <c>ClassFilterLabelTests</c> pins, which is the point of moving it this way.</para>
/// </summary>
public static class ClassFilterLabel
{
    /// <summary>How many classes are spelled out before the label counts instead.
    /// Three is Legends' own limit on active classes, so a real character always
    /// sees its own classes named.</summary>
    public const int MaxNamed = PickerFace.MaxNamed;

    public static string For(IReadOnlyList<string> selected) => PickerFace.For(
        selected, "class", "classes",
        offered: QuestClassFilter.Classes.Length,
        abbreviate: QuestClassFilter.Abbrev);

    /// <summary>The quick-select's own words (DRA-216 D1, S4.3). The requirements document
    /// and the plan both name the control <c>My Classes</c>, so that is what it is called on
    /// screen — a surface that renames the thing a requirement asks for costs a reader the
    /// join.</summary>
    public const string MyClasses = "My Classes";

    /// <summary>
    /// What the quick-select's hover says: the classes it will tick, then WHERE they came
    /// from.
    ///
    /// <para>Naming them is the whole of the honesty here. The action replaces the selection,
    /// so a player about to click it is entitled to know what it is about to become — and the
    /// source is the difference between the game's own statement and a guess off the log,
    /// which is exactly the judgement they need to decide whether to trust it.</para>
    ///
    /// <para><b><see cref="CharacterClasses.SourceLabel"/> rides VERBATIM in a parenthetical</b>
    /// — the same construction the identity note above the list already uses. Bevel's
    /// Helm-signed lock is that SourceLabel is one table and nobody composes a second verb
    /// around it: the verb here belongs to the classes ("Tick …"), and the parenthetical is
    /// the table's string untouched.</para>
    /// </summary>
    /// <param name="mine">What <c>QuestClassLens.MyClasses</c> answered. Never abbreviated:
    /// a hover has the room the face does not, and the codes exist for the face's width
    /// budget (#184), not for prose.</param>
    public static string MyClassesTip(IReadOnlyList<string> mine, ClassSource source)
    {
        if (mine is null || mine.Count == 0) return "";
        var named = $"Tick {string.Join(" · ", mine)}";
        var from = CharacterClasses.SourceLabel(source);
        return from.Length > 0 ? $"{named} ({from})" : named;
    }
}
