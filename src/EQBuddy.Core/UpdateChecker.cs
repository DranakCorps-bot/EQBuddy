using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>SetupPath is a local file ready to install as-is (the OneDrive path). DownloadUrl
/// is set instead for a GitHub-sourced update — StageForInstall fetches it over HTTP first,
/// then installs the same way. Both null means no update is available. (The Linux tarball and
/// macOS zip URLs the v1 Avalonia lane handed to the browser left with that lane; a v1 copy
/// runs its own copy of this record on `legacy-v1`.)</summary>
public sealed record UpdateInfo(Version Latest, string? SetupPath, string? DownloadUrl = null,
    string? Sha256Url = null, string? PageUrl = null);

/// <summary>
/// Local-first update checker: looks for a newer EQBuddySetup.exe in the family's
/// synced OneDrive folder (OneDrive does the distribution). When no update folder
/// exists (public installs from GitHub), falls back to the GitHub Releases API.
/// </summary>
public static class UpdateChecker
{
    private const string FolderName = "EQBuddyDownload";
    /// <summary>v1's installer name — reserved to the legacy line forever (its installed
    /// copies read exactly this asset and cannot be patched).</summary>
    public const string LegacySetupName = "EQBuddySetup.exe";
    /// <summary>Evolved's installer (own AppId, own install dir — installer\EQBuddyEvolved.iss).</summary>
    public const string EvolvedSetupName = "EQBuddyEvolvedSetup.exe";
    public const string LegacyPortableName = "EQBuddy-portable.zip";
    public const string EvolvedPortableName = "EQBuddyEvolved-portable.zip";

    /// <summary>The installer THIS line publishes, stages and runs. Until 2026-09-28 Evolved
    /// asked for v1's name, so a published Evolved release could never be installed by an
    /// Evolved copy — only ever sent to the browser.</summary>
    public static string SetupName => AppPaths.IsEvolvedLine ? EvolvedSetupName : LegacySetupName;
    public static string PortableName => AppPaths.IsEvolvedLine ? EvolvedPortableName : LegacyPortableName;
    private const string GitHubLatestApi = "https://api.github.com/repos/DranakCorps-bot/EQBuddy/releases/latest";
    public const string GitHubLatestPage = "https://github.com/DranakCorps-bot/EQBuddy/releases/latest";

    /// <summary>
    /// **EVOLVED DOES NOT READ "LATEST" (Founder, 2026-09-28).** GitHub has ONE Latest
    /// release, and installed v1 copies read <see cref="GitHubLatestApi"/> and nothing else,
    /// and cannot be patched. Since v2.0.0 that Latest is an Evolved release carrying no
    /// <c>EQBuddySetup.exe</c>, so a v1 copy is offered the release PAGE, never an install
    /// over itself. Evolved's own answer must not depend on which line holds Latest, so it
    /// lists recent releases and takes the highest non-draft,
    /// non-prerelease one whose major is Evolved's (<see cref="PickEvolvedRelease"/>). A
    /// prerelease still reaches nobody, on either line.
    /// </summary>
    private const string GitHubReleasesApi = "https://api.github.com/repos/DranakCorps-bot/EQBuddy/releases?per_page=30";
    public const string GitHubReleasesPage = "https://github.com/DranakCorps-bot/EQBuddy/releases";

    /// <summary>The page a "click to open the download page" banner opens for this offer:
    /// the release's own page when the feed named one, else the line's fallback — never
    /// <see cref="GitHubLatestPage"/> on Evolved, which is the LEGACY release.</summary>
    public static string PageFor(UpdateInfo? info) =>
        info?.PageUrl ?? (AppPaths.IsEvolvedLine ? GitHubReleasesPage : GitHubLatestPage);

    /// <summary>The player-facing release label from Directory.Build.props (e.g. "0.1 Beta"),
    /// or null. A LABEL only — every comparison uses <see cref="CurrentVersion"/>.</summary>
    public static string? ReleaseLabel { get; } =
        typeof(AppPaths).Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
            .OfType<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "ReleaseLabel")?.Value is { Length: > 0 } label ? label : null;

    /// <summary>What the app calls itself: "EQBuddy Evolved 0.1 Beta (v2.0.0)" on Evolved,
    /// "EQBuddy v1.99.18" on legacy. The version stays in the string so a bug report still
    /// carries the number the updater compares.</summary>
    public static string DisplayName => AppPaths.IsEvolvedLine
        ? $"EQBuddy Evolved{(ReleaseLabel is { } label ? " " + label : "")} (v{CurrentVersion})"
        : $"EQBuddy v{CurrentVersion}";

    /// <summary>Probing the releases API: a short timeout, because this runs unprompted at
    /// startup and every 6 h, and a slow answer should just mean "no update this time".</summary>
    private static readonly HttpClient Http = CreateClient(TimeSpan.FromSeconds(15));

    /// <summary>Fetching the installer itself. HttpClient.Timeout covers the whole response
    /// body, not just the headers, so the probe's 15 s would abort a ~45 MB download on any
    /// connection slower than ~25 Mbps — i.e. most of them. This one is generous instead;
    /// the user has clicked the banner and is watching a "downloading…" message.</summary>
    private static readonly HttpClient Downloads = CreateClient(TimeSpan.FromMinutes(10));

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        var c = new HttpClient { Timeout = timeout };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("EQBuddy-Updater");
        return c;
    }

    public static Version CurrentVersion
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var v = assembly.GetName().Version ?? new Version(0, 0, 0);
            return Normalize(v);
        }
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    /// <summary>
    /// Locate the shared download folder: an explicit setting wins, then the known family
    /// path, then a shallow scan of this PC's OneDrive roots for "EQBuddyDownload"
    /// (shared folders sync under different paths on each family member's account).
    /// </summary>
    public static string? FindUpdateFolder(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;

        foreach (var env in (string[])["OneDrive", "OneDriveConsumer", "OneDriveCommercial"])
        {
            var root = Environment.GetEnvironmentVariable(env);
            if (root is null || !Directory.Exists(root)) continue;
            var direct = Path.Combine(root, FolderName);
            if (Directory.Exists(direct)) return direct;
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(root))
                {
                    var nested = Path.Combine(sub, FolderName);
                    if (Directory.Exists(nested)) return nested;
                }
            }
            catch { /* ignore inaccessible roots */ }
        }
        return null;
    }

    /// <summary>Read the version stamped into the shared setup exe. Null if absent/unreadable.</summary>
    public static UpdateInfo? Check(string folder)
    {
        try
        {
            var setup = Path.Combine(folder, SetupName);
            if (!File.Exists(setup)) return null;
            var vi = FileVersionInfo.GetVersionInfo(setup);
            if (!Version.TryParse(vi.FileVersion ?? "", out var v)) return null;
            return new UpdateInfo(Normalize(v), setup);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsNewer(UpdateInfo info) => info.Latest > CurrentVersion;

    /// <summary>True when this process runs from an INSTALLED copy — Inno Setup always
    /// leaves its uninstaller beside the exe; a portable unzip has none. For portable
    /// copies the installer path is a trap (#119, Snagglefern): Setup.exe installs to
    /// Program Files and relaunches THAT copy, while the next manual launch of the
    /// portable exe is the old version again — "1.70.0 keeps reverting to 1.31". The
    /// banner sends portable users to the release page for the new zip instead.</summary>
    public static bool IsInstalledCopy =>
        Environment.ProcessPath is { } exe && IsInstalledCopyAt(Path.GetDirectoryName(exe) ?? "");

    /// <summary>Split out so the detection rule is testable with temp directories.</summary>
    public static bool IsInstalledCopyAt(string exeDirectory) =>
        exeDirectory.Length > 0 && File.Exists(Path.Combine(exeDirectory, "unins000.exe"));

    /// <summary>
    /// The best update available from either source: the shared folder when one is
    /// configured or discoverable, and the GitHub release feed.
    ///
    /// Local-first used to mean "if a local folder answered at all, don't ask GitHub", which
    /// had a hole big enough to hide a whole release: a synced-but-stale EQBuddySetup.exe is
    /// a perfectly good answer, just not a new one, and it stopped the fallback from ever
    /// running. Someone whose OneDrive hadn't caught up simply never heard about the
    /// release. Local still wins when it genuinely has the newer build — installing from a
    /// file already on disk beats a 45 MB download — but it no longer gets to veto.
    /// </summary>
    public static async Task<UpdateInfo?> FindBestAsync(string? configuredFolder)
    {
        var folder = FindUpdateFolder(configuredFolder);
        var local = folder is null ? null : Check(folder);

        // BOTH sources, always. This used to short-circuit — "if the local folder holds
        // anything newer than what is installed, take it and skip the network" — which is
        // the same veto the paragraph above says was removed, just narrower: local only had
        // to beat the INSTALLED build, not the published one. A folder sitting one release
        // behind therefore hid every release after it, one hop at a time, and the player
        // updated, relaunched, and was offered another (#218, n3cr0nk1tt3n: "I have to
        // update multiple times when starting up a session because it did not update to
        // the most modern build").
        //
        // The family/LAN channel loses nothing. PickBest still gives local the tie, so an
        // equally-new file on disk is still installed without a download — and the check
        // that now always runs is a small API probe with a short timeout, not the 45 MB.
        return PickBest(local, await CheckGitHubAsync());
    }

    /// <summary>Whichever source offers the higher version; local wins a tie, since it needs
    /// no download. Split out from <see cref="FindBestAsync"/> so the choice is testable
    /// without a network or a synced folder.</summary>
    public static UpdateInfo? PickBest(UpdateInfo? local, UpdateInfo? web)
    {
        if (local is null) return web;
        if (web is null) return local;
        return web.Latest > local.Latest ? web : local;
    }

    /// <summary>Latest GitHub release — null if unreachable or unparseable. See
    /// <see cref="ParseRelease"/> for which assets end up in the result.</summary>
    public static async Task<UpdateInfo?> CheckGitHubAsync()
    {
        try
        {
            return AppPaths.IsEvolvedLine
                ? PickEvolvedRelease(await Http.GetStringAsync(GitHubReleasesApi))
                : ParseRelease(await Http.GetStringAsync(GitHubLatestApi));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>A releases-API response as an UpdateInfo: the installer asset's download
    /// URL when the release publishes one (SetupName, with its sibling .sha256 for
    /// integrity checking); every other asset is ignored. Null when the tag isn't a
    /// version. Split out from <see cref="CheckGitHubAsync"/> so asset selection is
    /// testable without a network.</summary>
    public static UpdateInfo? ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ParseRelease(doc.RootElement);
    }

    /// <summary>
    /// The newest Evolved release in a <c>/releases</c> list response: drafts and
    /// prereleases skipped, a tag that is not a plain version skipped, and anything below
    /// <see cref="AppPaths.EvolvedMajor"/> skipped — the legacy releases share the feed.
    /// Split out so the choice is testable without a network.
    /// </summary>
    public static UpdateInfo? PickEvolvedRelease(string listJson)
    {
        using var doc = JsonDocument.Parse(listJson);
        UpdateInfo? best = null;
        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (Flag(release, "draft") || Flag(release, "prerelease")) continue;
            if (ParseRelease(release) is not { } info || info.Latest.Major < AppPaths.EvolvedMajor) continue;
            if (best is null || info.Latest > best.Latest) best = info;
        }
        return best;

        static bool Flag(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    }

    private static UpdateInfo? ParseRelease(JsonElement release)
    {
        var tag = release.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v)) return null;
        var page = release.TryGetProperty("html_url", out var h) && h.ValueKind == JsonValueKind.String
            ? h.GetString() : null;

        string? downloadUrl = null, sha256Url = null;
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            var url = asset.GetProperty("browser_download_url").GetString();
            if (name.Equals(SetupName, StringComparison.OrdinalIgnoreCase)) downloadUrl = url;
            else if (name.Equals(SetupName + ".sha256", StringComparison.OrdinalIgnoreCase)) sha256Url = url;
        }

        // Fail closed: an installer we can't verify is not one we'll download and run
        // on the user's behalf. Dropping the URL (rather than the whole update) leaves
        // the banner offering the release page, so the user still learns an update
        // exists and can fetch it deliberately. release.ps1 always publishes the hash,
        // so in practice this only fires on a hand-made or half-uploaded release.
        if (sha256Url is null) downloadUrl = null;

        return new UpdateInfo(Normalize(v), SetupPath: null, downloadUrl, sha256Url, page);
    }

    /// <summary>
    /// Stage the installer into %TEMP% and return its path, ready to run. From OneDrive
    /// this is a local copy (forces hydration of cloud-only files, and survives OneDrive
    /// sync touching the original); from GitHub it's an HTTP download of the release
    /// asset. Either way, when a sibling "{SetupName}.sha256" is published alongside
    /// it, the staged copy must match it — a corrupted or tampered installer is never run.
    /// </summary>
    public static async Task<string> StageForInstall(UpdateInfo info)
    {
        var staged = Path.Combine(Path.GetTempPath(), SetupName);
        string? expected;

        if (info.SetupPath is { } localPath)
        {
            File.Copy(localPath, staged, overwrite: true);
            var shaFile = localPath + ".sha256";
            expected = File.Exists(shaFile) ? File.ReadAllText(shaFile).Trim() : null;
        }
        else if (info.DownloadUrl is { } url)
        {
            // Downloaded installers are never run unverified — CheckGitHubAsync already
            // withholds the URL when no hash is published, and this is the backstop for a
            // hand-built UpdateInfo.
            if (info.Sha256Url is not { } shaUrl)
                throw new InvalidOperationException("Refusing to stage a download with no published SHA-256.");
            expected = (await Downloads.GetStringAsync(shaUrl)).Trim();

            // Streamed to disk rather than buffered: the installer is ~45 MB and there's no
            // reason to hold it in memory.
            await using var source = await Downloads.GetStreamAsync(url);
            await using var file = File.Create(staged);
            await source.CopyToAsync(file);
        }
        else
        {
            throw new InvalidOperationException("Nothing to stage: no local setup path or download URL.");
        }

        if (expected is not null)
        {
            string actual;
            using (var stream = File.OpenRead(staged))
                actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                // Don't leave a rejected installer lying in %TEMP% for someone to run by hand.
                try { File.Delete(staged); } catch { /* best effort */ }
                throw new InvalidOperationException(
                    $"Update installer failed integrity check (expected {expected[..12]}…, got {actual[..12]}…).");
            }
        }
        return staged;
    }
}
