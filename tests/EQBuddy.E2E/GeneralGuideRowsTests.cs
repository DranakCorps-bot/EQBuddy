using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.E2E;

/// <summary>
/// The General tab's guided detail pane, in the running app (DRA-46, Fable §3 N2).
///
/// <para>The WPF layer has no unit tests (docs/TestPlan.md §5), so the only way to claim the
/// walkthrough REACHED THE PANE is a launched app reporting its own structure. Every count
/// here comes off the real visual tree by the Tag its element carries (trap 39) — a number
/// taken from the projection that produced the rows cannot fail the way the screen can, and
/// the projection's own answer is dumped BESIDE the screen's so the two claims stay apart
/// (trap 56).</para>
///
/// <para><b>Why this file exists beside <see cref="GuideRowsTests"/>.</b> Every guide fact the
/// dump carried before today is counted off <c>QuestsPanel</c> — the LIST. This surface is in
/// <c>DetailPane</c>, so a <c>questsGuideRows</c> assertion on the General tab would read 0
/// and pass for entirely the wrong reason. Separate keys, separate file.</para>
/// </summary>
public class GeneralGuideRowsTests
{
    /// <summary>
    /// The quest this file is about, chosen from the SHIPPED catalog rather than named.
    ///
    /// <para>The harvest is regenerated weekly, so a literal quest name would make this suite
    /// test nothing the week that page changed shape — it would still launch, still pass, and
    /// still be photographing a real state of something else (trap 23). The predicate is the
    /// claim instead: a quest whose guide has BOTH kinds of step, so "the pieces are item rows
    /// and the rest are guide rows" has both halves to be about.</para>
    ///
    /// <para>Throws rather than skipping. An empty harvest is a failure of DRA-45, and a
    /// suite that quietly stops asserting is the vacuous pass trap 34 names.</para></summary>
    private static QuestEntry Target { get; } =
        QuestCatalog.LoadEmbedded().Quests
            .Where(q => q.Items.Count >= 2)
            .Where(q => GuideChecklistProjection.QuestGuideFor(GuideCatalog.Default, q.Name)
                is { } g && g.AllObjectives.Any(o =>
                    !string.Equals(o.ObjectiveType, "Collect", StringComparison.Ordinal)))
            .OrderBy(q => q.Name, StringComparer.Ordinal)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            "no shipped quest has a guide with both turn-in pieces and other steps — " +
            "the DRA-46 fixture needs one");

    private static Guide TargetGuide =>
        GuideChecklistProjection.QuestGuideFor(GuideCatalog.Default, Target.Name)!;

    /// <summary>The steps that get a CHECKBOX: everything the router does not answer from the
    /// quest ledger's owned counts. Derived from the router rather than from the objective
    /// type, because the router is what the pane actually asks (trap 4).</summary>
    private static int ExpectedGuideRows => TargetGuide.AllObjectives.Count(o =>
        GuideProgressRouter.HomeFor(o, new GuideStores([], [], Target), out _)
            != GuideProgressHome.LedgerItem);

    /// <summary>The steps that get an ITEM ROW instead — one per distinct turn-in item the
    /// guide names.</summary>
    private static int ExpectedItemRows => TargetGuide.AllObjectives
        .Where(o => GuideProgressRouter.HomeFor(o, new GuideStores([], [], Target), out var b)
            == GuideProgressHome.LedgerItem && b.QuestItem is not null)
        .Select(o =>
        {
            GuideProgressRouter.HomeFor(o, new GuideStores([], [], Target), out var b);
            return b.QuestItem!.Name;
        })
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    /// <summary>The fixture: the target quest PINNED so it is the pane's selection, and its
    /// guide EXPANDED so the rows exist to be counted. The fold key of a quest group is the
    /// guide id — taken from the catalog rather than typed, so re-harvesting cannot leave this
    /// seeding silently pointing at nothing.</summary>
    private static AppHarness Fixture(IReadOnlyDictionary<string, int>? owned = null)
    {
        var app = new AppHarness(
            s => s.GuideExpanded.Add(TargetGuide.Id),
            new Dictionary<string, string>
            {
                ["EQBUDDY_SHELL"] = "quests:general",
                ["EQBUDDY_QUESTS"] = "general",
            });
        app.SeedQuestLedger(tracked: [Target.Name], owned: owned);
        return app;
    }

    /// <summary>
    /// Selecting a guided quest draws its walkthrough, and the split between the two kinds of
    /// row is the one the router decided — <b>the N2 claim</b>.
    ///
    /// <para>Both numbers asserted, not just the total: a build that drew the Collect steps as
    /// a second list of checkboxes beside the item rows would keep the total and fail here,
    /// which is precisely the defect "not a second list" names.</para></summary>
    [Fact]
    public void SelectingAGuidedQuestDrawsItsStepsAndItsPiecesAsTheItemRows()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsTab", "general", "the shell to reach the General tab");
        app.WaitForDump("shellQuestsGeneralGuide", 1, "the guide block to reach the pane");

        // The store's answer and the screen's, from one read of one dump (trap 56).
        var seen = app.DumpValues(
            "shellQuestsGeneralGuideId",
            "shellQuestsGeneralGuideRows",
            "shellQuestsGeneralGuideItemRows",
            "shellQuestsGeneralGuideCards",
            "shellQuestsGeneralGuideFolded");

        Assert.Equal(TargetGuide.Id.Length, seen[0]);
        Assert.Equal(ExpectedGuideRows, seen[1]);
        Assert.Equal(ExpectedItemRows, seen[2]);
        // The NEXT card is above the rows, once.
        Assert.Equal(1, seen[3]);
        // Expanded, because the fixture opened it — the assertion that the counts above are
        // about an OPEN guide rather than a fold that happens to draw nothing.
        Assert.Equal(0, seen[4]);
    }

    /// <summary>Every row of a guided quest carries the share-back door, which is the whole
    /// correction path for a sentence that is the wiki's own (Fable §3 N2). Counted off the
    /// real buttons, because an absent control photographs as an unremarkable panel
    /// (trap 29).</summary>
    [Fact]
    public void EveryGuideStepOnThePaneCarriesTheImproveDoor()
    {
        using var app = Fixture();
        app.Launch();

        app.WaitForDump("shellQuestsGeneralGuide", 1, "the guide block to reach the pane");
        app.WaitForDump("shellQuestsGeneralGuideImprove", ExpectedGuideRows,
            "every drawn guide step to carry its Improve door");
    }

    /// <summary>
    /// <b>A Collect row lights when the ledger's owned count reaches Need</b> — the card's own
    /// acceptance criterion, end to end in the launched app.
    ///
    /// <para>Two launches rather than one, because the thing being proved is a DIFFERENCE: the
    /// same quest, the same guide, the same pane, with nothing changed but what the bags hold.
    /// Asserting only the loaded state would pass on a build that ticked the row regardless of
    /// the count.</para>
    ///
    /// <para>Counted off <c>questsGeneralGuideDone</c>, which reads CHECKBOXES — so a piece
    /// row moving is invisible to it by construction, and that is the point: the number that
    /// must NOT move is asserted beside the one that must. The piece's own state rides
    /// <c>questsGeneralGuidePiecesMet</c>.</para></summary>
    [Fact]
    public void APieceLightsWhenTheOwnedCountReachesNeed()
    {
        var need = TargetGuide.AllObjectives
            .Select(o =>
            {
                GuideProgressRouter.HomeFor(o, new GuideStores([], [], Target), out var b);
                return b.QuestItem;
            })
            .First(i => i is not null)!;

        int MetWith(IReadOnlyDictionary<string, int>? owned)
        {
            using var app = Fixture(owned);
            app.Launch();
            app.WaitForDump("shellQuestsGeneralGuide", 1, "the guide block to reach the pane");
            return app.DumpValue("shellQuestsGeneralGuidePiecesMet");
        }

        var empty = MetWith(null);
        var full = MetWith(new Dictionary<string, int>
        {
            [need.Name] = Math.Max(1, need.Qty),
        });

        Assert.Equal(0, empty);
        Assert.Equal(1, full);
    }
}
