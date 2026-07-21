using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;

namespace EQBuddy;

/// <summary>Details!-style bar rows shared by the live widget and the History window.</summary>
internal static class BreakdownRows
{
    public static SolidColorBrush BarBrush(FrameworkElement resources)
    {
        var accent = ((SolidColorBrush)resources.FindResource("AccentBrush")).Color;
        return new SolidColorBrush(Color.FromArgb(0x2E, accent.R, accent.G, accent.B));
    }

    /// <summary>One breakdown row: a bar sized to frac behind "name … value".</summary>
    public static Grid Row(FrameworkElement resources, string name, string value, double frac,
        Brush barBrush, string? tooltip)
    {
        frac = Math.Clamp(frac, 0.004, 1.0);
        var row = new Grid { Margin = new Thickness(0, 1, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
        var bar = new Border
        {
            Background = barBrush, CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left, Width = 0,
        };
        // Star columns collapse under infinite measure, so size the bar explicitly.
        row.SizeChanged += (_, se) => bar.Width = Math.Max(0, se.NewSize.Width * frac);
        row.Children.Add(bar);

        var content = new Grid { Margin = new Thickness(4, 1, 0, 1) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(new TextBlock
        {
            Text = name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = (Brush)resources.FindResource("TextBrush"),
        });
        var right = new TextBlock
        {
            Text = value, FontSize = 11, Foreground = (Brush)resources.FindResource("DimBrush"),
            Margin = new Thickness(8, 1, 2, 0),
        };
        Grid.SetColumn(right, 1);
        content.Children.Add(right);
        row.Children.Add(content);
        if (tooltip is not null) row.ToolTip = tooltip;
        return row;
    }

    /// <summary>Fill an ItemsControl with ability rows (ordered by total): the standard
    /// "total · ×hits · avg · rate (· crit%)" columns with share bars.</summary>
    public static void FillAbilityRows(FrameworkElement resources, ItemsControl list,
        IReadOnlyList<SourceDamage> stats, string unit, string rateLabel, int max = int.MaxValue)
    {
        list.Items.Clear();
        if (stats.Count == 0) return;
        var grand = Math.Max(1, stats.Sum(d => d.Total));
        var top = Math.Max(1, stats.Max(d => d.Total));
        var barBrush = BarBrush(resources);
        foreach (var d in stats.Take(max))
        {
            var critPart = d.Crits > 0 ? $" · {100.0 * d.Crits / Math.Max(1, d.Hits):0}% crit" : "";
            var ratePart = d.ActiveSeconds > 0
                ? $" · {d.Total / Math.Max(1, d.ActiveSeconds):0.#} {rateLabel}" : "";
            var value = $"{d.Total:N0} · ×{d.Hits} · avg {(double)d.Total / Math.Max(1, d.Hits):0.#}{ratePart}{critPart}";
            var tooltip = $"{100.0 * d.Total / grand:0.#}% of total" +
                (d.ActiveSeconds > 0
                    ? $" · {rateLabel} = total ÷ ~{d.ActiveSeconds:0}s this {unit} was in use" : "");
            list.Items.Add(Row(resources, d.Name, value, (double)d.Total / top, barBrush, tooltip));
        }
    }
}
