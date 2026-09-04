using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// LEGACY-002's gate proof, offline: the notice a Linux or macOS player sees when the feed
/// starts carrying EQBuddy v2 is PAINTED, by this build, on the widget that will show it.
///
/// The plan's original proof was a real prerelease published to GitHub, watched from a
/// bridged client. Evolved develops local-only, so there is no channel to publish to and no
/// prerelease to watch — inventing one to tick a row would be the tail wagging the dog. What
/// is provable without a wire is everything up to the wire, and this file is the half that
/// <c>LegacyPlatformUpdatePolicyTests</c> cannot reach: that file proves the DECISION, and a
/// correct decision that never reaches a control is trap 42's shape — "present in the build"
/// and "in effect at runtime" are different claims, and only the second one is the feature.
/// Trap 43 is the same lesson from the other end: a value with a producer and no consumer
/// means the app is doing something and telling the player nothing.
///
/// **Run it while this lane still exists.** These tests live on the Avalonia side because
/// the notice is FOR Linux and macOS, and that project is scheduled for deletion in E-2 —
/// which is an argument for proving it now rather than after the only build that can render
/// it has gone.
///
/// **What is deliberately not proved here**, so nobody reads more into a green run: the
/// widget asks the real <see cref="LegacyPlatformUpdatePolicy.Current()"/> for the platform
/// NAME in the sentence, so on a Windows runner the string says "Windows copy". The wording
/// per platform is asserted in the shared suite
/// (<c>LegacyPlatformUpdatePolicyTests.TheNoticeNamesTheRightPlatform</c>) and the mapping
/// itself in <c>CurrentAgreesWithTheRunningOperatingSystem</c>. What this file adds is that
/// the decision arrives on screen, and that the second automatic check paints nothing.
/// </summary>
[Collection("avalonia")]
public class LegacyNoticeRenderTests : IDisposable
{
    private readonly string _profile =
        Directory.CreateTempSubdirectory("eqbuddy-legacy-notice-").FullName;

    /// <summary>A v2 release shaped like the real feed — it carries the non-Windows
    /// artifacts too, because `release-assets.yml` has not been changed. Their EXISTING is
    /// not a reason to offer the update; that is the point of the policy.</summary>
    private static UpdateInfo V2 => new(new Version(2, 0, 0), SetupPath: null,
        DownloadUrl: "https://gh/EQBuddySetup.exe",
        Sha256Url: "https://gh/EQBuddySetup.exe.sha256",
        LinuxTarballUrl: "https://gh/EQBuddy-linux-x64.tar.gz",
        MacArm64Url: "https://gh/EQBuddy-osx-arm64.zip",
        MacX64Url: "https://gh/EQBuddy-osx-x64.zip");

    public LegacyNoticeRenderTests()
    {
        // A capture surface needs profile isolation MORE than an assertion does: its whole
        // output is a picture of whatever profile it finds, and this one WRITES
        // (the notice records its acknowledgement). Mirrors WidgetRenderTests.
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", _profile);
        Directory.CreateDirectory(Path.Combine(_profile, "logs"));
        File.WriteAllText(Path.Combine(_profile, "settings.json"),
            $$"""
              { "LogFolder": {{System.Text.Json.JsonSerializer.Serialize(Path.Combine(_profile, "logs"))}},
                "TruncateLogs": false, "ShowTutorial": false, "TrackSpawns": false,
                "LastSeenVersion": {{System.Text.Json.JsonSerializer.Serialize(UpdateChecker.CurrentVersion.ToString())}},
                "Theme": "ParchmentBrass" }
              """);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", null);
        try { Directory.Delete(_profile, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>The whole LEGACY-002 promise in one frame: a non-Windows copy told about
    /// v2.0.0 shows the notice, in the banner, in the rendered tree — and never the offer.
    /// The decision is taken from the real policy for <see cref="Desktop.Linux"/> rather
    /// than hand-written, so a policy change that stopped answering "notice" would fail
    /// here as well as in the shared suite.</summary>
    [AvaloniaFact]
    public void ALinuxCopyToldAboutVersionTwoPaintsTheNoticeAndNeverTheOffer()
    {
        var decision = LegacyPlatformUpdatePolicy.Decide(V2, Desktop.Linux,
            manual: false, acknowledged: false);
        Assert.False(decision.ShowUpdateOffer);
        Assert.True(decision.ShowFinalLegacyNotice);

        var window = new MainWindow();
        window.Show();
        window.ShowFinalLegacyNotice(V2, decision);
        Dispatcher.UIThread.RunJobs();

        var (visible, text) = window.UpdateBannerForTests;
        Assert.True(visible, "the LEGACY-002 notice decided by the policy never became visible");
        Assert.Equal(
            LegacyPlatformUpdatePolicy.FinalLegacyNoticeText(V2, LegacyPlatformUpdatePolicy.Current()),
            text);

        // The two things the player must be told, whatever the platform word is.
        Assert.Contains("Windows-only", text);
        Assert.Contains("2.0.0", text);
        // Trap 12: SizeToContent means an unbreakable token is a geometry change on an
        // always-on-top window over a fullscreen game. The link lives behind the click.
        Assert.DoesNotContain("http", text);

        // It is on SCREEN, not just on a field — the distinction that trap 15 cost a
        // release for (a correct surface painted into a collapsed host).
        var painted = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsVisible).Select(t => t.Text ?? "").ToList();
        Assert.Contains(painted, t => t.Contains("Windows-only"));
        // And the offer wording is nowhere near it. `Install` is what the update banner
        // says when it IS an offer (UpdateOffer.OfferText).
        Assert.DoesNotContain(painted, t => t.Contains("Install v2"));

        Assert.NotNull(window.CaptureRenderedFrame());
        Assert.True(window.Settings.LegacyFinalNoticeAcknowledged,
            "showing the notice must spend the nag, or the six-hourly check says it forever");
        window.Close();
    }

    /// <summary>And the second half of "one-time": once acknowledged, the automatic check
    /// paints NOTHING. Asserted on the banner rather than on the decision, because the
    /// decision is already covered — what this adds is that a false
    /// <c>ShowFinalLegacyNotice</c> leaves the widget silent instead of showing a stale
    /// string, which is the failure a player would report as "it keeps nagging me".</summary>
    [AvaloniaFact]
    public void TheSecondAutomaticCheckPaintsNothing()
    {
        var window = new MainWindow();
        window.Show();

        var first = LegacyPlatformUpdatePolicy.Decide(V2, Desktop.Linux,
            manual: false, acknowledged: false);
        window.ShowFinalLegacyNotice(V2, first);
        Dispatcher.UIThread.RunJobs();
        Assert.True(window.UpdateBannerForTests.Visible);

        // Fresh widget, same profile: the acknowledgement is persisted, so this is the
        // NEXT launch's six-hourly check rather than a second call in one session.
        window.Close();
        var next = new MainWindow();
        next.Show();
        Assert.True(next.Settings.LegacyFinalNoticeAcknowledged);

        var later = LegacyPlatformUpdatePolicy.Decide(V2, Desktop.Linux,
            manual: false, acknowledged: next.Settings.LegacyFinalNoticeAcknowledged);
        Assert.False(later.ShowFinalLegacyNotice);
        next.ShowFinalLegacyNotice(V2, later);
        Dispatcher.UIThread.RunJobs();

        var (visible, text) = next.UpdateBannerForTests;
        Assert.False(visible, $"the notice came back on the next automatic check: '{text}'");
        Assert.DoesNotContain("Windows-only", text);
        next.Close();
    }

    /// <summary>Help → Check for updates ALWAYS answers, acknowledged or not — silence
    /// there is a silent no-op, which this repo treats as broken. Proved on the widget
    /// because that is where the silence would be.</summary>
    [AvaloniaFact]
    public void TheMenuStillAnswersAfterTheNoticeHasBeenAcknowledged()
    {
        var window = new MainWindow();
        window.Show();

        var manual = LegacyPlatformUpdatePolicy.Decide(V2, Desktop.Linux,
            manual: true, acknowledged: true);
        window.ShowFinalLegacyNotice(V2, manual);
        Dispatcher.UIThread.RunJobs();

        var (visible, text) = window.UpdateBannerForTests;
        Assert.True(visible, "Help -> Check for updates answered with nothing at all");
        Assert.Contains("Windows-only", text);
        window.Close();
    }
}
