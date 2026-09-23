using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **SR-3's lift, guarded from the only side a test can reach it.**
///
/// The `cards` tab's whole body left `OptionsWindow.xaml(.cs)` for host-neutral
/// `SettingsHudView`, so the Evolved shell's Settings room can compose the SAME controls under
/// its own "HUD" tab instead of growing a second copy that drifts against them. The WPF layer
/// has no unit tests (docs/TestPlan.md §5), so what CAN be asserted is the source: that every
/// control really arrived somewhere, that the three stray SWITCHES kept their write paths, and
/// that the renamed heading did not leave a route pointing at a screen that no longer says it.
///
/// **The enumeration below is trap 26 written as code**, the shape
/// <see cref="SettingsAlertsBlockTests"/> established and <see cref="SettingsLookBehaviorBlockTests"/>
/// repeated. "When you fold a surface, list every control on it and say where each one went" —
/// the same event that produced #204, #210 and #212, every one of them a surface whose DATA
/// survived a move while its write path did not. A list in a commit message is read once; this
/// one fails the build when a row stops being true.
///
/// **This tab is the one where that risk is real rather than ceremonial**, because it is the
/// only one of the four whose contents were mostly XAML: a `Checked=` handler declared in
/// markup does not move with its control, it is simply left behind, and a tick box that renders
/// and saves nothing is the "silent no-ops are broken" rule with the switch on the other side.
/// </summary>
public class SettingsHudBlockTests
{
    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Src, "EQBuddy", relative));

    private static string Block => Read("SettingsHudView.cs");
    private static string WindowXaml => Read("OptionsWindow.xaml");
    private static string WindowCode => Read("OptionsWindow.xaml.cs");

    /// <summary>
    /// Every control the `cards` tab declared, and the token in the lifted block that proves
    /// it landed. A row asserts BOTH halves: the `x:Name` is gone from the window's XAML (so
    /// nothing is quietly declared twice) and the block builds the same control.
    ///
    /// Some rows point at a piece of TEXT rather than a field — a control whose only identity
    /// is its caption. A row that cannot fail is not a guard (trap 39), so every token here was
    /// checked against the pre-lift tree before it was written down.
    /// </summary>
    public static readonly (string WasNamed, string Went, string NowBuiltAs)[] Enumeration =
    [
        // ---- the panel list, and the two lists that hang off it
        ("CardsPanel", "HUD block", "_cards"),
        // The "No longer on the widget" rows (BuildRetired, OverlaySections.Retired*) left
        // with the block itself in DRA-352 D2 — Founder direction, Helm LOCKED the drop.
        // `CardsTabResidualProseTests.TheBlocksD2CutStayCut` is their must-NOT half.
        ("CardsPanel", "HUD block", "card.Absorbed"),
        // The three row buttons. Their tooltips said "card" and were reworded with the rest.
        ("CardsPanel", "HUD block", "CardButton("),
        ("CardsPanel", "HUD block", "Move up"),
        ("CardsPanel", "HUD block", "Hide this panel (data still collected)"),

        // ---- the minimised-HUD tick boxes
        ("MiniStatsPanel", "HUD block", "_miniStats"),
        // **OptionKeys and not Order since DRA-81's Founder LOCK.** `Order` is a formatting
        // table for CELLS, and walking it here is precisely how SA-1 left three ★s in the
        // profile that no screen could offer — the hole the Founder's smoke fell into. This
        // row is what stops a later tidy-up "simplifying" it back to the shorter list.
        ("MiniStatsPanel", "HUD block", "MiniBarPresentation.OptionKeys"),
        ("MiniStatsPanel", "HUD block", "MiniBarPresentation.Names"),
        ("MiniStatsPanel", "HUD block", "_main.SetMiniStat("),
        // The heading and its blurb. The two notes under the list (PromotedStatsNote,
        // GlancePetNote) and the Restore default order button left in DRA-352 D2 by Founder
        // direction; MiniBarOrder keeps its one writer, the bar drag.
        ("MiniStatsPanel", "HUD block", "HudStatsHeading"),
        ("MiniStatsPanel", "HUD block", "HudStatsBlurb"),

        // ---- the floating-window tick boxes LEFT in DRA-352 D2, and their writer MOVED
        // first: the pin on each floating window (BreakoutAutoOpen via
        // BreakoutHost.SetAutoOpen) is the one writer of DisabledBreakouts now.
        // `TheFloatingWindowsWriterMovedBeforeTheListWent` below is that row.

        // ---- the three strays. These are the rows this file exists for: each one had its
        // handler declared in XAML, which is the half that does not travel with a control.
        ("DoubleClickChipsCheck", "HUD block", "_doubleClickChips"),
        ("DoubleClickChipsCheck", "HUD block", "DoubleClickChipsToggleBreakouts ="),
        ("DoubleClickChipsCheck", "HUD block", "DoubleClickChipsBlurb"),
        ("TargetDropsCheck", "HUD block", "_targetDrops"),
        ("TargetDropsCheck", "HUD block", "_vm.ShowTargetDrops ="),
        ("TargetDropsCheck", "HUD block", "TargetDropsBlurb"),
        ("WindowCombo", "HUD block", "_recentWindow"),
        ("WindowCombo", "HUD block", "OptionsViewModel.WindowChoices"),
        ("WindowCombo", "HUD block", "_vm.RecentWindowIndex ="),
        ("WindowCombo", "HUD block", "RecentRateBlurb"),
    ];

    public static TheoryData<string, string, string> EnumerationRows()
    {
        var rows = new TheoryData<string, string, string>();
        foreach (var (was, went, now) in Enumeration) rows.Add(was, went, now);
        return rows;
    }

    [Theory]
    [MemberData(nameof(EnumerationRows))]
    public void EveryLiftedControlLandedInTheBlockAndLeftTheWindow(
        string wasNamed, string went, string nowBuiltAs)
    {
        Assert.DoesNotContain($"x:Name=\"{wasNamed}\"", WindowXaml, StringComparison.Ordinal);
        Assert.True(Block.Contains(nowBuiltAs, StringComparison.Ordinal),
            $"{wasNamed} was lifted to the {went} and SettingsHudView.cs no longer builds it "
            + $"(looked for \"{nowBuiltAs}\"). A control that left one surface and arrived at "
            + "none is the shape of #204, #210 and #212 — the data survives the move and the "
            + "write path does not, and nothing else in this repo can see it.");
    }

    /// <summary>
    /// **The three stray switches kept their WRITE paths, and the window kept none of them.**
    /// Their handlers were XAML attributes (`Checked="OnTargetDropsToggled"`), which is the one
    /// kind of wiring that a control does not carry with it when it is rebuilt in code: the
    /// control moves, the attribute is deleted with the markup, and what ships is a tick box
    /// that renders perfectly and saves nothing. Trap 20's polarity, arriving through a lift
    /// instead of through a fold.
    /// </summary>
    [Theory]
    [InlineData("OnTargetDropsToggled")]
    [InlineData("OnDoubleClickChipsToggled")]
    [InlineData("OnWindowChanged")]
    public void TheWindowNoLongerOwnsAnyOfTheStraySwitches(string handler)
    {
        Assert.DoesNotContain(handler, WindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain($"void {handler}(", WindowCode, StringComparison.Ordinal);
    }

    /// <summary>
    /// The write itself, not just a field with the right name. Each of the three ends in a
    /// PERSIST — `Settings.Save()` for the raw one, the view-model setter for the two it owns —
    /// and a lift that dropped the persist would look identical in every screenshot and every
    /// build.
    /// </summary>
    [Fact]
    public void EachStraySwitchStillPersists()
    {
        Assert.Contains("_main.Settings.Save();", Block, StringComparison.Ordinal);
        Assert.Contains("_vm.ShowTargetDrops = _targetDrops.IsChecked == true", Block,
            StringComparison.Ordinal);
        Assert.Contains("_vm.RecentWindowIndex = _recentWindow.SelectedIndex", Block,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// **Every handler in the block is closed until the HOST says it is ready.** A checkbox
    /// assigned during construction raises `Checked` exactly as a player's click does, and the
    /// window builds this block inside its own constructor — so without the gate, merely
    /// OPENING Options would write all three settings back and save. The gate is the host's
    /// callback rather than a flag of the block's own, because only the host knows when it has
    /// finished building.
    /// </summary>
    [Fact]
    public void EveryWriteIsBehindTheHostReadyGate()
    {
        Assert.Contains("private bool Ready => _hostReady();", Block, StringComparison.Ordinal);

        // Each of the three, by its own write, with the gate on the same side of it. Counting
        // gates would pass for a block that gated three OTHER things (trap 34's shape); this
        // asks the question per switch.
        Assert.Matches(
            @"if \(!Ready\) return;\s*_main\.Settings\.DoubleClickChipsToggleBreakouts =", Block);
        Assert.Contains("if (Ready) _vm.ShowTargetDrops =", Block, StringComparison.Ordinal);
        Assert.Contains("if (Ready) _vm.RecentWindowIndex =", Block, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trap 13 as a constructor contract: the block does not load settings for itself. Both
    /// hosts wrap the ONE <c>AppSettings</c> the widget holds, because a second snapshot
    /// clobbers the first one wholesale on its next save — which is exactly how "my tick-boxes
    /// won't stay ticked" (#169) presents, with nothing on screen to say so.
    /// </summary>
    [Fact]
    public void TheBlockDoesNotLoadItsOwnSettings() =>
        Assert.DoesNotContain("AppSettings.Load", Block, StringComparison.Ordinal);

    /// <summary>
    /// Trap 15: the lifted view carries its own spacing and the host it hangs in gets no state
    /// of its own. The tab panel's `Visibility` is the exception and is not state the block
    /// knows about — it belongs to this window's tab machinery, which `SelectTab` drives.
    /// </summary>
    [Fact]
    public void TheHostIsBare() =>
        Assert.Contains("<StackPanel x:Name=\"TabCardsPanel\" Visibility=\"Collapsed\"/>",
            WindowXaml, StringComparison.Ordinal);

    /// <summary>
    /// Trap 45, the standing rule for every block in this family: a host builds its OWN
    /// instance. A WPF <c>UIElement</c> has exactly one parent, so a block borrowed from
    /// another host is torn out of whichever painted it last — silently, on WPF, which is the
    /// harder failure to notice rather than the easier one.
    /// </summary>
    [Fact]
    public void TheWindowBuildsItsOwnInstance()
    {
        Assert.Contains("new SettingsHudView(_main, _vm, () => _ready, FindResource)",
            WindowCode, StringComparison.Ordinal);
        // And it does not hand its instance out. A method returning a long-lived UI object is
        // a transfer of ownership wearing a getter's clothes.
        Assert.DoesNotContain("internal SettingsHudView", WindowCode, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The v1 TAB LABEL did not change, and that is the signed half of item 4.** The
    /// terminology ban's own scope line exempts v1 `OptionsWindow`, and renaming shipped v1
    /// copy for no player benefit is the #228 class — mjtrainor's "stop changing every feature
    /// and its location every release". The headings INSIDE the block did change, because the
    /// block serves a host the ban does apply to; the strip above it is the window's.
    /// </summary>
    [Fact]
    public void TheV1TabKeepsItsShippedLabel()
    {
        Assert.Contains("x:Name=\"TabCards\" Text=\"Cards &amp; windows\"", WindowXaml,
            StringComparison.Ordinal);
        // And the block declares no tab name of its own. "HUD" is SR-5's word, spelled where
        // the room composes the four blocks — a label declared here would be a second source
        // for one string the day the room disagreed with it (trap 4). The block's doc comment
        // NAMES the word, on purpose, so whoever writes `SettingsSurface` reads it; what must
        // not exist is an assignment.
        Assert.DoesNotContain("= \"HUD\"", Block, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The ✕'s tooltip is a ROUTE, and DRA-352 D2 moved what it routes to.** Until then it
    /// printed "Options → " + the Floating windows heading, derived from the const so a rename
    /// could not strand it (#219's mechanism inside one sentence). The list is gone — the
    /// Founder asked for it off Options — and the switch it held is the pin beside the ✕, so
    /// the route now names the pin and no Options screen at all.
    ///
    /// **The ✕ is still a transient close (OE-7)**, so the negative that keeps it from
    /// promising a permanent one stays: "for good" is the exact phrase it shipped with.
    /// </summary>
    [Fact]
    public void TheDismissTipNamesThePinThatReplacedTheList()
    {
        Assert.Contains("pin", BreakoutPresentation.DismissTip, StringComparison.Ordinal);
        Assert.DoesNotContain("Options", BreakoutPresentation.DismissTip, StringComparison.Ordinal);
        Assert.DoesNotContain("for good", BreakoutPresentation.DismissTip,
            StringComparison.OrdinalIgnoreCase);

        // The window prints it from the const, never a literal.
        Assert.Contains("DismissIcon.ToolTip = BreakoutPresentation.DismissTip",
            Read("BreakoutWindow.xaml.cs"), StringComparison.Ordinal);

        // The negative that keeps it from growing back: nothing may route a player to either
        // heading the list ever had, in any of the files that used to.
        foreach (var file in new[]
                 {
                     "MainWindow.xaml.cs", "BreakoutWindow.xaml", "BreakoutWindow.xaml.cs",
                     "BreakoutHost.cs", "SettingsHudView.cs",
                 })
        {
            Assert.DoesNotContain("Options → Breakout windows", Read(file), StringComparison.Ordinal);
            Assert.DoesNotContain("Options → Floating windows", Read(file), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// **Trap 20/26 for DRA-352 D2: the Floating windows list was the ONE writer of
    /// <c>DisabledBreakouts</c>, so the write had to arrive somewhere before the list could
    /// leave.** It arrived on the window: the pin raises <c>AutoOpenChanged</c>, the host's
    /// <c>SetAutoOpen</c> calls <c>BreakoutAutoOpen.Set</c> and saves. Asserted as the CHAIN,
    /// link by link, because each link failing alone ships a pin that draws and writes
    /// nothing. The rule's own arithmetic is <c>BreakoutAutoOpenTests</c>.
    /// </summary>
    [Fact]
    public void TheFloatingWindowsWriterMovedBeforeTheListWent()
    {
        var window = Read("BreakoutWindow.xaml.cs");
        var host = Read("BreakoutHost.cs");
        Assert.Contains("MouseLeftButtonDown=\"OnAutoOpenPin\"", Read("BreakoutWindow.xaml"),
            StringComparison.Ordinal);
        Assert.Contains("AutoOpenChanged?.Invoke(_kind, !BreakoutAutoOpen.IsOn(", window,
            StringComparison.Ordinal);
        Assert.Contains("w.AutoOpenChanged += SetAutoOpen;", host, StringComparison.Ordinal);
        Assert.Contains("if (BreakoutAutoOpen.Set(settings, kind.ToString(), on)) settings.Save();",
            host, StringComparison.Ordinal);
        // And the gate reads the same rule the pin writes — one producer (trap 4).
        Assert.Contains("return BreakoutAutoOpen.IsOn(settings, kind.ToString())", host,
            StringComparison.Ordinal);
        // The ✕ still writes nothing (OE-7): the pin is the ONLY writer, not a second one.
        Assert.DoesNotContain("DisabledBreakouts.Add", window + host, StringComparison.Ordinal);
    }

    /// <summary>
    /// **The six reworded sentences, pinned to the ban rather than to my taste.**
    /// <see cref="ShellTerminologyTests"/> already scans this file and would fail on any of
    /// them; what it cannot say is that the sentence is still THERE. A rewrite that deleted the
    /// explanation instead of rewording it passes a ban scan perfectly, and this screen's
    /// blurbs are the only place three of these switches are explained at all.
    /// </summary>
    [Theory]
    [InlineData("What EQBuddy shows")]
    [InlineData("Every panel you leave visible shows while EQBuddy is open")]
    [InlineData("panel's own heading, not a second one.")]
    [InlineData("Double-click a HUD chip to open or close its window")]
    [InlineData("🎯 Show target drops in the Loot panel")]
    [InlineData("While you fight, the Loot panel lists what the creature can drop")]
    public void TheRewordedSentencesSurvivedTheRewording(string sentence) =>
        Assert.Contains(sentence, Block, StringComparison.Ordinal);

    /// <summary>
    /// The one heading that was deliberately NOT reworded. "Mini dashboard" is not on the ban
    /// list, and this PR adds nothing beyond what re-hosting needs — the v1 `PinWatchChips` row
    /// that stayed behind on the Watch tab still uses the phrase, so changing it here would
    /// have split one vocabulary across two tabs for no player benefit.
    /// </summary>
    [Fact]
    public void TheHeadingThatWasNotABanHitWasLeftAlone()
    {
        Assert.Contains("\"Mini dashboard\"", Block, StringComparison.Ordinal);
        Assert.Contains("mini dashboard", WindowXaml, StringComparison.Ordinal);
    }
}
