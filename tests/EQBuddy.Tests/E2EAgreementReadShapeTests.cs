using System.Text.RegularExpressions;

namespace EQBuddy.Tests;

/// <summary>
/// **A comparison between two dump values takes both off ONE read** (DRA-248, trap 56) —
/// the guard for the E2E sweep, so the sweep is not the whole of the fix (trap 78).
///
/// <para>`app.DumpValue(a)` and `app.DumpValue(b)` are two READS of `debug.txt`, at two
/// instants. Since DRA-228 each is a complete dump, but not the SAME dump, and a two-host
/// agreement only means anything at one moment — which is what `WidgetDump.PaintOneMoment`
/// makes true on the app side and what `Expected: 4 / Actual: 13` was, on DRA-225's ledger,
/// when the harness sampled two hosts while the fixture replay was still landing. DRA-225
/// moved the three tests it had ledger rows for onto `AppHarness.WaitForDumpValues`; DRA-248
/// moved the other 43 statements onto `WaitForDumpMoment`, which is the same read looked up
/// by key.</para>
///
/// <para><b>Two halves, because either alone is blind to the other's failure</b> (trap 34).
/// The FORBID-scan refuses any statement in `tests/EQBuddy.E2E` holding two `DumpValue` /
/// `DumpText` reads — the exact shape every one of the 43 had, and the shape the next one
/// will have if it is written from muscle memory. It cannot see a test that splits the pair
/// across two statements (`var a = app.DumpValue(x);` … `Assert.Equal(a, app.DumpValue(y))`),
/// so the MUST-LIST names every two-host agreement test and requires its body to take a
/// one-read tool. A new agreement test belongs on that list; that is the edit this guard asks
/// for, and it is a sentence rather than a detector because "is this comparison between two
/// hosts" is not something a regex can answer.</para>
///
/// <para><b>What it deliberately does not police:</b> the ~29 captured-local reads elsewhere
/// in the suite (`var body = …; var list = …;`) that feed a relationship inside ONE host.
/// They are the same two-moment shape and were out of this card's scope; a rule over them
/// would have to tell a relationship from two independent single-key checks, which is the
/// judgement the must-list exists to keep human.</para>
/// </summary>
public class E2EAgreementReadShapeTests
{
    /// <summary>The two-host agreement tests: each compares one host's number with the
    /// other's, so each must take its pair from one read. File → method.</summary>
    private static readonly (string File, string Method)[] AgreementTests =
    [
        ("ShellHostTests.cs", "TheShellAndTheProgressWindowAgreeAboutTheSameRoom"),
        ("ShellHostTests.cs", "TheShellAndTheWorldWindowAgreeAboutTheSameRoom"),
        ("ShellHostTests.cs", "TheShellAndTheCreatureWindowAgreeAboutTheDropsTheyBothShow"),
        ("ShellHostTests.cs", "TheShellAndTheGearLootWindowAgreeAboutTheSameRoom"),
        ("ShellHostTests.cs", "TheShellAndTheQuestsWindowAgreeAboutTheSameRoom"),
        ("ShellHostTests.cs", "TheSkyTabsRulesSurvivedTheLiftIntoTheSecondHost"),
        ("ShellHostTests.cs", "TheLiveRoomAndTheKillsWindowAgreeAboutTheSessionsKills"),
        ("ShellHostTests.cs", "TheLiveRoomAndTheProgressWindowAgreeAboutTheRaidLedger"),
        ("ShellHostTests.cs", "HomeAndLiveDescribeTheSameSittingAndOnlyLiveCountsIt"),
        ("ShellHostTests.cs", "TheShellAndTheOptionsWindowAgreeAboutTheSameSettings"),
        ("EpicGuideRowsTests.cs", "TheShellAndTheQuestsWindowDrawTheSameEpicGuide"),
        ("GuideRowsTests.cs", "TheShellAndTheQuestsWindowDrawTheSameGuide"),
    ];

    [Fact]
    public void NoE2EStatementComparesTwoSeparateReadsOfTheDump()
    {
        var offenders = E2ESources()
            .Where(f => Path.GetFileName(f) != "AppHarness.cs")
            .SelectMany(f => TwoReadStatements(File.ReadAllText(f))
                .Select(s => $"{Path.GetFileName(f)} {s}"))
            .ToList();

        Assert.True(offenders.Count == 0,
            "these statements take two readings of debug.txt — two moments — and compare them; "
            + "take the pair from ONE read with `app.WaitForDumpMoment(reason, a, b)` "
            + "(DRA-248, trap 56):\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void EveryTwoHostAgreementTestTakesItsPairFromOneRead()
    {
        var missing = AgreementTests
            .Where(t =>
            {
                var body = MethodBody(
                    File.ReadAllText(Path.Combine(E2EDir(), t.File)), t.Method);
                Assert.True(body is not null,
                    $"{t.File} no longer has {t.Method} — a renamed agreement test must be "
                    + "renamed here too, or this list goes quietly blind to it");
                return !OneReadTool.IsMatch(body!);
            })
            .Select(t => $"{t.File}: {t.Method}")
            .ToList();

        Assert.True(missing.Count == 0,
            "these two-host agreement tests no longer take their pair from one read "
            + "(`WaitForDumpMoment` / `WaitForDumpValues`):\n  " + string.Join("\n  ", missing));
    }

    /// <summary>
    /// **The detector FIRES** (trap 78: a detector aimed at nothing is green). Every shape
    /// the sweep removed is fed to it verbatim and must be caught; the shapes that replaced
    /// them, and the single reads the card said not to sweep, must not be.
    /// </summary>
    [Fact]
    public void TheDetectorCatchesEveryShapeTheSweepRemovedAndNothingItKept()
    {
        string[] caught =
        [
            // The one-line pair (26 of them in ShellHostTests alone).
            """Assert.Equal(app.DumpValue("kills"), app.DumpValue("shellLiveKillRows"));""",
            // Split across two lines — the shape a single-line grep undercounted.
            "Assert.Equal(app.DumpValue(\"questsReadySummary\"),\n"
                + "    app.DumpValue(\"shellQuestsReadySummary\"));",
            // The loop over a key list.
            "foreach (var key in keys)\n"
                + "    Assert.Equal(app.DumpValue(\"quests\" + key), app.DumpValue(\"shellQuests\" + key));",
            // A tuple that CLAIMED one read in its comment and took two.
            """var (door, open) = (app.DumpValue("a"), app.DumpValue("b"));""",
            // An inequality, a difference of differences, and a word.
            """Assert.True(app.DumpValue("a") > app.DumpValue("b"), "msg; with a semicolon");""",
            """Assert.Equal(app.DumpValue("a") - app.DumpValue("b"), 0);""",
            """Assert.Equal(app.DumpText("a"), app.DumpText("b"));""",
        ];
        foreach (var source in caught)
            Assert.True(TwoReadStatements(source).Count == 1, $"not caught: {source}");

        string[] kept =
        [
            """Assert.Equal(m["kills"], m["shellLiveKillRows"]);""",
            """var m = app.WaitForDumpMoment("r", "kills", "shellLiveKillRows");""",
            """Assert.True(app.DumpValue("tick") > 0, $"dump was: {app.Artifacts()}");""",
            // A diagnostic read inside the MESSAGE is not a second operand.
            """Assert.True(app.DumpValue("a") > 0, $"saw {app.DumpValue("a")}");""",
            // Commented-out history is not code.
            """// Assert.Equal(app.DumpValue("a"), app.DumpValue("b"));""",
        ];
        foreach (var source in kept)
            Assert.True(TwoReadStatements(source).Count == 0, $"falsely caught: {source}");
    }

    private static readonly Regex OneReadTool =
        new(@"\bWaitForDump(?:Moment|Values)\(", RegexOptions.Compiled);

    private static readonly Regex DumpRead =
        new(@"\bDump(?:Value|Text)\(", RegexOptions.Compiled);

    /// <summary>Statements of <paramref name="source"/> holding two or more dump reads, as
    /// "line N: the statement's first source line". String literals are blanked first (a
    /// message's `;` is not a statement end, and a read interpolated into a message is a
    /// diagnostic rather than an operand), then comments; neither blanking crosses a newline,
    /// so line numbers survive it.</summary>
    internal static IReadOnlyList<string> TwoReadStatements(string source)
    {
        var code = Regex.Replace(source, @"""(?:[^""\\\n]|\\.)*""", "\"\"");
        code = Regex.Replace(code, @"//[^\n]*", "");
        var lines = source.Split('\n');
        return Regex.Matches(code, @"[^;{}]*;")
            .Where(m => DumpRead.Matches(m.Value).Count >= 2)
            .Select(m =>
            {
                var at = m.Index + (m.Value.Length - m.Value.TrimStart().Length);
                var line = code.AsSpan(0, at).Count('\n');
                return $"line {line + 1}: {lines[line].Trim()}";
            })
            .ToList();
    }

    /// <summary>The text from a method's signature to the next test attribute (or the end of
    /// the file) — coarse, and enough: the question is only whether a call appears in it.</summary>
    private static string? MethodBody(string source, string method)
    {
        var start = Regex.Match(source, $@"\bvoid\s+{Regex.Escape(method)}\s*\(");
        if (!start.Success) return null;
        var rest = source[start.Index..];
        var next = Regex.Match(rest[1..], @"\[(?:Fact|Theory)\b");
        return next.Success ? rest[..(next.Index + 1)] : rest;
    }

    private static IEnumerable<string> E2ESources() =>
        Directory.EnumerateFiles(E2EDir(), "*.cs", SearchOption.TopDirectoryOnly);

    private static string E2EDir() => Path.Combine(RepoRoot(), "tests", "EQBuddy.E2E");

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EQBuddy.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
