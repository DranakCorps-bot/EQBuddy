namespace EQBuddy.UI.Shared;

/// <summary>
/// How long a tooltip is allowed to stay up — one bounded number for the whole app,
/// because the toolkit's own default for it stops every timer in the process.
///
/// **This is not a style preference, and the constant must never be "tidied" upward.**
/// WPF's <c>ToolTipService.ShowDuration</c> defaults to <c>int.MaxValue</c> ms — 24.8 days,
/// which is "forever" by accident rather than a duration anyone chose. When a tooltip
/// opens, WPF's <c>PopupControlService</c> arms a <c>DispatcherTimer</c> with that
/// interval, and the arithmetic underneath it is int32:
///
/// <list type="number">
/// <item><c>DispatcherTimer.Restart</c> computes <c>dueTime = Environment.TickCount +
///   (int)interval.TotalMilliseconds</c>. With <c>int.MaxValue</c> that OVERFLOWS negative
///   (the CI minidump held the exact value: <c>374781 + 2147483647</c> → <c>-2147108868</c>).</item>
/// <item><c>Dispatcher.UpdateWin32Timer</c> takes the WRAP-SAFE MINIMUM due time across
///   every registered timer — a subtraction, read as signed. The overflowed value reads as
///   ~24 days in the PAST and wins that minimum, but ONLY when some other registered timer
///   is already at least 1 ms overdue (see <see cref="StealsTheSharedTimer"/>). That δ is
///   the intermittency: a tooltip opening on an idle thread is harmless, and one opening
///   while a tick is running late is fatal.</item>
/// <item><c>SetWin32Timer</c> then computes <c>dueTime - TickCount</c>, which overflows the
///   other way to <c>+int.MaxValue</c>, and arms the ONE Win32 timer WPF shares across every
///   <c>DispatcherTimer</c> on the thread ~24 days out.</item>
/// <item>So the 1 s UI tick and the 50 ms EQBuddy Mobile pump die with it, permanently. The
///   thread sits in <c>GetMessage</c>, still painting and still dispatching clicks — which
///   is why the player-facing shape is "EQBuddy stopped updating but I can still use it"
///   rather than anything anyone would file as a freeze.</item>
/// </list>
///
/// **The fix is to remove the poisoned operand, not to defend against step 2.** With a
/// bounded interval, step 1 cannot overflow, so there is nothing for the wrap-safe minimum
/// to select and the mechanism has no first move. A watchdog would have been the other
/// shape and it is a <c>DispatcherTimer</c> too — it dies with the same shared Win32 timer
/// it would be guarding.
///
/// Diagnosed from CI run 34075983046 (`freeze-tick3` minidump, <c>_dueTimeInTicks =
/// -2147108868</c> against <c>PopupControlService._currentToolTipTimer</c>) and reproduced
/// standalone; see CLAUDE.md trap 63. `ToolTipPolicyTests` holds the arithmetic,
/// `ToolTipTimerTests` holds the mechanism and the runtime effect.
/// </summary>
public static class ToolTipPolicy
{
    /// <summary>The bounded default, applied app-wide. 30 s: long enough that a tooltip is
    /// not snatched away from someone reading a three-line hover (the xp chip's is the
    /// longest in the app), short enough that it is still recognisably a tooltip rather
    /// than a panel. Nothing in the mechanism cares about the exact number — anything far
    /// below <c>int.MaxValue</c> is safe — so this is a readability choice inside a range
    /// that is safe by construction.</summary>
    public const int ShowDurationMs = 30_000;

    /// <summary>The signed range this number may be moved within without re-arguing the
    /// mechanism. Both ends are about READING a tooltip; neither is a safety bound, which
    /// is the point — the safety comes from being bounded at all.</summary>
    public const int MinimumMs = 15_000;

    /// <summary>See <see cref="MinimumMs"/>.</summary>
    public const int MaximumMs = 60_000;

    /// <summary>What WPF hands out when nobody says otherwise, and the whole defect. Named
    /// here so the tests can state the mechanism against a constant rather than a magic
    /// <c>int.MaxValue</c> a reader has to recognise.</summary>
    public const int WpfDefaultMs = int.MaxValue;

    /// <summary>
    /// Step 2 of the mechanism, as arithmetic: would a tooltip timer of
    /// <paramref name="intervalMs"/>, started while another registered timer is already
    /// <paramref name="peerOverdueMs"/> past its due time, WIN the dispatcher's wrap-safe
    /// minimum and take the shared Win32 timer with it?
    ///
    /// The tooltip timer's due time is <c>tick + intervalMs</c> and the overdue peer's is
    /// <c>tick - peerOverdueMs</c>; the dispatcher compares them by subtracting, so the
    /// tick count cancels and the whole question is whether <c>intervalMs +
    /// peerOverdueMs</c> stays positive in int32. That is why this takes no clock: the
    /// answer does not depend on how long the app has been up.
    /// </summary>
    public static bool StealsTheSharedTimer(int intervalMs, int peerOverdueMs) =>
        unchecked(intervalMs + peerOverdueMs) < 0;
}
