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
        : w._questsHost.IsInline ? Facts(w, w._questsCard, w.QuestsSection)
        : w._worldHost.IsInline ? Facts(w, w._worldCard, w.MiscSection)
        : (EQBuddy.UI.Shared.WidgetMetrics.ThemeBodyMaxHeight, double.NaN, double.NaN);

    private static (double Cap, double Room, double Chrome) Facts<TTab>(
        MainWindow w, ThemeCardView<TTab> card, System.Windows.Controls.Expander section)
        where TTab : struct, Enum =>
        (card.BodyCap, ThemeBodyCapHost.RoomFor(w),
         ThemeBodyCapHost.ChromeFor(w, section, card.BodyChrome));

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
                    (w._questsHost.IsInline
                        ? $"questsInline=1 questsCardTab={QuestSurface.KeyFor(w._questsCard.SelectedTab)} " +
                          $"questsCardTabs={w._questsCard.TabCount} "
                        : "questsInline=0 ") +
                    $"questsHostWindowOpen={(w._questsHost.IsWindowOpen ? 1 : 0)} " +
                    $"raidsDefeated={w._raidLedger.DefeatedCount()} " +
                    // The WORLD theme's placement (World PR 3) — same contract as the
                    // three above: inline and windowOpen are never both 1, and the tab
                    // keys are emitted only while the CARD owns the body.
                    $"worldInline={(w._worldHost.IsInline ? 1 : 0)} " +
                    $"worldWindowOpen={(w._worldHost.IsWindowOpen ? 1 : 0)} " +
                    (w._worldHost.IsInline
                        ? $"worldTab={WorldSurface.KeyFor(w._worldCard.SelectedTab)} " +
                          $"worldTabs={w._worldCard.TabCount} "
                        : "") +
                    // The Travels tab's body, lifted into TravelsView (World PR 1). Same
                    // keys (zones/deaths) the misc card always dumped, plus travelsMarkers
                    // (never pinned before this PR) — DebugFacts() carries all three.
                    $"{w._travelsView.DebugFacts()} " +
                    $"killsTotal={s.YourKillCount} lootTotal={s.LootTotal} " +
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
                    // The Quests card (2026-08-16). It replaced the Epic and Sky cards,
                    // whose tab and row counts used to be asserted here. What a reader
                    // sees now is one launcher line, so that is what E2E pins: the card
                    // is present, and folding two cards into it kept BOTH checklists'
                    // counts on screen rather than quietly losing the glance.
                    $"questsCard={(w.QuestsSection.Visibility == Visibility.Visible ? 1 : 0)} " +
                    $"questsEpicTotal={w._settings.EpicQuestChecklist.Count} " +
                    $"questsSkyTotal={w._settings.SkyQuestChecklist.Count} " +
                    $"questsSummaryLen={w.QuestsHeader.Text.Length} " +
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
