using System.Windows;
using EQBuddy.Core;

namespace EQBuddy;

/// <summary>
/// THE SCREENSHOT / REVIEW HOOK SWITCHBOARD — every <c>EQBUDDY_*</c> environment name
/// that opens a window at startup, in one place, lifted verbatim out of
/// <c>MainWindow</c>'s constructor.
///
/// **Why the lift, and why in this PR.** These sixteen hooks share one job and one
/// reason: *a surface that can only be opened by a human clicking a menu cannot be
/// photographed, and a surface nobody can review reads as reviewed anyway* (trap 22).
/// None of them is window LOGIC — every branch is `if (env) Loaded += … call a method` —
/// so they are exactly what the hotspot ratchet means by "lift a whole surface into its
/// own class the way QuestChecklistView was", and they were the only 135 contiguous
/// lines in the constructor that owed nothing to the widget's own state.
///
/// The widget was at 4,699 lines against a 4,700 limit when E-3 opened, with one line of
/// headroom left on purpose: Fable's plan makes that number E-3's decomposition budget
/// and requires the baseline to come down in the same commit as each move, or the freed
/// room quietly refills. The shell needed a field and a hook; this is what paid for them.
/// The baseline came down to match in <c>ArchitectureTests.Hotspots</c>.
///
/// **Registration ORDER is preserved exactly**, because these are <c>Loaded</c> handlers
/// and several open windows that stack: a re-ordering here would be invisible in a diff
/// and would show up as a screenshot of the wrong window on top (trap 24's failure mode
/// arriving through a different door). Nothing in the move is a rewrite.
/// </summary>
internal static class DebugHooks
{
    /// <summary>How many times the OE-2 door probe has driven the widget's <c>Guide…</c>
    /// row — <c>Open EQBuddy…</c> until 2026-09-08, when the faces folded the two shell/quest
    /// rows into one. Reported in the <c>EQBUDDY_EXPAND</c> dump so the suite has a positive
    /// event to wait on; 0 forever unless <c>EQBUDDY_DOORPROBE=1</c> armed it.</summary>
    internal static int DoorProbeClicks;

    /// <summary>How many times the pet-drop probe has driven a real DROP on the mini bar
    /// (SIGNED #422 §8). Reported in the <c>EQBUDDY_EXPAND</c> dump so the suite has a
    /// positive event to wait on; 0 forever unless <c>EQBUDDY_PETDROP=1</c> armed it.</summary>
    internal static int PetProbeDrops;

    /// <summary>Called once from the widget's constructor, at the point the block used to
    /// sit — after the tray icon and the item-catalog warm, before the What's-new notes.
    /// </summary>
    public static void Apply(MainWindow w)
    {
        // Screenshot/debug hook, same family as EQBUDDY_OPTIONS: open the Quest Tracker
        // after the startup replay has fed the ledger. "1" opens the default view;
        // "zone"/"all" open that mode directly.
        if (Environment.GetEnvironmentVariable("EQBUDDY_DROPS") == "1")
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(
                () => w.ShowCreatureWindow(CreatureTab.Drops),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Same family as EQBUDDY_PROGRESS / EQBUDDY_GEARLOOT: a theme window that can only
        // be opened from a card cannot be reviewed, and a surface nobody can review reads
        // as reviewed anyway (trap 22). A tab key opens it there; anything else opens it
        // on Kills.
        if (Environment.GetEnvironmentVariable("EQBUDDY_CREATURE") is { Length: > 0 } cvTab)
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(
                () => w.ShowCreatureWindow(CreatureSurface.TabForKey(cvTab) ?? CreatureTab.Kills),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_WIKIPACK") == "1")
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(w.ShowWikiPackWindow,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_QUESTS") is { Length: > 0 } questsMode)
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(() =>
            {
                w.ShowQuestsWindow();
                if (QuestSurface.TabForKey(questsMode.Split(':')[0]) is not null)
                    w._questsWindow?.SetTab(questsMode);
                else if (questsMode is "zone" or "all") w._questsWindow?.SetMode(questsMode);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Same family. The Progress window is where five card BODIES went, and a card
        // body has never been photographable except through a hook — so without this the
        // theme's four tabs could not be reviewed, and a surface nobody can review reads
        // as "reviewed" anyway (trap 22). "1" opens it on Experience; a tab key
        // (wealth / faction / raids) opens it there.
        if (Environment.GetEnvironmentVariable("EQBUDDY_PROGRESS") is { Length: > 0 } progressTab)
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(
                () => w.ShowProgressWindow(progressTab == "1" ? null : progressTab),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // The WORLD theme's window (World PR 2). Same family, and the one that was
        // missing: the Spawns tab deliberately stays hidden until a countdown exists, so
        // scripts/shoot.ps1 could never capture it and Gate 3 shipped without a
        // screenshot review. "1" opens the World window on Camps at the current zone;
        // EQBUDDY_MAP/EQBUDDY_TRAVEL open it on Map/Path — three separate env names for
        // one shared window now, kept apart because they already appear in shot fixtures
        // and docs.
        if (Environment.GetEnvironmentVariable("EQBUDDY_SPAWNS") is { Length: > 0 } spawnZone)
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(
                () => w.ShowWorldWindow(WorldTab.Camps, spawnZone == "1" ? null : spawnZone),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_OPTIONS") == "1")
            w.Loaded += (_, _) => w.OnOptions(w, new RoutedEventArgs());

        if (Environment.GetEnvironmentVariable("EQBUDDY_MAP") == "1")
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(() => w.ShowWorldWindow(WorldTab.Map),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_TRAVEL") == "1")
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(() => w.ShowWorldWindow(WorldTab.Routes),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // The theme's own name, added 2026-09-05 with HUD subtraction cut 2 — and it is
        // the cut that made it necessary rather than tidy. Three of the four World rooms
        // already had a hook (MAP / SPAWNS / TRAVEL); TRAVELS had none, because it was the
        // one room the widget drew itself, so `EQBUDDY_EXPAND=1` reached it for free. With
        // the card gone there was no way for a test or a shot to put the Travels body on
        // screen at all — trap 22, a surface with no fixture state reading as reviewed
        // anyway. "1" opens the World window on its default room (Travels); a tab key
        // (map / spawns / travel / misc, or any alias TabForKey answers) opens it there.
        if (Environment.GetEnvironmentVariable("EQBUDDY_WORLD") is { Length: > 0 } worldTab)
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(
                () => w.ShowWorldWindow(WorldSurface.TabForKey(worldTab) ?? WorldSurface.DefaultTab),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // EQBUDDY_INVENTORY and EQBUDDY_GEARLOCKER both open the same TAB now — the two
        // windows merged on 2026-08-20. Kept as separate names because both appear in
        // shot fixtures and docs, and a hook that silently stops working is worse than a
        // redundant one.
        if (Environment.GetEnvironmentVariable("EQBUDDY_INVENTORY") == "1")
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(() => w.OnGearLocker(w, new RoutedEventArgs()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_GEARLOCKER") == "1")
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(() => w.OnGearLocker(w, new RoutedEventArgs()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_TIMELINE") == "1")
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(w.OpenFightTimeline,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Screenshot/debug hook, same family as EQBUDDY_QUESTS: open straight into
        // archive review of the given file (#74), skipping the file dialog.
        if (Environment.GetEnvironmentVariable("EQBUDDY_REVIEW") is { Length: > 0 } reviewPath)
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(() => w.EnterReview(reviewPath),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        if (Environment.GetEnvironmentVariable("EQBUDDY_FEEDBACK") == "1")
            w.Loaded += (_, _) => w.OnFeedback(w, new RoutedEventArgs());

        // Same family as EQBUDDY_PROGRESS / EQBUDDY_QUESTS: a theme window that can only
        // be opened from a menu cannot be reviewed, and a surface nobody can review reads
        // as reviewed anyway (trap 22). "1" opens it on Loot; a tab key opens it there.
        if (Environment.GetEnvironmentVariable("EQBUDDY_GEARLOOT") is { Length: > 0 } glTab)
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(() =>
            {
                w.ShowGearLootWindow();
                if (LootSurface.TabForKey(glTab) is { } t) w._gearLootWindow?.SetTab(t);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // "1" opens on the newest session; "charts" opens with NOTHING selected and one
        // character filtered — the only state the cross-session level/AA charts render in
        // (RenderProgress needs a single-character filter AND no selection AND dings across
        // more than one session). Without this mode those charts could not be photographed
        // at all, which is how README's chart shot went stale with nobody able to re-take
        // it: a surface with no way to reach its state reads as reviewed anyway (trap 22).
        if (Environment.GetEnvironmentVariable("EQBUDDY_HISTORY") is { Length: > 0 } historyMode)
            w.Loaded += async (_, _) =>
            {
                await Task.Delay(4000); // let initial ingest finish
                w.OnHistory(w, new RoutedEventArgs());
                // Opened on the newest session rather than on "Select a session.": the
                // detail pane is most of this window and an empty one photographs as a
                // window that exists and holds nothing (trap 22).
                if (historyMode == "charts") w._historyWindow?.SelectFirstCharacterFilter();
                else w._historyWindow?.SelectNewest();
            };

        // The quick tour, on a page of your choosing (1-based). Same family, same reason
        // as the rest: without it the tour's five illustrations could not be reviewed
        // without a human installing the app and clicking Next, which is how they came to
        // be a month out of date with nobody noticing.
        if (Environment.GetEnvironmentVariable("EQBUDDY_TOUR") is { Length: > 0 } tourPage)
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(
                () => new TutorialWindow(w, int.TryParse(tourPage, out var n) ? n : 1).Show(),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // Edit HUD mode (Surface A / SA-4). Same family, same reason as the rest: the mode
        // is reached only by a human clicking the pencil on the expanded title bar (faces
        // §C, 2026-09-08) or the expanded menu's row behind it, so without this the four
        // Place/Mute chicklets and the Done exit could not be photographed or
        // asserted at all — trap 22, a surface with no way to reach its state reading as
        // reviewed anyway. It is deliberately NOT staged from a setting: "the profile says
        // edit mode" and "the affordances are on screen" are different claims (trap 42).
        if (Environment.GetEnvironmentVariable("EQBUDDY_HUDEDIT") == "1")
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(
                () => w.OnEditHud(w, new RoutedEventArgs()),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // THE MINI-BAR EXPANSION (OE-1), and this one is not a convenience — it is the only
        // way the feature can be asserted or photographed at all. Every state it has is
        // reached by a POINTER on a chip, and there is nothing in the E2E dump channel or in
        // `shoot.ps1` that moves a mouse. Without it the peek, the pin and the ✕ would be
        // trap 22 exactly: a surface with no way to reach its state, reading as reviewed.
        //
        // `dps` / `hps` / `progress` PIN the panel (the click, lock 4); a `:peek` suffix
        // hovers instead (lock 3). The two are spelled apart on purpose — they render the
        // identical panel, so a hook that could only do one of them would make every
        // screenshot of the pair a picture of the same state.
        if (Environment.GetEnvironmentVariable("EQBUDDY_HUDEXPAND") is { Length: > 0 } expandKey)
            w.Loaded += (_, _) => w.Dispatcher.BeginInvoke(() =>
            {
                var parts = expandKey.Split(':');
                if (UI.Shared.HudExpand.TargetForKey(parts[0]) is not { } target) return;
                if (parts.Length > 1 && parts[1].Equals("peek", StringComparison.OrdinalIgnoreCase))
                    w._hudExpandBar.Hover(target);
                else w._hudExpandBar.Click(target);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

        // THE ✕ ON A FLOATING WINDOW (OE-7), for the same reason as the hook above: the only
        // way to reach it is a pointer on a 11px glyph, and this suite has no pointer. It
        // drives `BreakoutWindow.Dismiss` — the method the mouse handler itself calls — so
        // what is asserted afterwards is the real close path and not a state a test set.
        //
        // **It has to run LATER than the rest of this file**, at Background priority behind
        // one more idle turn: a float only exists once `BreakoutHost.Update` has been given a
        // snapshot, which is the widget's first tick, and `Dismiss` on a window that has not
        // been built yet would be a hook that silently did nothing — the shape trap 62 warns
        // about, an assertion asking its question one moment too early. It retries until the
        // window is there rather than assuming, and gives up after a few seconds so a
        // genuinely-never-opening float fails the test's own wait instead of hanging here.
        if (Environment.GetEnvironmentVariable("EQBUDDY_BREAKOUTCLOSE") is { Length: > 0 } closeKind
            && Enum.TryParse<BreakoutKind>(closeKind, ignoreCase: true, out var toClose))
            w.Loaded += (_, _) =>
            {
                var tries = 0;
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200),
                };
                timer.Tick += (s, _) =>
                {
                    if (w._breakoutHost.Visible(toClose) is { } win)
                    {
                        ((System.Windows.Threading.DispatcherTimer)s!).Stop();
                        win.Dismiss();
                    }
                    else if (++tries > 25)
                        ((System.Windows.Threading.DispatcherTimer)s!).Stop();
                };
                timer.Start();
            };

        if (Environment.GetEnvironmentVariable("EQBUDDY_MENU") == "1")
            w.Loaded += (_, _) =>
            {
                if (w.RootBorder().ContextMenu is not { } m) return;
                m.StaysOpen = true;
                m.PlacementTarget = w.RootBorder();
                m.Placement = System.Windows.Controls.Primitives.PlacementMode.Left;
                m.IsOpen = true;
            };

        // THE DOOR PROBE (OE-2), and it is the one hook in this file that does not fire at
        // startup — because the state it has to reach does not exist at startup. The claim
        // under test is "the Guide row brings back a shell the player CLOSED", and
        // the close has to happen in the middle: a startup hook could only ever prove the
        // door opens a shell that was never opened, which is a reading of the code (the ✕
        // nulls the same field) dressed up as a measurement. Trap 62's shape — a guard
        // asking the right question at the wrong moment reads as coverage either way.
        //
        // The rendezvous is a file in the profile, dropped by `tests/EQBuddy.E2E`, because
        // the E2E channel is one-way (the app writes `debug.txt`, the suite reads it) and
        // the suite has no other way to say "now". It drives the SAME handler the menu row
        // drives — as `EQBUDDY_HUDEDIT` does for Edit HUD — so what it proves is the row's
        // own path, not a private one built for the test.
        if (Environment.GetEnvironmentVariable("EQBUDDY_DOORPROBE") == "1")
            w.Loaded += (_, _) =>
            {
                var trigger = AppPaths.File("door.trigger");
                var poll = new System.Windows.Threading.DispatcherTimer(
                    System.Windows.Threading.DispatcherPriority.Background)
                { Interval = TimeSpan.FromMilliseconds(200) };
                poll.Tick += (_, _) =>
                {
                    if (!System.IO.File.Exists(trigger)) return;
                    // Deleted BEFORE the click, so a door that throws cannot spin the timer
                    // on one trigger forever — and so the suite's next drop is a new event
                    // rather than a leftover.
                    try { System.IO.File.Delete(trigger); }
                    catch (System.IO.IOException) { return; }
                    w.OnGuideDoor(w, new RoutedEventArgs());
                    // AFTER the handler, and that ordering is the whole value of the
                    // counter: the file leaving says the probe SAW the trigger, and only
                    // this says the door has finished being asked. A suite that asserted
                    // "and the room did not change" off the first of those two would be
                    // asking one moment too early — trap 62, which passed a test with the
                    // feature under it deleted.
                    DoorProbeClicks++;
                };
                poll.Start();
            };

        // THE PET-DROP PROBE (SIGNED #422), the door probe's shape one surface over and for
        // the same reason: the state under test does not exist at startup. The claim is "a
        // DROP writes HudGlancePet and the bar redraws with the pet on the other row", and a
        // drop is the END of a gesture nothing in `tests/EQBuddy.E2E` can perform — the suite
        // cannot put a synthetic pointer on a control inside the widget and may not assert
        // the screen at all.
        //
        // So the rendezvous is a file in the profile, and it drives the SAME method a
        // mouse-up drives (`HudBarReorder.Land`), never a private path built for the test.
        // What it deliberately does NOT drive is the pointer arithmetic — which slot an x
        // lands in and what that landing MEANS are `MiniBarDrag.PetDropIndex` /
        // `DropKind`, unit-tested with no window.
        //
        // The trigger's content is "<key> <slot>" — "pet -1" inserts into the always-on row,
        // "pet 0" ejects to the head of the cells. A slot the bar cannot honour raises
        // nothing, so a staging mistake times out naming the probe instead of reading as a
        // feature that did not fire.
        if (Environment.GetEnvironmentVariable("EQBUDDY_PETDROP") == "1")
            w.Loaded += (_, _) =>
            {
                var trigger = AppPaths.File("hud-drop.trigger");
                var poll = new System.Windows.Threading.DispatcherTimer(
                    System.Windows.Threading.DispatcherPriority.Background)
                { Interval = TimeSpan.FromMilliseconds(200) };
                poll.Tick += (_, _) =>
                {
                    if (!System.IO.File.Exists(trigger)) return;
                    string text;
                    // READ before DELETE, and delete before the drop for the door probe's
                    // reason: a drop that throws must not spin the timer on one trigger
                    // forever, and the suite's next write must be a new event.
                    try
                    {
                        text = System.IO.File.ReadAllText(trigger);
                        System.IO.File.Delete(trigger);
                    }
                    catch (System.IO.IOException) { return; }
                    var parts = text.Trim().Split(' ',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length != 2 || !int.TryParse(parts[1], out var slot)) return;
                    if (!w._hudBar.ProbeDrop(parts[0], slot)) return;
                    // AFTER the drop, so a wait on this is a wait on the far side of the
                    // write (trap 62) rather than on the trigger file being noticed.
                    PetProbeDrops++;
                };
                poll.Start();
            };

        // The Evolved shell (E-3 PR 1). Its player door is the widget's "Guide…"
        // context-menu row since OE-2; this hook stays beside it, because a row a human has
        // to click cannot land a capture on a NAMED room and a shot of the default one
        // proves nothing about the other six (trap 22).
        ShellHost.ApplyEnvHook(w);
    }
}
