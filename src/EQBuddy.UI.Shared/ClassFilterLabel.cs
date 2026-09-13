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
}
