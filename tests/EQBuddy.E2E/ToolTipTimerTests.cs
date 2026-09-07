using System.Windows.Threading;
using EQBuddy.UI.Shared;

namespace EQBuddy.E2E;

/// <summary>
/// THE TICK-FREEZE (#354/#357/#359/#360, PR-3). Two facts, and they fail independently.
///
/// **The mechanism guard** is the diagnosis's standalone repro, lifted here: it launches no
/// app, opens no window and touches no screen — it is one dispatcher on one thread, so it
/// says something about the TIMER arithmetic and nothing about EQBuddy's layout. It belongs
/// in this suite anyway because it needs a real WPF dispatcher, which `EQBuddy.Tests` (a
/// portable project) deliberately cannot have.
///
/// **The effect assertion** is the other half and the one trap 42 demands: an
/// <c>OverrideMetadata</c> fix has shipped in this repo before, been genuinely present in
/// the binary, and changed nothing at runtime. So the launched app reports what a control
/// actually resolves — <c>tooltipShowDurationMs</c> — and this asserts it equals the policy.
/// Passing the mechanism guard while the app resolves <c>int.MaxValue</c> is precisely the
/// state that would look like a fix and ship the freeze.
///
/// **THE PROVE-FAIL, and why it is not committed as a test.** Both directions were run
/// before this landed: substituting <see cref="ToolTipPolicy.WpfDefaultMs"/> into
/// <see cref="FastTicksOnceATooltipTimerStarts"/> starves the fast timer to exactly ZERO
/// ticks, and the bounded value leaves it ticking — which is what makes the assertion below
/// something other than a test that cannot fail (trap 34). It stays a one-word local edit
/// rather than a committed <c>[Fact]</c> on purpose: a test asserting that WPF's overflow
/// still reproduces would go red the day a .NET update FIXES it, and this suite gates the
/// release. The arithmetic half of the same negative is committed, in
/// <c>ToolTipPolicyTests</c>.
///
/// [Collection("e2e")] because one test here launches a real always-on-top widget.
/// </summary>
[Collection("e2e")]
public sealed class ToolTipTimerTests
{
    /// <summary>250 ms, the repro's fast timer — standing in for EQBuddy's own 1 s tick and
    /// 50 ms Mobile pump, which are the two that actually died in CI.</summary>
    private static readonly TimeSpan FastInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Long enough to hold the fast timer past its due date before the tooltip
    /// timer starts. This is the δ ≥ 1 ms trigger, and it is the whole reason the freeze is
    /// intermittent: the diagnosis's first run did NOT reproduce without it.</summary>
    private static readonly TimeSpan MakeTheFastTimerLate = TimeSpan.FromMilliseconds(300);

    /// <summary>Twelve fast intervals. The two outcomes are "still ticking" and "exactly
    /// zero", so the window only has to be long enough that a loaded runner cannot look
    /// like the second one.</summary>
    private static readonly TimeSpan Observe = TimeSpan.FromSeconds(3);

    /// <summary>
    /// THE MECHANISM. A tooltip timer armed with the bounded duration, started at the exact
    /// moment that poisons it — with another timer already overdue — leaves that other timer
    /// running.
    ///
    /// With WPF's default the count is zero: the overflowed due time wins the dispatcher's
    /// wrap-safe minimum and the one shared Win32 timer is armed 24 days out, so every
    /// DispatcherTimer on the thread stops at once while the thread sits in GetMessage,
    /// responsive and idle. See ToolTipPolicy for the arithmetic.
    /// </summary>
    [Fact]
    public void ABoundedTooltipTimerLeavesTheOtherTimersOnTheThreadTicking()
    {
        // Named rather than passed inline, so the prove-fail edit changes the number the
        // failure message reports as well as the number under test — a message quoting the
        // policy while the run used something else is one fact with two sources (trap 4).
        var interval = ToolTipPolicy.ShowDurationMs;
        var ticks = FastTicksOnceATooltipTimerStarts(interval);

        Assert.True(ticks >= 3,
            $"the 250 ms timer ticked {ticks} times in {Observe.TotalSeconds:0}s after a " +
            $"tooltip timer of {interval} ms started against an overdue " +
            "peer. Zero is the freeze: the tooltip timer has taken the dispatcher's one " +
            "shared Win32 timer with it (ToolTipPolicy).");
    }

    /// <summary>
    /// THE EFFECT (trap 42). The launched app's own answer for what a control resolves,
    /// which is the only thing that can tell "the override is in the binary" from "the
    /// override is in force".
    ///
    /// Prove-failed the same way: demanding <see cref="ToolTipPolicy.WpfDefaultMs"/> instead
    /// times out with *"last seen 30000"*, which is the launched app naming the value it
    /// really resolved rather than this test agreeing with a constant it was handed.
    /// </summary>
    [Fact]
    public void TheLaunchedAppResolvesTheBoundedDurationOnItsControls()
    {
        using var app = new AppHarness();
        app.Launch();

        app.WaitForDump("tooltipShowDurationMs", ToolTipPolicy.ShowDurationMs,
            "the app's own controls to resolve the bounded tooltip duration " +
            $"(int.MaxValue = {ToolTipPolicy.WpfDefaultMs} means the override never took effect)");
    }

    /// <summary>
    /// The diagnosis's repro, as a function of the tooltip timer's interval so the
    /// prove-fail is one word.
    ///
    /// Order matters and is the whole fixture: the fast timer is started, the thread is
    /// then BLOCKED past its due time, and only then does the tooltip timer start — so the
    /// wrap-safe minimum is being asked to choose between an overdue peer and the tooltip
    /// timer, which is the only situation in which the overflow selects anything.
    ///
    /// Shutdown comes from OUTSIDE, via a posted operation, because a DispatcherTimer
    /// deadline would die with the very timer under test and hang the run. Posted messages
    /// still arrive in the frozen state — that is exactly why the app went on answering
    /// clicks while its clock had stopped.
    /// </summary>
    private static int FastTicksOnceATooltipTimerStarts(int tooltipIntervalMs)
    {
        var ticksAfterTheTooltipTimerStarted = 0;
        var counting = false;
        Dispatcher? dispatcher = null;
        using var armed = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;

            var fast = new DispatcherTimer(DispatcherPriority.Background) { Interval = FastInterval };
            fast.Tick += (_, _) =>
            {
                if (Volatile.Read(ref counting))
                    Interlocked.Increment(ref ticksAfterTheTooltipTimerStarted);
            };
            fast.Start();

            Thread.Sleep(MakeTheFastTimerLate);

            var tooltip = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(tooltipIntervalMs),
            };
            tooltip.Tick += (_, _) => { };
            Volatile.Write(ref counting, true);
            tooltip.Start();

            armed.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        Assert.True(armed.Wait(TimeSpan.FromSeconds(30)), "the dispatcher thread never armed its timers");
        Thread.Sleep(Observe);
        dispatcher!.BeginInvokeShutdown(DispatcherPriority.Send);
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "the dispatcher thread never shut down");

        return Volatile.Read(ref ticksAfterTheTooltipTimerStarted);
    }
}
