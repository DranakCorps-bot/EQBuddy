using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Text for the Progress card's level-unlock rows (LevelUnlocks in Core picks WHAT
/// shows; this says HOW), shared so the Avalonia card renders the same words when it
/// adopts the feature.
/// </summary>
public static class LevelUnlockText
{
    /// <summary>Section label over the ding's list: "New at level 30".</summary>
    public static string NewAtLevelLabel(int level) => $"New at level {level}";

    /// <summary>The next-milestone preview's WORDS, counting each group it holds in list
    /// order: "At level 35: 2 new AA abilities, 3 new spells". No chevron — a surface
    /// that draws its own (<c>EqFoldLabel</c>) must not be handed one in the string.</summary>
    public static string NextWords(int level, int aaCount, int spellCount)
    {
        var parts = new List<string>(2);
        if (aaCount > 0)
            parts.Add($"{aaCount} new AA " + (aaCount == 1 ? "ability" : "abilities"));
        if (spellCount > 0)
            parts.Add($"{spellCount} new spell" + (spellCount == 1 ? "" : "s"));
        return $"At level {level}: " + string.Join(", ", parts);
    }

    /// <summary>The same label with a typed chevron in front, for the two card surfaces
    /// that still draw this fold as a plain TextBlock.
    ///
    /// **This is a hole in the glyph ratchet and it is worth knowing about.**
    /// `DesignRatchetTests` scans SOURCE, so a glyph laundered through a shared string
    /// helper reaches a migrated file invisibly: `MainWindow.xaml.cs` is on the ratchet
    /// and assigns this to `NextUnlocksLabel.Text` at runtime. Gate 5c converted seven
    /// folds to `EqFoldLabel` and missed this one for exactly that reason.
    ///
    /// The Progress breakout uses <see cref="NextWords"/> and draws the chevron as a
    /// vector. The two cards should follow; until they do, this keeps them unchanged
    /// rather than silently dropping their arrow.</summary>
    public static string NextLabel(int level, int aaCount, int spellCount, bool expanded) =>
        $"{(expanded ? "▾" : "▸")} " + NextWords(level, aaCount, spellCount);

    /// <summary>Right-column value: where the row comes from — its class, or its
    /// class-agnostic category (Archetype rows are labeled, never guessed per class:
    /// the wiki doesn't say which classes they cover) — plus the rank count when the
    /// ability has more than one to buy.</summary>
    public static string RowValue(AaCatalogEntry a) =>
        (a.Class is { Length: > 0 } cls ? cls : a.Category)
        + (a.MaxRank > 1 ? $" · {a.MaxRank} ranks" : "");

    /// <summary>Right-column value for a spell row: the picked classes gaining it at
    /// this level, plus the word that keeps spells apart from the AA rows —
    /// "Cleric spell", "Druid/Ranger spell".</summary>
    public static string SpellRowValue(SpellUnlock s) =>
        string.Join("/", s.Classes) + " spell" + (s.Derived ? DerivedMark : "");

    /// <summary>What a row derived from a spell page says, appended to its value column.
    ///
    /// **It is a mark, not a filter.** David's ruling (2026-08-23) is that the class page
    /// wins and anything taken from a spell page is FLAGGED — shown, and shown as less
    /// certain — and Bevel's lock is "do not silently pad from spell pages". This is not a
    /// rare footnote either: every class page on eqlwiki stops at level 50 while Legends
    /// caps at 60, so a level-50 character's entire next-level list is derived, plus the
    /// interior gaps (Paladin is missing seven sections; Rogue thirty-five).
    ///
    /// In the VALUE column, which already wraps, rather than beside the name — the name is
    /// what a click looks up on the wiki and what the eye scans down.</summary>
    public const string DerivedMark = " · from its spell page";

    /// <summary>Tooltip for a spell row: what the spell DOES, then every class that gets
    /// it and when.
    ///
    /// The description leads because it is the question being asked — David, 2026-08-23:
    /// *"have mouse over give the skill/spell description"* — and the class/level line
    /// follows as the provenance for it. It is eqlwiki's own prose carried verbatim, never
    /// composed here, which is the same rule as before: the row names the spell, the wiki
    /// explains it. All this changed is that you no longer open a browser to read it.
    ///
    /// A spell the wiki describes with nothing still hovers its class levels rather than
    /// an empty box, and AA rows are unaffected — they have shown
    /// <c>AaCatalog.Find(name)?.Effect</c> since the ledger existed, which is what made
    /// the spell rows' silence look like an oversight rather than a design.</summary>
    public static string? SpellTooltip(SpellLevelEntry? e)
    {
        if (e is null) return null;
        var levels = string.Join(" · ", e.Classes.Select(c => $"{c.Class} {c.Level}"));
        // ONE LINE, and the newline it obviously wanted is the reason. Both widgets
        // switch a tooltip to MONOSPACE the moment it contains "\n" (EqCardRows.cs:110,
        // CardParts.cs:77) — a good rule, because every other multi-line tip in this app
        // is a stat block whose columns need it. Wiki PROSE in monospace is not that, and
        // nothing could have caught it: a tooltip does not appear in a screenshot and no
        // test reads a font. The " · " separator is the house idiom anyway.
        return e.Description is { Length: > 0 } d
            ? (levels.Length > 0 ? d + " · " + levels : d)
            : levels;
    }
}
