using System.Windows;
using System.Windows.Input;

namespace EQBuddy;

/// <summary>
/// DRAG-TO-PLACE for the two HUD companion windows (OE-8 / OE-1b lock 1: "the grip is
/// anywhere on the box"), and the ONE code path that may write a park.
///
/// **Why this exists as a class rather than two handlers.** The chip row and the under-bar
/// panel are the same object to a player — a small always-on-top box hanging off the HUD —
/// and a second copy of "how does a drag become a saved corner" is the drift that put a
/// hand-copied older anchor on the Avalonia chip stacks and carried #122 and #152 to two more
/// platforms. One grip, two windows.
///
/// **A MANUAL move loop, not <c>Window.DragMove</c>, and that is the feature.** Three
/// reasons, in the order they bite:
///
/// <list type="bullet">
/// <item>**A drag needs an unambiguous END.** The park is written at drag end and nowhere
/// else, which is what makes trap 49's three actors — follower, toolkit, player — separate
/// BY CONSTRUCTION rather than by a <c>selfSet</c> flag: only the player's gesture has an
/// end, so only the player can reach the setting. <c>DragMove</c> hands the whole gesture to
/// a modal Win32 loop and returns after it, which is an end you have to infer.</item>
/// <item>**Both windows are <c>WS_EX_NOACTIVATE</c>** (<c>NoActivate.Attach</c>, so a chip
/// never steals focus from EverQuest). <c>DragMove</c> is a synthesised caption click and its
/// behaviour on a window that refuses activation is the window manager's business, not
/// ours.</item>
/// <item>**A press must still be able to be a CLICK.** The row's chicklets clear a due timer
/// on one and open the World window on two; the panel's header carries ↗ and ✕. So nothing
/// is captured or handled until the pointer has travelled past the system's own drag
/// threshold — below it the press is somebody else's, and this class never saw it.</item>
/// </list>
///
/// The affordance FACE — what says "you can drag me", and what the un-park control looks like
/// — is Bevel's at the implement review. This is the mechanism under whichever face lands.
/// </summary>
internal sealed class HudDragGrip
{
    private readonly Window _window;
    private readonly Action<double, double> _onPlaced;
    private Point _pressScreen;
    private double _pressLeft, _pressTop;
    private bool _pressed;
    private bool _dragging;

    /// <summary>True while the player is actually moving the window. The follower must not
    /// re-place a window mid-gesture: it would fight the cursor once a second, which reads as
    /// a window that will not be dragged.</summary>
    public bool Dragging => _dragging;

    /// <summary>
    /// Presses this grip has SEEN, and drags it has FINISHED — the <c>hudRowGrip</c> /
    /// <c>hudPanelGrip</c> dump facts, and they exist because three failures are
    /// indistinguishable from outside the app.
    ///
    /// When `scripts/drag-verify.ps1` reports "a real body drag persisted nothing", that is
    /// equally consistent with: the synthetic pointer never reached this window at all; the
    /// press arrived and the move never crossed the threshold; and the whole gesture ran and
    /// the write did not. A counter of each separates them in one run. It is trap 56's
    /// closing rule — ship the instrument before the third theory — and it earned itself
    /// immediately: the first park run failed here, and these two numbers are what said which
    /// of the three it was.
    /// </summary>
    public int PressCount { get; private set; }
    public int DragCount { get; private set; }

    /// <param name="onPlaced">The DRAG END, with the window's final corner. The only caller
    /// that may write the park pair — see this class's note above.</param>
    private HudDragGrip(Window window, Action<double, double> onPlaced)
    {
        _window = window;
        _onPlaced = onPlaced;
        // Preview, so a press anywhere on the box is seen before a chicklet or a header
        // button consumes it — and NOT handled, so those controls still get it if the
        // gesture turns out to be a click. Attached last by the caller when something else
        // (the panel's edge resize) claims the press first: a Preview handler registered with
        // handledEventsToo:false is skipped once that one has set Handled.
        window.PreviewMouseLeftButtonDown += OnDown;
        window.PreviewMouseMove += OnMove;
        window.PreviewMouseLeftButtonUp += OnUp;
        window.LostMouseCapture += (_, _) => Finish(commit: true);
    }

    public static HudDragGrip Attach(Window window, Action<double, double> onPlaced) =>
        new(window, onPlaced);

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        // A double-click is somebody's verb (the row opens the World window on one), never a
        // drag. Letting it arm the grip would start a move loop from the second press.
        if (e.ClickCount > 1) { _pressed = false; return; }
        PressCount++;
        _pressed = true;
        _dragging = false;
        _pressScreen = _window.PointToScreen(e.GetPosition(_window));
        _pressLeft = _window.Left;
        _pressTop = _window.Top;
        // CAPTURE ON THE PRESS, AND IN SUBTREE MODE — and both halves were paid for.
        //
        // Capturing only once the threshold was crossed looked right and could not work:
        // these windows are SMALL. The chip row is about thirty units tall, so a drag that
        // starts at its vertical centre has the pointer OFF the window before it has
        // travelled the system's drag distance — and an uncaptured window stops receiving
        // MouseMove the moment the pointer leaves it. `scripts/drag-verify.ps1 -Mode park`
        // reported "a real body drag persisted nothing", which is equally consistent with
        // three different faults; `hudRowGrip=1,0` — press seen, drag never started — is
        // what named this one, in one run, after the reasoning had said the opposite.
        //
        // SUBTREE, not the default element capture: capturing the WINDOW outright routes
        // every mouse event to the window itself, which would take the click off every
        // chicklet and both header buttons. In subtree mode hit-testing inside the window is
        // untouched and only events OUTSIDE it come here — exactly the two things this needs.
        Mouse.Capture(_window, CaptureMode.SubTree);
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || e.LeftButton != MouseButtonState.Pressed) return;
        var now = _window.PointToScreen(e.GetPosition(_window));
        var dx = now.X - _pressScreen.X;
        var dy = now.Y - _pressScreen.Y;

        if (!_dragging)
        {
            // The SYSTEM's threshold, not a number invented here: a player who set a
            // different one in Windows means it for every drag on their desk, and a chicklet
            // that needed twice the travel of everything else would read as a stuck window.
            // Capture is already held (see OnDown), so this only decides whether the gesture
            // is a MOVE — below the threshold it is still somebody else's click.
            if (Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance) return;
            _dragging = true;
        }

        // Screen units on both sides: PointToScreen gives DIPs relative to the desk, Left and
        // Top are in the same space, and nothing under the window's ChipScale transform is
        // involved — this moves a WINDOW, not a control inside one (trap 1).
        _window.Left = _pressLeft + dx;
        _window.Top = _pressTop + dy;
        e.Handled = true;
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        // Handled only when this WAS a drag, so a press that never crossed the threshold is
        // still the chicklet's click. The order matters: read _dragging before Finish clears
        // it.
        var wasDrag = _dragging;
        Finish(commit: true);
        if (wasDrag) e.Handled = true;
    }

    /// <summary>The end of the gesture, from either side — the button coming up, or the
    /// capture being taken away (an Alt-Tab, a lock screen, the app closing). Both are ends,
    /// and a park the player has visibly made must not be lost because the second kind
    /// happened.</summary>
    private void Finish(bool commit)
    {
        var wasDragging = _dragging;
        var wasPressed = _pressed;
        _pressed = false;
        _dragging = false;
        // Released whether or not the gesture became a drag: the capture was taken on the
        // press, so a plain click has to hand it back too or the next click on anything else
        // on the desk goes nowhere.
        if (wasPressed && ReferenceEquals(Mouse.Captured, _window)) Mouse.Capture(null);
        if (!wasDragging || !commit) return;
        DragCount++;
        _onPlaced(_window.Left, _window.Top);
    }
}
