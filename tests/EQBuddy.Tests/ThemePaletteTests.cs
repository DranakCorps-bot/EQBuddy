using System.Globalization;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The palette table is the single source of color for both UIs, and neither one can
/// report a problem with it at runtime: WPF resolves palette keys via DynamicResource and
/// Avalonia looks them up in a dictionary, so a missing key paints an invisible control
/// and a malformed value throws deep inside a UI that isn't running in CI. These tests are
/// where that gets caught instead.
/// </summary>
public class ThemePaletteTests
{
    [Fact]
    public void EveryCatalogThemeHasAPalette() =>
        Assert.Equal(
            ThemeCatalog.Themes.Select(t => t.Key).OrderBy(k => k),
            ThemePalettes.DefinedThemes.OrderBy(k => k));

    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void EveryThemeDefinesEveryKey(string theme)
    {
        var defined = ThemePalettes.For(theme).Select(entry => entry.Key).ToList();
        Assert.Equal(ThemePalettes.Keys, defined);
    }

    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void EveryValueIsParseableArgbHex(string theme)
    {
        foreach (var (key, hex) in ThemePalettes.For(theme))
        {
            Assert.True(hex.Length == 9 && hex[0] == '#',
                $"{theme}.{key} = '{hex}' — expected #AARRGGBB");
            Assert.True(
                uint.TryParse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _),
                $"{theme}.{key} = '{hex}' is not hex");
        }
    }

    /// <summary>An unknown theme (an older or hand-edited settings.json) must fall back to
    /// the default rather than throwing — the app has no other palette to paint with.</summary>
    [Fact]
    public void UnknownThemeFallsBackToTheDefault() =>
        Assert.Equal(ThemePalettes.For("ParchmentBrass"), ThemePalettes.For("NoSuchTheme"));

    /// <summary>Text on background has to stay legible in every theme — the light Solarized
    /// palette is one inverted value away from grey-on-grey. Checks WCAG relative luminance
    /// contrast, ignoring alpha (both are composited over an opaque-enough surface).</summary>
    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void TextContrastsWithBackground(string theme)
    {
        var palette = ThemePalettes.For(theme).ToDictionary(e => e.Key, e => e.Hex);
        var ratio = Contrast(palette["TextBrush"], palette["BgBrush"]);
        Assert.True(ratio >= 4.5, $"{theme}: text/background contrast {ratio:0.0}:1 is below 4.5:1");
    }

    /// <summary>DRA-352 D1: the mez chicklet's blue is 11px text over a moving game, so it
    /// takes the same 4.5:1 floor as body text — in every palette, the light one included.
    /// And it must actually be a BLUE (blue channel dominant), or "mez counts down in blue"
    /// is a promise a palette row can quietly break.</summary>
    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void MezChipBlueIsLegibleAndBlue(string theme)
    {
        var palette = ThemePalettes.For(theme).ToDictionary(e => e.Key, e => e.Hex);
        var mez = palette["MezChipBrush"];
        var ratio = Contrast(mez, palette["BgBrush"]);
        Assert.True(ratio >= 4.5, $"{theme}: mez chip/background contrast {ratio:0.0}:1 is below 4.5:1");
        var v = uint.Parse(mez[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var (r, g, b) = ((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
        Assert.True(b > r && b >= g, $"{theme}: MezChipBrush {mez} is not a blue");
    }

    /// <summary>The Custom theme derives its mez blue, and the derivation holds the same
    /// floor on a background the player picked — including a blue one.</summary>
    [Theory]
    [InlineData("#1A1A1A")]
    [InlineData("#FDF6E3")]
    [InlineData("#1E6EA7")]
    [InlineData("#808080")]
    public void CustomThemeMezBlueClearsTheFloorOnAnyBackground(string bg)
    {
        var palette = CustomTheme.Derive(bg, "#EAEAEA", "#E3B341").ToDictionary(e => e.Key, e => e.Hex);
        var ratio = Contrast(palette["MezChipBrush"], palette["BgBrush"]);
        Assert.True(ratio >= 4.5, $"custom bg {bg}: mez chip contrast {ratio:0.0}:1");
    }

    public static TheoryData<string> ThemeKeys()
    {
        var data = new TheoryData<string>();
        foreach (var (key, _) in ThemeCatalog.Themes) data.Add(key);
        return data;
    }

    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        var (hi, lo) = la > lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(string hex)
    {
        var v = uint.Parse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        double Channel(int shift)
        {
            var c = ((v >> shift) & 0xFF) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(16) + 0.7152 * Channel(8) + 0.0722 * Channel(0);
    }
}
