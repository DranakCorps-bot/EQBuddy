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
    /// <summary>Write the dump when the EXPAND gate is up. Same guard, same file, same
    /// keys as the block always had — the E2E suite's assertions are the contract.</summary>
    public static void MaybeWrite(MainWindow w, StatsSnapshot s)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EQBUDDY_EXPAND")))
        {
            try
            {
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
                    $"raidsDefeated={w._raidLedger.DefeatedCount()} " +
                    $"zones={w.ZoneList.Items.Count} deaths={w.DeathList.Items.Count} " +
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
                    $"altTabStyle={(NoActivate.IsToolWindow(w) ? 1 : 0)}";
                System.IO.File.WriteAllText(Core.AppPaths.File("debug.txt"), dump);
            }
            catch { }
        }
    }
}
