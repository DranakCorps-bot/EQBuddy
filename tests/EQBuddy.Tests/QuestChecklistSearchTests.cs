using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// "Who wants this drop?" — the item-grouped checklist search (#108, liminalwarmth).
///
/// The ask was "clicking through each class scrolling for a drop is tedious", and 1.69.0
/// answered it: matching rows from EVERY class at once, grouped by item, ignoring the
/// state filter. The Gate 2 rebuild kept a search box and lost both halves — the query
/// narrowed rows inside the per-class reward sections, and it ran after the class picker,
/// the class lens and the state lens, so a match outside the current filters was simply
/// not there.
///
/// That is the trap-20 shape again: the data survived the move, the capability did not,
/// and no compiler, test or ratchet could see it. These tests are what make it stay
/// fixed, and the important one is <see cref="One_item_several_classes_want_is_ONE_row"/>
/// — a search that returns three sections for one drop has not answered the question.
/// </summary>
public class QuestChecklistSearchTests
{
    private static SkyQuestChecklistItem Item(
        string id, string cls, string reward, string item, bool acquired = false,
        string source = "Isle 3") => new()
    {
        Id = id, ClassName = cls, Npc = "Cilin Spellsinger", Reward = reward,
        QuestItem = item, Source = source, Acquired = acquired,
    };

    /// <summary>"Wind Rune Azia" is wanted by three classes across four rewards — the
    /// shared-drop case the whole feature exists for. The rest is noise to search past.</summary>
    private static SkyQuestChecklistItem[] Corpus =>
    [
        Item("b1", "Bard", "Mask of Song", "Wind Rune Azia"),
        Item("b2", "Bard", "Mask of Song", "Woolen Mask", acquired: true),
        Item("c1", "Cleric", "Mantle of the Heavens", "Wind Rune Azia", acquired: true),
        Item("r1", "Ranger", "Bow of Sky", "Wind Rune Azia"),
        Item("r2", "Ranger", "Cloak of Winds", "Wind Rune Azia"),
        Item("w1", "Wizard", "Robe of the Sky", "Diamond", source: "Isle 6"),
        Item("w2", "Wizard", "Robe of the Sky", "Sapphire"),
    ];

    private static IReadOnlyList<QuestChecklistGroup> Groups(params string[] completed) =>
        QuestChecklistLayout.Sky(Corpus, completed);

    private static IReadOnlyList<QuestChecklistLayout.ChecklistItemMatch> Search(
        string query, params string[] completed) =>
        QuestChecklistLayout.SearchByItem(Groups(completed), query);

    /// <summary>THE test. One drop, three classes, ONE row — not one section per class.</summary>
    [Fact]
    public void One_item_several_classes_want_is_ONE_row()
    {
        var match = Assert.Single(Search("Wind Rune Azia"));

        Assert.Equal("Wind Rune Azia", match.Title);
        Assert.Equal(4, match.Total);       // four checklist rows...
        Assert.Equal(3, match.Classes);     // ...across three classes
        Assert.Equal(["Bard", "Cleric", "Ranger", "Ranger"],
            match.Wanters.Select(w => w.ClassName));
        Assert.Equal(["Mask of Song", "Mantle of the Heavens", "Bow of Sky", "Cloak of Winds"],
            match.Wanters.Select(w => w.Reward));
    }

    /// <summary>Search crosses classes by construction: the caller hands over every group,
    /// and a cross-class question answered inside one class's filter is not answered.</summary>
    [Fact]
    public void Search_reaches_classes_the_player_does_not_have_picked()
    {
        // Only the Bard groups survive a class pick — searching those alone would report
        // one wanter and quietly mislead.
        var bardOnly = Groups().Where(g => g.ClassName == "Bard");
        Assert.Single(Assert.Single(
            QuestChecklistLayout.SearchByItem(bardOnly, "Wind Rune Azia")).Wanters);

        // Handed the full set — which is what the views must pass — it is the real answer.
        Assert.Equal(4, Assert.Single(Search("Wind Rune Azia")).Total);
    }

    /// <summary>A turned-in reward's rows still answer "who wants this" — they are how you
    /// learn the drop is wanted elsewhere too. The state rides along instead.</summary>
    [Fact]
    public void A_turned_in_reward_still_reports_its_rows_and_says_it_is_complete()
    {
        var completed = QuestChecklistLayout.RewardKey("Cleric", "Mantle of the Heavens");
        var match = Assert.Single(Search("Wind Rune Azia", completed));

        Assert.Equal(4, match.Total);
        var cleric = match.Wanters.Single(w => w.ClassName == "Cleric");
        Assert.True(cleric.RewardCompleted);
        Assert.DoesNotContain(match.Wanters.Where(w => w.ClassName != "Cleric"),
            w => w.RewardCompleted);
    }

    [Fact]
    public void Held_counts_the_rows_already_ticked()
    {
        var match = Assert.Single(Search("Wind Rune Azia"));
        Assert.Equal(1, match.Held);        // the Cleric's
        Assert.Equal(4, match.Total);
    }

    /// <summary>Most-wanted first: the item three classes are queuing for outranks the one
    /// only a wizard needs.</summary>
    [Fact]
    public void Items_order_by_how_many_classes_want_them_then_alphabetically()
    {
        // "Rune" hits one shared item; a bare "a" would hit everything, so search for the
        // two-word overlap plus a single-class item in one query is not possible — assert
        // the ordering on a query that reaches both instead.
        var results = QuestChecklistLayout.SearchByItem(Groups(), "n");
        var titles = results.Select(r => r.Title).ToList();

        Assert.Equal("Wind Rune Azia", titles[0]);   // 3 classes
        Assert.True(results[0].Classes >= results[1].Classes,
            "a more widely wanted item must not sort below a narrower one");
    }

    [Fact]
    public void A_reward_name_pulls_in_all_of_its_rows()
    {
        var results = QuestChecklistLayout.SearchByItem(Groups(), "Robe of the Sky");

        Assert.Equal(["Diamond", "Sapphire"],
            results.Select(r => r.Title).OrderBy(t => t));
        Assert.All(results, r => Assert.Equal("Robe of the Sky",
            Assert.Single(r.Wanters).Reward));
    }

    [Fact]
    public void The_drop_location_travels_with_the_row()
    {
        var diamond = Assert.Single(QuestChecklistLayout.SearchByItem(Groups(), "Diamond"));
        Assert.Contains("Isle 6", Assert.Single(diamond.Wanters).Detail);
    }

    /// <summary>Detail carries the NPC and drop location, and #108's box has always
    /// promised "any part of an item (or reward) name" — a player who knows only where it
    /// drops should still land on it.</summary>
    [Fact]
    public void Searching_the_drop_location_finds_the_items_that_drop_there()
    {
        var results = QuestChecklistLayout.SearchByItem(Groups(), "Isle 6");
        Assert.Equal("Diamond", Assert.Single(results).Title);
    }

    [Fact]
    public void Matching_is_case_insensitive_and_partial()
    {
        Assert.Equal("Wind Rune Azia",
            Assert.Single(QuestChecklistLayout.SearchByItem(Groups(), "wind rune")).Title);
    }

    /// <summary>A view uses this to decide whether to draw the item layout at all, so a
    /// blank box must mean "not searching" and never "nothing matches".</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_blank_query_returns_nothing_so_the_caller_draws_its_normal_layout(string? query)
    {
        Assert.Empty(QuestChecklistLayout.SearchByItem(Groups(), query));
    }

    [Fact]
    public void A_query_nothing_matches_returns_empty()
    {
        Assert.Empty(QuestChecklistLayout.SearchByItem(Groups(), "Shard of Nonexistence"));
    }

    /// <summary>The rows come back with their Ids, because the result is a rearrangement
    /// of the checklist and not a read-only report — every wanter must be tickable.</summary>
    [Fact]
    public void Every_wanter_carries_the_row_id_so_the_tick_still_works()
    {
        var match = Assert.Single(Search("Wind Rune Azia"));
        Assert.Equal(["b1", "c1", "r1", "r2"],
            match.Wanters.Select(w => w.RowId).OrderBy(i => i));
    }

    /// <summary>EQBuddy Mobile runs this search on the PHONE — over the catalog the PC
    /// shipped once, so a keystroke costs no round trip — which means the page mirrors
    /// the rule in JavaScript instead of calling into Core. A mirror drifts unless
    /// something holds it, and the wording is the half a player actually reads: the class
    /// chips and state filter stay on screen above the results and stop narrowing them,
    /// so the note is what keeps them from being controls that look live and do nothing.
    ///
    /// This is the cheap half of the parity rule. The expensive half — the grouping
    /// itself — is asserted against the projection by SurfaceParityTests.</summary>
    [Fact]
    public void The_mobile_page_carries_the_same_scope_note_word_for_word()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "EQBuddy.Companion", "Web", "index.html"));
        Assert.True(File.Exists(path), $"the shipped page moved: {path}");

        Assert.Contains(QuestChecklistLayout.SearchScopeNote, File.ReadAllText(path));
    }

    /// <summary>Epic checklists go through the same box, and an Epic section has no
    /// turn-in of its own — so nothing there may claim to be a completed reward.</summary>
    [Fact]
    public void Epic_rows_search_the_same_way()
    {
        var epic = QuestChecklistLayout.Epic(
        [
            new EpicQuestChecklistItem
            {
                Id = "e1", ClassName = "Bard", Section = "Part 1",
                QuestItem = "Wind Rune Azia", Acquired = false,
            },
        ]);

        var match = Assert.Single(QuestChecklistLayout.SearchByItem(epic, "Wind Rune"));
        Assert.Equal("Part 1", Assert.Single(match.Wanters).Reward);
        Assert.False(Assert.Single(match.Wanters).RewardCompleted);
    }
}
