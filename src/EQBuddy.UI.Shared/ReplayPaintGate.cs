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
/// So until <see cref="LogWatcher.InitialIngestDone"/> the bar is handed the
/// <see cref="Replaying"/> placeholder — which it draws as the character's name and
/// <see cref="ReadingLabel"/> (2026-09-29; zeroes before that) — and the first live snapshot
/// it paints is the settled one. Holding the previous figures instead would be wrong on a character switch, where
/// they belong to someone else. Alerts were already gated on the same flag
/// (<c>MainWindow.ProcessTrackedAlerts</c>); this is the paint half of that rule.
/// </summary>
public static class ReplayPaintGate
{
    /// <summary>The one snapshot the bar is shown during a replay. Never mutated.</summary>
    public static readonly StatsSnapshot Replaying = new();

    /// <summary>What the bar SAYS during the replay (Founder, 2026-09-29): a 62 MB log takes
    /// 10–20 s to re-read, and a row of zeroes for that long reads as "nothing happened this
    /// session". The bar draws the character's name and this, and no stat chips, until the
    /// first settled snapshot — then the real numbers, once.</summary>
    public const string ReadingLabel = "Reading log…";

    /// <summary>The tooltip on <see cref="ReadingLabel"/>.</summary>
    public const string ReadingTip =
        "EQBuddy re-reads your log when it starts so the numbers pick up where you left off. "
        + "A long log takes a little while; your session appears here once, when it is done.";

    /// <summary>True when a live-painting surface was handed the replay placeholder rather
    /// than a real snapshot — identity, not content, so a genuinely empty session is never
    /// mistaken for "still reading".</summary>
    public static bool IsReplaying(StatsSnapshot s) => ReferenceEquals(s, Replaying);

    /// <summary>
    /// The chip row under the bar during the replay: EMPTY, and the builder is not even run
    /// (2026-09-29, the Founder's second recording). The replay walks every buff, mez and
    /// slow the old sessions cast, so the row flashed "Skin like Wood 0:00 est", "Shield of
    /// Thistles", "Levitation 0:00"… one after another for the whole re-read — old sessions'
    /// buffs "expiring" as the log went past them. Nothing the row could show mid-replay is
    /// about now; whatever is still running (a respawn countdown, a buff cast today) is
    /// re-derived by the replay and appears on the first settled tick.
    /// </summary>
    public static List<HudChipEntry> ChipRow(bool initialIngestDone, Func<List<HudChipEntry>> build) =>
        initialIngestDone ? build() : [];

    /// <summary>The snapshot a live-painting surface should draw this tick.</summary>
    public static StatsSnapshot ForDisplay(bool initialIngestDone, StatsSnapshot live) =>
        initialIngestDone ? live : Replaying;
}
