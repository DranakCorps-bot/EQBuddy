using EQBuddy.Core;

namespace EQBuddy.E2E;

/// <summary>
/// THE CHIP ROWS (Surface A / SA-2's one row, split in two by DRA-352 D1) — the FIGHT row
/// (mez &amp; slow, watch alerts, buffs; `hudChips*`/`hudRow*` keys) and the SPAWN row
/// (respawn countdowns; `spawnChips*`/`spawnRow*` keys).
///
/// **`HudChipRowTests`/`HudChipRowSplitTests` in EQBuddy.Tests prove the merge and the
/// split; this proves they reach the screen.** "Present in the build" and "in effect at
/// runtime" are different claims and trap 42 cost two builds to learn it — and the specific
/// thing a split could break silently is a family landing in the WRONG window while both
/// rows look perfectly correct. That is what the per-family counts, read off the window that
/// draws each family, and `hudChipsSpawnOnFight` are for.
///
/// A screenshot cannot settle any of it either: an absent chicklet photographs as a shorter
/// row (trap 29), and the Camps hide-rule needs two windows open at once to be visible at
/// all.
///
/// [Collection("e2e")] because every test here launches a real always-on-top widget and two
/// at once would fight for the desktop (trap 57).
/// </summary>
[Collection("e2e")]
public sealed class HudChipRowTests
{
    /// <summary>The two lines the game writes when a mez lands — a cast line and a landing
    /// line is the pair MezTracker correlates; the landing alone would give an untimed chip.
    /// Appended AFTER launch, through the real tail.</summary>
    private static void LandAMez(AppHarness app) =>
        app.AppendLogLines(
            "You begin casting Mesmerization.",
            "a skeleton has been mesmerized.");

    /// <summary>A Text watch rule the tests below fire with <see cref="FireTheWatchRule"/> —
    /// a second FIGHT-row family, so order can be asserted within one row.</summary>
    private static void AddWatchRule(AppSettings settings) =>
        settings.TrackedRules.Add(new TrackedRule
        {
            Id = "e2e-watch-fire",
            Name = "Assist call",
            Pattern = "assist on",
            Kind = WatchKind.Text,
            Enabled = true,
            AlertBanner = true,
        });

    private static void FireTheWatchRule(AppHarness app) =>
        app.AppendLogLines("Sanctari tells the group, 'assist on a froglok tad shaman'");

    /// <summary>
    /// The spawn family on ITS row, with one chicklet counting and one gone DUE.
    ///
    /// THE PREDICTION, written before it ran (trap 23): two seeded timers, one 60 s into a
    /// 30-minute cycle and one 30 s past a 10 s cycle, produce `spawnChipsRow=1`
    /// `hudChipsSpawn=2` `hudChipsDue=1`, and the FIGHT row stays down (`hudChipsRow=0`,
    /// `hudChipsSpawnOnFight=0`) — a respawn timer has no business holding the fight row up.
    /// </summary>
    [Fact]
    public void TheSpawnFamilyPutsItsCountdownsAndItsDueChipOnTheSpawnRow()
    {
        using var app = new AppHarness(settings => settings.TrackSpawns = true);
        app.SeedSpawnTimers(
            ("Runnyeye Citadel", "Kizdean Gix", 60, 1800),
            ("Befallen", "Bones Brackins", 30, 10));
        app.Launch();

        app.WaitForDump("spawnChipsRow", 1, "the spawn row to be on screen while timers run");
        app.WaitForDump("hudChipsSpawn", 2, "both seeded countdowns to be on the spawn row");
        app.WaitForDump("hudChipsDue", 1, "the overdue camp to be the one chip showing DUE");
        var (fightUp, leaked, mez) = Three(app.DumpValues("hudChipsRow", "hudChipsSpawnOnFight", "hudChipsMez"));
        Assert.Equal(0, fightUp);
        Assert.Equal(0, leaked);
        Assert.Equal(0, mez);
    }

    /// <summary>
    /// **THE SPLIT ITSELF (DRA-352 D1).** A spawn timer and a mez at once put ONE chip on
    /// EACH row, both rows are up, and no spawn chip is on the fight row.
    ///
    /// THE PREDICTION: `hudChipsRow=1 spawnChipsRow=1 hudChipOrder=Mez spawnChipOrder=Spawn
    /// hudChipsSpawnOnFight=0`, all off ONE dump line (trap 56). On the pre-D1 build this is
    /// `spawnChipsRow` absent and `hudChipOrder=Mez,Spawn`, which is the run that earns it.
    ///
    /// **And they STACK rather than overlap**: seated minimized with room below, the slaved
    /// spawn row starts at or under the fight row's bottom edge (`spawnRowUnderFight=1`,
    /// `chipRowsOverlap=0`) — a RELATIONSHIP between the two windows, never a coordinate.
    /// </summary>
    [Fact]
    public void SpawnAndMezLandOnTwoRowsThatStackWithoutOverlapping()
    {
        using var app = new AppHarness(Seat);
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();
        app.WaitForDump("spawnChipsRow", 1, "the spawn row to come up for the seeded timer");

        LandAMez(app);
        app.WaitForDump("hudChipsMez", 1, "the mez chip to arrive on the fight row");
        app.WaitForDump("spawnRowUnderFight", 1,
            "the slaved spawn row to follow beyond the fight row, not on top of it");

        var texts = app.DumpTexts("hudChipOrder", "spawnChipOrder");
        Assert.Equal("Mez", texts[0]);
        Assert.Equal("Spawn", texts[1]);
        var v = app.DumpValues("hudChipsRow", "spawnChipsRow", "hudChipsSpawnOnFight", "chipRowsOverlap");
        Assert.Equal([1, 1, 0, 0], v);
    }

    /// <summary>
    /// **The visibility half of the SA-2 hosting amendment, which Helm signed on 2026-09-05:
    /// a row is up whenever its chips exist, in BOTH HUD states** — carried over to the spawn
    /// row by D1.
    /// </summary>
    [Fact]
    public void TheSpawnRowIsOnScreenWhileTheWidgetIsMinimizedToo()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.TrackSpawns = true;
        });
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        app.WaitForDump("spawnChipsRow", 1, "the spawn row to be up with the widget minimized");
        app.WaitForDump("hudChipsSpawn", 1, "the seeded countdown to be on it");
    }

    /// <summary>
    /// The Bevel-signed Camps hide-rule, across the split: while World is on Camps the same
    /// timers are already on screen there, so the SPAWN row goes away — and the fight row,
    /// carrying the mez, stays up. The failure worth catching is the rule taking the wrong
    /// row with it.
    ///
    /// THE PREDICTION: `hudChipsRow=1` (the mez chip holds it up), `hudChipsMez=1`,
    /// `hudChipsSpawn=0` and `spawnChipsRow=0` (the hide-rule).
    /// </summary>
    [Fact]
    public void TheCampsTabTakesTheSpawnRowAwayAndLeavesTheFightRowUp()
    {
        using var app = new AppHarness(
            settings => settings.TrackSpawns = true,
            new Dictionary<string, string> { ["EQBUDDY_SPAWNS"] = "Runnyeye Citadel" });
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();
        app.WaitForDump("worldWindowOpen", 1, "the World window to open on its Camps tab");

        LandAMez(app);

        app.WaitForDump("hudChipsMez", 1, "the mez chip to arrive on the fight row");
        app.WaitForDump("hudChipsSpawn", 0,
            "the spawn family to be off screen while World is showing Camps");
        app.WaitForDump("spawnChipsRow", 0, "the spawn row itself to go away");
        app.WaitForDump("hudChipsRow", 1,
            "the fight row to stay up — the hide-rule is the spawn family's, not the HUD's");
    }

    // ---- SA-4: PLACE, MUTE, and the Edit mode that sets them ----
    //
    // `HudChipRowTests` in EQBuddy.Tests proves the two settings resolve; these prove they
    // reach the screen. Since D1, order is asserted WITHIN the fight row (mez + watch alert):
    // a stored order that interleaves Spawn cannot reorder two windows.

    /// <summary>
    /// PLACE: the stored order is the order the fight row is drawn in.
    ///
    /// THE PREDICTION, written before it ran (trap 23): with the profile naming WatchFire
    /// before Mez, `hudChipOrder=WatchFire,Mez`, beside `hudChipsMez=1` and
    /// `hudChipsWatch=1` so the token is a statement about a row with both on it.
    /// </summary>
    [Fact]
    public void TheStoredFamilyOrderIsTheOrderTheFightRowIsDrawnIn()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = false;
            settings.HudChipOrder = ["WatchFire", "Spawn", "Mez", "Buff"];
            AddWatchRule(settings);
        });
        app.Launch();

        LandAMez(app);
        app.WaitForDump("hudChipsMez", 1, "the mez chip to arrive on the fight row");
        FireTheWatchRule(app);
        app.WaitForDump("hudChipsWatch", 1, "the fired rule's chip to join it");
        app.WaitForDump("hudChipOrder", "WatchFire,Mez",
            "the watch family to be drawn FIRST, which is the order this profile asks for");
    }

    /// <summary>
    /// The same two families with the DEFAULT order — the negative the pair needs: an
    /// implementation that ignored the setting and always drew watch first would pass the
    /// test above and fail this one.
    ///
    /// THE PREDICTION: `hudChipOrder=Mez,WatchFire` — the signed default, combat-urgent first.
    /// </summary>
    [Fact]
    public void TheDefaultProfileDrawsTheMezFamilyFirst()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = false;
            AddWatchRule(settings);
        });
        app.Launch();

        LandAMez(app);
        app.WaitForDump("hudChipsMez", 1, "the mez chip to arrive on the fight row");
        FireTheWatchRule(app);
        app.WaitForDump("hudChipsWatch", 1, "the fired rule's chip to join it");
        app.WaitForDump("hudChipOrder", "Mez,WatchFire", "the default order to be mez first");
    }

    /// <summary>
    /// MUTE: a muted family leaves the screen, and the other row stays up.
    ///
    /// **The zero is asserted at a moment it can be WRONG at** (trap 62): both families are
    /// answered inside one `HudChipRow.Build` call, so a mez chip on the fight row is proof
    /// the spawn family was asked on that same tick and refused.
    ///
    /// THE PREDICTION: `hudMuted=Spawn`, `hudChipsSpawn=0` and `spawnChipsRow=0` with a
    /// seeded timer running, `hudChipsMez=1`, `hudChipsRow=1`.
    /// </summary>
    [Fact]
    public void AMutedFamilyLeavesTheScreenAndTheOtherRowStaysUp()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = true;
            settings.MutedChipFamilies = ["Spawn"];
        });
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        LandAMez(app);
        app.WaitForDump("hudChipsMez", 1, "the mez chip to arrive on the fight row");

        app.WaitForDump("hudMuted", "Spawn", "the profile's mute to be the one the row read");
        app.WaitForDump("hudChipsSpawn", 0,
            "the muted family to be off screen even though its timer is running");
        app.WaitForDump("spawnChipsRow", 0, "and its row with it, since it has nothing to show");
        app.WaitForDump("hudChipsRow", 1,
            "the fight row to stay up — mute is per family, not a switch for the HUD");
        app.WaitForDump("hudChipOrder", "Mez", "the drawn fight row to be the mez family alone");
    }

    /// <summary>
    /// EDIT MODE, on a profile with NOTHING running — the state that makes the mode worth
    /// having. Since D1 the pencil opens edit on BOTH rows, because each family's Place/Mute
    /// chicklet is in the window that draws it: so both windows must be UP with empty live
    /// rows.
    ///
    /// THE PREDICTION: `hudEdit=1`, `hudChipsRow=1`, `spawnChipsRow=1`, every family count 0,
    /// and the Done chicklet on the fight row (`hudEditDone=1`).
    /// </summary>
    [Fact]
    public void EditModePutsBothRowsOnScreenWithNoChipsOnThem()
    {
        using var app = new AppHarness(null,
            new Dictionary<string, string> { ["EQBUDDY_HUDEDIT"] = "1" });
        app.Launch();

        app.WaitForDump("hudEdit", 1, "Edit HUD to be the mode the rows are in");
        app.WaitForDump("hudChipsRow", 1,
            "the fight row to be on screen carrying its family editors, with no live chip");
        app.WaitForDump("spawnChipsRow", 1,
            "the spawn row to be on screen too — the Spawn editor lives in the spawn row");
        app.WaitForDump("hudChipsSpawn", 0, "no spawn chip to exist on this profile");
        app.WaitForDump("hudChipsMez", 0, "and no mez chip either");
        // The mode's OWN exit, on the row (faces §C). Counted off the panel by tag rather
        // than inferred from `hudEdit` (trap 39).
        app.WaitForDump("hudEditDone", 1, "Done to be on the row the hint points the player at");
    }

    /// <summary>
    /// **THE WAY IN IS ≤1 CLICK FROM THE EXPANDED WIDGET** — Bevel's cog/Options IA faces
    /// §C. `titleEditHud` is 1 only when the pencil is present, VISIBLE and enabled (traps
    /// 29, 17). **The mode is asserted OFF here**, on a launch that did not ask for it — the
    /// negative that stops the pair above passing against a build where the editor is always
    /// up.
    /// </summary>
    [Fact]
    public void TheExpandedWidgetCarriesThePencilThatOpensEditMode()
    {
        using var app = new AppHarness(settings => settings.Minimized = false);
        app.Launch();

        app.WaitForDump("titleEditHud", 1,
            "the expanded title bar's Edit HUD pencil to be present, visible and enabled");
        Assert.Equal(0, app.DumpValue("hudEdit"));
        Assert.Equal(0, app.DumpValue("hudEditDone"));
    }

    // ---- THE GROW DIRECTION (#425), per row since D1, and both halves of trap 42 ----
    //
    // **Neither row asserts a POSITION.** `*Above` is a RELATIONSHIP between two windows
    // (the stack's bottom edge against the widget's top), so it holds on a 1024×768 hosted
    // runner and on David's desk alike.

    /// <summary>A Top with room above it AND below it on the smallest monitor this ever runs
    /// on.</summary>
    private const double SeatTop = 320;

    /// <summary>
    /// **SEATS RUN MINIMIZED, AND THAT IS THE ASSERTION WORKING RATHER THAN A CONVENIENCE.**
    /// The EXPANDED widget is several hundred units tall, so on a 1024×768 runner "under the
    /// widget" does not fit and the flip-above rule sends a stack up whichever way it was
    /// told to grow — which made the first version of the grow pair vacuous.
    /// </summary>
    private static void Seat(AppSettings settings)
    {
        settings.TrackSpawns = true;
        settings.Minimized = true;
        settings.WindowTop = SeatTop;
    }

    /// <summary>
    /// **DOWN IS AN UNTOUCHED PROFILE** — for the spawn row as for the fight row.
    ///
    /// THE PREDICTION (trap 23): `spawnRowGrow=down`, `spawnRowAbove=0`.
    /// </summary>
    [Fact]
    public void AnUntouchedProfileGrowsTheSpawnRowDownwardUnderTheWidget()
    {
        using var app = new AppHarness(Seat);
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        app.WaitForDump("spawnChipsRow", 1, "the spawn row to be on screen while a timer runs");
        app.WaitForDump("spawnRowGrow", "down", "an untouched profile to grow the stack down");
        app.WaitForDump("spawnRowAbove", 0, "the stack to sit under the widget");
    }

    /// <summary>
    /// The spawn row's OWN toggle (D1's `SpawnRowGrowUp`) reaching the screen — and not the
    /// fight row's.
    ///
    /// THE PREDICTION: `spawnRowGrow=up` AND `spawnRowAbove=1`, with `hudChipGrow=down`
    /// beside them: the two rows' directions are two settings.
    /// </summary>
    [Fact]
    public void SpawnRowGrowUpPutsTheSpawnRowOnTheOtherSideOfTheWidget()
    {
        using var app = new AppHarness(settings =>
        {
            Seat(settings);
            settings.SpawnRowGrowUp = true;
        });
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        app.WaitForDump("spawnChipsRow", 1, "the spawn row to be on screen while a timer runs");
        app.WaitForDump("spawnRowGrow", "up", "the profile's direction to be the one the row read");
        app.WaitForDump("spawnRowAbove", 1,
            "the stack to actually be above the widget, not merely to have the setting");
        Assert.Equal("down", app.DumpText("hudChipGrow"));
    }

    /// <summary>
    /// The FIGHT row keeps SA-2's `HudChipRowGrowUp`, reaching the screen through a mez.
    ///
    /// THE PREDICTION: `hudChipGrow=up` AND `hudRowAbove=1`, read off ONE dump line.
    /// </summary>
    [Fact]
    public void FightRowGrowUpPutsTheFightRowOnTheOtherSideOfTheWidget()
    {
        using var app = new AppHarness(settings =>
        {
            Seat(settings);
            settings.TrackSpawns = false;
            settings.HudChipRowGrowUp = true;
        });
        app.Launch();

        LandAMez(app);
        app.WaitForDump("hudChipsMez", 1, "the mez chip to put the fight row on screen");
        app.WaitForDump("hudRowAbove", 1,
            "the fight row to actually be above the widget, not merely to have the setting");
        Assert.Equal("up", app.DumpText("hudChipGrow"));
    }

    private static (int, int, int) Three(int[] v) => (v[0], v[1], v[2]);
}
