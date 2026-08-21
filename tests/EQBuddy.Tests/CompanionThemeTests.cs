using System.Text.RegularExpressions;
using EQBuddy.Companion;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The phone follows the desktop's theme, custom ones included: the
/// #AARRGGBB → CSS conversion, the token set the page actually asks for, and the stamp
/// that decides whether a palette is re-sent at all.</summary>
public class CompanionThemeTests
{
    private static CompanionThemeSection Project(string theme = "ParchmentBrass") =>
        CompanionTheme.Project(theme, ThemePalettes.For(theme));

    /// <summary>THE PHONE HAS HAD NO THEME AT ALL, and the log said so 563 times on
    /// David's machine before anyone read it (2026-08-21).
    ///
    /// <c>ThemeManager.PaletteApplied</c> broadcasts the palette WITH the derived tones
    /// already concatenated — its own summary says "the derived tones included" — and
    /// Project derived them a second time and fed both into a ToDictionary. Duplicate key,
    /// ArgumentException, every single theme apply. CompanionHost.SetTheme catches so the
    /// app survives, which is exactly why it went unnoticed: nothing breaks, the phone just
    /// never receives a palette and renders in its own CSS defaults forever.
    ///
    /// A contract changed underneath a caller and the caller kept deriving. Project must
    /// therefore be correct for BOTH shapes — the explicit palette wins, and derivation
    /// only fills what is missing.</summary>
    [Fact]
    public void APaletteThatAlreadyCarriesItsDerivedTonesProjectsAnyway()
    {
        var palette = ThemePalettes.For("ParchmentBrass").ToList();
        // Exactly what PaletteApplied hands the companion host.
        var withTones = palette.Concat(ThemeTones.Derive(palette)).ToList();

        var fromPlain = CompanionTheme.Project("ParchmentBrass", palette);
        var fromBroadcast = CompanionTheme.Project("ParchmentBrass", withTones);

        // It used to throw here. And the two must agree: deriving twice cannot be allowed
        // to produce a different theme from deriving once.
        Assert.Equal(fromPlain.Colors, fromBroadcast.Colors);
    }

    /// <summary>An explicit value BEATS a derived one, which is the half that makes the
    /// fix a decision rather than a shrug: a palette that ships its own HairlineBrush is
    /// stating a preference, and derivation exists to fill gaps.</summary>
    [Fact]
    public void AnExplicitToneWinsOverTheDerivedOne()
    {
        var palette = ThemePalettes.For("ParchmentBrass").ToList();
        var stated = palette.Concat([("HairlineBrush", "#FF00FF00")]).ToList();

        var projected = CompanionTheme.Project("ParchmentBrass", stated);

        Assert.Equal(CompanionTheme.Web("#FF00FF00"), projected.Colors["hairline"]);
    }

    [Fact]
    public void OpaqueValuesBecomeHexAndTranslucentOnesKeepTheirAlpha()
    {
        Assert.Equal("#E3B341", CompanionTheme.Web("#FFE3B341"));
        Assert.Equal("rgba(255, 255, 255, 0.149)", CompanionTheme.Web("#26FFFFFF"));
        // The one exception: the page background has nothing behind it to show
        // through, so the widget's see-through alpha is dropped.
        Assert.Equal("#16130E", CompanionTheme.Web("#F216130E", opaque: true));
    }

    [Fact]
    public void ProjectsEveryTokenThePageAsksFor()
    {
        var colors = Project().Colors;
        // The page is the contract: every var(--x) it reads must arrive in the theme
        // message, or that rule silently falls back to a stale literal.
        var used = Regex.Matches(PhonePage.Html, @"var\(--([a-z0-9-]+)\)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Select(kebab => Regex.Replace(kebab, "-([a-z])", m => m.Groups[1].Value.ToUpperInvariant()))
            .ToList();

        Assert.NotEmpty(used);
        var missing = used.Where(t => !colors.ContainsKey(t)).ToList();
        Assert.True(missing.Count == 0,
            "The page reads CSS variables the theme projection never sends: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("ParchmentBrass")]
    [InlineData("BlueGrey")]
    [InlineData("Turquoise")]
    [InlineData("Redish")]
    [InlineData("Grey")]
    [InlineData("Solarized")]
    [InlineData("SolarizedDark")]
    [InlineData("HighContrast")]
    [InlineData("Custom")]
    public void EveryCatalogThemeProjectsTheWholeTokenSet(string theme)
    {
        var section = CompanionTheme.Project(theme, ThemePalettes.For(theme));
        Assert.Equal(theme, section.Key);
        foreach (var token in CompanionTheme.Tokens)
            Assert.True(section.Colors.ContainsKey(token), $"{theme} is missing the {token} token.");
        Assert.All(section.Colors.Values, v => Assert.Matches(@"^(#[0-9A-F]{6}|rgba\(.+\))$", v));
    }

    [Fact]
    public void CustomThemeDerivedTokensSurviveTheTrip()
    {
        // A user-picked palette: the accent must arrive verbatim, the derived text and
        // dim must arrive as the contrast pass left them, and the derived tones must
        // be the same ones WPF composes.
        var palette = CustomTheme.Derive("#101018", "#EFEFEF", "#FF3388").ToList();
        var section = CompanionTheme.Project(CustomTheme.Key, palette);

        Assert.Equal("#FF3388", section.Colors["accent"]);
        Assert.Equal("#101018", section.Colors["bg"]);          // alpha dropped, color kept
        Assert.Equal("#EFEFEF", section.Colors["text"]);
        Assert.NotEqual(section.Colors["text"], section.Colors["dim"]);
        // Hairline is the accent at 0x26 — the shared ThemeTones derivation, converted.
        Assert.Equal("rgba(255, 51, 136, 0.149)", section.Colors["hairline"]);
        Assert.Equal("rgba(255, 51, 136, 0.2)", section.Colors["accentWash"]);
        // The status trio stays fixed across custom palettes, as CustomTheme promises.
        Assert.Equal("#7FBF5F", section.Colors["good"]);
    }

    [Fact]
    public void StampMovesOnlyWhenAColorDoes()
    {
        Assert.Equal(Project().Stamp, Project().Stamp);
        Assert.NotEqual(Project().Stamp, Project("BlueGrey").Stamp);

        // Same theme key, one color changed by hand: the phone must still repaint.
        var tweaked = ThemePalettes.For("ParchmentBrass")
            .Select(e => e.Key == "AccentBrush" ? (e.Key, Hex: "#FF00FF00") : e);
        Assert.NotEqual(Project().Stamp, CompanionTheme.Project("ParchmentBrass", tweaked).Stamp);
    }

    [Fact]
    public void DerivedTonesMatchTheDesktopsOwn()
    {
        var palette = ThemePalettes.For("ParchmentBrass").ToList();
        var derived = ThemeTones.Derive(palette).ToDictionary(e => e.Key, e => e.Hex);

        Assert.Equal(ThemeTones.Keys.ToList(), derived.Keys.ToList());
        Assert.Equal("#26E3B341", derived["HairlineBrush"]);   // accent at a whisper
        Assert.Equal("#1EE3B341", derived["TrackBrush"]);
        Assert.Equal("#39FFFFFF", derived["RaisedBrush"]);     // panel one step up
        Assert.Equal("#FF886B27", derived["AccentDeepBrush"]);
    }

    [Fact]
    public void ParsesBothHexShapes()
    {
        Assert.Equal(((byte)0xFF, (byte)0x12, (byte)0x34, (byte)0x56), ThemeTones.Parse("#123456"));
        Assert.Equal(((byte)0x80, (byte)0x12, (byte)0x34, (byte)0x56), ThemeTones.Parse("#80123456"));
    }
}
