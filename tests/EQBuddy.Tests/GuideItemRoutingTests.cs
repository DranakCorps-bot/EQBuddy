using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The THIRD progress home (Fable plan §2 D1, Helm ACK 2026-09-09).
///
/// <para>An objective whose whole content is "get hold of this item" is not a new fact
/// either: it is the box the classic Sky checklist has always drawn for that item, which the
/// loot auto-tick already writes and four surfaces already read. Without this home a Warrior
/// would see "Stone Amulet" twice with two boxes on the first screen of the feature — trap 4,
/// and the loot auto-tick would light one of them and not the other.</para>
///
/// <para>The rule is deliberately narrow, and the narrowing is what is asserted here: the
/// right objective TYPE, exactly ONE match, and only inside the reward's OWN rows. Every way
/// of failing those falls back to the guide ledger, because an ambiguous claim on a store
/// four other things write is worse than a private tick.</para>
/// </summary>
public sealed class GuideItemRoutingTests : IDisposable
{
    private const string Dranak = "dranak_freeport";
    private const string GuideId = "war-pos-runed-wind-amulet";
    private const string Reward = "Runed Wind Amulet";
    private const string ClassName = "Warrior";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"guide-item-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static string RewardKey => QuestChecklistLayout.RewardKey(ClassName, Reward);

    private static SkyQuestChecklistItem Item(string id, string questItem) => new()
    {
        Id = id, ClassName = ClassName, Reward = Reward,
        Npc = "Torgon Blademaster", QuestItem = questItem,
    };

    /// <summary>This reward's own two drops, as <c>SkyQuestDefaults</c> spells them.</summary>
    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.SkyQuestChecklist.AddRange(
            [Item("sky-198", "Stone Amulet"), Item("sky-199", "Wind Rune Azia")]);
        return s;
    }

    private static IReadOnlyList<SkyQuestChecklistItem> ItemsOf(AppSettings s) =>
        GuideChecklistProjection.ItemsFor(s, RewardKey);

    private static GuideObjective Loot(string id, params string[] items) => new()
    {
        Id = id, Order = 1, ObjectiveType = "Loot", Title = id,
        ShortInstruction = "loot it", ItemNames = [.. items],
    };

    // ---- which home, and why ---------------------------------------------------------

    [Fact]
    public void AnAcquireStepNamingExactlyOneOfTheRewardsOwnItemsIsThatItemsBox()
    {
        var settings = Settings();

        var home = GuideProgressRouter.HomeFor(
            Loot("stone-amulet", "Stone Amulet"), ItemsOf(settings), out var backing);

        Assert.Equal(GuideProgressHome.SkyItem, home);
        Assert.Equal("sky-198", backing!.Id);
    }

    [Theory]
    [InlineData("Farm")]
    [InlineData("Collect")]
    public void FarmAndCollectAreAcquireShapedToo(string objectiveType)
    {
        var settings = Settings();
        var objective = Loot("wind-rune-azia", "Wind Rune Azia");
        objective.ObjectiveType = objectiveType;

        Assert.Equal(GuideProgressHome.SkyItem,
            GuideProgressRouter.HomeFor(objective, ItemsOf(settings), out _));
    }

    [Fact]
    public void AStepThatMerelyMENTIONSAnItemIsNotThatItemsTick()
    {
        var settings = Settings();
        // A Travel step whose prose names the drop it is heading toward. Ticking "fly to
        // isle 4" must not claim you are holding the amulet.
        var travel = Loot("fly-to-isle-4", "Stone Amulet");
        travel.ObjectiveType = "Travel";

        Assert.Equal(GuideProgressHome.GuideLedger,
            GuideProgressRouter.HomeFor(travel, ItemsOf(settings), out var backing));
        Assert.Null(backing);
    }

    [Fact]
    public void ATwoItemObjectiveStaysInTheLedgerBecauseNeitherBoxIsTheWholeAnswer()
    {
        var settings = Settings();

        Assert.Equal(GuideProgressHome.GuideLedger, GuideProgressRouter.HomeFor(
            Loot("both", "Stone Amulet", "Wind Rune Azia"), ItemsOf(settings), out var backing));
        Assert.Null(backing);
    }

    [Fact]
    public void AnItemNoRowOfThisRewardNamesStaysInTheLedger()
    {
        var settings = Settings();

        Assert.Equal(GuideProgressHome.GuideLedger, GuideProgressRouter.HomeFor(
            Loot("rope", "Coil of Rope"), ItemsOf(settings), out _));
    }

    [Fact]
    public void OneClassesWindRuneIsNeverAnotherClassesWindRune()
    {
        // The Bard wants a "Wind Rune Azia" too. Its row belongs to the Bard's reward, and
        // ItemsFor is keyed by THIS reward — so the Warrior guide can never resolve to it.
        var settings = Settings();
        settings.SkyQuestChecklist.Add(new SkyQuestChecklistItem
        {
            Id = "sky-007", ClassName = "Bard", Reward = "Mask of Song",
            QuestItem = "Wind Rune Azia",
        });

        var warriorRows = GuideChecklistProjection.ItemsFor(settings, RewardKey);
        GuideProgressRouter.HomeFor(
            Loot("wind-rune-azia", "Wind Rune Azia"), warriorRows, out var backing);

        Assert.Equal("sky-199", backing!.Id);
        Assert.DoesNotContain(warriorRows, i => i.Id == "sky-007");
    }

    // ---- reading and writing through the box -----------------------------------------

    [Fact]
    public void AnItemBackedStepReadsTheBoxTheLootAutoTickAlreadyWrites()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = Loot("stone-amulet", "Stone Amulet");

        Assert.False(GuideProgressRouter.IsDone(
            settings, ledger, Dranak, GuideId, objective, ItemsOf(settings)));

        // Exactly what SkyLootAutoCheck does when the log says the amulet dropped.
        settings.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired = true;

        Assert.True(GuideProgressRouter.IsDone(
            settings, ledger, Dranak, GuideId, objective, ItemsOf(settings)));
    }

    [Fact]
    public void TickingTheGuideStepWritesTheBoxAndClearsTheGuessedFlagAndTheLedgerStaysEmpty()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = Loot("stone-amulet", "Stone Amulet");
        // The auto-tick had guessed this one for another class and left its mark.
        var box = settings.SkyQuestChecklist.Single(i => i.Id == "sky-198");
        box.AcquiredUnassigned = true;

        GuideProgressRouter.SetDone(
            settings, ledger, Dranak, GuideId, objective, ItemsOf(settings), true);

        Assert.True(box.Acquired);
        Assert.False(box.AcquiredUnassigned);
        // The whole point: one fact, one store. The guide ledger holds nothing about it.
        Assert.Empty(ledger.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);
    }

    [Fact]
    public void UntickingTheGuideStepClearsTheBoxToo()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = Loot("stone-amulet", "Stone Amulet");
        var box = settings.SkyQuestChecklist.Single(i => i.Id == "sky-198");
        box.Acquired = true;

        GuideProgressRouter.SetDone(
            settings, ledger, Dranak, GuideId, objective, ItemsOf(settings), false);

        Assert.False(box.Acquired);
    }

    [Fact]
    public void TickingAnItemBackedStepClearsAContradictingSkip()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = Loot("stone-amulet", "Stone Amulet");
        GuideProgressRouter.SetSkipped(ledger, Dranak, GuideId, objective, true);

        GuideProgressRouter.SetDone(
            settings, ledger, Dranak, GuideId, objective, ItemsOf(settings), true);

        // A row cannot read struck-out and done at once.
        Assert.False(GuideProgressRouter.IsSkipped(ledger, Dranak, GuideId, objective));
    }

    /// <summary>The prove-fail the whole home exists for: WITHOUT the group's rows in hand
    /// the same objective falls to the ledger, which is the duplicate tick this replaced.
    /// If the group-aware overload ever stopped being the one the projection calls, this is
    /// the shape the bug would have.</summary>
    [Fact]
    public void WithNoGroupInHandTheSameStepFallsToTheLedgerAndTheTwoStoresDisagree()
    {
        var settings = Settings();
        var ledger = Store();
        var objective = Loot("stone-amulet", "Stone Amulet");

        Assert.Equal(GuideProgressHome.GuideLedger, GuideProgressRouter.HomeFor(objective));
        GuideProgressRouter.SetDone(settings, ledger, Dranak, GuideId, objective, true);

        Assert.False(settings.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired);
        Assert.Contains("stone-amulet", ledger.GuideProgressFor(Dranak, GuideId).DoneObjectiveIds);
    }

    [Fact]
    public void CountsReadTheBoxesToo()
    {
        var settings = Settings();
        var ledger = Store();
        var guide = new Guide
        {
            Id = GuideId, Name = "Runed Wind Amulet",
            Stages =
            [
                new GuideStage
                {
                    Id = "s1", Name = "Isle 4", Order = 1,
                    Objectives = [Loot("stone-amulet", "Stone Amulet")],
                },
            ],
        };

        Assert.Equal(0, GuideProgressRouter
            .Counts(settings, ledger, Dranak, guide, ItemsOf(settings)).Done);

        settings.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired = true;

        Assert.Equal(1, GuideProgressRouter
            .Counts(settings, ledger, Dranak, guide, ItemsOf(settings)).Done);
    }
}
