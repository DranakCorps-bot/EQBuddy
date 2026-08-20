using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The per-zone spawn-point archive (David's map brief, 2026-08-13): kills near a
/// fresh /loc cluster into points, archives persist and replay idempotently, and
/// projections come from the zone's own clock — plus the ZoneShare strings that
/// carry a zone's knowledge to another player, deviation gate included.
/// </summary>
public class SpawnPointLedgerTests
{
    private static readonly DateTime T0 = new(2026, 7, 18, 15, 0, 0);

    private static SpawnCatalog TestCatalog() => new()
    {
        Zones =
        [
            new SpawnZone
            {
                Zone = "Lower Guk",
                LogZoneName = "The Ruins of Old Guk",
                NamedDefaultSeconds = 1680,
                Named =
                [
                    new SpawnEntry { Name = "a froglok ghoul lord", RespawnSeconds = 1620 },
                    new SpawnEntry { Name = "the ghoul arch magi", Placeholder = "kor ghoul wizard" },
                ],
            },
            new SpawnZone
            {
                Zone = "Permafrost Keep",
                Named = [new SpawnEntry { Name = "Lady Vox", RespawnSeconds = 604800 }],
            },
        ],
    };

    private static SpawnPointLedger Ledger(string? dir = null) => new(dir, TestCatalog());

    // ---- observation: a point exists only where a /loc anchored a kill ----

    [Fact]
    public void KillNearAFreshLocBecomesASpawnPoint()
    {
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok ghoul lord", "You"));

        var archive = l.Snapshot("Lower Guk");
        var p = Assert.Single(archive.Points);
        Assert.Equal(-500, p.LocY);
        Assert.Equal(120, p.LocX);
        var (name, seen) = p.LastKilled();
        // Stored normalized, the same shape kill lines arrive in ("Froglok ghoul lord").
        Assert.True(SpawnCatalog.NameMatches("a froglok ghoul lord", name));
        Assert.Equal(1, seen.Kills);
    }

    [Fact]
    public void NoLocOrStaleLocRecordsNothing()
    {
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok ghoul lord", "You"));
        Assert.Empty(l.Snapshot("Lower Guk").Points);

        l.Apply(new LocationEvent(T0.AddMinutes(3), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(7), "a froglok ghoul lord", "You"));   // > 3-min window
        Assert.Empty(l.Snapshot("Lower Guk").Points);
    }

    [Fact]
    public void ZoningClearsTheLastLoc()
    {
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new LocationEvent(T0.AddSeconds(10), -500, 120, 3));
        l.Apply(new ZoneEvent(T0.AddSeconds(20), "Innothule Swamp"));
        l.Apply(new KillEvent(T0.AddSeconds(30), "a gnoll", "You"));
        // The old zone's /loc must not pin a kill in the new zone.
        Assert.Empty(l.Snapshot("Innothule Swamp").Points);
        Assert.Empty(l.Snapshot("Lower Guk").Points);
    }

    // ---- clustering ----

    [Fact]
    public void NearbyKillsClusterAndRefineTheCentroid()
    {
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok ghoul lord", "You"));
        l.Apply(new LocationEvent(T0.AddMinutes(3), -520, 120, 3));   // 20 units away
        l.Apply(new KillEvent(T0.AddMinutes(4), "kor ghoul wizard", "You"));

        var archive = l.Snapshot("Lower Guk");
        var p = Assert.Single(archive.Points);
        Assert.Equal(-510, p.LocY);   // centroid moved halfway toward the second obs
        Assert.Equal(2, p.Mobs.Count);
        Assert.Equal(2, p.TotalKills());
    }

    [Fact]
    public void DistantKillStartsANewPoint()
    {
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok ghoul lord", "You"));
        l.Apply(new LocationEvent(T0.AddMinutes(3), -600, 300, 3));
        l.Apply(new KillEvent(T0.AddMinutes(4), "kor ghoul wizard", "You"));
        Assert.Equal(2, l.Snapshot("Lower Guk").Points.Count);
    }

    // ---- persistence + replay ----

    [Fact]
    public void ArchivePersistsAndReplayIsIdempotent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eqbuddy-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var l = Ledger(dir);
            l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
            l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
            l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok ghoul lord", "You"));

            // A restart replays the same log history into a fresh ledger.
            var l2 = Ledger(dir);
            l2.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
            l2.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
            l2.Apply(new KillEvent(T0.AddMinutes(2), "a froglok ghoul lord", "You"));
            var p = Assert.Single(l2.Snapshot("Lower Guk").Points);
            Assert.Equal(1, p.TotalKills());   // high-water mark: not double-counted

            // Genuinely new history still lands.
            l2.Apply(new LocationEvent(T0.AddMinutes(30), -500, 120, 3));
            l2.Apply(new KillEvent(T0.AddMinutes(31), "a froglok ghoul lord", "You"));
            Assert.Equal(2, l2.Snapshot("Lower Guk").Points.Single().TotalKills());
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void SameSecondKillsBothLand()
    {
        // Log stamps have 1-second resolution; an AoE finishing two mobs in one
        // second must not lose the second (review 2026-08-13 — the high-water mark
        // was firing on live data).
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok guard", "You"));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok scryer", "You"));   // same second
        Assert.Equal(2, l.Snapshot("Lower Guk").Points.Single().TotalKills());
    }

    [Fact]
    public void SameSecondKillsReplayIdempotently()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eqbuddy-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            void ReplayAll(SpawnPointLedger l)
            {
                l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
                l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
                l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok guard", "You"));
                l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok scryer", "You"));
            }
            var l1 = Ledger(dir);
            ReplayAll(l1);
            l1.Flush();
            var l2 = Ledger(dir);
            ReplayAll(l2);   // restart replays the same history
            Assert.Equal(2, l2.Snapshot("Lower Guk").Points.Single().TotalKills());
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void ReplayRestartingInTheSameProcessStaysIdempotent()
    {
        // Audit finding 3: a fresh instance replays clean (the tests above), but the
        // LIVE ledger replays through the SAME instance every time LogWatcher.Select
        // restarts the pipeline — auto-follow away and back, review enter/exit — and
        // the in-process boundary counter never reset, so kills at the HighWater
        // second counted past the persisted HighWaterCount and re-archived on every
        // pass. ReplayStarting (called by Select) is that reset.
        void ReplayAll(SpawnPointLedger l)
        {
            l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
            l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
            l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok guard", "You"));
            l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok scryer", "You"));   // boundary second
        }
        var l1 = Ledger();
        ReplayAll(l1);
        l1.ReplayStarting();
        ReplayAll(l1);   // same instance, same history
        Assert.Equal(2, l1.Snapshot("Lower Guk").Points.Single().TotalKills());

        // And a live second kill in a NEW boundary second still lands after all that.
        l1.Apply(new LocationEvent(T0.AddMinutes(30), -500, 120, 3));
        l1.Apply(new KillEvent(T0.AddMinutes(31), "a froglok guard", "You"));
        Assert.Equal(3, l1.Snapshot("Lower Guk").Points.Single().TotalKills());
    }

    [Fact]
    public void KillsAttachToTheNearestPointNotTheFirst()
    {
        // Two camps 50 units apart; a kill 8 units from the newer camp must refine
        // THAT one even though the older camp (28 units away) is also in radius.
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok guard", "You"));
        l.Apply(new LocationEvent(T0.AddMinutes(3), -550, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(4), "a froglok scryer", "You"));
        l.Apply(new LocationEvent(T0.AddMinutes(5), -528, 120, 3));   // 28 from A, 22 from B — both in radius, B nearer
        l.Apply(new KillEvent(T0.AddMinutes(6), "a froglok scryer", "You"));

        var archive = l.Snapshot("Lower Guk");
        Assert.Equal(2, archive.Points.Count);
        var b = archive.Points.Single(p => p.Mobs.ContainsKey("froglok scryer") && p.TotalKills() == 2);
        Assert.True(b.LocY < -530);   // refined around the -550 camp, not dragged to -500's
    }

    // ---- named + projection ----

    [Fact]
    public void NamedPointWearsTheAccentOrdinaryDoesNot()
    {
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(2), "froglok ghoul lord", "You"));   // article-folded
        l.Apply(new LocationEvent(T0.AddMinutes(3), -600, 300, 3));
        l.Apply(new KillEvent(T0.AddMinutes(4), "a froglok guard", "You"));

        var archive = l.Snapshot("Lower Guk");
        var named = archive.Points.Single(p => p.Mobs.ContainsKey("froglok ghoul lord"));
        var trash = archive.Points.Single(p => p.Mobs.ContainsKey("froglok guard"));
        Assert.True(l.IsNamedPoint("Lower Guk", named));
        Assert.False(l.IsNamedPoint("Lower Guk", trash));
    }

    [Fact]
    public void ProjectedRespawnUsesTheZoneClockOrStaysHonestlyUnknown()
    {
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok guard", "You"));
        var p = l.Snapshot("Lower Guk").Points.Single();
        Assert.Equal(T0.AddMinutes(2).AddSeconds(1680), l.ProjectedRespawn("Lower Guk", p));

        var l2 = Ledger();
        l2.Apply(new ZoneEvent(T0, "Permafrost Keep"));
        l2.Apply(new LocationEvent(T0.AddMinutes(1), 100, 100, 0));
        l2.Apply(new KillEvent(T0.AddMinutes(2), "a goblin", "You"));
        var p2 = l2.Snapshot("Permafrost Keep").Points.Single();
        Assert.Null(l2.ProjectedRespawn("Permafrost Keep", p2));   // no zone clock
    }

    // ---- right-click removal (David, 2026-08-13) ----

    [Fact]
    public void RemovePointDeletesTheNearestAndSurvivesReplay()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eqbuddy-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            void ReplayAll(SpawnPointLedger l)
            {
                l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
                l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
                l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok guard", "You"));
                l.Apply(new LocationEvent(T0.AddMinutes(3), -600, 300, 3));
                l.Apply(new KillEvent(T0.AddMinutes(4), "a froglok scryer", "You"));
            }
            var l1 = Ledger(dir);
            ReplayAll(l1);
            var before = l1.Revision;
            Assert.True(l1.RemovePoint("Lower Guk", -600, 300));
            Assert.True(l1.Revision > before);   // the map's rebuild signal
            var p = Assert.Single(l1.Snapshot("Lower Guk").Points);
            Assert.Equal(-500, p.LocY);

            // A restart replays the same history: the removed kills sit behind the
            // high-water mark, so the point stays gone.
            var l2 = Ledger(dir);
            ReplayAll(l2);
            Assert.Single(l2.Snapshot("Lower Guk").Points);

            // But a FRESH kill near the spot honestly re-learns it.
            l2.Apply(new LocationEvent(T0.AddMinutes(30), -600, 300, 3));
            l2.Apply(new KillEvent(T0.AddMinutes(31), "a froglok scryer", "You"));
            Assert.Equal(2, l2.Snapshot("Lower Guk").Points.Count);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void ConfirmedPointsHoldTheirSpotAndPersist()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eqbuddy-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var l = Ledger(dir);
            l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
            l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
            l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok guard", "You"));
            Assert.True(l.ConfirmPoint("Lower Guk", -500, 120, confirmed: true));

            // A confirmed centroid stops drifting; kills still count.
            l.Apply(new LocationEvent(T0.AddMinutes(3), -520, 120, 3));
            l.Apply(new KillEvent(T0.AddMinutes(4), "a froglok guard", "You"));
            var p = Assert.Single(l.Snapshot("Lower Guk").Points);
            Assert.Equal(-500, p.LocY);          // held, not the -510 average
            Assert.Equal(2, p.TotalKills());     // but the kill landed
            Assert.True(p.Confirmed);

            // Survives a restart, and un-confirm resumes refinement.
            var l2 = Ledger(dir);
            Assert.True(l2.Snapshot("Lower Guk").Points.Single().Confirmed);
            Assert.False(l2.ConfirmPoint("Lower Guk", -500, 120, confirmed: false));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void ConfirmationTravelsInShareStringsAddOnly()
    {
        var a = new SpawnPointLedger.ZoneArchive { Zone = "Befallen" };
        a.Points.Add(new SpawnPointLedger.SpawnPoint
        {
            LocY = -100, LocX = 50, Confirmed = true,
            Mobs = { ["an elf skeleton"] = new SpawnPointLedger.MobSeen { Kills = 2, LastKill = T0 } },
        });
        var s = ZoneShare.Export(a, null, new SpawnOverrides());
        var local = new SpawnPointLedger.ZoneArchive { Zone = "Befallen" };
        local.Points.Add(new SpawnPointLedger.SpawnPoint
        {
            LocY = -105, LocX = 52,   // same cluster, unconfirmed locally
            Mobs = { ["an elf skeleton"] = new SpawnPointLedger.MobSeen { Kills = 1, LastKill = T0 } },
        });
        var preview = ZoneShare.PreviewImport(s, local, null, new SpawnOverrides())!;
        ZoneShare.Apply(preview, local, null, new SpawnOverrides(), includeFlagged: false);
        Assert.True(local.Points.Single().Confirmed);
    }

    [Fact]
    public void ClearZoneResetsTheArchiveDurably()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eqbuddy-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            void ReplayAll(SpawnPointLedger l)
            {
                l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
                l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
                l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok guard", "You"));
                l.Apply(new LocationEvent(T0.AddMinutes(3), -600, 300, 3));
                l.Apply(new KillEvent(T0.AddMinutes(4), "a froglok scryer", "You"));
            }
            var l1 = Ledger(dir);
            ReplayAll(l1);
            Assert.Equal(2, l1.ClearZone("Lower Guk"));
            Assert.Empty(l1.Snapshot("Lower Guk").Points);
            Assert.Equal(0, l1.ClearZone("Lower Guk"));   // idempotent, honest count

            // Replay after restart resurrects nothing; a fresh kill starts over.
            var l2 = Ledger(dir);
            ReplayAll(l2);
            Assert.Empty(l2.Snapshot("Lower Guk").Points);
            l2.Apply(new LocationEvent(T0.AddMinutes(30), -500, 120, 3));
            l2.Apply(new KillEvent(T0.AddMinutes(31), "a froglok guard", "You"));
            Assert.Single(l2.Snapshot("Lower Guk").Points);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void RemovePointMissesWhenNothingIsNear()
    {
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok guard", "You"));
        Assert.False(l.RemovePoint("Lower Guk", 900, 900));   // nowhere near anything
        Assert.Single(l.Snapshot("Lower Guk").Points);
    }

    // ---- pet folding (David, 2026-08-13: pets roll into their owner names) ----

    [Fact]
    public void PetKillsFoldIntoTheOwnersName()
    {
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok guard pet", "You"));
        l.Apply(new KillEvent(T0.AddMinutes(3), "a froglok guard", "You"));

        var p = Assert.Single(l.Snapshot("Lower Guk").Points);
        var mob = Assert.Single(p.Mobs);
        Assert.True(SpawnCatalog.NameMatches("froglok guard", mob.Key));
        Assert.Equal(2, mob.Value.Kills);
    }

    [Fact]
    public void PreFoldArchivesMigrateOnLoad()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eqbuddy-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            // An archive written before the fold existed: pet and owner as separate mobs.
            File.WriteAllText(Path.Combine(dir, "lower_guk.json"),
                """{"Zone":"Lower Guk","HighWater":"2026-07-18T15:02:00","Points":[{"LocY":-500,"LocX":120,"Mobs":{"Froglok guard pet":{"Kills":2,"LastKill":"2026-07-18T15:02:00"},"Froglok guard":{"Kills":1,"LastKill":"2026-07-18T15:01:00"}}}]}""");
            var l = Ledger(dir);
            var p = Assert.Single(l.Snapshot("Lower Guk").Points);
            var mob = Assert.Single(p.Mobs);
            Assert.Equal(3, mob.Value.Kills);
            Assert.Equal(new DateTime(2026, 7, 18, 15, 2, 0), mob.Value.LastKill);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void InstanceZoneNamesResolveToTheCatalogZone()
    {
        var l = Ledger();
        l.Apply(new ZoneEvent(T0, "The Ruins of Old Guk 4 (Refined)"));
        l.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        l.Apply(new KillEvent(T0.AddMinutes(2), "a froglok ghoul lord", "You"));
        Assert.Single(l.Snapshot("Lower Guk").Points);
    }
}

public class ZoneShareTests
{
    private static readonly DateTime T0 = new(2026, 7, 18, 15, 0, 0);

    private static SpawnZone Befallen() => new()
    {
        Zone = "Befallen",
        NamedDefaultSeconds = 270,   // David's verification example: ~4:30
        Named = [new SpawnEntry { Name = "Marnek the Sage", RespawnSeconds = 270 }],
    };

    private static SpawnPointLedger.ZoneArchive Archive(params (double Y, double X, string Mob, int Kills)[] points)
    {
        var a = new SpawnPointLedger.ZoneArchive { Zone = "Befallen" };
        foreach (var (y, x, mob, kills) in points)
            a.Points.Add(new SpawnPointLedger.SpawnPoint
            {
                LocY = y, LocX = x,
                Mobs = { [mob] = new SpawnPointLedger.MobSeen { Kills = kills, LastKill = T0 } },
            });
        return a;
    }

    /// <summary>An imported timer is recorded as SOMEONE ELSE'S number, and this
    /// player's own kills replace it and stop claiming to be theirs.
    ///
    /// Both an import and a re-kill gap set Learned, and until 2026-08-20 that was all
    /// either of them recorded — so the moment an import landed there was no way to tell
    /// a stranger's number from what you measured here. The Spawns window said as much
    /// out loud, because it could not do better: "learned automatically (your kills or an
    /// import)". The two do not rank the same, and now they do not look the same.</summary>
    [Fact]
    public void AnImportIsRecordedAsSomeoneElsesNumberUntilYourOwnKillsReplaceIt()
    {
        var zone = Befallen();
        var sharer = new SpawnOverrides();
        sharer.GetOrAdd("Befallen", "Marnek the Sage").RespawnSeconds = 300;

        var mine = new SpawnOverrides();
        var wire = ZoneShare.Export(new SpawnPointLedger.ZoneArchive { Zone = "Befallen" }, zone, sharer);
        var preview = ZoneShare.PreviewImport(wire, new SpawnPointLedger.ZoneArchive { Zone = "Befallen" },
            zone, mine)!;
        ZoneShare.Apply(preview, new SpawnPointLedger.ZoneArchive { Zone = "Befallen" },
            zone, mine, includeFlagged: true);

        var o = mine.Find("Befallen", "Marnek the Sage")!;
        Assert.Equal(300, o.RespawnSeconds);
        Assert.True(o.Learned);
        Assert.True(o.Imported);      // ...and now we can say whose it is

        // Camp it yourself, measure it shorter, and it becomes yours.
        var catalog = new SpawnCatalog { Zones = [zone] };
        var t = new SpawnTimers(catalog, mine) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "Befallen"));
        t.Apply(new KillEvent(T0, "Marnek the Sage", "You"));
        t.Apply(new KillEvent(T0.AddSeconds(240), "Marnek the Sage", "You"));

        var after = mine.Find("Befallen", "Marnek the Sage")!;
        Assert.Equal(240, after.RespawnSeconds);
        Assert.True(after.Learned);
        Assert.False(after.Imported);
    }

    /// <summary>An import cannot smuggle a stale Sighted flag onto a stranger's number.
    /// Sighted exempts a value from the self-heal that purges re-kill noise — it means
    /// "I watched this mob act before its clock ran out", which is never true of a
    /// number that arrived over the wire.</summary>
    [Fact]
    public void AnImportClearsTheSightedFlagItIsOverwriting()
    {
        var zone = Befallen();
        var sharer = new SpawnOverrides();
        sharer.GetOrAdd("Befallen", "Marnek the Sage").RespawnSeconds = 300;

        var mine = new SpawnOverrides();
        var was = mine.GetOrAdd("Befallen", "Marnek the Sage");
        was.RespawnSeconds = 280;
        was.Learned = true;
        was.Sighted = true;

        var wire = ZoneShare.Export(new SpawnPointLedger.ZoneArchive { Zone = "Befallen" }, zone, sharer);
        var preview = ZoneShare.PreviewImport(wire, new SpawnPointLedger.ZoneArchive { Zone = "Befallen" },
            zone, mine)!;
        ZoneShare.Apply(preview, new SpawnPointLedger.ZoneArchive { Zone = "Befallen" },
            zone, mine, includeFlagged: true);

        var o = mine.Find("Befallen", "Marnek the Sage")!;
        Assert.Equal(300, o.RespawnSeconds);
        Assert.True(o.Imported);
        Assert.False(o.Sighted);
    }

    /// <summary>And an import still never touches a typed duration — unchanged, and the
    /// one rule in this file that is not allowed to move.</summary>
    [Fact]
    public void AnImportNeverOverwritesATypedDuration()
    {
        var zone = Befallen();
        var sharer = new SpawnOverrides();
        sharer.GetOrAdd("Befallen", "Marnek the Sage").RespawnSeconds = 300;

        var mine = new SpawnOverrides();
        var typed = mine.GetOrAdd("Befallen", "Marnek the Sage");
        typed.RespawnSeconds = 195;
        typed.Learned = false;        // the player typed it

        var wire = ZoneShare.Export(new SpawnPointLedger.ZoneArchive { Zone = "Befallen" }, zone, sharer);
        var preview = ZoneShare.PreviewImport(wire, new SpawnPointLedger.ZoneArchive { Zone = "Befallen" },
            zone, mine)!;
        ZoneShare.Apply(preview, new SpawnPointLedger.ZoneArchive { Zone = "Befallen" },
            zone, mine, includeFlagged: true);

        var o = mine.Find("Befallen", "Marnek the Sage")!;
        Assert.Equal(195, o.RespawnSeconds);
        Assert.False(o.Learned);
        Assert.False(o.Imported);
    }

    [Fact]
    public void RoundTripCarriesPointsAndTimersLearnedAndManualAlike()
    {
        // David (2026-08-13): "things I set can be shared" — manual edits travel too.
        // The importer's protections are the deviation gate and never overwriting
        // their OWN manual edits, not filtering what a sharer may offer.
        var overrides = new SpawnOverrides();
        var learned = overrides.GetOrAdd("Befallen", "Marnek the Sage");
        learned.RespawnSeconds = 275;
        learned.Learned = true;

        var s = ZoneShare.Export(Archive((-100, 50, "Marnek the Sage", 3)), Befallen(), overrides);
        Assert.StartsWith(ZoneShare.Prefix, s);

        var preview = ZoneShare.PreviewImport(s, new SpawnPointLedger.ZoneArchive { Zone = "Befallen" },
            Befallen(), new SpawnOverrides());
        Assert.NotNull(preview);
        Assert.Equal(1, preview!.NewPoints);
        Assert.Equal(3, preview.NewObservations);
        var diff = Assert.Single(preview.Timers);
        Assert.Equal("Marnek the Sage", diff.Name);
        Assert.Equal(275, diff.IncomingSeconds);
        Assert.False(diff.Flagged);   // 275 vs 270 is well inside the gate

        // Flip the same timer to a MANUAL edit — it still travels.
        learned.Learned = false;
        var manualString = ZoneShare.Export(Archive(), Befallen(), overrides);
        var manualPreview = ZoneShare.PreviewImport(manualString,
            new SpawnPointLedger.ZoneArchive { Zone = "Befallen" }, Befallen(), new SpawnOverrides());
        Assert.Equal(275, Assert.Single(manualPreview!.Timers).IncomingSeconds);
    }

    [Fact]
    public void GarbageStringsPreviewAsNull()
    {
        var local = new SpawnPointLedger.ZoneArchive { Zone = "Befallen" };
        Assert.Null(ZoneShare.PreviewImport("not a share string", local, Befallen(), new SpawnOverrides()));
        Assert.Null(ZoneShare.PreviewImport(ZoneShare.Prefix + "!!!corrupt!!!", local, Befallen(), new SpawnOverrides()));
    }

    [Fact]
    public void DecodeDropsMoblessPointsAndFoldsPetNames()
    {
        // A mob-less point would crash LastKilled() on the map tick; a sharer on an
        // older build may still carry "X pet" entries. Both sanitize at decode.
        var a = Archive((-100, 50, "an elf skeleton", 2));
        a.Points.Add(new SpawnPointLedger.SpawnPoint { LocY = -300, LocX = 90 });   // no mobs
        a.Points[0].Mobs["an elf skeleton pet"] =
            new SpawnPointLedger.MobSeen { Kills = 3, LastKill = T0.AddMinutes(1) };
        var s = ZoneShare.Export(a, Befallen(), new SpawnOverrides());

        var payload = ZoneShare.TryDecode(s);
        Assert.NotNull(payload);
        var p = Assert.Single(payload!.Points);   // mob-less point dropped
        var mob = Assert.Single(p.Mobs);          // pet folded into owner
        Assert.Equal(5, mob.Value.Kills);
        Assert.Equal(T0.AddMinutes(1), mob.Value.LastKill);
    }

    [Fact]
    public void OversizedPayloadsAreRejected()
    {
        // A point-count flood is not a real zone archive (deflate-bomb guard's
        // structural sibling; the byte cap is exercised by the same TryDecode path).
        var big = new SpawnPointLedger.ZoneArchive { Zone = "Befallen" };
        for (var i = 0; i < ZoneShare.MaxPoints + 1; i++)
            big.Points.Add(new SpawnPointLedger.SpawnPoint
            {
                LocY = i * 100, LocX = 0,
                Mobs = { ["mob " + i] = new SpawnPointLedger.MobSeen { Kills = 1, LastKill = T0 } },
            });
        var s = ZoneShare.Export(big, null, new SpawnOverrides());
        Assert.Null(ZoneShare.TryDecode(s));
    }

    [Fact]
    public void CustomNamedTimersTravelAndArriveCustom()
    {
        // David's own added named ("things I set can be shared") — not in the
        // catalog, so without the Custom flag the importer's timers would never run.
        var sharer = new SpawnOverrides();
        var custom = sharer.GetOrAdd("Befallen", "Bonesnapper");
        custom.RespawnSeconds = 275;
        custom.Custom = true;
        var s = ZoneShare.Export(Archive(), Befallen(), sharer);

        var mine = new SpawnOverrides();
        var local = new SpawnPointLedger.ZoneArchive { Zone = "Befallen" };
        var preview = ZoneShare.PreviewImport(s, local, Befallen(), mine)!;
        var diff = Assert.Single(preview.Timers);
        Assert.Equal("Bonesnapper", diff.Name);
        Assert.True(diff.Flagged);   // no baseline for an unknown named — flagged

        ZoneShare.Apply(preview, local, Befallen(), mine, includeFlagged: true);
        var applied = mine.Find("Befallen", "Bonesnapper");
        Assert.Equal(275, applied?.RespawnSeconds);
        Assert.True(applied?.Custom);   // it times like any player-added named
    }

    [Fact]
    public void DeviationGateFlagsTankedTimers()
    {
        // David's Befallen test: the zone clock says ~4:30 (270s). Someone shipping
        // 600s (+122%) is flagged; 300s (+11%) sails through.
        var overrides = new SpawnOverrides();
        var o = overrides.GetOrAdd("Befallen", "Marnek the Sage");
        o.RespawnSeconds = 600;
        o.Learned = true;
        var tanked = ZoneShare.Export(Archive(), Befallen(), overrides);
        var preview = ZoneShare.PreviewImport(tanked, new SpawnPointLedger.ZoneArchive { Zone = "Befallen" },
            Befallen(), new SpawnOverrides());
        Assert.True(Assert.Single(preview!.Timers).Flagged);
        Assert.Single(preview.FlaggedTimers);

        o.RespawnSeconds = 300;
        var fine = ZoneShare.Export(Archive(), Befallen(), overrides);
        preview = ZoneShare.PreviewImport(fine, new SpawnPointLedger.ZoneArchive { Zone = "Befallen" },
            Befallen(), new SpawnOverrides());
        Assert.False(Assert.Single(preview!.Timers).Flagged);
    }

    [Fact]
    public void FlaggedTimersApplyOnlyWhenTheImporterSaysSo()
    {
        var sharer = new SpawnOverrides();
        var o = sharer.GetOrAdd("Befallen", "Marnek the Sage");
        o.RespawnSeconds = 600;
        o.Learned = true;
        var s = ZoneShare.Export(Archive(), Befallen(), sharer);

        var local = new SpawnPointLedger.ZoneArchive { Zone = "Befallen" };
        var mine = new SpawnOverrides();
        var preview = ZoneShare.PreviewImport(s, local, Befallen(), mine)!;

        ZoneShare.Apply(preview, local, Befallen(), mine, includeFlagged: false);
        Assert.Null(mine.Find("Befallen", "Marnek the Sage"));

        ZoneShare.Apply(preview, local, Befallen(), mine, includeFlagged: true);
        var applied = mine.Find("Befallen", "Marnek the Sage");
        Assert.Equal(600, applied?.RespawnSeconds);
        Assert.True(applied?.Learned);
    }

    [Fact]
    public void ImportNeverOverwritesAManualEdit()
    {
        var sharer = new SpawnOverrides();
        var o = sharer.GetOrAdd("Befallen", "Marnek the Sage");
        o.RespawnSeconds = 280;
        o.Learned = true;
        var s = ZoneShare.Export(Archive(), Befallen(), sharer);

        var mine = new SpawnOverrides();
        var my = mine.GetOrAdd("Befallen", "Marnek the Sage");
        my.RespawnSeconds = 240;   // Learned=false: I typed this myself
        var local = new SpawnPointLedger.ZoneArchive { Zone = "Befallen" };
        var preview = ZoneShare.PreviewImport(s, local, Befallen(), mine)!;
        ZoneShare.Apply(preview, local, Befallen(), mine, includeFlagged: true);

        var kept = mine.Find("Befallen", "Marnek the Sage");
        Assert.Equal(240, kept?.RespawnSeconds);
        Assert.False(kept?.Learned);
    }

    [Fact]
    public void PointMergeIsAddOnlyAndReimportAddsNothing()
    {
        var incoming = ZoneShare.Export(Archive((-100, 50, "an elf skeleton", 5)), Befallen(), new SpawnOverrides());
        var local = Archive((-110, 55, "an elf skeleton", 2));   // same cluster (≤30 units)
        var mine = new SpawnOverrides();

        var preview = ZoneShare.PreviewImport(incoming, local, Befallen(), mine)!;
        Assert.Equal(0, preview.NewPoints);
        Assert.Equal(1, preview.RefinedPoints);
        Assert.Equal(3, preview.NewObservations);   // 5 theirs − 2 mine

        ZoneShare.Apply(preview, local, Befallen(), mine, includeFlagged: false);
        Assert.Equal(5, local.Points.Single().TotalKills());   // max, not sum

        var again = ZoneShare.PreviewImport(incoming, local, Befallen(), mine)!;
        Assert.Equal(0, again.NewObservations);
        ZoneShare.Apply(again, local, Befallen(), mine, includeFlagged: false);
        Assert.Equal(5, local.Points.Single().TotalKills());   // idempotent
    }
}
