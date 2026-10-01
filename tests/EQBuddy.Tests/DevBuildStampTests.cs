using System.Reflection;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The in-app dev stamp (DRA-705 §6, DRA-707 D3): what <see cref="DevBuildStamp"/> says, and
/// the two scripts that decide whether a build carries one at all. The launched-app half —
/// a Release configuration shows no marker in Options' label — is the E2E row
/// <c>ReleaseBuildMarkerTests</c>.
/// </summary>
public class DevBuildStampTests
{
    private static readonly TimeZoneInfo Central =
        TimeZoneInfo.CreateCustomTimeZone("test-cdt", TimeSpan.FromHours(-5), "CDT", "CDT");

    private static AssemblyMetadataAttribute[] Meta(params (string Key, string? Value)[] pairs) =>
        pairs.Select(p => new AssemblyMetadataAttribute(p.Key, p.Value)).ToArray();

    [Fact]
    public void ADevBuildNamesItsCommitAndItsLocalBuildTime()
    {
        var m = DevBuildStamp.Marker(Meta(
            ("ReleaseLabel", "0.1 Beta"),
            ("EqDevBuild", "true"),
            ("EqDevSha", "52a9a672bbc54d8226f7cc19eb409475ae23b08f"),
            ("EqDevBuiltAt", "2026-10-01T20:56:00Z")), Central);
        Assert.Equal("dev 52a9a67 · Oct 1, 3:56 PM", m);
    }

    [Fact]
    public void ABuildWithoutTheFlagHasNoMarkerEvenIfACommitRidesAlong()
    {
        Assert.Null(DevBuildStamp.Marker(Meta(("ReleaseLabel", "0.1 Beta")), Central));
        Assert.Null(DevBuildStamp.Marker(Meta(("EqDevSha", "abc1234def"), ("EqDevBuiltAt", "2026-10-01T20:56:00Z")), Central));
        Assert.Null(DevBuildStamp.Marker(Meta(("EqDevBuild", "false"), ("EqDevSha", "abc1234def")), Central));
    }

    [Fact]
    public void ADevBuildMissingItsPartsStillSaysDev()
    {
        Assert.Equal("dev", DevBuildStamp.Marker(Meta(("EqDevBuild", "true"), ("EqDevSha", ""), ("EqDevBuiltAt", "")), Central));
        Assert.Equal("dev abc1234", DevBuildStamp.Marker(Meta(("EqDevBuild", "true"), ("EqDevSha", "abc1234"), ("EqDevBuiltAt", "not a time")), Central));
    }

    [Fact]
    public void AppendAddsTheMarkerOnlyWhenThereIsOne()
    {
        Assert.Equal("EQBuddy 2.0.3 · dev abc1234", DevBuildStamp.Append("EQBuddy 2.0.3", "dev abc1234"));
        Assert.Equal("EQBuddy 2.0.3", DevBuildStamp.Append("EQBuddy 2.0.3", null));
    }

    /// <summary>This test assembly is built by `dotnet build`/`dotnet test`, which never pass
    /// EqDevBuild — so it carries no stamp, and neither does anything else a plain build makes.</summary>
    [Fact]
    public void APlainBuildCarriesNoStamp()
    {
        Assert.Null(DevBuildStamp.Current);
        Assert.DoesNotContain(typeof(DevBuildStamp).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
            a => a.Key.StartsWith("EqDev", StringComparison.Ordinal));
    }

    /// <summary>Who sets the flag. The forbid (release.ps1 never names it) is paired with its
    /// must (install-local.ps1, the daily driver and auto-roll's one install path, does), so a
    /// rename on either side reddens rather than silently stamping nothing (trap 34).</summary>
    [Fact]
    public void OnlyTheLocalInstallPathSetsTheDevFlag()
    {
        Assert.DoesNotContain("EqDevBuild", Read("scripts/release.ps1"), StringComparison.Ordinal);
        var install = Read("scripts/install-local.ps1");
        Assert.Contains("-p:EqDevBuild=true", install, StringComparison.Ordinal);
        Assert.Contains("-p:SourceRevisionId=", install, StringComparison.Ordinal);
        Assert.Contains("-p:EqDevBuiltAt=", install, StringComparison.Ordinal);
        var props = Read("Directory.Build.props");
        Assert.Contains("Condition=\"'$(EqDevBuild)' == 'true'\"", props, StringComparison.Ordinal);
        foreach (var key in new[] { DevBuildStamp.FlagKey, DevBuildStamp.ShaKey, DevBuildStamp.BuiltAtKey })
            Assert.Contains($"Include=\"{key}\"", props, StringComparison.Ordinal);
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EQBuddy.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
