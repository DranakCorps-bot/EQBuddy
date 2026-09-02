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
        // POPULATED too, not only empty — the player likeliest to need the dump is the one
        // whose import has gone stale, which is RaidsCardView's own rule.
        app.WaitForDump("gearCopyCmd", 1,
            "the ⧉ copy of /outputfile inventory to survive a populated list");
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

    /// <summary>
    /// The Gear tab's list stops carrying a CARD-SIZED cap around inside a WINDOW.
    ///
    /// `GearCardView` built its own `ScrollViewer { MaxHeight = 320 }` — a number chosen
    /// for the widget, which came along when the surface was lifted into the Gear &amp;
    /// Loot window. The window already caps its body with `WindowSizing.BodyCap` off the
    /// monitor and off any height the player dragged, and the inner 320 overrode all of it:
    /// the window grew, the gear list did not. A resize that visibly does nothing is the
    /// complaint this whole area started with, and trap 36's own note had this file flagged
    /// as the loose end.
    ///
    /// The list is capped BELOW the window body, not equal to it, because the auto-tick
    /// note, the ⧉ copy of the command and the import report stay pinned outside the
    /// scroller (trap 37) — the reason the scroller is re-pointed rather than deleted.
    /// </summary>
    [Fact]
    public void TheGearListsCapFollowsTheWindowRatherThanACardSizedConstant()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_GEARLOOT"] = "gear",
        });
        app.Launch();
        app.WaitForWindow("gearListCap", "the Gear tab to report its list cap");

        var windowBody = app.DumpValue("gearLootBodyCap");
        var listCap = app.DumpValue("gearListCap");

        Assert.True(windowBody > 0,
            "the window should cap its own body; dump was: " + app.Artifacts());
        // Derived from the window, not from 320. On every screen the run can land on, the
        // window body opens at WindowSizing.DefaultBodyHeight (400) or less, so a list cap
        // of exactly 320 would mean the constant survived.
        Assert.True(listCap <= windowBody,
            $"the list must fit inside the window's body: {listCap} vs {windowBody}");
        Assert.True(listCap > 120,
            $"the list must still be a list: {listCap}");
        // The pinned footer is real, so the list gets LESS than the whole body — an equal
        // number would mean the note and the ⧉ copy were being counted as list.
        Assert.True(listCap < windowBody,
            $"the pinned note and ⧉ copy must be left room: {listCap} vs {windowBody}");
    }

    /// <summary>With nothing imported the card says so in one line and hides the pivot —
    /// the empty state is a real state and the lift must keep it.
    ///
    /// It is also the state a NEW PLAYER meets, which is why David met it (2026-08-20) and
    /// found it naming an import with no route and no tool. So it now asserts the two ways
    /// out as well as the absence of rows: the shopping-list route in the line, and the
    /// ⧉ copy of the in-game command that makes the ticks happen by themselves.</summary>
    [Fact]
    public void AnEmptyGearCardSaysSoAndOffersNoPivot()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_GEARLOOT"] = "gear",
        });
        app.Launch();

        app.WaitForDump("gearTotal", 0, "no gear list on the shared fixture");
        // 0, not 1. The empty state is the LIST NAME line — "export one from EQ Legends
        // Tools, then Options → …" — and the row that used to sit under it said the same
        // thing again in less useful words (David, 2026-08-20).
        app.WaitForDump("gearRows", 0, "no rows under the route line");
        app.WaitForDump("gearPivotShown", 0, "no list, so nothing to pivot");
        // The state a new player meets is the state the complaint was about (David,
        // 2026-08-20): the tab named an import and handed over no way to run it.
        app.WaitForDump("gearCopyCmd", 1,
            "the ⧉ copy of /outputfile inventory to be on the EMPTY tab");
    }

    /// <summary>The DROPS surface draws its creatures, its item rows and the filter that
    /// narrows both the display and the exports.
    ///
    /// Written BEFORE its body is lifted out of <c>DropsWindow.xaml</c> into a tab of the
    /// Kills &amp; Drops theme. That lift is a XAML-to-code conversion rather than the
    /// straight move the Gear Locker was, so every control is re-created by hand and any
    /// one of them can go missing with the build still green — and the WPF layer has no
    /// unit tests (docs/TestPlan.md §5). This assertion is the only thing between the
    /// conversion and a silent regression, and it is written to survive the move: the same
    /// keys, read out of whichever host owns the surface.
    ///
    /// The counts come off the fixture replay rather than being spelled out, because the
    /// fixture log is shared and a hard-coded creature count would be a test of the
    /// fixture. What is asserted is the RELATIONSHIP the surface exists to draw: a heading
    /// per creature, a row per drop, and the two adding up to what is on screen.</summary>
    /// <summary>The Wiki contribution pack window draws its rows and carries the re-check
    /// button (#226). The target COUNT is not predicted — it depends on what eqlwiki says
    /// about the fixture's creatures that minute — but the fact must be present, which is
    /// what says the button exists (trap 29/34).</summary>
    [Fact]
    public void TheWikiPackWindowDrawsRowsAndCarriesTheRecheck()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_WIKIPACK"] = "1",
        });
        app.Launch();

        app.WaitForWindow("packRows", "the Wiki contribution pack window to open");
        Wait.Until(() => app.DumpValue("packRows") > 0, TimeSpan.FromSeconds(45),
            "the fixture replay to land at least one creature in the pack");
        Assert.True(app.DumpValue("packRecheck") >= 0, "the re-check button reports its target count");
    }

    [Fact]
    public void TheDropsSurfaceDrawsACreatureHeadingAboveItsDropRows()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_DROPS"] = "1",
        });
        app.Launch();

        // The fixture kills things and loots them, so there is something to group.
        app.WaitForWindow("dropsMobs", "the Kills & Drops window to open on Drops");
        Wait.Until(() => app.DumpValue("dropsMobs") > 0, TimeSpan.FromSeconds(45),
            "the fixture replay to land at least one creature with a drop");

        var mobs = app.DumpValue("dropsMobs");
        var items = app.DumpValue("dropsItems");
        Assert.True(items >= mobs,
            $"every creature shown has at least one drop (mobs={mobs}, items={items})");
        // One heading per creature, one row per drop. This is the fact the conversion can
        // lose without failing anything else: drop the heading loop and the rows still
        // render, drop the row loop and the headings still do.
        app.WaitForDump("dropsRows", mobs + items,
            "a heading per creature above a row per drop");
        app.WaitForDump("dropsFilterLen", 0, "the filter box to start empty");
        // Every heading carries the wiki re-check ↻ (#226). Asserted because an absent
        // control photographs as an unremarkable header (trap 29/34).
        app.WaitForDump("dropsRecheck", mobs, "a wiki re-check button on every creature heading");
    }

    [Fact]
    public void SessionGoesLive_AndFreshKillUpdatesLiveStats()
    {
        // The kill ROWS are a tab now (the Kills & Drops theme), so the window has to be
        // open for them to exist. The COUNT still lives on the widget — it is the first
        // part of the launcher's one line — which is the split this assertion pins: the
        // number a player glances at stayed put, the list moved. Same shape as
        // KillThenLoot_ShowsUpOnTheLootSurface after the Gear & Loot fold.
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_CREATURE"] = "kills",
        });
        app.Launch();   // waits for the replayed session to be live

        var kills = app.DumpValue("killsTotal");
        Assert.True(kills > 0, "fixture replay should land kills");
        app.WaitForWindow("kills", "the Kills & Drops window to open and report its rows");
        var killRows = app.DumpValue("kills");
        Assert.Equal(1, app.DumpValue("killsCard"));   // the door is on the widget
        Assert.True(app.DumpValue("killsSummaryLen") > 0,
            "the launcher should summarise the theme; dump was: " + app.Artifacts());

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
        Assert.True(lootTotal > 0, "fixture replay should land loot");
        app.WaitForWindow("lootRows", "the Gear & Loot window to open and report its rows");
        var lootRows = app.DumpValue("lootRows");

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
    /// the inside is rearranged.
    ///
    /// **The classes are new, and the test was previously asserting less than it looked.**
    /// It ran with an empty ledger, so the preview it checked was built from the
    /// class-agnostic AA categories alone — which since 2026-08-23 does not appear at all
    /// (Bevel, Helm-signed: a list that cannot be about you should not claim to be). One
    /// seeded pick makes the assertion mean what its name says.</summary>
    [Fact]
    public void ProgressCard_DrawsItsUnlockListsOnADing()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_PROGRESS"] = "1",   // Experience, where these three lists now live
        });
        app.SeedQuestClasses("Druid");
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

    /// <summary>
    /// The next-level preview, split per class (David's ask, 2026-08-23; Bevel's rules,
    /// Helm-signed the same day).
    ///
    /// **The prediction, written before the run** (trap 23: a number you did not predict
    /// has not been reviewed). Warrior/Druid at level 12, from the shipped catalogs: the
    /// next level with anything is **13**, carrying three Druid spells (Befriend Animal,
    /// Expulse Summoned, See Invisible) and no AA at all — the AA catalog's levels are
    /// 1/6/8/10/12/15/…, so 13 has none, and there is therefore no "Any class" group.
    /// That gives **two** groups and **three** rows: Druid open with its three, Warrior
    /// kept as a row reading "Nothing new at 13" rather than dropped.
    ///
    /// The Warrior half is the assertion worth having. A class with no spell table at any
    /// level is exactly the group a tidy-minded refactor removes, and on screen its
    /// absence is indistinguishable from that class not being one of yours.
    /// </summary>
    [Fact]
    public void ProgressCard_SplitsTheNextLevelPreviewByClass()
    {
        using var app = new AppHarness(
            settings => settings.ShowNextUnlocks = true,   // the fold's rows, not just its label
            environment: new Dictionary<string, string> { ["EQBUDDY_PROGRESS"] = "1" });
        app.SeedQuestClasses("Druid", "Warrior");
        app.Launch();

        app.AppendLogLines("You have gained a level! Welcome to level 12!");

        app.WaitForDump("nextShown", 1, "the preview once a level is known");
        app.WaitForDump("nextGroups", 2, "one expander per class — Druid and Warrior");
        app.WaitForDump("nextRows", 3, "the three Druid spells at 13, with Warrior's empty group holding no rows");
    }

    /// <summary>
    /// The Quest Tracker's picks WIDEN the classes EQBuddy read from the log; they do not
    /// replace them.
    ///
    /// **This test asserted the opposite until 2026-08-23, and the change is the point.**
    /// It seeded Druid alone and expected ONE class — because picks used to win outright
    /// and inference was only consulted when there were none. That is the rule #104 argued
    /// against (a player may tick a class to help a friend) and the one Bevel's lock
    /// forbids ("never fall back to the Quest Tracker filter"). `CharacterClasses.Resolve`
    /// now unions them.
    ///
    /// **Prediction:** the harness fixture infers WARRIOR (its level-12 ding is Heroic Leap
    /// and Unbound Wrath, both Warrior Class AAs). Seeding a Druid PICK on top gives
    /// Warrior + Druid, so level 13 draws two groups and three rows — the three Druid
    /// spells, with Warrior kept as a row reading "nothing new at 13". The single-class
    /// no-expander rule is asserted where the class list is a parameter and this fixture
    /// cannot muddy it: `WidgetRenderTests.OneClassGetsItsNamesUnderTheHeadingWithNoLoneExpander`.
    /// </summary>
    [Fact]
    public void ProgressCard_PicksWidenTheInferredClassRatherThanReplacingIt()
    {
        using var app = new AppHarness(
            settings => settings.ShowNextUnlocks = true,
            environment: new Dictionary<string, string> { ["EQBUDDY_PROGRESS"] = "1" });
        app.SeedQuestClasses("Druid");
        app.Launch();

        app.AppendLogLines("You have gained a level! Welcome to level 12!");

        app.WaitForDump("nextGroups", 2, "the picked Druid AND the inferred Warrior");
        app.WaitForDump("nextRows", 3, "the three Druid spells at 13, Warrior contributing none");
    }

    // **There is no E2E twin for "no class hides the preview", and that is a finding
    // rather than an omission.** The harness always writes the shifted fixture log, and
    // that log carries enough class-unique evidence for `ClassInference` to name a class
    // — so with no seeded picks the app still HAS a class and the preview correctly
    // appears (observed: nextShown=1, nextGroups=2). The rule is asserted where the class
    // list can actually be empty: `WidgetRenderTests.NoClassHidesTheNextLevelPreview`.
    // Writing a class-free fixture just for this would give the whole suite a second log
    // to keep true, which is a worse trade than one assertion on the other lane.

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

    /// <summary>
    /// #250 (Paineless): **an untouched widget is pixel-identical to what shipped before
    /// the cap learned to scale.** ContentHeight is NaN until someone drags the grip, and
    /// this is the assertion that protects every existing player from a change none of
    /// them asked for.
    ///
    /// It has to be a launched app. The WPF layer has no unit tests (docs/TestPlan.md §5),
    /// the cap is assigned from a measurement no unit test can produce, and an absent or
    /// wrong MaxHeight photographs as an unremarkable card (trap 29).
    /// </summary>
    [Fact]
    public void AnUndraggedWidgetKeepsTheOldThemeBodyCap()
    {
        using var app = new AppHarness();
        app.Launch();
        app.WaitForDump("themeBodyCap", 320,
            "an undragged widget keeps the theme body cap it has always had");

        Assert.Equal(1, app.DumpValue("contentHeightAuto"));
        // EQBUDDY_EXPAND=1 opens the World theme card, so the number above is a REAL
        // expanded body's cap and not a default nobody consulted.
        Assert.Equal(1, app.DumpValue("worldInline"));
    }

    /// <summary>
    /// The ask itself, in a launched app: drag the widget taller and the open theme's body
    /// gets more room. Paineless reached for exactly this control and reported that it did
    /// nothing — *"cannot just expand window size"* — because the cap was a constant.
    ///
    /// 4000 is deliberately further than any monitor allows. The assertion is a RANGE
    /// rather than 640 on purpose: the drag is clamped to the work area before it reaches
    /// this arithmetic, so the exact answer depends on the screen this runs on — and a
    /// test that hard-coded the ceiling would be asserting the monitor, not the feature.
    /// What must hold on every screen is that the body grew and that it stayed bounded:
    /// one card may double, and it may not eat the monitor. The exact ceiling is pinned in
    /// <c>WidgetMetricsTests</c> and in the Avalonia render tests, where the screen is not
    /// a variable.
    /// </summary>
    [Fact]
    public void DraggingTheWidgetTallerGrowsTheOpenThemesBody()
    {
        using var app = new AppHarness(s => s.ContentHeight = 4000);
        app.Launch();
        app.WaitForWindow("themeBodyCap", "the expanded theme card reports its body cap");

        var cap = app.DumpValue("themeBodyCap");
        Assert.Equal(0, app.DumpValue("contentHeightAuto"));
        Assert.Equal(1, app.DumpValue("worldInline"));
        Assert.True(cap > 320, $"a dragged widget should grow the open body; cap was {cap}");
        Assert.True(cap <= 640, $"one card may double, never more; cap was {cap}");
    }

    /// <summary>Dragged SHORTER than the floor, the body keeps the floor — the direction
    /// that could have made a crowded widget worse than it was before the change.</summary>
    [Fact]
    public void DraggingTheWidgetShorterNeverTakesTheThemeBodyBelowTheFloor()
    {
        using var app = new AppHarness(s => s.ContentHeight = 200);
        app.Launch();
        app.WaitForDump("themeBodyCap", 320,
            "a widget dragged shorter than the floor keeps the floor");

        Assert.Equal(0, app.DumpValue("contentHeightAuto"));
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

        app.WaitForWindow("progressRaidsDefeated", "the Progress window to open on Raids");
        // POLLED, not read once. Launch() returns when the replay has landed its first
        // KILL, which is early — faction standings, vendor sales and raid clears arrive
        // later in the same replay, so a bare Assert.Equal here is a race against the log
        // ingest. It failed about one run in three once the Kills fold shifted startup
        // timing by a few hundred milliseconds; the assertion was always this fragile and
        // had simply been getting away with it.
        app.WaitForDump("progressRaidsDefeated", 2, "the two seeded raid clears");
        app.WaitForDump("progressRaidsRows", 6 + 21 + 2, "the raid target rows");
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

        app.WaitForWindow("progressMoneySold", "the Progress window to open on Wealth");
        // POLLED, not read once. Launch() returns when the replay has landed its first
        // KILL, which is early — faction standings, vendor sales and raid clears arrive
        // later in the same replay, so a bare Assert.Equal here is a race against the log
        // ingest. It failed about one run in three once the Kills fold shifted startup
        // timing by a few hundred milliseconds; the assertion was always this fragile and
        // had simply been getting away with it.
        app.WaitForDump("progressMoneySold", 24, "the 24 sold rows the Money card drew");
        app.WaitForDump("progressMoneySoldShown", 1, "the sold block's heading");
        app.WaitForDump("progressMotesRows", 1, "the Motes card's row");
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

        app.WaitForWindow("progressFaction", "the Progress window to open on Faction");
        // POLLED, not read once. Launch() returns when the replay has landed its first
        // KILL, which is early — faction standings, vendor sales and raid clears arrive
        // later in the same replay, so a bare Assert.Equal here is a race against the log
        // ingest. It failed about one run in three once the Kills fold shifted startup
        // timing by a few hundred milliseconds; the assertion was always this fragile and
        // had simply been getting away with it.
        app.WaitForDump("progressFaction", 5, "the five factions the card reported");
        app.WaitForDump("progressTab", "faction", "the window to open on the Faction tab");
        app.WaitForDump("progressTabs", 4, "all four Progress tabs");
    }

    /// <summary>A heading with nothing under it reads as a surface that failed to load.
    /// The Experience room drew "Skill-ups" unconditionally until 2026-08-22 — on the
    /// widget card AND in this window, since both hosts draw the same view — and the
    /// fixture session has no skill-ups, so it is exactly the state a screenshot caught
    /// and no test could. Same shape as MoneyCardView's sold block.</summary>
    [Fact]
    public void TheSkillUpsHeadingStaysDownWhenThereAreNoSkillUps()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_PROGRESS"] = "1",
        });
        app.Launch();

        app.WaitForWindow("progressSkillLabel", "the Progress window to open on Experience");
        app.WaitForDump("progressSkills", 0, "no skill-ups in the fixture session");
        app.WaitForDump("progressSkillLabel", 0, "the heading to stay down with no rows under it");
    }

    /// <summary>
    /// THE LEVEL-UPS FOLD (#240, joeymavity: *"leveling timestamps in an xp dropdown, I
    /// can't find it now"*), on the WPF lane, which has no unit tests of its own.
    ///
    /// **Three keys rather than one, because two of the states look almost identical on
    /// screen and are opposites.** A heading that is DOWN means this character has never
    /// dinged while EQBuddy watched. A heading that is UP over zero rows is the default
    /// every player gets — folded, with the count and the last date on the label. Asserting
    /// only "rows == 0" would pass for both, which is the shape of a guard that reads as
    /// coverage while being blind to the failure that matters (trap 34).
    ///
    /// **The prediction, written before the run** (trap 23). The fixture session announces
    /// no level, and a headless E2E profile has no `history.db` rows for this character —
    /// so on launch the heading is DOWN and the count is 0. Appending one ding is the only
    /// thing that can move it, and it moves it to exactly one: heading UP, count 1, rows
    /// still 0 because `ShowLevelUps` defaults to FOLDED (Bevel's lock, Helm-signed
    /// 2026-09-02 — unlike `ShowSkillUps` beside it, which defaults open).
    /// </summary>
    [Fact]
    public void ProgressCard_TheLevelUpsFoldAppearsOnADingAndStaysShut()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_PROGRESS"] = "1",   // Experience, where the fold lives
        });
        app.Launch();

        app.WaitForWindow("progressLevelUpsShown", "the Progress window to open on Experience");
        // No heading over nothing: this character has never dinged while EQBuddy watched.
        app.WaitForDump("progressLevelUpsShown", 0, "no Level-ups heading before any ding");
        app.WaitForDump("progressLevelUps", 0, "no level-ups to count yet");

        app.AppendLogLines("You have gained a level! Welcome to level 12!");

        app.WaitForDump("progressLevelUpsShown", 1, "the Level-ups heading once a ding lands");
        app.WaitForDump("progressLevelUps", 1, "the ding counted on the folded label");
        // FOLDED by default — the count is on the label, the rows wait to be asked for.
        // This is the assertion that fails if someone "helpfully" defaults it open.
        app.WaitForDump("progressLevelUpRows", 0, "the rows to stay folded by default");
    }

    /// <summary>
    /// WHO OWNS THE PROGRESS BODY — pinned here BEFORE Inline themes PR 1 turns the
    /// launcher into an expander, which is the only order in which this assertion is
    /// worth anything.
    ///
    /// Today the Progress launcher is a plain <c>Button</c>, so the theme can only ever
    /// be Collapsed or in its Window and <c>progressInline</c> can only ever be 0. That
    /// is exactly what makes it worth writing down: after the move the same key can be 1,
    /// and the thing that must never happen — both at once — is the invariant
    /// <c>ThemeHost</c> exists to hold. On Avalonia it is not a layout bug but a crash
    /// (one control, one visual parent), so the rule is load-bearing on the build the WPF
    /// suite cannot see.
    ///
    /// The WPF layer has no unit tests (docs/TestPlan.md §5), so a launched app reporting
    /// its own state is the only cover the move can have.
    /// </summary>
    [Fact]
    public void TheProgressThemeHasExactlyOneOwnerBeforeTheInlineMove()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_PROGRESS"] = "faction",
        });
        app.Launch();

        app.WaitForWindow("progressWindowOpen", "the widget to report the theme's owner");
        app.WaitForDump("progressWindowOpen", 1, "EQBUDDY_PROGRESS to put the body in the window");
        // The launcher cannot expand yet. When PR 1 makes it able to, this assertion is
        // what says the window still wins — a card that ALSO drew the body would leave
        // the same surface in two hosts, which is trap 15 on WPF and an exception on the
        // other widget.
        Assert.Equal(0, app.DumpValue("progressInline"));
        // The glance survives whichever way the body is owned (#219: a changed glance is
        // a lost feature until proven otherwise).
        Assert.Equal(1, app.DumpValue("progressCard"));
        Assert.True(app.DumpValue("progressSummaryLen") > 0,
            "the launcher should summarise the theme; dump was: " + app.Artifacts());
    }

    /// <summary>
    /// The theme EXPANDED IN PLACE — the change Inline themes PR 1 makes, asserted in the
    /// same keys that said 0 before it.
    ///
    /// The card owning the body is not enough on its own: it has to own the STRIP too, and
    /// on the room a player who has not chosen gets. <c>progressTab</c>/<c>progressTabs</c>
    /// come from whichever host holds the body, so this also proves the widget is emitting
    /// them at all — before the move only the window ever did.
    /// </summary>
    [Fact]
    public void ExpandingTheProgressCardDrawsTheThemeUnderIt()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_EXPAND"] = "progress",
        });
        app.Launch();

        app.WaitForDump("progressInline", 1, "the Progress card to own the body");
        // The window is NOT open. Both at once is the one thing ThemeHost exists to
        // prevent, and on the Avalonia widget it is a crash rather than a layout bug.
        Assert.Equal(0, app.DumpValue("progressWindowOpen"));
        app.WaitForDump("progressTabs", 4, "all four rooms in the card's strip");
        // Experience — "the room that moves while you play" (Bevel). Its key is
        // "progress", the one the card has always used.
        app.WaitForDump("progressTab", "progress", "the default room");
        // The glance the launcher carried survives being expandable (#219).
        Assert.True(app.DumpValue("progressSummaryLen") > 0,
            "the header should still summarise the theme; dump was: " + app.Artifacts());
    }

    /// <summary>
    /// A named ROOM — <c>EQBUDDY_EXPAND=progress:raids</c> — and with it the first GLANCE
    /// tab, whose contract is that it draws a LINE instead of a body.
    ///
    /// Without the room selector three of the theme's four bodies could not be reached by
    /// a test or a screenshot at all, and a surface with no way to reach its state reads
    /// as reviewed anyway (trap 22).
    /// </summary>
    [Fact]
    public void AnInlineThemeCanBeOpenedOnANamedRoom()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_EXPAND"] = "progress:raids",
        });
        app.Launch();

        app.WaitForDump("progressInline", 1, "the Progress card to own the body");
        app.WaitForDump("progressTab", "raids", "the room named in EQBUDDY_EXPAND");
        app.WaitForDump("progressTabs", 4, "all four rooms in the card's strip");
    }

    /// <summary>The other half of the pin: with nothing opened, NEITHER host owns the
    /// body. Without this the test above is satisfied by a dump that reports 1 and 0 for
    /// every launch, whatever the app is actually doing — the vacuous-guard shape trap 39
    /// cost us a whole test file to.</summary>
    [Fact]
    public void TheProgressThemeStartsOwnedByNobody()
    {
        using var app = new AppHarness();
        app.Launch();

        app.WaitForWindow("progressWindowOpen", "the widget to report the theme's owner");
        app.WaitForDump("progressWindowOpen", 0, "no Progress window on a plain launch");
        Assert.Equal(0, app.DumpValue("progressInline"));
        // PR 2's two themes start the same way — collapsed, owned by nobody.
        Assert.Equal(0, app.DumpValue("killsInline"));
        Assert.Equal(0, app.DumpValue("killsWindowOpen"));
        Assert.Equal(0, app.DumpValue("lootInline"));
        Assert.Equal(0, app.DumpValue("lootWindowOpen"));
    }

    /// <summary>
    /// Inline themes PR 2: the KILLS &amp; DROPS card expands in place on its Full room,
    /// and its Drops room is the theme set's second GLANCE — it reads the wiki, which an
    /// expanded card over a running game must not do (Bevel's move).
    /// </summary>
    [Fact]
    public void TheKillsThemeExpandsInPlaceAndDropsIsAGlance()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_EXPAND"] = "kills:drops",
        });
        app.Launch();

        app.WaitForDump("killsInline", 1, "the Kills & Drops card to own the body");
        Assert.Equal(0, app.DumpValue("killsWindowOpen"));
        app.WaitForDump("killsTab", "drops", "the room named in EQBUDDY_EXPAND");
        app.WaitForDump("killsTabs", 2, "both rooms in the card's strip");
        // The glance the launcher carried survives being expandable (#219).
        Assert.True(app.DumpValue("killsSummaryLen") > 0,
            "the header should still summarise the theme; dump was: " + app.Artifacts());
    }

    /// <summary>Inline themes PR 2: the GEAR &amp; LOOT card, opened on its Glance room —
    /// Inventory, Bevel's host-rule case (a long list with its own filter bar).</summary>
    [Fact]
    public void TheGearThemeExpandsInPlaceAndInventoryIsAGlance()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_EXPAND"] = "loot:inventory",
        });
        app.Launch();

        app.WaitForDump("lootInline", 1, "the Gear & Loot card to own the body");
        Assert.Equal(0, app.DumpValue("lootWindowOpen"));
        app.WaitForDump("lootTab", "inventory", "the room named in EQBUDDY_EXPAND");
        app.WaitForDump("lootTabs", 3, "all three rooms in the card's strip");
        Assert.True(app.DumpValue("lootSummaryLen") > 0,
            "the header should still summarise the theme; dump was: " + app.Artifacts());
    }

    /// <summary>Inline themes PR 3: the QUESTS card expands in place. Epic is a Full
    /// room (one class's rows, capped — QuestInline's arrangement); General and Unlocks
    /// are Glances. General is also the DEFAULT (Bevel: "3 quests ready to turn in" is
    /// the thing a player expands the card to learn).</summary>
    [Fact]
    public void TheQuestsThemeExpandsInPlaceOnItsEpicRoom()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_EXPAND"] = "quests:epic",
        });
        app.Launch();

        app.WaitForDump("questsInline", 1, "the Quests card to own the body");
        Assert.Equal(0, app.DumpValue("questsHostWindowOpen"));
        app.WaitForDump("questsCardTab", "epic", "the room named in EQBUDDY_EXPAND");
        app.WaitForDump("questsCardTabs", 4, "all four rooms in the card's strip");
        Assert.True(app.DumpValue("questsSummaryLen") > 0,
            "the header should still summarise both checklists; dump was: " + app.Artifacts());
    }

    /// <summary>Opening a theme's WINDOW keeps the card collapsed — one owner, PR 2's
    /// lanes behaving exactly as Progress does.</summary>
    [Fact]
    public void TheKillsAndGearWindowsOwnTheirBodiesAlone()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_CREATURE"] = "kills",
            ["EQBUDDY_GEARLOOT"] = "loot",
        });
        app.Launch();

        app.WaitForDump("killsWindowOpen", 1, "EQBUDDY_CREATURE to put the body in the window");
        Assert.Equal(0, app.DumpValue("killsInline"));
        app.WaitForDump("lootWindowOpen", 1, "EQBUDDY_GEARLOOT to put the body in the window");
        Assert.Equal(0, app.DumpValue("lootInline"));
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
        // Four since #238: General, Epic 1.0, Plane of Sky, Unlocks.
        Assert.Equal(4, app.DumpValue("questsTabs"));
        Assert.Equal(5, app.DumpValue("questsModes"));
    }

    /// <summary>
    /// The Sky tab hands over the command that FEEDS it.
    ///
    /// A hand-in never appears in the log, so `/outputfile achievements` is the only thing
    /// that can say a Sky reward was turned in before EQBuddy existed — and until
    /// 2026-08-25 this surface named no way to produce one, with the copy living on the
    /// widget menu and the Raids card. Same absence the Gear tab had, and the same reason
    /// nothing caught it: a negative assertion cannot see a missing control (trap 34), and
    /// an absent control photographs as an unremarkable panel (trap 29).
    ///
    /// The count is read off the real visual tree, so this is the WPF lane's only proof
    /// that the button EXISTS rather than that the constant is referenced somewhere.
    /// </summary>
    [Fact]
    public void TheSkyTabOffersTheAchievementsCommandItRunsOn()
    {
        using var app = new AppHarness(
            environment: new Dictionary<string, string> { ["EQBUDDY_QUESTS"] = "sky" });
        app.Launch();

        Wait.Until(() => app.DumpValue("questsTabs") > 0, TimeSpan.FromSeconds(45),
            "the Quest Tracker to open on the Sky tab", app.Artifacts);
        Assert.Equal(1, app.DumpValue("questsSkyCopyCmd"));
    }

    /// <summary>
    /// Alt+Tab exclusion is off by default, and the window agrees with the setting.
    ///
    /// Both halves are reported because they are different claims: `altTabWanted` is what
    /// `settings.json` says and `altTabStyle` is the ex-style actually on the HWND. Trap
    /// 42 is exactly the gap between them — a feature genuinely in the binary, genuinely
    /// not in force, and indistinguishable from a stale build from anywhere but here.
    /// </summary>
    [Fact]
    public void TheWidgetStaysInAltTabUntilAskedNotTo()
    {
        using var app = new AppHarness();
        app.Launch();

        Assert.Equal(0, app.DumpValue("altTabWanted"));
        Assert.Equal(0, app.DumpValue("altTabStyle"));
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
