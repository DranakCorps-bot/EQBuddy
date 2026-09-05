namespace EQBuddy.E2E;

/// <summary>
/// The suite's one wait discipline: every assertion about the live app polls an
/// observable condition with a deadline — never a bare sleep-and-hope.
/// </summary>
internal static class Wait
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);

    /// <param name="reason">What was being waited for — the timeout message leads with it.</param>
    /// <param name="detail">Extra diagnostics appended to the timeout message (the
    /// harness passes its artifact dump: debug.txt content, error.log tail).</param>
    /// <param name="abort">Asked on every poll: a non-null answer means the app can no
    /// longer arrive at ANY value, so waiting out the deadline would report the wrong
    /// thing. An app that has exited or stopped ticking looks identical from out here to
    /// a counter that simply will not move — the difference is the whole diagnosis, and
    /// a 90 s timeout that names the assertion buries it (trap 56).</param>
    public static void Until(Func<bool> condition, TimeSpan timeout, string reason,
        Func<string>? detail = null, Func<string?>? abort = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            if (abort?.Invoke() is { } stopped)
                throw new InvalidOperationException(
                    $"Gave up waiting for: {reason}{Environment.NewLine}...because {stopped}" +
                    (detail is null ? "" : Environment.NewLine + detail()));
            Thread.Sleep(Interval);
        }
        if (condition()) return;
        throw new TimeoutException(
            $"Timed out after {timeout.TotalSeconds:0}s waiting for: {reason}" +
            (detail is null ? "" : Environment.NewLine + detail()));
    }
}
