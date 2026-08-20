using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One mez spell in the Options editor.</summary>
/// <param name="Spell">Base spell name — what the player typed against.</param>
/// <param name="DurationText">The effective duration, formatted for the box.</param>
/// <param name="Source">Where that number came from.</param>
/// <param name="SourceNote">One line saying so, for under the row.</param>
public readonly record struct MezDurationRow(
    string Spell, string DurationText, MezDurationSource Source, string SourceNote);

/// <summary>
/// The mez-duration editor's rows, built once for both widgets.
///
/// The precedence is spawn timers' own — typed &gt; learned &gt; catalog — and this exists
/// so the two Options windows cannot come to different words about it. #210's rule: when
/// a surface exists on two screens, the decision goes in UI.Shared and both call it.
/// </summary>
public static class MezDurationRows
{
    /// <summary>Every catalog mez spell, in catalog order, with the duration that would
    /// actually be used for a chip right now and a note saying where it came from.</summary>
    public static List<MezDurationRow> Build(MezTracker tracker)
    {
        var rows = new List<MezDurationRow>();
        foreach (var spell in tracker.CatalogSpells)
        {
            var (seconds, source) = tracker.ResolveDuration(spell.Name);
            rows.Add(new MezDurationRow(
                spell.Name,
                MezDurationText.Format(seconds),
                source,
                Note(source, spell)));
        }
        return rows;
    }

    /// <summary>What to say under a row. The catalog line names its source, because
    /// "documented" and "measured on your own machine" are different claims and the
    /// player is entitled to know which one they are about to override.</summary>
    public static string Note(MezDurationSource source, MezSpellInfo spell) => source switch
    {
        MezDurationSource.Typed =>
            "yours — outranks anything EQBuddy works out, until you clear the box",
        MezDurationSource.Learned =>
            "measured from your own casts wearing off; clear a typed value to come back to this",
        MezDurationSource.Catalog when spell.Source.Length > 0 => $"as documented ({spell.Source})",
        MezDurationSource.Catalog => "as documented",
        _ => "no duration known — the chip shows the mez without a countdown",
    };

    /// <summary>The header blurb, shared so it cannot drift between the two windows.</summary>
    public const string Blurb =
        "Mez chips count down using the longest clean fade EQBuddy has seen you cast, " +
        "falling back to the documented duration. Type over any of them and your number " +
        "wins from then on — ranks lengthen mezzes, so if yours runs longer than the book " +
        "says, this is where to say so. Clear a box to hand it back to EQBuddy; anything " +
        "it has learned in the meantime takes over.";
}
