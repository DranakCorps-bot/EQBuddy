using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Which classes the Character room's chips are showing, and what one click does.
///
/// <para><b>The line under the name is <see cref="CharacterClasses.Resolve"/>.</b> This
/// type is the editor in front of that line, not a second resolution of who the
/// character is (trap 33). The two agree: with no statement the chips seed from the
/// same reading the line is already showing, and with a statement both of them are
/// that statement and nothing else.</para>
///
/// <para><b>Why the seed exists.</b> The line can already name Paladin · Druid · Warrior
/// while the saved statement is empty. Chips that start blank turn a click on Paladin
/// into an ADD — the player was trying to take off a class the line was showing, and
/// the click stored it. Seeding once makes that click a removal. The seed is not
/// stored until a click; opening the editor and closing it again leaves the guess in
/// charge.</para>
///
/// <para><b>A standing statement is the whole selection.</b> The dump, the log and the
/// quest picks are not consulted again, so a class the player removed cannot ride back
/// in when the editor is rebuilt, the achievements file is re-read, or the log infers
/// the class they took off. Empty is the undo (<see cref="HomeReadout.ClearStated"/>):
/// no statement, and the guess may seed again.</para>
/// </summary>
public static class ClassStatement
{
    /// <summary>Whether the player has told EQBuddy who this character is. Blanks are
    /// not a statement — the same guard <see cref="CharacterClasses.Resolve"/> keeps,
    /// so the chips and the line cannot disagree about "nothing said".</summary>
    public static bool HasStatement(IReadOnlyList<string>? stated) =>
        stated is not null && stated.Any(static c => c is { Length: > 0 });

    /// <summary>
    /// The chips, in tick order, at most <see cref="CharacterClasses.Max"/>.
    ///
    /// <para>A standing statement answers alone. Otherwise the chips seed from
    /// <see cref="CharacterClasses.Resolve"/> with no statement — the dump leading, the
    /// log filling in, picks widening — which is the line the player is looking at.</para>
    /// </summary>
    public static IReadOnlyList<string> EditorSelection(
        IReadOnlyList<string>? stated,
        IReadOnlyList<string>? unlocked,
        IReadOnlyList<string>? inferred,
        IReadOnlyList<string>? picks)
    {
        if (HasStatement(stated))
            return Normalize(stated);
        var (guess, _) = CharacterClasses.Resolve(unlocked, inferred, picks);
        return guess;
    }

    /// <summary>
    /// One click against the selection the chips are showing.
    ///
    /// <para>A class that is on comes off. A class that is off comes on, unless the
    /// selection is already at <see cref="CharacterClasses.Max"/>, in which case the
    /// list comes back unchanged — the note above the chips announces the cap, and a
    /// fourth tick must not evict a class the player chose first. The result is the
    /// statement to store. It is not united with the guess: a class that is not in
    /// this list is not in the statement.</para>
    /// </summary>
    public static IReadOnlyList<string> Toggle(IReadOnlyList<string>? selection, string? cls)
    {
        var next = Normalize(selection).ToList();
        if (cls is not { Length: > 0 }) return next;
        if (next.RemoveAll(c => c.Equals(cls, StringComparison.OrdinalIgnoreCase)) > 0)
            return next;
        if (next.Count >= CharacterClasses.Max) return next;
        next.Add(cls);
        return next;
    }

    /// <summary>Tick order, blanks dropped, a repeated name kept once, capped at
    /// <see cref="CharacterClasses.Max"/>. The cap is applied here so a stored list
    /// that somehow grew past three cannot paint a fourth chip the line would not
    /// name.</summary>
    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? from)
    {
        if (from is null) return [];
        var list = new List<string>(CharacterClasses.Max);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cls in from)
        {
            if (list.Count >= CharacterClasses.Max) break;
            if (cls is { Length: > 0 } && seen.Add(cls)) list.Add(cls);
        }
        return list;
    }
}
