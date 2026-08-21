using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The hide-the-overlay decision (#41 hide-when-unfocused, #114
/// hide-when-not-running): the full truth table, since the two opt-ins compose and
/// each has a deliberate carve-out that reads as a bug when violated.</summary>
public class FocusHideTests
{
    // (unfocused, notRunning, fgSelf, fgGame, gameRunning) → hide?
    [Theory]
    // Neither opt-in: never hide, whatever the world looks like.
    [InlineData(false, false, false, false, false, false)]
    [InlineData(false, false, false, false, true, false)]
    // Unfocused-only (the shipped #41 behavior, unchanged): hides only when the
    // game runs behind some third app; game closed keeps the widget visible.
    [InlineData(true, false, false, false, true, true)]
    [InlineData(true, false, false, false, false, false)]
    [InlineData(true, false, false, true, true, false)]     // playing: show
    [InlineData(true, false, true, false, true, false)]     // using EQBuddy: show
    // Not-running-only (#114): hides exactly when the game is closed; a running
    // game keeps the widget up even when unfocused (that's the OTHER toggle).
    [InlineData(false, true, false, false, false, true)]
    [InlineData(false, true, false, false, true, false)]
    [InlineData(false, true, true, false, false, false)]    // escape hatch: EQBuddy focused
    [InlineData(false, true, false, true, true, false)]     // playing: show
    // Both on: the overlay exists only while the game is focused or EQBuddy is used.
    [InlineData(true, true, false, false, true, true)]
    [InlineData(true, true, false, false, false, true)]
    [InlineData(true, true, false, true, true, false)]
    [InlineData(true, true, true, false, false, false)]
    public void DecideWalksTheTruthTable(
        bool unfocused, bool notRunning, bool fgSelf, bool fgGame, bool running, bool hide) =>
        Assert.Equal(hide, FocusHide.Decide(unfocused, notRunning, fgSelf, fgGame, running));

    /// <summary>Where the platform cannot say which window is in front, both tick-boxes
    /// save and then do nothing — so Options has to say so (David, 2026-08-16, on #169).
    /// A setting that keeps its state while doing nothing is the silent no-op CLAUDE.md
    /// calls broken, and it only became visible once Linux stopped running two copies of
    /// EQBuddy and the settings started persisting properly.</summary>
    [Fact]
    public void TheNoteAppearsExactlyWhereTheProbeIsMissing()
    {
        Assert.Equal(FocusHide.ForegroundProbeAvailable, FocusHide.UnavailableNote.Length == 0);
        // Windows and macOS both probe; X11/Wayland is the gap.
        Assert.Equal(
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(),
            FocusHide.ForegroundProbeAvailable);
        if (!FocusHide.ForegroundProbeAvailable)
        {
            // Name the reason, not just the refusal — a player who knows it's X11's
            // missing answer rather than an EQBuddy bug doesn't lose an evening to it.
            Assert.Contains("Linux", FocusHide.UnavailableNote);
            Assert.Contains("saved", FocusHide.UnavailableNote);
        }
    }

    // ---- which windows follow the widget down (#189, wizen) ----

    /// <summary>The reported case: the Quest Tracker stayed on screen after the widget
    /// vanished. It is a window opened from a menu, and nothing had ever said such a
    /// window should follow — the hide took the widget and the surfaces with their own
    /// per-tick gate, and that was all.</summary>
    [Theory]
    [InlineData("QuestsWindow")]
    [InlineData("MapWindow")]
    [InlineData("SpawnsWindow")]
    // DropsWindow became the Drops TAB of CreatureWindow on 2026-08-21, on both builds.
    [InlineData("CreatureWindow")]
    // GearLockerWindow and InventoryWindow were two rows here until they folded into the
    // Gear & Loot window's Inventory tab — Windows 2026-08-20, Linux/macOS 2026-08-21.
    // The rule is "yes unless named", so a row for a class that no longer exists passes
    // forever and quietly stops naming anything real.
    [InlineData("GearLootWindow")]
    [InlineData("HistoryWindow")]
    [InlineData("ItemInfoWindow")]
    [InlineData("TravelWindow")]
    [InlineData("FightTimelineWindow")]
    [InlineData("OptionsWindow")]
    public void EveryWindowOpenedFromTheWidgetFollowsIt(string window) =>
        Assert.True(FocusHide.FollowsWidgetHide(window));

    /// <summary>The exceptions, and the reason each one is one. The breakouts matter
    /// most: they re-derive visibility every tick from DisabledBreakouts plus the hide
    /// flag, so hiding one here and showing it back could resurrect one the player
    /// dismissed with its ✕.</summary>
    [Theory]
    [InlineData("MainWindow")]
    [InlineData("BreakoutWindow")]
    [InlineData("SpawnChipsWindow")]
    [InlineData("MezChipsWindow")]
    [InlineData("ClickThroughChip")]
    [InlineData("AlertWindow")]
    [InlineData("CursorRingWindow")]
    [InlineData("GridOverlayWindow")]
    public void TheSurfacesWithTheirOwnGateAreLeftAlone(string window) =>
        Assert.False(FocusHide.FollowsWidgetHide(window));

    /// <summary>The rule is a DENY-list on purpose. A window written tomorrow follows
    /// the widget without anyone remembering to add it — which is the whole of #189: an
    /// allow-list would have the same defect again on the next window.</summary>
    [Fact]
    public void AWindowNobodyHasThoughtOfYetFollowsByDefault() =>
        Assert.True(FocusHide.FollowsWidgetHide("SomeWindowNobodyHasWrittenYet"));

    /// <summary>Both UIs name their windows identically and read this one list, so a
    /// class that exists in only one of them is a parity gap worth seeing. The Avalonia
    /// build is deliberately allowed to lag (it has no CompanionWindow), so this asserts
    /// the direction that matters: nothing exists there that Windows lacks.</summary>
    [Fact]
    public void TheTwoUisNameTheirWindowsTheSameWay()
    {
        Assert.Subset(WindowNames("EQBuddy"), WindowNames("EQBuddy.Avalonia"));
    }

    private static HashSet<string> WindowNames(string project)
    {
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", project));
        return Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(p => System.Text.RegularExpressions.Regex
                .Matches(File.ReadAllText(p), @"class\s+([A-Za-z0-9_]+)\s*:\s*(?:System\.Windows\.)?Window")
                .Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
    }
}
