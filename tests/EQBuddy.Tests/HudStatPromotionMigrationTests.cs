using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// <c>AppSettings.MigrateHudStatStars</c> — the pass that puts "xp", "dps" and "hps" BACK
/// into <c>MiniStats</c>, because the collapsed bar's contents are the Mini dashboard's
/// checkboxes again (DRA-81's Founder LOCK). It replaces SA-1's promotion pass, which took
/// the same three keys out, and it still owes that pass's other half to any profile that
/// never went through it.
///
/// **Two populations, and telling them apart is the whole job.** A profile written since
/// SA-1 has <c>HudStatsPromoted</c> set and no three keys: it needs them restored and
/// nothing else. A profile from BEFORE SA-1 still carries the player's own stars, and those
/// stars are the only surviving record of whether the Damage and Healing windows were
/// allowed to open — <c>MainWindow.UpdateBreakouts</c> gated on the kind being absent from
/// <c>DisabledBreakouts</c> AND the key being in <c>MiniStats</c>. Add the keys to that
/// profile without reading them first and the evidence is destroyed in the same statement
/// (trap 20: the switch survives, the state it carried does not).
///
/// **And it is run TWICE through the whole chain**, which is trap 55's shape and the only
/// test that can see it: after the first pass all three keys ARE present, which is exactly
/// what a re-run of the pre-SA-1 branch would read as "the player had these windows open".
///
/// **The flag is its own, and trap 76 is why.** Every profile this pass exists for already
/// has <c>HudStatsPromoted</c> set — it is what the broken build wrote — so a repair gated
/// on that bool could never fire for the one population that needs it.
/// </summary>
public class HudStatPromotionMigrationTests
{
    /// <summary>A profile as it stood BEFORE SA-1, with whichever stars the player had.
    /// <c>hadFile</c> is true for every one of these — they are stored profiles.</summary>
    private static AppSettings Stored(string[] miniStats, params string[] disabled) => new()
    {
        MiniStats = [.. miniStats],
        DisabledBreakouts = [.. disabled],
    };

    /// <summary>A profile as SA-1 left it: promoted, and with the three keys already taken
    /// away. This is the Founder's file, and every other player's.</summary>
    private static AppSettings Promoted(params string[] miniStats) => new()
    {
        MiniStats = [.. miniStats],
        DisabledBreakouts = ["Healing"],
        HudStatsPromoted = true,
    };

    private static bool RunChain(AppSettings s, bool hadFile = true) => s.ApplyMigrations(hadFile);

    // ------------------------------------------------------ the keys come back ----

    /// <summary>
    /// **THE FOUNDER'S SMOKE, as a migration.** Their profile went through SA-1, so it holds
    /// no "hps" key at all — which under the new rule would mean a healer opening the app to
    /// find HPS missing AND an unticked box, which is the bug arriving a second time wearing
    /// the fix's clothes. All three come back.
    /// </summary>
    [Theory]
    [InlineData("xp")]
    [InlineData("dps")]
    [InlineData("hps")]
    public void ThePromotedKeysComeBackToMiniStats(string key)
    {
        var s = Promoted("kills", "loot");

        RunChain(s);

        Assert.Contains(key, s.MiniStats);
        // …and the player's own stars are untouched. A migration that tidied the list would
        // be a second, unannounced change riding on this one.
        Assert.Equal(["kills", "loot", "dps", "hps", "xp"], s.MiniStats);
    }

    /// <summary>A key the player already has is not added twice — a bar that drew one stat
    /// two-and-a-half times is what a duplicated key buys, and `MiniStats` is a list rather
    /// than a set.</summary>
    [Fact]
    public void AKeyAlreadyPresentIsNotDuplicated()
    {
        var s = Promoted("kills", "hps");

        RunChain(s);

        Assert.Equal(1, s.MiniStats.Count(k => k == "hps"));
    }

    /// <summary>The restore reaches the ROW, not just the list — the claim the profile can
    /// make and the claim the player can see are different ones (trap 42's shape, one layer
    /// down), and <see cref="HudGlanceStars.From"/> is the seam between them.</summary>
    [Fact]
    public void AfterTheRestoreTheRowDrawsAllThree()
    {
        var s = Promoted("kills");

        RunChain(s);

        Assert.Equal(new HudGlanceStars(Dps: true, Hps: true, Xp: true, Pet: false),
            HudGlanceStars.From(s));
    }

    // ------------------------------------- the star's state, carried across ----

    /// <summary>A STARRED dps on a PRE-SA-1 profile means the Damage breakout was allowed
    /// to open. It has to go on being allowed, and the only thing that can say so once the
    /// star stops gating the window is an absence from <c>DisabledBreakouts</c> — which is
    /// what it already is.</summary>
    [Fact]
    public void AnOpenDamageBreakoutStaysOpen()
    {
        var s = Stored(["kills", "dps"]);
        RunChain(s);
        Assert.DoesNotContain("Damage", s.DisabledBreakouts);
    }

    /// <summary>An UNSTARRED hps on a pre-SA-1 profile means the Healing breakout could
    /// never open, whatever <c>DisabledBreakouts</c> said — the gate needed both halves.
    /// Now that the tick is the whole gate, the "off" has to be written down BEFORE the key
    /// is restored, or the window starts appearing on the next minimize for somebody who
    /// never asked for it.</summary>
    [Fact]
    public void AClosedHealingBreakoutStaysClosed()
    {
        var s = Stored(["kills", "dps"]);   // no "hps"
        RunChain(s);
        Assert.Contains("Healing", s.DisabledBreakouts);
        // …and the key is restored anyway: the HUD slot and the window are different
        // objects now, and this is the sentence that says so in behaviour.
        Assert.Contains("hps", s.MiniStats);
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

    /// <summary>
    /// **THE ORDER OF OPERATIONS, as its own failure.** The window state is read off the
    /// stars and the stars are about to be overwritten, so a pass that restored first would
    /// find every key present and conclude both windows were wanted.
    ///
    /// Prove-failed rather than asserted in passing: this is the one arrangement where the
    /// two halves of the pass can silently be run the wrong way round, and green-only is
    /// vacuous coverage (trap 34).
    /// </summary>
    [Fact]
    public void TheWindowStateIsReadBeforeTheStarsAreRestored()
    {
        var s = Stored(["kills"]);   // pre-SA-1, both windows off

        RunChain(s);

        Assert.Contains("Damage", s.DisabledBreakouts);
        Assert.Contains("Healing", s.DisabledBreakouts);
        // The restore happened too — so this cannot pass by the pass having done nothing.
        Assert.Contains("dps", s.MiniStats);
        Assert.Contains("hps", s.MiniStats);
    }

    /// <summary>A profile SA-1 already promoted must NOT go through the window branch a
    /// second time: its stars are absent because SA-1 removed them, not because the player
    /// turned anything off, and re-reading them would close a Damage window that has been
    /// open ever since. <c>HudStatsPromoted</c> is what tells the two absences apart.</summary>
    [Fact]
    public void AnAlreadyPromotedProfileKeepsTheWindowsSA1LeftIt()
    {
        var s = new AppSettings
        {
            MiniStats = ["kills"],
            DisabledBreakouts = [],        // both windows open since SA-1
            HudStatsPromoted = true,
        };

        RunChain(s);

        Assert.Empty(s.DisabledBreakouts);
        Assert.Contains("dps", s.MiniStats);
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
    /// retired in the 2026-08-24 fold — so its star must write nothing anywhere.
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
    /// and changes nothing. A pre-flag version fails here — after run one all three keys are
    /// present, and "present" is what the pre-SA-1 branch reads as "these windows were
    /// wanted".</summary>
    [Fact]
    public void TheSecondRunOfTheWholeChainIsSilent()
    {
        var s = Stored(["kills", "loot"]);
        RunChain(s);
        var afterFirst = (mini: string.Join(",", s.MiniStats),
                          disabled: string.Join(",", s.DisabledBreakouts.Order()));

        Assert.False(RunChain(s), "the second pass through ApplyMigrations must report no work");
        Assert.Equal(afterFirst.mini, string.Join(",", s.MiniStats));
        Assert.Equal(afterFirst.disabled, string.Join(",", s.DisabledBreakouts.Order()));
    }

    /// <summary>…and the case the flag is really for: a player who had both windows OFF.
    /// Without the one-time flag, run two reads their now-restored stars as "on" and opens
    /// both — the migration undoing the player once per launch, in the direction that puts
    /// two windows over their game.</summary>
    [Fact]
    public void ASecondRunDoesNotOpenWindowsThePlayerHadClosed()
    {
        var s = Stored(["kills"]);
        RunChain(s);
        Assert.Contains("Damage", s.DisabledBreakouts);
        Assert.Contains("Healing", s.DisabledBreakouts);

        RunChain(s);
        RunChain(s);
        Assert.Contains("Damage", s.DisabledBreakouts);
        Assert.Contains("Healing", s.DisabledBreakouts);
    }

    // ------------------------------------------------------ a FRESH profile ----

    /// <summary>A brand-new profile has no history to restore and its defaults already ARE
    /// the restored state, so the pass stands down — which is why it takes <c>hadFile</c>,
    /// exactly as <c>MigrateMotesCard</c> does. Running it would add "hps" to an install
    /// that never asked for it.</summary>
    [Fact]
    public void AFreshProfileIsBornRestoredAndGainsNoHealingSlot()
    {
        var s = new AppSettings();

        RunChain(s, hadFile: false);

        Assert.True(s.HudStatStarsRestored);
        Assert.Equal(["dps", "xp", "kills"], s.MiniStats);
        Assert.DoesNotContain("hps", s.MiniStats);
        Assert.DoesNotContain("Damage", s.DisabledBreakouts);
        Assert.Contains("Healing", s.DisabledBreakouts);
    }

    /// <summary>
    /// **The default and the migrated state differ by exactly one key, and that is the
    /// decision rather than an accident.**
    ///
    /// A migrated profile gets "hps" because since SA-1 it has been SHOWING HPS whenever
    /// DRA-72's window said so — restoring it takes nothing off the player's screen, and
    /// they can now untick it, which they could not before. A fresh profile has no such
    /// history and a permanent "0 hps" is a poor first impression. Asserted so a later
    /// "these two should surely match" tidy-up has to argue with the reason.
    /// </summary>
    [Fact]
    public void TheMigratedRowGainsHpsAndTheFreshOneDoesNot()
    {
        var fresh = new AppSettings();
        fresh.ApplyMigrations(hadFile: false);

        var migrated = Promoted("kills");
        migrated.ApplyMigrations(hadFile: true);

        Assert.False(HudGlanceStars.From(fresh).Hps);
        Assert.True(HudGlanceStars.From(migrated).Hps);
        // The two agree about everything else on the row, which is what makes the one
        // difference readable as a choice.
        Assert.True(HudGlanceStars.From(fresh).Dps);
        Assert.True(HudGlanceStars.From(migrated).Dps);
        Assert.True(HudGlanceStars.From(fresh).Xp);
        Assert.True(HudGlanceStars.From(migrated).Xp);
    }

    /// <summary>The out-of-the-box defaults: the row every profile has drawn since SA-1
    /// (DPS and the XP rate), and the Healing window still closed because "hps" was never
    /// starred by default before SA-1 either. Preserved behaviour, not a new opinion.</summary>
    [Fact]
    public void TheDefaultsCarryTheRowEveryProfileHasDrawn()
    {
        var fresh = new AppSettings();
        Assert.Equal(["dps", "xp", "kills"], fresh.MiniStats);
        Assert.Equal(["Healing"], fresh.DisabledBreakouts);
    }

    // ------------------------------------------- the gate the keys fed ----

    /// <summary>
    /// **The star is back and it still does not gate the window**, which is the pairing this
    /// whole file is about. <c>StarKey</c> answering null for Damage and Healing used to be
    /// a consequence of the keys not existing; now it is a decision, because unticking DPS
    /// in Mini dashboard must not silently close somebody's Damage window.
    /// </summary>
    [Fact]
    public void DamageAndHealingStillHaveNoStarGatingThem()
    {
        Assert.Null(BreakoutPresentation.StarKey(BreakoutPresentation.Damage));
        Assert.Null(BreakoutPresentation.StarKey(BreakoutPresentation.Healing));
        Assert.Null(BreakoutPresentation.StarKey(BreakoutPresentation.Progress));

        // …even though the keys themselves are real ★s again. Without this line the
        // assertion above reads as "those stats have no star", which stopped being true.
        Assert.Contains(HudGlance.DpsKey, MiniBarPresentation.GlanceKeys);
        Assert.Contains(HudGlance.HpsKey, MiniBarPresentation.GlanceKeys);

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

    /// <summary>Each row's hover text comes from the kind, and Damage/Healing get their own
    /// — the two whose tick does not also set a star, which is now a thing to SAY rather
    /// than a consequence of there being no star to set.</summary>
    [Fact]
    public void TheWindowOnlyRowsSayTheirStarLivesElsewhere()
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

    /// <summary>
    /// **No copy anywhere still tells a player their star is gone** (DRA-81 §2). Every one
    /// of these sentences was written while the three stats were unswitchable, and a screen
    /// that says "there is nothing left to switch off" beside the box that switches it off
    /// is worse than one that says nothing at all.
    ///
    /// A forbid-scan, so it is PAIRED with the must-list above — `TheWindowOnlyRowsSayTheir
    /// StarLivesElsewhere` and the Options block's own tests are what stop this passing on
    /// a file that lost the sentence entirely (trap 34).
    /// </summary>
    [Theory]
    [InlineData("always on")]
    [InlineData("always-on")]
    [InlineData("there is no star")]
    [InlineData("stars are gone")]
    [InlineData("weight of the last half-minute")]
    public void NoBreakoutCopyStillClaimsTheStatsAreUnswitchable(string banned)
    {
        foreach (var text in new[]
                 {
                     BreakoutPresentation.AutoOpenOnTip, BreakoutPresentation.AutoOpenOffTip,
                     BreakoutPresentation.DismissTip, BreakoutPresentation.WatchNote,
                     BreakoutPresentation.StarNote, BreakoutPresentation.PromotedNote,
                 })
            Assert.DoesNotContain(banned, text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The #326 vocabulary ban, on the strings this pass rewrote. The shell
    /// scanner does not reach v1 Options, which is how "mini pill" survived there.</summary>
    [Fact]
    public void NoneOfTheBreakoutCopySaysMiniPill()
    {
        foreach (var text in new[]
                 {
                     BreakoutPresentation.AutoOpenOnTip, BreakoutPresentation.AutoOpenOffTip,
                     BreakoutPresentation.DismissTip, BreakoutPresentation.WatchNote,
                     BreakoutPresentation.StarNote, BreakoutPresentation.PromotedNote,
                 })
            Assert.DoesNotContain("mini pill", text, StringComparison.OrdinalIgnoreCase);
    }
}
