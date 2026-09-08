using EQBuddy.Core;

namespace EQBuddy.E2E;

/// <summary>
/// The COLLAPSED HUD bar — the surface that is on screen for the whole time a player is
/// farming, and the one the widget had never pinned a single fact about.
///
/// Written BEFORE the bar leaves <c>MainWindow</c> for <c>HudBarView</c> (Surface A /
/// SA-1), for the reason every lift in this repo has needed one: the WPF layer has no
/// unit tests (docs/TestPlan.md §5), so an assertion from a launched app is the only
/// thing standing between a move and a silent regression. <c>hudCells</c> is green on
/// the pre-move tree first, then must read the same after the move.
///
/// [Collection("e2e")] because every test here launches a real always-on-top widget and
/// two of them at once would fight for the desktop — the shared-state race trap 57
/// names, which is a fact about the SESSION rather than about any one class.
/// </summary>
[Collection("e2e")]
public sealed class HudBarTests
{
    /// <summary>
    /// One cell per starred stat, and one per PINNED watch rule — the two things the bar
    /// is built from.
    ///
    /// Trap 22 governs the staging: the built-in rules ship pinned, so letting
    /// <c>ApplyDefaultRules</c> run would make the count depend on how many built-ins the
    /// current version happens to ship. Both halves are seeded explicitly instead, and the
    /// rule is seeded PINNED so the watch-chip arm is exercised rather than merely not
    /// contradicted. **The 📌 is the whole switch since Surface A / SA-R** — the
    /// <c>PinWatchChips</c> master that used to gate it beside the pin has retired.
    ///
    /// Every breakout kind is disabled: starring dps/loot while minimized is exactly
    /// what opens those windows, and the point here is the bar, not its satellites.
    /// </summary>
    [Fact]
    public void TheCollapsedBarDrawsACellPerStarredStatAndPinnedRule()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills", "dps", "loot"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            // The built-in "CC broke" rule ships PINNED, so a profile that lets
            // ApplyDefaultRules run would put a chip on the bar this test never asked
            // for — and the count would then track however many built-ins the current
            // version happens to ship. Marking the defaults as already applied is what
            // makes the prediction below a prediction rather than a guess (trap 23).
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
            settings.TrackedRules.Add(new TrackedRule
            {
                Id = "hud-bar-pinned", Name = "Harness Pinned", Kind = WatchKind.Loot,
                Pattern = "Harness Test Widget", Pinned = true, AlertBanner = false,
            });
        });
        app.Launch();

        // THE PREDICTION, written before it ran (trap 23). "dps" is no longer a
        // MiniStats key — MigratePromotedHudStats strips it on load — so the seed's three
        // stars land as TWO cells, plus one pinned rule, plus the always-on trio: 3 + 2 + 1.
        app.WaitForDump("hudCells", 6, "the trio, a cell per surviving star, and the pin");
    }

    /// <summary>Un-pinning is the other direction, and it is the one a refactor drops
    /// silently: the stars keep drawing, so the bar still looks right.
    ///
    /// **It reaches the bar through the 📌 now, not through the retired master.** Until
    /// Surface A / SA-R this seeded an ENABLED, PINNED rule and a <c>PinWatchChips</c> of
    /// false, so it proved the master's arm and never the pin's — the assertion is the same
    /// number for a different reason, which is the thing to say out loud rather than leave
    /// for the next reader to work out from a diff.</summary>
    [Fact]
    public void UnpinnedWatchRulesPutNothingOnTheBar()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;   // see the note above
            settings.TrackedRules.Clear();
            settings.TrackedRules.Add(new TrackedRule
            {
                Id = "hud-bar-unpinned", Name = "Harness Unpinned", Kind = WatchKind.Loot,
                Pattern = "Harness Test Widget", Pinned = false, AlertBanner = false,
            });
        });
        app.Launch();

        // Trio plus the one starred stat; nothing for the unpinned rule.
        app.WaitForDump("hudCells", 4, "the trio and the one starred stat, and no chip for the rule");
    }

    /// <summary>
    /// The trio's third number, and the one swap it makes (Surface A / SA-1).
    ///
    /// **A screenshot cannot settle this and no unit test can reach it.** Both states
    /// render correctly and look equally right, so a picture proves only that ONE of them
    /// drew; <c>HudGlanceTests</c> proves the rule, and this proves the rule reaches the
    /// control — "present in the build" and "in effect at runtime" being different claims
    /// (trap 42).
    ///
    /// It drives the app through its real seam: log lines appended to the file the widget
    /// is tailing, exactly as the game would write them.
    /// </summary>
    [Fact]
    public void HealingTakesTheThirdSlotAndOneSwingGivesItBack()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        });
        app.Launch();

        // The fixture session is a melee one, so the third slot starts where it should.
        app.WaitForDump("hudGlance", "xp", "the third number to start as the XP rate");

        // Healing with nothing else happening: enough to outweigh anything the fixture's
        // last half-minute could still hold, so the assertion is about the RULE and not
        // about how the fixture happens to end.
        app.AppendLogLines(
            "You healed Grimwold for 9000 hit points by Light Healing.",
            "You healed Grimwold for 9000 hit points by Light Healing.",
            "You healed Grimwold for 9000 hit points by Light Healing.");
        app.WaitForDump("hudGlance", "hps", "healing to take the third slot");

        // …and one swing takes it straight back. The thirty-second window is still almost
        // entirely healing at this point, which is the whole point of the second, shorter
        // window: "the moment combat-as-damage returns", not "once healing stops winning".
        app.AppendLogLines("You crush a training dummy for 25 points of damage.");
        app.WaitForDump("hudGlance", "xp", "one swing to bring the XP rate back");
    }

    /// <summary>
    /// The xp chip's hover carries the next-level ETA and the tracked level (OE-3).
    ///
    /// **"Present in the build" and "in effect at runtime" are different claims** (trap 42),
    /// and here they are unusually easy to confuse: both numbers have existed in
    /// `SessionStats` all along and `ProgressPresentation` has worded the forecast all
    /// along. `HudXpTooltipTests` proves the sentence; this proves it reaches the chip. A
    /// dump fact that re-asked the session instead of reading what was DRAWN would have
    /// reported this feature working on the tree that does not have it.
    ///
    /// **The prediction, written before it ran** (trap 23). The fixture is a full session
    /// with 16 `You gain experience!` lines over roughly two hours, so it is far above the
    /// 0.05%/hr floor below which `HoursToLevel` is null — `hudXpEta=1` at launch. It
    /// contains no ding at all, so the ledger has nothing and the snapshot has nothing:
    /// `hudXpLevel=0`, the tooltip's "not seen yet" line, which is a DRAWN sentence rather
    /// than a missing one. Appending one ding then has to move it to 12 through the ledger
    /// the widget writes at the same tick.
    /// </summary>
    [Fact]
    public void TheXpChipsHoverCarriesTheEtaAndTheTrackedLevel()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;   // the bar only draws while MiniRoot is visible
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        });
        app.Launch();

        // The fixture is melee, so the third slot is the xp one and there IS a chip to
        // hover — the -1 reading (swapped to HPS) would make both facts below meaningless.
        app.WaitForDump("hudGlance", "xp", "the third slot to be the XP rate");
        app.WaitForDump("hudXpEta", 1, "the fixture's xp rate to put a forecast on the hover");
        app.WaitForDump("hudXpLevel", 0, "no level known before any ding — stated, not omitted");

        app.AppendLogLines("You have gained a level! Welcome to level 12!");
        app.WaitForDump("hudXpLevel", 12, "the announced level to reach the hover");
    }

    /// <summary>
    /// THE BUFF SET GETS A CELL (OE-7), and it is the only one on this bar that
    /// <c>MiniBarPresentation</c> does not know about.
    ///
    /// "buffs" has always been a valid <c>MiniStats</c> key that gated the Buff set window
    /// and drew nothing — <c>MiniBarPresentation.Order</c> says so in its own comment — so
    /// that window's only doors were the Settings tick and an opt-in double-click on a chip
    /// that did not exist. **The seat could not make the ✕ transient without giving it one**
    /// (trap 59: a Settings row is not a door either, and a float with no way back is
    /// discussion #45 again).
    ///
    /// **A missing cell photographs as an unremarkable bar** (trap 29), which is why this is
    /// an assertion and not a screenshot. The prediction, written before it ran (trap 23):
    /// the trio is always three, "kills" is one, and "buffs" is the one under test — 5 with
    /// it and 4 without, so the pair below fails in EITHER direction. The count does not
    /// depend on any buff being up: the chip reads zero and is still a door.
    /// </summary>
    [Fact]
    public void TheBuffStarPutsACellOnTheBarAndNothingElseDoes()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills", "buffs"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        });
        app.Launch();

        app.WaitForDump("hudCells", 5, "the trio, the kills cell and the buff set's own");
    }

    /// <summary>The other half of the pair above, and the reason it is a separate launch: a
    /// single count can be reached by a bar that draws the buff cell unconditionally, which
    /// would put a stat on the HUD of every player who never asked for it.</summary>
    [Fact]
    public void WithoutTheBuffStarThereIsNoBuffCell()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        });
        app.Launch();

        app.WaitForDump("hudCells", 4, "the trio and the kills cell, and no buff cell");
    }

    /// <summary>
    /// A SAVED ORDER IS THE ORDER THE BAR DRAWS (#191, TheMegaSage; owner lock 2026-09-07).
    ///
    /// **"Present in the build" and "in effect at runtime" are different claims** (trap 42),
    /// and this feature is unusually easy to half-ship: `MiniBarPresentation.ResolveOrder` is
    /// unit-tested and would go on answering correctly while `HudBarView` walked the
    /// canonical list beside it, drawing every chip the player asked for in the order they
    /// did not. `hudCells` cannot tell those two apart — it is the same count either way —
    /// which is why `hudCellOrder` reports what was DRAWN rather than what would be resolved.
    ///
    /// **The prediction, written before it ran** (trap 23). Four stars, one of them "buffs",
    /// and a saved order that puts money first and kills third: the bar reads
    /// money, buffs, kills, loot. The un-starred keys in the seed exist to prove they are
    /// carried without drawing — a settings file is a whole order, not just the visible part.
    ///
    /// The trio is not in this token and must not be: it is fixed leftmost, and its third
    /// slot swaps identity mid-session.
    /// </summary>
    [Fact]
    public void TheBarDrawsTheOrderTheProfileSaved()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills", "loot", "money", "buffs"];
            settings.MiniBarOrder =
                ["money", "buffs", "kills", "loot", "pet", "procs", "motes", "deaths"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        });
        app.Launch();

        app.WaitForDump("hudCellOrder", "money,buffs,kills,loot",
            "the bar to draw the chips in the order the profile saved");
        // …and the count still agrees, so this is a REORDER rather than a bar that dropped
        // a chip on the way: the trio plus four.
        app.WaitForDump("hudCells", 7, "the trio and all four starred chips");
        // Nothing was dragged in this session, so nothing was written. `hudCellGrip` is the
        // instrument for the day a harness does drive a real chip drag (trap 56); zero
        // presses here is also the assertion that a launch cannot write this setting.
        app.WaitForDump("hudCellGrip", "0,0", "no press and no write on a launch alone");
    }

    /// <summary>
    /// THE FLOOR, and it is the other half of the pair above: an untouched profile draws
    /// exactly the bar every release before this one drew.
    ///
    /// **A separate launch, because a single seeded assertion is reachable by a bar that
    /// ignores the canonical list entirely** — one that simply drew `MiniStats` in the order
    /// the file happens to list them would satisfy the test above and shuffle every existing
    /// player's bar. The stars are seeded deliberately out of canonical order to catch that.
    ///
    /// Empty means canonical is the whole migration story: there is no `ApplyMigrations`
    /// entry, and a profile reset restores the shipped bar by construction.
    /// </summary>
    [Fact]
    public void AProfileThatNeverDraggedAnythingDrawsTheCanonicalOrder()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["money", "kills", "loot"];   // NOT canonical order
            settings.MiniBarOrder.Clear();
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        });
        app.Launch();

        app.WaitForDump("hudCellOrder", "kills,loot,money",
            "an empty MiniBarOrder to mean the canonical bar, whatever order the stars list in");
    }

    // ---- PET DPS ON THE ALWAYS-ON ROW (SIGNED #422) ----------------------------------

    /// <summary>A minimized widget with kills and pet starred, nothing else on the bar, and
    /// no floating window opening behind it. <paramref name="inserted"/> is the one thing
    /// each scenario below varies.</summary>
    private static AppHarness PetBar(bool inserted, bool probe = false) =>
        new(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills", "pet"];
            settings.HudGlancePet = inserted;
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        },
        probe ? new Dictionary<string, string> { ["EQBUDDY_PETDROP"] = "1" } : null);

    /// <summary>
    /// THE FLOOR: a profile that has never dragged the pet chip anywhere draws exactly the
    /// bar every release before this one drew — three always-on numbers, and pet as a CELL.
    ///
    /// **The prediction, written before it ran** (trap 23): `hudGlancePet=0`, the cell order
    /// reads "kills,pet" in canonical order, and the bar holds five children — the three
    /// always-on slots plus two cells.
    /// </summary>
    [Fact]
    public void WithoutTheSettingPetDamageIsACellExactlyAsItWas()
    {
        using var app = PetBar(inserted: false);
        app.Launch();

        app.WaitForDump("hudGlancePet", 0, "the always-on row to draw no pet slot by default");
        app.WaitForDump("hudCellOrder", "kills,pet", "pet to be an ordinary starred cell");
        app.WaitForDump("hudCells", 5, "the three always-on numbers and two cells");
    }

    /// <summary>
    /// INSERTED, THE PET NUMBER IS ON THE ALWAYS-ON ROW AND NOT IN THE CELLS — the negative
    /// this whole change turns on, since a bar that drew both would show one player two
    /// different pet-damage numbers side by side.
    ///
    /// **The negative is asserted at a moment the positive names** (trap 62): "pet" being
    /// absent from `hudCellOrder` is equally true of a bar that has not drawn the insert
    /// yet, or of one where the setting never reached the view at all. `hudGlancePet=1` is
    /// what says the row DREW the slot, in the same dump — one moment, two facts.
    ///
    /// **The prediction** (trap 23): the count is FIVE either way, which is the point —
    /// name, DPS, pet, third, kills. Six would be the chip drawn twice; four would be it
    /// lost on the way. The pair with the test above therefore fails in both directions.
    /// </summary>
    [Fact]
    public void AnInsertedPetDrawsOnTheAlwaysOnRowAndNotAsACell()
    {
        using var app = PetBar(inserted: true);
        app.Launch();

        app.WaitForDump("hudGlancePet", 1, "the always-on row to draw the pet slot");
        app.WaitForDump("hudCellOrder", "kills", "the pet cell to leave the tray while it is up there");
        app.WaitForDump("hudCells", 5,
            "name, DPS, pet and the third number, plus the one remaining cell — never six");
    }

    /// <summary>
    /// **THE DROP WRITES THE SETTING, AND THE DROP IS THE ONLY THING THAT DOES** (§3/§6) —
    /// insert and eject in one launch, because what the pair proves is that the gesture moves
    /// the chip BOTH ways rather than that a seeded profile renders.
    ///
    /// A launch alone writes nothing: `hudCellGrip` reads 0,0 until the first drop, and it is
    /// the instrument that separates "the drop never happened" from "it happened and wrote
    /// nothing" (trap 56).
    ///
    /// **What this drives and what it does not.** The probe enters `HudBarReorder.Land`, the
    /// same method a mouse-up enters, so the write path, the persist and the redraw are the
    /// real ones. It does not drive the pointer arithmetic — which x lands in the gap, and
    /// what a landing MEANS — because nothing in this suite can move a pointer onto a control
    /// inside the widget; those two sums are `MiniBarDragTests`'.
    ///
    /// **The prediction** (trap 23): after the insert, `hudGlancePet=1` with the cells down
    /// to "kills"; after the eject at the head of the cells, `hudGlancePet=0` with the cells
    /// back to "kills,pet" — pet in its REMEMBERED canonical place rather than in front of
    /// kills, because an eject that lands beside no new neighbour writes no order at all.
    /// </summary>
    [Fact]
    public void ADropCarriesThePetChipOntoTheAlwaysOnRowAndBackOffIt()
    {
        using var app = PetBar(inserted: false, probe: true);
        app.Launch();

        app.WaitForDump("hudGlancePet", 0, "the pet chip to start in the cells");
        app.WaitForDump("hudCellGrip", "0,0", "no drop written by a launch alone");

        app.DropHudChip("pet", -1);
        app.WaitForDump("hudGlancePet", 1, "the drop to put the pet slot on the always-on row");
        app.WaitForDump("hudCellOrder", "kills", "and to take its cell out of the tray");
        app.WaitForDump("hudCellGrip", "0,1", "exactly one drop written");

        app.DropHudChip("pet", 0);
        app.WaitForDump("hudGlancePet", 0, "the second drop to bring the pet chip back down");
        app.WaitForDump("hudCellOrder", "kills,pet",
            "and to land it in the slot MiniBarOrder remembered for it");
        app.WaitForDump("hudCellGrip", "0,2", "two drops written, and no press faked by either");
    }
}
