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
/// </summary>
public static class ClassFilterLabel
{
    /// <summary>How many classes are spelled out before the label counts instead.
    /// Three is Legends' own limit on active classes, so a real character always
    /// sees its own classes named.</summary>
    public const int MaxNamed = 3;

    public static string For(IReadOnlyList<string> selected) => selected.Count switch
    {
        0 => "Any class",
        1 => selected[0],
        _ when selected.Count >= QuestClassFilter.Classes.Length => "All classes",
        <= MaxNamed => string.Join(" · ", selected.Select(QuestClassFilter.Abbrev)),
        _ => $"{selected.Count} classes",
    };
}
