using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Epic checklist's loot auto-tick (#121): the Sky scoping rules (#98/#106)
/// applied to prose steps keyed by the catalog items they mention (ItemNames),
/// plus the epic wrinkles — +N tier folding, earliest-open-step-first.
/// </summary>
public class EpicLootAutoCheckTests
{
    // Red Dragon Scales genuinely feed both the Bard and Warrior epics; Kedge
    // Backbone is Bard-only and appears in a loot step AND the later turn-in step.
    private static List<EpicQuestChecklistItem> Checklist() =>
    [
        new() { ClassName = "Bard", Order = 0, QuestItem = "Kill Phinigel Autropos in Kedge Keep, loot Kedge Backbone",
                ItemNames = ["Kedge Backbone"] },
        new() { ClassName = "Bard", Order = 1, QuestItem = "Give Forpar's Note to Himself and Kedge Backbone to Forpar Fizfla",
                ItemNames = ["Forpar's Note to Himself", "Kedge Backbone"] },
        new() { ClassName = "Bard", Order = 2, QuestItem = "Kill a red dragon, loot Red Dragon Scales",
                ItemNames = ["Red Dragon Scales"] },
        new() { ClassName = "Warrior", Order = 0, QuestItem = "Loot Red Dragon Scales from a red dragon",
                ItemNames = ["Red Dragon Scales"] },
        new() { ClassName = "Monk", Order = 0, QuestItem = "Hail Trunt in the Warrens",
                ItemNames = [] },
    ];

    /// <summary>#193's Epic half: with no filter and no tab, an empty tab was a
    /// wildcard, so one Red Dragon Scales drop ticked BOTH the Bard and Warrior epics.
    /// AppSettings.EpicQuestClass has had no writer since the 2026-08-16 consolidation
    /// deleted the widget's Epic card, so this was permanently on.</summary>
    [Fact]
    public void WithNoFilterAndNoTabAMultiClassItemTicksOnceAndIsFlagged()
    {
        var list = Checklist();
        var changed = EpicLootAutoCheck.Apply(list, "Red Dragon Scales", 1, [], activeTab: "");

        Assert.True(changed);
        var scales = list.Where(i => i.ItemNames.Contains("Red Dragon Scales")).ToList();
        Assert.Single(scales, i => i.Acquired);
        Assert.True(scales.Single(i => i.Acquired).AcquiredUnassigned);
    }

    [Fact]
    public void ASingleClassItemTicksItsEarliestStepWhateverTabIsActive()
    {
        // Bard-only backbone looted on the Warrior tab, no class filter: still Bard
        // progress — and the LOOT step (Order 0) gets it, not the later turn-in.
        var list = Checklist();
        var changed = EpicLootAutoCheck.Apply(list, "Kedge Backbone", 1, [], activeTab: "Warrior");

        Assert.True(changed);
        var ticked = Assert.Single(list.Where(i => i.Acquired));
        Assert.Equal("Bard", ticked.ClassName);
        Assert.Equal(0, ticked.Order);
        Assert.False(ticked.AcquiredUnassigned);
    }

    [Fact]
    public void ASecondCopyMovesOnToTheNextOpenStep()
    {
        var list = Checklist();
        list.Single(i => i.ClassName == "Bard" && i.Order == 0).Acquired = true;

        EpicLootAutoCheck.Apply(list, "Kedge Backbone", 1, ["Bard"], activeTab: "");

        Assert.True(list.Single(i => i.ClassName == "Bard" && i.Order == 1).Acquired);
    }

    [Fact]
    public void ASharedItemStaysScopedToTheActiveTab()
    {
        // One physical scale must not tick two epics the player doesn't work.
        var list = Checklist();
        EpicLootAutoCheck.Apply(list, "Red Dragon Scales", 1, [], activeTab: "Warrior");

        Assert.True(list.Single(i => i.ClassName == "Warrior").Acquired);
        Assert.False(list.Single(i => i.ClassName == "Bard" && i.Order == 2).Acquired);
    }

    [Fact]
    public void ASharedItemTicksEveryClassInThePlayersFilter()
    {
        var list = Checklist();
        EpicLootAutoCheck.Apply(list, "Red Dragon Scales", 1, ["Bard", "Warrior"], activeTab: "Monk");

        Assert.True(list.Single(i => i.ClassName == "Bard" && i.Order == 2).Acquired);
        Assert.True(list.Single(i => i.ClassName == "Warrior").Acquired);
    }

    [Fact]
    public void AMultiClassItemNobodyClaimsParksOneFlaggedTick()
    {
        // Scales looted on the Monk tab with no filter: first owning class in
        // checklist order gets the tick, flagged for the player to move.
        var list = Checklist();
        var changed = EpicLootAutoCheck.Apply(list, "Red Dragon Scales", 1, [], activeTab: "Monk");

        Assert.True(changed);
        var ticked = Assert.Single(list.Where(i => i.Acquired));
        Assert.Equal("Bard", ticked.ClassName);
        Assert.True(ticked.AcquiredUnassigned);
    }

    [Fact]
    public void AFullLensClassParksNothingOnAnUnplayedClass()
    {
        // The lens class's step is already done: "nothing ticked" must mean nothing
        // ticked — not a guessed tick on a class the player doesn't play.
        var list = Checklist();
        list.Single(i => i.ClassName == "Warrior").Acquired = true;

        var changed = EpicLootAutoCheck.Apply(list, "Red Dragon Scales", 1, ["Warrior"], activeTab: "Monk");

        Assert.False(changed);
        Assert.False(list.Single(i => i.ClassName == "Bard" && i.Order == 2).Acquired);
    }

    [Fact]
    public void TheLootBudgetCapsStepsPerClass()
    {
        var list = Checklist();
        EpicLootAutoCheck.Apply(list, "Kedge Backbone", 2, ["Bard"], activeTab: "");

        // Two backbones looted: both Bard steps mentioning it tick, nothing else.
        Assert.Equal(2, list.Count(i => i.Acquired));
    }

    [Fact]
    public void TheUpgradeTierFoldsOffBeforeMatching()
    {
        // Legends suffixes upgraded drops ("Kedge Backbone +2"); ItemNames hold the
        // catalog's base spelling, so the looted name folds first.
        var list = Checklist();
        var changed = EpicLootAutoCheck.Apply(list, "Kedge Backbone +2", 1, ["Bard"], activeTab: "");

        Assert.True(changed);
        Assert.True(list.Single(i => i.ClassName == "Bard" && i.Order == 0).Acquired);
    }

    [Fact]
    public void AnUnmentionedItemIsANoOp()
    {
        var list = Checklist();
        var changed = EpicLootAutoCheck.Apply(list, "Rusty Sword", 1, ["Bard"], activeTab: "");

        Assert.False(changed);
        Assert.DoesNotContain(list, i => i.Acquired);
    }
}
