using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Evolved reads the release LIST, not GitHub's single "Latest", and picks its own line out
/// of it (Founder, 2026-09-28). The legacy and Evolved releases share one feed, so every
/// test here puts a legacy release beside the Evolved ones: the selection has to be about
/// the MAJOR, not about whichever row happens to come first.
/// </summary>
public sealed class EvolvedReleaseFeedTests
{
    private static string Asset(string name, string url) =>
        $$"""{"name":"{{name}}","browser_download_url":"{{url}}"}""";

    private static string Release(string tag, bool prerelease = false, bool draft = false,
        bool withInstaller = true)
    {
        var assets = withInstaller
            ? Asset(UpdateChecker.EvolvedSetupName, $"https://gh/{tag}/setup") + "," +
              Asset(UpdateChecker.EvolvedSetupName + ".sha256", $"https://gh/{tag}/setup.sha256")
            : "";
        var pre = prerelease ? "true" : "false";
        var dr = draft ? "true" : "false";
        return $$"""
            {"tag_name":"{{tag}}","prerelease":{{pre}},"draft":{{dr}},"html_url":"https://github.com/DranakCorps-bot/EQBuddy/releases/tag/{{tag}}","assets":[{{assets}}]}
            """;
    }

    private static string List(params string[] releases) => "[" + string.Join(",", releases) + "]";

    [Fact]
    public void ThisBuildIsTheEvolvedLineAndAsksForTheEvolvedInstaller()
    {
        // Premise for everything below: main builds 2.x.
        Assert.True(AppPaths.IsEvolvedLine);
        Assert.Equal(UpdateChecker.EvolvedSetupName, UpdateChecker.SetupName);
        Assert.Equal(UpdateChecker.EvolvedPortableName, UpdateChecker.PortableName);
    }

    [Fact]
    public void TheHighestEvolvedReleaseWinsWhateverOrderTheFeedUses()
    {
        var info = UpdateChecker.PickEvolvedRelease(List(
            Release("v2.0.0"), Release("v1.99.18"), Release("v2.1.0"), Release("v2.0.5")))!;

        Assert.Equal(new Version(2, 1, 0), info.Latest);
        Assert.Equal("https://gh/v2.1.0/setup", info.DownloadUrl);
        Assert.EndsWith("/releases/tag/v2.1.0", info.PageUrl);
    }

    [Fact]
    public void LegacyReleasesAreNeverAnEvolvedUpdateEvenWhenNewestInTheFeed()
    {
        // A later 1.x patch (a legacy fix) sits at the top of the feed; it is not an answer.
        var info = UpdateChecker.PickEvolvedRelease(List(Release("v1.99.30"), Release("v2.0.0")))!;
        Assert.Equal(new Version(2, 0, 0), info.Latest);

        Assert.Null(UpdateChecker.PickEvolvedRelease(List(Release("v1.99.30"), Release("v1.99.18"))));
    }

    [Fact]
    public void PrereleasesDraftsAndNonVersionTagsAreSkipped()
    {
        var info = UpdateChecker.PickEvolvedRelease(List(
            Release("v2.3.0", prerelease: true),
            Release("v2.2.0", draft: true),
            Release("v2.9.0-beta"),
            Release("v2.0.0")))!;

        Assert.Equal(new Version(2, 0, 0), info.Latest);
    }

    [Fact]
    public void AnEvolvedReleaseWithoutItsInstallerIsStillAnOfferForThePage()
    {
        var info = UpdateChecker.PickEvolvedRelease(List(Release("v2.0.1", withInstaller: false)))!;
        Assert.Null(info.DownloadUrl);
        Assert.Equal("https://github.com/DranakCorps-bot/EQBuddy/releases/tag/v2.0.1",
            UpdateChecker.PageFor(info));
    }

    [Fact]
    public void TheFallbackPageOnEvolvedIsTheReleasesListNeverLatest()
    {
        Assert.Equal(UpdateChecker.GitHubReleasesPage, UpdateChecker.PageFor(null));
        Assert.DoesNotContain("/latest", UpdateChecker.PageFor(new UpdateInfo(new Version(2, 0, 1), null)));
    }

    [Fact]
    public void TheDisplayNameCarriesTheBetaLabelAndTheComparedVersion()
    {
        Assert.Equal("0.1 Beta", UpdateChecker.ReleaseLabel);
        var name = UpdateChecker.DisplayName;
        Assert.StartsWith("EQBuddy Evolved 0.1 Beta", name);
        Assert.Contains($"v{UpdateChecker.CurrentVersion.Major}.", name);
    }
}
