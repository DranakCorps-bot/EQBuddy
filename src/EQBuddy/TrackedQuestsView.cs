using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Tok = EQBuddy.UI.Shared.DesignTokens;

namespace EQBuddy;

/// <summary>
/// THE TRACKED QUESTS LIST, drawn — one builder for its two hosts: the minimized bar's hover
/// panel (<see cref="HudExpandWindow"/>) and the float its ⧉ pops out to
/// (<see cref="BreakoutWindow"/>, <see cref="BreakoutKind.Quests"/>). Founder, 2026-09-29:
/// "show all the information for the tracked quests but let them be expanded or collapsed
/// with a +/-" and "the ability to pop out the mini window and move it".
///
/// **One class so the two cannot disagree** (trap 4): the rows, their folds and their steps
/// are <see cref="TrackedQuestsPeek"/>'s, built here from the SAME ledger reads the Guide's
/// tabs make, and both hosts draw them through <see cref="Draw"/>. The float is the peek
/// with the cap taken off — nothing else about a row differs, which is the pop-out's promise
/// (OE-1 lock 6: the float carries the detail).
///
/// **Not a long-lived control either host borrows** (trap 45): each host owns its own
/// instance, and the list is rebuilt into the host's own panel on a signature change.
/// </summary>
internal sealed class TrackedQuestsView(MainWindow main, AppSettings settings)
{
    /// <summary>The steps the last <see cref="Draw"/> put on screen — the
    /// <c>hudExpandSteps</c> / <c>questsFloatSteps</c> dump facts. Counted where they are
    /// DRAWN, so a fold that wrote its setting and repainted nothing reads as zero (trap 42).
    /// </summary>
    public int StepsDrawn { get; private set; }

    /// <summary>The whole list, from the same reads the Guide's Quests, Plane of Sky and
    /// Epic tabs make.</summary>
    public TrackedQuestsBody Build()
    {
        var key = main.QuestCharacterKey;
        var ledger = main.QuestLedger;
        var tracked = main.TrackedQuests();
        var sections = main.TrackedSections();
        // The Sky and Epic tabs' OWN groups (ChecklistGroups, the producer those tabs call),
        // built only when something of that kind is tracked: this runs every second a host
        // is up, and projecting ninety-five Sky rewards to read one is waste.
        var skyGroups = tracked.Any(n => SkyTestSplit.RewardKeyFor(n).Length > 0)
            ? ChecklistGroups.Sky(settings, ledger, key)
            : null;
        var epicGroups = sections.Count > 0
            ? ChecklistGroups.Epic(settings, ledger, key, ChecklistGroups.EpicRows(settings))
            : null;
        return TrackedQuestsPeek.Build(
            main.QuestCatalog,
            ledger?.For(key)
                ?? new Dictionary<string, QuestLedgerStore.Entry>(StringComparer.OrdinalIgnoreCase),
            tracked,
            SkyCompleteToggle.CompletedQuests(settings, ledger, key),
            q => QuestPresentation.Distance(main.ZoneGraph, main.CurrentZoneName, q).Text,
            skyGroups, sections, epicGroups,
            // The Quests tab's detail pane projects a guided quest exactly this way.
            questGuide: ledger is null
                ? null
                : q => GuideChecklistProjection.ApplyQuest(q, GuideCatalog.Default, settings, ledger, key),
            expanded: settings.TrackedQuestsExpanded);
    }

    /// <summary>
    /// Draw <paramref name="body"/>'s rows into <paramref name="host"/>, capped at
    /// <paramref name="maxRows"/> (the peek) or not at all (the float). Returns the number of
    /// ROWS drawn — the host's own row fact.
    /// </summary>
    /// <param name="res">Any element in the host's tree, for the bar brush.</param>
    /// <param name="repaint">Called after an Untrack or a fold wrote, so the click repaints
    /// on the click rather than on the next tick — a row that stayed up for a second after
    /// "Untrack" reads as a click that did nothing.</param>
    /// <param name="viewQuests">The empty state's "View Quests" link.</param>
    public int Draw(Panel host, TrackedQuestsBody body, int? maxRows, FrameworkElement res,
        Action repaint, Action viewQuests)
    {
        host.Children.Clear();
        StepsDrawn = 0;
        if (body.Empty)
        {
            // "No quests being tracked – View Quests", the link IN the sentence.
            var line = Caption(TrackedQuestsPeek.EmptyLead + " – ", "DimBrush");
            var link = new System.Windows.Documents.Hyperlink(
                new System.Windows.Documents.Run(TrackedQuestsPeek.ViewQuests))
            {
                ToolTip = TrackedQuestsPeek.ViewQuestsTip,
            };
            link.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "AccentBrush");
            link.Click += (_, _) => viewQuests();
            line.Inlines.Add(link);
            host.Children.Add(line);
            return 0;
        }
        var shown = maxRows is { } cap ? body.Rows.Take(cap).ToList() : [.. body.Rows];
        foreach (var row in shown) host.Children.Add(Row(row, res, repaint));
        if (body.Rows.Count > shown.Count)
            host.Children.Add(Caption(TrackedQuestsPeek.MoreLine(body.Rows.Count - shown.Count), "DimBrush"));
        return shown.Count;
    }

    /// <summary>
    /// One tracked quest: +/− · name · badge · Untrack on the first line, the Guide's meta
    /// line under it, the gauge, and — unfolded — every step. A Grid, never a horizontal
    /// StackPanel, so a long name trims instead of pushing Untrack off the panel (trap 14).
    /// </summary>
    private Grid Row(TrackedQuestRow row, FrameworkElement res, Action repaint)
    {
        // The hover repeats the steps, so it is dropped once they are ON the panel.
        var grid = new Grid
        {
            Margin = new Thickness(0, 2, 0, 4),
            ToolTip = row.Expanded ? null : row.Tooltip,
        };
        for (var i = 0; i < 4; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // THE FOLD. The Guide's own face ("+" / "−", GuidePresentation.FoldFace) so the peek
        // and the tab it summarises read as one control. A row with nothing to unfold keeps
        // the column (Hidden, not Collapsed) so every name starts at the same x.
        var fold = new Button
        {
            Style = (Style)res.FindResource("ActionButton"),
            Content = GuidePresentation.FoldFace(!row.Expanded),
            FontSize = Tok.Spec(Tok.TypeRole.Body).Size,
            MinWidth = Tok.IconInlineHit,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, Tok.SpaceXs, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = TrackedQuestsPeek.FoldTip(row.Expanded),
            Tag = FoldTag,
            Visibility = row.Steps.Count > 0 ? Visibility.Visible : Visibility.Hidden,
        };
        System.Windows.Automation.AutomationProperties.SetName(fold,
            (row.Expanded ? "Hide steps: " : "Show steps: ") + row.Name);
        fold.Click += (_, _) =>
        {
            TrackedQuestsPeek.ToggleFold(settings.TrackedQuestsExpanded, row.FoldKey);
            main.PersistSettings();
            repaint();
        };
        grid.Children.Add(fold);

        var name = new TextBlock
        {
            Text = row.Name, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var badge = new TextBlock
        {
            Text = row.Badge.Label, FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(Tok.SpaceS, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        badge.SetResourceReference(TextBlock.ForegroundProperty, row.Badge.ColorKey);
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        var untrack = Link(TrackedQuestsPeek.Untrack, TrackedQuestsPeek.UntrackTip, () =>
        {
            var characterKey = main.QuestCharacterKey;
            if (main.QuestLedger is not { } ledger || characterKey.Length == 0) return;
            // The writer is the ROW's list: an Epic section is not in the quest list.
            if (row.Kind == TrackedKind.EpicSection)
                ledger.SetSectionTracked(characterKey, row.UntrackKey, false);
            else
                ledger.SetTracked(characterKey, row.UntrackKey, false);
            repaint();
        });
        untrack.FontSize = Tok.Spec(Tok.TypeRole.Caption).Size;
        untrack.Margin = new Thickness(Tok.SpaceS, 0, 0, 0);
        untrack.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(untrack, 3);
        grid.Children.Add(untrack);

        if (row.Meta.Length > 0)
        {
            var meta = Caption(row.Meta, "DimBrush");
            meta.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetRow(meta, 1);
            Grid.SetColumn(meta, 1);
            Grid.SetColumnSpan(meta, 3);
            grid.Children.Add(meta);
        }

        if (row.Share is { } share)
        {
            var track = new Grid { Margin = new Thickness(0, 3, 2, 0), Height = 3 };
            var bed = new Border { CornerRadius = new CornerRadius(1.5) };
            bed.SetResourceReference(Border.BackgroundProperty, "TrackBrush");
            track.Children.Add(bed);
            var fill = new Border
            {
                CornerRadius = new CornerRadius(1.5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = BreakdownRows.BarBrush(res),
            };
            track.SizeChanged += (_, e) => fill.Width = Math.Max(0, e.NewSize.Width * share);
            track.Children.Add(fill);
            Grid.SetRow(track, 2);
            Grid.SetColumn(track, 1);
            Grid.SetColumnSpan(track, 3);
            grid.Children.Add(track);
        }

        if (row.Expanded)
        {
            var steps = new StackPanel { Margin = new Thickness(0, Tok.SpaceXs, 0, 0) };
            for (var i = 0; i < row.Steps.Count; i++)
            {
                if (TrackedQuestsPeek.HeadingBefore(row.Steps, i))
                {
                    var heading = Caption(row.Steps[i].Heading, "DimBrush");
                    heading.FontWeight = FontWeights.SemiBold;
                    heading.Margin = new Thickness(0, i == 0 ? 0 : Tok.SpaceXs, 0, 1);
                    steps.Children.Add(heading);
                }
                steps.Children.Add(Step(row.Steps[i]));
                StepsDrawn++;
            }
            Grid.SetRow(steps, 3);
            Grid.SetColumn(steps, 1);
            Grid.SetColumnSpan(steps, 3);
            grid.Children.Add(steps);
        }
        return grid;
    }

    /// <summary>
    /// One step: a mark, then the words. Done is the green check with the text struck through
    /// and dimmed (the Founder's own words for it on the Epic tab: "scratched out font for
    /// completed steps or show a green check"); open is an empty ring; struck out on the tab
    /// is a dash. Read-only — a step is ticked on its tab, where the loot and hand-in routing
    /// that decide it live.
    /// </summary>
    private static Grid Step(TrackedStep step)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1), Tag = StepTag };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Tok.IconInlineHit) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        FrameworkElement mark = step.State switch
        {
            TrackedStepState.Done => new EqIcon
            {
                Glyph = "Check", Size = Tok.IconInline, Ink = "GoodBrush",
            },
            TrackedStepState.Skipped => Dash(),
            _ => Ring(),
        };
        mark.HorizontalAlignment = HorizontalAlignment.Center;
        mark.VerticalAlignment = VerticalAlignment.Top;
        mark.Margin = new Thickness(0, 2, 0, 0);
        grid.Children.Add(mark);

        var text = Caption(step.Title, step.State == TrackedStepState.Open ? "TextBrush" : "DimBrush");
        text.TextWrapping = TextWrapping.Wrap;
        if (step.State != TrackedStepState.Open) text.TextDecorations = TextDecorations.Strikethrough;
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static Ellipse Ring()
    {
        var ring = new Ellipse { Width = 8, Height = 8, StrokeThickness = 1.2 };
        ring.SetResourceReference(Shape.StrokeProperty, "DimBrush");
        return ring;
    }

    private static Rectangle Dash()
    {
        var dash = new Rectangle { Width = 8, Height = 1.5, Margin = new Thickness(0, 6, 0, 0) };
        dash.SetResourceReference(Shape.FillProperty, "DimBrush");
        return dash;
    }

    private static TextBlock Caption(string text, string brush)
    {
        var block = new TextBlock { Text = text, FontSize = Tok.Spec(Tok.TypeRole.Caption).Size };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }

    /// <summary>A worded link — a TextBlock holding one Hyperlink, which is how the rest of
    /// the app draws an inline door.</summary>
    public static TextBlock Link(string text, string tip, Action act)
    {
        var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(text))
        {
            ToolTip = tip,
        };
        link.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "AccentBrush");
        link.Click += (_, _) => act();
        var block = new TextBlock();
        block.Inlines.Add(link);
        return block;
    }

    /// <summary>The tag a row's +/− carries.</summary>
    public const string FoldTag = "trackedFold";

    /// <summary>The tag each drawn step carries.</summary>
    public const string StepTag = "trackedStep";
}
