using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The "Isle two - Protector of Sky" harvest defect, pinned in both directions.**
///
/// The original #70 harvest wrote "Isle two - Protector of Sky" onto some rows that the
/// wiki puts elsewhere. It has been found twice by two different reporters, a month apart,
/// and corrected twice — and until this file there was nothing executable behind either
/// correction, only a comment in <c>SkyQuestDefaults</c>:
///
/// <list type="bullet">
/// <item>#176, Fennec-Halas, 2026-08-16 — Paladin Golden Hilt (<c>sky-128</c>).</item>
/// <item>#483 last-look, 2026-09-09 — Warrior Gem of Invigoration (<c>sky-204</c>).</item>
/// </list>
///
/// The 2026-09-09 sweep (SSC #484) then read every REMAINING Isle 2 row against the wiki
/// and found both of them TRUE. That is the half this file exists for. Two corrections in
/// a row establish a pattern — "an Isle 2 Protector row is a harvest bug" — and the next
/// agent to apply that pattern by eye would break two correct rows. A guard that only
/// forbade the wrong thing could not see that (trap 34), so the must-list is here beside
/// the deny-list, and both come out of ONE table (trap 4).
///
/// **The evidence is the wiki's own code**, in the class test tables transcluded onto every
/// "&lt;Class&gt; Plane of Sky Tests" page from `Plane of Sky` (the class pages themselves are
/// transclusion stubs and contain no drop data — grep them for the item and you get nothing).
/// Every code is `&lt;island number&gt;-&lt;source on that island&gt;`: `8-EoV`, `5-SL`, `7-SotS`,
/// `6-BZ`, `3-Gorga`, `4-KoS`, `7-Trash`, `2-PoS`. The decoding is not a guess — the two rows
/// already corrected by other means both read `7-Trash`, which is what they were corrected TO.
/// Cached harvest: <c>scripts/harvests/eqlwiki/cache/lsth-Plane_of_Sky.wikitext</c>.
/// </summary>
public class SkyIsleTwoHarvestTests
{
    /// <summary>
    /// Every row the Isle-2 harvest defect has ever touched, with the wiki code that settles
    /// it and the island that code names. Deny-list and must-list in one table, because a
    /// second table is a second producer of one fact.
    /// </summary>
    public static TheoryData<string, string, string, double> WikiVerifiedIsles() => new()
    {
        // KEEP — swept 2026-09-09 and found CORRECT. Island 2 is "Azarack Island", its boss
        // is the Protector of Sky, and the zone page says he "drops the key to Island 3 and
        // some quest pieces". The items are named for the island; the quests are "Beastlord
        // Test of Azarack" and "Berserker Test of Blood". Do not "fix" these.
        { "sky-015", "Azarack Skin", "2-PoS", 2 },
        { "sky-027", "Azarack Blood", "2-PoS", 2 },

        // CORRECTED — these two carried the defect and must not drift back to Isle 2.
        { "sky-128", "Golden Hilt", "7-Trash", 7 },
        { "sky-204", "Gem of Invigoration", "7-Trash", 7 },
    };

    /// <summary>
    /// The island each row names is the island the wiki names — asserted through
    /// <see cref="SkyIslands.Parse"/> rather than against the prose, because the RULE is
    /// about where the player is sent, not about the wording. Rewording a source is allowed;
    /// moving the island is what needs a reporter and a hand-in.
    /// </summary>
    [Theory]
    [MemberData(nameof(WikiVerifiedIsles))]
    public void AWikiVerifiedRowKeepsTheIslandTheWikiGivesIt(
        string id, string questItem, string wikiCode, double island)
    {
        var row = Assert.Single(SkyQuestDefaults.Items.Where(i => i.Id == id));

        // The id must still be on the row the sweep actually checked — an id that quietly
        // moved to another item would let this pass while pinning nothing.
        Assert.Equal(questItem, row.QuestItem);

        Assert.Equal([island], SkyIslands.Parse(row.Source));

        // The failure message is the whole point: whoever trips this needs the wiki code,
        // not just a number, or they will re-run the same three-hour sweep to answer it.
        Assert.True(SkyIslands.Parse(row.Source) is [var actual] && actual == island,
            $"{id} ({questItem}) is on island {string.Join("/", SkyIslands.Parse(row.Source))}, " +
            $"but the wiki's Plane of Sky test table marks it '{wikiCode}' — island {island}. " +
            "Departing from the wiki needs a confirmed turn-in and a comment saying so.");
    }

    /// <summary>
    /// **The must-list half, stated as a count.** Exactly two shipped rows may sit on Isle 2,
    /// and they are the two the sweep verified. A third appearing means a re-harvest has
    /// reintroduced the defect somewhere new; either of these two disappearing means the
    /// pattern got applied to a row that was right all along.
    ///
    /// A count is the cheap half — the ids are asserted too, so a swap cannot leave it
    /// unmoved (trap 72's lesson, one file over).
    /// </summary>
    [Fact]
    public void ExactlyTheTwoSweptRowsSitOnIsleTwo()
    {
        var onIsleTwo = SkyQuestDefaults.Items
            .Where(i => SkyIslands.Parse(i.Source).Contains(2))
            .Select(i => i.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["sky-015", "sky-027"], onIsleTwo);
    }

    /// <summary>
    /// The two corrected rows must not name the Protector of Sky in their prose either. The
    /// island is the load-bearing fact, but a row reading "Isle 7 … Protector of Sky" would
    /// send a player to the wrong NAMED mob while passing every island assertion above.
    /// </summary>
    [Theory]
    [InlineData("sky-128")]
    [InlineData("sky-204")]
    public void ACorrectedRowNoLongerNamesTheProtectorOfSky(string id)
    {
        var row = Assert.Single(SkyQuestDefaults.Items.Where(i => i.Id == id));
        Assert.DoesNotContain("Protector of Sky", row.Source, StringComparison.OrdinalIgnoreCase);
    }
}
