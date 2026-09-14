using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **ONE LEVEL, TWO WRITERS, ORDERED BY TIME** (DRA-71 D3, plan P3).
///
/// <para>The acceptance bar the signed plan names is two FIXTURES, not one: *"ding-after-
/// statement wins; statement-after-ding wins."* Either one alone passes on an implementation
/// that simply prefers that source — which is the precedence table this feature exists
/// instead of — so they are written as a pair and neither is allowed to be the only one.</para>
/// </summary>
public class CharacterLevelTests
{
    private static readonly DateTime Early = new(2026, 9, 10, 20, 0, 0);
    private static readonly DateTime Late = new(2026, 9, 12, 21, 30, 0);

    // ---- the pair the plan asks for ------------------------------------------------------

    /// <summary>
    /// **Fixture 1: a ding AFTER your statement wins.**
    ///
    /// DRA-66's rule for classes carries over as *"a statement never beats FRESHER game
    /// truth"* — the game announced a level after you told EQBuddy one, so the game is now the
    /// newer claim and the statement has been overtaken.
    /// </summary>
    [Fact]
    public void ADingAfterYourStatementWins()
    {
        var resolved = CharacterLevel.Resolve(
            CharacterLevel.Reading(31, Late),
            CharacterLevel.Reading(28, Early));

        Assert.Equal(31, resolved.Level);
        Assert.Equal(LevelSource.Observed, resolved.Source);
        Assert.Equal(Late, resolved.At);
    }

    /// <summary>
    /// **Fixture 2: a statement AFTER the ding wins** — the Founder's own case.
    ///
    /// A Legends character holds up to three classes at once, so the level the log printed
    /// belongs to whatever was equipped when it printed. A player who swaps and says "I am 28
    /// on this one" is correcting a number that is still true about a different thing, and a
    /// table that ranked the game above the player would have no way to accept it.
    /// </summary>
    [Fact]
    public void AStatementAfterTheDingWins()
    {
        var resolved = CharacterLevel.Resolve(
            CharacterLevel.Reading(31, Early),
            CharacterLevel.Reading(28, Late));

        Assert.Equal(28, resolved.Level);
        Assert.Equal(LevelSource.Stated, resolved.Source);
        Assert.Equal(Late, resolved.At);
    }

    /// <summary>**The prove-fail for the pair above** (trap 34: green-only is vacuous). The
    /// same two numbers with the stamps swapped must produce the OTHER answer — so the
    /// assertions above are reading the TIME rather than agreeing with a source order that
    /// happened to fit.</summary>
    [Fact]
    public void SwappingTheSTAMPSSwapsTheWinnerWithTheNumbersUnchanged()
    {
        var dingIsNewer = CharacterLevel.Resolve(
            CharacterLevel.Reading(31, Late), CharacterLevel.Reading(28, Early));
        var statementIsNewer = CharacterLevel.Resolve(
            CharacterLevel.Reading(31, Early), CharacterLevel.Reading(28, Late));

        Assert.NotEqual(dingIsNewer.Level, statementIsNewer.Level);
        Assert.NotEqual(dingIsNewer.Source, statementIsNewer.Source);
    }

    // ---- the states around the pair -------------------------------------------------------

    [Fact]
    public void OneClaimAnswersAlone()
    {
        Assert.Equal(
            new ResolvedLevel(30, LevelSource.Observed, Early),
            CharacterLevel.Resolve(CharacterLevel.Reading(30, Early), null));
        Assert.Equal(
            new ResolvedLevel(30, LevelSource.Stated, Early),
            CharacterLevel.Resolve(null, CharacterLevel.Reading(30, Early)));
    }

    /// <summary>No claims at all is a real answer and not an error. <c>Known</c> is the guard
    /// every caller reads instead of comparing to zero in its own way.</summary>
    [Fact]
    public void NoClaimsIsUnknownAndUnknownIsNotKnown()
    {
        var resolved = CharacterLevel.Resolve(null, null);
        Assert.Equal(ResolvedLevel.Unknown, resolved);
        Assert.False(resolved.Known);
        Assert.Equal(0, resolved.Level);
        // And `default` is the same state, because HelperInputs takes it as a default
        // argument — a record struct whose default was a DIFFERENT state from its own
        // Unknown would put a level of 0 tagged Observed into the engine.
        Assert.Equal(ResolvedLevel.Unknown, default(ResolvedLevel));
    }

    /// <summary>
    /// **A tie goes to the statement, and it is written down rather than left to an
    /// operator.** A player who typed a level in the same second the game announced one was
    /// there for both, and the number they typed is the one they meant. A tie-break nobody
    /// named is a tie-break nobody can check.
    /// </summary>
    [Fact]
    public void AnExactTieGoesToTheStatement()
    {
        var resolved = CharacterLevel.Resolve(
            CharacterLevel.Reading(31, Early), CharacterLevel.Reading(28, Early));
        Assert.Equal(28, resolved.Level);
        Assert.Equal(LevelSource.Stated, resolved.Source);
    }

    /// <summary>
    /// **An UNSTAMPED stored level is the oldest claim there is** — the migration, asserted.
    ///
    /// Profiles written before this slice carry a level with no <c>LevelAt</c>, which arrives
    /// here as <see cref="DateTime.MinValue"/>. It must still ANSWER when it is alone (the
    /// player's stored level does not disappear on upgrade) and must YIELD to any statement
    /// made afterwards (they have not been made unbeatable until their next ding).
    /// </summary>
    [Fact]
    public void AnUnstampedStoredLevelAnswersAloneAndYieldsToAnyStatement()
    {
        var alone = CharacterLevel.Resolve(CharacterLevel.Reading(29, default), null);
        Assert.Equal(29, alone.Level);
        Assert.Equal(LevelSource.Observed, alone.Source);

        var overtaken = CharacterLevel.Resolve(
            CharacterLevel.Reading(29, default), CharacterLevel.Reading(34, Early));
        Assert.Equal(34, overtaken.Level);
        Assert.Equal(LevelSource.Stated, overtaken.Source);
    }

    /// <summary>Zero and negative are not claims. <c>LevelFor</c> answers 0 for "never seen"
    /// and the editor refuses an unparseable box, so both have to stop being levels before
    /// anything compares them as one.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveLevelIsNotAReading(int level)
    {
        Assert.Null(CharacterLevel.Reading(level, Early));
        Assert.False(CharacterLevel.Resolve(CharacterLevel.Reading(level, Early), null).Known);
    }

    // ---- the source label ------------------------------------------------------------------

    /// <summary>**ONE TABLE**, and every source a surface can be handed is in it. The sweep
    /// is over the enum rather than a list beside it (trap 30), and Unknown's empty answer is
    /// asserted as the DECISION it is: a line about a level nobody knows says so in its own
    /// sentence, not with a dangling "— ".</summary>
    [Theory]
    [MemberData(nameof(Sources))]
    public void EverySourceIsLabelledAndOnlyUnknownIsSilent(LevelSource source) =>
        Assert.Equal(source != LevelSource.Unknown,
            CharacterLevel.SourceLabel(source).Length > 0);

    /// <summary>The two words the plan named, verbatim. Asserted because they are the whole
    /// of how a player tells a fact from their own statement at a glance, and a reword that
    /// blurred them would pass every other test in this file.</summary>
    [Fact]
    public void TheTwoLabelsAreTheOnesThePlanNamed()
    {
        Assert.Equal("from your log's ding lines", CharacterLevel.SourceLabel(LevelSource.Observed));
        Assert.Equal("set by you", CharacterLevel.SourceLabel(LevelSource.Stated));
    }

    public static TheoryData<LevelSource> Sources()
    {
        var data = new TheoryData<LevelSource>();
        foreach (var source in Enum.GetValues<LevelSource>()) data.Add(source);
        return data;
    }
}
