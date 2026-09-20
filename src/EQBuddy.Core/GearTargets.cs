namespace EQBuddy.Core;

/// <summary>
/// One tracked goal's answer in ONE zone: the item, the zone as the item's own page spells
/// it, and the creatures that page named there.
/// </summary>
/// <param name="Item">The tracked goal's key — <see cref="TrackedUpgradeStore.Key"/>'s answer,
/// so the string here is the one the store holds and the one the catalog was looked up under.
/// It is repeated on every zone row because a row has to stand alone in a merged list.</param>
/// <param name="Zone">The zone as <see cref="ItemCatalog.Record.DropZones"/> spells it, never
/// re-spelled to the log's or the map pack's version. It is what a player reads, and
/// <see cref="GearTargetSet.Here"/> is the one place two spellings are ever compared.</param>
/// <param name="Creatures">The creatures the page named in this zone, in the PAGE's own order
/// and <b>uncapped</b> — this list is what a spawn point is matched against, and a cap here
/// would silently stop lighting the dot for the fourth creature on the page. The CAP is the
/// sentence's (<see cref="GearTargetSet"/>'s readers), which is where it can say so
/// (trap 50).
///
/// <para>Empty where the page listed the zone and named nobody under it. That is still a real
/// row — the item does drop there — it just cannot ever light a dot, because a spawn point is
/// matched by creature name and there is no name to match. An unanswered question draws
/// nothing rather than a zero (trap 73).</para></param>
public sealed record GearTargetZone(string Item, string Zone, IReadOnlyList<string> Creatures);

/// <summary>Why one tracked goal became no place at all. Both arms are REPORTED by name and
/// never silently dropped (trap 50) — a goal that vanished off the map with no sentence is
/// indistinguishable from a goal EQBuddy forgot about.</summary>
public enum GearTargetGap
{
    /// <summary>The catalog holds no record under this name at all. <b>The subject of the
    /// sentence is EQBuddy's catalog, never the game</b> — the same posture
    /// <c>HelperPresentation.UnreadWorn</c> keeps for a worn row it cannot describe.</summary>
    Unreadable,

    /// <summary>The record exists and names nowhere this build will read as a place: no
    /// <see cref="ItemCatalog.Record.DropZones"/> at all (3,104 of the 6,844 wearable records —
    /// quest rewards, vendor stock and craft output), or every entry refused by
    /// <see cref="TradeskillMaterials.IsPlace"/>.</summary>
    NoDropZone,
}

/// <summary>One tracked goal the catalog could not turn into a place, and which of the two
/// reasons it was.</summary>
public sealed record GearTargetRefusal(string Item, GearTargetGap Why);

/// <summary>One tracked goal a spawn point answers: which goal, and which of its creatures was
/// killed there. Both halves are needed in the sentence — a ring saying only "a target" over a
/// point that has seen five different mobs does not say which one to wait for.</summary>
public sealed record GearTargetHit(string Item, string Creature);

/// <summary>
/// **EVERY TRACKED GOAL, TURNED INTO PLACES** — the whole answer for one character, built once
/// per pass (DRA-216 D5, S13).
/// </summary>
/// <param name="Zones">One row per (goal, zone) the catalog admits, in the tracked order then
/// the page's own zone order. Nothing here is ranked: the map has no evidence to rank a zone
/// with, and the room that does have some is not this surface.</param>
/// <param name="Refused">The goals that produced no zone, with the reason.</param>
public sealed record GearTargetSet(
    IReadOnlyList<GearTargetZone> Zones,
    IReadOnlyList<GearTargetRefusal> Refused)
{
    /// <summary>Nothing tracked, or nothing the catalog can place. Callers draw NOTHING on it
    /// rather than an empty state — the <c>BuildTracked</c> rule one slice back.</summary>
    public bool IsEmpty => Zones.Count == 0 && Refused.Count == 0;

    /// <summary>
    /// The goals whose zone IS this zone — <b>exact title, then
    /// <see cref="ZoneMapFiles.IdentityKey"/>, and NOTHING looser</b>.
    ///
    /// <para>That is <see cref="ZoneLevels"/>' and <see cref="ZoneEras"/>' rule verbatim, and
    /// the reason is the same one they give: containment would match a zone name inside another
    /// zone's name. The committed exhibit is in the shipped catalog — <i>Bronze Long Sword</i>
    /// names <c>Commonlands</c>, and a player standing in <c>West Commonlands</c> must not get a
    /// target ring for it. A wrong band is a number a surface states as fact; a wrong target is
    /// a dot a player walks to.</para>
    ///
    /// <para><see cref="ZoneMapFiles.IdentityKey"/> already folds the instance tier, so
    /// "Befallen 4 (Refined)" finds "Befallen" — which is also why callers may hand this the
    /// LOG's zone name or the catalog's and get one answer either way.</para>
    /// </summary>
    public IReadOnlyList<GearTargetZone> Here(string? zone)
    {
        if (zone is not { Length: > 0 } || Zones.Count == 0) return [];
        var key = ZoneMapFiles.IdentityKey(zone);
        if (key.Length == 0) return [];
        return [.. Zones.Where(z =>
            z.Zone.Equals(zone, StringComparison.OrdinalIgnoreCase)
            || ZoneMapFiles.IdentityKey(z.Zone).Equals(key, StringComparison.OrdinalIgnoreCase))];
    }
}

/// <summary>
/// **WHERE THE THING YOU ARE GOING AFTER ACTUALLY DROPS** — the join from a tracked upgrade
/// goal to a zone, a creature, and the spawn points you have archived for it (DRA-216 D5, S13;
/// plan §3 Q8).
///
/// <para><b>It is a READER and never a store.</b> <see cref="TrackedUpgrade"/> deliberately
/// holds no drop zone (D4's own summary says why: <see cref="ItemCatalog"/> is the one producer
/// of where an item comes from, and a place frozen at track time goes quietly wrong the first
/// time a refresh moves it — trap 4). So the place is asked for HERE, every pass, off the same
/// catalog the Helper's gear sweep reads.</para>
///
/// <para><b>NO SECOND MAP ENGINE, and that is the plan's constraint rather than a preference</b>
/// (S13.1, S20). Everything geometric already exists: <see cref="SpawnPointLedger"/> archives a
/// point per cluster of kills with a fresh <c>/loc</c> behind it, <see cref="ZoneMap.FromLoc"/>
/// puts a game coordinate in map space, and the map already draws a circle per point with a
/// countdown on it. What was missing is one question — <i>is this dot one of MINE?</i> — and
/// this file is that question and nothing else. It computes no coordinate, loads no file, and
/// returns no geometry.</para>
///
/// <para><b>S13.5 (several known spawn points) falls out for free</b>, because
/// <c>ZoneArchive.Points</c> is a list and <see cref="AtPoint"/> is asked once per point. There
/// is no "the" spawn point anywhere in here.</para>
///
/// <para><b>Measured on the shipped catalog before any of it was drawn</b>
/// (<c>GearTargetsTests</c> pins each number, so a refresh that moves one says so): of 6,844
/// wearable records, 3,104 name no drop zone at all and 3,713 name at least one place. Of the
/// 6,004 (item, zone) pairs, 5,964 are a place — 40 are not, in 15 distinct spellings, and
/// <see cref="TradeskillMaterials.IsPlace"/> refuses every one of them — 5,871 name a creature,
/// and 2,213 name a creature <see cref="SpawnCatalog"/> knows as a zone NAMED. That last
/// number is the S14 split: about a third of targets get a real learned countdown, and the
/// rest get the ordinary point's projected respawn, which is exactly what the circle under
/// them already showed.</para>
/// </summary>
public static class GearTargets
{
    /// <summary>How many zones a sentence about ONE goal may name before it counts the rest.
    /// <i>Bronze Long Sword</i> drops in 23 of them, and a map side panel that listed all 23 is
    /// not a panel. The cap SAYS so, in the reader's own words (trap 50).</summary>
    public const int ZonesPerItem = 3;

    /// <summary>
    /// Turn what this character is going after into places.
    ///
    /// <para><b>The identity is the store's own key</b> —
    /// <see cref="TrackedUpgradeStore.Key"/>, which is
    /// <see cref="EqlWikiItemService.NormalizeTitle"/>, the ONE seam. So the name a goal is
    /// looked up under here cannot disagree with the name it was stored under, and a "+5" and
    /// its base title are one goal on the map exactly as they are one goal in the block
    /// (S8 is PARKED: nothing here holds, compares or invents a "+N").</para>
    ///
    /// <para>One row per (goal, zone). A goal that resolves to nothing is a
    /// <see cref="GearTargetRefusal"/> rather than a silence, and duplicate zone spellings on
    /// one page collapse on <see cref="ZoneMapFiles.IdentityKey"/> — the page is transcription,
    /// and a zone listed twice is one place said twice.</para>
    /// </summary>
    public static GearTargetSet For(
        IReadOnlyList<TrackedUpgrade>? tracked, ItemCatalog? catalog)
    {
        if (tracked is not { Count: > 0 }) return new GearTargetSet([], []);

        var zones = new List<GearTargetZone>();
        var refused = new List<GearTargetRefusal>();
        foreach (var goal in tracked)
        {
            var key = TrackedUpgradeStore.Key(goal.Item);
            if (key.Length == 0) continue;

            var record = catalog?.Find(key);
            if (record is null)
            {
                refused.Add(new GearTargetRefusal(key, GearTargetGap.Unreadable));
                continue;
            }

            var before = zones.Count;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var zone in record.DropZones ?? [])
            {
                if (!TradeskillMaterials.IsPlace(zone)) continue;
                if (!seen.Add(ZoneMapFiles.IdentityKey(zone))) continue;
                // The creature list is the catalog's own annotation on THIS zone entry — never
                // a second copy of the zone list, and never merged across zones (trap 4). A
                // page that named the zone and nobody under it lands here with an empty list,
                // which is a row that can be said and cannot be plotted.
                var mobs = record.DropMobs is { } byZone
                           && byZone.TryGetValue(zone, out var named) && named is { Count: > 0 }
                    ? (IReadOnlyList<string>)[.. named.Where(m => m is { Length: > 0 })]
                    : [];
                zones.Add(new GearTargetZone(key, zone.Trim(), mobs));
            }
            if (zones.Count == before)
                refused.Add(new GearTargetRefusal(key, GearTargetGap.NoDropZone));
        }
        return new GearTargetSet(zones, refused);
    }

    /// <summary>
    /// Which of this zone's goals a spawn point answers — <b>the one question the map layer
    /// asks, once per archived point</b>.
    ///
    /// <para><b>The match is <see cref="SpawnCatalog.NameMatches"/> and deliberately NOT its
    /// fuzzy sibling.</b> Fuzzy matching exists to rescue a TIMER from a wiki typo, where the
    /// cost of a miss is a countdown that does not run and the cost of a false hit is a chip
    /// that fires early. Here the costs are not symmetric: a false hit is a target ring on a
    /// dot that does not drop the item, and a player travels to it. The strict fold still buys
    /// everything that matters — the leading article, the backtick the wiki drops, a plural —
    /// because the ledger keys are normalized kill lines and the catalog names are wiki titles,
    /// which is the same pair <see cref="SpawnCatalog"/> was written for. A miss draws no ring,
    /// which is the direction a rule that only ever ADDS a mark is allowed to fail in.</para>
    ///
    /// <para>Every hit is returned, not the first: one point can be the camp for two different
    /// goals' creatures, and <c>FirstOrDefault</c> is the bug DRA-84 D4 took out of the gear
    /// sweep's <c>Who</c>.</para>
    /// </summary>
    /// <param name="here"><see cref="GearTargetSet.Here"/>'s answer for the shown zone. Passing
    /// the whole set would let a point in one zone light up for another zone's creature.</param>
    /// <param name="mobsSeen">The names archived at the point — <c>SpawnPoint.Mobs.Keys</c>.
    /// Taken as names rather than as the point so this stays a fold over strings that any host
    /// can test without building a ledger.</param>
    public static IReadOnlyList<GearTargetHit> AtPoint(
        IReadOnlyList<GearTargetZone>? here, IEnumerable<string>? mobsSeen)
    {
        if (here is not { Count: > 0 } || mobsSeen is null) return [];
        var seen = mobsSeen.Where(m => m is { Length: > 0 }).ToList();
        if (seen.Count == 0) return [];

        var hits = new List<GearTargetHit>();
        foreach (var goal in here)
            foreach (var creature in goal.Creatures)
                if (seen.Any(m => SpawnCatalog.NameMatches(creature, m))
                    && !hits.Any(h => h.Item.Equals(goal.Item, StringComparison.OrdinalIgnoreCase)
                                      && h.Creature.Equals(creature, StringComparison.OrdinalIgnoreCase)))
                    hits.Add(new GearTargetHit(goal.Item, creature));
        return hits;
    }

    /// <summary>
    /// The zones ONE goal drops in, for a sentence about where to go next — the goal's own rows
    /// out of the whole set, in the page's order, <b>excluding the zone the player is already
    /// looking at</b>.
    ///
    /// <para>It is a separate call rather than a filter at the row because the exclusion is the
    /// point: a line reading "it also drops in Befallen" while the map is showing Befallen is a
    /// surface that has not read its own screen.</para>
    /// </summary>
    public static IReadOnlyList<string> Elsewhere(GearTargetSet set, string item, string? shownZone)
    {
        var key = TrackedUpgradeStore.Key(item);
        if (key.Length == 0) return [];
        var hereKey = shownZone is { Length: > 0 }
            ? ZoneMapFiles.IdentityKey(shownZone) : "";
        return [.. set.Zones
            .Where(z => z.Item.Equals(key, StringComparison.OrdinalIgnoreCase))
            .Where(z => hereKey.Length == 0
                        || !ZoneMapFiles.IdentityKey(z.Zone)
                            .Equals(hereKey, StringComparison.OrdinalIgnoreCase))
            .Select(z => z.Zone)];
    }
}
