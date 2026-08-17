using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

// The grouping and labelling of the Epic and Sky checklists, shared by both desktop
// windows and (as the same shape) EQBuddy Mobile. Every assertion here is something
// #184 found missing on the desktop after the 2026-08-16 rewrite: the drop location,
// the reward as the unit of progress, and the * on a tick the player still has to
// place. They are pinned here because three surfaces read this and only a shared,
// tested definition stops a fourth divergence.
public class QuestChecklistLayoutTests
{
    private static SkyQuestChecklistItem Sky(
        string id, string cls, string npc, string reward, string item, string source,
        bool acquired = false, bool unassigned = false) => new()
    {
        Id = id, ClassName = cls, Npc = npc, Reward = reward, QuestItem = item,
        Source = source, Acquired = acquired, AcquiredUnassigned = unassigned,
    };

    private static readonly SkyQuestChecklistItem[] BardSky =
    [
        Sky("sky-005", "Bard", "Cilin Spellsinger", "Mask of Song",
            "Light Woolen Mask", "Isle 3: Gorgalosk"),
        Sky("sky-006", "Bard", "Cilin Spellsinger", "Mask of Song",
            "Wind Rune Meda", "Trash mobs"),
        Sky("sky-003", "Bard", "Cilin Spellsinger", "Mantle of the Songweaver",
            "Light Woolen Mantle", "Isle 4: Keeper of Souls"),
    ];

    [Fact]
    public void SkyGroupsByRewardNotByTurnInNpc()
    {
        // One NPC hands out every Bard sky reward. Grouping by the NPC collapses them
        // into one undifferentiated list, which is what #184 lost: you could no longer
        // see which pieces THIS reward still needs.
        var groups = QuestChecklistLayout.Sky(BardSky);

        Assert.Equal(2, groups.Count);
        Assert.Equal(["Bard · Mantle of the Songweaver", "Bard · Mask of Song"],
            groups.Select(g => g.Heading));
        Assert.Equal(2, groups.Single(g => g.Heading.EndsWith("Mask of Song")).Total);
    }

    [Fact]
    public void EveryRowCarriesTheTurnInNpcAndTheDropLocation()
    {
        // "Before there was turning npc and drop locations" (#184). Source was in the
        // model the whole time and simply never drawn.
        var row = QuestChecklistLayout.Sky(BardSky)
            .Single(g => g.Heading.EndsWith("Mask of Song"))
            .Rows.Single(r => r.Title == "Light Woolen Mask");

        Assert.Equal("Cilin Spellsinger · Isle 3: Gorgalosk", row.Detail);
    }

    [Fact]
    public void DetailDropsTheSeparatorWhenOnlyOneHalfExists()
    {
        var noSource = QuestChecklistLayout.Sky([Sky("a", "Bard", "Cilin", "R", "I", "")]);
        Assert.Equal("Cilin", noSource[0].Rows[0].Detail);

        var noNpc = QuestChecklistLayout.Sky([Sky("a", "Bard", "", "R", "I", "Isle 3")]);
        Assert.Equal("Isle 3", noNpc[0].Rows[0].Detail);
    }

    [Fact]
    public void AGroupReportsReadyOnlyWhenEveryPieceIsHeld()
    {
        var partly = QuestChecklistLayout.Sky([
            Sky("a", "Bard", "Cilin", "Mask of Song", "Mask", "Isle 3", acquired: true),
            Sky("b", "Bard", "Cilin", "Mask of Song", "Rune", "Trash"),
        ]);
        Assert.Equal("in progress", partly[0].Note);
        Assert.Equal(1, partly[0].Done);

        var all = QuestChecklistLayout.Sky([
            Sky("a", "Bard", "Cilin", "Mask of Song", "Mask", "Isle 3", acquired: true),
            Sky("b", "Bard", "Cilin", "Mask of Song", "Rune", "Trash", acquired: true),
        ]);
        Assert.Equal("ready", all[0].Note);
    }

    [Fact]
    public void AHandedInRewardReadsDoneRatherThanReady()
    {
        var groups = QuestChecklistLayout.Sky(
            [Sky("a", "Bard", "Cilin", "Mask of Song", "Mask", "Isle 3", acquired: true)],
            [QuestChecklistLayout.RewardKey("Bard", "Mask of Song")]);

        Assert.Equal("done", groups[0].Note);
    }

    [Fact]
    public void AnAutoPlacedTickIsMarkedSoThePlayerCanMoveIt()
    {
        // #106's contract, which Core's own comments promised and only the phone kept:
        // the loot auto-tick could not tell which class earned a shared item, so it
        // picked one and flagged it. A row that doesn't say so is a silent guess —
        // and #184 hit exactly that ("all are checked with no indication of anything
        // needing manual adjustments").
        var groups = QuestChecklistLayout.Sky(
            [Sky("a", "Bard", "Cilin", "Mask of Song", "Mask", "Isle 3",
                 acquired: true, unassigned: true)]);

        Assert.True(groups[0].Rows[0].Unassigned);
    }

    [Fact]
    public void EpicGroupsBySectionInWalkthroughOrder()
    {
        EpicQuestChecklistItem Epic(string id, string section, int order, string item) => new()
        {
            Id = id, ClassName = "Bard", Section = section, Order = order,
            QuestItem = item, QuestName = "Singing Short Sword", Source = "Kedge Keep",
        };

        var groups = QuestChecklistLayout.Epic([
            Epic("e3", "Part Two", 30, "Sea Sprite Blood"),
            Epic("e1", "Part One", 10, "Rock Fragments"),
            Epic("e2", "Part One", 20, "Sirens Hair"),
        ]);

        Assert.Equal(["Bard · Part One", "Bard · Part Two"], groups.Select(g => g.Heading));
        Assert.Equal(["Rock Fragments", "Sirens Hair"], groups[0].Rows.Select(r => r.Title));
        Assert.Equal("Kedge Keep", groups[0].Rows[0].Detail);
    }

    [Fact]
    public void AnEpicStepWithNoSourceFallsBackToTheQuestName()
    {
        // Mobile's rule, kept identical. The two must not diverge again (#184).
        var groups = QuestChecklistLayout.Epic([new EpicQuestChecklistItem
        {
            Id = "e1", ClassName = "Bard", Section = "Part One",
            QuestItem = "Hail Konia Swiftfoot", QuestName = "Bard Epic Quest",
        }]);

        Assert.Equal("Bard Epic Quest", groups[0].Rows[0].Detail);
    }

    [Fact]
    public void AnEmptyChecklistProducesNoGroupsRatherThanAnEmptyHeading()
    {
        Assert.Empty(QuestChecklistLayout.Sky([]));
        Assert.Empty(QuestChecklistLayout.Epic([]));
    }
}
