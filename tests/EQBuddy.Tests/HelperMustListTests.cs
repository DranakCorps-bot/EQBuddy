using System.Reflection;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE HELPER'S MUST-LISTS — trap 34's other half, four times over.**
///
/// <para>A negative rule cannot see a MISSING thing. "No line claims a camp is safe" is a
/// ban, and a ban passes perfectly over a goal nobody decided about, a fact nobody worded and
/// a door that leads nowhere: the surface renders, the build is green, and the player gets a
/// chip that does nothing or a blank line under a headline. So every closed set the Helper
/// has is walked here, from the ENUM rather than from a list beside it (trap 30 — a
/// hand-maintained list stops covering the set the day the set grows).</para>
///
/// <para>Four sets, and each one has already been the shape of a real bug somewhere in this
/// repo: the goals (an undecided member falling through a default arm),
/// the <see cref="WhyFact"/> subtypes (a fact with no sentence — <c>UnlockGuidance</c>'s own
/// <c>ShapeFor</c> answers null for exactly this reason), the doors (an affordance that opens
/// nothing, the rail's own forbidden shape one level in), and the gap reasons (an empty state
/// that renders as a blank panel).</para>
/// </summary>
public class HelperMustListTests
{
    // ---- 1. every goal has a DECIDED shape ---------------------------------------------

    /// <summary>
    /// **Null is "nobody decided", and it is the only thing this can catch.**
    ///
    /// <c>Recommendations.ShapeFor</c> has no default arm answering
    /// <see cref="HelperGoalShape.Deferred"/>, deliberately: "a later slice owns this" and
    /// "nobody thought about this" would have looked identical on screen, and the second one
    /// is the one that ships a chip doing nothing at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(Goals))]
    public void EveryGoalHasADecidedShape(HelperGoal goal) =>
        Assert.NotNull(Recommendations.ShapeFor(goal));

    /// <summary>The Founder's nine, and the count asserted out loud so a tenth arriving is a
    /// deliberate edit here rather than a line someone slid into the enum.</summary>
    [Fact]
    public void TheGoalListIsTheFoundersNine()
    {
        Assert.Equal(9, Recommendations.All.Count);
        Assert.Equal(
            ["Level Up", "Farm Gear", "Unlock Classes", "Unlock Races", "Farm Motes",
             "Work on Faction", "Farm Materials", "Make Money", "Achievements"],
            Recommendations.All.Select(HelperPresentation.GoalLabel).ToArray());
    }

    /// <summary>
    /// **"Continue quests" is HOME-001's fifth default category and it is deliberately NOT a
    /// goal.** The Guide room already is that answer, and a chip that opened a second copy of
    /// it would be a room competing with itself. Asserted rather than left as a comment: a
    /// well-meaning "the PRD lists five, we have four of them" reading is exactly how it
    /// would arrive.
    /// </summary>
    [Fact]
    public void ThereIsNoContinueQuestsChip() =>
        Assert.DoesNotContain(Recommendations.All,
            g => HelperPresentation.GoalLabel(g).Contains("quest", StringComparison.OrdinalIgnoreCase));

    /// <summary>Every goal's chip has a label and a tooltip. A chip whose tooltip was empty
    /// would be a pill with no explanation at the one width where the room has least room
    /// for prose.</summary>
    [Theory]
    [MemberData(nameof(Goals))]
    public void EveryGoalHasAChipLabelAndATip(HelperGoal goal)
    {
        Assert.NotEmpty(HelperPresentation.GoalLabel(goal));
        Assert.NotEmpty(HelperPresentation.GoalTip(goal));
    }

    /// <summary>
    /// **A deferred goal says so AND points somewhere.**
    ///
    /// This is the pairing that keeps five of the nine chips from being dead affordances
    /// while their engines are built. The rail's own rule is that an affordance which opens
    /// nothing is a trap; a chip producing one apologetic sentence and no door would be that
    /// rule broken one level in, where the rail's guard cannot see it. Both halves are
    /// asserted together because either one alone is the bug.
    /// </summary>
    [Theory]
    [MemberData(nameof(Goals))]
    public void EveryDeferredGoalNamesTheRoomThatAnswersItToday(HelperGoal goal)
    {
        var deferred = Recommendations.ShapeFor(goal) == HelperGoalShape.Deferred;
        Assert.Equal(deferred, HelperPresentation.NotAnsweredYet(goal).Length > 0);
        Assert.Equal(deferred, HelperPresentation.NotAnsweredDoor(goal) is not null);

        if (!deferred) return;
        var kind = HelperPresentation.NotAnsweredDoor(goal)!.Value;
        var address = HelperPresentation.AddressFor(kind);
        Assert.NotNull(address);
        var parsed = ShellPages.ParseAddress(address);
        Assert.NotNull(parsed);
        Assert.Contains(parsed!.Value.Page, ShellPages.Landed);
    }

    /// <summary>The four D1 engines, named. This row is meant to be EDITED — a goal moving
    /// from Deferred to Answered is the whole of what a later slice delivers, and it should
    /// be a deliberate line in a diff rather than something that happens as a side effect.</summary>
    [Fact]
    public void FourGoalsAreAnsweredInThisDelivery() =>
        Assert.Equal(
            [HelperGoal.LevelUp, HelperGoal.UnlockClasses, HelperGoal.UnlockRaces,
             HelperGoal.WorkOnFaction],
            Recommendations.All
                .Where(g => Recommendations.ShapeFor(g) == HelperGoalShape.Answered)
                .ToArray());

    // ---- 2. every why-fact has a sentence ----------------------------------------------

    /// <summary>
    /// **Every <see cref="WhyFact"/> in the assembly is worded.**
    ///
    /// <para>Reflected rather than listed, so a fact shape added tomorrow is covered
    /// tomorrow. <c>HelperPresentation.Why</c>'s switch has a default arm answering "" —
    /// which is correct, because a room must not throw over a fact it does not recognise —
    /// and that arm is precisely what makes this test necessary: without it, a new fact draws
    /// a blank line under a headline and nothing anywhere says so.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(WhyFactTypes))]
    public void EveryWhyFactShapeHasASentence(Type type)
    {
        var fact = Build(type);
        Assert.NotEmpty(HelperPresentation.Why(fact));
    }

    /// <summary>The reflection's own liveness check. A rename that emptied the sweep above
    /// would otherwise pass in silence, which is how a guard reads as coverage while seeing
    /// nothing (trap 78's shape: a guard aimed at nothing is green).</summary>
    [Fact]
    public void TheWhyFactSweepActuallyFindsFacts() =>
        Assert.True(Subtypes().Count >= 6,
            $"Only {Subtypes().Count} WhyFact shapes were reached — the reflection has "
            + "stopped finding them. Check the type, not the assertion.");

    /// <summary>
    /// **HOME-004, applied by construction.** The label is appended by <c>Why</c> itself
    /// rather than by each arm, so a catalog-sourced fact added later is labelled by the
    /// switch that has to grow to accommodate it — and cannot be added unlabelled by
    /// somebody who did not know the rule.
    /// </summary>
    [Theory]
    [MemberData(nameof(WhyFactTypes))]
    public void ACatalogLineCarriesTheEstimateLabelAndAPersonalOneDoesNot(Type type)
    {
        var fact = Build(type);
        var line = HelperPresentation.Why(fact);
        Assert.Equal(fact.Evidence == Evidence.Catalog, line.Contains(HelperPresentation.CatalogLabel));
    }

    /// <summary>
    /// **The prove-fail for the row above** (trap 34: green-only is vacuous coverage). The
    /// same fact, re-tagged Catalog, must gain the label — so the assertion is reading the
    /// TAG rather than agreeing with a sentence that happened to contain the words.
    /// </summary>
    [Fact]
    public void RetaggingALineAsCatalogIsWhatAddsTheLabel()
    {
        var personal = new WordedFact("Your kills of a froglok tad moved it +5 each.", Evidence.Personal);
        var catalog = personal with { Evidence = Evidence.Catalog };

        Assert.DoesNotContain(HelperPresentation.CatalogLabel, HelperPresentation.Why(personal));
        Assert.Contains(HelperPresentation.CatalogLabel, HelperPresentation.Why(catalog));
        // And the sentence itself survives the re-tag untouched — the label is added, not
        // substituted, so a pass-through line keeps the words its own producer chose.
        Assert.Contains(personal.Text, HelperPresentation.Why(catalog));
    }

    // ---- 3. every door opens something --------------------------------------------------

    /// <summary>
    /// **A door either resolves to a LANDED room or opens the wiki, and nothing else.**
    ///
    /// <para>The null arm of <c>AddressFor</c> is a real answer and not an omission —
    /// <see cref="HelperDoorKind.WikiFaction"/> opens a browser — so the assertion is written
    /// as an exclusive choice rather than as "not null". A caller that treated null as "no
    /// door" would silently drop the one affordance a player with no farming history has.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(DoorKinds))]
    public void EveryDoorEitherLandsOnARoomOrOpensTheWiki(HelperDoorKind kind)
    {
        Assert.NotEmpty(HelperPresentation.DoorLabel(kind));
        Assert.NotEmpty(HelperPresentation.DoorTip(new HelperDoor(kind, "Faydark Rangers")));

        var address = HelperPresentation.AddressFor(kind);
        if (kind == HelperDoorKind.WikiFaction) { Assert.Null(address); return; }

        Assert.NotNull(address);
        var parsed = ShellPages.ParseAddress(address);
        Assert.NotNull(parsed);
        // Filtered through the SAME list the rail draws from. A door that named a room which
        // has not landed is the dead affordance `helperDeadDoors` counts from a launched app;
        // this catches it without one.
        Assert.Contains(parsed!.Value.Page, ShellPages.Landed);
    }

    // ---- 4. every gap has an empty state ------------------------------------------------

    /// <summary>Every reason a selected goal can come back empty is worded. A gap that
    /// rendered as a blank line is the "silent no-op" rule with the switch on the other
    /// side.</summary>
    [Theory]
    [MemberData(nameof(GapReasons))]
    public void EveryGapReasonIsWordedForEveryAnsweredGoal(GoalGapReason reason)
    {
        foreach (var goal in Recommendations.All
                     .Where(g => Recommendations.ShapeFor(g) == HelperGoalShape.Answered))
            Assert.NotEmpty(HelperPresentation.Gap(new GoalGap(goal, reason)));
    }

    // ---- fixtures ------------------------------------------------------------------------

    public static TheoryData<HelperGoal> Goals()
    {
        var data = new TheoryData<HelperGoal>();
        foreach (var goal in Enum.GetValues<HelperGoal>()) data.Add(goal);
        return data;
    }

    public static TheoryData<HelperDoorKind> DoorKinds()
    {
        var data = new TheoryData<HelperDoorKind>();
        foreach (var kind in Enum.GetValues<HelperDoorKind>()) data.Add(kind);
        return data;
    }

    public static TheoryData<GoalGapReason> GapReasons()
    {
        var data = new TheoryData<GoalGapReason>();
        foreach (var reason in Enum.GetValues<GoalGapReason>()) data.Add(reason);
        return data;
    }

    public static TheoryData<Type> WhyFactTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in Subtypes()) data.Add(type);
        return data;
    }

    internal static List<Type> Subtypes() =>
        [.. typeof(WhyFact).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(WhyFact)))
            .OrderBy(t => t.Name, StringComparer.Ordinal)];

    /// <summary>One of every fact shape, built from its own constructor with plausible
    /// values. Reflection rather than nine hand-written instances, for the reason the sweep
    /// itself exists: a hand-written list of fixtures stops covering the set the day the set
    /// grows, which is the same trap one level down.</summary>
    internal static WhyFact Build(Type type)
    {
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var args = ctor.GetParameters().Select(p => Sample(p.ParameterType)).ToArray();
        return (WhyFact)ctor.Invoke(args);
    }

    private static object Sample(Type type)
    {
        if (type == typeof(string)) return "Lower Guk";
        if (type == typeof(int)) return 3;
        if (type == typeof(double)) return 7.5;
        if (type == typeof(Evidence)) return Evidence.Personal;
        throw new InvalidOperationException(
            $"A WhyFact takes a {type.Name}, which this fixture cannot make up. Add an arm — "
            + "the sweep is only as complete as the values it can construct.");
    }
}
