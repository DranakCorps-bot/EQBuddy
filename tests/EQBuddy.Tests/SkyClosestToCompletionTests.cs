using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The **Closest to Completion** lens and the hard-blocker model under it (DRA-218,
/// requirements S5.1/5.2/5.4/5.5, acceptance S23 AC 6-10).
///
/// <para>The arithmetic the lens needed was mostly already here — <c>Done</c>, <c>Total</c>,
/// <c>Progress</c>. What was missing is the reason a count is not an answer: a reward with one
/// step left and a struck-out prerequisite behind it is not one step from anything, and no
/// ordering over <see cref="QuestChecklistGroup.Remaining"/> alone can tell it apart from the
/// reward you could genuinely finish this evening.</para>
///
/// <para><b>Every claim about the blocker has a before-picture beside it</b> (trap 34): a
/// guard that only ever goes green is vacuous coverage, so the fixture below is built so that
/// ordering on the count ALONE puts the unfinishable reward FIRST, and a test asserts that it
/// does.</para>
/// </summary>
public sealed class SkyClosestToCompletionTests
{
    private const string Warrior = "Warrior";

    /// <summary>Alphabetically FIRST and, by the count alone, closest to done — and it is the
    /// one reward in the fixture that cannot be finished at all. Both of those are
    /// deliberate: they are the two orders the lens has to beat.</summary>
    private const string Blocked = "Aegis of Wind";

    /// <summary>Alphabetically LAST and genuinely the nearest thing to finished.</summary>
    private const string Close = "Zephyr Band";

    private const string Far = "Mantle of Gales";

    private const string SkippedStep = "Collect Wind Rune Azia";

    private static QuestChecklistRow Row(
        string id, bool acquired = false, bool skipped = false, bool turnIn = false,
        string blockedBy = "", double island = 0) =>
        new(id, Warrior, id, "",
            Acquired: acquired,
            Unassigned: false,
            IslandHeading: island > 0 ? SkyIslands.Heading(island) : "",
            GuideRowKey: "pos-warrior",
            IsTurnIn: turnIn,
            IsSkipped: skipped,
            IslandKey: island > 0 ? SkyIslands.SetKey([island]) : "",
            BlockedBy: blockedBy);

    private static QuestChecklistGroup Group(string title, params QuestChecklistRow[] rows) =>
        new(Warrior, title, rows,
            CompletionKey: QuestChecklistLayout.RewardKey(Warrior, title),
            GuideId: "pos-warrior");

    /// <summary>
    /// One piece to collect, one STRUCK OUT, and the hand-in waiting on both. Two steps left
    /// by the count — fewer than anything else in the fixture — and doing both of them will
    /// not finish it.
    ///
    /// <para><b>The open piece beside the skipped one is what makes this the case worth
    /// testing</b>, and the first fixture written here did not have it. With every
    /// gatherable step struck out the heading already said "set aside" — the player put the
    /// quest down whole, and that word is right. This is the shape that has real work in it:
    /// the tab reads "in progress", the player goes and does the work, and the reward still
    /// cannot be handed in at the end of it.</para></summary>
    private static QuestChecklistGroup BlockedGroup() => Group(Blocked,
        Row("aegis-piece", island: 4),
        Row("aegis-rune", skipped: true, island: 4),
        Row("aegis-turnin", turnIn: true, blockedBy: SkippedStep));

    /// <summary>One piece in hand, two to collect, then hand in. THREE steps left — more
    /// than the blocked reward has — and every one of them is something the player can go
    /// and do tonight.</summary>
    private static QuestChecklistGroup CloseGroup() => Group(Close,
        Row("zephyr-piece", acquired: true, island: 4),
        Row("zephyr-rune", island: 4),
        Row("zephyr-shard", island: 4),
        Row("zephyr-turnin", turnIn: true));

    private static QuestChecklistGroup FarGroup() => Group(Far,
        Row("mantle-piece", island: 6),
        Row("mantle-rune", island: 6),
        Row("mantle-shard", island: 6),
        Row("mantle-sigil", island: 6),
        Row("mantle-turnin", turnIn: true));

    private static IReadOnlyList<QuestChecklistGroup> Fixture() =>
        [BlockedGroup(), CloseGroup(), FarGroup()];

    // ---- S23 AC 7: the lens orders by work left, not by quest order -------------------

    /// <summary>The whole ask (S5.1): lead with the reward you have the least left to do on.
    /// The fixture's class-view order is alphabetical by reward — Aegis, Mantle, Zephyr — so
    /// an implementation that forgot to sort at all would fail here rather than pass by
    /// accident.</summary>
    [Fact]
    public void TheLensLeadsWithTheFewestStepsLeftAndNotWithTheOrderTheGroupsArrivedIn()
    {
        var arrived = Fixture().Select(g => g.Title).ToList();
        Assert.Equal([Blocked, Close, Far], arrived);

        var ranked = QuestChecklistLayout.ClosestToCompletion(Fixture());

        Assert.Equal([Close, Far, Blocked], ranked.Select(g => g.Title));
    }

    /// <summary>A step the player struck out is not work: <see cref="QuestChecklistGroup.Done"/>
    /// still counts it against the total, and <c>Remaining</c> deliberately does not. Without
    /// this the lens would rank a reward as further away than the player believes it is,
    /// which is the same disagreement the heading's "set aside" word was added to fix.</summary>
    [Fact]
    public void ASkippedStepIsNotWorkSoItIsNotCountedAsRemaining()
    {
        var blocked = BlockedGroup();

        Assert.Equal(3, blocked.Total);
        Assert.Equal(0, blocked.Done);
        // Three steps, none held, one struck out — so TWO things are left to do, and
        // Total - Done would have said three.
        Assert.Equal(2, blocked.Remaining);
        Assert.NotEqual(blocked.Total - blocked.Done, blocked.Remaining);
    }

    // ---- S23 AC 8: one step left is not the same as finishable ------------------------

    /// <summary><b>The before-picture</b> (trap 34). Ordering on the count alone — which is
    /// exactly what "fewest remaining required steps" says on the tin — puts the one reward
    /// that cannot be finished at the TOP of the list. Every assertion below about the
    /// blocker is only worth something because this is true.</summary>
    [Fact]
    public void TheCountALONEWouldPutTheUnfinishableRewardFirst()
    {
        var byCountOnly = Fixture().OrderBy(g => g.Remaining).Select(g => g.Title);

        Assert.Equal([Blocked, Close, Far], byCountOnly);
    }

    /// <summary>...and the lens does not. The reward waiting on a struck-out prerequisite
    /// sinks below every reward that still has workable steps — even the one with three of
    /// them, because "further away" beats "not reachable at all" for a list whose whole job is
    /// to name what to go and do next.</summary>
    [Fact]
    public void AQuestWaitingOnASkippedPrerequisiteNeverLEADSTheLensEvenWithOneStepLeft()
    {
        var ranked = QuestChecklistLayout.ClosestToCompletion(Fixture());

        Assert.Equal(Close, ranked[0].Title);
        Assert.Equal(Blocked, ranked[^1].Title);
        // And the count it is beaten by is LARGER, so this cannot have passed on the
        // arithmetic alone.
        Assert.True(ranked[^1].Remaining < ranked[0].Remaining);
    }

    /// <summary>The group says so in one word on its heading, where a folded list often shows
    /// nothing else about a quest. It read "in progress" before — true, and the reading that
    /// makes a stuck reward look like the next thing to go and do.</summary>
    [Fact]
    public void TheHeadingSaysBlockedRatherThanInProgress()
    {
        Assert.True(BlockedGroup().Blocked);
        Assert.Equal("blocked", BlockedGroup().Note);
    }

    /// <summary>The committed negatives, one per neighbouring word. A group that is merely
    /// part-done still reads "in progress", a group with every piece in hand still reads
    /// "ready", and a group whose remaining steps are ALL struck out still reads "set aside" —
    /// that player put the quest down whole, and "blocked" would invite them to go unblock
    /// something they closed on purpose.</summary>
    [Fact]
    public void NothingELSEStartedReadingBlocked()
    {
        Assert.False(CloseGroup().Blocked);
        Assert.Equal("in progress", CloseGroup().Note);
        Assert.Null(FarGroup().Note);

        var ready = Group(Close,
            Row("zephyr-piece", acquired: true),
            Row("zephyr-rune", acquired: true),
            Row("zephyr-turnin", turnIn: true));
        Assert.Equal("ready", ready.Note);
        Assert.False(ready.Blocked);

        // EVERY gatherable step struck out, with the hand-in still gated on one of them.
        // This group IS blocked and the heading still says "set aside", which is the order
        // the two arms are deliberately in: the player put this quest down whole, and
        // "blocked" would invite them to go unblock something they closed on purpose.
        var setAside = Group(Blocked,
            Row("aegis-piece", acquired: true),
            Row("aegis-rune", skipped: true),
            Row("aegis-turnin", turnIn: true, blockedBy: SkippedStep));
        Assert.True(setAside.Blocked);
        Assert.Equal("set aside", setAside.Note);
    }

    /// <summary>The sentence names the step, and an unblocked group gets NO sentence at all —
    /// a surface adds nothing for the ordinary case rather than carrying a standing
    /// disclaimer nobody reads.</summary>
    [Fact]
    public void TheNoteNamesTheSkippedStepAndAnUnblockedGroupGetsNone()
    {
        Assert.Equal(
            QuestChecklistLayout.BlockedLead + SkippedStep + ".",
            QuestChecklistLayout.BlockedNote(BlockedGroup()));

        Assert.Null(QuestChecklistLayout.BlockedNote(CloseGroup()));
        Assert.Null(QuestChecklistLayout.BlockedNote(FarGroup()));
    }

    /// <summary>A blocker on a row nobody is going to take answers nothing. An ACQUIRED row
    /// cannot be waiting on anything, and a SKIPPED one is not work — so neither makes a group
    /// blocked, and a stale string on either is inert rather than a false alarm.</summary>
    [Fact]
    public void ABlockerOnAnAcquiredOrSkippedRowDoesNotBlockTheGroup()
    {
        var acquired = Group(Close,
            Row("zephyr-piece", acquired: true, blockedBy: SkippedStep),
            Row("zephyr-rune"));
        Assert.False(acquired.Blocked);

        var skipped = Group(Close,
            Row("zephyr-piece", skipped: true, blockedBy: SkippedStep),
            Row("zephyr-rune"));
        Assert.False(skipped.Blocked);
    }

    // ---- S23 AC 6/10: nothing about the checklist itself changes ----------------------

    /// <summary><b>It is an ORDER, not a view.</b> The same group objects come back, by
    /// REFERENCE — so every row id, every tick setter and every ledger-backed refusal is the
    /// one class order would have handed over. "Looks the same" and "is the same object" are
    /// different promises and only the second cannot drift (signed plan P1, trap 4).</summary>
    [Fact]
    public void TheLensReordersTheVerySameGroupsAndAddsOrRemovesNothing()
    {
        var before = Fixture();

        var ranked = QuestChecklistLayout.ClosestToCompletion(before);

        Assert.Equal(before.Count, ranked.Count);
        foreach (var group in before) Assert.Contains(group, ranked);
        // S5.4: no per-lens step text. The rows are the objects, so there is no second
        // wording to disagree with the first.
        Assert.All(ranked, g => Assert.Same(
            before.Single(b => b.Title == g.Title).Rows, g.Rows));
        Assert.Equal(
            before.Sum(g => g.Done) + "/" + before.Sum(g => g.Total),
            ranked.Sum(g => g.Done) + "/" + ranked.Sum(g => g.Total));
    }

    /// <summary>A reward with nothing left — turned in, or every remaining step struck out —
    /// sinks below everything that still has work, because nothing is left to be close to.
    /// It sinks BELOW the blocked one too: the blocked reward still has a step somebody could
    /// un-skip, and a finished one has nothing at all.</summary>
    [Fact]
    public void AGroupWithNothingLeftSinksBelowEverythingThatStillHasWork()
    {
        var done = Group(Close,
            Row("zephyr-piece", acquired: true),
            Row("zephyr-turnin", turnIn: true, acquired: true));
        Assert.Equal(0, done.Remaining);

        var ranked = QuestChecklistLayout.ClosestToCompletion(
            [done, BlockedGroup(), FarGroup()]);

        Assert.Equal([Far, Blocked, Close], ranked.Select(g => g.Title));
    }

    /// <summary>The order is TOTAL, so a repaint cannot shuffle two rewards that are equally
    /// close. Two groups with identical counts fall through to class and title, which is the
    /// stable order the rest of this screen already uses.</summary>
    [Fact]
    public void TwoEquallyCloseRewardsCannotShuffleBetweenRepaints()
    {
        var a = Group("Alpha Band", Row("a1"), Row("a2", turnIn: true));
        var z = Group("Omega Band", Row("z1"), Row("z2", turnIn: true));

        Assert.Equal(["Alpha Band", "Omega Band"],
            QuestChecklistLayout.ClosestToCompletion([z, a]).Select(g => g.Title));
        Assert.Equal(["Alpha Band", "Omega Band"],
            QuestChecklistLayout.ClosestToCompletion([a, z]).Select(g => g.Title));
    }

    // ---- S23 AC 9: it composes with the EXISTING island view --------------------------

    /// <summary>The lens reaches the ROWS inside an island, through the island view the repo
    /// already ships — nothing regrouped, nothing rebuilt. Island 4 holds a step from each of
    /// two rewards, and with the lens on the closer reward's step leads; the blocked reward's
    /// sinks.</summary>
    [Fact]
    public void TheIslandViewLeadsWithTheCloserRewardWhenTheLensIsOn()
    {
        var island4 = QuestChecklistLayout
            .SkyByIsland(Fixture(), repeatMultiIsland: false, byCompletion: true)
            .Groups.Single(g => g.Heading == "Island 4");

        Assert.Equal([Close, Close, Close, Blocked, Blocked],
            island4.Rows.Select(r => r.Reward));
    }

    /// <summary>...and OFF, the island view is the one that shipped: class, then reward,
    /// then step, alphabetically. The lens's default is off, so this is what every player who
    /// upgrades keeps seeing (S23 AC 1 / done bar 6).</summary>
    [Fact]
    public void TheIslandViewIsUntouchedWhenTheLensIsOff()
    {
        var off = QuestChecklistLayout.SkyByIsland(Fixture())
            .Groups.Single(g => g.Heading == "Island 4");
        var explicitlyOff = QuestChecklistLayout
            .SkyByIsland(Fixture(), repeatMultiIsland: false, byCompletion: false)
            .Groups.Single(g => g.Heading == "Island 4");

        // Aegis of Wind before Zephyr Band — the alphabet, which is the order this view has
        // always used and the one the lens above deliberately beats.
        Assert.Equal([Blocked, Blocked, Close, Close, Close], off.Rows.Select(r => r.Reward));
        Assert.Equal(off.Rows.Select(r => r.Row.Id), explicitlyOff.Rows.Select(r => r.Row.Id));
    }

    /// <summary>The island view's own arithmetic is untouched by the lens: the same rows are
    /// in the same islands with the same score, only in a different order. A reordering that
    /// moved a row between islands or dropped one would change these numbers.</summary>
    [Fact]
    public void OrderingTheIslandViewChangesNoIslandsMembershipOrScore()
    {
        var off = QuestChecklistLayout.SkyByIsland(Fixture());
        var on = QuestChecklistLayout
            .SkyByIsland(Fixture(), repeatMultiIsland: false, byCompletion: true);

        Assert.Equal(
            off.Groups.Select(g => $"{g.Heading} {g.Done}/{g.Total}"),
            on.Groups.Select(g => $"{g.Heading} {g.Done}/{g.Total}"));
        Assert.Equal(off.HiddenTurnIns, on.HiddenTurnIns);
        Assert.Equal(off.HiddenRewards, on.HiddenRewards);
        foreach (var heading in off.Groups.Select(g => g.Heading))
            Assert.Equal(
                off.Groups.Single(g => g.Heading == heading).Rows
                    .Select(r => r.Row.Id).Order(StringComparer.Ordinal),
                on.Groups.Single(g => g.Heading == heading).Rows
                    .Select(r => r.Row.Id).Order(StringComparer.Ordinal));
    }

    // ---- done bar 6 -------------------------------------------------------------------

    /// <summary>S4.4 / S23 AC 1: the class-oriented view a player already knows is what they
    /// see after an upgrade. Both Sky arrangement settings default off, and this asserts the
    /// pair rather than the one this slice added — a default that moved would be invisible on
    /// the screen that cared.</summary>
    [Fact]
    public void TheClassOrientedViewIsStillWhatAPlayerGetsWithoutAsking()
    {
        var fresh = new AppSettings();

        Assert.False(fresh.SkyClosestToCompletion);
        Assert.False(fresh.SkyGroupByIsland);
    }
}

/// <summary>
/// The other half of DRA-218, and the half a Core-only suite cannot see: something has to
/// FILL <see cref="QuestChecklistRow.BlockedBy"/>, or every assertion in
/// <see cref="SkyClosestToCompletionTests"/> is about a field that is empty on every row the
/// app ever draws (trap 34 — a guard aimed at nothing is green).
///
/// <para>The prerequisite graph is real and curated: <c>GuideObjective.PrerequisiteObjectiveIds</c>,
/// which <c>GuideCatalog.Validate</c> already refuses to let dangle. This is the guide the
/// shipped Warrior seed is shaped like, run through the real projection against a real
/// ledger.</para>
/// </summary>
public sealed class SkyBlockerProjectionTests : IDisposable
{
    private const string Dranak = "dranak_freeport";
    private const string ClassName = "Warrior";
    private const string Reward = "Runed Wind Amulet";
    private const string RuneTitle = "Collect Wind Rune Azia";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"dra218-blocker-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static string RewardKey => QuestChecklistLayout.RewardKey(ClassName, Reward);

    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.SkyQuestChecklist.AddRange(
        [
            new SkyQuestChecklistItem
            {
                Id = "sky-198", ClassName = ClassName, Reward = Reward,
                Npc = "Torgon Blademaster", QuestItem = "Stone Amulet",
                Source = "Isle 4: Keeper of Souls",
            },
            new SkyQuestChecklistItem
            {
                Id = "sky-199", ClassName = ClassName, Reward = Reward,
                Npc = "Torgon Blademaster", QuestItem = "Wind Rune Azia",
                Source = "Trash mobs",
            },
        ]);
        return s;
    }

    private static GuideObjective Rune() => new()
    {
        Id = "wind-rune-azia", Order = 1, ObjectiveType = "Farm",
        Title = RuneTitle,
        ShortInstruction = "Loot a Wind Rune Azia from Plane of Sky trash.",
        ItemNames = ["Wind Rune Azia"],
        Authoring = GuideAuthoring.Authored,
        Who = "Plane of Sky trash", Where = "Plane of Sky.", What = "Loot Wind Rune Azia (1).",
    };

    private static GuideCatalog Catalog() => new()
    {
        Guides =
        [
            new Guide
            {
                Id = "pos-warrior-runed-wind-amulet",
                Name = "Runed Wind Amulet - Warrior, Plane of Sky",
                GuideType = GuideType.PlaneOfSkyQuest,
                QuestName = "Warrior Sky: Runed Wind Amulet",
                ZoneNames = ["Plane of Sky"],
                ApplicableClasses = [ClassName],
                Stages =
                [
                    new GuideStage
                    {
                        Id = "isle-4", Name = "Isle 4: Keeper of Souls", Order = 1,
                        Objectives =
                        [
                            new GuideObjective
                            {
                                Id = "stone-amulet", Order = 1, ObjectiveType = "Loot",
                                Title = "Loot the Stone Amulet from the Keeper of Souls",
                                ShortInstruction = "Kill the Keeper of Souls and loot the Stone Amulet.",
                                Who = "Keeper of Souls", Where = "Plane of Sky - Isle 4.",
                                What = "Loot Stone Amulet (1).",
                                ItemNames = ["Stone Amulet"],
                                Authoring = GuideAuthoring.Authored,
                            },
                        ],
                    },
                    new GuideStage
                    {
                        Id = "wind-runes", Name = "The wind rune", Order = 2,
                        Objectives = [Rune()],
                    },
                    new GuideStage
                    {
                        Id = "turn-in", Name = "Turn in to Torgon Blademaster", Order = 3,
                        Objectives =
                        [
                            new GuideObjective
                            {
                                Id = "turn-in-runed-wind-amulet", Order = 1, ObjectiveType = "TurnIn",
                                Title = "Hand in the Stone Amulet and Wind Rune Azia",
                                ShortInstruction = "Give Torgon Blademaster both pieces.",
                                Who = "Torgon Blademaster", Where = "Plane of Sky.",
                                What = "Hand over both.",
                                PrerequisiteObjectiveIds = ["stone-amulet", "wind-rune-azia"],
                                RewardKey = RewardKey,
                                Authoring = GuideAuthoring.Authored,
                            },
                        ],
                    },
                ],
            },
        ],
    };

    private QuestChecklistGroup Guided(AppSettings settings, QuestLedgerStore ledger) =>
        GuideChecklistProjection
            .Apply(QuestChecklistLayout.Sky(settings.SkyQuestChecklist, settings.SkyQuestCompleted),
                Catalog(), settings, ledger, Dranak)
            .Single(g => g.CompletionKey == RewardKey);

    /// <summary><b>The committed negative, and it is the one that matters</b> (trap 78: a
    /// detector whose list is empty reports clean). Nothing is skipped, so no row carries a
    /// blocker and the group is not blocked — even though the turn-in's prerequisites are
    /// both outstanding. Waiting on work you are going to do is not being blocked, and if
    /// this went green the "blocked" word would be on every unfinished quest in the app.</summary>
    [Fact]
    public void AnOrdinaryUnfinishedQuestCarriesNoBlockerAtAll()
    {
        var group = Guided(Settings(), Store());

        Assert.Equal(3, group.Rows.Count);
        Assert.All(group.Rows, r => Assert.Equal("", r.BlockedBy));
        Assert.False(group.Blocked);
        Assert.Equal(3, group.Remaining);
        Assert.Null(QuestChecklistLayout.BlockedNote(group));
    }

    /// <summary>Strike the wind rune out and the TURN-IN row — the one objective in the
    /// shipped catalog that carries prerequisites at all — starts naming it. The name is the
    /// step's <c>Title</c> through <c>GuidePresentation.StepName</c>, because a
    /// cross-reference is a NAME and not an instruction; the row five lines above already
    /// says "after: Collect Wind Rune Azia" in those same words.</summary>
    [Fact]
    public void SkippingAPrerequisiteStampsTheTurnInRowWithItsNAME()
    {
        var settings = Settings();
        var ledger = Store();
        GuideProgressRouter.SetSkipped(
            ledger, Dranak, "pos-warrior-runed-wind-amulet", Rune(), true);

        var group = Guided(settings, ledger);

        var turnIn = group.Rows.Single(r => r.IsTurnIn);
        Assert.Equal(RuneTitle, turnIn.BlockedBy);
        // And ONLY the turn-in: the two collect steps carry no prerequisites, so a blocker on
        // either would mean the stamp is being applied by position rather than by the graph.
        Assert.All(group.Rows.Where(r => !r.IsTurnIn), r => Assert.Equal("", r.BlockedBy));
    }

    /// <summary>The whole chain, end to end, on the ONE reachable shape the acceptance
    /// criterion describes (S23 AC 8): a struck-out prerequisite turns into a row stamp, into
    /// a blocked group, into a sentence — and into the lens ranking it behind a quest with
    /// MORE work left. The Stone Amulet is still open, so this quest still has something to
    /// go and do; doing it will not finish the quest.</summary>
    [Fact]
    public void ASkippedPrerequisiteReachesTheLensAndSinksTheQuestBehindABusierOne()
    {
        var settings = Settings();
        var ledger = Store();
        GuideProgressRouter.SetSkipped(
            ledger, Dranak, "pos-warrior-runed-wind-amulet", Rune(), true);
        var blocked = Guided(settings, ledger);

        Assert.True(blocked.Blocked);
        Assert.Equal("blocked", blocked.Note);
        Assert.Equal(
            QuestChecklistLayout.BlockedLead + RuneTitle + ".",
            QuestChecklistLayout.BlockedNote(blocked));

        // Two steps left — the Stone Amulet and the hand-in — against a quest with three.
        Assert.Equal(2, blocked.Remaining);
        var busier = new QuestChecklistGroup("Cleric", "Wind Rune Fana",
            [
                new QuestChecklistRow("c1", "Cleric", "one", "", false, false),
                new QuestChecklistRow("c2", "Cleric", "two", "", false, false),
                new QuestChecklistRow("c3", "Cleric", "three", "", false, false),
            ]);
        Assert.Equal(3, busier.Remaining);

        var ranked = QuestChecklistLayout.ClosestToCompletion([blocked, busier]);

        Assert.Equal("Wind Rune Fana", ranked[0].Title);
        Assert.Equal(Reward, ranked[1].Title);
    }

    /// <summary>Taking the strike back closes it again, with no trace left on the row. A
    /// blocker that survived its own cause would be the stale-memo shape (trap 38) on a
    /// sentence that tells a player their quest is stuck.</summary>
    [Fact]
    public void TakingTheSkipBackClearsTheBlockerCompletely()
    {
        var settings = Settings();
        var ledger = Store();
        GuideProgressRouter.SetSkipped(
            ledger, Dranak, "pos-warrior-runed-wind-amulet", Rune(), true);
        Assert.True(Guided(settings, ledger).Blocked);

        GuideProgressRouter.SetSkipped(
            ledger, Dranak, "pos-warrior-runed-wind-amulet", Rune(), false);

        var group = Guided(settings, ledger);
        Assert.False(group.Blocked);
        Assert.All(group.Rows, r => Assert.Equal("", r.BlockedBy));
        Assert.Equal("in progress", group.Note is null ? "in progress" : group.Note);
    }

    /// <summary>The active-step card and the heading are ONE rule read twice (trap 4). The
    /// card has said "the hand-in waits on a step you skipped" since #491; the heading's
    /// sentence is the same fact from the same producer, and they name the same step.
    /// Asserted together, because two sentences about one quest that could drift are exactly
    /// what the shared producer exists to prevent.</summary>
    [Fact]
    public void TheCardAndTheHeadingNameTheSameSkippedStep()
    {
        var settings = Settings();
        var ledger = Store();
        GuideProgressRouter.SetSkipped(
            ledger, Dranak, "pos-warrior-runed-wind-amulet", Rune(), true);

        var group = Guided(settings, ledger);
        var note = QuestChecklistLayout.BlockedNote(group);

        Assert.NotNull(note);
        Assert.Contains(RuneTitle, note);
        // The card is only drawn once nothing else is offerable — here the Stone Amulet is
        // still open, so the card names THAT and the heading carries the blocker. Both true,
        // neither contradicting the other, which is the pairing this asserts.
        Assert.NotNull(group.GuideCard);
        Assert.Contains("Stone Amulet", group.GuideCard!.Instruction);
    }
}
