using System.Text.Json;
using EQBuddy.Companion;
using EQBuddy.Core;
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
}
