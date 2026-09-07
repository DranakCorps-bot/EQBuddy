using System.IO;

namespace EQBuddy.Core;

/// <summary>
/// The game's `/outputfile spellbook` dump, read for ONE question: which spells has this
/// character actually scribed. It is the optional half of the buff-timer data path
/// (OE-5 LOCK A) — <see cref="BuffTracker"/> uses it to narrow a landing's candidate
/// spells, and for nothing else.
///
/// **This is never a timer source, and the rule is a product lock rather than a
/// preference.** A spellbook says what you KNOW; it cannot say what is on you, when it
/// landed, or when it ends. Every countdown still starts at a landing line and ends at a
/// fade line, exactly as it did before this file existed. What the dump buys is identity:
/// "a coat of shimmering runes surround you" is Rune IV or Rune V, and a character who has
/// only scribed one of them has told us which.
///
/// **The COLUMN LAYOUT of this dump is unverified, and this reader is written so that
/// does not matter.** Nobody on this project has a sample: the game's own usage line names
/// `spellbook` (quoted in <see cref="OutputfileAutoImport.KindOf"/> from Hateborne's log,
/// 2026-08-25) and that is the whole of what is known. The two dumps that HAVE been seen
/// are tab-separated with a header — inventory is `Location Name ID Count Slots`, factions
/// is `ID Name StandingValue PointsToMax` — and their name column sits in a different
/// place in each. So this reader does not guess a column. It collects EVERY field of every
/// line and leaves the deciding to the consumer, which intersects the result against a
/// known spell-name space (<see cref="BuffDurationCatalog.SpellNames"/>).
///
/// Over-collecting is safe here in a way that a wrong column guess is not: a header cell
/// or a slot label ("Name", "Spell Book Slot 3") matches no spell in the catalog, so it
/// narrows nothing, while a guess that picked the location column would have matched
/// nothing at all AND looked like it worked. Trap 23's lesson from the other side — the
/// staging that renders a real state is the one that fools you — and CLAUDE.md's rule that
/// we do not invent game data we have not seen. When a real sample arrives this reader can
/// tighten to the actual column; no consumer has to change for that.
/// </summary>
public static class SpellbookFile
{
    /// <summary>What one dump said, plus the two questions <see cref="BuffTracker"/> asks
    /// of it.</summary>
    /// <param name="Names">Every field the dump carried, case-insensitively. NOT "every
    /// spell" — see the class note; the consumer's intersection is what makes this a spell
    /// list.</param>
    public sealed record Snapshot(string Path, DateTime WrittenAt, HashSet<string> Names)
    {
        private HashSet<string>? _lines;

        /// <summary>Exactly this spell, rank and all — "Rune V" is not "Rune IV". This is
        /// the match that buys a per-rank duration out of the catalog.</summary>
        public bool Knows(string spell) =>
            spell is { Length: > 0 } && Names.Contains(spell.Trim());

        /// <summary>This spell's LINE, ranks folded — "Rune V" answers for "Rune IV" too.
        /// The fallback for a dump that spells its ranks some way we have not seen, and
        /// the match that still separates two different spells sharing one landing line
        /// (Boon of the Clear Mind vs Clarity).</summary>
        public bool KnowsLine(string spell)
        {
            if (spell is not { Length: > 0 }) return false;
            _lines ??= Names.Select(SpellCatalog.BaseName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _lines.Contains(SpellCatalog.BaseName(spell));
        }
    }

    /// <summary>Every field of every line, minus the empties and the numbers. Tab-separated
    /// like the other two dumps, and a file with no tabs at all degrades to one field per
    /// line rather than to nothing — the format is unverified in both directions.
    ///
    /// A trailing `*` is trimmed the way <see cref="InventoryFile.ParseEntries"/> trims it:
    /// that marker is the dump's own attuned/no-trade notation and is cosmetic wherever it
    /// appears.</summary>
    public static HashSet<string> Parse(IEnumerable<string> lines)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            foreach (var field in raw.Split('\t'))
            {
                var name = field.Trim().TrimEnd('*').Trim();
                if (name.Length == 0) continue;
                // A pure number is an id, a slot or a level in every dump the game writes,
                // and no spell in the catalog is called "7".
                if (long.TryParse(name, out _)) continue;
                names.Add(name);
            }
        }
        return names;
    }

    /// <summary>The newest spellbook dump for this character, parsed — or null when the
    /// player has never run the command, which is the ordinary case and not a fault.
    /// Finding it is <see cref="OutputfileAutoImport.FindLatest"/>'s job, in the one place
    /// that decides where a dump lives, so this finder cannot disagree with the other
    /// three (trap 4).</summary>
    public static Snapshot? FindLatest(string? logFolder, string character)
    {
        var file = OutputfileAutoImport.FindLatest(logFolder, character, OutputfileKind.Spellbook);
        if (file is null) return null;
        try
        {
            return new Snapshot(file.FullName, file.LastWriteTime,
                Parse(File.ReadLines(file.FullName)));
        }
        catch { return null; }
    }
}
