using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Discussion #679 (joeymavity; duplicate #957, LoZZoL): loot from a dungeon crawl's Reward
/// Chest was never captured, because every loot regex required "'s corpse". Every line here
/// is VERBATIM - the reporter's (#679 and #957) and David's own log (eqlog_Dranak_freeport,
/// 2026-09-20), which is where the bagged, merged and sold shapes come from.
/// </summary>
public sealed class RewardChestLootTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    // ---- the parser, one row per shape ----

    [Theory]
    // #679, the post itself. The post quotes the line WITHOUT its stamp, so this row alone
    // carries a synthetic one; the message after it is verbatim.
    [InlineData("[Thu Sep 10 12:00:00 2026] You looted 5 Mote of Major Potential from Reward Chest and stored it in your currency",
        "Mote of Major Potential", 5, "currency")]
    // #679, the reporter's comment - stamps and all.
    [InlineData("[Thu Sep 10 15:44:58 2026] You looted 4 Mote of Major Potential from Reward Chest and stored it in your currency",
        "Mote of Major Potential", 4, "currency")]
    [InlineData("[Thu Sep 17 23:20:52 2026] You looted a Mote of Greater Potential from Reward Chest and stored it in your currency",
        "Mote of Greater Potential", 1, "currency")]
    [InlineData("[Thu Sep 17 23:20:52 2026] You looted 4 Mote of Major Potential from Reward Chest and stored it in your currency",
        "Mote of Major Potential", 4, "currency")]
    [InlineData("[Thu Sep 10 17:01:45 2026] You looted 12 Phosphorous Powder from Reward Chest and stored it in your tradeskill depot",
        "Phosphorous Powder", 12, "tradeskill depot")]
    [InlineData("[Thu Sep 10 17:01:46 2026] You looted an Undead Froglok Tongue from Reward Chest and stored it in your tradeskill depot",
        "Undead Froglok Tongue", 1, "tradeskill depot")]
    // #957.
    [InlineData("[Tue Sep 29 11:29:01 2026] You looted 3 Mote of Major Potential from Reward Chest and stored it in your currency",
        "Mote of Major Potential", 3, "currency")]
    public void AnAutoStoredChestLineIsLoot(string line, string item, int count, string storedIn)
    {
        var e = Assert.IsType<LootEvent>(LogParser.Parse(line));
        Assert.Equal(item, e.Item);
        Assert.Equal(count, e.Count);
        Assert.Equal(storedIn, e.StoredIn);
        Assert.Equal(LootSources.RewardChest, e.Source);
        Assert.Null(e.UpgradeResult);
    }

    [Fact]
    public void ABaggedChestLineIsLoot()
    {
        var e = Assert.IsType<LootEvent>(LogParser.Parse(
            "[Sun Sep 20 08:04:41 2026] --You have looted 2 Mote of Major Potential from Reward Chest.--"));
        Assert.Equal(("Mote of Major Potential", 2, LootSources.RewardChest), (e.Item, e.Count, e.Source));
        Assert.Null(e.StoredIn);
    }

    [Fact]
    public void AMergingChestLineIsAnUpgrade()
    {
        var e = Assert.IsType<LootEvent>(LogParser.Parse(
            "[Sun Sep 20 08:04:41 2026] You looted a Pristine Studded Leather Tunic +4 from Reward Chest to create a Pristine Studded Leather Tunic +9"));
        Assert.Equal("Pristine Studded Leather Tunic +4", e.Item);
        Assert.Equal("Pristine Studded Leather Tunic +9", e.UpgradeResult);
        Assert.Equal(LootSources.RewardChest, e.Source);
    }

    [Theory]
    [InlineData("[Thu Sep 10 17:01:45 2026] You looted a Bronze Knuckles +4 from Reward Chest and sold it for 2 gold.",
        "Bronze Knuckles +4", 200)]
    [InlineData("[Sun Sep 20 08:04:41 2026] You looted a Dagger of Marnek +4 from Reward Chest and sold it for 2 platinum, 1 gold, 4 silver and 3 copper.",
        "Dagger of Marnek +4", 2143)]
    public void AnAutoSoldChestLineIsASale(string line, string item, long copper)
    {
        var e = Assert.IsType<AutoSellEvent>(LogParser.Parse(line));
        Assert.Equal((item, 1, LootSources.RewardChest, copper), (e.Item, e.Count, e.Source, e.Copper));
    }

    /// <summary>The corpse arm is unchanged by the alternation - the source is still the
    /// creature, articles folded, first letter raised.</summary>
    [Fact]
    public void ACorpseLineStillNamesTheCreature()
    {
        var e = Assert.IsType<LootEvent>(LogParser.Parse(
            "[Thu Sep 17 23:20:52 2026] You looted a Mote of Major Potential from a spite golem's corpse and stored it in your currency"));
        Assert.Equal("Spite golem", e.Source);
        Assert.True(LootSources.IsCreature(e.Source));
    }

    /// <summary>The chest is a LITERAL, not "anything that is not a corpse": a source nobody
    /// has measured stays unparsed rather than guessed at.</summary>
    [Theory]
    [InlineData("[Thu Sep 10 17:01:45 2026] You looted 5 Mote of Major Potential from Treasure Chest and stored it in your currency")]
    [InlineData("[Thu Sep 10 17:01:45 2026] You looted 5 Mote of Major Potential from Reward Chests and stored it in your currency")]
    [InlineData("[Thu Sep 10 17:01:45 2026] --You have looted 2 Mote of Major Potential from a Reward Chest pile.--")]
    public void AnUnmeasuredSourceIsNotLoot(string line) =>
        Assert.Null(LogParser.Parse(line));

    // ---- the whole haul, through the session ----

    /// <summary>The reporter's full 17:01:45 haul, verbatim and in order. The motes reach the
    /// Motes count, the depot items reach session loot, the sale reaches vendor income - and
    /// the chest never becomes a creature in the drop ledger (<see cref="LootSources"/>).</summary>
    private static readonly string[] Haul =
    [
        "[Thu Sep 10 17:01:45 2026] You gain party experience! (3.489%)",
        "[Thu Sep 10 17:01:45 2026] You have completed the Dungeon Crawl and earned reward loot!",
        "[Thu Sep 10 17:01:45 2026] You receive 80 platinum, 5 silver and 1 copper from the corpse.",
        "[Thu Sep 10 17:01:45 2026] You gained reward experience from the Dungeon Crawl!",
        "[Thu Sep 10 17:01:45 2026] You have gained a level! Welcome to level 30!",
        "[Thu Sep 10 17:01:45 2026] You earned a refund of your instance charge.",
        "[Thu Sep 10 17:01:45 2026] You looted 12 Phosphorous Powder from Reward Chest and stored it in your tradeskill depot",
        "[Thu Sep 10 17:01:45 2026] You looted 10 Mote of Major Potential from Reward Chest and stored it in your currency",
        "[Thu Sep 10 17:01:45 2026] You looted 2 Mote of Greater Potential from Reward Chest and stored it in your currency",
        "[Thu Sep 10 17:01:45 2026] You looted a Bronze Knuckles +4 from Reward Chest and sold it for 2 gold.",
        "[Thu Sep 10 17:01:46 2026] You looted an Undead Froglok Tongue from Reward Chest and stored it in your tradeskill depot",
        "[Thu Sep 10 17:01:46 2026] You have completed achievement: Level 30",
        "[Thu Sep 10 17:01:46 2026] You looted an Amber from Reward Chest and stored it in your tradeskill depot",
        "[Thu Sep 10 17:01:47 2026] You looted an Evil Eye Eyestalk from Reward Chest and stored it in your tradeskill depot",
        "[Thu Sep 10 17:01:48 2026] You looted a Froglok Leg from Reward Chest and stored it in your tradeskill depot",
        "[Thu Sep 10 17:01:49 2026] You looted 2 Gargoyle Eye from Reward Chest and stored it in your tradeskill depot",
    ];

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true, Normalize = QuestCatalog.BaseItemName };

    private static SessionStats Replay(QuestLedgerStore? store, params string[] lines)
    {
        var stats = new SessionStats { CharacterName = "Hateborne", ServerName = "neriak", QuestStore = store };
        foreach (var line in lines)
            if (LogParser.Parse(line) is { } e) stats.Apply(e);
        return stats;
    }

    [Fact]
    public void TheHaulsMotesAreCounted()
    {
        var s = Replay(null, Haul).Snapshot();
        var motes = Motes.Summarize(s.Loot, s.Elapsed);
        Assert.Equal(12, motes.Total);
        Assert.Contains(s.Loot, l => l.Item == "Mote of Major Potential" && l.Count == 10);
        Assert.Contains(s.Loot, l => l.Item == "Mote of Greater Potential" && l.Count == 2);
    }

    [Fact]
    public void TheHaulsDepotItemsAreLootAndItsSaleIsIncome()
    {
        var s = Replay(null, Haul).Snapshot();
        Assert.Contains(s.Loot, l => l.Item == "Phosphorous Powder" && l.Count == 12);
        Assert.Contains(s.Loot, l => l.Item == "Gargoyle Eye" && l.Count == 2);
        Assert.Contains(s.Loot, l => l.Item == "Froglok Leg");
        // The auto-sold knuckles are vendor income and not loot - the corpse rule, unchanged.
        Assert.DoesNotContain(s.Loot, l => l.Item == "Bronze Knuckles +4");
        Assert.Equal(200, s.VendorCopper);
        Assert.Contains(s.SoldItems, x => x.Item == "Bronze Knuckles +4" && x.Copper == 200);
    }

    [Fact]
    public void TheChestIsNeverACreatureButTheCorpseStillIs()
    {
        var s = Replay(null, [
            .. Haul,
            "[Thu Sep 10 17:02:10 2026] You have slain a froglok!",
            "[Thu Sep 10 17:02:11 2026] --You have looted a Froglok Leg from a froglok's corpse.--",
        ]).Snapshot();
        Assert.DoesNotContain(s.Mobs, m => m.Name.Equals(LootSources.RewardChest, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(s.Mobs.Single(m => m.Name == "Froglok").Loot, l => l.Item == "Froglok Leg");
    }

    [Fact]
    public void TheQuestLedgerSeesChestLootAndKnowsWhereItWent()
    {
        var store = Store();
        Replay(store, Haul);
        var entries = store.For("hateborne_neriak");
        Assert.Equal(10, entries["Mote of Major Potential"].Looted);
        Assert.True(store.IsOffDump("hateborne_neriak", "Mote of Major Potential"));
        Assert.Equal(12, entries["Phosphorous Powder"].Looted);
    }
}
