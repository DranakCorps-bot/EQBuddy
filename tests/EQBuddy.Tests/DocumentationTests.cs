using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The repo's own documentation, checked against the repo.
///
/// CLAUDE.md, docs/Architecture.md and docs/TestPlan.md exist so that an agent (or a new
/// contributor) does not have to rediscover this codebase from scratch every time. That
/// only works while they are TRUE — a confidently wrong map is worse than no map, because
/// it is followed. Keeping them true was a discipline until this file; now it is the
/// build.
///
/// These tests deliberately check only claims that can be checked mechanically: that the
/// files pointed at exist, that the tests named as evidence exist, and that quoted
/// numbers match their source. Prose is left to review.
/// </summary>
public class DocumentationTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Repo, relative));

    /// <summary>The docs write paths in shorthand ("Core/LogParser.cs") because the full
    /// project names make the tables unreadable. Same expansion a reader does by eye.</summary>
    private static string? Resolve(string quoted)
    {
        var candidates = new[]
        {
            quoted,
            "src/EQBuddy." + quoted,          // Core/…, UI.Shared/…, Companion/…, Avalonia/…
            "src/" + quoted,                  // EQBuddy/MainWindow.xaml.cs
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(Path.Combine(Repo, c.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(full)) return full;
        }
        return null;
    }

    /// <summary>The rotated channel ledgers (DRA-75 / M0-2) are the one thing under
    /// <c>docs/ops</c> that is a TRANSCRIPT rather than a map: what agents said to each
    /// other, immutable, dated. Their paths were true when they were written, so
    /// <c>scripts/release-review.ps1</c> and <c>/tmp/helm-entry.md</c> appear in them and
    /// no longer exist — and the sweep's remedy ("fix the doc or restore the file") is
    /// available for neither. A live doc is corrected; a record of what was said is not.
    /// The archive's own <c>README.md</c> IS a map and stays swept.</summary>
    private const string ChannelArchive = "docs/ops/claude-archive/channels/";

    private static bool IsRotatedChannelLedger(string relative) =>
        relative.StartsWith(ChannelArchive, StringComparison.Ordinal)
        && !relative.EndsWith("/README.md", StringComparison.Ordinal);

    private static List<string> SweptDocs()
    {
        // Live manuals plus the 2026-09-08 ops split (verification ladder, flake
        // ledger, CLAUDE archive). Archive novels stay true the same way CLAUDE.md
        // does — a confidently wrong map is worse than no map.
        var files = new List<string>
        {
            "CLAUDE.md",
            "docs/Architecture.md",
            "docs/TestPlan.md",
        };
        var ops = Path.Combine(Repo, "docs", "ops");
        if (Directory.Exists(ops))
        {
            files.AddRange(Directory
                .EnumerateFiles(ops, "*.md", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(Repo, p).Replace('\\', '/'))
                .Where(p => !IsRotatedChannelLedger(p))
                .OrderBy(p => p, StringComparer.Ordinal));
        }
        return files;
    }

    public static TheoryData<string> DocFiles()
    {
        var data = new TheoryData<string>();
        foreach (var f in SweptDocs()) data.Add(f);
        return data;
    }

    /// <summary>
    /// The paired must-list for the exclusion above (trap 34), and its non-vacuity check
    /// (trap 78). An exemption that silently stops matching anything is the same defect as
    /// no exemption — except green. So: the transcripts must actually BE there and excluded,
    /// the archive's README must still be swept, and every OTHER ops doc must stay swept.
    /// </summary>
    [Fact]
    public void OnlyTheRotatedChannelTranscriptsAreExemptFromTheLivePathSweep()
    {
        var channels = Path.Combine(Repo, "docs", "ops", "claude-archive", "channels");
        Assert.True(Directory.Exists(channels),
            "the rotated channel archive is missing — the exemption below would be vacuous");

        var onDisk = Directory
            .EnumerateFiles(channels, "*.md", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(Repo, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        var transcripts = onDisk.Where(IsRotatedChannelLedger).ToList();
        Assert.NotEmpty(transcripts);

        var swept = SweptDocs();

        // The transcripts are out.
        foreach (var t in transcripts)
            Assert.DoesNotContain(t, swept);

        // The archive's own map is in — it is the one file in there that a reader
        // navigates by, so a dead pointer in it is the ordinary failure this file catches.
        foreach (var readme in onDisk.Except(transcripts, StringComparer.Ordinal))
            Assert.Contains(readme, swept);

        // And nothing else under docs/ops slipped out with them.
        var everyOpsDoc = Directory
            .EnumerateFiles(Path.Combine(Repo, "docs", "ops"), "*.md", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(Repo, p).Replace('\\', '/'));
        foreach (var doc in everyOpsDoc.Except(transcripts, StringComparer.Ordinal))
            Assert.Contains(doc, swept);
    }

    [Theory]
    [MemberData(nameof(DocFiles))]
    public void EveryFileTheDocsPointAtExists(string doc)
    {
        var text = Read(doc);
        // Backticked tokens that look like a path to something in this repo.
        var missing = Regex.Matches(text, @"`([A-Za-z0-9_./-]+\.(?:cs|ps1|html|json|md|slnx|props))`")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            // Bare filenames ("settings.json", "index.html") name a concept, not a path.
            .Where(p => p.Contains('/'))
            .Where(p => Resolve(p) is null)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            $"{doc} points at files that no longer exist — fix the doc or restore the file:\n  "
            + string.Join("\n  ", missing));
    }

    [Theory]
    [InlineData("docs/TestPlan.md")]
    [InlineData("CLAUDE.md")]
    [InlineData("docs/Architecture.md")]
    public void EveryTestNamedAsEvidenceInTheTestPlanExists(string doc)
    {
        // The Held-by column cites test classes. A renamed or deleted suite silently
        // turns a documented guarantee into a fiction, which is the exact failure this
        // file exists to prevent.
        //
        // **Extended to CLAUDE.md and Architecture.md on 2026-08-24.** This checked only
        // the TestPlan, and the TestPlan is not where most of these claims live: the trap
        // list in CLAUDE.md cites ~30 suites, almost all of them in the form "→ **Now
        // guarded:** `SomethingTests`". A trap that names a guard which no longer exists
        // is worse than a trap with no guard at all — it tells the next reader the hole
        // is closed, and that reader is the one deciding whether to be careful.
        //
        // Scanned from SOURCE across every test project rather than by reflection over
        // this assembly: the docs legitimately cite suites in EQBuddy.E2E, which this
        // project does not reference and should not. (Caught by this very test on the day
        // E2E scenarios were first cited here. It also cited EQBuddy.Avalonia.Tests until
        // E-2c deleted that project — and this test is what forced the 15 citations of its
        // suites to be corrected in the same commit as the deletion, rather than left
        // pointing at guards that no longer exist.)
        var known = Directory
            .EnumerateFiles(Path.Combine(Repo, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(p => Regex.Matches(File.ReadAllText(p), @"\bclass\s+([A-Za-z0-9_]+Tests)\b")
                .Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        var cited = Regex.Matches(Read(doc), @"`([A-Za-z0-9_]+Tests)`")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(cited);   // a doc citing nothing is a doc that stopped being maintained
        var gone = cited.Where(name => !known.Contains(name))
            .OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(gone.Count == 0,
            $"{doc} cites test classes that exist in no test project. "
            + "Either the doc is stale or a guarantee lost its test:\n  " + string.Join("\n  ", gone));
    }

    [Fact]
    public void TheRatchetTableInTheArchitectureDocMatchesTheRatchetItself()
    {
        // Architecture.md quotes the hotspot baselines. Numbers in prose rot faster than
        // anything else in a repo, and a wrong headroom figure invites exactly the
        // "surely there's room" reasoning the ratchet exists to stop.
        var doc = Read("docs/Architecture.md");
        var source = Read("tests/EQBuddy.Tests/ArchitectureTests.cs");

        var actual = Regex.Matches(source, @"\(@""([^""]+)"",\s*(\d+)\)")
            .ToDictionary(m => m.Groups[1].Value.Replace('\\', '/'),
                          m => int.Parse(m.Groups[2].Value));
        Assert.NotEmpty(actual);

        foreach (var (path, baseline) in actual)
        {
            // Row shape: | `EQBuddy/MainWindow.xaml.cs` | 4,891 | … |
            var row = Regex.Match(doc,
                @"\|\s*`" + Regex.Escape(path) + @"`\s*\|\s*([\d,]+)\s*\|");
            Assert.True(row.Success,
                $"docs/Architecture.md's ratchet table is missing a row for {path}.");
            Assert.Equal(baseline, int.Parse(row.Groups[1].Value.Replace(",", "")));
        }
    }

    [Fact]
    public void TheDocsPointAtEachOtherSoNoneOfThemIsOrphaned()
    {
        // The whole scheme relies on CLAUDE.md being the entry point: it is the only one
        // loaded automatically, so anything it does not link to is effectively invisible.
        var claude = Read("CLAUDE.md");
        Assert.Contains("docs/Architecture.md", claude);
        Assert.Contains("docs/TestPlan.md", claude);
        Assert.Contains("scripts/check.ps1", claude);
        // C′ + Context (2026-09-08): the live file is the pointer set.
        Assert.Contains("docs/ops/verification-ladder.md", claude);
        Assert.Contains("docs/ops/flake-ledger.md", claude);
        Assert.Contains("docs/ops/claude-archive/", claude);
        // DRA-74 / DRA-73 M0 (2026-09-14): the execution-flow detail is only
        // reachable from the always-loaded file, so an unlinked copy is invisible.
        Assert.Contains("docs/ops/execution-flow.md", claude);
    }

    /// <summary>Markdown wraps, so a sentence that is one claim to a reader is several
    /// lines to <c>Contains</c>. Collapse runs of whitespace before asserting on prose.</summary>
    private static string Flatten(string text) => Regex.Replace(text, @"\s+", " ");

    /// <summary>
    /// DRA-74 (DRA-73 plan §7 M0 / §8.1–2, approved by David 2026-09-14): two process
    /// cutovers that exist ONLY as prose, and therefore have no other way to be kept true.
    ///
    /// **The failure this prevents is silent reversion.** Both rules DELETE a step
    /// (the `helm/ssc-N` PR; the per-slice kick authorization). A deleted step leaves no
    /// artifact behind, so nothing in the repo notices when an agent starts doing it again
    /// — the retired pattern simply reappears in a posture list and looks like diligence.
    /// A rule with a real reason to be reversed should be reversed OUT LOUD, by a HOLD and
    /// an edit that reddens this, not by drift.
    ///
    /// The `exo-experiment:` half is the other direction: §10.1 makes that tag the thing
    /// the M0-exit doctrine capture cites, so an untagged experiment is one the Corps
    /// playbook cannot find. Prove-failed by deleting each asserted phrase in turn.
    /// </summary>
    [Fact]
    public void TheRetiredSscPatternAndWholeSequenceAuthAreStatedInTheLiveDocs()
    {
        var claude = Flatten(Read("CLAUDE.md"));
        Assert.Contains("never a `helm/ssc-N` PR", claude);
        Assert.Contains("no new ones.", claude);
        Assert.Contains(
            "A signed plan authorizes every slice it declares, in order, on green gates.",
            claude);
        Assert.Contains(
            "Helm stops the train with a HOLD, not by withholding authorization",
            claude);

        // The detail doc carries the evidence and the rollback shape; the experiment names
        // are what the dashboard and the playbook entry key on.
        var flow = Flatten(Read(Path.Combine("docs", "ops", "execution-flow.md")));
        Assert.Contains("ssc-retirement", flow);
        Assert.Contains("whole-sequence-auth", flow);

        var decisions = Flatten(Read("DECISIONS.md"));
        Assert.Contains("exo-experiment: ssc-retirement", decisions);
        Assert.Contains("exo-experiment: whole-sequence-auth", decisions);
    }

    /// <summary>
    /// DRA-179 D2 (Helm SIGNED the D1–D4 sequence 2026-09-17, and picked the gate's
    /// enforcement mechanism in the same tip): the Jr/Sr lane mechanics.
    ///
    /// **Same failure mode as the test above, one lane down.** The Jr review gate is a
    /// CHECKLIST rather than GitHub branch protection — Helm's pick, because the org
    /// pushes through one bot identity and an unread rejection teaches people to route
    /// around a gate. A checklist refuses nothing by itself, so the ONLY thing that keeps
    /// it real is that the block is committed, cited, and unticked. Nothing else in the
    /// repo notices if the gate quietly loses its last box, gains a pre-ticked one, or
    /// stops being mentioned in the always-loaded file — and every one of those reads as
    /// tidying rather than as a deleted control.
    ///
    /// Prove-failed by mutation, one at a time: deleting each asserted phrase, dropping
    /// the Sr box from the block, pre-ticking a box, and reordering the Sr box out of last
    /// place each redden exactly one assertion here.
    /// </summary>
    [Fact]
    public void TheJrLaneMechanicsAndItsReviewGateAreStatedInTheLiveDocs()
    {
        // The compact live rule: an agent that reads only the always-loaded file still
        // learns the three things that bind it — the seat, the lane, the gate.
        var claude = Flatten(Read("CLAUDE.md"));
        Assert.Contains("Sr's review is not a second claim", claude);
        Assert.Contains("no shipped code path calls a model", claude);
        Assert.Contains("A Jr PR merges only on a ticked Sr gate", claude);

        var flow = Flatten(Read(Path.Combine("docs", "ops", "execution-flow.md")));
        Assert.Contains(
            "A Jr PR does not merge without an Sr review, and the enforcement mechanism "
            + "is a CHECKLIST",
            flow);
        Assert.Contains("an unticked last box is a merge that does not happen", flow);
        Assert.Contains("no shipped EQBuddy code path calls Qwen or any model", flow);

        // The gate block itself, read as a block rather than as prose: it is the artifact
        // a Jr PR pastes, so its SHAPE is the thing that has to survive an edit.
        var raw = Read(Path.Combine("docs", "ops", "execution-flow.md"));
        var block = Regex.Match(raw, @"```markdown\r?\n(?<body>### Jr lane gate.*?)```",
            RegexOptions.Singleline);
        Assert.True(block.Success, "docs/ops/execution-flow.md no longer carries the Jr lane gate block");

        var boxes = Regex.Matches(block.Groups["body"].Value, @"^- \[(?<tick>[ x])\] (?<text>.+)$",
            RegexOptions.Multiline);
        Assert.Equal(6, boxes.Count);

        // A shipped template with a box already ticked is a gate that arrives satisfied.
        Assert.DoesNotContain(boxes.Cast<Match>(), m => m.Groups["tick"].Value == "x");

        // The Sr box is LAST because the merge rule names it that way ("an unticked last
        // box"). A reorder would leave the words true and the instruction wrong.
        Assert.Contains("Sr reviewed — Sr ticks this, never Jr",
            boxes[^1].Groups["text"].Value);
        Assert.Contains("route: routine", boxes[0].Groups["text"].Value);
    }

    [Fact]
    public void FlakeLedgerNamesTheRequiredColumnsAndTheRerunRule()
    {
        var text = Read(Path.Combine("docs", "ops", "flake-ledger.md"));
        Assert.Contains("| Signature |", text);
        Assert.Contains("| Occurrences |", text);
        Assert.Contains("| Affected test |", text);
        Assert.Contains("| Environment |", text);
        Assert.Contains("| Disposition |", text);
        Assert.Contains("Passed on rerun", text);
        Assert.Contains("not a resolution", text);
    }

    [Fact]
    public void ClaudeArchiveKeepsThePreSplitSnapshot()
    {
        // Reversibility: the novels are not deleted, they moved. A missing
        // snapshot is a split that cannot be undone from the tree.
        var snapshot = Path.Combine(Repo, "docs", "ops", "claude-archive", "claude-2026-09-08.md");
        var traps = Path.Combine(Repo, "docs", "ops", "claude-archive", "traps.md");
        Assert.True(File.Exists(snapshot), "pre-split CLAUDE.md snapshot missing");
        Assert.True(File.Exists(traps), "anchored trap novels missing");
        Assert.True(new FileInfo(snapshot).Length > 100_000,
            "snapshot is too small to be the pre-split CLAUDE.md");
    }
}
