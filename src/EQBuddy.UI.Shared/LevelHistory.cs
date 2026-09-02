using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Every level-up EQBuddy has ever seen for one character, newest first — the durable
/// answer to #240 (joeymavity: *"leveling timestamps in an xp dropdown, I can't find it
/// now"*).
///
/// The only level timestamps the app has ever drawn are <see cref="ProgressPresentation.
/// Levels"/>'s one prose line, and that line is SESSION-scoped: <c>SessionStats</c> clears
/// its dings on the 60-minute session roll, so a fresh evening with no ding has no line at
/// all and there is nothing to find. The durable record existed only as Session History's
/// step chart, which reports a count and a date range and never the times.
///
/// This merges the two sources the app already has — every stored session's mined dings
/// (<see cref="SessionRepository.ProgressSeries"/>, the one SQLite reader; do not add a
/// second miner) and the live session's <see cref="StatsSnapshot.Levels"/> — into one list
/// of rows that three surfaces render identically.
///
/// **The merge is the part that needs a test rather than a hope.** A session finalised
/// while the widget is up is in BOTH sources for one tick: the ding lands in
/// <c>s.Levels</c> at parse time and in the store only when the session is written. So the
/// same level-up would appear twice, once, on exactly the surface a player opened to check
/// when they dinged.
///
/// **Nothing here says "x ago".** An age ticks, which changes measured text width on a
/// <c>SizeToContent</c> window (trap 12) and would re-wake every phone on the fingerprint
/// (trap 8). Times are wall-clock and gaps are fixed strings.
/// </summary>
public static class LevelHistory
{
    /// <summary>One level-up: the level reached, when it happened (local wall clock), and
    /// the wall-clock gap from the PREVIOUS level-up across sessions — null for the oldest
    /// row, which has no previous to measure from.
    ///
    /// <see cref="SincePrevious"/> is deliberately wall clock rather than played time:
    /// summing played time would need per-session elapsed that the miner does not read,
    /// and a number that claims more than it knows is the trap-50 shape. It is labelled
    /// "since previous" everywhere it is shown, never "time in level".</summary>
    public sealed record Row(int Level, DateTime Time, TimeSpan? SincePrevious);

    /// <summary>The prefix <c>SessionStats</c> writes into a level <see cref="TimedDetail"/>
    /// ("Level 24") and <see cref="SessionRepository.ProgressSeries"/> parses back out.
    /// Named once so the live half of the merge reads the store's own convention rather
    /// than a second copy of it.</summary>
    private const string LevelPrefix = "Level ";

    /// <summary>
    /// Every ding from the stored sessions plus the live one, de-duplicated on
    /// (level, time), newest first, each carrying its gap from the one before it.
    ///
    /// Either side may be empty: a new profile has only the live session, and a fresh
    /// launch after a session roll has only the store.
    /// </summary>
    public static List<Row> Rows(
        IReadOnlyList<SessionRepository.ProgressPoint>? stored, StatsSnapshot? live)
    {
        var seen = new HashSet<(int, DateTime)>();
        var dings = new List<(int Level, DateTime Time)>();

        void Add(int level, DateTime time)
        {
            if (seen.Add((level, time))) dings.Add((level, time));
        }

        if (stored is not null)
            foreach (var point in stored)
                foreach (var (time, level) in point.Dings)
                    Add(level, time);

        if (live is not null)
            foreach (var detail in live.Levels)
                if (ParseLevel(detail.Text) is { } level)
                    Add(level, detail.Time);

        // Chronological first, because SincePrevious is defined against the ding BEFORE
        // this one and the two sources arrive interleaved; reversed at the end so the row
        // a player wants — the last ding — is the one at the top.
        dings.Sort((a, b) => a.Time.CompareTo(b.Time));

        var rows = new List<Row>(dings.Count);
        for (var i = 0; i < dings.Count; i++)
            rows.Add(new Row(
                dings[i].Level, dings[i].Time,
                i == 0 ? null : dings[i].Time - dings[i - 1].Time));
        rows.Reverse();
        return rows;
    }

    /// <summary>The number in "Level 24", or null for anything else — the live snapshot's
    /// <c>Levels</c> list is display text, and only rows shaped like a level belong here.</summary>
    private static int? ParseLevel(string? text) =>
        text is not null
        && text.StartsWith(LevelPrefix, StringComparison.Ordinal)
        && int.TryParse(text[LevelPrefix.Length..], out var level)
            ? level : null;

    /// <summary>
    /// The stored half of <see cref="Rows"/>, scoped to one character — every archived
    /// session's mined dings, or nothing at all while no character is being followed.
    ///
    /// **The identity must come from the ARCHIVER**, which is what
    /// <see cref="SessionArchiver.Identity"/> exists to hand over: those are the exact two
    /// strings the rows were written under, and <see cref="SessionRepository.ProgressSeries"/>
    /// compares them with SQL <c>=</c>. The two desktop lanes source a character name
    /// differently (WPF from the log FILENAME, Avalonia from the parsed log), so "close
    /// enough" is a query that silently returns nothing.
    ///
    /// **Empty rather than unscoped when either half is blank.** `ProgressSeries` treats a
    /// null or empty side as "do not filter on it", so passing one through would answer
    /// with EVERY character's dings — a list about nobody, rendered under a heading that
    /// says it is about you. That is the failure this method exists to make impossible to
    /// re-derive per lane; both widgets call it, and the phone (PR 2) is the third caller.
    /// </summary>
    public static IReadOnlyList<SessionRepository.ProgressPoint> Stored(
        (string Server, string Character) identity,
        Func<string, string, List<SessionRepository.ProgressPoint>> series) =>
        identity.Server.Length == 0 || identity.Character.Length == 0
            ? [] : series(identity.Server, identity.Character);

    /// <summary>A row's name column: "Level 24".</summary>
    public static string Name(Row row) => $"{LevelPrefix}{row.Level}";

    /// <summary>A row's value column: "Aug 23, 8:14 PM". A fixed wall-clock stamp, so a
    /// row measures the same width on every tick it is drawn (trap 12).</summary>
    public static string Format(DateTime time) => $"{time:MMM d, h:mm tt}";

    /// <summary>"1d 3h" / "3h 20m" / "43m" — a gap, never an age. The largest two units,
    /// with a zero second unit dropped ("2d" rather than "2d 0h"), and anything under a
    /// minute as "&lt;1m" rather than a "0m" that reads as a measurement.</summary>
    public static string FormatGap(TimeSpan gap)
    {
        if (gap < TimeSpan.Zero) gap = TimeSpan.Zero;
        if (gap.Days > 0)
            return gap.Hours > 0 ? $"{gap.Days}d {gap.Hours}h" : $"{gap.Days}d";
        if (gap.Hours > 0)
            return gap.Minutes > 0 ? $"{gap.Hours}h {gap.Minutes}m" : $"{gap.Hours}h";
        return gap.Minutes > 0 ? $"{gap.Minutes}m" : "<1m";
    }

    /// <summary>The hover for a row, or null for the oldest one. Bevel's call, 2026-09-02:
    /// the gap lives in the TOOLTIP only — not as a third token on the row, which would
    /// spend the card's 320 budget on the least-read of the three facts.</summary>
    public static string? Tooltip(Row row) =>
        row.SincePrevious is { } gap ? $"{FormatGap(gap)} since the previous level-up" : null;

    /// <summary>The folded expander's label: "Level-ups (17) · last Aug 23". The count and
    /// the last ding's date are on the label BECAUSE the fold is closed by default — a
    /// veteran's list is long and the theme body's floor is 320 — so the glance answers
    /// "when did I last ding" without unfolding anything. Empty for no rows, and the fold
    /// is hidden entirely then: no heading over nothing.</summary>
    public static string FoldLabel(IReadOnlyList<Row> rows) =>
        rows.Count == 0 ? "" : $"Level-ups ({rows.Count}) · last {rows[0].Time:MMM d}";

    /// <summary>The rows as a card list — one place the three surfaces get their columns
    /// from, so the phone's projection can be asserted against the desktop's rows
    /// (<c>SurfaceParityTests</c>) rather than hand-rolling the same two strings.</summary>
    public static IEnumerable<(string Name, string Value)> CardRows(IEnumerable<Row> rows) =>
        rows.Select(r => (Name(r), Format(r.Time)));
}
