using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The one-owner invariant behind inline themes, and every transition that can reach it.
///
/// The rule this file exists for: **a theme's body is drawn in the card or in the window,
/// never both.** On WPF that would be a layout bug; on Avalonia the widget builds each body
/// once and the window borrows it, and a control has one visual parent — so the same
/// mistake THROWS. Nothing in either UI can express that rule where a test could see it,
/// which is why the state machine is here (docs/TestPlan.md §5: the WPF layer has no unit
/// tests at all).
/// </summary>
public class ThemeHostTests
{
    private static ThemeHost<ProgressTab> Host() => new(ProgressSurface.DefaultInlineTab);

    [Fact]
    public void ACardExpandsAndCollapsesAndStartsCollapsedOnItsDefaultRoom()
    {
        var host = Host();
        Assert.Equal(ThemePlacement.Collapsed, host.Placement);
        Assert.Equal(ProgressTab.Experience, host.SelectedTab);

        host.ToggleCard();
        Assert.True(host.IsInline);

        host.ToggleCard();
        Assert.Equal(ThemePlacement.Collapsed, host.Placement);
    }

    [Fact]
    public void PoppingOutCollapsesTheCardSoTheBodyHasOneOwner()
    {
        var host = Host();
        host.ToggleCard();
        host.PopOut();

        Assert.True(host.IsWindowOpen);
        Assert.False(host.IsInline);           // the card let go of the body
        Assert.False(host.ShouldBringWindowForward);
    }

    /// <summary>Clicking the card while the window is up must NOT draw a second copy —
    /// the caller is told to bring the window forward instead. This is the transition that
    /// crashes Avalonia if it is got wrong, so it is asserted rather than assumed.</summary>
    [Fact]
    public void ClickingTheCardWhileTheWindowIsOpenBringsItForwardAndDrawsNothing()
    {
        var host = Host();
        host.PopOut();

        host.ToggleCard();

        Assert.True(host.ShouldBringWindowForward);
        Assert.True(host.IsWindowOpen);
        Assert.False(host.IsInline);
    }

    /// <summary>Closing the window leaves the card COLLAPSED, never inline: the player
    /// closed a thing, and re-growing the widget in its place is the opposite of the ask.
    /// The room is kept for the next expand.</summary>
    [Fact]
    public void ClosingTheWindowCollapsesTheCardAndKeepsTheRoom()
    {
        var host = Host();
        host.ToggleCard();
        host.SelectTab(ProgressTab.Faction);
        host.PopOut();
        host.WindowClosed();

        Assert.Equal(ThemePlacement.Collapsed, host.Placement);
        Assert.Equal(ProgressTab.Faction, host.SelectedTab);

        host.ToggleCard();
        Assert.True(host.IsInline);
        Assert.Equal(ProgressTab.Faction, host.SelectedTab);   // where they left off
    }

    [Fact]
    public void AnOpenerThatIsNotTheCardCanNameItsRoomAndStillOwnsTheBodyAlone()
    {
        var host = Host();
        host.ToggleCard();

        host.OpenWindow(ProgressTab.Raids);   // the ⚙ menu / a hotkey / EQBUDDY_PROGRESS

        Assert.True(host.IsWindowOpen);
        Assert.False(host.IsInline);
        Assert.Equal(ProgressTab.Raids, host.SelectedTab);
    }

    [Fact]
    public void ResetGoesBackToFirstRunUnlikeClosingAWindow()
    {
        var host = Host();
        host.SelectTab(ProgressTab.Wealth);
        host.PopOut();

        host.Reset();

        Assert.Equal(ThemePlacement.Collapsed, host.Placement);
        Assert.Equal(ProgressTab.Experience, host.SelectedTab);
    }

    /// <summary>Whatever order the player clicks in, the body never has two owners. Every
    /// reachable transition, driven exhaustively — the invariant is the point of the class,
    /// so it is asserted after each step rather than at the end.</summary>
    [Fact]
    public void NoSequenceOfActionsEverPutsTheBodyInTwoPlaces()
    {
        var host = Host();
        var actions = new (string Name, Action<ThemeHost<ProgressTab>> Do)[]
        {
            ("toggle", h => h.ToggleCard()),
            ("popOut", h => h.PopOut()),
            ("openWindow", h => h.OpenWindow()),
            ("windowClosed", h => h.WindowClosed()),
            ("reset", h => h.Reset()),
        };

        // Every ordered triple of actions: 125 sequences, which covers every path through
        // three states without hand-picking the ones that look interesting.
        foreach (var a in actions)
            foreach (var b in actions)
                foreach (var c in actions)
                {
                    host.Reset();
                    foreach (var step in new[] { a, b, c })
                    {
                        step.Do(host);
                        Assert.False(host.IsInline && host.IsWindowOpen,
                            $"after {a.Name} → {b.Name} → {c.Name}: the body has two owners");
                        if (host.ShouldBringWindowForward)
                            Assert.True(host.IsWindowOpen,
                                $"after {a.Name} → {b.Name} → {c.Name}: asked to bring a window "
                                + "forward that is not open");
                    }
                }
    }
}
