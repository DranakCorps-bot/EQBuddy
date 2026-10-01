using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// The fade catalog is generated (scripts/harvests/eqlwiki/fades-harvest.py) from the
/// wiki's msg_wears_off fields merged over the hand-curated seed. These tests pin the
/// invariants the generator promises, so a regeneration that breaks one fails here
/// instead of in someone's Watch rules.
/// </summary>
public class FadeCatalogTests
{
    private const string Ts = "[Sat Jul 18 15:39:13 2026] ";

    // Discussion #64: Spirit of the Puma fading off YOURSELF prints flavor text, not a
    // named worn-off line — only the catalog can route it to a By-name SpellFade rule.
    [Fact]
    public void PumaSelfFadeCarriesItsSpell()
    {
        var evt = Assert.IsType<BuffFadeEvent>(LogParser.Parse(Ts + "The spirit of the puma departs."));
        Assert.Contains("Spirit of the Puma", evt.Spells);
        Assert.Equal("Spirit of the Puma", evt.Label);
        Assert.Equal("Buff", evt.Category);
    }

    [Fact]
    public void PumaCanBeFoundBySpellName() =>
        Assert.Equal("Spirit of the Puma",
            FadeMessageCatalog.Default.FindBySpell("Spirit of the Puma")?.Label);

    [Fact]
    public void BuffSpellChoicesIncludeKnownBuffNamesAndLabels()
    {
        Assert.Contains("Spirit of the Puma", FadeMessageCatalog.Default.BuffSpellChoices);
        Assert.Contains("Haste", FadeMessageCatalog.Default.BuffSpellChoices);
    }

    // #710 / DRA-638: the picker hid every buff whose fade LINE is shared with a debuff
    // or with another family — the harvest marks any multi-spell line "Other". The buff
    // catalog is the one producer of "is this a buff" (BuffTracker times from it), so
    // the picker asks it. Must-list half: every fade-catalog spell the buff catalog
    // times is offered. Reverting the Concat reddens this with 119 missing names.
    [Fact]
    public void EveryFadeSpellTheBuffTimersTimeIsInTheWatchPicker()
    {
        var choices = FadeMessageCatalog.Default.BuffSpellChoices.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = FadeMessageCatalog.Default.Entries
            .SelectMany(e => e.Spells)
            .Where(BuffDurationCatalog.Default.IsBuffSpell)
            .Select(SpellCatalog.BaseName)
            .Where(s => !choices.Contains(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Empty(missing);
    }

    // Named members of the 119 the picker newly offers, each on a shared "Other" line
    // ("Your heroism fades.", "Your shielding fades.", "The Avatar departs."). Red
    // without the buff gate. NOT the shroud: the wiki's "Shroud of Hate Recourse" page
    // carries spellname "siphon strength recourse" (a template copy-paste, the 25th
    // spellname-mismatch row), which the picker already offered folded into the real
    // Siphon Strength Recourse — so #710's visible fix is the wiki's name, not this.
    [Theory]
    [InlineData("Heroism", "Your heroism fades.")]
    [InlineData("Shield of the Magi", "Your shielding fades.")]
    [InlineData("Avatar", "The Avatar departs.")]
    public void ABuffOnASharedLineIsOffered(string spell, string line)
    {
        Assert.Equal("Other", FadeMessageCatalog.Default.Find(line)?.Category);
        Assert.True(BuffDurationCatalog.Default.IsBuffSpell(spell));
        Assert.Contains(spell, FadeMessageCatalog.Default.BuffSpellChoices);
    }

    // Negative half: the picker still never offers a detrimental. Shroud of Hate/Pain
    // and Scream of Hate/Pain are on the mob (wiki: Detrimental, Single); the buff the
    // caster wears is the recourse above, not the spell they cast.
    [Theory]
    [InlineData("Shroud of Hate")]
    [InlineData("Shroud of Pain")]
    [InlineData("Scream of Hate")]
    [InlineData("Scream of Pain")]
    public void TheDetrimentalHalfOfAStatStealIsNotOffered(string spell)
    {
        Assert.NotNull(FadeMessageCatalog.Default.FindBySpell(spell));
        Assert.DoesNotContain(spell, FadeMessageCatalog.Default.BuffSpellChoices,
            StringComparer.OrdinalIgnoreCase);
    }

    // A catalog built without the buff gate keeps the line-category behaviour exactly.
    [Fact]
    public void WithoutTheBuffGateTheLineCategoryAloneDecides()
    {
        var mixed = new FadeMessageCatalog.Entry
        {
            Message = "The hatred departs.", Label = "The hatred departs", Category = "Other",
            Spells = ["Scream of Hate", "siphon strength recourse"],
        };
        Assert.Empty(new FadeMessageCatalog([mixed]).BuffSpellChoices);
        Assert.Equal(["siphon strength recourse"],
            new FadeMessageCatalog([mixed], s => s == "siphon strength recourse").BuffSpellChoices);
    }

    // Every catalogued message must actually reach the catalog lookup: an entry whose
    // message some earlier parser rule also matches is dead weight and a lying candidate list.
    [Fact]
    public void EveryCatalogMessageParsesAsBuffFade()
    {
        foreach (var entry in FadeMessageCatalog.Default.Entries)
        {
            var evt = LogParser.Parse(Ts + entry.Message);
            var fade = Assert.IsType<BuffFadeEvent>(evt);
            Assert.Equal(entry.Label, fade.Label);
        }
    }

    // Befriend Animal's wiki wear-off text IS the generic charm-break line. The generator
    // must exclude it: the catalog lookup runs before SpellWornOffRx, and charm-break
    // handling (pets, mez tracker) depends on the SpellWornOffEvent shape.
    [Fact]
    public void CharmBreakLineStaysAWornOffEvent()
    {
        var evt = LogParser.Parse(Ts + "Your charm spell has worn off.");
        var worn = Assert.IsType<SpellWornOffEvent>(evt);
        Assert.Equal("charm", worn.Spell);
    }

    // Bystander-visible world emote (69 port spells share it) — not a personal buff fade.
    [Fact]
    public void PortalDespawnIsNotAFade() =>
        Assert.Null(LogParser.Parse(Ts + "The portal shimmers and fades."));

    // "You feel better." is both a heal landing and Allure of Death fading; ambiguous
    // lines must stay out or rules fire when the OTHER spell lands.
    [Fact]
    public void CastCollisionLinesAreExcluded() =>
        Assert.Null(FadeMessageCatalog.Default.Find("You feel better."));

    // The hand-curated seed survives regeneration: label and note-bearing entries win.
    [Fact]
    public void CuratedHasteEntrySurvivesGeneration()
    {
        var haste = FadeMessageCatalog.Default.Find("Your speed returns to normal.");
        Assert.NotNull(haste);
        Assert.Equal("Haste", haste!.Label);
        Assert.Contains("Alacrity", haste.Spells);
    }
}
