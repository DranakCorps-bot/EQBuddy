using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The SIZE claims in the docs, checked against the repo.
///
/// `DocumentationTests` already pins the structural claims — files pointed at exist, tests
/// named as evidence exist, the ratchet table matches the ratchet. It deliberately leaves
/// prose to review. But a handful of prose claims are not really prose: they are measurements,
/// and a measurement rots on its own without anybody touching it.
///
/// **This is not hypothetical, and the doc says so itself.** `docs/Architecture.md` carries
/// the note *"Sizes re-measured 2026-08-20 … the previous set had drifted far enough to
/// mislead — UI.Shared had doubled and the Avalonia build tripled since they were written."*
/// It had drifted again within four days: every row of that table was 10-15% low on
/// 2026-08-24, and `docs/TestPlan.md` §5 was describing the WPF app as 14,432 lines across 37
/// files when it was 20,838 across 64. A reader deciding whether a project is small enough to
/// read in one sitting is being told something false.
///
/// **Tolerance rather than exactness, on purpose.** Asserting to the line would make every
/// commit a documentation edit, and a check people route around is worse than no check — the
/// same reasoning behind the hotspot ratchet's 10% growth allowance. These fail only once a
/// number is misleading, which is the thing that actually costs a reader.
/// </summary>
public class DocumentationSizeTests
{
    /// <summary>Far enough out to mislead. Under this, the number still tells the truth
    /// about the order of magnitude and how long the project takes to read.</summary>
    private const double Tolerance = 0.10;

    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Repo, relative));

    /// <summary>Exactly what Architecture.md says it counted: `.cs` under the project,
    /// excluding `obj/` and `bin/`.</summary>
    private static (int Files, int Lines) Measure(string project)
    {
        var root = Path.Combine(Repo, "src", project);
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        return (files.Count, files.Sum(f => File.ReadLines(f).Count()));
    }

    private static void AssertClose(string what, int claimed, int actual)
    {
        var drift = Math.Abs(actual - claimed) / (double)Math.Max(1, actual);
        Assert.True(drift <= Tolerance,
            $"{what}: the doc says {claimed:N0}, the repo has {actual:N0} " +
            $"({drift:P0} out). Update the number — a measurement nobody re-measures " +
            $"is a confidently wrong map.");
    }

    /// <summary>
    /// The project table in §1 of Architecture.md. Parsed from the doc rather than
    /// duplicated here, so the doc stays the single source and this only checks it.
    /// </summary>
    [Fact]
    public void TheProjectSizeTableInTheArchitectureDocIsAccurate()
    {
        var text = Read(Path.Combine("docs", "Architecture.md"));
        var rows = Regex.Matches(text, @"^\|\s*`(?<proj>EQBuddy(?:\.[A-Za-z.]+)?)`\s*\|\s*(?<files>[\d,]+)\s*\|\s*(?<lines>[\d,]+)\s*\|",
            RegexOptions.Multiline);

        Assert.True(rows.Count >= 5,
            $"expected the five-project size table in Architecture.md §1; found {rows.Count} rows");

        foreach (Match row in rows)
        {
            var project = row.Groups["proj"].Value;
            var claimedFiles = int.Parse(row.Groups["files"].Value.Replace(",", ""));
            var claimedLines = int.Parse(row.Groups["lines"].Value.Replace(",", ""));
            var (files, lines) = Measure(project);

            AssertClose($"Architecture.md §1 — {project} file count", claimedFiles, files);
            AssertClose($"Architecture.md §1 — {project} line count", claimedLines, lines);
        }
    }

    /// <summary>
    /// TestPlan §5 opens by sizing the untested WPF layer, and that number is the whole
    /// argument for the section — "this much code has no automated coverage". It is the
    /// one number in the docs where being wrong understates a known risk.
    /// </summary>
    [Fact]
    public void TheUntestedWpfLayerIsSizedCorrectlyInTheTestPlan()
    {
        var text = Read(Path.Combine("docs", "TestPlan.md"));
        var m = Regex.Match(text, @"the WPF app,\s*(?<lines>[\d,]+)\s*lines across\s*(?<files>\d+)\s*files");
        Assert.True(m.Success, "TestPlan §5 no longer states the size of the WPF layer");

        var (files, lines) = Measure("EQBuddy");
        AssertClose("TestPlan §5 — WPF line count",
            int.Parse(m.Groups["lines"].Value.Replace(",", "")), lines);
        AssertClose("TestPlan §5 — WPF file count", int.Parse(m.Groups["files"].Value), files);
    }

    /// <summary>
    /// CLAUDE.md prints `BreakoutKind`'s members inline, and on 2026-08-24 it was missing
    /// `Progress` — added to the enum on 2026-08-19. **Trap 30 in that same file is about
    /// exactly this enum growing and a hand-written list not following**, so the doc went
    /// stale on the one member it warns you about. `scripts/shoot.ps1` had been updated;
    /// only the prose had not.
    ///
    /// Checks the WPF enum, which is the one the doc's sentence is about. The Avalonia twin
    /// is deliberately a smaller set and the doc now says so.
    /// </summary>
    [Fact]
    public void ClaudeMdListsEveryBreakoutKind()
    {
        var doc = Read("CLAUDE.md");
        var listed = Regex.Match(doc, @"`BreakoutKind`\s*is\s*`\{(?<members>[^}]*)\}`");
        Assert.True(listed.Success, "CLAUDE.md no longer spells out BreakoutKind's members");

        var source = Read(Path.Combine("src", "EQBuddy", "BreakoutWindow.xaml.cs"));
        var real = Regex.Match(source, @"enum\s+BreakoutKind\s*\{(?<members>[^}]*)\}");
        Assert.True(real.Success, "BreakoutKind is no longer declared in EQBuddy/BreakoutWindow.xaml.cs");

        static string[] Split(string s) =>
            [.. s.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0)];

        var documented = Split(listed.Groups["members"].Value);
        var actual = Split(real.Groups["members"].Value);

        Assert.Equal(actual, documented);
    }
}
