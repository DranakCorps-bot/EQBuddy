using System.Text.RegularExpressions;
using EQBuddy.Companion;
using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// **A page left open across a PC update must not keep running the old JavaScript.**
///
/// The mobile page never re-fetches itself: the socket reconnects forever with backoff,
/// so updating EQBuddy restarts the server, the phone reconnects, and the browser goes on
/// executing whatever it downloaded when the tab was first opened. `Cache-Control:
/// no-store` does nothing about it, because nothing ever asks for the HTML again.
///
/// That is how this feature is MEANT to be used — propped on a desk, added to the Home
/// Screen, left alone for days — which makes it a bug factory: a page-side fix ships, the
/// player updates, the symptom continues, and both sides compare version numbers that
/// agree while looking at different code. It is the leading suspect in #202 (bjstrange),
/// who reported the loot card still churning in a build that provably contains the
/// repaint-gate fix, and it would explain every other page-side fix the same way.
///
/// The envelope has always carried the PC's version; it was only ever printed in the
/// footer. These pin the pair — the server still sends it, and the page still acts on it.
/// </summary>
public class CompanionPageUpdateTests
{
    private static string PageSource()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "EQBuddy.Companion", "Web", "index.html"));
        Assert.True(File.Exists(path), $"the shipped page moved: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>The server's half: every envelope names the version that built it.</summary>
    [Fact]
    public void TheEnvelopeCarriesThePcsVersion()
    {
        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Testchar",
            AppVersion = "9.9.9",
            Offered = CompanionSurfaces.All,
            Stats = new StatsSnapshot(),
            Settings = new AppSettings(),
        }, DateTime.Now);

        Assert.Equal("9.9.9", snap.Identity.AppVersion);
    }

    /// <summary>The page's half: it compares that version against the one it booted with,
    /// on the snapshot path. Reading the shipped file is the only way to assert this —
    /// the same approach `CompanionRepaintGateTests` takes for the repaint gate, and for
    /// the same reason: there is no JS test runner here, and a guard that exists only in
    /// a developer's memory is the thing that went wrong.</summary>
    [Fact]
    public void ThePageChecksThatVersionOnEverySnapshot()
    {
        var page = PageSource();

        Assert.Contains("function staleAfterUpdate(", page);
        Assert.Matches(
            new Regex(@"msg\.kind !== ""snapshot"".*\n\s*if \(msg\.identity && staleAfterUpdate\(",
                RegexOptions.Multiline),
            page);
        Assert.Contains("location.reload()", page);
    }

    /// <summary>And it reloads AT MOST ONCE for a given version. A reload loop against a
    /// cache the page cannot see would be a worse bug than the stale code it is fixing,
    /// so the guard records what it reloaded for and falls back to telling the player.</summary>
    [Fact]
    public void ARefusedReloadBecomesAMessageRatherThanALoop()
    {
        var page = PageSource();

        Assert.Contains("sessionStorage.setItem(RELOADED_FOR", page);
        Assert.Contains("sessionStorage.getItem(RELOADED_FOR)", page);
        Assert.Matches(new Regex(@"already === version[\s\S]{0,220}notice\("), page);
    }
}
