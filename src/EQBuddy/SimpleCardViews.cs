using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>Shared furniture for the cards converted in Gate 5b: the small-caps label that
/// introduces a block, and a body that is just a stack.</summary>
internal static class CardParts
{
    public static TextBlock BlockLabel(string text, bool hidden = true)
    {
        var label = DesignSystem.Text(DesignTokens.TypeRole.Caption, text);
        label.FontWeight = FontWeights.SemiBold;
        label.Margin = new Thickness(0, DesignTokens.SpaceS, 0, DesignTokens.SpaceXxs);
        if (hidden) label.Visibility = Visibility.Collapsed;
        return label;
    }

    public static TextBlock Summary()
    {
        var text = DesignSystem.Text(DesignTokens.TypeRole.BodySecondary);
        text.TextWrapping = TextWrapping.Wrap;
        text.Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXs);
        return text;
    }

    /// <summary>The one-line body of a card that has nothing yet. The card stays where
    /// Options put it (David's verdict: "show what I've selected to see") and this says
    /// what will fill it — a card that hid itself would read as a missing feature.
    ///
    /// The same line <c>MainWindow.EmptyCardLine</c> draws; shared here so a lifted card
    /// does not have to reach back into the window for a TextBlock.</summary>
    public static TextBlock EmptyLine(string text)
    {
        var line = DesignSystem.Text(DesignTokens.TypeRole.Caption, text);
        line.TextWrapping = TextWrapping.Wrap;
        line.Margin = new Thickness(0, DesignTokens.SpaceXxs, 0, DesignTokens.SpaceXxs);
        return line;
    }
}

/// <summary>The Motes card (Gate 5b). Its rows are ITEMS, so this is the first card to
/// spend <see cref="ICardContext"/> — click for the wiki popup, hover for cached stats.</summary>
internal sealed class MotesCardView(ICardContext context) : IWidgetCard
{
    private readonly TextBlock _summary = CardParts.Summary();
    private readonly ItemsControl _rows = new();

    public string Key => "motes";
    public UIElement Body { get; } = new StackPanel();

    public void Attach()
    {
        var body = (StackPanel)Body;
        body.Children.Add(_summary);
        body.Children.Add(_rows);
    }

    public void Render(StatsSnapshot s)
    {
        var motes = Motes.Summarize(s.Loot, s.Elapsed);
        _summary.Text = MotesPresentation.Summary(motes);
        EqCardRows.Fill(_rows, MotesPresentation.Rows(motes), context);
    }
}

/// <summary>The Money card (Gate 5b). Sold items are drops too (#74), so they carry the
/// same click, hover and quest badge as the Loot card.</summary>
internal sealed class MoneyCardView(ICardContext context) : IWidgetCard
{
    private readonly TextBlock _summary = CardParts.Summary();
    private readonly TextBlock _soldLabel = CardParts.BlockLabel(MoneyPresentation.SoldLabel);
    private readonly ItemsControl _sold = new();

    public string Key => "money";
    public UIElement Body { get; } = new StackPanel();

    public void Attach()
    {
        var body = (StackPanel)Body;
        body.Children.Add(_summary);
        body.Children.Add(_soldLabel);
        body.Children.Add(_sold);
    }

    public void Render(StatsSnapshot s)
    {
        _summary.Text = string.Join(Environment.NewLine, MoneyPresentation.SummaryLines(s));
        _soldLabel.Visibility = MoneyPresentation.ShowSold(s)
            ? Visibility.Visible : Visibility.Collapsed;
        EqCardRows.Fill(_sold, MoneyPresentation.SoldRows(s), context);
    }
}

/// <summary>The Faction card (Gate 5b) — pure presentation, no context at all: it names
/// factions, not items.</summary>
internal sealed class FactionCardView : IWidgetCard
{
    private readonly ItemsControl _rows = new();

    public string Key => "faction";
    public UIElement Body { get; } = new StackPanel();

    /// <summary>Rendered row count, for the <c>EQBUDDY_EXPAND</c> dump E2E asserts on.</summary>
    public int RowCount => _rows.Items.Count;

    public void Attach() => ((StackPanel)Body).Children.Add(_rows);

    public void Render(StatsSnapshot s) => EqCardRows.Fill(_rows, FactionFormat.Rows(s.Faction));
}
