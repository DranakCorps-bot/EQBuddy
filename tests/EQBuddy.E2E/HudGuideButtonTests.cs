using EQBuddy.UI.Shared;

namespace EQBuddy.E2E;

/// <summary>
/// DRA-700 (Founder, 2026-10-01): the MINIMIZED bar carries a one-click Guide button right
/// after the character name, before the metric slots.
///
/// The WPF layer has no unit tests (docs/TestPlan.md §5), so these rows are the only thing
/// besides a screenshot that can see the button — and a screenshot cannot prove a control
/// EXISTS or that its click reaches anything (trap 29). Nothing here asserts the SCREEN:
/// every fact comes off the <c>EQBUDDY_EXPAND</c> dump, and the widths are read from one
/// moment (trap 56).
///
/// **PREDICTION, written before the first run** (trap 23). On a minimized bar seeded
/// <c>dps,xp</c>: <c>hudGuide</c> is 1 (child 0 is the name slot, the button is next);
/// <c>hudGuideName</c> is <c>Guide</c>; <c>hudGuideFocusable</c> is 1; the name slot measures
/// <c>NameReservedWidth</c> + its divider (92 + 12 + 1 = 105) with a 20-letter name exactly
/// as with "Testchar" — the button takes none of it — and the button has a width of its own.
/// Then the shell, closed with its ✕, comes back on the Guide room when the button is
/// pressed: the OE-2 recovery the context row has always carried, reached from the bar.
/// </summary>
[Collection("e2e")]
public sealed class HudGuideButtonTests
{
    /// <summary>A name longer than the slot can show — 20 letters against a slot sized for
    /// 16. EverQuest caps names below this; the point is the slot, not the game.</summary>
    private const string LongName = "Xanthelarionwyndsong";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheBarsGuideButtonSitsAfterTheNameAndOpensTheGuide(bool longName)
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["dps", "xp"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs", "Quests"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        },
        new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "1",
            ["EQBUDDY_DOORPROBE"] = "1",
        });
        if (longName)
        {
            // The log names the character, so the long name is a log of its own — and the
            // fixture's is removed rather than out-dated, so the tail cannot pick it either.
            FixtureLog.WriteShifted(
                Path.Combine(AppHarness.RepoRoot, "tests", "fixtures", "eqlog_Testchar_fixture.txt"),
                app.LogsDir, LongName, AppHarness.Server);
            File.Delete(app.LogPath);
        }
        app.Launch();

        app.WaitForDump("hudGlance", "dps,xp", "the bar to draw its metric row");
        app.WaitForDump("hudGuide", 1, "the Guide button to sit straight after the name slot");
        Assert.Equal(WidgetMenuPolicy.GuideButtonLabel, app.DumpText("hudGuideName"));
        Assert.Equal(1, app.DumpValue("hudGuideFocusable"));

        var widths = app.WaitForDumpValues("the name slot's and the button's widths",
            "hudNameWidth", "hudGuideWidth");
        // The name slot is the reserved width plus its hairline divider, with any name — so
        // the button cannot have taken any of it, and a long name trims inside it.
        Assert.Equal((int)Math.Round(HudGlance.NameReservedWidth + DesignTokens.SpaceL + 1),
            widths[0]);
        Assert.True(widths[1] > 0, $"the Guide button drew no width ({widths[1]})");

        // THE DOOR, from the state it exists to recover from: the shell taken by its ✕.
        app.WaitForDump("shellPage", "home", "the shell to open on its default room");
        app.CloseShellWindow(ShellPages.Label(ShellPage.Home));
        app.WaitForDump("shellPage", "",
            "the ✕ to take the shell away — the state the Guide door recovers from");

        app.ClickGuideButton();
        app.WaitForDump("shellPage", WidgetMenuPolicy.GuideAddress,
            "the bar's Guide button to bring the shell back on the Guide room");
        // The bar is unchanged by the click: same place, still a door.
        Assert.Equal(1, app.DumpValue("hudGuide"));
    }
}
