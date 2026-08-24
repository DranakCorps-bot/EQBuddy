using System.Diagnostics;
using System.IO;

namespace EQBuddy.Core;

/// <summary>
/// Manages the game's own configuration and log hygiene:
/// - keeps Log=1 in eqclient.ini so /log is always on,
/// - truncates stale log files so they never grow across sessions.
/// Both only act while the game is NOT running (the game rewrites its ini on exit,
/// and truncating a file the game holds open is unsafe).
/// </summary>
public static class EqConfig
{
    public static bool IsGameRunning() => Process.GetProcessesByName("eqgame").Length > 0;

    /// <summary>
    /// Other tools that tail the same eqlog files (COMPAT-001). GINA keeps a byte
    /// offset into the log; emptying the file under it leaves that offset past
    /// end-of-file and GINA silently stops firing triggers until restarted — reported
    /// on Reddit as "this breaks GINA" (2026-08-02). Reading never conflicts; only our
    /// truncation does, so it stands down while any of these is running, same as it
    /// does for the game itself. GamParse gets the same courtesy for the same reason.
    /// </summary>
    private static readonly string[] KnownLogReaders = ["GINA", "GamParse"];

    /// <summary>A log-tailing tool (GINA, GamParse) is running right now.</summary>
    public static bool IsLogReaderRunning() =>
        KnownLogReaders.Any(name => Process.GetProcessesByName(name).Length > 0);

    /// <summary>eqclient.ini lives in the install root, one level above Logs.</summary>
    public static string? FindClientIni(string logFolder)
    {
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(logFolder));
        if (root is null) return null;
        var ini = Path.Combine(root, "eqclient.ini");
        return File.Exists(ini) ? ini : null;
    }

    /// <summary>
    /// Set Log=1 in the [Defaults] section only (REL-002: section-aware — a "Log=" key in
    /// any other section is left alone, as is every other line). Returns true if changed.
    /// </summary>
    public static bool EnsureLoggingEnabled(string logFolder, bool ignoreGameCheck = false)
    {
        try
        {
            if (!ignoreGameCheck && IsGameRunning()) return false;
            var ini = FindClientIni(logFolder);
            if (ini is null) return false;

            var lines = File.ReadAllLines(ini).ToList();
            int defaultsHeader = -1;
            int logLine = -1;
            var inDefaults = false;
            for (var i = 0; i < lines.Count; i++)
            {
                var t = lines[i].Trim();
                if (t.StartsWith('[') && t.EndsWith(']'))
                {
                    inDefaults = t.Equals("[Defaults]", StringComparison.OrdinalIgnoreCase);
                    if (inDefaults) defaultsHeader = i;
                    continue;
                }
                if (inDefaults && t.StartsWith("Log=", StringComparison.OrdinalIgnoreCase))
                {
                    logLine = i;
                    break;
                }
            }

            if (logLine >= 0)
            {
                if (lines[logLine].Trim().Equals("Log=1", StringComparison.OrdinalIgnoreCase))
                    return false;
                lines[logLine] = "Log=1";
            }
            else if (defaultsHeader >= 0)
            {
                lines.Insert(defaultsHeader + 1, "Log=1");
            }
            else
            {
                lines.Insert(0, "[Defaults]");
                lines.Insert(1, "Log=1");
            }
            File.WriteAllLines(ini, lines);
            return true;
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return false;
        }
    }

    /// <summary>
    /// Empty every character log whose last activity is older than <paramref name="staleAfter"/>.
    /// A stale log is a finished play session; wiping it keeps files session-sized forever.
    /// With <paramref name="archive"/>, the content is first copied to
    /// Logs\archive\eqlog_name_server_yyyyMMddHHmmss.txt (stamp = the log's last write,
    /// i.e. when the session actually ended) — joeymavity's ask, discussion #52. The
    /// archive SUBFOLDER is load-bearing: a timestamped copy in the log folder itself
    /// would match this very method's glob next sweep and get truncated too.
    /// Returns the number of files truncated. <paramref name="archived"/> is invoked
    /// with the full path of each copy actually written — the janitor runs unattended on
    /// a background thread, and an archive nobody is told about is indistinguishable from
    /// one that never happened (#159, Frankthetankk, who lost a session and then had no
    /// way to check whether archiving was working at all).
    /// </summary>
    public static int TruncateStaleLogs(string logFolder, TimeSpan staleAfter,
        bool ignoreGameCheck = false, bool archive = false, Action<string>? archived = null)
    {
        if (!ignoreGameCheck && (IsGameRunning() || IsLogReaderRunning())) return 0;
        if (!Directory.Exists(logFolder)) return 0;
        int truncated = 0;
        foreach (var f in Directory.EnumerateFiles(logFolder, "eqlog_*.txt"))
        {
            // The glob is the game's shape AND the shape of every copy a player keeps
            // beside it, so it is not on its own permission to destroy the file
            // (GameWrittenLog, Reddit 2026-08-23). Enumerate with it, then decide.
            if (!GameWrittenLog.IsGameWritten(f)) continue;
            try
            {
                var fi = new FileInfo(f);
                if (fi.Length > 0 && DateTime.Now - fi.LastWriteTime > staleAfter)
                {
                    if (archive && ArchiveCopy(fi) is { } dest) archived?.Invoke(dest);
                    using var fs = new FileStream(f, FileMode.Open, FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete);
                    fs.SetLength(0);
                    truncated++;
                }
            }
            catch { /* file busy — skip, try next sweep */ }
        }
        return truncated;
    }

    /// <summary>Copies the log into Logsrchive. Returns the destination, or null when
    /// an identical copy is already there (see the dedup reasoning below) — the caller
    /// announces only real writes, so a retried sweep cannot claim an archive twice.</summary>
    private static string? ArchiveCopy(FileInfo log)
    {
        // A sweep whose copy succeeded but whose truncate lost the file-lock race
        // retries next sweep with the log unchanged — same stamp, same length — and
        // stacked a -2/-3 duplicate of the identical content every pass until the
        // lock cleared (audit finding 11a). Same stamp + same length is identity
        // here: the stamp IS the content's own last write. A same-stamp archive of
        // a DIFFERENT length still gets its dedup copy (the documented
        // same-second-twice case).
        if (AlreadyArchived(log)) return null;
        var dest = ArchiveDest(log);
        log.CopyTo(dest);
        return dest;
    }

    /// <summary>An archive copy of exactly this content already exists.</summary>
    private static bool AlreadyArchived(FileInfo log)
    {
        var dir = Path.Combine(log.DirectoryName!, "archive");
        if (!Directory.Exists(dir)) return false;
        var stem = $"{Path.GetFileNameWithoutExtension(log.Name)}_{log.LastWriteTime:yyyyMMddHHmmss}";
        return Directory.EnumerateFiles(dir, stem + "*.txt")
            .Any(f => new FileInfo(f).Length == log.Length);
    }

    /// <summary>Pick Logs\archive\eqlog_name_server_STAMP.txt, stamp = the log's last
    /// write (when the session actually ended), creating the folder if needed.</summary>
    private static string ArchiveDest(FileInfo log)
    {
        var dir = Path.Combine(log.DirectoryName!, "archive");
        Directory.CreateDirectory(dir);
        var stamp = log.LastWriteTime.ToString("yyyyMMddHHmmss");
        var dest = Path.Combine(dir,
            $"{Path.GetFileNameWithoutExtension(log.Name)}_{stamp}.txt");
        // Same second twice (clock oddities, manual copies): keep both rather than clobber.
        for (var n = 2; File.Exists(dest); n++)
            dest = Path.Combine(dir,
                $"{Path.GetFileNameWithoutExtension(log.Name)}_{stamp}-{n}.txt");
        return dest;
    }

    /// <summary>
    /// Split a live log right now: move its content to Logs\archive and leave a fresh
    /// empty file in its place (#52, the "Reset session stats" half of the ask).
    /// Rename-not-truncate is deliberate: the game writes each line open-append-close
    /// (joeymavity field-tested that EQL just starts writing the base name again), so a
    /// move mid-play loses nothing — unlike SetLength(0), which could leave a
    /// cached-offset writer null-padding the file. The empty base file is recreated
    /// immediately so most-recently-written character detection doesn't wander off to
    /// an alt's log while waiting for the game's next line. Note: a log-tailing tool
    /// (GINA) will need its own restart after a split, same as after any truncation —
    /// unlike the janitor sweep this runs on an explicit click, so it doesn't stand
    /// down for them. Returns the archive path, or null if there was nothing to split
    /// or the move failed (file locked — nothing is lost, the log just keeps growing).
    /// </summary>
    public static string? SplitLog(string logPath)
    {
        try
        {
            var fi = new FileInfo(logPath);
            if (!fi.Exists || fi.Length == 0) return null;
            var dest = ArchiveDest(fi);
            fi.MoveTo(dest);
            using (File.Create(logPath)) { }
            return dest;
        }
        catch (Exception ex)
        {
            CoreLog.Error(ex);
            return null;
        }
    }
}
