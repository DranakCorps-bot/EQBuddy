using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE MAP'S GUIDE-STEP LAYER, ON THE PHONE** (DRA-42 D3) — <see cref="MapTargetSurfaceParityTests"/>'
/// shape for the second layer: the marks and the block reach the wire from the same producers
/// the PC draws, a step change wakes a paired device, and the page DRAWS every field it is sent
/// and words none of them (trap 32, DRA-84 D5).
/// </summary>
public sealed class GuideMapSurfaceParityTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 9, 30, 20, 0, 0);
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "eqb-guidemap-" + Guid.NewGuid().ToString("N"));

    public GuideMapSurfaceParityTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp dir */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>Two archived points in one zone: one that has seen the step's dropper and one
    /// that has only seen something else — the committed negative in the same archive.</summary>
    private (CompanionMapSource Source, SpawnPointLedger Ledger) Zone()
    {
        var ledger = new SpawnPointLedger(Path.Combine(_dir, "ledger"), SpawnCatalog.LoadEmbedded());
        ledger.Apply(new ZoneEvent(Now.AddMinutes(-10), "West Commonlands"));
        ledger.Apply(new LocationEvent(Now.AddMinutes(-9), 100, 200, 0));
        ledger.Apply(new KillEvent(Now.AddMinutes(-9), "a nightfall giant", "Dranak"));
        ledger.Apply(new LocationEvent(Now.AddMinutes(-8), 500, 600, 0));
        ledger.Apply(new KillEvent(Now.AddMinutes(-8), "a giant rat", "Dranak"));
        return (new CompanionMapSource(new AppSettings { MapFolder = _dir }), ledger);
    }

    private static WhileHereStep Kill(string step) =>
        new("Armor of Ro Quests", step, step, ["a nightfall giant"], WhoDrops: true);

    private static WhileHereAnswer Answer(params WhileHereStep[] required) =>
        new("West Commonlands", WhileHereState.Answered, required, [], [], 0);

    private static CompanionMapRequest In(SpawnPointLedger ledger, WhileHereAnswer? guide) =>
        new()
        {
            MapZone = "West Commonlands", TimerZone = "West Commonlands", Points = ledger, Guide = guide,
        };

    [Fact]
    public void OnlyThePointThatHasSeenTheDropperWearsTheMarkAndSaysWhichStep()
    {
        var (source, ledger) = Zone();
        var map = source.Build(In(ledger, Answer(Kill("Collect Nightfall Giant's Head"))), Now);

        Assert.Equal(2, map.Circles.Count);
        var marked = Assert.Single(map.Circles, c => c.Guide);
        Assert.Contains("Collect Nightfall Giant's Head", marked.GuideText);
        Assert.Contains("a nightfall giant", marked.GuideText);
        // A second meaning rides BESIDE the gear flag, never instead of it.
        Assert.False(marked.Target);
        Assert.Equal("", Assert.Single(map.Circles, c => !c.Guide).GuideText);
    }

    [Fact]
    public void TheBlockCarriesTheSameSentencesThePcDrawsAndCapsWithItsOwnNumber()
    {
        var (source, ledger) = Zone();
        var steps = Enumerable.Range(1, GuideTargetPresentation.StepsShown + 2)
            .Select(i => Kill($"Step {i}")).ToArray();
        var person = new WhileHereStep("Armor of Ro Quests", "p", "Talk to Lord Searfire", ["Lord Searfire"]);

        var g = Assert.IsType<CompanionMapGuide>(
            source.Build(In(ledger, Answer([.. steps, person])), Now).Guide);

        Assert.Equal(GuideTargetPresentation.Heading("West Commonlands"), g.Heading);
        Assert.Equal(GuideTargetPresentation.PointsNote, g.Note);
        Assert.Equal(GuideTargetPresentation.StepsShown, g.Steps.Count);
        Assert.Equal(GuideTargetPresentation.StepRow(steps[0]), g.Steps[0]);
        Assert.Equal(GuideTargetPresentation.MoreSteps(2), g.More);
        Assert.Equal(GuideTargetPresentation.PointsHere(1, 2), g.Points);
        Assert.Equal(GuideTargetPresentation.Unmarkable(1), g.Unmarkable);
    }

    [Fact]
    public void NoOpenStepHereSendsNoBlockAndNoMarks()
    {
        var (source, ledger) = Zone();

        Assert.Null(source.Build(In(ledger, null), Now).Guide);
        var none = source.Build(In(ledger, WhileHereAnswer.None), Now);
        Assert.Null(none.Guide);
        Assert.All(none.Circles, c => Assert.False(c.Guide));
        // An answer about ANOTHER zone is nothing here either (the exact join).
        var elsewhere = Answer(Kill("x")) with { Zone = "Commonlands" };
        Assert.Null(source.Build(In(ledger, elsewhere), Now).Guide);
    }

    /// <summary>A step ticked moves no coordinate, no label and no kill count — the key must
    /// move anyway, and a SWAP must move it too (trap 72).</summary>
    [Fact]
    public void TheMapKeyMovesWhenAStepArrivesLeavesOrIsSwapped()
    {
        var (source, ledger) = Zone();

        string Key(params WhileHereStep[] steps)
        {
            var map = source.Build(In(ledger, steps.Length == 0 ? null : Answer(steps)), Now);
            return CompanionProjection.SectionFingerprints(
                new CompanionSnapshot { Map = map })[CompanionSurfaces.Map];
        }

        var none = Key();
        var one = Key(Kill("Collect Nightfall Giant's Head"));
        var other = Key(Kill("Collect Sand of Ro"));
        Assert.NotEqual(none, one);
        Assert.NotEqual(one, other);
        Assert.Equal(none, Key());
    }

    [Fact]
    public void ThePageSpellsNoneOfTheGuideLayersWordsAndDrawsAllOfItsFields()
    {
        var html = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "EQBuddy.Companion", "Web", "index.html"));

        foreach (var sentence in new[]
                 {
                     GuideTargetPresentation.PointsNote,
                     GuideTargetPresentation.MarkTip,
                     GuideTargetPresentation.Heading("West Commonlands"),
                     GuideTargetPresentation.PointsHere(1, 2),
                     GuideTargetPresentation.PointsHere(0, 0),
                     GuideTargetPresentation.MoreSteps(2),
                     GuideTargetPresentation.Unmarkable(1),
                     // The PC control's tips: the phone offers no switch (trap 35).
                     GuideTargetPresentation.ToggleTip(true),
                     GuideTargetPresentation.ToggleTip(false),
                 })
            Assert.DoesNotContain(sentence, html, StringComparison.Ordinal);

        // Every field CompanionMapGuide carries, plus the circle's two. One added without a row
        // here is DRA-84 D5's bug again.
        foreach (var field in new[]
                 {
                     "drawGuide", "data.guide", "g.heading", "g.steps", "g.more", "g.points",
                     "g.note", "g.unmarkable", "c.guide", "c.guideText",
                 })
            Assert.Contains(field, html, StringComparison.Ordinal);
    }
}
