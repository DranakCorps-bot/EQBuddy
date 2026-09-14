using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE FOUR OUTCOME WEIGHTS** (DRA-71 D4, plan P7; Founder smoke item 3 — *"DPS/Healing vs
/// mob difficulty"*).
///
/// <para>The plan's reading is outcome evidence FIRST, level-delta second, and never an
/// adjective. So the rows here are about four measurements — output per combat second, fight
/// length, deaths and downtime — each re-ordering the list and each saying on the row what it
/// measured. <b>Every one is PAIRED with a prove-fail</b> (trap 34: green-only is vacuous
/// coverage), and each pair differs in exactly the one input the weight is supposed to read:
/// the fixture is otherwise held still, so an assertion cannot be agreeing with an order that
/// happened to come out that way.</para>
///
/// <para><b>And the sentences are asserted beside the order.</b> A zone marked down in
/// silence is the bug this slice could most easily have shipped — the weight is invisible and
/// the room looks fine — so every discount row also checks that its evidence reached the
/// recommendation.</para>
///
/// <para><b>Nothing here measures another player.</b> Damage and healing are the values the
/// log has always carried about this character; there is no cohort in any fixture because
/// there is nowhere for one to come from.</para>
/// </summary>
public class RecommendationsThroughputTests
{
    // ---- fixtures ------------------------------------------------------------------------

    private static SessionRow Row(
        long id, string zone, double hours, double xp, double activeHours = -1, int deaths = 0) =>
        new(id, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(hours),
            hours * 3600, (activeHours < 0 ? hours : activeHours) * 3600,
            "ended", zone, 0, xp, 0, 0, deaths, 0, "", "");

    private static MobSummary Mob(string name, string zone, int kills, double fightSeconds = 30) =>
        new(name, kills, kills, fightSeconds, 0, 0, []) { Zone = zone };

    private static HelperInputs Inputs(
        IReadOnlyList<SessionRow> sessions,
        IReadOnlyList<MobSummary> pool,
        IReadOnlyList<SessionThroughput>? throughput = null) =>
        new(ZoneHistory.Fold(sessions, pool, throughput), pool, null, [], [], [], [], false,
            [], [], null, ResolvedLevel.Unknown);

    private static Recommendation Zone(RecommendationSet set, string zone) =>
        set.Top.Single(r => r.Zone == zone);

    /// <summary>
    /// Two zones, same length of play, and <b>Lower Guk has the BETTER measured experience
    /// rate</b> — 8.8%/hr against Sebilis' 8.0. That is the only arrangement in which a
    /// discount can be shown to do anything: before this slice Lower Guk won on weight every
    /// time, so any row below in which Sebilis comes first is the weight and not the rate.
    ///
    /// <para><b>The GAP between the two rates is chosen, not incidental.</b> Sebilis sits at
    /// 8.0/8.8 ≈ 0.91 of the best rate, so ONE discount of
    /// <c>0.8</c> is enough to overtake it and no discount is enough without one. A wider gap
    /// would have needed two weights to move anything, and a row asserting an order after two
    /// changes cannot say which of them did it.</para>
    /// </summary>
    /// <param name="gukFightSeconds">Lower Guk's own fight length. Sebilis is pinned at 30,
    /// so the pooled baseline moves with this — which is the point: the comparison is against
    /// the character, not against a number of seconds.</param>
    private static HelperInputs TwoZones(
        double gukDps = 60, double sebilisDps = 60,
        double gukHps = 0, double sebilisHps = 0,
        double gukActiveHours = -1, int gukDeaths = 0,
        double gukFightSeconds = 30,
        bool withThroughput = true) => Inputs(
        [Row(1, "Lower Guk", 5, 44, gukActiveHours, gukDeaths), Row(2, "Sebilis", 5, 40)],
        [Mob("a froglok tad", "Lower Guk", 200, gukFightSeconds),
         Mob("a sebilite juggernaut", "Sebilis", 200, 30)],
        withThroughput
            ? [new SessionThroughput(1, gukDps, gukHps, 3600),
               new SessionThroughput(2, sebilisDps, sebilisHps, 3600)]
            : null);

    // ---- 1. the throughput shortfall -----------------------------------------------------

    /// <summary>
    /// **A zone where this character put out a fraction of their usual output loses its
    /// lead, and the row says what was measured.**
    ///
    /// <para>Lower Guk pays better per hour and the player did 20 damage a second there
    /// against 60 in Sebilis — a third of the pooled figure, well under
    /// <see cref="Recommendations.ThroughputShortfall"/>. So Sebilis ranks first and Lower
    /// Guk's row carries both numbers. Nothing anywhere calls either place anything.</para>
    /// </summary>
    [Fact]
    public void AZoneWhereYourOutputCollapsedRanksBelowOneWhereItDidNot()
    {
        var set = Recommendations.Rank(
            TwoZones(gukDps: 20, sebilisDps: 60), [HelperGoal.LevelUp]);

        Assert.Equal(["Sebilis", "Lower Guk"], set.Top.Select(r => r.Zone).ToArray());

        var fact = Zone(set, "Lower Guk").Why.OfType<ZoneThroughputFact>().Single();
        Assert.Equal(20, fact.Dps, 3);
        Assert.Equal(20, fact.Output, 3);
        // The baseline is the pooled figure over BOTH zones — (20 + 60) × 3600 / 7200 = 40 —
        // and it names its own scope so the sentence can say what it rests on.
        Assert.Equal(40, fact.BaselineOutput, 3);
        Assert.Equal(2, fact.Zones);
        // The measured experience rate is UNTOUCHED. The discount is a weight, never an edit
        // to the player's own evidence.
        Assert.Equal(8.8, Zone(set, "Lower Guk").Why.OfType<ZoneXpRateFact>().Single().XpPerHour, 3);
    }

    /// <summary>
    /// **THE PROVE-FAIL.** The same fixture with the same output in both zones puts Lower Guk
    /// back on top — so the row above is reading the OUTPUT rather than agreeing with an order
    /// the rate would have produced anyway. The throughput sentence is still drawn on both
    /// rows, because smoke item 3 asked to see the number and a figure that only appeared when
    /// EQBuddy was marking a zone down would read as a verdict.
    /// </summary>
    [Fact]
    public void TheSameTwoZonesWithEqualOutputRankOnTheRateAndStillReportIt()
    {
        var set = Recommendations.Rank(
            TwoZones(gukDps: 60, sebilisDps: 60), [HelperGoal.LevelUp]);

        Assert.Equal(["Lower Guk", "Sebilis"], set.Top.Select(r => r.Zone).ToArray());
        Assert.Equal(2, set.Top.Count(r => r.Why.OfType<ZoneThroughputFact>().Any()));
    }

    /// <summary>
    /// **A healer's zone is not marked down for a dps of zero.**
    ///
    /// <para>The same collapse in damage, with the healing that replaced it. The weight reads
    /// damage AND healing per combat second, so this character's output in Lower Guk is 60
    /// like everywhere else and the order is the rate's again — where a damage-only measure
    /// would have discounted every zone a cleric did their job in.</para>
    ///
    /// <para>The prove-fail is the row above it: the identical fixture WITHOUT the healing
    /// ranks the other way.</para>
    /// </summary>
    [Fact]
    public void HealingCountsSoAClericsCampIsNotDiscountedForDoingTheirJob()
    {
        var set = Recommendations.Rank(
            TwoZones(gukDps: 0, gukHps: 60, sebilisDps: 60), [HelperGoal.LevelUp]);

        Assert.Equal(["Lower Guk", "Sebilis"], set.Top.Select(r => r.Zone).ToArray());

        var fact = Zone(set, "Lower Guk").Why.OfType<ZoneThroughputFact>().Single();
        Assert.Equal(0, fact.Dps, 3);
        Assert.Equal(60, fact.Hps, 3);
        Assert.Equal(60, fact.Output, 3);
    }

    /// <summary>The damage-only reading of the same fixture, run as a unit on the roll so the
    /// claim above is about arithmetic rather than about an order: dps is zero and output is
    /// not, and it is output the weight reads.</summary>
    [Fact]
    public void TheWeightReadsOutputAndNotDamageAlone()
    {
        var guk = TwoZones(gukDps: 0, gukHps: 60).Zones.Single(z => z.Zone == "Lower Guk");

        Assert.Equal(0, guk.Dps!.Value, 3);
        Assert.Equal(60, guk.OutputPerSecond!.Value, 3);
    }

    // ---- 2. fight length, against your own average ---------------------------------------

    /// <summary>
    /// **Fights that run far longer than this character's own average price the cadence in,
    /// and the cadence line says what the average is.**
    ///
    /// <para>Lower Guk's fights run 90 seconds and Sebilis' 30, so the pooled mean is 60 and
    /// Lower Guk is at 1.5× it — <see cref="Recommendations.SlowFightRatio"/> exactly. The
    /// comparison is against the player and not against a number of seconds, because "is 90
    /// seconds a long fight" has no answer in this repo: there is no mob-HP model and no
    /// con-colour scale to answer it with.</para>
    /// </summary>
    [Fact]
    public void FightsFarLongerThanYourOwnAverageRankTheZoneDownAndNameTheAverage()
    {
        var set = Recommendations.Rank(TwoZones(gukFightSeconds: 90), [HelperGoal.LevelUp]);

        Assert.Equal(["Sebilis", "Lower Guk"], set.Top.Select(r => r.Zone).ToArray());

        var cadence = Zone(set, "Lower Guk").Why.OfType<ZoneCadenceFact>().Single();
        Assert.Equal(90, cadence.AvgFightSeconds, 3);
        Assert.Equal(60, cadence.BaselineSeconds, 3);
    }

    /// <summary>**THE PROVE-FAIL.** The same fight length in both zones makes the baseline
    /// equal to each of them, nothing is at 1.5× anything, and the rate decides — so the row
    /// above is reading the RATIO and not the seconds.</summary>
    [Fact]
    public void EquallyLongFightsInBothZonesAreNotADiscountAnywhere()
    {
        // Sebilis is pinned at 30 in the fixture, so asking for 30 makes the two equal.
        var set = Recommendations.Rank(TwoZones(gukFightSeconds: 30), [HelperGoal.LevelUp]);

        Assert.Equal(["Lower Guk", "Sebilis"], set.Top.Select(r => r.Zone).ToArray());
        // The baseline IS 30 now, so there is nothing at 1.5x it and no discount. The clause
        // the sentence draws is the presentation layer's decision, asserted there.
        Assert.Equal(30,
            Zone(set, "Lower Guk").Why.OfType<ZoneCadenceFact>().Single().BaselineSeconds, 3);
    }

    // ---- 3. deaths -----------------------------------------------------------------------

    /// <summary>
    /// **Dying here is a measured cost, and the only sentence it produces is a count.**
    ///
    /// <para>Six deaths across five hours is 1.2 an hour, over
    /// <see cref="Recommendations.DeathsPerHourCost"/>. HOME-006 permits exactly this shape —
    /// the count with its scope — and nothing else: the row does not say the zone is
    /// dangerous, and the zone with no deaths does not say it is the opposite.</para>
    /// </summary>
    [Fact]
    public void DyingOftenRanksAZoneDownAndDrawsOnlyACount()
    {
        var set = Recommendations.Rank(TwoZones(gukDeaths: 6), [HelperGoal.LevelUp]);

        Assert.Equal(["Sebilis", "Lower Guk"], set.Top.Select(r => r.Zone).ToArray());

        var deaths = Zone(set, "Lower Guk").Why.OfType<ZoneDeathsFact>().Single();
        Assert.Equal(6, deaths.Deaths);
        Assert.Empty(Zone(set, "Sebilis").Why.OfType<ZoneDeathsFact>());
    }

    /// <summary>**THE PROVE-FAIL.** One death across the same five hours is 0.2 an hour, under
    /// the threshold: the sentence is still drawn, because it happened, and the order is the
    /// rate's. So the discount is reading the RATE and the sentence is reading the EVENT — two
    /// different questions, and conflating them would either hide a death or mark a zone down
    /// for one.</summary>
    [Fact]
    public void OneDeathIsReportedWithoutRankingTheZoneDown()
    {
        var set = Recommendations.Rank(TwoZones(gukDeaths: 1), [HelperGoal.LevelUp]);

        Assert.Equal(["Lower Guk", "Sebilis"], set.Top.Select(r => r.Zone).ToArray());
        Assert.Single(Zone(set, "Lower Guk").Why.OfType<ZoneDeathsFact>());
    }

    // ---- 4. downtime ---------------------------------------------------------------------

    /// <summary>
    /// **Time with nothing happening in it is priced, and the sentence names no cause.**
    ///
    /// <para>One active hour in five is 80% downtime, over
    /// <see cref="Recommendations.DowntimeShareCost"/>. The fact carries the share and its
    /// scope and nothing else: the log recorded that nothing was happening, not why, and a
    /// sentence naming medding or travel would be inventing the half it did not record
    /// (trap 73).</para>
    /// </summary>
    [Fact]
    public void HeavyDowntimeRanksAZoneDownAndReportsTheShare()
    {
        var set = Recommendations.Rank(TwoZones(gukActiveHours: 1), [HelperGoal.LevelUp]);

        Assert.Equal(["Sebilis", "Lower Guk"], set.Top.Select(r => r.Zone).ToArray());

        var downtime = Zone(set, "Lower Guk").Why.OfType<ZoneDowntimeFact>().Single();
        Assert.Equal(0.8, downtime.Share, 3);
        Assert.Equal(1, downtime.Sessions);
    }

    /// <summary>**THE PROVE-FAIL, and the silence with it.** A sitting that was active
    /// throughout draws NO downtime line at all and takes no discount. "12% of your time here
    /// had nothing happening in it" is true of every camp and would be furniture; the line
    /// exists to mark an outlier.</summary>
    [Fact]
    public void ASittingThatWasActiveThroughoutSaysNothingAboutDowntime()
    {
        var set = Recommendations.Rank(TwoZones(), [HelperGoal.LevelUp]);

        Assert.Equal(["Lower Guk", "Sebilis"], set.Top.Select(r => r.Zone).ToArray());
        Assert.Empty(set.Top.SelectMany(r => r.Why).OfType<ZoneDowntimeFact>());
    }

    // ---- 5. the absences ------------------------------------------------------------------

    /// <summary>
    /// **A profile whose snapshots carry no throughput ranks EXACTLY as it did before this
    /// slice**, and draws none of the new sentences.
    ///
    /// <para>This is the row that keeps a gap in EQBuddy's own reading from being scored as a
    /// bad result. An absent measurement is not a poor one — the rule the conned band already
    /// keeps (trap 73) — so a player upgrading into this build does not watch their best camp
    /// drop for a reason nothing on screen could explain.</para>
    /// </summary>
    [Fact]
    public void WithoutTheThroughputProbeNothingIsDiscountedAndNothingIsSaid()
    {
        var set = Recommendations.Rank(TwoZones(withThroughput: false), [HelperGoal.LevelUp]);

        Assert.Equal(["Lower Guk", "Sebilis"], set.Top.Select(r => r.Zone).ToArray());
        Assert.Empty(set.Top.SelectMany(r => r.Why).OfType<ZoneThroughputFact>());
        // The weight is the rate's alone: the best zone is 1.0 exactly, undiscounted.
        Assert.Equal(1, Zone(set, "Lower Guk").Weight, 4);
    }

    /// <summary>
    /// **A single measured zone gets its output reported and NO comparison**, because a
    /// baseline folded from one zone is that zone. A tautology printed as a finding would be
    /// worse than silence, and a discount that could never fire would look like one that had
    /// been checked.
    /// </summary>
    [Fact]
    public void OneMeasuredZoneReportsItsOutputAndComparesItWithNothing()
    {
        var set = Recommendations.Rank(Inputs(
            [Row(1, "Lower Guk", 5, 60)],
            [Mob("a froglok tad", "Lower Guk", 200)],
            [new SessionThroughput(1, 42, 0, 3600)]), [HelperGoal.LevelUp]);

        var fact = Assert.Single(set.Top).Why.OfType<ZoneThroughputFact>().Single();
        Assert.Equal(42, fact.Dps, 3);
        Assert.Equal(0, fact.BaselineOutput, 3);
        Assert.Equal(0, fact.Zones);
        Assert.Equal(1, Assert.Single(set.Top).Weight, 4);
    }

    // ---- 6. the instance tier -------------------------------------------------------------

    /// <summary>
    /// **The tier the player's own zone line recorded is reported, and weighs nothing.**
    ///
    /// <para>Both zones are measured identically; only one of them was an instance. The tiered
    /// one carries the fact and the weights come out equal, because the tier PREFERENCE is the
    /// mote engine's and belongs to its own slice (plan P10). A ranking rule added here would
    /// be this slice deciding something nobody has signed.</para>
    /// </summary>
    [Fact]
    public void AnInstanceTierIsReportedAsEvidenceAndChangesNoWeight()
    {
        var set = Recommendations.Rank(Inputs(
            [Row(1, "Najena 4 (Refined)", 5, 60), Row(2, "Najena", 5, 60)],
            [Mob("a bloodthirsty gnoll", "Najena 4 (Refined)", 200),
             Mob("a bloodthirsty gnoll", "Najena", 200)],
            [new SessionThroughput(1, 60, 0, 3600), new SessionThroughput(2, 60, 0, 3600)]),
            [HelperGoal.LevelUp]);

        var tiered = Zone(set, "Najena 4 (Refined)");
        Assert.Equal(4, tiered.Why.OfType<ZoneTierFact>().Single().Tier);
        Assert.Empty(Zone(set, "Najena").Why.OfType<ZoneTierFact>());
        Assert.Equal(Zone(set, "Najena").Weight, tiered.Weight, 4);
    }

    /// <summary>An instance whose adjective this build does not recognise is unmistakably an
    /// instance and has NO tier, so it gets no fact rather than a guessed D0 — the refusal
    /// <c>InstanceTier</c> itself makes, carried up to the surface.</summary>
    [Fact]
    public void AnUnrecognisedInstanceAdjectiveDrawsNoTierRatherThanGuessingD0()
    {
        var set = Recommendations.Rank(Inputs(
            [Row(1, "Najena 4 (Tempered)", 5, 60)],
            [Mob("a bloodthirsty gnoll", "Najena 4 (Tempered)", 200)],
            [new SessionThroughput(1, 60, 0, 3600)]), [HelperGoal.LevelUp]);

        // It IS an instance — the fold read the line and said so — and it has no tier.
        var roll = Assert.Single(set.Top);
        Assert.Empty(roll.Why.OfType<ZoneTierFact>());
        Assert.Equal(InstanceTier.UnknownAdjective,
            InstanceTier.FromZoneName("Najena 4 (Tempered)"));
        Assert.True(InstanceTier.IsInstance(InstanceTier.FromZoneName("Najena 4 (Tempered)")));
    }

    // ---- 7. the discounts compound, and never filter ---------------------------------------

    /// <summary>
    /// **Three measurements about one zone are three discounts, and the zone is still in the
    /// list.**
    ///
    /// <para>Lower Guk dies, drags and idles. The weight is
    /// <c>0.8³ = 0.512</c> of the rate's own share — and the row keeps its place, its measured
    /// rate and one sentence per discount that fired. <see cref="Recommendation.Weight"/> is a
    /// tie-break inside a kind; nothing here removes an answer the player's own play
    /// earned.</para>
    /// </summary>
    [Fact]
    public void ThreeMeasuredCostsCompoundAndTheZoneStaysInTheList()
    {
        var set = Recommendations.Rank(
            TwoZones(gukActiveHours: 1, gukDeaths: 6, gukFightSeconds: 90),
            [HelperGoal.LevelUp]);

        var guk = Zone(set, "Lower Guk");
        Assert.Equal(2, set.Top.Count);
        Assert.Equal(
            Recommendations.DeathsCostWeight * Recommendations.DowntimeCostWeight
            * Recommendations.SlowFightWeight,
            guk.Weight, 4);
        // One sentence per discount that fired, all of them the player's own evidence.
        Assert.Single(guk.Why.OfType<ZoneDeathsFact>());
        Assert.Single(guk.Why.OfType<ZoneDowntimeFact>());
        Assert.Single(guk.Why.OfType<ZoneCadenceFact>());
        Assert.All(guk.Why, w => Assert.Equal(Evidence.Personal, w.Evidence));
    }

    /// <summary>
    /// **Every discount that fires has a sentence on the same row — asserted as a rule rather
    /// than one case at a time.**
    ///
    /// <para>A zone marked down in silence is the shape this slice could most easily have
    /// shipped: the ordering changes, nothing on screen explains it, and every store-side
    /// assertion in the repo passes. So for each weight below 1, the fact its threshold reads
    /// has to be present.</para>
    /// </summary>
    [Fact]
    public void NoZoneIsEverMarkedDownWithoutASentenceSayingWhat()
    {
        var set = Recommendations.Rank(
            TwoZones(gukDps: 10, gukActiveHours: 1, gukDeaths: 6, gukFightSeconds: 120),
            [HelperGoal.LevelUp]);
        var guk = Zone(set, "Lower Guk");
        var roll = TwoZones(gukDps: 10, gukActiveHours: 1, gukDeaths: 6, gukFightSeconds: 120)
            .Zones.Single(z => z.Zone == "Lower Guk");

        Assert.True(guk.Weight < 1, "the fixture did not discount anything, so this row is vacuous");
        Assert.True(roll.DeathsPerHour >= Recommendations.DeathsPerHourCost);
        Assert.Single(guk.Why.OfType<ZoneDeathsFact>());
        Assert.True(roll.DowntimeShare >= Recommendations.DowntimeShareCost);
        Assert.Single(guk.Why.OfType<ZoneDowntimeFact>());
        Assert.Single(guk.Why.OfType<ZoneThroughputFact>());
        Assert.Single(guk.Why.OfType<ZoneCadenceFact>());
        // And every one of them renders — a fact with no sentence is a blank line under a
        // headline, which HelperMustListTests sweeps and this row confirms for the four.
        Assert.All(guk.Why, w => Assert.NotEmpty(HelperPresentation.Why(w)));
    }

    /// <summary>
    /// **The four weights can only ever push a zone DOWN.**
    ///
    /// <para>There is no bonus arm anywhere, so nothing in this slice can promote a camp the
    /// player's experience rate did not already earn — the rate stays the primary term. A
    /// zone with nothing measured against it comes out at exactly the share of the best rate
    /// it had before D4.</para>
    /// </summary>
    [Fact]
    public void NothingHereCanPromoteAZoneAboveWhatItsRateEarned()
    {
        // Sebilis is faster, undiscounted, and the best rate — so it is 1.0 exactly. Lower Guk
        // is two thirds of its rate and takes no discount, so it is 2/3 exactly and not more.
        var set = Recommendations.Rank(Inputs(
            [Row(1, "Sebilis", 5, 60), Row(2, "Lower Guk", 5, 40)],
            [Mob("a sebilite juggernaut", "Sebilis", 200),
             Mob("a froglok tad", "Lower Guk", 200)],
            [new SessionThroughput(1, 60, 0, 3600), new SessionThroughput(2, 60, 0, 3600)]),
            [HelperGoal.LevelUp]);

        Assert.Equal(1, Zone(set, "Sebilis").Weight, 4);
        Assert.Equal(2.0 / 3, Zone(set, "Lower Guk").Weight, 4);
    }

    // ---- 8. the per-row cap, which this slice had to raise ---------------------------------

    /// <summary>
    /// **THE CAP TAKES THE FACT THAT WEIGHS NOTHING, AND SAYS IT DID.**
    ///
    /// <para>This is the row that caught the one real regression in the slice, on this
    /// branch and before it left it. At
    /// <c>WhyCap = 4</c> a fully loaded zone — rate, throughput, cadence, deaths, downtime,
    /// outgrown band, instance tier — kept the first four in emit order and silently dropped
    /// the P6 outgrown sentence the PREVIOUS slice shipped. A zone marked down twice, drawing
    /// the explanation for one of them, with every store-side assertion in the repo passing.
    /// Trimming a caveat to make room for a number is the worst way for a cap to behave.</para>
    ///
    /// <para>So the cap is six and the tier is emitted LAST, because it is the only fact here
    /// that changes no weight. The seventh line is the one held back, and the row reports the
    /// count rather than stopping quietly (trap 50).</para>
    /// </summary>
    [Fact]
    public void AFullyLoadedRowKeepsEveryDiscountsSentenceAndWithholdsOnlyTheTier()
    {
        var set = Recommendations.Rank(new HelperInputs(
            ZoneHistory.Fold(
                [Row(1, "Najena 4 (Refined)", 5, 44, activeHours: 1, deaths: 6)],
                [Mob("a bloodthirsty gnoll", "Najena 4 (Refined)", 200, 90)
                    with { LevelMin = 8, LevelMax = 12 }],
                [new SessionThroughput(1, 20, 0, 3600)]),
            [], null, [], [], [], [], false, [], [], null,
            new ResolvedLevel(50, LevelSource.Observed, new DateTime(2026, 9, 12, 20, 0, 0))),
            [HelperGoal.LevelUp]);

        var row = Assert.Single(set.Top);

        // Six drawn, one held back, and the row says so.
        Assert.Equal(Recommendations.WhyCap, row.Why.Count);
        Assert.Equal(1, row.WithheldWhy);
        Assert.Equal("1 more reason not shown.", HelperPresentation.WithheldWhy(row.WithheldWhy));

        // Every fact that explains a weight survived — including D3's, which is the one that
        // was being dropped.
        Assert.Single(row.Why.OfType<ZoneXpRateFact>());
        Assert.Single(row.Why.OfType<ZoneThroughputFact>());
        Assert.Single(row.Why.OfType<ZoneCadenceFact>());
        Assert.Single(row.Why.OfType<ZoneDeathsFact>());
        Assert.Single(row.Why.OfType<ZoneDowntimeFact>());
        Assert.Single(row.Why.OfType<ZoneOutgrownFact>());

        // And the one that weighs nothing is the one that went.
        Assert.Empty(row.Why.OfType<ZoneTierFact>());

        // Nothing drawn is blank — a fact with no sentence is a blank line under a headline.
        Assert.All(row.Why, w => Assert.NotEmpty(HelperPresentation.Why(w)));
    }

    /// <summary>
    /// **The prove-fail for the cap's ORDER**: with no tier to spend the seventh slot on, the
    /// row fits in six and withholds nothing. So the row above is reading the cap rather than
    /// agreeing with a list that happened to be short — and the tier's position at the end of
    /// the emit order is doing the work the comment claims for it.
    /// </summary>
    [Fact]
    public void TheSameLoadedRowInAnOpenWorldZoneFitsAndWithholdsNothing()
    {
        var set = Recommendations.Rank(new HelperInputs(
            ZoneHistory.Fold(
                [Row(1, "Lower Guk", 5, 44, activeHours: 1, deaths: 6)],
                [Mob("a froglok tad", "Lower Guk", 200, 90) with { LevelMin = 8, LevelMax = 12 }],
                [new SessionThroughput(1, 20, 0, 3600)]),
            [], null, [], [], [], [], false, [], [], null,
            new ResolvedLevel(50, LevelSource.Observed, new DateTime(2026, 9, 12, 20, 0, 0))),
            [HelperGoal.LevelUp]);

        var row = Assert.Single(set.Top);
        Assert.Equal(6, row.Why.Count);
        Assert.Equal(0, row.WithheldWhy);
        Assert.Empty(HelperPresentation.WithheldWhy(row.WithheldWhy));
        Assert.Single(row.Why.OfType<ZoneOutgrownFact>());
    }
}
