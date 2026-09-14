using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE CURATED EIGHT, CHECKED AGAINST A CATALOG EQBUDDY ALREADY SHIPS** (DRA-71 D8, plan
/// P13).
///
/// <para>A hand-written list of game facts is exactly the thing that goes stale in silence, so
/// nothing here trusts <c>Tradeskills</c>' own spelling of anything. Every profession names
/// its Mastery AA, and the assertions read that ability back out of
/// <see cref="AaCatalog"/> — the eqlwiki harvest EQBuddy embeds — to check that the AA exists
/// and that its own effect sentence uses the same word for the skill. That is what "wiki
/// matched" can mean as a test rather than as a comment.</para>
///
/// <para>The other half is the boundary. Four real professions are deliberately NOT in the
/// list, and a curated file whose omissions are undocumented is indistinguishable from one
/// whose author forgot: the negatives below are what make the edge visible to whoever widens
/// it.</para>
/// </summary>
public class TradeskillsTests
{
    /// <summary>The count out loud, so a ninth arriving is a deliberate edit here rather than
    /// a row somebody slid into the enum (the goals' own rule, one list over).</summary>
    [Fact]
    public void ThereAreEightProfessionsAndTheEnumAndTheTableAgree()
    {
        Assert.Equal(8, Tradeskills.All.Count);
        Assert.Equal(Enum.GetValues<Tradeskill>(), Tradeskills.All.Select(p => p.Skill).ToArray());
        Assert.Equal(8, Tradeskills.All.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// **The source the plan named, read back from the shipped catalog.**
    ///
    /// <para>Two claims, and the second is the one a comment could never hold: the AA exists,
    /// and its own effect text — eqlwiki's prose, harvested, not ours — spells the skill the
    /// way this file does. "Jewel Craft Mastery" reducing failures of "Jewelcrafting" recipes
    /// is the wiki resolving its own normalization, and this is where that resolution is
    /// pinned.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Professions))]
    public void EveryProfessionIsNamedByItsOwnMasteryAaEffect(Tradeskill skill)
    {
        var profession = Tradeskills.For(skill);
        var aa = AaCatalog.Find(profession.MasteryAa);

        Assert.True(aa is not null,
            $"{profession.MasteryAa} is not in the shipped AA catalog. The curated profession "
            + "list is built FROM that catalog, so a missing ability means the list and the "
            + "harvest have come apart — check the harvest before editing this file.");
        Assert.Contains($"failing {profession.Name} recipes", aa!.Effect,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("General", aa.Category);
    }

    /// <summary>
    /// **The generic Mastery is refused, and the refusal is a named constant.**
    ///
    /// <para>A list assembled by matching "Mastery" in the catalog would have picked up
    /// Crafting Mastery, whose effect raises the specialization cap of professions you already
    /// have. It is not a ninth profession, and the assertion below is the committed negative
    /// that says so.</para>
    ///
    /// <para>Its effect text is also the closest thing the game's data has to a DEFINITION of
    /// the set: it names seven of these eight in one parenthetical — in a third spelling of
    /// Jewelcrafting — and the one it leaves out is Alchemy. Pinned here because the day that
    /// sentence changes is the day the curated list should be re-read.</para>
    /// </summary>
    [Fact]
    public void CraftingMasteryIsNotAProfessionAndNamesSevenOfTheEight()
    {
        Assert.DoesNotContain(Tradeskills.All,
            p => p.MasteryAa.Equals(Tradeskills.GenericMasteryAa, StringComparison.OrdinalIgnoreCase));

        var effect = AaCatalog.Find(Tradeskills.GenericMasteryAa)?.Effect ?? "";
        Assert.Contains("additional standard tradeskill", effect, StringComparison.OrdinalIgnoreCase);

        var named = Tradeskills.All
            .Where(p => effect.Contains(p.Name[..6], StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Skill)
            .ToArray();
        Assert.Equal(7, named.Length);
        Assert.DoesNotContain(Tradeskill.Alchemy, named);
    }

    /// <summary>
    /// Every "… Mastery" in the catalog's General category is either one of the eight or the
    /// generic one — <b>the liveness half</b>.
    ///
    /// <para>Without it, the two assertions above would both pass over a catalog that had
    /// grown a ninth profession's ability: each of the eight would still find its own row. This
    /// is the row that fails when EQ Legends adds a profession, which is the only way this
    /// curated file can learn about one.</para>
    /// </summary>
    [Fact]
    public void TheCatalogsGeneralMasteriesAreTheEightPlusTheGenericOne()
    {
        var general = AaCatalog.All
            .Where(a => a.Category == "General"
                        && a.Name.EndsWith("Mastery", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var expected = Tradeskills.All.Select(p => p.MasteryAa)
            .Append(Tradeskills.GenericMasteryAa)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, general);
    }

    /// <summary>Every profession points at an eqlwiki page title in the wiki's own shape.
    /// The pages are the ones the cached item pages LINK to — a title we assembled from the
    /// name would be a page nobody has seen (trap 3's lesson one step earlier).</summary>
    [Theory]
    [MemberData(nameof(Professions))]
    public void EveryProfessionNamesAWikiSkillPage(Tradeskill skill)
    {
        var profession = Tradeskills.For(skill);
        Assert.StartsWith("Skill ", profession.WikiPage, StringComparison.Ordinal);
        Assert.NotEmpty(profession.LogNames);
        Assert.All(profession.LogNames, Assert.NotEmpty);
    }

    // ---- what the log is allowed to mean ------------------------------------------------

    /// <summary>Every alias resolves back to its own profession — including the three
    /// spellings of Jewelcrafting, which is the row this whole mechanism exists for.</summary>
    [Theory]
    [MemberData(nameof(Professions))]
    public void EveryAliasMatchesItsOwnProfession(Tradeskill skill)
    {
        foreach (var alias in Tradeskills.For(skill).LogNames)
        {
            Assert.Equal(skill, Tradeskills.Match(alias));
            Assert.Equal(skill, Tradeskills.Match(alias.ToUpperInvariant()));
            Assert.Equal(skill, Tradeskills.Match("  " + alias + "  "));
        }
    }

    /// <summary>
    /// **The four professions this list deliberately does not carry.**
    ///
    /// <para>Tinkering, Spell Research, Make Poison and Fishing all have eqlwiki skill pages
    /// and all four appear in the cached item pages' recipe lines. None of them has a Mastery
    /// AA, which is the source the curated list is built from, so none is here. This is a
    /// committed negative rather than a comment: the next person to widen the list should find
    /// a failing assertion that tells them the boundary was a decision.</para>
    /// </summary>
    [Theory]
    [InlineData("Tinkering")]
    [InlineData("Research")]
    [InlineData("Spell Research")]
    [InlineData("Make Poison")]
    [InlineData("Poison Making")]
    [InlineData("Fishing")]
    public void TheSkillsWithNoMasteryAaAreNotProfessions(string skill) =>
        Assert.Null(Tradeskills.Match(skill));

    /// <summary>Every other skill the game announces answers null — which is what keeps the
    /// ledger profession-sized, and what stops a combat skill's number appearing under a
    /// craft.</summary>
    [Theory]
    [InlineData("1H Slashing")]
    [InlineData("Channeling")]
    [InlineData("Defense")]
    [InlineData("Meditate")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnUnrelatedSkillIsNotAProfession(string? skill)
    {
        Assert.Null(Tradeskills.Match(skill));
        Assert.False(Tradeskills.IsProfessionSkill(skill));
    }

    /// <summary>
    /// **Whole string, never a substring** — the rule written as its failing case.
    ///
    /// <para>"Blacksmithing Mastery" is an ABILITY, not a skill, and a substring rule would
    /// have read an AA purchase line as a skill-up. "Alchemyst" is the shape of any longer
    /// skill name that happens to open with a profession's letters. Both would put somebody
    /// else's number under a player's craft, which is worse than showing nothing.</para>
    /// </summary>
    [Theory]
    [InlineData("Blacksmithing Mastery")]
    [InlineData("Alchemyst")]
    [InlineData("Baking Ingredient")]
    [InlineData("Potter")]
    public void MatchingIsWholeStringAndNotASubstring(string skill) =>
        Assert.Null(Tradeskills.Match(skill));

    // ---- the standings fold ---------------------------------------------------------------

    /// <summary>
    /// **Unknown is not a zero**, which is the whole reason <see cref="TradeskillStanding.Known"/>
    /// exists beside the number.
    ///
    /// <para>A player who has been smithing for a month on a machine EQBuddy was not watching
    /// has no skill-up in the log EQBuddy can see. "You are at 0" is false for them; "EQBuddy
    /// has not seen one" is true for everybody. All eight always come back, because a
    /// profession that vanished from the list because nothing had been seen would be a picker
    /// that offers fewer rows the less you have done.</para>
    /// </summary>
    [Fact]
    public void EveryProfessionComesBackAndAnUnseenOneIsNotAZero()
    {
        var standings = Tradeskills.Standings([("Blacksmithing", 122, At)]);

        Assert.Equal(8, standings.Count);
        var smith = standings.Single(s => s.Skill == Tradeskill.Blacksmithing);
        Assert.True(smith.Known);
        Assert.Equal(122, smith.Value);
        Assert.Equal(At, smith.At);

        var baking = standings.Single(s => s.Skill == Tradeskill.Baking);
        Assert.False(baking.Known);
        Assert.Equal(0, baking.Value);
        Assert.Equal(default, baking.At);
    }

    /// <summary>The higher value wins and its own moment travels with it — two aliases of one
    /// profession are one standing, not two.</summary>
    [Fact]
    public void TheHighestValueWinsAndCarriesItsOwnMoment()
    {
        var standings = Tradeskills.Standings([
            ("Jewelcraft", 44, At),
            ("Jewelry Making", 91, At.AddDays(2)),
            ("Jewelcrafting", 12, At.AddDays(5)),
        ]);

        var jc = standings.Single(s => s.Skill == Tradeskill.Jewelcrafting);
        Assert.Equal(91, jc.Value);
        Assert.Equal(At.AddDays(2), jc.At);
    }

    /// <summary>A skill no profession claims contributes nothing, and neither does a
    /// nonsense value.</summary>
    [Fact]
    public void ASkillNoProfessionClaimsContributesNothing()
    {
        var standings = Tradeskills.Standings([
            ("1H Slashing", 200, At),
            ("Tinkering", 180, At),
            ("Pottery", 0, At),
            ("Pottery", -3, At),
        ]);

        Assert.All(standings, s => Assert.False(s.Known));
    }

    // ---- the pick store --------------------------------------------------------------------

    private const string Key = "dranak_legends";

    /// <summary>
    /// **Absent means all eight, and that is filter semantics on purpose.**
    ///
    /// <para><c>Picked</c> and <c>Listed</c> answer different questions and the difference is
    /// visible: the picker needs to know nothing is ticked, and the list needs to know it
    /// draws everything. A room that used one for both would either tick all eight boxes on a
    /// fresh profile or draw an empty block.</para>
    /// </summary>
    [Fact]
    public void NothingPickedListsAllEightAndTicksNone()
    {
        var settings = new AppSettings();

        Assert.Empty(TradeskillPickStore.Picked(settings, Key));
        Assert.Equal(8, TradeskillPickStore.Listed(settings, Key).Count);
    }

    [Fact]
    public void TogglingNarrowsTheListAndTogglingBackRemovesTheKey()
    {
        var settings = new AppSettings();

        TradeskillPickStore.Toggle(settings, Key, Tradeskill.Pottery);
        Assert.Equal([Tradeskill.Pottery], TradeskillPickStore.Picked(settings, Key));
        Assert.Equal([Tradeskill.Pottery], TradeskillPickStore.Listed(settings, Key));

        TradeskillPickStore.Toggle(settings, Key, Tradeskill.Pottery);
        Assert.Empty(TradeskillPickStore.Picked(settings, Key));
        // "Never picked" and "picked nothing" are the same state, stored one way.
        Assert.False(settings.HelperProfessions.ContainsKey(Key));
    }

    /// <summary>The pick belongs to a character, the same as every other Helper selection.</summary>
    [Fact]
    public void ThePickIsPerCharacter()
    {
        var settings = new AppSettings();
        TradeskillPickStore.Toggle(settings, Key, Tradeskill.Brewing);

        Assert.Empty(TradeskillPickStore.Picked(settings, "someone_else"));
        Assert.Equal(8, TradeskillPickStore.Listed(settings, "someone_else").Count);
    }

    /// <summary>A stored name the list no longer carries is skipped rather than throwing —
    /// a profession removed from the curation should stop mattering, not break the room for
    /// whoever had ticked it.</summary>
    [Fact]
    public void AnUnknownStoredNameIsSkipped()
    {
        var settings = new AppSettings();
        settings.HelperProfessions[Key] = ["Tinkering", "Pottery"];

        Assert.Equal([Tradeskill.Pottery], TradeskillPickStore.Picked(settings, Key));
    }

    /// <summary>The picks come back in the enum's own order however they were clicked, so the
    /// face reads the same way the picker does.</summary>
    [Fact]
    public void PicksComeBackInTheListsOwnOrder()
    {
        var settings = new AppSettings();
        settings.HelperProfessions[Key] = ["Tailoring", "Baking", "alchemy"];

        Assert.Equal(
            [Tradeskill.Alchemy, Tradeskill.Baking, Tradeskill.Tailoring],
            TradeskillPickStore.Picked(settings, Key));
    }

    private static readonly DateTime At = new(2026, 9, 7, 21, 14, 3);

    public static TheoryData<Tradeskill> Professions()
    {
        var data = new TheoryData<Tradeskill>();
        foreach (var skill in Enum.GetValues<Tradeskill>()) data.Add(skill);
        return data;
    }
}
