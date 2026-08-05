using System.IO;
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

    /// <summary>Optional fourth consumer: the experimental party-DPS tracker (temporary —
    /// see PartyDpsTracker for why it's kept separate from SessionStats). Unlike the other
    /// three consumers, it deliberately does NOT see the startup replay (below) — its whole
    /// point is "what's happening right now," and a replay of the day's whole log would
    /// dump hours of stale damage into it in one burst.</summary>
    public PartyDpsTracker? PartyDps { get; set; }

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

    public static string? FindDefaultLogFolder()
    {
        // The Daybreak installer records the install location in the uninstall registry
        // key, so custom install paths are found without any user configuration.
        if (OperatingSystem.IsWindows())
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
        }

        string[] candidates =
        [
            @"C:\Users\Public\Daybreak Game Company\Installed Games\EverQuest Legends\Logs",
            @"C:\Users\Public\Daybreak Game Company\Installed Games\EverQuest\Logs",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "Daybreak Game Company", "Installed Games",
                "EverQuest Legends", "Logs"),
        ];
        return candidates.FirstOrDefault(Directory.Exists);
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

    public void Select(string path)
    {
        lock (_lock)
        {
            _timer.Stop();
            _path = path;
            var charInfo = CharacterLog.FromPath(path);
            _stats.CharacterName = charInfo?.Character;
            _stats.ServerName = charInfo?.Server;
            if (Spawns is { } sp) sp.Server = charInfo?.Server ?? "";
            _offset = 0;
            _remainder.Clear();
            InitialIngestDone = false;
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
                if (fs.Length < _offset)
                {
                    // File truncated (session cleanup) — re-anchor but keep current stats;
                    // the 60-minute gap rule rolls the session when new play begins.
                    _offset = 0;
                    _remainder.Clear();
                }
                if (fs.Length == _offset) return;

                fs.Seek(_offset, SeekOrigin.Begin);
                using var reader = new StreamReader(fs, Encoding.Latin1, false, 1 << 16, leaveOpen: true);
                var chunk = reader.ReadToEnd();
                _offset = fs.Length;
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
                            if (InitialIngestDone) PartyDps?.Apply(evt);
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
