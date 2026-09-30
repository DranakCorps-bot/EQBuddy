using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// **WHILE YOU'RE HERE, drawn** — the Guide room's block above the tabs (DRA-42 D1,
/// requirements §18): the open steps that can be done in the zone the log last entered, in
/// §18's three groups.
///
/// <para><b>Every decision is <see cref="WhileHere"/>'s and every word
/// <see cref="WhileHerePresentation"/>'s.</b> This class draws the answer and asks nothing of
/// its own — the phone draws the same answer from the same producer.</para>
///
/// <para><b>Above the tabs, under the room's caption</b>: a notice about where you are goes
/// where the eye lands (trap 44), and it is about the WHOLE guide rather than one tab. Read-only
/// on purpose — a step is ticked on its tab, where the loot and hand-in routing that decide it
/// live, so the block never becomes a second writer of one tick (trap 4).</para>
///
/// <para><b>Owned by <see cref="QuestsRoom"/> and handed to nobody</b> (trap 45). The fold is
/// session-only, the Sky leftover bands' precedent: a block that is about where you are NOW has
/// no state worth persisting.</para>
/// </summary>
internal sealed class WhileHereView : Border
{
    private readonly StackPanel _body = new();
    private readonly TextBlock _heading;
    private readonly Button _fold;
    private string _signature = "";
    private bool _open = true;

    /// <summary>The answer last drawn — the dump's source, so the facts and the screen are
    /// one moment (trap 56).</summary>
    public WhileHereAnswer Answer { get; private set; } = WhileHereAnswer.None;

    /// <summary>Step rows actually on screen — counted where they are drawn (trap 42).</summary>
    public int StepsDrawn { get; private set; }

    public WhileHereView()
    {
        Margin = new Thickness(Tok.SpaceL, Tok.SpaceS, Tok.SpaceL, 0);
        Padding = new Thickness(Tok.SpaceM, Tok.SpaceS, Tok.SpaceM, Tok.SpaceS);
        CornerRadius = new CornerRadius(Tok.RadiusCard);
        BorderThickness = new Thickness(1);
        SetResourceReference(BorderBrushProperty, "TrackBrush");

        var outer = new StackPanel();
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _heading = new TextBlock
        {
            FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = WhileHerePresentation.SourceNote,
        };
        _heading.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        head.Children.Add(_heading);

        _fold = new Button
        {
            Content = GuidePresentation.FoldFace(false),
            FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
            MinWidth = Tok.IconInlineHit,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _fold.SetResourceReference(StyleProperty, "ActionButton");
        _fold.Click += (_, _) =>
        {
            _open = !_open;
            _signature = "";
            Draw(Answer);
        };
        Grid.SetColumn(_fold, 1);
        head.Children.Add(_fold);
        outer.Children.Add(head);
        outer.Children.Add(_body);
        Child = outer;
    }

    /// <summary>Ask the producer and draw the answer — cheap when nothing moved, because the
    /// rows are rebuilt only when what they SAY changed.</summary>
    public void Render(MainWindow main, StatsSnapshot s) => Draw(main.WhileHereNow(s));

    private void Draw(WhileHereAnswer answer)
    {
        Answer = answer;
        var signature = Signature(answer) + (_open ? "|open" : "|shut");
        if (signature == _signature) return;
        _signature = signature;

        _heading.Text = WhileHerePresentation.HeadingFor(answer);
        _fold.Content = GuidePresentation.FoldFace(!_open);
        _body.Children.Clear();
        _body.Visibility = _open ? Visibility.Visible : Visibility.Collapsed;
        StepsDrawn = 0;
        if (!_open) return;

        if (WhileHerePresentation.Empty(answer) is { } empty)
        {
            _body.Children.Add(Caption(empty, "DimBrush", wrap: true));
            return;
        }
        foreach (var group in WhileHerePresentation.Groups(answer))
        {
            _body.Children.Add(GroupLabel(group.Label));
            if (group.Group == WhileHereGroup.Optional)
            {
                // An Optional quest is a NAME, not a step — no ring, which would read as a box
                // to tick — and the names share one wrapped line, because the block sits above
                // the tabs and a column of quest names is height the tabs pay for.
                _body.Children.Add(Caption(string.Join(" · ", group.Rows.Select(r => r.Title)),
                    "TextBrush", wrap: true));
            }
            else
            {
                foreach (var row in group.Rows)
                {
                    _body.Children.Add(Step(row));
                    StepsDrawn++;
                }
            }
            if (group.More is { } more) _body.Children.Add(Caption(more, "DimBrush", wrap: true));
        }
        if (WhileHerePresentation.UnplacedLine(answer) is { } unplaced)
        {
            var line = Caption(unplaced, "DimBrush", wrap: true);
            line.Margin = new Thickness(0, Tok.SpaceXs, 0, 0);
            _body.Children.Add(line);
        }
    }

    /// <summary>Everything a row DRAWS, so a step ticked on a tab, on the phone or by the loot
    /// auto-tick repaints the block (trap 72) — and nothing that drifts every tick (trap 8).</summary>
    public static string Signature(WhileHereAnswer a) =>
        $"{a.State}|{a.Zone}|{a.UnplacedTracked}|"
        + string.Join(";", a.Required.Select(s => $"R:{s.Quest}/{s.StepId}/{string.Join(",", s.Who)}"))
        + "|" + string.Join(";", a.Relevant.Select(s => $"V:{s.Quest}/{s.StepId}/{string.Join(",", s.Who)}"))
        + "|" + string.Join(";", a.Optional);

    private static TextBlock GroupLabel(string text)
    {
        var label = Caption(text, "DimBrush", wrap: true);
        label.FontWeight = FontWeights.SemiBold;
        label.Margin = new Thickness(0, Tok.SpaceXs, 0, 1);
        return label;
    }

    /// <summary>One step: an open ring, the step as its tab words it, and under it the quest and
    /// who the source names here. A Grid, never a horizontal StackPanel, so the words wrap
    /// (trap 14).</summary>
    private static Grid Step(WhileHereRowView step)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 2), Tag = StepTag };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Tok.IconInlineHit) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var ring = new Ellipse
        {
            Width = 8, Height = 8, StrokeThickness = 1.2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 3, 0, 0),
        };
        ring.SetResourceReference(Shape.StrokeProperty, "DimBrush");
        grid.Children.Add(ring);

        var title = Caption(step.Title, "TextBrush", wrap: true);
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var detail = Caption(step.Detail, "DimBrush", wrap: true);
        Grid.SetColumn(detail, 1);
        Grid.SetRow(detail, 1);
        grid.Children.Add(detail);
        return grid;
    }

    private static TextBlock Caption(string text, string brush, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = Tok.Spec(Tok.TypeRole.Caption).Size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }

    /// <summary>The dump's facts, under the room's own prefix (trap 58). Counts, never names:
    /// the zone and the quests are the player's.</summary>
    public string DebugFacts() =>
        $"whileHereState={Answer.State} " +
        $"whileHereZone={Answer.Zone.Length} " +
        $"whileHereRequired={Answer.Required.Count} " +
        $"whileHereRelevant={Answer.Relevant.Count} " +
        $"whileHereOptional={Answer.Optional.Count} " +
        $"whileHereUnplaced={Answer.UnplacedTracked} " +
        $"whileHereStepsDrawn={StepsDrawn} " +
        $"whileHereOpen={(_open ? 1 : 0)}";

    /// <summary>The tag each drawn step carries.</summary>
    public const string StepTag = "whileHereStep";
}
