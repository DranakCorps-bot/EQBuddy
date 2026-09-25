using System.Text.Json;
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

    /// <summary>Decline writes the shown flag and NOTHING else (TEL-001 as amended).</summary>
    [Fact]
    public void DeclineWritesOnlyTheShownFlag()
    {
        var s = new AppSettings();
        var before = JsonSerializer.Serialize(s, Json);
        TelemetryHeartbeat.Decline(s);
        Assert.True(s.TelemetryPromptShown);
        s.TelemetryPromptShown = false;
        Assert.Equal(before, JsonSerializer.Serialize(s, Json));
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

    // ======================================== §9 guard 4: the prompt fires ONCE ====

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

    /// <summary>
    /// **The prompt shows on a profile with the flag unset, then never again**: not on
    /// relaunch, not after decline, not after accept-then-opt-out, and not after a version bump
    /// — each "launch" reloads what the previous one SAVED, so this is the disk's memory being
    /// tested rather than an object's.
    /// </summary>
    [Fact]
    public void ThePromptShowsOnceAndNeverAgain()
    {
        var disk = new Disk();
        var s = new AppSettings();
        var asked = 0;

        var first = TelemetryHeartbeat.RunFirstOpen(s, productProfile: true,
            endpointConfigured: true, scriptedAnswer: null,
            ask: () => { asked++; return false; }, save: () => disk.Save(s));
        Assert.Equal("declined", first);
        Assert.Equal(1, asked);

        // Relaunch after decline.
        s = disk.Reload();
        Assert.Equal("alreadyShown", TelemetryHeartbeat.RunFirstOpen(s, true, true, null,
            ask: () => { asked++; return true; }, save: () => disk.Save(s)));

        // A version bump: the whole migration chain, as Load runs it, then the same question.
        s.LastSeenVersion = "1.99.0";
        s.ApplyMigrations(hadFile: true);
        Assert.Equal("alreadyShown", TelemetryHeartbeat.RunFirstOpen(s, true, true, null,
            ask: () => { asked++; return true; }, save: () => disk.Save(s)));

        // Opting in and out through the toggle does not re-arm it either.
        TelemetryHeartbeat.OptIn(s);
        TelemetryHeartbeat.OptOut(s);
        Assert.Equal(TelemetryHeartbeat.PromptDecision.AlreadyShown,
            TelemetryHeartbeat.DecidePrompt(s.TelemetryPromptShown, true, true, null));

        Assert.Equal(1, asked);
    }

    /// <summary>
    /// **The flag is SAVED before the window opens** — so a player who kills EQBuddy with the
    /// prompt up has declined, and is not asked again. The mutation this catches is "set the
    /// flag on answer instead of on show": the disk would hold an unset flag while asking, and
    /// the relaunch would ask again.
    /// </summary>
    [Fact]
    public void AKillDuringThePromptIsADecline()
    {
        var disk = new Disk();
        var s = new AppSettings();
        bool flagOnDiskWhileAsking = false;

        Assert.Throws<OperationCanceledException>(() => TelemetryHeartbeat.RunFirstOpen(s,
            true, true, null,
            ask: () =>
            {
                flagOnDiskWhileAsking = disk.LastSaved is not null && disk.Reload().TelemetryPromptShown;
                throw new OperationCanceledException("the player killed the app");
            },
            save: () => disk.Save(s)));

        Assert.True(flagOnDiskWhileAsking, "the shown flag must be SAVED before the prompt opens");
        var relaunched = disk.Reload();
        Assert.True(relaunched.TelemetryPromptShown);
        Assert.False(relaunched.TelemetryEnabled);
        Assert.Null(relaunched.TelemetryInstallId);
    }

    [Fact]
    public void AcceptWritesTheFlagTheSwitchAndAFreshIdTogether()
    {
        var disk = new Disk();
        var s = new AppSettings();
        Assert.Equal("accepted", TelemetryHeartbeat.RunFirstOpen(s, true, true, null,
            ask: () => true, save: () => disk.Save(s)));
        var saved = disk.Reload();
        Assert.True(saved.TelemetryPromptShown);
        Assert.True(saved.TelemetryEnabled);
        Assert.True(TelemetryHeartbeat.IsInstallId(saved.TelemetryInstallId));
    }

    /// <summary>The harness door: only on an isolated profile, and it can only DECLINE — there
    /// is no scripted accept, so no automated run can opt anything in.</summary>
    [Theory]
    [InlineData(false, "decline", "declined")]
    [InlineData(false, "DECLINE", "declined")]
    [InlineData(false, "accept", "notTheProductProfile")]
    [InlineData(false, null, "notTheProductProfile")]
    [InlineData(true, "decline", "shown")]
    public void TheScriptedAnswerIsHonouredOnlyOnAnIsolatedProfileAndOnlyAsADecline(
        bool productProfile, string? scripted, string expectedWord)
    {
        var s = new AppSettings();
        var asked = false;
        var word = TelemetryHeartbeat.RunFirstOpen(s, productProfile, endpointConfigured: true,
            scripted, ask: () => { asked = true; return false; }, save: () => { });
        Assert.Equal(expectedWord == "shown" ? "declined" : expectedWord, word);
        Assert.Equal(expectedWord == "shown", asked);
        Assert.False(s.TelemetryEnabled);
    }

    /// <summary>With no endpoint compiled in, the product profile is NOT asked — its one
    /// showing is not spent on a build that cannot send — and the flag stays unset so the
    /// build that has one does ask.</summary>
    [Fact]
    public void NoEndpointMeansNoPromptAndTheOneShowingIsKept()
    {
        var s = new AppSettings();
        var asked = false;
        Assert.Equal("noEndpoint", TelemetryHeartbeat.RunFirstOpen(s, true, endpointConfigured: false,
            null, ask: () => { asked = true; return true; }, save: () => { }));
        Assert.False(asked);
        Assert.False(s.TelemetryPromptShown);
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
    /// **With the shipped endpoint, a fresh product profile is asked exactly once, and is OFF
    /// until it says yes.** The policy tests above hand <c>endpointConfigured</c> in as a
    /// literal; this one hands in the build's own answer, so the day the host went live is the
    /// day the prompt started showing — and closing it leaves telemetry off with no id.
    /// </summary>
    [Fact]
    public void WithTheShippedEndpointAFreshProductProfileIsAskedOnceAndStaysOff()
    {
        var disk = new Disk();
        var s = new AppSettings();
        Assert.False(s.TelemetryEnabled);
        Assert.Null(s.TelemetryInstallId);
        Assert.False(TelemetryHeartbeat.MaySend(s, productProfile: true, TelemetrySender.IsConfigured));

        Assert.Equal(TelemetryHeartbeat.PromptDecision.Show, TelemetryHeartbeat.DecidePrompt(
            s.TelemetryPromptShown, productProfile: true, TelemetrySender.IsConfigured, null));

        var asked = 0;
        Assert.Equal("declined", TelemetryHeartbeat.RunFirstOpen(s, true, TelemetrySender.IsConfigured,
            null, ask: () => { asked++; return false; }, save: () => disk.Save(s)));
        s = disk.Reload();
        Assert.Equal("alreadyShown", TelemetryHeartbeat.RunFirstOpen(s, true, TelemetrySender.IsConfigured,
            null, ask: () => { asked++; return true; }, save: () => disk.Save(s)));
        Assert.Equal(1, asked);
        Assert.False(s.TelemetryEnabled);
        Assert.Null(s.TelemetryInstallId);
    }

    /// <summary>
    /// **A live endpoint changes nothing for an isolated profile** — the E2E suite, shoot.ps1
    /// and this suite never see the prompt, a scripted ACCEPT is still no answer at all, and a
    /// staged ON profile still may not send. And this very process IS isolated
    /// (<c>TestProfileIsolation</c>), so no test here can put a heartbeat on the public
    /// numbers through the product path.
    /// </summary>
    [Fact]
    public void WithTheShippedEndpointAnIsolatedProfileIsNeverAskedAndNeverSends()
    {
        Assert.False(AppPaths.IsProductOwnedProfile, "the test process must run on an isolated profile");

        var s = new AppSettings();
        Assert.Equal(TelemetryHeartbeat.PromptDecision.NotTheProductProfile, TelemetryHeartbeat.DecidePrompt(
            s.TelemetryPromptShown, AppPaths.IsProductOwnedProfile, TelemetrySender.IsConfigured, null));
        Assert.Equal(TelemetryHeartbeat.PromptDecision.NotTheProductProfile, TelemetryHeartbeat.DecidePrompt(
            s.TelemetryPromptShown, AppPaths.IsProductOwnedProfile, TelemetrySender.IsConfigured, "accept"));

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
