namespace EQBuddy.Core;

/// <summary>A finalized fight against one creature (ENCOUNTER-*).</summary>
public sealed record EncounterInfo(
    string Name, DateTime Start, double DurationSeconds,
    long DamageOut, long DamageIn, double Dps, string Outcome, long Healed = 0)
{
    // Init-properties rather than positional params: sessions archived before these
    // existed deserialize them as empty lists, and the History window shows the fight
    // row without an expandable breakdown instead of failing.
    /// <summary>Your damage in this fight by ability, pet rows included.</summary>
    public List<SourceDamage> ByAbility { get; init; } = [];
    public List<SourceDamage> HealsBySpell { get; init; } = [];
    /// <summary>What the creature hit YOU with, by attack skill or spell.</summary>
    public List<SourceDamage> ByIncoming { get; init; } = [];
    /// <summary>The pet's share of ByAbility split by the pet's own ability — sessions
    /// archived before this existed deserialize it empty, same as the lists above.</summary>
    public List<SourceDamage> PetAbilities { get; init; } = [];
}

/// <summary>
/// The fight to show at the top of the Combat and Healing cards: the one you're in if you're
/// in one, otherwise the last one that finished. <see cref="InProgress"/> says which, because
/// the numbers mean different things — a fight still running has no final duration, and its
/// DPS is a running figure rather than a result.
/// </summary>
public sealed record LastFightInfo(
    string Name, double DurationSeconds, long DamageOut, long DamageIn, long Healed,
    double Dps, double Hps, string Outcome, bool InProgress,
    List<SourceDamage> ByAbility, List<SourceDamage> HealsBySpell,
    List<SourceDamage> ByIncoming)
{
    /// <summary>The per-creature fights inside this pull — the cards show a
    /// per-creature damage split when there's more than one.</summary>
    public IReadOnlyList<EncounterInfo> Fights { get; init; } = [];
    /// <summary>Pet damage in this pull/fight by pet ability (empty when no pet acted).</summary>
    public List<SourceDamage> PetAbilities { get; init; } = [];
    /// <summary>When the pull began — the fight timeline's time origin. MinValue on
    /// snapshots built before the field existed (the timeline just declines to open).</summary>
    public DateTime Start { get; init; }
}

/// <summary>
/// A pull: one or more per-creature fights whose activity overlapped or sat within
/// <see cref="EncounterGrouping.PullGap"/> of each other. This is the unit the History
/// fight review shows — "the encounter" as a player experiences it, adds included —
/// while the per-creature fights underneath keep powering farming stats and kill
/// correlation untouched.
/// </summary>
public sealed record PullInfo(
    string Title, DateTime Start, double DurationSeconds,
    long DamageOut, long DamageIn, long Healed, double Dps,
    IReadOnlyList<EncounterInfo> Fights,
    List<SourceDamage> ByAbility, List<SourceDamage> ByIncoming, List<SourceDamage> HealsBySpell)
{
    /// <summary>Pet damage across the pull by pet ability (empty when no pet acted).</summary>
    public List<SourceDamage> PetAbilities { get; init; } = [];
}

public static class EncounterGrouping
{
    /// <summary>Quiet time that separates one pull from the next — matches the combat
    /// window's close gap: if combat would have closed, the next fight is a new pull.</summary>
    public static readonly TimeSpan PullGap = TimeSpan.FromSeconds(10);

    /// <summary>Groups per-creature fights (oldest first) into pulls. Incoming rows are
    /// prefixed with the creature's name when a pull has more than one, so "Damage you
    /// took" keeps saying who did it; your own damage rows merge by ability.</summary>
    public static List<PullInfo> Group(IReadOnlyList<EncounterInfo> fights, TimeSpan? gap = null)
    {
        var g = gap ?? PullGap;
        var pulls = new List<PullInfo>();
        var current = new List<EncounterInfo>();
        var currentEnd = DateTime.MinValue;
        foreach (var f in fights.OrderBy(x => x.Start))
        {
            if (current.Count > 0 && f.Start > currentEnd + g)
            {
                pulls.Add(Build(current));
                current = [];
            }
            current.Add(f);
            var end = f.Start.AddSeconds(f.DurationSeconds);
            if (end > currentEnd) currentEnd = end;
        }
        if (current.Count > 0) pulls.Add(Build(current));
        return pulls;
    }

    private static PullInfo Build(List<EncounterInfo> fights)
    {
        var start = fights[0].Start;
        var end = fights.Max(f => f.Start.AddSeconds(f.DurationSeconds));
        var dur = Math.Max(1, (end - start).TotalSeconds);
        var multi = fights.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
        var title = string.Join(" + ", fights.GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g2 => g2.Count() > 1 ? $"{g2.Key} ×{g2.Count()}" : g2.Key));
        var dmgOut = fights.Sum(f => f.DamageOut);
        return new PullInfo(title, start, dur, dmgOut,
            fights.Sum(f => f.DamageIn), fights.Sum(f => f.Healed), dmgOut / dur, fights,
            Merge(fights.SelectMany(f => f.ByAbility)),
            Merge(fights.SelectMany(f => multi
                ? f.ByIncoming.Select(r => r with { Name = $"{f.Name}: {r.Name}" })
                : f.ByIncoming)),
            Merge(fights.SelectMany(f => f.HealsBySpell)))
        { PetAbilities = Merge(fights.SelectMany(f => f.PetAbilities)) };
    }

    private static List<SourceDamage> Merge(IEnumerable<SourceDamage> rows) =>
        rows.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SourceDamage(g.First().Name, g.Sum(r => r.Hits), g.Sum(r => r.Total),
                g.Sum(r => r.Crits), g.Sum(r => r.ActiveSeconds))
            {
                // Min of the fights that saw a hit (0 = never hit, not "hit for 0").
                MinHit = g.Where(r => r.MinHit > 0).Select(r => r.MinHit).DefaultIfEmpty(0).Min(),
                MaxHit = g.Max(r => r.MaxHit),
                Misses = g.Sum(r => r.Misses),
            })
            .OrderByDescending(r => r.Total)
            .ToList();
}

/// <summary>LastAt is when this item last dropped (log time) — the wiki contribution
/// pack includes it so contributors can find the moment in their own log again
/// (Frankthetankk's ask, #65). Null on records from before the field existed.</summary>
public sealed record MobLoot(string Item, int Count, double? DropRatePct)
{
    public DateTime? LastAt { get; init; }
}

/// <summary>One faction's response to killing this creature: the per-kill delta and
/// how many hits were recorded (vs. the kill count, absence is visible too).</summary>
public sealed record MobFactionHit(string Faction, int Delta, int Hits);

/// <summary>Per-creature farming aggregate (MOB-*). Drop rates are observed personal
/// rates — the kill denominator is always surfaced next to the percentage.
/// Zone/CoinMin/CoinMax/Factions are the stat-block trio (#65): zone at kill time,
/// per-kill coin-drop bounds, and faction hits with confirmed-absence visibility.</summary>
public sealed record MobSummary(
    string Name, int Kills, int Encounters, double AvgFightSeconds,
    double XpPercent, long Copper, List<MobLoot> Loot)
{
    public string Zone { get; init; } = "";
    /// <summary>Smallest/largest single coin drop; -1 min = no coin seen.</summary>
    public long CoinMin { get; init; } = -1;
    public long CoinMax { get; init; }
    public List<MobFactionHit> Factions { get; init; } = [];
    /// <summary>Level bounds from /consider lines (#65); 0 min = never conned.
    /// Sessions archived before this existed deserialize as unknown, same as above.</summary>
    public int LevelMin { get; init; }
    public int LevelMax { get; init; }
    /// <summary>/consider count, and how many of those the GAME called "a rare creature"
    /// (#217 ask 3). 0 = never conned this session, which is not the same as "not rare" —
    /// the pack says nothing at all rather than implying an absence it never observed.
    /// Sessions archived before this existed deserialize as 0, same as the level bounds.</summary>
    public int Considers { get; init; }
    public int RareConsiders { get; init; }
}

/// <summary>Combat time and damage while a stance was active (STANCE-*).</summary>
public sealed record StanceInfo(string Name, double CombatSeconds, long Damage, double Dps);

/// <summary>An owned AA ability: highest rank observed in the log and when it was bought.
/// The ledger feeds duration models — an AA can lengthen buffs or mez far past the spell's
/// base numbers, and "why does MY mez run long" starts here.</summary>
public sealed record AaAbilityInfo(string Name, int Rank, DateTime Time);

/// <summary>A spell observed hitting several creatures at once, measured per cast.
/// AvgTargets below MaxTargets means some casts caught fewer creatures than the best one —
/// the gap between them is how much AoE value is being left on the table by pull size.</summary>
public sealed record AreaSpellInfo(
    string Name, int Casts, double AvgTargets, int MaxTargets, long Damage, double DamagePerCast);
