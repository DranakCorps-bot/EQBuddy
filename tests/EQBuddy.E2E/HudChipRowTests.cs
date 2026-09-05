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
}
