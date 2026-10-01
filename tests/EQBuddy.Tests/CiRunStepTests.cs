using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// DRA-716. CI's "Generated catalogs match their generators" step ran six
/// <c>python … --check</c> lines in one <c>run: |</c> block on a Windows runner, where the
/// default shell is pwsh. GitHub's pwsh wrapper prepends <c>$ErrorActionPreference = 'stop'</c>
/// and appends <c>exit $LASTEXITCODE</c> — and a native command's non-zero exit is not an
/// error to pwsh (<c>$PSNativeCommandUseErrorActionPreference</c> is off), so only the LAST
/// line's exit code decided the step. Measured with the wrapper reproduced: a middle line
/// exiting 1 and a last line exiting 0 is a step that exits 0. Every generator check but
/// the last was a gate that could not go red.
///
/// <para>The rule is structural so it cannot be argued line by line: <b>a pwsh step runs ONE
/// command</b>. More than that goes in a script under <c>scripts/</c>, which owns its own
/// exit code. Bash steps are out of scope — the runner starts bash with <c>-e</c>, so a
/// failing middle command already stops the step.</para>
///
/// <para>The scanner is a line reader, not a YAML parser, so it is held to what it SEES as
/// well as to what it refuses (traps 34 and 78): a must-list of steps it has to find and
/// classify, and a committed negative — the pre-fix block verbatim — that it has to refuse.
/// A workflow-level or job-level <c>defaults:</c> block would change the shell under it, so
/// one makes the suite fail until the scanner is taught to read it.</para>
/// </summary>
public sealed class CiRunStepTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string WorkflowDir => Path.Combine(Repo, ".github", "workflows");

    public sealed record RunStep(string File, string Job, string Step, bool Pwsh, int Commands);

    private static readonly Regex JobKey = new(@"^  ([A-Za-z0-9_-]+):\s*$");
    private static readonly Regex StepStart = new(@"^(\s*)- (.*)$");
    private static readonly Regex Key = new(@"^\s*(?:- )?([A-Za-z0-9_-]+):\s*(.*)$");

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    /// <summary>Every step with a <c>run:</c>, with its shell and its command-line count.</summary>
    public static List<RunStep> Scan(string file, string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var found = new List<RunStep>();
        bool inJobs = false;
        string job = "", runsOn = "";
        int stepIndent = -1;

        string? stepName = null, stepShell = null;
        int? stepCommands = null;

        void Flush()
        {
            if (stepCommands is int n)
            {
                bool pwsh = stepShell is null
                    ? runsOn.Contains("windows", StringComparison.OrdinalIgnoreCase)
                    : stepShell is "pwsh" or "powershell";
                found.Add(new RunStep(file, job, stepName ?? "(unnamed)", pwsh, n));
            }
            stepName = null; stepShell = null; stepCommands = null;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#')) continue;

            if (line == "jobs:") { inJobs = true; continue; }
            if (!inJobs) continue;

            var jk = JobKey.Match(line);
            if (jk.Success)
            {
                Flush();
                job = jk.Groups[1].Value; runsOn = ""; stepIndent = -1;
                continue;
            }

            var ss = StepStart.Match(line);
            if (ss.Success && (stepIndent < 0 || Indent(line) == stepIndent))
            {
                Flush();
                stepIndent = Indent(line);
            }

            var k = Key.Match(line);
            if (!k.Success) continue;
            var (key, value) = (k.Groups[1].Value, k.Groups[2].Value.Trim());

            if (key == "runs-on" && Indent(line) == 4) runsOn = value;
            else if (stepIndent >= 0 && key == "name" && stepName is null) stepName = value;
            else if (stepIndent >= 0 && key == "shell") stepShell = value;
            else if (stepIndent >= 0 && key == "run")
            {
                if (value.StartsWith('|'))
                {
                    int keyIndent = Indent(line) + (line.TrimStart().StartsWith("- ") ? 2 : 0);
                    int commands = 0;
                    while (i + 1 < lines.Length
                           && (lines[i + 1].Trim().Length == 0 || Indent(lines[i + 1]) > keyIndent))
                    {
                        var body = lines[++i].Trim();
                        if (body.Length > 0 && !body.StartsWith('#')) commands++;
                    }
                    stepCommands = commands;
                }
                else
                {
                    // A plain or folded (`>`) scalar is one command line.
                    stepCommands = 1;
                }
            }
        }
        Flush();
        return found;
    }

    private static List<RunStep> ScanAll() =>
        Directory.GetFiles(WorkflowDir, "*.yml")
            .SelectMany(f => Scan(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();

    [Fact]
    public void NoPwshRunStepInAnyWorkflowRunsMoreThanOneCommand()
    {
        var offenders = ScanAll().Where(s => s.Pwsh && s.Commands > 1).ToList();
        Assert.True(offenders.Count == 0,
            "A pwsh `run:` step runs ONE command — its wrapper exits with the LAST line's " +
            "exit code, so a failing earlier line is green (DRA-716). Split into one step per " +
            "command, or move the block into a script:\n" +
            string.Join("\n", offenders.Select(o => $"  {o.File} / {o.Job} / {o.Step}: {o.Commands} commands")));
    }

    [Fact]
    public void NoWorkflowCarriesADefaultsBlockTheScannerCannotRead()
    {
        foreach (var f in Directory.GetFiles(WorkflowDir, "*.yml"))
            Assert.DoesNotMatch(new Regex(@"(?m)^\s*defaults:"), File.ReadAllText(f));
    }

    /// <summary>Trap 78: a scanner that finds nothing reports clean. These are steps it MUST
    /// see, each with the shell it must assign.</summary>
    [Theory]
    [InlineData("ci.yml", "build-and-test", "Build solution (WPF + Core + tests)", true)]
    [InlineData("ci.yml", "e2e-windows", "Build solution (E2E launches the built exe)", true)]
    [InlineData("ci.yml", "build-and-test", "Zone era refusals fire (selftest)", true)]
    [InlineData("pages.yml", "deploy", "Live figures into the artifact (never committed; a failure publishes \"unavailable\")", true)]
    [InlineData("knowledge-refresh.yml", "refresh", "Open or update the knowledge PR", false)]
    public void TheScannerSeesAndClassifiesTheStepsItMust(string file, string job, string step, bool pwsh)
    {
        var all = ScanAll();
        var hit = all.SingleOrDefault(s => s.File == file && s.Job == job && s.Step == step);
        Assert.True(hit is not null,
            $"scanner did not find {file} / {job} / {step}. It found:\n" +
            string.Join("\n", all.Select(s => $"  {s.File} / {s.Job} / {s.Step}")));
        Assert.Equal(pwsh, hit!.Pwsh);
    }

    [Fact]
    public void TheScannerSeesTheBashMultiLineBlockAsMultiLine()
    {
        // The reachable proof that block counting works on the real files: knowledge-refresh's
        // PR step is a long bash block, which is allowed, and must be counted as one.
        var pr = ScanAll().Single(s => s.Step == "Open or update the knowledge PR");
        Assert.True(pr.Commands > 5, $"counted {pr.Commands} lines");
    }

    /// <summary>The committed negative: the pre-fix step, verbatim in shape.</summary>
    [Fact]
    public void ThePreFixGeneratedCatalogsStepIsRefused()
    {
        const string preFix = """
            jobs:
              build-and-test:
                runs-on: windows-latest
                steps:
                  - uses: actions/checkout@v7

                  - name: Generated catalogs match their generators
                    run: |
                      python scripts/harvests/eqlwiki/guides-transform.py --check
                      python scripts/harvests/eqlwiki/epic-guides-build.py --check
                      # a comment is not a command
                      python scripts/harvests/eqlwiki/zone-eras-transform.py --selftest

                  - name: One command
                    run: python x.py --check
            """;
        var steps = Scan("fixture.yml", preFix);
        var bad = Assert.Single(steps, s => s.Commands > 1);
        Assert.Equal("Generated catalogs match their generators", bad.Step);
        Assert.True(bad.Pwsh);
        Assert.Equal(3, bad.Commands);

        // ...and the same block on an ubuntu job is bash, which `-e` already stops.
        var onLinux = Scan("fixture.yml", preFix.Replace("windows-latest", "ubuntu-latest"));
        Assert.DoesNotContain(onLinux, s => s.Pwsh);

        // ...and an explicit `shell: pwsh` on ubuntu is pwsh again.
        var explicitPwsh = Scan("fixture.yml", preFix
            .Replace("windows-latest", "ubuntu-latest")
            .Replace("    run: |", "    shell: pwsh\n        run: |"));
        Assert.Contains(explicitPwsh, s => s.Pwsh && s.Commands == 3);
    }
}
