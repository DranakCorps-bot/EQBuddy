using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Which classes a character has, and how we know — the premise every class-aware surface
/// reads (the Quest Tracker's filters, the Gear Locker, the Sky lens, the next-level
/// unlock list, EQBuddy Mobile).
///
/// **It exists because that premise was wrong.** EQBuddy resolved this to one string, or
/// `""` when two classes were close, and a Legends character is up to THREE at once
/// (David, 2026-08-23). These are the rules that replace it, each with the reason it is
/// the way round it is.
/// </summary>
public class CharacterClassesTests
{
    [Fact]
    public void TheAchievementsDumpOutranksInferenceAndPicks()
    {
        var (classes, source) = CharacterClasses.Resolve(
            unlocked: ["Warrior", "Druid"], inferred: ["Monk"], picks: ["Bard"]);

        // The dump is the GAME's statement; the other two are a heuristic and a filter.
        Assert.Equal("Warrior", classes[0]);
        Assert.Equal(ClassSource.Achievements, source);
    }

    /// <summary>The dump is a SNAPSHOT, so it must not silence live evidence: a class
    /// unlocked after the last dump is real and the log is showing it right now.</summary>
    [Fact]
    public void InferenceJoinsTheDumpRatherThanBeingSuppressedByIt()
    {
        var (classes, source) = CharacterClasses.Resolve(
            unlocked: ["Warrior"], inferred: ["Druid"], picks: null);

        Assert.Equal(["Warrior", "Druid"], classes);
        // Still ACHIEVEMENTS: the strongest evidence in the list names the source.
        Assert.Equal(ClassSource.Achievements, source);
    }

    /// <summary>
    /// Picks WIDEN and never source. #104 established that a player may tick a class to
    /// help a friend, so picks can add — but a picked class must never be what tells the
    /// app what the character IS, which is Bevel's lock ("never fall back to the Quest
    /// Tracker filter") and was impossible to honour until the inference returned a list.
    /// </summary>
    [Fact]
    public void PicksWidenTheAnswerAndNeverNarrowIt()
    {
        var (classes, source) = CharacterClasses.Resolve(
            unlocked: null, inferred: ["Druid"], picks: ["Bard"]);

        Assert.Equal(["Druid", "Bard"], classes);
        Assert.Equal(ClassSource.Inferred, source);
    }

    /// <summary>Picks answering ALONE is the common case at launch — a player who has
    /// never dumped, on a log that has shown nothing yet.</summary>
    [Fact]
    public void PicksAnswerAloneWhenNothingElseKnowsAnything()
    {
        var (classes, source) = CharacterClasses.Resolve(null, null, ["Bard", "Monk"]);

        Assert.Equal(["Bard", "Monk"], classes);
        Assert.Equal(ClassSource.Picked, source);
    }

    [Fact]
    public void NothingKnownIsAnEmptyListAndSaysSo()
    {
        var (classes, source) = CharacterClasses.Resolve(null, [], []);

        Assert.Empty(classes);
        Assert.Equal(ClassSource.Unknown, source);
        Assert.Equal("", CharacterClasses.SourceLabel(source));
    }

    /// <summary>Three is the wiki's number, not ours — eqlwiki's `Character Classes` page
    /// on trio builds. A fourth is dropped rather than shown, and the cap applies across
    /// the merged list rather than per source.</summary>
    [Fact]
    public void TheListIsCappedAtThreeAcrossAllSources()
    {
        var (classes, _) = CharacterClasses.Resolve(
            unlocked: ["Warrior", "Druid"], inferred: ["Monk"], picks: ["Bard", "Cleric"]);

        Assert.Equal(3, CharacterClasses.Max);
        Assert.Equal(["Warrior", "Druid", "Monk"], classes);
    }

    [Fact]
    public void AClassNamedTwiceAppearsOnce()
    {
        var (classes, _) = CharacterClasses.Resolve(["Druid"], ["druid"], ["DRUID"]);

        Assert.Equal(["Druid"], classes);
    }

    /// <summary>The words a surface prints. One table so the two desktops and the phone
    /// cannot describe the same list three ways — and so a player can tell a fact from a
    /// guess, which is the whole reason the source travels at all.</summary>
    [Theory]
    [InlineData(ClassSource.Achievements, "from your achievements")]
    [InlineData(ClassSource.Inferred, "inferred from your log")]
    [InlineData(ClassSource.Picked, "your picks")]
    public void EachSourceHasWordsForIt(ClassSource source, string expected) =>
        Assert.Equal(expected, CharacterClasses.SourceLabel(source));
}
