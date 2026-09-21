using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE MAP'S TARGET LAYER, ON THE PHONE** (DRA-216 D5, S13/S14).
///
/// <para>The map exists on both surfaces, so the rule this repo keeps is that neither may
/// quietly fall behind: the decision is in Core, the words are in UI.Shared, and both hosts
/// call them. What this file guards is the half that has no compiler behind it — that the
/// sentences reach the wire, that the page DRAWS every one of them, and that a goal tracked or
/// untracked actually wakes a paired device.</para>
///
/// <para><b>DRA-84 D5's lesson is the whole second half.</b> A caption the page is SENT and
/// never draws passes every projection assertion there is: the field is non-empty, the
/// fingerprint moves, the wire carries it, and the phone shows nothing. So every field the
/// page receives is named in the must-list below, in the same slice that added it (trap 34).</para>
/// </summary>
public class MapTargetSurfaceParityTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 9, 19, 20, 0, 0);
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "eqb-target-" + Guid.NewGuid().ToString("N"));

    public MapTargetSurfaceParityTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp dir */ }
        GC.SuppressFinalize(this);
    }

    private static TrackedUpgrade Goal(string item) =>
        new(item, "PRIMARY", "Rusty Dagger", new DateTime(2026, 9, 15, 20, 0, 0, DateTimeKind.Local));

    /// <summary>The shipped exhibit: <i>Blackened Wand</i> drops off <i>Priest Amiaz</i> in
    /// Befallen, and Priest Amiaz is one of Befallen's catalog nameds — so the point that has
    /// seen him is a target AND carries a real learned countdown (S14).</summary>
    private (CompanionMapSource Source, SpawnPointLedger Ledger) Befallen()
    {
        var catalog = SpawnCatalog.LoadEmbedded();
        var ledger = new SpawnPointLedger(Path.Combine(_dir, "ledger"), catalog);
        ledger.Apply(new ZoneEvent(Now.AddMinutes(-10), "Befallen"));
        ledger.Apply(new LocationEvent(Now.AddMinutes(-9), 100, 200, 0));
        ledger.Apply(new KillEvent(Now.AddMinutes(-9), "Priest Amiaz", "Dranak"));
        // A second point, 400 units away, that has only ever seen something else: the
        // committed negative for the ring, in the same zone and the same archive.
        ledger.Apply(new LocationEvent(Now.AddMinutes(-8), 500, 600, 0));
        ledger.Apply(new KillEvent(Now.AddMinutes(-8), "a decaying skeleton", "Dranak"));
        return (new CompanionMapSource(new AppSettings { MapFolder = _dir }), ledger);
    }

    private static CompanionMapRequest In(SpawnPointLedger ledger, GearTargetSet? targets) =>
        new()
        {
            MapZone = "Befallen", TimerZone = "Befallen", Points = ledger, Targets = targets,
        };

    /// <summary>One dot is ringed and the other is not, and the ringed one SAYS which goal and
    /// which creature. A flag with no text would mark a point that has seen five mobs without
    /// saying what for.</summary>
    [Fact]
    public void OnlyThePointThatHasSeenTheCreatureIsATarget()
    {
        var (source, ledger) = Befallen();
        var set = GearTargets.For([Goal("Blackened Wand")], ItemCatalog.Default);

        var map = source.Build(In(ledger, set), Now);

        Assert.Equal(2, map.Circles.Count);
        var target = Assert.Single(map.Circles, c => c.Target);
        Assert.Contains("Blackened Wand", target.TargetText);
        Assert.Contains("Priest Amiaz", target.TargetText);
        // The ring rides BESIDE the named accent rather than replacing it: two facts about one
        // dot, and the circle must not forget which question it was answering.
        Assert.True(target.Named);
        Assert.Equal("Priest Amiaz", target.Label);
        Assert.Equal("", Assert.Single(map.Circles, c => !c.Target).TargetText);
    }

    /// <summary>The block's own sentences, all eight fields, from the producers that own
    /// them — the projection words nothing.</summary>
    [Fact]
    public void TheBlockCarriesTheSameSentencesThePcDraws()
    {
        var (source, ledger) = Befallen();
        var set = GearTargets.For(
            [Goal("Blackened Wand"), Goal("Bronze Long Sword"), Goal("Nothing We Ship")],
            ItemCatalog.Default);

        var t = Assert.IsType<CompanionMapTargets>(source.Build(In(ledger, set), Now).Targets);

        Assert.Equal(GearTargetPresentation.Heading("Befallen"), t.Heading);
        Assert.Equal(GearTargetPresentation.PointsNote, t.Note);
        // Both goals drop in Befallen, so both are rows here…
        Assert.Equal(2, t.Goals.Count);
        Assert.Contains(t.Goals, g => g.StartsWith("Blackened Wand", StringComparison.Ordinal));
        // …and the denominator is the archive's, with only one of the two dots ringed.
        Assert.Equal(GearTargetPresentation.PointsHere(1, 2), t.Points);
        // Bronze Long Sword drops in 22 other zones; Blackened Wand drops only here, so it
        // contributes no elsewhere line at all.
        Assert.Equal(GearTargetPresentation.ElsewhereHeading, t.ElsewhereHeading);
        Assert.Single(t.Elsewhere);
        Assert.Contains("more zones on its page", t.Elsewhere[0]);
        // The refusal, named rather than dropped.
        Assert.Contains("Nothing We Ship", t.Unreadable);
        Assert.Equal("", t.NoDropZone);
    }

    /// <summary>Nothing tracked sends no block at all — not a heading over an empty list, which
    /// is a control that is not there. It is the Helper block's rule and the desktop map's, and
    /// it matters more here because the map is a surface most players open for another reason
    /// entirely.</summary>
    [Fact]
    public void NothingTrackedSendsNoBlockAndNoRings()
    {
        var (source, ledger) = Befallen();

        var empty = source.Build(In(ledger, GearTargets.For([], ItemCatalog.Default)), Now);
        Assert.Null(empty.Targets);
        Assert.All(empty.Circles, c => Assert.False(c.Target));

        // A host that answers nothing at all is the same picture, not a crash.
        Assert.Null(source.Build(In(ledger, null), Now).Targets);
    }

    /// <summary>A goal that drops nowhere near here rings nothing and counts nothing — the
    /// point count is withheld with the goal list it is about, because "none of your 2 points
    /// is one of these" under an empty goal list is counting an absence nobody asked
    /// about.</summary>
    [Fact]
    public void AGoalThatDropsSomewhereElseRingsNothingHereAndSaysWhere()
    {
        var (source, ledger) = Befallen();
        // A Froglok Hex Doll's page names exactly one zone, and it is not this one.
        var set = GearTargets.For([Goal("A Froglok Hex Doll")], ItemCatalog.Default);

        var map = source.Build(In(ledger, set), Now);

        Assert.All(map.Circles, c => Assert.False(c.Target));
        var t = Assert.IsType<CompanionMapTargets>(map.Targets);
        Assert.Empty(t.Goals);
        Assert.Equal("", t.Points);
        // …and it still says where to go, which is the whole answer for this state.
        Assert.NotEmpty(t.Elsewhere);
    }

    /// <summary>
    /// **TRACKING A GOAL WAKES A PAIRED DEVICE** (trap 72).
    ///
    /// <para>A goal tracked or untracked moves no coordinate, no label, no kill count and no
    /// timer, so a fingerprint that carried only the circle geometry would let a phone go on
    /// drawing yesterday's rings for as long as the player stayed in the zone. Both halves are
    /// asserted — the circle key and the block — because they are two places the news could
    /// have been lost.</para>
    /// </summary>
    [Fact]
    public void TheMapKeyMovesWhenAGoalIsTrackedAndWhenOneIsSwappedForAnother()
    {
        var (source, ledger) = Befallen();

        string Key(params TrackedUpgrade[] goals)
        {
            var map = source.Build(
                In(ledger, GearTargets.For(goals, ItemCatalog.Default)), Now);
            return CompanionProjection.SectionFingerprints(
                new CompanionSnapshot { Map = map })[CompanionSurfaces.Map];
        }

        var none = Key();
        var one = Key(Goal("Blackened Wand"));
        // A SWAP: one goal out, one in. Every count in this surface is unmoved, which is the
        // shape a count-folding key cannot see.
        var other = Key(Goal("Bronze Long Sword"));

        Assert.NotEqual(none, one);
        Assert.NotEqual(one, other);
        Assert.Equal(none, Key());
    }

    /// <summary>
    /// **THE PAGE WORDS NOTHING, AND IT DRAWS EVERYTHING IT IS SENT.**
    ///
    /// <para>The first half is trap 32: a sentence baked into <c>index.html</c> goes stale on a
    /// phone that never re-fetches itself. The second is DRA-84 D5's: a field that reaches the
    /// wire and is never drawn passes every assertion above this one.</para>
    /// </summary>
    [Fact]
    public void ThePageSpellsNoneOfTheTargetLayersWordsAndDrawsAllOfItsFields()
    {
        var html = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "EQBuddy.Companion", "Web", "index.html"));

        foreach (var sentence in new[]
                 {
                     GearTargetPresentation.PointsNote,
                     GearTargetPresentation.ElsewhereHeading,
                     GearTargetPresentation.RingTip,
                     GearTargetPresentation.Heading("Befallen"),
                     GearTargetPresentation.PointsHere(1, 2),
                     GearTargetPresentation.PointsHere(0, 0),
                     GearTargetPresentation.Unreadable(["Blackened Wand"]),
                     GearTargetPresentation.NoDropZone(["Blackened Wand"]),
                     // The toggle's two tips (D5 Planner review, D5-1). These are forbidden for
                     // a second reason on top of trap 32: the chip is a PC control, and a phone
                     // that spelled its words would be offering an affordance it cannot honour —
                     // every control in that room writes the profile the PC plays from
                     // (trap 35). The layer is switched where the switch is.
                     GearTargetPresentation.ToggleTip(true),
                     GearTargetPresentation.ToggleTip(false),
                 })
            Assert.DoesNotContain(sentence, html, StringComparison.Ordinal);

        // Every field `CompanionMapTargets` carries, plus the circle's two. A ninth added
        // without a row here is DRA-84 D5's bug again.
        foreach (var field in new[]
                 {
                     "drawTargets", "data.targets", "t.heading", "t.note", "t.goals",
                     "t.points", "t.elsewhereHeading", "t.elsewhere", "t.unreadable",
                     "t.noDropZone", "c.target", "c.targetText",
                 })
            Assert.Contains(field, html, StringComparison.Ordinal);
    }
}
