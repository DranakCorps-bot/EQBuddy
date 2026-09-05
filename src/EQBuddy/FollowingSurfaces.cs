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
        if (w._shellWindow is { IsLoaded: true, IsVisible: true } sh) yield return sh;
    }

    /// <summary>The widget's once-a-second follow tick. Lives here rather than in
    /// <c>MainWindow.RefreshUi</c> for the reason the hotspot ratchet exists: a loop over
    /// a list this file already owns is not window logic.</summary>
    public static void TickAll(MainWindow w)
    {
        foreach (var surface in OpenOn(w)) surface.MaybeFollow();
    }
}
