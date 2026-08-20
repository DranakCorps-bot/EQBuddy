namespace EQBuddy.UI.Shared;

/// <summary>
/// The one decision both widgets make about a QR code before drawing it: how much
/// blank margin the spec wants around the modules, and which way up the matrix is.
///
/// The pixel FORMAT is each toolkit's own business — WPF paints a 1-bpp BlackWhite
/// bitmap, Avalonia a BGRA one — but the quiet zone is not a rendering detail. Four
/// modules is what the QR spec requires for a scanner to find the symbol at all, and a
/// pairing code that will not scan is the whole feature failing on one screen and
/// working on the other. Two hand-copied constants is precisely the shape of divergence
/// CLAUDE.md's "if a fix exists in UI.Shared, both UIs must use it" is written about.
/// </summary>
public static class QrRaster
{
    /// <summary>The blank margin the spec requires, in modules.</summary>
    public const int QuietZone = 4;

    /// <summary>The module matrix with the quiet zone added on all four sides.
    /// <c>true</c> is a DARK module, the same sense <c>QrEncoder.Encode</c> returns.</summary>
    public static bool[,] WithQuietZone(bool[,] modules, int quietZone = QuietZone)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quietZone);
        var n = modules.GetLength(0);
        var size = n + quietZone * 2;
        var padded = new bool[size, size];
        for (var r = 0; r < n; r++)
            for (var c = 0; c < n; c++)
                padded[r + quietZone, c + quietZone] = modules[r, c];
        return padded;
    }
}
