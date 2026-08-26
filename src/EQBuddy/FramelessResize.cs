using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace EQBuddy;

/// <summary>
/// Gives a frameless window real resize borders.
///
/// **The bug this fixes shipped as a feature.** `WindowStyle="None"` +
/// `AllowsTransparency="True"` means Windows draws no non-client area, so there is no
/// border to grab — and `ResizeMode="CanResize"` does not create one. Every theme window
/// and every pop-out that gained resize on 2026-08-21 and 2026-08-25 had `CanResize`, had
/// `WindowZoom.AllowResize` wired, persisted a height on close, and **could not be resized
/// by hand.** Hateborne confirmed it directly on 2026-08-25: *"I cannot resize pop-up
/// windows."*
///
/// **Nothing could see it.** `ResizableWindowTests` asserts that a window does not say
/// `NoResize` and that it calls `AllowResize` — both true throughout. That is trap 34's
/// exact shape: a guard that checks for the wrong thing being present cannot see the right
/// thing being absent. A screenshot cannot see it either; the window looks correct.
/// `HANDOFF.md` even recorded the opposite as proven, reasoning from "the theme windows
/// use the same chrome and the owner has been resizing them" — an argument, not a
/// measurement, and the measurement went the other way.
///
/// The mechanism was already here and already right: `BreakoutWindow` has had this hook
/// since 2026-08-06, when David reported the same thing about the loot window ("I still
/// can't resize the loot window — a frameless window has no resize borders, and a corner
/// glyph nobody finds isn't an affordance"). It was never lifted out. This is that hook,
/// lifted, with the zone math still in the unit-tested
/// <see cref="EQBuddy.UI.Shared.ResizeZones"/>.
/// </summary>
internal static class FramelessResize
{
    private const int WmNcHitTest = 0x84;
    private const int WmNcLButtonDown = 0xA1;
    private const int WmExitSizeMove = 0x232;
    private const int HtLeft = 10, HtBottomRight = 17;

    /// <summary>
    /// Has the PLAYER dragged this window's border? An attached property rather than a
    /// field, so nothing holds a window alive after it closes.
    ///
    /// This is the fact trap 49's reverted follower could not get. That design tried to
    /// tell the player's resize from its own by flagging its own assignments — and missed
    /// the toolkit as a third resizer, so a `SizeToContent` re-measure read as a drag.
    /// `WM_NCLBUTTONDOWN` on a resize hit code followed by `WM_EXITSIZEMOVE` is the native
    /// size loop and nothing else: no attribution, no guess, no third actor.
    /// </summary>
    private static readonly DependencyProperty PlayerSizedProperty =
        DependencyProperty.RegisterAttached(
            "PlayerSized", typeof(bool), typeof(FramelessResize), new PropertyMetadata(false));

    /// <summary>Did the player drag this window's border? The persist decision reads this,
    /// so a height nobody chose is never written to <c>WindowHeights</c>.</summary>
    public static bool PlayerTookHeight(Window window) =>
        window.GetValue(PlayerSizedProperty) is true;

    /// <summary>
    /// A window opening at a RESTORED height starts owned.
    ///
    /// Without this, a size the player dragged survives one close and is deleted by the
    /// next: reopening builds a fresh window with the flag clear, so an undragged close
    /// then removes the entry. Measured — a run that dragged to 537 and reopened ended
    /// with WindowHeights empty. "Nobody who has ever dragged a window sees it move by
    /// itself afterwards" was the reverted follower's rule, and it was right.
    /// </summary>
    public static void MarkPlayerSized(Window window) =>
        window.SetValue(PlayerSizedProperty, true);

    /// <summary>
    /// The height the window is actually WEARING, or null while it still sizes to content.
    ///
    /// The body cap reads this rather than <see cref="PlayerTookHeight"/>, and the
    /// difference produced a real defect: capping the body at the design constant while the
    /// WINDOW wore a restored height left a void between the tab strip and the content —
    /// a tall window with a short body inside it (Hateborne's Kills &amp; Drops, 2026-08-25,
    /// stored at 1224 units: *"Why is everything minimized in the middle?"*).
    ///
    /// So the constant governs the size a window OPENS at, and once it has a concrete
    /// height — dragged or restored — the body fills it. Those are different questions and
    /// they need different answers.
    /// </summary>
    public static double? ManualHeight(Window window) =>
        window.SizeToContent == SizeToContent.Manual && window.ActualHeight > 0
            ? window.ActualHeight
            : null;

    /// <summary>Call once, any time before the window is shown. Safe on a window that
    /// already has a native border — it simply never fires, because a bordered window
    /// hit-tests its own frame before the client area is asked.</summary>
    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var hint = AddHint(window);
            if (hint is not null) window.MouseLeave += (_, _) => hint.Opacity = 0;
            if (PresentationSource.FromVisual(window) is HwndSource src)
                src.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
                    => Hook(window, hint, msg, wParam, lParam, ref handled));
        };
    }

    /// <summary>
    /// The visible hint, asked for by Hateborne on 2026-08-25 the moment the border started
    /// working: *"Is it possible to show the yellow bar at the bottom, similar to how the
    /// main window does when one hovers at the bottom?"*
    ///
    /// It is the same answer the widget's own `HeightGrip` gives, and for the same reason —
    /// that grip's XAML records a 1.66 field test where *"dragging the bottom doesn't
    /// work"* turned out to mean *"I never hit it"*. An edge you cannot see is discoverable
    /// only by accident, which is the 2026-08-06 note about a corner glyph nobody finds,
    /// inverted.
    ///
    /// Drawn by WRAPPING the window's content rather than by an adorner: an adorner layer
    /// depends on the content being inside an AdornerDecorator, which is a property of each
    /// window's XAML and would fail silently on the ones that lack it. The wrap happens once,
    /// at SourceInitialized, and the bar is <c>IsHitTestVisible="False"</c> so it can never
    /// take the hit-test the resize itself depends on.
    /// </summary>
    private static System.Windows.Shapes.Rectangle? AddHint(Window window)
    {
        if (window.Content is not UIElement content) return null;

        var bar = new System.Windows.Shapes.Rectangle
        {
            Height = 2.5,
            RadiusX = 1.25,
            RadiusY = 1.25,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsHitTestVisible = false,
            Opacity = 0,
        };
        // The same brush and the same margins the widget's grip uses, by resource key, so
        // a theme swap moves both and neither can drift into its own colour.
        bar.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "AccentBrush");
        bar.SetResourceReference(FrameworkElement.MarginProperty, "GripLine");

        window.Content = null;   // a UIElement may have only one parent
        var host = new Grid();
        host.Children.Add(content);
        host.Children.Add(bar);
        window.Content = host;
        return bar;
    }

    private static IntPtr Hook(
        Window window, System.Windows.Shapes.Rectangle? hint,
        int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WmNcHitTest:
            {
                // lParam: screen coords, low word X, high word Y — SIGNED, because a
                // second monitor to the left gives negative coordinates.
                var x = (short)((long)lParam & 0xFFFF);
                var y = (short)(((long)lParam >> 16) & 0xFFFF);
                var p = window.PointFromScreen(new Point(x, y));
                var hit = EQBuddy.UI.Shared.ResizeZones.Hit(
                    p.X, p.Y, window.ActualWidth, window.ActualHeight,
                    EQBuddy.UI.Shared.BreakdownRowLayout.ResizeEdge,
                    EQBuddy.UI.Shared.BreakdownRowLayout.ResizeCorner);
                // The hint shows exactly when a bottom drag would engage, so it is a
                // statement about what will happen rather than an approximation of it.
                if (hint is not null)
                    hint.Opacity = hit is EQBuddy.UI.Shared.ResizeZones.Bottom
                        or EQBuddy.UI.Shared.ResizeZones.BottomLeft
                        or EQBuddy.UI.Shared.ResizeZones.BottomRight ? 0.7 : 0;
                if (hit != 0) { handled = true; return hit; }
                break;
            }

            case WmNcLButtonDown when (long)wParam is >= HtLeft and <= HtBottomRight:
                // The native size loop is about to start. A window still sizing to its
                // content would snap straight back the moment layout runs, so hand the
                // axis over first — and assign the MEASURED size before clearing, or it
                // jumps to whatever XAML said for one frame.
                if (window.SizeToContent != SizeToContent.Manual)
                {
                    if (window.ActualWidth > 0) window.Width = window.ActualWidth;
                    if (window.ActualHeight > 0) window.Height = window.ActualHeight;
                    window.SizeToContent = SizeToContent.Manual;
                }
                break;

            case WmExitSizeMove when window.SizeToContent == SizeToContent.Manual:
                // ActualWidth/Height, not Width/Height: the native size loop moves the
                // window without writing the dependency properties, so without this the
                // size the player chose is not the size anything else can read — and
                // AllowResize's Closed handler is one of the things that reads it.
                if (window.ActualWidth > 0) window.Width = window.ActualWidth;
                if (window.ActualHeight > 0) window.Height = window.ActualHeight;
                // The size loop just ended, so this height is the player's by construction.
                window.SetValue(PlayerSizedProperty, true);
                break;
        }
        return IntPtr.Zero;
    }
}
