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
/// **WHAT ONE INVENTORY DUMP'S WORN ROWS CAME TO** — the anchors, and the rows that could not
/// become one (DRA-149 D2, plan P2).
///
/// <para><b>Both halves come out of <see cref="GearUpgrades.WornFrom"/> because one method
/// decided them</b> (trap 4). A caller that counted the anchors and then re-walked the dump to
/// work out what was missing would be a second producer of "is this item readable", free to
/// disagree with the first the day either one learns a rule — and the rule this pair exists to
/// report is precisely one that was learned late.</para>
///
/// <para>An EMPTY <see cref="Unread"/> beside an empty <see cref="Worn"/> is "EQBuddy has
/// never been told what you are wearing". A NON-empty <see cref="Unread"/> beside an empty
/// <see cref="Worn"/> is a different state with a different remedy, and the engine says so
/// rather than asking for a dump it already has
/// (<see cref="GoalGapReason.NothingWornIsReadable"/>).</para>
/// </summary>
/// <param name="Worn">One entry per (item, slot) the sweep can anchor on.</param>
/// <param name="Unread">The worn rows whose item the shipped catalog and the wiki cache both
/// failed to describe, as the DUMP spells them — deduped, in the dump's own order. Never a
/// guess at what they might have been: <see cref="ItemNameAliases"/> is curated and
/// whole-string, so a name nobody has measured stays unread and is said out loud.</param>
public sealed record WornSheet(IReadOnlyList<WornItem> Worn, IReadOnlyList<string> Unread)
{
    /// <summary>No dump at all — distinct from a dump whose every row was unreadable.</summary>
    public static readonly WornSheet Nothing = new([], []);
}

/// <summary>
/// One catalog item that beats something this character is wearing, and where it comes from.
/// </summary>
/// <param name="Item">The catalog's own name for it.</param>
/// <param name="Slot">The worn slot this is an upgrade IN.</param>
/// <param name="Over">The worn item it beats, as the dump spells it.</param>
/// <param name="GainMetric">The biggest single number that improved ("AC", "HP", "STR"), from
/// <see cref="ItemDominance.Gain"/>. Words are <c>HelperPresentation</c>'s.</param>
/// <param name="GainBy">By how much.</param>
/// <param name="ImprovedMetrics">How many numbers improved at all — the SECOND ordering key
/// since DRA-222 D6, and an arithmetic one rather than a taste one. See
/// <see cref="GearUpgrades.Sweep"/>.</param>
/// <param name="RelevantMetrics">How many of those improved numbers this character's classes'
/// own catalog items carry (<see cref="ClassStatRelevance"/>) — the FIRST ordering key since
/// DRA-222 D6 (S7.2).
///
/// <para><b>Zero for every row when the classes are unknown</b>, which is what makes an
/// unknown-class sweep order exactly as it did before D6 rather than differently-but-plausibly.
/// It is still a count and still not a weight: no row can outrank another by more than how many
/// of the class's own numbers it moved, and a low one is never removed.</para></param>
/// <param name="Zones">Where the catalog says it drops. May be empty for a quest-only
/// item.</param>
/// <param name="Quests">Which quests hand it out, per the catalog. Only ever REACHED behind
/// the include-quests toggle — see <see cref="GearUpgrades.Sweep"/>.</param>
/// <param name="Mobs">Per zone, the creatures the wiki named — populated since the DRA-84 D3
/// refresh filled <see cref="ItemCatalog.Record.DropMobs"/>, on 98.2% of the shipped catalog's
/// wearable (item, zone) pairs. Empty for a zone whose page named nobody, which is a real answer
/// and not a zero: an unanswered question draws nothing (trap 73), and since D4 a drop row that
/// cannot answer at all is withheld rather than drawn silent.</param>
public sealed record GearUpgrade(
    string Item,
    string Slot,
    string Over,
    string GainMetric,
    double GainBy,
    int ImprovedMetrics,
    IReadOnlyList<string> Zones,
    IReadOnlyList<string> Quests,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Mobs,
    int RelevantMetrics = 0)
{
    /// <summary>The creatures the catalog named in one zone, or empty. Empty is a real answer
    /// and never a zero — the wiki page said nothing, or this build's catalog predates the
    /// promoter carrying them.</summary>
    public IReadOnlyList<string> MobsIn(string zone) =>
        Mobs.TryGetValue(zone, out var mobs) ? mobs : [];
}

/// <summary>
/// What one sweep found, and the four things it did not pass on (trap 50).
/// </summary>
/// <param name="Upgrades">The candidates, after the per-anchor cap.</param>
/// <param name="Withheld">What <see cref="GearUpgrades.MaxPerAnchor"/> held back.</param>
/// <param name="NoSource">
/// **DOMINATING ITEMS THE CATALOG CANNOT SAY HOW TO GET** (DRA-219, plan S10.1).
///
/// <para>The sweep has always dropped a candidate with no drop zone and no quest, silently —
/// it is not a camp and not a hand-in, so there is nothing to put on a row. That refusal is
/// right and its SILENCE is the Founder's bow one layer up: from the player's side a
/// candidate nobody counted is indistinguishable from a slot with nothing better in it. 1,772
/// of the shipped catalog's 6,844 wearable records carry neither a <c>DropZones</c> nor a
/// <c>Quests</c>, so this is a routine number rather than a rare one.</para>
///
/// <para>Counted per (anchor, item) CANDIDATE, which is what one <see cref="GearUpgrade"/> is,
/// and counted BEFORE the per-anchor cap — the candidate never enters the list the cap trims,
/// so the two numbers cannot double-count one item.</para>
/// </param>
/// <param name="QuestOnly">
/// Candidates whose ONLY source is a quest, while the include-quests toggle is off (S10.1:
/// *"do not restrict recommendations to direct creature drops"*).
///
/// <para><b>A different fact from <paramref name="NoSource"/> and it must not be summed with
/// it</b>: this one has a remedy the player is holding — the toggle they set — and that one
/// has none. 1,284 wearable records are quest-only in the shipped catalog, so a character who
/// leaves the toggle off is routinely being shown a narrower list than EQBuddy found, with
/// nothing on screen saying so.</para>
/// </param>
/// <param name="OffHandRefusals">How many candidates beat the worn item on every number and
/// were removed anyway, because they are two-handed and this character's SECONDARY is occupied
/// (DRA-222 D6).
///
/// <para><b>Its own number beside the three above and never summed into any of them</b>, for
/// the reason they keep theirs apart from each other: <paramref name="Withheld"/> is a CAP,
/// <paramref name="NoSource"/> is a gap in the catalog, <paramref name="QuestOnly"/> has a
/// remedy the player is holding — and this is a RULE about the character's own hands. One
/// merged figure would explain none of the four. Measured on the Founder's committed dump
/// against the shipped catalog: his PRIMARY anchor alone contributes 29.</para></param>
public sealed record GearSweep(
    IReadOnlyList<GearUpgrade> Upgrades, int Withheld, int NoSource = 0, int QuestOnly = 0,
    int OffHandRefusals = 0)
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
/// class-lock filter is the same, the same-name refusal is the same, and the stats on both
/// sides come out of the same <see cref="ItemStatsBlock"/> parser — the catalog is built
/// THROUGH the app's own parsers precisely so a catalog record and a live page cannot
/// disagree.</para>
///
/// <para><b>THE "+N" TIER RULE IS THE ONE THING THE TWO SURFACES NO LONGER SHARE</b> (DRA-149
/// D1, plan P1). The Locker still asks <see cref="ItemDominance.CanClaimUpgrade"/>, where its
/// premise holds: both names come off the same dump and both carry a tier. The sweep asks
/// <see cref="ItemDominance.Dominates"/>, because on this side the premise is false by
/// construction — no catalog name carries a "+N" — and a rule whose answer is fixed before it
/// reads its inputs is not a strict filter but an empty one. <b>The CLAIM narrows to match the
/// weaker question</b>: a row says it is a better BASE item, and the block says once that a
/// "+N" raises the worn one by an amount the wiki does not state. Sharing the table was never
/// the same thing as sharing every rule built on it — what must not fork is the arithmetic,
/// and that has not.</para>
///
/// <para><b>WEAPON-AWARE SINCE DRA-222 D6</b> (S7.3). The table prices every number on both
/// blocks and no SLOT, so a two-handed weapon that wins on all of them "beat" the one-hander in
/// the player's hand and the advice quietly emptied their off-hand. Measured on the Founder's
/// committed dump: his PRIMARY anchor found 64 dominating candidates, 29 of them two-handed,
/// and because <see cref="MaxPerAnchor"/> is 8 the eight rows he could actually see were SEVEN
/// two-handers and one one-hander — for a character wielding a second morning star in
/// SECONDARY. The refusal is <see cref="ItemDominance.Compare"/>'s, it fires only where the
/// dump shows an occupied off-hand, and it is COUNTED
/// (<see cref="GearSweep.OffHandRefusals"/>).</para>
///
/// <para><b>CLASS-AWARE SINCE DRA-222 D6, AND ONLY IN THE ORDER AND THE SENTENCE</b> (S7.2).
/// <see cref="ClassStatRelevance"/> measures what share of a class's own catalog items carry
/// each number; the sweep uses it to pick which improvement a row NAMES and to rank the
/// relevant count above the total. <b>It removes nothing</b> — the dominance question is
/// untouched, so no player can lose a candidate to a measurement they would argue with — and
/// with unknown classes it is the empty set and this sweep behaves exactly as it did before the
/// slice.</para>
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
        // DRA-219. Two DIFFERENT reasons a dominating item never becomes a row, counted apart
        // because their remedies are different: one has none and the other is a toggle the
        // player is looking at. See GearSweep for the shipped numbers behind both.
        var noSource = 0;
        var questOnly = 0;
        // DRA-222 D6. A THIRD reason, and the only one of the three that removes a candidate
        // which beat the worn item on every number - see GearSweep.
        var offHandRefused = 0;
        var index = SlotIndex(catalog);

        // **READ FROM THE WHOLE WORN SHEET, NEVER FROM THE ANCHORS** (DRA-222 D6, S7.3). The
        // anchors may have been narrowed to the player's picks; whether their off-hand is full
        // is a fact about the CHARACTER, and a player who picked only their helm has not
        // emptied their shield hand by doing so.
        //
        // A row the dump files under "Any Slot" does not count — the Founder's own shield is
        // one — which is the conservative direction on purpose: this rule only ever removes
        // offers, so an off-hand nobody has measured leaves it stood down.
        var offHandInUse = worn.Any(w => WeaponSkills.IsOffHand(w.Slot));

        // The metrics this character's classes' own catalog items carry (DRA-222 D6, S7.2).
        // EMPTY for an unknown class, and empty is the pre-D6 ranking exactly.
        var relevant = ClassStatRelevance.For(catalog, myClasses);

        foreach (var anchor in anchors)
        {
            var inSlot = PoolFor(anchor, index);
            if (inSlot.Count == 0) continue;

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
                // **BASE-vs-BASE, and the tier rule is deliberately NOT asked here** (DRA-149
                // D1, plan P1). `CanClaimUpgrade` also demands the candidate's "+N" match or
                // beat the worn item's, which is sound in the Locker — both names come out of
                // the same dump and both carry a tier. Here one side never can: **0 of the
                // catalog's 11,196 names end in "+N"**, so `UpgradeTier(candidate)` is always 0
                // and the comparison was `0 >= 6` for every plussed row a player owns. It is
                // not a strict filter, it is a structurally empty answer — measured over the
                // Founder's committed dump, 19 of 19 gear anchors returned nothing and only
                // tier-0 `Arrow` passed anything at all. He read the true sentence under it and
                // called the feature broken, correctly.
                //
                // What the row may CLAIM narrows to match: not "this beats what you wear" but
                // "this is a better BASE item than yours — at the same +, it wins", with the
                // caveat said once per block rather than templated onto every row (trap 73).
                // No "+N" arithmetic is invented to close the gap, because the wiki does not
                // state what a "+N" is worth.
                //
                // **AND THE OFF-HAND RULE IS ASKED THROUGH THE SAME CALL** (DRA-222 D6, S7.3).
                // `Compare` answers three things rather than two so this loop can tell "it
                // loses on AC" from "it wins and costs you your shield hand" and COUNT the
                // second — a rule that removes offers says so by count (trap 50), and a bool
                // could not have carried the difference.
                var verdict = ItemDominance.Compare(
                    record.Name, stats, anchor.Name, anchor.Stats, myClasses, offHandInUse);
                if (verdict == DominanceVerdict.CostsTheOffHand) { offHandRefused++; continue; }
                if (verdict != DominanceVerdict.Yes) continue;

                var zones = record.DropZones?.Where(z => z.Length > 0).ToList() ?? [];
                var quests = record.Quests?.Where(q => q.Length > 0).ToList() ?? [];
                // A quest-only item behind a toggle that is off is not a candidate at all. An
                // item that both drops AND is a quest reward stays in either way — its drop
                // zones are the farmable half, and dropping it for carrying a quest line too
                // would hide a real camp.
                //
                // **THE TWO REFUSALS ARE NOW COUNTED APART** (DRA-219, S10.1/S19.2). They used
                // to be one `continue` and one silence, and they are not one fact: "no page says
                // where this comes from" has no remedy, and "this comes only from a quest, and
                // quests are switched off" has one the player is looking at. A single number
                // would point at neither.
                if (zones.Count == 0)
                {
                    if (quests.Count == 0) { noSource++; continue; }
                    if (!includeQuests) { questOnly++; continue; }
                }

                // **THE NAMED GAIN IS THE BIGGEST RELEVANT ONE, NOT THE BIGGEST**
                // (DRA-222 D6, S7.2). The metrics are not on one scale, so "+12 HP" beat
                // "+2 WIS" on every cleric's row by arithmetic that has nothing to do with
                // the cleric. An unknown class passes an empty set and gets the old answer.
                var gain = ItemDominance.Gain(stats, anchor.Stats, relevant);
                if (gain is not { } g) continue;   // Dominates has already ruled this out

                beats.Add(new GearUpgrade(
                    record.Name, anchor.Slot, anchor.Name, g.Metric, g.By,
                    ItemDominance.MetricPairs(stats, anchor.Stats).Count(p => p.B > p.A),
                    zones,
                    includeQuests ? quests : [],
                    Mobs(record, zones),
                    ClassStatRelevance.Improved(relevant, stats, anchor.Stats)));
            }

            // **Ordered by how many numbers improved, and that is arithmetic rather than
            // taste.** "Which of two upgrades is better for you" has no answer in this repo —
            // it depends on what the character does, and nothing here knows that. "This one
            // improves six of the numbers and that one improves two" is a count, and a count
            // is something the player can disagree with.
            //
            // **AND SINCE DRA-222 D6 THE COUNT IS TAKEN TWICE, RELEVANT ONES FIRST** (S7.2).
            // That is still a count and still not a taste: `ClassStatRelevance` measures what
            // share of the class's OWN catalog items carry each number, so "this one moved
            // three of the numbers your classes' gear carries and that one moved none" is as
            // checkable as the total beside it.
            //
            // **The two keys are LEXICOGRAPHIC, and that is the whole of the claim.** The
            // relevant count is asked first and the total only breaks its ties, so a row that
            // moved two numbers of which one is a warrior's outranks a row that moved eleven of
            // which none is. That is the intended reading of S7.2 — "how many of YOUR numbers"
            // is the question, and the total is what settles rows the first question cannot
            // separate — but it is worth stating plainly, because "the total stays as the
            // tiebreak" is easy to read as "the total still wins when it is much larger", and
            // it does not. The bound on the damage is that neither key can REMOVE a row: the
            // per-anchor cap keeps the same number of candidates either way, so the worst a
            // wrong relevance set can do is put the right upgrade lower down the same list.
            //
            // With an unknown class every row scores zero on the new key and this list comes
            // out in exactly its pre-D6 order.
            var ordered = beats
                .OrderByDescending(u => u.RelevantMetrics)
                .ThenByDescending(u => u.ImprovedMetrics)
                .ThenBy(u => u.Item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            found.AddRange(ordered.Take(MaxPerAnchor));
            withheld += Math.Max(0, ordered.Count - MaxPerAnchor);
        }

        return new GearSweep(found, withheld, noSource, questOnly, offHandRefused);
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
    /// <para>Without it the sweep is every worn item × every one of 11,196 records, on a
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
                    if (NormalizeSlot(slot) is not { } key) continue;
                    if (!index.TryGetValue(key, out var list)) index[key] = list = [];
                    list.Add(record);
                }
            }
            return index;
        });

    /// <summary>
    /// **ONE SPELLING OF A SLOT, DECIDED AT THE READ SEAM** (DRA-149 D1, plan P3).
    ///
    /// <para>The index is keyed on the catalog's slot vocabulary and looked up with the DUMP's,
    /// and the two do not agree. Measured against the shipped catalog: <b>224 slot entries sit
    /// under keys an inventory dump never produces</b> — <c>FINGER</c> 209 (the dump says
    /// <c>FINGERS</c>, which the catalog itself uses 11 times), <c>SHOULDER</c> 5,
    /// <c>SECONDAY</c> 3 — plus promoter debris: <c>PRIMARY,</c>, <c>BACK,</c>, <c>/</c>,
    /// <c>EMPTY</c>, <c>ORNAMENTATION:</c>. Every one of those is a candidate that could never
    /// be reached, silently, with nothing on screen able to say so.</para>
    ///
    /// <para><b>Null means "this is not a slot", and that is a different answer from a slot
    /// nothing is worn in.</b> The debris keys are refused by name rather than passed through,
    /// so a test can COUNT them against the shipped catalog (<c>GearUpgradesSlotTests</c>) and
    /// notice the day the promoter emits a new one.</para>
    ///
    /// <para><b>It normalizes the LOOKUP, never the anchor.</b> A worn row's slot and label stay
    /// exactly as the dump printed them (DRA-81) — this decides only which pile of catalog
    /// records that row is compared against. Fixing the promoter's own output is a separate
    /// card; no catalog is rebuilt here.</para>
    /// </summary>
    public static string? NormalizeSlot(string? slot)
    {
        if (slot is null) return null;
        var key = slot.Trim().ToUpperInvariant();
        while (key.Length > 0 && (key[^1] == ',' || key[^1] == ':')) key = key[..^1].TrimEnd();
        return key switch
        {
            "" or "/" or "EMPTY" or "ORNAMENTATION" => null,
            "SECONDAY" => "SECONDARY",
            "SHOULDER" => "SHOULDERS",
            "FINGER" => "FINGERS",
            _ => key,
        };
    }

    /// <summary>
    /// The catalog records one anchor is compared against — <b>and the fallback that gives an
    /// "Any Slot" row a pool at all</b> (DRA-149 D1, plan P3).
    ///
    /// <para>The dump writes <c>Any Slot</c> for a row the client does not attribute to a
    /// named slot — the Founder's <c>Shiny Brass Shield +6</c> and <c>Lute +1</c> are both
    /// there. <c>ANY SLOT</c> is a key the catalog never emits, so those anchors matched an
    /// empty pile and answered nothing, forever, for a structural reason no sentence explained.
    /// When the dump's own slot finds no pile, the item's CATALOG <c>Slot:</c> line is asked
    /// instead — the shield's own page says where a shield goes.</para>
    ///
    /// <para><b>The fallback moves the POOL and never the anchor</b> (DRA-81 KEEP): the row is
    /// still labelled and still identified by the dump's slot, because the catalog's line says
    /// where an item MAY go and the dump says where this character actually has it. An item
    /// naming two slots is de-duplicated by name, so a candidate listed in both cannot be
    /// offered twice.</para>
    /// </summary>
    private static List<ItemCatalog.Record> PoolFor(
        WornItem anchor, Dictionary<string, List<ItemCatalog.Record>> index)
    {
        if (NormalizeSlot(anchor.Slot) is { } key && index.TryGetValue(key, out var inSlot))
            return inSlot;

        var pooled = new List<ItemCatalog.Record>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in anchor.Stats.Slots)
            if (NormalizeSlot(slot) is { } fallback && index.TryGetValue(fallback, out var more))
                foreach (var record in more)
                    if (seen.Add(record.Name)) pooled.Add(record);
        return pooled;
    }

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
    /// <para><b>…AND IT IS NAMED, which is the half that was missing</b> (DRA-149 D2, plan
    /// P2). The paragraph above describes the right refusal and the wrong ending: the row
    /// vanished, nothing counted it, and the room drew twenty worn items where the dump has
    /// twenty-one. The Founder's bow is that row — the game spells it <c>Deterioriated</c> and
    /// eqlwiki spells it <c>Deteriorated</c> — and from the outside a silent drop is
    /// indistinguishable from an item with no upgrades. So the drops come back BESIDE the
    /// anchors, from the one method that decided them (trap 4), and every surface can say what
    /// it could not read.</para>
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
    public static WornSheet WornFrom(
        IEnumerable<InventoryFile.Entry> entries, Func<string, ItemStatsBlock?> statsFor)
    {
        var worn = new List<WornItem>();
        var unread = new List<string>();
        foreach (var entry in entries.Where(e => e.Worn))
        {
            var slot = entry.WornSlot;
            if (slot.Length == 0) continue;   // a location that is only an ordinal
            // **THE ONE SEAM, and the alias table is inside it** (DRA-149 D2). `BaseName` has
            // always been documented as "the wiki's title for it, which is the catalog's key"
            // — and it used to be built by a second "+N" stripper that had never heard of a
            // spelling, so the contract was true only where the two agreed. Asking
            // `NormalizeTitle` is what makes it true generally, and it is the same call
            // `ItemCatalog.Find` makes one layer down, so the anchor's key and the lookup's key
            // can no longer disagree — which is what let the sweep's same-name refusal miss the
            // catalog record for the very item being worn.
            var baseName = EqlWikiItemService.NormalizeTitle(entry.Name);
            if (statsFor(baseName) is not { Wearable: true } stats)
            {
                // The dump's OWN spelling, "+N" and all, because that is the string the player
                // is looking at in the game and on the picker — a sentence naming the folded
                // base name would be EQBuddy reporting a miss under a name nobody has seen
                // (trap 35's shape: the right fact in a form the reader cannot use). Deduped,
                // so a pair of unknown rings is one thing to say rather than two.
                if (!unread.Contains(entry.Name, StringComparer.OrdinalIgnoreCase))
                    unread.Add(entry.Name);
                continue;
            }
            if (worn.Any(w => w.Slot == slot
                              && w.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
                continue;
            worn.Add(new WornItem(entry.Name, baseName, slot, stats));
        }
        return new WornSheet(worn, unread);
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
