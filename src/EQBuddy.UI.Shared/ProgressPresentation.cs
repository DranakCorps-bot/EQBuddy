using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The Progress card's summary (Gate 5b): experience, AAs, skill-ups and the level-ups
/// this session, with how long each took.
///
/// Composed inline in <c>RefreshUi</c> before this, including a per-level loop that walks
/// backwards to the previous ding to work out the gap — the sort of arithmetic that is
/// obviously right until the first level of a session, where "the previous ding" is the
/// session start instead. That case is now a test rather than a hope.
/// </summary>
public static class ProgressPresentation
{
    public static List<string> SummaryLines(StatsSnapshot s)
    {
        var lines = new List<string>
        {
            $"{s.XpTicks} xp gains · {s.XpPerHour:0.0}%/hr · " +
            $"{s.XpPerActiveHour:0.0}% active · {s.SkillUpTotal} skill-ups",
        };
        if (s.Recent is { } recent)
            lines.Add($"Last {(int)recent.Window.TotalMinutes}m: {recent.XpPerHour:0.0}%/hr");
        if (s.AaGained > 0)
            lines.Add($"{s.AaGained} AA point{(s.AaGained == 1 ? "" : "s")} · " +
                      $"{s.AaPerHour:0.0} AA/hr (now {s.AaTotal} unspent)");
        // Motes per hour, as ONE line item (David, 2026-08-23, asked and answered with
        // the question tool: the Experience room, not Wealth). It sits beside the AA line
        // because both are "what this session has accrued that spends", and above the ETA
        // because everything below is the xp forecast rather than a tally.
        //
        // Omitted rather than zeroed when nothing has dropped: this block already omits
        // the AA line and the ETA on the same principle, and "0 motes/hr" reads as a
        // measurement of a camp rather than as "none yet".
        //
        // **The Motes card keeps its own surface** — David: *"keep the separate Motes
        // tracking for people specifically farming motes"* — and the Wealth chip stays
        // coin (Bevel, Helm-signed 2026-08-22). This is a summary of that card, in the
        // room a player already has open, not a replacement for it.
        if (MotesPresentation.RateLine(Motes.Summarize(s.Loot, s.Elapsed)) is { } motes)
            lines.Add(motes);
        if (s.HoursToLevel is { } eta) lines.Add(NextLevelSentence(eta));
        if (Levels(s) is { Length: > 0 } levels) lines.Add(levels);
        return lines;
    }

    /// <summary>Each level-up, when it happened, and how long it took — measured from the
    /// PREVIOUS ding, or from the session start for the first one. Getting that wrong
    /// would report the first level of a session as having taken zero minutes.</summary>
    public static string Levels(StatsSnapshot s)
    {
        if (s.Levels.Count == 0) return "";
        return string.Join(", ", s.Levels.Select((level, i) =>
        {
            var from = i == 0 ? s.SessionStart : s.Levels[i - 1].Time;
            var minutes = from is { } f ? (int)(level.Time - f).TotalMinutes : 0;
            return $"{level.Text} at {level.Time:h:mm tt} ({minutes}m)";
        }));
    }

    /// <summary>"Next level in ~2h 15m at this pace" — the ETA sentence, in ONE place
    /// because the collapsed HUD's xp tooltip says it too now (OE-3).
    ///
    /// It was composed inline above until then, and a second host wording the same
    /// forecast by hand is exactly how two surfaces start disagreeing about one fact
    /// (trap 4). The tooltip asks for the SENTENCE, not for <see cref="FormatEta"/> plus
    /// its own prose.</summary>
    public static string NextLevelSentence(double hours) =>
        $"Next level in {FormatEta(hours)} at this pace";

    /// <summary>"~2h 15m" / "~40m". Hours only once there is at least one, and never
    /// "~0m": a level that is minutes away should say a minute, not nothing.</summary>
    public static string FormatEta(double hours) => hours >= 1
        ? $"~{(int)hours}h {(int)((hours - (int)hours) * 60)}m"
        : $"~{Math.Max(1, (int)(hours * 60))}m";
}
