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

    // ---- Character Setup's statement (DRA-66) --------------------------------------

    /// <summary>
    /// The whole reason the parameter exists: "correct EQBuddy's guess" is not a
    /// correction if the guess can keep arguing with it. A player who states Warrior
    /// against an inference of Rogue (a borrowed clicky, #120's shape) must see Warrior
    /// and ONLY Warrior — a union here would show "Warrior · Rogue", which is the wrong
    /// answer surviving the fix.
    /// </summary>
    [Fact]
    public void AStatedClassSilencesTheGuess()
    {
        var (classes, source) = CharacterClasses.Resolve(
            unlocked: null, inferred: ["Rogue"], picks: null, stated: ["Warrior"]);

        Assert.Equal(["Warrior"], classes);
        Assert.Equal(ClassSource.Stated, source);
    }

    /// <summary>The dump is the GAME's statement and outranks the player's memory of a
    /// character screen — it stays, and the two union the way the dump and inference
    /// always have. What the statement removes is only the GUESS.</summary>
    [Fact]
    public void TheDumpStillLeadsAndUnionsWithAStatement()
    {
        var (classes, source) = CharacterClasses.Resolve(
            unlocked: ["Warrior"], inferred: ["Monk"], picks: null, stated: ["Druid"]);

        Assert.Equal(["Warrior", "Druid"], classes);
        Assert.Equal(ClassSource.Achievements, source);
    }

    /// <summary>An EMPTY statement is "go back to EQBuddy's own reading" — the undo the
    /// store deliberately allows (unlike an empty dump list, which is a parse failure).
    /// Behaviour with nothing stated is byte-identical to before DRA-66.</summary>
    [Fact]
    public void ClearingTheStatementPutsTheGuessBackInCharge()
    {
        var (classes, source) = CharacterClasses.Resolve(
            unlocked: null, inferred: ["Rogue"], picks: null, stated: []);

        Assert.Equal(["Rogue"], classes);
        Assert.Equal(ClassSource.Inferred, source);
    }

    /// <summary>#104 is untouched: the quest picker is a lens that may widen whatever the
    /// identity answer is, a statement included — helping a friend does not stop working
    /// because you told EQBuddy who you are.</summary>
    [Fact]
    public void PicksStillWidenAStatedAnswer()
    {
        var (classes, source) = CharacterClasses.Resolve(
            unlocked: null, inferred: ["Monk"], picks: ["Bard"], stated: ["Warrior"]);

        Assert.Equal(["Warrior", "Bard"], classes);
        Assert.Equal(ClassSource.Stated, source);
    }

    /// <summary>The words a surface prints. One table so the two desktops and the phone
    /// cannot describe the same list three ways — and so a player can tell a fact from a
    /// guess, which is the whole reason the source travels at all.</summary>
    [Theory]
    [InlineData(ClassSource.Achievements, "from your achievements")]
    [InlineData(ClassSource.Stated, "from your setup")]
    [InlineData(ClassSource.Inferred, "inferred from your log")]
    [InlineData(ClassSource.Picked, "from your picks")]
    public void EachSourceHasWordsForIt(ClassSource source, string expected) =>
        Assert.Equal(expected, CharacterClasses.SourceLabel(source));

    /// <summary>
    /// The label names a SOURCE and nothing else — Bevel, Helm-signed 2026-08-23: *"Do not
    /// say 'override'"* and *"the phone must not compose a second verb around
    /// SourceLabel."*
    ///
    /// It shipped for about an hour as "… — pick classes to override", which told the
    /// player to override their own character. The picker is a lens over identity (#104),
    /// not a replacement for it, so the parenthetical that names who you ARE cannot also
    /// instruct you to change it. Asserted rather than trusted, because the pressure to
    /// add "— do X" to a label is constant and the string lives in one place.
    /// </summary>
    [Fact]
    public void TheLabelCarriesNoVerbAndNoInstruction()
    {
        foreach (var source in Enum.GetValues<ClassSource>())
        {
            var label = CharacterClasses.SourceLabel(source);
            Assert.DoesNotContain("override", label, StringComparison.OrdinalIgnoreCase);
            // "pick" as a NOUN is the source's own name ("from your picks") and belongs
            // here; what must not appear is the INSTRUCTION. Asserting the bare substring
            // failed on the correct string, which is the assertion being wrong rather than
            // the label — the difference between naming a source and telling the player to
            // change it is a verb, not a word stem.
            Assert.DoesNotContain("pick classes", label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" to ", label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("—", label);
        }
        // And the three read in parallel, which is what makes fact-vs-guess scannable.
        Assert.StartsWith("from your", CharacterClasses.SourceLabel(ClassSource.Achievements));
        Assert.StartsWith("from your", CharacterClasses.SourceLabel(ClassSource.Picked));
        Assert.Contains("inferred", CharacterClasses.SourceLabel(ClassSource.Inferred));
    }
}
