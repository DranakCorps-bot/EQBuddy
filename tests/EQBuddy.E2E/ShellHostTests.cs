using EQBuddy.UI.Shared;

namespace EQBuddy.E2E;

/// <summary>
/// The Evolved shell host, asserted against the real launched app (E-3 Phase 2 PR 1).
///
/// **This suite is the only thing besides a screenshot that can see this window**, and
/// the two see different failures. A picture proves the rail reads well; it cannot prove
/// a control EXISTS, because an absent control photographs as an unremarkable window
/// (trap 29) — which is how the title-bar Mobile button stayed invisible for six days
/// through several releases, a compile, a test run and a diff. And the WPF layer has no
/// unit tests at all, so an assertion from a launched app is the only coverage the wiring
/// between `ShellLayoutPolicy` and the window it drives will ever have.
///
/// **Nothing here asserts the SCREEN.** A hosted runner is 1024×768, so "the rail shows
/// labels" would be an assertion about the desk this was written on. The dump carries the
/// INPUT (`shellWidth`) beside the ANSWER (`shellRailLabels`), and what is asserted is
/// that one follows from the other — a relationship, true on any monitor.
/// </summary>
public class ShellHostTests
{
    private static Dictionary<string, string> OpenOn(string address, string? size = null)
    {
        var env = new Dictionary<string, string> { ["EQBUDDY_SHELL"] = address };
        if (size is not null) env["EQBUDDY_SHELL_SIZE"] = size;
        return env;
    }

    /// <summary>
    /// The host opens, the rail draws, the Search affordance is there, and the Progress
    /// room paints — the four things E-3 PR 1 claims to have built.
    ///
    /// **`shellRail=1` is the assertion with teeth.** The signed pre-design refuses a
    /// disabled row for a room that has not shipped (*"an affordance that opens nothing
    /// is a trap"*), so this number is the count of rows DRAWN and it must equal the
    /// number of rooms that exist. The day a seventh row appears without a room behind
    /// it — or a room lands without joining the rail — this is what says so.
    /// </summary>
    [Fact]
    public void TheShellOpensOnProgressWithOneRailRowAndASearchAffordance()
    {
        using var app = new AppHarness(environment: OpenOn("progress"));
        app.Launch();

        app.WaitForDump("shellPage", "progress", "the shell to land on the Progress room");
        Assert.Equal(ShellPages.Landed.Count, app.DumpValue("shellRail"));
        Assert.Equal(1, app.DumpValue("shellSearch"));
        // Search is a shortcut past the nav, not a page: it must not be OPEN on arrival.
        Assert.Equal(0, app.DumpValue("shellPalette"));
        // The room actually painted. Four tabs, from Core's ProgressSurface — the same
        // four the Progress WINDOW builds from the same definition.
        Assert.Equal(4, app.DumpValue("shellProgressTabs"));
        // "progress", not "experience": `ProgressSurface.KeyFor(Experience)` is the card
        // key the five surfaces folded into, deliberately one OF the absorbed keys rather
        // than a new one. So the Experience room's address is `progress:progress`, which
        // reads oddly and is correct — the room half is the SURFACE's vocabulary, not the
        // shell's, and re-spelling it here would be a second name for one room.
        Assert.Equal("progress", app.DumpText("shellProgressTab"));
    }

    /// <summary>
    /// **One navigation path, exercised end to end**: `page:room` lands inside the room,
    /// not merely on it. This is the grammar `EQBUDDY_EXPAND` has taken since 2026-08-26,
    /// reused so the rail, the Ctrl+K palette and a future HUD button resolve to one
    /// destination spelling — two ways to land on a room is trap 33 lifted from data into
    /// navigation.
    /// </summary>
    [Fact]
    public void AnAddressLandsInsideTheRoomAndNotJustOnIt()
    {
        using var app = new AppHarness(environment: OpenOn("progress:raids"));
        app.Launch();

        app.WaitForDump("shellProgressTab", "raids", "the address's room half to be honoured");
        app.WaitForDump("shellPage", "progress", "and its page half");
    }

    /// <summary>
    /// **Two hosts of one room must report the same numbers.** The shell builds its own
    /// instances of the Progress surfaces (a UIElement has one parent — trap 45), and
    /// every RULE behind them is shared, so a divergence between the two would be a real
    /// defect and an invisible one: both windows render, both look right, and nothing but
    /// a comparison can tell.
    ///
    /// Both are open at once here on purpose. That is the condition trap 45's exemption
    /// note calls out — *"the day one of them expands in place, it is the Progress crash
    /// again"* — and on WPF the symptom is not a crash but a surface silently vanishing
    /// from whichever host drew it first, which these row counts would catch.
    /// </summary>
    [Fact]
    public void TheShellAndTheProgressWindowAgreeAboutTheSameRoom()
    {
        using var app = new AppHarness(environment: new Dictionary<string, string>
        {
            ["EQBUDDY_SHELL"] = "progress:raids",
            ["EQBUDDY_PROGRESS"] = "raids",
        });
        app.Launch();

        app.WaitForDump("shellProgressTab", "raids", "both hosts to reach the Raids room");
        Assert.Equal(app.DumpValue("progressTabs"), app.DumpValue("shellProgressTabs"));
        Assert.Equal(app.DumpValue("progressRaidsRows"), app.DumpValue("shellProgressRaidsRows"));
        Assert.Equal(app.DumpValue("progressFaction"), app.DumpValue("shellProgressFaction"));
        Assert.Equal(app.DumpValue("progressMotesRows"), app.DumpValue("shellProgressMotesRows"));
    }

    /// <summary>
    /// Degrade axis 1, asserted as the RELATIONSHIP rather than as a picture.
    ///
    /// `ShellNavigationTests` proves the arithmetic; it cannot prove the window applied
    /// it, and the gap between those two claims is trap 42 — a fix that was genuinely in
    /// the binary, correct in the diff, green in the tests, and not in effect at runtime,
    /// for two builds. So this reads the width the window actually has and asserts the
    /// answer the policy gives for THAT width, which holds at 1024×768 or at 4K.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("580x480")]
    public void TheRailsLabelsFollowTheWidthTheWindowActuallyHas(string? size)
    {
        using var app = new AppHarness(environment: OpenOn("progress", size));
        app.Launch();
        app.WaitForDump("shellPage", "progress", "the shell to open");

        var width = app.DumpValue("shellWidth");
        Assert.True(width >= ShellLayoutPolicy.MinWidth,
            $"the floor must hold: shellWidth={width}; dump was: {app.Artifacts()}");
        Assert.Equal(
            ShellLayoutPolicy.For(width).RailLabelsVisible ? 1 : 0,
            app.DumpValue("shellRailLabels"));
    }
}
