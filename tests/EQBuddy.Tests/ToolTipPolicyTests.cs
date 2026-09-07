using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The bounded tooltip duration, and the arithmetic that is the whole reason for it.
///
/// The RUNTIME half — that the number actually reaches a control, and that a timer armed
/// with it leaves the other timers on the thread alive — is `ToolTipTimerTests` in
/// tests/EQBuddy.E2E, because neither can be seen from a pure unit test (the WPF layer has
/// no unit tests at all: docs/TestPlan.md §5). What CAN be seen here is the claim the
/// constant rests on, so it is stated as arithmetic rather than as prose in a doc comment
/// nobody re-derives.
/// </summary>
public class ToolTipPolicyTests
{
    [Fact]
    public void TheBoundedDurationIsPositiveAndInsideTheSignedRange()
    {
        Assert.True(ToolTipPolicy.ShowDurationMs > 0);
        Assert.InRange(ToolTipPolicy.ShowDurationMs, ToolTipPolicy.MinimumMs, ToolTipPolicy.MaximumMs);
        // The upper end is the ceiling Helm signed. It is not a safety bound — safety
        // comes from being bounded at all — but a number that drifted past it would be a
        // product decision nobody made, and "tidied back up" is exactly how this class of
        // constant dies.
        Assert.True(ToolTipPolicy.MaximumMs <= 60_000);
    }

    /// <summary>
    /// The mechanism, in one line: a tooltip timer steals the dispatcher's single shared
    /// Win32 timer when its interval plus the amount another timer is already overdue
    /// overflows int32. Every δ here is a plausible amount for the 1 s tick to be running
    /// late by — the CI freeze happened with a startup replay holding the thread, which is
    /// milliseconds, and a day is far past anything a live app produces.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    [InlineData(1_000)]
    [InlineData(60_000)]
    [InlineData(3_600_000)]
    [InlineData(86_400_000)]
    public void TheBoundedDurationCannotWinTheWrapSafeMinimum(int peerOverdueMs)
    {
        Assert.False(ToolTipPolicy.StealsTheSharedTimer(ToolTipPolicy.ShowDurationMs, peerOverdueMs));
        // The claim holds across the whole signed range, not only at the number chosen —
        // so moving it within 15-60 s stays a readability decision rather than a new
        // argument about overflow.
        Assert.False(ToolTipPolicy.StealsTheSharedTimer(ToolTipPolicy.MinimumMs, peerOverdueMs));
        Assert.False(ToolTipPolicy.StealsTheSharedTimer(ToolTipPolicy.MaximumMs, peerOverdueMs));
    }

    /// <summary>
    /// THE NEGATIVE, and the reason the assertions above are not vacuous: WPF's own default
    /// DOES steal it, and it needs only one millisecond of lateness to do so. An equality
    /// suite with no negative in it passes just as well against arithmetic that can never
    /// answer true (trap 39).
    /// </summary>
    [Fact]
    public void TheWpfDefaultStealsItTheMomentAnythingIsOneMillisecondLate()
    {
        Assert.True(ToolTipPolicy.StealsTheSharedTimer(ToolTipPolicy.WpfDefaultMs, 1));
        Assert.True(ToolTipPolicy.StealsTheSharedTimer(ToolTipPolicy.WpfDefaultMs, 250));
        // …and the δ trigger is real rather than decorative: with every other timer due in
        // the FUTURE, the same poisoned interval does nothing at all. That is the
        // intermittency the diagnosis named, and it is why this freeze took five red CI
        // runs to reproduce and never once showed up on a desk.
        Assert.False(ToolTipPolicy.StealsTheSharedTimer(ToolTipPolicy.WpfDefaultMs, 0));
    }

    /// <summary>The overflow itself, at the exact value the CI minidump held, so the
    /// arithmetic in ToolTipPolicy's doc comment is checked rather than believed:
    /// 374,781 ms of uptime + int.MaxValue = -2,147,108,868.</summary>
    [Fact]
    public void TheMinidumpsDueTimeIsWhatTheWpfDefaultProduces()
    {
        Assert.Equal(-2_147_108_868, unchecked(374_781 + ToolTipPolicy.WpfDefaultMs));
        // The bounded number, from the same uptime, is a due time 30 s in the future —
        // an ordinary one the dispatcher can sort with the rest.
        Assert.Equal(404_781, unchecked(374_781 + ToolTipPolicy.ShowDurationMs));
    }
}
