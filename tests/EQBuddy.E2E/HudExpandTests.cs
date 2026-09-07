namespace EQBuddy.E2E;

/// <summary>
/// THE MINI-BAR EXPANSION reaching the screen (OE-1).
///
/// <c>HudExpandTests</c> in <c>tests/EQBuddy.Tests</c> proves the owner's locks as RULES,
/// with no window. This proves the rules reach a running app — "present in the build" and
/// "in effect at runtime" being different claims, which trap 42 cost two builds to learn.
/// The two halves are named the same on purpose; the assertions have nothing in common.
///
/// **The gestures arrive through <c>EQBUDDY_HUDEXPAND</c> and not through a mouse.** Neither
/// this suite nor `shoot.ps1` can move a pointer, and every state this feature has is reached
/// by one — so without the hook the peek, the pin and the panel would be trap 22 exactly: a
/// surface with no way to reach its state, reading as reviewed.
///
/// [Collection("e2e")] because every test here launches a real always-on-top widget and two
/// of them at once would fight for the desktop (trap 57 / trap 61).
/// </summary>
[Collection("e2e")]
public sealed class HudExpandTests
{
    /// <summary>
    /// Lock 3: the pointer on the DPS chip peeks a panel under the bar, and the panel draws
    /// the meter rather than an apology.
    ///
    /// The fixture session is a melee one, so the Damage meter HAS rows — which is the
    /// prediction written before it ran (trap 23). `hudExpandRows` is what tells a drawn row
    /// from the empty state: without it "the panel is up" would pass just as well over a
    /// panel that could not find a number, which is trap 20's shape.
    /// </summary>
    [Fact]
    public void HoveringTheDpsChipPeeksAPanelUnderTheBar()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        }, new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "dps:peek" });
        app.Launch();

        app.WaitForDump("hudExpand", "dps", "the DPS chip's panel to be the one showing");
        // PEEK, not pinned — and this is the assertion a screenshot could never make. Both
        // states draw the identical panel; only the app can say which rule put it there.
        app.WaitForDump("hudExpandMode", "peek", "a hover to peek rather than pin");
        app.WaitForDump("hudExpandPanel", 1, "the companion window to be on screen");
        app.WaitForDumpAtLeast("hudExpandRows", 1,
            "the panel to draw the fixture's damage rows, not the empty state");
    }

    /// <summary>
    /// Lock 4 on the third slot, whose tracker is Progress while the XP rate owns it — and
    /// lock 8's third shipped tracker.
    ///
    /// Progress is the one whose ⧉ goes to a WINDOW rather than a float (the 2026-08-25 fold),
    /// so it is also the one where a wrong wiring would look completely normal on screen.
    /// </summary>
    [Fact]
    public void ClickingTheXpChipPinsTheProgressPanel()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        }, new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "progress" });
        app.Launch();

        app.WaitForDump("hudExpand", "progress", "the XP chip's panel to be the one showing");
        app.WaitForDump("hudExpandMode", "pinned", "a click to pin rather than peek");
        app.WaitForDump("hudExpandPanel", 1, "the companion window to be on screen");
        app.WaitForDumpAtLeast("hudExpandRows", 1,
            "the Progress glance to draw its summary lines");
    }

    /// <summary>
    /// Nothing is expanded until something expands it — the state every player who has
    /// configured nothing sees, and the one a mistake here would leave permanently on screen
    /// over their game.
    ///
    /// **The moment this is true AT is named rather than hoped for** (trap 62). A
    /// `WaitForDump(key, 0)` straight after a launch is satisfied by the zero that was
    /// already there, so it would pass against an app that had not decided anything — and
    /// the same assertion passed with the whole gate deleted the last time that was tried.
    /// Here the positive event is `hudGlance`: it can only be written once
    /// <c>HudBarView.Render</c> has run, and Render is what builds the two expansion chips.
    /// A bar that has drawn its chips and reports no panel is a real answer.
    /// </summary>
    [Fact]
    public void NothingIsExpandedUntilSomethingExpandsIt()
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

        // The bar has rendered — the chips exist and had their chance to expand.
        app.WaitForDump("hudGlance", "xp", "the collapsed bar to draw its trio");
        app.WaitForDump("hudExpand", "none", "no tracker to be expanded on a default launch");
        app.WaitForDump("hudExpandMode", "collapsed", "the model to be collapsed");
        app.WaitForDump("hudExpandPanel", 0, "no companion panel on screen");
        app.WaitForDump("hudExpandRows", 0, "and nothing drawn in one");
    }
}
