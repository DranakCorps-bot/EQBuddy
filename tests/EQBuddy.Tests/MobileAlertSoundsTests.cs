using System.Text.Json;
using System.Text.RegularExpressions;
using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Tests;

/// <summary>
/// EQBuddy Mobile's alert audio (#208, sbaum23), from the switch to the wire to the page.
///
/// **The report this answers.** sbaum23 is on PopOS/Cosmic, where the compositor places
/// every new window at the cursor and refuses to move one that is already mapped — so the
/// overlay chips and Watch alerts land on the monitor the game is on, whatever he saves in
/// Options, and no code of ours can overrule it. He tried EQBuddy Mobile on his second
/// monitor and it worked, then named the one thing missing, verbatim:
/// *"As long as I can turn off alerts/chip in EQBuddy but leave them on in mobile (with
/// the sounds for alerts happening in the browser) I think that will work for my use
/// case."* The reply on 2026-08-22 said plainly that the page plays no sound at all today.
/// This is that half.
///
/// **The presentation is Bevel's, Helm-signed 2026-09-04**, and it is a narrow cut on
/// purpose: one master switch, default Off, gating Mobile audio and nothing else. Per-event
/// pickers, a volume slider and a sample-on-toggle are all explicitly out — so the
/// assertions below are as much about what does NOT exist as about what does.
/// </summary>
/// <remarks>In the serial collection because constructing a <see cref="CompanionHost"/>
/// can write the shared profile's settings.json (its quest-gate migration saves), and a
/// test that writes that file in parallel with one asserting on it is the 2026-08-22
/// flake.</remarks>
[Collection(SettingsFileCollection.Name)]
public class MobileAlertSoundsTests
{
    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    // ---------------------------------------------------------------- the switch

    /// <summary>Default Off, and off is the state a fresh profile is in. Stated on its own
    /// because every other assertion here is only interesting against that default —
    /// "opt-in" is the entire product decision, not an implementation detail.</summary>
    [Fact]
    public void MobileSoundsAreOffOnAFreshProfile()
    {
        var settings = new AppSettings();

        Assert.False(settings.CompanionSounds);
        Assert.False(MobileAlertSounds.ShouldCue(settings));
    }

    /// <summary>Both halves are required, and neither stands in for the other. Turning
    /// EQBuddy Mobile off must silence the phone without the owner also having to find this
    /// switch; turning this switch off must silence it without unpairing the device.</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]   // paired, switch off — the default, and the point
    [InlineData(false, true, false)]   // switch on, no server to carry it
    [InlineData(true, true, true)]
    public void ACueNeedsTheListenerAndTheSwitch(bool listener, bool sounds, bool expected)
    {
        Assert.Equal(expected, MobileAlertSounds.ShouldCue(listener, sounds));
        Assert.Equal(expected, MobileAlertSounds.ShouldCue(
            new AppSettings { CompanionEnabled = listener, CompanionSounds = sounds }));
    }

    /// <summary>The helper line, exactly as Bevel locked it. Pinned as a literal rather
    /// than compared to itself: the words ARE the ruling, and a "clarifying" rewrite is the
    /// change this test exists to make somebody defend.</summary>
    [Fact]
    public void TheOptionsCopyIsTheWordsBevelSigned()
    {
        Assert.Equal("Mobile sounds", MobileAlertSounds.Label);
        Assert.Equal("Off until you turn it on — phone stays quiet when alerts fire.",
            MobileAlertSounds.HelperText);
        // And the scope line says what it does NOT take over. A switch that silently
        // adopted the desktop's alert sounds would be the #228 class — a capability
        // removed by a change that reads as an addition.
        Assert.Contains("Only EQBuddy Mobile", MobileAlertSounds.ScopeNote);
    }

    // ---------------------------------------------------------------- the wire

    private static CompanionSnapshot Snap(bool enabled, long seq) =>
        CompanionProjection.Build(new CompanionInputs
        {
            Character = "Xyrid",
            AppVersion = "1.99.18",
            Offered = [CompanionSurfaces.Session],
            Alerts = new CompanionAlertsSection(enabled, seq),
        }, new DateTime(2026, 9, 4, 13, 0, 0));

    private static string Envelope(CompanionSnapshot snap) =>
        CompanionProjection.SectionFingerprints(snap)[CompanionProjection.EnvelopeSection];

    /// <summary>The cue rides the ENVELOPE, which wakes every connected device whatever it
    /// subscribed to. That is the requirement, not an implementation choice: a tablet
    /// showing only the zone map is exactly the device that needs to hear a camp pop, and a
    /// surface-scoped cue would reach nobody who was not already looking at the right
    /// screen.</summary>
    [Fact]
    public void AFiredAlertMovesTheEnvelopeFingerprint()
    {
        Assert.NotEqual(Envelope(Snap(true, 4)), Envelope(Snap(true, 5)));
        // Flipping the switch moves it too — a phone that has just been silenced needs to
        // be told, or its ⚙ panel goes on claiming sounds are on until something else
        // happens to change (trap 8's mirror: excluding a STEP change is its own bug).
        Assert.NotEqual(Envelope(Snap(true, 5)), Envelope(Snap(false, 5)));
    }

    /// <summary>And it does NOT move on its own. The envelope is diffed every tick against
    /// every paired device; a value that drifts here wakes all of them once a second
    /// forever (trap 8). The count is a count, so two ticks with nothing happening are
    /// identical — including their timestamps, which is what would have gone wrong if this
    /// carried "when did it fire" instead.</summary>
    [Fact]
    public void AQuietTickDoesNotWakeAnybody()
    {
        var early = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Xyrid", AppVersion = "1.99.18",
            Offered = [CompanionSurfaces.Session],
            Alerts = new CompanionAlertsSection(true, 5),
        }, new DateTime(2026, 9, 4, 13, 0, 0));
        var later = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Xyrid", AppVersion = "1.99.18",
            Offered = [CompanionSurfaces.Session],
            Alerts = new CompanionAlertsSection(true, 5),
        }, new DateTime(2026, 9, 4, 13, 47, 11));   // three quarters of an hour later

        Assert.Equal(Envelope(early), Envelope(later));
    }

    /// <summary>The keys the page actually reads, asserted against the JSON the server
    /// really emits (<see cref="CompanionWireKeyTests"/>'s lesson: the property name IS the
    /// wire key, and a camelCase surprise reaches a phone rather than a compiler). Each
    /// carries its negative, because "contains seq" would pass on `alertSeq` too.</summary>
    [Fact]
    public void ThePageReadsTheKeysTheServerEmits()
    {
        var json = JsonSerializer.Serialize(Snap(true, 7), CompanionSnapshot.JsonOpts);

        Assert.Contains("\"alerts\":", json);
        Assert.Contains("\"soundEnabled\":true", json);
        Assert.Contains("\"seq\":7", json);
        Assert.DoesNotContain("\"SoundEnabled\"", json);
        Assert.DoesNotContain("\"alertSeq\"", json);
    }

    /// <summary>The switch is sent even when it is OFF. A page told nothing can only look
    /// broken, and "why is my phone silent" is the exact question the ⚙ panel's one line
    /// exists to answer — <c>WhenWritingNull</c> would have dropped the section entirely if
    /// this had been modelled as a nullable flag.</summary>
    [Fact]
    public void ASilencedPhoneIsToldItIsSilenced()
    {
        var json = JsonSerializer.Serialize(Snap(false, 0), CompanionSnapshot.JsonOpts);

        Assert.Contains("\"soundEnabled\":false", json);
    }

    // ---------------------------------------------------------------- the host

    /// <summary>The host mints a cue only when the switch says so. Asserted through the
    /// public surface both widgets call, because "the widget checks first" is exactly the
    /// arrangement trap 47 says will eventually disagree with itself in one of the three
    /// places that ask.</summary>
    [Fact]
    public void TheHostRefusesToMintACueTheOwnerDidNotAskFor()
    {
        var settings = new AppSettings { CompanionEnabled = false, CompanionSounds = false };
        using var host = new CompanionHost(settings, "1.99.18");

        host.RaiseAlert();
        host.RaiseAlert();

        // Nothing to observe on the wire without a paired device, so the assertion is the
        // one thing that IS observable: the call is safe, silent and does not start a
        // server the owner never enabled.
        Assert.False(host.Running);
    }

    // ---------------------------------------------------------------- the wiring

    /// <summary>
    /// THE HALF NO UNIT TEST COULD OTHERWISE SEE: every place a widget plays an alert sound
    /// also tells the phone.
    ///
    /// A source scan, for <see cref="CompanionSnapshotArgumentTests"/>'s reason — the
    /// widgets have no unit tests at all (docs/TestPlan.md §5) and this feature lives
    /// entirely in which call sites remembered. Both lanes are checked in one loop: a fix
    /// on Windows alone is how #122 and #152 reached Linux three releases later.
    ///
    /// It is written as trap 34's shape — a curated must-list rather than a ban — because
    /// the failure mode here is an ABSENCE. "No call site may play a sound without cueing"
    /// cannot see a third alert site that does neither, and a phone that never makes a noise
    /// is indistinguishable from a phone whose owner left the switch off.
    /// </summary>
    [Fact]
    public void EveryAlertThatMakesANoiseOnThePcMakesOneOnThePhone()
    {
        foreach (var relative in new[]
        {
            Path.Combine("EQBuddy", "MainWindow.xaml.cs"),
            Path.Combine("EQBuddy.Avalonia", "MainWindow.cs"),
        })
        {
            var path = Path.Combine(Src, relative);
            Assert.True(File.Exists(path), $"{relative} moved — update this test's paths.");
            var lines = File.ReadAllLines(path);

            var plays = lines
                .Select((line, i) => (Line: line, Number: i + 1))
                .Where(l => l.Line.Contains("PlayAlertSound(")
                         && !l.Line.Contains("void PlayAlertSound")
                         && !l.Line.Contains("internal void PlayAlertSound()"))
                .ToList();

            Assert.True(plays.Count >= 2,
                $"{relative}: expected the spawn-due and watch-rule alert sites to still be "
                + "here; found " + plays.Count + ". If an alert site moved, move its "
                + "_companion.RaiseAlert() with it — the phone is a surface, not a mirror.");

            foreach (var (line, number) in plays)
            {
                // The cue may sit on the same line (the WPF widget is one line under its
                // hotspot ratchet) or on either side of the play, so the window is the
                // statement's neighbourhood rather than the line alone.
                var near = string.Join('\n', lines
                    .Skip(Math.Max(0, number - 4)).Take(7));
                Assert.True(near.Contains("RaiseAlert("),
                    $"{relative}:{number} plays an alert sound on the PC and never tells "
                    + "EQBuddy Mobile, so a paired phone stays silent for that alert while "
                    + "making a noise for the others — the half-working state #208 is about. "
                    + "Add _companion.RaiseAlert() beside it; the switch is checked inside "
                    + "the host (UI.Shared/MobileAlertSounds), not here.");
            }
        }
    }

    /// <summary>
    /// The page's end, scanned as source for the same reason the widgets are: there is no
    /// browser in this suite, and the shipped <c>index.html</c> is the only copy that
    /// reaches a phone.
    ///
    /// The three properties asserted are the three that would fail SILENTLY. A page that
    /// played on every payload would beep once a second; a page that played on its first
    /// payload would replay a camp that popped while the owner was in the kitchen; and a
    /// page that never called <c>resume()</c> would be permanently, invisibly muted by the
    /// browser's autoplay policy with everything else looking correct.
    /// </summary>
    [Fact]
    public void ThePageOnlyMakesANoiseWhenTheCountMoves()
    {
        var page = File.ReadAllText(Path.Combine(Src, "EQBuddy.Companion", "Web", "index.html"));

        Assert.Contains("alertAudio", page);
        Assert.Contains("msg.alerts", page);
        // Adopt-without-playing on the first payload and after a PC restart. Both are the
        // same rule: an alert is a DEADLINE, and one you hear late is worse than silence.
        Assert.Contains("seen === null || seq < seen", page);
        // The browser will not let a page make a sound until it has been touched, and no
        // setting on the PC can change that. Taken from any first touch of any kind — a
        // modal, a dedicated unlock button and a sample-on-toggle are all out of the cut.
        Assert.Contains("ctx.resume()", page);
        // Exactly one place in the page makes a noise, and it is downstream of the count
        // comparison above. A second call site is precisely how "beeps once a second"
        // gets reintroduced by someone fixing something else.
        Assert.Equal(1, Regex.Matches(page, @"\bchirp\(\);").Count);
    }

    /// <summary>What this cut deliberately does NOT ship. Written down because the pressure
    /// on a sound feature is always toward more knobs, and the next session will not have
    /// read the ruling — per-event pickers, a volume slider and a forced-On after pairing
    /// are each named as out of scope (Bevel, Helm-signed 2026-09-04).</summary>
    [Fact]
    public void ThereIsExactlyOneKnobAndNobodyIsForcedOntoIt()
    {
        var knobs = typeof(AppSettings).GetProperties()
            .Where(p => p.Name.StartsWith("CompanionSound", StringComparison.Ordinal)
                     || p.Name.Contains("MobileSound", StringComparison.Ordinal)
                     || p.Name.Contains("MobileVolume", StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(new[] { "CompanionSounds" }, knobs);

        // Pairing does not turn it on. The pairing window is where a first-run "would you
        // like sounds?" modal would naturally have gone, and that is the shape Bevel ruled
        // out — enabling EQBuddy Mobile leaves the switch exactly where the owner left it.
        var paired = new AppSettings { CompanionEnabled = true };
        Assert.False(paired.CompanionSounds);
        Assert.False(MobileAlertSounds.ShouldCue(paired));
    }
}
