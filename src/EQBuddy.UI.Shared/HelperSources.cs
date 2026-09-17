using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// **The Helper's INPUT bundle, assembled in one place** — the stores
/// <see cref="Recommendations.Rank"/> ranks over, read behind one throttle and one clock so
/// a caller cannot take one and skip the other.
///
/// <para><b>It exists because DRA-71 D9 put a SECOND host on the same engine.</b> The desktop
/// room has read these seven things since D1; the phone's projection now ranks with the same
/// <see cref="HelperInputs"/>, and the plan's own words for that slice are *"parity stays by
/// shared module"*. The alternative was a second copy of the assembly in
/// <c>MainWindow</c> — seven reads, three folds and an inventory stamp, written twice — which
/// is the #210 shape exactly: EQBuddy Mobile and the desktop answering one question from two
/// pieces of code, drifting the first time one of them learns a store. So the reads move here
/// and the room calls it too.</para>
///
/// <para><b>Each host holds its OWN instance, and that is deliberate</b> (trap 45): this is a
/// memo — a cache with a clock in it — and a memo handed between hosts is state two owners
/// invalidate. The same split <c>LevelHistoryMemo</c> already keeps between the Experience
/// card and the phone. What is shared is the PRODUCER; what is per-host is the cache.</para>
///
/// <para>Framework-free, like everything in this project: the reads arrive as delegates from
/// whoever owns the archive, the dump and the wiki cache.</para>
/// </summary>
public sealed class HelperSources
{
    /// <summary>
    /// How long the disk-and-database reads are trusted for.
    ///
    /// <para>Five seconds, and the argument is <c>HomeRoom</c>'s: these inputs are a SQLite
    /// query over every stored session plus two snapshot probes plus a dump file, and none of
    /// them can change faster than a player can finish a sitting or type a command. Anything
    /// the player just DID clears it immediately (<see cref="Invalidate"/>), so the cache
    /// never stands between them and their own action — which is the whole promise the
    /// Helper's "run this command" empty states make.</para>
    /// </summary>
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(5);

    /// <summary>Where the seven inputs come from. Delegates rather than a reference to the
    /// widget, because UI.Shared may not know what a widget is — and because the test that
    /// proves a fold is what fakes them.</summary>
    /// <param name="StoredMobRows">The archived creature rows the pool folds. Handed to
    /// <see cref="WikiPackPool"/>, which stays the one cache in front of
    /// <see cref="MobHistory.Pool"/>.</param>
    /// <param name="StoredSessions">Every stored session for this character.</param>
    /// <param name="StoredThroughput">DRA-71 D4's snapshot probe: per-session dps/hps and
    /// their combat seconds.</param>
    /// <param name="StoredSales">DRA-71 D7's snapshot probe: what a vendor has paid.</param>
    /// <param name="LatestInventory">The newest inventory dump, or null when there is
    /// none.</param>
    /// <param name="StatsFor">Base name → stats, the catalog-first resolver
    /// <see cref="GearUpgrades.WornFrom"/> and <c>GearLocker.Build</c> both take.</param>
    /// <param name="ActiveSessionRowId">The row the live session is being archived under, so
    /// the live half is never double-counted against its own stored half.</param>
    public sealed record Reads(
        Func<IReadOnlyList<MobHistory.SessionMobs>> StoredMobRows,
        Func<IReadOnlyList<SessionRow>> StoredSessions,
        Func<IReadOnlyList<SessionThroughput>> StoredThroughput,
        Func<IReadOnlyList<SessionSales>> StoredSales,
        Func<InventoryFile.Snapshot?> LatestInventory,
        Func<string, ItemStatsBlock?> StatsFor,
        Func<long> ActiveSessionRowId);

    private readonly Reads _reads;
    private readonly WikiPackPool _pool;
    private DateTime _readAt = DateTime.MinValue;

    public HelperSources(Reads reads)
    {
        _reads = reads;
        _pool = new WikiPackPool(reads.StoredMobRows);
    }

    /// <summary>Per-zone all-time evidence — <see cref="ZoneHistory.Fold"/>'s own
    /// answer.</summary>
    public IReadOnlyList<ZoneRoll> Zones { get; private set; } = [];

    /// <summary>The pooled creatures. <see cref="MobHistory.Pool"/> stays the one pooler;
    /// <see cref="WikiPackPool"/> is a cache in front of it and not a second fold.</summary>
    public IReadOnlyList<MobSummary> Pool => _pool.Mobs;

    /// <summary>Where motes have actually dropped — <see cref="MoteHistory.Fold"/>'s
    /// answer, over the pool and the rollup above. No third query.</summary>
    public IReadOnlyList<MoteRoll> Motes { get; private set; } = [];

    /// <summary>What a vendor has actually paid — <see cref="SaleHistory.Fold"/>'s
    /// answer, including the LIVE session's own sales so a vendor trip this evening prices
    /// tonight's drops.</summary>
    public IReadOnlyList<SaleRoll> Sales { get; private set; } = [];

    /// <summary>What the character is WEARING — the Farm Gear sweep's anchor set.</summary>
    public IReadOnlyList<WornItem> Worn => _sheet.Worn;

    /// <summary>The worn rows EQBuddy could not read about, as the dump spells them (DRA-149
    /// D2). It comes off the SAME fold as <see cref="Worn"/> — see
    /// <see cref="WornSheet"/> for why the two halves are never computed apart.</summary>
    public IReadOnlyList<string> UnreadWorn => _sheet.Unread;

    private WornSheet _sheet = WornSheet.Nothing;

    /// <summary>The dump <see cref="Worn"/> was folded from, for a caller's repaint
    /// fingerprint. A new dump changes what is worn, which changes every gear answer without
    /// moving anything else a caller watches — trap 72's exact shape, one store along.</summary>
    public string InventoryStamp { get; private set; } = "";

    /// <summary>Steps when the pool actually re-folded. A caller folds it into its own
    /// repaint key rather than stringifying several hundred creatures once a second.</summary>
    public int PoolVersion { get; private set; }

    /// <summary>
    /// Take the reads, if the throttle has expired. The pool is asked EVERY call — its own
    /// signature decides whether it re-folds, so asking is one string-join, and it is what
    /// makes a kill during a live session move the Helper's cadence line.
    /// </summary>
    public void Read(StatsSnapshot? live, string character, string server)
    {
        if (_pool.Refresh(live ?? new StatsSnapshot(), character, server,
                _reads.ActiveSessionRowId())) PoolVersion++;

        if (DateTime.Now - _readAt < CacheFor) return;
        _readAt = DateTime.Now;

        // ONE producer for the zone rollup: `ZoneHistory.Fold` over the session rows this
        // character already has, joined to the pool above. Nothing here re-pools creatures and
        // nothing here mines dings — `MobHistory.Pool` and `ProgressSeries` stay the only ones
        // that do.
        Zones = ZoneHistory.Fold(
            _reads.StoredSessions(), _pool.Mobs, _reads.StoredThroughput());

        // DRA-71 D7's two folds, built from what is already in hand. MoteHistory takes the
        // pool and the rollup above it — no third query — and SaleHistory takes one more
        // snapshot probe, the sibling of the throughput one.
        Motes = MoteHistory.Fold(_pool.Mobs, Zones);
        Sales = SaleHistory.Fold(_reads.StoredSales(), live, _reads.ActiveSessionRowId());

        // DRA-71 D6: what the character is WEARING. A file read plus a fold over ~200 rows —
        // the same cost as the session query it sits beside.
        var dump = _reads.LatestInventory();
        InventoryStamp = dump is null ? "" : $"{dump.Path}|{dump.WrittenAt:O}|{dump.Entries.Count}";
        _sheet = dump is null
            ? WornSheet.Nothing
            : GearUpgrades.WornFrom(dump.Entries, _reads.StatsFor);
    }

    /// <summary>A dump the player just produced landed: re-read on the next
    /// <see cref="Read"/> rather than up to <see cref="CacheFor"/> later.</summary>
    public void Invalidate() => _readAt = DateTime.MinValue;

    /// <summary>
    /// **Everything one Helper pass reads, in one object** — the engine's input, the goals it
    /// is ranked against, and the four selections a HOST draws pickers for.
    ///
    /// <para>The selections ride here rather than being re-read by each caller for trap 33's
    /// reason: two callers reading one store at slightly different moments produce two current
    /// answers and whichever ran last wins. The desktop room draws its pickers from these
    /// fields and ranks from <see cref="Inputs"/>, and both came out of the same pass.</para>
    /// </summary>
    /// <param name="Inputs">What <see cref="Recommendations.Rank"/> reads.</param>
    /// <param name="Goals">What the character is working toward. Empty weighs all of them.</param>
    /// <param name="Factions">The picked factions. Empty means the picker has not been used —
    /// NOT "all of them", because 200 standings is not a recommendation.</param>
    /// <param name="UnlockPicks">The picked race and class unlocks. Empty means ALL of them —
    /// filter semantics, the opposite of <paramref name="Factions"/> beside it.</param>
    /// <param name="WornPicks">The picked worn items. Empty means all of them.</param>
    /// <param name="Professions">The picked professions, in the curated enum's order.</param>
    /// <param name="Skills">Where this character's profession skills stand.</param>
    public sealed record Bundle(
        HelperInputs Inputs,
        IReadOnlyList<HelperGoal> Goals,
        IReadOnlyList<string> Factions,
        IReadOnlyList<string> UnlockPicks,
        IReadOnlyList<string> WornPicks,
        IReadOnlyList<Tradeskill> Professions,
        IReadOnlyList<(string Skill, int Value, DateTime At)> Skills);

    /// <summary>
    /// Assemble the pass. <see cref="Read"/> first — this method does no I/O of its own; it
    /// joins what the throttle holds to the per-tick stores, which are dictionary lookups and
    /// are deliberately NOT cached: a pick the player just made, or a level they just typed
    /// one room away, must change the answers now rather than in five seconds.
    /// </summary>
    /// <param name="ledgerClasses">The classes the player picked for this character.</param>
    /// <param name="inferredClass">What the log looks like, behind them. Empty means unknown,
    /// and unknown filters NOTHING — hiding a real upgrade is worse than showing one the
    /// player will recognise as not theirs.</param>
    public Bundle Gather(
        AppSettings settings, string characterKey, UnlockSource unlocks, ResolvedLevel level,
        QuestCatalog? catalog, ItemCatalog? items,
        IReadOnlyList<string> ledgerClasses, string inferredClass,
        IReadOnlyList<(string Skill, int Value, DateTime At)> skills)
    {
        var goals = HelperGoalStore.Goals(settings, characterKey);
        var factions = HelperGoalStore.Factions(settings, characterKey);
        // DRA-71 D5: the SAME store the Quests window's Unlocks tab reads. One producer of the
        // pick; no room keeps a copy of it.
        var unlockPicks = UnlockPickStore.Picked(settings, characterKey);
        var intent = GearIntentStore.Intent(settings, characterKey);
        var wornPicks = GearIntentStore.WornPicks(settings, characterKey);
        var includeQuests = GearIntentStore.IncludeQuests(settings, characterKey);
        var professions = TradeskillPickStore.Picked(settings, characterKey);

        return new Bundle(
            new HelperInputs(
                Zones, Pool, unlocks.Factions, factions,
                unlocks.Races, unlocks.Classes, unlockPicks, unlocks.HasAchievements,
                settings.SkyQuestChecklist, settings.SkyQuestCompleted, catalog, level)
            {
                Worn = Worn,
                // DRA-149 D2, and it is supplied HERE for the reason `Bands` below is: this is
                // the one assembly point both surfaces go through, so a worn row EQBuddy could
                // not read is reported on the phone and on the PC or on neither.
                UnreadWorn = UnreadWorn,
                Items = items,
                MyClasses = ClassCodes(ledgerClasses, inferredClass),
                GearIntent = intent,
                WornPicks = wornPicks,
                IncludeQuests = includeQuests,
                Motes = Motes,
                Sales = Sales,
                // **DRA-84 D2, and it is supplied HERE rather than by a host.** This is the one
                // assembly point the room and the phone both go through, so the band gate
                // cannot be live on one surface and stood down on the other — which is the
                // shape porting a feature to the phone keeps finding (trap 4). It is the lazy
                // shipped catalog, so naming it costs nothing until something reads a band.
                Bands = ZoneLevels.Default,
                // **DRA-149 D3, and it is supplied HERE for the same reason.** The pick was
                // already read a few lines up and carried only to the picker; the engine is the
                // SECOND reader of that one store rather than a second producer of the pick
                // (trap 4), and routing it through this one assembly point is what stops the
                // phone from ranking materials against a different set of professions than the
                // PC does.
                Professions = professions,
            },
            goals, factions, unlockPicks, wornPicks, professions, skills);
    }

    /// <summary>
    /// This character's classes as the item blocks spell them (PAL, RNG) — the gear sweep's
    /// class-lock filter, and the same resolution the Gear room's Inventory tab makes: the
    /// ledger's picked classes first, the log's inference behind them.
    ///
    /// <para>It is here rather than in a room because the phone needs the identical answer,
    /// and a class filter that differed between two surfaces would hide an upgrade on one of
    /// them (trap 4).</para>
    /// </summary>
    public static IReadOnlyList<string> ClassCodes(
        IReadOnlyList<string> ledgerClasses, string inferredClass)
    {
        var picked = ledgerClasses;
        if (picked.Count == 0 && inferredClass is { Length: > 0 }) picked = [inferredClass];
        return [.. picked.Select(GearLocker.Code)];
    }

    /// <summary>
    /// The part of a caller's repaint fingerprint that is ABOUT these stores — folded by
    /// CONTENT, because a swap leaves a count unmoved (trap 72).
    ///
    /// <para>It is here rather than in each host for the reason the folds are: two hosts
    /// keying on two different subsets of one bundle is how one of them ends up drawing the
    /// moment before, and the field most likely to be missed is whichever one the next slice
    /// adds.</para>
    /// </summary>
    public string Signature() => string.Join('|',
        PoolVersion,
        InventoryStamp,
        // The throughput fields ride for trap 72's reason: a re-fold that gained combat
        // seconds, damage or healing and moved a weight without moving the session count or
        // the experience total would leave a caller drawing the moment before it.
        string.Join(',', Zones.Select(z =>
            $"{z.Zone}:{z.Sessions}:{z.Kills}:{z.XpPercent:0.##}"
            + $":{z.CombatSeconds:0.##}:{z.CombatDamage:0.##}:{z.HealingDone:0.##}"
            + $":{z.ActiveHours:0.####}:{z.Deaths}")),
        // Neither of these can be inferred from anything else here: a mote looted from a
        // creature already in the pool moves `MobLoot.Count` without moving the pool's own
        // signature, and a vendor trip moves nothing else at all.
        string.Join(',', Motes.Select(m => $"{m.Zone}:{m.Motes}:{m.Potency}:{m.Kills}")),
        string.Join(',', Sales.Select(x => $"{x.Item}:{x.Count}:{x.Copper}")));
}
