using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Hateborne, 2026-09-18: the Plane of Sky tab ticked High Quality Raiment (Berserker) and
/// Wind Rune Meda (Ranger, Shaman) with none of either held. Every wrong row was a * GUESS -
/// the loot auto-tick's rule 3 parking a tick on a class because none passed the lens - and
/// nothing ever took one back. These are his rows, from the shipped catalog.
/// </summary>
public class SkyGuessReconcileTests
{
    private static List<SkyQuestChecklistItem> Catalog() =>
        [.. SkyChecklistRows.Items.Select(i => i.Clone())];

    private static SkyQuestChecklistItem Row(List<SkyQuestChecklistItem> list, string id) =>
        list.Single(i => i.Id == id);

    private static void Guess(List<SkyQuestChecklistItem> list, params string[] ids)
    {
        foreach (var id in ids) { Row(list, id).Acquired = true; Row(list, id).AcquiredUnassigned = true; }
    }

    /// <summary>His profile on 2026-09-18, reduced to the rows that matter: the Raiment was
    /// looted once and handed to Wizard Schrock (Raiment of Thunder, turned in); the Meda
    /// guesses had walked across Cleric, Druid, Ranger and Shaman.</summary>
    private static (List<SkyQuestChecklistItem> List, List<string> Completed) HisRows()
    {
        var list = Catalog();
        Guess(list, "sky-034", "sky-042", "sky-056", "sky-149", "sky-164", "sky-163");
        Row(list, "sky-213").Acquired = true;   // Wizard's Raiment, consumed by the turn-in
        return (list, ["Wizard|Raiment of Thunder", "Cleric|Aegis of the Wind"]);
    }

    [Fact]
    public void NoneHeldTakesBackEveryGuessOnAnOpenReward()
    {
        var (list, completed) = HisRows();
        var held = new Dictionary<string, int> { ["Leather Cord"] = 1 };

        var cleared = SkyGuessReconcile.Apply(list, completed,
            item => held.TryGetValue(item, out var n) ? n : 0);

        Assert.Equal(["sky-034", "sky-056", "sky-149", "sky-164"], cleared.Select(r => r.Id).Order());
        Assert.All(cleared, r => Assert.False(r.Acquired || r.AcquiredUnassigned));
        // The Leather Cord in the bank still covers Shaman's guess.
        Assert.True(Row(list, "sky-163").Acquired);
        // A turned-in reward's rows are the hand-in's, not a guess to take back -
        // Cleric's Aegis is completed, so its Meda tick stays.
        Assert.True(Row(list, "sky-042").Acquired);
        Assert.True(Row(list, "sky-213").Acquired);
    }

    [Fact]
    public void OneHeldKeepsTheFirstListedGuessAndTakesTheRest()
    {
        var list = Catalog();
        Guess(list, "sky-056", "sky-149", "sky-164");   // Druid, Ranger, Shaman Meda

        var cleared = SkyGuessReconcile.Apply(list, [], _ => 1);

        Assert.True(Row(list, "sky-056").Acquired);
        Assert.Equal(["sky-149", "sky-164"], cleared.Select(r => r.Id));
    }

    [Fact]
    public void ATickThePlayerMadeIsNeverTakenBackAndClaimsItsCopyFirst()
    {
        var list = Catalog();
        Row(list, "sky-149").Acquired = true;   // the player's own tick: no star
        Guess(list, "sky-164");

        var cleared = SkyGuessReconcile.Apply(list, [], _ => 1);

        Assert.True(Row(list, "sky-149").Acquired);
        Assert.Equal("sky-164", Assert.Single(cleared).Id);

        // And with nothing held at all, the player's own tick STILL stands.
        SkyGuessReconcile.Apply(list, [], _ => 0);
        Assert.True(Row(list, "sky-149").Acquired);
    }

    [Fact]
    public void AnUnknownCountLeavesGuessesAlone()
    {
        var list = Catalog();
        Guess(list, "sky-149");

        Assert.Empty(SkyGuessReconcile.Apply(list, [], _ => null));
        Assert.True(Row(list, "sky-149").Acquired);
    }

    [Fact]
    public void OnlyItemsLimitsThePassToWhatLeft()
    {
        var list = Catalog();
        Guess(list, "sky-149", "sky-034");   // Meda and Raiment guesses

        var cleared = SkyGuessReconcile.Apply(list, [], _ => 0, onlyItems: ["Wind Rune Meda"]);

        Assert.Equal("sky-149", Assert.Single(cleared).Id);
        Assert.True(Row(list, "sky-034").Acquired);
    }

    /// <summary>A rune's recorded count cannot be trusted - scans made before EQBuddy knew
    /// runes live in currency recorded every one as zero - so a hand-in takes back exactly
    /// one guess per rune handed over, and never reconciles the rest to that zero.</summary>
    [Fact]
    public void AHandInTakesBackOneGuessPerCopyNotEveryGuess()
    {
        var list = Catalog();
        Guess(list, "sky-008", "sky-044", "sky-071");   // Wind Rune Caza: Bard, Cleric, Enchanter

        var taken = SkyGuessReconcile.TakeBack(list, [], "Wind Rune Caza", 1);

        Assert.Equal("sky-071", Assert.Single(taken).Id);   // last-listed first
        Assert.True(Row(list, "sky-008").Acquired && Row(list, "sky-044").Acquired);
    }

    [Fact]
    public void UndoPutsBackExactlyTheGuessesStillMarkedAsGuesses()
    {
        var (list, completed) = HisRows();
        var ids = SkyGuessReconcile.Apply(list, completed, _ => 0).Select(r => r.Id).ToList();

        SkyGuessReconcile.Undo(list, ids);

        Assert.All(ids, id => Assert.True(Row(list, id).Acquired && Row(list, id).AcquiredUnassigned));
    }

    // ---- the cascade itself, through the real feed ----

    private static AppSettings Settings() => new()
    {
        SkyQuestChecklist = Catalog(),
        EpicQuestChecklist = [],
    };

    /// <summary>
    /// **The bug, as it happened.** With no class picked, one looted Wind Rune Meda parks one
    /// * tick. The widget used to diff session loot against a RAM high-water mark that every
    /// launch cleared while the log was re-read from the top - so each restart re-applied the
    /// same line and parked the next * on the next class. Keyed on the ledger's persisted
    /// gate, the relaunch feeds nothing and ticks nothing.
    /// </summary>
    [Fact]
    public void ARelaunchReplayingTheSameLootTicksNothingNew()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");
        try
        {
            var store = new QuestLedgerStore(path) { TrackFilter = _ => true, Normalize = QuestCatalog.BaseItemName };
            var settings = Settings();
            const string loot = "[Tue Aug 25 11:26:45 2026] --You have looted a Wind Rune Meda from Sister of the Spire's corpse.--";

            for (var launch = 0; launch < 4; launch++)
            {
                var stats = new SessionStats { CharacterName = "Hateborne", ServerName = "neriak", QuestStore = store };
                stats.Apply(LogParser.Parse(loot)!);
                ChecklistLedgerSync.Apply(settings, stats.QuestFeed.Drain(), [], _ => null, CurrencyItems.IsKnown);
            }

            Assert.Single(settings.SkyQuestChecklist, i => i.QuestItem == "Wind Rune Meda" && i.Acquired);
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(path + ".rules"); } catch { }
        }
    }

    [Fact]
    public void AHandInThroughTheFeedTakesTheGuessBack()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");
        try
        {
            var store = new QuestLedgerStore(path) { TrackFilter = _ => true, Normalize = QuestCatalog.BaseItemName };
            var settings = Settings();
            var stats = new SessionStats { CharacterName = "Hateborne", ServerName = "neriak", QuestStore = store };
            int? Held(string item) => store.For("hateborne_neriak").TryGetValue(item, out var e) ? e.Total : null;

            stats.Apply(LogParser.Parse("[Thu Sep 17 08:07:40 2026] You looted a Wind Rune Meda from Protector of Sky's corpse and stored it in your currency")!);
            ChecklistLedgerSync.Apply(settings, stats.QuestFeed.Drain(), [], Held, CurrencyItems.IsKnown);
            var guessed = settings.SkyQuestChecklist.Single(i => i.QuestItem == "Wind Rune Meda" && i.Acquired);
            Assert.True(guessed.AcquiredUnassigned);

            foreach (var line in new[]
            {
                "[Fri Sep 18 11:16:44 2026] You offered 1 Wind Rune Meda to Cilin Spellsinger.",
                "[Fri Sep 18 11:16:45 2026] You complete the trade with Cilin Spellsinger.",
                "[Fri Sep 18 11:17:25 2026] You gain experience! (1.312%)",
            })
                stats.Apply(LogParser.Parse(line)!);
            Assert.True(ChecklistLedgerSync.Apply(settings, stats.QuestFeed.Drain(), [], Held, CurrencyItems.IsKnown));

            Assert.DoesNotContain(settings.SkyQuestChecklist, i => i.QuestItem == "Wind Rune Meda" && i.Acquired);
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(path + ".rules"); } catch { }
        }
    }

    /// <summary>The ledger admits what a checklist can tick - or the ledger-keyed auto-tick
    /// could never tick an item the wiki catalog does not list as a turn-in.</summary>
    [Fact]
    public void EveryChecklistItemIsAnAutoTickName()
    {
        var settings = Settings();
        var names = QuestChecklistLayout.AutoTickItemNames(settings);

        Assert.All(SkyChecklistRows.Items, i => Assert.Contains(QuestCatalog.BaseItemName(i.QuestItem), names));
        Assert.Contains("Wind Rune Meda", names);
        Assert.DoesNotContain("Raiment of Thunder", names);   // a reward, never an ingredient
    }
}
