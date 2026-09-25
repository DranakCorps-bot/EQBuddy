using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE TWO WRITERS, THROUGH THE REAL STORE** (DRA-71 D3, plan P3).
///
/// <para><see cref="CharacterLevelTests"/> proves the RULE over two readings handed to it.
/// This file proves the STORE plays by it: that the ding's stamp survives a save, that the
/// statement's stamp is written at all, that the resolution happens under one lock, and that
/// the way back actually clears. A rule that is right and a store that drops one of its two
/// inputs is the same bug from the player's side.</para>
/// </summary>
public class LevelStatementStoreTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"level-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path);

    private const string Key = "dranak_legends";
    private static readonly DateTime Ding = new(2026, 9, 10, 20, 0, 0);

    /// <summary>**Fixture 1 through the store: the ding is newer, so the ding wins.** The
    /// statement is made first, then the game announces — which is the state a player reaches
    /// by correcting EQBuddy and then playing.</summary>
    [Fact]
    public void ADingAfterTheStatementWinsThroughTheStore()
    {
        var store = Store();
        store.SetStatedLevel(Key, 28);
        // The ding's stamp is the LOG's time, so it can be dated deliberately — which is the
        // whole reason the signature takes one instead of stamping DateTime.Now itself.
        store.SetLevel(Key, 31, DateTime.Now.AddMinutes(1));

        var resolved = store.ResolvedLevelFor(Key);
        Assert.Equal(31, resolved.Level);
        Assert.Equal(LevelSource.Observed, resolved.Source);
    }

    /// <summary>**Fixture 2 through the store: the statement is newer, so the statement
    /// wins** — the class-swap case the Founder named.</summary>
    [Fact]
    public void AStatementAfterTheDingWinsThroughTheStore()
    {
        var store = Store();
        store.SetLevel(Key, 31, Ding);
        store.SetStatedLevel(Key, 28);

        var resolved = store.ResolvedLevelFor(Key);
        Assert.Equal(28, resolved.Level);
        Assert.Equal(LevelSource.Stated, resolved.Source);
    }

    /// <summary>
    /// **"Let EQBuddy work it out" returns the line to the log's own reading**, and the log's
    /// reading is still THERE to return to.
    ///
    /// <para>The undo is one click because a correction that could not be taken back would
    /// leave a player who typed it wrong once telling EQBuddy forever — the same argument
    /// <c>HomeReadout.ClearStated</c> carries for classes. Clearing must not disturb the
    /// observed half, which is what the second assertion is for.</para>
    /// </summary>
    [Fact]
    public void ClearingTheStatementReturnsTheLineToTheLog()
    {
        var store = Store();
        store.SetLevel(Key, 31, Ding);
        store.SetStatedLevel(Key, 28);
        store.SetStatedLevel(Key, 0);

        var resolved = store.ResolvedLevelFor(Key);
        Assert.Equal(31, resolved.Level);
        Assert.Equal(LevelSource.Observed, resolved.Source);
        Assert.Null(store.StatedLevelFor(Key));
        Assert.Equal(31, store.LevelFor(Key));
    }

    /// <summary>Both halves survive a reload — the statement, its stamp, and the ding's
    /// stamp — so the resolution after a restart is the resolution before it. This is the
    /// assertion that would have caught a <c>Rekey</c> that forgot the new fields, from the
    /// player's side rather than by reflection.</summary>
    [Fact]
    public void TheResolutionSurvivesARestart()
    {
        var store = Store();
        store.SetLevel(Key, 31, Ding);
        store.SetStatedLevel(Key, 28);
        store.Flush();

        var resolved = new QuestLedgerStore(_path).ResolvedLevelFor(Key);
        Assert.Equal(28, resolved.Level);
        Assert.Equal(LevelSource.Stated, resolved.Source);
    }

    /// <summary>
    /// **A REPLAYED ding does not un-do a statement made after it.**
    ///
    /// <para>Every launch re-offers the whole log oldest-first, so the ding that is already
    /// stored arrives again, carrying the LOG's own stamp. <c>MainWindow</c> gates that on the
    /// OBSERVED reading — number AND moment since DRA-356, so a second class dinging to the
    /// same number still lands — so the re-offer is a no-op and the stamp stays where it was.
    /// If it re-stamped with "now", the replay would silently overturn the player's correction on every restart, which is
    /// the worst version of this bug because it looks like the setting never saved.</para>
    /// </summary>
    [Fact]
    public void AReplayedDingDoesNotOverturnALaterStatement()
    {
        var store = Store();
        store.SetLevel(Key, 31, Ding);
        store.SetStatedLevel(Key, 28);

        // The gate MainWindow applies, spelled out: the same reading, so nothing is offered.
        if (store.ObservedLevelFor(Key) != new LevelReading(31, Ding)) store.SetLevel(Key, 31, Ding);

        Assert.Equal(28, store.ResolvedLevelFor(Key).Level);
        Assert.Equal(Ding, store.ObservedLevelFor(Key)!.Value.At);
    }

    // ---- per-class memory (DRA-356, DRA-352 D4) ------------------------------------------

    private static readonly string[] WarEnc = ["Warrior", "Enchanter"];

    /// <summary>
    /// **THE FOUNDER'S CASE, BY NAME: Warrior 50 + a newly equipped Enchanter at 17 is a
    /// level-17 character, and stating 30 moves the Enchanter to 30 and leaves the Warrior at
    /// 50.** The line then names Enchanter as the lowest.
    /// </summary>
    [Fact]
    public void FoundersWarrior50AndEnchanter17IsSeventeenAndStating30MovesOnlyTheEnchanter()
    {
        var store = Store();
        store.SetLevel(Key, 50, Ding, ["Warrior"]);
        store.SetStatedLevel(Key, 17, WarEnc);   // Enchanter has no memory — it is the minimum

        var seventeen = store.ResolvedLevelFor(Key, WarEnc);
        Assert.Equal(17, seventeen.Level);
        Assert.Equal("Enchanter", seventeen.LowestClass);
        Assert.Equal(50, store.ClassLevelsFor(Key)["Warrior"].Level);

        store.SetStatedLevel(Key, 30, WarEnc);

        var thirty = store.ResolvedLevelFor(Key, WarEnc);
        Assert.Equal(30, thirty.Level);
        Assert.Equal("Enchanter", thirty.LowestClass);
        Assert.Equal(LevelSource.Stated, thirty.Source);
        var classes = store.ClassLevelsFor(Key);
        Assert.Equal(30, classes["Enchanter"].Level);
        Assert.Equal(50, classes["Warrior"].Level);
    }

    /// <summary>A statement LOWERS only the class at the current minimum — the other class,
    /// standing above the pick, is not the subject of it.</summary>
    [Fact]
    public void AStatementBelowEveryClassLowersOnlyTheMinimum()
    {
        var store = Store();
        store.SetLevel(Key, 50, Ding, ["Warrior"]);
        store.SetLevel(Key, 20, Ding.AddMinutes(1), ["Enchanter"]);

        store.SetStatedLevel(Key, 12, WarEnc);

        var classes = store.ClassLevelsFor(Key);
        Assert.Equal(12, classes["Enchanter"].Level);
        Assert.Equal(50, classes["Warrior"].Level);
        Assert.Equal(12, store.ResolvedLevelFor(Key, WarEnc).Level);
    }

    /// <summary>A statement ABOVE a class raises it — every equipped class below the pick
    /// rises, the plan's first arm.</summary>
    [Fact]
    public void AStatementRaisesEveryEquippedClassBelowIt()
    {
        var store = Store();
        store.SetLevel(Key, 20, Ding, WarEnc);

        store.SetStatedLevel(Key, 25, WarEnc);

        var classes = store.ClassLevelsFor(Key);
        Assert.Equal(25, classes["Warrior"].Level);
        Assert.Equal(25, classes["Enchanter"].Level);
    }

    /// <summary>
    /// **A ding never lowers a class.** Warrior stands at 50 by the player's own statement; a
    /// ding to 18 (the Enchanter earning it) is written to the Enchanter and NOT to the
    /// Warrior — even though the ding is fresher, which is exactly the case fresher-wins alone
    /// would get wrong.
    /// </summary>
    [Fact]
    public void ADingIsRaiseOnlyPerClass()
    {
        var store = Store();
        store.SetStatedLevel(Key, 50, ["Warrior"]);
        store.SetStatedLevel(Key, 17, WarEnc);

        store.SetLevel(Key, 18, DateTime.Now.AddMinutes(1), WarEnc);

        var classes = store.ClassLevelsFor(Key);
        Assert.Equal(50, classes["Warrior"].Level);
        Assert.Equal(18, classes["Enchanter"].Level);
        Assert.Equal(LevelSource.Observed, classes["Enchanter"].Source);
        Assert.Equal(18, store.ResolvedLevelFor(Key, WarEnc).Level);
    }

    /// <summary>
    /// **An equipped class with NO memory is never guessed** (trap 73): the answer falls back
    /// to the character's single pair — exactly what a profile written before per-class memory
    /// resolves to — and names the class it could not weigh.
    /// </summary>
    [Fact]
    public void AnEquippedClassWithNoMemoryFallsBackToTheSinglePairAndSaysSo()
    {
        var store = Store();
        store.SetLevel(Key, 50, Ding, ["Warrior"]);

        var resolved = store.ResolvedLevelFor(Key, WarEnc);
        Assert.Equal(50, resolved.Level);
        Assert.Equal("Enchanter", resolved.UnknownClass);
        Assert.Equal("", resolved.LowestClass);

        // The negative: with no roster at all the store answers exactly as before DRA-356.
        Assert.Equal(store.ResolvedLevelFor(Key), store.ResolvedLevelFor(Key, null));
        Assert.Equal("", store.ResolvedLevelFor(Key).UnknownClass);
    }

    /// <summary>The undo clears every class's statement too, so what is left is only what the
    /// log said.</summary>
    [Fact]
    public void ClearingTheStatementClearsEveryClassStatement()
    {
        var store = Store();
        store.SetLevel(Key, 50, Ding, ["Warrior"]);
        store.SetStatedLevel(Key, 17, WarEnc);

        store.SetStatedLevel(Key, 0, WarEnc);

        Assert.Null(store.StatedLevelFor(Key));
        Assert.False(store.ClassLevelsFor(Key).ContainsKey("Enchanter"));
        var back = store.ResolvedLevelFor(Key, WarEnc);
        Assert.Equal(50, back.Level);
        Assert.Equal("Enchanter", back.UnknownClass);
    }

    /// <summary>Per-class memory survives a restart, case-insensitively keyed like every other
    /// name-keyed dictionary in the ledger.</summary>
    [Fact]
    public void PerClassMemorySurvivesARestart()
    {
        var store = Store();
        store.SetLevel(Key, 50, Ding, ["Warrior"]);
        store.SetStatedLevel(Key, 17, WarEnc);
        store.Flush();

        var reloaded = Store();
        var resolved = reloaded.ResolvedLevelFor(Key, ["warrior", "ENCHANTER"]);
        Assert.Equal(17, resolved.Level);
        Assert.Equal("ENCHANTER", resolved.LowestClass);
    }

    /// <summary>An unknown character is Unknown rather than level 0 — "we have not been told"
    /// and "you are level zero" are different answers and only one of them is true.</summary>
    [Fact]
    public void ACharacterNobodyHasSeenIsUnknown()
    {
        Assert.Equal(ResolvedLevel.Unknown, Store().ResolvedLevelFor("nobody_legends"));
        Assert.Null(Store().ObservedLevelFor("nobody_legends"));
        Assert.Null(Store().StatedLevelFor("nobody_legends"));
    }
}
