using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The collapsed HUD's always-on row (Surface A / SA-1, AMENDED by DRA-72).
///
/// Three things are being pinned, and only one of them is the obvious one. WHICH SLOTS are
/// on the row is the interesting logic. The FIXED SHAPE of every string is the trap-12
/// guard, and it is the half that a later "just add a decimal" would break silently — a
/// readout whose width changes resizes an always-on-top window over a fullscreen game,
/// which is what cost #173 its keyboard. And the ROW ITSELF — order, keys, reserved widths
/// — is what the view now draws without knowing what any slot means, so a rule that used
/// to live in a branch of `HudBarView` (which no test project can reach, docs/TestPlan.md
/// §5) is asserted here instead.
///
/// **DRA-72 is the swap becoming an addition**, and the file is organised so the situation
/// that produced the Founder's video has a test of its own: the old rule's ENTER test and
/// EXIT test were both true of one character, so the third slot alternated between HPS and
/// the XP rate about once a second. Each direction had a passing test; nothing drove them
/// together.
/// </summary>
public class HudGlanceTests
{
    private static HudGlanceInput Situation(
        double currentDps = 0, double sessionDps = 0, double hps = 0, double xpPerHour = 0,
        long recentDamage = 0, long recentHealing = 0, long damageSinceResume = 0,
        string? name = "Dranak", double petDps = 0, bool petInserted = false) =>
        new(name, currentDps, sessionDps, hps, xpPerHour,
            recentDamage, recentHealing, damageSinceResume, petDps, petInserted);

    private static string Row(HudGlanceState state, in HudGlanceInput input) =>
        HudGlance.Read(state, in input).RowKey;

    private static readonly HudGlanceState Healing = new(true);

    // --------------------------------------------------- the row a player sees ----

    /// <summary>The row every melee character sees, forever: DPS then the XP rate, with
    /// nothing conditional on it. Same two numbers SA-1 shipped, in the same order.</summary>
    [Fact]
    public void AMeleeCharactersRowIsDpsThenTheXpRate()
    {
        var glance = HudGlance.Read(HudGlanceState.Start, Situation(xpPerHour: 12.5));

        Assert.Equal("dps,xp", glance.RowKey);
        Assert.Equal("Dranak", glance.Name);
        Assert.Equal("  12.5%/hr", glance.TextOf(HudGlance.XpKey));
        Assert.Null(glance.TextOf(HudGlance.HpsKey));
        Assert.False(glance.State.Healing);
    }

    /// <summary>
    /// **THE WHOLE OF DRA-72, in one assertion**: when healing is on the row, the XP rate is
    /// still on it too. Both numbers, at once, each in its own slot.
    ///
    /// Before this, HPS and the XP rate shared one slot and the readout could only ever
    /// answer with one of them — which is why the assertion is on the ROW and not on a
    /// "which is it" mode. A test that asked "is the third slot HPS" cannot fail on the bug
    /// this fixes, because the answer was always yes-or-no and both answers drew correctly.
    /// </summary>
    [Fact]
    public void WhenHealingIsOnTheRowTheXpRateIsStillThere()
    {
        var glance = HudGlance.Read(HudGlanceState.Start,
            Situation(hps: 141, xpPerHour: 167.5, recentDamage: 200, recentHealing: 2400));

        Assert.Equal("dps,hps,xp", glance.RowKey);
        Assert.Equal("   141 hps", glance.TextOf(HudGlance.HpsKey));
        Assert.Equal(" 167.5%/hr", glance.TextOf(HudGlance.XpKey));
        Assert.Equal(HudGlance.HealingIcon,
            glance.Slots.Single(slot => slot.Key == HudGlance.HpsKey).Icon);
        Assert.Equal(HudGlance.ExperienceIcon,
            glance.Slots.Single(slot => slot.Key == HudGlance.XpKey).Icon);
    }

    /// <summary>
    /// **THE FOUNDER'S VIDEO, driven as a situation and then held.**
    ///
    /// The character was healing enough to out-weigh damage across the ~30 s window AND
    /// swinging inside the ~5 s resume window — so the old rule's "healing has dominated,
    /// take the slot" and its "damage is back, give it up" were both true, and the one slot
    /// they shared alternated between "13 hps" and "167.5%/hr" on the one-second timer,
    /// forever. Nothing was broken in either rule; they had never been asked at the same
    /// moment.
    ///
    /// Fed back into itself, because that is how a host uses this: the row has to be the
    /// same row on every tick, not the same row on alternate ones. An assertion that only
    /// looked at the first answer would pass on the code that flashes.
    /// </summary>
    [Fact]
    public void TheVideosSituationShowsBothNumbersAndHoldsStillOnEveryTick()
    {
        var input = Situation(currentDps: 18, hps: 13, xpPerHour: 167.5,
            recentDamage: 200, recentHealing: 2400, damageSinceResume: 60);

        var state = HudGlanceState.Start;
        var rows = new List<string>();
        for (var tick = 0; tick < 6; tick++)
        {
            var glance = HudGlance.Read(state, input);
            state = glance.State;
            rows.Add(glance.RowKey);
        }

        Assert.Equal(["dps,hps,xp", "dps,hps,xp", "dps,hps,xp", "dps,hps,xp",
            "dps,hps,xp", "dps,hps,xp"], rows);
    }

    /// <summary>The deleted clause, named and asserted: damage returning does not take the
    /// HPS slot away. It used to, instantly and by design — "collapse again the moment
    /// combat-as-damage returns" — because the XP rate had nowhere else to be drawn. It has
    /// somewhere else now.</summary>
    [Fact]
    public void OneSwingNoLongerTakesTheHealingSlotAway()
    {
        var swung = Situation(hps: 141, xpPerHour: 12.5,
            recentDamage: 40, recentHealing: 9000, damageSinceResume: 40);

        Assert.Equal("dps,hps,xp", Row(Healing, swung));
        Assert.True(HudGlance.HealingShown(shown: true, swung));
    }

    // ---------------------------------------------- when the slot arrives ----

    /// <summary>Arriving is deliberately slow, and that half of the hysteresis is
    /// UNCHANGED: healing has to have out-weighed damage across the whole ~30 s window. A
    /// damage dealer who lands one heal mid-pull gains no slot — and since a slot arriving
    /// widens an always-on-top window, that protection is now about the player's HUD as
    /// well as about their XP rate.</summary>
    [Fact]
    public void OneHealDuringAFightDoesNotPutHealingOnTheRow()
    {
        var glance = HudGlance.Read(HudGlanceState.Start,
            Situation(recentDamage: 9000, recentHealing: 300));

        Assert.Equal("dps,xp", glance.RowKey);
        Assert.False(glance.State.Healing);
    }

    [Fact]
    public void HealingThatOutweighsDamageOverTheWindowArrives()
    {
        var glance = HudGlance.Read(HudGlanceState.Start,
            Situation(hps: 141, recentDamage: 200, recentHealing: 2400));

        Assert.True(glance.State.Healing);
        Assert.Equal("dps,hps,xp", glance.RowKey);
    }

    [Fact]
    public void HealingWithNoDamageAtAllStillCounts()
    {
        // A healer between pulls: nothing to hit, everything to mend.
        Assert.Equal("dps,hps,xp",
            Row(HudGlanceState.Start, Situation(recentDamage: 0, recentHealing: 1200)));
    }

    /// <summary>Equal weight is not dominance. A tie leaves the row alone, because a slot
    /// arriving needs a reason.</summary>
    [Fact]
    public void EqualHealingAndDamageLeavesTheRowAlone() =>
        Assert.Equal("dps,xp",
            Row(HudGlanceState.Start, Situation(recentDamage: 1000, recentHealing: 1000)));

    /// <summary>Once it is there, healing that is REAL but no longer dominant keeps it —
    /// which is the "stays while you keep healing" half, and the reason a healer who starts
    /// swinging does not watch their own number disappear. It is also the arithmetic that
    /// makes an oscillation impossible: the stay test is weaker than the arrive test, so no
    /// input can satisfy one and fail the other in alternate directions.</summary>
    [Fact]
    public void OnceItIsThereHealingThatIsNoLongerDominantKeepsIt() =>
        Assert.Equal("dps,hps,xp",
            Row(Healing, Situation(recentDamage: 9000, recentHealing: 300)));

    // -------------------------------------------------- when it leaves ----

    /// <summary>A healer who simply stops: thirty seconds later the window holds no
    /// healing, and there is no longer anything for the slot to be about. UNCHANGED from
    /// SA-1 — and it is the one way the slot can leave, which is what makes the row's width
    /// a function of what the player is doing rather than of the timer.</summary>
    [Fact]
    public void TheSlotLeavesWhenTheHealingWindowEmpties()
    {
        var glance = HudGlance.Read(Healing, Situation());

        Assert.Equal("dps,xp", glance.RowKey);
        Assert.False(glance.State.Healing);
    }

    /// <summary>…and it leaves on an empty healing window even while damage is still
    /// pouring in, which is the same rule read from the other side.</summary>
    [Fact]
    public void ItLeavesOnAnEmptyHealingWindowWhateverDamageIsDoing() =>
        Assert.Equal("dps,xp",
            Row(Healing, Situation(recentDamage: 9000, damageSinceResume: 9000)));

    /// <summary>Feeding the answer back in is how a host uses this, so a settled state has
    /// to stay settled — in both memberships, because a row that flickers costs the same
    /// either way round.</summary>
    [Theory]
    [InlineData(100, 3000, "dps,hps,xp")]
    [InlineData(9000, 0, "dps,xp")]
    public void FedBackToItselfTheRowSettles(long recentDamage, long recentHealing, string row)
    {
        var input = Situation(hps: 141, xpPerHour: 12.5,
            recentDamage: recentDamage, recentHealing: recentHealing,
            damageSinceResume: recentDamage);

        var state = HudGlanceState.Start;
        for (var i = 0; i < 5; i++)
        {
            var glance = HudGlance.Read(state, input);
            state = glance.State;
            Assert.Equal(row, glance.RowKey);
        }
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
        var input = Situation(currentDps: 412, hps: 141, xpPerHour: 12.5,
            recentHealing: 2400, petDps: 88, petInserted: true);

        var slots = HudGlance.Read(Healing, input).Slots;

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
    /// so these are constants of the row.
    /// </summary>
    [Fact]
    public void EachSlotCarriesItsOwnMetricsReservedWidth()
    {
        var glance = HudGlance.Read(Healing,
            Situation(hps: 141, xpPerHour: 12.5, recentHealing: 2400, petDps: 88,
                petInserted: true));

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
        var glance = HudGlance.Read(Healing,
            Situation(hps: 141, xpPerHour: 12.5, recentHealing: 2400, petDps: 88,
                petInserted: true));
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
        var row = HudGlance.Read(Healing,
            Situation(hps: 141, recentHealing: 2400, petDps: 88, petInserted: true)).RowKey;

        Assert.Equal("dps,pet,hps,xp", row);
        Assert.DoesNotContain(' ', row);
    }

    // -------------------------------------------------- straight off a snapshot ----

    /// <summary>The snapshot overload is what the widget actually calls. It exists so the
    /// mapping from session fields to glance inputs lives once — a second host wiring its
    /// own would be two producers of one decision (trap 33).</summary>
    [Fact]
    public void TheSnapshotOverloadReadsTheSessionsOwnEffortSignal()
    {
        var snapshot = new StatsSnapshot
        {
            CurrentDps = 300,
            Hps = 141,
            XpPerHour = 12.5,
            Effort = new RecentEffort(TimeSpan.FromSeconds(30), 100, 4000,
                TimeSpan.FromSeconds(5), 0),
        };

        var glance = HudGlance.Read(HudGlanceState.Start, snapshot, "Dranak",
            petInserted: false);

        Assert.Equal("dps,hps,xp", glance.RowKey);
        Assert.Equal("Dranak", glance.Name);
        Assert.Equal("   300 dps", glance.TextOf(HudGlance.DpsKey));
        Assert.Equal("   141 hps", glance.TextOf(HudGlance.HpsKey));
        Assert.Equal("  12.5%/hr", glance.TextOf(HudGlance.XpKey));
    }

    [Fact]
    public void ASnapshotWithNoEffortYetShowsTheXpRateAlone()
    {
        var snapshot = new StatsSnapshot { XpPerHour = 12.5 };

        var glance = HudGlance.Read(HudGlanceState.Start, snapshot, "", petInserted: false);

        Assert.Equal("dps,xp", glance.RowKey);
        Assert.Equal("", glance.Name);
        Assert.Equal("  12.5%/hr", glance.TextOf(HudGlance.XpKey));
    }

    // ------------------------------------- the one inserted slot (SIGNED #422) ----

    /// <summary>The default row is what SA-1 shipped: the pet slot is ABSENT rather than
    /// present-and-empty. Membership is the whole answer the view reads, so "not inserted"
    /// and "inserted but reading nothing" must not collapse into one value — the second is a
    /// real state (a session with no pet damage yet) and it still draws a slot.</summary>
    [Fact]
    public void WithoutTheSettingThereIsNoPetSlotAtAll()
    {
        var glance = HudGlance.Read(HudGlanceState.Start,
            Situation(xpPerHour: 12.5, petDps: 88, petInserted: false));

        Assert.Equal("dps,xp", glance.RowKey);
        Assert.False(glance.Has(MiniBarPresentation.PetKey));
        Assert.Null(glance.TextOf(MiniBarPresentation.PetKey));
    }

    [Fact]
    public void InsertedTheRowReadsDpsThenPetThenTheXpRate()
    {
        var glance = HudGlance.Read(HudGlanceState.Start,
            Situation(currentDps: 412, xpPerHour: 12.5, petDps: 88, petInserted: true));

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
            HudGlance.Read(HudGlanceState.Start, Situation(petInserted: true))
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
    /// reasoning it had to route around was #413's: a fixed slot that changes identity
    /// mid-session must not be a drop target. DRA-72 removes the identity change altogether,
    /// and this asserts the consequence that matters — an HPS slot ARRIVING lands to the
    /// RIGHT of the pet slot, so the gap the drag measures is the same gap either way.
    /// </summary>
    [Fact]
    public void AnArrivingHealingSlotLandsAfterTheInsertedPetSlot()
    {
        var input = Situation(hps: 141, xpPerHour: 12.5, recentDamage: 200,
            recentHealing: 2400, petDps: 88, petInserted: true);

        var glance = HudGlance.Read(HudGlanceState.Start, input);

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
        var icons = HudGlance.Read(Healing,
                Situation(hps: 141, recentHealing: 2400, petDps: 88, petInserted: true))
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

        var glance = HudGlance.Read(HudGlanceState.Start, snapshot, "Dranak",
            petInserted: true);

        Assert.Equal(88, snapshot.PetDps);
        Assert.Equal("    88 dps", glance.TextOf(MiniBarPresentation.PetKey));
        // …and the SAME snapshot with the setting off draws no slot, so the two facts cannot
        // be read off one another.
        Assert.False(HudGlance
            .Read(HudGlanceState.Start, snapshot, "Dranak", petInserted: false)
            .Has(MiniBarPresentation.PetKey));
    }
}
