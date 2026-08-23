using Avalonia;
using Avalonia.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// Coin, and the items the log saw sold (#74, Snagglefern: <i>"if an item is unknown on the
/// wiki I definitely sold it"</i>).
///
/// Lifted out of <c>MainWindow</c> for PR A. The rows are unchanged from the fields this
/// replaced — same click, same tooltip, same quest badges as the Loot card, with the count
/// in the value column so the name stays a clean lookup key.
/// </summary>
internal sealed class MoneyCardView(ICardContext ctx) : IWidgetCard
{
    private readonly TextBlock _summary = AppTheme.DimText("");
    private readonly TextBlock _soldLabel = AppTheme.Heading("Sold to merchants");
    private readonly ItemsControl _soldList = new();
    private StackPanel? _body;

    public string Key => "money";

    public Control Body => _body ??= Build();

    private StackPanel Build()
    {
        var panel = new StackPanel();
        panel.Children.Add(_summary);
        _soldLabel.Margin = new Thickness(0, DesignTokens.SpaceS, 0, 0);
        panel.Children.Add(_soldLabel);
        panel.Children.Add(_soldList);
        return panel;
    }

    public void Render(StatsSnapshot s)
    {
        _summary.Text =
            $"Corpses {StatsSnapshot.FormatCoin(s.CorpseCopper)} ({s.CoinDrops} drops, biggest {StatsSnapshot.FormatCoin(s.BiggestDrop)})\n" +
            $"Merchant sales {StatsSnapshot.FormatCoin(s.VendorCopper)} ({s.SalesCount} sales)\n" +
            $"{StatsSnapshot.FormatCoin(s.CopperPerHour)} per hour - {StatsSnapshot.FormatCoin(s.CopperPerActiveHour)} per active hour" +
            (s.Recent is { } rm ? $"\nLast {(int)rm.Window.TotalMinutes}m: {StatsSnapshot.FormatCoin(rm.Copper)}" : "");
        _soldLabel.IsVisible = s.SoldItems.Count > 0;
        CardParts.FillList(_soldList, s.SoldItems.Select(i =>
                (i.Item, (i.Count > 1 ? $"x{i.Count} - " : "") + StatsSnapshot.FormatCoin(i.Copper))),
            ctx,
            onNameClick: ctx.ShowItemInfo,
            tooltip: n => ctx.QuestAwareTooltip(n, ctx.ItemHoverStats(n)), questBadges: true);
    }
}

/// <summary>
/// The Potential upgrade-currency ladder (#49, flipwon).
///
/// **Two live instances are normal here and always were** — the widget's own Motes card and
/// the Progress window's Wealth tab both draw motes. Before PR A that was one renderer and
/// two hand-held pairs of controls (<c>_motesSummary</c> vs <c>_cardMotesSummary</c>), with
/// a comment explaining that a Control has one parent. It is now two instances of one class,
/// which is the same fact said structurally: nothing is shared, so nothing can be moved.
/// </summary>
internal sealed class MotesCardView(ICardContext ctx) : IWidgetCard
{
    private readonly TextBlock _summary = AppTheme.DimText("");
    private readonly ItemsControl _list = new();
    private StackPanel? _body;

    public string Key => "motes";

    public Control Body => _body ??= Build();

    private StackPanel Build()
    {
        var panel = new StackPanel();
        _summary.Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXs);
        panel.Children.Add(_summary);
        panel.Children.Add(_list);
        return panel;
    }

    public void Render(StatsSnapshot s)
    {
        var motes = Motes.Summarize(s.Loot, s.Elapsed);
        _summary.Text = motes.Total > 0
            ? $"{motes.PerHour:0.#} motes/hr this session"
            : "No motes yet this session — every Mote of … Potential you loot " +
              "(or store as currency) lands here.";
        CardParts.FillList(_list, motes.Tiers.Select(t => (t.Item, $"x{t.Count}")),
            ctx, onNameClick: ctx.ShowItemInfo, tooltip: ctx.ItemHoverStats);
    }
}

/// <summary>Standing, per faction, with the per-kill deltas. The smallest of the five,
/// and it takes no context at all — everything it draws comes off the snapshot, which is
/// the shape <see cref="ICardContext"/>'s doc comment says to aim for.</summary>
internal sealed class FactionCardView : IWidgetCard
{
    private readonly ItemsControl _list = new();

    public string Key => "faction";

    public Control Body => _list;

    /// <summary>Row count, for the <c>EQBUDDY_EXPAND</c> dump the widget writes.</summary>
    public int RowCount => _list.Items.Count;

    public void Render(StatsSnapshot s) =>
        CardParts.FillList(_list,
            s.Faction.Select(f => (f.Faction, FactionFormat.Net(f))),
            valueBrush: f => f.StartsWith('-') ? AppTheme.BadBrush : AppTheme.GoodBrush);
}
