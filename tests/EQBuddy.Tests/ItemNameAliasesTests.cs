using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE CURATED GAME→WIKI SPELLING TABLE** (DRA-149 D2, plan P2).
///
/// <para>A curated file is only as good as the survey behind it (trap 73), so every row is
/// checked against the SHIPPED catalog in BOTH directions rather than read: the wiki title must
/// be a record, and the game spelling must NOT be. A row whose game spelling already resolves
/// is not a fix, it is a rewrite of a name that was working — and it would be invisible,
/// because the lookup would go on succeeding.</para>
///
/// <para>The committed negative is the one the ruling turns on: an unknown misspelling comes
/// back UNCHANGED, so it stays unread and gets said out loud instead of being guessed at.</para>
/// </summary>
public class ItemNameAliasesTests
{
    /// <summary>The Founder's bow, end to end through the seam every reader uses. This is the
    /// row the slice exists for: the dump's spelling now finds the catalog record, with the
    /// "+N" folded off on the way.</summary>
    [Theory]
    [InlineData("Deterioriated Ancient Faydark Longbow +2")]
    [InlineData("Deterioriated Ancient Faydark Longbow +8")]
    [InlineData("Deterioriated Ancient Faydark Longbow")]
    [InlineData("deterioriated ancient faydark longbow +2")]
    public void TheFoundersBowResolvesToTheWikisSpellingThroughTheOneSeam(string inGame)
    {
        Assert.Equal(
            "Deteriorated Ancient Faydark Longbow",
            EqlWikiItemService.NormalizeTitle(inGame));

        // …and that is not a string equality dressed up as a fix — the SHIPPED catalog answers.
        var record = ItemCatalog.Default.Find(inGame);
        Assert.NotNull(record);
        Assert.Equal("Deteriorated Ancient Faydark Longbow", record!.Name);
        Assert.Contains("RANGE", record.Slots);
    }

    /// <summary>
    /// **NEVER FUZZY, and this is the assertion that says so.**
    ///
    /// <para>Every one of these is a near-miss of a real row, and every one of them must come
    /// back untouched: a longer name CONTAINING the game spelling is a different item, a
    /// shorter one is a different item, and a name nobody has measured is an item EQBuddy has
    /// not read about. Answering any of them would let the room describe stats, drops and camps
    /// belonging to something the player is not wearing, which is the "uniquely wrong" failure
    /// this repo refuses to trade for coverage.</para>
    /// </summary>
    [Theory]
    [InlineData("Deterioriated Ancient Faydark Shortbow")]
    [InlineData("Ancient Faydark Longbow")]
    [InlineData("Ornate Deterioriated Ancient Faydark Longbow")]
    [InlineData("Deterioriated Ancient Faydark Longbow of the Sky")]
    [InlineData("Rusty Braod Sword")]
    [InlineData("")]
    public void AnUnknownMisspellingResolvesToItselfAndIsNeverGuessedAt(string name)
    {
        Assert.Equal(name, ItemNameAliases.Resolve(name));
    }

    /// <summary>
    /// Every row is REAL, in both directions, against the shipped catalog.
    ///
    /// <para>The second half is the one that would go stale silently: if a later refresh adds a
    /// page under the GAME's spelling, this row stops being a fix and starts being a redirect
    /// away from a record that exists. Then it should be deleted, and this is what says so.</para>
    /// </summary>
    [Fact]
    public void EveryCuratedRowNamesAWikiRecordAndAGameSpellingTheCatalogLacks()
    {
        Assert.NotEmpty(ItemNameAliases.Rows);
        foreach (var row in ItemNameAliases.Rows)
        {
            Assert.NotEqual(row.Game, row.Wiki, StringComparer.OrdinalIgnoreCase);

            // The wiki side is a record. Found by NAME rather than through Find(), which would
            // route back through the alias and pass for a row pointing at nothing.
            Assert.True(
                ItemCatalog.Default.All.Any(r =>
                    r.Name.Equals(row.Wiki, StringComparison.OrdinalIgnoreCase)),
                $"alias target '{row.Wiki}' is not a record in the shipped catalog");

            // The game side is NOT a record — otherwise the row rewrites a working name.
            Assert.False(
                ItemCatalog.Default.All.Any(r =>
                    r.Name.Equals(row.Game, StringComparison.OrdinalIgnoreCase)),
                $"alias source '{row.Game}' IS a record in the shipped catalog — the row is a "
                + "rewrite of a name that already resolved, not a fix; delete it");

            // A curated row with no evidence is a guess wearing a table's clothes (trap 73).
            Assert.NotEmpty(row.Evidence);
            Assert.Contains(row.Wiki, row.Evidence, StringComparison.OrdinalIgnoreCase);
        }

        // One key per game spelling: two rows for one name is a table that answers differently
        // depending on which one the dictionary happened to keep.
        Assert.Equal(
            ItemNameAliases.Rows.Count,
            ItemNameAliases.Rows.Select(r => r.Game)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>The tier suffix comes off FIRST, so one row covers every "+N" — otherwise the
    /// table would need a row per tier and the Founder's "+8" would still be unread while his
    /// fixture's "+2" worked.</summary>
    [Fact]
    public void TheTierSuffixIsFoldedBeforeTheTableIsAsked()
    {
        foreach (var row in ItemNameAliases.Rows)
            for (var tier = 1; tier <= 9; tier++)
                Assert.Equal(row.Wiki, EqlWikiItemService.NormalizeTitle($"{row.Game} +{tier}"));
    }

    /// <summary>
    /// **THE PLAYER'S WIKI DOOR GOES THROUGH THE SAME SEAM**, so an aliased name opens the page
    /// that actually exists rather than searching for the spelling that failed.
    ///
    /// <para>And a name the table has never heard of searches for the GAME's spelling, which is
    /// the right answer for the door under the unread sentence: that is the string the player
    /// can compare against what the wiki shows them.</para>
    /// </summary>
    [Fact]
    public void TheWikiSearchDoorCarriesTheResolvedSpelling()
    {
        Assert.Contains(
            Uri.EscapeDataString("Deteriorated Ancient Faydark Longbow"),
            UI.Shared.WikiLinks.Search("Deterioriated Ancient Faydark Longbow +2"));

        Assert.Contains(
            Uri.EscapeDataString("Mystery Pauldrons"),
            UI.Shared.WikiLinks.Search("Mystery Pauldrons +3"));
    }
}
