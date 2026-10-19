using System.Globalization;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **Every word the heartbeat puts on screen** — the v2.0.7 note, the Options
/// block, the delete dialog and the "last heartbeat" line. TEL-A's copy (Bevel, DRA-359) as
/// C-1 / Helm ruled it, verbatim from <c>docs/v2/telemetry.md</c> §8.3.
///
/// The words are promises about what leaves a player's machine, and a promise spelled inline
/// in a window is a promise nothing can check. Here they are one file, and
/// <c>TelemetryCopyTests</c> reads each one back out of the requirement page, so a drift
/// between the page Helm signed and the words the player reads reddens the build.
///
/// Markdown in the page (<c>**bold**</c>, list numbers) is layout, not copy: the window draws
/// the emphasis, and the strings here carry the words.
/// </summary>
public static class TelemetryCopy
{
    // ===================================================== A. the v2.0.7 note ====
    // DRA-2010 (Founder order 2, 2026-10-18): telemetry is on for every install, and the
    // player is told so by a NON-BLOCKING note on the widget, never a window that waits. Short
    // on purpose, as DRA-385 ruled for the prompt before it: the body still names all three
    // fields (TEL-001: the copy carries the entire payload), the off line names the toggle by
    // its real path, and nothing says "anonymous" — it lists what is sent instead.

    public const string NoteHeading = "EQBuddy now sends a small check-in";

    public const string NoteBody =
        "While EQBuddy is open it sends a random id, the app version and your Windows version. "
        + "No names, characters, chat, logs or files.";

    /// <summary>The purpose the Founder's order names: the install-base footprint and usage,
    /// so support and resources can be sized.</summary>
    public const string NotePurpose =
        "It shows how many people use EQBuddy and how, so David can plan support and resources.";

    /// <summary>
    /// **The cohort sentence (DRA-2010 §1 protection 2, Challenger condition DRA-2011)**, drawn
    /// only where <c>TelemetryHeartbeat.NoteFor</c> answers <c>Returning</c>. It says THIS
    /// version chose to turn the earlier off back on, and never that the last version failed to
    /// keep it: v2.0.6 saved that choice correctly.
    /// </summary>
    public const string NoteReturning =
        "This version turns it back on even if you said \"Not now\", turned it off or deleted "
        + "your data before. Only turning it off from now on stays off.";

    /// <summary>§8.3.1 row 7's path fill, carried over: the toggle lives in the Behavior block,
    /// which Options and the shell's Settings room both compose.</summary>
    public const string NoteWhereOff =
        "To turn it off, go to Options → Behavior → Help improve EQBuddy. Off there stays off.";

    /// <summary>The note's link text; the target is <see cref="PlayerPageUrl"/>.</summary>
    public const string NoteLearnMore = "Learn more";

    /// <summary>§8.3 §A's target: the player page, <c>docs/Telemetry.md</c> (TEL-PR4, DRA-363,
    /// shipped with v2.0.0).</summary>
    public const string PlayerPageUrl =
        "https://github.com/DranakCorps-bot/EQBuddy/blob/main/docs/Telemetry.md";

    /// <summary>The note's two buttons: a DOOR to the toggle, never a gate — the only off is
    /// the toggle itself (DRA-2010) — and the dismissal, which hides the note for this launch.</summary>
    public const string NoteOpenOptions = "Open Options";
    public const string NoteDismiss = "Got it";

    // ================================================ B. the Options toggle block ====

    public const string ToggleLabel = "Help improve EQBuddy";

    /// <summary>The caption under the toggle, in both states (DRA-2010 §2): on by default, what
    /// it is for, and that an off here is final.</summary>
    public const string ToggleCaption =
        "On by default for every install. It shows how many people use EQBuddy and how, so "
        + "David can plan support and resources. Turning it off here is final: EQBuddy will not "
        + "turn it back on.";

    /// <summary>§8.3.1 rows 2–3: ONE OFF heading, true for a player who never opted in and
    /// for one who opted out (Helm rejected a fourth settings key to tell them apart).</summary>
    public const string OffHeading =
        "Off — nothing is being sent. Heartbeats sent earlier age out within 90 days.";

    public const string OffLead =
        "Turning this ON will start sending small \"heartbeats\" about how the app is being "
        + "used. Each heartbeat carries exactly three fields — nothing else, ever:";

    public static readonly IReadOnlyList<(string Name, string Text)> OffFields =
    [
        ("Install id:", "a random number we create when you turn this on. It is not your "
            + "name, your computer, or your account."),
        ("App version:", "the build number of the EQBuddy you are running."),
        ("Operating system:", "your OS and its version, in the form the system reports it."),
    ];

    public const string OffRetention =
        "That is the entire list. We keep each heartbeat 90 days and then delete it. Turning "
        + "this OFF at any time stops the sends AND deletes that install id from your machine "
        + "— it is gone. If you turn it back on later, we create a new one, so we cannot "
        + "connect it to the old one.";

    public const string OnHeading = "On.";

    public const string OnLead =
        "Each heartbeat (about every 5 minutes while the app runs; nothing on exit) carries "
        + "exactly three fields — nothing else, ever:";

    /// <summary>§8.3.1 row 14: the ON block shows the first 8 hex characters of the id.</summary>
    public static IReadOnlyList<(string Name, string Text)> OnFields(string installIdPrefix) =>
    [
        ("Install id:", installIdPrefix + "… (your random number, created when this was turned "
            + "on; delete it with the toggle or the button below)."),
        ("App version:", "the build number of the EQBuddy you are running."),
        ("Operating system:", "your OS and its version, in the form the system reports it."),
    ];

    /// <summary>The id's first 8 characters, or "" for anything that is not an id.</summary>
    public static string IdPrefix(string? installId) =>
        TelemetryHeartbeat.IsInstallId(installId) ? installId![..8] : "";

    public const string OnRetention =
        "That is the entire list. We keep each heartbeat 90 days and then delete it. Turning "
        + "this OFF stops the sends AND destroys the install id on your machine.";

    public const string OffDoesLabel = "What turning this OFF does:";
    public const string OffDoes =
        "it stops all sending, and it deletes the install id from your machine. You cannot "
        + "re-enable the old id — turning it back ON creates a new one.";

    /// <summary>The page reads <c>**What turning it OFF does NOT do** (say so, don't let it
    /// be a surprise): your past…</c>. The parenthetical is Bevel's note to the implementer,
    /// not a sentence for the player, so it is the one piece of §8.3 not drawn — the label
    /// takes the colon it introduced. Every word after it is verbatim.</summary>
    public const string OffDoesNotLabel = "What turning it OFF does NOT do:";
    public const string OffDoesNot =
        "your past heartbeats already sent on this "
        + "machine stay on our backend until they age out of their 90-day window. We do not "
        + "auto-delete them when you flip this OFF, because we no longer have the id to match "
        + "them against — the id is what we use to find your rows, and it is already gone. If "
        + "you want them gone right now, use Delete my telemetry data below.";

    public const string DeleteButton = "Delete my telemetry data…";

    /// <summary>§8.3.1 row 3: the dimmed button's tooltip while OFF.</summary>
    public const string DeleteDisabledTip =
        "While this is off there is no install id to delete with. Any earlier heartbeats age "
        + "out within 90 days.";

    // ====================================================== C. the delete dialog ====

    public const string DeleteTitle = "Delete my telemetry data?";

    public const string DeleteLead =
        "This deletes the heartbeats EQBuddy has sent from this computer, on our backend. "
        + "What gets deleted:";

    public static readonly IReadOnlyList<string> DeleteItems =
    [
        "Every raw heartbeat we have kept for your install id, including any within the past "
            + "90 days. (Heartbeats older than that are already gone — we auto-delete them at "
            + "90 days.)",
        "Your install id from your machine.",
    ];

    public const string DeleteKeeps =
        "What does NOT get deleted (it is not yours to delete, and it is not about you): the "
        + "aggregate counts (how many distinct installs turned this on, how many are active "
        + "now, the version mix). Those numbers do not contain your id, and they are what the "
        + "public page shows.";

    public const string DeleteAfter =
        "After this, your old install id is gone. If you turn the toggle ON again, we create "
        + "a new one and your fresh heartbeats are not connectable to the old ones.";

    public const string DeleteCancel = "Cancel";
    public const string DeleteConfirm = "Delete. Do it now.";

    public const string Deleted =
        "Deleted. Your id is gone. No more heartbeats from this computer.";

    // ================================================= D. the "last heartbeat" line ====
    // A CLOSED set (§8.3 §D as ruled by §8.3.1 rows 8–12). TelemetryCopyTests pins every
    // string; the view reserves the width of the longest so a tick cannot resize its row
    // (trap 12).

    public const string StatusNoneYet = "On — no heartbeats sent yet";
    public const string StatusSendFailed = "On — last send failed, will try again";
    public const string StatusDeleteFailed = "On — delete did not reach the server, try again";
    public const string StatusLastPrefix = "On — last heartbeat: ";

    /// <summary>
    /// The five relative-time forms, gap-free (§8.3.1 row 9): <c>just now</c> (&lt;1 min) ·
    /// <c>N min ago</c> (1–59) · <c>N hr ago</c> (1–23) · <c>yesterday</c> (24–47 hr) ·
    /// <c>N days ago</c> (≥2 days, floor). A negative span (the clock went backwards) reads
    /// as <c>just now</c> rather than inventing a sixth form.
    /// </summary>
    public static string RelativeTime(TimeSpan ago)
    {
        if (ago < TimeSpan.FromMinutes(1)) return "just now";
        if (ago < TimeSpan.FromHours(1))
            return string.Create(CultureInfo.InvariantCulture, $"{(int)ago.TotalMinutes} min ago");
        if (ago < TimeSpan.FromHours(24))
            return string.Create(CultureInfo.InvariantCulture, $"{(int)ago.TotalHours} hr ago");
        if (ago < TimeSpan.FromHours(48)) return "yesterday";
        return string.Create(CultureInfo.InvariantCulture, $"{(int)ago.TotalDays} days ago");
    }

    /// <summary>What the last event was, for <see cref="StatusLine"/>. Last event wins.</summary>
    public enum LastEvent { None, Sent, SendFailed, DeleteFailed, Deleted }

    /// <summary>
    /// The status line, or null when it does not render (§8.3 §D state 4: OFF draws nothing,
    /// because the OFF block already says it — except straight after a confirmed delete,
    /// where §C's sentence takes the line's place).
    /// </summary>
    public static string? StatusLine(bool enabled, LastEvent last, DateTime? lastSentAt, DateTime now)
    {
        if (!enabled) return last == LastEvent.Deleted ? Deleted : null;
        return last switch
        {
            LastEvent.SendFailed => StatusSendFailed,
            LastEvent.DeleteFailed => StatusDeleteFailed,
            LastEvent.Sent when lastSentAt is { } at => StatusLastPrefix + RelativeTime(now - at),
            _ => StatusNoneYet,
        };
    }

    /// <summary>Every width the line can take, longest forms included, so a view can reserve
    /// the widest (trap 12). The numeric forms use their widest plausible digits.</summary>
    public static readonly IReadOnlyList<string> StatusWidthSamples =
    [
        StatusNoneYet, StatusSendFailed, StatusDeleteFailed, Deleted,
        StatusLastPrefix + "just now", StatusLastPrefix + "59 min ago",
        StatusLastPrefix + "23 hr ago", StatusLastPrefix + "yesterday",
        StatusLastPrefix + "999 days ago",
    ];
}
