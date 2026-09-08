using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Buff countdowns: cast + cast-on-you correlation starts the timer, the fade line
/// ends it, natural fades teach real durations (ranks lengthen; the wiki base is a
/// floor), zoning keeps buffs and dying doesn't. Real log lines throughout.
/// </summary>
public class BuffTrackerTests
{
    private static readonly DateTime T0 = DateTime.Parse("2026-08-12T21:00:00");

    private static GameEvent Ev(int seconds, string message) =>
        LogParser.Parse($"[{T0.AddSeconds(seconds):ddd MMM d HH:mm:ss yyyy}] {message}")!;

    private static BuffTracker Replay(params GameEvent[] events)
    {
        var t = new BuffTracker();
        foreach (var e in events) t.Apply(e);
        return t;
    }

    [Fact]
    public void YourOwnCastPlusLandingStartsAnExactCountdown()
    {
        var t = Replay(
            Ev(0, "You begin casting Armor of Faith."),
            Ev(3, "You feel the favor of the gods upon you."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal("Armor of Faith", b.Label);
        Assert.Equal("You", b.Caster);
        Assert.True(b.Estimated);   // wiki base until a natural fade teaches better
        Assert.Equal(3780 - 1, b.RemainingSeconds(T0.AddSeconds(4))!.Value, 0);
    }

    [Fact]
    public void SomeoneElsesBuffOnYouIsAttributedThroughTheirCastLine()
    {
        var t = Replay(
            Ev(0, "Sanctari begins casting Armor of Faith."),
            Ev(3, "You feel the favor of the gods upon you."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal("Sanctari", b.Caster);
    }

    [Fact]
    public void AnUnexplainedLandingKeepsAllCandidatesAndSaysEst()
    {
        // A clicky or an out-of-range caster: nobody visibly cast anything.
        var t = Replay(Ev(0, "You feel the favor of the gods upon you."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(1)));
        Assert.True(b.Estimated);
        Assert.Equal("", b.Caster);
    }

    [Fact]
    public void TheFadeLineClearsTheBuff()
    {
        // FadeMessages knows Armor of Faith's wear-off line.
        var fadeLine = FadeMessageCatalog.Default.FindBySpell("Armor of Faith");
        Assert.NotNull(fadeLine);   // the test's own premise, checked
        var t = Replay(
            Ev(0, "You begin casting Armor of Faith."),
            Ev(3, "You feel the favor of the gods upon you."),
            Ev(600, fadeLine!.Message));

        Assert.Empty(t.Snapshot(T0.AddSeconds(601)));
    }

    [Fact]
    public void ANaturalFadeTeachesTheRealDurationForNextTime()
    {
        var fadeLine = FadeMessageCatalog.Default.FindBySpell("Armor of Faith")!;
        var t = new BuffTracker();
        // Rank-lengthened: fades at 4200s, wiki says 3780. Learn 4200 (tick-floored).
        t.Apply(Ev(0, "You begin casting Armor of Faith."));
        t.Apply(Ev(2, "You feel the favor of the gods upon you."));
        t.Apply(Ev(2 + 4201, fadeLine.Message));

        Assert.Equal(4200, t.LearnedDurations["Armor of Faith"], 0);

        // The next cast counts down from the learned number, no longer estimated.
        t.Apply(Ev(5000, "You begin casting Armor of Faith."));
        t.Apply(Ev(5002, "You feel the favor of the gods upon you."));
        var b = Assert.Single(t.Snapshot(T0.AddSeconds(5003)));
        Assert.False(b.Estimated);
        Assert.Equal(4200 - 1, b.RemainingSeconds(T0.AddSeconds(5003))!.Value, 0);
    }

    [Fact]
    public void AnEarlyFadeIsADispelNotADuration()
    {
        var fadeLine = FadeMessageCatalog.Default.FindBySpell("Armor of Faith")!;
        var t = Replay(
            Ev(0, "You begin casting Armor of Faith."),
            Ev(2, "You feel the favor of the gods upon you."),
            Ev(300, fadeLine.Message));   // way short of 3780s — dispelled/clicked off

        Assert.Empty(t.Snapshot(T0.AddSeconds(301)));
        Assert.False(t.LearnedDurations.ContainsKey("Armor of Faith"));
    }

    [Fact]
    public void ZoningKeepsBuffsAndDyingClearsThem()
    {
        var zoned = Replay(
            Ev(0, "You begin casting Armor of Faith."),
            Ev(2, "You feel the favor of the gods upon you."),
            Ev(10, "You have entered The Plane of Sky."));
        Assert.Single(zoned.Snapshot(T0.AddSeconds(11)));

        var slain = Replay(
            Ev(0, "You begin casting Armor of Faith."),
            Ev(2, "You feel the favor of the gods upon you."),
            Ev(10, "You have been slain by Master Yael!"));
        Assert.Empty(slain.Snapshot(T0.AddSeconds(11)));
    }

    [Fact]
    public void YourReinforcementRankStretchesYourOwnEstimates()
    {
        // SCR rank 3: +30% on beneficial spells YOU cast. Wiki base 3780 → 4914.
        var t = new BuffTracker { ReinforcementRank = () => 3 };
        t.Apply(Ev(0, "You begin casting Armor of Faith."));
        t.Apply(Ev(2, "You feel the favor of the gods upon you."));

        var own = Assert.Single(t.Snapshot(T0.AddSeconds(3)));
        Assert.Equal(3780 * 1.30 - 1, own.RemainingSeconds(T0.AddSeconds(3))!.Value, 0);
        Assert.True(own.Estimated);   // closer, but still the wiki's number scaled

        // Someone else's cast: their AAs are invisible to your log — base stands.
        var other = new BuffTracker { ReinforcementRank = () => 3 };
        other.Apply(Ev(0, "Sanctari begins casting Armor of Faith."));
        other.Apply(Ev(2, "You feel the favor of the gods upon you."));
        Assert.Equal(3780 - 1, other.Snapshot(T0.AddSeconds(3))[0]
            .RemainingSeconds(T0.AddSeconds(3))!.Value, 0);
    }

    [Fact]
    public void RebuffingRefreshesInsteadOfDuplicating()
    {
        var t = Replay(
            Ev(0, "You begin casting Armor of Faith."),
            Ev(2, "You feel the favor of the gods upon you."),
            Ev(100, "You begin casting Armor of Faith."),
            Ev(102, "You feel the favor of the gods upon you."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(103)));
        Assert.Equal(T0.AddSeconds(102), b.LandedAt);
    }

    // ---- Shield of Thorns: the row that could not be reached (PR #407) ----------
    //
    // Every line below is the owner's own, off `eqlog_Dranak_freeport.txt`. For as long
    // as `BuffDurations.json` carried the wiki page title `Shield of Thorns (Spell)`,
    // all three of these were impossible at once: the landing never resolved, so the
    // label was the line's ("Thorns damage shield"), SCR was gated on `resolved` and
    // never applied, and fade-learn is gated on a single candidate so it could never
    // fire. His chip read 15:00 on all 28 landings of a shield his log measures at
    // ~24 minutes. These three are the audit's prove list, as assertions.

    /// <summary>PROVE 1 — the cast resolves: the log's ranked spelling meets the
    /// catalog's name through <see cref="SpellCatalog.BaseName"/>.</summary>
    [Fact]
    public void ThornsResolvesFromTheOwnersOwnCastLine()
    {
        var t = Replay(
            Ev(0, "You begin casting Shield of Thorns V."),
            Ev(3, "You are surrounded by a thorny barrier."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal("Shield of Thorns", b.Label);          // not "Thorns damage shield"
        Assert.Equal("You", b.Caster);
        Assert.Equal(["Shield of Thorns"], b.Candidates);
    }

    /// <summary>PROVE 2 — Spell Casting Reinforcement rank 1 (+5%, the owner's real rank
    /// from his own AA export; there is no SCR-max and no invented per-rank number)
    /// reaches the spell now that the landing resolves, and it is spent exactly once.
    ///
    /// The length it is spent ON is the RANKED one: 1,350 s for Shield of Thorns V, not the
    /// 900 s the wiki page carries for the folded line. That correction is
    /// <see cref="ThornsVArmsAtTheDurationTheOwnerMeasured"/> below; this test's job is that
    /// rank 1 and only rank 1 is applied to whatever length is picked.</summary>
    [Fact]
    public void ScrRankOneReachesThornsOnceItResolves()
    {
        var t = new BuffTracker { ReinforcementRank = () => 1 };
        t.Apply(Ev(0, "You begin casting Shield of Thorns V."));
        t.Apply(Ev(3, "You are surrounded by a thorny barrier."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal(BuffDurationModel.WithReinforcement(1350, 1) - 1,
            b.RemainingSeconds(T0.AddSeconds(4))!.Value, 0);
        // Rank 4's number would be 2,025 s — nine and a half minutes longer. He has rank 1.
        Assert.NotEqual(BuffDurationModel.WithReinforcement(1350, 4) - 1,
            b.RemainingSeconds(T0.AddSeconds(4))!.Value, 0);
        Assert.True(b.Estimated);   // a shipped length, scaled — still not HIS measured number
    }

    // ---- The early alert: the owner's measured lengths (2026-09-08) ------------------
    //
    // Level 50 Druid, Spell Casting Reinforcement rank 1 of 4, stopwatch on his own client:
    // Shield of Thorns V runs 23:36 and Chloroplast V runs 21:00. Every buff surface arms off
    // BuffState.ExpiresAt — the HUD's expiring chicklet, the Buffs card's warn tint,
    // expiring-only mode — so these two assertions are where "the alert fires several minutes
    // early" lives, and there is one producer to fix.
    //
    // PROVE-FAIL: against the folded wiki bases these read 942 s and 1,008 s, so the first
    // fails by 7:54 and the second by 4:12.

    /// <summary>Shield of Thorns V: 1,350 s base × SCR 1 → 1,416 s = 23:36, as measured.</summary>
    [Fact]
    public void ThornsVArmsAtTheDurationTheOwnerMeasured()
    {
        var t = new BuffTracker { ReinforcementRank = () => 1 };
        t.Apply(Ev(0, "You begin casting Shield of Thorns V."));
        t.Apply(Ev(3, "You are surrounded by a thorny barrier."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal(T0.AddSeconds(3), b.LandedAt);
        Assert.Equal(1416, (b.ExpiresAt!.Value - b.LandedAt).TotalSeconds, 0);
        Assert.Equal("Shield of Thorns V", b.Spell);   // the rank is kept, not folded away
        Assert.Equal("Shield of Thorns", b.Label);     // …while identity still folds
    }

    /// <summary>Chloroplast V: 1,200 s base × SCR 1 → 1,260 s = 21:00, as measured. A second
    /// spell because one row proves a lookup and two prove there is no formula hiding in it —
    /// these sit at +50% and +25% over their wiki bases, so nothing derives both.</summary>
    [Fact]
    public void ChloroplastVArmsAtTheDurationTheOwnerMeasured()
    {
        var t = new BuffTracker { ReinforcementRank = () => 1 };
        t.Apply(Ev(0, "You begin casting Chloroplast V."));
        t.Apply(Ev(3, "You begin to regenerate."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal(1260, (b.ExpiresAt!.Value - b.LandedAt).TotalSeconds, 0);
        Assert.Equal("Chloroplast V", b.Spell);
    }

    /// <summary>A rank nobody has measured borrows nothing: Shield of Thorns II falls back to
    /// the wiki base for the line, which is the honest floor it has always been. Handing it
    /// rank V's 1,350 s would be the same conflation in the other direction.</summary>
    [Fact]
    public void AnUnmeasuredRankFallsBackToTheWikiBase()
    {
        var t = new BuffTracker { ReinforcementRank = () => 1 };
        t.Apply(Ev(0, "You begin casting Shield of Thorns II."));
        t.Apply(Ev(3, "You are surrounded by a thorny barrier."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal(BuffDurationModel.WithReinforcement(900, 1),
            (b.ExpiresAt!.Value - b.LandedAt).TotalSeconds, 0);
    }

    /// <summary>Someone ELSE casting Shield of Thorns V on you still gets the ranked length —
    /// the rank is on their cast line — but not your SCR, because their AAs are invisible to
    /// your log. 1,350 s flat.</summary>
    [Fact]
    public void AnotherCastersRankedThornsGetsTheLengthButNotYourAas()
    {
        var t = new BuffTracker { ReinforcementRank = () => 1 };
        t.Apply(Ev(0, "Sanctari begins casting Shield of Thorns V."));
        t.Apply(Ev(3, "You are surrounded by a thorny barrier."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal(1350, (b.ExpiresAt!.Value - b.LandedAt).TotalSeconds, 0);
    }

    /// <summary>An UNRESOLVED landing — a clicky, a caster out of range — names no rank, so
    /// there is nothing to look up and the candidate set's longest wiki base stands. The
    /// ledger is reached through the cast line or not at all.</summary>
    [Fact]
    public void AnUnresolvedThornsLandingNeverReachesTheRankedLedger()
    {
        var b = Assert.Single(Replay(Ev(0, "You are surrounded by a thorny barrier."))
            .Snapshot(T0.AddSeconds(1)));

        Assert.Equal("", b.Spell);
        Assert.Equal(900, (b.ExpiresAt!.Value - b.LandedAt).TotalSeconds, 0);
    }

    /// <summary>
    /// PROVE 3 — fade-learn can fire, which is the mechanism that closes the 9-minute
    /// under-read without anyone inventing a duration. The interval is one of the two
    /// clean landing→fade pairs measured in his log (13:54:40 → 14:19:08 = 1,468s),
    /// floored to the server tick: 1,464s / 24:24, taught from the log alone.
    /// </summary>
    [Fact]
    public void AThornsFadeTeachesTheRealDurationFromTheOwnersOwnLog()
    {
        var t = new BuffTracker { ReinforcementRank = () => 1 };
        t.Apply(Ev(0, "You begin casting Shield of Thorns V."));
        t.Apply(Ev(3, "You are surrounded by a thorny barrier."));
        t.Apply(Ev(3 + 1468, "The brambles fall away."));

        var learned = Assert.Single(t.LearnedDurations);
        // Keyed on the RANK he cast, not the folded line: ranks lengthen buffs, so a duration
        // measured at V is not IV's, and one key for both is the conflation that put a
        // 15-minute countdown on a 23-minute shield in the first place.
        Assert.Equal("Shield of Thorns V", learned.Key);
        Assert.Equal(1464, learned.Value, 0);

        // And the next landing opens on the learned number rather than the wiki base —
        // no longer an estimate, because the log measured it.
        t.Apply(Ev(2000, "You begin casting Shield of Thorns V."));
        t.Apply(Ev(2003, "You are surrounded by a thorny barrier."));
        var b = Assert.Single(t.Snapshot(T0.AddSeconds(2004)));
        Assert.Equal(1464 - 1, b.RemainingSeconds(T0.AddSeconds(2004))!.Value, 0);
        Assert.False(b.Estimated);
    }

    /// <summary>
    /// A rank UPGRADE does not inherit the old rank's measured length. Learn Shield of Thorns
    /// IV at 1,200 s, scribe V, and the countdown moves to V's — the ledger's, since nothing
    /// has timed V on this character yet.
    ///
    /// Without the ranked key this is the early alert coming back by the other door: the day
    /// he upgrades a rank, his own store hands the new spell the old spell's number.
    /// </summary>
    [Fact]
    public void LearningARankDoesNotSetTheLengthOfADifferentRank()
    {
        var t = new BuffTracker { ReinforcementRank = () => 1 };
        t.Apply(Ev(0, "You begin casting Shield of Thorns IV."));
        t.Apply(Ev(3, "You are surrounded by a thorny barrier."));
        t.Apply(Ev(3 + 1200, "The brambles fall away."));
        Assert.Equal("Shield of Thorns IV", Assert.Single(t.LearnedDurations).Key);

        t.Apply(Ev(2000, "You begin casting Shield of Thorns V."));
        t.Apply(Ev(2003, "You are surrounded by a thorny barrier."));
        var b = Assert.Single(t.Snapshot(T0.AddSeconds(2004)));
        Assert.Equal(1416, (b.ExpiresAt!.Value - b.LandedAt).TotalSeconds, 0);
        Assert.NotEqual(1200, (b.ExpiresAt.Value - b.LandedAt).TotalSeconds, 0);
    }

    /// <summary>
    /// A duration learned BEFORE ranks were keyed is still honoured. The store is install-wide
    /// and survives upgrades, so dropping the folded key would silently throw away a real
    /// measurement off the player's own log in order to fix a rarer one — and this
    /// character's own timing outranks any length we ship, whichever key it arrived under.
    /// </summary>
    [Fact]
    public void ALegacyFoldedLearnedDurationStillWins()
    {
        var dir = Path.Combine(Path.GetTempPath(), "eqb-buff-" + Guid.NewGuid().ToString("N"));
        var store = Path.Combine(dir, "buffs.json");
        Directory.CreateDirectory(dir);
        try
        {
            // Exactly what a pre-rank store looks like: the folded line name.
            File.WriteAllText(store, "{\"Shield of Thorns\":1500}");
            var t = new BuffTracker { ReinforcementRank = () => 1 };
            t.AttachStore(store);
            t.Apply(Ev(0, "You begin casting Shield of Thorns V."));
            t.Apply(Ev(3, "You are surrounded by a thorny barrier."));

            var b = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
            Assert.Equal(1500, (b.ExpiresAt!.Value - b.LandedAt).TotalSeconds, 0);
            Assert.False(b.Estimated);   // his log measured it, whatever the key
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
