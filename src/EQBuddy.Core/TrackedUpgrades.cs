namespace EQBuddy.Core;

/// <summary>
/// **ONE UPGRADE THIS CHARACTER HAS DECIDED TO GO AND GET** (DRA-216 D4, S12; plan §3 Q3).
///
/// <para><b>It is its own domain object, and the plan says why in one sentence: a tracked goal
/// outlives the sweep.</b> <see cref="GearUpgrade"/> is a record <see cref="GearUpgrades.Sweep"/>
/// builds fresh out of the CURRENT worn gear and the CURRENT catalog, every pass — so hanging
/// durable state on one would tie a decision the player made on Tuesday to whether Saturday's
/// dump still produces the row that offered it. Replace the anchor, untick the worn pick, take a
/// catalog refresh, or let the band gate refuse the zone, and the offer is gone; the DECISION is
/// not, and a goal that quietly disappeared because the list under it moved is the silent no-op
/// this repo refuses everywhere else.</para>
///
/// <para><b>BASE ITEMS ONLY, and that is Helm's park rather than a simplification</b>
/// (<c>HELM.md</c> 2026-09-19: S8 `+0..+10` and S9 exaltations are PARKED for the whole DRA-216
/// program, together with the S12.3 acceptance that depends on them). <see cref="Item"/> is the
/// name folded through <see cref="EqlWikiItemService.NormalizeTitle"/> — the ONE seam — so a goal
/// is a goal about the base item eqlwiki has a page for. Nothing here holds, compares or invents
/// a "+N", and no exaltation is claimed compatible with anything: the repo holds no enhanced-item
/// stat anywhere (<see cref="ItemDominance"/>'s committed measurement: <b>0 of 11,196</b> catalog
/// names carry a tier), so a tracked "+5" would be a goal naming an object EQBuddy cannot
/// describe.</para>
///
/// <para><b>THERE IS NO COMPLETION CONDITION IN THIS SLICE, DELIBERATELY.</b> S12.3 asks when a
/// tracked upgrade is DONE, and every answer the requirements give runs through the crossover
/// tier ("keep your +5 until this hits +6") that S8 was parked for. So a goal ends when the
/// player says it does — <see cref="TrackedUpgradeStore.Untrack"/> — and EQBuddy makes no claim
/// about whether the thing in your bags beats the thing on your character. Un-parking S8/S9 is a
/// new Helm ruling, not an Executor judgement, and the shape of this record leaves room for one
/// without moving what is stored.</para>
///
/// <para><b>What is NOT here is as decided as what is.</b> No gain figure and no drop zone is
/// stored: <see cref="ItemCatalog"/> is the one producer of where an item comes from and
/// <see cref="ItemDominance"/> the one producer of what it is worth (trap 4), and a number frozen
/// at track time is a claim that goes quietly wrong the first time either moves. The map and
/// spawn join that turns <see cref="Item"/> into a place is D5's, over this object.</para>
/// </summary>
/// <param name="Item">The catalog's own name for it, folded to the base title. This is the
/// IDENTITY — one goal per item, not one per slot: a player chasing a two-hand blade is chasing
/// it once, and two rows differing only by which hand it was offered for is a list nobody
/// asked for.</param>
/// <param name="Slot">The worn slot the offer was made IN, as the dump spelled it. Context for
/// the sentence rather than part of the identity.</param>
/// <param name="Over">The worn item the offer beat, as the DUMP spells it — "+N" and all,
/// because that is the string on the player's own character sheet. It is a transcription of what
/// they were wearing when they decided, never an input to arithmetic: the "+N" is exactly the
/// thing this repo states no value for.</param>
/// <param name="TrackedAt">When they decided, on the LOCAL wall clock — a player action, so the
/// same clock <c>QuestLedgerStore.StatedLevelAt</c> keeps and not the log's stamp. It orders the
/// list and it is in the sentence, because "I have been chasing this since Tuesday" is a thing
/// only the stamp can answer.</param>
public sealed record TrackedUpgrade(string Item, string Slot, string Over, DateTime TrackedAt);

/// <summary>
/// **THE TRACKED UPGRADES, READ AND WRITTEN IN ONE PLACE** — the <see cref="GearIntentStore"/>
/// idiom, fourth time (DRA-216 D4, S12).
///
/// <para>Writer and reader land in the same slice (trap 20), and it writes through
/// <see cref="AppSettings"/> itself rather than mutating a dictionary it was handed, which is
/// what keeps the key visible to <c>DeadSettingTests</c>' scan.</para>
///
/// <para><b>THE ONLY DOOR IN IS A <see cref="GearUpgradeFact"/> THE ENGINE PRODUCED</b>, and
/// that signature is the design rather than convenience. <see cref="GearUpgrades"/>' summary
/// keeps this room off the best-in-slot side of the line with one property above all others —
/// <i>every candidate has a WORN ANCHOR</i> — and a store that accepted a bare item name would be
/// a way to name a game item with nothing on the character behind it, which is precisely the BiS
/// claim the lock forbids. A fact carries its anchor, so it cannot be built out of nothing.</para>
///
/// <para><b>Per character, like every other selection in this room.</b> One install holds a
/// level-8 alt whose upgrades are quest rewards and a main farming raid zones, and a shared list
/// would put one character's goals in the other's room.</para>
/// </summary>
public static class TrackedUpgradeStore
{
    /// <summary>
    /// The identity of a goal — <b>the ONE seam, asked here so nothing downstream has to
    /// remember to</b>.
    ///
    /// <para><see cref="EqlWikiItemService.NormalizeTitle"/> folds the "+N" off and then consults
    /// the curated alias table, which is the same call <see cref="ItemCatalog.Find"/>,
    /// <c>GearUpgrades.WornFrom</c> and the player's own wiki door already make. So tracking
    /// <i>Crushbone Belt +5</i> and tracking <i>Crushbone Belt</i> is tracking one thing, and the
    /// key a later slice joins to the catalog with cannot disagree with the key the catalog was
    /// looked up under.</para>
    ///
    /// <para>An unknown name comes back UNCHANGED rather than guessed at — the table is
    /// whole-string and curated, never fuzzy (see <see cref="ItemNameAliases"/>) — so a goal is
    /// stored under the name it was offered under and is reported, not silently re-pointed at a
    /// near match describing a different item.</para>
    /// </summary>
    public static string Key(string? item) =>
        item is { Length: > 0 } ? EqlWikiItemService.NormalizeTitle(item).Trim() : "";

    /// <summary>
    /// What this character is going after, newest first.
    ///
    /// <para><b>The order is decided here rather than left to the store's insertion order</b>,
    /// because the two are only the same until something removes a row. Newest first because the
    /// row the player just created is the one they are looking at the screen for; the item name
    /// breaks a tie, so two goals tracked in the same tick cannot swap places between two paints
    /// and make a surface look like it re-ranked something.</para>
    ///
    /// <para>Rows whose stored item is empty are dropped: a goal with no subject is not a goal,
    /// and a hand-edited profile is the one way one could arrive.</para>
    /// </summary>
    public static IReadOnlyList<TrackedUpgrade> For(AppSettings? settings, string characterKey)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)) return [];
        if (!settings.TrackedUpgrades.TryGetValue(characterKey, out var rows)
            || rows is not { Count: > 0 }) return [];
        return [.. rows
            .Where(r => r is { Item.Length: > 0 })
            .OrderByDescending(r => r.TrackedAt)
            .ThenBy(r => r.Item, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Whether this item is already a goal. Asked by the rows that offer to track it —
    /// a control that said "Track" over something already tracked would be the surface
    /// disagreeing with the store it writes.</summary>
    public static bool IsTracked(AppSettings? settings, string characterKey, string? item)
    {
        var key = Key(item);
        return key.Length > 0
               && For(settings, characterKey)
                   .Any(r => r.Item.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Start going after one — <b>and re-tracking something is a NO-OP that keeps the first
    /// decision</b>.
    ///
    /// <para>The same item can be offered again from a different row (a second worn anchor, a
    /// later pass), and the honest reading of a second click is "yes, I still want that" rather
    /// than "start the clock again". Overwriting would move <see cref="TrackedUpgrade.TrackedAt"/>
    /// to now, which is the one field nothing else can recover.</para>
    /// </summary>
    /// <param name="at">The LOCAL wall clock, passed in rather than read here so the decision's
    /// stamp is the caller's to supply and a test can state it.</param>
    /// <returns>True when a goal was added.</returns>
    public static bool Track(
        AppSettings? settings, string characterKey, GearUpgradeFact? offer, DateTime at)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey) || offer is null) return false;
        var key = Key(offer.Item);
        if (key.Length == 0) return false;
        if (IsTracked(settings, characterKey, key)) return false;

        var rows = new List<TrackedUpgrade>();
        if (settings.TrackedUpgrades.TryGetValue(characterKey, out var existing) && existing is not null)
            rows.AddRange(existing.Where(r => r is { Item.Length: > 0 }));
        rows.Add(new TrackedUpgrade(key, offer.Slot ?? "", offer.Over ?? "", at));
        settings.TrackedUpgrades[characterKey] = rows;
        return true;
    }

    /// <summary>
    /// Stop going after one. <b>It is the only way a goal ends</b> — see
    /// <see cref="TrackedUpgrade"/> for why there is no completion condition in this slice.
    ///
    /// <para>An empty result REMOVES the key rather than storing an empty list, the idiom every
    /// other per-character selection here keeps: "never tracked anything" and "untracked the last
    /// one" are one state, and two spellings of one state is a distinction a later reader
    /// eventually acts on.</para>
    /// </summary>
    /// <returns>True when a goal was removed.</returns>
    public static bool Untrack(AppSettings? settings, string characterKey, string? item)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)) return false;
        var key = Key(item);
        if (key.Length == 0) return false;
        if (!settings.TrackedUpgrades.TryGetValue(characterKey, out var existing)
            || existing is null) return false;

        var rows = existing.Where(r => r is { Item.Length: > 0 }).ToList();
        if (rows.RemoveAll(r => r.Item.Equals(key, StringComparison.OrdinalIgnoreCase)) == 0)
            return false;
        if (rows.Count == 0) settings.TrackedUpgrades.Remove(characterKey);
        else settings.TrackedUpgrades[characterKey] = rows;
        return true;
    }

    /// <summary>One control, both directions — the row's button is a toggle, so the decision
    /// about which way it goes lives with the store rather than in each host's click handler
    /// (trap 4: the desktop and any later surface would otherwise each own half of it).</summary>
    /// <returns>True when the item is tracked AFTER the call.</returns>
    public static bool Toggle(
        AppSettings? settings, string characterKey, GearUpgradeFact? offer, DateTime at)
    {
        if (offer is null) return false;
        if (IsTracked(settings, characterKey, offer.Item))
        {
            Untrack(settings, characterKey, offer.Item);
            return false;
        }
        return Track(settings, characterKey, offer, at);
    }
}
