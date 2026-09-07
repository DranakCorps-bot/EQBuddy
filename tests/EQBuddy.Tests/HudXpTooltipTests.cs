using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The xp chip's hover text (OE-3; `BEVEL.md` item 2, Helm-signed #347 item 2).
///
/// Three things are being pinned and the third is the one a tidy refactor drops. The two
/// facts have to be THERE; the ETA sentence has to be the Progress room's own words rather
/// than a second copy of them; and both EMPTY states have to say something, because "no
/// level line" and "no ETA line" are indistinguishable from the app not tracking either —
/// which is precisely the report this item answers.
/// </summary>
public class HudXpTooltipTests
{
    /// <summary>A snapshot with only the two fields the tooltip reads set. Everything else
    /// stays at its default so a failure names the field it is about.</summary>
    private static StatsSnapshot Session(double? hoursToLevel = null, int? lastLevel = null) =>
        new() { HoursToLevel = hoursToLevel, LastLevel = lastLevel };

    [Fact]
    public void TheHoverCarriesTheLevelAndTheEta()
    {
        var tip = HudXpTooltip.For(Session(hoursToLevel: 2.25, lastLevel: 26), ledgerLevel: 27);

        Assert.Equal(27, tip.Level);
        Assert.True(tip.HasEta);
        Assert.Contains("Level 27", tip.Text);
        Assert.Contains("Next level in ~2h 15m at this pace", tip.Text);
        // …and it still says what the chip is and what clicking it does. OE-1's line is
        // the only place the peek/pin interaction is explained at all, so adding facts
        // above it must not be the change that quietly removes it.
        Assert.Contains(HudXpTooltip.GestureLine, tip.Text);
    }

    /// <summary>The ETA sentence is the Progress room's, not a second wording of it — the
    /// assertion that fails the day someone "tidies" one of the two into its own format
    /// string (trap 4). It compares against the SOURCE rather than a literal, so a
    /// deliberate reword of the sentence changes one place and both surfaces follow.</summary>
    [Fact]
    public void TheEtaSentenceIsTheProgressRoomsOwnWords()
    {
        var tip = HudXpTooltip.For(Session(hoursToLevel: 0.4, lastLevel: 12), ledgerLevel: 12);
        Assert.Contains(ProgressPresentation.NextLevelSentence(0.4), tip.Text);
    }

    /// <summary>The signed fallback order — the ledger first, because it is the half that
    /// survives a restart and a truncated log. The snapshot is set to a DIFFERENT level so
    /// the test can tell which source answered; in the app they agree, which is exactly why
    /// a swapped order would go unnoticed until someone reloaded.</summary>
    [Fact]
    public void TheLedgerLevelWinsOverTheSessionsLastDing()
    {
        var tip = HudXpTooltip.For(Session(lastLevel: 3), ledgerLevel: 41);
        Assert.Equal(41, tip.Level);
        Assert.Contains("Level 41", tip.Text);
    }

    /// <summary>…and the session's ding is the fallback, which is the arm that carries a
    /// brand-new character whose ledger has nothing in it yet.</summary>
    [Fact]
    public void TheSessionsLastDingAnswersWhenTheLedgerHasNothing()
    {
        var tip = HudXpTooltip.For(Session(lastLevel: 12), ledgerLevel: null);
        Assert.Equal(12, tip.Level);
        Assert.Contains("Level 12", tip.Text);
    }

    /// <summary>No level anywhere: the tooltip SAYS so. Dropping the line would leave a
    /// hover that reads exactly like an app which does not track levels — the owner's
    /// actual complaint, arriving through the fix for it.</summary>
    [Fact]
    public void AnUnknownLevelIsStatedRatherThanOmitted()
    {
        var tip = HudXpTooltip.For(Session(hoursToLevel: 1.0), ledgerLevel: null);

        Assert.Equal(0, tip.Level);
        Assert.Contains(HudXpTooltip.NoLevelLine, tip.Text);
        Assert.DoesNotContain("Level 0", tip.Text);
    }

    /// <summary>Same on the other side. `HoursToLevel` is null below 0.05%/hr, which on a
    /// session that has just started is every hover for the first minute — the state a
    /// player is MOST likely to meet, so it is the one that must not read as broken.</summary>
    [Fact]
    public void AnUnknowableEtaIsStatedRatherThanOmitted()
    {
        var tip = HudXpTooltip.For(Session(lastLevel: 12), ledgerLevel: 12);

        Assert.False(tip.HasEta);
        Assert.Contains(HudXpTooltip.NoEtaLine, tip.Text);
        Assert.DoesNotContain("at this pace", tip.Text);
    }

    /// <summary>Whatever it knows, the hover is always the same three lines. A tooltip that
    /// grows and shrinks by state is one whose empty states were written as absences.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData(27, null)]
    [InlineData(null, 2.5)]
    [InlineData(27, 2.5)]
    public void EveryStateDrawsThreeLines(int? level, double? hours)
    {
        var tip = HudXpTooltip.For(Session(hoursToLevel: hours), ledgerLevel: level);
        Assert.Equal(3, tip.Text.Split('\n').Length);
    }
}
