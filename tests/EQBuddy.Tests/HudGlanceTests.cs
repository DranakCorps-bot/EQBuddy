using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The collapsed HUD's metric row (Surface A / SA-1, amended by DRA-72, **superseded on
/// membership by DRA-81's Founder LOCK**).
///
/// Three things are being pinned, and only one of them is the obvious one. WHICH SLOTS are
/// on the row is the interesting logic. The FIXED SHAPE of every string is the trap-12
/// guard, and it is the half that a later "just add a decimal" would break silently — a
/// readout whose width changes resizes an always-on-top window over a fullscreen game,
/// which is what cost #173 its keyboard. And the ROW ITSELF — order, keys, reserved widths
/// — is what the view draws without knowing what any slot means, so a rule that used to
/// live in a branch of `HudBarView` (which no test project can reach, docs/TestPlan.md §5)
/// is asserted here instead.
///
/// **What DRA-81 changed is the first of those, and it made it smaller.** Membership used
/// to be a rule: SA-1 declared DPS and the XP rate always-on and deleted their switches,
/// DRA-72 then gave HPS a dominance window, a 30-second weigh-in and a hysteresis. Now it
/// is <see cref="HudGlanceStars"/> — what the player ticked — and this file's membership
/// half is a truth table rather than a simulation. The tests that drove the old rule's
/// window (one heal mid-fight, a tie, the slot ageing out) went with it; there is no
/// timer-driven input left for them to be about.
///
/// **The DRA-72 property they protected is still asserted, because it is still true and is
/// the half the Founder actually needed**: HPS and the XP rate hold separate slots, so both
/// can be up at once and neither can take the other's place. It is proved here as a
/// membership fact rather than as a settling loop.
/// </summary>
public class HudGlanceTests
{
    private static HudGlanceInput Situation(
        double currentDps = 0, double sessionDps = 0, double hps = 0, double xpPerHour = 0,
        string? name = "Dranak", double petDps = 0) =>
        new(name, currentDps, sessionDps, hps, xpPerHour, petDps);

    /// <summary>The row a profile draws out of the box — DPS and the XP rate ticked, HPS
    /// not (<see cref="AppSettings.MiniStats"/>'s default).</summary>
    private static readonly HudGlanceStars Default = new(Dps: true, Hps: false, Xp: true, Pet: false);

    /// <summary>A healer's row: the same, plus the HPS box.</summary>
    private static readonly HudGlanceStars Healing = Default with { Hps = true };

    private static string Row(HudGlanceStars stars, in HudGlanceInput input) =>
        HudGlance.Read(stars, in input).RowKey;

    // --------------------------------------------------- the row a player sees ----

    /// <summary>The row a default profile draws: DPS then the XP rate. Byte for byte what
    /// SA-1 shipped and what DRA-72 left behind — the difference is that it is now a
    /// consequence of two ticked boxes rather than of a promotion nobody can undo.</summary>
    [Fact]
    public void ADefaultProfilesRowIsDpsThenTheXpRate()
    {
        var glance = HudGlance.Read(Default, Situation(xpPerHour: 12.5));

        Assert.Equal("dps,xp", glance.RowKey);
        Assert.Equal("Dranak", glance.Name);
        Assert.Equal("  12.5%/hr", glance.TextOf(HudGlance.XpKey));
        Assert.Null(glance.TextOf(HudGlance.HpsKey));
    }

    /// <summary>
    /// **THE FOUNDER'S SMOKE, in one assertion**: HPS is ticked, so HPS is on the row.
    ///
    /// No healing has happened in this situation and none needs to — that is the whole
    /// point. Under DRA-72 this row drew nothing for HPS until healing had out-weighed
    /// damage across the last half-minute, so a healer who ticked the box (or believed they
    /// had) got a bar that disagreed with them, and there was no box to check because SA-1
    /// had deleted it. A ★ that shows the number is smaller than any window rule and cannot
    /// be wrong about what the player wants.
    /// </summary>
    [Fact]
    public void TickingHpsPutsItOnTheRowWithNoHealingRequired()
    {
        var glance = HudGlance.Read(Healing, Situation(hps: 0, xpPerHour: 12.5));

        Assert.Equal("dps,hps,xp", glance.RowKey);
        // Reading zero is a READING — a healer between pulls — and it is why the slot must
        // not be gated on evidence. A row that appeared only once the number was interesting
        // is a row that is missing exactly when somebody goes looking for it.
        Assert.Equal("     0 hps", glance.TextOf(HudGlance.HpsKey));
    }

    /// <summary>The other direction, and it is the one SA-1 made impossible: unticking HPS
    /// takes the slot away even while healing is pouring in. "I do not want this number" is
    /// an answer the app has to accept.</summary>
    [Fact]
    public void UntickingHpsTakesItOffEvenWhileHealing()
    {
        var glance = HudGlance.Read(Default, Situation(hps: 900, xpPerHour: 12.5));

        Assert.Equal("dps,xp", glance.RowKey);
        Assert.False(glance.Has(HudGlance.HpsKey));
        Assert.Null(glance.TextOf(HudGlance.HpsKey));
    }

    /// <summary>
    /// **THE DRA-72 PROPERTY, KEPT**: HPS and the XP rate are separate slots, so both are up
    /// at once.
    ///
    /// The bug it was written against was a shared slot whose identity alternated on the
    /// one-second timer between "13 hps" and "167.5%/hr". DRA-72 fixed it with a hysteresis;
    /// DRA-81 keeps the fix and drops the hysteresis, because the slots being separate is
    /// what made "both at once" possible and the window was only ever deciding WHEN. The
    /// assertion is on the ROW rather than on a "which is it" mode for the original reason:
    /// a test that asked "is the third slot HPS" cannot fail on the flashing bar, since both
    /// answers drew correctly.
    /// </summary>
    [Fact]
    public void WhenHealingIsOnTheRowTheXpRateIsStillThere()
    {
        var glance = HudGlance.Read(Healing, Situation(hps: 141, xpPerHour: 167.5));

        Assert.Equal("dps,hps,xp", glance.RowKey);
        Assert.Equal("   141 hps", glance.TextOf(HudGlance.HpsKey));
        Assert.Equal(" 167.5%/hr", glance.TextOf(HudGlance.XpKey));
        Assert.Equal(HudGlance.HealingIcon,
            glance.Slots.Single(slot => slot.Key == HudGlance.HpsKey).Icon);
        Assert.Equal(HudGlance.ExperienceIcon,
            glance.Slots.Single(slot => slot.Key == HudGlance.XpKey).Icon);
    }

    /// <summary>
    /// **THE FOUNDER'S VIDEO, and why it can no longer happen.**
    ///
    /// The character was healing enough to out-weigh damage across the ~30 s window AND
    /// swinging inside the ~5 s resume window, so the old rule's "healing has dominated,
    /// take the slot" and its "damage is back, give it up" were both true and the shared
    /// slot alternated forever. DRA-72 answered it by deleting one clause; DRA-81 answers it
    /// by construction — the row is a function of the profile alone, so a hundred reads of
    /// one unchanged situation are a hundred identical rows and there is no input left that
    /// could make it flicker.
    ///
    /// Read repeatedly rather than once, because the bug was a bar that was right half the
    /// time: an assertion that looked at the first answer passed on the code that flashed.
    /// </summary>
    [Fact]
    public void TheVideosSituationShowsBothNumbersAndHoldsStillOnEveryTick()
    {
        var input = Situation(currentDps: 18, hps: 13, xpPerHour: 167.5);

        var rows = Enumerable.Range(0, 6)
            .Select(_ => HudGlance.Read(Healing, input).RowKey)
            .ToList();

        Assert.Equal(["dps,hps,xp", "dps,hps,xp", "dps,hps,xp", "dps,hps,xp",
            "dps,hps,xp", "dps,hps,xp"], rows);
    }

    // ------------------------------------------------ membership is the ★, only ----

    /// <summary>
    /// **The truth table, so no ★ can be quietly ignored.** Each of the three has to be able
    /// to be the ONLY thing on the row and the only thing missing from it — a membership
    /// rule that reads two of three boxes passes every test that only ever ticks all of
    /// them.
    /// </summary>
    [Theory]
    [InlineData(true, true, true, "dps,hps,xp")]
    [InlineData(true, false, true, "dps,xp")]
    [InlineData(false, true, true, "hps,xp")]
    [InlineData(true, true, false, "dps,hps")]
    [InlineData(true, false, false, "dps")]
    [InlineData(false, true, false, "hps")]
    [InlineData(false, false, true, "xp")]
    public void EveryStarDecidesItsOwnSlotAndNobodyElses(bool dps, bool hps, bool xp, string row) =>
        Assert.Equal(row, Row(new HudGlanceStars(dps, hps, xp, Pet: false),
            Situation(currentDps: 412, hps: 141, xpPerHour: 12.5)));

    /// <summary>Every box clear leaves the NAME and nothing else, and the row token says so
    /// as "-" rather than as an empty string — the dump is space-separated key=value, so an
    /// empty value cannot be waited on (<see cref="MiniBarPresentation.OrderKey"/>).
    ///
    /// It is a state a player can ask for on purpose: someone who wants the bar to be a name
    /// and their own chips is allowed to have that, and it is the first time since SA-1 they
    /// could.</summary>
    [Fact]
    public void EveryStarClearedLeavesTheNameAloneOnTheRow()
    {
        var glance = HudGlance.Read(default, Situation(currentDps: 412, hps: 141, xpPerHour: 12.5));

        Assert.Empty(glance.Slots);
        Assert.Equal("-", glance.RowKey);
        Assert.Equal("Dranak", glance.Name);
    }

    /// <summary>The ORDER is the class's and never the order the boxes were ticked in —
    /// `MiniBarPresentation.Order`'s rule one row up, and for its reason: a row that
    /// reshuffles as you toggle is a row you have to re-read every time. Asserted by reading
    /// the same membership out of two differently-ordered profiles.</summary>
    [Fact]
    public void TheRowsOrderIsFixedAndNotTheOrderTheStarsWereSetIn()
    {
        var input = Situation(currentDps: 412, hps: 141, xpPerHour: 12.5, petDps: 88);
        var all = new HudGlanceStars(Dps: true, Hps: true, Xp: true, Pet: true);

        Assert.Equal("dps,pet,hps,xp", Row(all, input));

        var backwards = new AppSettings { MiniStats = ["xp", "hps", "dps"], HudGlancePet = true };
        Assert.Equal("dps,pet,hps,xp", Row(HudGlanceStars.From(backwards), input));
    }

    // ------------------------------------------------ the ★s come off the profile ----

    /// <summary>
    /// <see cref="HudGlanceStars.From"/> is the ONE place a ★ becomes a slot (trap 4), so
    /// the widget, a test and any later host cannot disagree about what the profile says.
    ///
    /// The negative is the half worth having: a profile with the keys ABSENT answers false
    /// for each, which is the state every player's file was in between SA-1 and the restore
    /// pass — and reading it as anything but "off" is how an unswitchable row comes back.
    /// </summary>
    [Fact]
    public void TheStarsAreReadOffTheProfileAndAnAbsentKeyIsOff()
    {
        var settings = new AppSettings { MiniStats = ["kills", "dps", "hps", "xp"], HudGlancePet = true };

        Assert.Equal(new HudGlanceStars(true, true, true, true), HudGlanceStars.From(settings));
        Assert.Equal(default, HudGlanceStars.From(new AppSettings { MiniStats = ["kills"] }));
        // "pet" is its own verb and its own setting — a ★ for pet is about the CELL, and
        // whether the slot is on this row is the drag's answer (SIGNED #422).
        Assert.False(HudGlanceStars.From(new AppSettings { MiniStats = ["pet"] }).Pet);
    }

    /// <summary>A fresh profile draws the row every profile has drawn since SA-1 — DPS and
    /// the XP rate — so nothing about a new install changed when the switches came back.
    /// **And HPS is deliberately NOT in it**: a permanent "0 hps" is not what to hand
    /// somebody who has never cast a heal, and the box is right there.</summary>
    [Fact]
    public void AFreshProfilesDefaultRowIsDpsAndTheXpRate()
    {
        var stars = HudGlanceStars.From(new AppSettings());

        Assert.Equal(new HudGlanceStars(Dps: true, Hps: false, Xp: true, Pet: false), stars);
        Assert.Equal("dps,xp", Row(stars, Situation(xpPerHour: 12.5)));
    }

    // ------------------------------------------------------- the fixed shape ----

    /// <summary>Trap 12: the widget is SizeToContent, so a metric that changes width IS a
    /// window resize — on a timer, forever. Every value any slot can hold formats to the
    /// same number of characters.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.4)]
    [InlineData(9.9)]
    [InlineData(87)]
    [InlineData(412.6)]
    [InlineData(9999)]
    [InlineData(123456)]
    // Past the clamp: the promise holds because the value is clamped, not because the
    // number happens to be short.
    [InlineData(99999999)]
    public void EveryDpsStringIsTheSameLength(double dps)
    {
        Assert.Equal(HudGlance.MetricFixedLength,
            HudGlance.DpsText(Situation(currentDps: dps)).Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.4)]
    [InlineData(12.5)]
    [InlineData(140.75)]
    [InlineData(167.5)]
    [InlineData(9999.9)]
    [InlineData(50000)]
    public void EveryExperienceStringIsTheSameLength(double xpPerHour)
    {
        Assert.Equal(HudGlance.MetricFixedLength,
            HudGlance.ExperienceText(Situation(xpPerHour: xpPerHour)).Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7.5)]
    [InlineData(13)]
    [InlineData(141)]
    [InlineData(98765)]
    [InlineData(1000000)]
    public void EveryHealingStringIsTheSameLength(double hps)
    {
        Assert.Equal(HudGlance.MetricFixedLength, HudGlance.HealingText(Situation(hps: hps)).Length);
    }

    /// <summary>The FOUR metrics are the same length as EACH OTHER, which is the assertion a
    /// per-metric invariant misses: one shape for every slot is what makes the row
    /// measurable, and it is what lets a slot ARRIVE at a known cost instead of at whatever
    /// its own string happens to want.</summary>
    [Fact]
    public void EverySlotsStringIsTheSameLengthAsEveryOthers()
    {
        var input = Situation(currentDps: 412, hps: 141, xpPerHour: 12.5, petDps: 88);

        var slots = HudGlance.Read(Healing with { Pet = true }, input).Slots;

        // The count first: `Assert.All` over a row that had quietly lost a slot would pass
        // while asserting nothing about it, which is the shape of a vacuous guard.
        Assert.Equal(4, slots.Count);
        Assert.All(slots,
            slot => Assert.Equal(HudGlance.MetricFixedLength, slot.Text.Length));
    }

    /// <summary>Between pulls the DPS slot falls back to the session rate — the same rule
    /// the bar's own dps cell always had, so promoting the number did not redefine it.</summary>
    [Fact]
    public void TheDpsSlotFallsBackToTheSessionRateBetweenPulls()
    {
        Assert.Equal("   412 dps", HudGlance.DpsText(Situation(currentDps: 0, sessionDps: 412)));
        Assert.Equal("   500 dps", HudGlance.DpsText(Situation(currentDps: 500, sessionDps: 412)));
    }

    // ----------------------------------------------------- the reserved widths ----

    /// <summary>
    /// Each slot carries the reserved width for ITS metric, and the view pins the string to
    /// it — the trap-12 guard's other half, and the half a diff cannot see.
    ///
    /// **A per-metric width only became legal with DRA-72.** SA-1 gave every metric ONE
    /// width because the third slot changed its string's identity on a timer, so a per-string
    /// width there would have been the resize the rule forbids. No slot changes identity now,
    /// so these are constants of the row — and since DRA-81 the only thing that can add one
    /// is a click the player just made.
    /// </summary>
    [Fact]
    public void EachSlotCarriesItsOwnMetricsReservedWidth()
    {
        var glance = HudGlance.Read(Healing with { Pet = true },
            Situation(hps: 141, xpPerHour: 12.5, petDps: 88));

        Assert.Equal(HudGlance.MetricReservedWidth,
            glance.Slots.Single(s => s.Key == HudGlance.DpsKey).ReservedWidth);
        Assert.Equal(HudGlance.MetricReservedWidth,
            glance.Slots.Single(s => s.Key == MiniBarPresentation.PetKey).ReservedWidth);
        Assert.Equal(HudGlance.MetricReservedWidth,
            glance.Slots.Single(s => s.Key == HudGlance.HpsKey).ReservedWidth);
        Assert.Equal(HudGlance.ExperienceReservedWidth,
            glance.Slots.Single(s => s.Key == HudGlance.XpKey).ReservedWidth);
        Assert.All(glance.Slots, slot => Assert.True(slot.ReservedWidth > 0,
            "a slot with no reserved width is a slot whose string decides the window's size"));
    }

    /// <summary>The XP slot's box is WIDER than a rate slot's, at the same character count.
    /// "9999.9%/hr" spends two characters on '%' and '/', which out-measure the leading
    /// spaces "  1234 dps" pads with — so at one shared width a four-digit XP rate trimmed
    /// to an ellipsis, which is the Founder's "the XP string eats the gap". Not a measured
    /// number (nothing here can measure text without a window); headroom, asserted as an
    /// ordering so a later tidy-up cannot quietly equalise them again.</summary>
    [Fact]
    public void TheXpSlotsBoxIsWiderThanARateSlots() =>
        Assert.True(HudGlance.ExperienceReservedWidth > HudGlance.MetricReservedWidth,
            "the XP rate's ten characters carry '%' and '/' where a rate's carry spaces");

    // --------------------------------------------------------------- the name ----

    [Fact]
    public void AKnownCharacterNameIsShownAsItIs() =>
        Assert.Equal("Dranak", HudGlance.NameText("Dranak"));

    /// <summary>An empty name is a normal state — the log has not named anybody yet — and
    /// it renders as an EMPTY slot at the reserved width rather than as a placeholder
    /// sentence or a collapsing hole. Both alternatives move the numbers beside it.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnUnknownCharacterNameIsEmptyRatherThanAPlaceholder(string? name)
    {
        Assert.Equal("", HudGlance.NameText(name));
        Assert.True(HudGlance.NameReservedWidth > 0,
            "the empty slot has to keep its width, or the metrics beside it move");
    }

    // -------------------------------------------------- the keys are the bar's ----

    /// <summary>
    /// **Every key this row can produce resolves to its own expansion target** — the
    /// must-list half that a forbid-scan cannot see (trap 34), and the reason `HudBarView`
    /// no longer carries a hand-written "this slot is HPS so the target is Hps" switch.
    ///
    /// One table answers for the chip, the peek, the title, the icon and the ⧉ (trap 4),
    /// exactly as it has for the tray cells since OE-9. The negative matters as much as the
    /// list: four DISTINCT targets, so a key that quietly resolved to its neighbour's panel
    /// would fail here rather than open the wrong window.
    /// </summary>
    [Fact]
    public void EverySlotKeyResolvesToItsOwnExpansionTarget()
    {
        var glance = HudGlance.Read(Healing with { Pet = true },
            Situation(hps: 141, xpPerHour: 12.5, petDps: 88));
        var targets = glance.Slots
            .Select(slot => (slot.Key, Target: HudExpand.TargetForKey(slot.Key)))
            .ToList();

        Assert.Equal(4, targets.Count);
        Assert.All(targets, row => Assert.NotNull(row.Target));
        Assert.Equal(4, targets.Select(row => row.Target).Distinct().Count());
        Assert.Equal(HudExpandTarget.Dps, HudExpand.TargetForKey(HudGlance.DpsKey));
        Assert.Equal(HudExpandTarget.Hps, HudExpand.TargetForKey(HudGlance.HpsKey));
        Assert.Equal(HudExpandTarget.Pet, HudExpand.TargetForKey(MiniBarPresentation.PetKey));
        // The xp slot's ⧉ is the Progress WINDOW — the signed 2026-08-25 fold, not a float.
        Assert.Equal(HudExpandTarget.Progress, HudExpand.TargetForKey(HudGlance.XpKey));
    }

    /// <summary>The row token is the dump's, and it is spelled the way every other key list
    /// on this bar is spelled (<see cref="MiniBarPresentation.OrderKey"/>) — the dump is
    /// space-separated <c>key=value</c>, so a value with a space in it would silently become
    /// two keys.</summary>
    [Fact]
    public void TheRowTokenIsOneSpaceFreeWordInTheBarsOwnSpelling()
    {
        var row = HudGlance.Read(Healing with { Pet = true },
            Situation(hps: 141, petDps: 88)).RowKey;

        Assert.Equal("dps,pet,hps,xp", row);
        Assert.DoesNotContain(' ', row);
    }

    // -------------------------------------------------- straight off a snapshot ----

    /// <summary>The snapshot overload is what the widget actually calls. It exists so the
    /// mapping from session fields to glance inputs lives once — a second host wiring its
    /// own would be two producers of one decision (trap 33).</summary>
    [Fact]
    public void TheSnapshotOverloadFormatsTheSessionsOwnNumbers()
    {
        var snapshot = new StatsSnapshot { CurrentDps = 300, Hps = 141, XpPerHour = 12.5 };

        var glance = HudGlance.Read(Healing, snapshot, "Dranak");

        Assert.Equal("dps,hps,xp", glance.RowKey);
        Assert.Equal("Dranak", glance.Name);
        Assert.Equal("   300 dps", glance.TextOf(HudGlance.DpsKey));
        Assert.Equal("   141 hps", glance.TextOf(HudGlance.HpsKey));
        Assert.Equal("  12.5%/hr", glance.TextOf(HudGlance.XpKey));
    }

    /// <summary>…and the SAME snapshot under a default profile draws no HPS slot, so "the
    /// session healed" and "the row shows healing" cannot be read off one another. This is
    /// the pair that would have caught the Founder's smoke from the code's side: the number
    /// existed on the snapshot the whole time.</summary>
    [Fact]
    public void TheSameSnapshotWithoutTheStarDrawsNoHealingSlot()
    {
        var snapshot = new StatsSnapshot { CurrentDps = 300, Hps = 141, XpPerHour = 12.5 };

        var glance = HudGlance.Read(Default, snapshot, "");

        Assert.Equal("dps,xp", glance.RowKey);
        Assert.Equal("", glance.Name);
        Assert.False(glance.Has(HudGlance.HpsKey));
        Assert.Equal(141, snapshot.Hps);   // the number was there; the ★ was not
    }

    // ------------------------------------- the one inserted slot (SIGNED #422) ----

    /// <summary>Without the drag there is no pet slot at all — ABSENT rather than
    /// present-and-empty. Membership is the whole answer the view reads, so "not inserted"
    /// and "inserted but reading nothing" must not collapse into one value: the second is a
    /// real state (a session with no pet damage yet) and it still draws a slot.</summary>
    [Fact]
    public void WithoutTheSettingThereIsNoPetSlotAtAll()
    {
        var glance = HudGlance.Read(Default, Situation(xpPerHour: 12.5, petDps: 88));

        Assert.Equal("dps,xp", glance.RowKey);
        Assert.False(glance.Has(MiniBarPresentation.PetKey));
        Assert.Null(glance.TextOf(MiniBarPresentation.PetKey));
    }

    [Fact]
    public void InsertedTheRowReadsDpsThenPetThenTheXpRate()
    {
        var glance = HudGlance.Read(Default with { Pet = true },
            Situation(currentDps: 412, xpPerHour: 12.5, petDps: 88));

        Assert.Equal("dps,pet,xp", glance.RowKey);
        Assert.Equal("   412 dps", glance.TextOf(HudGlance.DpsKey));
        Assert.Equal("    88 dps", glance.TextOf(MiniBarPresentation.PetKey));
        Assert.Equal("  12.5%/hr", glance.TextOf(HudGlance.XpKey));
    }

    /// <summary>A pet that has done nothing yet is a normal state and draws a slot reading
    /// zero — the alternative is a row that changes width the moment the pet lands its first
    /// hit, which is the trap-12 resize this class exists to prevent.</summary>
    [Fact]
    public void AnInsertedPetWithNoDamageYetStillDrawsItsSlot() =>
        Assert.Equal("     0 dps",
            HudGlance.Read(Default with { Pet = true }, Situation())
                .TextOf(MiniBarPresentation.PetKey));

    /// <summary>Trap 12, for the inserted slot: every value it can hold formats to the same
    /// number of characters as the metrics beside it, so an insert changes the measured
    /// width ONCE — on the player's drop — and never again on the timer.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.4)]
    [InlineData(9.9)]
    [InlineData(87)]
    [InlineData(412.6)]
    [InlineData(9999)]
    [InlineData(123456)]
    [InlineData(99999999)]
    public void EveryPetStringIsTheSameLength(double petDps)
    {
        Assert.Equal(HudGlance.MetricFixedLength,
            HudGlance.PetText(Situation(petDps: petDps)).Length);
        // …and the same length as its NEIGHBOURS, which is the assertion a per-string
        // invariant misses.
        Assert.Equal(HudGlance.DpsText(Situation(currentDps: petDps)).Length,
            HudGlance.PetText(Situation(petDps: petDps)).Length);
    }

    /// <summary>
    /// **The insertion POINT does not move, which is what keeps #413 routed around rather
    /// than reopened.**
    ///
    /// SIGNED #422 put the pet slot between DPS and the metrics that follow it, and the
    /// reasoning it routed around was #413's: a fixed slot that changes identity mid-session
    /// must not be a drop target. Nothing on this row changes identity at all now, and this
    /// asserts the consequence that matters — ticking HPS lands the new slot to the RIGHT of
    /// the pet slot, so the gap the drag measures is the same gap either way.
    /// </summary>
    [Fact]
    public void TickingHealingLandsItAfterTheInsertedPetSlot()
    {
        var input = Situation(hps: 141, xpPerHour: 12.5, petDps: 88);

        Assert.Equal("dps,pet,xp", Row(Default with { Pet = true }, input));

        var glance = HudGlance.Read(Healing with { Pet = true }, input);

        Assert.Equal("dps,pet,hps,xp", glance.RowKey);
        // The two ends are fixed, which is what makes the middle safe to grow: DPS is
        // first and the XP rate is last, exactly as they have been since SA-1.
        Assert.Equal(HudGlance.DpsKey, glance.Slots[0].Key);
        Assert.Equal(HudGlance.XpKey, glance.Slots[^1].Key);
    }

    /// <summary>The pet slot's icon is its CELL's, not a new vector: a chip that changed
    /// shape when it changed rows would read as a different stat. It is also what tells two
    /// "N dps" readouts apart on one row.</summary>
    [Fact]
    public void ThePetSlotWearsTheSameVectorItsCellDoes()
    {
        Assert.Equal(HudGlance.PetIcon, MiniBarPresentation.Icons[MiniBarPresentation.PetKey]);
        Assert.NotEqual(HudGlance.DpsIcon, HudGlance.PetIcon);
        Assert.Contains(HudGlance.PetIcon, IconPaths.Names);
    }

    /// <summary>Every icon on the row is a real vector and no two slots wear the same one —
    /// four numbers on one bar, two of which read "N dps", so the vector is what tells them
    /// apart (#148, #166: a vector and never a glyph).</summary>
    [Fact]
    public void EverySlotWearsItsOwnRealVector()
    {
        var icons = HudGlance.Read(Healing with { Pet = true },
                Situation(hps: 141, petDps: 88))
            .Slots.Select(slot => slot.Icon).ToList();

        Assert.Equal(4, icons.Distinct().Count());
        Assert.All(icons, icon => Assert.Contains(icon, IconPaths.Names));
    }

    /// <summary>The snapshot overload takes the pet rate off the SNAPSHOT rather than
    /// recomputing it, which is the trap-4 half of §4: the cell down in the tray formats the
    /// same number, and a second expression here is how the two would drift.</summary>
    [Fact]
    public void TheSnapshotOverloadFormatsTheSnapshotsOwnPetRate()
    {
        var snapshot = new StatsSnapshot
        {
            CombatSeconds = 120,
            PetAbilities = [new SourceDamage("Pet (Gnoll Pup)", 40, 10_560)],
        };

        var glance = HudGlance.Read(Default with { Pet = true }, snapshot, "Dranak");

        Assert.Equal(88, snapshot.PetDps);
        Assert.Equal("    88 dps", glance.TextOf(MiniBarPresentation.PetKey));
        // …and the SAME snapshot with the setting off draws no slot, so the two facts cannot
        // be read off one another.
        Assert.False(HudGlance.Read(Default, snapshot, "Dranak")
            .Has(MiniBarPresentation.PetKey));
    }
}
