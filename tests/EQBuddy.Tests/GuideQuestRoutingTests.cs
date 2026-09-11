using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The two homes a harvested guide's "Turn-in pieces" stage routes to (DRA-45, N1):
/// <see cref="GuideProgressHome.LedgerItem"/> for a piece, and
/// <see cref="GuideProgressHome.QuestCompletion"/> for the hand-in.
///
/// <para><b>Neither is a new fact.</b> "Do I hold four Orc Belts" is the count the loot tail,
/// the inventory dump and the quest card's own manual number already agree on; "have I done
/// this quest" is the integer the card's completed toggle writes. A guide that kept its own
/// tick beside either would be the same fact disagreeing with itself on two screens (trap 4) —
/// and the piece row is the sharper case, because the next loot line would contradict the tick
/// within seconds.</para>
///
/// <para>Both halves are asserted (trap 34): the right store moves, AND the guide ledger does
/// not gain a row. And the refusal is asserted as a REFUSAL with a sentence behind it, not as
/// a write that quietly lands somewhere else — a control over a store that ignores it is a
/// silent no-op, which is broken.</para>
/// </summary>
public sealed class GuideQuestRoutingTests : IDisposable
{
    private const string Dranak = "dranak_freeport";
    private const string GuideId = "hq-a-job-for-nanrum";
    private const string QuestName = "A Job for Nanrum";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"quest-ledger-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    /// <summary>The quest the General tab already computes: two pieces, one of them held.</summary>
    private static QuestMatch Quest(int eyesHeld = 0, int torchesHeld = 0) =>
        new(new QuestEntry
            {
                Name = QuestName,
                Items = [new QuestItemNeed { Name = "Fire Beetle Eye", Qty = 3 },
                         new QuestItemNeed { Name = "Torch", Qty = 1 }],
            },
            ItemsHave: (eyesHeld > 0 ? 1 : 0) + (torchesHeld > 0 ? 1 : 0),
            ItemsTotal: 2,
            Items: [new QuestItemProgress("Fire Beetle Eye", 3, eyesHeld),
                    new QuestItemProgress("Torch", 1, torchesHeld)],
            Tracked: false);

    private static GuideObjective Collect(string item) => new()
    {
        Id = $"{GuideId}-item-{item.ToLowerInvariant().Replace(' ', '-')}",
        Order = 1, ObjectiveType = "Collect",
        Title = $"{item} ×3", ShortInstruction = $"Collect {item}",
        Who = "Basher Nanrum", Where = "Grobb", What = $"Collect {item} ×3 for Basher Nanrum.",
        ItemNames = [item], Authoring = GuideAuthoring.Authored,
    };

    private static GuideObjective HandIn() => new()
    {
        Id = $"{GuideId}-handin", Order = 9, ObjectiveType = "TurnIn",
        Title = "Hand in to Basher Nanrum", ShortInstruction = "Hand the pieces over",
        Who = "Basher Nanrum", Where = "Grobb", What = "Hand the pieces to Basher Nanrum.",
        Authoring = GuideAuthoring.Authored,
    };

    // ---- Which store owns it ----------------------------------------------------------

    [Fact]
    public void APieceRowIsTheQuestLedgersCount()
    {
        var target = GuideProgressRouter.TargetFor(Collect("Fire Beetle Eye"), [], [], Quest());
        Assert.Equal(GuideProgressHome.LedgerItem, target.Home);
        Assert.Equal("Fire Beetle Eye", target.LedgerNeed!.Name);
        Assert.Equal(3, target.LedgerNeed.Need);
    }

    [Fact]
    public void TheHandInRowIsTheQuestsCompletionRecord()
    {
        var target = GuideProgressRouter.TargetFor(HandIn(), [], [], Quest());
        Assert.Equal(GuideProgressHome.QuestCompletion, target.Home);
    }

    /// <summary>Without a quest in hand — a curated guide that walks no ordinary quest — the
    /// same two rows fall to the guide ledger, exactly as they did before this home existed.
    /// The new homes are reachable only when the caller supplies the quest, which is what keeps
    /// the Sky and Epic tabs' routing byte-identical.</summary>
    [Fact]
    public void WithNoQuestInHandNeitherRowChangesWhereItUsedToLive()
    {
        Assert.Equal(GuideProgressHome.GuideLedger,
            GuideProgressRouter.TargetFor(Collect("Fire Beetle Eye"), [], [], null).Home);
        Assert.Equal(GuideProgressHome.GuideLedger,
            GuideProgressRouter.TargetFor(HandIn(), [], [], null).Home);
    }

    /// <summary>An item name the quest does not want has no home on the quest's count — a row
    /// mentioning "Rusty Dagger" must not silently become some other piece's box. Falls to the
    /// guide ledger, the same answer the Sky path gives for a miss.</summary>
    [Fact]
    public void APieceRowNamingNothingTheQuestWantsKeepsItsOwnTick()
    {
        var stray = Collect("Rusty Dagger");
        Assert.Equal(GuideProgressHome.GuideLedger,
            GuideProgressRouter.TargetFor(stray, [], [], Quest()).Home);
    }

    /// <summary>Two matches is an ambiguous claim on a shared count, and an ambiguous claim on
    /// a store four surfaces read is worse than a private tick.</summary>
    [Fact]
    public void APieceRowNamingTwoOfTheQuestsItemsKeepsItsOwnTick()
    {
        var greedy = Collect("Fire Beetle Eye");
        greedy.ItemNames = ["Fire Beetle Eye", "Torch"];
        Assert.Equal(GuideProgressHome.GuideLedger,
            GuideProgressRouter.TargetFor(greedy, [], [], Quest()).Home);
    }

    /// <summary>A step that merely MENTIONS an item is not that item's box — the same gate
    /// <see cref="GuideProgressHome.SkyItem"/> has. "Travel to Grobb, where Fire Beetle Eyes
    /// sell" is not "collect three of them".</summary>
    [Fact]
    public void OnlyAnAcquireShapedRowMayReadTheQuestsCount()
    {
        var travel = Collect("Fire Beetle Eye");
        travel.ObjectiveType = "Travel";
        Assert.Equal(GuideProgressHome.GuideLedger,
            GuideProgressRouter.TargetFor(travel, [], [], Quest()).Home);
    }

    // ---- Reading -----------------------------------------------------------------------

    [Fact]
    public void APieceRowIsDoneWhenTheLedgerSaysYouHoldEnough()
    {
        var settings = new AppSettings();
        var ledger = Store();
        var row = Collect("Fire Beetle Eye");

        Assert.False(IsDone(settings, ledger, row, Quest(eyesHeld: 2)));
        Assert.True(IsDone(settings, ledger, row, Quest(eyesHeld: 3)));
        Assert.True(IsDone(settings, ledger, row, Quest(eyesHeld: 9)));
    }

    [Fact]
    public void TheHandInRowIsDoneWhenTheQuestIsRecordedComplete()
    {
        var settings = new AppSettings();
        var ledger = Store();
        var row = HandIn();

        Assert.False(IsDone(settings, ledger, row, Quest()));
        ledger.SetCompleted(Dranak, QuestName, true);
        Assert.True(IsDone(settings, ledger, row, Quest()));
        ledger.SetCompleted(Dranak, QuestName, false);
        Assert.False(IsDone(settings, ledger, row, Quest()));
    }

    // ---- Writing -----------------------------------------------------------------------

    /// <summary>Ticking the hand-in row writes the line the quest card's toggle writes, and
    /// only that: one integer, one store, reachable from both screens.</summary>
    [Fact]
    public void TickingTheHandInRowMarksTheQuestComplete()
    {
        var settings = new AppSettings();
        var ledger = Store();
        var row = HandIn();

        SetDone(settings, ledger, row, Quest(), done: true);
        Assert.Equal(1, ledger.CompletedFor(Dranak)[QuestName]);
        Assert.DoesNotContain(row.Id,
            ledger.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);

        SetDone(settings, ledger, row, Quest(), done: false);
        Assert.False(ledger.CompletedFor(Dranak).ContainsKey(QuestName));
    }

    /// <summary>
    /// <b>The refusal, and both halves of it.</b> A piece row does not take a tick — and the
    /// write does not quietly fall through to the guide ledger either, which is the failure
    /// that would look like it worked until the next loot line disagreed with it.
    /// </summary>
    [Fact]
    public void APieceRowRefusesAManualTickAndDoesNotWriteAnywhereElse()
    {
        var settings = new AppSettings();
        var ledger = Store();
        var row = Collect("Fire Beetle Eye");

        SetDone(settings, ledger, row, Quest(), done: true);

        Assert.Empty(ledger.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);
        Assert.Empty(ledger.CompletedFor(Dranak));
        Assert.False(IsDone(settings, ledger, row, Quest()));
        Assert.False(IsDone(settings, ledger, row, Quest(eyesHeld: 1)));
    }

    /// <summary>The refusal has to be VISIBLE, so the router hands a surface both the fact and
    /// the sentence. A dimmed box with no explanation is trap 17; an undimmed one over this
    /// store is a silent no-op.</summary>
    [Fact]
    public void TheRefusalComesWithTheSentenceTheDimmedBoxOwesThePlayer()
    {
        Assert.False(GuideProgressRouter.CanSetDone(GuideProgressHome.LedgerItem));
        Assert.NotEmpty(GuideProgressRouter.RefusalNote(GuideProgressHome.LedgerItem));

        foreach (var home in Enum.GetValues<GuideProgressHome>()
                     .Where(h => h != GuideProgressHome.LedgerItem))
        {
            Assert.True(GuideProgressRouter.CanSetDone(home), $"{home} should take a tick");
            Assert.Empty(GuideProgressRouter.RefusalNote(home));
        }
    }

    /// <summary>Skip is the asymmetry the router has always had: "I am not doing this" has no
    /// home in the quest ledger's count and lands in the guide ledger for every row, including
    /// the one that refuses a tick. A piece you are not going to farm is a real statement.</summary>
    [Fact]
    public void APieceRowThatRefusesATickCanStillBeStruckOut()
    {
        var ledger = Store();
        var row = Collect("Fire Beetle Eye");

        GuideProgressRouter.SetSkipped(ledger, Dranak, GuideId, row, true);
        Assert.True(GuideProgressRouter.IsSkipped(ledger, Dranak, GuideId, row));
    }

    /// <summary>The caption over a "Turn-in pieces" stage counts through the same
    /// <see cref="GuideProgressRouter.IsDone"/> the rows are drawn from, so it cannot disagree
    /// with the ticks under it — one producer, two readers.</summary>
    [Fact]
    public void TheCountsReadTheQuestLedgerToo()
    {
        var settings = new AppSettings();
        var ledger = Store();
        var guide = new Guide
        {
            Id = GuideId, Name = QuestName, QuestName = QuestName,
            Stages =
            [
                new GuideStage
                {
                    Id = $"{GuideId}-turnin", Name = "Turn-in pieces", Order = 1,
                    Objectives = [Collect("Fire Beetle Eye"), Collect("Torch"), HandIn()],
                },
            ],
        };
        guide.Stages[0].Objectives[1].Order = 2;

        var none = GuideProgressRouter.Counts(settings, ledger, Dranak, guide, [], [], Quest());
        Assert.Equal(new GuideProgressCounts(0, 0, 3), none);

        var one = GuideProgressRouter.Counts(settings, ledger, Dranak, guide, [], [],
            Quest(eyesHeld: 3));
        Assert.Equal(new GuideProgressCounts(1, 0, 3), one);

        ledger.SetCompleted(Dranak, QuestName, true);
        var two = GuideProgressRouter.Counts(settings, ledger, Dranak, guide, [], [],
            Quest(eyesHeld: 3, torchesHeld: 1));
        Assert.Equal(new GuideProgressCounts(3, 0, 3), two);
    }

    private static bool IsDone(AppSettings settings, QuestLedgerStore ledger,
        GuideObjective objective, QuestMatch quest) =>
        GuideProgressRouter.IsDone(settings, ledger, Dranak, GuideId, objective, [], [], quest);

    private static void SetDone(AppSettings settings, QuestLedgerStore ledger,
        GuideObjective objective, QuestMatch quest, bool done) =>
        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, objective, [], [], quest,
            done);
}
