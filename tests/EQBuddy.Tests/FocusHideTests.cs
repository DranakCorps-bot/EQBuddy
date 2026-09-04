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

    /// <summary>
    /// **Every NAME the deny-list excepts still belongs to a real window.**
    ///
    /// This replaces `TheTwoUisNameTheirWindowsTheSameWay`, and BOTH halves of that
    /// replacement are worth reading, because the old test was doubly unable to fail.
    ///
    /// **One: with one lane, a parity check is `Assert.Subset(x, {})`** — true for every
    /// possible x. E-2 (2026-09-04) removed the second lane, so it had to go.
    ///
    /// **Two: it never worked in the first place.** <see cref="WindowNames"/>'s pattern
    /// carried two literal BACKSPACE characters (0x08) where `\b` was meant — a `\b`
    /// interpreted as an escape by whatever wrote the file, which is exactly the hazard
    /// CLAUDE.md's tooling note describes about authoring code through heredocs. The regex
    /// therefore asked for a backspace before "class" and matched nothing, in either lane,
    /// ever. Both sets were empty, `Assert.Subset` passed, and a parity guard read as
    /// coverage for as long as it existed (trap 34, and trap 39's exact shape: the
    /// assertion could not fail).
    ///
    /// What replaces it guards the half that survives, and it is a real one:
    /// <see cref="FocusHide.FollowsWidgetHide"/> compares a window's type NAME, and a name
    /// is a string with no compiler behind it (trap 53 cost this repo six dark days on
    /// exactly that). An exception whose window has been renamed or folded away goes on
    /// excepting nothing, and the next window to take that name inherits an exemption
    /// nobody granted it.
    /// </summary>
    [Fact]
    public void EveryDenyListedWindowNameStillExists()
    {
        var windows = WindowNames("EQBuddy");
        // The scan itself finds windows — the assertion the old version needed and did
        // not have. Without this line the guard below passes on an empty set.
        Assert.True(windows.Count > 10,
            $"the window scan found {windows.Count} classes, so nothing below can fail: " +
            string.Join(", ", windows.OrderBy(n => n, StringComparer.Ordinal)));

        var excepted = new[]
        {
            "MainWindow", "BreakoutWindow", "SpawnChipsWindow", "MezChipsWindow",
            "ClickThroughChip", "AlertWindow", "CursorRingWindow", "GridOverlayWindow",
        }.Where(name => !FocusHide.FollowsWidgetHide(name)).ToList();

        Assert.Equal(8, excepted.Count);   // all eight are really excepted, not merely listed
        foreach (var name in excepted)
            Assert.True(windows.Contains(name),
                $"FocusHide excepts '{name}' from following the widget, and no window class " +
                "by that name exists any more. Either it was renamed — in which case the " +
                "exception is now granted to nobody and the window follows the widget down " +
                "with its own gate still running — or it was folded away and the row goes " +
                "with it.");
    }

    private static HashSet<string> WindowNames(string project)
    {
        var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", project));
        return Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(p => System.Text.RegularExpressions.Regex
                .Matches(File.ReadAllText(p), @"\bclass\s+([A-Za-z0-9_]+)\s*:\s*(?:System\.Windows\.)?Window\b")
                .Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
    }
}
