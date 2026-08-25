namespace EQBuddy.UI.Shared;

/// <summary>
/// A theme window's user-chosen size: what to restore on open, and what is worth writing
/// back on close.
///
/// **Why it lives beside the zoom rather than next to it.** `WindowZoom` already OWNS a
/// theme window's width — it sets <c>Width = baseWidth × zoom</c> on every Ctrl+wheel step.
/// A second thing writing Width would be two owners of one value, which is the trap this
/// codebase keeps paying for (#202 is the same shape one level up: two producers, both
/// current, last one wins). So a user's drag does not store a width; it stores a new BASE
/// width, and the zoom goes on multiplying it. One owner, one formula.
///
/// Height is simpler: the zoom never sets it, so once a window stops sizing itself to its
/// content the stored height is the only writer there is.
///
/// David hit this on 2026-08-21 trying to force the Kills &amp; Drops tab to scroll: all four
/// theme windows shipped `ResizeMode="NoResize"`, because they draw their own chrome and
/// nobody had put the resize back. A review surface you cannot resize is a review surface
/// that decides for you how much of your screen it is worth.
/// </summary>
public static class WindowSizing
{
    /// <summary>Below this in either axis a window is a sliver a player cannot recover
    /// from, and past it a stored value is likelier corruption than intent.</summary>
    public const double MinWidth = 320;
    public const double MinHeight = 200;

    /// <summary>Nothing wider or taller than this is a size someone chose on purpose; it
    /// is a value that survived a monitor change or a scaling switch.</summary>
    public const double MaxAny = 10000;

    /// <summary>
    /// How tall a theme window's scrolling body OPENS, before anyone drags it.
    ///
    /// **A cap, not a fill target** — Bevel's words, on these very windows: *"386 lu was a
    /// cap, not a fill target. ~175 lu on the tallest Progress room is the right
    /// SizeToContent outcome. 320 stands until a shot overflows it."* The code never used a
    /// constant. Every pop-out derived its body from the MONITOR
    /// (<c>BodyScroll.MaxHeight = MaxHeight - chrome</c>, where <c>MaxHeight</c> was 85% of
    /// the work area), so on a tall screen the Quest Tracker opened with a ~944-unit body
    /// and filled the display — measured at 1822 physical px (Hateborne, 2026-08-25: *"Let's
    /// NOT force pop out windows to default to a giant size"*).
    ///
    /// 400 rather than Bevel's 320 because Hateborne asked for "25-50% larger than the
    /// previous default" and 320 x 1.25 = 400. It is the OPENING size only: the window's own
    /// ceiling stays screen-derived, so a player can still drag one as tall as their monitor
    /// allows and <see cref="BodyCap"/> follows them there.
    /// </summary>
    public const double DefaultBodyHeight = 400;

    /// <summary>
    /// The body scroller's cap: the design constant while the window is still sizing to its
    /// content, and the window's own height once the player has taken it.
    ///
    /// Both halves matter. Without the first a pop-out opens full-screen; without the
    /// second, dragging a window taller would gain empty space below a body that refused to
    /// grow — a resize that visibly does nothing, which is the complaint this whole area
    /// started with.
    /// </summary>
    /// <param name="ceiling">The window's hard ceiling, screen-derived.</param>
    /// <param name="chrome">What this window spends on everything that is not the body:
    /// header, tabs, filters, footer. Per window, because they differ by ~160 units.</param>
    /// <param name="taken">The height the player dragged the window to, or null while it is
    /// still following its content.</param>
    public static double BodyCap(double ceiling, double chrome, double? taken)
    {
        var room = Math.Max(120, ceiling - chrome);
        return taken is { } h && IsSaneHeight(h)
            ? Math.Max(120, Math.Min(h - chrome, room))
            : Math.Min(DefaultBodyHeight, room);
    }

    /// <summary>Is a stored number worth restoring at all?</summary>
    public static bool IsSaneWidth(double w) => w is >= MinWidth and <= MaxAny;

    public static bool IsSaneHeight(double h) => h is >= MinHeight and <= MaxAny;

    /// <summary>The width to open at: the player's stored BASE width when they have set
    /// one, otherwise the window's declared width — either way multiplied by the zoom,
    /// because zoom owns the final number.</summary>
    public static double BaseWidth(double? stored, double declared) =>
        stored is { } s && IsSaneWidth(s) ? s : declared;

    /// <summary>Turn the size a window is CURRENTLY wearing back into a base width to
    /// store. Dividing the zoom out is what keeps the two mechanisms from compounding:
    /// resize at 1.5x, reopen at 1.5x, and without this the window would come back 2.25x
    /// its base and keep growing every session.</summary>
    public static double? BaseWidthToStore(double actualWidth, double zoom)
    {
        if (zoom <= 0 || double.IsNaN(actualWidth)) return null;
        var basis = actualWidth / zoom;
        return IsSaneWidth(basis) ? Math.Round(basis) : null;
    }

    /// <summary>The height to store, or null when the number is not worth keeping. A
    /// window that was never resized reports its content height, which is exactly what it
    /// would compute again next time — storing it is harmless, and storing a torn-down
    /// window's zero is not (the #169 lesson, from positions).</summary>
    public static double? HeightToStore(double actualHeight) =>
        IsSaneHeight(actualHeight) ? Math.Round(actualHeight) : null;
}
