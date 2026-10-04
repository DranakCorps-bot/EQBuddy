using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// DRA-67. The landing page's hero pill read "Log-only — reads your /log file, nothing
/// else" — on the most-read line of the most public surface the project has — while the
/// SAME page handed the player a copy button for an /outputfile command whose dump we
/// read. There are four such dumps (<see cref="GameCommands"/>), so the claim was not a
/// rounding error; and nothing caught it because no test had ever opened
/// <c>site/index.html</c>.
///
/// DRA-68 widened it to the repo's FRONT DOOR. The landing's own footer links
/// <c>README.md</c> and <c>EQBuddy-Evolved.md</c>, so correcting only the page walked a
/// reader who checks us from a corrected surface straight onto an uncorrected one —
/// <c>README.md</c> said "Log-only, by principle … it knows only what your own log says"
/// and <c>EQBuddy-Evolved.md</c> carried the literal sibling of the §08 card. A guard that
/// reads one of three surfaces is a guard against one third of the claim.
///
/// DRA-87 took the last three, and they are a DIFFERENT SHAPE — labels over prose that was
/// already true, which is why two honesty cards walked past them. <c>PRODUCT.md</c>'s
/// "### Log-only and local-first" and the v2 charter's "## 2.2 Log-only and local-first"
/// each head a bullet list where every bullet is correct ("no game-memory reads", "no
/// packet inspection"); <c>SECURITY.md</c>'s "EQBuddy's rule is log-only, zero telemetry"
/// is a true statement about EGRESS wearing the wrong noun. Nothing under the heading was
/// wrong, and the heading is the part a reader quotes back at you. The chain is the same
/// one, a link further: <c>EQBuddy-Evolved.md</c>'s own intro points at <c>PRODUCT.md</c>
/// BY NAME for "the product identity in full".
///
/// Six surfaces now, and two columns of <see cref="Surfaces"/> differ by decision rather
/// than convenience — which surface must ENUMERATE the dumps, and which boundary line each
/// must keep while being corrected (<see cref="SecurityBoundary"/>).
///
/// The negative below cannot see a MISSING thing (trap 34), so it is paired with a
/// must-list that is DERIVED from <see cref="GameCommands"/> rather than written here:
/// a fifth /outputfile dump reddens this test until the enumerating surfaces name it,
/// which is the only version of this guard that survives the next command being added.
///
/// DRA-89 then closed the gap that widening left. DRA-87 put a list into the v2 charter's
/// ACCURACY-001 table that enumerates the dumps and cites <c>GameCommands</c> BY NAME — into
/// the one surface the table tells to take the short form. So the only list in that change
/// naming the producer as its authority was the only list nothing checked against it. See
/// <see cref="OutputFilesRowViolations"/>, and the note there on why the row is checked
/// where it is written rather than by flipping the surface's flag.
///
/// The checks are factored through <see cref="Violations"/> so the pre-DRA-67 wording can
/// ride along as a committed negative — green-only is vacuous coverage, and the wording
/// this exists to forbid is the honest fixture to prove-fail against.
/// </summary>
public sealed class LandingSourceClaimsTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Page => File.ReadAllText(Path.Combine(Repo, "site", "index.html"));

    /// <summary>
    /// The dump NOUN of every /outputfile command the app ships, taken from the one
    /// producer: "/outputfile faction" → "faction". Reflection rather than a literal list
    /// is the whole point — this is the must-list, and a hand-copied one stops covering
    /// the enum the day it grows (trap 30).
    /// </summary>
    public static readonly string[] DumpNames = typeof(GameCommands)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(string)
                 && f.Name.StartsWith("Outputfile", StringComparison.Ordinal))
        .Select(f => (string)f.GetValue(null)!)
        .Select(c => c.Split(' ')[1])
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToArray();

    /// <summary>
    /// The claim, in every shape it has actually been written in across the three
    /// surfaces. "knows only what your own log says" is README's own phrasing and is the
    /// reason a scan for the hyphenated pill would have reported that file clean.
    /// </summary>
    private static readonly string[] ForbiddenClaims =
    [
        "log-only",
        "log only",
        "reads only the log",
        "knows only what your own log",
    ];

    /// <summary>
    /// The product boundary, as CONCEPTS with their accepted phrasings — not as literal
    /// bytes. The surfaces say these in different words and always have: README writes
    /// "game memory" where the landing writes "game-memory"; EQBuddy-Evolved.md's hard
    /// line says "a way to judge other people", PRODUCT.md says "judge other players" and
    /// the v2 charter says "judging other players", where the landing says "measures other
    /// players". A guard that demanded one spelling would be demanding a rewrite of a
    /// correct sentence, which is how a gate teaches people to edit around it.
    ///
    /// DRA-87 widened the second pattern for exactly that reason: PRODUCT.md's and the
    /// charter's sentences were already correct and already shipped, and the alternative
    /// to widening was editing two true sentences to buy a green run.
    /// </summary>
    private static readonly (string Label, string Pattern)[] ProductBoundary =
    [
        ("game-memory", @"game[- ]memory"),
        ("measures other players", @"measures other players|judg(?:e|ing) other (?:people|players)"),
    ];

    /// <summary>
    /// SECURITY.md's boundary is NOT the product's two, and DRA-87's decision was to say so
    /// rather than to switch the arm off for that file.
    ///
    /// The card left "whether/how to claim-test SECURITY.md" to the executor. Arms (a), (b)
    /// and (c) fit it better than any other surface — a page whose own genre is
    /// completeness ("The complete list of hosts", "That's the whole list", "Everything
    /// lives under %AppData%") is the last place that may take the short form, and a fifth
    /// dump SHOULD redden it. Arm (d) was the problem: SECURITY.md carries NEITHER product
    /// values line, because it is not about the product's values — it is about what leaves
    /// the machine, what is written to disk, and how an update is verified. Demanding
    /// "never measures other players" on it would have forced unrelated prose onto a
    /// correct page, which is the same failure as demanding one spelling.
    ///
    /// So the arm is not exempted; it is KEYED TO THE PROMISE THE PAGE ACTUALLY MAKES.
    /// That matters because an off switch would have let a future surface join with the
    /// check silently disabled, whereas a per-surface SET cannot go quietly empty —
    /// <see cref="EverySurfaceCarriesABoundaryToKeep"/> refuses one that does. The point of
    /// arm (d) was never the two specific sentences; it was that correcting a false claim
    /// must not cost the true boundary line standing next to it, and on this page that line
    /// is "zero telemetry".
    /// </summary>
    private static readonly (string Label, string Pattern)[] SecurityBoundary =
    [
        ("zero telemetry", @"zero telemetry|no telemetry"),
        ("never sends your data on its own", @"never sends your data|sends nothing about you"),
    ];

    /// <summary>
    /// What a surface may not say, and what it must. Returns one line per violation so a
    /// failure names the claim rather than reporting a bare false.
    /// </summary>
    /// <param name="markdown">Pick the paragraph notion: &lt;p&gt; tags, or blank-line
    /// and list-item blocks. The must-list is a claim about a SINGLE paragraph, so it is
    /// only as good as the splitter.</param>
    /// <param name="mustEnumerateDumps">Whether this surface has to name every dump. The
    /// landing's §08 card and EQBuddy-Evolved.md's hard line are the two places that
    /// answer "what does EQBuddy read?" in full; README deliberately takes the short form
    /// (DRA-68's card), so it must DISCLOSE that the dumps exist without enumerating
    /// them. The consequence is stated rather than hidden: a fifth dump reddens the two
    /// enumerating surfaces, and README has nothing to go stale.</param>
    /// <param name="exempt">Sentences that contain a forbidden claim and are nonetheless
    /// TRUE. These excuse a CLAIM only — they are stripped before the claim scan and
    /// nowhere else, so an exemption can never satisfy the must-list or stand in for a
    /// values line. <see cref="EveryExemptSentenceIsStillInItsFile"/> keeps them honest.</param>
    /// <param name="valuesLines">The boundary line(s) this surface must keep while being
    /// corrected. Defaults to <see cref="ProductBoundary"/>; SECURITY.md brings its own
    /// (<see cref="SecurityBoundary"/>). Never null and never empty — that is the whole
    /// difference between a per-surface SET and an off switch.</param>
    internal static IReadOnlyList<string> Violations(
        string text,
        bool markdown = false,
        bool mustEnumerateDumps = true,
        IReadOnlyList<string>? exempt = null,
        (string Label, string Pattern)[]? valuesLines = null)
    {
        var bad = new List<string>();
        var flat = Flatten(text);

        // (a) The false claim. Scanned over a copy with the exempt sentences removed, so a
        // sentence that is true in its own context does not have to be reworded into a
        // vaguer one to buy a green run.
        var scan = flat;
        foreach (var ok in exempt ?? [])
            scan = scan.Replace(Flatten(ok), " ", StringComparison.OrdinalIgnoreCase);

        foreach (var claim in ForbiddenClaims)
            if (scan.Contains(claim, StringComparison.OrdinalIgnoreCase))
                bad.Add($"claims \"{claim}\" — we also read the /outputfile dumps");

        // (b) Silence is not honesty. Every covered surface has to tell the reader the
        // dumps exist at all; deleting the pill rather than correcting it is the failure
        // this arm exists for.
        if (!flat.Contains("/outputfile", StringComparison.Ordinal))
            bad.Add("never mentions /outputfile — the dumps are undisclosed");

        // (c) The must-list: ONE place answers "what does EQBuddy read?" in full. Scattered
        // half-answers are how "nothing else" survived beside a working /outputfile button,
        // so the assertion is that a single paragraph names the log AND every dump.
        if (mustEnumerateDumps)
        {
            var answered = Blocks(text, markdown).Any(p =>
                p.Contains("/log", StringComparison.Ordinal) &&
                p.Contains("/outputfile", StringComparison.Ordinal) &&
                DumpNames.All(n => p.Contains(n, StringComparison.OrdinalIgnoreCase)));
            if (!answered)
                bad.Add("no single paragraph names /log plus every /outputfile dump ("
                        + string.Join(", ", DumpNames) + ")");
        }

        // (d) Being honest about the dumps must not cost the true boundary line standing
        // next to the false one. The pill got broader; these do not move. Which line that
        // is depends on the promise the surface makes — see SecurityBoundary.
        foreach (var (label, pattern) in valuesLines ?? ProductBoundary)
            if (!Regex.IsMatch(flat, pattern, RegexOptions.IgnoreCase))
                bad.Add($"dropped the values line: \"{label}\"");

        return bad;
    }

    /// <summary>
    /// Collapse whitespace runs to one space, because HTML does and the source does not.
    /// Every check here is about a SENTENCE, and where a sentence happens to be wrapped is
    /// a property of the editor that last touched the file. This is not hypothetical
    /// tidiness: the first cut of this guard compared raw bytes and reddened against a
    /// competing DRA-67 branch whose footer wrapped "never measures / other players" across
    /// a line — the values line was present and correct, and the gate called it dropped.
    /// A gate that fails for a reason unrelated to its claim is one people learn to re-run
    /// until it passes, which is trap 74's real cost.
    /// </summary>
    private static string Flatten(string s) => Regex.Replace(s, @"\s+", " ").Trim();

    private static IEnumerable<string> Blocks(string text, bool markdown) =>
        markdown ? MarkdownBlocks(text) : Paragraphs(text);

    private static IEnumerable<string> Paragraphs(string html) =>
        Regex.Matches(html, "<p[^>]*>(.*?)</p>", RegexOptions.Singleline)
             .Select(m => Flatten(m.Groups[1].Value));

    /// <summary>
    /// Markdown's paragraph is a blank-line-separated run — except that a LIST ITEM starts
    /// one too. Without that second rule EQBuddy-Evolved.md's four "Hard lines" bullets are
    /// one block, and the must-list would pass on a file that named the dumps in the bullet
    /// ABOUT SOMETHING ELSE. The whole point of asking for a single paragraph is that the
    /// answer arrives where the claim is made.
    /// </summary>
    private static IEnumerable<string> MarkdownBlocks(string md)
    {
        var cur = new List<string>();
        foreach (var line in md.Replace("\r\n", "\n").Split('\n'))
        {
            var blank = line.Trim().Length == 0;
            var item = Regex.IsMatch(line, @"^\s*([-*+]|\d+\.)\s");
            if ((blank || item) && cur.Count > 0)
            {
                yield return Flatten(string.Join(" ", cur));
                cur.Clear();
            }
            if (!blank) cur.Add(line);
        }
        if (cur.Count > 0) yield return Flatten(string.Join(" ", cur));
    }

    // ---- The covered surfaces -------------------------------------------------------

    /// <summary>
    /// README.md line ~358, verbatim. "EQBuddy reads only the log" is a forbidden claim
    /// everywhere else and is EXACTLY TRUE here, because it is about live POSITION: no
    /// /outputfile dump reports where you are standing, which is why the marker moves when
    /// you type /loc and not by magic. DRA-68's card named this sentence and said not to
    /// touch it — a regex sweep would have replaced a true sentence with a vaguer one.
    ///
    /// The exemption is keyed on the SENTENCE, not on the file or a line number, so it
    /// cannot quietly excuse the next "log-only" someone adds to README.
    /// </summary>
    private const string ReadmePositionSentence =
        "EQBuddy reads only the log, so the marker moves when you ask it to, not by magic.";

    internal sealed record Surface(
        string Path,
        bool Markdown,
        bool MustEnumerateDumps,
        string[] Exempt,
        (string Label, string Pattern)[] ValuesLines);

    /// <summary>
    /// Six surfaces, and the two columns that differ are decisions rather than
    /// convenience.
    ///
    /// ENUMERATE answers "does this surface promise to answer in full?" The landing's §08
    /// card, EQBuddy-Evolved.md's hard line, PRODUCT.md's principle and SECURITY.md's
    /// opening paragraph all do; README (DRA-68's card) and the v2 charter take the short
    /// form and must DISCLOSE the dumps without listing them. The consequence is stated
    /// rather than hidden: a fifth dump reddens the four enumerating surfaces, and README
    /// has nothing to go stale.
    ///
    /// The charter takes the short form because the must-list exists for a PLAYER asking
    /// "what does EQBuddy read?" — that reader reaches the landing, README, PRODUCT.md and
    /// SECURITY.md, not an internal requirements doc whose audience line names Helm, Fable
    /// and the execution agents. What the charter owes is that its hard lines are not
    /// FALSE, which is arms (a) and (b).
    ///
    /// "Nothing to go stale" was said of the charter too until DRA-89, and it was never
    /// true of it: excusing this file from ENUMERATING never stopped it enumerating, and
    /// DRA-87's ACCURACY-001 row does. The flag stays <c>false</c> — it asks "does SOME
    /// paragraph answer in full?", which §2.2 could discharge while the stale row sat
    /// untouched — and the row is checked where it is written instead
    /// (<see cref="TheCharterOutputFilesRowIsVerifiedAgainstGameCommands"/>).
    ///
    /// VALUES is the boundary each surface must keep while being corrected, and SECURITY.md
    /// is the one that is not the product pair — see <see cref="SecurityBoundary"/>.
    /// </summary>
    internal static readonly Surface[] Surfaces =
    [
        new(Path.Combine("site", "index.html"), Markdown: false, MustEnumerateDumps: true,
            Exempt: [], ValuesLines: ProductBoundary),
        new("EQBuddy-Evolved.md", Markdown: true, MustEnumerateDumps: true,
            Exempt: [], ValuesLines: ProductBoundary),
        new("README.md", Markdown: true, MustEnumerateDumps: false,
            Exempt: [ReadmePositionSentence], ValuesLines: ProductBoundary),
        new("PRODUCT.md", Markdown: true, MustEnumerateDumps: true,
            Exempt: [], ValuesLines: ProductBoundary),
        new("SECURITY.md", Markdown: true, MustEnumerateDumps: true,
            Exempt: [], ValuesLines: SecurityBoundary),
        new(Path.Combine("docs", "v2", "EQBuddy-v2-Project-Guide-Requirements.md"),
            Markdown: true, MustEnumerateDumps: false, Exempt: [], ValuesLines: ProductBoundary),
    ];

    public static IEnumerable<object[]> SurfacePaths() => Surfaces.Select(s => new object[] { s.Path });

    private static Surface Find(string path) => Surfaces.Single(s => s.Path == path);

    [Theory]
    [MemberData(nameof(SurfacePaths))]
    public void TheSurfaceIsHonestAboutWhatItReads(string path)
    {
        var s = Find(path);
        var text = File.ReadAllText(Path.Combine(Repo, s.Path));
        Assert.Empty(Violations(text, s.Markdown, s.MustEnumerateDumps, s.Exempt, s.ValuesLines));
    }

    /// <summary>
    /// An exemption is a standing permission to say something false, so it has to keep
    /// pointing at the true sentence that earned it. If README is reworded and this stops
    /// matching, the exemption is dead text quietly widening the guard's blind spot —
    /// trap 34 aimed at the guard's own carve-out rather than at the product.
    /// </summary>
    [Fact]
    public void EveryExemptSentenceIsStillInItsFile()
    {
        foreach (var s in Surfaces)
        {
            var flat = Flatten(File.ReadAllText(Path.Combine(Repo, s.Path)));
            foreach (var ok in s.Exempt)
                Assert.True(flat.Contains(Flatten(ok), StringComparison.OrdinalIgnoreCase),
                    $"{s.Path} no longer contains the exempt sentence \"{ok}\" — "
                    + "delete the exemption or restore the sentence.");
        }
    }

    /// <summary>The exemption is narrow by construction: README's position sentence is
    /// excused, and a second "log-only" in the same file is still caught.</summary>
    [Fact]
    public void TheExemptionDoesNotCoverTheNextClaim()
    {
        var withExtra = ReadmePositionSentence
                      + " EQBuddy is log-only, by principle."
                      + " It never reads game memory and never measures other players."
                      + " It reads your /outputfile dumps.";
        Assert.Contains(Violations(withExtra, markdown: true, mustEnumerateDumps: false,
                                   exempt: [ReadmePositionSentence]),
                        v => v.StartsWith("claims", StringComparison.Ordinal));

        // And without the exemption the position sentence itself is caught — proving the
        // exemption is what is doing the work above, not a hole in the claim list.
        Assert.Contains(Violations(ReadmePositionSentence, markdown: true, mustEnumerateDumps: false),
                        v => v.StartsWith("claims", StringComparison.Ordinal));
    }

    /// <summary>Four dumps today. If this is ever 1, the reflection above broke and the
    /// must-list went quietly vacuous.</summary>
    [Fact]
    public void EveryOutputfileCommandContributesADumpName()
    {
        Assert.Equal(["achievements", "faction", "inventory", "spellbook"], DumpNames);
        Assert.All(DumpNames, n => Assert.DoesNotContain(' ', n));
    }

    /// <summary>A detector whose pattern list is empty matches nothing and reports clean
    /// (trap 78). Every list here is load-bearing; none may go quietly empty.</summary>
    [Fact]
    public void TheDetectorListsAreNotEmpty()
    {
        Assert.NotEmpty(ForbiddenClaims);
        Assert.NotEmpty(ProductBoundary);
        Assert.NotEmpty(SecurityBoundary);
        Assert.NotEmpty(Surfaces);
        Assert.NotEmpty(CountWords);
    }

    // ---- The charter's Output-files row (DRA-89) -------------------------------------

    /// <summary>
    /// DRA-89. The v2 charter's ACCURACY-001 corpus table has a row that enumerates the
    /// dumps AND cites <c>GameCommands</c> as its authority — and it is the one list in
    /// DRA-87's change that nothing checked against that producer. A fifth /outputfile
    /// command reddens the four enumerating surfaces and leaves this row saying "the four"
    /// (trap 30), with the citation making the stale claim read as verified.
    ///
    /// The row is checked HERE rather than by flipping the charter's
    /// <c>MustEnumerateDumps</c>, and the difference is not stylistic. That flag asserts
    /// "SOME single paragraph in this file names /log plus every dump" — §2.2 is the
    /// paragraph that would answer it, so a fifth dump would be discharged by editing §2.2
    /// and THIS ROW WOULD STILL SAY "the four". The flag makes the FILE redden; only a
    /// check anchored on the row makes the ROW true. It would also overturn DRA-87's
    /// reasoned short-form decision for an internal requirements doc (see
    /// <see cref="Surfaces"/>) to buy a weaker assertion.
    /// </summary>
    private const string CharterOutputFilesRowPrefix = "| Output files |";

    /// <summary>
    /// Spelled counts, indexed by the number they mean. The row states its count in words,
    /// so "does the prose agree with the enum" needs the prose's own alphabet. A count the
    /// table cannot read is a failure with the word in it, never a silent pass.
    /// </summary>
    private static readonly string[] CountWords =
    [
        "zero", "one", "two", "three", "four", "five", "six",
        "seven", "eight", "nine", "ten", "eleven", "twelve",
    ];

    /// <summary>
    /// What the row owes <see cref="GameCommands"/>, as one line per violation.
    ///
    /// Arm (b) matches each dump as a WHOLE WORD on purpose: <c>Contains("faction")</c> is
    /// satisfied by "factions", which is the exact wrong spelling #635 corrected in this
    /// row. A substring check would have let that regression back in silently.
    /// </summary>
    internal static IReadOnlyList<string> OutputFilesRowViolations(string row)
    {
        var bad = new List<string>();
        var flat = Flatten(row);

        // (a) The citation is what makes a stale list read as verified. If the row stops
        // claiming GameCommands as its authority it is an ordinary list, but while it does
        // claim it, the claim is this test's business.
        if (!flat.Contains("GameCommands", StringComparison.Ordinal))
            bad.Add("no longer cites GameCommands as its authority");

        // (b) Every dump the app actually ships, as a whole word.
        foreach (var n in DumpNames)
            if (!Regex.IsMatch(flat, $@"\b{Regex.Escape(n)}\b", RegexOptions.IgnoreCase))
                bad.Add($"does not name the \"{n}\" dump — GameCommands ships it");

        // (c) The COUNT is a second hand-copied enumeration of the same enum, and it is the
        // half that cannot be fixed by adding a noun. "the four" is a claim about
        // GameCommands.Length written in words.
        var stated = Regex.Match(flat, @"\bthe\s+([A-Za-z0-9]+)\s+`?/outputfile`?\s+dumps\b",
                                 RegexOptions.IgnoreCase);
        if (!stated.Success)
        {
            bad.Add("states no count of the /outputfile dumps — expected \"the "
                    + Spell(DumpNames.Length) + " `/outputfile` dumps\"");
        }
        else
        {
            var word = stated.Groups[1].Value;
            var n = int.TryParse(word, out var digits) ? digits : Array.IndexOf(CountWords, word.ToLowerInvariant());
            if (n != DumpNames.Length)
                bad.Add($"says \"the {word}\" /outputfile dumps, but GameCommands ships "
                        + $"{DumpNames.Length} ({string.Join(", ", DumpNames)}) — expected \"the "
                        + Spell(DumpNames.Length) + "\"");
        }

        return bad;
    }

    private static string Spell(int n) =>
        n >= 0 && n < CountWords.Length ? CountWords[n] : n.ToString();

    /// <summary>
    /// The committed row, against the committed enum. This is the assertion the finding
    /// asked for: a fifth /outputfile command in <see cref="GameCommands"/> reddens it on
    /// BOTH arms — the new dump is unnamed, and "the four" is no longer four.
    /// </summary>
    [Fact]
    public void TheCharterOutputFilesRowIsVerifiedAgainstGameCommands()
    {
        Assert.Empty(OutputFilesRowViolations(CharterOutputFilesRow()));
    }

    /// <summary>
    /// A locator that matches nothing reports clean, and one that matches everything
    /// reports on the wrong text (traps 78 and 80). The row is asserted to be exactly one
    /// line, so a renamed column heading is a loud failure rather than a quiet exemption.
    /// </summary>
    private static string CharterOutputFilesRow()
    {
        var path = Path.Combine("docs", "v2", "EQBuddy-v2-Project-Guide-Requirements.md");
        Assert.Contains(Surfaces, s => s.Path == path);

        var rows = File.ReadAllLines(Path.Combine(Repo, path))
            .Where(l => l.TrimStart().StartsWith(CharterOutputFilesRowPrefix, StringComparison.Ordinal))
            .ToArray();
        return Assert.Single(rows);
    }

    /// <summary>
    /// Prove-fail without touching the shipped enum. Each fixture is a way this row has
    /// been wrong or could go wrong, and the first is VERBATIM the pre-#635 spelling — the
    /// substring reading of "faction" passes on it, which is why arm (b) matches words.
    /// </summary>
    [Theory]
    [InlineData("| Output files | inventory, achievements, factions, spellbook — the four `/outputfile` dumps `GameCommands` ships |",
                "does not name the \"faction\" dump")]
    [InlineData("| Output files | inventory, achievements, faction — the three `/outputfile` dumps `GameCommands` ships |",
                "does not name the \"spellbook\" dump")]
    [InlineData("| Output files | inventory, achievements, faction, spellbook — the three `/outputfile` dumps `GameCommands` ships |",
                "says \"the three\" /outputfile dumps")]
    [InlineData("| Output files | inventory, achievements, faction, spellbook — the `/outputfile` dumps `GameCommands` ships |",
                "states no count")]
    [InlineData("| Output files | inventory, achievements, faction, spellbook — the four `/outputfile` dumps |",
                "no longer cites GameCommands")]
    public void EachArmOfTheRowCheckFires(string row, string expected) =>
        Assert.Contains(OutputFilesRowViolations(row),
                        v => v.Contains(expected, StringComparison.Ordinal));

    /// <summary>
    /// And the rule is satisfiable at a DIFFERENT enum size, so the count arm is reading
    /// the producer rather than agreeing with today's number by coincidence. This is the
    /// shape the row must take the day a fifth dump lands.
    /// </summary>
    [Fact]
    public void TheCountArmIsSatisfiableAtTheNextEnumSize()
    {
        Assert.Equal("four", Spell(4));
        Assert.Equal("five", Spell(5));

        var next = "| Output files | " + string.Join(", ", DumpNames) + ", motes — the "
                 + Spell(DumpNames.Length + 1) + " `/outputfile` dumps `GameCommands` ships |";
        var bad = OutputFilesRowViolations(next);
        Assert.Contains(bad, v => v.Contains("/outputfile dumps, but GameCommands ships", StringComparison.Ordinal));
        Assert.DoesNotContain(bad, v => v.StartsWith("does not name", StringComparison.Ordinal));
    }

    /// <summary>
    /// The price of making arm (d) per-surface. A bool would have had two states and both
    /// are visible in the table; a SET has a third — empty — which turns the arm off while
    /// still looking like a configured surface, and `Violations` would then report that
    /// file clean forever (trap 78 aimed at the surface table instead of the detector).
    ///
    /// This is the assertion that makes "SECURITY.md brings its own boundary" a different
    /// thing from "SECURITY.md is excused". A surface may change WHICH line it keeps; it
    /// may not join with none.
    /// </summary>
    [Fact]
    public void EverySurfaceCarriesABoundaryToKeep()
    {
        foreach (var s in Surfaces)
            Assert.True(s.ValuesLines.Length > 0,
                $"{s.Path} joined the table with no boundary line — arm (d) is off for it.");

        // And the empty set really would be the hole above: with nothing to keep, a page
        // that says only true things about its sources passes while naming no boundary.
        const string noBoundary = "EQBuddy reads your /log and the /outputfile dumps — "
                                + "inventory, achievements, faction, spellbook.";
        Assert.NotEmpty(Violations(noBoundary, markdown: true));
        Assert.Empty(Violations(noBoundary, markdown: true, valuesLines: []));
    }

    /// <summary>
    /// The decision under <see cref="SecurityBoundary"/>, proved rather than asserted.
    ///
    /// If the two sets were a distinction without a difference, handing SECURITY.md the
    /// product pair would change nothing and the per-surface column would be ceremony. It
    /// is not: the real committed file carries NEITHER product values line, because it
    /// never made that promise. So the choice was to force two unrelated sentences onto a
    /// correct security page, or to key the arm to the promise the page does make.
    /// </summary>
    [Fact]
    public void SecurityMdKeepsItsOwnPromiseAndWouldFailTheProductOne()
    {
        var text = File.ReadAllText(Path.Combine(Repo, "SECURITY.md"));

        // Its own boundary: kept, and the page passes on it.
        Assert.Empty(Violations(text, markdown: true, valuesLines: SecurityBoundary));

        // The product pair: absent from the file, and absent because the page is about
        // egress and disk rather than about what EQBuddy will not become.
        var underProductRules = Violations(text, markdown: true, valuesLines: ProductBoundary);
        Assert.Contains(underProductRules, v => v.Contains("game-memory", StringComparison.Ordinal));
        Assert.Contains(underProductRules,
            v => v.Contains("measures other players", StringComparison.Ordinal));
    }

    /// <summary>
    /// And the arm still bites on the page it was keyed to. Correcting the "log-only" label
    /// in SECURITY.md's egress rule while dropping "zero telemetry" from the same sentence
    /// is the DRA-67 failure with a new noun — a true boundary line spent to buy a green
    /// run on a false one — and it is refused.
    /// </summary>
    [Fact]
    public void FixingTheLabelMayNotCostSecurityMdsOwnBoundary()
    {
        const string spent = "EQBuddy reads your /log and the /outputfile dumps — inventory, "
                           + "achievements, faction, spellbook. EQBuddy's rule is local-first: "
                           + "here is the list of hosts it contacts.";
        var bad = Violations(spent, markdown: true, valuesLines: SecurityBoundary);
        Assert.Contains(bad, v => v.Contains("zero telemetry", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("never sends your data", StringComparison.Ordinal));

        // Keeping it is all that was ever being asked.
        const string kept = "EQBuddy reads your /log and the /outputfile dumps — inventory, "
                          + "achievements, faction, spellbook. EQBuddy's rule is local-first, "
                          + "zero telemetry: it never sends your data anywhere on its own.";
        Assert.Empty(Violations(kept, markdown: true, valuesLines: SecurityBoundary));
    }

    /// <summary>The exact bytes DRA-67 removed, and the other ways this has been said —
    /// including README's own pre-DRA-68 phrasing, which no scan for the hyphenated pill
    /// would have caught. Each must be caught, or the guard above is decoration.</summary>
    [Theory]
    [InlineData("""<span class="pill"><b>Log-only</b> — reads your /log file, nothing else</span>""")]
    [InlineData("<p>EQBuddy reads only the log file the game writes.</p>")]
    [InlineData("<h3>Log-only and local-first</h3>")]
    [InlineData("""<meta name="description" content="the personal, log-only companion">""")]
    [InlineData("**Log-only, by principle.** EQBuddy knows only what your own log says.")]
    [InlineData("the same private, log-only companion, finished into one coherent product")]
    // DRA-87's three, verbatim from the pre-change files. All three are LABELS over prose
    // that was already true — PRODUCT.md's and the charter's bullets ("no game-memory
    // reads", "no packet inspection"…) say nothing false, and SECURITY.md's sentence is a
    // correct statement about egress wearing the wrong noun. That is why four content
    // passes and two prior honesty cards walked past them: nothing under the heading was
    // wrong, and the heading is the part a reader quotes back at you.
    [InlineData("### Log-only and local-first")]
    [InlineData("## 2.2 Log-only and local-first")]
    [InlineData("EQBuddy's rule is **log-only, zero telemetry**: it never sends your data "
              + "anywhere on its own.")]
    public void ThePreDra67WordingIsCaught(string text) =>
        Assert.Contains(Violations(text), v => v.StartsWith("claims", StringComparison.Ordinal));

    /// <summary>
    /// The missing-thing half. A page that says nothing false, and also never tells the
    /// player about the dumps, is the state DRA-67 would have left behind if the pill had
    /// simply been deleted — and it is the state a FIFTH dump puts us in tomorrow.
    /// </summary>
    [Theory]
    [InlineData("<p>Local-first. No game-memory reads, and it never measures other players.</p>",
                "the pill was deleted rather than corrected")]
    [InlineData("<p>We read your /log file and the /outputfile dumps — inventory, achievements, faction. "
              + "No game-memory reads; never measures other players.</p>",
                "a dump exists that the page does not name")]
    public void AnUnnamedDumpIsAViolationToo(string html, string why)
    {
        var bad = Violations(html);
        Assert.Contains(bad, v => v.StartsWith("no single paragraph", StringComparison.Ordinal));
        Assert.NotEmpty(why);
    }

    /// <summary>
    /// The short-form surface still has to disclose the dumps. README is excused from
    /// ENUMERATING them, which is a different thing from being excused from mentioning
    /// them — without this arm, "take the short form" would have licensed silence and
    /// DRA-68 would have deleted a false claim while answering nothing.
    /// </summary>
    [Fact]
    public void TheShortFormMustStillDiscloseTheDumps()
    {
        const string silent = "EQBuddy never reads game memory and never measures other players.";
        Assert.Contains(Violations(silent, markdown: true, mustEnumerateDumps: false),
                        v => v.StartsWith("never mentions /outputfile", StringComparison.Ordinal));

        const string honest = "EQBuddy never reads game memory and never measures other players "
                            + "— it knows only what the game writes for you: the /log it tails, "
                            + "and the /outputfile dumps you ask the game for.";
        Assert.Empty(Violations(honest, markdown: true, mustEnumerateDumps: false));
    }

    /// <summary>
    /// A markdown list item is its own paragraph, and that is what keeps "one paragraph
    /// answers in full" from degrading into "the file mentions these words somewhere".
    /// EQBuddy-Evolved.md's "Hard lines" are four bullets with no blank line between them,
    /// so a blank-line-only splitter hands the must-list ONE block containing all of them —
    /// and the scattered fixture below, which answers nothing in any single place, would
    /// pass. The assertion underneath is the demonstration: the whole text does contain
    /// every required word, and the guard still refuses it.
    /// </summary>
    [Fact]
    public void AnAnswerScatteredAcrossBulletsIsNotASingleParagraph()
    {
        const string scattered = """
            - **Local-first.** EQBuddy reads your /log and the /outputfile dumps it asks for.
              No game-memory reads, and it never measures other players.
            - It knows about your inventory and your achievements.
            - It also knows about faction and spellbook.
            """;

        // Every word the must-list looks for IS in the file — just never together.
        var whole = Flatten(scattered);
        Assert.Contains("/log", whole, StringComparison.Ordinal);
        Assert.Contains("/outputfile", whole, StringComparison.Ordinal);
        Assert.All(DumpNames, n => Assert.Contains(n, whole, StringComparison.OrdinalIgnoreCase));

        Assert.Contains(Violations(scattered, markdown: true),
                        v => v.StartsWith("no single paragraph", StringComparison.Ordinal));

        // And one bullet that answers in full is accepted, so the rule is satisfiable.
        const string together = """
            - **Your own files.** EQBuddy reads the /log it tails live, and the /outputfile dumps
              you ask for — inventory, achievements, faction, spellbook. No game-memory reads,
              and it never measures other players.
            - Another hard line that says nothing about sources.
            """;
        Assert.Empty(Violations(together, markdown: true));
    }

    /// <summary>And dropping a values line while rewording the pill is its own failure.</summary>
    [Fact]
    public void TheValuesLinesAreStillRequired()
    {
        var bad = Violations("<p>We read your /log file and the /outputfile dumps — "
                           + "inventory, achievements, faction, spellbook.</p>");
        Assert.Contains(bad, v => v.Contains("game-memory", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("measures other players", StringComparison.Ordinal));
    }

    /// <summary>
    /// The values check is about the CONCEPT, so each surface's own spelling counts. These
    /// are the two variants actually shipped — README's unhyphenated "game memory" and
    /// EQBuddy-Evolved.md's "judge other people" — and a guard that reddened on either
    /// would be asking for a correct sentence to be rewritten.
    /// </summary>
    [Fact]
    public void EachSurfacesOwnSpellingOfTheValuesLineCounts()
    {
        const string readmeSpelling =
            "EQBuddy never reads game memory and never measures other players. "
            + "It reads your /outputfile dumps.";
        Assert.Empty(Violations(readmeSpelling, markdown: true, mustEnumerateDumps: false));

        const string evolvedSpelling =
            "No game-memory reads. It is not a leaderboard, or a way to judge other people. "
            + "It reads your /outputfile dumps.";
        Assert.Empty(Violations(evolvedSpelling, markdown: true, mustEnumerateDumps: false));

        // DRA-87's two, and the reason the pattern was widened rather than the sentences
        // rewritten: both were already shipped, already correct, and say the same thing in
        // the tense their own document is written in.
        const string productSpelling =
            "It does not become a party/raid ranking tool, leaderboard, coaching score, or a "
            + "way to judge other players. No game-memory reads. It reads your /outputfile dumps.";
        Assert.Empty(Violations(productSpelling, markdown: true, mustEnumerateDumps: false));

        const string charterSpelling =
            "It must not become a party/raid ranking tool, leaderboard, coaching score, or "
            + "mechanism for judging other players. No game-memory reads. "
            + "It reads your /outputfile dumps.";
        Assert.Empty(Violations(charterSpelling, markdown: true, mustEnumerateDumps: false));
    }

    /// <summary>
    /// Widening an ACCEPT pattern makes a guard weaker, so the widening gets its own
    /// negative. "judge"/"judging" and "people"/"players" are admitted; a page that names
    /// neither the judging nor the measuring is still caught, and the alternation did not
    /// quietly become a match on "other players" alone.
    /// </summary>
    [Fact]
    public void TheWidenedJudgingPatternStillRefusesASilentPage()
    {
        const string silent = "No game-memory reads. EQBuddy shows you what other players "
                            + "are doing. It reads your /log and /outputfile dumps.";
        Assert.Contains(Violations(silent, markdown: true, mustEnumerateDumps: false),
                        v => v.Contains("measures other players", StringComparison.Ordinal));
    }

    /// <summary>
    /// WHERE A SENTENCE IS WRAPPED IS NOT A CLAIM ABOUT ANYTHING. The fixture is real: it is
    /// the footer from `claude/opus-dra67-landing-honest-20260912`, a competing branch that
    /// fixed the same bug independently and wrapped "never measures / other players" across a
    /// line. The first cut of this guard reported that page as having DROPPED the values line
    /// — a false red on a page that was correct. Kept as a test because the next person to
    /// reflow this paragraph is entitled to a green run.
    /// </summary>
    [Fact]
    public void AWrappedSentenceIsStillTheSentence()
    {
        var wrapped = """
            <p>EQBuddy reads what the game writes on your own PC — your /log, and the /outputfile
            dumps you ask for — inventory, achievements, faction, spellbook. It never reads game
            memory, no game-memory reads, never sends anything off your PC unless you opt in, and never measures
            other players.</p>
            """;
        Assert.Empty(Violations(wrapped));
    }

    /// <summary>
    /// DRA-451 (DRA-382 S2-1). Evolved on main has an opt-in heartbeat, so the landing
    /// may not say "never phones home" — <c>docs/v2/telemetry.md</c> §8.1 removed that
    /// phrase on purpose, because a reader with a network monitor can falsify it. An
    /// unqualified "no cloud" is the same claim. The allowed shape is the page's own
    /// conditional. README dropped the phrase at TEL-PR4 and is checked by
    /// <c>TelemetryPublicCopyTests</c> (its Mobile bullet's scoped "no cloud" is true, so this
    /// scan's second arm is not applied there); LEGACY-V1 keeps it, true of 1.x forever.
    /// </summary>
    [Fact]
    public void TheLandingDoesNotClaimItNeverPhonesHome()
    {
        Assert.Empty(PhonesHomeViolations(Page));
        var flat = Flatten(Page);
        Assert.Contains(
            "never sends anything off your PC unless you opt in",
            flat,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("never phones home", flat, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no cloud", flat, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The prove-fail. The footer and the trust card as they stood on main before DRA-451
    /// are refused, and the replacement sentences are accepted, so the rule is satisfiable
    /// and a green-only scan cannot hide a return of the old phrase (trap 34).
    /// </summary>
    [Fact]
    public void TheOldNeverPhonesHomePhraseIsRefused()
    {
        const string oldFooter = """
            <p>It never reads game memory, never phones home, and never measures other players.</p>
            """;
        Assert.Contains(
            PhonesHomeViolations(oldFooter),
            v => v.Contains("phones home", StringComparison.OrdinalIgnoreCase));

        const string oldCloud = """
            <p>Local-first. No account, no cloud, and nothing sent off your PC unless you opt in.</p>
            """;
        Assert.Contains(
            PhonesHomeViolations(oldCloud),
            v => v.Contains("no cloud", StringComparison.OrdinalIgnoreCase));

        const string kept = """
            <p>It never reads game memory, never sends anything off your PC unless you opt in, and never measures other players.</p>
            <p>Local-first. No account, and nothing sent off your PC unless you opt in.</p>
            """;
        Assert.Empty(PhonesHomeViolations(kept));
    }

    /// <summary>
    /// DRA-451 (DRA-382 S3-1, S3-2). The Route layer is the quest turn-in path that ships.
    /// The evidence card does not promise observed/estimated badges the app does not draw.
    /// </summary>
    [Fact]
    public void TheChainAndEvidenceLinesMatchWhatShips()
    {
        Assert.Contains(
            "How many zones away a quest's turn-in is, and the path there.",
            Page,
            StringComparison.Ordinal);
        Assert.DoesNotContain("The zones between where you stand", Page, StringComparison.Ordinal);
        Assert.Contains(
            "Numbers from your own kills say so, and estimates are marked as estimates.",
            Page,
            StringComparison.Ordinal);
        Assert.DoesNotContain("badge observed", Page, StringComparison.Ordinal);
        Assert.DoesNotContain("badge estimated", Page, StringComparison.Ordinal);
    }

    /// <summary>
    /// DRA-451 (DRA-382 S3-3). At 730px the four section links used to sit in a horizontal
    /// scroller, so GitHub and Support EQBuddy were off-screen. The breakpoint hides those
    /// section links and does not scroll the bar. Brand, GitHub and Support stay.
    /// </summary>
    [Fact]
    public void TheNarrowTopbarHidesSectionLinks()
    {
        var css = File.ReadAllText(Path.Combine(Repo, "site", "assets", "css", "landing.css"));
        Assert.Empty(NarrowTopbarViolations(css));
    }

    /// <summary>The pre-DRA-451 730px rule: a scroller, and every nav link still painted.</summary>
    [Fact]
    public void TheOldScrollingTopbarIsRefused()
    {
        const string old = """
            @media (max-width: 730px) {
              .topbar { max-width: calc(100vw - 20px); overflow-x: auto; }
              .topbar a.nav { padding: 5px 7px; }
            }
            """;
        var bad = NarrowTopbarViolations(old);
        Assert.Contains(bad, v => v.Contains("section nav", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(bad, v => v.Contains("scrolls", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// DRA-69 placed a quiet Support EQBuddy chip in the topbar, opening
    /// https://ko-fi.com/eqbuddy in a new tab. DRA-934 (2026-10-04): Ko-fi
    /// suspended that page, so the anchor is an HTML comment marked
    /// <c>DRA-934 KOFI-SUSPENDED</c>. Restore is un-commenting that anchor and
    /// flipping the live-anchor assertion below back to requiring the chip.
    /// Comments are stripped before the match — a regex over raw HTML still
    /// sees a commented-out anchor and would stay green while proving nothing.
    /// The hero and footer must not carry it. The Stripe Payment Link that used
    /// to sit here is dead and must not return.
    /// </summary>
    [Fact]
    public void TheTopbarCarriesAQuietSupportChip()
    {
        var html = Page;
        // The marker is what a restore card searches for. Deleting the anchor
        // would also hide the link, and that is not the reversible change.
        Assert.Contains("DRA-934 KOFI-SUSPENDED", html, StringComparison.Ordinal);
        Assert.False(
            LiveKofiAnchor(html),
            "DRA-934: a live ko-fi.com anchor is still on the page (comments are stripped before this match)");

        var live = WithoutHtmlComments(html);
        var topbar = Regex.Match(
            live,
            """<nav\s+class="topbar"[^>]*>.*?</nav>""",
            RegexOptions.Singleline);
        Assert.True(topbar.Success, "landing is missing the topbar");
        Assert.DoesNotContain("Support EQBuddy", topbar.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("$5", topbar.Value, StringComparison.Ordinal);

        var hero = Regex.Match(
            html,
            """<section\s+class="hero"[^>]*>.*?</section>""",
            RegexOptions.Singleline);
        Assert.True(hero.Success, "landing is missing the hero section");
        Assert.DoesNotContain("Support EQBuddy", hero.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"support\"", hero.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("ko-fi.com/eqbuddy", hero.Value, StringComparison.Ordinal);

        var footer = Regex.Match(html, """<footer\b.*?</footer>""", RegexOptions.Singleline);
        Assert.True(footer.Success, "landing is missing the footer");
        Assert.DoesNotContain(
            "Support EQBuddy",
            footer.Value,
            StringComparison.Ordinal);

        Assert.DoesNotContain("buy.stripe.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("js.stripe.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stripe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paypal", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// DRA-934 prove-fail. The suspension check reads comment-stripped markup.
    /// An uncommented chip is a hit; the same chip inside a comment is not.
    /// A raw-HTML regex would match both, which is the trap this exists to close.
    /// </summary>
    [Fact]
    public void ACommentedOutKofiChipIsNotALiveAnchor()
    {
        const string uncommented = """
            <a class="nav support" href="https://ko-fi.com/eqbuddy" target="_blank" rel="noopener noreferrer">Support EQBuddy</a>
            """;
        Assert.True(LiveKofiAnchor(uncommented), "an uncommented Ko-fi chip must fail the suspension check");

        const string commented = """
            <!-- DRA-934 KOFI-SUSPENDED: restore by un-commenting this anchor
            <a class="nav support" href="https://ko-fi.com/eqbuddy" target="_blank" rel="noopener noreferrer">Support EQBuddy</a>
            -->
            """;
        Assert.False(LiveKofiAnchor(commented), "a commented-out chip must not count as a live anchor");
    }

    /// <summary>HTML comments removed. A commented-out anchor is not a live link.</summary>
    private static string WithoutHtmlComments(string html) =>
        Regex.Replace(html, "<!--.*?-->", " ", RegexOptions.Singleline);

    /// <summary>True when comment-stripped markup still has an <c>a</c> whose href is on ko-fi.com.</summary>
    private static bool LiveKofiAnchor(string html) =>
        Regex.IsMatch(
            WithoutHtmlComments(html),
            """<a\b[^>]*href="[^"]*ko-fi\.com""",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>
    /// DRA-692. The Windows download label carries no version, and its href is GitHub's
    /// latest-release asset (<c>releases/latest/download/EQBuddyEvolvedSetup.exe</c>) so a
    /// release marked Latest is what the button downloads without a landing edit. That is
    /// the ONE <c>releases/latest</c> URL. Release notes stay tag-pinned to <c>v2.0.0</c>.
    /// The page still says Beta and Windows where it offers the download.
    ///
    /// <para>Founder decision 2026-09-28 pinned every Evolved link, including the installer,
    /// so a later release could not change the download. That pin was still serving v2.0.0
    /// after v2.0.2 was Latest. DRA-692 keeps the pin for notes and for the v1.99.18 Mac /
    /// Linux page, and lifts it for this one asset URL.</para>
    ///
    /// <para>The one 1.x link allowed is the TAG-PINNED <c>v1.99.18</c> release PAGE, only as
    /// the Mac / Linux answer, never styled as a button and never as a Windows download.</para>
    /// </summary>
    [Fact]
    public void TheLandingDownloadTracksTheCurrentRelease()
    {
        Assert.Empty(ReleaseLinkViolations(Page));
        Assert.Equal(2, Regex.Matches(Page, Regex.Escape(CurrentInstaller)).Count);
        Assert.DoesNotContain("releases/download/v2.0.0/", Page, StringComparison.Ordinal);
        Assert.DoesNotContain("0.1", Page, StringComparison.Ordinal);
        Assert.DoesNotContain("coming soon", VisibleProse(Page), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("will be code-signed", VisibleProse(Page), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Every release is code-signed", VisibleProse(Page), StringComparison.Ordinal);
    }

    /// <summary>The rule is satisfiable: the minimal compliant CTA passes it. A rule that nothing
    /// can pass is not a rule (trap 34's other face).</summary>
    [Fact]
    public void TheReleaseLinkRuleAcceptsTheMinimalBetaCta() =>
        Assert.Empty(ReleaseLinkViolations(BetaCta));

    /// <summary>Committed negative: D2's pre-direction variant-B hero, verbatim, trips every arm
    /// it should — an unpinned <c>releases/latest</c> button, 1.x as today's download, and a
    /// v1.99.18 link with no Mac / Linux context.</summary>
    [Fact]
    public void TheReleaseLinkRuleRefusesTheOldVariantBHero()
    {
        const string oldHero = """
            <div class="ctas">
              <a class="btn primary" href="https://github.com/DranakCorps-bot/EQBuddy/releases/latest">Download EQBuddy</a>
            </div>
            <p class="quiet">Evolved v2 arriving — 1.x available today.</p>
            <a href="https://github.com/DranakCorps-bot/EQBuddy/releases/tag/v1.99.18">v1.99.18</a>
            """;
        var bad = ReleaseLinkViolations(oldHero);
        Assert.Contains(bad, v => v.Contains("releases/latest", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("download button", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("1.x", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("Mac / Linux", StringComparison.Ordinal));
    }

    /// <summary>Committed negatives for the Evolved half (DRA-692). The latest-asset URL is the
    /// download; a tag-pinned installer, a bare <c>releases/latest</c> page, another asset on
    /// latest, a version in the button label, unpinned notes, or a CTA that forgets Beta or
    /// Windows is refused. The bare-latest case is also <see cref="TheReleaseLinkRuleRefusesTheOldVariantBHero"/>.</summary>
    [Fact]
    public void AStalePinOrAnotherLatestUrlOrAVersionedLabelIsRefused()
    {
        var pinnedInstaller = ReleaseLinkViolations(BetaCta.Replace(
            "releases/latest/download/EQBuddyEvolvedSetup.exe",
            "releases/download/v2.0.0/EQBuddyEvolvedSetup.exe", StringComparison.Ordinal));
        Assert.Contains(pinnedInstaller, v => v.Contains("download/v2.0.0/EQBuddyEvolvedSetup.exe", StringComparison.Ordinal));

        var bareLatest = ReleaseLinkViolations(BetaCta.Replace(
            "releases/latest/download/EQBuddyEvolvedSetup.exe",
            "releases/latest", StringComparison.Ordinal));
        Assert.Contains(bareLatest, v => v.Contains("releases/latest", StringComparison.Ordinal));

        var wrongAsset = ReleaseLinkViolations(BetaCta.Replace(
            "releases/latest/download/EQBuddyEvolvedSetup.exe",
            "releases/latest/download/EQBuddySetup.exe", StringComparison.Ordinal));
        Assert.Contains(wrongAsset, v => v.Contains("EQBuddySetup.exe", StringComparison.Ordinal));

        var versioned = ReleaseLinkViolations(BetaCta.Replace(
            "Download EQBuddy for Windows",
            "Download EQBuddy Evolved 0.1 Beta", StringComparison.Ordinal));
        Assert.Contains(versioned, v => v.Contains("version", StringComparison.Ordinal));

        var otherTag = ReleaseLinkViolations(BetaCta.Replace(
            "releases/tag/v2.0.0", "releases/tag/v2.0.1", StringComparison.Ordinal));
        Assert.Contains(otherTag, v => v.Contains("v2.0.1", StringComparison.Ordinal));

        var bareReleases = ReleaseLinkViolations(BetaCta.Replace(
            "releases/tag/v2.0.0", "releases", StringComparison.Ordinal));
        Assert.Contains(bareReleases, v => v.Contains("not one of the allowed", StringComparison.Ordinal));

        var buttonToNotes = ReleaseLinkViolations(BetaCta.Replace(
            """href="https://github.com/DranakCorps-bot/EQBuddy/releases/latest/download/EQBuddyEvolvedSetup.exe">Download""",
            """href="https://github.com/DranakCorps-bot/EQBuddy/releases/tag/v2.0.0">Download""", StringComparison.Ordinal));
        Assert.Contains(buttonToNotes, v => v.Contains("download button", StringComparison.Ordinal));

        var noBeta = ReleaseLinkViolations(BetaCta.Replace("<span class=\"beta\">Beta</span>", "", StringComparison.Ordinal));
        Assert.Contains(noBeta, v => v.Contains("Beta", StringComparison.Ordinal));

        var noWindows = ReleaseLinkViolations(BetaCta.Replace("Windows 10/11 only · ", "", StringComparison.Ordinal)
            .Replace("Evolved is Windows-only. ", "", StringComparison.Ordinal));
        Assert.Contains(noWindows, v => v.Contains("Windows", StringComparison.Ordinal));
    }

    /// <summary>Committed negatives for the 1.x half (Founder 2026-09-28): a 1.x INSTALLER link,
    /// an unpinned or other-tag 1.x link, the pinned page dressed as a button, or the pinned page
    /// offered for Windows or without its Mac / Linux context — each is refused.</summary>
    [Fact]
    public void AV1InstallerOrAnUnpinnedOrOutOfContextV1LinkIsRefused()
    {
        var installer = ReleaseLinkViolations(BetaCta.Replace(
            "releases/tag/v1.99.18", "releases/download/v1.99.18/EQBuddySetup.exe", StringComparison.Ordinal));
        Assert.Contains(installer, v => v.Contains("EQBuddySetup.exe", StringComparison.Ordinal));

        var otherV1 = ReleaseLinkViolations(BetaCta.Replace(
            "releases/tag/v1.99.18", "releases/tag/v1.99.17", StringComparison.Ordinal));
        Assert.Contains(otherV1, v => v.Contains("v1.99.17", StringComparison.Ordinal));

        var asButton = ReleaseLinkViolations(BetaCta.Replace(
            """<a href="https://github.com/DranakCorps-bot/EQBuddy/releases/tag/v1.99.18">""",
            """<a class="btn ghost" href="https://github.com/DranakCorps-bot/EQBuddy/releases/tag/v1.99.18">""", StringComparison.Ordinal));
        Assert.Contains(asButton, v => v.Contains("button", StringComparison.Ordinal));

        var forWindows = ReleaseLinkViolations(BetaCta.Replace(
            "Mac / Linux: EQBuddy legacy v1.99.18", "Windows 7: EQBuddy legacy v1.99.18", StringComparison.Ordinal));
        Assert.Contains(forWindows, v => v.Contains("Mac / Linux", StringComparison.Ordinal));
        Assert.Contains(forWindows, v => v.Contains("Windows", StringComparison.Ordinal));
    }

    /// <summary>
    /// DRA-691 / DRA-692. The page may link exactly one community macOS build: Scooffs'
    /// <c>osxeql-buddy</c> release, as a secondary button whose text names Scooffs and macOS.
    /// Both download blocks carry it directly under the Windows button, with the credit
    /// (maintained by Scooffs, not EQBuddy; macOS issues go to his repo; EQBuddy is
    /// Windows-first) in that same row. Windows Evolved stays the only primary button.
    /// </summary>
    [Fact]
    public void TheLandingOffersScooffsMacBuildAsASecondaryButton()
    {
        Assert.Empty(ReleaseLinkViolations(Page));
        Assert.Equal(2, Regex.Matches(Page, $"""class="btn secondary" href="{Regex.Escape(CommunityMacBuild)}""").Count);
        var prose = VisibleProse(Page);
        Assert.Equal(2, Regex.Matches(prose, "community build by Scooffs").Count);
        Assert.Equal(2, Regex.Matches(prose, "maintained by Scooffs, not EQBuddy").Count);
        Assert.Equal(2, Regex.Matches(prose, "Report macOS issues on his repo").Count);
        Assert.Equal(2, Regex.Matches(prose, "EQBuddy development and support are Windows-first").Count);
        Assert.DoesNotContain(
            $"""class="btn primary" href="{CommunityMacBuild}""",
            Page,
            StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(Page, ">Download EQBuddy for Windows<").Count);
    }

    /// <summary>
    /// Committed negative (trap 34/78): the allowlist is one exact URL, and the one allowed
    /// treatment is <c>btn secondary</c>. A different tag on Scooffs' repo, an arbitrary
    /// third-party <c>/releases/</c> URL, the same URL as a primary button or a quiet line,
    /// or link text that does not name Scooffs and macOS are still refused.
    /// </summary>
    [Fact]
    public void ADifferentScooffsReleaseOrAThirdPartyReleasesLinkIsRefused()
    {
        var otherTag = ReleaseLinkViolations(BetaCta.Replace(
            CommunityMacBuild,
            "https://github.com/scoofz/osxEQL/releases/tag/some-other-tag",
            StringComparison.Ordinal));
        Assert.Contains(otherTag, v => v.Contains("some-other-tag", StringComparison.Ordinal));
        Assert.Contains(otherTag, v => v.Contains("not one of the allowed", StringComparison.Ordinal));

        var thirdParty = ReleaseLinkViolations(BetaCta.Replace(
            CommunityMacBuild,
            "https://github.com/example/other/releases/tag/v1",
            StringComparison.Ordinal));
        Assert.Contains(thirdParty, v => v.Contains("example/other", StringComparison.Ordinal));
        Assert.Contains(thirdParty, v => v.Contains("not one of the allowed", StringComparison.Ordinal));

        var asPrimary = ReleaseLinkViolations(BetaCta.Replace(
            $"""<a class="btn secondary" href="{CommunityMacBuild}">""",
            $"""<a class="btn primary" href="{CommunityMacBuild}">""",
            StringComparison.Ordinal));
        Assert.Contains(asPrimary, v => v.Contains("primary button", StringComparison.Ordinal));

        var quiet = ReleaseLinkViolations(BetaCta.Replace(
            $"""<a class="btn secondary" href="{CommunityMacBuild}">""",
            $"""<a href="{CommunityMacBuild}">""",
            StringComparison.Ordinal));
        Assert.Contains(quiet, v => v.Contains("secondary button", StringComparison.Ordinal));

        var unnamed = ReleaseLinkViolations(BetaCta.Replace(
            "macOS: community build by Scooffs",
            "get the build",
            StringComparison.Ordinal));
        Assert.Contains(unnamed, v => v.Contains("Scooffs", StringComparison.Ordinal));
        Assert.Contains(unnamed, v => v.Contains("macOS", StringComparison.Ordinal));
    }

    /// <summary>
    /// Founder, 2026-10-01 (DRA-692). In both download blocks the macOS button is the next
    /// control under the Windows button, inside a column <c>.download-group</c>, and the
    /// Scooffs credit shares <c>.mac-line</c> so it sits beside that button and wraps under
    /// it. A side-by-side CTA row, or a credit paragraph after the buttons, is further down
    /// the page than this asks for.
    /// </summary>
    [Fact]
    public void TheMacButtonSitsDirectlyUnderTheWindowsButton()
    {
        var css = File.ReadAllText(Path.Combine(Repo, "site", "assets", "css", "landing.css"));
        Assert.Empty(DownloadPlacementViolations(Page, css));
        Assert.Equal(2, Regex.Matches(Page, "class=\"download-group\"").Count);
    }

    /// <summary>Committed negatives: the macOS button as a sibling in the horizontal CTA row,
    /// the credit as a paragraph after that row, and a <c>.download-group</c> that is a row
    /// so the source order does not stack. Each is refused on its own.</summary>
    [Fact]
    public void AMacButtonBesideTheWindowsButtonOrACreditFurtherDownIsRefused()
    {
        var css = File.ReadAllText(Path.Combine(Repo, "site", "assets", "css", "landing.css"));
        Assert.Empty(DownloadPlacementViolations(Page, css));

        const string beside = """
            <div class="ctas">
              <a class="btn primary" href="https://github.com/DranakCorps-bot/EQBuddy/releases/latest/download/EQBuddyEvolvedSetup.exe">Download EQBuddy for Windows</a>
              <a class="btn secondary" href="https://github.com/scoofz/osxEQL/releases/tag/osxeql-buddy">macOS: community build by Scooffs</a>
            </div>
            <p class="quiet">maintained by Scooffs, not EQBuddy. Report macOS issues on his repo. EQBuddy development and support are Windows-first.</p>
            """;
        var sideBySide = DownloadPlacementViolations(beside, css);
        Assert.Contains(sideBySide, v => v.Contains("directly under", StringComparison.Ordinal));
        Assert.Contains(sideBySide, v => v.Contains("credit", StringComparison.Ordinal));

        const string creditBelow = """
            <div class="download-group">
              <a class="btn primary" href="https://github.com/DranakCorps-bot/EQBuddy/releases/latest/download/EQBuddyEvolvedSetup.exe">Download EQBuddy for Windows</a>
              <div class="mac-line">
                <a class="btn secondary" href="https://github.com/scoofz/osxEQL/releases/tag/osxeql-buddy">macOS: community build by Scooffs</a>
              </div>
            </div>
            <p class="quiet">Windows 10/11 only</p>
            <p class="quiet">maintained by Scooffs, not EQBuddy. Report macOS issues on his repo. EQBuddy development and support are Windows-first.</p>
            """;
        Assert.Contains(DownloadPlacementViolations(creditBelow, css), v => v.Contains("credit", StringComparison.Ordinal));

        var rowCss = css.Replace("flex-direction: column", "flex-direction: row", StringComparison.Ordinal);
        Assert.NotEqual(css, rowCss);
        Assert.Contains(DownloadPlacementViolations(Page, rowCss), v => v.Contains("column", StringComparison.Ordinal));

        var noWrap = css.Replace("flex-wrap: wrap", "flex-wrap: nowrap", StringComparison.Ordinal);
        Assert.NotEqual(css, noWrap);
        Assert.Contains(DownloadPlacementViolations(Page, noWrap), v => v.Contains("wrap", StringComparison.Ordinal));
    }

    private const string Releases = "https://github.com/DranakCorps-bot/EQBuddy/releases/";

    /// <summary>DRA-692. The Windows download: GitHub's latest-release asset. The only
    /// <c>releases/latest</c> URL the page may carry.</summary>
    private const string CurrentInstaller = Releases + "latest/download/EQBuddyEvolvedSetup.exe";

    /// <summary>The Evolved 0.1 Beta release notes, pinned to its tag.</summary>
    private const string PinnedNotes = Releases + "tag/v2.0.0";

    /// <summary>The ONE 1.x link the page may carry: the legacy release page, for Mac / Linux.</summary>
    private const string PinnedLegacyPage = Releases + "tag/v1.99.18";

    /// <summary>The ONE community macOS link (DRA-691): Scooffs' osxeql-buddy release, exact string.</summary>
    private const string CommunityMacBuild = "https://github.com/scoofz/osxEQL/releases/tag/osxeql-buddy";

    /// <summary>The minimal compliant CTA, shaped like the page's own.</summary>
    private const string BetaCta = """
        <h1>EQBuddy <span class="grad">Evolved</span> <span class="beta">Beta</span></h1>
        <div class="ctas">
          <div class="download-group">
            <a class="btn primary" href="https://github.com/DranakCorps-bot/EQBuddy/releases/latest/download/EQBuddyEvolvedSetup.exe">Download EQBuddy for Windows</a>
            <div class="mac-line">
              <a class="btn secondary" href="https://github.com/scoofz/osxEQL/releases/tag/osxeql-buddy">macOS: community build by Scooffs</a>
              <p class="quiet">maintained by Scooffs, not EQBuddy. Report macOS issues on his repo. EQBuddy development and support are Windows-first.</p>
            </div>
          </div>
          <a class="btn ghost" href="https://github.com/DranakCorps-bot/EQBuddy/releases/tag/v2.0.0">Release notes</a>
        </div>
        <p class="quiet">Windows 10/11 only · Code-signed</p>
        <p class="quiet legacy">Evolved is Windows-only. <a href="https://github.com/DranakCorps-bot/EQBuddy/releases/tag/v1.99.18">Mac / Linux: EQBuddy legacy v1.99.18</a></p>
        """;

    private static readonly Regex Anchor = new(
        """<(?<tag>a|button)\b(?<attrs>[^>]*)>(?<text>.*?)</\k<tag>>""",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>The page with comments, scripts and tags removed — what a visitor reads.</summary>
    private static string VisibleProse(string html)
    {
        var s = Regex.Replace(html, "<!--.*?-->", " ", RegexOptions.Singleline);
        s = Regex.Replace(s, @"<(script|style)\b[^>]*>.*?</\1>", " ", RegexOptions.Singleline);
        return Flatten(Regex.Replace(s, "<[^>]*>", " "));
    }

    internal static IReadOnlyList<string> ReleaseLinkViolations(string html)
    {
        var bad = new List<string>();
        var markup = Regex.Replace(html, "<!--.*?-->", " ", RegexOptions.Singleline);
        var prose = VisibleProse(markup);

        foreach (Match href in Regex.Matches(markup, """"href="(?<u>[^"]*)""""))
        {
            var url = href.Groups["u"].Value;
            // DRA-692: the Windows installer is the one releases/latest URL. Every other
            // latest URL (the release page, another asset, a 1.x installer) stays refused,
            // and so does a tag-pinned Evolved installer — that pin is what went stale.
            if (url.Contains("/releases/latest", StringComparison.OrdinalIgnoreCase) && url != CurrentInstaller)
                bad.Add($"links {url} — releases/latest is allowed only as the Evolved installer asset");
            else if (Regex.IsMatch(url, "/releases(/|$)", RegexOptions.IgnoreCase)
                     && url != CurrentInstaller && url != PinnedNotes && url != PinnedLegacyPage
                     && url != CommunityMacBuild)
                bad.Add($"links {url}, which is not one of the allowed release links (current Evolved installer, v2.0.0 notes, v1.99.18 page for Mac / Linux, Scooffs macOS community build)");
            if (url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && url != CurrentInstaller)
                bad.Add($"links an installer other than the current Evolved one: {url} (a 1.x EQBuddySetup.exe download is never offered, and a tag-pinned Evolved installer goes stale)");
        }

        var installerLinks = 0;
        var notesLinks = 0;
        var communityLinks = 0;
        foreach (Match a in Anchor.Matches(markup))
        {
            var attrs = a.Groups["attrs"].Value;
            var url = Regex.Match(attrs, """"href="(?<u>[^"]*)"""").Groups["u"].Value;
            var text = Flatten(Regex.Replace(a.Groups["text"].Value, "<[^>]*>", " "));
            if (url == CurrentInstaller) installerLinks++;
            if (url == PinnedNotes) notesLinks++;
            if (url == CommunityMacBuild) communityLinks++;

            // An in-page anchor (the topbar's "Download" -> #evolved) is navigation to the CTA,
            // not a download; anything else that says Download must BE the current installer.
            if (text.StartsWith("Download", StringComparison.OrdinalIgnoreCase) && url != CurrentInstaller
                && !url.StartsWith('#'))
                bad.Add($"a download button (\"{text}\") points at {(url.Length == 0 ? "nothing" : url)}, not the current-release installer");

            if (url == CurrentInstaller)
            {
                if (!Regex.IsMatch(attrs, """class="[^"]*\bprimary\b"""))
                    bad.Add("the Windows download is not the primary button");
                if (Regex.IsMatch(text, @"\d"))
                    bad.Add($"the Windows download button (\"{text}\") carries a version number; the label stays version-free so a release does not edit it");
            }

            if (url == PinnedLegacyPage)
            {
                if (Regex.IsMatch(attrs, """class="[^"]*\bbtn\b"""))
                    bad.Add("the v1.99.18 link is styled as a button; the legacy release is a quiet Mac / Linux line, never a CTA");
                if (!text.Contains("Mac / Linux", StringComparison.Ordinal))
                    bad.Add($"the v1.99.18 link (\"{text}\") does not say Mac / Linux — 1.x is linked only as the answer for those platforms");
                if (text.Contains("Windows", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Download", StringComparison.OrdinalIgnoreCase))
                    bad.Add($"the v1.99.18 link (\"{text}\") is offered as a Windows download; Windows gets Evolved");
            }

            if (url == CommunityMacBuild)
            {
                var primary = Regex.IsMatch(attrs, """class="[^"]*\bprimary\b""");
                var classAttr = Regex.Match(attrs, """class="([^"]*)""").Groups[1].Value;
                var secondary = Regex.IsMatch(classAttr, @"\bbtn\b") && Regex.IsMatch(classAttr, @"\bsecondary\b");
                if (primary)
                    bad.Add("the community macOS link is styled as a primary button; Windows Evolved stays the only primary download");
                else if (!secondary)
                    bad.Add("the community macOS link is not a secondary button; a quiet line is too easy to miss");
                if (!text.Contains("Scooffs", StringComparison.Ordinal) || !text.Contains("macOS", StringComparison.Ordinal))
                    bad.Add($"the community macOS link (\"{text}\") does not name Scooffs and macOS");
            }
        }

        if (installerLinks == 0)
            bad.Add("no link to the current Evolved installer");
        if (notesLinks == 0)
            bad.Add("no link to the pinned v2.0.0 release notes");
        if (communityLinks == 0)
            bad.Add("no link to Scooffs' macOS community build");
        if (Regex.IsMatch(prose, @"1\.x available", RegexOptions.IgnoreCase))
            bad.Add("offers 1.x as today's download");
        if (!Regex.IsMatch(prose, @"\bBeta\b", RegexOptions.CultureInvariant))
            bad.Add("does not say Beta — EQBuddy Evolved 0.1 is a beta and the page must mark it");
        if (!prose.Contains("Windows 10/11", StringComparison.Ordinal) || !prose.Contains("Windows-only", StringComparison.Ordinal))
            bad.Add("does not say the download is for Windows 10/11 and that Evolved is Windows-only");
        return bad;
    }

    /// <summary>
    /// DRA-692 placement. Each Windows download button is followed immediately by the macOS
    /// row, and that row holds the credit. The column rule is what makes source order a
    /// stack; the wrap rule is what lets the credit sit beside the button or under it.
    /// </summary>
    internal static IReadOnlyList<string> DownloadPlacementViolations(string html, string css)
    {
        var bad = new List<string>();
        var markup = Regex.Replace(html, "<!--.*?-->", " ", RegexOptions.Singleline);
        var primaries = Regex.Matches(
            markup,
            $"""<a class="btn primary" href="{Regex.Escape(CurrentInstaller)}">Download EQBuddy for Windows</a>""");
        if (primaries.Count == 0)
            bad.Add("no Windows download button to place the macOS button under");

        foreach (Match primary in primaries)
        {
            var before = markup[..primary.Index];
            if (!Regex.IsMatch(before, """<div class="download-group">\s*$"""))
                bad.Add("the Windows and macOS buttons are not in the same download group");

            var after = markup[(primary.Index + primary.Length)..];
            var row = Regex.Match(after, """^\s*<div class="mac-line">(?<inner>.*?)</div>""", RegexOptions.Singleline);
            if (!row.Success)
            {
                bad.Add("the macOS button is not directly under the Windows download button");
                bad.Add("the Scooffs credit is not beside or under the macOS button");
                continue;
            }

            var inner = row.Groups["inner"].Value;
            var buttonAt = inner.IndexOf($"class=\"btn secondary\" href=\"{CommunityMacBuild}\"", StringComparison.Ordinal);
            var creditAt = inner.IndexOf("maintained by Scooffs, not EQBuddy", StringComparison.Ordinal);
            if (buttonAt < 0)
                bad.Add("the macOS button is not directly under the Windows download button");
            if (creditAt < 0 || (buttonAt >= 0 && creditAt < buttonAt))
                bad.Add("the Scooffs credit is not beside or under the macOS button");
        }

        var groupRule = Regex.Match(css, @"\.download-group\s*\{(?<b>[^}]*)\}");
        var groupBody = groupRule.Success ? groupRule.Groups["b"].Value : "";
        if (!groupRule.Success || !groupBody.Contains("display: flex", StringComparison.Ordinal)
            || !groupBody.Contains("flex-direction: column", StringComparison.Ordinal))
            bad.Add(".download-group is not a column, so the macOS button does not sit under the Windows button");

        var macRule = Regex.Match(css, @"\.mac-line\s*\{(?<b>[^}]*)\}");
        var macBody = macRule.Success ? macRule.Groups["b"].Value : "";
        if (!macRule.Success || !macBody.Contains("flex-wrap: wrap", StringComparison.Ordinal))
            bad.Add(".mac-line does not wrap, so the Scooffs credit cannot sit beside or under the macOS button");

        return bad;
    }

    /// <summary>
    /// DRA-451. "never phones home" and an unqualified "no cloud" are false of Evolved
    /// once the opt-in heartbeat exists. The scan is the landing's text, not README.
    /// </summary>
    internal static IReadOnlyList<string> PhonesHomeViolations(string html)
    {
        var bad = new List<string>();
        var flat = Flatten(html);
        if (Regex.IsMatch(flat, @"never phones home", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            bad.Add("claims \"never phones home\" — an opted-in heartbeat sends off the PC");
        if (Regex.IsMatch(flat, @"\bno cloud\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            bad.Add("claims \"no cloud\" without an opt-in condition on that claim");
        return bad;
    }

    /// <summary>
    /// DRA-451. The 730px breakpoint must hide the four section links and must not
    /// scroll the topbar. GitHub (<c>.gh</c>) and Support (<c>.support</c>) stay.
    /// </summary>
    internal static IReadOnlyList<string> NarrowTopbarViolations(string css)
    {
        var bad = new List<string>();
        var block = Regex.Match(
            css,
            @"@media\s*\(max-width:\s*730px\)\s*\{(?<body>(?:[^{}]|\{[^{}]*\})*)\}",
            RegexOptions.CultureInvariant);
        if (!block.Success)
        {
            bad.Add("no 730px breakpoint");
            return bad;
        }

        var body = block.Groups["body"].Value;
        if (!Regex.IsMatch(
                body,
                @"\.topbar a\.nav:not\(\.gh\):not\(\.support\)\s*\{[^}]*display:\s*none",
                RegexOptions.CultureInvariant))
            bad.Add("the 730px breakpoint does not hide the section nav links");
        if (Regex.IsMatch(body, @"\.topbar\s*\{[^}]*overflow-x:\s*auto", RegexOptions.CultureInvariant))
            bad.Add("the 730px topbar still scrolls horizontally");
        return bad;
    }

    /// <summary>
    /// <para>History, so the negatives below read as what they are. Founder ask 2026-09-22,
    /// narrowed by DRA-373 D2 and re-widened by DRA-378: the hero KPI band used to wear four
    /// principle zeros (0 game-memory reads, 0 accounts, 0 telemetry by default, 11,000+
    /// catalog); 2026-09-22 made it four measured stats, DRA-373 D2 cut it to the two CONTENT
    /// facts, DRA-378 brought an all-versions "EQBuddy Downloads" tile back, and the morning of
    /// 2026-09-28 added a SECOND strip of five live opt-in telemetry tiles under its own
    /// heading.</para>
    ///
    /// <para><b>Founder, 2026-09-28 afternoon: "looks bad with two sets of stats".</b> The hero
    /// now has ONE strip of exactly seven tiles, in this order: Quests in the guide, Items
    /// cataloged (both static, from <c>site/metrics.json</c>, and the counts are the shipped
    /// arrays themselves, so a refresh that moves the file without moving the JSON goes red
    /// here), then Total installs, Hours used, Peak daily users, Peak weekly active and Peak
    /// concurrent (live, shipped as dashes, painted only from the deploy's same-origin
    /// <c>live.json</c>). The all-versions downloads tile is gone — it represented v1. One
    /// caption under the strip carries the scope; there is no second heading.</para>
    ///
    /// <para><b>Founder, 2026-09-29: "Instead of total installs, I would like to show
    /// downloads"</b> — and, asked which, <b>Evolved</b> downloads. Tile 3 is now
    /// <c>data-live="evolvedDownloads"</c>, "Evolved downloads": the EQBuddyEvolvedSetup.exe
    /// download count across every release, walked hourly by the same deploy into its own
    /// half of <c>live.json</c>. The caption says it counts downloads, not people (TEL-005's
    /// honesty line). <c>installsAllTime</c> joins the retired live keys.</para>
    /// </summary>
    [Fact]
    public void TheHeroStripIsTheSevenTilesInOrder()
    {
        using var metricsDoc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Repo, "site", "metrics.json")));
        var metrics = metricsDoc.RootElement;

        Assert.Empty(StripViolations(Page, metrics));
        Assert.Empty(MetricsViolations(metrics));

        Assert.Equal(QuestArrayCount(), metrics.GetProperty("questsTracked").GetInt32());
        Assert.Equal(ItemArrayCount(), metrics.GetProperty("itemsCataloged").GetInt32());

        // The painter reads the two same-origin files, hard-codes no figure, and reads the
        // Evolved downloads tile from live.json's downloads half — never metrics.json's
        // all-versions "downloads" record, which nothing draws.
        var js = File.ReadAllText(Path.Combine(Repo, "site", "assets", "js", "landing.js"));
        Assert.Contains("\"metrics.json\"", js, StringComparison.Ordinal);
        Assert.Contains("\"live.json\"", js, StringComparison.Ordinal);
        Assert.Contains("\"evolvedDownloads\"", js, StringComparison.Ordinal);
        Assert.Contains("live.downloads", js, StringComparison.Ordinal);
        Assert.DoesNotContain("maxConcurrentUsers", js, StringComparison.Ordinal);
        Assert.DoesNotContain("metrics.downloads", js, StringComparison.Ordinal);
        foreach (var figure in new[] { "28462", "37676", "37759", "38181", "419", "421", "1173", "11196", "11230" })
            Assert.DoesNotContain(figure, js, StringComparison.Ordinal);
    }

    /// <summary>
    /// The pre-change band, kept as a committed negative. A guard that only checks
    /// for the new labels cannot see these phrases come back (trap 34).
    /// </summary>
    [Fact]
    public void TheRetiredZeroKpiBandIsRefused()
    {
        const string old = """
            <div class="kpis reveal">
              <div class="kpi"><div class="n">0</div><div class="l">game-memory reads — ever</div></div>
              <div class="kpi"><div class="n">0</div><div class="l">accounts or cloud services required</div></div>
              <div class="kpi"><div class="n">0</div><div class="l">telemetry by default</div></div>
              <div class="kpi"><div class="n">11,000+</div><div class="l">items in the built-in offline catalog</div></div>
            </div>
            """;

        using var metrics = JsonDocument.Parse(ShippedMetricsJson);
        var bad = StripViolations(old, metrics.RootElement);
        Assert.Contains(bad, v => v.Contains("game-memory reads", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("telemetry by default", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("accounts or cloud services required", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("built-in offline catalog", StringComparison.Ordinal));
    }

    /// <summary>
    /// DRA-373's committed negative, still refused: the four-tile band the page shipped until
    /// D2, verbatim — the telemetry tile the brief removed, and a downloads tile. The seven-tile
    /// strip is accepted, so the rule is satisfiable.
    /// </summary>
    [Fact]
    public void ThePreDra373FourTileBandIsRefused()
    {
        using var metrics = JsonDocument.Parse(ShippedMetricsJson);

        var oldBand = StripViolations(FourTileBand, metrics.RootElement);
        Assert.Contains(oldBand, v => v.Contains("draws the maxConcurrentUsers tile", StringComparison.Ordinal));
        Assert.Contains(oldBand, v => v.Contains("downloads tile is retired", StringComparison.Ordinal));

        Assert.Empty(StripViolations(SevenTileStrip, metrics.RootElement));
    }

    /// <summary>
    /// Founder, 2026-09-28: the "EQBuddy Downloads" tile represented v1, and it is dropped. Every
    /// wording it has ever shipped in is refused — the DRA-378 all-versions tile as it stood
    /// that morning, the pre-378 bare "Downloads", the all-versions KEY under a label naming
    /// Evolved — and so is an all-versions tile bolted onto an otherwise-correct strip. What
    /// 2026-09-29 added is exactly one downloads tile, <c>data-live="evolvedDownloads"</c>,
    /// "Evolved downloads", in tile 3; relabelled as anything else it is refused too.
    /// </summary>
    [Fact]
    public void AnAllVersionsDownloadsTileIsRefused()
    {
        using var metrics = JsonDocument.Parse(ShippedMetricsJson);

        const string evolvedLabelling = """
            <div class="kpis reveal" id="hero-kpis">
              <div class="kpi"><div class="n" data-metric="questsTracked">1,173</div><div class="l">Quests Tracked</div></div>
              <div class="kpi"><div class="n" data-metric="itemsCataloged">11,196</div><div class="l">Items Cataloged</div></div>
              <div class="kpi"><div class="n" data-metric="downloads">37,676</div><div class="l">Evolved Downloads</div><div class="note">all versions · installer downloads</div></div>
            </div>
            """;
        var eighth = SevenTileStrip.Replace(
            """<div class="l">Peak concurrent</div></div>""",
            """<div class="l">Peak concurrent</div></div><div class="kpi"><div class="n" data-live="downloads">—</div><div class="l">EQBuddy Downloads</div></div>""",
            StringComparison.Ordinal);
        Assert.NotEqual(SevenTileStrip, eighth);

        var relabelled = SevenTileStrip.Replace(
            """<div class="l">Evolved downloads</div>""", """<div class="l">EQBuddy Downloads</div>""",
            StringComparison.Ordinal);
        Assert.NotEqual(SevenTileStrip, relabelled);

        foreach (var band in new[] { ThreeTileStrip, BareDownloadsTileBand, evolvedLabelling, eighth, relabelled })
            Assert.Contains(StripViolations(band, metrics.RootElement),
                v => v.Contains("downloads tile is retired", StringComparison.Ordinal));

        // And the rule is satisfiable: the shipped strip's Evolved downloads tile passes.
        Assert.Empty(StripViolations(SevenTileStrip, metrics.RootElement));
    }

    /// <summary>
    /// The hero exactly as it shipped the morning of 2026-09-28 — the three-tile strip, then a
    /// second "EQBuddy Evolved, live" heading over five more tiles and their caption. The
    /// Founder's "two sets of stats", kept verbatim as the committed negative: it is refused for
    /// the second strip, its heading, the downloads tile, and the live figures it drew that the
    /// one strip does not.
    /// </summary>
    [Fact]
    public void TheTwoStripHeroIsRefused()
    {
        using var metrics = JsonDocument.Parse(ShippedMetricsJson);
        var bad = StripViolations(TwoStripHero, metrics.RootElement);

        Assert.Contains(bad, v => v.Contains("second stat strip", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("second heading", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("downloads tile is retired", StringComparison.Ordinal));
        foreach (var retired in RetiredLiveKeys)
            Assert.Contains(bad, v => v.Contains(retired, StringComparison.Ordinal) && v.Contains("no longer drawn", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("expected 7 tiles", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>maxConcurrentUsers</c> is not a figure the telemetry worker publishes at all (its
    /// keys are <c>concurrentNow</c>/<c>peakConcurrent</c>), so an integer under it is invented
    /// whatever the backend says. And no worker figure may be COMMITTED in <c>metrics.json</c>:
    /// live telemetry reaches the public site only through the hourly deploy's
    /// <c>live.json</c>, which validates and dates it.
    /// </summary>
    [Fact]
    public void AFabricatedConcurrentIntegerOrACommittedTelemetryFigureIsRefused()
    {
        using var invented = JsonDocument.Parse("""
            {
              "questsTracked": 1173,
              "itemsCataloged": 11196,
              "downloads": 28462,
              "maxConcurrentUsers": 128
            }
            """);
        Assert.Contains(MetricsViolations(invented.RootElement),
            v => v.Contains("fabricated integer", StringComparison.Ordinal));

        using var committedTelemetry = JsonDocument.Parse("""
            { "questsTracked": 1173, "weeklyActive": 1, "scope": {} }
            """);
        Assert.Contains(MetricsViolations(committedTelemetry.RootElement),
            v => v.Contains("weeklyActive", StringComparison.Ordinal) && v.Contains("live.json", StringComparison.Ordinal));

        using var unpublished = JsonDocument.Parse(ShippedMetricsJson);
        Assert.Empty(MetricsViolations(unpublished.RootElement));
    }

    /// <summary>
    /// "not uniques" was the honesty line on the old downloads tile. A strip or caption that
    /// calls anything on it unique players is still refused.
    /// </summary>
    [Fact]
    public void CallingACountUniquesIsRefused()
    {
        var strip = SevenTileStrip.Replace("Hours used, peak daily users", "Unique players, peak daily users", StringComparison.Ordinal);
        Assert.NotEqual(SevenTileStrip, strip);
        using var metrics = JsonDocument.Parse(ShippedMetricsJson);
        Assert.Contains(StripViolations(strip, metrics.RootElement),
            v => v.Contains("unique", StringComparison.Ordinal));
    }

    /// <summary>
    /// Founder decision 2026-09-28. The five live tiles are in the committed page, and not one
    /// of their figures is: every tile ships as a dash, and <c>landing.js</c> paints them from
    /// the same-origin <c>live.json</c> the hourly deploy writes. So a local build, a failed
    /// fetch or a stale file shows "unavailable", never a number somebody typed. Read straight
    /// off the page, independent of <see cref="StripViolations"/>.
    /// </summary>
    [Fact]
    public void TheLiveTilesCommitNoFigure()
    {
        var markup = Regex.Replace(Page, "<!--.*?-->", " ", RegexOptions.Singleline);
        var live = Regex.Matches(markup, """data-live="(?<key>[^"]+)">(?<n>[^<]*)<""");
        Assert.Equal(
            StripTiles.Where(t => t.Live).Select(t => t.Key),
            live.Select(m => m.Groups["key"].Value));
        Assert.All(live, m => Assert.Equal("—", m.Groups["n"].Value.Trim()));
    }

    /// <summary>Committed negatives for the strip's live half and its one caption: a typed-in
    /// number, a caption without its lower-bound / opt-in / hourly scope, one that forgets a
    /// figure or its as-of node, no caption at all, the tiles reordered, and a live tile swapped
    /// back to a figure the strip retired — each is refused.</summary>
    [Fact]
    public void ALiveTileWithACommittedNumberOrACaptionWithoutItsScopeIsRefused()
    {
        using var metricsDoc = JsonDocument.Parse(ShippedMetricsJson);
        var metrics = metricsDoc.RootElement;
        IReadOnlyList<string> Mutant(string from, string to)
        {
            var mutated = SevenTileStrip.Replace(from, to, StringComparison.Ordinal);
            Assert.NotEqual(SevenTileStrip, mutated);
            return StripViolations(mutated, metrics);
        }

        Assert.Empty(StripViolations(SevenTileStrip, metrics));

        Assert.Contains(Mutant("""data-live="peakWeeklyActive">—""", """data-live="peakWeeklyActive">13"""),
            v => v.Contains("peakWeeklyActive", StringComparison.Ordinal) && v.Contains("commits", StringComparison.Ordinal));
        Assert.Contains(Mutant("lower bound", "floor"), v => v.Contains("lower bound", StringComparison.Ordinal));
        Assert.Contains(Mutant("opted-in Evolved installs only", "Evolved installs"), v => v.Contains("opted-in", StringComparison.Ordinal));
        Assert.Contains(Mutant("Updated hourly.", ""), v => v.Contains("hourly", StringComparison.Ordinal));
        Assert.Contains(Mutant("and peak concurrent count", "count"), v => v.Contains("Peak concurrent", StringComparison.Ordinal));
        Assert.Contains(Mutant("downloads, not people", "downloads"), v => v.Contains("not people", StringComparison.Ordinal));
        Assert.Contains(Mutant("""<span id="live-asof">Not available right now.</span>""", ""), v => v.Contains("live-asof", StringComparison.Ordinal));

        var reordered = SevenTileStrip.Replace("""data-live="peakDailyActive">""", "__d__", StringComparison.Ordinal)
            .Replace("""data-live="peakWeeklyActive">""", """data-live="peakDailyActive">""", StringComparison.Ordinal)
            .Replace("__d__", """data-live="peakWeeklyActive">""", StringComparison.Ordinal);
        Assert.NotEqual(SevenTileStrip, reordered);
        Assert.Contains(StripViolations(reordered, metrics), v => v.Contains("tile 5", StringComparison.Ordinal));

        foreach (var retired in RetiredLiveKeys.Append("installsAllTime"))
            Assert.Contains(Mutant("""data-live="evolvedDownloads">""", $"""data-live="{retired}">"""),
                v => v.Contains(retired, StringComparison.Ordinal) && v.Contains("no longer drawn", StringComparison.Ordinal));

        var noCaption = Regex.Replace(SevenTileStrip, """<p class="quiet livecap">.*?</p>""", "", RegexOptions.Singleline);
        Assert.NotEqual(SevenTileStrip, noCaption);
        Assert.Contains(StripViolations(noCaption, metrics), v => v.Contains("caption", StringComparison.Ordinal));

        Assert.Contains(StripViolations("<p>no strip</p>", metrics), v => v.Contains("no stat strip", StringComparison.Ordinal));
    }

    /// <summary>
    /// Founder, 2026-09-28: every number centred over its label, tiles of equal width, wrapping
    /// gracefully (7 across, 4 + 3, then 2 per row with the last centred). Read off the shipped
    /// stylesheet: the strip wraps and centres its rows, and each tile centres its contents and
    /// does not grow, so a short last row keeps the width of the rows above it.
    /// </summary>
    [Fact]
    public void TheStripCentresEveryTile()
    {
        var css = File.ReadAllText(Path.Combine(Repo, "site", "assets", "css", "landing.css"));
        Assert.Empty(StripCssViolations(css));
    }

    /// <summary>The committed negative: the three-tile grid the strip replaced, which left
    /// every number flush left and could only ever hold one row of three.</summary>
    [Fact]
    public void TheOldLeftAlignedGridStripIsRefused()
    {
        const string old = """
            .kpis {
              display: grid;
              grid-template-columns: repeat(3, 1fr);
              gap: 1px;
              max-width: 600px;
            }
            .kpi { background: var(--panel); padding: 18px 20px; }
            """;
        var bad = StripCssViolations(old);
        Assert.Contains(bad, v => v.Contains("text-align: center", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("flex-wrap", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("justify-content: center", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("equal", StringComparison.Ordinal));
    }

    /// <summary>The committed <c>site/live.json</c> is explicitly unavailable and names no
    /// figure in EITHER half — so whatever publishes it without the hourly step's output
    /// publishes "unavailable", not a number. The downloads half came back 2026-09-29 with the
    /// Evolved downloads tile.</summary>
    [Fact]
    public void TheCommittedLiveFileCarriesNoFigure()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo, "site", "live.json")));
        Assert.Empty(CommittedLiveFileViolations(doc.RootElement));

        using var withFigure = JsonDocument.Parse("""
            { "schema": 1, "generatedAt": "2026-09-28T19:00:00Z",
              "telemetry": { "available": true, "asOf": "2026-09-28T19:00:00Z", "peakConcurrent": 13 },
              "downloads": { "available": false, "reason": "y" } }
            """);
        Assert.Contains(CommittedLiveFileViolations(withFigure.RootElement), v => v.Contains("telemetry", StringComparison.Ordinal));

        using var withDownloadsFigure = JsonDocument.Parse("""
            { "schema": 1, "generatedAt": null,
              "telemetry": { "available": false, "reason": "x" },
              "downloads": { "available": true, "asOf": "2026-09-29T19:00:00Z", "evolvedDownloads": 421 } }
            """);
        Assert.Contains(CommittedLiveFileViolations(withDownloadsFigure.RootElement), v => v.Contains("downloads", StringComparison.Ordinal));

        // The committed file as it stood 2026-09-28 to 29: no downloads half, so a deploy that
        // could not run the script would publish a strip with no answer for tile 3.
        using var noDownloadsHalf = JsonDocument.Parse("""
            { "schema": 1, "generatedAt": null, "telemetry": { "available": false, "reason": "x" } }
            """);
        Assert.Contains(CommittedLiveFileViolations(noDownloadsHalf.RootElement), v => v.Contains("downloads", StringComparison.Ordinal));
    }

    /// <summary>
    /// The mechanism, read off the workflow (Founder decision 2026-09-28): an hourly schedule;
    /// the generator writes <c>site/live.json</c> into the workspace BEFORE the Pages artifact is
    /// uploaded; its step cannot fail the deploy; and the job can commit nothing.
    /// </summary>
    [Fact]
    public void ThePagesWorkflowWritesLiveFiguresIntoTheArtifactOnly()
    {
        Assert.Empty(PagesWorkflowViolations(PagesYaml));
    }

    /// <summary>Committed negatives for the workflow rule: a job that can push, a generator
    /// that runs after the upload (so its file never ships), one that can fail the deploy, and
    /// no schedule — each is refused.</summary>
    [Fact]
    public void APagesWorkflowThatCommitsOrCanFailOnTheFetchIsRefused()
    {
        var yaml = PagesYaml;

        var pushes = PagesWorkflowViolations(yaml.Replace("contents: read", "contents: write", StringComparison.Ordinal)
            + "\n      - run: git commit -am live && git push\n");
        Assert.Contains(pushes, v => v.Contains("contents: write", StringComparison.Ordinal));
        Assert.Contains(pushes, v => v.Contains("git push", StringComparison.Ordinal));

        var step = Regex.Match(yaml, @"      - name: Live figures.*?-OutFile site/live\.json\n", RegexOptions.Singleline).Value;
        Assert.False(string.IsNullOrEmpty(step), "could not find the live-figures step to move");
        var late = PagesWorkflowViolations(yaml.Replace(step, "", StringComparison.Ordinal)
            .Replace("      - id: deployment", step + "      - id: deployment", StringComparison.Ordinal));
        Assert.Contains(late, v => v.Contains("before", StringComparison.Ordinal));

        var fatal = PagesWorkflowViolations(yaml.Replace("continue-on-error: true", "continue-on-error: false", StringComparison.Ordinal));
        Assert.Contains(fatal, v => v.Contains("continue-on-error", StringComparison.Ordinal));

        var unscheduled = PagesWorkflowViolations(Regex.Replace(yaml, @"  schedule:\n    - cron: '[^']*'\n", ""));
        Assert.Contains(unscheduled, v => v.Contains("schedule", StringComparison.Ordinal));
    }

    /// <summary>pages.yml with its line endings normalised, so an autocrlf checkout reads the same.</summary>
    private static string PagesYaml =>
        File.ReadAllText(Path.Combine(Repo, ".github", "workflows", "pages.yml"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private const string ShippedMetricsJson = """
        {
          "questsTracked": 1173,
          "itemsCataloged": 11196,
          "downloads": 37676
        }
        """;

    /// <summary>The DRA-378 hero: three tiles, the third an all-versions downloads total. It
    /// shipped until the one-strip hero of 2026-09-28 retired the downloads tile.</summary>
    private const string ThreeTileStrip = """
        <div class="kpis reveal" id="hero-kpis">
          <div class="kpi"><div class="n" data-metric="questsTracked">1,173</div><div class="l">Quests in the guide</div></div>
          <div class="kpi"><div class="n" data-metric="itemsCataloged">11,196</div><div class="l">Items Cataloged</div></div>
          <div class="kpi"><div class="n" data-metric="downloads">37,676</div><div class="l">EQBuddy Downloads</div><div class="note">all versions · installer downloads</div></div>
        </div>
        """;

    /// <summary>
    /// The pre-DRA-378 shape of the downloads tile: the number, the KEY and the note are
    /// all there; only the label is a bare "Downloads".
    /// </summary>
    private const string BareDownloadsTileBand = """
        <div class="kpis reveal" id="hero-kpis">
          <div class="kpi"><div class="n" data-metric="questsTracked">1,173</div><div class="l">Quests Tracked</div></div>
          <div class="kpi"><div class="n" data-metric="itemsCataloged">11,196</div><div class="l">Items Cataloged</div></div>
          <div class="kpi"><div class="n" data-metric="downloads">37,676</div><div class="l">Downloads</div><div class="note">installer, not uniques</div></div>
        </div>
        """;

    /// <summary>The hero band as it shipped from 2026-09-22 until DRA-373 D2.</summary>
    private const string FourTileBand = """
        <div class="kpis reveal" id="hero-kpis">
          <div class="kpi"><div class="n" data-metric="questsTracked">1,173</div><div class="l">Quests Tracked</div></div>
          <div class="kpi"><div class="n" data-metric="itemsCataloged">11,196</div><div class="l">Items Cataloged</div></div>
          <div class="kpi"><div class="n" data-metric="downloads">28,462</div><div class="l">Downloads</div><div class="note">installer, not uniques</div></div>
          <div class="kpi"><div class="n" data-metric="maxConcurrentUsers">Telemetry not live yet</div><div class="l">Max Concurrent Users</div><div class="note">max concurrent (opt-in)</div></div>
        </div>
        """;

    /// <summary>The hero as it shipped the morning of 2026-09-28, verbatim: the three-tile strip
    /// and, under it, a second headed strip of five live tiles. The Founder's "two sets of
    /// stats".</summary>
    private const string TwoStripHero = """
        <div class="kpis reveal" id="hero-kpis">
          <div class="kpi"><div class="n" data-metric="questsTracked">1,173</div><div class="l">Quests in the guide</div></div>
          <div class="kpi"><div class="n" data-metric="itemsCataloged">11,196</div><div class="l">Items Cataloged</div></div>
          <div class="kpi"><div class="n" data-metric="downloads">37,759</div><div class="l">EQBuddy Downloads</div><div class="note">all versions · installer downloads · as of 28 Sep 2026</div></div>
        </div>

        <div class="livestats reveal">
          <p class="livehead"><span class="beta">Beta</span> EQBuddy Evolved, live</p>
          <div class="kpis live" id="live-kpis">
            <div class="kpi"><div class="n" data-live="uniqueUsers30d">—</div><div class="l">Unique installs (last 30 days)</div></div>
            <div class="kpi"><div class="n" data-live="usageHoursAllTime">—</div><div class="l">Usage hours (all time)</div></div>
            <div class="kpi"><div class="n" data-live="dailyActive">—</div><div class="l">Daily active</div></div>
            <div class="kpi"><div class="n" data-live="weeklyActive">—</div><div class="l">Weekly active</div></div>
            <div class="kpi"><div class="n" data-live="peakConcurrent">—</div><div class="l">Peak concurrent</div></div>
          </div>
          <p class="quiet livecap">Opted-in Evolved installs only — telemetry is off unless a
          player turns it on, so every figure is a lower bound. Updated hourly.
          <span id="live-asof">Not available right now.</span></p>
        </div>
        """;

    /// <summary>The one strip as the page ships it (Founder, 2026-09-28): two content facts,
    /// five live dashes, and the one scoped caption.</summary>
    private const string SevenTileStrip = """
        <div class="stats reveal">
          <div class="kpis" id="hero-kpis">
            <div class="kpi"><div class="n" data-metric="questsTracked">1,173</div><div class="l">Quests in the guide</div></div>
            <div class="kpi"><div class="n" data-metric="itemsCataloged">11,196</div><div class="l">Items cataloged</div></div>
            <div class="kpi"><div class="n" data-live="evolvedDownloads">—</div><div class="l">Evolved downloads</div></div>
            <div class="kpi"><div class="n" data-live="usageHoursAllTime">—</div><div class="l">Hours used</div></div>
            <div class="kpi"><div class="n" data-live="peakDailyActive">—</div><div class="l">Peak daily users</div></div>
            <div class="kpi"><div class="n" data-live="peakWeeklyActive">—</div><div class="l">Peak weekly active</div></div>
            <div class="kpi"><div class="n" data-live="peakConcurrent">—</div><div class="l">Peak concurrent</div></div>
          </div>
          <p class="quiet livecap">Evolved downloads counts every Evolved installer downloaded from
          GitHub, updates included — downloads, not people. Hours used, peak daily users, peak
          weekly active and peak concurrent count opted-in Evolved installs only — telemetry is off
          unless a player turns it on, so each is a lower bound. Updated hourly.
          <span id="live-asof">Not available right now.</span></p>
        </div>
        """;

    /// <summary>The hero's one strip, in order (Founder, 2026-09-28). <c>Live</c> tiles are
    /// <c>data-live</c> and ship as a dash; the rest are <c>data-metric</c> and paint the
    /// committed metrics.json value.</summary>
    private static readonly (bool Live, string Key, string Label)[] StripTiles =
    [
        (false, "questsTracked", "Quests in the guide"),
        (false, "itemsCataloged", "Items cataloged"),
        (true, "evolvedDownloads", "Evolved downloads"),
        (true, "usageHoursAllTime", "Hours used"),
        (true, "peakDailyActive", "Peak daily users"),
        (true, "peakWeeklyActive", "Peak weekly active"),
        (true, "peakConcurrent", "Peak concurrent"),
    ];

    /// <summary>Live figures the morning's second strip drew and the one strip does not.</summary>
    private static readonly string[] RetiredLiveKeys = ["uniqueUsers30d", "dailyActive", "weeklyActive"];

    /// <summary>Keys metrics.json may carry that the hero must NOT draw, each with why.</summary>
    private static readonly (string Key, string Why)[] UndrawnKpis =
    [
        ("maxConcurrentUsers", "the telemetry tile left with DRA-373's brief and DRA-378's follow-up leaves it out"),
    ];

    private static readonly string[] RetiredKpiClaims =
    [
        "game-memory reads",
        "telemetry by default",
        "accounts or cloud services required",
        "built-in offline catalog",
    ];

    /// <summary>Every figure the telemetry worker's schema-1 <c>/metrics.json</c> publishes,
    /// including the two peaks the companion worker PR adds — none of them is ever committed.</summary>
    private static readonly string[] WorkerTelemetryKeys =
    [
        "concurrentNow", "peakConcurrent", "uniqueUsers30d", "versionMix7d",
        "dailyActive", "weeklyActive", "usageHours", "installsAllTime",
        "peakDailyActive", "peakWeeklyActive",
    ];

    /// <summary>One strip tile: a number over a label, and nothing else (no note).</summary>
    private static readonly Regex StripTile = new(
        """<div\s+class="kpi">\s*<div\s+class="n"\s+data-(?<src>metric|live)="(?<key>[^"]+)">(?<n>[^<]*)</div>\s*<div\s+class="l">(?<l>[^<]*)</div>\s*</div>""",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>
    /// The hero's ONE stat strip (Founder, 2026-09-28), read off page markup (comments
    /// stripped): <c>div#hero-kpis</c> holds exactly the seven <see cref="StripTiles"/> in
    /// order; a content tile paints the committed metrics.json value, a live tile ships a dash;
    /// directly under it sits the one caption with its scope, a mention of every live figure
    /// and the <c>live-asof</c> node. Refused anywhere in the markup: a second strip or its
    /// heading, a downloads tile in any wording, a live figure the strip retired, the retired
    /// principle zeros, the maxConcurrentUsers tile, and a claim of unique counts.
    /// </summary>
    internal static IReadOnlyList<string> StripViolations(string html, JsonElement metrics)
    {
        var bad = new List<string>();
        var markup = Regex.Replace(html, "<!--.*?-->", " ", RegexOptions.Singleline);
        var band = HeroKpiBand(markup);
        var caption = "";
        if (band.Length > 0)
        {
            var after = markup.IndexOf(band, StringComparison.Ordinal) + band.Length;
            var cap = new Regex("""\G\s*<p\s+class="quiet livecap">(?<cap>.*?)</p>""", RegexOptions.Singleline | RegexOptions.CultureInvariant)
                .Match(markup, after);
            if (cap.Success) caption = cap.Groups["cap"].Value;
        }
        // The claim scans read the strip and its caption; a fixture with no strip is read whole.
        var claims = band.Length > 0 ? band + " " + caption : markup;

        foreach (var retired in RetiredKpiClaims)
            if (claims.Contains(retired, StringComparison.Ordinal))
                bad.Add($"retired KPI claim still in the band: \"{retired}\"");

        var honesty = Regex.Replace(claims, "not uniques?", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (honesty.Contains("unique", StringComparison.OrdinalIgnoreCase))
            bad.Add("the strip claims unique counts");

        foreach (var (key, why) in UndrawnKpis)
            if (markup.Contains($"data-metric=\"{key}\"", StringComparison.Ordinal))
                bad.Add($"the band draws the {key} tile — {why}");
        foreach (var key in WorkerTelemetryKeys)
            if (markup.Contains($"data-metric=\"{key}\"", StringComparison.Ordinal))
                bad.Add($"the strip draws the telemetry figure {key} from metrics.json; live figures are painted only from the deploy's live.json");

        // Founder, 2026-09-28: the all-versions downloads tile represented v1 and is dropped; the
        // record may stay in metrics.json and nothing draws it. 2026-09-29 added exactly ONE
        // downloads tile — data-live="evolvedDownloads", "Evolved downloads" — so the all-versions
        // KEY is refused in any wording, and so is any other label naming downloads.
        var otherDownloadLabel = Regex.Matches(markup, """<div\s+class="l">(?<l>[^<]*)</div>""", RegexOptions.CultureInvariant)
            .Any(m => m.Groups["l"].Value.Contains("download", StringComparison.OrdinalIgnoreCase)
                      && !string.Equals(m.Groups["l"].Value.Trim(), "Evolved downloads", StringComparison.Ordinal));
        if (otherDownloadLabel || Regex.IsMatch(markup, "data-(?:metric|live)=\"downloads\"", RegexOptions.CultureInvariant))
            bad.Add("the all-versions downloads tile is retired (it represented v1); the one downloads tile is data-live=\"evolvedDownloads\", \"Evolved downloads\"");
        foreach (var key in RetiredLiveKeys.Append("installsAllTime"))
            if (markup.Contains($"data-live=\"{key}\"", StringComparison.Ordinal))
                bad.Add($"the page draws {key}; that live figure is no longer drawn (the one strip is the seven tiles in order; Evolved downloads took Total installs' place 2026-09-29)");

        if (Regex.Matches(markup, """class="kpis[\s"]""").Count > 1 || markup.Contains("id=\"live-kpis\"", StringComparison.Ordinal))
            bad.Add("a second stat strip; the hero has ONE (Founder, 2026-09-28: \"looks bad with two sets of stats\")");
        if (markup.Contains("livehead", StringComparison.Ordinal) || markup.Contains("livestats", StringComparison.Ordinal))
            bad.Add("a second heading over the stats; the one strip has none");

        if (band.Length == 0)
        {
            bad.Add("no stat strip (div#hero-kpis)");
            return bad;
        }

        var tiles = StripTile.Matches(band);
        var drawn = Regex.Matches(band, """class="kpi">""").Count;
        if (tiles.Count != StripTiles.Length || drawn != StripTiles.Length)
            bad.Add($"expected {StripTiles.Length} tiles, found {tiles.Count} well-formed (of {drawn} kpi nodes)");

        for (var i = 0; i < StripTiles.Length && i < tiles.Count; i++)
        {
            var (live, key, label) = StripTiles[i];
            var tile = tiles[i];
            var src = live ? "live" : "metric";
            if (!string.Equals(tile.Groups["src"].Value, src, StringComparison.Ordinal) ||
                !string.Equals(tile.Groups["key"].Value, key, StringComparison.Ordinal))
                bad.Add($"tile {i + 1} is data-{tile.Groups["src"].Value}=\"{tile.Groups["key"].Value}\", expected data-{src}=\"{key}\"");
            if (!string.Equals(tile.Groups["l"].Value.Trim(), label, StringComparison.Ordinal))
                bad.Add($"tile {i + 1} label is \"{tile.Groups["l"].Value.Trim()}\", expected {label}");

            var painted = tile.Groups["n"].Value.Trim();
            if (tile.Groups["src"].Value == "live")
            {
                if (painted != "—")
                    bad.Add($"the live tile {tile.Groups["key"].Value} commits \"{painted}\"; the page ships a dash and only live.json paints a figure");
                continue;
            }

            if (!metrics.TryGetProperty(key, out var value))
            {
                bad.Add($"metrics.json is missing {key}");
                continue;
            }

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            {
                bad.Add($"{key} is not an integer");
                continue;
            }

            var formatted = number.ToString("N0", CultureInfo.InvariantCulture);
            if (!string.Equals(painted, formatted, StringComparison.Ordinal))
                bad.Add($"{key} paints \"{painted}\" but metrics.json formats as \"{formatted}\"");
        }

        if (caption.Length == 0)
        {
            bad.Add("no caption (p.quiet.livecap) directly under the strip");
            return bad;
        }
        if (Regex.Matches(markup, "livecap").Count > 1)
            bad.Add("more than one caption; the strip has ONE");
        var flat = Flatten(Regex.Replace(caption, "<[^>]*>", " "));
        if (!flat.Contains("opted-in Evolved installs only", StringComparison.OrdinalIgnoreCase))
            bad.Add("the caption does not say opted-in Evolved installs only");
        if (!flat.Contains("lower bound", StringComparison.Ordinal))
            bad.Add("the caption does not say each live figure is a lower bound");
        if (!flat.Contains("Updated hourly", StringComparison.OrdinalIgnoreCase))
            bad.Add("the caption does not say it is updated hourly");
        if (!flat.Contains("downloads, not people", StringComparison.Ordinal))
            bad.Add("the caption does not say Evolved downloads counts downloads, not people (TEL-005)");
        foreach (var (live, _, label) in StripTiles)
            if (live && !flat.Contains(label, StringComparison.OrdinalIgnoreCase))
                bad.Add($"the caption does not name {label}, so its scope does not visibly cover that tile");
        if (!caption.Contains("id=\"live-asof\"", StringComparison.Ordinal))
            bad.Add("the caption has no live-asof node for the as-of time");
        return bad;
    }

    /// <summary>What the stylesheet owes the strip: it wraps and centres its rows, and each
    /// tile centres its number over its label and does not grow, so tiles stay equal width
    /// even in a short last row.</summary>
    internal static IReadOnlyList<string> StripCssViolations(string css)
    {
        var bad = new List<string>();
        string Rule(string selector)
        {
            var m = Regex.Match(css, @"(?m)^" + Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}", RegexOptions.CultureInvariant);
            return m.Success ? m.Groups["body"].Value : "";
        }

        var strip = Rule(".kpis");
        if (!Regex.IsMatch(strip, @"flex-wrap:\s*wrap"))
            bad.Add(".kpis does not flex-wrap, so seven tiles cannot wrap gracefully");
        if (!Regex.IsMatch(strip, @"justify-content:\s*center"))
            bad.Add(".kpis lacks justify-content: center, so a short last row is not centred");

        var tile = Rule(".kpi");
        if (!Regex.IsMatch(tile, @"text-align:\s*center"))
            bad.Add(".kpi lacks text-align: center, so a number is not centred over its label");
        if (!Regex.IsMatch(tile, @"align-items:\s*center"))
            bad.Add(".kpi lacks align-items: center");
        if (!Regex.IsMatch(tile, @"flex:\s*0\s+0\s"))
            bad.Add(".kpi may grow (flex is not 0 0 <basis>), so tiles are not equal width in a short row");
        return bad;
    }

    /// <summary>What the COMMITTED metrics.json owes: <c>maxConcurrentUsers</c> may be absent but
    /// never a number (no backend publishes that key), and no telemetry figure at all — those
    /// reach the public site only through the hourly deploy's validated, dated live.json.</summary>
    internal static IReadOnlyList<string> MetricsViolations(JsonElement metrics)
    {
        var bad = new List<string>();
        if (metrics.TryGetProperty("maxConcurrentUsers", out var concurrent) && concurrent.ValueKind != JsonValueKind.Null)
            bad.Add("maxConcurrentUsers is a fabricated integer; no telemetry backend publishes that key, so it stays null");

        foreach (var key in WorkerTelemetryKeys)
            if (metrics.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null)
                bad.Add($"metrics.json commits the telemetry figure {key}; live figures are never committed — the hourly Pages deploy writes them into live.json");
        return bad;
    }

    /// <summary>The committed live.json: schema 1, and a telemetry half and a downloads half
    /// that are each explicitly unavailable and name no figure — and nothing else.</summary>
    internal static IReadOnlyList<string> CommittedLiveFileViolations(JsonElement live)
    {
        var bad = new List<string>();
        if (!live.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number || schema.GetInt32() != 1)
            bad.Add("live.json is not schema 1");
        var top = live.EnumerateObject().Select(p => p.Name).ToArray();
        if (!top.SequenceEqual(["schema", "generatedAt", "telemetry", "downloads"]))
            bad.Add($"the committed live.json carries {string.Join(",", top)}; it carries schema, generatedAt, telemetry and downloads");
        foreach (var half in new[] { "telemetry", "downloads" })
        {
            if (!live.TryGetProperty(half, out var h) || h.ValueKind != JsonValueKind.Object)
            {
                bad.Add($"live.json has no {half} object");
                continue;
            }
            var keys = h.EnumerateObject().Select(p => p.Name).ToArray();
            if (!h.TryGetProperty("available", out var a) || a.ValueKind != JsonValueKind.False)
                bad.Add($"the committed live.json says {half} is available; only the hourly deploy may");
            if (!keys.SequenceEqual(["available", "reason"]))
                bad.Add($"the committed live.json's {half} carries {string.Join(",", keys)}; it may carry only available and reason");
        }
        return bad;
    }

    internal static IReadOnlyList<string> PagesWorkflowViolations(string yaml)
    {
        var bad = new List<string>();
        if (!Regex.IsMatch(yaml, @"^\s*schedule:\s*\n\s*-\s*cron:\s*'[^']+'", RegexOptions.Multiline))
            bad.Add("pages.yml has no schedule; the live figures would only move when somebody pushes");
        if (Regex.IsMatch(yaml, @"contents:\s*write"))
            bad.Add("pages.yml grants contents: write; the live figures are never committed");
        if (Regex.IsMatch(yaml, @"\bgit\s+(push|commit)\b"))
            bad.Add("pages.yml runs git push/commit; the live figures go into the artifact only");

        var generate = yaml.IndexOf("landing-telemetry.ps1 -OutFile site/live.json", StringComparison.Ordinal);
        var upload = yaml.IndexOf("actions/upload-pages-artifact", StringComparison.Ordinal);
        if (generate < 0)
            bad.Add("pages.yml does not run landing-telemetry.ps1 -OutFile site/live.json");
        else if (upload < 0 || generate > upload)
            bad.Add("pages.yml must write site/live.json before upload-pages-artifact, or the file never ships");
        if (generate >= 0)
        {
            var stepStart = yaml.LastIndexOf("      - ", generate, StringComparison.Ordinal);
            var step = stepStart < 0 ? "" : yaml[stepStart..generate];
            if (!step.Contains("continue-on-error: true", StringComparison.Ordinal))
                bad.Add("the live-figures step lacks continue-on-error: true; a failed fetch must never fail the deploy");
        }
        return bad;
    }

    private static string HeroKpiBand(string html)
    {
        const string marker = "id=\"hero-kpis\"";
        var at = html.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) return "";
        var open = html.LastIndexOf("<div", at, StringComparison.Ordinal);
        if (open < 0) return "";

        var depth = 0;
        for (var i = open; i < html.Length; i++)
        {
            if (i + 4 <= html.Length && html.AsSpan(i, 4).SequenceEqual("<div"))
            {
                depth++;
                i += 3;
                continue;
            }

            if (i + 6 <= html.Length && html.AsSpan(i, 6).SequenceEqual("</div>"))
            {
                depth--;
                if (depth == 0) return html[open..(i + 6)];
                i += 5;
            }
        }

        return "";
    }

    private static int QuestArrayCount()
    {
        var path = Path.Combine(Repo, "src", "EQBuddy.Core", "Data", "QuestCatalog.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("quests").GetArrayLength();
    }

    private static int ItemArrayCount()
    {
        var path = Path.Combine(Repo, "src", "EQBuddy.Core", "Data", "ItemCatalog.json.gz");
        using var file = File.OpenRead(path);
        using var gz = new GZipStream(file, CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(gz);
        return doc.RootElement.GetProperty("Items").GetArrayLength();
    }
}
