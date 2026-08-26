namespace EQBuddy.Core;

/// <summary>What one pooled pack covers, for the scope line that keeps the pooling honest
/// — decision 1 and 2 of the plan (pool across characters AND servers, no "since" filter)
/// are only sayable out loud because this record carries what was actually pooled.</summary>
public sealed record PoolScope(
    IReadOnlyList<string> Characters,
    IReadOnlyList<string> Servers,
    int SessionCount,
    DateTime? Earliest,
    DateTime? Latest);

/// <summary>
/// Pools per-creature observations across the sessions already stored in
/// <c>history.db</c>, plus the live one (#217 ask 2, Frankthetankk; Fable 5's plan,
/// 2026-08-22). The concrete miss it exists for: three 4-kill sessions never cross the
/// pack's 10-kill rarity bar despite twelve real kills — the honesty rules are right and
/// must not be relaxed, so the fix is to stop throwing away the evidence that satisfies
/// them. The same thinning hit money ranges, faction hits and con-derived level bounds.
///
/// **This is the all-time stats direction's first real query** (#168 / #159): a pure fold
/// over archives already on disk, no new collection. The all-time VIEW, when it is built,
/// should consume this rather than writing a second pooler.
///
/// Decisions, from the plan (logged in DECISIONS.md when the item was taken):
/// <list type="bullet">
/// <item><b>Across characters and servers, no toggle</b> — drop tables, level ranges and
/// faction hits are facts about the MOB in the game, not about who observed them. The
/// scope line names everyone pooled, so nothing is silent.</item>
/// <item><b>Keyed on (name, zone)</b> — a session's <see cref="MobSummary.Zone"/> is the
/// kill zone (the #65 fix), and "an ice giant" in two zones is two mobs.</item>
/// <item><b>No "since" filter</b> — a retune is not a date we hold. What protects against
/// one is what already protects the pack: every number is presented for reconciliation,
/// never as a correction.</item>
/// <item><b>Unknown stays unknown</b> — a pre-field snapshot deserialises CoinMin as -1
/// and LevelMin as 0, and the pool treats those as absent, never as zero observations
/// of zero.</item>
/// </list>
/// </summary>
public static class MobHistory
{
    /// <summary>One stored session's contribution: the row id (so the LIVE session's
    /// checkpointed row can be excluded rather than counted twice), who it was, when it
    /// started and ended, and its per-creature aggregates.</summary>
    public sealed record SessionMobs(
        long Id, string Server, string Character, DateTime StartLocal, DateTime EndLocal,
        IReadOnlyList<MobSummary> Mobs);

    /// <summary>
    /// Fold stored sessions and the live snapshot into one per-creature list.
    /// </summary>
    /// <param name="rows">Stored sessions (see <c>SessionRepository.MobRows</c>).</param>
    /// <param name="live">The live session's snapshot, or null. Its kills are taken from
    /// HERE and its checkpointed row — <paramref name="liveRowId"/> — is skipped, or a
    /// checkpoint that has already landed would pool the same kills twice.</param>
    public static (IReadOnlyList<MobSummary> Mobs, PoolScope Scope) Pool(
        IReadOnlyList<SessionMobs> rows, StatsSnapshot? live,
        string liveCharacter, string liveServer, long liveRowId)
    {
        var pooled = new Dictionary<(string Name, string Zone), Acc>();
        var characters = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var servers = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        DateTime? earliest = null, latest = null;
        var sessions = 0;

        foreach (var row in rows)
        {
            if (row.Id == liveRowId) continue;
            // The live session's row by IDENTITY as well as by id. The id alone has a
            // timing hole the staged shot found on its first run: ActiveRowId is set by
            // the first checkpoint, and a pool computed BEFORE that (a re-ingested log
            // whose sessions are already archived — the adoption case) counted the
            // archived twin AND the live snapshot, so every number doubled. The same
            // (server, character, session start) is the same session — the adoption
            // rule Checkpoint itself uses.
            if (live?.SessionStart is { } liveStart
                && row.StartLocal == liveStart
                && row.Server.Equals(liveServer, StringComparison.OrdinalIgnoreCase)
                && row.Character.Equals(liveCharacter, StringComparison.OrdinalIgnoreCase))
                continue;
            sessions++;
            if (row.Character.Length > 0) characters.Add(row.Character);
            if (row.Server.Length > 0) servers.Add(row.Server);
            if (earliest is null || row.EndLocal < earliest) earliest = row.EndLocal;
            if (latest is null || row.EndLocal > latest) latest = row.EndLocal;
            foreach (var mob in row.Mobs) Fold(pooled, mob);
        }

        if (live is not null && live.Mobs.Count > 0)
        {
            sessions++;
            if (liveCharacter.Length > 0) characters.Add(liveCharacter);
            if (liveServer.Length > 0) servers.Add(liveServer);
            latest = DateTime.Now;
            earliest ??= live.SessionStart ?? DateTime.Now;
            foreach (var mob in live.Mobs) Fold(pooled, mob);
        }

        var mobs = pooled.Values
            .Select(a => a.Build())
            .OrderByDescending(m => m.Kills)
            .ToList();
        return (mobs, new PoolScope([.. characters], [.. servers], sessions, earliest, latest));
    }

    private static void Fold(Dictionary<(string, string), Acc> pooled, MobSummary mob)
    {
        var key = (mob.Name, mob.Zone);
        if (!pooled.TryGetValue(key, out var acc))
            pooled[key] = acc = new Acc(mob.Name, mob.Zone);
        acc.Add(mob);
    }

    /// <summary>The running fold for one (name, zone). Loot counts sum per BASE item
    /// (<see cref="QuestCatalog.BaseItemName"/>, the existing tier fold) with the rate
    /// recomputed from the pooled numbers — same formula the live snapshot uses
    /// (100 × count / kills) — and <see cref="MobLoot.LastAt"/> the latest.</summary>
    private sealed class Acc(string name, string zone)
    {
        private int _kills, _encounters;
        private double _fightSeconds, _xp;
        private long _copper;
        private long _coinMin = -1, _coinMax;
        private int _levelMin, _levelMax;
        private int _considers, _rareConsiders;
        private readonly Dictionary<string, (string Display, int Count, DateTime? LastAt)> _loot =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (int Delta, int Hits)> _factions =
            new(StringComparer.OrdinalIgnoreCase);

        public void Add(MobSummary m)
        {
            _kills += m.Kills;
            _encounters += m.Encounters;
            _fightSeconds += m.AvgFightSeconds * m.Encounters;
            _xp += m.XpPercent;
            _copper += m.Copper;
            // -1 = no coin seen that session; only real observations move the extremes.
            if (m.CoinMin >= 0)
            {
                _coinMin = _coinMin < 0 ? m.CoinMin : Math.Min(_coinMin, m.CoinMin);
                _coinMax = Math.Max(_coinMax, m.CoinMax);
            }
            // 0 = never conned; unknown never narrows a real range.
            if (m.LevelMin > 0)
            {
                _levelMin = _levelMin == 0 ? m.LevelMin : Math.Min(_levelMin, m.LevelMin);
                _levelMax = Math.Max(_levelMax, m.LevelMax);
            }
            _considers += m.Considers;
            _rareConsiders += m.RareConsiders;

            foreach (var l in m.Loot)
            {
                var baseName = QuestCatalog.BaseItemName(l.Item);
                var prev = _loot.TryGetValue(baseName, out var p)
                    ? p : (Display: l.Item, Count: 0, LastAt: (DateTime?)null);
                var lastAt = l.LastAt is { } at && (prev.LastAt is not { } was || at > was)
                    ? at : prev.LastAt;
                _loot[baseName] = (prev.Display, prev.Count + l.Count, lastAt);
            }
            foreach (var f in m.Factions)
            {
                var prev = _factions.TryGetValue(f.Faction, out var p) ? p : (f.Delta, 0);
                _factions[f.Faction] = (prev.Item1, prev.Item2 + f.Hits);
            }
        }

        public MobSummary Build() => new(
            name, _kills, _encounters,
            _encounters > 0 ? _fightSeconds / _encounters : 0,
            _xp, _copper,
            _loot.Values
                .OrderByDescending(l => l.Count)
                .Select(l => new MobLoot(l.Display, l.Count,
                    _kills > 0 ? 100.0 * l.Count / _kills : null)
                { LastAt = l.LastAt })
                .ToList())
        {
            Zone = zone,
            CoinMin = _coinMin,
            CoinMax = _coinMax,
            Factions = _factions
                .Select(f => new MobFactionHit(f.Key, f.Value.Delta, f.Value.Hits))
                .OrderBy(f => f.Faction)
                .ToList(),
            LevelMin = _levelMin,
            LevelMax = _levelMax,
            Considers = _considers,
            RareConsiders = _rareConsiders,
        };
    }
}
