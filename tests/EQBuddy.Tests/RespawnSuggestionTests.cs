using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The honesty bar for suggesting respawn timers to eqlwiki (the spawn-timer feed,
/// Fable 5's plan 2026-08-22). The bar IS the product: too low and EQBuddy becomes the
/// source of uniquely wrong timers on the shared reference; too high and nothing is ever
/// suggested. The plan's own acceptance pair is here: three agreeing cycles MUST suggest,
/// and a scattered sample MUST NOT.
/// </summary>
public class RespawnSuggestionTests
{
    private static SpawnCycle C(double seconds) => new(seconds, "Rekill", DateTime.Now);

    [Fact]
    public void ThreeAgreeingCyclesSuggestTheMedianInTheWikisIdiom()
    {
        var v = RespawnSuggestion.Evaluate([C(738), C(744), C(731)], suppressed: false, wikiField: "");

        Assert.Equal(RespawnVerdictKind.Suggest, v.Kind);
        Assert.Equal("12.3 min", v.Wording);
        Assert.Contains("3 agreeing cycles", v.Note);
    }

    /// <summary>Kill-to-kill alone does not determine a duration — a scattered sample
    /// usually means the camp was not watched end to end, and it suggests NOTHING.</summary>
    [Fact]
    public void ScatteredCyclesSuggestNothing()
    {
        var v = RespawnSuggestion.Evaluate(
            [C(738), C(1900), C(731)], suppressed: false, wikiField: "");

        Assert.Equal(RespawnVerdictKind.None, v.Kind);
        Assert.Contains("do not agree", v.Note);
    }

    [Fact]
    public void FewerThanThreeCyclesSuggestNothingHoweverCleanTheyAre()
    {
        Assert.Equal(RespawnVerdictKind.None,
            RespawnSuggestion.Evaluate([C(738), C(738)], false, "").Kind);
        Assert.Contains("the bar is 3",
            RespawnSuggestion.Evaluate([C(738)], false, "").Note);
    }

    /// <summary>Triggered / raid-instanced / multi-spawn: no cycle exists to suggest
    /// (#109's bees), whatever the ledger holds.</summary>
    [Fact]
    public void ASuppressedEntrySuggestsNothingWhateverTheCyclesSay()
    {
        Assert.Equal(RespawnVerdictKind.None,
            RespawnSuggestion.Evaluate([C(738), C(738), C(738)], suppressed: true, "").Kind);
    }

    [Fact]
    public void AMedianUnderTheFloorSuggestsNothing()
    {
        // 60-second gaps that agree perfectly are multi-spawn noise, not a cycle.
        Assert.Equal(RespawnVerdictKind.None,
            RespawnSuggestion.Evaluate([C(60), C(62), C(58)], false, "").Kind);
    }

    /// <summary>The three-way compare: a wiki that already says it gets "nothing to
    /// add" (the KnownDrops line for timers); a wiki that disagrees gets both numbers
    /// and never a paste-over.</summary>
    [Fact]
    public void TheWikiFieldDecidesAgreeVersusReconcile()
    {
        var agrees = RespawnSuggestion.Evaluate([C(570), C(575), C(566)], false, "9.5 min");
        Assert.Equal(RespawnVerdictKind.WikiAgrees, agrees.Kind);
        Assert.Contains("nothing to add", agrees.Note);

        var disagrees = RespawnSuggestion.Evaluate([C(570), C(575), C(566)], false, "25 min");
        Assert.Equal(RespawnVerdictKind.WikiDisagrees, disagrees.Kind);
        Assert.Contains("compare, don't overwrite", disagrees.Note);

        // Free prose that is not a number at all ("Triggered") is a DISAGREEMENT to
        // show, never silently treated as agreement.
        Assert.Equal(RespawnVerdictKind.WikiDisagrees,
            RespawnSuggestion.Evaluate([C(570), C(575), C(566)], false, "Triggered").Kind);
    }

    /// <summary>The two idioms stay apart: ours is the chip's, the wiki's is prose
    /// minutes/hours/days — and never a variance clause, because three cycles cannot
    /// measure variance.</summary>
    [Theory]
    [InlineData(570, "9.5 min")]
    [InlineData(1320, "22 min")]
    [InlineData(21600, "6 hours")]
    [InlineData(259200, "3 days")]
    public void WikiWordingUsesTheWikisOwnIdiom(double seconds, string expected)
    {
        Assert.Equal(expected, RespawnSuggestion.WikiWording(seconds));
    }

    // ---- through the pack ----

    private static MobSummary Mob(string name, params string[] loot) =>
        new(name, 5, 5, 8.0, 1.0, 50,
            loot.Select(i => new MobLoot(i, 1, 20.0)).ToList())
        { Zone = "Crushbone" };

    private static MobLookupResult Page(string respawnField, params string[] drops) =>
        new(new MobInfo
        {
            IsCreaturePage = true,
            Name = "x",
            PageTitle = "Bloodgurgler",
            RespawnField = respawnField,
            Drops = drops.Select(d => (d, "")).ToList(),
        }, ItemLookupState.Cached, DateTime.UtcNow);

    [Fact]
    public void AnAllKnownCreatureWithAgreeingCyclesEarnsARespawnSectionInTheExport()
    {
        var obs = new[]
        {
            new WikiContribution.MobObservation(
                Mob("Bloodgurgler", "Slaver's Whip"),
                Page("", "Slaver's Whip"),
                new WikiContribution.RespawnEvidence([C(738), C(744), C(731)], Suppressed: false)),
        };
        var export = WikiContribution.BuildExport(obs, "Hugzee", "qeynos", "Crushbone", DateTime.Now);

        Assert.Contains("respawn timer observed ===", export);
        Assert.Contains("| respawn_time  = 12.3 min", export);
        Assert.Contains("Cycles behind it", export);
        Assert.DoesNotContain("Nothing confirmed new yet", export);

        // And the negative: an unread page gets no claim of any kind.
        var unread = new[]
        {
            new WikiContribution.MobObservation(
                Mob("Bloodgurgler", "Slaver's Whip"),
                new MobLookupResult(null, ItemLookupState.Offline, null),
                new WikiContribution.RespawnEvidence([C(738), C(744), C(731)], false)),
        };
        Assert.DoesNotContain("respawn_time",
            WikiContribution.BuildExport(unread, "Hugzee", "qeynos", "Crushbone", DateTime.Now));
    }

    [Fact]
    public void TheRespawnRowAppearsInThePackAndCountsInTheHeadline()
    {
        var pack = WikiPackPresentation.Build(
        [
            new WikiContribution.MobObservation(
                Mob("Bloodgurgler", "Slaver's Whip"),
                Page("", "Slaver's Whip"),
                new WikiContribution.RespawnEvidence([C(738), C(744), C(731)], false)),
        ]);

        var row = Assert.Single(pack.Rows);
        Assert.Equal(WikiPackPresentation.RowKind.RespawnObserved, row.Kind);
        Assert.Contains("observed 12.3 min", row.Note);
        Assert.Equal(1, pack.RespawnCreatures);
        Assert.Equal("1 respawn timer for the wiki", WikiPackPresentation.Headline(pack));
        Assert.True(WikiPackPresentation.CanCopy(pack));

        // Scattered cycles: no row, no false confidence.
        var scattered = WikiPackPresentation.Build(
        [
            new WikiContribution.MobObservation(
                Mob("Bloodgurgler", "Slaver's Whip"),
                Page("", "Slaver's Whip"),
                new WikiContribution.RespawnEvidence([C(738), C(1900), C(731)], false)),
        ]);
        Assert.Empty(scattered.Rows);
    }

    // ---- through SpawnTimers: cycles are recorded exactly where a gap passes the
    // honesty gates, and never otherwise ----

    private static SpawnCatalog OneNamed(string zone, string name,
        double? respawn = 738, string spawnType = "", bool multiSpawn = false) => new()
    {
        Zones =
        [
            new SpawnZone
            {
                Zone = zone,
                NamedDefaultSeconds = 738,
                Named =
                [
                    new SpawnEntry
                    {
                        Name = name, RespawnSeconds = respawn,
                        SpawnType = spawnType, MultiSpawn = multiSpawn,
                    },
                ],
            },
        ],
    };

    [Fact]
    public void ARekillGapIsRecordedEvenWhenItDoesNotTightenTheCountdown()
    {
        var cycles = new SpawnCycleLedger();
        var t = new SpawnTimers(OneNamed("Crushbone", "Bloodgurgler"),
            new SpawnOverrides(), cycles: cycles) { Server = "qeynos" };
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:00 2026] You have entered Crushbone.")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:10 2026] You have slain Bloodgurgler!")!);
        // 750 s later: LONGER than the catalog's 738, so the countdown must not learn
        // it — but it is a real observed cycle and the ledger keeps it.
        t.Apply(LogParser.Parse("[Mon Aug 17 19:12:40 2026] You have slain Bloodgurgler!")!);

        var cycle = Assert.Single(cycles.For("qeynos", "Crushbone", "Bloodgurgler"));
        Assert.Equal(750, cycle.DurationSeconds);
        Assert.Equal("Rekill", cycle.Kind);
    }

    [Fact]
    public void ATriggeredEntryNeverRecordsACycle()
    {
        var cycles = new SpawnCycleLedger();
        var t = new SpawnTimers(OneNamed("Plane of Sky", "Bzzzt", respawn: null, spawnType: "triggered"),
            new SpawnOverrides(), cycles: cycles) { Server = "qeynos" };
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:00 2026] You have entered Plane of Sky.")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:10 2026] You have slain Bzzzt!")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:12:40 2026] You have slain Bzzzt!")!);

        Assert.Empty(cycles.For("qeynos", "Plane of Sky", "Bzzzt"));
    }

    [Fact]
    public void AGapAcrossAZoneTripIsNeverACycle()
    {
        var cycles = new SpawnCycleLedger();
        var t = new SpawnTimers(OneNamed("Crushbone", "Bloodgurgler"),
            new SpawnOverrides(), cycles: cycles) { Server = "qeynos" };
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:00 2026] You have entered Crushbone.")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:10 2026] You have slain Bloodgurgler!")!);
        // Left for Freeport and came back — the gap is an upper bound, not a cycle.
        t.Apply(LogParser.Parse("[Mon Aug 17 19:03:00 2026] You have entered East Freeport.")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:05:00 2026] You have entered Crushbone.")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:12:40 2026] You have slain Bloodgurgler!")!);

        Assert.Empty(cycles.For("qeynos", "Crushbone", "Bloodgurgler"));
    }

    [Fact]
    public void TheLedgerCapsAtTwentyCyclesNewestKept()
    {
        var ledger = new SpawnCycleLedger();
        for (var i = 0; i < 25; i++)
            ledger.Record("qeynos", "Crushbone", "Bloodgurgler", 700 + i, "Rekill", DateTime.Now);

        var kept = ledger.For("qeynos", "Crushbone", "Bloodgurgler");
        Assert.Equal(SpawnCycleLedger.Cap, kept.Count);
        Assert.Equal(705, kept[0].DurationSeconds);   // the five oldest fell off
        Assert.Equal(724, kept[^1].DurationSeconds);
    }
}
