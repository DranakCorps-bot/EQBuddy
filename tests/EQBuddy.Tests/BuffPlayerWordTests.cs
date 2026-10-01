using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// #954 (charlesneitzel): a buff chip can be DISMISSED with a right-click and its LENGTH can
/// be set by the player — the two things a spawn timer already let you do.
///
/// The load-bearing half is the replay. EQBuddy re-reads the whole log on every launch, so
/// anything the player says about a buff that is held only in RAM is undone by the next
/// start (trap 85). The reporter's own words: "Starting a new session in the app does NOT
/// clear orphaned buff chips." Every persistence test below pairs its assertion with the
/// reachable negative — the same replay into a tracker WITHOUT the store — so it cannot go
/// green by the buff simply never having landed.
/// </summary>
public class BuffPlayerWordTests : IDisposable
{
    private static readonly DateTime T0 = DateTime.Parse("2026-09-28T21:00:00");

    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "eqb-buffword-" + Guid.NewGuid().ToString("N"));

    private string Store => Path.Combine(_dir, "buff-player.json");

    public BuffPlayerWordTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp */ }
    }

    private static GameEvent Ev(int seconds, string message) =>
        LogParser.Parse($"[{T0.AddSeconds(seconds):ddd MMM d HH:mm:ss yyyy}] {message}")!;

    /// <summary>The reporter's shape: an UNATTRIBUTED "Stone skin" landing — the label the
    /// catalog gives "Your skin turns hard as stone." when no cast line names it.</summary>
    private static readonly GameEvent[] StoneSkin = [Ev(0, "Your skin turns hard as stone.")];

    /// <summary>A tracker as the app builds one: the player store attached, then the log
    /// selected for a character (which is what sets the character key), then replayed.</summary>
    private BuffTracker Launch(string character, bool withStore, params GameEvent[] log)
    {
        var t = new BuffTracker();
        if (withStore) t.AttachPlayerStore(Store);
        t.AttachSpellbook(null, character);
        foreach (var e in log) t.Apply(e);
        return t;
    }

    // ---- dismiss ----

    /// <summary>The premise, checked: the landing makes the chip the reporter could not get
    /// rid of — the catalog label, estimated.</summary>
    [Fact]
    public void TheReportersStoneSkinLandsAsAnEstimatedChip()
    {
        var b = Assert.Single(Launch("Charles", true, StoneSkin).Snapshot(T0.AddSeconds(1)));
        Assert.Equal("Stone skin", b.Label);
        Assert.True(b.Estimated);
    }

    [Fact]
    public void RightClickingTheHudChipDismissesTheBuffNow()
    {
        var t = Launch("Charles", true, StoneSkin);
        // 1,620 s estimate, 60 s window: the chicklet is up from 1,560 s.
        var chip = Assert.Single(HudChipRow.BuffChips(t, T0.AddSeconds(1600), warnSeconds: 60));

        chip.OnDismiss!();

        Assert.Empty(t.Snapshot(T0.AddSeconds(1600)));
        Assert.Empty(HudChipRow.BuffChips(t, T0.AddSeconds(1600), warnSeconds: 60));
    }

    /// <summary>
    /// THE REPORT: "Starting a new session in the app does NOT clear orphaned buff chips."
    ///
    /// PREDICTION: after the dismissal, a fresh tracker replaying the same log with the store
    /// shows nothing — and the same replay without the store shows the chip, which is what
    /// the relaunch would have drawn if the dismissal lived in RAM.
    /// </summary>
    [Fact]
    public void ADismissalSurvivesTheLaunchReplay()
    {
        Launch("Charles", true, StoneSkin).Dismiss("Stone skin");

        Assert.Empty(Launch("Charles", true, StoneSkin).Snapshot(T0.AddSeconds(1)));
        Assert.Single(Launch("Charles", false, StoneSkin).Snapshot(T0.AddSeconds(1)));
    }

    /// <summary>A dismissal is of ONE landing — the next real one starts a fresh chip, the
    /// slow-chip rule. "Never show me this buff" is the family Mute, not this.</summary>
    [Fact]
    public void TheNextRealLandingShowsAgain()
    {
        var t = Launch("Charles", true, StoneSkin);
        t.Dismiss("Stone skin");

        t.Apply(Ev(2000, "Your skin turns hard as stone."));

        var b = Assert.Single(t.Snapshot(T0.AddSeconds(2001)));
        Assert.Equal(T0.AddSeconds(2000), b.LandedAt);
    }

    /// <summary>A dismissal is per character: B's own landing of the same buff, even an
    /// OLDER one, is not A's to hide.</summary>
    [Fact]
    public void ADismissalOnOneCharacterDoesNotHideAnothers()
    {
        Launch("Charles", true, Ev(500, "Your skin turns hard as stone.")).Dismiss("Stone skin");

        Assert.Single(Launch("Other", true, StoneSkin).Snapshot(T0.AddSeconds(1)));
    }

    /// <summary>The landing still HAPPENED: the buff set's honesty states read what the log
    /// showed, and a dismissal is the player's word about the chip, not about the log.</summary>
    [Fact]
    public void ADismissedLandingIsStillALandingTheLogShowed()
    {
        Launch("Charles", true, StoneSkin).Dismiss("Stone skin");

        var t = Launch("Charles", true, StoneSkin);

        Assert.Contains("Skin Like Rock", t.SetSights().Landings);
    }

    [Fact]
    public void DismissingABuffThatIsNotUpWritesNothing()
    {
        Launch("Charles", true, StoneSkin).Dismiss("Armor of Faith");

        Assert.False(File.Exists(Store));
    }

    // ---- the player's length ----

    /// <summary>
    /// PREDICTION: Armor of Faith lands at +3 s on the 3,780 s catalog estimate. The player
    /// says it runs 70 minutes: the RUNNING countdown re-derives from its original landing —
    /// 4,200 − 1 = 4,199 s left at +4 s, not 4,200 from the moment of the edit — and it is no
    /// longer "est". Clearing it puts EQBuddy's number back, estimate flag and all.
    /// </summary>
    [Fact]
    public void AnEditedLengthReDerivesTheRunningCountdownAndClearingItPutsItBack()
    {
        var t = Launch("Charles", true,
            Ev(0, "You begin casting Armor of Faith."),
            Ev(3, "You feel the favor of the gods upon you."));
        var now = T0.AddSeconds(4);

        Assert.True(t.SetPlayerLength("Armor of Faith", 4200));
        var edited = Assert.Single(t.Snapshot(now));
        Assert.Equal(4199, edited.RemainingSeconds(now)!.Value, 0);
        Assert.False(edited.Estimated);
        Assert.True(edited.PlayerSet);
        Assert.Contains("your length: 1h 10m", BuffRosterPresentation.Detail(edited));

        Assert.True(t.SetPlayerLength("Armor of Faith", null));
        var back = Assert.Single(t.Snapshot(now));
        Assert.Equal(3779, back.RemainingSeconds(now)!.Value, 0);
        Assert.True(back.Estimated);
        Assert.False(back.PlayerSet);
    }

    /// <summary>The spawn-override rule: a typed length outranks inference forever — across
    /// the launch replay, and for the next landing too.</summary>
    [Fact]
    public void AnEditedLengthSurvivesTheReplayAndAppliesToTheNextLanding()
    {
        Launch("Charles", true, StoneSkin).SetPlayerLength("Stone skin", 2160);

        var t = Launch("Charles", true, StoneSkin);
        Assert.Equal(2160, Assert.Single(t.Snapshot(T0)).RemainingSeconds(T0)!.Value, 0);

        var without = Launch("Charles", false, StoneSkin);
        Assert.Equal(1620, Assert.Single(without.Snapshot(T0)).RemainingSeconds(T0)!.Value, 0);
    }

    /// <summary>
    /// Trap 71: the length is filed under the RANKED name. A length the player typed while
    /// Shield of Thorns V was up says nothing about rank IV — one key for both is the fold
    /// that made the alert fire eight minutes early.
    /// </summary>
    [Fact]
    public void AnEditedLengthIsFiledUnderTheRankTheLogNamed()
    {
        var t = Launch("Charles", true,
            Ev(0, "You begin casting Shield of Thorns V."),
            Ev(3, "You are surrounded by a thorny barrier."));
        var five = Assert.Single(t.Snapshot(T0.AddSeconds(4)));
        Assert.Equal("Shield of Thorns V", five.DurationKey);
        t.SetPlayerLength(five.DurationKey, 1500);
        Assert.True(Assert.Single(t.Snapshot(T0.AddSeconds(4))).PlayerSet);

        t.Apply(Ev(3000, "You begin casting Shield of Thorns IV."));
        t.Apply(Ev(3003, "You are surrounded by a thorny barrier."));

        Assert.False(Assert.Single(t.Snapshot(T0.AddSeconds(3004))).PlayerSet);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-60d)]
    [InlineData(24 * 3600d)]
    [InlineData(double.NaN)]
    public void ALengthOutsideADayIsRefusedAndChangesNothing(double seconds)
    {
        var t = Launch("Charles", true, StoneSkin);

        Assert.False(t.SetPlayerLength("Stone skin", seconds));
        Assert.Null(t.PlayerLengthFor("Stone skin"));
        Assert.False(File.Exists(Store));
    }

    // ---- the editor's words, and the chip's gestures ----

    [Theory]
    [InlineData("27", 1620d)]
    [InlineData("8.5m", 510d)]
    [InlineData("1h 30m", 5400d)]
    [InlineData("45:00", 2700d)]
    [InlineData("90s", 90d)]
    public void TheEditorReadsTheSpawnTimerGrammar(string typed, double seconds)
    {
        Assert.Equal(seconds, BuffLengthEditor.Read(typed, out var error));
        Assert.Equal("", error);
    }

    /// <summary>An unreadable entry REFUSES with a sentence; it never quietly clears the
    /// player's length (the spawn side's #124).</summary>
    [Theory]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("25h")]
    public void TheEditorRefusesWhatItCannotReadAndSaysSo(string typed)
    {
        Assert.Null(BuffLengthEditor.Read(typed, out var error));
        Assert.NotEqual("", error);
    }

    [Fact]
    public void TheEditorNamesBothNumbersAndTheKeyItFilesUnder()
    {
        var t = Launch("Charles", true, StoneSkin);
        var b = Assert.Single(t.Snapshot(T0));

        Assert.Equal("EQBuddy's length: 27m (estimated from the catalog — ranks and AAs can make it longer)",
            BuffLengthEditor.DerivedLine(b));
        Assert.Equal("27m", BuffLengthEditor.Prefill(b, null));
        Assert.Equal("36m", BuffLengthEditor.Prefill(b, 2160));
        Assert.Equal("", BuffLengthEditor.TypedLine(null));
        Assert.Contains("Stone skin", BuffLengthEditor.ScopeLine(b));
    }

    /// <summary>The hover names the gesture the FAMILY has — the renderer used to spell the
    /// spawn double-click for every chip.</summary>
    [Fact]
    public void EachFamilyNamesItsOwnDoubleClick()
    {
        Assert.Equal("Double-click: the zone's camp list", HudChipRow.DoubleClickHint(HudChipFamily.Spawn));
        Assert.Contains("how long this buff lasts", HudChipRow.DoubleClickHint(HudChipFamily.Buff));
        Assert.Equal("", HudChipRow.DoubleClickHint(HudChipFamily.Mez));
        Assert.Contains("shows again", HudChipRow.DismissHint(HudChipFamily.Buff));
        Assert.Equal("Right-click: dismiss", HudChipRow.DismissHint(HudChipFamily.Spawn));
    }

    /// <summary>The Buffs card's roster dismisses through the same tracker call when it is
    /// handed the tracker, and is inert without one.</summary>
    [Fact]
    public void TheRosterChipDismissesThroughTheSameTracker()
    {
        var t = Launch("Charles", true, StoneSkin);
        var shown = t.Snapshot(T0);

        Assert.Null(Assert.Single(BuffRosterPresentation.Chips(shown, T0, 60)).Chip.OnDismiss);
        Assert.Single(BuffRosterPresentation.Chips(shown, T0, 60, t)).Chip.OnDismiss!();
        Assert.Empty(t.Snapshot(T0));
    }
}
