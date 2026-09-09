using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// A class is fully on the guided model or exactly as it was — never half (Founder lock 5,
/// Fable plan §2 D4).
///
/// <para><b>Why this is a must-list and not a "no guide may…" rule.</b> Forbidding the wrong
/// thing cannot see a MISSING thing (trap 34). The failure this guards is not a bad guide, it
/// is a guide that quietly leaves one of its reward's drops out: the projection REPLACES a
/// group's item rows with the guide's objectives, so an unmentioned drop does not appear
/// alongside — it disappears, and the player is walked through a quest that never asks them
/// for a piece they still need. Nothing in the guide would look wrong.</para>
///
/// <para>So the claim is positive and it is enumerated from <see cref="SkyQuestDefaults"/>:
/// every item row of every reward of every class that has ANY guide has an objective naming
/// it. Authoring a class is all of it or none of it.</para>
/// </summary>
public sealed class GuideClassCoverageTests
{
    private static IReadOnlyList<SkyQuestChecklistItem> SkyRows =>
        [.. SkyQuestDefaults.Items];

    /// <summary>Classes that have at least one shipped guide — the ones the rules below
    /// bind. A class with none renders the classic checklist and is not in scope.</summary>
    private static IReadOnlyList<string> GuidedClasses =>
        [.. GuideCatalog.Default.Guides
            .SelectMany(g => g.ApplicableClasses)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];

    [Fact]
    public void AtLeastOneClassIsGuidedOrThisWholeSuiteIsVacuous()
    {
        // The floor that stops every test below passing on an empty catalog.
        Assert.NotEmpty(GuidedClasses);
        Assert.True(GuideCatalog.Default.Guides.Count >= 3,
            "a proof set of one class is not a proof set");
    }

    [Fact]
    public void EveryRewardOfAGuidedClassHasAGuide()
    {
        var missing = new List<string>();
        foreach (var className in GuidedClasses)
        {
            var rewards = SkyRows
                .Where(i => i.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
                .Select(i => QuestChecklistLayout.RewardKey(i.ClassName, i.Reward))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            missing.AddRange(rewards
                .Where(key => GuideChecklistProjection.GuideFor(GuideCatalog.Default, key) is null)
                .Select(key => $"{key} has no guide, but {className} is a guided class"));
        }

        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }

    [Fact]
    public void EveryItemRowOfAGuidedRewardHasAnObjectiveNamingIt()
    {
        var missing = new List<string>();
        foreach (var guide in GuideCatalog.Default.Guides)
        {
            var rewardKey = GuideChecklistProjection.RewardKeyOf(guide);
            var named = new HashSet<string>(
                guide.AllObjectives.SelectMany(o => o.ItemNames), StringComparer.OrdinalIgnoreCase);

            foreach (var row in SkyRows.Where(i =>
                         QuestChecklistLayout.RewardKey(i.ClassName, i.Reward)
                             .Equals(rewardKey, StringComparison.OrdinalIgnoreCase)))
                if (!named.Contains(row.QuestItem))
                    missing.Add($"{guide.Id}: the checklist row '{row.QuestItem}' ({row.Id}) has "
                        + "no objective — the projection would drop it off the screen entirely");
        }

        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }

    /// <summary>Every gathering step of a Sky guide is item-backed, so its tick lands on the
    /// box the loot auto-tick already writes rather than in the guide ledger beside it.
    /// A step that quietly fell to the ledger would still work — and would stop lighting up
    /// when the log saw the drop, which is trap 4 wearing a working feature's clothes.</summary>
    [Fact]
    public void EveryGatheringStepOfAShippedSkyGuideIsBackedByItsOwnChecklistBox()
    {
        var settings = new AppSettings();
        settings.SkyQuestChecklist.AddRange(SkyRows.Select(i => i.Clone()));

        var wrong = new List<string>();
        foreach (var guide in GuideCatalog.Default.Guides
                     .Where(g => g.GuideType == GuideType.PlaneOfSkyQuest))
        {
            var items = GuideChecklistProjection.ItemsFor(
                settings, GuideChecklistProjection.RewardKeyOf(guide));
            foreach (var objective in guide.AllObjectives.Where(o => o.RewardKey.Length == 0))
            {
                var home = GuideProgressRouter.HomeFor(objective, items, out _);
                if (home != GuideProgressHome.SkyItem)
                    wrong.Add($"{guide.Id}/{objective.Id} routed to {home}, not SkyItem");
            }
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    [Fact]
    public void EverySkyGuideHasExactlyOneTurnInAndItNamesThisRewardsKey()
    {
        foreach (var guide in GuideCatalog.Default.Guides
                     .Where(g => g.GuideType == GuideType.PlaneOfSkyQuest))
        {
            var turnIns = guide.AllObjectives.Where(o => o.RewardKey.Length > 0).ToList();
            Assert.True(turnIns.Count == 1,
                $"{guide.Id} has {turnIns.Count} turn-in objectives; a Sky reward is handed "
                + "in exactly once");
            // And the quest name and the reward key describe the SAME reward — the catalog
            // link and the store key are two strings about one thing.
            Assert.Equal(turnIns[0].RewardKey, SkyTestSplit.RewardKeyFor(guide.QuestName));
        }
    }

    /// <summary>The turn-in cannot be reached before its pieces are. Authored per guide
    /// rather than derived, so this is the row that catches an author who forgot one.</summary>
    [Fact]
    public void ATurnInWaitsOnEveryOneOfItsOwnGatheringSteps()
    {
        foreach (var guide in GuideCatalog.Default.Guides
                     .Where(g => g.GuideType == GuideType.PlaneOfSkyQuest))
        {
            var turnIn = guide.AllObjectives.Single(o => o.RewardKey.Length > 0);
            var gathering = guide.AllObjectives
                .Where(o => o.RewardKey.Length == 0).Select(o => o.Id).Order();

            Assert.Equal(gathering, turnIn.PrerequisiteObjectiveIds.Order());
        }
    }

    [Fact]
    public void NoTwoGuidesClaimTheSameReward()
    {
        var byKey = GuideCatalog.Default.Guides
            .GroupBy(GuideChecklistProjection.RewardKeyOf, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} is claimed by {string.Join(", ", g.Select(x => x.Id))}");

        Assert.Empty(byKey);
    }

    /// <summary>Prove-fail: the coverage rule actually fails when a drop is left out. Without
    /// this the three tests above could be passing on an accident of the fixture.</summary>
    [Fact]
    public void AGuideThatLeavesOneOfItsRewardsDropsOutIsCaught()
    {
        var guide = GuideCatalog.Default.Guides
            .First(g => g.GuideType == GuideType.PlaneOfSkyQuest);
        var rewardKey = GuideChecklistProjection.RewardKeyOf(guide);
        var named = new HashSet<string>(
            guide.AllObjectives.SelectMany(o => o.ItemNames), StringComparer.OrdinalIgnoreCase);

        // The real catalog covers it...
        var rows = SkyRows.Where(i => QuestChecklistLayout.RewardKey(i.ClassName, i.Reward)
            .Equals(rewardKey, StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Contains(r.QuestItem, named, StringComparer.OrdinalIgnoreCase));

        // ...and the same check over a catalog missing one drop does not.
        named.Remove(rows[0].QuestItem);
        Assert.DoesNotContain(rows[0].QuestItem, named, StringComparer.OrdinalIgnoreCase);
    }
}
