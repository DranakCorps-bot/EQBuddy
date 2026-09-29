namespace EQBuddy.Core;

/// <summary>
/// WHAT KIND of damage or healing a meter row is (Founder, 2026-09-29: "each damage type could
/// have its own color … HPS could break down similarly HOTs, burst heals"). Stamped on the row
/// where the LOG LINE is read — <see cref="SessionStats"/> knows the line's shape there — and
/// never guessed afterwards from a row's name.
///
/// **<see cref="Other"/> is zero on purpose**: a session archived before this existed
/// deserializes every row as Other and draws grey, which is honest; a guess from the name
/// would colour an old "Stinging Swarm" row (DoT and direct hits merged) as one or the other.
/// </summary>
public enum OutputKind
{
    /// <summary>Unknown — an archived session, or a row nothing classified.</summary>
    Other = 0,
    /// <summary>Your auto-attack: the melee verbs that are not an activated skill.</summary>
    Melee,
    /// <summary>An activated melee skill — <see cref="OutputKinds.Skills"/>, by name.</summary>
    Skill,
    /// <summary>Archery and throwing.</summary>
    Ranged,
    /// <summary>A spell's direct hit ("… damage by Burst of Flame", or unnamed non-melee).</summary>
    Spell,
    /// <summary>A damage-over-time tick ("has taken N damage from your X").</summary>
    DoT,
    /// <summary>YOUR damage shield ("is pierced by YOUR thorns").</summary>
    DamageShield,
    /// <summary>Spell damage whose every hit had no cast of that spell before it — the
    /// proc heuristic <see cref="SessionStats"/> already runs, kept as a heuristic.</summary>
    Proc,
    /// <summary>Your pet's damage.</summary>
    Pet,
    /// <summary>A direct heal — any outgoing heal line without "over time".</summary>
    Heal,
    /// <summary>A heal-over-time tick — the log's own "healed … over time".</summary>
    HoT,
}

/// <summary>The classification rules that are tables rather than line shapes.</summary>
public static class OutputKinds
{
    /// <summary>What a DoT tick's row is called: the spell, marked — so a spell that both
    /// hits and ticks is two rows, each one kind, instead of one row that is neither.</summary>
    public const string DotSuffix = " (DoT)";

    /// <summary>What a heal-over-time tick's row is called, for the same reason.</summary>
    public const string HotSuffix = " (HoT)";

    /// <summary>
    /// The ACTIVATED melee skills, by the name <see cref="SessionStats"/> files them under.
    /// Curated, because the log writes "You kick X" exactly as it writes "You crush X": the
    /// verb is the only tell. Deliberately short: "strike", "claw" and "punch" are also
    /// ordinary auto-attack verbs for some classes, so they stay Melee rather than be
    /// guessed; a monk's special only becomes a Skill once the game NAMES it (the "You will
    /// now use …" substitution line that turns Kick into Round Kick).
    /// </summary>
    public static readonly IReadOnlySet<string> Skills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Kick", "Round Kick", "Flying Kick", "Bash", "Slam", "Backstab", "Frenzy",
        "Tiger Claw", "Eagle Strike", "Dragon Punch",
    };

    /// <summary>The ranged skills, by the same names.</summary>
    public static readonly IReadOnlySet<string> RangedSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Archery", "Throwing",
    };

    /// <summary>
    /// The ROW a damage line lands in: its source name, except that a DoT tick is
    /// "&lt;Spell&gt; (DoT)" (2026-09-29) — the log marks every tick, and a spell that both hits
    /// and ticks used to be one row that was neither kind, so it could be coloured as neither.
    /// </summary>
    /// <param name="name">The source as <see cref="SessionStats"/> files it (a melee skill
    /// already substituted — Kick → Round Kick).</param>
    public static string RowName(DamageDealtEvent dd, string name) =>
        dd.Kind == DamageKind.Spell && dd.OverTime && !dd.IsAux ? name + DotSuffix : name;

    /// <summary>The kind one damage line is, from its shape: your damage shield, a melee skill
    /// by the tables above, a DoT tick, a proc (the caller's heuristic said so), else a spell.
    /// </summary>
    public static OutputKind Of(DamageDealtEvent dd, string rowName, bool procHit) =>
        dd.IsAux ? OutputKind.DamageShield
        : dd.Kind == DamageKind.Melee ? ForMelee(rowName)
        : dd.OverTime ? OutputKind.DoT
        : procHit ? OutputKind.Proc
        : OutputKind.Spell;

    /// <summary>A heal line's row and kind: a HoT tick is "&lt;Spell&gt; (HoT)", for the DoT
    /// split's reason (Budding Heal both heals and ticks).</summary>
    public static (string Row, OutputKind Kind) HealRow(string spell, bool overTime) =>
        overTime ? (spell + HotSuffix, OutputKind.HoT) : (spell, OutputKind.Heal);

    /// <summary>
    /// A row's kind after one more hit. The first hit decides; the ONE later change is a Proc
    /// row meeting a hit that followed your own cast of that spell, which makes it a Spell row —
    /// the proc heuristic holds only while nothing contradicts it.
    /// </summary>
    public static OutputKind Merge(OutputKind row, OutputKind hit) =>
        row == OutputKind.Other || (row == OutputKind.Proc && hit == OutputKind.Spell) ? hit : row;

    /// <summary>A melee row's kind from its skill name.</summary>
    public static OutputKind ForMelee(string skill) =>
        Skills.Contains(skill) ? OutputKind.Skill
        : RangedSkills.Contains(skill) ? OutputKind.Ranged
        : OutputKind.Melee;
}
