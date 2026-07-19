using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace EQBuddy.Avalonia;

internal static class AppTheme
{
    public static readonly IBrush PanelBrush = Brush("#26FFFFFF");
    public static readonly IBrush PanelHoverBrush = Brush("#33FFD98C");
    public static readonly IBrush BorderBrush = Brush("#66C9A227");
    public static readonly IBrush TextBrush = Brush("#FFEDE4D3");
    public static readonly IBrush DimBrush = Brush("#FF9C927F");
    public static readonly IBrush AccentBrush = Brush("#FFE3B341");
    public static readonly IBrush GoodBrush = Brush("#FF7FBF5F");
    public static readonly IBrush BadBrush = Brush("#FFD9634F");
    public static readonly IBrush WarnBrush = Brush("#FFE0A030");
    public static readonly IBrush BgBrush = Brush("#F21C1917");

    public static IBrush BgWithOpacity(double opacity) =>
        new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(opacity, 0.15, 1.0) * 255), 0x1C, 0x19, 0x17));

    public static Button IconButton(string text, string tip)
    {
        var button = new Button
        {
            Content = text,
            Background = Brushes.Transparent,
            Foreground = DimBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2),
            FontSize = 13,
            Cursor = new Cursor(StandardCursorType.Hand),
            MinWidth = 26,
            MinHeight = 24,
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    public static ToggleButton IconToggle(string text, string tip)
    {
        var button = new ToggleButton
        {
            Content = text,
            Background = Brushes.Transparent,
            Foreground = AccentBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2),
            FontSize = 13,
            Cursor = new Cursor(StandardCursorType.Hand),
            MinWidth = 26,
            MinHeight = 24,
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    public static ToggleButton StarToggle(string key, string tip)
    {
        var button = new ToggleButton
        {
            Tag = key,
            Content = "☆",
            Background = Brushes.Transparent,
            Foreground = DimBrush,
            BorderThickness = new Thickness(0),
            FontSize = 12,
            Margin = new Thickness(8, 0, 0, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        ToolTip.SetTip(button, tip);
        return button;
    }

    public static TextBlock DimText(string text, Thickness? margin = null) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = DimBrush,
        TextWrapping = TextWrapping.Wrap,
        Margin = margin ?? default,
    };

    public static TextBlock StatValue(string text = "") => new()
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        Foreground = AccentBrush,
    };

    public static SectionPanel Section(Control header, Control content) => new(header, content);

    public static TextBlock Heading(string text, IBrush? brush = null) => new()
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = brush ?? AccentBrush,
    };

    public static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}

internal sealed class SectionPanel : Border
{
    private readonly Border _body;
    private readonly TextBlock _chevron;

    public bool IsExpanded
    {
        get => _body.IsVisible;
        set
        {
            _body.IsVisible = value;
            _chevron.Text = value ? "v" : ">";
        }
    }

    public SectionPanel(Control header, Control content)
    {
        Background = AppTheme.PanelBrush;
        CornerRadius = new CornerRadius(6);
        Margin = new Thickness(0, 2, 0, 0);

        _chevron = new TextBlock
        {
            Text = ">",
            Foreground = AppTheme.DimBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        headerGrid.Children.Add(header);
        Grid.SetColumn(_chevron, 1);
        headerGrid.Children.Add(_chevron);

        var headerBorder = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = headerGrid,
        };
        headerBorder.PointerPressed += (_, args) =>
        {
            if (args.Source is Button or ToggleButton) return;
            IsExpanded = !IsExpanded;
            args.Handled = true;
        };

        _body = new Border
        {
            Padding = new Thickness(10, 0, 10, 8),
            Child = content,
            IsVisible = false,
        };

        Child = new StackPanel
        {
            Children =
            {
                headerBorder,
                _body,
            },
        };
    }
}
