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
    /// though the rollup it draws is shared and tested.
    ///
    /// It is a TAB now — the GEAR &amp; LOOT theme (docs/Themes.md) — so the window has to
    /// be open for the surface to exist at all. Same assertion, new host, which is the
    /// whole point of having had it before the fold: this suite is the only cover this
    /// build has for that surface, and it caught the fold's first attempt (an empty
    /// collection) when the window existed on Windows and not here.</summary>
    [AvaloniaFact]
    public void TheGearCardOffersTheByZonePivot()
    {
        var window = new MainWindow();
        window.Show();
        window.ShowGearLootWindow("gear");

        var checks = window.GearLootWindowForTests!.GetLogicalDescendants().OfType<CheckBox>()
            .Select(c => c.Content as string ?? "").ToList();

        Assert.Contains(checks, c => c.Contains("Group by farm zone"));
        window.GearLootWindowForTests?.Close();
        window.Close();
    }

    /// <summary>EVERY tab this window offers can actually be opened.
    ///
    /// Written because it wasn't. <c>LootSurface.Hosted</c> is shared vocabulary and gained
    /// Inventory on 2026-08-20 when the Gear Locker folded into a tab ON WINDOWS; this
    /// build had no body for it, and the body lookup is a dictionary, so clicking that chip
    /// threw KeyNotFoundException. The strip rendered perfectly right up until the click,
    /// and no existing test clicked it — they assert which LABELS appear, which is exactly
    /// the assertion that cannot see this.
    ///
    /// So: select every offered tab in turn and demand a body. That is the assertion that
    /// scales, because it fails the day a shared catalog grows a member this UI has not
    /// implemented — the same shape as the ApplySectionLayout startup crash.</summary>
    [AvaloniaFact]
    public void EveryTabTheWindowOffersCanBeOpened()
    {
        var window = new MainWindow();
        window.Show();
        window.ShowGearLootWindow();
        var host = window.GearLootWindowForTests!;

        foreach (var tab in LootSurface.Hosted)
        {
            var offered = window.OfferedLootTabsForTests.Any(h => h.Tab == tab);
            if (!offered) continue;   // not implemented here yet is fine; crashing is not
            window.ShowGearLootWindow(LootSurface.KeyFor(tab));
            Dispatcher.UIThread.RunJobs();
        }

        // And since 2026-08-21 the answer is ALL of them: the Inventory tab landed here and
        // the 1.98.1 parity gap is closed. The loop above tolerates a missing tab on
        // purpose — that tolerance is what let this build ship one release behind without
        // crashing — so the closing of the gap needs its own assertion, or "not implemented
        // yet" quietly becomes permanent.
        Assert.Equal(LootSurface.Hosted,
            window.OfferedLootTabsForTests.Select(h => h.Tab).ToList());
        host.Close();
        window.Close();
    }

    /// <summary>The gear checklist auto-ticks from the game's own inventory dump, and for
    /// as long as the surface has existed it never said so and offered no way to produce
    /// one — David, 2026-08-20: <i>"telling me to import it but not telling me how or
    /// giving me the tool with which to do it."</i>
    ///
    /// Asserted on the EMPTY profile deliberately. That is the state a new player meets,
    /// it is the state the complaint was about, and the control is built outside
    /// <c>RenderGearChecklist</c> precisely so no state can lose it — which is a claim only
    /// a test that looks at the empty tree can make. This build has no E2E suite, so this
    /// is the only cover the affordance has here (CLAUDE.md: pin it before it moves — the
    /// gear checklist is the named candidate for the next lift out of MainWindow).</summary>
    [AvaloniaFact]
    public void TheGearCardHandsOverTheInventoryCommand()
    {
        var window = new MainWindow();
        window.Show();
        window.ShowGearLootWindow("gear");

        var text = string.Join("\n", window.GearLootWindowForTests!.GetLogicalDescendants()
            .OfType<TextBlock>().Select(t => t.Text ?? ""));
        var buttons = window.GearLootWindowForTests!.GetLogicalDescendants()
            .OfType<Button>().Select(b => b.Content as string ?? "").ToList();

        // The route out of the empty state — both halves, because they are two different
        // imports and naming one is what made the old sentence useless.
        Assert.Contains(GearChecklistPresentation.EmptyRoute, text);
        Assert.Contains(GearChecklistPresentation.AutoTickNote, text);
        // And the command itself, one click away, from GameCommands and not a literal.
        Assert.Contains(buttons, b => b.Contains(GameCommands.OutputfileInventory));

        window.GearLootWindowForTests?.Close();
        window.Close();
    }


    /// <summary>Two wishes, imported. Written BEFORE the gear checklist was lifted out of
    /// <c>MainWindow.cs</c>, because CLAUDE.md's rule for a lift is to pin the behaviour
    /// first and this build has no E2E suite to pin it in — the three pins that already
    /// existed cover the pivot's existence, the command hand-over and the empty state, and
    /// none of them had ever seen a POPULATED checklist draw a single row.
    ///
    /// So this is the populated state, asserted through the shared presentation rather
    /// than against spelled-out strings: the group headings come from
    /// <see cref="GearChecklistPresentation.BuildGroups"/> and the count line from
    /// <see cref="GearChecklistPresentation.ListName"/>, so the lift is free to move the
    /// drawing and is not free to change what is drawn.</summary>
    [AvaloniaFact]
    public void TheGearChecklistDrawsItsGroupsRowsAndCount()
    {
        var window = new MainWindow();
        window.Show();
        window.Settings.GearChecklistName = "Cleric 50 shopping list";
        window.Settings.GearChecklist =
        [
            new GearChecklistItem { Slot = "Feet", Item = "Golden Efreeti Boots", Acquired = true },
            new GearChecklistItem { Slot = "Head", Item = "Circlet of Shadow", Source = "a shadow man" },
        ];
        window.ShowGearLootWindow("gear");
        Dispatcher.UIThread.RunJobs();

        var text = GearText(window);
        Assert.Contains(GearChecklistPresentation.ListName(
            window.Settings.GearChecklistName, window.Settings.GearChecklist), text);
        foreach (var group in GearChecklistPresentation.BuildGroups(window.Settings.GearChecklist))
            Assert.Contains(group.Heading, text);
        // Both wishes, the acquired one included — by KIND is the list as a list.
        Assert.Contains("Golden Efreeti Boots", text);
        Assert.Contains("Circlet of Shadow", text);
        Assert.Contains("a shadow man", text);   // provenance rides the row
        // One tickable row per wish, and the ticked one reads as ticked.
        Assert.Equal(2, GearRows(window).Count);
        Assert.True(RowFor(window, "Golden Efreeti Boots").IsChecked);
        Assert.False(RowFor(window, "Circlet of Shadow").IsChecked);

        window.GearLootWindowForTests?.Close();
        window.Close();
    }

    /// <summary>Ticking a row is the only thing this surface DOES, and it has to survive
    /// the lift with the count line following it — the count is what the tab badge reads,
    /// so a tick that updated the model and not the line would be the "one entry, two
    /// sources for one fact" trap on a one-second delay.</summary>
    [AvaloniaFact]
    public void TickingAGearRowMarksItAcquiredAndUpdatesTheCount()
    {
        var window = new MainWindow();
        window.Show();
        var wish = new GearChecklistItem { Slot = "Head", Item = "Circlet of Shadow" };
        window.Settings.GearChecklistName = "Cleric 50 shopping list";
        window.Settings.GearChecklist = [wish];
        window.ShowGearLootWindow("gear");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(GearChecklistPresentation.ListName(
            window.Settings.GearChecklistName, window.Settings.GearChecklist), GearText(window));

        RowFor(window, "Circlet of Shadow").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(wish.Acquired);
        // "1/1" now, from the same formatter — asserted through it so the two hosts and
        // the phone cannot end up with three answers.
        Assert.Contains(GearChecklistPresentation.ListName(
            window.Settings.GearChecklistName, window.Settings.GearChecklist), GearText(window));
        Assert.Equal(LootTheme.Gear(window.Settings.GearChecklist),
            window.OfferedLootTabsForTests.Single(h => h.Tab == LootTab.Gear).Value);

        window.GearLootWindowForTests?.Close();
        window.Close();
    }

    /// <summary>The by-zone pivot, DRAWING rather than merely existing. The pin that was
    /// already here asserts the toggle is in the tree; this one flips it and demands the
    /// rows change, which is the half a lift can silently drop.
    ///
    /// The two views are not symmetrical and that asymmetry is the assertion: by zone
    /// EXCLUDES what you already own, because its question is "where do I go next".
    /// Invented item names deliberately — the catalog cannot place them, so they land in
    /// <see cref="GearFarmRollup.NoDataHeading"/> and the test does not quietly become a
    /// test of the shipped catalog.</summary>
    [AvaloniaFact]
    public void TheByZonePivotRedrawsTheChecklistByFarmZone()
    {
        var window = new MainWindow();
        window.Show();
        window.Settings.GearChecklist =
        [
            new GearChecklistItem { Slot = "Feet", Item = "Zzyzx Boots of Testing", Acquired = true },
            new GearChecklistItem { Slot = "Head", Item = "Zzyzx Circlet of Testing" },
        ];
        window.ShowGearLootWindow("gear");
        Dispatcher.UIThread.RunJobs();

        var pivot = window.GearLootWindowForTests!.GetLogicalDescendants().OfType<CheckBox>()
            .Single(c => (c.Content as string ?? "").Contains("Group by farm zone"));
        pivot.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.Settings.GearGroupByZone);
        var text = GearText(window);
        Assert.Contains(GearFarmRollup.NoDataHeading, text);
        Assert.Contains("Zzyzx Circlet of Testing", text);
        // The acquired wish is gone: this view answers "what is left", not "what I own".
        Assert.DoesNotContain("Zzyzx Boots of Testing", text);
        Assert.Single(GearRows(window));

        // And back, without a restart — the same toggle owns both directions.
        pivot.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.Settings.GearGroupByZone);
        Assert.Contains("Zzyzx Boots of Testing", GearText(window));

        window.GearLootWindowForTests?.Close();
        window.Close();
    }

    /// <summary>Every checkbox on the Gear tab that is a WISH — the pivot toggle is a
    /// control of the surface, not a row of it, and counting it would put every row
    /// assertion above out by one. Rows are told apart by carrying a built CONTENT rather
    /// than a string, which is also the thing the lift must not change.</summary>
    private static List<CheckBox> GearRows(MainWindow window) =>
        [.. window.GearLootWindowForTests!.GetLogicalDescendants().OfType<CheckBox>()
            .Where(c => c.Content is Control)];

    private static CheckBox RowFor(MainWindow window, string item) =>
        GearRows(window).Single(c => RowText(c).Contains(item));

    private static string RowText(CheckBox row) =>
        string.Join("\n", ((Control)row.Content!).GetLogicalDescendants()
            .OfType<TextBlock>().Select(TextOf));

    /// <summary>Everything the Gear tab has on screen, as one string. Inlines included:
    /// an exaltation's effect rides its name as a second Run, so a plain Text read would
    /// miss half of the row it is asserting about.</summary>
    private static string GearText(MainWindow window) =>
        string.Join("\n", window.GearLootWindowForTests!.GetLogicalDescendants()
            .OfType<TextBlock>().Select(TextOf));

    private static string TextOf(TextBlock t) =>
        t.Text is { Length: > 0 } s ? s : t.Inlines?.Text ?? "";
    /// <summary>The KILLS &amp; DROPS theme reached this build in the SAME change as its
    /// WPF twin, which is not a preference: the theme switches on in shared vocabulary
    /// (<see cref="CreatureSurface"/> and the Options catalog), so a fold that landed on
    /// Windows alone would take the Kills card off this widget with nowhere for it to go.
    /// That is the 1.98.1 Inventory gap running the other way.
    ///
    /// Both tabs are selected and both are demanded to have a body — the assertion
    /// EveryTabTheWindowOffersCanBeOpened was written for, applied to the new theme from
    /// its first commit rather than after a KeyNotFoundException taught it.</summary>
    [AvaloniaFact]
    public void TheKillsAndDropsWindowOffersBothTabsAndDrawsThem()
    {
        var window = new MainWindow();
        window.Show();
        window.RenderSnapshotForTest(new StatsSnapshot
        {
            YourKillCount = 12,
            Mobs = [new MobSummary("a moss snake", 2, 2, 10, 1.0, 0,
                [new MobLoot("Snake Fang", 1, 50.0)])],
        });
        Dispatcher.UIThread.RunJobs();

        // The widget keeps a DOOR, and the line behind it carries what the card header
        // carried plus the rate — a fold chooses which numbers survive, it does not get to
        // lose one quietly (#219).
        var widget = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Kills & Drops", widget);
        Assert.Contains("↗", widget);   // this card leaves rather than unfolds

        window.ShowCreatureWindow();
        var host = window.CreatureWindowForTests!;
        foreach (var tab in CreatureSurface.Hosted)
        {
            window.ShowCreatureWindow(CreatureSurface.KeyFor(tab));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(CreatureSurface.KeyFor(tab), CreatureSurface.KeyFor(host.Tab));
            Assert.NotEmpty(host.GetLogicalDescendants().OfType<TextBlock>());
        }
        // Every hosted tab is offered — no "not implemented here yet" hole to grow into.
        // What the tabs SAY is asserted through the shared vocabulary, and what each one
        // DRAWS is asserted by DropsRenderTests and by the Kills card's own rows: this
        // window reads CurrentSnapshot() from the live widget rather than a seeded one, so
        // a row assertion here would be a test of the empty session, not of the fold.
        var tabs = host.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        foreach (var tab in CreatureSurface.Hosted)
            Assert.Contains(CreatureSurface.LabelFor(tab), tabs);
        Assert.Equal(CreatureSurface.Hosted.Count, window.OfferedCreatureTabsForTests.Count);

        host.Close();
        window.Close();
    }

    /// <summary>The launcher that replaced the two cards. It has one line to carry what
    /// both card headers carried, and the tab strip beside it has to name both surfaces —
    /// #219 is the release where a fold trimmed a number out of a summary line and the
    /// player who used it turned up within the hour.</summary>
    [AvaloniaFact]
    public void TheGearAndLootCardLeavesForItsWindowAndSummarisesBoth()
    {
        var window = new MainWindow();
        window.Show();
        window.Settings.GearChecklist =
        [
            new GearChecklistItem { Slot = "Feet", Item = "Golden Efreeti Boots", Acquired = true },
            new GearChecklistItem { Slot = "Head", Item = "Circlet of Shadow" },
        ];

        var snapshot = new StatsSnapshot
        {
            Loot = [new LootDetail("Rusty Dagger", 3, "a gnoll")],
            Crafted = [new NameCount("Iron Ration", 1)],
        };
        window.RenderSnapshotForTest(snapshot);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Gear & Loot", text);
        Assert.Contains("↗", text);   // this card leaves rather than unfolds
        // The exact line the shared formatter produces — asserted through it rather than
        // spelled again here, so the two windows and the phone cannot drift apart.
        Assert.Contains(LootTheme.LauncherSummary(snapshot, window.Settings.GearChecklist), text);

        // And the window names both tabs, with the badges the two headers used to carry.
        window.ShowGearLootWindow();
        var tabs = window.GearLootWindowForTests!.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Loot", tabs);
        // "Wishlist" since 2026-08-20 — the tab held a wishlist and was labelled as though
        // it held your gear, one tab away from the surface that will.
        Assert.Contains(LootSurface.LabelFor(LootTab.Gear), tabs);
        Assert.Contains("1/2", tabs);
        window.GearLootWindowForTests?.Close();
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

    /// <summary>
    /// **The guarantee PR A added, and the one no test could reach before it.**
    ///
    /// The two reopen tests below pass on `main` as it was — but they passed by MITIGATION:
    /// a closed window's presentation source is cleared, so the re-parent checked against
    /// null. Nothing stopped two LIVE hosts colliding, and the first thing that tried it
    /// (the inline theme card) threw immediately.
    ///
    /// Now every host builds its own set, so the question changes from "does the move
    /// survive?" to "is there anything to move?" — and the answer is no. Two sets share no
    /// control, and both can be mounted at once, which is what PR B needs.
    /// </summary>
    [AvaloniaFact]
    public void EveryHostGetsItsOwnProgressSurfacesAndTwoCanLiveAtOnce()
    {
        var window = new MainWindow();
        window.Show();

        var first = window.NewProgressSurfaces();
        var second = window.NewProgressSurfaces();

        // Not the same objects, and — the half that matters — not the same CONTROLS.
        Assert.NotSame(first.Experience, second.Experience);
        Assert.NotSame(first.Experience.Body, second.Experience.Body);
        Assert.NotSame(first.Money.Body, second.Money.Body);
        Assert.NotSame(first.Motes.Body, second.Motes.Body);
        Assert.NotSame(first.Faction.Body, second.Faction.Body);
        Assert.NotSame(first.Raids.Body, second.Raids.Body);

        // Both mounted, both live, both painted — the collision that used to throw.
        var snapshot = new StatsSnapshot { SessionStart = new DateTime(2026, 8, 8) };
        var a = new Window { Content = first.Experience.Body, Width = 400, Height = 300 };
        var b = new Window { Content = second.Experience.Body, Width = 400, Height = 300 };
        a.Show();
        b.Show();
        first.Experience.Render(snapshot);
        second.Experience.Render(snapshot);
        Dispatcher.UIThread.RunJobs();

        a.Close();
        b.Close();
        window.Close();
    }

    /// <summary>
    /// **The negative that makes the rule a fact rather than a belief** (trap 39: every
    /// equality assertion deserves one negative).
    ///
    /// PR A rests entirely on the claim that a control cannot be shown by two live windows
    /// on this toolkit. Everything else — the factory, the per-host sets, the deleted
    /// accessor — is downstream of it. So the claim is EXERCISED here rather than cited.
    ///
    /// **Be precise about which mechanism this catches, because it is not the one from the
    /// production crash.** Asking a control to live in a second root while the first still
    /// holds it throws `InvalidOperationException` from Avalonia's visual-parent guard —
    /// immediate, unambiguous, and what this test asserts. The crash that reached players
    /// was the subtler sibling: a RE-PARENT that passes the parent guard and then throws
    /// `ArgumentException: Attempt to call InvalidateArrange on wrong LayoutManager` on the
    /// next layout pass (avalonia#12753/#17906/#21267). Two mechanisms, one conclusion —
    /// **there is no supported way to share a control between live hosts** — and the honest
    /// thing is to say which one a green test actually proves.
    ///
    /// **If this ever fails, that is a decision to make, not a test to fix:** it means the
    /// toolkit changed under us and someone should read the release notes before relaxing
    /// anything. A rule that outlives its reason becomes folklore.
    /// </summary>
    [AvaloniaFact]
    public void AControlCannotBeShownByTwoLiveWindows()
    {
        var shared = new TextBlock { Text = "one control, two roots" };
        var a = new Window { Content = shared, Width = 300, Height = 200 };
        a.Show();
        Dispatcher.UIThread.RunJobs();

        // The move: same control, second live root.
        a.Content = null;
        var b = new Window { Content = shared, Width = 300, Height = 200 };
        b.Show();

        var moved = Record.Exception(() =>
        {
            a.Content = shared;   // back to the first, while the second still holds it
            Dispatcher.UIThread.RunJobs();
        });

        a.Close();
        b.Close();

        Assert.NotNull(moved);
        Assert.IsType<InvalidOperationException>(moved);
        Assert.Contains("TextBlock", moved.Message);
    }

    /// <summary>
    /// STEP 0 of Fable's Avalonia-seam plan: does `main` ALREADY do a cross-window move?
    ///
    /// **Kept after PR A, and it means something different now.** It used to ask whether the
    /// mitigation held; it now asserts that a path which no longer moves anything still
    /// works end to end. Deleting it would remove the only case that closes and reopens the
    /// window at all.
    ///
    /// `ShowProgressWindow` builds a NEW `ProgressWindow` whenever the old one has closed,
    /// and `IProgressHost.ProgressTabBody` hands the new window the SAME
    /// `_progressTabBodies[...]` controls the previous one drew. That is a control moving
    /// between two window roots — the operation that throws *"Attempt to call
    /// InvalidateArrange on wrong LayoutManager"* and which Avalonia has had open as a bug
    /// since 2023 (#12753, #17906, #21267; we ship 12.1.1). Inline themes ran into it, but
    /// this path predates them and **no test has ever closed and reopened the window**.
    ///
    /// Fable's hypothesis, which this test exists to settle rather than argue: it survives
    /// because a closed window's presentation source is cleared, so the check passes by
    /// null and only two LIVE roots collide. If that is right this test passes and the
    /// reopen path is safe; if it throws, players can already reach a crash by closing and
    /// reopening Progress, and that is a release-blocking defect rather than a refactor.
    ///
    /// Either answer changes nothing about the plan — which is why it is worth one test.
    /// </summary>
    [AvaloniaFact]
    public void ClosingAndReopeningTheProgressWindowDoesNotThrow()
    {
        var window = new MainWindow();
        window.Show();

        window.ShowProgressWindow("progress");
        window.RenderSnapshotForTest(new StatsSnapshot { SessionStart = new DateTime(2026, 8, 8) });
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.ProgressWindowForTests);

        // Close it. The widget drops its reference on Closed, so the next call builds a
        // fresh window — and hands it the bodies the closed one was holding.
        window.ProgressWindowForTests!.Close();
        Dispatcher.UIThread.RunJobs();

        window.ShowProgressWindow("progress");
        window.RenderSnapshotForTest(new StatsSnapshot { SessionStart = new DateTime(2026, 8, 8) });
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.ProgressWindowForTests);

        window.ProgressWindowForTests!.Close();
        window.Close();
    }

    /// <summary>The same path with a TAB CHANGE in between, which is the half that moves a
    /// DIFFERENT body: reopening on Wealth hands the new window a control the old window
    /// never held, while the one it did hold is still parented to the closed root.</summary>
    [AvaloniaFact]
    public void ReopeningTheProgressWindowOnAnotherTabDoesNotThrow()
    {
        var window = new MainWindow();
        window.Show();

        window.ShowProgressWindow("progress");
        Dispatcher.UIThread.RunJobs();
        window.ProgressWindowForTests!.SetTab("faction");
        window.RenderSnapshotForTest(new StatsSnapshot { SessionStart = new DateTime(2026, 8, 8) });
        Dispatcher.UIThread.RunJobs();

        window.ProgressWindowForTests!.Close();
        Dispatcher.UIThread.RunJobs();

        window.ShowProgressWindow("wealth");
        window.RenderSnapshotForTest(new StatsSnapshot { SessionStart = new DateTime(2026, 8, 8) });
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.ProgressWindowForTests);

        window.ProgressWindowForTests!.Close();
        window.Close();
    }

    /// <summary>The same close-and-reopen for the other two theme windows, because the bug
    /// was never about Progress — all three borrow the widget's single tab bodies through
    /// the same shape (`LootTabBody`, `CreatureTabBody`). Fixing three windows and guarding
    /// one is how the third regresses quietly.</summary>
    [AvaloniaFact]
    public void ClosingAndReopeningTheGearLootWindowDoesNotThrow()
    {
        var window = new MainWindow();
        window.Show();

        window.ShowGearLootWindow("loot");
        Dispatcher.UIThread.RunJobs();
        window.GearLootWindowForTests!.Close();
        Dispatcher.UIThread.RunJobs();

        window.ShowGearLootWindow("loot");
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.GearLootWindowForTests);

        window.GearLootWindowForTests!.Close();
        window.Close();
    }

    [AvaloniaFact]
    public void ClosingAndReopeningTheCreatureWindowDoesNotThrow()
    {
        var window = new MainWindow();
        window.Show();

        window.ShowCreatureWindow(CreatureSurface.KeyFor(CreatureTab.Drops));
        Dispatcher.UIThread.RunJobs();
        window.CreatureWindowForTests!.Close();
        Dispatcher.UIThread.RunJobs();

        window.ShowCreatureWindow(CreatureSurface.KeyFor(CreatureTab.Drops));
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.CreatureWindowForTests);

        window.CreatureWindowForTests!.Close();
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
    // ---- the next-level preview, split per class (David, 2026-08-23) ----

    /// <summary>Builds the Experience surface on its own, with a class source the test
    /// controls. The widget's own source is picks-then-inference off a live ledger, and a
    /// headless profile has neither — so driving it through <c>MainWindow</c> would test
    /// the empty case three times over. The window is still created first: it is what
    /// applies the theme, and <see cref="AppTheme"/>'s brushes are process-wide (trap
    /// 31).</summary>
    private static (MainWindow Main, Window Host, ProgressCardView View) ExperienceWith(
        params string[] classes) => ExperienceAt(12, classes);

    private static (MainWindow Main, Window Host, ProgressCardView View) ExperienceAt(
        int level, params string[] classes)
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.ShowNextUnlocks = true;   // the rows, not just the fold's label
        ProgressCardView? view = null;
        view = new ProgressCardView(main.Settings, _ => classes, () => level,
            () => view!.Render(new StatsSnapshot()));
        var host = new Window { Content = view.Body, Width = 320, Height = 480 };
        host.Show();
        view.Render(new StatsSnapshot());
        host.UpdateLayout();
        return (main, host, view);
    }

    /// <summary>
    /// The WPF twin of <c>ProgressCard_SplitsTheNextLevelPreviewByClass</c> — Bevel's
    /// grouping rules, Helm-signed 2026-08-23, on the lane whose only coverage is a
    /// rendered frame.
    ///
    /// **The prediction, written before the run** (trap 23). Druid/Warrior at 12, from the
    /// shipped catalogs: the next level with anything is **13**, carrying three Druid
    /// spells (Befriend Animal, Expulse Summoned, See Invisible) and no AA — the AA
    /// catalog's levels are 1/6/8/10/12/15/…, so 13 has none and there is no "Any class"
    /// group. Two groups, three rows, and Warrior present as a row that says so.
    ///
    /// The negative assertion is the one that keeps this honest: Warrior must NOT be an
    /// <see cref="EqFoldLabel"/>. A chevron over an empty group is an affordance that
    /// opens nothing, and every icon assertion in this file needs one negative after
    /// <c>DropsRenderTests</c> spent months comparing two type names (trap 39).
    /// </summary>
    [AvaloniaFact]
    public void NextLevelPreviewSplitsIntoOneExpanderPerClass()
    {
        var (main, host, view) = ExperienceWith("Druid", "Warrior");

        Assert.Equal(2, view.NextGroups);
        Assert.Equal(3, view.NextRows);
        var text = host.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("▾ At level 13: 3 new spells", text);
        Assert.Contains("Befriend Animal", text);
        Assert.Contains("Druid spell", text);
        // Warrior keeps its row and says what it has: nothing. Dropping the group is
        // indistinguishable on screen from Warrior not being one of your classes.
        Assert.Contains("Warrior", text);
        Assert.Contains("Nothing new at 13", text);
        Assert.Contains("Druid", host.GetVisualDescendants().OfType<EqFoldLabel>()
            .Select(f => f.Text));
        Assert.DoesNotContain("Warrior", host.GetVisualDescendants().OfType<EqFoldLabel>()
            .Select(f => f.Text));

        host.Close();
        main.Close();
    }

    /// <summary>*"One inferred class = names under the heading, no lone expander."*
    ///
    /// **Prediction:** Druid alone reaches the same level 13 and the same three spells, so
    /// only the chrome differs — no <see cref="EqFoldLabel"/> at all under the heading.
    /// Asserting the identical row count against a different group count is what makes
    /// this a test of the split rule rather than of the catalog.</summary>
    [AvaloniaFact]
    public void OneClassGetsItsNamesUnderTheHeadingWithNoLoneExpander()
    {
        var (main, host, view) = ExperienceWith("Druid");

        Assert.Equal(0, view.NextGroups);
        Assert.Equal(3, view.NextRows);
        var text = host.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Befriend Animal", text);
        // Scoped to the class name, not to "no folds at all": the Experience surface
        // always carries the All-AA fold, and asserting on the whole tree would have been
        // a test of that instead.
        Assert.DoesNotContain("Druid", host.GetVisualDescendants().OfType<EqFoldLabel>()
            .Select(f => f.Text));

        host.Close();
        main.Close();
    }

    /// <summary>
    /// **"Any class" is a shared bucket, not a player class** — Bevel, Helm-signed
    /// 2026-08-23 1:05 PM CT: *"It does not trip the one-class no-expander rule. One player
    /// class with content stays flat names."* The first build counted GROUPS, so a
    /// single-class character who reached a level carrying a General or Archetype AA grew
    /// two expanders for one class to choose between.
    ///
    /// **Prediction, written before the run.** A Druid at 14 reaches level 15, which is the
    /// only nearby level carrying both: four Druid spells (Calm Animal, Ring of North
    /// Karana, Ring of Surefall Glade, Terrorize Animal) AND the Archetype AA Double
    /// Riposte, which belongs to no class. Five rows, flat, no expander — and Double
    /// Riposte still says "Archetype" in its value column, so the shared row is attributed
    /// without a heading to do it.
    /// </summary>
    [AvaloniaFact]
    public void TheSharedBucketDoesNotTurnOneClassIntoAFold()
    {
        var (main, host, view) = ExperienceAt(14, "Druid");

        Assert.Equal(0, view.NextGroups);
        Assert.Equal(5, view.NextRows);
        var text = host.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Calm Animal", text);
        Assert.Contains("Double Riposte", text);
        Assert.Contains("Archetype · 3 ranks", text);
        // No heading for either — not the class, not the bucket.
        Assert.DoesNotContain("Druid", host.GetVisualDescendants().OfType<EqFoldLabel>()
            .Select(f => f.Text));
        Assert.DoesNotContain(LevelUnlockGroups.SharedGroup,
            host.GetVisualDescendants().OfType<EqFoldLabel>().Select(f => f.Text));

        host.Close();
        main.Close();
    }

    /// <summary>
    /// No class in play hides the preview outright (Bevel, Helm-signed 2026-08-23).
    ///
    /// **This is a deliberate loss and it is worth stating.** Before 2026-08-23 a
    /// character with no picked and no inferred class still got a preview — built from the
    /// class-agnostic AA categories alone, with <c>LevelUnlocks.Next</c> walking forward
    /// to whatever level had one. That is how David's own card came to offer *"At level
    /// 39: 1 new AA ability"*, an Archetype pet ability five levels away, to a character
    /// with no pet.
    ///
    /// **It lives here rather than in E2E, and that took a failing run to learn.** The E2E
    /// harness always writes the shifted fixture log, which carries enough class-unique
    /// evidence for <c>ClassInference</c> to name a class — so the no-class state is not
    /// reachable there at all, and the assertion that looked like the strongest one in the
    /// suite was about a state the harness cannot produce. Here the class list is a
    /// parameter.
    /// </summary>
    [AvaloniaFact]
    public void NoClassHidesTheNextLevelPreview()
    {
        var (main, host, view) = ExperienceWith();

        Assert.Equal(0, view.NextGroups);
        Assert.Equal(0, view.NextRows);
        Assert.DoesNotContain(host.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? ""), t => t.Contains("At level "));

        host.Close();
        main.Close();
    }

    /// <summary>
    /// First class open, the rest collapsed — and a click moves it, for this session only.
    ///
    /// **Prediction:** Druid/Cleric at 12 both reach level 13 with three spells each
    /// (Druid: Befriend Animal, Expulse Summoned, See Invisible; Cleric: Cancel Magic,
    /// Endure Cold, Expulse Undead) and no AA between them. So the opening state is three
    /// rows, not six, and opening Cleric makes it six.
    ///
    /// The click is the assertion that matters. The state is a FIELD rather than a
    /// setting (Bevel: session-only), so nothing on disk can be inspected instead — and a
    /// fold whose chevron moves while its rows do not is exactly the kind of half-wired
    /// toggle a screenshot cannot tell from a working one.
    /// </summary>
    [AvaloniaFact]
    public void TheFirstClassOpensAndTheRestFoldUntilClicked()
    {
        var (main, host, view) = ExperienceWith("Druid", "Cleric");

        Assert.Equal(2, view.NextGroups);
        Assert.Equal(3, view.NextRows);
        Assert.True(Fold(host, "Druid").Open);
        Assert.False(Fold(host, "Cleric").Open);
        var text = host.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Befriend Animal", text);
        Assert.DoesNotContain("Cancel Magic", text);

        var cleric = Fold(host, "Cleric");
        var point = global::Avalonia.VisualExtensions.TranslatePoint(
            cleric, new global::Avalonia.Point(6, cleric.Bounds.Height / 2), host);
        Assert.True(point.HasValue, "the Cleric heading is not laid out — it cannot be clicked");
        host.MouseDown(point!.Value, global::Avalonia.Input.MouseButton.Left);
        host.MouseUp(point!.Value, global::Avalonia.Input.MouseButton.Left);
        host.UpdateLayout();

        Assert.Equal(6, view.NextRows);
        Assert.True(Fold(host, "Cleric").Open);
        Assert.Contains("Cancel Magic", host.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? ""));

        host.Close();
        main.Close();
    }

    // ---- the Progress theme's INLINE card (Inline themes PR B) ----

    /// <summary>The sequence that throws if any body assignment bypasses the one-owner
    /// rule: expand the card, pop the theme out, close the window, expand again. Each
    /// host builds its own surfaces since PR A, so the crash-class here is a shared
    /// instance sneaking back in — this is the guard the plan named before PR 1.</summary>
    [AvaloniaFact]
    public void ExpandPopOutCloseExpandDoesNotThrowAndEndsCollapsed()
    {
        var window = new MainWindow();
        window.Show();
        var snap = new StatsSnapshot { SessionStart = new DateTime(2026, 8, 8) };

        var card = window.ProgressCardForTests;
        // The class fixture sets EQBUDDY_EXPAND=1, so the card starts expanded (inline).
        Assert.True(card.IsExpanded, "EQBUDDY_EXPAND=1 should expand the theme card like every sibling");
        window.RenderSnapshotForTest(snap);
        Dispatcher.UIThread.RunJobs();
        Assert.True(card.TabCount >= 4, $"the inline strip should carry the theme's tabs, got {card.TabCount}");

        // Pop out: the window takes the body and the card collapses (one owner).
        window.ShowProgressWindow();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.ProgressWindowForTests);
        Assert.False(card.IsExpanded, "pop-out must collapse the card - one owner of the body");

        // Change tab IN the window with a real (headless) click - the PLAYER's switch is
        // what reaches the host; a programmatic SetTab deliberately does not.
        var pw = window.ProgressWindowForTests!;
        pw.RenderVisible(snap);
        pw.UpdateLayout();
        var chip = pw.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.Text == "Faction");
        var point = global::Avalonia.VisualExtensions.TranslatePoint(
            chip, new global::Avalonia.Point(2, chip.Bounds.Height / 2), pw);
        Assert.True(point.HasValue, "the Faction chip is not laid out - it cannot be clicked");
        pw.MouseDown(point!.Value, global::Avalonia.Input.MouseButton.Left);
        pw.MouseUp(point.Value, global::Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        // Close the window: collapsed, never back to inline (Bevel's rule).
        pw.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.False(card.IsExpanded, "closing the window must not re-grow the widget");

        // Expand again and paint - the sequence that threw on this toolkit when a body
        // had two hosts. This is the plan's "human step", now reachable headless because
        // PR A gave every host its own surfaces. The room the player picked in the
        // window is the room the card reopens on - the hand-back half of the handshake.
        card.IsExpanded = true;
        window.RenderSnapshotForTest(snap);
        Dispatcher.UIThread.RunJobs();
        Assert.True(card.IsExpanded);
        Assert.Equal(ProgressTab.Faction, card.SelectedTab);

        window.Close();
    }

    /// <summary>EQBUDDY_EXPAND=progress:raids — WPF parity (the named form, with the
    /// theme's room). Raids is the GLANCE room: its line renders and its full view is
    /// never built, which is the contract InlineMode exists to make.</summary>
    [AvaloniaFact]
    public void TheNamedExpandFormOpensTheThemeOnItsGlanceRoom()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_EXPAND", "progress:raids");
        try
        {
            var window = new MainWindow();
            window.Show();
            window.RenderSnapshotForTest(new StatsSnapshot { SessionStart = new DateTime(2026, 8, 8) });
            Dispatcher.UIThread.RunJobs();

            var card = window.ProgressCardForTests;
            Assert.True(card.IsExpanded, "EQBUDDY_EXPAND=progress:raids should expand the card");
            Assert.Equal(ProgressTab.Raids, card.SelectedTab);
            // The glance line is on screen - RaidsGlance's words, not the 29-row ledger.
            var text = window.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Text ?? "").ToList();
            Assert.Contains(text, t => t.Contains("left") || t.Contains("all cleared"));

            window.Close();
        }
        finally
        {
            Environment.SetEnvironmentVariable("EQBUDDY_EXPAND", "1");
        }
    }

    /// <summary>Clicking the card while its window is open brings the window forward and
    /// never draws a second copy of the surface — ThemeHost's ShouldBringWindowForward
    /// answer, asserted through the real card.</summary>
    [AvaloniaFact]
    public void TogglingTheCardWhileTheWindowIsOpenNeverExpandsIt()
    {
        var window = new MainWindow();
        window.Show();

        window.ShowProgressWindow();
        Dispatcher.UIThread.RunJobs();
        var card = window.ProgressCardForTests;
        Assert.False(card.IsExpanded);

        // The stack machinery's own path (Options, EQBUDDY_EXPAND) — routed through the
        // host, which answers bring-forward, not inline.
        card.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        Assert.False(card.IsExpanded, "the card must not expand while its window owns the body");

        window.ProgressWindowForTests!.Close();
        window.Close();
    }
}
