using System.Globalization;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **Every word the opt-in heartbeat puts on screen** — the first-open prompt, the Options
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
    // ======================================================== A. first-open prompt ====

    public const string PromptTitle = "Help make EQBuddy better? (optional)";

    public const string PromptLead =
        "When this is on, EQBuddy sends a small \"heartbeat\" about how it is being used, "
        + "roughly every 5 minutes while the app runs, and once more if you press Delete my "
        + "telemetry data. That is the only time it sends anything.";

    public const string PromptListLead =
        "Exactly three fields are in each heartbeat — nothing else, ever:";

    /// <summary>The prompt's three rows: the field's plain name, then its sentence.</summary>
    public static readonly IReadOnlyList<(string Name, string Text)> PromptFields =
    [
        ("Install id", "a random number we create when you turn this on. It is not your name, "
            + "computer, or account, and we cannot work backwards from it to you."),
        ("App version", "the build number of EQBuddy you are running."),
        ("Operating system", "your OS and its version, in the form the system reports it."),
    ];

    public const string PromptRetention =
        "That is the entire list. We keep each heartbeat for 90 days and then delete it. "
        + "Aggregate counts of distinct installs (not your id, not your name) are what we use "
        + "to size the backend. Your id is kept on your machine and in the heartbeats we "
        + "store. It is how Delete finds your rows, and it is not linked to your name, "
        + "computer or account.";

    public const string PromptOffLater =
        "If you turn this off later, we delete the id from your machine and stop sending. "
        + "Your past heartbeats stay until they age out.";

    public const string PromptOptional =
        "This is optional. EQBuddy works fully without it, exactly as it works today.";

    /// <summary>§8.3.1 row 7: the footnote names the REAL path of the day. The toggle lives
    /// in the Behavior block, which Options and the shell's Settings room both compose.</summary>
    public const string PromptFootnote =
        "This prompt appears once. If you decline, nothing changes on your machine and we "
        + "won't ask again. You can turn this on any time from Options → Behavior → Help "
        + "improve EQBuddy.";

    /// <summary>§8.3.1 row 6. The link text, and the target it opens.</summary>
    public const string PromptLinkLead = "Everything about it, and how to delete it:";

    /// <summary>Until TEL-PR4 ships <c>docs/Telemetry.md</c>, the requirement page (§8.3 §A).</summary>
    public const string RequirementPageUrl =
        "https://github.com/DranakCorps-bot/EQBuddy/blob/main/docs/v2/telemetry.md";

    /// <summary>§8.3.1 row 5: TEL-001's own label.</summary>
    public const string PromptDecline = "Not now";
    public const string PromptAccept = "Send these heartbeats";

    // ================================================ B. the Options toggle block ====

    public const string ToggleLabel = "Help improve EQBuddy";

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
        ("Install id:", installIdPrefix + "… (your random number, created when you turned "
            + "this on; delete it with the toggle or the button below)."),
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
