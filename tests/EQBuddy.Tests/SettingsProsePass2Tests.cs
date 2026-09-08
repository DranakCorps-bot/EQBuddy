using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **Pass 2 of the prose-to-hover conversion — Look, Alerts &amp; chips, Watch rules and
/// Behavior.**
///
/// Pass 1 moved the HUD block's five explanations onto an ⓘ and left a note saying Pass 2
/// must not "finish the job" silently. This is Pass 2, guarded the same way and for the same
/// reason: the WPF layer has no unit tests (docs/TestPlan.md §5), so the only thing a test can
/// see is that each paragraph really arrived on an ⓘ, really left the body, and that the ones
/// deliberately kept are still printed. **A conversion that DELETED a paragraph instead of
/// moving it looks identical in every screenshot, in every build, and in a diff nobody
/// re-reads sentence by sentence.**
///
/// **The exemptions are the point of this file, not an appendix to it.** Twelve paragraphs
/// moved; SEVEN did not, and each of the seven is a row with its reason. They fall into three
/// kinds, and only the first was already named by Pass 1:
///
/// <list type="number">
/// <item><b>No control to hang on</b> — Pass 1's <c>PromotedStatsNote</c> exemption in new
///   places. A paragraph whose subject has no switch on the screen has nothing to put an ⓘ
///   beside, and an ⓘ nobody knows to hover is the same thing as deleting it (traps
///   29/34).</item>
/// <item><b>Past the hover budget</b> — the policy's OTHER end, which Pass 1 never had to
///   spend. A paragraph a reader cannot finish inside
///   <see cref="ToolTipPolicy.ShowDurationMs"/> closes mid-sentence with no way to ask for
///   the rest (<see cref="SettingsProsePolicy.FitsOneHover"/>). Both offenders are long
///   because they explain several controls at once, so the fix is to SPLIT them — a copy
///   decision, which is Bevel's and not an executor's.</item>
/// <item><b>It names a door</b> — the way back to a surface, or to EQBuddy itself. Trap 59's
///   rule is to enumerate the entrances a player actually has before subtracting one, and a
///   sentence that is the only printed answer to "how do I get it back" is an entrance.</item>
/// </list>
/// </summary>
public class SettingsProsePass2Tests
{
    private const string Look = "SettingsLookView.cs";
    private const string Alerts = "SettingsAlertsView.cs";
    private const string Behavior = "SettingsBehaviorView.cs";

    private static string Src(string file) => SettingsProseSource.Block(file);

    // ---------------------------------------------------------------------------------
    // What moved
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Every paragraph Pass 2 moved, and the call that proves where it landed. Each block
    /// declares them as consts and the WPF assembly is not referenced from here, so each row
    /// is checked both ways: the string VALUE is read out of the source and measured against
    /// the policy, and the CALL that hangs it is asserted by name.
    /// </summary>
    public static readonly (string File, string Const, string HungBy)[] MovedToHover =
    [
        (Look, "GridOverlayBlurb", "HintRow(_gridOverlayCheck, GridOverlayBlurb"),

        (Alerts, "SlowChipBlurb", "HintRow(_slowAlert, SlowChipBlurb"),
        (Alerts, "RaidDetectionBlurb", "HintRow(_slowRaidOnly, RaidDetectionBlurb"),
        (Alerts, "BuffExpiringOnlyBlurb", "HintRow(_buffExpiringOnly, BuffExpiringOnlyBlurb"),

        (Behavior, "HideUnfocusedBlurb", "HintRow(_hideUnfocused, HideUnfocusedBlurb"),
        (Behavior, "KeepAboveBlurb", "HintRow(_keepAbove, KeepAboveBlurb"),
        // The only one hung on a HEADING rather than a control: the hotkey rows below it are
        // rebuilt on every click (BuildHotkeyRows), so the heading is the one anchor in that
        // section that survives a rebuild.
        (Behavior, "HotkeysBlurb", "HintRow(Heading(\"Global hotkeys\", \"TextBrush\"), HotkeysBlurb"),
        (Behavior, "RegenOverrideBlurb", "HintRow(row, RegenOverrideBlurb"),
        (Behavior, "AutoEmptyBlurb", "HintRow(_truncate, AutoEmptyBlurb"),
        (Behavior, "ArchiveBlurb", "HintRow(_archive, ArchiveBlurb"),
        (Behavior, "PerfReadoutBlurb", "HintRow(_perfStats, PerfReadoutBlurb"),
    ];

    public static TheoryData<string, string, string> MovedRows()
    {
        var rows = new TheoryData<string, string, string>();
        foreach (var (file, name, hungBy) in MovedToHover) rows.Add(file, name, hungBy);
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
    public void EveryMovedParagraphIsOnAnAffordanceAndOutOfTheBody(
        string file, string name, string hungBy)
    {
        var src = Src(file);
        Assert.True(src.Contains(hungBy, StringComparison.Ordinal),
            $"{name} was converted to hover and {file} no longer hangs it (looked for "
            + $"\"{hungBy}\"). A paragraph that left the body and arrived on no affordance is "
            + "copy deleted by accident — identical in every screenshot.");
        Assert.DoesNotContain($"Dim({name}", src, StringComparison.Ordinal);
    }

    /// <summary>
    /// The half that stops the list going vacuous: every paragraph that MOVED was over the
    /// ceiling to begin with (so the pass applied the policy rather than its author's taste),
    /// and every one of them fits the hover it now lives on.
    /// </summary>
    [Theory]
    [MemberData(nameof(MovedRows))]
    public void EveryMovedParagraphWasAnExplanationAndFitsOneHover(
        string file, string name, string _)
    {
        var prose = SettingsProseSource.Prose(Src(file), name);
        Assert.True(SettingsProsePolicy.BelongsOnHover(prose),
            $"{name} is {SettingsProsePolicy.Words(prose)} words — a caption, not an "
            + "explanation. It belongs under the control where nothing takes it away.");
        Assert.True(SettingsProsePolicy.FitsOneHover(prose),
            $"{name} takes {SettingsProsePolicy.ReadingMs(prose)} ms to read and the tooltip "
            + $"closes at {ToolTipPolicy.ShowDurationMs} ms — it would be taken away "
            + "mid-sentence.");
    }

    /// <summary>Setup's note is the one moved paragraph whose text lives in UI.Shared, so it
    /// is measured directly rather than read out of source — and it hangs on the button the
    /// SHELL draws and <c>OptionsWindow</c> does not, which is why `behaviorHints` is compared
    /// across hosts as a difference rather than an equality (`ShellHostTests`).</summary>
    [Fact]
    public void TheSetupNoteMovedToo()
    {
        Assert.True(SettingsProsePolicy.BelongsOnHover(SetupReadout.BehaviorNote));
        Assert.True(SettingsProsePolicy.FitsOneHover(SetupReadout.BehaviorNote));
        Assert.Contains("HintRow(_setupBtn, SetupReadout.BehaviorNote", Src(Behavior),
            StringComparison.Ordinal);
        Assert.DoesNotContain("Dim(SetupReadout.BehaviorNote", Src(Behavior),
            StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------
    // What deliberately did not move — kind 1: no control to hang on
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// **Pass 1's exemption in new places.** Both of these explain something with no switch on
    /// the screen they are printed on, so there is nothing to put an ⓘ beside; hiding them
    /// behind one would remove them for exactly the reader they were written for.
    ///
    /// The row is the opening of the paragraph, which is also how the sweep below recognises
    /// it — one locator rather than two that can drift apart.
    /// </summary>
    [Theory]
    [InlineData(Alerts, "While Options is open, the ★ alert banner tile",
        "is about a tile that appears on the DESK while Options is open — the alert banner "
        + "has no control anywhere on this screen")]
    [InlineData(Alerts, "Watch loot, kills, skill-ups, deaths, milestones",
        "opens the Watch block and explains the rules TABLE. The block's only heading belongs "
        + "to the host (the shell room's label IS the tab), so there is no in-block anchor — "
        + "a block owns nothing above its first control (trap 15)")]
    public void TheParagraphsWithNoControlToHangOnStayedInTheBody(
        string file, string opening, string why)
    {
        var prose = FindPrinted(file, opening);
        Assert.True(SettingsProsePolicy.BelongsOnHover(prose),
            $"\"{opening}…\" is now under the ceiling, so it is no longer an exemption — it is "
            + $"just a caption. It was kept because it {why}; if that is still true the "
            + "sentence needs no ruling, so delete this row rather than leaving a judgement "
            + "recorded about a paragraph that no longer needs one.");
    }

    // ---------------------------------------------------------------------------------
    // What deliberately did not move — kind 2: past the hover budget
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// **The policy's other end, spent for the first time.** These two are far over the
    /// ceiling AND past what the bounded tooltip can be read in, so an ⓘ would close them
    /// mid-sentence with no way to ask for the rest. They are long because each explains
    /// several controls at once; the fix is to SPLIT them across those controls, which is a
    /// copy decision (Bevel's) rather than an executor's, and this pass had no Bevel seat.
    ///
    /// **The assertion is `False(FitsOneHover)`, deliberately.** If somebody shortens one of
    /// these, the exemption's premise is gone and this row fails asking for the conversion —
    /// rather than sitting green over a paragraph that could have moved months ago.
    /// </summary>
    [Theory]
    [InlineData(Alerts, "Pick the buffs this character never camps without")]
    [InlineData(Alerts, "Kill a named — or its placeholder")]
    public void TheParagraphsTooLongForOneHoverStayedInTheBody(string file, string opening)
    {
        var prose = FindPrinted(file, opening);
        Assert.False(SettingsProsePolicy.FitsOneHover(prose),
            $"\"{opening}…\" is {SettingsProsePolicy.Words(prose)} words and now FITS the "
            + $"{ToolTipPolicy.ShowDurationMs} ms hover — the reason it stayed in the body is "
            + "gone. Convert it and move its row up to MovedToHover, or say here why it still "
            + "belongs in the body.");
    }

    // ---------------------------------------------------------------------------------
    // What deliberately did not move — kind 3: it names a door
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// **A sentence that is the only printed answer to "how do I get it back" is an
    /// entrance, and trap 59 says enumerate the entrances before subtracting one.** Behind an
    /// ⓘ, the player who ticks the box without hovering has lost the only place they were
    /// told — and they have just made EQBuddy disappear, so Settings is not somewhere they
    /// can go and look.
    ///
    /// Each row names the DOOR as well as the paragraph, and asserts the door phrase is still
    /// printed: a reword that drops "Start menu" is the same loss as a conversion.
    /// </summary>
    [Theory]
    [InlineData(Behavior, "EQBuddy is on screen only while you play", "Start menu")]
    [InlineData(Behavior, "Show EQBuddy on a phone or tablet on your Wi-Fi",
        "📱 button in the title bar")]
    public void TheParagraphsThatNameADoorStayedInTheBody(string file, string opening, string door)
    {
        var prose = FindPrinted(file, opening);
        Assert.Contains(door, prose, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two door paragraphs the sweep cannot see, because the block PRINTS a const declared
    /// in UI.Shared rather than a literal of its own (<see cref="SettingsProseSource"/> is
    /// explicit that it does not pretend to).
    ///
    /// <see cref="AltTabPolicy.TaskbarWarning"/> is the only printed sentence in the product
    /// that names the tray icon as the way back to a hidden EQBuddy, under the switch that
    /// closes the other ways in. It is also 21 words — one over the ceiling — and the string
    /// this block actually shows is <c>TaskbarWarning + UnavailableNote</c>, whose length the
    /// PLATFORM decides at runtime, so the policy cannot answer for it either way.
    ///
    /// <see cref="MobileAlertSounds.HelperText"/> exists to say the DEFAULT out loud so that
    /// nobody has to flip a switch to discover it (its own doc comment says exactly that);
    /// putting it behind a hover is that reason with the answer removed. It only reaches the
    /// ceiling because the block prints it joined to <see cref="MobileAlertSounds.ScopeNote"/>
    /// — twelve words and eleven, neither an explanation on its own.
    /// </summary>
    [Fact]
    public void TheSharedConstNotesStayedInTheBodyToo()
    {
        var src = Src(Behavior);
        Assert.Contains("AltTabPolicy.TaskbarWarning, AltTabPolicy.UnavailableNote", src,
            StringComparison.Ordinal);
        Assert.Contains("MobileAlertSounds.HelperText + \" \" + MobileAlertSounds.ScopeNote", src,
            StringComparison.Ordinal);
        Assert.DoesNotContain("HintRow(_hideAltTab", src, StringComparison.Ordinal);
        Assert.DoesNotContain("HintRow(_mobileSounds", src, StringComparison.Ordinal);

        // …and the premise: each is a caption on its own, and only the JOIN crosses the line.
        // A ceiling that swept up either half alone would be sweeping up captions.
        Assert.False(SettingsProsePolicy.BelongsOnHover(MobileAlertSounds.HelperText));
        Assert.False(SettingsProsePolicy.BelongsOnHover(MobileAlertSounds.ScopeNote));
        Assert.True(SettingsProsePolicy.BelongsOnHover(
            MobileAlertSounds.HelperText + " " + MobileAlertSounds.ScopeNote));
        // One word over, which is the other half of why this one is a judgement rather than
        // an application of the rule.
        Assert.Equal(SettingsProsePolicy.BodyWordCeiling + 1,
            SettingsProsePolicy.Words(AltTabPolicy.TaskbarWarning));
    }

    // ---------------------------------------------------------------------------------
    // The sweep: and NOTHING ELSE
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// **The must-list half (trap 34).** Every row above says "this named paragraph moved" or
    /// "this named paragraph stayed, because…". Only a sweep can say *and nothing else over
    /// the ceiling is still printed on these four tabs* — which is the assertion that catches
    /// a NEW essay arriving on a converted tab next month with every existing row still green,
    /// and the one that catches a paragraph quietly restored to the body.
    ///
    /// The exempt list here is the SAME openings the rows above are keyed on, so an exemption
    /// cannot be added to the sweep without a row somewhere stating its reason.
    /// </summary>
    [Theory]
    [InlineData(Look)]
    [InlineData(Alerts)]
    [InlineData(Behavior)]
    public void NoOtherExplanationIsStillPrintedInTheBody(string file)
    {
        foreach (var prose in SettingsProseSource.PrintedLiterals(Src(file)))
        {
            if (!SettingsProsePolicy.BelongsOnHover(prose)) continue;
            var opening = prose[..Math.Min(60, prose.Length)];
            Assert.True(Exempt.Any(e => prose.StartsWith(e, StringComparison.Ordinal)),
                $"{file} still PRINTS a {SettingsProsePolicy.Words(prose)}-word explanation "
                + $"that no row accounts for: \"{opening}…\". Either convert it to an ⓘ and add "
                + "it to MovedToHover, or add it to one of the three exemption theories above "
                + "with the reason it has to stay — a paragraph nobody has ruled on is how the "
                + "essay grows back.");
        }
    }

    /// <summary>The openings of the seven paragraphs the theories above rule on, in the same
    /// words those theories use. A sweep with its own private list would be a second place to
    /// exempt a paragraph, and the reason would only be in one of them.</summary>
    private static readonly string[] Exempt =
    [
        "While Options is open, the ★ alert banner tile",
        "Watch loot, kills, skill-ups, deaths, milestones",
        "Pick the buffs this character never camps without",
        "Kill a named — or its placeholder",
        "EQBuddy is on screen only while you play",
        "Show EQBuddy on a phone or tablet on your Wi-Fi",
    ];

    /// <summary>Every exemption in <see cref="Exempt"/> is a paragraph that really is still
    /// printed. Without this, deleting a kept paragraph outright would leave the sweep green
    /// and a row passing on a sentence nobody can read any more — which is the exact failure
    /// this whole file exists to catch, wearing the guard's own clothes.</summary>
    [Fact]
    public void EveryExemptParagraphIsReallyStillPrinted()
    {
        foreach (var opening in Exempt)
        {
            var found = new[] { Look, Alerts, Behavior }
                .SelectMany(f => SettingsProseSource.PrintedLiterals(Src(f)))
                .Count(p => p.StartsWith(opening, StringComparison.Ordinal));
            Assert.True(found == 1,
                $"\"{opening}…\" is exempted from the prose sweep but is printed {found} times "
                + "on the Pass 2 tabs. Zero means it was deleted rather than kept; more than "
                + "one means it is duplicated and the exemption covers whichever copy the "
                + "reader hits first.");
        }
    }

    // ---------------------------------------------------------------------------------
    // The affordance count reaches the dump
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Each block reports how many ⓘ it BUILT into the <c>EQBUDDY_EXPAND</c> dump, which is
    /// the only thing that can see at RUNTIME that an affordance rendered at all — twelve
    /// explanations now exist ONLY behind one, and an ⓘ that failed to build is a paragraph
    /// that has left the product and photographs as an unremarkable panel (traps 29/34).
    /// `ShellHostTests` is what compares the two hosts' numbers.
    /// </summary>
    [Theory]
    [InlineData(Look, "lookHints={_hints}")]
    [InlineData(Alerts, "alertsHints={_hints}")]
    [InlineData(Behavior, "behaviorHints={_hints}")]
    public void TheAffordanceCountIsInTheDump(string file, string fact)
    {
        var src = Src(file);
        Assert.Contains(fact, src, StringComparison.Ordinal);
        // Counted off BUILT buttons rather than off a list of the paragraphs, which is the
        // difference between a fact and a restatement of the source (traps 34/39).
        Assert.Contains("_hints++;", src, StringComparison.Ordinal);
    }

    /// <summary>One row-builder for all four blocks (<c>DesignSystem.HintRow</c>). Pass 1 had
    /// it privately in the HUD block; a second hand-built copy is where two Settings tabs start
    /// disagreeing about how the ⓘ wraps beside a long label (trap 25).</summary>
    [Fact]
    public void EveryBlockHangsItsHintThroughTheOneRowBuilder()
    {
        var design = SettingsProseSource.Block("DesignSystem.cs");
        Assert.Contains("public static UIElement HintRow(", design, StringComparison.Ordinal);
        Assert.Contains("new WrapPanel { Margin = margin }", design, StringComparison.Ordinal);
        foreach (var file in new[] { Look, Alerts, Behavior, "SettingsHudView.cs" })
            Assert.Contains("DesignSystem.HintRow(", Src(file), StringComparison.Ordinal);
        // And nobody rebuilt the row locally — the shape the one builder exists to prevent.
        foreach (var file in new[] { Look, Alerts, Behavior })
            Assert.DoesNotContain("row.Children.Add(Hint(", Src(file), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------

    /// <summary>A paragraph the block still prints, found by its opening. Failing here means
    /// the sentence a row was written about is gone — which is what a row about a KEPT
    /// paragraph exists to notice.</summary>
    private static string FindPrinted(string file, string opening)
    {
        var match = SettingsProseSource.PrintedLiterals(Src(file))
            .FirstOrDefault(p => p.StartsWith(opening, StringComparison.Ordinal));
        Assert.True(match is not null,
            $"{file} no longer prints a paragraph starting \"{opening}…\". It was deliberately "
            + "kept in the body — if it moved onto an ⓘ, that is a reversal of a recorded "
            + "judgement and needs Bevel, not a silent edit; if it was reworded, point this "
            + "row at the new opening.");
        return match!;
    }
}
