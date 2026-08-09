using Avalonia.Controls;
using Avalonia.Headless;
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

    [AvaloniaFact]
    public void TargetDropsToggleAndAlertColorControlsAreAvailable()
    {
        var (main, options) = Open();
        var targetDrops = options.GetVisualDescendants().OfType<CheckBox>()
            .Single(c => (c.Content as TextBlock)?.Text?.Contains("known drops") == true);
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

    [AvaloniaFact]
    public void LongOptionsContentHasABoundedScrollableViewport()
    {
        var main = new MainWindow();
        main.Show();
        for (var i = 0; i < 30; i++)
            main.Settings.TrackedRules.Add(new TrackedRule
            {
                Name = $"extra rule {i}", Pattern = $"pattern {i}", Kind = WatchKind.Text,
            });

        var options = new OptionsWindow(main);
        options.Show();
        var scroll = options.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(s => s.Content is StackPanel { Width: 520 });

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
}
