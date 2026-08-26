using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

// The tab strip shared by the desktop quest window and EQBuddy Mobile (David,
// 2026-08-15: "tabs for general quests, Epic quests, Plane of Sky quests … it should
// look and work the same on mobile"). These pin the parts a second implementation
// would otherwise be free to get subtly different: which tabs exist, their order,
// their names, and their wire keys.
public class QuestSurfaceTests
{
    [Fact]
    public void EveryTabAlwaysExistsInAFixedOrder()
    {
        // Nothing ticked anywhere: the tabs still show. A Sky tab that appears only
        // once you've started Sky hides itself from the player who most needs it.
        var tabs = QuestSurface.Tabs();
        Assert.Equal([QuestTab.General, QuestTab.Epic, QuestTab.Sky, QuestTab.Unlocks], tabs.Select(t => t.Tab));
    }

    [Fact]
    public void ChecklistTabsCarryTheirProgressBadge()
    {
        var tabs = QuestSurface.Tabs(epic: (3, 10), sky: (12, 12));
        Assert.Equal("3 / 10", tabs.Single(t => t.Tab == QuestTab.Epic).Badge);
        Assert.Equal("12 / 12", tabs.Single(t => t.Tab == QuestTab.Sky).Badge);
    }

    [Fact]
    public void GeneralHasNoBadgeBecauseItIsACatalogNotAChecklist()
    {
        // "0 / 900" would read as failure. General is a library you search, and the
        // whole catalog is never something you "finish".
        Assert.Null(QuestSurface.Tabs().Single(t => t.Tab == QuestTab.General).Badge);
    }

    [Fact]
    public void AnEmptyChecklistShowsNoBadgeRatherThanZeroOfZero()
    {
        var tabs = QuestSurface.Tabs(epic: (0, 0));
        Assert.Null(tabs.Single(t => t.Tab == QuestTab.Epic).Badge);
    }

    [Theory]
    [InlineData(QuestTab.General, "general", "Quests")]
    [InlineData(QuestTab.Epic, "epic", "Epic 1.0")]
    [InlineData(QuestTab.Sky, "sky", "Plane of Sky")]
    public void KeysAndLabelsAreTheOnesEveryUiUses(QuestTab tab, string key, string label)
    {
        Assert.Equal(key, QuestSurface.KeyFor(tab));
        Assert.Equal(label, QuestSurface.LabelFor(tab));
    }

    [Fact]
    public void EveryTabKeyRoundTrips()
    {
        // The mobile page stores the chosen tab by key in localStorage; a key it can't
        // read back is a device silently reset to the first tab on every launch.
        foreach (var t in QuestSurface.Tabs())
            Assert.Equal(t.Tab, QuestSurface.TabForKey(t.Key));
    }

    [Fact]
    public void AnUnknownKeyIsRejectedRatherThanGuessed()
    {
        Assert.Null(QuestSurface.TabForKey("epics"));   // near-miss of the real key
        Assert.Null(QuestSurface.TabForKey(""));
    }
}
