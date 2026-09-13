using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// <see cref="PickerFace"/> — #184's cap, with the word "class" taken out of it (DRA-71 D2).
///
/// <para><c>ClassFilterLabelTests</c> stays exactly as it was and is the regression half: the
/// class picker's behaviour is the acceptance bar for the migration, so a test that changed to
/// accommodate the refactor would have removed the only evidence it behaves identically. These
/// are about the GENERALISATION — that the rule still caps when the noun is long, which is the
/// case the class picker never had and the Helper's goals are.</para>
/// </summary>
public class PickerFaceTests
{
    // The Founder's nine, longest first, so a bound written against "the longest" is written
    // against the real one.
    private static readonly string[] Goals =
        [.. Recommendations.All.Select(HelperPresentation.GoalLabel)];

    [Fact]
    public void NothingPickedReadsAsTheOpenState() =>
        Assert.Equal("Any goal", PickerFace.For([], "goal", "goals"));

    /// <summary>One pick is always its own name — "1 goal" is strictly less than the goal's
    /// name and no narrower than it needs to be. The class face has done this since #184
    /// ("Cleric", not "1 class") and the generalisation keeps it.</summary>
    [Fact]
    public void OnePickIsNamedEvenWhenItIsLongerThanTheBudget() =>
        Assert.Equal("Work on Faction",
            PickerFace.For(["Work on Faction"], "goal", "goals", maxChars: 4));

    [Fact]
    public void TickingEveryRowSaysSoByNameRatherThanCounting() =>
        Assert.Equal("All goals",
            PickerFace.For(Goals, "goal", "goals", offered: Goals.Length,
                maxChars: HelperPresentation.FaceChars));

    /// <summary>
    /// **THE GENERALISATION'S OWN FAILURE MODE.** Two picks whose names happen to be short are
    /// named; two whose names are long are counted. A rule that capped on COUNT alone would
    /// pass the first of these and ship #184 straight back on the second — forty-five
    /// characters of face where the room budgeted thirty-four.
    /// </summary>
    [Fact]
    public void TheBudgetIsAWidthAndNotOnlyACount()
    {
        Assert.Equal("Level Up · Farm Gear",
            PickerFace.For(["Level Up", "Farm Gear"], "goal", "goals",
                maxChars: HelperPresentation.FaceChars));
        Assert.Equal("3 goals",
            PickerFace.For(["Unlock Classes", "Unlock Races", "Farm Materials"],
                "goal", "goals", maxChars: HelperPresentation.FaceChars));
    }

    /// <summary>
    /// **THE PROVE-FAIL.** #184's defect was that the face grew with the selection, and the
    /// only assertion that can catch it is one over EVERY selection rather than a handful of
    /// examples. This walks all 512 subsets of the Founder's nine goals — the real labels, not
    /// a fixture — and holds the face to the budget the Helper room chose. Drop the
    /// <c>maxChars</c> check from <see cref="PickerFace.For"/> and this reddens on the 32-char
    /// pairs immediately; drop the count check too and it reddens at 49.
    /// </summary>
    [Fact]
    public void NoSelectionOfTheFoundersNineGoalsOutgrowsTheFace()
    {
        var worst = "";
        for (var mask = 0; mask < 1 << 9; mask++)
        {
            var picked = Goals.Where((_, i) => (mask & (1 << i)) != 0).ToList();
            var face = PickerFace.For(picked, "goal", "goals",
                offered: Goals.Length, maxChars: HelperPresentation.FaceChars);
            if (face.Length > worst.Length) worst = face;
        }

        Assert.True(worst.Length <= HelperPresentation.FaceChars,
            $"the widest goal face was {worst.Length} chars: \"{worst}\"");
    }

    /// <summary>
    /// A picker over a CAPPED list is never told how many it offers, so it can never claim
    /// "All factions". The Helper shows twelve standings out of a dump that carries hundreds
    /// and prints the withheld count directly under the face — a face that un-said that one
    /// control up would be worse than no cap note at all.
    /// </summary>
    [Fact]
    public void AFaceOverACappedOfferNeverClaimsAllOfThem()
    {
        var everyRowShown = Enumerable.Range(0, HelperPresentation.FactionPickerCap)
            .Select(i => $"Faction {i}").ToList();

        var face = HelperPresentation.FactionFace(everyRowShown);

        Assert.DoesNotContain("All", face);
        Assert.Equal($"{HelperPresentation.FactionPickerCap} factions", face);
    }

    /// <summary>The Helper's goal face is really wired to the store's own values — the enum,
    /// not a list of strings this test wrote down (trap 30: a hand-enumerated list stops
    /// covering the enum the day it grows).</summary>
    [Fact]
    public void TheGoalFaceNamesTheGoalsTheRoomOffers()
    {
        Assert.Equal("Any goal", HelperPresentation.GoalFace([]));
        Assert.Equal("Level Up", HelperPresentation.GoalFace([HelperGoal.LevelUp]));
        Assert.Equal("All goals", HelperPresentation.GoalFace(Recommendations.All));
    }

    /// <summary>The class picker's own rule is untouched by the move — asserted HERE as well
    /// as in <c>ClassFilterLabelTests</c>, because the migration's acceptance bar is that the
    /// generalised function reproduces it, and that claim belongs beside the generalisation.
    /// </summary>
    [Fact]
    public void TheClassFaceIsTheGeneralRuleWithClassWordsInIt()
    {
        Assert.Equal(ClassFilterLabel.For(["Bard", "Cleric", "Warrior"]),
            PickerFace.For(["Bard", "Cleric", "Warrior"], "class", "classes",
                offered: QuestClassFilter.Classes.Length,
                abbreviate: QuestClassFilter.Abbrev));
        Assert.Equal("BRD · CLR · WAR", ClassFilterLabel.For(["Bard", "Cleric", "Warrior"]));
    }
}
