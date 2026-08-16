using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The live countdown on a rule with a cue in flight. Asked for alongside per-rule mini
/// chips: a respawn timer you set eight minutes ago is only useful if you can see how long
/// is left without opening Options to remember what you typed.
/// </summary>
public class CountdownTests
{
    private static readonly DateTime T0 = new(2026, 7, 31, 20, 0, 0);

    private static TrackedRule Rule(double delay, string name) =>
        new() { Name = name, Pattern = "x", Kind = WatchKind.Text, AlertDelaySeconds = delay };

    [Fact]
    public void APendingCueReportsWhenItIsDue()
    {
        var alerts = new DelayedAlerts();
        var rule = Rule(480, "Respawn");
        alerts.Schedule(rule, "Respawn", "PH down", T0);

        var due = alerts.NextDueByRule(T0.AddSeconds(60));

        // Keyed by the rule's id, not its name — two rules may share a name.
        Assert.Equal(T0.AddSeconds(480), due[rule.Id]);
    }

    /// <summary>Several cues on one rule: the soonest is the one about to matter.</summary>
    [Fact]
    public void TheSoonestCueWins()
    {
        var alerts = new DelayedAlerts();
        var rule = Rule(480, "Respawn");
        alerts.Schedule(rule, "Respawn", "first", T0);
        alerts.Schedule(rule, "Respawn", "second", T0.AddSeconds(30));

        Assert.Equal(T0.AddSeconds(480), alerts.NextDueByRule(T0.AddSeconds(60))[rule.Id]);
    }

    [Fact]
    public void ACueThatHasAlreadyFiredStopsCountingDown()
    {
        var alerts = new DelayedAlerts();
        alerts.Schedule(Rule(10, "Cast"), "Cast", "go", T0);

        Assert.Empty(alerts.NextDueByRule(T0.AddSeconds(11)));
    }

    /// <summary>A countdown that kept ticking after the cue was cancelled would be worse than
    /// none — it would show a timer for something that will never fire.</summary>
    [Fact]
    public void CancelledCuesStopCountingDown()
    {
        var alerts = new DelayedAlerts();
        alerts.Schedule(Rule(30, "Cast"), "Cast", "go", T0);
        alerts.CancelCombatCues();

        Assert.Empty(alerts.NextDueByRule(T0.AddSeconds(5)));
    }

    /// <summary>A respawn timer survives death, so its countdown must too.</summary>
    [Fact]
    public void LongTimersKeepCountingDownThroughDeath()
    {
        var alerts = new DelayedAlerts();
        alerts.Schedule(Rule(480, "Respawn"), "Respawn", "PH down", T0);
        alerts.CancelCombatCues();

        Assert.Single(alerts.NextDueByRule(T0.AddSeconds(5)));
    }

    [Fact]
    public void SessionEndClearsEveryCountdown()
    {
        var alerts = new DelayedAlerts();
        alerts.Schedule(Rule(480, "Respawn"), "Respawn", "PH down", T0);
        alerts.CancelAll();

        Assert.Empty(alerts.NextDueByRule(T0.AddSeconds(5)));
    }

    /// <summary>m:ss while a minute or more remains, bare seconds after that — a respawn wants
    /// "4:12" and the tail of a cast cue wants "3s".</summary>
    [Theory]
    [InlineData(492, "8:12")]
    [InlineData(61, "1:01")]
    [InlineData(60, "1:00")]
    // A live clock crosses the minute at 59.98s-ish every cycle: rounding that up
    // must read "1:00", never "60s" — which looked like the countdown stalling.
    [InlineData(59.98, "1:00")]
    [InlineData(59, "59s")]
    [InlineData(2.4, "3s")]
    [InlineData(0, "1s")]
    [InlineData(-5, "1s")]
    public void CountdownFormatting(double seconds, string expected) =>
        Assert.Equal(expected, Countdown.Format(TimeSpan.FromSeconds(seconds)));
}
