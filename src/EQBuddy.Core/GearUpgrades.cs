using System.Runtime.CompilerServices;

namespace EQBuddy.Core;

/// <summary>
/// **WHAT THE PLAYER IS ASKING FOR WHEN THEY PICK "FARM GEAR"** (DRA-71 D6, Fable plan P8;
/// Founder smoke items 4a/4b).
///
/// <para>The Founder smoked D1 and did not ask for a gear engine — he asked for THREE
/// DIFFERENT ONES, and named them: <i>upgrade what I wear</i>, <i>replace gear with better</i>,
/// <i>farm valuable gear to sell</i>. They are not the same question: the first names items,
/// the second names slots, and the third does not look at what you are wearing at all. A single
/// "Farm Gear" answer would have had to pick one of the three silently, which is how a room
/// ends up feeling like it misunderstood you.</para>
///
/// <para><b>Single-select, because they are not filters.</b> The goals above are a
/// multi-select — you can be levelling AND working faction — but you are asking one question
/// about gear at a time, and "upgrade what I wear" and "replace with better" ticked together
/// would produce one merged list whose rows nobody could attribute.</para>
/// </summary>
public enum GearIntent
{
    /// <summary>Find better versions of the items I PICK. The anchor is the picked worn
    /// item.</summary>
    UpgradeWorn,

    /// <summary>Look at my whole kit and tell me what to replace. The anchor is every worn
    /// slot, and no picker is drawn.</summary>
    ReplaceSlot,

    /// <summary>
    /// Farm things worth money. Answered in DRA-71 D7 — <b>and not by this file</b>.
    ///
    /// <para><see cref="GearUpgrades.Sweep"/> refuses it, deliberately and permanently: "what
    /// is worth money" has no worn anchor, and every one of the three properties that keeps
    /// this class off the best-in-slot side of the line rests on there being one. The engine
    /// is <c>Recommendations.FarmToSell</c>, whose anchor is the player's own LOOT priced at
    /// what a vendor actually paid them — two measurements of this player, so the lock is not
    /// approached rather than merely respected.</para>
    /// </summary>
    FarmToSell,
}

/// <summary>Whether an intent has an engine behind it today. Both values are DECISIONS — see
/// <see cref="GearUpgrades.ShapeFor"/>, which answers null for neither, exactly as
/// <c>Recommendations.ShapeFor</c> does one level up.</summary>
public enum GearIntentShape
{
    Answered,

    /// <summary>Decided, and the decision is that a later slice owns it. The strip still
    /// offers it — the Founder asked for three — and picking it says so and points at the room
    /// that answers the question today.</summary>
    Deferred,
}

/// <summary>
/// One thing the character is actually WEARING, as the sweep anchors on it.
/// </summary>
/// <param name="Name">As the dump printed it, "+N" and all — the tier rule reads this.</param>
/// <param name="BaseName">The wiki's title for it, which is the catalog's key.</param>
/// <param name="Slot">The slot it is in, upper-cased the way the stats block prints it. An
/// item with two slots produces one <see cref="WornItem"/> per slot it was found in, because
/// the anchor is the slot as much as the item.</param>
/// <param name="Stats">Its numbers. Never null — an item the catalog cannot describe cannot
/// be compared against anything, so it is not an anchor at all rather than an anchor that
/// loses every comparison.</param>
public sealed record WornItem(string Name, string BaseName, string Slot, ItemStatsBlock Stats);

/// <summary>
/// One catalog item that beats something this character is wearing, and where it comes from.
/// </summary>
/// <param name="Item">The catalog's own name for it.</param>
/// <param name="Slot">The worn slot this is an upgrade IN.</param>
/// <param name="Over">The worn item it beats, as the dump spells it.</param>
/// <param name="GainMetric">The biggest single number that improved ("AC", "HP", "STR"), from
/// <see cref="ItemDominance.Gain"/>. Words are <c>HelperPresentation</c>'s.</param>
/// <param name="GainBy">By how much.</param>
/// <param name="ImprovedMetrics">How many numbers improved at all — the ordering key, and an
/// arithmetic one rather than a taste one. See <see cref="GearUpgrades.Sweep"/>.</param>
/// <param name="Zones">Where the catalog says it drops. May be empty for a quest-only
/// item.</param>
/// <param name="Quests">Which quests hand it out, per the catalog. Only ever REACHED behind
/// the include-quests toggle — see <see cref="GearUpgrades.Sweep"/>.</param>
/// <param name="Mobs">Per zone, the creatures the wiki named. Empty for every zone until the
/// weekly refresh regenerates the catalog with <see cref="ItemCatalog.Record.DropMobs"/> in
/// it — an unanswered question draws nothing (trap 73).</param>
public sealed record GearUpgrade(
    string Item,
    string Slot,
    string Over,
    string GainMetric,
    double GainBy,
    int ImprovedMetrics,
    IReadOnlyList<string> Zones,
    IReadOnlyList<string> Quests,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Mobs)
{
    /// <summary>The creatures the catalog named in one zone, or empty. Empty is a real answer
    /// and never a zero — the wiki page said nothing, or this build's catalog predates the
    /// promoter carrying them.</summary>
    public IReadOnlyList<string> MobsIn(string zone) =>
        Mobs.TryGetValue(zone, out var mobs) ? mobs : [];
}

/// <summary>What one sweep found, and what its cap held back (trap 50).</summary>
public sealed record GearSweep(IReadOnlyList<GearUpgrade> Upgrades, int Withheld)
{
    public static readonly GearSweep Nothing = new([], 0);
}

/// <summary>
/// **THE CATALOG DOMINANCE SWEEP** — the Gear Locker's comparison, pointed at the game instead
/// of at your bags (DRA-71 D6, Fable plan P8).
///
/// <para><b>THE "NEVER BiS" LOCK IS AMENDED KNOWINGLY, AND THE AMENDMENT IS NARROW.</b>
/// <c>GearLocker</c>'s summary has said since #104 that it compares what is in your bags and
/// <i>"never BiS: the Locker compares what's in your bags, not what exists in the game."</i>
/// That lock is UNCHANGED for the Locker, which still only ever compares things you own. What
/// this file adds is a different claim, in a different room: <b>"here is a catalog item that
/// beats what you are wearing, and here is where it drops"</b> — a farmable upgrade, not a
/// best. Three properties keep it that side of the line, and all three are load-bearing:</para>
///
/// <list type="number">
/// <item><b>It only ever answers ABOUT something you wear.</b> Every candidate has an anchor,
/// and the anchor is a worn item. Nothing here ranks the game's items against each other, and
/// an empty slot yields nothing at all — "the best thing for a slot you have nothing in" is
/// precisely the BiS claim the lock forbids, and it is the obvious next feature, so the
/// refusal is written down rather than left to be re-derived.</item>
/// <item><b>Every line it produces is <c>Evidence.Catalog</c></b>, which means HOME-004's
/// estimate label is appended by construction in <c>HelperPresentation.Why</c> — a sentence
/// from a file EQBuddy ships can never read as a measurement of your play.</item>
/// <item><b>It says nothing about what the catalog does not contain.</b> "Nothing beats your
/// helm" would be a BiS claim wearing a negative, so the empty state names the CATALOG as its
/// subject instead of the game.</item>
/// </list>
///
/// <para><b>The metric table is <see cref="ItemDominance"/> and there is one of it.</b> The
/// class-lock filter is the same, the "+N" tier refusal is the same
/// (<see cref="ItemDominance.CanClaimUpgrade"/>), and the stats on both sides come out of the
/// same <see cref="ItemStatsBlock"/> parser — the catalog is built THROUGH the app's own
/// parsers precisely so a catalog record and a live page cannot disagree.</para>
/// </summary>
public static class GearUpgrades
{
    /// <summary>
    /// The decided shape for each intent — <b>trap 34's must-list, one level down from the
    /// goals'</b>.
    ///
    /// <para>Null means nobody decided, which is only reachable by adding a member to
    /// <see cref="GearIntent"/>. A default arm answering <see cref="GearIntentShape.Deferred"/>
    /// would have swallowed exactly that case, and "a later slice owns this" and "nobody
    /// thought about this" would have looked identical on screen.</para>
    /// </summary>
    public static GearIntentShape? ShapeFor(GearIntent intent) => intent switch
    {
        GearIntent.UpgradeWorn => GearIntentShape.Answered,
        GearIntent.ReplaceSlot => GearIntentShape.Answered,
        // DRA-71 D7. Answered by `Recommendations.FarmToSell`, NOT by Sweep below — the
        // shape says an engine exists, not that this file is it.
        GearIntent.FarmToSell => GearIntentShape.Answered,
        _ => null,
    };

    /// <summary>Every intent, in the Founder's own order, read from the enum rather than from
    /// a list beside it (trap 30).</summary>
    public static IReadOnlyList<GearIntent> All => Enum.GetValues<GearIntent>();

    /// <summary>The intent a character who has never chosen one gets: the Founder's own first,
    /// and the one that asks a question ("which items?") rather than assuming the answer.</summary>
    public const GearIntent DefaultIntent = GearIntent.UpgradeWorn;

    /// <summary>
    /// How many upgrades one anchor may contribute before the sweep stops and reports the
    /// rest.
    ///
    /// <para>A popular slot has dozens of dominating items in an 11,000-row catalog, and a
    /// list of dozens is not a recommendation (HOME-002 asks for the opposite). Eight is
    /// comfortably more than the three a zone row will name, so the ZONE join still gets a
    /// real choice to make rather than being handed a pre-narrowed set.</para>
    /// </summary>
    public const int MaxPerAnchor = 8;

    /// <summary>
    /// **THE SWEEP.**
    /// </summary>
    /// <param name="intent">Which question is being asked. <see cref="GearIntent.FarmToSell"/>
    /// answers <see cref="GearSweep.Nothing"/> and <b>always will</b>: it has no worn anchor,
    /// and an anchorless candidate here would be the game's items ranked against each other,
    /// which is the one claim this class exists not to make. Its engine is elsewhere
    /// (<c>Recommendations.FarmToSell</c>); <c>GearUpgradesTests</c> holds the refusal.</param>
    /// <param name="worn">What the character is wearing, one entry per (item, slot).</param>
    /// <param name="picks">Which worn items the player picked. <b>Empty means ALL of them</b> —
    /// filter semantics, the same reading <see cref="AppSettings.UnlockPicks"/> has and the
    /// deliberate opposite of the faction picker's. Read ONLY for
    /// <see cref="GearIntent.UpgradeWorn"/>: "replace with better" is the intent that does not
    /// take a list of items, which is the whole difference between the two.</param>
    /// <param name="catalog">The shipped catalog. Null answers nothing rather than throwing —
    /// a caller with no catalog is a test fixture or a stripped build, not an error.</param>
    /// <param name="myClasses">This character's classes as the item blocks spell them (PAL,
    /// RNG). Empty means "unknown", and an unknown class filters NOTHING — the same
    /// conservative reading <see cref="ItemDominance.Dominates"/> has always had, because
    /// hiding a real upgrade is worse than showing one the player will recognise as not
    /// theirs.</param>
    /// <param name="includeQuests">Whether quest-obtained items may be offered. Off by
    /// default: "farm gear" and "go and do a quest chain" are different evenings, and the
    /// Founder asked for the toggle by name.</param>
    public static GearSweep Sweep(
        GearIntent intent,
        IReadOnlyList<WornItem> worn,
        IReadOnlyList<string> picks,
        ItemCatalog? catalog,
        IReadOnlyList<string> myClasses,
        bool includeQuests)
    {
        // The ANCHOR rule, enforced at the door. "Farm to sell" has an engine (DRA-71 D7) and it
        // is not this one: every candidate here has a worn item behind it, and an intent that
        // cannot supply one is refused rather than served an anchorless list.
        if (intent == GearIntent.FarmToSell) return GearSweep.Nothing;
        if (ShapeFor(intent) != GearIntentShape.Answered) return GearSweep.Nothing;
        if (catalog is null || worn is not { Count: > 0 }) return GearSweep.Nothing;

        var anchors = Anchors(intent, worn, picks);
        if (anchors.Count == 0) return GearSweep.Nothing;

        var found = new List<GearUpgrade>();
        var withheld = 0;
        var index = SlotIndex(catalog);

        foreach (var anchor in anchors)
        {
            if (!index.TryGetValue(anchor.Slot, out var inSlot)) continue;

            var beats = new List<GearUpgrade>();
            foreach (var record in inSlot)
            {
                // The catalog holds the item the player is wearing too, so the same-name
                // refusal inside Dominates is doing real work here rather than guarding a
                // duplicate bag row. The comparison key is the BASE name: a worn "+5" is the
                // base item's stats plus an amount the wiki does not state.
                if (record.Name.Equals(anchor.BaseName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var stats = record.ToStatsBlock();
                // The tier rule, not bare dominance: a worn "+9" whose BASE numbers lose is
                // under-described rather than beaten, and telling somebody to go and farm a
                // replacement for their best item is worse than saying nothing.
                if (!ItemDominance.CanClaimUpgrade(
                        record.Name, stats, anchor.Name, anchor.Stats, myClasses)) continue;

                var zones = record.DropZones?.Where(z => z.Length > 0).ToList() ?? [];
                var quests = record.Quests?.Where(q => q.Length > 0).ToList() ?? [];
                // A quest-only item behind a toggle that is off is not a candidate at all. An
                // item that both drops AND is a quest reward stays in either way — its drop
                // zones are the farmable half, and dropping it for carrying a quest line too
                // would hide a real camp.
                if (zones.Count == 0 && (!includeQuests || quests.Count == 0)) continue;

                var gain = ItemDominance.Gain(stats, anchor.Stats);
                if (gain is not { } g) continue;   // Dominates has already ruled this out

                beats.Add(new GearUpgrade(
                    record.Name, anchor.Slot, anchor.Name, g.Metric, g.By,
                    ItemDominance.MetricPairs(stats, anchor.Stats).Count(p => p.B > p.A),
                    zones,
                    includeQuests ? quests : [],
                    Mobs(record, zones)));
            }

            // **Ordered by how many numbers improved, and that is arithmetic rather than
            // taste.** "Which of two upgrades is better for you" has no answer in this repo —
            // it depends on what the character does, and nothing here knows that. "This one
            // improves six of the numbers and that one improves two" is a count, and a count
            // is something the player can disagree with.
            var ordered = beats
                .OrderByDescending(u => u.ImprovedMetrics)
                .ThenBy(u => u.Item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            found.AddRange(ordered.Take(MaxPerAnchor));
            withheld += Math.Max(0, ordered.Count - MaxPerAnchor);
        }

        return new GearSweep(found, withheld);
    }

    /// <summary>
    /// What the sweep anchors on — <b>the one place the two answered intents actually
    /// differ</b>.
    ///
    /// <para><see cref="GearIntent.UpgradeWorn"/> anchors on the items the player NAMED, which
    /// is why that intent draws a picker; <see cref="GearIntent.ReplaceSlot"/> anchors on every
    /// worn slot and draws none. Written here as one method rather than as two branches at the
    /// call site, so "the picks are read for one intent and not the other" is a fact somebody
    /// can test rather than a shape somebody has to notice.</para>
    ///
    /// <para>An item in two slots is two anchors and one row per slot. A slot holding nothing
    /// is not an anchor — see this class's summary for why that refusal is the whole of what
    /// keeps this off the BiS side of the line.</para>
    /// </summary>
    private static List<WornItem> Anchors(
        GearIntent intent, IReadOnlyList<WornItem> worn, IReadOnlyList<string> picks)
    {
        if (intent != GearIntent.UpgradeWorn || picks is not { Count: > 0 })
            return [.. worn];
        var named = worn
            .Where(w => picks.Contains(w.Name, StringComparer.OrdinalIgnoreCase)
                        || picks.Contains(w.BaseName, StringComparer.OrdinalIgnoreCase))
            .ToList();
        // A pick that names nothing you are wearing narrows nothing — the same per-section
        // rule UnlockPickStore.Narrow keeps, and for the same reason: a stale pick (an item
        // you since replaced) must not silently empty the answer with no control on screen
        // able to explain it.
        return named.Count > 0 ? named : [.. worn];
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Mobs(
        ItemCatalog.Record record, IReadOnlyList<string> zones)
    {
        if (record.DropMobs is not { Count: > 0 } mobs) return EmptyMobs;
        var kept = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in zones)
            if (mobs.TryGetValue(zone, out var named) && named.Count > 0)
                kept[zone] = named;
        return kept.Count > 0 ? kept : EmptyMobs;
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyMobs =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    // ---- the slot index -----------------------------------------------------------------

    /// <summary>
    /// The catalog's wearable records, grouped by the slot they go in.
    ///
    /// <para>Without it the sweep is every worn item × every one of 11,146 records, on a
    /// selection click. With it, a worn item is compared against the few hundred records that
    /// could go in the same slot. Keyed on the catalog INSTANCE through a weak table rather
    /// than on <see cref="ItemCatalog.Default"/>, so a test's small fixture catalog gets its
    /// own index and neither can poison the other; the table holds no strong reference, so a
    /// catalog that goes away takes its index with it.</para>
    /// </summary>
    private static readonly ConditionalWeakTable<ItemCatalog, Dictionary<string, List<ItemCatalog.Record>>>
        Indexes = new();

    private static Dictionary<string, List<ItemCatalog.Record>> SlotIndex(ItemCatalog catalog) =>
        Indexes.GetValue(catalog, static c =>
        {
            var index = new Dictionary<string, List<ItemCatalog.Record>>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in c.All)
            {
                if (record.Slots is not { Count: > 0 }) continue;
                foreach (var slot in record.Slots)
                {
                    var key = slot.ToUpperInvariant();
                    if (!index.TryGetValue(key, out var list)) index[key] = list = [];
                    list.Add(record);
                }
            }
            return index;
        });

    // ---- what the character is wearing ---------------------------------------------------

    /// <summary>
    /// Turn an inventory dump into the sweep's anchors.
    ///
    /// <para>In Core and not in the room, so the phone anchors on the same rule when it
    /// arrives (porting a feature TO a surface is the signal its logic never went through the
    /// shared layer). "Is this worn" is <see cref="InventoryFile.Entry.Worn"/> — one producer,
    /// the same one <c>GearLocker</c> ranks locations with.</para>
    ///
    /// <para>An item the catalog cannot describe is DROPPED rather than carried with null
    /// stats: it cannot win or lose a comparison, and an anchor that silently matches nothing
    /// would read as "no upgrades exist" instead of "EQBuddy has never read about this
    /// item".</para>
    ///
    /// <para><b>ONE ANCHOR PER WORN ROW, and the slot is the DUMP'S</b>
    /// (<see cref="InventoryFile.Entry.WornSlot"/>, DRA-81 Founder smoke). This used to
    /// expand the CATALOG's <c>Slot:</c> line, which is a statement about where an item may
    /// go rather than about where this character is wearing it — so a one-handed weapon in
    /// the primary hand became a PRIMARY anchor and a SECONDARY one, and the picker listed
    /// the same sword twice; and a bow whose wiki page also names PRIMARY was anchored as a
    /// hand weapon, so the RANGE row the Founder was looking for never appeared at all. Two
    /// symptoms, one root: the dump already prints the answer, once, per row.</para>
    ///
    /// <para>The (name, slot) de-duplication survives that change and still earns its keep —
    /// a ring in both FINGER rows is ONE anchor, because both rows normalise to FINGER and
    /// the upgrade for the two of them is the same upgrade.</para>
    /// </summary>
    /// <param name="statsFor">Base name → stats, the room's own resolver (catalog first, wiki
    /// cache behind it) — the same delegate <c>GearLocker.Build</c> takes. The stats still
    /// have to say WEARABLE: a spell scroll sitting in a worn row is not gear, and it is the
    /// stats block rather than the location that knows that.</param>
    public static List<WornItem> WornFrom(
        IEnumerable<InventoryFile.Entry> entries, Func<string, ItemStatsBlock?> statsFor)
    {
        var worn = new List<WornItem>();
        foreach (var entry in entries.Where(e => e.Worn))
        {
            var slot = entry.WornSlot;
            if (slot.Length == 0) continue;   // a location that is only an ordinal
            var baseName = QuestCatalog.BaseItemName(entry.Name);
            if (statsFor(baseName) is not { Wearable: true } stats) continue;
            if (worn.Any(w => w.Slot == slot
                              && w.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
                continue;
            worn.Add(new WornItem(entry.Name, baseName, slot, stats));
        }
        return worn;
    }
}

/// <summary>
/// The Helper's Farm Gear selections, read and written in one place — <b>the
/// <c>HelperGoalStore</c> idiom, third time</b> (DRA-71 D6).
///
/// <para>Writer and reader land in the same slice on purpose (trap 20), and it writes through
/// <see cref="AppSettings"/> itself rather than mutating a dictionary it was handed, which is
/// what keeps all three keys visible to <c>DeadSettingTests</c>' scan.</para>
/// </summary>
public static class GearIntentStore
{
    /// <summary>
    /// Which question this character is asking about gear.
    ///
    /// <para>An absent key is <see cref="GearUpgrades.DefaultIntent"/> and so is an unknown
    /// stored name — an intent removed from the list should stop mattering, not leave the room
    /// with nothing selected. <b>Names rather than ordinals</b>, for
    /// <see cref="AppSettings.HelperGoals"/>' reason: a fourth intent inserted in the middle
    /// would silently re-point every stored choice.</para>
    /// </summary>
    public static GearIntent Intent(AppSettings settings, string characterKey) =>
        settings is not null && !string.IsNullOrEmpty(characterKey)
        && settings.HelperGearIntent.TryGetValue(characterKey, out var name)
        && Enum.TryParse<GearIntent>(name, ignoreCase: true, out var intent)
        && GearUpgrades.ShapeFor(intent) is not null
            ? intent
            : GearUpgrades.DefaultIntent;

    /// <summary>Choose one. Single-select, so this SETS rather than toggles — a strip where
    /// clicking the selected segment cleared it would leave the room with no question to
    /// answer and no visible reason why.</summary>
    public static void Choose(AppSettings settings, string characterKey, GearIntent intent)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)) return;
        if (intent == GearUpgrades.DefaultIntent) settings.HelperGearIntent.Remove(characterKey);
        else settings.HelperGearIntent[characterKey] = intent.ToString();
    }

    /// <summary>Which worn items the player picked, or EMPTY when they never have — which
    /// means ALL of them. Stored as the dump's own spelling, "+N" and all, because that is
    /// what the picker's rows are labelled with and what the anchor is keyed on.</summary>
    public static IReadOnlyList<string> WornPicks(AppSettings settings, string characterKey) =>
        settings is not null && !string.IsNullOrEmpty(characterKey)
        && settings.HelperWornPicks.TryGetValue(characterKey, out var picked)
            ? picked
            : [];

    /// <summary>Turn one worn item on or off. An empty result REMOVES the key: "never picked"
    /// and "unticked the last one" are one state here — both mean every worn item — and two
    /// spellings of one state is a distinction a later reader would eventually act on.</summary>
    public static void ToggleWorn(AppSettings settings, string characterKey, string item)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)
            || string.IsNullOrWhiteSpace(item)) return;
        var picked = WornPicks(settings, characterKey).ToList();
        if (picked.RemoveAll(i => i.Equals(item, StringComparison.OrdinalIgnoreCase)) == 0)
            picked.Add(item);
        if (picked.Count == 0) settings.HelperWornPicks.Remove(characterKey);
        else settings.HelperWornPicks[characterKey] = picked;
    }

    /// <summary>Whether quest-obtained items may be offered. Off unless the character turned
    /// it on — an absent key is the default and the default is the quieter answer.</summary>
    public static bool IncludeQuests(AppSettings settings, string characterKey) =>
        settings is not null && !string.IsNullOrEmpty(characterKey)
        && settings.HelperGearQuests.TryGetValue(characterKey, out var on) && on;

    /// <summary>Flip it. False REMOVES the key rather than storing a false, for
    /// <see cref="ToggleWorn"/>'s reason: "never touched it" and "turned it off" are one
    /// state.</summary>
    public static void ToggleQuests(AppSettings settings, string characterKey)
    {
        if (settings is null || string.IsNullOrEmpty(characterKey)) return;
        if (IncludeQuests(settings, characterKey)) settings.HelperGearQuests.Remove(characterKey);
        else settings.HelperGearQuests[characterKey] = true;
    }
}
