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
/// <param name="ConnedMin">The LOWEST level any creature here ever conned at, or 0 when
/// nothing here has been conned. <b>The level band your evidence was earned at</b> (DRA-71
/// D3, plan P6) — from <see cref="MobSummary.LevelMin"/>, which is <c>/consider</c>'s own
/// reading and the only thing in this repo that knows how hard a zone's creatures are. 0 is
/// unknown and never zero: a player who has never conned anything here has said nothing
/// about the band, which is a different fact from "the creatures are level 0".</param>
/// <param name="ConnedMax">The HIGHEST level any creature here ever conned at, same
/// source and the same unknown rule.</param>
/// <param name="ConnedKills">How many of <see cref="Kills"/> those conned creatures
/// account for — the band's own denominator, so a surface can say what it rests on rather
/// than quoting a range one <c>/consider</c> produced.</param>
/// <param name="ActiveHours">Of <paramref name="Hours"/>, the part the session rows counted
/// as ACTIVE — two-minute buckets that contained a meaningful event
/// (<c>StatsSnapshot.ActiveSeconds</c>). The gap between the two is this fold's only
/// measurement of downtime, and it is a measurement rather than an inference: nothing here
/// knows WHY nothing was happening (DRA-71 D4, plan P7).</param>
/// <param name="FightKills">The kills <paramref name="AvgFightSeconds"/> was averaged over —
/// its own denominator, which is smaller than <paramref name="Kills"/> whenever a pooled
/// creature recorded no fight time. Carried for the same reason
/// <paramref name="ConnedKills"/> is: a mean whose denominator a surface cannot name is a
/// number nobody can argue with.</param>
/// <param name="CombatSeconds">Seconds of actual COMBAT across those sessions — the
/// denominator <c>StatsSnapshot.SessionDps</c> and <c>Hps</c> are quoted against, summed
/// from the archived snapshots. 0 is "no throughput measured here", never "you did
/// nothing".</param>
/// <param name="CombatDamage">Damage those combat seconds carried, reconstructed as each
/// session's own <c>SessionDps</c> × its own <c>CombatSeconds</c>.
///
/// <para><b>Reconstructed rather than read off <c>DamageDealt</c>, deliberately.</b>
/// <c>SessionStats</c> divides CLOSED-fight damage by combat seconds, and
/// <c>DamageDealt</c> is the session total — the two differ whenever a fight was still open
/// at the end, so dividing the second by the first would be a SECOND definition of "your
/// dps" that disagrees with the one every other surface prints (trap 4). Multiplying the
/// stored rate back out keeps exactly one definition and makes the pooled figure the true
/// combat-second-weighted mean rather than an average of averages.</para></param>
/// <param name="HealingDone">Healing those combat seconds carried, reconstructed the same
/// way from each session's own <c>Hps</c>, for the same reason.</param>
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
    DateTime? LastPlayedLocal,
    int ConnedMin = 0,
    int ConnedMax = 0,
    int ConnedKills = 0,
    double ActiveHours = 0,
    int FightKills = 0,
    double CombatSeconds = 0,
    double CombatDamage = 0,
    double HealingDone = 0)
{
    /// <summary>Has anything here ever been conned? Both bounds, because a band with one
    /// end is not a band.</summary>
    public bool HasConnedBand => ConnedMin > 0 && ConnedMax >= ConnedMin;

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

    // ---- throughput (DRA-71 D4, plan P7) -----------------------------------------------

    /// <summary>
    /// Was your OUTPUT measured here at all? The gate every throughput answer sits behind,
    /// and it asks two separate questions.
    ///
    /// <para><see cref="CombatSeconds"/> is the denominator, so without it there is no rate
    /// to quote. <see cref="ZoneHistory.MinHours"/> is the same floor <see cref="XpPerHour"/>
    /// keeps, for the same reason: four minutes in a zone that happened to contain one good
    /// pull is not a throughput measurement, and a recommender that ranked on it would be
    /// ranking noise. Both, or nothing is said.</para>
    /// </summary>
    public bool HasThroughput => CombatSeconds > 0 && Hours >= ZoneHistory.MinHours;

    /// <summary>Your observed damage per combat second here, or null when
    /// <see cref="HasThroughput"/> is false. Null is "not measured"; <b>0.0 is a real
    /// answer</b> — a character who healed through a zone and swung at nothing did measure a
    /// dps of zero, and that is a different fact from never having fought here.</summary>
    public double? Dps => HasThroughput ? CombatDamage / CombatSeconds : null;

    /// <summary>Your observed healing per combat second here, same gate and the same
    /// distinction between null and zero.</summary>
    public double? Hps => HasThroughput ? HealingDone / CombatSeconds : null;

    /// <summary>
    /// Damage AND healing per combat second — <b>the figure the ranking weighs</b>, and the
    /// reason it is not <see cref="Dps"/> alone.
    ///
    /// <para>A cleric's contribution to a camp is healing. Weighing a zone on damage alone
    /// would discount every place a healer actually did their job, which is a recommender
    /// telling a player their class is wrong. So the two are added: not as a claim that a
    /// point healed is worth a point dealt — nothing here knows that — but because "how much
    /// was this character doing per second of combat" is the question, and a measure that
    /// can only see one of the two answers it for exactly one kind of character.</para>
    ///
    /// <para>The SENTENCE still reports the two separately (see
    /// <c>HelperPresentation</c>): the player can see which half is theirs.</para>
    /// </summary>
    public double? OutputPerSecond => HasThroughput
        ? (CombatDamage + HealingDone) / CombatSeconds
        : null;

    /// <summary>
    /// The share of your elapsed time here that the session rows counted as INACTIVE, 0..1,
    /// or null under the floor.
    ///
    /// <para>It is the gap between <see cref="Hours"/> and <see cref="ActiveHours"/> and
    /// nothing more. <b>It does not know why</b> — medding, travelling, a bank trip and a
    /// corpse run are indistinguishable to it — so no surface built on it may name a cause
    /// (trap 73: silence beats a template with a guess in it).</para>
    /// </summary>
    public double? DowntimeShare => Hours >= ZoneHistory.MinHours && Hours > 0
        ? Math.Clamp(1 - (ActiveHours / Hours), 0, 1)
        : null;

    /// <summary>Deaths per elapsed hour here, or null under the floor. A RATE rather than a
    /// count, because one death in twenty hours and one in twenty minutes are the same count
    /// and not the same fact.</summary>
    public double? DeathsPerHour => Hours >= ZoneHistory.MinHours ? Deaths / Hours : null;

    /// <summary>
    /// The instance difficulty the player's OWN zone line recorded, decoded from
    /// <see cref="Zone"/> — <c>InstanceTier.OpenWorld</c>, a tier 0..4, or one of the
    /// unknown sentinels.
    ///
    /// <para><b>There is nothing to plumb, and that is the point.</b> A session's
    /// <c>PrimaryZone</c> is the zone name the game printed ("Najena 4 (Refined)"), stored
    /// verbatim; this fold has never normalised it and <c>RaidTargets</c> already decodes the
    /// same string the same way. So the tier is not a new observation — it is the one the
    /// player's own log made, read where it already sits, which is why it needs no schema
    /// change and why it is <c>Evidence.Personal</c> when a surface draws it.</para>
    ///
    /// <para>The one difficulty scale the game's data actually states is this one. It is
    /// reported as evidence and, in D4, weighed at nothing: the tier PREFERENCE belongs to
    /// the mote engine, which is its own slice (plan P10) — pre-empting it here would be this
    /// slice ranking on a rule nobody has signed.</para>
    /// </summary>
    public int ObservedTier => InstanceTier.FromZoneName(Zone);
}

/// <summary>
/// One archived session's own throughput, mined from its stored snapshot — the third input
/// to <see cref="ZoneHistory.Fold"/> (DRA-71 D4, plan P7).
///
/// <para><b>Why it is a separate input rather than three more columns.</b>
/// <c>SessionRow</c> carries <c>Dps</c> because <c>history.db</c> has a <c>Dps</c> column;
/// it carries no <c>Hps</c> and no <c>CombatSeconds</c>, and a rate without its denominator
/// cannot be pooled — an average of per-session averages would let a three-minute sitting
/// weigh as much as a four-hour one. Adding columns is a schema migration, which the D3
/// slice filed as its own work rather than doing quietly; probing the stored snapshot is
/// what <c>SessionRepository.ProgressSeries</c> and <c>MobRows</c> already do for exactly
/// this reason, so this joins them.</para>
///
/// <para><b>All three come from ONE probe of ONE snapshot</b> (trap 56). Taking the rate
/// from the <c>Dps</c> COLUMN and its denominator from the JSON would be two readings of one
/// moment, and they would disagree the day anything writes one without the other.</para>
/// </summary>
/// <param name="Id">The session row this describes — the join key. Rows arrive from a
/// different query in a different order from <c>SessionRepository.Query</c>'s, so the join
/// is by id and never by position.</param>
/// <param name="Dps">The session's own <c>SessionDps</c>, verbatim.</param>
/// <param name="Hps">The session's own <c>Hps</c>, verbatim.</param>
/// <param name="CombatSeconds">The denominator both were quoted against.</param>
public sealed record SessionThroughput(long Id, double Dps, double Hps, double CombatSeconds);

/// <summary>
/// <b>This character's own output, across every zone the fold could measure</b> — the yardstick
/// the throughput weight compares a single zone against (DRA-71 D4, plan P7).
///
/// <para><b>It is the answer to "compared to WHAT?", and it is deliberately the player
/// themselves.</b> EQBuddy has no mob-HP model and no con-colour model; the plan's own
/// evidence section says so. So there is no way to ask "was your dps good for this
/// creature" — and inventing a difficulty score to ask it with would be trap 73's shape with
/// arithmetic instead of prose. What CAN be asked, entirely from measurements already on
/// disk, is "was your output here what your output usually is". A zone where this character
/// put out half their usual damage-and-healing per second of combat is a zone something was
/// slowing them down, and that is an outcome rather than an adjective.</para>
///
/// <para><b>Nothing here is anybody else's number.</b> The baseline is this character's own
/// pooled combat; there is no comparison with another player, and there is no cohort.</para>
/// </summary>
/// <param name="OutputPerSecond">Pooled damage-and-healing per combat second.</param>
/// <param name="AvgFightSeconds">Pooled kill-weighted fight length.</param>
/// <param name="CombatSeconds">The combat seconds it rests on.</param>
/// <param name="FightKills">The kills the fight length rests on.</param>
/// <param name="Zones">How many zones contributed COMBAT SECONDS — the scope a sentence
/// names.</param>
/// <param name="FightZones">How many contributed a FIGHT LENGTH, which is its own count: a
/// profile can have measured combat in two zones and pooled fight lengths in one, and the
/// two halves of this record are allowed to be known separately rather than dragging each
/// other down to the weaker one.</param>
public sealed record ThroughputBaseline(
    double OutputPerSecond, double AvgFightSeconds, double CombatSeconds, int FightKills,
    int Zones, int FightZones)
{
    /// <summary>The baseline nothing could be measured from.</summary>
    public static readonly ThroughputBaseline None = new(0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Is there anything to compare against? <b>Two zones at minimum, and that is the
    /// important clause.</b>
    ///
    /// <para>A baseline folded from one zone IS that zone, so "your output here is exactly
    /// your average" would be true of every single-zone profile by construction — a
    /// tautology printed as a finding, and a discount that could never fire dressed as one
    /// that had been checked. A player with one measured camp gets their dps reported and no
    /// comparison at all.</para>
    /// </summary>
    public bool Known => Zones >= 2 && CombatSeconds > 0 && OutputPerSecond > 0;

    /// <summary>Whether the fight-length half can be compared — its own two-zone clause, for
    /// the same reason and against its own count.</summary>
    public bool FightLengthKnown => FightZones >= 2 && FightKills > 0 && AvgFightSeconds > 0;
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
/// <item><b>Throughput comes from the stored SNAPSHOTS</b> (DRA-71 D4), because that is the
/// only place the dps/hps DENOMINATOR is: <c>history.db</c> has a <c>Dps</c> column and no
/// combat seconds, and a rate with no denominator cannot be pooled without letting a
/// three-minute sitting weigh as much as a four-hour one. It rides the same primary-zone
/// attribution as the time and the experience — the same approximation, said out loud in
/// the same sentence.</item>
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
    /// <param name="throughput">Per-session dps/hps and the combat seconds they were quoted
    /// against — <c>SessionRepository.ThroughputRows</c>'s own output, joined to
    /// <paramref name="sessions"/> BY ID (DRA-71 D4). Null or empty is a real state and not a
    /// hole: a profile whose snapshots predate the probe, or a caller that does not need
    /// throughput, gets every other number unchanged and <see cref="ZoneRoll.Dps"/> answering
    /// null — which is "not measured" and never "zero".</param>
    /// <returns>One row per zone either source knows about, ordered by observed experience
    /// per hour, highest first; zones with no rate sort after every zone that has one, and
    /// the order inside that tail is by name so it is stable.</returns>
    public static IReadOnlyList<ZoneRoll> Fold(
        IReadOnlyList<SessionRow> sessions, IReadOnlyList<MobSummary> pool,
        IReadOnlyList<SessionThroughput>? throughput = null)
    {
        var acc = new Dictionary<string, Acc>(StringComparer.OrdinalIgnoreCase);
        // Indexed rather than searched, and LAST wins on a duplicate id: the probe reads one
        // row per session by primary key, so a repeat would be a bug in the query rather
        // than two observations to reconcile.
        var byId = new Dictionary<long, SessionThroughput>();
        foreach (var t in throughput ?? []) byId[t.Id] = t;

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
            // And the active half beside it, so the GAP between them can be reported as a
            // measurement (DRA-71 D4). Clamped to the elapsed figure: the two are written by
            // the same snapshot and active can never legitimately exceed elapsed, but a
            // stored row from a build where it did must not produce a negative downtime that
            // a surface would print as a percentage.
            a.ActiveSeconds += Math.Clamp(row.ActiveSeconds, 0, Math.Max(0, row.ElapsedSeconds));
            a.XpPercent += row.XpPercent;
            a.Copper += row.Copper;
            a.Deaths += row.Deaths;
            if (row.EndLocal is { } ended && (a.LastPlayed is null || ended > a.LastPlayed))
                a.LastPlayed = ended;

            // Throughput, attributed to the SAME primary zone as the time and the experience
            // above — the same approximation, named in this class's own summary, and the
            // reason the sentence a surface draws says "your sessions here" rather than "in
            // this zone". A session with no combat seconds contributes nothing at all rather
            // than a zero that would drag the pooled rate toward the floor: unknown is not
            // zero, the rule this fold already keeps for fight length and the conned band.
            if (byId.TryGetValue(row.Id, out var t) && t.CombatSeconds > 0)
            {
                a.CombatSeconds += t.CombatSeconds;
                a.CombatDamage += t.Dps * t.CombatSeconds;
                a.HealingDone += t.Hps * t.CombatSeconds;
            }
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
            // The conned band, under the SAME "unknown contributes nothing" rule the line
            // above applies to fight length. A creature nobody ever conned deserializes as
            // LevelMin 0 (the pool's own comment says so), and folding that in as a floor
            // would put every zone's band at 0 the first time somebody killed something
            // without looking at it.
            if (mob.LevelMin > 0)
            {
                a.ConnedMin = a.ConnedMin == 0 ? mob.LevelMin : Math.Min(a.ConnedMin, mob.LevelMin);
                a.ConnedMax = Math.Max(a.ConnedMax, Math.Max(mob.LevelMax, mob.LevelMin));
                a.ConnedKills += mob.Kills;
            }
        }

        return [.. acc.Values
            .Select(a => a.Build())
            .OrderByDescending(z => z.XpPerHour ?? double.NegativeInfinity)
            .ThenBy(z => z.Zone, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// This character's own output across every zone the fold measured — the yardstick a
    /// single zone's throughput is compared against (DRA-71 D4, plan P7).
    /// </summary>
    /// <param name="rolls"><see cref="Fold"/>'s own output. A second fold over the first,
    /// deliberately: the baseline is a property of the SET, so it cannot live on a row, and
    /// computing it inside the recommender would put the arithmetic one layer away from the
    /// numbers it sums — which is how a place grows a second answer to one question
    /// (trap 4).</param>
    /// <returns><see cref="ThroughputBaseline.None"/> when nothing was measured. The result
    /// is pooled — total damage-and-healing over total combat seconds — and never a mean of
    /// the rows' own rates, so a zone farmed for forty hours counts for more than one farmed
    /// for twenty minutes rather than the same.</returns>
    public static ThroughputBaseline Baseline(IReadOnlyList<ZoneRoll>? rolls)
    {
        double combat = 0, output = 0, fightSeconds = 0;
        int fightKills = 0, zones = 0, fightZones = 0;

        foreach (var z in rolls ?? [])
        {
            // The SAME gate a row's own rate sits behind. A zone under the floor is not part
            // of the yardstick, or the yardstick would be built from the noise the floor
            // exists to keep out.
            if (z.HasThroughput)
            {
                combat += z.CombatSeconds;
                output += z.CombatDamage + z.HealingDone;
                zones++;
            }
            if (z.FightKills > 0 && z.AvgFightSeconds > 0)
            {
                fightSeconds += z.AvgFightSeconds * z.FightKills;
                fightKills += z.FightKills;
                fightZones++;
            }
        }

        if (zones == 0 && fightZones == 0) return ThroughputBaseline.None;
        return new ThroughputBaseline(
            combat > 0 ? output / combat : 0,
            fightKills > 0 ? fightSeconds / fightKills : 0,
            combat, fightKills, zones, fightZones);
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
        public double ActiveSeconds;
        public double XpPercent;
        public long Copper;
        public int Deaths;
        public int Kills;
        public int Mobs;
        public double FightSeconds;
        public int FightKills;
        public double CombatSeconds;
        public double CombatDamage;
        public double HealingDone;
        public DateTime? LastPlayed;
        public int ConnedMin;
        public int ConnedMax;
        public int ConnedKills;

        public ZoneRoll Build() => new(
            zone, Sessions, Seconds / 3600.0, XpPercent, Copper, Deaths, Kills,
            FightKills > 0 ? FightSeconds / FightKills : 0, Mobs, LastPlayed,
            ConnedMin, ConnedMax, ConnedKills,
            ActiveSeconds / 3600.0, FightKills, CombatSeconds, CombatDamage, HealingDone);
    }
}
