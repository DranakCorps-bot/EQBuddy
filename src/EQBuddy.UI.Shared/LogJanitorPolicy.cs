namespace EQBuddy.UI.Shared;

/// <summary>
/// May the log janitor empty finished-session logs RIGHT NOW?
///
/// This exists because the answer was written twice per widget and one of the four
/// copies disagreed, in the direction that destroys data. The WPF startup sweep asked
/// <c>TruncateLogs &amp;&amp; !ShowTutorial</c>; the WPF periodic janitor asked
/// <c>TruncateLogs</c> alone. Both Avalonia copies had the guard.
///
/// **Why the missing guard was a P0 and not an inconsistency.** The tour's first page is
/// the consent question — "Keep my log files — disable auto-empty" — so pruning has to
/// wait for an answer, which is exactly what the startup sweep does. But the janitor's
/// <c>_lastJanitorRun</c> starts at <see cref="System.DateTime.MinValue"/>, so its
/// "every 10 minutes" test passes on the FIRST one-second UI tick. The unguarded copy
/// therefore emptied every log about a second after launch — while the consent dialog
/// was still on page 1, before the player could physically read the sentence asking
/// them. Strilker-TV (Reddit, 2026-08-23) ticked the box, was emptied anyway, and could
/// only report that it "didn't take hold properly". It took hold; it was simply asked
/// after the fact.
///
/// The startup guard was not merely duplicated-and-drifted, then — it was load-bearing,
/// and the janitor silently undid it. One function, four callers, and a test that the
/// tour blocks pruning no matter which path asks.
/// </summary>
public static class LogJanitorPolicy
{
    /// <summary>
    /// <paramref name="truncateLogs"/> is the player's setting. <paramref name="showTutorial"/>
    /// means the tour is still enabled — i.e. the consent question has not been answered
    /// yet, since finishing the tour is what clears it. Unanswered means DO NOT PRUNE:
    /// the destructive default may only run once the player has had the chance to decline.
    /// </summary>
    public static bool ShouldPrune(bool truncateLogs, bool showTutorial)
        => truncateLogs && !showTutorial;
}
