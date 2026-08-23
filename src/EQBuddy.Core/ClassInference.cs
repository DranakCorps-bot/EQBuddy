namespace EQBuddy.Core;

/// <summary>How strong one piece of class evidence is, which is a question about what
/// could have produced the line rather than about the class.</summary>
public enum ClassSignalKind
{
    /// <summary>A skill, discipline or AA — something only that class HAS. No item lends
    /// you Backstab or Harm Touch, so a single ability stands on its own.</summary>
    Ability,

    /// <summary>A spell only one class learns. Anyone can carry a clicky that casts one,
    /// so one spell is never enough by itself — see
    /// <see cref="ClassInference.DistinctSpellFloor"/>.</summary>
    Spell,
}

/// <summary>One name's verdict: the class it belongs to, and how it can be faked.</summary>
public readonly record struct ClassSignal(string Class, ClassSignalKind Kind);

/// <summary>
/// Every name in the shipped catalogs that belongs to exactly ONE class, so a log line
/// naming it is evidence about who is playing.
///
/// Built from the wiki harvests EQBuddy already ships — <see cref="AaCatalog"/> (144
/// abilities, each with its class) and <see cref="SpellLevelCatalog"/> (1,428 spells with
/// per-class levels) — plus a small curated table of melee skills the log names by verb
/// and no catalog carries. Deriving beats hand-typing here: the weekly refresh keeps the
/// harvests current, a hand list would rot, and the spell catalog is the only place a
/// CASTER's evidence could have come from at all (#120).
///
/// **Shared is not evidence.** A name is kept only when every catalog that mentions it
/// agrees on a single class, so Gate, Superior Healing and Divine Aura are all absent —
/// the union is taken across sources precisely so a name that one harvest happens to
/// list under one class is still dropped when another shows it shared.
/// </summary>
public static class ClassSignalCatalog
{
    /// <summary>Melee skills the log names through its own verbs ("You backstab …",
    /// "frenzies on"), and the touches, which arrive as damage sources rather than casts.
    /// Curated because no shipped catalog lists them, and authoritative because they were
    /// checked by hand: a derived row never overrides one of these.</summary>
    private static readonly Dictionary<string, string> Curated = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Backstab"] = "Rogue",
        ["Harm Touch"] = "Shadow Knight",
        ["Lay on Hands"] = "Paladin",
        ["Lay Hands"] = "Paladin",
        ["Flying Kick"] = "Monk", ["Round Kick"] = "Monk", ["Tiger Claw"] = "Monk",
        ["Eagle Strike"] = "Monk", ["Dragon Punch"] = "Monk",
        ["Frenzy"] = "Berserker",
    };

    private static readonly Lazy<Dictionary<string, ClassSignal>> Map = new(Build);

    private static Dictionary<string, ClassSignal> Build()
    {
        // name → every class any catalog attaches to it. Two entries and the name is out.
        var classes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var fromAa = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var known = new HashSet<string>(QuestClassFilter.Classes, StringComparer.OrdinalIgnoreCase);

        void Note(string name, string? cls, bool ability)
        {
            // A class the app doesn't know is a harvest typo, not a sixteenth-and-a-half
            // class: it could never reach the quest lens, so it must not silently win a
            // vote either.
            if (name.Length == 0 || cls is not { Length: > 0 } || !known.Contains(cls)) return;
            if (!classes.TryGetValue(name, out var set))
                classes[name] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(cls);
            if (ability) fromAa.Add(name);
        }

        foreach (var aa in AaCatalog.All) Note(aa.Name, aa.Class, ability: true);
        foreach (var spell in SpellLevelCatalog.Default.All)
            foreach (var c in spell.Classes)
                Note(spell.Name, c.Class, ability: false);

        var map = new Dictionary<string, ClassSignal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, set) in classes)
        {
            if (set.Count != 1) continue;
            map[name] = new ClassSignal(set.First(),
                fromAa.Contains(name) ? ClassSignalKind.Ability : ClassSignalKind.Spell);
        }
        foreach (var (name, cls) in Curated) map[name] = new ClassSignal(cls, ClassSignalKind.Ability);
        return map;
    }

    /// <summary>How many names carry a class. Loaded once, then every lookup is a hash
    /// hit — this runs against a live log and must never become a scan.</summary>
    public static int Count => Map.Value.Count;

    /// <summary>The class a spell or ability name proves, or null when the name is shared,
    /// unknown, or not a class marker at all (the common case, by far).</summary>
    public static ClassSignal? Find(string name) =>
        name.Length > 0 && Map.Value.TryGetValue(name, out var s) ? s : null;

    /// <summary>Classes that can produce evidence at all. The bug in #120 was that this
    /// was the melee half of the roster; a test now holds it at all sixteen.</summary>
    public static IReadOnlySet<string> ClassesWithSignals =>
        Map.Value.Values.Select(v => v.Class).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Which class the log looks like, from class-unique lines, weighted towards what you are
/// doing NOW.
///
/// Frankthetankk (#120) found the first version structurally one-sided: every signal it
/// knew was a melee skill, so a caster who once produced a melee-ish line was labelled
/// that class for the rest of the session — no contrary evidence could exist, because
/// nothing a caster does was in the table. Two things fix that, and both are needed:
///
/// 1. **Casters get signals**, from the shipped spell catalog (<see cref="ClassSignalCatalog"/>).
/// 2. **Recency**, here: every sighting decays with a half-life, so the current character
///    overtakes the previous one instead of arguing with an hour-old ballot forever.
///
/// Three guards keep the answer honest, because the reading drives the quest lens, the
/// buff-set fallback and level filtering, and a confident wrong answer costs more there
/// than "don't know" — the UI always says "(inferred)", never states it as fact:
/// a sighting floor, corroboration for anything an item could have cast, and a lead
/// margin below which no class is named at all.
///
/// Not thread-safe on its own: <see cref="SessionStats"/> owns the only instance and
/// touches it under its own lock.
/// </summary>
public sealed class ClassInference
{
    /// <summary>Sightings halve in weight every ten minutes of LOG time (never wall
    /// clock — a replayed log must infer what it inferred live).
    ///
    /// Ten minutes is the compromise: short enough that an hour-old handful of lines is
    /// worth 1/64 of what you are casting now, long enough that a class whose only tells
    /// are on long reuse timers (a twenty-minute Harm Touch) still holds a lead it has
    /// earned. Uniform decay never changes the ORDER on its own — a med break, a corpse
    /// run or an AFK cannot flip a reading, only new evidence can.</summary>
    public static readonly TimeSpan HalfLife = TimeSpan.FromMinutes(10);

    /// <summary>Sightings before a class is eligible at all. Counted raw and never
    /// decayed: the floor exists to stop ONE line relabelling a character, and if it
    /// decayed, a character who played for an hour and then stood in the bazaar would
    /// slowly lose an inference it had properly earned.</summary>
    public const int EvidenceFloor = 3;

    /// <summary>Distinct class-unique SPELLS a class needs when no ability backs it up.
    /// One clicky is one spell; a spellbook is many. This is what stops a warrior with a
    /// borrowed wizard nuke on an item from reading as a wizard — a real wizard clears it
    /// in his first two casts.</summary>
    public const int DistinctSpellFloor = 2;

    /// <summary>
    /// How much of the leader's weight a class needs to stay in the list — not to BEAT
    /// it. A class earns its place against the floors above; this only drops a class the
    /// log has stopped playing.
    ///
    /// **It replaces a winner-takes-all margin that was wrong about the game.** Until
    /// 2026-08-23 this was <c>LeadMargin = 2.0</c>: the leader had to be twice the
    /// runner-up or the answer was <c>""</c>, documented as *"two qualifying classes at
    /// comparable weight is a genuinely ambiguous log, and the honest answer there is no
    /// answer."* In EverQuest Legends that is not ambiguity — **a character is up to three
    /// classes at once** (David, 2026-08-23: *"you seem to think EQ Legends just lets you
    /// have 1 class when in fact you can be 3 at a time"*), so a correctly-played
    /// Warrior/Druid/Monk produced strong evidence for three classes, none cleared the
    /// margin, and the app concluded it did not know. The one case the rule protected
    /// against (#120: a caster wearing a melee class after one melee-ish line) and the
    /// ordinary case of a three-class character were indistinguishable to it.
    ///
    /// **#120's protection does not depend on this and never did** — a single melee-ish
    /// line is ONE sighting against <see cref="EvidenceFloor"/> of three. What the margin
    /// actually guarded was the alt-swap (two characters in one log), and decay already
    /// handles that: an old class's weight halves every ten minutes and falls under this
    /// fraction within the hour.
    /// </summary>
    public const double MemberFraction = 0.25;

    /// <summary>The most classes a character can hold. **The wiki's number, not ours** —
    /// eqlwiki's `Character Classes`: *"EverQuest Legends also allows players to mix
    /// classes, creating custom class combinations and trio builds."* Departing from the
    /// wiki on game truth needs decisive evidence (CLAUDE.md), and inventing a cap would
    /// be exactly that departure.</summary>
    public const int MaxClasses = 3;

    private sealed class Tally
    {
        public double Weight;
        public int Sightings;
        public readonly HashSet<string> Abilities = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Spells = new(StringComparer.OrdinalIgnoreCase);

        public bool Qualifies => Sightings >= EvidenceFloor
            && (Abilities.Count > 0 || Spells.Count >= DistinctSpellFloor);
    }

    private readonly Dictionary<string, Tally> _tallies = new(StringComparer.OrdinalIgnoreCase);
    // Every weight in the table is decayed to this instant, so a read needs no time of
    // its own and two weights are always comparable.
    private DateTime? _asOf;

    /// <summary>Forget everything — a character switch starts a fresh ballot.</summary>
    public void Clear()
    {
        _tallies.Clear();
        _asOf = null;
    }

    /// <summary>A cast we started. Spells and abilities both arrive here; a clicky is
    /// indistinguishable from a memmed spell in the log, which is exactly why spell
    /// evidence needs corroboration.</summary>
    public void RecordCast(string spell, DateTime at)
    {
        if (ClassSignalCatalog.Find(spell) is { } signal) Record(signal, spell, at);
    }

    /// <summary>A song we sang. Only bards sing — the parser separates "begin to sing"
    /// from "begin casting" — so the song's own name never has to be class-unique.</summary>
    public void RecordSong(string song, DateTime at) =>
        Record(new ClassSignal("Bard", ClassSignalKind.Ability), song, at);

    /// <summary>An ability named as the SOURCE of damage we dealt ("… by Harm Touch",
    /// "You backstab …"). Ability-grade names only: a damage line is also what an item
    /// proc looks like, and a proc is a fact about the weapon, not about the wielder.</summary>
    public void RecordAbilityUse(string source, DateTime at)
    {
        if (ClassSignalCatalog.Find(source) is { Kind: ClassSignalKind.Ability } signal)
            Record(signal, source, at);
    }

    private void Record(ClassSignal signal, string source, DateTime at)
    {
        Decay(at);
        if (!_tallies.TryGetValue(signal.Class, out var tally))
            _tallies[signal.Class] = tally = new Tally();
        tally.Weight += 1;
        tally.Sightings++;
        (signal.Kind == ClassSignalKind.Ability ? tally.Abilities : tally.Spells).Add(source);
    }

    private void Decay(DateTime at)
    {
        if (_asOf is not { } since) { _asOf = at; return; }
        // Log lines share a second constantly and a re-ingest can hand us an older one;
        // neither may run the clock backwards and inflate what is already banked.
        if (at <= since) return;
        var factor = Math.Pow(0.5, (at - since).TotalMinutes / HalfLife.TotalMinutes);
        foreach (var tally in _tallies.Values) tally.Weight *= factor;
        _asOf = at;
    }

    /// <summary>
    /// Every class the log currently looks like, heaviest first, at most
    /// <see cref="MaxClasses"/> — empty for "don't know", which is a real answer here
    /// rather than a failure.
    ///
    /// **A LIST, because a Legends character is one** (David, 2026-08-23). Each class
    /// argues for itself against <see cref="EvidenceFloor"/> and
    /// <see cref="DistinctSpellFloor"/>; <see cref="MemberFraction"/> then removes only
    /// what the log has stopped playing. That is the trap-11 rule kept intact for a list:
    /// every outcome this can name still has a way to be named — the catalog derives
    /// signals for all sixteen classes — and a way to STOP being named, which is decay
    /// plus the fraction rather than a rival's success.
    ///
    /// Ordered by decayed weight so the first entry is what you are playing most right
    /// now, which is what a surface showing one class should show. Ties break on the class
    /// NAME rather than on dictionary order: two classes at identical weight is common on
    /// a fresh log, and which one a quest filter picks must not depend on insertion order.
    /// </summary>
    public IReadOnlyList<string> CurrentClasses()
    {
        var qualifying = _tallies
            .Where(kv => kv.Value.Qualifies)
            .OrderByDescending(kv => kv.Value.Weight)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (qualifying.Count == 0) return [];

        var leader = qualifying[0].Value.Weight;
        return [.. qualifying
            .Where(kv => kv.Value.Weight >= leader * MemberFraction)
            .Take(MaxClasses)
            .Select(kv => kv.Key)];
    }

    /// <summary>The heaviest class the log looks like, or "". Kept for the surfaces that
    /// genuinely want ONE class — and note it no longer means the same thing: it is now
    /// "the class you are playing most", where it used to be "the class I am confident
    /// enough about to name at all". Anything asking "what is this character" wants
    /// <see cref="CurrentClasses"/>.</summary>
    public string Current() => CurrentClasses().FirstOrDefault() ?? "";
}
