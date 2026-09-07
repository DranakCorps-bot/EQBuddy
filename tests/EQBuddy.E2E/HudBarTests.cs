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
}
