namespace EQBuddy.Core;

/// <summary>
/// Guards saved window positions against monitor-layout changes. A position saved
/// while a second monitor (or higher resolution) was attached can land entirely
/// off-screen after the layout changes; settings.json survives reinstalls, so the
/// window stays invisible until the file is hand-edited (field report, 2026-08-03).
/// </summary>
public static class WindowPlacement
{
    /// <summary>Minimum pixels of the window that must remain visible, per axis,
    /// for a saved position to be trusted. Our windows drag from anywhere in the
    /// body, so any grab-able corner this size is enough to rescue one by hand.</summary>
    public const double Margin = 40;

    /// <summary>
    /// True when a window at (left, top) keeps at least a Margin×Margin grab area
    /// inside the virtual-screen rectangle. NaN coordinates are never reachable
    /// (first launch). Width/height are optional: windows that size to content pass
    /// NaN, which conservatively shrinks the window to the grab area — a position
    /// is then trusted only if its top-left corner region is visible, while a known
    /// width lets a window deliberately tucked past an edge keep its spot.
    /// </summary>
    public static bool IsReachable(double left, double top,
        double screenLeft, double screenTop, double screenWidth, double screenHeight,
        double width = double.NaN, double height = double.NaN)
    {
        if (double.IsNaN(left) || double.IsNaN(top)) return false;
        if (double.IsNaN(width) || width < Margin) width = Margin;
        if (double.IsNaN(height) || height < Margin) height = Margin;
        var overlapW = Math.Min(left + width, screenLeft + screenWidth) - Math.Max(left, screenLeft);
        var overlapH = Math.Min(top + height, screenTop + screenHeight) - Math.Max(top, screenTop);
        return overlapW >= Margin && overlapH >= Margin;
    }

    /// <summary>
    /// What a window's Closed handler should persist (#117, Snagglefern's 4-screen
    /// rig). A window placed at its FALLBACK — the saved position was rejected,
    /// which happens under a TRANSIENT monitor topology (sleeping displays, an RDP
    /// hop, an update relaunching the app with a screen off) — and never moved by
    /// the user must keep the ORIGINAL saved values: the monitors come back, and
    /// persisting the fallback would permanently teleport a window the player had
    /// parked with care. User movement always wins, as does a genuine restore.
    /// </summary>
    public static (double Left, double Top) PositionToPersist(
        bool restoredFromSaved, double placedLeft, double placedTop,
        double currentLeft, double currentTop, double savedLeft, double savedTop)
    {
        var moved = currentLeft != placedLeft || currentTop != placedTop;
        return PositionToPersist(restoredFromSaved, moved, currentLeft, currentTop, savedLeft, savedTop);
    }

    /// <summary>Overload for windows that MOVE THEMSELVES (the grow-up chip stacks,
    /// whose anchor rewrites Top on every size change — 2026-08-13 review): a
    /// coordinate delta can't tell a user drag from the anchor's own writes, so the
    /// caller states user movement explicitly, set where the drag actually starts.</summary>
    public static (double Left, double Top) PositionToPersist(
        bool restoredFromSaved, bool userMoved,
        double currentLeft, double currentTop, double savedLeft, double savedTop) =>
        restoredFromSaved || userMoved || double.IsNaN(savedLeft) || double.IsNaN(savedTop)
            ? (currentLeft, currentTop)
            : (savedLeft, savedTop);

    /// <summary>The gap between a secondary monitor's leading edge and a window opened
    /// there. It is <c>AppHarness.SecondaryShotOrigin</c>'s 60, deliberately the same
    /// number: the harness has been putting fixture windows on that display since
    /// 2026-09-05 and a second nearby constant would be two answers to one question.</summary>
    public const double SecondaryMargin = 60;

    /// <summary>
    /// Where a window opens when the desk has a monitor BESIDE the primary one — null when
    /// it does not, which means "you decide" and leaves the caller's own default (WPF's
    /// <c>CenterScreen</c>) alone.
    ///
    /// **Why the shell wants this at all.** The game is on the player's monitor; everything
    /// else goes somewhere else, which is this product's whole surface rule. The widget
    /// already honours that — it restores to a saved position, and David's is on DISPLAY2 —
    /// but a window that centres on the PRIMARY screen opens on top of EverQuest every time,
    /// and a review door that lands over the thing being reviewed is a door nobody opens
    /// twice.
    ///
    /// **Every number here is in the same unit space as <c>SystemParameters</c> and
    /// <c>Window.Left</c>/<c>Top</c>** — WPF device-independent units, with the primary
    /// monitor's top-left at the origin. That is trap 1's rule and it is the reason this
    /// works off the virtual-screen metrics rather than off <c>GetMonitorInfo</c>: the Win32
    /// rects are in physical pixels, and mixing those with a <c>Left</c> assignment is
    /// exactly the mismatch that only shows up at scales ≠ 100%.
    ///
    /// **It answers for a monitor to the LEFT or RIGHT and refuses for one ABOVE or BELOW,
    /// and the refusal is the honest half.** The virtual-screen rectangle says how far the
    /// desk extends, not where each monitor sits inside it: a desk that is taller than the
    /// primary has a monitor stacked somewhere, and nothing in these six numbers says which
    /// COLUMN it occupies. Guessing would put the shell half on a screen and half on
    /// nothing, which is worse than the primary-centred window this is replacing. A stacked
    /// desk therefore gets today's behaviour, not a plausible-looking wrong one.
    /// </summary>
    /// <param name="primaryWidth">The primary monitor's width. Its top-left is the origin,
    /// so the primary occupies x ∈ [0, primaryWidth) and nothing else does.</param>
    /// <param name="windowWidth">The window's size, which is part of the question rather
    /// than a detail: a band too narrow to hold the window is not a place to open it.</param>
    public static (double Left, double Top)? SecondaryOrigin(
        double virtualLeft, double virtualTop, double virtualWidth, double virtualHeight,
        double primaryWidth,
        double windowWidth, double windowHeight, double margin = SecondaryMargin)
    {
        // A metric that has not been measured is not a monitor. Answering null here is what
        // makes a headless or half-initialised host fall back rather than throw.
        if (!double.IsFinite(virtualLeft) || !double.IsFinite(virtualTop)
            || !double.IsFinite(virtualWidth) || !double.IsFinite(virtualHeight)
            || !double.IsFinite(primaryWidth) || !double.IsFinite(margin)
            || !(windowWidth > 0) || !(windowHeight > 0) || !(primaryWidth > 0))
            return null;

        var virtualRight = virtualLeft + virtualWidth;
        var virtualBottom = virtualTop + virtualHeight;

        // Right first, then left: a second monitor is most often to the right, and picking
        // by "whichever band is wider" would move the shell between two launches on a desk
        // that had not changed. A stated preference is reproducible; a measurement of two
        // near-equal bands is not.
        double bandLeft;
        if (virtualRight - primaryWidth >= windowWidth + margin) bandLeft = primaryWidth;
        else if (-virtualLeft >= windowWidth + margin) bandLeft = virtualLeft;
        else return null;

        // Clamped so the whole window is inside the desk. The clamp can only ever pull it
        // back towards the band's leading edge, which the fit test above already proved has
        // room — so it cannot push the window back onto the primary.
        var left = Math.Min(bandLeft + margin, virtualRight - windowWidth);
        // The PRIMARY's own row, deliberately, even when the desk reaches higher. A
        // negative virtual top says some screen extends above y = 0; it never says WHICH,
        // so on a three-monitor desk with one stacked above, starting there would put the
        // window off the side-by-side display entirely. y = 0 is on the primary by
        // definition and on the secondary in every arrangement that shares its row.
        // Below is clamped for the same reason the left edge is.
        var top = Math.Max(virtualTop, 0) + margin;
        top = Math.Max(virtualTop, Math.Min(top, virtualBottom - windowHeight));
        return (left, top);
    }
}
