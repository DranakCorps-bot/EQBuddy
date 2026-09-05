using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The chip families' existence rules (`ChipStackPlan`), lifted out of both MainWindows on
/// 2026-08-27 — until then the Bevel-signed Camps hide-rule was an inline expression on each
/// lane and no test could see it. The source scan at the bottom is the half that keeps it
/// lifted: the widget must ASK the plan, never re-grow its own copy of the decision.
///
/// **The Options-open placement preview left in Surface A / SA-2**, with free placement —
/// the one row is slaved to the HUD, saves no coordinates and cannot be dragged, so there is
/// nothing to park and nothing to preview. `FightStack` lost the three parameters that only
/// the preview read; what is left is the question this class is for. Its former tests are
/// gone rather than rewritten against a behaviour that no longer exists.
/// </summary>
public class ChipStackPlanTests
{
    // ---- Spawn family: ambient, exists exactly while timers do… ----

    [Fact]
    public void SpawnStackShowsWhileTimersRun()
        => Assert.True(ChipStackPlan.SpawnStack(
            trackSpawns: true, hiddenForFocus: false, worldOnCamps: false, hasActiveTimers: true));

    [Theory]
    // …except while the World window is visible ON CAMPS (Bevel-signed hide-rule): the
    // same timers are already on screen there.
    [InlineData(true, false, true, true)]
    // Focus-hide wins over everything — the player asked the overlay to get out of the way.
    [InlineData(true, true, false, true)]
    // Spawn tracking off means no chips, whatever the timers say.
    [InlineData(false, false, false, true)]
    // No running timers, no chips — the family never idles empty.
    [InlineData(true, false, false, false)]
    public void SpawnStackHides(bool track, bool hidden, bool worldOnCamps, bool timers)
        => Assert.False(ChipStackPlan.SpawnStack(track, hidden, worldOnCamps, timers));

    // The rule is Camps-tab-visible specifically: any other World tab (or a closed window)
    // reaches this call with worldOnCamps=false and leaves the chips up. Asserted as its
    // own fact so the parameter's meaning can't quietly widen to "World window open".
    [Fact]
    public void WorldOnAnyOtherTabLeavesTheStackUp()
        => Assert.True(ChipStackPlan.SpawnStack(
            trackSpawns: true, hiddenForFocus: false, worldOnCamps: false, hasActiveTimers: true));

    // ---- Fight family: mez + slow ----

    [Theory]
    [InlineData(true, false)]   // a live mez chip alone
    [InlineData(false, true)]   // a live slow chip alone
    public void FightStackShowsForAnyLiveChip(bool mez, bool slow)
        => Assert.True(ChipStackPlan.FightStack(hiddenForFocus: false,
            mezHasChips: mez, slowHasChips: slow));

    [Fact]
    public void FocusHideBeatsALiveChip()
        => Assert.False(ChipStackPlan.FightStack(hiddenForFocus: true,
            mezHasChips: true, slowHasChips: true));

    /// <summary>The family never idles empty — and since SA-2 there is no exception. An
    /// open Options window used to force it as a placement preview; the row it would have
    /// previewed has no position for the player to choose.</summary>
    [Fact]
    public void NothingLiveShowsNothing()
        => Assert.False(ChipStackPlan.FightStack(hiddenForFocus: false,
            mezHasChips: false, slowHasChips: false));

    // ---- The scan that keeps the decision lifted (trap 34's positive half) ----

    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    /// <summary>The widget asks the plan and keeps no copy of its own.
    ///
    /// **This scanned two lanes until E-2 (2026-09-04), and losing the second one does not
    /// make it pointless — it changes what it is for.** The old reason was drift between
    /// hand-copied twins; the reason now is that the decision must stay LIFTED. A widget
    /// that grows its own `if (timers.Count > 0)` passes every unit test in this file —
    /// `ChipStackPlan` would still be correct and simply not consulted — and only a scan
    /// can see that.</summary>
    [Theory]
    [InlineData("EQBuddy", "MainWindow.xaml.cs")]
    public void TheWidgetAsksThePlanAndKeepsNoCopyOfItsOwn(string project, string file)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, file));

        Assert.Contains("ChipStackPlan.SpawnStack(", text);
        Assert.Contains("ChipStackPlan.FightStack(", text);
        // …and the row it feeds is the shared one, not a second merge grown in the window.
        Assert.Contains("HudChipRow.Merge(", text);
        // The retired placement preview's wording must not come back as a literal here.
        Assert.DoesNotContain("drag me — chips appear here", text);
    }
}
