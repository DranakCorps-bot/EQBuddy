using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The timer/progress vocabulary (Gate 3). The Gate 1 audit's three findings about the
/// Spawns window were all one defect — a countdown that is a string has no state, so
/// nothing can stop "unknown" being drawn as "nothing". These pin that it now can.
/// </summary>
public class TimerViewTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Local);

    private static TimerView.View At(double secondsLeft, double? duration = 600) =>
        TimerView.For(Now.AddSeconds(secondsLeft), duration, Now);

    [Fact]
    public void ARunningTimerCarriesTheAccentAndItsShareOfTheCycle()
    {
        var view = At(300, duration: 600);
        Assert.Equal(TimerView.State.Running, view.State);
        Assert.Equal(0.5, view.Fraction!.Value, 3);
        Assert.Equal("AccentBrush", view.TextColorKey);
    }

    [Fact]
    public void TheLastFewSecondsAreImminent()
    {
        Assert.Equal(TimerView.State.Running, At(TimerView.ImminentSeconds + 1).State);
        Assert.Equal(TimerView.State.Imminent, At(TimerView.ImminentSeconds).State);
        Assert.Equal(TimerView.State.Imminent, At(1).State);
    }

    /// <summary>The map pulses its named circles on the same window. A player watching one
    /// camp on the desktop and the phone must not see them disagree about "about to pop".</summary>
    [Fact]
    public void ImminentMatchesTheMapsPulseWindow() =>
        Assert.Equal(EQBuddy.Companion.CompanionMapSource.PulseWindowSeconds,
            TimerView.ImminentSeconds);

    [Fact]
    public void DueFillsTheBarAndSaysSoLoudly()
    {
        var view = At(-5);
        Assert.Equal(TimerView.State.Due, view.State);
        Assert.Equal(1.0, view.Fraction!.Value);
        Assert.Equal("DUE", TimerView.Text(view, Now.AddSeconds(-5), Now));
    }

    /// <summary>A camp popping is the good news the window exists to deliver. Red ink on
    /// every successful outcome would teach the player that the window shouts at them
    /// when things go RIGHT.</summary>
    [Fact]
    public void DueWearsWarnRatherThanDanger() => Assert.Equal("WarnBrush", At(-1).TextColorKey);

    /// <summary>THE audit finding: a row with no duration used to render a blank
    /// countdown, so "we don't know" and "there is nothing here" looked identical.</summary>
    [Fact]
    public void AKillWithNoKnownRespawnIsUnknownAndNotIdle()
    {
        var unknown = TimerView.For(dueAt: null, durationSeconds: null, Now, hasTimer: true);
        var idle = TimerView.For(dueAt: null, durationSeconds: null, Now, hasTimer: false);

        Assert.Equal(TimerView.State.Unknown, unknown.State);
        Assert.Equal(TimerView.State.Idle, idle.State);
        Assert.NotEqual(unknown.State, idle.State);
        // And it SAYS something rather than nothing.
        Assert.Equal("—", TimerView.Text(unknown, null, Now));
        Assert.Equal("", TimerView.Text(idle, null, Now));
    }

    /// <summary>Unknown keeps its track: the row still has a slot for progress, it just has
    /// no claim to make. Without this the list reflows every time a timer starts.</summary>
    [Fact]
    public void UnknownDrawsAnEmptyTrackRatherThanNoTrack()
    {
        var unknown = TimerView.For(null, null, Now, hasTimer: true);
        Assert.True(unknown.HasTrack);
        Assert.Null(unknown.Fraction);
        Assert.Null(unknown.FillColorKey);
    }

    /// <summary>Idle and suppressed rows have no cycle at all, so a track would be
    /// claiming one exists.</summary>
    [Fact]
    public void RowsWithNoCycleDrawNoTrack()
    {
        Assert.False(TimerView.For(null, null, Now, hasTimer: false).HasTrack);
        Assert.False(TimerView.For(null, null, Now, suppression: TimerSuppression.RaidInstance).HasTrack);
    }

    /// <summary>#109: a raid-instanced boss respawns on the game's instance clock, not a
    /// camp cycle. Saying "instance" beats a blank that reads as broken.</summary>
    [Fact]
    public void ARaidInstancedRowExplainsItselfInsteadOfCountingDown()
    {
        var view = TimerView.For(Now.AddSeconds(300), 600, Now, suppression: TimerSuppression.RaidInstance);
        Assert.Equal(TimerView.State.Suppressed, view.State);
        Assert.Equal("instance", TimerView.Text(view, Now.AddSeconds(300), Now));
        Assert.Null(view.Fraction);
    }

    /// <summary>The Sky follow-up to #109: a TRIGGERED spawn is a different reason from a
    /// raid instance, and the word on the row must differ because the player's next
    /// action differs (go kill the trigger, versus wait for the instance clock). One
    /// flag for two meanings is trap 4.</summary>
    [Fact]
    public void ATriggeredRowSaysTriggeredNotInstance()
    {
        var view = TimerView.For(null, null, Now, suppression: TimerSuppression.Triggered);
        Assert.Equal(TimerView.State.Triggered, view.State);
        Assert.Equal("triggered", TimerView.Text(view, null, Now));
        Assert.Null(view.Fraction);
        Assert.False(view.HasTrack);
        Assert.NotEqual(TimerView.Text(TimerView.For(null, null, Now, suppression: TimerSuppression.RaidInstance), null, Now),
            TimerView.Text(view, null, Now));
    }

    /// <summary>A timer running with no known duration counts down but cannot claim a
    /// share of a cycle it doesn't know the length of.</summary>
    [Fact]
    public void NoDurationMeansNoFractionEvenWhileCountingDown()
    {
        var view = TimerView.For(Now.AddSeconds(120), durationSeconds: null, Now);
        Assert.Equal(TimerView.State.Running, view.State);
        Assert.Null(view.Fraction);
        Assert.True(view.HasTrack);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveDurationCannotProduceAFraction(double duration) =>
        Assert.Null(TimerView.For(Now.AddSeconds(60), duration, Now).Fraction);

    /// <summary>The fraction is a share and is clamped to one: a timer overdue by an hour
    /// must not draw a bar three times the width of its track.</summary>
    [Fact]
    public void TheFractionNeverLeavesTheTrack()
    {
        Assert.Equal(1.0, TimerView.For(Now.AddSeconds(-3600), 60, Now).Fraction);
        Assert.InRange(At(599, duration: 600).Fraction!.Value, 0, 1);
    }

    /// <summary>Every colour a state can name has to be a real palette key, or it paints
    /// an invisible countdown in all eight themes at once.</summary>
    [Fact]
    public void EveryTimerColourIsAPaletteKey()
    {
        foreach (var view in new[]
        {
            At(300), At(5), At(-1),
            TimerView.For(null, null, Now, hasTimer: true),
            TimerView.For(null, null, Now, hasTimer: false),
            TimerView.For(null, null, Now, suppression: TimerSuppression.RaidInstance),
        })
        {
            Assert.Contains(view.TextColorKey, ThemePalettes.Keys);
            Assert.Contains(view.TrackColorKey, ThemeTones.Keys);
            if (view.FillColorKey is { } fill) Assert.Contains(fill, ThemePalettes.Keys);
        }
    }
}
