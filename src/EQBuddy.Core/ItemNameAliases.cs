namespace EQBuddy.Core;

/// <summary>
/// **WHERE THE GAME AND EQLWIKI SPELL ONE ITEM DIFFERENTLY** — a CURATED table, hand-written,
/// one row per measured miss (DRA-149 D2, plan P2).
///
/// <para><b>It exists because a spelling miss is SILENT and costs a whole worn slot.</b> The
/// Founder's dump prints <c>Deterioriated Ancient Faydark Longbow +2</c> — the game's own
/// spelling, with the extra <c>i</c> — and eqlwiki titles the page <c>Deteriorated Ancient
/// Faydark Longbow</c>. Every lookup in this app folds a name through
/// <see cref="EqlWikiItemService.NormalizeTitle"/> and then asks the catalog for it, so the
/// bow resolved to nothing, <see cref="GearUpgrades.WornFrom"/> dropped the row, and the
/// Helper showed twenty worn items with no way to say where the twenty-first went. The
/// Founder failed Farm Gear for it.</para>
///
/// <para><b>NEVER FUZZY, and that is the whole design</b> (plan P2). A near-match would let
/// EQBuddy answer about a DIFFERENT item — stats, drops and camps for something the player is
/// not wearing — and being uniquely wrong is the one failure this repo will not trade for
/// coverage (CLAUDE.md: match the wiki; departing needs decisive evidence). So the match is
/// WHOLE-STRING and case-insensitive, and a name this table has never been told about comes
/// back unchanged and is REPORTED as unread rather than guessed at. The committed negative in
/// <c>ItemNameAliasesTests</c> is exactly that: an unknown misspelling still resolves to
/// itself.</para>
///
/// <para><b>Curated, so it is hand-written only</b> — the same rule the spawn timers, the AA
/// catalog and <see cref="Tradeskills"/> keep. Nothing generates a row here and no refresh may
/// write one: a wrong alias is worse than a missing one, because a missing one says so out
/// loud and a wrong one answers confidently. Every row carries the evidence that put it here,
/// and <c>ItemNameAliasesTests</c> checks each one against the SHIPPED catalog in both
/// directions — the wiki title must be a record, and the game spelling must NOT be, because a
/// row whose game spelling already resolves is a rewrite of a name that was working.</para>
/// </summary>
public static class ItemNameAliases
{
    /// <summary>One curated row.</summary>
    /// <param name="Game">What the game prints — the inventory dump's own spelling, with any
    /// "+N" tier already folded off by <see cref="EqlWikiItemService.NormalizeTitle"/>.</param>
    /// <param name="Wiki">The eqlwiki page title, which is the key the shipped catalog is
    /// built on.</param>
    /// <param name="Evidence">Where this row came from. Not decoration: a curated file with an
    /// unsourced row in it is a guess wearing a table's clothes (trap 73), and the test reads
    /// this field.</param>
    public sealed record Alias(string Game, string Wiki, string Evidence);

    /// <summary>
    /// The table. ONE row today, and it stays one row until something is MEASURED.
    ///
    /// <para>The count is not a gap to fill. A second row arrives when a dump and a wiki page
    /// are seen to disagree, with the file and line that showed it — never from a sweep looking
    /// for names that nearly match, which is the fuzzy rule arriving by the back door.</para>
    /// </summary>
    public static readonly IReadOnlyList<Alias> Rows =
    [
        new("Deterioriated Ancient Faydark Longbow",
            "Deteriorated Ancient Faydark Longbow",
            "The Founder's committed inventory dump, tests/fixtures/inventory/dranak.txt line "
            + "52, prints \"Deterioriated Ancient Faydark Longbow +2\" in the Range slot; the "
            + "shipped catalog and eqlwiki title the page \"Deteriorated Ancient Faydark "
            + "Longbow\" (RANGE, DMG 14 / Delay 55, Crushbone: orc warlord, orc scoutsman). "
            + "Measured DRA-149: the game spelling matches 0 of 11,196 catalog records and the "
            + "wiki spelling matches exactly 1."),
    ];

    private static readonly Dictionary<string, string> ByGame =
        Rows.ToDictionary(r => r.Game, r => r.Wiki, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The wiki's spelling of <paramref name="title"/>, or <paramref name="title"/> unchanged.
    ///
    /// <para><b>Whole-string and case-insensitive, and nothing else.</b> No prefix, no
    /// containment, no edit distance: a name that merely CONTAINS a known game spelling is a
    /// different item and comes back untouched (asserted, because "the obvious extension" of a
    /// lookup table is the fuzzy match this ruling refused).</para>
    ///
    /// <para>Called from <see cref="EqlWikiItemService.NormalizeTitle"/>, which is the ONE seam
    /// every reader already goes through — the catalog's <c>Find</c>, the wiki cache's
    /// <c>CachedInfo</c>, the item window's heading and <c>WikiLinks.Search</c>. Putting it
    /// anywhere else would be a second answer to "what is this item called" (trap 4).</para>
    /// </summary>
    public static string Resolve(string title) =>
        title is { Length: > 0 } && ByGame.TryGetValue(title, out var wiki) ? wiki : title;
}
