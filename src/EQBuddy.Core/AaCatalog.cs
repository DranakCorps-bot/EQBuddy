using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EQBuddy.Core;

/// <summary>One AA ability as the eqlwiki Alternate Advancement page describes it.
/// Effect text is verbatim wiki prose — "?" inside it is the wiki's own unconfirmed
/// marker, never ours.</summary>
public sealed class AaCatalogEntry
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    [JsonPropertyName("class")] public string? Class { get; set; }
    public int? MaxRank { get; set; }
    public string CostPerRank { get; set; } = "";
    public int? LevelRequirement { get; set; }
    public string Effect { get; set; } = "";
}

/// <summary>
/// The embedded AA catalog (144 rows, eqlwiki Alternate Advancement page, hand-edited
/// to revision 2026-10-30). First consumer: the Progress card's AA ledger rows show
/// what each owned ability actually does.
///
/// Duration note for timer features (full sweep in the 2026-08-06 harvest report): EQ
/// Legends has NO AA that extends detrimental mez/charm durations — MezTracker's learned
/// durations need no per-character AA correction; Adamant Will only moves resist chance.
/// The modifier that matters is Spell Casting Reinforcement (+5/15/30/50% BENEFICIAL
/// spell duration) — any future buff-duration countdown must read the ledger for it.
/// </summary>
public static class AaCatalog
{
    private sealed class Root
    {
        public List<AaCatalogEntry> Abilities { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<Dictionary<string, AaCatalogEntry>> ByName = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.AaCatalog.json")
            ?? throw new InvalidOperationException("AaCatalog.json missing from resources");
        var root = JsonSerializer.Deserialize<Root>(stream, JsonOpts) ?? new Root();
        // Last-in wins on duplicate names. Druid and Wizard both list "Quick Evacuation",
        // so a name lookup returns the Wizard row.
        var map = new Dictionary<string, AaCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in root.Abilities) map[a.Name] = a;
        return map;
    });

    public static int Count => ByName.Value.Count;

    /// <summary>Every catalog entry (name-deduplicated) — the level-unlock scan needs
    /// the whole table, not a name lookup.</summary>
    public static IReadOnlyCollection<AaCatalogEntry> All => ByName.Value.Values;

    /// <summary>Catalog entry for an ability name, or null — the log can name abilities
    /// the wiki page doesn't list yet ("Symphonic Aura: Enabled" toggle rows).</summary>
    public static AaCatalogEntry? Find(string name) =>
        ByName.Value.TryGetValue(name, out var e) ? e : null;
}
