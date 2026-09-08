using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The Buffs card's roster (OE-4): what it shows, what each chip reads, and what has to
/// change before it rebuilds.
///
/// The card itself is WPF and has no unit tests (docs/TestPlan.md §5) — which is why every
/// decision it makes is in <see cref="BuffRosterPresentation"/> and asserted here with no
/// window. The two things only a launched app can answer (the chips are in a WrapPanel, and
/// there are N of them on screen) are pinned in <c>tests/EQBuddy.E2E</c> instead.
/// </summary>
public class BuffRosterPresentationTests
{
    private static readonly DateTime T0 = new(2026, 9, 6, 20, 0, 0);

    private static GameEvent Ev(int seconds, string message) =>
        LogParser.Parse($"[{T0.AddSeconds(seconds):ddd MMM d HH:mm:ss yyyy}] {message}")!;

    private static BuffState Buff(string label, double? seconds, bool estimated = true,
        string caster = "You", string[]? candidates = null, double landedAgo = 0) =>
        new(label, candidates ?? [label], caster,
            T0.AddSeconds(-landedAgo),
            seconds is { } s ? T0.AddSeconds(s - landedAgo) : null,
            estimated);

    // ---- what the roster shows ----

    /// <summary>The default: the WHOLE roster, in the order the tracker handed over
    /// (soonest to fade first), and nothing kept quiet.</summary>
    [Fact]
    public void TheRosterShowsEveryActiveBuffByDefault()
    {
        List<BuffState> active = [Buff("Valor", 60), Buff("Aegolism", 9000)];

        var shown = BuffRosterPresentation.Shown(active, T0, expiringOnly: false, 60, out var quiet);

        Assert.Equal(["Valor", "Aegolism"], shown.Select(b => b.Label));
        Assert.Equal(0, quiet);
    }

    /// <summary>Expiring-only mode keeps the card quiet until a buff is inside the warning
    /// window — and COUNTS what it is not showing. A list that hides rows and looks complete
    /// is a silent no-op with the switch on the other side (trap 50's closing rule).</summary>
    [Fact]
    public void ExpiringOnlyHidesTheCalmBuffsAndCountsThem()
    {
        List<BuffState> active = [Buff("Valor", 45), Buff("Aegolism", 9000), Buff("Health", 3000)];

        var shown = BuffRosterPresentation.Shown(active, T0, expiringOnly: true, 60, out var quiet);

        Assert.Equal(["Valor"], shown.Select(b => b.Label));
        Assert.Equal(2, quiet);
    }

    /// <summary>The threshold is the player's own <c>BuffWarnSeconds</c> through
    /// <see cref="HudChipRow.BuffWarnWindow"/>, floor included — the same one function the HUD
    /// chip and the buff set ask. Three surfaces, one answer to "when is a buff urgent".
    /// </summary>
    [Fact]
    public void TheQuietFilterUsesThePlayersOwnWarnWindow()
    {
        List<BuffState> active = [Buff("Health", 240)];

        Assert.Empty(BuffRosterPresentation.Shown(active, T0, true, 60, out _));
        Assert.Single(BuffRosterPresentation.Shown(active, T0, true, 300, out _));
        // Below the floor the window is ten seconds, not the number typed.
        Assert.Empty(BuffRosterPresentation.Shown([Buff("Health", 20)], T0, true, 1, out _));
    }

    // ---- what a chip reads ----

    /// <summary>PREDICTION: an hour-long buff landed two minutes ago reads "58:00" on an
    /// hourglass chip in the buff family, with 3.3% of its duration elapsed — so the gauge,
    /// which DRAINS for this family, paints 96.7%. (Written as "28:00" first, off a wrong
    /// duration; the fraction assertions beside it held throughout, which is what said the
    /// prediction was wrong rather than the code.)</summary>
    [Fact]
    public void AChipReadsItsCountdownAndDrainsAcrossTheBuffsOwnDuration()
    {
        var entry = Assert.Single(BuffRosterPresentation.Chips(
            [Buff("Riftwind's Protection", 3600, landedAgo: 120)], T0, 60));

        Assert.Equal(HudChipFamily.Buff, entry.Family);
        Assert.Equal("Riftwind's Protection", entry.Chip.Name);
        Assert.Equal("58:00", entry.Chip.CountdownText);
        Assert.Equal("Hourglass", entry.Chip.Icon);
        Assert.Equal(120 / 3600d, entry.Chip.Fraction!.Value, 3);
        Assert.Equal(1 - 120 / 3600d, HudChipRow.GaugeShare(entry)!.Value, 3);
    }

    /// <summary>The roster's gauge is NOT the HUD chicklet's. That one measures the warning
    /// window, because a 27-minute buff that earns a chicklet for its last minute would draw
    /// a bar frozen at 99%; a roster chip is on screen for the buff's whole life, so its bar
    /// is the buff. Same buff, same instant, two honest numbers — this is the negative that
    /// keeps the pair from being "consolidated" into one.</summary>
    [Fact]
    public void TheRosterGaugeMeasuresTheBuffWhereTheHudChickletMeasuresTheWindow()
    {
        var buff = Buff("Aegolism", 9000, landedAgo: 4500);

        var roster = Assert.Single(BuffRosterPresentation.Chips([buff], T0, 60));

        Assert.Equal(0.5, roster.Chip.Fraction!.Value, 3);
        Assert.NotEqual(0.5, BuffRosterPresentation.ElapsedShare(buff, T0.AddSeconds(4470))!.Value, 3);
    }

    /// <summary>A landing nobody could attribute has no duration at all: the face says so
    /// rather than inventing one, and the chip's track hides rather than painting progress it
    /// does not have.</summary>
    [Fact]
    public void AnUnattributedLandingHasNoClockAndNoGauge()
    {
        var entry = Assert.Single(BuffRosterPresentation.Chips(
            [Buff("You feel different.", null)], T0, 60));

        Assert.Equal("?", entry.Chip.CountdownText);
        Assert.Null(entry.Chip.Fraction);
        Assert.Null(HudChipRow.GaugeShare(entry));
    }

    /// <summary>The formatter marks an estimate and ONLY an estimate — the negative is the
    /// point, since a formatter that always appended the word would read as coverage while
    /// saying nothing.</summary>
    [Fact]
    public void EstMarksAnEstimateAndOnlyAnEstimate()
    {
        Assert.Equal("1:00 est", BuffRosterPresentation.Clock(60, estimated: true));
        Assert.Equal("1:00", BuffRosterPresentation.Clock(60, estimated: false));
        Assert.Equal("?", BuffRosterPresentation.Clock(null, estimated: true));
    }

    /// <summary>
    /// THE ROSTER FACE DROPS "est" AND THE HUD CHICKLET KEEPS IT — measured, not assumed.
    ///
    /// Bevel's item 1 flagged this as the one question its source read could not answer, and
    /// `docs/screenshots/buffs-card.png` was shot twice against the same eight staged buffs
    /// to answer it: five wrapped rows with the suffix, four without. On a card that is 320
    /// units wide the marker is most of the density the change exists to deliver, and it
    /// marks nothing anyway — the tracker calls a duration estimated until a natural fade
    /// teaches it, so a full roster is a column of identical suffixes.
    ///
    /// This test is the divergence written down on purpose. The two surfaces show one buff
    /// two ways, and the claim survives in the roster's hover, which has room for the whole
    /// sentence rather than four characters of it.
    /// </summary>
    [Fact]
    public void TheRosterMovesTheEstimateMarkerToItsHoverAndTheHudChickletDoesNot()
    {
        var buff = Buff("Valor", 45);

        Assert.Equal("0:45", BuffRosterPresentation.RosterFace(buff, T0));
        Assert.Equal("0:45", Assert.Single(
            BuffRosterPresentation.Chips([buff], T0, 60)).Chip.CountdownText);
        Assert.Contains("est = catalog length", BuffRosterPresentation.Detail(buff));

        // The real parser and the real tracker, so this reads the shipped catalog's own
        // 3,240 s Valor: landed at +3 s and read at +6 s, the chicklet has 53:57 left.
        var tracker = new BuffTracker();
        tracker.Apply(Ev(0, "Sanctari begins casting Valor."));
        tracker.Apply(Ev(3, "You feel valorous."));
        Assert.Equal("53:57 est",
            Assert.Single(HudChipRow.BuffChips(tracker, T0.AddSeconds(6), 4000)).CountdownText);
    }

    /// <summary>The chip trims its name at <see cref="BuffRosterPresentation.NameMaxWidth"/>,
    /// so the hover has to open with the FULL one — a roster of "Riftwind's Pr…" that says
    /// nothing on hover is the density change costing the player the answer it was meant to
    /// make easier to see.</summary>
    [Fact]
    public void TheHoverOpensWithTheFullNameThenSaysWhereTheBuffCameFrom()
    {
        var detail = BuffRosterPresentation.Detail(
            Buff("Riftwind's Protection", 3600, caster: "Sanctari"));

        Assert.StartsWith("Riftwind's Protection", detail);
        Assert.Contains("cast by Sanctari", detail);
        Assert.Contains("landed", detail);
        Assert.Contains("est = catalog length", detail);
    }

    /// <summary>An unresolved landing names every candidate instead of the label — the honest
    /// range, which is what the card has always shown and what the HUD chicklet now reads
    /// from this same one producer.</summary>
    [Fact]
    public void AnUnresolvedLandingListsItsCandidates()
    {
        var detail = BuffRosterPresentation.Detail(Buff("Magic Awareness line", 11700,
            caster: "", candidates: ["Magic Awareness I", "Magic Awareness II"]));

        Assert.StartsWith("One of: Magic Awareness I, Magic Awareness II", detail);
        Assert.DoesNotContain("cast by", detail);
    }

    /// <summary>Warn ink and a warn border come on at the player's warn window — the card
    /// used to tint at a hard-coded 60 while the setting said something else, which is one
    /// fact with two sources. A default profile (60) sees exactly what it saw before.
    /// </summary>
    [Fact]
    public void UrgencyIsThePlayersWarnWindowAndNotAHardCodedMinute()
    {
        Assert.True(BuffRosterPresentation.IsUrgent(Buff("Valor", 45), T0, 60));
        Assert.False(BuffRosterPresentation.IsUrgent(Buff("Valor", 90), T0, 60));
        Assert.True(BuffRosterPresentation.IsUrgent(Buff("Valor", 90), T0, 120));
        // A buff family chip never flips to the word DUE — there is no moment, only a recast.
        var entry = Assert.Single(BuffRosterPresentation.Chips([Buff("Valor", 4)], T0, 60));
        Assert.True(entry.Chip.IsDue);
        Assert.Equal("0:04", HudChipRow.FaceText(entry));
    }

    // ---- rebuild gate ----

    /// <summary>The signature moves when the SET of buffs changes and stands still while only
    /// the clocks do. Including a countdown would rebuild the whole panel once a second,
    /// which is trap 8's rule one surface over.</summary>
    [Fact]
    public void TheSignatureIgnoresCountdownsAndNoticesTheSet()
    {
        List<BuffState> one = [Buff("Valor", 3240)];
        var at0 = BuffRosterPresentation.Signature(one, 0, [], [], [], []);
        var at30 = BuffRosterPresentation.Signature(
            [Buff("Valor", 3240, landedAgo: 30)], 0, [], [], [], []);

        Assert.Equal(at0, at30);
        Assert.NotEqual(at0, BuffRosterPresentation.Signature(
            [Buff("Valor", 3240), Buff("Health", 3000)], 0, [], [], [], []));
        // The quiet COUNT is part of it: in expiring-only mode it is the only thing on
        // screen, so a change in it has to repaint.
        Assert.NotEqual(at0, BuffRosterPresentation.Signature(one, 2, [], [], [], []));
        // And so is every part of the set line — a buff going missing changes no chip.
        Assert.NotEqual(at0, BuffRosterPresentation.Signature(one, 0, ["Aegolism"], [], [], []));
        Assert.NotEqual(at0, BuffRosterPresentation.Signature(one, 0, [], ["Aegolism"], [], []));
        Assert.NotEqual(at0, BuffRosterPresentation.Signature(one, 0, [], [], ["Aegolism"], []));
        Assert.NotEqual(at0, BuffRosterPresentation.Signature(one, 0, [], [], [], ["Valor@Cleric"]));
    }

    /// <summary>A buff whose duration stops being an estimate is a DIFFERENT chip: the hover
    /// is built once, at build time, so a text-in-place tick would go on offering "est = wiki
    /// base" for a duration the app had just learned for real.</summary>
    [Fact]
    public void LearningARealDurationRebuildsTheChip() =>
        Assert.NotEqual(
            BuffRosterPresentation.Signature([Buff("Valor", 3240)], 0, [], [], [], []),
            BuffRosterPresentation.Signature([Buff("Valor", 3240, estimated: false)], 0, [], [], [], []));

    // ---- the empty state ----

    /// <summary>Two different facts, said differently: nothing is running, versus you asked
    /// to be told late and N buffs are running. The second one names the threshold, because
    /// "why is my card empty" is answerable only if it does.</summary>
    [Fact]
    public void TheEmptyLineSaysWhichKindOfEmptyThisIs()
    {
        Assert.Contains("Nothing running",
            BuffRosterPresentation.EmptyLine(expiringOnly: false, quiet: 0, 60));
        Assert.Contains("Nothing running",
            BuffRosterPresentation.EmptyLine(expiringOnly: true, quiet: 0, 60));

        var quiet = BuffRosterPresentation.EmptyLine(expiringOnly: true, quiet: 3, 60);
        Assert.Contains("3 running quietly", quiet);
        Assert.Contains("60s left", quiet);
        Assert.Contains("10s left", BuffRosterPresentation.EmptyLine(true, 3, 1));
    }
}
