namespace EQBuddy.E2E;

/// <summary>
/// **A player's own colour for a damage/healing type reaches the screen** (David, 2026-09-29:
/// "let people color code the types to whichever color they want from a color wheel").
///
/// The unit suite (<c>KindColourTests</c>) proves the pick reaches the PALETTE; this proves it
/// reaches a PAINTED square — "in the settings" and "in effect" are different claims (trap 42),
/// and the dump reads each square's resolved brush rather than its kind.
///
/// [Collection("e2e")] because every test here launches a real always-on-top widget.
/// </summary>
[Collection("e2e")]
public sealed class KindColourTests
{
    /// <summary>
    /// Melee picked as magenta; the DPS peek over the fixture's last pull draws its three rows
    /// (DoT, Skill, Melee — predicted from the fixture, see HudExpandTests) and the Melee
    /// square is painted the PICK while the other two stay on their locked dark defaults
    /// (DoT #9B86D6, Skill #E8743B). The un-picked two are the half that stops a pick from
    /// passing by painting every row in it.
    /// </summary>
    [Fact]
    public void APickedTypeIsPaintedInThePlayersColourAndTheOthersKeepTheirs()
    {
        using var app = new AppHarness(settings =>
        {
            settings.Minimized = true;
            settings.MiniStats = ["kills", "dps", "xp"];
            settings.DisabledBreakouts =
                ["Damage", "Healing", "Pet", "Watch", "Loot", "Buffs"];
            settings.DefaultRulesVersion = int.MaxValue;
            settings.TrackedRules.Clear();
            settings.KindColours = new() { ["Melee"] = "#FF00FF" };
        }, new Dictionary<string, string> { ["EQBUDDY_HUDEXPAND"] = "dps:peek" });
        app.Launch();

        app.WaitForDump("hudExpandKinds", "kindDot,kindSkill,kindMelee",
            "the last pull's rows to be drawn, each in its own kind");
        app.WaitForDump("hudExpandKindHex", "#9B86D6,#E8743B,#FF00FF",
            "the picked Melee square to be painted the player's colour, the others their defaults");
    }

    /// <summary>
    /// Options → Look builds one row per type, counts the pick in force, and the wheel opens
    /// (through <c>EQBUDDY_KIND_WHEEL</c>, the pointer's stand-in — trap 22).
    /// </summary>
    [Fact]
    public void OptionsLookBuildsARowPerTypeAndOpensTheWheel()
    {
        using var app = new AppHarness(settings => settings.KindColours = new() { ["Pet"] = "#112233" },
            new Dictionary<string, string>
            {
                ["EQBUDDY_OPTIONS"] = "1",
                ["EQBUDDY_KIND_WHEEL"] = "DoT",
            });
        app.Launch();

        app.WaitForDump("optionsLookKindRows", 11, "one colour row per damage/healing type");
        app.WaitForDump("optionsLookKindPicked", 1, "the one seeded pick to be counted");
        app.WaitForDump("optionsLookKindWheel", "DoT", "the DoT row's colour wheel to be open");
    }
}
