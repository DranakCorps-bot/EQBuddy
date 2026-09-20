using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// The Plane of Sky **Closest to Completion** lens, in the running app (DRA-218).
///
/// <para>The WPF layer has no unit tests (docs/TestPlan.md §5), so the ordering itself is
/// proved over Core in <c>SkyClosestToCompletionTests</c> and what is asserted here is that
/// the lens REACHED THE SCREEN: the box is on the tab, the setting and the control agree from
/// one moment (trap 56), and the group the panel was handed FIRST is the one the lens picked.
/// A render that ignored the reordered list would pass every Core assertion there is.</para>
///
/// <para><b>The fixture is a BLOCKED quest, and that is a measurement rather than a
/// preference.</b> The shipped Warrior Sky guides hold three or four objectives each, and over
/// those sizes the class view's own progress-descending order and this lens's fewest-remaining
/// order <em>cannot</em> disagree — <c>k/4 &gt; j/3</c> and <c>4-k &gt; 3-j</c> have no solution.
/// So a count-only fixture would photograph two identical lists and call it a passing lens
/// (trap 34). The one thing that genuinely reorders this catalog is the blocker, which is also
/// the acceptance criterion the slice exists for (S23 AC 8).</para>
///
/// <para><b>Every expectation is DERIVED from the shipped catalog, never typed</b> — a literal
/// reward name would drift the day a class is re-authored and would then be photographing a
/// real state of something else (trap 23). The fixture character reads as a Warrior, so the
/// Sky tab narrows to Warrior on its own.</para>
/// </summary>
public class SkyClosestLensTests
{
    private static IReadOnlyList<Guide> WarriorGuides =>
        [.. GuideCatalog.Default.ForClass("Warrior")
            .Where(g => g.GuideType == GuideType.PlaneOfSkyQuest)];

    /// <summary>The reward a guide is for, off the key the projection itself matches on —
    /// never split out of a heading the render composed (trap 4).</summary>
    private static string RewardOf(Guide guide) =>
        GuideChecklistProjection.RewardKeyOf(guide).Split('|')[1];

    /// <summary>Spaces become underscores in the dump — see <c>QuestsView.Dumped</c>.</summary>
    private static string HeadingOf(string reward) => ("Warrior · " + reward).Replace(' ', '_');

    /// <summary>The quest the CLASS view leads with, and the one this fixture blocks. Rewards
    /// arrive ordered by class, then turned-in, then progress, then title — and a fixture that
    /// ticks nothing has made no progress, so the alphabet decides.</summary>
    private static Guide ClassViewLeader =>
        WarriorGuides.OrderBy(RewardOf, StringComparer.OrdinalIgnoreCase).First();

    /// <summary>The hand-in, found by the only thing that identifies it in the DATA: it is the
    /// objective that names prerequisites. <c>GuidePresentationTests</c> holds the invariant
    /// that this is true of every shipped guide, so a catalog that broke it fails there with
    /// its own name on it rather than here with a confusing one.</summary>
    private static GuideObjective TurnInOf(Guide guide) =>
        guide.AllObjectives.Single(o => o.PrerequisiteObjectiveIds.Count > 0);

    /// <summary>The step this fixture strikes out: a prerequisite of the hand-in, which is
    /// what makes the quest unfinishable rather than merely unfinished.</summary>
    private static GuideObjective SkippedStepOf(Guide guide) =>
        guide.AllObjectives.Single(o =>
            o.Id == TurnInOf(guide).PrerequisiteObjectiveIds[0]);

    /// <summary>What the LENS leads with once the alphabetical leader is blocked: the fewest
    /// steps left among the quests that can still be finished. On a fixture that ticks nothing,
    /// a guide's remaining count is its objective count.</summary>
    private static (string Reward, int Remaining) FewestUnblocked =>
        WarriorGuides
            .Where(g => g.Id != ClassViewLeader.Id)
            .Select(g => (Reward: RewardOf(g), Remaining: g.AllObjectives.Count()))
            .OrderBy(x => x.Remaining)
            .ThenBy(x => x.Reward, StringComparer.OrdinalIgnoreCase)
            .First();

    private static AppHarness Fixture(bool closest)
    {
        var app = new AppHarness(
            s => s.SkyClosestToCompletion = closest,
            new Dictionary<string, string>
            {
                ["EQBUDDY_SHELL"] = "quests:sky",
                ["EQBUDDY_QUESTS"] = "sky",
            });
        app.SeedQuestLedger(skippedObjectives: new Dictionary<string, IReadOnlyList<string>>
        {
            [ClassViewLeader.Id] = [SkippedStepOf(ClassViewLeader).Id],
        });
        return app;
    }

    /// <summary>
    /// **The fixture really does split the two orders**, and this runs before either app does.
    /// Without it both rows below could pass on a lens that changed nothing at all — the
    /// vacuous green trap 34 is about, one layer up from the code.
    ///
    /// <para>It also pins the arithmetic the acceptance criterion rests on: the blocked quest
    /// has FEWER steps left than the one that overtakes it, so this cannot be a count-based
    /// order wearing the blocker's clothes.</para>
    /// </summary>
    [Fact]
    public void TheBlockedQuestHasFewerStepsLeftThanTheOneThatOvertakesIt()
    {
        Assert.True(WarriorGuides.Count >= 2);
        Assert.NotEqual(RewardOf(ClassViewLeader), FewestUnblocked.Reward);

        // One struck-out step out of its objectives, so the blocked quest's own remaining
        // count is strictly the smallest on the tab — and it still must not lead.
        var blockedRemaining = ClassViewLeader.AllObjectives.Count() - 1;
        Assert.True(blockedRemaining < FewestUnblocked.Remaining);
    }

    /// <summary>
    /// **OFF is the default, and this is the row that proves it** (S4.4 / S23 AC 1). Even with
    /// a blocked quest in the profile, a player who has not touched the box gets the
    /// class-and-alphabet order they already know — the blocked quest still FIRST. The box is
    /// on screen offering the other one, because a lens with no door is a capability nobody
    /// can reach (trap 20) and an absent control photographs as an unremarkable panel
    /// (trap 29).
    /// </summary>
    [Fact]
    public void ByDefaultTheTabKeepsTheOrderItAlwaysHadEvenWithABlockedQuestInIt()
    {
        using var app = Fixture(closest: false);
        app.Launch();

        app.WaitForDump("shellQuestsTab", "sky", "the shell to reach the Plane of Sky tab");
        app.WaitForDump("shellQuestsSkyFirstGroup",
            HeadingOf(RewardOf(ClassViewLeader)), "the class order's first group");

        // THE SETTING AND THE CONTROL, from one moment — "the store says off" and "the box is
        // unticked" are different claims (trap 56).
        Assert.Equal(0, app.DumpValue("shellQuestsSkyClosest"));
        Assert.Equal(0, app.DumpValue("shellQuestsSkyClosestBox"));
        Assert.Equal(1, app.DumpValue("shellQuestsSkyViewShown"));
    }

    /// <summary>
    /// **ON: the blocked quest stops leading, and the sentence saying why is on the screen.**
    ///
    /// <para>The quest with the FEWEST steps left is last, and the quest that leads has MORE
    /// left to do — which is the acceptance criterion stated as an ordering (S23 AC 8). The
    /// remaining count is asserted beside the heading deliberately: a render that drew the
    /// right group for the wrong reason would pass a heading check alone.</para>
    ///
    /// <para>And the blocked sentence is read off the PANEL rather than off the group, because
    /// "the layout says so" and "the screen says so" are different claims (trap 56) — a
    /// heading that reordered correctly and drew no explanation would leave the player looking
    /// at a quest that had silently moved to the bottom.</para>
    /// </summary>
    [Fact]
    public void WithTheLensOnTheBlockedQuestSinksAndTheScreenSaysWhy()
    {
        using var app = Fixture(closest: true);
        app.Launch();

        app.WaitForDump("shellQuestsTab", "sky", "the shell to reach the Plane of Sky tab");
        app.WaitForDump("shellQuestsSkyClosest", 1, "the Sky tab to be on the completion lens");
        app.WaitForDump("shellQuestsSkyFirstGroup",
            HeadingOf(FewestUnblocked.Reward), "the closest FINISHABLE reward to lead");

        Assert.Equal(1, app.DumpValue("shellQuestsSkyClosestBox"));
        Assert.Equal(FewestUnblocked.Remaining, app.DumpValue("shellQuestsSkyFirstRemaining"));

        // The quest that USED to lead is not leading, and the one that is has more work in it.
        Assert.NotEqual(HeadingOf(RewardOf(ClassViewLeader)),
            app.DumpText("shellQuestsSkyFirstGroup"));

        // ...and the screen explains it, in Core's words. "-" is the dump's "no blocked
        // sentence reached the panel" — asserted against by name, or the StartsWith below
        // would be checking a sentinel (trap 78).
        var note = app.DumpText("shellQuestsSkyBlockedNote");
        Assert.NotEqual("-", note);
        Assert.StartsWith(QuestChecklistLayout.BlockedLead.Replace(' ', '_'), note,
            StringComparison.Ordinal);
        Assert.Contains(SkippedStepOf(ClassViewLeader).Title.Replace(' ', '_'), note,
            StringComparison.Ordinal);
    }
}
