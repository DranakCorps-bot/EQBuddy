using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The state machine behind "a resizable window follows its content until the player takes
/// the height" (Fable 5, 2026-08-23).
///
/// It replaces a pin taken at `ContentRendered` — the first frame, which for a replay-fed
/// body has nothing in it, and which left the Progress window's Experience tab at ~203px
/// for three releases. **The WPF layer has no test project**, so these are the only tests
/// this decision will ever have; the wiring above them is a handful of lines whose job is
/// to report facts into this class.
/// </summary>
public class WindowHeightFollowerTests
{
    private const double Cap = 800;

    /// <summary>The case the old pin got wrong: content arrives late and the window has to
    /// grow to meet it. A replay fills a body seconds after the first frame.</summary>
    [Fact]
    public void ItFollowsContentThatArrivesAfterTheFirstFrame()
    {
        var f = new WindowHeightFollower();

        // First frame: a nearly-empty Experience tab.
        Assert.Equal(203, f.Desired(203, Cap));
        // The replay lands.
        Assert.Equal(389, f.Desired(389, Cap));
        Assert.False(f.Owned);
    }

    /// <summary>**And it SHRINKS**, which the shipped pin also broke: folding a section away
    /// left the window at its tallest, with empty space below the content. A follower that
    /// only grows is half a fix.</summary>
    [Fact]
    public void ItShrinksWhenContentGoesAway()
    {
        var f = new WindowHeightFollower();
        f.Desired(389, Cap);

        Assert.Equal(210, f.Desired(210, Cap));
    }

    /// <summary>
    /// Geometry moves on a DELTA, never on a clock — trap 12, which cost EverQuest its
    /// keyboard under X11 (#173). Both widgets are `SizeToContent`, so assigning a height
    /// asks the windowing system to resize an always-on-top window; doing that every tick
    /// with an unchanged value is the bug that trap describes.
    /// </summary>
    [Fact]
    public void AnUnchangedHeightAsksForNothing()
    {
        var f = new WindowHeightFollower();

        Assert.Equal(389, f.Desired(389, Cap));
        Assert.Null(f.Desired(389, Cap));
        Assert.Null(f.Desired(389, Cap));
        // Sub-pixel jitter is not a change either — a layout pass that returns 389.0001
        // must not produce a resize request.
        Assert.Null(f.Desired(389.2, Cap));
    }

    /// <summary>Content taller than the monitor-derived cap scrolls inside the window
    /// instead of running off the screen; the follower asks for the cap and stops.</summary>
    [Fact]
    public void ItNeverAsksForMoreThanTheCap()
    {
        var f = new WindowHeightFollower();

        Assert.Equal(Cap, f.Desired(2000, Cap));
        Assert.Null(f.Desired(2500, Cap));   // still capped: no change to ask for
    }

    /// <summary>An unmeasured window reports 0, and a NaN would poison every comparison
    /// after it. Neither is a height to follow — the same lesson as trap 2, where a control
    /// that has not been laid out has no size and recording one records nonsense.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void AnUnmeasuredWindowIsNotFollowed(double bogus)
    {
        var f = new WindowHeightFollower();

        Assert.Null(f.Desired(bogus, Cap));
        Assert.False(f.Owned);
        // And it has not poisoned the state: a real measurement still works.
        Assert.Equal(300, f.Desired(300, Cap));
    }

    /// <summary>The moment the player drags the edge, following ends and the height is
    /// theirs — this is the half `SizeToContent` alone cannot give you, and the reason the
    /// old code released the axis at all.</summary>
    [Fact]
    public void AUserResizeTakesOwnershipAndIsWhatGetsPersisted()
    {
        var f = new WindowHeightFollower();
        f.Desired(389, Cap);

        f.OnSizeChanged(640, selfSet: false);

        Assert.True(f.Owned);
        Assert.Equal(640, f.OwnedHeight);
    }

    /// <summary>Our OWN assignment is not a user resize. The caller sets a flag around it
    /// rather than comparing values, because a drag that lands on exactly the height we last
    /// asked for is indistinguishable by value — and guessing wrong there takes the window
    /// away from the player forever.</summary>
    [Fact]
    public void OurOwnAssignmentIsNotAUserResize()
    {
        var f = new WindowHeightFollower();
        var target = f.Desired(389, Cap)!.Value;

        f.OnSizeChanged(target, selfSet: true);

        Assert.False(f.Owned);
        Assert.Null(f.OwnedHeight);
        // ...and it is still following.
        Assert.Equal(450, f.Desired(450, Cap));
    }

    /// <summary>**Ownership is one-way.** A window that resumed following after a drag would
    /// undo the player's action for them, which is this bug in reverse.</summary>
    [Fact]
    public void OnceOwnedItNeverFollowsAgain()
    {
        var f = new WindowHeightFollower();
        f.OnSizeChanged(640, selfSet: false);

        Assert.Null(f.Desired(389, Cap));
        Assert.Null(f.Desired(1200, Cap));
        Assert.Null(f.Desired(200, Cap));
        Assert.Equal(640, f.OwnedHeight);
    }

    /// <summary>A profile that already carries a height starts owned: nobody who has ever
    /// dragged a window sees it move by itself on the next launch. This is also what keeps
    /// `WindowHeights` honest in both directions (trap 20) — the reader is this, the writer
    /// is the user resize above.</summary>
    [Fact]
    public void ASavedHeightStartsOwnedSoARestoredWindowNeverMovesItself()
    {
        var f = new WindowHeightFollower();
        f.StartOwned(555);

        Assert.True(f.Owned);
        Assert.Equal(555, f.OwnedHeight);
        Assert.Null(f.Desired(389, Cap));
    }

    /// <summary>A second drag updates what gets persisted rather than being ignored as
    /// "already owned" — the player's LAST choice is the one that survives.</summary>
    [Fact]
    public void ASecondDragIsStillTheOneThatCounts()
    {
        var f = new WindowHeightFollower();
        f.OnSizeChanged(640, selfSet: false);
        f.OnSizeChanged(700, selfSet: false);

        Assert.Equal(700, f.OwnedHeight);
    }

    // ---- The loop risk, as a unit test rather than as a screenshot ----------

    /// <summary>
    /// **The specific thing that makes `LayoutUpdated` wiring safe** (Fable 5's ask on the
    /// re-scope). `LayoutUpdated` fires continuously, and the handler ASSIGNS a height —
    /// which causes another layout, which fires it again. That is only not a loop because
    /// one delta produces exactly one emit and the settled state produces none.
    ///
    /// Simulated the way the real cycle runs: emit, then feed back the height we just
    /// assigned (a settled window measures what it was told), repeatedly.
    /// </summary>
    [Fact]
    public void OneDeltaEmitsOnceAndThenTheLayoutSettles()
    {
        var f = new WindowHeightFollower();
        var emits = 0;

        var measured = 389.0;
        for (var pass = 0; pass < 25; pass++)
        {
            if (f.Desired(measured, Cap) is { } target)
            {
                emits++;
                measured = target;   // the window is now this tall; the body fits exactly
            }
        }

        Assert.Equal(1, emits);
    }

    /// <summary>Trap 12 is the reason this matters more here than it looks. Assigning a
    /// height asks the windowing system to resize an always-on-top window, and doing that
    /// on a repeating signal is what cost EverQuest its keyboard under X11 (#173). Twenty
    /// layout passes with nothing changing must ask for nothing at all.</summary>
    [Fact]
    public void AQuietLayoutAsksForNothingHoweverOftenItFires()
    {
        var f = new WindowHeightFollower();
        Assert.NotNull(f.Desired(400, Cap));

        for (var pass = 0; pass < 20; pass++)
            Assert.Null(f.Desired(400, Cap));
    }

    // ---- Natural(): the measurement the wiring feeds in --------------------

    [Fact]
    public void AClippedBodyReportsTheHeightItWouldNeed()
    {
        // The shipped defect, in numbers: a 203px window whose body holds 389px of content.
        Assert.Equal(389, WindowHeightFollower.Natural(
            actualHeight: 203, extentHeight: 350, viewportHeight: 164));
    }

    [Fact]
    public void ABodyThatFitsReportsTheHeightItAlreadyHas()
        => Assert.Equal(389, WindowHeightFollower.Natural(389, 350, 350));

    [Fact]
    public void AFoldedSectionReportsASMALLERHeight()
    {
        // The half the old pin ALSO broke: the window could never shrink again. The
        // remainder is signed, so a viewport larger than its content pulls the window in.
        Assert.Equal(300, WindowHeightFollower.Natural(
            actualHeight: 389, extentHeight: 250, viewportHeight: 339));
    }

    /// <summary>
    /// The pin must not come back by reflex. It is one line
    /// (`window.ContentRendered += Release`) and it read as obviously correct for three
    /// releases, so the guard is a source scan — the WPF layer has no other kind.
    /// </summary>
    [Fact]
    public void WindowZoomNeverPinsTheHeightOnARenderEventAgain()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot(), "src", "EQBuddy", "WindowZoom.cs"));

        // The SUBSCRIPTION, not the word: the doc comment above the method explains what
        // the pin was and why it went, and a scan that forbade naming it would forbid the
        // explanation. Matching `+=` is also the precise claim — nothing may hook a render
        // event to set geometry.
        Assert.DoesNotMatch(@"ContentRendered\s*\+=", source);
        // The negative that stops this going vacuous (trap 39): the replacement really is
        // wired, so a file that merely deleted the pin cannot pass.
        Assert.Contains("LayoutUpdated", source);
        Assert.Contains("WindowHeightFollower", source);
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "EQBuddy.slnx")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
