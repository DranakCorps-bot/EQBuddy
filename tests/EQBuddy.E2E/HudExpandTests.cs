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

    /// <summary>
    /// EVERY TARGET THAT IS NOT THE SHIPPED TRIO reaches the screen, on the same model —
    /// OE-7's four, and OE-9's five.
    ///
    /// **The panel BODY is what is asserted, not just that a panel appeared.** Each of these
    /// draws through a different builder — <c>LivePresentation.Meter</c> for Pet,
    /// <c>HudExpandPeek</c> for the rest — and a target wired to the wrong body is exactly the
    /// failure that renders perfectly and photographs as a correct screenshot of the wrong
    /// feature (trap 24's shape one layer in). `hudExpandRows` at 1 or more tells a drawn row
    /// from the empty state, which is the half a picture cannot settle.
    ///
    /// The prediction, written before it ran (trap 23) — **and the Pet row is here because it
    /// was WRONG the first time, which is the point of writing one down.** Pet has rows: the
    /// fixture has a CHARMED pet (the `hud-expand-dps` capture carries a "Pet (Giant spider)"
    /// row, which is what settled it). A `grep -i pet` over the fixture finds nothing but "You
    /// cannot have more than one pet at a time" and reads as "no pet", because a pet is named
    /// rather than called one — and that is what the first version of this test asserted.
    /// Watch and Buffs genuinely have none: nothing is pinned in this profile and no buff is
    /// up. **Loot is TARGET-scoped since #392** and the fixture's trailing lines are past
    /// `TargetLinger`, so it ends with no target at all — 0 rows, never session drops. That
    /// was not predicted, it was MEASURED: `hud-expand-loot` came back byte-identical to
    /// `hud-expand-loot-notarget` until a /consider was staged into it.
    ///
    /// The mismatch was settled by the `hudExpandBody` fact rather than by re-reasoning
    /// (trap 33's closing line: ship the instrument before the third theory). "Pet drew five
    /// rows" is equally consistent with a correct Pet panel and with the Damage meter under a
    /// Pet header — five is also `MaxRows` against this fixture's eleven damage sources — and
    /// nothing else in the dump could tell those apart.
    ///
    /// **OE-9's four, predicted from the fixture BEFORE the run rather than from what it
    /// happened to report** — each one grepped, because "a melee log surely has procs" is the
    /// kind of confident guess that made the Pet row wrong. (`deaths` was a fifth row here
    /// until Helm's 2026-09-07 sign of #400 kept the Deaths gate and #389's "Deaths OUT"; the
    /// target is gone, so the row went with it rather than being left asserting an empty panel
    /// that no longer exists.)
    /// <list type="bullet">
    /// <item>`kills` — ROWS. It is what this session IS.</item>
    /// <item>`money` — ROWS (four facts). Thirty "You receive N silver … from the corpse"
    /// lines, so the coin total cannot be zero.</item>
    /// <item>`motes` — ROWS. Exactly ONE mote line ("a Mote of Infinitesimal Potential"), so
    /// one tier row; asserted at 1 or more rather than exactly 1, because the row count is
    /// about the WIRING and the exact tally belongs to the unit tests.</item>
    /// <item>`procs` — EMPTY, and this is the one worth stating. `ItemProcRx` matches
    /// "Your &lt;item&gt; feels alive with power." and the fixture contains that string zero
    /// times, so the honest prediction is the empty state. Asserting a row here would have
    /// been a red test blaming a correct feature.</item>
    /// </list>
    ///
    /// **`loot` stays in this theory at ZERO (#392's row), and that is a claim about the
    /// FIXTURE rather than about the feature** — it is the one row here whose expected number
    /// would change if the log gained a trailing /consider. What it CANNOT say is WHICH empty
    /// state drew, and "select a target" versus "this creature has no known drops" is the
    /// whole of what the re-scope is about;
    /// <see cref="TheLootPeekShowsTargetDropsAndSaysSoWhenThereIsNoTarget"/> stages the state
    /// and asserts `hudExpandEmpty` for exactly that reason (trap 20 — the thing being
    /// asserted is what is not there).
    /// </summary>
    [Theory]
    [InlineData("loot", 0)]
    [InlineData("pet", 1)]
    [InlineData("watch", 0)]
    [InlineData("buffs", 0)]
    [InlineData("kills", 1)]
    [InlineData("money", 1)]
    [InlineData("motes", 1)]
    [InlineData("procs", 0)]
    public void EveryNonTrioTargetDrawsItsOwnBody(string key, int minimumRows)
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
        }, new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = key });
        app.Launch();

        app.WaitForDump("hudExpand", key, $"the {key} chip's panel to be the one showing");
        app.WaitForDump("hudExpandMode", "pinned", "a click to pin rather than peek");
        app.WaitForDump("hudExpandPanel", 1, "the companion window to be on screen");
        // The BODY, which is the assertion the row count cannot make: "pet" and "dps" both
        // come back with five rows against this fixture, and a panel headed "Pet damage"
        // over the Damage meter is indistinguishable from a correct one everywhere else.
        app.WaitForDump("hudExpandBody", key, $"the {key} panel's ROWS to be {key}'s");
        if (minimumRows > 0)
            app.WaitForDumpAtLeast("hudExpandRows", minimumRows,
                $"the {key} panel to draw the fixture's rows");
        else
            app.WaitForDump("hudExpandRows", 0,
                $"the {key} panel to draw its empty state — the fixture has no {key}");
    }

    /// <summary>
    /// OE-9 lock 2: THE LOOT PEEK IS TARGET-SCOPED, and with no target it says so instead of
    /// falling back to the session.
    ///
    /// **The no-target state is STAGED rather than hoped for.** `ShowTargetDrops = false` is
    /// what makes `TargetDropsContent` return no target on every tick — the same gate the Loot
    /// float's Target view passes through — so this does not depend on where in the fixture's
    /// pull the replay happens to settle. Left on, the fixture ends inside a melee session and
    /// the panel would show whatever was last swung at, which is a real state and a different
    /// test.
    ///
    /// **`hudExpandEmpty` is what makes it an assertion at all** (trap 20): the session slice
    /// this replaced would have drawn 39 rows here, but so would a target with drops, and
    /// `hudExpandRows=0` alone cannot tell "select a target" from "this creature has no known
    /// drops". `hudExpandBody=loot` is the positive event on the far side of the decision — it
    /// is written only where the loot body is BUILT — so nothing here is a bare zero asked one
    /// moment too early (trap 62).
    /// </summary>
    [Fact]
    public void TheLootPeekShowsTargetDropsAndSaysSoWhenThereIsNoTarget()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
            settings.ShowTargetDrops = false;
        }, new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "loot" });
        app.Launch();

        app.WaitForDump("hudExpand", "loot", "the loot chip's panel to be the one showing");
        app.WaitForDump("hudExpandBody", "loot", "the loot builder to be what drew the rows");
        app.WaitForDump("hudExpandEmpty", "notarget",
            "the no-target line rather than a session fallback");
        app.WaitForDump("hudExpandRows", 0, "and no rows under it");
    }
}
