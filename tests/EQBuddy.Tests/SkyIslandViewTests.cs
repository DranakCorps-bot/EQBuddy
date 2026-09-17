using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// The ISLAND view (DRA-164, the Founder's ask of 2026-09-17): *"multi-select which classes I
/// am, then group checklist by island — everything to collect on island N before moving to
/// next"*, warrior/monk/druid.
///
/// <para>The class view is KEPT and is still the default; this is a second arrangement of the
/// SAME rows. That is the claim most of this file is about, and it is asserted by REFERENCE
/// where it can be: an island row must be the very object the class view would have drawn, or
/// the tick a player presses is wired to a copy and the two views can drift (signed plan P1,
/// trap 4). "Looks the same" and "is the same object" are different promises.</para>
/// </summary>
public class SkyIslandViewTests
{
    private static SkyQuestChecklistItem Step(
        string id, string className, string reward, string item, string source,
        bool acquired = false) => new()
    {
        Id = id,
        ClassName = className,
        Npc = "Torgon Blademaster",
        Reward = reward,
        QuestItem = item,
        Source = source,
        Acquired = acquired,
    };

    private const string ThreeIsles =
        "Isle eight: the Hand of Veeshan; Isle four: Overseer of Air; Isle 1.5: Noble Dojorn";

    /// <summary>The Founder's own example: three classes, and the question is what is on each
    /// island for all of them at once.</summary>
    private static readonly SkyQuestChecklistItem[] WarriorMonkDruid =
    [
        Step("w1", "Warrior", "Belt of the Four Winds", "Wind Tablet", "Isle 6: Bazzt Zzzt"),
        Step("w2", "Warrior", "Belt of the Four Winds", "Azure Ring", "Isle 1.5: Noble Dojorn"),
        Step("w3", "Warrior", "Belt of the Four Winds", "Wind Rune Fana", "Trash mobs"),
        Step("m1", "Monk", "Celestial Fists", "Jade Bracelet", "Isle 6: Bazzt Zzzt"),
        Step("m2", "Monk", "Celestial Fists", "Efreeti Belt", ThreeIsles),
        Step("d1", "Druid", "Nature Walkers Scimitar", "Spiroc Feather", "Isle 5: The Spiroc Lord"),
    ];

    private static IReadOnlyList<QuestChecklistGroup> Groups(bool repeat = false) =>
        QuestChecklistLayout.Sky(WarriorMonkDruid, null, repeat);

    /// <summary>**The ask, answered.** Everything to collect on Island 6 is one group, and it
    /// holds the Warrior's step and the Monk's — the island is the unit now, not the class and
    /// not the reward.</summary>
    [Fact]
    public void OneIslandIsOneGroupAcrossEveryClassThePlayerPicked()
    {
        var island6 = QuestChecklistLayout.SkyByIsland(Groups()).Groups
            .Single(g => g.Heading == "Island 6");

        Assert.Equal(["Monk · Celestial Fists", "Warrior · Belt of the Four Winds"],
            island6.Rows.Select(r => r.Label));
        Assert.Equal(["Jade Bracelet", "Wind Tablet"], island6.Rows.Select(r => r.Row.Title));
    }

    /// <summary>Islands ascend NUMERICALLY (1.5 before 5, which is the whole reason islands are
    /// doubles), then the multi-island sets, then the rows that name no island. Empty islands
    /// are NOT emitted: the list is the data's, never an invented 1–8 scaffold that would
    /// promise work nobody has recorded.</summary>
    [Fact]
    public void IslandsAscendThenTheSetsThenTheUnlocated()
    {
        Assert.Equal(
            ["Island 1.5", "Island 5", "Island 6", "Islands 1.5 · 4 · 8",
             SkyIslands.AnywhereHeading],
            QuestChecklistLayout.SkyByIsland(Groups()).Groups.Select(g => g.Heading));
    }

    /// <summary>
    /// **A rearrangement, proved by reference.** Every island row IS the class view's row
    /// object — so it carries the same id, the same tick routing, the same
    /// <c>LedgerItemName</c> refusal, and a surface wires the box it already knows how to wire.
    ///
    /// <para>Value equality would pass here on a projection that rebuilt every row, which is
    /// the one thing P1 forbids.</para>
    /// </summary>
    [Fact]
    public void EveryIslandRowIsTheSameObjectTheClassViewWouldDraw()
    {
        var groups = Groups();
        var classViewRows = groups.SelectMany(g => g.Rows).ToList();

        foreach (var row in QuestChecklistLayout.SkyByIsland(groups).Groups.SelectMany(g => g.Rows))
            Assert.Contains(classViewRows, original => ReferenceEquals(original, row.Row));
    }

    /// <summary>The reward travels WITH the row, because its heading is no longer above it.
    /// Worded once, here, in the same shape as <c>QuestChecklistGroup.Heading</c> — three
    /// surfaces draw this and none of them spells it itself (#184).</summary>
    [Fact]
    public void ARowSaysItsClassAndItsRewardBecauseNothingAboveItDoes()
    {
        var row = QuestChecklistLayout.SkyByIsland(Groups()).Groups
            .Single(g => g.Heading == "Island 5").Rows.Single();

        Assert.Equal("Druid · Nature Walkers Scimitar", row.Label);
        Assert.Equal("Nature Walkers Scimitar", row.Reward);
        Assert.Equal("Druid", row.Row.ClassName);
    }

    // ----- P5: what it excludes, it counts out loud (trap 50) -----------------------------

    private static QuestChecklistRow Row(string id, string title, string islandKey,
        string heading, bool turnIn = false, bool acquired = false) =>
        new(id, "Warrior", title, "detail", acquired, false,
            IslandHeading: heading, IsTurnIn: turnIn, IslandKey: islandKey);

    private static QuestChecklistGroup Group(bool completed, params QuestChecklistRow[] rows) =>
        new("Warrior", "Belt of the Four Winds", rows,
            CompletionKey: QuestChecklistLayout.RewardKey("Warrior", "Belt of the Four Winds"),
            Completed: completed);

    /// <summary>**A hand-in is not island work.** The Ready band above the lens already answers
    /// "what can I turn in right now", cross-class, and stays on screen in both modes — one
    /// owner per question. The view says where the answer went rather than silently dropping
    /// the row.</summary>
    [Fact]
    public void AHandInIsExcludedAndTheViewSaysWhereItWent()
    {
        var layout = QuestChecklistLayout.SkyByIsland(
        [
            Group(false,
                Row("a", "Wind Tablet", SkyIslands.SetKey([6]), "Island 6"),
                Row("t", "Turn in to Torgon Blademaster", "", "Turn in to Torgon Blademaster",
                    turnIn: true)),
        ]);

        Assert.Equal(["Island 6"], layout.Groups.Select(g => g.Heading));
        Assert.Equal(1, layout.HiddenTurnIns);
        Assert.Contains("Ready band", layout.TurnInNote);
    }

    /// <summary>**A turned-in reward's rows are all force-acquired** by
    /// <c>MarkRewardTurnedIn</c>, so leaving them in would render every island as mostly-done
    /// noise. Hidden, counted, and the sentence names where they still are.</summary>
    [Fact]
    public void ATurnedInRewardIsHiddenAndTheCountSaysSo()
    {
        var layout = QuestChecklistLayout.SkyByIsland(
        [
            Group(true, Row("done", "Wind Tablet", SkyIslands.SetKey([6]), "Island 6", acquired: true)),
            Group(false, Row("open", "Azure Ring", SkyIslands.SetKey([1.5]), "Island 1.5")),
        ]);

        Assert.Equal(["Island 1.5"], layout.Groups.Select(g => g.Heading));
        Assert.Equal(1, layout.HiddenRewards);
        Assert.Contains("1 turned-in reward is not listed here", layout.HiddenNote);
        Assert.Contains("Class view still has them", layout.HiddenNote);
    }

    /// <summary>
    /// **Prove-fail for both notes: a note that fires when nothing was hidden is not a
    /// disclosure, it is furniture.**
    ///
    /// <para>Trap 50 asks a surviving cap to SAY so; the other half of that is that a cap which
    /// did not fire has nothing to confess. Without this row both sentences could be constants
    /// and the two tests above would still pass.</para>
    /// </summary>
    [Fact]
    public void NeitherNoteIsSaidWhenNothingWasHidden()
    {
        var layout = QuestChecklistLayout.SkyByIsland(Groups());

        Assert.Equal(0, layout.HiddenRewards);
        Assert.Equal(0, layout.HiddenTurnIns);
        Assert.Equal("", layout.HiddenNote);
        Assert.Equal("", layout.TurnInNote);
    }

    /// <summary>Plural and singular are both real states of this sentence, and a player reading
    /// "2 turned-in reward is not listed" learns that nobody read the line.</summary>
    [Fact]
    public void TheHiddenSentenceCountsInEnglish()
    {
        var layout = QuestChecklistLayout.SkyByIsland(
        [
            Group(true, Row("d1", "Wind Tablet", SkyIslands.SetKey([6]), "Island 6", acquired: true)),
            Group(true, Row("d2", "Azure Ring", SkyIslands.SetKey([1.5]), "Island 1.5", acquired: true)),
        ]);

        Assert.Equal(2, layout.HiddenRewards);
        Assert.Contains("2 turned-in rewards are not listed here", layout.HiddenNote);
        Assert.Empty(layout.Groups);
    }

    // ----- The setting that got its job back ---------------------------------------------

    /// <summary>ON: a step naming three islands appears under all three, so "what can I do on
    /// Island 4" is answered completely. This is the player's existing
    /// <c>SkyStepsUnderEveryIsland</c> choice, read by a second surface — and with every Sky
    /// reward now guided it is the reader that makes the setting matter again.</summary>
    [Fact]
    public void RepeatModePutsAMultiIslandStepUnderEveryIslandItNames()
    {
        var headings = QuestChecklistLayout.SkyByIsland(Groups(repeat: true), repeatMultiIsland: true)
            .Groups
            .Where(g => g.Rows.Any(r => r.Row.Title == "Efreeti Belt"))
            .Select(g => g.Heading);

        Assert.Equal(["Island 1.5", "Island 4", "Island 8"], headings);
    }

    /// <summary>
    /// **Repeating a step never changes the score** — the <c>QuestChecklistGroup.Done</c>
    /// lesson, in island shape.
    ///
    /// <para>It cost a "3/12" on a six-piece reward the first time, silently, on the surface
    /// whose entire job is saying how far along you are. Counting RENDERED rows would do it
    /// again here: the Monk's Efreeti Belt is drawn three times in repeat mode, so an island
    /// total built from <c>Rows.Count</c> would report four Monk steps where there are two.</para>
    /// </summary>
    [Fact]
    public void RepeatingAStepNeverChangesTheScore()
    {
        var flat = QuestChecklistLayout.SkyByIsland(Groups()).Groups;
        var repeated = QuestChecklistLayout.SkyByIsland(Groups(repeat: true), repeatMultiIsland: true)
            .Groups;

        // Same distinct steps either way — six of them, one per fixture item.
        Assert.Equal(6, flat.Sum(g => g.Total));
        Assert.Equal(
            6,
            repeated.SelectMany(g => g.Rows).Select(r => r.Row.Id).Distinct(StringComparer.Ordinal).Count());

        // And the island the step is drawn on counts it ONCE, not once per rendering.
        var isle4 = repeated.Single(g => g.Heading == "Island 4");
        Assert.Equal(1, isle4.Total);
        Assert.Equal(0, isle4.Done);
    }

    /// <summary>Two different island SETS are two different headings and do not interleave —
    /// they were only ever one bucket because the heading could not tell them apart (David,
    /// 2026-08-23). The island view inherits that, because it reads the SET off the row rather
    /// than the heading a human reads.</summary>
    [Fact]
    public void DifferentIslandSetsStayApart()
    {
        var layout = QuestChecklistLayout.SkyByIsland(
        [
            Group(false,
                Row("a", "Efreeti Belt", SkyIslands.SetKey([1.5, 4, 8]), "Islands 1.5 · 4 · 8"),
                Row("b", "Storm Sapphire", SkyIslands.SetKey([4, 8]), "Islands 4 · 8")),
        ]);

        Assert.Equal(["Islands 1.5 · 4 · 8", "Islands 4 · 8"],
            layout.Groups.Select(g => g.Heading));
        Assert.All(layout.Groups, g => Assert.Single(g.Rows));
    }

    /// <summary>**An unlocated row keeps the heading it already has, verbatim.** The guide
    /// catalog's stage names — "The wind rune", "Not placed" — are already the honest label for
    /// a step nobody located, and inventing a location word for them is trap 73 with the
    /// grouping switched on. They are grouped, not merged.</summary>
    [Fact]
    public void UnlocatedRowsKeepTheirOwnHeadingsRatherThanBeingGivenOne()
    {
        var layout = QuestChecklistLayout.SkyByIsland(
        [
            Group(false,
                Row("r", "Wind Rune Fana", "", "The wind rune"),
                Row("n", "Something", "", "Not placed"),
                Row("s", "Wind Rune Azia", "", "The wind rune")),
        ]);

        Assert.Equal(["Not placed", "The wind rune"], layout.Groups.Select(g => g.Heading));
        Assert.Equal(2, layout.Groups.Single(g => g.Heading == "The wind rune").Total);
    }

    /// <summary>The score is the ACQUIRED count, and it is per island — which is what makes
    /// "everything to collect on island N" checkable at a glance.</summary>
    [Fact]
    public void AnIslandScoresItsOwnSteps()
    {
        var layout = QuestChecklistLayout.SkyByIsland(Groups());
        var anywhere = layout.Groups.Single(g => g.Heading == SkyIslands.AnywhereHeading);

        Assert.Equal(1, anywhere.Total);
        Assert.Equal(0, anywhere.Done);

        var withOneHeld = QuestChecklistLayout.SkyByIsland(QuestChecklistLayout.Sky(
        [
            Step("w1", "Warrior", "Belt of the Four Winds", "Wind Tablet", "Isle 6: Bazzt Zzzt",
                acquired: true),
            Step("m1", "Monk", "Celestial Fists", "Jade Bracelet", "Isle 6: Bazzt Zzzt"),
        ], null));

        var island6 = withOneHeld.Groups.Single();
        Assert.Equal(2, island6.Total);
        Assert.Equal(1, island6.Done);
    }

    /// <summary>Nothing in, nothing out — and no note either. An empty checklist is not an
    /// occasion for a sentence about what was hidden.</summary>
    [Fact]
    public void AnEmptyChecklistIsAnEmptyIslandView()
    {
        var layout = QuestChecklistLayout.SkyByIsland([]);

        Assert.Empty(layout.Groups);
        Assert.Equal("", layout.HiddenNote);
        Assert.Equal("", layout.TurnInNote);
    }
}
