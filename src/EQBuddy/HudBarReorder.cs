using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// DRAG-TO-REORDER for the minimized bar's chips (#191, TheMegaSage; owner lock 2026-09-07
/// ~4:44 PM CT), and the ONE code path that may write <c>AppSettings.MiniBarOrder</c>.
///
/// **The chip is the HANDLE, and the vocabulary is one sentence: a drag that starts on a
/// CHIP is about the chip; a drag that starts on GROUND is about the window.** OE-8 taught
/// the two companion windows that their body places the window; the widget's own ground
/// still starts a <c>DragMove</c> from <c>MainWindow.OnDrag</c>. Neither is touched here,
/// and neither can be reached from a chip: <c>HudBarView.AttachGestures</c> has always set
/// <c>Handled</c> on a chip's mouse-down precisely to keep clicks out of that
/// <c>DragMove</c>, so press-and-move on a chip was DEAD SPACE before this class existed.
/// Reorder claims dead space rather than competing for a live gesture, which is why no
/// window-level threshold against free-drag is needed at all.
///
/// **What it does move is the CLICK.** A chip used to act on mouse-DOWN; it now acts on the
/// UP that never crossed the threshold. <see cref="Consumed"/> is how the chip's own handler
/// asks whether the gesture turned into something else — read while this class is still
/// mid-gesture, which is why its handler on the host is a BUBBLING one: the chip's up runs
/// first, then this one finishes.
///
/// **A manual move loop, and a manual capture, for <c>HudDragGrip</c>'s reasons.** A drag
/// needs an unambiguous END — the order is written there and nowhere else, which is what
/// keeps the toolkit's own once-a-second rebuild out of the setting by construction rather
/// than by a flag (trap 49) — and a press must still be able to be a click.
///
/// **The chips are SMALL, so capture is taken on the press and in SubTree mode**, the half
/// <c>HudDragGrip</c> paid for: a chip is about twenty units tall, so a horizontal drag that
/// starts at its centre has the pointer off the element well before it has travelled the
/// system's drag distance, and an uncaptured element stops receiving <c>MouseMove</c> the
/// moment the pointer leaves it. SubTree rather than the default so hit-testing INSIDE the
/// bar is untouched and only events outside it are routed here.
///
/// **Nothing this class draws changes a measurement** (trap 12). The carried chip moves by
/// <c>RenderTransform</c> and the insertion mark is an ADORNER — both are ink in a layer
/// above the layout — because the widget is <c>SizeToContent</c> over a fullscreen game and
/// a drag that measured the bar wider would ask the window manager to resize an
/// always-on-top window on every mouse-move (#173's mechanism).
///
/// The affordance FACE — the drag sentence in the tooltip, the cursor, and where a "back to
/// the default order" control lives — is Bevel's at the review. This is the mechanism under
/// whichever face lands.
/// </summary>
internal sealed class HudBarReorder
{
    private readonly Panel _host;
    private readonly AppSettings _settings;
    private readonly Action _onDropped;
    private readonly Action _onGestureStart;

    /// <summary>The reorderable chips of the CURRENT render, in draw order:
    /// <c>MiniBarPresentation.DrawnKeys</c>'s answer with an element attached to each. The
    /// trio and the pinned watch chips are deliberately not in here — they register nothing,
    /// so a press on one is a press this class never saw.</summary>
    private readonly List<(string Key, FrameworkElement Chip)> _chips = [];

    private Point _pressScreen;
    private int _from = -1;
    private bool _pressed;
    private bool _dragging;
    private bool _dead;
    private int _to = -1;
    private IReadOnlyList<double> _midpoints = [];
    private TranslateTransform? _carry;
    private InsertionMark? _mark;

    /// <summary>True while a chip is actually being carried. <c>HudBarView.Render</c> reads
    /// it and returns: the bar rebuilds every second, and replacing the elements under a
    /// captured drag would take the chip out of the player's hand mid-gesture. Everything
    /// else on the widget goes on ticking; only this one panel defers, and only until the
    /// drop.</summary>
    public bool Dragging => _dragging;

    /// <summary>Has this gesture stopped being available as a CLICK? True once it has become
    /// a drag, and true for the vertical wipe that means nothing — a press that left the bar
    /// downwards must not pin a peek on the way out.</summary>
    public bool Consumed => _dragging || _dead;

    /// <summary>
    /// Presses this bar has SEEN and drops it has WRITTEN — the <c>hudCellGrip</c> dump
    /// fact, and it exists for the reason <c>HudDragGrip</c>'s twin does: when a harness
    /// reports "a chip drag reordered nothing", that is equally consistent with the pointer
    /// never reaching a chip, the move never crossing the threshold, and the whole gesture
    /// running with the write not happening. Two counters separate the three in one run
    /// (trap 56 — ship the instrument before the third theory).
    /// </summary>
    public int PressCount { get; private set; }

    /// <summary>See <see cref="PressCount"/>.</summary>
    public int DropCount { get; private set; }

    /// <param name="onDropped">The DROP: persist and rebuild. The only path that may write
    /// <c>MiniBarOrder</c> — see this class's note above.</param>
    /// <param name="onGestureStart">A carry has begun: close an unpinned peek, so a panel
    /// does not flicker under a moving chip. A PINNED panel is left alone and re-anchors on
    /// the next render, which <c>AnchorOf</c> already recomputes per render (#404).</param>
    public HudBarReorder(Panel host, AppSettings settings, Action onDropped, Action onGestureStart)
    {
        _host = host;
        _settings = settings;
        _onDropped = onDropped;
        _onGestureStart = onGestureStart;
        // PREVIEW on the way down, so the press is seen before the chip's own handler sets
        // Handled (it must set it — that is the DragMove suppression this bar has always
        // needed). Not handled here, so the chip still gets it and the gesture can still
        // turn out to be its click.
        host.PreviewMouseLeftButtonDown += OnDown;
        host.PreviewMouseMove += OnMove;
        // BUBBLING on the way up, and handledEventsToo, so this runs AFTER the chip's own up
        // handler — which reads Consumed to decide whether it still owns a click. A preview
        // handler here would answer that question before it was asked.
        host.AddHandler(UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnUp), handledEventsToo: true);
        host.LostMouseCapture += (_, _) => Finish(commit: true);
    }

    /// <summary>Called by <c>HudBarView.Render</c> as it builds the bar: this chip draws
    /// <paramref name="key"/> and may be carried. Order of registration IS draw order.</summary>
    public void Register(string key, FrameworkElement chip) => _chips.Add((key, chip));

    /// <summary>Called at the top of every render, before the chips are rebuilt.</summary>
    public void Clear() => _chips.Clear();

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        // A double-click is somebody's verb — the opt-in gesture opens a chip's window — and
        // never a drag. Letting it arm would start a carry from the second press.
        if (e.ClickCount > 1) { _pressed = false; return; }
        _from = IndexOf(e.OriginalSource as DependencyObject);
        if (_from < 0) return;             // the trio, a watch pin, or the bar's own ground
        PressCount++;
        _pressed = true;
        _dragging = false;
        _dead = false;
        _to = _from;
        _pressScreen = _host.PointToScreen(e.GetPosition(_host));
        // Measured ONCE, here, off the untransformed chips: every later move compares the
        // pointer against these, so the numbers cannot drift as the carried chip is
        // translated away from where it was drawn.
        _midpoints = Midpoints();
        Mouse.Capture(_host, CaptureMode.SubTree);
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || _dead || e.LeftButton != MouseButtonState.Pressed) return;
        var now = _host.PointToScreen(e.GetPosition(_host));
        var dx = now.X - _pressScreen.X;
        var dy = now.Y - _pressScreen.Y;

        if (!_dragging)
        {
            // The SYSTEM's own thresholds, never a number invented here. The decision is
            // MiniBarDrag's and is unit-tested with no window.
            switch (MiniBarDrag.Classify(dx, dy,
                SystemParameters.MinimumHorizontalDragDistance,
                SystemParameters.MinimumVerticalDragDistance))
            {
                case MiniBarGesture.Pending: return;
                case MiniBarGesture.Dead: _dead = true; return;
                default: break;
            }
            _dragging = true;
            _onGestureStart();
            BeginCarry();
        }

        var chip = _chips[_from].Chip;
        if (_carry is not null) _carry.X = dx;
        chip.Opacity = 0.65;
        _to = MiniBarDrag.DropIndex(_midpoints, _from, PointerX(now));
        _mark?.MoveTo(MarkX(_to));
        e.Handled = true;
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        var wasDrag = _dragging;
        Finish(commit: true);
        if (wasDrag) e.Handled = true;
    }

    /// <summary>The end of the gesture, from either side — the button coming up, or the
    /// capture being taken away (an Alt-Tab, a lock screen, the app closing). Both are ends,
    /// and an order the player has visibly made must not be lost because the second kind
    /// happened.</summary>
    private void Finish(bool commit)
    {
        var wasDragging = _dragging;
        var wasPressed = _pressed;
        var from = _from;
        var to = _to;
        _pressed = false;
        _dragging = false;
        _dead = false;
        _from = -1;
        _to = -1;
        EndCarry(from);
        // Released whether or not the gesture became a drag: the capture was taken on the
        // press, so a plain click has to hand it back too or the next click anywhere on the
        // desk goes nowhere.
        if (wasPressed && ReferenceEquals(Mouse.Captured, _host)) Mouse.Capture(null);
        if (!wasDragging || !commit) return;
        if (from < 0 || to < 0 || to == from) { _onDropped(); return; }
        // THE WRITE, and the only one. MiniBarDrag re-inserts the carried key beside its new
        // neighbour in the FULL order, so the keys with no ★ keep the places the player left
        // them in.
        MiniBarPresentation.SetOrder(_settings, MiniBarDrag.Move(
            MiniBarPresentation.ResolveOrder(_settings),
            [.. _chips.Select(c => c.Key)], from, to));
        DropCount++;
        _onDropped();
    }

    // ---- INK ------------------------------------------------------------------------
    //
    // Everything below moves pixels and measures nothing (trap 12).

    private void BeginCarry()
    {
        var chip = _chips[_from].Chip;
        _carry = new TranslateTransform();
        chip.RenderTransform = _carry;
        chip.Cursor = Cursors.SizeWE;
        if (AdornerLayer.GetAdornerLayer(_host) is { } layer)
        {
            _mark = new InsertionMark(_host);
            layer.Add(_mark);
            _mark.MoveTo(MarkX(_from));
        }
    }

    private void EndCarry(int from)
    {
        if (from >= 0 && from < _chips.Count)
        {
            var chip = _chips[from].Chip;
            chip.RenderTransform = Transform.Identity;
            chip.Opacity = 1;
            chip.Cursor = Cursors.Hand;
        }
        _carry = null;
        if (_mark is null) return;
        AdornerLayer.GetAdornerLayer(_host)?.Remove(_mark);
        _mark = null;
    }

    // ---- MEASUREMENT ----------------------------------------------------------------

    /// <summary>Each drawn chip's horizontal centre, in the HOST's own coordinate space.
    /// Through the framework's transform rather than by adding up margins (trap 1): the bar
    /// sits under the widget's UI-scale <c>LayoutTransform</c>, and the pointer positions
    /// these are compared against come from the same <c>_host</c>.</summary>
    private IReadOnlyList<double> Midpoints()
    {
        var mids = new List<double>(_chips.Count);
        foreach (var (_, chip) in _chips)
        {
            var left = LeftOf(chip);
            mids.Add(double.IsNaN(left) ? double.MaxValue : left + chip.ActualWidth / 2);
        }
        return mids;
    }

    private double LeftOf(FrameworkElement chip)
    {
        if (!chip.IsVisible) return double.NaN;
        try { return chip.TransformToAncestor(_host).Transform(default).X; }
        catch (InvalidOperationException) { return double.NaN; }
    }

    /// <summary>The pointer, in the same space <see cref="Midpoints"/> answers in.</summary>
    private double PointerX(Point screen) => _host.PointFromScreen(screen).X;

    /// <summary>Where the insertion mark is drawn for a landing slot: the leading edge of the
    /// chip that will be to its right, or the trailing edge of the bar for the last slot.</summary>
    private double MarkX(int slot)
    {
        if (slot < 0 || _chips.Count == 0) return 0;
        // Landing to the RIGHT of where it started means the mark belongs after that chip;
        // landing to the left means before it. Both are read off the chip's own box.
        var chip = _chips[Math.Min(slot, _chips.Count - 1)].Chip;
        var left = LeftOf(chip);
        if (double.IsNaN(left)) return 0;
        return slot > _from ? left + chip.ActualWidth : left;
    }

    private int IndexOf(DependencyObject? source)
    {
        for (var node = source; node is not null; node = VisualTreeHelperParent(node))
            for (var i = 0; i < _chips.Count; i++)
                if (ReferenceEquals(_chips[i].Chip, node)) return i;
        return -1;
    }

    private static DependencyObject? VisualTreeHelperParent(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);

    /// <summary>The insertion caret: one accent hairline in the adorner layer, at the x the
    /// carried chip would land at. **An adorner and not a spacer**, because a spacer is
    /// layout and this bar's layout is the widget's width (trap 12).</summary>
    private sealed class InsertionMark : Adorner
    {
        private double _x;

        // Never hit-testable: an adorner sits above the content, and one that answered the
        // mouse would take the drop out of the hand carrying it.
        public InsertionMark(UIElement adorned) : base(adorned) => IsHitTestVisible = false;

        public void MoveTo(double x) { _x = x; InvalidateVisual(); }

        protected override void OnRender(DrawingContext dc)
        {
            var brush = (Application.Current?.TryFindResource("AccentBrush") as Brush)
                ?? Brushes.White;
            var height = ((FrameworkElement)AdornedElement).ActualHeight;
            dc.DrawRectangle(brush, null, new Rect(_x - 1, 0, 2, height));
        }
    }
}
