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
/// The same session as <see cref="RecentSession"/> describes, read by the room whose job
/// the meters ARE — <c>LiveRoom</c>'s session-report block.
///
/// **A SIBLING RECORD RATHER THAN A WIDER <see cref="RecentSession"/>, and that is a
/// signed requirement rather than a preference** (Bevel's Live pre-design §2, Helm-signed
/// 2026-09-05 ~6:35 AM CT). <c>RecentSession</c> is combat-field-free BY TEST —
/// <c>HomeRoomTests.TheRecentSessionRecordCarriesNoCombatNumbersToRender</c> reads it by
/// reflection and fails the build if <c>Dps</c>, <c>Kills</c>, <c>Deaths</c>,
/// <c>Damage</c> or <c>Healing</c> appears on it — because that is the Home/Live boundary
/// expressed as a type. Live's need for those numbers is real and is exactly what that
/// test was written to keep OFF Home; satisfying it by widening the record would delete
/// the boundary rather than cross it.
///
/// **What is NOT duplicated is the part that would actually drift.** Both records are
/// built from one <see cref="SessionSummary.Pick"/> — the decision about WHICH session
/// this is, which is the hard half (a running sitting exists in the store and in the live
/// snapshot at once, and read naively the newest stored row IS the live one). Two
/// independent derivations of that would disagree at exactly the boundary a race exposes,
/// which is trap 33 one level up from data into records.
///
/// **Three of <c>SessionRow</c>'s twelve columns, and the two left out are deliberate.**
/// Damage and healing totals are not on the stored row at all, so a record carrying them
/// would answer honestly for a running session and zero for a finished one — a field that
/// is only sometimes true is worse than one that is absent. Those two are per-tab facts
/// read straight off the snapshot by the tab that shows them, not session-report facts.
/// </summary>
public sealed record LiveSession(
    RecentSessionState State, string Character, string Server, string Zone,
    DateTime? StartedLocal, DateTime? EndedLocal, TimeSpan Elapsed,
    int Kills, int Deaths, double Dps);

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
/// <summary>Which session the store and the live snapshot agree on, and which of the two
/// carries its numbers. <see cref="Row"/> and <see cref="Live"/> are mutually exclusive by
/// construction — see <see cref="SessionSummary.Pick"/>.</summary>
public readonly record struct SessionPick(
    RecentSessionState State, SessionRow? Row, StatsSnapshot? Live);

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
    /// WHICH session this is — the one answer both records are built from.
    ///
    /// **This is the part that had to be factored, and the fields are the part that did
    /// not.** Bevel's Live pre-design §2 puts it exactly: *"`SessionSummary.Of`'s hard part
    /// is not the fields, it is the MERGE"*. A sitting that is running exists in BOTH
    /// sources at once — <c>SessionArchiver</c> checkpoints it into the store under
    /// <see cref="SessionRepository.ActiveEndReason"/> on the tick, and the live snapshot
    /// carries it too — so "the newest stored row" is the running session until something
    /// says otherwise. <see cref="IsTheLiveSession"/> is that something, with its
    /// end-reason check and its one-second tolerance, and a second room re-deriving it
    /// would drift from this one at exactly the boundary a race exposes.
    ///
    /// Either side may be empty: a fresh profile has only the live session, and a launch
    /// before the first tick has only the store.
    ///
    /// <see cref="SessionPick.Row"/> is set only for <see cref="RecentSessionState.Ended"/>
    /// and <see cref="SessionPick.Live"/> only for <see cref="RecentSessionState.InProgress"/>,
    /// so a caller cannot read the wrong source for the state it was handed.
    /// </summary>
    public static SessionPick Pick(IReadOnlyList<SessionRow>? stored, StatsSnapshot? live)
    {
        var liveStart = live?.SessionStart;
        if (live is not null && liveStart is not null && SessionRepository.IsMeaningful(live))
            return new SessionPick(RecentSessionState.InProgress, null, live);

        var last = stored is null
            ? null
            : stored.Where(row => !IsTheLiveSession(row, liveStart))
                .OrderByDescending(row => row.StartLocal)
                .FirstOrDefault();
        return last is null
            ? new SessionPick(RecentSessionState.NeverPlayed, null, null)
            : new SessionPick(RecentSessionState.Ended, last, null);
    }

    /// <summary>
    /// The one session Home describes, from the store and the live snapshot together.
    /// </summary>
    public static RecentSession Of(
        (string Server, string Character) identity,
        IReadOnlyList<SessionRow>? stored, StatsSnapshot? live)
    {
        var pick = Pick(stored, live);
        if (pick is { State: RecentSessionState.InProgress, Live: { } s })
            return new RecentSession(
                RecentSessionState.InProgress, identity.Character, identity.Server,
                s.CurrentZone, s.SessionStart, null, s.Elapsed,
                s.XpPercent, s.Copper, s.LootTotal);

        if (pick.Row is not { } last)
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
    /// The same session, as the room that draws the meters needs it — <see cref="Of"/>'s
    /// sibling, built from the same <see cref="Pick"/> so the two can never disagree about
    /// which sitting they are describing.
    ///
    /// **The numbers come from the SOURCE the pick named**, never from whichever one
    /// happens to be non-null: a running session's counts are the snapshot's, and a
    /// finished one's are the stored row's. Reading the live snapshot for an
    /// <see cref="RecentSessionState.Ended"/> state would report the sitting the player is
    /// NOT in, under a heading saying they had finished it.
    /// </summary>
    public static LiveSession LiveOf(
        (string Server, string Character) identity,
        IReadOnlyList<SessionRow>? stored, StatsSnapshot? live)
    {
        var pick = Pick(stored, live);
        if (pick is { State: RecentSessionState.InProgress, Live: { } s })
            return new LiveSession(
                RecentSessionState.InProgress, identity.Character, identity.Server,
                s.CurrentZone, s.SessionStart, null, s.Elapsed,
                s.YourKillCount, s.Deaths.Count, s.SessionDps);

        if (pick.Row is not { } last)
            return new LiveSession(
                RecentSessionState.NeverPlayed, identity.Character, identity.Server, "",
                null, null, TimeSpan.Zero, 0, 0, 0);

        return new LiveSession(
            RecentSessionState.Ended, last.Character, last.Server, last.PrimaryZone,
            last.StartLocal, last.EndLocal ?? last.StartLocal,
            TimeSpan.FromSeconds(Math.Max(0, last.ElapsedSeconds)),
            last.Kills, last.Deaths, last.Dps);
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
