using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **SR-1's lift, guarded from the only side a test can reach it.**
///
/// The Look and Behavior blocks left `OptionsWindow.xaml(.cs)` for host-neutral
/// `SettingsLookView` / `SettingsBehaviorView`, so the Evolved shell's Settings room can
/// compose the SAME controls instead of growing a second copy that drifts against them. The
/// WPF layer has no unit tests (docs/TestPlan.md §5), so what CAN be asserted is the source:
/// that every control really arrived somewhere, that the sentence which must NOT travel did
/// not, and that the host still routes the one thing a block cannot own.
///
/// **The enumeration below is trap 26 written as code**, the shape
/// <see cref="SettingsAlertsBlockTests"/> established one PR earlier. "When you fold a
/// surface, list every control on it and say where each one went" — the same event that
/// produced #204, #210 and #212, every one of them a surface whose DATA survived a move while
/// its write path did not. A list in a commit message is read once; this one fails the build
/// when a row stops being true.
/// </summary>
public class SettingsLookBehaviorBlockTests
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
    /// Some rows point at a piece of TEXT rather than a field — a control whose only identity
    /// is its caption. A row that cannot fail is not a guard (trap 39), so every token here
    /// was checked against the pre-lift tree before it was written down.
    /// </summary>
    public static readonly (string WasNamed, string File, string Went, string NowBuiltAs)[] Enumeration =
    [
        // ---- Look: the palette picker and its Custom rows
        ("ThemeCombo", "SettingsLookView.cs", "Look block", "_themeCombo"),
        ("CustomColorsPanel", "SettingsLookView.cs", "Look block", "_customColors"),
        // The swatch grid and the hex boxes travelled with the panel they fill.
        ("CustomColorsPanel", "SettingsLookView.cs", "Look block", "SwatchColors"),

        // ---- Look: the four sliders and their live values
        ("ScaleSlider", "SettingsLookView.cs", "Look block", "_scaleSlider"),
        ("ScaleLabel", "SettingsLookView.cs", "Look block", "_scaleLabel"),
        ("ChipScaleSlider", "SettingsLookView.cs", "Look block", "_chipScaleSlider"),
        ("ChipScaleLabel", "SettingsLookView.cs", "Look block", "_chipScaleLabel"),
        ("BgOpacitySlider", "SettingsLookView.cs", "Look block", "_bgOpacitySlider"),
        ("BgOpacityLabel", "SettingsLookView.cs", "Look block", "_bgOpacityLabel"),
        ("OpacitySlider", "SettingsLookView.cs", "Look block", "_opacitySlider"),
        ("OpacityLabel", "SettingsLookView.cs", "Look block", "_opacityLabel"),

        // ---- Look: the alignment grid and the cursor ring
        ("GridOverlayCheck", "SettingsLookView.cs", "Look block", "_gridOverlayCheck"),
        ("GridSpacingSlider", "SettingsLookView.cs", "Look block", "_gridSpacingSlider"),
        ("GridSpacingLabel", "SettingsLookView.cs", "Look block", "_gridSpacingLabel"),
        ("CursorRingCheck", "SettingsLookView.cs", "Look block", "_cursorRingCheck"),

        // ---- Behavior: EQBuddy Mobile. The pairing button moved WITH the block; the
        // title-bar 📱 button is untouched and is asserted separately below.
        ("SecondScreenBlock", "SettingsBehaviorView.cs", "Behavior block", "BuildSecondScreen()"),
        ("SecondScreenBtn", "SettingsBehaviorView.cs", "Behavior block", "EQBuddy Mobile…"),
        ("MobileSoundsCheck", "SettingsBehaviorView.cs", "Behavior block", "_mobileSounds"),
        ("MobileSoundsLabel", "SettingsBehaviorView.cs", "Behavior block", "MobileAlertSounds.Label"),
        ("MobileSoundsNote", "SettingsBehaviorView.cs", "Behavior block", "MobileAlertSounds.HelperText"),

        // ---- Behavior: the three hide-when rules and keep-above
        ("HideUnfocusedCheck", "SettingsBehaviorView.cs", "Behavior block", "_hideUnfocused"),
        ("HideNotRunningCheck", "SettingsBehaviorView.cs", "Behavior block", "_hideNotRunning"),
        ("HideAltTabCheck", "SettingsBehaviorView.cs", "Behavior block", "_hideAltTab"),
        ("HideAltTabNote", "SettingsBehaviorView.cs", "Behavior block", "AltTabPolicy.TaskbarWarning"),
        ("KeepAboveCheck", "SettingsBehaviorView.cs", "Behavior block", "_keepAbove"),

        // ---- Behavior: hotkeys, regen, log housekeeping, tutorial, perf
        ("HotkeysPanel", "SettingsBehaviorView.cs", "Behavior block", "_hotkeysPanel"),
        ("RegenPerTickBox", "SettingsBehaviorView.cs", "Behavior block", "_regenPerTickBox"),
        ("TruncateCheck", "SettingsBehaviorView.cs", "Behavior block", "_truncate"),
        ("ArchiveCheck", "SettingsBehaviorView.cs", "Behavior block", "_archive"),
        ("TutorialCheck", "SettingsBehaviorView.cs", "Behavior block", "_tutorial"),
        ("PerfStatsCheck", "SettingsBehaviorView.cs", "Behavior block", "_perfStats"),
    ];

    public static TheoryData<string, string, string, string> EnumerationRows()
    {
        var rows = new TheoryData<string, string, string, string>();
        foreach (var (was, file, went, now) in Enumeration) rows.Add(was, file, went, now);
        return rows;
    }

    [Theory]
    [MemberData(nameof(EnumerationRows))]
    public void EveryLiftedControlLandedInItsBlockAndLeftTheWindow(
        string wasNamed, string file, string went, string nowBuiltAs)
    {
        Assert.DoesNotContain($"x:Name=\"{wasNamed}\"", Read("OptionsWindow.xaml"), StringComparison.Ordinal);
        Assert.True(Read(file).Contains(nowBuiltAs, StringComparison.Ordinal),
            $"{wasNamed} was lifted to the {went} and {file} no longer builds it (looked for "
            + $"\"{nowBuiltAs}\"). A control that left one surface and arrived at none is the "
            + "shape of #204, #210 and #212 — the data survives the move and the write path "
            + "does not, and nothing else in this repo can see it.");
    }

    /// <summary>
    /// **The one sentence that did NOT lift.** "Drag either side edge to widen this window" is
    /// about `OptionsWindow`'s resize grips, which a shell room does not have — a block that
    /// carried it would tell half its hosts to do something impossible. Trap 37's lesson from
    /// the other end: when you lift a view out of a host, list what each part of that host was
    /// buying, and leave behind the parts that were buying the host's own chrome.
    /// </summary>
    [Fact]
    public void TheWindowKeepsTheSentenceThatIsAboutTheWindow()
    {
        const string chrome = "Drag either side edge to widen this window.";
        Assert.Contains(chrome, Read("OptionsWindow.xaml"), StringComparison.Ordinal);
        Assert.DoesNotContain(chrome, Read("SettingsLookView.cs"), StringComparison.Ordinal);

        // And the half that DID travel, so this row cannot be satisfied by the sentence
        // simply having been deleted.
        Assert.Contains("Size also scales all text.", Read("SettingsLookView.cs"), StringComparison.Ordinal);
    }

    /// <summary>
    /// **The title-bar 📱 button is untouched, and that is the point of the row.** CLAUDE.md's
    /// own carve-out makes it the standing SECOND door into EQBuddy Mobile — *"Settings live in
    /// Options — except EQBuddy Mobile, which David wanted as its own title-bar button"* — and
    /// a lift that quietly left Settings as the only way in would violate that rule by omission
    /// rather than by edit, which is precisely the failure trap 59 describes (an entrance that
    /// exists in the wiring and not for a player who has configured nothing).
    /// </summary>
    [Fact]
    public void ThePairingPanelMovedAndTheTitleBarDoorDidNot()
    {
        var widget = File.ReadAllText(Path.Combine(Src, "EQBuddy", "MainWindow.xaml"));
        Assert.Contains("x:Name=\"MobileBtn\"", widget, StringComparison.Ordinal);
        // A control that exists and is hidden is trap 29 exactly — the button shipped
        // `Visibility="Collapsed"` for six days after the gate that un-hid it was deleted,
        // and an absent control photographs as an unremarkable title bar.
        Assert.DoesNotContain("x:Name=\"MobileBtn\" Grid.Column=\"4\" Visibility=\"Collapsed\"",
            widget, StringComparison.Ordinal);

        // The block names that door in its own helper line, so a player reading Settings is
        // told where the other one is rather than having to find it.
        Assert.Contains("button in the title bar opens it any time",
            Read("SettingsBehaviorView.cs"), StringComparison.Ordinal);
    }

    /// <summary>
    /// The hotkey recorder is the one piece of this lift that a host still has to help with:
    /// rebuilding the rows on click destroys the button that had focus, so the press arrives at
    /// the WINDOW and never tunnels through the block's panel. The block owns the decision and
    /// the host owns the route — and a host that forgets to forward gets a recorder that
    /// silently never records, which no screenshot and no build can see.
    /// </summary>
    [Fact]
    public void TheHostRoutesAKeyPressToTheBlockThatOwnsTheDecision()
    {
        Assert.Contains("public bool HandleRecordingKey(", Read("SettingsBehaviorView.cs"),
            StringComparison.Ordinal);

        var host = Read("OptionsWindow.xaml.cs");
        Assert.Contains("_behavior?.HandleRecordingKey(e)", host, StringComparison.Ordinal);
        // The negative that keeps it honest: a window still parsing gestures itself would be a
        // second copy of the rule the block was created to hold.
        Assert.DoesNotContain("HotkeyManager.Parse(", host, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trap 13 as a constructor contract: neither block loads settings for itself. Both hosts
    /// wrap the ONE <c>AppSettings</c> instance the widget holds, because a second snapshot
    /// would clobber the first one wholesale on its next save — which is exactly how "my
    /// tick-boxes won't stay ticked" (#169) presents, with nothing on screen to say so.
    /// </summary>
    [Theory]
    [InlineData("SettingsLookView.cs")]
    [InlineData("SettingsBehaviorView.cs")]
    public void ANeitherBlockLoadsItsOwnSettings(string file) =>
        Assert.DoesNotContain("AppSettings.Load", Read(file), StringComparison.Ordinal);

    /// <summary>
    /// Trap 15: a lifted view carries its own visibility and spacing, and the host it hangs in
    /// gets no state of its own. The Gate 4 Loot breakout shipped correct, selected filter
    /// strips into a `ContentControl` XAML had declared `Visibility="Collapsed"` — invisible on
    /// every launch, with nothing in a diff, a test or a build to say so.
    ///
    /// The tab panels' OWN `Visibility` is the exception and is not state the blocks know
    /// about: it belongs to this window's tab machinery, which `SelectTab` drives. What must
    /// not appear is a Margin, or a Visibility on the inner host the block is added to.
    /// </summary>
    [Fact]
    public void TheHostsAreBare()
    {
        var xaml = Read("OptionsWindow.xaml");
        Assert.Contains("<StackPanel x:Name=\"LookBlockHost\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("<StackPanel x:Name=\"TabBehaviorPanel\" Visibility=\"Collapsed\"/>",
            xaml, StringComparison.Ordinal);
    }
}
