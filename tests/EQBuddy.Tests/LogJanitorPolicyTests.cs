using System.Text.RegularExpressions;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The auto-empty consent question, and the two ways it was defeated.
///
/// Reddit, 2026-08-23, Strilker-TV: *"I ran your program to try it out a week or so ago.
/// It deleted all of my logs. Even the ones not being played. I was keeping all of my log
/// files and had renamed them as they got larger. Those are all gone."* And then, shown
/// the consent page: *"I saw that and I know that I selected the check box. I made sure
/// that I did… Why it didn't take hold properly is unknown."*
///
/// Both halves were real, and neither was the checkbox failing to save.
/// </summary>
public class LogJanitorPolicyTests
{
    // ---- Half one: the tour blocks pruning, on EVERY path -------------------

    [Fact]
    public void TheTourBlocksPruningEvenWhenAutoEmptyIsOn()
    {
        // ShowTutorial means the consent question has not been answered yet. The
        // destructive default may not run before the player can decline it.
        Assert.False(LogJanitorPolicy.ShouldPrune(truncateLogs: true, showTutorial: true));
    }

    [Fact]
    public void OnceTheTourIsDonePruningFollowsThePlayersSetting()
    {
        Assert.True(LogJanitorPolicy.ShouldPrune(truncateLogs: true, showTutorial: false));
        Assert.False(LogJanitorPolicy.ShouldPrune(truncateLogs: false, showTutorial: false));
    }

    [Fact]
    public void DecliningInTheTourIsHonouredWhileTheTourIsStillUp()
    {
        // The tick calls SetTruncateLogs(false) immediately, so this is the state
        // between ticking the box and clicking Finish.
        Assert.False(LogJanitorPolicy.ShouldPrune(truncateLogs: false, showTutorial: true));
    }

    /// <summary>
    /// The actual defect: FOUR places decided this and one of them — the WPF periodic
    /// janitor — asked only <c>TruncateLogs</c>. Because <c>_lastJanitorRun</c> starts at
    /// DateTime.MinValue, that copy ran on the first one-second tick and emptied
    /// everything while the consent dialog was still on page 1.
    ///
    /// This scans both widgets' source so a fifth call site cannot reintroduce it.
    /// Same shape as CompanionSnapshotArgumentTests (trap 33).
    /// </summary>
    [Theory]
    [InlineData("src/EQBuddy/MainWindow.xaml.cs")]
    [InlineData("src/EQBuddy.Avalonia/MainWindow.cs")]
    public void NoWidgetDecidesPruningForItself(string relative)
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), relative));

        // Every assignment of a prune flag must come from the shared policy.
        var assignments = Regex.Matches(source, @"var\s+prune\s*=\s*(?<rhs>[^;]+);");
        Assert.NotEmpty(assignments);
        foreach (Match m in assignments)
        {
            var rhs = m.Groups["rhs"].Value;
            Assert.Contains("LogJanitorPolicy.ShouldPrune", rhs);
        }

        // The negative that keeps this from going vacuous (trap 39): a hand-rolled
        // TruncateLogs test anywhere near the janitor is exactly what shipped.
        Assert.DoesNotContain("prune = _settings.TruncateLogs", source);
    }

    // ---- Half two: the sweep may only empty logs the GAME wrote -------------

    [Theory]
    [InlineData("eqlog_Dranak_legends.txt")]              // the ordinary case
    [InlineData("eqlog_Kaybek_freeport.txt")]
    [InlineData("eqlog_Aenari_erollisi_marr.txt")]        // server short name WITH an underscore
    public void TheGamesOwnLogsAreStillSwept(string name)
        => Assert.True(GameWrittenLog.IsGameWritten(name));

    [Theory]
    [InlineData("eqlog_Strilker_erollisi_2026-08-01.txt")] // the reporter's own shape
    [InlineData("eqlog_Strilker_erollisi 2026-08-01.txt")]
    [InlineData("eqlog_Strilker_erollisi - Copy.txt")]
    [InlineData("eqlog_Strilker_erollisi (1).txt")]
    [InlineData("eqlog_Dranak_legends_20260607143219.txt")] // an archive copy
    [InlineData("eqlog_Dranak_legends.txt.bak")]
    public void APlayersOwnKeptCopyIsNeverSwept(string name)
        => Assert.False(GameWrittenLog.IsGameWritten(name));

    [Fact]
    public void TheSweepLeavesRenamedCopiesAloneOnDisk()
    {
        // End to end through the real sweep, because the predicate being right and the
        // sweep CALLING it are different claims.
        var dir = Directory.CreateTempSubdirectory("eqbuddy-janitor-").FullName;
        try
        {
            var live = Path.Combine(dir, "eqlog_Strilker_erollisi.txt");
            var kept = Path.Combine(dir, "eqlog_Strilker_erollisi_2026-08-01.txt");
            File.WriteAllText(live, new string('x', 4000));
            File.WriteAllText(kept, "a month of play the player deliberately kept");
            var old = DateTime.Now.AddHours(-5);
            File.SetLastWriteTime(live, old);
            File.SetLastWriteTime(kept, old);

            var count = EqConfig.TruncateStaleLogs(dir, TimeSpan.FromMinutes(60),
                ignoreGameCheck: true);

            Assert.Equal(1, count);
            Assert.Equal(0, new FileInfo(live).Length);
            Assert.Equal("a month of play the player deliberately kept",
                File.ReadAllText(kept));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EQBuddy.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
