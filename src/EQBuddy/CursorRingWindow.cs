using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// A click-through ring riding the mouse cursor (issue #81, Bigmatt500: "I often lose
/// my tiny cursor when playing"). Same overlay recipe as the grid: never focused,
/// never hit-tested, toggled from the right-click menu only. The window is a small
/// square that FOLLOWS the cursor rather than a desk-sized canvas repainting on every
/// move — 30 Hz position updates of a tiny layered window cost nothing measurable.
/// Accent-colored with a dark under-stroke so it reads on bright and dark ground both.
/// The diameter is <see cref="AppSettings.CursorRingSize"/> (Options → Look → Ring size).
/// The window stays click-through, so nothing here is a drag handle (DRA-3202).
/// </summary>
public sealed class CursorRingWindow : Window
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _follow;
    private readonly Ellipse _under = new();
    private readonly Ellipse _ring = new();

    public CursorRingWindow(AppSettings settings)
    {
        _settings = settings;
        // WindowStyle.None draws no title bar. The title is how the screenshot harness
        // finds this window; the widget's own title stays the exact string "EQBuddy".
        Title = "EQBuddy cursor ring";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        // A transparent layered window photographs as empty. EQBUDDY_OPAQUE is the
        // screenshot harness only (CaptureTheme); players keep the clear ground.
        Background = CaptureTheme.Enabled
            ? new SolidColorBrush(Color.FromRgb(0x20, 0x22, 0x25))
            : Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;

        var grid = new System.Windows.Controls.Grid();
        grid.Children.Add(_under);
        grid.Children.Add(_ring);
        Content = grid;
        ApplySize();

        SourceInitialized += (_, _) => ApplyClickThrough();
        _follow = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _follow.Tick += (_, _) => Follow();
        IsVisibleChanged += (_, _) => { if (IsVisible) _follow.Start(); else _follow.Stop(); };
    }

    /// <summary>(Re)build from settings — called on open and live from Options.</summary>
    public void ApplySize()
    {
        var d = CursorRingSizeLimits.Clamp(_settings.CursorRingSize);
        Width = d + 8;
        Height = d + 8;
        foreach (var (e, stroke, thickness) in new[]
        {
            (_under, (Brush)new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), 4.5),
            (_ring, TryFindResource("AccentBrush") as Brush ?? Brushes.Gold, 2.5),
        })
        {
            e.Width = d;
            e.Height = d;
            e.Stroke = stroke;
            e.StrokeThickness = thickness;
            e.HorizontalAlignment = HorizontalAlignment.Center;
            e.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    private void Follow()
    {
        // Raw device pixels end to end: WPF's DIP-based Left/Top converts through ONE
        // monitor's scale factor, so on mixed-DPI desks the ring drifted off the
        // pointer the moment it crossed monitors (caught by the smoke test, first
        // try). SetWindowPos speaks the cursor's own coordinate system everywhere.
        if (_hwnd == IntPtr.Zero || !GetCursorPos(out var p)) return;
        if (!GetWindowRect(_hwnd, out var r)) return;
        SetWindowPos(_hwnd, IntPtr.Zero,
            p.X - (r.Right - r.Left) / 2, p.Y - (r.Bottom - r.Top) / 2, 0, 0,
            SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x80;
    private const uint SwpNoSize = 0x1, SwpNoZOrder = 0x4, SwpNoActivate = 0x10;

    private IntPtr _hwnd;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out PixelPoint p);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out PixelRect r);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after,
        int x, int y, int cx, int cy, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelPoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelRect { public int Left, Top, Right, Bottom; }

    private void ApplyClickThrough()
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        if (_hwnd == IntPtr.Zero) return;
        SetWindowLong(_hwnd, GwlExStyle,
            GetWindowLong(_hwnd, GwlExStyle) | WsExTransparent | WsExNoActivate | WsExToolWindow);
    }
}
