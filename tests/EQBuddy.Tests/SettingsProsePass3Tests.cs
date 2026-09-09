using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **Pass 3 — the residual, and the hole that let it survive two passes.**
///
/// The owner ran the finished Pass 1 + Pass 2 build against a live profile and the Cards &amp;
/// windows tab still opened on body prose. Every row in `SettingsProsePolicyTests` and
/// `SettingsProsePass2Tests` was green while it did, and the reason is a gap rather than a
/// wrong judgement: **the Pass 2 sweep reads `Dim("…")` literals, and the HUD block prints its
/// last paragraphs from consts declared in UI.Shared** — `OverlaySections.RetiredBlurb` and
/// `RetiredCard.Line`. `SettingsProseSource.PrintedLiterals` says in as many words that it
/// cannot see a `Dim(SomeConst)`; Pass 1 covered its own consts with named rows and had no
/// sweep at all, so a paragraph that was neither a literal nor a named row was covered by
/// nothing. That is trap 34 exactly — a guard that forbids the wrong thing cannot see a
/// missing thing — and the fix is a must-list, not another row.
///
/// **So this file is two things, and the second is the one worth having:**
///
///  1. The residual conversion. `RetiredBlurb` moved onto an ⓘ beside the "No longer on the
///     widget" heading, in the Pass 1 <c>HeadingHint</c> shape and without one word rewritten.
///  2. **A must-list over everything the HUD block PRINTS**, by identifier rather than by
///     literal, so a paragraph arriving through a const is ruled on exactly like one arriving
///     through a string.
///
/// **What did NOT move, and it is a judgement rather than an oversight: the retired ROWS.**
/// Each one ends "Right-click EQBuddy and choose “World…”" — it NAMES A DOOR, which is Pass
/// 2's third exemption kind, signed by Helm at #458/#459. Trap 59's rule is to enumerate the
/// entrances a player who has configured nothing actually has before subtracting one, and this
/// is the screen someone opens when the card they want is missing: an ⓘ nobody knows to hover
/// is the same thing as deleting the only printed answer to "how do I get it back". The blurb
/// above them names no door — it says the features are intact — so it converts and they do not.
/// </summary>
public class SettingsProsePass3Tests
{
    private const string Hud = "SettingsHudView.cs";

    private static string Block => SettingsProseSource.Block(Hud);

    // ---------------------------------------------------------------------------------
    // What Pass 3 moved
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// **The residual paragraph the owner's shot caught, on an ⓘ and out of the body.**
    ///
    /// Both halves, because they fail differently and only one of them is visible: a paragraph
    /// left in the body as well is a duplicate nobody notices, and one that never reached an ⓘ
    /// is gone from the product with nothing to say so.
    ///
    /// Its text lives in UI.Shared, so — unlike every Pass 1/2 row — it is measured DIRECTLY
    /// rather than read out of the block's source. That is the same gap this file's sweep
    /// closes from the other side.
    /// </summary>
    [Fact]
    public void TheRetiredBlurbMovedOntoTheHeadingsHint()
    {
        Assert.True(SettingsProsePolicy.BelongsOnHover(OverlaySections.RetiredBlurb),
            $"RetiredBlurb is {SettingsProsePolicy.Words(OverlaySections.RetiredBlurb)} words — "
            + "a caption, not an explanation. If it was shortened, it belongs back in the body "
            + "where nothing takes it away, and this conversion should be reverted rather than "
            + "left standing on a premise that has gone.");
        Assert.True(SettingsProsePolicy.FitsOneHover(OverlaySections.RetiredBlurb));

        Assert.Contains("Hint(OverlaySections.RetiredBlurb)", Block, StringComparison.Ordinal);
        Assert.DoesNotContain("Meta(OverlaySections.RetiredBlurb", Block, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The ⓘ is built ONCE and re-shown, not built per render.**
    ///
    /// `BuildRetired` runs inside `BuildCards`, which runs on every panel move, every hide and
    /// every palette swap — so a hint constructed there would push `hudHints` up by one on each
    /// redraw and turn the dump's only runtime check on this pass into a number that always
    /// passes its floor. Worse, `_cards.Children.Clear()` detaches the ROW but leaves the
    /// button parented to it, so re-adding the same button to a fresh row throws. Holding the
    /// built row is `_breakoutsHint`'s own answer to the same question.
    /// </summary>
    [Fact]
    public void TheRetiredHintIsBuiltOnceAndReused()
    {
        Assert.Contains("_retiredRow ??=", Block, StringComparison.Ordinal);
        // The count that reaches EQBUDDY_EXPAND is off BUILT buttons (traps 34/39), so a hint
        // rebuilt per render would inflate it rather than be caught by it.
        Assert.Contains("hudHints={_hints}", Block, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------
    // What deliberately did not move — the doors
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// **Every retired row NAMES A DOOR, and that is why it is still printed.**
    ///
    /// Pass 2's third exemption kind, Helm-signed at #458/#459, applied to the list it was
    /// really about. The assertion is against the RECORD rather than against two sentences
    /// typed here, so the seven cuts queued behind Surface A are covered on the day they land
    /// instead of the day somebody remembers this file: a future row whose line stops naming
    /// its context-menu header fails here, and one that is quietly hidden behind an ⓘ fails
    /// the body assertion below.
    /// </summary>
    [Fact]
    public void EveryRetiredRowNamesItsDoorAndIsStillPrintedInTheBody()
    {
        Assert.NotEmpty(OverlaySections.Retired);
        foreach (var gone in OverlaySections.Retired)
            Assert.True(gone.Line.Contains(gone.MenuHeader, StringComparison.Ordinal),
                $"the \"{gone.Title}\" row no longer names the context-menu row that opens it "
                + $"(\"{gone.MenuHeader}\"). A hotkey is not a door and nothing is bound by "
                + "default (trap 59) — this sentence is the only printed answer this screen has "
                + "to \"how do I get it back\", and it is the whole reason the row was exempted "
                + "from the prose sweep.");

        Assert.Contains("Meta(gone.Line", Block, StringComparison.Ordinal);
        Assert.DoesNotContain("Hint(gone.Line", Block, StringComparison.Ordinal);
    }

    /// <summary>
    /// The fold notes' other half: a card that was ABSORBED gets its old names under the card
    /// that took them, and that line is a CAPTION — which is why no pass has ever had to rule
    /// on it. Asserted rather than assumed, because the sentence is ASSEMBLED from a list that
    /// grows with every fold (<see cref="OverlaySections.AbsorbedNote"/>), so the day one card
    /// absorbs six names it crosses the ceiling and the judgement has to be made for real.
    /// </summary>
    [Fact]
    public void EveryAbsorbedNoteIsStillACaption()
    {
        // Asked of the CATALOG rather than of the private fold table, which is also the only
        // set that can actually reach the screen: a fold note keyed on a card that no longer
        // exists is never rendered at all (trap 55).
        var notes = OverlaySections.Catalog
            .Select(card => (card.Key, Note: OverlaySections.AbsorbedNote(card.Key)))
            .Where(row => row.Note is not null)
            .ToList();
        Assert.NotEmpty(notes);

        foreach (var (key, note) in notes)
            Assert.False(SettingsProsePolicy.BelongsOnHover(note),
                $"the fold note under \"{key}\" is now {SettingsProsePolicy.Words(note)} words "
                + "— an explanation rather than the caption every pass has assumed it is. It "
                + "has a card of its own to hang an ⓘ beside, so this is a conversion nobody "
                + "has done yet, not a failing test.");
    }

    // ---------------------------------------------------------------------------------
    // The sweep: BY IDENTIFIER, which is the hole Pass 3 exists to close
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// **The must-list half for the HUD block (trap 34), reading the CALLS rather than the
    /// strings.**
    ///
    /// Pass 2's sweep asks "what literals does this file still print"; a paragraph declared as
    /// a const somewhere else is invisible to it, which is exactly how the owner's residual
    /// survived two green passes. This one asks the question the other way round — *what does
    /// this block hand to a body-prose builder at all* — and every answer has to be a name on
    /// the list below with a reason attached. A new `Dim(…)` or `Meta(…)` of any kind fails
    /// until somebody rules on it.
    /// </summary>
    [Fact]
    public void NothingUnruledIsPrintedAsBodyProseInTheHudBlock()
    {
        var printed = BodyProseArguments(Block).ToList();
        // The floor first: a scanner that found nothing would make this assertion pass over a
        // screen that was nothing but essays (trap 39, and SettingsProseSourceTests' own rule).
        Assert.NotEmpty(printed);

        foreach (var argument in printed)
            Assert.True(RuledBodyProse.ContainsKey(argument),
                $"{Hud} prints \"{argument}\" as body prose and no row accounts for it. Either "
                + "hang it on an ⓘ (SettingsProsePolicy) or add it to RuledBodyProse with the "
                + "reason it has to stay printed — a paragraph nobody has ruled on is how the "
                + "essay grows back, and a const one is invisible to the Pass 2 literal sweep.");
    }

    /// <summary>
    /// **The sweep's own floor.** A scanner that returned nothing would make
    /// <see cref="NothingUnruledIsPrintedAsBodyProseInTheHudBlock"/> pass over a screen that
    /// was entirely essays, and one that read comments as code would fail on a docstring while
    /// the screen was fine — which it did, on the comment describing this very sweep. Both
    /// directions, on an input small enough to read: a real call is reported, the builders' own
    /// declarations are not, a `Dim(` written in prose is not, and a `//` inside a string
    /// literal does not truncate the line it is on.
    /// </summary>
    [Fact]
    public void TheSweepReadsCallsAndNotProseAboutThem()
    {
        var found = BodyProseArguments(
            "/// The Pass 2 sweep reads Dim(\"…\") literals.\n"
            + "        panel.Children.Add(Dim(RealParagraph, new Thickness(0)));\n"
            + "        _cards.Children.Add(Meta(gone.Line, top: 2));   // Dim(NotThisOne)\n"
            + "        var url = Dim(\"https://example.invalid/a\", new Thickness(0));\n"
            + "    private TextBlock Dim(string text, Thickness margin) => new()\n").ToList();

        Assert.Equal(["RealParagraph", "\"https://example.invalid/a\"", "gone.Line"], found);
    }

    /// <summary>Everything the HUD block is allowed to print under a control, and why. The
    /// first three are Pass 1's recorded judgements; the fourth is Pass 3's.</summary>
    private static readonly Dictionary<string, string> RuledBodyProse = new(StringComparer.Ordinal)
    {
        ["PromotedStatsNote"] =
            "answers where XP, DPS and HPS went — no control on this screen to hang an ⓘ beside",
        ["GlancePetNote"] =
            "the only place un-starring pet is explained as narrowing rather than stopping",
        ["RecentRateBlurb"] =
            "ten words — a caption, and the negative that keeps the policy from meaning "
            + "\"hide everything\"",
        ["gone.Line"] =
            "each retired row NAMES THE DOOR back to the surface (trap 59); Pass 2's third "
            + "exemption kind, Helm-signed #458/#459",
    };

    /// <summary>Every ruled paragraph is one the block really still prints. Without this,
    /// deleting a kept paragraph outright would leave the sweep green and a row passing on a
    /// sentence nobody can read any more — the exact failure this file exists to catch, wearing
    /// the guard's own clothes.</summary>
    [Fact]
    public void EveryRuledParagraphIsReallyStillPrinted()
    {
        var printed = BodyProseArguments(Block).ToList();
        foreach (var (argument, why) in RuledBodyProse)
            Assert.True(printed.Contains(argument),
                $"\"{argument}\" is ruled as body prose on the HUD block — it {why} — but the "
                + "block no longer prints it. Zero means it was deleted or converted rather "
                + "than kept; a conversion is a reversal of a recorded judgement and needs "
                + "Bevel, not a silent edit.");
    }

    /// <summary>
    /// The first argument of every <c>Dim(…)</c> / <c>Meta(…)</c> call in the block — the two
    /// builders that put dim body text on this screen — as written at the call site, so a const
    /// reports its NAME and a literal reports itself.
    ///
    /// Deliberately not a reader of values: the point of this sweep is to notice a paragraph
    /// whose text lives somewhere this file cannot read, which is precisely the case the value
    /// readers give up on.
    /// </summary>
    private static IEnumerable<string> BodyProseArguments(string source)
    {
        source = WithoutComments(source);
        foreach (var builder in new[] { "Dim(", "Meta(" })
        {
            for (var i = 0; (i = source.IndexOf(builder, i, StringComparison.Ordinal)) >= 0;)
            {
                var start = i + builder.Length;
                i = start;
                // The builders' own declarations — `TextBlock Dim(string text, …)` — are not
                // call sites, and a sweep that counted them would report a parameter name.
                var end = source.IndexOfAny([',', ')'], start);
                if (end < 0) break;
                var argument = source[start..end].Trim();
                if (argument.Length == 0
                    || argument.StartsWith("string ", StringComparison.Ordinal)) continue;
                // A literal reports its opening line rather than the whole chain: this sweep
                // rules on WHAT is printed, and the value readers already measure the words.
                yield return argument;
            }
        }
    }

    /// <summary>
    /// <paramref name="source"/> with its comments removed.
    ///
    /// **Written because the sweep caught the comment that documents it.** The sentence
    /// explaining that Pass 2 reads <c>Dim("…")</c> literals is itself a `Dim(` in the file,
    /// and a scanner that reads prose ABOUT the code as code fails on a docstring while the
    /// screen is fine — a false alarm that gets a guard weakened rather than a defect found.
    /// The Pass 2 sweep has the same blind spot and has simply never been written about.
    ///
    /// It tracks quotes rather than cutting at the first <c>//</c>, because these blocks print
    /// URLs and paths and a naive cut would truncate a literal mid-sentence — quietly, into
    /// something that still parses.
    /// </summary>
    private static string WithoutComments(string source)
    {
        var kept = new List<string>();
        foreach (var line in source.Split('\n'))
        {
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            var inString = false;
            var cut = -1;
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == '\\' && inString) { i++; continue; }
                if (line[i] == '"') { inString = !inString; continue; }
                if (!inString && line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    cut = i;
                    break;
                }
            }
            kept.Add(cut < 0 ? line : line[..cut]);
        }
        return string.Join("\n", kept);
    }
}
