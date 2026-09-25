using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// EQL DOES log hand-ins (Hateborne, 2026-09-18). The Sky tab believed it did not, so a Wind
/// Rune Meda handed to Cilin Spellsinger stayed ticked on every class that wanted one. The
/// line shapes here are verbatim from his log (2026-09-03 and 2026-09-18).
/// </summary>
public sealed class HandInTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private const string Key = "hateborne_neriak";

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true, Normalize = QuestCatalog.BaseItemName };

    private static SessionStats Stats(QuestLedgerStore store) => new()
    {
        CharacterName = "Hateborne", ServerName = "neriak", QuestStore = store,
    };

    private static void Feed(SessionStats stats, params string[] lines)
    {
        foreach (var line in lines)
            if (LogParser.Parse(line) is { } e) stats.Apply(e);
    }

    // ---- the parser ----

    [Fact]
    public void TheThreeTradeLinesParse()
    {
        var offer = Assert.IsType<TradeOfferEvent>(LogParser.Parse(
            "[Fri Sep 18 11:16:44 2026] You offered 1 Wind Rune Meda to Cilin Spellsinger."));
        Assert.Equal(("Wind Rune Meda", 1, "Cilin Spellsinger"), (offer.Item, offer.Count, offer.Target));

        var done = Assert.IsType<TradeCompleteEvent>(LogParser.Parse(
            "[Fri Sep 18 11:16:45 2026] You complete the trade with Cilin Spellsinger."));
        Assert.Equal("Cilin Spellsinger", done.Target);

        var refused = Assert.IsType<TradeRefusedEvent>(LogParser.Parse(
            "[Thu Sep 03 21:06:33 2026] Wizard Schrock says, 'I have no need for this, Hateborne. You can have it back.'"));
        Assert.Equal("Wizard Schrock", refused.Npc);
    }

    [Fact]
    public void AnUpgradedItemAndAnItemNamedWithToBothSplitAtTheLastTo()
    {
        var tiered = Assert.IsType<TradeOfferEvent>(LogParser.Parse(
            "[Thu Sep 03 20:57:39 2026] You offered 1 High Quality Raiment +1 to Wizard Schrock."));
        Assert.Equal("High Quality Raiment +1", tiered.Item);

        var named = Assert.IsType<TradeOfferEvent>(LogParser.Parse(
            "[Thu Sep 03 20:57:39 2026] You offered 2 Key to the Vault to Wizard Schrock."));
        Assert.Equal(("Key to the Vault", 2, "Wizard Schrock"), (named.Item, named.Count, named.Target));
    }

    [Fact]
    public void ACurrencyLootLineSaysWhereItWent()
    {
        var loot = Assert.IsType<LootEvent>(LogParser.Parse(
            "[Thu Sep 17 08:07:40 2026] You looted a Wind Rune Meda from Protector of Sky's corpse and stored it in your currency"));
        Assert.Equal("Wind Rune Meda", loot.Item);
        Assert.Equal("currency", loot.StoredIn);

        var bagged = Assert.IsType<LootEvent>(LogParser.Parse(
            "[Thu Sep 03 08:31:20 2026] --You have looted a High Quality Raiment +1 from The Spiroc Lord's corpse.--"));
        Assert.Null(bagged.StoredIn);
    }

    // ---- the tracker ----

    private static readonly DateTime T = new(2026, 9, 18, 11, 16, 40);

    [Fact]
    public void ACompletedTradeBecomesFinalOnlyAfterTheRefusalWindow()
    {
        var t = new HandInTracker();
        Assert.Null(t.Observe(new TradeOfferEvent(T, "Light Woolen Mask", 1, "Cilin Spellsinger")));
        Assert.Null(t.Observe(new TradeOfferEvent(T.AddSeconds(4), "Wind Rune Meda", 1, "Cilin Spellsinger")));
        Assert.Null(t.Observe(new TradeCompleteEvent(T.AddSeconds(5), "Cilin Spellsinger")));
        // "You gain experience!" one second later is inside the window: still pending.
        Assert.Null(t.Observe(new XpEvent(T.AddSeconds(6), 1.3, Party: false)));

        var final = t.Observe(new XpEvent(T.AddSeconds(30), 1.3, Party: false));

        Assert.NotNull(final);
        Assert.Equal("Cilin Spellsinger", final!.Target);
        Assert.Equal(T.AddSeconds(5), final.At);
        Assert.Equal(["Light Woolen Mask", "Wind Rune Meda"], final.Items.Select(i => i.Item).Order());
    }

    [Fact]
    public void ARefusalFromTheSameNpcCancelsTheWholeTrade()
    {
        var t = new HandInTracker();
        t.Observe(new TradeOfferEvent(T, "Wind Rune Fana", 1, "Wizard Schrock"));
        t.Observe(new TradeCompleteEvent(T.AddSeconds(1), "Wizard Schrock"));

        Assert.Null(t.Observe(new TradeRefusedEvent(T.AddSeconds(2), "Wizard Schrock")));
        Assert.Null(t.Observe(new XpEvent(T.AddMinutes(5), 1, Party: false)));
    }

    [Fact]
    public void ARefusalFromSomeoneElseDoesNotCancel()
    {
        var t = new HandInTracker();
        t.Observe(new TradeOfferEvent(T, "Wind Rune Fana", 1, "Wizard Schrock"));
        t.Observe(new TradeCompleteEvent(T.AddSeconds(1), "Wizard Schrock"));
        t.Observe(new TradeRefusedEvent(T.AddSeconds(2), "Enchanter Jolas"));

        Assert.NotNull(t.Observe(new XpEvent(T.AddMinutes(5), 1, Party: false)));
    }

    [Fact]
    public void OffersWithNoCompletionAreACancelledTradeAndConsumeNothing()
    {
        var t = new HandInTracker();
        t.Observe(new TradeOfferEvent(T, "Wind Rune Meda", 1, "Cilin Spellsinger"));
        Assert.Null(t.Observe(new XpEvent(T.AddMinutes(5), 1, Party: false)));
        // ...and a completion minutes later with no fresh offer hands in nothing.
        t.Observe(new TradeCompleteEvent(T.AddMinutes(5), "Cilin Spellsinger"));
        Assert.Null(t.Observe(new XpEvent(T.AddMinutes(10), 1, Party: false)));
    }

    [Fact]
    public void TwoTradesInsideOneWindowBothStand()
    {
        var t = new HandInTracker();
        t.Observe(new TradeOfferEvent(T, "Glowing Necklace", 1, "Enchanter Jolas"));
        t.Observe(new TradeCompleteEvent(T.AddSeconds(1), "Enchanter Jolas"));
        t.Observe(new TradeOfferEvent(T.AddSeconds(2), "Finely Woven Cloth Cord", 1, "Enchanter Jolas"));
        var first = t.Observe(new TradeCompleteEvent(T.AddSeconds(3), "Enchanter Jolas"));
        var second = t.Observe(new XpEvent(T.AddMinutes(1), 1, Party: false));

        Assert.Equal("Glowing Necklace", Assert.Single(first!.Items).Item);
        Assert.Equal("Finely Woven Cloth Cord", Assert.Single(second!.Items).Item);
    }

    // ---- through SessionStats into the ledger ----

    private static readonly string[] MedaHandIn =
    [
        "[Thu Sep 17 08:07:40 2026] You looted a Wind Rune Meda from Protector of Sky's corpse and stored it in your currency",
        "[Fri Sep 18 11:16:40 2026] You offered 1 Light Woolen Mask to Cilin Spellsinger.",
        "[Fri Sep 18 11:16:44 2026] You offered 1 Wind Rune Meda to Cilin Spellsinger.",
        "[Fri Sep 18 11:16:45 2026] You gain experience! (1.312%)",
        "[Fri Sep 18 11:16:45 2026] You complete the trade with Cilin Spellsinger.",
        "[Fri Sep 18 11:17:25 2026] You gain experience! (1.312%)",
    ];

    [Fact]
    public void AHandInTakesTheItemOutOfTheLedgerAndTellsTheFeed()
    {
        var store = Store();
        var stats = Stats(store);

        Feed(stats, MedaHandIn);

        Assert.Equal(0, store.For(Key)["Wind Rune Meda"].Total);
        // The first offer arrived after a day-long gap, which rolls the SESSION over - the
        // trade must survive that, or the first item of every first trade of the day is lost.
        Assert.Equal(1, store.For(Key)["Light Woolen Mask"].Consumed);
        var delta = stats.QuestFeed.Drain();
        Assert.Equal(("Wind Rune Meda", 1), Assert.Single(delta.Gained));
        Assert.Contains(("Wind Rune Meda", 1), delta.Lost);
        Assert.True(stats.QuestFeed.Drain().IsEmpty);   // drained means drained
    }

    /// <summary>The launch replay re-reads the whole log. The ledger's time gate must bounce
    /// the loot AND the hand-in, and the feed must stay quiet - that silence is what stops
    /// the Sky auto-tick parking another * on the next class's row every restart.</summary>
    [Fact]
    public void AReplayOfTheSameLogChangesNothingAndFeedsNothing()
    {
        var store = Store();
        Feed(Stats(store), MedaHandIn);

        var relaunch = Stats(store);
        Feed(relaunch, MedaHandIn);

        Assert.Equal(0, store.For(Key)["Wind Rune Meda"].Total);
        Assert.True(relaunch.QuestFeed.Drain().IsEmpty);
    }

    [Fact]
    public void ARefusedTradeLeavesTheCountAlone()
    {
        var store = Store();
        var stats = Stats(store);

        Feed(stats,
            "[Thu Sep 03 21:05:00 2026] --You have looted a Wind Rune Fana from a spiroc guardian's corpse.--",
            "[Thu Sep 03 21:06:30 2026] You offered 1 Wind Rune Fana to Wizard Schrock.",
            "[Thu Sep 03 21:06:32 2026] You complete the trade with Wizard Schrock.",
            "[Thu Sep 03 21:06:33 2026] Wizard Schrock says, 'I have no need for this, Hateborne. You can have it back.'",
            "[Thu Sep 03 21:07:00 2026] You gain experience! (1.312%)");

        Assert.Equal(1, store.For(Key)["Wind Rune Fana"].Total);
        Assert.Empty(stats.QuestFeed.Drain().Lost);
    }

    [Fact]
    public void ReviewReplayRecordsNoHandIn()
    {
        var store = Store();
        store.RecordLoot(Key, "Wind Rune Meda", 1, new DateTime(2026, 9, 17, 8, 7, 40));
        var stats = Stats(store);
        stats.StoresSuppressed = true;

        Feed(stats, MedaHandIn[1..]);

        Assert.Equal(1, store.For(Key)["Wind Rune Meda"].Total);
    }

    // ---- currency items and the dump ----

    [Fact]
    public void ADumpDoesNotZeroAnItemTheLogSawGoToCurrency()
    {
        var store = Store();
        Assert.True(store.RecordLoot(Key, "Wind Rune Ena", 3, new DateTime(2026, 9, 17, 8, 0, 0), offDump: true));
        store.RecordLoot(Key, "Griffon Talon", 1, new DateTime(2026, 9, 17, 8, 1, 0));

        store.ReconcileInventory(Key, new Dictionary<string, int> { ["Leather Cord"] = 1 },
            new DateTime(2026, 9, 18, 11, 45, 38));

        var items = store.For(Key);
        Assert.Equal(3, items["Wind Rune Ena"].Total);   // currency: the dump cannot see it
        Assert.Equal(0, items["Griffon Talon"].Total);   // bags: absent means none
        Assert.Equal(1, items["Leather Cord"].Total);
    }

    /// <summary>A Wind Rune is a currency item before the log has said so this run - every
    /// scan since 2026-09-16 recorded the player's runes as zero, so the rule cannot wait to
    /// be taught by a loot line the current log may not hold.</summary>
    [Fact]
    public void AKnownCurrencyItemIsSparedBeforeTheLogTeachesIt()
    {
        var store = Store();
        store.RecordLoot(Key, "Wind Rune Dena", 3, new DateTime(2026, 9, 1, 8, 0, 0));   // a bag loot line

        store.ReconcileInventory(Key, new Dictionary<string, int> { ["Leather Cord"] = 1 },
            new DateTime(2026, 9, 18, 11, 45, 38));

        Assert.Equal(3, store.For(Key)["Wind Rune Dena"].Total);
        Assert.True(store.IsOffDump(Key, "Wind Rune Dena"));
        Assert.False(store.IsOffDump(Key, "Leather Cord"));
    }

    [Fact]
    public void AReplayedCurrencyLineStillTeachesOffDump()
    {
        var store = Store();
        var at = new DateTime(2026, 9, 17, 8, 0, 0);
        store.RecordLoot(Key, "Wind Rune Ena", 1, at);

        Assert.False(store.RecordLoot(Key, "Wind Rune Ena", 1, at, offDump: true));   // bounced...
        Assert.True(store.For(Key)["Wind Rune Ena"].OffDump);                           // ...but learned
    }
}
