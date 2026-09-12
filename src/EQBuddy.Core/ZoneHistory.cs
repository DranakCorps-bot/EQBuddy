namespace EQBuddy.Core;

/// <summary>
/// One zone's all-time rollup, folded from sessions already on disk.
/// </summary>
/// <param name="Zone">The zone as the log spells it. Never normalised here — <see cref="ZoneMap"/>
/// owns aliases, and a fold that quietly renamed a zone would be a second spelling of one
/// place (trap 33 in the data).</param>
/// <param name="Sessions">How many stored sessions named this zone as their PRIMARY one.
/// Carried so a surface can say what its number rests on: "across 14 of your sessions" is a
/// scope, and a rate with no scope is a claim nobody can argue with.</param>
/// <param name="Hours">Elapsed hours of those sessions.</param>
/// <param name="XpPercent">Experience gained in them, in percent-of-level.</param>
/// <param name="Copper">Coin gained in them.</param>
/// <param name="Deaths">Deaths recorded in them. Present for <b>HOME-006</b>: a surface may
/// show what your own history says about dying here, and may never say a camp is safe.</param>
/// <param name="Kills">Kills the POOL attributes to this zone — a different producer from
/// the four fields above, and deliberately so. See <see cref="ZoneHistory"/>.</param>
/// <param name="AvgFightSeconds">Kill-weighted mean fight length from the pool, or 0 when
/// the pool has nothing for this zone. 0 is "unknown", never "instant".</param>
/// <param name="MobsKnown">How many pooled creatures this zone contributed.</param>
/// <param name="LastPlayedLocal">When the newest of those sessions ended.</param>
public sealed record ZoneRoll(
    string Zone,
    int Sessions,
    double Hours,
    double XpPercent,
    long Copper,
    int Deaths,
    int Kills,
    double AvgFightSeconds,
    int MobsKnown,
    DateTime? LastPlayedLocal)
{
    /// <summary>
    /// Your observed experience per hour here, or null when there is not enough of your own
    /// play to divide.
    ///
    /// <para><b>Null is the important half.</b> Four minutes in a zone that happened to
    /// contain one good pull is "120%/hr" if you let it divide, and a recommender that
    /// printed that number would be ranking noise above the camp somebody actually farmed.
    /// <see cref="ZoneHistory.MinHours"/> is the floor; below it this answers nothing and
    /// the surface says nothing (trap 73 — silence beats a template with a guessed number
    /// in it).</para>
    /// </summary>
    public double? XpPerHour => Hours >= ZoneHistory.MinHours ? XpPercent / Hours : null;

    /// <summary>Your observed coin per hour here, on the same floor and for the same
    /// reason. Read by the Make Money engine, which is a later slice — the fold answers it
    /// now because the arithmetic is the same division over the same rows, and a second
    /// pooler added later is the thing this file exists to prevent.</summary>
    public double? CopperPerHour => Hours >= ZoneHistory.MinHours ? Copper / Hours : null;

    /// <summary>Is there enough of your own play here for a recommendation to lean on?
    /// Both halves are needed: hours make the division honest, and a kill makes it about
    /// fighting rather than about standing in a bank.</summary>
    public bool HasPersonalEvidence => XpPerHour is not null && Kills > 0;
}

/// <summary>
/// **Per-zone all-time evidence — the fold the Helper's "where should I level" answer reads**
/// (DRA-70 D1; the plan's D7 decision, Helm-signed 2026-09-12).
///
/// <para><b>It is a fold and not a collection.</b> Every number here is already on disk:
/// <c>history.db</c> has one row per archived session carrying <c>PrimaryZone</c>,
/// <c>ElapsedSeconds</c>, <c>XpPercent</c>, <c>Copper</c> and <c>Deaths</c>, and
/// <see cref="MobHistory.Pool"/> has already folded every creature you have killed, keyed on
/// (name, zone). Nothing new is recorded, nothing is written, and no session is re-parsed —
/// this is the all-time direction's second query over archives that were already being
/// kept (<see cref="MobHistory"/> was the first, and its own comment asks that the all-time
/// VIEW consume it rather than write a second pooler; so does this).</para>
///
/// <para><b>ONE PRODUCER PER FACT, and the split between the two sources is the whole
/// design.</b> The session rows have a clock and the pool does not; the pool knows which
/// zone a kill happened in and the session row does not. So:</para>
///
/// <list type="bullet">
/// <item><b>Time, experience, coin and deaths come from the SESSION ROWS</b>, attributed to
/// the session's <c>PrimaryZone</c>. That attribution is approximate by construction — a
/// sitting that started in Crushbone and finished in Unrest counts entirely as one of
/// them — which is why <see cref="ZoneRoll.Sessions"/> travels beside every rate: the honest
/// sentence is "across 14 of your sessions here", never "in this zone".</item>
/// <item><b>Kills and fight length come from the POOL</b>, which is keyed on the zone the
/// kill actually happened in (the #65 fix). Taking kills from <c>SessionRow.Kills</c>
/// instead would be the same number measured a second way, disagreeing with the first
/// exactly when a session crossed a zone line — trap 4, one entry with two sources.</item>
/// </list>
///
/// <para><b>It does not decide anything.</b> No ranking, no wording, no "best" — those are
/// the recommender's and the presentation layer's jobs. This answers what your own log
/// already knew, in one shape, so that exactly one piece of code has to be right about
/// it.</para>
/// </summary>
public static class ZoneHistory
{
    /// <summary>
    /// The least pooled time in a zone before a rate is reported at all.
    ///
    /// <para>Fifteen minutes, and the number is a judgement rather than a measurement — it
    /// is the shortest sitting that can contain a pull, a death and a recovery, which is
    /// what an XP rate is supposed to average over. It is here rather than in the
    /// recommender because the honest answer to "what is my rate in Befallen" must be the
    /// same one wherever it is asked.</para>
    /// </summary>
    public const double MinHours = 0.25;

    /// <summary>
    /// Fold stored sessions and a pooled creature list into one row per zone.
    /// </summary>
    /// <param name="sessions">Stored session rows for the character — <c>SessionRepository.Query</c>'s
    /// own output, already scoped. A row with no <c>PrimaryZone</c> is skipped rather than
    /// folded under an empty name: "" is not a place, and a bucket named "" would sort into
    /// the recommendations as a zone nobody can travel to.</param>
    /// <param name="pool">Pooled per-creature observations — <see cref="MobHistory.Pool"/>'s
    /// own output, never re-pooled here.</param>
    /// <returns>One row per zone either source knows about, ordered by observed experience
    /// per hour, highest first; zones with no rate sort after every zone that has one, and
    /// the order inside that tail is by name so it is stable.</returns>
    public static IReadOnlyList<ZoneRoll> Fold(
        IReadOnlyList<SessionRow> sessions, IReadOnlyList<MobSummary> pool)
    {
        var acc = new Dictionary<string, Acc>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in sessions ?? [])
        {
            if (string.IsNullOrWhiteSpace(row.PrimaryZone)) continue;
            var a = At(acc, row.PrimaryZone);
            a.Sessions++;
            // Elapsed rather than ACTIVE seconds. Active time excludes the buckets where
            // nothing happened, which is exactly the downtime a camp recommendation is
            // supposed to price in: a zone you spend half your time medding in is not
            // paying you its active rate, and reporting one would flatter every bad camp.
            a.Seconds += Math.Max(0, row.ElapsedSeconds);
            a.XpPercent += row.XpPercent;
            a.Copper += row.Copper;
            a.Deaths += row.Deaths;
            if (row.EndLocal is { } ended && (a.LastPlayed is null || ended > a.LastPlayed))
                a.LastPlayed = ended;
        }

        foreach (var mob in pool ?? [])
        {
            if (string.IsNullOrWhiteSpace(mob.Zone)) continue;
            var a = At(acc, mob.Zone);
            a.Kills += mob.Kills;
            a.Mobs++;
            // Kill-weighted, so a named killed twice does not outweigh two hundred trash
            // pulls in the average a player would recognise as "how long a fight takes
            // here". A mob with no recorded fight time contributes nothing rather than
            // pulling the mean toward zero — the same "unknown is not zero" rule the pool
            // applies to coin and level bounds.
            if (mob.AvgFightSeconds > 0 && mob.Kills > 0)
            {
                a.FightSeconds += mob.AvgFightSeconds * mob.Kills;
                a.FightKills += mob.Kills;
            }
        }

        return [.. acc.Values
            .Select(a => a.Build())
            .OrderByDescending(z => z.XpPerHour ?? double.NegativeInfinity)
            .ThenBy(z => z.Zone, StringComparer.OrdinalIgnoreCase)];
    }

    private static Acc At(Dictionary<string, Acc> acc, string zone)
    {
        if (!acc.TryGetValue(zone, out var a)) acc[zone] = a = new Acc(zone.Trim());
        return a;
    }

    private sealed class Acc(string zone)
    {
        public int Sessions;
        public double Seconds;
        public double XpPercent;
        public long Copper;
        public int Deaths;
        public int Kills;
        public int Mobs;
        public double FightSeconds;
        public int FightKills;
        public DateTime? LastPlayed;

        public ZoneRoll Build() => new(
            zone, Sessions, Seconds / 3600.0, XpPercent, Copper, Deaths, Kills,
            FightKills > 0 ? FightSeconds / FightKills : 0, Mobs, LastPlayed);
    }
}
