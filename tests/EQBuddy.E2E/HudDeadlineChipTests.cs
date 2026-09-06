using EQBuddy.Core;

namespace EQBuddy.E2E;

/// <summary>
/// SA-3's two NET-NEW deadline chip families — a watch rule that has just fired, and a buff
/// inside its expiry warning window — reaching the screen on the row SA-2 built.
///
/// **`HudChipRowTests` in EQBuddy.Tests proves the chicklets; this proves the wiring.** Both
/// families are new behaviour rather than a fold, so the failure this file exists for is not
/// a flattened difference (that was SA-2's) but a family that is perfectly correct and never
/// gets asked: a ledger nothing records into, a threshold read from the wrong place, a chip
/// built on a tick the row does not run. Every one of those compiles, passes the unit suite
/// and photographs as a shorter row (trap 29).
///
/// The watch half in particular can ONLY be asserted from a launched app: the recording
/// happens inside `MainWindow.FireAlert`, behind the cooldown gate, on the dispatcher, and
/// there is no seam below the window to reach it.
///
/// [Collection("e2e")] because every test here launches a real always-on-top widget and two
/// at once would fight for the desktop (trap 57).
/// </summary>
[Collection("e2e")]
public sealed class HudDeadlineChipTests
{
    /// <summary>
    /// A Text watch rule fires on a line the game writes, and its chicklet arrives on the row.
    ///
    /// THE PREDICTION, written before it ran (trap 23): with one enabled Text rule and no
    /// spawn timers, appending the matching line gives `hudChipsWatch=1`, `hudChipsRow=1`,
    /// and `hudChipsSpawn=0` / `hudChipsMez=0` / `hudChipsBuff=0` — the three other families
    /// asserted at ZERO on purpose, because a merge that leaked one family's chips into
    /// another's count would pass an "is the row up" assertion (SA-2's own lesson).
    ///
    /// `AppendLogLines` writes AFTER launch deliberately: a matching line already in the log
    /// at startup is suppressed with every other alert during the initial ingest, which is
    /// correct behaviour and would make this test assert nothing.
    /// </summary>
    [Fact]
    public void AFiringWatchRulePutsAChipOnTheRow()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = false;
            settings.TrackedRules.Add(new TrackedRule
            {
                Id = "e2e-watch-fire",
                Name = "Assist call",
                Pattern = "assist on",
                Kind = WatchKind.Text,
                Enabled = true,
                AlertBanner = true,
            });
        });
        app.Launch();
        app.WaitForDump("hudChipsWatch", 0, "no watch chip before anything has fired");

        app.AppendLogLines("Sanctari tells the group, 'assist on a froglok tad shaman'");

        app.WaitForDump("hudChipsWatch", 1, "the fired rule's chicklet to arrive on the row");
        app.WaitForDump("hudChipsRow", 1, "the row itself to come up for it");
        app.WaitForDump("hudChipsSpawn", 0, "no spawn chip to appear from a rule firing");
        app.WaitForDump("hudChipsMez", 0, "no mez chip either");
        app.WaitForDump("hudChipsBuff", 0, "and no buff chip");
    }

    /// <summary>
    /// **A rule whose banner the player turned OFF gets no chicklet — and one whose banner is
    /// on, firing on the SAME line at the SAME moment, does.**
    ///
    /// This is the one SA-3 judgement that could have gone the other way, so it is asserted
    /// rather than left to the comment that explains it. `AlertBanner` is the existing "put
    /// this rule on my screen" switch; until SA-4 brings the per-family Mute, a chip that
    /// ignored it would be an on-screen output with no off-switch anywhere.
    ///
    /// **TWO rules on one line, and that shape is the whole test.** The obvious version —
    /// one banner-off rule, append, assert zero — is a guard that cannot fail (trap 34).
    /// `AppendLogLines` returns when the TAIL has read the bytes, but `OnTextMatched` alerts
    /// through `Dispatcher.BeginInvoke`, so the dump is read before the alert path has run
    /// and the assertion passes against an app that has not yet decided anything. It was
    /// written that way first and a prove-fail run caught it: with the gate deleted the test
    /// still passed. Waiting for the LOUD rule's chip is a synchronisation point that only
    /// exists on the far side of the decision — both rules are handled in one dispatcher
    /// callback, so by the time one chicklet is on the row the other rule has been asked too.
    ///
    /// THE PREDICTION: `hudChipsWatch=1`, and the one chicklet is the loud rule's. With the
    /// gate removed it is 2 and the wait times out, which is the run that earns this test.
    /// </summary>
    [Fact]
    public void ARuleWithItsBannerTurnedOffGetsNoChipWhileItsLoudTwinDoes()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = false;
            settings.TrackedRules.Add(new TrackedRule
            {
                Id = "e2e-watch-quiet",
                Name = "Assist call (quiet)",
                Pattern = "assist on",
                Kind = WatchKind.Text,
                Enabled = true,
                AlertBanner = false,
            });
            settings.TrackedRules.Add(new TrackedRule
            {
                Id = "e2e-watch-loud",
                Name = "Assist call (loud)",
                Pattern = "assist on",
                Kind = WatchKind.Text,
                Enabled = true,
                AlertBanner = true,
            });
        });
        app.Launch();
        app.AppendLogLines("Sanctari tells the group, 'assist on a froglok tad shaman'");

        app.WaitForDump("hudChipsWatch", 1,
            "exactly one of the two rules that matched to reach the HUD — the loud one");
    }

    /// <summary>
    /// A buff lands and takes its place on the row once it is inside the player's warning
    /// window — and the window is the player's own `BuffWarnSeconds`, not a pinned constant.
    ///
    /// **Staged through the setting, which is what makes this assertable at all.** Every buff
    /// in the shipped catalog runs for tens of minutes, so no test can wait one out. Setting
    /// the warning window to two hours puts a freshly-landed 63-minute buff inside it
    /// immediately — a real state a real player reaches by typing a number into Options — and
    /// it is a strictly stronger assertion than a short buff would be: a chip built on a
    /// hard-coded 60 s threshold passes nothing here.
    ///
    /// THE PREDICTION: `hudChipsBuff=1`, `hudChipsRow=1`, and the other three families at 0.
    /// The two appended lines are the real pair `BuffTracker` correlates — a cast line and the
    /// catalog's landing message — through the real parser, and "Armor of Faith" is 3,780 s.
    /// </summary>
    [Fact]
    public void AnExpiringBuffPutsAChipOnTheRow()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = false;
            settings.BuffWarnSeconds = 7200;
        });
        app.Launch();
        app.WaitForDump("hudChipsBuff", 0, "no buff chip before anything has landed");

        app.AppendLogLines(
            "You begin casting Armor of Faith.",
            "You feel the favor of the gods upon you.");

        app.WaitForDump("hudChipsBuff", 1, "the landed buff to take its place on the row");
        app.WaitForDump("hudChipsRow", 1, "the row itself to come up for it");
        app.WaitForDump("hudChipsMez", 0, "no mez chip to appear from a buff landing");
        app.WaitForDump("hudChipsWatch", 0, "and no watch chip");
    }

    /// <summary>
    /// **The threshold is READ, not pinned** — the same buff, the same lines, the default
    /// 60-second window, and no chicklet.
    ///
    /// It is the negative half of the test above and the pair is the point: either one alone
    /// passes with the wrong implementation. A hard-coded threshold passes this one; a family
    /// that shows every active buff passes the other.
    ///
    /// THE PREDICTION: `hudChipsBuff=0` with `buffsCard` showing the buff is genuinely
    /// tracked — asserted through the widget's own Buffs count so "no chip" cannot quietly be
    /// "no buff".
    /// </summary>
    [Fact]
    public void ABuffOutsideTheWarningWindowStaysOffTheRow()
    {
        using var app = new AppHarness(settings =>
        {
            settings.TrackSpawns = false;
            settings.BuffWarnSeconds = 60;
        });
        app.Launch();
        app.AppendLogLines(
            "You begin casting Armor of Faith.",
            "You feel the favor of the gods upon you.");

        app.WaitForDump("buffsActive", 1, "the buff to be tracked, so a missing chip is the window");
        app.WaitForDump("hudChipsBuff", 0, "a buff an hour from fading to stay off the HUD");
    }
}
