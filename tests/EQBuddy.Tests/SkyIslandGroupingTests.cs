using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// Grouping Plane of Sky steps by island (David, 2026-08-23, from a Reddit ask): *"list the
/// unknown location steps as we do today, but where we know a step is on a specific island,
/// list those steps by it… sorted by island numerically."*
///
/// The grouping lives in <see cref="QuestChecklistLayout.Sky"/> rather than in a window,
/// because three surfaces draw this checklist — both desktops and EQBuddy Mobile — and #210
/// is what happens when one of them decides for itself. Each surface draws a heading when
/// <see cref="QuestChecklistRow.IslandHeading"/> changes and owns no grouping logic at all.
/// </summary>
public class SkyIslandGroupingTests
{
    private static SkyQuestChecklistItem Step(string id, string item, string source) => new()
    {
        Id = id,
        ClassName = "Warrior",
        Npc = "Torgon Blademaster",
        Reward = "Belt of the Four Winds",
        QuestItem = item,
        Source = source,
    };

    private const string ThreeIsles =
        "Isle eight: the Hand of Veeshan; Isle four: Overseer of Air; Isle 1.5: Noble Dojorn";

    private static readonly SkyQuestChecklistItem[] Reward =
    [
        Step("a", "Wind Rune Fana", "Trash mobs"),
        Step("b", "Wind Tablet", "Isle 6: Bazzt Zzzt"),
        Step("c", "Efreeti Belt", ThreeIsles),
        Step("d", "Azure Ring", "Isle 1.5: Noble Dojorn"),
    ];

    private static IReadOnlyList<QuestChecklistRow> Rows(bool repeat = false) =>
        QuestChecklistLayout.Sky(Reward, null, repeat).Single().Rows;

    /// <summary>**Numerically**, which is the word that needed saying: Sky has an island
    /// 1.5, so 1.5 must land before 6 rather than wherever a string sort puts it. Then the
    /// several-island steps, then the ones with no island named — the unlocated steps close
    /// the list rather than interrupting it.</summary>
    [Fact]
    public void IslandsAreOrderedNumericallyWithTheUnlocatedLast()
    {
        Assert.Equal(
            ["Island 1.5", "Island 6", "Islands 1.5 · 4 · 8", SkyIslands.AnywhereHeading],
            Rows().Select(r => r.IslandHeading));
    }

    /// <summary>Off (the default): a step on three islands appears ONCE. It is not filed with
    /// the unlocated steps — we know all three places — and it does not join a numbered
    /// group, because by the ask's own wording it is not on *a specific island*.</summary>
    [Fact]
    public void ByDefaultAMultiIslandStepIsListedOnceUnderSeveralIslands()
    {
        var rows = Rows();

        Assert.Equal(4, rows.Count);
        var efreeti = Assert.Single(rows, r => r.Id == "c");
        // NAMED, not just "Several islands" (David, 2026-08-23, after seeing the first
        // build): a player on Island 4 can tell at a glance that this is reachable.
        Assert.Equal("Islands 1.5 · 4 · 8", efreeti.IslandHeading);
        // And the prose stays, because the heading says WHERE while the prose says which
        // mob on each — a mapping that exists nowhere else.
        Assert.Contains("Overseer of Air", efreeti.Detail);
    }

    /// <summary>On: the same step appears under every island it drops on, so "what can I do
    /// on Island 4 today" is answered completely. Island 4 exists only in this mode — nothing
    /// else in this reward is on it.</summary>
    [Fact]
    public void RepeatModePutsTheStepUnderEveryIslandItNames()
    {
        var rows = Rows(repeat: true);

        Assert.Equal(3, rows.Count(r => r.Id == "c"));
        Assert.Equal(
            ["Island 1.5", "Island 1.5", "Island 4", "Island 6", "Island 8", SkyIslands.AnywhereHeading],
            rows.Select(r => r.IslandHeading));
        // And "Several islands" is not drawn at all — every one of them has a home.
        Assert.DoesNotContain(rows, r => r.IslandHeading.StartsWith("Islands "));
    }

    /// <summary>
    /// **The trap this feature sets, and the reason the counter changed with it.**
    ///
    /// In repeat mode one step renders three times. Progress counted ROWS, so a four-step
    /// reward would have reported itself as six — a checklist telling the player the wrong
    /// number of pieces, on the surface whose entire job is to say how far along they are,
    /// and only in the mode they opted into. Nothing about the rendering code would look
    /// wrong; the rows really are all there.
    ///
    /// Counts are over DISTINCT steps now, so the same reward reads the same either way.
    /// </summary>
    [Fact]
    public void RepeatingAStepNeverChangesTheScore()
    {
        var acquired = Reward.Select(i => new SkyQuestChecklistItem
        {
            Id = i.Id, ClassName = i.ClassName, Npc = i.Npc, Reward = i.Reward,
            QuestItem = i.QuestItem, Source = i.Source,
            Acquired = i.Id is "c",       // the multi-island one is the one in hand
        }).ToList();

        var once = QuestChecklistLayout.Sky(acquired, null, repeatMultiIsland: false).Single();
        var thrice = QuestChecklistLayout.Sky(acquired, null, repeatMultiIsland: true).Single();

        Assert.Equal(4, once.Total);
        Assert.Equal(4, thrice.Total);      // NOT 6
        Assert.Equal(1, once.Done);
        Assert.Equal(1, thrice.Done);       // NOT 3
        Assert.Equal(once.Progress, thrice.Progress);
        // The rows really did multiply — the score simply does not follow them.
        Assert.Equal(4, once.Rows.Count);
        Assert.Equal(6, thrice.Rows.Count);
    }

    /// <summary>Ready-to-turn-in survives repetition too: every distinct step in hand still
    /// means the reward is ready, and a repeated row cannot make it look unfinished.</summary>
    [Fact]
    public void ReadyToTurnInIsUnaffectedByRepetition()
    {
        var all = Reward.Select(i => new SkyQuestChecklistItem
        {
            Id = i.Id, ClassName = i.ClassName, Npc = i.Npc, Reward = i.Reward,
            QuestItem = i.QuestItem, Source = i.Source, Acquired = true,
        }).ToList();

        Assert.True(QuestChecklistLayout.Sky(all, null, false).Single().ReadyToTurnIn);
        Assert.True(QuestChecklistLayout.Sky(all, null, true).Single().ReadyToTurnIn);
    }

    /// <summary>**Two different island SETS are two different headings, and they sit apart**
    /// (David, 2026-08-23: *"for the 'Several Islands' ones, please list which. IE: 1, 3,
    /// and 7"*).
    ///
    /// Before the heading named its islands, every multi-island step shared one "Several
    /// islands" bucket — which was only ever right because the heading could not tell them
    /// apart. Naming them makes that bucket wrong, so the ordering had to gain a tiebreak on
    /// the set. This is the assertion that would catch it being dropped: without it the two
    /// steps below interleave under whichever heading came first, and the list quietly claims
    /// one is on islands it is not.</summary>
    [Fact]
    public void DifferentIslandSetsGetDifferentHeadingsAndDoNotInterleave()
    {
        var mixed = new[]
        {
            Step("p", "Alpha", ThreeIsles),                                   // 1.5, 4, 8
            Step("q", "Bravo", "Isle 2: Protector of Sky; Isle 7: Sister of the Spire"),
            Step("r", "Charlie", ThreeIsles),                                 // 1.5, 4, 8
        };

        var rows = QuestChecklistLayout.Sky(mixed, null, repeatMultiIsland: false).Single().Rows;

        Assert.Equal(
            ["Islands 1.5 · 4 · 8", "Islands 1.5 · 4 · 8", "Islands 2 · 7"],
            rows.Select(r => r.IslandHeading));
        // The two members of the first set are adjacent, which is what "grouped" means.
        Assert.Equal(["Alpha", "Charlie", "Bravo"], rows.Select(r => r.Title));
    }

    /// <summary>**A middle dot, not commas** (David, 2026-08-23). He read the first cut,
    /// "Islands 1.5, 4, and 8", back as FOUR islands — "1, 5, 4, 8" — because the
    /// half-island puts a decimal point inside a comma-separated list. He had chosen the
    /// comma format himself an hour before, which is the argument: if its author misreads
    /// it, a player who does not yet know there is a half-island has no chance. "·" is
    /// already the separator the rows beneath these headings use.</summary>
    [Theory]
    [InlineData(new[] { 4.0, 8.0 }, "Islands 4 · 8")]
    [InlineData(new[] { 1.5, 4.0, 8.0 }, "Islands 1.5 · 4 · 8")]
    [InlineData(new[] { 1.0, 3.0, 7.0 }, "Islands 1 · 3 · 7")]
    // One is not "several" at all — it is that island, spelled the ordinary way, so a caller
    // that loses track of how many it has cannot produce a heading that reads as a list.
    [InlineData(new[] { 6.0 }, "Island 6")]
    public void TheHeadingNamesTheIslands(double[] islands, string expected) =>
        Assert.Equal(expected, SkyIslands.SeveralHeading(islands));

    /// <summary>Epic rows carry no island heading at all, so an Epic surface draws none.
    /// The negative that keeps the field from leaking into the other checklist.</summary>
    [Fact]
    public void EpicRowsHaveNoIslandHeading()
    {
        var epic = QuestChecklistLayout.Epic(
        [
            new EpicQuestChecklistItem
            {
                Id = "e1", ClassName = "Warrior", Section = "Step 1",
                QuestItem = "Thick Boned Hammer", Source = "Isle 6: Bazzt Zzzt",
            },
        ]);

        Assert.All(epic.SelectMany(g => g.Rows), r => Assert.Equal("", r.IslandHeading));
    }
}
