using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EQBuddy.Core;

namespace EQBuddy.E2E;

/// <summary>
/// One launch-to-teardown lifetime of the REAL EQBuddy.exe against an isolated profile:
/// a temp EQBUDDY_APPDATA dir (settings.json pre-seeded, so no UI interaction is ever
/// needed for setup) and a temp "game install" whose Logs\ holds the shifted fixture.
/// EQBUDDY_EXPAND=1 makes the app expand every card and write a debug.txt state dump
/// each UI tick — that dump is the suite's assertion channel.
/// </summary>
internal sealed class AppHarness : IDisposable
{
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan AssertTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    private readonly string _root;
    private Process? _process;

    public string ProfileDir { get; }
    public string LogsDir { get; }
    public string LogPath { get; }
    public string HistoryDbPath => Path.Combine(ProfileDir, "history.db");
    private string DebugDumpPath => Path.Combine(ProfileDir, "debug.txt");
    private string ErrorLogPath => Path.Combine(ProfileDir, "error.log");

    public const string Character = "Testchar";
    public const string Server = "test";

    /// <summary>Repo root, found by walking up from the test assembly to EQBuddy.slnx.</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>
    /// The app under test. PREREQUISITE: build it first — `dotnet build EQBuddy.slnx -c
    /// Release`. The suite launches the built exe rather than building here, so a test
    /// run never mutates build outputs mid-flight (and stays fast).
    /// </summary>
    public static string ExePath { get; } = Path.Combine(RepoRoot,
        "src", "EQBuddy", "bin", "Release", "net10.0-windows", "EQBuddy.exe");

    /// <summary>Extra EQBUDDY_* hooks for this launch — the screenshot/debug family
    /// MainWindow already reads (EQBUDDY_QUESTS, EQBUDDY_MAP, …). A scenario that needs a
    /// satellite window open sets one here rather than driving the UI: the suite asserts
    /// on the state dump, and there is nothing to click in it.</summary>
    private readonly Dictionary<string, string> _environment = [];

    public AppHarness(Action<AppSettings>? configureSettings = null,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        if (environment is not null)
            foreach (var (name, value) in environment) _environment[name] = value;
        if (!File.Exists(ExePath))
            throw new FileNotFoundException(
                "EQBuddy.exe not built. Run `dotnet build EQBuddy.slnx -c Release` first " +
                "(see tests/EQBuddy.E2E/README.md).", ExePath);

        _root = Directory.CreateTempSubdirectory("eqbuddy-e2e-").FullName;
        ProfileDir = Directory.CreateDirectory(Path.Combine(_root, "profile")).FullName;
        LogsDir = Directory.CreateDirectory(Path.Combine(_root, "game", "Logs")).FullName;
        // Empty but existing: UpdateChecker treats a configured folder with no
        // EQBuddySetup.exe as "no update" — no OneDrive scan, no GitHub call.
        var updateDir = Directory.CreateDirectory(Path.Combine(_root, "updates")).FullName;

        LogPath = FixtureLog.WriteShifted(
            Path.Combine(RepoRoot, "tests", "fixtures", "eqlog_Testchar_fixture.txt"),
            LogsDir, Character, Server);

        // Core's own assembly version is Directory.Build.props' — the same number
        // EQBuddy.exe reports — so the What's-new gate stays satisfied across bumps.
        var v = typeof(AppSettings).Assembly.GetName().Version ?? new Version(0, 0, 0);
        var settings = new AppSettings
        {
            LogFolder = LogsDir,
            UpdateFolder = updateDir,
            WindowLeft = 60,
            WindowTop = 60,
            Minimized = false,
            ShowTutorial = false,
            LastSeenVersion = $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}",
            // No satellite windows and no log rewriting under the test's feet.
            TrackSpawns = false,
            TruncateLogs = false,
            // Already-current: keeps Load() from adding the built-in CC-broke rule, so
            // the dump's tracked= total counts only rules a test seeded itself.
            DefaultRulesVersion = 1,
            WatchPinsMigrated = true,
        };
        configureSettings?.Invoke(settings);

        // AppSettings.Save targets the CURRENT process's profile; serialize by hand
        // instead, with the same options (NaN window positions are legitimate values).
        File.WriteAllText(Path.Combine(ProfileDir, "settings.json"),
            JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                NumberHandling = System.Text.Json.Serialization
                    .JsonNumberHandling.AllowNamedFloatingPointLiterals,
            }));
    }

    /// <summary>Launches EQBuddy.exe on this profile and waits for the startup replay
    /// to finish (the fixture has kills, so a live session shows killsTotal &gt; 0).</summary>
    public void Launch()
    {
        if (_process is { HasExited: false })
            throw new InvalidOperationException("App already running — one instance per harness at a time.");
        // A dump left by a previous launch of this profile must not satisfy this one's waits.
        File.Delete(DebugDumpPath);

        var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false };
        psi.Environment["EQBUDDY_APPDATA"] = ProfileDir;
        psi.Environment["EQBUDDY_EXPAND"] = "1";
        foreach (var (name, value) in _environment) psi.Environment[name] = value;
        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Process.Start returned null for {ExePath}");

        Wait.Until(() => DumpValue("killsTotal") > 0, LaunchTimeout,
            "app to launch and replay the fixture into a live session (debug.txt killsTotal > 0)",
            Artifacts);
    }

    /// <summary>Appends messages to the character log with live timestamps, the way the
    /// game would. Latin1 + CRLF, matching what LogWatcher's tail reads.</summary>
    public void AppendLogLines(params string[] messages)
    {
        var now = DateTime.Now;
        var text = string.Concat(messages.Select(m => FixtureLog.Stamp(now, m) + "\r\n"));
        File.AppendAllText(LogPath, text, Encoding.Latin1);
    }

    /// <summary>Current value of a debug.txt "key=value" field, or -1 while the dump is
    /// missing, mid-write, or lacks the key — callers poll via <see cref="WaitForDump"/>.</summary>
    public int DumpValue(string key)
    {
        try
        {
            foreach (var pair in File.ReadAllText(DebugDumpPath).Split(' '))
                if (pair.StartsWith(key + "=", StringComparison.Ordinal) &&
                    int.TryParse(pair.AsSpan(key.Length + 1), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var value))
                    return value;
        }
        catch (IOException) { }   // covers FileNotFound too: missing dump = not yet
        return -1;
    }

    public void WaitForDump(string key, int expected, string reason) =>
        Wait.Until(() => DumpValue(key) == expected, AssertTimeout,
            $"{reason} (debug.txt {key} to reach {expected}; last seen {DumpValue(key)})",
            Artifacts);

    /// <summary>The same wait for a fact that is a WORD rather than a count — a sort
    /// mode, a state name. The dump is space-separated <c>key=value</c>, so the value
    /// may not contain a space.</summary>
    public void WaitForDump(string key, string expected, string reason) =>
        Wait.Until(() => DumpText(key) == expected, AssertTimeout,
            $"{reason} (debug.txt {key} to read '{expected}'; last seen '{DumpText(key)}')",
            Artifacts);

    /// <summary>The raw value for a key, or "" when the dump has not appeared yet.</summary>
    public string DumpText(string key)
    {
        try
        {
            foreach (var pair in File.ReadAllText(DebugDumpPath).Split(' '))
                if (pair.StartsWith(key + "=", StringComparison.Ordinal))
                    return pair[(key.Length + 1)..];
        }
        catch (IOException) { }
        return "";
    }

    /// <summary>Closes the main window (WM_CLOSE — the same path as the user's ✕) and
    /// waits for the process to exit, so shutdown-time persistence has run.</summary>
    public void CloseGracefully()
    {
        var p = _process ?? throw new InvalidOperationException("App not launched.");
        Wait.Until(() => { p.Refresh(); return p.MainWindowHandle != IntPtr.Zero; },
            AssertTimeout, "main window handle to exist before closing", Artifacts);
        Wait.Until(() => p.HasExited || p.CloseMainWindow(), AssertTimeout,
            "WM_CLOSE to be accepted", Artifacts);
        if (!p.WaitForExit((int)ExitTimeout.TotalMilliseconds))
            throw new TimeoutException(
                $"App did not exit within {ExitTimeout.TotalSeconds:0}s of its main window closing." +
                Environment.NewLine + Artifacts());
        _process = null;
    }

    /// <summary>Failure diagnostics: the state dump and the tail of the profile's
    /// error.log, folded into every timeout message.</summary>
    public string Artifacts()
    {
        var sb = new StringBuilder();
        sb.Append("debug.txt: ").AppendLine(TryRead(DebugDumpPath) ?? "(missing)");
        var errors = TryRead(ErrorLogPath);
        if (errors is not null)
            sb.Append("error.log (tail): ").AppendLine(errors.Length > 2000 ? errors[^2000..] : errors);
        return sb.ToString();
    }

    private static string? TryRead(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch (IOException) { return null; }
    }

    public void Dispose()
    {
        if (_process is { } p)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            p.WaitForExit(10_000);
            p.Dispose();
            _process = null;
        }
        // The app's SQLite pool (and our own asserts') can hold history.db briefly.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var attempt = 0; ; attempt++)
        {
            try { Directory.Delete(_root, recursive: true); return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 4) return;   // leak a temp dir rather than fail teardown
                Thread.Sleep(500);
            }
        }
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "EQBuddy.slnx")))
                return dir.FullName;
        throw new InvalidOperationException(
            "EQBuddy.slnx not found above the test assembly — run from the repo tree.");
    }
}
