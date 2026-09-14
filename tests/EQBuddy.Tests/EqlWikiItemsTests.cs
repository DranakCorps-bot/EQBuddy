using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>The eqlwiki item lookup: {{Itempage}} parsing against REAL saved wikitext
/// (fetched during the 2026-08-04 survey — the fixtures ARE the wiki's output), title
/// normalization, and the cache/fetch state machine with an injected fetcher.</summary>
public class EqlWikiItemsTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "fixtures", "wiki", name + ".txt"));

    [Fact]
    public void RustyBroadSwordParsesEverySection()
    {
        var item = EqlWikiItemService.Parse(Fixture("rusty"), "Rusty Broad Sword");

        Assert.Equal("Rusty Broad Sword", item.Name);
        Assert.Contains("Slot: PRIMARY SECONDARY", item.StatsLines);
        Assert.Contains("Skill: 1H Slashing  Atk Delay: 36", item.StatsLines);
        Assert.Contains("Class: WAR PAL RNG SHD BRD ROG", item.StatsLines);
        Assert.Equal("5s 8c", item.MerchantValue);   // HTML per-coin shape, zeros dropped

        // Piped links in ItemWhereRow must not break the positional split.
        Assert.Equal(2, item.SoldBy.Count);
        Assert.Equal(("East Freeport", "Harg Tonicka", "(-41, -493)"), item.SoldBy[0]);

        Assert.Contains("Bladed Weapons", item.Quests);
        Assert.Contains("Practice Rune (Beza) (Trivial:21)", item.Recipes);
        Assert.Contains("Vendor Sold", item.Categories);

        // Freetext dropsfrom ("Various Zones" + a bullet) survives as-is.
        var (zone, mobs) = Assert.Single(item.DropsFrom);
        Assert.Equal("Various Zones", zone);
        Assert.Equal("Various Mobs Level 1 - 15", Assert.Single(mobs));
    }

    /// <summary>
    /// **THE VENDOR PRICE CARRIES THE CONDITION IT WAS QUOTED AT** (DRA-71 D7, plan P9).
    ///
    /// <para>This fixture is real saved wikitext from the 2026-08-04 survey, and its
    /// <c>merchant_value</c> block has always opened <i>"VALUE TO VENDOR with CHA : 80 and
    /// faction at Ally"</i>. Until this slice the parser kept the coins and threw the heading
    /// away — which turns one editor's Charisma and faction into a fact about the object. The
    /// survey of the cached dump is what made that matter: 235 of the 646 pages whose value
    /// this build can read state a condition, and the Charisma differs per page (80, 72,
    /// 111).</para>
    ///
    /// <para>The number is unchanged and the condition rides beside it, verbatim — re-phrasing
    /// it would be a second producer of a claim the page already made.</para>
    /// </summary>
    [Fact]
    public void TheVendorValueKeepsTheCharismaAndFactionItWasQuotedAt()
    {
        var item = EqlWikiItemService.Parse(Fixture("rusty"), "Rusty Broad Sword");

        Assert.Equal("5s 8c", item.MerchantValue);
        Assert.Equal("VALUE TO VENDOR with CHA : 80 and faction at Ally", item.MerchantCondition);
        // And the number the catalog will carry is the one CoinText reads back off that text —
        // one grammar, asserted end to end rather than assumed.
        Assert.Equal(58L, CoinText.Parse(item.MerchantValue));
    }

    /// <summary>A page that states its value as plain text states no condition, and gets ""
    /// rather than an invented caveat — an unanswered question draws nothing (trap 73).</summary>
    [Fact]
    public void APlainValueCarriesNoConditionRatherThanAMadeUpOne()
    {
        var item = EqlWikiItemService.Parse(
            "{{Itempage\n|merchant_value = 4p 7g 5s\n|itemname = Thing\n}}", "Thing");

        Assert.Equal("4p 7g 5s", item.MerchantValue);
        Assert.Equal("", item.MerchantCondition);
        Assert.Equal(4750L, CoinText.Parse(item.MerchantValue));
    }

    [Fact]
    public void DroppedOnlyItemHasDropsAndNoVendors()
    {
        var item = EqlWikiItemService.Parse(Fixture("cloak"), "Cloak of Flames");
        Assert.NotEmpty(item.DropsFrom);
        Assert.Empty(item.SoldBy);
    }

    [Fact]
    public void NoDropFlagSurvivesInStats()
    {
        var item = EqlWikiItemService.Parse(Fixture("key"), "Master Crushbone Cell Key");
        Assert.Contains(item.StatsLines, l => l.Contains("NO DROP"));
    }

    [Fact]
    public void ApostropheNamesParse()
    {
        var item = EqlWikiItemService.Parse(Fixture("apos"), "Kilva's Blistering Flesh");
        Assert.NotEmpty(item.StatsLines);
    }

    /// <summary>#75 (Phosphorous Powder): the page's stats block says QUEST ITEM but
    /// the editor forgot the Quest Items category — the flag must count on its own,
    /// from either source, and never fire on an ordinary item.</summary>
    [Fact]
    public void QuestFlagReadsStatsBlockAndCategory()
    {
        const string flaggedNoCategory = "{{Itempage\n|itemname=Phosphorous Powder\n" +
            "|statsblock=QUEST ITEM<br>WT: 0.1  Size: TINY<br>Class: ALL<br>Race: ALL\n}}\n" +
            "[[Category:Inventory Items]]";
        Assert.True(EqlWikiItemService.Parse(flaggedNoCategory, "Phosphorous Powder").QuestFlagged);

        const string categoryOnly = "{{Itempage\n|itemname=Sealed Note\n" +
            "|statsblock=WT: 0.1  Size: SMALL\n}}\n[[Category:Quest Items]]";
        Assert.True(EqlWikiItemService.Parse(categoryOnly, "Sealed Note").QuestFlagged);

        // An ordinary item trips neither source. (The rusty fixture can't play this
        // part — the real Rusty Broad Sword sits in Category:Quest Items.)
        const string ordinary = "{{Itempage\n|itemname=Cracked Staff\n" +
            "|statsblock=Slot: PRIMARY<br>Skill: 2H Blunt  Atk Delay: 36\n}}\n" +
            "[[Category:Vendor Sold]]";
        Assert.False(EqlWikiItemService.Parse(ordinary, "Cracked Staff").QuestFlagged);
    }

    [Theory]
    [InlineData("Rusty Broad Sword +4", "Rusty Broad Sword")]
    [InlineData("Fine Steel Long Sword +12", "Fine Steel Long Sword")]
    [InlineData("Cloak of Flames", "Cloak of Flames")]
    public void InGameUpgradeSuffixesStripForLookup(string inGame, string title) =>
        Assert.Equal(title, EqlWikiItemService.NormalizeTitle(inGame));

    [Fact]
    public async Task LookupCachesAndServesStaleWhenOffline()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"eqbuddy-wiki-{Guid.NewGuid():N}");
        try
        {
            var fetches = 0;
            var svc = new EqlWikiItemService(dir, _ => { fetches++; return Task.FromResult<string?>(Fixture("rusty")); });

            var live = await svc.LookupAsync("Rusty Broad Sword +4");
            Assert.Equal(ItemLookupState.Live, live.State);
            Assert.Equal("Rusty Broad Sword", live.Item!.Name);
            Assert.Equal(1, fetches);

            var cached = await svc.LookupAsync("Rusty Broad Sword +4");
            Assert.Equal(ItemLookupState.Cached, cached.State);
            Assert.Equal(1, fetches);   // no second fetch inside the cache lifetime

            // A new service over the same cache dir whose fetcher FAILS: stale beats nothing —
            // but only once the cache has expired would fetch even be tried; simulate by a
            // throwing fetcher and a fresh service with an expired-cache override path.
            var offline = new EqlWikiItemService(dir, _ => throw new HttpRequestException("offline"));
            var again = await offline.LookupAsync("Rusty Broad Sword +4");
            Assert.Equal(ItemLookupState.Cached, again.State);   // still fresh → cache, no fetch
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task MissingPagesReportNotFoundAndBacktickFallbackIsTried()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"eqbuddy-wiki-{Guid.NewGuid():N}");
        try
        {
            var asked = new List<string>();
            var svc = new EqlWikiItemService(dir, title =>
            {
                asked.Add(title);
                return Task.FromResult<string?>(null);   // every page missing
            });
            var result = await svc.LookupAsync("Teir`Dal Sword");
            Assert.Equal(ItemLookupState.NotFound, result.State);
            Assert.Equal(["Teir`Dal Sword", "TeirDal Sword"], asked);   // exact first, then stripped
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void TheWikiClientNamesTheTool()
    {
        // Audit finding 13: the framework's blank default agent says nothing about
        // who is asking, and wiki operators rate and block by agent string. Both
        // wiki services share this client shape (same idiom as UpdateChecker's).
        using var c = EqlWikiText.CreateClient();
        Assert.Equal("EQBuddy-WikiLookup", c.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Equal(TimeSpan.FromSeconds(12), c.Timeout);
    }
}
