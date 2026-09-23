using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **What Options → Cards &amp; windows still PRINTS after the prose-to-hover passes, and why
/// each surviving paragraph is allowed to be there.**
///
/// Passes 1 and 2 moved seventeen explanations onto an ⓘ, and `SettingsProsePolicyTests` guards
/// that half from both sides: each moved paragraph arrived on an affordance AND left the body.
/// This file is the OTHER half, and it exists because of a shot rather than a theory — the
/// owner's 2026-09-08 QA capture of this tab
/// (`.claude/soft-captures/20260908-owner-qa/options-cards-live-189220.png`, publish
/// `2.0.0+ed989e52`) shows the converted screen still carrying two full paragraphs under the
/// mini dashboard and a four-line "No longer on the widget" block under the panel list.
///
/// **DRA-352 D2 (Founder direction on the card's own screenshot, 2026-09-23) took most of that
/// off the screen**: the "No longer on the widget" block (Helm LOCKED the drop of
/// <c>OverlaySections.Retired</c>, data and all), both mini-dashboard notes and the Restore
/// default order button, and the Floating windows list. What this file guards now is what is
/// LEFT — the absorbed notes, which are CLAUDE.md's way back #1 and are still body prose — and
/// that the removed blocks stay removed, because a screen the owner asked to be shorter is
/// the thing a well-meant "restore the explanation" quietly undoes.
///
/// So: a must-list with a reason per row, a must-NOT list for what D2 cut, plus a ceiling on
/// the total, because the owner's complaint is about VOLUME and a per-paragraph rule cannot
/// see a screen filling back up one short line at a time.
/// </summary>
public class CardsTabResidualProseTests
{
    private static string Block => SettingsProseSource.Block("SettingsHudView.cs");

    private static string Prose(string name) => SettingsProseSource.Prose(Block, name);

    /// <summary>
    /// **The "where did it go" note, and the call that proves it is still in the BODY.**
    ///
    /// It is exempt because of WHO reads it: someone scanning this list for a row that is not
    /// in it. A paragraph explaining a switch has an obvious owner to hover; an answer to
    /// "there is no Money row here any more" has no control at all, and the player it is
    /// written for is the one player who does not know to look for an ⓘ.
    ///
    /// Until DRA-352 D2 three more rows stood here — the retired heading, its blurb and each
    /// retired card's line. They left with <c>OverlaySections.Retired</c> by Founder direction;
    /// <see cref="TheBlocksD2CutStayCut"/> is their other half.
    /// </summary>
    public static readonly (string What, string PrintedBy, string Why)[] StaysInTheBody =
    [
        ("the absorbed note", "Text = absorbed",
            "#219's own fix: a folded card's name returns on the card that ABSORBED it, at the "
            + "screen where the question is asked"),
    ];

    public static TheoryData<string, string, string> BodyRows()
    {
        var rows = new TheoryData<string, string, string>();
        foreach (var row in StaysInTheBody) rows.Add(row.What, row.PrintedBy, row.Why);
        return rows;
    }

    /// <summary>
    /// Each row is still drawn as body copy. The failure message carries the REASON rather than
    /// the rule, because the person who hits this will be mid-way through a conversion that
    /// looks obviously right.
    /// </summary>
    [Theory]
    [MemberData(nameof(BodyRows))]
    public void TheWhereDidMyCardGoMachineryIsStillPrinted(string what, string printedBy, string why)
    {
        Assert.True(Block.Contains(printedBy, StringComparison.Ordinal),
            $"SettingsHudView no longer prints {what} in the body (looked for \"{printedBy}\"). "
            + $"It stays visible because {why}. Moving it onto an ⓘ removes it for exactly the "
            + "player it was written for — an absent sentence photographs as an unremarkable "
            + "list (traps 29/34). If this is a deliberate reversal it needs Bevel's copy and "
            + "Helm's sign, and this row deleted with a note — not a silent edit.");
    }

    /// <summary>
    /// **What DRA-352 D2 took off this tab stays off it** — the Founder's screenshot, as a
    /// must-NOT list. Each is the call that drew the block, so restoring any of them (a
    /// well-meant "put the explanation back") fails here by name. The Floating windows list
    /// is checked by its builder AND its write, because the list was the one writer of
    /// <c>DisabledBreakouts</c> and that writer lives on the window's pin now
    /// (<c>BreakoutAutoOpenTests</c>); a second writer arriving back here is trap 4.
    /// </summary>
    [Theory]
    [InlineData("OverlaySections.Retired", "the \"No longer on the widget\" block")]
    [InlineData("BuildRetired", "the \"No longer on the widget\" block's builder")]
    [InlineData("PromotedStatsNote", "the DPS/HPS/XP top-row note")]
    [InlineData("GlancePetNote", "the pet-damage top-row note")]
    [InlineData("RestoreOrder", "the Restore default order button")]
    [InlineData("MiniBarOrder.Clear()", "the Restore default order button's write")]
    [InlineData("BuildBreakouts", "the Floating windows list")]
    [InlineData("DisabledBreakouts.", "the Floating windows list's write")]
    public void TheBlocksD2CutStayCut(string needle, string what)
    {
        Assert.False(Block.Contains(needle, StringComparison.Ordinal),
            $"SettingsHudView mentions {needle} again — {what} left Options → Cards & windows "
            + "by Founder direction (DRA-352 D2). Bringing it back is a product reversal, not "
            + "a tidy-up.");
    }

    /// <summary>The must-NOT list above is a substring scan, so it is worth only as much as
    /// its ability to come back TRUE: the same scan must find blocks that are still drawn.
    /// </summary>
    [Fact]
    public void TheCutListCanSeeABlockThatIsStillDrawn()
    {
        Assert.Contains("HudStatsHeading", Block, StringComparison.Ordinal);
        Assert.Contains("DoubleClickChipsLabel", Block, StringComparison.Ordinal);
    }

    /// <summary>And none of it has been quietly hung on a hint as well as printed — a paragraph
    /// in both places is the duplicate half of the same guard, which nobody notices because
    /// everything still works.</summary>
    [Fact]
    public void NoneOfItIsAlsoOnAnAffordance()
    {
        Assert.DoesNotContain("Hint(absorbed", Block, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every word this tab still prints as body copy, counted the way
    /// <see cref="SettingsProsePolicy.Words"/> counts them. Derived from the same lists the tab
    /// renders from — the absorbed notes are WALKED rather than typed here,
    /// so a future HUD subtraction is counted the day it lands rather than the day somebody
    /// remembers this file (trap 30).
    /// </summary>
    public static int BodyWords()
    {
        var total = SettingsProsePolicy.Words(Prose("RecentRateBlurb"));
        foreach (var card in OverlaySections.Catalog)
            total += SettingsProsePolicy.Words(OverlaySections.AbsorbedNote(card.Key));
        return total;
    }

    /// <summary>
    /// **The ceiling, which is a RATCHET and not a rule about any one paragraph.**
    ///
    /// It was 220 — the owner's 2026-09-08 QA shot, measured: 120 words of mini-dashboard
    /// notes, 66 of retired block, 24 of absorbed notes and 10 of recent-rate caption. **DRA-352
    /// D2 re-derived it from what remains** (the Founder's 2026-09-23 screenshot asked for the
    /// first two gone): the three absorbed notes and the caption, 34 words, and nothing else.
    /// It is pinned so the screen cannot fill back up the way it filled up the first time — one
    /// reasonable short line at a time, each of them under the ceiling
    /// <see cref="SettingsProsePolicy.BodyWordCeiling"/> can see.
    ///
    /// **A FOLD is the one edit that raises it** — a folded card's names owe
    /// <c>AbsorbedTitles</c> a row (CLAUDE.md's way back #1). Raise it in the same commit, by
    /// the words the new note costs, and say which fold spent them. (Subtractions no longer
    /// owe this screen a row: the retired list that took them left in D2.) What this guard
    /// refuses is the OTHER kind of growth — a new explanation arriving in the body of a tab
    /// two passes have already converted.
    /// </summary>
    public const int BodyWordCeiling = 34;

    [Fact]
    public void TheTabsBodyProseHasNotGrownBackPastWhatTheOwnerPhotographed()
    {
        var words = BodyWords();
        Assert.True(words <= BodyWordCeiling,
            $"Options → Cards & windows now prints {words} words of body prose, up from the "
            + $"{BodyWordCeiling} left after DRA-352 D2. If a fold added an absorbed note, "
            + "raise the ceiling in the same commit and name the fold that spent the words. "
            + "If something else grew, it is a new explanation on a converted tab — "
            + "hang it on an ⓘ (SettingsProsePolicy) rather than under the control.");
    }

    /// <summary>
    /// **The ceiling has to be able to FAIL, or it is a number that documents nothing.**
    ///
    /// The negative is the shape the ratchet exists to catch: put ONE of the paragraphs Pass 1
    /// moved back into the body and the total is over. `PanelsBlurb` is the smallest of the
    /// five, so if the smallest reversal is caught, all of them are.
    /// </summary>
    [Fact]
    public void PuttingAConvertedParagraphBackWouldBreakTheCeiling()
    {
        var reversed = BodyWords() + SettingsProsePolicy.Words(Prose("PanelsBlurb"));
        Assert.True(reversed > BodyWordCeiling,
            $"the ceiling has {BodyWordCeiling - BodyWords()} words of slack — enough to absorb "
            + "a converted paragraph moving back into the body without failing, which is the "
            + "one thing it is here to notice.");
    }

    /// <summary>
    /// **And the scan that says "still in the body" can really tell the difference.**
    ///
    /// Every assertion above is a substring search over one file, so it is worth exactly as much
    /// as its ability to come back false. `PanelsBlurb` is the control: Pass 1 hung it on an ⓘ
    /// and took it out of the body, so the same two questions asked about it must answer the
    /// other way round. Without this, a scan pointed at a renamed or deleted call would report
    /// the absorbed note as present forever.
    /// </summary>
    [Fact]
    public void TheScanCanSeeAParagraphThatMovedToHover()
    {
        Assert.Contains("HeadingHint(PanelsHeading, PanelsBlurb", Block, StringComparison.Ordinal);
        Assert.DoesNotContain("Dim(PanelsBlurb", Block, StringComparison.Ordinal);
    }
}
