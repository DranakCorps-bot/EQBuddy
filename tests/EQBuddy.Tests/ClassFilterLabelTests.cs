using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

// #184: ticking every class turned the button face into a 16-item list that pushed the
// mode strip off the window. These pin the cap — and pin that a real character, who has
// at most Legends' three classes, still sees them named.
public class ClassFilterLabelTests
{
    [Fact]
    public void NoneOrOneReadsPlainly()
    {
        Assert.Equal("Any class", ClassFilterLabel.For([]));
        Assert.Equal("Cleric", ClassFilterLabel.For(["Cleric"]));
    }

    [Fact]
    public void ARealCharactersClassesAreStillNamed()
    {
        // Legends allows three. The cap must never hide a class an actual player has.
        Assert.Equal("BRD · CLR · WAR", ClassFilterLabel.For(["Bard", "Cleric", "Warrior"]));
    }

    [Fact]
    public void BrowsingManyClassesCountsInsteadOfListing()
    {
        Assert.Equal("4 classes",
            ClassFilterLabel.For(["Bard", "Cleric", "Warrior", "Wizard"]));
    }

    [Fact]
    public void EveryClassReadsAllClassesRatherThanSixteen()
    {
        Assert.Equal("All classes", ClassFilterLabel.For(QuestClassFilter.Classes));
    }

    [Fact]
    public void TheLabelNeverGrowsWithTheSelection()
    {
        // The actual defect: label width tracked the number of classes picked. Whatever
        // is selected, the face stays short enough to leave the filter row its controls.
        var longest = 0;
        for (var n = 0; n <= QuestClassFilter.Classes.Length; n++)
            longest = Math.Max(longest, ClassFilterLabel.For(QuestClassFilter.Classes[..n]).Length);

        Assert.True(longest <= "BRD · BST · BER".Length + 1,
            $"longest class-filter label was {longest} chars");
    }
}
