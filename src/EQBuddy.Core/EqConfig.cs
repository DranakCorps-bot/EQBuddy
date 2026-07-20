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

    /// <summary>eqclient.ini lives in the install root, one level above Logs.</summary>
    public static string? FindClientIni(string logFolder)
    {
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(logFolder));
        if (root is null) return null;
        var ini = Path.Combine(root, "eqclient.ini");
        return File.Exists(ini) ? ini : null;
    }

    /// <summary>Set Log=1 in [Defaults]. Returns true if the file was changed.</summary>
    public static bool EnsureLoggingEnabled(string logFolder, bool ignoreGameCheck = false)
    {
        try
        {
            if (!ignoreGameCheck && IsGameRunning()) return false;
            var ini = FindClientIni(logFolder);
            if (ini is null) return false;

            var lines = File.ReadAllLines(ini).ToList();
            int idx = lines.FindIndex(l =>
            {
                var t = l.TrimStart();
                return t.StartsWith("Log=", StringComparison.OrdinalIgnoreCase);
            });
            if (idx >= 0)
            {
                if (lines[idx].Trim().Equals("Log=1", StringComparison.OrdinalIgnoreCase))
                    return false;
                lines[idx] = "Log=1";
            }
            else
            {
                int defaults = lines.FindIndex(l => l.Trim().Equals("[Defaults]", StringComparison.OrdinalIgnoreCase));
                if (defaults >= 0) lines.Insert(defaults + 1, "Log=1");
                else { lines.Insert(0, "[Defaults]"); lines.Insert(1, "Log=1"); }
            }
            File.WriteAllLines(ini, lines);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Empty every character log whose last activity is older than <paramref name="staleAfter"/>.
    /// A stale log is a finished play session; wiping it keeps files session-sized forever.
    /// Returns the number of files truncated.
    /// </summary>
    public static int TruncateStaleLogs(string logFolder, TimeSpan staleAfter, bool ignoreGameCheck = false)
    {
        if (!ignoreGameCheck && IsGameRunning()) return 0;
        if (!Directory.Exists(logFolder)) return 0;
        int truncated = 0;
        foreach (var f in Directory.EnumerateFiles(logFolder, "eqlog_*.txt"))
        {
            try
            {
                var fi = new FileInfo(f);
                if (fi.Length > 0 && DateTime.Now - fi.LastWriteTime > staleAfter)
                {
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
}
