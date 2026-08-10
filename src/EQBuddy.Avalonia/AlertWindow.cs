using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using EQBuddy.Core;

namespace EQBuddy.Avalonia;

/// <summary>
/// Floating tracked-rule alert tile. During play it never activates and it is excluded
/// from hit-testing; while Options is open it becomes a draggable placement target.
/// </summary>
public sealed class AlertWindow : Window
{
    private readonly AppSettings _settings;
    private readonly MainWindow _owner;
    private readonly TextBlock _alertText;
    private readonly DispatcherTimer _hide;
    private bool _placement;

    public AlertWindow(AppSettings settings, MainWindow owner)
    {
        _settings = settings;
        _owner = owner;
        Title = "EQBuddy Alert";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;

        _alertText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = AppTheme.AccentBrush,
            TextWrapping = TextWrapping.Wrap,
        };
        Content = new Border
        {
            Background = AppTheme.BgBrush,
            BorderBrush = AppTheme.AccentBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8),
            MaxWidth = 380,
            Child = _alertText,
        };

        _hide = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _hide.Tick += (_, _) =>
        {
            _hide.Stop();
            if (!_placement) Hide();
        };
        Opened += (_, _) => ApplyClickThrough(!_placement);
        PointerPressed += OnDrag;
    }

    /// <summary>Optionally tinted per rule (Chaosrah's color-coded alerts, 2026-08-06);
    /// null/empty keeps the theme accent, applied per call so tints never stick.</summary>
    public void ShowAlert(string text, string? colorHex = null)
    {
        _alertText.Text = text;
        var tile = (Border)Content!;
        if (!string.IsNullOrEmpty(colorHex) && Color.TryParse(colorHex, out var c))
        {
            var brush = new SolidColorBrush(c);
            _alertText.Foreground = brush;
            tile.BorderBrush = brush;
        }
        else
        {
            _alertText.Foreground = AppTheme.AccentBrush;
            tile.BorderBrush = AppTheme.AccentBrush;
        }
        PositionFromSettings();
        ShowOwned();
        Topmost = true;
        _hide.Stop();
        _hide.Start();
    }

    /// <summary>Show a draggable preview while Options is open.</summary>
    public void EnterPlacement()
    {
        _placement = true;
        _hide.Stop();
        _alertText.Text = "★ Alert banner — drag me to where alerts should appear";
        PositionFromSettings();
        ShowOwned();
        ApplyClickThrough(false);
        Topmost = true;
    }

    /// <summary>Save the chosen location and restore play-mode click-through.</summary>
    public void ExitPlacement()
    {
        if (!_placement) return;
        _placement = false;
        _settings.AlertLeft = Position.X;
        _settings.AlertTop = Position.Y;
        _settings.Save();
        ApplyClickThrough(true);
        Hide();
    }

    private void ShowOwned()
    {
        if (!IsVisible) Show(_owner);
    }

    private void PositionFromSettings()
    {
        var screen = _owner.Screens.ScreenFromWindow(_owner) ?? _owner.Screens.Primary;
        var work = screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var left = _settings.AlertLeft;
        var top = _settings.AlertTop;
        // Checked against every screen, not clamped to the owner's: the old clamp
        // yanked a tile parked on another monitor back every launch (WPF parity).
        if (!ScreenGuard.OnScreen(this, left, top, 140, 44))
        {
            // First use, or the saved monitor is gone: just above the widget.
            left = Math.Clamp(_owner.Position.X, work.X, work.Right - 140);
            top = Math.Clamp(_owner.Position.Y - 64, work.Y, work.Bottom - 44);
        }

        Position = new PixelPoint((int)left, (int)top);
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (_placement && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void ApplyClickThrough(bool enabled)
    {
        if (TryGetPlatformHandle() is null) return;
        ClickThrough.Set(this, enabled);
    }
}
