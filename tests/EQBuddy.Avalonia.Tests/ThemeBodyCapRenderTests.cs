using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// #250 (Paineless): the expanded theme body's cap follows the height grip.
///
/// **These are here because "present in the build" and "in effect at runtime" are
/// different claims, and only the second one is the feature** — trap 42, where a correct
/// one-line fix shipped, was genuinely in the binary, and changed nothing on screen. The
/// arithmetic is unit-tested in <c>WidgetMetricsTests</c>; what this file asserts is that
/// the number reaches the control, on the lane that can be rendered without a display
/// server. The WPF twin has no unit tests at all (docs/TestPlan.md §5), so it says the
/// same thing through the <c>EQBUDDY_EXPAND</c> dump and <c>tests/EQBuddy.E2E</c>.
/// </summary>
[Collection("avalonia")]
public class ThemeBodyCapRenderTests : IDisposable
{
    private readonly string _profile =
        Directory.CreateTempSubdirectory("eqbuddy-bodycap-").FullName;

    public ThemeBodyCapRenderTests()
    {
        // A capture/render surface needs profile isolation MORE than an assertion does:
        // its whole output is a picture of whatever profile it finds (CLAUDE.md).
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", _profile);
        Directory.CreateDirectory(Path.Combine(_profile, "logs"));
        File.WriteAllText(Path.Combine(_profile, "settings.json"),
            $$"""
              { "LogFolder": {{System.Text.Json.JsonSerializer.Serialize(Path.Combine(_profile, "logs"))}},
                "TruncateLogs": false, "ShowTutorial": false, "TrackSpawns": false,
                "LastSeenVersion": {{System.Text.Json.JsonSerializer.Serialize(UpdateChecker.CurrentVersion.ToString())}},
                "Theme": "ParchmentBrass" }
              """);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", null);
        try { Directory.Delete(_profile, recursive: true); } catch { /* best effort */ }
    }

    private static ThemeCardPanel<ProgressTab> ProgressCard(MainWindow w) =>
        w.GetVisualDescendants().OfType<ThemeCardPanel<ProgressTab>>().Single();

    /// <summary>The body's ScrollViewer as the player's toolkit sees it. Asserting the
    /// CONTROL rather than the property behind it is the whole point: a cap that the card
    /// computed and never assigned is trap 42 wearing this feature's clothes.</summary>
    private static ScrollViewer BodyScroller(ThemeCardPanel<ProgressTab> card) =>
        card.GetVisualDescendants().OfType<ScrollViewer>().First();

    private static StatsSnapshot Snapshot() =>
        new() { SessionStart = new DateTime(2026, 8, 8), LastLevel = 12 };

    /// <summary>**The promise that protects every existing player: an untouched widget is
    /// pixel-identical.** ContentHeight is NaN until someone drags the grip, and the cap
    /// must be the number the app has always drawn — not "about 320", exactly 320.</summary>
    [AvaloniaFact]
    public void AWidgetNobodyHasDraggedKeepsExactlyTheOldCap()
    {
        var window = new MainWindow();
        window.Show();
        var card = ProgressCard(window);
        card.IsExpanded = true;
        window.RenderSnapshotForTest(Snapshot());

        Assert.True(double.IsNaN(window.Settings.ContentHeight));
        Assert.Equal(WidgetMetrics.ThemeBodyMaxHeight, card.BodyCap);
        Assert.Equal(WidgetMetrics.ThemeBodyMaxHeight, BodyScroller(card).MaxHeight);

        window.Close();
    }

    /// <summary>The ask itself. A player who drags the widget far taller gets more body,
    /// up to the ceiling — and the ceiling is in force at runtime, not merely in the
    /// arithmetic.</summary>
    [AvaloniaFact]
    public void DraggingTheWidgetTallerRaisesTheCapAsFarAsTheCeiling()
    {
        var window = new MainWindow();
        window.Show();
        var card = ProgressCard(window);
        card.IsExpanded = true;
        window.RenderSnapshotForTest(Snapshot());

        window.Settings.ContentHeight = 4000;   // further than any monitor allows
        window.RenderSnapshotForTest(Snapshot());

        Assert.Equal(WidgetMetrics.ThemeBodyCeiling, card.BodyCap);
        Assert.Equal(WidgetMetrics.ThemeBodyCeiling, BodyScroller(card).MaxHeight);
        // One card may double. It may not eat the monitor — and the number that says so
        // is a relationship, not a second unexplained constant.
        Assert.Equal(2 * WidgetMetrics.ThemeBodyMaxHeight, card.BodyCap);

        window.Close();
    }

    /// <summary>Dragging the widget SHORT can never take the body below what it had
    /// before any of this existed. This is the direction that could have regressed
    /// everyone, so it is asserted against the control and not just the formula.</summary>
    [AvaloniaFact]
    public void DraggingTheWidgetShortNeverGoesBelowTheFloor()
    {
        var window = new MainWindow();
        window.Show();
        var card = ProgressCard(window);
        card.IsExpanded = true;

        window.Settings.ContentHeight = 200;    // shorter than the floor itself
        window.RenderSnapshotForTest(Snapshot());

        Assert.Equal(WidgetMetrics.ThemeBodyMaxHeight, card.BodyCap);
        Assert.Equal(WidgetMetrics.ThemeBodyMaxHeight, BodyScroller(card).MaxHeight);

        window.Close();
    }

    /// <summary>And it FOLLOWS the grip rather than being read once at construction. A
    /// value sampled in a constructor is the shape of bug that makes a player drag a
    /// control and watch nothing happen — which is what #250 reported in the first
    /// place.</summary>
    [AvaloniaFact]
    public void TheCapFollowsTheGripBothWaysRatherThanBeingSampledOnce()
    {
        var window = new MainWindow();
        window.Show();
        var card = ProgressCard(window);
        card.IsExpanded = true;
        window.RenderSnapshotForTest(Snapshot());
        Assert.Equal(WidgetMetrics.ThemeBodyMaxHeight, card.BodyCap);

        window.Settings.ContentHeight = 4000;
        window.RenderSnapshotForTest(Snapshot());
        Assert.Equal(WidgetMetrics.ThemeBodyCeiling, card.BodyCap);

        // Double-tap on the grip: back to automatic, and back to the old number.
        window.Settings.ContentHeight = double.NaN;
        window.RenderSnapshotForTest(Snapshot());
        Assert.Equal(WidgetMetrics.ThemeBodyMaxHeight, card.BodyCap);

        window.Close();
    }

    /// <summary>A collapsed theme card is ALL header, so its extent is the whole card —
    /// the input the chrome sum depends on. If this ever answered a body height, every
    /// other card in the stack would silently shrink the open one.</summary>
    [AvaloniaFact]
    public void ACollapsedCardsHeaderExtentIsTheWholeCard()
    {
        var window = new MainWindow();
        window.Show();
        var card = ProgressCard(window);
        window.RenderSnapshotForTest(Snapshot());

        Assert.False(card.IsExpanded);
        Assert.Equal(card.Bounds.Height + card.Margin.Top + card.Margin.Bottom,
            card.HeaderExtent);

        window.Close();
    }
}
