using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **The ONE policy for the opt-in heartbeat** — consent, the install id's lifecycle, whether
/// the first-open prompt may show, the cadence and its epoch, and the payload
/// (<c>docs/v2/telemetry.md</c> §7, DRA-362 TEL-PR3).
///
/// Pure and framework-free, with no network. The only code that opens a socket is
/// <see cref="TelemetrySender"/>, and it sends nothing this class did not build. **Trap 47
/// binds:** one module decides an off-machine question and every send path goes through it —
/// the periodic tick, an opt-in mid-session and the delete action all ask here first.
///
/// This is the first feature in EQBuddy's history that sends anything off the player's
/// machine, so every rule below is written for a reader with a network monitor.
/// </summary>
public static class TelemetryHeartbeat
{
    /// <summary>
    /// **TEL-002's curated must-list** (trap 34's pair): the serialized heartbeat has exactly
    /// these keys. <c>TelemetryHeartbeatTests</c> serializes a real payload and compares its
    /// key set to this list, and a second test pins the list itself — so a fourth field fails
    /// the build until the signed plan is amended and re-signed.
    /// </summary>
    public static readonly IReadOnlyList<string> PayloadKeys = ["installId", "appVersion", "os"];

    // ------------------------------------------------------------------ the payload ----

    /// <summary>The complete body of every heartbeat (§2). There is no other request body.
    /// The property names are pinned by attribute rather than by a naming policy, so a
    /// serializer option somewhere else cannot rename a key the server checks.</summary>
    public sealed record Payload(
        [property: JsonPropertyName("installId")] string InstallId,
        [property: JsonPropertyName("appVersion")] string AppVersion,
        [property: JsonPropertyName("os")] string Os);

    /// <summary>The delete body (§5): exactly one key.</summary>
    public sealed record DeleteRequest(
        [property: JsonPropertyName("installId")] string InstallId);

    public static string Serialize(Payload payload) => JsonSerializer.Serialize(payload);

    public static string Serialize(DeleteRequest request) => JsonSerializer.Serialize(request);

    /// <summary>The heartbeat for this settings state, or null when nothing may be sent —
    /// off, or on with no id. Null is the answer the scheduler honours; it never falls back
    /// to minting an id on its own, because an id is minted by CONSENT and only by it.</summary>
    public static Payload? PayloadFor(AppSettings settings, string appVersion, string os) =>
        settings.TelemetryEnabled && IsInstallId(settings.TelemetryInstallId)
            ? new Payload(settings.TelemetryInstallId!, appVersion, os)
            : null;

    /// <summary>A fresh install id: a random GUID in lowercase <c>D</c> form (36 chars,
    /// hyphenated) — the only shape the server accepts. Never derived from anything.</summary>
    public static string MintInstallId() => Guid.NewGuid().ToString("D");

    /// <summary>Is this the shape <see cref="MintInstallId"/> produces? A hand-edited
    /// settings.json with anything else in it is treated as NO id, so it is never sent.</summary>
    public static bool IsInstallId(string? id) =>
        id is { Length: 36 }
        && Guid.TryParseExact(id, "D", out _)
        && id == id.ToLowerInvariant();

    /// <summary><c>appVersion</c>: the <c>&lt;Version&gt;</c> from Directory.Build.props as
    /// Major.Minor.Build — no revision, no build metadata.</summary>
    public static string AppVersionFrom(Version version) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}");

    /// <summary><c>os</c>: platform name + Major.Minor.Build. Coarse on purpose — no edition,
    /// no revision, no locale (TEL-002).</summary>
    public static string OsFrom(Version osVersion) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Windows {osVersion.Major}.{osVersion.Minor}.{Math.Max(osVersion.Build, 0)}");

    // ------------------------------------------------------------------- consent ----
    // Every write of the three settings keys is one of these five methods. The caller saves.

    /// <summary>The prompt was put on screen. Set on SHOW (§7, §11): a prompt closed by
    /// killing the app is a decline, never a re-prompt.</summary>
    public static void MarkPromptShown(AppSettings settings) => settings.TelemetryPromptShown = true;

    /// <summary>The prompt's decline — Not now, Esc, ✕, focus-out. Writes the shown flag and
    /// NOTHING else (TEL-001 as amended).</summary>
    public static void Decline(AppSettings settings) => MarkPromptShown(settings);

    /// <summary>Opt in — the prompt's accept or the toggle turned ON. Mints a FRESH id every
    /// time: re-enabling after an opt-out is a new identity (TEL-001).</summary>
    public static void OptIn(AppSettings settings)
    {
        settings.TelemetryPromptShown = true;
        settings.TelemetryEnabled = true;
        settings.TelemetryInstallId = MintInstallId();
    }

    /// <summary>Opt out — the toggle turned OFF. Stops sends and destroys the id in the same
    /// write. Sends NO delete: that would be an off-machine send nobody asked for (§11).</summary>
    public static void OptOut(AppSettings settings)
    {
        settings.TelemetryEnabled = false;
        settings.TelemetryInstallId = null;
    }

    /// <summary>The server confirmed a delete (<c>204</c>). Only then is the id destroyed —
    /// a failed delete changes nothing, so the player can retry with the id that names their
    /// rows (§7).</summary>
    public static void DeleteConfirmed(AppSettings settings) => OptOut(settings);

    /// <summary>The delete body for this state, or null when there is no id to delete with
    /// (§8.3 §B: the button is dimmed while off).</summary>
    public static DeleteRequest? DeleteFor(AppSettings settings) =>
        settings.TelemetryEnabled && IsInstallId(settings.TelemetryInstallId)
            ? new DeleteRequest(settings.TelemetryInstallId!)
            : null;

    // ------------------------------------------------------------ the first open ----

    /// <summary>What happened to the first-open prompt on this launch. The dump's word for
    /// each is <see cref="Word(PromptDecision)"/>.</summary>
    public enum PromptDecision
    {
        /// <summary>Put on screen; the player's answer follows.</summary>
        Show,
        /// <summary>Shown on some earlier launch (any answer, or none). Never again.</summary>
        AlreadyShown,
        /// <summary>An isolated profile (E2E, shoot.ps1) with no scripted answer. A modal
        /// with nobody to answer it would hang the harness.</summary>
        NotTheProductProfile,
        /// <summary>No endpoint is compiled in yet, so there is nothing to consent TO. The
        /// prompt's one showing is not spent on a build that cannot send.</summary>
        NoEndpoint,
        /// <summary>The harness answered "decline" without a window — only ever on an
        /// isolated profile.</summary>
        ScriptedDecline,
    }

    /// <summary>
    /// May the first-open prompt show on this launch?
    ///
    /// <para><b>Once per install, and the flag is the whole memory.</b> There is no version
    /// input on purpose: a version bump cannot re-arm a prompt whose rule does not read a
    /// version. An existing profile has never set the flag on the first build that carries
    /// telemetry, so it sees the prompt once, on that build — the rule working, not a
    /// re-prompt (§7).</para>
    ///
    /// <para><b>The harness hook is inert on a player's machine.</b>
    /// <paramref name="scriptedAnswer"/> is honoured only on a profile that is NOT the
    /// product's own, which no player's is (<see cref="AppPaths.IsProductOwnedProfile"/>),
    /// and it can only ever DECLINE — there is no scripted accept, so no harness run can
    /// opt anything in.</para>
    /// </summary>
    public static PromptDecision DecidePrompt(bool promptShown, bool productProfile,
        bool endpointConfigured, string? scriptedAnswer)
    {
        if (promptShown) return PromptDecision.AlreadyShown;
        if (!productProfile)
            return string.Equals(scriptedAnswer, "decline", StringComparison.OrdinalIgnoreCase)
                ? PromptDecision.ScriptedDecline
                : PromptDecision.NotTheProductProfile;
        return endpointConfigured ? PromptDecision.Show : PromptDecision.NoEndpoint;
    }

    /// <summary>One token per decision, for the space-separated <c>EQBUDDY_EXPAND</c> dump.
    /// Named after the enum so a new member cannot arrive without one.</summary>
    public static string Word(PromptDecision decision) => decision switch
    {
        PromptDecision.Show => "shown",
        PromptDecision.AlreadyShown => "alreadyShown",
        PromptDecision.NotTheProductProfile => "notTheProductProfile",
        PromptDecision.NoEndpoint => "noEndpoint",
        PromptDecision.ScriptedDecline => "declined",
        _ => "unknown",
    };

    /// <summary>
    /// **The whole first-open sequence**, with the window and the disk handed in so the ORDER
    /// is testable: decide; on <see cref="PromptDecision.Show"/> write the shown flag and SAVE
    /// it BEFORE asking — so a kill with the prompt up is a decline, never a re-prompt — then
    /// save the answer. Returns the dump word for what happened.
    /// </summary>
    /// <param name="ask">Puts the prompt on screen; true only for "Send these heartbeats".</param>
    /// <param name="save">Persists <paramref name="settings"/> (<c>AppSettings.Save</c>).</param>
    public static string RunFirstOpen(AppSettings settings, bool productProfile,
        bool endpointConfigured, string? scriptedAnswer, Func<bool> ask, Action save)
    {
        var decision = DecidePrompt(settings.TelemetryPromptShown, productProfile,
            endpointConfigured, scriptedAnswer);
        switch (decision)
        {
            case PromptDecision.ScriptedDecline:
                Decline(settings);
                save();
                return Word(decision);
            case PromptDecision.Show:
                MarkPromptShown(settings);
                save();
                if (!ask()) return "declined";
                OptIn(settings);
                save();
                return "accepted";
            default:
                return Word(decision);
        }
    }

    // ------------------------------------------------------------ may this send? ----

    /// <summary>
    /// May a heartbeat leave this process at all? Consent, an id, an endpoint — and a
    /// PRODUCT profile. <b>An isolated profile never sends</b>, whatever its settings say:
    /// a CI run, a screenshot batch or a staged ON shot must never put a heartbeat on the
    /// public numbers. Fail-closed, so a harness that forgets to opt out cannot leak one.
    /// </summary>
    public static bool MaySend(AppSettings settings, bool productProfile, bool endpointConfigured) =>
        productProfile && endpointConfigured
        && settings.TelemetryEnabled && IsInstallId(settings.TelemetryInstallId);
}

/// <summary>
/// **The cadence (§3), as a clock the caller supplies** — so every rule here is testable
/// without waiting and without a timer.
///
/// One heartbeat ~2 minutes after the EPOCH (launch with telemetry on, or the moment of
/// opt-in — never <see cref="DateTime.MinValue"/>, trap 47), then every 5 minutes. A failed
/// beat is DROPPED, never retried or queued: a heartbeat means "running now", so a late one
/// is a wrong one. The signed plan's "bounded backoff" slows the NEXT tick instead — each
/// consecutive failure doubles the interval (5 → 10 → 20 → 40 → 60 min cap) and the first
/// success resets it. All of it lives in memory; a relaunch starts at the ordinary dwell.
/// </summary>
public sealed class TelemetrySchedule
{
    public static readonly TimeSpan LaunchDwell = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan BackoffCap = TimeSpan.FromMinutes(60);

    /// <summary>When the next heartbeat is due, or null while disarmed (telemetry off).</summary>
    public DateTime? NextDue { get; private set; }

    /// <summary>Consecutive failures since the last success. Drives the backoff.</summary>
    public int ConsecutiveFailures { get; private set; }

    /// <summary>Is a send in flight? The scheduler never starts a second one.</summary>
    public bool InFlight { get; private set; }

    /// <summary>Start the clock: the first beat is <see cref="LaunchDwell"/> after
    /// <paramref name="epoch"/>. Resets the backoff — a fresh consent is a fresh start.</summary>
    public void Arm(DateTime epoch)
    {
        NextDue = epoch + LaunchDwell;
        ConsecutiveFailures = 0;
    }

    /// <summary>Opt-out or delete: no further send in this process until re-armed.</summary>
    public void Disarm()
    {
        NextDue = null;
        ConsecutiveFailures = 0;
    }

    public bool IsDue(DateTime now) => !InFlight && NextDue is { } due && now >= due;

    /// <summary>A send has started. Clears the due time so a slow request is never doubled;
    /// the outcome re-arms it.</summary>
    public void Started()
    {
        InFlight = true;
        NextDue = null;
    }

    /// <summary>The send answered <c>204</c>. Next beat one ordinary interval from now.
    /// <paramref name="stillArmed"/> false means the player opted out while it was in
    /// flight — the answer is recorded, and nothing is re-armed.</summary>
    public void Succeeded(DateTime now, bool stillArmed)
    {
        InFlight = false;
        ConsecutiveFailures = 0;
        NextDue = stillArmed ? now + Interval : null;
    }

    /// <summary>The send failed — network, timeout, 4xx, 5xx alike. The beat is dropped; the
    /// NEXT one waits <see cref="BackoffAfter"/> the new failure count.</summary>
    public void Failed(DateTime now, bool stillArmed)
    {
        InFlight = false;
        ConsecutiveFailures++;
        NextDue = stillArmed ? now + BackoffAfter(ConsecutiveFailures) : null;
    }

    /// <summary>The wait after <paramref name="failures"/> consecutive failures: 5 minutes
    /// doubled per failure, capped at 60 — so a dead endpoint costs one request an hour.</summary>
    public static TimeSpan BackoffAfter(int failures)
    {
        if (failures <= 0) return Interval;
        var minutes = Interval.TotalMinutes * Math.Pow(2, Math.Min(failures, 16));
        return TimeSpan.FromMinutes(Math.Min(minutes, BackoffCap.TotalMinutes));
    }
}
