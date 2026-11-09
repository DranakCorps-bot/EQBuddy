using System.Reflection;
using System.Text.Json;
using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>The wiki-harvested CC catalog (Data\CcSpells.json), added for Chaosrah's
/// report: "all CC" alerts missed enchanter stuns — the hand seed knew 6 stuns, the
/// game has 87.</summary>
public class CcSpellCatalogTests
{
    private const string Ts = "[Sat Jul 18 15:39:13 2026] ";

    [Theory]
    [InlineData("Color Slant", SpellCategory.Stun)]              // enchanter, never in the hand seed
    [InlineData("Brusco's Bombastic Bellow", SpellCategory.Stun)] // bard stun
    [InlineData("Harpy Voice", SpellCategory.Mesmerize)]          // NPC mez — breadth check
    [InlineData("Alluring Whispers", SpellCategory.Charm)]        // the NPC-charms-YOU spell
    public void WikiHarvestedSpellsClassify(string spell, SpellCategory expected)
    {
        Assert.Equal(expected, new SpellCatalog().Classify(spell));
    }

    [Fact]
    public void LogVerifiedSeedEntriesKeepTheirClassification()
    {
        // The bard song split (issue #29) is log-verified in the curated seed; whichever
        // source answers first, the classification must hold.
        Assert.Equal(SpellCategory.Charm, new SpellCatalog().Classify("Solon's Bravura"));
        Assert.Equal(SpellCategory.Mesmerize, new SpellCatalog().Classify("Kelin's Lucid Lullaby"));
    }

    [Fact]
    public void ColorFluxFadeFiresAnAnyCrowdControlRule()
    {
        // Chaosrah's exact gap, end to end: a Color Flux fade must count on an
        // "any CC" spell-fade watch rule.
        var rule = new TrackedRule
        {
            Name = "CC broke", Kind = WatchKind.SpellFade,
            SpellFilter = SpellFilter.AnyCrowdControl, Enabled = true,
        };
        var stats = new SessionStats();
        stats.Apply(LogParser.Parse(Ts + "Your Color Flux spell has worn off of an orc pawn.")!);
        var result = stats.Snapshot(null, [rule]).Tracked.Single();
        Assert.Equal(1, result.TotalQuantity);
    }

    /// <summary>
    /// DRA-4193. eqlwiki Greater Immobilize (fetched 2026-11-09) is a Root: slot
    /// effect Root, and <c>msg_cast_on_you</c> is "Your feet adhere to the ground."
    /// Classify is not the pin — the "immobiliz" family fragment would still answer
    /// Root if this curated row were deleted.
    /// </summary>
    [Fact]
    public void GreaterImmobilizeIsACuratedRoot()
    {
        using var stream = typeof(SpellCatalog).Assembly
            .GetManifestResourceStream("EQBuddy.Core.Data.CcSpells.json")
            ?? throw new InvalidOperationException("CcSpells.json missing from resources");
        using var doc = JsonDocument.Parse(stream);
        var row = Assert.Single(
            doc.RootElement.GetProperty("spells").EnumerateArray(),
            s => s.GetProperty("name").GetString() == "Greater Immobilize");
        Assert.Equal("Root", row.GetProperty("category").GetString());
        Assert.Equal(SpellCategory.Root, new SpellCatalog().Classify("Greater Immobilize"));
    }
}
