using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// The recent heal-versus-damage weight the collapsed HUD's third number swaps on
/// (Surface A / SA-1). <c>HudGlanceTests</c> pins what the HUD does with it; this pins
/// that the session produces it, from real parsed log lines.
///
/// It is derived purely from event totals, anchored on the last log timestamp — no wall
/// clock — which is exactly why it can be tested at all: a replay of timestamped lines
/// gives the same answer on any machine at any hour. The alternative (a
/// <c>DateTime.Now</c> window, like <see cref="StatsSnapshot.CurrentDps"/>) would have
/// needed the snapshot memo's caveat extended and would have made this file a test about
/// the clock. See the record's own doc comment and DECISIONS.md.
/// </summary>
public class RecentEffortTests
{
    private static string At(int mm, int ss, string msg) =>
        $"[Sat Jul 18 15:{mm:D2}:{ss:D2} 2026] {msg}";

    private static StatsSnapshot Replay(params string[] lines)
    {
        var stats = new SessionStats { CharacterName = "Kaybek", ServerName = "freeport" };
        foreach (var line in lines)
            if (LogParser.Parse(line) is { } evt) stats.Apply(evt);
        return stats.Snapshot();
    }

    [Fact]
    public void ASessionWithNoEventsHasNoEffortEitherWay()
    {
        var effort = new SessionStats().Snapshot().Effort;
        Assert.Equal(0, effort.DamageDone);
        Assert.Equal(0, effort.HealingDone);
        Assert.Equal(0, effort.DamageDoneInResumeWindow);
    }

    [Fact]
    public void DamageInsideTheWindowIsCounted()
    {
        var effort = Replay(
            At(0, 0, "You slash a ghoul for 40 points of damage."),
            At(0, 2, "You slash a ghoul for 60 points of damage.")).Effort;
        Assert.Equal(100, effort.DamageDone);
        Assert.Equal(0, effort.HealingDone);
    }

    /// <summary>The window is anchored on the LAST event, so the first line here is 40
    /// seconds behind the last one and out of a 30-second window.</summary>
    [Fact]
    public void DamageOlderThanTheWindowHasFallenOutOfIt()
    {
        var effort = Replay(
            At(0, 0, "You slash a ghoul for 5000 points of damage."),
            At(0, 40, "You slash a ghoul for 60 points of damage.")).Effort;
        Assert.Equal(60, effort.DamageDone);
    }

    [Fact]
    public void YourOwnHealsAreCounted()
    {
        var effort = Replay(
            At(0, 0, "You healed Grimwold for 250 hit points by Light Healing."),
            At(0, 3, "You healed Grimwold for 300 hit points by Light Healing.")).Effort;
        Assert.Equal(550, effort.HealingDone);
        Assert.Equal(0, effort.DamageDone);
    }

    /// <summary>Being healed is not healing. The HUD number is HPS — YOURS — and the one
    /// thing EQBuddy will never show is somebody else's, so an incoming heal must not
    /// push the slot over to a healing readout.</summary>
    [Fact]
    public void HealsYouRECEIVEDoNotCountAsHealingYouDid()
    {
        var effort = Replay(
            At(0, 0, "Grimwold healed you for 400 hit points by Light Healing.")).Effort;
        Assert.Equal(0, effort.HealingDone);
    }

    /// <summary>The two windows are the whole point of the record: damage that has
    /// already stopped stays in the long window (so the weight is honest) and leaves the
    /// short one (so the HUD does not flip back for a swing thirty seconds ago).</summary>
    [Fact]
    public void TheResumeWindowIsShorterThanTheDominanceWindow()
    {
        var effort = Replay(
            At(0, 0, "You slash a ghoul for 400 points of damage."),
            At(0, 20, "You healed Grimwold for 900 hit points by Light Healing.")).Effort;
        Assert.Equal(400, effort.DamageDone);              // still inside 30 s
        Assert.Equal(0, effort.DamageDoneInResumeWindow);  // outside 5 s
        Assert.Equal(900, effort.HealingDone);
    }

    [Fact]
    public void DamageInTheLastFewSecondsIsInBothWindows()
    {
        var effort = Replay(
            At(0, 0, "You healed Grimwold for 900 hit points by Light Healing."),
            At(0, 20, "You slash a ghoul for 400 points of damage.")).Effort;
        Assert.Equal(400, effort.DamageDone);
        Assert.Equal(400, effort.DamageDoneInResumeWindow);
    }

    /// <summary>The windows it reports are the windows it used — a consumer that had to
    /// hard-code 30 and 5 would be a second source for one fact (trap 4).</summary>
    [Fact]
    public void TheEffortReportsTheWindowsItWasComputedOver()
    {
        var effort = Replay(At(0, 0, "You slash a ghoul for 40 points of damage.")).Effort;
        Assert.Equal(SessionStats.EffortWindow, effort.Window);
        Assert.Equal(SessionStats.EffortResumeWindow, effort.ResumeWindow);
        Assert.True(effort.ResumeWindow < effort.Window);
    }

    /// <summary>The snapshot memo serves a cached instance for an unchanged version. This
    /// value is derived from the journal alone, so a cached answer is a CURRENT answer —
    /// which is the property that let it stay out of the memo's wall-clock caveat.</summary>
    [Fact]
    public void TwoSnapshotsOfAnUnchangedSessionAgree()
    {
        var stats = new SessionStats { CharacterName = "Kaybek", ServerName = "freeport" };
        foreach (var line in new[]
                 {
                     At(0, 0, "You healed Grimwold for 900 hit points by Light Healing."),
                     At(0, 2, "You slash a ghoul for 40 points of damage."),
                 })
            if (LogParser.Parse(line) is { } evt) stats.Apply(evt);

        Assert.Equal(stats.Snapshot().Effort, stats.Snapshot().Effort);
    }
}
