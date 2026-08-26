using System.Windows;
using System.Windows.Controls;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy;

/// <summary>
/// Composes the QUESTS theme's inline card — Inline themes PR 3, the same shape as
/// <see cref="ProgressThemeCard"/> (which carries the fuller commentary).
///
/// Epic and Sky are the Full rooms, as Bevel ruled them: ONE class's rows, capped —
/// <see cref="QuestInline"/> owns that arrangement so the Avalonia twin cannot decide it
/// differently. **General is the Glance and the DEFAULT** (Bevel: "3 quests ready to turn
/// in" is the thing a player expands the card to learn); **Unlocks is a Glance pending a
/// Bevel ruling** — it joined the strip with #238, after the table was signed.
/// </summary>
internal static class QuestsThemeCard
{
    /// <summary>The read-only capped checklist body both Full rooms share.</summary>
    private sealed class InlineChecklist
    {
        public readonly StackPanel Panel = new();
        private readonly TextBlock _heading = CardParts.BlockLabel("");
        private readonly ItemsControl _rows = new();
        private readonly TextBlock _note = CardParts.Summary();

        public InlineChecklist()
        {
            Panel.Children.Add(_heading);
            Panel.Children.Add(_rows);
            Panel.Children.Add(_note);
        }

        public void Render(QuestInline.Slice slice)
        {
            _heading.Text = slice.Heading;
            _heading.Visibility = slice.Heading.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            EqCardRows.Fill(_rows, slice.Rows.Select(r => new CardRow(
                r.Title,
                r.Acquired ? "✓" : "",
                Note: r.Unassigned ? "auto-ticked — confirm in the tracker" : null)));
            var tail = new List<string>();
            if (slice.More > 0) tail.Add(QuestInline.MoreLine(slice.More));
            if (slice.Note is { Length: > 0 } n) tail.Add(n);
            _note.Text = string.Join(Environment.NewLine, tail);
            _note.Visibility = tail.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public static ThemeCardView<QuestTab> Build(
        Expander section,
        ContentControl bodyHost,
        ContentControl popOutHost,
        ThemeHost<QuestTab> host,
        Func<IReadOnlyList<ThemeCardTab<QuestTab>>> tabs,
        Func<IReadOnlyCollection<string>> classes,
        AppSettings settings,
        Func<(int Done, int Total)?> unlockCounts,
        Action popOut,
        Action bringWindowForward,
        double bodyMaxHeight)
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

        var card = new ThemeCardView<QuestTab>(
            section, bodyHost, host,
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
            bodyMaxHeight: bodyMaxHeight);

        popOutHost.Content = card.PopOutButton;
        return card;
    }
}
