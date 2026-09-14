using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The ExO dashboard (DRA-78 / M0-5) against the things it claims about.
///
/// <para>The dashboard exists so that every later claim about the new operating model is
/// checkable against a number that was written down FIRST. That property has two ways to
/// rot, and this file closes both:</para>
///
/// <list type="number">
/// <item>The frozen baseline stops being reproducible — the script that made it is gone,
/// renamed, or no longer named by the doc. An illustration of our own numbers is a
/// capture with a recipe, or it does not ship.</item>
/// <item>An experiment gets an <c>exo-experiment:</c> tag in DECISIONS.md and never
/// reaches the dashboard's §10.3 section, so the M0-exit doctrine capture cannot cite it.
/// Plan §10.1 makes the tag the thing the playbook looks up; a tag the dashboard does not
/// carry is an experiment that graduates on nothing. This is trap 34's must-list: the
/// script's tag SCAN can only see tags that exist, and only a paired list catches the one
/// that never arrived.</item>
/// </list>
///
/// <para>Prove-failed by deleting a tag from the dashboard, by nulling a frozen KPI, and
/// by renaming the script the doc cites.</para>
/// </summary>
public class ExoDashboardTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Repo, relative));

    private const string Dashboard = "docs/ops/exo-dashboard.md";
    private const string BaselineJson = "docs/ops/exo-baseline.json";
    private const string Script = "scripts/exo-metrics.ps1";

    /// <summary>The tags plan §10.1 requires on every adopted process change.</summary>
    private static List<string> TaggedExperiments() =>
        Regex.Matches(Read("DECISIONS.md"), @"exo-experiment:\s*([a-z0-9-]+)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void TheDashboardAndItsFrozenBaselineAreBothCommitted()
    {
        Assert.True(File.Exists(Path.Combine(Repo, Dashboard)),
            $"{Dashboard} is missing — the baseline every later ExO claim is measured against.");
        Assert.True(File.Exists(Path.Combine(Repo, BaselineJson)),
            $"{BaselineJson} is missing — the markdown alone cannot fill a 'vs baseline' column.");
        Assert.True(File.Exists(Path.Combine(Repo, Script)),
            $"{Script} is missing — a frozen number nobody can regenerate is an anecdote.");
    }

    [Fact]
    public void TheDashboardNamesTheScriptThatGeneratesIt()
    {
        // The illustration lock's shape, applied to numbers instead of pixels: a reading
        // ships with the recipe that produced it, or a later reader cannot tell a measured
        // figure from a remembered one.
        var doc = Read(Dashboard);
        Assert.Contains(Script, doc, StringComparison.Ordinal);
        Assert.Contains("-SelfTest", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFrozenBaselineCarriesEveryKpiTheMigrationWillBeJudgedOn()
    {
        using var doc = JsonDocument.Parse(Read(BaselineJson));
        var root = doc.RootElement;

        // Plan §6 names these as the KPIs. A baseline missing one of them cannot support
        // the comparison it exists for.
        foreach (var key in new[]
                 {
                     "gwr", "accr", "prsPerSlice", "helmTouchesPerSlice",
                     "vetoRate", "reworkRate", "medianCiMinutes", "leadTimeHours",
                 })
        {
            Assert.True(root.TryGetProperty(key, out var value),
                $"frozen baseline has no '{key}' — the §6 KPI it judges cannot be compared.");
            Assert.NotEqual(JsonValueKind.Null, value.ValueKind);
        }

        // The window is part of the claim. A baseline that does not say WHAT it measured
        // is a number anyone can move by re-running it over a friendlier range.
        Assert.True(root.TryGetProperty("window", out var window));
        Assert.False(string.IsNullOrWhiteSpace(window.GetString()));
        Assert.True(root.TryGetProperty("windowStart", out _));
        Assert.True(root.TryGetProperty("windowEnd", out _));
    }

    [Fact]
    public void TheBaselineWindowIsTheOneTheMigrationWasArguedFrom()
    {
        using var doc = JsonDocument.Parse(Read(BaselineJson));
        var root = doc.RootElement;

        // DRA-73's whole argument is measured from PRs #580–#607. If the frozen file is
        // re-cut over some other window, the plan's claims and the dashboard's numbers
        // stop being about the same thing — silently, because both still look like numbers.
        Assert.Equal("PRs #580-#607", root.GetProperty("window").GetString());

        // And the pre-migration shape has to actually be present, or this is a baseline of
        // the new model wearing the old model's label: every slice governed, none autonomous.
        Assert.Equal(0d, root.GetProperty("accr").GetDouble());
        Assert.True(root.GetProperty("helmTouchesPerSlice").GetDouble() >= 2d,
            "the baseline window is supposed to show ≥2 Helm touches per slice");
        Assert.True(root.GetProperty("governanceShare").GetDouble() > 0d,
            "the baseline window is supposed to contain helm/ssc-N PRs — that is the cost cutover 1 removes");
    }

    [Fact]
    public void EveryTaggedExperimentReachesTheDashboard()
    {
        var tags = TaggedExperiments();

        // Non-vacuity (trap 78): a scan over an empty tag list passes while telling you
        // nothing. DRA-74 and DRA-75 both landed tags, so an empty list means the parse
        // broke, not that the experiments stopped.
        Assert.True(tags.Count >= 3,
            $"expected at least three exo-experiment tags in DECISIONS.md, found {tags.Count} — "
            + "either §10.1 stopped being followed or this scan stopped matching.");

        var doc = Read(Dashboard);
        var missing = tags.Where(t => !doc.Contains(t, StringComparison.Ordinal)).ToList();

        // The remedy is one command, and it is named here because the most likely way to
        // hit this red is order-of-landing rather than neglect: a PR that adds a tag
        // merges after the dashboard was last generated, and the person who sees the red
        // is whoever merged second.
        Assert.True(missing.Count == 0,
            "these exo-experiment tags are in DECISIONS.md but not in the dashboard's "
            + "'Experiments in flight' section, so the M0-exit doctrine capture cannot cite "
            + "them (plan §10.3):\n  " + string.Join("\n  ", missing)
            + "\n\nRegenerate: pwsh -NoProfile -File scripts/exo-metrics.ps1 -FromPr 580 -ToPr 607 -Baseline");
    }

    [Fact]
    public void TheDashboardSaysWhatItCouldNotMeasureRatherThanReportingZero()
    {
        // The failure this guards is the quiet one: a metric with no data rendering as 0,
        // and a later reader taking "0 escaped defects" for evidence rather than for the
        // absence of a measurement.
        var doc = Read(Dashboard);
        Assert.Contains("unmeasured", doc, StringComparison.Ordinal);
        Assert.Contains("Escaped defect rate", doc, StringComparison.Ordinal);
    }
}
