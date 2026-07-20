namespace EQBuddy.Core;

/// <summary>
/// Thread-safe aggregator for one play session. A "play session" is a contiguous
/// run of log activity; a gap of >= SessionGap between log timestamps starts a new one.
/// </summary>
public sealed class SessionStats
{
    public static readonly TimeSpan SessionGap = TimeSpan.FromMinutes(60);
    // Combat stays "live" while ANY nearby combat signal arrives within this window:
    // your hits/misses, damage you take, group members hitting or being hit, kills.
    // This keeps slow-swinging melee and medding casters honest: time between your own
    // attacks still counts as in-combat while the fight rages, but true downtime
    // (nobody hitting anybody) never dilutes DPS.
    private static readonly TimeSpan CombatGap = TimeSpan.FromSeconds(10);
    // Bystander activity may keep the clock alive only this long after the player's
    // (or their pet's) last own action — brief participation in a group fight must not
    // inherit the whole fight's duration.
    private static readonly TimeSpan BystanderGrace = TimeSpan.FromSeconds(20);

    private readonly object _lock = new();

    private DateTime? _sessionStart;
    private DateTime? _lastEventTime;

    private readonly Dictionary<string, int> _yourKills = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _partyKillsByTarget = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _partyKillsByKiller = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(DateTime Time, string Killer)> _deaths = new();

    private long _damageDealt, _meleeDamage, _spellDamage;
    private int _hitCount, _critCount, _missCount;
    private int _maxHit; private string _maxHitDesc = "";
    private readonly Dictionary<string, (int Count, long Total)> _damageBySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _specialHits = new(StringComparer.OrdinalIgnoreCase);

    private long _damageTaken;
    private int _avoidedIncoming;
    private int _meleeHitsTaken;
    private readonly Dictionary<string, (int Count, long Total)> _damageByAttacker = new(StringComparer.OrdinalIgnoreCase);

    private long _healingDone; private int _healCount;
    private long _healingReceived;
    private readonly Dictionary<string, (int Count, long Total)> _healsByHealer = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Count, long Total)> _healsBySpell = new(StringComparer.OrdinalIgnoreCase);
    private int _regenTicks;
    private string? _characterName;

    /// <summary>The watched character's name — needed to recognize self-heals
    /// ("You healed Douglas ..." appears in Douglas's own log).</summary>
    public string? CharacterName
    {
        get { lock (_lock) return _characterName; }
        set { lock (_lock) _characterName = value; }
    }

    private readonly Dictionary<string, (int Count, string LastSource)> _loot = new(StringComparer.OrdinalIgnoreCase);
    private int _lootCount;
    private readonly Dictionary<string, int> _crafted = new(StringComparer.OrdinalIgnoreCase);

    private long _copper; private int _coinDrops; private long _biggestDrop;
    private long _vendorCopper; private int _salesCount;
    private readonly Dictionary<string, (int Count, long Copper)> _soldItems = new(StringComparer.OrdinalIgnoreCase);

    private double _xpPercent; private int _xpTicks;
    private double _xpSinceLevel;
    private int _aaGained; private int _aaTotal;
    private readonly List<(DateTime Time, int Level)> _levels = new();

    private readonly Dictionary<string, (int Ups, int Value)> _skills = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Hits, int Net)> _faction = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(DateTime Time, string Zone)> _zones = new();
    private int _fizzles, _resists;

    // Combat-window tracking for DPS
    private double _closedCombatSeconds; private long _closedCombatDamage;
    private DateTime? _combatStart; private DateTime? _combatLast; private long _combatDamage;
    private DateTime? _lastOwnAction;
    private string? _petName;        // normalized (article stripped, capitalized)
    private bool _petConfirmed;      // false = blink-only (charm suspected, no "Master" tell yet)

    public event Action? SessionRolledOver;

    public void Apply(GameEvent e)
    {
        lock (_lock)
        {
            if (_lastEventTime is { } last && e.Time - last >= SessionGap)
            {
                ResetLocked();
                SessionRolledOver?.Invoke();
            }
            _sessionStart ??= e.Time;
            _lastEventTime = e.Time;

            switch (e)
            {
                case KillEvent k when k.Killer == "You" || IsPet(k.Killer):
                    Bump(_yourKills, k.Target);
                    TrackCombat(k.Time);
                    break;
                case KillEvent k:
                    Bump(_partyKillsByTarget, k.Target);
                    Bump(_partyKillsByKiller, k.Killer);
                    TrackCombat(k.Time, canStart: false);
                    break;
                case PetClaimEvent pc:
                    ConfirmPet(LogParser.Normalize(pc.PetName));
                    TrackCombat(pc.Time);
                    break;
                case PetBlinkEvent pb:
                    // Charm just landed (probably) — provisionally claim this creature.
                    _petName = LogParser.Normalize(pb.Name);
                    _petConfirmed = false;
                    break;
                case ThirdMeleeEvent tm when IsPet(tm.Attacker):
                    AddPetDamage(tm.Time, tm.Amount, DamageKind.Melee, tm.Target);
                    break;
                case ThirdDotEvent td when IsPet(td.Caster):
                    AddPetDamage(td.Time, td.Amount, DamageKind.Spell, td.Target);
                    break;
                case ThirdSchoolEvent tse when IsPet(tse.Attacker):
                    AddPetDamage(tse.Time, tse.Amount, DamageKind.Spell, tse.Target);
                    break;
                case ThirdSchoolEvent tse2:
                    TrackCombat(tse2.Time, canStart: false);
                    break;
                case ThirdMissEvent tm2 when IsPet(tm2.Attacker):
                    TrackCombat(tm2.Time);
                    break;
                case ThirdMeleeEvent tm3:
                    TrackCombat(tm3.Time, canStart: false);
                    break;
                case ThirdDotEvent td2:
                    TrackCombat(td2.Time, canStart: false);
                    break;
                case ThirdMissEvent tm4:
                    TrackCombat(tm4.Time, canStart: false);
                    break;
                case DeathEvent d:
                    _deaths.Add((d.Time, d.Killer));
                    break;
                case DamageDealtEvent dd:
                    _damageDealt += dd.Amount;
                    if (dd.Kind == DamageKind.Melee) _meleeDamage += dd.Amount; else _spellDamage += dd.Amount;
                    if (!dd.IsAux)
                    {
                        _hitCount++;
                        if (dd.Critical) _critCount++;
                        if (dd.Note is { } note && note is not ("Critical" or "Crippling Blow"))
                            Bump(_specialHits, note);
                    }
                    if (dd.Amount > _maxHit) { _maxHit = dd.Amount; _maxHitDesc = $"{dd.Source} on {dd.Target}"; }
                    var src = _damageBySource.TryGetValue(dd.Source, out var s) ? s : (0, 0L);
                    _damageBySource[dd.Source] = (src.Item1 + 1, src.Item2 + dd.Amount);
                    TrackCombat(dd.Time, dd.Amount);
                    break;
                case MissEvent { Outgoing: true } m:
                    _missCount++;
                    TrackCombat(m.Time);
                    break;
                case MissEvent m:
                    _avoidedIncoming++;
                    TrackCombat(m.Time);
                    break;
                case DamageTakenEvent dt:
                    // A "pet" attacking us means the charm broke — stop crediting it.
                    if (IsPet(dt.Attacker)) _petName = null;
                    _damageTaken += dt.Amount;
                    if (dt.Melee) _meleeHitsTaken++;
                    var atk = _damageByAttacker.TryGetValue(dt.Attacker, out var a) ? a : (0, 0L);
                    _damageByAttacker[dt.Attacker] = (atk.Item1 + 1, atk.Item2 + dt.Amount);
                    TrackCombat(dt.Time);
                    break;
                case HealEvent { Outgoing: true } h:
                    _healingDone += h.Amount; _healCount++;
                    var sp = _healsBySpell.TryGetValue(h.Spell, out var spv) ? spv : (0, 0L);
                    _healsBySpell[h.Spell] = (spv.Item1 + 1, spv.Item2 + h.Amount);
                    // Self-heals appear as "You healed <own name>" — count as received too.
                    if (_characterName is { } me &&
                        string.Equals(h.Target, me, StringComparison.OrdinalIgnoreCase))
                    {
                        _healingReceived += h.Amount;
                        var self = _healsByHealer.TryGetValue("Yourself", out var sv2) ? sv2 : (0, 0L);
                        _healsByHealer["Yourself"] = (self.Item1 + 1, self.Item2 + h.Amount);
                    }
                    TrackCombat(h.Time, canStart: false);
                    break;
                case HealEvent h:
                    _healingReceived += h.Amount;
                    if (h.Healer.Length > 0)
                    {
                        var hv = _healsByHealer.TryGetValue(h.Healer, out var hc) ? hc : (0, 0L);
                        _healsByHealer[h.Healer] = (hv.Item1 + 1, hv.Item2 + h.Amount);
                    }
                    break;
                case RegenTickEvent:
                    _regenTicks++;
                    break;
                case LootEvent l:
                    var cur = _loot.TryGetValue(l.Item, out var lv) ? lv : (0, l.Source);
                    _loot[l.Item] = (cur.Item1 + 1, l.Source);
                    _lootCount++;
                    break;
                case CraftEvent c:
                    Bump(_crafted, c.Item);
                    break;
                case MoneyEvent { Vendor: true } m:
                    _vendorCopper += m.Copper; _salesCount++;
                    if (m.Item is { } sold)
                    {
                        var sv = _soldItems.TryGetValue(sold, out var sc) ? sc : (0, 0L);
                        _soldItems[sold] = (sv.Item1 + 1, sv.Item2 + m.Copper);
                    }
                    break;
                case MoneyEvent m:
                    _copper += m.Copper; _coinDrops++;
                    if (m.Copper > _biggestDrop) _biggestDrop = m.Copper;
                    break;
                case XpEvent x:
                    _xpPercent += x.Percent; _xpTicks++;
                    _xpSinceLevel += x.Percent;
                    break;
                case LevelEvent lv2:
                    _levels.Add((lv2.Time, lv2.Level));
                    _xpSinceLevel = 0;
                    break;
                case AaEvent aa:
                    _aaGained++; _aaTotal = aa.TotalPoints;
                    break;
                case AutoSellEvent asell:
                    var lcur = _loot.TryGetValue(asell.Item, out var lval) ? lval : (0, asell.Source);
                    _loot[asell.Item] = (lcur.Item1 + asell.Count, asell.Source);
                    _lootCount += asell.Count;
                    _vendorCopper += asell.Copper; _salesCount++;
                    var scur = _soldItems.TryGetValue(asell.Item, out var sval) ? sval : (0, 0L);
                    _soldItems[asell.Item] = (scur.Item1 + asell.Count, scur.Item2 + asell.Copper);
                    break;
                case SkillUpEvent su:
                    var sk = _skills.TryGetValue(su.Skill, out var skv) ? skv : (0, 0);
                    _skills[su.Skill] = (sk.Item1 + 1, Math.Max(sk.Item2, su.Value));
                    break;
                case FactionEvent f:
                    var fv = _faction.TryGetValue(f.Faction, out var fcur) ? fcur : (0, 0);
                    _faction[f.Faction] = (fv.Item1 + 1, fv.Item2 + f.Delta);
                    break;
                case ZoneEvent z:
                    if (_zones.Count == 0 || !string.Equals(_zones[^1].Zone, z.Zone, StringComparison.OrdinalIgnoreCase))
                        _zones.Add((z.Time, z.Zone));
                    break;
                case FizzleEvent: _fizzles++; break;
                case ResistEvent: _resists++; break;
            }
        }
    }

    private bool IsPet(string name) =>
        _petName is not null &&
        string.Equals(LogParser.Normalize(name), _petName, StringComparison.OrdinalIgnoreCase);

    /// <summary>A "Master" tell proves the pet is ours — upgrade any provisional damage.</summary>
    private void ConfirmPet(string name)
    {
        _petName = name;
        if (_petConfirmed) return;
        _petConfirmed = true;
        if (_damageBySource.Remove($"Pet? ({name})", out var provisional))
        {
            var label = $"Pet ({name})";
            var cur = _damageBySource.TryGetValue(label, out var c) ? c : (0, 0L);
            _damageBySource[label] = (cur.Item1 + provisional.Count, cur.Item2 + provisional.Total);
        }
    }

    /// <summary>Pet damage is the player's damage, reported under a "Pet (Name)" source
    /// ("Pet? (Name)" while the charm is only suspected from a blink).</summary>
    private void AddPetDamage(DateTime t, int amount, DamageKind kind, string target)
    {
        _damageDealt += amount;
        if (kind == DamageKind.Melee) _meleeDamage += amount; else _spellDamage += amount;
        var label = _petConfirmed ? $"Pet ({_petName})" : $"Pet? ({_petName})";
        if (amount > _maxHit) { _maxHit = amount; _maxHitDesc = $"{label} on {target}"; }
        var src = _damageBySource.TryGetValue(label, out var s) ? s : (0, 0L);
        _damageBySource[label] = (src.Item1 + 1, src.Item2 + amount);
        TrackCombat(t, amount);
    }

    /// <summary>
    /// canStart=false marks bystander activity (group members / nearby fights): it never
    /// opens a window (idling in a busy zone isn't combat) and keeps one alive only within
    /// BystanderGrace of the player's/pet's own last action, so tagging one mob doesn't
    /// inherit the whole group fight. Own attacks, misses, pet actions, and damage taken
    /// open and extend windows freely.
    /// </summary>
    private void TrackCombat(DateTime t, int dmg = 0, bool canStart = true)
    {
        if (_combatLast is { } cl && t - cl > CombatGap)
            CloseCombatLocked();
        if (!canStart)
        {
            if (_combatStart is null) return;
            if (_lastOwnAction is not { } own || t - own > BystanderGrace) return;
        }
        else
        {
            _lastOwnAction = t;
        }
        _combatStart ??= t;
        _combatLast = t;
        _combatDamage += dmg;
    }

    private void CloseCombatLocked()
    {
        if (_combatStart is { } cs && _combatLast is { } cl)
        {
            _closedCombatSeconds += Math.Max(1, (cl - cs).TotalSeconds);
            _closedCombatDamage += _combatDamage;
        }
        _combatStart = null; _combatLast = null; _combatDamage = 0;
    }

    public void Reset()
    {
        lock (_lock) ResetLocked();
    }

    private void ResetLocked()
    {
        _sessionStart = null; _lastEventTime = null;
        _yourKills.Clear(); _partyKillsByTarget.Clear(); _partyKillsByKiller.Clear(); _deaths.Clear();
        _damageDealt = _meleeDamage = _spellDamage = 0;
        _hitCount = _critCount = _missCount = 0; _maxHit = 0; _maxHitDesc = "";
        _damageBySource.Clear(); _specialHits.Clear();
        _damageTaken = 0; _avoidedIncoming = 0; _meleeHitsTaken = 0; _damageByAttacker.Clear();
        _healingDone = 0; _healCount = 0; _healingReceived = 0;
        _healsByHealer.Clear(); _healsBySpell.Clear(); _regenTicks = 0;
        _loot.Clear(); _lootCount = 0; _crafted.Clear();
        _copper = 0; _coinDrops = 0; _biggestDrop = 0;
        _vendorCopper = 0; _salesCount = 0; _soldItems.Clear();
        _xpPercent = 0; _xpTicks = 0; _xpSinceLevel = 0; _levels.Clear();
        _aaGained = 0; _aaTotal = 0;
        _skills.Clear(); _faction.Clear(); _zones.Clear();
        _fizzles = 0; _resists = 0;
        _closedCombatSeconds = 0; _closedCombatDamage = 0;
        _combatStart = null; _combatLast = null; _combatDamage = 0;
        _lastOwnAction = null; _petName = null; _petConfirmed = false;
    }

    private static void Bump(Dictionary<string, int> d, string key) =>
        d[key] = d.TryGetValue(key, out var v) ? v + 1 : 1;

    public StatsSnapshot Snapshot()
    {
        lock (_lock)
        {
            double combatSeconds = _closedCombatSeconds;
            long combatDamage = _closedCombatDamage;
            double currentDps = 0;
            if (_combatStart is { } cs && _combatLast is { } cl)
            {
                var dur = Math.Max(1, (cl - cs).TotalSeconds);
                combatSeconds += dur;
                combatDamage += _combatDamage;
                // Only advertise a "current" DPS while the fight is actually live
                // (log timestamps are local time, so wall clock is comparable).
                if (DateTime.Now - cl <= CombatGap + TimeSpan.FromSeconds(2))
                    currentDps = _combatDamage / dur;
            }
            var sessionDps = combatSeconds > 0 ? combatDamage / combatSeconds : 0;
            var elapsed = _sessionStart is { } ss && _lastEventTime is { } le
                ? (le - ss) : TimeSpan.Zero;
            var hours = Math.Max(elapsed.TotalHours, 1.0 / 60);

            return new StatsSnapshot
            {
                SessionStart = _sessionStart,
                LastEventTime = _lastEventTime,
                Elapsed = elapsed,
                YourKillCount = _yourKills.Values.Sum(),
                YourKills = _yourKills.OrderByDescending(kv => kv.Value)
                    .Select(kv => new NameCount(kv.Key, kv.Value)).ToList(),
                PartyKillCount = _partyKillsByTarget.Values.Sum(),
                PartyKillsByTarget = _partyKillsByTarget.OrderByDescending(kv => kv.Value)
                    .Select(kv => new NameCount(kv.Key, kv.Value)).ToList(),
                PartyKillsByKiller = _partyKillsByKiller.OrderByDescending(kv => kv.Value)
                    .Select(kv => new NameCount(kv.Key, kv.Value)).ToList(),
                KillsPerHour = _yourKills.Values.Sum() / hours,
                Deaths = _deaths.Select(d => new TimedDetail(d.Time, d.Killer)).ToList(),
                DamageDealt = _damageDealt,
                MeleeDamage = _meleeDamage,
                SpellDamage = _spellDamage,
                HitCount = _hitCount,
                CritCount = _critCount,
                MissCount = _missCount,
                MaxHit = _maxHit,
                MaxHitDesc = _maxHitDesc,
                DamageBySource = _damageBySource.OrderByDescending(kv => kv.Value.Total)
                    .Select(kv => new SourceDamage(kv.Key, kv.Value.Count, kv.Value.Total)).ToList(),
                SpecialHits = _specialHits.OrderByDescending(kv => kv.Value)
                    .Select(kv => new NameCount(kv.Key, kv.Value)).ToList(),
                SessionDps = sessionDps,
                CurrentDps = currentDps,
                CombatSeconds = combatSeconds,
                DamageTaken = _damageTaken,
                AvoidedIncoming = _avoidedIncoming,
                MeleeHitsTaken = _meleeHitsTaken,
                DamageByAttacker = _damageByAttacker.OrderByDescending(kv => kv.Value.Total)
                    .Select(kv => new SourceDamage(kv.Key, kv.Value.Count, kv.Value.Total)).ToList(),
                HealingDone = _healingDone,
                HealingReceived = _healingReceived,
                HealsByHealer = _healsByHealer.OrderByDescending(kv => kv.Value.Total)
                    .Select(kv => new SourceDamage(kv.Key, kv.Value.Count, kv.Value.Total)).ToList(),
                HealsBySpell = _healsBySpell.OrderByDescending(kv => kv.Value.Total)
                    .Select(kv => new SourceDamage(kv.Key, kv.Value.Count, kv.Value.Total)).ToList(),
                Hps = combatSeconds > 0 ? _healingDone / combatSeconds : 0,
                RegenTicks = _regenTicks,
                LootTotal = _lootCount,
                Loot = _loot.OrderByDescending(kv => kv.Value.Count)
                    .Select(kv => new LootDetail(kv.Key, kv.Value.Count, kv.Value.LastSource)).ToList(),
                Crafted = _crafted.OrderByDescending(kv => kv.Value)
                    .Select(kv => new NameCount(kv.Key, kv.Value)).ToList(),
                CraftedTotal = _crafted.Values.Sum(),
                Copper = _copper + _vendorCopper,
                CorpseCopper = _copper,
                VendorCopper = _vendorCopper,
                SalesCount = _salesCount,
                SoldItems = _soldItems.OrderByDescending(kv => kv.Value.Copper)
                    .Select(kv => new SoldDetail(kv.Key, kv.Value.Count, kv.Value.Copper)).ToList(),
                CoinDrops = _coinDrops,
                BiggestDrop = _biggestDrop,
                CopperPerHour = (long)((_copper + _vendorCopper) / hours),
                XpPercent = _xpPercent,
                XpTicks = _xpTicks,
                XpPerHour = _xpPercent / hours,
                HoursToLevel = _xpPercent / hours > 0.05
                    ? Math.Max(0, 100 - Math.Min(_xpSinceLevel, 100)) / (_xpPercent / hours)
                    : null,
                AaGained = _aaGained,
                AaTotal = _aaTotal,
                AaPerHour = _aaGained / hours,
                Levels = _levels.Select(l => new TimedDetail(l.Time, $"Level {l.Level}")).ToList(),
                SkillUps = _skills.OrderByDescending(kv => kv.Value.Ups)
                    .Select(kv => new SkillDetail(kv.Key, kv.Value.Ups, kv.Value.Value)).ToList(),
                SkillUpTotal = _skills.Values.Sum(v => v.Ups),
                Faction = _faction.OrderByDescending(kv => Math.Abs(kv.Value.Net))
                    .Select(kv => new FactionDetail(kv.Key, kv.Value.Hits, kv.Value.Net)).ToList(),
                Zones = _zones.Select(z => new TimedDetail(z.Time, z.Zone)).ToList(),
                CurrentZone = _zones.Count > 0 ? _zones[^1].Zone : "",
                Fizzles = _fizzles,
                Resists = _resists,
            };
        }
    }
}

public record NameCount(string Name, int Count);
public record TimedDetail(DateTime Time, string Text);
public record SourceDamage(string Name, int Hits, long Total);
public record LootDetail(string Item, int Count, string LastSource);
public record SkillDetail(string Skill, int Ups, int Value);
public record SoldDetail(string Item, int Count, long Copper);
public record FactionDetail(string Faction, int Hits, int Net);

public sealed class StatsSnapshot
{
    public DateTime? SessionStart { get; init; }
    public DateTime? LastEventTime { get; init; }
    public TimeSpan Elapsed { get; init; }
    public int YourKillCount { get; init; }
    public List<NameCount> YourKills { get; init; } = [];
    public int PartyKillCount { get; init; }
    public List<NameCount> PartyKillsByTarget { get; init; } = [];
    public List<NameCount> PartyKillsByKiller { get; init; } = [];
    public double KillsPerHour { get; init; }
    public List<TimedDetail> Deaths { get; init; } = [];
    public long DamageDealt { get; init; }
    public long MeleeDamage { get; init; }
    public long SpellDamage { get; init; }
    public int HitCount { get; init; }
    public int CritCount { get; init; }
    public int MissCount { get; init; }
    public int MaxHit { get; init; }
    public string MaxHitDesc { get; init; } = "";
    public List<SourceDamage> DamageBySource { get; init; } = [];
    public List<NameCount> SpecialHits { get; init; } = [];
    public double SessionDps { get; init; }
    public double CurrentDps { get; init; }
    public double CombatSeconds { get; init; }
    public long DamageTaken { get; init; }
    public int AvoidedIncoming { get; init; }
    public int MeleeHitsTaken { get; init; }
    public List<SourceDamage> DamageByAttacker { get; init; } = [];
    public long HealingDone { get; init; }
    public long HealingReceived { get; init; }
    public List<SourceDamage> HealsByHealer { get; init; } = [];
    public List<SourceDamage> HealsBySpell { get; init; } = [];
    public double Hps { get; init; }
    public int RegenTicks { get; init; }
    public int LootTotal { get; init; }
    public List<LootDetail> Loot { get; init; } = [];
    public List<NameCount> Crafted { get; init; } = [];
    public int CraftedTotal { get; init; }
    public long Copper { get; init; }
    public long CorpseCopper { get; init; }
    public long VendorCopper { get; init; }
    public int SalesCount { get; init; }
    public List<SoldDetail> SoldItems { get; init; } = [];
    public int CoinDrops { get; init; }
    public long BiggestDrop { get; init; }
    public long CopperPerHour { get; init; }
    public double XpPercent { get; init; }
    public int XpTicks { get; init; }
    public double XpPerHour { get; init; }
    /// <summary>Estimated hours to next level at this session's XP rate; null when the rate is negligible. Exact when a level-up was seen this session, otherwise an upper bound.</summary>
    public double? HoursToLevel { get; init; }
    public int AaGained { get; init; }
    public int AaTotal { get; init; }
    public double AaPerHour { get; init; }
    public List<TimedDetail> Levels { get; init; } = [];
    public List<SkillDetail> SkillUps { get; init; } = [];
    public int SkillUpTotal { get; init; }
    public List<FactionDetail> Faction { get; init; } = [];
    public List<TimedDetail> Zones { get; init; } = [];
    public string CurrentZone { get; init; } = "";
    public int Fizzles { get; init; }
    public int Resists { get; init; }

    /// <summary>Format copper as "3p 2g 4s 7c".</summary>
    public static string FormatCoin(long copper)
    {
        if (copper == 0) return "0c";
        var p = copper / 1000; copper %= 1000;
        var g = copper / 100; copper %= 100;
        var s = copper / 10; var c = copper % 10;
        var parts = new List<string>(4);
        if (p > 0) parts.Add($"{p}p");
        if (g > 0) parts.Add($"{g}g");
        if (s > 0) parts.Add($"{s}s");
        if (c > 0) parts.Add($"{c}c");
        return string.Join(" ", parts);
    }
}
