using System.Globalization;
using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The "Custom" theme: the user picks three solid colors — background, text, accent —
/// and the other fourteen palette keys are derived here (field request 2026-08-03:
/// "let us pick primary and background?"). Derivation, not free editing of all 17
/// keys, so every custom theme keeps the guarantees the built-ins have: the text is
/// auto-corrected until it clears the same 4.5:1 contrast floor ThemePaletteTests
/// enforces, and the status colors stay the shared green/red/amber so alerts read
/// the same in every theme.
/// </summary>
public static class CustomTheme
{
    public const string Key = "Custom";

    // Seed colors: the Grey theme's neutrals with the brass accent, so switching to
    // Custom before touching anything looks familiar rather than broken.
    public const string DefaultBg = "#1A1A1A";
    public const string DefaultText = "#EAEAEA";
    public const string DefaultAccent = "#E3B341";

    /// <summary>The palette row for the seed colors — registered in
    /// <see cref="ThemePalettes"/> so "Custom" behaves like any catalog theme (and the
    /// palette tests cover it) even before the user picks colors.</summary>
    public static string[] SeedRow { get; } =
        [.. Derive(DefaultBg, DefaultText, DefaultAccent).Select(e => e.Hex)];

    /// <summary>The palette for the current settings: the derived custom palette when
    /// the Custom theme is active (each unparseable color falls back to its seed),
    /// otherwise the selected catalog theme.</summary>
    public static IEnumerable<(string Key, string Hex)> PaletteFor(AppSettings settings) =>
        settings.Theme == Key
            ? Derive(
                Valid(settings.CustomThemeBg) ?? DefaultBg,
                Valid(settings.CustomThemeText) ?? DefaultText,
                Valid(settings.CustomThemeAccent) ?? DefaultAccent)
            : ThemePalettes.For(settings.Theme);

    /// <summary>Derives all 17 palette keys, in <see cref="ThemePalettes.Keys"/> order,
    /// from three #RRGGBB colors. Callers pass validated input (see
    /// <see cref="Valid"/>); alpha in the output follows the built-in themes'
    /// conventions (translucent panels and washes, near-opaque background).</summary>
    public static IEnumerable<(string Key, string Hex)> Derive(string bg, string text, string accent)
    {
        var (br, bgc, bb) = Rgb(bg);
        var dark = Luminance(br, bgc, bb) < 0.35;
        var textFixed = EnsureContrast(text, bg, 4.5);
        var dim = EnsureContrast(Blend(textFixed, bg, 0.42), bg, 3.0);

        yield return ("BgBrush", "#F2" + Hex6(bg));
        yield return ("PanelBrush", dark ? "#26FFFFFF" : "#14000000");
        yield return ("PanelHoverBrush", "#33" + Hex6(accent));
        yield return ("BorderBrush", "#66" + Hex6(accent));
        yield return ("TextBrush", "#FF" + Hex6(textFixed));
        yield return ("DimBrush", "#FF" + Hex6(dim));
        yield return ("AccentBrush", "#FF" + Hex6(accent));
        yield return ("GoodBrush", "#FF7FBF5F");
        yield return ("BadBrush", "#FFD9634F");
        yield return ("WarnBrush", "#FFE0A030");
        yield return ("PopupBrush", "#FF" + Hex6(Shift(bg, dark ? 10 : -10)));
        yield return ("ComboBoxBrush", "#FF" + Hex6(Shift(bg, dark ? 14 : -14)));
        yield return ("ToggleHighlightBrush", "#33" + Hex6(accent));
        yield return ("ScrollThumbBrush", "#40" + Hex6(accent));
        yield return ("ScrollThumbHoverBrush", "#8C" + Hex6(accent));
        yield return ("GoodWashBrush", "#337FBF5F");
        yield return ("WarnWashBrush", "#33E0A030");
        // Fixed like the status trio: incoming damage keeps its cool hue so it reads
        // the same in every custom palette.
        yield return ("IncomingBrush", "#FF6FA8B8");
        // Chart-series steps stay fixed too (the validated ParchmentBrass set): a
        // user-picked accent can be anything, and deriving series colors from it
        // would undo the colorblind-separation work the fixed set encodes.
        yield return ("ChartYouBrush", "#FFB4892C");
        yield return ("ChartPetBrush", "#FF5CA352");
        yield return ("ChartIncomingBrush", "#FF4796DB");
        yield return ("ChartCritBrush", "#FFF0BC55");
        // DRA-352 D1: mez chicklets in blue. The dark-theme blue on a dark ground and
        // Solarized's blue on a light one, then pushed to the same 4.5:1 floor the text
        // takes — a user-picked background can be anything, including blue.
        yield return ("MezChipBrush",
            "#FF" + Hex6(EnsureContrast(dark ? "#6CB4F0" : "#1E6EA7", bg, 4.5)));
    }

    /// <summary>Accepts #RRGGBB (or #AARRGGBB, alpha discarded — some users will paste
    /// values straight out of settings.json); anything else is null.</summary>
    public static string? Valid(string? hex)
    {
        if (hex is null) return null;
        hex = hex.Trim();
        if (hex.Length == 9 && hex[0] == '#') hex = "#" + hex[3..];
        if (hex.Length != 7 || hex[0] != '#') return null;
        return uint.TryParse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? hex.ToUpperInvariant() : null;
    }

    /// <summary>Nudges a color toward whichever of white/black contrasts better with
    /// the background, until it clears the required WCAG ratio. The better pole always
    /// reaches ≥4.58:1 (the worst case, a mid-grey background, sits at the point where
    /// both poles tie) — picking by background darkness instead fails there: white on
    /// #808080 tops out at 3.9:1.</summary>
    private static string EnsureContrast(string color, string bg, double floor)
    {
        if (Contrast(color, bg) >= floor) return color;
        var pole = Contrast("#FFFFFF", bg) >= Contrast("#000000", bg) ? "#FFFFFF" : "#000000";
        for (var t = 0.1; t < 1.0; t += 0.1)
        {
            var candidate = Blend(pole, color, 1 - t);
            if (Contrast(candidate, bg) >= floor) return candidate;
        }
        return pole;
    }

    private static (int R, int G, int B) Rgb(string hex)
    {
        var v = uint.Parse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((int)(v >> 16) & 0xFF, (int)(v >> 8) & 0xFF, (int)v & 0xFF);
    }

    private static string Hex6(string hex) => hex[1..].ToUpperInvariant();

    private static string From(int r, int g, int b) => $"#{r:X2}{g:X2}{b:X2}";

    private static string Blend(string a, string b, double t)
    {
        var (ar, ag, ab) = Rgb(a);
        var (br, bgc, bb) = Rgb(b);
        return From(
            (int)Math.Round(ar * (1 - t) + br * t),
            (int)Math.Round(ag * (1 - t) + bgc * t),
            (int)Math.Round(ab * (1 - t) + bb * t));
    }

    private static string Shift(string hex, int delta)
    {
        var (r, g, b) = Rgb(hex);
        return From(Math.Clamp(r + delta, 0, 255), Math.Clamp(g + delta, 0, 255), Math.Clamp(b + delta, 0, 255));
    }

    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(Rgb(a)), Luminance(Rgb(b)));
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance((int R, int G, int B) c)
    {
        double Channel(int v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static double Luminance(int r, int g, int b) => Luminance((r, g, b));
}
