using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// **EDIT HUD'S EXIT COPY — the regression this refuses by name.**
///
/// Bevel's cog/Options IA faces §C (Helm-signed 2026-09-08). The mode's door was
/// asymmetric: you entered it through a right-click and a menu row, and you LEFT it through
/// the same right-click and the same row a second time. The sentence on the edit strip said
/// so — *"Right-click the widget and choose Edit HUD… again when you are done"* — which is
/// copy standing in for a control that was not there.
///
/// The control exists now (Done on the strip), Esc does the same, and the title-bar pencil
/// toggles. **None of those three is unit-testable**: the WPF layer has no test project
/// (docs/TestPlan.md §5), so the chicklet, the key handler and the button are E2E facts
/// (`hudEditDone`, `titleEditHud`). What a unit test CAN hold is the words — and the words
/// are the thing that goes stale silently, because a hint naming a way out that no longer
/// exists still renders perfectly.
/// </summary>
public sealed class HudEditTextTests
{
    /// <summary>The hint names both ways out, in the order a player can act on: Done is on
    /// the strip they are looking at, Esc is a key they have to know.</summary>
    [Fact]
    public void TheHintNamesDoneAndEsc()
    {
        Assert.Contains(HudEditText.DoneLabel, HudEditText.Hint, StringComparison.Ordinal);
        Assert.Contains("Esc", HudEditText.Hint, StringComparison.Ordinal);
        Assert.True(
            HudEditText.Hint.IndexOf(HudEditText.DoneLabel, StringComparison.Ordinal)
            < HudEditText.Hint.IndexOf("Esc", StringComparison.Ordinal),
            "Done is the exit visible from the row and is named first");
    }

    /// <summary>
    /// **The old sentence may not come back.** "Choose Edit HUD… again" is the defect §C
    /// fixed, and a hint that says it is a mode whose only advertised exit is the door it
    /// was entered by — whether or not Done is still drawn beside it.
    /// </summary>
    [Fact]
    public void TheHintDoesNotSendThePlayerBackToTheMenuRow()
    {
        Assert.DoesNotContain("again", HudEditText.Hint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Right-click", HudEditText.Hint, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The three exits are one set and every piece of copy about them has to agree. The
    /// Done control's own hover names the other two, because a player who found Done did
    /// not necessarily find the pencil that put them here.
    /// </summary>
    [Fact]
    public void TheDoneHoverNamesTheOtherTwoExits()
    {
        Assert.Contains("Esc", HudEditText.DoneTip, StringComparison.Ordinal);
        Assert.Contains("pencil", HudEditText.DoneTip, StringComparison.Ordinal);
    }

    /// <summary>
    /// The way IN says what the mode does and where sound settings are NOT — the one
    /// question Edit HUD's mute has always invited, and the reason the menu row's tooltip
    /// carried the same clause before the pencil existed.
    /// </summary>
    [Fact]
    public void TheEnterHoverSaysWhatTheModeDoesAndWhereSoundLives()
    {
        Assert.Contains("Edit HUD", HudEditText.EnterTip, StringComparison.Ordinal);
        Assert.Contains("Alerts & chips", HudEditText.EnterTip, StringComparison.Ordinal);
    }
}
