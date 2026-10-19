using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **The ONE policy for the heartbeat** — consent, the install id's lifecycle, the v2.0.7
/// switch-on and its note, the cadence and its epoch, and the payload
/// (<c>docs/v2/telemetry.md</c> §7, DRA-362 TEL-PR3).
///
/// <para><b>On for every install since DRA-2010 (Founder, 2026-10-18)</b>, replacing
/// DRA-1980's notice-before-send. There is no prompt and no notice window: the first product
/// launch of a build carrying DRA-2010 turns an off profile ON
/// (<see cref="DecideSwitchOn"/>), records that it did (<c>TelemetryOnByDefaultApplied</c>),
/// and the widget shows a non-blocking one-time note (<see cref="NoteFor"/>). After that the
/// Options toggle is the only off, and it is final. An old "Not now", a v2.0.6 off and a
/// confirmed delete wrote the same bytes, so all three are switched on (plan §1, logged in
/// <c>DECISIONS.md</c>); the first beat after such a switch-on is held one cadence on every
/// launch until it is sent (<see cref="EpochFor"/>).</para>
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
    // Every write of the five settings keys is one of these methods. The caller saves.

    /// <summary>Opt in — the toggle turned ON. Mints a FRESH id every time: re-enabling after
    /// an opt-out is a new identity (TEL-001). Holds no beat: a player who just turned it on
    /// gets DRA-381's immediate one.</summary>
    public static void OptIn(AppSettings settings)
    {
        settings.TelemetryPromptShown = true;
        settings.TelemetryEnabled = true;
        settings.TelemetryInstallId = MintInstallId();
    }

    /// <summary>Opt out — the toggle turned OFF, or a confirmed delete. Stops sends, destroys
    /// the id and drops a held first beat in the same write (DRA-2010 §1: the off is final, so
    /// there is no beat left to hold). Sends NO delete: that would be an off-machine send nobody
    /// asked for (§11).</summary>
    public static void OptOut(AppSettings settings)
    {
        settings.TelemetryEnabled = false;
        settings.TelemetryInstallId = null;
        settings.TelemetryFirstBeatHeld = false;
    }

    /// <summary>
    /// **The Options toggle, in the order that makes OFF immediate.** Turning it off writes
    /// <see cref="OptOut"/> and DISARMS <paramref name="schedule"/> before <paramref name="save"/>
    /// runs, so no beat can be due — and a beat already in flight cannot re-arm, because
    /// <see cref="MaySend"/> already answers false — by the time the write returns. Turning it
    /// on writes <see cref="OptIn"/>; arming is the caller's, after the save, because only the
    /// caller owns the timer. Returns false, writing nothing, when the switch did not move.
    /// </summary>
    public static bool SetEnabled(AppSettings settings, TelemetrySchedule schedule, bool on, Action save)
    {
        if (on == settings.TelemetryEnabled) return false;
        if (on) OptIn(settings);
        else
        {
            OptOut(settings);
            schedule.Disarm();
        }
        save();
        return true;
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

    // ------------------------------------------------- the v2.0.7 switch-on ----

    /// <summary>What the switch-on did on this launch (DRA-2010 §1). The dump's word for each
    /// is <see cref="Word(SwitchOnDecision)"/>.</summary>
    public enum SwitchOnDecision
    {
        /// <summary>The profile was OFF and has not been through the switch-on: turned ON with a
        /// fresh id, the first beat held, the key written, the note shown.</summary>
        SwitchedOn,
        /// <summary>The profile was already on: nothing changes but the key, and the note shows
        /// once. No beat is held — it was sending yesterday.</summary>
        AlreadyOn,
        /// <summary>The switch-on happened on an earlier launch. The setting is the player's: an
        /// off here stays off for good.</summary>
        AlreadyApplied,
        /// <summary>An isolated profile (E2E, shoot.ps1, unit runs). Never switched on, the key
        /// is never written, and no note shows.</summary>
        NotTheProductProfile,
        /// <summary>No endpoint is compiled in, so nothing can send. The key stays false so the
        /// one switch-on is not spent on a build that cannot send.</summary>
        NoEndpoint,
    }

    /// <summary>
    /// **The whole switch-on rule, in one place** (DRA-2010 §1). The isolated and no-endpoint
    /// arms come first, so neither can ever reach a write. Then the key decides: true means
    /// this profile's setting is the player's own and nothing here touches it. False with
    /// telemetry off — whatever wrote the off (an old "Not now", a v2.0.6 notice's Turn off or
    /// Options toggle, a confirmed delete, or never having answered) — is switched on, because
    /// those wrote the same bytes and cannot be told apart. "On" means on with a real id: an
    /// enabled flag with no usable id sends nothing today, so it is switched on too.
    /// </summary>
    public static SwitchOnDecision DecideSwitchOn(AppSettings settings, bool productProfile,
        bool endpointConfigured)
    {
        if (!productProfile) return SwitchOnDecision.NotTheProductProfile;
        if (!endpointConfigured) return SwitchOnDecision.NoEndpoint;
        if (settings.TelemetryOnByDefaultApplied) return SwitchOnDecision.AlreadyApplied;
        return settings.TelemetryEnabled && IsInstallId(settings.TelemetryInstallId)
            ? SwitchOnDecision.AlreadyOn
            : SwitchOnDecision.SwitchedOn;
    }

    /// <summary>One token per decision, for the space-separated <c>EQBUDDY_EXPAND</c> dump.
    /// Named after the enum so a new member cannot arrive without one.</summary>
    public static string Word(SwitchOnDecision decision) => decision switch
    {
        SwitchOnDecision.SwitchedOn => "switchedOn",
        SwitchOnDecision.AlreadyOn => "alreadyOn",
        SwitchOnDecision.AlreadyApplied => "alreadyApplied",
        SwitchOnDecision.NotTheProductProfile => "notTheProductProfile",
        SwitchOnDecision.NoEndpoint => "noEndpoint",
        _ => "unknown",
    };

    /// <summary>
    /// **Run the switch-on for this launch, with the disk handed in so the ONE save is
    /// testable.** <see cref="SwitchOnDecision.SwitchedOn"/> writes telemetry on, a fresh id,
    /// the held first beat and the key together; <see cref="SwitchOnDecision.AlreadyOn"/> writes
    /// the key alone. Every other decision writes nothing and does not save.
    /// <c>TelemetryPromptShown</c> is never written here: it still says whether this profile had
    /// answered before, which is what the note's cohort sentence reads (§1 protection 2).
    /// </summary>
    /// <param name="save">Persists <paramref name="settings"/> (<c>AppSettings.Save</c>).</param>
    public static SwitchOnDecision RunSwitchOn(AppSettings settings, bool productProfile,
        bool endpointConfigured, Action save)
    {
        var decision = DecideSwitchOn(settings, productProfile, endpointConfigured);
        switch (decision)
        {
            case SwitchOnDecision.SwitchedOn:
                settings.TelemetryEnabled = true;
                settings.TelemetryInstallId = MintInstallId();
                settings.TelemetryFirstBeatHeld = true;
                settings.TelemetryOnByDefaultApplied = true;
                save();
                break;
            case SwitchOnDecision.AlreadyOn:
                settings.TelemetryOnByDefaultApplied = true;
                save();
                break;
        }
        return decision;
    }

    /// <summary>Which form of the one-time note this launch draws.</summary>
    public enum NoteKind
    {
        None,
        /// <summary>Telemetry is on, what it is for, what it sends, where to turn it off.</summary>
        Generic,
        /// <summary>The generic note plus the cohort sentence: this version turns earlier offs
        /// back on (§1 protection 2).</summary>
        Returning,
    }

    /// <summary>
    /// **Does the note show on this launch, and which form** (DRA-2010 §2). Once at the
    /// switch-on — either kind — and then on EVERY launch while the first beat is held, so it
    /// does not leave before the player could act on it (§1 protection 1). Dismissing it hides
    /// it for that launch only, which is the host's in-memory business, never a write.
    ///
    /// <para><b>The cohort sentence is gated on <c>TelemetryPromptShown</c></b>, which only an
    /// answer ever set: a past "Not now", a v2.0.6 off or a delete. A never-answered install
    /// gets the generic note, and so does one that was already on (its flag is true from the
    /// yes, but nothing of its was turned back on).</para>
    /// </summary>
    public static NoteKind NoteFor(SwitchOnDecision decision, AppSettings settings)
    {
        var cohort = settings.TelemetryPromptShown ? NoteKind.Returning : NoteKind.Generic;
        return decision switch
        {
            SwitchOnDecision.SwitchedOn => cohort,
            SwitchOnDecision.AlreadyOn => NoteKind.Generic,
            SwitchOnDecision.AlreadyApplied when settings.TelemetryFirstBeatHeld
                && settings.TelemetryEnabled => cohort,
            _ => NoteKind.None,
        };
    }

    /// <summary>The dump's word for a note kind.</summary>
    public static string Word(NoteKind kind) => kind switch
    {
        NoteKind.Generic => "generic",
        NoteKind.Returning => "returning",
        _ => "none",
    };

    // ------------------------------------------------------ the held first beat ----

    /// <summary>
    /// **When the first beat of this process is due** (DRA-2010 §1 protection 1, Challenger
    /// condition DRA-2011). Ordinarily AT the launch (DRA-381). While the first beat after a
    /// switch-on from off is held, one cadence after the launch instead — on every launch,
    /// because the hold is on disk — so a restart straight after the update sends nothing at
    /// once. Nothing else about the schedule changes.
    /// </summary>
    public static DateTime EpochFor(AppSettings settings, DateTime launch) =>
        settings.TelemetryFirstBeatHeld ? launch + TelemetrySchedule.Interval : launch;

    /// <summary>A heartbeat the server ACCEPTED: the hold is over. Returns whether anything
    /// changed, so the caller saves only on the beat that released it. Written on the accepted
    /// answer and never on a send's start: a beat that fails or is refused has not gone, and the
    /// note keeps showing.</summary>
    public static bool FirstBeatSent(AppSettings settings)
    {
        if (!settings.TelemetryFirstBeatHeld) return false;
        settings.TelemetryFirstBeatHeld = false;
        return true;
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
/// One heartbeat IMMEDIATELY at the EPOCH (launch with telemetry on, or the moment of
/// opt-in — never <see cref="DateTime.MinValue"/>, trap 47), then every 5 minutes.
/// The crash-loop guard is the server's per-id 429 rate limit, not a client-side dwell:
/// a crash-looping client costs at most one small POST per launch. A failed
/// beat is DROPPED, never retried or queued: a heartbeat means "running now", so a late one
/// is a wrong one. The signed plan's "bounded backoff" slows the NEXT tick instead — each
/// consecutive failure doubles the interval (5 → 10 → 20 → 40 → 60 min cap) and the first
/// success resets it. All of it lives in memory; no state is persisted.
/// </summary>
public sealed class TelemetrySchedule
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan BackoffCap = TimeSpan.FromMinutes(60);

    /// <summary>When the next heartbeat is due, or null while disarmed (telemetry off).</summary>
    public DateTime? NextDue { get; private set; }

    /// <summary>Consecutive failures since the last success. Drives the backoff.</summary>
    public int ConsecutiveFailures { get; private set; }

    /// <summary>Is a send in flight? The scheduler never starts a second one.</summary>
    public bool InFlight { get; private set; }

    /// <summary>Start the clock: the first beat is due AT <paramref name="epoch"/>
    /// (the launch/opt-in moment). Resets the backoff — a fresh consent is a fresh start.</summary>
    public void Arm(DateTime epoch)
    {
        NextDue = epoch;
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
