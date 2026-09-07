using System.Collections.Generic;
using System.Windows;

namespace EQBuddy;

/// <summary>
/// A satellite window that follows the widget's tick and can say which snapshot it last
/// painted. The six theme pop-outs implement it; the widget itself does not.
/// </summary>
internal interface IFollowingSurface
{
    /// <summary>The once-a-tick follow, subject to the window's own throttle.</summary>
    void MaybeFollow();

    /// <summary>Paint NOW, throttle or no throttle, from the widget's current snapshot.
    /// Used only by the <c>EQBUDDY_EXPAND</c> dump, which has to describe ONE moment.</summary>
    void PaintNow();

    /// <summary>The snapshot version this window last painted; -1 before its first.</summary>
    long RenderedVersion { get; }
}

/// <summary>
/// WHICH satellite windows are following right now — one list, in one place.
///
/// There were three copies of it: <c>RefreshUi</c>'s follow tick, <c>WidgetDump</c>'s
/// <c>surfacesBehind</c> count, and (in effect) whatever a reader assumed. Two of them
/// were already reading the SAME six fields through the SAME guard and had to be kept in
/// step by hand, which is trap 30's shape — a list that cannot be type-checked stops
/// covering the set the day the set grows. Adding a seventh pop-out now means adding one
/// line here, and both callers get it.
/// </summary>
internal static class FollowingSurfaces
{
    /// <summary>Open, loaded and VISIBLE — the same guard <c>RefreshUi</c> has always
    /// used. A loaded but hidden window is never ticked, so counting it as "behind"
    /// would wait for a paint that is never coming.</summary>
    public static IEnumerable<IFollowingSurface> OpenOn(MainWindow w)
    {
        if (w._questsWindow is { IsLoaded: true, IsVisible: true } q) yield return q;
        if (w._progressWindow is { IsLoaded: true, IsVisible: true } p) yield return p;
        if (w._gearLootWindow is { IsLoaded: true, IsVisible: true } g) yield return g;
        if (w._creatureWindow is { IsLoaded: true, IsVisible: true } c) yield return c;
        if (w._wikiPackWindow is { IsLoaded: true, IsVisible: true } k) yield return k;
        if (w._worldWindow is { IsLoaded: true, IsVisible: true } o) yield return o;
        // The Evolved shell (E-3 PR 1) is the seventh, and it is the line this file's own
        // header predicted: "adding a seventh pop-out now means adding one line here, and
        // both callers get it." It is not a pop-out — it is a normal window with native
        // chrome — but it hosts live surfaces off the same snapshot, so the tick and the
        // dump's surfacesBehind count both have to see it or the dump describes two
        // moments again (trap 56).
        if (ShellHost.Window is { IsLoaded: true, IsVisible: true } sh) yield return sh;
    }

    /// <summary>The widget's once-a-second follow tick. Lives here rather than in
    /// <c>MainWindow.RefreshUi</c> for the reason the hotspot ratchet exists: a loop over
    /// a list this file already owns is not window logic.</summary>
    public static void TickAll(MainWindow w)
    {
        foreach (var surface in OpenOn(w)) surface.MaybeFollow();
    }

    /// <summary>
    /// A new <c>/outputfile inventory</c> dump landed — tell every host that draws it.
    ///
    /// **It is a fan-out rather than one call because the Evolved shell became a second
    /// host of the Gear room (E-3 PR 2), and the surface it hosts paints on ARRIVAL rather
    /// than on the tick.** Both hosts deliberately skip the once-a-second repaint for the
    /// Inventory room — it re-scans the game folder and rebuilds every row, which clears a
    /// StackPanel out from under the player's cursor — so this notification is the ONLY
    /// thing that fills it in while they are watching. A notification that reached one host
    /// and not the other would leave the second showing the old bags indefinitely: exactly
    /// the "EQBuddy did nothing" reading the auto-import exists to prevent, and invisible
    /// to a diff, a test and a screenshot alike.
    ///
    /// It lives here rather than at the call site for the reason this whole file exists —
    /// a list of satellite surfaces that has to be kept in step by hand is trap 30's shape,
    /// and adding the third host should be one line in one place.
    /// </summary>
    public static void InventoryChanged(MainWindow w)
    {
        w._gearLootWindow?.InventoryChanged();
        ShellHost.Window?.InventoryChanged();
    }
}
