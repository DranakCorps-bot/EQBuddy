using System.Text.Json;

namespace EQBuddy.E2E;

/// <summary>
/// **§9 guard 3: an isolated launch is never switched on and sends nothing** (DRA-362 TEL-PR3,
/// DRA-2010; <c>docs/v2/telemetry.md</c> §7, §9).
///
/// The policy's unit tests prove the rules; only the real EQBuddy.exe can prove the WIRING —
/// that the switch-on step runs in <c>App.OnStartup</c> before MainWindow, that the process's
/// one scheduler reads the settings the app actually loaded, and that the widget builds the
/// note the policy chose. <c>telemetry=</c> in the dump is the SETTING and <c>sends=</c> the
/// requests put on the wire, so "consent is off" and "nothing went" are two assertions rather
/// than one inferred from the other.
///
/// Prove-failed by defaulting <c>AppSettings.TelemetryEnabled</c> to true (both scenarios
/// redden on <c>telemetry=on</c>) and by dropping the isolated arm of
/// <c>TelemetryHeartbeat.DecideSwitchOn</c> (the first redden on <c>telemetrySwitchOn=</c>).
/// </summary>
public class TelemetryTests
{
    /// <summary>The default profile: off, nothing sent, the switch-on refused by name, the key
    /// never written, and no note — an isolated profile is never switched on.</summary>
    [Fact]
    public void AnIsolatedLaunchIsNeverSwitchedOnShowsNoNoteAndSendsNothing()
    {
        using var app = new AppHarness();
        app.Launch();

        app.WaitForDump("telemetry", "off", "telemetry to be OFF on a default profile");
        Assert.Equal(0, app.DumpValue("sends"));
        app.WaitForDump("telemetrySwitchOn", "notTheProductProfile",
            "an isolated profile to be refused the v2.0.7 switch-on by name");
        Assert.Equal(0, app.DumpValue("telemetryHeld"));
        app.WaitForDump("telemetryNote", "none", "no telemetry note on an isolated profile");
        Assert.Equal(0, app.DumpValue("telemetryNoteShown"));

        var saved = Settings(app);
        Assert.False(saved.GetProperty("TelemetryEnabled").GetBoolean());
        Assert.True(NoId(saved));
        Assert.False(Flag(saved, "TelemetryOnByDefaultApplied"));
        Assert.False(Flag(saved, "TelemetryFirstBeatHeld"));
    }

    /// <summary>
    /// **The note's display hook draws it, and writes nothing.** With
    /// <c>EQBUDDY_SHOW_TELEMETRY_NOTE=returning</c> the widget builds the cohort form and it
    /// is on screen; the switch-on is still refused, telemetry stays OFF, nothing is sent, and
    /// the profile holds none of the switch-on's keys — so the hook a shot uses cannot opt
    /// anything in.
    /// </summary>
    [Fact]
    public void TheNoteHookDrawsTheNoteAndWritesNothing()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHOW_TELEMETRY_NOTE"] = "returning",
        });
        app.Launch();

        app.WaitForDump("telemetryNote", "returning", "the hook to build the cohort form of the note");
        app.WaitForDump("telemetryNoteShown", 1, "the note to be on screen");
        app.WaitForDump("telemetrySwitchOn", "notTheProductProfile", "the switch-on to stay refused");
        app.WaitForDump("telemetry", "off", "telemetry to stay OFF under the display hook");
        Assert.Equal(0, app.DumpValue("sends"));
        Assert.Equal(0, app.DumpValue("telemetryHeld"));

        var saved = Settings(app);
        Assert.False(saved.GetProperty("TelemetryEnabled").GetBoolean());
        Assert.True(NoId(saved));
        Assert.False(Flag(saved, "TelemetryOnByDefaultApplied"));
        Assert.False(Flag(saved, "TelemetryFirstBeatHeld"));
    }

    /// <summary>No install id on disk: absent, or written as null, depending on how the
    /// settings writer treats nulls.</summary>
    private static bool NoId(JsonElement settings) =>
        !settings.TryGetProperty("TelemetryInstallId", out var id) || id.ValueKind == JsonValueKind.Null;

    /// <summary>A bool key, false when absent (a profile the app has not saved since).</summary>
    private static bool Flag(JsonElement settings, string key) =>
        settings.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    private static JsonElement Settings(AppHarness app)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(app.ProfileDir, "settings.json")));
        return doc.RootElement.Clone();
    }
}
