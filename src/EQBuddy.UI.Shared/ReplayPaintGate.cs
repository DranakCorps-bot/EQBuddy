using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **WHAT THE MINIMIZED BAR PAINTS WHILE THE LOG IS STILL BEING RE-READ.**
///
/// On launch (and on every character switch) <see cref="LogWatcher"/> replays the whole
/// log from byte 0, and <see cref="SessionStats"/> rolls to a new session at every
/// 60-minute gap it passes. Each <c>Apply</c> takes the stats lock on its own, so the
/// widget's 1-second tick reads whichever OLD session the replay happens to be inside —
/// the Founder's recording (2026-09-28) shows the bar walk through six earlier sessions'
/// totals (19 dps, 30, 31, 44, 15, 0, 139) over ~6 seconds before landing on the real one.
/// Every one of those numbers was true once and none of them was about now.
///
/// So until <see cref="LogWatcher.InitialIngestDone"/> the bar is handed an EMPTY
/// snapshot — zeroes, one state — and the first live snapshot it paints is the settled
/// one. Holding the previous figures instead would be wrong on a character switch, where
/// they belong to someone else. Alerts were already gated on the same flag
/// (<c>MainWindow.ProcessTrackedAlerts</c>); this is the paint half of that rule.
/// </summary>
public static class ReplayPaintGate
{
    /// <summary>The one snapshot the bar is shown during a replay. Never mutated.</summary>
    public static readonly StatsSnapshot Replaying = new();

    /// <summary>The snapshot a live-painting surface should draw this tick.</summary>
    public static StatsSnapshot ForDisplay(bool initialIngestDone, StatsSnapshot live) =>
        initialIngestDone ? live : Replaying;
}
