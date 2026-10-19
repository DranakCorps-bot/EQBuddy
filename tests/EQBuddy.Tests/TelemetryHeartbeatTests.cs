using System.Text.Json;
using System.Text.RegularExpressions;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The opt-in heartbeat's one policy** (<c>UI.Shared/TelemetryHeartbeat</c>, DRA-362
/// TEL-PR3; <c>docs/v2/telemetry.md</c> §2, §3, §7, §9). Everything here runs without a network
/// and without a clock: the policy is pure, and the scheduler takes the time it is handed.
/// </summary>
public class TelemetryHeartbeatTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    // ======================================================== TEL-002: the key set ====

    /// <summary>
    /// **§9 guard 1: the serialized heartbeat's keys are EXACTLY installId, appVersion, os.**
    /// Read off a real serialization, not off the record's declaration, so a fourth property
    /// — or a renamed one — reddens here until the signed plan is amended and re-signed.
    /// </summary>
    [Fact]
    public void TheSerializedHeartbeatCarriesExactlyTheThreeSignedKeys()
    {
        var json = TelemetryHeartbeat.Serialize(new TelemetryHeartbeat.Payload(
            TelemetryHeartbeat.MintInstallId(), "2.0.0", "Windows 10.0.26200"));

        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(["installId", "appVersion", "os"], keys);
        Assert.Equal(TelemetryHeartbeat.PayloadKeys, keys);
    }

    /// <summary>The must-list itself, pinned beside the guard that reads it: editing the list
    /// to make a fourth key pass is the same act as adding the key, and fails the same way.</summary>
    [Fact]
    public void TheCuratedKeyListIsTheSignedList() =>
        Assert.Equal(["installId", "appVersion", "os"], TelemetryHeartbeat.PayloadKeys);

    [Fact]
    public void TheDeleteBodyCarriesExactlyOneKey()
    {
        var json = TelemetryHeartbeat.Serialize(
            new TelemetryHeartbeat.DeleteRequest(TelemetryHeartbeat.MintInstallId()));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(["installId"], doc.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void AMintedIdIsALowercaseHyphenatedGuidAndEveryMintIsFresh()
    {
        var a = TelemetryHeartbeat.MintInstallId();
        var b = TelemetryHeartbeat.MintInstallId();
        Assert.True(TelemetryHeartbeat.IsInstallId(a));
        Assert.Equal(36, a.Length);
        Assert.Equal(a.ToLowerInvariant(), a);
        Assert.NotEqual(a, b);
        // Committed negatives: the shapes the server would refuse are no id at all here.
        Assert.False(TelemetryHeartbeat.IsInstallId(a.ToUpperInvariant()));
        Assert.False(TelemetryHeartbeat.IsInstallId(a.Replace("-", "")));
        Assert.False(TelemetryHeartbeat.IsInstallId(""));
        Assert.False(TelemetryHeartbeat.IsInstallId(null));
    }

    [Fact]
    public void VersionAndOsAreCoarse()
    {
        Assert.Equal("2.0.0", TelemetryHeartbeat.AppVersionFrom(new Version(2, 0, 0, 17)));
        Assert.Equal("2.1.0", TelemetryHeartbeat.AppVersionFrom(new Version(2, 1)));
        Assert.Equal("Windows 10.0.26200",
            TelemetryHeartbeat.OsFrom(new Version(10, 0, 26200, 6899)));
    }

    // ================================================================ consent ====

    [Fact]
    public void AFreshProfileIsOffWithNoIdAndNothingToSend()
    {
        var s = new AppSettings();
        Assert.False(s.TelemetryEnabled);
        Assert.Null(s.TelemetryInstallId);
        Assert.False(s.TelemetryPromptShown);
        Assert.False(s.TelemetryOnByDefaultApplied);
        Assert.False(s.TelemetryFirstBeatHeld);
        Assert.Null(TelemetryHeartbeat.PayloadFor(s, "2.0.0", "Windows 10.0.1"));
        Assert.Null(TelemetryHeartbeat.DeleteFor(s));
        Assert.False(TelemetryHeartbeat.MaySend(s, productProfile: true, endpointConfigured: true));
    }

    [Fact]
    public void OptInMintsAnIdAndOptOutDestroysIt_AndReEnablingIsANewIdentity()
    {
        var s = new AppSettings();
        TelemetryHeartbeat.OptIn(s);
        var first = s.TelemetryInstallId;
        Assert.True(s.TelemetryEnabled);
        Assert.True(TelemetryHeartbeat.IsInstallId(first));
        Assert.Equal(first, TelemetryHeartbeat.PayloadFor(s, "2.0.0", "Windows 10.0.1")!.InstallId);

        TelemetryHeartbeat.OptOut(s);
        Assert.False(s.TelemetryEnabled);
        Assert.Null(s.TelemetryInstallId);
        Assert.Null(TelemetryHeartbeat.PayloadFor(s, "2.0.0", "Windows 10.0.1"));

        TelemetryHeartbeat.OptIn(s);
        Assert.NotEqual(first, s.TelemetryInstallId);
    }

    [Fact]
    public void AnIsolatedProfileNeverSendsWhateverItsSettingsSay()
    {
        var s = new AppSettings();
        TelemetryHeartbeat.OptIn(s);
        Assert.True(TelemetryHeartbeat.MaySend(s, productProfile: true, endpointConfigured: true));
        Assert.False(TelemetryHeartbeat.MaySend(s, productProfile: false, endpointConfigured: true));
        Assert.False(TelemetryHeartbeat.MaySend(s, productProfile: true, endpointConfigured: false));
    }

    // ============================================ helpers: the disk's memory ====

    /// <summary>AppSettings carries NaN window positions, as its own Save allows.</summary>
    private static readonly JsonSerializerOptions Json = new()
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private sealed class Disk
    {
        public int Saves;
        public string? LastSaved;
        public void Save(AppSettings s) { Saves++; LastSaved = JsonSerializer.Serialize(s, Json); }
        public AppSettings Reload() => JsonSerializer.Deserialize<AppSettings>(LastSaved!, Json)!;
    }

    /// <summary>One "launch" of the switch-on against the disk, the way App.OnStartup runs it
    /// on a product profile with the shipped endpoint.</summary>
    private static TelemetryHeartbeat.SwitchOnDecision Launch(AppSettings s, Disk disk,
        bool productProfile = true, bool endpointConfigured = true) =>
        TelemetryHeartbeat.RunSwitchOn(s, productProfile, endpointConfigured, () => disk.Save(s));

    // ================================ DRA-2010 §1: the v2.0.7 switch-on, per state ====
    // One test per on-disk shape, each reloaded from what the previous "launch" SAVED, so it is
    // the disk's memory being tested rather than an object's. Prove-failed against the mutants
    // named on each (listed in the PR).

    /// <summary>The three ways an install was OFF before v2.0.7, as the code that wrote each
    /// one leaves the disk — an old "Not now" (the flag alone), a v2.0.6 toggle off after a yes,
    /// and a confirmed delete. They are the same bytes, which is the plan's §1 premise.</summary>
    public static TheoryData<string> AnsweredOffShapes() => ["notNow", "toggleOff", "deleted"];

    private static AppSettings AnsweredOff(string shape)
    {
        var s = new AppSettings();
        switch (shape)
        {
            case "notNow": s.TelemetryPromptShown = true; break;
            case "toggleOff": TelemetryHeartbeat.OptIn(s); TelemetryHeartbeat.OptOut(s); break;
            case "deleted": TelemetryHeartbeat.OptIn(s); TelemetryHeartbeat.DeleteConfirmed(s); break;
        }
        return s;
    }

    [Fact]
    public void TheThreeOldOffsAreTheSameBytes()
    {
        var bytes = new[] { "notNow", "toggleOff", "deleted" }.Select(shape => JsonSerializer.Serialize(AnsweredOff(shape), Json))
            .Distinct().ToList();
        Assert.Single(bytes);
    }

    /// <summary>
    /// **Key false, never answered: switched ON in one save, first beat held, generic note.**
    /// The id is fresh, the key is written with the switch, and <c>TelemetryPromptShown</c> is
    /// left false. Mutants that redden it: the switch-on not writing the key (it would run again
    /// every launch); saving twice; writing the flag (the note would gain the cohort sentence).
    /// </summary>
    [Fact]
    public void KeyFalseNeverAnsweredIsSwitchedOnInOneSaveWithTheFirstBeatHeld()
    {
        var disk = new Disk();
        disk.Save(new AppSettings());
        var s = disk.Reload();

        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.SwitchedOn, Launch(s, disk));
        Assert.Equal(2, disk.Saves);
        var saved = disk.Reload();
        Assert.True(saved.TelemetryEnabled);
        Assert.True(TelemetryHeartbeat.IsInstallId(saved.TelemetryInstallId));
        Assert.True(saved.TelemetryOnByDefaultApplied);
        Assert.True(saved.TelemetryFirstBeatHeld);
        Assert.False(saved.TelemetryPromptShown);
        Assert.True(TelemetryHeartbeat.MaySend(saved, productProfile: true, endpointConfigured: true));
        Assert.Equal(TelemetryHeartbeat.NoteKind.Generic,
            TelemetryHeartbeat.NoteFor(TelemetryHeartbeat.SwitchOnDecision.SwitchedOn, saved));
    }

    /// <summary>
    /// **Key false, answered and off (a past "Not now", a v2.0.6 off, a delete): switched ON,
    /// the first beat held, and the note carries the cohort sentence.** Mutant that reddens it:
    /// a DecideSwitchOn that spares a profile with the flag set (DRA-1980's rule).
    /// </summary>
    [Theory]
    [MemberData(nameof(AnsweredOffShapes))]
    public void KeyFalseAnsweredOffIsSwitchedOnAndToldThisVersionTurnedItBackOn(string shape)
    {
        var disk = new Disk();
        disk.Save(AnsweredOff(shape));
        var s = disk.Reload();
        s.LastSeenVersion = "2.0.6";
        s.ApplyMigrations(hadFile: true);

        var decision = Launch(s, disk);
        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.SwitchedOn, decision);
        var saved = disk.Reload();
        Assert.True(saved.TelemetryEnabled);
        Assert.True(TelemetryHeartbeat.IsInstallId(saved.TelemetryInstallId));
        Assert.True(saved.TelemetryOnByDefaultApplied);
        Assert.True(saved.TelemetryFirstBeatHeld);
        Assert.Equal(TelemetryHeartbeat.NoteKind.Returning, TelemetryHeartbeat.NoteFor(decision, saved));
    }

    /// <summary>
    /// **Key false, already on: same id, no hold, the generic note once, the key written.**
    /// Its flag is true from the yes, so this is the committed negative for the cohort sentence
    /// AND for the held beat. Mutants that redden it: re-minting the id; holding the beat;
    /// gating the cohort sentence on the flag alone.
    /// </summary>
    [Fact]
    public void KeyFalseAlreadyOnKeepsItsIdHoldsNothingAndGetsTheGenericNoteOnce()
    {
        var disk = new Disk();
        var on = new AppSettings();
        TelemetryHeartbeat.OptIn(on);
        var id = on.TelemetryInstallId;
        disk.Save(on);
        var s = disk.Reload();

        var decision = Launch(s, disk);
        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.AlreadyOn, decision);
        Assert.Equal(2, disk.Saves);
        var saved = disk.Reload();
        Assert.Equal(id, saved.TelemetryInstallId);
        Assert.True(saved.TelemetryOnByDefaultApplied);
        Assert.False(saved.TelemetryFirstBeatHeld);
        Assert.Equal(TelemetryHeartbeat.NoteKind.Generic, TelemetryHeartbeat.NoteFor(decision, saved));
        Assert.Equal(T0, TelemetryHeartbeat.EpochFor(saved, T0));

        // The next launch: nothing written, no note.
        var next = disk.Reload();
        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.AlreadyApplied, Launch(next, disk));
        Assert.Equal(2, disk.Saves);
        Assert.Equal(TelemetryHeartbeat.NoteKind.None,
            TelemetryHeartbeat.NoteFor(TelemetryHeartbeat.SwitchOnDecision.AlreadyApplied, next));
    }

    /// <summary>
    /// **Key true and off — the toggle turned it off on v2.0.7 or later — is NEVER flipped**:
    /// not on relaunch, not after a version bump through the whole migration chain, and no
    /// note. This is the "off via Options stays off" half of order 2. Mutant that reddens it: a
    /// DecideSwitchOn that ignores the key.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeyTrueOffIsNeverFlippedAgain(bool viaDelete)
    {
        var disk = new Disk();
        disk.Save(new AppSettings());
        var s = disk.Reload();
        Launch(s, disk);
        s = disk.Reload();
        if (viaDelete) TelemetryHeartbeat.DeleteConfirmed(s);
        else Assert.True(TelemetryHeartbeat.SetEnabled(s, new TelemetrySchedule(), on: false, save: () => disk.Save(s)));
        disk.Save(s);

        for (var launch = 0; launch < 3; launch++)
        {
            s = disk.Reload();
            s.LastSeenVersion = "2.0.7";
            s.ApplyMigrations(hadFile: true);
            var saves = disk.Saves;
            var decision = Launch(s, disk);
            Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.AlreadyApplied, decision);
            Assert.Equal(saves, disk.Saves);
            Assert.False(s.TelemetryEnabled);
            Assert.Null(s.TelemetryInstallId);
            Assert.False(TelemetryHeartbeat.MaySend(s, productProfile: true, endpointConfigured: true));
            Assert.Equal(TelemetryHeartbeat.NoteKind.None, TelemetryHeartbeat.NoteFor(decision, s));
        }
    }

    /// <summary>Key true and on, the first beat sent: nothing written, same id, no note.</summary>
    [Fact]
    public void KeyTrueOnIsLeftAlone()
    {
        var disk = new Disk();
        disk.Save(new AppSettings());
        var s = disk.Reload();
        Launch(s, disk);
        s = disk.Reload();
        Assert.True(TelemetryHeartbeat.FirstBeatSent(s));
        disk.Save(s);
        var id = s.TelemetryInstallId;

        s = disk.Reload();
        var saves = disk.Saves;
        var decision = Launch(s, disk);
        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.AlreadyApplied, decision);
        Assert.Equal(saves, disk.Saves);
        Assert.Equal(id, s.TelemetryInstallId);
        Assert.Equal(TelemetryHeartbeat.NoteKind.None, TelemetryHeartbeat.NoteFor(decision, s));
    }

    /// <summary>
    /// **An isolated profile is never switched on, whatever is on disk**: no write, no save,
    /// the key untouched, no note. Fail-closed, so a harness can never put a CI run on the
    /// public numbers. Mutant that reddens it: checking the key before the profile.
    /// </summary>
    [Theory]
    [InlineData("fresh")]
    [InlineData("notNow")]
    [InlineData("on")]
    public void AnIsolatedProfileIsNeverSwitchedOnAndTheKeyIsUntouched(string shape)
    {
        var s = shape switch
        {
            "notNow" => AnsweredOff("notNow"),
            "on" => new AppSettings(),
            _ => new AppSettings(),
        };
        if (shape == "on") TelemetryHeartbeat.OptIn(s);
        var before = JsonSerializer.Serialize(s, Json);
        var disk = new Disk();

        var decision = Launch(s, disk, productProfile: false);
        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.NotTheProductProfile, decision);
        Assert.Equal(0, disk.Saves);
        Assert.Equal(before, JsonSerializer.Serialize(s, Json));
        Assert.Equal(TelemetryHeartbeat.NoteKind.None, TelemetryHeartbeat.NoteFor(decision, s));
    }

    /// <summary>
    /// **A build with no endpoint spends nothing**: no switch-on, the key stays false, no
    /// note — and the build that has one does switch it on. Mutant that reddens it: writing the
    /// key on NoEndpoint.
    /// </summary>
    [Fact]
    public void NoEndpointKeepsTheOneSwitchOnForABuildThatCanSend()
    {
        var disk = new Disk();
        disk.Save(AnsweredOff("notNow"));
        var s = disk.Reload();
        var decision = Launch(s, disk, endpointConfigured: false);
        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.NoEndpoint, decision);
        Assert.Equal(1, disk.Saves);
        Assert.False(s.TelemetryOnByDefaultApplied);
        Assert.False(s.TelemetryEnabled);
        Assert.Equal(TelemetryHeartbeat.NoteKind.None, TelemetryHeartbeat.NoteFor(decision, s));

        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.SwitchedOn, Launch(s, disk));
    }

    /// <summary>An "on" with no usable id sends nothing today, so it counts as off and is
    /// switched on with a fresh id rather than being left on and silent.</summary>
    [Fact]
    public void AnEnabledFlagWithNoUsableIdIsSwitchedOnWithAFreshId()
    {
        var s = new AppSettings { TelemetryEnabled = true, TelemetryInstallId = "not-an-id" };
        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.SwitchedOn, Launch(s, new Disk()));
        Assert.True(TelemetryHeartbeat.IsInstallId(s.TelemetryInstallId));
    }

    // ==================================== DRA-2010 §1 protection 1: the held beat ====

    /// <summary>
    /// **A relaunch while the first beat is held does not beat at the epoch, and still shows
    /// the note** (Challenger condition DRA-2011). Each launch reloads the disk and arms a fresh
    /// schedule from <see cref="TelemetryHeartbeat.EpochFor"/>, exactly as the runtime does.
    /// Mutants that redden it: the hold kept in memory only; EpochFor ignoring it; a note that
    /// shows only on the switch-on launch.
    /// </summary>
    [Theory]
    [InlineData("fresh", TelemetryHeartbeat.NoteKind.Generic)]
    [InlineData("notNow", TelemetryHeartbeat.NoteKind.Returning)]
    [InlineData("toggleOff", TelemetryHeartbeat.NoteKind.Returning)]
    public void ARelaunchWhileTheFirstBeatIsHeldDoesNotBeatAtTheEpochAndStillShowsTheNote(
        string shape, TelemetryHeartbeat.NoteKind note)
    {
        var disk = new Disk();
        disk.Save(shape == "fresh" ? new AppSettings() : AnsweredOff(shape));
        var s = disk.Reload();
        Launch(s, disk);

        for (var relaunch = 0; relaunch < 3; relaunch++)
        {
            s = disk.Reload();
            var launch = T0.AddHours(relaunch);
            var decision = Launch(s, disk);
            Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.AlreadyApplied, decision);
            Assert.Equal(note, TelemetryHeartbeat.NoteFor(decision, s));

            var clock = new TelemetrySchedule();
            clock.Arm(TelemetryHeartbeat.EpochFor(s, launch));
            Assert.False(clock.IsDue(launch), "a held first beat may not go at the launch");
            Assert.False(clock.IsDue(launch + TelemetrySchedule.Interval - TimeSpan.FromSeconds(1)));
            Assert.True(clock.IsDue(launch + TelemetrySchedule.Interval));
        }
    }

    /// <summary>
    /// **The hold clears on the first ACCEPTED beat, and only then**: after it, the next launch
    /// beats at its epoch again (DRA-381) and shows no note. <see cref="TelemetryHeartbeat.FirstBeatSent"/>
    /// answers true exactly once, so the runtime saves only on the beat that released it.
    /// </summary>
    [Fact]
    public void TheHoldClearsOnTheFirstSentBeatAndTheNextLaunchIsImmediateWithNoNote()
    {
        var disk = new Disk();
        disk.Save(AnsweredOff("notNow"));
        var s = disk.Reload();
        Launch(s, disk);
        s = disk.Reload();

        Assert.True(TelemetryHeartbeat.FirstBeatSent(s));
        Assert.False(TelemetryHeartbeat.FirstBeatSent(s));
        disk.Save(s);

        s = disk.Reload();
        var decision = Launch(s, disk);
        Assert.Equal(TelemetryHeartbeat.NoteKind.None, TelemetryHeartbeat.NoteFor(decision, s));
        Assert.Equal(T0, TelemetryHeartbeat.EpochFor(s, T0));
        Assert.True(s.TelemetryEnabled);
    }

    /// <summary>
    /// **The runtime clears the hold on <c>result.Ok</c> and nowhere else** (Helm's SIGN, item
    /// 2): never on <c>Started()</c>, never on a failed beat — a beat that did not go must keep
    /// the note up and the next launch waiting. The runtime is WPF and has no test project, so
    /// this reads its source: exactly one call, inside the success branch.
    /// </summary>
    [Fact]
    public void TheRuntimeReleasesTheHoldOnlyOnAnAcceptedBeat()
    {
        var src = File.ReadAllText(Path.Combine(Repo, "src", "EQBuddy", "TelemetryRuntime.cs"));
        Assert.Equal(1, Regex.Matches(src, @"TelemetryHeartbeat\.FirstBeatSent\(").Count);
        var ok = src.IndexOf("if (result.Ok)", StringComparison.Ordinal);
        var failed = src.IndexOf("_schedule.Failed(", StringComparison.Ordinal);
        var call = src.IndexOf("TelemetryHeartbeat.FirstBeatSent(", StringComparison.Ordinal);
        Assert.True(ok >= 0 && failed > ok, "the success/failure branches were not found");
        Assert.True(call > ok && call < failed, "FirstBeatSent must sit inside the result.Ok branch");
        // And the epoch the runtime arms comes from the policy, not from the clock alone.
        Assert.Contains("TelemetryHeartbeat.EpochFor(_settings,", src, StringComparison.Ordinal);
    }

    /// <summary>
    /// **Turning it off while held drops the hold, and the off is final**: the next launch
    /// does not switch it back on, shows no note and holds nothing. Mutant that reddens it:
    /// OptOut leaving the hold set (the note would come back on an off profile).
    /// </summary>
    [Fact]
    public void TurningItOffWhileHeldDropsTheHoldAndStaysOff()
    {
        var disk = new Disk();
        disk.Save(AnsweredOff("notNow"));
        var s = disk.Reload();
        Launch(s, disk);
        s = disk.Reload();
        Assert.True(s.TelemetryFirstBeatHeld);

        var clock = new TelemetrySchedule();
        clock.Arm(TelemetryHeartbeat.EpochFor(s, T0));
        Assert.True(TelemetryHeartbeat.SetEnabled(s, clock, on: false, save: () => disk.Save(s)));
        Assert.False(s.TelemetryFirstBeatHeld);
        Assert.Null(clock.NextDue);

        s = disk.Reload();
        var decision = Launch(s, disk);
        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.AlreadyApplied, decision);
        Assert.False(s.TelemetryEnabled);
        Assert.Equal(TelemetryHeartbeat.NoteKind.None, TelemetryHeartbeat.NoteFor(decision, s));
    }

    /// <summary>Turning it back ON later is a fresh consent: a new id and DRA-381's immediate
    /// beat, never a held one.</summary>
    [Fact]
    public void TurningItBackOnLaterBeatsImmediately()
    {
        var s = AnsweredOff("toggleOff");
        s.TelemetryOnByDefaultApplied = true;
        Assert.True(TelemetryHeartbeat.SetEnabled(s, new TelemetrySchedule(), on: true, save: () => { }));
        Assert.False(s.TelemetryFirstBeatHeld);
        Assert.Equal(T0, TelemetryHeartbeat.EpochFor(s, T0));
    }

    /// <summary>
    /// **Clicking an already-off toggle writes NOTHING** (Helm's SIGN, item 3): no save and not
    /// one byte of the settings moves, the new keys included — so a click can never record an
    /// off that was not a change. Pinned on a key-true off profile, the one a click lands on.
    /// </summary>
    [Fact]
    public void ClickingAnAlreadyOffToggleWritesNothing()
    {
        var s = AnsweredOff("toggleOff");
        s.TelemetryOnByDefaultApplied = true;
        var before = JsonSerializer.Serialize(s, Json);
        var saves = 0;
        Assert.False(TelemetryHeartbeat.SetEnabled(s, new TelemetrySchedule(), on: false, save: () => saves++));
        Assert.Equal(0, saves);
        Assert.Equal(before, JsonSerializer.Serialize(s, Json));
    }

    // ==================================================== the Options toggle ====
    /// <summary>
    /// **Turning the toggle OFF stops sends before the flag write returns** (plan §2): at the
    /// moment <c>save</c> runs, the schedule is already disarmed, nothing is due at any time,
    /// <see cref="TelemetryHeartbeat.MaySend"/> answers false, and a beat that was IN FLIGHT
    /// cannot re-arm when it lands. Mutant that reddens it: disarming after the save.
    /// </summary>
    [Fact]
    public void TurningTheToggleOffDisarmsBeforeTheWriteReturnsAndAnInFlightBeatCannotReArm()
    {
        var s = new AppSettings();
        TelemetryHeartbeat.OptIn(s);
        var clock = new TelemetrySchedule();
        clock.Arm(T0);
        clock.Started();
        clock.Succeeded(T0, stillArmed: true);
        Assert.NotNull(clock.NextDue);

        DateTime? dueAtSave = T0;
        bool? maySendAtSave = null;
        var saves = 0;
        Assert.True(TelemetryHeartbeat.SetEnabled(s, clock, on: false, save: () =>
        {
            saves++;
            dueAtSave = clock.NextDue;
            maySendAtSave = TelemetryHeartbeat.MaySend(s, productProfile: true, endpointConfigured: true);
        }));
        Assert.Equal(1, saves);
        Assert.Null(dueAtSave);
        Assert.False(maySendAtSave);
        Assert.False(clock.IsDue(T0.AddDays(1)));
        Assert.Null(s.TelemetryInstallId);

        // A beat in flight when the switch went off lands afterwards and arms nothing.
        var inFlight = new AppSettings();
        TelemetryHeartbeat.OptIn(inFlight);
        var flying = new TelemetrySchedule();
        flying.Arm(T0);
        flying.Started();
        TelemetryHeartbeat.SetEnabled(inFlight, flying, on: false, save: () => { });
        flying.Succeeded(T0.AddSeconds(3),
            stillArmed: TelemetryHeartbeat.MaySend(inFlight, productProfile: true, endpointConfigured: true));
        Assert.Null(flying.NextDue);

        // A switch that did not move writes nothing.
        Assert.False(TelemetryHeartbeat.SetEnabled(s, clock, on: false, save: () => saves++));
        Assert.Equal(1, saves);
    }

    /// <summary>Turning the toggle ON writes OptIn before the save; arming is the caller's.</summary>
    [Fact]
    public void TurningTheToggleOnMintsAFreshIdInTheSave()
    {
        var s = new AppSettings();
        s.TelemetryPromptShown = true;
        string? idAtSave = null;
        Assert.True(TelemetryHeartbeat.SetEnabled(s, new TelemetrySchedule(), on: true,
            save: () => idAtSave = s.TelemetryInstallId));
        Assert.True(TelemetryHeartbeat.IsInstallId(idAtSave));
        Assert.True(s.TelemetryEnabled);
    }

    // ======================================================= the shipped build ====

    /// <summary>
    /// **The literal is the deployed host, and nothing looser** (DRA-369). HTTPS, the
    /// workers.dev name Helm kept, no trailing slash and no path — the two paths are appended
    /// to it, so a trailing slash would send to <c>//heartbeat</c>. A changed host has to change
    /// this line too, which is the point: it is the one place a player's data can go.
    /// </summary>
    [Fact]
    public void TheShippedEndpointIsTheDeployedHost()
    {
        Assert.Equal("https://eqbuddy-telemetry.eqbuddy-telemetry.workers.dev", TelemetrySender.BaseUrl);
        var uri = new Uri(TelemetrySender.BaseUrl);
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        Assert.Equal("/", uri.AbsolutePath);
        Assert.False(TelemetrySender.BaseUrl.EndsWith('/'));
        Assert.True(TelemetrySender.IsConfigured);
    }

    /// <summary>
    /// **With the shipped endpoint, a fresh product profile is switched on exactly once** (DRA-2010).
    /// The policy tests above hand <c>endpointConfigured</c> in as a literal; this one hands in
    /// the build's own answer. The disk default never moved — it is OFF until the switch-on —
    /// and ON, with one id that survives the relaunch, after.
    /// </summary>
    [Fact]
    public void WithTheShippedEndpointAFreshProductProfileIsSwitchedOnOnce()
    {
        var disk = new Disk();
        var s = new AppSettings();
        Assert.False(s.TelemetryEnabled);
        Assert.False(TelemetryHeartbeat.MaySend(s, productProfile: true, TelemetrySender.IsConfigured));

        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.SwitchedOn,
            Launch(s, disk, endpointConfigured: TelemetrySender.IsConfigured));
        s = disk.Reload();
        var id = s.TelemetryInstallId;
        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.AlreadyApplied,
            Launch(s, disk, endpointConfigured: TelemetrySender.IsConfigured));
        Assert.Equal(1, disk.Saves);
        Assert.True(TelemetryHeartbeat.IsInstallId(id));
        Assert.Equal(id, s.TelemetryInstallId);
        Assert.True(TelemetryHeartbeat.MaySend(s, productProfile: true, TelemetrySender.IsConfigured));
    }

    /// <summary>
    /// **A live endpoint changes nothing for an isolated profile** — the E2E suite, shoot.ps1
    /// and this suite are never switched on, and a staged ON profile still may not send. And
    /// this very process IS isolated (<c>TestProfileIsolation</c>), so no test here can put a
    /// heartbeat on the public numbers through the product path.
    /// </summary>
    [Fact]
    public void WithTheShippedEndpointAnIsolatedProfileIsNeverSwitchedOnAndNeverSends()
    {
        Assert.False(AppPaths.IsProductOwnedProfile, "the test process must run on an isolated profile");

        var s = new AppSettings();
        Assert.Equal(TelemetryHeartbeat.SwitchOnDecision.NotTheProductProfile, TelemetryHeartbeat.DecideSwitchOn(
            s, AppPaths.IsProductOwnedProfile, TelemetrySender.IsConfigured));

        TelemetryHeartbeat.OptIn(s);
        Assert.True(TelemetryHeartbeat.IsInstallId(s.TelemetryInstallId));
        Assert.False(TelemetryHeartbeat.MaySend(s, AppPaths.IsProductOwnedProfile, TelemetrySender.IsConfigured));
    }
    // ================================================================ cadence ====

    private static readonly DateTime T0 = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheFirstBeatIsDueAtTheEpochThenEveryFive_BothArmPaths()
    {
        // DRA-381: no dwell — the first beat is IMMEDIATE, due at the arm time.
        // Cover both arm paths: launch-with-consent and mid-session opt-in,
        // since Arm() is the same call the runtime makes for each (ruling 5:11 PM).
        var launch = new TelemetrySchedule();
        Assert.False(launch.IsDue(T0));           // disarmed: never due
        launch.Arm(T0);
        Assert.True(launch.IsDue(T0));            // due AT the epoch — not delayed
        launch.Started();
        Assert.False(launch.IsDue(T0.AddSeconds(60))); // in flight: never doubled
        launch.Succeeded(T0, stillArmed: true);
        Assert.Equal(T0.AddMinutes(5), launch.NextDue); // interval unchanged

        var optIn = new TelemetrySchedule();
        optIn.Arm(T0);                            // epoch = moment of consent
        Assert.True(optIn.IsDue(T0));             // due AT the epoch — not delayed
    }

    /// <summary>A failed beat is dropped; the NEXT waits 10, 20, 40, then 60 min forever,
    /// and the first success resets it to 5.</summary>
    [Fact]
    public void FailuresBackOffTheNextTickAndCapAtAnHour()
    {
        var clock = new TelemetrySchedule();
        clock.Arm(T0);
        var now = T0.AddMinutes(2);
        var waits = new List<double>();
        for (var i = 0; i < 6; i++)
        {
            clock.Started();
            clock.Failed(now, stillArmed: true);
            waits.Add((clock.NextDue!.Value - now).TotalMinutes);
            now = clock.NextDue.Value;
        }
        Assert.Equal([10, 20, 40, 60, 60, 60], waits);

        clock.Started();
        clock.Succeeded(now, stillArmed: true);
        Assert.Equal(5, (clock.NextDue!.Value - now).TotalMinutes);
        Assert.Equal(0, clock.ConsecutiveFailures);
    }

    [Fact]
    public void AnOptOutWhileInFlightArmsNothing()
    {
        var clock = new TelemetrySchedule();
        clock.Arm(T0);
        clock.Started();
        clock.Succeeded(T0.AddMinutes(2), stillArmed: false);
        Assert.Null(clock.NextDue);
        Assert.False(clock.IsDue(T0.AddDays(1)));
    }

    // ================================================ the "last heartbeat" line ====

    /// <summary>The five forms, gap-free at every boundary (§8.3.1 row 9).</summary>
    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 min ago")]
    [InlineData(59 * 60 + 59, "59 min ago")]
    [InlineData(3600, "1 hr ago")]
    [InlineData(23 * 3600 + 3599, "23 hr ago")]
    [InlineData(24 * 3600, "yesterday")]
    [InlineData(47 * 3600 + 3599, "yesterday")]
    [InlineData(48 * 3600, "2 days ago")]
    [InlineData(11 * 86400 + 86399, "11 days ago")]
    [InlineData(-30, "just now")]
    public void RelativeTimeIsTheClosedSet(int seconds, string expected) =>
        Assert.Equal(expected, TelemetryCopy.RelativeTime(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void TheStatusLineIsOneOfTheRuledStrings()
    {
        var now = T0;
        Assert.Null(TelemetryCopy.StatusLine(false, TelemetryCopy.LastEvent.None, null, now));
        Assert.Null(TelemetryCopy.StatusLine(false, TelemetryCopy.LastEvent.SendFailed, null, now));
        Assert.Equal("Deleted. Your id is gone. No more heartbeats from this computer.",
            TelemetryCopy.StatusLine(false, TelemetryCopy.LastEvent.Deleted, null, now));
        Assert.Equal("On — no heartbeats sent yet",
            TelemetryCopy.StatusLine(true, TelemetryCopy.LastEvent.None, null, now));
        Assert.Equal("On — last heartbeat: 4 min ago",
            TelemetryCopy.StatusLine(true, TelemetryCopy.LastEvent.Sent, now.AddMinutes(-4), now));
        Assert.Equal("On — last send failed, will try again",
            TelemetryCopy.StatusLine(true, TelemetryCopy.LastEvent.SendFailed, now.AddMinutes(-4), now));
        Assert.Equal("On — delete did not reach the server, try again",
            TelemetryCopy.StatusLine(true, TelemetryCopy.LastEvent.DeleteFailed, null, now));
    }

    /// <summary>The width reservation must cover every string the line can take (trap 12).</summary>
    [Fact]
    public void TheWidthSamplesIncludeEveryFixedString()
    {
        foreach (var fixedString in new[]
                 {
                     TelemetryCopy.StatusNoneYet, TelemetryCopy.StatusSendFailed,
                     TelemetryCopy.StatusDeleteFailed, TelemetryCopy.Deleted,
                 })
            Assert.Contains(fixedString, TelemetryCopy.StatusWidthSamples);
    }
}
