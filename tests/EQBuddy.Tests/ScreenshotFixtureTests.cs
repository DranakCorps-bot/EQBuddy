using System.Text.Json;
using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Builds the wire snapshot the README's EQBuddy Mobile screenshots are rendered from,
/// through the REAL projection — the game's own paw.txt for geometry, a real
/// SpawnPointLedger taught by real kill lines, a real breadcrumb trail. Only the clock
/// and the log are fixtures; every transformation is the shipped one.
///
/// Skipped by default: it needs the game's maps folder, and it writes a file. Run it
/// deliberately when refreshing the shots:
///   dotnet test --filter FullyQualifiedName~ScreenshotFixture -e EQBUDDY_SHOOT=1
/// </summary>
public class ScreenshotFixtureTests
{
    private const string MapsFolder =
        @"C:\Users\Public\Daybreak Game Company\Installed Games\EverQuest Legends\maps";

    [Fact]
    public void WriteMobileSnapshot()
    {
        // Opt-in rather than a skip attribute: this is tooling, and it is not worth a
        // package reference in the test project to dress it up as a skipped test.
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;
        if (!Directory.Exists(MapsFolder)) return;

        var now = new DateTime(2026, 8, 14, 21, 12, 0);
        const string logZone = "The Lair of the Splitpaw";   // names paw.txt via the alias table
        const string timerZone = "Splitpaw Lair";            // what the spawn catalog calls it

        var dir = Path.Combine(Path.GetTempPath(), "eqb-shot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var catalog = SpawnCatalog.LoadEmbedded();
        var points = new SpawnPointLedger(Path.Combine(dir, "zone-spawns"), catalog);
        points.Apply(new ZoneEvent(now.AddMinutes(-52), timerZone));

        // Three named, killed at their camps — each becomes an archived circle.
        var named = new[]
        {
            ("A Nisch Mas Mender", -430.0, 240.0, 21.0),
            ("Kurrpok Splitpaw", -330.0, 280.0, 14.0),
            ("Rosch Val L'Vlor", -600.0, 120.0, 8.0),
        };
        foreach (var (name, y, x, minsAgo) in named)
        {
            points.Apply(new LocationEvent(now.AddMinutes(-minsAgo).AddSeconds(-30), y, x, -78.97));
            points.Apply(new KillEvent(now.AddMinutes(-minsAgo), name, "Hugzee"));
        }
        // A couple of trash camps so the map shows the dim circles too.
        foreach (var (y, x) in new[] { (-560.0, 150.0), (-480.0, 210.0) })
        {
            points.Apply(new LocationEvent(now.AddMinutes(-30), y, x, -78.97));
            points.Apply(new KillEvent(now.AddMinutes(-30), "a gnoll pup", "Hugzee"));
        }

        var timers = named.Select((n, i) => new SpawnTimerState(
            "legends", timerZone, n.Item1, now.AddMinutes(-n.Item4), (i + 4) * 300.0)).ToList();

        // The comet tail: crumbs inside TrailFade's one-minute horizon, 25+ units apart.
        var trail = new List<LocationEvent>();
        var walk = new[] { (-330.0, 280.0), (-368.0, 300.0), (-402.0, 318.0), (-436.0, 331.0), (-470.0, 344.0), (-500.0, 358.0) };
        for (var i = 0; i < walk.Length; i++)
            trail.Add(new LocationEvent(now.AddSeconds(-52 + i * 10), walk[i].Item1, walk[i].Item2, -78.97));

        var maps = new CompanionMapSource(new AppSettings { MapFolder = MapsFolder });
        var map = maps.Build(new CompanionMapRequest
        {
            MapZone = logZone,
            TimerZone = timerZone,
            Points = points,
            Timers = timers,
            Location = trail[^1],
            Trail = trail,
            // Two camps learned from the kills, one deliberately "from the wiki" so the
            // shot shows the ~ the desktop prints.
            CampFor = t => t.Name switch
            {
                "A Nisch Mas Mender" => (-430.0, 240.0, false),
                "Kurrpok Splitpaw" => (-330.0, 280.0, false),
                "Rosch Val L'Vlor" => (-600.0, 120.0, true),
                _ => null,
            },
        }, now);

        // EQBUDDY_SHOOT_THEME picks the palette (the DRA-48 landing shoots BlueGrey);
        // catalog names fall through CustomTheme.PaletteFor to ThemePalettes.For.
        var themeKey = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_THEME") ?? "midnight";
        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Hugzee",
            AppVersion = "1.80.0",
            Offered = [CompanionSurfaces.Map],
            Stats = new StatsSnapshot { CurrentZone = logZone },
            Map = map,
            Theme = CompanionTheme.Project(themeKey,
                EQBuddy.UI.Shared.CustomTheme.PaletteFor(new AppSettings { Theme = themeKey })),
        }, now);

        var outPath = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_OUT")
            ?? Path.Combine(Path.GetTempPath(), "eqbuddy-mobile-snapshot.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(snap, CompanionSnapshot.JsonOpts));

        Assert.NotNull(map.Geometry);
        Assert.NotEmpty(map.Circles);
        Assert.NotEmpty(map.Trail);
        Assert.NotEmpty(map.Named);
        Directory.Delete(dir, true);
    }

    /// <summary>
    /// The Progress theme's snapshot for the mobile harness — specifically the next-level
    /// split, which is the one half of that feature with no automated guard.
    ///
    /// **It exists because the manual check that passed was the wrong shape.** The split
    /// was verified by driving the shipped page against a snapshot typed BY HAND, so it
    /// exercised the page against a payload the server never sends — and the wire key was
    /// wrong (`className` where the page reads `class`), which the hand-written snapshot
    /// could not possibly reveal. Found by Fable 5 in the v1.99.6 review. Everything here
    /// goes through the real catalogs, the real <see cref="LevelUnlocks"/> and the real
    /// projection, so the JSON the harness loads is byte-identical in shape to a phone's.
    ///
    /// Warrior/Druid/Monk at level 12 — David's own combination, and the one that shows
    /// all three rules at once: two classes with nothing, one with three spells.
    ///   dotnet test --filter FullyQualifiedName~ScreenshotFixture -e EQBUDDY_SHOOT=1
    /// </summary>
    [Fact]
    public void WriteMobileProgressSnapshot()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;

        var now = new DateTime(2026, 8, 23, 21, 12, 0);
        string[] classes = ["Warrior", "Druid", "Monk"];
        const int level = 12;

        var stats = new StatsSnapshot
        {
            CurrentZone = "West Commonlands",
            Elapsed = TimeSpan.FromHours(2),
            XpPercent = 16.0,
            XpPerHour = 14.2,
            XpPerActiveHour = 14.2,
            AaGained = 1,
            AaTotal = 8,
            AaPerHour = 0.5,
            HoursToLevel = 7.0,
            // So the Experience room's mote line has something to say — the other half
            // of what shipped that day, and it is drawn by the same body.
            Loot = [new LootDetail("Mote of Lesser Potential", 3, "a ghoul")],
            // The live half of the Level-ups list: the ding that got this character to 12,
            // an hour into the session. Stored dings below carry the three before it.
            Levels = [new TimedDetail(now.AddHours(-1), $"Level {level}")],
        };

        // Every level-up this character has (#240), through the REAL merge — three
        // archived sessions plus the live ding, which is the state the fold is about: a
        // list that outlives the session, with a gap between rows that spans nights. A
        // hand-written list here would be trap 23 with a JSON key, exactly as the
        // next-level split was.
        var levelUps = LevelHistory.Rows(
            [
                new SessionRepository.ProgressPoint(now.AddDays(-2), 0,
                    [(now.AddDays(-2).AddHours(-2), 9), (now.AddDays(-2), 10)]),
                new SessionRepository.ProgressPoint(now.AddDays(-1), 0, [(now.AddDays(-1), 11)]),
            ],
            stats);

        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            AppVersion = UpdateChecker.CurrentVersion.ToString(),
            Offered = [CompanionSurfaces.Progress],
            Stats = stats,
            Level = level,
            Unlocks = LevelUnlocks.UnlocksAt(classes, level),
            UnlockClasses = classes,
            NextUnlocks = LevelUnlocks.Next(classes, level),
            LevelUps = levelUps,
            Theme = CompanionTheme.Project("midnight",
                EQBuddy.UI.Shared.CustomTheme.PaletteFor(new AppSettings { Theme = "midnight" })),
        }, now);

        var outPath = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_OUT")
            ?? Path.Combine(Path.GetTempPath(), "eqbuddy-mobile-progress.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(snap, CompanionSnapshot.JsonOpts));

        // The shape the harness is about to be judged on, asserted here so a fixture that
        // silently stops carrying the feature fails rather than photographing an empty
        // room (trap 22). Three groups, two of them empty, Druid opening.
        var progress = Assert.IsType<CompanionProgressSection>(snap.Progress);
        Assert.Equal("At level 13: 3 new spells", progress.NextLabel);
        Assert.Equal(classes, progress.NextGroups!.Select(g => g.Class));
        Assert.True(progress.NextGrouped);
        Assert.Equal(1, progress.NextOpenIndex);
        Assert.Equal(3, progress.NextGroups![1].Rows.Count);
        Assert.Equal("Nothing new at 13", progress.NextGroups![0].Empty);
        Assert.Equal("3 motes · 1.5/hr", progress.MoteLine);
        // And the Level-ups fold the harness is about to photograph: four rows newest
        // first, the label the PC decided, and a gap on every row but the oldest. Written
        // down BEFORE the run (trap 23) — a picture whose numbers nobody predicted has not
        // been reviewed.
        Assert.Equal("Level-ups (4) · last Aug 23", progress.LevelUpsLabel);
        Assert.Equal(["Level 12", "Level 11", "Level 10", "Level 9"],
            progress.LevelUps!.Select(r => r.Name));
        Assert.Equal("Aug 23, 8:12 PM", progress.LevelUps[0].Value);
        Assert.Equal("23h since the previous level-up", progress.LevelUps[0].Tip);
        Assert.Null(progress.LevelUps[^1].Tip);
    }

    /// <summary>The quest surface's snapshot for the mobile harness and the README
    /// shots: the REAL embedded catalog (all ~1,200 quests, so the shot carries the
    /// real search index and its real weight), a real ledger taught by real calls,
    /// and the shipped projection. Needs no maps folder.
    ///   dotnet test --filter FullyQualifiedName~ScreenshotFixture -e EQBUDDY_SHOOT=1</summary>
    [Fact]
    public void WriteMobileQuestsSnapshot()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;

        var now = new DateTime(2026, 8, 16, 21, 12, 0);
        var catalog = QuestCatalog.LoadEmbedded();
        Assert.NotEmpty(catalog.Quests);

        var dir = Path.Combine(Path.GetTempPath(), "eqb-shot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ledger = new QuestLedgerStore(Path.Combine(dir, "quest-ledger.json"))
        { TrackFilter = catalog.IsTurnInItem, Normalize = QuestCatalog.BaseItemName };
        const string key = "hugzee_legends";

        // A believable mid-session ledger: some farmed stacks, one pinned goal, one
        // dabbled class pair, one finished quest.
        ledger.RecordLoot(key, "Bone Chips", 4, now.AddMinutes(-40));
        ledger.SetManual(key, "Crushbone Belt", 6);
        ledger.SetManual(key, "Blue Orc Head", 1);
        ledger.SetClasses(key, ["Bard", "Monk"]);
        var pinnable = catalog.Quests.FirstOrDefault(q => q.Items.Count > 0);
        if (pinnable is not null) ledger.SetTracked(key, pinnable.Name, true);
        // A pin on a GUIDED quest, so the General tab's walkthrough is in the frame at all
        // (DRA-46). Without one the harness photographs a quest list with no guide under any
        // card — a real state of something else, and the state every card was in before this
        // slice (trap 22). Chosen off the catalog rather than named, because the harvest is
        // regenerated weekly.
        var guided = catalog.Quests.FirstOrDefault(q =>
            q.Items.Count >= 2
            && GuideChecklistProjection.QuestGuideFor(GuideCatalog.Default, q.Name) is not null);
        if (guided is not null) ledger.SetTracked(key, guided.Name, true);

        var settings = new AppSettings
        {
            EpicQuestChecklist =
            [
                new EpicQuestChecklistItem { Id = "e1", ClassName = "Bard", Section = "Pieces", QuestItem = "Sword of the Ykesha", Order = 1, Acquired = true },
                new EpicQuestChecklistItem { Id = "e2", ClassName = "Bard", Section = "Pieces", QuestItem = "Mace of the Shadowed Soul", Order = 2 },
                new EpicQuestChecklistItem { Id = "e3", ClassName = "Monk", Section = "Pieces", QuestItem = "Robe of the Whistling Fists", Order = 1 },
            ],
            // TWO classes, and one reward with every piece in hand, so the ★ Ready band
            // actually appears — without that the mobile Sky surface could not show the
            // shape #212 (bjstrange) reported, which is why nothing here caught it.
            SkyQuestChecklist =
            [
                new SkyQuestChecklistItem { Id = "s1", ClassName = "Bard", Reward = "Singing Short Sword", Npc = "Gorgalosk", QuestItem = "Bracelet of the Sky", Acquired = true },
                new SkyQuestChecklistItem { Id = "s2", ClassName = "Bard", Reward = "Singing Short Sword", Npc = "Gorgalosk", QuestItem = "Efreeti War Spear" },
                new SkyQuestChecklistItem { Id = "s3", ClassName = "Bard", Reward = "Mask of Song", Npc = "Cilin Spellsinger", QuestItem = "Light Woolen Mask", Acquired = true },
                new SkyQuestChecklistItem { Id = "s4", ClassName = "Bard", Reward = "Mask of Song", Npc = "Cilin Spellsinger", QuestItem = "Wind Rune Meda", Acquired = true },
                new SkyQuestChecklistItem { Id = "s5", ClassName = "Cleric", Reward = "Baton of the Sky", Npc = "Josin Faithbringer", QuestItem = "Efreeti Standard" },
            ],
            // A class NOBODY here plays, and nothing in the app can change it — the
            // widget's Sky card was its only writer and 2026-08-16 deleted that card.
            // Mobile used to scope its whole Sky list by this, so a stale value emptied
            // the page below the Ready band forever (#212). Staged deliberately: the
            // fixture must carry the poison for the shot to prove the antidote.
            SkyQuestClass = "Necromancer",
        };

        var request = new CompanionQuestRequest
        {
            Catalog = catalog,
            Owned = ledger.For(key),
            Tracked = ledger.TrackedFor(key),
            Hidden = ledger.HiddenFor(key),
            Completed = ledger.CompletedFor(key),
            Classes = ledger.ClassesFor(key),
            // THE LEDGER ITSELF, which this fixture never passed. Every guided surface on the
            // phone is gated on it (`settings is not null && req.Ledger is not null`), so the
            // harness had been photographing the CLASSIC Sky and Epic lists ever since guides
            // shipped, and would have shown no quest walkthrough at all. The running app
            // always passes one; a fixture that does not is staging a state the product does
            // not have (trap 23).
            Ledger = ledger,
            CharacterKey = key,
        };
        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Hugzee",
            AppVersion = "1.88.0",
            Offered = [CompanionSurfaces.Quests],
            Stats = new StatsSnapshot { CurrentZone = "Crushbone" },
            Settings = settings,
            Quests = request,
            QuestIndex = CompanionQuestIndex.Build(catalog),
            Theme = CompanionTheme.Project("ParchmentBrass",
                EQBuddy.UI.Shared.ThemePalettes.For("ParchmentBrass")),
        }, now);

        var outPath = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_OUT")
            ?? Path.Combine(Path.GetTempPath(), "eqbuddy-mobile-quests-snapshot.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(snap, CompanionSnapshot.JsonOpts));

        Assert.NotNull(snap.Quests);
        Assert.NotNull(snap.Quests!.Catalog);
        Assert.NotEmpty(snap.Quests.Mine);

        // A SECOND snapshot for the state the class-source line actually exists in: no
        // picks, and the character's classes resolved from an achievements dump.
        //
        // The fixture above sets picks, so `d.classes` is non-empty and the page suppresses
        // that line entirely — it could never have exercised it, which is exactly the shape
        // that let the next-level split ship with an unreadable wire key. Written through
        // the real projection for the same reason.
        var resolved = CharacterClasses.Resolve(
            unlocked: ["Warrior", "Druid", "Monk"], inferred: null, picks: null);
        var noPicks = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            AppVersion = UpdateChecker.CurrentVersion.ToString(),
            Offered = [CompanionSurfaces.Quests],
            Stats = new StatsSnapshot { CurrentZone = "Crushbone" },
            Settings = settings,
            Quests = new CompanionQuestRequest
            {
                Catalog = catalog,
                Owned = ledger.For(key),
                Tracked = ledger.TrackedFor(key),
                Hidden = ledger.HiddenFor(key),
                Completed = ledger.CompletedFor(key),
                Classes = [],                                   // nothing picked
                CharacterClassNames = resolved.Classes,
                ClassSource = resolved.Source,
            },
            QuestIndex = CompanionQuestIndex.Build(catalog),
            Theme = CompanionTheme.Project("ParchmentBrass",
                EQBuddy.UI.Shared.ThemePalettes.For("ParchmentBrass")),
        }, now);

        File.WriteAllText(
            Path.ChangeExtension(outPath, null) + "-nopicks.json",
            JsonSerializer.Serialize(noPicks, CompanionSnapshot.JsonOpts));

        // The shape the harness is about to be judged on, so a fixture that stops carrying
        // the feature fails here rather than photographing an empty line (trap 22).
        Assert.Equal(["Warrior", "Druid", "Monk"], noPicks.Quests!.CharacterClasses);
        Assert.Equal("from your achievements", noPicks.Quests.ClassSourceLabel);
        Assert.Empty(noPicks.Quests.Classes);

        Directory.Delete(dir, true);
    }

    /// <summary>
    /// The PROGRESS THEME's four tabs, through the real projection, for driving the
    /// shipped page in scripts/mobile-harness.ps1.
    ///
    /// It exists because the theme grew three whole blocks on the phone in one change
    /// (Wealth, Faction, Raids) and the page's own layout rules have bitten this repo four
    /// times — a flex `margin: 0 auto` collapsing a column, a CSS class beating a
    /// presentation attribute, a layout class carrying behaviour, a headless viewport that
    /// was not the CSS viewport. None of those were visible to a unit test, and all of them
    /// were visible the moment the real page was driven with a real snapshot.
    ///
    ///   dotnet test --filter FullyQualifiedName~ScreenshotFixture -e EQBUDDY_SHOOT=1 \
    ///     -e EQBUDDY_SHOOT_PROGRESS=&lt;path.json&gt;
    /// </summary>
    [Fact]
    public void WriteProgressSnapshot()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;
        var outPath = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_PROGRESS");
        if (string.IsNullOrWhiteSpace(outPath)) return;

        var now = new DateTime(2026, 8, 14, 21, 12, 0);
        var dir = Path.Combine(Path.GetTempPath(), "eqb-prog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        // A real ledger, taught the way the app teaches it — one witnessed kill and one
        // imported achievement, so the Raids tab shows both row shapes rather than one.
        var raids = new RaidKillLedger(Path.Combine(dir, "raid-kills.json"))
        {
            CharacterKey = () => "testchar_test",
        };
        raids.Apply(new KillEvent(now.AddHours(-2), "Phinigel Autropos", "you"));
        // The dump's own shape — section "EverQuest: Raids", a "Conqueror of …" entry, and
        // the boss as a completed CRITERION. Written from RaidKillLedger.MarkAchievements
        // rather than guessed: a plausible-looking entry it silently ignores would have
        // produced a snapshot with one clear instead of two, which is a real state and
        // therefore an invisible fixture bug (trap 23).
        raids.MarkAchievements([new AchievementEntry(
            "EverQuest: Raids", "Conqueror of Nagafen's Lair", true,
            [("Lord Nagafen", true)])]);

        var stats = new StatsSnapshot
        {
            SessionStart = now.AddHours(-1),
            XpPercent = 16.0,
            XpPerHour = 14.2,
            AaGained = 1,
            AaTotal = 8,
            Copper = 51408,
            CorpseCopper = 13401,
            VendorCopper = 38007,
            CoinDrops = 30,
            SalesCount = 55,
            Faction = [new FactionDetail("Knights of Truth", 4, 80)],
        };

        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Testchar",
            AppVersion = "test",
            Stats = stats,
            // SESSION as well as Progress since E-3 PR 5: the raid clears this fixture
            // exists to stage now ride the Session screen, so a harness run offered only
            // Progress would photograph a page with no raids on it at all — a real state,
            // and therefore an invisible fixture bug (trap 23).
            Offered = [CompanionSurfaces.Progress, CompanionSurfaces.Session],
            Raids = raids,
            Level = 12,
            Theme = CompanionTheme.Project("midnight",
                EQBuddy.UI.Shared.CustomTheme.PaletteFor(new AppSettings { Theme = "midnight" })),
        }, now);

        File.WriteAllText(outPath!, JsonSerializer.Serialize(snap, CompanionSnapshot.JsonOpts));

        // Predicted before the run (trap 23): THREE tabs — it was four until E-3 PR 5 moved
        // Raids to the Session screen with the desktop's Raids tab — the catalog's own boss
        // total, and two clears in two different shapes.
        Assert.Equal(3, snap.Progress!.Tabs.Count);
        Assert.DoesNotContain("raids", snap.Progress.Tabs.Select(t => t.Key));
        Assert.Equal(RaidTargetCatalog.Default.BossCount, snap.Session!.Raids!.Total);
        Assert.Equal(2, snap.Session.Raids.Defeated);
        Directory.Delete(dir, true);
    }

    /// <summary>
    /// The GEAR checklist on the phone, for driving the shipped page in
    /// scripts/mobile-harness.ps1. It had no fixture at all, which is why the phone's
    /// half of David's 2026-08-20 report ("telling me to import it but not telling me
    /// how") could not be looked at before it was written.
    ///
    /// Pass <c>-e EQBUDDY_SHOOT_GEAR_EMPTY=1</c> for the state a new player meets — the one
    /// the complaint was about. The populated state is the default because it is the one
    /// that proves the prompt does not belong to the empty branch: the player whose import
    /// has gone stale is holding a full list.
    ///
    ///   dotnet test --filter FullyQualifiedName~ScreenshotFixture -e EQBUDDY_SHOOT=1 \
    ///     -e EQBUDDY_SHOOT_GEAR=&lt;path.json&gt;
    /// </summary>
    [Fact]
    public void WriteGearSnapshot()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;
        var outPath = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_GEAR");
        if (string.IsNullOrWhiteSpace(outPath)) return;
        var empty = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_GEAR_EMPTY") == "1";

        var now = new DateTime(2026, 8, 20, 21, 12, 0);
        // The same list shoot.ps1 seeds for the desktop's gearloot-gear shot, on purpose:
        // the two surfaces are then photographs of the same data, and a difference between
        // them is a difference in the SURFACES rather than in what they were handed.
        var settings = new AppSettings
        {
            GearChecklistName = empty ? "" : "Kael push",
            GearChecklist = empty ? [] :
            [
                new GearChecklistItem { Slot = "HEAD", Item = "Crown of Narandi", Source = "Kael Drakkel" },
                new GearChecklistItem { Slot = "HANDS", Item = "Gloves of Dark Embers", Source = "Sebilis", Acquired = true },
                new GearChecklistItem { Slot = "PRIMARY", Item = "Blade of Carnage", Source = "Kael Drakkel" },
                new GearChecklistItem { Slot = "NECK", Item = "Silver Chain of Dread", Source = "Plane of Fear" },
                new GearChecklistItem
                {
                    Slot = "HEAD", Item = "Exquisite Velium Shard", IsExaltation = true,
                    ExaltationEffect = "+15 hp", Source = "Kael Drakkel",
                },
            ],
        };

        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Testchar",
            AppVersion = "test",
            Stats = new StatsSnapshot { CurrentZone = "Kael Drakkel" },
            Offered = [CompanionSurfaces.Gear],
            Settings = settings,
            Theme = CompanionTheme.Project("ParchmentBrass",
                EQBuddy.UI.Shared.ThemePalettes.For("ParchmentBrass")),
        }, now);

        File.WriteAllText(outPath!, JsonSerializer.Serialize(snap, CompanionSnapshot.JsonOpts));

        // Predicted before the run (trap 23). The prompt rides BOTH states — that is the
        // fact under review, so it is asserted rather than looked for in the picture.
        Assert.Equal(empty ? 0 : 5, snap.Gear!.Total);
        Assert.Equal(empty ? 0 : 1, snap.Gear.Done);
        Assert.Equal(EQBuddy.UI.Shared.GameCommands.OutputfileInventory, snap.Gear.Prompt!.Command);
        Assert.Equal(EQBuddy.UI.Shared.GearChecklistPresentation.EmptyRoute, snap.Gear.Empty);
    }

    /// <summary>
    /// The Sky tab's LEFTOVER BANDS on the phone (#243, tvongaza) — the third renderer of
    /// the join PR 0 built and PR 1 drew on both desktops.
    ///
    /// Through the real defaults, the real embedded quest catalog and the real projection,
    /// for the reason the Progress fixture above states in as many words: the one thing a
    /// hand-typed snapshot cannot check is whether the server sends the shape the page
    /// reads. It also stages a state the surface cannot otherwise be reviewed in — the
    /// bands are ABSENT without a dump, so a default profile photographs as a Sky tab with
    /// nothing to say about them (trap 22).
    ///
    /// **Predicted before the run (trap 23), and asserted here so a fixture drift fails
    /// instead of producing a plausible picture of something else:**
    ///
    ///   No longer needed — 2      Amulet of Woven Hair ×1 · bags
    ///                             Crude Wooden Flute ×1 · bags
    ///     note: 1 more is still wanted by another quest:
    ///           Black Silk Cape (Necromancer Epic Quest)
    ///   Other classes still want — 2
    ///                             Azure Ring ×1 · bags
    ///                             Brass Knuckles ×2 · bank
    ///
    /// A Bard who has handed in Ervaj's Flute of Flight and the Amulet of the Fae, holding
    /// four other pieces plus a Necromancer cape the Necro epic still wants. Azure Ring is
    /// Warrior's and Brass Knuckles are Beastlord's and Monk's — no class this character
    /// has, which is band B's whole claim — and the cape is band A's veto, so it is named
    /// in the note rather than silently missing.
    ///
    ///   dotnet test --filter FullyQualifiedName~ScreenshotFixture -e EQBUDDY_SHOOT=1     ///     -e EQBUDDY_SHOOT_SKY=&lt;path.json&gt;
    /// </summary>
    [Fact]
    public void WriteSkyLeftoverSnapshot()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;
        var outPath = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_SKY");
        if (string.IsNullOrWhiteSpace(outPath)) return;

        var now = new DateTime(2026, 9, 2, 21, 12, 0);
        var settings = new AppSettings();
        settings.ApplyDefaultSkyQuestChecklist();   // the shipped catalog, not a hand list
        settings.SkyQuestCompleted.AddRange([
            QuestChecklistLayout.RewardKey("Bard", "Ervaj's Flute of Flight"),
            QuestChecklistLayout.RewardKey("Bard", "Amulet of the Fae"),
            // Profile-global, like the checklist itself: the Necro alt's cloak is what
            // gives band A something to hold back and a quest to name.
            QuestChecklistLayout.RewardKey("Necromancer", "Cloak of Spiroc Feathers"),
        ]);

        var dump = new[]
        {
            ("General1-Slot1", "Crude Wooden Flute", 1),
            ("General1-Slot2", "Amulet of Woven Hair", 1),
            ("General2-Slot1", "Azure Ring", 1),
            ("General2-Slot2", "Black Silk Cape", 1),
            ("Bank1", "Brass Knuckles", 2),
        };
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, name, count) in dump)
            counts[QuestCatalog.BaseItemName(name)] = counts.GetValueOrDefault(name) + count;
        var inventory = new InventoryFile.Snapshot(
            "eqbuddy-inventory.txt", now.AddMinutes(-4), counts)
        {
            Entries = [.. dump.Select(d => new InventoryFile.Entry(d.Item1, d.Item2, d.Item3))],
        };

        var catalog = QuestCatalog.LoadEmbedded();
        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            AppVersion = UpdateChecker.CurrentVersion.ToString(),
            Offered = [CompanionSurfaces.Quests],
            Stats = new StatsSnapshot { CurrentZone = "Plane of Sky" },
            Settings = settings,
            Quests = new CompanionQuestRequest
            {
                Catalog = catalog,
                Inventory = inventory,
                CharacterClassNames = ["Bard"],
                ClassSource = ClassSource.Achievements,
            },
            QuestIndex = CompanionQuestIndex.Build(catalog),
            Theme = CompanionTheme.Project("ParchmentBrass",
                EQBuddy.UI.Shared.ThemePalettes.For("ParchmentBrass")),
        }, now);

        File.WriteAllText(outPath!, JsonSerializer.Serialize(snap, CompanionSnapshot.JsonOpts));

        // The prediction above, as assertions. A shot whose numbers were not predicted in
        // advance has not been reviewed, and a fixture in the wrong SHAPE renders a state
        // that is real — which looks exactly like a correct screenshot (trap 23).
        var bands = snap.Quests!.Sky.Groups
            .Where(g => !g.Tickable && g.Heading.Contains('—'))
            .ToList();
        Assert.Equal(2, bands.Count);
        Assert.Equal("No longer needed — 2", bands[0].Heading);
        Assert.Equal(["Amulet of Woven Hair ×1 · bags", "Crude Wooden Flute ×1 · bags"],
            bands[0].Rows.Select(r => r.Text));
        Assert.Equal(
            "1 more is still wanted by another quest: Black Silk Cape (Necromancer Epic Quest).",
            bands[0].Note);
        Assert.Equal("Other classes still want — 2", bands[1].Heading);
        Assert.Equal(["Azure Ring ×1 · bags", "Brass Knuckles ×2 · bank"],
            bands[1].Rows.Select(r => r.Text));
    }

    /// <summary>
    /// **The Plane of Sky ISLAND view, for EQBuddy Mobile** (DRA-164 D3).
    ///
    /// <para>The phone reads the PC's own <c>SkyGroupByIsland</c> and calls the same
    /// <c>QuestChecklistLayout.SkyByIsland</c> from the same point — so this fixture is
    /// staged by flipping ONE setting on a shipped checklist, which is exactly what a player
    /// does. Anything else would be a fixture in the wrong shape rendering a state that is
    /// real (trap 23).</para>
    ///
    /// <para>PREDICTION, derived from the shipped <c>SkyQuestDefaults</c> before the run. Nine
    /// groups, ascending, with these counts:</para>
    ///
    /// <code>
    ///   Island 2               0/2      Island 7                0/20
    ///   Island 3               0/16     Island 8                0/16
    ///   Island 4               0/15     Islands 1.5 · 4 · 8     0/22
    ///   Island 5               0/17     Anywhere on the plane   0/94
    ///   Island 6               0/18
    /// </code>
    ///
    /// <para>220 rows, not 222: the Bard's Ervaj's Flute of Flight is turned in, so its two
    /// rows are hidden — one from Island 5 (18→17) and one from "Anywhere" (95→94) — and the
    /// section's note must SAY that one reward is not listed. That is the point of staging a
    /// completed reward rather than a clean profile: an exclusion nobody can see fire is an
    /// exclusion nobody can review.</para>
    ///
    /// <para><b>No ledger, so these are the CLASSIC rows</b> — a fresh profile's state, and the
    /// one where the island fact comes from each step's own <c>Source</c> prose rather than a
    /// guide's stage name. The guided half is covered by <c>SkyIslandPlacementSweepTests</c>
    /// over the real catalog; this frame is about what the PHONE draws.</para>
    ///
    ///     dotnet test --filter WriteSkyIslandSnapshot -e EQBUDDY_SHOOT=1 -e EQBUDDY_SHOOT_SKY_ISLAND=&lt;path&gt;
    ///     pwsh scripts/mobile-harness.ps1 -Snapshot &lt;path&gt; -Screenshot
    /// </summary>
    [Fact]
    public void WriteSkyIslandSnapshot()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;
        var outPath = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_SKY_ISLAND");
        if (string.IsNullOrWhiteSpace(outPath)) return;

        var now = new DateTime(2026, 9, 17, 9, 30, 0);
        var settings = new AppSettings();
        settings.ApplyDefaultSkyQuestChecklist();   // the shipped catalog, not a hand list
        settings.SkyGroupByIsland = true;           // the ONE thing a player flips
        settings.SkyQuestCompleted.Add(
            QuestChecklistLayout.RewardKey("Bard", "Ervaj's Flute of Flight"));

        var catalog = QuestCatalog.LoadEmbedded();
        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            AppVersion = UpdateChecker.CurrentVersion.ToString(),
            Offered = [CompanionSurfaces.Quests],
            Stats = new StatsSnapshot { CurrentZone = "Plane of Sky" },
            Settings = settings,
            Quests = new CompanionQuestRequest
            {
                Catalog = catalog,
                CharacterClassNames = ["Warrior", "Monk", "Druid"],
                ClassSource = ClassSource.Achievements,
            },
            QuestIndex = CompanionQuestIndex.Build(catalog),
            Theme = CompanionTheme.Project("ParchmentBrass",
                EQBuddy.UI.Shared.ThemePalettes.For("ParchmentBrass")),
        }, now);

        File.WriteAllText(outPath!, JsonSerializer.Serialize(snap, CompanionSnapshot.JsonOpts));

        // The prediction above, as assertions. A shot whose numbers were not predicted in
        // advance has not been reviewed (trap 23).
        var islands = snap.Quests!.Sky.Groups.Where(g => g.Tickable).ToList();
        Assert.Equal(
            [("Island 2", "0 of 2"), ("Island 3", "0 of 16"), ("Island 4", "0 of 15"),
             ("Island 5", "0 of 17"), ("Island 6", "0 of 18"), ("Island 7", "0 of 20"),
             ("Island 8", "0 of 16"), ("Islands 1.5 · 4 · 8", "0 of 22"),
             ("Anywhere on the plane", "0 of 94")],
            islands.Select(g => (g.Heading, g.Note)));
        Assert.Equal(220, islands.Sum(g => g.Rows.Count));

        // The note is on the WIRE — the exclusion and the reason the class chips stand down.
        Assert.NotNull(snap.Quests.Sky.Note);
        Assert.Contains(QuestChecklistLayout.SkyIslandCrossClassNote, snap.Quests.Sky.Note);
        Assert.Contains("1 turned-in reward is not listed here", snap.Quests.Sky.Note);

        // And every row still says whose work it is, because no heading does any more.
        Assert.All(islands.SelectMany(g => g.Rows),
            r => Assert.Contains(" · ", r.Detail ?? ""));
    }

    /// <summary>
    /// **The Helper screen, for EQBuddy Mobile** (DRA-71 D9).
    ///
    /// <para>Staged through the REAL projection over a real <c>HelperInputs</c>, so what the
    /// harness renders is what a paired phone renders — a fixture in the wrong shape draws a
    /// state that is real, which looks exactly like a correct screenshot (trap 23). The
    /// numbers below are PREDICTED and then asserted for the same reason.</para>
    ///
    /// <para>Two zones, because D4's throughput comparison has nothing to compare a single
    /// zone against but itself; a faction dump with two standings, so the faction engine has
    /// something to rank and the faction pick has a face; and a deliberately UNKNOWN level, so
    /// the shot carries the disclosure line and its Character door — the one state a reviewer
    /// most needs to see, because it is what a brand-new profile looks like.</para>
    ///
    ///     dotnet test --filter WriteHelperSnapshot -e EQBUDDY_SHOOT=1 -e EQBUDDY_SHOOT_HELPER=&lt;path&gt;
    ///     pwsh scripts/mobile-harness.ps1 -Snapshot &lt;path&gt; -Screenshot
    /// </summary>
    [Fact]
    public void WriteHelperSnapshot()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;
        var outPath = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_HELPER");
        if (string.IsNullOrWhiteSpace(outPath)) return;

        var now = new DateTime(2026, 9, 14, 21, 12, 0);
        IReadOnlyList<SessionRow> sessions =
        [
            new(1, "erollisi", "Dranak", now.AddDays(-6), now.AddDays(-6).AddHours(6),
                6 * 3600, 6 * 3600, "ended", "Lower Guk", 0, 240, 5_000, 0, 1, 0, "", ""),
            new(2, "erollisi", "Dranak", now.AddDays(-3), now.AddDays(-3).AddHours(4),
                4 * 3600, 4 * 3600, "ended", "Befallen", 0, 80, 900, 0, 0, 0, "", ""),
        ];
        IReadOnlyList<MobSummary> pool =
        [
            new("a froglok tad", 300, 300, 28, 0, 0, []) { Zone = "Lower Guk" },
            new("a skeleton", 120, 120, 41, 0, 0, []) { Zone = "Befallen" },
        ];
        var factions = new FactionsFile.Snapshot("eqbuddy-factions.txt", now.AddHours(-2),
            [new FactionsFile.Standing(1, "Guards of Qeynos", 620, 130),
             new FactionsFile.Standing(2, "Merchants of Qeynos", 410, 340)]);

        var inputs = new HelperInputs(
            ZoneHistory.Fold(sessions, pool), pool, factions, ["Guards of Qeynos"],
            [], [], [], false, [], [], null, ResolvedLevel.Unknown);

        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            AppVersion = UpdateChecker.CurrentVersion.ToString(),
            Offered = [CompanionSurfaces.Helper],
            Stats = new StatsSnapshot { CurrentZone = "Lower Guk" },
            Helper = new CompanionHelperRequest(
                inputs, [HelperGoal.LevelUp, HelperGoal.WorkOnFaction], [],
                Tradeskills.All.Count),
            Theme = CompanionTheme.Project("ParchmentBrass",
                EQBuddy.UI.Shared.ThemePalettes.For("ParchmentBrass")),
        }, now);

        File.WriteAllText(outPath!, JsonSerializer.Serialize(snap, CompanionSnapshot.JsonOpts));

        // THE PREDICTION, as assertions (trap 23).
        var helper = snap.Helper!;
        // Two picks: the goals face, and the faction sub-pick its goal turns on. No gear,
        // unlock or profession block, because neither of their goals is picked.
        Assert.Equal(["Your goals", "Work on Faction"],
            helper.Picks.Select(p => p.Heading));
        Assert.Equal("Level Up · Work on Faction", helper.Picks[0].Face);
        Assert.Equal("Guards of Qeynos", helper.Picks[1].Face);
        // Lower Guk first: 40%/hr against Befallen's 20%/hr, and it is where the faction
        // this character picked is NOT moved — so the two answers are a camp and a faction,
        // which is the shape the cross-domain ranking is for.
        Assert.Equal("Lower Guk", helper.Answers[0].Headline);
        Assert.Contains("40.0%/hr here", helper.Answers[0].Why[0].Text);
        // An unknown level draws its own sentence and the door that fixes it — never a guess.
        Assert.Equal(LevelReadout.UsedByHelper(ResolvedLevel.Unknown), helper.LevelNote);
        // Nothing on this screen is a control, and no door on it is a link.
        Assert.False(CompanionSurfaces.AcceptsTicks(CompanionSurfaces.Helper));
    }

    /// <summary>
    /// **The Helper answering FARM GEAR, for EQBuddy Mobile** (DRA-84 D5, plan P6).
    ///
    /// <para>The row above pictures the Helper a new profile meets; this one pictures the
    /// screen the Founder FAILED. It is staged against the <b>REAL shipped catalog</b> rather
    /// than a two-record fixture, because acceptance 1 is precisely that the answers come from
    /// full item knowledge and not from what this session happened to loot — a hand-built
    /// catalog would photograph as a correct screenshot of the thing under test being absent
    /// (trap 23).</para>
    ///
    /// <para><b>The prediction is not mine to invent: it is a number already committed.</b>
    /// This fixture stages the same anchor, the same intent and the same unknown level as the
    /// E2E row <c>AnUpgradeNothingCanNameADropperForIsWithheldAndTheRoomSaysSo</c>, which
    /// asserts the desktop's three zones, its six named creatures and its two withheld counts
    /// against the launched app. So the phone's numbers are PREDICTED from the PC's committed
    /// ones, and a disagreement here is a parity defect rather than a fixture to re-fit —
    /// which is the one thing a second surface's screenshot is uniquely able to find (trap
    /// 4).</para>
    ///
    /// <para><b>What this picture deliberately does NOT carry: the band gate's sentence.</b>
    /// The level is unknown, so the gate stands down (trap 73) and the screen shows its
    /// level disclosure and the Character door instead. The gate's own picture is the
    /// desktop's <c>shell-helper-gear-band</c>, and that the phone says the same words when
    /// it does fire is <c>HelperSurfaceParityTests.ARefusedZoneSaysSoOnThePhoneToo</c>'s
    /// claim, asserted against a fixture that produces a real refusal. Staging a level here
    /// would have re-ranked the zones and left this shot with numbers nothing else had
    /// computed.</para>
    ///
    ///     dotnet test --filter WriteHelperGearSnapshot -e EQBUDDY_SHOOT=1 -e EQBUDDY_SHOOT_HELPER_GEAR=&lt;path&gt;
    ///     pwsh scripts/mobile-harness.ps1 -Snapshot &lt;path&gt; -Screenshot
    ///     msedge --headless=new --disable-gpu --hide-scrollbars --virtual-time-budget=20000 \
    ///            --window-size=516,1500 --screenshot=docs/screenshots/mobile-helper-gear.png \
    ///            dist/mobile-harness/harness.html
    ///
    /// <para><b>The third line is here because it was missing for `mobile-helper.png` and had
    /// to be reconstructed.</b> The harness builds a page; it does not take a picture, so a
    /// recipe that stops at line two is a recipe nobody can re-run (the illustration lock).
    /// 516 is not a choice — headless Edge clamps its CSS viewport at 492 px however small
    /// `--window-size` is (trap 7, measured in DRA-71 D9), so these line breaks are a large
    /// phone's rather than a small one's. 1500 is the height this content needs; at 1060 the
    /// two cap sentences fell below the fold, which is a correct photograph of the evidence
    /// being absent.</para>
    /// </summary>
    [Fact]
    public void WriteHelperGearSnapshot()
    {
        if (Environment.GetEnvironmentVariable("EQBUDDY_SHOOT") != "1") return;
        var outPath = Environment.GetEnvironmentVariable("EQBUDDY_SHOOT_HELPER_GEAR");
        if (string.IsNullOrWhiteSpace(outPath)) return;

        var now = new DateTime(2026, 9, 15, 0, 20, 0);
        var items = ItemCatalog.Default;
        // The anchor the E2E wears, resolved the way the app resolves it — the catalog's own
        // stats for the base name, which is what `GearUpgrades.WornFrom` is handed in
        // production. AC-2 cloth, so the sweep has plenty to beat.
        var gloves = items.Find("Cloth Gloves")
            ?? throw new InvalidOperationException("the shipped catalog has no Cloth Gloves");
        var inputs = HelperInputs.Nothing with
        {
            Worn = [new WornItem("Cloth Gloves", "Cloth Gloves", "HANDS", gloves.ToStatsBlock())],
            Items = items,
            // **The E2E's character is a WARRIOR, and the class-lock filter is an INPUT to the
            // sweep.** The first draft of this fixture left it empty — which filters nothing,
            // and is the honest reading of "EQBuddy has not been told" — and the three zones
            // came back as Plane of Growth / Temple of Veeshan / Chardok. That is a real screen
            // of a different character, which is what trap 23 is about: the prediction caught
            // it, and the fixture moved rather than the number.
            MyClasses = ["WAR"],
            GearIntent = GearIntent.ReplaceSlot,
            // Named for the reason `HelperSources.Gather` names it: a gate that is live on one
            // surface and stood down on the other is trap 4 wearing a fixture (it stands down
            // here anyway, because the level is unknown).
            Bands = ZoneLevels.Default,
        };

        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            AppVersion = UpdateChecker.CurrentVersion.ToString(),
            Offered = [CompanionSurfaces.Helper],
            Stats = new StatsSnapshot { CurrentZone = "Lower Guk" },
            Helper = new CompanionHelperRequest(
                inputs, [HelperGoal.FarmGear], [], Tradeskills.All.Count),
            Theme = CompanionTheme.Project("ParchmentBrass",
                EQBuddy.UI.Shared.ThemePalettes.For("ParchmentBrass")),
        }, now);

        File.WriteAllText(outPath!, JsonSerializer.Serialize(snap, CompanionSnapshot.JsonOpts));

        // THE PREDICTION, as assertions (trap 23) — every number below is the E2E row's.
        var helper = snap.Helper!;
        Assert.Equal(["Temple of Veeshan", "Kael Drakkel", "Dragon Necropolis"],
            helper.Answers.Select(a => a.Headline));

        // Acceptance 2, on the phone: every drawn item line names its creatures. Six lines
        // across the three zones, and the sentence carries the names rather than a field
        // beside them (trap 32).
        // BOTH verbs: the clause agrees with the count ("a bandit drops it" / "a bandit, a
        // hill giant and a ghoul drop it"), and a predicate that knew only the plural counted
        // one of these six.
        var gear = helper.Answers.SelectMany(a => a.Why)
            .Where(w => w.Text.Contains(" drops it", StringComparison.Ordinal)
                        || w.Text.Contains(" drop it", StringComparison.Ordinal)).ToList();
        Assert.Equal(6, gear.Count);
        // Nothing was looted in this fixture, so every one of those creatures is the
        // CATALOG's and carries the estimate label that says so.
        foreach (var line in gear) Assert.False(line.Personal);

        // Both caps say so, in the words the PC uses (trap 50). Five offers the who rule
        // removed — the `Slime Blood of Cazic-Thule` phantom zones — and 89 the sweep's own
        // per-anchor cap held back before the rule ever ran.
        Assert.Equal(HelperPresentation.DropOffersWithheld(5), helper.GearWhoWithheld);
        Assert.Equal(HelperPresentation.GearWithheld(89), helper.GearWithheld);
        // The band gate stood down, and the screen says which number it does not have.
        Assert.Equal("", helper.GearBandRefused);
        Assert.Equal(LevelReadout.UsedByHelper(ResolvedLevel.Unknown), helper.LevelNote);
        // Still read-only, one surface down (trap 35).
        Assert.False(CompanionSurfaces.AcceptsTicks(CompanionSurfaces.Helper));
    }
}
