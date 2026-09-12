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
/// The negative below cannot see a MISSING thing (trap 34), so it is paired with a
/// must-list that is DERIVED from <see cref="GameCommands"/> rather than written here:
/// a fifth /outputfile dump reddens this test until the page names it, which is the only
/// version of this guard that survives the next command being added.
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
    /// What the page may not say, and what it must. Returns one line per violation so a
    /// failure names the claim rather than reporting a bare false.
    /// </summary>
    internal static IReadOnlyList<string> Violations(string html)
    {
        var bad = new List<string>();
        var flat = Flatten(html);

        // (a) The false claim, in every shape it has actually been written in.
        foreach (var claim in new[] { "log-only", "log only", "reads only the log" })
            if (flat.Contains(claim, StringComparison.OrdinalIgnoreCase))
                bad.Add($"claims \"{claim}\" — we also read the /outputfile dumps");

        // (b) The must-list: ONE place answers "what does EQBuddy read?" in full. Scattered
        // half-answers are how "nothing else" survived beside a working /outputfile button,
        // so the assertion is that a single paragraph names the log AND every dump.
        var answered = Paragraphs(html).Any(p =>
            p.Contains("/log", StringComparison.Ordinal) &&
            p.Contains("/outputfile", StringComparison.Ordinal) &&
            DumpNames.All(n => p.Contains(n, StringComparison.OrdinalIgnoreCase)));
        if (!answered)
            bad.Add("no single paragraph names /log plus every /outputfile dump ("
                    + string.Join(", ", DumpNames) + ")");

        // (c) Being honest about the dumps must not cost the line that is the actual
        // product boundary. The pill got broader; these do not move.
        foreach (var line in new[] { "game-memory", "measures other players" })
            if (!flat.Contains(line, StringComparison.OrdinalIgnoreCase))
                bad.Add($"dropped the values line: \"{line}\"");

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
    private static string Flatten(string s) => Regex.Replace(s, @"\s+", " ");

    private static IEnumerable<string> Paragraphs(string html) =>
        Regex.Matches(html, "<p[^>]*>(.*?)</p>", RegexOptions.Singleline)
             .Select(m => Flatten(m.Groups[1].Value));

    [Fact]
    public void TheLandingPageIsHonestAboutWhatItReads() =>
        Assert.Empty(Violations(Page));

    /// <summary>Four dumps today. If this is ever 1, the reflection above broke and the
    /// must-list went quietly vacuous.</summary>
    [Fact]
    public void EveryOutputfileCommandContributesADumpName()
    {
        Assert.Equal(["achievements", "faction", "inventory", "spellbook"], DumpNames);
        Assert.All(DumpNames, n => Assert.DoesNotContain(' ', n));
    }

    /// <summary>The exact bytes DRA-67 removed, and the two other ways this has been said.
    /// Each must be caught, or the guard above is decoration.</summary>
    [Theory]
    [InlineData("""<span class="pill"><b>Log-only</b> — reads your /log file, nothing else</span>""")]
    [InlineData("<p>EQBuddy reads only the log file the game writes.</p>")]
    [InlineData("<h3>Log-only and local-first</h3>")]
    [InlineData("""<meta name="description" content="the personal, log-only companion">""")]
    public void ThePreDra67WordingIsCaught(string html) =>
        Assert.Contains(Violations(html), v => v.StartsWith("claims", StringComparison.Ordinal));

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
}
