using System.Globalization;
using EQBuddy.Core;

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
    /// <summary>The derived keys, in the order <see cref="Derive"/> yields them: the four
    /// alpha/lightness tones, then one <c>Kind&lt;Name&gt;Brush</c> per meter kind in
    /// <see cref="OutputKindPresentation.Order"/>.</summary>
    public static readonly string[] Keys =
        ["HairlineBrush", "TrackBrush", "RaisedBrush", "AccentDeepBrush",
         .. OutputKindPresentation.Order.Select(OutputKindPresentation.BrushKey)];

    /// <summary>
    /// The meter-kind colours (Founder's option A, 2026-09-29), one per
    /// <see cref="OutputKindPresentation.Order"/> entry, for a DARK ground. They are not
    /// derived from the palette the way the four tones are — a kind's hue is its identity,
    /// and a "Spells are blue" that turned brass in ParchmentBrass would be a legend that
    /// changes meaning per theme — but they are DERIVED KEYS all the same: which set a theme
    /// gets is decided here from its ground, so no theme row has to carry eleven more values
    /// and a new theme (Custom included) is covered for free.
    ///
    /// Two differ from the mockup: Proc (#e0679a → #e36f9f) and Other (#8a8f98 → #959aa3),
    /// each lifted just enough to clear 3:1 against SolarizedDark's panel, the lowest-contrast
    /// ground a dark theme ships (<c>OutputKindColourTests</c>).
    /// </summary>
    public static readonly string[] KindDark =
    [
        "#FFD9A441", "#FFE8743B", "#FFC9B98A", "#FF5AA9E6", "#FF9B86D6", "#FF3FBFAE",
        "#FFE36F9F", "#FF6BBF59", "#FF4CC38A", "#FF46B3D6", "#FF959AA3",
    ];

    /// <summary>The same kinds for a LIGHT ground (Solarized), same order — deeper cuts of the
    /// same hues, so a kind reads as the same colour in both.</summary>
    public static readonly string[] KindLight =
    [
        "#FF9A6F12", "#FFB8521C", "#FF7D6C3A", "#FF1F6FB2", "#FF6552B0", "#FF16877A",
        "#FFB3386E", "#FF3F8A34", "#FF237D52", "#FF1B7C96", "#FF6B7075",
    ];

    /// <summary>A ground whose relative luminance is above this takes <see cref="KindLight"/>.
    /// The shipped grounds sit at ≤ 0.02 (dark) and 0.92 (Solarized); the midpoint keeps a
    /// Custom theme on whichever side its own background is.</summary>
    public const double LightGroundLuminance = 0.4;

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

        // The meter kinds: the light set on a light ground, the dark set otherwise. A palette
        // with no BgBrush (a partial one a test hands in) is read as dark, the common case.
        //
        // **An explicit Kind*Brush row in the palette WINS** — that is how the player's own
        // colour for a type (KindColours, Options → Look) reaches every surface through this one
        // producer: CustomTheme.PaletteFor appends the picks as palette rows, and they override
        // the default here in every theme. The lookup is by the kind's KEY, never its position.
        var kinds = by.TryGetValue("BgBrush", out var bg) && IsLightGround(bg) ? KindLight : KindDark;
        var order = OutputKindPresentation.Order;
        for (var i = 0; i < order.Count; i++)
        {
            var key = OutputKindPresentation.BrushKey(order[i]);
            yield return (key, by.TryGetValue(key, out var picked) && IsHex(picked) ? picked : kinds[i]);
        }
    }

    /// <summary>Whether a ground colour (alpha ignored — it is the see-through, not the hue)
    /// is light enough to take the light kind set.</summary>
    public static bool IsLightGround(string hex) =>
        RelativeLuminance(hex) > LightGroundLuminance;

    /// <summary>WCAG relative luminance of a colour's RGB (alpha ignored).</summary>
    public static double RelativeLuminance(string hex)
    {
        var (_, r, g, b) = Parse(hex);
        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);

        static double Linear(byte c)
        {
            var v = c / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
    }

    /// <summary>Whether a value reads as #AARRGGBB or #RRGGBB — an explicit kind row that does
    /// not is ignored rather than thrown on.</summary>
    private static bool IsHex(string hex)
    {
        var body = hex.AsSpan().TrimStart('#');
        return (body.Length is 6 or 8) && hex.StartsWith('#')
            && uint.TryParse(body, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
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
