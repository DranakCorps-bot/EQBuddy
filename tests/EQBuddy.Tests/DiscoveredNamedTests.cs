using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

// discussion #185, elderbit: "keeping a manually maintained list fully up to date is
// probably a never-ending task" — every named in The Hole is missing, Chief Goonda in
// West Karana is missing. The log already says which mobs are named, so a proper-named
// kill the catalog doesn't know starts tracking itself and its second death measures
// the cycle.
//
// Most of these tests are about NOT firing. A discovered timer that shouldn't exist
// sends you to a camp that isn't up, which is worse than the gap it was meant to fill.
public class DiscoveredNamedTests
{
    // A zone with no named at all — exactly elderbit's Hole/West Karana case.
    private static SpawnCatalog EmptyZone(string zone) => new()
    {
        Zones = [new SpawnZone { Zone = zone, NamedDefaultSeconds = 738, Named = [] }],
    };

    private static (SpawnTimers Timers, SpawnOverrides Overrides) InZone(string zone, string enterLine)
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(EmptyZone(zone), overrides) { Server = "qeynos" };
        t.Apply(LogParser.Parse(enterLine)!);
        return (t, overrides);
    }

    [Fact]
    public void AProperNamedKillTheCatalogDoesNotKnowStartsTracking()
    {
        var (t, overrides) = InZone("Western Plains of Karana",
            "[Mon Aug 17 19:00:00 2026] You have entered Western Plains of Karana.");
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:10 2026] You have slain Chief Goonda!")!);

        var row = Assert.Single(t.Snapshot(DateTime.Parse("2026-08-17T19:00:11")));
        Assert.Equal("Chief Goonda", row.Name);
        // No duration yet: one kill cannot know the cycle, and a guessed one would lie.
        Assert.Null(row.DurationSeconds);
        Assert.True(overrides.Find("Western Plains of Karana", "Chief Goonda")!.Discovered);
    }

    [Fact]
    public void TheSecondKillMeasuresTheCycle()
    {
        var (t, overrides) = InZone("The Hole",
            "[Mon Aug 17 19:00:00 2026] You have entered The Hole.");
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:00 2026] You have slain Slizik the Mighty!")!);
        // 20 minutes later, the same named again.
        t.Apply(LogParser.Parse("[Mon Aug 17 19:20:00 2026] You have slain Slizik the Mighty!")!);

        var learned = overrides.Find("The Hole", "Slizik the Mighty")!;
        Assert.Equal(1200, learned.RespawnSeconds);
        Assert.True(learned.Learned);
        Assert.Equal(1200, Assert.Single(t.Snapshot(DateTime.Parse("2026-08-17T19:20:01"))).DurationSeconds);
    }

    [Fact]
    public void TrashNeverStartsATimerHoweverOftenItDies()
    {
        // The whole safeguard: "an ogre shaman" and "a skeleton" carry articles.
        var (t, overrides) = InZone("The Hole",
            "[Mon Aug 17 19:00:00 2026] You have entered The Hole.");
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:00 2026] You have slain an ogre shaman!")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:20:00 2026] You have slain an ogre shaman!")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:40:00 2026] You have slain a skeleton!")!);

        Assert.Empty(t.Snapshot(DateTime.Parse("2026-08-17T19:40:01")));
        Assert.Null(overrides.Find("The Hole", "Ogre shaman"));
    }

    [Fact]
    public void SomebodyElsesKillNeverSeedsADiscovery()
    {
        // The gate that removes pets and players wholesale (elderbit raised the pet
        // case): they die as "X has been slain by Y", never as "You have slain X!".
        var (t, overrides) = InZone("The Hole",
            "[Mon Aug 17 19:00:00 2026] You have entered The Hole.");
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:10 2026] Gobanab has been slain by a ghoul!")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:20 2026] Xyrid has been slain by a ghoul!")!);

        Assert.Empty(t.Snapshot(DateTime.Parse("2026-08-17T19:00:21")));
        Assert.Null(overrides.Find("The Hole", "Gobanab"));
        Assert.Null(overrides.Find("The Hole", "Xyrid"));
    }

    [Fact]
    public void ARekillTooFastToBeACycleTeachesNothing()
    {
        // Two mobs sharing a proper name across a zone, same multi-spawn noise the
        // curated path already refuses below MinLearnSeconds.
        var (t, overrides) = InZone("The Hole",
            "[Mon Aug 17 19:00:00 2026] You have entered The Hole.");
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:00 2026] You have slain Slizik the Mighty!")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:30 2026] You have slain Slizik the Mighty!")!);

        Assert.Null(overrides.Find("The Hole", "Slizik the Mighty")!.RespawnSeconds);
    }

    [Fact]
    public void AnOvernightGapIsNotARespawnCycle()
    {
        var (t, overrides) = InZone("The Hole",
            "[Mon Aug 17 09:00:00 2026] You have entered The Hole.");
        t.Apply(LogParser.Parse("[Mon Aug 17 09:00:00 2026] You have slain Slizik the Mighty!")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 21:00:00 2026] You have slain Slizik the Mighty!")!);

        Assert.Null(overrides.Find("The Hole", "Slizik the Mighty")!.RespawnSeconds);
    }

    [Fact]
    public void LearningTightensButNeverLoosens()
    {
        var (t, overrides) = InZone("The Hole",
            "[Mon Aug 17 09:00:00 2026] You have entered The Hole.");
        t.Apply(LogParser.Parse("[Mon Aug 17 09:00:00 2026] You have slain Slizik the Mighty!")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 09:20:00 2026] You have slain Slizik the Mighty!")!);
        Assert.Equal(1200, overrides.Find("The Hole", "Slizik the Mighty")!.RespawnSeconds);

        // A later, longer gap is "you weren't watching", not a slower respawn.
        t.Apply(LogParser.Parse("[Mon Aug 17 10:20:00 2026] You have slain Slizik the Mighty!")!);
        Assert.Equal(1200, overrides.Find("The Hole", "Slizik the Mighty")!.RespawnSeconds);

        // A shorter one is real evidence and tightens it.
        t.Apply(LogParser.Parse("[Mon Aug 17 10:35:00 2026] You have slain Slizik the Mighty!")!);
        Assert.Equal(900, overrides.Find("The Hole", "Slizik the Mighty")!.RespawnSeconds);
    }

    [Fact]
    public void APlayerTypedDurationOutranksAnythingMeasured()
    {
        var (t, overrides) = InZone("The Hole",
            "[Mon Aug 17 09:00:00 2026] You have entered The Hole.");
        t.Apply(LogParser.Parse("[Mon Aug 17 09:00:00 2026] You have slain Slizik the Mighty!")!);
        var o = overrides.Find("The Hole", "Slizik the Mighty")!;
        o.RespawnSeconds = 1080;   // the player typed it
        o.Learned = false;

        t.Apply(LogParser.Parse("[Mon Aug 17 09:20:00 2026] You have slain Slizik the Mighty!")!);

        Assert.Equal(1080, overrides.Find("The Hole", "Slizik the Mighty")!.RespawnSeconds);
        Assert.False(overrides.Find("The Hole", "Slizik the Mighty")!.Learned);
    }

    [Fact]
    public void SerialNamedTrashIsNotDiscoveredWhenItsFamilysNamedIsCatalogued()
    {
        // The limit of elderbit's premise, and it is a real one: "generic mobs are
        // always referenced with an article" does not hold for Sol A's clockworks —
        // "CWG Model XA" is trash and carries no article, so the convention alone would
        // invent a timer per serial number. The catalog listing CWG Model EXG and not
        // his siblings is the statement that the siblings are trash. #181 needed the
        // same family fact to stop them bridging ONTO his clock.
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(SpawnCatalog.LoadEmbedded(), overrides) { Server = "oggok" };
        t.Apply(LogParser.Parse("[Sun Aug 16 15:17:23 2026] You have entered Solusek's Eye 2 (Adaptive).")!);
        t.Apply(LogParser.Parse("[Sun Aug 16 15:33:41 2026] You have slain CWG Model XB!")!);
        t.Apply(LogParser.Parse("[Sun Aug 16 15:34:58 2026] You have slain CWG Model XA!")!);

        Assert.Empty(t.Snapshot(DateTime.Parse("2026-08-16T15:35:00")));
        Assert.Null(overrides.Find("Solusek's Eye", "CWG Model XA"));
    }

    [Fact]
    public void ACatalogNamedIsStillHandledByTheCatalogNotDiscovered()
    {
        var catalog = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Crushbone", NamedDefaultSeconds = 738, NamedDefaultTrusted = true,
                    Named = [new SpawnEntry { Name = "Emperor Crush" }],
                },
            ],
        };
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(catalog, overrides) { Server = "qeynos" };
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:00 2026] You have entered Clan Crushbone.")!);
        t.Apply(LogParser.Parse("[Mon Aug 17 19:00:10 2026] You have slain Emperor Crush!")!);

        Assert.Equal(738, Assert.Single(t.Snapshot(DateTime.Parse("2026-08-17T19:00:11"))).DurationSeconds);
        Assert.False(overrides.Find("Crushbone", "Emperor Crush")?.Discovered ?? false);
    }
}
