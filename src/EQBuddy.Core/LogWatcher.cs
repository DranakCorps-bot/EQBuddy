using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace EQBuddy.Core;

public sealed record CharacterLog(string FilePath, string Character, string Server)
{
    public string Display => $"{Character} ({Server})";
    public static CharacterLog? FromPath(string path)
    {
        var m = Regex.Match(Path.GetFileName(path), @"^eqlog_(?<char>[^_]+)_(?<server>.+)\.txt$",
            RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        return new CharacterLog(path, m.Groups["char"].Value, m.Groups["server"].Value);
    }
}

/// <summary>
/// Tails a single character's log file: initial ingest of the whole file (SessionStats
/// auto-rolls on 60-minute gaps, so only the latest play session survives), then
/// incremental reads of appended bytes.
/// </summary>
public sealed class LogWatcher : IDisposable
{
    private readonly SessionStats _stats;
    private readonly System.Timers.Timer _timer;
    private readonly object _lock = new();

    private string? _path;
    private long _offset;
    /// <summary>Read cap for session-range review (#74): Poll never reads past this.
    /// long.MaxValue = live tailing, the normal state.</summary>
    private long _endOffset = long.MaxValue;
    private readonly StringBuilder _remainder = new();

    public DateTime? LastGrowth { get; private set; }
    public string? CurrentPath => _path;
    public bool InitialIngestDone { get; private set; }
    public Exception? LastError { get; private set; }

    /// <summary>Optional second consumer of the parsed event stream. Spawn timers ride
    /// the same replay-then-tail pipeline as stats, which is what lets a restart
    /// re-derive running countdowns from the log's own timestamps.</summary>
    public SpawnTimers? Spawns { get; set; }

    /// <summary>Optional third consumer: the mez-target tracker, same replay-safe
    /// pipeline (its entries are short-lived, so replay mostly proves them expired).</summary>
    public MezTracker? Mez { get; set; }

    public LogWatcher(SessionStats stats)
    {
        _stats = stats;
        // 150 ms, not 500: this interval is the floor on how fast a watch rule can alert,
        // and Text rules are used for time-critical calls (a heal rotation announced by
        // someone else's raid script) where half a second of polling lag is the difference
        // between a useful cue and a late one. A poll on an unchanged file is a length
        // check — cheap enough to run ~7×/s and still be invisible next to the game.
        _timer = new System.Timers.Timer(150) { AutoReset = true };
        _timer.Elapsed += (_, _) => Poll();
    }

    /// <summary>The installed-game folder names, newest product first: a "EverQuest Legends"
    /// install and a plain "EverQuest" one can sit side by side under the same publisher
    /// directory.</summary>
    private static readonly string[] GameFolders = ["EverQuest Legends", "EverQuest"];

    public static string? FindDefaultLogFolder()
    {
        if (OperatingSystem.IsWindows() && FindLogFolderInRegistry() is { } installed)
            return installed;

        return PickLogFolder(CandidateLogFolders());
    }

    /// <summary>The Daybreak installer records the install location in the uninstall registry
    /// key, so custom install paths are found without any user configuration.</summary>
    [SupportedOSPlatform("windows")]
    private static string? FindLogFolderInRegistry()
    {
        foreach (var hive in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
        foreach (var subkey in new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\DGC-EverQuest Legends",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\DGC-EverQuest Legends",
        })
        {
            try
            {
                using var key = hive.OpenSubKey(subkey);
                var marker = key?.GetValue("UninstallString") as string
                             ?? key?.GetValue("DisplayIcon") as string;
                if (marker is null) continue;
                var root = Path.GetDirectoryName(marker.Trim('"'));
                if (root is null) continue;
                var logs = Path.Combine(root, "Logs");
                if (Directory.Exists(logs)) return logs;
            }
            catch { /* registry access denied — fall through */ }
        }
        return null;
    }

    /// <summary>
    /// The candidate that looks most like the install actually being played: the one whose
    /// newest character log was written most recently.
    ///
    /// Existence alone is too weak a signal once several candidates are in play. A Mac with
    /// two Wine wrappers installed has two complete game trees, each with a Logs folder the
    /// installer created — but only the one that has been played holds any `eqlog_*.txt`,
    /// and someone who moved from one wrapper to the other leaves the abandoned tree behind
    /// forever. Falls back to the first existing folder when nothing has been played yet,
    /// which is the pre-existing behaviour for a fresh install.
    /// </summary>
    internal static string? PickLogFolder(IEnumerable<string> candidates)
    {
        var existing = candidates.Where(Directory.Exists).ToList();
        return existing
            .Select(folder => (folder, played: NewestLogWrite(folder)))
            .Where(c => c.played is not null)
            .OrderByDescending(c => c.played)
            .Select(c => c.folder)
            .FirstOrDefault()
            ?? existing.FirstOrDefault();
    }

    /// <summary>When this folder last saw play, or null if it holds no character logs.</summary>
    private static DateTime? NewestLogWrite(string folder)
    {
        // DiscoverCharacters already orders by write time, so the head is the newest.
        if (DiscoverCharacters(folder).FirstOrDefault() is not { } newest) return null;
        try { return File.GetLastWriteTimeUtc(newest.FilePath); }
        catch (IOException) { return null; }
    }

    private static IEnumerable<string> CandidateLogFolders()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var game in GameFolders)
            yield return Path.Combine(@"C:\Users\Public\Daybreak Game Company\Installed Games", game, "Logs");

        yield return Path.Combine(home, ".local", "share", "Daybreak Game Company",
            "Installed Games", "EverQuest Legends", "Logs");

        if (!OperatingSystem.IsMacOS()) yield break;

        foreach (var prefix in WinePrefixRoots(home))
        foreach (var game in GameFolders)
            yield return Path.Combine(prefix, "drive_c", "users", "Public",
                "Daybreak Game Company", "Installed Games", game, "Logs");
    }

    /// <summary>
    /// Directories that may hold a Wine `drive_c` on macOS. EverQuest Legends has no Mac
    /// build, so a Mac player is running it under some Windows compatibility wrapper, and
    /// each wrapper parks its prefix somewhere different. Bottle names are the user's own
    /// words (CrossOver) or a generated id (Whisky), so bottle containers are enumerated
    /// rather than guessed at.
    /// </summary>
    private static IEnumerable<string> WinePrefixRoots(string home)
    {
        var appSupport = Path.Combine(home, "Library", "Application Support");

        // An explicit WINEPREFIX wins: whoever set it means it, and it is the only way to
        // find hand-rolled prefixes and Game Porting Toolkit setups, which have no fixed home.
        if (Environment.GetEnvironmentVariable("WINEPREFIX") is { Length: > 0 } chosen)
            yield return chosen;

        yield return Path.Combine(appSupport, "osxEQL", "prefix");
        yield return Path.Combine(home, ".wine");

        foreach (var container in new[]
        {
            Path.Combine(appSupport, "CrossOver", "Bottles"),
            Path.Combine(home, "Library", "Containers", "com.isaacmarovitz.Whisky", "Bottles"),
            Path.Combine(home, "Library", "PlayOnMac", "wineprefix"),
        })
        foreach (var bottle in ChildDirectories(container))
            yield return bottle;
    }

    private static IEnumerable<string> ChildDirectories(string parent)
    {
        try
        {
            return Directory.Exists(parent) ? Directory.EnumerateDirectories(parent) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CoreLog.Error(ex);
            return [];
        }
    }

    public static List<CharacterLog> DiscoverCharacters(string logFolder)
    {
        if (!Directory.Exists(logFolder)) return [];
        return Directory.EnumerateFiles(logFolder, "eqlog_*.txt")
            .Select(CharacterLog.FromPath)
            .Where(c => c is not null)
            .Select(c => c!)
            .OrderByDescending(c => File.GetLastWriteTimeUtc(c.FilePath))
            .ToList();
    }

    /// <summary>The character whose log grew most recently (the one being played).</summary>
    public static CharacterLog? MostRecentlyActive(string logFolder) =>
        DiscoverCharacters(logFolder).FirstOrDefault();

    public void Select(string path) => Select(path, 0, long.MaxValue);

    /// <summary>Select with a byte range — review mode replaying ONE session out of a
    /// multi-session file (#74). [startOffset, endOffset) must fall on line boundaries
    /// (LogSessions.Scan guarantees it); live tailing is the (0, MaxValue) case.</summary>
    public void Select(string path, long startOffset, long endOffset)
    {
        lock (_lock)
        {
            _timer.Stop();
            _path = path;
            var charInfo = CharacterLog.FromPath(path);
            _stats.CharacterName = charInfo?.Character;
            _stats.ServerName = charInfo?.Server;
            if (Spawns is { } sp) sp.Server = charInfo?.Server ?? "";
            _offset = startOffset;
            _endOffset = endOffset;
            _remainder.Clear();
            InitialIngestDone = false;
            _stats.ClearCharacterState();
            _stats.Reset();
        }
        Task.Run(() =>
        {
            Poll(); // full-file ingest
            lock (_lock) InitialIngestDone = true;
            _timer.Start();
        });
    }

    private void Poll()
    {
        lock (_lock)
        {
            if (_path is null || !File.Exists(_path)) return;
            try
            {
                using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                var readable = Math.Min(fs.Length, _endOffset);
                if (readable < _offset)
                {
                    // File truncated (session cleanup) — re-anchor but keep current stats;
                    // the 60-minute gap rule rolls the session when new play begins.
                    _offset = 0;
                    _remainder.Clear();
                    readable = Math.Min(fs.Length, _endOffset);
                }
                if (readable == _offset) return;

                fs.Seek(_offset, SeekOrigin.Begin);
                var buf = new byte[readable - _offset];
                fs.ReadExactly(buf);
                var chunk = Encoding.Latin1.GetString(buf);
                _offset = readable;
                LastGrowth = DateTime.Now;

                var text = _remainder.ToString() + chunk;
                _remainder.Clear();
                int start = 0;
                while (true)
                {
                    int nl = text.IndexOf('\n', start);
                    if (nl < 0)
                    {
                        _remainder.Append(text, start, text.Length - start);
                        break;
                    }
                    int end = nl > start && text[nl - 1] == '\r' ? nl - 1 : nl;
                    if (end > start)
                    {
                        var line = text[start..end];
                        var evt = LogParser.Parse(line);
                        if (evt is not null)
                        {
                            _stats.Apply(evt);
                            Spawns?.Apply(evt);
                            Mez?.Apply(evt);
                        }
                        // Every line, parsed or not: a Text watch rule matches the line's
                        // words, not whatever event we did or didn't make of it.
                        _stats.ObserveRawLine(line);
                    }
                    start = nl + 1;
                }
            }
            catch (IOException)
            {
                // File busy — try again next tick.
            }
            catch (Exception ex)
            {
                LastError = ex;
            }
        }
    }

    public void Dispose() => _timer.Dispose();
}
