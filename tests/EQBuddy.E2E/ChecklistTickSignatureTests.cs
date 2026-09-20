using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// **The Quest Tracker repaints when a guide step MOVES between "skipped" and "done"**
/// (DRA-218, Planner review of D2).
///
/// <para><c>QuestsView.ChecklistTickSignature</c> folded <c>SkippedObjectiveIds</c> and
/// <c>DoneObjectiveIds</c> through ONE <c>guideId + "/" + id</c> string, so an id present in
/// exactly one of them contributed the same hash and the same 1 whichever list it was in.
/// <c>QuestLedgerStore.SetObjectiveMembership</c> moves an id across in a single locked write
/// — ticking a struck-out step clears the strike — so one user action left the XOR unmoved,
/// the count unmoved, the whole signature unmoved, and <c>Refresh</c> taking its early
/// return. Trap 72 on this surface for the sixth time, and the second time on this term.</para>
///
/// <para><b>Why the writer is a PROBE and not a click.</b> Every write site inside a view
/// force-refreshes that view, so a step ticked by the surface under assertion proves nothing
/// about the repaint gate. The gate is only reached by a writer that CANNOT force a repaint:
/// the phone's tap, or the checkbox in the other instance (QuestsWindow and QuestsRoom build
/// one <c>QuestsView</c> each over one ledger, trap 45). <see cref="AppHarness.RemotelyTickGuideStep"/>
/// is that writer, through <c>GuideProgressRouter</c> and forcing nothing.</para>
///
/// <para><b>Why the GENERAL tab and not the Sky lens the finding named.</b> The blocked
/// heading D2 added would be the sharper symptom, and it is not reachable: MEASURED against
/// the shipped catalog, all 222 non-reward Plane of Sky objectives are <c>Loot</c>/<c>Farm</c>
/// naming exactly one row of their own reward group, so every one of them is
/// <c>GuideProgressHome.SkyItem</c> — its tick lands in <c>SkyQuestChecklist</c>, which is its
/// own term in the same signature, and the skip is cleared beside it. Two terms move and the
/// collision cannot happen there. The ids that live in BOTH ledger lists are the harvested
/// guides' prose and <c>TalkToNpc</c> steps, which the General tab draws — so that is where
/// the defect is, and asserting it on Sky would have been a green row over a state the app
/// cannot enter (trap 23). <see cref="TheFixtureStepIsOneTheGuideLedgerItselfOwns"/> pins the
/// premise, so a catalog that grew a guide-ledger-homed Sky step fails there by name.</para>
///
/// <para><b>Every expectation is DERIVED from the shipped catalog, never typed</b> — the
/// harvest is regenerated weekly, so a literal quest or step name would still launch, still
/// pass, and be photographing a real state of something else (trap 23).</para>
/// </summary>
public class ChecklistTickSignatureTests
{
    /// <summary>
    /// The fixture: a pinned quest whose guide holds a step the GUIDE LEDGER owns outright.
    ///
    /// <para>"Outright" is the predicate and it is stricter than "the router says GuideLedger
    /// today": a reward-keyed step is a Sky turn-in and an acquire-shaped one may be a
    /// checklist box depending on the group's rows, so both are excluded by SHAPE. What is
    /// left is guide-ledger-homed under every store set, which is what makes the tick a pure
    /// move between the two lists of one record and nothing else.</para>
    ///
    /// <para>Throws rather than skipping: a harvest with no such step would make every row
    /// below vacuous, which is the pass trap 34 names.</para></summary>
    private static (QuestEntry Quest, Guide Guide, GuideObjective Step) Target { get; } = Find();

    private static (QuestEntry, Guide, GuideObjective) Find()
    {
        foreach (var quest in QuestCatalog.LoadEmbedded().Quests
                     .OrderBy(q => q.Name, StringComparer.Ordinal))
        {
            if (GuideChecklistProjection.QuestGuideFor(GuideCatalog.Default, quest.Name)
                is not { } guide) continue;
            var step = guide.AllObjectives.FirstOrDefault(o => OwnedByTheGuideLedger(o, quest));
            if (step is not null) return (quest, guide, step);
        }
        throw new InvalidOperationException(
            "no shipped quest has a guide with a step the guide ledger owns outright — " +
            "DRA-218's signature fixture needs one");
    }

    /// <summary>Guide-ledger-homed under EVERY store set — see <see cref="Target"/>. Asked
    /// BOTH ways: the store-independent shape (which is the probe's own refusal rule), and
    /// the home under the stores this quest's pane really hands the router. The second one is
    /// not redundant — a <c>TurnIn</c> step passes the first and is the QUEST COMPLETION
    /// record once the quest is in hand, which is a different store and not a pure move.
    /// </summary>
    private static bool OwnedByTheGuideLedger(GuideObjective step, QuestEntry quest) =>
        step.RewardKey.Length == 0
        && !string.Equals(step.ObjectiveType, "TurnIn", StringComparison.Ordinal)
        && !GuideProgressRouter.ItemBackedObjectiveTypes.Contains(
            step.ObjectiveType, StringComparer.Ordinal)
        && GuideProgressRouter.HomeFor(step) == GuideProgressHome.GuideLedger
        && GuideProgressRouter.HomeFor(step, new GuideStores([], [], quest), out _)
            == GuideProgressHome.GuideLedger;

    private static string RowId =>
        GuideChecklistProjection.RowId(Target.Guide.Id, Target.Step.Id);

    /// <summary>The target quest pinned so it is the pane's selection, its guide expanded so
    /// the rows exist to be counted, and the one step STRUCK OUT — which is the half of the
    /// move the app cannot be driven into from out here (trap 22: a skip lives in the guide
    /// ledger, so <c>configureSettings</c> cannot reach it).</summary>
    private static AppHarness Fixture()
    {
        var app = new AppHarness(
            s => s.GuideExpanded.Add(Target.Guide.Id),
            new Dictionary<string, string>
            {
                ["EQBUDDY_SHELL"] = "quests:general",
                ["EQBUDDY_QUESTS"] = "general",
                // The remote writer. Inert until a trigger file lands in the profile.
                ["EQBUDDY_LENSPROBE"] = "1",
            });
        app.SeedQuestLedger(
            tracked: [Target.Quest.Name],
            skippedObjectives: new Dictionary<string, IReadOnlyList<string>>
            {
                [Target.Guide.Id] = [Target.Step.Id],
            });
        return app;
    }

    /// <summary>
    /// **The fixture really is a PURE move, and this runs before the app does.**
    ///
    /// <para>Without it the row below could pass on a build where the repaint came from some
    /// OTHER term of the signature moving — a Sky box, an Epic box, a completion record — and
    /// the collision would still be there. The claim is that ticking this step writes the
    /// guide ledger and nothing else, which is what makes it the one write the old fold could
    /// not see.</para>
    ///
    /// <para>It is also where a catalog change fails BY NAME: a Plane of Sky step that became
    /// guide-ledger-homed, or a harvest that lost its prose steps, breaks the premise here
    /// rather than as a confusing timeout in the launched app.</para>
    /// </summary>
    [Fact]
    public void TheFixtureStepIsOneTheGuideLedgerItselfOwns()
    {
        Assert.True(OwnedByTheGuideLedger(Target.Step, Target.Quest));
        // Spelled out again rather than left inside the predicate, because this is the arm
        // that already caught a real mis-pick: the first quest in the catalog whose guide
        // passed the store-independent shape offered its hand-in, which is
        // GuideProgressHome.QuestCompletion under its own quest and not a pure move.
        Assert.Equal(GuideProgressHome.GuideLedger, GuideProgressRouter.HomeFor(
            Target.Step, new GuideStores([], [], Target.Quest), out _));

        // The step the pane draws is the step the probe will name. RowId is BUILT by the
        // projection rather than spelled here, so a separator inside an id cannot make these
        // two disagree (GuideChecklistProjection.RowId's own rule).
        Assert.NotNull(GuideChecklistProjection.Resolve(GuideCatalog.Default, RowId));
    }

    /// <summary>
    /// **A struck-out step ticked from OUTSIDE the view repaints it.**
    ///
    /// <para>The shell's Guide room is the surface asserted: it never wrote anything, it
    /// cannot be forced from out here, and the only thing that can make its checkbox move is
    /// the <c>ck:</c> term noticing that one id changed lists. On the pre-fix build the XOR
    /// and the count are both unmoved by that write, so this times out with
    /// <c>shellQuestsGeneralGuideDone</c> still reading 0 — which is the prove-fail, measured
    /// rather than asserted (green-only coverage is vacuous, trap 34).</para>
    ///
    /// <para>The v1 window is asserted too, and with its OWN wait rather than beside the
    /// shell's from one read. It follows the widget's tick behind a two-second throttle
    /// (<c>QuestsView.MaybeRefresh</c>), so the moment the shell's number moves is not a
    /// moment the window's is about — reading them together fails on the throttle and says
    /// nothing about the gate, which is trap 56 from the other side. Two hosts, two waits,
    /// both the screen's answer and neither the store's.</para>
    /// </summary>
    [Fact]
    public void TickingASkippedStepFromOutsideTheViewRedrawsIt()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsTab", "general", "the shell to reach the General tab");
        app.WaitForDump("shellQuestsGeneralGuide", 1, "the guide block to reach the pane");
        // Drawn, and nothing ticked: the struck-out step is still a checkbox, unticked. The
        // floor under the assertion below — "0 became 1" is only a claim if 0 was real.
        app.WaitForDump("shellQuestsGeneralGuideDone", 0, "the struck-out step to draw unticked");
        Assert.True(app.DumpValue("shellQuestsGeneralGuideRows") >= 1,
            $"the pane drew no guide rows to tick; dump was: {app.Artifacts()}");
        // AND THE ROOM HAS STOPPED REDRAWING ON ITS OWN. Without this the row is green on
        // the broken build — MEASURED: the shell settles at its sixth render several seconds
        // after the guide block first appears, and a probe fired before that rides a repaint
        // that was already coming. With the anchor in, the pre-fix build holds `done=0` for
        // fifteen seconds after the write and this times out. Four seconds because "still"
        // has to outlast the surface's own two-second throttle.
        app.WaitUntilStill("shellQuestsRenders", TimeSpan.FromSeconds(4),
            "the Guide room to stop redrawing on its own before the probe writes");

        app.RemotelyTickGuideStep(RowId);

        app.WaitForDump("shellQuestsGeneralGuideDone", 1,
            "the shell's Guide room to redraw with the struck-out step now ticked");
        app.WaitForDump("questsGeneralGuideDone", 1,
            "and the v1 window to redraw too, once its own two-second throttle lets it");
    }

    /// <summary>
    /// **And the other direction, which is the same collision with the arguments swapped.**
    ///
    /// <para><c>SetObjectiveSkipped</c> is the mirror image of <c>SetObjectiveDone</c> — same
    /// locked move, same two lists — so a fix that only separated one of them would leave
    /// half the defect. Striking out a step that is DONE moves the id the other way, and the
    /// checkbox has to clear.</para>
    ///
    /// <para>The tick that sets it up is the same remote writer, deliberately: it means the
    /// second half of the test runs against a ledger the app itself wrote, rather than one
    /// seeded into a shape the app might never produce (trap 23).</para>
    /// </summary>
    [Fact]
    public void StrikingOutATickedStepFromOutsideTheViewRedrawsItToo()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsGeneralGuide", 1, "the guide block to reach the pane");
        app.WaitUntilStill("shellQuestsRenders", TimeSpan.FromSeconds(4),
            "the Guide room to stop redrawing on its own before the first probe writes");
        app.RemotelyTickGuideStep(RowId);
        app.WaitForDump("shellQuestsGeneralGuideDone", 1, "the step to read as done first");

        // Still again, so the SECOND write is the only thing the clear can be attributed to.
        app.WaitUntilStill("shellQuestsRenders", TimeSpan.FromSeconds(4),
            "the Guide room to stop redrawing before the strike-out is written");
        app.RemotelyTickGuideStep(RowId, done: false);

        app.WaitForDump("shellQuestsGeneralGuideDone", 0,
            "the shell's Guide room to redraw with the step struck out again");
    }
}
