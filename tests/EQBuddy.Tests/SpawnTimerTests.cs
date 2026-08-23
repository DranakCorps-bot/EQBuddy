using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Spawn timers: the shipped catalog, kill matching (named + placeholder, zone-gated,
/// per-server), countdown lifecycle, player overrides, and the window's view model.
/// </summary>
public class SpawnTimerTests
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
                Named = [new SpawnEntry { Name = "Lady Vox", RespawnSeconds = 604800, Variance = "±8h" }],
            },
            // A zone shaped like the ones the shipped catalog actually has trouble with:
            // named listed, respawn blank, and NO NamedDefaultSeconds to fall back on.
            // 126 entries ship this way — all 38 in High Keep, 47 in Western Wastes.
            new SpawnZone
            {
                Zone = "High Keep",
                Named =
                [
                    new SpawnEntry { Name = "Princess Lenia" },
                    new SpawnEntry { Name = "Mistress Anna", Placeholder = "a guard" },
                ],
            },
        ],
    };

    private static SpawnTimers Tracker(SpawnOverrides? overrides = null, string? path = null) =>
        new(TestCatalog(), overrides ?? new SpawnOverrides(), path) { Server = "freeport" };

    // ---- camp locations (the map's named pins, 2026-08-10) ----

    [Fact]
    public void KillNearAFreshLocPinsTheCamp()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        t.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        t.Apply(new KillEvent(T0.AddMinutes(2), "a froglok ghoul lord", "You"));

        var timer = Assert.Single(t.Snapshot(T0.AddMinutes(2)));
        Assert.Equal((-500.0, 120.0), (timer.CampLocY, timer.CampLocX));

        // A re-kill with NO fresh /loc keeps the learned camp; a stale /loc (an
        // hour old) never pins the wrong hillside.
        t.Apply(new KillEvent(T0.AddMinutes(30), "a froglok ghoul lord", "You"));
        timer = Assert.Single(t.Snapshot(T0.AddMinutes(30)));
        Assert.Equal(-500.0, timer.CampLocY);
    }

    [Fact]
    public void StaleOrForeignLocsNeverPin()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        t.Apply(new LocationEvent(T0.AddMinutes(1), -500, 120, 3));
        // Kill happens 20 minutes after the /loc: outside the window, no pin.
        t.Apply(new KillEvent(T0.AddMinutes(21), "a froglok ghoul lord", "You"));
        Assert.Null(Assert.Single(t.Snapshot(T0.AddMinutes(21))).CampLocY);

        // A /loc from the PREVIOUS zone dies at the border.
        var t2 = Tracker();
        t2.Apply(new ZoneEvent(T0, "Innothule Swamp"));
        t2.Apply(new LocationEvent(T0.AddMinutes(1), -1, -1, 0));
        t2.Apply(new ZoneEvent(T0.AddMinutes(2), "The Ruins of Old Guk"));
        t2.Apply(new KillEvent(T0.AddMinutes(3), "a froglok ghoul lord", "You"));
        Assert.Null(Assert.Single(t2.Snapshot(T0.AddMinutes(3))).CampLocY);
    }

    // ---- the shipped catalog ----

    [Fact]
    public void EmbeddedCatalogLoadsAndIsComprehensive()
    {
        var cat = SpawnCatalog.LoadEmbedded();
        Assert.True(cat.Zones.Count >= 100, $"only {cat.Zones.Count} zones");
        Assert.True(cat.Zones.Sum(z => z.Named.Count) >= 800, "named entries went missing");
        // Every zone parses; no entry has a negative or absurd timer (8 days is the
        // ceiling anything documented reaches).
        foreach (var z in cat.Zones)
        foreach (var n in z.Named)
            if (n.RespawnSeconds is { } s)
                Assert.InRange(s, 30, 8 * 86400);
    }

    [Fact]
    public void FindZoneShrugsOffArticlesAndLogNames()
    {
        var cat = SpawnCatalog.LoadEmbedded();
        Assert.NotNull(cat.FindZone("Estate of Unrest"));
        Assert.NotNull(cat.FindZone("The Estate of Unrest"));
        Assert.NotNull(cat.FindZone("Lower Guk"));
    }

    /// <summary>EQ Legends runs difficulty-tier instances of a zone — the log says
    /// "Befallen 1 (Awakened)" or "Befallen 4 (Refined)" (both observed in
    /// eqlog_Hugzee). They resolve to the base zone so Follow and kill matching keep
    /// working there.</summary>
    [Theory]
    [InlineData("Befallen 1 (Awakened)", "Befallen")]
    [InlineData("Befallen 4 (Refined)", "Befallen")]
    [InlineData("Befallen 2", "Befallen")]
    public void DifficultyTierZonesResolveToTheirBase(string logZone, string expected)
    {
        var cat = SpawnCatalog.LoadEmbedded();
        Assert.Equal(expected, cat.FindZone(logZone)?.Zone);
    }

    /// <summary>The map's named panel filters timers by CurrentZone.Zone — this is
    /// the invariant it leans on: a kill inside any instance of a zone stores its
    /// timer under exactly that catalog zone, so hopping to another instance of the
    /// same zone keeps every pin (David's field test, 2026-08-10: "Befallen 4
    /// (Refined)" showed an empty panel over timers stored under "Befallen").</summary>
    [Fact]
    public void TimersInAnyInstanceLiveUnderTheCatalogZoneTheFollowedZoneResolvesTo()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk 2 (Adaptive)"));
        t.Apply(new KillEvent(T0.AddMinutes(1), "a froglok ghoul lord", "You"));

        var timer = Assert.Single(t.Snapshot(T0.AddMinutes(1)));
        Assert.Equal("Lower Guk", timer.Zone);
        Assert.Equal(timer.Zone, t.CurrentZone?.Zone);

        // A different instance of the same zone resolves to the same catalog zone.
        t.Apply(new ZoneEvent(T0.AddMinutes(5), "The Ruins of Old Guk 4 (Refined)"));
        Assert.Equal(timer.Zone, t.CurrentZone?.Zone);
    }

    [Theory]
    [InlineData("Befallen 1 (Awakened)", "Befallen")]
    [InlineData("Clan Crushbone 2 (Adaptive)", "Clan Crushbone")]
    [InlineData("Befallen", "Befallen")]
    [InlineData("Solusek's Eye", "Solusek's Eye")]   // no tier suffix — unchanged
    public void TierVariantStrippingIsConservative(string input, string expected) =>
        Assert.Equal(expected, SpawnCatalog.StripTierVariant(input));

    [Theory]
    [InlineData("a froglok ghoul lord", "froglok ghoul lord", true)]   // article
    [InlineData("orc centurions", "orc centurion", true)]              // plural note
    [InlineData("Lady Vox", "lady vox", true)]                         // case
    [InlineData("a froglok ghoul lord", "froglok ghoul", false)]       // prefix is not a match
    [InlineData("Skeleton Lrodd", "Skeleton L`rodd", true)]            // wikis drop the EQ backtick
    [InlineData("Asaka LRei", "Asaka L`Rei", true)]
    [InlineData("", "anything", false)]
    public void NameMatchingIsForgivingButNotFuzzy(string catalogName, string killed, bool expected) =>
        Assert.Equal(expected, SpawnCatalog.NameMatches(catalogName, killed));

    /// <summary>Fuzzy matching absorbs wiki typos (the Velious page spells Keljemor
    /// "Leljemor") but stays bounded: short names never fuzz, and unrelated names
    /// never collide.</summary>
    [Theory]
    [InlineData("Leljemor", "Keljemor", true)]          // one-letter wiki typo
    [InlineData("Kriegara", "Krigara", true)]           // dropped letter
    [InlineData("Red V", "Red X", false)]               // short names: exact only
    [InlineData("Emperor Crush", "Ambassador D`Vinn", false)]
    [InlineData("Gynok Moltor", "Gynok Molto", true)]   // truncated log capture
    // Rank-ladder siblings inflect the word's END — one substitution apart, but a
    // different creature. Trainee kills were restarting the Trainer clock (David,
    // live in Crushbone 2026-08-09).
    [InlineData("Orc Trainer", "orc trainee", false)]
    // Serial families change the last word's LENGTH too, dodging the same-length
    // rule: Sol A's trash clockworks sat two edits from the named CWG Model EXG, so
    // ordinary kills ran his clock (2026-08-16). A shared prefix with a different
    // last word is a sibling unless one last word truncates the other (Gynok Molto).
    [InlineData("CWG Model EXG", "CWG Model XB", false)]
    [InlineData("CWG Model EXG", "CWG Model XA", false)]
    [InlineData("CWG Model EXG", "CWG Model XC", false)]
    public void FuzzyMatchingToleratesTyposWithoutInventingThem(string a, string b, bool expected) =>
        Assert.Equal(expected, SpawnCatalog.NameMatchesFuzzy(a, b));

    [Fact]
    public void ExactCatalogEntriesAlwaysBeatFuzzyOnes()
    {
        var catalog = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Testzone", NamedDefaultSeconds = 600,
                    Named =
                    [
                        // The typo'd entry sits FIRST — order must not decide.
                        new SpawnEntry { Name = "Gynok Molto" },
                        new SpawnEntry { Name = "Gynok Moltor" },
                    ],
                },
            ],
        };
        var t = new SpawnTimers(catalog, new SpawnOverrides()) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "Testzone"));
        t.Apply(new KillEvent(T0, "Gynok Moltor", "You"));

        Assert.Equal("Gynok Moltor", Assert.Single(t.Snapshot(T0)).Name);
    }

    // ---- kill-driven timers ----

    [Fact]
    public void AKillInTheCurrentZoneStartsTheCountdown()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        t.Apply(new KillEvent(T0.AddMinutes(1), "froglok ghoul lord", "You"));

        var timer = Assert.Single(t.Snapshot(T0.AddMinutes(2)));
        Assert.Equal("a froglok ghoul lord", timer.Name);
        Assert.Equal(T0.AddMinutes(1).AddSeconds(1620), timer.DueAt);
    }

    [Fact]
    public void KillingThePlaceholderRunsTheSameClock()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "kor ghoul wizard", "Lizzid"));

        var timer = Assert.Single(t.Snapshot(T0.AddMinutes(1)));
        Assert.Equal("the ghoul arch magi", timer.Name);
        // No per-mob timer documented — the zone's named default carries it.
        Assert.Equal(T0.AddSeconds(1680), timer.DueAt);
    }

    [Fact]
    public void KillsMatchNothingWithoutAZoneAndNothingAcrossZones()
    {
        var t = Tracker();
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));      // no zone yet
        Assert.Empty(t.Snapshot(T0));

        t.Apply(new ZoneEvent(T0, "Permafrost Keep"));
        t.Apply(new KillEvent(T0.AddMinutes(1), "froglok ghoul lord", "You")); // wrong zone
        Assert.Empty(t.Snapshot(T0.AddMinutes(1)));
    }

    [Fact]
    public void ReplayingTheLogNeverRewindsATimer()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0.AddMinutes(5), "froglok ghoul lord", "You"));
        // Startup ingest replays the same kill, then an older one from earlier in the log.
        t.Apply(new KillEvent(T0.AddMinutes(5), "froglok ghoul lord", "You"));
        t.Apply(new KillEvent(T0.AddMinutes(2), "froglok ghoul lord", "You"));

        var timer = Assert.Single(t.Snapshot(T0.AddMinutes(6)));
        Assert.Equal(T0.AddMinutes(5), timer.KilledAt);

        // A genuinely newer kill restarts the clock.
        t.Apply(new KillEvent(T0.AddMinutes(30), "froglok ghoul lord", "You"));
        Assert.Equal(T0.AddMinutes(30), Assert.Single(t.Snapshot(T0.AddMinutes(31))).KilledAt);
    }

    // ---- sighting-based completion and learning (David camping Baron Telyx,
    // 2026-08-08: a timer 25s too long can never tighten from re-kill gaps, because
    // kill-to-kill includes the time it takes to notice and kill the spawn — but the
    // mob ACTING in the log before its chip says DUE is proof the respawn happened) ----

    [Fact]
    public void APreDueSightingCompletesTheCountdownAndLearns()
    {
        var overrides = new SpawnOverrides();
        var t = Tracker(overrides);
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));

        // 1500s into a 1620s countdown, the lord is already swinging at someone.
        t.Apply(new DamageDealtEvent(T0.AddSeconds(1500), "froglok ghoul lord", 30,
            DamageKind.Melee, "Slash", false));

        var timer = Assert.Single(t.Snapshot(T0.AddSeconds(1501)));
        Assert.True(timer.IsDue(T0.AddSeconds(1501)));
        Assert.Equal(1500, timer.DurationSeconds);
        // The observed cycle becomes the learned respawn for next time.
        var o = overrides.Find("Lower Guk", "a froglok ghoul lord");
        Assert.NotNull(o);
        Assert.True(o!.Learned);
        Assert.Equal(1500, o.RespawnSeconds);
    }

    [Fact]
    public void AConsiderLineCountsAsASighting()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        t.Apply(new ConsiderEvent(T0.AddSeconds(1400), "Froglok ghoul lord", 30));

        Assert.True(Assert.Single(t.Snapshot(T0.AddSeconds(1401))).IsDue(T0.AddSeconds(1401)));
    }

    /// <summary>Several mobs can share a catalog name (Crushbone taskmasters): a
    /// same-named stranger acting mid-window is a twin, not this camp's respawn.
    /// Only the final fifth of a countdown accepts sightings.</summary>
    [Fact]
    public void AMidWindowSightingIsATwinAndChangesNothing()
    {
        var overrides = new SpawnOverrides();
        var t = Tracker(overrides);
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        t.Apply(new DamageDealtEvent(T0.AddSeconds(600), "froglok ghoul lord", 30,
            DamageKind.Melee, "Slash", false));

        var timer = Assert.Single(t.Snapshot(T0.AddSeconds(601)));
        Assert.False(timer.IsDue(T0.AddSeconds(601)));
        Assert.Equal(1620, timer.DurationSeconds);
        Assert.Null(overrides.Find("Lower Guk", "a froglok ghoul lord"));
    }

    /// <summary>David's Baron case: a manual 295s edit over a ~270s reality. The
    /// sighting still completes THIS countdown (the mob is provably up — the chip
    /// must say so), but the player's typed value is never overwritten.</summary>
    [Fact]
    public void ASightingCompletesTheChipButNeverTouchesAManualEdit()
    {
        var overrides = new SpawnOverrides();
        overrides.GetOrAdd("Lower Guk", "a froglok ghoul lord").RespawnSeconds = 2000;
        var t = Tracker(overrides);
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        t.Apply(new DamageDealtEvent(T0.AddSeconds(1900), "froglok ghoul lord", 30,
            DamageKind.Melee, "Slash", false));

        var timer = Assert.Single(t.Snapshot(T0.AddSeconds(1901)));
        Assert.True(timer.IsDue(T0.AddSeconds(1901)));
        var o = overrides.Find("Lower Guk", "a froglok ghoul lord")!;
        Assert.Equal(2000, o.RespawnSeconds);
        Assert.False(o.Learned);
    }

    [Fact]
    public void TimersArePerServer()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));

        t.Server = "qeynos";   // character switch to another server
        Assert.Empty(t.Snapshot(T0.AddMinutes(1)));
        t.Server = "freeport";
        Assert.Single(t.Snapshot(T0.AddMinutes(1)));
    }

    [Fact]
    public void AnOverriddenDurationBeatsTheCatalog()
    {
        var overrides = new SpawnOverrides();
        overrides.GetOrAdd("Lower Guk", "a froglok ghoul lord").RespawnSeconds = 2000;
        var t = Tracker(overrides);
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));

        Assert.Equal(T0.AddSeconds(2000), Assert.Single(t.Snapshot(T0)).DueAt);
    }

    [Fact]
    public void ManualStartAndDurationEditsRederiveTheCountdown()
    {
        var t = Tracker();
        t.StartManual("Permafrost Keep", "Lady Vox", 604800, elapsed: TimeSpan.FromHours(2));

        var timer = Assert.Single(t.Snapshot(DateTime.Now));
        Assert.True(timer.DueAt < DateTime.Now.AddDays(7));

        t.SetDuration("Permafrost Keep", "Lady Vox", 3 * 86400);
        Assert.Equal(timer.KilledAt.AddDays(3), Assert.Single(t.Snapshot(DateTime.Now)).DueAt);
    }

    /// <summary>DUE shows for one minute from the first snapshot that saw it, then
    /// the timer clears itself — if nobody clicked it away, they've moved on and a
    /// stale DUE tells them nothing.</summary>
    [Fact]
    public void DueTimersShowForAMinuteThenDrop()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));    // 27 min timer

        var due = T0.AddSeconds(1620);
        Assert.Single(t.Snapshot(due.AddSeconds(1)));     // the 1 Hz tick sees it due
        Assert.Single(t.Snapshot(due.AddSeconds(30)));    // DUE, within the minute
        Assert.Empty(t.Snapshot(due.AddSeconds(62)));     // cleaned itself up
    }

    /// <summary>The gap the linger now guards against: no snapshot ran while the
    /// timer came due (laptop sleep, an OS-throttled background process). The first
    /// look after the gap still shows DUE — and keeps it a full linger from that
    /// look — instead of pruning the camp before anyone ever saw it, which also
    /// swallowed the due alert.</summary>
    [Fact]
    public void ADueTimerSurvivesATickGapUntilItHasBeenSeen()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));    // due T0+1620s

        var due = T0.AddSeconds(1620);
        var wake = due.AddMinutes(10);                    // first tick after the gap
        Assert.True(Assert.Single(t.Snapshot(wake)).IsDue(wake));
        Assert.Single(t.Snapshot(wake.AddSeconds(59)));   // a full linger from the look
        Assert.Empty(t.Snapshot(wake.AddSeconds(61)));

        // Past the revival cap the camp is ancient history: silent cleanup.
        var t2 = Tracker();
        t2.Apply(new ZoneEvent(T0, "Lower Guk"));
        t2.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        Assert.Empty(t2.Snapshot(due.AddHours(2)));
    }

    [Fact]
    public void TimersSurviveARestartThroughThePersistFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spawn-timers-{Guid.NewGuid():N}.json");
        try
        {
            var t = Tracker(path: path);
            t.Apply(new ZoneEvent(T0, "Lower Guk"));
            t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));

            var reborn = Tracker(path: path);
            var timer = Assert.Single(reborn.Snapshot(T0.AddMinutes(1)));
            Assert.Equal(T0, timer.KilledAt);
            // What started the clock survives too — learning depends on it.
            Assert.Equal("froglok ghoul lord", timer.KilledName);
        }
        finally { File.Delete(path); }
    }

    // ---- instances are copies, not the same zone later (David, 2026-08-20) ----

    /// <summary>David's own sequence: log out beside the camp, log back in at D0 where
    /// the named is up, kill it, take a private instance and run to the spawn point -
    /// where it is up again, because the instance was built that moment. Killing it
    /// there used to "measure" a twelve-minute respawn.
    ///
    /// A gap across an instance change is not a loose upper bound like a trip to the
    /// bank. It is NO bound: the mob never respawned, a different copy of it was
    /// standing there. Every instance of a zone shares one timer key, which is what let
    /// the two kills look like one cycle.</summary>
    [Fact]
    public void AKillInANewInstanceNeverMeasuresACycleAgainstTheOldOne()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk - Solo"));            // D0
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        t.Apply(new ZoneEvent(T0.AddMinutes(5), "The Ruins of Old Guk 2 (Adaptive)"));
        t.Apply(new KillEvent(T0.AddMinutes(12), "froglok ghoul lord", "You"));

        // Nothing learned - and nothing to learn FROM, because the old copy's countdown
        // went with the old copy.
        Assert.Null(overrides.Find("Lower Guk", "a froglok ghoul lord")?.RespawnSeconds);
        var timer = Assert.Single(t.Snapshot(T0.AddMinutes(13)));
        Assert.Equal(T0.AddMinutes(12), timer.KilledAt);
        Assert.Equal(1620, timer.DurationSeconds);      // the catalog's, untouched
    }

    /// <summary>Stepping out does NOT cost you your camp timer. An instance keeps its
    /// state (David, 2026-08-20), so the named goes on respawning in your D2 while you
    /// are at the bank — and since zoning back in lands you at D0 before you rejoin,
    /// a rule that dropped countdowns whenever the difficulty changed would delete the
    /// timer of anyone who stepped out for two minutes.</summary>
    [Fact]
    public void SteppingOutOfAnInstanceKeepsItsCountdownRunning()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk 2 (Adaptive)"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        t.Apply(new ZoneEvent(T0.AddMinutes(2), "Befallen"));

        var timer = Assert.Single(t.Snapshot(T0.AddMinutes(3)));
        Assert.Equal(T0.AddSeconds(1620), timer.DueAt);
    }

    /// <summary>Passing through D0 on the way back to your own instance does not let a
    /// kill there measure anything either - it is a different copy of the zone, and the
    /// player zoned to reach it.</summary>
    [Fact]
    public void AKillAtD0OnTheWayBackIsNotMeasuredAgainstYourInstance()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk 2 (Adaptive)"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        t.Apply(new ZoneEvent(T0.AddMinutes(2), "Befallen"));
        t.Apply(new ZoneEvent(T0.AddMinutes(4), "The Ruins of Old Guk - Solo"));   // D0
        t.Apply(new KillEvent(T0.AddMinutes(6), "froglok ghoul lord", "You"));

        Assert.Null(overrides.Find("Lower Guk", "a froglok ghoul lord")?.RespawnSeconds);
    }

    /// <summary>...but only that zone's. A countdown running somewhere else is no
    /// business of a Guk instance.</summary>
    [Fact]
    public void TakingAnInstanceLeavesOtherZonesCountdownsAlone()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "Permafrost Keep"));
        t.Apply(new KillEvent(T0, "Lady Vox", "You"));
        t.Apply(new ZoneEvent(T0.AddMinutes(1), "The Ruins of Old Guk 2 (Adaptive)"));

        var timer = Assert.Single(t.Snapshot(T0.AddMinutes(2)));
        Assert.Equal("Permafrost Keep", timer.Zone);
    }

    /// <summary>Zoning stops the LEARNING, never the countdown. The named goes on
    /// respawning while you are at the bank, and your own instance keeps its state while
    /// you are away, so the clock is still true - and losing a camp timer every time
    /// someone stepped out would be worse than the bug this rule exists for.</summary>
    [Fact]
    public void ZoningDuringACountdownLeavesTheCountdownAlone()
    {
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        t.Apply(new ZoneEvent(T0.AddMinutes(2), "Permafrost Keep"));

        var timer = Assert.Single(t.Snapshot(T0.AddMinutes(3)));
        Assert.Equal(T0.AddSeconds(1620), timer.DueAt);
    }

    /// <summary>An OPEN-WORLD countdown survives a trip into an instance of the same
    /// zone — the open world has one copy and that mob is still due when it is due — but
    /// a kill inside the instance must not be measured against it: the timer is
    /// legitimate and the gap to it still spans two different copies of the mob.</summary>
    [Fact]
    public void AnOpenWorldCountdownSurvivesAnInstanceButIsNeverMeasuredInsideOne()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));                  // open world
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        t.Apply(new ZoneEvent(T0.AddMinutes(2), "The Ruins of Old Guk 2 (Adaptive)"));

        Assert.Single(t.Snapshot(T0.AddMinutes(3)));     // the open world is still ticking
        t.Apply(new KillEvent(T0.AddMinutes(10), "froglok ghoul lord", "You"));
        Assert.Null(overrides.Find("Lower Guk", "a froglok ghoul lord")?.RespawnSeconds);
    }

    /// <summary>A sighting in a fresh instance still completes the chip - the creature
    /// really is up where the player is standing, and the alert is the point - but it
    /// teaches nothing, because it is a different copy from the one that died.</summary>
    [Fact]
    public void ASightingAcrossAnInstanceChangeCompletesTheChipButNeverLearns()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));               // open world, 1620s
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        // Open world, so the countdown survives into the instance (see above).
        t.Apply(new ZoneEvent(T0.AddMinutes(2), "The Ruins of Old Guk 2 (Adaptive)"));
        t.Apply(new DamageDealtEvent(T0.AddSeconds(1500), "froglok ghoul lord", 30,
            DamageKind.Melee, "Slash", false));

        Assert.True(Assert.Single(t.Snapshot(T0.AddSeconds(1501))).IsDue(T0.AddSeconds(1501)));
        Assert.Null(overrides.Find("Lower Guk", "a froglok ghoul lord")?.RespawnSeconds);
    }

    // ---- named the catalog lists with NO respawn (2026-08-20) ----

    /// <summary>The 126 shipped named whose respawn is blank in a zone with no default.
    /// They used to be worse off than a mob the catalog had never heard of: a DISCOVERED
    /// named measures its cycle on the second kill, while these could never learn one at
    /// all, because LearnFromRekill returned before it started when there was no current
    /// duration to compare a gap against. Being known was worse than being unknown.</summary>
    /// <summary>Clearing a timer has to SURVIVE the log being read again — #228
    /// (joeymavity): "respawn timers randomly re-open after they've been cleared."
    ///
    /// `Clear` genuinely removes the entry, which is why this looked mysterious. What it
    /// did not do is record that the player DISMISSED that kill. Every `LogWatcher.Select`
    /// is a full-file ingest, so every kill line in the log replays through `Apply` — and
    /// `Upsert` had nothing to consult, so it faithfully rebuilt the timer from the very
    /// kill the player had just dismissed. "Randomly" is the restart, or a character
    /// switch, or anything else that re-selects the log.
    ///
    /// Trap 20's family once more: the state was removed and the DECISION was not kept.
    ///
    /// A later kill is a different matter — that is a real new spawn cycle and it must
    /// bring the timer back, or "clear" would mean "never track this again".</summary>
    [Fact]
    public void AClearedTimerStaysClearedWhenTheLogIsReadAgain()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };

        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        t.Apply(new KillEvent(T0, "a froglok ghoul lord", "You"));
        Assert.Single(t.Snapshot(T0.AddMinutes(1)));

        t.Clear("Lower Guk", "a froglok ghoul lord");
        Assert.Empty(t.Snapshot(T0.AddMinutes(1)));

        // The replay: the same kill line, read again from the top of the file.
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        t.Apply(new KillEvent(T0, "a froglok ghoul lord", "You"));
        Assert.Empty(t.Snapshot(T0.AddMinutes(2)));

        // But killing it AGAIN is a real cycle, and that timer is wanted.
        t.Apply(new KillEvent(T0.AddMinutes(30), "a froglok ghoul lord", "You"));
        Assert.Single(t.Snapshot(T0.AddMinutes(31)));
    }

    /// <summary>And the dismissal outlives a restart, which is the case that actually
    /// bites: the replay that rebuilds a cleared timer is the one that runs at startup,
    /// so a dismissal held only in memory would be forgotten at exactly the wrong
    /// moment.</summary>
    [Fact]
    public void AClearedTimerStaysClearedAcrossARestart()
    {
        var dir = Directory.CreateTempSubdirectory("eqb-spawn-clear");
        try
        {
            var path = Path.Combine(dir.FullName, "spawn-timers.json");
            var overrides = new SpawnOverrides();

            var first = new SpawnTimers(TestCatalog(), overrides, path) { Server = "freeport" };
            first.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
            first.Apply(new KillEvent(T0, "a froglok ghoul lord", "You"));
            first.Clear("Lower Guk", "a froglok ghoul lord");

            // A new process, the same profile, and the log ingested from the top.
            var second = new SpawnTimers(TestCatalog(), overrides, path) { Server = "freeport" };
            second.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
            second.Apply(new KillEvent(T0, "a froglok ghoul lord", "You"));
            Assert.Empty(second.Snapshot(T0.AddMinutes(2)));
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void ACatalogNamedWithNoRespawnLearnsFromItsSecondKill()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "High Keep"));
        t.Apply(new KillEvent(T0, "Princess Lenia", "You"));

        // First kill: a chip that says how long ago, and honestly nothing more.
        Assert.Null(Assert.Single(t.Snapshot(T0.AddMinutes(1))).DurationSeconds);
        Assert.Null(overrides.Find("High Keep", "Princess Lenia")?.RespawnSeconds);

        t.Apply(new KillEvent(T0.AddMinutes(22), "Princess Lenia", "You"));

        var o = overrides.Find("High Keep", "Princess Lenia");
        Assert.NotNull(o);
        Assert.True(o!.Learned);
        Assert.False(o.Imported);
        Assert.Equal(1320, o.RespawnSeconds);
        Assert.Equal(1320, Assert.Single(t.Snapshot(T0.AddMinutes(23))).DurationSeconds);
    }

    /// <summary>...and it still only ever tightens, exactly like a named that came with
    /// a duration.</summary>
    [Fact]
    public void ANoRespawnNamedKeepsTighteningButNeverLoosens()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "High Keep"));
        t.Apply(new KillEvent(T0, "Princess Lenia", "You"));
        t.Apply(new KillEvent(T0.AddMinutes(22), "Princess Lenia", "You"));
        Assert.Equal(1320, overrides.Find("High Keep", "Princess Lenia")!.RespawnSeconds);

        t.Apply(new KillEvent(T0.AddMinutes(40), "Princess Lenia", "You"));   // 18m - tighter
        Assert.Equal(1080, overrides.Find("High Keep", "Princess Lenia")!.RespawnSeconds);
        t.Apply(new KillEvent(T0.AddMinutes(90), "Princess Lenia", "You"));   // 50m - you were slow
        Assert.Equal(1080, overrides.Find("High Keep", "Princess Lenia")!.RespawnSeconds);
    }

    /// <summary>A typed duration outranks the new path like it outranks every other.</summary>
    [Fact]
    public void ATypedDurationOnANoRespawnNamedIsNeverMeasuredOver()
    {
        var overrides = new SpawnOverrides();
        var vm = new EQBuddy.UI.Shared.SpawnsViewModel(TestCatalog(), overrides,
            new SpawnTimers(TestCatalog(), overrides));
        vm.SetDuration("High Keep", "Princess Lenia", "30");

        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "High Keep"));
        t.Apply(new KillEvent(T0, "Princess Lenia", "You"));
        t.Apply(new KillEvent(T0.AddMinutes(22), "Princess Lenia", "You"));

        var o = overrides.Find("High Keep", "Princess Lenia")!;
        Assert.Equal(1800, o.RespawnSeconds);
        Assert.False(o.Learned);
    }

    // ---- the same-stay rule (2026-08-20) ----

    /// <summary>With NO known duration the first accepted gap BECOMES the countdown, so
    /// nothing on screen can contradict it. "Killed it, went to Freeport, came back five
    /// hours later and killed it" is a true upper bound and a useless one, and it would
    /// print a confident five-hour timer. Both kills must fall in one continuous stay.</summary>
    [Fact]
    public void ANoRespawnNamedRefusesAGapAcrossAZoneTrip()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "High Keep"));
        t.Apply(new KillEvent(T0, "Princess Lenia", "You"));
        t.Apply(new ZoneEvent(T0.AddMinutes(5), "Lower Guk"));       // off to sell
        t.Apply(new ZoneEvent(T0.AddHours(5), "High Keep"));         // ...back at teatime
        t.Apply(new KillEvent(T0.AddHours(5).AddMinutes(1), "Princess Lenia", "You"));

        Assert.Null(overrides.Find("High Keep", "Princess Lenia")?.RespawnSeconds);
        // The clock still restarts on the kill - only the LEARNING is refused.
        var timer = Assert.Single(t.Snapshot(T0.AddHours(5).AddMinutes(2)));
        Assert.Equal(T0.AddHours(5).AddMinutes(1), timer.KilledAt);
        Assert.Null(timer.DurationSeconds);
    }

    /// <summary>A discovered named is the other half of the same hole.</summary>
    [Fact]
    public void ADiscoveredNamedRefusesAGapAcrossAZoneTrip()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        t.Apply(new KillEvent(T0, "Chief Goonda", "You") { ProperName = true });
        t.Apply(new ZoneEvent(T0.AddMinutes(5), "Permafrost Keep"));
        t.Apply(new ZoneEvent(T0.AddHours(3), "The Ruins of Old Guk"));
        t.Apply(new KillEvent(T0.AddHours(3).AddMinutes(1), "Chief Goonda", "You") { ProperName = true });

        var o = overrides.Find("Lower Guk", "Chief Goonda");
        Assert.NotNull(o);
        Assert.True(o!.Discovered);
        Assert.Null(o.RespawnSeconds);
    }

    /// <summary>The rule is the same wherever a duration is known: leave the zone during
    /// a countdown and that countdown's gap teaches nothing. It costs the honest case
    /// too - a bank trip in a zone with no instances, where the gap really is a true
    /// upper bound - and that is a deliberate, cheap trade. Such a gap has a whole errand
    /// added to it, so it is rarely the tightest bound seen and rarely the one that would
    /// have won; and refusing it costs a measurement, while accepting the instance case
    /// it cannot be told apart from costs a camp.</summary>
    [Fact]
    public void AKnownDurationRefusesAGapAcrossAZoneTrip()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));                 // catalog: 1620s
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        t.Apply(new ZoneEvent(T0.AddMinutes(2), "Permafrost Keep"));
        t.Apply(new ZoneEvent(T0.AddMinutes(15), "The Ruins of Old Guk"));
        t.Apply(new KillEvent(T0.AddMinutes(20), "froglok ghoul lord", "You"));   // 1200s < 1620s

        Assert.Null(overrides.Find("Lower Guk", "a froglok ghoul lord")?.RespawnSeconds);
        // The kill still restarts the clock on the catalog's own number.
        Assert.Equal(1620, Assert.Single(t.Snapshot(T0.AddMinutes(21))).DurationSeconds);
    }

    /// <summary>A timer recovered from the persist file carries no stay, and no evidence
    /// must never read as agreement - the same rule KilledName follows across a restart.
    /// Without this, every restart would hand the no-duration path a free anchor whose
    /// provenance nobody knows.</summary>
    [Fact]
    public void ATimerRecoveredFromDiskAnchorsNoFirstDuration()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spawn-timers-{Guid.NewGuid():N}.json");
        try
        {
            var overrides = new SpawnOverrides();
            var t = new SpawnTimers(TestCatalog(), overrides, path) { Server = "freeport" };
            t.Apply(new ZoneEvent(T0, "High Keep"));
            t.Apply(new KillEvent(T0, "Princess Lenia", "You"));

            var reborn = new SpawnTimers(TestCatalog(), overrides, path) { Server = "freeport" };
            reborn.Apply(new ZoneEvent(T0.AddMinutes(20), "High Keep"));
            reborn.Apply(new KillEvent(T0.AddMinutes(22), "Princess Lenia", "You"));

            Assert.Null(overrides.Find("High Keep", "Princess Lenia")?.RespawnSeconds);
        }
        finally { File.Delete(path); }
    }

    /// <summary>The floor and the ceiling still apply on the new path: below the floor is
    /// multi-spawn noise, above the ceiling is "you went to bed" - and thanks to the
    /// same-stay rule the ceiling now means a player who really did sit there.</summary>
    [Theory]
    [InlineData(1, false)]        // under MinLearnSeconds
    [InlineData(22, true)]
    [InlineData(60 * 7, false)]   // over MaxDiscoverSeconds
    public void TheNoRespawnPathKeepsItsFloorAndCeiling(int gapMinutes, bool learns)
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "High Keep"));
        t.Apply(new KillEvent(T0, "Princess Lenia", "You"));
        t.Apply(new KillEvent(T0.AddMinutes(gapMinutes), "Princess Lenia", "You"));

        Assert.Equal(learns, overrides.Find("High Keep", "Princess Lenia")?.RespawnSeconds is not null);
    }

    /// <summary>A placeholder death on either end is still walk time between two different
    /// mobs, not a cycle - the new path must not have reopened the door #181 closed.</summary>
    [Fact]
    public void APlaceholderDeathNeverMeasuresANoRespawnNamed()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "High Keep"));
        t.Apply(new KillEvent(T0, "a guard", "You"));                            // placeholder
        t.Apply(new KillEvent(T0.AddMinutes(22), "Mistress Anna", "You"));        // the named

        Assert.Null(overrides.Find("High Keep", "Mistress Anna")?.RespawnSeconds);
    }

    /// <summary>Timers tighten themselves from play: a re-kill sooner than the timer
    /// <summary>David's rule (2026-08-04, Orc Taskmaster running a learned 328s under
    /// Crushbone's MEASURED 738s clock): a trusted timer disables re-kill learning —
    /// a shorter gap against a measurement is multi-spawn noise (two taskmasters at
    /// different camps), not evidence of a faster respawn.</summary>
    [Fact]
    public void TrustedClocksRefuseToLearnFromRekillGaps()
    {
        var catalog = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Crushbone", NamedDefaultSeconds = 738, NamedDefaultTrusted = true,
                    Named = [new SpawnEntry { Name = "Orc Taskmaster" }],
                },
            ],
        };
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(catalog, overrides) { Server = "qeynos" };
        t.Apply(LogParser.Parse("[Tue Aug 4 19:00:00 2026] You have entered Clan Crushbone.")!);
        t.Apply(LogParser.Parse("[Tue Aug 4 19:00:10 2026] You have slain Orc Taskmaster!")!);
        // Re-kill 328s later — a second taskmaster at another camp, NOT a fast respawn.
        t.Apply(LogParser.Parse("[Tue Aug 4 19:05:38 2026] You have slain Orc Taskmaster!")!);

        Assert.Null(overrides.Find("Crushbone", "Orc Taskmaster"));   // nothing learned
        Assert.Equal(738, Assert.Single(t.Snapshot(DateTime.Parse("2026-08-04T19:05:39"))).DurationSeconds);
    }

    [Fact]
    public void AStaleLearnedOverrideUnderATrustedClockSelfHeals()
    {
        var catalog = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Crushbone", NamedDefaultSeconds = 738, NamedDefaultTrusted = true,
                    Named = [new SpawnEntry { Name = "Orc Taskmaster" }],
                },
            ],
        };
        var overrides = new SpawnOverrides();
        var stale = overrides.GetOrAdd("Crushbone", "Orc Taskmaster");
        stale.RespawnSeconds = 328;   // learned before the clock was measured
        stale.Learned = true;
        stale.Alert = true;           // the player's bell choice must survive the heal

        var t = new SpawnTimers(catalog, overrides) { Server = "qeynos" };
        t.Apply(LogParser.Parse("[Tue Aug 4 19:00:00 2026] You have entered Clan Crushbone.")!);
        t.Apply(LogParser.Parse("[Tue Aug 4 19:00:10 2026] You have slain Orc Taskmaster!")!);

        Assert.Equal(738, Assert.Single(t.Snapshot(DateTime.Parse("2026-08-04T19:00:11"))).DurationSeconds);
        var healed = overrides.Find("Crushbone", "Orc Taskmaster")!;
        Assert.Null(healed.RespawnSeconds);
        Assert.False(healed.Learned);
        Assert.True(healed.Alert);

        // A MANUAL (typed) edit is sovereign — never healed away.
        var manual = overrides.GetOrAdd("Crushbone", "Orc Taskmaster");
        manual.RespawnSeconds = 300;
        manual.Learned = false;
        t.Apply(LogParser.Parse("[Tue Aug 4 19:20:00 2026] You have slain Orc Taskmaster!")!);
        Assert.Equal(300, Assert.Single(t.Snapshot(DateTime.Parse("2026-08-04T19:20:01"))).DurationSeconds);
    }

    /// <summary>David's call (2026-08-09, fighting a trainer his chip said was five
    /// minutes away): "for actual nameds I don't want to lock the timers if we
    /// actually observe them being lower." A final-window sighting now out-measures
    /// even a TRUSTED clock — and the value it learns is marked Sighted, so the
    /// self-heal (which exists to purge re-kill noise) leaves it standing.</summary>
    [Fact]
    public void AFinalWindowSightingOutranksATrustedClockAndSurvivesTheHeal()
    {
        var catalog = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Crushbone", NamedDefaultSeconds = 738, NamedDefaultTrusted = true,
                    Named = [new SpawnEntry { Name = "Orc Trainer" }],
                },
            ],
        };
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(catalog, overrides) { Server = "qeynos" };
        t.Apply(new ZoneEvent(T0, "Clan Crushbone"));
        t.Apply(new KillEvent(T0, "Orc Trainer", "You"));

        // 620s into the trusted 738s clock (inside the final fifth), the trainer
        // is already swinging: the chip completes and the observation is learned.
        t.Apply(new DamageDealtEvent(T0.AddSeconds(620), "Orc Trainer", 12,
            DamageKind.Melee, "Slash", false));
        Assert.True(Assert.Single(t.Snapshot(T0.AddSeconds(621))).IsDue(T0.AddSeconds(621)));
        var o = overrides.Find("Crushbone", "Orc Trainer")!;
        Assert.Equal(620, o.RespawnSeconds);
        Assert.True(o.Sighted);

        // The next kill would have self-healed a re-kill-learned 620 under a trusted
        // 738 — the sighted value stays, and the new countdown runs on it.
        t.Apply(new KillEvent(T0.AddSeconds(700), "Orc Trainer", "You"));
        Assert.Equal(620, overrides.Find("Crushbone", "Orc Trainer")!.RespawnSeconds);
        Assert.Equal(620, Assert.Single(t.Snapshot(T0.AddSeconds(701))).DurationSeconds);
    }

    /// <summary>The refinement, minutes later: "it should just be for the actual
    /// named/boss mobs. Not mobs that spawn in multiple locations — Royal Guard, for
    /// example, spawns in a number of places." Multi-spawn entries get NO sighting
    /// treatment: any same-named activity may be a sibling, so their clocks are
    /// kill-driven only, even inside the final window.</summary>
    [Fact]
    public void MultiSpawnNamesIgnoreSightingsEntirely()
    {
        var catalog = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Crushbone",
                    Named = [new SpawnEntry { Name = "Royal Guard", RespawnSeconds = 480, MultiSpawn = true }],
                },
            ],
        };
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(catalog, overrides) { Server = "qeynos" };
        t.Apply(new ZoneEvent(T0, "Clan Crushbone"));
        t.Apply(new KillEvent(T0, "Royal Guard", "You"));

        // Another guard piercing you at 460s — deep in the final window — is one of
        // its siblings elsewhere, not this camp's respawn. Nothing moves.
        t.Apply(new DamageDealtEvent(T0.AddSeconds(460), "Royal Guard", 8,
            DamageKind.Melee, "Pierce", false));
        var timer = Assert.Single(t.Snapshot(T0.AddSeconds(461)));
        Assert.False(timer.IsDue(T0.AddSeconds(461)));
        Assert.Equal(480, timer.DurationSeconds);
        Assert.Null(overrides.Find("Crushbone", "Royal Guard"));

        // Re-kill gaps teach nothing either: killing a SIBLING 120s after this camp's
        // kill must not become the learned respawn (the 111s Trainer poison, David's
        // log 2026-08-09 — a trainee-restarted clock "measured" a two-minute cycle).
        t.Apply(new KillEvent(T0.AddSeconds(120), "Royal Guard", "You"));
        Assert.Null(overrides.Find("Crushbone", "Royal Guard"));
        Assert.Equal(480, Assert.Single(t.Snapshot(T0.AddSeconds(121))).DurationSeconds);
    }

    /// <summary>Poison already in the file from before multiSpawn existed (David's
    /// Trainer at 111s) heals on the next kill — including the startup replay, so an
    /// update alone fixes the chip without anyone editing overrides by hand.</summary>
    [Fact]
    public void StaleLearnedValuesOnMultiSpawnEntriesHealOnKill()
    {
        var catalog = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Crushbone",
                    Named = [new SpawnEntry { Name = "Orc Trainer", RespawnSeconds = 480, MultiSpawn = true }],
                },
            ],
        };
        var overrides = new SpawnOverrides();
        var poisoned = overrides.GetOrAdd("Crushbone", "Orc Trainer");
        poisoned.RespawnSeconds = 111;
        poisoned.Learned = true;

        var t = new SpawnTimers(catalog, overrides) { Server = "qeynos" };
        t.Apply(new ZoneEvent(T0, "Clan Crushbone"));
        t.Apply(new KillEvent(T0, "orc trainer", "You"));

        Assert.Equal(480, Assert.Single(t.Snapshot(T0.AddSeconds(1))).DurationSeconds);
        var healed = overrides.Find("Crushbone", "Orc Trainer")!;
        Assert.Null(healed.RespawnSeconds);
        Assert.False(healed.Learned);

        // A manual value on a multiSpawn entry is still sovereign.
        var manual = overrides.GetOrAdd("Crushbone", "Orc Trainer");
        manual.RespawnSeconds = 300;
        manual.Learned = false;
        t.Apply(new KillEvent(T0.AddSeconds(600), "orc trainer", "You"));
        Assert.Equal(300, Assert.Single(t.Snapshot(T0.AddSeconds(601))).DurationSeconds);
        Assert.Equal(300, overrides.Find("Crushbone", "Orc Trainer")!.RespawnSeconds);
    }

    /// <summary>Issue #36 regression net: article-bearing catalog names ("the froglok
    /// shin lord", 285 entries) must match normalized kill lines, end to end against
    /// the REAL embedded catalog — zone resolution included. When this passes but a
    /// player still reports no timer, the divergence is Legends-vs-catalog data (zone
    /// name or mob placement), not code.</summary>
    /// <summary>Legends renames MOBS too: "the ghoul lord" is "Hoptor Thaggelum"
    /// in-game (issue #38, chrstahl's verbatim lines — which also proved Lower Guk
    /// kept classic's "Old Guk" zone name). Entry aliases absorb mob renames the way
    /// zone aliases absorb zone renames.</summary>
    [Fact]
    public void ARenamedMobStartsItsClassicEntrysTimer()
    {
        var t = new SpawnTimers(SpawnCatalog.LoadEmbedded(), new SpawnOverrides()) { Server = "qeynos" };
        t.Apply(LogParser.Parse("[Tue Aug 4 17:08:21 2026] You have entered The Ruins of Old Guk 4 (Refined).")!);
        t.Apply(LogParser.Parse("[Tue Aug 4 17:16:04 2026] You have slain Hoptor Thaggelum!")!);
        Assert.Single(t.Snapshot(DateTime.Parse("2026-08-04T17:16:05")),
            s => s.Name == "the ghoul lord" && s.Zone == "Lower Guk");
    }

    /// <summary>Legends renamed Lower Guk "The Ruins of ANCIENT Guk" (classic said
    /// "Old"); a single mismatched log-zone name silently kills every timer in the
    /// zone (issue #36's likely cause). The alias list absorbs renames.</summary>
    [Theory]
    [InlineData("You have entered The Ruins of Ancient Guk.")]
    [InlineData("You have entered The Ruins of Old Guk.")]
    [InlineData("You have entered The Ruins of Ancient Guk 2 (Adaptive).")]
    public void ZoneAliasesResolveLegendsRenames(string zoneLine)
    {
        var t = new SpawnTimers(SpawnCatalog.LoadEmbedded(), new SpawnOverrides()) { Server = "qeynos" };
        t.Apply(LogParser.Parse($"[Tue Aug 4 19:00:00 2026] {zoneLine}")!);
        t.Apply(LogParser.Parse("[Tue Aug 4 19:05:00 2026] You have slain the ghoul lord!")!);
        Assert.Single(t.Snapshot(DateTime.Parse("2026-08-04T19:05:01")),
            s => s.Name == "the ghoul lord" && s.Zone == "Lower Guk");
    }

    [Theory]
    [InlineData("You have entered Guk.")]
    [InlineData("You have entered Upper Guk 3 (Fused).")]
    // chrstahl's verbatim lines from issue #36 — Legends' real name for Upper Guk is
    // "The City of Guk", which no classic source predicted. Field data beats theory.
    [InlineData("You have entered The City of Guk 4 (Refined).")]
    public void ArticleNamedMobsStartTimersAgainstTheRealCatalog(string zoneLine)
    {
        var t = new SpawnTimers(SpawnCatalog.LoadEmbedded(), new SpawnOverrides()) { Server = "qeynos" };
        t.Apply(LogParser.Parse($"[Tue Aug 4 19:00:00 2026] {zoneLine}")!);
        t.Apply(LogParser.Parse("[Tue Aug 4 19:05:00 2026] You have slain the froglok shin lord!")!);
        Assert.Single(t.Snapshot(DateTime.Parse("2026-08-04T19:05:01")),
            s => s.Name == "the froglok shin lord");
    }

    /// says is possible proves the respawn is at most that gap. Manual edits are never
    /// touched, learning never loosens, and sub-90-second gaps are multi-spawn noise.</summary>
    [Fact]
    public void RekillsSoonerThanTheTimerTightenIt()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));                 // catalog: 1620s
        t.Apply(new KillEvent(T0.AddMinutes(5), "froglok ghoul lord", "You"));   // back in 300s!

        var o = overrides.Find("Lower Guk", "a froglok ghoul lord");
        Assert.NotNull(o);
        Assert.True(o!.Learned);
        Assert.Equal(300, o.RespawnSeconds);
        Assert.Equal(T0.AddMinutes(5).AddSeconds(300), Assert.Single(t.Snapshot(T0.AddMinutes(6))).DueAt);

        // Better evidence keeps tightening…
        t.Apply(new KillEvent(T0.AddMinutes(9), "froglok ghoul lord", "You"));   // 240s gap
        Assert.Equal(240, overrides.Find("Lower Guk", "a froglok ghoul lord")!.RespawnSeconds);
        // …but a slower pair of kills never loosens what was learned.
        t.Apply(new KillEvent(T0.AddMinutes(29), "froglok ghoul lord", "You"));  // 1200s gap
        Assert.Equal(240, overrides.Find("Lower Guk", "a froglok ghoul lord")!.RespawnSeconds);
    }

    [Fact]
    public void LearningNeverOverridesAManualEditAndIgnoresNoiseGaps()
    {
        var overrides = new SpawnOverrides();
        var timers = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        var vm = new SpawnsViewModel(TestCatalog(), overrides, timers);

        vm.SetDuration("Lower Guk", "a froglok ghoul lord", "20m");   // the player's word
        timers.Apply(new ZoneEvent(T0, "Lower Guk"));
        timers.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        timers.Apply(new KillEvent(T0.AddMinutes(5), "froglok ghoul lord", "You"));
        Assert.Equal(1200, overrides.Find("Lower Guk", "a froglok ghoul lord")!.RespawnSeconds);
        Assert.False(overrides.Find("Lower Guk", "a froglok ghoul lord")!.Learned);

        // Fresh named, kills 60 s apart: multi-spawn noise, not a 60-second respawn.
        timers.Apply(new KillEvent(T0.AddMinutes(10), "kor ghoul wizard", "You"));
        timers.Apply(new KillEvent(T0.AddMinutes(11), "kor ghoul wizard", "You"));
        Assert.Null(overrides.Find("Lower Guk", "the ghoul arch magi"));
    }

    // ---- cross-mob learning poison (LW's Sol A session, 2026-08-16: trash
    // clockwork kills fuzzy-bridged onto CWG Model EXG's clock, then killing the
    // REAL EXG 93s later "learned" a 93-second respawn — walk time between two
    // different mobs, not a cycle. Learning now demands the named's own death on
    // BOTH ends of the gap.) ----

    /// <summary>End to end against the shipped catalog: the trash clockworks that
    /// actually bridged (XB/XA from the live log) start nothing, the named himself
    /// still runs the zone's 18-minute clock.</summary>
    [Fact]
    public void TrashClockworksStartNoClockButTheNamedStillDoes()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(SpawnCatalog.LoadEmbedded(), overrides) { Server = "oggok" };
        t.Apply(LogParser.Parse("[Sun Aug 16 15:17:23 2026] You have entered Solusek's Eye 2 (Adaptive).")!);
        t.Apply(LogParser.Parse("[Sun Aug 16 15:33:41 2026] You have slain CWG Model XB!")!);
        t.Apply(LogParser.Parse("[Sun Aug 16 15:34:58 2026] You have slain CWG Model XA!")!);
        Assert.Empty(t.Snapshot(DateTime.Parse("2026-08-16T15:35:00")));

        t.Apply(LogParser.Parse("[Sun Aug 16 15:36:54 2026] You have slain CWG Model EXG!")!);
        var timer = Assert.Single(t.Snapshot(DateTime.Parse("2026-08-16T15:36:55")));
        Assert.Equal("CWG Model EXG", timer.Name);
        Assert.Equal(1080, timer.DurationSeconds);
        Assert.Null(overrides.Find("Solusek's Eye", "CWG Model EXG"));
    }

    /// <summary>A placeholder death restarts the clock (that part is the feature),
    /// but the gap from a placeholder kill to the named's kill teaches nothing —
    /// only a named-to-named gap is a cycle.</summary>
    [Fact]
    public void PlaceholderToNamedGapsTeachNothingButNamedToNamedStillDo()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "kor ghoul wizard", "You"));   // PH: 1680s zone clock

        // The named was up the whole time; you reach him 200s later.
        t.Apply(new KillEvent(T0.AddSeconds(200), "the ghoul arch magi", "You"));
        Assert.Null(overrides.Find("Lower Guk", "the ghoul arch magi"));
        var timer = Assert.Single(t.Snapshot(T0.AddSeconds(201)));
        Assert.Equal(1680, timer.DurationSeconds);               // full clock, restarted
        Assert.Equal(T0.AddSeconds(200), timer.KilledAt);

        // Named again 300s after the named: that IS a measured cycle.
        t.Apply(new KillEvent(T0.AddSeconds(500), "the ghoul arch magi", "You"));
        Assert.Equal(300, overrides.Find("Lower Guk", "the ghoul arch magi")!.RespawnSeconds);
    }

    /// <summary>A final-window sighting on a placeholder-started clock still flips
    /// the chip (the mob is provably up) but learns nothing: measured from the
    /// placeholder's death, the elapsed time is not the named's cycle.</summary>
    [Fact]
    public void SightingsOnAPlaceholderStartedClockCompleteTheChipButNeverLearn()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "Lower Guk"));
        t.Apply(new KillEvent(T0, "kor ghoul wizard", "You"));   // PH: 1680s zone clock

        // 1400s in (final fifth starts at 1344), the arch magi is already casting.
        t.Apply(new DamageDealtEvent(T0.AddSeconds(1400), "the ghoul arch magi", 50,
            DamageKind.Melee, "Slash", false));
        var timer = Assert.Single(t.Snapshot(T0.AddSeconds(1401)));
        Assert.True(timer.IsDue(T0.AddSeconds(1401)));
        Assert.Null(overrides.Find("Lower Guk", "the ghoul arch magi"));
    }

    /// <summary>The ▶ button must never silently lose to a running clock: a manual
    /// start backdated by "died an hour ago" is the player's word, and it replaces
    /// a newer automatic timer instead of being swallowed by the replay guard.</summary>
    [Fact]
    public void ABackdatedManualStartReplacesARunningTimer()
    {
        var t = Tracker();
        var now = DateTime.Now;
        t.Apply(new ZoneEvent(now, "Permafrost Keep"));
        t.Apply(new KillEvent(now, "Lady Vox", "You"));          // clock just started

        t.StartManual("Permafrost Keep", "Lady Vox", 604800, elapsed: TimeSpan.FromHours(1));
        var timer = Assert.Single(t.Snapshot(now));
        Assert.True(timer.KilledAt <= now - TimeSpan.FromMinutes(59));
    }

    // ---- duration text ----

    [Theory]
    [InlineData("8.5m", 510)]       // #124 (wizen): decimal + unit must not double
    [InlineData("8.5", 510)]
    [InlineData("1.5h", 5400)]
    [InlineData("22", 1320)]        // bare number = minutes, the wiki convention
    [InlineData("90s", 90)]
    [InlineData("8m", 480)]
    [InlineData("12h", 43200)]
    [InlineData("3d", 259200)]
    [InlineData("3d 12h", 302400)]
    [InlineData("1h30m", 5400)]
    [InlineData("6:40", 400)]       // m:ss, how eqlwiki writes zone timers
    [InlineData("1:00:00", 3600)]
    public void DurationTextParses(string text, double seconds) =>
        Assert.Equal(seconds, SpawnDurationText.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("h")]
    [InlineData("1:2:3:4")]
    public void DurationTextRejectsNoise(string text) =>
        Assert.Null(SpawnDurationText.Parse(text));

    [Theory]
    [InlineData(1320, "22m")]
    [InlineData(302400, "3d 12h")]
    [InlineData(400, "6m 40s")]
    [InlineData(90, "1m 30s")]
    public void DurationTextFormats(double seconds, string expected) =>
        Assert.Equal(expected, SpawnDurationText.Format(seconds));

    // ---- the view model ----

    private static (SpawnsViewModel Vm, SpawnTimers Timers, SpawnOverrides Overrides) Vm()
    {
        var overrides = new SpawnOverrides();
        var timers = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };
        return (new SpawnsViewModel(TestCatalog(), overrides, timers), timers, overrides);
    }

    [Fact]
    public void RowsPutRunningTimersFirstAndNamePlaceholders()
    {
        var (vm, timers, _) = Vm();
        timers.Apply(new ZoneEvent(T0, "Lower Guk"));
        timers.Apply(new KillEvent(T0, "kor ghoul wizard", "You"));

        var rows = vm.RowsFor("Lower Guk", T0.AddMinutes(1));
        Assert.Equal(2, rows.Count);
        Assert.Equal("the ghoul arch magi", rows[0].Name);   // running timer sorts first
        Assert.True(rows[0].HasActiveTimer);
        Assert.Equal("the ghoul arch magi — Placeholder (kor ghoul wizard)", rows[0].DisplayName);
        Assert.Equal("27m", rows[1].DurationText);           // catalog 1620 s
    }

    [Fact]
    public void EditingADurationSticksAsAnOverrideAndRetimesTheClock()
    {
        var (vm, timers, overrides) = Vm();
        timers.Apply(new ZoneEvent(T0, "Lower Guk"));
        timers.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));

        vm.SetDuration("Lower Guk", "a froglok ghoul lord", "30m");

        Assert.Equal(1800, overrides.Find("Lower Guk", "a froglok ghoul lord")!.RespawnSeconds);
        Assert.Equal(T0.AddMinutes(30), Assert.Single(timers.Snapshot(T0)).DueAt);
    }

    [Fact]
    public void CustomNamedJoinTheirZoneAndDuplicatesAreRefused()
    {
        var (vm, _, _) = Vm();
        Assert.True(vm.AddCustom("Lower Guk", "the Fabled Froglok", "45m"));
        Assert.False(vm.AddCustom("Lower Guk", "a froglok ghoul lord", "45m")); // already catalogued

        var rows = vm.RowsFor("Lower Guk", T0);
        Assert.Contains(rows, r => r.Name == "the Fabled Froglok" && r.IsCustom && r.DurationText == "45m");
    }

    [Fact]
    public void DueAlertsFireOnceOnTheLiveTransitionAndNeverOnStartup()
    {
        var (vm, timers, _) = Vm();
        vm.ToggleAlert("Lower Guk", "a froglok ghoul lord");   // bell on (default off)
        timers.Apply(new ZoneEvent(T0, "Lower Guk"));
        timers.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));

        // First look happens after the timer already expired — startup priming, no alert.
        Assert.Empty(vm.ConsumeDueAlerts(T0.AddMinutes(60)));

        // A fresh kill counts down live: nothing while running, one alert at zero, silent after.
        timers.Apply(new KillEvent(T0.AddMinutes(70), "froglok ghoul lord", "You"));
        Assert.Empty(vm.ConsumeDueAlerts(T0.AddMinutes(71)));
        var due = vm.ConsumeDueAlerts(T0.AddMinutes(70 + 28));
        Assert.Equal("a froglok ghoul lord", Assert.Single(due).Name);
        Assert.Empty(vm.ConsumeDueAlerts(T0.AddMinutes(70 + 29)));
    }

    /// <summary>ConsumeNewTimers drives the pop-on-kill window: recovered timers pop at
    /// startup (unlike due ALERTS, which prime silently), each kill pops once, and a
    /// re-kill pops again because it carries a new kill time.</summary>
    [Fact]
    public void NewTimersReportOnceIncludingThoseRecoveredAtStartup()
    {
        var (vm, timers, _) = Vm();
        timers.Apply(new ZoneEvent(T0, "Lower Guk"));
        timers.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));   // "recovered" during ingest

        var first = vm.ConsumeNewTimers(T0.AddMinutes(1));
        Assert.Equal("a froglok ghoul lord", Assert.Single(first).Name);
        Assert.Empty(vm.ConsumeNewTimers(T0.AddMinutes(2)));            // unchanged — no re-pop

        timers.Apply(new KillEvent(T0.AddMinutes(5), "froglok ghoul lord", "You"));
        Assert.Single(vm.ConsumeNewTimers(T0.AddMinutes(6)));           // re-kill = new information

        Assert.True(vm.HasActiveTimers(T0.AddMinutes(7)));
    }

    /// <summary>Chicklets: every running timer on the server, soonest first, regardless
    /// of zone — a Befallen camp timer keeps its chip while you bank elsewhere.</summary>
    [Fact]
    public void ChipsSpanZonesSortSoonestFirstAndFlagDue()
    {
        var (vm, timers, _) = Vm();
        timers.Apply(new ZoneEvent(T0, "Lower Guk"));
        timers.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));          // 27 min
        timers.Apply(new ZoneEvent(T0.AddMinutes(1), "Permafrost Keep"));
        timers.Apply(new KillEvent(T0.AddMinutes(1), "Lady Vox", "You"));      // 7 days

        var chips = vm.Chips(T0.AddMinutes(2));
        Assert.Equal(2, chips.Count);
        Assert.Equal("a froglok ghoul lord", chips[0].Name);   // soonest first
        Assert.Equal("Lady Vox", chips[1].Name);
        Assert.All(chips, c => Assert.False(c.IsDue));

        var later = vm.Chips(T0.AddSeconds(1620 + 30));        // ghoul lord due 30 s ago
        Assert.True(later[0].IsDue);
        Assert.False(later[1].IsDue);

        vm.ClearTimer("Lower Guk", "a froglok ghoul lord");    // click-away on a due chip
        Assert.Equal("Lady Vox", Assert.Single(vm.Chips(T0.AddSeconds(1620 + 31))).Name);
    }

    /// <summary>Audit finding 14: SpawnChipsWindow only painted the track gauge at
    /// REBUILD, and rebuilds happen on signature change (zone|name|due) — so the fill
    /// froze between them. The window's per-tick refresh now reads Fraction every
    /// second; pinned here on the data side: the fraction advances across two ticks
    /// the chip signature calls identical. (The WPF width wiring itself mirrors
    /// MezChipsWindow's and isn't hostable in this suite.)</summary>
    [Fact]
    public void ChipFractionAdvancesBetweenSignatureStableTicks()
    {
        var (vm, timers, _) = Vm();
        timers.Apply(new ZoneEvent(T0, "Lower Guk"));
        timers.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));   // 27-min clock

        var early = Assert.Single(vm.Chips(T0.AddMinutes(2)));
        var later = Assert.Single(vm.Chips(T0.AddMinutes(20)));
        Assert.Equal((early.Zone, early.Name, early.IsDue),
            (later.Zone, later.Name, later.IsDue));                     // same rebuild signature
        Assert.True(later.Fraction!.Value > early.Fraction!.Value);     // live gauge data
    }

    /// <summary>Per-named due sounds: "Default" maps to Alarm (a camp popping is the
    /// most time-critical thing the app announces — David's call, deliberately NOT the
    /// Options alert sound); "Off" silences one named; anything else is that named's
    /// own built-in or file.</summary>
    [Theory]
    [InlineData(null, "Alarm")]             // untouched: Default = Alarm
    [InlineData("", "Alarm")]               // explicit Default pick: same
    [InlineData("Off", null)]               // opted out individually
    [InlineData("Chimes", "Chimes")]        // own pick wins
    [InlineData(@"C:\sounds\vox.mp3", @"C:\sounds\vox.mp3")]
    public void PerNamedSoundResolution(string? own, string? expected)
    {
        var (vm, _, _) = Vm();
        if (own is not null) vm.SetSound("Lower Guk", "a froglok ghoul lord", own);
        Assert.Equal(expected, vm.SoundFor("Lower Guk", "a froglok ghoul lord"));
    }

    /// <summary>The bell defaults OFF, matching watch-rule sounds — a due timer is
    /// visible (chip flips to DUE) but silent until opted in. Picking a concrete
    /// sound counts as opting in.</summary>
    [Fact]
    public void DueSoundsAreOptInAndPickingASoundOptsIn()
    {
        var (vm, timers, _) = Vm();
        vm.ConsumeDueAlerts(T0);                               // prime
        timers.Apply(new ZoneEvent(T0, "Lower Guk"));
        timers.Apply(new KillEvent(T0.AddMinutes(1), "froglok ghoul lord", "You"));
        Assert.Empty(vm.ConsumeDueAlerts(T0.AddMinutes(1 + 28)));   // bell off by default

        vm.SetSound("Lower Guk", "a froglok ghoul lord", "Chimes");  // picking a sound = bell on
        timers.Apply(new KillEvent(T0.AddMinutes(40), "froglok ghoul lord", "You"));
        var due = vm.ConsumeDueAlerts(T0.AddMinutes(40 + 28));
        Assert.Single(due);
        Assert.Equal("Chimes", vm.SoundFor("Lower Guk", "a froglok ghoul lord"));
    }

    // ---- raid-instance bosses (#109, Frankthetankk): no fake countdowns ----

    private static SpawnCatalog RaidCatalog()
    {
        var cat = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Plane of Hate",
                    NamedDefaultSeconds = 43200,
                    Named =
                    [
                        new SpawnEntry { Name = "Maestro of Rancor", RespawnSeconds = 259200 },
                        new SpawnEntry { Name = "Hand of the Maestro", RespawnSeconds = 43200 },
                        new SpawnEntry { Name = "the ghoul lord", Aliases = ["Hoptor Thaggelum"] },
                    ],
                },
            ],
        };
        SpawnCatalog.MarkRaidInstanced(cat, new RaidTargetCatalog(
        [
            new RaidTargetCatalog.ZoneEntry
                { Zone = "Plane of Hate", Bosses = ["Maestro of Rancor", "Hoptor Thaggelum"] },
        ]));
        return cat;
    }

    [Fact]
    public void RaidTargetCrossReferenceMarksBossesByNameAndAlias()
    {
        var cat = RaidCatalog();
        var zone = cat.Zones[0];
        Assert.True(zone.Named[0].RaidInstanced);                      // by name
        Assert.False(zone.Named[1].RaidInstanced);                     // mini-boss: not in the dump
        Assert.True(zone.Named[2].RaidInstanced);                      // by alias
        // Instanced entries have no effective respawn — not even the zone default.
        Assert.Null(SpawnCatalog.EffectiveSeconds(zone, zone.Named[0]));
        Assert.Equal(43200, SpawnCatalog.EffectiveSeconds(zone, zone.Named[1]));
    }

    [Fact]
    public void EmbeddedCatalogMarksTheReportedBosses()
    {
        // The three from Frank's screenshot, resolved through the real shipped data.
        var cat = SpawnCatalog.LoadEmbedded();
        foreach (var (zone, boss) in new[]
        {
            ("Plane of Hate", "Maestro of Rancor"),
            ("Nagafen's Lair", "Lord Nagafen"),
            ("Plane of Sky", "Noble Dojorn"),
            // The dump's "Cazic-Thule" must reach the catalog's space-form entry.
            ("Plane of Fear", "Cazic Thule"),
        })
        {
            var entry = cat.FindZone(zone)!.Named.Single(n => n.Name == boss);
            Assert.True(entry.RaidInstanced, $"{boss} not marked");
        }
        // The Raids card's whole catalog cross-marks: every boss the achievements
        // dump names that also sits in the spawn catalog is instanced there now.
        Assert.True(cat.Zones.Sum(z => z.Named.Count(n => n.RaidInstanced)) >= 3);
    }

    // ---- triggered spawns (#109 follow-up, Frankthetankk; FABLE.md, Fable 5) ----
    //
    // eqlwiki's own word: a creature page carrying `respawn_time = Triggered` appears when
    // something ELSE happens — the previous link in a chain dying, or a particular trash
    // kill — and has no cycle to count. The bug was not only the missing countdown
    // suppression: over an UNTRUSTED 8 h zone default, two Bzzzt kills three minutes apart
    // "measured" a three-minute respawn, wrote it to the overrides file as Learned, and
    // counted every later kill down to DUE. So the type has to stop LEARNING, and heal
    // what was already learned.

    private static SpawnCatalog SkyCatalog() => new()
    {
        Zones =
        [
            new SpawnZone
            {
                Zone = "Plane of Sky",
                NamedDefaultSeconds = 28800,      // untrusted, exactly as shipped
                Named =
                [
                    // The whole Island 6 chain, as the shipped catalog carries it since
                    // #109's four-bee follow-up (2026-08-23). The OPENER is the interesting
                    // one: eqlwiki gives Bzzazzt a real 12-hour clock, so it is deliberately
                    // NOT triggered — a chain whose first link is triggered never starts.
                    new SpawnEntry { Name = "Bzzazzt", RespawnSeconds = 43200, MultiSpawn = true },
                    new SpawnEntry { Name = "Bazzzazzt", SpawnType = "triggered", TriggeredBy = "Bzzazzt", MultiSpawn = true },
                    new SpawnEntry { Name = "Bzzzt", SpawnType = "triggered", TriggeredBy = "Bazzzazzt" },
                    new SpawnEntry { Name = "Noble Dojorn", RespawnSeconds = 604800 },
                ],
            },
        ],
    };

    [Fact]
    public void ATriggeredKillStartsNoCountdownAndARekillGapTeachesNothing()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(SkyCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "Plane of Sky"));

        t.Apply(new KillEvent(T0, "Bzzzt", "You"));
        Assert.Empty(t.Snapshot(T0.AddMinutes(1)));

        // Three minutes later, the next link dies. Before the type this was a "measured"
        // three-minute respawn, persisted as Learned.
        t.Apply(new KillEvent(T0.AddMinutes(3), "Bzzzt", "You"));
        Assert.Empty(t.Snapshot(T0.AddMinutes(4)));
        Assert.Null(overrides.Find("Plane of Sky", "Bzzzt"));

        // An ordinary timed named in the same zone is untouched.
        t.Apply(new KillEvent(T0, "Noble Dojorn", "You"));
        Assert.Equal("Noble Dojorn", Assert.Single(t.Snapshot(T0.AddMinutes(5))).Name);
        // A triggered entry contributes no default, even though the zone has one. Named by
        // NAME rather than by index: this read `Named[0]` until 2026-08-23, when two more
        // bees joined the chain in front of it and the assertion silently began testing a
        // different creature — which is how a positional fixture reference always fails.
        var sky = SkyCatalog().Zones[0];
        Assert.Null(SpawnCatalog.EffectiveSeconds(sky, sky.Named.Single(e => e.Name == "Bzzzt")));
    }

    [Fact]
    public void APoisonedLearnedOverrideHealsOnTheNextKillAndAtLoad()
    {
        // What a player who reported this already has in spawn-overrides.json.
        var overrides = new SpawnOverrides();
        var poisoned = overrides.GetOrAdd("Plane of Sky", "Bzzzt");
        poisoned.RespawnSeconds = 180;
        poisoned.Learned = true;

        var t = new SpawnTimers(SkyCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "Plane of Sky"));
        t.Apply(new KillEvent(T0, "Bzzzt", "You"));

        Assert.Empty(t.Snapshot(T0.AddMinutes(1)));
        var healed = overrides.Find("Plane of Sky", "Bzzzt")!;
        Assert.Null(healed.RespawnSeconds);
        Assert.False(healed.Learned);

        // And a countdown persisted before the type existed drops at load, as #109's did.
        var path = Path.Combine(Path.GetTempPath(), $"eqbuddy-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
                [{"Server":"freeport","Zone":"Plane of Sky","Name":"Bzzzt",
                  "KilledAt":"2026-07-18T15:00:00","DurationSeconds":180}]
                """);
            var reloaded = new SpawnTimers(SkyCatalog(), new SpawnOverrides(), path) { Server = "freeport" };
            Assert.Empty(reloaded.Snapshot(T0.AddMinutes(1)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void APlayerTypedDurationStillRunsOnATriggeredEntry()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(SkyCatalog(), overrides) { Server = "freeport" };
        var vm = new SpawnsViewModel(SkyCatalog(), overrides, t);
        vm.SetDuration("Plane of Sky", "Bzzzt", "10m");   // their reminder, their call

        t.Apply(new ZoneEvent(T0, "Plane of Sky"));
        t.Apply(new KillEvent(T0, "Bzzzt", "You"));
        Assert.Equal(T0.AddMinutes(10), Assert.Single(t.Snapshot(T0.AddMinutes(1))).DueAt);
    }

    /// <summary>The shipped catalog: every spawnType is a value we know (a typo in a
    /// curated file must fail here, never the load), and the four Sky mobs the report was
    /// about are typed, with a note that says which wiki page says so.</summary>
    [Fact]
    public void EmbeddedCatalogSpawnTypesAreKnownAndTheSkyChainIsTyped()
    {
        var cat = SpawnCatalog.LoadEmbedded();
        foreach (var zone in cat.Zones)
            foreach (var entry in zone.Named)
                Assert.Contains(entry.SpawnType, SpawnEntry.KnownSpawnTypes);

        var sky = cat.FindZone("Plane of Sky")!;
        foreach (var name in new[] { "Bzzzt", "Bazzt Zzzt", "The Spiroc Guardian", "The Spiroc Lord" })
        {
            var entry = sky.Named.Single(n => n.Name == name);
            Assert.True(entry.IsTriggered, $"{name} not typed");
            Assert.NotEmpty(entry.TriggeredBy);
            Assert.Contains("eqlwiki", entry.Note);
        }
        // The Lord keeps RaidInstanced — the achievements dump's fact to state — and is
        // now quiet for the right reason as well.
        Assert.True(sky.Named.Single(n => n.Name == "The Spiroc Lord").RaidInstanced);
        Assert.False(sky.Named.Single(n => n.Name == "Noble Dojorn").IsTriggered);
    }

    [Fact]
    public void KillingARaidBossStartsNoCountdown()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(RaidCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "Plane of Hate"));
        t.Apply(new KillEvent(T0, "Maestro of Rancor", "You"));
        Assert.Empty(t.Snapshot(T0.AddMinutes(1)));

        // The mini-boss the dump doesn't know keeps its normal clock.
        t.Apply(new KillEvent(T0, "Hand of the Maestro", "You"));
        Assert.Equal("Hand of the Maestro", Assert.Single(t.Snapshot(T0.AddMinutes(1))).Name);
    }

    [Fact]
    public void PlayerTypedDurationOutranksTheSuppression()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(RaidCatalog(), overrides) { Server = "freeport" };
        var vm = new SpawnsViewModel(RaidCatalog(), overrides, t);
        vm.SetDuration("Plane of Hate", "Maestro of Rancor", "24h");   // their reminder, their call

        t.Apply(new ZoneEvent(T0, "Plane of Hate"));
        t.Apply(new KillEvent(T0, "Maestro of Rancor", "You"));
        var timer = Assert.Single(t.Snapshot(T0.AddMinutes(1)));
        Assert.Equal(T0.AddHours(24), timer.DueAt);
    }

    [Fact]
    public void PersistedRaidBossCountdownsHealAtLoad()
    {
        // A countdown persisted before the suppression existed (Frank's Maestro at
        // "8:13:38") must not keep running for days after the update.
        var path = Path.Combine(Path.GetTempPath(), $"eqbuddy-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
                [{"Server":"freeport","Zone":"Plane of Hate","Name":"Maestro of Rancor",
                  "KilledAt":"2026-07-18T15:00:00","DurationSeconds":259200},
                 {"Server":"freeport","Zone":"Plane of Hate","Name":"Hand of the Maestro",
                  "KilledAt":"2026-07-18T15:00:00","DurationSeconds":43200}]
                """);
            var t = new SpawnTimers(RaidCatalog(), new SpawnOverrides(), path) { Server = "freeport" };
            Assert.Equal("Hand of the Maestro", Assert.Single(t.Snapshot(T0.AddMinutes(1))).Name);
        }
        finally { File.Delete(path); }
    }

    // ---- #109 round two: ANY kill inside an instanced zone starts no countdown ----
    // The zone-enter line is the game's only statement of instance-ness (community
    // 1.4M-line log survey, 2026-08-13); these are its real shapes.

    [Theory]
    [InlineData("The Plane of Hate - Solo")]                // base instance
    [InlineData("The Plane of Hate - Solo 4 (Refined)")]    // tiered, Solo word
    [InlineData("Nagafen's Lair - Group 3 (Fused)")]        // tiered, Group word
    [InlineData("Najena 4 (Refined)")]                      // tiered, no Solo/Group word
    [InlineData("Befallen 1 (Awakened)")]                   // difficulty-tier dungeon
    public void InstancedZoneNamesAreRecognized(string zone) =>
        Assert.True(SpawnCatalog.IsInstancedZoneName(zone));

    [Theory]
    [InlineData("The Plane of Hate")]
    [InlineData("Innothule Swamp")]
    [InlineData("Nagafen's Lair")]
    [InlineData("North Solace")]                            // digits-free, paren-free
    public void OpenWorldZoneNamesAreNot(string zone) =>
        Assert.False(SpawnCatalog.IsInstancedZoneName(zone));

    /// <summary>Frankthetankk's verbatim sequence (#109, 2026-08-21): a personal Plane of
    /// Sky instance announces itself as "Player X creating instance The Plane of Sky
    /// 13931." and then enters with a line BYTE-IDENTICAL to the open world's. So the
    /// zone gate keyed on the enter line alone could never fire there — exactly what
    /// Fable's plan suspected and could not check without his log. The announcement is
    /// spent on the next enter line, and a kill of an ordinary catalog named inside the
    /// instance starts no countdown; the same kill in the open world runs its clock.</summary>
    [Fact]
    public void ACreatingInstanceLineMakesTheNextEnterAnInstanceEvenWhenTheLineLooksOpenWorld()
    {
        var cat = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Plane of Sky", LogZoneName = "The Plane of Sky", RaidZone = true,
                    NamedDefaultSeconds = 28800,
                    Named = [new SpawnEntry { Name = "a presence" }],   // timed, not raid-listed, not triggered
                },
            ],
        };
        var t = new SpawnTimers(cat, new SpawnOverrides()) { Server = "freeport" };

        t.Apply(LogParser.Parse("[Fri Aug 21 21:00:00 2026] Player Dranak creating instance The Plane of Sky 13931.")!);
        t.Apply(LogParser.Parse("[Fri Aug 21 21:00:05 2026] You have entered The Plane of Sky.")!);
        t.Apply(LogParser.Parse("[Fri Aug 21 21:01:00 2026] You have slain a presence!")!);
        Assert.Empty(t.Snapshot(new DateTime(2026, 8, 21, 21, 2, 0)));

        // Open world: the same enter line with no announcement before it.
        t.Apply(LogParser.Parse("[Fri Aug 21 22:00:00 2026] You have entered The Plane of Sky.")!);
        t.Apply(LogParser.Parse("[Fri Aug 21 22:01:00 2026] You have slain a presence!")!);
        Assert.Single(t.Snapshot(new DateTime(2026, 8, 21, 22, 2, 0)));
    }

    [Fact]
    public void AStaleInstanceAnnouncementDoesNotLeakOntoALaterZone()
    {
        var t = Tracker();
        t.Apply(LogParser.Parse("[Fri Aug 21 21:00:00 2026] Player Dranak creating instance The Plane of Sky 13931.")!);
        // The player never zoned into Sky; the next enter line is an ordinary dungeon.
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        t.Apply(new KillEvent(T0, "a froglok ghoul lord", "You"));
        Assert.Single(t.Snapshot(T0.AddMinutes(1)));
    }

    [Fact]
    public void KillsInsideAnInstancedRaidZoneStartNoCountdownEvenForUnlistedNamed()
    {
        // Hand of the Maestro is NOT in the achievements dump — the entry-level
        // suppression can't catch him, the zone gate must (Frank's minis case).
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(RaidCatalog(), overrides) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "The Plane of Hate - Solo 2 (Adaptive)"));
        t.Apply(new KillEvent(T0, "Hand of the Maestro", "You"));
        Assert.Empty(t.Snapshot(T0.AddMinutes(1)));

        // The SAME kill in the open-world zone runs its normal clock.
        t.Apply(new ZoneEvent(T0.AddMinutes(2), "The Plane of Hate"));
        t.Apply(new KillEvent(T0.AddMinutes(2), "Hand of the Maestro", "You"));
        Assert.Equal("Hand of the Maestro", Assert.Single(t.Snapshot(T0.AddMinutes(3))).Name);
    }

    [Fact]
    public void OrdinaryDungeonInstancesKeepTheirTimers()
    {
        // Mobs respawn inside tier-variant leveling dungeons — our Befallen and
        // Crushbone zone clocks were MEASURED there. Only raid zones suppress.
        var t = Tracker();
        t.Apply(new ZoneEvent(T0, "The Ruins of Old Guk 3 (Fused)"));
        t.Apply(new KillEvent(T0, "a froglok ghoul lord", "You"));
        Assert.Equal("a froglok ghoul lord", Assert.Single(t.Snapshot(T0.AddMinutes(1))).Name);
    }

    [Fact]
    public void DualUseDungeonsHousingARaidBossKeepTheirCampTimers()
    {
        // 2026-08-13 review: the first zone gate suppressed EVERY named in any
        // instanced raid-catalog zone — but Kedge/Nagafen's Lair/Permafrost are
        // leveling dungeons that merely house one raid boss. Only the Planes are
        // pure raid zones; a camped named in tiered Permafrost must keep its clock.
        var cat = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Permafrost Keep",
                    NamedDefaultSeconds = 600,
                    Named =
                    [
                        new SpawnEntry { Name = "Lady Vox", RespawnSeconds = 604800 },
                        new SpawnEntry { Name = "the goblin king", RespawnSeconds = 900 },
                    ],
                },
            ],
        };
        SpawnCatalog.MarkRaidInstanced(cat, new RaidTargetCatalog(
            [new RaidTargetCatalog.ZoneEntry { Zone = "Permafrost Keep", Bosses = ["Lady Vox"] }]));

        Assert.False(cat.Zones[0].RaidZone);                   // dual-use, not a Plane
        Assert.True(cat.Zones[0].Named[0].RaidInstanced);      // Vox herself still gated

        var t = new SpawnTimers(cat, new SpawnOverrides()) { Server = "freeport" };
        t.Apply(new ZoneEvent(T0, "Permafrost Keep 2 (Adaptive)"));
        t.Apply(new KillEvent(T0, "the goblin king", "You"));  // ordinary camp: timer runs
        Assert.Equal("the goblin king", Assert.Single(t.Snapshot(T0.AddMinutes(1))).Name);

        t.Apply(new KillEvent(T0.AddMinutes(2), "Lady Vox", "You"));  // the boss: still none
        Assert.Single(t.Snapshot(T0.AddMinutes(3)));
    }

    [Fact]
    public void APlayerTypedDurationStillRunsInsideAnInstance()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(RaidCatalog(), overrides) { Server = "freeport" };
        var vm = new SpawnsViewModel(RaidCatalog(), overrides, t);
        vm.SetDuration("Plane of Hate", "Hand of the Maestro", "12h");

        t.Apply(new ZoneEvent(T0, "The Plane of Hate - Solo 4 (Refined)"));
        t.Apply(new KillEvent(T0, "Hand of the Maestro", "You"));
        Assert.Equal(T0.AddHours(12), Assert.Single(t.Snapshot(T0.AddMinutes(1))).DueAt);
    }

    /// <summary>Fable 5's lower-confidence H4 note, verified: a triggered entry carrying a
    /// `Learned` override from before 1.99.1 heals on the next KILL, but nothing healed it
    /// at LOAD — <c>SuppressedByCatalog</c> drops the persisted timer, not the override. So
    /// until the player next killed the mob, the row printed the poisoned duration ("3m")
    /// in the box beside the word "triggered", which contradicts itself on screen.
    /// Frankthetankk's own file is exactly this case, which is why it is worth the fix
    /// rather than waiting for a kill to tidy it.</summary>
    [Fact]
    public void APoisonedOverrideOnATriggeredEntryHealsAtLoadNotOnlyOnTheNextKill()
    {
        var overrides = new SpawnOverrides();
        var poisoned = overrides.GetOrAdd("Plane of Sky", "Bzzzt");
        poisoned.RespawnSeconds = 180;
        poisoned.Learned = true;

        // Construction alone — no kill, no zone line — is what a player gets on launch.
        var t = new SpawnTimers(SkyCatalog(), overrides) { Server = "freeport" };
        var vm = new SpawnsViewModel(SkyCatalog(), overrides, t);

        var row = vm.RowsFor("Plane of Sky", T0).Single(r => r.Name == "Bzzzt");
        Assert.Equal("triggered · Bazzzazzt", row.CountdownText);
        Assert.Equal("", row.DurationText);          // …and no "3m" contradicting it
        Assert.Null(overrides.Find("Plane of Sky", "Bzzzt")?.RespawnSeconds);
    }

    /// <summary>**The same heal, for the one bee that is NOT triggered** (#109's four-bee
    /// follow-up, 2026-08-23). eqlwiki gives Bzzazzt a real 12-hour clock — it is the opener,
    /// and a chain whose first link is triggered can never start — so the triggered and
    /// raid-instanced rules above both skip it, and its poisoned override would have
    /// survived them.
    ///
    /// What catches it is `multiSpawn`: three wasps share the name at island start, the
    /// learner already refuses to learn from such an entry, and therefore a `Learned` value
    /// on one is a number the current code cannot produce. That is the whole argument, and
    /// it is the failure this discussion opened with — two same-named kills three minutes
    /// apart becoming a three-minute timer that goes DUE forever.
    ///
    /// The catalog's own 12 hours takes over, rather than nothing: healing must not cost the
    /// player the honest answer the wiki already has.</summary>
    [Fact]
    public void APoisonedOverrideOnAMultiSpawnEntryHealsAtLoadToo()
    {
        var overrides = new SpawnOverrides();
        var poisoned = overrides.GetOrAdd("Plane of Sky", "Bzzazzt");
        poisoned.RespawnSeconds = 180;
        poisoned.Learned = true;

        var t = new SpawnTimers(SkyCatalog(), overrides) { Server = "freeport" };

        Assert.Null(overrides.Find("Plane of Sky", "Bzzazzt")?.RespawnSeconds);
        Assert.False(overrides.Find("Plane of Sky", "Bzzazzt")?.Learned);
        // And the wiki's clock is what the player is left with.
        var entry = SkyCatalog().Zones
            .Single(z => z.Zone == "Plane of Sky").Named.Single(e => e.Name == "Bzzazzt");
        Assert.Equal(TimeSpan.FromHours(12).TotalSeconds, entry.RespawnSeconds);
        _ = t;
    }

    /// <summary>**A player's TYPED duration survives all of it**, on a multi-spawn entry as
    /// everywhere else. The heal exists to remove numbers the app invented, never numbers
    /// the player chose — and this is the negative that keeps the rule above from quietly
    /// becoming "delete everything on a multiSpawn row".</summary>
    [Fact]
    public void TheMultiSpawnHealNeverTouchesATypedDuration()
    {
        var overrides = new SpawnOverrides();
        var mine = overrides.GetOrAdd("Plane of Sky", "Bzzazzt");
        mine.RespawnSeconds = 900;          // the player typed 15m
        mine.Learned = false;

        _ = new SpawnTimers(SkyCatalog(), overrides) { Server = "freeport" };

        Assert.Equal(900, overrides.Find("Plane of Sky", "Bzzazzt")?.RespawnSeconds);
    }

    /// <summary>Three triggers do not fit the "Next spawn" column, so the glance shows the
    /// first and counts the rest; the tooltip keeps every name (Bevel, 2026-08-22).</summary>
    [Fact]
    public void AMultiTriggerGlanceShowsTheFirstAndCountsTheRest()
    {
        // Fits: named on the glance, article stripped.
        Assert.Equal("Bazzzazzt", SpawnsViewModel.TriggerGlance("Bazzzazzt"));
        Assert.Equal("Bzzzt", SpawnsViewModel.TriggerGlance("Bzzzt"));

        // Does not fit: the bare word, and the tooltip keeps every name. NOT an ellipsis —
        // the first cut clipped "triggered · a spiroc banisher" mid-word into the Respawn
        // box, and a truncated trigger tells a player less than no trigger while looking
        // like a bug. Caught by the screenshot; no test could have seen it.
        Assert.Equal("", SpawnsViewModel.TriggerGlance("a spiroc banisher / a spiroc walker / a spiroc revolter"));
        Assert.Equal("", SpawnsViewModel.TriggerGlance("The Spiroc Guardian"));
        Assert.Equal("", SpawnsViewModel.TriggerGlance(""));

        // Every glance that IS shown fits the column's real budget.
        foreach (var t in new[] { "Bazzzazzt", "Bzzzt", "a spiroc banisher / a spiroc walker" })
            Assert.True(SpawnsViewModel.TriggerGlance(t).Length <= SpawnsViewModel.TriggerGlanceBudget);
    }

    /// <summary>The Sky follow-up to #109: the row for a triggered spawn says
    /// "triggered", names what brings it, and offers no fake default to edit — and it
    /// is a DIFFERENT word from "instance", because the next action is different.</summary>
    [Fact]
    public void ATriggeredRowSaysTriggeredAndNamesItsTrigger()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(SkyCatalog(), overrides) { Server = "freeport" };
        var vm = new SpawnsViewModel(SkyCatalog(), overrides, t);

        var row = vm.RowsFor("Plane of Sky", T0).Single(r => r.Name == "Bzzzt");
        Assert.Equal("triggered · Bazzzazzt", row.CountdownText);   // named on the glance
        Assert.Equal("Bazzzazzt", row.SuppressionNote);
        Assert.Equal("", row.DurationText);
        Assert.Equal(TimerSuppression.Triggered, row.Suppression);
        Assert.Contains("Bazzzazzt", row.Detail);
        Assert.Contains("eqlwiki", row.Detail);
        Assert.False(row.HasActiveTimer);

        // A typed duration turns it back into an ordinary row — their reminder, their call.
        vm.SetDuration("Plane of Sky", "Bzzzt", "10m");
        var typed = vm.RowsFor("Plane of Sky", T0).Single(r => r.Name == "Bzzzt");
        Assert.Equal(TimerSuppression.None, typed.Suppression);
        Assert.Equal("10m", typed.DurationText);
    }

    [Fact]
    public void RaidBossRowSaysInstanceInsteadOfABlank()
    {
        var overrides = new SpawnOverrides();
        var t = new SpawnTimers(RaidCatalog(), overrides) { Server = "freeport" };
        var vm = new SpawnsViewModel(RaidCatalog(), overrides, t);

        var row = vm.RowsFor("Plane of Hate", T0).Single(r => r.Name == "Maestro of Rancor");
        Assert.Equal("instance", row.CountdownText);
        Assert.Equal("", row.DurationText);                            // no fake default to edit
        Assert.Contains("Instance Maintenance", row.Detail);
        Assert.False(row.HasActiveTimer);

        // The unmarked mini-boss row is untouched.
        var hand = vm.RowsFor("Plane of Hate", T0).Single(r => r.Name == "Hand of the Maestro");
        Assert.Equal("", hand.CountdownText);
        Assert.Equal("12h", hand.DurationText);
    }
    /// <summary>
    /// A PET IS NOT A NAMED, and the profiles that already learned one get cleaned up
    /// (David, 2026-08-22: *"we were starting to see named's pet"*).
    ///
    /// The heuristic refusing them stops NEW ones, which is invisible to the person who
    /// reported it — his list is already full of them. A fix the reporter cannot see is a
    /// fix they report again.
    /// </summary>
    [Fact]
    public void PetsAreDiscoveredNeverAndPurgedFromProfilesThatHaveThem()
    {
        var overrides = new SpawnOverrides();
        // A profile an older build wrote: a pet learned as a named, beside a real one.
        var discovered = overrides.GetOrAdd("Plane of Hate", "Xanthus`s pet");
        discovered.Learned = true;
        discovered.Discovered = true;   // EQBuddy put it there, so EQBuddy may take it away
        overrides.GetOrAdd("Plane of Hate", "Innoruk").Learned = true;

        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };

        Assert.Null(overrides.Find("Plane of Hate", "Xanthus`s pet"));
        // …and the real named beside it is untouched. Without this the purge could be
        // emptying the whole store and the test above would not notice.
        Assert.NotNull(overrides.Find("Plane of Hate", "Innoruk"));

        // And nothing new is discovered from a pet kill, however proper the name reads.
        t.Apply(new ZoneEvent(T0, "Plane of Hate"));
        t.Apply(new KillEvent(T0, "Xanthus`s pet", "You", ProperName: true));
        Assert.Null(overrides.Find("Plane of Hate", "Xanthus`s pet"));
    }

    /// <summary>
    /// THE PURGE MUST NOT TOUCH THE PLAYER'S OWN WORK (Fable 5, v1.99.5 release review).
    ///
    /// The first cut matched on the NAME alone, which would have deleted a hand-added entry
    /// and a TYPED duration for anything a player called "… pet" — against the principle
    /// written on <c>SpawnOverride.Discovered</c> itself: *"so a discovery can be discarded
    /// without touching the player's own additions."* The original test could not see it
    /// because it never set <c>Custom</c>.
    /// </summary>
    [Fact]
    public void ThePetPurgeSparesEntriesThePlayerAddedThemselves()
    {
        var overrides = new SpawnOverrides();
        // A player's own entry, with a duration they typed. Named like a pet on purpose.
        var mine = overrides.GetOrAdd("Plane of Hate", "Teacher`s pet");
        mine.Custom = true;
        mine.RespawnSeconds = 300;      // typed, so Learned stays false — see IsManual
        // …and beside it, one EQBuddy discovered, which SHOULD go.
        var ours = overrides.GetOrAdd("Plane of Hate", "Xanthus`s pet");
        ours.Discovered = true;
        ours.Learned = true;

        var t = new SpawnTimers(TestCatalog(), overrides) { Server = "freeport" };

        Assert.NotNull(overrides.Find("Plane of Hate", "Teacher`s pet"));
        Assert.Equal(300, overrides.Find("Plane of Hate", "Teacher`s pet")!.RespawnSeconds);
        Assert.Null(overrides.Find("Plane of Hate", "Xanthus`s pet"));
    }

}
