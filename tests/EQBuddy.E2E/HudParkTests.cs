namespace EQBuddy.E2E;

/// <summary>
/// FREE PLACEMENT (OE-8) reaching a running app — the RESTORE half, which is the half that
/// can go wrong without anybody touching a mouse.
///
/// <c>HudParkTests</c> in <c>tests/EQBuddy.Tests</c> proves the arithmetic with no window;
/// this proves the profile reaches the screen. They are named the same on purpose and share
/// no assertion — "present in the build" and "in effect at runtime" are different claims, and
/// trap 42 cost two builds to learn it.
///
/// **What this suite deliberately does NOT do is assert a POSITION.** A hosted runner is
/// 1024×768, and a test that demands a window be at 640,480 is asserting the desk it was
/// written on. Every assertion here is a RELATIONSHIP between two keys read off ONE dump line
/// — therefore one moment (trap 56) — and every seeded point is chosen to hold on any desk: a
/// reachable one inside the smallest monitor anyone runs this on, and an unreachable one no
/// virtual screen contains.
///
/// **The DRAG itself is not here, and saying so is the point** (trap 34: a guard that cannot
/// fail reads as coverage). Neither this suite nor `shoot.ps1` can move a pointer, so
/// "dragging parks it" and "an undragged close persists nothing" are
/// `scripts/drag-verify.ps1`'s phases — a harness that drives the real exe with real mouse
/// input, which is the shipped precedent for exactly this question (trap 49's `4548e10`).
///
/// [Collection("e2e")] because every test here launches a real always-on-top widget and two
/// at once would fight for the desktop (trap 57 / trap 61).
/// </summary>
[Collection("e2e")]
public sealed class HudParkTests
{
    /// <summary>A point every monitor this ever runs on contains, well inside a 1024×768
    /// runner and clear of the corner the widget itself restores to.</summary>
    private const double ParkLeft = 120, ParkTop = 240;

    /// <summary>
    /// **NaN IS SLAVED, AND AN UNTOUCHED PROFILE IS THE SA-2 APP.** The default is the whole
    /// of the safety argument for reopening the trap-2 architecture, so it gets the first
    /// assertion rather than being assumed by the ones below.
    ///
    /// THE PREDICTION, written before it ran (trap 23): with two seeded spawn timers the row
    /// is up (`hudChipsRow=1`) and BOTH park keys read `slaved` — the effect and the setting
    /// agreeing, which is what "nothing has happened to this profile" looks like.
    /// </summary>
    [Fact]
    public void AnUntouchedProfileRunsTheRowSlavedUnderTheWidget()
    {
        using var app = new AppHarness(settings => settings.TrackSpawns = true);
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        app.WaitForDump("hudChipsRow", 1, "the chip row to be on screen while a timer runs");
        app.WaitForDump("hudRowPark", "slaved", "an untouched profile to follow the widget");
        app.WaitForDump("hudRowParkSaved", "slaved",
            "the profile to hold no park at all, which is what makes the default free");
    }

    /// <summary>
    /// A park the desk CAN show is honoured: the row opens where the player left it.
    ///
    /// THE PREDICTION: `hudRowPark` is NOT `slaved`, and `hudRowParkSaved` is exactly the
    /// seeded pair. The effect key is asserted as "not slaved" rather than as a coordinate on
    /// purpose — the window clamps against its own monitor's work area, so demanding 120,240
    /// would be demanding a work area, which is the desk-it-was-written-on assertion this
    /// file's header refuses.
    /// </summary>
    [Fact]
    public void AReachableParkOpensWhereThePlayerLeftIt()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = true;
            settings.HudRowParkLeft = ParkLeft;
            settings.HudRowParkTop = ParkTop;
        });
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        app.WaitForDump("hudChipsRow", 1, "the chip row to be on screen");
        app.WaitForDump("hudRowParkSaved", $"{ParkLeft},{ParkTop}",
            "the seeded park to still be in the profile");
        Assert.NotEqual("slaved", app.DumpText("hudRowPark"));
    }

    /// <summary>
    /// **THE MISSING MONITOR (#117), AND THE ASSERTION IS THAT NOTHING WAS WRITTEN.**
    ///
    /// A park at 30000,30000 is on no desk anyone owns, so `ScreenGuard.OnScreen` rejects it,
    /// the row runs SLAVED for the session, and the pair survives untouched — the monitors
    /// come back and the park comes back with them. Persisting the fallback instead is the
    /// bug `WindowPlacement.PositionToPersist` exists for, and it is invisible until the day
    /// the display is plugged back in.
    ///
    /// **Trap 62: the "did not write" half is paired with a positive on the far side of the
    /// decision, and both come off ONE dump line.** `hudRowPark=slaved` is only reachable
    /// after the app has read the pair and judged it — a bare zero straight after a launch
    /// would be satisfied by the state that was already there. Waiting for
    /// `hudRowParkSaved` to be the seeded point proves the app got that far; reading
    /// `hudRowPark` off the same dump proves what it decided, at the same moment.
    /// </summary>
    [Fact]
    public void AParkOnAMonitorThatIsNotThereRunsSlavedAndDoesNotRewriteTheSetting()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = true;
            settings.HudRowParkLeft = 30000;
            settings.HudRowParkTop = 30000;
        });
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        app.WaitForDump("hudChipsRow", 1, "the chip row to be on screen");
        app.WaitForDump("hudRowParkSaved", "30000,30000",
            "the unreachable park to still be in the profile — the app has read and judged it");
        Assert.Equal("slaved", app.DumpText("hudRowPark"));
    }

    /// <summary>
    /// The under-bar panel carries its own pair, and it is INDEPENDENT of the row's — the
    /// per-WINDOW granularity the plan settled on. A profile with the row parked and the
    /// panel not is a state a player reaches by dragging one of them, so it has to be a state
    /// the app can hold.
    ///
    /// THE PREDICTION: `hudRowParkSaved` is the seeded pair, `hudPanelParkSaved` is `slaved`,
    /// and the panel that `EQBUDDY_HUDEXPAND` peeks is on screen regardless.
    /// </summary>
    [Fact]
    public void TheRowAndThePanelParkIndependentlyBecauseTheyAreTwoWindows()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts = ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
            settings.HudRowParkLeft = ParkLeft;
            settings.HudRowParkTop = ParkTop;
        }, new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "dps:peek" });
        app.Launch();

        app.WaitForDump("hudExpandPanel", 1, "the under-bar panel to be on screen");
        app.WaitForDump("hudRowParkSaved", $"{ParkLeft},{ParkTop}",
            "the row's park to be the one that was seeded");
        app.WaitForDump("hudPanelParkSaved", "slaved",
            "the panel to be untouched by the row's park — one pair per window");
        Assert.Equal("slaved", app.DumpText("hudPanelPark"));
    }

    /// <summary>
    /// The panel's own park restores, and it does so through the same rules — this is the
    /// second window proving the machinery is shared rather than copied (the drift that put a
    /// hand-copied older anchor on the Avalonia chip stacks and carried #122 and #152 to two
    /// more platforms).
    /// </summary>
    [Fact]
    public void AParkedPanelOpensWhereThePlayerLeftItAndAnUnreachableOneDoesNot()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts = ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
            settings.HudPanelParkLeft = ParkLeft;
            settings.HudPanelParkTop = ParkTop;
        }, new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "dps:peek" });
        app.Launch();

        app.WaitForDump("hudExpandPanel", 1, "the under-bar panel to be on screen");
        app.WaitForDump("hudPanelParkSaved", $"{ParkLeft},{ParkTop}",
            "the seeded panel park to still be in the profile");
        Assert.NotEqual("slaved", app.DumpText("hudPanelPark"));
    }

    /// <summary>
    /// **THE TAKEN WIDTH (OE-1b lock 3).** A width in the profile is what the panel draws; a
    /// profile without one gets OE-7's single shipped 300, which every other test in this
    /// file is running with. 420 is chosen precisely because it is not that number — a panel
    /// that never consulted the setting reports 300 here and fails, so the assertion cannot
    /// be satisfied by the state that was already there.
    ///
    /// It is also inside a 1024×768 runner's work area, so the monitor clamp cannot be what
    /// makes it pass or fail (this file's header: no assertion about the desk).
    /// </summary>
    [Fact]
    public void APanelWidthThePlayerTookIsWhatThePanelDraws()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills"];
            settings.DisabledBreakouts = ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
            settings.HudPanelWidth = 420;
        }, new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "dps:peek" });
        app.Launch();

        app.WaitForDump("hudExpandPanel", 1, "the under-bar panel to be on screen");
        app.WaitForDump("hudPanelWidth", 420, "the taken width to be what the panel draws");
    }
}
