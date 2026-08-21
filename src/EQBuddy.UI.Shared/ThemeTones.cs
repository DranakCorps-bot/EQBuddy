using System.Globalization;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The tones every UI DERIVES from a palette rather than storing per theme (the
/// 2026-08-11 modernization): hairlines, bar tracks, raised chips, the deep end of a
/// bar gradient. They are alpha/lightness variations of two palette keys, so a new
/// theme gets them for free — and they live here, not in a UI, because WPF composes
/// brushes from them, Avalonia will, and EQBuddy Mobile ships them to the phone.
/// Diverging copies is exactly the bug <see cref="ThemePalettes"/>'s header describes.
/// </summary>
public static class ThemeTones
{
    /// <summary>The derived keys, in the order <see cref="Derive"/> yields them.</summary>
    public static readonly string[] Keys =
        ["HairlineBrush", "TrackBrush", "RaisedBrush", "AccentDeepBrush"];

    /// <summary>Derives the tones from a full palette (<see cref="ThemePalettes.For"/> or
    /// <see cref="CustomTheme.PaletteFor"/>), as #AARRGGBB like every palette value.</summary>
    public static IEnumerable<(string Key, string Hex)> Derive(IEnumerable<(string Key, string Hex)> palette)
    {
        // Tolerates a repeated key (last wins) rather than throwing: callers hand this
        // whatever palette they have, including one that already carries derived tones,
        // and a colour lookup is no place to be strict about duplicates.
        var by = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, hex) in palette) by[key] = hex;
        var (aa, ar, ag, ab) = Parse(by["AccentBrush"]);
        var (pa, pr, pg, pb) = Parse(by["PanelBrush"]);

        // Card borders: the accent at a whisper instead of a solid line.
        yield return ("HairlineBrush", Hex(0x26, ar, ag, ab));
        // The empty part of a stat bar, under the accent-filled part.
        yield return ("TrackBrush", Hex(0x1E, ar, ag, ab));
        // Chips and tiles, one step above panel.
        yield return ("RaisedBrush", Hex((byte)Math.Min(255, pa * 3 / 2), pr, pg, pb));
        // Gradient start for bar fills: the accent pulled toward the ground.
        yield return ("AccentDeepBrush", Hex(aa, (byte)(ar * 6 / 10), (byte)(ag * 6 / 10), (byte)(ab * 6 / 10)));
    }

    /// <summary>#AARRGGBB or #RRGGBB (alpha defaults to opaque) → channels.</summary>
    public static (byte A, byte R, byte G, byte B) Parse(string hex)
    {
        var body = hex.AsSpan().TrimStart('#');
        var v = uint.Parse(body, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (body.Length == 6) v |= 0xFF000000;
        return ((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public static string Hex(byte a, byte r, byte g, byte b) => $"#{a:X2}{r:X2}{g:X2}{b:X2}";
}
