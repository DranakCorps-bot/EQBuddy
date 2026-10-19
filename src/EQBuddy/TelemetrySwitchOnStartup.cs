using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **Where the v2.0.7 switch-on runs** (DRA-2010 §1; TEL-001 as amended by DRA-2010). Called
/// once from <c>App.OnStartup</c>, after the settings load and BEFORE MainWindow is built —
/// MainWindow loads its own snapshot, so the switch-on has to be on disk before it does, and a
/// write after it would be reverted by the widget's next whole-file save (trap 13). It also runs
/// before <see cref="TelemetryRuntime.Start"/>, so the runtime arms off the switched-on state
/// and its held first beat.
///
/// <para><b>Nothing waits on the player.</b> DRA-1980's blocking notice window is gone (Founder
/// order 2, 2026-10-18). What the player sees is the widget's one-time note
/// (<see cref="TelemetryNoteBanner"/>), whose form this decides and leaves in
/// <see cref="Note"/>.</para>
///
/// <para><b>The harness never switches anything on.</b> E2E and shoot.ps1 run isolated profiles
/// (<see cref="AppPaths.IsProductOwnedProfile"/> is false there), which the policy refuses
/// before any write. <c>EQBUDDY_SHOW_TELEMETRY_NOTE=generic|returning</c> puts the note on
/// screen for a shot or an E2E read, and only on such a profile — DISPLAY ONLY: it writes
/// nothing, and it cannot arm, hold or send anything (trap 22).</para>
///
/// It never throws: a consent rule that could stop EQBuddy from starting is worse than none.
/// A crash here writes nothing, so the switch-on simply runs again next launch.
/// </summary>
internal static class TelemetrySwitchOnStartup
{
    private const string ShowNoteHook = "EQBUDDY_SHOW_TELEMETRY_NOTE";

    /// <summary>The note this launch draws. Read by the widget once it is built.</summary>
    public static TelemetryHeartbeat.NoteKind Note { get; private set; }

    public static void Run(AppSettings settings)
    {
        try
        {
            var productProfile = AppPaths.IsEvolvedLine && AppPaths.IsProductOwnedProfile;
            // The order (one save, the key with the switch) is TelemetryHeartbeat's, where a
            // unit test can see it; this method supplies the disk.
            var decision = TelemetryHeartbeat.RunSwitchOn(settings, productProfile,
                TelemetrySender.IsConfigured, settings.Save);
            TelemetryRuntime.SwitchOnWord = TelemetryHeartbeat.Word(decision);
            Note = TelemetryHeartbeat.NoteFor(decision, settings);
            if (!AppPaths.IsProductOwnedProfile)
                Note = Environment.GetEnvironmentVariable(ShowNoteHook)?.ToLowerInvariant() switch
                {
                    "generic" => TelemetryHeartbeat.NoteKind.Generic,
                    "returning" => TelemetryHeartbeat.NoteKind.Returning,
                    _ => Note,
                };
        }
        catch (Exception ex)
        {
            TelemetryRuntime.SwitchOnWord = "failed";
            App.LogError(ex);
        }
    }
}
