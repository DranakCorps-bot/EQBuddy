using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Replay tests: feed raw log lines through the parser into PartyDpsTracker and assert the
/// resulting snapshot — same path live tailing uses (mirrors SessionStatsTests).
/// </summary>
public class PartyDpsTrackerTests
{
    private static DateTime T(int mm, int ss) => new(2026, 7, 18, 15, mm, ss);

    private static string At(int mm, int ss, string msg) =>
        $"[Sat Jul 18 15:{mm:D2}:{ss:D2} 2026] {msg}";

    private static PartyDpsTracker Replay(params string[] lines)
    {
        var tracker = new PartyDpsTracker();
        foreach (var line in lines)
        {
            var evt = LogParser.Parse(line);
            if (evt is not null) tracker.Apply(evt);
        }
        return tracker;
    }

    [Fact]
    public void SelfDamageIsAttributedToYou()
    {
        var tracker = Replay(
            At(0, 0, "You slash orc pawn for 10 points of damage."),
            At(0, 1, "You slash orc pawn for 15 points of damage. (Critical)"));

        var snap = tracker.Snapshot(T(0, 1));
        var you = Assert.Single(snap.Rows);
        Assert.Equal("You", you.Name);
        Assert.Equal(2, you.Hits);
        Assert.Equal(25, you.Total);
        Assert.Equal(1, you.Crits);
    }

    /// <summary>EQ logs a mob hitting a groupmate as a Third* line, but a mob hitting YOU
    /// specifically is a totally different line ("Orc centurion hits YOU for..."). Without
    /// picking up DamageTakenEvent too, the enemy's own dps would vanish from Current Pull
    /// the moment it started attacking you instead of someone else (field report
    /// 2026-08-05).</summary>
    [Fact]
    public void EnemyDamageAgainstYouIsAttributedToTheAttacker()
    {
        var tracker = Replay(
            At(0, 0, "Orc centurion hits YOU for 4 points of damage."),
            At(0, 1, "Orc centurion hits YOU for 6 points of damage."));

        var snap = tracker.Snapshot(T(0, 1));
        var orc = Assert.Single(snap.Rows);
        Assert.Equal("Orc centurion", orc.Name);
        Assert.Equal(2, orc.Hits);
        Assert.Equal(10, orc.Total);
    }

    /// <summary>HP-cost casting, falls, drowning — self-inflicted, not an attacker.</summary>
    [Fact]
    public void SelfInflictedDamageTakenIsNotAttributedToAnAttacker()
    {
        var tracker = Replay(At(0, 0, "You hurt yourself for 27 points."));

        var snap = tracker.Snapshot(T(0, 0));
        Assert.Empty(snap.Rows);
    }

    /// <summary>LogParser's catch-all for non-melee lines EQ doesn't cleanly separate
    /// attacker from effect ("YOU are burned by orc centurion's flames...") puts a whole
    /// descriptive phrase in the Attacker field — not a name fit for a Track menu (field
    /// report 2026-08-05: "Burned by a gust of wind's flames" showing up as an attacker).
    /// It must not create a row.</summary>
    [Fact]
    public void AmbiguousNonMeleeDamageLabelIsNotTrackedAsAnAttacker()
    {
        var tracker = Replay(
            At(0, 0, "YOU are burned by orc centurion's flames for 6 points of non-melee damage!"));

        var snap = tracker.Snapshot(T(0, 0));
        Assert.Empty(snap.Rows);
    }

    /// <summary>Spell damage taken from a known caster is a clean, trackable attacker name —
    /// unlike the ambiguous label case above, this one has a real name and a known ability.</summary>
    [Fact]
    public void SpellDamageTakenFromAKnownCasterIsAttributedToTheCaster()
    {
        var tracker = Replay(
            At(0, 0, "ice boned skeleton hit you for 20 points of cold damage by Ice Bone Frost Burst."));

        var snap = tracker.Snapshot(T(0, 0));
        var attacker = Assert.Single(snap.Rows);
        Assert.Equal("Ice boned skeleton", attacker.Name);
        Assert.Equal(20, attacker.Total);
    }

    [Fact]
    public void ThirdPartyMeleeDotAndSchoolDamageAreSummedPerAttacker()
    {
        var tracker = Replay(
            At(0, 0, "Lizzid reaves orc legionnaire for 7 points of damage."),
            At(0, 2, "Orc centurion has taken 3 damage from Disease Cloud by Lizzid."),
            At(0, 4, "Jibekn hit orc centurion for 11 points of magic damage by Lifespike."));

        var snap = tracker.Snapshot(T(0, 4));
        Assert.Equal(21, snap.TotalDamage);

        var lizzid = Assert.Single(snap.Rows, r => r.Name == "Lizzid");
        Assert.Equal(2, lizzid.Hits);
        Assert.Equal(10, lizzid.Total);

        var jibekn = Assert.Single(snap.Rows, r => r.Name == "Jibekn");
        Assert.Equal(1, jibekn.Hits);
        Assert.Equal(11, jibekn.Total);
    }

    /// <summary>A miss carries no damage but is still combat activity — it must keep the
    /// pull alive the same way a hit does, or a quiet-but-still-swinging attacker would
    /// wrongly reset everyone else's totals.</summary>
    [Fact]
    public void MissesCarryNoDamageButKeepThePullAlive()
    {
        var tracker = Replay(
            At(0, 0, "Lizzid reaves orc legionnaire for 7 points of damage."),
            At(0, 9, "A puma tries to slash a ghoul, but misses!"),
            At(0, 17, "Lizzid reaves orc legionnaire for 5 points of damage."));

        // 17s separates the two damage hits, but the miss at 9s bridges the gap
        // (9s and 8s, both under the 10s pull boundary) — same pull, totals accumulate.
        var snap = tracker.Snapshot(T(0, 17));
        var lizzid = Assert.Single(snap.Rows);
        Assert.Equal(2, lizzid.Hits);
        Assert.Equal(12, lizzid.Total);
    }

    [Fact]
    public void AGapOverTenSecondsStartsAFreshPull()
    {
        var tracker = Replay(
            At(0, 0, "Lizzid reaves orc legionnaire for 7 points of damage."),
            At(0, 15, "Lizzid reaves orc legionnaire for 5 points of damage."));

        var snap = tracker.Snapshot(T(0, 15));
        var lizzid = Assert.Single(snap.Rows);
        // Only the second hit survives — the first pull's total was cleared at the gap.
        Assert.Equal(1, lizzid.Hits);
        Assert.Equal(5, lizzid.Total);
    }

    [Fact]
    public void SnapshotClearsStaleRowsAfterTheGapEvenWithoutANewEvent()
    {
        var tracker = Replay(At(0, 0, "Lizzid reaves orc legionnaire for 7 points of damage."));

        // Nothing else ever happens — 11 quiet seconds later the pull is over.
        var snap = tracker.Snapshot(T(0, 11));
        Assert.False(snap.Active);
        Assert.Empty(snap.Rows);
    }

    /// <summary>Running totals are a separate ledger from the live per-pull view — a
    /// pull-ending gap must not erase them, or "running totals" would just be the pull
    /// view with extra steps.</summary>
    [Fact]
    public void RunningTotalsSurviveAPullGapThatClearsTheLiveView()
    {
        var tracker = Replay(
            At(0, 0, "Lizzid reaves orc legionnaire for 7 points of damage."),
            At(0, 15, "Lizzid reaves orc legionnaire for 5 points of damage."));

        var roster = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Lizzid" };
        var totals = tracker.TotalsSnapshot(T(0, 15), roster);
        var lizzid = Assert.Single(totals.Rows);
        Assert.Equal(2, lizzid.Hits);
        Assert.Equal(12, lizzid.Total);

        // Meanwhile the live per-pull view did reset at the gap.
        var pull = tracker.Snapshot(T(0, 15));
        Assert.Equal(1, Assert.Single(pull.Rows).Hits);
    }

    /// <summary>The whole point of tracking combat-active seconds instead of wall-clock time
    /// since the totals started: standing around between pulls must not dilute dps.</summary>
    [Fact]
    public void RunningTotalsDurationExcludesIdleTimeBetweenPulls()
    {
        var tracker = Replay(
            At(0, 0, "Lizzid reaves orc legionnaire for 7 points of damage."),
            At(0, 5, "Lizzid reaves orc legionnaire for 3 points of damage."),
            // 20s of nothing — past the 10s pull gap — then a second pull.
            At(0, 25, "Lizzid reaves orc legionnaire for 7 points of damage."),
            At(0, 30, "Lizzid reaves orc legionnaire for 3 points of damage."));

        var roster = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Lizzid" };
        var totals = tracker.TotalsSnapshot(T(0, 30), roster);

        // Two 5-second pulls = 10s of actual combat, not the 30s of wall-clock time
        // that elapsed between the first hit and the last.
        Assert.Equal(10, totals.DurationSeconds, 3);
        Assert.Equal(20, totals.TotalDamage);
    }

    /// <summary>Clicking Reset mid-fight must not leave the pre-reset portion of the
    /// still-open pull counting toward the new duration.</summary>
    [Fact]
    public void ResetTotalsMidFightDoesNotCountTimeBeforeTheReset()
    {
        var tracker = Replay(
            At(0, 0, "Lizzid reaves orc legionnaire for 7 points of damage."),
            At(0, 5, "Lizzid reaves orc legionnaire for 3 points of damage."));
        tracker.ResetTotals();
        var roster = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Lizzid" };

        // Still mid-pull 4s after the reset, with no new hit yet.
        var totals = tracker.TotalsSnapshot(T(0, 9), roster);
        Assert.Equal(4, totals.DurationSeconds, 3);
        Assert.Empty(totals.Rows);
    }

    [Fact]
    public void TotalsSnapshotOnlyIncludesTheGivenRoster()
    {
        var tracker = Replay(
            At(0, 0, "Lizzid reaves orc legionnaire for 7 points of damage."),
            At(0, 1, "Jibekn hit orc centurion for 11 points of magic damage by Lifespike."));

        var roster = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Lizzid" };
        var totals = tracker.TotalsSnapshot(T(0, 1), roster);
        var row = Assert.Single(totals.Rows);
        Assert.Equal("Lizzid", row.Name);
    }

    [Fact]
    public void ResetTotalsClearsRunningTotalsOnly()
    {
        var tracker = Replay(At(0, 0, "Lizzid reaves orc legionnaire for 7 points of damage."));
        tracker.ResetTotals();

        var roster = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Lizzid" };
        var totals = tracker.TotalsSnapshot(T(0, 1), roster);
        Assert.Empty(totals.Rows);
    }
}
