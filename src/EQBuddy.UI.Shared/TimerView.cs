namespace EQBuddy.UI.Shared;

/// <summary>
/// A countdown and its progress, as one answer — `EqTimer` + `EqProgress` from
/// docs/DesignSystem.md §3, and the reason §6 put Spawns ahead of the widget.
///
/// The Gate 1 audit's worked example was the Spawns window, and its findings were all
/// about this one thing:
///
///  * the countdown and the editable duration carried near-identical weight, so a
///    glanceable value and a configuration field read as two equal columns;
///  * a row with no timer left the countdown blank, so **"unknown" and "nothing" looked
///    the same**;
///  * there was no progress toward respawn anywhere, so "due in 4:21" and "due in 18:31"
///    looked equally urgent.
///
/// All three are answered by giving a timer a STATE rather than a string. The state
/// decides the words, the colour and whether there is a bar at all — so a caller cannot
/// draw an amber countdown with a green bar, and cannot silently render "unknown" as
/// empty.
///
/// Spawns is the first user; mez and charm chips, buff fades, the map's named circles and
/// the widget's Watch card are the rest. Text formatting is not repeated here — that is
/// <see cref="SpawnDurationText"/> and <see cref="Countdown"/>, both already load-bearing.
/// </summary>
/// <summary>WHY a row deliberately runs no countdown. A bool carried this until the Sky
/// work (#109 follow-up) needed a second reason, and one flag meaning two things is
/// trap 4: the word on the row must differ, because the player's next action differs —
/// wait for the instance clock, versus go and kill the trigger.</summary>
public enum TimerSuppression
{
    None,
    /// <summary>A raid-instanced boss: the game's own instance clock, not a camp cycle (#109).</summary>
    RaidInstance,
    /// <summary>eqlwiki's "Triggered": it appears when something else happens, never on a clock.</summary>
    Triggered,
}

public static class TimerView
{
    /// <summary>Due within this many seconds counts as imminent, and it stays imminent
    /// this long past due. Same window the desktop map pulses its circles on
    /// (<c>CompanionMapSource.PulseWindowSeconds</c>) — a player watching a camp on two
    /// surfaces must not see them disagree about what "about to pop" means.</summary>
    public const double ImminentSeconds = 10;

    public enum State
    {
        /// <summary>No timer, and none expected — the resting state of a catalog row.</summary>
        Idle,
        /// <summary>A kill was seen but no respawn time is known, so no countdown can be
        /// run. **This is not the same as Idle**, and the whole point of having a state is
        /// that it cannot be drawn as if it were.</summary>
        Unknown,
        /// <summary>Counting down.</summary>
        Running,
        /// <summary>About to pop.</summary>
        Imminent,
        /// <summary>Due now, or overdue.</summary>
        Due,
        /// <summary>Deliberately not counted — a raid-instanced boss whose respawn is the
        /// game's own instance clock, not a kill-to-respawn camp cycle (#109). Says so
        /// instead of showing a blank that reads as broken.</summary>
        Suppressed,
        /// <summary>Deliberately not counted — a TRIGGERED spawn (eqlwiki's own word): it
        /// appears when the previous link in a chain dies or a particular trash mob is
        /// killed, never on a clock. Distinct from <see cref="Suppressed"/> because the
        /// player's next action is different — go and kill the trigger.</summary>
        Triggered,
    }

    /// <param name="Fraction">Elapsed share of the cycle, 0..1, or null when there is
    /// nothing honest to draw. A null fraction must render as an EMPTY TRACK rather than
    /// no track: the row still has a slot for progress, it just has no claim to make.</param>
    /// <param name="TextColorKey">Ink for the countdown itself.</param>
    /// <param name="FillColorKey">The bar's filled part; null when there is no fill.</param>
    public readonly record struct View(
        State State, double? Fraction, string TextColorKey, string? FillColorKey)
    {
        /// <summary>The empty part of the bar, under the fill. Always drawn, so an unknown
        /// timer and a barely-started one occupy the same space and the list does not
        /// reflow as timers come and go.</summary>
        public string TrackColorKey => "TrackBrush";

        /// <summary>Is there a bar to draw at all? A suppressed or idle row has no cycle,
        /// so a track would be claiming one exists.</summary>
        public bool HasTrack => State is State.Unknown or State.Running or State.Imminent or State.Due;
    }

    /// <summary>
    /// The state of one timer right now.
    /// </summary>
    /// <param name="dueAt">When it pops, or null if a kill was seen but no duration is
    /// known.</param>
    /// <param name="durationSeconds">The full cycle, for the progress fraction. Null or
    /// non-positive means no fraction can be computed — the bar draws an empty track.</param>
    /// <param name="hasTimer">Whether a kill has been seen at all.</param>
    /// <param name="suppression">Why no countdown runs, when one deliberately does not.</param>
    public static View For(DateTime? dueAt, double? durationSeconds, DateTime now,
        bool hasTimer = true, TimerSuppression suppression = TimerSuppression.None)
    {
        if (suppression == TimerSuppression.RaidInstance) return new(State.Suppressed, null, "DimBrush", null);
        if (suppression == TimerSuppression.Triggered) return new(State.Triggered, null, "DimBrush", null);
        if (!hasTimer) return new(State.Idle, null, "DimBrush", null);
        if (dueAt is not { } due) return new(State.Unknown, null, "DimBrush", null);

        var left = (due - now).TotalSeconds;
        var fraction = durationSeconds is { } total && total > 0
            ? Math.Clamp(1 - left / total, 0, 1)
            : (double?)null;

        // Due wears WARN, not BAD. A camp popping is the good news the window exists to
        // deliver; red would make every successful outcome read as a fault. Bad is
        // reserved for the bar fill, where it has to beat the accent for attention at a
        // glance and there is no text competing with it.
        if (left <= 0) return new(State.Due, 1.0, "WarnBrush", "BadBrush");
        if (left <= ImminentSeconds) return new(State.Imminent, fraction, "WarnBrush", "WarnBrush");
        return new(State.Running, fraction, "AccentBrush", "AccentBrush");
    }

    /// <summary>What the countdown READS. "DUE" shouts because it is the one word in the
    /// window worth interrupting for; the rest defer to
    /// <see cref="SpawnDurationText.Countdown"/>. An em dash for unknown — never a blank,
    /// which is the audit's "uncertainty and absence look the same".</summary>
    /// <param name="note">What to name beside a suppressed state — for a triggered spawn,
    /// what brings the mob. Bevel, 2026-08-22: the glance should NAME the trigger rather
    /// than hide it in a tooltip, because "go kill X" is the whole action the row implies.
    /// Empty leaves the bare word, which is what a raid instance gets.</param>
    public static string Text(View view, DateTime? dueAt, DateTime now, string note = "") => view.State switch
    {
        State.Due => "DUE",
        State.Suppressed => "instance",
        State.Triggered => note.Length > 0 ? $"triggered · {note}" : "triggered",
        State.Unknown => "—",
        State.Idle => "",
        _ => SpawnDurationText.Countdown((dueAt ?? now) - now),
    };
}
