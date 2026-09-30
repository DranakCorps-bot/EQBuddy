using EQBuddy.Core;

namespace EQBuddy.E2E;

/// <summary>
/// #942 (Jeff-Crawford, DRA-639): the minimised bar can grow LEFT.
///
/// "I place it under my map on the right side of the screen. So, it currently grows off
/// screen." The arithmetic is unit-tested in <c>WidgetMetricsTests</c>; this is the half a
/// unit test cannot reach, that the switch actually reaches the WINDOW — "present in the
/// build" and "in effect at runtime" are different claims (trap 42). Nothing here asserts
/// the screen: both edges come off ONE dump (trap 56), and the only thing compared is which
/// of them moved.
///
/// **THE PREDICTION, written before it ran** (trap 23). The bar starts <c>dps,xp</c>;
/// ticking HPS through the ★'s own writer adds a slot and the bar widens. With the switch
/// ON, <c>widgetRight</c> is unchanged (±1 for rounding) and <c>widgetLeft</c> falls by the
/// growth. OFF — the negative, and the shape the reporter filmed — <c>widgetLeft</c> is
/// unchanged and <c>widgetRight</c> rises. Each arm WAITS for its own moving edge first,
/// which is the positive event that says the layout has landed (trap 62); only then is the
/// still edge read.
/// </summary>
[Collection("e2e")]
public sealed class HudGrowsLeftTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheMinimisedBarWidensFromTheEdgeTheSwitchNames(bool growsLeft)
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniBarGrowsLeft = growsLeft;
            settings.WindowLeft = 700;
            settings.WindowTop = 120;
            settings.MiniStats = ["kills", "dps", "xp"];   // no "hps" — the tick adds it
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        },
        new Dictionary<string, string> { ["EQBUDDY_STARPROBE"] = "1" });
        app.Launch();

        app.WaitForDump("hudGlance", "dps,xp", "the row to start as DPS and the XP rate");
        app.WaitForDump("miniGrowsLeft", growsLeft ? 1 : 0, "the switch to reach the window");
        app.WaitUntilStill("widgetRight", TimeSpan.FromSeconds(3),
            "the bar to finish its launch-time layout before the baseline is read");
        var before = app.WaitForDumpValues("both edges before the tick", "widgetLeft", "widgetRight");
        var (left0, right0) = (before[0], before[1]);

        app.SetMiniStat("hps", true);
        app.WaitForDump("hudGlance", "dps,hps,xp", "the HPS star to add its slot");

        int[] after = [];
        Wait.Until(() =>
        {
            after = app.DumpValues("widgetLeft", "widgetRight");
            if (after[0] < 0 || after[1] < 0) return false;
            return growsLeft ? after[0] < left0 - 1 : after[1] > right0 + 1;
        }, TimeSpan.FromSeconds(30),
            growsLeft
                ? $"the bar to widen LEFTWARD (widgetLeft below {left0})"
                : $"the bar to widen RIGHTWARD (widgetRight above {right0})");

        if (growsLeft)
            Assert.InRange(after[1], right0 - 1, right0 + 1);
        else
            Assert.InRange(after[0], left0 - 1, left0 + 1);
    }

    /// <summary>
    /// **The relaunch returns to last session's RIGHT edge** — the walk-left a right-anchored
    /// bar would otherwise do. The profile is staged as a close would have left it: Left 700
    /// and a 600-wide bar, so the right edge was 1300. The bar opens far narrower than 600,
    /// and the seed (<c>WidgetMetrics.MiniBarAnchorSeed</c>) must put that narrower bar's
    /// right edge back on 1300. Unseeded, <c>widgetLeft</c> would stay 700 and the right
    /// edge would sit wherever the narrow bar ends — the prediction the mutant is checked
    /// against. 600 is chosen WIDER than this bar can draw on an empty log, so the
    /// assertion cannot pass by the bar happening to measure 600.
    /// </summary>
    [Fact]
    public void ARelaunchReturnsToLastSessionsRightEdge()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniBarGrowsLeft = true;
            settings.WindowLeft = 700;
            settings.WindowTop = 120;
            settings.MiniBarWidth = 600;
            settings.MiniStats = ["dps", "xp"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        });
        app.Launch();

        app.WaitForDump("hudGlance", "dps,xp", "the row to draw");
        int[] edges = [];
        Wait.Until(() =>
        {
            edges = app.DumpValues("widgetLeft", "widgetRight");
            return edges[1] is >= 1299 and <= 1301;
        }, TimeSpan.FromSeconds(30),
            "the right edge to return to 1300 (debug.txt widgetRight)");
        Assert.True(edges[0] > 700,
            $"the bar measured 600 or more, so this run proves nothing (left={edges[0]})");
    }
}
