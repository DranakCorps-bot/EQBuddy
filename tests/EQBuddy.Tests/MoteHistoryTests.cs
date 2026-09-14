using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE MOTE FOLD** (DRA-71 D7, plan P10; the fold #580 filed for D7).
///
/// <para>It joins two things that were already on disk — the pooled creatures and the zone
/// rollup — and adds exactly one fact: what a place has paid this character in motes. The
/// tests below are about the joins and the silences: which source each number comes from, and
/// the two floors under which the fold answers nothing at all.</para>
/// </summary>
public class MoteHistoryTests
{
    private static MobSummary Mob(
        string name, string zone, int kills, params (string Item, int Count)[] loot) =>
        new(name, kills, kills, 30, 0, 0,
            [.. loot.Select(l => new MobLoot(l.Item, l.Count, null))])
        { Zone = zone };

    private static SessionRow Session(string zone, double hours, long copper = 0) =>
        new(1, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(hours),
            hours * 3600, hours * 3600, "ended", zone, 0, 10, copper, 0, 0, 0, "", "");

    private static IReadOnlyList<MoteRoll> Fold(
        IReadOnlyList<MobSummary> pool, IReadOnlyList<SessionRow> sessions) =>
        MoteHistory.Fold(pool, ZoneHistory.Fold(sessions, pool));

    // ---- the potency ladder is the one producer of "what is a mote worth" ----------------

    /// <summary>Potency comes from <see cref="Motes.PotencyOf"/> and nowhere else, so the
    /// wiki's own 1/1/2/4/5/6/7/8/9/10 ladder decides the rate rather than a count. Six Major
    /// motes at 5 each is 30 over 5 hours — and a hundred Infinitesimals would be worth less,
    /// which is the whole reason the fold weighs rather than counts (#154).</summary>
    [Fact]
    public void PotencyComesFromTheLadderAndTheRateDividesByTheSessionHours()
    {
        var roll = Assert.Single(Fold(
            [Mob("a froglok tad", "Lower Guk", 200, ("Mote of Major Potential", 6))],
            [Session("Lower Guk", 5)]));

        Assert.Equal("Lower Guk", roll.Zone);
        Assert.Equal(6, roll.Motes);
        Assert.Equal(30, roll.Potency);
        Assert.Equal(6.0, roll.PotencyPerHour!.Value, 3);
        Assert.Equal(1.2, roll.MotesPerHour!.Value, 3);
        Assert.Equal(40.0, roll.KillsPerHour!.Value, 3);
    }

    /// <summary>An ordinary item is not a mote. The fold reads
    /// <see cref="Motes.IsMote"/>, which means a named mote like Crystallized Fire Mote stays
    /// ordinary loot — the distinction <c>Motes</c> has drawn since it was written.</summary>
    [Fact]
    public void OnlyThePotentialLadderCounts()
    {
        Assert.Empty(Fold(
            [Mob("a froglok tad", "Lower Guk", 200,
                ("Crystallized Fire Mote", 40), ("Froglok Blood", 12))],
            [Session("Lower Guk", 5)]));
    }

    /// <summary>
    /// **THE VOID-TOUCHED MOTE COUNTS AND WEIGHS NOTHING, AND THE FOLD SAYS BOTH.**
    ///
    /// <para>The ladder gives it no experience — its worth is a whole item tier, which the wiki
    /// publishes no number for — so a zone whose only motes were Void-Touched has a potency of
    /// zero. That is correct arithmetic and a terrible sentence on its own, which is why the
    /// count rides its own field for the presentation layer to name.</para>
    /// </summary>
    [Fact]
    public void TheVoidTouchedMoteIsCountedSeparatelyBecauseItWeighsNothing()
    {
        var roll = Assert.Single(Fold(
            [Mob("a sky defender", "Plane of Sky", 120, (Motes.VoidTouched, 3))],
            [Session("Plane of Sky", 5)]));

        Assert.Equal(3, roll.Motes);
        Assert.Equal(3, roll.VoidTouched);
        Assert.Equal(0, roll.Potency);
        // It still HAS a rate — the zone cleared both floors — and the rate is honestly zero.
        Assert.Equal(0.0, roll.PotencyPerHour!.Value, 3);
        Assert.Equal(0.6, roll.MotesPerHour!.Value, 3);
    }

    // ---- the two floors -------------------------------------------------------------------

    /// <summary>Under <see cref="ZoneHistory.MinHours"/> there is no rate, for the reason the
    /// experience rate has none: four minutes containing one good pull is not a
    /// measurement.</summary>
    [Fact]
    public void UnderTheHoursFloorThereIsNoRate()
    {
        var roll = Assert.Single(Fold(
            [Mob("a froglok tad", "Lower Guk", 200, ("Mote of Major Potential", 6))],
            [Session("Lower Guk", 0.1)]));

        Assert.False(roll.HasRate);
        Assert.Null(roll.PotencyPerHour);
        // The COUNTS survive — they were observed, and only the division is refused.
        Assert.Equal(6, roll.Motes);
    }

    /// <summary>
    /// **THE KILLS FLOOR IS THE ONE THE HOURS FLOOR CANNOT SEE**, and it is why this fold has a
    /// second one at all. A single Infinite mote in a legitimate hour is ten potency an hour,
    /// measured correctly, over four kills — and it will never happen again. Time alone says
    /// nothing about that, because the time was real.
    /// </summary>
    [Fact]
    public void UnderTheKillsFloorThereIsNoRateEvenWithPlentyOfHours()
    {
        var roll = Assert.Single(Fold(
            [Mob("a named", "Lower Guk", 4, ("Mote of Infinite Potential", 1))],
            [Session("Lower Guk", 5)]));

        Assert.Equal(4, roll.Kills);
        Assert.True(roll.Hours >= ZoneHistory.MinHours);
        Assert.False(roll.HasRate);
        Assert.Null(roll.PotencyPerHour);
    }

    // ---- the joins ------------------------------------------------------------------------

    /// <summary>
    /// **KILLS COME FROM EVERY CREATURE IN THE ZONE, NOT ONLY THE GENEROUS ONES.**
    ///
    /// <para>"How often do you kill things here" is the cadence question. Counting only the
    /// creatures that dropped a mote would answer a different one — a camp where one rare named
    /// drops them would read as a place you kill twice an hour.</para>
    /// </summary>
    [Fact]
    public void TheCadenceCountsEveryKillInTheZone()
    {
        var roll = Assert.Single(Fold(
            [Mob("a named", "Lower Guk", 20, ("Mote of Major Potential", 4)),
             Mob("a froglok tad", "Lower Guk", 380, ("Froglok Blood", 9))],
            [Session("Lower Guk", 5)]));

        Assert.Equal(400, roll.Kills);
        Assert.Equal(80.0, roll.KillsPerHour!.Value, 3);
        // …while the MOTES are only the ones that actually dropped, and only one creature is
        // credited with them.
        Assert.Equal(4, roll.Motes);
        Assert.Equal(1, roll.Creatures);
        Assert.Equal("a named", roll.Top!.Mob);
    }

    /// <summary>
    /// The top creature is chosen by POTENCY and not by count — four Infinite motes (10 each)
    /// beat thirty Infinitesimals (1 each), because "who is worth killing here" is the question
    /// and a mote is not a unit of anything.
    /// </summary>
    [Fact]
    public void TheTopCreatureIsTheOneWorthTheMostRatherThanTheBusiest()
    {
        var roll = Assert.Single(Fold(
            [Mob("a named", "Lower Guk", 60, ("Mote of Infinite Potential", 4)),
             Mob("a froglok tad", "Lower Guk", 340, ("Mote of Infinitesimal Potential", 30))],
            [Session("Lower Guk", 5)]));

        Assert.Equal("a named", roll.Top!.Mob);
        Assert.Equal(40, roll.Top.Potency);
        Assert.Equal(4, roll.Top.Motes);
        Assert.Equal(70, roll.Potency);    // 40 + 30, both creatures pooled
        Assert.Equal(34, roll.Motes);
    }

    /// <summary>At EQUAL potency the busier creature wins, and that is a deliberate tie-break
    /// rather than whichever the pool happened to hand over first: at the same worth, the one
    /// you have actually seen more of is the one you can repeat.</summary>
    [Fact]
    public void AtEqualPotencyTheCreatureYouHaveSeenMoreOfWins()
    {
        var roll = Assert.Single(Fold(
            // 4 × 10 = 40, and 40 × 1 = 40.
            [Mob("a named", "Lower Guk", 60, ("Mote of Infinite Potential", 4)),
             Mob("a froglok tad", "Lower Guk", 340, ("Mote of Infinitesimal Potential", 40))],
            [Session("Lower Guk", 5)]));

        Assert.Equal("a froglok tad", roll.Top!.Mob);
        Assert.Equal(40, roll.Top.Potency);
        Assert.Equal(40, roll.Top.Motes);
    }

    /// <summary>The conned band and the instance tier are JOINED from the zone rollup rather
    /// than re-derived, so the mote engine's level discount reads the same <c>/consider</c>
    /// evidence the experience engine does (trap 4).</summary>
    [Fact]
    public void TheBandAndTheTierAreJoinedFromTheZoneRollup()
    {
        var pool = new[]
        {
            Mob("a froglok tad", "Najena 4 (Refined)", 200, ("Mote of Major Potential", 6)),
        };
        pool[0] = pool[0] with { LevelMin = 8, LevelMax = 12 };

        var roll = Assert.Single(MoteHistory.Fold(
            pool, ZoneHistory.Fold([Session("Najena 4 (Refined)", 5)], pool)));

        Assert.Equal(8, roll.ConnedMin);
        Assert.Equal(12, roll.ConnedMax);
        Assert.Equal(200, roll.ConnedKills);
        Assert.True(roll.HasConnedBand);
        Assert.Equal(4, roll.Tier);
    }

    /// <summary>A zone the POOL knows about and the session rows do not gets a row with no
    /// hours and no rate — the motes were really looted, and hiding them would be worse than
    /// reporting them without a division nobody can make.</summary>
    [Fact]
    public void AZoneWithNoStoredSessionKeepsItsMotesAndLosesOnlyItsRate()
    {
        var roll = Assert.Single(Fold(
            [Mob("a froglok tad", "Lower Guk", 200, ("Mote of Major Potential", 6))],
            [Session("Befallen", 5)]));

        Assert.Equal("Lower Guk", roll.Zone);
        Assert.Equal(6, roll.Motes);
        Assert.Equal(0, roll.Hours);
        Assert.Equal(0, roll.Sessions);
        Assert.False(roll.HasRate);
    }

    /// <summary>Rated zones sort first and by rate; the rateless tail sorts by name so the
    /// order is stable rather than whatever the dictionary produced.</summary>
    [Fact]
    public void RatedZonesSortFirstAndTheTailIsStable()
    {
        var rolls = Fold(
            [Mob("a", "Befallen", 200, ("Mote of Minor Potential", 4)),
             Mob("b", "Lower Guk", 200, ("Mote of Infinite Potential", 8)),
             Mob("c", "Unrest", 200, ("Mote of Major Potential", 4))],
            [Session("Befallen", 5), Session("Lower Guk", 5)]);

        Assert.Equal(["Lower Guk", "Befallen", "Unrest"], rolls.Select(r => r.Zone));
        Assert.False(rolls[2].HasRate);
    }
}
