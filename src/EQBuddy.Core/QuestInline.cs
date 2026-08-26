namespace EQBuddy.Core;

/// <summary>
/// What an INLINE Epic or Sky room shows on the widget — the decisions, so the two
/// desktops draw one rule rather than each re-deciding (#184's lesson; a fourth copy of
/// the checklist arrangement is how that bug happened).
///
/// Bevel's table (Helm-signed 2026-08-22): *"Epic 1.0 (one class, capped) · Plane of Sky
/// (current class, capped)."* So this is deliberately NOT the window's checklist — no
/// search, no state lens, no class picker, no import report (window chrome stays with the
/// window, trap 37): ONE class's rows, capped, read on the widget between pulls, with the
/// ⧉ as the door to the working surface.
///
/// The rows are read-only ON PURPOSE, and it is a design rather than a shortcut: a
/// checkbox drawn at widget scale inside a capped scroller invites ticks the cap may be
/// hiding context for, and a disabled-looking one is trap 17. Ticking lives in the
/// window; the card is the glance-plus.
/// </summary>
public static class QuestInline
{
    /// <summary>Rows before the cap says "... and N more" — a surviving cap must SAY so
    /// (#234's rule; a trimmed list that looks complete cost a bug report to find).</summary>
    public const int RowCap = 12;

    /// <param name="More">Rows the cap withheld — 0 means the slice is complete.</param>
    /// <param name="Note">The one-line state that replaces rows when there are none.</param>
    public sealed record Slice(
        string Heading, IReadOnlyList<QuestChecklistRow> Rows, int More,
        bool Completed, string? Note);

    /// <summary>
    /// The one class's worth of checklist an inline room shows: the first RESOLVED class
    /// with anything to show, else the first class at all — the same "first with
    /// something to SHOW" rule the next-level fold uses (`DefaultOpenIndex`), for the
    /// same reason: opening on an empty group puts a shrug above the content.
    /// </summary>
    public static Slice For(
        IReadOnlyList<QuestChecklistGroup> groups, IReadOnlyCollection<string> classes)
    {
        if (groups.Count == 0)
            return new Slice("", [], 0, false,
                "This checklist fills in from the wiki catalog and your own progress.");

        var scoped = classes.Count == 0
            ? groups
            : [.. groups.Where(g => classes.Contains(g.ClassName, StringComparer.OrdinalIgnoreCase))];
        if (scoped.Count == 0) scoped = groups;

        var pick = scoped.FirstOrDefault(g => g.Rows.Count > 0 && !g.Completed)
            ?? scoped[0];
        var rows = pick.Rows.Take(RowCap).ToList();
        return new Slice(
            Heading: pick.Title.Length > 0 ? $"{pick.ClassName} — {pick.Title}" : pick.ClassName,
            Rows: rows,
            More: Math.Max(0, pick.Rows.Count - rows.Count),
            Completed: pick.Completed,
            Note: pick.Completed ? "completed — reopen it in the Quest Tracker" : null);
    }

    /// <summary>The cap's own sentence — never silent (#234).</summary>
    public static string MoreLine(int more) =>
        more == 1 ? "... and 1 more" : $"... and {more} more";
}
