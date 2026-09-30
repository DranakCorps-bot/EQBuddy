using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **/who SETS THE CLASSES AND THE LEVEL** (Founder, 2026-09-30). Every fixture line is
/// verbatim from the Founder's own log (<c>eqlog_Dranak_freeport.txt</c>, Sep 11 and Sep 30),
/// including the other players' rows — which are here to prove they are DROPPED, never kept:
/// EQBuddy never measures other players.
/// </summary>
public class WhoTests : IDisposable
{
    private const string Mine =
        "[Wed Sep 30 15:27:23 2026] [50 WAR/DRU/MNK] Dranak (Ancient Wolf) <Ascendancy> ZONE: Nagafen's Lair (soldungb)  ";

    private static readonly string[] Block =
    [
        "[Wed Sep 30 15:27:23 2026] Players in EverQuest Legends:",
        "[Wed Sep 30 15:27:23 2026] ---------------------------",
        "[Wed Sep 30 15:27:23 2026] [45 SHD/MNK/NEC] Gamed (Iksar) <Debeo Amicitia> ZONE: Nagafen's Lair (soldungb)  ",
        "[Wed Sep 30 15:27:23 2026] [50 CLR/BRD/SHM] Athos (Half Elf)  ZONE: Nagafen's Lair (soldungb)  ",
        "[Wed Sep 30 15:27:23 2026] [50 SHD/ROG/SHM] Athuahua (Iksar) <Vacate These Premises> ZONE: Nagafen's Lair (soldungb)   LFG",
        "[Wed Sep 30 15:27:23 2026] [ANONYMOUS] Qari ",
        Mine,
        "[Wed Sep 30 15:27:23 2026] There are 9 players in EverQuest Legends.",
    ];

    private static readonly DateTime WhoAt = new(2026, 9, 30, 15, 27, 23);

    // ---- the parser -------------------------------------------------------------------

    [Fact]
    public void TheFoundersOwnRowParsesToHisLevelAndHisThreeClassesInTheCatalogsSpelling()
    {
        var e = Assert.IsType<WhoEntryEvent>(LogParser.Parse(Mine));
        Assert.Equal("Dranak", e.Name);
        Assert.Equal(50, e.Level);
        Assert.Equal(["Warrior", "Druid", "Monk"], e.Classes);
        Assert.Equal(WhoAt, e.Time);
    }

    [Theory]
    [InlineData("[Fri Sep 11 13:00:11 2026] [1 CLR/RNG] Plinky (Halfling)  ZONE: Clan Crushbone 4 (crushbone)  ", 1, "Cleric,Ranger")]
    [InlineData("[Wed Sep 30 15:27:23 2026] [50 SHD/ROG/SHM] Athuahua (Iksar) <Vacate These Premises> ZONE: Nagafen's Lair (soldungb)   LFG", 50, "Shadow Knight,Rogue,Shaman")]
    [InlineData("[Wed Sep 30 15:27:23 2026] [50 CLR/BRD/SHM] Athos (Half Elf)  ZONE: Nagafen's Lair (soldungb)  ", 50, "Cleric,Bard,Shaman")]
    public void EveryRowShapeInTheLogParses(string line, int level, string classes)
    {
        var e = Assert.IsType<WhoEntryEvent>(LogParser.Parse(line));
        Assert.Equal(level, e.Level);
        Assert.Equal(classes.Split(','), e.Classes);
    }

    /// <summary>Refusals: /anon hides both halves, the header and footer are not rows, and a
    /// title or unknown code is refused WHOLE rather than half-read (never guessed).</summary>
    [Theory]
    [InlineData("[Wed Sep 30 15:27:23 2026] [ANONYMOUS] Qari ")]
    [InlineData("[Wed Sep 30 15:27:23 2026] [ANONYMOUS] Dranak <Ascendancy>")]
    [InlineData("[Wed Sep 30 15:27:23 2026] There are 9 players in EverQuest Legends.")]
    [InlineData("[Wed Sep 30 15:27:23 2026] Players in EverQuest Legends:")]
    [InlineData("[Wed Sep 30 15:27:23 2026] [50 Warlord] Dranak (Iksar)  ZONE: Nagafen's Lair (soldungb)")]
    [InlineData("[Wed Sep 30 15:27:23 2026] [50 WAR/XYZ] Dranak (Iksar)  ZONE: Nagafen's Lair (soldungb)")]
    public void RowsThatStateNoClassesParseToNothing(string line) =>
        Assert.IsNotType<WhoEntryEvent>(LogParser.Parse(line));

    // ---- only YOUR row is kept -------------------------------------------------------

    [Fact]
    public void TheTrackerKeepsOnlyTheWatchedCharactersRowAndNoOneElses()
    {
        var tracker = new WhoTracker();
        foreach (var line in Block)
            if (LogParser.Parse(line) is { } e) tracker.Observe(e, "Dranak");

        var who = tracker.LatestFor("Dranak");
        Assert.NotNull(who);
        Assert.Equal("Dranak", who.Name);
        Assert.Equal(50, who.Level);
        Assert.Equal(["Warrior", "Druid", "Monk"], who.Classes);
    }

    /// <summary>The reachable negative: the same block in another character's log keeps
    /// nothing, because that character's row is not in it — another player's row is never a
    /// stand-in.</summary>
    [Fact]
    public void ACharacterWhoseRowIsNotInTheListingLearnsNothingFromOtherPlayersRows()
    {
        var tracker = new WhoTracker();
        foreach (var line in Block)
            if (LogParser.Parse(line) is { } e) tracker.Observe(e, "Hugzee");
        Assert.Null(tracker.LatestFor("Hugzee"));
    }

    [Fact]
    public void SwitchingCharacterForgetsThePreviousCharactersRow()
    {
        var tracker = new WhoTracker();
        tracker.Observe(LogParser.Parse(Mine)!, "Dranak");
        Assert.Null(tracker.LatestFor("Hugzee"));
    }

    // ---- which roster stands: the fresher claim ---------------------------------------

    private static readonly ClassReading WhoRow = new(["Warrior", "Druid", "Monk"], WhoAt);

    [Fact]
    public void AWhoOverAnUnstampedStatementWins_TheFirstWhoReplacesAnOldStatement()
    {
        var (classes, source) = CharacterClasses.Resolve(
            ["Paladin", "Warrior", "Druid"], null, null, ["Warrior", "Cleric", "Enchanter"], default, WhoRow);
        Assert.Equal(["Warrior", "Druid", "Monk"], classes);
        Assert.Equal(ClassSource.Who, source);
        Assert.Equal("from /who", CharacterClasses.SourceLabel(source));
    }

    [Fact]
    public void AStatementMadeAfterTheWhoWins_UntilTheNextWho()
    {
        var stated = new List<string> { "Warrior", "Cleric", "Enchanter" };
        var (after, s1) = CharacterClasses.Resolve(null, null, null, stated, WhoAt.AddMinutes(5), WhoRow);
        Assert.Equal(stated, after);
        Assert.Equal(ClassSource.Stated, s1);

        var nextWho = WhoRow with { At = WhoAt.AddMinutes(10) };
        var (again, s2) = CharacterClasses.Resolve(null, null, null, stated, WhoAt.AddMinutes(5), nextWho);
        Assert.Equal(WhoRow.Classes, again);
        Assert.Equal(ClassSource.Who, s2);
    }

    [Fact]
    public void OnAnExactTieTheStatementWins() =>
        Assert.Equal(ClassSource.Stated,
            CharacterClasses.Resolve(null, null, null, ["Cleric"], WhoAt, WhoRow).Source);

    [Fact]
    public void AWhoDisplacesTheDumpAndTheLogAndThePicks()
    {
        var (classes, source) = CharacterClasses.Resolve(
            ["Paladin"], ["Bard"], ["Necromancer"], null, default, WhoRow);
        Assert.Equal(WhoRow.Classes, classes);
        Assert.Equal(ClassSource.Who, source);
    }

    [Fact]
    public void WithNoWhoNothingChanges() =>
        Assert.Equal(ClassSource.Achievements, CharacterClasses.Resolve(["Paladin"], null, null).Source);

    /// <summary>The Character room's pill shows the roster the line shows — a click edits the
    /// /who's three, not a stale statement behind it.</summary>
    [Fact]
    public void TheEditorSeedsFromTheWhoWhenItIsTheFresherClaim()
    {
        Assert.Equal(WhoRow.Classes,
            ClassStatement.EditorSelection(["Cleric"], null, null, null, default, WhoRow));
        Assert.Equal(["Cleric"],
            ClassStatement.EditorSelection(["Cleric"], null, null, null, WhoAt.AddSeconds(1), WhoRow));
    }

    // ---- the store: roster + level ----------------------------------------------------

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"who-ledger-{Guid.NewGuid():N}.json");
    private const string Key = "dranak_freeport";

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private static WhoReading Reading(int level, DateTime at, params string[] classes) =>
        new("Dranak", level, classes, at);

    [Fact]
    public void AWhoOnAFreshCharacterMakesEveryEquippedClassItsLevel_AndSaysSo()
    {
        var store = new QuestLedgerStore(_path);
        Assert.True(store.SetWho(Key, Reading(50, WhoAt, "Warrior", "Druid", "Monk")));

        var (_, _, who) = store.ClassClaimsFor(Key);
        Assert.Equal(["Warrior", "Druid", "Monk"], who!.Classes);
        var level = store.ResolvedLevelFor(Key, who.Classes);
        Assert.Equal(50, level.Level);
        Assert.Equal(LevelSource.Who, level.Source);
        Assert.Equal("from /who", CharacterLevel.SourceLabel(level.Source));
        Assert.Equal("", level.UnknownClass);
    }

    /// <summary>The Founder's rule, the /who level is the LOWEST equipped class: a class the
    /// store knows HIGHER stays higher (the row only says "at least N" about it), and one
    /// below rises.</summary>
    [Fact]
    public void AClassAboveTheWhoLevelStaysAndAClassBelowRises()
    {
        var store = new QuestLedgerStore(_path);
        var before = WhoAt.AddDays(-1);
        store.SetLevel(Key, 60, before, ["Warrior"]);
        store.SetLevel(Key, 30, before, ["Druid"]);
        store.SetWho(Key, Reading(50, WhoAt, "Warrior", "Druid", "Monk"));

        var per = store.ClassLevelsFor(Key);
        Assert.Equal(60, per["Warrior"].Level);
        Assert.Equal(50, per["Druid"].Level);
        Assert.Equal(50, per["Monk"].Level);
        Assert.Equal(50, store.ResolvedLevelFor(Key, ["Warrior", "Druid", "Monk"]).Level);
    }

    [Fact]
    public void WhenEveryClassStandsAboveTheWhoLevelTheLowestComesDownToIt()
    {
        var store = new QuestLedgerStore(_path);
        var before = WhoAt.AddDays(-1);
        store.SetLevel(Key, 60, before, ["Warrior"]);
        store.SetLevel(Key, 55, before, ["Druid"]);
        store.SetWho(Key, Reading(52, WhoAt, "Warrior", "Druid"));

        var per = store.ClassLevelsFor(Key);
        Assert.Equal(60, per["Warrior"].Level);
        Assert.Equal(52, per["Druid"].Level);
    }

    /// <summary>Trap 85: the launch replay re-offers every /who in the file, oldest first. An
    /// older row must never undo a newer one — the gate is PERSISTED, so it holds across a
    /// restart too.</summary>
    [Fact]
    public void AnOlderWhoReplayedAtLaunchUndoesNothing()
    {
        var store = new QuestLedgerStore(_path);
        store.SetWho(Key, Reading(50, WhoAt, "Warrior", "Druid", "Monk"));
        store.Flush();

        var reopened = new QuestLedgerStore(_path);
        Assert.False(reopened.SetWho(Key, Reading(20, WhoAt.AddDays(-19), "Warrior", "Cleric", "Enchanter")));
        Assert.False(reopened.SetWho(Key, Reading(50, WhoAt, "Warrior", "Druid", "Monk")));
        Assert.Equal(["Warrior", "Druid", "Monk"], reopened.ClassClaimsFor(Key).Who!.Classes);
        Assert.Equal(50, reopened.ResolvedLevelFor(Key, ["Warrior", "Druid", "Monk"]).Level);
    }

    [Fact]
    public void AFresherDingOnAClassStandsOverAnOlderWho()
    {
        var store = new QuestLedgerStore(_path);
        store.SetLevel(Key, 51, WhoAt.AddHours(1), ["Monk"]);
        store.SetWho(Key, Reading(50, WhoAt, "Warrior", "Druid", "Monk"));
        Assert.Equal(51, store.ClassLevelsFor(Key)["Monk"].Level);
    }

    [Fact]
    public void AStatementAfterTheWhoStandsAndTheNextWhoReplacesIt()
    {
        var store = new QuestLedgerStore(_path);
        store.SetWho(Key, Reading(50, DateTime.Now.AddMinutes(-5), "Warrior", "Druid", "Monk"));
        store.SetStatedClasses(Key, ["Warrior", "Cleric", "Enchanter"]);
        var (stated, statedAt, who) = store.ClassClaimsFor(Key);
        Assert.Equal(ClassSource.Stated, CharacterClasses.Resolve(null, null, null, stated, statedAt, who).Source);

        store.SetWho(Key, Reading(50, DateTime.Now.AddMinutes(5), "Warrior", "Druid", "Monk"));
        (stated, statedAt, who) = store.ClassClaimsFor(Key);
        Assert.Equal(ClassSource.Who, CharacterClasses.Resolve(null, null, null, stated, statedAt, who).Source);
    }

    [Fact]
    public void TheWhoAndTheStatementsStampSurviveARestart()
    {
        var store = new QuestLedgerStore(_path);
        store.SetWho(Key, Reading(50, WhoAt, "Warrior", "Druid", "Monk"));
        store.SetStatedClasses(Key, ["Cleric"]);
        store.Flush();

        var (stated, statedAt, who) = new QuestLedgerStore(_path).ClassClaimsFor(Key);
        Assert.Equal(["Cleric"], stated);
        Assert.NotEqual(default, statedAt);
        Assert.Equal(WhoAt, who!.At);
        Assert.Equal(LevelSource.Who,
            new QuestLedgerStore(_path).ResolvedLevelFor(Key, who.Classes).Source);
    }
}
