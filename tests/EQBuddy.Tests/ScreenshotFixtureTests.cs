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

        var snap = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Hugzee",
            AppVersion = "1.80.0",
            Offered = [CompanionSurfaces.Map],
            Stats = new StatsSnapshot { CurrentZone = logZone },
            Map = map,
            Theme = CompanionTheme.Project("midnight",
                EQBuddy.UI.Shared.CustomTheme.PaletteFor(new AppSettings { Theme = "midnight" })),
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
            Offered = [CompanionSurfaces.Progress],
            Raids = raids,
            Level = 12,
            Theme = CompanionTheme.Project("midnight",
                EQBuddy.UI.Shared.CustomTheme.PaletteFor(new AppSettings { Theme = "midnight" })),
        }, now);

        File.WriteAllText(outPath!, JsonSerializer.Serialize(snap, CompanionSnapshot.JsonOpts));

        // Predicted before the run (trap 23): four tabs, the catalog's own boss total, and
        // two clears in two different shapes.
        Assert.Equal(4, snap.Progress!.Tabs.Count);
        Assert.Equal(RaidTargetCatalog.Default.BossCount, snap.Progress.Raids.Total);
        Assert.Equal(2, snap.Progress.Raids.Defeated);
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
}
