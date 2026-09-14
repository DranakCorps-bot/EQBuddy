using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE MAKE MONEY ENGINE** (DRA-71 D7, Fable plan P9; Founder smoke item 4c).
///
/// <para>The sell half — the <see cref="GearIntent.FarmToSell"/> intent, which shares this
/// engine's <c>Sellables</c> producer — is covered in <c>RecommendationsGearTests</c>, beside
/// the two gear questions it sits next to on screen. This file covers what Make Money adds: the
/// coin rate your own sessions earned, and the two silences under it.</para>
/// </summary>
public class RecommendationsMoneyTests
{
    private static MobSummary Mob(
        string name, string zone, int kills, params (string Item, int Count)[] loot) =>
        new(name, kills, kills, 30, 0, 0,
            [.. loot.Select(l => new MobLoot(l.Item, l.Count, null))])
        { Zone = zone };

    private static SessionRow Session(string zone, double hours, long copper, long id = 1) =>
        new(id, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(hours),
            hours * 3600, hours * 3600, "ended", zone, 0, 10, copper, 0, 0, 0, "", "");

    /// <summary>
    /// A fixture for the money engine.
    ///
    /// <para><b>A pool is synthesized for every session zone that was not given one</b>, and
    /// that is not convenience: <see cref="ZoneRoll.HasPersonalEvidence"/> wants hours AND a
    /// kill, so a zone with coin and no pooled creature answers nothing. That is the engine's
    /// rule rather than this helper's — see
    /// <see cref="CoinWithNoKillsInTheZoneIsNotACamp"/>, which asserts it directly — and
    /// without the synthesis every fixture here would be silently exercising the rule instead
    /// of the feature.</para>
    /// </summary>
    private static HelperInputs Inputs(
        IReadOnlyList<SessionRow> sessions,
        IReadOnlyList<MobSummary>? pool = null,
        IReadOnlyList<SaleRoll>? sales = null)
    {
        var mobs = pool ?? [.. sessions
            .Select(s => s.PrimaryZone)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(z => Mob("a froglok tad", z, 200))];
        return new HelperInputs(
            ZoneHistory.Fold(sessions, mobs), mobs, null, [], [], [], [],
            false, [], [], null, ResolvedLevel.Unknown)
        {
            Sales = sales ?? [],
        };
    }

    private static RecommendationSet Rank(HelperInputs inputs) =>
        Recommendations.Rank(inputs, [HelperGoal.MakeMoney]);

    /// <summary>The zone your sessions earned the most coin per hour in, with the scope the
    /// division rests on. The sentence prints through <see cref="StatsSnapshot.FormatCoin"/> —
    /// the one formatter — so a money answer reads the way every other coin in the app
    /// does.</summary>
    [Fact]
    public void TheZoneThatPaidBestPerHourRanksFirstAndNamesItsScope()
    {
        var set = Rank(Inputs(
            [Session("Lower Guk", 5, 50_000), Session("Crushbone", 5, 5_000, id: 2)]));

        Assert.Equal(["Lower Guk", "Crushbone"], set.Top.Select(r => r.Zone));

        var fact = Assert.Single(set.Top[0].Why.OfType<ZoneCoinRateFact>());
        Assert.Equal(10_000, fact.CopperPerHour, 3);
        Assert.Equal(1, fact.Sessions);
        Assert.Equal(Evidence.Personal, fact.Evidence);

        var sentence = HelperPresentation.Why(fact);
        Assert.Contains("10p", sentence);
        Assert.Contains("1 stored session", sentence);
        Assert.DoesNotContain(HelperPresentation.CatalogLabel, sentence);
    }

    /// <summary>A money row names what you loot there and what you were paid for it — the SAME
    /// producer the "farm to sell" intent reads, so the two questions cannot grow two answers
    /// about one zone (trap 4).</summary>
    [Fact]
    public void AMoneyRowAlsoNamesWhatYouLootThereAndWhatYouWerePaid()
    {
        var top = Assert.Single(Rank(Inputs(
            [Session("Lower Guk", 5, 50_000)],
            pool: [Mob("a froglok tad", "Lower Guk", 340, ("Froglok Blood", 12))],
            sales: [new SaleRoll("Froglok Blood", 4, 320)])).Top);

        Assert.Single(top.Why.OfType<ZoneCoinRateFact>());
        var drop = Assert.Single(top.Why.OfType<SellableDropFact>());
        Assert.Equal("Froglok Blood", drop.Item);
        Assert.Equal(80, drop.CopperEach);
    }

    /// <summary>
    /// **MOTES ARE NEVER OFFERED AS SOMETHING TO SELL.**
    ///
    /// <para>They are the other engine's answer and they are not vendor goods; naming one here
    /// would be one item with two homes, and a recommendation to sell the thing the player is
    /// being told to farm.</para>
    /// </summary>
    [Fact]
    public void AMoteIsNeverNamedAsSomethingToSell()
    {
        var top = Assert.Single(Rank(Inputs(
            [Session("Lower Guk", 5, 50_000)],
            pool: [Mob("a froglok tad", "Lower Guk", 340, ("Mote of Major Potential", 12))],
            sales: [new SaleRoll("Mote of Major Potential", 4, 4000)])).Top);

        Assert.Empty(top.Why.OfType<SellableDropFact>());
    }

    /// <summary>
    /// **THE SELLABLES CAP IS TWO, AND THE REASON IS THE WHY-CAP.**
    ///
    /// <para>A money row already spends a line on its coin rate; three named items would push a
    /// loaded row past <see cref="Recommendations.WhyCap"/> and start trimming the sentences a
    /// zone was marked down for — the failure D4 had to raise the cap over. The most valuable
    /// are the ones kept, which is arithmetic over two measurements.</para>
    /// </summary>
    [Fact]
    public void AtMostTwoSellablesAreNamedAndTheyAreTheValuableOnes()
    {
        var top = Assert.Single(Rank(Inputs(
            [Session("Lower Guk", 5, 50_000)],
            pool:
            [
                Mob("a froglok tad", "Lower Guk", 340,
                    ("Bone Chips", 50), ("Froglok Blood", 12), ("Rusty Axe", 4)),
            ],
            sales:
            [
                new SaleRoll("Bone Chips", 10, 100),        // 10c each × 50 = 500
                new SaleRoll("Froglok Blood", 4, 320),      // 80c each × 12 = 960
                new SaleRoll("Rusty Axe", 2, 1000),         // 500c each × 4 = 2000
            ])).Top);

        var named = top.Why.OfType<SellableDropFact>().Select(f => f.Item).ToList();
        Assert.Equal(Recommendations.SellablesPerRow, named.Count);
        Assert.Equal(["Rusty Axe", "Froglok Blood"], named);
    }

    // ---- the silences -------------------------------------------------------------------

    /// <summary>Stored play that earned no coin is its own sentence — a measured zero, and not
    /// the "EQBuddy has read nothing" state a fresh profile gets.</summary>
    [Fact]
    public void StoredPlayThatEarnedNoCoinIsItsOwnGap()
    {
        var set = Rank(Inputs([Session("Crushbone", 5, 0)]));

        Assert.Empty(set.Top);
        var gap = Assert.Single(set.Gaps);
        Assert.Equal(GoalGapReason.NoCoinEarned, gap.Reason);
        Assert.NotEmpty(HelperPresentation.Gap(gap));

        Assert.Equal(GoalGapReason.NoPlayHistory,
            Assert.Single(Rank(HelperInputs.Nothing).Gaps).Reason);
    }

    /// <summary>Under <see cref="ZoneHistory.MinHours"/> there is no rate and so no row — the
    /// same floor the experience rate keeps, and for the same reason: ten minutes containing one
    /// good corpse is not a camp.</summary>
    [Fact]
    public void UnderTheHoursFloorThereIsNoMoneyAnswer()
    {
        var set = Rank(Inputs([Session("Lower Guk", 0.1, 50_000)]));

        Assert.Empty(set.Top);
        Assert.Equal(GoalGapReason.NoPlayHistory, Assert.Single(set.Gaps).Reason);
    }

    /// <summary>
    /// **COIN IN A ZONE YOU HAVE KILLED NOTHING IN IS NOT A CAMP.**
    ///
    /// <para>The engine sits behind <see cref="ZoneRoll.HasPersonalEvidence"/>, which wants
    /// hours AND a kill — an hour in a bank that ended with more coin than it started with is
    /// not somewhere to recommend farming. It is the same gate the experience engine uses, and
    /// it is asserted here rather than left implicit because every other fixture in this file
    /// has to work around it.</para>
    /// </summary>
    [Fact]
    public void CoinWithNoKillsInTheZoneIsNotACamp()
    {
        var set = Rank(Inputs([Session("Freeport", 5, 50_000)], pool: []));

        Assert.Empty(set.Top);
        Assert.Equal(GoalGapReason.NoPlayHistory, Assert.Single(set.Gaps).Reason);
    }

    // ---- the level exemption, from the engine's side --------------------------------------

    /// <summary>
    /// **MAKE MONEY IS EXEMPT FROM THE LEVEL, AND THE REASON IS A CLAIM ABOUT THE GAME.**
    ///
    /// <para><c>HelperMustListTests</c> proves the exemption behaviourally over the shared
    /// fixture. This asserts the reason exists and says what it says, because an exemption whose
    /// reason drifted into something else is as wrong as one that went missing — and the reason
    /// here is a real claim (coin is a property of the creature) that somebody should be able to
    /// disagree with in one place.</para>
    /// </summary>
    [Fact]
    public void TheMoneyExemptionOwesItsReasonAndTheReasonIsAboutTheCreature()
    {
        Assert.Equal(Recommendations.LevelUse.Exempt,
            Recommendations.LevelUseFor(HelperGoal.MakeMoney));

        var reason = Recommendations.LevelExemptReason(HelperGoal.MakeMoney);
        Assert.Contains("creature", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("outgrown", reason, StringComparison.OrdinalIgnoreCase);
    }
}
