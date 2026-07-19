using System.Windows;
using System.Windows.Input;

namespace EQBuddy;

public partial class OptionsWindow : Window
{
    private readonly MainWindow _main;
    private bool _ready;

    public OptionsWindow(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        Owner = main;
        ScaleSlider.Value = main.UiScale;
        OpacitySlider.Value = main.Opacity;
        UpdateLabels();
        _ready = true;
    }

    private void UpdateLabels()
    {
        ScaleLabel.Text = $"{ScaleSlider.Value:P0}";
        OpacityLabel.Text = $"{OpacitySlider.Value:P0}";
    }

    private void OnScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _main.SetUiScale(ScaleSlider.Value);
        UpdateLabels();
    }

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _main.SetWindowOpacity(OpacitySlider.Value);
        UpdateLabels();
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
