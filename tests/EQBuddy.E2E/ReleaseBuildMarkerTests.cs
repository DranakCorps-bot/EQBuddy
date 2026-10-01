namespace EQBuddy.E2E;

/// <summary>
/// **A Release-configuration build shows no dev marker** (DRA-705 §6, DRA-707 D3). The
/// launched app is the build CI and <c>release.ps1</c> make — <c>-c Release</c> with no
/// <c>EqDevBuild</c> — so this is the committed negative for "a player never sees the dev
/// stamp": the assembly carries none (<c>devBuild=</c>, which is what Options' footer line
/// shows) and the version line a Feedback post appends does not name one (<c>feedbackDev=</c>).
///
/// Prove-failed by building the app with <c>-p:EqDevBuild=true</c> (what
/// <c>install-local.ps1</c> passes): both facts flip and this row reddens.
/// </summary>
public class ReleaseBuildMarkerTests
{
    [Fact]
    public void AReleaseConfigurationBuildShowsNoDevMarker()
    {
        using var app = new AppHarness();
        app.Launch();

        app.WaitForDump("devBuild", "none", "a Release build to carry no dev stamp");
        Assert.Equal(0, app.DumpValue("feedbackDev"));
    }
}
