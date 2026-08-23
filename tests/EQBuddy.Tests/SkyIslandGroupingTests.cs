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
            ["Island 1.5", "Island 6", SkyIslands.SeveralHeading, SkyIslands.AnywhereHeading],
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
        Assert.Equal(SkyIslands.SeveralHeading, efreeti.IslandHeading);
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
        Assert.DoesNotContain(SkyIslands.SeveralHeading, rows.Select(r => r.IslandHeading));
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
