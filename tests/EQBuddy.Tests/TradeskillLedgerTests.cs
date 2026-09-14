using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **A SKILL VALUE USED TO DIE WITH THE SESSION** (DRA-71 D8, plan P13).
///
/// <para><c>StatsSnapshot.SkillUps</c> has always known what you raised tonight and nothing
/// has ever remembered it, so a player who spent last week smithing opened a tool that knew
/// nothing about their profession. This is the store that fixes it, and these are the three
/// rules it turns on: only professions are admitted, the highest value wins, and the whole
/// thing survives the disk.</para>
///
/// <para>The writer lands in the same slice as the reader (trap 20) — <c>MainWindow</c> offers
/// the live session's list every tick and the Helper's professions block draws it. The E2E row
/// in <c>ShellHostTests</c> is the half that proves both ends from a launched app.</para>
/// </summary>
public class TradeskillLedgerTests : IDisposable
{
    private const string Dranak = "dranak_legends";
    private static readonly DateTime At = new(2026, 9, 7, 21, 14, 3);

    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"eqb-skills-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        foreach (var f in new[] { _path, _path + ".rules", _path + ".bak" })
            if (File.Exists(f)) File.Delete(f);
        GC.SuppressFinalize(this);
    }

    private QuestLedgerStore Store() => new(_path);

    /// <summary>The value and its moment both survive a save and a reload — a standing with no
    /// moment would be a number a surface has to imply "now" about.</summary>
    [Fact]
    public void AProfessionSkillSurvivesTheDisk()
    {
        var store = Store();
        Assert.True(store.SetSkills(Dranak, [("Blacksmithing", 122, At)]));
        // Save is debounced two seconds; hosts Flush at exit. Same as every other round-trip
        // test in this repo — the assertion is about what the file holds, not about when.
        store.Flush();

        var row = Assert.Single(Store().SkillsFor(Dranak));
        Assert.Equal("Blacksmithing", row.Skill);
        Assert.Equal(122, row.Value);
        Assert.Equal(At, row.At);
    }

    /// <summary>
    /// **The replay bounce** — and it is a DIFFERENT mechanism from the loot ledger's.
    ///
    /// <para>The full-log replay every launch re-offers every skill-up in the file. Loot
    /// accumulates, so that store needs a time high-water mark; a skill-up carries the TOTAL
    /// the game printed, so re-offering it lands on the same number and changes nothing. The
    /// assertion is on the return value rather than on the count, because "nothing moved"
    /// is what decides whether the profile file is rewritten.</para>
    /// </summary>
    [Fact]
    public void ReOfferingTheSameSkillUpChangesNothing()
    {
        var store = Store();
        store.SetSkills(Dranak, [("Blacksmithing", 122, At)]);

        Assert.False(store.SetSkills(Dranak, [("Blacksmithing", 122, At)]));
        Assert.False(store.SetSkills(Dranak, [("Blacksmithing", 122, At.AddDays(3))]));
        Assert.Equal(122, Assert.Single(store.SkillsFor(Dranak)).Value);
    }

    /// <summary>The higher value wins and brings its own moment; a lower one is ignored
    /// outright. The game has never printed a lower one — the fold must not depend on
    /// that.</summary>
    [Fact]
    public void TheHighestValueWinsAndALowerOneIsIgnored()
    {
        var store = Store();
        store.SetSkills(Dranak, [("Pottery", 40, At)]);

        Assert.True(store.SetSkills(Dranak, [("Pottery", 41, At.AddHours(1))]));
        Assert.False(store.SetSkills(Dranak, [("Pottery", 12, At.AddHours(2))]));

        var row = Assert.Single(store.SkillsFor(Dranak));
        Assert.Equal(41, row.Value);
        Assert.Equal(At.AddHours(1), row.At);
    }

    /// <summary>
    /// **Only the eight professions land here.**
    ///
    /// <para>It is <c>TrackFilter</c>'s rule applied to a second kind of row — a ledger admits
    /// what a surface can answer about, so the file stays profession-sized rather than storing
    /// sixty combat skills nothing reads. Writing rows for a reader that does not exist is the
    /// app doing something and telling nobody (trap 43); widening the filter is the job of the
    /// slice that builds the surface.</para>
    /// </summary>
    [Fact]
    public void ACombatSkillIsRefusedAndAProfessionBesideItIsNot()
    {
        var store = Store();

        Assert.True(store.SetSkills(Dranak, [
            ("1H Slashing", 200, At),
            ("Channeling", 180, At),
            ("Tailoring", 31, At),
        ]));

        var row = Assert.Single(store.SkillsFor(Dranak));
        Assert.Equal("Tailoring", row.Skill);
    }

    /// <summary>A batch of nothing but refused skills writes nothing at all — the profile file
    /// is not rewritten for a session of pure combat.</summary>
    [Fact]
    public void ABatchWithNoProfessionInItWritesNothing()
    {
        var store = Store();

        Assert.False(store.SetSkills(Dranak, [("1H Slashing", 200, At), ("Defense", 150, At)]));
        Assert.Empty(store.SkillsFor(Dranak));
    }

    /// <summary>Skills belong to a character. Two characters on one profile raising the same
    /// craft are two standings.</summary>
    [Fact]
    public void SkillsAreKeptPerCharacter()
    {
        var store = Store();
        store.SetSkills(Dranak, [("Brewing", 88, At)]);
        store.SetSkills("someone_else", [("Brewing", 12, At)]);

        Assert.Equal(88, Assert.Single(store.SkillsFor(Dranak)).Value);
        Assert.Equal(12, Assert.Single(store.SkillsFor("someone_else")).Value);
        Assert.Empty(store.SkillsFor("never_played"));
        Assert.Empty(store.SkillsFor(""));
    }

    /// <summary>
    /// **The join the surface actually draws**, from the store's own rows.
    ///
    /// <para>The store keeps the log's spelling and <c>Tradeskills.Standings</c> is the one
    /// thing that turns it into a profession — so a character whose log says "Jewelry Making"
    /// and one whose log says "Jewelcrafting" read as the same craft on screen, which is the
    /// normalization the curated aliases exist for.</para>
    /// </summary>
    [Fact]
    public void TheStoredRowsFoldIntoStandingsThroughTheCuratedAliases()
    {
        var store = Store();
        store.SetSkills(Dranak, [("Jewelry Making", 74, At)]);

        var standing = Tradeskills.Standings(store.SkillsFor(Dranak))
            .Single(s => s.Skill == Tradeskill.Jewelcrafting);

        Assert.True(standing.Known);
        Assert.Equal(74, standing.Value);
    }
}
