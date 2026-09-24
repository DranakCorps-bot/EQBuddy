using System.Text.Json;

namespace EQBuddy.E2E;

/// <summary>
/// **§9 guard 3: the opt-in heartbeat is OFF, and sends nothing, from a launched app**
/// (DRA-362 TEL-PR3; <c>docs/v2/telemetry.md</c> §7, §9).
///
/// The policy's unit tests prove the rules; only the real EQBuddy.exe can prove the WIRING —
/// that the prompt step runs in <c>App.OnStartup</c> before MainWindow, and that the process's
/// one scheduler reads the settings the app actually loaded. <c>telemetry=</c> in the dump is
/// the SETTING and <c>sends=</c> the requests put on the wire, so "consent is off" and
/// "nothing went" are two assertions rather than one inferred from the other.
///
/// Prove-failed by defaulting <c>AppSettings.TelemetryEnabled</c> to true: both scenarios
/// redden on <c>telemetry=on</c>.
/// </summary>
public class TelemetryTests
{
    /// <summary>The default profile: off, nothing sent, and the prompt never shown — an
    /// isolated profile with no scripted answer must never get a modal at startup, or every
    /// run in this suite would hang behind it.</summary>
    [Fact]
    public void TheDefaultProfileIsOffAndSendsNothing()
    {
        using var app = new AppHarness();
        app.Launch();

        app.WaitForDump("telemetry", "off", "telemetry to be OFF on a default profile");
        Assert.Equal(0, app.DumpValue("sends"));
        app.WaitForDump("telemetryPrompt", "notTheProductProfile",
            "an isolated profile to be refused the first-open prompt by name");

        var saved = Settings(app);
        Assert.False(saved.GetProperty("TelemetryEnabled").GetBoolean());
        Assert.True(NoId(saved));
    }

    /// <summary>
    /// **A declined prompt changes nothing but the flag, and the prompt never comes back.**
    /// First launch: the harness answers "decline" (the only answer it can give), the dump
    /// says so, the profile holds the flag and nothing else, and nothing is sent. Relaunch on
    /// the SAME profile, with the same hook still set: the flag wins, so the prompt is not
    /// shown again — the hook cannot re-arm it.
    /// </summary>
    [Fact]
    public void ADeclinedPromptStaysOffAndIsNeverShownAgain()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_TELEMETRY_PROMPT"] = "decline",
        });
        app.Launch();

        app.WaitForDump("telemetryPrompt", "declined", "the scripted decline to be taken");
        app.WaitForDump("telemetry", "off", "telemetry to stay OFF after a decline");
        Assert.Equal(0, app.DumpValue("sends"));

        var afterDecline = Settings(app);
        Assert.True(afterDecline.GetProperty("TelemetryPromptShown").GetBoolean());
        Assert.False(afterDecline.GetProperty("TelemetryEnabled").GetBoolean());
        Assert.True(NoId(afterDecline));

        app.CloseGracefully();
        app.Launch();

        app.WaitForDump("telemetryPrompt", "alreadyShown", "the prompt to never show twice");
        app.WaitForDump("telemetry", "off", "telemetry to stay OFF on relaunch");
        Assert.Equal(0, app.DumpValue("sends"));
    }

    /// <summary>No install id on disk: absent, or written as null, depending on how the
    /// settings writer treats nulls.</summary>
    private static bool NoId(JsonElement settings) =>
        !settings.TryGetProperty("TelemetryInstallId", out var id) || id.ValueKind == JsonValueKind.Null;

    private static JsonElement Settings(AppHarness app)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(app.ProfileDir, "settings.json")));
        return doc.RootElement.Clone();
    }
}
