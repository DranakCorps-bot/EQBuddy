using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Loot card's "recent" view (#160, wizen).
///
/// He was farming HQ Lion Skins and wanted to catch anything unusual that dropped along
/// the way. The aggregate view can't show that: a rare pelt appearing is one row whose
/// count goes 0 → 1, which looks exactly like the skin count going 200 → 201. In arrival
/// order it's the row that isn't a Lion Skin.
///
/// The collapsing rule is what makes it usable rather than a wall: a farm produces long
/// runs of one item, and if each got a line the interesting drop would be pushed off the
/// end by the very thing you're trying to see past.
/// </summary>
public class RawLootViewTests
{
    private static readonly DateTime T0 = new(2026, 8, 15, 14, 30, 0);

    private static LootPickup P(int secondsAgo, string item, int count = 1) =>
        new(T0.AddSeconds(-secondsAgo), item, count, "a lion");

    [Fact]
    public void ARunOfTheSameItemCollapsesIntoOneRowCarryingTheTotal()
    {
        var rows = RawLootView.Rows([
            P(0, "HQ Lion Skin"), P(10, "HQ Lion Skin"), P(20, "HQ Lion Skin"),
        ]);

        var row = Assert.Single(rows);
        Assert.Equal("HQ Lion Skin", row.Item);
        Assert.Contains("×3", row.Detail);
    }

    [Fact]
    public void TheUnusualDropSurvivesARunOnEitherSide()
    {
        // The whole point: this is the row wizen is farming to notice.
        var rows = RawLootView.Rows([
            P(0, "HQ Lion Skin"), P(5, "HQ Lion Skin"),
            P(9, "Ruined Lion Pelt"),
            P(15, "HQ Lion Skin"), P(20, "HQ Lion Skin"), P(25, "HQ Lion Skin"),
        ]);

        Assert.Equal(3, rows.Count);
        Assert.Equal("Ruined Lion Pelt", rows[1].Item);
    }

    [Fact]
    public void ARunKeepsTheNewestTime()
    {
        // The row's time is when the run most recently added to itself — anything older
        // would make a still-dropping item look stale.
        var rows = RawLootView.Rows([P(0, "Bone Chips"), P(600, "Bone Chips")]);

        Assert.Contains(T0.ToString("h:mm:ss tt"), Assert.Single(rows).Detail);
    }

    [Fact]
    public void SeparateRunsOfTheSameItemStaySeparate()
    {
        // Only CONSECUTIVE drops fold. Coming back to an item after something else is
        // history worth keeping — it says the camp is still producing both.
        var rows = RawLootView.Rows([
            P(0, "HQ Lion Skin"), P(5, "Ruined Lion Pelt"), P(10, "HQ Lion Skin"),
        ]);

        Assert.Equal(3, rows.Count);
        Assert.Equal(["HQ Lion Skin", "Ruined Lion Pelt", "HQ Lion Skin"],
            rows.Select(r => r.Item));
    }

    [Fact]
    public void ASingleDropShowsOnlyItsTime()
    {
        var row = Assert.Single(RawLootView.Rows([P(0, "Ruined Lion Pelt")]));

        Assert.DoesNotContain("×", row.Detail);
        Assert.Equal(T0.ToString("h:mm:ss tt"), row.Detail);
    }

    [Fact]
    public void AStackedDropKeepsItsStackSize()
    {
        var row = Assert.Single(RawLootView.Rows([P(0, "Bone Chips", count: 4)]));

        Assert.Contains("×4", row.Detail);
    }

    [Fact]
    public void NothingLootedIsNoRows() => Assert.Empty(RawLootView.Rows([]));

    // ---- the snapshot side: SessionStats must actually supply the rows ----

    /// <summary>
    /// Formatting rows is only half of it — the snapshot has to carry the drops in
    /// arrival order in the first place. Replays real loot lines through SessionStats
    /// and checks the newest is first, which is the ordering the whole view rests on.
    /// </summary>
    [Fact]
    public void TheSnapshotCarriesEveryDropNewestFirst()
    {
        var stats = new SessionStats();
        foreach (var line in new[]
                 {
                     "[Sat Aug 15 14:00:00 2026] --You have looted a HQ Lion Skin from a lion's corpse.--",
                     "[Sat Aug 15 14:00:30 2026] --You have looted a Ruined Lion Pelt from a lion's corpse.--",
                     "[Sat Aug 15 14:01:00 2026] --You have looted a HQ Lion Skin from a lion's corpse.--",
                 })
            if (LogParser.Parse(line) is { } evt) stats.Apply(evt);

        var recent = stats.Snapshot().RecentLoot;

        Assert.Equal(3, recent.Count);
        Assert.Equal("HQ Lion Skin", recent[0].Item);
        Assert.Equal("Ruined Lion Pelt", recent[1].Item);
        Assert.True(recent[0].Time > recent[1].Time, "newest must come first");
    }

    // ---- forage (#163, wizen) ----

    /// <summary>
    /// Foraged items never reached the loot card at all — the game announces them with
    /// "You have scrounged up X." rather than a corpse-loot line, and nothing parsed it.
    /// Routing it to a LootEvent means the loot card, the raw drop view and every
    /// quest/gear auto-checker pick it up for free; a forage that finishes a quest step
    /// should count exactly like one pulled off a corpse.
    /// </summary>
    [Fact]
    public void AForagedItemIsLootedWithForageAsItsSource()
    {
        var evt = Assert.IsType<LootEvent>(LogParser.Parse(
            "[Sat Aug 15 14:00:00 2026] You have scrounged up a Ration."));

        Assert.Equal("Ration", evt.Item);
        Assert.Equal("Forage", evt.Source);
        Assert.Equal(1, evt.Count);
    }

    [Fact]
    public void AForagedItemStartingWithAVowelParsesToo()
    {
        var evt = Assert.IsType<LootEvent>(LogParser.Parse(
            "[Sat Aug 15 14:00:00 2026] You have scrounged up an Apple."));

        Assert.Equal("Apple", evt.Item);
    }

    // The real game rarely uses an article — most foraged items are mass nouns or plurals
    // ("Vegetables", "Berries", "Pod of Water", "Roots"). Requiring "a"/"an" dropped every
    // one of them; these are lifted verbatim from LW's log (130 forages, all missed).
    [Theory]
    [InlineData("You have scrounged up Vegetables.", "Vegetables")]
    [InlineData("You have scrounged up Berries.", "Berries")]
    [InlineData("You have scrounged up Pod of Water.", "Pod of Water")]
    [InlineData("You have scrounged up Rabbit Meat.", "Rabbit Meat")]
    [InlineData("You have scrounged up Roots.", "Roots")]
    [InlineData("You have scrounged up Fishing Grubs.", "Fishing Grubs")]
    public void AForagedItemWithNoArticleParses(string line, string item)
    {
        var evt = Assert.IsType<LootEvent>(LogParser.Parse($"[Sun Aug 16 16:45:20 2026] {line}"));

        Assert.Equal(item, evt.Item);
        Assert.Equal("Forage", evt.Source);
        Assert.Equal(1, evt.Count);
    }

    [Fact]
    public void AFashionedItemIsAFashionEvent()
    {
        var evt = Assert.IsType<FashionEvent>(LogParser.Parse(
            "[Sat Aug 15 17:17:46 2026] You have fashioned the items together to create something new: Elixir of Greater Concentration."));
        Assert.Equal("Elixir of Greater Concentration", evt.Item);
    }

    [Fact]
    public void APickedUpParcelIsLootedWithParcelSource()
    {
        var evt = Assert.IsType<LootEvent>(LogParser.Parse(
            "[Sun Aug 16 14:47:14 2026] Laitia hands you the Short Sword of the Ykesha that was sent from Rodrigo."));
        Assert.Equal("Short Sword of the Ykesha", evt.Item);
        Assert.Equal("Parcel", evt.Source);
        Assert.Equal(1, evt.Count);
    }

    [Fact]
    public void AWaitingParcelNotificationDoesNotCount()
    {
        // Announces a parcel is waiting at the merchant — the item isn't in your bags yet,
        // so it must not reach the loot list until the pickup line above.
        Assert.Null(LogParser.Parse(
            "[Sun Aug 16 14:47:14 2026] You have received a new parcel delivery containing 1 Efreeti War Spear from Ivan!"));
    }

    [Fact]
    public void AFailedForageIsNotAnEvent()
    {
        // A miss is not an acquisition and must not reach the loot card.
        Assert.Null(LogParser.Parse(
            "[Sat Aug 15 14:00:00 2026] You fail to locate any food nearby."));
    }

    [Fact]
    public void ForagedItemsReachTheLootCardAndTheRawView()
    {
        var stats = new SessionStats();
        foreach (var line in new[]
                 {
                     "[Sat Aug 15 14:00:00 2026] You have scrounged up a Ration.",
                     "[Sat Aug 15 14:00:30 2026] --You have looted a HQ Lion Skin from a lion's corpse.--",
                 })
            if (LogParser.Parse(line) is { } evt) stats.Apply(evt);

        var snap = stats.Snapshot();
        Assert.Contains(snap.Loot, l => l.Item == "Ration");
        Assert.Contains(snap.RecentLoot, l => l.Item == "Ration" && l.Source == "Forage");
    }
}
