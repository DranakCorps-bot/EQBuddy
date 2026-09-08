using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// **The guard that keeps the suite off a real profile, tested rather than trusted.**
///
/// <see cref="TestProfileIsolation"/> exists because a test once overwrote David's live
/// settings.json (2026-08-14). On 2026-09-07 it happened twice more — ~390 KB down to
/// ~4.9 KB, which is a defaults file — and NOT because the redirect was wrong: because the
/// initializer's first line handed the decision to an inherited environment variable
/// (<c>if (EQBUDDY_APPDATA is set) return;</c>), and the shell the suite was started from
/// was pointing at <c>%AppData%\EQBuddy Evolved</c> to drive the real app. Isolation opted
/// out of itself, in silence, and every assertion in the suite went on passing.
///
/// That is trap 34's shape at the root of the tree: a guard that cannot fail reads as
/// coverage. Nothing in 2,900 tests could see it, because the thing that was wrong was the
/// guard's own premise — "a preset means somebody meant it" — and no test asked what the
/// premise was standing in for (trap 64).
///
/// Asked of the pure <see cref="TestProfileIsolation.Decide"/> rather than by setting
/// EQBUDDY_APPDATA: the module initializer has long since run, and this assembly runs its
/// collections in parallel, so a test that mutated the variable would move every other
/// test's profile out from under it (trap 57). <c>AppPaths.IsProductOwned</c> is separated
/// from <c>AppPaths.IsProductOwnedProfile</c> for exactly this reason, and the live half is
/// covered below by asserting what this process actually did.
///
/// This file is in the settings.json collection because it calls
/// <c>AppSettings.Load()</c>/<c>Save()</c> on the shared throwaway profile.
/// </summary>
[Collection(SettingsFileCollection.Name)]
public class TestProfileIsolationTests
{
    /// <summary>A preset that is not a player profile but is unmistakably somebody's
    /// deliberate-looking choice — the exact shape the old early-return honoured.</summary>
    private static string FakeEvolved =>
        Path.Combine(Path.GetTempPath(), "eqbuddy-isolation-decoy", "EQBuddy Evolved");

    /// <summary>**The prove-fail.** A preset with no opt-in is overridden, full stop. Under
    /// the pre-fix rule this answers "keep the preset" and the suite writes wherever the
    /// parent shell was pointing.</summary>
    [Fact]
    public void APresetWithNoOptInIsOverridden() =>
        Assert.Equal(IsolationChoice.OverrodePreset, Decide(FakeEvolved, allowPreset: null));

    /// <summary>Nothing set at all is the ordinary case: CI, <c>check.ps1</c> and a plain
    /// <c>dotnet test</c> all arrive here, and the redirect fills the gap.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoPresetAtAllIsFilledIn(string? preset) =>
        Assert.Equal(IsolationChoice.FilledTheGap, Decide(preset, allowPreset: null));

    /// <summary>Exactly one spelling means "keep my override". A half-spelled opt-in
    /// isolates, because the direction a typo must fail in is the one where the player's
    /// data survives.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData("yes")]
    [InlineData(" 1")]
    public void OnlyTheExactOptInValueKeepsAPreset(string? allowPreset) =>
        Assert.Equal(IsolationChoice.OverrodePreset, Decide(FakeEvolved, allowPreset));

    /// <summary>And the opt-in does work — otherwise it is not an opt-in, it is a comment.
    /// A harness that has already made its own throwaway directory keeps it.</summary>
    [Fact]
    public void TheOptInKeepsAPresetThatIsNotAPlayersProfile() =>
        Assert.Equal(IsolationChoice.KeptPresetByOptIn,
            Decide(FakeEvolved, TestProfileIsolation.AllowPresetValue));

    /// <summary>
    /// **The belt the opt-in wears.** An opt-in may move the suite to another throwaway
    /// directory; it may not point it at the data this whole guard exists to protect. Both
    /// lines count — an Evolved build's own profile AND the v1 one beside it, which is just
    /// as much somebody's install — and every spelling of a directory is that directory.
    /// </summary>
    [Fact]
    public void EvenTheOptInWillNotPointTheSuiteAtAPlayersProfile()
    {
        foreach (var profile in TestProfileIsolation.PlayerProfiles)
        {
            Assert.Equal(IsolationChoice.RefusedPlayerProfile,
                Decide(profile, TestProfileIsolation.AllowPresetValue));
            Assert.Equal(IsolationChoice.RefusedPlayerProfile,
                Decide(profile + Path.DirectorySeparatorChar,
                    TestProfileIsolation.AllowPresetValue));
            Assert.Equal(IsolationChoice.RefusedPlayerProfile,
                Decide(profile.ToUpperInvariant(), TestProfileIsolation.AllowPresetValue));
        }

        // The pair is the point: %AppData%\EQBuddy Evolved is what the 2026-09-07 sessions
        // exported, and %AppData%\EQBuddy is the v1 profile the transition imports FROM.
        Assert.Equal(2, TestProfileIsolation.PlayerProfiles.Count);
        Assert.Contains(AppPaths.ProductDir, TestProfileIsolation.PlayerProfiles);
        Assert.Contains(AppPaths.LegacyDir, TestProfileIsolation.PlayerProfiles);
    }

    /// <summary>An unusable preset never throws out of the decision — this question gates
    /// the isolation of the whole suite, so it has to have an answer for every string. With
    /// no opt-in it is overridden like any other; with one it is the caller's problem, and
    /// the failure is a save that cannot find its directory rather than one that finds
    /// somebody's.</summary>
    [Theory]
    [InlineData("\0:://not a path")]
    [InlineData("   ")]
    [InlineData("|<>")]
    public void AnUnusablePresetIsOverriddenRatherThanThrown(string preset)
    {
        Assert.Equal(IsolationChoice.OverrodePreset, Decide(preset, allowPreset: null));
        _ = Decide(preset, TestProfileIsolation.AllowPresetValue);
    }

    /// <summary>
    /// **The live half: what THIS process actually did**, rather than what the rule reads
    /// like. The rule being right and the initializer not calling it is trap 42's shape
    /// (present in the source ≠ in effect at runtime), and it is the half that would have
    /// let 2026-09-07 happen anyway.
    /// </summary>
    [Fact]
    public void ThisProcessIsRunningOnAThrowawayProfile()
    {
        Assert.NotEqual(IsolationChoice.RefusedPlayerProfile, TestProfileIsolation.Choice);

        // The invariant, true in every branch: whatever the environment said, the suite is
        // not pointed at anybody's install.
        foreach (var profile in TestProfileIsolation.PlayerProfiles)
            Assert.False(TestProfileIsolation.SameDirectory(AppPaths.Dir, profile),
                $"the suite is writing to a real profile: {AppPaths.Dir}");

        // And unless a caller explicitly opted in, it is under our own temp root.
        if (TestProfileIsolation.Choice != IsolationChoice.KeptPresetByOptIn)
            Assert.StartsWith(
                Path.GetFullPath(Path.Combine(Path.GetTempPath(), TestProfileIsolation.TempRoot)),
                Path.GetFullPath(AppPaths.Dir), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The whole point, stated as the thing that went wrong: a save lands in the throwaway
    /// profile. `AppPaths.Dir` being right is the rule; a file appearing there is the
    /// product of it, and it is what the two clobbers were.
    /// </summary>
    [Fact]
    public void ASaveLandsInTheThrowawayProfileAndNowhereElse()
    {
        var path = AppPaths.File("settings.json");
        foreach (var profile in TestProfileIsolation.PlayerProfiles)
            Assert.False(
                Path.GetFullPath(path).StartsWith(
                    Path.GetFullPath(profile) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase),
                $"a save would land in a real profile: {path}");

        var settings = AppSettings.Load();
        settings.UiScale = 1.05;
        settings.Save();

        Assert.True(File.Exists(path), $"the save did not reach the isolated profile: {path}");
    }

    private static IsolationChoice Decide(string? preset, string? allowPreset) =>
        TestProfileIsolation.Decide(preset, allowPreset, TestProfileIsolation.PlayerProfiles);
}
