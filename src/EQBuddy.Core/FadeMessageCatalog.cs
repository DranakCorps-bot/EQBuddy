using System.Reflection;
using System.Text.Json;

namespace EQBuddy.Core;

/// <summary>
/// Buff and HoT wear-off messages (FADE-001). Mez and charm fades name their spell
/// ("Your Mesmerize spell has worn off of a gnoll.") — but buffs and HoTs fade with
/// spell-specific flavor text that names NOTHING: "The echo of healing fades away."
/// is Echoing Light ending, "Your speed returns to normal." is a haste dropping.
/// Reported by an enchanter on Reddit whose HoT/haste fade rules never fired — no
/// rule can match a spell name the log never prints. This catalog maps each known
/// wear-off line to its candidate spells so SpellFade rules can fire on them.
///
/// A message can belong to several spells (every haste in the game shares one line),
/// so entries carry a candidate list plus a display label ("Haste"). Sources:
/// eqlwiki + classic spell pages, seeded from lines observed in real Legends logs;
/// entries whose wiki pages left the wear-off field blank (Flowering Heal) simply
/// are not here — those spells appear to fade silently, and a delay-cue rule is the
/// honest tool for them.
/// </summary>
public sealed class FadeMessageCatalog
{
    public sealed class Entry
    {
        public string Message { get; set; } = "";
        public string[] Spells { get; set; } = [];
        public string Label { get; set; } = "";
        public string Category { get; set; } = "";
    }

    private readonly Dictionary<string, Entry> _byMessage;
    private readonly Dictionary<string, Entry> _bySpell;
    private readonly string[] _buffSpellChoices;

    /// <summary>
    /// Spell titles a player watches as their own buff even though the wear-off
    /// line is category Other. Discussion #710 (TheOneGargoyle): Shroud of Hate
    /// and Shroud of Pain steal ATK and AC onto the caster (eqlwiki), but each
    /// fade line is shared with the lower-level scream of the same name, so
    /// fades-harvest.py marks the line Other and the beneficial filter leaves
    /// the titles out of the watch picker. The lines themselves are already
    /// catalogued — "The hatred departs." and "The pain subsides." Recategorizing
    /// the line would also offer Scream of Hate and Scream of Pain on every
    /// Buff-class watch. A name is admitted only when the catalog already
    /// carries it, so a harvest that drops the spell cannot leave a dead row.
    /// </summary>
    private static readonly string[] WatchBuffNamesDespiteSharedLine =
    [
        "Shroud of Hate",
        "Shroud of Pain",
    ];

    public FadeMessageCatalog(IEnumerable<Entry> entries)
    {
        var list = entries.Where(e => e.Message.Length > 0).ToList();
        _byMessage = list.ToDictionary(e => e.Message, e => e, StringComparer.OrdinalIgnoreCase);
        _bySpell = list
            .SelectMany(e => e.Spells.Select(spell => (Spell: SpellCatalog.BaseName(spell), Entry: e)))
            .Where(x => x.Spell.Length > 0)
            .GroupBy(x => x.Spell, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Entry, StringComparer.OrdinalIgnoreCase);
        var choices = list
            .Where(e => IsBeneficialCategory(e.Category))
            .SelectMany(e => e.Spells.Append(e.Label))
            .Select(SpellCatalog.BaseName)
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in WatchBuffNamesDespiteSharedLine)
        {
            var key = SpellCatalog.BaseName(name);
            if (_bySpell.ContainsKey(key)) choices.Add(key);
        }
        _buffSpellChoices = choices
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public int Count => _byMessage.Count;

    public IEnumerable<Entry> Entries => _byMessage.Values;

    public IReadOnlyList<string> BuffSpellChoices => _buffSpellChoices;

    public Entry? Find(string message) =>
        _byMessage.TryGetValue(message, out var e) ? e : null;

    public Entry? FindBySpell(string spell) =>
        _bySpell.TryGetValue(SpellCatalog.BaseName(spell), out var e) ? e : null;

    public static bool IsBeneficialCategory(string category) => category is
        "Buff" or "StatBuff" or "Protection" or "Haste" or "Movement" or "Clarity"
        or "Regen" or "DamageShield" or "Rune" or "Illusion" or "Invisibility"
        or "HealOverTime";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static FadeMessageCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EQBuddy.Core.Data.FadeMessages.json")
            ?? throw new InvalidOperationException("FadeMessages.json missing from resources");
        var entries = JsonSerializer.Deserialize<List<Entry>>(stream, JsonOpts) ?? [];
        return new FadeMessageCatalog(entries);
    }

    /// <summary>Shared instance for the parser's per-line lookups.</summary>
    public static FadeMessageCatalog Default { get; } = LoadEmbedded();
}
