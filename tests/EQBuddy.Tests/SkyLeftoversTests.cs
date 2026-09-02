using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// #243, tvongaza: *"when you do an inventory dump, it could cross check which sky quests
/// you've completed and which sky quest items you no longer need as you've completed all
/// the quests which use them. Would help with limited inventory space."*
///
/// Every input existed; the join did not. These tests are about the two things that make a
/// join like this either useful or harmful: the STRENGTH of each claim, and what it refuses
/// to say. A Band A row is the app telling someone an item is finished with — if that is
/// ever wrong, they free a slot and lose a turn-in they cannot get back.
/// </summary>
public class SkyLeftoversTests
{
    // One single-class item, one rune five classes want. The shape that matters: a shared
    // item is only finished when EVERY class's reward that takes it is finished.
    private static List<SkyQuestChecklistItem> Checklist() =>
    [
        new() { ClassName = "Beastlord", QuestItem = "Sphinx Claw", Reward = "Windhowl/Spirit Render" },
        new() { ClassName = "Druid", QuestItem = "Wind Rune Azia", Reward = "Test of Nature" },
        new() { ClassName = "Monk", QuestItem = "Wind Rune Azia", Reward = "Test of Fists" },
        new() { ClassName = "Wizard", QuestItem = "Wind Rune Azia", Reward = "Test of Frost" },
        new() { ClassName = "Bard", QuestItem = "Leather Cord", Reward = "Test of Wind" },
    ];

    private static InventoryFile.Snapshot Dump(params (string Location, string Name, int Count)[] rows)
    {
        var entries = rows.Select(r => new InventoryFile.Entry(r.Location, r.Name, r.Count)).ToList();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            var name = QuestCatalog.BaseItemName(e.Name.TrimEnd('*'));
            counts[name] = counts.GetValueOrDefault(name) + e.Count;
        }
        return new InventoryFile.Snapshot("dump.txt", new DateTime(2026, 9, 2, 8, 0, 0), counts)
        {
            Entries = entries,
        };
    }

    private static string Key(string cls, string reward) => QuestChecklistLayout.RewardKey(cls, reward);

    // ---- Band A: the strong claim -----------------------------------------------------

    [Fact]
    public void ASingleClassItemIsNoLongerNeededOnceItsOwnRewardIsDone()
    {
        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Sphinx Claw", 1)),
            Checklist(),
            [Key("Beastlord", "Windhowl/Spirit Render")],
            ["Beastlord"],
            catalog: null);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Sphinx Claw", row.Item);
        Assert.Equal(SkyLeftoverBand.NoLongerNeeded, row.Band);
        Assert.Equal(1, row.Held);
        Assert.Equal("bags", row.Where);
        Assert.Equal(["Beastlord · Windhowl/Spirit Render"], row.TurnedInRewards);
        Assert.Empty(row.OpenClasses);
    }

    /// <summary>The rune three classes want is NOT finished until all three are. Two of
    /// three done is the case where a merged band would be wrong and confident.</summary>
    [Fact]
    public void ASharedRuneIsNotNoLongerNeededUntilEveryClassRewardIsDone()
    {
        var dump = Dump(("General1-Slot2", "Wind Rune Azia", 1));

        var partial = SkyLeftovers.Compute(dump, Checklist(),
            [Key("Druid", "Test of Nature"), Key("Monk", "Test of Fists")],
            ["Druid", "Monk", "Wizard"], catalog: null);
        Assert.Empty(partial.Rows);

        var all = SkyLeftovers.Compute(dump, Checklist(),
            [Key("Druid", "Test of Nature"), Key("Monk", "Test of Fists"), Key("Wizard", "Test of Frost")],
            ["Druid", "Monk", "Wizard"], catalog: null);
        var row = Assert.Single(all.Rows);
        Assert.Equal(SkyLeftoverBand.NoLongerNeeded, row.Band);
        Assert.Equal(3, row.TurnedInRewards.Count);
    }

    // ---- Band B: the weak claim, and the lens rule -------------------------------------

    /// <summary>#193's rule, inherited deliberately: no lens is not a wildcard. With no
    /// classes known, an item another class wants says NOTHING, because we cannot tell
    /// "not yours" from "we have no idea whose".</summary>
    [Fact]
    public void OtherClassesWantIsNeverProducedWithoutAClassLens()
    {
        var dump = Dump(("General1-Slot2", "Wind Rune Azia", 1));

        var noLens = SkyLeftovers.Compute(dump, Checklist(), [], myClasses: [], catalog: null);
        Assert.Empty(noLens.Rows);

        var withLens = SkyLeftovers.Compute(dump, Checklist(), [], ["Beastlord"], catalog: null);
        var row = Assert.Single(withLens.Rows);
        Assert.Equal(SkyLeftoverBand.OtherClassesWant, row.Band);
        Assert.Equal(["Druid", "Monk", "Wizard"], row.OpenClasses);
    }

    /// <summary>A class you DO have still wanting it outranks the other two: your own open
    /// reward means the item is simply still needed, and it is not listed at all.</summary>
    [Fact]
    public void AnItemYourOwnClassStillWantsIsNotAListedLeftover()
    {
        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot2", "Wind Rune Azia", 1)),
            Checklist(), [Key("Druid", "Test of Nature")],
            ["Druid", "Monk"], catalog: null);

        Assert.Empty(result.Rows);
    }

    // ---- The veto, which is the safety half --------------------------------------------

    /// <summary>Fable's plan flagged this as a hypothesis for the executor to confirm:
    /// a non-Sky catalog quest that takes the item vetoes "no longer needed". It is the
    /// one rule whose absence costs a player something real.</summary>
    [Fact]
    public void ANonSkyQuestWantingTheItemVetoesNoLongerNeeded()
    {
        var catalog = new QuestCatalog
        {
            Quests =
            [
                new QuestEntry { Name = "Bone Chip Turn-in", Items = [new QuestItemNeed { Name = "Sphinx Claw", Qty = 1 }] },
            ],
        };

        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Sphinx Claw", 1)),
            Checklist(), [Key("Beastlord", "Windhowl/Spirit Render")],
            ["Beastlord"], catalog);

        Assert.Empty(result.Rows);
        var (item, quest) = Assert.Single(result.HeldBackByOtherQuests);
        Assert.Equal("Sphinx Claw", item);
        Assert.Equal("Bone Chip Turn-in", quest);
    }

    /// <summary>...and a SPLIT Sky Test quest is the same reward under another name, so it
    /// must not veto itself.
    ///
    /// **Fable's plan flagged this as its one unchecked hypothesis** — *"that
    /// `QuestCatalog.QuestsWanting` returns the split Sky Test quests for Sky items ... the
    /// executor confirms with one test before relying on the veto."* It does, against the
    /// SHIPPED catalog, which is what makes this test worth writing here rather than over a
    /// hand-built one: a hand-built catalog cannot confirm it at all, because
    /// <see cref="SkyTestSplit.Apply"/> only splits a class whose aggregate page is present.
    ///
    /// Without the exclusion every Band A row vanishes — the feature ships, looks correct,
    /// and lists nothing forever.</summary>
    [Fact]
    public void ASplitSkyTestQuestDoesNotVetoItsOwnItem()
    {
        var catalog = QuestCatalog.LoadEmbedded();

        // A Sky item whose only catalog wanters ARE the split Sky Test quests. Both halves
        // asserted: that such quests come back at all (the hypothesis), and that nothing
        // else wants this particular item (so the veto's only possible source is the split).
        var item = SkyQuestDefaults.Items
            .Select(i => QuestCatalog.BaseItemName(i.QuestItem))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(name =>
            {
                var wanters = catalog.QuestsWanting(name);
                return wanters.Count > 0 &&
                       wanters.All(q => SkyTestSplit.RewardKeyFor(q.Name).Length > 0);
            });
        Assert.NotNull(item);

        var checklist = SkyQuestDefaults.Items
            .Where(i => QuestCatalog.BaseItemName(i.QuestItem)
                .Equals(item, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var completed = checklist
            .Select(i => QuestChecklistLayout.RewardKey(i.ClassName, i.Reward))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", item!, 1)), checklist, completed,
            [checklist[0].ClassName], catalog);

        Assert.Empty(result.HeldBackByOtherQuests);
        Assert.Equal(SkyLeftoverBand.NoLongerNeeded, Assert.Single(result.Rows).Band);
    }

    // ---- Name folding, holdings, and the states that must say nothing ------------------

    /// <summary>The dump writes "Wind Rune Azia*" for attuned and "+1" for upgrade tiers;
    /// the checklist writes neither. Both sides fold through
    /// <see cref="QuestCatalog.BaseItemName"/>, or the join matches nothing and the feature
    /// looks like it simply has no rows — trap 23's shape in a pure function.</summary>
    [Fact]
    public void BaseNamesFoldOnBothSidesSoAttunedAndUpgradedRowsStillMatch()
    {
        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Sphinx Claw*", 1), ("General2-Slot1", "Sphinx Claw +1", 1)),
            Checklist(), [Key("Beastlord", "Windhowl/Spirit Render")],
            ["Beastlord"], catalog: null);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Sphinx Claw", row.Item);
        Assert.Equal(2, row.Held);
    }

    [Fact]
    public void AnItemYouDoNotHoldIsNeverListedHoweverFinishedItIs()
    {
        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Bone Chips", 12)),
            Checklist(), [Key("Beastlord", "Windhowl/Spirit Render")],
            ["Beastlord"], catalog: null);

        Assert.Empty(result.Rows);
    }

    /// <summary>No dump read yet is not "you have nothing left over" — it is no answer.
    /// The two look identical in a count and only one of them is a fact.</summary>
    [Fact]
    public void NoDumpMeansNoRowsRatherThanAnEmptyClaim()
    {
        var result = SkyLeftovers.Compute(null, Checklist(),
            [Key("Beastlord", "Windhowl/Spirit Render")], ["Beastlord"], catalog: null);

        Assert.Empty(result.Rows);
        Assert.Empty(result.HeldBackByOtherQuests);
    }

    /// <summary>The band reads <c>SkyQuestCompleted</c> live, so re-opening a reward moves
    /// its item straight back out of Band A. This is the contract the Ready band already
    /// has, and it is what makes the list safe to act on.</summary>
    [Fact]
    public void ReopeningARewardMovesItsItemBackOutOfNoLongerNeeded()
    {
        var dump = Dump(("General1-Slot1", "Sphinx Claw", 1));
        var completed = new List<string> { Key("Beastlord", "Windhowl/Spirit Render") };

        Assert.Single(SkyLeftovers.Compute(dump, Checklist(), completed, ["Beastlord"], null).Rows);

        completed.Clear();   // the Reopen click
        Assert.Empty(SkyLeftovers.Compute(dump, Checklist(), completed, ["Beastlord"], null).Rows);
    }

    /// <summary>Bank vs bags: the ask was bag SPACE, so a row has to say which. An item in
    /// both places says both — the player decides which copy to drop.</summary>
    [Theory]
    [InlineData("General1-Slot1", "bags")]
    [InlineData("Bank1", "bank")]
    public void WhereNamesBagsOrBank(string location, string expected)
    {
        var result = SkyLeftovers.Compute(
            Dump((location, "Sphinx Claw", 1)), Checklist(),
            [Key("Beastlord", "Windhowl/Spirit Render")], ["Beastlord"], null);

        Assert.Equal(expected, Assert.Single(result.Rows).Where);
    }

    [Fact]
    public void AnItemHeldInBothPlacesSaysBoth()
    {
        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Sphinx Claw", 1), ("Bank3", "Sphinx Claw", 1)),
            Checklist(), [Key("Beastlord", "Windhowl/Spirit Render")], ["Beastlord"], null);

        Assert.Equal("bags and bank", Assert.Single(result.Rows).Where);
    }

    // ---- What the import report says, which is where the reporter meets this ----------
    //
    // "when you do an inventory dump" is his literal moment, and the report already reaches
    // the Gear tab AND the Sky tab (ImportReportReachesASurfaceTests' second-host list), so
    // the count lands where the dump lands with no new surface. Trap 43's rule: the value
    // has a reader on day one.

    [Fact]
    public void TheInventorySummaryNamesTheCountAndTheHoverNamesTheItems()
    {
        var outcome = new AutoImportOutcome(
            OutputfileKind.Inventory, "Testchar_test-Inventory.txt",
            new DateTime(2026, 9, 2, 8, 5, 0), GearTicked: 0, RaidsMarked: 0, SkyMarked: 0)
        {
            SkyLeftoverItems = ["Mithril Bands", "Sphinx Claw"],
        };

        Assert.Equal(2, outcome.SkyLeftovers);
        Assert.Equal(
            "Read your inventory dump (08:05) — nothing new to tick · 2 Sky items no longer needed.",
            outcome.Summary);
        Assert.Contains("Mithril Bands, Sphinx Claw", outcome.Detail);
        Assert.Contains("Nothing has been sold, destroyed or ticked", outcome.Detail);

        // Not a warning. Finding free bag space is the one piece of good news an import has,
        // and Noted is what paints the line amber on both lanes' ImportReportView.
        Assert.Equal(0, outcome.Noted);
    }

    [Fact]
    public void OneLeftoverIsSingularAndNoneAddsNothingAtAll()
    {
        var at = new DateTime(2026, 9, 2, 8, 5, 0);
        var one = new AutoImportOutcome(OutputfileKind.Inventory, "d.txt", at, 2, 0, 0)
        {
            SkyLeftoverItems = ["Sphinx Claw"],
        };
        Assert.Equal(
            "Read your inventory dump (08:05) — 2 items ticked · 1 Sky item no longer needed.",
            one.Summary);

        // The pre-#243 sentence, unchanged to the character — the clause appears only when
        // there is something to say.
        var none = new AutoImportOutcome(OutputfileKind.Inventory, "d.txt", at, 2, 0, 0);
        Assert.Equal("Read your inventory dump (08:05) — 2 items ticked.", none.Summary);
        Assert.Null(none.Detail);
    }

    /// <summary>Band A before Band B, then alphabetical — a repeated dump lands the same
    /// rows in the same order, and the strong claim leads.</summary>
    [Fact]
    public void RowsAreOrderedStrongClaimFirstThenAlphabetically()
    {
        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Sphinx Claw", 1), ("General1-Slot2", "Wind Rune Azia", 1)),
            Checklist(), [Key("Beastlord", "Windhowl/Spirit Render")],
            ["Beastlord"], catalog: null);

        Assert.Equal(
            [SkyLeftoverBand.NoLongerNeeded, SkyLeftoverBand.OtherClassesWant],
            result.Rows.Select(r => r.Band));
        Assert.Equal(1, result.NoLongerNeeded);
        Assert.Equal(1, result.OtherClassesWant);
    }

    // ---- the WORDS, said once for all three surfaces (PR 1) ----------------------------
    //
    // The desktop bands, and the phone group after them, are three renderers of ONE
    // decision. A row format or a heading hand-copied into three files is exactly what
    // drifted before #184, and here the words carry the honesty: band B under band A's
    // heading would be the app telling someone an item is finished with when it is not.

    [Fact]
    public void TheRowAndTheHeadingsAreSaidOnceForEverySurface()
    {
        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Sphinx Claw", 1),
                 ("Bank2", "Sphinx Claw", 2),
                 ("General1-Slot2", "Wind Rune Azia", 1)),
            Checklist(), [Key("Beastlord", "Windhowl/Spirit Render")],
            ["Beastlord"], catalog: null);

        Assert.Equal("No longer needed — 1", result.NoLongerNeededHeading);
        Assert.Equal("Other classes still want — 1", result.OtherClassesWantHeading);

        var a = Assert.Single(result.RowsIn(SkyLeftoverBand.NoLongerNeeded));
        Assert.Equal("Sphinx Claw ×3 · bags and bank", a.Line);
        Assert.Equal(
            "Every Sky reward that takes it is turned in: Beastlord · Windhowl/Spirit Render.",
            a.Detail);

        var b = Assert.Single(result.RowsIn(SkyLeftoverBand.OtherClassesWant));
        Assert.Equal("Wind Rune Azia ×1 · bags", b.Line);
        // "Not yours", never "junk" — a Legends character can unlock the class later, and
        // saying so is the difference the two bands exist to keep.
        //
        // Asserted whole, and the LEAD is the point (Bevel, Helm-signed 2026-09-02): the
        // phone draws this as one ellipsised line, so a `Contains` of the caveat cannot
        // tell "leads with it" from "trails it and gets cut" — which is the defect this
        // wording fixed. Order is only pinned by pinning the front of the string.
        Assert.Equal(
            "Not yours — still wanted by Druid, Monk, Wizard; "
                + "a Legends character can unlock one later.",
            b.Detail);
        Assert.StartsWith("Not yours", b.Detail);
        Assert.DoesNotContain("no longer needed", b.Detail, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The reorder kept the evidence it was carrying. Band B's hover has a second
    /// half when some of the item's rewards ARE done — and a suffix nothing asserts is
    /// exactly the thing a rewrite drops in silence (trap 26's shape: the fold moved the
    /// string and the tail went missing). It stays BEHIND the caveat on purpose: the phone
    /// truncates, so the least load-bearing end is the end that should be eaten.</summary>
    [Fact]
    public void BandBKeepsItsAlreadyTurnedInEvidenceAfterTheCaveat()
    {
        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Wind Rune Azia", 1)),
            Checklist(),
            [Key("Druid", "Test of Nature")],
            ["Bard"],                       // a class none of the open rewards belong to
            catalog: null);

        var b = Assert.Single(result.RowsIn(SkyLeftoverBand.OtherClassesWant));
        Assert.Equal(
            "Not yours — still wanted by Monk, Wizard; "
                + "a Legends character can unlock one later."
                + "\n\nAlready turned in: Druid · Test of Nature.",
            b.Detail);
    }

    /// <summary>The heading's count and the rows under it come from ONE list, so they
    /// cannot disagree (trap 4 in miniature — the failure that produces a band headed
    /// "2" with three rows in it).</summary>
    [Fact]
    public void EachHeadingCountsExactlyTheRowsItsBandWillDraw()
    {
        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Sphinx Claw", 1), ("General1-Slot2", "Wind Rune Azia", 1)),
            Checklist(), [Key("Beastlord", "Windhowl/Spirit Render")],
            ["Beastlord"], catalog: null);

        Assert.EndsWith(
            $"— {result.RowsIn(SkyLeftoverBand.NoLongerNeeded).Count}",
            result.NoLongerNeededHeading);
        Assert.EndsWith(
            $"— {result.RowsIn(SkyLeftoverBand.OtherClassesWant).Count}",
            result.OtherClassesWantHeading);
        Assert.False(result.IsEmpty);
        Assert.True(SkyLeftoversResult.Empty.IsEmpty);
    }

    /// <summary>An item a NON-Sky quest still wants is deliberately in neither band — and
    /// the band says so rather than letting it simply vanish. An item missing with no
    /// reason reads as a bug in the join; naming the quest is the sentence that stops
    /// someone selling it.</summary>
    [Fact]
    public void TheHeldBackNoteNamesTheQuestAndDisappearsWhenThereIsNone()
    {
        var catalog = new QuestCatalog
        {
            Quests =
            [
                new QuestEntry
                {
                    Name = "Blackburrow Brewers",
                    Items = [new QuestItemNeed { Name = "Sphinx Claw", Qty = 1 }],
                },
            ],
        };
        var result = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Sphinx Claw", 1), ("General1-Slot2", "Wind Rune Azia", 1)),
            Checklist(), [Key("Beastlord", "Windhowl/Spirit Render")],
            ["Beastlord"], catalog);

        Assert.Empty(result.RowsIn(SkyLeftoverBand.NoLongerNeeded));
        Assert.Equal(
            "1 more is still wanted by another quest: Sphinx Claw (Blackburrow Brewers).",
            result.HeldBackNote);

        // Nothing vetoed, nothing said — the note is a fact, not furniture.
        var clean = SkyLeftovers.Compute(
            Dump(("General1-Slot1", "Sphinx Claw", 1)), Checklist(),
            [Key("Beastlord", "Windhowl/Spirit Render")], ["Beastlord"], catalog: null);
        Assert.Equal("", clean.HeldBackNote);
    }
}
