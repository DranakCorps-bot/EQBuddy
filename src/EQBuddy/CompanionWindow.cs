using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EQBuddy.Companion;

namespace EQBuddy;

/// <summary>
/// The EQBuddy Mobile pairing window (Options → Behavior → "EQBuddy Mobile…"): the
/// enable switch, the QR code a phone scans to become a live companion display, the
/// literal URL for when scanning won't take, the desktop gate (which screens the PC
/// is willing to send), and the honest firewall talk. All lifecycle logic lives in
/// CompanionHost (EQBuddy.Companion, UI-free); this window is furniture.
/// </summary>
public sealed class CompanionWindow : Window
{
    private readonly CompanionHost _host;
    private readonly CheckBox _enable;
    private readonly Image _qrImage = new() { Width = 220, Height = 220, Margin = new Thickness(0, 4, 0, 4) };
    private readonly TextBox _urlBox = new()
    {
        FontSize = 12, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
        BorderThickness = new Thickness(0), Background = Brushes.Transparent,
        Margin = new Thickness(0, 2, 0, 2),
    };
    private readonly TextBlock _statusLine = new() { FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _errorLine = new()
    {
        FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
        Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 0),
    };
    private readonly StackPanel _pairPanel = new();
    /// <summary>The address picker and its two lines of copy, together — this whole block
    /// is absent on a PC with one address, because a choice of one is not a choice (#264).</summary>
    private readonly StackPanel _addressPanel = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly ComboBox _addressBox = new() { FontSize = 12, Margin = new Thickness(0, 2, 0, 0) };
    /// <summary>Refresh() writes the selection itself; without this the write is
    /// indistinguishable from a click and would re-enter through SelectionChanged.</summary>
    private bool _syncingAddress;

    // ---- DRA-64: what has actually reached this PC ----
    private readonly TextBlock _reachHeadline = new()
    {
        FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 10, 0, 0),
    };
    private readonly TextBlock _reachDetail = new()
    {
        FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0),
    };
    private readonly TextBlock _reachChecklist = new()
    {
        FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
    };
    private readonly Button _copyCommand =
        Theming.Button(EQBuddy.UI.Shared.CompanionReachability.CopyCommandLabel);
    private readonly TextBlock _copiedNotice = new()
    {
        Text = EQBuddy.UI.Shared.CompanionReachability.CopiedNotice,
        FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0),
    };
    /// <summary>Re-evaluates the verdict while nothing happens — see the note where it is
    /// started. Field so Closed can stop it.</summary>
    private System.Windows.Threading.DispatcherTimer? _patience;
    /// <summary>When the player started looking at this code. The verdict's clock, so a PC
    /// that has been up all evening is not reported as having failed to pair all evening.</summary>
    private readonly DateTime _openedUtc = DateTime.UtcNow;

    public CompanionWindow(CompanionHost host)
    {
        _host = host;
        Title = EQBuddy.UI.Shared.CompanionPairingText.Title;
        Width = 430;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        SetResourceReference(BackgroundProperty, "BgBrush");

        var root = new StackPanel { Margin = new Thickness(14, 10, 14, 12) };

        var intro = Dim(EQBuddy.UI.Shared.CompanionPairingText.Intro);
        intro.Margin = new Thickness(0, 0, 0, 10);
        root.Children.Add(intro);

        _enable = new CheckBox
        {
            Content = new TextBlock
            {
                Text = EQBuddy.UI.Shared.CompanionPairingText.EnableLabel, FontSize = 12,
            },
            IsChecked = _host.Running,
        };
        ((TextBlock)_enable.Content).SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        _enable.Checked += (_, _) => { _host.SetEnabled(true); Refresh(); };
        _enable.Unchecked += (_, _) => { _host.SetEnabled(false); Refresh(); };
        root.Children.Add(_enable);
        root.Children.Add(_errorLine);
        _errorLine.SetResourceReference(TextBlock.ForegroundProperty, "BadBrush");

        // ---- pairing (QR + URL + connected count), visible while running ----
        _qrImage.HorizontalAlignment = HorizontalAlignment.Center;
        RenderOptions.SetBitmapScalingMode(_qrImage, BitmapScalingMode.NearestNeighbor);
        _pairPanel.Children.Add(_qrImage);
        var urlHint = Dim(EQBuddy.UI.Shared.CompanionPairingText.UrlHint);
        urlHint.Margin = new Thickness(0, 4, 0, 0);
        _pairPanel.Children.Add(urlHint);
        _urlBox.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
        _pairPanel.Children.Add(_urlBox);

        // ---- which of this PC's addresses the code points at (#264) ----
        var addressHead = new TextBlock
        {
            Text = EQBuddy.UI.Shared.CompanionPairingText.AddressLabel, FontSize = 12,
            FontWeight = FontWeights.SemiBold,
        };
        addressHead.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        _addressPanel.Children.Add(addressHead);
        _addressPanel.Children.Add(Dim(EQBuddy.UI.Shared.CompanionPairingText.AddressHint));
        _addressBox.SelectionChanged += (_, _) =>
        {
            if (_syncingAddress) return;
            _host.SetPairingAddress(_addressBox.SelectedIndex <= 0
                ? null
                : _host.PairingAddresses[_addressBox.SelectedIndex - 1].Address);
            Refresh();
        };
        _addressPanel.Children.Add(_addressBox);
        _pairPanel.Children.Add(_addressPanel);

        _statusLine.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        _pairPanel.Children.Add(_statusLine);

        var chrome = Dim(EQBuddy.UI.Shared.CompanionPairingText.HomeScreenHint);
        chrome.Margin = new Thickness(0, 6, 0, 0);
        _pairPanel.Children.Add(chrome);

        var regen = Theming.Button(EQBuddy.UI.Shared.CompanionPairingText.RegenerateLabel);
        regen.ToolTip = EQBuddy.UI.Shared.CompanionPairingText.RegenerateTip;
        regen.Margin = new Thickness(0, 6, 0, 0);
        regen.HorizontalAlignment = HorizontalAlignment.Left;
        regen.Click += (_, _) => { _host.RegenerateToken(); Refresh(); };
        _pairPanel.Children.Add(regen);

        // ---- desktop gate: which screens this PC is willing to send ----
        var gateHead = new TextBlock
        {
            Text = EQBuddy.UI.Shared.CompanionPairingText.GateHeading, FontSize = 12,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 2),
        };
        gateHead.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        _pairPanel.Children.Add(gateHead);
        _pairPanel.Children.Add(Dim(EQBuddy.UI.Shared.CompanionPairingText.GateHint));
        // The list is long enough now to need its own scroll rather than a window
        // taller than a laptop screen.
        var gateList = new StackPanel();
        foreach (var surface in CompanionSurfaces.All)
        {
            var cb = new CheckBox
            {
                Content = new TextBlock { Text = CompanionSurfaces.Label(surface), FontSize = 12 },
                IsChecked = _host.OfferedSurfaces.Contains(surface),
                Margin = new Thickness(0, 4, 0, 0),
                ToolTip = CompanionSurfaces.Describe(surface),
            };
            ((TextBlock)cb.Content).SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            cb.Checked += (_, _) => _host.SetSurfaceOffered(surface, true);
            cb.Unchecked += (_, _) => _host.SetSurfaceOffered(surface, false);
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
        // Chosen by OPERATING SYSTEM in UI.Shared rather than written here, so the
        // Avalonia twin cannot end up telling a Linux player to open a Windows dialog —
        // which is exactly what a hand-copied port of this paragraph did (#208).
        var fw = Dim(EQBuddy.UI.Shared.CompanionPairingText.Firewall);
        fw.Margin = new Thickness(0, 12, 0, 0);
        _pairPanel.Children.Add(fw);

        // ---- what has actually reached this PC (DRA-64) ----
        // The only part of this window that reports a MEASUREMENT rather than a setting.
        // Everything it says is decided in UI.Shared so the wording cannot drift from the
        // arithmetic that produced it.
        _reachHeadline.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        _pairPanel.Children.Add(_reachHeadline);
        _pairPanel.Children.Add(_reachDetail);
        _pairPanel.Children.Add(_reachChecklist);
        _copyCommand.Margin = new Thickness(0, 6, 0, 0);
        _copyCommand.HorizontalAlignment = HorizontalAlignment.Left;
        _copyCommand.Visibility = Visibility.Collapsed;
        _copyCommand.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(EQBuddy.UI.Shared.CompanionReachability.FirewallRuleCommand(
                    ExePath, _host.Port));
                _copiedNotice.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { EQBuddy.Core.CoreLog.Error(ex); }
        };
        _pairPanel.Children.Add(_copyCommand);
        _copiedNotice.Visibility = Visibility.Collapsed;
        _pairPanel.Children.Add(_copiedNotice);

        root.Children.Add(_pairPanel);
        Content = root;

        _host.ClientsChanged += OnClientsChanged;
        // The verdict turns on a clock as well as on arrivals, so silence has to be able
        // to repaint itself — a window that only redraws on an event would sit on
        // "Waiting…" forever precisely when nothing is arriving (trap 72's shape: the
        // repaint gate has to see the thing the feature decides on).
        _patience = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _patience.Tick += (_, _) => UpdateStatus();
        _patience.Start();
        Closed += (_, _) =>
        {
            _host.ClientsChanged -= OnClientsChanged;
            _patience?.Stop();
        };
        Refresh();
    }

    /// <summary>The file the firewall rule has to name. <c>Environment.ProcessPath</c> is
    /// the running executable, which is the whole point — a rule naming the path EQBuddy
    /// used to live at is the bug this window now explains.</summary>
    private static string ExePath =>
        Environment.ProcessPath ?? System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "EQBuddy.exe";

    private void OnClientsChanged() => Dispatcher.BeginInvoke(UpdateStatus);

    private void Refresh()
    {
        _pairPanel.Visibility = _host.Running ? Visibility.Visible : Visibility.Collapsed;
        // A NOTICE shows in the same line as an error but is not one: today it is only
        // "the port you asked for was refused, so I took another". Falling back silently
        // would be a feature quietly behaving differently from what settings.json says.
        var line = _host.LastError ?? _host.Notice;
        _errorLine.Text = line ?? "";
        _errorLine.Visibility = line is null ? Visibility.Collapsed : Visibility.Visible;
        _errorLine.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
            _host.LastError is null ? "DimBrush" : "BadBrush");
        _enable.IsChecked = _host.Running;

        RefreshAddresses();
        if (_host.PairingUrl is { } url)
        {
            _urlBox.Text = url;
            _qrImage.Source = QrBitmap.Render(QrEncoder.Encode(url));
        }
        UpdateStatus();
    }

    /// <summary>Rebuild the address picker from what is actually BOUND. Hidden unless the
    /// machine has a real choice, which is the common case — one NIC, one address, no
    /// control implying otherwise.</summary>
    private void RefreshAddresses()
    {
        var choices = _host.PairingAddresses;
        _addressPanel.Visibility = choices.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (choices.Count <= 1) return;

        _syncingAddress = true;
        try
        {
            _addressBox.Items.Clear();
            _addressBox.Items.Add(EQBuddy.UI.Shared.CompanionPairingText.AddressAuto);
            foreach (var choice in choices)
                _addressBox.Items.Add(EQBuddy.UI.Shared.CompanionPairingText.AddressChoice(
                    choice.Address, choice.AdapterDescription, choice.Wireless));
            // The pin, or Automatic — read back through the host so a pin naming an
            // address this machine no longer has shows as Automatic rather than as a row
            // that is not there.
            var pinned = _host.PinnedPairingAddress;
            var index = pinned is null ? -1 : IndexOf(choices, pinned);
            _addressBox.SelectedIndex = index < 0 ? 0 : index + 1;
            _addressBox.ToolTip = (string)_addressBox.Items[_addressBox.SelectedIndex];
        }
        finally { _syncingAddress = false; }
    }

    private static int IndexOf(IReadOnlyList<EQBuddy.Core.LanAddressCandidate> choices, string address)
    {
        for (var i = 0; i < choices.Count; i++)
            if (string.Equals(choices[i].Address, address, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private void UpdateStatus()
    {
        _statusLine.Text = EQBuddy.UI.Shared.CompanionPairingText.Status(
            _host.ClientCount, _host.OffBoxClientCount);

        var reach = EQBuddy.UI.Shared.CompanionReachability.Verdict(
            _host.Running, _host.OffBoxConnects, _host.SameMachineConnects,
            DateTime.UtcNow - _openedUtc);

        _reachHeadline.Text = EQBuddy.UI.Shared.CompanionReachability.Headline(reach);
        _reachDetail.Text = EQBuddy.UI.Shared.CompanionReachability.Detail(reach);
        _reachHeadline.SetResourceReference(TextBlock.ForegroundProperty,
            reach == EQBuddy.UI.Shared.CompanionReach.Reached ? "GoodBrush" : "AccentBrush");
        _reachDetail.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");

        var checklist = EQBuddy.UI.Shared.CompanionReachability.ShowsChecklist(reach);
        _reachChecklist.Text = checklist
            ? EQBuddy.UI.Shared.CompanionReachability.FirewallCause(ExePath) + "\n\n" +
              EQBuddy.UI.Shared.CompanionReachability.OtherCauses
            : "";
        _reachChecklist.Visibility = checklist ? Visibility.Visible : Visibility.Collapsed;
        _reachChecklist.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");

        // The copy button only exists where the command it copies is the advice — no
        // firewall command on a verdict that has not named the firewall.
        var offerCommand = checklist && OperatingSystem.IsWindows();
        _copyCommand.Visibility = offerCommand ? Visibility.Visible : Visibility.Collapsed;
        if (!offerCommand) _copiedNotice.Visibility = Visibility.Collapsed;
        _copiedNotice.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
    }

    private static TextBlock Dim(string text)
    {
        var tb = new TextBlock { Text = text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "DimBrush");
        return tb;
    }
}

/// <summary>QR module matrix → a crisp WPF bitmap: black on white (scanners want
/// contrast, not theming). The spec's quiet zone comes from UI.Shared so the Avalonia
/// widget's renderer cannot drift from this one.</summary>
internal static class QrBitmap
{
    public static BitmapSource Render(bool[,] modules)
    {
        var padded = EQBuddy.UI.Shared.QrRaster.WithQuietZone(modules);
        var size = padded.GetLength(0);
        var stride = (size + 7) / 8;
        var pixels = new byte[stride * size];
        // BlackWhite format: 1 = white. Start all white, punch the dark modules.
        Array.Fill(pixels, (byte)0xFF);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                if (!padded[y, x]) continue;
                pixels[y * stride + x / 8] &= (byte)~(0x80 >> (x % 8));
            }
        var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.BlackWhite, null, pixels, stride);
        bmp.Freeze();
        return bmp;
    }
}
