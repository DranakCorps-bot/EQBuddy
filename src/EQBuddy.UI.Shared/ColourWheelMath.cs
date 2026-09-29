using System.Globalization;

namespace EQBuddy.UI.Shared;

/// <summary>A colour as hue (0–360°), saturation (0–1) and value/brightness (0–1).</summary>
public readonly record struct Hsv(double H, double S, double V);

/// <summary>
/// **The colour wheel's arithmetic, framework-free** so it can be unit-tested (the WPF layer
/// has no test project — docs/TestPlan.md §5). <c>EqColourWheel</c> draws a hue/saturation
/// DISC — hue is the ANGLE, saturation the RADIUS — at full brightness, and a separate slider
/// carries the value. Everything the control needs to turn a pointer into a colour and a
/// colour back into a thumb position is here.
///
/// **Angles are the maths convention on a screen**: 0° points RIGHT and hue increases
/// COUNTER-clockwise as the eye sees it. Screen y grows downward, so the y term is negated in
/// both directions — a wheel whose red sat at 3 o'clock and whose green sat at 4 o'clock
/// instead of 11 would be a wheel drawn upside down.
/// </summary>
public static class ColourWheelMath
{
    /// <summary>"#RRGGBB" (or #AARRGGBB, alpha ignored) → HSV. Null when it does not read.</summary>
    public static Hsv? FromHex(string? hex)
    {
        if (CustomTheme.Valid(hex) is not { } v) return null;
        var n = uint.Parse(v.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return FromRgb((byte)(n >> 16), (byte)(n >> 8), (byte)n);
    }

    /// <summary>HSV → "#RRGGBB".</summary>
    public static string ToHex(Hsv c)
    {
        var (r, g, b) = ToRgb(c);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static Hsv FromRgb(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == rf) h = 60 * (((gf - bf) / d) % 6);
            else if (max == gf) h = 60 * (((bf - rf) / d) + 2);
            else h = 60 * (((rf - gf) / d) + 4);
        }
        if (h < 0) h += 360;
        return new Hsv(h, max == 0 ? 0 : d / max, max);
    }

    public static (byte R, byte G, byte B) ToRgb(Hsv c)
    {
        var h = ((c.H % 360) + 360) % 360;
        var s = Math.Clamp(c.S, 0, 1);
        var v = Math.Clamp(c.V, 0, 1);
        var chroma = v * s;
        var x = chroma * (1 - Math.Abs((h / 60) % 2 - 1));
        var m = v - chroma;
        var (r, g, b) = (int)(h / 60) switch
        {
            0 => (chroma, x, 0.0),
            1 => (x, chroma, 0.0),
            2 => (0.0, chroma, x),
            3 => (0.0, x, chroma),
            4 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        return (Byte(r + m), Byte(g + m), Byte(b + m));

        static byte Byte(double f) => (byte)Math.Clamp(Math.Round(f * 255), 0, 255);
    }

    /// <summary>
    /// Where a hue and saturation sit on a disc of <paramref name="radius"/>, as an offset from
    /// its CENTRE in screen units (x right, y DOWN). Saturation 0 is the centre; 1 the rim.
    /// </summary>
    public static (double X, double Y) PointFor(double hue, double saturation, double radius)
    {
        var a = hue * Math.PI / 180;
        var r = Math.Clamp(saturation, 0, 1) * radius;
        return (r * Math.Cos(a), -r * Math.Sin(a));
    }

    /// <summary>
    /// The hue and saturation under a pointer at (<paramref name="dx"/>, <paramref name="dy"/>)
    /// from the disc's centre. A point outside the rim CLAMPS to it (a drag that leaves the
    /// disc keeps steering the hue rather than stopping), and the exact centre is hue 0.
    /// </summary>
    public static (double Hue, double Saturation) FromPoint(double dx, double dy, double radius)
    {
        if (radius <= 0) return (0, 0);
        var dist = Math.Sqrt(dx * dx + dy * dy);
        var hue = dist == 0 ? 0 : Math.Atan2(-dy, dx) * 180 / Math.PI;
        if (hue < 0) hue += 360;
        return (hue, Math.Min(1, dist / radius));
    }

    /// <summary>
    /// The colour of one pixel of the disc at full brightness, or null outside it — what the
    /// control renders once per size into its bitmap. (x, y) is the pixel's offset from the
    /// centre; a one-pixel feather is the caller's business.
    /// </summary>
    public static (byte R, byte G, byte B)? DiscPixel(double dx, double dy, double radius)
    {
        if (dx * dx + dy * dy > radius * radius) return null;
        var (h, s) = FromPoint(dx, dy, radius);
        return ToRgb(new Hsv(h, s, 1));
    }
}
