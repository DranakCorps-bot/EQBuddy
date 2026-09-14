using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **READING A COIN AMOUNT SOMEBODY ELSE WROTE** (DRA-71 D7, plan P9).
///
/// <para>The shapes below are not invented: every one of them was taken from the survey of
/// eqlwiki's <c>merchant_value</c> field across the 10,957 cached item pages, which is the
/// evidence step the plan asked for before the promoter carried anything (trap 73's tell).
/// The refusals matter more than the acceptances — an unparseable value must be ABSENT rather
/// than guessed, and each of these is a real page this build declines to read rather than a
/// hypothetical.</para>
/// </summary>
public class CoinTextTests
{
    // ---- what it reads -------------------------------------------------------------------

    [Theory]
    [InlineData("1p 1g 2s 1c", 1121)]      // the commonest compact shape (11 pages)
    [InlineData("4p 7g 5s", 4750)]
    [InlineData("49p 4g", 49_400)]
    [InlineData("5s 8c", 58)]              // what the HTML block normalizes to
    [InlineData("5pp", 5000)]              // the other EQ spelling, 15 pages
    [InlineData("4gp", 400)]
    [InlineData("0cp", 0)]                 // a page saying the item is worthless
    [InlineData("  3g   2s  ", 320)]       // whitespace is not meaning
    public void ItReadsPlainCoinText(string text, long expected) =>
        Assert.Equal(expected, CoinText.Parse(text));

    /// <summary>Zero is a real answer and it is NOT null. A reader that folded the two would
    /// price a worthless item the same as one nobody has written a value for.</summary>
    [Fact]
    public void ZeroIsAnAnswerAndNullIsNot()
    {
        Assert.Equal(0L, CoinText.Parse("0cp"));
        Assert.Null(CoinText.Parse(""));
        Assert.Null(CoinText.Parse(null));
        Assert.Null(CoinText.Parse("   "));
    }

    // ---- what it refuses, and every one is a real page ------------------------------------

    /// <summary>
    /// **THE REFUSALS ARE THE FEATURE.** Each string here is a value that actually appears in
    /// the cached dump. Guessing at any of them — deciding what "~" means, dropping "Max",
    /// reading "4.2p" as four platinum two gold — would be inventing a number the page did not
    /// state, which is exactly what the survey was run to prevent.
    /// </summary>
    [Theory]
    [InlineData("absolutely nothing")]                  // 11 pages
    [InlineData("~2pp")]                                // an approximation
    [InlineData("~5 gold")]
    [InlineData("11.7p")]                               // a decimal platinum
    [InlineData("4.2p")]
    [InlineData("197.6p Max")]                          // qualified
    [InlineData("2p 1g 8s 3c with 111 Charisma")]       // a condition written inline
    [InlineData("6.5pp")]
    [InlineData("p")]                                   // a unit with no number
    [InlineData("5x")]                                  // a denomination this build does not know
    public void ItRefusesEverythingItCannotReadExactly(string text) =>
        Assert.Null(CoinText.Parse(text));

    /// <summary>A repeated denomination is refused rather than summed. Two statements of one
    /// unit is an editing mistake, and adding them up would invent an amount out of it.</summary>
    [Fact]
    public void ARepeatedDenominationIsRefused()
    {
        Assert.Null(CoinText.Parse("1p 2p"));
        Assert.Null(CoinText.Parse("3s 1g 4s"));
        // …and the same units stated once each still read, so the refusal is about the repeat
        // and not about the length of the string.
        Assert.Equal(1340L, CoinText.Parse("1p 3g 4s"));
    }

    // ---- the pairing with the one formatter -----------------------------------------------

    /// <summary>
    /// **THE ROUND TRIP IS WHY THE TWO ARE ALLOWED TO LIVE IN DIFFERENT FILES.**
    ///
    /// <para><see cref="StatsSnapshot.FormatCoin"/> is the one formatter every coin in this app
    /// is printed through; <see cref="CoinText.Parse"/> is its inverse and sits in its own file
    /// because <c>SessionStats*.cs</c> is a ratcheted hotspot. Adjacency was never the guard —
    /// this is. Every amount the formatter can produce has to come back through the parser
    /// unchanged, so a change to either grammar fails here rather than in a vendor price a
    /// player reads.</para>
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(58)]
    [InlineData(320)]
    [InlineData(1121)]
    [InlineData(4750)]
    [InlineData(49_400)]
    [InlineData(1_234_567)]
    public void EveryFormattedAmountParsesBackToItself(long copper) =>
        Assert.Equal(copper, CoinText.Parse(StatsSnapshot.FormatCoin(copper)));

    /// <summary>The prove-fail for the row above: an amount the formatter never emits is not
    /// quietly accepted because it happens to look similar. "0c" is what FormatCoin prints for
    /// nothing, and it round-trips; "0" on its own is not coin text.</summary>
    [Fact]
    public void TheRoundTripIsReadingTheGrammarRatherThanAgreeingWithItself()
    {
        Assert.Equal("0c", StatsSnapshot.FormatCoin(0));
        Assert.Equal(0L, CoinText.Parse("0c"));
        Assert.Null(CoinText.Parse("0"));
    }
}
