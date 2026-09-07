using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// THE OWNER'S TEN LOCKS, as assertions (OE-1; `BEVEL.md` §4's owner interview, Helm-signed
/// at #347/#348).
///
/// Seven of the ten are state rules and every one of them arrived as a SENTENCE — "one
/// under-bar expansion at a time", "close floated window → just the mini-bar" — rather than
/// as code. A sentence in a channel file is exactly the kind of rule that rots silently: the
/// WPF layer has no unit tests (docs/TestPlan.md §5), so a rule living in a mouse handler is
/// a rule nothing can check, and the next executor reads the handler rather than the lock.
/// One test per lock, named for the lock, is what makes that impossible.
///
/// The other three are not state: lock 2 (chips look like buttons) and lock 10 (motion) are
/// the view's, and lock 8 (ship DPS → HPS → Progress and stop) is the ENUM — which is why
/// <see cref="EveryTargetHasAKeyThatReadsBackToIt"/> asserts the membership out loud rather
/// than leaving it to whoever adds the fourth.
/// </summary>
public class HudExpandTests
{
    [Fact]
    public void Lock3_HoverPeeksAndMouseAwayCollapses()
    {
        var expand = new HudExpand();
        Assert.Equal(ThemePlacement.Collapsed, expand.Placement);

        expand.Hover(HudExpandTarget.Dps);
        Assert.True(expand.IsInline);
        Assert.Equal(HudExpandTarget.Dps, expand.Target);
        // A peek is NOT a pin — the whole distinction lock 4 exists to make.
        Assert.False(expand.Pinned);
        Assert.Equal("peek", expand.ModeKey);

        expand.Away();
        Assert.Equal(ThemePlacement.Collapsed, expand.Placement);
        Assert.Equal("none", expand.TargetKey);
    }

    [Fact]
    public void Lock4_ClickStaysOpenThroughMouseAway()
    {
        var expand = new HudExpand();
        expand.Hover(HudExpandTarget.Dps);
        expand.Click(HudExpandTarget.Dps);
        Assert.True(expand.Pinned);
        Assert.Equal("pinned", expand.ModeKey);

        expand.Away();
        Assert.True(expand.IsInline);
        Assert.True(expand.Pinned);
        Assert.Equal(HudExpandTarget.Dps, expand.Target);
    }

    /// <summary>A second click on the pinned chip is the way back out. ThemeHost's
    /// <c>ToggleCard</c> already means "the launcher was clicked"; a chip that could only
    /// ever open would need the ✕ to be the only exit, which is a target the size of a
    /// glyph over a running game.</summary>
    [Fact]
    public void Lock4_ClickingThePinnedChipAgainClosesIt()
    {
        var expand = new HudExpand();
        expand.Click(HudExpandTarget.Hps);
        Assert.True(expand.Pinned);

        expand.Click(HudExpandTarget.Hps);
        Assert.Equal(ThemePlacement.Collapsed, expand.Placement);
        Assert.Equal("collapsed", expand.ModeKey);
    }

    [Fact]
    public void Lock1_PinningASecondTrackerReplacesTheFirst()
    {
        var expand = new HudExpand();
        expand.Click(HudExpandTarget.Dps);
        expand.Click(HudExpandTarget.Progress);

        Assert.True(expand.IsInline);
        Assert.Equal(HudExpandTarget.Progress, expand.Target);
        Assert.True(expand.Pinned);
        // There is no second placement to inspect — which IS the lock. What can be
        // asserted is that going away does not restore the one it replaced.
        expand.Away();
        Assert.Equal(HudExpandTarget.Progress, expand.Target);
    }

    /// <summary>A hover over another chip while one is pinned shows the hovered one and
    /// gives the pin back on the way out. Lock 9 forbids a tracker that stops answering a
    /// hover, and "pinned so the rest of the bar is inert" would be exactly that.</summary>
    [Fact]
    public void Lock3And4_APeekOverAPinnedPanelRevertsToThePin()
    {
        var expand = new HudExpand();
        expand.Click(HudExpandTarget.Dps);

        expand.Hover(HudExpandTarget.Hps);
        Assert.Equal(HudExpandTarget.Hps, expand.Target);
        Assert.False(expand.Pinned);          // showing a peek, not the pin

        expand.Away();
        Assert.Equal(HudExpandTarget.Dps, expand.Target);
        Assert.True(expand.Pinned);
    }

    [Fact]
    public void Lock5_TheXOnThePanelCollapsesBackToTheBar()
    {
        var expand = new HudExpand();
        expand.Click(HudExpandTarget.Dps);
        expand.Collapse();

        Assert.Equal(ThemePlacement.Collapsed, expand.Placement);
        // And the pin went with it: a stray hover must not bring it back PINNED.
        expand.Hover(HudExpandTarget.Dps);
        Assert.False(expand.Pinned);
        expand.Away();
        Assert.Equal(ThemePlacement.Collapsed, expand.Placement);
    }

    [Fact]
    public void Lock6_PopOutCollapsesTheUnderBarPanel()
    {
        var expand = new HudExpand();
        expand.Click(HudExpandTarget.Dps);
        expand.PopOut();

        Assert.True(expand.IsWindowOpen);
        Assert.False(expand.IsInline);
        Assert.Equal("window", expand.ModeKey);
        Assert.Equal("dps", expand.TargetKey);
    }

    /// <summary>While the float is up the bar draws nothing for it — the float IS the
    /// detail. This is ThemeHost's one invariant reaching the bar: two owners of one body
    /// is a layout bug here and was a crash on the lane that is gone.</summary>
    [Fact]
    public void Lock6_HoveringTheSameChipWhileItsFloatIsUpDrawsNoPanel()
    {
        var expand = new HudExpand();
        expand.Click(HudExpandTarget.Dps);
        expand.PopOut();

        expand.Hover(HudExpandTarget.Dps);
        Assert.False(expand.IsInline);
        Assert.True(expand.IsWindowOpen);

        expand.Click(HudExpandTarget.Dps);
        Assert.True(expand.ShouldBringWindowForward);
        Assert.False(expand.IsInline);
    }

    [Fact]
    public void Lock7_ClosingTheFloatLeavesNothingExpanded()
    {
        var expand = new HudExpand();
        expand.Click(HudExpandTarget.Dps);
        expand.PopOut();
        expand.WindowClosed(HudExpandTarget.Dps);

        // Collapsed, never silently back to Inline — ThemeHost's own rule, and the reason
        // this delegates rather than re-deciding.
        Assert.Equal(ThemePlacement.Collapsed, expand.Placement);
        Assert.Equal("none", expand.TargetKey);
        Assert.False(expand.Pinned);
    }

    /// <summary>The half a keyless <c>WindowClosed()</c> would have got wrong: a ✕ on a
    /// float the bar has moved on from must not collapse what the bar is showing NOW.
    /// Nothing in a diff or a screenshot says which of the two a call means.</summary>
    [Fact]
    public void Lock7_ClosingAFloatTheBarHasMovedOnFromLeavesThePanelAlone()
    {
        var expand = new HudExpand();
        expand.Click(HudExpandTarget.Dps);
        expand.PopOut();
        // A different chip while the float is up: a fresh expansion; the float stays an
        // ordinary window with its own ✕.
        expand.Click(HudExpandTarget.Hps);
        Assert.True(expand.IsInline);
        Assert.Equal(HudExpandTarget.Hps, expand.Target);

        expand.WindowClosed(HudExpandTarget.Dps);
        Assert.True(expand.IsInline);
        Assert.Equal(HudExpandTarget.Hps, expand.Target);
        Assert.True(expand.Pinned);
    }

    /// <summary>Leaving the collapsed HUD takes the panel with it. The bar is the panel's
    /// only anchor, and a slaved companion left parked under an expanded widget is trap 12's
    /// mechanism wearing a stale window.</summary>
    [Fact]
    public void ResetPutsItBackToFirstRun()
    {
        var expand = new HudExpand();
        expand.Click(HudExpandTarget.Progress);
        expand.Reset();

        Assert.Equal(ThemePlacement.Collapsed, expand.Placement);
        Assert.Equal("collapsed", expand.ModeKey);
        Assert.False(expand.Pinned);
    }

    /// <summary>Lock 8's membership, said out loud, plus the key round trip. A key that
    /// reads one way only is how <c>EQBUDDY_HUDEXPAND=hps</c> silently opens DPS.</summary>
    [Fact]
    public void EveryTargetHasAKeyThatReadsBackToIt()
    {
        var targets = Enum.GetValues<HudExpandTarget>();
        Assert.Equal(
            [HudExpandTarget.Dps, HudExpandTarget.Hps, HudExpandTarget.Progress],
            targets);

        foreach (var target in targets)
        {
            var key = HudExpand.Key(target);
            Assert.Equal(target, HudExpand.TargetForKey(key));
            Assert.Equal(target, HudExpand.TargetForKey(key.ToUpperInvariant()));
            // One word: the dump is space-separated key=value.
            Assert.DoesNotContain(' ', key);
            Assert.NotEmpty(HudExpand.Title(target));
            Assert.NotEmpty(HudExpand.Icon(target));
        }

        Assert.Null(HudExpand.TargetForKey("kills"));
        Assert.Null(HudExpand.TargetForKey(null));
        // "xp" is the HUD's own word for the slot Progress owns, so it reads too.
        Assert.Equal(HudExpandTarget.Progress, HudExpand.TargetForKey("xp"));
    }

    /// <summary>Progress pops to the Progress WINDOW, and the tooltip has to say so.
    /// <c>Progress</c> left <c>BreakoutKind</c> by a signed fold on 2026-08-25 and
    /// <c>DocumentationSizeTests</c> pins that list; a pop-out that named a float would be
    /// the first step back toward reverting it.</summary>
    [Fact]
    public void ProgressPopsToTheProgressWindowAndTheTooltipSaysSo()
    {
        Assert.Contains("Progress window", HudExpand.PopOutTip(HudExpandTarget.Progress));
        Assert.Contains("floating", HudExpand.PopOutTip(HudExpandTarget.Dps));
        Assert.Contains("floating", HudExpand.PopOutTip(HudExpandTarget.Hps));
    }
}
