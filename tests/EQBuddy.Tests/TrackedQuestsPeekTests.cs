using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The minimized bar's Tracked quests chip and its peek (Founder, 2026-09-29): the quests the
/// player tracked, drawn the way the Guide's Quests tab draws them, each with an Untrack, a
/// link to the Guide, and "No quests being tracked – View Quests" when there are none.
///
/// The peek's promise is that it is the Guide's row and not a second opinion of it, so most
/// of these assert EQUALITY with <see cref="QuestPresentation"/> rather than restating a
/// string — a peek that drifted from the tab would fail here, not in a screenshot.
/// </summary>
public class TrackedQuestsPeekTests
{
    private static QuestCatalog Catalog() => new()
    {
        Quests =
        [
            new QuestEntry { Name = "Belt Collector", StartZone = "Crushbone", QuestGiver = "Retlon",
                Items = [new QuestItemNeed { Name = "Crushbone Belt", Qty = 4 }] },
            new QuestEntry { Name = "Bone Ritual", Items =
                [new QuestItemNeed { Name = "Bone Chips", Qty = 2 },
                 new QuestItemNeed { Name = "Gnoll Fang", Qty = 1 }] },
            // A dialogue quest: no turn-in items at all.
            new QuestEntry { Name = "Words of the Wise", StartZone = "Qeynos" },
            new QuestEntry { Name = "Unrelated", Items =
                [new QuestItemNeed { Name = "Fish Scales", Qty = 1 }] },
        ],
    };

    private static Dictionary<string, QuestLedgerStore.Entry> Owned(params (string Item, int N)[] items)
        => items.ToDictionary(i => i.Item, i => new QuestLedgerStore.Entry { Looted = i.N },
            StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> Tracked(params string[] names) =>
        new(names, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, int> NoneDone = new(StringComparer.OrdinalIgnoreCase);

    // ---- the empty state ----

    /// <summary>The Founder's words, and no rows: the window draws the link after the lead.
    /// </summary>
    [Fact]
    public void NothingTrackedIsTheEmptyState()
    {
        var body = TrackedQuestsPeek.Build(Catalog(), Owned(), Tracked(), NoneDone);

        Assert.True(body.Empty);
        Assert.Equal("No quests being tracked", TrackedQuestsPeek.EmptyLead);
        Assert.Equal("View Quests", TrackedQuestsPeek.ViewQuests);
    }

    // ---- the rows ARE the Guide's rows ----

    /// <summary>
    /// PREDICTION: tracking Belt Collector with 1 of 4 belts gives one row whose badge,
    /// meta line and gauge are exactly what the Quests tab draws — "1/1" (one distinct item,
    /// owned), the tab's meta line, a full gauge (1 of 1 distinct items) — and the untracked
    /// Bone Ritual, overlap or not, is absent: this is the TRACKED list, not "mine".
    /// </summary>
    [Fact]
    public void ATrackedQuestIsDrawnWithTheGuidesOwnBadgeAndMetaLine()
    {
        var owned = Owned(("Crushbone Belt", 1), ("Bone Chips", 2));
        var body = TrackedQuestsPeek.Build(Catalog(), owned, Tracked("Belt Collector"), NoneDone,
            _ => "2 zones away");

        var row = Assert.Single(body.Rows);
        var guide = QuestMatcher.Match(Catalog(), owned, Tracked("Belt Collector"))
            .Single(m => m.Quest.Name == "Belt Collector");
        Assert.Equal("Belt Collector", row.Name);
        Assert.Equal(QuestPresentation.BadgeFor(guide, 0), row.Badge);
        Assert.Equal(QuestPresentation.MetaLine(guide.Quest, 0, "2 zones away"), row.Meta);
        Assert.Equal("Crushbone · from Retlon · 2 zones away", row.Meta);
        Assert.Equal(guide.Fraction, row.Share);
        Assert.Equal("Crushbone Belt: 1/4", row.Tooltip);
        Assert.Equal("1 tracked", body.Subtext);
    }

    /// <summary>A quest ready to turn in says so in the subtext, in the Guide's own
    /// sentence.</summary>
    [Fact]
    public void AReadyQuestIsCountedInTheSubtext()
    {
        var body = TrackedQuestsPeek.Build(Catalog(),
            Owned(("Bone Chips", 2), ("Gnoll Fang", 1)),
            Tracked("Bone Ritual", "Belt Collector"), NoneDone);

        Assert.Equal("ready", body.Rows.Single(r => r.Name == "Bone Ritual").Badge.Label);
        Assert.Equal("2 tracked · " + QuestPresentation.ReadySummary(1), body.Subtext);
    }

    /// <summary>
    /// THE MATCHER GAP, at the surface: a tracked quest with NO turn-in items used to be
    /// skipped before the tracked test ran, so a tracked dialogue quest vanished from "mine"
    /// and would have vanished from this peek too. It is a "steps" row with no gauge.
    /// </summary>
    [Fact]
    public void ATrackedQuestWithNoTurnInItemsIsStillListed()
    {
        var body = TrackedQuestsPeek.Build(Catalog(), Owned(), Tracked("Words of the Wise"), NoneDone);

        var row = Assert.Single(body.Rows);
        Assert.Equal("steps", row.Badge.Label);
        Assert.Null(row.Share);
        Assert.Equal("Qeynos", row.Meta);
    }

    /// <summary>The matcher itself: the tracked item-less quest is in, and — the negative —
    /// an UNtracked item-less quest is still out, so "mine" did not start listing every
    /// dialogue quest in the catalog.</summary>
    [Fact]
    public void TheMatcherKeepsTrackedItemlessQuestsAndOnlyThose()
    {
        Assert.Contains(QuestMatcher.Match(Catalog(), Owned(), Tracked("Words of the Wise")),
            m => m.Quest.Name == "Words of the Wise" && m.Tracked);
        Assert.DoesNotContain(QuestMatcher.Match(Catalog(), Owned(), Tracked()),
            m => m.Quest.Name == "Words of the Wise");
    }

    /// <summary>A tracked name the catalog has lost is SHOWN with a sentence rather than
    /// dropped — a pin nobody can see is a pin nobody can untrack (#954's stuck chip, one
    /// surface over).</summary>
    [Fact]
    public void ATrackedNameTheCatalogLostIsShownSoItCanBeUntracked()
    {
        var body = TrackedQuestsPeek.Build(Catalog(), Owned(),
            Tracked("Belt Collector", "Retired Quest"), NoneDone);

        Assert.Equal(["Belt Collector", "Retired Quest"], body.Rows.Select(r => r.Name));
        Assert.Equal(TrackedQuestsPeek.NotInCatalog, body.Rows[1].Meta);
        Assert.Null(body.Rows[1].Share);
    }

    /// <summary>A finished non-repeatable says "done", exactly as its Guide row does.</summary>
    [Fact]
    public void ACompletedQuestReadsDoneLikeTheGuide()
    {
        var done = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Belt Collector"] = 1 };
        var row = Assert.Single(TrackedQuestsPeek.Build(Catalog(), Owned(), Tracked("Belt Collector"), done).Rows);

        Assert.Equal("done", row.Badge.Label);
    }

    // ---- the repaint gate ----

    /// <summary>Trap 72: the phone and the Guide both write the tracked set, and a loot moves
    /// a badge — each must move the signature, or the panel keeps drawing the moment before.
    /// </summary>
    [Fact]
    public void TheSignatureMovesWithTheTrackedSetAndWithProgress()
    {
        var one = TrackedQuestsPeek.Build(Catalog(), Owned(), Tracked("Belt Collector"), NoneDone);
        var two = TrackedQuestsPeek.Build(Catalog(), Owned(), Tracked("Belt Collector", "Bone Ritual"), NoneDone);
        var looted = TrackedQuestsPeek.Build(Catalog(), Owned(("Crushbone Belt", 1)), Tracked("Belt Collector"), NoneDone);
        var same = TrackedQuestsPeek.Build(Catalog(), Owned(), Tracked("Belt Collector"), NoneDone);

        Assert.NotEqual(one.Signature, two.Signature);
        Assert.NotEqual(one.Signature, looted.Signature);
        Assert.Equal(one.Signature, same.Signature);
    }

    [Fact]
    public void TheCapSaysSoAndNamesTheLink()
        => Assert.Equal("…and 3 more — View Quests for the full list", TrackedQuestsPeek.MoreLine(3));

    // ---- the chip, the target and where its link goes ----

    /// <summary>The chip's key round-trips, its words come from the cell table like the
    /// other stat chips, and its vector is the Guide's own flag.</summary>
    [Fact]
    public void TheQuestsTargetReadsBackAndIsNamedLikeItsChip()
    {
        Assert.Equal(HudExpandTarget.Quests, HudExpand.TargetForKey(MiniBarPresentation.QuestsKey));
        Assert.Equal(MiniBarPresentation.QuestsKey, HudExpand.Key(HudExpandTarget.Quests));
        Assert.Equal("Tracked quests", HudExpand.Title(HudExpandTarget.Quests));
        Assert.Equal(MiniBarPresentation.QuestsIcon, HudExpand.Icon(HudExpandTarget.Quests));
        Assert.True(IconPaths.All.ContainsKey(MiniBarPresentation.QuestsIcon));
    }

    /// <summary>The link opens the Guide on its Quests tab — the SAME address the Helper's
    /// quest-catalog door uses — and the tooltip names the tab by the label a player sees.
    /// </summary>
    [Fact]
    public void TheLinkGoesToTheGuidesQuestsTab()
    {
        var destination = HudExpand.DestinationOf(HudExpandTarget.Quests);

        Assert.Equal(HudDestinationHost.Guide, destination.Host);
        Assert.Equal("quests:general", ShellPages.Address(ShellPage.Quests, destination.Tab));
        Assert.Equal("Open the Guide (Quests)", HudExpand.PopOutTip(HudExpandTarget.Quests));
    }

    /// <summary>
    /// The Guide is NAVIGATION, not a pop-out: the EQBuddy window never reports closing to
    /// the bar, so holding the model in Window placement would leave the chip unable to peek
    /// while the Guide stayed open. The negative is every other target, which still pops out
    /// — so this cannot pass by `PopsOut` answering false for everything.
    /// </summary>
    [Fact]
    public void OnlyTheGuideNavigatesEveryOtherDestinationPopsOut()
    {
        foreach (var target in Enum.GetValues<HudExpandTarget>())
            Assert.Equal(target != HudExpandTarget.Quests,
                HudExpand.PopsOut(HudExpand.DestinationOf(target)));
    }

    /// <summary>
    /// The ★ is what puts the chip on the bar, and it is offered in Options (the way OFF the
    /// bar, since untracking the last quest deliberately leaves the chip showing its empty
    /// state). It is drawn by the bar itself — never through `Cell`, which would draw it
    /// blank — and it keeps a place in the order so it can be dragged.
    /// </summary>
    [Fact]
    public void TheQuestsStarPutsTheChipOnTheBarAndIsOfferedInOptions()
    {
        var starred = new AppSettings { MiniStats = [MiniBarPresentation.QuestsKey] };
        var unstarred = new AppSettings { MiniStats = ["kills"] };

        Assert.Contains(MiniBarPresentation.QuestsKey, MiniBarPresentation.DrawnKeys(starred));
        Assert.DoesNotContain(MiniBarPresentation.QuestsKey, MiniBarPresentation.DrawnKeys(unstarred));
        Assert.Contains(MiniBarPresentation.QuestsKey, MiniBarPresentation.OptionKeys);
        Assert.Contains(MiniBarPresentation.QuestsKey, MiniBarPresentation.CanonicalOrder);
        Assert.Null(MiniBarPresentation.Cell(new StatsSnapshot(), MiniBarPresentation.QuestsKey));
    }
}
