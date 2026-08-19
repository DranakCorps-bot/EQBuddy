using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// Renders the Linux widget without a display server.
///
/// These exist because of a specific failure of process: for a week, changes shipped to the
/// Avalonia UI — themes, a delay box, a guide panel, per-fight sections — verified only by
/// "it compiles and the unit tests pass". Neither of those would have caught a window that
/// draws nothing, a theme that leaves everything the same colour, or a card that throws when
/// it populates. A frame captured here is the cheapest thing that would.
///
/// The widget is deliberately built as the app builds it, so a break in construction shows
/// up here rather than on a user's desktop.
/// </summary>
[Collection("avalonia")]
public class WidgetRenderTests : IDisposable
{
    private readonly string _profile =
        Directory.CreateTempSubdirectory("eqbuddy-render-").FullName;

    /// <summary>"Is this the icon called X?" — comparing to <c>IconPaths.Path(name)</c>
    /// directly does NOT work: Avalonia re-serializes a parsed geometry, so the string
    /// coming back off a PathIcon is a normalized form of the one that went in. Parse the
    /// expected side too and both go through the same normalization.</summary>
    private static Func<PathIcon, bool> IsIcon(string name)
    {
        var expected = StreamGeometry.Parse(IconPaths.Path(name)).ToString();
        return icon => icon.Data?.ToString() == expected;
    }

    public WidgetRenderTests()
    {
        // Isolate settings/history: constructing the widget opens a SQLite history db and
        // reads settings, and a test must not touch the real profile.
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", _profile);
        Environment.SetEnvironmentVariable("EQBUDDY_EXPAND", "1");
        Directory.CreateDirectory(Path.Combine(_profile, "logs"));
        File.WriteAllText(Path.Combine(_profile, "settings.json"),
            $$"""
              { "LogFolder": {{System.Text.Json.JsonSerializer.Serialize(Path.Combine(_profile, "logs"))}},
                "TruncateLogs": false, "ShowTutorial": false, "TrackSpawns": false,
                "LastSeenVersion": {{System.Text.Json.JsonSerializer.Serialize(UpdateChecker.CurrentVersion.ToString())}},
                "Theme": "ParchmentBrass" }
              """);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", null);
        Environment.SetEnvironmentVariable("EQBUDDY_EXPAND", null);
        try { Directory.Delete(_profile, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>The widget builds, shows, and paints something. A window that constructs but
    /// draws nothing is the failure mode "it compiles" can't see.</summary>
    [AvaloniaFact]
    public void TheWidgetRendersAFrame()
    {
        var window = new MainWindow();
        window.Show();

        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        Assert.True(frame!.Size.Width > 100, $"window rendered only {frame.Size.Width}px wide");
        Assert.True(frame.Size.Height > 100, $"window rendered only {frame.Size.Height}px tall");
        window.Close();
    }

    /// <summary>The cards are actually in the visual tree, not just fields on the class.</summary>
    [AvaloniaFact]
    public void TheCardsArePresent()
    {
        var window = new MainWindow();
        window.Show();

        var headings = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();

        Assert.Contains(headings, h => h.Contains("Combat"));
        Assert.Contains(headings, h => h.Contains("Healing"));
        Assert.Contains(headings, h => h.Contains("Kills"));
        window.Close();
    }

    /// <summary>The Quests card exists at all — until it did, the "quests" key sat in the
    /// shared OverlaySections catalog with nothing here to build it, which is what
    /// crashed startup, and then (once guarded) left a dead row in Options. Since
    /// 2026-08-16 it is a launcher rather than two tabbed checklists, so what it owes the
    /// widget is the summary line that took their place.</summary>
    [AvaloniaFact]
    public void TheQuestsCardLeavesForTheTrackerAndSummarisesBothChecklists()
    {
        var window = new MainWindow();
        window.Show();

        window.RenderSnapshotForTest(new StatsSnapshot());
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        // The heading is a vector icon plus a word now (Gate 5): the emoji it replaces is
        // exactly the kind that failed to render at all under Wine (#148, #166) — on this
        // build.
        Assert.Contains("Quests", text);
        Assert.Contains(window.GetVisualDescendants().OfType<global::Avalonia.Controls.PathIcon>(),
            i => i.Data is not null);
        Assert.Contains("↗", text);   // this card leaves rather than unfolds

        // Both checklists are seeded from the embedded catalogs, so the glance the two
        // deleted cards used to give is still on the widget, in one line.
        var epic = window.Settings.EpicQuestChecklist;
        var sky = window.Settings.SkyQuestChecklist;
        Assert.NotEmpty(epic);
        Assert.NotEmpty(sky);
        Assert.Contains($"Epic {epic.Count(i => i.Acquired)}/{epic.Count} · "
            + $"Sky {sky.Count(i => i.Acquired)}/{sky.Count}", text);
        window.Close();
    }

    /// <summary>The Gear card's WHERE-TO-GO pivot (#122abd6) reached this UI: the
    /// toggle has to exist in the tree, or the by-zone view is unreachable here even
    /// though the rollup it draws is shared and tested.</summary>
    [AvaloniaFact]
    public void TheGearCardOffersTheByZonePivot()
    {
        var window = new MainWindow();
        window.Show();

        var checks = window.GetLogicalDescendants().OfType<CheckBox>()
            .Select(c => c.Content as string ?? "").ToList();

        Assert.Contains(checks, c => c.Contains("Group by farm zone"));
        window.Close();
    }

    [AvaloniaFact]
    public void WhatsNewPopupRendersSkippedReleasesAndHighlights()
    {
        var entries = WhatsNewCatalog.EntriesBetween("1.23.1", "1.25.0");
        var window = new WhatsNewWindow(entries);
        window.Show();

        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("What's new since your last version", text);
        Assert.Contains("EQBuddy 1.25.0", text);
        Assert.Contains("EQBuddy 1.24.0", text);
        Assert.Contains(text, t => t.StartsWith("This popup!"));

        window.Close();
    }

    [Fact]
    public void PreFeatureBaselineSelectsOnlyTheCurrentMinorRelease()
    {
        Assert.Equal("1.24.0", MainWindow.PreviousVersionBaseline("1.25.0.0"));
    }

    [AvaloniaFact]
    public void SpawnTrackerRendersTheCatalogAndControls()
    {
        var main = new MainWindow();
        main.Show();
        var catalog = SpawnCatalog.LoadEmbedded();
        var overrides = SpawnOverrides.Load(Path.Combine(_profile, "spawn-test-overrides.json"));
        var timers = new SpawnTimers(catalog, overrides, Path.Combine(_profile, "spawn-test-timers.json"));
        var tracker = new SpawnsWindow(main, new SpawnsViewModel(catalog, overrides, timers));
        tracker.Show(main);

        Assert.NotNull(tracker.CaptureRenderedFrame());
        var text = tracker.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains(text, value => value.Contains("Spawns"));
        Assert.Contains(text, value => value.Contains("countdown starts from the log"));
        Assert.Contains(tracker.GetVisualDescendants().OfType<CheckBox>(),
            check => Equals(check.Content, "Follow"));
        Assert.Contains(tracker.GetVisualDescendants().OfType<ComboBox>(),
            combo => combo.Items.Contains("Custom…") && combo.Items.Contains("Chimes"));

        tracker.Close();
        main.Close();
    }

    [AvaloniaFact]
    public void SpawnTrackerCanHideWithoutDisarmingTrackingAndOpensOnRequestedZone()
    {
        var main = new MainWindow();
        main.Settings.TrackSpawns = true;
        main.Show();
        var catalog = SpawnCatalog.LoadEmbedded();
        var overrides = SpawnOverrides.Load(Path.Combine(_profile, "spawn-lifecycle-overrides.json"));
        var timers = new SpawnTimers(catalog, overrides, Path.Combine(_profile, "spawn-lifecycle-timers.json"));
        var tracker = new SpawnsWindow(main,
            new SpawnsViewModel(catalog, overrides, timers), "Befallen");
        tracker.Show(main);

        // Gate 3 (docs/DesignSystem.md §11.5): the clock emoji in the title is a vector
        // now, and the window grew the column headers the audit asked for — five
        // unlabelled columns of boxes and glyphs was a puzzle the first time and a memory
        // test after that.
        var text = tracker.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Spawns — Befallen", text);
        Assert.Contains("Named", text);
        Assert.Contains("Next spawn", text);
        Assert.Contains("Respawn", text);
        tracker.Close();
        Assert.True(main.Settings.TrackSpawns);

        main.Close();
    }

    [AvaloniaFact]
    public void SpawnCountdownsRenderAsCompactChips()
    {
        var main = new MainWindow();
        main.Show();
        var catalog = SpawnCatalog.LoadEmbedded();
        var overrides = SpawnOverrides.Load(Path.Combine(_profile, "spawn-chip-overrides.json"));
        var timers = new SpawnTimers(catalog, overrides, Path.Combine(_profile, "spawn-chip-timers.json"));
        timers.StartManual("Befallen", "Asaka L`Rei", 210);
        var chips = new SpawnChipsWindow(main, new SpawnsViewModel(catalog, overrides, timers));
        chips.RefreshChips(DateTime.Now);
        chips.Show(main);

        Assert.NotNull(chips.CaptureRenderedFrame());
        var text = chips.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? "").ToList();
        // The name alone — the chip kind's mark is a vector beside it, not a glyph glued
        // to the front of the name (#148, #166: those emoji do not render under Wine, so
        // the three chip kinds were three identical boxes on the platform this build is
        // for).
        Assert.Contains("Asaka L`Rei", text);
        var isTimer = IsIcon("Timer");
        Assert.Contains(chips.GetVisualDescendants().OfType<PathIcon>(), icon => isTimer(icon));
        Assert.Contains(text, value => value.StartsWith("3:"));

        var active = Assert.Single(new SpawnsViewModel(catalog, overrides, timers).Chips(DateTime.Now));
        chips.DismissChip(active);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(chips.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "Asaka L`Rei");

        // The drag flag, not a coordinate delta, is the persistence signal (#117).
        chips.MarkUserMovedForTests();
        chips.Position = new global::Avalonia.PixelPoint(321, 222);
        chips.Close();
        Assert.Equal(321, main.Settings.SpawnChipsLeft);
        Assert.Equal(222, main.Settings.SpawnChipsTop);
        main.Close();
    }

    [AvaloniaFact]
    public void MezTargetsRenderInTheirOwnMovableChipStack()
    {
        var settings = AppSettings.Load();
        var now = new DateTime(2026, 8, 8, 15, 0, 0);
        var mezzes = new[]
        {
            new MezState("an orc centurion", "Mesmerize", "You", now.AddSeconds(-10), now.AddSeconds(20)),
            new MezState("an orc centurion", "Mesmerize", "You", now.AddSeconds(-8), now.AddSeconds(22)),
            new MezState("an orc oracle", "Entrance", "Aenari", now.AddSeconds(-5), null),
        };
        // The clock-source ctor is the only shape left (WPF parity): the stack asks
        // its source at refresh time; BuildChips remains the shared mez builder.
        var chips = new MezChipsWindow(settings, at => MezChipsWindow.BuildChips(mezzes, at));
        chips.RefreshChips(now);
        chips.Show();

        Assert.NotNull(chips.CaptureRenderedFrame());
        var text = chips.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? "").ToList();
        Assert.Contains("an orc centurion (1)", text);
        Assert.Contains("an orc centurion (2)", text);
        Assert.Contains("0:20", text);
        Assert.Contains("an orc oracle", text);
        // The mez mark is the crescent, drawn once per chip.
        Assert.Equal(3, chips.GetVisualDescendants().OfType<PathIcon>().Count(IsIcon("Moon")));
        Assert.Contains("?", text);

        // A USER drag persists; a merely programmatic Position write must not
        // (#117 round two: the grow-up anchor moves the window itself, so the drag
        // flag — not a coordinate delta — is the persistence signal).
        chips.MarkUserMovedForTests();
        chips.Position = new global::Avalonia.PixelPoint(432, 234);
        chips.Close();
        Assert.Equal(432, settings.MezChipsLeft);
        Assert.Equal(234, settings.MezChipsTop);
    }

    [AvaloniaFact]
    public void ItemInfoPopupRendersWikiSectionsAndSourceState()
    {
        var service = new EqlWikiItemService(Path.Combine(_profile, "item-cache"),
            _ => Task.FromResult<string?>(null));
        var window = new ItemInfoWindow(service, new AppSettings());
        window.Render(new ItemLookupResult(new ItemInfo
        {
            Name = "Cloak of Flames",
            StatsLines = ["MAGIC ITEM", "Slot: BACK", "AC: 10"],
            MerchantValue = "5g",
            DropsFrom = [("Nagafen's Lair", ["Lord Nagafen"])],
            Quests = ["A Fiery Favor"],
            WikiUrl = "https://eqlwiki.com/Cloak_of_Flames",
        }, ItemLookupState.Cached, new DateTime(2026, 8, 5)));
        window.Show();

        Assert.NotNull(window.CaptureRenderedFrame());
        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? "").ToList();
        Assert.Contains("Cloak of Flames", text);
        Assert.Contains("CACHED 8/5", text);
        Assert.Contains("MAGIC ITEM", text);
        Assert.Contains("Lord Nagafen — Nagafen's Lair", text);
        Assert.Contains("A Fiery Favor", text);
        Assert.Contains("Open wiki page ↗", text);

        window.Close();
    }

    /// <summary>Applying a snapshot is where a card that mis-formats or dereferences null
    /// blows up — and it's the path every refresh takes.</summary>
    [AvaloniaFact]
    public void ApplyingStatsDoesNotThrow()
    {
        var window = new MainWindow();
        window.Show();

        var stats = new SessionStats { CharacterName = "Testchar" };
        foreach (var line in (string[])
                 [
                     "[Sat Jul 18 15:00:00 2026] You slash orc pawn for 12 points of damage.",
                     "[Sat Jul 18 15:00:02 2026] Orc pawn hits YOU for 4 points of damage.",
                     "[Sat Jul 18 15:00:03 2026] You healed Testchar for 20 hit points by Light Healing.",
                     "[Sat Jul 18 15:00:04 2026] You have slain orc pawn!",
                     "[Sat Jul 18 15:00:05 2026] --You have looted a Mote of Minor Potential from orc pawn's corpse.--",
                 ])
            if (LogParser.Parse(line) is { } evt) stats.Apply(evt);

        var snapshot = stats.Snapshot(null, null);

        // The exception, if any, is the point — this is the call every refresh makes.
        window.RenderSnapshotForTest(snapshot);

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        window.Close();
    }

    [AvaloniaFact]
    public void CombatCardShowsSpellDamageAndCastCompletion()
    {
        var window = new MainWindow();
        window.Show();

        window.RenderSnapshotForTest(new StatsSnapshot
        {
            DotDamage = 1_250,
            DirectSpellDamage = 875,
            CastsStarted = 10,
            CastsInterrupted = 1,
            Fizzles = 2,
            Resists = 3,
        });

        var summary = window.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => t.Text?.StartsWith("Dealt ") == true).Text!;
        Assert.Contains("Your spells: 1,250 over time / 875 direct", summary);
        Assert.Contains("Casts 10 · 70% completed (1 interrupted · 2 fizzled · 3 resisted)", summary);

        window.Close();
    }

    [AvaloniaFact]
    public void AreaSpellsAppearOnlyWhenTheSnapshotContainsThem()
    {
        var window = new MainWindow();
        window.Show();

        window.RenderSnapshotForTest(new StatsSnapshot());
        var heading = window.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => t.Text == "Area spells (per cast)");
        Assert.False(heading.IsVisible);

        window.RenderSnapshotForTest(new StatsSnapshot
        {
            AreaSpells =
            [
                new AreaSpellInfo("Rain of Fire", 3, 2.5, 4, 3600, 1200),
                new AreaSpellInfo("Circle of Flame", 2, 3, 3, 1000, 500),
            ],
        });

        Assert.True(heading.IsVisible);
        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Rain of Fire", text);
        Assert.Contains("1,200/cast - x3 - 2.5 targets (best 4)", text);
        Assert.Contains("500/cast - x2 - 3 targets", text);

        window.Close();
    }

    /// <summary>The fold heading carrying these words. EqFoldLabel is internal and the
    /// test project has InternalsVisibleTo, so the chevron's direction is assertable
    /// directly rather than through the text it no longer lives in.</summary>
    private static EqFoldLabel Fold(global::Avalonia.Visual root, string text) =>
        root.GetVisualDescendants().OfType<EqFoldLabel>().Single(f => f.Text == text);

    [AvaloniaFact]
    public void PendingCueCountsDownInTheTrackedCardHeading()
    {
        var window = new MainWindow();
        window.Show();
        var rule = new TrackedRule { Name = "Respawn", Pattern = "placeholder" };
        window.Settings.TrackedRules.Add(rule);
        var dueAt = DateTime.Now.AddMinutes(8);

        window.RenderSnapshotForTest(new StatsSnapshot
        {
            Tracked =
            [
                new TrackedRuleResult(rule.Name, 1, [], 1, 1,
                    DateTime.Now, DateTime.Now, "placeholder", rule.Id),
            ],
        }, new Dictionary<string, DateTime> { [rule.Id] = dueAt });

        // Gate 5: the hourglass is a Timer VECTOR beside its own countdown now, so the
        // heading is the rule name alone and the countdown is a sibling. Both still go
        // WarnBrush — a cue that counts down in the body ink says nothing.
        var texts = window.GetVisualDescendants().OfType<TextBlock>().ToList();
        var heading = texts.Single(t => t.Text == "RESPAWN");
        Assert.Same(AppTheme.WarnBrush, heading.Foreground);
        var countdown = texts.Single(t => Regex.IsMatch(t.Text ?? "", @"^(7:5\d|8:00)$"));
        Assert.Same(AppTheme.WarnBrush, countdown.Foreground);

        window.Close();
    }

    [AvaloniaFact]
    public void WatchCardLeadsWithLastMatchAndCollapsesMultipleKinds()
    {
        var window = new MainWindow();
        window.Show();
        var rule = new TrackedRule { Name = "Buff fades", Pattern = "placeholder" };
        window.Settings.TrackedRules.Add(rule);

        window.RenderSnapshotForTest(new StatsSnapshot
        {
            Tracked =
            [
                new TrackedRuleResult(rule.Name, 3,
                    [new NameCount("Haste", 2), new NameCount("Echoing Light", 1)],
                    3, 3, DateTime.Now, DateTime.Now.AddSeconds(-5), "Haste", rule.Id),
            ],
        });

        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains(text, t => t.StartsWith("last: Haste · ") && t.EndsWith(" ago"));
        Assert.Contains("all 2 kinds", text);
        Assert.DoesNotContain("Haste   x2", text);

        window.Close();
    }

    [AvaloniaFact]
    public void PetAbilitiesDefaultCollapsedAndExpandFromTheSavedSetting()
    {
        var window = new MainWindow();
        window.Show();
        var snapshot = new StatsSnapshot
        {
            PetAbilities = [new SourceDamage("Slash", 2, 30)],
        };

        window.Settings.ShowPetAbilities = false;
        window.RenderSnapshotForTest(snapshot);
        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Pet abilities (1)", text);
        Assert.DoesNotContain("Slash", text);
        // The open/shut state moved OUT of the text and into the chevron when the "▾"/"▸"
        // glyphs became vectors, so assert it where it now lives — otherwise the
        // conversion silently drops the only coverage of which way the fold points.
        Assert.False(Fold(window, "Pet abilities (1)").Open);

        window.Settings.ShowPetAbilities = true;
        window.RenderSnapshotForTest(snapshot);
        text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Pet abilities", text);
        Assert.Contains("Slash", text);
        Assert.True(Fold(window, "Pet abilities").Open);

        window.Close();
    }

    /// <summary>AA display since the 2026-08-11 rethink: session-new AAs lead, the full
    /// character ledger folds behind the ▸ label (Pet-abilities idiom, WPF parity).
    ///
    /// The surface moved to the Progress window's Experience tab when the PROGRESS THEME
    /// folded five cards into one (docs/Themes.md) — so the assertion moved with it, and
    /// that is the whole point of keeping it: the fold's claim is that the tabs draw what
    /// the cards drew, and this is the only headless place that can check it.</summary>
    [AvaloniaFact]
    public void ProgressCardFoldsTheAaLedgerBehindAToggle()
    {
        var window = new MainWindow();
        window.Show();
        window.ShowProgressWindow("progress");   // Experience
        var snapshot = new StatsSnapshot
        {
            SessionStart = new DateTime(2026, 8, 8),
            AaAbilities =
            [
                new AaAbilityInfo("Spell Casting Mastery", 3, new DateTime(2026, 8, 8, 1, 0, 0)),
                new AaAbilityInfo("Natural Durability", 1, new DateTime(2026, 8, 7)),
            ],
        };

        window.Settings.ShowAllAAs = false;
        window.RenderSnapshotForTest(snapshot);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var text = window.ProgressWindowForTests!.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        // Learned this session: leads unfolded; the pre-session AA stays folded away.
        Assert.Contains("AA learned this session", text);
        Assert.Contains("Spell Casting Mastery", text);
        Assert.Contains("rank 3", text);
        Assert.Contains("All AA abilities (2)", text);
        Assert.DoesNotContain("Natural Durability", text);
        Assert.False(Fold(window.ProgressWindowForTests!, "All AA abilities (2)").Open);

        window.Settings.ShowAllAAs = true;
        window.RenderSnapshotForTest(snapshot);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        text = window.ProgressWindowForTests!.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("All AA abilities", text);
        Assert.Contains("Natural Durability", text);
        Assert.True(Fold(window.ProgressWindowForTests!, "All AA abilities").Open);
        window.ProgressWindowForTests?.Close();
        window.Close();
    }

    /// <summary>The ⏳ Buff set breakout (#120 stage 2): per-class sections with each
    /// pick's honesty state, and the ✕ that removes from THAT bucket only. Without a
    /// named character the whole surface degrades to its honest locked state, so both
    /// halves are asserted here.</summary>
    [AvaloniaFact]
    public void BuffSetBreakoutShowsPerClassSectionsAndRemovesFromOneBucket()
    {
        var main = new MainWindow();
        main.BuffSetIdentityForTests = () => ("tester_p1999", "Tester", ["Shaman"], true);
        main.Show();
        BuffSetStore.Add(main.Settings.BuffSetsByClass, "tester_p1999", "Shaman", "Spirit of Wolf");
        BuffSetStore.Add(main.Settings.BuffSetsByClass, "tester_p1999", BuffSetStore.AnyClass, "Strength");

        var window = new BreakoutWindow(main.Settings, BreakoutKind.Buffs) { BuffHost = main };
        window.Show();
        window.Update(main.CurrentSnapshot());
        Dispatcher.UIThread.RunJobs();

        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Buff set", text);
        Assert.Contains("Spirit of Wolf", text);
        Assert.Contains("Strength", text);
        // The class combination is named, and says it was picked rather than inferred.
        Assert.Contains(text, t => t.StartsWith("Tester ·") && !t.Contains("inferred"));
        // Neither has landed this session, so both read as the honest "not seen" state
        // rather than being claimed active.
        Assert.Contains("not seen", text);

        // ✕ on the Shaman row takes it out of that bucket only — (any class) survives.
        var remove = window.GetVisualDescendants().OfType<Button>()
            .First(b => ToolTip.GetTip(b) is string tip && tip == "Remove Spirit of Wolf from Shaman");
        remove.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var stored = main.Settings.BuffSetsByClass["tester_p1999"];
        Assert.DoesNotContain("Spirit of Wolf", BuffSetStore.SpellsFor(stored, "Shaman"));
        Assert.Contains("Strength", BuffSetStore.SpellsFor(stored, BuffSetStore.AnyClass));
        window.Close();
        main.Close();
    }

    /// <summary>No character named yet: the set can't be keyed, so the window says so
    /// and locks its editor instead of showing an empty set that looks configurable.</summary>
    [AvaloniaFact]
    public void BuffSetBreakoutLocksItselfUntilTheLogNamesACharacter()
    {
        var main = new MainWindow();
        main.Show();
        var window = new BreakoutWindow(main.Settings, BreakoutKind.Buffs) { BuffHost = main };
        window.Show();
        window.Update(main.CurrentSnapshot());
        Dispatcher.UIThread.RunJobs();

        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("No character detected yet", text);
        var addBox = window.GetVisualDescendants().OfType<TextBox>()
            .Single(b => b.Watermark == "add a buff…");
        Assert.False(addBox.IsEnabled);
        window.Close();
        main.Close();
    }

    [AvaloniaFact]
    public void DamageBreakoutRendersFightAbilityBars()
    {
        var settings = AppSettings.Load();
        var window = new BreakoutWindow(settings, BreakoutKind.Damage);
        window.Update(new StatsSnapshot
        {
            LastFight = new LastFightInfo("a froglok", 10, 150, 8, 0, 15, 0,
                "slain", false,
                [new SourceDamage("Backstab", 2, 100), new SourceDamage("Slash", 5, 50)],
                [], []),
        });
        window.Show();

        Assert.NotNull(window.CaptureRenderedFrame());
        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Your damage", text);
        Assert.Contains("Backstab", text);
        // BreakdownRows layout: "100" is the semibold headline, the columns read dim beside it.
        Assert.Contains("100", text);
        Assert.Contains("×2 · avg 50 · 10 dps", text);
        window.Close();
    }

    [AvaloniaFact]
    public void FeedbackWindowExplainsThatGitHubReviewsTheDraft()
    {
        var window = new FeedbackWindow();
        window.Show();

        Assert.NotNull(window.CaptureRenderedFrame());
        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("💡 Feature request", text);
        Assert.Contains("🐛 Bug report", text);
        Assert.Contains(text, t => t.Contains("nothing is sent from the app"));
        window.Close();
    }

    /// <summary>The KPI strip (2026-08-11 modernization): the headline numbers are
    /// always painted, before any card opens.</summary>
    [AvaloniaFact]
    public void KpiStripShowsTheHeadlineNumbers()
    {
        var window = new MainWindow();
        window.Show();

        window.RenderSnapshotForTest(new StatsSnapshot
        {
            CurrentDps = 42, YourKillCount = 7, LootTotal = 3, XpPerHour = 12.5,
        });

        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("XP/HR", text);   // the strip's captions (SectionLabel uppercases)
        Assert.Contains("42", text);      // current DPS leads while fighting
        Assert.Contains("7", text);
        Assert.Contains("12.5%", text);
        window.Close();
    }

    /// <summary>Buffs stays where Options put it, with an honest empty state instead of
    /// vanishing (David's 1.66.2 verdict), and Raids says the same thing from the Progress
    /// window's Raids tab — where it went when the PROGRESS THEME folded five cards into
    /// one (docs/Themes.md).
    ///
    /// Raids is checked through the WINDOW rather than deleted from this test, because the
    /// empty state is the thing most easily lost in a move: it is what a fresh character
    /// sees, and the rule that a card never hides itself is exactly what a re-host
    /// forgets.</summary>
    [AvaloniaFact]
    public void BuffsAndRaidsShowHonestEmptyStates()
    {
        var window = new MainWindow();
        window.Show();

        window.RenderSnapshotForTest(new StatsSnapshot());

        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Buffs", text);   // vector icon + word since Gate 5
        Assert.Contains(text, t => t.StartsWith("Nothing running"));
        // The launcher that replaced the five cards, on the widget itself.
        Assert.Contains("Progress", text);

        window.ShowProgressWindow("raids");
        var raids = window.ProgressWindowForTests!.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Raids", raids);
        Assert.Contains(raids, t => t.StartsWith("Nothing defeated yet"));
        // Close the SATELLITE first. A Progress window left open outlives its test — it
        // is a separate top-level window, and MainWindow.Close() does not take it with
        // it — so a later test's Dispatcher.RunJobs() renders a window nobody owns, at
        // whatever size it happens to have. That surfaces as headless Avalonia's
        // "Size should be >= (1,1)" in an UNRELATED test, which is a miserable thing to
        // debug from a CI log.
        window.ProgressWindowForTests?.Close();
        window.Close();
    }

    /// <summary>The PROGRESS THEME's strip: four tabs, in Core's order, with Experience
    /// selected by default. Built from ProgressSurface so this build, the WPF window and
    /// EQBuddy Mobile cannot end up offering different tabs (#184, #210).</summary>
    [AvaloniaFact]
    public void ProgressWindowShowsTheFourThemeTabs()
    {
        var window = new MainWindow();
        window.Show();
        window.ShowProgressWindow();

        var text = window.ProgressWindowForTests!.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        foreach (var tab in Enum.GetValues<ProgressTab>())
            Assert.Contains(ProgressSurface.LabelFor(tab), text);
        Assert.Equal(ProgressTab.Experience, window.ProgressWindowForTests!.Tab);
        // Close the SATELLITE first. A Progress window left open outlives its test — it
        // is a separate top-level window, and MainWindow.Close() does not take it with
        // it — so a later test's Dispatcher.RunJobs() renders a window nobody owns, at
        // whatever size it happens to have. That surfaces as headless Avalonia's
        // "Size should be >= (1,1)" in an UNRELATED test, which is a miserable thing to
        // debug from a CI log.
        window.ProgressWindowForTests?.Close();
        window.Close();
    }

    /// <summary>A theme swap has to change what's on screen. Mutating brushes in place is
    /// clever but invisible to a compiler: this is the check that it actually repaints.</summary>
    [AvaloniaFact]
    public void SwitchingThemeChangesTheColours()
    {
        // Set the starting theme rather than reading whatever the last test left: AppTheme's
        // brushes are process-wide singletons, so an ambient starting point makes this test
        // depend on execution order.
        AppTheme.Apply("ParchmentBrass");
        var parchment = AppTheme.BgBrush.Color;
        AppTheme.Apply("Solarized");
        var light = AppTheme.BgBrush.Color;
        AppTheme.Apply("SolarizedDark");
        var dark = AppTheme.BgBrush.Color;

        Assert.NotEqual(parchment, light);
        Assert.NotEqual(light, dark);

        AppTheme.Apply("ParchmentBrass");
        Assert.Equal(parchment, AppTheme.BgBrush.Color);   // and back again
    }

    /// <summary>Every catalogued theme has to produce a paintable palette on this platform
    /// too — a hex the shared table accepts but Avalonia's parser rejects would only show up
    /// at runtime, on Linux, for whoever picked that theme.</summary>
    [AvaloniaFact]
    public void EveryThemeApplies()
    {
        foreach (var (key, _) in ThemeCatalog.Themes)
        {
            AppTheme.Apply(key);
            Assert.NotEqual(default, AppTheme.BgBrush.Color);
            Assert.NotEqual(default, AppTheme.TextBrush.Color);
        }
        AppTheme.Apply("ParchmentBrass");
    }

    /// <summary>The toggleAll hotkey's restore loop must skip a window that closed while
    /// hidden (a chip stack whose timers ran out, a tracker torn down by its owner) —
    /// Avalonia throws "Cannot re-show a closed window" where WPF just checked IsLoaded.</summary>
    [AvaloniaFact]
    public void HotkeyRestoreSurvivesAWindowClosedWhileHidden()
    {
        var main = new MainWindow();
        main.Show();
        var satellite = new Window { Width = 120, Height = 60 };
        satellite.Show();
        // Headless has no desktop lifetime, so the capture list comes from the seam.
        main.WindowEnumeratorForTests = () => [main, satellite];

        main.HandleHotkeyAction("toggleAll");
        Assert.False(satellite.IsVisible);   // proves the hide pass actually captured it
        satellite.Close();                   // closes while hidden

        main.HandleHotkeyAction("toggleAll");   // restore must not throw on the corpse
        Assert.True(main.IsVisible);
        main.Close();
    }

    // ---- the title-bar CPU/memory readout must not resize the window (#173) ----

    private static TextBlock PerfLabel(MainWindow main) =>
        main.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => ToolTip.GetTip(t) is string tip && tip.Contains("own CPU (all cores)"));

    /// <summary>
    /// #173 (KoboldCoterie, CachyOS): turning the readout on took EverQuest's keyboard away.
    ///
    /// The widget is SizeToContent and the readout sits in an Auto column of the title bar,
    /// so before the fix each new sample re-measured the text and asked the windowing system
    /// to resize the window — every three seconds, forever, on an always-on-top transparent
    /// window over a fullscreen X11 game. Nothing headless can see that symptom, so this
    /// asserts the property that makes the mechanism impossible: a new sample must not change
    /// the readout's measured size, whatever the digits do.
    ///
    /// It is measured on the label rather than the window because the widget is only as
    /// narrow as its title bar in the minimized pill — with cards open the cards are wider
    /// and would hide the regression.
    /// </summary>
    [AvaloniaFact]
    public void TheCpuReadoutNeverChangesItsMeasuredWidth()
    {
        var main = new MainWindow();
        main.Show();
        var label = PerfLabel(main);
        label.IsVisible = true;

        var arranged = new SortedSet<double>();
        var desired = new SortedSet<double>();
        foreach (var (cpu, mb) in new (double, long)[]
                 { (0, 40), (9.9, 99), (10, 100), (99.9, 999), (100, 4096) })
        {
            label.Text = PerfReadout.Format(cpu, mb * 1024 * 1024);
            main.UpdateLayout();
            arranged.Add(label.Bounds.Width);
            desired.Add(label.DesiredSize.Width);
        }

        Assert.True(arranged.Count == 1 && desired.Count == 1,
            "the readout re-measures per sample, so every sample resizes the window: "
            + string.Join(", ", arranged));
        main.Close();
    }

    /// <summary>
    /// The title bar stays ONE LINE, whatever the character is called and whether or not
    /// the perf readout is reserving width beside it.
    ///
    /// KoboldCoterie's screenshot on #173 (2026-08-16): reserving width for the readout
    /// starved the character label's star column, and because AppTheme.DimText wraps by
    /// default — and a wrapping TextBlock in a star column has no natural minimum width
    /// under SizeToContent — the name stood up vertically, one letter per line, in a
    /// 152px-tall title bar. The ellipsis already on that label could not prevent it:
    /// wrapping wins over trimming.
    /// </summary>
    [AvaloniaFact]
    public void TheTitleBarStaysOneLineWhateverTheCharacterIsCalled()
    {
        var main = new MainWindow();
        main.Show();
        PerfLabel(main).IsVisible = true;
        // By its tooltip, like PerfLabel above — the text is whatever character the
        // profile last followed, so matching on the placeholder is not reliable.
        var name = main.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => ToolTip.GetTip(t) is string tip && tip.Contains("Follows whoever"));

        // A name long enough that a wrapping label would fold it — his was "Kobold (neriak)".
        name.Text = "Kobold (neriak)";
        main.UpdateLayout();
        var oneLine = name.DesiredSize.Height;

        name.Text = "Kobold (neriak) the Extraordinarily Long Nameplate of Wrapping";
        main.UpdateLayout();

        Assert.Equal(oneLine, name.DesiredSize.Height);
        Assert.Equal(TextWrapping.NoWrap, name.TextWrapping);
        main.Close();
    }

    /// <summary>Off by default and collapsed without leaving a gap (#112) — the reserved
    /// width must not become a permanent hole in the title bar.</summary>
    [AvaloniaFact]
    public void TheCpuReadoutIsOffAndCostsNothingUntilItIsTurnedOn()
    {
        var main = new MainWindow();
        main.Show();
        main.UpdateLayout();
        var label = PerfLabel(main);

        Assert.False(main.Settings.ShowPerfStats);
        Assert.False(label.IsVisible);
        Assert.Equal(0, label.Bounds.Width);
        main.Close();
    }
}
