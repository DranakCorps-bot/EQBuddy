using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The hover side of the prose policy, which had rows and no ceiling.**
///
/// Passes 1 and 2 asserted <see cref="SettingsProsePolicy.FitsOneHover"/> for each of the
/// seventeen paragraphs they MOVED, and `SettingsProsePass2Tests` sweeps the body so that no
/// eighteenth explanation can quietly arrive there. Both halves of that are about the body.
/// **Nothing measured the hover text that was already on these tabs, or the hover text that
/// arrives next** — which is trap 34's shape seen from the other side: a must-list of the
/// paragraphs we touched, with no ceiling over the ones we did not.
///
/// It is not hypothetical. The census below was taken on 2026-09-08 while the gear-menu-slim
/// fold (DRA-25) was in flight, and that fold adds the longest hover string on any of these
/// four tabs — a 35-word wiki-pack explanation — to a tab two passes had already converted.
/// It arrived measured by nothing.
///
/// **What this file asserts is the policy's own arithmetic, applied to every hover string
/// rather than to a list of them.** A tooltip a reader cannot finish before
/// <see cref="ToolTipPolicy.ShowDurationMs"/> closes it is taken away mid-sentence with no way
/// to ask for the rest (trap 63's family), and that is true whichever pass put it there.
///
/// **What it deliberately does NOT assert: that an explanation belongs on an ⓘ rather than on
/// the control's own tooltip.** Four of the strings below are over
/// <see cref="SettingsProsePolicy.BodyWordCeiling"/>, so the policy calls them explanations,
/// and three of the four are on ICON buttons where the tooltip is how you learn what the glyph
/// does at all — demanding an ⓘ beside every glyph is a copy decision with a real cost, and
/// copy decisions are Bevel's rather than an executor's (the same line Pass 2 drew around its
/// kind-2 exemptions). That question is filed in `BEVEL-FEEDBACK.md`; it is not settled here by
/// a test that would look like it had been.
///
/// The measured census, for the record — hover strings per block, by the policy's own count:
/// Look 1 (9w) · Alerts 12 (max 26w) · Behavior 1 (1w), or 3 (max 35w) with the fold in flight
/// · HUD 0. The HUD zero is not a gap: every tooltip on that tab is a named const or a call,
/// which this sweep does not read and does not pretend to.
/// </summary>
public class SettingsHoverProseTests
{
    private const string Look = "SettingsLookView.cs";
    private const string Alerts = "SettingsAlertsView.cs";
    private const string Behavior = "SettingsBehaviorView.cs";
    private const string Hud = "SettingsHudView.cs";

    /// <summary>The four blocks the prose passes converted — the same list
    /// <see cref="SettingsProsePass2Tests"/> sweeps for body copy, because a tab that had its
    /// body cleared is exactly the tab where the next explanation goes onto a hover.</summary>
    public static readonly string[] ConvertedBlocks = [Look, Alerts, Behavior, Hud];

    public static TheoryData<string> BlockRows()
    {
        var rows = new TheoryData<string>();
        foreach (var file in ConvertedBlocks) rows.Add(file);
        return rows;
    }

    private static string Src(string file) => SettingsProseSource.Block(file);

    // ---------------------------------------------------------------------------------
    // The ceiling the hover side never had
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// **Every hover string on a converted tab fits one hover.** Not the ones a pass moved —
    /// every one, including the ones that were tooltips long before there was a policy and the
    /// ones a future fold brings with it.
    ///
    /// This is the assertion that catches the shape the passes made likely: a tab whose body is
    /// now clear is an inviting place to park a paragraph, and the tooltip is the only container
    /// left on it. The tooltip is bounded at 30 s because WPF's unbounded default froze the app
    /// (trap 63), so "put the essay on the hover instead" ends with the essay closing
    /// mid-sentence and no way to ask for the rest.
    /// </summary>
    [Theory]
    [MemberData(nameof(BlockRows))]
    public void EveryHoverStringCanBeReadBeforeTheTooltipCloses(string file)
    {
        foreach (var tip in SettingsProseSource.HoverLiterals(Src(file)))
        {
            Assert.True(SettingsProsePolicy.FitsOneHover(tip),
                $"{file} hangs a {SettingsProsePolicy.Words(tip)}-word tooltip that takes "
                + $"{SettingsProsePolicy.ReadingMs(tip)} ms to read, and the tooltip closes at "
                + $"{ToolTipPolicy.ShowDurationMs} ms: \"{Opening(tip)}…\". It would be taken "
                + "away mid-sentence. Split it across the controls it is actually about, or "
                + "leave it in the body where nothing takes it away — the same two answers "
                + "Pass 2 gave the paragraphs it could not convert.");
        }
    }

    /// <summary>
    /// **The ceiling has to be able to fail.** Everything above is green today by a wide margin
    /// — the longest hover string on these tabs is 35 words against a budget near 100 — and a
    /// guard that has only ever passed, on values nowhere near its limit, is vacuous coverage
    /// (trap 34).
    ///
    /// So the reader and the policy are run TOGETHER over a synthetic block holding a tooltip
    /// just past the budget, which is the whole mechanism the theory above uses. A ceiling
    /// proved only against <see cref="SettingsProsePolicy"/> in isolation would still pass if
    /// the sweep handed it nothing.
    /// </summary>
    [Fact]
    public void AnOverBudgetTooltipWouldBeCaught()
    {
        var essay = string.Join(' ', Enumerable.Repeat("word", 400));
        var synthetic = $$"""
            var b = new Button { Content = "x", ToolTip = "{{essay}}" };
            """;

        var found = Assert.Single(SettingsProseSource.HoverLiterals(synthetic));
        Assert.Equal(400, SettingsProsePolicy.Words(found));
        Assert.False(SettingsProsePolicy.FitsOneHover(found),
            "a 400-word tooltip fits inside the bounded hover, so the budget in "
            + "SettingsProsePolicy.FitsOneHover no longer bounds anything a person could "
            + "actually write.");
    }

    // ---------------------------------------------------------------------------------
    // The reader's own floor
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// **A sweep that found nothing would make the ceiling above pass over a screen of
    /// essays** — the same failure `SettingsProseSourceTests` guards for the body reader, and
    /// the reason that reader lives in one place. So: it finds hover strings at all, it stops
    /// at the end of each one rather than running two together, and it really does SKIP the
    /// three shapes it cannot read rather than silently measuring whatever literal comes next.
    ///
    /// The skips are the half worth asserting. A reader that fell through a
    /// <c>ToolTip = SomeConst</c> onto the next quoted string in the file would report a
    /// confident word count for the wrong sentence, and every row above it would stay green.
    /// </summary>
    [Fact]
    public void TheSweepFindsHoverStringsAndSkipsTheOnesItCannotRead()
    {
        var look = SettingsProseSource.HoverLiterals(Src(Look)).ToList();
        var chipRow = Assert.Single(look);
        Assert.EndsWith("the alert banner", chipRow, StringComparison.Ordinal);

        var alerts = SettingsProseSource.HoverLiterals(Src(Alerts)).ToList();
        Assert.NotEmpty(alerts);
        // Stops at its own closing quote — this one is followed by another tooltip a few lines
        // down, so a reader that ran on would report the two as one.
        var spellClass = Assert.Single(alerts,
            t => t.StartsWith("Spell class:", StringComparison.Ordinal));
        Assert.EndsWith("with no match text needed", spellClass, StringComparison.Ordinal);
        // An interpolated tooltip ($"Remove {spell} from {cls}") is a value the RUNTIME
        // decides; the sweep must not report it, and must not report the next literal as it.
        Assert.DoesNotContain(alerts, t => t.StartsWith("Remove", StringComparison.Ordinal));

        // The strongest skip: the HUD block assigns several tooltips and NONE of them is a
        // literal, so the honest answer is an empty sweep rather than a census of the consts'
        // neighbours.
        var hud = Src(Hud);
        Assert.Contains("ToolTip = RestoreOrderTip", hud, StringComparison.Ordinal);
        Assert.Contains("ToolTip = HudStatTip", hud, StringComparison.Ordinal);
        Assert.Empty(SettingsProseSource.HoverLiterals(hud));
    }

    /// <summary>
    /// **And the sweep is really pointed at the converted tabs.** Every row above is keyed on a
    /// file name; if one were renamed or mistyped, <see cref="SettingsProseSource.Block"/> would
    /// throw — but a block that simply stopped assigning any literal tooltip would leave the
    /// theory iterating an empty list and passing. Three of the four carry hover text today, so
    /// all three going quiet at once is the state worth noticing.
    /// </summary>
    [Fact]
    public void TheConvertedTabsStillCarryHoverTextForTheCeilingToMeasure()
    {
        var measured = ConvertedBlocks
            .Sum(file => SettingsProseSource.HoverLiterals(Src(file)).Count());
        Assert.True(measured >= 10,
            $"the hover sweep measured {measured} strings across the four converted Settings "
            + "tabs, down from the 14 in the 2026-09-08 census. Either the tooltips moved to "
            + "consts — in which case this floor should be lowered deliberately, saying which "
            + "block went quiet — or the reader stopped matching the way they are written, and "
            + "every ceiling above is now passing over text nobody is measuring.");
    }

    private static string Opening(string text) => text[..Math.Min(60, text.Length)];
}
