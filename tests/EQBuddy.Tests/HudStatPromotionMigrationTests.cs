using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// <c>AppSettings.MigratePromotedHudStats</c> — the pass that takes "xp", "dps" and "hps"
/// out of <c>MiniStats</c> when they become the always-on collapsed HUD numbers
/// (Surface A / SA-1).
///
/// **The whole reason this migration exists is trap 20.** The ★ for dps and hps was never
/// only a HUD cell: <c>MainWindow.UpdateBreakouts</c> opened the Damage and Healing
/// breakout windows only when the kind was not in <c>DisabledBreakouts</c> AND its key was
/// in <c>MiniStats</c>. Strip the keys naively and every player with those windows open
/// keeps them (the star is now vacuously satisfied) while every player who had them off
/// gets them opening on the next minimize — a switch surviving a promotion with the state
/// it carried thrown away.
///
/// **And it is run TWICE through the whole chain**, which is trap 55's shape and the only
/// test that can see it: after the first pass every one of the three keys IS absent, and
/// "absent" is exactly what this migration reads as "the player had it off". A pass with
/// no one-time flag would close two windows on every launch, forever, on a profile where
/// the player had chosen to keep them.
/// </summary>
public class HudStatPromotionMigrationTests
{
    /// <summary>A profile as it stood before SA-1, with whichever stars the player had.
    /// <c>hadFile</c> is true for every one of these — they are stored profiles.</summary>
    private static AppSettings Stored(string[] miniStats, params string[] disabled) => new()
    {
        MiniStats = [.. miniStats],
        DisabledBreakouts = [.. disabled],
    };

    private static bool RunChain(AppSettings s, bool hadFile = true) => s.ApplyMigrations(hadFile);

    // ------------------------------------------------------- the keys leave ----

    [Theory]
    [InlineData("xp")]
    [InlineData("dps")]
    [InlineData("hps")]
    public void ThePromotedKeysLeaveMiniStats(string key)
    {
        var s = Stored(["kills", key, "loot"]);
        RunChain(s);
        Assert.DoesNotContain(key, s.MiniStats);
        // …and nothing else does. A migration that tidied the list would be a second,
        // unannounced change riding on this one.
        Assert.Equal(["kills", "loot"], s.MiniStats);
    }

    // ------------------------------------- the star's state, carried across ----

    /// <summary>A STARRED dps means the Damage breakout was allowed to open. It has to go
    /// on being allowed, and with the star gone the only thing that can say so is an
    /// absence from <c>DisabledBreakouts</c> — which is what it already is.</summary>
    [Fact]
    public void AnOpenDamageBreakoutStaysOpen()
    {
        var s = Stored(["kills", "dps"]);
        RunChain(s);
        Assert.DoesNotContain("Damage", s.DisabledBreakouts);
    }

    /// <summary>An UNSTARRED hps means the Healing breakout could never open, whatever
    /// <c>DisabledBreakouts</c> said — the gate needed both halves. Now that the tick is
    /// the whole gate, the "off" has to be written down or the window starts appearing on
    /// the next minimize for somebody who never asked for it.</summary>
    [Fact]
    public void AClosedHealingBreakoutStaysClosed()
    {
        var s = Stored(["kills", "dps"]);   // no "hps"
        RunChain(s);
        Assert.Contains("Healing", s.DisabledBreakouts);
    }

    [Fact]
    public void AnUnstarredDamageBreakoutStaysClosedToo()
    {
        var s = Stored(["kills"]);
        RunChain(s);
        Assert.Contains("Damage", s.DisabledBreakouts);
    }

    [Fact]
    public void AStarredHealingBreakoutStaysOpen()
    {
        var s = Stored(["kills", "hps"]);
        RunChain(s);
        Assert.DoesNotContain("Healing", s.DisabledBreakouts);
    }

    /// <summary>A player who had BOTH halves off keeps one entry, not two — the pass adds
    /// a kind it has already found rather than duplicating it.</summary>
    [Fact]
    public void AnAlreadyDisabledKindIsNotAddedTwice()
    {
        var s = Stored(["kills"], "Damage");
        RunChain(s);
        Assert.Equal(1, s.DisabledBreakouts.Count(k => k == "Damage"));
    }

    /// <summary>"xp" has no <c>BreakoutKind</c> at all — the tab-less Progress float was
    /// retired in the 2026-08-24 fold — so unstarring it must write nothing anywhere.
    /// A pass that treated all three keys alike would invent a disabled window.</summary>
    [Fact]
    public void XpWritesNoBreakoutStateBecauseItHasNoWindow()
    {
        var s = Stored(["kills"]);
        RunChain(s);
        Assert.DoesNotContain("Progress", s.DisabledBreakouts);
        Assert.All(s.DisabledBreakouts, k => Assert.Contains(k, new[] { "Damage", "Healing" }));
    }

    // ------------------------------------------------------- run it TWICE ----

    /// <summary>Trap 55, exactly: the second pass through the WHOLE chain reports nothing
    /// and changes nothing. The pre-flag version of this migration fails here — after run
    /// one the keys are gone, and "gone" is what it reads as "the player had it off".</summary>
    [Fact]
    public void TheSecondRunOfTheWholeChainIsSilent()
    {
        var s = Stored(["kills", "dps", "hps", "xp", "loot"]);
        RunChain(s);
        var afterFirst = (mini: string.Join(",", s.MiniStats),
                          disabled: string.Join(",", s.DisabledBreakouts.Order()));

        Assert.False(RunChain(s), "the second pass through ApplyMigrations must report no work");
        Assert.Equal(afterFirst.mini, string.Join(",", s.MiniStats));
        Assert.Equal(afterFirst.disabled, string.Join(",", s.DisabledBreakouts.Order()));
    }

    /// <summary>…and the case the flag is really for: a player who KEPT both windows.
    /// Without the one-time flag, run two reads their now-absent stars as "off" and
    /// silently closes both — the promotion undoing the player once per launch.</summary>
    [Fact]
    public void ASecondRunDoesNotCloseWindowsThePlayerKept()
    {
        var s = Stored(["kills", "dps", "hps"]);
        RunChain(s);
        Assert.DoesNotContain("Damage", s.DisabledBreakouts);
        Assert.DoesNotContain("Healing", s.DisabledBreakouts);

        RunChain(s);
        RunChain(s);
        Assert.DoesNotContain("Damage", s.DisabledBreakouts);
        Assert.DoesNotContain("Healing", s.DisabledBreakouts);
    }

    // ------------------------------------------------------ a FRESH profile ----

    /// <summary>A brand-new profile has no star to read: the defaults ARE the promoted
    /// state. Running the pass against them would read the new default as the player's old
    /// choice and close the Damage window a fresh install has always had — which is why
    /// this migration takes <c>hadFile</c>, exactly as <c>MigrateMotesCard</c> does.</summary>
    [Fact]
    public void AFreshProfileIsBornPromotedAndKeepsItsDamageWindow()
    {
        var s = new AppSettings();
        RunChain(s, hadFile: false);

        Assert.True(s.HudStatsPromoted);
        Assert.DoesNotContain("Damage", s.DisabledBreakouts);
        Assert.Contains("Healing", s.DisabledBreakouts);
        Assert.Empty(s.MiniStats.Where(k => k is "xp" or "dps" or "hps"));
    }

    /// <summary>The out-of-the-box defaults have to say what the stars used to say: "dps"
    /// was starred by default and "hps" was not, so a fresh install opened Damage on
    /// minimize and never opened Healing. That is preserved behaviour, not a new
    /// opinion — asserted here so a later "why is Healing disabled by default?" tidy-up
    /// has to argue with the reason.</summary>
    [Fact]
    public void TheDefaultsCarryWhatTheStarsUsedToSay()
    {
        var fresh = new AppSettings();
        Assert.Equal(["kills"], fresh.MiniStats);
        Assert.Equal(["Healing"], fresh.DisabledBreakouts);
    }

    // ------------------------------------------- the gate the keys fed ----

    /// <summary>The other half of the re-key, in the place both the widget's gate and the
    /// Options tick read it from: Damage and Healing have no star any more, and Watch is
    /// still the one kind Options cannot finish switching on.</summary>
    [Fact]
    public void DamageAndHealingNoLongerHaveAStarToGateThem()
    {
        Assert.Null(BreakoutPresentation.StarKey(BreakoutPresentation.Damage));
        Assert.Null(BreakoutPresentation.StarKey(BreakoutPresentation.Healing));
        Assert.Null(BreakoutPresentation.StarKey(BreakoutPresentation.Progress));

        // The kinds that KEEP one — the negative that stops the assertion above going
        // vacuous the day somebody nulls the whole table (trap 39).
        Assert.Equal("pet", BreakoutPresentation.StarKey(BreakoutPresentation.Pet));
        Assert.Equal("loot", BreakoutPresentation.StarKey(BreakoutPresentation.Loot));
        Assert.Equal("buffs", BreakoutPresentation.StarKey(BreakoutPresentation.Buffs));
    }

    /// <summary>A null star used to MEAN "this is Watch". Four kinds share that null now,
    /// so the pinned-rule rule is asked for by name — otherwise a Damage row would be told
    /// it opens for a pinned watch rule, which is a tick box that lies.</summary>
    [Fact]
    public void OnlyWatchStillNeedsAPinnedRule()
    {
        Assert.True(BreakoutPresentation.NeedsPinnedRule(BreakoutPresentation.Watch));
        Assert.False(BreakoutPresentation.NeedsPinnedRule(BreakoutPresentation.Damage));
        Assert.False(BreakoutPresentation.NeedsPinnedRule(BreakoutPresentation.Healing));
        Assert.False(BreakoutPresentation.NeedsPinnedRule(BreakoutPresentation.Pet));
    }

    /// <summary>Each row's hover text comes from the kind, and the promoted pair get their
    /// own — naming the removed toggle rather than only the replacement (#233).</summary>
    [Fact]
    public void ThePromotedRowsExplainThatTheirStarIsGone()
    {
        Assert.Equal(BreakoutPresentation.PromotedNote,
            BreakoutPresentation.Note(BreakoutPresentation.Damage));
        Assert.Equal(BreakoutPresentation.PromotedNote,
            BreakoutPresentation.Note(BreakoutPresentation.Healing));
        Assert.Equal(BreakoutPresentation.WatchNote,
            BreakoutPresentation.Note(BreakoutPresentation.Watch));
        Assert.Equal(BreakoutPresentation.StarNote,
            BreakoutPresentation.Note(BreakoutPresentation.Pet));
    }

    /// <summary>The #326 vocabulary ban, on the strings this pass rewrote. The shell
    /// scanner does not reach v1 Options, which is how "mini pill" survived there.</summary>
    [Fact]
    public void NoneOfTheBreakoutCopySaysMiniPill()
    {
        foreach (var text in new[]
                 {
                     BreakoutPresentation.Blurb, BreakoutPresentation.WatchNote,
                     BreakoutPresentation.StarNote, BreakoutPresentation.PromotedNote,
                 })
            Assert.DoesNotContain("mini pill", text, StringComparison.OrdinalIgnoreCase);
    }
}
