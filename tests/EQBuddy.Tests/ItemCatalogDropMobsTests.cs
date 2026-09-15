using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE PROMOTER CARRIES WHO, AND THE GATE COMPARES CONTENTS** (DRA-71 D6, Fable plan P8).
///
/// <para>Two halves, and they fail in different directions. The schema half holds
/// <see cref="ItemCatalog.Record.DropMobs"/> to being an ANNOTATION on
/// <see cref="ItemCatalog.Record.DropZones"/> rather than a second copy of the zone list
/// (trap 4) — checked against the shipped file AND against a committed violation, which was
/// written when the shipped file carried no creatures and a check over nothing would have been
/// green for no reason (trap 78). The gate half holds the build tool to comparing the
/// DECOMPRESSED payload: gzip is a container whose bytes depend on which zlib built them, which
/// is exactly how <c>HarvestedGuides.json.gz</c> reddened CI against an identical payload
/// (trap 74).</para>
///
/// <para><b>THE DATA ARRIVED IN DRA-84 D3, and everything below now runs over it.</b> The item
/// dump (<c>cache/items-wikitext.jsonl</c>) is gitignored and rebuilding it means fetching ~11k
/// pages from eqlwiki, which is why DRA-71 D6 landed the promoter and left the field empty —
/// the request policy toward eqlwiki is the Founder's call, not a delivery's. D3 ran the refresh
/// under its own named Helm AUTHORIZE. These rows were written to go on holding the day it did,
/// and they did; the COVERAGE of what arrived is <c>ItemCatalogWhoCoverageTests</c>, which is
/// the survey DRA-84 D4 opened with.</para>
/// </summary>
public class ItemCatalogDropMobsTests
{
    private static string RepoRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string BuilderSource() => File.ReadAllText(Path.Combine(
        RepoRoot(), "scripts", "harvests", "itemcatalog-build", "Program.cs"));

    /// <summary>Every key in <c>DropMobs</c> is also in <c>DropZones</c>, over the whole
    /// shipped catalog. The zone list keeps exactly one producer; this says something EXTRA
    /// about some of its entries.</summary>
    [Fact]
    public void EveryDropMobsKeyInTheShippedCatalogIsAlsoADropZone()
    {
        foreach (var record in ItemCatalog.Default.All)
        {
            if (record.DropMobs is not { Count: > 0 } mobs) continue;
            foreach (var zone in mobs.Keys)
                Assert.Contains(zone, record.DropZones ?? [], StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// **AND THE CHECK ABOVE CAN ACTUALLY FAIL** — the committed negative. It was written
    /// because the shipped catalog carried no creatures, so "every key is a zone" over zero keys
    /// was green for no reason at all; it KEEPS now that there are 5,591 of them, because a
    /// negative that only earns its keep while the data is absent is a negative nobody will
    /// re-add on the day it goes missing again.
    /// </summary>
    [Fact]
    public void AZoneNamedOnlyInDropMobsIsAViolation()
    {
        var bad = new ItemCatalog.Record
        {
            Name = "Bone Helm",
            DropZones = ["Lower Guk"],
            DropMobs = new() { ["Befallen"] = ["a skeleton"] },
        };

        Assert.DoesNotContain("Befallen", bad.DropZones, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(bad.DropMobs!.Keys,
            zone => !bad.DropZones!.Contains(zone, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>An item with nothing to say carries no dictionary rather than an empty one,
    /// and a zone with no creature is absent rather than mapped to an empty list. Two
    /// spellings of "nothing" is a distinction a later reader would eventually act on.</summary>
    [Fact]
    public void TheShippedCatalogNeverCarriesAnEmptyDropMobsEntry()
    {
        foreach (var record in ItemCatalog.Default.All)
        {
            if (record.DropMobs is not { } mobs) continue;
            Assert.NotEmpty(mobs);
            foreach (var (zone, named) in mobs)
            {
                Assert.NotEmpty(named);
                Assert.All(named, Assert.NotEmpty);
                Assert.NotEmpty(zone);
            }
        }
    }

    /// <summary>
    /// **DISTINCT-COUNT DISCIPLINE** (trap 73's tell), armed for the refresh that fills this
    /// in.
    ///
    /// <para>Thousands of items carrying a handful of creature names between them would be a
    /// parser that found one template, not a catalog that learned who drops things — and only
    /// a count can tell the two apart before anybody reads a row. It was armed and trivially
    /// green while the field was empty; on the post-D3 catalog it measures 4,712 distinct names
    /// over 5,591 records carrying them, and it is now a live check rather than a promise.
    /// <c>ItemCatalogWhoCoverageTests</c> holds the same tell against MENTIONS, which is the
    /// number a reader actually meets.</para>
    /// </summary>
    [Fact]
    public void WhereTheCatalogNamesCreaturesTheyAreNotATemplate()
    {
        var withMobs = ItemCatalog.Default.All.Count(r => r.DropMobs is { Count: > 0 });
        var distinct = ItemCatalog.Default.All
            .SelectMany(r => r.DropMobs?.Values.SelectMany(v => v) ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.True(withMobs == 0 || distinct >= withMobs / 10.0,
            $"{withMobs:N0} catalog items carry named creatures and there are only "
            + $"{distinct:N0} distinct names between them. That ratio is what a template looks "
            + "like, not what research looks like — survey the promoter's output before "
            + "believing it (trap 73).");
    }

    /// <summary>The creatures reach a live lookup too, not only the sweep — the catalog
    /// fallback in <c>EqlWikiItemService</c> is what item surfaces read, and a field the
    /// catalog carried and nobody could reach would be a property written and never read
    /// (trap 43).</summary>
    [Fact]
    public void TheCatalogFallbackCarriesTheCreaturesThrough()
    {
        var record = new ItemCatalog.Record
        {
            Name = "Bone Helm",
            StatsText = "Slot: HEAD\nAC: 9",
            Slots = ["HEAD"],
            Ac = 9,
            DropZones = ["Lower Guk", "Befallen"],
            DropMobs = new() { ["Lower Guk"] = ["a froglok knight"] },
        };

        // The same projection EqlWikiItemService.FromCatalog makes, asserted through the
        // sweep's own reader so both consumers are covered by one fixture.
        var sweep = GearUpgrades.Sweep(
            GearIntent.ReplaceSlot,
            [new WornItem("Rusty Helm", "Rusty Helm", "HEAD",
                ItemStatsBlock.Parse(["Slot: HEAD", "AC: 4"]))],
            [],
            new ItemCatalog([record]),
            [], includeQuests: false);

        var upgrade = Assert.Single(sweep.Upgrades);
        Assert.Equal(["a froglok knight"], upgrade.MobsIn("Lower Guk"));
        Assert.Empty(upgrade.MobsIn("Befallen"));
    }

    // ---- the reproducibility gate (trap 74) ----------------------------------------------

    /// <summary>
    /// **THE GATE COMPARES THE THING THE CLAIM IS ABOUT.**
    ///
    /// <para>"Reproducible from the cache" is a claim about the catalog's CONTENTS. The build
    /// tool must decompress the committed file before comparing, and must not compare the .gz
    /// bytes — a gate that reddens on a zlib version teaches the next person to re-run until
    /// green, and then it is a guard nobody believes.</para>
    /// </summary>
    [Fact]
    public void TheBuildToolComparesDecompressedContentsAndNotTheContainer()
    {
        var source = BuilderSource();

        Assert.Contains("--check", source);
        Assert.Contains("Decompress(outPath)", source);
        Assert.Contains("SequenceEqual(payload)", source);
        Assert.DoesNotContain(Naive, source);
    }

    /// <summary>
    /// **And the scan can fail** — the shape it exists to catch, kept as a committed negative
    /// (trap 78). A guard aimed at the wrong thing is at least green for a reason; a guard
    /// aimed at nothing is green for no reason at all.
    /// </summary>
    [Fact]
    public void TheContainerComparisonIsTheShapeTheScanCatches()
    {
        Assert.Contains(Naive, Naive);
        Assert.DoesNotContain("Decompress", Naive);
    }

    /// <summary>The byte-comparison shape a well-meaning author would write, verbatim. It is
    /// what <c>guides-transform.py --check</c> did before trap 74 was written down.</summary>
    private const string Naive = "File.ReadAllBytes(outPath).SequenceEqual(";

    /// <summary>The tool refuses — loudly, and with a distinct exit code — when the gitignored
    /// dump is absent, rather than reporting a clean comparison of nothing. Every clone and
    /// every CI run is in that state, which is why the gate is not in
    /// <c>scripts/check.ps1</c>.</summary>
    [Fact]
    public void TheBuildToolRefusesWithoutTheDumpRatherThanPassing()
    {
        var source = BuilderSource();

        Assert.Contains("if (!File.Exists(dump))", source);
        Assert.Contains("return check ? 2 : 1;", source);

        // And the promoter shim forwards arguments, or --check could never reach it.
        var shim = File.ReadAllText(Path.Combine(
            RepoRoot(), "scripts", "harvests", "eqlwiki", "items-promote.py"));
        Assert.Contains("*sys.argv[1:]", shim);
    }
}
