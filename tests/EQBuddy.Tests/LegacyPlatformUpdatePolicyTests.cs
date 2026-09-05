using System.Text.RegularExpressions;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// LEGACY-002 (#275, charter §19 Phase 0): a Linux or macOS copy of v1 must never be
/// offered EQBuddy v2, because v2 is Windows-only and the offer is a one-way door — once
/// a 2.x release is `latest`, every un-bridged install is being steered toward an
/// installer that cannot run on it, and the thing that would carry a later fix is the
/// update they were told to take.
///
/// The full matrix is asserted here rather than at either widget: four platforms × two
/// feeds × manual/automatic × acknowledged/not, plus the two negatives that keep it from
/// going vacuous (trap 39) — no legacy target may be `releases/latest`, and no widget may
/// test the major version for itself.
///
/// **Verified against the pre-fix tree**: with `LegacyPlatformUpdatePolicy` absent this
/// file does not compile, so the honest form of "the guard has failed at least once" is
/// the behavioural check — every major-2 non-Windows row below asserts the OPPOSITE of
/// what the shipped 1.99.18 widgets did, which offered v2 to Linux and macOS.
/// </summary>
public class LegacyPlatformUpdatePolicyTests
{
    private static readonly Desktop[] All =
        [Desktop.Windows, Desktop.Linux, Desktop.MacArm64, Desktop.MacX64];

    private static readonly Desktop[] OffWindows =
        [Desktop.Linux, Desktop.MacArm64, Desktop.MacX64];

    /// <summary>A v2 release, shaped like the worst case rather than like today's feed:
    /// it carries a Windows installer AND non-Windows artifacts. E-2c deleted
    /// `release-assets.yml`, so nothing on the Evolved mainline attaches those any more —
    /// which is exactly why this fixture keeps them. The policy must not start depending
    /// on their absence: the artifacts EXISTING was never the reason to offer the update,
    /// and a rule that only works while nobody re-adds a workflow is not a rule.</summary>
    private static UpdateInfo V2 => new(new Version(2, 0, 0), SetupPath: null,
        DownloadUrl: "https://gh/EQBuddySetup.exe",
        Sha256Url: "https://gh/EQBuddySetup.exe.sha256",
        LinuxTarballUrl: "https://gh/EQBuddy-linux-x64.tar.gz",
        MacArm64Url: "https://gh/EQBuddy-osx-arm64.zip",
        MacX64Url: "https://gh/EQBuddy-osx-x64.zip");

    /// <summary>A further LEGACY patch. `legacy-v1` existing does not mean it will never
    /// be touched, and a 1.99.x fix must still reach the platforms it is for.</summary>
    private static UpdateInfo V1Patch => new(new Version(1, 99, 19), SetupPath: null,
        DownloadUrl: "https://gh/EQBuddySetup.exe",
        LinuxTarballUrl: "https://gh/EQBuddy-linux-x64.tar.gz",
        MacArm64Url: "https://gh/EQBuddy-osx-arm64.zip",
        MacX64Url: "https://gh/EQBuddy-osx-x64.zip");

    // ---- Rule 1: Windows is unchanged, in every case ------------------------

    /// <summary>Every combination on Windows answers "behave as today". A Phase 0 change
    /// that altered the Windows update banner would have widened its own blast radius for
    /// nothing, so this is the row that must never move.</summary>
    [Fact]
    public void WindowsIsOfferedEveryUpdateWhateverTheRestOfTheMatrixSays()
    {
        foreach (var info in new[] { V1Patch, V2 })
            foreach (var manual in new[] { false, true })
                foreach (var acknowledged in new[] { false, true })
                {
                    var d = LegacyPlatformUpdatePolicy.Decide(info, Desktop.Windows,
                        manual, acknowledged);
                    Assert.True(d.ShowUpdateOffer, $"v{info.Latest} manual:{manual} ack:{acknowledged}");
                    Assert.False(d.ShowFinalLegacyNotice);
                    Assert.False(d.RecordAcknowledgement);
                    Assert.Null(d.BrowserTarget);
                }
    }

    // ---- Rule 3: a legacy 1.x patch still reaches every platform -------------

    [Fact]
    public void ALegacyPatchIsStillOfferedEverywhere()
    {
        foreach (var platform in All)
            foreach (var manual in new[] { false, true })
                foreach (var acknowledged in new[] { false, true })
                {
                    var d = LegacyPlatformUpdatePolicy.Decide(V1Patch, platform,
                        manual, acknowledged);
                    Assert.True(d.ShowUpdateOffer, $"{platform} manual:{manual} ack:{acknowledged}");
                    Assert.False(d.ShowFinalLegacyNotice);
                    Assert.Null(d.BrowserTarget);
                }
    }

    // ---- Rule 2: v2 is never offered off Windows ----------------------------

    /// <summary>The heart of LEGACY-002, and the row that fails on the shipped tree.</summary>
    [Fact]
    public void NoDesktopOffWindowsIsEverOfferedVersionTwo()
    {
        foreach (var platform in OffWindows)
            foreach (var manual in new[] { false, true })
                foreach (var acknowledged in new[] { false, true })
                    Assert.False(
                        LegacyPlatformUpdatePolicy.Decide(V2, platform, manual, acknowledged)
                            .ShowUpdateOffer,
                        $"{platform} manual:{manual} ack:{acknowledged}");
    }

    /// <summary>The automatic 6-hourly check says it ONCE. `_lastUpdateCheck` starts at
    /// DateTime.MinValue on both lanes, so "at startup" and "every six hours" are one path
    /// that fires on the first one-second tick (trap 47's epoch) — there is no separate
    /// startup path to make this idempotent in, which is why the setting exists.</summary>
    [Fact]
    public void TheAutomaticCheckShowsTheNoticeOnceAndRecordsIt()
    {
        foreach (var platform in OffWindows)
        {
            var first = LegacyPlatformUpdatePolicy.Decide(V2, platform,
                manual: false, acknowledged: false);
            Assert.True(first.ShowFinalLegacyNotice, $"{platform}");
            Assert.True(first.RecordAcknowledgement, $"{platform}");
            Assert.NotNull(first.BrowserTarget);

            var later = LegacyPlatformUpdatePolicy.Decide(V2, platform,
                manual: false, acknowledged: true);
            Assert.False(later.ShowFinalLegacyNotice, $"{platform}");
            Assert.False(later.RecordAcknowledgement, $"{platform}");
            Assert.Null(later.BrowserTarget);
        }
    }

    /// <summary>Help → Check for updates ALWAYS answers, acknowledged or not. Silence
    /// there is a silent no-op, which this repo treats as broken — the player asked a
    /// direct question. It never re-arms the automatic nag: the acknowledgement only ever
    /// goes false → true, so a manual check cannot bring the six-hourly notice back.</summary>
    [Fact]
    public void TheMenuAlwaysAnswersAndNeverReArmsTheAutomaticNotice()
    {
        foreach (var platform in OffWindows)
            foreach (var acknowledged in new[] { false, true })
            {
                var d = LegacyPlatformUpdatePolicy.Decide(V2, platform,
                    manual: true, acknowledged);
                Assert.True(d.ShowFinalLegacyNotice, $"{platform} ack:{acknowledged}");
                Assert.NotNull(d.BrowserTarget);
                // Not "false when already acknowledged": the flag has one direction, and
                // the lanes only write when it actually changes.
                Assert.True(d.RecordAcknowledgement, $"{platform} ack:{acknowledged}");

                // And it answers with the SAME thing the automatic path shows — the notice
                // is one surface, so both paths reach the same page.
                Assert.Equal(
                    LegacyPlatformUpdatePolicy.Decide(V2, platform,
                        manual: false, acknowledged: false).BrowserTarget,
                    d.BrowserTarget);
            }
    }

    /// <summary>The two answers are never both true — that is what the record buys over
    /// the bool that produced #93.</summary>
    [Fact]
    public void AnOfferAndTheNoticeAreNeverBothShown()
    {
        foreach (var platform in All)
            foreach (var info in new[] { V1Patch, V2 })
                foreach (var manual in new[] { false, true })
                    foreach (var acknowledged in new[] { false, true })
                    {
                        var d = LegacyPlatformUpdatePolicy.Decide(info, platform, manual, acknowledged);
                        Assert.False(d.ShowUpdateOffer && d.ShowFinalLegacyNotice,
                            $"{platform} v{info.Latest} manual:{manual} ack:{acknowledged}");
                    }
    }

    // ---- Negative 1: the target is NEVER releases/latest ---------------------

    /// <summary>The single highest-risk detail in Phase 0. `UpdateOffer.BrowserTarget`
    /// falls back to <c>UpdateChecker.GitHubLatestPage</c>, which IS the v2 release page
    /// the moment v2 ships, and its most prominent asset is EQBuddySetup.exe. A
    /// correct-looking notice that ends there is LEGACY-002 point 3 arriving through the
    /// back door — and it would read as a working feature in every screenshot.</summary>
    [Fact]
    public void TheLegacyTargetIsNeverTheLatestReleasePage()
    {
        foreach (var platform in OffWindows)
            foreach (var manual in new[] { false, true })
                foreach (var acknowledged in new[] { false, true })
                {
                    var target = LegacyPlatformUpdatePolicy
                        .Decide(V2, platform, manual, acknowledged).BrowserTarget;
                    if (target is null) continue;   // nothing shown, nothing to click
                    Assert.DoesNotContain("releases/latest", target);
                    Assert.NotEqual(UpdateChecker.GitHubLatestPage, target);
                    Assert.Equal(UpdateChecker.GitHubLegacyReleasePage, target);
                }
    }

    /// <summary>And the constant itself, so a future edit cannot quietly point it back at
    /// the moving page. It names a TAG, and the tag is this build's own — the only installs
    /// that can see the notice are the ones that took the bridge, so for every reader of
    /// this value the bridge tag and the running version are the same string.</summary>
    [Fact]
    public void TheLegacyReleasePageNamesATagAndNotLatest()
    {
        Assert.DoesNotContain("releases/latest", UpdateChecker.GitHubLegacyReleasePage);
        Assert.Contains("/releases/tag/", UpdateChecker.GitHubLegacyReleasePage);
        Assert.StartsWith("v", UpdateChecker.LegacyFinalTag);
        Assert.EndsWith(UpdateChecker.LegacyFinalTag, UpdateChecker.GitHubLegacyReleasePage);
    }

    // ---- The notice itself: 320 px, wrapping, no unbreakable token -----------

    /// <summary>Trap 12: both widgets are SizeToContent, so a string that cannot wrap is a
    /// geometry change on a transparent always-on-top window over a fullscreen game
    /// (#173). A bare URL is exactly such a token, which is why the link lives behind the
    /// click and not in the sentence.</summary>
    [Fact]
    public void TheNoticeCarriesNoUnbreakableToken()
    {
        foreach (var platform in OffWindows)
        {
            var text = LegacyPlatformUpdatePolicy.FinalLegacyNoticeText(V2, platform);
            Assert.DoesNotContain("http", text);
            foreach (var word in text.Split(' '))
                Assert.True(word.Length <= 20, $"{platform}: unbreakable token '{word}'");
        }
    }

    /// <summary>#93 in the wording: "not Windows" must not mean "Linux". A Mac player is
    /// told they are on macOS.</summary>
    [Fact]
    public void TheNoticeNamesTheRightPlatform()
    {
        Assert.Contains("Linux",
            LegacyPlatformUpdatePolicy.FinalLegacyNoticeText(V2, Desktop.Linux));
        foreach (var platform in new[] { Desktop.MacArm64, Desktop.MacX64 })
        {
            var text = LegacyPlatformUpdatePolicy.FinalLegacyNoticeText(V2, platform);
            Assert.Contains("macOS", text);
            Assert.DoesNotContain("Linux", text);
        }
    }

    /// <summary>The platform mapping. It cannot be asserted against a fixed answer — the
    /// suite runs on Windows AND on the Linux CI job — so it is checked for CONSISTENCY
    /// with the runtime, which is what a wrong branch would break.</summary>
    [Fact]
    public void CurrentAgreesWithTheRunningOperatingSystem()
    {
        var current = LegacyPlatformUpdatePolicy.Current();
        if (OperatingSystem.IsWindows()) Assert.Equal(Desktop.Windows, current);
        else if (OperatingSystem.IsMacOS())
            Assert.True(current is Desktop.MacArm64 or Desktop.MacX64, $"macOS resolved to {current}");
        else Assert.Equal(Desktop.Linux, current);
        Assert.Equal(OperatingSystem.IsWindows(), LegacyPlatformUpdatePolicy.IsWindows(current));
    }

    // ---- The scanner: six call sites, one policy -----------------------------

    /// <summary>
    /// THREE participants per lane decide this, not two: the six-hourly TICK, the Help
    /// MENU, and the banner CLICK — and the answer they need is one POLICY. Naming them
    /// is deliberate (trap 49: thirteen green tests agreed with a bug because the model
    /// they encoded had two actors and the system had three).
    ///
    /// Same shape as <c>LogJanitorPolicyTests.NoWidgetDecidesPruningForItself</c> and
    /// <c>CompanionSnapshotArgumentTests</c>: a seventh call site that decides for itself
    /// fails the build instead of reaching a player on a platform we can no longer ship to.
    ///
    /// **E-2 removed the Avalonia row (2026-09-04) and the guard KEEPS ITS FULL WEIGHT** —
    /// this is the one policy on the list whose subject is the platforms being cut, so it
    /// is tempting to read the deletion as "the question is settled". It is not. The
    /// remaining widget is the one that will tell a Linux or macOS player, running the
    /// final v1, where their build ends; `LegacyPlatformUpdatePolicy` is what decides that
    /// and the three participants above are what could still route around it.
    /// </summary>
    [Theory]
    [InlineData("src/EQBuddy/MainWindow.xaml.cs")]
    public void NoWidgetDecidesTheLegacyPlatformQuestionForItself(string relative)
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), relative));

        // The TICK and the MENU: two callers, and each NAMES which participant it is.
        // A positional call is a site whose role can only be read by counting arguments.
        var callers = Regex.Matches(source, @"CheckForUpdates\(manual:\s*(true|false)\)");
        Assert.Equal(2, callers.Count);
        Assert.Contains(callers, m => m.Value.Contains("false"));   // the six-hourly tick
        Assert.Contains(callers, m => m.Value.Contains("true"));    // Help -> Check for updates
        Assert.DoesNotContain("CheckForUpdates(true", source);
        Assert.DoesNotContain("CheckForUpdates(false", source);

        // The POLICY: exactly one decision per lane, so the two callers cannot diverge.
        Assert.Single(Regex.Matches(source, @"LegacyPlatformUpdatePolicy\.Decide\("));

        // The CLICK: routed through the policy's own target, never the latest page.
        Assert.Contains("_legacyNoticeTarget is { } legacy", source);
        Assert.Contains("LegacyPlatformUpdatePolicy.FinalLegacyOpenedText()", source);

        // The negatives that keep this from going vacuous (trap 39). A widget that knows
        // the major version, or writes the acknowledgement outside its one helper, is
        // exactly the drift the policy exists to prevent.
        Assert.DoesNotContain("Latest.Major", source);
        Assert.DoesNotContain("GitHubLegacyReleasePage", source);
        Assert.Single(Regex.Matches(source, @"LegacyFinalNoticeAcknowledged = true"));
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EQBuddy.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
