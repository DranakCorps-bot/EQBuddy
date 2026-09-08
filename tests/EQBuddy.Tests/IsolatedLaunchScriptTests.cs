using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The launch scripts and the E2E harness share one refuse rule with
/// <see cref="IsolatedLaunchPolicy"/>, and neither side has a compiler that
/// can see the other (trap 53). This file is the rendezvous: it reads the
/// scripts and the E2E source the way <c>ScreenLockTests</c> reads
/// <c>shoot.ps1</c>.
/// </summary>
public class IsolatedLaunchScriptTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Repo, relative.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public void TheSharedScriptNamesBothLiveProfileDirectories()
    {
        var script = Read("scripts/isolated-profile.ps1");
        Assert.Contains("function Test-EqLivePlayerProfile", script);
        Assert.Contains("function Assert-EqIsolatedProfile", script);
        Assert.Contains("function Clear-EqHarnessProfileOverrides", script);
        // Same two names AppPaths owns. A tidy rename here without the constant
        // is a second profile the refuse cannot see.
        Assert.Contains($"'{AppPaths.LegacyDirName}'", script);
        Assert.Contains($"'{AppPaths.EvolvedDirName}'", script);
        foreach (var name in new[]
                 {
                     IsolatedLaunchPolicy.ProfileVar,
                     IsolatedLaunchPolicy.V1SourceVar,
                     IsolatedLaunchPolicy.AllowLiveVar,
                 })
            Assert.Contains($"'{name}'", script);
    }

    public static TheoryData<string> IsolatedLaunchScripts() =>
    [
        "scripts/shoot.ps1",
        "scripts/drag-verify.ps1",
        "scripts/drag-check.ps1",
        "scripts/mode-swap-verify.ps1",
    ];

    [Theory]
    [MemberData(nameof(IsolatedLaunchScripts))]
    public void EveryIsolatedLaunchScriptDotsourcesAndAsserts(string relative)
    {
        var script = Read(relative);
        Assert.Matches(@"isolated-profile\.ps1['""]", script);
        Assert.Contains("Assert-EqIsolatedProfile", script);
    }

    [Fact]
    public void ShootRelaunchClearsHarnessOverridesRatherThanInheritingThem()
    {
        var script = Read("scripts/shoot.ps1");
        Assert.Contains("Clear-EqHarnessProfileOverrides", script);
        // The old Start-Process $path inherited this process's environment,
        // including any Evolved export the seat already carried.
        Assert.DoesNotContain("Start-Process $path", script);
    }

    [Fact]
    public void AppHarnessPinsTheChildProfileAfterTheCallerDictionary()
    {
        var source = Read("tests/EQBuddy.E2E/AppHarness.cs");
        Assert.Contains("IsolatedLaunchPolicy.PinChildProfile", source);
        // The assignment that used to sit BEFORE the caller foreach — that
        // order is the hole. PinChildProfile must be the last write of the
        // profile key.
        var pin = source.LastIndexOf("IsolatedLaunchPolicy.PinChildProfile(psi.Environment", StringComparison.Ordinal);
        var foreachEnv = source.IndexOf("foreach (var (name, value) in _environment)", StringComparison.Ordinal);
        Assert.True(foreachEnv >= 0 && pin > foreachEnv,
            "PinChildProfile must run AFTER the caller dictionary is applied, " +
            "or a scenario can still overwrite EQBUDDY_APPDATA with a live path.");
    }

    /// <summary>
    /// The E2E HOST references Core and has no <c>TestProfileIsolation</c>.
    /// A future test that called <c>AppSettings.Save()</c> would write the
    /// live Evolved profile. The harness already serializes settings by hand
    /// for that reason; this scan keeps a fifth site from forgetting.
    /// </summary>
    [Fact]
    public void TheE2EHostNeverWritesAProfileThroughCore()
    {
        var files = Directory.EnumerateFiles(
                Path.Combine(Repo, "tests", "EQBuddy.E2E"), "*.cs")
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.NotEmpty(files);

        var hits = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            if (text.Contains("AppSettings.Save(", StringComparison.Ordinal) ||
                text.Contains("ProfileJson.Write(", StringComparison.Ordinal))
                hits.Add(Path.GetFileName(file));
        }

        Assert.Empty(hits);
        // Vacuity guard (trap 39): the comment that named the danger must
        // still be there, or this scan is reading a tree that forgot why.
        Assert.Contains("AppSettings.Save targets the CURRENT process's profile",
            File.ReadAllText(Path.Combine(Repo, "tests", "EQBuddy.E2E", "AppHarness.cs")));
    }

    [Fact]
    public void ProductLaunchersStillPointAtTheEvolvedProfileOnPurpose()
    {
        var install = Read("scripts/install-local.ps1");
        var cmd = Read("scripts/Launch-Evolved-Shell.cmd");
        Assert.Contains("EQBuddy Evolved", install);
        Assert.Contains("EQBuddy Evolved", cmd);
        Assert.DoesNotContain("Assert-EqIsolatedProfile", install);
        Assert.DoesNotContain("Assert-EqIsolatedProfile", cmd);
    }
}
