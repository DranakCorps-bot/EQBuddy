using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The first-run Setup screen's one RULE and its words (OE-6 — owner LOCK B through Helm
/// on #355, Bevel's pre-design #356, Fable's seat #358).
///
/// The WPF layer has no unit tests, so everything here is the half of the feature that was
/// deliberately kept out of the window: the auto-launch predicate and the sentences. What
/// only a launched app can answer — that the screen appears, that the ⧉ buttons exist on
/// it, that dismissing it sticks — is `tests/EQBuddy.E2E`'s `ShellHostTests`.
/// </summary>
public class SetupReadoutTests
{
    private static ReadinessRow Row(OutputfileKind kind, ReadinessState state) =>
        new(kind, kind.ToString(), "what it feeds", state,
            state == ReadinessState.Scanned ? DateTime.Now : null, "");

    private static IReadOnlyList<ReadinessRow> All(ReadinessState state) =>
    [
        Row(OutputfileKind.Inventory, state),
        Row(OutputfileKind.Achievements, state),
        Row(OutputfileKind.Factions, state),
    ];

    /// <summary>The state the screen exists for: a profile that has run none of them.</summary>
    [Fact]
    public void ItAutoShowsWhenEveryDumpHasNeverBeenRun() =>
        Assert.True(SetupReadout.ShouldAutoShow(All(ReadinessState.NeverScanned), dismissed: false));

    /// <summary>
    /// **EVERY row, not "any" — and this is the assertion that says so.** A player who has
    /// run one command has met the mechanism, and an onboarding screen that kept opening
    /// until all three landed would be a nag rather than an introduction. The remainder is
    /// Home's Readiness block's job, which asks every time the shell is opened.
    /// </summary>
    [Theory]
    [InlineData(OutputfileKind.Inventory)]
    [InlineData(OutputfileKind.Achievements)]
    [InlineData(OutputfileKind.Factions)]
    public void OneDumpThatHasLandedIsEnoughToStopIt(OutputfileKind landed)
    {
        var rows = All(ReadinessState.NeverScanned)
            .Select(r => r.Kind == landed ? Row(r.Kind, ReadinessState.Scanned) : r)
            .ToList();
        Assert.False(SetupReadout.ShouldAutoShow(rows, dismissed: false));
    }

    /// <summary>Satisfied, which is the state the whole thing is aiming at.</summary>
    [Fact]
    public void ItStopsOnceEveryDumpHasLanded() =>
        Assert.False(SetupReadout.ShouldAutoShow(All(ReadinessState.Scanned), dismissed: false));

    /// <summary>The player's own answer, and the ONLY half of this that is a setting. Its
    /// writer and its reader landed in the same change, which is trap 20's rule in the
    /// polarity that costs a capability.</summary>
    [Fact]
    public void TheDismissalStopsItEvenWithNothingRun() =>
        Assert.False(SetupReadout.ShouldAutoShow(All(ReadinessState.NeverScanned), dismissed: true));

    /// <summary>
    /// **No rows means no CHARACTER, and "every row is never-scanned" is vacuously true of
    /// an empty list** — which would have opened Setup over the one profile that has a
    /// better answer already. `HomeReadout.Readiness` returns nothing before EQBuddy has
    /// seen a log line, and Home's whole-room empty is what that profile gets: `/log on` and
    /// the Logs folder, the two things that have to happen before any of these three
    /// commands is worth typing.
    ///
    /// This is the row that would pass a naive `All(…)` and is the reason the predicate
    /// carries a `Count > 0` it looks like it does not need.
    /// </summary>
    [Fact]
    public void ItDoesNotShowOverAProfileWithNoCharacterAtAll() =>
        Assert.False(SetupReadout.ShouldAutoShow([], dismissed: false));

    /// <summary>
    /// **The rows are `HomeReadout`'s, and this is the assertion that pins the ONE producer.**
    /// Bevel's source-of-truth ruling is the whole design: a hand-rolled second copy of
    /// "Inventory / Achievements / Factions" inside a Setup class is trap 33's shape with
    /// the two producers being two hosts, and it stops agreeing the day a fourth dump is
    /// added — a change already on the board (OE-5 PR-1's spellbook row). The predicate
    /// takes the rows rather than building them, so there is nothing here that COULD drift.
    /// </summary>
    [Fact]
    public void TheScreenAsksForExactlyTheDumpsHomeReportsOn()
    {
        var rows = HomeReadout.Readiness(("erollisi", "Dranak"), _ => null);
        Assert.Equal(3, rows.Count);
        Assert.True(SetupReadout.ShouldAutoShow(rows, dismissed: false),
            "a profile with no dumps at all must be exactly the state Setup opens for");

        // And the day a fourth row joins `Readiness()`, this still holds without an edit —
        // which is the property a hand-written list can never have (trap 30).
        Assert.All(rows, r => Assert.Equal(ReadinessState.NeverScanned, r.State));
    }

    /// <summary>
    /// The words exist and say the thing that matters most about the button: closing this
    /// screen is permanent, and where it comes back from.
    ///
    /// **A screen with one close that does not say what the close DOES is the defect**, not
    /// a style preference — the alternative shape (a "not now" beside a "never") is two
    /// paths deciding one question, which is trap 47's shape with the consequence being a
    /// nag rather than a deletion.
    /// </summary>
    [Fact]
    public void TheCloseSaysWhatItDoesAndWhereTheScreenComesBackFrom()
    {
        Assert.NotEmpty(SetupReadout.Headline);
        Assert.NotEmpty(SetupReadout.Lead);
        Assert.NotEmpty(SetupReadout.Done);
        Assert.Contains("not open this by itself again", SetupReadout.ReopenNote,
            StringComparison.OrdinalIgnoreCase);
        // Both ways back are named, because only one of them is this screen: Home keeps
        // asking, Settings re-opens.
        Assert.Contains("Home", SetupReadout.ReopenNote, StringComparison.Ordinal);
        Assert.Contains("Behavior", SetupReadout.ReopenNote, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The re-open entry is on Settings' Behavior tab and there is no fifth tab** — the
    /// signed I-11/#331 count is FOUR, `SettingsRoomTests` pins the strip, and this is the
    /// row that says the Setup words were written against that ruling rather than against a
    /// tab that does not exist.
    /// </summary>
    [Fact]
    public void TheReopenEntryBelongsToTheBehaviorTabThatAlreadyExists()
    {
        Assert.NotEmpty(SetupReadout.BehaviorLabel);
        Assert.NotEmpty(SetupReadout.BehaviorNote);
        Assert.Contains(SettingsTab.Behavior, SettingsSurface.Tabs().Select(t => t.Tab));
        Assert.Equal(4, SettingsSurface.Tabs().Count);
    }
}
