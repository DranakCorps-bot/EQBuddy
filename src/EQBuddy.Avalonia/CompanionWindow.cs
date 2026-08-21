using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using EQBuddy.Companion;

namespace EQBuddy.Avalonia;

/// <summary>
/// The EQBuddy Mobile pairing window — the Linux/macOS twin of the WPF one, same words
/// and same order: the enable switch, the QR code a phone scans to become a live
/// companion display, the literal URL for when scanning won't take, the desktop gate
/// (which screens the PC is willing to send), and the honest firewall talk. All
/// lifecycle logic lives in CompanionHost (EQBuddy.Companion, UI-free); this window is
/// furniture.
/// </summary>
public sealed class CompanionWindow : Window
{
    private readonly CompanionHost _host;
    private readonly CheckBox _enable = new()
    {
        Content = new TextBlock
        {
            Text = EQBuddy.UI.Shared.CompanionPairingText.EnableLabel,
            FontSize = 12, Foreground = AppTheme.TextBrush,
        },
    };
    private readonly Image _qrImage = new()
    {
        Width = 220, Height = 220, Margin = new Thickness(0, 4, 0, 4),
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    private readonly TextBox _urlBox = new()
    {
        FontSize = 12, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
        BorderThickness = new Thickness(0), Background = Brushes.Transparent,
        Foreground = AppTheme.AccentBrush, Margin = new Thickness(0, 2, 0, 2),
    };
    private readonly TextBlock _statusLine = new()
    {
        FontSize = 12, Margin = new Thickness(0, 4, 0, 0), Foreground = AppTheme.TextBrush,
    };
    private readonly TextBlock _errorLine = new()
    {
        FontSize = 11.5, TextWrapping = TextWrapping.Wrap, IsVisible = false,
        Margin = new Thickness(0, 4, 0, 0), Foreground = AppTheme.BadBrush,
    };
    private readonly StackPanel _pairPanel = new();

    public CompanionWindow(CompanionHost host)
    {
        _host = host;
        Title = EQBuddy.UI.Shared.CompanionPairingText.Title;
        Width = 430;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        ShowInTaskbar = false;
        Background = AppTheme.BgBrush;

        var root = new StackPanel { Margin = new Thickness(14, 10, 14, 12) };

        var intro = Dim(EQBuddy.UI.Shared.CompanionPairingText.Intro);
        intro.Margin = new Thickness(0, 0, 0, 10);
        root.Children.Add(intro);

        _enable.IsChecked = _host.Running;
        // One handler, not Checked/Unchecked: Avalonia raises IsCheckedChanged for both
        // edges, and Refresh() below writes IsChecked itself — SetEnabled returns early
        // when the value already matches, so the echo cannot loop.
        _enable.IsCheckedChanged += (_, _) =>
        {
            _host.SetEnabled(_enable.IsChecked == true);
            Refresh();
        };
        root.Children.Add(_enable);
        root.Children.Add(_errorLine);

        // ---- pairing (QR + URL + connected count), visible while running ----
        RenderOptions.SetBitmapInterpolationMode(_qrImage, BitmapInterpolationMode.None);
        _pairPanel.Children.Add(_qrImage);
        var urlHint = Dim(EQBuddy.UI.Shared.CompanionPairingText.UrlHint);
        urlHint.Margin = new Thickness(0, 4, 0, 0);
        _pairPanel.Children.Add(urlHint);
        _pairPanel.Children.Add(_urlBox);
        _pairPanel.Children.Add(_statusLine);

        var chrome = Dim(EQBuddy.UI.Shared.CompanionPairingText.HomeScreenHint);
        chrome.Margin = new Thickness(0, 6, 0, 0);
        _pairPanel.Children.Add(chrome);

        // ActionButton, not IconButton: this is a real action with a consequence, and the
        // icon style is borderless and transparent — it rendered as an indented line of
        // prose that happened to respond to clicks. Only the capture said so.
        var regen = AppTheme.ActionButton(
            EQBuddy.UI.Shared.CompanionPairingText.RegenerateLabel,
            EQBuddy.UI.Shared.CompanionPairingText.RegenerateTip);
        regen.Margin = new Thickness(0, 6, 0, 0);
        regen.HorizontalAlignment = HorizontalAlignment.Left;
        regen.Click += (_, _) => { _host.RegenerateToken(); Refresh(); };
        _pairPanel.Children.Add(regen);

        // ---- desktop gate: which screens this PC is willing to send ----
        _pairPanel.Children.Add(new TextBlock
        {
            Text = EQBuddy.UI.Shared.CompanionPairingText.GateHeading, FontSize = 12,
            FontWeight = FontWeight.SemiBold, Foreground = AppTheme.AccentBrush,
            Margin = new Thickness(0, 12, 0, 2),
        });
        _pairPanel.Children.Add(Dim(EQBuddy.UI.Shared.CompanionPairingText.GateHint));
        // The list is long enough now to need its own scroll rather than a window taller
        // than a laptop screen.
        var gateList = new StackPanel();
        foreach (var surface in CompanionSurfaces.All)
        {
            var key = surface;   // captured per row, not the loop variable's last value
            var cb = new CheckBox
            {
                Content = new TextBlock
                {
                    Text = CompanionSurfaces.Label(key), FontSize = 12,
                    Foreground = AppTheme.TextBrush,
                },
                IsChecked = _host.OfferedSurfaces.Contains(key),
                Margin = new Thickness(0, 4, 0, 0),
            };
            ToolTip.SetTip(cb, CompanionSurfaces.Describe(key));
            cb.IsCheckedChanged += (_, _) => _host.SetSurfaceOffered(key, cb.IsChecked == true);
            gateList.Children.Add(cb);
        }
        _pairPanel.Children.Add(new ScrollViewer
        {
            Content = gateList,
            MaxHeight = 210,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 2, 0, 0),
        });

        // ---- the honest firewall talk (see CompanionServer's header comment) ----
        // Chosen by OPERATING SYSTEM in UI.Shared, not by which widget is asking: a
        // Windows player running this build should still be told about the Windows
        // prompt, and a Linux player should not be sent to a dialog that does not exist.
        var fw = Dim(EQBuddy.UI.Shared.CompanionPairingText.Firewall);
        fw.Margin = new Thickness(0, 12, 0, 0);
        _pairPanel.Children.Add(fw);

        root.Children.Add(_pairPanel);
        Content = root;

        _host.ClientsChanged += OnClientsChanged;
        Closed += (_, _) => _host.ClientsChanged -= OnClientsChanged;
        Refresh();
    }

    private void OnClientsChanged() => Dispatcher.UIThread.Post(UpdateStatus);

    private void Refresh()
    {
        _pairPanel.IsVisible = _host.Running;
        // See the WPF twin: a notice is not an error, and a silent fallback would be a
        // feature quietly behaving differently from what settings.json says.
        var line = _host.LastError ?? _host.Notice;
        _errorLine.Text = line ?? "";
        _errorLine.IsVisible = line is not null;
        _errorLine.Foreground = _host.LastError is null ? AppTheme.DimBrush : AppTheme.BadBrush;
        _enable.IsChecked = _host.Running;

        if (_host.PairingUrl is { } url)
        {
            _urlBox.Text = url;
            _qrImage.Source = QrBitmap.Render(QrEncoder.Encode(url));
        }
        UpdateStatus();
    }

    private void UpdateStatus() =>
        _statusLine.Text = EQBuddy.UI.Shared.CompanionPairingText.Status(_host.ClientCount);

    private static TextBlock Dim(string text) => new()
    {
        Text = text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
        Foreground = AppTheme.DimBrush,
    };
}

/// <summary>QR module matrix → a crisp Avalonia bitmap: black on white (scanners want
/// contrast, not theming), with the spec's quiet zone taken from UI.Shared so this and
/// the WPF renderer cannot drift.</summary>
internal static class QrBitmap
{
    public static Bitmap Render(bool[,] modules)
    {
        var padded = EQBuddy.UI.Shared.QrRaster.WithQuietZone(modules);
        var size = padded.GetLength(0);
        var bitmap = new WriteableBitmap(
            new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var buffer = bitmap.Lock();
        var stride = buffer.RowBytes;
        var pixels = new byte[stride * size];
        // Opaque white ground with the dark modules punched into it. Bgra8888 is four
        // bytes per pixel, so 0xFF everywhere IS white at full alpha.
        Array.Fill(pixels, (byte)0xFF);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if (!padded[y, x]) continue;
                var i = y * stride + x * 4;
                pixels[i] = pixels[i + 1] = pixels[i + 2] = 0x00;   // B, G, R — alpha stays 0xFF
            }
        Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
        return bitmap;
    }
}
