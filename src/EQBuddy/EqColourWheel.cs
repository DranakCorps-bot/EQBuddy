using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **The app's colour wheel** — a swatch face that opens a popup with a hue/saturation DISC
/// (hue = angle, saturation = radius), a brightness slider, a hex box that round-trips, and a
/// preview (David, 2026-09-29: "let people color code the types to whichever color they want
/// from a color wheel"). WPF ships none, so this is the one; the arithmetic is
/// <see cref="ColourWheelMath"/>, framework-free and unit-tested.
///
/// **It applies LIVE while dragging** (<c>onChange(hex, final: false)</c>) and hands the host a
/// FINAL pick on release, on Enter in the hex box and when the popup closes — so a host can
/// repaint on every move and persist once. Escape or a click outside closes it and KEEPS the
/// last pick: there is nothing to cancel because nothing was ever pending.
///
/// Keyboard: the disc is focusable — ←/→ turn the hue, ↑/↓ move the saturation; the slider and
/// the hex box are ordinary focusable controls. Every part carries an automation name.
///
/// The disc is rendered ONCE per size into a <see cref="WriteableBitmap"/> at full brightness;
/// brightness is a black overlay whose opacity is 1 − V, so dragging the slider repaints nothing
/// but one opacity.
/// </summary>
internal sealed class EqColourWheel
{
    private const int Disc = 168;
    private const double Radius = Disc / 2.0;
    private const double ThumbSize = 12;

    private static WriteableBitmap? _discBitmap;

    private readonly Popup _popup = new();
    private readonly Ellipse _dim = new() { Width = Disc, Height = Disc, Fill = Brushes.Black, IsHitTestVisible = false };
    private readonly Ellipse _thumb = new()
    {
        Width = ThumbSize, Height = ThumbSize, StrokeThickness = 2, Stroke = Brushes.White,
        IsHitTestVisible = false,
    };
    private readonly Canvas _canvas = new() { Width = Disc, Height = Disc, Focusable = true, Background = Brushes.Transparent };
    private readonly Slider _value = new() { Minimum = 0, Maximum = 100, SmallChange = 1, LargeChange = 10 };
    private readonly TextBox _hex = new() { Width = 72 };
    private readonly Border _preview = new() { Width = 28, Height = 20, CornerRadius = new CornerRadius(Tok.RadiusControl / 2) };
    private readonly Action<string, bool> _onChange;
    private Hsv _hsv = new(0, 0, 1);
    private bool _syncing;
    private string _lastSent = "";

    /// <summary>The button the player clicks: a swatch painted from <c>brushKey</c> by
    /// resource reference, so it always shows the colour in force.</summary>
    public Button Face { get; }

    /// <summary>The element a host adds: the face and its popup together, so neither can be
    /// placed without the other (<see cref="EqMultiPicker.Host"/>'s rule).</summary>
    public Panel Host { get; }

    public bool IsOpen => _popup.IsOpen;

    /// <summary>The hex the wheel currently holds — for a dump to report.</summary>
    public string Current => ColourWheelMath.ToHex(_hsv);

    /// <param name="name">What is being coloured, for the automation names ("DoT colour").</param>
    /// <param name="brushKey">The resource the face's swatch paints from.</param>
    /// <param name="current">The colour in force when the popup opens.</param>
    /// <param name="onChange">Every pick: live (<c>final</c> false) and settled (true).</param>
    public EqColourWheel(string name, string brushKey, Func<string> current, Action<string, bool> onChange)
    {
        _onChange = onChange;

        var swatch = new Border
        {
            Width = 16, Height = 16, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1),
        };
        swatch.SetResourceReference(Border.BackgroundProperty, brushKey);
        swatch.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        Face = new Button
        {
            Content = swatch,
            Height = Tok.ControlHeight,
            Padding = new Thickness(Tok.SpaceS, Tok.SpaceXxs, Tok.SpaceS, Tok.SpaceXxs),
            ToolTip = $"Pick the {name} colour",
        };
        Face.SetResourceReference(FrameworkElement.StyleProperty, "ActionButton");
        AutomationProperties.SetName(Face, $"{name} colour");
        Face.Click += (_, _) => { if (_popup.IsOpen) _popup.IsOpen = false; else Open(current()); };

        // ---- the disc ----
        var disc = new Image { Width = Disc, Height = Disc, Source = DiscBitmap(), IsHitTestVisible = false };
        _canvas.Children.Add(disc);
        _canvas.Children.Add(_dim);
        _canvas.Children.Add(_thumb);
        AutomationProperties.SetName(_canvas, $"{name} colour wheel — arrow keys turn the hue and saturation");
        _canvas.MouseLeftButtonDown += (_, e) =>
        {
            _canvas.Focus();
            _canvas.CaptureMouse();
            PickAt(e.GetPosition(_canvas));
            e.Handled = true;
        };
        _canvas.MouseMove += (_, e) => { if (_canvas.IsMouseCaptured) PickAt(e.GetPosition(_canvas)); };
        _canvas.MouseLeftButtonUp += (_, _) =>
        {
            if (!_canvas.IsMouseCaptured) return;
            _canvas.ReleaseMouseCapture();
            Send(final: true);
        };
        _canvas.KeyDown += OnDiscKey;
        // A visible focus ring for the keyboard: the thumb thickens while the disc has focus.
        _canvas.GotKeyboardFocus += (_, _) => _thumb.StrokeThickness = 3;
        _canvas.LostKeyboardFocus += (_, _) => _thumb.StrokeThickness = 2;

        // ---- brightness ----
        AutomationProperties.SetName(_value, $"{name} brightness");
        _value.ValueChanged += (_, _) =>
        {
            if (_syncing) return;
            _hsv = _hsv with { V = _value.Value / 100 };
            Sync(fromHex: false);
            Send(final: false);
        };
        _value.PreviewMouseLeftButtonUp += (_, _) => Send(final: true);
        _value.KeyUp += (_, _) => Send(final: true);

        // ---- hex box + preview ----
        AutomationProperties.SetName(_hex, $"{name} colour hex");
        _hex.SetResourceReference(FrameworkElement.StyleProperty, "InputBox");
        _hex.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitHex(); e.Handled = true; } };
        _hex.LostKeyboardFocus += (_, _) => CommitHex();
        _preview.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        _preview.BorderThickness = new Thickness(1);

        var bottom = new Grid { Margin = new Thickness(0, Tok.SpaceS, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.Children.Add(_preview);
        var hexLabel = DesignSystem.Text(Tok.TypeRole.Caption, "Hex");
        hexLabel.VerticalAlignment = VerticalAlignment.Center;
        hexLabel.HorizontalAlignment = HorizontalAlignment.Right;
        hexLabel.Margin = new Thickness(0, 0, Tok.SpaceS, 0);
        Grid.SetColumn(hexLabel, 1);
        bottom.Children.Add(hexLabel);
        Grid.SetColumn(_hex, 2);
        bottom.Children.Add(_hex);

        var brightnessLabel = DesignSystem.Text(Tok.TypeRole.Caption, "Brightness");
        brightnessLabel.Margin = new Thickness(0, Tok.SpaceS, 0, 0);

        var stack = new StackPanel { Width = Disc };
        stack.Children.Add(_canvas);
        stack.Children.Add(brightnessLabel);
        stack.Children.Add(_value);
        stack.Children.Add(bottom);

        var frame = new Border
        {
            CornerRadius = new CornerRadius(Tok.RadiusCard),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(Tok.SpaceL),
            Child = stack,
        };
        frame.SetResourceReference(Border.BackgroundProperty, "PopupBrush");
        frame.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        frame.KeyDown += (_, e) => { if (e.Key == Key.Escape) { _popup.IsOpen = false; e.Handled = true; } };

        _popup.PlacementTarget = Face;
        _popup.Placement = PlacementMode.Bottom;
        _popup.StaysOpen = false;      // a click outside closes it, keeping the last pick
        _popup.AllowsTransparency = true;
        _popup.Child = frame;
        _popup.Closed += (_, _) => Send(final: true);

        var host = new StackPanel { Orientation = Orientation.Horizontal };
        host.Children.Add(Face);
        host.Children.Add(_popup);
        Host = host;
    }

    /// <summary>Opens the popup on a colour ("#RRGGBB"); the debug hook's door as well as the
    /// face's.</summary>
    public void Open(string hex)
    {
        _hsv = ColourWheelMath.FromHex(hex) ?? new Hsv(0, 0, 1);
        _lastSent = ColourWheelMath.ToHex(_hsv);
        Sync(fromHex: false);
        _popup.IsOpen = true;
        _canvas.Dispatcher.BeginInvoke(() => _canvas.Focus(),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void PickAt(Point p)
    {
        var (h, s) = ColourWheelMath.FromPoint(p.X - Radius, p.Y - Radius, Radius);
        // Picking on the disc at zero brightness would change nothing visible — a black wheel
        // that ignores the pointer — so a pick lifts a fully dark colour to full brightness.
        _hsv = new Hsv(h, s, _hsv.V <= 0.01 ? 1 : _hsv.V);
        Sync(fromHex: false);
        Send(final: false);
    }

    private void OnDiscKey(object sender, KeyEventArgs e)
    {
        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 15 : 5;
        _hsv = e.Key switch
        {
            Key.Left => _hsv with { H = (_hsv.H + step) % 360 },
            Key.Right => _hsv with { H = (_hsv.H - step + 360) % 360 },
            Key.Up => _hsv with { S = Math.Min(1, _hsv.S + step / 100.0) },
            Key.Down => _hsv with { S = Math.Max(0, _hsv.S - step / 100.0) },
            _ => _hsv,
        };
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
        e.Handled = true;
        Sync(fromHex: false);
        Send(final: true);
    }

    private void CommitHex()
    {
        if (ColourWheelMath.FromHex(_hex.Text) is not { } hsv)
        {
            _hex.Text = ColourWheelMath.ToHex(_hsv);   // an unreadable entry is not committed
            return;
        }
        _hsv = hsv;
        Sync(fromHex: true);
        Send(final: true);
    }

    /// <summary>Puts every part of the control at the current colour.</summary>
    private void Sync(bool fromHex)
    {
        _syncing = true;
        var (x, y) = ColourWheelMath.PointFor(_hsv.H, _hsv.S, Radius);
        Canvas.SetLeft(_thumb, Radius + x - ThumbSize / 2);
        Canvas.SetTop(_thumb, Radius + y - ThumbSize / 2);
        _dim.Opacity = 1 - _hsv.V;
        _value.Value = Math.Round(_hsv.V * 100);
        var hex = ColourWheelMath.ToHex(_hsv);
        var (r, g, b) = ColourWheelMath.ToRgb(_hsv);
        _preview.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
        // The thumb's ring reads on light and dark picks alike.
        _thumb.Stroke = _hsv.V > 0.6 && _hsv.S < 0.4 ? Brushes.Black : Brushes.White;
        if (!fromHex || !_hex.IsKeyboardFocusWithin) _hex.Text = hex;
        _syncing = false;
    }

    private void Send(bool final)
    {
        var hex = ColourWheelMath.ToHex(_hsv);
        if (!final && hex == _lastSent) return;
        _lastSent = hex;
        _onChange(hex, final);
    }

    /// <summary>The disc at full brightness, rendered once and shared by every wheel (it is
    /// frozen, and every wheel is the same size). A one-pixel alpha feather at the rim keeps
    /// the edge from stair-stepping.</summary>
    private static WriteableBitmap DiscBitmap()
    {
        if (_discBitmap is { } done) return done;
        var bmp = new WriteableBitmap(Disc, Disc, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[Disc * Disc * 4];
        for (var py = 0; py < Disc; py++)
        for (var px = 0; px < Disc; px++)
        {
            double dx = px + 0.5 - Radius, dy = py + 0.5 - Radius;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > Radius) continue;
            var (r, g, b) = ColourWheelMath.DiscPixel(dx, dy, Radius)!.Value;
            var alpha = (byte)Math.Clamp(Math.Round((Radius - dist) * 255), 0, 255);
            var i = (py * Disc + px) * 4;
            // Premultiplied BGRA is not required for Bgra32; straight alpha is what it takes.
            pixels[i] = b; pixels[i + 1] = g; pixels[i + 2] = r; pixels[i + 3] = alpha;
        }
        bmp.WritePixels(new Int32Rect(0, 0, Disc, Disc), pixels, Disc * 4, 0);
        bmp.Freeze();
        return _discBitmap = bmp;
    }
}
