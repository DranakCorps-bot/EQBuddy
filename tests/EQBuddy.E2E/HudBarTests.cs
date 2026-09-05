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
    /// Trap 22 governs the staging: the built-in rules ship pinned, so leaving
    /// <c>PinWatchChips</c> at its default would make the count depend on how many
    /// built-ins the current version happens to ship. Both halves are seeded explicitly
    /// instead, and the pinned rule is seeded ON so the watch-chip arm is exercised
    /// rather than merely not contradicted.
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
            settings.PinWatchChips = true;
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

        // Three stars plus one pinned rule.
        app.WaitForDump("hudCells", 4, "the collapsed bar to draw a cell per star and pin");
    }

    /// <summary>Un-pinning is the other direction, and it is the one a refactor drops
    /// silently: the stars keep drawing, so the bar still looks right.</summary>
    [Fact]
    public void UnpinnedWatchRulesPutNothingOnTheBar()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.PinWatchChips = false;
            settings.DefaultRulesVersion = int.MaxValue;   // see the note above
            settings.TrackedRules.Clear();
            settings.TrackedRules.Add(new TrackedRule
            {
                Id = "hud-bar-unpinned", Name = "Harness Unpinned", Kind = WatchKind.Loot,
                Pattern = "Harness Test Widget", Pinned = true, AlertBanner = false,
            });
        });
        app.Launch();

        app.WaitForDump("hudCells", 1, "the one starred stat, and no chip for the rule");
    }
}
