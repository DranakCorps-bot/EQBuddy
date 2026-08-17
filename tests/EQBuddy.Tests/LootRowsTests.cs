using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The loot row builder shared by the Loot card and its breakout: the show filter
/// (all/looted/made), the sort modes, and — the part that used to live untested inside two
/// WPF windows — how "all" mixes looted and made into one ordered list.</summary>
public class LootRowsTests
{
    // Snapshot hands these back count-desc already, so mirror that here.
    private static readonly List<LootDetail> Loot =
    [
        new("Bone Chips", 5, "a decaying skeleton"),
        new("Vegetables", 3, "Forage"),
        new("HQ Lion Skin", 2, "a lion"),
    ];

    private static readonly List<NameCount> Crafted =
    [
        new("Fine Steel Dagger", 4),
        new("Crushbone Belt +5", 1),
    ];

    private static readonly List<LootPickup> Recent =
    [
        new(new DateTime(2026, 8, 16, 17, 3, 0), "Vegetables", 1, "Forage"),
        new(new DateTime(2026, 8, 16, 17, 2, 0), "HQ Lion Skin", 1, "a lion"),
        new(new DateTime(2026, 8, 16, 17, 1, 0), "Bone Chips", 2, "a decaying skeleton"),
    ];

    private static string[] Items(string view, string mode) =>
        LootRows.Build(Loot, Crafted, Recent, view, mode).Select(r => r.Item).ToArray();

    [Fact]
    public void LootedCountKeepsTheCountDescOrder_AndIncludesForage() =>
        Assert.Equal(new[] { "Bone Chips", "Vegetables", "HQ Lion Skin" }, Items("looted", "count"));

    [Fact]
    public void MadeShowsOnlyCrafts() =>
        Assert.Equal(new[] { "Fine Steel Dagger", "Crushbone Belt +5" }, Items("made", "count"));

    [Fact]
    public void AllCountMixesLootedAndMadeByCount()
    {
        // 5 Bone Chips, 4 Fine Steel Dagger, 3 Vegetables, 2 HQ Lion Skin, 1 Crushbone Belt.
        Assert.Equal(
            new[] { "Bone Chips", "Fine Steel Dagger", "Vegetables", "HQ Lion Skin", "Crushbone Belt +5" },
            Items("all", "count"));
    }

    [Fact]
    public void AllNameSortsTheWholeMixedListAlphabetically() =>
        Assert.Equal(
            new[] { "Bone Chips", "Crushbone Belt +5", "Fine Steel Dagger", "HQ Lion Skin", "Vegetables" },
            Items("all", "name"));

    [Fact]
    public void LootedRecentIsArrivalOrderNewestFirst() =>
        Assert.Equal(new[] { "Vegetables", "HQ Lion Skin", "Bone Chips" }, Items("looted", "recent"));

    [Fact]
    public void AllRecentIsRecentLootedThenMadeByCount() =>
        // Crafts carry no timestamp, so they can't interleave — recent looted first, made appended.
        Assert.Equal(
            new[] { "Vegetables", "HQ Lion Skin", "Bone Chips", "Fine Steel Dagger", "Crushbone Belt +5" },
            Items("all", "recent"));

    [Fact]
    public void CountRowsCarryTheStackValue() =>
        Assert.Equal("×5", LootRows.Build(Loot, Crafted, Recent, "looted", "count")[0].Value);
}
