namespace EQBuddy.UI.Shared;

/// <summary>
/// The minimised bar's right-edge anchor's STATE (#942, Jeff-Crawford) — lifted out of
/// MainWindow so the hotspot does not carry it, and so the double-move rule has a test.
///
/// The width a change is compared against is the one this anchor last SAW, never
/// <c>SizeChangedEventArgs.PreviousSize</c>: a SizeChanged that lands after the mode swap has
/// already anchored itself reads a zero delta instead of moving the window twice. While
/// <see cref="Swapping"/> is set, a width is recorded and answers nothing — the swap does
/// its own <see cref="WidgetMetrics.RightAnchoredLeft"/>. The arithmetic stays
/// <see cref="WidgetMetrics.MiniBarLeft"/>'s; this class only remembers.
/// </summary>
public sealed class MiniBarAnchor
{
    private double _width;

    /// <param name="seed"><see cref="WidgetMetrics.MiniBarAnchorSeed"/>'s answer.</param>
    public MiniBarAnchor(double seed) => _width = seed;

    /// <summary>True while the mode swap re-measures; the swap anchors itself.</summary>
    public bool Swapping { get; private set; }

    /// <summary>Set <see cref="Swapping"/> for the life of the returned scope.</summary>
    public IDisposable Swap()
    {
        Swapping = true;
        return new Scope(this);
    }

    private sealed class Scope(MiniBarAnchor owner) : IDisposable
    {
        public void Dispose() => owner.Swapping = false;
    }

    /// <summary>Record a width without moving anything (the swap's own result).</summary>
    public void Saw(double width) => _width = width;

    /// <summary>The Left for a bar that has just become <paramref name="width"/> wide.</summary>
    public double LeftFor(double width, bool minimized, bool growsLeft, double left)
    {
        var old = _width;
        _width = width;
        return Swapping ? left : WidgetMetrics.MiniBarLeft(minimized, growsLeft, left, old, width);
    }
}
