using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// Pins the platform routing of the update banner (issues #30, #56 and #93): Windows
/// installs or is sent to the installer, Linux is pointed at the tarball, macOS at its
/// own zip — and none of them is ever shown EQBuddySetup.exe or each other's artifact.
/// Pure-function tests; the real click handler feeds these outputs to Process.Start and
/// the banner TextBlock verbatim.
/// </summary>
public class UpdateOfferTests
{
    private static readonly Version V = new(1, 40, 0);

    private static UpdateInfo FullRelease => new(V, SetupPath: null,
        DownloadUrl: "https://gh/EQBuddySetup.exe",
        Sha256Url: "https://gh/EQBuddySetup.exe.sha256",
        LinuxTarballUrl: "https://gh/EQBuddy-linux-x64.tar.gz",
        MacArm64Url: "https://gh/EQBuddy-osx-arm64.zip",
        MacX64Url: "https://gh/EQBuddy-osx-x64.zip");

    private static UpdateInfo WindowsOnlyRelease => new(V, SetupPath: null,
        DownloadUrl: "https://gh/EQBuddySetup.exe",
        Sha256Url: "https://gh/EQBuddySetup.exe.sha256");

    private static UpdateInfo OneDriveUpdate => new(V, SetupPath: @"C:\OneDrive\EQBuddySetup.exe");

    // ---- Windows keeps its silent install ----

    [Fact]
    public void WindowsAutoInstallsFromGitHubOrOneDrive()
    {
        Assert.True(UpdateOffer.CanAutoInstall(FullRelease, Desktop.Windows));
        Assert.True(UpdateOffer.CanAutoInstall(OneDriveUpdate, Desktop.Windows));
        Assert.Contains("click here to install", UpdateOffer.OfferText(FullRelease, Desktop.Windows));
    }

    // ---- Nothing off Windows touches the installer ----

    /// <summary>Looped rather than a [Theory]: this was written when Desktop was internal
    /// to the Avalonia assembly. It moved to <c>UI.Shared</c> in P0-2 (both lanes ask the
    /// platform question now), so a [Theory] would compile today — the loops stay because
    /// <c>UpdateOffer</c> itself is still internal and the rows are unchanged. The
    /// platform is named in each assert so a failure still says which one.</summary>
    private static readonly Desktop[] OffWindows =
        [Desktop.Linux, Desktop.MacArm64, Desktop.MacX64];

    [Fact]
    public void NoDesktopOffWindowsAutoInstalls()
    {
        foreach (var platform in OffWindows)
        {
            Assert.False(UpdateOffer.CanAutoInstall(FullRelease, platform), $"{platform}");
            Assert.False(UpdateOffer.CanAutoInstall(OneDriveUpdate, platform), $"{platform}");
            Assert.DoesNotContain("EQBuddySetup.exe", UpdateOffer.OfferText(FullRelease, platform));
            Assert.DoesNotContain("EQBuddySetup.exe", UpdateOffer.OpenedText(FullRelease, platform));
        }
    }

    /// <summary>The heart of issue #56: the click on Linux lands on the tarball asset.</summary>
    [Fact]
    public void LinuxIsPointedAtTheTarballAsset()
    {
        Assert.Equal("https://gh/EQBuddy-linux-x64.tar.gz",
            UpdateOffer.BrowserTarget(FullRelease, Desktop.Linux));
        Assert.Contains("EQBuddy-linux-x64.tar.gz", UpdateOffer.OfferText(FullRelease, Desktop.Linux));
    }

    /// <summary>#93 (Amatyr): a Mac was offered the LINUX tarball, because every decision
    /// here took a single bool and "not Windows" meant Linux. Each Mac architecture gets
    /// its own zip — the native builds have been on every release since the workflow that
    /// makes them was added for this very discussion.</summary>
    [Fact]
    public void MacIsPointedAtItsOwnBuild()
    {
        (Desktop Platform, string Url, string Asset)[] cases =
        [
            (Desktop.MacArm64, "https://gh/EQBuddy-osx-arm64.zip", "EQBuddy-osx-arm64.zip"),
            (Desktop.MacX64, "https://gh/EQBuddy-osx-x64.zip", "EQBuddy-osx-x64.zip"),
        ];
        foreach (var (platform, url, asset) in cases)
        {
            Assert.Equal(url, UpdateOffer.BrowserTarget(FullRelease, platform));
            Assert.Contains(asset, UpdateOffer.OfferText(FullRelease, platform));
            Assert.Contains(asset, UpdateOffer.OpenedText(FullRelease, platform));
        }
    }

    /// <summary>The regression itself, stated as the thing that must never come back: no
    /// Mac string and no Mac URL may mention the Linux tarball.</summary>
    [Fact]
    public void MacIsNeverOfferedTheLinuxTarball()
    {
        foreach (var platform in new[] { Desktop.MacArm64, Desktop.MacX64 })
        {
            Assert.DoesNotContain("linux", UpdateOffer.BrowserTarget(FullRelease, platform),
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("linux", UpdateOffer.OfferText(FullRelease, platform),
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("linux", UpdateOffer.OpenedText(FullRelease, platform),
                StringComparison.OrdinalIgnoreCase);
            // And the fallback path stays clean too — that is the one Amatyr would have
            // hit in the minutes after a release, before CI attaches the platform builds.
            Assert.DoesNotContain("linux", UpdateOffer.OpenedText(WindowsOnlyRelease, platform),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>A fresh release may not have its platform build yet (CI attaches it
    /// minutes after publish). Every desktop still learns about the update and gets the
    /// release page rather than a link that 404s.</summary>
    [Fact]
    public void AMissingPlatformBuildFallsBackToTheReleasePage()
    {
        (Desktop Platform, string Asset)[] cases =
        [
            (Desktop.Linux, "EQBuddy-linux-x64.tar.gz"),
            (Desktop.MacArm64, "EQBuddy-osx-arm64.zip"),
            (Desktop.MacX64, "EQBuddy-osx-x64.zip"),
        ];
        foreach (var (platform, asset) in cases)
        {
            Assert.Equal(UpdateChecker.GitHubLatestPage,
                UpdateOffer.BrowserTarget(WindowsOnlyRelease, platform));
            Assert.Contains("download page", UpdateOffer.OfferText(WindowsOnlyRelease, platform));
            // It still NAMES the file to look for — the whole value of the fallback.
            Assert.Contains(asset, UpdateOffer.OpenedText(WindowsOnlyRelease, platform));
        }
    }

    /// <summary>#119: a portable Windows copy must never silent-install — the installer
    /// lands elsewhere and the portable exe stays old, reading as the update
    /// "reverting" on every relaunch. Browser flow, portable-zip wording.</summary>
    [Fact]
    public void PortableWindowsCopiesNeverAutoInstall()
    {
        Assert.False(UpdateOffer.CanAutoInstall(FullRelease, Desktop.Windows, isInstalled: false));
        Assert.Contains("EQBuddy-portable.zip",
            UpdateOffer.OfferText(FullRelease, Desktop.Windows, isInstalled: false));
        Assert.Contains("EQBuddy-portable.zip",
            UpdateOffer.OpenedText(FullRelease, Desktop.Windows, isInstalled: false));
        Assert.Equal(UpdateChecker.GitHubLatestPage,
            UpdateOffer.BrowserTarget(FullRelease, Desktop.Windows));
    }

    /// <summary>Windows without a verifiable installer (fail-closed path) is browser-bound
    /// too, but goes to the release page, not to somebody else's artifact.</summary>
    [Fact]
    public void WindowsWithoutAnInstallerGetsTheReleasePage()
    {
        var noInstaller = new UpdateInfo(V, SetupPath: null,
            LinuxTarballUrl: "https://gh/EQBuddy-linux-x64.tar.gz",
            MacArm64Url: "https://gh/EQBuddy-osx-arm64.zip");
        Assert.False(UpdateOffer.CanAutoInstall(noInstaller, Desktop.Windows));
        Assert.Equal(UpdateChecker.GitHubLatestPage,
            UpdateOffer.BrowserTarget(noInstaller, Desktop.Windows));
        Assert.Contains("EQBuddySetup.exe", UpdateOffer.OpenedText(noInstaller, Desktop.Windows));
    }

    /// <summary>The platform mapping itself. It cannot be asserted against a fixed answer
    /// — the suite runs on all three — so it is checked for CONSISTENCY with the runtime,
    /// which is what a wrong branch would break. It lives in <c>UI.Shared</c> since P0-2;
    /// this row stayed here because this is the lane the code actually runs on.</summary>
    [Fact]
    public void CurrentAgreesWithTheRunningOperatingSystem()
    {
        var current = LegacyPlatformUpdatePolicy.Current();
        if (OperatingSystem.IsWindows()) Assert.Equal(Desktop.Windows, current);
        else if (OperatingSystem.IsMacOS())
            Assert.True(current is Desktop.MacArm64 or Desktop.MacX64, $"macOS resolved to {current}");
        else Assert.Equal(Desktop.Linux, current);
    }

    // ---- LEGACY-002 / LEGACY-003, on the lane that actually runs off Windows ----

    /// <summary>A v2 release, carrying non-Windows artifacts because
    /// <c>release-assets.yml</c> is unchanged. Their existence is not a reason to offer
    /// the update: v2 is Windows-only, and these files would be a v1 tarball wearing a
    /// v2 version number.</summary>
    private static UpdateInfo V2Release => new(new Version(2, 0, 0), SetupPath: null,
        DownloadUrl: "https://gh/EQBuddySetup.exe",
        Sha256Url: "https://gh/EQBuddySetup.exe.sha256",
        LinuxTarballUrl: "https://gh/EQBuddy-linux-x64.tar.gz",
        MacArm64Url: "https://gh/EQBuddy-osx-arm64.zip",
        MacX64Url: "https://gh/EQBuddy-osx-x64.zip");

    /// <summary>LEGACY-002 (#275) on the Avalonia lane: the shared policy refuses the
    /// offer for every non-Windows desktop, whichever path asked, and sends the click to
    /// the final legacy release rather than to <c>releases/latest</c> — which IS the v2
    /// release page the moment v2 ships, with a Windows installer at the top of it.</summary>
    [Fact]
    public void NoDesktopOffWindowsIsOfferedVersionTwo()
    {
        foreach (var platform in OffWindows)
            foreach (var manual in new[] { false, true })
                foreach (var acknowledged in new[] { false, true })
                {
                    var d = LegacyPlatformUpdatePolicy.Decide(V2Release, platform,
                        manual, acknowledged);
                    Assert.False(d.ShowUpdateOffer, $"{platform} manual:{manual} ack:{acknowledged}");
                    if (d.BrowserTarget is not { } target) continue;
                    Assert.DoesNotContain("releases/latest", target);
                    Assert.DoesNotContain(UpdateChecker.LinuxTarballName, target);
                }
    }

    /// <summary>LEGACY-003, stated as its own negative rather than left implied: nothing
    /// off Windows is ever staged, run or overwritten. <c>CanAutoInstall</c> has required
    /// <c>IsWindows</c> since #93; this is the assertion that stops a future edit removing
    /// the guarantee quietly, and it covers the v2 feed as well as v1.</summary>
    [Fact]
    public void NothingOffWindowsIsEverAutoInstalledIncludingFromAV2Release()
    {
        foreach (var platform in OffWindows)
        {
            Assert.False(UpdateOffer.CanAutoInstall(V2Release, platform), $"{platform}");
            Assert.False(UpdateOffer.CanAutoInstall(V2Release, platform, isInstalled: true), $"{platform}");
            Assert.False(UpdateOffer.CanAutoInstall(V2Release, platform, isInstalled: false), $"{platform}");
            Assert.False(UpdateOffer.CanAutoInstall(FullRelease, platform), $"{platform}");
        }
        // The positive that keeps the negative honest: Windows still installs.
        Assert.True(UpdateOffer.CanAutoInstall(V2Release, Desktop.Windows));
    }
}
