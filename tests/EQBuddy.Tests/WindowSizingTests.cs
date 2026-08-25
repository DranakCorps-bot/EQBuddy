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
}
