using EQBuddy.Core;

namespace EQBuddy.Tests;

public class AaCatalogTests
{
    [Fact]
    public void EmbeddedCatalogLoadsEveryAbility()
    {
        // 144 rows from the eqlwiki Alternate Advancement page (revision 2026-10-30).
        // Druid and Wizard both list Quick Evacuation, so the name map is one under
        // the row count.
        Assert.Equal(143, AaCatalog.Count);
    }

    [Fact]
    public void LookupIsCaseInsensitiveAndCarriesTheEffectText()
    {
        var scr = AaCatalog.Find("spell casting reinforcement");
        Assert.NotNull(scr);
        Assert.Equal("Archetype", scr!.Category);
        Assert.Equal(4, scr.MaxRank);
        Assert.Contains("increases the duration of beneficial spells", scr.Effect);
    }

    [Fact]
    public void UnknownAbilityIsNullNotAGuess()
    {
        // The log names toggle rows ("Symphonic Aura: Enabled") the wiki page doesn't
        // list — the ledger shows them without a tooltip rather than borrowing one.
        Assert.Null(AaCatalog.Find("Symphonic Aura: Enabled"));
    }

    /// <summary>
    /// The rows DRA-4192 measured against the Alternate Advancement page
    /// (revision 2026-10-30T16:57:51Z). Numbers are the page's; a remaining "?"
    /// is the page's own unconfirmed rank.
    /// </summary>
    [Fact]
    public void Dra4192RowsMatchTheWikiRevision()
    {
        var widened = AaCatalog.Find("Widened Perception");
        Assert.NotNull(widened);
        Assert.Equal("General", widened!.Category);
        Assert.Equal(1, widened.MaxRank);
        Assert.Equal("12", widened.CostPerRank);
        Assert.Equal(25, widened.LevelRequirement);

        var pointBlank = AaCatalog.Find("Point Blank Fire");
        Assert.NotNull(pointBlank);
        Assert.Equal("Archetype", pointBlank!.Category);
        Assert.Equal("0", pointBlank.CostPerRank);
        Assert.Equal(1, pointBlank.LevelRequirement);

        Assert.Null(AaCatalog.Find("Tireless Spirit"));
        var sprint = AaCatalog.Find("Tireless Sprint");
        Assert.Equal("Berserker", sprint!.Class);
        Assert.Equal(1, sprint.MaxRank);
        Assert.Equal("3", sprint.CostPerRank);
        Assert.Equal(10, sprint.LevelRequirement);
        Assert.Contains("Activation ID: 372", sprint.Effect);

        var healing = AaCatalog.Find("Innate Divine Healing");
        Assert.Equal("Cleric", healing!.Class);
        Assert.Equal("0", healing.CostPerRank);
        Assert.Equal(1, healing.LevelRequirement);
        Assert.Contains("5% bonus", healing.Effect);

        var aura = AaCatalog.Find("Divine Aura");
        Assert.Equal(50, aura!.LevelRequirement);
        Assert.Equal("6", aura.CostPerRank);
        Assert.Contains("invulnerable", aura.Effect);

        Assert.Null(AaCatalog.Find("Valiant Steed"));
        Assert.Null(AaCatalog.Find("Abyssal Steed"));

        Assert.Equal(1, AaCatalog.Find("Master of All")!.MaxRank);
        Assert.Equal("10", AaCatalog.Find("Master of All")!.CostPerRank);
        Assert.Equal(3, AaCatalog.Find("Spell Casting Subtlety")!.MaxRank);
        Assert.Equal("2/4/6", AaCatalog.Find("Spell Casting Subtlety")!.CostPerRank);
        Assert.Equal(3, AaCatalog.Find("Reaching Notes")!.MaxRank);
        Assert.Equal("2/4/6", AaCatalog.Find("Reaching Notes")!.CostPerRank);
        Assert.Equal(5, AaCatalog.Find("Symphonic Aura")!.MaxRank);
        Assert.Equal("0/3/3/3/3", AaCatalog.Find("Symphonic Aura")!.CostPerRank);
        Assert.Equal(3, AaCatalog.Find("Unbound Companion")!.MaxRank);
        Assert.Contains("2/?/?%", AaCatalog.Find("Unbound Companion")!.Effect);

        Assert.Equal(1, AaCatalog.Find("Combat Stability")!.LevelRequirement);
        Assert.Equal(1, AaCatalog.Find("Quick Buff")!.LevelRequirement);
        Assert.Equal(50, AaCatalog.Find("Paragon of Spirit")!.LevelRequirement);
        Assert.Equal(12, AaCatalog.Find("Unbound Ferocity")!.LevelRequirement);
        Assert.Equal(12, AaCatalog.Find("Unbound Life")!.LevelRequirement);
        Assert.Equal(12, AaCatalog.Find("Unbound Lethality")!.LevelRequirement);

        Assert.Equal("3/6/9", AaCatalog.Find("Alchemy Mastery")!.CostPerRank);
        Assert.Equal("2/4/6", AaCatalog.Find("Baking Mastery")!.CostPerRank);
        Assert.Equal("2/4/6", AaCatalog.Find("Tailoring Mastery")!.CostPerRank);
        Assert.Equal("2/4/6", AaCatalog.Find("Jewel Craft Mastery")!.CostPerRank);
        Assert.Contains("failing Jewelcrafting recipes", AaCatalog.Find("Jewel Craft Mastery")!.Effect);
        Assert.Equal("3/3/3/3/3/3", AaCatalog.Find("Crafting Mastery")!.CostPerRank);
        Assert.Equal("1/1/1/3/6/9", AaCatalog.Find("First Aid")!.CostPerRank);
        Assert.Equal("3/3/3/3/3", AaCatalog.Find("Innate Eminence")!.CostPerRank);
        Assert.Equal("1/1/1", AaCatalog.Find("Innate Lung Capacity")!.CostPerRank);
        Assert.Equal("1/1/1", AaCatalog.Find("Innate Metabolism")!.CostPerRank);
        Assert.Equal("2/2/2/2/2", AaCatalog.Find("Innate Spell Resistance")!.CostPerRank);
        Assert.Equal("3/6/9", AaCatalog.Find("Burst of Power")!.CostPerRank);
        Assert.Equal("3/5/6/6/6", AaCatalog.Find("Conjurer's Efficiency")!.CostPerRank);
        Assert.Equal("3/6/?", AaCatalog.Find("Turn Summoned")!.CostPerRank);
        Assert.Contains("400/800/?", AaCatalog.Find("Turn Summoned")!.Effect);
    }
}
