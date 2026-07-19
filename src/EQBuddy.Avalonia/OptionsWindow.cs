using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace EQBuddy.Avalonia;

public sealed class OptionsWindow : Window
{
    private readonly MainWindow _main;
    private readonly TextBlock _scaleLabel = LabelValue();
    private readonly TextBlock _bgOpacityLabel = LabelValue();
    private readonly TextBlock _opacityLabel = LabelValue();
    private readonly Slider _scaleSlider = Slider(0.8, 1.6, 0.05);
    private readonly Slider _bgOpacitySlider = Slider(0.15, 1.0, 0.05);
    private readonly Slider _opacitySlider = Slider(0.5, 1.0, 0.02);
    private bool _ready;

    public OptionsWindow(MainWindow main)
    {
        _main = main;
        Title = "EQBuddy Options";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Content = new Border
        {
            Background = AppTheme.BgBrush,
            CornerRadius = new CornerRadius(10),
            BorderBrush = AppTheme.BorderBrush,
            BorderThickness = new Thickness(1),
            Child = BuildContent(),
        };

        PointerPressed += OnDrag;
        _scaleSlider.Value = main.UiScale;
        _opacitySlider.Value = main.WidgetOpacity;
        _bgOpacitySlider.Value = main.BackgroundOpacityValue;
        Subscribe(_scaleSlider, () => _main.SetUiScale(_scaleSlider.Value));
        Subscribe(_bgOpacitySlider, () => _main.SetBackgroundOpacity(_bgOpacitySlider.Value));
        Subscribe(_opacitySlider, () => _main.SetWindowOpacity(_opacitySlider.Value));
        UpdateLabels();
        _ready = true;
    }

    private Control BuildContent()
    {
        var panel = new StackPanel { Margin = new Thickness(16), Width = 240 };
        var title = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        title.Children.Add(new TextBlock
        {
            Text = "Options",
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            Foreground = AppTheme.AccentBrush,
        });
        var close = AppTheme.IconButton("x", "Close");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => Close();
        title.Children.Add(close);
        panel.Children.Add(title);

        AddSlider(panel, "Widget size", _scaleLabel, _scaleSlider);
        AddSlider(panel, "Background see-through", _bgOpacityLabel, _bgOpacitySlider,
            "Only the dark panel fades; text stays sharp.");
        AddSlider(panel, "Whole-widget opacity", _opacityLabel, _opacitySlider,
            "Fades everything, text included.");
        panel.Children.Add(AppTheme.DimText("Size also scales all text. Changes apply instantly and are saved.",
            margin: new Thickness(0, 8, 0, 0)));
        return panel;
    }

    private static void AddSlider(Panel panel, string label, TextBlock value, Slider slider, string? hint = null)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = AppTheme.TextBrush });
        Grid.SetColumn(value, 1);
        row.Children.Add(value);
        panel.Children.Add(row);
        if (hint is not null)
            panel.Children.Add(AppTheme.DimText(hint));
        slider.Margin = new Thickness(0, 4, 0, 12);
        panel.Children.Add(slider);
    }

    private void Subscribe(Slider slider, Action apply)
    {
        slider.PropertyChanged += (_, args) =>
        {
            if (args.Property != RangeBase.ValueProperty) return;
            if (!_ready) return;
            apply();
            UpdateLabels();
        };
    }

    private void UpdateLabels()
    {
        _scaleLabel.Text = $"{_scaleSlider.Value:P0}";
        _opacityLabel.Text = $"{_opacitySlider.Value:P0}";
        _bgOpacityLabel.Text = $"{_bgOpacitySlider.Value:P0}";
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private static TextBlock LabelValue() => new()
    {
        FontSize = 12,
        Foreground = AppTheme.AccentBrush,
    };

    private static Slider Slider(double min, double max, double tick) => new()
    {
        Minimum = min,
        Maximum = max,
        TickFrequency = tick,
        IsSnapToTickEnabled = true,
    };
}
