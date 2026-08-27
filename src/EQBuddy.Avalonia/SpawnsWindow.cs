using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// A thin host for <see cref="SpawnsView"/> (World PR 1 — the view lift, zero product
/// change). The whole panel — border, title, rows, help text, add-form — lives in the
/// view now; this window owns only what a literal OS window owns: borderless chrome,
/// sizing, position memory, and the "closing only hides it" lifecycle. Tracking stays
/// armed while this window is hidden. New timers pop it open and the final timer
/// expiring closes it; disabling tracking lives in Options/the main menu.
/// </summary>
public sealed class SpawnsWindow : Window
{
    private readonly SpawnsView _view;
    private readonly AppSettings _settings;
    private bool _restoredSaved;
    private PixelPoint _placed;
    /// <summary>The last on-screen position, so Closed never persists a torn-down
    /// window's 0,0 (#169).</summary>
    private LastVisiblePosition _seen;

    public SpawnsWindow(MainWindow main, SpawnsViewModel vm, string? initialZone = null)
    {
        Title = "EQBuddy Spawns";
        // Rows can carry start, bell, clear, and delete actions. Give those controls a
        // permanent lane so starting a timer (which adds Clear) never reflows the inputs.
        Width = 740;
        SizeToContent = SizeToContent.Height;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;

        _settings = main.Settings;
        _view = main.NewSpawnsView(initialZone);
        Content = _view.Body;
        WindowZoom.Attach(this, "spawns", _settings);

        Opened += (_, _) =>
        {
            UpdateHeightLimit();
            _restoredSaved = ScreenGuard.OnScreen(this, _settings.SpawnLeft, _settings.SpawnTop, Width, Height);
            if (_restoredSaved)
                Position = new PixelPoint((int)_settings.SpawnLeft, (int)_settings.SpawnTop);
            _placed = Position;
        };
        // Follow the window between monitors; primary-only/open-time caps waste most of
        // a portrait secondary's height.
        PositionChanged += (_, _) =>
        {
            UpdateHeightLimit();
            _seen.Observe(Position.X, Position.Y, IsVisible);
        };
        Closed += (_, _) =>
        {
            _view.StopTicking();
            // Never read Position here — a closing window can report 0,0 on X11/Wayland
            // and that zero lands in settings.json as a real choice (#169). Only a
            // position seen while the window was on screen counts; with none seen, the
            // saved spot stands.
            var (curX, curY) = _seen.Or(_settings.SpawnLeft, _settings.SpawnTop);
            // Never let an unmoved fallback overwrite a real saved spot (#117).
            (_settings.SpawnLeft, _settings.SpawnTop) = WindowPlacement.PositionToPersist(
                _restoredSaved, _placed.X, _placed.Y, curX, curY,
                _settings.SpawnLeft, _settings.SpawnTop);
            _settings.Save();
        };
    }

    private void UpdateHeightLimit()
    {
        var screen = Screens.ScreenFromWindow(this);
        if (screen is null) return;
        var available = Math.Max(260, screen.WorkingArea.Height / screen.Scaling - 40);
        MaxHeight = available;
        _view.BodyScrollView.MaxHeight = Math.Max(120, available - 190);
    }

    /// <summary>Facts for a debug/E2E-style dump, mirroring the WPF window's shape.</summary>
    public string DebugFacts() => _view.DebugFacts();
}
