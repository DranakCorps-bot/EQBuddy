using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **Pass 1 of the prose-to-hover conversion, guarded from the two sides a test can reach.**
///
/// The owner's ask is about the SHAPE of Settings: nearly every tab had grown an
/// instructional paragraph under every control, and a screen whose job is "find the switch
/// you came for and flip it" had turned into an essay you scroll past. Bevel's faces are the
/// ruling and <see cref="SettingsProsePolicy"/> is the arithmetic under them; this file holds
/// both halves of what can actually be checked:
///
///  1. **The policy itself**, as arithmetic, including the negative that keeps it from being
///     a rule that can never say no.
///  2. **The HUD block's application of it**, read out of the SOURCE — the WPF layer has no
///     unit tests (docs/TestPlan.md §5), so the only thing a test can see is that each
///     paragraph really arrived on an ⓘ and really left the body, and that the two that were
///     deliberately kept are still printed.
///
/// **Point 2 is the one this file exists for.** A conversion that DELETED a paragraph instead
/// of moving it looks identical in every screenshot, in every build and in a diff nobody
/// re-reads sentence by sentence — the same shape as trap 26's missing writer, with copy
/// instead of a handler. Every row below was checked against the pre-conversion tree.
/// </summary>
public class SettingsProsePolicyTests
{
    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    private static string Block =>
        File.ReadAllText(Path.Combine(Src, "EQBuddy", "SettingsHudView.cs"));

    // ---------------------------------------------------------------------------------
    // The policy, as arithmetic
    // ---------------------------------------------------------------------------------

    /// <summary>Words are what a reader counts: whitespace-separated tokens carrying a letter
    /// or a digit. The dashes and arrows our copy punctuates with are not words, and a count
    /// that included them would make a paragraph's budget depend on its typography.</summary>
    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("Recent-rate window", 2)]
    [InlineData("Show it — or don't", 4)]
    [InlineData("↗ ✕ ★", 0)]
    public void WordsCountsWhatAReaderWouldCount(string text, int expected) =>
        Assert.Equal(expected, SettingsProsePolicy.Words(text));

    /// <summary>The ceiling is a caption/explanation line, and it has to be able to say NO —
    /// a policy that swept up every helper line would leave a screen of bare labels, which is
    /// the opposite failure and no cheaper.</summary>
    [Fact]
    public void TheCeilingSeparatesACaptionFromAnExplanation()
    {
        Assert.False(SettingsProsePolicy.BelongsOnHover(
            "The \"Last Xm\" figures on Combat, Kills, Money, and Progress."));
        Assert.True(SettingsProsePolicy.BelongsOnHover(
            "Every panel you leave visible shows while EQBuddy is open — one with nothing "
            + "yet says so in a line and fills in as it happens."));
    }

    /// <summary>
    /// **The hover budget is the tooltip's own bounded life, not a taste call.** A paragraph
    /// a reader cannot finish before <see cref="ToolTipPolicy.ShowDurationMs"/> takes it away
    /// is not made hoverable by wanting it to be: it closes mid-sentence and there is no way
    /// to ask for the rest.
    ///
    /// The negative is what stops this being an assertion that can only ever pass — three of
    /// this screen's paragraphs concatenated is far past the budget, and that is the shape a
    /// future pass would produce by hanging a whole section on one ⓘ.
    /// </summary>
    [Fact]
    public void OneHoverIsWhatTheBoundedTooltipCanBeReadIn()
    {
        Assert.True(SettingsProsePolicy.FitsOneHover(Prose("DoubleClickChipsBlurb")));

        var everything = string.Join(" ",
            Prose("DoubleClickChipsBlurb"), Prose("PromotedStatsNote"), Prose("GlancePetNote"));
        Assert.False(SettingsProsePolicy.FitsOneHover(everything),
            "three paragraphs joined onto one ⓘ still fit the bounded tooltip, which means "
            + "the budget is not measuring anything. Check ReadingWordsPerMinute against "
            + "ToolTipPolicy.ShowDurationMs before relaxing this.");

        // And the budget is DERIVED from the tooltip's bounded life rather than a second
        // number to keep in step (trap 4): exactly what fits in ShowDurationMs fits, and one
        // word more does not. Move the 30 s and this moves with it.
        var budget = ToolTipPolicy.ShowDurationMs
                     * SettingsProsePolicy.ReadingWordsPerMinute / 60_000;
        Assert.True(SettingsProsePolicy.FitsOneHover(
            string.Join(" ", Enumerable.Repeat("word", budget))));
        Assert.False(SettingsProsePolicy.FitsOneHover(
            string.Join(" ", Enumerable.Repeat("word", budget + 1))));
    }

    // ---------------------------------------------------------------------------------
    // The HUD block's conversion — Pass 1
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Every paragraph this pass moved, and the call that proves where it landed. The block
    /// declares them as consts and the WPF assembly is not referenced from here, so each row
    /// is checked both ways: the string VALUE is read out of the source and measured against
    /// the policy, and the CALL that hangs it is asserted by name.
    /// </summary>
    public static readonly (string Const, string HungBy)[] MovedToHover =
    [
        ("PanelsBlurb", "HeadingHint(PanelsHeading, PanelsBlurb"),
        ("HudStatsBlurb", "HeadingHint(HudStatsHeading, HudStatsBlurb"),
        ("DoubleClickChipsBlurb", "HintRow(_doubleClickChips, DoubleClickChipsBlurb"),
        ("TargetDropsBlurb", "HintRow(_targetDrops, TargetDropsBlurb"),
    ];

    public static TheoryData<string, string> MovedRows()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (name, hungBy) in MovedToHover) rows.Add(name, hungBy);
        return rows;
    }

    /// <summary>
    /// **Each moved paragraph arrived on an ⓘ AND left the body.** Both halves, because they
    /// fail differently and only one of them is visible: a paragraph left in the body as well
    /// is a duplicate nobody notices, and a paragraph that never reached an ⓘ is gone from the
    /// product with nothing to say so.
    /// </summary>
    [Theory]
    [MemberData(nameof(MovedRows))]
    public void EveryMovedParagraphIsOnAnAffordanceAndOutOfTheBody(string name, string hungBy)
    {
        Assert.True(Block.Contains(hungBy, StringComparison.Ordinal),
            $"{name} was converted to hover and SettingsHudView.cs no longer hangs it "
            + $"(looked for \"{hungBy}\"). A paragraph that left the body and arrived on no "
            + "affordance is copy deleted by accident — identical in every screenshot.");
        Assert.DoesNotContain($"Dim({name}", Block, StringComparison.Ordinal);
    }

    /// <summary>
    /// The half that stops the list going vacuous: every paragraph that MOVED was over the
    /// ceiling to begin with (so the pass was applying the policy rather than its author's
    /// taste), and every one of them fits the hover it now lives on.
    /// </summary>
    [Theory]
    [MemberData(nameof(MovedRows))]
    public void EveryMovedParagraphWasAnExplanationAndFitsOneHover(string name, string _)
    {
        var prose = Prose(name);
        Assert.True(SettingsProsePolicy.BelongsOnHover(prose),
            $"{name} is {SettingsProsePolicy.Words(prose)} words — a caption, not an "
            + "explanation. It belongs under the control where nothing takes it away.");
        Assert.True(SettingsProsePolicy.FitsOneHover(prose),
            $"{name} takes {SettingsProsePolicy.ReadingMs(prose)} ms to read and the tooltip "
            + $"closes at {ToolTipPolicy.ShowDurationMs} ms — it would be taken away "
            + "mid-sentence.");
    }

    /// <summary>The floating-window list's blurb is the one that lives in UI.Shared, so it is
    /// measured directly rather than read out of source — and it is hung on a hint the block
    /// keeps a reference to, because <c>BuildBreakouts</c> re-points it on every redraw.</summary>
    [Fact]
    public void TheFloatingWindowBlurbMovedToo()
    {
        Assert.True(SettingsProsePolicy.BelongsOnHover(BreakoutPresentation.Blurb));
        Assert.True(SettingsProsePolicy.FitsOneHover(BreakoutPresentation.Blurb));
        Assert.Contains("_breakoutsHint = Hint(BreakoutPresentation.Blurb)", Block,
            StringComparison.Ordinal);
        Assert.Contains("DesignSystem.SetHintProse(_breakoutsHint, BreakoutPresentation.Blurb)",
            Block, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The two paragraphs this pass deliberately did NOT move, with the reason attached.**
    ///
    /// Both answer "where did my switch GO" — the question asked by somebody scanning a list
    /// for a row that is not in it. They have no control of their own to hover, and an ⓘ
    /// nobody knows to hover is the same thing as deleting them: an absent sentence
    /// photographs as an unremarkable list (traps 29/34), which is how #233's complaint was
    /// earned in the first place.
    ///
    /// This is a row rather than a comment because the obvious next move for whoever runs
    /// Pass 2 is to "finish the job" on the two longest paragraphs left on the screen.
    /// </summary>
    [Theory]
    [InlineData("PromotedStatsNote", "answers where XP, DPS and HPS went — no control to hang on")]
    [InlineData("GlancePetNote", "the only place un-starring pet is explained as narrowing, not stopping")]
    public void TheWhereDidItGoNotesStayedInTheBody(string name, string why)
    {
        Assert.True(Block.Contains($"Dim({name}", StringComparison.Ordinal),
            $"{name} is no longer printed in the body of the HUD block. It {why}, so moving "
            + "it behind an ⓘ removes it for exactly the player it was written for. If this "
            + "is a deliberate reversal, it needs Bevel and a row deleted here — not a silent "
            + "edit.");
        // …and they really are the long ones, so the exemption is a judgement being made
        // against the policy rather than one that never had to be made.
        Assert.True(SettingsProsePolicy.BelongsOnHover(Prose(name)));
    }

    /// <summary>The screen's one short helper line, which the policy leaves alone. Without a
    /// row like this "convert the prose" reads as "hide the prose", and the next pass takes
    /// the captions with it.</summary>
    [Fact]
    public void TheShortHelperLineWasLeftWhereItWas()
    {
        Assert.False(SettingsProsePolicy.BelongsOnHover(Prose("RecentRateBlurb")));
        Assert.Contains("Dim(RecentRateBlurb", Block, StringComparison.Ordinal);
    }

    /// <summary>
    /// The ⓘ count reaches the <c>EQBUDDY_EXPAND</c> dump, counted off BUILT buttons. Five
    /// explanations now exist ONLY behind an ⓘ, so an ⓘ that failed to build is a paragraph
    /// that has left the product — and `ShellHostTests` is what compares the two hosts'
    /// numbers, which is the only place that can see it at runtime.
    /// </summary>
    [Fact]
    public void TheAffordanceCountIsInTheDump()
    {
        Assert.Contains("hudHints={_hints}", Block, StringComparison.Ordinal);
        Assert.Contains("_hints++;", Block, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------
    // Reading a const out of the source
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The value of one of the block's string consts, assembled from the source. The test
    /// project does not reference the WPF assembly (docs/TestPlan.md §5), and measuring the
    /// IDENTIFIER instead of the sentence would be a word count of nothing.
    ///
    /// **The scanner itself moved to <see cref="SettingsProseSource"/> in Pass 2**, which
    /// needed the same answer about three more files. A second copy of it would be two
    /// producers of one fact whose disagreement nobody would ever see (trap 4). Its floor —
    /// the escaped quote and the semicolon INSIDE a literal, both of which live in the
    /// paragraphs this file measures — went with it.
    /// </summary>
    private static string Prose(string name) => SettingsProseSource.Prose(Block, name);

    /// <summary>
    /// **Both halves of what Pass 1 moved really are what a player sees**, checked against
    /// the sentences `SettingsHudBlockTests` pins against the vocabulary ban. Not the
    /// reader's floor any more — that is
    /// <see cref="SettingsProseSourceTests.TheReaderReallyReadsTheSentence"/> — but the
    /// claim that THIS file is still pointed at the right sentences after the move.
    /// </summary>
    [Fact]
    public void TheSourceReaderReallyReadsTheSentence()
    {
        Assert.StartsWith("Every panel you leave visible shows while EQBuddy is open",
            Prose("PanelsBlurb"), StringComparison.Ordinal);
        Assert.EndsWith("fills in as it happens.", Prose("PanelsBlurb"), StringComparison.Ordinal);

        // The escaped quote — a scanner that dropped the backslash handling would stop here.
        Assert.Contains("\"Last Xm\"", Prose("RecentRateBlurb"), StringComparison.Ordinal);
        // The semicolon INSIDE the literal, which is what a naive match-to-the-first-`;`
        // would truncate at. The words after it are the point of the sentence.
        Assert.EndsWith("there is nothing left to switch off.", Prose("PromotedStatsNote"),
            StringComparison.Ordinal);
    }
}
