using EQBuddy.Core;

namespace EQBuddy.Tests;

/// <summary>
/// DRA-47 (Delivery 2 N3, PR B): the Plane of Sky checklist is read off the Sky guides, and
/// <c>SkyQuestDefaults.cs</c> is gone. Before it went, a one-off test held all 222 derived rows
/// equal to the table on every field (id, class, NPC, reward, item, drop line) and the reward
/// order identical; these are what stay.
///
/// <para>The one that matters most is the first. A row id is not a label, it is the KEY every
/// player's tick is stored under — per character, in the quest ledger — so a renumbered id is a
/// tick that silently stops showing. <c>tests/fixtures/sky-checklist-ids.txt</c> is that
/// contract, frozen from the table the day it retired.</para>
/// </summary>
public class SkyChecklistRowsTests
{
    private static IEnumerable<GuideObjective> ShippedSkyPieces =>
        GuideCatalog.LoadCurated().Guides
            .Where(g => g.GuideType == GuideType.PlaneOfSkyQuest)
            .SelectMany(g => g.AllObjectives)
            .Where(o => o.RewardKey.Length == 0);

    [Fact]
    public void TheChecklistIdsAreTheOnesTicksWereWrittenUnder()
    {
        var contract = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "fixtures", "sky-checklist-ids.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToList();
        var derived = SkyChecklistRows.Items
            .Select(i => $"{i.Id}|{i.ClassName}|{i.Reward}|{i.QuestItem}")
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(222, contract.Count);
        Assert.Equal(contract, derived);
    }

    /// <summary>Every piece of every shipped Sky guide IS a checklist row: it carries the id its
    /// tick lives under and the drop line <see cref="SkyIslands"/> reads. A piece without an id
    /// would be a step whose box can never be ticked — <see cref="SkyChecklistRows.From"/> skips
    /// it rather than inventing one, so this is where that shows.</summary>
    [Fact]
    public void EveryShippedSkyPieceCarriesItsRowIdAndDropLine()
    {
        var pieces = ShippedSkyPieces.ToList();
        Assert.Equal(222, pieces.Count);
        Assert.All(pieces, o =>
        {
            Assert.Matches(@"^sky-\d{3}$", o.ChecklistId);
            Assert.NotEmpty(o.ChecklistSource);
            Assert.Single(o.ItemNames);
        });
        Assert.Equal(pieces.Count, pieces.Select(o => o.ChecklistId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(95, SkyChecklistRows.Items.Select(i => i.ClassName + "|" + i.Reward).Distinct().Count());
    }

    /// <summary>The table's provenance comments became Sources on the steps they decided — and
    /// AFTER the wiki page, because the step's hover and share-back door read the FIRST source
    /// as the page a correction goes to.</summary>
    [Fact]
    public void TheTablesProvenanceRidesTheStepsItDecidedBehindTheWikiPage()
    {
        var byId = ShippedSkyPieces.ToDictionary(o => o.ChecklistId);
        foreach (var (id, thread) in new[]
                 {
                     ("sky-003", "discussions/139"), ("sky-003", "issues/150"),
                     ("sky-005", "discussions/139"), ("sky-005", "issues/150"),
                     ("sky-128", "discussions/176"), ("sky-204", "pull/480"),
                 })
        {
            var sources = byId[id].Sources;
            Assert.Contains("eqlwiki.com", sources[0].Url);
            var provenance = Assert.Single(sources, s => s.Url.EndsWith(thread, StringComparison.Ordinal));
            Assert.NotEmpty(provenance.Note);
        }
    }

    /// <summary>Committed negatives for the reader itself: a Sky guide with no turn-in names no
    /// reward, and a step with no row id is not a row. Neither is guessed at.</summary>
    [Fact]
    public void AGuideWithNoTurnInOrAStepWithNoIdContributesNoRow()
    {
        var catalog = GuideCatalog.FromJson("""
            {
              "guides": [
                {
                  "id": "no-turn-in", "guideType": "PlaneOfSkyQuest", "applicableClasses": ["Bard"],
                  "stages": [ { "id": "s", "order": 1, "objectives": [
                    { "id": "a", "order": 1, "checklistId": "sky-900", "checklistSource": "Isle 3: x", "itemNames": ["Thing"] } ] } ]
                },
                {
                  "id": "one-row", "guideType": "PlaneOfSkyQuest", "applicableClasses": ["Bard"],
                  "stages": [ { "id": "s", "order": 1, "objectives": [
                    { "id": "a", "order": 1, "checklistId": "sky-901", "checklistSource": "Isle 3: x", "itemNames": ["Thing"] },
                    { "id": "b", "order": 2, "itemNames": ["Unkeyed"] },
                    { "id": "t", "order": 3, "rewardKey": "Bard|Mask of Song", "who": "Cilin Spellsinger" } ] } ]
                }
              ]
            }
            """);

        var row = Assert.Single(SkyChecklistRows.From(catalog));
        Assert.Equal("sky-901", row.Id);
        Assert.Equal("Bard", row.ClassName);
        Assert.Equal("Mask of Song", row.Reward);
        Assert.Equal("Cilin Spellsinger", row.Npc);
        Assert.Equal("Thing", row.QuestItem);
        Assert.Equal("Isle 3: x", row.Source);
    }
}
