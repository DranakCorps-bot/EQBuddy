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
/// **Every one of those is deliberate, and until this file existed only two of them said so
/// where a test could see it.** The `Dim(PromotedStatsNote` / `Dim(GlancePetNote` rows in
/// `SettingsProsePolicyTests` cover the pair Helm signed as body exemptions at PR #456. The
/// "where did my card go" machinery below — the absorbed notes and the whole retired list — had
/// nothing asserting it stays VISIBLE, which is the failure this tab has already shipped once:
/// #219 filed "now I can't get it back" against a screen whose whole job is to list every card,
/// and CLAUDE.md's three ways back are the standing answer. **Two of those three ways are lines
/// of body prose on this tab.** A future pass that "finishes the job" by hanging them on an ⓘ
/// would satisfy every existing guard, photograph as a tidier screen, and put the answer behind
/// a hover nobody knows to reach — traps 29/34, arriving as a tidy-up.
///
/// So: a must-list with a reason per row, plus a ceiling on the total, because the owner's
/// complaint is about VOLUME and a per-paragraph rule cannot see a screen filling back up one
/// short line at a time.
/// </summary>
public class CardsTabResidualProseTests
{
    private static string Block => SettingsProseSource.Block("SettingsHudView.cs");

    private static string Prose(string name) => SettingsProseSource.Prose(Block, name);

    /// <summary>
    /// **The "where did it go" machinery, and the call that proves it is still in the BODY.**
    ///
    /// These are not exempt because they are short — two of them are over
    /// <see cref="SettingsProsePolicy.BodyWordCeiling"/> and the policy would happily call them
    /// explanations. They are exempt because of WHO reads them: someone scanning this list for a
    /// row that is not in it. A paragraph explaining a switch has an obvious owner to hover; an
    /// answer to "there is no Quests row here any more" has no control at all, and the player it
    /// is written for is the one player who does not know to look for an ⓘ.
    ///
    /// The text of each lives in `UI.Shared/OptionsViewModel.cs` and is content-checked by
    /// `RetiredCardsTests`; what is checked HERE is the presentation the file that draws them
    /// chose — which is the half a rename in the other file cannot break and a tidy-up in this
    /// one silently can.
    /// </summary>
    public static readonly (string What, string PrintedBy, string Why)[] StaysInTheBody =
    [
        ("the retired heading", "Text = OverlaySections.RetiredHeading",
            "\"No longer on the widget\" is the only heading on this screen a player hunting a "
            + "missing card can find by scanning"),
        ("the retired blurb", "Meta(OverlaySections.RetiredBlurb",
            "it says the features are INTACT before it says where they went, which is the "
            + "sentence a player who has just failed to find something needs first"),
        ("each retired card's line", "Meta(gone.Line",
            "CLAUDE.md's \"X is now Y\" — the old place AND the new one, plus the context-menu "
            + "row that opens it, because a hotkey is not a door (trap 59)"),
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

    /// <summary>And none of it has been quietly hung on a hint as well as printed — a paragraph
    /// in both places is the duplicate half of the same guard, which nobody notices because
    /// everything still works.</summary>
    [Fact]
    public void NoneOfItIsAlsoOnAnAffordance()
    {
        Assert.DoesNotContain("Hint(OverlaySections.Retired", Block, StringComparison.Ordinal);
        Assert.DoesNotContain("HeadingHint(OverlaySections.Retired", Block,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Hint(absorbed", Block, StringComparison.Ordinal);
        Assert.DoesNotContain("Hint(gone.Line", Block, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every word this tab still prints as body copy, counted the way
    /// <see cref="SettingsProsePolicy.Words"/> counts them. Derived from the same lists the tab
    /// renders from — the retired rows and the absorbed notes are WALKED rather than typed here,
    /// so a future HUD subtraction is counted the day it lands rather than the day somebody
    /// remembers this file (trap 30).
    /// </summary>
    public static int BodyWords()
    {
        var total = SettingsProsePolicy.Words(Prose("PromotedStatsNote"))
                    + SettingsProsePolicy.Words(Prose("GlancePetNote"))
                    + SettingsProsePolicy.Words(Prose("RecentRateBlurb"))
                    + SettingsProsePolicy.Words(OverlaySections.RetiredHeading)
                    + SettingsProsePolicy.Words(OverlaySections.RetiredBlurb);
        foreach (var gone in OverlaySections.Retired)
            total += SettingsProsePolicy.Words(gone.Line);
        foreach (var card in OverlaySections.Catalog)
            total += SettingsProsePolicy.Words(OverlaySections.AbsorbedNote(card.Key));
        return total;
    }

    /// <summary>
    /// **The ceiling, which is a RATCHET and not a rule about any one paragraph.**
    ///
    /// 220 is what the owner's 2026-09-08 QA shot photographed, measured rather than estimated:
    /// 120 of those words are the two signed mini-dashboard exemptions, 66 are the retired
    /// block, 24 are the three absorbed notes and 10 are the recent-rate caption. It is pinned
    /// so the screen cannot fill back up the way it filled up the first time — one reasonable
    /// short line at a time, each of them under the ceiling
    /// <see cref="SettingsProsePolicy.BodyWordCeiling"/> can see.
    ///
    /// **A HUD subtraction is REQUIRED to push this number up** — every cut owes the retired
    /// list a row (CLAUDE.md, Bevel I-11 §4), and seven more cards are queued behind Surface A.
    /// That is the one edit that raises the ceiling rather than failing against it: raise it in
    /// the same commit, by the words the new row costs, and say which cut spent them. What this
    /// guard refuses is the OTHER kind of growth — a new explanation arriving in the body of a
    /// tab two passes have already converted.
    /// </summary>
    public const int BodyWordCeiling = 220;

    [Fact]
    public void TheTabsBodyProseHasNotGrownBackPastWhatTheOwnerPhotographed()
    {
        var words = BodyWords();
        Assert.True(words <= BodyWordCeiling,
            $"Options → Cards & windows now prints {words} words of body prose, up from the "
            + $"{BodyWordCeiling} in the 2026-09-08 owner QA shot. If a HUD subtraction added a "
            + "retired row, raise the ceiling in the same commit and name the cut that spent "
            + "the words. If something else grew, it is a new explanation on a converted tab — "
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
    /// the retired list as present forever.
    /// </summary>
    [Fact]
    public void TheScanCanSeeAParagraphThatMovedToHover()
    {
        Assert.Contains("HeadingHint(PanelsHeading, PanelsBlurb", Block, StringComparison.Ordinal);
        Assert.DoesNotContain("Dim(PanelsBlurb", Block, StringComparison.Ordinal);
    }
}
