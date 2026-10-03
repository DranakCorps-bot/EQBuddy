namespace EQBuddy.Core;

/// <summary>One place a guide step can be done, and who the catalog names there.</summary>
/// <param name="Zone">The zone as its SOURCE spells it — the item page's drop zone, or the quest's
/// start zone — never re-spelled. <see cref="WhileHerePlaces.SameZone"/> is the one place two
/// spellings are compared.</param>
/// <param name="Who">The creatures the item's page named in this zone, in the page's order, or
/// the quest giver for a step done at a person. Empty where the source named nobody: the row can
/// still be said, and an unanswered question draws nothing (trap 73).</param>
/// <param name="WhoDrops">True when <paramref name="Who"/> names creatures the step's item DROPS
/// from — something a player kills — and false when it names a person the step is done AT.
/// DRA-42 D3's map mark reads it: a spawn point is where the log saw a KILL, so only a dropper
/// can ever be the creature at a dot, and a quest giver matched to one would be a mark on the
/// place somebody once killed the person you are meant to talk to.</param>
public sealed record WhileHerePlace(string Zone, IReadOnlyList<string> Who, bool WhoDrops = false);

/// <summary>
/// **WHERE A GUIDE STEP CAN BE DONE, FROM STRUCTURED REFERENCES ONLY** — the place half of
/// "while you're here" (DRA-42 D1, requirements §18).
///
/// <para><b>Three sources, and no fourth.</b> An item-shaped step is placed wherever
/// <see cref="ItemCatalog"/> says its item drops (<see cref="ItemCatalog.Record.DropZones"/>, the
/// one producer of that fact, with <see cref="ItemCatalog.Record.DropMobs"/> as the who). A step
/// done at a person — talk to, hand in, return to — is placed at its quest's
/// <see cref="QuestEntry.StartZone"/>. A Plane of Sky checklist piece is placed at its quest's
/// start zone too, because <c>SkyTestSplit</c> WROTE that start zone from the fact that every
/// Sky checklist row is a Plane of Sky row; the caller decides that from the router's own
/// answer (the SkyItem/SkyTurnIn homes), so nothing here re-derives which rows are Sky.</para>
///
/// <para><b>Prose is never read.</b> An authored <c>Where</c> is worded for a reader, and a
/// transcribed step's sentence ("Give the torch to Fajio Knejo in Misty Thicket") names a zone a
/// parser could lift — which is inference wearing the wiki's citation, trap 73 at 486×. So every
/// Epic 1.0 step places NOWHERE, and the surface says so rather than guessing. Nor is a guide's
/// own <c>ZoneNames</c> read: an epic guide names ONE zone for a walk across a dozen of them, and
/// reading it would place fifty steps in Lake Rathetear.</para>
/// </summary>
public static class WhileHerePlaces
{
    /// <summary>The step types whose whole content is "do this at a person". Their place is the
    /// quest's start zone — the harvest wrote a hand-in step's <c>Where</c> FROM that field (1,185
    /// of 1,185 authored NPC steps agree with it, measured 2026-09-30), so reading the field
    /// rather than the step's prose is the same fact without the parse.</summary>
    public static readonly string[] NpcStepTypes = ["TalkToNpc", "TurnIn", "ReturnToNpc"];

    /// <summary>
    /// Is <paramref name="a"/> the same zone as <paramref name="b"/>? <b>Exact title, then
    /// <see cref="ZoneMapFiles.IdentityKey"/>, and NOTHING looser</b> — the
    /// <see cref="ZoneLevels"/>/<see cref="ZoneEras"/>/<see cref="GearTargetSet.Here"/> rule
    /// verbatim.
    ///
    /// <para>Deliberately not <see cref="QuestEntry.TouchesZone"/>, which backs up with
    /// containment either way: that is right for a quest search box and wrong here, because a
    /// player standing in West Commonlands would be told a Commonlands drop is "here". A wrong
    /// "while you're here" is an evening spent camping the wrong zone.</para>
    ///
    /// <para><see cref="ZoneMapFiles.IdentityKey"/> already folds a leading "The" and the
    /// instance tier, so the LOG's "You have entered The Plane of Sky." and the catalog's
    /// "Plane of Sky" answer one way.</para>
    /// </summary>
    public static bool SameZone(string? a, string? b)
    {
        if (a is not { Length: > 0 } || b is not { Length: > 0 }) return false;
        if (a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        var ka = ZoneMapFiles.IdentityKey(a);
        return ka.Length > 0 && ka.Equals(ZoneMapFiles.IdentityKey(b), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every place the catalog says <paramref name="item"/> drops, each with the creatures its page
    /// named there. A non-place (<c>Various Zones</c>, a stray <c>}}</c>) is refused through
    /// <see cref="TradeskillMaterials.IsPlace"/> — the same refusal the Helper and the map targets
    /// make — and one zone spelled twice on a page is one place.
    /// </summary>
    public static IReadOnlyList<WhileHerePlace> DropPlaces(ItemCatalog? catalog, string item)
    {
        if (catalog is null || item is not { Length: > 0 }) return [];
        if (catalog.Find(item) is not { DropZones: { Count: > 0 } zones } record) return [];

        var places = new List<WhileHerePlace>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in zones)
        {
            if (!TradeskillMaterials.IsPlace(zone)) continue;
            if (!seen.Add(ZoneMapFiles.IdentityKey(zone))) continue;
            var who = record.DropMobs is { } byZone
                      && byZone.TryGetValue(zone, out var named) && named is { Count: > 0 }
                ? (IReadOnlyList<string>)[.. named.Where(m => m is { Length: > 0 })]
                : [];
            places.Add(new WhileHerePlace(zone.Trim(), who, WhoDrops: true));
        }
        return places;
    }

    /// <summary>The quest's start zone as a place, with its giver as the who — or nothing where
    /// the page named no start zone (46 catalog quests), or named a non-place.</summary>
    public static IReadOnlyList<WhileHerePlace> StartPlace(QuestEntry quest) =>
        quest.StartZone is { Length: > 0 } zone && TradeskillMaterials.IsPlace(zone)
            ? [new WhileHerePlace(zone.Trim(),
                quest.QuestGiver is { Length: > 0 } giver ? [giver] : [])]
            : [];

    /// <summary>Is this a step done at a person? See <see cref="NpcStepTypes"/>.</summary>
    public static bool IsNpcStep(string objectiveType) =>
        NpcStepTypes.Contains(objectiveType, StringComparer.Ordinal);
}
