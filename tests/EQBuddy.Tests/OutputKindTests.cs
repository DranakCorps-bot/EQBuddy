using System.Text.RegularExpressions;
using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **DPS/HPS by type** (Founder's option A, 2026-09-29): every meter row carries its
/// <see cref="OutputKind"/>, drawn as a colour square + a coloured bar, with a mix strip and a
/// legend at the top of the meter — on every desktop meter and on the phone.
///
/// Four layers, each its own region: the RULES (<see cref="OutputKinds"/> — a verb table, a
/// proc demoted by your own cast, the DoT/HoT row split), the ROWS a real log produces (the
/// shared fixture: crush, kick, a DoT, thorns, a pet), the MIX and its words
/// (<see cref="OutputKindPresentation"/>), and the COLOURS (<see cref="ThemeTones"/>' derived
/// kind keys, their contrast on every shipped theme, and the phone's copy of them).
/// </summary>
public class OutputKindTests
{
    private static string At(int mm, int ss, string msg) => $"[Thu Jul 30 10:{mm:D2}:{ss:D2} 2026] {msg}";

    private static StatsSnapshot Replay(params string[] lines)
    {
        var stats = new SessionStats { CharacterName = "Hugzee" };
        foreach (var line in lines)
            if (LogParser.Parse(line) is { } evt) stats.Apply(evt);
        return stats.Snapshot();
    }

    private static string FixturePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "eqlog_Testchar_fixture.txt"));

    // ---- the rules ------------------------------------------------------------------------

    /// <summary>The log writes "You kick X" exactly as it writes "You crush X", so the verb
    /// table is the only tell — and "strike"/"punch" stay Melee on purpose, because they are
    /// auto-attack verbs for some classes too.</summary>
    [Theory]
    [InlineData("Crush", OutputKind.Melee)]
    [InlineData("Slash", OutputKind.Melee)]
    [InlineData("Strike", OutputKind.Melee)]
    [InlineData("Punch", OutputKind.Melee)]
    [InlineData("Kick", OutputKind.Skill)]
    [InlineData("Round Kick", OutputKind.Skill)]
    [InlineData("backstab", OutputKind.Skill)]
    [InlineData("Bash", OutputKind.Skill)]
    [InlineData("Archery", OutputKind.Ranged)]
    [InlineData("Throwing", OutputKind.Ranged)]
    public void AMeleeRowsKindComesFromTheVerbTables(string skill, OutputKind expected) =>
        Assert.Equal(expected, OutputKinds.ForMelee(skill));

    /// <summary>A DoT tick is its own row, "&lt;Spell&gt; (DoT)", beside the spell's direct
    /// hits — a spell that both hits and ticks used to be one row that was neither kind.</summary>
    [Fact]
    public void ADotTickIsItsOwnRowBesideTheDirectHits()
    {
        var s = Replay(
            At(0, 0, "You begin casting Stinging Swarm."),
            At(0, 3, "A puma has taken 32 damage from your Stinging Swarm."),
            At(0, 9, "A puma has taken 12 damage from your Stinging Swarm."),
            At(0, 10, "You begin casting Burst of Flame."),
            At(0, 12, "You hit a puma for 40 points of fire damage by Burst of Flame."));

        var dot = Assert.Single(s.DamageBySource, d => d.Name == "Stinging Swarm (DoT)");
        Assert.Equal(OutputKind.DoT, dot.Kind);
        Assert.Equal(44, dot.Total);
        Assert.DoesNotContain(s.DamageBySource, d => d.Name == "Stinging Swarm");
        Assert.Equal(OutputKind.Spell, Assert.Single(s.DamageBySource, d => d.Name == "Burst of Flame").Kind);
    }

    /// <summary>The heal side's split: a HoT tick is "&lt;Spell&gt; (HoT)" and a direct heal
    /// keeps the spell's own name — Budding Heal both lands and ticks.</summary>
    [Fact]
    public void AHotTickIsItsOwnRowAndADirectHealKeepsItsName()
    {
        var s = Replay(
            At(0, 0, "You healed Spamwagon for 60 hit points by Budding Heal."),
            At(0, 6, "You healed Spamwagon over time for 11 hit points by Budding Heal."),
            At(0, 12, "You healed Spamwagon over time for 11 hit points by Budding Heal."));

        var direct = Assert.Single(s.HealsBySpell, h => h.Name == "Budding Heal");
        Assert.Equal(OutputKind.Heal, direct.Kind);
        Assert.Equal(60, direct.Total);
        var hot = Assert.Single(s.HealsBySpell, h => h.Name == "Budding Heal (HoT)");
        Assert.Equal(OutputKind.HoT, hot.Kind);
        Assert.Equal(22, hot.Total);
    }

    /// <summary>Spell damage with no cast of that spell before it is a proc — the heuristic
    /// SessionStats already runs for the Procs list — and the row stays a Proc only while
    /// nothing contradicts it: the first hit that follows your own cast makes it a Spell.</summary>
    [Fact]
    public void AProcRowIsDemotedToASpellByYourOwnCast()
    {
        var uncast = Replay(
            At(0, 0, "You hit a froglok tad for 64 points of magic damage by Harm Touch."));
        Assert.Equal(OutputKind.Proc, Assert.Single(uncast.DamageBySource).Kind);

        var thenCast = Replay(
            At(0, 0, "You hit a froglok tad for 64 points of magic damage by Harm Touch."),
            At(1, 0, "You begin casting Harm Touch."),
            At(1, 1, "You hit a froglok tad for 640 points of magic damage by Harm Touch."));
        Assert.Equal(OutputKind.Spell, Assert.Single(thenCast.DamageBySource).Kind);
    }

    /// <summary>The demotion runs ONE way: a row your own cast already made a Spell is not
    /// turned into a Proc by a later hit with no cast before it (a recast out of the window).
    /// The first hit decides; the only later change is Proc → Spell.</summary>
    [Fact]
    public void ACastSpellRowIsNeverPromotedToAProc()
    {
        var s = Replay(
            At(0, 0, "You begin casting Harm Touch."),
            At(0, 1, "You hit a froglok tad for 640 points of magic damage by Harm Touch."),
            At(30, 0, "You hit a froglok tad for 64 points of magic damage by Harm Touch."));
        Assert.Equal(OutputKind.Spell, Assert.Single(s.DamageBySource).Kind);

        Assert.Equal(OutputKind.Spell, OutputKinds.Merge(OutputKind.Spell, OutputKind.Proc));
        Assert.Equal(OutputKind.Spell, OutputKinds.Merge(OutputKind.Proc, OutputKind.Spell));
        Assert.Equal(OutputKind.Melee, OutputKinds.Merge(OutputKind.Other, OutputKind.Melee));
    }

    // ---- the rows a real log produces ----------------------------------------------------

    /// <summary>
    /// The shared fixture, replayed whole, predicted before it ran (trap 23): its crush rows
    /// are Melee, its kick is a Skill, its archery Ranged, its Stinging Swarm ticks a DoT row
    /// of their own, its thorns the Damage shield, and every warder/charm row Pet. **No row is
    /// left Other** — a live session classifies everything it meters.
    /// </summary>
    [Fact]
    public void TheFixtureLogsMeterRowsEachCarryTheirKind()
    {
        var stats = new SessionStats { CharacterName = "Tumog" };
        foreach (var line in File.ReadLines(FixturePath))
            if (LogParser.Parse(line) is { } evt) stats.Apply(evt);
        var s = stats.Snapshot();
        OutputKind KindOf(string name) => Assert.Single(s.DamageBySource, d => d.Name == name).Kind;

        Assert.Equal(OutputKind.Melee, KindOf("Crush"));
        Assert.Equal(OutputKind.Skill, KindOf("Kick"));
        Assert.Equal(OutputKind.Ranged, KindOf("Archery"));
        Assert.Equal(OutputKind.DoT, KindOf("Stinging Swarm V (DoT)"));
        Assert.Equal(OutputKind.DamageShield, KindOf("Damage shield"));
        Assert.All(s.DamageBySource.Where(d => d.Name.StartsWith("Pet (", StringComparison.Ordinal)),
            d => Assert.Equal(OutputKind.Pet, d.Kind));
        Assert.DoesNotContain(s.DamageBySource, d => d.Kind == OutputKind.Other);
        // The pet's own split names its attacks by what they are, not as "Pet".
        Assert.NotEmpty(s.PetAbilities);
        Assert.All(s.PetAbilities, p => Assert.Contains(p.Kind, new[] { OutputKind.Melee, OutputKind.Spell }));

        // The mix over the same rows: the kinds present, in the fixed order, summing to one.
        var mix = OutputKindPresentation.Mix(s.DamageBySource);
        Assert.Equal(
            [OutputKind.Melee, OutputKind.Skill, OutputKind.Ranged, OutputKind.DoT,
             OutputKind.DamageShield, OutputKind.Pet],
            mix.Select(m => m.Kind));
        Assert.Equal(1.0, mix.Sum(m => m.Share), 9);
    }

    /// <summary>A fight's rows are stamped too — the DPS peek and every "Last fight" list read
    /// them, not the session's.</summary>
    [Fact]
    public void AFightsRowsCarryTheirKindsToo()
    {
        var s = Replay(
            At(0, 0, "You crush a puma for 24 points of damage."),
            At(0, 1, "You kick a puma for 12 points of damage."),
            At(0, 2, "A puma is pierced by YOUR thorns for 6 points of non-melee damage."));
        var fight = Assert.IsType<LastFightInfo>(s.LastFight);
        Assert.Equal(OutputKind.Melee, fight.ByAbility.Single(r => r.Name == "Crush").Kind);
        Assert.Equal(OutputKind.Skill, fight.ByAbility.Single(r => r.Name == "Kick").Kind);
        Assert.Equal(OutputKind.DamageShield, fight.ByAbility.Single(r => r.Kind == OutputKind.DamageShield).Kind);
    }

    // ---- the mix and its words -----------------------------------------------------------

    [Fact]
    public void TheMixIsInTheFixedOrderWithOnlyTheKindsPresent()
    {
        var mix = OutputKindPresentation.Mix(
        [
            new SourceDamage("Stinging Swarm (DoT)", 5, 300) { Kind = OutputKind.DoT },
            new SourceDamage("Crush", 10, 600) { Kind = OutputKind.Melee },
            new SourceDamage("Pet (Puma)", 4, 100) { Kind = OutputKind.Pet },
        ]);
        // Damage order would be Melee, DoT, Pet too — so a second, re-ordered case below
        // proves it is the FIXED order and not the rows' order that decides.
        Assert.Equal([OutputKind.Melee, OutputKind.DoT, OutputKind.Pet], mix.Select(m => m.Kind));
        Assert.Equal([0.6, 0.3, 0.1], mix.Select(m => Math.Round(m.Share, 9)));

        var reordered = OutputKindPresentation.Mix(
        [
            new SourceDamage("Pet (Puma)", 4, 900) { Kind = OutputKind.Pet },
            new SourceDamage("Crush", 10, 100) { Kind = OutputKind.Melee },
        ]);
        Assert.Equal([OutputKind.Melee, OutputKind.Pet], reordered.Select(m => m.Kind));
    }

    /// <summary>Nothing to explain draws nothing: no rows, zero totals, or every row Other —
    /// an ARCHIVED session, whose rows deserialize as Other and draw grey. One grey bar
    /// labelled "Other" would be a legend for a colour that says nothing.</summary>
    [Fact]
    public void AnArchivedSessionHasNoMix()
    {
        Assert.Empty(OutputKindPresentation.Mix([]));
        Assert.Empty(OutputKindPresentation.Mix([new SourceDamage("Crush", 0, 0) { Kind = OutputKind.Melee }]));
        Assert.Empty(OutputKindPresentation.Mix(
            [new SourceDamage("Crush", 10, 600), new SourceDamage("Stinging Swarm", 5, 300)]));
        // …but Other beside a classified kind IS a real share, and is named.
        var mixed = OutputKindPresentation.Mix(
            [new SourceDamage("Crush", 10, 600) { Kind = OutputKind.Melee }, new SourceDamage("Old", 1, 200)]);
        Assert.Equal([OutputKind.Melee, OutputKind.Other], mixed.Select(m => m.Kind));
    }

    [Fact]
    public void TheLegendSaysTheFoundersWordsAndItsShare()
    {
        Assert.Equal("DoT", OutputKindPresentation.Label(OutputKind.DoT));
        Assert.Equal("HoT", OutputKindPresentation.Label(OutputKind.HoT));
        Assert.All(OutputKindPresentation.Order,
            k => Assert.DoesNotContain("over time", OutputKindPresentation.Label(k), StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Melee 62%", OutputKindPresentation.LegendText(new(OutputKind.Melee, "Melee", 0.6249)));
        // A sliver that rounds to nothing says so, rather than "0%" beside a visible colour.
        Assert.Equal("Procs <1%", OutputKindPresentation.LegendText(new(OutputKind.Proc, "Procs", 0.004)));
    }

    /// <summary>Every kind is in the order exactly once — a kind missing from it would have no
    /// colour key and no legend slot, and draw with nothing behind it.</summary>
    [Fact]
    public void EveryKindHasOnePlaceInTheOrder()
    {
        Assert.Equal(Enum.GetValues<OutputKind>().OrderBy(k => k), OutputKindPresentation.Order.OrderBy(k => k));
        Assert.Equal(OutputKindPresentation.Order.Count, OutputKindPresentation.Order.Distinct().Count());
        Assert.Equal("kindDot", OutputKindPresentation.Token(OutputKind.DoT));
        Assert.Equal("kindHot", OutputKindPresentation.Token(OutputKind.HoT));
        Assert.Equal("kindDamageShield", OutputKindPresentation.Token(OutputKind.DamageShield));
        Assert.Equal("KindDoTBrush", OutputKindPresentation.BrushKey(OutputKind.DoT));
    }

    // ---- the repaint gates -----------------------------------------------------------------

    /// <summary>The row's kind is its colour, so both gates key on it (trap 72): two meters
    /// whose rows agree on every name and total but not on kind are two paints.</summary>
    [Fact]
    public void AKindChangeMovesBothRepaintGates()
    {
        LiveMeter Meter(OutputKind kind) => new("Damage",
            [new SourceDamage("Harm Touch", 1, 640) { Kind = kind }], 30, "dps", "", null, null);
        Assert.NotEqual(
            LivePresentation.MeterSignature("Damage", false, "Total", Meter(OutputKind.Proc)),
            LivePresentation.MeterSignature("Damage", false, "Total", Meter(OutputKind.Spell)));

        string Fingerprint(OutputKind kind) => CompanionProjection.SectionFingerprints(Phone(new StatsSnapshot
        {
            CombatSeconds = 60,
            DamageBySource = [new SourceDamage("Harm Touch", 1, 640) { Kind = kind }],
        }))[CompanionSurfaces.Combat];
        Assert.NotEqual(Fingerprint(OutputKind.Proc), Fingerprint(OutputKind.Spell));
    }

    // ---- the phone ---------------------------------------------------------------------------

    private static readonly DateTime Now = new(2026, 9, 29, 20, 0, 0);

    private static CompanionSnapshot Phone(StatsSnapshot stats) =>
        CompanionProjection.Build(new CompanionInputs
        {
            Stats = stats, Character = "Tumog", AppVersion = "2.0.1", Offered = CompanionSurfaces.All,
        }, Now);

    /// <summary>The phone gets each row's kind as a TOKEN and the mix with the desktop's own
    /// legend words — built by the same <see cref="OutputKindPresentation.Mix"/>, so the two
    /// cannot say different shares (trap 4), and never a sentence the page composes (trap 32).</summary>
    [Fact]
    public void ThePhoneGetsEachRowsKindTokenAndTheSameMix()
    {
        SourceDamage[] rows =
        [
            new("Crush", 10, 600) { Kind = OutputKind.Melee },
            new("Stinging Swarm (DoT)", 5, 300) { Kind = OutputKind.DoT },
            new("Damage shield", 4, 100) { Kind = OutputKind.DamageShield },
        ];
        var board = Phone(new StatsSnapshot { CombatSeconds = 60, DamageBySource = [.. rows] })
            .Combat!.Boards.Single(b => b.Key == "damage");

        Assert.Equal(["kindMelee", "kindDot", "kindDamageShield"], board.Session.Select(r => r.Kind));
        Assert.Equal(
            OutputKindPresentation.Mix(rows).Select(m => (OutputKindPresentation.Token(m.Kind),
                OutputKindPresentation.LegendText(m), m.Share)),
            board.SessionMix.Select(m => (m.Kind, m.Label, m.Share)));
        Assert.Equal("Damage shield 10%", board.SessionMix[^1].Label);
        // No fight yet: the fight scope has no rows and so no strip.
        Assert.Empty(board.FightMix);
    }

    /// <summary>
    /// The page DRAWS what it is sent (trap 34's must-list half — a field that reaches the wire
    /// and is never drawn passes every projection test there is), paints every kind with a
    /// rule of its own, and has not learned the legend's words (trap 32).
    /// </summary>
    [Fact]
    public void ThePageDrawsTheKindsItIsSentAndSpellsNoneOfTheirWords()
    {
        var html = File.ReadAllText(Path.Combine(SrcRoot(), "EQBuddy.Companion", "Web", "index.html"));
        foreach (var field in new[] { "b.fightMix", "b.sessionMix", "kind: r.kind", "seg.label", "seg.share", "mixStrip" })
            Assert.Contains(field, html, StringComparison.Ordinal);
        foreach (var kind in OutputKindPresentation.Order)
        {
            var css = Regex.Replace(OutputKindPresentation.Token(kind), "[A-Z]", m => "-" + m.Value.ToLowerInvariant());
            Assert.Contains($".rows li .{css}, .mix .{css} {{ background:var(--{css}); }}", html, StringComparison.Ordinal);
        }
        foreach (var words in new[] { "Direct heals", "Damage shield" })
            Assert.DoesNotContain(words, html, StringComparison.Ordinal);
    }

    private static string SrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "EQBuddy.Companion")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src");
    }

    // ---- the colours -----------------------------------------------------------------------

    public static TheoryData<string> Themes()
    {
        var data = new TheoryData<string>();
        foreach (var t in ThemePalettes.DefinedThemes) data.Add(t);
        return data;
    }

    /// <summary>The kind keys are DERIVED tones, one per kind, in <see cref="ThemeTones.Keys"/>
    /// order, and the phone's theme carries a token for each.</summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void EveryThemeDerivesAColourForEveryKind(string theme)
    {
        var palette = ThemePalettes.For(theme).ToList();
        var derived = ThemeTones.Derive(palette).ToList();
        Assert.Equal(ThemeTones.Keys, derived.Select(d => d.Key));
        foreach (var kind in OutputKindPresentation.Order)
            Assert.Contains(derived, d => d.Key == OutputKindPresentation.BrushKey(kind));

        var phone = CompanionTheme.Project(theme, palette);
        foreach (var kind in OutputKindPresentation.Order)
            Assert.True(phone.Colors.ContainsKey(OutputKindPresentation.Token(kind)), $"{theme}: no {kind} colour on the phone");
    }

    /// <summary>Solarized — the one light theme — takes the light set; everything else, the
    /// dark one. Decided off the theme's own ground, so a new theme needs no row.</summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void ALightGroundTakesTheLightSet(string theme)
    {
        var derived = ThemeTones.Derive(ThemePalettes.For(theme)).ToDictionary(d => d.Key, d => d.Hex);
        var expected = theme == "Solarized" ? ThemeTones.KindLight : ThemeTones.KindDark;
        Assert.Equal(expected, OutputKindPresentation.Order.Select(k => derived[OutputKindPresentation.BrushKey(k)]));
    }

    /// <summary>The floor every kind colour must clear on every shipped theme — WCAG's 3:1 for
    /// non-text graphics (a square and a bar ARE the information, not decoration).</summary>
    private const double GraphicContrastFloor = 3.0;

    /// <summary>
    /// **Every kind colour clears 3:1 against BOTH grounds a meter is drawn on**, on every
    /// shipped theme: the theme's background (a breakout float, the HUD panel) and its panel
    /// wash composited over that background (a widget card). SolarizedDark's panel is the
    /// hardest dark ground there is, and the mockup's Proc (#e0679a, 2.97) and Other
    /// (#8a8f98, 2.92) missed it — both were lifted rather than the floor lowered.
    /// </summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void EveryKindColourClearsTheGraphicContrastFloorOnEveryGround(string theme)
    {
        var palette = ThemePalettes.For(theme).ToDictionary(e => e.Key, e => e.Hex);
        var bg = Opaque(palette["BgBrush"]);
        var panel = Over(palette["PanelBrush"], bg);
        var derived = ThemeTones.Derive(ThemePalettes.For(theme)).ToDictionary(d => d.Key, d => d.Hex);
        var misses = new List<string>();
        foreach (var kind in OutputKindPresentation.Order)
        {
            var hex = derived[OutputKindPresentation.BrushKey(kind)];
            foreach (var (ground, name) in new[] { (bg, "background"), (panel, "panel") })
            {
                var ratio = Contrast(hex, ground);
                if (ratio < GraphicContrastFloor) misses.Add($"{kind} {hex} on {name} {ground}: {ratio:0.00}");
            }
        }
        Assert.True(misses.Count == 0, $"{theme}: " + string.Join("; ", misses));
    }

    /// <summary>The contrast arithmetic, checked against WCAG's own anchors — or a broken
    /// luminance would pass every colour above.</summary>
    [Fact]
    public void TheContrastArithmeticMatchesTheWcagAnchors()
    {
        Assert.Equal(21.0, Contrast("#FF000000", "#FFFFFFFF"), 2);
        Assert.Equal(1.0, Contrast("#FF777777", "#FF777777"), 6);
        Assert.Equal(4.48, Contrast("#FF777777", "#FFFFFFFF"), 2);
        Assert.True(ThemeTones.IsLightGround("#F2FDF6E3"));
        Assert.False(ThemeTones.IsLightGround("#F2002B36"));
    }

    private static double Contrast(string a, string b)
    {
        var (la, lb) = (ThemeTones.RelativeLuminance(a), ThemeTones.RelativeLuminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static string Opaque(string hex)
    {
        var (_, r, g, b) = ThemeTones.Parse(hex);
        return ThemeTones.Hex(0xFF, r, g, b);
    }

    private static string Over(string fg, string ground)
    {
        var (fa, fr, fgg, fb) = ThemeTones.Parse(fg);
        var (_, gr, gg, gb) = ThemeTones.Parse(ground);
        var a = fa / 255.0;
        byte Mix(byte f, byte g) => (byte)Math.Round(f * a + g * (1 - a));
        return ThemeTones.Hex(0xFF, Mix(fr, gr), Mix(fgg, gg), Mix(fb, gb));
    }
}
