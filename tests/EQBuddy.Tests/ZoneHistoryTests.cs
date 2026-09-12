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
}
