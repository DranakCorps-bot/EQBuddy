using System.IO;

namespace EQBuddy.Core;

/// <summary>
/// Finds and re-reads the two dumps the Unlocks tab is built from, and caches on their
/// timestamps.
///
/// **It re-reads rather than storing what it parsed.** The alternative is persisting the
/// unlock state into the ledger at import time, and that is the shape behind #204, #210
/// and #212: data survives a move and the write path does not, so a surface goes on
/// showing what was true weeks ago. The files are on disk, the game rewrites them
/// whenever the player asks, and re-reading a 60 KB text file when its timestamp changes
/// costs nothing. Nothing here writes.
///
/// Both dumps live where <see cref="InventoryFile.FindLatest"/> looks — the log folder's
/// PARENT — which is said in those two finders and nowhere else, so they cannot disagree.
/// </summary>
public sealed class UnlockSource
{
    private string _achievementsPath = "";
    private DateTime _achievementsAt;
    private List<AchievementEntry> _achievements = [];

    private string _factionsPath = "";
    private DateTime _factionsAt;
    private FactionsFile.Snapshot? _factions;

    private List<UnlockProgress>? _races;
    private List<UnlockProgress>? _classes;

    /// <summary>Race unlocks as the newest achievements dump states them. Empty when the
    /// player has never run the command — which is a different state from "none unlocked"
    /// and the surface says so.</summary>
    public IReadOnlyList<UnlockProgress> Races => _races ?? [];

    public IReadOnlyList<UnlockProgress> Classes => _classes ?? [];

    /// <summary>The newest faction dump, or null when there is none. Null is a real state:
    /// it is the difference between "you are not maxed with anyone" and "EQBuddy has
    /// never been told where you stand".</summary>
    public FactionsFile.Snapshot? Factions => _factions;

    /// <summary>Has anything been read at all? Drives the tab's empty state, which must
    /// ask for the command rather than showing sixteen unlocks at zero.</summary>
    public bool HasAchievements => _achievements.Count > 0;

    /// <summary>
    /// Re-read whichever dump has changed. Cheap to call on a tick: it stats two files and
    /// returns unless a timestamp moved.
    /// </summary>
    /// <returns>True when something was re-parsed, so a caller can re-render only then.</returns>
    public bool Refresh(string? logFolder, string character)
    {
        if (string.IsNullOrWhiteSpace(logFolder) || string.IsNullOrWhiteSpace(character))
            return false;
        var root = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(logFolder));
        if (root is null || !Directory.Exists(root)) return false;

        var changed = false;
        try
        {
            if (Newest(root, $"{character}_*-Achievements.txt") is { } ach
                && (ach.FullName != _achievementsPath || ach.LastWriteTime != _achievementsAt))
            {
                _achievementsPath = ach.FullName;
                _achievementsAt = ach.LastWriteTime;
                _achievements = AchievementsImport.Parse(File.ReadLines(ach.FullName));
                _races = UnlockRequirements.Races(_achievements);
                _classes = UnlockRequirements.Classes(_achievements);
                changed = true;
            }

            if (Newest(root, $"{character}_*-Factions.txt") is { } fac
                && (fac.FullName != _factionsPath || fac.LastWriteTime != _factionsAt))
            {
                _factionsPath = fac.FullName;
                _factionsAt = fac.LastWriteTime;
                _factions = new FactionsFile.Snapshot(fac.FullName, fac.LastWriteTime,
                    FactionsFile.Parse(File.ReadLines(fac.FullName)));
                changed = true;
            }
        }
        catch { /* a dump being rewritten as we read it is not an error worth surfacing */ }
        return changed;
    }

    private static FileInfo? Newest(string root, string pattern) =>
        Directory.EnumerateFiles(root, pattern)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .FirstOrDefault();
}
