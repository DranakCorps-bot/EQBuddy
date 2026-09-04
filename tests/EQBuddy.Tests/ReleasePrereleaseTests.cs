using System.Text.RegularExpressions;

namespace EQBuddy.Tests;

/// <summary>
/// `release.ps1 -Prerelease` keeps a v2 milestone away from every v1 client, and it does so
/// through TWO facts that live in different files and can drift apart silently.
///
/// 1. The release must be published with `--prerelease` (`scripts/release.ps1`).
/// 2. The in-app updater must go on reading GitHub's **latest-release** endpoint
///    (`UpdateChecker`), because that endpoint is what excludes prereleases. Point the
///    checker at `/releases` instead — which is the natural edit if someone ever wants the
///    updater to see more than one release — and the flag protects nobody, with nothing
///    failing and nothing to see in either diff.
///
/// The population this protects is the one already installed on players' machines, where no
/// later fix of ours can reach them; that is why the guard is on the mechanism rather than
/// on an outcome we could correct later. Charter RELEASE-002 · #275 P0-1 (LEGACY-001).
///
/// These are TEXT assertions over a PowerShell script, in the shape of
/// <see cref="WeeklyRefreshWiringTests"/> — there is no PowerShell to run here, and the
/// failure being guarded is two files disagreeing rather than either one being wrong alone.
/// </summary>
public class ReleasePrereleaseTests
{
    private static string Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relative)
    {
        var path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"{relative} has moved — this guard scans it, so a wrong "
            + "path here is a guard that silently passes (trap 34).");
        return File.ReadAllText(path);
    }

    /// <summary>The switch exists and is a switch — not `[string]$Prerelease`, which would be
    /// truthy for `-Prerelease $false` and is the classic way a boolean flag inverts.</summary>
    [Fact]
    public void ReleaseScriptDeclaresAPrereleaseSwitch()
    {
        var release = Read("scripts/release.ps1");
        Assert.Matches(new Regex(@"^param\(.*\[switch\]\$Prerelease.*\)", RegexOptions.Multiline), release);
    }

    /// <summary>`--prerelease` reaches `gh release create`, and reaches it ONLY when the
    /// switch is set. Both halves: an unconditional flag would mark every ordinary v1
    /// release as a prerelease and hide it from the players it is for — the same defect with
    /// its sign flipped, and one nobody would notice until an update stopped being
    /// offered.</summary>
    [Fact]
    public void PrereleaseFlagIsWiredToGhReleaseCreateAndIsConditional()
    {
        var release = Read("scripts/release.ps1");

        var flag = Regex.Matches(release, @"'--prerelease'|""--prerelease""|(?<!-)--prerelease");
        Assert.True(flag.Count == 1,
            $"expected exactly one --prerelease in release.ps1, found {flag.Count} (outside comments this is "
            + "the whole mechanism; two of them means one is unreachable or one is unconditional)");

        Assert.Contains("if ($Prerelease) { $ghArgs += '--prerelease' }", release);

        // The flag has to be on the arguments the release is actually created with. Building
        // $ghArgs and then calling `gh release create` with a hand-written list would compile,
        // run, publish, and drop the flag on the floor.
        Assert.Matches(new Regex(@"gh\s+release\s+create\s+@ghArgs"), release);
        var argsIndex = release.IndexOf("$ghArgs += '--prerelease'", StringComparison.Ordinal);
        var createIndex = release.IndexOf("gh release create @ghArgs", StringComparison.Ordinal);
        Assert.True(argsIndex >= 0 && createIndex > argsIndex,
            "--prerelease must be added to $ghArgs BEFORE `gh release create @ghArgs` runs");
    }

    /// <summary>A switch that does nothing is a silent no-op, and this one would do nothing
    /// on a run with no `-Tag`: no `gh release create` happens, while the build, the signing,
    /// the OneDrive copy and the local install all still do.</summary>
    [Fact]
    public void PrereleaseWithoutATagIsRefused()
    {
        var release = Read("scripts/release.ps1");
        Assert.Matches(new Regex(@"if \(\$Prerelease -and -not \$Tag\) \{ throw "), release);
    }

    /// <summary>
    /// The other half of the mechanism, and the one that lives nowhere near the script: the
    /// updater reads the **latest-release** endpoint, which is what excludes prereleases.
    ///
    /// This asserts the URL rather than any behaviour, deliberately — the behaviour belongs
    /// to GitHub and cannot be exercised from a unit test. What can be pinned here is that
    /// the client still asks the question whose answer skips prereleases.
    /// </summary>
    [Fact]
    public void UpdateCheckerStillReadsTheLatestReleaseEndpoint()
    {
        var checker = Read("src/EQBuddy.Core/UpdateChecker.cs");
        Assert.Contains("https://api.github.com/repos/DranakCorps-bot/EQBuddy/releases/latest", checker);
    }

    /// <summary>
    /// Second belt, and it is worth pinning because it is easy to delete as dead code:
    /// `ParseRelease` returns null for a tag that is not a plain version, so a tag shaped
    /// `v2.0.0-beta1` offers a v1 client nothing even if it were marked latest.
    ///
    /// Belt, not replacement — `v2.0.0` parses fine — which is exactly why the `--prerelease`
    /// flag above is the primary protection and this is the backstop.
    /// </summary>
    [Fact]
    public void ParseReleaseRejectsANonVersionTag()
    {
        const string json = """
            {"tag_name":"v2.0.0-beta1","assets":[]}
            """;
        Assert.Null(EQBuddy.Core.UpdateChecker.ParseRelease(json));

        // The negative that keeps the assertion above from going vacuous: a plain version tag
        // still parses, so the null is about the tag SHAPE and not about the empty asset list.
        Assert.NotNull(EQBuddy.Core.UpdateChecker.ParseRelease("""
            {"tag_name":"v2.0.0","assets":[]}
            """));
    }
}
