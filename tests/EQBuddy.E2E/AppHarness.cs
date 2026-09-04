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

        var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false };
        psi.Environment["EQBUDDY_APPDATA"] = ProfileDir;
        psi.Environment["EQBUDDY_EXPAND"] = "1";
        foreach (var (name, value) in _environment) psi.Environment[name] = value;
        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Process.Start returned null for {ExePath}");

        Wait.Until(() => DumpValue("killsTotal") > 0, LaunchTimeout,
            "app to launch and replay the fixture into a live session (debug.txt killsTotal > 0)",
            Artifacts);
        WaitForReplayToSettle();
    }

    /// <summary>
    /// Waits until the app SAYS it is settled — on BOTH counts, because they are two
    /// different facts and the suite needed each of them:
    ///
    /// 1. `ingestDone` — `LogWatcher.InitialIngestDone`. The startup replay has finished,
    ///    so the session TOTALS have stopped climbing.
    /// 2. `surfacesBehind` — how many open satellite windows have not yet painted the
    ///    snapshot those totals came from. `RefreshUi` ticks the windows BEFORE it builds
    ///    the snapshot it dumps, and each window throttles on top of that (1 s for Kills
    ///    &amp; Drops and Gear &amp; Loot, 2 s for Progress and Quests, 3 s for the wiki
    ///    pack), so a ROW COUNT can be a whole creature behind its own total for seconds.
    ///
    /// **Three earlier versions guessed at this from stillness and all three were wrong on
    /// a slow machine.** Watching `killsTotal` + `lootTotal` missed the fixture's trailing
    /// sale lines ("progressMoneySold to reach 24; last seen 10"). Watching the WHOLE dump
    /// missed that a mid-ingest pause looks exactly like a finished one ("killsTotal to
    /// reach 83; last seen 82"). Adding `ingestDone` fixed the log half and left the render
    /// half: `SessionGoesLive…` still failed at "kills to reach 12; last seen 13" beside a
    /// dump reading `ingestDone=1` — the log was fully read and the Kills window was one
    /// row short when the baseline was taken, then jumped past 12. And 2.5 s of quiet
    /// cannot cover a 2 s throttle plus a tick, which is why the fourth guess would have
    /// been a bigger number rather than an answer.
    ///
    /// **Quiet is not done, and the app knew the difference both times.** Every wait here
    /// is now a question the app answers; nothing is inferred from a timer.
    /// (Trap 33's closing line, earning itself twice: ship the instrument, not the theory.)
    /// </summary>
    private void WaitForReplayToSettle()
    {
        Wait.Until(() => DumpValue("ingestDone") == 1, LaunchTimeout,
            "the startup replay to FINISH (debug.txt ingestDone=1) before any test samples " +
            "a baseline — the app's own answer, not a guess from stillness", Artifacts);

        // Both, together and re-read each poll: an open window that is still catching up
        // will report 0 for a tick only if the totals are also quiet, and asserting the
        // pair rules out the one ordering that could sneak through — surfaces level with
        // a snapshot that the log is about to move on from.
        Wait.Until(() => DumpValue("ingestDone") == 1 && DumpValue("surfacesBehind") == 0
                         && DumpValue("logPending") == 0,
            AssertTimeout,
            "every open surface to have PAINTED the settled session (debug.txt " +
            "surfacesBehind=0 alongside ingestDone=1, with logPending=0) — the windows " +
            "follow the widget's tick on their own throttle, so a row count sampled the " +
            "instant the ingest finished can still be climbing", Artifacts);
    }

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
        Wait.Until(() => DumpValue("logPending") == 0, AssertTimeout,
            $"the app's tail to READ the {messages.Length} appended line(s) " +
            "(debug.txt logPending back to 0)", Artifacts);
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
        Wait.Until(() => DumpValue(key) >= 0, AssertTimeout,
            $"{reason} (debug.txt has no {key} yet)", Artifacts);

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
