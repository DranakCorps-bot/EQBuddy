using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **SR-4's lift, guarded from the only side a test can reach it.**
///
/// The four alert blocks left `OptionsWindow.xaml(.cs)` for a host-neutral
/// `SettingsAlertsView`, so the Evolved shell's Settings room can compose the SAME controls
/// instead of growing a second copy that drifts against them. The WPF layer has no unit tests
/// (docs/TestPlan.md §5), so what CAN be asserted is the source: that every control really
/// arrived somewhere, that the one control which must NOT move did not, and that the tab
/// definition comes from Core rather than from four literals in a window.
///
/// **The enumeration below is trap 26 written as code.** "When you fold a surface, list every
/// control on it and say where each one went" — the same event that produced #204, #210 and
/// #212, every one of them a surface whose DATA survived a move while its write path did not.
/// A list in a commit message is read once; this one fails the build when a row stops being
/// true, and it is the same curated-must-list shape as `GameCommandsTests.SurfacesNeedingACommand`
/// and `DeadSettingTests.Known` (trap 34: forbidding the wrong thing cannot see a missing one).
/// </summary>
public class SettingsAlertsBlockTests
{
    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(Src, "EQBuddy", relative));

    /// <summary>
    /// Every control the two source tabs declared, and the token in the lifted view that
    /// proves it landed. A row asserts BOTH halves: the `x:Name` is gone from the window's
    /// XAML (so nothing is quietly declared twice) and the view builds the same control.
    ///
    /// Two rows deliberately point at a piece of TEXT rather than a field — the two buttons
    /// whose only identity is their caption. A row that cannot fail is not a guard (trap 39),
    /// so each token was checked against the pre-lift tree before it was written down.
    /// </summary>
    public static readonly (string WasNamed, string Went, string NowBuiltAs)[] Enumeration =
    [
        // ---- the shared sound/voice header: one block above the four, because "alert me at
        // this volume with this sound" is a cross-cutting default every rule overrides.
        ("SoundCombo", "shared header", "_soundCombo"),
        ("AlertVolumeSlider", "shared header", "_alertVolumeSlider"),
        ("AlertVolumeLabel", "shared header", "_alertVolumeLabel"),
        ("SoundFileNote", "shared header", "_soundFileNote"),
        ("VoiceCombo", "shared header", "_voiceCombo"),
        ("SpeechRateSlider", "shared header", "_speechRateSlider"),
        ("SpeechRateLabel", "shared header", "_speechRateLabel"),
        ("SpeechVolumeSlider", "shared header", "_speechVolumeSlider"),
        ("SpeechVolumeLabel", "shared header", "_speechVolumeLabel"),
        // The executor call the plan asked for, logged in DECISIONS.md: the slow alert is a
        // built-in, not a rule the player wrote, and the header's own helper line already
        // names it — so it sits with the voice that speaks it, not in the rules editor.
        ("SlowAlertCheck", "shared header", "_slowAlert"),
        ("SlowSpokenCheck", "shared header", "_slowSpoken"),
        ("SlowRaidOnlyCheck", "shared header", "_slowRaidOnly"),

        // ---- Buffs
        ("BuffExpiringOnlyCheck", "Buffs block", "_buffExpiringOnly"),
        ("BuffWarnBox", "Buffs block", "_buffWarnBox"),
        ("BuffSetCharNote", "Buffs block", "_buffSetCharNote"),
        ("BuffSetPanel", "Buffs block", "_buffSetPanel"),
        ("BuffSetClassBox", "Buffs block", "_buffSetClassBox"),
        ("BuffSetAddBox", "Buffs block", "_buffSetAddBox"),
        ("BuffSetPopup", "Buffs block", "_buffSetPopup"),
        ("BuffSetChrome", "Buffs block", "_buffSetChrome"),
        ("BuffSetMatches", "Buffs block", "_buffSetMatches"),

        // ---- Spawns
        ("TrackSpawnsCheck", "Spawns block", "_trackSpawns"),

        // ---- Crowd
        ("MezChipsCheck", "Crowd block", "_mezChips"),
        ("MezDurationsBlurb", "Crowd block", "new MezDurationsView("),
        ("MezDurationList", "Crowd block", "new MezDurationsView("),

        // ---- Watch: the rules editor, its examples panel, the log-line picker and import
        ("GuideToggle", "Watch block", "_guideToggle"),
        ("GuidePanel", "Watch block", "_guidePanel"),
        ("GuideContent", "Watch block", "_guideContent"),
        ("RulesPanel", "Watch block", "_rulesPanel"),
        ("AddRuleBtn", "Watch block", "+ Add watch rule"),
        ("AddFromLogBtn", "Watch block", "_addFromLogBtn"),
        ("RecentLinesPopup", "Watch block", "_recentLinesPopup"),
        ("RecentLinesChrome", "Watch block", "_recentLinesChrome"),
        ("RecentLinesHideChat", "Watch block", "_recentLinesHideChat"),
        ("RecentLinesList", "Watch block", "_recentLinesList"),
        ("ImportBox", "Watch block", "_importBox"),
        ("ImportBtn", "Watch block", "Import…"),
        ("ImportPreview", "Watch block", "_importPreview"),
        ("ImportConfirmBtn", "Watch block", "_importConfirmBtn"),
    ];

    public static TheoryData<string, string, string> EnumerationRows()
    {
        var rows = new TheoryData<string, string, string>();
        foreach (var (was, went, now) in Enumeration) rows.Add(was, went, now);
        return rows;
    }

    [Theory]
    [MemberData(nameof(EnumerationRows))]
    public void EveryLiftedControlLandedInItsBlockAndLeftTheWindow(string wasNamed, string went, string nowBuiltAs)
    {
        Assert.DoesNotContain($"x:Name=\"{wasNamed}\"", Read("OptionsWindow.xaml"), StringComparison.Ordinal);
        Assert.True(Read("SettingsAlertsView.cs").Contains(nowBuiltAs, StringComparison.Ordinal),
            $"{wasNamed} was lifted to the {went} and SettingsAlertsView.cs no longer builds it "
            + $"(looked for \"{nowBuiltAs}\"). A control that left one surface and arrived at "
            + "none is the shape of #204, #210 and #212 — the data survives the move and the "
            + "write path does not, and nothing else in this repo can see it.");
    }

    /// <summary>
    /// **`PinWatchChips` was the one control this lift did NOT move, and it is now RETIRED**
    /// (Surface A / SA-R, Helm #341). It was left behind on purpose: "Show watch chips in the
    /// mini dashboard" is PRESENCE, not what-fires, and it collided with the per-rule 📌 a few
    /// pixels below it — two switches converging on "does this chip show". The reconciliation
    /// asked for ONE, and the pin is the survivor, so the master left the v1 window rather
    /// than arriving here.
    ///
    /// **This test used to assert the row STAYED, and inverting it is the point.** A guard
    /// that says "the switch is still in the window" cannot tell a deliberate retirement from
    /// a lift that dropped a control on the floor — which is #204/#210/#212's shape and the
    /// thing the enumeration above exists to catch.
    ///
    /// The other half — that no file in `src` reads the setting except its declaration and
    /// its one-time translation — is asserted by `WatchPinMigrationTests`, which is where the
    /// retirement lives and which is already in the settings-file collection.
    /// </summary>
    [Fact]
    public void ThePresenceSwitchIsRetiredFromTheWatchTab()
    {
        // The DECLARATIONS, not the word: the comments those two files now carry name the
        // retired control on purpose, because a tombstone that cannot say what it buries is
        // not a tombstone (trap 2's precedent for recording a guard leaving with its subject).
        Assert.DoesNotContain("x:Name=\"PinChipsCheck\"", Read("OptionsWindow.xaml"), StringComparison.Ordinal);
        Assert.DoesNotContain("void OnPinChipsChanged", Read("OptionsWindow.xaml.cs"), StringComparison.Ordinal);

        var view = Read("SettingsAlertsView.cs");
        Assert.DoesNotContain("_vm.PinWatchChips", view, StringComparison.Ordinal);
        Assert.DoesNotContain("PinChipsCheck", view, StringComparison.Ordinal);
    }

    /// <summary>
    /// The window asks <see cref="AlertSurface"/> what the tabs are; it does not know. This is
    /// the first spend of a definition that has sat unused since before the pivot, and the
    /// reason the shell's Settings room cannot end up showing a different set of blocks, in a
    /// different order, from the window it replaces.
    /// </summary>
    [Fact]
    public void TheHostTakesItsOrderAndItsLabelsFromAlertSurface()
    {
        Assert.Contains("AlertSurface.Tabs(", Read("SettingsAlertsView.cs"), StringComparison.Ordinal);

        var host = Read("OptionsWindow.xaml.cs");
        Assert.Contains("_alerts.Tabs()", host, StringComparison.Ordinal);
        Assert.Contains("_alerts.Heading(", host, StringComparison.Ordinal);

        // The negative that keeps it honest: a host that spelled the labels itself would pass
        // everything above while quietly owning a second definition of the tab set.
        foreach (var tab in Enum.GetValues<AlertTab>())
            Assert.DoesNotContain($"\"{AlertSurface.LabelFor(tab)}\"", host, StringComparison.Ordinal);
    }

    /// <summary>
    /// The badges are REAL counts, not placeholders — rules written, buff buckets assembled,
    /// timers running. Crowd is null on purpose: three of these tabs configure a list and
    /// Crowd configures a switch plus a table of durations nobody is expected to fill in, so a
    /// "0" beside Crowd control would read as failure rather than as a default. (The record's
    /// own rule: null is "not applicable", 0 is "none yet, and that is actionable".)
    /// </summary>
    [Fact]
    public void TheCountsPassedToAlertSurfaceAreTheRealOnes()
    {
        var view = Read("SettingsAlertsView.cs");
        Assert.Contains("watch: _vm.Rules.Count", view, StringComparison.Ordinal);
        Assert.Contains("buffs: BuffSetBucketCount()", view, StringComparison.Ordinal);
        Assert.Contains("spawns: _main.SpawnTimers.Snapshot(DateTime.Now).Count", view, StringComparison.Ordinal);
        Assert.Contains("crowd: null", view, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trap 13 as a constructor contract: the block never loads settings for itself. Both hosts
    /// wrap the ONE <c>AppSettings</c> instance the widget holds, because a second snapshot
    /// would clobber the first one wholesale on its next save — which is exactly how "my
    /// tick-boxes won't stay ticked" (#169) presents, with nothing on screen to say so.
    /// </summary>
    [Fact]
    public void TheBlockNeverLoadsItsOwnSettings()
    {
        Assert.DoesNotContain("AppSettings.Load", Read("SettingsAlertsView.cs"), StringComparison.Ordinal);
    }
}
