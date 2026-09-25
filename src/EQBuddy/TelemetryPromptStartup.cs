using System.Windows;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// **Where the first-open telemetry prompt is asked** (DRA-362 TEL-PR3; TEL-001 as amended).
/// Called once from <c>App.OnStartup</c>, after the settings load and BEFORE MainWindow is
/// built — MainWindow loads its own snapshot, so the answer has to be on disk before it does,
/// and a write after it would be reverted by the widget's next whole-file save (trap 13).
///
/// <para><b>The flag is written BEFORE the window opens.</b> §7: it is set when the prompt is
/// SHOWN, so killing the app with the prompt up is a decline, never a re-prompt. The answer's
/// write is a second save; accept puts the flag, <c>TelemetryEnabled</c> and a fresh id in it
/// together.</para>
///
/// <para><b>The harness never sees a window.</b> E2E and shoot.ps1 run isolated profiles
/// (<see cref="AppPaths.IsProductOwnedProfile"/> is false there), where a startup modal with
/// nobody to answer would hang every run. <c>EQBUDDY_TELEMETRY_PROMPT=decline</c> scripts the
/// one answer a harness may give, and only on an isolated profile — there is no scripted
/// accept (<see cref="TelemetryHeartbeat.DecidePrompt"/>).</para>
///
/// It never throws: a consent question that could stop EQBuddy from starting is worse than no
/// question, and the flag already written means a crash here is a decline.
/// </summary>
internal static class TelemetryPromptStartup
{
    private const string ScriptHook = "EQBUDDY_TELEMETRY_PROMPT";

    public static void AskOnce(Application app, AppSettings settings)
    {
        try
        {
            // The order (flag saved before the window opens) is TelemetryHeartbeat's, where
            // a unit test can see it; this method supplies the window and the disk.
            TelemetryRuntime.PromptWord = TelemetryHeartbeat.RunFirstOpen(settings,
                productProfile: AppPaths.IsEvolvedLine && AppPaths.IsProductOwnedProfile,
                endpointConfigured: TelemetrySender.IsConfigured,
                scriptedAnswer: Environment.GetEnvironmentVariable(ScriptHook),
                ask: () => Ask(app),
                save: settings.Save);
        }
        catch (Exception ex)
        {
            TelemetryRuntime.PromptWord = "failed";
            App.LogError(ex);
        }
    }

    private static bool Ask(Application app)
    {
        // No window exists yet, so the default shutdown mode would end the app the moment
        // this dialog closes (ProfileImportStartup.Ask has the same four lines, same reason).
        var previous = app.ShutdownMode;
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var window = new TelemetryPromptWindow();
            window.ShowDialog();
            return window.Accepted;
        }
        finally
        {
            app.ShutdownMode = previous;
        }
    }
}
