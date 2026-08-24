using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// #234, atrzonkowski: *"Under session history, Mob Farming and Kills by Creature do not
/// pull named mobs from Guk. They are listed in the encounters. Examples: Ghoul Savant,
/// Ghoul Sentinel."* Follow-up: *"In this instance all named I had the killing blow. This
/// was a solo instance with no pet. Frenzied Ghoul, Bloodthirsty Ghoul are also absent."*
///
/// The control the reporter supplied rules out every attribution theory — own killing blow,
/// solo, no pet — which is what makes the remaining explanation the boring one.
///
/// **Nothing is dropped. Both rollups are TOP-N BY KILL COUNT, and a named is by
/// definition the thing you killed once.** `HistoryPresentation` takes the first 10 kills
/// and the first 8 farmed mobs from lists Core sorts `OrderByDescending(count)`. A Guk
/// session has a dozen kinds of trash at ten-plus kills each, so a named at x1 sorts below
/// every one of them and falls off the end of both lists. Encounters is not ranked or
/// truncated, which is exactly why the reporter can see them there — the discrepancy in the
/// report IS the diagnosis.
/// </summary>
public class GukNamedsRollupTests
{
    /// <summary>One Guk session, in the shape the reporter described: trash farmed hard,
    /// four nameds killed once each.</summary>
    private static StatsSnapshot GukSession()
    {
        var stats = new SessionStats();
        var second = 0;
        void Kill(string mob)
        {
            var t = TimeSpan.FromSeconds(second += 20);
            var stamp = new DateTime(2026, 8, 23, 13, 0, 0).Add(t);
            stats.Apply(LogParser.Parse(
                $"[{stamp:ddd MMM d HH:mm:ss yyyy}] You have slain {mob}!")!);
        }

        // Twelve kinds of trash, hammered — an ordinary evening in Lower Guk.
        string[] trash =
        [
            "a froglok tad", "a froglok grunt", "a froglok wizard", "a froglok shaman",
            "a froglok knight", "a froglok scout", "a ghoul", "a decaying skeleton",
            "a young shadow wolf", "a giant rat", "a large snake", "a cavern crawler",
        ];
        foreach (var mob in trash)
            for (var n = 0; n < 12; n++) Kill(mob);

        // The four the reporter named. One kill each, which is what a named IS.
        foreach (var named in new[]
                 { "Ghoul Savant", "Ghoul Sentinel", "Frenzied Ghoul", "Bloodthirsty Ghoul" })
            Kill(named);

        return stats.Snapshot();
    }

    /// <summary>Core is INNOCENT: every named is recorded, with its kill. Whatever is wrong
    /// is downstream of here, which is what the reporter seeing them in Encounters says.</summary>
    [Fact]
    public void CoreRecordsEveryNamedKill()
    {
        var s = GukSession();

        foreach (var named in new[]
                 { "Ghoul Savant", "Ghoul Sentinel", "Frenzied Ghoul", "Bloodthirsty Ghoul" })
        {
            Assert.Contains(s.YourKills, k => k.Name == named);
            Assert.Contains(s.Mobs, m => m.Name == named);
        }
    }

    /// <summary>
    /// **The reported defect, and the fix.** Both rollups used to drop every named, and to do
    /// it silently — no "and 9 more" line, so the surface read as a complete list of what you
    /// killed. Verified against the pre-fix tree: this fails there, four times over.
    /// </summary>
    [Fact]
    public void EveryNamedReachesBothSessionHistoryRollups()
    {
        var text = HistoryPresentation.BuildDetail(Row(), GukSession()).RestText;

        var kills = Section(text, "Kills by creature:");
        var farming = Section(text, "Mob farming");
        foreach (var named in new[]
                 { "Ghoul Savant", "Ghoul Sentinel", "Frenzied Ghoul", "Bloodthirsty Ghoul" })
        {
            Assert.Contains(named, kills);
            Assert.Contains(named, farming);
        }
    }

    /// <summary>A cap that survives says so. A truncated list that looks complete is how
    /// #234 stayed invisible: the player cannot tell a short session from a trimmed one.</summary>
    [Fact]
    public void ARemainingCapSaysWhatItCut()
    {
        var stats = new SessionStats();
        for (var n = 0; n < 20; n++)
            stats.Apply(LogParser.Parse(
                $"[Sun Aug 23 13:{n / 10:D2}:{n % 10:D2} 2026] " +
                $"--You have looted a Rusty Sword {(char)('A' + n)} from a ghoul's corpse.--")!);

        var snapshot = stats.Snapshot();
        // Assert the FIXTURE first. An earlier draft guarded the real assertion with
        // `if (text.Contains("Loot:"))`, which passes silently when the log lines fail to
        // parse — a test that cannot fail is not a test (trap 34/39).
        Assert.Equal(20, snapshot.Loot.Count);

        var text = HistoryPresentation.BuildDetail(Row(), snapshot).RestText;

        Assert.Contains("Loot:", text);
        Assert.Contains("... and 5 more items", text);
    }

    /// <summary>The lists the reporter named carry no cap at all now, so there is nothing
    /// for a "more" line to report — the negative that stops the test above from passing
    /// vacuously if the kills list quietly regains a limit.</summary>
    [Fact]
    public void TheKillListIsNotTruncatedAtAll()
    {
        var s = GukSession();
        var kills = Section(
            HistoryPresentation.BuildDetail(Row(), s).RestText, "Kills by creature:");

        foreach (var kill in s.YourKills) Assert.Contains(kill.Name, kills);
        Assert.DoesNotContain("... and", kills);
    }

    /// <summary>The block under one heading, up to the blank line that ends it.</summary>
    private static string Section(string text, string heading)
    {
        var start = text.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(start >= 0, $"section '{heading}' missing from:\n{text}");
        var end = text.IndexOf("\n\n", start, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    /// <summary>The negative that names the CAUSE rather than the symptom: the lists are
    /// full of trash, so nothing filtered nameds out — they were simply ranked below the
    /// cut. A fix that made the two assertions above pass by dropping the caps entirely
    /// would still have to keep this true.</summary>
    [Fact]
    public void TheRollupsAreFullOfTrashWhichIsWhyTheNamedsFellOff()
    {
        var s = GukSession();
        var text = HistoryPresentation.BuildDetail(Row(), s).RestText;

        // LogParser.Normalize strips the article and capitalises, so the trash reads
        // "Froglok tad" here — the same transform that makes a named indistinguishable
        // from trash by name alone once it reaches SessionStats.
        Assert.Contains("Froglok tad", text);
        Assert.True(s.YourKills.Count > 10, "the kill list must overflow its cap");
        Assert.All(s.YourKills.Take(10), k => Assert.True(k.Count > 1,
            "everything above the cut is farmed trash, not a named"));
    }

    private static SessionRow Row() => new(
        Id: 1, Server: "legends", Character: "Atrzonkowski",
        StartLocal: new DateTime(2026, 8, 23, 13, 0, 0), EndLocal: null,
        ElapsedSeconds: 3600, ActiveSeconds: 3000, EndReason: "closed",
        PrimaryZone: "Lower Guk", Kills: 148, XpPercent: 12, Copper: 0,
        LootCount: 0, Deaths: 0, Dps: 0, Note: "", Tags: "");
}
