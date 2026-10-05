using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EQBuddy;

/// <summary>
/// The way out of click-through: while the widget's clicks pass to the game, this tiny
/// normal-hit-test chip parks beside it — one click restores interaction. It exists
/// because the global hotkeys were removed (they ate Ctrl+Shift+T from every browser on
/// the machine — Reddit report, 2026-08-06), and a transparent window cannot, by
/// definition, be clicked back to life. Built in code like the chip windows; amber like
/// the click-through border so the two read as one state.
/// </summary>
internal sealed class ClickThroughChip : Window
{
    public ClickThroughChip(Action unlock)
    {
        Title = "EQBuddy click-through";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var text = new TextBlock { Text = "🔒 click-through", FontSize = 11 };
        text.SetResourceReference(TextBlock.ForegroundProperty, "WarnBrush");
        var chip = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(7, 2, 7, 3),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Click to turn click-through off",
            Child = text,
        };
        chip.SetResourceReference(Border.BackgroundProperty, "BgBrush");
        chip.SetResourceReference(Border.BorderBrushProperty, "WarnBrush");
        chip.MouseLeftButtonUp += (_, _) => unlock();
        Content = chip;
    }

    /// <summary>Parks just above the widget's top-left corner, on-screen guarded, at the
    /// player's Chips &amp; alerts size — it is a pill like the others, and it was the one
    /// floating chip that honoured neither size (#1046 audit, DRA-1048).</summary>
    public void ShowNear(Window main, double chipScale)
    {
        ChipScale.Apply(this, chipScale);
        Left = Math.Max(SystemParameters.WorkArea.Left, main.Left);
        Top = Math.Max(SystemParameters.WorkArea.Top, main.Top - 30);
        Show();
        // Chips & alerts size grows this chip (#1046), so park it by its MEASURED height
        // rather than the 1x guess above, which a 2x chip would overlap the widget by.
        if (ActualHeight > 0)
            Top = Math.Max(SystemParameters.WorkArea.Top, main.Top - ActualHeight - 6);
    }
}
