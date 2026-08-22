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
    public void TheNormalizedNameCanNoLongerAnswerWhichIsWhyRawIsRequired()
    {
        // The whole reason KillEvent carries the raw verdict: after Normalize, trash and
        // named are indistinguishable. If this ever passes both, the plumbing is wrong.
        Assert.Equal("Skeleton", LogParser.Normalize("a skeleton"));
        Assert.True(NamedMobHeuristic.LooksProperName(LogParser.Normalize("a skeleton")));
        Assert.False(NamedMobHeuristic.LooksProperName("a skeleton"));
    }
    /// <summary>
    /// A PET IS NOT A NAMED (David, 2026-08-22, from his own spawn list: *"we were starting
    /// to see named's pet"*).
    ///
    /// `Xanthus`s pet` has no article and is capitalised, so the article convention waves it
    /// through and it earns a respawn timer — for a thing with no spawn cycle, only a
    /// summoner. An invented timer teaches you to walk to a camp that is not up.
    /// </summary>
    [Theory]
    [InlineData("Xanthus`s pet")]       // the game's own backtick possessive
    [InlineData("Xanthus's pet")]       // straight apostrophe
    [InlineData("Lord Nagafen`s pet")]  // a real named in front of it changes nothing
    [InlineData("Pet")]                 // bare, however it reached us
    public void APetIsNeverATimeableNamed(string raw) =>
        Assert.False(NamedMobHeuristic.LooksProperName(raw));

    /// <summary>The negative, and it is the one that matters: "pet" is an ordinary run of
    /// letters inside real names, and matching it as a SUBSTRING would quietly delete named
    /// mobs from the spawn list. Being wrong that way is far more expensive than missing one
    /// pet, because a missing timer is visible and a missing MOB is not.</summary>
    [Theory]
    [InlineData("Petrifier")]
    [InlineData("Petras Thex")]
    [InlineData("Carpet Merchant")]
    public void NamesThatMerelyContainPetAreStillNamed(string raw) =>
        Assert.True(NamedMobHeuristic.LooksProperName(raw));

}
