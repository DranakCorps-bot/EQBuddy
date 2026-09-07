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
    /// reads one way only is how <c>EQBUDDY_HUDEXPAND=hps</c> silently opens DPS.
    ///
    /// **TWELVE since OE-9**, which is lock 9 arriving in full rather than lock 8 being
    /// broken: three shipped first so the owner could test the mechanics, OE-7 added the four
    /// floating-window kinds whose ✕ had nowhere else to record a dismissal, and the owner's
    /// ~1:29 PM CT amend (2026-09-07) added the rest of the tray — *"everything on the
    /// minimized bar MUST have hover peek + pop-out"*. There is no cell left that answers a
    /// hover with nothing, which is the state lock 9 forbids.</summary>
    [Fact]
    public void EveryTargetHasAKeyThatReadsBackToIt()
    {
        var targets = Enum.GetValues<HudExpandTarget>();
        Assert.Equal(
            [
                HudExpandTarget.Dps, HudExpandTarget.Hps, HudExpandTarget.Progress,
                HudExpandTarget.Pet, HudExpandTarget.Watch, HudExpandTarget.Loot,
                HudExpandTarget.Buffs, HudExpandTarget.Motes, HudExpandTarget.Kills,
                HudExpandTarget.Procs, HudExpandTarget.Money, HudExpandTarget.Deaths,
            ],
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

        Assert.Null(HudExpand.TargetForKey("nonsense"));
        Assert.Null(HudExpand.TargetForKey(null));
        // "xp" is the HUD's own word for the slot Progress owns, so it reads too.
        Assert.Equal(HudExpandTarget.Progress, HudExpand.TargetForKey("xp"));
    }

    /// <summary>
    /// **THE OWNER'S ~1:29 PM CT AMEND AS ONE ASSERTION: every cell on the minimized bar
    /// expands.**
    ///
    /// <c>TargetForKey</c> is the BRIDGE <c>HudBarView</c> crosses to turn a
    /// <see cref="MiniBarCell"/> into an expansion chip, and the hand switch it replaced is
    /// what made this checkable at all: a cell key with no target is now a chip with no hover,
    /// no ⧉ and nothing to say what it is. Read out of
    /// <see cref="MiniBarPresentation.Order"/> rather than typed out again, because a list
    /// retyped here would stop covering that table the day it grows (trap 30) — which is
    /// exactly how "deaths" came to be the one cell the signed plan left behind.
    ///
    /// The TITLE and ICON are asserted against the cell's own, not merely as non-empty: the
    /// panel a chip opens must not be a different word for the same stat.
    /// </summary>
    [Fact]
    public void EveryMiniBarCellHasAnExpansionTarget()
    {
        Assert.NotEmpty(MiniBarPresentation.Order);
        foreach (var key in MiniBarPresentation.Order)
        {
            var target = HudExpand.TargetForKey(key);
            Assert.True(target is not null,
                $"the '{key}' cell has no HudExpandTarget, so its chip cannot peek or pop out");
            Assert.Equal(key, HudExpand.Key(target!.Value));
            Assert.Equal(MiniBarPresentation.Names[key], HudExpand.Title(target.Value));
            Assert.Equal(MiniBarPresentation.Icons[key], HudExpand.Icon(target.Value));
        }
    }

    /// <summary>
    /// **THE TRAP-64 MAP, TOTAL OVER THE ENUM.**
    ///
    /// <c>HudExpandBar</c> used to route a ⧉ by reading *"no breakout name → the Progress
    /// window"*, which was EXACT while Progress was the only non-float destination — the
    /// absence was standing in for a fact — and would have sent Kills and Deaths to Progress
    /// with no line of that method changing. That is the same shape as the ternary OE-7
    /// replaced, one level up, in the same file whose own doc comment tells the first story.
    ///
    /// A destination that answered NOTHING is the failure this asserts against: a target with
    /// no window is a chip whose ⧉ is a silent no-op, and every one of the five OE-9 members
    /// arrived without a float to fall back on.
    /// </summary>
    [Fact]
    public void EveryTargetKnowsWhichWindowItsPopOutOpens()
    {
        foreach (var target in Enum.GetValues<HudExpandTarget>())
        {
            var destination = HudExpand.DestinationOf(target);
            Assert.NotEmpty(HudExpand.Words(destination));
            // A float names its BreakoutKind member and the words it borrows; a window
            // destination names neither, and mixing the two is how a Progress route would
            // try to Enum.Parse itself into a float that does not exist.
            if (destination.Host == HudDestinationHost.Float)
            {
                Assert.NotNull(destination.BreakoutName);
                Assert.NotEmpty(BreakoutPresentation.Title(destination.FloatKind!));
            }
            else
            {
                Assert.Null(destination.BreakoutName);
                Assert.Null(destination.FloatKind);
            }
            // The pop-out tooltip names the DESTINATION, never the chip (#233 in a hover).
            Assert.Equal($"Open {HudExpand.Words(destination)}", HudExpand.PopOutTip(target));
        }

        // The five routes OE-9 decided, spelled out — a map is only worth having if the rows
        // someone has to argue about are legible.
        Assert.Equal(HudDestinationHost.ProgressWindow,
            HudExpand.DestinationOf(HudExpandTarget.Motes).Host);
        Assert.Equal("wealth", HudExpand.DestinationOf(HudExpandTarget.Motes).Tab);
        Assert.Equal("wealth", HudExpand.DestinationOf(HudExpandTarget.Money).Tab);
        // Progress itself opens on WHATEVER TAB IT WAS LEFT ON — the 2026-08-25 fold, and the
        // reason a null tab is a value here rather than a gap.
        Assert.Null(HudExpand.DestinationOf(HudExpandTarget.Progress).Tab);
        Assert.Equal(HudDestinationHost.CreatureWindow,
            HudExpand.DestinationOf(HudExpandTarget.Kills).Host);
        Assert.Equal(HudDestinationHost.WorldWindow,
            HudExpand.DestinationOf(HudExpandTarget.Deaths).Host);
        // Procs shares the DAMAGE float rather than getting a tenth always-on-top window.
        Assert.Equal("Damage", HudExpand.DestinationOf(HudExpandTarget.Procs).BreakoutName);
    }

    /// <summary>
    /// **LOCK 7 IS DESTINATION-KEYED BECAUSE TWO TARGETS CAN SHARE A WINDOW**, which is new
    /// with OE-9 and is the reason the close handler could not stay keyed on the breakout
    /// kind. Dps and Procs both mean the Damage float; Progress, Motes and Money all mean the
    /// Progress window. A ✕ on one of those closes the window for both, and the TAB must not
    /// enter into it — a player who closed the Progress window closed it whichever tab they
    /// had wandered to.
    /// </summary>
    [Fact]
    public void TwoTargetsThatShareAWindowShareItsClose()
    {
        var dps = HudExpand.DestinationOf(HudExpandTarget.Dps);
        var procs = HudExpand.DestinationOf(HudExpandTarget.Procs);
        Assert.True(HudExpand.SameWindow(dps, procs));

        var progress = HudExpand.DestinationOf(HudExpandTarget.Progress);
        var money = HudExpand.DestinationOf(HudExpandTarget.Money);
        Assert.True(HudExpand.SameWindow(progress, money));   // different tabs, one window

        // And the negatives, or the comparison is vacuous (trap 39): the Healing float is not
        // the Damage float, and a theme window is not a float at all.
        Assert.False(HudExpand.SameWindow(dps, HudExpand.DestinationOf(HudExpandTarget.Hps)));
        Assert.False(HudExpand.SameWindow(progress,
            HudExpand.DestinationOf(HudExpandTarget.Kills)));
        Assert.False(HudExpand.SameWindow(dps, progress));
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

    /// <summary>
    /// **THE OE-7 SEAT, AS ONE ASSERTION: every floating-window kind has a chip, and every
    /// chip knows which window it pops to.**
    ///
    /// It is the premise the transient ✕ is built on. A ✕ that stops writing
    /// <c>DisabledBreakouts</c> is safe exactly as long as the kind it closed can be summoned
    /// back from the bar; a kind that answered <see cref="HudExpand.TargetForBreakout"/> with
    /// null would be a float with no door, which is discussion #45's whack-a-mole reached from
    /// the other direction. Nothing else in the repo can say this: the enum lives in the WPF
    /// layer, which has no unit tests, so the membership is read out of it by reflection here
    /// rather than typed out again.
    ///
    /// The pairing is asserted in BOTH directions on purpose. <c>HudExpandBar</c> routes a ⧉
    /// through <see cref="HudExpand.BreakoutName"/>, and a name that mapped forward but not
    /// back is how a Loot chip's ⧉ opens the Damage float — a wrong window that renders
    /// perfectly, which is what the ternary it replaced would have done for all four of
    /// these (trap 64).
    ///
    /// The kinds are read out of the WPF SOURCE, the way <c>DocumentationSizeTests</c> reads
    /// them: this project does not reference the widget (it is a Windows-only WPF exe), and a
    /// list retyped here would stop covering the enum the day it grows, which is the one
    /// thing trap 30 says about this exact enum.
    ///
    /// **The reverse lookup is COMPUTED from the destination map since OE-9**, where it used
    /// to be a second hand-written table (<c>TargetForBreakout</c>). That table retired for
    /// the reason it existed: a name that mapped forward but not back is how a Loot chip's ⧉
    /// opens the Damage float, and two tables is one more place for that to happen than the
    /// zero this now has.
    /// </summary>
    [Fact]
    public void EveryFloatingWindowKindHasAChipAndEveryChipKnowsItsWindow()
    {
        var names = BreakoutKindNames();
        Assert.Equal(["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"], names);

        foreach (var name in names)
        {
            var summoners = Enum.GetValues<HudExpandTarget>()
                .Where(t => HudExpand.BreakoutName(t) == name).ToList();
            Assert.True(summoners.Count > 0,
                $"{name} has no HUD chip to be summoned from, so its ✕ would be a one-way trap");
        }

        // Damage is the one float TWO chips summon (Dps and Procs), which is what made lock
        // 7 destination-keyed — see TwoTargetsThatShareAWindowShareItsClose.
        Assert.Equal(
            [HudExpandTarget.Dps, HudExpandTarget.Procs],
            Enum.GetValues<HudExpandTarget>().Where(t => HudExpand.BreakoutName(t) == "Damage"));

        // Progress is a target that is NOT a float, and the negative is what keeps this from
        // going vacuous: it left BreakoutKind by a signed fold on 2026-08-25, and a
        // BreakoutName for it would be the first step back toward reverting that. Kills and
        // Deaths join it for a different reason — their windows never were floats.
        Assert.Null(HudExpand.BreakoutName(HudExpandTarget.Progress));
        Assert.Null(HudExpand.BreakoutName(HudExpandTarget.Kills));
        Assert.Null(HudExpand.BreakoutName(HudExpandTarget.Deaths));
        Assert.DoesNotContain("Progress", names);
    }

    /// <summary>Title and icon read ONE kind mapping, so a target cannot be named as one
    /// surface and drawn as another. They were three parallel switches until OE-7 — which is
    /// fine at three members and is three chances to miss one at twelve.
    ///
    /// **The five OE-9 targets answer null and take the CELL's words instead**
    /// (<see cref="EveryMiniBarCellHasAnExpansionTarget"/> asserts that half). The null is
    /// load-bearing: Procs pops to the Damage float, so reading its words off its DESTINATION
    /// would have put "Your damage" and a sword on the weapon-procs chip.</summary>
    [Fact]
    public void EveryTargetsWordsAndVectorComeFromTheSameKind()
    {
        foreach (var target in Enum.GetValues<HudExpandTarget>())
        {
            if (HudExpand.KindOf(target) is not { } kind)
            {
                // No float, so no BreakoutPresentation words — MiniBarPresentation's instead.
                Assert.Equal(MiniBarPresentation.Names[HudExpand.Key(target)],
                    HudExpand.Title(target));
                continue;
            }
            Assert.Equal(BreakoutPresentation.Title(kind), HudExpand.Title(target));
            Assert.Equal(BreakoutPresentation.Icon(kind), HudExpand.Icon(target));
            // Only Dps may be "damage": KindOf has no fallback any more, but a hand-written
            // switch is one typo from giving Watch a sword.
            if (target != HudExpandTarget.Dps)
                Assert.NotEqual(BreakoutPresentation.Damage, kind);
        }

        // And the five that answer null are exactly the five, so a future target cannot join
        // them by being forgotten (trap 20 — the thing you are looking for is what is not
        // there).
        Assert.Equal(
            [HudExpandTarget.Motes, HudExpandTarget.Kills, HudExpandTarget.Procs,
             HudExpandTarget.Money, HudExpandTarget.Deaths],
            Enum.GetValues<HudExpandTarget>().Where(t => HudExpand.KindOf(t) is null));
    }

    /// <summary><c>BreakoutKind</c>'s members, out of the WPF source. Same regex
    /// <c>DocumentationSizeTests</c> uses, and it fails loudly rather than returning an empty
    /// set if the enum moves — a scan that silently found nothing would make the assertions
    /// above pass over zero kinds (trap 34).</summary>
    private static string[] BreakoutKindNames()
    {
        var repo = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var source = File.ReadAllText(
            Path.Combine(repo, "src", "EQBuddy", "BreakoutWindow.xaml.cs"));
        var match = System.Text.RegularExpressions.Regex.Match(
            source, @"enum\s+BreakoutKind\s*\{(?<members>[^}]*)\}");
        Assert.True(match.Success,
            "BreakoutKind is no longer declared in EQBuddy/BreakoutWindow.xaml.cs");
        return [.. match.Groups["members"].Value
            .Split(',').Select(p => p.Trim()).Where(p => p.Length > 0)];
    }
}
