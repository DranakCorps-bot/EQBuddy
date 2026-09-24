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
            memory, no game-memory reads, never phones home, and never measures
            other players.</p>
            """;
        Assert.Empty(Violations(wrapped));
    }

    /// <summary>
    /// DRA-69. The landing's Support EQBuddy control is a quiet topbar chip
    /// (top-right, after GitHub), not a hero paragraph, not a download CTA, and
    /// not a checkout embed. It opens https://ko-fi.com/eqbuddy in a new tab —
    /// an optional community tip for a free program, not charity, crowdfunding,
    /// or paid access, and the label stays Support EQBuddy. The page must not
    /// grow a third-party script — "this page makes no third-party requests" is
    /// a live claim in the footer. Founder asked 2026-09-22 for a top-of-page
    /// placement, then after #826 landed clarified the placement as a topbar
    /// chip rather than a hero sentence. The Stripe Payment Link that used to
    /// sit here is dead and must not return.
    /// </summary>
    [Fact]
    public void TheTopbarCarriesAQuietSupportChip()
    {
        var html = Page;
        var topbar = Regex.Match(
            html,
            """<nav\s+class="topbar"[^>]*>.*?</nav>""",
            RegexOptions.Singleline);
        Assert.True(topbar.Success, "landing is missing the topbar");
        var match = Regex.Match(
            topbar.Value,
            """<a\s+[^>]*href="https://ko-fi\.com/eqbuddy"[^>]*>\s*Support EQBuddy\s*</a>""",
            RegexOptions.Singleline);
        Assert.True(match.Success, "topbar is missing the Support EQBuddy Ko-fi chip");
        Assert.Contains("target=\"_blank\"", match.Value, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", match.Value, StringComparison.Ordinal);
        Assert.Contains("class=\"nav support\"", match.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Donate", match.Value, StringComparison.OrdinalIgnoreCase);
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
    /// DRA-373 CTA variant B, Founder direction 2026-09-24 1:08 PM CT (via Helm): the landing
    /// presents EQBuddy Evolved as COMING SOON. It links to no 1.x download — no
    /// <c>releases/latest</c>, no tag or channel page, no "1.x available today" line — and it has
    /// no download button at all, because the only installer that exists is v1. Variant A (a
    /// download button) is forbidden until a public Evolved installer exists; the PR that flips
    /// it changes this test in the same commit.
    /// </summary>
    [Fact]
    public void TheLandingIsComingSoonAndNeverLinksV1()
    {
        Assert.Empty(ComingSoonViolations(Page));
        Assert.Contains("coming soon", Page, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Committed negative: D2's pre-direction variant-B hero, verbatim, trips every arm
    /// it should — the button, the releases link and the 1.x line.</summary>
    [Fact]
    public void TheComingSoonRuleRefusesTheOldVariantBHero()
    {
        const string oldHero = """
            <div class="ctas">
              <a class="btn primary" href="https://github.com/DranakCorps-bot/EQBuddy/releases/latest">Download EQBuddy</a>
            </div>
            <p class="quiet">Evolved v2 arriving — 1.x available today.</p>
            <a href="https://github.com/DranakCorps-bot/EQBuddy/releases/tag/v1.99.18">v1.99.18</a>
            """;
        var bad = ComingSoonViolations(oldHero);
        Assert.Contains(bad, v => v.Contains("releases", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("download", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("1.x", StringComparison.Ordinal));
    }

    private static List<string> ComingSoonViolations(string html)
    {
        var bad = new List<string>();
        if (Regex.IsMatch(html, """href="[^"]*/releases(/|")""", RegexOptions.IgnoreCase))
            bad.Add("links a GitHub releases page (a v1 download)");
        if (Regex.IsMatch(html, """<(a|button)\b[^>]*>\s*Download\b""", RegexOptions.IgnoreCase))
            bad.Add("carries a download button");
        if (Regex.IsMatch(html, """1\.x available|v1\.99\.\d+""", RegexOptions.IgnoreCase))
            bad.Add("offers 1.x as today's download");
        return bad;
    }

    /// <summary>
    /// Founder ask 2026-09-22, narrowed by DRA-373 D2 (Founder brief, 2026-09-24). The hero
    /// KPI band used to wear four principle zeros (0 game-memory reads, 0 accounts, 0
    /// telemetry by default, 11,000+ catalog); 2026-09-22 made it four measured stats. The
    /// five-section page keeps the two CONTENT facts — Quests Tracked, Items Cataloged —
    /// painted from <c>site/metrics.json</c>. The downloads tile left because its number is
    /// <b>1.x</b> installer downloads, which on an Evolved page reads as Evolved downloads; the
    /// concurrent tile left per the brief. <c>metrics.json</c> keeps both keys and their scope
    /// notes, so the JSON half of the old guard still binds: the download count is the measured
    /// one and the concurrent figure stays null until opt-in telemetry publishes one. The
    /// catalog counts are the arrays themselves, so a refresh that moves the file without
    /// moving the JSON goes red here.
    /// </summary>
    [Fact]
    public void TheHeroKpisAreMeasuredStats()
    {
        var band = HeroKpiBand(Page);
        Assert.False(string.IsNullOrEmpty(band), "hero KPI band is missing");

        using var metricsDoc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Repo, "site", "metrics.json")));
        var metrics = metricsDoc.RootElement;

        Assert.Empty(HeroKpiViolations(band, metrics));
        Assert.Empty(MetricsViolations(metrics));

        Assert.Equal(QuestArrayCount(), metrics.GetProperty("questsTracked").GetInt32());
        Assert.Equal(ItemArrayCount(), metrics.GetProperty("itemsCataloged").GetInt32());
        Assert.Equal(MeasuredInstallerDownloads, metrics.GetProperty("downloads").GetInt32());

        var downloadsScope = metrics.GetProperty("scope").GetProperty("downloads").GetString();
        Assert.NotNull(downloadsScope);
        Assert.Contains("EQBuddySetup.exe", downloadsScope, StringComparison.Ordinal);
        Assert.Contains("not unique", downloadsScope, StringComparison.OrdinalIgnoreCase);

        var concurrentScope = metrics.GetProperty("scope").GetProperty("maxConcurrentUsers").GetString();
        Assert.NotNull(concurrentScope);
        Assert.Contains("opt-in", concurrentScope, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(PendingConcurrent, concurrentScope, StringComparison.Ordinal);
        Assert.DoesNotContain("em dash", concurrentScope, StringComparison.OrdinalIgnoreCase);

        // The painter reads the JSON and hard-codes no figure. The concurrent special case
        // left with its tile: an arm for a key the page does not draw is code nobody runs.
        var js = File.ReadAllText(Path.Combine(Repo, "site", "assets", "js", "landing.js"));
        Assert.Contains("metrics.json", js, StringComparison.Ordinal);
        Assert.DoesNotContain("maxConcurrentUsers", js, StringComparison.Ordinal);
        Assert.DoesNotContain("28462", js, StringComparison.Ordinal);
        Assert.DoesNotContain("1173", js, StringComparison.Ordinal);
        Assert.DoesNotContain("11196", js, StringComparison.Ordinal);
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
        var bad = HeroKpiViolations(old, metrics.RootElement);
        Assert.Contains(bad, v => v.Contains("game-memory reads", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("telemetry by default", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("accounts or cloud services required", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("built-in offline catalog", StringComparison.Ordinal));
    }

    /// <summary>
    /// DRA-373's committed negative: the four-tile band this page shipped until D2, verbatim.
    /// DRA-378 restored the downloads tile, but only under the all-versions label — so this
    /// band is refused on FOUR fronts: the bare "Downloads" label (must read "EQBuddy
    /// Downloads"), the painted 1.x snapshot (metrics.json now carries the refreshed
    /// all-versions total), the telemetry tile the brief still removed, and the tile count.
    /// The all-versions three-tile strip is accepted, so the rule is satisfiable.
    /// </summary>
    [Fact]
    public void ThePreDra373FourTileBandIsRefused()
    {
        using var metrics = JsonDocument.Parse(ShippedMetricsJson);
        var m = metrics.RootElement;

        var bad = HeroKpiViolations(FourTileBand, m);
        Assert.Contains(bad, v => v.Contains("expected EQBuddy Downloads", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("maxConcurrentUsers tile", StringComparison.Ordinal));

        // The all-versions label is the only label the hero may carry on this tile; a bare
        // "Downloads" or an "Evolved downloads" reading is refused.
        var bare = ThreeTileStrip.Replace("EQBuddy Downloads", "Downloads", StringComparison.Ordinal);
        Assert.Contains(HeroKpiViolations(bare, m), v => v.Contains("expected EQBuddy Downloads", StringComparison.Ordinal));

        var evolved = ThreeTileStrip.Replace("EQBuddy Downloads", "Evolved downloads", StringComparison.Ordinal);
        Assert.Contains(HeroKpiViolations(evolved, m), v => v.Contains("expected EQBuddy Downloads", StringComparison.Ordinal));

        // A band wearing the right label but still painting the old 1.x snapshot is refused
        // against the LIVE site metrics: the figure must be the all-versions total
        // metrics.json carries, so the pre-refresh count can never pass again.
        using var live = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo, "site", "metrics.json")));
        var lm = live.RootElement;
        var stale = FourTileBand.Replace(
            "<div class=\"l\">Downloads</div>",
            "<div class=\"l\">EQBuddy Downloads</div>",
            StringComparison.Ordinal);
        Assert.Contains(HeroKpiViolations(stale, lm),
            v => v.Contains("downloads paints \"28,462\"", StringComparison.Ordinal));

        // Satisfiability: the accepted strip is the all-versions three-tile hero against the
        // live metrics; a two-tile band still trips the count.
        Assert.Empty(HeroKpiViolations(ThreeTileStrip, lm));
        Assert.Contains(HeroKpiViolations(TwoTileStrip, lm), v => v.Contains("KPI tiles, found 2", StringComparison.Ordinal));
    }

    /// <summary>
    /// A concurrent integer is a fabricated figure until opt-in telemetry publishes one.
    /// The page no longer draws the key, so the JSON is where the claim lives — and a
    /// published but unsourced number there is one refresh away from the page again.
    /// </summary>
    [Fact]
    public void AFabricatedConcurrentIntegerIsRefused()
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

        using var unpublished = JsonDocument.Parse(ShippedMetricsJson);
        Assert.Empty(MetricsViolations(unpublished.RootElement));
    }

    /// <summary>
    /// "not uniques" was the honesty line on the downloads tile. The tile is gone, but a
    /// band that calls anything on it unique players is still refused.
    /// </summary>
    [Fact]
    public void CallingACountUniquesIsRefused()
    {
        var band = TwoTileStrip.Replace(
            "<div class=\"l\">Quests Tracked</div>",
            "<div class=\"l\">Quests Tracked</div><div class=\"note\">unique players</div>",
            StringComparison.Ordinal);
        using var metrics = JsonDocument.Parse(ShippedMetricsJson);
        Assert.Contains(HeroKpiViolations(band, metrics.RootElement),
            v => v.Contains("unique", StringComparison.Ordinal));
    }

    private const int MeasuredInstallerDownloads = 37679;

    private const string PendingConcurrent = "Telemetry not live yet";

    private const string ShippedMetricsJson = """
        {
          "questsTracked": 1173,
          "itemsCataloged": 11196,
          "downloads": 28462,
          "maxConcurrentUsers": null
        }
        """;

    private const string TwoTileStrip = """
        <div class="kpis reveal" id="hero-kpis">
          <div class="kpi"><div class="n" data-metric="questsTracked">1,173</div><div class="l">Quests Tracked</div></div>
          <div class="kpi"><div class="n" data-metric="itemsCataloged">11,196</div><div class="l">Items Cataloged</div></div>
        </div>
        """;

    /// <summary>
    /// DRA-378's positive fixture: the hero band after the all-versions downloads tile was
    /// restored under the "EQBuddy Downloads" label — two catalog tiles plus the downloads
    /// tile with its scope note. This is the strip the rule must accept.
    /// </summary>
    private const string ThreeTileStrip = """
        <div class="kpis reveal" id="hero-kpis">
          <div class="kpi"><div class="n" data-metric="questsTracked">1,173</div><div class="l">Quests Tracked</div></div>
          <div class="kpi"><div class="n" data-metric="itemsCataloged">11,196</div><div class="l">Items Cataloged</div></div>
          <div class="kpi"><div class="n" data-metric="downloads">37,679</div><div class="l">EQBuddy Downloads</div><div class="note">all versions · installer downloads</div></div>
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

    private static readonly (string Key, string Label)[] HeroKpiOrder =
    [
        ("questsTracked", "Quests Tracked"),
        ("itemsCataloged", "Items Cataloged"),
        ("downloads", "EQBuddy Downloads")
    ];

    /// <summary>Keys metrics.json carries that the hero must NOT draw, each with why.</summary>
    private static readonly (string Key, string Why)[] UndrawnKpis =
    [
        ("maxConcurrentUsers", "the telemetry tile left with DRA-373's brief")
    ];

    private static readonly string[] RetiredKpiClaims =
    [
        "game-memory reads",
        "telemetry by default",
        "accounts or cloud services required",
        "built-in offline catalog",
    ];

    private static readonly Regex HeroKpiTile = new(
        """<div\s+class="kpi">\s*<div\s+class="n"\s+data-metric="(?<key>[^"]+)">(?<n>[^<]*)</div>\s*<div\s+class="l">(?<l>[^<]*)</div>(?:\s*<div\s+class="note">(?<note>[^<]*)</div>)?\s*</div>""",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    internal static IReadOnlyList<string> HeroKpiViolations(string band, JsonElement metrics)
    {
        var bad = new List<string>();
        foreach (var retired in RetiredKpiClaims)
            if (band.Contains(retired, StringComparison.Ordinal))
                bad.Add($"retired KPI claim still in the band: \"{retired}\"");

        var honesty = Regex.Replace(band, "not uniques", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        honesty = Regex.Replace(honesty, "not unique", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (honesty.Contains("unique", StringComparison.OrdinalIgnoreCase))
            bad.Add("the band claims unique counts");

        foreach (var (key, why) in UndrawnKpis)
            if (band.Contains($"data-metric=\"{key}\"", StringComparison.Ordinal))
                bad.Add($"the band draws the {key} tile — {why}");

        var tiles = HeroKpiTile.Matches(band);
        if (tiles.Count != HeroKpiOrder.Length)
            bad.Add($"expected {HeroKpiOrder.Length} KPI tiles, found {tiles.Count}");

        for (var i = 0; i < HeroKpiOrder.Length && i < tiles.Count; i++)
        {
            var (key, label) = HeroKpiOrder[i];
            var tile = tiles[i];
            if (!string.Equals(tile.Groups["key"].Value, key, StringComparison.Ordinal))
                bad.Add($"tile {i + 1} key is \"{tile.Groups["key"].Value}\", expected {key}");
            if (!string.Equals(tile.Groups["l"].Value.Trim(), label, StringComparison.Ordinal))
                bad.Add($"tile {i + 1} label is \"{tile.Groups["l"].Value.Trim()}\", expected {label}");

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

            var painted = tile.Groups["n"].Value.Trim();
            var formatted = number.ToString("N0", CultureInfo.InvariantCulture);
            if (!string.Equals(painted, formatted, StringComparison.Ordinal))
                bad.Add($"{key} paints \"{painted}\" but metrics.json formats as \"{formatted}\"");
        }

        return bad;
    }

    /// <summary>What metrics.json owes whether or not the page draws a key.</summary>
    internal static IReadOnlyList<string> MetricsViolations(JsonElement metrics)
    {
        var bad = new List<string>();
        if (!metrics.TryGetProperty("maxConcurrentUsers", out var concurrent))
            bad.Add("metrics.json is missing maxConcurrentUsers");
        else if (concurrent.ValueKind != JsonValueKind.Null)
            bad.Add("maxConcurrentUsers is a fabricated integer; it stays null until opt-in telemetry publishes a figure");
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
