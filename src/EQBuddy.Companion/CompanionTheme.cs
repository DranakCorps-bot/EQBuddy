using System.Globalization;
using EQBuddy.UI.Shared;

namespace EQBuddy.Companion;

/// <summary>The desktop's live palette, ready to drop into CSS custom properties.
/// <see cref="Stamp"/> changes only when a color does — the server sends the section
/// on connect and after a theme swap, never on the tick in between.</summary>
public sealed record CompanionThemeSection(
    string Key,
    string Stamp,
    IReadOnlyDictionary<string, string> Colors);

/// <summary>
/// Palette → phone. The phone follows the desktop's theme, custom ones included, so a
/// tablet propped beside the monitor doesn't look like a different program.
///
/// THE CONVERSION (#AARRGGBB → CSS). Palette values carry alpha because the desktop
/// widget floats over the running game; the browser needs web colors:
///   • opaque (AA = FF) → "#RRGGBB".
///   • translucent      → "rgba(R, G, B, A/255)" — alpha PRESERVED rather than
///     flattened, because the page layers exactly like the widget does (panels and
///     washes over the background) and CSS composites it for free.
///   • the background token is the ONE exception: always emitted opaque. Its alpha
///     exists so the GAME shows through, and behind a browser page there is nothing
///     to show through — honoring it would blend the phone's own white paper in.
/// Derived tones (hairline, track, raised, accent-deep) come from
/// <see cref="ThemeTones"/>, the same helper WPF composes its brushes from, so the
/// phone can never drift from the desktop's derived look.
/// </summary>
public static class CompanionTheme
{
    /// <summary>Wire token → palette key. The wire names are the page's CSS custom
    /// properties in camelCase (bg → --bg, panelHover → --panel-hover);
    /// CompanionThemeTests pins that every var() the page uses is a name in here.</summary>
    private static readonly (string Token, string PaletteKey)[] TokenMap =
    [
        ("bg", "BgBrush"),
        ("panel", "PanelBrush"),
        ("panelHover", "PanelHoverBrush"),
        ("border", "BorderBrush"),
        ("text", "TextBrush"),
        ("dim", "DimBrush"),
        ("accent", "AccentBrush"),
        ("good", "GoodBrush"),
        ("bad", "BadBrush"),
        ("warn", "WarnBrush"),
        ("popup", "PopupBrush"),
        ("goodWash", "GoodWashBrush"),
        ("warnWash", "WarnWashBrush"),
        ("incoming", "IncomingBrush"),
        ("chartYou", "ChartYouBrush"),
        ("chartPet", "ChartPetBrush"),
        ("chartIncoming", "ChartIncomingBrush"),
        ("chartCrit", "ChartCritBrush"),
        ("hairline", "HairlineBrush"),
        ("track", "TrackBrush"),
        ("raised", "RaisedBrush"),
        ("accentDeep", "AccentDeepBrush"),
    ];

    /// <summary>Rows the page needs that no palette key carries: a wash of the accent
    /// for "this one is DUE" rows, in the idiom of GoodWash/WarnWash.</summary>
    private const byte AccentWashAlpha = 0x33;

    /// <summary>Every token the page may reference, for the test that pins the page
    /// against the projection.</summary>
    public static IReadOnlyList<string> Tokens { get; } =
        [.. TokenMap.Select(t => t.Token), "accentWash"];

    /// <summary>Project a palette (<see cref="ThemePalettes.For"/> or
    /// <see cref="CustomTheme.PaletteFor"/>) for the wire.</summary>
    public static CompanionThemeSection Project(string themeKey, IEnumerable<(string Key, string Hex)> palette)
    {
        var rows = palette.ToList();
        // EXPLICIT WINS, DERIVED FILLS GAPS — and this must survive being handed a palette
        // that already carries its derived tones.
        //
        // ThemeManager.PaletteApplied broadcasts exactly that: its own summary says "the
        // full palette (the derived tones included)". This method then derived them a
        // SECOND time and fed both into a ToDictionary, which throws on the duplicate key.
        // CompanionHost.SetTheme catches, so nothing crashed and nothing was fixed: the
        // phone simply never received a palette and rendered in its own CSS defaults. It
        // had logged 563 times on David's machine before anyone read the file (2026-08-21).
        //
        // A contract changed underneath a caller and the caller kept deriving. Tolerating
        // both shapes is the fix, not asking the caller to stop — the derivation is a
        // FALLBACK by definition, so a palette that states its own HairlineBrush is stating
        // a preference and should keep it.
        var by = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, hex) in rows) by[key] = hex;
        foreach (var (key, hex) in ThemeTones.Derive(rows)) by.TryAdd(key, hex);

        var colors = new Dictionary<string, string>(TokenMap.Length + 1, StringComparer.Ordinal);
        foreach (var (token, key) in TokenMap)
            if (by.TryGetValue(key, out var hex))
                colors[token] = Web(hex, opaque: token == "bg");
        if (by.TryGetValue("AccentBrush", out var accent))
        {
            var (_, r, g, b) = ThemeTones.Parse(accent);
            colors["accentWash"] = Web(ThemeTones.Hex(AccentWashAlpha, r, g, b));
        }

        return new CompanionThemeSection(themeKey, Stamp(themeKey, colors), colors);
    }

    /// <summary>#AARRGGBB → a CSS color. <paramref name="opaque"/> drops the alpha
    /// (see the class header: only the page background wants that).</summary>
    internal static string Web(string hex, bool opaque = false)
    {
        var (a, r, g, b) = ThemeTones.Parse(hex);
        if (opaque || a == 0xFF) return $"#{r:X2}{g:X2}{b:X2}";
        return string.Create(CultureInfo.InvariantCulture,
            $"rgba({r}, {g}, {b}, {Math.Round(a / 255.0, 3)})");
    }

    private static string Stamp(string themeKey, Dictionary<string, string> colors)
    {
        var sb = new System.Text.StringBuilder(themeKey, 256);
        foreach (var (token, value) in colors) sb.Append('|').Append(token).Append(':').Append(value);
        return themeKey + "#" + CompanionHash.Of(sb.ToString());
    }
}

/// <summary>FNV-1a over a string, hex. Not security — just "did these bytes change",
/// for payloads too big to keep a copy of per connected device (map geometry) or too
/// wide to compare field by field (a palette).</summary>
public static class CompanionHash
{
    public static string Of(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }
}
