using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The inventory dump proving a Sky reward already turned in (Hateborne, 2026-09-03:
/// Ivory Mask in the bank, and the Sky tab still listing it as ready to turn in).
///
/// The checklist tracks INGREDIENTS, so until this pass existed the finished reward item
/// was evidence nothing consulted — the log never records a hand-in, and the #101 guard
/// rightly refuses an auto-granted achievement's word for it. Ownership of the reward is
/// the one witness that cannot lie: the game's own unlock criterion is "Obtain X", and
/// the item existing in bags or bank IS the obtain, however long before EQBuddy it
/// happened.
/// </summary>
public class SkyRewardAutoCompleteTests
{
    private static string HateborneInventory => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "fixtures", "inventory", "hateborne.txt"));

    private static AppSettings WithIvoryMask()
    {
        var s = new AppSettings();
        s.SkyQuestChecklist.AddRange(
        [
            new SkyQuestChecklistItem
            {
                Id = "a", ClassName = "Enchanter", Reward = "Ivory Mask",
                Npc = "Enchanter Jolas", QuestItem = "Silken Mask",
            },
            new SkyQuestChecklistItem
            {
                Id = "b", ClassName = "Enchanter", Reward = "Ivory Mask",
                Npc = "Enchanter Jolas", QuestItem = "Wind Rune Beza", AcquiredUnassigned = true,
            },
            // A different reward the dump does not hold, to prove the pass is surgical.
            new SkyQuestChecklistItem
            {
                Id = "c", ClassName = "Enchanter", Reward = "Sphinx Hair Cord",
                Npc = "Enchanter Jolas", QuestItem = "Golden Silk Ribbon",
            },
        ]);
        return s;
    }

    private static string Key => QuestChecklistLayout.RewardKey("Enchanter", "Ivory Mask");

    /// <summary>Counts and entries built through the REAL parser, so the "+N" fold and
    /// the (Exaltation) non-fold below are the shipped rules, not a test's re-statement
    /// of them (trap 23: seed through the same parser the app uses).</summary>
    private static InventoryFile.Snapshot Dump(params (string Loc, string Name)[] rows)
    {
        var lines = new[] { "Location\tName\tID\tCount\tSlots" }
            .Concat(rows.Select(r => $"{r.Loc}\t{r.Name}\t0\t1\t10")).ToList();
        return new InventoryFile.Snapshot("Hateborne_neriak-Inventory.txt",
            new DateTime(2026, 9, 3, 12, 0, 0), InventoryFile.Parse(lines))
        { Entries = InventoryFile.ParseEntries(lines) };
    }

    [Fact]
    public void OwningTheFinishedRewardMarksItTurnedIn()
    {
        var s = WithIvoryMask();
        var matches = SkyRewardAutoComplete.FindTurnedIn(
            Dump(("Bank15-Slot3", "Ivory Mask")), s.SkyQuestChecklist, s.SkyQuestCompleted);

        Assert.Equal([new SkyRewardAutoComplete.Match("Enchanter", "Ivory Mask")], matches);
        Assert.Equal(1, SkyRewardAutoComplete.Apply(matches, s));
        Assert.Contains(Key, s.SkyQuestCompleted);
        Assert.All(s.SkyQuestChecklist.Where(i => i.Reward == "Ivory Mask"),
            i => { Assert.True(i.Acquired); Assert.False(i.AcquiredUnassigned); });
    }

    /// <summary>Where it sits proves nothing either way — a turned-in reward is a
    /// keepsake, and bags and bank both count.</summary>
    [Fact]
    public void BagsAndBankBothCountAsOwnership()
    {
        var s = WithIvoryMask();
        Assert.Single(SkyRewardAutoComplete.FindTurnedIn(
            Dump(("General 3-Slot2", "Ivory Mask")), s.SkyQuestChecklist, s.SkyQuestCompleted));
    }

    /// <summary>An upgraded copy folds to the base name every quest page uses —
    /// the same rule the turn-in counter has always applied.</summary>
    [Fact]
    public void APlusTierCopyStillProvesOwnership()
    {
        var s = WithIvoryMask();
        Assert.Single(SkyRewardAutoComplete.FindTurnedIn(
            Dump(("Bank15-Slot3", "Ivory Mask +1")), s.SkyQuestChecklist, s.SkyQuestCompleted));
    }

    /// <summary>BaseItemName never strips "(Exaltation)", so an aug-slot copy is a
    /// structural non-match — this pins the deliberate non-stripping (trap 39: every
    /// equality deserves one negative).</summary>
    [Fact]
    public void AnExaltationCopyDoesNotMatch()
    {
        var s = WithIvoryMask();
        Assert.Empty(SkyRewardAutoComplete.FindTurnedIn(
            Dump(("Face-Slot7", "Ivory Mask (Exaltation)")), s.SkyQuestChecklist, s.SkyQuestCompleted));
    }

    /// <summary>Holding an INGREDIENT is what "ready to turn in" already means — it must
    /// never read as the turn-in itself.</summary>
    [Fact]
    public void AnIngredientNameNeverMatchesAsAReward()
    {
        var s = WithIvoryMask();
        Assert.Empty(SkyRewardAutoComplete.FindTurnedIn(
            Dump(("General 1-Slot1", "Silken Mask"), ("General 1-Slot2", "Wind Rune Beza")),
            s.SkyQuestChecklist, s.SkyQuestCompleted));
    }

    /// <summary>Ownership is decisive REGARDLESS of ingredient state: a player who turned
    /// in long before EQBuddy existed has an untouched checklist and the reward in the
    /// bank, which is exactly the case the log can never see.</summary>
    [Fact]
    public void AnUntouchedChecklistDoesNotWeakenTheEvidence()
    {
        var s = WithIvoryMask();
        Assert.All(s.SkyQuestChecklist, i => Assert.False(i.Acquired));
        var matches = SkyRewardAutoComplete.FindTurnedIn(
            Dump(("Bank15-Slot3", "Ivory Mask")), s.SkyQuestChecklist, s.SkyQuestCompleted);
        Assert.Single(matches);
    }

    /// <summary>Already-completed keys are excluded in FindTurnedIn itself, so every
    /// match IS newly markable and a report can name the list verbatim.</summary>
    [Fact]
    public void AnAlreadyCompletedRewardIsNotFoundAgain()
    {
        var s = WithIvoryMask();
        s.SkyQuestCompleted.Add(Key);
        Assert.Empty(SkyRewardAutoComplete.FindTurnedIn(
            Dump(("Bank15-Slot3", "Ivory Mask")), s.SkyQuestChecklist, s.SkyQuestCompleted));
    }

    [Fact]
    public void ApplyingTwiceMarksAndRecordsOnce()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");
        try
        {
            var s = WithIvoryMask();
            var ledger = new QuestLedgerStore(path) { TrackFilter = _ => true };
            var matches = SkyRewardAutoComplete.FindTurnedIn(
                Dump(("Bank15-Slot3", "Ivory Mask")), s.SkyQuestChecklist, s.SkyQuestCompleted);

            Assert.Equal(1, SkyRewardAutoComplete.Apply(matches, s, ledger, "enchanter_legends"));
            Assert.Equal(0, SkyRewardAutoComplete.Apply(matches, s, ledger, "enchanter_legends"));

            Assert.Single(s.SkyQuestCompleted, k => k.Equals(Key, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1, ledger.CompletedFor("enchanter_legends")
                ["Enchanter Sky Test: Ivory Mask"]);
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(path + ".rules"); } catch { }
        }
    }

    /// <summary>The real case, on the real fixture pair: Hateborne's own dump holds
    /// Cloak of Spiroc Feathers (Bank7-Slot4), a Necromancer Sky reward whose unlock
    /// achievement is still incomplete — owns the reward, never got an achievement
    /// credit for it. The dump is parsed by the shipped reader, not restated.</summary>
    [Fact]
    public void HateborneOwnsTheCloakAndItAutoCompletes()
    {
        var lines = File.ReadLines(HateborneInventory).ToList();
        var dump = new InventoryFile.Snapshot(HateborneInventory,
            new DateTime(2026, 9, 3, 12, 0, 0), InventoryFile.Parse(lines))
        { Entries = InventoryFile.ParseEntries(lines) };

        var s = new AppSettings();
        s.ApplyDefaultSkyQuestChecklist();

        var matches = SkyRewardAutoComplete.FindTurnedIn(
            dump, s.SkyQuestChecklist, s.SkyQuestCompleted);

        Assert.Contains(new SkyRewardAutoComplete.Match("Necromancer", "Cloak of Spiroc Feathers"),
            matches);
        SkyRewardAutoComplete.Apply(matches, s);
        Assert.Contains(QuestChecklistLayout.RewardKey("Necromancer", "Cloak of Spiroc Feathers"),
            s.SkyQuestCompleted);
    }

    // ---- the Core primitive both this pass and the player's click go through ----

    [Fact]
    public void MarkRewardTurnedInIsIdempotentAndReportsTheTransition()
    {
        var s = WithIvoryMask();
        var items = s.SkyQuestChecklist.Where(i => i.Reward == "Ivory Mask").ToList();

        Assert.True(QuestChecklistLayout.MarkRewardTurnedIn(s, Key, items));
        Assert.False(QuestChecklistLayout.MarkRewardTurnedIn(s, Key, items));
        Assert.Single(s.SkyQuestCompleted, k => k.Equals(Key, StringComparison.OrdinalIgnoreCase));
    }

    // ---- the Ready band's already-unlocked caveat (Core computes the words) ----

    private static QuestChecklistGroup ReadyGroup(AppSettings s) =>
        QuestChecklistLayout.Sky(s.SkyQuestChecklist, s.SkyQuestCompleted)
            .Single(g => g.Title == "Ivory Mask");

    [Fact]
    public void ReadyNoteIsNullWhenTheClassIsNotUnlocked()
    {
        var g = ReadyGroup(WithIvoryMask());
        Assert.Null(QuestChecklistLayout.ReadyNote(g, null));
        Assert.Null(QuestChecklistLayout.ReadyNote(g, ["Shaman"]));
    }

    [Fact]
    public void ReadyNoteNamesTheClassWhenUnlocked()
    {
        var g = ReadyGroup(WithIvoryMask());
        Assert.Equal("Enchanter already unlocked — turn in for the item only",
            QuestChecklistLayout.ReadyNote(g, ["enchanter"]));   // case-insensitive
    }

    [Fact]
    public void ReadyDetailJoinsTheNpcAndTheNote()
    {
        var g = ReadyGroup(WithIvoryMask());
        Assert.Equal("Enchanter Jolas", QuestChecklistLayout.ReadyDetail(g, null));
        Assert.Equal(
            "Enchanter Jolas — Enchanter already unlocked — turn in for the item only",
            QuestChecklistLayout.ReadyDetail(g, ["Enchanter"]));
    }

    [Fact]
    public void ReadyDetailIsJustTheNoteWhenNoNpcIsNamed()
    {
        var s = new AppSettings();
        s.SkyQuestChecklist.Add(new SkyQuestChecklistItem
        {
            Id = "z", ClassName = "Enchanter", Reward = "Ivory Mask", QuestItem = "Silken Mask",
        });
        var g = QuestChecklistLayout.Sky(s.SkyQuestChecklist, s.SkyQuestCompleted).Single();
        Assert.Equal("Enchanter already unlocked — turn in for the item only",
            QuestChecklistLayout.ReadyDetail(g, ["Enchanter"]));
    }
}
