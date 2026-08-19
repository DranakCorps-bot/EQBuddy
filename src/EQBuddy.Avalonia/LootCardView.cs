using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// The widget's Loot card on Linux and macOS — the mirror of the WPF <c>LootCardView</c>,
/// built from the same <see cref="LootPresentation"/> and the same <see cref="LootRows"/>
/// (Gate 4, docs/DesignSystem.md §11.5).
///
/// This lane had fallen a whole feature behind. #198 gave the Windows card a show filter,
/// a sort strip and inline provenance on 2026-08-17; here the card still listed
/// <c>s.Loot</c> raw, with merges in a separate "Created by merging" block below it and
/// no way to tell a foraged root from a corpse drop. The shared row builder existed the
/// whole time and this UI simply never called it — which is the same shape as the chip
/// stacks carrying #122 and #152 to Linux after Windows had already paid for both.
///
/// So this is the one place in Gate 4 where behaviour changes, and it changes toward the
/// other UI: the two filters, the provenance tags, the timeline sort, and the empty-slice
/// wording all arrive here at once, reading the same two settings so a profile shared
/// between a Windows and a Linux machine behaves the same on both.
///
/// The card's chrome and header stay with the window — thirteen cards wear them and they
/// change together, in Gate 5.
/// </summary>
internal sealed class LootCardView
{
    private readonly MainWindow _w;
    private readonly AppSettings _settings;

    private readonly StackPanel _viewGroup = Group();
    private readonly StackPanel _sortGroup = Group();
    private readonly EqSegmentedStrip _views;
    private readonly EqSegmentedStrip _sorts;

    private readonly ItemsControl _rows = new();

    private readonly StackPanel _targetBlock = new() { IsVisible = false };
    private readonly TextBlock _targetHeading;
    private readonly ItemsControl _targetRows = new();

    /// <summary>The card's body — what the Loot section hangs inside its expander.</summary>
    public Control Body { get; }

    /// <summary>Rendered row count, for the <c>EQBUDDY_EXPAND</c> dump.</summary>
    public int RowCount => (_rows.ItemsSource as ICollection<Control>)?.Count ?? 0;

    public LootCardView(MainWindow w, AppSettings settings)
    {
        _w = w;
        _settings = settings;

        // One outer WrapPanel so a narrow widget breaks BETWEEN the groups rather than
        // mid-strip: "sort:" separated from its own chips would read as a heading.
        var strips = new WrapPanel { Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXs) };
        _viewGroup.Margin = new Thickness(0, 0, DesignTokens.SpaceL, 0);
        strips.Children.Add(_viewGroup);
        strips.Children.Add(_sortGroup);

        _views = BuildStrip(_viewGroup, "show:", LootPresentation.Views, key =>
        {
            _settings.LootView = key;
            _settings.Save();
            _w.RepaintLootCard();
        });
        _sorts = BuildStrip(_sortGroup, "sort:", LootPresentation.Sorts, key =>
        {
            _settings.LootSort = key;
            _settings.Save();
            // Repaint this card alone: a full refresh recomputes nothing new and repaints
            // every card and the whole mobile projection.
            _w.RepaintLootCard();
        });

        _targetBlock.Margin = new Thickness(0, DesignTokens.SpaceS, 0, 0);
        _targetHeading = DesignSystem.Text(DesignTokens.TypeRole.Caption);
        _targetHeading.TextWrapping = TextWrapping.Wrap;
        _targetHeading.FontWeight = FontWeight.SemiBold;
        _targetHeading.Foreground = AppTheme.BrushFor("WarnBrush");
        _targetBlock.Children.Add(IconLine("Target", "WarnBrush", _targetHeading));
        _targetRows.Margin = new Thickness(DesignTokens.SpaceM, DesignTokens.SpaceXxs, 0, 0);
        _targetBlock.Children.Add(_targetRows);

        var body = new StackPanel();
        body.Children.Add(strips);
        body.Children.Add(_rows);
        body.Children.Add(_targetBlock);
        Body = body;
    }

    // ---- composition ----

    private static StackPanel Group() => new() { Orientation = Orientation.Horizontal };

    private static EqSegmentedStrip BuildStrip(Panel group, string label,
        IReadOnlyList<LootPresentation.Option> options, Action<string> onPick)
    {
        var caption = DesignSystem.Text(DesignTokens.TypeRole.Caption, label);
        caption.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
        caption.VerticalAlignment = VerticalAlignment.Center;
        group.Children.Add(caption);

        var host = new StackPanel { Orientation = Orientation.Horizontal };
        group.Children.Add(host);

        var strip = new EqSegmentedStrip(host);
        foreach (var option in options)
        {
            var key = option.Key;
            strip.Add(option.Label, key, tip: option.Tip, onClick: () => onPick(key));
        }
        return strip;
    }

    /// <summary>An icon beside wrapping text, as a two-column Grid — never a horizontal
    /// StackPanel, which measures its children with infinite width so the text is clipped
    /// rather than wrapped (trap 14 in CLAUDE.md).</summary>
    private static Grid IconLine(string icon, string colorKey, Control text)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        var glyph = DesignSystem.Icon(icon, colorKey, size: DesignTokens.IconInline);
        glyph.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
        glyph.VerticalAlignment = VerticalAlignment.Top;
        grid.Children.Add(glyph);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    // ---- painting ----

    /// <summary>Paints the card from a snapshot. Every decision about WHAT to show comes
    /// from <see cref="LootPresentation"/>, which the WPF card and the WPF breakout read
    /// too — the point of the shared layer is that this window cannot answer differently
    /// even by accident.</summary>
    public void Render(StatsSnapshot s)
    {
        var plan = LootPresentation.Build(s.Loot, s.Crafted, s.Fashioned, s.RecentLoot,
            _settings.LootView, _settings.LootSort);

        _viewGroup.IsVisible = plan.ShowViewStrip;
        _sortGroup.IsVisible = plan.ShowSortStrip;
        _views.Select(plan.View);
        _sorts.Select(plan.Sort);
        // "recent" is withheld rather than disabled when nothing on screen carries a
        // timestamp: a chip that does nothing is worse than a chip that isn't there.
        if (_sorts.Chip(LootPresentation.SortRecent) is { } recent)
            recent.IsVisible = plan.ShowRecent;

        if (plan.EmptyNote is { } note)
        {
            // The chosen slice is empty — NAME it rather than blanking, or an applied
            // filter reads as a broken card.
            var line = DesignSystem.Text(DesignTokens.TypeRole.Body, note);
            line.Foreground = AppTheme.BrushFor("DimBrush");
            line.Margin = new Thickness(0, 1, 0, 1);
            _rows.ItemsSource = new List<Control> { line };
        }
        else _rows.ItemsSource = plan.Rows.Select(BuildRow).ToList();
    }

    /// <summary>One loot row: the item, its provenance, and the value column — plus the
    /// quest badge, drawn as a vector. The emoji it replaces is exactly the kind that
    /// failed to render in Wine prefixes (#148, #166), on this build.</summary>
    private Control BuildRow(LootRow row) => BuildRow(row.Item, row.Value, row.Tag);

    private Control BuildRow(string item, string value, string? tag)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var name = DesignSystem.Text(DesignTokens.TypeRole.Body);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.Margin = new Thickness(0, 1, DesignTokens.SpaceM, 1);
        if (LootPresentation.Note(tag) is { } note)
        {
            // Provenance is a separate run, not part of the name, so a click still looks
            // the base item up.
            name.Inlines = [
                new Run(item),
                new Run($" {note}")
                {
                    FontSize = MetadataSize,
                    Foreground = AppTheme.BrushFor("DimBrush"),
                },
            ];
        }
        else name.Text = item;

        if (_w.QuestAwareTooltip(item, _w.ItemHoverStats(item)) is { Length: > 0 } tip)
        {
            var tipText = new TextBlock
            {
                Text = tip,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = DesignTokens.TipWidth,
                Foreground = AppTheme.BrushFor("TextBrush"),
            };
            // Multi-line tips are stat blocks — monospace keeps their columns readable.
            if (tip.Contains('\n')) tipText.FontFamily = MainWindow.MonoFamily;
            ToolTip.SetTip(name, tipText);
        }
        else ToolTip.SetTip(name, "Click for item info (eqlwiki)");

        name.Cursor = new Cursor(StandardCursorType.Hand);
        name.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(name).Properties.IsLeftButtonPressed) return;
            e.Handled = true;
            _w.ShowItemInfo(item);
        };
        grid.Children.Add(name);

        if (_w.IsActiveQuestItem(item))
        {
            // Click → the Quest Tracker filtered to this item's quests; the item's own
            // name still opens its wiki page (David's shape, 2026-08-07). A button rather
            // than a handled vector, so the whole square is clickable (#211).
            var badge = DesignSystem.InlineIconButton("Map",
                "Part of a quest — click for its quest info",
                () => _w.OpenQuestInfoForItem(item), "GoodBrush");
            badge.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
            Grid.SetColumn(badge, 1);
            grid.Children.Add(badge);
        }

        var right = DesignSystem.Text(DesignTokens.TypeRole.Body, value);
        right.Foreground = AppTheme.BrushFor("DimBrush");
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);
        return grid;
    }

    // ---- target drops (TARGET-*) ----

    /// <summary>What the creature you're fighting can drop. The window still gathers it —
    /// merging the wiki's list with this session's observed counts is its job and its
    /// cache — and hands the two halves of the heading here, so the sentence is composed
    /// once, in <see cref="LootPresentation"/>, for every surface that draws it.</summary>
    public void ShowTargetDrops(string names, string detail,
        IEnumerable<(string Name, string Value)> rows)
    {
        _targetBlock.IsVisible = true;
        _targetHeading.Text = LootPresentation.TargetHeading(names, detail);
        _targetRows.ItemsSource = rows.Select(r => BuildRow(r.Name, r.Value, null)).ToList();
    }

    public void HideTargetDrops() => _targetBlock.IsVisible = false;

    private static readonly double MetadataSize =
        DesignTokens.Spec(DesignTokens.TypeRole.Metadata).Size;

}
