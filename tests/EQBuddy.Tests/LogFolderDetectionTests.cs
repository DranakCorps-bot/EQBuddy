using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Choosing between candidate installs. This matters on macOS, where the game runs under a
/// Wine wrapper and a machine can easily carry two complete game trees — an osxEQL prefix
/// and a CrossOver bottle — each with the Logs folder its installer created.
/// </summary>
public class LogFolderDetectionTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("eqbuddy-logfolder-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    /// <summary>A Logs folder, optionally with a character log last written at a given time.</summary>
    private string Install(string name, DateTime? played = null)
    {
        var logs = Path.Combine(_root, name, "Logs");
        Directory.CreateDirectory(logs);
        if (played is not { } when) return logs;

        var log = Path.Combine(logs, "eqlog_Aset_qeynos.txt");
        File.WriteAllText(log, "[Sun Aug 09 22:49:00 2026] You have entered Qeynos.\n");
        File.SetLastWriteTimeUtc(log, when);
        return logs;
    }

    [Fact]
    public void AnAbandonedPrefixLosesToTheOneBeingPlayed()
    {
        var abandoned = Install("osxEQL");                                     // installed, never played
        var played = Install("CrossOver", new DateTime(2026, 8, 9, 22, 49, 0, DateTimeKind.Utc));

        Assert.Equal(played, LogWatcher.PickLogFolder([abandoned, played]));
    }

    /// <summary>Order must not decide it: the abandoned tree is listed first here, and on a
    /// real machine whichever wrapper sorts first would otherwise always win.</summary>
    [Fact]
    public void EvidenceOfPlayBeatsCandidateOrder()
    {
        var played = Install("CrossOver", new DateTime(2026, 8, 9, 22, 49, 0, DateTimeKind.Utc));
        var abandoned = Install("osxEQL");

        Assert.Equal(played, LogWatcher.PickLogFolder([abandoned, played]));
    }

    [Fact]
    public void TwoPlayedInstallsPickTheMoreRecentlyPlayed()
    {
        var lastYear = Install("OldBottle", new DateTime(2025, 8, 9, 22, 49, 0, DateTimeKind.Utc));
        var tonight = Install("NewBottle", new DateTime(2026, 8, 9, 22, 49, 0, DateTimeKind.Utc));

        Assert.Equal(tonight, LogWatcher.PickLogFolder([lastYear, tonight]));
    }

    /// <summary>A fresh install has no logs yet, and still has to be found — otherwise
    /// EQBuddy sends a first-time player to the folder picker for no reason.</summary>
    [Fact]
    public void WithNothingPlayedYetTheFirstExistingFolderWins()
    {
        var first = Install("EverQuest Legends");
        var second = Install("EverQuest");

        Assert.Equal(first, LogWatcher.PickLogFolder([first, second]));
    }

    [Fact]
    public void FoldersThatDoNotExistAreIgnored()
    {
        var real = Install("EverQuest Legends");

        Assert.Equal(real, LogWatcher.PickLogFolder([Path.Combine(_root, "nope"), real]));
    }

    [Fact]
    public void NoCandidatesAtAllMeansNoFolder()
    {
        Assert.Null(LogWatcher.PickLogFolder([Path.Combine(_root, "nope")]));
    }

    /// <summary>Files that aren't character logs don't count as play: the installer drops
    /// dbg.txt and Sky.txt into Logs before the game is ever launched.</summary>
    [Fact]
    public void NonCharacterFilesAreNotEvidenceOfPlay()
    {
        var noise = Install("osxEQL");
        File.WriteAllText(Path.Combine(noise, "dbg.txt"), "debug");
        File.WriteAllText(Path.Combine(noise, "Sky.txt"), "sky");
        var played = Install("CrossOver", new DateTime(2026, 8, 9, 22, 49, 0, DateTimeKind.Utc));

        Assert.Equal(played, LogWatcher.PickLogFolder([noise, played]));
    }
}
