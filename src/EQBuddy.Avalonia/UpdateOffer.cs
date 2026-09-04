using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The update banner's decisions, as pure functions of the found update and the platform,
/// so tests can pin them down without a network or a window. The stakes: this app ships
/// on Windows (installer), Linux (tarball) and macOS (zip), and issue #56 was Linux users
/// being steered toward EQBuddySetup.exe — every string and URL here exists to send each
/// platform to the artifact it can actually use.
///
/// **It modelled two platforms for a world that has three, and macOS paid for it.** Every
/// decision below took a single <c>bool isWindows</c>, so "not Windows" silently meant
/// "Linux": a Mac user was offered <c>EQBuddy-linux-x64.tar.gz</c>, sent to that asset,
/// and told to extract it over their install (#93, Amatyr — "the link in app to download
/// the update points to EQBuddy-linux-x84.tar.gz"). The native Mac builds have been
/// attached to every release since the workflow that produces them was added FOR that
/// same discussion; nothing had ever pointed at them.
///
/// A bool cannot express a third case, which is why the fix is the ENUM rather than
/// another branch — the next platform is a new member and a compiler error at every
/// decision, instead of a fourth thing quietly inheriting Linux's answers.
/// </summary>
internal static class UpdateOffer
{
    /// <summary><see cref="Desktop"/> and its <c>Current()</c> mapping moved to
    /// <see cref="LegacyPlatformUpdatePolicy"/> in <c>UI.Shared</c> (P0-2), because both
    /// lanes now need to ask which platform this is — the WPF widget has no reference to
    /// this assembly. The artifact and wording functions below stayed: they are about
    /// which FILE a platform can use, which is still only this lane's question.</summary>
    private static bool IsWindows(Desktop d) => LegacyPlatformUpdatePolicy.IsWindows(d);

    /// <summary>The artifact this platform can actually use, or null when the release has
    /// not got one attached. Null is a real state and not an error: CI attaches the
    /// non-Windows builds after the release is created, so there is a minutes-wide window
    /// where a fresh release has none of them — the caller falls back to the release page
    /// rather than offering a download that 404s.</summary>
    internal static string? AssetUrl(UpdateInfo info, Desktop platform) => platform switch
    {
        Desktop.Linux => info.LinuxTarballUrl,
        Desktop.MacArm64 => info.MacArm64Url,
        Desktop.MacX64 => info.MacX64Url,
        _ => null,
    };

    /// <summary>What that artifact is CALLED, for the sentence that names it. A user who
    /// has to find the file on a release page needs the name, not a description.</summary>
    internal static string AssetName(Desktop platform) => platform switch
    {
        Desktop.MacArm64 => UpdateChecker.MacArm64Name,
        Desktop.MacX64 => UpdateChecker.MacX64Name,
        _ => UpdateChecker.LinuxTarballName,
    };

    /// <summary>The staged file is always a Windows EQBuddySetup.exe run with an Inno
    /// Setup /SILENT flag — there's nothing installable that way on Linux or macOS, so
    /// updates there always go through the browser. OneDrive-sourced updates (SetupPath)
    /// are a Windows-only distribution channel already, so they're unaffected by this
    /// check. Portable Windows copies never get the silent install either (#119): the
    /// installer lands elsewhere and the portable exe stays old, which reads as the
    /// update "reverting" on every relaunch.</summary>
    internal static bool CanAutoInstall(UpdateInfo info, Desktop platform, bool isInstalled = true) =>
        IsWindows(platform) && isInstalled
        && (info.SetupPath is not null || info.DownloadUrl is not null);

    /// <summary>What the banner offers before the click.</summary>
    internal static string OfferText(UpdateInfo info, Desktop platform, bool isInstalled = true)
    {
        if (IsWindows(platform) && !isInstalled)
            return $"Update v{info.Latest} is out. You're running the portable copy - click to open " +
                   "the download page, then replace this folder with the new EQBuddy-portable.zip.";
        if (CanAutoInstall(info, platform, isInstalled))
            return $"Update v{info.Latest} is ready - click here to install.";
        if (AssetUrl(info, platform) is not null)
            return $"Update v{info.Latest} is available - click to download {AssetName(platform)}.";
        return $"Update v{info.Latest} is available - click to open the download page.";
    }

    /// <summary>Where a non-auto-install click sends the browser: straight to this
    /// platform's own asset when the release has one (the point of issue #56 — no hunting
    /// through a page whose most prominent asset is a Windows installer). The release
    /// page remains the fallback everywhere, including the minutes-wide window where CI
    /// hasn't attached the platform builds to a fresh release yet.</summary>
    internal static string BrowserTarget(UpdateInfo info, Desktop platform) =>
        AssetUrl(info, platform) ?? UpdateChecker.GitHubLatestPage;

    /// <summary>What the banner says once the browser is open. Off Windows the setup exe
    /// means nothing — say what actually works there (issue #30: the old text told Linux
    /// users to run a Windows installer).</summary>
    internal static string OpenedText(UpdateInfo info, Desktop platform, bool isInstalled = true)
    {
        if (IsWindows(platform))
            return isInstalled
                ? "Download page opened - run the new EQBuddySetup.exe to update."
                : "Download page opened - grab EQBuddy-portable.zip, close EQBuddy, and replace this folder's files with the zip's.";
        return AssetUrl(info, platform) is not null
            ? $"Downloading {AssetName(platform)} - extract it over this install and restart."
            : $"Download page opened - get {AssetName(platform)} and extract it over this install.";
    }
}
