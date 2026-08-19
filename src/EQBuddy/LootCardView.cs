using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// The widget's Loot card, on the design system (Gate 4, docs/DesignSystem.md §11.5).
///
/// It is a class rather than a block of XAML for the reason <c>QuestChecklistView</c> was:
/// <c>MainWindow.xaml.cs</c> cannot join <c>DesignRatchetTests.Migrated</c> until the whole
/// widget is migrated (that is Gate 5), so a surface migrated inside it is guarded by
/// nothing and drifts back. Lifted out, the Loot card is held to the scale from the day it
/// lands — which matters here more than anywhere, because this is the surface #198 rebuilt
/// by hand six hours after Gate 2 deleted the pattern from the Quest Tracker.
///
/// What it does NOT own: the card's chrome and header. Every card on the widget wears the
/// same <c>Section</c> expander and the same emoji-and-count header, and changing one of
/// thirteen would read as a bug rather than a migration. Gate 5 changes them together.
///
/// Functionality is unchanged. Same two filters writing the same two settings, the same
/// shared row builder, the same click / hover / quest-badge behaviour, the same
/// target-drops block. What changed is that the filters are the app's chip primitive
/// instead of a seventeenth hand-built copy, the sizes come from the scale, and the two
/// glyphs are vectors.
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

    private readonly StackPanel _targetBlock = new() { Visibility = Visibility.Collapsed };
    private readonly TextBlock _targetHeading;
    private readonly ItemsControl _targetRows = new();

    /// <summary>The card's body — what <c>MainWindow.xaml</c> hangs inside the Loot
    /// expander.</summary>
    public UIElement Body { get; }

    /// <summary>Rendered row count, for the <c>EQBUDDY_EXPAND</c> dump the E2E suite
    /// asserts on. It was <c>LootList.Items.Count</c> when the list was a named control in
    /// the window; the fact it reports is the same one.</summary>
    public int RowCount => _rows.Items.Count;

    public LootCardView(MainWindow w, AppSettings settings)
    {
        _w = w;
        _settings = settings;

        // The two filter strips. One outer WrapPanel so a narrow widget breaks BETWEEN
        // the groups rather than mid-strip: "sort:" separated from its own chips would
        // read as a heading over the show row.
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
            // Reorder just this card from the snapshot we already have — a full RefreshUi
            // recomputed nothing new (the memo hands back the same snapshot) and repainted
            // every card, both breakouts and the whole mobile projection. The sort is
            // microseconds; the repaint was the cost.
            _w.RepaintLootCard();
        });

        // Target drops (TARGET-*): what the creature you're fighting can drop, the wiki's
        // knowledge merged with this session's observed counts.
        _targetBlock.Margin = new Thickness(0, DesignTokens.SpaceS, 0, 0);
        _targetHeading = DesignSystem.Text(DesignTokens.TypeRole.Caption);
        _targetHeading.TextWrapping = TextWrapping.Wrap;
        _targetHeading.FontWeight = FontWeights.SemiBold;
        _targetHeading.Ink("WarnBrush");
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

    /// <summary>A labelled segmented strip. The label is a caption and the segments are
    /// <see cref="EqChip"/>, which is the whole point of the gate: this shape existed
    /// sixteen times in the two widget files as bare TextBlocks with a Tag and a click
    /// handler, coloured by a hand-written ApplyVisual.</summary>
    private static EqSegmentedStrip BuildStrip(Panel group, string label,
        IReadOnlyList<LootPresentation.Option> options, Action<string> onPick)
    {
        var caption = DesignSystem.Text(DesignTokens.TypeRole.Caption, label);
        // Semibold, matching the breakout's strips: these are labels, and the weight
        // is what separates them from the options they govern (LW, 2026-08-18).
        caption.FontWeight = FontWeights.SemiBold;
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
    /// rather than wrapped (trap 14 in CLAUDE.md; it shipped in Gate 2 and only a
    /// screenshot found it).</summary>
    private static Grid IconLine(string icon, string colorKey, UIElement text)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var glyph = DesignSystem.Icon(icon, colorKey, size: DesignTokens.IconInline);
        glyph.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
        glyph.VerticalAlignment = VerticalAlignment.Top;
        grid.Children.Add(glyph);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    // ---- painting ----

    /// <summary>Paints the card from a snapshot: the two strips, one row list (looted and
    /// made mixed under "all", or either alone) and the target-drops block. Every decision
    /// about WHAT to show comes from <see cref="LootPresentation"/>, which the breakout
    /// and the Avalonia card read too, so the four surfaces cannot drift.</summary>
    public void Render(StatsSnapshot s)
    {
        var plan = LootPresentation.Build(s.Loot, s.Crafted, s.Fashioned, s.RecentLoot,
            _settings.LootView, _settings.LootSort);

        _viewGroup.Visibility = plan.ShowViewStrip ? Visibility.Visible : Visibility.Collapsed;
        _sortGroup.Visibility = plan.ShowSortStrip ? Visibility.Visible : Visibility.Collapsed;
        _views.Select(plan.View);
        _sorts.Select(plan.Sort);
        // "recent" is withheld rather than disabled when nothing on screen carries a
        // timestamp: a chip that does nothing is worse than a chip that isn't there.
        if (_sorts.Chip(LootPresentation.SortRecent) is { } recent)
            recent.Visibility = plan.ShowRecent ? Visibility.Visible : Visibility.Collapsed;

        _rows.Items.Clear();
        if (plan.EmptyNote is { } note)
        {
            // The chosen slice is empty — NAME it rather than blanking, or an applied
            // filter reads as a broken card. It rides in the list so the row count the
            // E2E dump reports still describes what a player can see.
            var line = DesignSystem.Text(DesignTokens.TypeRole.Body, note).Ink("DimBrush");
            line.Margin = new Thickness(0, 1, 0, 1);
            _rows.Items.Add(line);
        }
        else
        {
            foreach (var row in plan.Rows) _rows.Items.Add(BuildRow(row));
        }

        RenderTargetDrops(s);
    }

    /// <summary>One loot row: the item, its provenance, and the value column — plus the
    /// quest badge, which is now a vector. It was an emoji map, and a glyph is a Wine bug
    /// waiting to happen on the two platforms that are EQBuddy's only uncontested ground
    /// (#148, #166) — as this test just proved by refusing the comment that said so.</summary>
    private Grid BuildRow(LootRow row)
    {
        object? tip = null;
        if (_w.QuestAwareTooltip(row.Item, _w.ItemHoverStats(row.Item)) is { Length: > 0 } text)
        {
            var tipText = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = DesignTokens.TipWidth };
            // Multi-line tips are stat blocks — monospace keeps their columns readable.
            if (text.Contains('\n')) tipText.FontFamily = MainWindow.MonoFamily;
            tip = new ToolTip { Content = tipText };
        }
        return ItemRow(_w, row.Item, row.Value, LootPresentation.Note(row.Tag),
            tip ?? "Click for item info (eqlwiki)", _w.ShowItemInfo);
    }

    /// <summary>THE item row — one builder for the Loot card, the Loot breakout and the
    /// Progress breakout (LW, 2026-08-18: "the more we bring the two windows together
    /// visually and operate from the same source, the better"). Name + provenance run +
    /// quest badge + dim value, flat: no share bar — these rows state facts, and the
    /// underline track under every breakout row read as clutter next to the card. What
    /// hover and click DO stays each caller's own (the card opens the in-app item info,
    /// the breakouts open the wiki); clicks ride <see cref="DesignSystem.WireClick"/>'s
    /// press-guard, because a breakout fold header rebuilds its list on mouse-down and
    /// an unguarded name would catch the stray up.</summary>
    internal static Grid ItemRow(MainWindow? w, string item, string value, string? note,
        object? tip, Action<string>? onNameClick)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = DesignSystem.Text(DesignTokens.TypeRole.Body);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.Margin = new Thickness(0, 1, DesignTokens.SpaceM, 1);
        if (note is { Length: > 0 })
        {
            // Provenance is a separate run, not part of the name, so a click still looks
            // the base item up (LW, 2026-08-17).
            name.Inlines.Add(new Run(item));
            var tag = new Run($" {note}") { FontSize = MetadataSize };
            tag.SetResourceReference(TextElement.ForegroundProperty, "DimBrush");
            name.Inlines.Add(tag);
        }
        else name.Text = item;
        if (tip is not null) name.ToolTip = tip;
        if (onNameClick is not null)
        {
            var clicked = item;
            DesignSystem.WireClick(name, () => onNameClick(clicked));
        }
        grid.Children.Add(name);

        if (w is not null && EqCardRows.QuestBadge(w, item) is { } badge)
        {
            Grid.SetColumn(badge, 1);
            grid.Children.Add(badge);
        }

        var valueBlock = DesignSystem.Text(DesignTokens.TypeRole.Body, value).Ink("DimBrush");
        Grid.SetColumn(valueBlock, 2);
        grid.Children.Add(valueBlock);
        return grid;
    }


    private void RenderTargetDrops(StatsSnapshot s)
    {
        var (names, detail, rows) = _w.TargetDropsContent(s);
        if (names.Length == 0)
        {
            _targetBlock.Visibility = Visibility.Collapsed;
            return;
        }
        _targetBlock.Visibility = Visibility.Visible;
        _targetHeading.Text = LootPresentation.TargetHeading(names, detail);

        _targetRows.Items.Clear();
        foreach (var (name, value) in rows)
            _targetRows.Items.Add(BuildRow(new LootRow(name, value, null)));
    }

    // Sizes that are a role's size rather than a number: the ratchet wants no literals,
    // and a Run has no style to inherit one from.
    private static readonly double MetadataSize =
        DesignTokens.Spec(DesignTokens.TypeRole.Metadata).Size;

}
