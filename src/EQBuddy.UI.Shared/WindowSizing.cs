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
