using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Sky checklist's loot auto-tick scoping: shared items respect the player's
/// class filter / active tab (#98), single-class items tick their class no matter
/// what tab is showing (#106 — both reported by bjstrange, five days apart).
/// </summary>
public class SkyLootAutoCheckTests
{
    private static List<SkyQuestChecklistItem> Checklist() =>
    [
        new() { ClassName = "Berserker", QuestItem = "Great Staff", Reward = "Test of Fury" },
        new() { ClassName = "Druid", QuestItem = "Wind Rune Azia", Reward = "Test of Nature" },
        new() { ClassName = "Monk", QuestItem = "Wind Rune Azia", Reward = "Test of Fists" },
        new() { ClassName = "Wizard", QuestItem = "Wind Rune Azia", Reward = "Test of Frost" },
    ];

    /// <summary>
    /// #193, wizen: "Sky Quest Tracker falsely reporting data ... it shows me as having
    /// looted a bunch of things I did not loot."
    ///
    /// With NO class filter and NO active tab, the predicate treated an empty tab as a
    /// WILDCARD, so one physical Wind Rune Azia ticked Druid, Monk AND Wizard. It was
    /// survivable while the widget's Sky card kept the tab populated; the 2026-08-16
    /// consolidation deleted that card, and AppSettings.SkyQuestClass has had no writer
    /// since — so for anyone who had not set classes in the ⚙ picker the wildcard was
    /// permanently on. No test covered this case, which is how it shipped.
    ///
    /// One rune is one rune: park a single tick and flag it, don't invent three.
    /// </summary>
    [Fact]
    public void WithNoFilterAndNoTabASharedItemTicksOnceNotOncePerClass()
    {
        var list = Checklist();
        var changed = SkyLootAutoCheck.Apply(list, "Wind Rune Azia", 1, [], activeTab: "");

        Assert.True(changed);
        var runes = list.Where(i => i.QuestItem == "Wind Rune Azia").ToList();
        Assert.Single(runes, i => i.Acquired);
        // ...and it wears the * so the player can move it to the class that earned it.
        Assert.True(runes.Single(i => i.Acquired).AcquiredUnassigned);
    }

    [Fact]
    public void WithNoFilterAndNoTabAnUnambiguousItemStillTicksItsOnlyClass()
    {
        // Rule 2 is untouched: exactly one class wants the Great Staff, so there is
        // nothing to guess and nothing to flag.
        var list = Checklist();
        SkyLootAutoCheck.Apply(list, "Great Staff", 1, [], activeTab: "");

        var staff = list.Single(i => i.QuestItem == "Great Staff");
        Assert.True(staff.Acquired);
        Assert.False(staff.AcquiredUnassigned);
    }

    [Fact]
    public void ASingleClassItemTicksItsClassWhateverTabIsActive()
    {
        // #106 verbatim: active tab Druid, no class filter, Berserker staff drops.
        var list = Checklist();
        var changed = SkyLootAutoCheck.Apply(list, "Great Staff", 1, [], activeTab: "Druid");

        Assert.True(changed);
        Assert.True(list.Single(i => i.ClassName == "Berserker").Acquired);
    }

    [Fact]
    public void ASharedItemStaysScopedToTheActiveTab()
    {
        // One physical rune must not tick three class plans (#98's careful side).
        var list = Checklist();
        SkyLootAutoCheck.Apply(list, "Wind Rune Azia", 1, [], activeTab: "Druid");

        Assert.True(list.Single(i => i.ClassName == "Druid" && i.QuestItem == "Wind Rune Azia").Acquired);
        Assert.False(list.Single(i => i.ClassName == "Monk").Acquired);
        Assert.False(list.Single(i => i.ClassName == "Wizard").Acquired);
    }

    [Fact]
    public void ASharedItemTicksEveryClassInThePlayersFilter()
    {
        // #98's shipped behavior: the multiclass set all tick, one slot each.
        var list = Checklist();
        SkyLootAutoCheck.Apply(list, "Wind Rune Azia", 1, ["Druid", "Monk"], activeTab: "Wizard");

        Assert.True(list.Single(i => i.ClassName == "Druid" && i.QuestItem == "Wind Rune Azia").Acquired);
        Assert.True(list.Single(i => i.ClassName == "Monk").Acquired);
        Assert.False(list.Single(i => i.ClassName == "Wizard").Acquired);
    }

    [Fact]
    public void TheLootBudgetCapsSlotsPerClass()
    {
        var list = new List<SkyQuestChecklistItem>
        {
            new() { ClassName = "Berserker", QuestItem = "Great Staff", Reward = "Test of Fury" },
            new() { ClassName = "Berserker", QuestItem = "Great Staff", Reward = "Test of Rage" },
        };
        SkyLootAutoCheck.Apply(list, "Great Staff", 1, [], activeTab: "Druid");

        Assert.Equal(1, list.Count(i => i.Acquired));   // one staff, one tick
    }

    // ---- #106 round two: an item exactly TWO classes want, neither in the lens ----

    private static List<SkyQuestChecklistItem> TwoClassChecklist() =>
    [
        new() { ClassName = "Berserker", QuestItem = "Twisted Staff", Reward = "Test of Fury" },
        new() { ClassName = "Necromancer", QuestItem = "Twisted Staff", Reward = "Test of Decay" },
        new() { ClassName = "Druid", QuestItem = "Wind Rune Azia", Reward = "Test of Nature" },
    ];

    [Fact]
    public void AMultiClassItemNobodyClaimsParksOneFlaggedTick()
    {
        // bjstrange's staff: wanted by Berserker AND Necromancer, looted on the Druid
        // tab with no filter. Previously lost entirely; now the first owning class
        // gets the tick, flagged for the player to move.
        var list = TwoClassChecklist();
        var changed = SkyLootAutoCheck.Apply(list, "Twisted Staff", 1, [], activeTab: "Druid");

        Assert.True(changed);
        var ticked = Assert.Single(list.Where(i => i.Acquired));
        Assert.Equal("Berserker", ticked.ClassName);       // first owning class, deterministic
        Assert.True(ticked.AcquiredUnassigned);            // wears the *
    }

    [Fact]
    public void ALensMatchTicksNormallyWithoutTheFlag()
    {
        var list = TwoClassChecklist();
        SkyLootAutoCheck.Apply(list, "Twisted Staff", 1, ["Necromancer"], activeTab: "Druid");

        var ticked = Assert.Single(list.Where(i => i.Acquired));
        Assert.Equal("Necromancer", ticked.ClassName);
        Assert.False(ticked.AcquiredUnassigned);           // a real match is no guess
    }

    [Fact]
    public void AFullLensClassParksNothingOnAnUnplayedClass()
    {
        // 2026-08-13 review: "!changed" was a proxy for "no lens match" and fired
        // when the lens class's slots were simply full — guessing a tick onto a
        // class the player doesn't play. A second copy now ticks nothing.
        var list = TwoClassChecklist();
        list.Single(i => i.ClassName == "Berserker" && i.QuestItem == "Twisted Staff").Acquired = true;

        var changed = SkyLootAutoCheck.Apply(list, "Twisted Staff", 1, ["Berserker"], activeTab: "Druid");

        Assert.False(changed);
        Assert.False(list.Single(i => i.ClassName == "Necromancer").Acquired);
    }

    [Fact]
    public void TheParkedTickRespectsTheLootBudget()
    {
        var list = TwoClassChecklist();
        SkyLootAutoCheck.Apply(list, "Twisted Staff", 2, [], activeTab: "Druid");

        // Two staffs looted: both open slots get one, both flagged.
        Assert.Equal(2, list.Count(i => i.Acquired));
        Assert.All(list.Where(i => i.Acquired), i => Assert.True(i.AcquiredUnassigned));
    }

    // ---- upgrade tiers fold (#156, bjstrange) ----

    /// <summary>
    /// "Azure Ring +1" is an Azure Ring. The dump and the loot line both print the
    /// upgrade suffix; every catalog — sky, epic, gear, the wiki — stores the base name.
    ///
    /// This checker compared raw strings until 1.85.0 while EpicLootAutoCheck and
    /// GearLootAutoCheck both folded through QuestCatalog.BaseItemName, so one loot line
    /// ticked a gear wish and an epic step and quietly skipped the Sky row that wanted
    /// the same item. The inconsistency is the bug; the fold is the fix.
    /// </summary>
    [Fact]
    public void AnUpgradedDropTicksTheRowForItsBaseItem()
    {
        var list = Checklist();

        var changed = SkyLootAutoCheck.Apply(list, "Great Staff +1", 1, [], activeTab: "Druid");

        Assert.True(changed);
        Assert.True(list.Single(i => i.ClassName == "Berserker").Acquired);
    }

    [Fact]
    public void ADoubleDigitUpgradeFoldsToo()
    {
        var list = Checklist();

        Assert.True(SkyLootAutoCheck.Apply(list, "Great Staff +12", 1, [], activeTab: "Druid"));
        Assert.True(list.Single(i => i.ClassName == "Berserker").Acquired);
    }

    [Fact]
    public void FoldingStillRespectsTheClassLens()
    {
        // The fold must not smuggle an item past the scoping rules: a shared rune with
        // an upgrade suffix is still shared, and still ticks only the played class.
        var list = Checklist();

        SkyLootAutoCheck.Apply(list, "Wind Rune Azia +1", 1, ["Druid"], activeTab: "");

        Assert.True(list.Single(i => i.ClassName == "Druid").Acquired);
        Assert.False(list.Single(i => i.ClassName == "Monk").Acquired);
        Assert.False(list.Single(i => i.ClassName == "Wizard").Acquired);
    }

    [Fact]
    public void AnUnrelatedItemStillDoesNotMatch()
    {
        // Guard against the fold turning into a loose contains-match.
        var list = Checklist();

        Assert.False(SkyLootAutoCheck.Apply(list, "Great Staff of Something Else", 1, [], "Druid"));
        Assert.All(list, i => Assert.False(i.Acquired));
    }
}
