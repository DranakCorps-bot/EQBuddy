using System.Windows;
using EQBuddy.Core;

namespace EQBuddy;

/// <summary>
/// The <c>EQBUDDY_EXPAND</c> debug dump — the WPF widget's only test seam
/// (docs/TestPlan.md §5: this layer has no unit tests, so facts go into the dump and
/// <c>tests/EQBuddy.E2E</c> asserts them from a launched app).
///
/// Lifted out of <c>MainWindow.RefreshUi</c> as the first commit of Inline themes PR 2,
/// exactly as the plan's ratchet amendment prescribed: ~140 lines of pure string-building
/// — a sum, not a pixel — that the hotspot glob was paying for. NOT a partial, because
/// <c>ArchitectureTests</c> sums partials on purpose. It reads MainWindow's internals; if
/// this file starts needing LOGIC rather than formatting, that logic belongs in
/// Core/UI.Shared instead.
/// </summary>
internal static class WidgetDump
{
    /// <summary>The cap in force on whichever theme card currently owns a body, AND the two
    /// inputs it was computed from, or the floor when no card owns one (#250). Only one
    /// theme is ever inline in the review set, and with none open the floor is the honest
    /// answer: nothing is being capped.
    ///
    /// **The inputs travel with the answer, from one selection.** A test that has only the
    /// cap can compare it to a constant, and the constant is a claim about the MONITOR —
    /// the room is clamped to the work area, so a 4000-unit drag on a 1024x768 hosted
    /// runner correctly yields the floor and "the body grew" cannot be asserted there. With
    /// room and chrome in the dump, E2E asserts cap == ThemeBodyCap(room, chrome) against
    /// the control's real MaxHeight, which holds on every screen.</summary>
    private static (double Cap, double Room, double Chrome) ThemeBodyFactsInForce(MainWindow w) =>
        w._progressHost.IsInline ? Facts(w, w._progressCard, w.ProgressSection)
        : w._creatureHost.IsInline ? Facts(w, w._killsCard, w.KillsSection)
        : w._lootHost.IsInline ? Facts(w, w._lootCard, w.LootSection)
        // Quests and World are absent here on purpose: neither has had a card since
        // 2026-09-05 (HUD subtraction cuts 1 and 2), so neither host can be Inline and
        // there is no body to measure. Their window placements are still reported, as
        // questsHostWindowOpen and worldWindowOpen.
        //
        // World was the LAST theme card in the EQBUDDY_EXPAND=1 review set, so a bare
        // EXPAND=1 launch now falls through to the floor here — correctly, since nothing
        // is being capped. The E2E scenarios about a capped body name their card.
        : (EQBuddy.UI.Shared.WidgetMetrics.ThemeBodyMaxHeight, double.NaN, double.NaN);

    private static (double Cap, double Room, double Chrome) Facts<TTab>(
        MainWindow w, ThemeCardView<TTab> card, System.Windows.Controls.Expander section)
        where TTab : struct, Enum =>
        (card.BodyCap, ThemeBodyCapHost.RoomFor(w),
         ThemeBodyCapHost.ChromeFor(w, section, card.BodyChrome));

    /// <summary>
    /// ONE MOMENT PER DUMP: bring every open satellite level with the snapshot this dump
    /// is about to report, before reading a single row count off it.
    ///
    /// **The widget's totals and a window's row counts sit in one dump line and used to
    /// describe two different moments.** Each satellite throttles its follow tick — one
    /// second for Kills &amp; Drops and Gear &amp; Loot, two for Progress and Quests, three
    /// for the wiki pack — so `kills` could be a whole creature behind `killsTotal`, for
    /// seconds, with nothing wrong anywhere.
    ///
    /// That cost the E2E suite four rounds on a hosted runner. A test samples a row count
    /// as its baseline, appends a line and waits for baseline + 1, and `WaitForDump` is an
    /// EQUALITY — so a window still catching up sails PAST the expected number between two
    /// polls and the wait can never be satisfied again (`SessionGoesLive…`: "kills to reach
    /// 14; last seen 13", beside a dump reading `ingestDone=1 logPending=0 killKinds=14` —
    /// the log fully read, the data complete, and the window one row short).
    ///
    /// **The first three rounds guessed at "settled" from stillness; the fourth asked the
    /// app and then waited for an answer the throttles alone were never obliged to give.**
    /// Reporting `surfacesBehind` made the disagreement VISIBLE, which was the right half
    /// of the lesson and only half: a dump that says "these two numbers are a tick apart"
    /// is still a dump carrying two moments. This closes it — trap 56's own general rule,
    /// taken to its second clause: *say which moment each number came from, or MAKE THEM
    /// COME FROM THE SAME ONE.*
    ///
    /// Costs a player nothing: the whole path is behind the <c>EQBUDDY_EXPAND</c> gate,
    /// which already opens every card. It is not free licence either — <c>PaintNow</c> is
    /// the window's own throttled paint with the throttle skipped, never a heavier one
    /// (Gear &amp; Loot stays <c>force: false</c> so the Inventory tab does not re-scan the
    /// game folder, and the wiki pack's lookups are keyed per creature, not per paint).
    /// </summary>
    private static void PaintOneMoment(MainWindow w, long version)
    {
        foreach (var surface in FollowingSurfaces.OpenOn(w))
            if (surface.RenderedVersion != version) surface.PaintNow();
    }

    /// <summary>How many OPEN satellite windows have NOT painted this tick's snapshot —
    /// zero by construction now that <see cref="PaintOneMoment"/> runs first, and kept as
    /// the assertion that it IS. A non-zero here means a window's paint did not record the
    /// version it painted, which is the one way the guarantee above can rot silently.</summary>
    private static int SurfacesBehind(MainWindow w, long version) =>
        FollowingSurfaces.OpenOn(w).Count(s => s.RenderedVersion != version);

    /// <summary>Cards on the widget the player can actually see — the panel's children
    /// minus whatever is hidden in Options → Cards &amp; windows. Read off the SAME panel
    /// ApplySectionLayout fills, so it counts what is on screen rather than re-deriving it
    /// from the catalog, which is the half a subtraction could get wrong (a key can leave
    /// the catalog and stay in the map, and that pair is what throws on startup).</summary>
    private static int CountVisible(MainWindow w) =>
        w.SectionsPanel.Children.OfType<System.Windows.FrameworkElement>()
            .Count(e => e.Visibility == Visibility.Visible);

    /// <summary>A dump value is an integer the suite parses, and -1 is its "absent". A
    /// measurement that has not happened (NaN — never dragged, or a card the layout has not
    /// reached) is exactly that, so it is spelled -1 rather than "NaN".</summary>
    private static double Dumpable(double value) => double.IsFinite(value) ? value : -1;

    /// <summary>Write the dump when the EXPAND gate is up. Same guard, same file, same
    /// keys as the block always had — the E2E suite's assertions are the contract.</summary>
    public static void MaybeWrite(MainWindow w, StatsSnapshot s)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EQBUDDY_EXPAND")))
        {
            try
            {
                // FIRST, before anything is read: every open satellite paints THIS
                // snapshot, so the row counts below and the totals beside them are one
                // moment. See PaintOneMoment.
                PaintOneMoment(w, s.Version);
                // One selection of the card that owns a body, read once: the cap and the
                // two inputs it came from have to describe the SAME card.
                var body = ThemeBodyFactsInForce(w);
                // Row counts say "a new name appeared"; the snapshot totals say "the
                // session moved" — the E2E suite (tests/EQBuddy.E2E) asserts on both.
                var dump = $"dmgSrc={w.DamageSourceList.Items.Count} dmgTaken={w.DamageTakenList.Items.Count} " +
                    // The KILLS & DROPS launcher card (docs/Themes.md). It replaced the
                    // Kills card, whose row counts used to be asserted here; what a reader
                    // sees now is one line, so that is what this pins. The ROWS moved with
                    // the surface into CreatureWindow.DebugFacts() below, where the same
                    // E2E assertions read them — the point being that they are the SAME
                    // numbers out of a new host.
                    $"killsCard={(w.KillsSection.Visibility == Visibility.Visible ? 1 : 0)} " +
                    $"killsSummaryLen={w.KillsHeader.Text.Length} " +

                    // The PROGRESS THEME's launcher card (docs/Themes.md). It replaced
                    // five cards whose row counts used to be asserted here; what a reader
                    // sees now is one line, so that is what this pins — the card is
                    // present, and folding five cards into it kept their numbers on
                    // screen rather than quietly losing the glance. Exactly the shape
                    // questsCard/questsSummaryLen took when the quest cards folded.
                    //
                    // The ROWS moved with the surfaces, into ProgressWindow.DebugFacts()
                    // below, where the same E2E assertions read them.
                    $"progressCard={(w.ProgressSection.Visibility == Visibility.Visible ? 1 : 0)} " +
                    $"progressSummaryLen={w.ProgressHeader.Text.Length} " +
                    // WHO OWNS THE PROGRESS BODY. Pinned here while the launcher is still
                    // a plain Button, so Inline themes PR 1 has to keep them true rather
                    // than define them: today progressInline can only ever be 0, and the
                    // assertion that it IS 0 is what makes the 1 mean something later.
                    //
                    // The two are never both 1 — that is ThemeHost's one invariant, and
                    // on Avalonia it is what keeps the app from throwing (one control,
                    // one visual parent). progressTab/progressTabs stay the WINDOW's to
                    // report while it is up: DumpValue takes the FIRST match in the file,
                    // so two emitters of one key is not a conflict the suite can see.
                    $"progressInline={(w._progressHost.IsInline ? 1 : 0)} " +
                    $"progressWindowOpen={(w._progressHost.IsWindowOpen ? 1 : 0)} " +
                    // The CARD's strip, and only while the card owns the body. The window
                    // reports the same two keys from its own DebugFacts, and DumpValue
                    // takes the FIRST match in the file — so emitting both at once would
                    // not be a conflict the suite could see. One owner of the body, one
                    // owner of the keys that describe it.
                    (w._progressHost.IsInline
                        ? $"progressTab={ProgressSurface.KeyFor(w._progressCard.SelectedTab)} " +
                          $"progressTabs={w._progressCard.TabCount} "
                        : "") +
                    // #250: the expanded theme body's cap, and the height it is derived
                    // from. 320 on a widget nobody has dragged — which is the assertion
                    // that matters, because "pixel-identical until you touch the grip" is
                    // the whole safety of the change and nothing else can see it. The
                    // WPF layer has no unit tests (docs/TestPlan.md §5), and an absent
                    // control photographs as an unremarkable panel (trap 29), so a
                    // screenshot could never say what number is in force.
                    // Has the startup replay FINISHED? The watcher's own answer, which is
                    // the only honest one: every test samples a baseline and waits for
                    // baseline + 1, and a counter still climbing through the fixture sails
                    // past the expected value between two polls. Quiet is not the same as
                    // done — a slow machine pauses mid-ingest, and the two hosted-runner
                    // flakes this key exists for both read as "settled" to a harness that
                    // was watching for stillness instead of asking.
                    $"ingestDone={(w._watcher.InitialIngestDone ? 1 : 0)} " +
                    // And the OTHER half of "is this dump settled?", which ingestDone
                    // cannot answer: how many open satellite windows have not yet painted
                    // the snapshot the totals below came from. See SurfacesBehind — the
                    // log being read and the windows being drawn are two facts, and the
                    // E2E flake this pair exists for needed both.
                    $"surfacesBehind={SurfacesBehind(w, s.Version)} " +
                    $"themeBodyCap={body.Cap:0} " +
                    // ...and the two numbers it was computed FROM, so a test can assert the
                    // relationship instead of a constant. -1 means "no measurement": the
                    // widget was never dragged, or no card owns a body.
                    $"themeBodyRoom={Dumpable(body.Room):0} " +
                    $"themeBodyChrome={Dumpable(body.Chrome):0} " +
                    // Its own key rather than a sentinel inside contentHeight: DumpValue
                    // answers -1 for "absent", so a NaN spelled as -1 would be a value the
                    // suite cannot tell from a dump that never mentioned it.
                    $"contentHeightAuto={(double.IsNaN(w._settings.ContentHeight) ? 1 : 0)} " +
                    $"contentHeight={(double.IsNaN(w._settings.ContentHeight) ? 0 : w._settings.ContentHeight):0} " +
                    // The other two themes' placement, PR 2 - same contract as the
                    // progress keys above: inline and windowOpen are never both 1, and
                    // the tab keys are emitted only while the CARD owns the body.
                    $"killsInline={(w._creatureHost.IsInline ? 1 : 0)} " +
                    $"killsWindowOpen={(w._creatureHost.IsWindowOpen ? 1 : 0)} " +
                    (w._creatureHost.IsInline
                        ? $"killsTab={CreatureSurface.KeyFor(w._killsCard.SelectedTab)} " +
                          $"killsTabs={w._killsCard.TabCount} "
                        : "") +
                    $"lootInline={(w._lootHost.IsInline ? 1 : 0)} " +
                    $"lootWindowOpen={(w._lootHost.IsWindowOpen ? 1 : 0)} " +
                    (w._lootHost.IsInline
                        ? $"lootTab={LootSurface.KeyFor(w._lootCard.SelectedTab)} " +
                          $"lootTabs={w._lootCard.TabCount} "
                        : "") +
                    // Quests has no card to be inline in since 2026-09-05 (HUD subtraction
                    // cut 1), so questsInline/questsCardTab/questsCardTabs are gone with
                    // it. What is left is the only placement the surface still has, and
                    // the one this cut has to keep true: the WINDOW opens, from the
                    // context-menu row, the hotkey, or EQBUDDY_QUESTS.
                    $"questsHostWindowOpen={(w._questsHost.IsWindowOpen ? 1 : 0)} " +
                    $"raidsDefeated={w._raidLedger.DefeatedCount()} " +
                    // The WORLD theme's placement. `worldInline`, and the `worldTab`/
                    // `worldTabs` pair the CARD emitted, went with the card on 2026-09-05
                    // (HUD subtraction cut 2) — the same shape as Quests above. The window
                    // still reports worldTab/worldTabs from its own DebugFacts(), which is
                    // where WorldOpenersTests reads them; the widget no longer has an
                    // opinion, so the dump can no longer carry two answers to one key
                    // (trap 58, avoided by subtraction rather than by prefixing).
                    $"worldWindowOpen={(w._worldHost.IsWindowOpen ? 1 : 0)} " +
                    // THE WIDGET'S OWN TravelsView DUMPED zones/deaths/travelsMarkers HERE
                    // (World PR 1) and went with the card. Those three keys are still in
                    // the dump — from `WorldWindow.DebugFacts()`, off the window's own
                    // instance — but only while that window is open AND on Travels, since
                    // Refresh paints the visible tab alone (trap 46). `EQBUDDY_WORLD=1` is
                    // the hook that puts it there, and it exists because this cut removed
                    // the last default way to reach that body from a test or a shot
                    // (trap 22: a surface with no fixture state reads as reviewed anyway).
                    $"killsTotal={s.YourKillCount} lootTotal={s.LootTotal} " +
                    // The DATA's distinct-creature count, beside the window's RENDERED
                    // one (`kills`, from CreatureWindow.DebugFacts). Two keys for one
                    // fact on purpose — and since PaintOneMoment they are read off the
                    // SAME snapshot, so they are now an EQUALITY the suite can assert
                    // rather than two moments it has to reconcile. A run where they
                    // disagree is a render bug; a run where they agree and the total is
                    // wrong is a parse bug. One CI failure showed kills=13 against
                    // killKinds=14 and nothing else in the dump could say which was lying.
                    $"killKinds={s.YourKills.Count} lootKinds={s.Loot.Count} " +
                    // How many times RefreshUi has run. Nothing asserts a value; it is
                    // there so the E2E harness can tell "this counter will never move"
                    // from "this APP is no longer moving" — two failures that look
                    // identical from outside and cost a whole round apart.
                    $"tick={w._uiTicks} " +
                    // …and whether the tail has anything left to read. See
                    // LogWatcher.PendingBytes: a total that will not move with bytes
                    // pending is a stalled TAIL; the same total with 0 pending is a line
                    // that parsed and did not count.
                    $"logPending={w._watcher.PendingBytes} " +
                    // …and whether the log was re-SELECTED, which resets the session and
                    // replays the file. 1 is a normal launch.
                    $"logSelects={w._watcher.SelectCount} " +
                    $"tracked={s.Tracked.Sum(t => t.TotalQuantity)} " +
                    // The Watch card's RENDERED shape, not just its total. The total
                    // above proves the data arrived; these prove the card drew it, and
                    // they exist because this surface is about to be lifted into a file
                    // of its own — the WPF layer has no unit tests (docs/TestPlan.md §5),
                    // so an assertion from a launched app is the only thing standing
                    // between that move and a silent regression. Row count, whether the
                    // sort strip is up (it appears only above two or more rules), and
                    // which sort is lit.
                    // The PROGRESS card's rendered shape, for the same reason and in the
                    // same week: it is the next surface being lifted out. "skills" above
                    // proves the data arrived; these prove the card drew the three lists
                    // that are easy to lose in a move — the ding unlocks (shown only when
                    // a level was announced this session), the next-milestone preview
                    // (hidden until a level is known at all, and folded behind a setting)
                    // and the AA split into session-new vs the full ledger.
                    // THE COLLAPSED HUD BAR's rendered cell count, pinned here BEFORE the
                    // surface moves (Surface A / SA-1). No mini-bar fact has ever existed
                    // in this dump, and the WPF layer has no unit tests (docs/TestPlan.md
                    // §5) — so this assertion, green on the pre-move tree, is the only
                    // thing standing between the lift into HudBarView and a silent
                    // regression. Exactly what watchRows/progress*/gear* did for the four
                    // surfaces lifted before it.
                    //
                    // It counts what is ON THE BAR: one cell per starred stat, plus one
                    // per pinned watch rule. Zero while the widget is expanded, because
                    // UpdateMiniChips only runs while MiniRoot is visible.
                    $"hudCells={w._hudBar.CellCount} " +
                    // …and WHICH number the glance's third slot currently is: "xp" or
                    // "hps". A word, not a count, because the dump is space-separated
                    // key=value and the suite has a string wait for exactly this shape.
                    //
                    // The SWAP is the one piece of SA-1 that a screenshot cannot settle:
                    // both states render correctly and look equally right, so only the app
                    // can say which rule fired. HudGlance decides it and is unit-tested
                    // with no window; this proves the decision reaches the control, which
                    // is the half a unit test cannot see (trap 42).
                    $"hudGlance={w._hudBar.GlanceKey} " +
                    // THE ONE CHIP ROW (Surface A / SA-2). Four keys, because the fold has
                    // four separable ways to go wrong and a single "is the row up" could
                    // not tell them apart: the row's presence, each family's contribution,
                    // and how many chicklets are showing a DUE face.
                    //
                    // hudChipsRow is the ROW WINDOW's own visibility, which is the half a
                    // count cannot claim — "present in the build" and "on screen" are
                    // different claims (trap 42), and this row replaced two windows whose
                    // whole job was being on screen at the right moment. The per-family
                    // counts come from the merge rather than off the panel, so a family
                    // that silently stopped contributing is visible as a 0 beside a live
                    // row rather than as an absence nothing names (trap 20's shape).
                    //
                    // Emitted whether or not the row exists: a key that disappears with its
                    // window is a key a test cannot assert is ZERO, and "the spawn family
                    // left the row" is exactly the assertion the Camps hide-rule needs.
                    $"hudChipsRow={(w._hudChips is { IsVisible: true } ? 1 : 0)} " +
                    $"hudChipsMez={w._hudChips?.MezChips ?? 0} " +
                    $"hudChipsSpawn={w._hudChips?.SpawnChips ?? 0} " +
                    // SA-3's two net-new families. Separate keys rather than a total for the
                    // reason above: "the buff family stopped contributing" and "the row is
                    // empty" are different failures and a sum tells them apart never.
                    $"hudChipsWatch={w._hudChips?.WatchChips ?? 0} " +
                    $"hudChipsBuff={w._hudChips?.BuffChips ?? 0} " +
                    $"hudChipsDue={w._hudChips?.DueChips ?? 0} " +
                    // The DATA behind the buff family, beside the family's rendered count —
                    // so "no chip because nothing is expiring" and "no chip because nothing
                    // landed" are two readings rather than one absence. Without it the
                    // negative assertion (a buff outside its warning window earns no chip)
                    // passes just as well against a tracker that never saw the landing at
                    // all, which is trap 56's lesson about a wait needing a liveness question
                    // as well as a value one.
                    $"buffsActive={w._buffTracker.ActiveCount} " +
                    $"watchRows={w._watch.RowCount} " +
                    $"watchStrip={(w._watch.SortStripShown ? 1 : 0)} " +
                    $"watchSort={w._settings.WatchSortMode} " +
                    // The GEAR card's rendered shape, pinned for the same reason and in
                    // the same way as the two above: it is the next surface to be lifted
                    // out (the Gear & Loot theme), and the WPF layer has no unit tests,
                    // so an assertion from a launched app is the only thing standing
                    // between that move and a silent regression.
                    //
                    // The gear numbers themselves moved with the surface, into
                    // GearLootWindow.DebugFacts() below — same keys, new host, which is
                    // exactly what the E2E assertions are for.
                    $"lootCard={(w.LootSection.Visibility == Visibility.Visible ? 1 : 0)} " +
                    $"lootSummaryLen={w.LootHeader.Text.Length} " +
                    $"actualH={w.ActualHeight:0} actualW={w.ActualWidth:0} " +
                    // Geometry, for the E2E wiring check. WidgetMetrics is unit-tested,
                    // but only a launched app can show that its answer actually reaches
                    // the control — which is the half of #144 a unit test cannot see.
                    // uiScale is ×100 because the dump carries integers.
                    $"uiScale100={w._settings.UiScale * 100:0} " +
                    $"sectionCapScreen={w._sectionAutoCap:0} " +
                    $"sectionMaxH={w.SectionScroll.MaxHeight:0} " +
                    // HOW MANY CARDS THE WIDGET IS ACTUALLY DRAWING, and how many of them
                    // the player can see. `cards` is the catalog's length as realised in
                    // the panel — eight since 2026-09-05, when Quests and then World left
                    // (HUD subtraction cuts 1 and 2) — and `cardsVisible` subtracts
                    // whatever is hidden in Options.
                    //
                    // A COUNT rather than a per-card key, deliberately: `questsCard=1` was
                    // the old shape and it could only ever say something about the card it
                    // was named after. A subtraction is a claim about the STACK, and the
                    // next cut wants the same assertion without anyone editing this file.
                    // Cut 2 (the World card, 2026-09-05) is the first to collect on that:
                    // eight cards now, and not a line of CODE here changed to say so —
                    // only the count in the E2E assertion and the sentence above.
                    $"cards={w.SectionsPanel.Children.Count} " +
                    $"cardsVisible={CountVisible(w)} " +
                    // The checklists are still BUILT with no card of their own to render
                    // into — they feed the Quest Tracker, the Evolved shell's Quests room
                    // and EQBuddy Mobile, and the loot auto-checkers tick them whether or
                    // not anything is on screen. That is what these two pin now that the
                    // launcher line is gone.
                    $"questsEpicTotal={w._settings.EpicQuestChecklist.Count} " +
                    $"questsSkyTotal={w._settings.SkyQuestChecklist.Count} " +
                    // The Quest Tracker WINDOW, when EQBUDDY_QUESTS opened one. The WPF
                    // layer has no unit tests (docs/TestPlan.md §5), so the Gate 2
                    // rebuild's structure — list rows, a selection, a populated detail
                    // pane — is only assertable from a launched app. The window formats
                    // its own facts; this just carries them.
                    (w._questsWindow is { IsLoaded: true } qwin ? qwin.DebugFacts() + " " : "") +
                    // The Progress WINDOW, when EQBUDDY_PROGRESS opened one. The five
                    // surfaces it hosts were pinned on the widget before the fold; this
                    // is where those same numbers come out now, and the point of the
                    // assertion is that they are the SAME numbers.
                    (w._progressWindow is { IsLoaded: true } pwin ? pwin.DebugFacts() + " " : "") +
                    // The Gear & Loot WINDOW, when EQBUDDY_GEARLOOT opened one. Its gear
                    // numbers are the ones pinned on the widget before the lift; the
                    // point of the assertion is that they are the SAME numbers.
                    (w._gearLootWindow is { IsLoaded: true } glwin ? glwin.DebugFacts() + " " : "") +
                    // The Wiki contribution pack WINDOW, when EQBUDDY_WIKIPACK opened one:
                    // its rows and its re-check button's target count (#226).
                    (w._wikiPackWindow is { IsLoaded: true } wpwin ? wpwin.DebugFacts() + " " : "") +
                    // The Kills & Drops WINDOW, when EQBUDDY_DROPS or EQBUDDY_CREATURE
                    // opened one. Its drops numbers are the ones pinned on the OLD host
                    // before the lift, and its kills numbers the ones pinned on the widget
                    // before the fold; the point of the assertion is that they are the SAME
                    // numbers.
                    (w._creatureWindow is { IsLoaded: true } cwin ? cwin.DebugFacts() + " " : "") +
                    // The WORLD theme's window (World PR 2 — replaces the three
                    // standalone windows the three keys above used to come from). Same
                    // reason as every DebugFacts() above: the WPF layer has no unit
                    // tests, so these numbers are pinned from a launched app and must
                    // read the same after the fold as they did on the old hosts.
                    (w._worldWindow is { IsLoaded: true } wwin ? wwin.DebugFacts() + " " : "") +
                    // The SESSION HISTORY studio, when EQBUDDY_HISTORY opened one. Added in
                    // E-3 S3, when the Evolved Progress room took the career BROWSE and this
                    // window kept the four jobs the browse cannot do (compare, notes, export,
                    // delete/import). It has ONE door — the widget's context menu — and no
                    // unit tests, so this is the only thing that can say it still opens
                    // beside the room rather than being quietly retired by a cleanup.
                    (w._historyWindow is { IsLoaded: true } hwin ? hwin.DebugFacts() + " " : "") +
                    // The EVOLVED SHELL, when EQBUDDY_SHELL opened one (E-3 PR 1). It has
                    // no player-facing door yet, so this is the only thing besides a
                    // screenshot that can say the rail drew, the Search affordance exists
                    // and the room painted — and an absent control photographs as an
                    // unremarkable window (trap 29), so a picture alone would not.
                    // Its Progress numbers come out under shellProgress* BESIDE the
                    // window's progress* keys on purpose: two hosts of one room is
                    // exactly where a silent divergence would live.
                    (w._shellWindow is { IsLoaded: true } shwin ? shwin.DebugFacts() + " " : "") +
                    // EQBuddy Mobile's pump: it should be running, and it should be
                    // doing nothing, because this profile has no paired device.
                    $"companionPumpTicks={w._companionPumpTicks} " +
                    $"companionPushes={w._companionPushes} " +
                    // Alt+Tab (Hateborne, 2026-08-25). Reported as the EFFECT — the ex-style
                    // actually on the HWND — not as the setting, because "present in the
                    // build" and "in effect at runtime" are different claims and trap 42
                    // cost two builds to learn it. The setting is beside it so a
                    // disagreement between the two is visible rather than inferable.
                    $"altTabWanted={(w._settings.HideFromAltTab ? 1 : 0)} " +
                    $"altTabStyle={(NoActivate.IsToolWindow(w) ? 1 : 0)} " +
                    // The bit that defeated the one above for a week (Hateborne,
                    // 2026-09-03): WPF asserts WS_EX_APPWINDOW for ShowInTaskbar=true,
                    // and APPWINDOW overrides TOOLWINDOW for switcher membership. Hidden
                    // means style=1 AND appWindow=0, and only the HWND can say so.
                    $"altTabAppWindow={(NoActivate.HasAppWindowStyle(w) ? 1 : 0)} " +
                    $"altTabTaskbar={(w.ShowInTaskbar ? 1 : 0)}";
                System.IO.File.WriteAllText(Core.AppPaths.File("debug.txt"), dump);
            }
            catch { }
        }
    }
}
