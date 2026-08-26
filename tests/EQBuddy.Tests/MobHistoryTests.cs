using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The wiki pack's history pool (#217 ask 2, Frankthetankk). The concrete miss these
/// exist for: three 4-kill sessions never crossed the 10-kill rarity bar despite twelve
/// real kills — the bar is right and stays; the evidence now survives the session
/// boundary. The reporter's own test description ("a fixture of several fake snapshots
/// asserting the pooled counts") is what this file is.
/// </summary>
public class MobHistoryTests
{
    private static MobSummary Mob(string name, int kills, string zone = "Guk",
        params (string Item, int Count)[] loot) =>
        new(name, kills, kills, 8.0, 1.0, kills * 10,
            loot.Select(l => new MobLoot(l.Item, l.Count, 100.0 * l.Count / kills)).ToList())
        { Zone = zone };

    private static MobHistory.SessionMobs Session(long id, params MobSummary[] mobs) =>
        new(id, "freeport", "Dranak", new DateTime(2026, 8, 20).AddDays(id).AddHours(-2),
            new DateTime(2026, 8, 20).AddDays(id), mobs);

    [Fact]
    public void ThreeThinSessionsPoolPastTheTenKillBar()
    {
        var rows = new[]
        {
            Session(1, Mob("Ghoul Savant", 4, loot: ("Savant's Cap", 1))),
            Session(2, Mob("Ghoul Savant", 4, loot: ("Savant's Cap", 1))),
            Session(3, Mob("Ghoul Savant", 4, loot: ("Savant's Cap", 2))),
        };

        var (mobs, scope) = MobHistory.Pool(rows, live: null, "Dranak", "freeport", liveRowId: 0);

        var savant = Assert.Single(mobs);
        Assert.Equal(12, savant.Kills);
        var cap = Assert.Single(savant.Loot);
        Assert.Equal(4, cap.Count);
        // The rate is recomputed from the POOLED numbers, same formula as the live
        // snapshot: 100 x 4 / 12.
        Assert.Equal(33.3, cap.DropRatePct!.Value, 1);
        // And the bar the whole item exists for: 12 kills can now carry a label.
        Assert.NotNull(WikiContribution.SuggestRarity(cap.DropRatePct, savant.Kills));
        Assert.Equal(3, scope.SessionCount);
    }

    /// <summary>(name, zone) keying — a session's Zone is the kill zone (the #65 fix),
    /// and "an ice giant" in two zones is two mobs, never one averaged one.</summary>
    [Fact]
    public void TheSameNameInTwoZonesIsTwoMobs()
    {
        var rows = new[]
        {
            Session(1, Mob("an ice giant", 3, zone: "Everfrost")),
            Session(2, Mob("an ice giant", 5, zone: "Permafrost")),
        };

        var (mobs, _) = MobHistory.Pool(rows, null, "Dranak", "freeport", 0);

        Assert.Equal(2, mobs.Count);
        Assert.Equal(5, mobs.First(m => m.Zone == "Permafrost").Kills);
        Assert.Equal(3, mobs.First(m => m.Zone == "Everfrost").Kills);
    }

    /// <summary>The live session's checkpointed row is EXCLUDED and its kills come from
    /// the live snapshot — counting both would pool the same kills twice.</summary>
    [Fact]
    public void TheLiveSessionsCheckpointedRowIsPooledOnce()
    {
        var rows = new[]
        {
            Session(1, Mob("orc pawn", 5)),
            // The live session's own checkpoint, already landed in the DB.
            Session(42, Mob("orc pawn", 3)),
        };
        var live = new StatsSnapshot
        {
            SessionStart = DateTime.Now.AddHours(-1),
            Mobs = [Mob("orc pawn", 4)],
        };

        var (mobs, scope) = MobHistory.Pool(rows, live, "Dranak", "freeport", liveRowId: 42);

        Assert.Equal(9, Assert.Single(mobs).Kills);   // 5 stored + 4 live; the 3 is the stale checkpoint
        Assert.Equal(2, scope.SessionCount);
    }

    /// <summary>The id alone has a timing hole the staged shot found on its first run:
    /// ActiveRowId is set by the FIRST checkpoint, so a pool computed before it — a
    /// re-ingested log whose sessions are already archived, the adoption case — counted
    /// the archived twin AND the live snapshot, and every number doubled (the Asp read
    /// 12 kills from 5). The same (server, character, session start) is the same session,
    /// which is Checkpoint's own adoption rule.</summary>
    [Fact]
    public void TheLiveSessionsArchivedTwinIsExcludedByIdentityBeforeTheFirstCheckpoint()
    {
        var start = new DateTime(2026, 8, 26, 5, 0, 0);
        var rows = new[]
        {
            new MobHistory.SessionMobs(7, "freeport", "Dranak", start,
                start.AddHours(1), [Mob("an asp", 5)]),
        };
        var live = new StatsSnapshot { SessionStart = start, Mobs = [Mob("an asp", 5)] };

        // liveRowId 0 — the first checkpoint has not landed yet.
        var (mobs, scope) = MobHistory.Pool(rows, live, "Dranak", "freeport", liveRowId: 0);

        Assert.Equal(5, Assert.Single(mobs).Kills);
        Assert.Equal(1, scope.SessionCount);
    }

    /// <summary>Unknown stays unknown: -1 coin / 0 level from a pre-field snapshot never
    /// narrows a real range and never counts as an observation of zero.</summary>
    [Fact]
    public void UnknownCoinAndLevelStayUnknownAndNeverNarrowARealRange()
    {
        var known = Mob("a puma", 2) with { CoinMin = 5, CoinMax = 30, LevelMin = 8, LevelMax = 10 };
        var unknown = Mob("a puma", 3);   // CoinMin -1, LevelMin 0 — never seen, never conned

        var (mobs, _) = MobHistory.Pool(
            [Session(1, known), Session(2, unknown)], null, "Dranak", "freeport", 0);

        var puma = Assert.Single(mobs);
        Assert.Equal(5, puma.CoinMin);
        Assert.Equal(30, puma.CoinMax);
        Assert.Equal(8, puma.LevelMin);
        Assert.Equal(10, puma.LevelMax);

        var (allUnknown, _) = MobHistory.Pool(
            [Session(1, unknown)], null, "Dranak", "freeport", 0);
        Assert.Equal(-1, Assert.Single(allUnknown).CoinMin);
        Assert.Equal(0, Assert.Single(allUnknown).LevelMin);
    }

    [Fact]
    public void LootFoldsOnBaseItemNameAndKeepsTheLatestSighting()
    {
        var early = Mob("a spider", 2, loot: ("Spider Silk", 1)) with
        {
            Loot = [new MobLoot("Spider Silk +1", 1, 50.0) { LastAt = new DateTime(2026, 8, 1) }],
        };
        var late = Mob("a spider", 2) with
        {
            Loot = [new MobLoot("Spider Silk", 2, 100.0) { LastAt = new DateTime(2026, 8, 20) }],
        };

        var (mobs, _) = MobHistory.Pool(
            [Session(1, early), Session(2, late)], null, "Dranak", "freeport", 0);

        var silk = Assert.Single(Assert.Single(mobs).Loot);
        Assert.Equal(3, silk.Count);   // the +1 tier folds into the base item (#65)
        Assert.Equal(new DateTime(2026, 8, 20), silk.LastAt);
    }

    [Fact]
    public void FactionHitsSumAndConsidersPool()
    {
        var a = Mob("gnoll pup", 3) with
        {
            Factions = [new MobFactionHit("Sabertooths of Blackburrow", -5, 3)],
            Considers = 2,
            RareConsiders = 1,
        };
        var b = Mob("gnoll pup", 2) with
        {
            Factions = [new MobFactionHit("Sabertooths of Blackburrow", -5, 2)],
            Considers = 1,
            RareConsiders = 0,
        };

        var (mobs, _) = MobHistory.Pool(
            [Session(1, a), Session(2, b)], null, "Dranak", "freeport", 0);

        var pup = Assert.Single(mobs);
        var hit = Assert.Single(pup.Factions);
        Assert.Equal(5, hit.Hits);
        Assert.Equal(-5, hit.Delta);
        Assert.Equal(3, pup.Considers);
        Assert.Equal(1, pup.RareConsiders);
    }

    /// <summary>The scope names everyone pooled — decision 1 (across characters AND
    /// servers) is only honest because this list reaches the scope line.</summary>
    [Fact]
    public void TheScopeNamesEveryCharacterAndServerPooled()
    {
        var rows = new[]
        {
            new MobHistory.SessionMobs(1, "freeport", "Dranak",
                new DateTime(2026, 7, 30).AddHours(-2), new DateTime(2026, 7, 30), [Mob("orc pawn", 1)]),
            new MobHistory.SessionMobs(2, "freeport", "Flossie",
                new DateTime(2026, 8, 10).AddHours(-2), new DateTime(2026, 8, 10), [Mob("orc pawn", 2)]),
        };

        var (_, scope) = MobHistory.Pool(rows, null, "Dranak", "freeport", 0);

        Assert.Equal(["Dranak", "Flossie"], scope.Characters);
        Assert.Equal(["freeport"], scope.Servers);
        Assert.Equal(new DateTime(2026, 7, 30), scope.Earliest);
        Assert.Equal(new DateTime(2026, 8, 10), scope.Latest);
    }

    /// <summary>The repository probe end to end: sessions written through the real
    /// Checkpoint land in MobRows with their mobs intact, and a session with no mobs is
    /// simply absent rather than an empty row.</summary>
    [Fact]
    public void MobRowsReadsWhatCheckpointWrote()
    {
        var db = Path.Combine(Path.GetTempPath(), $"eqbuddy-mobrows-{Guid.NewGuid():N}.db");
        try
        {
            using var repo = new SessionRepository(db);
            repo.Checkpoint(0, new StatsSnapshot
            {
                SessionStart = new DateTime(2026, 8, 20, 19, 0, 0),
                YourKillCount = 4,
                Mobs = [Mob("Ghoul Savant", 4, loot: ("Savant's Cap", 1))],
            }, "freeport", "Dranak", "Test");
            repo.Checkpoint(0, new StatsSnapshot
            {
                SessionStart = new DateTime(2026, 8, 21, 19, 0, 0),
                YourKillCount = 0,
                Elapsed = TimeSpan.FromMinutes(15),
            }, "freeport", "Dranak", "Test");

            var rows = repo.MobRows();

            var row = Assert.Single(rows);   // the mobless session is not a row
            Assert.Equal("Dranak", row.Character);
            var mob = Assert.Single(row.Mobs);
            Assert.Equal("Ghoul Savant", mob.Name);
            Assert.Equal(4, mob.Kills);
            Assert.Equal("Savant's Cap", Assert.Single(mob.Loot).Item);
        }
        finally
        {
            try { File.Delete(db); } catch { /* temp cleanup */ }
        }
    }
}
