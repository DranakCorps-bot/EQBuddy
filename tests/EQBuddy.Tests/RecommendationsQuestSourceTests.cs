using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE QUEST ACQUISITION PATH** (DRA-219, requirements S10/S11; acceptance S25 AC 1–8).
///
/// <para><c>RecommendationsGearTests</c> owns the DROP path — the who rule, the band gate, the
/// creature clause. Everything in this file is about the other half of S10.1, which until this
/// slice could say a quest's NAME and nothing else: no giver, no start zone, no level, no
/// components, and no way to get from the row to any of them. That is S11.2's *"never force the
/// player to reverse-engineer the source"*, failing quietly on 1,352 real offers.</para>
///
/// <para><b>Two rules, and they are siblings rather than alternatives.</b>
/// <c>Recommendations.WhoRule</c> withholds a drop offer nothing can name a creature for;
/// <c>QuestSourceRule</c> withholds a quest offer EQBuddy's own quest list cannot describe. Both
/// say the same thing about the two halves of one requirement: an offer that cannot say how to
/// pursue it is not an offer.</para>
/// </summary>
public class RecommendationsQuestSourceTests
{
    // ---- fixtures ----------------------------------------------------------------------

    private static ItemCatalog.Record Record(
        string name, string slot, int ac, string[]? zones = null, string[]? quests = null) =>
        new()
        {
            Name = name, StatsText = $"Slot: {slot}\nAC: {ac}", Slots = [slot], Ac = ac,
            DropZones = zones?.ToList(), Quests = quests?.ToList(),
            DropMobs = zones is not { Length: > 0 }
                ? null
                : zones.Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(z => z, z => new List<string> { $"a {z.ToLowerInvariant()} dweller" },
                        StringComparer.OrdinalIgnoreCase),
        };

    private static WornItem Worn(string name, string slot, int ac) =>
        new(name, name, slot, ItemStatsBlock.Parse([$"Slot: {slot}", $"AC: {ac}"]));

    /// <summary>One quest entry, with whichever of the four questions a test is about. The
    /// defaults answer NONE of them on purpose — that is the state the withhold rule exists
    /// for, and a fixture that quietly answered one would hide it.</summary>
    private static QuestEntry Quest(
        string name, string giver = "", string zone = "", int minLevel = 0,
        string[]? items = null, string era = "") =>
        new()
        {
            Name = name, QuestGiver = giver, StartZone = zone, MinLevel = minLevel, Era = era,
            Items = [.. (items ?? []).Select(i => new QuestItemNeed { Name = i })],
        };

    private static QuestCatalog Catalog(params QuestEntry[] quests) =>
        new() { Quests = [.. quests] };

    /// <summary>
    /// The default carries NO quest catalog, which stands the quest-source rule down whole —
    /// the band gate's own voice (trap 73), and what keeps every test written before this slice
    /// testing what it was written to test.
    /// </summary>
    private static HelperInputs Gear(
        IReadOnlyList<WornItem> worn,
        ItemCatalog? catalog,
        QuestCatalog? quests = null,
        bool includeQuests = true,
        IReadOnlyList<MobSummary>? pool = null) =>
        new([], pool ?? [], null, [], [], [], [], false, [], [], quests, ResolvedLevel.Unknown)
        {
            Worn = worn, Items = catalog, GearIntent = GearIntent.UpgradeWorn,
            IncludeQuests = includeQuests,
        };

    private static RecommendationSet Rank(HelperInputs inputs) =>
        Recommendations.Rank(inputs, [HelperGoal.FarmGear]);

    // ---- S25 AC 1-6: the six questions, on a quest row ---------------------------------

    /// <summary>
    /// **THE WHOLE POINT OF THE SLICE**: a quest row says who starts it, where they stand, from
    /// what level, and what it takes — beside the item line, which already said WHAT and WHY.
    /// </summary>
    [Fact]
    public void AQuestRowAnswersWhoWhereWhenAndHow()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["A Humble Errand"])]),
            Catalog(Quest("A Humble Errand", "Herald Ganelorn", "Qeynos", 12,
                ["Rat Whisker", "Bent Copper Ring"]))));

        var row = Assert.Single(set.Top);
        Assert.Equal(RecommendationKind.Quest, row.Kind);

        var source = Assert.Single(row.Why.OfType<QuestSourceFact>());
        Assert.Equal("A Humble Errand", source.Quest);
        Assert.Equal("Herald Ganelorn", source.Giver);       // WHO
        Assert.Equal("Qeynos", source.StartZone);            // WHERE
        Assert.Equal(12, source.MinLevel);                   // WHEN
        Assert.Equal(2, source.Components);                  // HOW
        Assert.Equal(["Rat Whisker", "Bent Copper Ring"], source.Items);
        Assert.Equal(Evidence.Catalog, source.Evidence);

        // WHAT and WHY are the item line's, unchanged and still beside it.
        var upgrade = Assert.Single(row.Why.OfType<GearUpgradeFact>());
        Assert.Equal("Blessed Helm", upgrade.Item);
        Assert.Equal("Rusty Helm", upgrade.Over);

        var sentence = HelperPresentation.Why(source);
        Assert.Contains("Herald Ganelorn", sentence);
        Assert.Contains("Qeynos", sentence);
        Assert.Contains("from level 12", sentence);
        Assert.Contains("2 turn-in items", sentence);
        Assert.Contains("Rat Whisker and Bent Copper Ring", sentence);
        Assert.EndsWith(HelperPresentation.CatalogLabel, sentence);
    }

    /// <summary>
    /// **THE SOURCE LINE LEADS THE ROW.** The item lines under it say what the reward is worth;
    /// this one says how to go and get it, and the <c>WhyCap</c> trims the TAIL — so a quest
    /// handing out three upgrades must not be able to push its own directions off the row.
    /// </summary>
    [Fact]
    public void TheQuestSourceLineComesFirstOnTheRow()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4), Worn("Rusty Bracer", "ARMS", 2)],
            new ItemCatalog([
                Record("Blessed Helm", "HEAD", 12, quests: ["A Humble Errand"]),
                Record("Blessed Bracer", "ARMS", 9, quests: ["A Humble Errand"]),
            ]),
            Catalog(Quest("A Humble Errand", "Herald Ganelorn", "Qeynos"))));

        var row = Assert.Single(set.Top);
        Assert.IsType<QuestSourceFact>(row.Why[0]);
        Assert.Equal(2, row.Why.OfType<GearUpgradeFact>().Count());
    }

    /// <summary>
    /// **AN UNANSWERED QUESTION DRAWS NOTHING** (trap 73 — the schema is not a licence to answer
    /// every question). The catalog answers three of four here, and the sentence carries exactly
    /// three clauses: no "from an unknown NPC", no "from level 1".
    /// </summary>
    [Fact]
    public void AQuestionTheQuestCatalogDoesNotAnswerIsLeftEmpty()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["A Quiet Word"])]),
            Catalog(Quest("A Quiet Word", giver: "Sentry Alechin", zone: "Qeynos"))));

        var source = Assert.Single(Assert.Single(set.Top).Why.OfType<QuestSourceFact>());
        Assert.Equal(0, source.MinLevel);
        Assert.Equal(0, source.Components);
        Assert.Empty(source.Items);

        var sentence = HelperPresentation.Why(source);
        Assert.Contains("Sentry Alechin", sentence);
        Assert.DoesNotContain("level", sentence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("turn-in", sentence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unknown", sentence, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A quest asking for more components than one row will name counts the whole list
    /// and says the rest are on its page (trap 50: a surviving cap says so).</summary>
    [Fact]
    public void TheComponentListIsCappedAndSaysWhatItHeldBack()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["A Long Errand"])]),
            Catalog(Quest("A Long Errand", "Herald Ganelorn", items:
                ["Rat Whisker", "Bent Copper Ring", "Cracked Staff", "Gnoll Fang", "Bone Chips"]))));

        var source = Assert.Single(Assert.Single(set.Top).Why.OfType<QuestSourceFact>());
        Assert.Equal(5, source.Components);
        Assert.Equal(Recommendations.QuestItemsPerRow, source.Items.Count);

        var sentence = HelperPresentation.Why(source);
        Assert.Contains("5 turn-in items", sentence);
        Assert.Contains("and the rest on its page", sentence);
    }

    // ---- S11.2: the row is an execution path ------------------------------------------

    /// <summary>
    /// **A QUEST ROW OPENS THE MAP** (S11.2). The quest list and the Gear room were always
    /// there; the destination is what a hand-in could never offer, and the quest catalog knows
    /// where its giver stands.
    /// </summary>
    [Fact]
    public void AQuestRowWithAStartZoneOffersTheMapDoor()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["A Humble Errand"])]),
            Catalog(Quest("A Humble Errand", "Herald Ganelorn", "Qeynos"))));

        var row = Assert.Single(set.Top);
        var map = Assert.Single(row.Doors, d => d.Kind == HelperDoorKind.World);
        Assert.Equal("Qeynos", map.Target);
        Assert.Contains("Qeynos", HelperPresentation.DoorTip(map));

        // …and the row is still ABOUT the quest. A quest row that took the zone as its subject
        // would join with drop rows there (HOME-005) and claim a camp hands out the reward.
        Assert.Equal("A Humble Errand", row.Subject);
        Assert.Equal("", row.Zone);
    }

    /// <summary>The negative that keeps the door honest: a quest whose page named nowhere gets
    /// the two doors it always had rather than a map door pointing at nothing.</summary>
    [Fact]
    public void AQuestWithNoStartZoneOffersNoMapDoor()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["A Humble Errand"])]),
            Catalog(Quest("A Humble Errand", giver: "Herald Ganelorn"))));

        var row = Assert.Single(set.Top);
        Assert.DoesNotContain(row.Doors, d => d.Kind == HelperDoorKind.World);
        Assert.Contains(row.Doors, d => d.Kind == HelperDoorKind.QuestCatalog);
    }

    // ---- the withhold rule -------------------------------------------------------------

    /// <summary>
    /// **A QUEST EQBUDDY'S OWN QUEST LIST DOES NOT HOLD IS NOT AN OFFER** — and it is COUNTED,
    /// never dropped in silence (S19.2, S20; trap 50).
    /// </summary>
    [Fact]
    public void AQuestTheCatalogCannotDescribeIsWithheldAndCounted()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["</ul>"])]),
            Catalog(Quest("A Humble Errand", "Herald Ganelorn", "Qeynos"))));

        Assert.Empty(set.Top);
        Assert.Equal(1, set.GearQuestWithheld);
        Assert.Equal(GoalGapReason.NoUpgradeNamesAQuestPath, Assert.Single(set.Gaps).Reason);

        var caption = HelperPresentation.QuestOffersWithheld(set.GearQuestWithheld);
        Assert.Contains("quest list EQBuddy ships", caption);
    }

    /// <summary>A quest the catalog HOLDS but that answers none of the four questions is
    /// withheld too: resolving a name is not the same as having something to say, and a row
    /// that can only print a title is the Rathe defect in quest clothes.</summary>
    [Fact]
    public void AQuestEntryThatAnswersNothingIsWithheldAsWell()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["A Silent Errand"])]),
            Catalog(Quest("A Silent Errand", era: "Classic"))));

        Assert.Empty(set.Top);
        Assert.Equal(1, set.GearQuestWithheld);
    }

    /// <summary>The negative that keeps the rule from being a filter that removes everything: a
    /// quest the catalog CAN describe survives, and nothing is counted.</summary>
    [Fact]
    public void ADescribableQuestSurvivesAndNothingIsWithheld()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["A Humble Errand"])]),
            Catalog(Quest("A Humble Errand", "Herald Ganelorn"))));

        Assert.Equal("A Humble Errand", Assert.Single(set.Top).Subject);
        Assert.Equal(0, set.GearQuestWithheld);
        Assert.Empty(set.Gaps);
    }

    /// <summary>
    /// **NO QUEST CATALOG STANDS THE RULE DOWN WHOLE** (trap 73), the band gate's own voice —
    /// and it is the clause that keeps a stripped build or a fixture from silently losing every
    /// quest row.
    /// </summary>
    [Fact]
    public void WithNoQuestCatalogNothingIsWithheldAndNothingIsClaimed()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["A Humble Errand"])]),
            quests: null));

        var row = Assert.Single(set.Top);
        Assert.Equal("A Humble Errand", row.Subject);
        Assert.Equal(0, set.GearQuestWithheld);
        // Nothing is INVENTED to fill the gap either — the row keeps the shape it had before
        // this slice rather than growing a sentence with no source behind it.
        Assert.Empty(row.Why.OfType<QuestSourceFact>());
    }

    /// <summary>It counts per (item, quest) OFFER, the who rule's own denominator: one quest
    /// handing out two undescribable upgrades is two withheld offers and one missing row.</summary>
    [Fact]
    public void TheWithholdCountIsPerOfferAndNotPerQuest()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4), Worn("Rusty Bracer", "ARMS", 2)],
            new ItemCatalog([
                Record("Blessed Helm", "HEAD", 12, quests: ["A Silent Errand"]),
                Record("Blessed Bracer", "ARMS", 9, quests: ["A Silent Errand"]),
            ]),
            Catalog(Quest("A Silent Errand"))));

        Assert.Empty(set.Top);
        Assert.Equal(2, set.GearQuestWithheld);
    }

    /// <summary>A withheld offer must not set the yardstick the surviving rows are measured
    /// against — <c>WhoRule</c>'s own property, and the reason both rules prune rather than
    /// filtering at the far end.</summary>
    [Fact]
    public void AWithheldQuestOfferDoesNotInflateTheWeightOfTheRowsThatSurvive()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4), Worn("Rusty Bracer", "ARMS", 2)],
            new ItemCatalog([
                Record("Blessed Helm", "HEAD", 12, quests: ["A Humble Errand"]),
                Record("Blessed Bracer", "ARMS", 9, quests: ["A Silent Errand"]),
            ]),
            Catalog(Quest("A Humble Errand", "Herald Ganelorn"), Quest("A Silent Errand"))));

        var row = Assert.Single(set.Top);
        Assert.Equal("A Humble Errand", row.Subject);
        Assert.Equal(1.0, row.Weight);
    }

    // ---- S25 AC 7: one comparison truth ------------------------------------------------

    /// <summary>
    /// **THE DROP PATH AND THE QUEST PATH COMPARE THE ITEM THE SAME WAY** (S25 AC 7, S10.3:
    /// *"the acquisition path changes; the item-comparison truth must not"*).
    ///
    /// <para>One item that both drops AND is a quest reward produces two rows, and the two
    /// rows' item lines are identical down to the metric and the number — because they are the
    /// same <c>GearUpgrade</c>, out of one <c>GearUpgrades.Sweep</c> pass over one
    /// <c>ItemDominance</c> table. Nothing in this engine compares an item twice.</para>
    /// </summary>
    [Fact]
    public void ADropRowAndAQuestRowForOneItemCarryTheSameComparison()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([
                Record("Blessed Helm", "HEAD", 12, ["Lower Guk"], ["A Humble Errand"]),
            ]),
            Catalog(Quest("A Humble Errand", "Herald Ganelorn", "Qeynos"))));

        var drop = Assert.Single(set.Top, r => r.Kind == RecommendationKind.Zone);
        var quest = Assert.Single(set.Top, r => r.Kind == RecommendationKind.Quest);

        var a = Assert.Single(drop.Why.OfType<GearUpgradeFact>());
        var b = Assert.Single(quest.Why.OfType<GearUpgradeFact>());
        Assert.Equal(a.Item, b.Item);
        Assert.Equal(a.Over, b.Over);
        Assert.Equal(a.Slot, b.Slot);
        Assert.Equal(a.GainMetric, b.GainMetric);
        Assert.Equal(a.GainBy, b.GainBy);

        // The paths differ where they SHOULD: the camp names a creature and the quest names a
        // giver, and neither borrows the other's answer.
        Assert.NotEmpty(a.Who);
        Assert.Empty(b.Who);
        Assert.Empty(drop.Why.OfType<QuestSourceFact>());
        Assert.Single(quest.Why.OfType<QuestSourceFact>());
    }

    // ---- S10.1: the two the sweep never passed on --------------------------------------

    /// <summary>
    /// **AN UPGRADE WITH NO SOURCE AT ALL IS COUNTED** (S10.1/S19.2). The sweep has always
    /// dropped it; what is new is that it stops doing so in silence, which is the Founder's
    /// unread bow one layer up.
    /// </summary>
    [Fact]
    public void AnUpgradeNoPageCanSourceIsCountedAndSaidOutLoud()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Mystery Helm", "HEAD", 12)])));

        Assert.Empty(set.Top);
        Assert.Equal(1, set.GearNoSource);
        Assert.Equal(0, set.GearQuestOnly);
        Assert.Equal(GoalGapReason.NoCatalogUpgrade, Assert.Single(set.Gaps).Reason);

        Assert.Contains("nowhere to send you",
            HelperPresentation.SourcelessUpgrades(set.GearNoSource));
    }

    /// <summary>
    /// **AND ONE THE TOGGLE IS HIDING IS COUNTED SEPARATELY** (S10.1: *"do not restrict
    /// recommendations to direct creature drops"*). It is the one refusal in the block the
    /// player can undo from where they are standing, so it must not be summed with the one that
    /// has no remedy at all.
    /// </summary>
    [Fact]
    public void AQuestOnlyUpgradeBehindTheToggleIsItsOwnCount()
    {
        var inputs = Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([
                Record("Blessed Helm", "HEAD", 12, quests: ["A Humble Errand"]),
                Record("Mystery Helm", "HEAD", 11),
            ]),
            Catalog(Quest("A Humble Errand", "Herald Ganelorn")),
            includeQuests: false);

        var set = Rank(inputs);
        Assert.Empty(set.Top);
        Assert.Equal(1, set.GearQuestOnly);
        Assert.Equal(1, set.GearNoSource);

        Assert.Contains("include quests", HelperPresentation.QuestOnlyUpgrades(set.GearQuestOnly));

        // …and turning the toggle on really is the remedy the sentence names.
        var on = Rank(inputs with { IncludeQuests = true });
        Assert.Equal("A Humble Errand", Assert.Single(on.Top).Subject);
        Assert.Equal(0, on.GearQuestOnly);
    }

    /// <summary>
    /// The two counts ride a list that is NOT empty, which is the case a caption about them has
    /// to survive: a player looking at three good rows is exactly the player who cannot tell
    /// that a fourth was dropped for having no page behind it.
    /// </summary>
    [Fact]
    public void TheSweepsCountsRideBesideAListThatAnsweredSomething()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([
                Record("Froglok Bone Helm", "HEAD", 9, ["Lower Guk"]),
                Record("Mystery Helm", "HEAD", 12),
            ])));

        Assert.Equal("Lower Guk", Assert.Single(set.Top).Zone);
        Assert.Equal(1, set.GearCandidates);   // only the one that reached a bucket
        Assert.Equal(1, set.GearNoSource);
        Assert.Empty(set.Gaps);                // the goal answered; the count is a caveat
    }

    // ---- DRA-180 D3's ladder gains a fourth cause --------------------------------------

    /// <summary>
    /// **THE PER-ANCHOR SENTENCE NAMES THE QUEST RULE TOO**, and the four causes stay a
    /// partition of what the sweep found (DRA-180 D3's property, one rule on).
    /// </summary>
    [Fact]
    public void AnAnchorEmptiedByTheQuestRuleSaysSoAndTheCausesStillSum()
    {
        var set = Rank(Gear(
            [Worn("Rusty Helm", "HEAD", 4)],
            new ItemCatalog([Record("Blessed Helm", "HEAD", 12, quests: ["A Silent Errand"])]),
            Catalog(Quest("A Silent Errand"))));

        var anchor = Assert.Single(set.GearAnchorsRemoved);
        Assert.Equal("Rusty Helm", anchor.Anchor);
        Assert.Equal(1, anchor.Found);
        Assert.Equal(1, anchor.NoQuestPath);
        Assert.Equal(anchor.Found,
            anchor.LaterContent + anchor.OutsideBand + anchor.NoCreature + anchor.NoQuestPath);

        var sentence = HelperPresentation.AnchorAllRemoved(anchor);
        Assert.Contains("quest list EQBuddy ships", sentence);
        // Only the causes that spent something — a "0 sit in later content" clause would be a
        // sentence about a rule that did not run.
        Assert.DoesNotContain("eqlwiki lists creature levels", sentence);
    }
}
