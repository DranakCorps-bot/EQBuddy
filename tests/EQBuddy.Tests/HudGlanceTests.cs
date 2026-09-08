using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// The collapsed HUD's three numbers (Surface A / SA-1).
///
/// Two things are being pinned, and only one of them is the obvious one. The swap rule is
/// the interesting logic; the FIXED SHAPE of every string is the trap-12 guard, and it is
/// the half that a later "just add a decimal" would break silently — a readout whose
/// width changes resizes an always-on-top window over a fullscreen game, which is what
/// cost #173 its keyboard.
/// </summary>
public class HudGlanceTests
{
    private static HudGlanceInput Situation(
        double currentDps = 0, double sessionDps = 0, double hps = 0, double xpPerHour = 0,
        long recentDamage = 0, long recentHealing = 0, long damageSinceResume = 0,
        string? name = "Dranak", double petDps = 0, bool petInserted = false) =>
        new(name, currentDps, sessionDps, hps, xpPerHour,
            recentDamage, recentHealing, damageSinceResume, petDps, petInserted);

    // ------------------------------------------------------------- the swap ----

    [Fact]
    public void TheThirdNumberStartsAsExperience()
    {
        var glance = HudGlance.Next(HudThird.Experience, Situation(xpPerHour: 12.5));
        Assert.Equal(HudThird.Experience, glance.Third);
        Assert.Contains("%/hr", glance.ThirdText);
        Assert.Equal(HudGlance.ExperienceIcon, glance.ThirdIcon);
    }

    [Fact]
    public void HealingThatOutweighsDamageOverTheWindowTakesTheThirdSlot()
    {
        var glance = HudGlance.Next(HudThird.Experience,
            Situation(hps: 141, recentDamage: 200, recentHealing: 2400));
        Assert.Equal(HudThird.Healing, glance.Third);
        Assert.Contains("hps", glance.ThirdText);
        Assert.Equal(HudGlance.HealingIcon, glance.ThirdIcon);
    }

    /// <summary>The hysteresis, entering: healing that is real but not the weight of the
    /// window leaves a farmer's XP rate alone. A damage dealer who lands one heal
    /// mid-pull must not lose the number they minimized the widget for.</summary>
    [Fact]
    public void OneHealDuringAFightDoesNotTakeTheXpRateAway()
    {
        var glance = HudGlance.Next(HudThird.Experience,
            Situation(recentDamage: 9000, recentHealing: 300));
        Assert.Equal(HudThird.Experience, glance.Third);
    }

    [Fact]
    public void HealingWithNoDamageAtAllStillCounts()
    {
        // A healer between pulls: nothing to hit, everything to mend.
        var glance = HudGlance.Next(HudThird.Experience,
            Situation(recentDamage: 0, recentHealing: 1200));
        Assert.Equal(HudThird.Healing, glance.Third);
    }

    /// <summary>The hysteresis, leaving — and this direction is deliberately INSTANT.
    /// The spec's words are "collapse again the moment combat-as-damage returns", so one
    /// swing inside the short resume window is enough, even while the thirty-second
    /// window is still overwhelmingly healing.</summary>
    [Fact]
    public void OneSwingBringsTheXpRateBackImmediately()
    {
        var glance = HudGlance.Next(HudThird.Healing,
            Situation(recentDamage: 40, recentHealing: 9000, damageSinceResume: 40));
        Assert.Equal(HudThird.Experience, glance.Third);
    }

    /// <summary>Damage in the long window but NOT in the resume one is damage that has
    /// already stopped — it must not flip the slot back, or the two windows would be one
    /// window and the swap would chatter.</summary>
    [Fact]
    public void DamageThatHasAlreadyStoppedDoesNotFlipItBack()
    {
        var glance = HudGlance.Next(HudThird.Healing,
            Situation(recentDamage: 4000, recentHealing: 9000, damageSinceResume: 0));
        Assert.Equal(HudThird.Healing, glance.Third);
    }

    /// <summary>A healer who simply stops: thirty seconds later the window holds no
    /// healing, and there is no longer anything for the slot to be about.</summary>
    [Fact]
    public void TheSlotGoesBackWhenTheHealingWindowEmpties()
    {
        var glance = HudGlance.Next(HudThird.Healing, Situation());
        Assert.Equal(HudThird.Experience, glance.Third);
    }

    /// <summary>Equal weight is not dominance. Ties go to XP, because XP is the default
    /// and a swap needs a reason.</summary>
    [Fact]
    public void EqualHealingAndDamageLeavesTheSlotAlone()
    {
        var glance = HudGlance.Next(HudThird.Experience,
            Situation(recentDamage: 1000, recentHealing: 1000));
        Assert.Equal(HudThird.Experience, glance.Third);
    }

    /// <summary>Feeding the answer back in is how a host uses this, so a settled state
    /// has to stay settled rather than oscillate.</summary>
    [Fact]
    public void FedBackToItselfTheSwapSettles()
    {
        var input = Situation(hps: 141, recentDamage: 100, recentHealing: 3000);
        var third = HudThird.Experience;
        for (var i = 0; i < 5; i++) third = HudGlance.Next(third, input).Third;
        Assert.Equal(HudThird.Healing, third);
    }

    // ------------------------------------------------------- the fixed shape ----

    /// <summary>Trap 12: the widget is SizeToContent, so a metric that changes width IS a
    /// window resize — on a timer, forever. Every value either slot can hold formats to
    /// the same number of characters.</summary>
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
    [InlineData(9999.9)]
    [InlineData(50000)]
    public void EveryExperienceStringIsTheSameLength(double xpPerHour)
    {
        Assert.Equal(HudGlance.MetricFixedLength,
            HudGlance.ThirdText(HudThird.Experience, Situation(xpPerHour: xpPerHour)).Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7.5)]
    [InlineData(141)]
    [InlineData(98765)]
    [InlineData(1000000)]
    public void EveryHealingStringIsTheSameLength(double hps)
    {
        Assert.Equal(HudGlance.MetricFixedLength,
            HudGlance.ThirdText(HudThird.Healing, Situation(hps: hps)).Length);
    }

    /// <summary>The SWAP is the timer-driven width change this class exists to prevent,
    /// so the two strings slot three alternates between must be the same length as each
    /// other — not merely each internally consistent.</summary>
    [Fact]
    public void TheTwoThirdSlotStringsAreTheSameWidthAsEachOther()
    {
        var input = Situation(hps: 141, xpPerHour: 12.5);
        Assert.Equal(
            HudGlance.ThirdText(HudThird.Experience, input).Length,
            HudGlance.ThirdText(HudThird.Healing, input).Length);
    }

    /// <summary>Between pulls the DPS slot falls back to the session rate — the same rule
    /// the bar's own dps cell always had, so promoting the number did not redefine it.</summary>
    [Fact]
    public void TheDpsSlotFallsBackToTheSessionRateBetweenPulls()
    {
        Assert.Equal("   412 dps", HudGlance.DpsText(Situation(currentDps: 0, sessionDps: 412)));
        Assert.Equal("   500 dps", HudGlance.DpsText(Situation(currentDps: 500, sessionDps: 412)));
    }

    // --------------------------------------------------------------- the name ----

    [Fact]
    public void AKnownCharacterNameIsShownAsItIs() =>
        Assert.Equal("Dranak", HudGlance.NameText("Dranak"));

    /// <summary>An empty name is a normal state — the log has not named anybody yet — and
    /// it renders as an EMPTY slot at the reserved width rather than as a placeholder
    /// sentence or a collapsing hole. Both alternatives move the two numbers beside it.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnUnknownCharacterNameIsEmptyRatherThanAPlaceholder(string? name)
    {
        Assert.Equal("", HudGlance.NameText(name));
        Assert.True(HudGlance.NameReservedWidth > 0,
            "the empty slot has to keep its width, or the metrics beside it move");
    }

    // -------------------------------------------------------- straight off a snapshot ----

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

        var glance = HudGlance.Next(HudThird.Experience, snapshot, "Dranak", petInserted: false);

        Assert.Equal(HudThird.Healing, glance.Third);
        Assert.Equal("Dranak", glance.Name);
        Assert.Equal("   300 dps", glance.Dps);
        Assert.Equal("   141 hps", glance.ThirdText);
    }

    [Fact]
    public void ASnapshotWithNoEffortYetShowsTheXpRate()
    {
        var snapshot = new StatsSnapshot { XpPerHour = 12.5 };
        var glance = HudGlance.Next(HudThird.Experience, snapshot, "", petInserted: false);
        Assert.Equal(HudThird.Experience, glance.Third);
        Assert.Equal("", glance.Name);
        Assert.Equal("  12.5%/hr", glance.ThirdText);
    }

    // ------------------------------------- the one optional slot (SIGNED #422) ----

    /// <summary>The default row is byte-for-byte what SA-1 shipped: three slots, and the
    /// optional one is a NULL rather than an empty string. Null is the membership answer the
    /// view reads, so "not inserted" and "inserted but reading nothing" must not collapse
    /// into one value — the second is a real state (a session with no pet damage yet) and it
    /// still draws a slot.</summary>
    [Fact]
    public void WithoutTheSettingThereIsNoPetSlotAtAll()
    {
        var glance = HudGlance.Next(HudThird.Experience,
            Situation(xpPerHour: 12.5, petDps: 88, petInserted: false));
        Assert.Null(glance.PetDps);
    }

    [Fact]
    public void InsertedTheRowReadsNameThenDpsThenPetThenTheThirdNumber()
    {
        var glance = HudGlance.Next(HudThird.Experience,
            Situation(currentDps: 412, xpPerHour: 12.5, petDps: 88, petInserted: true));

        Assert.Equal("Dranak", glance.Name);
        Assert.Equal("   412 dps", glance.Dps);
        Assert.Equal("    88 dps", glance.PetDps);
        Assert.Equal("  12.5%/hr", glance.ThirdText);
    }

    /// <summary>A pet that has done nothing yet is a normal state and draws a slot reading
    /// zero — the alternative is a row that changes width the moment the pet lands its first
    /// hit, which is the trap-12 resize this class exists to prevent.</summary>
    [Fact]
    public void AnInsertedPetWithNoDamageYetStillDrawsItsSlot() =>
        Assert.Equal("     0 dps",
            HudGlance.Next(HudThird.Experience, Situation(petInserted: true)).PetDps);

    /// <summary>Trap 12, for the new slot: every value it can hold formats to the same
    /// number of characters as the two metrics beside it, so an insert changes the measured
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
        // invariant misses: three slots of one width is what makes the row measurable.
        Assert.Equal(HudGlance.DpsText(Situation(currentDps: petDps)).Length,
            HudGlance.PetText(Situation(petDps: petDps)).Length);
    }

    /// <summary>The insertion point sits BETWEEN two fixed slots and the third one goes on
    /// swapping exactly as it did — which is the whole reason #413's "a fixed slot that
    /// changes identity mid-session must not be a drop target" is routed around rather than
    /// reopened. Both directions of the hysteresis, with the slot inserted.</summary>
    [Fact]
    public void InsertingThePetSlotDoesNotDisturbTheThirdSlotsSwap()
    {
        var healing = Situation(hps: 141, recentDamage: 200, recentHealing: 2400,
            petDps: 88, petInserted: true);
        var entered = HudGlance.Next(HudThird.Experience, healing);
        Assert.Equal(HudThird.Healing, entered.Third);
        Assert.Equal("   141 hps", entered.ThirdText);
        Assert.Equal(HudGlance.HealingIcon, entered.ThirdIcon);
        Assert.Equal("    88 dps", entered.PetDps);

        var swung = Situation(hps: 141, recentDamage: 40, recentHealing: 9000,
            damageSinceResume: 40, xpPerHour: 12.5, petDps: 88, petInserted: true);
        var left = HudGlance.Next(HudThird.Healing, swung);
        Assert.Equal(HudThird.Experience, left.Third);
        Assert.Equal("  12.5%/hr", left.ThirdText);
        Assert.Equal("    88 dps", left.PetDps);
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

        var glance = HudGlance.Next(HudThird.Experience, snapshot, "Dranak", petInserted: true);

        Assert.Equal(88, snapshot.PetDps);
        Assert.Equal("    88 dps", glance.PetDps);
        // …and the SAME snapshot with the setting off draws no slot, so the two facts cannot
        // be read off one another.
        Assert.Null(HudGlance.Next(HudThird.Experience, snapshot, "Dranak", petInserted: false)
            .PetDps);
    }
}
