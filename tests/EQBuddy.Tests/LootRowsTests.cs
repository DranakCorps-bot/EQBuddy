using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The loot row builder shared by the Loot card and its breakout: the show filter
/// (all / looted / other), the sort modes, the provenance tags, and how "all" mixes the
/// slices into one ordered list. Auto-sold pickups never appear in any of it — the
/// snapshot excludes them before these rows are built (see SessionStatsTests).</summary>
public class LootRowsTests
{
    // Snapshot hands loot back count-desc; sources: a mob = corpse (untagged), "Forage",
    // "Parcel". Crafted (merges) and Fashioned (crafts) come as their own lists.
    private static readonly List<LootDetail> Loot =
    [
        new("Bone Chips", 5, "a decaying skeleton"),
        new("Vegetables", 3, "Forage"),
        new("HQ Lion Skin", 2, "a lion"),
        new("Short Sword of the Ykesha", 1, "Parcel"),
    ];

    private static readonly List<NameCount> Merged = [new("Crushbone Belt +5", 1)];   // s.Crafted
    private static readonly List<NameCount> Fashioned = [new("Elixir of Concentration", 4)];

    // Recent is every acquisition in arrival order (newest first), crafts/merges included
    // — they ride the timeline with source "Fashion"/"Merge".
    private static readonly List<LootPickup> Recent =
    [
        new(new DateTime(2026, 8, 16, 17, 4, 0), "Elixir of Concentration", 1, "Fashion"),
        new(new DateTime(2026, 8, 16, 17, 3, 0), "Short Sword of the Ykesha", 1, "Parcel"),
        new(new DateTime(2026, 8, 16, 17, 2, 0), "Vegetables", 1, "Forage"),
        new(new DateTime(2026, 8, 16, 17, 1, 0), "Bone Chips", 2, "a decaying skeleton"),
        new(new DateTime(2026, 8, 16, 17, 0, 0), "Crushbone Belt +5", 1, "Merge"),
    ];

    private static List<LootRow> Build(string view, string mode) =>
        LootRows.Build(Loot, Merged, Fashioned, Recent, view, mode);

    private static string[] Items(string view, string mode) =>
        Build(view, mode).Select(r => r.Item).ToArray();

    [Fact]
    public void LootedIsCorpseDropsOnly_NoForageOrParcelOrMade() =>
        Assert.Equal(new[] { "Bone Chips", "HQ Lion Skin" }, Items("looted", "count"));

    [Fact]
    public void OtherIsForageParcelCraftedMerged()
    {
        // 5? no — Elixir ×4, Vegetables ×3, Belt+5 ×1, Short Sword ×1 (ties alphabetical).
        Assert.Equal(
            new[] { "Elixir of Concentration", "Vegetables", "Crushbone Belt +5", "Short Sword of the Ykesha" },
            Items("other", "count"));
    }

    [Fact]
    public void AllMixesEveryThingByCount() =>
        Assert.Equal(
            new[] { "Bone Chips", "Elixir of Concentration", "Vegetables", "HQ Lion Skin",
                    "Crushbone Belt +5", "Short Sword of the Ykesha" },
            Items("all", "count"));

    [Theory]
    [InlineData("Vegetables", "Foraged")]
    [InlineData("Short Sword of the Ykesha", "Parcel")]
    [InlineData("Crushbone Belt +5", "Merged")]
    [InlineData("Elixir of Concentration", "Crafted")]
    [InlineData("Bone Chips", null)]
    public void EachItemCarriesItsProvenanceTag(string item, string? tag) =>
        Assert.Equal(tag, Build("all", "count").First(r => r.Item == item).Tag);

    [Fact]
    public void LootedRecentIsArrivalOrder_TaggedBySource()
    {
        var rows = Build("looted", "recent");
        Assert.Equal(new[] { "Bone Chips" }, rows.Select(r => r.Item).ToArray());   // only corpse has arrival here
        Assert.Null(rows[0].Tag);
    }

    [Fact]
    public void OtherRecentIsEveryNonCorpseAcquisitionInArrivalOrder()
    {
        // Newest first: Elixir (craft), Short Sword (parcel), Vegetables (forage), Belt (merge).
        var rows = Build("other", "recent");
        Assert.Equal(
            new[] { "Elixir of Concentration", "Short Sword of the Ykesha", "Vegetables", "Crushbone Belt +5" },
            rows.Select(r => r.Item).ToArray());
        Assert.Equal("Crafted", rows[0].Tag);   // the crafted elixir shows in recent, tagged
    }

    [Fact]
    public void AllRecentInterleavesEverythingByTime() =>
        Assert.Equal(
            new[] { "Elixir of Concentration", "Short Sword of the Ykesha", "Vegetables",
                    "Bone Chips", "Crushbone Belt +5" },
            Items("all", "recent"));

    [Fact]
    public void RecentKeepsOneRowPerAcquisition_NoRunCollapse()
    {
        // Three separate Bone Chips loots stay three timestamped rows (aggregating is the
        // count view's job) — the raw timeline, not RawLootView's run-collapse.
        var picks = new List<LootPickup>
        {
            new(new DateTime(2026, 8, 16, 17, 3, 0), "Bone Chips", 1, "a skeleton"),
            new(new DateTime(2026, 8, 16, 17, 2, 0), "Bone Chips", 1, "a skeleton"),
            new(new DateTime(2026, 8, 16, 17, 1, 0), "Bone Chips", 1, "a skeleton"),
        };
        var rows = LootRows.Build([], [], [], picks, "all", "recent");
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal("Bone Chips", r.Item));
    }

    [Fact]
    public void CountTiesBreakAlphabetically()
    {
        var loot = new List<LootDetail> { new("Zebra Hide", 1, "x"), new("apple", 1, "x"), new("Mango", 1, "x") };
        Assert.Equal(
            new[] { "apple", "Mango", "Zebra Hide" },
            LootRows.Build(loot, [], [], [], "looted", "count").Select(r => r.Item).ToArray());
    }
}
