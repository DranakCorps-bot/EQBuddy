using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using EQBuddy.Core;

namespace EQBuddy;

/// <summary>
/// The floating alert tile: a tiny always-on-top window, independent of the widget,
/// that shows tracked-rule alerts. It is permanently click-through and never takes
/// focus, so it can sit over the game without interfering — except while Options is
/// open (placement mode), when it becomes draggable so the user can position it.
/// </summary>
public partial class AlertWindow : Window
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _hide;
    private bool _placement;

    public AlertWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _hide = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _hide.Tick += (_, _) => { _hide.Stop(); if (!_placement) Hide(); };
        SourceInitialized += (_, _) => ApplyClickThrough(!_placement);
    }

    public void ShowAlert(string text)
    {
        AlertText.Text = text;
        PositionFromSettings();
        Show();
        Topmost = true;
        _hide.Stop();
        _hide.Start();
    }

    /// <summary>Options is open: show the tile as a draggable placement target.</summary>
    public void EnterPlacement()
    {
        _placement = true;
        _hide.Stop();
        AlertText.Text = "★ Alert banner — drag me to where alerts should appear";
        PositionFromSettings();
        Show();
        ApplyClickThrough(false);
        Topmost = true;
    }

    /// <summary>Options closed: persist the position and go back to click-through.</summary>
    public void ExitPlacement()
    {
        if (!_placement) return;
        _placement = false;
        _settings.AlertLeft = Left;
        _settings.AlertTop = Top;
        _settings.Save();
        ApplyClickThrough(true);
        Hide();
    }

    private void PositionFromSettings()
    {
        var wa = SystemParameters.WorkArea;
        double left = _settings.AlertLeft, top = _settings.AlertTop;
        if (double.IsNaN(left) || double.IsNaN(top))
        {
            // First use: just above the widget, falling back to the top-right corner.
            left = Owner?.Left ?? (wa.Right - 400);
            top = (Owner?.Top ?? 110) - 64;
        }
        Left = Math.Clamp(left, wa.Left, wa.Right - 140);
        Top = Math.Clamp(top, wa.Top, wa.Bottom - 44);
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (_placement && e.ChangedButton == MouseButton.Left) DragMove();
    }

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x80;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    private void ApplyClickThrough(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;   // not created yet — SourceInitialized applies it
        var style = GetWindowLong(hwnd, GwlExStyle) | WsExNoActivate | WsExToolWindow;
        style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLong(hwnd, GwlExStyle, style);
    }
}
