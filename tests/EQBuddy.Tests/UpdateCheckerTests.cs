using System.Security.Cryptography;
using EQBuddy.Core;

namespace EQBuddy.Tests;

public class UpdateCheckerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("eqbuddy-upd-").FullName;
    private string SetupPath => Path.Combine(_dir, "EQBuddySetup.exe");

    public UpdateCheckerTests() => File.WriteAllBytes(SetupPath, [1, 2, 3, 4, 5]);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private UpdateInfo Info => new(new Version(9, 9, 9), SetupPath);

    // ---- choosing between the shared folder and the GitHub feed ----

    private static UpdateInfo Local(int minor) => new(new Version(1, minor, 0), "C:\\setup.exe");
    private static UpdateInfo Web(int minor) =>
        new(new Version(1, minor, 0), null, "https://example/EQBuddySetup.exe", "https://example/EQBuddySetup.exe.sha256");

    /// <summary>The bug this exists to prevent: a synced-but-stale local installer is a
    /// perfectly good answer, just not a new one. It used to stop the GitHub feed from being
    /// consulted at all, so a family member whose OneDrive hadn't caught up never heard about
    /// the release — and a restart didn't help, because startup took the same path.
    ///
    /// **And it went on shipping while this test was green (#218).** The rule was right and
    /// asserted here; the CALLER never reached it. <c>FindBestAsync</c> kept an early
    /// return — "if local beats what is INSTALLED, take it and skip the network" — which is
    /// the same veto one level up, narrower and therefore harder to see: local only had to
    /// beat the installed build, not the published one, so a folder one release behind hid
    /// every release after it and the player updated a hop at a time.
    ///
    /// Worth remembering as a shape: a decision function can be correct, and tested, and
    /// bypassed. When a rule matters, ask who is allowed NOT to call it.</summary>
    [Fact]
    public void AStaleLocalFolderDoesNotHideANewerRelease() =>
        Assert.Equal(new Version(1, 15, 0), UpdateChecker.PickBest(Local(14), Web(15))!.Latest);

    /// <summary>#119 (Snagglefern): a PORTABLE copy that runs the installer "updates"
    /// into Program Files while the portable exe stays old — every relaunch looks like
    /// a revert. Detection is Inno's uninstaller sitting beside the exe.</summary>
    [Fact]
    public void InstalledCopyIsDetectedByTheUninstallerBesideTheExe()
    {
        Assert.False(UpdateChecker.IsInstalledCopyAt(_dir));           // portable unzip
        File.WriteAllBytes(Path.Combine(_dir, "unins000.exe"), [1]);
        Assert.True(UpdateChecker.IsInstalledCopyAt(_dir));            // Inno install
        Assert.False(UpdateChecker.IsInstalledCopyAt(""));             // no process path
    }

    /// <summary>When the local folder has the newer build, install from disk — no reason to
    /// download 45 MB that's already sitting there.</summary>
    [Fact]
    public void ANewerLocalFolderWins()
    {
        var best = UpdateChecker.PickBest(Local(16), Web(15))!;
        Assert.Equal(new Version(1, 16, 0), best.Latest);
        Assert.NotNull(best.SetupPath);
    }

    /// <summary>Ties go local, for the same reason.</summary>
    [Fact]
    public void ATieGoesToTheLocalFolder() =>
        Assert.NotNull(UpdateChecker.PickBest(Local(15), Web(15))!.SetupPath);

    [Fact]
    public void EitherSourceAloneIsUsed()
    {
        Assert.Equal(new Version(1, 15, 0), UpdateChecker.PickBest(null, Web(15))!.Latest);
        Assert.Equal(new Version(1, 15, 0), UpdateChecker.PickBest(Local(15), null)!.Latest);
        Assert.Null(UpdateChecker.PickBest(null, null));
    }

    // ---- parsing the GitHub release feed ----

    private static string ReleaseJson(string tag, params (string Name, string Url)[] assets)
    {
        var assetJson = string.Join(",", assets.Select(a =>
            $$"""{"name": "{{a.Name}}", "browser_download_url": "{{a.Url}}"}"""));
        return $$"""{"tag_name": "{{tag}}", "assets": [{{assetJson}}]}""";
    }

    [Fact]
    public void ParsesAFullReleaseWithAllThreeAssets()
    {
        var info = UpdateChecker.ParseRelease(ReleaseJson("v1.40.0",
            ("EQBuddySetup.exe", "https://gh/setup"),
            ("EQBuddySetup.exe.sha256", "https://gh/setup.sha256"),
            ("EQBuddy-linux-x64.tar.gz", "https://gh/linux")))!;

        Assert.Equal(new Version(1, 40, 0), info.Latest);
        Assert.Equal("https://gh/setup", info.DownloadUrl);
        Assert.Equal("https://gh/setup.sha256", info.Sha256Url);
        Assert.Equal("https://gh/linux", info.LinuxTarballUrl);
        Assert.Null(info.SetupPath);
    }

    /// <summary>The fail-closed rule drops an unverifiable installer, but must not take the
    /// tarball with it — the tarball is only ever handed to the browser, never staged and
    /// executed, so it carries the same trust as clicking the asset on the release page.</summary>
    [Fact]
    public void AMissingInstallerHashDropsTheInstallerButKeepsTheTarball()
    {
        var info = UpdateChecker.ParseRelease(ReleaseJson("v1.40.0",
            ("EQBuddySetup.exe", "https://gh/setup"),
            ("EQBuddy-linux-x64.tar.gz", "https://gh/linux")))!;

        Assert.Null(info.DownloadUrl);
        Assert.Equal("https://gh/linux", info.LinuxTarballUrl);
    }

    /// <summary>The window issue #56 lives in: CI attaches the tarball a few minutes after
    /// the release publishes, so a fresh release can list only the Windows assets. That's
    /// still an update — Linux just falls back to the release page.</summary>
    [Fact]
    public void AReleaseWithoutATarballStillCounts()
    {
        var info = UpdateChecker.ParseRelease(ReleaseJson("v1.40.0",
            ("EQBuddySetup.exe", "https://gh/setup"),
            ("EQBuddySetup.exe.sha256", "https://gh/setup.sha256")))!;

        Assert.Equal("https://gh/setup", info.DownloadUrl);
        Assert.Null(info.LinuxTarballUrl);
    }

    [Fact]
    public void ANonVersionTagIsNoUpdate() =>
        Assert.Null(UpdateChecker.ParseRelease(ReleaseJson("nightly")));

    [Fact]
    public async Task StagesWithoutHashFile()
    {
        var staged = await UpdateChecker.StageForInstall(Info);
        Assert.True(File.Exists(staged));
    }

    [Fact]
    public async Task StagesWhenHashMatches()
    {
        using var s = File.OpenRead(SetupPath);
        File.WriteAllText(SetupPath + ".sha256", Convert.ToHexString(SHA256.HashData(s)));
        var staged = await UpdateChecker.StageForInstall(Info);
        Assert.True(File.Exists(staged));
    }

    [Fact]
    public async Task RefusesWhenHashMismatches()
    {
        File.WriteAllText(SetupPath + ".sha256", new string('A', 64));
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateChecker.StageForInstall(Info));
    }

    [Fact]
    public async Task NothingToStageCannotBeStaged() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UpdateChecker.StageForInstall(new UpdateInfo(new Version(9, 9, 9), null)));

    [Fact]
    public async Task DownloadsAndStagesFromGitHub()
    {
        var bytes = new byte[] { 9, 8, 7, 6, 5 };
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        using var server = new StubAssetServer(bytes, hash);

        var info = new UpdateInfo(new Version(9, 9, 9), SetupPath: null, server.SetupUrl, server.Sha256Url);
        var staged = await UpdateChecker.StageForInstall(info);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(staged));
    }

    [Fact]
    public async Task RefusesWhenDownloadedHashMismatches()
    {
        var bytes = new byte[] { 9, 8, 7, 6, 5 };
        using var server = new StubAssetServer(bytes, new string('A', 64));

        var info = new UpdateInfo(new Version(9, 9, 9), SetupPath: null, server.SetupUrl, server.Sha256Url);
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateChecker.StageForInstall(info));
    }

    /// <summary>A rejected installer must not be left behind in %TEMP%, where a user
    /// hunting for "the update" could run it by hand.</summary>
    [Fact]
    public async Task DeletesTheStagedFileWhenTheHashMismatches()
    {
        var bytes = new byte[] { 4, 4, 4 };
        using var server = new StubAssetServer(bytes, new string('B', 64));

        var info = new UpdateInfo(new Version(9, 9, 9), SetupPath: null, server.SetupUrl, server.Sha256Url);
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateChecker.StageForInstall(info));
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "EQBuddySetup.exe")));
    }

    /// <summary>Downloads are only ever run when a published hash can vouch for them. The
    /// local OneDrive path keeps its older behavior — that folder is already trusted, and
    /// it predates the hash file.</summary>
    [Fact]
    public async Task RefusesToDownloadWithoutAPublishedHash()
    {
        var info = new UpdateInfo(new Version(9, 9, 9), SetupPath: null,
            DownloadUrl: "http://127.0.0.1:9/EQBuddySetup.exe", Sha256Url: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => UpdateChecker.StageForInstall(info));
        Assert.Contains("SHA-256", ex.Message);
    }

    /// <summary>Minimal local HTTP server standing in for a GitHub release's download
    /// assets, so StageForInstall's HTTP path gets real network round-trips in tests
    /// rather than only the local-file (OneDrive) path.</summary>
    private sealed class StubAssetServer : IDisposable
    {
        private readonly System.Net.HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();

        public string SetupUrl { get; }
        public string Sha256Url { get; }

        public StubAssetServer(byte[] setupBytes, string sha256Hex)
        {
            var port = GetFreePort();
            var prefix = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            SetupUrl = prefix + "EQBuddySetup.exe";
            Sha256Url = prefix + "EQBuddySetup.exe.sha256";

            _ = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        var ctx = await _listener.GetContextAsync();
                        var body = ctx.Request.Url!.AbsolutePath.EndsWith(".sha256")
                            ? System.Text.Encoding.ASCII.GetBytes(sha256Hex)
                            : setupBytes;
                        ctx.Response.ContentLength64 = body.Length;
                        await ctx.Response.OutputStream.WriteAsync(body);
                        ctx.Response.OutputStream.Close();
                    }
                }
                catch (Exception) { /* listener stopped */ }
            }, _cts.Token);
        }

        private static int GetFreePort()
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
        }
    }
}
