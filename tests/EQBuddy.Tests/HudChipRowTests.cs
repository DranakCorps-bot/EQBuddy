using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// THE ONE CHIP ROW's merge decision (Surface A / SA-2) — which families are on it, in what
/// order, how each family's chicklet reads, and where the slaved companion parks.
///
/// It exists because the fold had to be honest about a difference that was easy to flatten:
/// `SpawnChipsWindow` and `MezChipsWindow` did NOT render identically (one flips a due chip
/// to the word "DUE" and fills its gauge, the other keeps counting and drains), and merging
/// two near-copies into one renderer is exactly where a silent behaviour change hides. The
/// WPF layer has no unit tests (docs/TestPlan.md §5), so these traits are asserted here or
/// nowhere.
///
/// The two family BUILDERS came out of `MainWindow` with the row, which is the first time
/// the mez numbering ("orc pawn (2)") has been assertable at all.
/// </summary>
public class HudChipRowTests
{
    private static SpawnChip Chip(string name, string countdown = "3:12", bool due = false,
        double? fraction = null, string zone = "") =>
        new(zone, name, countdown, due, "detail") { Fraction = fraction };

    // ---- Family order and instance order ----

    /// <summary>Mez before spawn: combat-urgent before ambient, which is the distinction the
    /// two retired windows' own doc comments drew.</summary>
    [Fact]
    public void MezComesFirstByDefault()
    {
        var row = HudChipRow.Merge([Chip("a skeleton")], [Chip("Asaka L`Rei")]);

        Assert.Equal([HudChipFamily.Mez, HudChipFamily.Spawn], row.Select(e => e.Family));
        Assert.Equal(["a skeleton", "Asaka L`Rei"], row.Select(e => e.Chip.Name));
    }

    /// <summary>Instance order inside a family is whatever the family handed over — no
    /// global "soonest first" re-sort. A row that re-sorted every second would move a
    /// chicklet out from under the cursor mid-click, on a surface whose click DISMISSES.
    /// </summary>
    [Fact]
    public void InstanceOrderWithinAFamilyIsPreserved()
    {
        var row = HudChipRow.Merge(
            [Chip("first", "0:30"), Chip("second", "9:00"), Chip("third", "0:05")], []);

        Assert.Equal(["first", "second", "third"], row.Select(e => e.Chip.Name));
    }

    [Fact]
    public void AFamilyWithNoChipsContributesNothing()
    {
        Assert.Empty(HudChipRow.Merge([], []));
        Assert.Single(HudChipRow.Merge([], [Chip("Asaka L`Rei")]));
    }

    /// <summary>The order argument is the seam SA-4's `HudChipOrder` reads. A family missing
    /// from a supplied order is DROPPED, never appended — SA-4's Mute is a per-family
    /// absence, and an order that silently re-added what mute removed would be two answers
    /// to one question.</summary>
    [Fact]
    public void AnExplicitOrderReordersAndCanOmitAFamily()
    {
        var mez = new[] { Chip("a skeleton") };
        var spawn = new[] { Chip("Asaka L`Rei") };

        Assert.Equal([HudChipFamily.Spawn, HudChipFamily.Mez],
            HudChipRow.Merge(mez, spawn, [HudChipFamily.Spawn, HudChipFamily.Mez])
                .Select(e => e.Family));

        var spawnOnly = HudChipRow.Merge(mez, spawn, [HudChipFamily.Spawn]);
        Assert.Single(spawnOnly);
        Assert.Equal(HudChipFamily.Spawn, spawnOnly[0].Family);
    }

    // ---- The DUE flip, which the two windows did differently ----

    /// <summary>A due SPAWN chip replaces its countdown with the word "DUE" — the camp has
    /// popped and the chip has said its piece.</summary>
    [Fact]
    public void ADueSpawnChipFlipsToTheWordDue()
        => Assert.Equal("DUE", HudChipRow.FaceText(
            new HudChipEntry(HudChipFamily.Spawn, Chip("Asaka L`Rei", "0:00", due: true))));

    /// <summary>A due MEZ chip does NOT. "Due" there means "inside the last tick before the
    /// wake-up", and the number is the whole point of watching it; the warning tint is the
    /// signal. Flattening this into one rule at fold time is the change nothing else could
    /// have caught.</summary>
    [Fact]
    public void ADueMezChipKeepsCountingAndDoesNotSayDue()
        => Assert.Equal("0:04", HudChipRow.FaceText(
            new HudChipEntry(HudChipFamily.Mez, Chip("a skeleton", "0:04", due: true))));

    [Fact]
    public void OnlySpawnFlipsToDue()
    {
        Assert.True(HudChipRow.FlipsToDue(HudChipFamily.Spawn));
        Assert.False(HudChipRow.FlipsToDue(HudChipFamily.Mez));
    }

    // ---- The gauge, which the two windows also did differently ----

    /// <summary>Both families are handed the same 0..1 ELAPSED share and paint opposite
    /// sides of it: spawn fills toward the respawn, the fight family drains what is left,
    /// like a buff bar. One input, two paints, one place that knows which.</summary>
    [Fact]
    public void SpawnFillsElapsedAndTheFightFamilyDrainsRemaining()
    {
        Assert.Equal(0.75, HudChipRow.GaugeShare(
            new HudChipEntry(HudChipFamily.Spawn, Chip("Asaka L`Rei", fraction: 0.75))));
        Assert.Equal(0.25, HudChipRow.GaugeShare(
            new HudChipEntry(HudChipFamily.Mez, Chip("a skeleton", fraction: 0.75))));
    }

    /// <summary>No known duration, no gauge — the track hides rather than lying about
    /// progress it cannot know.</summary>
    [Fact]
    public void NoFractionMeansNoGauge()
        => Assert.Null(HudChipRow.GaugeShare(
            new HudChipEntry(HudChipFamily.Mez, Chip("a skeleton", "?"))));

    /// <summary>A DUE spawn chip fills solid even with no fraction: a bar frozen at 97%
    /// under the word "DUE" would be two answers to one question.</summary>
    [Fact]
    public void ADueSpawnChipFillsSolid()
    {
        Assert.Equal(1.0, HudChipRow.GaugeShare(
            new HudChipEntry(HudChipFamily.Spawn, Chip("Asaka L`Rei", "0:00", due: true))));
        Assert.Equal(1.0, HudChipRow.GaugeShare(new HudChipEntry(
            HudChipFamily.Spawn, Chip("Asaka L`Rei", "0:00", due: true, fraction: 0.97))));
    }

    /// <summary>A mez chip inside its last tick keeps draining — it is still counting.
    /// </summary>
    [Fact]
    public void ADueMezChipKeepsDraining()
    {
        var share = HudChipRow.GaugeShare(new HudChipEntry(
            HudChipFamily.Mez, Chip("a skeleton", "0:04", due: true, fraction: 0.95)));
        Assert.Equal(0.05, Assert.NotNull(share), 6);
    }

    // ---- Counts, for the hudChips dump family ----

    [Fact]
    public void CountsAreReportedPerFamilyAndForDueChips()
    {
        var row = HudChipRow.Merge(
            [Chip("a skeleton"), Chip("a skeleton (2)", "0:03", due: true)],
            [Chip("Asaka L`Rei", "0:00", due: true), Chip("Ghoul Lord")]);

        Assert.Equal(2, HudChipRow.CountOf(row, HudChipFamily.Mez));
        Assert.Equal(2, HudChipRow.CountOf(row, HudChipFamily.Spawn));
        Assert.Equal(2, HudChipRow.DueCount(row));
    }

    // ---- The rebuild signature ----

    /// <summary>Identity, not values: a tick that only moved the countdowns must not rebuild
    /// the row (that is the flicker, and the reason the in-place tick path exists).</summary>
    [Fact]
    public void TheSignatureIgnoresACountdownTicking()
        => Assert.Equal(
            HudChipRow.Signature(HudChipRow.Merge([Chip("a skeleton", "0:30")], [])),
            HudChipRow.Signature(HudChipRow.Merge([Chip("a skeleton", "0:29")], [])));

    [Fact]
    public void TheSignatureChangesWhenAChipArrivesOrGoesDue()
    {
        var one = HudChipRow.Signature(HudChipRow.Merge([Chip("a skeleton")], []));
        Assert.NotEqual(one, HudChipRow.Signature(
            HudChipRow.Merge([Chip("a skeleton"), Chip("a putrid skeleton")], [])));
        Assert.NotEqual(one, HudChipRow.Signature(
            HudChipRow.Merge([Chip("a skeleton", due: true)], [])));
    }

    /// <summary>Two families holding an identically-named chip are two rows, not one. The
    /// two windows' signatures could never collide because they were different windows; one
    /// row is exactly where that could start.</summary>
    [Fact]
    public void TheSignatureSeparatesTheFamilies()
        => Assert.NotEqual(
            HudChipRow.Signature(HudChipRow.Merge([Chip("Asaka L`Rei")], [])),
            HudChipRow.Signature(HudChipRow.Merge([], [Chip("Asaka L`Rei")])));

    /// <summary>Dismissing the LAST chip makes the new signature the empty string, so the
    /// reset value cannot be "" or the rebuild is skipped and a ghost chicklet stays painted
    /// (PR #67). The sentinel is what keeps that fixed through the merge.</summary>
    [Fact]
    public void TheDismissSentinelIsNotTheEmptyRowsSignature()
        => Assert.NotEqual(HudChipRow.Signature([]), HudChipRow.DismissedSignature);

    // ---- Placement of the slaved companion ----

    /// <summary>Directly under the widget, left edges aligned. Nothing is persisted: this is
    /// recomputed from the widget every tick, which is what retires the whole trap-2
    /// (#122/#152) saved-position family.</summary>
    [Fact]
    public void TheRowParksUnderTheWidget()
        => Assert.Equal((100, 200 + 60 + HudChipRow.HudGap), HudChipRow.Placement(
            hudLeft: 100, hudTop: 200, hudHeight: 60, rowHeight: 24,
            workAreaTop: 0, workAreaBottom: 1000));

    /// <summary>…and goes ABOVE it instead when there is no room below. A chicklet half off
    /// the screen is the same defect as one that never drew.</summary>
    [Fact]
    public void TheRowFlipsAboveTheWidgetAtTheBottomOfTheScreen()
        => Assert.Equal((100, 950 - HudChipRow.HudGap - 24), HudChipRow.Placement(
            hudLeft: 100, hudTop: 950, hudHeight: 60, rowHeight: 24,
            workAreaTop: 0, workAreaBottom: 1000));

    /// <summary>A widget wedged against BOTH edges of a short work area keeps the space
    /// below: flipping above would only move the same overflow to the other end.</summary>
    [Fact]
    public void WithNoRoomEitherWayItStaysBelow()
        => Assert.Equal((0, 10 + 60 + HudChipRow.HudGap), HudChipRow.Placement(
            hudLeft: 0, hudTop: 10, hudHeight: 60, rowHeight: 400,
            workAreaTop: 0, workAreaBottom: 100));

    /// <summary>A height that is not real yet — the first layout pass — takes the space
    /// below without a flip. "We cannot tell yet" and "draw where you always draw" are the
    /// same instruction.</summary>
    [Theory]
    [InlineData(0d)]
    [InlineData(double.NaN)]
    public void AnUnmeasuredRowDoesNotFlip(double rowHeight)
        => Assert.Equal((100, 200 + 60 + HudChipRow.HudGap), HudChipRow.Placement(
            hudLeft: 100, hudTop: 200, hudHeight: 60, rowHeight: rowHeight,
            workAreaTop: 0, workAreaBottom: 210));

    /// <summary>A negative Left is legitimate on a multi-monitor desk and is left alone —
    /// the row is slaved to the widget, so clamping it against the primary monitor would
    /// tear the two apart (the same reasoning `WidgetMetrics.RightAnchoredLeft` gives).
    /// </summary>
    [Fact]
    public void TheHorizontalPositionIsNeverClamped()
        => Assert.Equal(-1400d, HudChipRow.Placement(
            hudLeft: -1400, hudTop: 100, hudHeight: 60, rowHeight: 24,
            workAreaTop: 0, workAreaBottom: 1000).Left);

    // ---- The family builders, lifted out of MainWindow with the row ----

    private static readonly DateTime T0 = DateTime.Parse("2026-09-05T12:00:00");

    private static GameEvent Ev(int seconds, string message) =>
        LogParser.Parse($"[{T0.AddSeconds(seconds):ddd MMM d HH:mm:ss yyyy}] {message}")!;

    private static MezTracker Mezzed(params string[] targets)
    {
        var t = new MezTracker();
        t.Apply(Ev(0, "You begin casting Mesmerization."));
        foreach (var target in targets) t.Apply(Ev(1, $"{target} has been mesmerized."));
        return t;
    }

    /// <summary>Same-named mezzes are NUMBERED, because the log cannot tell the creatures
    /// apart (#32 asked for separate timers rather than one merged chip). This numbering
    /// lived in the WPF layer, where nothing could assert it, until SA-2.</summary>
    [Fact]
    public void SameNamedMezChipsAreNumberedAndUniqueOnesAreNot()
    {
        var chips = HudChipRow.MezChips(
            Mezzed("a skeleton", "a skeleton", "a putrid skeleton"), T0.AddSeconds(2));

        Assert.Equal(["Skeleton (1)", "Skeleton (2)", "Putrid skeleton"],
            chips.Select(c => c.Name));
    }

    /// <summary>The mez chicklet's mark is an IconPaths NAME, never a glyph: on the Wine
    /// prefixes where "💤" does not render, the one surface a player watches mid-pull told
    /// its kinds apart with identical boxes (#148, #166).</summary>
    [Fact]
    public void MezChipsWearTheMoonVectorAndNeverAGlyph()
    {
        var chip = Assert.Single(HudChipRow.MezChips(Mezzed("a skeleton"), T0.AddSeconds(2)));
        Assert.Equal("Moon", chip.Icon);
    }

    /// <summary>An unknown duration reads "?" and carries no gauge — the chip still shows
    /// the mez and still clears on break.</summary>
    [Fact]
    public void AMezWithNoKnownDurationReadsAQuestionMarkAndHasNoGauge()
    {
        // A catalog entry with its duration nulled — the pre-research state, the same way
        // MezTrackerTests stages it. The chip still appears and still clears on break.
        var t = new MezTracker([new MezSpellInfo { Name = "Mesmerize" }]);
        t.Apply(Ev(0, "You begin casting Mesmerize."));
        t.Apply(Ev(1, "a skeleton has been mesmerized."));
        var chip = Assert.Single(HudChipRow.MezChips(t, T0.AddSeconds(2)));

        Assert.Equal("?", chip.CountdownText);
        Assert.Null(chip.Fraction);
        Assert.Null(HudChipRow.GaugeShare(new HudChipEntry(HudChipFamily.Mez, chip)));
    }
}
