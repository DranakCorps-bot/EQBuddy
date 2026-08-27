using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The accuracy gates (David, 2026-08-11: "being wrong once drops faith in
/// everything"). These invariants run on every build — including the weekly
/// knowledge-refresh PR's CI — so a harvest that reintroduces parse artifacts,
/// index pages, or a new aggregate species CANNOT merge quietly. Each rule
/// encodes a defect actually found in the 2026-08-11 audit.
/// </summary>
public class CatalogSanityTests
{
    private static readonly QuestCatalog Cat = QuestCatalog.LoadEmbedded();

    [Fact]
    public void ItemNamesAreCleanLootMatchableText()
    {
        foreach (var q in Cat.Quests)
            foreach (var i in q.Items)
            {
                Assert.False(string.IsNullOrWhiteSpace(i.Name), $"'{q.Name}': empty item name");
                // The Dreadscale ':Banded Bracer' class of artifact, and anything a
                // loot line could never print.
                Assert.False(i.Name.StartsWith(':') || i.Name.Contains("  ")
                    || i.Name.IndexOfAny(['[', ']', '{', '}', '|', '<', '>']) >= 0,
                    $"'{q.Name}': artifact item name '{i.Name}'");
                Assert.InRange(i.Qty, 1, 100);
            }
    }

    [Fact]
    public void NoQuestListsTheSameItemTwice()
    {
        foreach (var q in Cat.Quests)
        {
            var dupes = q.Items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(dupes.Count == 0, $"'{q.Name}': duplicate items [{string.Join(", ", dupes)}]");
        }
    }

    [Fact]
    public void AggregatePagesCannotSneakBackIn()
    {
        // Every "...Quests" page must be flagged (the #99 disease, catalog-wide).
        foreach (var q in Cat.Quests.Where(q =>
                     q.Name.EndsWith("Quests", StringComparison.OrdinalIgnoreCase)))
            Assert.True(q.Collection, $"'{q.Name}' looks aggregate but isn't flagged");

        // An UNFLAGGED page with an absurd reward list is a new index page the
        // hygiene list hasn't met yet ("Popular Quests by Level" carried 347).
        // Gambling quests are the legitimate exception: one turn-in, a long list
        // of possible payouts.
        var gamblingPayouts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Deck of Spontaneous Generation Quest", "Knight Card Quest" };
        foreach (var q in Cat.Quests.Where(q => !q.Collection && !gamblingPayouts.Contains(q.Name)))
            Assert.True(q.Rewards.Count < 30,
                $"'{q.Name}': {q.Rewards.Count} rewards unflagged — new index/aggregate page?");

        // And an unflagged non-epic with a vast item list is a new aggregate species.
        foreach (var q in Cat.Quests.Where(q =>
                     !q.Collection && !q.Name.Contains("Epic", StringComparison.OrdinalIgnoreCase)))
            Assert.True(q.Items.Count < 45,
                $"'{q.Name}': {q.Items.Count} items unflagged — new aggregate page?");
    }

    /// <summary>David's live catch (2026-08-11): Blue Orc Head's quest badge led to
    /// "nothing matches" because the search honored his class filter and The Falchion
    /// is a Paladin quest. The badge's promise — click an item, see its quests — must
    /// hold for EVERY turn-in item in the catalog, through the same search the
    /// tracker uses, with no filter able to break it.</summary>
    [Fact]
    public void EveryTurnInItemFindsItsQuestThroughSearch()
    {
        // The exact three from the field report first, by name.
        Assert.Contains(QuestSearch.Find(Cat, "Blue Orc Head"), q => q.Name == "The Falchion");
        Assert.Contains(QuestSearch.Find(Cat, "Tergon's Spellbook"), q => q.Name == "Tergon's Spellbook Quest");
        Assert.Contains(QuestSearch.Find(Cat, "Sealed Note"), q => q.Name == "Scouts Cape Quest");

        // Then the whole catalog: every item on any quest's turn-in list must surface
        // at least that quest when searched verbatim.
        foreach (var q in Cat.Quests)
            foreach (var i in q.Items)
                Assert.True(QuestSearch.Matches(q, i.Name),
                    $"'{i.Name}' fails to find its own quest '{q.Name}'");
    }

    /// <summary>The 2026-08-11 durable fix behind the Collection stopgap: chain and
    /// armor-set pages split into per-step quests at harvest time (reward-anchored
    /// section splitting, quests-harvest.py). These flagship steps pin the split so
    /// a future harvest that silently loses it cannot merge.</summary>
    [Fact]
    public void ChainStepsAreRealQuests()
    {
        // The Coldain Ring chain: nine real steps, each a single-reward quest that
        // computes progress, plus the chain page still flagged as the overview.
        var copper = Assert.Single(Cat.Quests, q => q.Name == "Copper Coldain Insignia Ring (#1)");
        Assert.False(copper.Collection);
        Assert.Equal(["Copper Coldain Insignia Ring"], copper.Rewards);
        Assert.Contains(copper.Items, i => i.Name == "High Quality Cougarskin" && i.Qty == 2);

        // Chain semantics: each step turns in the previous step's reward.
        var silver = Assert.Single(Cat.Quests, q => q.Name == "Silver Coldain Insignia Ring (#2)");
        Assert.Contains(silver.Items, i => i.Name == "Copper Coldain Insignia Ring");

        var chainPage = Assert.Single(Cat.Quests, q => q.Name == "Coldain Ring Quests");
        Assert.True(chainPage.Collection);

        // Ring 10 lives on its own wiki page — the split must delegate, not duplicate.
        Assert.Single(Cat.Quests, q => q.Rewards.Contains("Ring of Dain Frostreaver IV"));

        // The other split families: shawl chain, symbol chain, armor sets.
        Assert.Single(Cat.Quests, q => q.Name == "Burlap Coldain Prayer Shawl (#1)");
        Assert.Single(Cat.Quests, q => q.Name == "Initiate Symbol of Tunare");
        Assert.Single(Cat.Quests, q => q.Name == "Trooper Scale Boots");

        // Every step is a finishable quest: exactly one reward, at least one item,
        // never Collection-flagged (their names never end in "Quests").
        foreach (var step in Cat.Quests.Where(q => q.Name.EndsWith(')') && q.Name.Contains("(#")))
        {
            Assert.False(step.Collection, $"step '{step.Name}' flagged Collection");
            Assert.NotEmpty(step.Items);
        }
    }

    /// <summary>#246 (jlcrisp): the wiki states the cask count only in prose
    /// ("recovered three of these casks" / "your third Blackburrow Cask"), which
    /// quests-harvest.py's "N x [[Item]]" pattern can't see, so the bare-link
    /// default of 1 shipped instead of 3. Helm-authorized hand correction
    /// (2026-08-27), not a harvest-parser change — pin it so a future harvest
    /// run can't silently reset it back to 1.</summary>
    [Fact]
    public void BlackburrowBrewersNeedsThreeCasks()
    {
        var brewers = Assert.Single(Cat.Quests, q => q.Name == "Blackburrow Brewers");
        Assert.Contains(brewers.Items, i => i.Name == "Blackburrow Cask" && i.Qty == 3);
    }

    [Fact]
    public void ErasStayOnTheLadder()
    {
        foreach (var q in Cat.Quests.Where(q => q.Era.Length > 0))
            Assert.Contains(QuestEraLadder.Eras, e =>
                e.Equals(q.Era, StringComparison.OrdinalIgnoreCase));
    }
}
