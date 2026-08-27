using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The overlay chip stacks' existence rules (`ChipStackPlan`), lifted out of both
/// MainWindows on 2026-08-27 — until then the Bevel-signed Camps hide-rule was an inline
/// expression on each lane and no test could see it. The source scan at the bottom is the
/// half that keeps it lifted: both widgets must ask the plan, neither may re-grow its own
/// copy of the decision or the placement-preview wording.
/// </summary>
public class ChipStackPlanTests
{
    // ---- Spawn stack: ambient, exists exactly while timers do… ----

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
    // Spawn tracking off means no stack, whatever the timers say.
    [InlineData(false, false, false, true)]
    // No running timers, no stack — the stack never idles empty.
    [InlineData(true, false, false, false)]
    public void SpawnStackHides(bool track, bool hidden, bool worldOnCamps, bool timers)
        => Assert.False(ChipStackPlan.SpawnStack(track, hidden, worldOnCamps, timers));

    // The rule is Camps-tab-visible specifically: any other World tab (or a closed window)
    // reaches this call with worldOnCamps=false and leaves the stack up. Asserted as its
    // own fact so the parameter's meaning can't quietly widen to "World window open".
    [Fact]
    public void WorldOnAnyOtherTabLeavesTheStackUp()
        => Assert.True(ChipStackPlan.SpawnStack(
            trackSpawns: true, hiddenForFocus: false, worldOnCamps: false, hasActiveTimers: true));

    // ---- Fight stack: mez + slow, plus the Options-open placement preview ----

    [Theory]
    [InlineData(true, false)]   // a live mez chip alone
    [InlineData(false, true)]   // a live slow chip alone
    public void FightStackShowsForAnyLiveChip(bool mez, bool slow)
        => Assert.True(ChipStackPlan.FightStack(hiddenForFocus: false, optionsOpen: false,
            mezEnabled: mez, slowEnabled: slow, mezHasChips: mez, slowHasChips: slow));

    [Fact]
    public void OptionsOpenForcesTheStackAsAPlacementPreviewEvenWhenEmpty()
        => Assert.True(ChipStackPlan.FightStack(hiddenForFocus: false, optionsOpen: true,
            mezEnabled: false, slowEnabled: true, mezHasChips: false, slowHasChips: false));

    [Fact]
    public void OptionsOpenWithBothKindsDisabledShowsNothing()
        => Assert.False(ChipStackPlan.FightStack(hiddenForFocus: false, optionsOpen: true,
            mezEnabled: false, slowEnabled: false, mezHasChips: false, slowHasChips: false));

    [Fact]
    public void FocusHideBeatsEvenALiveChipAndAnOpenOptionsWindow()
        => Assert.False(ChipStackPlan.FightStack(hiddenForFocus: true, optionsOpen: true,
            mezEnabled: true, slowEnabled: true, mezHasChips: true, slowHasChips: true));

    [Fact]
    public void NothingLiveAndOptionsClosedShowsNothing()
        => Assert.False(ChipStackPlan.FightStack(hiddenForFocus: false, optionsOpen: false,
            mezEnabled: true, slowEnabled: true, mezHasChips: false, slowHasChips: false));

    // ---- The placement preview chip: one home for a player-facing string ----

    [Fact]
    public void PlacementPreviewIsADraggablePlaceholderNotATimer()
    {
        var chip = ChipStackPlan.PlacementPreview();
        Assert.Equal("drag me — chips appear here", chip.Name);
        Assert.Equal("ChevronsDown", chip.Icon);   // the fight stack's own icon, not Timer
        Assert.False(chip.IsDue);
        Assert.Equal("", chip.CountdownText);
        Assert.Contains("disappears when", chip.Detail);
    }

    // ---- The scan that keeps the decision lifted (trap 34's positive half) ----

    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    [Theory]
    [InlineData("EQBuddy", "MainWindow.xaml.cs")]
    [InlineData("EQBuddy.Avalonia", "MainWindow.cs")]
    public void BothLanesAskThePlanAndNeitherKeepsItsOwnCopy(string project, string file)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, file));

        Assert.Contains("ChipStackPlan.SpawnStack(", text);
        Assert.Contains("ChipStackPlan.FightStack(", text);
        Assert.Contains("ChipStackPlan.PlacementPreview()", text);
        // The wording lives in the plan; a lane re-growing its own literal is the drift
        // this scan exists to fail.
        Assert.DoesNotContain("drag me — chips appear here", text);
    }
}
