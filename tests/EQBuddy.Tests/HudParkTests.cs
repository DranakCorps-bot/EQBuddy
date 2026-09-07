using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// FREE PLACEMENT (OE-8) — the sums behind "the chip row and the under-bar panel can be
/// parked anywhere", asserted without a window because the WPF layer has none
/// (docs/TestPlan.md §5) and because this is the class of decision that has already cost
/// this repo two releases.
///
/// **THE TESTS ARE NAMED FOR THE THREE ACTORS, and that is trap 49's closing rule rather
/// than a style.** The window-height follower shipped a `selfSet` flag that asked "did I
/// cause this, or the player?" — correct, and irrelevant, because a `SizeToContent` window
/// has a THIRD participant that the model had no name for: the toolkit, resizing it on every
/// content change. Thirteen green tests agreed with the bug, all of them written over the
/// same two-actor world. So the participants go in the names here:
///
/// <list type="bullet">
/// <item>**follower** — the per-tick recompute from the widget. It PLACES and never
/// persists.</item>
/// <item>**toolkit** — WPF resizing a `SizeToContent` window as chicklets arrive and leave.
/// It changes the window's extent and must never move the anchor.</item>
/// <item>**player** — a drag with an END, which is the only thing in the app that may write
/// a park pair.</item>
/// </list>
///
/// The separation is BY CONSTRUCTION rather than by flag: only a gesture has an end, so only
/// the player can reach the setting. What these tests can hold is the other half — that the
/// arithmetic each actor drives answers correctly, and that an unreachable park is a THIRD
/// state rather than a correction.
/// </summary>
public class HudParkTests
{
    // A 1920×1040 primary work area, as every "the desk is ordinary" case below.
    private const double AreaLeft = 0, AreaTop = 0, AreaRight = 1920, AreaBottom = 1040;

    // ---- The three modes -------------------------------------------------------------

    /// <summary>The default: an untouched profile is NaN, which is slaved — byte-for-byte the
    /// SA-2 app, with no migration step to run twice (trap 55).</summary>
    [Fact]
    public void AnUntouchedProfileIsSlavedBecauseNaNIsTheDefault()
    {
        Assert.Equal(HudChipRow.HudParkMode.Slaved,
            HudChipRow.ParkMode(double.NaN, double.NaN, reachable: true));
        Assert.False(HudChipRow.IsParked(double.NaN, double.NaN));
        Assert.Equal("slaved", HudChipRow.ParkKey(double.NaN, double.NaN));
    }

    /// <summary>A HALF-written pair is not a position. A hand-edited file or an interrupted
    /// write can leave one finite and one NaN, and placing a window at NaN puts it nowhere at
    /// all — so both halves have to be real before this is a park.</summary>
    [Theory]
    [InlineData(400, double.NaN)]
    [InlineData(double.NaN, 300)]
    public void AHalfWrittenPairIsSlavedRatherThanAWindowAtNaN(double left, double top)
    {
        Assert.False(HudChipRow.IsParked(left, top));
        Assert.Equal(HudChipRow.HudParkMode.Slaved,
            HudChipRow.ParkMode(left, top, reachable: true));
    }

    /// <summary>The player dragged it somewhere this desk can show: parked.</summary>
    [Fact]
    public void ThePlayersDragEndProducesAParkedPair()
    {
        Assert.Equal(HudChipRow.HudParkMode.Parked,
            HudChipRow.ParkMode(400, 300, reachable: true));
        Assert.Equal("400,300", HudChipRow.ParkKey(400, 300));
    }

    /// <summary>
    /// THE MISSING MONITOR (#117, Snagglefern's four-screen rig) — the state that has to be a
    /// THIRD reading rather than a correction.
    ///
    /// A park on a display that is asleep, detached or on the far side of an RDP hop fails
    /// the reachability check. The window runs slaved for the SESSION and the pair survives:
    /// the monitors come back and the park comes back with them. Persisting the fallback
    /// instead would permanently teleport a spot the player chose with care, which is the
    /// exact bug `WindowPlacement.PositionToPersist` exists for.
    ///
    /// A harness cannot detach a display, so this half stays a unit test and says so — the
    /// plan named it that way rather than pretending `drag-verify.ps1` could reach it.
    /// </summary>
    [Fact]
    public void AParkOnAMissingMonitorRunsSlavedAndTheSettingIsStillThere()
    {
        var mode = HudChipRow.ParkMode(-4000, 1200, reachable: false);
        Assert.Equal(HudChipRow.HudParkMode.Unreachable, mode);
        Assert.NotEqual(HudChipRow.HudParkMode.Slaved, mode);       // three readings, not two
        // The pair is untouched by the decision — nothing here rewrites it, and nothing in
        // the app can, because the only writer is the end of a drag.
        Assert.Equal("-4000,1200", HudChipRow.ParkKey(-4000, 1200));
    }

    // ---- The toolkit, growing a SizeToContent window ---------------------------------

    /// <summary>
    /// **THE TOOLKIT GROWS THE WINDOW AND THE ANCHOR DOES NOT MOVE** — the #122/#152
    /// mechanism, inverted. Those bugs were a window that moved ITSELF and rewrote its own
    /// anchor as it went; here a chicklet arriving widens and heightens the window, the
    /// anchored corner stays exactly where the player dropped it, and growth runs away from
    /// it.
    /// </summary>
    [Fact]
    public void TheToolkitGrowingTheWindowLeavesThePlayersAnchorWhereItWas()
    {
        var small = HudChipRow.ParkedPlacement(600, 400, 180, 30,
            AreaLeft, AreaTop, AreaRight, AreaBottom);
        var grown = HudChipRow.ParkedPlacement(600, 400, 620, 64,
            AreaLeft, AreaTop, AreaRight, AreaBottom);
        Assert.Equal((600d, 400d), small);
        Assert.Equal((600d, 400d), grown);
    }

    /// <summary>A size that is not real yet — 0 on the first layout pass, NaN before one —
    /// draws at the anchor rather than being clamped against a measurement that never
    /// happened. "We cannot tell yet" and "draw where you were put" are the same instruction,
    /// which is the rule `Placement` already states for the slaved branch.</summary>
    [Theory]
    [InlineData(0d)]
    [InlineData(double.NaN)]
    public void TheToolkitHasNotMeasuredYetSoTheAnchorIsUsedUnclamped(double extent)
    {
        Assert.Equal((1900d, 1030d), HudChipRow.ParkedPlacement(1900, 1030, extent, extent,
            AreaLeft, AreaTop, AreaRight, AreaBottom));
    }

    /// <summary>Grown past the monitor's edge, the window is pulled back just enough to fit —
    /// it does not FLIP. A parked window has nothing to avoid (that is the slaved branch's
    /// problem, where the widget is occupying the space below), so a flip would teleport it a
    /// full window-width away from a corner the player deliberately chose.</summary>
    [Fact]
    public void TheToolkitGrowingPastTheEdgeClampsRatherThanFlippingTheWindowAway()
    {
        var (left, top) = HudChipRow.ParkedPlacement(1800, 1000, 400, 120,
            AreaLeft, AreaTop, AreaRight, AreaBottom);
        Assert.Equal(1520, left);   // 1920 - 400, not 1800 - 400
        Assert.Equal(920, top);     // 1040 - 120
    }

    /// <summary>Wider than the monitor it is parked on: the LEADING edge wins, because the
    /// corner a player can still grab is the one they parked at.</summary>
    [Fact]
    public void AWindowWiderThanItsMonitorKeepsTheEdgeThePlayerCanGrab()
    {
        var (left, _) = HudChipRow.ParkedPlacement(300, 200, 3000, 40,
            AreaLeft, AreaTop, AreaRight, AreaBottom);
        Assert.Equal(AreaLeft, left);
    }

    /// <summary>
    /// **THE CLAMP READS THE PARKED POINT'S OWN MONITOR** — the plan's §2.2 implement check,
    /// and the reason `ScreenGuard.WorkAreaAt` exists rather than another
    /// `SystemParameters.WorkArea` read.
    ///
    /// A row parked on a second display to the LEFT of the primary lives at negative
    /// coordinates. Clamped against the primary's area (0…1920) it would be yanked to 0 the
    /// first time a chicklet arrived — a window leaving a screen the player put it on, once
    /// per fight. Against its own monitor's area it does not move at all.
    /// </summary>
    [Fact]
    public void ASecondMonitorParkIsClampedAgainstThatMonitorAndNotThePrimary()
    {
        var onItsOwn = HudChipRow.ParkedPlacement(-1500, 300, 400, 60,
            areaLeft: -1920, areaTop: 0, areaRight: 0, areaBottom: 1040);
        Assert.Equal((-1500d, 300d), onItsOwn);

        // The same park judged against the PRIMARY's area — what the pre-OE-8 code would have
        // done — is the committed negative: it proves the assertion above is not vacuous.
        var yanked = HudChipRow.ParkedPlacement(-1500, 300, 400, 60,
            AreaLeft, AreaTop, AreaRight, AreaBottom);
        Assert.Equal(AreaLeft, yanked.Left);
    }

    /// <summary>A work area that has not been measured — a half-initialised host, a headless
    /// run — answers with the anchor rather than clamping against zeroes. Trap 1's family: a
    /// number that is not a measurement is not a boundary.</summary>
    [Fact]
    public void AnUnmeasuredWorkAreaLeavesTheAnchorAlone()
    {
        Assert.Equal((500d, 500d), HudChipRow.ParkedPlacement(500, 500, 200, 40,
            double.NaN, double.NaN, double.NaN, double.NaN));
    }

    // ---- The follower, still the default ---------------------------------------------

    /// <summary>The slaved branch is untouched by OE-8, and this is the assertion that says
    /// so: the same call the SA-2 row has always made still puts the row directly under the
    /// widget with its left edges aligned.</summary>
    [Fact]
    public void TheFollowerStillPlacesASlavedRowExactlyWhereSA2Did()
    {
        var (left, top) = HudChipRow.Placement(
            hudLeft: 100, hudTop: 200, hudHeight: 40, rowHeight: 30,
            workAreaTop: AreaTop, workAreaBottom: AreaBottom);
        Assert.Equal(100, left);
        Assert.Equal(200 + 40 + HudChipRow.HudGap, top);
    }

    // ---- The wrap cap ----------------------------------------------------------------

    /// <summary>One function for the cap, so the slaved and parked paths cannot answer it
    /// differently — the slaved row passes the widget's monitor and the parked row passes the
    /// parked point's, and the arithmetic is the same either way (trap 4).</summary>
    [Fact]
    public void TheWrapCapIsOneAnswerForBothPlacements()
    {
        Assert.Equal(1920, HudChipRow.WrapWidth(1920));
        Assert.Equal(HudChipRow.MinWrapWidth, HudChipRow.WrapWidth(0));
        Assert.Equal(HudChipRow.MinWrapWidth, HudChipRow.WrapWidth(double.NaN));
    }

    // ---- The panel's taken width (OE-1b lock 3) --------------------------------------

    /// <summary>NaN means "the shipped width", the same sentinel convention the park pairs
    /// use — so a reset profile gets OE-7's single width with no migration step.</summary>
    [Fact]
    public void AnUntouchedProfileGetsTheShippedPanelWidth()
    {
        Assert.Equal(300, HudChipRow.PanelWidth(double.NaN, 300, 1920));
        Assert.Equal(300, HudChipRow.PanelWidth(0, 300, 1920));
    }

    /// <summary>A width the player took is kept, clamped to a floor a header can still render
    /// in and to the monitor it is drawn on.</summary>
    [Theory]
    [InlineData(460, 460)]
    [InlineData(40, HudChipRow.MinPanelWidth)]
    [InlineData(4000, 1920)]
    public void ThePlayersTakenPanelWidthIsKeptWithinTheMonitorAndTheFloor(
        double saved, double expected)
    {
        Assert.Equal(expected, HudChipRow.PanelWidth(saved, 300, 1920));
    }

    /// <summary>
    /// **THE EDGE DRAG DIVIDES BY THE CHIP SCALE, and that is trap 1 rather than a detail.**
    /// The cursor travels in screen units while the panel's chrome sits under `ChipScale`'s
    /// `LayoutTransform`, so a 4K player at 1.5× who drags 150px to the right has widened the
    /// chrome by 100 — and adding the raw delta would be correct at 100% and wrong everywhere
    /// else, which is #144's exact shape. `WidgetMetrics.ContentHeightFromDrag` is the
    /// precedent this copies.
    /// </summary>
    [Fact]
    public void ThePlayersEdgeDragIsInScreenUnitsAndTheChromeIsNot()
    {
        Assert.Equal(400, HudChipRow.PanelWidthFromDrag(300, 150, chipScale: 1.5, grip: 1));
        Assert.Equal(450, HudChipRow.PanelWidthFromDrag(300, 150, chipScale: 1.0, grip: 1));
        // The committed negative: at 1.5× the raw delta would have said 450.
        Assert.NotEqual(450, HudChipRow.PanelWidthFromDrag(300, 150, chipScale: 1.5, grip: 1));
    }

    /// <summary>The LEFT edge narrows when the pointer moves right. One function with a sign
    /// rather than two, so the two edges cannot drift apart the way two near-copies of one
    /// renderer did before SA-2.</summary>
    [Fact]
    public void ThePlayersLeftEdgeDragNarrowsWhereTheRightEdgeWidens()
    {
        Assert.Equal(200, HudChipRow.PanelWidthFromDrag(300, 100, chipScale: 1, grip: -1));
        Assert.Equal(400, HudChipRow.PanelWidthFromDrag(300, 100, chipScale: 1, grip: 1));
    }

    /// <summary>A drag cannot take the panel below the floor, whichever edge it is on: below
    /// it the header's icon, title and two buttons stop being a header and start being an
    /// ellipsis.</summary>
    [Fact]
    public void ThePlayerCannotDragThePanelBelowTheFloor()
    {
        Assert.Equal(HudChipRow.MinPanelWidth,
            HudChipRow.PanelWidthFromDrag(300, 5000, chipScale: 1, grip: -1));
    }
}
