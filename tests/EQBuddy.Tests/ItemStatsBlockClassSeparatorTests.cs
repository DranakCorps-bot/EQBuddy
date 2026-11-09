using System.IO.Compression;
using System.Text.Json;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **A COMMA ON A `Class:` LINE IS A SEPARATOR, NEVER PART OF A CLASS CODE** (DRA-4198, found
/// landing the 2026-11-02 refresh under DRA-4190).
///
/// <para>eqlwiki's newer item pages write the class line comma-separated
/// ("Class: ENC, MAG, NEC, WIZ"). `ItemStatsBlock.Parse` split on the space alone, so the catalog
/// shipped `["ENC,", "MAG,", "NEC,", "WIZ"]` — and the class-lock filter compares TOKENS, so every
/// class but the last one on the line was silently never offered the item. Four records on
/// `main` (Cap of Mending, Corroded Buckler, Spell: Armor of Faith, Twisted Silver Torque) and
/// sixteen after that refresh.</para>
///
/// <para>Three halves, so the rule cannot drift from what ships: the parser reads a committed
/// comma-separated line (the negative), the shipped Classes column carries no comma (the census),
/// and every record's column is what the parser reads off its own `StatsText` — so a parser change
/// that lands without its catalog, or a refresh built on an older parser, reddens by name.</para>
/// </summary>
public class ItemStatsBlockClassSeparatorTests
{
    /// <summary>Pyrosilk Gloves' own line (the 2026-11-02 refresh), Cap of Mending's double space
    /// after the colon, and the space-separated shape every older page uses.</summary>
    [Theory]
    [InlineData("Class: ENC, MAG, NEC, WIZ", new[] { "ENC", "MAG", "NEC", "WIZ" })]
    [InlineData("Class:  BER, BRD, BST", new[] { "BER", "BRD", "BST" })]
    [InlineData("Class: PAL,SHD", new[] { "PAL", "SHD" })]
    [InlineData("Class: PAL WAR", new[] { "PAL", "WAR" })]
    public void ACommaSeparatedClassLineReadsAsBareClassCodes(string line, string[] expected)
    {
        Assert.Equal(expected, ItemStatsBlock.Parse([line]).Classes);
    }

    /// <summary>A trailing comma must not hide the word that means "no restriction".</summary>
    [Fact]
    public void AllFollowedByACommaIsStillUnrestricted()
    {
        Assert.Empty(ItemStatsBlock.Parse(["Class: ALL,"]).Classes);
    }

    /// <summary>The effect the defect had, asserted on the filter that every surface uses: an
    /// enchanter is offered an item whose comma-separated line names ENC first.</summary>
    [Fact]
    public void TheClassLockAdmitsEveryClassOnACommaSeparatedLine()
    {
        var candidate = ItemStatsBlock.Parse(["Slot: HANDS", "AC: 9", "Class: ENC, MAG, NEC, WIZ"]);
        var worn = ItemStatsBlock.Parse(["Slot: HANDS", "AC: 2"]);

        foreach (var cls in new[] { "ENC", "MAG", "NEC", "WIZ" })
            Assert.Equal(DominanceVerdict.Yes,
                ItemDominance.Compare("Gloves", candidate, "Worn", worn, [cls]));
        Assert.NotEqual(DominanceVerdict.Yes,
            ItemDominance.Compare("Gloves", candidate, "Worn", worn, ["WAR"]));
    }

    /// <summary>The census: no shipped Classes token contains a comma. Prove-failed against the
    /// catalog as it stood on `main` before this card (4 records).</summary>
    [Fact]
    public void NoShippedClassesTokenContainsAComma()
    {
        var offenders = Committed
            .Where(r => r.Classes is { } cl && cl.Any(t => t.Contains(',')))
            .Select(r => $"{r.Name}: [{string.Join(" | ", r.Classes!)}]")
            .ToList();
        Assert.Empty(offenders);
    }

    /// <summary>The catalog is built through `ItemStatsBlock.Parse`, so its Classes column must be
    /// what the parser reads off the record's own text — for every record.</summary>
    [Fact]
    public void EveryCatalogClassesColumnIsWhatTheParserReadsOffItsOwnText()
    {
        var drift = Committed
            .Where(r => r.StatsText.Length > 0)
            .Select(r => (r.Name,
                Catalog: string.Join(" ", r.Classes ?? []),
                Parser: string.Join(" ", ItemStatsBlock.Parse(r.StatsText.Split('\n')).Classes)))
            .Where(x => x.Catalog != x.Parser)
            .Select(x => $"{x.Name}: catalog [{x.Catalog}], parser [{x.Parser}]")
            .ToList();
        Assert.Empty(drift);
    }

    /// <summary>Twisted Silver Torque was one of the four on `main`: its line names BRD first, and
    /// a bard was never offered it.</summary>
    [Fact]
    public void TwistedSilverTorqueIsOfferedToEveryClassItsLineNames()
    {
        var record = ItemCatalog.Default.Find("Twisted Silver Torque");
        Assert.NotNull(record);
        Assert.Contains("Class: BRD, PAL", record!.StatsText);
        Assert.Equal(["BRD", "PAL", "RNG", "ROG", "SHD", "WAR"], record.ToStatsBlock().Classes);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Every record in the COMMITTED file — not <see cref="ItemCatalog.All"/>, which folds
    /// case-variant names; the census is a count of the file.</summary>
    private static readonly List<ItemCatalog.Record> Committed = LoadCommitted();

    private static List<ItemCatalog.Record> LoadCommitted()
    {
        using var stream = typeof(ItemCatalog).Assembly
            .GetManifestResourceStream("EQBuddy.Core.Data.ItemCatalog.json.gz")!;
        using var gz = new GZipStream(stream, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<ItemCatalog.Root>(gz)!.Items;
    }
}
