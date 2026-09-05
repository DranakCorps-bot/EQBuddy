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

        // The name is sized to its CONTENT and capped; the context takes what is left
        // (BreakdownRowLayout, #182 and its over-correction). Two defects, one column
        // pair: the context was once Auto and starved the name to "." and ".."; then both
        // were proportional and a five-letter name held two thirds of the row while the
        // stat line was cut mid-number. Auto + NameCap is the shape that has neither.
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });
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
        // The cap, applied as the row learns how wide it is. Safe against a layout loop:
        // the content Grid is stretched by its parent, so its width is decided above this
        // and setting a child's MaxWidth cannot feed back into it.
        content.SizeChanged += (_, e) =>
            nameBlock.MaxWidth = BreakdownRowLayout.NameCap(e.NewSize.Width);
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
                // Right-aligned inside its own flexible column, so a short context still
                // sits against the headline exactly as it did when the column was Auto.
                HorizontalAlignment = HorizontalAlignment.Right,
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

        // Hover gives the WHOLE row back, always — the other half of what Ladylag asked
        // for (#182): trimming is right, because a long name must never push the numbers
        // off the row, but a trimmed name with no way to read it is not. On the ROW
        // rather than on the name, so it cannot shadow the caller's own richer tooltip;
        // that one is appended instead.
        row.ToolTip = BreakdownRowLayout.HoverText(name, context, tooltip);
        return row;
    }

    /// <summary>Multi-line tooltips are stat blocks — monospace keeps their columns
    /// readable. A static family because the item catalog makes that branch always-taken,
    /// and a fresh <c>FontFamily</c> per row per render second was measurable churn
    /// (2026-08-13 review).</summary>
    private static readonly FontFamily MonoFamily = new("Consolas");

    /// <summary>
    /// The plain name/value list the un-migrated card BODIES still draw with — the
    /// breakdown lists, the ding unlocks, deaths and zones. <c>EqCardRows</c> is what
    /// replaced it for every card that has been through the seam; the rest is a later
    /// batch (docs/DesignSystem.md §11.9).
    ///
    /// **It lived in <c>MainWindow</c> until E-3's Live room needed it too**, and the
    /// second consumer is the whole argument for the move rather than a copy: the Damage
    /// tab draws the same procs, stances, area-spell and damage-taken lists the Combat card
    /// draws, and two builders for one row shape would drift the way every pair in this
    /// repo eventually has (trap 33). It takes the host as <c>resources</c>, exactly as
    /// every other method here does, because the brushes are resource lookups and a room is
    /// as legitimate a resource scope as a window.
    ///
    /// It used to take a <c>questBadges</c> flag that hung a quest map-pin on each item
    /// row. Nothing has passed it since the Loot card moved onto <c>EqCardRows</c>, which
    /// draws that badge itself — so the branch was unreachable, and it was unreachable in
    /// the #211 shape: a bare clickable vector with holes you could click through. Dead
    /// code carrying a bug already paid for is worse than no code.
    /// </summary>
    public static void FillPairRows(FrameworkElement resources, ItemsControl list,
        IEnumerable<(string Name, string Value)> rows,
        Func<string, Brush>? valueBrush = null, Action<string>? onNameClick = null,
        Func<string, string?>? tooltip = null, Func<string, Brush?>? nameBrush = null,
        Func<string, string?>? noteFor = null)
    {
        var items = rows.ToList();
        list.Items.Clear();
        foreach (var (name, value) in items)
        {
            // A GRID and never a horizontal StackPanel: a stack measures its children with
            // infinite width, so the value would be pushed off the edge and the name would
            // be clipped with no ellipsis to say so (trap 14).
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new TextBlock
            {
                FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Body).Size,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = nameBrush?.Invoke(name)
                    ?? (Brush)resources.FindResource("TextBrush"),
                Margin = new Thickness(0, 1, DesignTokens.SpaceM, 1),
            };
            // Provenance rides inline as a muted "(Foraged)"/"(Crafted)"/… after the name —
            // a separate run, not part of the name, so the click still looks up the base item.
            if (noteFor?.Invoke(name) is { Length: > 0 } note)
            {
                left.Inlines.Add(new System.Windows.Documents.Run(name));
                left.Inlines.Add(new System.Windows.Documents.Run($" {note}")
                {
                    FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Caption).Size,
                    Foreground = (Brush)resources.FindResource("DimBrush"),
                });
            }
            else left.Text = name;
            if (tooltip?.Invoke(name) is { Length: > 0 } tip)
            {
                var tipText = new TextBlock { Text = tip, TextWrapping = TextWrapping.Wrap, MaxWidth = 340 };
                if (tip.Contains('\n')) tipText.FontFamily = MonoFamily;
                left.ToolTip = new ToolTip { Content = tipText };
            }
            if (onNameClick is not null)
            {
                var clickName = name;
                left.Cursor = System.Windows.Input.Cursors.Hand;
                left.ToolTip ??= "Click for item info (eqlwiki)";
                // Swallow the down so it can't start a window DragMove and eat the Up
                // (the discussion #46 failure mode, same fix as the breakout rows).
                left.MouseLeftButtonDown += (_, ev) => ev.Handled = true;
                left.MouseLeftButtonUp += (_, _) => onNameClick(clickName);
            }
            var right = new TextBlock
            {
                Text = value,
                FontSize = DesignTokens.Spec(DesignTokens.TypeRole.Body).Size,
                Foreground = valueBrush?.Invoke(value)
                    ?? (Brush)resources.FindResource("DimBrush"),
            };
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);
            list.Items.Add(grid);
        }
    }

    /// <summary>A Total/Count/Avg stat list in the chosen sort order — "Damage you took",
    /// on the widget's Combat card and on the Live room's Damage tab. Lifted with
    /// <see cref="FillPairRows"/> and for the same reason.</summary>
    public static void FillStatRows(FrameworkElement resources, ItemsControl list,
        IEnumerable<SourceDamage> stats, StatSort sort, string unit)
    {
        var sorted = sort switch
        {
            StatSort.Hits => stats.OrderByDescending(d => d.Hits),
            StatSort.Avg => stats.OrderByDescending(d => (double)d.Total / d.Hits),
            _ => stats.OrderByDescending(d => d.Total),
        };
        FillPairRows(resources, list, sorted.Select(d =>
            (d.Name, $"{d.Total:N0} · {d.Hits} {unit}{(d.Hits == 1 ? "" : "s")} · avg {(double)d.Total / d.Hits:0.#}")));
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
