namespace EQBuddy.E2E;

/// <summary>
/// THE ONE CHIP ROW (Surface A / SA-2) — the companion window that replaced
/// <c>SpawnChipsWindow</c> and <c>MezChipsWindow</c>.
///
/// **`HudChipRowTests` in EQBuddy.Tests proves the merge; this proves it reaches the
/// screen.** "Present in the build" and "in effect at runtime" are different claims and
/// trap 42 cost two builds to learn it — and the specific thing this fold could break
/// silently is not the arithmetic, it is a family quietly stopping contributing while the
/// row looks perfectly correct with the other one on it. That is what the per-family counts
/// beside `hudChipsRow` are for.
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
    /// <summary>
    /// The spawn family on the row, with one chicklet counting and one gone DUE.
    ///
    /// THE PREDICTION, written before it ran (trap 23): two seeded timers, one 60 s into a
    /// 30-minute cycle and one 30 s past a 10 s cycle, produce `hudChipsRow=1`
    /// `hudChipsSpawn=2` `hudChipsMez=0` `hudChipsDue=1`. The mez count is asserted at ZERO
    /// on purpose — a merge that leaked one family's chips into the other's count is exactly
    /// the fold bug this file exists for, and it would pass an "is the row up" assertion.
    /// </summary>
    [Fact]
    public void TheSpawnFamilyPutsItsCountdownsAndItsDueChipOnTheRow()
    {
        using var app = new AppHarness(settings => settings.TrackSpawns = true);
        app.SeedSpawnTimers(
            ("Runnyeye Citadel", "Kizdean Gix", 60, 1800),
            ("Befallen", "Bones Brackins", 30, 10));
        app.Launch();

        app.WaitForDump("hudChipsRow", 1, "the chip row to be on screen while timers run");
        app.WaitForDump("hudChipsSpawn", 2, "both seeded countdowns to be on the row");
        app.WaitForDump("hudChipsMez", 0, "no mez chip to appear from a spawn-only profile");
        app.WaitForDump("hudChipsDue", 1, "the overdue camp to be the one chip showing DUE");
    }

    /// <summary>
    /// **The visibility half of the SA-2 hosting amendment, which Helm signed on 2026-09-05:
    /// the row is up whenever chips exist, in BOTH HUD states.**
    ///
    /// B3's letter said "inside the HUD (expanded state)". The two retired stacks were
    /// visible regardless of the widget's state — "the stack exists exactly while timers
    /// do" — so an expanded-only row would have subtracted a live capability mid-pass, which
    /// is what the per-item HUD-subtraction gate exists to forbid. Nothing but a launched app
    /// can say which of the two shipped: the code reads the same either way, and a screenshot
    /// of a collapsed widget with a row under it is a screenshot of the collapsed widget.
    /// </summary>
    [Fact]
    public void TheRowIsOnScreenWhileTheWidgetIsMinimizedToo()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.TrackSpawns = true;
        });
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        app.WaitForDump("hudChipsRow", 1, "the chip row to be up with the widget minimized");
        app.WaitForDump("hudChipsSpawn", 1, "the seeded countdown to be on it");
    }

    /// <summary>
    /// The Bevel-signed Camps hide-rule, ON THE MERGED ROW — and the assertion the fold made
    /// possible for the first time.
    ///
    /// While the World window is showing Camps the same timers are already on screen there,
    /// so the SPAWN family leaves the row. It used to be a whole window closing, which any
    /// "is anything up" check could see. On one row the interesting failure is different:
    /// the rule firing for the wrong family, or taking the whole row with it. So this seeds
    /// BOTH families and asserts the row stays up carrying only the fight one.
    ///
    /// THE PREDICTION, written before it ran: `hudChipsRow=1` (the mez chip holds it up),
    /// `hudChipsSpawn=0` (the hide-rule), `hudChipsMez=1`.
    ///
    /// `EQBUDDY_SPAWNS` is the hook that opens the World window on its Camps tab — the same
    /// one `scripts/shoot.ps1` uses for `spawns-window`.
    /// </summary>
    [Fact]
    public void TheCampsTabTakesTheSpawnFamilyOffTheRowAndLeavesTheFightFamilyOnIt()
    {
        using var app = new AppHarness(
            settings => settings.TrackSpawns = true,
            new Dictionary<string, string> { ["EQBUDDY_SPAWNS"] = "Runnyeye Citadel" });
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();
        app.WaitForDump("worldWindowOpen", 1, "the World window to open on its Camps tab");

        // Through the real seam: the two lines the game writes when a mez lands, appended to
        // the file the widget is tailing. A cast line and a landing line is the pair
        // MezTracker correlates — the landing alone would give an untimed chip.
        app.AppendLogLines(
            "You begin casting Mesmerization.",
            "a skeleton has been mesmerized.");

        app.WaitForDump("hudChipsMez", 1, "the mez chip to arrive on the row");
        app.WaitForDump("hudChipsSpawn", 0,
            "the spawn family to leave the row while World is showing Camps");
        app.WaitForDump("hudChipsRow", 1,
            "the row itself to stay up — the hide-rule is per family, not per row");
    }

    // ---- SA-4: PLACE, MUTE, and the Edit mode that sets them ----
    //
    // `HudChipRowTests` in EQBuddy.Tests proves the two settings resolve; these prove they
    // reach the screen. That gap is the whole point of this file — the SA-1 promotion and the
    // trap 42 pair both cost builds to the difference between "in the profile" and "in
    // effect" — and it is wider than usual here, because BOTH settings could resolve
    // perfectly and never be handed to Merge.

    /// <summary>
    /// PLACE: the stored order is the order on screen.
    ///
    /// Two families, one profile that names them backwards. `hudChipOrder` is read off the
    /// ROW — the families in the order they were actually drawn — so it can only say
    /// "Spawn,Mez" if the setting travelled all the way through `HudChipRow.Build`.
    ///
    /// THE PREDICTION, written before it ran (trap 23): `hudChipOrder=Spawn,Mez`, with
    /// `hudChipsSpawn=1` and `hudChipsMez=1` beside it so the token is a statement about a
    /// row that actually has both families on it rather than about an empty one.
    /// </summary>
    [Fact]
    public void TheStoredFamilyOrderIsTheOrderTheRowIsDrawnIn()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = true;
            settings.HudChipOrder = ["Spawn", "Mez", "WatchFire", "Buff"];
        });
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        app.AppendLogLines(
            "You begin casting Mesmerization.",
            "a skeleton has been mesmerized.");
        app.WaitForDump("hudChipsMez", 1, "the mez chip to arrive on the row");

        app.WaitForDump("hudChipsSpawn", 1, "the seeded countdown to be on it too");
        app.WaitForDump("hudChipOrder", "Spawn,Mez",
            "the spawn family to be drawn FIRST, which is the order this profile asks for");
    }

    /// <summary>
    /// The same two families with the DEFAULT profile, which is the negative this pair needs:
    /// without it, an implementation that ignored the setting and always drew mez first would
    /// fail the test above and one that always drew spawn first would pass it.
    ///
    /// THE PREDICTION: `hudChipOrder=Mez,Spawn` — the signed default, combat-urgent first.
    /// </summary>
    [Fact]
    public void TheDefaultProfileDrawsTheFightFamilyFirst()
    {
        using var app = new AppHarness(settings => settings.TrackSpawns = true);
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        app.AppendLogLines(
            "You begin casting Mesmerization.",
            "a skeleton has been mesmerized.");
        app.WaitForDump("hudChipsMez", 1, "the mez chip to arrive on the row");

        app.WaitForDump("hudChipOrder", "Mez,Spawn", "the default order to be mez then spawn");
    }

    /// <summary>
    /// MUTE: a muted family leaves the row, the row stays up, and nothing else moves.
    ///
    /// **The zero is asserted at a moment it can be WRONG at** (trap 62). A negative
    /// assertion made straight after `AppendLogLines` proves nothing — the harness waits for
    /// the tail to READ the bytes, not for the app to have decided anything — so this waits
    /// for the MEZ chip first. Both families are answered inside one `HudChipRow.Build` call,
    /// so a mez chip on the row is proof that the spawn family was asked on that same tick
    /// and refused. With the mute deleted the value is 1 against a demanded 0.
    ///
    /// THE PREDICTION: `hudMuted=Spawn`, `hudChipsSpawn=0` with a seeded timer running,
    /// `hudChipsMez=1`, `hudChipsRow=1`.
    /// </summary>
    [Fact]
    public void AMutedFamilyLeavesTheRowAndTheRestOfItStaysUp()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = true;
            settings.MutedChipFamilies = ["Spawn"];
        });
        app.SeedSpawnTimers(("Runnyeye Citadel", "Kizdean Gix", 60, 1800));
        app.Launch();

        app.AppendLogLines(
            "You begin casting Mesmerization.",
            "a skeleton has been mesmerized.");
        app.WaitForDump("hudChipsMez", 1, "the mez chip to arrive on the row");

        app.WaitForDump("hudMuted", "Spawn", "the profile's mute to be the one the row read");
        app.WaitForDump("hudChipsSpawn", 0,
            "the muted family to be off the row even though its timer is running");
        app.WaitForDump("hudChipsRow", 1,
            "the row itself to stay up — mute is per family, not a switch for the row");
        app.WaitForDump("hudChipOrder", "Mez", "the drawn row to be the fight family alone");
    }

    /// <summary>
    /// EDIT MODE, on a profile with NOTHING on the row — which is the state that makes the
    /// mode worth having and the one a live-chip implementation would get wrong.
    ///
    /// The verbs are per FAMILY, and a family with nothing running has no chicklet to hang
    /// them on: so the two families a player most wants to mute — the ones that keep
    /// interrupting — would be un-editable at exactly the moment they were quiet. Edit mode
    /// puts all four on screen regardless, which means the row window must be UP with an
    /// empty live row, and that is the assertion `hudChipsRow=1` makes here.
    ///
    /// THE PREDICTION: `hudEdit=1`, `hudChipsRow=1`, and every family count 0 — the counts
    /// keep describing the LIVE row while the mode is on, so they are all zero on a profile
    /// with no timers, no mez, no fired rule and no buff.
    /// </summary>
    [Fact]
    public void EditModePutsTheRowOnScreenWithNoChipsOnIt()
    {
        using var app = new AppHarness(null,
            new Dictionary<string, string> { ["EQBUDDY_HUDEDIT"] = "1" });
        app.Launch();

        app.WaitForDump("hudEdit", 1, "Edit HUD to be the mode the row is in");
        app.WaitForDump("hudChipsRow", 1,
            "the row to be on screen carrying the four family editors, with no live chip on it");
        app.WaitForDump("hudChipsSpawn", 0, "no spawn chip to exist on this profile");
        app.WaitForDump("hudChipsMez", 0, "and no mez chip either");
    }
}
