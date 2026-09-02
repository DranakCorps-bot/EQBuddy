using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// #243, tvongaza: *"cross check which sky quests you've completed and which sky quest items
/// you no longer need as you've completed all the quests which use them."*
///
/// The join, and the two honesty limits that make it safe to print. **Band A is a claim about
/// the whole game** — every Sky reward that takes this item is turned in — and **band B is a
/// claim about this character's classes**, which is weaker because a Legends character unlocks
/// classes later. They are separate bands with separate headings on purpose (Bevel,
/// Helm-signed 2026-09-02); mixing B under "No longer needed" is the one presentation that
/// would make the feature lie.
///
/// The third limit is the veto: another quest in the catalog wanting the item keeps it out of
/// band A entirely. Nothing here destroys anything, so the only cost this feature can impose
/// on a player is a wrong "you can free that" about an item something else needed.
/// </summary>
public class SkyLeftoversTests
{
    /// <summary>Built through the REAL parser from the dump's own tab-separated shape, not a
    /// hand-shaped substitute (trap 23) — the location strings are what decide "bags" vs
    /// "bank", so a fixture that skipped <see cref="InventoryFile.ParseEntries"/> would be
    /// testing a different reader than the app uses.</summary>
    private static InventoryFile.Snapshot Dump(params string[] rows)
    {
        var lines = new[] { "Location\tName\tID\tCount\tSlots" }.Concat(rows).ToList();
        return new InventoryFile.Snapshot("Dranak_freeport-Inventory.txt",
            new DateTime(2026, 9, 2, 8, 0, 0), InventoryFile.Parse(lines))
        { Entries = InventoryFile.ParseEntries(lines) };
    }

    private static string Row(string location, string name, int count = 1) =>
        $"{location}\t{name}\t12345\t{count}\t0";

    private static List<SkyQuestChecklistItem> Checklist() =>
    [
        // One class wants it, one reward.
        new() { Id = "a", ClassName = "Beastlord", Reward = "Windhowl", QuestItem = "Sphinx Claw" },
        // Two classes want the same rune.
        new() { Id = "b", ClassName = "Beastlord", Reward = "Azarack Skin Wristwraps", QuestItem = "Wind Rune Heda" },
        new() { Id = "c", ClassName = "Bard", Reward = "Harmonic Spear", QuestItem = "Wind Rune Heda" },
        // An open Beastlord reward, so its item is never a leftover.
        new() { Id = "d", ClassName = "Beastlord", Reward = "Griffin-Hide Armguards", QuestItem = "Leather Cord" },
    ];

    private static string Key(string cls, string reward) => QuestChecklistLayout.RewardKey(cls, reward);

    // ---- Band A: the strong claim -------------------------------------------------

    [Fact]
    public void ASingleClassItemIsNoLongerNeededOnceItsOneRewardIsDone()
    {
        var report = SkyLeftovers.Compute(
            Dump(Row("General 1-Slot2", "Sphinx Claw")), Checklist(),
            [Key("Beastlord", "Windhowl")], ["Beastlord"], catalog: null);

        var row = Assert.Single(report.NoLongerNeeded);
        Assert.Equal("Sphinx Claw", row.Item);
        Assert.Equal(1, row.Held);
        Assert.Equal("bags", row.Where);
        Assert.Equal(SkyLeftoverBand.NoLongerNeeded, row.Band);
        // The tooltip's evidence: the claim is only as good as the reward behind it.
        Assert.Equal(["Beastlord · Windhowl"], row.UsedBy);
        Assert.Empty(row.StillWantedBy);
        Assert.Empty(report.OtherClassesWant);
    }

    /// <summary>The whole point of the band, and the reason band B has to exist: a shared
    /// rune is "no longer needed" only when EVERY class's reward that takes it is done. One
    /// of six is not five sixths of a claim, it is a different claim.</summary>
    [Fact]
    public void ASharedRuneReachesBandAOnlyWhenEveryClassesRewardIsDone()
    {
        var checklist = Checklist();
        var dump = Dump(Row("General 2-Slot1", "Wind Rune Heda"));

        var oneDone = SkyLeftovers.Compute(dump, checklist,
            [Key("Beastlord", "Azarack Skin Wristwraps")], ["Beastlord"], catalog: null);
        Assert.Empty(oneDone.NoLongerNeeded);

        var bothDone = SkyLeftovers.Compute(dump, checklist,
            [Key("Beastlord", "Azarack Skin Wristwraps"), Key("Bard", "Harmonic Spear")],
            ["Beastlord"], catalog: null);
        Assert.Equal(["Wind Rune Heda"], bothDone.NoLongerNeeded.Select(r => r.Item));
    }

    /// <summary>Against the SHIPPED table rather than a four-row fixture: Wind Rune Heda is
    /// wanted by six classes, so five completions is still not band A. A fixture can be built
    /// to agree with the code; the real data cannot.</summary>
    [Fact]
    public void TheShippedTableAgrees_HedaNeedsAllSixClassesBeforeItIsNoLongerNeeded()
    {
        var checklist = SkyQuestDefaults.Items.Select(i => i.Clone()).ToList();
        var wanters = checklist
            .Where(i => i.QuestItem == "Wind Rune Heda")
            .Select(i => Key(i.ClassName, i.Reward)).Distinct().ToList();
        Assert.Equal(6, wanters.Count);

        var dump = Dump(Row("General 1-Slot4", "Wind Rune Heda", 2));
        Assert.Empty(SkyLeftovers
            .Compute(dump, checklist, wanters.Take(5).ToList(), [], catalog: null).NoLongerNeeded);

        var all = SkyLeftovers.Compute(dump, checklist, wanters, [], catalog: null);
        var row = Assert.Single(all.NoLongerNeeded, r => r.Item == "Wind Rune Heda");
        Assert.Equal(2, row.Held);
        Assert.Equal(6, row.UsedBy.Count);
    }

    /// <summary>A reward the player un-marks (Reopen) is open again, and the item leaves the
    /// band on the next render. The Ready band and this one read the same live
    /// <c>SkyQuestCompleted</c>, so this is the contract, not a nicety.</summary>
    [Fact]
    public void ReopeningARewardTakesTheItemBackOutOfBandA()
    {
        var completed = new List<string> { Key("Beastlord", "Windhowl") };
        var dump = Dump(Row("General 1-Slot2", "Sphinx Claw"));
        Assert.Single(SkyLeftovers.Compute(dump, Checklist(), completed, ["Beastlord"], null).NoLongerNeeded);

        completed.Clear();
        Assert.Empty(SkyLeftovers.Compute(dump, Checklist(), completed, ["Beastlord"], null).NoLongerNeeded);
    }

    /// <summary>The #101 guard refuses to mark a reward whose class unlock was GRANTED rather
    /// than earned, so that reward never reaches <c>SkyQuestCompleted</c> — and this join
    /// inherits the refusal without knowing about it. Such an item stays needed, correctly:
    /// the items never existed, so the turn-in is still ahead of the player.</summary>
    [Fact]
    public void ARewardTheAchievementsGuardSkippedStillCountsAsOpen()
    {
        var report = SkyLeftovers.Compute(
            Dump(Row("General 1-Slot2", "Sphinx Claw")), Checklist(),
            completedRewardKeys: [], ["Beastlord"], catalog: null);

        Assert.Empty(report.NoLongerNeeded);
        Assert.Empty(report.OtherClassesWant);
    }

    // ---- Band B: the weaker claim, and the lens rule -------------------------------

    [Fact]
    public void OnlyOtherClassesWantingItIsItsOwnBandNeverBandA()
    {
        var report = SkyLeftovers.Compute(
            Dump(Row("General 3-Slot1", "Wind Rune Heda")), Checklist(),
            [Key("Beastlord", "Azarack Skin Wristwraps")], ["Beastlord"], catalog: null);

        Assert.Empty(report.NoLongerNeeded);
        var row = Assert.Single(report.OtherClassesWant);
        Assert.Equal("Wind Rune Heda", row.Item);
        Assert.Equal(SkyLeftoverBand.OtherClassesWant, row.Band);
        Assert.Equal(["Bard"], row.StillWantedBy);
        Assert.Equal(["Beastlord · Azarack Skin Wristwraps"], row.UsedBy);
    }

    /// <summary>#193's rule, one surface over: <b>no lens is not a wildcard.</b> Without the
    /// classes this character holds there is no "other" to be other than, so band B is not
    /// produced at all — the item is simply still wanted, and not listed.</summary>
    [Fact]
    public void WithNoClassLensBandBIsNeverProduced()
    {
        var report = SkyLeftovers.Compute(
            Dump(Row("General 3-Slot1", "Wind Rune Heda")), Checklist(),
            [Key("Beastlord", "Azarack Skin Wristwraps")], myClasses: [], catalog: null);

        Assert.Empty(report.OtherClassesWant);
        Assert.Empty(report.NoLongerNeeded);
        Assert.True(report.IsEmpty);
    }

    /// <summary>Band A is a statement about the game, so the lens must not touch it: an item
    /// nobody in the world still wants is no longer needed whether or not the ⚙ picks are
    /// set. Only band B is lens-gated.</summary>
    [Fact]
    public void BandAIsUnaffectedByTheClassLens()
    {
        var report = SkyLeftovers.Compute(
            Dump(Row("General 1-Slot2", "Sphinx Claw")), Checklist(),
            [Key("Beastlord", "Windhowl")], myClasses: [], catalog: null);

        Assert.Equal(["Sphinx Claw"], report.NoLongerNeeded.Select(r => r.Item));
    }

    [Fact]
    public void AnOpenRewardOfMyOwnClassIsNotALeftoverInEitherBand()
    {
        var report = SkyLeftovers.Compute(
            Dump(Row("General 1-Slot1", "Leather Cord")), Checklist(),
            [Key("Beastlord", "Windhowl")], ["Beastlord"], catalog: null);

        Assert.True(report.IsEmpty);
    }

    // ---- The veto ------------------------------------------------------------------

    /// <summary>Fable's plan carried this as a HYPOTHESIS: that <c>QuestsWanting</c> returns
    /// the split Sky Test quests for a Sky item, which is what makes the veto need its filter
    /// at all. Confirmed here against the real catalog before anything relies on it — a veto
    /// that vetoed every row would empty band A permanently and look like "no leftovers".</summary>
    [Fact]
    public void TheRealCatalogsSkyTestQuestsDoWantSkyItems_WhichIsWhyTheVetoFiltersThem()
    {
        var catalog = QuestCatalog.LoadEmbedded();
        var wanting = catalog.QuestsWanting("Wind Rune Heda");

        Assert.NotEmpty(wanting);
        Assert.Contains(wanting, q => SkyTestSplit.RewardKeyFor(q.Name).Length > 0);
    }

    [Fact]
    public void AnotherQuestWantingTheItemKeepsItOutOfBandAAndIsExplained()
    {
        var catalog = new QuestCatalog
        {
            Quests =
            [
                new QuestEntry { Name = "Beastlord Sky Test: Windhowl", Items = [new() { Name = "Sphinx Claw" }] },
                new QuestEntry { Name = "Sphinx Fur Collar", Items = [new() { Name = "Sphinx Claw" }] },
            ],
        };

        var report = SkyLeftovers.Compute(
            Dump(Row("General 1-Slot2", "Sphinx Claw")), Checklist(),
            [Key("Beastlord", "Windhowl")], ["Beastlord"], catalog);

        Assert.Empty(report.NoLongerNeeded);
        var held = Assert.Single(report.WantedByAnotherQuest);
        Assert.Equal("Sphinx Claw", held.Item);
        Assert.Equal(["Sphinx Fur Collar"], held.Quests);
        // Not silently absent: the band's hover says why the item it expected is missing.
        Assert.Equal("1 more is still wanted by another quest: Sphinx Claw (Sphinx Fur Collar).",
            report.OtherQuestNote);
    }

    /// <summary>The split Sky Tests ARE the checklist. If they counted as "another quest",
    /// every row would veto itself and the band could never contain anything.</summary>
    [Fact]
    public void TheSkyTestsThemselvesDoNotVetoTheirOwnItems()
    {
        var catalog = new QuestCatalog
        {
            Quests = [new QuestEntry { Name = "Beastlord Sky Test: Windhowl", Items = [new() { Name = "Sphinx Claw" }] }],
        };

        var report = SkyLeftovers.Compute(
            Dump(Row("General 1-Slot2", "Sphinx Claw")), Checklist(),
            [Key("Beastlord", "Windhowl")], ["Beastlord"], catalog);

        Assert.Equal(["Sphinx Claw"], report.NoLongerNeeded.Select(r => r.Item));
        Assert.Empty(report.WantedByAnotherQuest);
        Assert.Equal("", report.OtherQuestNote);
    }

    // ---- What the dump says ---------------------------------------------------------

    [Fact]
    public void AnItemTheDumpDoesNotHoldIsNeverListed()
    {
        var report = SkyLeftovers.Compute(
            Dump(Row("General 1-Slot2", "Bone Chips", 47)), Checklist(),
            [Key("Beastlord", "Windhowl")], ["Beastlord"], catalog: null);

        Assert.True(report.IsEmpty);
    }

    [Fact]
    public void NoDumpAtAllIsAnEmptyReportRatherThanAnEmptyBand()
    {
        var report = SkyLeftovers.Compute(null, Checklist(),
            [Key("Beastlord", "Windhowl")], ["Beastlord"], catalog: null);

        Assert.True(report.IsEmpty);
        Assert.Empty(report.WantedByAnotherQuest);
    }

    /// <summary>Both sides fold through <see cref="QuestCatalog.BaseItemName"/>, and the
    /// dump's cosmetic attuned `*` is stripped by the parser — the same folding that took
    /// 1.85.0 to reach the Sky checker at all (#156).</summary>
    [Fact]
    public void UpgradedAndAttunedRowsFoldToTheBaseItem()
    {
        var report = SkyLeftovers.Compute(
            Dump(Row("General 1-Slot2", "Sphinx Claw +1"), Row("Bank1-Slot3", "Sphinx Claw*")),
            Checklist(), [Key("Beastlord", "Windhowl")], ["Beastlord"], catalog: null);

        var row = Assert.Single(report.NoLongerNeeded);
        // The checklist's spelling, not "Sphinx Claw +1" — the wiki name every other
        // surface prints.
        Assert.Equal("Sphinx Claw", row.Item);
        Assert.Equal(2, row.Held);
        Assert.Equal("bags and bank", row.Where);
    }

    /// <summary>The ask is bag SPACE, so where the item is sitting is part of the answer —
    /// an item in the bank is not the problem he has. The shared bank is a bank
    /// (<see cref="InventoryFile.Entry.InBank"/>), which a plain "Bank" prefix test misses.</summary>
    [Theory]
    [InlineData("General 4-Slot1", "bags")]
    [InlineData("General 4", "bags")]
    [InlineData("Bank2-Slot7", "bank")]
    [InlineData("SharedBank1", "bank")]
    [InlineData("Primary", "worn")]
    public void WhereCollapsesToBagsBankOrWorn(string location, string expected)
    {
        var report = SkyLeftovers.Compute(
            Dump(Row(location, "Sphinx Claw")), Checklist(),
            [Key("Beastlord", "Windhowl")], ["Beastlord"], catalog: null);

        Assert.Equal(expected, Assert.Single(report.NoLongerNeeded).Where);
    }

    /// <summary>An item the LOG saw arrive after the dump was written is in
    /// <c>Counts</c> and in no <c>Entry</c>, so there is no location to claim. The row drops
    /// the clause rather than guessing a bag.</summary>
    [Fact]
    public void AnItemGainedSinceTheDumpIsListedWithoutAPlace()
    {
        var report = SkyLeftovers.Compute(
            Dump().WithChanges(new() { ["Sphinx Claw"] = 1 }), Checklist(),
            [Key("Beastlord", "Windhowl")], ["Beastlord"], catalog: null);

        var row = Assert.Single(report.NoLongerNeeded);
        Assert.Equal("", row.Where);
        Assert.Equal("Sphinx Claw ×1", row.Line);
    }

    // ---- What the three surfaces draw -----------------------------------------------

    /// <summary>The row and the two headings live in Core because WPF, Avalonia and the
    /// phone are three renderers of one decision, and this is exactly the formatting that
    /// drifts between them. Band B must never wear band A's words.</summary>
    [Fact]
    public void TheRowAndTheHeadingsAreSaidOnceForAllThreeSurfaces()
    {
        var report = SkyLeftovers.Compute(
            Dump(Row("General 1-Slot2", "Sphinx Claw", 3), Row("General 3-Slot1", "Wind Rune Heda")),
            Checklist(),
            [Key("Beastlord", "Windhowl"), Key("Beastlord", "Azarack Skin Wristwraps")],
            ["Beastlord"], catalog: null);

        Assert.Equal("Sphinx Claw ×3 · bags", Assert.Single(report.NoLongerNeeded).Line);
        Assert.Equal("Wind Rune Heda ×1 · bags", Assert.Single(report.OtherClassesWant).Line);
        Assert.Equal("No longer needed — 1", report.NoLongerNeededHeading);
        Assert.Equal("Other classes still want — 1", report.OtherClassesWantHeading);
        Assert.DoesNotContain("Wind Rune Heda", report.NoLongerNeededHeading);
    }

    [Fact]
    public void RowsAreAlphabeticalSoARepeatedLookLandsInTheSamePlace()
    {
        var checklist = Checklist();
        checklist.Add(new SkyQuestChecklistItem
        { Id = "e", ClassName = "Beastlord", Reward = "Windhowl", QuestItem = "Brass Knuckles" });

        var report = SkyLeftovers.Compute(
            Dump(Row("General 1-Slot2", "Sphinx Claw"), Row("General 1-Slot3", "Brass Knuckles")),
            checklist, [Key("Beastlord", "Windhowl")], ["Beastlord"], catalog: null);

        Assert.Equal(["Brass Knuckles", "Sphinx Claw"], report.NoLongerNeeded.Select(r => r.Item));
    }
}
