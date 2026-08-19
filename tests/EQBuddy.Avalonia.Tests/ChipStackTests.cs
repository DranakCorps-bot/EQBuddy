using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// The 1.33→1.72 chip-stack modernization: gauges along each chip's bottom edge, the
/// shared ChipScale transform host, and the clock-driven source that lets MainWindow
/// fold slow chips (#94) into the mez window's stack. Rendering-level checks run on the
/// headless platform like the WidgetRenderTests; gauge math itself lives in the chips'
/// Fraction values, pinned here without a window.
/// </summary>
[Collection("avalonia")]
public sealed class ChipStackTests
{
    /// <summary>The mez builder now carries the elapsed share the draining gauge needs;
    /// an unknown duration stays null so the track hides instead of lying.</summary>
    [Fact]
    public void MezChipsCarryElapsedFractionWhenDurationIsKnown()
    {
        var now = new DateTime(2026, 8, 8, 15, 0, 0);
        var chips = MezChipsWindow.BuildChips(
        [
            new MezState("an orc centurion", "Mesmerize", "You", now.AddSeconds(-12), now.AddSeconds(36)),
            new MezState("an orc oracle", "Entrance", "Aenari", now.AddSeconds(-5), null),
        ], now);

        Assert.Equal(0.25, chips[0].Fraction!.Value, precision: 3);
        Assert.Null(chips[1].Fraction);
    }

    /// <summary>WPF's FightChips wiring: one clock source feeds the stack whatever
    /// MainWindow built — here a slow chip — and the per-second refresh updates the
    /// countdown without a rebuild.</summary>
    [AvaloniaFact]
    public void FightStackAcceptsAClockSourceAndTicksCountdowns()
    {
        var settings = AppSettings.Load();
        var countdown = "1:30";
        var window = new MezChipsWindow(settings, _ =>
        [
            new SpawnChip("", "Slowed 55% · disease 1", countdown, false,
                "Togor's Insects · landed 3:00:00 pm", "Hourglass") { Fraction = 0.4 },
        ]);
        window.RefreshChips(DateTime.Now);
        window.Show();

        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? "").ToList();
        // The name alone: the kind's mark is a vector in its own column now, not a glyph
        // concatenated onto the front of the name (#148, #166).
        Assert.Contains("Slowed 55% · disease 1", text);
        Assert.Contains("1:30", text);
        // Parse the expected side too: Avalonia re-serializes a parsed geometry, so the
        // string off a PathIcon is a normalized form of the one that went in.
        var hourglass = global::Avalonia.Media.StreamGeometry
            .Parse(IconPaths.Path("Hourglass")).ToString();
        Assert.Contains(window.GetVisualDescendants().OfType<PathIcon>(),
            icon => icon.Data?.ToString() == hourglass);
        // The chip carries its gauge track under the row.
        Assert.Contains(window.GetVisualDescendants().OfType<Grid>(), g => g.Height == 2.5);
        // The scale host is in place, so ChipScale.Apply has a transform target.
        Assert.IsType<LayoutTransformControl>(window.Content);

        countdown = "1:29";
        window.RefreshChips(DateTime.Now);
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "1:29");
        window.Close();
    }

    /// <summary>Spawn chips grew the same bottom-edge gauge; a DUE chip's fill is the
    /// solid bad-red bar rather than a fraction.</summary>
    [AvaloniaFact]
    public void SpawnChipsRenderTheirGaugeTrack()
    {
        var main = new MainWindow();
        main.Show();
        var catalog = SpawnCatalog.LoadEmbedded();
        var profile = Directory.CreateTempSubdirectory("eqbuddy-chip-gauge").FullName;
        var overrides = SpawnOverrides.Load(System.IO.Path.Combine(profile, "overrides.json"));
        var timers = new SpawnTimers(catalog, overrides, System.IO.Path.Combine(profile, "timers.json"));
        timers.StartManual("Befallen", "Asaka L`Rei", 210);
        var chips = new SpawnChipsWindow(main, new SpawnsViewModel(catalog, overrides, timers));
        chips.RefreshChips(DateTime.Now);
        chips.Show(main);

        Assert.Contains(chips.GetVisualDescendants().OfType<Grid>(), g => g.Height == 2.5);
        Assert.IsType<LayoutTransformControl>(chips.Content);
        chips.Close();
        main.Close();
    }
}
