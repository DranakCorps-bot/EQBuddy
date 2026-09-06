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
            HudChipRow.Merge(mez, spawn, order: [HudChipFamily.Spawn, HudChipFamily.Mez])
                .Select(e => e.Family));

        var spawnOnly = HudChipRow.Merge(mez, spawn, order: [HudChipFamily.Spawn]);
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

    // ---- SA-3: the two net-new deadline families ----

    /// <summary>The four families come out in the order SA-4's signed default already names —
    /// "mez, spawn, watch-fire, buff", urgency order — so the setting that arrives next
    /// reads a list this file has already pinned rather than one an executor invented.
    /// </summary>
    [Fact]
    public void TheFourFamiliesLandInTheSignedDefaultOrder()
    {
        var row = HudChipRow.Merge(
            [Chip("a skeleton")], [Chip("Asaka L`Rei")],
            [Chip("Rares")], [Chip("Clarity")]);

        Assert.Equal(
            [HudChipFamily.Mez, HudChipFamily.Spawn, HudChipFamily.WatchFire, HudChipFamily.Buff],
            row.Select(e => e.Family));
        Assert.Equal(HudChipRow.DefaultOrder, row.Select(e => e.Family));
    }

    /// <summary>SPAWN is the only family whose gauge fills. Everything else on the row is a
    /// thing going away — a mez, a slow, a warned buff, a lingering alert — and draws the
    /// share it has LEFT. Asserted as a table rather than per family so a fifth member of the
    /// enum cannot quietly get the wrong half (trap 30's shape).</summary>
    [Fact]
    public void OnlyTheSpawnFamilyFillsItsGauge()
    {
        foreach (var family in Enum.GetValues<HudChipFamily>())
            Assert.Equal(family != HudChipFamily.Spawn, HudChipRow.GaugeDrains(family));
    }

    /// <summary>…and SPAWN is the only family that flips a due chip to the word "DUE". A buff
    /// inside its last tick is still counting toward a recast, and a watch-fire chip has no
    /// due moment at all — its countdown is its own linger.</summary>
    [Fact]
    public void OnlyTheSpawnFamilyFlipsToDue()
    {
        foreach (var family in Enum.GetValues<HudChipFamily>())
            Assert.Equal(family == HudChipFamily.Spawn, HudChipRow.FlipsToDue(family));
    }

    // -- Watch-fire --

    private static WatchFireLedger Fired(DateTime at, params (string Id, string Name)[] rules)
    {
        var ledger = new WatchFireLedger();
        foreach (var (id, name) in rules) ledger.Record(id, name, $"{name} matched", at);
        return ledger;
    }

    /// <summary>
    /// A fired rule's chicklet: the RULE'S NAME on the face, the linger as the countdown, the
    /// match in the tooltip where it has room, and a Bell.
    ///
    /// PREDICTION, written before it ran: five seconds into a thirty-second linger the face
    /// reads "0:25", the elapsed fraction is 5/30, and the drained gauge share is 25/30.
    /// </summary>
    [Fact]
    public void AFiredRuleWearsItsNameAndCountsItsLingerDown()
    {
        var chip = Assert.Single(HudChipRow.WatchChips(
            Fired(T0, ("r1", "Rares")), T0.AddSeconds(5)));

        Assert.Equal("Rares", chip.Name);
        Assert.Equal("0:25", chip.CountdownText);
        Assert.Equal("Bell", chip.Icon);
        Assert.Contains("Rares matched", chip.Detail);
        Assert.False(chip.IsDue);
        Assert.Equal(5 / 30d, chip.Fraction!.Value, 3);
        Assert.Equal(25 / 30d, HudChipRow.GaugeShare(
            new HudChipEntry(HudChipFamily.WatchFire, chip))!.Value, 3);
    }

    /// <summary>A rule that fires again refreshes its own chicklet rather than adding a
    /// second one. A Text rule on a busy channel fires repeatedly, and a ledger keyed on the
    /// firing would put a dozen identical chicklets on the row inside one pull.</summary>
    [Fact]
    public void ReFiringOneRuleRefreshesItsChipInsteadOfAddingAnother()
    {
        var ledger = Fired(T0, ("r1", "Rares"));
        ledger.Record("r1", "Rares", "a second match", T0.AddSeconds(20));

        var chip = Assert.Single(HudChipRow.WatchChips(ledger, T0.AddSeconds(25)));
        Assert.Equal("0:25", chip.CountdownText);       // 25 s after the SECOND firing
        Assert.Contains("a second match", chip.Detail);  // and the newer label
    }

    /// <summary>Two rules are two chicklets, newest first: the freshest alert is the one you
    /// are most likely to be looking for.</summary>
    [Fact]
    public void SeveralRulesAreSeveralChipsNewestFirst()
    {
        var ledger = Fired(T0, ("r1", "Rares"));
        ledger.Record("r2", "Named up", "Frenzied Ghoul", T0.AddSeconds(10));

        Assert.Equal(["Named up", "Rares"],
            HudChipRow.WatchChips(ledger, T0.AddSeconds(11)).Select(c => c.Name));
    }

    /// <summary>The linger runs out and the chicklet goes — with the boundary asserted on
    /// both sides, because "still there at 29 s" and "gone at 30 s" are the two claims and a
    /// one-sided test would pass with the constant doubled.</summary>
    [Fact]
    public void AChipLeavesWhenItsLingerRunsOut()
    {
        var ledger = Fired(T0, ("r1", "Rares"));

        Assert.Single(HudChipRow.WatchChips(ledger, T0.AddSeconds(29)));
        Assert.Empty(HudChipRow.WatchChips(ledger, T0.AddSeconds(30)));
        Assert.False(ledger.Any(T0.AddSeconds(30)));
    }

    /// <summary>Right-click dismisses this screen's chicklet, never the rule: the next firing
    /// brings it straight back, exactly as a dismissed slow chip does.</summary>
    [Fact]
    public void DismissingAWatchChipDropsItAndTheNextFiringBringsItBack()
    {
        var ledger = Fired(T0, ("r1", "Rares"));
        Assert.Single(HudChipRow.WatchChips(ledger, T0.AddSeconds(1))).OnDismiss!();

        Assert.Empty(HudChipRow.WatchChips(ledger, T0.AddSeconds(2)));
        ledger.Record("r1", "Rares", "matched again", T0.AddSeconds(3));
        Assert.Single(HudChipRow.WatchChips(ledger, T0.AddSeconds(4)));
    }

    // -- Buff expiring --

    /// <summary>"Armor of Faith" is 3,780 s in the shipped catalog and lands at +3 s — the
    /// same pair `BuffTrackerTests` uses, through the real parser and the real tracker.
    /// </summary>
    private static BuffTracker Buffed()
    {
        var t = new BuffTracker();
        t.Apply(Ev(0, "You begin casting Armor of Faith."));
        t.Apply(Ev(3, "You feel the favor of the gods upon you."));
        return t;
    }

    /// <summary>
    /// A buff only earns a chicklet once it is inside the warning window, and the window is
    /// the player's own `BuffWarnSeconds` — the answer they already gave the Buffs card.
    ///
    /// PREDICTION: with the default 60 s window, a 3,780 s buff landed at +3 s has no chip an
    /// hour in (3,183 s left) and one chip at +3,750 s (33 s left).
    /// </summary>
    [Fact]
    public void ABuffGetsNoChipUntilItIsInsideTheWarningWindow()
    {
        var t = Buffed();

        Assert.Empty(HudChipRow.BuffChips(t, T0.AddSeconds(600), warnSeconds: 60));
        Assert.Single(HudChipRow.BuffChips(t, T0.AddSeconds(3750), warnSeconds: 60));
    }

    /// <summary>Widen the player's window and the same buff earns its chip earlier. This is
    /// the assertion that the threshold is READ rather than pinned: a hard-coded 60 would
    /// pass every test above and fail this one.</summary>
    [Fact]
    public void AWiderWarningWindowBringsTheChipOutSooner()
        => Assert.Single(HudChipRow.BuffChips(Buffed(), T0.AddSeconds(600), warnSeconds: 3600));

    /// <summary>
    /// The chicklet itself.
    ///
    /// PREDICTION: at +3,750 s the buff has 33 s left, so the face reads "0:33 est" — "est"
    /// because a wiki-base duration is a floor until a natural fade teaches the real number,
    /// which is the same word the card uses. The gauge is measured against the WINDOW, not
    /// the spell: 33 of 60 seconds left, so 45% elapsed and 55% painted. Against the spell it
    /// would be a bar frozen at 99% for the chip's whole life.
    /// </summary>
    [Fact]
    public void TheBuffChipReadsItsRemainingTimeAndDrainsAcrossTheWarningWindow()
    {
        var chip = Assert.Single(HudChipRow.BuffChips(Buffed(), T0.AddSeconds(3750), 60));

        Assert.Equal("Armor of Faith", chip.Name);
        Assert.Equal("0:33 est", chip.CountdownText);
        Assert.Equal("Hourglass", chip.Icon);
        Assert.False(chip.IsDue);
        Assert.Equal(1 - 33 / 60d, chip.Fraction!.Value, 2);
        Assert.Equal(33 / 60d, HudChipRow.GaugeShare(
            new HudChipEntry(HudChipFamily.Buff, chip))!.Value, 2);
    }

    /// <summary>Inside the last server tick it takes the warning tint — and keeps counting,
    /// because unlike a spawn there is no "it happened" moment to flip to.</summary>
    [Fact]
    public void ABuffInsideItsLastServerTickIsDueButStillCounting()
    {
        var chip = Assert.Single(HudChipRow.BuffChips(Buffed(), T0.AddSeconds(3779), 60));

        Assert.True(chip.IsDue);
        Assert.Equal("0:04 est", HudChipRow.FaceText(new HudChipEntry(HudChipFamily.Buff, chip)));
    }

    /// <summary>A buff chip is not dismissible: it clears itself when the buff fades or is
    /// recast, exactly as a mez chip clears off the log. The tooltip only offers gestures a
    /// chicklet actually has, so this is also what stops it advertising one it hasn't.
    /// </summary>
    [Fact]
    public void ABuffChipIsNotDismissible()
        => Assert.Null(Assert.Single(HudChipRow.BuffChips(Buffed(), T0.AddSeconds(3750), 60)).OnDismiss);

    /// <summary>The ten-second floor is the Buffs card's, and it lives in one place so the
    /// card and the chip cannot disagree about when a buff has become urgent.</summary>
    [Theory]
    [InlineData(0d, 10d)]
    [InlineData(9d, 10d)]
    [InlineData(60d, 60d)]
    [InlineData(3600d, 3600d)]
    public void TheWarningWindowKeepsTheCardsTenSecondFloor(double setting, double expected)
        => Assert.Equal(expected, HudChipRow.BuffWarnWindow(setting));

    // ---- Build: the whole row for one tick, lifted out of MainWindow in SA-3 ----
    //
    // None of this was assertable before the lift. The four gates lived in the window, where
    // the WPF layer has no unit tests, so "focus-hide takes the row" and "the Camps rule is
    // per family" could only ever be checked by launching the app.

    /// <summary>Every family the fixture can produce, all four on one row, in order.</summary>
    private static List<HudChipEntry> BuildAll(bool hiddenForFocus = false, bool worldOnCamps = false,
        Action<AppSettings>? profile = null)
    {
        var settings = new AppSettings { TrackSpawns = true, MezChipsEnabled = true, BuffWarnSeconds = 7200 };
        profile?.Invoke(settings);
        var catalog = new SpawnCatalog
        {
            Zones =
            [
                new SpawnZone
                {
                    Zone = "Lower Guk", LogZoneName = "The Ruins of Old Guk",
                    Named = [new SpawnEntry { Name = "a froglok ghoul lord", RespawnSeconds = 1620 }],
                },
            ],
        };
        var overrides = new SpawnOverrides();
        var timers = new SpawnTimers(catalog, overrides) { Server = "test" };
        timers.Apply(new ZoneEvent(T0, "The Ruins of Old Guk"));
        timers.Apply(new KillEvent(T0, "froglok ghoul lord", "You"));
        var spawns = new SpawnsViewModel(catalog, overrides, timers);

        var fires = new WatchFireLedger();
        fires.Record("r1", "Rares", "Fungi Tunic", T0);

        return HudChipRow.Build(settings, hiddenForFocus, worldOnCamps, spawns,
            Mezzed("a skeleton"), new SlowTracker(), fires, Buffed(), T0.AddSeconds(5));
    }

    /// <summary>All four families reach the row through one call, in the default order.
    /// PREDICTION: mez, spawn, watch-fire, buff — one chicklet each.</summary>
    [Fact]
    public void BuildPutsEveryLiveFamilyOnTheRow()
        => Assert.Equal(
            [HudChipFamily.Mez, HudChipFamily.Spawn, HudChipFamily.WatchFire, HudChipFamily.Buff],
            BuildAll().Select(e => e.Family));

    /// <summary>Focus-hide takes the WHOLE row, every family with it — a chip row over
    /// someone's browser is the thing focus-hide exists to prevent, and a family that
    /// forgot to ask would be the one left floating there.</summary>
    [Fact]
    public void FocusHideTakesEveryFamilyOffTheRow()
        => Assert.Empty(BuildAll(hiddenForFocus: true));

    /// <summary>The Bevel-signed Camps hide-rule is PER FAMILY: the spawn chips leave because
    /// the same timers are on screen in the World window, and nothing else moves.</summary>
    [Fact]
    public void TheCampsRuleTakesOnlyTheSpawnFamily()
    {
        var row = BuildAll(worldOnCamps: true);

        Assert.Equal(0, HudChipRow.CountOf(row, HudChipFamily.Spawn));
        Assert.Equal(1, HudChipRow.CountOf(row, HudChipFamily.Mez));
        Assert.Equal(1, HudChipRow.CountOf(row, HudChipFamily.WatchFire));
        Assert.Equal(1, HudChipRow.CountOf(row, HudChipFamily.Buff));
    }

    // ---- SA-4: PLACE and MUTE ----
    //
    // Two settings, two verbs, and the reconciliation between them is the only place this
    // could go wrong quietly: an order that could also REMOVE a family would be a mute with
    // no switch naming it, which is trap 20's shape and #219's mechanism.

    private static AppSettings Profile(string[]? order = null, string[]? muted = null)
    {
        var settings = new AppSettings();
        if (order is not null) settings.HudChipOrder = [.. order];
        if (muted is not null) settings.MutedChipFamilies = [.. muted];
        return settings;
    }

    // -- Place --

    /// <summary>The shipped default IS the signed order, read through the resolver rather
    /// than asserted against the constant: a default that disagreed with `DefaultOrder`
    /// would be a fresh profile whose row was not the one the sign describes.</summary>
    [Fact]
    public void AFreshProfileResolvesToTheSignedDefaultOrder()
        => Assert.Equal(HudChipRow.DefaultOrder, HudChipRow.ResolveOrder(new AppSettings()));

    [Fact]
    public void TheStoredOrderIsWhatThePlayerGets()
        => Assert.Equal(
            [HudChipFamily.Buff, HudChipFamily.WatchFire, HudChipFamily.Spawn, HudChipFamily.Mez],
            HudChipRow.ResolveOrder(Profile(order: ["Buff", "WatchFire", "Spawn", "Mez"])));

    /// <summary>**A family the setting omits is APPENDED, never dropped.** `Merge` drops a
    /// family missing from the order it is handed, so an unrepaired omission would be a
    /// permanent invisible mute — a hand-edited file, or a profile written before a family
    /// existed, losing chips forever with nothing naming the loss. Mute is the only thing
    /// that removes a family, and it has its own key.</summary>
    [Fact]
    public void AFamilyMissingFromTheStoredOrderIsAppendedNotDropped()
    {
        var order = HudChipRow.ResolveOrder(Profile(order: ["Buff", "Mez"]));

        Assert.Equal(
            [HudChipFamily.Buff, HudChipFamily.Mez, HudChipFamily.Spawn, HudChipFamily.WatchFire],
            order);
        Assert.Equal(Enum.GetValues<HudChipFamily>().Length, order.Count);
    }

    /// <summary>An empty list is the same case, and it is the one a hand-edited profile
    /// reaches most easily: every family, in the default order, rather than a blank row.
    /// </summary>
    [Fact]
    public void AnEmptyStoredOrderFallsBackToTheDefaultRatherThanToAnEmptyRow()
        => Assert.Equal(HudChipRow.DefaultOrder, HudChipRow.ResolveOrder(Profile(order: [])));

    /// <summary>A name no family answers to is ignored, and a duplicate collapses to its
    /// first appearance — a typed-in file cannot make one family render twice.</summary>
    [Fact]
    public void UnknownNamesAreIgnoredAndDuplicatesCollapse()
        => Assert.Equal(
            [HudChipFamily.Spawn, HudChipFamily.Mez, HudChipFamily.WatchFire, HudChipFamily.Buff],
            HudChipRow.ResolveOrder(Profile(order: ["Spawn", "Pets", "spawn", "Mez", "SPAWN"])));

    /// <summary>The names are case-insensitive on the way in, because they are a file a
    /// player may type into, and exact on the way out (`SetOrder` writes `ToString`).
    /// </summary>
    [Fact]
    public void StoredNamesAreReadCaseInsensitively()
        => Assert.Equal([HudChipFamily.WatchFire, HudChipFamily.Mez],
            HudChipRow.ResolveOrder(Profile(order: ["watchfire", "MEZ"])).Take(2));

    /// <summary>Nudge moves one family one place, and the ends are NO-OPS rather than
    /// wrap-arounds: a ◀ on the leftmost chicklet that sent it to the far right would be a
    /// gesture nobody asked for, on a row whose whole point is that it stops surprising you.
    /// </summary>
    [Fact]
    public void NudgeMovesOneStepAndStopsAtTheEnds()
    {
        var order = HudChipRow.DefaultOrder;   // Mez, Spawn, WatchFire, Buff

        Assert.Equal([HudChipFamily.Spawn, HudChipFamily.Mez, HudChipFamily.WatchFire, HudChipFamily.Buff],
            HudChipRow.Nudge(order, HudChipFamily.Mez, +1));
        Assert.Equal([HudChipFamily.Mez, HudChipFamily.WatchFire, HudChipFamily.Spawn, HudChipFamily.Buff],
            HudChipRow.Nudge(order, HudChipFamily.WatchFire, -1));
        Assert.Equal(order, HudChipRow.Nudge(order, HudChipFamily.Mez, -1));
        Assert.Equal(order, HudChipRow.Nudge(order, HudChipFamily.Buff, +1));
    }

    /// <summary>Nudging is a WRITE, and the write and the read are the same PR — the
    /// `DeadSettingTests` posture. A round trip through the profile is what proves the pair,
    /// because three player-facing bugs came from data that survived a move and a write path
    /// that did not (#204, #210, #212).</summary>
    [Fact]
    public void APlaceEditRoundTripsThroughTheProfile()
    {
        var settings = new AppSettings();

        HudChipRow.SetOrder(settings,
            HudChipRow.Nudge(HudChipRow.ResolveOrder(settings), HudChipFamily.Buff, -1));

        Assert.Equal(["Mez", "Spawn", "Buff", "WatchFire"], settings.HudChipOrder);
        Assert.Equal(
            [HudChipFamily.Mez, HudChipFamily.Spawn, HudChipFamily.Buff, HudChipFamily.WatchFire],
            HudChipRow.ResolveOrder(settings));
    }

    /// <summary>The order reaches the ROW, which is the half none of the above proves: the
    /// setting could resolve perfectly and never be handed to `Merge`.</summary>
    [Fact]
    public void TheStoredOrderReachesTheBuiltRow()
        => Assert.Equal(
            [HudChipFamily.Buff, HudChipFamily.Spawn, HudChipFamily.WatchFire, HudChipFamily.Mez],
            BuildAll(profile: s => s.HudChipOrder = ["Buff", "Spawn", "WatchFire", "Mez"])
                .Select(e => e.Family));

    // -- Mute --

    [Fact]
    public void NothingIsMutedOnAFreshProfile()
    {
        foreach (var family in Enum.GetValues<HudChipFamily>())
            Assert.False(HudChipRow.IsMuted(new AppSettings(), family));
    }

    /// <summary>A muted family is off the row and NOTHING ELSE MOVES. This is the assertion
    /// the sibling-not-repurposing ruling is made of: mute is per family, and a mute that
    /// took the row down with it would be the switch doing a different job.</summary>
    [Fact]
    public void AMutedFamilyLeavesTheRowAndTheOthersStay()
    {
        var row = BuildAll(profile: s => s.MutedChipFamilies = ["Buff"]);

        Assert.Equal(0, HudChipRow.CountOf(row, HudChipFamily.Buff));
        Assert.Equal(1, HudChipRow.CountOf(row, HudChipFamily.Mez));
        Assert.Equal(1, HudChipRow.CountOf(row, HudChipFamily.Spawn));
        Assert.Equal(1, HudChipRow.CountOf(row, HudChipFamily.WatchFire));
    }

    /// <summary>Mute every family and the row is empty — which is the state the host reads to
    /// decide the companion window is not on screen at all. A player who mutes everything has
    /// asked for no row, not for an empty one hovering under the widget.</summary>
    [Fact]
    public void MutingEveryFamilyEmptiesTheRow()
        => Assert.Empty(BuildAll(profile: s =>
            s.MutedChipFamilies = [.. Enum.GetValues<HudChipFamily>().Select(f => f.ToString())]));

    /// <summary>Muting does not REORDER. A family keeps its place while it is muted, so
    /// unmuting puts it back where the player left it rather than at the end of the row.
    /// </summary>
    [Fact]
    public void AMutedFamilyKeepsItsPlaceForWhenItComesBack()
    {
        var settings = Profile(order: ["Buff", "Mez", "Spawn", "WatchFire"], muted: ["Mez"]);

        Assert.Equal([HudChipFamily.Buff, HudChipFamily.Spawn, HudChipFamily.WatchFire],
            HudChipRow.VisibleOrder(settings));

        HudChipRow.SetMuted(settings, HudChipFamily.Mez, false);
        Assert.Equal([HudChipFamily.Buff, HudChipFamily.Mez, HudChipFamily.Spawn, HudChipFamily.WatchFire],
            HudChipRow.VisibleOrder(settings));
    }

    /// <summary>The mute WRITER, round-tripped like the order's, and asserted for the
    /// idempotence a toggle needs: muting twice must not put two copies in the file, or the
    /// list grows every click and unmute has to remove them all.</summary>
    [Fact]
    public void AMuteEditRoundTripsThroughTheProfileAndDoesNotAccumulate()
    {
        var settings = new AppSettings();

        HudChipRow.SetMuted(settings, HudChipFamily.Spawn, true);
        HudChipRow.SetMuted(settings, HudChipFamily.Spawn, true);
        Assert.Equal(["Spawn"], settings.MutedChipFamilies);
        Assert.True(HudChipRow.IsMuted(settings, HudChipFamily.Spawn));

        HudChipRow.SetMuted(settings, HudChipFamily.Spawn, false);
        Assert.Empty(settings.MutedChipFamilies);
        Assert.False(HudChipRow.IsMuted(settings, HudChipFamily.Spawn));
    }

    /// <summary>Unmuting clears a hand-typed name whatever its case, so a file that says
    /// "spawn" is not a mute the button cannot lift.</summary>
    [Fact]
    public void UnmutingClearsANameWhateverItsCase()
    {
        var settings = Profile(muted: ["spawn"]);
        Assert.True(HudChipRow.IsMuted(settings, HudChipFamily.Spawn));

        HudChipRow.SetMuted(settings, HudChipFamily.Spawn, false);
        Assert.False(HudChipRow.IsMuted(settings, HudChipFamily.Spawn));
    }

    /// <summary>Mute is presence, and it is the ONLY thing that subtracts a family — the
    /// order never does. Asserted together because the two settings are one row's worth of
    /// state and the failure worth catching is them disagreeing.</summary>
    [Fact]
    public void OrderAndMuteAreTheOnlyTwoAnswersAndMuteIsTheOneThatSubtracts()
    {
        var settings = Profile(order: ["Spawn", "Mez"], muted: ["WatchFire"]);

        Assert.Equal(
            [HudChipFamily.Spawn, HudChipFamily.Mez, HudChipFamily.WatchFire, HudChipFamily.Buff],
            HudChipRow.ResolveOrder(settings));
        Assert.Equal([HudChipFamily.Spawn, HudChipFamily.Mez, HudChipFamily.Buff],
            HudChipRow.VisibleOrder(settings));
    }

    // -- What the edit chicklets say --

    /// <summary>Every family has a name and an emblem, and no two share either. "Mez & slow"
    /// names BOTH halves of the fight family: a mute button labelled "Mez" that also silences
    /// slow chips is a tick box that lies.</summary>
    [Fact]
    public void EveryFamilyHasItsOwnLabelAndEmblem()
    {
        var families = Enum.GetValues<HudChipFamily>();

        Assert.Equal(families.Length, families.Select(HudChipRow.Label).Distinct().Count());
        Assert.Equal(families.Length, families.Select(HudChipRow.Emblem).Distinct().Count());
        Assert.Contains("slow", HudChipRow.Label(HudChipFamily.Mez), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The emblems are `IconPaths` NAMES, and every one of them resolves — a name
    /// with no vector behind it draws nothing at all, which photographs as a chicklet that
    /// happens to have no icon (#148/#166's failure with the box removed).</summary>
    [Fact]
    public void EveryEmblemIsARealVector()
    {
        foreach (var family in Enum.GetValues<HudChipFamily>())
            Assert.True(IconPaths.All.ContainsKey(HudChipRow.Emblem(family)),
                $"{family}'s emblem '{HudChipRow.Emblem(family)}' is not in IconPaths.");
    }

    /// <summary>The dump token: comma-separated, never a space (the dump is space-separated
    /// key=value, so a space would silently become a second key), and "-" for nothing rather
    /// than "" (a key with an empty value is a key no wait can be written against).</summary>
    [Fact]
    public void TheDumpTokenIsSpaceFreeAndSaysSomethingWhenEmpty()
    {
        Assert.Equal("Mez,Buff", HudChipRow.OrderKey([HudChipFamily.Mez, HudChipFamily.Buff]));
        Assert.Equal("-", HudChipRow.OrderKey([]));
        Assert.DoesNotContain(' ', HudChipRow.OrderKey(HudChipRow.DefaultOrder));
    }
}
