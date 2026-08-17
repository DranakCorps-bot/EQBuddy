using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The mez-target tracker: cast→landing correlation from ANY group member's log (the
/// landing line is bystander-visible and other players' casts log with spell and rank),
/// durations from the eqlwiki catalog, break-on-damage, and caster-side duration
/// learning from natural fades.
/// </summary>
public class MezTrackerTests
{
    private static readonly DateTime T0 = DateTime.Parse("2026-08-04T20:00:00");

    private static GameEvent Ev(int seconds, string message) =>
        LogParser.Parse($"[{T0.AddSeconds(seconds):ddd MMM d HH:mm:ss yyyy}] {message}")!;

    private static MezTracker Replay(params GameEvent[] events)
    {
        var t = new MezTracker();
        foreach (var e in events) t.Apply(e);
        return t;
    }

    /// <summary>
    /// #183, TheLethean: "when I mez multiple mobs with the same name, and then wake one
    /// by attacking it, the chips for all of the same-named mobs disappear."
    ///
    /// ONE break prints TWO lines, and each of them dropped a chip:
    ///   Your Mesmerization spell has worn off of a skeleton.
    ///   A skeleton has been awakened by Dorr.
    /// so every wake cost two. His second log counts it exactly — one wake, two chips —
    /// and note the ORDER: the fade line comes FIRST, which is why the existing
    /// "did the awake ledger just move" guard could not see it.
    /// </summary>
    [Fact]
    public void OneWakeCostsOneChipEvenThoughTheGamePrintsTwoLines()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a putrid skeleton has been mesmerized."),
            // One skeleton woken: the pair, fade first, exactly as his log has it.
            Ev(5, "Your Mesmerization spell has worn off of a skeleton."),
            Ev(5, "A skeleton has been awakened by Dorr."));

        var chips = t.Snapshot(T0.AddSeconds(6));
        Assert.Equal(3, chips.Count);
        Assert.Equal(2, chips.Count(c => c.Target == "Skeleton"));
        Assert.Single(chips, c => c.Target == "Putrid skeleton");
    }

    /// <summary>The same pair in the other order — a log that prints the break line
    /// first must not double-drop either.</summary>
    [Fact]
    public void ThePairIsCoalescedWhicheverHalfArrivesFirst()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(5, "A skeleton has been awakened by Dorr."),
            Ev(5, "Your Mesmerization spell has worn off of a skeleton."));

        Assert.Equal(2, t.Snapshot(T0.AddSeconds(6)).Count);
    }

    /// <summary>Pairing must stay 1:1. Two mobs genuinely broken at once print two of
    /// EACH line, and both breaks still have to count — otherwise the fix would trade
    /// vanishing chips for chips that never leave.</summary>
    [Fact]
    public void TwoRealBreaksInTheSameSecondStillDropTwoChips()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(5, "Your Mesmerization spell has worn off of a skeleton."),
            Ev(5, "A skeleton has been awakened by Dorr."),
            Ev(5, "Your Mesmerization spell has worn off of a skeleton."),
            Ev(5, "A skeleton has been awakened by Dorr."));

        Assert.Equal(2, t.Snapshot(T0.AddSeconds(6)).Count);
    }

    /// <summary>A break of ONE name says nothing about another. The putrid skeleton in
    /// his log kept its chip throughout, and must.</summary>
    [Fact]
    public void ABreakPairNeverReachesADifferentlyNamedMob()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a putrid skeleton has been mesmerized."),
            Ev(5, "Your Mesmerization spell has worn off of a skeleton."),
            Ev(5, "A skeleton has been awakened by Dorr."));

        Assert.Single(t.Snapshot(T0.AddSeconds(6)), c => c.Target == "Putrid skeleton");
    }

    /// <summary>A genuine LATER break still drops its chip — the token must not linger
    /// and swallow the next one.</summary>
    [Fact]
    public void ALaterBreakOfTheSameNameIsNotSwallowedByAnEarlierPair()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(1, "a skeleton has been mesmerized."),
            Ev(5, "Your Mesmerization spell has worn off of a skeleton."),
            Ev(5, "A skeleton has been awakened by Dorr."),
            Ev(12, "Your Mesmerization spell has worn off of a skeleton."),
            Ev(12, "A skeleton has been awakened by Dorr."));

        Assert.Single(t.Snapshot(T0.AddSeconds(13)));
    }

    [Fact]
    public void OwnMezCastPlusLandingStartsACountdown()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerize."),
            Ev(2, "ice boned skeleton has been mesmerized."));

        var m = Assert.Single(t.Snapshot(T0.AddSeconds(3)));
        Assert.Equal("Ice boned skeleton", m.Target);
        Assert.Equal("You", m.Caster);
        Assert.Equal(23, m.RemainingSeconds(T0.AddSeconds(3))!.Value, 0);   // 24s catalog duration
    }

    [Fact]
    public void AGroupMembersMezIsTrackedFromABystanderLog()
    {
        // The whole point: Hugzee casts, THIS log belongs to someone else in the group.
        var t = Replay(
            Ev(0, "Hugzee begins casting Enthrall."),
            Ev(3, "an orc centurion has been enthralled."));

        var m = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal("Hugzee", m.Caster);
        Assert.Equal("Enthrall", m.Spell);
        Assert.Equal(47, m.RemainingSeconds(T0.AddSeconds(4))!.Value, 0);   // 48s catalog duration
    }

    [Fact]
    public void ALandingNobodyVisiblyCastIsIgnored()
    {
        // Same trust rule as charm: an unexplained bystander-visible line claims nothing.
        var t = Replay(Ev(0, "a Teir`Dal rogue has been mesmerized."));
        Assert.Empty(t.Snapshot(T0.AddSeconds(1)));
    }

    [Fact]
    public void DamageWakesTheTargetAndClearsTheChip()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerize."),
            Ev(2, "ice boned skeleton has been mesmerized."),
            Ev(5, "Twiddley slashes ice boned skeleton for 12 points of damage."));

        Assert.Empty(t.Snapshot(T0.AddSeconds(6)));
    }

    // ---- #122 (Snagglefern's Plane of Hate log): the explicit awakened line and
    // the resist pre-arm — same-name adds must not eat mezzed siblings' chips. ----

    [Fact]
    public void TheAwakenedLineDropsExactlyOneChip()
    {
        // Two same-named drakes mezzed by one AE cast (same-second landings are
        // distinct creatures); the game's own break line wakes ONE of them.
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(2, "an ashenbone drake has been mesmerized."),
            Ev(2, "an ashenbone drake has been mesmerized."),
            Ev(8, "An ashenbone drake has been awakened by Terrak."));

        Assert.Single(t.Snapshot(T0.AddSeconds(9)));   // the sibling sleeps on
    }

    [Fact]
    public void AResistedTwinsAttacksDoNotEatTheMezzedSiblingsChip()
    {
        // Snagglefern's exact sequence: one drake resists the AE (it is awake and
        // fighting), the other is mezzed. Without the resist pre-arm the awake
        // drake's claws attributed to the shared name and dropped the sleeping
        // sibling's chip within seconds.
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(2, "An ashenbone drake resisted your Mesmerization!"),
            Ev(2, "an ashenbone drake has been mesmerized."),
            Ev(5, "An ashenbone drake claws YOU for 75 points of damage."),
            Ev(7, "An ashenbone drake claws YOU for 41 points of damage."));

        Assert.Single(t.Snapshot(T0.AddSeconds(8)));   // the mezzed one keeps its chip
    }

    [Fact]
    public void AResistNeverDropsAChipByItself()
    {
        // A mezzed mob can resist a re-application while staying asleep — the
        // resist only ADDS awake knowledge, it never touches chips.
        var t = Replay(
            Ev(0, "You begin casting Mesmerize."),
            Ev(2, "ice boned skeleton has been mesmerized."),
            Ev(8, "You begin casting Mesmerize."),
            Ev(10, "An ice boned skeleton resisted your Mesmerize!"));

        Assert.Single(t.Snapshot(T0.AddSeconds(11)));
    }

    [Fact]
    public void AwakenedThenRemezzedGetsAFreshChip()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerize."),
            Ev(2, "ice boned skeleton has been mesmerized."),
            Ev(10, "Ice boned skeleton has been awakened by Terrak."),
            Ev(12, "You begin casting Mesmerize."),
            Ev(14, "ice boned skeleton has been mesmerized."));

        var m = Assert.Single(t.Snapshot(T0.AddSeconds(15)));
        Assert.Equal(T0.AddSeconds(14), m.LandedAt);
    }

    [Fact]
    public void AoeMezCoversEveryLandingFromOneCast()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(3, "an orc pawn has been mesmerized."),
            Ev(3, "an orc centurion has been mesmerized."));

        Assert.Equal(2, t.Snapshot(T0.AddSeconds(4)).Count);
    }

    [Fact]
    public void TheCasterLearnsTheRealDurationFromANaturalFade()
    {
        // Rank II lasts longer than the base 24s the catalog knows; the caster's own
        // worn-off line measures it, and the next cast uses the learned value.
        var t = Replay(
            Ev(0, "You begin casting Mesmerize II."),
            Ev(2, "an orc pawn has been mesmerized."),
            // 32s raw gap = a 30s (5-tick) mez plus worn-off message lag — the learner
            // tick-floors it (Aenari's report: raw gaps made timers run 2-3s long).
            Ev(34, "Your Mesmerize II spell has worn off of an orc pawn."),
            Ev(60, "You begin casting Mesmerize II."),
            Ev(62, "a gnoll has been mesmerized."));

        Assert.Equal(30, t.LearnedDurations["Mesmerize II"], 0);
        var m = Assert.Single(t.Snapshot(T0.AddSeconds(63)));
        Assert.Equal(29, m.RemainingSeconds(T0.AddSeconds(63))!.Value, 0);
    }

    [Fact]
    public void ExoticLandingLinesParse()
    {
        Assert.IsType<MezzedEvent>(Ev(0, "an orc pawn swoons in raptured bliss."));
        Assert.IsType<MezzedEvent>(Ev(0, "a gnoll begins to scream."));
        Assert.IsType<MezzedEvent>(Ev(0, "a gnoll's eyes glaze over."));
        Assert.IsType<MezzedEvent>(Ev(0, "an orc oracle has been mesmerized by the Glamour of Kintaz."));
        // And the cast line that isn't a landing stays a cast.
        Assert.IsType<OtherCastEvent>(Ev(0, "Shack begins casting Shield of Thistles IV."));
    }

    /// <summary>Issue #32 item 2: chain-mezzing one target is the normal workflow — a
    /// re-landing must REFRESH the countdown (also what keeps bard pulse songs alive).</summary>
    [Fact]
    public void RemezRefreshesTheCountdown()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerize."),
            Ev(2, "an orc pawn has been mesmerized."),
            Ev(20, "You begin casting Mesmerize."),
            Ev(22, "an orc pawn has been mesmerized."));

        var m = Assert.Single(t.Snapshot(T0.AddSeconds(23)));
        Assert.Equal(23, m.RemainingSeconds(T0.AddSeconds(23))!.Value, 0);   // 22+24-23
    }

    /// <summary>Issue #32 item 3: same-second landings (an AoE catching same-named
    /// mobs) are distinct creatures with separate chips — and a break clears ONE of
    /// them (the earliest-expiring), not both.</summary>
    [Fact]
    public void AoeSameNameGetsSeparateEntriesAndBreaksClearOne()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(3, "an orc pawn has been mesmerized."),
            Ev(3, "an orc pawn has been mesmerized."));
        Assert.Equal(2, t.Snapshot(T0.AddSeconds(4)).Count);

        t.Apply(Ev(6, "Twiddley slashes an orc pawn for 5 points of damage."));
        Assert.Single(t.Snapshot(T0.AddSeconds(7)));
    }

    /// <summary>Issue #32 item 1: a DoT cast before the mez keeps ticking on the player
    /// while the mob sleeps — the tick must not clear the chip. Real melee still does.</summary>
    [Fact]
    public void ADotTickFromTheMezzedMobDoesNotClearTheChip()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerize."),
            Ev(2, "an orc oracle has been mesmerized."),
            Ev(8, "You have taken 7 damage from Flame Lick by an orc oracle."));
        Assert.Single(t.Snapshot(T0.AddSeconds(9)));

        t.Apply(Ev(12, "Orc oracle hits YOU for 9 points of damage."));
        Assert.Empty(t.Snapshot(T0.AddSeconds(13)));
    }

    /// <summary>Issue #32 item 1 (the other half): rank-lengthened mezzes outlive the
    /// catalog's base duration — an expired chip lingers visibly at 0:00 instead of
    /// silently vanishing mid-mez.</summary>
    [Fact]
    public void AnExpiredChipLingersBrieflyInsteadOfVanishing()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerize."),
            Ev(2, "an orc pawn has been mesmerized."));   // expires at +26

        var lingering = Assert.Single(t.Snapshot(T0.AddSeconds(30)));
        Assert.Equal(0, lingering.RemainingSeconds(T0.AddSeconds(30))!.Value, 0);
        Assert.Empty(t.Snapshot(T0.AddSeconds(40)));
    }

    /// <summary>A rank-lengthened mez fades long after the base duration — the entry
    /// must still be there (hidden) for the fade to teach the real duration, or high
    /// ranks would be unlearnable.</summary>
    [Fact]
    public void AFadeWellPastTheBaseDurationStillTeaches()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerize III."),
            Ev(2, "an orc pawn has been mesmerized."),                     // base says +26
            Ev(46, "Your Mesmerize III spell has worn off of an orc pawn."));   // 44s raw

        Assert.Equal(42, t.LearnedDurations["Mesmerize III"], 0);   // tick-floored: 7 ticks
    }

    /// <summary>Issue #35 (Vellum670): once one twin breaks, its ongoing fight kept
    /// generating damage lines for the shared name — and each line ate another
    /// sibling's chip. The awake ledger attributes those lines to the woken creature.</summary>
    [Fact]
    public void AWokenTwinsFightDoesNotEatTheSleepersChip()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(3, "a rock golem has been mesmerized."),
            Ev(3, "a rock golem has been mesmerized."),
            Ev(10, "Twiddley slashes a rock golem for 12 points of damage."));   // one breaks
        Assert.Single(t.Snapshot(T0.AddSeconds(11)));

        // The woken golem fights on — its lines are ITS fight, not new breaks.
        t.Apply(Ev(12, "A rock golem hits Twiddley for 9 points of damage."));
        t.Apply(Ev(14, "Twiddley slashes a rock golem for 15 points of damage."));
        Assert.Single(t.Snapshot(T0.AddSeconds(15)));

        // Killing the awake one settles the ledger; the sleeper keeps its chip.
        t.Apply(Ev(18, "You have slain a rock golem!"));
        Assert.Single(t.Snapshot(T0.AddSeconds(19)));

        // Nothing awake anymore: the next hit is a REAL break of the sleeper.
        t.Apply(Ev(20, "Twiddley slashes a rock golem for 15 points of damage."));
        Assert.Empty(t.Snapshot(T0.AddSeconds(21)));
    }

    /// <summary>Re-mezzing the woken twin adds a fresh chip — it must not steal or
    /// refresh the still-sleeping sibling's.</summary>
    [Fact]
    public void RemezOfTheWokenTwinAddsAChipWithoutTouchingTheSleeper()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(3, "a rock golem has been mesmerized."),
            Ev(3, "a rock golem has been mesmerized."),
            Ev(10, "Twiddley slashes a rock golem for 12 points of damage."),
            Ev(12, "You begin casting Mesmerize."),
            Ev(14, "a rock golem has been mesmerized."));

        Assert.Equal(2, t.Snapshot(T0.AddSeconds(15)).Count);
    }

    /// <summary>Field report (David, live session): a single 7s early break got learned
    /// as Mesmerize's "duration" and shrank every chip on the machine. The worn-off
    /// line fires on breaks too — an observation under the catalog base (ranks only
    /// lengthen) must never teach.</summary>
    /// <summary>The Reddit report (2026-08-10): an AoE mez re-cast on cooldown showed
    /// 1+ minute chips. Re-mezzing an already-mezzed target logs NO new landing line,
    /// so the entry's anchor stays at the FIRST landing and the eventual natural fade
    /// measures the whole chain — and a camper who always chains never produces the
    /// clean fade that latest-wins healing needs. The visible re-casts themselves are
    /// the tell: a fade with a same-spell cast after the landing teaches nothing.</summary>
    [Fact]
    public void AChainedAoeMezFadeTeachesNothing()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization."),
            Ev(3, "an orc pawn has been mesmerized."),        // anchor: t=3, base 24s
            Ev(20, "You begin casting Mesmerization."),       // re-mez in place — no landing line
            Ev(40, "You begin casting Mesmerization."),       // and again
            Ev(63, "Your Mesmerization spell has worn off of an orc pawn."));   // 60s chain

        Assert.False(t.LearnedDurations.ContainsKey("Mesmerization"),
            "a chain-spanning fade must not become the learned duration");

        // The next cast still shows the catalog's 24s, not a one-minute chip.
        t.Apply(Ev(100, "You begin casting Mesmerization."));
        t.Apply(Ev(103, "an orc pawn has been mesmerized."));
        var chip = Assert.Single(t.Snapshot(T0.AddSeconds(104)));
        Assert.Equal(23, chip.RemainingSeconds(T0.AddSeconds(104))!.Value, 0);

        // A clean cycle (no re-cast before the fade) still teaches normally.
        t.Apply(Ev(127, "Your Mesmerization spell has worn off of an orc pawn."));
        Assert.Equal(24, t.LearnedDurations["Mesmerization"]);
    }

    [Fact]
    public void AnEarlyBreakFadeDoesNotPoisonTheLearnedDuration()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerize."),
            Ev(2, "an orc pawn has been mesmerized."),
            Ev(9, "Your Mesmerize spell has worn off of an orc pawn."));   // 7s = break

        Assert.False(t.LearnedDurations.ContainsKey("Mesmerize"));

        // The next cast still counts down from the honest catalog base.
        t.Apply(Ev(20, "You begin casting Mesmerize."));
        t.Apply(Ev(22, "a gnoll has been mesmerized."));
        var m = Assert.Single(t.Snapshot(T0.AddSeconds(23)));
        Assert.Equal(23, m.RemainingSeconds(T0.AddSeconds(23))!.Value, 0);
    }

    [Fact]
    public void PoisonedStoreValuesAreQuarantinedOnLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eqbuddy-mez-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"Mesmerize":7,"Enthrall":52}""");
            var t = new MezTracker();
            t.AttachStore(path);
            // 7 is under Mesmerize's base 24 → quarantined; 52 carries message lag and
            // tick-floors to 48 (≥ Enthrall's base 48) → kept, healed.
            Assert.False(t.LearnedDurations.ContainsKey("Mesmerize"));
            Assert.Equal(48, t.LearnedDurations["Enthrall"]);
        }
        finally { File.Delete(path); }
    }

    /// <summary>Discussions #68/#69: chain-mez artifacts wrote inflated durations into
    /// the learned store, and "only ever learn longer" made them immortal (Taendar's
    /// 1:10 chip on a 24s mez); the ×2 ceiling that briefly replaced it clipped genuine
    /// mote-upgraded durations (rahvynn's 44s Mesmerization shrank to base 24). Truth:
    /// a Legends mez holds fixed duration unless damage breaks it, and broken chips
    /// never reach the learner — so the latest CLEAN observation wins, both directions,
    /// and any poison heals on the very next honest fade.</summary>
    [Fact]
    public void ChainMezPoisonHealsOnTheFirstCleanFade()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eqbuddy-mez-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"Mesmerization V":72}""");
            var t = new MezTracker();
            t.AttachStore(path);
            // The stale 72 loads (nothing better is known yet)…
            Assert.Equal(72, t.LearnedDurations["Mesmerization V"]);

            // …and the first clean land→fade at 24s replaces it outright.
            t.Apply(Ev(0, "You begin casting Mesmerization V."));
            t.Apply(Ev(2, "a farmer has been mesmerized."));
            t.Apply(Ev(26, "Your Mesmerization V spell has worn off of a farmer."));
            Assert.Equal(24, t.LearnedDurations["Mesmerization V"], 0);
        }
        finally { File.Delete(path); }
    }

    /// <summary>rahvynn's case (#69): a legitimately long mote-upgraded duration —
    /// past double the catalog base — must survive a reload untouched.</summary>
    [Fact]
    public void LegitimateLongUpgradedDurationSurvivesReload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"eqbuddy-mez-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"Mesmerization":54}""");
            var t = new MezTracker();
            t.AttachStore(path);
            Assert.Equal(54, t.LearnedDurations["Mesmerization"]);
        }
        finally { File.Delete(path); }
    }

    /// <summary>A chain-mez artifact learned live (a clicky re-mez logs no cast line,
    /// so the fade measures against the original anchor) is wrong for one cycle —
    /// then the next clean fade heals it. Self-correcting beats permanently wrong.</summary>
    [Fact]
    public void LiveChainArtifactHealsItself()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization V."),
            Ev(2, "a farmer has been mesmerized."),
            // Unexplained clicky re-mezzes kept the t=2 anchor: 72s measured.
            Ev(74, "Your Mesmerization V spell has worn off of a farmer."),
            // The next honest single mez corrects the record.
            Ev(100, "You begin casting Mesmerization V."),
            Ev(102, "a rat has been mesmerized."),
            Ev(126, "Your Mesmerization V spell has worn off of a rat."));

        Assert.Equal(24, t.LearnedDurations["Mesmerization V"], 0);
    }

    /// <summary>The genuine upgrade duration learns from its first natural fade; the
    /// entry is retained past its visible expiry precisely so this fade can find it.</summary>
    [Fact]
    public void UpgradedRankDurationStillLearns()
    {
        var t = Replay(
            Ev(0, "You begin casting Mesmerization VI."),
            Ev(2, "a farmer has been mesmerized."),
            Ev(38, "Your Mesmerization VI spell has worn off of a farmer."));

        Assert.Equal(36, t.LearnedDurations["Mesmerization VI"], 0);
    }

    [Fact]
    public void ZoningClearsEverything()
    {
        var t = Replay(
            Ev(0, "You begin casting Entrance."),
            Ev(2, "an orc pawn has been entranced."),
            Ev(5, "You have entered Clan Crushbone."));

        Assert.Empty(t.Snapshot(T0.AddSeconds(6)));
    }

    [Fact]
    public void UnknownDurationChipsStillShowAndStillBreak()
    {
        // A mez spell missing from the catalog: chip appears with no countdown, and
        // damage still clears it. (Requires the spell to be cast-correlated — add the
        // name to MezSpells.json for it to track at all; this uses a catalog entry
        // with its duration nulled to simulate the pre-research state.)
        var t = new MezTracker([new MezSpellInfo { Name = "Mesmerize" }]);
        t.Apply(Ev(0, "You begin casting Mesmerize."));
        t.Apply(Ev(2, "an orc pawn has been mesmerized."));

        var m = Assert.Single(t.Snapshot(T0.AddSeconds(3)));
        Assert.Null(m.RemainingSeconds(T0.AddSeconds(3)));

        t.Apply(Ev(6, "Twiddley slashes an orc pawn for 5 points of damage."));
        Assert.Empty(t.Snapshot(T0.AddSeconds(7)));
    }

    /// <summary>Audit finding 8: the 120s unknown-duration cap pruned by LandedAt
    /// alone, so a mez KNOWN to run longer was dropped mid-sleep — its chip vanished
    /// early and its natural fade found no entry to learn from, making long
    /// durations permanently unlearnable. Known durations now hold their entries to
    /// ExpiresAt + the linger.</summary>
    [Fact]
    public void AKnownLongMezOutlivesTheUnknownDurationCapAndStaysLearnable()
    {
        var t = new MezTracker([new MezSpellInfo { Name = "Longsleep", DurationSeconds = 150 }]);
        t.Apply(Ev(0, "You begin casting Longsleep."));
        t.Apply(Ev(2, "an orc pawn has been mesmerized."));                  // asleep until 152
        t.Apply(Ev(130, "You slash a gnoll for 5 points of damage."));       // a prune past the cap

        var m = Assert.Single(t.Snapshot(T0.AddSeconds(140)));               // chip survives the cap
        Assert.Equal(12, m.RemainingSeconds(T0.AddSeconds(140))!.Value, 0);

        t.Apply(Ev(154, "Your Longsleep spell has worn off of an orc pawn."));
        Assert.Equal(150, t.LearnedDurations["Longsleep"], 0);               // the fade still taught
    }
}
