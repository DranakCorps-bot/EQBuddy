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

    public FadeMessageCatalog(IEnumerable<Entry> entries)
    {
        var list = entries.Where(e => e.Message.Length > 0).ToList();
        _byMessage = list.ToDictionary(e => e.Message, e => e, StringComparer.OrdinalIgnoreCase);
        _bySpell = list
            .SelectMany(e => e.Spells.Select(spell => (Spell: SpellCatalog.BaseName(spell), Entry: e)))
            .Where(x => x.Spell.Length > 0)
            .GroupBy(x => x.Spell, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Entry, StringComparer.OrdinalIgnoreCase);
        _buffSpellChoices = list
            .Where(e => IsBeneficialCategory(e.Category))
            .SelectMany(e => e.Spells.Append(e.Label))
            .Select(SpellCatalog.BaseName)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
