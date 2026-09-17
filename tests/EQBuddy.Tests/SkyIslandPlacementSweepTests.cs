using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The whole shipped Plane of Sky catalog, run through the real projection, with every row's
/// island placement counted** (DRA-164 D1's guard).
///
/// <para><b>Why a sweep and not a handful of cases.</b> The island view moves 317 objectives
/// into groups a player plans a night around, and the way this goes wrong is not an exception —
/// it is a row quietly filed under the wrong heading, which looks exactly like a row filed
/// under the right one. Pinning the OUTCOME means a future catalog edit that moves one step is
/// a red diff naming the number that changed, never a silent regroup.</para>
///
/// <para><b>The four figures were measured twice before they became floors.</b> Fable's survey
/// and this suite are the first parse; <c>scripts/dra164-island-survey.py</c> is the second,
/// re-implementing the parser and the placement rule over the same JSON, and the two agree on
/// all four. A floor nobody can re-derive is a number that was copied (trap 52).</para>
///
/// <para>It goes through <c>GuideChecklistProjection.Apply</c> rather than reading the JSON,
/// because the claim is about what a PLAYER's checklist does — the stage-name rule, the
/// <c>Where</c> fallback and the turn-in exclusion are one decision made in one place, and a
/// test that re-implemented them would be the second producer the design exists to avoid.</para>
/// </summary>
public sealed class SkyIslandPlacementSweepTests : IDisposable
{
    private const string Character = "dranak_freeport";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"island-sweep-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private IReadOnlyList<QuestChecklistGroup> ShippedSkyGroups()
    {
        var settings = new AppSettings();
        settings.ApplyDefaultSkyQuestChecklist();
        var groups = QuestChecklistLayout.Sky(settings.SkyQuestChecklist, settings.SkyQuestCompleted);
        return GuideChecklistProjection.Apply(
            groups, GuideCatalog.LoadEmbedded(), settings,
            new QuestLedgerStore(_path) { TrackFilter = _ => true }, Character);
    }

    /// <summary>
    /// The pin: **103 single-isle / 22 on {1.5, 4, 8} / 97 with no island / 95 hand-ins**.
    ///
    /// <para>The 22 are the whole reason P3 exists. Under the parser this repo shipped at
    /// `29f18da9` they came back as island 1.5 alone and the first two buckets read 125 / 0 —
    /// see <c>SkyIslandsTests.TheShippedParserReadTheEfreetiDropAsOneIsland</c>, which holds
    /// that before-picture so this row cannot pass vacuously (trap 34). Every other number in
    /// this test is IDENTICAL either side of the fix, which is exactly why the fix needed a
    /// count to be visible at all.</para>
    ///
    /// <para>The 97 are not a gap: 95 are wind runes, whose `Where` says "any isle" because
    /// trash drops anywhere on the plane, and 2 are honest stubs. Calling them unknown would
    /// call the catalog incomplete when it is telling the truth.</para>
    /// </summary>
    [Fact]
    public void EveryShippedSkyObjectiveLandsWhereTheSurveySaidItWould()
    {
        var rows = ShippedSkyGroups().SelectMany(g => g.Rows).ToList();

        var turnIns = rows.Count(r => r.IsTurnIn);
        var gathering = rows.Where(r => !r.IsTurnIn).ToList();
        var single = gathering.Count(r => SkyIslands.FromSetKey(r.IslandKey).Count == 1);
        var multi = gathering.Count(r => SkyIslands.FromSetKey(r.IslandKey).Count > 1);
        var none = gathering.Count(r => SkyIslands.FromSetKey(r.IslandKey).Count == 0);

        Assert.Equal(317, rows.Count);
        Assert.Equal(103, single);
        Assert.Equal(22, multi);
        Assert.Equal(97, none);
        Assert.Equal(95, turnIns);
        // The buckets are a partition, so a future edit cannot move a row out of one without
        // this saying which one took it.
        Assert.Equal(rows.Count, single + multi + none + turnIns);
    }

    /// <summary>The 22 are the Efreeti drop, and all 22 land on the SAME three islands — the
    /// stage names none of them, and the one `Where` string they share names all three. One
    /// distinct placement across 22 rows is the telltale that this is transcription rather than
    /// 22 separate readings (trap 73's distinct-count lesson, pointed the other way).</summary>
    [Fact]
    public void TheEfreetiDropIsTheOnlyMultiIslandStepAndItIsOnAllThree()
    {
        var multi = ShippedSkyGroups()
            .SelectMany(g => g.Rows)
            .Where(r => !r.IsTurnIn && SkyIslands.FromSetKey(r.IslandKey).Count > 1)
            .ToList();

        Assert.Equal(22, multi.Count);
        Assert.Equal([SkyIslands.SetKey([1.5, 4, 8])],
            multi.Select(r => r.IslandKey).Distinct(StringComparer.Ordinal));
        Assert.Equal(["The Efreeti drop"],
            multi.Select(r => r.IslandHeading).Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// **No hand-in is given an island, and this is the row that proves the exclusion earns its
    /// keep rather than merely existing.**
    ///
    /// <para>48 of the 95 turn-in objectives mention Isle 1 in their `Where` — they are the
    /// directions TO the Efreeti Chamber, where the pieces are handed over. Without the turn-in
    /// clause the `Where` fallback would file 48 hand-ins as gathering work on Island 1: a
    /// confident wrong island on rows a player would plan a night around, and the failure a
    /// naive prose fallback ships with. The count is asserted so that a catalog rewording
    /// cannot quietly make this test vacuous.</para>
    /// </summary>
    [Fact]
    public void NoHandInIsGivenAnIslandEvenThoughFortyEightNameOne()
    {
        var catalog = GuideCatalog.LoadEmbedded();
        var mentionIsleOne = catalog.Guides
            .Where(g => g.GuideType == GuideType.PlaneOfSkyQuest)
            .SelectMany(g => g.AllObjectives)
            .Count(o => string.Equals(o.ObjectiveType, "TurnIn", StringComparison.OrdinalIgnoreCase)
                        && SkyIslands.Parse(o.Where).Count > 0);

        Assert.Equal(48, mentionIsleOne);

        Assert.All(
            ShippedSkyGroups().SelectMany(g => g.Rows).Where(r => r.IsTurnIn),
            r => Assert.Equal("", r.IslandKey));
    }

    /// <summary>
    /// The island view over the SHIPPED catalog — and this row is the reason "no invented 1–8
    /// scaffold" is a rule rather than a preference.
    ///
    /// <para><b>The catalog locates gathering work on SEVEN islands, 2 through 8. There is no
    /// Island 1 group and no Island 1.5 group.</b> Island 1 is the arrival isle, and 1.5
    /// appears only INSIDE the Efreeti set — no stage names it alone. A view that drew an
    /// empty row per island number would put two headings on screen promising a night's work
    /// that does not exist, and a player who flew to Island 1 on the strength of one would be
    /// right to call it a bug.</para>
    ///
    /// <para>The counts are the drawn ones, so a catalog edit that moves a step between islands
    /// reddens this with the pair that changed. They are named rather than summed for the
    /// reason the buckets above are: a total can stay still while two islands swap.</para>
    /// </summary>
    [Fact]
    public void TheShippedIslandViewDrawsTheIslandsTheDataHasAndNoOthers()
    {
        var layout = QuestChecklistLayout.SkyByIsland(ShippedSkyGroups());

        Assert.Equal(
            [("Island 2", 2), ("Island 3", 16), ("Island 4", 13), ("Island 5", 18),
             ("Island 6", 18), ("Island 7", 20), ("Island 8", 16),
             ("Islands 1.5 · 4 · 8", 22), ("Not placed", 2), ("The wind rune", 95)],
            layout.Groups.Select(g => (g.Heading, g.Total)));

        Assert.DoesNotContain("Island 1", layout.Groups.Select(g => g.Heading));
        Assert.DoesNotContain("Island 1.5", layout.Groups.Select(g => g.Heading));

        // Every gathering row is drawn exactly once, and no hand-in is.
        Assert.Equal(222, layout.Groups.Sum(g => g.Total));
        Assert.Equal(95, layout.HiddenTurnIns);
        Assert.Equal(0, layout.HiddenRewards);   // a fresh character has turned nothing in
    }

    /// <summary>An EPIC guide's rows still have no island — its stages are section names, not
    /// places. The sibling of <c>SkyIslandGroupingTests.EpicRowsHaveNoIslandHeading</c>, and
    /// the negative that keeps the new field from leaking onto a tab that has no islands.</summary>
    [Fact]
    public void NoEpicGuideRowCarriesAnIslandKey()
    {
        var catalog = GuideCatalog.LoadEmbedded();
        var epics = catalog.Guides.Where(g => g.GuideType == GuideType.EpicQuest).ToList();
        Assert.NotEmpty(epics);

        // Read at the source rather than through the Epic tab's lens, which would first remove
        // rows and could leave this passing because nothing was drawn at all.
        foreach (var objective in epics.SelectMany(g => g.AllObjectives))
            Assert.Empty(SkyIslands.Parse(
                epics.SelectMany(g => g.Stages)
                     .FirstOrDefault(s => s.Objectives.Any(o => o.Id == objective.Id))?.Name ?? ""));
    }
}
