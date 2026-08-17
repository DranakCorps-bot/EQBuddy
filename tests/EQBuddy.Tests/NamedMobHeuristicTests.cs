using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

// discussion #185, elderbit: the curated spawn catalog will always have gaps, and the
// log already distinguishes named from trash by whether it uses an article. These pin
// the rule and — more importantly — the cases where it must say NO, because a false
// positive invents a respawn timer for something that has none.
public class NamedMobHeuristicTests
{
    [Theory]
    [InlineData("Chief Goonda")]
    [InlineData("Slizik the Mighty")]
    [InlineData("CWG Model EXG")]
    [InlineData("Lady Vox")]
    public void AProperNameWithNoArticleReadsAsNamed(string raw) =>
        Assert.True(NamedMobHeuristic.LooksProperName(raw));

    [Theory]
    [InlineData("a skeleton")]
    [InlineData("an ogre shaman")]
    [InlineData("A skeleton")]
    [InlineData("An ogre shaman")]
    [InlineData("a putrid skeleton")]
    public void AnArticleMeansTrashWhateverTheCase(string raw) =>
        Assert.False(NamedMobHeuristic.LooksProperName(raw));

    [Fact]
    public void TheIsTreatedAsTrashBecauseItProvesNothing()
    {
        // The wiki writes "the Spiroc Lord" and "Spiroc Lord" both ways, so a leading
        // "the" is not evidence. Calling it trash costs a timer we never had; calling
        // it named risks one we shouldn't have.
        Assert.False(NamedMobHeuristic.LooksProperName("the Spiroc Lord"));
    }

    [Fact]
    public void ALowercaseBareNameIsNotProof()
    {
        // Articleless but uncapitalised is a log the game didn't capitalise — not a
        // proper name. Being wrong here would time every trash mob in the zone.
        Assert.False(NamedMobHeuristic.LooksProperName("skeleton"));
    }

    [Fact]
    public void EmptyAndWhitespaceAreNotNamed()
    {
        Assert.False(NamedMobHeuristic.LooksProperName(null));
        Assert.False(NamedMobHeuristic.LooksProperName("   "));
    }

    [Fact]
    public void ACorpseIsNotAMob() =>
        Assert.False(NamedMobHeuristic.LooksProperName("Chief Goonda's corpse"));

    [Fact]
    public void PetsAreExcludedEvenThoughTheyLookNamed()
    {
        // elderbit raised this himself: "Lonn slashes an ogre shaman" — pets carry
        // proper names and no article. A pet dying is not a spawn cycle.
        Assert.True(NamedMobHeuristic.LooksProperName("Gobanab"));
        Assert.True(NamedMobHeuristic.IsExcluded("Gobanab", ["Gobanab"], []));
        Assert.False(NamedMobHeuristic.IsTimeableNamed("Gobanab", "Gobanab", ["Gobanab"], []));
    }

    [Fact]
    public void PlayersAreExcludedToo()
    {
        // A group member's death is a proper-named kill line and must never start a
        // respawn clock.
        Assert.False(NamedMobHeuristic.IsTimeableNamed("Xyrid", "Xyrid", [], ["Xyrid"]));
    }

    [Fact]
    public void ExclusionIgnoresCase() =>
        Assert.True(NamedMobHeuristic.IsExcluded("gobanab", ["Gobanab"], []));

    [Fact]
    public void ARealNamedWithNoPetOrPlayerCollisionIsTimeable() =>
        Assert.True(NamedMobHeuristic.IsTimeableNamed(
            "Chief Goonda", "Chief Goonda", ["Gobanab"], ["Xyrid"]));

    [Fact]
    public void TheNormalizedNameCanNoLongerAnswerWhichIsWhyRawIsRequired()
    {
        // The whole reason KillEvent carries the raw verdict: after Normalize, trash and
        // named are indistinguishable. If this ever passes both, the plumbing is wrong.
        Assert.Equal("Skeleton", LogParser.Normalize("a skeleton"));
        Assert.True(NamedMobHeuristic.LooksProperName(LogParser.Normalize("a skeleton")));
        Assert.False(NamedMobHeuristic.LooksProperName("a skeleton"));
    }
}
