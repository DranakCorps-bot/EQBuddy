using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EQBuddy.Core;

/// <summary>One class's availability level for a spell. Class spellings match
/// <see cref="QuestClassFilter.Classes"/> ("Shadow Knight", not "Shadowknight" —
/// the promotion folds the wiki's variant), so app class lists join without
/// translation. Beastlord rows exist in the data even though the class picker
/// doesn't offer it yet.</summary>
public sealed class SpellClassLevel
{
    [JsonPropertyName("class")] public string Class { get; set; } = "";
    public int Level { get; set; }

    /// <summary>Where this class/level came from: <c>"class"</c> (the class page,
    /// authoritative) or <c>"spell"</c> (derived from the spell's own page because the
    /// class page has NO section for that level).
    ///
    /// **It exists so a derived row can be marked rather than hidden** — David's ruling
    /// (2026-08-23) is that the class page wins and anything from a spell page is FLAGGED,
    /// and Bevel's lock is "do not silently pad from spell pages". Every class page stops
    /// at 50 against Legends' cap of 60, so a level-50 character's whole next-level list
    /// is derived; that has to be visible.
    ///
    /// Defaults to <see cref="ClassPage"/> so a hand-built test catalog, and any older
    /// SpellLevels.json, reads as authoritative rather than as a silent unknown.</summary>
    public string Source { get; set; } = ClassPage;

    public const string ClassPage = "class";
    public const string SpellPage = "spell";

    public bool IsDerived => Source.Equals(SpellPage, StringComparison.OrdinalIgnoreCase);
}

/// <summary>One spell's per-class levels as eqlwiki's spell pages record them.
/// Name and levels only — effect text stays on the wiki, an unlock row never
/// invents it.</summary>
public sealed class SpellLevelEntry
{
    public string Name { get; set; } = "";
    public List<SpellClassLevel> Classes { get; set; } = [];
}

/// <summary>The picked classes that gain a spell at exactly one level — the spell
/// half of a <see cref="LevelUnlockSet"/>. Classes carry the catalog's spelling
/// for display regardless of how the caller cased its picks.</summary>
/// <param name="Derived">Every class row behind this unlock came from the spell's own
/// page rather than a class page — so the surface says so instead of presenting it with
/// the same confidence as a curated row. False when ANY contributing class row is from a
/// class page: a mixed unlock is answered by the authoritative half.</param>
public sealed record SpellUnlock(string Name, IReadOnlyList<string> Classes, bool Derived = false);

/// <summary>
/// The embedded spell-level catalog (eqlwiki spells harvest, promoted by
/// scripts/harvests/eqlwiki/spell-levels-promote.py). Every row is a real
/// class-level fact from a spell page — level 0/absent never promotes, so a
/// match here is never "unknown coerced to something".
///
/// Spells have no class-agnostic rows: unlike the AA catalog's General and
/// Archetype categories, a spell with no picked class showing would be every
/// class's spellbook at once, so an empty class pick yields NO spell unlocks
/// (the AA side still answers with its class-agnostic categories).
/// </summary>
public sealed class SpellLevelCatalog
{
    private sealed class Root { public List<SpellLevelEntry> Spells { get; set; } = []; }

    private readonly List<SpellLevelEntry> _spells;
    private readonly Dictionary<string, SpellLevelEntry> _byName;

    public SpellLevelCatalog(IEnumerable<SpellLevelEntry> spells)
    {
        _spells = spells.Where(s => s.Name.Length > 0 && s.Classes.Count > 0).ToList();
        // Promotion output is name-deduplicated; last-in wins keeps a hand-built
        // test catalog with a stray twin serviceable all the same.
        _byName = new Dictionary<string, SpellLevelEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in _spells) _byName[s.Name] = s;
    }

    public int Count => _spells.Count;

    /// <summary>Every catalog entry — the level-unlock scan needs the whole table.</summary>
    public IReadOnlyList<SpellLevelEntry> All => _spells;

    /// <summary>Catalog entry for a spell name, or null.</summary>
    public SpellLevelEntry? Find(string name) =>
        _byName.TryGetValue(name, out var e) ? e : null;

    /// <summary>Spells any picked class gains at exactly <paramref name="level"/>,
    /// each with the picked classes that gain it there, names alphabetical.</summary>
    public IReadOnlyList<SpellUnlock> UnlocksAt(IReadOnlyCollection<string> classes, int level)
    {
        if (classes.Count == 0) return [];
        var unlocks = new List<SpellUnlock>();
        foreach (var s in _spells)
        {
            var rows = s.Classes
                .Where(c => c.Level == level
                    && classes.Contains(c.Class, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (rows.Count > 0)
                unlocks.Add(new SpellUnlock(s.Name, [.. rows.Select(c => c.Class)],
                    Derived: rows.All(c => c.IsDerived)));
        }
        return unlocks.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Distinct levels at which any picked class gains a spell — the
    /// milestone scan's spell half.</summary>
    public IEnumerable<int> LevelsFor(IReadOnlyCollection<string> classes) =>
        _spells.SelectMany(s => s.Classes)
            .Where(c => classes.Contains(c.Class, StringComparer.OrdinalIgnoreCase))
            .Select(c => c.Level).Distinct();

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static SpellLevelCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.SpellLevels.json")
            ?? throw new InvalidOperationException("SpellLevels.json missing from resources");
        var root = JsonSerializer.Deserialize<Root>(stream, JsonOpts)
            ?? throw new InvalidOperationException("SpellLevels.json unreadable");
        return new SpellLevelCatalog(root.Spells);
    }

    private static readonly Lazy<SpellLevelCatalog> Embedded = new(LoadEmbedded);
    public static SpellLevelCatalog Default => Embedded.Value;
}
