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

    public LogWatcher(SessionStats stats)
    {
        _stats = stats;
        _timer = new System.Timers.Timer(500) { AutoReset = true };
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
            _stats.CharacterName = CharacterLog.FromPath(path)?.Character;
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
                        var evt = LogParser.Parse(text[start..end]);
                        if (evt is not null) _stats.Apply(evt);
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
