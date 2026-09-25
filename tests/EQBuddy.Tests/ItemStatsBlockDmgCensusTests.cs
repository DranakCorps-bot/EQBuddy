using System.IO.Compression;
using System.Text.Json;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **EVERY `… Dmg:` SPELLING THE SHIPPED CATALOG HAS, AND WHICH OF THEM IS DAMAGE** (DRA-251,
/// Helm SIGN on PR #898: Q1 admit, Q2 exact spelling list, Q3 pin the whole census).
///
/// <para>`ItemStatsBlock.Parse` reads a key through an EXACT switch, and that list is the only
/// thing keeping the elemental and bane keys out of the damage slot — `PairRx` already matches
/// every one of them. All eleven records that carry one ALSO carry a plain `DMG:`, so a pattern
/// admitting them would overwrite true base damage with a bonus number. This file pins the
/// census so a refresh that brings a seventh spelling, or a second `Base Dmg` record, reddens
/// by count and says which.</para>
///
/// <para>The census reads the committed `StatsText` with plain string search — no regex, so the
/// census cannot share a blind spot with the parser it is checking.</para>
/// </summary>
public class ItemStatsBlockDmgCensusTests
{
    /// <summary>The whole census, measured 2026-09-25 against the committed catalog (11,196
    /// records): the key, how many records carry it, and whether it is read as damage.</summary>
    private static readonly (string Key, int Records, bool Admitted)[] Census =
    [
        ("DMG", 1_648, true),
        ("Base Dmg", 1, true),
        ("Bane Dmg", 4, false),
        ("Cold Dmg", 3, false),
        ("Fire Dmg", 2, false),
        ("Poison Dmg", 2, false),
    ];

    /// <summary>The eleven records carrying an elemental or bane key — every one of them also
    /// carries a plain `DMG:`, which is what a pattern would have overwritten.</summary>
    private static readonly string[] ElementalRecords =
    [
        "Blessed Champion Arrows", "Blessed Guardian Arrows", "Fleeting Memory",
        "Gnoll Slayer", "Gnoll Slayer (final)", "Greenmist", "Gunthak Dagger",
        "Gunthak Scimitar", "Mithril Champion Arrows", "Staff of the Spiritcharmer",
        "Steel Guardian Arrows",
    ];

    [Fact]
    public void TheCatalogCarriesExactlyTheSixDmgSpellingsInTheirMeasuredCounts()
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var record in Committed)
            foreach (var key in DmgKeys(record.StatsText))
                counts[key] = counts.GetValueOrDefault(key) + 1;

        Assert.Equal(
            Census.OrderBy(c => c.Key, StringComparer.Ordinal)
                  .Select(c => $"{c.Key}: {c.Records}"),
            counts.Select(kv => $"{kv.Key}: {kv.Value}"));
    }

    [Fact]
    public void TheAdmittedSetIsExactlyDmgAndBaseDmg()
    {
        var admitted = Census
            .Where(c => ItemStatsBlock.Parse([$"{c.Key}: 9"]).Dmg == 9)
            .Select(c => c.Key)
            .ToList();

        Assert.Equal(["DMG", "Base Dmg"], admitted);
        Assert.Equal(Census.Where(c => c.Admitted).Select(c => c.Key), admitted);
    }

    /// <summary>The four elemental keys are committed NEGATIVES, by name: each is unread on its
    /// own, and on every shipped record that carries one the damage is the plain `DMG:`.</summary>
    [Theory]
    [InlineData("Bane Dmg")]
    [InlineData("Cold Dmg")]
    [InlineData("Fire Dmg")]
    [InlineData("Poison Dmg")]
    public void AnElementalDmgKeyIsNeverReadAsDamage(string key)
    {
        Assert.Null(ItemStatsBlock.Parse([$"{key}: 9"]).Dmg);
        Assert.Null(ItemStatsBlock.Parse([$"{key.ToUpperInvariant()}: 9"]).Dmg);

        var carriers = Committed
            .Where(r => DmgKeys(r.StatsText).Contains(key))
            .ToList();
        Assert.Equal(Census.Single(c => c.Key == key).Records, carriers.Count);
        Assert.All(carriers, r =>
        {
            Assert.Contains("DMG", DmgKeys(r.StatsText));
            Assert.Equal(PlainDmg(r.StatsText), Parse(r).Dmg);
            Assert.Equal(PlainDmg(r.StatsText), r.ToStatsBlock().Dmg);
        });
    }

    [Fact]
    public void TheElevenElementalRecordsAreTheOnesNamed()
    {
        var named = Committed
            .Where(r => DmgKeys(r.StatsText).Any(k => Census.Any(c => c.Key == k && !c.Admitted)))
            .Select(r => r.Name)
            .OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(ElementalRecords, named);
    }

    [Fact]
    public void KegMalletIsAWeaponWithDmgNineAndRatioPointThree()
    {
        var record = ItemCatalog.Default.Find("Keg Mallet");
        Assert.NotNull(record);
        Assert.Contains("Base Dmg: 9", record!.StatsText);

        // Through the parser, and through the catalog column every surface actually reads.
        foreach (var block in new[] { Parse(record), record.ToStatsBlock() })
        {
            Assert.Equal(9, block.Dmg);
            Assert.Equal(30, block.Delay);
            Assert.Equal(0.3, block.Ratio!.Value, 9);
        }
    }

    [Fact]
    public void KegMalletIsInTheWeaponHalfOfItemDominance()
    {
        var keg = ItemCatalog.Default.Find("Keg Mallet")!.ToStatsBlock();
        var none = ItemStatsBlock.Parse([]);

        var pairs = ItemDominance.MetricPairs(keg, none).ToDictionary(p => p.Metric, p => p.B);
        Assert.Equal(9, pairs["DMG"]);
        Assert.Equal(0.3, pairs["ratio"], 9);

        // A worn copy one point of damage short: the weapon half is what decides it.
        var worn = new ItemStatsBlock
        {
            Slots = keg.Slots, Dmg = 8, Delay = keg.Delay, Skill = keg.Skill,
            Attributes = new Dictionary<string, int>(keg.Attributes, StringComparer.OrdinalIgnoreCase),
        };
        Assert.Equal(DominanceVerdict.Yes,
            ItemDominance.Compare("Keg Mallet", keg, "A worn mallet", worn, []));
        Assert.Equal("DMG", ItemDominance.Gain(keg, worn)?.Metric);
    }

    /// <summary>The catalog is built through `ItemStatsBlock.Parse`, so its `Dmg` column must be
    /// what the parser reads off the record's own text — for every record. A parser change that
    /// ships without its catalog column (or the reverse) reddens here, by name.</summary>
    [Fact]
    public void EveryCatalogDmgIsWhatTheParserReadsOffItsOwnText()
    {
        var drift = Committed
            .Where(r => r.StatsText.Length > 0 && r.Dmg != Parse(r).Dmg)
            .Select(r => $"{r.Name}: catalog {r.Dmg?.ToString() ?? "null"}, parser {Parse(r).Dmg?.ToString() ?? "null"}")
            .ToList();
        Assert.Empty(drift);

        // 1,648 plain DMG + Keg Mallet.
        Assert.Equal(1_649, Committed.Count(r => r.Dmg is not null));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Every record in the COMMITTED file, all 11,196 of them. Not
    /// <see cref="ItemCatalog.All"/>: that folds 13 case-variant names into one key each (11,183
    /// records), two of them DMG weapons, and the census is a count of the file.</summary>
    private static readonly List<ItemCatalog.Record> Committed = LoadCommitted();

    private static List<ItemCatalog.Record> LoadCommitted()
    {
        using var stream = typeof(ItemCatalog).Assembly
            .GetManifestResourceStream("EQBuddy.Core.Data.ItemCatalog.json.gz")!;
        using var gz = new GZipStream(stream, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<ItemCatalog.Root>(gz)!.Items;
    }

    private static ItemStatsBlock Parse(ItemCatalog.Record r) =>
        ItemStatsBlock.Parse(r.StatsText.Split('\n'));

    /// <summary>The distinct `… Dmg:` keys in one block, spelled as <see cref="Census"/> spells
    /// them: plain `DMG`, or the one word before it in title case ("Base Dmg").</summary>
    private static HashSet<string> DmgKeys(string statsText)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in statsText.Split('\n'))
        {
            var at = 0;
            while ((at = line.IndexOf("dmg:", at, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var before = line[..at].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var word = before.Length > 0 ? before[^1] : "";
                keys.Add(word.Length > 0 && word.All(char.IsLetter)
                    ? char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant() + " Dmg"
                    : "DMG");
                at += 4;
            }
        }
        return keys;
    }

    /// <summary>The number after the plain `DMG:` key — the value the damage slot must hold.</summary>
    private static int? PlainDmg(string statsText)
    {
        foreach (var line in statsText.Split('\n'))
        {
            var at = 0;
            while ((at = line.IndexOf("dmg:", at, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var before = line[..at].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var plain = before.Length == 0 || !before[^1].All(char.IsLetter);
                at += 4;
                if (!plain) continue;
                var value = line[at..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (int.TryParse(value, out var n)) return n;
            }
        }
        return null;
    }
}
