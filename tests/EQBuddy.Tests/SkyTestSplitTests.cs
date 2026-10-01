using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>#99 (wizen): the per-class "Plane of Sky Tests" wiki pages are AGGREGATES,
/// and treating each as one quest demanded every class item at once. The split turns
/// each into one quest per reward, driven by the Sky card's own reward↔items data.</summary>
public class SkyTestSplitTests
{
    [Fact]
    public void AggregatePagesAreGoneAndPerRewardQuestsExist()
    {
        var cat = QuestCatalog.LoadEmbedded();

        Assert.DoesNotContain(cat.Quests, q =>
            q.Name.EndsWith("Plane of Sky Tests", StringComparison.OrdinalIgnoreCase));

        // Spot-check a known reward from the Sky defaults (Wizard: Nargon's Staff,
        // three turn-in items, from Wizard Schrock).
        var nargon = Assert.Single(cat.Quests, q => q.Name == "Wizard Sky Test: Nargon's Staff");
        Assert.Equal("Wizard Schrock", nargon.QuestGiver);
        Assert.Equal(3, nargon.Items.Count);
        Assert.Contains(nargon.Items, i => i.Name == "Efreeti War Staff");
        Assert.Equal("Plane of Sky", nargon.StartZone);
        Assert.Equal("Sky", nargon.Era);
        Assert.Equal("Wizard", nargon.Classes);
        Assert.Contains("Nargon's Staff", nargon.Rewards);
    }

    [Fact]
    public void EverySplitQuestIsSmallAndClassScoped()
    {
        var cat = QuestCatalog.LoadEmbedded();
        var split = cat.Quests.Where(q => q.Name.Contains(" Sky Test: ")).ToList();

        // 14+ classes × several tests each — and no test needs more than a handful
        // of items (the aggregate disease was dozens-at-once).
        Assert.True(split.Count >= 60, $"only {split.Count} split quests");
        Assert.All(split, q =>
        {
            Assert.InRange(q.Items.Count, 1, 5);
            Assert.True(q.Classes.Length > 0, $"{q.Name} lost its class");
            Assert.Single(q.Rewards);
        });

        // The class filter respects the scoping: a Wizard selection sees Wizard
        // tests, not Monk ones.
        var wizardOnly = split.Where(q => QuestClassFilter.MatchesAny(q.Classes, ["Wizard"])).ToList();
        Assert.NotEmpty(wizardOnly);
        Assert.DoesNotContain(wizardOnly, q => q.Name.StartsWith("Monk "));
    }

    /// <summary>The split quest name and the Sky checklist key are two spellings of one
    /// fact, and the round trip is what stops them drifting. The negatives matter as much:
    /// an ordinary catalog quest must resolve to "" or every quest in the catalog would
    /// start reading its completion off the Sky checklist.</summary>
    [Fact]
    public void ASplitQuestNameRoundTripsToItsRewardKey()
    {
        Assert.Equal("Wizard Sky Test: Nargon's Staff",
            SkyTestSplit.QuestName("Wizard", "Nargon's Staff"));
        Assert.Equal("Wizard|Nargon's Staff",
            SkyTestSplit.RewardKeyFor("Wizard Sky Test: Nargon's Staff"));
        // The class with a space in it is the one that broke everything else.
        Assert.Equal("Shadow Knight|Obtenebrate Mithril Guard",
            SkyTestSplit.RewardKeyFor("Shadow Knight Sky Test: Obtenebrate Mithril Guard"));

        Assert.Equal("", SkyTestSplit.RewardKeyFor("Journey to the Plane of Sky"));
        Assert.Equal("", SkyTestSplit.RewardKeyFor("Sky Test: no class"));
        Assert.Equal("", SkyTestSplit.RewardKeyFor("Wizard Sky Test: "));
        Assert.Equal("", SkyTestSplit.RewardKeyFor(""));

        // Every SHIPPED split quest resolves — the property that makes the fold total,
        // asserted against the real catalog rather than a hand-built one.
        var split = QuestCatalog.LoadEmbedded().Quests
            .Where(q => q.Name.Contains(" Sky Test: ", StringComparison.Ordinal)).ToList();
        Assert.True(split.Count >= 60, $"only {split.Count} split quests");
        Assert.All(split, q => Assert.NotEqual("", SkyTestSplit.RewardKeyFor(q.Name)));
    }

    /// <summary>
    /// #101/#204's other half: the Quests tab read the per-character quest ledger and the
    /// Sky tab read `SkyQuestCompleted`, and nothing joined them — so a reward the game's
    /// own achievements dump reported as handed in still sat on the Quests tab as work to
    /// do. The fold is read-only and additive; a ledger count already there wins.
    /// </summary>
    [Fact]
    public void ASkyRewardTurnedInReadsCompletedOnTheCatalogTabToo()
    {
        // Since DRA-47 the fold is SkyCompleteToggle.CompletedQuests — one door beside the
        // turn-in that writes it — rather than a merge each caller ran itself.
        var path = Path.Combine(Path.GetTempPath(), $"eqb-split-{Guid.NewGuid():N}.json");
        try
        {
            var ledger = new QuestLedgerStore(path);
            ledger.SetCompleted(Me, "Journey to the Plane of Sky", true);
            for (var i = 0; i < 3; i++)   // repeatable count the player set
                ledger.RecordCompletion(Me, "Wizard Sky Test: Nargon's Staff", []);
            var settings = new AppSettings
            {
                SkyQuestCompleted = ["Shadow Knight|Obtenebrate Mithril Guard", "Wizard|Nargon's Staff"],
            };

            var merged = SkyCompleteToggle.CompletedQuests(settings, ledger, Me);

            Assert.Equal(1, merged["Shadow Knight Sky Test: Obtenebrate Mithril Guard"]);
            // The ledger's own answer is not overwritten by the fold.
            Assert.Equal(3, merged["Wizard Sky Test: Nargon's Staff"]);
            Assert.Equal(1, merged["Journey to the Plane of Sky"]);
            // Nothing invented: a reward nobody turned in stays absent, so a quest with no
            // entry is still "not completed" rather than "completed zero times".
            Assert.DoesNotContain("Bard Sky Test: Mask of Song", merged.Keys);

            // No character yet is the startup case and must not throw — it still answers the
            // turn-ins, which are the bound working set's.
            Assert.Equal(2, SkyCompleteToggle.CompletedQuests(settings, ledger, "").Count);
        }
        finally
        {
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*"))
                File.Delete(f);
        }
    }

    /// <summary>The inverse of the split name, both directions, and a key that is not one.</summary>
    [Fact]
    public void QuestNameForIsTheInverseOfRewardKeyFor()
    {
        var name = SkyTestSplit.QuestNameFor("Bard|Amulet of the Fae");
        Assert.Equal("Bard Sky Test: Amulet of the Fae", name);
        Assert.Equal("Bard|Amulet of the Fae", SkyTestSplit.RewardKeyFor(name));
        Assert.Equal("", SkyTestSplit.QuestNameFor("no bar here"));
        Assert.Equal("", SkyTestSplit.QuestNameFor("Bard|"));
    }

    private const string Me = "dranak_legends";
}
