using System.Windows.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **The heartbeat's clock and its one instance per process** (DRA-362 TEL-PR3). It owns the
/// in-memory schedule, asks <see cref="TelemetryHeartbeat"/> before every send, hands the body
/// to <see cref="TelemetrySender"/> off the UI thread, and records what happened for the
/// Options line and the <c>EQBUDDY_EXPAND</c> dump.
///
/// <para><b>One per process, not one per window.</b> The Behavior block is built twice (the
/// Options window and the shell's Settings room, trap 45), and two schedulers would be two
/// senders — the duplicate the server's 60-second limit exists to refuse. The block therefore
/// holds no clock; it calls <see cref="SetEnabled"/> / <see cref="DeleteAsync"/> here and paints
/// from <see cref="Status"/>.</para>
///
/// <para><b>It wraps MainWindow's <see cref="AppSettings"/>, never a second snapshot</b>
/// (trap 13) — the same instance every settings view writes.</para>
///
/// <para>Nothing here can throw into the app: every send ends as a value, and the timer's
/// handler catches what the value cannot.</para>
/// </summary>
internal sealed class TelemetryRuntime
{
    /// <summary>The process's instance, or null before MainWindow exists.</summary>
    public static TelemetryRuntime? Current { get; private set; }

    private readonly AppSettings _settings;
    private readonly bool _productProfile;
    private readonly string _appVersion;
    private readonly string _os;
    private readonly TelemetrySchedule _schedule = new();
    private readonly DispatcherTimer _timer;

    /// <summary>Heartbeat REQUESTS put on the wire this process (attempts, not successes).
    /// The dump's <c>sends=</c>: the E2E OFF fact asserts it stays 0.</summary>
    public int Sends { get; private set; }

    public TelemetryCopy.LastEvent Last { get; private set; } = TelemetryCopy.LastEvent.None;
    public DateTime? LastSentAt { get; private set; }

    /// <summary>What happened to the first-open prompt on this launch (dump word).</summary>
    public static string PromptWord { get; set; } = "notAsked";

    public bool Enabled => _settings.TelemetryEnabled;
    public string? InstallId => _settings.TelemetryInstallId;

    /// <summary>Is a delete in flight? The button stays disabled until it answers.</summary>
    public bool Deleting { get; private set; }

    private TelemetryRuntime(AppSettings settings)
    {
        _settings = settings;
        _productProfile = AppPaths.IsProductOwnedProfile;
        _appVersion = TelemetryHeartbeat.AppVersionFrom(
            typeof(AppSettings).Assembly.GetName().Version ?? new Version(0, 0, 0));
        _os = TelemetryHeartbeat.OsFrom(Environment.OSVersion.Version);
        // A check every 15 s against a 2-minute dwell and a 5-minute cadence: cheap, and at
        // most 15 s late, which no definition in §3 can see.
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>Start the process's instance. Launch with telemetry on arms the clock with
    /// the LAUNCH as its epoch; off arms nothing and opens no socket (§3).</summary>
    public static void Start(AppSettings settings)
    {
        if (Current is not null) return;
        Current = new TelemetryRuntime(settings);
        if (Current.MaySend) Current.Arm();
    }

    private bool MaySend => TelemetryHeartbeat.MaySend(
        _settings, _productProfile, TelemetrySender.IsConfigured);

    private void Arm()
    {
        _schedule.Arm(DateTime.UtcNow);
        _timer.Start();
    }

    private void Disarm()
    {
        _schedule.Disarm();
        _timer.Stop();
    }

    /// <summary>The Options toggle. ON mints a fresh id and makes NOW the epoch; OFF
    /// destroys the id and cancels the pending beat, in one settings write.</summary>
    public void SetEnabled(bool on)
    {
        if (on == _settings.TelemetryEnabled) return;
        if (on) TelemetryHeartbeat.OptIn(_settings);
        else TelemetryHeartbeat.OptOut(_settings);
        _settings.Save();
        Last = TelemetryCopy.LastEvent.None;
        LastSentAt = null;
        if (on && MaySend) Arm();
        else Disarm();
    }

    /// <summary>The line under the toggle, or null when it does not render.</summary>
    public string? Status(DateTime nowUtc) =>
        TelemetryCopy.StatusLine(_settings.TelemetryEnabled, Last, LastSentAt, nowUtc);

    private void Tick()
    {
        try
        {
            var now = DateTime.UtcNow;
            if (!_schedule.IsDue(now)) return;
            // Asked again at the moment of sending, not only when armed: settings are one
            // shared instance, and a write elsewhere must stop the NEXT beat (trap 47).
            if (!MaySend
                || TelemetryHeartbeat.PayloadFor(_settings, _appVersion, _os) is not { } payload)
            {
                Disarm();
                return;
            }
            _schedule.Started();
            Sends++;
            var json = TelemetryHeartbeat.Serialize(payload);
            _ = SendAsync(json);
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    private async Task SendAsync(string json)
    {
        // The sender never throws; ConfigureAwait(true) lands the outcome back on the UI
        // thread that owns the schedule.
        var result = await Task.Run(() => TelemetrySender.PostHeartbeatAsync(json));
        var now = DateTime.UtcNow;
        var armed = MaySend;
        if (result.Ok)
        {
            _schedule.Succeeded(now, armed);
            if (armed) { Last = TelemetryCopy.LastEvent.Sent; LastSentAt = now; }
        }
        else
        {
            _schedule.Failed(now, armed);
            if (armed) Last = TelemetryCopy.LastEvent.SendFailed;
            // error.log only (§3). "refused" and "unreachable" are different bugs.
            CoreLog.Error($"Telemetry heartbeat {Word(result.Outcome)} ({result.Detail}); "
                + $"next try in {TelemetrySchedule.BackoffAfter(_schedule.ConsecutiveFailures).TotalMinutes:0} min.");
        }
        if (!armed) _timer.Stop();
    }

    /// <summary>
    /// "Delete my telemetry data". Posts the id; ONLY a confirmed <c>204</c> clears it and
    /// turns telemetry off. Anything else changes nothing and the line says the delete did
    /// not reach the server, so the player can retry with the id that names their rows (§7).
    /// </summary>
    public async Task<bool> DeleteAsync()
    {
        if (Deleting || TelemetryHeartbeat.DeleteFor(_settings) is not { } request) return false;
        Deleting = true;
        try
        {
            var result = TelemetrySender.IsConfigured && _productProfile
                ? await Task.Run(() => TelemetrySender.PostDeleteAsync(TelemetryHeartbeat.Serialize(request)))
                : new TelemetrySender.Result(TelemetrySender.Outcome.NotConfigured,
                    _productProfile ? "no endpoint is configured" : "an isolated profile never sends");
            if (result.Ok)
            {
                TelemetryHeartbeat.DeleteConfirmed(_settings);
                _settings.Save();
                Disarm();
                Last = TelemetryCopy.LastEvent.Deleted;
                LastSentAt = null;
                return true;
            }
            Last = TelemetryCopy.LastEvent.DeleteFailed;
            CoreLog.Error($"Telemetry delete {Word(result.Outcome)} ({result.Detail}); "
                + "nothing was changed on this machine.");
            return false;
        }
        finally { Deleting = false; }
    }

    private static string Word(TelemetrySender.Outcome outcome) => outcome switch
    {
        TelemetrySender.Outcome.Refused => "refused",
        TelemetrySender.Outcome.NotConfigured => "not sent",
        _ => "unreachable",
    };

    /// <summary>The <c>EQBUDDY_EXPAND</c> facts. <c>telemetry=</c> reads the SETTING, so a
    /// default flipped to true reddens the E2E OFF fact even though an isolated profile never
    /// sends; <c>sends=</c> counts requests put on the wire.</summary>
    public static string DebugFacts() =>
        Current is { } t
            ? $"telemetry={(t.Enabled ? "on" : "off")} sends={t.Sends} telemetryPrompt={PromptWord}"
            : $"telemetry=unknown sends=0 telemetryPrompt={PromptWord}";
}
