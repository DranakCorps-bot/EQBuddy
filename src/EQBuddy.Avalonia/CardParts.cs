using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EQBuddy.UI.Shared;
using Role = EQBuddy.UI.Shared.DesignTokens.TypeRole;

namespace EQBuddy.Avalonia;

/// <summary>
/// The two-column name/value row every surface on this lane draws, and the small parts
/// that go around it — the Avalonia twin of WPF's <c>CardParts</c>.
///
/// **Lifted out of <c>MainWindow</c> for PR A** (Fable 5, 2026-08-22), because a card that
/// lives in its own file cannot reach a private method in a 5,600-line window. It was
/// already the most-called helper in that file — 22 call sites — and the only thing tying
/// it to the host was the quest badge, which now comes through <see cref="ICardContext"/>
/// like everything else a card is allowed to ask for.
///
/// Nothing about the rows changed in the move. That is deliberate and it is checkable:
/// every existing <c>WidgetRenderTests</c> case had to pass unchanged, which is the
/// "the tabs draw what the cards drew" claim carried across the seam.
/// </summary>
internal static class CardParts
{
    internal static readonly FontFamily MonoFamily = new("monospace");

    /// <summary>The one-line body of a card that has nothing yet. The card stays where
    /// Options put it (David's verdict: "show what I've selected to see") and this says
    /// what will fill it — a card that hid itself would read as a missing feature.</summary>
    public static TextBlock EmptyLine(string text) => new()
    {
        Text = text,
        FontSize = DesignSystem.Size(Role.Caption),
        TextWrapping = TextWrapping.Wrap,
        Foreground = AppTheme.DimBrush,
        Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXxs),
    };

    /// <summary>
    /// Name on the left, value on the right, with the optional middle column carrying the
    /// quest badge.
    /// </summary>
    /// <param name="ctx">Only needed for <paramref name="questBadges"/> and item clicks.
    /// A surface with neither passes null and depends on nothing.</param>
    public static void FillList(ItemsControl list,
        IEnumerable<(string Name, string Value)> rows,
        ICardContext? ctx = null,
        Func<string, IBrush>? valueBrush = null, Action<string>? onNameClick = null,
        Func<string, string?>? tooltip = null, bool questBadges = false)
    {
        list.ItemsSource = rows.Select(row =>
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            var left = new TextBlock
            {
                Text = row.Name,
                FontSize = DesignSystem.Size(Role.Body),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = AppTheme.TextBrush,
                Margin = new Thickness(0, 1, DesignTokens.SpaceM, 1),
            };
            if (tooltip?.Invoke(row.Name) is { Length: > 0 } tip)
            {
                var tipText = new TextBlock
                {
                    Text = tip,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 340,
                    Foreground = AppTheme.TextBrush,
                };
                // Multi-line tips are stat blocks — monospace keeps their columns readable.
                if (tip.Contains('\n')) tipText.FontFamily = MonoFamily;
                ToolTip.SetTip(left, tipText);
            }
            if (onNameClick is not null)
            {
                var itemName = row.Name;
                left.Cursor = new Cursor(StandardCursorType.Hand);
                if (tooltip is null) ToolTip.SetTip(left, "Click for item info (eqlwiki)");
                left.PointerPressed += (_, e) =>
                {
                    if (!e.GetCurrentPoint(left).Properties.IsLeftButtonPressed) return;
                    onNameClick(itemName);
                    e.Handled = true;
                };
            }
            grid.Children.Add(left);
            if (questBadges && ctx is not null && ctx.IsActiveQuestItem(row.Name))
            {
                // 🗺 next to quest loot → the Quest Tracker, filtered to this item's
                // quests; each card's name opens the wiki walkthrough from there
                // (David's final shape, 2026-08-07: item click = item page, 🗺 = tracker).
                var badgeName = row.Name;
                // A BUTTON rather than a handled vector, so the whole square is clickable
                // (#211, n3cr0nk1tt3n): the map pin has a gap between its folds you could
                // click straight through. Same conversion LootCardView already made.
                var badge = DesignSystem.InlineIconButton("Map",
                    "Part of a quest — click for its quest info",
                    () => ctx.OpenQuestInfoForItem(badgeName), "GoodBrush");
                badge.Margin = new Thickness(0, 1, DesignTokens.SpaceS, 1);
                badge.PointerPressed += (_, e) =>
                {
                    if (!e.GetCurrentPoint(badge).Properties.IsLeftButtonPressed) return;
                    ctx.OpenQuestInfoForItem(badgeName);
                    e.Handled = true;
                };
                Grid.SetColumn(badge, 1);
                grid.Children.Add(badge);
            }
            var right = new TextBlock
            {
                Text = row.Value,
                FontSize = DesignSystem.Size(Role.Body),
                Foreground = valueBrush?.Invoke(row.Value) ?? AppTheme.DimBrush,
            };
            Grid.SetColumn(right, 2);
            grid.Children.Add(right);
            return grid;
        }).ToList();
    }
}
