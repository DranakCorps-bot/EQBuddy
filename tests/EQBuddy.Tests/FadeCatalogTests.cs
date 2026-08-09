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
