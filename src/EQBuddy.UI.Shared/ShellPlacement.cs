namespace EQBuddy.UI.Shared;

/// <summary>One monitor's WORK AREA (the screen minus the taskbar), in the caller's units —
/// <c>Window.Left</c>'s DIPs, converted by the WPF adapter and never here (trap 1). A plain
/// record rather than <c>System.Windows.Rect</c> because this project is framework-free.</summary>
public readonly record struct ScreenArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

/// <summary>Where the shell sits: position and size, one moment's answer.</summary>
public readonly record struct ShellBounds(double Left, double Top, double Width, double Height);

/// <summary>
/// WHERE THE EVOLVED SHELL OPENS, AND WHAT IT REMEMBERS — the arithmetic behind
/// discussion #966 (CryfaceCorpse): *"the new window will open on my 3rd monitor with the
/// border outside of selection range … The new saved window location is not saved."*
///
/// **Both halves were true, and they were two different defects.**
///
/// <list type="number">
/// <item><b>Placement.</b> <see cref="EQBuddy.Core.WindowPlacement.SecondaryOrigin"/> picks the
/// monitor BESIDE the primary from the six virtual-screen numbers, and its own doc comment
/// says the price: those numbers say how far the desk reaches, never where each monitor sits
/// inside it. So it knows the COLUMN and guesses the ROW — <c>Top</c> = the primary's row +
/// 60. On a desk whose side monitor is shorter or set lower than the primary (a 1080p panel
/// bottom-aligned beside a 1440p one starts 360 units down), that row is dead space above the
/// monitor, and the title bar — the only thing a native-chrome window drags by — is on no
/// screen at all. Nothing then corrected it: a guess was applied as a placement.</item>
/// <item><b>Persistence.</b> The shell had no saved position. Every open recomputed the same
/// guess, so moving it by hand bought exactly one session.</item>
/// </list>
///
/// **The fix is a FIT against the real monitors, applied last, whatever chose the
/// position** — the secondary-monitor guess, a saved spot from a desk that has since changed,
/// or a saved spot that sits in the virtual screen's dead space (which
/// <c>ScreenGuard.OnScreen</c>'s virtual-rectangle test cannot see, for the same six-number
/// reason). The WPF adapter hands in every monitor's work area; this picks one and fits the
/// window inside it. The per-axis clamp is <see cref="HudChipRow.ParkedPlacement"/>'s, reused
/// rather than restated: "stay where you were put until the monitor's edge stops you, and the
/// leading edge wins when you do not fit" is the same rule for a parked chip row and a shell.
/// </summary>
public static class ShellPlacement
{
    /// <summary>
    /// The size to open at: the saved one when there is one, floored at the shell's minimum;
    /// otherwise <see cref="ShellLayoutPolicy.OpenWidth"/> × <see cref="ShellLayoutPolicy.OpenHeight"/>.
    /// A half-written pair (one NaN) is not a size, the same rule
    /// <see cref="HudChipRow.IsParked"/> applies to a position.
    /// </summary>
    public static (double Width, double Height) OpeningSize(double savedWidth, double savedHeight)
    {
        if (!double.IsFinite(savedWidth) || !double.IsFinite(savedHeight)
            || savedWidth <= 0 || savedHeight <= 0)
            return (ShellLayoutPolicy.OpenWidth, ShellLayoutPolicy.OpenHeight);
        return (Math.Max(savedWidth, ShellLayoutPolicy.MinWidth),
                Math.Max(savedHeight, ShellLayoutPolicy.MinHeight));
    }

    /// <summary>
    /// Which monitor a window at these bounds belongs to.
    ///
    /// <list type="number">
    /// <item>The one it overlaps MOST — the rule Windows itself uses for a window on two
    /// screens (<c>MonitorFromRect</c>).</item>
    /// <item>Overlapping none (the #966 case: the title bar in the dead space above a shorter
    /// side monitor), the nearest monitor <b>in the same column</b> — a monitor the window
    /// shares horizontal extent with — before any other. <b>Column first is deliberate and
    /// not Euclidean:</b> the secondary-monitor placement DECIDED the column and GUESSED the
    /// row, so the column is the evidence. Straight-line distance would hand the #966 window
    /// back to the PRIMARY — 60 units to its side edge against a few hundred to the side
    /// monitor's top — and drop the shell over the game, which is the one place it exists
    /// not to open.</item>
    /// <item>Sharing no column either, the nearest by horizontal then vertical gap.</item>
    /// </list>
    /// Null when there is no monitor to ask about (a headless or half-initialised host), which
    /// the caller reads as "leave the window alone".
    /// </summary>
    public static ScreenArea? AreaFor(ShellBounds window, IReadOnlyList<ScreenArea> areas)
    {
        if (areas is not { Count: > 0 } || !Finite(window)) return null;

        ScreenArea? best = null;
        var bestOverlap = 0.0;
        foreach (var a in areas)
        {
            if (!Usable(a)) continue;
            var w = Math.Min(window.Left + window.Width, a.Right) - Math.Max(window.Left, a.Left);
            var h = Math.Min(window.Top + window.Height, a.Bottom) - Math.Max(window.Top, a.Top);
            if (w <= 0 || h <= 0) continue;
            if (w * h > bestOverlap) { bestOverlap = w * h; best = a; }
        }
        if (best is not null) return best;

        var bestKey = (Dx: double.MaxValue, Dy: double.MaxValue);
        foreach (var a in areas)
        {
            if (!Usable(a)) continue;
            var key = (Dx: Gap(window.Left, window.Left + window.Width, a.Left, a.Right),
                       Dy: Gap(window.Top, window.Top + window.Height, a.Top, a.Bottom));
            if (key.Dx < bestKey.Dx || (key.Dx == bestKey.Dx && key.Dy < bestKey.Dy))
            {
                bestKey = key;
                best = a;
            }
        }
        return best;
    }

    /// <summary>
    /// The bounds that put the whole window on a monitor's work area — so the title bar is
    /// always somewhere a mouse can reach. The size is capped to the area first (never below
    /// the shell's own floor), then the position is clamped per axis by
    /// <see cref="HudChipRow.ParkedPlacement"/>: a window that already fits is not moved a
    /// unit, and one bigger than the area keeps its leading (top-left) edge — the corner with
    /// the title bar in it.
    ///
    /// Null means "no answer, leave it": no monitors, or bounds that are not real yet.
    /// </summary>
    public static ShellBounds? Fit(ShellBounds window, IReadOnlyList<ScreenArea> areas)
    {
        if (AreaFor(window, areas) is not { } area) return null;
        var width = Math.Max(ShellLayoutPolicy.MinWidth, Math.Min(window.Width, area.Width));
        var height = Math.Max(ShellLayoutPolicy.MinHeight, Math.Min(window.Height, area.Height));
        var (left, top) = HudChipRow.ParkedPlacement(
            window.Left, window.Top, width, height, area.Left, area.Top, area.Right, area.Bottom);
        return new ShellBounds(left, top, width, height);
    }

    /// <summary>
    /// What the shell's close writes back — #117's rule (<see cref="EQBuddy.Core.WindowPlacement.PositionToPersist"/>),
    /// applied to the size as well as the position.
    ///
    /// A window that opened at its FALLBACK (the saved spot was rejected, which a TRANSIENT
    /// desk — a sleeping display, an RDP hop — also produces) and was never touched keeps the
    /// saved values, so the monitors coming back bring the spot back. Anything the player did
    /// — a move OR a resize — wins, and so does a genuine restore. "Placed" is where the shell
    /// was AFTER its own fit, so the fit is never mistaken for the player moving it.
    /// </summary>
    public static ShellBounds ToPersist(
        bool restoredFromSaved, ShellBounds placed, ShellBounds current, ShellBounds saved)
    {
        var touched = Moved(placed.Left, current.Left) || Moved(placed.Top, current.Top)
            || Moved(placed.Width, current.Width) || Moved(placed.Height, current.Height);
        var (left, top) = EQBuddy.Core.WindowPlacement.PositionToPersist(
            restoredFromSaved, touched, current.Left, current.Top, saved.Left, saved.Top);
        var (width, height) = EQBuddy.Core.WindowPlacement.PositionToPersist(
            restoredFromSaved, touched, current.Width, current.Height, saved.Width, saved.Height);
        return new ShellBounds(left, top, width, height);
    }

    /// <summary>How far a bound may drift before it counts as the player's doing. One unit,
    /// because a fitted size goes through the window's pixel rounding at a non-integer
    /// scale and comes back a fraction off — which is layout, not a drag.</summary>
    public const double TouchTolerance = 1;

    private static bool Moved(double placed, double current) =>
        !(Math.Abs(current - placed) <= TouchTolerance);

    private static double Gap(double lo, double hi, double areaLo, double areaHi) =>
        hi <= areaLo ? areaLo - hi : lo >= areaHi ? lo - areaHi : 0;

    private static bool Usable(ScreenArea a) =>
        double.IsFinite(a.Left) && double.IsFinite(a.Top)
        && double.IsFinite(a.Width) && double.IsFinite(a.Height) && a.Width > 0 && a.Height > 0;

    private static bool Finite(ShellBounds b) =>
        double.IsFinite(b.Left) && double.IsFinite(b.Top)
        && double.IsFinite(b.Width) && double.IsFinite(b.Height) && b.Width > 0 && b.Height > 0;
}
