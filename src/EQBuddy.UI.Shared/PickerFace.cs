namespace EQBuddy.UI.Shared;

/// <summary>
/// **WHAT A MULTI-SELECT DROPDOWN'S FACE SAYS, FOR ANY NOUN** (DRA-71 D2, plan P1).
///
/// <para>This is <see cref="ClassFilterLabel"/>'s rule with the word "class" taken out of it.
/// That rule was paid for once already: the quest window's class button joined every picked
/// class with " · ", which read fine for the three a Legends character can have and became
/// <i>"BRD · BST · BER · CLR · DRU · ENC · MAG · MNK · NEC · PAL · RNG · ROG · SHD · SHM ·
/// WAR · WIZ"</i> the moment someone ticked all sixteen to browse — a face that ate the
/// filter row and pushed the mode strip clean off the window (#184, bjstrange). The fix was
/// a cap. The reason it lives here now is that the Helper's goals, its factions, and every
/// sub-picker the later slices add are the same control with a different noun, and a rule
/// re-typed per surface is a rule that holds on some of them.</para>
///
/// <para><b>THE CAP IS A WIDTH, AND IT ALWAYS WAS.</b> <see cref="ClassFilterLabel"/> counts
/// past three because three abbreviations is as much as its row can hold — the count was the
/// mechanism, the width was the point (its own test says so: <i>"the actual defect: label
/// width tracked the number of classes picked"</i>). Generalising by COUNT alone would have
/// shipped the #184 defect straight back on the Helper, whose nouns are not three-letter
/// codes: <c>"Unlock Classes · Unlock Races · Farm Materials"</c> is three picks and
/// forty-five characters. So both bounds are honoured, and a caller that needs a roomier face
/// raises <paramref name="maxChars"/> with a reason rather than opting out of the cap.</para>
///
/// <para><b>ONE PICK IS ALWAYS NAMED.</b> "1 goal" is strictly less than the goal's name and
/// no narrower than it needs to be, so the budget does not apply to a single selection — which
/// is also exactly what the class face has always done ("Cleric", not "1 class").</para>
///
/// <para>Framework-free and string-only, so the phone can draw the same face from the same
/// rule when the Helper reaches it (plan P15) rather than growing a second copy the way the
/// Avalonia chip stacks once did.</para>
/// </summary>
public static class PickerFace
{
    /// <summary>How many picks are spelled out before the face counts instead. Three is
    /// Legends' own limit on active classes, so a real character always sees its own
    /// classes named — and it is a sensible ceiling for any noun: a face is a summary,
    /// and the list itself is one click away inside the popup.</summary>
    public const int MaxNamed = 3;

    /// <summary>How many characters the face may claim by default — the bound
    /// <c>ClassFilterLabelTests.TheLabelNeverGrowsWithTheSelection</c> has asserted since
    /// #184 ("BRD · BST · BER" plus one). A face that shares its row with other controls
    /// keeps this; one that owns its own row may ask for more.</summary>
    public const int MaxChars = 16;

    /// <summary>
    /// The face text.
    /// </summary>
    /// <param name="picked">What is ticked, in the spelling the rows use.</param>
    /// <param name="singular">The noun for the empty state — "class", "goal", "faction".
    /// Reads as "Any class".</param>
    /// <param name="plural">The noun for the counted and complete states — "classes",
    /// "goals". Reads as "4 goals" / "All goals".</param>
    /// <param name="offered">How many rows the picker holds. Ticking all of them says so
    /// by name rather than counting, because "All classes" is a state a player recognises
    /// and "16 classes" is a number they have to compare. Pass 0 when the offer is open
    /// ended (a capped list, where "all of them" is not a thing the face can know).</param>
    /// <param name="abbreviate">Applied when more than one is named, where the noun HAS a
    /// short form. Classes do; goals and factions do not, and a face that invented one
    /// would be naming something the rows do not call it.</param>
    /// <param name="maxNamed">See <see cref="MaxNamed"/>.</param>
    /// <param name="maxChars">See <see cref="MaxChars"/>.</param>
    public static string For(
        IReadOnlyList<string> picked,
        string singular,
        string plural,
        int offered = 0,
        Func<string, string>? abbreviate = null,
        int maxNamed = MaxNamed,
        int maxChars = MaxChars)
    {
        if (picked is null || picked.Count == 0) return $"Any {singular}";
        // A single pick is its own name, whatever the budget — see the class summary.
        if (picked.Count == 1) return picked[0];
        if (offered > 0 && picked.Count >= offered) return $"All {plural}";

        var counted = $"{picked.Count} {plural}";
        if (picked.Count > maxNamed) return counted;

        var named = string.Join(" · ",
            abbreviate is null ? picked : picked.Select(abbreviate));
        return named.Length <= maxChars ? named : counted;
    }
}
