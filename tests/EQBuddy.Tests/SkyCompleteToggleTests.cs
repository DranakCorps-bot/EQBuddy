using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Turning a Plane of Sky reward in — the capability that went missing on 2026-08-16 and
/// came back on 2026-08-18.
///
/// The widget's Sky card carried a per-reward turn-in check. When that card became a
/// launcher and the tracker was rebuilt around a list and a detail pane, the per-ITEM
/// ticks survived and the per-REWARD one did not: `SkyQuestCompleted` kept being READ by
/// both desktops and by EQBuddy Mobile while the achievements import became the only thing
/// that could WRITE it. These tests exist so the capability cannot go missing quietly
/// again — a reorganisation should not cost a feature.
/// </summary>
public class SkyCompleteToggleTests : IDisposable
{
    private readonly string _ledgerPath =
        Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_ledgerPath); } catch { }
        try { File.Delete(_ledgerPath + ".rules"); } catch { }
    }

    private QuestLedgerStore Ledger() => new(_ledgerPath) { TrackFilter = _ => true };

    private static AppSettings WithReward(bool firstAcquired = false)
    {
        var s = new AppSettings();
        s.SkyQuestChecklist.AddRange(
        [
            new SkyQuestChecklistItem
            {
                Id = "a", ClassName = "Ranger", Reward = "Bow of Sky",
                QuestItem = "Sky Emerald", Acquired = firstAcquired,
            },
            new SkyQuestChecklistItem
            {
                Id = "b", ClassName = "Ranger", Reward = "Bow of Sky",
                QuestItem = "Sky Diamond", AcquiredUnassigned = true, Acquired = true,
            },
            // A different reward, to prove the toggle is surgical.
            new SkyQuestChecklistItem
            {
                Id = "c", ClassName = "Ranger", Reward = "Cloak of Sky", QuestItem = "Sky Pearl",
            },
        ]);
        return s;
    }

    private static string Key => QuestChecklistLayout.RewardKey("Ranger", "Bow of Sky");

    /// <summary>Turning in acquires every item in the reward: you had them, and then you
    /// did not. Asking the player to tick six boxes to record one turn-in is what the old
    /// card refused to do.</summary>
    [Fact]
    public void TurningInMarksTheRewardAndAcquiresItsItems()
    {
        var s = WithReward();
        SkyCompleteToggle.MarkTurnedIn(s, Key, SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, Key));

        Assert.True(SkyCompleteToggle.IsTurnedIn(s, Key));
        Assert.All(s.SkyQuestChecklist.Where(i => i.Reward == "Bow of Sky"),
            i => Assert.True(i.Acquired));
    }

    /// <summary>A parked auto-tick is provisional until the player decides. Turning the
    /// reward in IS that decision — the same contract a manual tick has always had.</summary>
    [Fact]
    public void TurningInResolvesAParkedAutoTick()
    {
        var s = WithReward();
        SkyCompleteToggle.MarkTurnedIn(s, Key, SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, Key));
        Assert.All(s.SkyQuestChecklist, i => Assert.False(i.AcquiredUnassigned));
    }

    /// <summary>Reopening leaves the item boxes exactly as they are. A mis-click on the
    /// turn-in must cost one click to undo, not six — the player knows what they still
    /// hold, and silently clearing their ticks is the destructive half of a toggle.</summary>
    [Fact]
    public void ReopeningDoesNotUntickTheItems()
    {
        var s = WithReward();
        SkyCompleteToggle.MarkTurnedIn(s, Key, SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, Key));
        SkyCompleteToggle.Reopen(s, Key);

        Assert.False(SkyCompleteToggle.IsTurnedIn(s, Key));
        Assert.All(s.SkyQuestChecklist.Where(i => i.Reward == "Bow of Sky"),
            i => Assert.True(i.Acquired));
    }

    /// <summary>It touches one reward. A Ranger with two Sky rewards in flight must not
    /// have the other closed out from under them.</summary>
    [Fact]
    public void ItTouchesOnlyTheRewardNamed()
    {
        var s = WithReward();
        SkyCompleteToggle.MarkTurnedIn(s, Key, SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, Key));

        var other = s.SkyQuestChecklist.Single(i => i.Reward == "Cloak of Sky");
        Assert.False(other.Acquired);
        Assert.False(SkyCompleteToggle.IsTurnedIn(s,
            QuestChecklistLayout.RewardKey("Ranger", "Cloak of Sky")));
    }

    /// <summary>Idempotent: a click and the achievements import can both land on the same
    /// reward, and the key must not appear twice.</summary>
    [Fact]
    public void MarkingTwiceIsMarkingOnce()
    {
        var s = WithReward();
        var items = SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, Key);
        SkyCompleteToggle.MarkTurnedIn(s, Key, items);
        SkyCompleteToggle.MarkTurnedIn(s, Key, items);
        Assert.Single(s.SkyQuestCompleted, k => k.Equals(Key, StringComparison.OrdinalIgnoreCase));
    }

    // ---- consumption (#241 PR 2: the ✔ that was promised) ----

    /// <summary>Turning in consumes the reward's turn-in items from the quest ledger — the
    /// gap DasGud's report exposed: a Sky Test hand-in from the Sky tab's own button never
    /// touched the ledger, so the have-count stayed inflated forever. And it bumps the
    /// ledger's own completion count for the split quest, the same fact the Quests tab's
    /// non-Sky turn-in button already records via RecordCompletion.</summary>
    [Fact]
    public void TurningInConsumesTheRewardsItemsFromTheLedger()
    {
        var s = WithReward();
        var ledger = Ledger();
        var key = "ranger_legends";
        ledger.RecordLoot(key, "Sky Emerald", 1, new DateTime(2026, 8, 27, 12, 0, 0));
        ledger.RecordLoot(key, "Sky Diamond", 1, new DateTime(2026, 8, 27, 12, 0, 1));

        SkyCompleteToggle.MarkTurnedIn(s, Key,
            SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, Key), ledger, key);

        var owned = ledger.For(key);
        Assert.Equal(0, owned["Sky Emerald"].Total);
        Assert.Equal(0, owned["Sky Diamond"].Total);
        Assert.Equal(1, ledger.CompletedFor(key)["Ranger Sky Test: Bow of Sky"]);
    }

    /// <summary>The mirror of <see cref="ReopeningDoesNotUntickTheItems"/>: reopening does
    /// not hand the consumed items back either. A mis-click on the turn-in costs one click
    /// to undo on the checklist, and the ledger squares itself on the next dump regardless
    /// (#241) — restoring it here would be a second, competing way to fix the same number.</summary>
    [Fact]
    public void ReopeningDoesNotRestoreTheConsumedLedgerItems()
    {
        var s = WithReward();
        var ledger = Ledger();
        var key = "ranger_legends";
        ledger.RecordLoot(key, "Sky Emerald", 1, new DateTime(2026, 8, 27, 12, 0, 0));
        ledger.RecordLoot(key, "Sky Diamond", 1, new DateTime(2026, 8, 27, 12, 0, 1));
        SkyCompleteToggle.MarkTurnedIn(s, Key,
            SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, Key), ledger, key);

        SkyCompleteToggle.Reopen(s, Key);

        var owned = ledger.For(key);
        Assert.Equal(0, owned["Sky Emerald"].Total);
        Assert.Equal(0, owned["Sky Diamond"].Total);
    }

    /// <summary>A click and the achievements import can both arrive at
    /// <see cref="SkyCompleteToggle.MarkTurnedIn"/> for the same reward — the ledger must
    /// only ever be consumed once, or a second call would subtract items the player was
    /// never asked to hand in twice.</summary>
    [Fact]
    public void MarkingTwiceConsumesTheLedgerOnlyOnce()
    {
        var s = WithReward();
        var ledger = Ledger();
        var key = "ranger_legends";
        ledger.RecordLoot(key, "Sky Emerald", 2, new DateTime(2026, 8, 27, 12, 0, 0));
        ledger.RecordLoot(key, "Sky Diamond", 2, new DateTime(2026, 8, 27, 12, 0, 1));
        var items = SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, Key);

        SkyCompleteToggle.MarkTurnedIn(s, Key, items, ledger, key);
        SkyCompleteToggle.MarkTurnedIn(s, Key, items, ledger, key);

        var owned = ledger.For(key);
        Assert.Equal(1, owned["Sky Emerald"].Total);   // 2 looted − 1 consumed, not 0
        Assert.Equal(1, owned["Sky Diamond"].Total);
        Assert.Equal(1, ledger.CompletedFor(key)["Ranger Sky Test: Bow of Sky"]);
    }

    /// <summary>Omitting the ledger (the default) still runs the checklist side effect —
    /// a caller (or a test) that has no ledger in hand must not be forced to construct
    /// one.</summary>
    [Fact]
    public void OmittingTheLedgerStillMarksTheChecklist()
    {
        var s = WithReward();
        SkyCompleteToggle.MarkTurnedIn(s, Key, SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, Key));
        Assert.True(SkyCompleteToggle.IsTurnedIn(s, Key));
    }

    // ---- what the layout tells a view to draw ----

    /// <summary>Holding every piece and having handed them over are DIFFERENT states, and
    /// telling them apart is the entire reason Sky groups by reward.</summary>
    [Fact]
    public void EveryItemHeldIsReadyToTurnInAndNotYetDone()
    {
        var s = WithReward(firstAcquired: true);
        var group = QuestChecklistLayout.Sky(s.SkyQuestChecklist, s.SkyQuestCompleted)
            .Single(g => g.Heading.Contains("Bow of Sky"));

        Assert.True(group.ReadyToTurnIn);
        Assert.False(group.Completed);
        Assert.Equal("ready", group.Note);
    }

    [Fact]
    public void OnceTurnedInItIsDoneAndNoLongerOffersTheTurnIn()
    {
        var s = WithReward(firstAcquired: true);
        SkyCompleteToggle.MarkTurnedIn(s, Key, SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, Key));

        var group = QuestChecklistLayout.Sky(s.SkyQuestChecklist, s.SkyQuestCompleted)
            .Single(g => g.Heading.Contains("Bow of Sky"));

        Assert.True(group.Completed);
        Assert.False(group.ReadyToTurnIn);
        Assert.Equal("done", group.Note);
    }

    /// <summary>A reward you are still collecting offers nothing to turn in.</summary>
    [Fact]
    public void APartlyCollectedRewardIsNotReady()
    {
        var s = WithReward();
        var group = QuestChecklistLayout.Sky(s.SkyQuestChecklist, s.SkyQuestCompleted)
            .Single(g => g.Heading.Contains("Cloak of Sky"));
        Assert.False(group.ReadyToTurnIn);
        Assert.Null(group.Note);
    }

    /// <summary>Every Sky group carries the key a view needs to offer the control at all.
    /// Epic's completion is per CLASS, not per group, so its groups carry none — and a
    /// view must not grow a turn-in button there by accident.</summary>
    [Fact]
    public void OnlySkyGroupsCarryACompletionKey()
    {
        var s = WithReward();
        Assert.All(QuestChecklistLayout.Sky(s.SkyQuestChecklist, s.SkyQuestCompleted),
            g => Assert.False(string.IsNullOrEmpty(g.CompletionKey)));

        var epic = QuestChecklistLayout.Epic(
            [new EpicQuestChecklistItem { Id = "e", ClassName = "Ranger", Section = "Something", QuestItem = "A thing" }]);
        Assert.All(epic, g => Assert.Null(g.CompletionKey));
    }

    [Fact]
    public void TheButtonSaysWhichWayItGoes()
    {
        Assert.Equal("Mark turned in", SkyCompleteToggle.ButtonLabel(completed: false));
        Assert.Equal("Reopen", SkyCompleteToggle.ButtonLabel(completed: true));
    }

    /// <summary>A row whose ClassName has drifted from the catalog is INVISIBLE: every
    /// surface groups and filters by class, so the tick survives in settings.json and the
    /// player never sees it again. The catalog merge refreshes the class with the rest of
    /// the metadata — found on 2026-08-18 by seeding a checklist for a screenshot and
    /// watching the ticked rows disappear.</summary>
    [Fact]
    public void TheCatalogMergeRepairsARowWhoseClassDrifted()
    {
        var s = new AppSettings();
        s.ApplyDefaultSkyQuestChecklist();
        var row = s.SkyQuestChecklist[0];
        var realClass = row.ClassName;

        row.ClassName = "";        // what a hand-edited or partially-seeded profile looks like
        row.Acquired = true;       // and the player's tick, which must survive the repair

        s.ApplyDefaultSkyQuestChecklist();

        Assert.Equal(realClass, row.ClassName);
        Assert.True(row.Acquired);
    }
}
