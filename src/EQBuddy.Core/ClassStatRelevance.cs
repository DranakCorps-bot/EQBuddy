using System.Runtime.CompilerServices;

namespace EQBuddy.Core;

/// <summary>
/// **WHICH NUMBERS THIS CHARACTER'S CLASSES' OWN GEAR ACTUALLY CARRIES** (DRA-222 D6, S7.2).
///
/// <para><b>The claim, exactly.</b> This file does not say what a class NEEDS, what a stat is
/// WORTH, or which of two upgrades is better for you. It says one measured thing: of the
/// catalog records whose own <c>Class:</c> line admits this class, what share carry a given
/// number at all. That is eqlwiki's transcription of what the game's item designers gave the
/// class, counted — and counting is the only kind of answer this repo is allowed to give here
/// (S20; the Helper's gear weight has been "a count and never a taste" since DRA-71 D6).</para>
///
/// <para><b>What it is NOT, and the refusal is the design.</b> There is no weight, no
/// coefficient and no per-stat score — a relevance set is a SET. Nothing here can make one
/// candidate outrank another by more than "it improved more of the numbers your classes' gear
/// carries", and <b>nothing here removes a row</b>. That last property is load-bearing: the
/// off-hand rule beside it (<see cref="WeaponSkills"/>) refuses offers and therefore reports
/// its count out loud, and this one never has to, because a player cannot lose a candidate to
/// a measurement they might disagree with.</para>
///
/// <para><b>Measured over the shipped catalog</b> (6,844 wearable records, 4,880 of them
/// class-restricted). The separations the floor produces are the ones the Founder's S7.2 asked
/// for, and they are large rather than marginal — <c>Mana</c> is carried by 50% of WIZ items,
/// 49% of ENC and MAG, 47% of NEC, and by 4% of WAR and ROG items; <c>INT</c> by 51–52% of the
/// four INT casters and 6–8% of the melee classes; <c>WIS</c> by 42% of DRU, 30% of CLR and
/// 29% of SHM items and 6% of BER; <c>DMG</c> by 48% of RNG and 40% of ROG items and 6% of
/// CLR. <c>AC</c> clears the floor for all sixteen, which is the right answer and not a
/// degenerate one.</para>
///
/// <para><b>The limitation, written down rather than tuned away.</b> The floor is a share of
/// the class's own items, so a number that is RARE across the whole catalog can miss it for a
/// class that really does use it: <c>Mana</c> sits at 19% for CLR and 23% for SHM and is
/// therefore NOT relevant to either, though a cleric plainly cares about mana. Both are pinned
/// as committed negatives in <c>ClassStatRelevanceTests</c>, so the day a refresh moves them
/// the suite says so. The threshold was NOT moved until those two cells looked right: fitting
/// the rule to what the author already believed is the failure mode this whole file is written
/// against, and the cost of the miss is bounded by the paragraph above — a worse ORDER in an
/// edge case, never a wrong sentence and never a hidden row.</para>
///
/// <para><b>Unknown stands down whole</b> (trap 73). No classes, no catalog, or a class the
/// catalog holds too few items for, and the answer is the EMPTY set — which every reader
/// treats as "rank exactly as this repo ranked before D6", not as "nothing is relevant".
/// <c>GearUpgradesTests</c> proves that equivalence rather than asserting it.</para>
/// </summary>
public static class ClassStatRelevance
{
    /// <summary>
    /// What share of a class's own items must carry a number before this file will call it one
    /// of that class's numbers.
    ///
    /// <para>One threshold and no second arm. A lift-over-baseline rule was measured beside it
    /// and rejected: the catalog's wearable baseline is itself three-quarters melee-usable, so
    /// "carried more often than gear at large" makes <c>AC</c> irrelevant to a WARRIOR (66%
    /// against a 67% baseline) while admitting <c>CHA</c> for an ENCHANTER — one cell better
    /// and one cell absurd, for a second knob.</para>
    /// </summary>
    public const double RelevanceFloor = 0.25;

    /// <summary>
    /// How many class-restricted wearable records a class needs before a share of them means
    /// anything.
    ///
    /// <para>The smallest class in the shipped catalog is BST at 124 records and the next is
    /// BER at 155, so 100 admits all sixteen today — it is a floor against a FUTURE class
    /// arriving with eleven items and one of them happening to carry Mana, not a filter
    /// anything currently fails. A class under it contributes nothing rather than a noisy set,
    /// and a character whose every class is under it gets the empty set, which stands the whole
    /// rule down.</para>
    /// </summary>
    public const int MinClassRecords = 100;

    /// <summary>
    /// The metrics this character's classes' own gear carries, in
    /// <see cref="ItemDominance.MetricPairs"/>' own spelling.
    ///
    /// <para><b>The UNION across the character's classes</b>, because a Legends character holds
    /// up to three at once (<see cref="CharacterClasses.Max"/>) and is wearing one set of gear
    /// for all of them. An intersection would answer the empty set for the common case of a
    /// melee/caster pair, and a "primary class only" reading would be this file deciding which
    /// of a player's three classes is the real one — which is a claim about the character, not
    /// about the catalog, and not this file's to make.</para>
    ///
    /// <para>Memoized per (catalog, class-set) through a weak table keyed on the catalog
    /// INSTANCE, which is <c>GearUpgrades.SlotIndex</c>'s arrangement and its reason: a test's
    /// two-record fixture gets its own survey and neither can poison the other, and a catalog
    /// that goes away takes its survey with it.</para>
    /// </summary>
    public static IReadOnlySet<string> For(ItemCatalog? catalog, IReadOnlyList<string>? myClasses)
    {
        if (catalog is null || myClasses is not { Count: > 0 }) return Empty;

        var survey = Surveys.GetValue(catalog, Survey);
        var union = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cls in myClasses)
            if (cls is { Length: > 0 } && survey.TryGetValue(cls.Trim(), out var metrics))
                union.UnionWith(metrics);
        return union.Count > 0 ? union : Empty;
    }

    /// <summary>Whether one metric is one of this set's — a named question rather than a
    /// <c>Contains</c> at four call sites, so the empty-set reading stays in one place.</summary>
    public static bool IsRelevant(IReadOnlySet<string> relevant, string metric) =>
        relevant.Count > 0 && relevant.Contains(metric);

    /// <summary>How many of the metrics that improved are ones this character's classes' gear
    /// carries. Zero when the set is empty, which is what makes an unknown-class ranking
    /// identical to the pre-D6 one.</summary>
    public static int Improved(
        IReadOnlySet<string> relevant, ItemStatsBlock candidate, ItemStatsBlock worn)
    {
        if (relevant.Count == 0) return 0;
        var count = 0;
        foreach (var (metric, b, a) in ItemDominance.MetricPairs(candidate, worn))
            if (b > a && relevant.Contains(metric)) count++;
        return count;
    }

    private static readonly IReadOnlySet<string> Empty =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly ConditionalWeakTable<
        ItemCatalog, Dictionary<string, HashSet<string>>> Surveys = new();

    /// <summary>
    /// **THE SURVEY** — one pass over the catalog, per class, per metric.
    ///
    /// <para>A record counts for a class when its own <c>Class:</c> line NAMES that class. An
    /// unrestricted item (the block says <c>Class: ALL</c>, which
    /// <see cref="ItemStatsBlock.Parse"/> records as an empty list) counts for nobody here —
    /// it says nothing about any one class, and letting all 1,964 of them into all sixteen
    /// denominators would drag every class's shares towards one shared average, which is the
    /// opposite of what is being measured.</para>
    ///
    /// <para><c>ratio</c> is not surveyed and takes <c>DMG</c>'s answer, because it is a
    /// computed property (<see cref="ItemStatsBlock.Ratio"/>, damage per point of delay) rather
    /// than a field a page carries — surveying it would count exactly the records that carry
    /// both <c>Dmg</c> and <c>Delay</c>, which is a measurement of how completely the wiki
    /// transcribes weapon blocks and not of any class. One producer, stated once (trap 4).</para>
    /// </summary>
    private static Dictionary<string, HashSet<string>> Survey(ItemCatalog catalog)
    {
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var carrying = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in catalog.All)
        {
            if (record.Slots is not { Count: > 0 }) continue;       // not gear
            if (record.Classes is not { Count: > 0 } classes) continue;   // says nothing about a class

            var stats = record.ToStatsBlock();
            foreach (var raw in classes)
            {
                // The promoter emits a handful of trailing-comma spellings ("BRD,") and a
                // literal "NONE"; the trim folds the first and the record floor below quietly
                // drops the second. Neither is admitted as a class in its own right — that is
                // `QuestClassFilter`'s vocabulary, and a sixteenth-and-a-half class invented
                // here could never reach a surface that could say so.
                var cls = raw.Trim().Trim(',').Trim();
                if (cls.Length == 0) continue;
                totals[cls] = totals.GetValueOrDefault(cls) + 1;
                if (!carrying.TryGetValue(cls, out var metrics))
                    carrying[cls] = metrics = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var metric in Carried(stats))
                    metrics[metric] = metrics.GetValueOrDefault(metric) + 1;
            }
        }

        var survey = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (cls, total) in totals)
        {
            if (total < MinClassRecords) continue;
            var relevant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (metric, count) in carrying.GetValueOrDefault(cls) ?? [])
                if ((double)count / total >= RelevanceFloor) relevant.Add(metric);
            // `ratio` travels with DMG — see the method note.
            if (relevant.Contains("DMG")) relevant.Add("ratio");
            if (relevant.Count > 0) survey[cls] = relevant;
        }
        return survey;
    }

    /// <summary>
    /// Which of <see cref="ItemDominance.MetricPairs"/>' names one record carries a POSITIVE
    /// value for.
    ///
    /// <para>Positive and not merely present: a stats block records a printed <c>STR: -5</c> as
    /// a negative, and counting a penalty as evidence that the class's gear carries the stat
    /// would measure the opposite of the question. <c>ratio</c> is absent by construction — see
    /// the survey's note.</para>
    /// </summary>
    private static IEnumerable<string> Carried(ItemStatsBlock stats)
    {
        if (stats.Ac > 0) yield return "AC";
        if (stats.Hp > 0) yield return "HP";
        if (stats.Mana > 0) yield return "Mana";
        if (stats.Dmg > 0) yield return "DMG";
        foreach (var (key, value) in stats.Attributes)
            if (value > 0) yield return key;
    }
}
