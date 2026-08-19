using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// <see cref="WindowZoom"/>'s width side (#186): a zoomed-out window has to SHRINK, not
/// merely render smaller type inside the same rectangle.
///
/// This exists because the Avalonia copy of that logic had drifted from the WPF original
/// and shipped a zero-width window. Both builds read the saved zoom with
/// <c>TryGetValue(key, out var saved)</c>; WPF then looked the value up a SECOND time with
/// its own <c>: 1.0</c> fallback before computing the width, and this build passed
/// <c>saved</c> straight through — which is 0.0 when the key is absent. So every
/// baseWidth window (the Quest Tracker, and now Progress) opened at
/// <c>baseWidth × 0</c> on Linux and macOS for anyone who had never Ctrl+wheeled it, and
/// never on Windows.
///
/// The lesson is the one CLAUDE.md already records — a hand-copied twin drifts — so the
/// test is written against the OBSERVABLE consequence rather than the line that was wrong.
/// </summary>
public class WindowZoomTests
{
    [AvaloniaFact]
    public void AWindowWithNoSavedZoomKeepsItsFullWidth()
    {
        var window = new Window { Width = 520, Content = new StackPanel() };
        WindowZoom.Attach(window, "never-zoomed", new AppSettings(), baseWidth: 520);

        Assert.Equal(520, window.Width);
    }

    [AvaloniaFact]
    public void ASavedZoomScalesTheWindowWidth()
    {
        var settings = new AppSettings();
        settings.WindowZooms["progress"] = 0.75;
        var window = new Window { Width = 520, Content = new StackPanel() };
        WindowZoom.Attach(window, "progress", settings, baseWidth: 520);

        Assert.Equal(390, window.Width);
    }
}
