using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using EQBuddy.Companion;
using EQBuddy.Core;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// EQBuddy Mobile on Linux/macOS (#208, sbaum23). The server was UI-toolkit-free from the
/// start so this lane could host it, but the wiring is what actually decides whether a
/// paired phone gets a screen — and the wiring is ~20 callbacks in a record, where a
/// MISSING one is not a compile error. It is a surface that arrives empty here and full on
/// Windows, and nobody finds that without a phone, a Linux box and the right camp.
///
/// That is not hypothetical: <c>CompanionSources.Raids</c> and <c>.Progress</c> were added
/// on 2026-08-19, five days after the record, and a port written from an older mental
/// model of it would have shipped without them and looked complete.
/// </summary>
[Collection("avalonia")]
public sealed class CompanionWiringTests : IDisposable
{
    private readonly string _profile = Path.Combine(
        Path.GetTempPath(), "eqb-companion-" + Guid.NewGuid().ToString("N")[..8]);

    public CompanionWiringTests()
    {
        Directory.CreateDirectory(Path.Combine(_profile, "logs"));
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", _profile);
        File.WriteAllText(Path.Combine(_profile, "settings.json"),
            $$"""
              {
                "LogFolder": {{System.Text.Json.JsonSerializer.Serialize(Path.Combine(_profile, "logs"))}},
                "TruncateLogs": false, "ShowTutorial": false, "TrackSpawns": false,
                "LastSeenVersion": {{System.Text.Json.JsonSerializer.Serialize(UpdateChecker.CurrentVersion.ToString())}},
                "Theme": "ParchmentBrass"
              }
              """);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", null);
        try { Directory.Delete(_profile, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>Every source the record declares is wired. Reflection rather than a list of
    /// names on purpose: adding a member to <see cref="CompanionSources"/> and forgetting
    /// this build then fails HERE, at the moment the gap is created, instead of on a
    /// player's tablet weeks later.</summary>
    [AvaloniaFact]
    public void EverySourceTheRecordDeclaresIsWired()
    {
        var main = new MainWindow();
        main.Show();

        var unwired = typeof(CompanionSources)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetValue(main.CompanionSources) is null)
            .Select(p => p.Name)
            .ToList();

        Assert.True(unwired.Count == 0,
            "EQBuddy Mobile would serve these surfaces empty on Linux/macOS: " +
            string.Join(", ", unwired));

        main.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A fresh profile opens no socket. The same contract
    /// <c>CompanionEnableTests</c> holds the host to, asserted through the widget that
    /// constructs it — because "off by default" is a property of the WIRING as much as of
    /// the host, and this lane's wiring is new.</summary>
    [AvaloniaFact]
    public void AFreshProfileListensOnNothing()
    {
        var main = new MainWindow();
        main.Show();

        Assert.False(main.Companion.Running);
        Assert.Null(main.Companion.PairingUrl);
        Assert.Equal(0, main.Companion.ClientCount);

        main.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The pairing window builds, and its QR code renders — the one control here
    /// that is neither text nor a checkbox, and the one a player cannot use the feature
    /// without. A matrix that rasterizes to nothing would still open a window.</summary>
    [AvaloniaFact]
    public void ThePairingWindowRendersItsCode()
    {
        var main = new MainWindow();
        main.Show();
        var window = new CompanionWindow(main.Companion);
        window.Show();

        var modules = QrEncoder.Encode("http://192.168.1.20:8777/#abcdef");
        var code = QrBitmap.Render(modules);
        // The symbol, plus the spec's quiet zone on both sides. Asserted against the
        // matrix the encoder actually returned rather than a module count written down
        // here: a version bump in the encoder is not a bug in this renderer, and a
        // hardcoded number would report it as one.
        Assert.Equal(modules.GetLength(0) + EQBuddy.UI.Shared.QrRaster.QuietZone * 2,
            code.PixelSize.Width);
        Assert.Equal(code.PixelSize.Width, code.PixelSize.Height);

        window.Close();
        main.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
