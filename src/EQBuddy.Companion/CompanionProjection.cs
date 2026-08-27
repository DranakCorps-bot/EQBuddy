using EQBuddy.Core;

namespace EQBuddy.Companion;

/// <summary>
/// Projects the desktop's already-built state (the per-tick shared StatsSnapshot plus
/// the live trackers) into the wire DTO. Pure functions: the host owns when to call,
/// the server owns when (and to whom) to send.
///
/// Every surface is built ONLY when the desktop gate offers it, so a withheld surface
/// doesn't exist in memory to leak, let alone send. Each also gets a
/// <see cref="SectionFingerprints"/> entry — the per-section change detection that
/// keeps a mez-only phone from being woken by a loot line.
/// </summary>
public static partial class CompanionProjection
{
    /// <summary>A timer with this much or less left counts as "imminent" — the page
    /// pulses those rows. Matches the "get ready to camp" horizon, not a lore number.</summary>
    public const double ImminentSeconds = 120;

    /// <summary>Rows per list. A phone scrolls; a phone does not want 400 rows, and
    /// the desktop's own breakouts cap in the same neighborhood.</summary>
    private const int MaxRows = 20;

    /// <summary>Build the full offered snapshot. The gate (Options → EQBuddy Mobile)
    /// decides which sections are projected at all, so gated data doesn't exist in
    /// memory to leak, let alone send.</summary>
    public static CompanionSnapshot Build(CompanionInputs input, DateTime now)
    {
        var offered = input.Offered ?? CompanionSurfaces.All;
        var stats = input.Stats;
        bool On(string surface) => offered.Contains(surface, StringComparer.OrdinalIgnoreCase);

        return new CompanionSnapshot
        {
            Version = stats?.Version ?? 0,
            SentAtUtc = now.ToUniversalTime(),
            Identity = new CompanionIdentity(input.Character, stats?.CurrentZone ?? "", input.AppVersion),
            Offered = offered,
            Theme = input.Theme,
            Map = On(CompanionSurfaces.Map) ? input.Map : null,
            Spawns = On(CompanionSurfaces.Spawns) ? new CompanionSpawnSection(BuildTimers(input.Timers, now)) : null,
            Travel = On(CompanionSurfaces.Travel)
                ? BuildTravel(input.ZoneGraph, stats?.CurrentZone ?? "", input.TravelDestination) : null,
            Mez = On(CompanionSurfaces.Mez) ? BuildMez(input.Mezzes, now) : null,
            Buffs = On(CompanionSurfaces.Buffs) ? BuildBuffs(input.BuffSets, input.BuffLosses, now) : null,
            Combat = On(CompanionSurfaces.Combat) ? BuildCombat(stats) : null,
            Session = On(CompanionSurfaces.Session)
                ? new CompanionSessionSection(
                    Kills: stats?.YourKillCount ?? 0,
                    XpPerHour: stats?.XpPerHour ?? 0,
                    SessionSeconds: stats?.Elapsed.TotalSeconds ?? 0,
                    SessionDps: stats?.SessionDps ?? 0)
                : null,
            Loot = On(CompanionSurfaces.Loot) ? BuildLoot(stats) : null,
            Progress = On(CompanionSurfaces.Progress)
                ? BuildProgress(stats, input.Level, input.Unlocks, input.Raids,
                    input.UnlockClasses, input.NextUnlocks)
                : null,
            Quests = On(CompanionSurfaces.Quests)
                ? BuildQuests(input.Settings, input.Quests, input.QuestIndex) : null,
            Gear = On(CompanionSurfaces.Gear) ? BuildGear(input.Settings, input.HopsFromHere) : null,
        };
    }

    /// <summary>The Phase 1 call shape, kept so spawn/session callers and their tests
    /// don't have to know about the bundle.</summary>
    public static CompanionSnapshot Build(
        StatsSnapshot? stats,
        IReadOnlyList<SpawnTimerState> timers,
        string character,
        string appVersion,
        DateTime now,
        IReadOnlyList<string>? offered = null) =>
        Build(new CompanionInputs
        {
            Stats = stats,
            Timers = timers,
            Character = character,
            AppVersion = appVersion,
            Offered = offered ?? [CompanionSurfaces.Spawns, CompanionSurfaces.Session],
        }, now);

    /// <summary>The Path tab (World PR 4) — the SAME <see cref="TravelPlan"/> module the
    /// desktop Path tab reads, so the phone cannot compute a different route (#210's
    /// rule). No zone graph (a test host, or the surface built before one is wired)
    /// still answers, with an empty zone list and "noroute".</summary>
    private static CompanionTravelSection BuildTravel(ZoneGraph? graph, string from, string? destination)
    {
        if (graph is null)
            return new CompanionTravelSection(from, destination, [], "noroute", 0, [],
                "This copy of EQBuddy has no zone graph loaded.");
        if (string.IsNullOrWhiteSpace(destination))
            return new CompanionTravelSection(from, null, graph.Zones.ToList(), "noroute", 0, [],
                "Pick a destination.");

        var result = TravelPlan.Plan(graph, from, destination);
        return new CompanionTravelSection(
            from, destination, graph.Zones.ToList(),
            result.Outcome.ToString().ToLowerInvariant(),
            result.Hops, result.Path, result.Note);
    }

    private static List<CompanionSpawnTimer> BuildTimers(IReadOnlyList<SpawnTimerState> timers, DateTime now)
    {
        var rows = new List<CompanionSpawnTimer>(timers.Count);
        foreach (var t in timers)
        {
            double? remaining = t.DueAt is { } due ? Math.Max(0, (due - now).TotalSeconds) : null;
            var isDue = t.IsDue(now);
            rows.Add(new CompanionSpawnTimer(
                t.Name, t.Zone, remaining,
                Due: isDue,
                Imminent: !isDue && remaining is { } r && r <= ImminentSeconds,
                DurationSeconds: t.DurationSeconds));
        }
        // Soonest first; due rows (remaining 0) naturally float to the top; unknown
        // durations sink to the bottom — the page renders in this order as-is.
        rows.Sort((a, b) => (a.RemainingSeconds ?? double.MaxValue)
            .CompareTo(b.RemainingSeconds ?? double.MaxValue));
        return rows;
    }

    /// <summary>
    /// What "the version moved" means for push decisions, PER SECTION: a stable string
    /// over each surface's DATA identity, deliberately excluding the values that drift
    /// every single tick (remaining seconds, session length, xp rate) — the page ticks
    /// those locally. A section absent from the result is a section not offered.
    ///
    /// The host diffs this against the previous tick's map and hands the changed names
    /// to the server, which wakes only the devices subscribed to one of them: a phone
    /// showing mez chips is not woken by a loot line, and a phone showing loot is not
    /// woken by a mez landing. The envelope's own identity (character, zone, the offer
    /// list, the theme) rides <see cref="EnvelopeSection"/>, which wakes everyone.
    /// </summary>
    public static Dictionary<string, string> SectionFingerprints(CompanionSnapshot snap)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EnvelopeSection] =
                $"{snap.Identity.Character}|{snap.Identity.Zone}|{string.Join(',', snap.Offered)}|{snap.Theme?.Stamp}",
        };

        if (snap.Map is { } m)
            map[CompanionSurfaces.Map] = Fold(m.Zone, m.GeometryStamp, m.Missing,
                m.You is { } you ? $"{you.X:0.#},{you.Y:0.#}" : "-",
                Join(m.Circles, c => $"{c.X:0}:{c.Y:0}:{c.Label}:{c.Imminent}:{c.Confirmed}:{c.Kills}"),
                // Crumb POSITIONS only. A trail that is merely fading is not news — the
                // page burns it down locally on the same curve, and shipping ages here
                // would wake every map device every single second.
                Join(m.Trail, c => $"{c.X:0}:{c.Y:0}"),
                // Named countdowns tick on the page like every other clock; a named is
                // news when it appears, its camp resolves or moves, or it flips to DUE.
                Join(m.Named, n => $"{n.Name}:{n.X:0}:{n.Y:0}:{n.Due}:{n.FromWiki}"));

        if (snap.Spawns is { } sp)
            map[CompanionSurfaces.Spawns] = Join(sp.Timers,
                t => $"{t.Zone}/{t.Name}:{(t.Due ? 'D' : t.Imminent ? 'I' : '-')}:{t.DurationSeconds ?? -1}");

        // Trap 8: no clock rides this key. The route recomputes from the CURRENT zone
        // every tick (trap 38 says this surface must not go sticky), but its identity —
        // whether it is worth re-sending to a subscribed device — is the from/destination/
        // outcome/path, not "did a second pass". A zone change with the same destination
        // legitimately changes this key, which is exactly the point.
        if (snap.Travel is { } tr)
            map[CompanionSurfaces.Travel] =
                Fold(tr.From, tr.Destination ?? "", tr.Outcome, Join(tr.Path, p => p));

        if (snap.Mez is { } mz)
            map[CompanionSurfaces.Mez] = Join(mz.Chips, c => $"{c.Name}:{c.Warning}");

        if (snap.Buffs is { } bf)
            map[CompanionSurfaces.Buffs] = Fold(
                Join(bf.Groups, g => g.Class + "=" + Join(g.Rows, r => $"{r.Spell}:{r.Status}")),
                Join(bf.Lost, l => $"{l.Spell}:{l.Cause}"));

        if (snap.Combat is { } cb)
            map[CompanionSurfaces.Combat] = Join(cb.Boards,
                b => $"{b.Key}:{b.FightHeader}:{Join(b.Fight, r => $"{r.Name}={r.Total}")}" +
                     $":{Join(b.Session, r => $"{r.Name}={r.Total}")}");

        // Session's numbers all drift every tick; its identity is the kill count (the
        // one step change), and the forced refresh carries the rest.
        if (snap.Session is { } se)
            map[CompanionSurfaces.Session] = se.Kills.ToString();

        if (snap.Loot is { } lt)
            map[CompanionSurfaces.Loot] = Fold($"{lt.Total}/{lt.CraftedTotal}",
                Join(lt.Items, i => $"{i.Name}={i.Count}"),
                Join(lt.Watch, w => $"{w.Name}={w.Total}"));

        // The theme's four tabs in one fingerprint. Coin, motes, faction and raid clears
        // all move in STEPS (a drop, a sale, a kill), so they belong here — but the
        // per-hour rates and the xp fraction drift every tick, and including one would
        // wake every paired device once a second (trap 8). XpPercent is truncated to a
        // whole number for exactly that reason and stays that way.
        // The next-level preview joins on the same terms: its identity is the LEVEL, the
        // class split and which rows are in each group — all step changes (a ding, a class
        // pick). Its `MoteLine` deliberately does NOT, for the reason the block below
        // states: the rate drifts on the clock with the total standing still, and one
        // drifting value in a key wakes every paired device for nothing.
        if (snap.Progress is { } pr)
            map[CompanionSurfaces.Progress] = Fold(
                $"{pr.Level}|{pr.AaTotal}|{pr.Unlocks.Count}|{(int)pr.XpPercent}",
                pr.NextLabel + "|" + (pr.NextGrouped ? "g" : "-") + "|" +
                    Join(pr.NextGroups ?? [], g => g.Class + "=" +
                        Join(g.Rows, r => r.Name) + (g.Empty is null ? "" : "!")),
                // Coin, then the mote LADDER. It was `Wealth.MotesSummary` until
                // 2026-08-23, which is the rate — the one value in this record that moves
                // on the clock while nothing is happening, so it both woke paired devices
                // for nothing AND was the only thing standing in for "a mote dropped".
                // The tiers are the step change; the rate rides the forced refresh with
                // xp/hr and the rest (trap 8).
                pr.Wealth.Total + "/" + Join(pr.Wealth.Motes, m => $"{m.Name}={m.Count}"),
                Join(pr.Wealth.Sold, i => $"{i.Name}={i.Count}"),
                Join(pr.Faction, f => $"{f.Name}={f.Count}"),
                pr.Raids.Defeated.ToString());

        // Quests: everything here is a step change (a loot, a pin, a tick) — no clock
        // drifts through it, so nothing needs excluding. The catalog itself rides only
        // as its stamp: the payload is sticky and its identity IS the stamp.
        if (snap.Quests is { } qs)
            map[CompanionSurfaces.Quests] = Fold(
                qs.CatalogStamp,
                Join(qs.Tabs, t => $"{t.Key}:{t.Badge}"),
                Join(qs.Mine, n => n) + "+" + qs.MineMore,
                Join(qs.Owned.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase),
                    kv => $"{kv.Key}={kv.Value}"),
                Join(qs.Tracked, t => t),
                Join(qs.Hidden, h => h),
                Join(qs.Completed.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase),
                    kv => $"{kv.Key}={kv.Value}"),
                Join(qs.Classes, c => c.Abbrev),
                qs.InferredClass,
                // The resolved list and its source: step changes (a dump read, a pick, a
                // class clearing its evidence floor), never a per-tick drift.
                Join(qs.CharacterClasses ?? [], c => c) + "|" + qs.ClassSourceLabel,
                ChecklistPrint(qs.Epics),
                ChecklistPrint(qs.Sky));

        AddChecklist(map, CompanionSurfaces.Gear, snap.Gear);
        return map;

        static void AddChecklist(Dictionary<string, string> into, string surface, CompanionChecklistSection? section)
        {
            if (section is null) return;
            into[surface] = ChecklistPrint(section);
        }

        static string ChecklistPrint(CompanionChecklistSection section) =>
            Fold($"{section.Done}/{section.Total}",
                Join(section.Groups, g => g.Heading + "=" + Join(g.Rows, r => $"{r.Id}:{(r.Done ? '1' : '0')}")));
    }

    /// <summary>The pseudo-section for envelope-level change (who/where/the gate/the
    /// theme): every connected device is woken by it, whatever it subscribed to. The
    /// leading space keeps it out of the surface namespace.</summary>
    public const string EnvelopeSection = " envelope";

    /// <summary>The Phase 1 whole-snapshot fingerprint, now the section map flattened —
    /// still the answer to "did anything at all move".</summary>
    public static string Fingerprint(CompanionSnapshot snap)
    {
        var sections = SectionFingerprints(snap);
        return $"{snap.Version}|" + string.Join('|',
            sections.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value));
    }

    private static string Fold(params string?[] parts) => string.Join('|', parts);

    private static string Join<T>(IEnumerable<T> items, Func<T, string> of) =>
        string.Join(';', items.Select(of));
}
