namespace EQBuddy.Core;

/// <summary>
/// One creature that has actually given this character motes, and where.
/// </summary>
/// <param name="Mob">The creature, as the pool spells it.</param>
/// <param name="Zone">The zone the kills happened in — the pool's own key, which is the real
/// kill zone rather than a session's primary one.</param>
/// <param name="Motes">How many motes of every kind it dropped.</param>
/// <param name="Potency">What those motes are worth in upgrade experience, through
/// <see cref="Motes.PotencyOf"/> — the ladder the wiki publishes, never a formula.</param>
/// <param name="Kills">How many kills of it are pooled — the denominator, so a surface can
/// say what the drop rate rests on rather than calling it common.</param>
public sealed record MoteSource(string Mob, string Zone, int Motes, int Potency, int Kills);

/// <summary>
/// **What one zone has actually paid this character in motes** (DRA-71 D7, plan P10; Founder
/// smoke item 5).
/// </summary>
/// <param name="Zone">The zone as the log spells it, never normalised — <see cref="ZoneMap"/>
/// owns aliases and a fold that renamed a place would be a second spelling of one zone.</param>
/// <param name="Motes">Every mote looted here, of every tier.</param>
/// <param name="Potency">The upgrade experience those motes are worth.</param>
/// <param name="VoidTouched">How many of <paramref name="Motes"/> were the raid-only
/// <see cref="Core.Motes.VoidTouched"/>.
///
/// <para><b>It is carried separately because it weighs NOTHING and is worth the MOST.</b>
/// <see cref="Core.Motes.PotencyOf"/> answers 0 for it, deliberately — its worth is a whole
/// item tier rather than a number of points, and the wiki publishes no points for it. A zone
/// whose only motes were Void-Touched therefore has a potency of zero, and a row that printed
/// "0 an hour" and stopped would be telling a player their raid night produced nothing. So the
/// count rides its own field, the sentence names it, and the ARITHMETIC still refuses to make
/// up a value for it (trap 73: silence beats a guessed number, and a guessed number in a
/// ranking is worse than one in a sentence).</para></param>
/// <param name="Kills">How many pooled kills happened in this zone — the cadence's own
/// denominator, and the Founder's "frequent kills" measured rather than asserted.</param>
/// <param name="Creatures">How many pooled creatures here dropped at least one mote.</param>
/// <param name="Hours">Elapsed hours of the stored sessions whose PRIMARY zone was this one —
/// <see cref="ZoneRoll.Hours"/>, joined in rather than re-counted.
///
/// <para><b>The split between the two sources is the same one <see cref="ZoneHistory"/>
/// documents, and for the same reason</b> (trap 4): the session rows have a clock and the pool
/// does not; the pool knows which zone a kill happened in and the session row does not. So the
/// motes and the kills come from the POOL and the hours come from the SESSION ROWS, which is
/// why a rate here is worded "across N of your sessions" and never "in this zone".</para></param>
/// <param name="Sessions">How many of those sessions there were — the scope beside the
/// rate.</param>
/// <param name="ConnedMin">The level band the zone's creatures conned at, from
/// <see cref="ZoneRoll.ConnedMin"/>. Carried so the mote engine can consume the character's
/// level through the same <c>/consider</c> evidence the experience engine does, rather than
/// growing a second reading of "how hard is this place".</param>
/// <param name="ConnedMax">The top of that band, same source and the same unknown rule: 0 is
/// "nothing here was ever conned" and never "level 0".</param>
/// <param name="ConnedKills">How many kills that band rests on.</param>
/// <param name="Tier">The instance tier this character's OWN zone line recorded —
/// <see cref="ZoneRoll.ObservedTier"/>, decoded from the name the game printed. Not a lookup,
/// and the only difficulty scale the game's data states.</param>
/// <param name="Top">The creature that gave the most POTENCY here, or null when nothing did.
/// One answer to "who drops them", from the one source that cannot be stale.</param>
public sealed record MoteRoll(
    string Zone,
    int Motes,
    int Potency,
    int VoidTouched,
    int Kills,
    int Creatures,
    double Hours,
    int Sessions,
    int ConnedMin,
    int ConnedMax,
    int ConnedKills,
    int Tier,
    MoteSource? Top)
{
    /// <summary>
    /// Upgrade experience per hour here, or null when there is not enough of your own play to
    /// divide.
    ///
    /// <para>The floor is <see cref="ZoneHistory.MinHours"/> — the same fifteen minutes the
    /// experience rate keeps, and for the same reason. <b>And a second floor beside it</b>
    /// (<see cref="MoteHistory.MinKills"/>): a mote rate divides a handful of drops by a
    /// handful of hours, so one lucky Infinite mote in a twenty-minute sitting is a rate of
    /// thirty an hour that nobody will ever see again. Time alone cannot see that, because the
    /// time was real.</para>
    /// </summary>
    public double? PotencyPerHour => HasRate ? Potency / Hours : null;

    /// <summary>Motes per hour here, on the same two floors. Reported beside the potency
    /// because a hundred Infinitesimal motes and a hundred Infinite motes are the same COUNT
    /// and not the same hour — the distinction <see cref="MotesSummary.Potency"/> exists
    /// for.</summary>
    public double? MotesPerHour => HasRate ? Motes / Hours : null;

    /// <summary>Your pooled kills here per elapsed hour — <b>the Founder's "frequent kills"
    /// as a measurement</b>. Null under the same floors: a cadence divided out of ten minutes
    /// is an evening rather than a camp.</summary>
    public double? KillsPerHour => HasRate ? Kills / Hours : null;

    /// <summary>Is there enough of your own play here for a mote answer to lean on? Three
    /// clauses, and each one is a different way of having nothing: no hours to divide by, too
    /// few kills for a drop rate to mean anything, and no mote ever seen.</summary>
    public bool HasRate =>
        Hours >= ZoneHistory.MinHours && Kills >= MoteHistory.MinKills && Motes > 0;

    /// <summary>Has this character conned anything here? Both ends, because a band with one
    /// end is not a band — <see cref="ZoneRoll.HasConnedBand"/>'s own rule.</summary>
    public bool HasConnedBand => ConnedMin > 0 && ConnedMax >= ConnedMin;
}

/// <summary>
/// **WHERE MOTES HAVE ACTUALLY DROPPED FOR YOU** — the fold the Helper's Farm Motes answer
/// reads (DRA-71 D7, plan P10; the fold #580 filed for D7 and nobody built until now).
///
/// <para><b>It is a fold and not a collection.</b> Every number is already on disk:
/// <see cref="MobHistory.Pool"/> has folded every creature this character has killed, keyed on
/// (name, zone), with its loot; <see cref="Motes"/> already knows which of those loot lines is
/// a mote and what it is worth. Nobody had ever joined them. Nothing new is recorded, nothing
/// is written, and no session is re-parsed.</para>
///
/// <para><b>ONE PRODUCER PER FACT.</b> The motes, the kills and the creatures come from the
/// POOL. The hours, the session count and the conned band come from <see cref="ZoneRoll"/> —
/// joined in by zone name rather than recomputed, because <see cref="ZoneHistory"/> is already
/// the one place that turns session rows into hours and a second one would disagree with it
/// the first time either changed (trap 4). This fold adds exactly one new fact to the repo:
/// what a zone has paid in motes.</para>
///
/// <para><b>It does not decide anything.</b> No ranking, no wording, no preference for a tier
/// — those belong to <see cref="Recommendations"/> and <c>HelperPresentation</c>. This answers
/// what the log already knew, in one shape.</para>
///
/// <para><b>There is no catalog half, and the survey is the reason.</b> The plan allowed the
/// executor to check whether mote items carry <c>DropZones</c> in the shipped catalog. They do
/// — and not one of the eleven values is a place: ten say "Various Zones" or "Unknown" and one
/// says "D3+ Zones". A drop zone that cannot be travelled to is not a drop zone, so there are
/// no catalog mote rows and the empty state says what EQBuddy is waiting for
/// (<c>MoteCatalogSurveyTests</c> holds that finding against the shipped file, and fails the
/// day a real zone arrives so somebody decides rather than nobody noticing).</para>
/// </summary>
public static class MoteHistory
{
    /// <summary>
    /// The fewest pooled kills in a zone before a mote rate is quoted at all.
    ///
    /// <para>Fifty, and <b>the number is a judgement rather than a measurement</b> — the same
    /// admission <see cref="ZoneHistory.MinHours"/> makes about its fifteen minutes. It exists
    /// because a mote rate has a failure mode an hours floor cannot see: one Infinite mote in
    /// a legitimate twenty-minute sitting is thirty potency an hour, measured correctly, and
    /// it will never happen again. Fifty kills is the point at which a drop this rare has been
    /// given a chance to be ordinary.</para>
    ///
    /// <para>It is a FLOOR and not a discount: under it the zone answers nothing at all rather
    /// than a number with a caveat, because a rate nobody should act on is not improved by
    /// saying so beside it (trap 73).</para>
    /// </summary>
    public const int MinKills = 50;

    /// <summary>
    /// Fold pooled creatures and the zone rollup into one row per zone that has paid motes.
    /// </summary>
    /// <param name="pool"><see cref="MobHistory.Pool"/>'s own output, never re-pooled here.</param>
    /// <param name="zones"><see cref="ZoneHistory.Fold"/>'s own output — the clock. A zone the
    /// pool knows about and the session rows do not gets a row with no hours, which answers no
    /// rate: that is the honest state for a zone whose kills arrived under another session's
    /// primary zone, and dropping it would hide motes the player really did loot.</param>
    /// <returns>One row per zone where at least one mote was looted, ordered by potency per
    /// hour, highest first; rows with no rate sort after every row that has one and are
    /// ordered by name so the list is stable.</returns>
    public static IReadOnlyList<MoteRoll> Fold(
        IReadOnlyList<MobSummary> pool, IReadOnlyList<ZoneRoll> zones)
    {
        var byZone = new Dictionary<string, ZoneRoll>(StringComparer.OrdinalIgnoreCase);
        foreach (var z in zones ?? []) byZone[z.Zone] = z;

        var acc = new Dictionary<string, Acc>(StringComparer.OrdinalIgnoreCase);
        foreach (var mob in pool ?? [])
        {
            if (string.IsNullOrWhiteSpace(mob.Zone)) continue;

            int motes = 0, potency = 0, voids = 0;
            foreach (var loot in mob.Loot)
            {
                if (!Motes.IsMote(loot.Item)) continue;
                motes += loot.Count;
                potency += loot.Count * Motes.PotencyOf(loot.Item);
                if (loot.Item.Trim().Equals(Motes.VoidTouched, StringComparison.OrdinalIgnoreCase))
                    voids += loot.Count;
            }
            if (motes == 0) continue;

            if (!acc.TryGetValue(mob.Zone, out var a)) acc[mob.Zone] = a = new Acc(mob.Zone.Trim());
            a.Add(mob, motes, potency, voids);
        }

        // The KILLS come from the pool as a whole and not only from the creatures that dropped
        // something. "How often do you kill things here" is the cadence question, and counting
        // only the generous creatures would answer a different one — a camp where one rare
        // named drops a mote and nothing else does would read as a place you kill once an hour.
        foreach (var mob in pool ?? [])
            if (!string.IsNullOrWhiteSpace(mob.Zone)
                && acc.TryGetValue(mob.Zone, out var a)) a.Kills += mob.Kills;

        return [.. acc.Values
            .Select(a => a.Build(byZone.TryGetValue(a.Zone, out var z) ? z : null))
            .OrderByDescending(m => m.PotencyPerHour ?? double.NegativeInfinity)
            .ThenBy(m => m.Zone, StringComparer.OrdinalIgnoreCase)];
    }

    private sealed class Acc(string zone)
    {
        public readonly string Zone = zone;
        public int Kills;
        private int _motes, _potency, _voids, _creatures;
        private MoteSource? _top;

        public void Add(MobSummary mob, int motes, int potency, int voids)
        {
            _motes += motes;
            _potency += potency;
            _voids += voids;
            _creatures++;
            var source = new MoteSource(mob.Name, Zone, motes, potency, mob.Kills);
            // By POTENCY and not by count: "who is worth killing for motes here" is the
            // question, and a creature that gave you forty Infinitesimals is not a better
            // answer than one that gave you four Infinites. Count breaks the tie, because at
            // equal potency the one you have actually seen more of is the one you can repeat.
            if (_top is null || potency > _top.Potency
                || (potency == _top.Potency && motes > _top.Motes)) _top = source;
        }

        public MoteRoll Build(ZoneRoll? z) => new(
            Zone, _motes, _potency, _voids, Kills, _creatures,
            z?.Hours ?? 0, z?.Sessions ?? 0,
            z?.ConnedMin ?? 0, z?.ConnedMax ?? 0, z?.ConnedKills ?? 0,
            z?.ObservedTier ?? InstanceTier.Unknown,
            _top);
    }
}
