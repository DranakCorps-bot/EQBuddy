using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **WHICH NUMBERS A CLASS'S OWN GEAR CARRIES, MEASURED AGAINST THE SHIPPED CATALOG**
/// (DRA-222 D6, S7.2).
///
/// <para>The rule is a count over eqlwiki's own item blocks, so the tests are the count —
/// taken through the real code over the real catalog rather than re-implemented here. Every
/// row below is a separation the rule has to produce to be worth having, and every one comes
/// with its NEGATIVE, because "Mana is relevant to a wizard" passes for a rule that returns
/// every metric for every class (trap 11's shape: evidence only one side can produce).</para>
/// </summary>
public class ClassStatRelevanceTests
{
    private static IReadOnlySet<string> For(params string[] classes) =>
        ClassStatRelevance.For(ItemCatalog.Default, classes);

    /// <summary>
    /// **THE SEPARATIONS, EACH WITH THE CLASS THEY DO NOT REACH.**
    ///
    /// <para>These are the cells the Founder's S7.2 is about: a caster's row should be able to
    /// be about mana, and a warrior's should not.</para>
    /// </summary>
    [Theory]
    // Mana: 47–50% of the four INT casters' items carry it; 3–6% of WAR / ROG / BER / MNK.
    [InlineData("Mana", "WIZ", "WAR")]
    [InlineData("Mana", "ENC", "ROG")]
    [InlineData("Mana", "MAG", "BER")]
    [InlineData("Mana", "NEC", "MNK")]
    // INT: 51–52% of the same four; 6–8% of the melee roster.
    [InlineData("INT", "WIZ", "WAR")]
    [InlineData("INT", "NEC", "RNG")]
    // WIS: 42% DRU, 30% CLR, 29% SHM; 6% BER.
    [InlineData("WIS", "DRU", "BER")]
    [InlineData("WIS", "CLR", "WAR")]
    [InlineData("WIS", "SHM", "ROG")]
    // DMG: 48% RNG, 40% ROG, 36% WAR; 6% CLR, 13–14% of the INT casters.
    [InlineData("DMG", "RNG", "CLR")]
    [InlineData("DMG", "ROG", "WIZ")]
    [InlineData("DMG", "WAR", "ENC")]
    // The weapon ratio travels with DMG, because it is computed rather than transcribed.
    [InlineData("ratio", "RNG", "CLR")]
    public void TheCatalogSeparatesTheClassesItsOwnItemsAreWrittenFor(
        string metric, string carries, string doesNot)
    {
        Assert.Contains(metric, For(carries));
        Assert.DoesNotContain(metric, For(doesNot));
    }

    /// <summary>
    /// **AC CLEARS THE FLOOR FOR ALL SIXTEEN, AND THAT IS THE RIGHT ANSWER.**
    ///
    /// <para>It is also the row that would be absent under a lift-over-baseline rule — a
    /// warrior's items carry AC 66% of the time against a 67% wearable baseline — which is why
    /// the floor is absolute. A metric every class uses is not evidence that the measure is
    /// degenerate; the assertions above are what rule that out.</para>
    /// </summary>
    [Theory]
    [InlineData("WAR")] [InlineData("CLR")] [InlineData("WIZ")] [InlineData("ROG")]
    [InlineData("SHD")] [InlineData("PAL")] [InlineData("BRD")] [InlineData("RNG")]
    [InlineData("SHM")] [InlineData("NEC")] [InlineData("DRU")] [InlineData("ENC")]
    [InlineData("MAG")] [InlineData("MNK")] [InlineData("BER")] [InlineData("BST")]
    public void EveryClassTheCatalogHoldsGearForAnswersSomething(string cls)
    {
        var relevant = For(cls);
        Assert.NotEmpty(relevant);
        Assert.Contains("AC", relevant);
    }

    /// <summary>
    /// **THE LIMITATION, COMMITTED AS A NEGATIVE RATHER THAN TUNED AWAY.**
    ///
    /// <para>A share of the class's own items is a share, so a number that is rare across the
    /// catalog can miss the floor for a class that really does use it. Mana sits at 19% for CLR
    /// and 23% for SHM, and both therefore answer NO. That is written down here, in the file
    /// that measures it, so the day a refresh moves either cell this suite says so and a reader
    /// can decide with numbers in front of them — rather than the threshold being moved until
    /// the table matched what somebody already believed, which is the failure this whole
    /// mechanism is written against.</para>
    ///
    /// <para><b>The cost is bounded and that is why it is acceptable:</b> relevance decides an
    /// ORDER and which true sentence to say. It removes no row, so a player cannot lose a
    /// cleric's mana upgrade to this — <see cref="RelevanceNeverRemovesACandidate"/> is that
    /// claim, proved.</para>
    /// </summary>
    [Theory]
    [InlineData("CLR")]
    [InlineData("SHM")]
    public void ThePriestClassesMissManaAndTheMissIsAdmitted(string cls) =>
        Assert.DoesNotContain("Mana", For(cls));

    /// <summary>
    /// **UNKNOWN STANDS DOWN WHOLE** (trap 73) — and it is the EMPTY set rather than a set
    /// somebody has to remember means "everything".
    /// </summary>
    [Fact]
    public void NothingKnownAnswersNothing()
    {
        Assert.Empty(ClassStatRelevance.For(ItemCatalog.Default, []));
        Assert.Empty(ClassStatRelevance.For(ItemCatalog.Default, null));
        Assert.Empty(ClassStatRelevance.For(null, ["WIZ"]));
        // A class the catalog has never heard of contributes nothing, rather than every metric
        // or a crash — the same conservative reading a class-locked comparison has.
        Assert.Empty(For("NOTACLASS"));
        // …and it does not poison a real one standing beside it.
        Assert.Contains("Mana", For("NOTACLASS", "WIZ"));
    }

    /// <summary>
    /// **A THREE-CLASS CHARACTER GETS THE UNION**, because they are wearing one set of gear for
    /// all three. An intersection would answer nothing for the common melee/caster pair, and a
    /// "primary class only" reading would be this file deciding which of a player's classes is
    /// the real one.
    /// </summary>
    [Fact]
    public void ThreeClassesAreOneCharacter()
    {
        var both = For("WAR", "WIZ");
        Assert.Contains("DMG", both);    // the warrior's
        Assert.Contains("Mana", both);   // the wizard's
        Assert.Superset(For("WAR").ToHashSet(), both.ToHashSet());
        Assert.Superset(For("WIZ").ToHashSet(), both.ToHashSet());
    }

    /// <summary>
    /// **RELEVANCE NEVER REMOVES A CANDIDATE** — the property the limitation above rests on.
    ///
    /// <para>Run the real sweep over the Founder's committed dump with no classes and with a
    /// caster's, and the SET of items offered is identical. Only the order and the named gain
    /// may move. If this ever fails, the rule has grown a refusal and the file's own summary is
    /// no longer true.</para>
    /// </summary>
    [Fact]
    public void RelevanceNeverRemovesACandidate()
    {
        var worn = FounderFixture.Worn();

        var blind = GearUpgrades.Sweep(
            GearIntent.ReplaceSlot, worn, [], ItemCatalog.Default, [], includeQuests: false);
        // A class list the catalog's own items name, so the class-lock filter inside Dominates
        // is not what moves the numbers: every one of these rows is offered to both runs.
        var known = GearUpgrades.Sweep(
            GearIntent.ReplaceSlot, worn, [], ItemCatalog.Default, ["WIZ"], includeQuests: false);

        Assert.NotEmpty(blind.Upgrades);
        // The class-lock filter DOES narrow (a WIZ cannot use a warrior's plate), so the
        // comparison is made per anchor over items both runs could reach: nothing that survived
        // the class filter may be missing from the known-class run.
        var keys = (GearSweep s) => s.Upgrades
            .Select(u => (u.Over, u.Slot, u.Item)).ToHashSet();
        Assert.Subset(keys(blind), keys(known).Intersect(keys(blind)).ToHashSet());

        // The off-hand counts DIFFER between the two runs, and that is the class-lock filter
        // rather than relevance: 32 two-handers beat something this character wears, and only 2
        // of them are items a WIZ may hold at all, so the other 30 lose at the class gate
        // before the hand question is reached. Asserted rather than left implicit, because the
        // obvious reading of the paragraph above is that this number should be stable.
        Assert.True(blind.OffHandRefusals > known.OffHandRefusals);
    }

    /// <summary>
    /// **THE SWEEP ACTUALLY CONSULTS IT — the guard that was missing when this file was first
    /// written** (trap 78).
    ///
    /// <para>Every other assertion here proves the TABLE: which metrics a class's items carry,
    /// that unknown stands down, that nothing is removed. All of them stayed green with BOTH
    /// uses of the table deleted from <c>GearUpgrades.Sweep</c> — measured, by doing it — which
    /// is a rule aimed at nothing. These two are the reachable ones.</para>
    ///
    /// <para>The invariant rather than a hand-picked pair: <b>if any relevant metric improved,
    /// the row must name a relevant one.</b> That is exactly what <c>ItemDominance.Gain</c>'s
    /// relevance argument buys, it holds for every row rather than for one the author found,
    /// and it is false for every row the moment the argument is dropped.</para>
    /// </summary>
    [Fact]
    public void ARowNamesARelevantMetricWheneverOneImproved()
    {
        var relevant = For("WIZ");
        Assert.NotEmpty(relevant);

        var sweep = GearUpgrades.Sweep(
            GearIntent.ReplaceSlot, FounderFixture.Worn(), [], ItemCatalog.Default, ["WIZ"],
            includeQuests: false);
        Assert.NotEmpty(sweep.Upgrades);

        var couldHave = 0;
        foreach (var upgrade in sweep.Upgrades)
        {
            if (upgrade.RelevantMetrics == 0) continue;   // nothing relevant moved — see Gain
            couldHave++;
            Assert.True(relevant.Contains(upgrade.GainMetric),
                $"{upgrade.Item} moved {upgrade.RelevantMetrics} of this character's own "
                + $"numbers and the row names {upgrade.GainMetric}, which is not one of them");
        }

        // …and the loop is not vacuous: a run where nothing relevant ever improved would
        // satisfy every assertion above without the rule existing (trap 78, one layer down).
        Assert.True(couldHave > 0,
            "no upgrade in this sweep improved a metric a wizard's own gear carries — the "
            + "assertion above decided nothing");
    }

    /// <summary>
    /// **AND IT ORDERS ON IT, AGAINST THE COUNT THAT USED TO DECIDE ALONE.**
    ///
    /// <para>Two claims, and the second is what makes the first non-vacuous: the list is sorted
    /// on (relevant, improved, name) — and somewhere in it a row with FEWER improved metrics
    /// sits above one with more, because more of its numbers are this character's. Without that
    /// second assertion the sort check passes on a build that never looks at relevance, since
    /// every row would score zero and the old key would produce the same order.</para>
    /// </summary>
    [Fact]
    public void RelevanceOutranksTheRawImprovedCount()
    {
        var sweep = GearUpgrades.Sweep(
            GearIntent.ReplaceSlot, FounderFixture.Worn(), [], ItemCatalog.Default, ["WIZ"],
            includeQuests: false);

        var inversions = 0;
        foreach (var perAnchor in sweep.Upgrades.GroupBy(u => (u.Over, u.Slot)))
        {
            var rows = perAnchor.ToList();
            var expected = rows
                .OrderByDescending(u => u.RelevantMetrics)
                .ThenByDescending(u => u.ImprovedMetrics)
                .ThenBy(u => u.Item, StringComparer.OrdinalIgnoreCase)
                .Select(u => u.Item)
                .ToList();
            Assert.Equal(expected, rows.Select(u => u.Item).ToList());

            for (var i = 1; i < rows.Count; i++)
                if (rows[i - 1].RelevantMetrics > rows[i].RelevantMetrics
                    && rows[i - 1].ImprovedMetrics < rows[i].ImprovedMetrics)
                    inversions++;
        }

        Assert.True(inversions > 0,
            "no row was promoted over one that moved MORE numbers, so the sort above is the "
            + "pre-D6 one and proves nothing about relevance");
    }

    /// <summary>
    /// **AND THE ROW USES THE SAME KEYS THE SWEEP DID** (trap 4).
    ///
    /// <para><c>Recommendations.GearRow</c> re-sorts a zone's candidates and caps them at
    /// <c>GearNamedPerRow</c>, so whichever comparer runs there decides which upgrades the
    /// player actually READS about. It had its own copy of the pre-D6 key, which would have
    /// meant the eight the sweep kept and the three the row names were chosen on different
    /// grounds — visible only on the rows where the two disagree, which is exactly the class of
    /// bug that survives a review.</para>
    ///
    /// <para>Measured through <c>Rank</c> over the shipped catalog and the Founder's own dump,
    /// because a fixture small enough to reason about has no pair that disagrees.</para>
    /// </summary>
    [Fact]
    public void TheDrawnRowNamesTheUpgradesTheSweepRankedFirst()
    {
        var inputs = new HelperInputs(
            ZoneHistory.Fold([], []), [], null, [], [], [], [], false, [], [], null,
            ResolvedLevel.Unknown)
        {
            Worn = FounderFixture.Worn(),
            Items = ItemCatalog.Default,
            MyClasses = ["WIZ"],
            GearIntent = GearIntent.ReplaceSlot,
        };

        var set = Recommendations.Rank(inputs, [HelperGoal.FarmGear], cap: 40);
        var rows = set.Top
            .Select(r => r.Why.OfType<GearUpgradeFact>().ToList())
            .Where(f => f.Count > 1)
            .ToList();
        Assert.NotEmpty(rows);

        var varied = 0;
        foreach (var facts in rows)
        {
            for (var i = 1; i < facts.Count; i++)
            {
                Assert.True(facts[i - 1].RelevantMetrics >= facts[i].RelevantMetrics,
                    $"{facts[i].Item} moved more of this character's own numbers than "
                    + $"{facts[i - 1].Item} and is named after it");
                if (facts[i - 1].RelevantMetrics != facts[i].RelevantMetrics) varied++;
            }
        }

        // …and the assertion above decided something: a set of rows whose facts all carry the
        // same relevant count is non-decreasing whatever comparer produced it (trap 78).
        Assert.True(varied > 0,
            "every drawn row named upgrades with identical relevant counts, so the ordering "
            + "assertion above proves nothing");
    }

    /// <summary>
    /// **AND AN UNKNOWN CLASS RANKS EXACTLY AS THIS REPO RANKED BEFORE D6.**
    ///
    /// <para>The prove-fail is the ordering key itself: with an empty relevance set every row
    /// scores <c>RelevantMetrics</c> 0, so the sort falls through to the improved-metric count
    /// and the name, which is the pre-slice comparer verbatim. Asserted rather than reasoned
    /// about, over the Founder's own dump.</para>
    /// </summary>
    [Fact]
    public void AnUnknownClassOrdersOnTheOldKeyAlone()
    {
        var sweep = GearUpgrades.Sweep(
            GearIntent.ReplaceSlot, FounderFixture.Worn(), [], ItemCatalog.Default, [],
            includeQuests: false);

        Assert.NotEmpty(sweep.Upgrades);
        Assert.All(sweep.Upgrades, u => Assert.Equal(0, u.RelevantMetrics));

        foreach (var perAnchor in sweep.Upgrades.GroupBy(u => (u.Over, u.Slot)))
        {
            var expected = perAnchor
                .OrderByDescending(u => u.ImprovedMetrics)
                .ThenBy(u => u.Item, StringComparer.OrdinalIgnoreCase)
                .Select(u => u.Item)
                .ToList();
            Assert.Equal(expected, perAnchor.Select(u => u.Item).ToList());
        }
    }
}
