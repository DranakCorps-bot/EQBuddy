using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Track on the Epic 1.0 and Plane of Sky tabs (Founder smoke, 2026-09-29): "all quest types
/// … Epic Quests will need a track selection that is at the section level. For POS, we can
/// toggle tracking at the quest name as we do for regular quests."
///
/// Real shipped data throughout — the epic checklist, the Sky checklist, the guide catalog —
/// because the join between a tracked key and what the tab draws IS the ids in those files,
/// and a hand-made fixture would prove a join the product does not have.
/// </summary>
public class EpicAndSkyTrackingTests : IDisposable
{
    private const string Character = "dranak_legends";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"track-epic-sky-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.EpicQuestChecklist.AddRange(EpicQuestDefaults.Items());
        s.SkyQuestChecklist.AddRange(SkyChecklistRows.Items.Select(i => i.Clone()));
        return s;
    }

    private static readonly Dictionary<string, QuestLedgerStore.Entry> NoItems =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, int> NoneDone = new(StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> Set(params string[] items) => new(items, StringComparer.OrdinalIgnoreCase);

    private static GuideStage WarriorStage(string id) =>
        GuideCatalog.Default.Find("epic-warrior")!.Stages.Single(s => s.Id == id);

    // ---- Epic: a SECTION is what is tracked ----

    /// <summary>
    /// PREDICTION: the Warrior's "The Blades" section with its first two steps ticked reads
    /// "Warrior Epic · The Blades", "2/N" where N is the number of rows the Epic tab draws
    /// under that heading, a gauge of 2/N, and the THIRD step as "next".
    /// </summary>
    [Fact]
    public void ATrackedEpicSectionReadsItsStepsTheWayTheEpicTabDrawsThem()
    {
        var settings = Settings();
        var ledger = Store();
        var stage = WarriorStage("the-blades");
        foreach (var step in stage.Objectives.Take(2))
            settings.EpicQuestChecklist.Single(i => i.Id == step.Id).Acquired = true;

        var epicGroups = ChecklistGroups.Epic(settings, ledger, Character, ChecklistGroups.EpicRows(settings));
        var group = epicGroups.Single(g => g.GuideId == "epic-warrior");
        var drawn = EpicSection.Rows(group, stage);
        Assert.True(drawn.Count > 2, "the premise: The Blades is more than two steps");

        var row = Assert.Single(TrackedQuestsPeek.Build(QuestCatalog.LoadEmbedded(), NoItems, Set(), NoneDone,
            trackedSections: ["epic-warrior/the-blades"], epicGroups: epicGroups).Rows);

        Assert.Equal("Warrior Epic · The Blades", row.Name);
        Assert.Equal($"2/{drawn.Count}", row.Badge.Label);
        Assert.Equal(2d / drawn.Count, row.Share!.Value, 3);
        Assert.Equal($"{TrackedQuestsPeek.EpicLead} · next: {drawn[2].Title}", row.Meta);
        Assert.Equal(TrackedKind.EpicSection, row.Kind);
        Assert.Equal("epic-warrior/the-blades", row.UntrackKey);
    }

    /// <summary>
    /// The key a section heading writes resolves back to that section for EVERY guided epic
    /// class. Keyed on the stage the projection stamped as the heading — so Cleric, Druid and
    /// Rogue, whose rows' section text is "Checklist" while their stage is "&lt;Class&gt; Epic
    /// Quest", still get a key; a key built from the section text would have none.
    /// </summary>
    [Fact]
    public void EverySectionHeadingOnTheEpicTabHasAKeyThatResolvesBackToIt()
    {
        var settings = Settings();
        var groups = ChecklistGroups.Epic(settings, Store(), Character, ChecklistGroups.EpicRows(settings))
            .Where(g => g.GuideId.Length > 0).ToList();
        Assert.Equal(14, groups.Count);

        foreach (var group in groups)
            foreach (var heading in group.Rows.Select(r => r.IslandHeading).Distinct())
            {
                var row = group.Rows.First(r => r.IslandHeading == heading);
                var key = EpicSection.KeyFor(GuideCatalog.Default, row);
                Assert.NotNull(key);
                var resolved = EpicSection.Resolve(GuideCatalog.Default, key!);
                Assert.Equal(heading, resolved!.Value.Stage.Name);
            }
    }

    [Fact]
    public void ACompletedSectionReadsDone()
    {
        var settings = Settings();
        foreach (var step in WarriorStage("the-red-scabbard").Objectives)
            settings.EpicQuestChecklist.Single(i => i.Id == step.Id).Acquired = true;
        var groups = ChecklistGroups.Epic(settings, Store(), Character, ChecklistGroups.EpicRows(settings));

        var row = TrackedQuestsPeek.EpicSectionRow("epic-warrior/the-red-scabbard", groups, GuideCatalog.Default);

        Assert.Equal("done", row.Badge.Label);
        Assert.Equal(TrackedQuestsPeek.EpicLead, row.Meta);
    }

    /// <summary>A section key the guide no longer has is SHOWN, so it can be untracked — the
    /// same rule as a tracked quest the catalog lost.</summary>
    [Fact]
    public void ASectionTheGuideLostIsShownSoItCanBeUntracked()
    {
        var row = TrackedQuestsPeek.EpicSectionRow("epic-warrior/no-such-stage", [], GuideCatalog.Default);

        Assert.Equal(TrackedQuestsPeek.SectionGone, row.Meta);
        Assert.Equal("epic-warrior/no-such-stage", row.UntrackKey);
    }

    // ---- Sky: the reward's QUEST NAME is what is tracked ----

    /// <summary>
    /// A Sky reward is tracked by its catalog quest name — the Quests tab's own list — and
    /// drawn the way the Sky tab draws it: "Class · Reward", its steps, its state. The
    /// negative: with no Sky groups handed in, the same name falls back to the Quests-tab
    /// row, which is how it can be told the Sky arm actually ran.
    /// </summary>
    [Fact]
    public void ATrackedSkyRewardReadsTheSkyTabsOwnHeadingAndSteps()
    {
        var settings = Settings();
        var ledger = Store();
        var catalog = QuestCatalog.LoadEmbedded();
        var skyGroups = ChecklistGroups.Sky(settings, ledger, Character);
        var group = skyGroups.First(g => g.CompletionKey is { } k
            && catalog.Quests.Any(q => q.Name == SkyTestSplit.QuestNameFor(k)));
        var questName = SkyTestSplit.QuestNameFor(group.CompletionKey!);

        var sky = Assert.Single(TrackedQuestsPeek.Build(catalog, NoItems, Set(questName), NoneDone,
            skyGroups: skyGroups).Rows);
        var plain = Assert.Single(TrackedQuestsPeek.Build(catalog, NoItems, Set(questName), NoneDone).Rows);

        Assert.Equal(group.Heading, sky.Name);
        Assert.Equal(TrackedKind.Sky, sky.Kind);
        Assert.Equal(questName, sky.UntrackKey);
        Assert.Equal($"0/{group.Total}", sky.Badge.Label);
        Assert.StartsWith(TrackedQuestsPeek.SkyLead, sky.Meta);
        Assert.Equal(TrackedKind.Quest, plain.Kind);
        Assert.Equal(questName, plain.Name);
    }

    [Fact]
    public void ATurnedInSkyRewardSaysSo()
    {
        var settings = Settings();
        var ledger = Store();
        var groups = ChecklistGroups.Sky(settings, ledger, Character);
        var key = groups.First(g => g.CompletionKey is not null).CompletionKey!;
        settings.SkyQuestCompleted.Add(key);

        var group = ChecklistGroups.Sky(settings, ledger, Character).Single(g => g.CompletionKey == key);

        Assert.Equal("turned in", TrackedQuestsPeek.SkyRow(group, SkyTestSplit.QuestNameFor(key)).Badge.Label);
    }

    // ---- the store ----

    /// <summary>A section is its OWN list: tracking one writes nothing into the quest list
    /// the phone and the matcher read as catalog names, and it survives a reload.</summary>
    [Fact]
    public void ASectionIsTrackedInItsOwnListAndSurvivesAReload()
    {
        var ledger = Store();
        ledger.SetSectionTracked(Character, "epic-warrior/the-blades", true);
        ledger.Flush();

        var reloaded = Store();
        Assert.Contains("epic-warrior/the-blades", reloaded.TrackedSectionsFor(Character));
        Assert.Empty(reloaded.TrackedFor(Character));

        reloaded.SetSectionTracked(Character, "epic-warrior/the-blades", false);
        Assert.Empty(reloaded.TrackedSectionsFor(Character));
    }

    /// <summary>The peek's subtext and its repaint gate count sections too: a section ticked
    /// from the Epic tab must move the signature, or the panel keeps drawing the moment
    /// before (trap 72).</summary>
    [Fact]
    public void ATrackedSectionMovesThePeeksSignature()
    {
        var settings = Settings();
        var groups = ChecklistGroups.Epic(settings, Store(), Character, ChecklistGroups.EpicRows(settings));
        var catalog = QuestCatalog.LoadEmbedded();

        var none = TrackedQuestsPeek.Build(catalog, NoItems, Set(), NoneDone, epicGroups: groups);
        var one = TrackedQuestsPeek.Build(catalog, NoItems, Set(), NoneDone,
            trackedSections: ["epic-warrior/the-blades"], epicGroups: groups);

        Assert.True(none.Empty);
        Assert.NotEqual(none.Signature, one.Signature);
        Assert.Equal("1 tracked", one.Subtext);
    }
}
