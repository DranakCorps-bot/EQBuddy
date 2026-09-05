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
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(180);
    // 45 s on a dev machine was never close to tight; a `windows-latest` runner is two
    // slow cores rendering a whole widget per tick, and the suite runs on every push as of
    // 2026-09-04. A timeout is patience, not a claim — the assertions are unchanged, and
    // a genuine failure still reports in the same second it would have before.
    private static readonly TimeSpan AssertTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);
    /// <summary>How long the dump's `tick` may stand still before a wait stops blaming
    /// its own assertion and says the APP has stopped. The UI tick is once a second and
    /// the initial ingest runs off it, so this is thirty ticks of slack on a runner that
    /// is already two slow cores — generous enough never to fire on lateness, short
    /// enough to name the failure well inside the 90 s budget.</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

    private readonly string _root;
    private Process? _process;

    public string ProfileDir { get; }
    public string LogsDir { get; }
    public string LogPath { get; }
    /// <summary>The "game install" — the Logs folder's PARENT, which is where the game
    /// writes `/outputfile` dumps and where `InventoryFile.FindLatest` looks for them.</summary>
    public string GameDir => Path.GetDirectoryName(LogsDir)!;
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
            // Prefer secondary monitor when virtual desktop is wider than primary (David: EQ on primary).
            WindowLeft = SecondaryShotOrigin().left,
            WindowTop = SecondaryShotOrigin().top,
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

    /// <summary>An `/outputfile inventory` dump sitting where the game writes it, in the
    /// game's own tab-separated shape (Location / Name / ID / Count / Slots) so it goes
    /// through the real <c>InventoryFile.ParseEntries</c> rather than a fixture-shaped
    /// substitute — trap 23: staging in the wrong shape renders a state that is real, and
    /// the assertion then passes or fails against something else entirely.
    ///
    /// Call BEFORE <see cref="Launch"/>; the app reads the newest dump for its character.</summary>
    public void WriteInventoryDump(params (string Location, string Name, int Count)[] rows)
    {
        var lines = new StringBuilder();
        lines.AppendLine("Location\tName\tID\tCount\tSlots");
        foreach (var (location, name, count) in rows)
            lines.AppendLine(CultureInfo.InvariantCulture,
                $"{location}\t{name}\t0\t{count}\t0");
        File.WriteAllText(
            Path.Combine(GameDir, $"{Character}_{Server}-Inventory.txt"), lines.ToString());
    }

    /// <summary>The Quest Tracker's own class PICKS, which live in quest-ledger.json and
    /// not in settings.json — so a scenario that needs a character to hold more (or fewer)
    /// classes than the fixture log infers has to seed them here. Key is the ledger's own
    /// "{character}_{server}", lowercased.
    ///
    /// Call BEFORE <see cref="Launch"/>.</summary>
    public void WriteLedgerClasses(params string[] classes) =>
        File.WriteAllText(Path.Combine(ProfileDir, "quest-ledger.json"),
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                [$"{Character}_{Server}".ToLowerInvariant()] = new { Classes = classes },
            }, new JsonSerializerOptions { WriteIndented = true }));

    /// <summary>Launches EQBuddy.exe on this profile and waits for the startup replay to
    /// finish — first for it to START (the fixture has kills, so a live session shows
    /// killsTotal &gt; 0), then for it to STOP moving.
    ///
    /// **The second wait is the one that was missing, and the first one reads exactly like
    /// it is there.** A test's usual shape is "sample a baseline, append a line, wait for
    /// baseline + 1", and <see cref="WaitForDump(string,int,string)"/> is an EQUALITY: a
    /// counter that is still climbing through the rest of the fixture sails past the
    /// expected number between two polls and the wait can never be satisfied again. On a
    /// dev machine the replay finishes inside the first tick and nothing shows; on a
    /// hosted runner it does not, and `SessionGoesLive_AndFreshKillUpdatesLiveStats` failed
    /// there with "kills to reach 10; last seen 9" beside a dump reading kills=14 —
    /// the counter had gone past 10 while the harness was sleeping.</summary>
    public void Launch()
    {
        if (_process is { HasExited: false })
            throw new InvalidOperationException("App already running — one instance per harness at a time.");
        // A dump left by a previous launch of this profile must not satisfy this one's waits.
        File.Delete(DebugDumpPath);
        _lastTick = -1;
        _tickMovedAt = DateTime.UtcNow;

        var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false };
        psi.Environment["EQBUDDY_APPDATA"] = ProfileDir;
        psi.Environment["EQBUDDY_EXPAND"] = "1";
        foreach (var (name, value) in _environment) psi.Environment[name] = value;
        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Process.Start returned null for {ExePath}");

        Until(() => DumpValue("killsTotal") > 0, LaunchTimeout,
            "app to launch and replay the fixture into a live session (debug.txt killsTotal > 0)");
        WaitForReplayToSettle();
    }

    // The dump's `tick` (RefreshUi's count) as this harness last saw it, and when it last
    // moved. See WhyTheAppCannotAnswer.
    private long _lastTick = -1;
    private DateTime _tickMovedAt = DateTime.UtcNow;

    /// <summary>
    /// Why a wait should stop early instead of blaming its own assertion — or null while
    /// the app is still capable of answering.
    ///
    /// **Two failures are indistinguishable from out here, and they cost a round apart.**
    /// "kills will never reach 14" and "this app stopped ticking twelve seconds ago" both
    /// present as a value that does not change, and the timeout message names the value.
    /// The dump's `tick` separates them in one line, and the process itself answers the
    /// third case — an app that has EXITED leaves a debug.txt that looks perfectly healthy
    /// and perfectly frozen, which reads as a broken feature for the full 90 s.
    /// </summary>
    private string? WhyTheAppCannotAnswer()
    {
        if (_process is { HasExited: true } dead)
            return $"the app EXITED with code {dead.ExitCode}. Its last dump is below; " +
                   "the values in it are whatever was true when it went, not a verdict on the assertion.";
        var tick = DumpValue("tick");
        if (tick < 0) return null;   // no dump yet — too early to conclude anything
        if (tick != _lastTick)
        {
            _lastTick = tick;
            _tickMovedAt = DateTime.UtcNow;
            return null;
        }
        var still = DateTime.UtcNow - _tickMovedAt;
        return still < StallTimeout ? null
            : $"the app STOPPED TICKING: debug.txt tick has read {tick} for {still.TotalSeconds:0}s " +
              $"(process alive, Responding={IsResponding()}). Every number in the dump below is " +
              "frozen at that tick, so none of them is evidence about the assertion.";
    }

    private string IsResponding()
    {
        try { _process?.Refresh(); return _process?.Responding.ToString() ?? "no process"; }
        catch (InvalidOperationException) { return "unknown"; }
    }

    /// <summary>Every wait about a RUNNING app goes through here, so all of them get the
    /// artifact dump and the early abort. The shutdown waits deliberately do not — an
    /// exited process is the POINT there, not a diagnosis.</summary>
    private void Until(Func<bool> condition, TimeSpan timeout, string reason) =>
        Wait.Until(condition, timeout, reason, Artifacts, WhyTheAppCannotAnswer);

    /// <summary>
    /// Waits until the app SAYS the startup replay is over, so a test can sample a
    /// baseline that will not move under it. Two facts, both the app's own answer:
    ///
    /// 1. `ingestDone` — `LogWatcher.InitialIngestDone`: the full-file replay has finished.
    /// 2. `logPending` — `LogWatcher.PendingBytes`: nothing the tail has not read.
    ///
    /// **The RENDER half is no longer waited for, because it can no longer be behind.**
    /// It used to be the third condition (`surfacesBehind=0`), and it was the one that
    /// would not come: the satellite windows follow the widget's tick on their own
    /// throttles, so a row count in the dump described a different moment from the total
    /// beside it, and a wait for the two to coincide is a wait on a coincidence. The dump
    /// now paints every open surface from the snapshot it is about to report
    /// (`WidgetDump.PaintOneMoment`), so `kills == killKinds` in every dump by
    /// construction. `surfacesBehind` stays in the dump as the assertion that this holds.
    ///
    /// **Four rounds went into inferring this instead.** Watching `killsTotal` +
    /// `lootTotal` for stillness missed the fixture's trailing sale lines
    /// ("progressMoneySold to reach 24; last seen 10"). Watching the WHOLE dump could not
    /// tell a mid-ingest lull from an ending ("killsTotal to reach 83; last seen 82").
    /// `ingestDone` answered the log half honestly and left the render half, which then
    /// timed out on its own terms ("surfacesBehind=0", 90 s, beside `ingestDone=1
    /// logPending=0 killKinds=14 kills=13` — a complete log, complete data, and one row
    /// short on screen). Quiet was never the question; the question was whose moment a
    /// number came from, and the honest fix was to make there be one moment.
    /// </summary>
    private void WaitForReplayToSettle() =>
        Until(() => DumpValue("ingestDone") == 1 && DumpValue("logPending") == 0,
            LaunchTimeout,
            "the startup replay to FINISH before any test samples a baseline (debug.txt " +
            "ingestDone=1 with logPending=0) — the app's own answer, not a guess from stillness");

    /// <summary>
    /// Seeds the raid-kill ledger, which lives in its own file rather than in
    /// settings.json — so a scenario that wants the Raids surface to have ROWS cannot
    /// get there through <c>configureSettings</c>. Trap 22: with an empty ledger the
    /// surface is a one-line empty state, and asserting on that proves nothing about
    /// the rows underneath.
    ///
    /// Keys are <c>"{character}_{server}|{boss}"</c>, lowercased, exactly as
    /// <see cref="RaidKillLedger"/> writes them — the same shape scripts/shoot.ps1
    /// stages for the <c>raids-card</c> shot. Call before <see cref="Launch"/>.
    /// </summary>
    public void SeedRaids(params (string Boss, int Kills, bool Achievement)[] bosses)
    {
        var records = bosses.ToDictionary(
            b => $"{Character.ToLowerInvariant()}_{Server}|{b.Boss.ToLowerInvariant()}",
            b => new Dictionary<string, object?>
            {
                ["Kills"] = b.Kills,
                ["FirstKill"] = b.Kills > 0 ? "2026-07-02T21:15:00" : null,
                ["LastKill"] = b.Kills > 0 ? "2026-08-09T22:40:00" : null,
                ["AchievementComplete"] = b.Achievement,
                ["TierKills"] = b.Kills > 0
                    ? new Dictionary<string, int> { ["d2"] = b.Kills } : new Dictionary<string, int>(),
            });
        File.WriteAllText(Path.Combine(ProfileDir, "raid-kills.json"),
            JsonSerializer.Serialize(new
            {
                Records = records,
                HighWater = "2026-08-01T00:00:00",
            }, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Seeds the Quest Tracker's picked classes for the harness character — the source
    /// every level-unlock surface filters by (<c>UnlockClasses</c>: picks first, the
    /// combat-inferred class second).
    ///
    /// **Trap 22 again, and this one hides a whole feature.** With no classes the
    /// next-level preview is not merely thin, it is HIDDEN (Bevel, Helm-signed
    /// 2026-08-23) — so a test that leaves them empty and asserts the preview is asserting
    /// about a surface that cannot appear, and would go on passing if the preview never
    /// worked again. Inference cannot be staged from here: it needs a run of
    /// class-unique log lines and, per <c>FABLE.md</c>, collapses three classes to one
    /// anyway. Picks are the honest lever.
    ///
    /// Keys are <c>"{character}_{server}"</c>, lowercased, exactly as
    /// <see cref="SessionStats.LedgerCharacterKey"/> writes them. Call before
    /// <see cref="Launch"/>.
    /// </summary>
    public void SeedQuestClasses(params string[] classes)
    {
        File.WriteAllText(Path.Combine(ProfileDir, "quest-ledger.json"),
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                [$"{Character.ToLowerInvariant()}_{Server}"] = new { Classes = classes },
            }, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Appends messages to the character log with live timestamps, the way the
    /// game would. Latin1 + CRLF, matching what LogWatcher's tail reads.
    ///
    /// **Returns only once the app has READ the bytes** — `logPending` back to 0. That is
    /// a post-condition, not patience: a tail that has stopped and a line that parsed
    /// without counting produce the same symptom from out here (a counter that will not
    /// move), and a whole round went to the wrong one of the two. Failing at the append
    /// names the tail; failing at the assertion after it names the parse.</summary>
    public void AppendLogLines(params string[] messages)
    {
        var now = DateTime.Now;
        var text = string.Concat(messages.Select(m => FixtureLog.Stamp(now, m) + "\r\n"));
        File.AppendAllText(LogPath, text, Encoding.Latin1);
        Until(() => DumpValue("logPending") == 0, AssertTimeout,
            $"the app's tail to READ the {messages.Length} appended line(s) " +
            "(debug.txt logPending back to 0)");
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

    /// <summary>Wait until a key EXISTS in the dump.
    ///
    /// A theme window opens at ApplicationIdle AFTER Launch() returns, so for a tick
    /// or two the dump has none of its keys and DumpValue answers -1. A test that
    /// reads a value in that gap either asserts against -1 or takes -1 as a baseline
    /// and then waits forever for -1 + 1. It cost one flaky run in three the day the
    /// Kills fold landed, and the Progress tests had carried the same race silently
    /// since their own fold — so it lives HERE rather than in each test.</summary>
    public void WaitForWindow(string key, string reason) =>
        Until(() => DumpValue(key) >= 0, AssertTimeout,
            $"{reason} (debug.txt has no {key} yet)");

    /// <summary>Wait until a key reaches AT LEAST a value.
    ///
    /// **It exists because a size is not a count.** A window's measured height is 0 until
    /// WPF has laid it out, and layout happens after the window is shown — so a test that
    /// reads one the moment its page appears in the dump is asserting against a number the
    /// app has not computed yet. An equality wait cannot be used for it either: the value
    /// is whatever the monitor allows, and a test that named it would be asserting the desk
    /// it was written on (the rule the whole shell suite follows).</summary>
    public void WaitForDumpAtLeast(string key, int minimum, string reason) =>
        Until(() => DumpValue(key) >= minimum, AssertTimeout,
            $"{reason} (debug.txt {key} to reach at least {minimum}; last seen {DumpValue(key)})");

    public void WaitForDump(string key, int expected, string reason) =>
        Until(() => DumpValue(key) == expected, AssertTimeout,
            $"{reason} (debug.txt {key} to reach {expected}; last seen {DumpValue(key)})");

    /// <summary>The same wait for a fact that is a WORD rather than a count — a sort
    /// mode, a state name. The dump is space-separated <c>key=value</c>, so the value
    /// may not contain a space.</summary>
    public void WaitForDump(string key, string expected, string reason) =>
        Until(() => DumpText(key) == expected, AssertTimeout,
            $"{reason} (debug.txt {key} to read '{expected}'; last seen '{DumpText(key)}')");

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


    // Dual-monitor local: place harness windows on the display to the right of primary.
    // Single-screen CI: VirtualScreenWidth == PrimaryScreenWidth → 60,60.
    private static (double left, double top) SecondaryShotOrigin()
    {
        var primaryW = System.Windows.SystemParameters.PrimaryScreenWidth;
        var virtL = System.Windows.SystemParameters.VirtualScreenLeft;
        var virtT = System.Windows.SystemParameters.VirtualScreenTop;
        var virtW = System.Windows.SystemParameters.VirtualScreenWidth;
        if (virtW > primaryW + 10)
            return (virtL + primaryW + 60, virtT + 60);
        return (60, 60);
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
