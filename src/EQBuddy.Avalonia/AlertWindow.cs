using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using EQBuddy.Core;

namespace EQBuddy.Avalonia;

/// <summary>
/// Floating tracked-rule alert tile. During play it never activates and its X11 input
/// region is empty; while Options is open it becomes a draggable placement target.
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
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x1E, 0x1A, 0x14)),
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

    public void ShowAlert(string text)
    {
        _alertText.Text = text;
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
        if (double.IsNaN(left) || double.IsNaN(top))
        {
            left = _owner.Position.X;
            top = _owner.Position.Y - 64;
        }

        Position = new PixelPoint(
            (int)Math.Clamp(left, work.X, work.Right - 140),
            (int)Math.Clamp(top, work.Y, work.Bottom - 44));
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (_placement && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void ApplyClickThrough(bool enabled)
    {
        if (TryGetPlatformHandle() is null) return;
        X11ClickThrough.Set(this, enabled);
    }
}
