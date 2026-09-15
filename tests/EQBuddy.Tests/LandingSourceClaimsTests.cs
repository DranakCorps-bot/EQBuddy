using System.Reflection;
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
/// The negative below cannot see a MISSING thing (trap 34), so it is paired with a
/// must-list that is DERIVED from <see cref="GameCommands"/> rather than written here:
/// a fifth /outputfile dump reddens this test until the enumerating surfaces name it,
/// which is the only version of this guard that survives the next command being added.
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
    /// rather than hidden: a fifth dump reddens the four enumerating surfaces, and the
    /// other two have nothing to go stale.
    ///
    /// The charter takes the short form because the must-list exists for a PLAYER asking
    /// "what does EQBuddy read?" — that reader reaches the landing, README, PRODUCT.md and
    /// SECURITY.md, not an internal requirements doc whose audience line names Helm, Fable
    /// and the execution agents. What the charter owes is that its hard lines are not
    /// FALSE, which is arms (a) and (b).
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
    /// DRA-69. The landing's Support EQBuddy control is a footer text link, not a
    /// hero CTA, and it opens the Stripe Payment Link in a new tab. The page must
    /// not grow a checkout embed or a third-party script — "this page makes no
    /// third-party requests" is a live claim in the same footer.
    /// </summary>
    [Fact]
    public void TheFooterCarriesAQuietSupportLink()
    {
        var html = Page;
        var match = Regex.Match(
            html,
            """<a\s+href="https://buy\.stripe\.com/aFa00k1tE2064qRb0S9R600"[^>]*>\s*Support EQBuddy\s*</a>""",
            RegexOptions.Singleline);
        Assert.True(match.Success, "footer is missing the Support EQBuddy Stripe Payment Link");
        Assert.Contains("target=\"_blank\"", match.Value, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", match.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("js.stripe.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paypal", html, StringComparison.OrdinalIgnoreCase);
    }
}
