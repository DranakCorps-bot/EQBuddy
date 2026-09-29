using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// THE BUFF LENGTH EDITOR's decisions and words (#954, charlesneitzel: "we need to be able
/// to adjust their duration, similar to how we can adjust spawn timers manually").
///
/// **The spawn-timer override, reused rather than re-invented.** A length the player types
/// outranks everything EQBuddy derives, persists, and re-derives the RUNNING countdown from
/// its original landing — <c>SpawnsViewModel.SetDuration</c>'s three rules. The grammar is
/// <see cref="SpawnDurationText"/>'s too ("27m", "1h 30m", "45:00", a bare number is
/// minutes), so the two editors cannot teach a player two ways to type one length.
///
/// **What it is filed under is <see cref="BuffState.DurationKey"/>**, the RANKED name where
/// the log named one — and the editor SAYS that name, because a length typed on the chip
/// "Shield of Thorns" applies to rank V and not to rank IV (trap 71).
///
/// Framework-free, so every sentence here is asserted without a window (docs/TestPlan.md §5).
/// </summary>
public static class BuffLengthEditor
{
    /// <summary>The window's title and heading.</summary>
    public static string Title(BuffState b) => $"Buff length — {b.Label}";

    /// <summary>What EQBuddy's own number is and where it came from — shown whether or not
    /// the player has typed one, so "use EQBuddy's length" never asks them to agree to a
    /// number they cannot see.</summary>
    public static string DerivedLine(BuffState b) =>
        $"EQBuddy's length: {SpawnDurationText.Format(b.DerivedSeconds)}"
        + (b.DerivedEstimated
            ? " (estimated from the catalog — ranks and AAs can make it longer)"
            : " (timed by your own log)");

    /// <summary>The line naming the player's own length, or "" when they have none.</summary>
    public static string TypedLine(double? typedSeconds) => typedSeconds is { } s
        ? $"Your length: {SpawnDurationText.Format(s)} — the countdown is using it."
        : "";

    /// <summary>Which buff a saved length applies to — the key, spelled out.</summary>
    public static string ScopeLine(BuffState b) =>
        $"Applies to every landing of {b.DurationKey} on this character, starting with the one running now.";

    /// <summary>The input's hint.</summary>
    public const string Grammar = "Type a length: 27m, 1h 30m, 45:00 or 90s. A bare number is minutes.";

    /// <summary>What the text box starts with: the player's length if they have one, else
    /// EQBuddy's — the number the countdown is using right now either way.</summary>
    public static string Prefill(BuffState b, double? typedSeconds) =>
        SpawnDurationText.Format(typedSeconds ?? b.DerivedSeconds);

    /// <summary>
    /// Read what the player typed: seconds, or null with <paramref name="error"/> set to the
    /// sentence to show. Refuses rather than guessing — an unreadable entry that silently
    /// cleared the override is #124's defect on the spawn side ("8.5m produced a 16.5 minute
    /// timer"), and the buff editor does not get to repeat it.
    /// </summary>
    public static double? Read(string? text, out string error)
    {
        error = "";
        if (SpawnDurationText.Parse(text) is not { } seconds)
        {
            error = "EQBuddy can't read that as a length — try 27m or 1h 30m.";
            return null;
        }
        if (seconds >= BuffTracker.MaxPlayerSeconds)
        {
            error = "That's a day or more — no buff runs that long.";
            return null;
        }
        return seconds;
    }

    /// <summary>The Save button.</summary>
    public const string Save = "Save";

    /// <summary>The button that clears the player's length.</summary>
    public const string UseDerived = "Use EQBuddy's length";

    /// <summary>Why "Use EQBuddy's length" is dimmed when there is nothing to clear (trap
    /// 17: a disabled control says why).</summary>
    public const string UseDerivedIdle = "You haven't set a length for this buff — it is already using EQBuddy's.";

    /// <summary>The Cancel button.</summary>
    public const string Cancel = "Cancel";
}
