using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// **The suite asserting that it cannot reach a player's profile.**
///
/// <see cref="TestProfileIsolation"/> is the only thing between ~2,900 tests and
/// <c>%AppData%\EQBuddy Evolved</c>, and until 2026-09-07 it stepped aside whenever
/// <c>EQBUDDY_APPDATA</c> was already set — so an agent seat that had inherited the
/// Evolved launcher's export ran the whole suite against the live profile and truncated
/// David's <c>settings.json</c> (390 KB → 4.8 KB). Nothing in the tree said a word: every
/// test passed, because they were all writing somewhere writable.
///
/// So the guard is asserted rather than assumed, from two directions. The pure rule
/// (<see cref="TestProfileIsolation.ShouldRedirect"/>) says what the module initializer
/// decides; the environment assertions below say what it actually DID to this run — trap
/// 42's distinction, and the reason the second kind exists at all: "the redirect is in the
/// source" and "the redirect is in force" are different claims, and only the second one is
/// the guard.
/// </summary>
public class TestProfileIsolationTests
{
    private static string Full(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string Roaming =>
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>
    /// **Only the exact string <c>"1"</c> lets a live profile through.** Every other value
    /// — unset, empty, "0", "false", a stray space — redirects, because the cost of the two
    /// mistakes is not symmetric: refusing to honour a malformed opt-out wastes a
    /// developer's minute, and honouring one destroys a profile that cannot be rebuilt
    /// (trap 65).
    /// </summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0", true)]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData(" 1", true)]
    [InlineData("1 ", true)]
    [InlineData("1", false)]
    public void OnlyTheExactOptOutLetsALiveProfileThrough(string? optOut, bool redirects) =>
        Assert.Equal(redirects, TestProfileIsolation.ShouldRedirect(optOut));

    /// <summary>
    /// **An inherited <c>EQBUDDY_APPDATA</c> is not a decision, so it does not win.** This
    /// is the whole change: the old rule asked "is a profile already chosen?", which the
    /// launcher's export answers yes to on every seat that has ever run Evolved.
    /// </summary>
    [Fact]
    public void AnAlreadySetProfileVariableDoesNotStopTheRedirect() =>
        Assert.True(TestProfileIsolation.ShouldRedirect(optOut: null));

    /// <summary>
    /// The profile this run is actually using is a throwaway one this assembly created —
    /// asked of the environment, not of the source.
    ///
    /// **This is the test the opt-out turns red, on purpose.** A run under
    /// <c>EQBUDDY_ALLOW_LIVE_APPDATA=1</c> is not isolated and the suite says so rather
    /// than passing quietly; the message names the variable, so the failure reads as the
    /// door somebody opened rather than as a defect in whatever they were reviewing.
    /// </summary>
    [Fact]
    public void TheProfileInForceIsAThrowawayTempDirectory()
    {
        var dir = Full(AppPaths.Dir);
        var temp = Full(Path.Combine(Path.GetTempPath(), "eqbuddy-tests"));

        Assert.True(dir.StartsWith(temp, StringComparison.OrdinalIgnoreCase),
            $"the profile in force is {dir}, not a throwaway under {temp}. " +
            $"{TestProfileIsolation.OptOutVar}=" +
            $"{Environment.GetEnvironmentVariable(TestProfileIsolation.OptOutVar) ?? "(unset)"}" +
            " — with the opt-out set this suite writes wherever the environment points it, " +
            "including a player's live profile.");
        Assert.True(Directory.Exists(dir), $"the isolated profile {dir} was never created");
    }

    /// <summary>
    /// **The claim that matters: neither line's real profile is in force.** Evolved
    /// (<c>AppPaths.ProductDir</c>) is what the 2026-09-07 truncation hit; v1
    /// (<c>AppPaths.LegacyDir</c>) is the import SOURCE and is equally a player's data. The
    /// roaming check is the general form — it fails for any future directory either of
    /// them grows, rather than only for the two we can name today.
    /// </summary>
    [Fact]
    public void NoRealProfileDirectoryIsInForce()
    {
        var dir = Full(AppPaths.Dir);

        Assert.NotEqual(Full(AppPaths.ProductDir), dir, StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual(Full(AppPaths.LegacyDir), dir, StringComparer.OrdinalIgnoreCase);
        Assert.False(dir.StartsWith(Full(Roaming), StringComparison.OrdinalIgnoreCase),
            $"the suite is writing inside %AppData% ({dir})");
    }

    /// <summary>
    /// And because the profile is isolated, <c>ProfileImport</c> refuses to run in it — so
    /// a suite run can never copy a real v1 profile into a throwaway one either.
    /// <c>ProfileImportTests.AnIsolatedProfileIsNeverOfferedAnImport</c> holds the rule;
    /// this holds that THIS run satisfies its premise.
    /// </summary>
    [Fact]
    public void TheIsolatedProfileIsNotProductOwned() =>
        Assert.False(AppPaths.IsProductOwnedProfile);

    /// <summary>
    /// **What a displaced override proves, and it is the assertion the prove-run drives.**
    /// Run the suite with <c>EQBUDDY_APPDATA</c> pointing anywhere — including at a live
    /// profile — and the variable is recorded here and NOT used. On an ordinary run nothing
    /// was inherited and this test has nothing to say, which is exactly the shape trap 34
    /// warns about: **so it is not the guard.** The guard is
    /// <see cref="NoRealProfileDirectoryIsInForce"/>, which asserts unconditionally; this
    /// one is what makes the displaced value legible when a run DID inherit one, and it is
    /// what the prove-run in the PR drives.
    /// </summary>
    [Fact]
    public void AnInheritedOverrideWasDisplacedRatherThanTrusted()
    {
        var displaced = Environment.GetEnvironmentVariable(TestProfileIsolation.DisplacedVar);

        // Nothing inherited: there is nothing to displace, and the temp-directory
        // assertions above are already the whole of this run's story.
        if (displaced is not { Length: > 0 }) return;

        Assert.NotEqual(Full(displaced), Full(AppPaths.Dir), StringComparer.OrdinalIgnoreCase);
    }
}
