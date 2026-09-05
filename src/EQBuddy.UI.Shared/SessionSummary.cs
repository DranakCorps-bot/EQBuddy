using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>Which session Home is describing, which is three states and not two.</summary>
public enum RecentSessionState
{
    /// <summary>Nothing has ever been recorded for this character — a genuinely fresh
    /// profile, or one that has never been followed long enough to be worth keeping
    /// (<see cref="SessionRepository.IsMeaningful"/>).</summary>
    NeverPlayed,

    /// <summary>A session is running right now. **Home says so and stops there** — see the
    /// boundary note on <see cref="SessionSummary"/>.</summary>
    InProgress,

    /// <summary>A finished session is the newest thing on record: "where you left off".</summary>
    Ended,
}

/// <summary>
/// One session, as a DESK surface describes it: which character, where, when, how long,
/// and what it moved. Deliberately not a meter.
///
/// **Every combat number is absent BY CONSTRUCTION rather than by discipline** — there is
/// no <c>Dps</c>, no <c>Kills</c>, no <c>Deaths</c>, no damage or healing on this record,
/// so a room holding one cannot render them by reaching for a property. That is the
/// Helm-signed Home/Live boundary (2026-09-05 ~5:20 AM CT) expressed as a type: the
/// temptation it guards against is real, because <c>MainWindow.CurrentSnapshot()</c> is
/// sitting right there with all four, and a kill count on Home would look like a small
/// harmless convenience rather than Live's job arriving early.
/// </summary>
/// <param name="Zone">The session's primary zone, or empty when nothing said.</param>
/// <param name="Elapsed">Wall-clock length of the session.</param>
public sealed record RecentSession(
    RecentSessionState State, string Character, string Server, string Zone,
    DateTime? StartedLocal, DateTime? EndedLocal, TimeSpan Elapsed,
    double XpPercent, long Copper, int LootCount);

/// <summary>
/// "Where you left off" — the last session on record for the character being followed,
/// merged with the one running now so the two cannot be reported as three.
///
/// **It lives in UI.Shared because Home is not its only reader.** Bevel's Home pre-design
/// asks for this explicitly: *"whatever computes 'what you just did' for Home should live
/// in Core/UI.Shared, not inside the Home room's own code, because Live's own PR will need
/// the identical fact once it exists."* A session summary is already split across surfaces
/// — Session History renders the full row, Progress owns the career view — and Home's
/// one-screen version is a THIRD reader of one underlying record. Building it once now is
/// cheaper than Live re-deriving it later and the two drifting the way #202's two snapshot
/// builders did (trap 33).
///
/// **THE MERGE IS THE PART THAT NEEDS A TEST RATHER THAN A HOPE**, and it is
/// <see cref="LevelHistory"/>'s lesson arriving one record up. A session that is running
/// while the widget is up exists in BOTH sources: <c>SessionArchiver</c> checkpoints it
/// into the store under <see cref="SessionRepository.ActiveEndReason"/> on the tick, and
/// the live <c>StatsSnapshot</c> carries it too. Read naively, the newest stored row IS the
/// live session, and Home would render "where you left off" as the sitting the player is
/// in the middle of — which is Live's job, described in the past tense, on the one surface
/// signed not to do it.
///
/// **Nothing here says "x ago".** An age ticks, which changes measured text width on a
/// <c>SizeToContent</c> window (trap 12) and would re-wake every phone on the fingerprint
/// (trap 8). Times are wall clock and durations are fixed strings.
/// </summary>
public static class SessionSummary
{
    /// <summary>
    /// The stored half, scoped to one character — <see cref="LevelHistory.Stored"/>'s rule
    /// applied to the session list, and it is the same rule for the same reason.
    ///
    /// **Empty rather than unscoped when either half is blank.** <c>SessionRepository.Query</c>
    /// treats a null or empty side as "do not filter on it", so passing a blank identity
    /// through would answer with EVERY character's sessions — a "where you left off" about
    /// somebody else, under a heading that says it is about you.
    ///
    /// **And the identity must come from the ARCHIVER**, which is what
    /// <see cref="SessionArchiver.Identity"/> exists to hand over: those are the exact two
    /// strings the rows were WRITTEN under, and <see cref="SessionRepository.Query"/>
    /// compares them with SQL <c>=</c>. The widget sources a character name from the log
    /// filename, which is close enough for a window title and is a query that silently
    /// returns nothing.
    /// </summary>
    public static IReadOnlyList<SessionRow> Stored(
        (string Server, string Character) identity,
        Func<string, string, List<SessionRow>> query) =>
        identity.Server.Length == 0 || identity.Character.Length == 0
            ? [] : query(identity.Server, identity.Character);

    /// <summary>
    /// The one session Home describes, from the store and the live snapshot together.
    ///
    /// Either side may be empty: a fresh profile has only the live session, and a launch
    /// before the first tick has only the store.
    /// </summary>
    public static RecentSession Of(
        (string Server, string Character) identity,
        IReadOnlyList<SessionRow>? stored, StatsSnapshot? live)
    {
        var liveStart = live?.SessionStart;
        if (live is not null && liveStart is not null && SessionRepository.IsMeaningful(live))
            return new RecentSession(
                RecentSessionState.InProgress, identity.Character, identity.Server,
                live.CurrentZone, liveStart, null, live.Elapsed,
                live.XpPercent, live.Copper, live.LootTotal);

        var last = stored is null
            ? null
            : stored.Where(row => !IsTheLiveSession(row, liveStart))
                .OrderByDescending(row => row.StartLocal)
                .FirstOrDefault();
        if (last is null)
            return new RecentSession(
                RecentSessionState.NeverPlayed, identity.Character, identity.Server, "",
                null, null, TimeSpan.Zero, 0, 0, 0);

        return new RecentSession(
            RecentSessionState.Ended, last.Character, last.Server, last.PrimaryZone,
            last.StartLocal, last.EndLocal ?? last.StartLocal,
            TimeSpan.FromSeconds(Math.Max(0, last.ElapsedSeconds)),
            last.XpPercent, last.Copper, last.LootCount);
    }

    /// <summary>
    /// Is this stored row the sitting that is happening right now?
    ///
    /// **Two questions, because either one alone has a hole.** The end reason is the
    /// archiver's own mark and is the reliable answer while the app is up (a reason left
    /// behind by a crash is rewritten to <c>RecoveredAfterCrash</c> at startup, by
    /// <c>MarkInterruptedAsRecovered</c>, before anything reads it). The start-time match
    /// covers the window where the live session has been finalised under a real end reason
    /// but the snapshot has not rolled yet. A second's tolerance because the row travels
    /// through UTC and an ISO string on the way to disk.
    /// </summary>
    private static bool IsTheLiveSession(SessionRow row, DateTime? liveStart) =>
        row.EndReason == SessionRepository.ActiveEndReason
        || (liveStart is { } start && Math.Abs((row.StartLocal - start).TotalSeconds) < 1);

    /// <summary>The block's heading: what this session IS, in one line.</summary>
    public static string Headline(RecentSession session) => session.State switch
    {
        RecentSessionState.InProgress => "Session in progress",
        RecentSessionState.Ended => session.Zone.Length > 0
            ? $"Last session — {session.Zone}"
            : "Last session",
        _ => "No sessions yet",
    };

    /// <summary>
    /// The line under the heading.
    ///
    /// **The in-progress case is a sentence and not a row of numbers, on purpose.** It is
    /// the one state where the meters exist and are moving, and the signed boundary says
    /// Home does not draw them; a "0.0% xp" that is about to be wrong is not a smaller
    /// version of Live, it is a worse one.
    /// </summary>
    public static string Detail(RecentSession session) => session.State switch
    {
        RecentSessionState.InProgress => session.Zone.Length > 0
            ? $"You are playing in {session.Zone} right now. EQBuddy will record it here "
              + "when the session ends."
            : "EQBuddy is following a session right now, and will record it here when it ends.",
        RecentSessionState.Ended => string.Join(" · ", Facts(session)),
        _ => "Nothing has been recorded for this character yet. Play for a few minutes with "
             + "logging on and the session lands here when it ends.",
    };

    /// <summary>
    /// A finished session's facts, in the order a desk surface reads them: when it ended,
    /// how long it ran, then what it moved.
    ///
    /// **Three of the twelve columns <c>SessionRow</c> carries, and the nine left out are
    /// the point.** Kills, deaths, DPS, damage and healing are Live's, per the signed
    /// boundary; they are not on <see cref="RecentSession"/> to be reached for.
    /// </summary>
    public static IEnumerable<string> Facts(RecentSession session)
    {
        if (session.EndedLocal is { } ended) yield return Stamp(ended);
        yield return LevelHistory.FormatGap(session.Elapsed);
        if (session.XpPercent > 0) yield return $"{session.XpPercent:0.#}% xp";
        if (session.Copper > 0) yield return StatsSnapshot.FormatCoin(session.Copper);
        if (session.LootCount > 0)
            yield return $"{session.LootCount} item{(session.LootCount == 1 ? "" : "s")}";
    }

    /// <summary>"Sep 4, 8:14 PM" — a fixed wall-clock stamp, the same shape
    /// <see cref="LevelHistory.Format"/> writes, so a row measures identically on every
    /// tick it is drawn (trap 12).</summary>
    public static string Stamp(DateTime time) => $"{time:MMM d, h:mm tt}";
}
