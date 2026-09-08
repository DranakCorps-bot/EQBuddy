using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Guards for the 2026-09-08 EQBuddy lab experiments (C′ + Context).
///
/// These are not product behaviour. They keep the live manual a manual, the
/// flake ledger a ledger, and the Soft/local ladder from quietly becoming
/// "CI is optional". Lab, not a Corps-wide standard — the files say so.
/// </summary>
public class LabDocsTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Repo, relative));

    /// <summary>
    /// Context lab: CLAUDE.md is the live manual. The pre-split file was
    /// ~168 KiB of novel; that size is the committed negative. 96 KiB is
    /// "misleading as a session load" — room for new compact traps, not for
    /// pasting the archive back. 20 KiB is "the rules are still here".
    /// </summary>
    [Fact]
    public void ClaudeMdStaysALiveManual()
    {
        var live = new FileInfo(Path.Combine(Repo, "CLAUDE.md")).Length;
        Assert.True(live >= 20 * 1024,
            $"CLAUDE.md is {live:N0} bytes — too small to still hold the live rules. " +
            "Do not gut a still-needed rule to hit a size number.");
        Assert.True(live <= 96 * 1024,
            $"CLAUDE.md is {live:N0} bytes — that is a novel again. Compact the story; " +
            "move it to docs/archive/. The 2026-09-08 snapshot was 171,685 bytes.");
    }

    [Fact]
    public void ArchiveKeepsThePreSplitManual()
    {
        var snapshot = Path.Combine(Repo, "docs", "archive", "claude-2026-09-08.md");
        Assert.True(File.Exists(snapshot),
            "docs/archive/claude-2026-09-08.md is the pre-split novel. Do not delete it.");
        var bytes = new FileInfo(snapshot).Length;
        Assert.True(bytes >= 150 * 1024,
            $"The snapshot is {bytes:N0} bytes; the live file was 171,685 on 2026-09-08. " +
            "Do not edit the snapshot to 'fix' it.");
    }

    [Fact]
    public void ClaudeMdKeepsEveryTrapNumber()
    {
        var text = Read("CLAUDE.md");
        var missing = new List<int>();
        for (var n = 1; n <= 68; n++)
        {
            // "1. **Title" at the start of a trap line. Two traps share 64 on purpose.
            if (!Regex.IsMatch(text, $@"^{n}\. \*\*", RegexOptions.Multiline))
                missing.Add(n);
        }

        Assert.True(missing.Count == 0,
            "CLAUDE.md dropped trap number(s) " + string.Join(", ", missing) +
            ". Compact the novel; keep the rule. Snapshot: docs/archive/claude-2026-09-08.md");
    }

    [Fact]
    public void ClaudeMdPointsAtTheLabAndTheArchive()
    {
        var text = Read("CLAUDE.md");
        Assert.Contains("docs/lab/VerificationLadder.md", text);
        Assert.Contains("docs/lab/FlakeLedger.md", text);
        Assert.Contains("docs/archive/README.md", text);
        Assert.Contains("docs/Architecture.md", text);
        Assert.Contains("docs/TestPlan.md", text);
        Assert.Contains("scripts/check.ps1", text);
    }

    [Fact]
    public void VerificationLadderKeepsCiAuthoritative()
    {
        var text = Read(Path.Combine("docs", "lab", "VerificationLadder.md"));
        Assert.Contains("Lab experiment", text);
        Assert.Contains("not a Corps-wide standard", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("authoritative", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("V0", text);
        Assert.Contains("V1", text);
        Assert.Contains("V2", text);
        Assert.Contains("V3", text);
        Assert.Contains("scripts/check.ps1", text);
        Assert.DoesNotContain("CI can be skipped", text);
    }

    [Fact]
    public void FlakeLedgerHasTheContractColumnsAndForbidsPassedOnRerunAsDisposition()
    {
        var text = Read(Path.Combine("docs", "lab", "FlakeLedger.md"));
        foreach (var column in new[] { "Signature", "Test", "Environment", "Occurrences", "Disposition" })
            Assert.Contains(column, text);

        Assert.Contains("Passed on rerun", text);
        Assert.Contains("observation", text, StringComparison.OrdinalIgnoreCase);

        var ledger = text.Split("## Ledger", 2);
        Assert.True(ledger.Length == 2, "FlakeLedger.md lost its ## Ledger table");

        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "passed-on-rerun", "passed on rerun", "resolved", "flake", "ignore"
        };
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "open", "watched", "fixed", "retired", "accepted"
        };

        var rows = Regex.Matches(ledger[1], @"^\| (?<sig>[^|\n]+) \|(?<rest>.+)\|$",
            RegexOptions.Multiline);
        var data = new List<(string Sig, string Disposition)>();
        foreach (Match row in rows)
        {
            var sig = row.Groups["sig"].Value.Trim();
            if (sig is "Signature" or "---" || sig.StartsWith('-')) continue;
            var cells = row.Groups["rest"].Value.Split('|')
                .Select(c => c.Trim())
                .ToArray();
            // Test | Environment | Occurrences | Disposition | Notes
            if (cells.Length < 4) continue;
            data.Add((sig, cells[3]));
        }

        Assert.True(data.Count >= 5,
            $"expected seeded flake rows; found {data.Count}");
        foreach (var (sig, disposition) in data)
        {
            Assert.False(forbidden.Contains(disposition),
                $"Disposition '{disposition}' on '{sig}' is an observation, not a close. " +
                "Use open/watched/fixed/retired/accepted. Put 'passed on rerun' in Occurrences.");
            Assert.True(allowed.Contains(disposition),
                $"Disposition '{disposition}' on '{sig}' is not one of " +
                "open/watched/fixed/retired/accepted.");
        }
    }
}
