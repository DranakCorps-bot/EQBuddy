using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The must-list over <see cref="UnlockNeed"/> — trap 34.**
///
/// <para>Every other test in <see cref="UnlockGuidanceTests"/> is a negative or a positive
/// about a criterion kind that EXISTS: "a Task with no catalog match gains nothing", "an
/// Obtain row the checklist knows counts its pieces". Not one of them can see a MEMBER
/// NOBODY DECIDED ABOUT. The dump's line shapes are the game's, so a new one arriving is a
/// normal Tuesday — and the first thing anyone does is add a member to
/// <see cref="UnlockNeed"/> and classify the text. Without this file that member reaches the
/// Unlocks tab with no guidance shape, every existing test stays green, and the feature
/// silently does not cover it.</para>
///
/// <para><b>Two halves, and both are load-bearing.</b>
/// <see cref="UnlockGuidance.ShapeFor"/> returns null for anything undecided rather than
/// defaulting to <see cref="UnlockGuidanceShape.None"/> — "no guidance" and "no decision"
/// are different answers and a default arm would have collapsed them. And the CURATED table
/// below has to name every member itself, so a new one fails here even if somebody adds a
/// switch arm for it without deciding what the row should say.</para>
///
/// <para>Prove-failed before it went green (CLAUDE.md: green-only is vacuous coverage): a
/// throwaway <c>UnlockNeed.Placeholder</c> member failed both facts —
/// <c>EveryKindOfCriterionHasADecidedGuidanceShape</c> on the null, and
/// <c>TheMustListNamesEveryKindTheDumpCanProduce</c> on the missing row.</para>
/// </summary>
public class UnlockGuidanceMustListTests
{
    /// <summary>What each kind of criterion is DECIDED to offer, written out rather than
    /// derived from the code under test — a must-list that asked the implementation what it
    /// covers would agree with itself forever.</summary>
    private static readonly Dictionary<UnlockNeed, UnlockGuidanceShape> MustList = new()
    {
        // "Get maximum faction with X" — the faction dump says the distance, the player's
        // own pooled kills say the rate, eqlwiki holds what we will not harvest yet.
        [UnlockNeed.MaxFaction] = UnlockGuidanceShape.FactionGrind,
        // "Obtain X" — a Plane of Sky reward the app already guides, piece count and door.
        [UnlockNeed.Obtain] = UnlockGuidanceShape.SkyPieces,
        // "Complete the 'X' Task" — a door on an exact catalog match, silence otherwise.
        [UnlockNeed.Task] = UnlockGuidanceShape.CatalogQuest,
        // Neither of these is work, so neither gets guidance. DECIDED, not defaulted: that
        // is the whole distinction this file exists to keep.
        [UnlockNeed.Derived] = UnlockGuidanceShape.None,
        [UnlockNeed.Bypass] = UnlockGuidanceShape.None,
    };

    [Fact]
    public void EveryKindOfCriterionHasADecidedGuidanceShape()
    {
        foreach (var need in Enum.GetValues<UnlockNeed>())
            Assert.True(UnlockGuidance.ShapeFor(need) is not null,
                $"UnlockNeed.{need} has no decided guidance shape. Add an arm to "
                + "UnlockGuidance.ShapeFor and a row to this file's MustList — and if the "
                + "answer is 'nothing', say so with UnlockGuidanceShape.None rather than "
                + "leaving it to the default.");
    }

    [Fact]
    public void TheMustListNamesEveryKindTheDumpCanProduce()
    {
        foreach (var need in Enum.GetValues<UnlockNeed>())
            Assert.True(MustList.ContainsKey(need),
                $"UnlockNeed.{need} is not in this file's MustList — somebody added a "
                + "criterion kind without deciding what the Unlocks tab should offer for it.");
        // The other direction, so a member that is DELETED does not leave a row here
        // asserting a contract for a shape nothing produces.
        foreach (var need in MustList.Keys)
            Assert.Contains(need, Enum.GetValues<UnlockNeed>());
    }

    [Fact]
    public void TheImplementationAgreesWithTheMustList()
    {
        foreach (var (need, shape) in MustList)
            Assert.Equal(shape, UnlockGuidance.ShapeFor(need));
    }

    /// <summary>And every decided shape is actually REACHED by the resolver — a shape in the
    /// table that no input can produce would be a promise with no code behind it.</summary>
    [Fact]
    public void EveryShapeInTheTableIsOneTheResolverCanReturn()
    {
        var reached = MustList.Values.Distinct().ToList();
        Assert.Contains(UnlockGuidanceShape.FactionGrind, reached);
        Assert.Contains(UnlockGuidanceShape.SkyPieces, reached);
        Assert.Contains(UnlockGuidanceShape.CatalogQuest, reached);
        Assert.Contains(UnlockGuidanceShape.None, reached);
        Assert.Equal(Enum.GetValues<UnlockGuidanceShape>().Length, reached.Count);
    }
}
