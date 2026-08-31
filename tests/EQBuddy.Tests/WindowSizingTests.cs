using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// How tall a theme window's body opens, and how it behaves once the player has taken the
/// height.
///
/// It is a SUM rather than a pixel, so it lives in UI.Shared and is tested here — the
/// standing reason this repo lifts window arithmetic out of the WPF layer, which has no
/// test project of its own.
/// </summary>
public class WindowSizingTests
{
    /// <summary>
    /// The defect this replaces: every pop-out sized its body from the MONITOR, so a tall
    /// screen produced a window that filled it. On a 1440-unit work area the old rule gave
    /// the Quest Tracker a 944-unit body (1440 x 0.85 - 280); measured on a real display it
    /// opened at 1822 physical px.
    /// </summary>
    [Fact]
    public void ABigMonitorNoLongerMeansABigWindow()
    {
        var ceiling = 1440 * 0.85;                      // what the window allows itself
        var body = WindowSizing.BodyCap(ceiling, chrome: 280, taken: null);

        Assert.Equal(WindowSizing.DefaultBodyHeight, body);
        Assert.True(body < ceiling - 280,
            "the whole point is that the body no longer takes every unit the screen offers");
    }

    /// <summary>A small screen still wins. The constant is a CAP, not a floor — asking for
    /// 400 units of body on a laptop would push the window off the bottom, which is the
    /// failure the screen-derived rule existed to prevent in the first place.</summary>
    [Fact]
    public void ASmallScreenStillGetsTheLastWord()
    {
        var ceiling = 600 * 0.85;                       // 510
        var body = WindowSizing.BodyCap(ceiling, chrome: 280, taken: null);

        Assert.Equal(230, body);                        // 510 - 280, not the 400 constant
        Assert.True(body < WindowSizing.DefaultBodyHeight);
    }

    /// <summary>
    /// Once the player has dragged, the body follows THEM.
    ///
    /// Without this half, dragging a window taller would add empty space beneath a body
    /// that refused to grow — a resize that visibly does nothing, which is the complaint
    /// this whole area started with.
    /// </summary>
    [Fact]
    public void AWindowThePlayerMadeTallerGetsATallerBody()
    {
        var ceiling = 1440 * 0.85;
        var following = WindowSizing.BodyCap(ceiling, chrome: 280, taken: null);
        var taken = WindowSizing.BodyCap(ceiling, chrome: 280, taken: 900);

        Assert.Equal(WindowSizing.DefaultBodyHeight, following);
        Assert.Equal(620, taken);                       // 900 - 280
        Assert.True(taken > following);
    }

    /// <summary>Dragged SHORTER is just as much a choice as dragged taller, and the body
    /// has to shrink with it or the window clips its own content.</summary>
    [Fact]
    public void AWindowThePlayerMadeShorterGetsAShorterBody()
    {
        var body = WindowSizing.BodyCap(1440 * 0.85, chrome: 280, taken: 420);
        Assert.Equal(140, body);
    }

    /// <summary>
    /// Never below a usable sliver, never past the room, and never trusting a number that
    /// is not a size anyone chose.
    ///
    /// The last row is the one worth reading: 99,999 fails <see cref="WindowSizing.IsSaneHeight"/>,
    /// so it is not treated as a height the player took at all — the window opens at its
    /// design default instead of at a value that survived a monitor change. Clamping it to
    /// the screen would have been the obvious thing and the wrong one: it would hand the
    /// player a full-height window they never asked for, off a stored number nobody can
    /// account for.
    /// </summary>
    [Theory]
    [InlineData(1224, 280, 300d, 120)]      // taken smaller than the chrome: floor, not negative
    [InlineData(1224, 280, 1100d, 820)]     // a big but SANE choice is honoured
    [InlineData(1224, 280, 99999d, 400)]    // insane: distrusted, so the default stands
    [InlineData(250, 280, null, 120)]       // chrome exceeds the ceiling: floor
    public void TheBodyIsNeverASliverAndNeverTallerThanTheRoom(
        double ceiling, double chrome, double? taken, double expected)
    {
        Assert.Equal(expected, WindowSizing.BodyCap(ceiling, chrome, taken));
    }

    /// <summary>The constant is what Hateborne asked for on 2026-08-25 — "25-50% larger
    /// than the previous default" — measured against Bevel's already-signed 320. Pinned so
    /// a later change is a decision rather than a drift.</summary>
    [Fact]
    public void TheDefaultIsTwentyFivePercentAboveBevelsThreeTwenty()
    {
        Assert.Equal(400, WindowSizing.DefaultBodyHeight);
        Assert.Equal(320 * 1.25, WindowSizing.DefaultBodyHeight);
    }

    // ---- A nested scroller inside a host body (GearCardView's gear list) ----

    /// <summary>The whole point: the nested list is derived from the HOST, so a window
    /// dragged taller grows the list inside it. `GearCardView` carried a hard 320 — a
    /// card-sized number that came along when the surface was lifted into a window — so
    /// the window grew and the list did not, which is a resize that visibly does
    /// nothing.</summary>
    [Fact]
    public void TheNestedListFollowsTheHostBodyRatherThanAConstant()
    {
        Assert.Equal(306, WindowSizing.NestedBodyCap(hostBodyCap: 400, pinnedChrome: 94));
        Assert.Equal(686, WindowSizing.NestedBodyCap(hostBodyCap: 780, pinnedChrome: 94));
        // A host that shrank takes the list down with it — the direction that keeps the
        // pinned footer reachable rather than pushed under the fold.
        Assert.Equal(206, WindowSizing.NestedBodyCap(300, 94));
    }

    /// <summary>The pinned chrome always gets its room. It is the auto-tick note, the ⧉
    /// copy of `/outputfile inventory` and the import report — the affordances that sit
    /// outside the scroller precisely so a forty-row list cannot bury them (traps 34 and
    /// 37), which is why this scroller is re-pointed rather than deleted.</summary>
    [Fact]
    public void ThePinnedChromeIsAlwaysSubtractedAndNeverAddsRoom()
    {
        Assert.Equal(400, WindowSizing.NestedBodyCap(400, pinnedChrome: 0));
        // A negative measurement is a toolkit having a bad day, not extra room.
        Assert.Equal(400, WindowSizing.NestedBodyCap(400, pinnedChrome: -50));
    }

    /// <summary>A list too short is not a list. Chrome bigger than the body cannot squeeze
    /// it out of existence — the same floor `BodyCap` keeps one level up.</summary>
    [Fact]
    public void TheNestedListIsNeverSqueezedToASliver()
    {
        Assert.Equal(120, WindowSizing.NestedBodyCap(200, pinnedChrome: 500));
    }

    /// <summary>A host that has not sized itself yet answers the DESIGN OPENING HEIGHT,
    /// not "no cap". An uncapped list inside an uncapped body is exactly how a pop-out
    /// filled a tall display (Hateborne, 2026-08-25), and the first render of this surface
    /// genuinely can land before layout.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AHostThatHasNotSizedItselfYetAnswersTheDesignHeight(double unsized)
    {
        Assert.Equal(WindowSizing.DefaultBodyHeight - 40,
            WindowSizing.NestedBodyCap(unsized, pinnedChrome: 40));
    }
}
