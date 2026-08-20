using EQBuddy.Core;

namespace EQBuddy.E2E;

// One app instance per test, never two at once: each launch shows a real always-on-top
// widget, and parallel widgets would fight for the desktop (and the CPU the replay uses).
[CollectionDefinition("e2e", DisableParallelization = true)]
public sealed class E2ECollection;

/// <summary>
/// The four v1 scenarios, end to end: the REAL EQBuddy.exe, a real log file growing
/// under it, assertions on the rendered result via the EQBUDDY_EXPAND state dump and
/// on the persisted history.db. Line shapes are copied verbatim from the fixture —
/// they are what LogParser actually matches.
/// </summary>
[Collection("e2e")]
public sealed class EndToEndTests
{
    // "a training dummy" appears nowhere in the fixture, so a fresh kill/loot of it
    // moves both the distinct-row counts and the totals by exactly one.
    private const string MeleeHit = "You crush a training dummy for 25 points of damage.";
    private const string Kill = "You have slain a training dummy!";

    /// <summary>
    /// The GEAR card's rendered shape, pinned before the Loot &amp; Items theme lifts it
    /// out of MainWindow.
    ///
    /// Trap 22 governs this test: the shared fixture imports no gear list, so the card is
    /// a one-line "No gear list imported." and a shot or an assertion against it would
    /// prove nothing about the rows underneath. The list is seeded here instead — and
    /// seeded across two slots and two zones, because the thing most easily lost in a
    /// lift is not the ITEMS but the GROUP HEADINGS between them, and the by-zone pivot
    /// exists entirely to change what those headings say.
    ///
    /// The WPF layer has no unit tests (docs/TestPlan.md §5), so this is the only thing
    /// standing between that move and a silent regression.
    /// </summary>
    [Fact]
    public void TheGearCardDrawsItsGroupsAndPivotsBetweenSlotAndZone()
    {
        // The card is a TAB now (the Gear & Loot theme), so the window has to be open for
        // its surface to exist. Same assertions, same numbers, new host — which is the
        // entire point of having pinned them before the fold.
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_GEARLOOT"] = "gear",
        }, configureSettings: s =>
        {
            s.GearChecklistName = "Harness list";
            s.GearChecklist =
            [
                new GearChecklistItem { Slot = "HEAD", Item = "Crown of Narandi", Source = "Kael Drakkel" },
                new GearChecklistItem { Slot = "HEAD", Item = "Helm of Rile", Source = "Kael Drakkel" },
                new GearChecklistItem { Slot = "HANDS", Item = "Gloves of Dark Embers", Source = "Sebilis", Acquired = true },
                // The second group. Without one the Exaltations heading never renders
                // and a lift could drop it with every assertion still green.
                new GearChecklistItem
                {
                    Slot = "HEAD", Item = "Exquisite Velium Shard", IsExaltation = true,
                    ExaltationEffect = "+15 hp", Source = "Kael Drakkel",
                },
            ];
        });
        app.Launch();

        // The grouping is Gear vs EXALTATIONS — not by slot, whatever the render
        // method is called (the slot rides on the row, not the heading). Predicting
        // five rows and getting four is what taught me that, and it is exactly the
        // kind of fact a lift can quietly lose: one heading, three gear rows, a
        // second heading, one exaltation row.
        app.WaitForDump("gearTotal", 4, "the seeded gear list to load");
        app.WaitForDump("gearAcquired", 1, "the one acquired item to count");
        app.WaitForDump("gearRows", 6, "two group headings above four item rows");
        app.WaitForDump("gearByZone", 0, "the card to start on the by-slot grouping");
        // The pivot toggle only exists once there is a list to pivot — with none it
        // would be a silent no-op, so it is hidden rather than dead.
        app.WaitForDump("gearPivotShown", 1, "the by-zone toggle to appear for a real list");
        // The named list is what tells you WHICH shopping list this is; a lift that
        // dropped the line would still render every row.
        // "Harness list - 1/4": the name AND the progress count, which is the line
        // that says which shopping list this is and how far through it you are.
        app.WaitForDump("gearListNameLen", "Harness list - 1/4".Length, "the list's name and progress");

        // And the LAUNCHER kept the glance. It replaced two card headers with one line,
        // so the line has to carry what both carried — loot count, what was crafted, and
        // the gear fraction. #219 is what happens when a fold quietly drops one of them.
        app.WaitForDump("lootCard", 1, "the Gear & Loot launcher card to be on the widget");
        // The line the shared formatter would produce for this fixture and this seeded
        // list, asserted by LENGTH because that is what the dump can carry — and the
        // length is the thing that moves if a part is silently dropped.
        app.WaitForDump("lootSummaryLen",
            LootSurface.LauncherSummary(items: 39, crafted: 4, gearTotal: 4, gearAcquired: 1).Length,
            "the launcher line to carry both cards' numbers");
    }

    /// <summary>With nothing imported the card says so in one line and hides the pivot —
    /// the empty state is a real state and the lift must keep it.</summary>
    [Fact]
    public void AnEmptyGearCardSaysSoAndOffersNoPivot()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_GEARLOOT"] = "gear",
        });
        app.Launch();

        app.WaitForDump("gearTotal", 0, "no gear list on the shared fixture");
        app.WaitForDump("gearRows", 1, "the one-line empty state");
        app.WaitForDump("gearPivotShown", 0, "no list, so nothing to pivot");
    }

    [Fact]
    public void SessionGoesLive_AndFreshKillUpdatesLiveStats()
    {
        using var app = new AppHarness();
        app.Launch();   // waits for the replayed session to be live

        var kills = app.DumpValue("killsTotal");
        var killRows = app.DumpValue("kills");
        Assert.True(kills > 0, "fixture replay should land kills");

        app.AppendLogLines(MeleeHit, Kill);

        app.WaitForDump("killsTotal", kills + 1, "the fresh kill to reach the widget");
        app.WaitForDump("kills", killRows + 1, "the new creature to get its own kill row");
    }

    [Fact]
    public void KillThenLoot_ShowsUpOnTheLootSurface()
    {
        // The loot ROWS are a tab now (the Gear & Loot theme), so the window has to be
        // open for them to exist. The COUNT still lives on the widget — it is half of the
        // launcher's one line — which is the split this assertion now pins: the number a
        // player glances at stayed put, the list moved.
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_GEARLOOT"] = "loot",
        });
        app.Launch();

        var lootTotal = app.DumpValue("lootTotal");
        var lootRows = app.DumpValue("lootRows");
        Assert.True(lootTotal > 0, "fixture replay should land loot");

        app.AppendLogLines(MeleeHit, Kill,
            "--You have looted a Harness Test Trinket from a training dummy's corpse.--");

        app.WaitForDump("lootTotal", lootTotal + 1, "the looted item to reach the widget");
        app.WaitForDump("lootRows", lootRows + 1, "the new item to get its own loot row");
    }

    [Fact]
    public void SeededWatchRule_CountsItsMatchingLoot()
    {
        using var app = new AppHarness(settings =>
            settings.TrackedRules.Add(new TrackedRule
            {
                Name = "Harness Test Widget",   // pattern falls back to the name
                Kind = WatchKind.Loot,
                AlertBanner = false,            // counting is under test, not alerting
            }));
        app.Launch();

        app.WaitForDump("tracked", 0, "the seeded rule to be live with no matches yet");

        app.AppendLogLines(
            "--You have looted a Harness Test Widget from a training dummy's corpse.--");

        app.WaitForDump("tracked", 1, "the watch rule's total to increment on the match");

        // The card DREW it, not merely counted it. One rule renders a heading and a
        // "last:" line and no sort strip — the strip appears only above two or more
        // rules, which is a rule easy to lose in a refactor and invisible to every
        // other test. Pinned here because the Watch surface is being lifted into its
        // own file and the WPF layer has no unit tests to catch the difference
        // (docs/TestPlan.md §5).
        app.WaitForDump("watchRows", 2, "a heading and a last-match line for the one rule");
        app.WaitForDump("watchStrip", 0, "no sort strip above a single rule");
    }

    /// <summary>The Progress surface DRAWS its three lists, not merely holds their data.
    ///
    /// Pinned from the outside because that surface keeps moving and the WPF layer has no
    /// unit tests (docs/TestPlan.md §5) — the same reason, and the same week, as the Watch
    /// assertions above. The three easy-to-lose conditions are all here: the ding list
    /// appears only once a level is ANNOUNCED, the next-milestone preview appears once a
    /// level is merely KNOWN but keeps its rows folded until the setting says otherwise,
    /// and the AA ledger splits into session-new and the whole list rather than showing
    /// one of them twice.
    ///
    /// It has now survived two moves without a single assertion changing: the card's lift
    /// into ProgressCardView, and the PROGRESS THEME folding that card into the Progress
    /// window's Experience tab. The only line that changed is the one that opens the
    /// window — which is what a good outside-in assertion is supposed to look like when
    /// the inside is rearranged.</summary>
    [Fact]
    public void ProgressCard_DrawsItsUnlockListsOnADing()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_PROGRESS"] = "1",   // Experience, where these three lists now live
        });
        app.Launch();

        // Before any level is known, neither list can honestly say anything.
        app.WaitForDump("dingShown", 0, "no ding list before a level is announced");
        app.WaitForDump("nextShown", 0, "no next-milestone preview before a level is known");

        app.AppendLogLines("You have gained a level! Welcome to level 12!");

        app.WaitForDump("dingShown", 1, "the ding list to appear once a level lands");
        // A level being known also unlocks the preview — the two are driven by different
        // facts (announced vs known) and a refactor that conflates them shows up here.
        app.WaitForDump("nextShown", 1, "the next-milestone preview once a level is known");
        // Folded by default: the label offers it, the rows wait to be asked for.
        app.WaitForDump("nextRows", 0, "the preview's rows to stay folded by default");
    }

    /// <summary>Two rules bring the sort strip up, and it starts on the stored default.
    /// The strip is a shared control now (UI.Shared.SortStrip.ForWatchRules) and a key
    /// spelled differently in one lane would paint four options with none selected —
    /// the silent no-op this pins from the outside.</summary>
    [Fact]
    public void TwoWatchRules_BringUpTheSortStrip()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackedRules.Add(new TrackedRule
            {
                Name = "Harness Test Widget", Kind = WatchKind.Loot, AlertBanner = false,
            });
            settings.TrackedRules.Add(new TrackedRule
            {
                Name = "Harness Test Trinket", Kind = WatchKind.Loot, AlertBanner = false,
            });
        });
        app.Launch();

        app.AppendLogLines(
            "--You have looted a Harness Test Widget from a training dummy's corpse.--",
            "--You have looted a Harness Test Trinket from a training dummy's corpse.--");

        app.WaitForDump("tracked", 2, "both rules to count their match");
        app.WaitForDump("watchStrip", 1, "the sort strip to appear above two rules");
        app.WaitForDump("watchSort", "manual", "the strip to start on the stored default");
    }

    [Fact]
    public void Session_SurvivesGracefulCloseAndRelaunch()
    {
        using var app = new AppHarness();
        app.Launch();

        var kills = app.DumpValue("killsTotal");
        app.AppendLogLines(MeleeHit, Kill);
        app.WaitForDump("killsTotal", kills + 1, "the fresh kill to land before closing");

        app.CloseGracefully();   // finalizes the session into history.db

        // Same profile, same log: the replay must ADOPT the finalized session
        // (same server/character/start), not mint a duplicate row.
        app.Launch();
        app.CloseGracefully();

        using var repo = new SessionRepository(app.HistoryDbPath);
        var row = Assert.Single(repo.Query(character: AppHarness.Character));
        Assert.Equal(AppHarness.Server, row.Server);
        Assert.Equal(kills + 1, row.Kills);
        Assert.Equal("ApplicationExit", row.EndReason);
    }

    /// <summary>
    /// The half of #144 that a unit test cannot reach.
    ///
    /// `WidgetMetrics.SectionMaxHeight` is unit-tested, but the bug that reached players
    /// was not the arithmetic being wrong in isolation — it was a screen-pixel figure
    /// being handed to a control that measures in pre-scale units. Only a launched app
    /// can show that the conversion happens at the point of assignment, with the real
    /// monitor cap and the real scale. So: launch at a scale where the two units
    /// disagree, and check that the card list's cap covers the monitor exactly once.
    ///
    /// Deliberately run at 1.6 rather than 1.0, because at 1.0 the buggy and the correct
    /// code produce the same number — which is precisely why this shipped.
    /// </summary>
    [Fact]
    public void TheCardListsCapIsConvertedForTheUiScale()
    {
        using var app = new AppHarness(s => s.UiScale = 1.6);
        app.Launch();

        var scale = app.DumpValue("uiScale100") / 100.0;
        var screenCap = app.DumpValue("sectionCapScreen");
        var assigned = app.DumpValue("sectionMaxH");

        Assert.Equal(1.6, scale, 2);
        Assert.True(screenCap > 0, "monitor-derived cap should be set; dump was: " + app.Artifacts());

        // The assigned value is in pre-scale units, so scaling it back must land on the
        // monitor's own ceiling. The pre-fix code assigned screenCap unconverted, which
        // would come back 1.6x too large here.
        Assert.Equal(screenCap, assigned * scale, 1);
    }

    /// <summary>The same check at 100%, where the units coincide — a guard against
    /// "fixing" the conversion in a way that only works when it is needed.</summary>
    [Fact]
    public void TheCapIsUnchangedAtFullScale()
    {
        using var app = new AppHarness(s => s.UiScale = 1.0);
        app.Launch();

        var screenCap = app.DumpValue("sectionCapScreen");
        // Guard against passing on two missing keys: DumpValue answers -1 for absent,
        // and -1 == -1 would make this test agree with a dump that says nothing at all.
        Assert.True(screenCap > 0, "monitor-derived cap should be set; dump was: " + app.Artifacts());
        Assert.Equal(screenCap, app.DumpValue("sectionMaxH"));
    }

    /// <summary>
    /// The Quests card, which replaced the Epic and Sky cards on 2026-08-16.
    ///
    /// This test used to pin those two cards' tab and row counts — written on
    /// 2026-08-15 to prove the QuestChecklistView extraction moved behaviour and not
    /// just lines. Both cards are now one launcher that opens the Quest Tracker, so the
    /// tabs it asserted no longer exist on the widget; what has to stay true is the
    /// reason the cards existed at all. The card SHOWS (a hidden launcher is an
    /// unreachable window), both checklists are still built and populated behind it, and
    /// its one line is non-empty — which is the whole glance that two cards used to
    /// carry, and the thing a consolidation can silently drop.
    ///
    /// Bard is still the seeded class: its epic is the one whose rows we have argued
    /// about most (#150 / #139), so a wrong count here is a number someone recognizes.
    /// </summary>
    [Fact]
    public void TheQuestsCardShowsAndKeepsBothChecklistsGlanceable()
    {
        using var app = new AppHarness(s => s.EpicQuestClass = "Bard");
        app.Launch();

        Assert.Equal(1, app.DumpValue("questsCard"));

        // The checklists still build with no card of their own to render into — they
        // feed the Quest Tracker window and EQBuddy Mobile, and the loot auto-checkers
        // tick them whether or not anything is on screen.
        Assert.True(app.DumpValue("questsEpicTotal") > 0,
            "the epic checklist should still be built behind the card; dump was: " + app.Artifacts());
        Assert.True(app.DumpValue("questsSkyTotal") > 0,
            "the sky checklist should still be built behind the card; dump was: " + app.Artifacts());

        // "Epic 1/12 · Sky 3/40" — folding two cards into one must not cost the numbers.
        Assert.True(app.DumpValue("questsSummaryLen") > 0,
            "the Quests card should summarise both checklists; dump was: " + app.Artifacts());
    }

    /// <summary>
    /// #158 (twidget76): v1.84.0 would not start for anyone who had "classic-doable
    /// only" ticked.
    ///
    /// The constructor restores that checkbox, restoring it raises its Checked handler,
    /// and after the QuestChecklistView extraction that handler forwarded to a field
    /// assigned thirty lines further down. NullReferenceException inside
    /// MainWindow..ctor — so the process started, logged, and never produced a window:
    /// visible in Task Manager, no CPU, nothing on the taskbar.
    ///
    /// The existing quest test did not catch it because the harness left the setting
    /// false, and assigning false to an unchecked box raises nothing. The bug lived
    /// entirely in the TRUE path, which is why it reached players.
    ///
    /// So: launch with it on, and require the app to reach a live session — the same
    /// bar every other scenario uses, which a half-built window cannot clear.
    ///
    /// The checkbox itself moved to the Quest Tracker's Epic tab on 2026-08-16 with the
    /// card consolidation, so MainWindow no longer restores it and this exact crash can
    /// no longer happen there. The scenario stays anyway: the setting is still honored
    /// (by the Epic tab and by EQBuddy Mobile's projection), and "launches with this on"
    /// is cheap insurance on a path that has already cost one release.
    /// </summary>
    [Fact]
    public void TheAppStartsWithTheClassicOnlyEpicFilterAlreadyOn()
    {
        using var app = new AppHarness(s => s.EpicQuestClassicOnly = true);
        app.Launch();   // waits for killsTotal > 0; a window that never built never gets there

        Assert.Equal(1, app.DumpValue("questsCard"));
    }

    /// <summary>
    /// The PROGRESS THEME's Raids tab (docs/Themes.md), and the number that makes this
    /// assertion worth having: <b>29</b>.
    ///
    /// It was pinned first against the Raids CARD on the widget — 6 zone headings, 21 boss
    /// rows, the provenance note and the /outputfile button — and then had to come back
    /// unchanged from three different places in turn: the card, <c>RaidsCardView</c> after
    /// the lift, and the Progress window's tab after the fold. A consolidation's whole
    /// claim is that the tabs draw what the cards drew, and the WPF layer has no unit test
    /// project (docs/TestPlan.md §5), so this is the only thing that can check it.
    ///
    /// Raids needed new machinery on both sides. Its rows live in raid-kills.json rather
    /// than settings.json, so an empty ledger renders the one-line empty state and an
    /// assertion on THAT proves nothing about the rows (trap 22) — hence
    /// <see cref="AppHarness.SeedRaids"/>. And only the ACTIVE tab paints, which is why
    /// each tab gets its own launch here rather than one test reading all four.
    /// </summary>
    [Fact]
    public void TheProgressThemesRaidsTabDrawsWhatTheRaidsCardDrew()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_PROGRESS"] = "raids",
        });
        // Two clears in two different shapes, because the surface renders them
        // differently: a witnessed kill carries a difficulty badge and a date, an imported
        // achievement carries neither. One of each makes the "honesty over flattery" rule
        // visible to an assertion.
        app.SeedRaids(("Phinigel Autropos", 3, false), ("Lord Nagafen", 0, true));
        app.Launch();

        Assert.Equal(2, app.DumpValue("progressRaidsDefeated"));
        Assert.Equal(6 + 21 + 2, app.DumpValue("progressRaidsRows"));
        // The launcher that replaced five cards. A hidden one is an unreachable window.
        Assert.Equal(1, app.DumpValue("progressCard"));
        Assert.True(app.DumpValue("progressSummaryLen") > 0,
            "the launcher should summarise the theme; dump was: " + app.Artifacts());
    }

    /// <summary>
    /// The Wealth tab — the one tab that absorbs TWO cards, and therefore the one that
    /// could be half built and still look finished.
    ///
    /// Both halves are pinned: the 24 sold rows the Money card drew, the sold BLOCK being
    /// up (its label appears only when something was sold, and a heading that stops
    /// appearing is invisible in a diff), and the Motes card's row.
    /// </summary>
    [Fact]
    public void TheWealthTabDrawsBothCardsItAbsorbed()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_PROGRESS"] = "wealth",
        });
        app.Launch();

        Assert.Equal(24, app.DumpValue("progressMoneySold"));
        Assert.Equal(1, app.DumpValue("progressMoneySoldShown"));
        Assert.Equal(1, app.DumpValue("progressMotesRows"));
    }

    /// <summary>The Faction tab, which the widget's own header reported as five for this
    /// fixture and which the tab has to keep reporting as five.</summary>
    [Fact]
    public void TheFactionTabDrawsWhatTheFactionCardDrew()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_PROGRESS"] = "faction",
        });
        app.Launch();

        Assert.Equal(5, app.DumpValue("progressFaction"));
        Assert.Equal("faction", app.DumpText("progressTab"));
        Assert.Equal(4, app.DumpValue("progressTabs"));
    }

    /// <summary>
    /// The Quest Tracker WINDOW, rebuilt for Gate 2 of the UI/UX rework
    /// (docs/DesignSystem.md): a column of self-contained cards became a LIST plus a
    /// DETAIL PANE.
    ///
    /// The WPF layer has no unit test project (docs/TestPlan.md §5), so a launched app
    /// reporting its own structure is the only cover this rebuild can have — and the
    /// rebuild's whole claim is structural. Its Avalonia twin is covered headlessly by
    /// QuestsRenderTests; this is the Windows half, and the thing that would otherwise
    /// regress silently is exactly the join between them: rows built, one of them
    /// selected, and a pane that actually filled with that selection's content.
    ///
    /// "all" rather than "mine", so the assertion does not depend on the fixture
    /// character happening to own a catalogued turn-in — the whole catalog is always
    /// there, and the render cap is what the row count is really pinning.
    /// </summary>
    [Fact]
    public void TheQuestTrackerBuildsAListWithASelectionAndAFilledDetailPane()
    {
        using var app = new AppHarness(
            environment: new Dictionary<string, string> { ["EQBUDDY_QUESTS"] = "all" });
        app.Launch();

        // The window opens at ApplicationIdle after the replay, so the dump carries its
        // facts only once it exists.
        Wait.Until(() => app.DumpValue("questsRows") > 0, TimeSpan.FromSeconds(45),
            "the Quest Tracker to open and build rows", app.Artifacts);

        // The render cap, honoured: "all" offers the whole 1,172-quest catalog and the
        // window builds the first 60. A row count equal to the catalog would mean the cap
        // stopped working, which is the change that once froze the window per keystroke.
        Assert.Equal(60, app.DumpValue("questsRows"));
        // Never a silent cap (CLAUDE.md): what was withheld is counted and said.
        Assert.True(app.DumpValue("questsSuppressed") > 0,
            "the withheld remainder should be counted; dump was: " + app.Artifacts());

        // The pane is the other half of the surface. A selection with an empty pane, or a
        // pane with no selection, is the rebuild half-working — and both render as a
        // plausible-looking window.
        Assert.Equal(1, app.DumpValue("questsSelected"));
        Assert.Equal(1, app.DumpValue("questsDetailShown"));
        Assert.True(app.DumpValue("questsDetailBlocks") >= 3,
            "the detail pane should carry at least title, status and details; dump was: "
            + app.Artifacts());

        // Core's QuestSurface owns the tab list, and the mode strip is the five views.
        Assert.Equal(3, app.DumpValue("questsTabs"));
        Assert.Equal(5, app.DumpValue("questsModes"));
    }

    /// <summary>
    /// EQBuddy Mobile's 20 Hz pump costs nothing when nobody is paired.
    ///
    /// `CompanionPumpGateTests` proves the gate returns false; it cannot prove the real
    /// timer is wired to the real gate in the real app. That is the half worth checking
    /// here, because the failure is silent: a mis-wired pump doesn't break a feature, it
    /// rebuilds a snapshot twenty times a second forever, for nobody — and the only
    /// symptom is a fan. This profile never pairs a device, and the fixture replay makes
    /// the session version move plenty, so any push at all is a wiring bug.
    /// </summary>
    [Fact]
    public void TheMobilePumpRunsAndCostsNothingWithNoDevicePaired()
    {
        using var app = new AppHarness();
        app.Launch();

        Assert.True(app.DumpValue("companionPumpTicks") > 0,
            "the mobile pump should be running at all; dump was: " + app.Artifacts());
        Assert.Equal(0, app.DumpValue("companionPushes"));
    }
}
