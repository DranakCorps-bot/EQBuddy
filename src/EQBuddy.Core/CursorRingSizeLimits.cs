namespace EQBuddy.Core;

/// <summary>
/// The cursor ring's diameter, in device-independent pixels (DRA-3202, discussion #1067).
/// One range for the Options slider, the ring window, and a hand-edited
/// <c>settings.json</c> — a second copy of 20 and 160 is how those three drift.
/// </summary>
public static class CursorRingSizeLimits
{
    public const double Min = 20;
    public const double Max = 160;
    public const double Default = 46;

    /// <summary>A size the ring can be drawn at. <see cref="double.NaN"/> becomes
    /// <see cref="Default"/> because <c>Math.Clamp</c> leaves NaN alone, and a NaN
    /// window size is the crash an out-of-range edit was not supposed to be.</summary>
    public static double Clamp(double value) =>
        double.IsNaN(value) ? Default : Math.Clamp(value, Min, Max);

    /// <summary>Writes <paramref name="size"/> back clamped. False when it was already
    /// inside the range, so a load of an ordinary profile does not save.</summary>
    public static bool TryNormalize(ref double size)
    {
        var clamped = Clamp(size);
        // NaN compares unequal to everything, including itself, so it must not take
        // the "already fine" path.
        if (!double.IsNaN(size) && clamped == size) return false;
        size = clamped;
        return true;
    }
}
