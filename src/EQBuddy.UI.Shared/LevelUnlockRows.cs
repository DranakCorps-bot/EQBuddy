using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// How a <see cref="LevelUnlockSet"/> becomes a list of rows — the merged AA-then-Spells
/// order, and what each row's hover says.
///
/// These were <c>static</c> members of <c>MainWindow</c>, which meant the Progress
/// BREAKOUT reached into the widget class to call them (<c>MainWindow.UnlockRows</c>,
/// <c>MainWindow.UnlockTooltip</c>). That is the shape §11.9 named as the real cost of
/// lifting files without moving dependencies: the surface moves, and a fistful of
/// internals stays behind so the moved thing can reach back. Moving the DECISION here
/// instead means the card, the breakout and any later surface all call one framework-free
/// function — and it can be unit-tested, which on the WPF side is otherwise impossible
/// (docs/TestPlan.md §5).
///
/// The rule that makes this non-trivial: a merged list has two kinds of row in it, and
/// only the SET knows which kind a name came from. Every lookup here is resolved per set
/// for that reason — asking "is this a spell?" globally would answer wrongly for an AA
/// that shares a name with one.
/// </summary>
public static class LevelUnlockRows
{
    /// <summary>The AA group in its category order, then the Spells grouping — one list,
    /// rows told apart by their value column rather than by a heading between them.</summary>
    public static IEnumerable<(string Name, string Value)> Rows(LevelUnlockSet set) =>
        set.Aas.Select(a => (a.Name, LevelUnlockText.RowValue(a)))
            .Concat(set.Spells.Select(sp => (sp.Name, LevelUnlockText.SpellRowValue(sp))));

    /// <summary>Did this name come from the set's SPELLS half? The one question the two
    /// row kinds differ on, asked per set because only the set can answer it.</summary>
    public static bool IsSpell(LevelUnlockSet set, string name) =>
        set.Spells.Any(sp => sp.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Hover text for a merged list: a spell row shows which classes get it and
    /// when — catalog facts, never invented effect prose — and an AA row keeps the wiki's
    /// own effect text.</summary>
    public static Func<string, string?> Tooltip(LevelUnlockSet set) =>
        name => IsSpell(set, name)
            ? LevelUnlockText.SpellTooltip(SpellLevelCatalog.Default.Find(name))
            : AaCatalog.Find(name)?.Effect;

    /// <summary>Where the wiki's AA facts live: one page of tables, no per-ability pages,
    /// so an AA row's click has exactly one honest destination.</summary>
    public const string AaWikiPage = "Alternate Advancement";

    /// <summary>Which wiki page a row should open — its own if it is a spell, the single
    /// AA page otherwise. Returns the TITLE rather than opening anything, so this stays
    /// framework-free and both UIs apply their own navigation.</summary>
    public static string WikiPageFor(LevelUnlockSet set, string name) =>
        IsSpell(set, name) ? name : AaWikiPage;
}
