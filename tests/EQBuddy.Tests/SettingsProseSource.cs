using System.Text;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **Reading Settings' copy out of the SOURCE, because nothing else can see it.**
///
/// The WPF layer has no test project (docs/TestPlan.md §5), so the prose-to-hover passes are
/// guarded by reading the blocks' own `.cs` files: a paragraph's SENTENCE is measured against
/// <see cref="EQBuddy.UI.Shared.SettingsProsePolicy"/>, and the call that hangs it is asserted
/// by name. Measuring the IDENTIFIER instead would be a word count of nothing.
///
/// **This lives in one place on purpose.** Pass 1 wrote the const scanner privately inside
/// `SettingsProsePolicyTests`; Pass 2 needs the same answer about three more files, and a
/// second copy of a scanner is two producers of one fact whose disagreement nobody would ever
/// see — the scanner going quiet is indistinguishable from the copy being fine (trap 4, and
/// trap 34's shape: a reader that returns "" makes every `FitsOneHover` pass). Pass 1's
/// <c>Prose</c> delegates here, and <see cref="TheReaderReallyReadsTheSentence"/> below is
/// the reader's own floor.
/// </summary>
internal static class SettingsProseSource
{
    /// <summary>`src/`, from the test binary's own location.</summary>
    public static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    /// <summary>One of the Settings blocks, by file name.</summary>
    public static string Block(string file) =>
        File.ReadAllText(Path.Combine(Src, "EQBuddy", file));

    /// <summary>
    /// The value of a <c>const string</c> declared in <paramref name="source"/>, assembled
    /// from the literal.
    ///
    /// It walks the literal rather than matching to the first <c>;</c>, because several of
    /// these paragraphs contain a semicolon INSIDE the string — a regex that stopped there
    /// would silently measure half a sentence and pass.
    /// </summary>
    public static string Prose(string source, string name)
    {
        var at = source.IndexOf("const string " + name, StringComparison.Ordinal);
        Assert.True(at >= 0,
            $"the block no longer declares a const named {name}. If it was renamed, point the "
            + "row at the new name rather than deleting it — a row that cannot find its "
            + "sentence is the only way this guard goes quiet.");
        return Literal(source, source.IndexOf('=', at) + 1, out _);
    }

    /// <summary>
    /// **Every paragraph a block still PRINTS as a literal**, in source order: the argument of
    /// each <c>Dim("…")</c> call whose text is written at the call site.
    ///
    /// This is the must-list half that a per-paragraph row cannot be (trap 34). Rows say
    /// "these named paragraphs moved"; only a sweep can say "and nothing ELSE over the ceiling
    /// is still printed" — which is the assertion that keeps a NEW essay from arriving on a
    /// converted tab a month from now, with every existing row still green.
    ///
    /// It cannot see a <c>Dim(SomeConst)</c>, and does not pretend to: those are enumerated by
    /// name where they occur, because their text lives in another file and the file that
    /// PRINTS them is not the file that declares them.
    /// </summary>
    public static IEnumerable<string> PrintedLiterals(string source)
    {
        for (var i = 0; (i = source.IndexOf("Dim(", i, StringComparison.Ordinal)) >= 0;)
        {
            var j = i + "Dim(".Length;
            while (j < source.Length && char.IsWhiteSpace(source[j])) j++;
            if (j >= source.Length || source[j] != '"') { i = j; continue; }
            var text = Literal(source, j, out var end);
            i = end;
            if (text.Length > 0) yield return text;
        }
    }

    /// <summary>A C# string literal — or a chain of them joined by <c>+</c> across lines,
    /// which is how every paragraph on these screens is written — starting at or after
    /// <paramref name="from"/>. <paramref name="end"/> is the index just past it.</summary>
    private static string Literal(string source, int from, out int end)
    {
        var text = new StringBuilder();
        var i = from;
        while (true)
        {
            while (i < source.Length && char.IsWhiteSpace(source[i])) i++;
            if (i >= source.Length || source[i] != '"') break;
            for (i++; i < source.Length && source[i] != '"'; i++)
            {
                if (source[i] != '\\') { text.Append(source[i]); continue; }
                i++;
                text.Append(source[i] switch { 'n' => '\n', 't' => '\t', var c => c });
            }
            i++;   // past the closing quote

            // A chain continues only through a '+'. Anything else (a ',' before the margin,
            // a ')') ends the paragraph — without this the reader would run on into the next
            // literal in the file and measure two paragraphs as one.
            var k = i;
            while (k < source.Length && char.IsWhiteSpace(source[k])) k++;
            if (k >= source.Length || source[k] != '+') break;
            i = k + 1;
        }
        end = i;
        return text.ToString();
    }
}

/// <summary>The reader's own floor — see <see cref="SettingsProseSource"/>.</summary>
public class SettingsProseSourceTests
{
    /// <summary>
    /// **A scanner that returned "" would make every `FitsOneHover` pass and every
    /// `BelongsOnHover` fail with a message about copy rather than about itself.** So it is
    /// checked against sentences asserted verbatim elsewhere, including the two shapes that
    /// break a naive reader: an escaped quote, and a semicolon INSIDE the literal.
    /// </summary>
    [Fact]
    public void TheReaderReallyReadsTheSentence()
    {
        var hud = SettingsProseSource.Block("SettingsHudView.cs");
        Assert.StartsWith("Every panel you leave visible shows while EQBuddy is open",
            SettingsProseSource.Prose(hud, "PanelsBlurb"), StringComparison.Ordinal);
        Assert.Contains("\"Last Xm\"", SettingsProseSource.Prose(hud, "RecentRateBlurb"),
            StringComparison.Ordinal);
        Assert.EndsWith("there is nothing left to switch off.",
            SettingsProseSource.Prose(hud, "PromotedStatsNote"), StringComparison.Ordinal);
    }

    /// <summary>
    /// **The sweep's own floor, and the half that stops it being vacuous.** A
    /// <see cref="SettingsProseSource.PrintedLiterals"/> that found nothing would let every
    /// "no over-ceiling paragraph is still printed" assertion pass over a screen that was
    /// nothing but essays. So: it finds paragraphs at all, it stops at the closing quote
    /// rather than running two paragraphs together, and it really does skip a
    /// <c>Dim(SomeConst)</c> rather than silently reading whatever literal comes next.
    /// </summary>
    [Fact]
    public void TheSweepFindsPrintedParagraphsAndStopsAtEachOne()
    {
        var behavior = SettingsProseSource.Block("SettingsBehaviorView.cs");
        var printed = SettingsProseSource.PrintedLiterals(behavior).ToList();

        Assert.NotEmpty(printed);
        // The Alt+Tab note is Dim(AltTabPolicy.TaskbarWarning + …) — a const, not a literal —
        // so the sweep must not report it, and must not report the paragraph after it as
        // starting there either.
        Assert.DoesNotContain(printed, p => p.Contains("taskbar button", StringComparison.Ordinal));
        // And a paragraph it DOES find ends where its literal ends.
        var hideNotRunning = Assert.Single(printed,
            p => p.StartsWith("EQBuddy is on screen only while you play", StringComparison.Ordinal));
        Assert.EndsWith("while any of its windows has focus.", hideNotRunning,
            StringComparison.Ordinal);
    }
}
