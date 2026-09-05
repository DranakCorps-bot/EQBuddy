using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The design system is data, and — exactly like <see cref="ThemePalettes"/> — neither UI
/// can report a problem with it at runtime: a missing size resolves to a default nobody
/// chose, and a malformed path throws deep inside a window that never runs in CI. This is
/// where that gets caught, and where the Gate 1 promises (docs/DesignSystem.md §2–§4) are
/// held to: one scale, no duplicate glyph for one meaning, no icon that can't be drawn.
/// </summary>
public class DesignSystemTests
{
    // ---- tokens ----

    [Fact]
    public void EveryTypeRoleHasASpec() =>
        Assert.Equal(
            Enum.GetValues<DesignTokens.TypeRole>().OrderBy(r => r),
            DesignTokens.Type.Keys.OrderBy(r => r));

    /// <summary>Every role's ink is a real palette key. A role naming a key no theme
    /// defines paints an invisible control in all eight themes at once.</summary>
    [Fact]
    public void EveryTypeRoleInkIsAPaletteKey()
    {
        foreach (var (role, spec) in DesignTokens.Type)
            Assert.True(ThemePalettes.Keys.Contains(spec.ColorKey),
                $"{role} inks with '{spec.ColorKey}', which is not a ThemePalettes key.");
    }

    /// <summary>The half-point sizes (9.5, 10.5, 11.5, 12.5) are the audit's tell — a size
    /// nudged to make one row fit rather than chosen. 11.5 and 12.5 survive as named roles;
    /// what must not come back is a size below the readable floor. 7pt was in the old set
    /// and does not survive the migration (§2.2).</summary>
    [Fact]
    public void NoRoleIsBelowTheReadableFloor()
    {
        foreach (var (role, spec) in DesignTokens.Type)
            Assert.True(spec.Size >= 10, $"{role} is {spec.Size}pt — below the 10pt floor.");
    }

    /// <summary>Seven roles replacing thirteen sizes only helps if they stay seven. A new
    /// role is a design decision; this makes it a reviewed one.</summary>
    [Fact]
    public void TheTypeScaleIsSevenRoles() => Assert.Equal(7, DesignTokens.Type.Count);

    /// <summary>Every font role has a matching numeric token under the FontKey convention,
    /// with the same value — that pair is how a UI composes "FontBody" as a resource and
    /// still gets what TypeRole.Body says.</summary>
    [Fact]
    public void EveryTypeRoleHasAMatchingNumericToken()
    {
        foreach (var (role, spec) in DesignTokens.Type)
        {
            var key = DesignTokens.FontKey(role);
            Assert.True(DesignTokens.Numbers.TryGetValue(key, out var value),
                $"{role} has no '{key}' entry in DesignTokens.Numbers.");
            Assert.Equal(spec.Size, value);
        }
    }

    [Fact]
    public void SpacingIsAMonotonicScale()
    {
        double[] scale =
        [
            DesignTokens.SpaceXxs, DesignTokens.SpaceXs, DesignTokens.SpaceS,
            DesignTokens.SpaceM, DesignTokens.SpaceL, DesignTokens.SpaceXl,
        ];
        Assert.Equal(scale.OrderBy(v => v), scale);
        Assert.Equal(scale.Length, scale.Distinct().Count());
    }

    /// <summary>Nothing in the table may be zero or negative: both UIs feed these
    /// straight into Thickness/CornerRadius, where a negative silently mis-lays out
    /// rather than throwing.</summary>
    [Fact]
    public void EveryNumericTokenIsPositive()
    {
        foreach (var (key, value) in DesignTokens.Numbers)
            Assert.True(value > 0, $"{key} = {value}");
    }

    // ---- icons ----

    /// <summary>The cheap structural half, which runs everywhere: a move-to first, and
    /// nothing in the string but path grammar — a stray letter from a copy-paste, or a
    /// unicode minus out of a design tool, lands here.
    ///
    /// The REAL check is <c>IconGeometryTests</c>, which parses every path with an actual
    /// geometry parser and measures it against the 24×24 grid. That one cannot live here:
    /// UI.Shared and its test project are deliberately toolkit-free (ArchitectureTests),
    /// so there is no parser to call. It lived in EQBuddy.Avalonia.Tests until 2026-09-04
    /// and is now in <c>tests/EQBuddy.E2E</c>, where it runs through WPF's parser — the
    /// one the shipping app hands these strings to.</summary>
    [Theory]
    [MemberData(nameof(IconNames))]
    public void EveryIconPathIsWellFormed(string name)
    {
        var data = IconPaths.Path(name);
        Assert.False(string.IsNullOrWhiteSpace(data), $"{name} has no path data.");
        Assert.True(data[0] is 'M' or 'm', $"{name} does not start with a move: '{data[..1]}'");

        const string commands = "MmLlHhVvCcSsQqTtAaZz";
        foreach (var c in data)
            Assert.True(char.IsAsciiDigit(c) || c is ' ' or ',' or '.' or '-' || commands.Contains(c),
                $"{name}: '{c}' is not path grammar.");
    }

    /// <summary>The audit's headline defect was the same concept wearing two glyphs —
    /// done was ✓ ×62 AND ✔ ×15, refresh ⟳ ×22 AND ↻ ×4. Identical path data under two
    /// names is that bug reappearing in the replacement.</summary>
    [Fact]
    public void NoTwoIconsShareGeometry()
    {
        var dupes = IconPaths.All
            .GroupBy(e => e.Value, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" = ", g.Select(e => e.Key)))
            .ToList();
        Assert.True(dupes.Count == 0,
            "Two names, one shape — pick one: " + string.Join("; ", dupes));
    }

    [Fact]
    public void AnUnknownIconThrowsRatherThanDrawingNothing() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => IconPaths.Path("NoSuchIcon"));

    /// <summary>Every silhouette <see cref="IconPaths.ForItem"/> can name has to exist.
    /// This is the pairing that a lookup table gets wrong silently.</summary>
    [Fact]
    public void EverySilhouetteTheMapperCanNameExists()
    {
        string[] slots =
        [
            "PRIMARY", "SECONDARY", "SECONDAY", "HEAD", "FACE", "CHEST", "ARMS",
            "SHOULDER", "SHOULDERS", "LEGS", "HANDS", "WRIST", "FEET", "WAIST",
            "NECK", "FINGER", "FINGERS", "EAR", "BACK", "BACK,", "RANGE", "AMMO",
            "", "nonsense",
        ];
        string[] skills =
        [
            "", "1H Slashing", "2H Blunt", "Piercing", "Archery", "Throwingv2",
            "Hand to Hand", "SHIELD", "Evocation",
        ];
        foreach (var slot in slots)
            foreach (var skill in skills)
                Assert.False(string.IsNullOrEmpty(IconPaths.Path(IconPaths.ForItem([slot], skill))));
    }

    /// <summary>The harvested catalog carries real dirt — mixed case, a stray trailing
    /// comma, one "SECONDAY". Letters-only matching is what absorbs it; these are the
    /// actual strings in the shipped table, not invented ones.</summary>
    [Theory]
    [InlineData("PRIMARY", "", "SlotWeapon")]
    [InlineData("Primary", "", "SlotWeapon")]
    [InlineData("SECONDARY", "", "SlotShield")]
    [InlineData("SECONDAY", "", "SlotShield")]
    [InlineData("BACK,", "", "SlotBack")]
    [InlineData("FINGERS", "", "SlotRing")]
    [InlineData("SHOULDER", "", "SlotBody")]
    [InlineData("EAR", "", "SlotEar")]
    public void DirtyCatalogSlotsStillLandOnASilhouette(string slot, string skill, string expected) =>
        Assert.Equal(expected, IconPaths.ForItem([slot], skill));

    /// <summary>The weapon SKILL beats the slot: "PRIMARY, 2H Blunt" is a hammer, and a
    /// sword drawn for it is exactly the confidently-wrong art §8a rules out.</summary>
    [Theory]
    [InlineData("2H Blunt", "SlotBlunt")]
    [InlineData("1H Blunt", "SlotBlunt")]
    [InlineData("Archery", "SlotRanged")]
    [InlineData("Throwingv1", "SlotRanged")]
    [InlineData("1H Slashing", "SlotWeapon")]
    [InlineData("Piercing", "SlotWeapon")]
    public void WeaponSkillOutranksThePrimarySlot(string skill, string expected) =>
        Assert.Equal(expected, IconPaths.ForItem(["PRIMARY"], skill));

    /// <summary>Anything the catalog doesn't place gets the crate, which claims nothing.
    /// Quest turn-ins are mostly unequippable, so this is the COMMON path, not the edge.</summary>
    [Fact]
    public void UnknownItemsGetTheNeutralSilhouette()
    {
        Assert.Equal("SlotItem", IconPaths.ForItem(null, null));
        Assert.Equal("SlotItem", IconPaths.ForItem([], ""));
        Assert.Equal("SlotItem", IconPaths.ForItem(["Ornamentation:"], "Alteration"));
    }

    /// <summary>The mapper has to hold against the SHIPPED catalog, not a sample: 11k
    /// records harvested from the wiki, every one of which reaches a real glyph.</summary>
    [Fact]
    public void EveryShippedItemResolvesToARealSilhouette()
    {
        var catalog = ItemCatalog.Default;
        Assert.True(catalog.Count > 1000, $"Item catalog looks empty ({catalog.Count}).");
        foreach (var record in catalog.All)
        {
            var icon = IconPaths.ForItem(record.Slots, record.Skill);
            Assert.True(IconPaths.All.ContainsKey(icon),
                $"'{record.Name}' (slots {string.Join('/', record.Slots)}, skill '{record.Skill}') " +
                $"maps to '{icon}', which is not an icon.");
        }
    }

    // ---- the chip (gate 2b) ----

    /// <summary>Both UIs paint a chip by looking these names up. A key no theme defines
    /// paints an invisible pill in all eight at once — and the chip is the app's most
    /// rebuilt shape, so it would be invisible in a lot of places.</summary>
    [Fact]
    public void EveryChipInkIsAPaletteKey()
    {
        foreach (var selected in new[] { true, false })
        {
            var ink = ChipStyle.For(selected);
            foreach (var key in new[] { ink.Background, ink.Border, ink.Label, ink.Badge })
                Assert.True(ThemePalettes.Keys.Contains(key) || ThemeTones.Keys.Contains(key),
                    $"selected={selected}: '{key}' is neither a palette key nor a derived tone.");
        }
    }

    /// <summary>Selected has to be legible AS selected. If the fill and the ink both
    /// matched the unselected state the strip would still work and still look broken —
    /// which is the complaint that started this ("I couldn't tell they were tabs at first
    /// glance", David, 2026-08-15).</summary>
    [Fact]
    public void SelectedAndUnselectedChipsDifferInFillAndInk()
    {
        var on = ChipStyle.For(true);
        var off = ChipStyle.For(false);
        Assert.NotEqual(on.Background, off.Background);
        Assert.NotEqual(on.Label, off.Label);
    }

    /// <summary>Only the SELECTED chip may carry the accent — §2.1's discipline is the
    /// one rule of the whole colour system, and a strip is where it is easiest to break.</summary>
    [Fact]
    public void OnlyTheSelectedChipWearsTheAccent()
    {
        Assert.Equal("AccentBrush", ChipStyle.For(true).Background);
        var off = ChipStyle.For(false);
        Assert.DoesNotContain("Accent", off.Background, StringComparison.Ordinal);
        Assert.DoesNotContain("Accent", off.Label, StringComparison.Ordinal);
    }

    /// <summary>Unselected is not "nothing": it keeps a fill and an edge, so the row reads
    /// as a set of controls rather than a line of prose.</summary>
    [Fact]
    public void UnselectedChipsStillLookLikeControls()
    {
        var off = ChipStyle.For(false);
        Assert.Equal("RaisedBrush", off.Background);
        Assert.Equal("HairlineBrush", off.Border);
        Assert.True(ChipStyle.BorderThickness > 0);
    }

    [Fact]
    public void ChipGeometryComesFromTheTokenScale()
    {
        Assert.Equal(DesignTokens.RadiusPill, ChipStyle.Radius);
        foreach (var v in new[] { ChipStyle.Padding.Left, ChipStyle.Padding.Top,
                     ChipStyle.Padding.Right, ChipStyle.Padding.Bottom,
                     ChipStyle.Gap.Right, ChipStyle.Gap.Bottom })
            Assert.Contains(v, DesignTokens.Numbers.Values);
    }

    /// <summary>Every card on the widget has an icon, and every icon it names is real.
    /// The fourteen emoji these replace were the last big block of glyphs in the app and
    /// sat on the surface that is always on screen (Gate 5).</summary>
    [Fact]
    public void EveryWidgetCardHasARealIcon()
    {
        foreach (var (key, title) in OverlaySections.Catalog)
        {
            var icon = OverlaySections.Icon(key);
            Assert.True(IconPaths.Names.Contains(icon),
                $"card '{key}' ({title}) names icon '{icon}', which IconPaths does not have.");
        }
    }

    /// <summary>An unmapped card falls back rather than throwing: a new card should look
    /// plain, not take the widget down.</summary>
    [Fact]
    public void AnUnknownCardFallsBackToARealIcon() =>
        Assert.True(IconPaths.Names.Contains(OverlaySections.Icon("nosuchcard")));

    // ---- the capture path ----

    /// <summary>Only the window GROUND goes opaque. Flattening a tint would repaint the
    /// app rather than photograph it — PanelBrush at full alpha is pure white.</summary>
    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void CaptureModeMakesTheGroundOpaqueAndTouchesNothingElse(string theme)
    {
        var original = ThemePalettes.For(theme).ToList();
        var opaque = CaptureTheme.Opaque(original).ToList();

        Assert.Equal(original.Select(e => e.Key), opaque.Select(e => e.Key));
        foreach (var (key, hex) in opaque)
        {
            var was = original.First(e => e.Key == key).Hex;
            if (CaptureTheme.GroundKeys.Contains(key))
            {
                Assert.Equal(0xFF, ThemeTones.Parse(hex).A);
                // Same colour, only the alpha replaced: the theme's own value at full
                // strength, never a re-mix.
                var (_, r, g, b) = ThemeTones.Parse(was);
                Assert.Equal((r, g, b), (ThemeTones.Parse(hex).R, ThemeTones.Parse(hex).G,
                    ThemeTones.Parse(hex).B));
            }
            else
            {
                Assert.Equal(was, hex);
            }
        }
    }

    /// <summary>BgBrush is the one shipped ground that actually carries alpha, and it
    /// carries it in every theme. If that ever stops being true the capture path is
    /// solving a problem that moved.</summary>
    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void EveryThemesBackgroundIsTranslucentUntilCaptureMode(string theme)
    {
        var bg = ThemePalettes.For(theme).First(e => e.Key == "BgBrush").Hex;
        Assert.True(ThemeTones.Parse(bg).A < 0xFF,
            $"{theme}'s BgBrush is already opaque — the widget stopped showing the game through.");
    }

    /// <summary>Off by default, always. Nothing but a deliberate scripts/shoot.ps1 run may
    /// change what a player sees.</summary>
    [Fact]
    public void CaptureModeIsOffUnlessTheEnvironmentAsksForIt()
    {
        Assert.Null(Environment.GetEnvironmentVariable(CaptureTheme.EnvVar));
        Assert.False(CaptureTheme.Enabled);
        var palette = ThemePalettes.For("ParchmentBrass").ToList();
        Assert.Equal(palette, CaptureTheme.IfEnabled(palette));
    }

    public static TheoryData<string> ThemeKeys()
    {
        var data = new TheoryData<string>();
        foreach (var (key, _) in ThemeCatalog.Themes) data.Add(key);
        return data;
    }

    public static TheoryData<string> IconNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in IconPaths.Names) data.Add(name);
        return data;
    }
}
