using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE FOUNDER'S OWN DUMP, AGAINST THE SHIPPED CATALOG — before and after DRA-149 D2**
/// (plan P2; the FAIL's item 1, *"worn bow missing"*).
///
/// <para>Everything here runs over <c>tests/fixtures/inventory/dranak.txt</c> and
/// <see cref="ItemCatalog.Default"/>: his real twenty-one worn rows and the ~11k records that
/// actually ship. That is the whole point — the unit tests beside this one prove the RULES with
/// two-row fixtures, and none of them could have caught a spelling that only exists in the real
/// pair. A reduction of the situation is not the situation (trap 23).</para>
///
/// <para><b>The BEFORE is not narrated, it is RUN.</b> <see cref="PreAliasStats"/> is the
/// resolver with the alias table taken away, so the first test reproduces exactly what the
/// Founder saw — twenty anchors, no Range row, nothing said — and the second shows the same
/// dump with the slice in. A claim about what used to happen that cannot be executed is a
/// comment.</para>
/// </summary>
public class FounderWornSheetTests
{
    private const string BowInDump = "Deterioriated Ancient Faydark Longbow +2";
    private const string BowOnTheWiki = "Deteriorated Ancient Faydark Longbow";

    private static List<InventoryFile.Entry> Dump() =>
        InventoryFile.ParseEntries(File.ReadAllLines(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "fixtures", "inventory", "dranak.txt")));

    /// <summary>The catalog-first arm of <c>EqlWikiItemService.StatsFor</c>, which is the only
    /// arm that answers in a test run: the wiki disk cache is empty, and nothing here
    /// fetches.</summary>
    private static ItemStatsBlock? CatalogStats(string baseName) =>
        ItemCatalog.Default.Find(baseName) is { } rec
        && (rec.Slots.Count > 0 || rec.StatsText.Length > 0)
            ? rec.ToStatsBlock()
            : null;

    /// <summary>
    /// The same resolver with the curated table taken away — a name that resolves ONLY because
    /// an alias pointed at it answers null, exactly as it did before this slice.
    ///
    /// <para>It is written as a refusal of the alias TARGETS rather than as a second copy of
    /// the old lookup, because <c>WornFrom</c> now normalises before it asks: re-implementing
    /// the pre-slice fold here would be a second producer of "what is this item called" living
    /// in a test (trap 4), and it would go stale the first time the real one changed.</para>
    /// </summary>
    private static ItemStatsBlock? PreAliasStats(string baseName) =>
        ItemNameAliases.Rows.Any(r => r.Wiki.Equals(baseName, StringComparison.OrdinalIgnoreCase))
            ? null
            : CatalogStats(baseName);

    /// <summary>
    /// **WHAT THE FOUNDER SAW.** Twenty-one worn rows go in, twenty anchors come out, the Range
    /// row is not among them — and before this slice nothing on any surface could say so.
    /// </summary>
    [Fact]
    public void BeforeTheAliasTheBowIsDroppedAndTheSheetSaysSo()
    {
        var sheet = GearUpgrades.WornFrom(Dump(), PreAliasStats);

        Assert.Equal(20, sheet.Worn.Count);
        Assert.DoesNotContain("RANGE", sheet.Worn.Select(w => w.Slot));

        // The drop is the same drop. What is new is that it is COUNTED and NAMED, under the
        // spelling the game printed, which is the string the Founder was looking at.
        Assert.Equal([BowInDump], sheet.Unread);
        Assert.Contains(BowInDump, HelperPresentation.UnreadWorn(sheet.Unread));
    }

    /// <summary>
    /// **AND WHAT HE SEES NOW.** The same dump, the same shipped catalog, the alias in: the
    /// Range anchor EXISTS, it is the bow, its stats are the wiki's record, and there is
    /// nothing left to report as unread.
    /// </summary>
    [Fact]
    public void AfterTheAliasTheRangeAnchorIsTheBowAndNothingIsUnread()
    {
        var sheet = GearUpgrades.WornFrom(Dump(), CatalogStats);

        Assert.Equal(21, sheet.Worn.Count);
        Assert.Empty(sheet.Unread);
        Assert.Empty(HelperPresentation.UnreadWorn(sheet.Unread));

        var bow = Assert.Single(sheet.Worn, w => w.Slot == "RANGE");
        // The LABEL stays the dump's, "+2" and all (DRA-81 KEEP) — the picker's rows and the
        // stored worn picks are keyed on it.
        Assert.Equal(BowInDump, bow.Name);
        // …and the KEY is the wiki's, which is what `WornItem.BaseName` has always claimed to
        // be and could not be while a second "+N" stripper built it.
        Assert.Equal(BowOnTheWiki, bow.BaseName);
        Assert.Equal(14, bow.Stats.Dmg);
        Assert.Equal(55, bow.Stats.Delay);
    }

    /// <summary>
    /// **THE ANCHOR'S KEY AND THE CATALOG'S KEY CANNOT DISAGREE ANY MORE**, which is the seam
    /// the sweep's same-name refusal rests on.
    ///
    /// <para><c>GearUpgrades.Sweep</c> skips a record whose name equals the anchor's
    /// <c>BaseName</c> — "the catalog holds the item the player is wearing too". While
    /// <c>BaseName</c> was built by a second "+N" stripper that had never heard of a spelling,
    /// that refusal missed for exactly the items an alias covers, and the bow was free to be
    /// offered as an upgrade over itself the moment anything else about the comparison
    /// changed.</para>
    /// </summary>
    [Fact]
    public void EveryAnchorsBaseNameIsTheNameTheCatalogFiledItUnder()
    {
        foreach (var worn in GearUpgrades.WornFrom(Dump(), CatalogStats).Worn)
        {
            var record = ItemCatalog.Default.Find(worn.Name);
            Assert.NotNull(record);
            // Case-insensitively, which is the catalog's OWN key and the refusal's own
            // comparison: the dump prints "Raw-Hide Skullcap" where the wiki titles it
            // "Raw-hide Skullcap", and neither `ItemCatalog.Find` nor the same-name skip in
            // `Sweep` has ever cared. Demanding exact case here would be this test inventing a
            // stricter rule than the code it is about, and the alias table would then be asked
            // to carry a row for every capital letter in the game.
            Assert.Equal(record!.Name, worn.BaseName, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The fixture's own survey, pinned as numbers (trap 23: a shot whose figures you did not
    /// predict has not been reviewed, and the same goes for a fixture).
    ///
    /// <para>Twenty-one worn rows and exactly ONE of them unreadable before the slice. If a
    /// later refresh adds the game's spelling as a page, the alias row is deleted and
    /// <c>ItemNameAliasesTests</c> is what fails first; if a refresh drops the wiki's record,
    /// this count moves and says so here.</para>
    /// </summary>
    [Fact]
    public void TheFixtureHasTwentyOneWornRowsAndTheBowIsTheOnlyOneThatEverMissed()
    {
        var wornRows = Dump().Where(e => e.Worn && e.WornSlot.Length > 0).ToList();
        Assert.Equal(21, wornRows.Count);
        Assert.Single(wornRows, e => e.Name == BowInDump);

        // Not "some row missed" — the bow, and only the bow.
        Assert.Single(GearUpgrades.WornFrom(Dump(), PreAliasStats).Unread);
    }
}
