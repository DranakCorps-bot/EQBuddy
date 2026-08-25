using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// The Options window on Linux, rendered headlessly.
///
/// This is where the Avalonia port drifted furthest from WPF without anyone noticing: rules
/// had a boolean "sound on/off" toggle where Windows had a per-rule sound picker, so the
/// recommended way to use delayed alerts — two rules on one match, a quiet "heard it" and a
/// loud "cast now" — was silently useless on Linux. Nothing failed; the option simply wasn't
/// there. These tests assert the controls exist rather than trusting that they do.
/// </summary>
[Collection("avalonia")]
public class OptionsRenderTests : IDisposable
{
    private readonly string _profile =
        Directory.CreateTempSubdirectory("eqbuddy-options-").FullName;

    public OptionsRenderTests()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", _profile);
        Directory.CreateDirectory(Path.Combine(_profile, "logs"));
        // A rule of each interesting shape, so the editor has something to draw.
        File.WriteAllText(Path.Combine(_profile, "settings.json"),
            $$"""
              {
                "LogFolder": {{System.Text.Json.JsonSerializer.Serialize(Path.Combine(_profile, "logs"))}},
                "TruncateLogs": false, "ShowTutorial": false, "TrackSpawns": false,
                "LastSeenVersion": {{System.Text.Json.JsonSerializer.Serialize(UpdateChecker.CurrentVersion.ToString())}},
                "Theme": "ParchmentBrass",
                "AlertVolume": 0.35,
                "_comment": "DefaultRulesVersion is set so loading doesn't inject the built-in CC broke rule and change the rule count out from under these tests",
                "DefaultRulesVersion": 1,
                "TrackedRules": [
                  { "Name": "heard it", "Pattern": "CH -->", "Kind": 6, "Enabled": true,
                    "AlertBanner": true, "AlertSound": true, "AlertSoundName": "Ding" },
                  { "Name": "CAST NOW", "Pattern": "CH -->", "Kind": 6, "Enabled": true,
                    "AlertBanner": true, "AlertSound": true, "AlertSoundName": "Alarm",
                    "AlertDelaySeconds": 2.5 },
                  { "Name": "Befriend dropped", "Pattern": "Befriend", "Kind": 5,
                    "SpellFilter": 0, "Enabled": true },
                  { "Name": "Any mez dropped", "Pattern": "keep this", "Kind": 5,
                    "SpellFilter": 4, "Enabled": true }
                ]
              }
              """);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", null);
        try { Directory.Delete(_profile, recursive: true); } catch { /* best effort */ }
    }

    private static (MainWindow Main, OptionsWindow Options) Open()
    {
        var main = new MainWindow();
        main.Show();
        var options = new OptionsWindow(main);
        options.Show();
        return (main, options);
    }

    /// <summary>The window's body scroller. Every TextBox and open ComboBox brings its own
    /// ScrollViewer, so it is picked out by what it holds: the one margined tab body.</summary>
    private static ScrollViewer ContentScroll(OptionsWindow options) =>
        options.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(s => s.Content is Grid { Margin.Left: 16 });

    [AvaloniaFact]
    public void OptionsRendersAFrame()
    {
        var (main, options) = Open();

        var frame = options.CaptureRenderedFrame();

        Assert.NotNull(frame);
        Assert.True(frame!.Size.Width > 100, $"Options rendered only {frame.Size.Width}px wide");
        options.Close();
        main.Close();
    }

    [AvaloniaFact]
    public void AlertVolumeSliderLoadsAndPersistsTheSharedSetting()
    {
        var (main, options) = Open();
        Assert.Contains(options.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "Alert volume");
        var slider = options.GetVisualDescendants().OfType<Slider>()
            .Single(control => Math.Abs(control.Minimum - 0.1) < 0.001
                && Math.Abs(control.Maximum - 1.0) < 0.001
                && Math.Abs(control.Value - 0.35) < 0.001);

        slider.Value = 0.7;

        Assert.Equal(0.7, main.Settings.AlertVolume, 3);
        Assert.Contains(options.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text?.Contains("70") == true && text.Text.Contains('%'));
        options.Close();
        main.Close();
    }

    [AvaloniaFact]
    public void SpawnTrackingToggleUpdatesTheSharedSetting()
    {
        var (main, options) = Open();
        var toggle = options.GetVisualDescendants().OfType<CheckBox>()
            .Single(c => (c.Content as TextBlock)?.Text?.Contains("Track spawns") == true);

        Assert.False(toggle.IsChecked);
        toggle.IsChecked = true;
        Assert.True(main.Settings.TrackSpawns);

        main.SetTrackSpawns(false);
        Assert.False(toggle.IsChecked);
        Assert.False(main.Settings.TrackSpawns);

        options.Close();
        main.Close();
    }

    /// <summary>
    /// The Alt+Tab tick-box is really on screen on this lane, and it says what it costs.
    ///
    /// An absent control photographs as an unremarkable panel (trap 29) and a setting with
    /// no writer is trap 20, so neither a screenshot nor `DeadSettingTests` would catch
    /// this lane simply not having the row. The warning text is asserted too, because
    /// WS_EX_TOOLWINDOW takes the taskbar button with it and the tray icon then becomes
    /// the only way back — a control that removes someone's way back without saying so is
    /// the failure, not the flag.
    /// </summary>
    [AvaloniaFact]
    public void TheAltTabBoxIsOnScreenAndNamesWhatItCosts()
    {
        var (main, options) = Open();
        var toggle = options.GetVisualDescendants().OfType<CheckBox>()
            .Single(c => (c.Content as TextBlock)?.Text?.Contains("Alt+Tab") == true);

        Assert.False(toggle.IsChecked);
        toggle.IsChecked = true;
        Assert.True(main.Settings.HideFromAltTab);

        // The cost is beside the box, off the shared table rather than spelled here.
        var texts = options.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains(texts, t => t.Contains("taskbar button"));
        Assert.Contains(texts, t => t == EQBuddy.UI.Shared.AltTabPolicy.TaskbarWarning);

        options.Close();
        main.Close();
    }

    [AvaloniaFact]
    public void TargetDropsToggleAndAlertColorControlsAreAvailable()
    {
        var (main, options) = Open();
        var targetDrops = options.GetVisualDescendants().OfType<CheckBox>()
            .Single(c => (c.Content as TextBlock)?.Text?.Contains("target drops") == true);
        Assert.True(targetDrops.IsChecked);
        targetDrops.IsChecked = false;
        Assert.False(main.Settings.ShowTargetDrops);

        var colorDots = options.GetVisualDescendants().OfType<Button>()
            .Where(button => Equals(button.Content, "●"))
            .ToList();
        Assert.Equal(main.Settings.TrackedRules.Count, colorDots.Count);

        options.Close();
        main.Close();
    }

    /// <summary>
    /// The mez-duration editor exists on THIS lane too, with a row per catalog spell and
    /// the same provenance line the WPF window shows.
    ///
    /// The rows come from `UI.Shared.MezDurationRows`, so what this really pins is that
    /// the Avalonia Options window still CALLS it. A surface that exists on two screens
    /// and is built twice is #210's failure, and the way it shows up is one lane quietly
    /// not having the feature at all — which is exactly what #208 turned out to be.
    /// </summary>
    [AvaloniaFact]
    public void TheAlertsTabEditsMezDurations()
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.OptionsTab = "alerts";   // the tab they live on
        var options = new OptionsWindow(main);
        options.Show();

        var texts = options.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains("Mez durations", texts);
        Assert.Contains("Mesmerize", texts);
        // The provenance line, not just the name — a row that cannot say where its number
        // came from is the thing this feature exists to fix.
        Assert.Contains(texts, t => t.Contains("as documented"));

        // A box per catalog spell, holding the effective duration.
        var boxes = options.GetVisualDescendants().OfType<TextBox>()
            .Where(b => b.Text is "24s" or "48s" or "6s").ToList();
        Assert.NotEmpty(boxes);

        options.Close();
        main.Close();
    }

    /// <summary>Typing a duration writes it through as the player's own, and clearing the
    /// box hands the spell back. The precedence itself is `MezDurationOverrideTests`'
    /// business; this is the wiring between the box and the store.</summary>
    [AvaloniaFact]
    public void TypingAMezDurationSticksAndClearingItGivesItBack()
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.OptionsTab = "alerts";   // the tab they live on
        var options = new OptionsWindow(main);
        options.Show();

        var box = options.GetVisualDescendants().OfType<TextBox>().First(b => b.Text == "24s");
        box.Text = "44";
        PressEnter(box);
        Assert.Equal(44, main.MezDurations.Find("Mesmerize"));
        Assert.Equal(MezDurationSource.Typed, main.MezTracker.ResolveDuration("Mesmerize").Source);

        var typed = options.GetVisualDescendants().OfType<TextBox>().First(b => b.Text == "44s");
        typed.Text = "";
        PressEnter(typed);
        Assert.Null(main.MezDurations.Find("Mesmerize"));
        Assert.Equal(MezDurationSource.Catalog, main.MezTracker.ResolveDuration("Mesmerize").Source);

        options.Close();
        main.Close();
    }

    /// <summary>
    /// Ticking a breakout window in Options actually TURNS IT ON.
    ///
    /// It used to only clear the ✕-dismissal, while the switch that decides whether the
    /// window ever opens was a ★ on a card — so someone who came here, found the pet row,
    /// ticked it and saw nothing had to go and ask, repeatedly, on Reddit (relayed by
    /// David, 2026-08-20). A tick box that needs a second, unadvertised step is the
    /// "silent no-ops are broken" rule with the switch on the other side.
    /// </summary>
    [AvaloniaFact]
    public void TickingABreakoutWindowStarsTheStatThatOpensIt()
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.OptionsTab = "cards";
        main.Settings.MiniStats.Remove("pet");
        var options = new OptionsWindow(main);
        options.Show();

        var pet = options.GetVisualDescendants().OfType<CheckBox>()
            .Single(c => c.Content is StackPanel sp
                && sp.Children.OfType<TextBlock>().Any(t => t.Text == "Pet damage"));
        Assert.False(pet.IsChecked);          // not starred, so not on — and it SAYS so

        pet.IsChecked = true;
        Assert.Contains("pet", main.Settings.MiniStats);
        Assert.DoesNotContain("Pet", main.Settings.DisabledBreakouts);

        // Unticking stops the window and LEAVES the star: that same key is a cell in the
        // minimised pill, and quietly removing someone's pill cell because they closed a
        // window would be a second surprise in the opposite direction.
        pet.IsChecked = false;
        Assert.Contains("Pet", main.Settings.DisabledBreakouts);
        Assert.Contains("pet", main.Settings.MiniStats);

        options.Close();
        main.Close();
    }

    /// <summary>A row whose stat is already starred opens ticked — the box reports the
    /// real state rather than only whether it has been dismissed.</summary>
    [AvaloniaFact]
    public void ABreakoutRowReportsWhetherItWouldActuallyOpen()
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.OptionsTab = "cards";
        if (!main.Settings.MiniStats.Contains("pet")) main.Settings.MiniStats.Add("pet");
        main.Settings.DisabledBreakouts.Remove("Pet");
        var options = new OptionsWindow(main);
        options.Show();

        var pet = options.GetVisualDescendants().OfType<CheckBox>()
            .Single(c => c.Content is StackPanel sp
                && sp.Children.OfType<TextBlock>().Any(t => t.Text == "Pet damage"));
        Assert.True(pet.IsChecked);

        options.Close();
        main.Close();
    }

    /// <summary>Commit a duration box the way a player does. LostFocus carries
    /// FocusChangedEventArgs and cannot be synthesised from a bare RoutedEventArgs, so
    /// this drives the OTHER commit path the editor offers — which is a real user path,
    /// not a test-only door.</summary>
    private static void PressEnter(TextBox box) =>
        box.RaiseEvent(new global::Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = global::Avalonia.Input.Key.Enter,
        });

    [AvaloniaFact]
    public void LongOptionsContentHasABoundedScrollableViewport()
    {
        var main = new MainWindow();
        main.Show();
        // The rules live on the Watch tab — it must be the open tab for its height to count.
        main.Settings.OptionsTab = "watch";
        for (var i = 0; i < 30; i++)
            main.Settings.TrackedRules.Add(new TrackedRule
            {
                Name = $"extra rule {i}", Pattern = $"pattern {i}", Kind = WatchKind.Text,
            });

        var options = new OptionsWindow(main);
        options.Show();
        var scroll = ContentScroll(options);

        Assert.Equal(global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            scroll.VerticalScrollBarVisibility);
        Assert.True(scroll.MaxHeight < double.PositiveInfinity);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height,
            $"content {scroll.Extent.Height}px should exceed viewport {scroll.Viewport.Height}px");
        scroll.Offset = new global::Avalonia.Vector(0, 100);
        Assert.True(scroll.Offset.Y > 0);

        options.Close();
        main.Close();
    }

    /// <summary>Each rule offers a real sound choice, not just on/off — and the two rules in
    /// the fixture keep their different sounds.</summary>
    [AvaloniaFact]
    public void EachRuleHasItsOwnSoundPicker()
    {
        var (main, options) = Open();

        var soundPickers = options.GetVisualDescendants().OfType<ComboBox>()
            .Where(c => c.Items.Contains(AlertSoundCatalog.CustomChoice))
            .ToList();

        Assert.Equal(main.Settings.TrackedRules.Count, soundPickers.Count); // one per rule
        Assert.NotEqual(soundPickers[0].SelectedIndex, soundPickers[1].SelectedIndex);
        options.Close();
        main.Close();
    }

    /// <summary>The same drift as the sound picker, one feature over: SpeechVoice, SpeechRate
    /// and SpeechVolume existed in settings.json with nothing on this side to set them, so the
    /// only way to slow a too-fast voice down was a text editor. Asserts the controls exist and
    /// write through.</summary>
    [AvaloniaFact]
    public void SpeechVoiceAndSlidersArePresentAndPersist()
    {
        var (main, options) = Open();

        Assert.Contains(options.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "Alert voice");
        var voice = options.GetVisualDescendants().OfType<ComboBox>()
            .Single(c => c.Items.Contains(OptionsViewModel.DefaultVoiceChoice));
        // Voice enumeration is Windows-only, so the list is as long as this machine's
        // voices allow — one entry off Windows, more on it (the CI runner has several).
        // What holds everywhere: the default leads and is selected, and the honest empty
        // state is a picker left visible but disabled, exactly when it offers no choice.
        Assert.Equal(OptionsViewModel.DefaultVoiceChoice, voice.Items[0]);
        Assert.Equal(0, voice.SelectedIndex);
        Assert.Equal(voice.Items.Count > 1, voice.IsEnabled);

        var rate = options.GetVisualDescendants().OfType<Slider>()
            .Single(s => s.Minimum == SpokenAlerts.MinRate && s.Maximum == SpokenAlerts.MaxRate);
        rate.Value = -2;
        Assert.Equal(-2, main.Settings.SpeechRate);
        Assert.Contains(options.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "-2");

        var volume = options.GetVisualDescendants().OfType<Slider>()
            .Single(s => s.Minimum == 0 && s.Maximum == 100);
        volume.Value = 60;
        Assert.Equal(60, main.Settings.SpeechVolume);
        Assert.Contains(options.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "60%");

        options.Close();
        main.Close();
    }

    /// <summary>The per-rule phrase box: one per rule, revealed by that rule's S toggle, and
    /// saved on the way out. Empty keeps the old behaviour of speaking the alert's own label.</summary>
    [AvaloniaFact]
    public void TheSpokenPhraseBoxFollowsTheSpeechToggle()
    {
        var (main, options) = Open();

        var speechToggles = options.GetVisualDescendants()
            .OfType<global::Avalonia.Controls.Primitives.ToggleButton>()
            .Where(t => Equals(t.Content, "S"))
            .ToList();
        Assert.Equal(main.Settings.TrackedRules.Count, speechToggles.Count);

        var row = Assert.IsType<Grid>(speechToggles[0].Parent);
        var phrase = row.Children.OfType<TextBox>().Single(b => Grid.GetColumn(b) == 7);
        Assert.False(phrase.IsVisible);   // the rule doesn't speak yet

        speechToggles[0].IsChecked = true;
        Assert.True(phrase.IsVisible);

        phrase.Text = "Recast charm now";
        // The box saves on LostFocus. Focus() is a no-op in this headless window (no active
        // top level to hand focus to), so the event is raised directly — with the
        // FocusChangedEventArgs its handlers are typed for, not a bare RoutedEventArgs.
        phrase.RaiseEvent(new global::Avalonia.Input.FocusChangedEventArgs(
            global::Avalonia.Input.InputElement.LostFocusEvent));
        Assert.Equal("Recast charm now", main.Settings.TrackedRules[0].SpokenPhrase);

        options.Close();
        main.Close();
    }

    /// <summary>The delay box is present and shows what was saved — the entry point for the
    /// cue feature.</summary>
    [AvaloniaFact]
    public void TheDelayBoxShowsTheSavedValue()
    {
        var (main, options) = Open();

        var texts = options.GetVisualDescendants().OfType<TextBox>()
            .Select(t => t.Text ?? "").ToList();

        Assert.Contains("2.5", texts);
        options.Close();
        main.Close();
    }

    [AvaloniaFact]
    public void CustomThemeShowsEditableColorsAndSwatches()
    {
        var (main, options) = Open();
        var theme = options.GetVisualDescendants().OfType<ComboBox>()
            .Single(c => c.Items.Contains(OptionsViewModel.ThemeLabels[0]));

        theme.SelectedIndex = ThemeCatalog.IndexOf(CustomTheme.Key);

        Assert.Equal(CustomTheme.Key, main.Settings.Theme);
        var text = options.GetVisualDescendants().OfType<TextBox>()
            .Select(t => t.Text).ToList();
        Assert.Contains(CustomTheme.DefaultBg, text);
        Assert.Contains(CustomTheme.DefaultText, text);
        Assert.Contains(CustomTheme.DefaultAccent, text);
        Assert.True(options.GetVisualDescendants().OfType<Border>()
            .Count(b => b.Width == 15 && b.Height == 15) >= 16 * 3);

        options.Close();
        main.Close();
    }

    /// <summary>Every watch-rule kind is offered here too — a kind that exists in Core but
    /// never reaches the Linux dropdown is unreachable for those users.</summary>
    [AvaloniaFact]
    public void EveryWatchKindIsOffered()
    {
        var (main, options) = Open();

        var kindPicker = options.GetVisualDescendants().OfType<ComboBox>()
            .First(c => c.Items.Contains(OptionsViewModel.KindNames[0]));

        Assert.Equal(Enum.GetValues<WatchKind>().Length, kindPicker.Items.Count);
        options.Close();
        main.Close();
    }

    [AvaloniaFact]
    public void SpellFadeRulesOfferEveryClassAndHideIgnoredMatchText()
    {
        var (main, options) = Open();

        var filterPickers = options.GetVisualDescendants().OfType<ComboBox>()
            .Where(c => c.Items.Contains(OptionsViewModel.SpellFilterNames[0]))
            .ToList();
        Assert.Equal(main.Settings.TrackedRules.Count, filterPickers.Count);
        Assert.All(filterPickers, picker =>
            Assert.Equal(Enum.GetValues<SpellFilter>().Length, picker.Items.Count));
        Assert.Equal(2, filterPickers.Count(picker => picker.IsVisible));

        var namedPattern = options.GetVisualDescendants().OfType<AutoCompleteBox>()
            .Single(t => t.Text == "Befriend");
        var classPattern = options.GetVisualDescendants().OfType<AutoCompleteBox>()
            .Single(t => t.Text == "keep this");
        Assert.True(namedPattern.IsVisible);
        Assert.False(classPattern.IsVisible);
        Assert.Contains("Spirit of the Puma", namedPattern.ItemsSource!.Cast<string>());

        var kindPickers = options.GetVisualDescendants().OfType<ComboBox>()
            .Where(c => c.Items.Contains(OptionsViewModel.KindNames[0]))
            .ToList();
        kindPickers[0].SelectedIndex = (int)WatchKind.SpellFade;
        Assert.Equal(WatchKind.SpellFade, main.Settings.TrackedRules[0].Kind);
        Assert.True(filterPickers[0].IsVisible);

        options.Close();
        main.Close();
    }

    [AvaloniaFact]
    public void ChangingSpellFilterPersistsAndPreservesTheNamedPattern()
    {
        var (main, options) = Open();
        var rule = main.Settings.TrackedRules.Single(r => r.Name == "Befriend dropped");
        var filterPicker = options.GetVisualDescendants().OfType<ComboBox>()
            .Where(c => c.Items.Contains(OptionsViewModel.SpellFilterNames[0]))
            .Single(c => c.SelectedIndex == (int)SpellFilter.ByName && c.IsVisible);
        var pattern = options.GetVisualDescendants().OfType<AutoCompleteBox>()
            .Single(t => t.Text == "Befriend");

        filterPicker.SelectedIndex = (int)SpellFilter.Charm;

        Assert.Equal(SpellFilter.Charm, rule.SpellFilter);
        Assert.Equal("Befriend", rule.Pattern);
        Assert.False(pattern.IsVisible);
        Assert.Contains("\"SpellFilter\": 3", File.ReadAllText(Path.Combine(_profile, "settings.json")));

        filterPicker.SelectedIndex = (int)SpellFilter.ByName;
        Assert.True(pattern.IsVisible);
        Assert.Equal("Befriend", pattern.Text);

        options.Close();
        main.Close();
    }

    /// <summary>The window opens at the width the user last dragged it to, shared with WPF
    /// through OptionsWidth — it used to size itself to a hardcoded 520 panel and stay there.
    /// </summary>
    [AvaloniaFact]
    public void OptionsOpensAtTheSavedWidth()
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.OptionsWidth = 640;

        var options = new OptionsWindow(main);
        options.Show();

        Assert.Equal(640, options.Width, 1);
        // The body fills it, less the 1px frame on each side — the panel inside no longer
        // has a width of its own to hold the window open at.
        Assert.Equal(638, ContentScroll(options).Bounds.Width, 1);

        options.Close();
        main.Close();
    }

    /// <summary>A width beyond the bounds is pulled back in, so a settings file carrying a
    /// silly number cannot open a two-character-wide rule editor or a window off the screen.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(50, 390)]
    [InlineData(4000, 900)]
    public void ASavedWidthOutsideTheBoundsIsClamped(double saved, double expected)
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.OptionsWidth = saved;

        var options = new OptionsWindow(main);
        options.Show();

        Assert.Equal(expected, options.Width, 1);

        options.Close();
        main.Close();
    }

    /// <summary>Dragging the right grip widens the window and saves the result. Custom chrome
    /// means there is no native resize border, so these grips are the only way to resize —
    /// on macOS there was previously no way at all.</summary>
    [AvaloniaFact]
    public void DraggingTheRightEdgeWidensTheWindowAndSavesIt()
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.OptionsWidth = 500;
        var options = new OptionsWindow(main);
        options.Show();

        var grabX = options.Bounds.Width - 4;
        options.MouseDown(new global::Avalonia.Point(grabX, 100), MouseButton.Left);
        options.MouseMove(new global::Avalonia.Point(grabX + 120, 100));
        options.MouseUp(new global::Avalonia.Point(grabX + 120, 100), MouseButton.Left);

        Assert.Equal(620, options.Width, 1);
        Assert.Equal(620, main.Settings.OptionsWidth, 1);

        options.Close();
        main.Close();
    }

    /// <summary>The left grip grows the window leftwards: the width goes up and the window
    /// moves by the same amount, so the right edge stays where the user left it.</summary>
    [AvaloniaFact]
    public void DraggingTheLeftEdgeGrowsLeftwardsAndKeepsTheRightEdgeStill()
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.OptionsWidth = 500;
        var options = new OptionsWindow(main);
        options.Show();
        var startRight = options.Position.X + (int)Math.Round(options.Bounds.Width * options.RenderScaling);

        options.MouseDown(new global::Avalonia.Point(4, 100), MouseButton.Left);
        options.MouseMove(new global::Avalonia.Point(-80, 100));
        options.MouseUp(new global::Avalonia.Point(-80, 100), MouseButton.Left);

        Assert.Equal(584, options.Width, 1);   // grabbed at 4, released at -80
        var right = options.Position.X + (int)Math.Round(options.Bounds.Width * options.RenderScaling);
        Assert.True(Math.Abs(right - startRight) <= 1,
            $"right edge moved from {startRight} to {right}");

        options.Close();
        main.Close();
    }

    /// <summary>The tabbed layout (WPF 1.67.0): all five panels exist, and the saved
    /// OptionsTab decides which one is open — a stale key falls back to Look.</summary>
    [AvaloniaTheory]
    [InlineData("behavior", "behavior")]
    [InlineData("cards", "cards")]
    [InlineData("bogus-tab", "look")]
    public void TheSavedTabIsTheOpenOne(string saved, string effective)
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.OptionsTab = saved;
        var options = new OptionsWindow(main);
        options.Show();

        var links = options.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Text is "Look" or "Alerts & chips" or "Watch rules" or "Cards & windows" or "Behavior")
            .ToList();
        Assert.Equal(5, links.Count);
        // Exactly one link is underlined-active, and it matches the effective tab.
        var active = links.Single(l => l.TextDecorations is not null);
        var expected = effective switch
        {
            "behavior" => "Behavior", "cards" => "Cards & windows", _ => "Look",
        };
        Assert.Equal(expected, active.Text);

        options.Close();
        main.Close();
    }

    /// <summary>Share-string import: paste → preview → confirm, nothing landing unseen.
    /// The string comes from the same WatchRuleShare the ⤴ buttons use.</summary>
    [AvaloniaFact]
    public void ImportingAShareStringPreviewsThenAddsTheRule()
    {
        var (main, options) = Open();
        var before = main.Settings.TrackedRules.Count;
        var share = WatchRuleShare.Encode(
            [new TrackedRule { Name = "guildie's rule", Pattern = "FTE", Kind = WatchKind.Text }]);

        var importBox = options.GetVisualDescendants().OfType<TextBox>()
            .Single(t => ToolTip.GetTip(t) is string tip && tip.Contains("EQB1"));
        importBox.Text = share;
        var importBtn = options.GetVisualDescendants().OfType<Button>()
            .Single(b => Equals(b.Content, "Import…"));
        importBtn.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        var confirm = options.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Content is string s && s.StartsWith("✔"));
        Assert.True(confirm.IsVisible);
        Assert.Equal(before, main.Settings.TrackedRules.Count);   // preview adds nothing

        confirm.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(before + 1, main.Settings.TrackedRules.Count);
        Assert.Equal("guildie's rule", main.Settings.TrackedRules[^1].Name);

        options.Close();
        main.Close();
    }

    /// <summary>New shared settings reached the port: chip scale (Look), archive-before-
    /// empty (Behavior), and the hide-while-game-not-running opt-in (#114).</summary>
    [AvaloniaFact]
    public void ChipScaleArchiveAndHideTogglesPersist()
    {
        var (main, options) = Open();

        var chipScale = options.GetVisualDescendants().OfType<Slider>()
            .Single(s => Math.Abs(s.Minimum - 0.8) < 0.001 && Math.Abs(s.Maximum - 2.0) < 0.001);
        chipScale.Value = 1.4;
        Assert.Equal(1.4, main.Settings.ChipScale, 3);

        // Archiving is ON out of the box from 1.84.0 (#146) — emptying a log and keeping
        // no copy was a destructive default nobody chose. So the toggle worth exercising
        // is the one that gives the disk space back.
        var archive = options.GetVisualDescendants().OfType<CheckBox>()
            .Single(c => (c.Content as TextBlock)?.Text?.Contains("timestamped copy") == true);
        Assert.True(archive.IsChecked);
        archive.IsChecked = false;
        Assert.False(main.Settings.ArchiveLogs);
        archive.IsChecked = true;
        Assert.True(main.Settings.ArchiveLogs);

        var hideNotRunning = options.GetVisualDescendants().OfType<CheckBox>()
            .Single(c => (c.Content as TextBlock)?.Text?.Contains("isn't running at all") == true);
        hideNotRunning.IsChecked = true;
        Assert.True(main.Settings.HideWhenGameNotRunning);

        options.Close();
        main.Close();
    }

    /// <summary>
    /// #169 (joma65, Linux): the two hide tick-boxes would not stay ticked across launches.
    ///
    /// The suspected cause is outside this window — a second copy of EQBuddy on the same
    /// profile, which nothing stopped off Windows until SingleInstance — but "the box
    /// silently fails to persist" is the shape that had to be ruled out first, and nothing
    /// covered it. This walks the player's actual journey rather than assigning IsChecked:
    /// the Behavior tab, a real click on each box, Options closed and reopened (a fresh
    /// window reading the stored value back), then the widget closed and settings reloaded
    /// from disk. The #158 failure — a handler firing during construction and writing the
    /// default back over the loaded value — would fail at the reopen assertion.
    /// </summary>
    [AvaloniaFact]
    public void TheHideTickBoxesSurviveAClickACloseAndAReload()
    {
        var main = new MainWindow();
        main.Settings.OptionsTab = "behavior";   // the tab they live on, so a click lands
        main.Show();
        var options = new OptionsWindow(main);
        options.Show();

        static CheckBox Box(OptionsWindow w, string fragment) =>
            w.GetVisualDescendants().OfType<CheckBox>()
                .Single(c => (c.Content as TextBlock)?.Text?.Contains(fragment) == true);

        void Click(CheckBox box)
        {
            var point = box.TranslatePoint(new global::Avalonia.Point(6, box.Bounds.Height / 2), options);
            Assert.True(point.HasValue, "the Behavior tab is not showing — the box cannot be clicked");
            options.MouseDown(point!.Value, MouseButton.Left);
            options.MouseUp(point!.Value, MouseButton.Left);
        }

        Click(Box(options, "not focused"));
        Click(Box(options, "isn't running at all"));
        Assert.True(main.Settings.HideWhenGameUnfocused);
        Assert.True(main.Settings.HideWhenGameNotRunning);
        options.Close();

        var reopened = new OptionsWindow(main);
        reopened.Show();
        Assert.True(Box(reopened, "not focused").IsChecked);
        Assert.True(Box(reopened, "isn't running at all").IsChecked);
        reopened.Close();
        main.Close();   // OnClosed persists — the last chance to lose them

        var fromDisk = AppSettings.Load();
        Assert.True(fromDisk.HideWhenGameUnfocused, "hide-while-unfocused did not survive the restart");
        Assert.True(fromDisk.HideWhenGameNotRunning, "hide-while-not-running did not survive the restart");
    }

    /// <summary>Hotkeys are opt-in (#100): every action shows an unbound recorder until
    /// the player binds it, and a saved gesture is displayed on its button.</summary>
    [AvaloniaFact]
    public void HotkeyRowsShowUnboundRecordersAndSavedGestures()
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.Hotkeys["toggleMap"] = "Ctrl+Alt+M";
        var options = new OptionsWindow(main);
        options.Show();

        var recorders = options.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Content is string s
                && (s == "not bound — click to set" || s == "Ctrl+Alt+M"))
            .ToList();
        Assert.Equal(6, recorders.Count);   // one per HotkeyManager action
        Assert.Single(recorders, b => Equals(b.Content, "Ctrl+Alt+M"));

        options.Close();
        main.Close();
    }

    // ---- buff set (#120, Frankthetankk) ----

    private const string BuffSetKey = "tester_p1999";

    /// <summary>The editor needs a named character and a class combination. The headless
    /// log pipeline never names one, so MainWindow's test identity seam supplies it —
    /// the same answer the card and the ⏳ breakout read, so all three agree here too.
    /// </summary>
    private static (MainWindow Main, OptionsWindow Options) OpenWithBuffSet(
        IReadOnlyList<string> classes, params (string Class, string Spell)[] stored)
    {
        var main = new MainWindow();
        main.BuffSetIdentityForTests = () => (BuffSetKey, "Tester", classes, true);
        main.Show();
        foreach (var (cls, spell) in stored)
            BuffSetStore.Add(main.Settings.BuffSetsByClass, BuffSetKey, cls, spell);
        var options = new OptionsWindow(main);
        options.Show();
        options.RefreshBuffSetEditor();
        return (main, options);
    }

    /// <summary>The search popup's list. It hangs off the Popup rather than the window's
    /// visual tree, because an open popup lives in its own top level — and it is picked out
    /// by its content, since every templated ComboBox brings a Popup of its own.</summary>
    private static ListBox BuffSetMatches(OptionsWindow options) =>
        options.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.Popup>()
            .Select(p => (p.Child as Border)?.Child as ListBox)
            .Single(list => list is { MaxWidth: 480 })!;

    private static TextBox BuffSetAddBox(OptionsWindow options) =>
        options.GetVisualDescendants().OfType<TextBox>()
            .Single(t => ToolTip.GetTip(t) is string tip && tip.Contains("seen casting"));

    /// <summary>Stage 2's honesty rule: a bucket whose class isn't in the current
    /// combination is still shown — parked, dimmed and labelled — because this editor is
    /// the one place a parked pick can be removed. Hiding it would make the picks look
    /// lost when they are only waiting for the swap back.</summary>
    [AvaloniaFact]
    public void ParkedClassBucketsStayVisibleAndAreMarkedAsParked()
    {
        var (main, options) = OpenWithBuffSet(["Shaman"],
            (BuffSetStore.AnyClass, "Talisman of the Cat"),
            ("Shaman", "Talisman of the Beast"),
            ("Rogue", "Talisman of the Brute"));

        var headers = options.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.FontWeight == global::Avalonia.Media.FontWeight.SemiBold
                && t.FontSize == 11 && t.Text is not null)
            .Select(t => (t.Text!, t.Foreground))
            .ToList();
        Assert.Contains((BuffSetStore.AnyClass, (global::Avalonia.Media.IBrush)AppTheme.AccentBrush), headers);
        Assert.Contains(("Shaman", (global::Avalonia.Media.IBrush)AppTheme.AccentBrush), headers);
        var parked = Assert.Single(headers, h => h.Item1.StartsWith("Rogue"));
        Assert.Contains("kept for the swap back", parked.Item1);
        Assert.Same(AppTheme.DimBrush, parked.Item2);   // dimmed, not silently dropped

        // Every stored pick has a row, active or parked.
        var rows = options.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text).ToList();
        Assert.Contains("Talisman of the Cat", rows);
        Assert.Contains("Talisman of the Beast", rows);
        Assert.Contains("Talisman of the Brute", rows);
        // The note names the character and the combination it assembles from.
        Assert.Contains(options.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text?.Contains("Saved for Tester") == true && t.Text.Contains("SHM"));

        options.Close();
        main.Close();
    }

    /// <summary>Typing in the add box ranks the catalog into the popup, and picking a match
    /// writes it into the selected class bucket and saves.</summary>
    [AvaloniaFact]
    public void AddingABuffThroughTheSearchWritesItToTheSelectedBucket()
    {
        var (main, options) = OpenWithBuffSet(["Shaman"]);
        var classBox = options.GetVisualDescendants().OfType<ComboBox>()
            .Single(c => c.Items.Contains(BuffSetStore.AnyClass));
        // The full class list is offered, not just the active one, so a coming swap can be
        // configured in advance.
        Assert.Equal(QuestClassFilter.Classes.Length + 1, classBox.Items.Count);
        Assert.Equal(BuffSetStore.AnyClass, classBox.SelectedItem);
        classBox.SelectedItem = "Shaman";

        BuffSetAddBox(options).Text = "of the Cat";

        var matches = BuffSetMatches(options);
        var pick = matches.Items.Cast<ListBoxItem>().Single(i => Equals(i.Tag, "Talisman of the Cat"));
        matches.SelectedItem = pick;

        Assert.Equal(["Talisman of the Cat"], main.Settings.BuffSetsByClass[BuffSetKey]["Shaman"]);
        Assert.Contains("Talisman of the Cat",
            File.ReadAllText(Path.Combine(_profile, "settings.json")));
        Assert.Equal("", BuffSetAddBox(options).Text);
        Assert.Contains(options.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text == "Talisman of the Cat");   // the panel repainted at once

        options.Close();
        main.Close();
    }

    /// <summary>A row's ✕ removes that one pick from that one bucket and saves.</summary>
    [AvaloniaFact]
    public void RemovingARowWritesThrough()
    {
        var (main, options) = OpenWithBuffSet(["Shaman"],
            (BuffSetStore.AnyClass, "Talisman of the Cat"),
            ("Shaman", "Talisman of the Beast"));

        var remove = options.GetVisualDescendants().OfType<Button>()
            .Single(b => Equals(ToolTip.GetTip(b), "Remove Talisman of the Beast from Shaman"));
        remove.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        // Emptied buckets are pruned, so settings JSON never accumulates hollow entries.
        Assert.False(main.Settings.BuffSetsByClass[BuffSetKey].ContainsKey("Shaman"));
        Assert.Equal(["Talisman of the Cat"],
            main.Settings.BuffSetsByClass[BuffSetKey][BuffSetStore.AnyClass]);
        Assert.DoesNotContain(options.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text == "Talisman of the Beast");

        options.Close();
        main.Close();
    }

    /// <summary>Without a character there is nowhere to save: the editor says so and stays
    /// disabled rather than quietly writing into a nameless key.</summary>
    [AvaloniaFact]
    public void WithNoCharacterTheBuffSetEditorSaysSoAndStaysDisabled()
    {
        var (main, options) = Open();

        Assert.Contains(options.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text?.StartsWith("No character detected yet") == true);
        Assert.False(BuffSetAddBox(options).IsEnabled);
        Assert.False(options.GetVisualDescendants().OfType<ComboBox>()
            .Single(c => c.Items.Contains(BuffSetStore.AnyClass)).IsEnabled);

        options.Close();
        main.Close();
    }

    /// <summary>The grips must not also start a window move — the press is theirs alone,
    /// or the window walks off with the pointer instead of resizing.</summary>
    [AvaloniaFact]
    public void PressingAGripDoesNotDragTheWindow()
    {
        var main = new MainWindow();
        main.Show();
        main.Settings.OptionsWidth = 500;
        var options = new OptionsWindow(main);
        options.Show();
        var start = options.Position;

        var grabX = options.Bounds.Width - 4;
        options.MouseDown(new global::Avalonia.Point(grabX, 100), MouseButton.Left);
        options.MouseMove(new global::Avalonia.Point(grabX + 60, 140));
        options.MouseUp(new global::Avalonia.Point(grabX + 60, 140), MouseButton.Left);

        Assert.Equal(start, options.Position);

        options.Close();
        main.Close();
    }
}
