using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The CHILD-process half of live-profile isolation — prove-failed, not assumed.
///
/// <c>TestProfileIsolation</c> (#428) isolates the unit-suite HOST. E2E and
/// <c>shoot.ps1</c> write on the launched exe. Until this policy, a caller
/// dictionary applied after <c>EQBUDDY_APPDATA = ProfileDir</c> could point
/// that child at a live profile, and every host isolation test would still
/// pass. Helm authorized the refuse follow-up after #426; this is it.
///
/// Pure functions only: this assembly runs collections in parallel, and a
/// test that mutated <c>EQBUDDY_APPDATA</c> would move every other test's
/// profile (trap 57).
/// </summary>
public class IsolatedLaunchPolicyTests
{
    private static string TempProfile =>
        Path.Combine(Path.GetTempPath(), "eqbuddy-isolated-launch-tests");

    [Fact]
    public void AThrowawayDirectoryIsNotALivePlayerProfile() =>
        Assert.False(AppPaths.IsLivePlayerDirectory(TempProfile));

    [Fact]
    public void BothProductLinesCountAsLivePlayerProfiles()
    {
        Assert.True(AppPaths.IsLivePlayerDirectory(AppPaths.ProductDir));
        Assert.True(AppPaths.IsLivePlayerDirectory(AppPaths.LegacyDir));
        Assert.True(AppPaths.IsLivePlayerDirectory(AppPaths.ProductDir + Path.DirectorySeparatorChar));
        Assert.True(AppPaths.IsLivePlayerDirectory(AppPaths.LegacyDir.ToUpperInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnEmptyPathIsNotALivePlayerProfile(string? path) =>
        Assert.False(AppPaths.IsLivePlayerDirectory(path));

    /// <summary>
    /// **The hole this exists to close.** A caller env that already names the
    /// live Evolved profile is overwritten, not trusted — the same lesson as
    /// trap 68, one process out.
    /// </summary>
    [Fact]
    public void PinChildProfileOverwritesACallerSuppliedLiveOverride()
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [IsolatedLaunchPolicy.ProfileVar] = AppPaths.ProductDir,
        };

        IsolatedLaunchPolicy.PinChildProfile(env, TempProfile);

        Assert.Equal(TempProfile, env[IsolatedLaunchPolicy.ProfileVar]);
        Assert.False(AppPaths.IsLivePlayerDirectory(env[IsolatedLaunchPolicy.ProfileVar]));
    }

    /// <summary>
    /// **Prove-fail target #1.** Delete the live-isolated-path check and this
    /// goes green — which is the accident of pointing <c>ProfileDir</c> at
    /// AppData.
    /// </summary>
    [Fact]
    public void PinChildProfileRefusesALiveIsolatedPath()
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var ex = Assert.Throws<InvalidOperationException>(
            () => IsolatedLaunchPolicy.PinChildProfile(env, AppPaths.ProductDir));
        Assert.Contains("live player profile", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(env.ContainsKey(IsolatedLaunchPolicy.ProfileVar),
            "a refused pin must not write EQBUDDY_APPDATA at all");
    }

    /// <summary>
    /// **Prove-fail target #2.** A harness that set <c>EQBUDDY_V1_APPDATA</c>
    /// to the real v1 profile would copy a player's settings into a throwaway
    /// Evolved one (the capture-surface failure, with a whole-directory copy
    /// behind it).
    /// </summary>
    [Fact]
    public void PinChildProfileRefusesALiveV1ImportSource()
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [IsolatedLaunchPolicy.V1SourceVar] = AppPaths.LegacyDir,
        };
        var ex = Assert.Throws<InvalidOperationException>(
            () => IsolatedLaunchPolicy.PinChildProfile(env, TempProfile));
        Assert.Contains("import from a live player profile", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PinChildProfileAllowsAFakeV1ImportSource()
    {
        var fakeV1 = Path.Combine(TempProfile, "v1");
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [IsolatedLaunchPolicy.V1SourceVar] = fakeV1,
        };

        IsolatedLaunchPolicy.PinChildProfile(env, TempProfile);

        Assert.Equal(TempProfile, env[IsolatedLaunchPolicy.ProfileVar]);
        Assert.Equal(fakeV1, env[IsolatedLaunchPolicy.V1SourceVar]);
    }

    [Fact]
    public void ClearHarnessOverridesRemovesTheThreeRedirects()
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [IsolatedLaunchPolicy.ProfileVar] = AppPaths.ProductDir,
            [IsolatedLaunchPolicy.V1SourceVar] = AppPaths.LegacyDir,
            [IsolatedLaunchPolicy.AllowLiveVar] = "1",
            ["EQBUDDY_SHELL"] = "1",
        };

        IsolatedLaunchPolicy.ClearHarnessOverrides(env);

        Assert.False(env.ContainsKey(IsolatedLaunchPolicy.ProfileVar));
        Assert.False(env.ContainsKey(IsolatedLaunchPolicy.V1SourceVar));
        Assert.False(env.ContainsKey(IsolatedLaunchPolicy.AllowLiveVar));
        Assert.Equal("1", env["EQBUDDY_SHELL"]);
    }
}
