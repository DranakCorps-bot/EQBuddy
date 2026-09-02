using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Draws <see cref="CardRow"/>s (Gate 5b) — the one place the widget's name-and-value row
/// is built.
///
/// It replaces `MainWindow.FillList` and the per-surface copies that grew beside it, and
/// it exists before the remaining cards are converted rather than after, because twelve
/// cards each inventing a row is precisely how the app arrived at 174 distinct spacing
/// tuples in the first place.
///
/// Item behaviour — the wiki popup, the cached-stats hover, the quest badge — comes in
/// through <see cref="ICardContext"/> and applies only to rows flagged
/// <see cref="CardRow.Item"/>. A card that shows no items passes no context and needs no
/// window to test.
/// </summary>
internal static class EqCardRows
{
    /// <param name="tooltip">Hover text per row, or null. A LOOKUP rather than a field on
    /// <see cref="CardRow"/> on purpose: the answer is resolved per SET, not per row — in
    /// a merged unlock list the same name means one thing among the spells and another
    /// among the AAs, and only the set it came from can say which
    /// (<see cref="UI.Shared.LevelUnlockRows.Tooltip"/>). A field would have to be filled
    /// in by whoever built the rows, which is the same knowledge in two places.</param>
    /// <param name="onNameClick">Click per row, or null — same reasoning. This pair is what
    /// <c>MainWindow.FillList</c> had and this did not, and it is the whole reason the
    /// Progress, Combat and Healing bodies could not move onto this routine.</param>
    public static void Fill(ItemsControl list, IEnumerable<CardRow> rows,
        ICardContext? context = null,
        Func<string, string?>? tooltip = null,
        Action<string>? onNameClick = null)
    {
        list.Items.Clear();
        foreach (var row in rows) list.Items.Add(Build(row, context, tooltip, onNameClick));
    }

    public static Grid Build(CardRow row, ICardContext? context = null,
        Func<string, string?>? tooltip = null,
        Action<string>? onNameClick = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = DesignSystem.Text(DesignTokens.TypeRole.Body);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.Margin = new Thickness(row.Indent ? DesignTokens.Indent : 0, 1,
            DesignTokens.SpaceM, 1);
        if (row.Note is { Length: > 0 } note)
        {
            // A separate run, so the name a click looks up is unchanged.
            name.Inlines.Add(new Run(row.Name));
            var tag = new Run($" ({note})") { FontSize = MetadataSize };
            tag.SetResourceReference(TextElement.ForegroundProperty, "DimBrush");
            name.Inlines.Add(tag);
        }
        else name.Text = row.Name;
        // A trimmed name says its full self on hover (#182). An item overwrites this with
        // its stat block below, which is strictly more information.
        name.ToolTip = row.Name;

        if (row.Item && context is not null) MakeItem(name, row.Name, context);

        // An explicit lookup wins over the trimmed-name fallback, and is applied AFTER
        // MakeItem so a caller that knows more than the item stats can say so. A null or
        // empty answer leaves whatever was there — "no tooltip for this row" must not
        // silently delete the full-name hover that #182 exists for.
        // The row's OWN tip wins over the name-keyed lookup: a list whose names repeat
        // (Level-ups, #240) cannot be answered by a function of the name alone.
        if ((row.Tip ?? tooltip?.Invoke(row.Name)) is { Length: > 0 } rowTip)
        {
            var text = new TextBlock
            {
                Text = rowTip, TextWrapping = TextWrapping.Wrap, MaxWidth = DesignTokens.TipWidth,
            };
            name.ToolTip = new ToolTip { Content = text };
        }
        if (onNameClick is { } click)
        {
            name.Cursor = System.Windows.Input.Cursors.Hand;
            DesignSystem.WireClick(name, () => click(row.Name));
        }
        grid.Children.Add(name);

        if (row.Item && context is { } ctx && QuestBadge(ctx, row.Name) is { } badge)
        {
            Grid.SetColumn(badge, 1);
            grid.Children.Add(badge);
        }

        var value = DesignSystem.Text(DesignTokens.TypeRole.Body, row.Value)
            .Ink(row.ValueInk ?? "DimBrush");
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        return grid;
    }

    /// <summary>Click for the wiki popup, hover for the cached stats.</summary>
    private static void MakeItem(TextBlock name, string item, ICardContext context)
    {
        if (context.QuestAwareTooltip(item, context.ItemHoverStats(item)) is { Length: > 0 } tip)
        {
            var text = new TextBlock { Text = tip, TextWrapping = TextWrapping.Wrap, MaxWidth = DesignTokens.TipWidth };
            // Multi-line tips are stat blocks — monospace keeps their columns readable.
            if (tip.Contains('\n')) text.FontFamily = MainWindow.MonoFamily;
            name.ToolTip = new ToolTip { Content = text };
        }
        // The press-guarded contract (#46 + the mid-gesture rebuild race) — one home
        // for every clickable list element, LootCardView's rows included.
        DesignSystem.WireClick(name, () => context.ShowItemInfo(item));
    }

    /// <summary>The quest marker beside an item: click for the Quest Tracker filtered to
    /// this item's quests; the item's own name still opens its wiki page (David's shape,
    /// 2026-08-07). The one copy — the Loot card and the Loot breakout drew their own
    /// until #211, and drawing a badge in three places is how a badge ends up clickable
    /// in one of them.</summary>
    internal static UIElement? QuestBadge(ICardContext context, string item)
    {
        if (!context.IsActiveQuestItem(item)) return null;
        var badge = DesignSystem.InlineIconButton("Map",
            "Part of a quest — click for its quest info",
            (_, _) => context.OpenQuestInfoForItem(item), "GoodBrush");
        badge.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
        return badge;
    }

    private static readonly double MetadataSize =
        DesignTokens.Spec(DesignTokens.TypeRole.Metadata).Size;

}
