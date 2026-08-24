namespace EQBuddy.UI.Shared;

/// <summary>
/// Whether a resizable window's height still follows its content, or belongs to the player.
///
/// **The bug this replaces, and why the obvious fixes were all wrong.**
/// `WindowZoom.AllowResize` wanted two things WPF will not do together: size to content, and
/// let the user drag the bottom edge and remember it. It resolved that by SAMPLING the
/// natural height once, at `ContentRendered`, and switching to manual — and
/// `ContentRendered` fires on the first frame. For a window whose body is filled by the log
/// replay that is a frame with nothing in it: the Progress window's Experience tab pinned
/// ~203px and scrolled forever after, for three releases (measured: 203 pinned vs 389 with
/// the pin skipped).
///
/// Four candidate fixes all tried to pick a BETTER INSTANT to sample, and every instant is
/// wrong for some window, because "the content has arrived" is not an event the toolkit
/// gives you. **Fable 5's ruling (2026-08-23) is that the pin should not be a moment at
/// all**: the window follows its content until the user takes the height for themselves,
/// and the first user-caused size change ends following and starts persisting. That is what
/// both features were separately trying to buy — size-to-content for people who never touch
/// the edge, a remembered height for people who do — with no timer, no per-window knowledge,
/// and no lost capability.
///
/// It is a SUM rather than a pixel, so it lives here and is unit-tested: the WPF layer has
/// no test project (docs/TestPlan.md §5), which is the standing reason this repo lifts
/// window arithmetic into UI.Shared.
/// </summary>
public sealed class WindowHeightFollower
{
    private double? _lastDesired;

    /// <summary>The height belongs to the player now; the window stops following its
    /// content. One-way on purpose — a window that resumed following after a drag would
    /// undo the player's action for them, which is the whole complaint in reverse.</summary>
    public bool Owned { get; private set; }

    /// <summary>The height to PERSIST, or null while the window is still following. Only a
    /// user-caused size gets here, so <c>WindowHeights</c> stores what the player chose
    /// rather than whatever the content happened to measure when the app closed.</summary>
    public double? OwnedHeight { get; private set; }

    /// <summary>A profile that already carries a height starts OWNED at that height.
    /// **Nobody who has ever dragged a window sees it move by itself afterwards** — and it
    /// keeps <c>WindowHeights</c> honest in both directions (trap 20): the reader is this,
    /// the writer is <see cref="OnSizeChanged"/>.</summary>
    public void StartOwned(double height)
    {
        Owned = true;
        OwnedHeight = height;
    }

    /// <summary>
    /// What the window's height should be now, or null for "leave it alone".
    ///
    /// Null once <see cref="Owned"/>, and null whenever the answer has not CHANGED — which
    /// is trap 12's lesson wearing a different hat. Both widgets are `SizeToContent`, so
    /// assigning a height is asking the windowing system to resize a window; doing that on
    /// every tick with an unchanged value is what cost EverQuest its keyboard under X11
    /// (#173). Geometry moves on a delta, never on a clock.
    /// </summary>
    /// <param name="contentHeight">What the window's content currently measures.</param>
    /// <param name="cap">The most it may be — the monitor-derived limit the window already
    /// computes. Content taller than this scrolls inside the window rather than running off
    /// the screen.</param>
    public double? Desired(double contentHeight, double cap)
    {
        if (Owned) return null;
        // An unmeasured window reports 0 and a NaN would poison the comparison; neither is
        // a height to follow. Same family as trap 2 — a control that has not been laid out
        // has no size, and recording one is recording nonsense.
        if (double.IsNaN(contentHeight) || contentHeight <= 0) return null;

        var target = Math.Min(contentHeight, cap);
        if (_lastDesired is { } last && Math.Abs(last - target) < 0.5) return null;
        _lastDesired = target;
        return target;
    }

    /// <summary>
    /// A size change happened. <paramref name="selfSet"/> says whether WE caused it.
    ///
    /// **The attribution has to be a fact rather than a guess**, which is why the caller
    /// sets a flag around its own assignment instead of comparing values: a user drag that
    /// lands on exactly the height we last asked for is indistinguishable by value, and
    /// guessing wrong in that direction takes the window away from the player permanently.
    ///
    /// Anything we did not cause is the player's, and it is the moment following ends.
    /// </summary>
    public void OnSizeChanged(double newHeight, bool selfSet)
    {
        // `selfSet` is the ONLY thing ignored. An earlier draft also returned early when
        // already owned, which silently discarded every drag after the first — the player's
        // LAST choice is the one that has to survive, and once owned we assign nothing, so
        // any change reaching here is theirs by construction.
        if (selfSet) return;
        if (double.IsNaN(newHeight) || newHeight <= 0) return;
        Owned = true;
        OwnedHeight = newHeight;
    }
}
