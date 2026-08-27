using System.Windows;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// A thin host for <see cref="SpawnsView"/> (World PR 1 — the view lift, zero product
/// change). The whole panel — border, title, rows, help text, add-form — lives in the
/// view now; this window owns only what a literal OS window owns: borderless chrome,
/// sizing, position memory, and the "closing only hides it" lifecycle (SPAWN-004): while
/// "Track spawns" is armed the window stays hidden until a countdown exists, and closing
/// destroys this instance — the next kill (or menu pick) builds a fresh one, so from the
/// player's chair it reads as hide/show even though it is really gone/rebuilt.
/// </summary>
public sealed class SpawnsWindow : Window
{
    private readonly SpawnsView _view;
    private readonly AppSettings _settings;

    /// <paramref name="initialZone"/>: the zone whose kill popped the window, so it
    /// opens showing the timer that summoned it.
    public SpawnsWindow(MainWindow main, SpawnsViewModel vm, string? initialZone = null)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.CanResize;
        Width = 560;
        WindowStartupLocation = WindowStartupLocation.Manual;

        WindowZoom.Attach(this, "spawns", main.Settings);
        // Resizable and REMEMBERED (David, 2026-08-25: "allow all the pop out windows to
        // be resized"). Safe for AllowResize's height sample because the spawn rows come
        // from the ledger the app already holds — nothing arrives after first render.
        WindowZoom.AllowResize(this, "spawns", main.Settings);
        _settings = main.Settings;
        _view = main.NewSpawnsView(initialZone);
        Content = _view.Body;

        MaxHeight = SystemParameters.WorkArea.Height - 40;
        _view.BodyScrollView.MaxHeight = SystemParameters.WorkArea.Height - 220;
        // Follow the monitor this window is on (portrait secondaries — discussion #31).
        SourceInitialized += (_, _) => UpdateHeightCaps();
        LocationChanged += (_, _) => UpdateHeightCaps();

        var restored = ScreenGuard.OnScreen(_settings.SpawnLeft, _settings.SpawnTop, Width, Height);
        if (restored) { Left = _settings.SpawnLeft; Top = _settings.SpawnTop; }
        else { Left = SystemParameters.WorkArea.Left + 40; Top = 80; }
        var (placedLeft, placedTop) = (Left, Top);

        Closed += (_, _) =>
        {
            _view.StopTicking();
            // Never let an unmoved fallback overwrite a real saved spot (#117).
            (_settings.SpawnLeft, _settings.SpawnTop) = WindowPlacement.PositionToPersist(
                restored, placedLeft, placedTop, Left, Top,
                _settings.SpawnLeft, _settings.SpawnTop);
            _settings.Save();
        };
    }

    /// <summary>Height caps follow the monitor this window occupies (portrait
    /// secondary screens are taller than the primary — discussion #31).</summary>
    private void UpdateHeightCaps()
    {
        if (MonitorMetrics.WorkAreaFor(this) is not { } work) return;
        MaxHeight = Math.Max(200, work.Height - 40);
        _view.BodyScrollView.MaxHeight = Math.Max(120, work.Height - 220);
    }

    /// <summary>The window's own facts for the <c>EQBUDDY_EXPAND</c> dump, in the shape
    /// <c>QuestsWindow.DebugFacts</c> established.</summary>
    public string DebugFacts() => _view.DebugFacts();
}
