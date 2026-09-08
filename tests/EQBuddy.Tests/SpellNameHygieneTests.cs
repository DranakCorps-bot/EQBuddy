using System.Reflection;
using System.Text.Json;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **A shipped spell NAME must be a token the game can put on a log line.**
///
/// PR #407's audit measured what the alternative costs. `BuffDurations.json` carried the
/// eqlwiki page TITLE `Shield of Thorns (Spell)` — disambiguated because the wiki also has
/// an item of that name — and <see cref="SpellCatalog.BaseName"/> strips a trailing roman
/// rank and nothing else, so the log's `Shield of Thorns V` could never meet it. That one
/// row disabled four mechanisms at once: the landing never resolved, Spell Casting
/// Reinforcement was gated on `resolved` so it never reached the spell, the learned-duration
/// lookup is keyed on the resolved label, and fade-learn is gated on a single candidate. The
/// owner's chip read 15:00 on all 28 landings of a damage shield his own log measures at
/// ~24 minutes, for as long as the row shipped.
///
/// It was a CLASS, not a row: four names across three catalogs. Nothing could see it —
/// the JSON is valid, the loader is happy, every buff test passed, and an unresolved
/// landing renders as a perfectly ordinary chip with the line's label on it. This is
/// trap 30's shape (**a curated field whose FORMAT nothing checks**), one level over from
/// trap 66's `EveryShippedPlaceholderSegmentIsAWholeMobName`.
///
/// The generator half is `scripts/harvests/eqlwiki/spellnames.py`, which strips the two
/// disambiguator shapes we have seen so a regenerate cannot re-introduce them. This is the
/// other half, and the division is deliberate (trap 48's move): **strip what is known,
/// refuse the whole class.** A third shape the harvest has never met fails here rather
/// than shipping silently.
/// </summary>
public class SpellNameHygieneTests
{
    /// <summary>
    /// The shipped catalogs whose `name` fields and `spells` arrays are SPELL NAMES —
    /// tokens the game writes. Each row carries why it is in the guard's scope
    /// (`DeadSettingTests.Known`'s shape: an entry nobody can see is a blind spot).
    /// </summary>
    private static readonly Dictionary<string, string> SpellNameCatalogs = new()
    {
        ["BuffDurations.json"] = "buff countdown candidates — the four polluted rows lived here and in the two below (#407)",
        ["RankedBuffDurations.json"] = "measured per-RANK durations, keyed on the exact ranked name a cast line writes — a disambiguated title here could never be reached at all",
        ["FadeMessages.json"] = "fade candidates; a name here is matched against the spell a fade line names",
        ["DebuffLandings.json"] = "debuff landing candidates, matched against BaseName of a cast",
        ["SlowSpells.json"] = "slow candidates plus fadeOf (the colliding haste spells), all matched against log spellings",
        ["CcSpells.json"] = "crowd-control classification, keyed by BaseName of a cast",
        ["CharmSpells.json"] = "charm cast times and the notCharms veto list, both keyed by BaseName",
        ["MezSpells.json"] = "mez durations, keyed by BaseName of your own cast",
        ["RegenSpells.json"] = "regen-tick attribution, keyed by BaseName",
        ["SpellLevels.json"] = "class/level lookup and the ClassInference signal source",
    };

    /// <summary>
    /// The shipped catalogs that carry no spell names, each with the reason its `name`
    /// fields are legitimately NOT log-writable spell tokens. This half is what makes the
    /// list above provably complete rather than merely long — see
    /// <see cref="EveryShippedCatalogIsClassifiedOneWayOrTheOther"/>.
    /// </summary>
    private static readonly Dictionary<string, string> NotSpellNameCatalogs = new()
    {
        ["AaCatalog.json"] = "AA names — the wiki disambiguates these differently and nothing matches them to a cast line",
        ["QuestCatalog.json"] = "quest and item names; real ones carry parentheses ('Journeyman Boots (Quest)')",
        ["SpawnCatalog.json"] = "mob names and placeholder prose — guarded by trap 66's placeholder test instead",
        ["EpicQuestChecklist.json"] = "quest step prose",
        ["GuideCatalog.json"] = "guide, stage and objective names — a step's `name` is prose we write for a player, never a token the game casts; its item and mob names are guarded by GuideCatalogTests instead",
        ["RaidTargets.json"] = "boss names",
        ["ZoneGraph.json"] = "zone names and connections",
        ["WhatsNew.json"] = "release notes — prose written by us, for players",
    };

    private const string Prefix = "EQBuddy.Core.Data.";

    /// <summary>Property names whose STRING array elements are spell names. Where the
    /// elements are objects instead, the `name` rule below catches them.</summary>
    private static readonly HashSet<string> NameArrays = ["spells", "fadeOf", "notCharms"];

    private static JsonDocument Load(string file)
    {
        using var stream = typeof(SpellCatalog).Assembly.GetManifestResourceStream(Prefix + file)
            ?? throw new InvalidOperationException($"{file} is not an embedded resource");
        return JsonDocument.Parse(stream);
    }

    private static void Collect(JsonElement node, string? property, List<string> into)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in node.EnumerateObject()) Collect(p.Value, p.Name, into);
                break;
            case JsonValueKind.Array:
                foreach (var v in node.EnumerateArray())
                {
                    if (v.ValueKind == JsonValueKind.String && property is not null
                        && NameArrays.Contains(property)) into.Add(v.GetString()!);
                    else Collect(v, property, into);
                }
                break;
            case JsonValueKind.String when property == "name":
                into.Add(node.GetString()!);
                break;
        }
    }

    private static List<string> SpellNamesIn(string file)
    {
        var names = new List<string>();
        using var doc = Load(file);
        Collect(doc.RootElement, null, names);
        return names;
    }

    /// <summary>
    /// The guard itself. Every spell name in every spell catalog must be a name a log line
    /// could carry — 3,500-odd of them, and the whole product's buff accuracy rests on each
    /// one being able to meet `BaseName(what the game wrote)`.
    ///
    /// Prove-fail: restore any one of the four names PR #407 named
    /// (`Shield of Thorns (Spell)`, `Kilva's Skin of Flame (Spell)`, `Firestrike (Effect)`)
    /// and this fails naming the file and the row.
    /// </summary>
    [Fact]
    public void NoShippedSpellNameCarriesAWikiDisambiguator()
    {
        var offenders = new List<string>();
        foreach (var (file, _) in SpellNameCatalogs)
            offenders.AddRange(SpellNamesIn(file)
                .Where(n => !SpellCatalog.IsLogWritableName(n))
                .Distinct()
                .Select(n => $"{file}: '{n}'"));

        Assert.Empty(offenders);
    }

    /// <summary>A guard that reads a handful of rows and calls it a catalog is trap 39's
    /// vacuous-assertion shape. The counts are the floor the sweep actually covered.</summary>
    [Fact]
    public void TheSweepReachesEveryCatalogItClaimsToCover()
    {
        foreach (var (file, reason) in SpellNameCatalogs)
            Assert.True(SpellNamesIn(file).Count > 0,
                $"{file} contributed no spell names to the sweep, but the list says: {reason}");

        Assert.True(SpellNameCatalogs.Keys.Sum(f => SpellNamesIn(f).Count) > 3_000);
    }

    /// <summary>
    /// **The committed negative** (trap 39: every equality assertion deserves one). Without
    /// this, a predicate that returned `true` for everything would satisfy the guard above
    /// forever and read as coverage.
    /// </summary>
    [Fact]
    public void ThePredicateRefusesTheNamesThatCausedThis()
    {
        Assert.False(SpellCatalog.IsLogWritableName("Shield of Thorns (Spell)"));
        Assert.False(SpellCatalog.IsLogWritableName("Kilva's Skin of Flame (Spell)"));
        Assert.False(SpellCatalog.IsLogWritableName("Firestrike (Effect)"));
        // The whole class, not the two shapes the harvest strips.
        Assert.False(SpellCatalog.IsLogWritableName("Shield of Thorns (Item)"));
        Assert.False(SpellCatalog.IsLogWritableName("Firestrike (disambiguation)"));
        Assert.False(SpellCatalog.IsLogWritableName(""));

        // And it does not refuse the names the game actually writes, ranks included.
        Assert.True(SpellCatalog.IsLogWritableName("Shield of Thorns"));
        Assert.True(SpellCatalog.IsLogWritableName("Shield of Thorns V"));
        Assert.True(SpellCatalog.IsLogWritableName("Kilva's Skin of Flame"));
        Assert.True(SpellCatalog.IsLogWritableName("Turgur`s Insects"));
    }

    /// <summary>
    /// The four names, corrected, are in the shipped catalogs under the spelling the game
    /// writes — and reachable from the log's own ranked spelling through
    /// <see cref="SpellCatalog.BaseName"/>, which is the meeting that could not happen.
    /// </summary>
    [Fact]
    public void TheFourCorrectedNamesAreReachableFromALogSpelling()
    {
        Assert.Contains("Shield of Thorns", SpellNamesIn("BuffDurations.json"));
        Assert.Contains("Shield of Thorns", SpellNamesIn("FadeMessages.json"));
        Assert.Contains("Kilva's Skin of Flame", SpellNamesIn("FadeMessages.json"));
        Assert.Contains("Firestrike", SpellNamesIn("DebuffLandings.json"));

        // What the log writes, folded, is what the catalog now holds.
        Assert.Equal("Shield of Thorns", SpellCatalog.BaseName("Shield of Thorns V"));
        Assert.Equal("Kilva's Skin of Flame", SpellCatalog.BaseName("Kilva's Skin of Flame II"));

        // And the wiki's disambiguated spellings are gone from all three.
        Assert.DoesNotContain("Shield of Thorns (Spell)", SpellNamesIn("BuffDurations.json"));
        Assert.DoesNotContain("Shield of Thorns (Spell)", SpellNamesIn("FadeMessages.json"));
        Assert.DoesNotContain("Kilva's Skin of Flame (Spell)", SpellNamesIn("FadeMessages.json"));
        Assert.DoesNotContain("Firestrike (Effect)", SpellNamesIn("DebuffLandings.json"));
    }

    /// <summary>
    /// **Trap 30's answer: a hand-maintained list stops covering the set the day the set
    /// grows.** A new `Data/*.json` must be classified — swept, or excluded with a reason —
    /// and this is what makes that a build failure rather than a silent gap.
    /// </summary>
    [Fact]
    public void EveryShippedCatalogIsClassifiedOneWayOrTheOther()
    {
        var shipped = typeof(SpellCatalog).Assembly.GetManifestResourceNames()
            .Where(r => r.StartsWith(Prefix, StringComparison.Ordinal)
                        && r.EndsWith(".json", StringComparison.Ordinal))
            .Select(r => r[Prefix.Length..])
            .ToHashSet();

        var classified = SpellNameCatalogs.Keys.Concat(NotSpellNameCatalogs.Keys).ToHashSet();

        Assert.Empty(shipped.Except(classified));   // a new catalog nobody decided about
        Assert.Empty(classified.Except(shipped));   // a row for a catalog that no longer ships
        Assert.Empty(SpellNameCatalogs.Keys.Intersect(NotSpellNameCatalogs.Keys));
    }
}
