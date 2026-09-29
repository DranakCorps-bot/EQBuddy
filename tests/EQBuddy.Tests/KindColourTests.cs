using System.Text.RegularExpressions;
using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **A type's colour is LOCKED, and the player may change it** (David, 2026-09-29: "make sure
/// the colors stay consistent for type so if they're not locked, please lock them" and "let
/// people color code the types to whichever color they want from a color wheel").
///
/// Three regions: the LOCK (the default sets pinned as literals, by kind — a committed copy
/// here, never a read of <see cref="ThemeTones"/>' own arrays, which an edit would carry along;
/// the phone page's fallbacks; kind-keyed, never positional), the PLAYER'S PICKS
/// (<see cref="KindColours"/> through the one palette producer, every theme, the desktop and
/// the phone's first frame alike), and the WHEEL's arithmetic (<see cref="ColourWheelMath"/>).
/// </summary>
[Collection(SettingsFileCollection.Name)]
public class KindColourTests
{
    // ---- the lock --------------------------------------------------------------------------

    /// <summary>The DARK defaults — every theme but Solarized. The Founder-approved mockup, with
    /// Proc and Other lifted to clear 3:1 (DECISIONS.md, 2026-09-29).</summary>
    private static readonly Dictionary<OutputKind, string> DarkDefaults = new()
    {
        [OutputKind.Melee] = "#FFD9A441",
        [OutputKind.Skill] = "#FFE8743B",
        [OutputKind.Ranged] = "#FFC9B98A",
        [OutputKind.Spell] = "#FF5AA9E6",
        [OutputKind.DoT] = "#FF9B86D6",
        [OutputKind.DamageShield] = "#FF3FBFAE",
        [OutputKind.Proc] = "#FFE36F9F",
        [OutputKind.Pet] = "#FF6BBF59",
        [OutputKind.Heal] = "#FF4CC38A",
        [OutputKind.HoT] = "#FF46B3D6",
        [OutputKind.Other] = "#FF959AA3",
    };

    /// <summary>The LIGHT defaults — Solarized, the one light theme: darker shades of the same
    /// hues, so a type reads as the same colour in both.</summary>
    private static readonly Dictionary<OutputKind, string> LightDefaults = new()
    {
        [OutputKind.Melee] = "#FF9A6F12",
        [OutputKind.Skill] = "#FFB8521C",
        [OutputKind.Ranged] = "#FF7D6C3A",
        [OutputKind.Spell] = "#FF1F6FB2",
        [OutputKind.DoT] = "#FF6552B0",
        [OutputKind.DamageShield] = "#FF16877A",
        [OutputKind.Proc] = "#FFB3386E",
        [OutputKind.Pet] = "#FF3F8A34",
        [OutputKind.Heal] = "#FF237D52",
        [OutputKind.HoT] = "#FF1B7C96",
        [OutputKind.Other] = "#FF6B7075",
    };

    public static TheoryData<string> Themes()
    {
        var data = new TheoryData<string>();
        foreach (var t in ThemePalettes.DefinedThemes) data.Add(t);
        return data;
    }

    private static Dictionary<string, string> Derived(IEnumerable<(string Key, string Hex)> palette) =>
        ThemeTones.Derive(palette).ToDictionary(e => e.Key, e => e.Hex);

    /// <summary>
    /// **Every theme paints every type in its LOCKED colour**, looked up by the kind's KEY.
    /// An edit to either default set — or a reorder of the kinds that shifted which colour a
    /// kind's index lands on — changes a value here, and this copy does not move with it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void EveryThemePaintsEveryTypeInItsLockedColour(string theme)
    {
        var derived = Derived(ThemePalettes.For(theme));
        var expected = theme == "Solarized" ? LightDefaults : DarkDefaults;
        Assert.Equal(Enum.GetValues<OutputKind>().Length, expected.Count);
        foreach (var (kind, hex) in expected)
            Assert.Equal(hex, derived[OutputKindPresentation.BrushKey(kind)]);
    }

    /// <summary>The phone draws in the page's CSS fallbacks until the first theme frame lands,
    /// so those are locked to the dark defaults too — or a phone would flash a different
    /// colour for a type on every connect.</summary>
    [Fact]
    public void ThePhonePagesFallbacksAreTheLockedDarkSet()
    {
        var html = File.ReadAllText(Path.Combine(SrcRoot(), "EQBuddy.Companion", "Web", "index.html"));
        foreach (var (kind, hex) in DarkDefaults)
        {
            var css = Regex.Replace(OutputKindPresentation.Token(kind), "[A-Z]", m => "-" + m.Value.ToLowerInvariant());
            var m = Regex.Match(html, $@"--{css}:(#[0-9a-fA-F]{{6}});");
            Assert.True(m.Success, $"index.html has no --{css} fallback");
            Assert.Equal("#" + hex[3..], m.Groups[1].Value.ToUpperInvariant());
        }
    }

    /// <summary>
    /// **A row's colour follows its TYPE, never its place.** The same rows in the opposite
    /// damage order keep their kind tokens (the phone's colour key), and the mix's segments
    /// keep theirs — so the Melee row is the Melee colour whether it is first or last.
    /// </summary>
    [Fact]
    public void ARowsColourFollowsItsTypeNotItsPosition()
    {
        SourceDamage Row(string name, long total, OutputKind kind) => new(name, 1, total) { Kind = kind };
        IReadOnlyList<CompanionAbilityRow> Rows(params SourceDamage[] rows) =>
            CompanionProjection.Build(new CompanionInputs
            {
                Stats = new StatsSnapshot { CombatSeconds = 60, DamageBySource = [.. rows] },
                Character = "Tumog", AppVersion = "2.0.1", Offered = CompanionSurfaces.All,
            }, new DateTime(2026, 9, 29, 20, 0, 0)).Combat!.Boards[0].Session;

        // The snapshot hands rows over already in damage order, so "the other order" is the
        // other damage ranking, handed over as such.
        var meleeFirst = Rows(Row("Crush", 900, OutputKind.Melee), Row("Stinging Swarm (DoT)", 100, OutputKind.DoT));
        var meleeLast = Rows(Row("Stinging Swarm (DoT)", 900, OutputKind.DoT), Row("Crush", 100, OutputKind.Melee));
        Assert.Equal(["Crush", "Stinging Swarm (DoT)"], meleeFirst.Select(r => r.Name));
        Assert.Equal(["Stinging Swarm (DoT)", "Crush"], meleeLast.Select(r => r.Name));
        foreach (var rows in new[] { meleeFirst, meleeLast })
        {
            Assert.Equal("kindMelee", rows.Single(r => r.Name == "Crush").Kind);
            Assert.Equal("kindDot", rows.Single(r => r.Name == "Stinging Swarm (DoT)").Kind);
        }
    }

    // ---- the player's picks ----------------------------------------------------------------

    private static AppSettings With(string theme, params (string Kind, string Hex)[] picks)
    {
        var s = new AppSettings { Theme = theme };
        foreach (var (kind, hex) in picks) s.KindColours[kind] = hex;
        return s;
    }

    /// <summary>A pick applies to that type in EVERY theme — it overrides the dark default and
    /// the light one alike — and leaves every other type on its locked default.</summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void APickOverridesThatTypeInEveryThemeAndNothingElse(string theme)
    {
        var derived = Derived(CustomTheme.PaletteFor(With(theme, ("Melee", "#ff00aa"))));
        Assert.Equal("#FFFF00AA", derived["KindMeleeBrush"]);
        var defaults = theme == "Solarized" ? LightDefaults : DarkDefaults;
        foreach (var (kind, hex) in defaults.Where(d => d.Key != OutputKind.Melee))
            Assert.Equal(hex, derived[OutputKindPresentation.BrushKey(kind)]);
    }

    /// <summary>An unreadable value is ignored — the type keeps its default — and never
    /// thrown on; a name that is no kind adds nothing; names match case-insensitively (a
    /// deserialized dictionary loses its comparer).</summary>
    [Fact]
    public void AnInvalidPickIsIgnoredAndNeverThrows()
    {
        var derived = Derived(CustomTheme.PaletteFor(With("BlueGrey",
            ("Melee", "not a colour"), ("Spell", "#12"), ("Nonsense", "#FF0000"), ("dot", "#00ff00"))));
        Assert.Equal(DarkDefaults[OutputKind.Melee], derived["KindMeleeBrush"]);
        Assert.Equal(DarkDefaults[OutputKind.Spell], derived["KindSpellBrush"]);
        Assert.Equal("#FF00FF00", derived["KindDoTBrush"]);
        Assert.Empty(KindColours.PaletteRows(null));
        // No picks: the palette is exactly the theme's, row for row.
        Assert.Equal(ThemePalettes.For("BlueGrey"), CustomTheme.PaletteFor(new AppSettings { Theme = "BlueGrey" }));
    }

    /// <summary>The store: Set replaces the dictionary (the DeadSettingTests-visible write),
    /// null clears one, an invalid hex writes nothing, ResetAll clears all, and Effective
    /// answers the colour in force through the same derivation.</summary>
    [Fact]
    public void TheStoreSetsClearsAndResets()
    {
        var s = new AppSettings { Theme = "Turquoise" };
        KindColours.Set(s, OutputKind.DoT, "#abcdef");
        Assert.True(KindColours.IsPicked(s, OutputKind.DoT));
        Assert.Equal("#ABCDEF", KindColours.Effective(s, OutputKind.DoT));
        KindColours.Set(s, OutputKind.DoT, "garbage");
        Assert.Equal("#ABCDEF", KindColours.Effective(s, OutputKind.DoT));
        KindColours.Set(s, OutputKind.Pet, "#010203");
        KindColours.Set(s, OutputKind.DoT, null);
        Assert.False(KindColours.IsPicked(s, OutputKind.DoT));
        Assert.Equal("#" + DarkDefaults[OutputKind.DoT][3..], KindColours.Effective(s, OutputKind.DoT));
        Assert.True(KindColours.IsPicked(s, OutputKind.Pet));
        KindColours.ResetAll(s);
        Assert.Empty(s.KindColours);
    }

    /// <summary>
    /// **The phone gets a pick on its FIRST frame, not only on the next broadcast.** The host
    /// builds its first theme from settings in its constructor, before ThemeManager has raised
    /// anything; the pick rides the palette, so both paths carry it.
    /// </summary>
    [Fact]
    public void ThePhonesFirstFrameAndEveryBroadcastCarryThePick()
    {
        var settings = With("Solarized", ("HoT", "#123456"));
        settings.CompanionEnabled = false;
        using var host = new CompanionHost(settings, "test");
        Assert.Equal("#123456", host.Theme!.Colors["kindHot"]);

        // The broadcast path: ThemeManager hands PaletteApplied the palette PLUS its derived
        // tones (duplicates included) — the pick survives that too.
        var palette = CustomTheme.PaletteFor(settings).ToList();
        var broadcast = palette.Concat(ThemeTones.Derive(palette)).ToList();
        Assert.Equal("#123456", CompanionTheme.Project("Solarized", broadcast).Colors["kindHot"]);
        Assert.Equal("#" + LightDefaults[OutputKind.Heal][3..], CompanionTheme.Project("Solarized", broadcast).Colors["kindHeal"]);
    }

    // ---- the wheel's arithmetic ------------------------------------------------------------

    /// <summary>hex → HSV → a point on the disc → back → hex, within one step per channel,
    /// for colours spread over the whole wheel (and the defaults, which a player starts from).</summary>
    [Theory]
    [InlineData("#FF0000")] [InlineData("#00FF00")] [InlineData("#0000FF")] [InlineData("#D9A441")]
    [InlineData("#9B86D6")] [InlineData("#3FBFAE")] [InlineData("#6B7075")] [InlineData("#123456")]
    [InlineData("#FFFFFF")] [InlineData("#808080")] [InlineData("#E36F9F")] [InlineData("#1B7C96")]
    public void AColourSurvivesTheRoundTripThroughTheDisc(string hex)
    {
        const double radius = 84;
        var hsv = ColourWheelMath.FromHex(hex)!.Value;
        var (x, y) = ColourWheelMath.PointFor(hsv.H, hsv.S, radius);
        var (h, s) = ColourWheelMath.FromPoint(x, y, radius);
        var back = ColourWheelMath.ToHex(new Hsv(h, s, hsv.V));
        var (r0, g0, b0) = Channels(hex);
        var (r1, g1, b1) = Channels(back);
        Assert.True(Math.Abs(r0 - r1) <= 1 && Math.Abs(g0 - g1) <= 1 && Math.Abs(b0 - b1) <= 1,
            $"{hex} came back as {back}");
    }

    [Fact]
    public void TheCentreIsGreyAndTheRimAtZeroDegreesIsRed()
    {
        const double radius = 84;
        Assert.Equal("#FFFFFF", ColourWheelMath.ToHex(new Hsv(ColourWheelMath.FromPoint(0, 0, radius).Hue, 0, 1)));
        var (h0, s0) = ColourWheelMath.FromPoint(0, 0, radius);
        Assert.Equal(0, s0);
        Assert.Equal("#808080", ColourWheelMath.ToHex(new Hsv(h0, s0, 128 / 255.0)));
        Assert.Equal((byte)255, ColourWheelMath.DiscPixel(0, 0, radius)!.Value.R);   // white centre
        Assert.Equal((byte)255, ColourWheelMath.DiscPixel(0, 0, radius)!.Value.B);

        var (hr, sr) = ColourWheelMath.FromPoint(radius, 0, radius);
        Assert.Equal("#FF0000", ColourWheelMath.ToHex(new Hsv(hr, sr, 1)));
        // …counter-clockwise AS THE EYE SEES IT: 120° is up-and-left, and it is green.
        var (gx, gy) = ColourWheelMath.PointFor(120, 1, radius);
        Assert.True(gx < 0 && gy < 0);
        Assert.Equal("#00FF00", ColourWheelMath.ToHex(new Hsv(120, 1, 1)));
        // A drag past the rim clamps to it rather than stopping the hue.
        Assert.Equal(1, ColourWheelMath.FromPoint(radius * 3, 0, radius).Saturation);
        Assert.Null(ColourWheelMath.DiscPixel(radius + 1, 0, radius));
        Assert.Null(ColourWheelMath.FromHex("#12"));
    }

    private static (int R, int G, int B) Channels(string hex) =>
        (Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));

    private static string SrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "EQBuddy.Companion")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src");
    }
}
