using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE FARM MOTES ENGINE** (DRA-71 D7, Fable plan P10; Founder smoke item 5 — *"Motes:
/// highest-level zone, frequent kills, tier 2–4"*).
///
/// <para><c>MoteHistoryTests</c> covers the fold — what counts as a mote and what it is worth.
/// This file covers what the ENGINE does with it: the measured potency rate is the primary
/// term, and each of the Founder's three criteria is a NAMED discount with a sentence. Every
/// one of them is proved by running the same fixture twice with only that criterion moved,
/// because a weight nobody can see move is a comment.</para>
/// </summary>
public class RecommendationsMotesTests
{
    private static MobSummary Mob(
        string name, string zone, int kills, int band, params (string Item, int Count)[] loot) =>
        new(name, kills, kills, 30, 0, 0,
            [.. loot.Select(l => new MobLoot(l.Item, l.Count, null))])
        { Zone = zone, LevelMin = band, LevelMax = band + 4 };

    private static SessionRow Session(string zone, double hours, long id = 1) =>
        new(id, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(hours),
            hours * 3600, hours * 3600, "ended", zone, 0, 10, 0, 0, 0, 0, "", "");

    private static HelperInputs Inputs(
        IReadOnlyList<MobSummary> pool, IReadOnlyList<SessionRow> sessions,
        ResolvedLevel level = default)
    {
        var zones = ZoneHistory.Fold(sessions, pool);
        return new HelperInputs(zones, pool, null, [], [], [], [], false, [], [], null, level)
        {
            Motes = MoteHistory.Fold(pool, zones),
        };
    }

    private static RecommendationSet Rank(HelperInputs inputs) =>
        Recommendations.Rank(inputs, [HelperGoal.FarmMotes]);

    // ---- the answer ------------------------------------------------------------------------

    /// <summary>
    /// **A zone with observed motes outranks one without** — acceptance A7's first clause, and
    /// it needs no weight at all: a zone that has never paid a mote produces no candidate, so
    /// the ranking cannot reach it. The row names what was measured and who gave it to you.
    /// </summary>
    [Fact]
    public void AZoneWithMotesIsTheAnswerAndAZoneWithoutIsNotACandidate()
    {
        var set = Rank(Inputs(
            [Mob("a froglok tad", "Lower Guk", 200, 30, ("Mote of Major Potential", 6)),
             Mob("an orc pawn", "Crushbone", 200, 30, ("Rusty Axe", 20))],
            [Session("Lower Guk", 5), Session("Crushbone", 5, id: 2)]));

        var top = Assert.Single(set.Top);
        Assert.Equal("Lower Guk", top.Zone);
        Assert.Equal([HelperGoal.FarmMotes], top.Goals);

        var rate = Assert.Single(top.Why.OfType<ZoneMoteRateFact>());
        Assert.Equal(6.0, rate.PotencyPerHour, 3);
        Assert.Equal(Evidence.Personal, rate.Evidence);

        var who = Assert.Single(top.Why.OfType<MoteSourceFact>());
        Assert.Equal("a froglok tad", who.Mob);

        // Personal throughout: there is no catalog arm for motes at all, so no line here may
        // carry the estimate label (see MoteCatalogSurveyTests for why there is no arm).
        Assert.All(top.Why, w => Assert.Equal(Evidence.Personal, w.Evidence));
        Assert.DoesNotContain(HelperPresentation.CatalogLabel,
            string.Join(" ", top.Why.Select(HelperPresentation.Why)));
    }

    /// <summary>The higher measured potency rate ranks first — the primary term, before any
    /// discount runs. Both zones are held identical apart from what they paid.</summary>
    [Fact]
    public void TheMeasuredPotencyRateIsThePrimaryTerm()
    {
        var set = Rank(Inputs(
            [Mob("a froglok tad", "Lower Guk", 200, 30, ("Mote of Minor Potential", 6)),
             Mob("a sky defender", "Plane of Sky", 200, 30, ("Mote of Infinite Potential", 6))],
            [Session("Lower Guk", 5), Session("Plane of Sky", 5, id: 2)]));

        Assert.Equal(["Plane of Sky", "Lower Guk"], set.Top.Select(r => r.Zone));
        Assert.True(set.Top[0].Weight > set.Top[1].Weight);
    }

    // ---- criterion 1: the highest-level zone (the level this engine CONSUMES) --------------

    /// <summary>
    /// **"HIGHEST-LEVEL ZONE", AS THE SAME JUDGEMENT THE EXPERIENCE ENGINE MAKES.**
    ///
    /// <para>One fixture, ranked at two levels, with nothing else moved. At 12 the creatures
    /// still con near the character and nothing is said; at 60 they are a band the character
    /// has left behind, the row keeps its measured rate, gains the sentence and is weighted
    /// down. The sentence reports two numbers and no verdict — this repo has no XP curve and
    /// none is invented.</para>
    /// </summary>
    [Fact]
    public void AnOutgrownBandIsDiscountedAndSaysWhatItMeasured()
    {
        var pool = new[]
        {
            Mob("a froglok tad", "Lower Guk", 200, 8, ("Mote of Major Potential", 6)),
        };
        var sessions = new[] { Session("Lower Guk", 5) };

        var low = Assert.Single(Rank(Inputs(pool, sessions, Level(12))).Top);
        var high = Assert.Single(Rank(Inputs(pool, sessions, Level(60))).Top);

        Assert.Empty(low.Why.OfType<ZoneOutgrownFact>());
        var fact = Assert.Single(high.Why.OfType<ZoneOutgrownFact>());
        Assert.Equal(8, fact.ConnedMin);
        Assert.Equal(60, fact.Level);

        Assert.Equal(low.Weight * Recommendations.OutgrownWeight, high.Weight, 6);
        // The RATE is untouched — the discount re-orders and never edits a measurement.
        Assert.Equal(
            low.Why.OfType<ZoneMoteRateFact>().Single().PotencyPerHour,
            high.Why.OfType<ZoneMoteRateFact>().Single().PotencyPerHour, 6);
    }

    /// <summary>With no resolved level NOTHING changes — the unknown-level contract. A
    /// recommender that fell silent because it had not been told a number would be worse than
    /// one that never asked.</summary>
    [Fact]
    public void AnUnknownLevelChangesNothing()
    {
        var pool = new[] { Mob("a froglok tad", "Lower Guk", 200, 8, ("Mote of Major Potential", 6)) };
        var sessions = new[] { Session("Lower Guk", 5) };

        var unknown = Assert.Single(Rank(Inputs(pool, sessions, ResolvedLevel.Unknown)).Top);
        var known = Assert.Single(Rank(Inputs(pool, sessions, Level(12))).Top);

        Assert.Empty(unknown.Why.OfType<ZoneOutgrownFact>());
        Assert.Equal(known.Weight, unknown.Weight, 6);
    }

    // ---- criterion 2: frequent kills ---------------------------------------------------------

    /// <summary>
    /// **"FREQUENT KILLS", AGAINST YOUR OWN POOLED RATE AND NEVER A NUMBER OF KILLS.**
    ///
    /// <para>Two zones paying the same potency per hour, one of them getting there on a
    /// quarter of the kills. The slow one is marked down and draws the sentence; the busy one
    /// is left alone rather than promoted, because there is no bonus arm.</para>
    /// </summary>
    [Fact]
    public void ASlowCampIsDiscountedAgainstYourOwnKillRate()
    {
        var set = Rank(Inputs(
            [Mob("a froglok tad", "Lower Guk", 800, 30, ("Mote of Major Potential", 6)),
             Mob("a named", "Sebilis", 60, 30, ("Mote of Major Potential", 6))],
            [Session("Lower Guk", 5), Session("Sebilis", 5, id: 2)]));

        var busy = set.Top.Single(r => r.Zone == "Lower Guk");
        var slow = set.Top.Single(r => r.Zone == "Sebilis");

        Assert.Empty(busy.Why.OfType<ZoneKillRateFact>());
        var fact = Assert.Single(slow.Why.OfType<ZoneKillRateFact>());
        Assert.Equal(12.0, fact.KillsPerHour, 3);
        Assert.Equal(86.0, fact.BaselineKillsPerHour, 3);   // (800 + 60) / 10 hours

        Assert.Equal(busy.Weight * Recommendations.SlowKillWeight, slow.Weight, 6);
    }

    /// <summary>
    /// **ONE measured zone draws no cadence comparison at all** — D4's own clause, kept. A
    /// baseline folded from one zone IS that zone, so every single-camp profile would compare
    /// exactly average and a discount that could never fire would be dressed as one that had
    /// been checked (trap 78).
    /// </summary>
    [Fact]
    public void OneZoneIsNeverComparedAgainstItself()
    {
        var top = Assert.Single(Rank(Inputs(
            [Mob("a named", "Sebilis", 60, 30, ("Mote of Major Potential", 6))],
            [Session("Sebilis", 5)])).Top);

        Assert.Empty(top.Why.OfType<ZoneKillRateFact>());
        Assert.Equal(1.0, top.Weight, 6);
    }

    // ---- criterion 3: the instance tier -------------------------------------------------------

    /// <summary>
    /// **THE TIER PREFERENCE FIRES ONLY BETWEEN INSTANCES, AND ONLY OUTSIDE D2–D4.**
    ///
    /// <para>The zone names here are the game's own: "Najena 4 (Refined)" decodes to D4 and
    /// "Najena - Solo" to D0, through <see cref="InstanceTier"/> and no new parsing. The D0 one
    /// is marked down and says so; the D4 one is left alone rather than promoted, because D4
    /// refused bonus arms and this slice does not reopen that.</para>
    /// </summary>
    [Fact]
    public void AnInstanceOutsideTheNamedBandIsDiscountedAndSaysSo()
    {
        var set = Rank(Inputs(
            [Mob("a shadowed man", "Najena 4 (Refined)", 200, 30, ("Mote of Major Potential", 6)),
             Mob("a shadowed man", "Najena - Solo", 200, 30, ("Mote of Major Potential", 6))],
            [Session("Najena 4 (Refined)", 5), Session("Najena - Solo", 5, id: 2)]));

        var preferred = set.Top.Single(r => r.Zone == "Najena 4 (Refined)");
        var low = set.Top.Single(r => r.Zone == "Najena - Solo");

        Assert.Empty(preferred.Why.OfType<ZoneTierPreferenceFact>());
        var fact = Assert.Single(low.Why.OfType<ZoneTierPreferenceFact>());
        Assert.Equal(0, fact.Tier);
        Assert.Equal(Recommendations.MotePreferredTierMin, fact.PreferredMin);

        Assert.Equal(preferred.Weight * Recommendations.OffPreferredTierWeight, low.Weight, 6);
        Assert.Contains("D0", HelperPresentation.Why(fact));
    }

    /// <summary>
    /// **AN OPEN-WORLD ZONE IS UNTOUCHED, AND THIS IS THE CLAUSE THAT KEEPS THE PREFERENCE
    /// HONEST.**
    ///
    /// <para>Open world is most of the game and most of what a low-level character can reach.
    /// Marking it down would be EQBuddy ruling on a comparison nobody here can make — "is an
    /// open-world camp better or worse than a D3 for motes" has no answer in this repo — and it
    /// would read as a verdict on the player's whole evening.</para>
    /// </summary>
    [Fact]
    public void OpenWorldIsNeverMarkedDownForNotBeingAnInstance()
    {
        var set = Rank(Inputs(
            [Mob("a shadowed man", "Najena 4 (Refined)", 200, 30, ("Mote of Major Potential", 6)),
             Mob("a froglok tad", "Lower Guk", 200, 30, ("Mote of Major Potential", 6))],
            [Session("Najena 4 (Refined)", 5), Session("Lower Guk", 5, id: 2)]));

        var open = set.Top.Single(r => r.Zone == "Lower Guk");
        var instance = set.Top.Single(r => r.Zone == "Najena 4 (Refined)");

        Assert.Equal(InstanceTier.OpenWorld, InstanceTier.FromZoneName("Lower Guk"));
        Assert.Empty(open.Why.OfType<ZoneTierPreferenceFact>());
        Assert.Equal(instance.Weight, open.Weight, 6);
    }

    // ---- the silences --------------------------------------------------------------------------

    /// <summary>Stored play with no mote in it is its OWN sentence, and it is not the no-history
    /// one: "EQBuddy has read nothing" and "EQBuddy has read it and there were no motes" are
    /// different facts and only one of them is about the zones.</summary>
    [Fact]
    public void StoredPlayWithNoMotesIsItsOwnGap()
    {
        var set = Rank(Inputs(
            [Mob("an orc pawn", "Crushbone", 200, 30, ("Rusty Axe", 20))],
            [Session("Crushbone", 5)]));

        Assert.Empty(set.Top);
        var gap = Assert.Single(set.Gaps);
        Assert.Equal(GoalGapReason.NoMotesSeen, gap.Reason);
        Assert.NotEmpty(HelperPresentation.Gap(gap));

        var fresh = Rank(HelperInputs.Nothing);
        Assert.Equal(GoalGapReason.NoPlayHistory, Assert.Single(fresh.Gaps).Reason);
    }

    /// <summary>Motes in the bags and not enough kills to divide by is silence rather than a
    /// number with a caveat — <see cref="MoteHistory.MinKills"/>'s whole reason, asserted from
    /// the engine's side so a floor that stopped being read would show up here.</summary>
    [Fact]
    public void ALuckyDropUnderTheKillsFloorRanksNothing()
    {
        var set = Rank(Inputs(
            [Mob("a named", "Sebilis", 4, 30, ("Mote of Infinite Potential", 1))],
            [Session("Sebilis", 5)]));

        Assert.Empty(set.Top);
        Assert.Equal(GoalGapReason.NoMotesSeen, Assert.Single(set.Gaps).Reason);
    }

    /// <summary>The Void-Touched clause reaches the SCREEN. The fold carries the count and the
    /// potency reads zero, so a sentence that stopped at the rate would tell a raider their
    /// night paid nothing (trap 71's shape: a surface that degrades must be asserted on what it
    /// SAYS).</summary>
    [Fact]
    public void TheVoidTouchedMoteIsNamedRatherThanReadingAsZero()
    {
        var top = Assert.Single(Rank(Inputs(
            [Mob("a sky defender", "Plane of Sky", 120, 45, (Motes.VoidTouched, 3))],
            [Session("Plane of Sky", 5)])).Top);

        var sentence = HelperPresentation.Why(top.Why.OfType<ZoneMoteRateFact>().Single());
        Assert.Contains(Motes.VoidTouched, sentence);
        Assert.Contains("whole tier", sentence);
    }

    private static ResolvedLevel Level(int level) =>
        new(level, LevelSource.Observed, new DateTime(2026, 9, 12, 20, 0, 0));
}
