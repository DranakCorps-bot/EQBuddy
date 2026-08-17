namespace EQBuddy.Core;

/// <summary>
/// Thread-safe aggregator for one play session. A "play session" is a contiguous
/// run of log activity; a gap of >= SessionGap between log timestamps starts a new one.
/// </summary>
public sealed partial class SessionStats
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

    /// <summary>Per-ability aggregate. ActiveSeconds approximates the time the ability
    /// was in use: consecutive hits within AbilityGap accumulate their real spacing;
    /// an isolated hit (or the first) counts IsolatedHitSeconds. Total ÷ ActiveSeconds
    /// is the closest per-ability DPS/HPS the log allows (no cast-time data exists).</summary>
    private sealed class AbilityAgg
    {
        public int Count; public long Total; public int Crits;
        public double ActiveSeconds; public DateTime LastTime;
        /// <summary>Smallest/largest single hit — the "88 – 412 dmg" range on the
        /// breakdown rows. Min is 0 until the first hit lands.</summary>
        public long Min; public long Max;
        /// <summary>Swings of this skill the log saw fail (miss/dodge/parry/riposte) —
        /// the row's miss %. Only melee misses name a skill, so spells stay at 0
        /// (their failure is the resist count, tracked separately).</summary>
        public int Misses;

        public void Add(DateTime t, long amount, bool crit = false)
        {
            var gap = (t - LastTime).TotalSeconds;
            ActiveSeconds += Count == 0 || gap < 0 || gap > AbilityGapSeconds
                ? IsolatedHitSeconds : gap;
            LastTime = t; Count++; Total += amount; if (crit) Crits++;
            if (Min == 0 || amount < Min) Min = amount;
            if (amount > Max) Max = amount;
        }

        /// <summary>A miss is an attempt, not damage: it must not touch the hit count,
        /// the range, or the active-time clock the rate is computed from.</summary>
        public void AddMiss() => Misses++;
    }
    private const double AbilityGapSeconds = 10;
    private const double IsolatedHitSeconds = 2.5;

    private long _damageDealt, _meleeDamage, _spellDamage;
    // Damage per minute-of-day bucket (key = ticks / TicksPerMinute), for the History
    // window's DPS-over-time graph. Bounded by session length: 60-min gaps reset it.
    private readonly Dictionary<long, long> _damageTimeline = new();
    private int _hitCount, _critCount, _missCount;
    private int _maxHit; private string _maxHitDesc = "";
    /// <summary>Basic attack skill → the ability that has taken it over ("Kick" → "Round
    /// Kick"), learned from the game's own announcement. Deliberately survives session
    /// resets: which abilities a character has is a fact about the character, not about the
    /// session, and the announcement is logged once when the ability is earned — possibly
    /// days before the session you're looking at.</summary>
    private readonly Dictionary<string, string> _skillAliases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What a melee hit should be filed under: the ability that replaced the skill,
    /// or the skill itself.</summary>
    private string SkillName(string skill) =>
        _skillAliases.TryGetValue(skill, out var ability) ? ability : skill;

    private readonly Dictionary<string, AbilityAgg> _damageBySource = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>What the pet is doing, split out of its single "Pet (Name)" damage row.
    /// Keyed by ability alone, not by pet: swapping charms keeps one readable list, and the
    /// per-pet totals are already the rows above it.</summary>
    private readonly Dictionary<string, AbilityAgg> _petAbilities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _specialHits = new(StringComparer.OrdinalIgnoreCase);

    private long _damageTaken;
    private int _avoidedIncoming;
    private int _meleeHitsTaken;
    /// <summary>Who hit us last, for blaming a "You died." that names no killer.</summary>
    private (string Attacker, DateTime Time)? _lastDamageFrom;
    /// <summary>How stale the last hit can be and still be blamed for a death. Generous
    /// because the fatal blow may be a damage-over-time tick a few seconds behind the last
    /// direct hit, and nothing else is competing for the blame.</summary>
    private static readonly TimeSpan DeathBlameWindow = TimeSpan.FromSeconds(20);
    private readonly Dictionary<string, (int Count, long Total)> _damageByAttacker = new(StringComparer.OrdinalIgnoreCase);

    private long _healingDone; private int _healCount;
    private long _healingReceived;
    private readonly Dictionary<string, (int Count, long Total)> _healsByHealer = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AbilityAgg> _healsBySpell = new(StringComparer.OrdinalIgnoreCase);
    private int _regenTicks;
    private long _regenEstimated;
    private string? _regenSpell;
    private string? _lastRegenCast;
    private (string Name, DateTime Time)? _lastConsider;
    private LocationEvent? _lastLoc;
    private readonly List<LocationEvent> _locTrail = [];

    private static double Distance(LocationEvent a, LocationEvent b) =>
        Math.Sqrt((a.LocX - b.LocX) * (a.LocX - b.LocX) + (a.LocY - b.LocY) * (a.LocY - b.LocY));

    /// <summary>Bumped whenever <see cref="_charmHoldByBreak"/> gains an entry —
    /// the tracked scan's invalidation guards (SessionStats.Tracked.cs) watch it.</summary>
    private int _charmHoldRevision;

    /// <summary>Perf audit #12: the last snapshot built, keyed by everything that can
    /// change its content — the version (every applied event bumps it), the recent
    /// window, and the rules fingerprint. Identical inputs return the identical
    /// instance, so idle ticks rebuild nothing anywhere. Snapshots are immutable
    /// (init-only properties over freshly built lists), which is what makes sharing
    /// one instance across consumers safe.</summary>
    private (long Version, TimeSpan? Window, string RulesFp, StatsSnapshot Snap)? _snapshotMemo;

    /// <summary>Player-supplied hp-per-tick for the regen estimate (Options), 0 = unset.
    /// The log can't know instrument resonance or ranks; the player's health bar can —
    /// same "your number wins" rule the spawn timers use.</summary>
    public int RegenPerTickOverride { get; set; }

    private int _runeGainCount; private long _runeGainPoints;
    /// <summary>Consecutive incoming melee attacks fully absorbed by the rune since the
    /// last one that actually landed. Resets to 0 the moment melee damage gets through,
    /// so it answers "how many hits did the rune eat before it broke."</summary>
    private int _runeBlockStreak, _runeBlockStreakMax, _runeBlockCount;
    private string? _characterName;

    /// <summary>The watched character's name — needed to recognize self-heals
    /// ("You healed Douglas ..." appears in Douglas's own log).</summary>
    public string? CharacterName
    {
        get { lock (_lock) return _characterName; }
        // The version bump keeps the snapshot memo honest: the character key feeds
        // the AA ledger a snapshot shows, so identity changes must not serve a
        // cached snapshot (perf audit #12).
        set { lock (_lock) { _characterName = value; _version++; } }
    }

    private string? _serverName;
    public string? ServerName
    {
        get { lock (_lock) return _serverName; }
        set { lock (_lock) { _serverName = value; _version++; } }
    }

    private readonly Dictionary<string, (int Count, string LastSource)> _loot = new(StringComparer.OrdinalIgnoreCase);
    private int _lootCount;
    private readonly Dictionary<string, int> _crafted = new(StringComparer.OrdinalIgnoreCase);
    // Loot-merge RESULTS ("looted a Belt +2 ... to create a Belt +5"): the consumed
    // item lands in _loot, but the created "+5" appeared nowhere in the snapshot —
    // and reaching a wished tier via loot-merge is the Gear card's main auto-done
    // moment. Kept apart from _crafted so the "+N made" header stays what it was.
    private readonly Dictionary<string, int> _upgraded = new(StringComparer.OrdinalIgnoreCase);

    private long _copper; private int _coinDrops; private long _biggestDrop;
    private long _vendorCopper; private int _salesCount;
    private readonly Dictionary<string, (int Count, long Copper)> _soldItems = new(StringComparer.OrdinalIgnoreCase);

    private double _xpPercent; private int _xpTicks;
    private double _xpSinceLevel;
    private int _aaGained; private int _aaTotal;
    /// <summary>AA abilities owned: name → (highest observed rank, when). Survives session
    /// resets deliberately — purchases are character state, not session activity, and the
    /// duration models that read them need the full picture, not since-last-camp.</summary>
    private readonly Dictionary<string, (int Rank, DateTime Time)> _aaAbilities = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional durable ledger behind <see cref="_aaAbilities"/> — purchases write
    /// through to it, and snapshots read the union, so truncated logs can't forget an AA.
    /// Attaching one bumps the version: the store's contents feed snapshots, so the
    /// memo (perf audit #12) must not keep serving a pre-attach one.</summary>
    public AaLedgerStore? AaStore
    {
        get { lock (_lock) return _aaStore; }
        set { lock (_lock) { _aaStore = value; _version++; } }
    }
    private AaLedgerStore? _aaStore;

    /// <summary>Optional quest-item ledger, fed from loot events the same way AaStore
    /// rides AA purchases (QUEST-*; the UI wires catalog + path).</summary>
    public QuestLedgerStore? QuestStore { get; set; }

    /// <summary>Optional spell-stacking ledger, fed from blocked-cast lines the same
    /// way — its own time high-water mark keeps the startup replay idempotent.</summary>
    public StackingLedgerStore? StackingStore { get; set; }

    /// <summary>Review replay is a window onto the past, not a new session (#74):
    /// while set, the durable per-character ledgers above ignore this run's events —
    /// an archived log replayed under review recorded its facts when it was live,
    /// and (before the FromPath stamp fix) wrote them again under phantom keys
    /// (audit finding 4). MainWindow sets this for the duration of review; snapshots
    /// still READ the stores.</summary>
    public bool StoresSuppressed { get; set; }

    /// <summary>The per-character ledger key ("dranak_legends") the stores are written
    /// under — the Quest Tracker window queries the ledger with this.</summary>
    public string LedgerCharacterKey => AaCharacterKey;

    private string AaCharacterKey =>
        CharacterName is { Length: > 0 } c ? $"{c}_{ServerName}".ToLowerInvariant() : "";
    private readonly List<(DateTime Time, int Level)> _levels = new();

    private readonly Dictionary<string, (int Ups, int Value)> _skills = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Hits, int Net, bool Capped, bool CappedDown)> _faction = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(DateTime Time, string Zone)> _zones = new();
    private int _fizzles, _resists, _blocked;

    // Session event journal (JOURNAL-*): loot/coin/xp/kill/etc. kept whole-session;
    // high-frequency combat/heal events pruned past the largest recent window.
    private readonly List<GameEvent> _journal = new();
    /// <summary>How many raw drops the newest-first loot view carries. Long farms run to
    /// thousands; the card shows a window, and the aggregate view remains the complete
    /// record.</summary>
    private const int MaxRecentLoot = 250;
    private static readonly TimeSpan CombatJournalRetention = TimeSpan.FromMinutes(40);
    private int _journalAppendsSincePrune;

    // Active-play tracking (ACTIVE-*): 2-minute buckets containing any meaningful event.
    private static readonly TimeSpan ActiveBucket = TimeSpan.FromMinutes(2);
    private readonly SortedSet<long> _activeBuckets = new();

    private readonly List<(DateTime Time, string Label)> _markers = new();

    // ---- encounters + mob farming (Release C) ----
    private sealed class ActiveFight
    {
        public DateTime Start, Last;
        public long DmgOut, DmgIn, Healed;
        /// <summary>Same breakdown as the session's, scoped to this fight — what actually
        /// killed the thing in front of you, rather than what you've used all night.</summary>
        public readonly Dictionary<string, AbilityAgg> ByAbility = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>What the creature did to YOU, keyed by its attack skill or spell name.
        /// The fight is already keyed by the attacker, so rows don't repeat its name.</summary>
        public readonly Dictionary<string, AbilityAgg> ByIncoming = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, AbilityAgg> HealsBySpell = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>The pet's damage in this fight split by its ability — the per-fight
        /// counterpart of the session-wide split, so a fight can answer "what did the pet
        /// actually do here" (the ByAbility list keeps the pet as one labeled row).</summary>
        public readonly Dictionary<string, AbilityAgg> PetAbilities = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The fight healing is currently credited to. Heals name a target, not a
    /// creature, so there's nothing in the line tying one to the fight it belongs to — the
    /// only honest link is "whatever you were fighting at the time". Heals cast between
    /// pulls belong to no fight and count only towards the session.</summary>
    private string? _healingFight;
    private sealed class MobAgg
    {
        public int Kills, Encounters;
        public double FightSeconds;
        public double Xp;
        public long Copper;
        public readonly Dictionary<string, int> Loot = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Last time each item dropped — rides into MobLoot.LastAt (#65).</summary>
        public readonly Dictionary<string, DateTime> LootLast = new(StringComparer.OrdinalIgnoreCase);
        // Stat-block trio (#65, Frankthetankk): zone AT KILL TIME (not wherever the
        // tool saw the player last), per-kill coin-drop bounds for the wiki's
        // low–high-per-coin format, and faction hits with their per-kill deltas —
        // a confirmed absence being data too.
        public string Zone = "";
        public long CoinMin = -1, CoinMax;
        public readonly Dictionary<string, (int Hits, int Delta)> Factions = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Level bounds from /consider lines (#65: the wiki pack's level
        /// field). 0 min = never conned; each distinct conned level widens the range.</summary>
        public int LevelMin, LevelMax;
    }
    private static readonly TimeSpan EncounterTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RewardWindow = TimeSpan.FromSeconds(3);
    private readonly Dictionary<string, ActiveFight> _activeFights = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<EncounterInfo> _encounters = new();
    private readonly Dictionary<string, MobAgg> _mobs = new(StringComparer.OrdinalIgnoreCase);
    private (string Name, DateTime Time)? _lastKill;
    private (string Item, int Count, DateTime Time)? _lastDestroyed;
    // EQL logs rewards BEFORE the kill line ("You gain experience!" → coin → "You have
    // slain X!", same second), so xp/coin are held here until a kill claims them.
    private readonly List<(DateTime Time, double Percent)> _pendingXp = [];
    private readonly List<(DateTime Time, long Copper)> _pendingCoin = [];

    // ---- stance windows (Release D) ----
    private string? _currentStance;
    private readonly Dictionary<string, (double Seconds, long Damage)> _stanceAgg = new(StringComparer.OrdinalIgnoreCase);

    // ---- invocation windows (2026-08-03, same model as stances) ----
    private string? _currentInvocation;
    private readonly Dictionary<string, (double Seconds, long Damage)> _invocationAgg = new(StringComparer.OrdinalIgnoreCase);

    // Combat-window tracking for DPS
    private readonly List<(DateTime Start, DateTime End)> _combatSpans = new();
    private double _closedCombatSeconds; private long _closedCombatDamage;
    private DateTime? _combatStart; private DateTime? _combatLast; private long _combatDamage;
    private DateTime? _lastOwnAction;
    private string? _petName;        // normalized (article stripped, capitalized)
    private bool _petConfirmed;      // false = blink-only (charm suspected, no "Master" tell yet)

    // ---- spell tracking ----
    private readonly SpellCatalog _spells = new();
    /// <summary>The spell classifier, exposed so the apps can attach the persistent
    /// learned-category store (tests don't, keeping learning session-local).</summary>
    public SpellCatalog Spells => _spells;
    private (string Spell, DateTime Time)? _pendingCast;     // last cast started

    // ---- procs (#85, Kerdude): spell damage whose spell was never cast ----
    /// <summary>How long after "You begin casting X." damage "by X" still counts as the
    /// cast (cast time + travel + log flush). Longer than this, or never cast at all,
    /// and the hit is a proc. Kerdude's snippet: Grasping Roots cast→hit 2s.</summary>
    private static readonly TimeSpan ProcCastWindow = TimeSpan.FromSeconds(12);
    /// <summary>An item-proc line this close before the damage names the vehicle
    /// ("Your Polished Mithril Mask (Exaltation) feels alive with power." then the
    /// Bolt of Flame hit, same second in the field snippet).</summary>
    private static readonly TimeSpan ProcItemWindow = TimeSpan.FromSeconds(2.5);
    // ---- inferred class (David, 2026-08-11; rebuilt for #120, Frankthetankk) ----
    /// <summary>Which class the log looks like. The table of signals, the recency
    /// weighting and the "don't know" rules all live in <see cref="ClassInference"/>,
    /// where they can be tested without a log: the first version knew only melee skills,
    /// so a caster who once produced a melee-ish line could never out-vote it.</summary>
    private readonly ClassInference _classInference = new();

    private readonly Dictionary<string, (int Count, long Damage)> _procs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _spellCastAt = new(StringComparer.OrdinalIgnoreCase);
    // Per-spell casts vs resists (#102, jeremycranfill: "do I need to switch to
    // overchannel?") and stacking blocks ("did not take hold"). Keyed by base spell
    // name; songs count too — they resist the same.
    private readonly Dictionary<string, (int Casts, int Resists, int Blocked)> _spellOutcomes = new(StringComparer.OrdinalIgnoreCase);
    private (string Item, DateTime Time)? _lastItemProc;
    // A cast that preceded a blink or charmed line, held until a "Master" tell proves it
    // was a charm. Pet carries the creature the line named: the tell must name the SAME
    // creature to teach, so a bystander's charm coinciding with our own unrelated cast
    // (Hugzee's Heroic Leap) can never mislabel that cast as a charm (issue #29).
    private (string Spell, DateTime Time, string Pet)? _charmCandidate;
    // #130 (bjstrange): how long the current charm has HELD, and how long the last
    // one held. Set only by charm-path claims — a summoned pet never "breaks".
    private (string Pet, DateTime LandedAt)? _charmHold;
    // Provisional charm claims (late landings, unknown spells) start the clock too;
    // the Master tell that confirms them keeps the original landing time.
    private (string Pet, DateTime LandedAt)? _charmProvisional;
    // Break-time → held seconds, so the fade alert's label can say "held 4:32"
    // (the journal scan rebuilds labels repeatedly; this is its lookaside).
    private readonly Dictionary<DateTime, double> _charmHoldByBreak = new();
    // The game prints a charm's fade line up to several seconds AFTER the event that
    // actually broke it. One window covers both faces of that skew: FadeLabel looks
    // this far back for a recorded hold (#135, v1.76.0: attack-then-fade), and the
    // wear-off ingest treats a fade this close to an already-recorded break as that
    // break's delayed echo rather than a new break (#135, bjstrange: re-charm cascade).
    private const int CharmFadeSkewSeconds = 10;
    // A swing already in flight when the charm lands still hits YOU a beat later — the
    // mob was mid-round, and the game resolves that round before the charm takes hold.
    // Reading it as "my pet turned on me" destroys the claim one second after making it,
    // so the real fade minutes later has no landing to measure from and prints no hold
    // (#135, bjstrange: "3 charms announced, the 4th didn't", different mobs, random —
    // random because it depends on whether the mob happened to be mid-swing). A melee
    // round is ~2 s, so 3 covers the tail without hiding a genuine instant break: the
    // authoritative "worn off" line still records that a moment later.
    private const int CharmSettleSeconds = 3;
    // When we last had PROOF that two creatures share the pet's name: a line where the
    // pet both attacks and is attacked under the same name — "A greater ice bones
    // slashes a greater ice bones" (#135, bjstrange's charm4.txt). EQ's log identifies
    // creatures by name and nothing else, so while a duplicate is known to exist, a
    // same-named attacker hitting US is ambiguous and must not destroy the charm claim;
    // the unambiguous "worn off" line settles it instead.
    //
    // Deliberately narrower than "the pet is busy with someone else". A pet fighting a
    // GHOUL and then turning on you is a real break with no ambiguity about identity,
    // and suppressing that would keep crediting a creature that is now hitting you.
    private const int SameNameProofSeconds = 30;
    private DateTime? _sameNameProofAt;
    // Since when your pet has been on HOLD, from its own reply ("Now holding, Master. I
    // will not start attacks until ordered."). A held pet does not initiate attacks, so
    // a same-named creature swinging at you while yours is held is a DIFFERENT creature —
    // the second, independent proof of a duplicate, and the one #135's fifth log needed.
    //
    // charm6.txt: Bzzazzt charmed at 01:25:21, told to hold at 01:25:28, and at 01:25:36
    // "Bzzazzt" lands a full five-hit round on the player. The 1.87.0 guard could not
    // help, because its only proof is a creature attacking something of its own name and
    // the two Bzzazzts never fought each other — one fought the player, the charmed one
    // fought (and killed) Eye of Veeshan. So the claim died 15 seconds into a 3:28 charm
    // and the wear-off at 01:28:49 had no landing left to measure.
    private DateTime? _petHeldSince;
    private int _castsStarted, _castsInterrupted;
    private long _dotDamage, _directSpellDamage;

    // ---- area-spell detection ----
    // A spell that damages several creatures at once is one cast, not several. Reporting
    // it per target makes an AoE look weaker than a nuke it actually beats, which is
    // exactly backwards for deciding whether to pull a group and AoE it down.
    // Detection is behavioural (same spell, multiple targets, close together) so no list
    // of area spells is needed. Working from damage lines also means travel spells can
    // never be mistaken for area damage — they produce no damage at all.
    private static readonly TimeSpan AreaBurstWindow = TimeSpan.FromSeconds(2);

    private sealed class SpellBurst
    {
        public DateTime Start;
        public readonly HashSet<string> Targets = new(StringComparer.OrdinalIgnoreCase);
        public long Damage;
    }

    private sealed class CastAgg
    {
        public int Casts;
        public int TargetHits;
        public long Damage;
        public int MaxTargets;
    }

    private readonly Dictionary<string, SpellBurst> _openBursts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CastAgg> _castAgg = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How long after a cast starts a blink can still belong to it. Charm casts
    /// run a few seconds; observed gap in real logs is ~4s. This is the FALLBACK for
    /// spells whose cast time the catalog doesn't know — known charms use the tighter
    /// per-spell arm window below.</summary>
    private static readonly TimeSpan CastToBlink = TimeSpan.FromSeconds(30);

    /// <summary>Slack past a spell's cast time in the arm window: log timestamps round
    /// to the second and the server adds a beat, but a landing seconds after our cast
    /// COMPLETED is somebody else's charm.</summary>
    internal const double CharmArmSlackSeconds = 1.5;

    /// <summary>Per-spell charm arm window (approved 2026-08-13): a landing line is
    /// ours only within the spell's own cast time + slack of the cast starting. The
    /// old fixed 30s meant a bystander's charm landing 20s after our failed Beguile
    /// (3.5s cast) could steal the claim; now the window fits the spell.
    /// Two honesty guards from the review: log stamps are WHOLE seconds (a real
    /// 3.02s gap can log as 4), so the fractional cast time rounds UP before the
    /// slack is added; and a zero/absent cast time means "instant or unknown" —
    /// either way the generic window applies, never a 1.5s trap.</summary>
    private TimeSpan ArmWindow(string spell) =>
        _spells.CastTimeSeconds(spell) is { } ct && ct > 0
            ? TimeSpan.FromSeconds(Math.Ceiling(ct) + CharmArmSlackSeconds)
            : CastToBlink;
    /// <summary>How long after a blink a "Master" tell still confirms the same charm.
    /// Observed gap in real logs is ~5s; pets can be slow to announce.</summary>
    private static readonly TimeSpan BlinkToClaim = TimeSpan.FromSeconds(60);

    public event Action? SessionRolledOver;
    /// <summary>Raised (outside the lock) with the final snapshot of a session that just
    /// ended via the inactivity gap — the hook for persisting it to history.</summary>
    public event Action<StatsSnapshot>? SessionEnding;

    /// <summary>Text patterns from the enabled <see cref="WatchKind.Text"/> rules. Kept
    /// current by <see cref="Snapshot"/>, so an edit in Options takes effect on the next
    /// refresh a second later without the host having to push anything.</summary>
    // The rules themselves rather than pattern strings: Matches() is what knows
    // whether a pattern is a substring or a regex (#83), and it caches its Regex.
    private TrackedRule[] _textPatterns = [];

    /// <summary>
    /// Seed the text-rule prefilter before tailing starts. <see cref="Snapshot"/> keeps it
    /// up to date afterwards, but the initial full-log ingest runs before the first
    /// snapshot — without this, a text rule would silently ignore everything already in
    /// today's log and only match lines written after startup.
    /// </summary>
    public void RefreshTextPatterns(IEnumerable<TrackedRule>? rules)
    {
        var patterns = rules is null ? [] : rules
            .Where(r => r.Enabled && r.Kind == WatchKind.Text && r.EffectivePattern.Length > 0)
            .ToArray();
        lock (_lock) _textPatterns = patterns;
    }

    /// <summary>
    /// Offer a raw log line for <see cref="WatchKind.Text"/> matching. Called for every
    /// line, parsed or not — a raid-assist announcement may well also be a line EQBuddy
    /// understands, and text rules are about the text, not about what we made of it.
    ///
    /// Only lines matching an active pattern are kept, so with no text rules configured
    /// this costs one array-length check per line and changes nothing else.
    /// </summary>
    /// <summary>The last few raw messages, newest last — the "new rule from a recent
    /// line" picker's menu (Companion-parity idea: alerts built from what just
    /// happened instead of typed from memory). Bounded ring, kept even with no text
    /// rules configured — the picker is how the FIRST rule gets made.</summary>
    public const int RecentLineCap = 80;
    private readonly Queue<(DateTime Time, string Message)> _recentLines = new();

    /// <summary>Snapshot copy of the recent-lines ring, oldest first.</summary>
    public List<(DateTime Time, string Message)> RecentLines()
    {
        lock (_lock) return [.. _recentLines];
    }

    public void ObserveRawLine(string line)
    {
        if (LogParser.TrySplitLine(line, out var ts, out var msg)) ObserveRawLine(ts, msg);
    }

    /// <summary>Already-split overload (perf audit #13): LogWatcher splits each line
    /// once and hands the parts to both Parse and this — same behavior, one split.</summary>
    public void ObserveRawLine(DateTime ts, string msg)
    {
        TrackedRule[] patterns;
        lock (_lock) patterns = _textPatterns;
        lock (_lock)
        {
            _recentLines.Enqueue((ts, msg));
            if (_recentLines.Count > RecentLineCap) _recentLines.Dequeue();
        }
        if (patterns.Length == 0) return;

        foreach (var pattern in patterns)
        {
            if (!pattern.Matches(msg)) continue;
            var evt = new RawLineEvent(ts, msg);
            Apply(evt);
            // Raised outside the lock, on the ingest thread, so the host can alert now
            // rather than on its next refresh. See TextMatched.
            TextMatched?.Invoke(evt);
            return;   // one event per line, however many rules it satisfies
        }
    }

    /// <summary>
    /// A line just matched a <see cref="WatchKind.Text"/> rule. Every other alert is driven
    /// off the host's periodic snapshot, which adds up to a full refresh interval of lag;
    /// text rules exist for calls you have to react to, so they get told immediately.
    ///
    /// Raised on the ingest thread — handlers must marshal to their UI thread themselves,
    /// and must not block, or they stall tailing.
    /// </summary>
    public event Action<RawLineEvent>? TextMatched;

    /// <summary>Bumped on every applied event and on reset — the UI's cheap "did
    /// anything change since my last render" signal (perf audit #1: rebuilding a few
    /// hundred WPF rows per second during idle was the app's main steady-state cost).</summary>
    private long _version;

    /// <summary>The version WITHOUT building a snapshot — for consumers that only
    /// need "did anything happen since I last looked" (the fight timeline's tick was
    /// paying a full snapshot + journal copy every second to learn the answer was no).</summary>
    public long CurrentVersion { get { lock (_lock) return _version; } }

    public void Apply(GameEvent e)
    {
        var rolled = false;
        StatsSnapshot? finalSnapshot = null;
        lock (_lock)
        {
            _version++;
            if (_lastEventTime is { } last && e.Time - last >= SessionGap)
            {
                finalSnapshot = BuildSnapshotLocked(null, null);
                ResetLocked();
                rolled = true;
            }
            _sessionStart ??= e.Time;
            _lastEventTime = e.Time;

            _journal.Add(e);
            // A matched text line is not evidence you were playing — a raid-assist macro or
            // a guild chat pattern fires just as happily while you're stood in the bank or
            // away from the keyboard. Active-play buckets stay a record of your own actions.
            // Raid chatter is other people's typing — the same rule, for the same reason.
            if (e is not RawLineEvent and not RaidChatterEvent)
                _activeBuckets.Add(e.Time.Ticks / ActiveBucket.Ticks);
            if (++_journalAppendsSincePrune >= 512)
            {
                _journalAppendsSincePrune = 0;
                PruneJournalLocked(e.Time - CombatJournalRetention);
            }

            SweepStaleFights(e.Time);

            switch (e)
            {
                case KillEvent k when k.Killer == "You" || IsPet(k.Killer):
                    Bump(_yourKills, k.Target);
                    TrackCombat(k.Time);
                    FinalizeFight(k.Target, k.Time, "Killed");
                    var killedMob = Mob(k.Target);
                    killedMob.Kills++;
                    // Zone at time of THIS kill — a creature farmed in two zones keeps
                    // the earliest, and the export can say so honestly.
                    if (killedMob.Zone.Length == 0 && _zones.Count > 0)
                        killedMob.Zone = _zones[^1].Zone;
                    _lastKill = (k.Target, k.Time);
                    ClaimPendingRewards(k.Target, k.Time);
                    break;
                case KillEvent k:
                    Bump(_partyKillsByTarget, k.Target);
                    Bump(_partyKillsByKiller, k.Killer);
                    TrackCombat(k.Time, canStart: false);
                    // Someone else finished a mob we may have been fighting.
                    FinalizeFight(k.Target, k.Time, "Killed");
                    _lastKill = (k.Target, k.Time);
                    ClaimPendingRewards(k.Target, k.Time);
                    break;
                case CharmedEvent ch:
                    // The direct charm-success line — but it names NO caster and is
                    // bystander-visible (12 of 43 in eqlog_Hugzee had no own cast near
                    // them: other players charming nearby; David called this before it
                    // shipped wrong). Worse, "unknown cast in flight" is no proof of
                    // ownership either: Hugzee spams Heroic Leap (unknown to the
                    // catalog), and one leap coinciding with a bystander's charm would
                    // both steal the pet AND teach the catalog that Heroic Leap is a
                    // charm. So this line claims ONLY behind a cast already KNOWN to be
                    // a charm — where it beats the "Attacking … Master." tell by up to
                    // 9 s of otherwise-unclaimed damage. Unknown charm spells still get
                    // learned via the Master tell, which is caster-only and unspoofable.
                    // Deliberately NO TrackCombat: charming isn't fighting.
                    if (_pendingCast is { } chCast && ch.Time - chCast.Time <= CastToBlink)
                    {
                        var chCategory = _spells.Classify(chCast.Spell);
                        // Known charm: claim only inside ITS arm window (cast time +
                        // slack) — past that our cast completed without this landing,
                        // so the line is probably a bystander's charm.
                        if (chCategory == SpellCategory.Charm
                            && ch.Time - chCast.Time <= ArmWindow(chCast.Spell))
                        {
                            _pendingCast = null;
                            ConfirmPet(LogParser.Normalize(ch.Name));
                            _charmHold = (LogParser.Normalize(ch.Name), ch.Time);   // #130
                        }
                        // Outside the window but our charm cast IS still recent:
                        // degrade to the provisional "Pet?" state instead of nothing
                        // (lag/rounding cases) — the "Master" tell resolves it and
                        // merges the provisional damage, same as the blink path.
                        else if (chCategory == SpellCategory.Charm && _petName is null)
                        {
                            _petName = LogParser.Normalize(ch.Name);
                            _petConfirmed = false;
                            _charmProvisional = (_petName, ch.Time);   // #130: clock starts at the landing
                        }
                        // Unknown cast + no pet of our own: record the cast as a charm
                        // candidate — NO claim, no damage credit (a bystander's charm
                        // coinciding with Heroic Leap must not steal anything) — so the
                        // "Master" tell that follows the first attack order can teach the
                        // spell. Before this, the learning hook only existed on the blink
                        // path: a client whose charms log "has been charmed." with a spell
                        // outside the catalog never learned it, and every charm waited for
                        // the attack button (issue #29). With the persistent store, that
                        // wait now happens once per spell ever.
                        else if (chCategory == SpellCategory.Unknown && _petName is null)
                            _charmCandidate = (chCast.Spell, ch.Time, LogParser.Normalize(ch.Name));
                    }
                    break;
                case MezzedEvent glazed:
                    // "X's eyes glaze over." lands BOTH bard charm songs and bard mez
                    // songs (eqlwiki: Solon's line vs Crission's/Sionachie's — identical
                    // message). The parser can't tell them apart; the pending SONG can.
                    // MezTracker consumes this event for mez songs; here, a pending
                    // charm-classified cast makes it a charm landing.
                    if (_pendingCast is { } glazeCast
                        && glazed.Time - glazeCast.Time <= CastToBlink
                        && _spells.Classify(glazeCast.Spell) == SpellCategory.Charm)
                    {
                        // Inside the song's own arm window the claim is certain; a
                        // glaze on a LATER pulse (bard songs pulse ~6s and only the
                        // first "begin to sing" logs) degrades to provisional — the
                        // attack-order tell confirms and merges, never loses.
                        if (glazed.Time - glazeCast.Time <= ArmWindow(glazeCast.Spell))
                        {
                            _pendingCast = null;
                            ConfirmPet(glazed.Target);
                            _charmHold = (glazed.Target, glazed.Time);   // #130
                        }
                        else if (_petName is null)
                        {
                            _petName = glazed.Target;
                            _petConfirmed = false;
                            _charmProvisional = (glazed.Target, glazed.Time);
                        }
                    }
                    break;
                case PetClaimEvent pc:
                    // "My leader is X." rides the broadcast say channel, so a nearby player's
                    // pet answering THEIR /pet leader lands in our log too — the name is what
                    // separates them, and it has to be ours. An unknown character name can't
                    // check it, and an unverifiable claim is not one we take: _petName is a
                    // single slot, so a wrong one swaps our pet's damage out for a stranger's.
                    // (The attack order names nobody and needs none — it is a tell addressed
                    // to us, which no bystander's pet ever sends.)
                    if (pc.Leader is { } leader
                        && !string.Equals(leader, _characterName, StringComparison.OrdinalIgnoreCase))
                    {
                        // …and when it names somebody ELSE, it is not merely unhelpful:
                        // it is the one line in the log that DISPROVES ownership, which
                        // is chrstahl's own suggestion in #177 and settles the cases
                        // inference cannot. Inference has to guess from timing alone —
                        // two charmers in one camp and a landing line that names no
                        // caster — and a wrong guess quietly credits a stranger's pet
                        // to us for as long as it lives. Drop the claim.
                        //
                        // Only against a character name we actually know: with none, the
                        // leader may well BE us and the "disproof" would be us releasing
                        // our own pet. And only for the creature the line names, since a
                        // statement about a different pet says nothing about ours.
                        var disproved = LogParser.Normalize(pc.PetName);
                        if (_characterName is { Length: > 0 } && _petName is not null
                            && string.Equals(_petName, disproved, StringComparison.OrdinalIgnoreCase))
                        {
                            // Deliberately NOT a charm break: nothing of ours ended, so
                            // recording a hold would print a duration for a pet we never
                            // had. Damage already credited stays as it was booked —
                            // rewinding aggregates would leave the session totals and
                            // the per-source rows disagreeing, and the provisional rows
                            // say "Pet?" precisely because they might be wrong.
                            _petName = null;
                            _petConfirmed = false;
                            if (_charmHold is { } hold && string.Equals(
                                    hold.Pet, disproved, StringComparison.OrdinalIgnoreCase))
                                _charmHold = null;
                            if (_charmProvisional is { } prv && string.Equals(
                                    prv.Pet, disproved, StringComparison.OrdinalIgnoreCase))
                                _charmProvisional = null;
                        }
                        // The held cast must go too, or the next landing line re-claims
                        // the creature we were just told is not ours.
                        if (_charmCandidate is { } foreign
                            && string.Equals(foreign.Pet, disproved, StringComparison.OrdinalIgnoreCase))
                            _charmCandidate = null;
                        break;
                    }
                    // A blink/charmed line that followed an unrecognised cast, now proven
                    // ours: that cast was a charm spell, so remember it — permanently, via
                    // the attached store. The claim must name the same creature the line
                    // did; a claim about a different pet proves nothing about that cast.
                    var claimed = LogParser.Normalize(pc.PetName);
                    if (_charmCandidate is { } cand && pc.Time - cand.Time <= BlinkToClaim
                        && string.Equals(cand.Pet, claimed, StringComparison.OrdinalIgnoreCase))
                    {
                        _spells.Learn(cand.Spell, SpellCategory.Charm);
                        _charmHold ??= (claimed, cand.Time);   // #130: the blink was the landing
                        _charmCandidate = null;
                    }
                    // A provisional charm claim the tell just confirmed keeps its
                    // original landing time — the clock started when the charm did.
                    if (_charmProvisional is { } prov && pc.Time - prov.LandedAt <= BlinkToClaim
                        && string.Equals(prov.Pet, claimed, StringComparison.OrdinalIgnoreCase))
                    {
                        _charmHold ??= (claimed, prov.LandedAt);
                        _charmProvisional = null;
                    }
                    ConfirmPet(claimed);
                    // Only the attack order proves a fight; the leader response would
                    // otherwise open a combat span while camped.
                    if (pc.Fighting) TrackCombat(pc.Time);
                    break;
                case PetHoldEvent ph:
                    // Only for the creature the line names, and only when that is our
                    // pet: a nearby charmer's pet answering THEIR hold order rides the
                    // same say channel, and taking it would excuse a genuine break by
                    // ours. Same name test the leader line uses.
                    if (_petName is not null
                        && string.Equals(_petName, LogParser.Normalize(ph.PetName),
                            StringComparison.OrdinalIgnoreCase))
                        _petHeldSince = ph.Holding ? ph.Time : null;
                    break;

                case PetBlinkEvent pb:
                    // Charm just landed. If one of our charm casts is still in flight the
                    // claim is certain, so skip the provisional "Pet?" state entirely.
                    var blinked = LogParser.Normalize(pb.Name);
                    if (_pendingCast is { } cast && pb.Time - cast.Time <= CastToBlink)
                    {
                        var category = _spells.Classify(cast.Spell);
                        // Certain only inside the spell's own arm window; a blink
                        // seconds after our cast completed gets the provisional
                        // treatment below instead of a confident claim.
                        if (category == SpellCategory.Charm
                            && pb.Time - cast.Time <= ArmWindow(cast.Spell))
                        {
                            ConfirmPet(blinked);
                            _pendingCast = null;
                            _charmHold = (blinked, pb.Time);   // #130
                            break;
                        }
                        // A known charm whose arm window already closed: a weak line
                        // (moan) is ambient flavor again — our cast completed without
                        // it. Strong blinks fall through to the provisional state.
                        if (category == SpellCategory.Charm && pb.Weak)
                            break;
                        // Unrecognised spell: hold onto it so a following "Master" tell
                        // can teach us it was a charm.
                        if (category == SpellCategory.Unknown)
                            _charmCandidate = (cast.Spell, pb.Time, blinked);
                    }
                    else if (pb.Weak)
                    {
                        // A moan with no cast of ours in flight is ambient flavor,
                        // not a charm — never even provisional.
                        break;
                    }
                    _petName = blinked;
                    _petConfirmed = false;
                    break;
                case SpellCastEvent started:
                    // Songs correlate (bard charms/mezzes ARE songs) but stay out of the
                    // cast-completion stats — twisting would swamp them.
                    if (!started.Song) _castsStarted++;
                    {
                        var outKey = SpellCatalog.BaseName(started.Spell);
                        var so = _spellOutcomes.GetValueOrDefault(outKey);
                        _spellOutcomes[outKey] = (so.Casts + 1, so.Resists, so.Blocked);
                        // Class evidence (#120): a song proves Bard whatever it is called —
                        // only bards sing — while a cast is judged on the spell's own name.
                        if (started.Song) _classInference.RecordSong(outKey, started.Time);
                        else _classInference.RecordCast(outKey, started.Time);
                    }
                    _pendingCast = (started.Spell, started.Time);
                    // Proc detection reads this: damage "by <Spell>" with no cast-start
                    // for that spell on record is a proc (#85).
                    _spellCastAt[started.Spell] = started.Time;
                    // Amount-less regen family: remember the last one cast/sung, so the
                    // shared "wounds begin to heal" tick line knows whose ticks these are.
                    if (RegenCatalog.PerTick(SpellCatalog.BaseName(started.Spell)) is not null)
                        _lastRegenCast = SpellCatalog.BaseName(started.Spell);
                    break;
                case SpellInterruptedEvent:
                    _castsInterrupted++;
                    _pendingCast = null;
                    break;
                case SpellBlockedEvent blk:
                    // "did not take hold": the cast COMPLETED — mana spent, so it stays
                    // out of the interrupt/fizzle completion math, like a resist — but
                    // nothing landed, so nothing may stay armed. A pending charm cast
                    // left live here could claim a bystander's blink seconds later,
                    // the exact phantom the interrupt case already prevents.
                    _blocked++;
                    {
                        var blkKey = SpellCatalog.BaseName(blk.Spell);
                        var so = _spellOutcomes.GetValueOrDefault(blkKey);
                        _spellOutcomes[blkKey] = (so.Casts, so.Resists, so.Blocked + 1);
                        if (_pendingCast is { } bpc && string.Equals(
                                SpellCatalog.BaseName(bpc.Spell), blkKey, StringComparison.OrdinalIgnoreCase))
                            _pendingCast = null;
                        // A charm blocked BY ITSELF is a re-cast bouncing off the pet
                        // already held — evidence about the NEW cast, not the armed
                        // candidate from the original landing. Disarming there would
                        // cost a chain-charmer the claim of a genuinely held pet.
                        if (_charmCandidate is { } bcc && string.Equals(
                                SpellCatalog.BaseName(bcc.Spell), blkKey, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(SpellCatalog.BaseName(blk.BlockedBy), blkKey,
                                StringComparison.OrdinalIgnoreCase))
                            _charmCandidate = null;
                        // A blocker-less line still counts above; only a NAMED pair is
                        // a stacking fact the ledger can hold.
                        if (blk.BlockedBy.Length > 0 && !StoresSuppressed)
                            StackingStore?.Record(AaCharacterKey, blkKey, blk.BlockedBy, blk.Time);
                    }
                    break;
                case SpellWornOffEvent { Pet: false } wo when _petName is not null && wo.Target.Length > 0
                        && IsPet(wo.Target) && _spells.Classify(wo.Spell) == SpellCategory.Charm:
                    // A fade this soon after a recorded break is that break's delayed
                    // echo: the break already dropped the OLD claim, so the claim held
                    // now belongs to a re-charm of the same creature and must survive
                    // the stale line (#135, bjstrange: re-charm echo cascade).
                    if (IsCharmBreakEcho(wo.Time)) break;
                    // Charm broke on our pet. Drop the claim now instead of waiting for the
                    // creature to turn around and hit us.
                    RecordCharmBreak(wo.Time);
                    _petName = null;
                    _petConfirmed = false;
                    break;
                case SpellWornOffEvent { Pet: false, Target.Length: 0 } woNoTarget
                        when _petName is not null
                        && _spells.Classify(woNoTarget.Spell) == SpellCategory.Charm:
                    // Befriend Animal's break line names NO target — "Your charm spell
                    // has worn off." (eqlwiki; unique among the animal charms). Only one
                    // charm can be active, so a targetless charm fade is ours.
                    if (IsCharmBreakEcho(woNoTarget.Time)) break;   // #135: stale echo, claim is the re-charm's
                    RecordCharmBreak(woNoTarget.Time);
                    _petName = null;
                    _petConfirmed = false;
                    break;
                case ThirdMeleeEvent tm when IsPet(tm.Attacker):
                    // Attacker AND target share the pet's name: there are two of them.
                    if (IsPet(tm.Target)) _sameNameProofAt = tm.Time;
                    AddPetDamage(tm.Time, tm.Amount, DamageKind.Melee, tm.Target, tm.Skill, tm.Critical);
                    break;
                case ThirdDotEvent td when IsPet(td.Caster):
                    AddPetDamage(td.Time, td.Amount, DamageKind.Spell, td.Target, td.Spell, td.Critical);
                    break;
                case ThirdSchoolEvent tse when IsPet(tse.Attacker):
                    AddPetDamage(tse.Time, tse.Amount, DamageKind.Spell, tse.Target, tse.Spell, tse.Critical);
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
                    // "You died." names nobody, so credit whatever last hurt us — for a
                    // damage-over-time death that's the caster of the tick that finished the
                    // job, which is the answer a player wants. Falls back to "Something"
                    // rather than an empty string so the row, and any Death watch rule
                    // matching on killer, still reads sensibly.
                    _deaths.Add((d.Time, d.Killer.Length > 0
                        ? d.Killer
                        : _lastDamageFrom is { } src && d.Time - src.Time <= DeathBlameWindow
                            ? src.Attacker
                            : "Something"));
                    break;
                case DamageDealtEvent dd:
                    _damageDealt += dd.Amount;
                    AddTimelineDamage(dd.Time, dd.Amount);
                    if (dd.Kind == DamageKind.Melee) _meleeDamage += dd.Amount; else _spellDamage += dd.Amount;
                    // Class inference (David, 2026-08-11): class-unique abilities vote,
                    // recent use wins — a rogue clicky in a warrior's hand loses to ten
                    // thousand real swings. Character-scoped, cleared on switch. Both
                    // damage kinds are offered: the touches print as SPELL damage ("… for
                    // 751 points of magic damage by Harm Touch"), so the old melee-only
                    // guard meant the Shadow Knight and Paladin rows could never fire.
                    // ClassInference itself drops anything an item proc could have cast.
                    _classInference.RecordAbilityUse(dd.Source, dd.Time);
                    // Damage spells label themselves by line shape, so classification is
                    // observed rather than looked up in a table.
                    if (dd.Kind == DamageKind.Spell && !dd.IsAux)
                    {
                        if (dd.OverTime)
                        {
                            _dotDamage += dd.Amount;
                            _spells.Learn(dd.Source, SpellCategory.DamageOverTime);
                        }
                        else
                        {
                            _directSpellDamage += dd.Amount;
                            _spells.Learn(dd.Source, SpellCategory.DirectDamage);
                            // A proc IS the absence: spell damage whose spell was never
                            // cast (Kerdude's Bolt of Flame, #85). The log prints the
                            // identical line for a cast nuke and a weapon/poison proc —
                            // the missing "You begin casting X." is the only tell. The
                            // generic "Direct spell" label can't name a proc, so it
                            // stays out. An item-proc line just before it names the
                            // vehicle ("... feels alive with power.").
                            if (dd.Source != "Direct spell"
                                && !(_spellCastAt.TryGetValue(dd.Source, out var castAt)
                                     && dd.Time - castAt <= ProcCastWindow))
                            {
                                var label = _lastItemProc is { } ip
                                    && dd.Time - ip.Time <= ProcItemWindow
                                    ? $"{dd.Source} · {ip.Item}" : dd.Source;
                                var p = _procs.TryGetValue(label, out var prev) ? prev : (0, 0L);
                                _procs[label] = (p.Item1 + 1, p.Item2 + dd.Amount);
                            }
                        }
                        TrackSpellBurst(dd.Source, dd.Target, dd.Amount, dd.Time);
                    }
                    if (!dd.IsAux)
                    {
                        _hitCount++;
                        if (dd.Critical) _critCount++;
                        if (dd.Note is { } note && note is not ("Critical" or "Crippling Blow"))
                            Bump(_specialHits, note);
                    }
                    // Melee hits are filed under the ability that took the skill over, when
                    // the game has told us about one — "You kick …" is Round Kick from the
                    // moment it says so, and the log never mentions it again.
                    var source = dd.Kind == DamageKind.Melee ? SkillName(dd.Source) : dd.Source;
                    if (dd.Amount > _maxHit) { _maxHit = dd.Amount; _maxHitDesc = $"{source} on {dd.Target}"; }
                    Ability(_damageBySource, source).Add(dd.Time, dd.Amount, dd.Critical);
                    TrackCombat(dd.Time, dd.Amount);
                    // TouchFight first: it opens the fight, and the opening hit belongs in
                    // that fight's breakdown as much as any later one.
                    TouchFight(dd.Target, dd.Time, dmgOut: dd.Amount);
                    if (_activeFights.TryGetValue(dd.Target, out var hitFight))
                        Ability(hitFight.ByAbility, source).Add(dd.Time, dd.Amount, dd.Critical);
                    if (_currentStance is { } st1)
                    {
                        var sv1 = _stanceAgg.TryGetValue(st1, out var stCur) ? stCur : (0.0, 0L);
                        _stanceAgg[st1] = (sv1.Item1, sv1.Item2 + dd.Amount);
                    }
                    if (_currentInvocation is { } inv1)
                    {
                        var iv1 = _invocationAgg.TryGetValue(inv1, out var invCur) ? invCur : (0.0, 0L);
                        _invocationAgg[inv1] = (iv1.Item1, iv1.Item2 + dd.Amount);
                    }
                    break;
                case MissEvent { Outgoing: true } m:
                    _missCount++;
                    TrackCombat(m.Time);
                    // Per-skill miss tallies (the breakdown's miss %). Filed under the
                    // same substituted skill name as the hits, or the whole row would
                    // split in two the moment Round Kick takes Kick over. A miss joins
                    // an EXISTING fight only — an attempt that never landed is not
                    // evidence enough to open one (the encounter rules are unchanged).
                    if (m.Ability.Length > 0)
                    {
                        var missSkill = SkillName(m.Ability);
                        Ability(_damageBySource, missSkill).AddMiss();
                        if (m.Target.Length > 0 && _activeFights.TryGetValue(m.Target, out var missFight))
                            Ability(missFight.ByAbility, missSkill).AddMiss();
                    }
                    break;
                case MissEvent m:
                    _avoidedIncoming++;
                    TrackCombat(m.Time);
                    break;
                case RuneBlockEvent rb:
                    _avoidedIncoming++;
                    _runeBlockCount++;
                    if (++_runeBlockStreak > _runeBlockStreakMax) _runeBlockStreakMax = _runeBlockStreak;
                    TrackCombat(rb.Time);
                    break;
                case DamageTakenEvent { Self: true } sdt:
                    // HP-cost casting, falls, drowning. Counted as damage taken so the
                    // Taken number is honest, but deliberately NOT a combat signal: no
                    // combat window, no encounter — a swim across a lake is not a fight,
                    // and a necromancer's own casting must not inflate combat seconds.
                    _damageTaken += sdt.Amount;
                    var selfAgg = _damageByAttacker.TryGetValue(sdt.Attacker, out var selfCur)
                        ? selfCur : (0, 0L);
                    _damageByAttacker[sdt.Attacker] = (selfAgg.Item1 + 1, selfAgg.Item2 + sdt.Amount);
                    break;
                case DamageTakenEvent dt:
                    // A "pet" attacking us means the charm broke — stop crediting it.
                    // Unless the charm only just landed, in which case this is the
                    // mob's in-flight swing finishing its round (see CharmSettleSeconds).
                    // And never a DoT TICK: a spell the creature cast before you charmed
                    // it keeps ticking afterwards, and a tick is not a decision to attack.
                    // The mez tracker already knew this (issue #32, and see
                    // DamageTakenEvent.OverTime); charm never got the same guard, so
                    // bjstrange's Choking ticked six seconds into a 5:31 charm and threw
                    // the clock away — the real fade minutes later then had no landing to
                    // measure and printed no hold (#135, charm5.txt).
                    if (IsPet(dt.Attacker) && !dt.OverTime && !CharmJustLanded(dt.Time)
                        && !SameNameDuplicateKnown(dt.Time))
                    { RecordCharmBreak(dt.Time); _petName = null; }
                    _damageTaken += dt.Amount;
                    if (dt.Melee) { _meleeHitsTaken++; _runeBlockStreak = 0; }
                    TouchFight(dt.Attacker, dt.Time, dmgIn: dt.Amount);
                    if (_activeFights.TryGetValue(dt.Attacker, out var inFight))
                        Ability(inFight.ByIncoming,
                            dt.Ability.Length > 0 ? dt.Ability : dt.Melee ? "Melee" : "Non-melee")
                            .Add(dt.Time, dt.Amount);
                    var atk = _damageByAttacker.TryGetValue(dt.Attacker, out var a) ? a : (0, 0L);
                    _damageByAttacker[dt.Attacker] = (atk.Item1 + 1, atk.Item2 + dt.Amount);
                    _lastDamageFrom = (dt.Attacker, dt.Time);
                    TrackCombat(dt.Time);
                    break;
                case HealEvent { Outgoing: true } h:
                    _healingDone += h.Amount; _healCount++;
                    // The divine invocation heals the party's lowest-health member for
                    // the mana of whatever you cast — a proc, not a cast, so its heal
                    // line carries no "by <spell>" clause and used to land in the
                    // "Unknown" bucket (David, 2026-08-09). While that invocation is
                    // being recited, an unattributed outgoing heal IS the invocation.
                    // ("Divine", not "Divine Invocation": the log says "You begin
                    // reciting the divine invocation." and the parser keeps the word.)
                    var healSpell = h.Spell == "Unknown" && _currentInvocation == "Divine"
                        ? "Divine Invocation" : h.Spell;
                    Ability(_healsBySpell, healSpell).Add(h.Time, h.Amount);
                    // Credited to the fight you were in, if any — see _healingFight.
                    if (_healingFight is { } hf && _activeFights.TryGetValue(hf, out var hFight))
                    {
                        hFight.Healed += h.Amount;
                        Ability(hFight.HealsBySpell, healSpell).Add(h.Time, h.Amount);
                    }
                    // Learning keys off what the LOG named (h.Spell, not the relabel):
                    // "Divine Invocation" isn't a castable spell and must not enter
                    // the learned spell catalog.
                    if (h.Spell != "Unknown")
                        _spells.Learn(h.Spell, h.OverTime ? SpellCategory.HealOverTime : SpellCategory.Heal);
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
                    if (h.Spell == "Rune") { _runeGainCount++; _runeGainPoints += h.Amount; }
                    // Incoming heals name the spell too ("healed you ... by Echoing
                    // Light") — a HoT someone keeps on you teaches the catalog even if
                    // you never cast one.
                    if (h.Spell != "Unknown")
                        _spells.Learn(h.Spell, h.OverTime ? SpellCategory.HealOverTime : SpellCategory.Heal);
                    break;
                case ConsiderEvent con:
                    // Deliberate targeting: a /con names the creature you care about
                    // without a swing landed — it competes with recent fights for the
                    // target-drops surfaces (David, 2026-08-06).
                    _lastConsider = (con.Name, con.Time);
                    // And the con LINE names a level — the one place the log ever does.
                    // Bounds, not last-seen: same-named spawns roam a range (#65).
                    if (con.Level > 0)
                    {
                        var conAgg = Mob(con.Name);
                        conAgg.LevelMin = conAgg.LevelMin == 0
                            ? con.Level : Math.Min(conAgg.LevelMin, con.Level);
                        conAgg.LevelMax = Math.Max(conAgg.LevelMax, con.Level);
                    }
                    break;
                case RegenTickEvent:
                    _regenTicks++;
                    // Estimated regen healing (David, 2026-08-06): the tick line names no
                    // spell and no amount, so this is attribution-by-own-cast × a per-tick
                    // value — the player's Options override when set (they can read the
                    // real number off their health bar; instruments/ranks raise it past
                    // the wiki base), else the wiki base. No cast seen → count only.
                    if (_lastRegenCast is { } regenSpell)
                    {
                        var perTick = RegenPerTickOverride > 0
                            ? RegenPerTickOverride
                            : RegenCatalog.PerTick(regenSpell) ?? 0;
                        _regenEstimated += perTick;
                        _regenSpell = regenSpell;
                    }
                    break;
                case LootEvent l:
                    var cur = _loot.TryGetValue(l.Item, out var lv) ? lv : (0, l.Source);
                    _loot[l.Item] = (cur.Item1 + l.Count, l.Source);
                    _lootCount += l.Count;
                    // Loot lines name the corpse — explicit creature correlation (CORRELATE-005).
                    Bump(Mob(l.Source).Loot, l.Item);
                    Mob(l.Source).LootLast[l.Item] = l.Time;
                    // Quest ledger rides the same event; the store's own filter and
                    // time high-water mark decide whether anything actually lands.
                    // Loot-MERGE lines ("looted a Belt +2 ... to create a Belt +4") are
                    // net zero for the quest count: the corpse's item and the held item
                    // became one, so possession didn't change (David, 2026-08-07 —
                    // "ready ×17" was counting every merge-consumed belt).
                    if (l.UpgradeResult is null)
                    {
                        if (!StoresSuppressed)
                            QuestStore?.RecordLoot(AaCharacterKey, l.Item, l.Count, l.Time);
                    }
                    else
                        Bump(_upgraded, l.UpgradeResult);   // the created "+N" form
                    break;
                case CraftEvent c:
                    Bump(_crafted, c.Item);
                    // A manual merge turned two held items into one.
                    if (!StoresSuppressed)
                        QuestStore?.RecordConsumed(AaCharacterKey, c.Item, 1, c.Time);
                    break;
                case ItemDestroyedEvent d:
                    _lastDestroyed = (d.Item, d.Count, d.Time);
                    if (!StoresSuppressed)
                        QuestStore?.RecordConsumed(AaCharacterKey, d.Item, d.Count, d.Time);
                    break;
                case MoneyEvent { Vendor: true } m:
                    _vendorCopper += m.Copper; _salesCount++;
                    // A sale from the advanced loot window logs no item name; the
                    // "successfully destroyed" line just before it names what was sold.
                    var (soldName, soldCount) = m.Item is { } named ? (named, 1)
                        : _lastDestroyed is { } ld && m.Time - ld.Time <= RewardWindow
                            ? (ld.Item, ld.Count)
                            : ("Loot window sale", 1);
                    var sv = _soldItems.TryGetValue(soldName, out var sc) ? sc : (0, 0L);
                    _soldItems[soldName] = (sv.Item1 + soldCount, sv.Item2 + m.Copper);
                    // A NAMED sale is a held item leaving. Nameless loot-window sales
                    // already subtracted via their preceding "successfully destroyed"
                    // line — subtracting here too would double-count the exit.
                    if (m.Item is { } soldItem && !StoresSuppressed)
                        QuestStore?.RecordConsumed(AaCharacterKey, soldItem, 1, m.Time);
                    break;
                case MoneyEvent m:
                    _copper += m.Copper; _coinDrops++;
                    if (m.Copper > _biggestDrop) _biggestDrop = m.Copper;
                    // Coin right after a kill belongs to that creature; coin before the
                    // kill line (EQL's usual order) waits for the kill to claim it.
                    if (_lastKill is { } lk1 && m.Time - lk1.Time <= RewardWindow)
                        TrackMobCoin(Mob(lk1.Name), m.Copper);
                    else
                        _pendingCoin.Add((m.Time, m.Copper));
                    break;
                case XpEvent x:
                    _xpPercent += x.Percent; _xpTicks++;
                    _xpSinceLevel += x.Percent;
                    if (_lastKill is { } lk2 && x.Time - lk2.Time <= RewardWindow)
                        Mob(lk2.Name).Xp += x.Percent;
                    else
                        _pendingXp.Add((x.Time, x.Percent));
                    break;
                case LevelEvent lv2:
                    _levels.Add((lv2.Time, lv2.Level));
                    _xpSinceLevel = 0;
                    break;
                case AaEvent aa:
                    _aaGained += aa.Points; _aaTotal = aa.TotalPoints;
                    break;
                case AaPurchaseEvent ap:
                    // Highest rank wins regardless of replay order; a re-observed rank-1
                    // "gained" after an "improved" (log replay) must not regress the ledger.
                    if (!_aaAbilities.TryGetValue(ap.Ability, out var known) || ap.Rank > known.Rank)
                        _aaAbilities[ap.Ability] = (ap.Rank, ap.Time);
                    if (!StoresSuppressed)
                        AaStore?.Record(AaCharacterKey, ap.Ability, ap.Rank, ap.Time);
                    break;
                case StanceEvent stc:
                    // Close the open combat window under the OLD stance before switching,
                    // so its time is attributed correctly.
                    CloseCombatLocked();
                    _currentStance = stc.Stance;
                    if (!_stanceAgg.ContainsKey(stc.Stance)) _stanceAgg[stc.Stance] = (0, 0);
                    break;
                case InvocationEvent inv:
                    // Same attribution boundary as a stance change.
                    CloseCombatLocked();
                    _currentInvocation = inv.Invocation;
                    if (!_invocationAgg.ContainsKey(inv.Invocation)) _invocationAgg[inv.Invocation] = (0, 0);
                    break;
                case AutoSellEvent asell:
                    var lcur = _loot.TryGetValue(asell.Item, out var lval) ? lval : (0, asell.Source);
                    _loot[asell.Item] = (lcur.Item1 + asell.Count, asell.Source);
                    _lootCount += asell.Count;
                    var mobLoot = Mob(asell.Source).Loot;
                    mobLoot[asell.Item] = mobLoot.TryGetValue(asell.Item, out var mlc) ? mlc + asell.Count : asell.Count;
                    Mob(asell.Source).LootLast[asell.Item] = asell.Time;
                    _vendorCopper += asell.Copper; _salesCount++;
                    var scur = _soldItems.TryGetValue(asell.Item, out var sval) ? sval : (0, 0L);
                    _soldItems[asell.Item] = (scur.Item1 + asell.Count, scur.Item2 + asell.Copper);
                    break;
                case SkillUpEvent su:
                    var sk = _skills.TryGetValue(su.Skill, out var skv) ? skv : (0, 0);
                    _skills[su.Skill] = (sk.Item1 + 1, Math.Max(sk.Item2, su.Value));
                    break;
                case SkillSubstitutionEvent sub:
                    // Hits already recorded under the old skill stay there — they really were
                    // plain kicks. Everything from here is the ability that replaced it.
                    _skillAliases[sub.Replaced] = sub.Ability;
                    break;
                case FactionEvent f:
                    // Faction lines follow their kill within the reward window — the
                    // per-creature ledger feeds the wiki pack's stat block (#65).
                    if (_lastKill is { } lkf && f.Time - lkf.Time <= RewardWindow)
                    {
                        var factions = Mob(lkf.Name).Factions;
                        var prevHit = factions.TryGetValue(f.Faction, out var ph) ? ph : (0, 0);
                        factions[f.Faction] = (prevHit.Item1 + 1, f.Delta);
                    }
                    var fv = _faction.TryGetValue(f.Faction, out var fcur) ? fcur : (0, 0, false, false);
                    // Capped is sticky for the session: standing pinned at the cap is why
                    // the number stopped moving, and that's worth saying even if earlier
                    // kills still adjusted it. Direction follows the latest capped line —
                    // "maxed" and "bottomed" are different news (#86).
                    _faction[f.Faction] = (fv.Item1 + 1, fv.Item2 + f.Delta, fv.Item3 || f.Capped,
                        f.Capped ? f.CappedDown : fv.Item4);
                    break;
                case ZoneEvent z:
                    if (_zones.Count == 0 || !string.Equals(_zones[^1].Zone, z.Zone, StringComparison.OrdinalIgnoreCase))
                        _zones.Add((z.Time, z.Zone));
                    _lastLoc = null;   // a /loc from the previous zone is a lie here
                    _locTrail.Clear();
                    break;
                case LocationEvent loc:
                    _lastLoc = loc;    // the map window's player marker
                    // The breadcrumb trail: /locs in this zone, oldest first, bounded —
                    // and thinned by distance, because the overlapping-keybind trick
                    // (the /loc social bound to W) fires one per movement keypress:
                    // without thinning, 80 points would cover one corridor. Points
                    // closer than ~25 units to the last crumb refresh the marker but
                    // don't spend a slot, so the trail spans real ground.
                    if (_locTrail.Count == 0 || Distance(_locTrail[^1], loc) >= 25)
                    {
                        _locTrail.Add(loc);
                        if (_locTrail.Count > 80) _locTrail.RemoveAt(0);
                    }
                    break;
                case FizzleEvent: _fizzles++; break;
                case ResistEvent rz:
                    _resists++;
                    {
                        var rzKey = SpellCatalog.BaseName(rz.Spell);
                        var so = _spellOutcomes.GetValueOrDefault(rzKey);
                        _spellOutcomes[rzKey] = (so.Casts, so.Resists + 1, so.Blocked);
                    }
                    break;
                case ItemProcEvent iproc: _lastItemProc = (iproc.Item, iproc.Time); break;
                case SessionMarkerEvent mk:
                    _markers.Add((mk.Time, mk.Label));
                    break;
            }
        }
        // REL-001: never invoke user callbacks while holding the stats lock.
        if (rolled)
        {
            if (finalSnapshot is not null) SessionEnding?.Invoke(finalSnapshot);
            SessionRolledOver?.Invoke();
        }
    }

    /// <summary>Drop high-frequency combat/heal entries older than the retention
    /// cutoff. In-place compaction rather than RemoveAll so the tracked scan's index
    /// can follow: every removed entry below <see cref="_trackedScanIndex"/> shifts
    /// the same unscanned entry one slot left (perf audit #10). No accumulator ever
    /// rewinds — the combat kinds are ones no tracked rule can match, and RawLineEvents
    /// (perf audit #5: previously retained whole-session) are kept countable by the
    /// Text-rule ACCUMULATORS, which survive pruning by construction — see
    /// ScanTrackedLocked's text-preservation rule.</summary>
    private void PruneJournalLocked(DateTime cutoff)
    {
        var removedBeforeScanIndex = 0;
        var write = 0;
        for (var read = 0; read < _journal.Count; read++)
        {
            var j = _journal[read];
            if (j.Time < cutoff && j is DamageDealtEvent
                or DamageTakenEvent or MissEvent or RuneBlockEvent or ThirdMeleeEvent
                or ThirdDotEvent or ThirdSchoolEvent or ThirdMissEvent or HealEvent
                or RegenTickEvent or RawLineEvent)
            {
                if (read < _trackedScanIndex) removedBeforeScanIndex++;
                continue;
            }
            _journal[write++] = j;
        }
        _journal.RemoveRange(write, _journal.Count - write);
        _trackedScanIndex -= removedBeforeScanIndex;
    }

    /// <summary>#130 (bjstrange): close the charm-hold clock at a break and remember
    /// how long it held, keyed by the break time so the fade alert's label can carry
    /// it ("Charm (a gnoll) — held 4:32").</summary>
    /// <summary>Did the charm land so recently that a hit on us is the mob's own
    /// in-flight swing rather than a break? (#135 — see <see cref="CharmSettleSeconds"/>.)</summary>
    private bool CharmJustLanded(DateTime at) =>
        (_charmHold?.LandedAt ?? _charmProvisional?.LandedAt) is { } landed
        && at >= landed && (at - landed).TotalSeconds <= CharmSettleSeconds;

    /// <summary>Do we know a second creature shares the pet's name right now? Then a
    /// same-named attacker hitting us proves nothing, and the claim survives until the
    /// wear-off line settles it (#135 — see <see cref="SameNameProofSeconds"/>).</summary>
    private bool SameNameDuplicateKnown(DateTime at) =>
        (_sameNameProofAt is { } seen
            && at >= seen && (at - seen).TotalSeconds <= SameNameProofSeconds)
        || PetHeldAt(at);

    /// <summary>Was the pet under a HOLD order at this moment? Then it did not start this
    /// attack, so a same-named attacker is someone else (#135, charm6.txt). Unlike the
    /// attacked-its-own-name proof this does not expire on a timer — hold is a state the
    /// pet stays in until released, and the release line says so.</summary>
    private bool PetHeldAt(DateTime at) =>
        _petHeldSince is { } since && at >= since;

    private void RecordCharmBreak(DateTime at)
    {
        var landed = _charmHold?.LandedAt ?? _charmProvisional?.LandedAt;
        _charmHold = null;
        _charmProvisional = null;
        _sameNameProofAt = null;
        _petHeldSince = null;   // the hold belonged to the pet we just stopped claiming
        if (landed is not { } l) return;
        var held = (at - l).TotalSeconds;
        if (held <= 0) return;
        _charmHoldByBreak[at] = held;
        // A new hold can retroactively relabel an already-scanned fade (FadeLabel
        // tolerates ordering skew), so the incremental tracked scan must rebuild.
        _charmHoldRevision++;
        if (_charmHoldByBreak.Count > 64)
            foreach (var old in _charmHoldByBreak.Keys.OrderBy(k => k)
                         .Take(_charmHoldByBreak.Count - 64).ToList())
                _charmHoldByBreak.Remove(old);
    }

    /// <summary>#135 (bjstrange): a charm fade line landing within the skew window of
    /// an already-recorded break is that break's delayed echo, not a new break. Acting
    /// on it would measure a bogus tiny hold from any re-charm in between AND null the
    /// re-charm's live claim — the later real break then has no landing to measure from,
    /// which is exactly the missing "held M:SS" bjstrange kept seeing.</summary>
    private bool IsCharmBreakEcho(DateTime at) => _charmHoldByBreak.Keys
        .Any(k => k <= at && (at - k).TotalSeconds <= CharmFadeSkewSeconds);

    /// <summary>Journal label for a fade row/alert — a charm break gets its hold
    /// duration appended (#130). The lookup tolerates ordering skew (#135,
    /// bjstrange: "doesn't always trigger the time"): when the pet turning on you
    /// is what breaks the charm, the hold gets recorded at the ATTACK's timestamp
    /// and the fade line prints a few seconds later — so an exact-time miss falls
    /// back to the most recent hold recorded within the skew window.</summary>
    private string FadeLabel(SpellWornOffEvent wo)
    {
        var label = wo.Target.Length > 0 ? $"{wo.Spell} ({wo.Target})" : wo.Spell;
        if (!_charmHoldByBreak.TryGetValue(wo.Time, out var held))
        {
            var near = _charmHoldByBreak.Keys
                .Where(k => k <= wo.Time && (wo.Time - k).TotalSeconds <= CharmFadeSkewSeconds)
                .OrderByDescending(k => k)
                .Cast<DateTime?>()
                .FirstOrDefault();
            if (near is not { } n) return label;
            held = _charmHoldByBreak[n];
        }
        var t = TimeSpan.FromSeconds(held);
        var text = t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes}:{t.Seconds:00}";
        return label + $" — held {text}";
    }

    /// <summary>The filters that mean "my crowd control of a MOB ended" — the ones a
    /// first-person self-fade line must never satisfy (see the BuffFadeEvent match).</summary>
    private static bool IsCcFilter(SpellFilter f) => f is SpellFilter.AnyCrowdControl
        or SpellFilter.Charm or SpellFilter.Mesmerize or SpellFilter.Root
        or SpellFilter.Lull or SpellFilter.Stun;

    /// <summary>A SpellFade rule matches either one named spell or a whole class of them.
    /// Class filters are evaluated against the catalog, so they keep working as a
    /// character levels into new spells and higher ranks.</summary>
    private bool SpellFadeMatches(TrackedRule rule, string spell) => rule.SpellFilter switch
    {
        SpellFilter.ByName => rule.Matches(spell),
        SpellFilter.AnySpell => true,
        SpellFilter.Buff => FadeMessageCatalog.Default.FindBySpell(spell) is { } fade
            && FadeMessageCatalog.IsBeneficialCategory(fade.Category),
        SpellFilter.AnyCrowdControl => _spells.IsCrowdControl(spell),
        _ => rule.FilterCategory is { } wanted && _spells.Classify(spell) == wanted,
    };

    private bool BuffFadeMatches(TrackedRule rule, BuffFadeEvent fade) => rule.SpellFilter switch
    {
        SpellFilter.ByName => rule.Matches(fade.Label)
            || fade.Spells.Any(sp => rule.Matches(sp)),
        SpellFilter.AnySpell => true,
        SpellFilter.Buff => FadeMessageCatalog.IsBeneficialCategory(fade.Category),
        SpellFilter.AnyCrowdControl => false,
        _ => rule.FilterCategory is { } wanted
            && (string.Equals(fade.Category, wanted.ToString(), StringComparison.OrdinalIgnoreCase)
                || fade.Spells.Any(sp => _spells.Classify(sp) == wanted)),
    };

    /// <summary>
    /// Group a spell's damage into casts. Hits on distinct creatures inside
    /// <see cref="AreaBurstWindow"/> belong to one cast; a hit after the window (or a
    /// repeat on a creature already in this burst, which means it landed again) starts a
    /// new one. DoT ticks therefore count as separate casts, which is right — each tick
    /// is its own damage event and the spell was only cast once, so per-cast figures stay
    /// meaningful only for direct damage. Callers filter on MaxTargets to find real AoEs.
    /// </summary>
    private void TrackSpellBurst(string spell, string target, int amount, DateTime time)
    {
        var key = SpellCatalog.BaseName(spell);
        if (_openBursts.TryGetValue(key, out var burst) &&
            time - burst.Start <= AreaBurstWindow && !burst.Targets.Contains(target))
        {
            burst.Targets.Add(target);
            burst.Damage += amount;
            return;
        }
        if (burst is not null) CloseBurst(key, burst);
        var fresh = new SpellBurst { Start = time, Damage = amount };
        fresh.Targets.Add(target);
        _openBursts[key] = fresh;
    }

    /// <summary>
    /// Per-cast figures for spells seen hitting more than one creature at once. The
    /// still-open burst is folded in so a spell shows up the moment it lands, rather than
    /// waiting for the next cast to close it out.
    /// </summary>
    private List<AreaSpellInfo> BuildAreaSpells()
    {
        var totals = new Dictionary<string, CastAgg>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, agg) in _castAgg)
            totals[key] = new CastAgg
            {
                Casts = agg.Casts, TargetHits = agg.TargetHits,
                Damage = agg.Damage, MaxTargets = agg.MaxTargets,
            };
        foreach (var (key, burst) in _openBursts)
        {
            var agg = totals.TryGetValue(key, out var a) ? a : totals[key] = new CastAgg();
            agg.Casts++;
            agg.TargetHits += burst.Targets.Count;
            agg.Damage += burst.Damage;
            agg.MaxTargets = Math.Max(agg.MaxTargets, burst.Targets.Count);
        }
        return totals
            .Where(kv => kv.Value.MaxTargets >= 2 && kv.Value.Casts > 0)
            .Select(kv => new AreaSpellInfo(
                kv.Key, kv.Value.Casts,
                kv.Value.TargetHits / (double)kv.Value.Casts,
                kv.Value.MaxTargets,
                kv.Value.Damage,
                kv.Value.Damage / (double)kv.Value.Casts))
            .OrderByDescending(x => x.Damage)
            .ToList();
    }

    private void CloseBurst(string key, SpellBurst burst)
    {
        var agg = _castAgg.TryGetValue(key, out var a) ? a : _castAgg[key] = new CastAgg();
        agg.Casts++;
        agg.TargetHits += burst.Targets.Count;
        agg.Damage += burst.Damage;
        agg.MaxTargets = Math.Max(agg.MaxTargets, burst.Targets.Count);
    }

    /// <summary>The game sometimes refers to the pet generically instead of by name —
    /// confirmed in real logs by "Your pet's Tangling Weeds spell has worn off.". Nothing
    /// but your own pet is ever called this, so it needs no prior identification: it works
    /// for a summoned pet that has never been given an attack order, which is the one case
    /// the "Attacking … Master." line can't cover.</summary>
    private const string GenericPetName = "Your pet";

    private bool IsPet(string name)
    {
        var normalized = LogParser.Normalize(name);
        if (string.Equals(normalized, GenericPetName, StringComparison.OrdinalIgnoreCase))
            return true;
        return _petName is not null &&
            string.Equals(normalized, _petName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A "Master" tell proves the pet is ours — upgrade any provisional damage.</summary>
    private void ConfirmPet(string name)
    {
        _petName = name;
        if (_petConfirmed) return;
        _petConfirmed = true;
        if (_damageBySource.Remove($"Pet? ({name})", out var provisional))
        {
            var cur = Ability(_damageBySource, $"Pet ({name})");
            cur.Count += provisional.Count;
            cur.Total += provisional.Total;
            cur.Crits += provisional.Crits;
            cur.ActiveSeconds += provisional.ActiveSeconds;
            if (provisional.LastTime > cur.LastTime) cur.LastTime = provisional.LastTime;
        }
    }

    private void AddTimelineDamage(DateTime t, int amount)
    {
        var bucket = t.Ticks / TimeSpan.TicksPerMinute;
        _damageTimeline[bucket] = _damageTimeline.GetValueOrDefault(bucket) + amount;
    }

    /// <summary>Pet damage is the player's damage, reported under a "Pet (Name)" source
    /// ("Pet? (Name)" while the charm is only suspected from a blink). The ability behind
    /// each hit — the melee skill, or the spell the log names — is also totalled on its own
    /// so the single pet row can be broken down.</summary>
    private void AddPetDamage(DateTime t, int amount, DamageKind kind, string target, string ability,
        bool critical = false)
    {
        _damageDealt += amount;
        AddTimelineDamage(t, amount);
        if (kind == DamageKind.Melee) _meleeDamage += amount; else _spellDamage += amount;
        // No name yet means this arrived via the generic "Your pet" form — still certainly
        // ours, so it gets the confirmed label rather than the provisional one.
        var label = _petName is null ? "Pet"
            : _petConfirmed ? $"Pet ({_petName})" : $"Pet? ({_petName})";
        if (amount > _maxHit) { _maxHit = amount; _maxHitDesc = $"{label} on {target}"; }
        // Pet crits carry the same "(Critical)" annotation your own hits do, so the pet rows
        // show a real crit % rather than a blank one. Pet hits stay out of YOUR accuracy
        // counters, though — those are about what you swung, and pet misses aren't credited.
        Ability(_damageBySource, label).Add(t, amount, critical);
        // A verb the melee pattern matched but the mapping didn't recognise still counts;
        // it just lands in a generic bucket rather than being dropped.
        Ability(_petAbilities, ability.Length > 0 ? ability
            : kind == DamageKind.Melee ? "Melee" : "Spell").Add(t, amount, critical);
        TrackCombat(t, amount);
        TouchFight(target, t, dmgOut: amount);
        // The pet's damage joins the fight's ability rows as one labeled row (mirrors the
        // session list, where the pet is a single row with its own split behind a click),
        // and the per-fight pet split keyed by ability alongside it.
        if (_activeFights.TryGetValue(target, out var petFight))
        {
            Ability(petFight.ByAbility, label).Add(t, amount, critical);
            Ability(petFight.PetAbilities, ability.Length > 0 ? ability
                : kind == DamageKind.Melee ? "Melee" : "Spell").Add(t, amount, critical);
        }
    }

    private MobAgg Mob(string name) =>
        _mobs.TryGetValue(name, out var m) ? m : _mobs[name] = new MobAgg();

    private static AbilityAgg Ability(Dictionary<string, AbilityAgg> d, string key) =>
        d.TryGetValue(key, out var a) ? a : d[key] = new AbilityAgg();

    /// <summary>Matched log lines become row labels, and a raid announcement can be a
    /// paragraph. Trim to something a 320px-wide card and a mini-dashboard chip can show.</summary>
    private static string Ellipsize(string line, int max = 64) =>
        line.Length <= max ? line : line[..(max - 1)].TrimEnd() + "…";

    /// <summary>A kill claims the xp/coin logged just before its kill line (EQL order);
    /// anything older than the window is dropped as uncorrelatable.</summary>
    private void ClaimPendingRewards(string target, DateTime killTime)
    {
        var mob = Mob(target);
        foreach (var p in _pendingXp)
            if (killTime - p.Time <= RewardWindow) mob.Xp += p.Percent;
        foreach (var p in _pendingCoin)
            if (killTime - p.Time <= RewardWindow) TrackMobCoin(mob, p.Copper);
        _pendingXp.Clear();
        _pendingCoin.Clear();
    }

    /// <summary>One coin line ≈ one corpse's purse: besides the running total, keep the
    /// smallest and largest single drop, which is exactly the wiki's money format
    /// ("0 - 7 Golds") and the range-not-point reporting Frankthetankk asked for (#65).</summary>
    private static void TrackMobCoin(MobAgg mob, long copper)
    {
        mob.Copper += copper;
        if (mob.CoinMin < 0 || copper < mob.CoinMin) mob.CoinMin = copper;
        if (copper > mob.CoinMax) mob.CoinMax = copper;
    }

    private void TouchFight(string target, DateTime t, long dmgOut = 0, long dmgIn = 0)
    {
        if (!_activeFights.TryGetValue(target, out var f))
            _activeFights[target] = f = new ActiveFight { Start = t };
        f.Last = t;
        f.DmgOut += dmgOut;
        f.DmgIn += dmgIn;
        _healingFight = target;
    }

    private void FinalizeFight(string target, DateTime t, string outcome)
    {
        if (!_activeFights.Remove(target, out var f)) return;
        if (_healingFight == target) _healingFight = null;   // heals after this belong to no fight
        var dur = Math.Max(1, ((outcome == "Killed" ? t : f.Last) - f.Start).TotalSeconds);
        // Every retained encounter carries its full breakdown now (HISTORY fight review,
        // 2026-08-04): the 300-encounter prune bounds the cost, and archived sessions
        // get per-fight detail in the History window.
        var byAbility = Breakdown(f.ByAbility);
        var heals = Breakdown(f.HealsBySpell);
        var byIncoming = Breakdown(f.ByIncoming);
        _encounters.Add(new EncounterInfo(target, f.Start, dur, f.DmgOut, f.DmgIn,
            f.DmgOut / dur, outcome, f.Healed)
        { ByAbility = byAbility, HealsBySpell = heals, ByIncoming = byIncoming,
          PetAbilities = Breakdown(f.PetAbilities) });
        if (_encounters.Count > 300) _encounters.RemoveRange(0, 100);
        var mob = Mob(target);
        mob.Encounters++;
        mob.FightSeconds += dur;
    }

    /// <summary>
    /// The encounter worth showing at the top of the card: the current PULL (open fights
    /// plus anything that finished within the pull gap of them — an add killed two seconds
    /// ago is still this encounter), or the last completed pull between pulls. Same
    /// grouping the History review uses, so the live card and the archive agree on what
    /// "the fight" was (per David, 2026-08-04).
    /// </summary>
    /// <summary>
    /// The combat journal's events inside a time window, for the fight timeline: your
    /// hits/misses/resists, the pet's and the mob's, casts and rune blocks — every mark
    /// the timeline draws. A snapshot copy under the lock; records are immutable, so
    /// callers can walk it off-thread. Bounded by CombatJournalRetention (40 min): a
    /// window older than that honestly comes back empty rather than partial.
    /// </summary>
    public List<GameEvent> JournalWindow(DateTime from, DateTime to)
    {
        lock (_lock)
            return _journal.Where(e => e.Time >= from && e.Time <= to && e is
                    DamageDealtEvent or MissEvent or ResistEvent or FizzleEvent
                    or DamageTakenEvent or RuneBlockEvent or SpellCastEvent
                    or ThirdMeleeEvent or ThirdDotEvent or ThirdSchoolEvent or KillEvent
                    // Mode boundaries for the timeline's phase markers — the whitelist
                    // silently starved them at first (found via fixture capture: the
                    // unit test fed the builder directly and never crossed this layer).
                    or StanceEvent or InvocationEvent)
                .OrderBy(e => e.Time)
                .ToList();
    }

    private LastFightInfo? BuildLastFight()
    {
        // Materialize open fights as in-progress encounters so they group with the
        // recently finalized ones. 32-fight tail: a pull chain longer than that is
        // ancient history for a "current fight" card, and grouping stays O(small).
        var pool = _encounters.TakeLast(32).Concat(_activeFights.Select(kv =>
            new EncounterInfo(kv.Key, kv.Value.Start,
                Math.Max(1, (kv.Value.Last - kv.Value.Start).TotalSeconds),
                kv.Value.DmgOut, kv.Value.DmgIn,
                kv.Value.DmgOut / Math.Max(1, (kv.Value.Last - kv.Value.Start).TotalSeconds),
                "Fighting", kv.Value.Healed)
            {
                ByAbility = Breakdown(kv.Value.ByAbility),
                HealsBySpell = Breakdown(kv.Value.HealsBySpell),
                ByIncoming = Breakdown(kv.Value.ByIncoming),
                PetAbilities = Breakdown(kv.Value.PetAbilities),
            })).ToList();
        if (pool.Count == 0) return null;

        var pull = EncounterGrouping.Group(pool)[^1];
        var inProgress = pull.Fights.Any(f => f.Outcome == "Fighting");
        var outcome = inProgress ? "Fighting"
            : pull.Fights.All(f => f.Outcome == "Killed") ? "Killed"
            : pull.Fights.Count == 1 ? pull.Fights[0].Outcome   // no self-referential name prefix
            : string.Join(" · ", pull.Fights.Where(f => f.Outcome is not ("Killed" or "Fighting"))
                .Select(f => $"{f.Name} {f.Outcome}").Distinct());
        return new LastFightInfo(pull.Title, pull.DurationSeconds, pull.DamageOut,
            pull.DamageIn, pull.Healed, pull.Dps, pull.Healed / pull.DurationSeconds,
            outcome, inProgress, pull.ByAbility, pull.HealsBySpell, pull.ByIncoming)
        { Fights = pull.Fights, PetAbilities = pull.PetAbilities, Start = pull.Start };
    }

    /// <summary>How long a finished fight's creature stays "the target" for the Loot
    /// card's drops block — long enough to read the list after the kill, short enough
    /// that walking away really clears it.</summary>
    private static readonly TimeSpan TargetLinger = TimeSpan.FromSeconds(45);

    /// <summary>The creatures to show target drops for. The log never says which one is
    /// actually TARGETED, so in a multi-creature pull the pool is EVERY open fight
    /// (David's live report, 2026-08-06: picking the most-recently-touched one made the
    /// window cycle with whoever swung last and reset its lookups). Ordered oldest fight
    /// first so the list is stable while the pull lasts, capped at 5 — an AE farm pull
    /// doesn't need thirty wiki lookups. Between fights: the newer of the last finished
    /// fight and the last /consider, each within <see cref="TargetLinger"/>.</summary>
    private List<string> BuildCurrentTargetsLocked()
    {
        if (_activeFights.Count > 0)
            return _activeFights.OrderBy(kv => kv.Value.Start)
                .Select(kv => kv.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5).ToList();
        if (_lastEventTime is not { } last) return [];

        var best = ""; var bestAt = DateTime.MinValue;
        if (_encounters.Count > 0)
        {
            var e = _encounters[^1];
            var end = e.Start.AddSeconds(e.DurationSeconds);
            if (last - end <= TargetLinger) { best = e.Name; bestAt = end; }
        }
        if (_lastConsider is { } con && last - con.Time <= TargetLinger && con.Time > bestAt)
            best = con.Name;
        return best.Length > 0 ? [best] : [];
    }

    /// <summary>The highest rank of one AA the character is known to own (0 = none seen) —
    /// the cheap single-name probe duration models use per event, where building the whole
    /// snapshot ledger would be waste. Same union as <see cref="BuildAaLedgerLocked"/>.</summary>
    public int AaRank(string ability)
    {
        lock (_lock)
        {
            var rank = _aaAbilities.TryGetValue(ability, out var seen) ? seen.Rank : 0;
            if (AaStore is { } store && AaCharacterKey.Length > 0
                && store.For(AaCharacterKey).TryGetValue(ability, out var stored) && stored.Rank > rank)
                rank = stored.Rank;
            return rank;
        }
    }

    /// <summary>The AA ledger a snapshot shows: union of this run's observations and the
    /// durable store, highest rank per ability — the store is what survives log truncation,
    /// the in-memory side is what a store-less test (or first run) sees.</summary>
    private List<AaAbilityInfo> BuildAaLedgerLocked()
    {
        var merged = new Dictionary<string, (int Rank, DateTime Time)>(_aaAbilities, StringComparer.OrdinalIgnoreCase);
        if (AaStore is { } store && AaCharacterKey.Length > 0)
            foreach (var (name, e) in store.For(AaCharacterKey))
                if (!merged.TryGetValue(name, out var known) || e.Rank > known.Rank)
                    merged[name] = (e.Rank, e.Time);
        return merged.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new AaAbilityInfo(kv.Key, kv.Value.Rank, kv.Value.Time)).ToList();
    }

    private static List<SourceDamage> Breakdown(Dictionary<string, AbilityAgg> d) =>
        d.OrderByDescending(kv => kv.Value.Total)
            .Select(kv => new SourceDamage(kv.Key, kv.Value.Count, kv.Value.Total,
                kv.Value.Crits, kv.Value.ActiveSeconds)
            { MinHit = kv.Value.Min, MaxHit = kv.Value.Max, Misses = kv.Value.Misses })
            .ToList();

    private void SweepStaleFights(DateTime now)
    {
        if (_activeFights.Count == 0) return;
        List<string>? stale = null;
        foreach (var (name, f) in _activeFights)
            if (now - f.Last > EncounterTimeout)
                (stale ??= []).Add(name);
        if (stale is null) return;
        foreach (var name in stale)
            FinalizeFight(name, now, "Timeout");   // ENCOUNTER-004: no kill line seen
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
            var span = Math.Max(1, (cl - cs).TotalSeconds);
            _closedCombatSeconds += span;
            _closedCombatDamage += _combatDamage;
            _combatSpans.Add((cs, cl));
            if (_combatSpans.Count > 2048) _combatSpans.RemoveRange(0, 1024);
            // Attribute the combat time to whichever stance was active (STANCE-002-lite).
            if (_currentStance is { } st)
            {
                var v = _stanceAgg.TryGetValue(st, out var cur) ? cur : (0.0, 0L);
                _stanceAgg[st] = (v.Item1 + span, v.Item2);
            }
            if (_currentInvocation is { } inv)
            {
                var v = _invocationAgg.TryGetValue(inv, out var cur) ? cur : (0.0, 0L);
                _invocationAgg[inv] = (v.Item1 + span, v.Item2);
            }
        }
        _combatStart = null; _combatLast = null; _combatDamage = 0;
    }

    /// <summary>Drop a camp/segment marker (wall-clock timestamped).</summary>
    public void AddMarker(string label) => Apply(new SessionMarkerEvent(DateTime.Now, label));

    public void Reset()
    {
        lock (_lock) ResetLocked();
    }

    /// <summary>Wipe character-scoped state that outlives session resets (the AA ledger).
    /// Called on character switch, where the whole new log is replayed anyway — NOT part of
    /// <see cref="ResetLocked"/>, because the initial full-log ingest replays session-gap
    /// resets and clearing there would forget every purchase made before the last gap.
    /// Caveat (until the ledger gets a durable store): log truncation erases purchase
    /// lines, so a restart after auto-empty starts the ledger over.</summary>
    public void ClearCharacterState()
    {
        lock (_lock)
        {
            _aaAbilities.Clear();
            _classInference.Clear();   // the next character's swings vote fresh
            _version++;   // both feed snapshots — the memo must not serve stale ones
        }
    }

    private void ResetLocked()
    {
        _version++;
        _sessionStart = null; _lastEventTime = null;
        _yourKills.Clear(); _partyKillsByTarget.Clear(); _partyKillsByKiller.Clear(); _deaths.Clear();
        _damageDealt = _meleeDamage = _spellDamage = 0;
        _hitCount = _critCount = _missCount = 0; _maxHit = 0; _maxHitDesc = "";
        _damageBySource.Clear(); _petAbilities.Clear(); _specialHits.Clear();
        _damageTaken = 0; _avoidedIncoming = 0; _meleeHitsTaken = 0; _damageByAttacker.Clear();
        _lastDamageFrom = null;
        _healingDone = 0; _healCount = 0; _healingReceived = 0;
        _healsByHealer.Clear(); _healsBySpell.Clear(); _regenTicks = 0;
        _regenEstimated = 0; _regenSpell = null; _lastRegenCast = null; _lastConsider = null;
        _lastLoc = null; _locTrail.Clear(); _trackedMemo = null;
        _snapshotMemo = null;   // also frees the ended session's lists for GC
        // The journal is about to be cleared: every place _journal empties or is
        // replaced funnels through here (reset, rollover, character/review switches
        // via LogWatcher.Select), so this is the one invalidation point the
        // incremental tracked scan needs (perf audit #10).
        _trackedAccs = null; _trackedAccFingerprint = null; _trackedScanIndex = 0;
        _runeGainCount = 0; _runeGainPoints = 0;
        _runeBlockStreak = 0; _runeBlockStreakMax = 0; _runeBlockCount = 0;
        _loot.Clear(); _lootCount = 0; _crafted.Clear(); _upgraded.Clear();
        _copper = 0; _coinDrops = 0; _biggestDrop = 0;
        _vendorCopper = 0; _salesCount = 0; _soldItems.Clear();
        _xpPercent = 0; _xpTicks = 0; _xpSinceLevel = 0; _levels.Clear();
        _aaGained = 0; _aaTotal = 0;
        _skills.Clear(); _faction.Clear(); _zones.Clear();
        _fizzles = 0; _resists = 0; _blocked = 0;
        _closedCombatSeconds = 0; _closedCombatDamage = 0;
        _combatStart = null; _combatLast = null; _combatDamage = 0;
        _lastOwnAction = null; _petName = null; _petConfirmed = false;
        _charmHold = null; _charmProvisional = null; _charmHoldByBreak.Clear();
        _pendingCast = null; _charmCandidate = null;
        _castsStarted = 0; _castsInterrupted = 0;
        _dotDamage = 0; _directSpellDamage = 0;
        _openBursts.Clear(); _castAgg.Clear();
        _journal.Clear(); _journalAppendsSincePrune = 0;
        _activeBuckets.Clear(); _markers.Clear(); _combatSpans.Clear();
        _damageTimeline.Clear();
        _activeFights.Clear(); _encounters.Clear(); _mobs.Clear(); _lastKill = null;
        _healingFight = null;
        _lastDestroyed = null; _pendingXp.Clear(); _pendingCoin.Clear();
        _currentStance = null; _stanceAgg.Clear();
        _currentInvocation = null; _invocationAgg.Clear();
        _procs.Clear(); _spellCastAt.Clear(); _lastItemProc = null;
        _spellOutcomes.Clear();
    }

    private static void Bump(Dictionary<string, int> d, string key) =>
        d[key] = d.TryGetValue(key, out var v) ? v + 1 : 1;

    /// <summary>Net items gained since <paramref name="since"/> — loot in, auto-sells /
    /// destroys / vendor sales out — the live overlay the inventory views lay over a
    /// /outputfile dump (David, 2026-08-11: the dump is a baseline, the log keeps it
    /// current). Keys are base item names. Loot events are journal-retained whole
    /// session, so a dump older than the session is adjusted by everything the
    /// session saw; what happened before the session started is the dump's to know.</summary>
    public Dictionary<string, int> ItemsGainedSince(DateTime since)
    {
        var gained = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        void Add(string item, int n)
        {
            var b = QuestCatalog.BaseItemName(item);
            gained[b] = gained.GetValueOrDefault(b) + n;
        }
        lock (_lock)
            foreach (var e in _journal)
            {
                if (e.Time <= since) continue;
                switch (e)
                {
                    case LootEvent l: Add(l.Item, Math.Max(1, l.Count)); break;
                    case AutoSellEvent a: Add(a.Item, -Math.Max(1, a.Count)); break;
                    case ItemDestroyedEvent x: Add(x.Item, -Math.Max(1, x.Count)); break;
                    // Vendor sales name one item per line; counts aren't logged, so
                    // one per line is the honest floor.
                    case MoneyEvent { Vendor: true, Item: { Length: > 0 } sold }: Add(sold, -1); break;
                }
            }
        return gained;
    }

    public StatsSnapshot Snapshot() => Snapshot(recentWindow: null, rules: null);

    /// <summary>
    /// Snapshot with optional journal-derived extras: recent-window rates (RATE-006:
    /// computed from timestamped events, never proportional estimates) and tracked-rule
    /// results (recomputed from the journal, so rule edits apply mid-session).
    /// </summary>
    public StatsSnapshot Snapshot(TimeSpan? recentWindow, IReadOnlyList<TrackedRule>? rules)
    {
        lock (_lock)
        {
            return BuildSnapshotLocked(recentWindow, rules);
        }
    }

    private StatsSnapshot BuildSnapshotLocked(TimeSpan? recentWindow, IReadOnlyList<TrackedRule>? rules)
    {
        {
            // Perf audit #12: same version + window + rules = the same snapshot —
            // serve the cached instance instead of rebuilding every list. The one
            // field that reads the wall clock is CurrentDps ("only advertise a
            // current DPS while the fight is actually live", below): a cached 0
            // stays 0 without new events (the combat damage can't move), and a
            // cached >0 is only served while the live-fight window that produced
            // it still holds — once it lapses, the rebuild honestly reports 0.
            var rulesFp = rules is null ? "" : string.Join("", rules.Select(r =>
                $"{r.Id}|{r.Enabled}|{(int)r.Kind}|{(int)r.SpellFilter}|{r.EffectivePattern}|{r.UseRegex}"));
            if (_snapshotMemo is { } sm && sm.Version == _version
                && sm.Window == recentWindow && sm.RulesFp == rulesFp
                && (sm.Snap.CurrentDps == 0
                    || (_combatLast is { } liveCl
                        && DateTime.Now - liveCl <= CombatGap + TimeSpan.FromSeconds(2))))
                return sm.Snap;

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

            var activeSeconds = Math.Min(_activeBuckets.Count * ActiveBucket.TotalSeconds,
                Math.Max(elapsed.TotalSeconds, ActiveBucket.TotalSeconds));
            var activeHours = Math.Max(activeSeconds / 3600.0, 1.0 / 60);

            RecentRates? recent = null;
            if (recentWindow is { } w && _lastEventTime is { } winEnd)
            {
                var winStart = winEnd - w;
                double xp = 0, dmg = 0, healed = 0;
                int kills = 0;
                long coin = 0;
                // Perf audit #11: the journal is appended in log order (timestamps
                // are non-decreasing within a session — a 60-min regression rolls
                // the session and clears it), so walk BACKWARD from the end and stop
                // at the first entry older than the window instead of touching every
                // entry per snapshot. An entry exactly AT winStart still counts,
                // same as the old forward filter (`< winStart` skipped).
                for (var i = _journal.Count - 1; i >= 0; i--)
                {
                    var evt = _journal[i];
                    if (evt.Time < winStart) break;
                    switch (evt)
                    {
                        case XpEvent x: xp += x.Percent; break;
                        case KillEvent k when k.Killer == "You" || IsPet(k.Killer): kills++; break;
                        case DamageDealtEvent dd: dmg += dd.Amount; break;
                        case HealEvent { Outgoing: true } h: healed += h.Amount; break;
                        case MoneyEvent m: coin += m.Copper; break;
                        case AutoSellEvent a: coin += a.Copper; break;
                    }
                }
                double combatInWindow = 0;
                // Same bound for the spans: they close in chronological order, so
                // ends are non-decreasing and everything before the first span
                // ending short of winStart overlaps zero seconds (OverlapSeconds
                // already returns 0 for a span ending exactly AT winStart, so
                // stopping there changes no total).
                for (var i = _combatSpans.Count - 1; i >= 0; i--)
                {
                    var (s2, e2) = _combatSpans[i];
                    if (e2 < winStart) break;
                    combatInWindow += OverlapSeconds(s2, e2, winStart, winEnd);
                }
                if (_combatStart is { } ocs && _combatLast is { } ocl)
                    combatInWindow += OverlapSeconds(ocs, ocl, winStart, winEnd);
                if (combatInWindow < 1 && dmg > 0) combatInWindow = 1;
                recent = new RecentRates(
                    Window: w,
                    HasFullWindow: elapsed >= w,
                    XpPercent: xp,
                    XpPerHour: xp / w.TotalHours,
                    Kills: kills,
                    Copper: coin,
                    Dps: combatInWindow > 0 ? dmg / combatInWindow : 0,
                    Hps: combatInWindow > 0 ? healed / combatInWindow : 0);
            }

            List<TrackedRuleResult> tracked = [];
            if (rules is not null)
            {
                // Keep the ingest-side prefilter current with the rules we were just handed:
                // ObserveRawLine only keeps lines one of these matches. Already holding
                // _lock here, so this assigns directly rather than calling
                // RefreshTextPatterns (which takes it).
                _textPatterns = rules
                    .Where(r => r.Enabled && r.Kind == WatchKind.Text && r.EffectivePattern.Length > 0)
                    .ToArray();

                // Perf audit #4: this replay is O(rules × journal) and ran EVERY
                // second — the one per-tick cost that scales with session length and
                // rule count. The scan result can only change when an event lands or
                // the rules themselves change, so it's memoized on exactly that pair
                // (the fingerprint is computed once at the top, shared with the
                // snapshot memo of perf audit #12);
                // only the time-derived rates below are recomputed per snapshot.
                if (_trackedMemo is { } memo && memo.Version == _version && memo.Fingerprint == rulesFp)
                {
                    foreach (var sc in memo.Scans)
                        tracked.Add(new TrackedRuleResult(sc.Name, sc.Total, sc.Items,
                            sc.Total / hours, sc.Total / activeHours, sc.First, sc.Last, sc.LastItem, sc.Id));
                    goto trackedDone;
                }
                // Perf audit #10: the version moved (a raid keeps it moving every
                // tick), so fold ONLY the entries appended since the last scan into
                // per-rule accumulators, instead of rescanning O(rules × journal).
                var scans = ScanTrackedLocked(rules, rulesFp);
                _trackedMemo = (_version, rulesFp, scans);
                foreach (var sc in scans)
                    tracked.Add(new TrackedRuleResult(sc.Name, sc.Total, sc.Items,
                        sc.Total / hours, sc.Total / activeHours, sc.First, sc.Last, sc.LastItem, sc.Id));
                trackedDone: ;
            }

            var snap = new StatsSnapshot
            {
                Version = _version,
                LastLocation = _lastLoc,
                LocationTrail = _locTrail.ToList(),
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
                DamageTimeline = _damageTimeline.OrderBy(kv => kv.Key)
                    .Select(kv => new TimelinePoint(new DateTime(kv.Key * TimeSpan.TicksPerMinute), kv.Value))
                    .ToList(),
                MeleeDamage = _meleeDamage,
                SpellDamage = _spellDamage,
                HitCount = _hitCount,
                CritCount = _critCount,
                MissCount = _missCount,
                MaxHit = _maxHit,
                MaxHitDesc = _maxHitDesc,
                DamageBySource = Breakdown(_damageBySource),
                PetAbilities = Breakdown(_petAbilities),
                PetName = _petName ?? "",
                CharmedSince = _charmHold?.LandedAt ?? _charmProvisional?.LandedAt,
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
                    .Select(kv => new SourceDamage(kv.Key, kv.Value.Count, kv.Value.Total,
                        0, kv.Value.ActiveSeconds)).ToList(),
                Hps = combatSeconds > 0 ? _healingDone / combatSeconds : 0,
                RegenTicks = _regenTicks,
                RegenEstimatedHealed = _regenEstimated,
                RegenSpell = _regenSpell ?? "",
                RuneGainCount = _runeGainCount,
                RuneGainPoints = _runeGainPoints,
                RuneBlockCount = _runeBlockCount,
                RuneBlockStreak = _runeBlockStreak,
                RuneBlockStreakMax = _runeBlockStreakMax,
                LootTotal = _lootCount,
                // Every drop in the order it happened, newest first. The aggregate above
                // answers "what did this session give me"; a farm answers a different
                // question — "did anything unusual land while I was grinding the same
                // thing 200 times" — and a count that ticked from 41 to 42 cannot show
                // that (#160, wizen). Capped because it feeds a scrolling card, not a log.
                RecentLoot = _journal.OfType<LootEvent>()
                    .Reverse().Take(MaxRecentLoot)
                    .Select(l => new LootPickup(l.Time, l.Item, Math.Max(1, l.Count), l.Source))
                    .ToList(),
                Loot = _loot.OrderByDescending(kv => kv.Value.Count)
                    .Select(kv => new LootDetail(kv.Key, kv.Value.Count, kv.Value.LastSource)).ToList(),
                Crafted = _crafted.OrderByDescending(kv => kv.Value)
                    .Select(kv => new NameCount(kv.Key, kv.Value)).ToList(),
                CraftedTotal = _crafted.Values.Sum(),
                Upgraded = _upgraded.OrderByDescending(kv => kv.Value)
                    .Select(kv => new NameCount(kv.Key, kv.Value)).ToList(),
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
                AaAbilities = BuildAaLedgerLocked(),
                AaTotal = _aaTotal,
                AaPerHour = _aaGained / hours,
                Levels = _levels.Select(l => new TimedDetail(l.Time, $"Level {l.Level}")).ToList(),
                LastLevel = _levels.Count > 0 ? _levels[^1].Level : null,
                SkillUps = _skills.OrderByDescending(kv => kv.Value.Ups)
                    .Select(kv => new SkillDetail(kv.Key, kv.Value.Ups, kv.Value.Value)).ToList(),
                SkillUpTotal = _skills.Values.Sum(v => v.Ups),
                Faction = _faction.OrderByDescending(kv => Math.Abs(kv.Value.Net))
                    .Select(kv => new FactionDetail(kv.Key, kv.Value.Hits, kv.Value.Net,
                        kv.Value.Capped, kv.Value.CappedDown)).ToList(),
                Zones = _zones.Select(z => new TimedDetail(z.Time, z.Zone)).ToList(),
                CurrentZone = _zones.Count > 0 ? _zones[^1].Zone : "",
                Fizzles = _fizzles,
                Resists = _resists,
                Blocked = _blocked,
                CastsStarted = _castsStarted,
                CastsInterrupted = _castsInterrupted,
                DotDamage = _dotDamage,
                DirectSpellDamage = _directSpellDamage,
                ActiveSeconds = activeSeconds,
                XpPerActiveHour = _xpPercent / activeHours,
                CopperPerActiveHour = (long)((_copper + _vendorCopper) / activeHours),
                KillsPerActiveHour = _yourKills.Values.Sum() / activeHours,
                Recent = recent,
                Tracked = tracked,
                Markers = _markers.Select(m => new TimedDetail(m.Time, m.Label)).ToList(),
                LastFight = BuildLastFight(),
                CurrentTargets = BuildCurrentTargetsLocked(),
                RecentEncounters = _encounters.TakeLast(8).Reverse().ToList(),
                Encounters = _encounters.ToList(),
                EncounterCount = _encounters.Count,
                Mobs = _mobs.OrderByDescending(kv => kv.Value.Kills)
                    .Select(kv => new MobSummary(
                        kv.Key, kv.Value.Kills, kv.Value.Encounters,
                        kv.Value.Encounters > 0 ? kv.Value.FightSeconds / kv.Value.Encounters : 0,
                        kv.Value.Xp, kv.Value.Copper,
                        kv.Value.Loot.OrderByDescending(l => l.Value)
                            .Select(l => new MobLoot(l.Key, l.Value,
                                kv.Value.Kills > 0 ? 100.0 * l.Value / kv.Value.Kills : null)
                            {
                                LastAt = kv.Value.LootLast.TryGetValue(l.Key, out var at) ? at : null,
                            })
                            .ToList())
                    {
                        Zone = kv.Value.Zone,
                        CoinMin = kv.Value.CoinMin,
                        CoinMax = kv.Value.CoinMax,
                        Factions = kv.Value.Factions
                            .Select(f => new MobFactionHit(f.Key, f.Value.Delta, f.Value.Hits))
                            .OrderBy(f => f.Faction)
                            .ToList(),
                        LevelMin = kv.Value.LevelMin,
                        LevelMax = kv.Value.LevelMax,
                    })
                    .ToList(),
                AreaSpells = BuildAreaSpells(),
                Procs = _procs
                    .Select(kv => (kv.Key, kv.Value.Count, kv.Value.Damage))
                    .OrderByDescending(x => x.Damage).ToList(),
                SpellResists = _spellOutcomes
                    .Where(kv => kv.Value.Resists > 0 || kv.Value.Blocked > 0)
                    .Select(kv => (kv.Key, kv.Value.Casts, kv.Value.Resists, kv.Value.Blocked))
                    .OrderByDescending(x => x.Resists + x.Blocked).ToList(),
                InferredClass = _classInference.Current(),
                CurrentStance = _currentStance ?? "",
                Stances = _stanceAgg
                    .Select(kv => new StanceInfo(kv.Key, kv.Value.Seconds, kv.Value.Damage,
                        kv.Value.Seconds > 0 ? kv.Value.Damage / kv.Value.Seconds : 0))
                    .OrderByDescending(x => x.CombatSeconds).ToList(),
                CurrentInvocation = _currentInvocation ?? "",
                Invocations = _invocationAgg
                    .Select(kv => new StanceInfo(kv.Key, kv.Value.Seconds, kv.Value.Damage,
                        kv.Value.Seconds > 0 ? kv.Value.Damage / kv.Value.Seconds : 0))
                    .OrderByDescending(x => x.CombatSeconds).ToList(),
            };
            _snapshotMemo = (_version, recentWindow, rulesFp, snap);
            return snap;
        }
    }

    private static double OverlapSeconds(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
    {
        var s = aStart > bStart ? aStart : bStart;
        var e = aEnd < bEnd ? aEnd : bEnd;
        return e > s ? (e - s).TotalSeconds : 0;
    }
}

public record NameCount(string Name, int Count);
/// <summary>Rolling-window rates computed from journal events (never proportional estimates).</summary>
public record RecentRates(TimeSpan Window, bool HasFullWindow, double XpPercent, double XpPerHour,
    int Kills, long Copper, double Dps, double Hps);
public record TimedDetail(DateTime Time, string Text);

/// <summary>One minute of the session's damage timeline (see
/// <see cref="StatsSnapshot.DamageTimeline"/>).</summary>
public record TimelinePoint(DateTime Time, long Damage);
/// <summary>ActiveSeconds &gt; 0 enables per-ability rate display (Total ÷ ActiveSeconds);
/// it is 0 for lists that don't track it (damage taken, healers) and for
/// sessions stored before it existed.</summary>
public record SourceDamage(string Name, int Hits, long Total, int Crits = 0, double ActiveSeconds = 0)
{
    /// <summary>Smallest/largest single hit (0 when unknown — sessions archived before
    /// these existed deserialize as unknown and the range simply doesn't render).</summary>
    public long MinHit { get; init; }
    public long MaxHit { get; init; }
    /// <summary>Failed swings of this skill (melee only — spells fail as resists).</summary>
    public int Misses { get; init; }
}
public record LootDetail(string Item, int Count, string LastSource);

/// <summary>One drop as it happened — the raw loot view's row (#160).</summary>
public record LootPickup(DateTime Time, string Item, int Count, string Source);
public record SkillDetail(string Skill, int Ups, int Value);
public record SoldDetail(string Item, int Count, long Copper);
/// <param name="Capped">Standing hit the cap this session ("could not possibly get any
/// better/worse"). Default false so history snapshots from before this existed deserialize
/// unchanged.</param>
/// <param name="CappedDown">The cap was the FLOOR ("any worse") — shown as "bottomed"
/// rather than "maxed" (#86). Defaults false, so old snapshots keep reading "maxed".</param>
public record FactionDetail(string Faction, int Hits, int Net, bool Capped = false,
    bool CappedDown = false);

public sealed class StatsSnapshot
{
    /// <summary>Event counter at snapshot time — equal versions mean equal content
    /// (only time-derived rates move), so renderers can skip rebuilding. Sessions
    /// archived before this existed deserialize as 0, which only ever re-renders.</summary>
    public long Version { get; init; }
    /// <summary>The last /loc seen in THIS zone, or null (zoning clears it — a
    /// position from the previous zone would lie on the map).</summary>
    public LocationEvent? LastLocation { get; init; }
    /// <summary>Every /loc in this zone, oldest first, bounded — the map's
    /// breadcrumb trail. Empty for sessions archived before it existed.</summary>
    public List<LocationEvent> LocationTrail { get; init; } = [];
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
    /// <summary>Damage per minute of the session, for the History DPS-over-time graph.
    /// Minutes with no damage are absent. Empty for sessions archived before the graph
    /// existed — the History window shows no graph rather than a flat line.</summary>
    public List<TimelinePoint> DamageTimeline { get; init; } = [];
    public long MeleeDamage { get; init; }
    public long SpellDamage { get; init; }
    public int HitCount { get; init; }
    public int CritCount { get; init; }
    public int MissCount { get; init; }
    public int MaxHit { get; init; }
    public string MaxHitDesc { get; init; } = "";
    public List<SourceDamage> DamageBySource { get; init; } = [];
    /// <summary>Your pet's damage split by what it used (melee skill or spell name), summing
    /// to the pet rows in <see cref="DamageBySource"/>. Empty when no pet damage was seen.</summary>
    public List<SourceDamage> PetAbilities { get; init; } = [];
    /// <summary>The current pet's name, or "" when none is claimed — window titles want the
    /// name without fishing it back out of a "Pet (Name)" row label.</summary>
    public string PetName { get; init; } = "";
    /// <summary>When the current CHARM landed (#130) — null for summoned pets and
    /// when nothing is charmed. The pet breakout shows the running hold from it.</summary>
    public DateTime? CharmedSince { get; init; }
    /// <summary>The creatures being fought right now (every open fight — the log can't
    /// say which is targeted), or the one just killed / last considered, briefly. Feeds
    /// the target-drops surfaces. Empty between pulls.</summary>
    public List<string> CurrentTargets { get; init; } = [];
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
    /// <summary>Estimated regen healing: ticks × (player override, else wiki base) for
    /// the attributed spell. A floor, labeled est., never part of <see cref="Hps"/>.</summary>
    public long RegenEstimatedHealed { get; init; }
    /// <summary>The regen spell the ticks were attributed to ("" when no own cast seen).</summary>
    public string RegenSpell { get; init; } = "";
    /// <summary>How many times the rune buff built its absorption pool ("You gain a rune
    /// for N points of absorption."), and the total points gained — already folded into
    /// HealingReceived/HealsByHealer["Rune"], broken out here for a dedicated readout.</summary>
    public int RuneGainCount { get; init; }
    public long RuneGainPoints { get; init; }
    /// <summary>Incoming melee attacks the rune fully absorbed. Streak is the current run
    /// since the last hit that actually landed; StreakMax is the longest run this session.</summary>
    public int RuneBlockCount { get; init; }
    public int RuneBlockStreak { get; init; }
    public int RuneBlockStreakMax { get; init; }
    public int LootTotal { get; init; }
    public List<LootDetail> Loot { get; init; } = [];
    /// <summary>Every drop, newest first, capped — see SessionStats.MaxRecentLoot.</summary>
    public List<LootPickup> RecentLoot { get; init; } = [];
    public List<NameCount> Crafted { get; init; } = [];
    public int CraftedTotal { get; init; }
    /// <summary>Loot-merge results by created name ("... to create a Belt +5" → Belt +5).
    /// Not part of CraftedTotal — the merge consumed a held item, nothing new was "made";
    /// the Gear card's auto-done is the consumer (reaching a wished "+N" tier).</summary>
    public List<NameCount> Upgraded { get; init; } = [];
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
    /// <summary>AA abilities owned (name, highest rank seen, last purchase time) —
    /// character-scoped, rebuilt from the whole log at ingest, alphabetical.</summary>
    public List<AaAbilityInfo> AaAbilities { get; init; } = [];
    public int AaTotal { get; init; }
    public double AaPerHour { get; init; }
    public List<TimedDetail> Levels { get; init; } = [];
    /// <summary>The latest level-up the ingest saw ("Welcome to level N!"), null when
    /// none — the level-unlock views key off the number, not the display text.</summary>
    public int? LastLevel { get; init; }
    public List<SkillDetail> SkillUps { get; init; } = [];
    public int SkillUpTotal { get; init; }
    public List<FactionDetail> Faction { get; init; } = [];
    public List<TimedDetail> Zones { get; init; } = [];
    public string CurrentZone { get; init; } = "";
    public int Fizzles { get; init; }
    public int Resists { get; init; }
    /// <summary>Buff casts that did not take hold — another buff held the stacking slot.
    /// Excluded from <see cref="CastCompletion"/> for the same reason resists are: the
    /// cast itself completed. Defaults 0, so old archives deserialize unchanged.</summary>
    public int Blocked { get; init; }
    /// <summary>Casts begun ("You begin casting X."). The denominator for cast completion.</summary>
    public int CastsStarted { get; init; }
    public int CastsInterrupted { get; init; }
    /// <summary>Share of started casts that were neither interrupted nor fizzled. Null
    /// until at least one cast is seen. Resists are excluded — a resisted spell was cast
    /// successfully, it just did nothing.</summary>
    public double? CastCompletion => CastsStarted > 0
        ? Math.Max(0, CastsStarted - CastsInterrupted - Fizzles) / (double)CastsStarted
        : null;
    /// <summary>Your own damage-over-time damage, split out from direct spell damage.
    /// Classified by log-line shape rather than by spell name. Pet damage is excluded —
    /// third-party lines carry no shape we can split on — so these two need not sum to
    /// the spell total.</summary>
    public long DotDamage { get; init; }
    public long DirectSpellDamage { get; init; }
    /// <summary>Active-play seconds (2-minute buckets containing any meaningful event).</summary>
    public double ActiveSeconds { get; init; }
    public double XpPerActiveHour { get; init; }
    public long CopperPerActiveHour { get; init; }
    public double KillsPerActiveHour { get; init; }
    public RecentRates? Recent { get; init; }
    public List<TrackedRuleResult> Tracked { get; init; } = [];
    public List<TimedDetail> Markers { get; init; } = [];
    /// <summary>The fight in progress, or the last one that finished; null before the first
    /// fight of the session. Shown above the session totals on Combat and Healing.</summary>
    public LastFightInfo? LastFight { get; init; }
    public List<EncounterInfo> RecentEncounters { get; init; } = [];
    /// <summary>Every retained fight of the session, oldest first (capped at 300 by the
    /// in-session prune), each carrying its full breakdown for the History fight review.
    /// Empty on sessions archived before 2026-08-04.</summary>
    public List<EncounterInfo> Encounters { get; init; } = [];
    public int EncounterCount { get; init; }
    public List<MobSummary> Mobs { get; init; } = [];
    public string CurrentStance { get; init; } = "";
    public List<StanceInfo> Stances { get; init; } = [];
    /// <summary>Invocation brackets, same model (and record shape) as stances.</summary>
    public string CurrentInvocation { get; init; } = "";
    public List<StanceInfo> Invocations { get; init; } = [];
    /// <summary>Spells observed hitting more than one creature at once, reported per
    /// cast rather than per target — the figures that decide whether pulling a group and
    /// AoEing it beats killing them one at a time.</summary>
    public List<AreaSpellInfo> AreaSpells { get; init; } = [];
    /// <summary>Spell damage whose spell was never cast (#85): weapon/poison/item procs,
    /// each with hit count and total damage. Rate display divides by combat minutes.</summary>
    public List<(string Name, int Count, long Damage)> Procs { get; init; } = [];
    /// <summary>Per-spell resist/block tallies for spells that failed at least once
    /// (#102, jeremycranfill): base spell name, casts started, resists seen, stacking
    /// blocks seen — the "switch to overchannel?" numbers plus the "what's eating my
    /// buff slot?" ones. Session-scoped; your own casts only.</summary>
    public List<(string Spell, int Casts, int Resists, int Blocked)> SpellResists { get; init; } = [];
    /// <summary>Most-evidenced class from class-unique signals — "" until enough
    /// sightings. ALWAYS present as "(inferred)": players swap classes.</summary>
    public string InferredClass { get; init; } = "";

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
