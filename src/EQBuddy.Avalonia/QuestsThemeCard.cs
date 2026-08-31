using Avalonia.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia;

/// <summary>
/// Composes the QUESTS theme's inline card for this lane — the Avalonia half of Inline
/// themes PR 3; <c>EQBuddy/QuestsThemeCard.cs</c> is the WPF twin. Epic and Sky inline as
/// ONE class's rows, capped (<see cref="QuestInline"/> owns the arrangement); General is
/// the Glance and the default (Bevel); Unlocks is a Glance pending its ruling.
/// </summary>
internal static class QuestsThemeCard
{
    private sealed class InlineChecklist
    {
        public readonly StackPanel Panel = new();
        private readonly TextBlock _heading = AppTheme.Heading("");
        private readonly ItemsControl _rows = new();
        private readonly TextBlock _note = CardParts.EmptyLine("");

        public InlineChecklist()
        {
            Panel.Children.Add(_heading);
            Panel.Children.Add(_rows);
            Panel.Children.Add(_note);
        }

        public void Render(QuestInline.Slice slice)
        {
            _heading.Text = slice.Heading;
            _heading.IsVisible = slice.Heading.Length > 0;
            CardParts.FillList(_rows, slice.Rows.Select(r =>
                (r.Title, r.Acquired ? "✓" : r.Unassigned ? "?" : "")));
            var tail = new List<string>();
            if (slice.More > 0) tail.Add(QuestInline.MoreLine(slice.More));
            if (slice.Note is { Length: > 0 } n) tail.Add(n);
            _note.Text = string.Join(Environment.NewLine, tail);
            _note.IsVisible = tail.Count > 0;
        }
    }

    public static ThemeCardPanel<QuestTab> Build(
        Control header,
        ThemeHost<QuestTab> host,
        Func<IReadOnlyList<ThemeCardTab<QuestTab>>> tabs,
        Func<IReadOnlyCollection<string>> classes,
        AppSettings settings,
        Func<(int Done, int Total)?> unlockCounts,
        Action popOut,
        Action bringWindowForward,
        Func<double, double> bodyCap)
    {
        InlineChecklist? body = null;
        InlineChecklist Body() => body ??= new InlineChecklist();

        IReadOnlyList<QuestChecklistGroup> Groups(QuestTab tab) => tab == QuestTab.Epic
            ? QuestChecklistLayout.Epic(settings.EpicQuestChecklist)
            : QuestChecklistLayout.Sky(settings.SkyQuestChecklist, settings.SkyQuestCompleted,
                settings.SkyStepsUnderEveryIsland);

        int ReadyCount() =>
            QuestChecklistLayout.ReadyToTurnIn(Groups(QuestTab.Epic)).Count
            + QuestChecklistLayout.ReadyToTurnIn(Groups(QuestTab.Sky)).Count;

        return new ThemeCardPanel<QuestTab>(
            header, host,
            tabs: _ => tabs(),
            modeFor: QuestSurface.InlineModeFor,
            bodyFor: _ => Body().Panel,
            glanceFor: (tab, _) => tab == QuestTab.Unlocks
                ? QuestSurface.UnlocksGlance(unlockCounts())
                : QuestSurface.GeneralGlance(ReadyCount()),
            render: (tab, _) =>
            {
                if (tab is QuestTab.Epic or QuestTab.Sky)
                    Body().Render(QuestInline.For(Groups(tab), classes()));
            },
            popOut: popOut,
            bringWindowForward: bringWindowForward,
            popOutTip: "Open the Quest Tracker — search, filters, and the working checklists",
            bodyCap: bodyCap);
    }
}
