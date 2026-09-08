using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// **The Evolved/v1 profile split is a PRODUCT property now, not a launcher one** — TR-1's
/// first change (Fable's transition plan §0.2, Helm-signed).
///
/// Before this, <c>AppPaths.Dir</c> hardcoded "EQBuddy" and the <c>EQBuddy Evolved</c>
/// directory existed only because two local scripts set <c>EQBUDDY_APPDATA</c> before
/// launching. Any door that bypassed them — a published Evolved exe, double-clicked — ran
/// Evolved on the v1 profile and rewrote it on the first load. That is trap 42's shape
/// (present in the script ≠ in effect at runtime) standing in front of a player's data, so
/// what is asserted here is the RULE rather than this machine's directory layout.
/// </summary>
public class ProfileSplitTests
{
    [Theory]
    [InlineData(1, AppPaths.LegacyDirName)]
    [InlineData(2, AppPaths.EvolvedDirName)]
    [InlineData(3, AppPaths.EvolvedDirName)]
    public void TheProfileDirectoryIsDecidedByTheProductLine(int major, string expected) =>
        Assert.Equal(expected, AppPaths.DirNameFor(major));

    /// <summary>The two names are RESERVED to their lines. Already-shipped v1 binaries
    /// identify their profile by "EQBuddy" and cannot be patched — the same reasoning the
    /// transition plan §3 applies to <c>EQBuddySetup.exe</c> — so the day somebody
    /// "tidies" these strings is the day every v1 install on a machine loses its
    /// profile.</summary>
    [Fact]
    public void TheTwoDirectoryNamesAreTheOnesTheShippedProductsUse()
    {
        Assert.Equal("EQBuddy", AppPaths.LegacyDirName);
        Assert.Equal("EQBuddy Evolved", AppPaths.EvolvedDirName);
        // The exact string scripts/install-local.ps1 and scripts/Launch-Evolved-Shell.cmd
        // build from %APPDATA%; both say in their own comments that they move when this
        // does. A capital difference here is a second profile that looks like an EQBuddy
        // which has forgotten everything.
        Assert.NotEqual(AppPaths.LegacyDirName, AppPaths.EvolvedDirName);
    }

    /// <summary>**One number, one home** (trap 4 with a version in it). "The major at which
    /// EQBuddy stops being cross-platform" and "the major at which it takes its own profile
    /// directory" are the same event; two constants for it would agree today and disagree
    /// the first time one moved. This is not a coincidence check —
    /// <c>WindowsOnlyMajor</c> is DEFINED as <c>EvolvedMajor</c> — it is the assertion that
    /// nobody has re-inlined it.</summary>
    [Fact]
    public void TheEvolvedMajorAndTheWindowsOnlyMajorAreOneNumber() =>
        Assert.Equal(AppPaths.EvolvedMajor, LegacyPlatformUpdatePolicy.WindowsOnlyMajor);

    /// <summary>This build IS the Evolved line, read off Core's own assembly version —
    /// which is <c>Directory.Build.props</c>' <c>&lt;Version&gt;</c>, the same number
    /// EQBuddy.exe reports. Asked of the product rather than of the entry assembly, which
    /// under <c>dotnet test</c> is the test host and has nothing to say about it.</summary>
    [Fact]
    public void ThisTreeIsTheEvolvedLine()
    {
        Assert.True(AppPaths.IsEvolvedLine);
        Assert.Equal(AppPaths.EvolvedDirName, Path.GetFileName(AppPaths.ProductDir));
    }

    /// <summary>The v1 profile sits BESIDE the Evolved one and never inside it. Named here
    /// because it is the import's SOURCE and nothing else in the product may write to
    /// it.</summary>
    [Fact]
    public void TheLegacyProfileIsBesideTheProductOneNeverInsideIt()
    {
        Assert.Equal(AppPaths.LegacyDirName, Path.GetFileName(AppPaths.LegacyDir));
        Assert.NotEqual(
            Path.GetFullPath(AppPaths.LegacyDir), Path.GetFullPath(AppPaths.ProductDir));
        Assert.False(Path.GetFullPath(AppPaths.ProductDir)
            .StartsWith(Path.GetFullPath(AppPaths.LegacyDir) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// **The override still wins, and that is the rule the whole test suite stands on.**
    /// This assembly's module initializer points EQBUDDY_APPDATA at a temp directory
    /// because a test once overwrote David's live settings.json; TR-1 must not have
    /// weakened it.
    /// </summary>
    [Fact]
    public void TheEnvironmentOverrideStillDecidesWhereTheProfileIs()
    {
        Assert.Equal(Environment.GetEnvironmentVariable("EQBUDDY_APPDATA"), AppPaths.Dir);
        Assert.NotEqual(Path.GetFullPath(AppPaths.ProductDir), Path.GetFullPath(AppPaths.Dir));
    }

    /// <summary>
    /// **The question the import gates on**, and the two answers that are easy to get
    /// backwards: a redirect somewhere ELSE is an isolated profile (a test, a shot, an E2E
    /// run) and nothing about the v1 transition may happen inside one — but a redirect that
    /// POINTS AT the product directory is not a redirect at all, and that is what
    /// <c>install-local.ps1 -Evolved</c> sets, which is how David runs Evolved. Getting that
    /// half wrong would turn the import off on the only machine that runs it.
    /// </summary>
    /// <remarks>Asked of the pure <see cref="AppPaths.IsProductOwned"/> rather than by
    /// setting EQBUDDY_APPDATA: this assembly runs its collections in parallel, and a test
    /// that moved the profile would move it for every test running beside it (trap 57).
    /// </remarks>
    [Fact]
    public void OnlyAProfileThatIsTheProductsOwnCountsAsProductOwned()
    {
        // The suite's own temp redirect — an isolated profile, and nothing about the v1
        // transition may happen inside one.
        Assert.False(AppPaths.IsProductOwnedProfile);
        Assert.False(AppPaths.IsProductOwned(Path.GetTempPath()));

        Assert.True(AppPaths.IsProductOwned(AppPaths.ProductDir));
        // What the two local scripts actually set: the same directory, built from %APPDATA%
        // rather than from SpecialFolder and sometimes with a trailing separator. Compared
        // as paths, so neither spelling is a second profile.
        Assert.True(AppPaths.IsProductOwned(AppPaths.ProductDir + Path.DirectorySeparatorChar));
        Assert.True(AppPaths.IsProductOwned(AppPaths.ProductDir.ToUpperInvariant()));

        // The v1 profile is NOT this product's own, which is what stops an Evolved build
        // pointed at it from running on a v1 player's data. It IS a live player
        // profile, which is what IsolatedLaunchPolicy refuses.
        Assert.False(AppPaths.IsProductOwned(AppPaths.LegacyDir));
        Assert.True(AppPaths.IsLivePlayerDirectory(AppPaths.LegacyDir));
        Assert.True(AppPaths.IsLivePlayerDirectory(AppPaths.ProductDir));
        Assert.False(AppPaths.IsLivePlayerDirectory(Path.GetTempPath()));

        // No override at all is the ordinary player launch.
        Assert.True(AppPaths.IsProductOwned(null));
        Assert.True(AppPaths.IsProductOwned(""));
    }
}
