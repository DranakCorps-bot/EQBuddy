using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>Sort order for ability/heal breakdown lists — shared by the main cards and
/// the breakout windows.</summary>
internal enum StatSort { Total, Hits, Avg, Rate }

/// <summary>Details!-style bar rows shared by the live widget and the History window.</summary>
internal static class BreakdownRows
{
    /// <summary>The bar fill: accent gradient, deep→bright left to right — depth
    /// without a second palette row (2026-08-11 modernization).</summary>
    public static Brush BarBrush(FrameworkElement resources)
    {
        var accent = ((SolidColorBrush)resources.FindResource("AccentBrush")).Color;
        var deep = ((SolidColorBrush)resources.FindResource("AccentDeepBrush")).Color;
        var g = new LinearGradientBrush(deep, accent, 0.0);
        g.Freeze();
        return g;
    }

    /// <summary>One breakdown row, 2026-08-11 layout: the bar is an UNDERLINE, not a
    /// background — text stays crisp, comparison stays instant (bars behind text were
    /// the old look's biggest source of mud). The value string's first " · " segment
    /// is the headline and sits hard-right in semibold; the rest reads dim beside it.
    /// Callers keep the same signature — every card and breakout upgrades at once.</summary>
    public static Grid Row(FrameworkElement resources, string name, string value, double frac,
        Brush barBrush, string? tooltip, Brush? nameBrush = null, UIElement? nameBadge = null,
        string? nameNote = null)
    {
        frac = Math.Clamp(frac, 0.01, 1.0);
        var row = new Grid { Margin = new Thickness(0, 2, 0, 3), HorizontalAlignment = HorizontalAlignment.Stretch };
        row.RowDefinitions.Add(new RowDefinition());
        row.RowDefinitions.Add(new RowDefinition());

        var sep = value.IndexOf(" · ", StringComparison.Ordinal);
        var primary = sep < 0 ? value : value[..sep];
        var context = sep < 0 ? "" : value[(sep + 3)..];

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var nameBlock = new TextBlock
        {
            FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = nameBrush ?? (Brush)resources.FindResource("TextBrush"),
        };
        // A muted inline note after the name (e.g. "(Foraged)") — a separate run, so the
        // name it stands beside is unchanged for click/lookup.
        if (nameNote is { Length: > 0 })
        {
            nameBlock.Inlines.Add(new System.Windows.Documents.Run(name));
            nameBlock.Inlines.Add(new System.Windows.Documents.Run($" {nameNote}")
            {
                FontSize = 10.5, Foreground = (Brush)resources.FindResource("DimBrush"),
            });
        }
        else nameBlock.Text = name;
        content.Children.Add(nameBlock);
        if (nameBadge is not null)
        {
            Grid.SetColumn(nameBadge, 1);
            content.Children.Add(nameBadge);
        }
        if (context.Length > 0)
        {
            var ctx = new TextBlock
            {
                Text = context, FontSize = 10, Foreground = (Brush)resources.FindResource("DimBrush"),
                Margin = new Thickness(8, 1.5, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(ctx, 2);
            content.Children.Add(ctx);
        }
        var headline = new TextBlock
        {
            Text = primary, FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)resources.FindResource("TextBrush"),
            Margin = new Thickness(10, 0, 2, 0),
        };
        Grid.SetColumn(headline, 3);
        content.Children.Add(headline);
        row.Children.Add(content);

        // The under-bar: full-width whisper track, accent-gradient fill to frac.
        var track = new Grid { Margin = new Thickness(0, 3, 2, 0), Height = 3 };
        track.Children.Add(new Border
        {
            Background = (Brush)resources.FindResource("TrackBrush"),
            CornerRadius = new CornerRadius(1.5),
        });
        var fill = new Border
        {
            Background = barBrush, CornerRadius = new CornerRadius(1.5),
            HorizontalAlignment = HorizontalAlignment.Left, Width = 0,
        };
        track.Children.Add(fill);
        // Star columns collapse under infinite measure, so size the fill explicitly.
        track.SizeChanged += (_, se) => fill.Width = Math.Max(0, se.NewSize.Width * frac);
        Grid.SetRow(track, 1);
        row.Children.Add(track);

        if (tooltip is not null) row.ToolTip = tooltip;
        return row;
    }

    /// <summary>Render pre-built shared-presentation rows (HistoryPresentation).</summary>
    public static void FillRows(FrameworkElement resources, ItemsControl list,
        IEnumerable<HistoryBreakdownRow> rows)
    {
        list.Items.Clear();
        var barBrush = BarBrush(resources);
        foreach (var r in rows)
            list.Items.Add(Row(resources, r.Name, r.Value, r.Fraction, barBrush, r.Tooltip));
    }

    /// <summary>Fill an ItemsControl with ability rows (ordered by total): the standard
    /// "total · ×hits · avg · rate (· crit%)" columns with share bars. Rate uses the
    /// parser convention (ability total ÷ time in combat); burst is in the tooltip.</summary>
    public static void FillAbilityRows(FrameworkElement resources, ItemsControl list,
        IReadOnlyList<SourceDamage> stats, double combatSeconds, string rateLabel,
        int max = int.MaxValue) =>
        FillAbilityRowsSorted(resources, list, stats, StatSort.Total, combatSeconds, rateLabel, max);

    /// <summary>The sorted flavor (hoisted from MainWindow.FillBreakdown when the breakout
    /// windows grew sort bars): rows AND bars follow the chosen metric, so what's sorted
    /// biggest is also drawn longest.</summary>
    public static void FillAbilityRowsSorted(FrameworkElement resources, ItemsControl list,
        IEnumerable<SourceDamage> stats, StatSort sort, double combatSeconds, string rateLabel,
        int max = int.MaxValue,
        IReadOnlyDictionary<string, (int Casts, int Resists, int Blocked)>? resists = null,
        IReadOnlyDictionary<string, string>? blockedBy = null)
    {
        var secs = Math.Max(1, combatSeconds);
        double Rate(SourceDamage d) => d.Total / secs;
        static double Avg(SourceDamage d) => (double)d.Total / Math.Max(1, d.Hits);
        var sorted = (sort switch
        {
            StatSort.Hits => stats.OrderByDescending(d => d.Hits),
            StatSort.Avg => stats.OrderByDescending(Avg),
            StatSort.Rate => stats.OrderByDescending(Rate),
            _ => stats.OrderByDescending(d => d.Total),
        }).ToList();
        list.Items.Clear();
        if (sorted.Count == 0) return;
        var grand = Math.Max(1, sorted.Sum(d => d.Total));
        Func<SourceDamage, double> metric = sort switch
        {
            StatSort.Hits => d => d.Hits,
            StatSort.Avg => Avg,
            StatSort.Rate => Rate,
            _ => d => d.Total,
        };
        var topMetric = Math.Max(1e-9, sorted.Max(metric));
        var barBrush = BarBrush(resources);
        // Overflow is said out loud, never silently truncated: a capped list that
        // looks complete would misstate the session (the no-silent-caps rule).
        var overflow = sorted.Count - max;
        foreach (var d in sorted.Take(max))
        {
            var critPart = d.Crits > 0 ? $" · {100.0 * d.Crits / Math.Max(1, d.Hits):0}% crit" : "";
            // Resist share on session spell rows (#102, jeremycranfill — "do I need to
            // switch to overchannel?"). Capped at 100: one AoE cast can log several
            // resist lines, so the raw counts live in the tooltip.
            var resistPart = "";
            var resistTip = "";
            if (resists is not null
                && resists.TryGetValue(SpellCatalog.BaseName(d.Name), out var rr))
            {
                if (rr.Resists > 0)
                {
                    resistPart = $" · {Math.Min(100, 100.0 * rr.Resists / Math.Max(1, rr.Casts)):0}% resist";
                    resistTip = $" · {rr.Resists} resist{(rr.Resists == 1 ? "" : "s")} across {rr.Casts} casts this session";
                }
                // Stacking blocks ("did not take hold") ride the same lookup: a raw
                // count, not a % — one occupied slot blocks every re-cast, and a
                // percentage would read like a resist rate. The ledger names the
                // blocker(s) in the tooltip when it knows them.
                if (rr.Blocked > 0)
                {
                    resistPart += $" · {rr.Blocked} blocked";
                    resistTip += blockedBy is not null
                        && blockedBy.TryGetValue(SpellCatalog.BaseName(d.Name), out var blockers)
                        ? $" · {blockers}"
                        : $" · {rr.Blocked} cast{(rr.Blocked == 1 ? "" : "s")} did not take hold (another buff held the slot)";
                }
            }
            // Miss % out of ATTEMPTS (hits + misses), the number a player means by it;
            // melee only — a spell's failure is its resist %, never both.
            var missPart = d.Misses > 0
                ? $" · {100.0 * d.Misses / (d.Hits + d.Misses):0}% miss" : "";
            var rangePart = d.MaxHit > 0 ? $" · hits {d.MinHit:N0}–{d.MaxHit:N0}" : "";
            var value = $"{d.Total:N0} · ×{d.Hits} · avg {Avg(d):0.#} · {Rate(d):0.#} {rateLabel}{critPart}{missPart}{resistPart}";
            var tooltip = $"{100.0 * d.Total / grand:0.#}% of total{rangePart} · {rateLabel} = total ÷ {secs:0}s in combat" +
                (d.ActiveSeconds > 0
                    ? $" · burst {d.Total / Math.Max(1, d.ActiveSeconds):0.#}/s over the ~{d.ActiveSeconds:0}s it was in use"
                    : "") +
                (d.Misses > 0 ? $" · {d.Misses} miss{(d.Misses == 1 ? "" : "es")} in {d.Hits + d.Misses} attempts" : "")
                + resistTip;
            list.Items.Add(Row(resources, d.Name, value, metric(d) / topMetric, barBrush, tooltip));
        }
        if (overflow > 0)
            list.Items.Add(new TextBlock
            {
                Text = $"…{overflow} more (smaller) — sorting reorders the top; the breakout window shows all",
                FontSize = 10,
                Foreground = (Brush)resources.FindResource("DimBrush"),
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
    }
}
