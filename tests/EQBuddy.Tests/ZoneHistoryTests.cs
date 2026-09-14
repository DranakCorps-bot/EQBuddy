using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The per-zone all-time fold (DRA-70 D1, the plan's D7 decision).
///
/// <para>What these rows are really about is the SPLIT: two sources, one row, and each fact
/// taken from the only one that can answer it. Time, experience, coin and deaths come from
/// the stored session rows because the pool has no clock; kills and fight length come from
/// the pool because a session row attributes everything to its primary zone and the pool is
/// keyed on where the kill actually happened. Taking kills from both would be trap 4 — one
/// entry, two sources — disagreeing exactly when a sitting crossed a zone line, which is the
/// case nobody stages by hand and every real player produces.</para>
/// </summary>
public class ZoneHistoryTests
{
    private static SessionRow Session(
        string zone, double hours, double xp, long copper = 0, int deaths = 0,
        int kills = 0, DateTime? ended = null) =>
        new(1, "erollisi", "Dranak", DateTime.Today, ended ?? DateTime.Today.AddHours(hours),
            hours * 3600, hours * 3600, "ended", zone, kills, xp, copper, 0, deaths, 0, "", "");

    private static MobSummary Mob(string name, string zone, int kills, double fightSeconds = 0) =>
        new(name, kills, kills, fightSeconds, 0, 0, []) { Zone = zone };

    [Fact]
    public void ItSumsTimeExperienceCoinAndDeathsFromTheSessionRows()
    {
        var rolls = ZoneHistory.Fold(
            [Session("Lower Guk", 2, 12, copper: 400, deaths: 1),
             Session("Lower Guk", 1, 6, copper: 200, deaths: 2)],
            []);

        var guk = Assert.Single(rolls);
        Assert.Equal(2, guk.Sessions);
        Assert.Equal(3, guk.Hours, 3);
        Assert.Equal(18, guk.XpPercent, 3);
        Assert.Equal(600, guk.Copper);
        Assert.Equal(3, guk.Deaths);
        Assert.Equal(6, guk.XpPerHour!.Value, 3);
    }

    /// <summary>
    /// **The split, asserted as a disagreement.** The session rows say this sitting happened
    /// in Lower Guk; the pool says eleven of the kills were in Lower Guk and four were in
    /// Upper Guk, because the player walked. The fold has to believe the pool about kills and
    /// the rows about hours, which is what makes Upper Guk exist at all here with no session
    /// of its own.
    /// </summary>
    [Fact]
    public void KillsComeFromThePoolAndNotFromTheSessionRowsOwnCount()
    {
        var rolls = ZoneHistory.Fold(
            // The session row CLAIMS 15 kills, all attributed to its primary zone.
            [Session("Lower Guk", 3, 9, kills: 15)],
            [Mob("a froglok tad", "Lower Guk", 11), Mob("a froglok wizard", "Upper Guk", 4)]);

        var guk = rolls.Single(z => z.Zone == "Lower Guk");
        var upper = rolls.Single(z => z.Zone == "Upper Guk");
        Assert.Equal(11, guk.Kills);
        Assert.Equal(4, upper.Kills);
        // And Upper Guk has no clock at all, so it has no rate — a zone the pool knows and
        // no session named is real, and reporting "4 kills at ∞%/hr" for it would be the
        // whole of what is wrong with dividing by a denominator nobody measured.
        Assert.Null(upper.XpPerHour);
        Assert.False(upper.HasPersonalEvidence);
    }

    /// <summary>Four minutes containing one lucky pull is not a rate. The floor is the
    /// difference between a recommendation and noise ranked first.</summary>
    [Fact]
    public void AZoneUnderTheTimeFloorReportsNoRateAtAll()
    {
        var rolls = ZoneHistory.Fold(
            [Session("Befallen", ZoneHistory.MinHours / 2, 8)],
            [Mob("a skeleton", "Befallen", 3)]);

        var befallen = Assert.Single(rolls);
        Assert.Null(befallen.XpPerHour);
        Assert.Null(befallen.CopperPerHour);
        Assert.False(befallen.HasPersonalEvidence);

        // The negative that keeps the row above from being vacuous (trap 39): one minute more
        // than the floor and the same fold answers.
        var enough = ZoneHistory.Fold(
            [Session("Befallen", ZoneHistory.MinHours, 8)], [Mob("a skeleton", "Befallen", 3)]);
        Assert.NotNull(enough.Single().XpPerHour);
        Assert.True(enough.Single().HasPersonalEvidence);
    }

    /// <summary>Hours alone are not evidence. A player who parked in a zone for an hour and
    /// killed nothing has a rate the arithmetic can produce and nothing a recommendation
    /// should rest on.</summary>
    [Fact]
    public void TimeWithNoKillsIsNotPersonalEvidence()
    {
        var rolls = ZoneHistory.Fold([Session("Freeport", 2, 0)], []);
        var freeport = Assert.Single(rolls);
        Assert.NotNull(freeport.XpPerHour);
        Assert.Equal(0, freeport.Kills);
        Assert.False(freeport.HasPersonalEvidence);
    }

    /// <summary>Kill-WEIGHTED, so a named killed twice does not outweigh two hundred trash
    /// pulls in the number a player would recognise as "how long a fight takes here".</summary>
    [Fact]
    public void FightLengthIsWeightedByKills()
    {
        var rolls = ZoneHistory.Fold(
            [Session("Sebilis", 4, 20)],
            [Mob("a juggernaut golem", "Sebilis", 2, fightSeconds: 300),
             Mob("a froglok tad", "Sebilis", 98, fightSeconds: 20)]);

        var seb = Assert.Single(rolls);
        Assert.Equal(100, seb.Kills);
        // (2×300 + 98×20) / 100 = 25.6 — nowhere near the 160 an unweighted mean would give.
        Assert.Equal(25.6, seb.AvgFightSeconds, 1);
    }

    /// <summary>A creature the pool never timed contributes nothing to the mean rather than
    /// dragging it to zero — the "unknown is not zero" rule the pool itself applies to coin
    /// and level bounds.</summary>
    [Fact]
    public void ACreatureWithNoRecordedFightTimeDoesNotDragTheMeanDown()
    {
        var rolls = ZoneHistory.Fold(
            [Session("Unrest", 2, 8)],
            [Mob("a ghoul", "Unrest", 10, fightSeconds: 30),
             Mob("an undead knight", "Unrest", 10)]);

        Assert.Equal(30, rolls.Single().AvgFightSeconds, 1);
        Assert.Equal(20, rolls.Single().Kills);
    }

    /// <summary>"" is not a place. A row with no primary zone is skipped rather than folded
    /// into a bucket that would sort into the recommendations as a zone nobody can travel
    /// to.</summary>
    [Fact]
    public void RowsWithNoZoneAreSkippedRatherThanBucketedUnderAnEmptyName()
    {
        var rolls = ZoneHistory.Fold(
            [Session("", 3, 9), Session("   ", 3, 9)], [Mob("a rat", "", 4)]);
        Assert.Empty(rolls);
    }

    /// <summary>Best rate first, and the zones with no rate after all of them — a stable
    /// order the recommender can take the top of without sorting again.</summary>
    [Fact]
    public void ZonesAreOrderedByObservedRateWithTheUnmeasuredOnesLast()
    {
        var rolls = ZoneHistory.Fold(
            [Session("Crushbone", 2, 4), Session("Lower Guk", 2, 20), Session("Unrest", 2, 10)],
            [Mob("a bat", "Kithicor", 5)]);

        Assert.Equal(["Lower Guk", "Unrest", "Crushbone", "Kithicor"],
            rolls.Select(z => z.Zone).ToArray());
    }

    /// <summary>Zones fold case-insensitively — two sources capitalise differently and one
    /// place must not become two rows — and the newest end time survives.</summary>
    [Fact]
    public void OneZoneSpelledTwoWaysIsStillOnePlace()
    {
        var older = DateTime.Today.AddDays(-3);
        var newer = DateTime.Today.AddDays(-1);
        var rolls = ZoneHistory.Fold(
            [Session("Lower Guk", 2, 6, ended: older), Session("lower guk", 2, 6, ended: newer)],
            [Mob("a froglok tad", "LOWER GUK", 40)]);

        var guk = Assert.Single(rolls);
        Assert.Equal(2, guk.Sessions);
        Assert.Equal(40, guk.Kills);
        Assert.Equal(newer, guk.LastPlayedLocal);
    }

    /// <summary>Nulls in, empty out. The fold is called on a fresh profile before anything
    /// has been stored, and throwing there would be a room that cannot draw.</summary>
    [Fact]
    public void EmptyInputsFoldToNothing()
    {
        Assert.Empty(ZoneHistory.Fold([], []));
        Assert.Empty(ZoneHistory.Fold(null!, null!));
    }

    // ---- the conned band: what level your evidence was earned at (DRA-71 D3) ---------

    private static MobSummary Conned(
        string name, string zone, int kills, int levelMin, int levelMax) =>
        Mob(name, zone, kills) with { LevelMin = levelMin, LevelMax = levelMax };

    /// <summary>The band is the OUTER bounds across every creature conned here, and the kill
    /// count beside it is the denominator: a range that rested on one /consider and a range
    /// that rested on two hundred kills read identically without it.</summary>
    [Fact]
    public void TheBandIsTheOuterBoundsAcrossEveryConnedCreature()
    {
        var guk = Assert.Single(ZoneHistory.Fold(
            [Session("Lower Guk", 3, 18)],
            [Conned("a froglok tad", "Lower Guk", 150, 8, 10),
             Conned("a froglok wizard", "Lower Guk", 50, 11, 13)]));

        Assert.True(guk.HasConnedBand);
        Assert.Equal(8, guk.ConnedMin);
        Assert.Equal(13, guk.ConnedMax);
        Assert.Equal(200, guk.ConnedKills);
    }

    /// <summary>
    /// **A creature nobody conned contributes NOTHING — it does not drag the floor to
    /// zero.**
    ///
    /// <para>This is the same "unknown is not zero" rule the pool already applies to coin and
    /// fight length, and it is the one that would have shipped the bug: <c>LevelMin</c>
    /// deserializes as 0 for every creature killed before <c>/consider</c> was ever pressed,
    /// and a <c>Math.Min</c> that took those would put every zone's band floor at 0 the first
    /// time a player killed something without looking at it — which reads downstream as a
    /// zone whose creatures start at level zero.</para>
    /// </summary>
    [Fact]
    public void AnUnconnedCreatureDoesNotPullTheBandDownToZero()
    {
        var guk = Assert.Single(ZoneHistory.Fold(
            [Session("Lower Guk", 3, 18)],
            [Conned("a froglok tad", "Lower Guk", 150, 8, 10),
             Mob("a froglok slave", "Lower Guk", 500)]));

        Assert.Equal(8, guk.ConnedMin);
        Assert.Equal(10, guk.ConnedMax);
        // Its kills count toward the zone, but not toward what the BAND rests on — the
        // denominator has to describe the same creatures the range does.
        Assert.Equal(650, guk.Kills);
        Assert.Equal(150, guk.ConnedKills);
    }

    /// <summary>A zone where nothing was ever conned has no band at all, and says so through
    /// <c>HasConnedBand</c> rather than through a pair of zeroes a caller has to remember to
    /// check. The discount reads that property; a zero it had to interpret would be the
    /// trap-64b shape (a proxy standing in for a fact).</summary>
    [Fact]
    public void AZoneNobodyEverConnedHasNoBand()
    {
        var befallen = Assert.Single(ZoneHistory.Fold(
            [Session("Befallen", 3, 18)], [Mob("a skeleton", "Befallen", 200)]));

        Assert.False(befallen.HasConnedBand);
        Assert.Equal(0, befallen.ConnedMin);
        Assert.Equal(0, befallen.ConnedKills);
    }

    /// <summary>A creature conned exactly once carries one level in both bounds — the pool
    /// records <c>LevelMax</c> as 0 until a second reading widens it, and a band of "8 to 0"
    /// would sort and compare as nonsense everywhere downstream.</summary>
    [Fact]
    public void ASingleConsiderGivesABandOfOneLevelRatherThanOneEndedAtZero()
    {
        var guk = Assert.Single(ZoneHistory.Fold(
            [Session("Lower Guk", 3, 18)],
            [Mob("a froglok tad", "Lower Guk", 40) with { LevelMin = 8, LevelMax = 0 }]));

        Assert.True(guk.HasConnedBand);
        Assert.Equal(8, guk.ConnedMin);
        Assert.Equal(8, guk.ConnedMax);
    }

    // ---- throughput: the third source (DRA-71 D4, plan P7) -------------------------------

    /// <summary>A row whose id and active seconds can both be set — the existing
    /// <see cref="Session"/> helper pins the id at 1 and makes active equal elapsed, which is
    /// exactly what the rows below need to vary.</summary>
    private static SessionRow Row(
        long id, string zone, double hours, double xp, double activeHours = -1,
        int deaths = 0) =>
        new(id, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(hours),
            hours * 3600, (activeHours < 0 ? hours : activeHours) * 3600,
            "ended", zone, 0, xp, 0, 0, deaths, 0, "", "");

    /// <summary>
    /// **The pooled rate is combat-second weighted, not an average of averages.**
    ///
    /// <para>A four-hour sitting at 40 dps and a six-minute one at 400 do not average to 220.
    /// The stored rate is multiplied back out by its own denominator and the totals are
    /// divided once — which is the whole reason the probe carries <c>CombatSeconds</c> at all,
    /// and the reason a <c>Dps</c> column on its own could not have answered this.</para>
    /// </summary>
    [Fact]
    public void ThroughputIsPooledByCombatSecondsAndNotAveragedAcrossSessions()
    {
        var guk = Assert.Single(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 4, 20), Row(2, "Lower Guk", 0.5, 3)],
            [Mob("a froglok tad", "Lower Guk", 100, 30)],
            [new SessionThroughput(1, 40, 0, 3600), new SessionThroughput(2, 400, 0, 60)]));

        Assert.Equal(3660, guk.CombatSeconds, 3);
        Assert.Equal((40 * 3600) + (400 * 60), guk.CombatDamage, 3);
        // 168000 / 3660 ≈ 45.9 — nowhere near the 220 an unweighted mean would have given.
        Assert.Equal(45.9, guk.Dps!.Value, 1);
    }

    /// <summary>
    /// **A session with no combat seconds contributes NOTHING**, rather than a zero that
    /// would drag the pooled rate toward the floor. Unknown is not zero — the rule this fold
    /// already keeps for fight length and the conned band.
    /// </summary>
    [Fact]
    public void ASessionWithNoCombatSecondsDoesNotDragTheRateDown()
    {
        var guk = Assert.Single(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 4, 20), Row(2, "Lower Guk", 4, 20)],
            [Mob("a froglok tad", "Lower Guk", 100, 30)],
            // The second session was a bank trip: stored, real, and with nothing to divide.
            [new SessionThroughput(1, 40, 0, 3600), new SessionThroughput(2, 0, 0, 0)]));

        Assert.Equal(3600, guk.CombatSeconds, 3);
        Assert.Equal(40, guk.Dps!.Value, 3);
    }

    /// <summary>The join is by ROW ID. The probe reads its own query in its own order, so a
    /// positional join would attribute one zone's output to another the first time a player
    /// had two.</summary>
    [Fact]
    public void ThroughputJoinsBySessionIdAndNotByPosition()
    {
        var rolls = ZoneHistory.Fold(
            [Row(7, "Lower Guk", 3, 18), Row(9, "Befallen", 3, 18)],
            [Mob("a froglok tad", "Lower Guk", 50, 30), Mob("a skeleton", "Befallen", 50, 30)],
            // Deliberately in the other order from the session rows above.
            [new SessionThroughput(9, 12, 0, 600), new SessionThroughput(7, 88, 0, 600)]);

        Assert.Equal(88, rolls.Single(z => z.Zone == "Lower Guk").Dps!.Value, 3);
        Assert.Equal(12, rolls.Single(z => z.Zone == "Befallen").Dps!.Value, 3);
    }

    /// <summary>
    /// **No throughput input at all leaves every other number exactly where it was**, and
    /// answers null rather than zero. A profile whose snapshots predate the probe, and every
    /// caller that does not need throughput, must rank as they did before this slice.
    /// </summary>
    [Fact]
    public void WithoutTheProbeThroughputIsNullRatherThanZero()
    {
        var guk = Assert.Single(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 3, 18)], [Mob("a froglok tad", "Lower Guk", 50, 30)]));

        Assert.False(guk.HasThroughput);
        Assert.Null(guk.Dps);
        Assert.Null(guk.Hps);
        Assert.Null(guk.OutputPerSecond);
        // And the rate that existed before the slice is untouched.
        Assert.Equal(6, guk.XpPerHour!.Value, 3);
    }

    /// <summary>The <see cref="ZoneHistory.MinHours"/> floor governs throughput too: four
    /// minutes containing one good pull is not a measurement of anything.</summary>
    [Fact]
    public void ThroughputKeepsTheSameFloorTheExperienceRateKeeps()
    {
        var thin = Assert.Single(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 0.1, 4)], [Mob("a froglok tad", "Lower Guk", 3, 30)],
            [new SessionThroughput(1, 90, 0, 120)]));

        Assert.False(thin.HasThroughput);
        Assert.Null(thin.Dps);
        // The raw sums are still there — the refusal is the RATE, not the record.
        Assert.Equal(120, thin.CombatSeconds, 3);
    }

    /// <summary>
    /// **Healing counts toward the figure the ranking weighs.** A cleric who healed through a
    /// camp and swung at nothing measured a dps of zero and an output that is not zero, and a
    /// weight that could only see damage would mark down every zone they did their job in.
    /// </summary>
    [Fact]
    public void AHealersOutputIsNotZeroJustBecauseTheirDamageIs()
    {
        var guk = Assert.Single(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 3, 18)], [Mob("a froglok tad", "Lower Guk", 50, 30)],
            [new SessionThroughput(1, 0, 55, 1800)]));

        Assert.Equal(0, guk.Dps!.Value, 3);
        Assert.Equal(55, guk.Hps!.Value, 3);
        Assert.Equal(55, guk.OutputPerSecond!.Value, 3);
    }

    /// <summary>Downtime is the GAP between elapsed and active, and it is a share rather than
    /// a count of minutes — an hour idle in a two-hour sitting and an hour idle in a ten-hour
    /// one are the same count and not the same fact.</summary>
    [Fact]
    public void DowntimeIsTheGapBetweenElapsedAndActiveTime()
    {
        var guk = Assert.Single(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 4, 20, activeHours: 1)],
            [Mob("a froglok tad", "Lower Guk", 50, 30)]));

        Assert.Equal(1, guk.ActiveHours, 3);
        Assert.Equal(0.75, guk.DowntimeShare!.Value, 3);
    }

    /// <summary>An active figure larger than the elapsed one cannot happen from one honest
    /// snapshot, and a stored row from a build where it did must not produce a negative
    /// percentage on screen.</summary>
    [Fact]
    public void AnImpossibleActiveFigureClampsRatherThanGoingNegative()
    {
        var guk = Assert.Single(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 2, 20, activeHours: 9)],
            [Mob("a froglok tad", "Lower Guk", 50, 30)]));

        Assert.Equal(0, guk.DowntimeShare!.Value, 3);
    }

    /// <summary>Deaths are reported as a RATE as well as a count, and the floor applies —
    /// one death in twenty minutes and one in twenty hours are the same count.</summary>
    [Fact]
    public void DeathsAreAvailableAsARateAndNotOnlyAsACount()
    {
        var guk = Assert.Single(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 4, 20, deaths: 6)],
            [Mob("a froglok tad", "Lower Guk", 50, 30)]));

        Assert.Equal(6, guk.Deaths);
        Assert.Equal(1.5, guk.DeathsPerHour!.Value, 3);
    }

    /// <summary>
    /// **The instance tier needs no plumbing: it is already in the zone's own name.**
    ///
    /// <para>A session stores the zone string the game printed, verbatim, and this fold has
    /// never normalised it — so the tier is the observation the player's own log made, decoded
    /// where it already sits. An open-world zone decodes to <c>OpenWorld</c> and draws
    /// nothing.</para>
    /// </summary>
    [Fact]
    public void TheInstanceTierComesOutOfTheZoneNameTheSessionAlreadyStored()
    {
        var rolls = ZoneHistory.Fold(
            [Row(1, "Najena 4 (Refined)", 3, 18), Row(2, "Lower Guk", 3, 18)], []);

        Assert.Equal(4, rolls.Single(z => z.Zone.StartsWith("Najena")).ObservedTier);
        Assert.Equal(InstanceTier.OpenWorld, rolls.Single(z => z.Zone == "Lower Guk").ObservedTier);
    }

    // ---- the baseline --------------------------------------------------------------------

    /// <summary>
    /// **A one-zone profile has NO baseline**, and that clause is the one worth a test.
    ///
    /// <para>A baseline folded from one zone IS that zone, so any comparison against it would
    /// be a tautology — "your output here is exactly your average" true by construction, and a
    /// discount that could never fire looking like one that had been checked.</para>
    /// </summary>
    [Fact]
    public void OneMeasuredZoneIsNotAYardstickForItself()
    {
        var one = ZoneHistory.Baseline(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 3, 18)], [Mob("a froglok tad", "Lower Guk", 50, 30)],
            [new SessionThroughput(1, 40, 0, 1800)]));

        Assert.False(one.Known);
        // The arithmetic still ran — the refusal is the COMPARISON, not the sum.
        Assert.Equal(40, one.OutputPerSecond, 3);
        Assert.Equal(1, one.Zones);
    }

    /// <summary>The baseline is pooled over combat seconds, so a camp farmed for hours weighs
    /// more than one visited for minutes — the same rule a single row's own rate keeps.</summary>
    [Fact]
    public void TheBaselineIsPooledAndNotAMeanOfTheRows()
    {
        var baseline = ZoneHistory.Baseline(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 4, 20), Row(2, "Befallen", 3, 12)],
            [Mob("a froglok tad", "Lower Guk", 200, 20),
             Mob("a skeleton", "Befallen", 20, 80)],
            [new SessionThroughput(1, 100, 0, 3600), new SessionThroughput(2, 10, 0, 400)]));

        Assert.True(baseline.Known);
        Assert.Equal(2, baseline.Zones);
        // (100×3600 + 10×400) / 4000 = 91.0, not the 55 a two-row mean would give.
        Assert.Equal(91, baseline.OutputPerSecond, 1);
        // Kill-weighted fight length, same discipline: (20×200 + 80×20) / 220 ≈ 25.5.
        Assert.True(baseline.FightLengthKnown);
        Assert.Equal(25.5, baseline.AvgFightSeconds, 1);
    }

    /// <summary>
    /// **The two halves are known SEPARATELY.** A profile can have measured combat in two
    /// zones and a pooled fight length in one; dragging both down to the weaker one would
    /// throw away a comparison that was available.
    /// </summary>
    [Fact]
    public void TheOutputAndFightLengthHalvesOfTheBaselineAreKnownIndependently()
    {
        var baseline = ZoneHistory.Baseline(ZoneHistory.Fold(
            [Row(1, "Lower Guk", 4, 20), Row(2, "Befallen", 3, 12)],
            // Only Lower Guk's creatures ever recorded a fight length.
            [Mob("a froglok tad", "Lower Guk", 200, 20), Mob("a skeleton", "Befallen", 20)],
            [new SessionThroughput(1, 100, 0, 3600), new SessionThroughput(2, 10, 0, 400)]));

        Assert.True(baseline.Known);
        Assert.False(baseline.FightLengthKnown);
    }

    /// <summary>Nothing measured anywhere is <see cref="ThroughputBaseline.None"/>, and a null
    /// list is the same answer as an empty one.</summary>
    [Fact]
    public void ABaselineOverNothingIsNone()
    {
        Assert.Equal(ThroughputBaseline.None, ZoneHistory.Baseline([]));
        Assert.Equal(ThroughputBaseline.None, ZoneHistory.Baseline(null));
        Assert.False(ThroughputBaseline.None.Known);
    }
}
