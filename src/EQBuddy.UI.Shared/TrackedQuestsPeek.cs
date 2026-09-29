using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One tracked quest as the bar's peek draws it.</summary>
/// <param name="Name">The quest's name — also the key Untrack writes, since the ledger's
/// tracked set is keyed on it (<see cref="QuestLedgerStore.SetTracked"/>).</param>
/// <param name="Badge">The SAME badge the Guide's quest row wears
/// (<see cref="QuestPresentation.BadgeFor"/>).</param>
/// <param name="Meta">The SAME meta line (<see cref="QuestPresentation.MetaLine"/>).</param>
/// <param name="Share">Turn-in progress 0..1 for the gauge, or null when the quest has no
/// turn-in fraction to show (a steps quest, a set of quests, one no longer in the catalog).
/// </param>
/// <param name="Tooltip">The turn-in items with have/need — the detail pane's item list,
/// folded into a hover.</param>
public sealed record TrackedQuestRow(
    string Name, QuestPresentation.Badge Badge, string Meta, double? Share, string Tooltip);

/// <summary>The whole peek: a subtext, the rows, and the signature that decides a rebuild.
/// No rows is the EMPTY state, and <see cref="TrackedQuestsPeek.EmptyLead"/> is what it
/// says.</summary>
public sealed record TrackedQuestsBody(
    string Subtext, IReadOnlyList<TrackedQuestRow> Rows, string Signature)
{
    public bool Empty => Rows.Count == 0;
}

/// <summary>
/// THE TRACKED QUESTS PEEK (Founder, 2026-09-29) — the hover panel of the minimized bar's
/// quests chip: every quest the player 📌-tracked, drawn the way the Guide's Quests tab
/// draws it, each with an Untrack, and a link to the Guide.
///
/// **It is the Guide's own row, not a second opinion of it.** The badge is
/// <see cref="QuestPresentation.BadgeFor"/>, the meta line is
/// <see cref="QuestPresentation.MetaLine"/> and the progress is
/// <see cref="QuestMatcher.Match"/> over the same owned counts, tracked set and completion
/// counts the Quests tab reads — so the chip and the tab cannot disagree about a quest
/// (trap 4). Nothing here decides a word the Guide already decided.
///
/// **A tracked name the catalog no longer has is SHOWN, not dropped.** The tracked set is a
/// list of names, and a catalog refresh can rename or retire one. Dropping it would leave a
/// pin nobody can see and nobody can remove, which is the stuck-chip complaint (#954) one
/// surface over; drawing it with a sentence and an Untrack lets the player clear it.
///
/// Framework-free, so every sentence and every row is asserted without a window
/// (docs/TestPlan.md §5).
/// </summary>
public static class TrackedQuestsPeek
{
    /// <summary>The empty state's lead — the Founder's words, the link follows it.</summary>
    public const string EmptyLead = "No quests being tracked";

    /// <summary>The link's words, in the empty state and in the panel's header alike.</summary>
    public const string ViewQuests = "View Quests";

    /// <summary>The link's hover.</summary>
    public const string ViewQuestsTip = "Open the Quests tab of the Guide";

    /// <summary>The per-row control.</summary>
    public const string Untrack = "Untrack";

    /// <summary>The per-row control's hover — says both places the pin goes from, because
    /// the Guide and this panel read one list.</summary>
    public const string UntrackTip = "Stop tracking this quest — it leaves this list and loses its Track tick in the Guide";

    /// <summary>The meta line for a tracked name the catalog no longer carries.</summary>
    public const string NotInCatalog = "no longer in EQBuddy's quest list — untrack to clear it";

    /// <summary>
    /// The tracked quests, in the Guide's own order for them (<see cref="QuestMatcher"/>:
    /// most complete first, then fewest requirements, then by name), then any tracked name
    /// the catalog has lost.
    /// </summary>
    /// <param name="completed">The ledger's completion counts
    /// (<c>SkyCompleteToggle.CompletedQuests</c>), the same map the Quests tab reads.</param>
    /// <param name="distance">"you're here" / "3 zones away" / "" for a quest — the host's
    /// zone graph, through <see cref="QuestPresentation.Distance"/>. Null draws none.</param>
    public static TrackedQuestsBody Build(
        QuestCatalog catalog,
        IReadOnlyDictionary<string, QuestLedgerStore.Entry> owned,
        IReadOnlySet<string> tracked,
        IReadOnlyDictionary<string, int> completed,
        Func<QuestEntry, string>? distance = null)
    {
        if (tracked.Count == 0) return new("", [], "quests|none");

        var trackedSet = new HashSet<string>(tracked, StringComparer.OrdinalIgnoreCase);
        var done = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, count) in completed) done[name] = count;

        var rows = new List<TrackedQuestRow>();
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in QuestMatcher.Match(catalog, owned, trackedSet).Where(m => m.Tracked))
        {
            found.Add(m.Quest.Name);
            var count = done.GetValueOrDefault(m.Quest.Name);
            rows.Add(new TrackedQuestRow(
                m.Quest.Name,
                QuestPresentation.BadgeFor(m, count),
                QuestPresentation.MetaLine(m.Quest, count, distance?.Invoke(m.Quest) ?? ""),
                m.ItemsTotal > 0 && !m.Quest.Collection ? m.Fraction : null,
                ItemsTooltip(m)));
        }
        foreach (var orphan in tracked
                     .Where(name => !found.Contains(name))
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            rows.Add(new TrackedQuestRow(orphan,
                new QuestPresentation.Badge(QuestPresentation.State.Open, "?", "DimBrush"),
                NotInCatalog, null, NotInCatalog));

        var ready = rows.Count(r => r.Badge.State == QuestPresentation.State.Ready);
        var subtext = QuestPresentation.ReadySummary(ready) is { } readyLine
            ? $"{rows.Count} tracked · {readyLine}"
            : $"{rows.Count} tracked";
        // Everything a row DRAWS goes in the signature, so a pin written anywhere else — the
        // Guide, the phone — or a loot that moves a badge repaints the panel (trap 72).
        var signature = "quests|" + string.Join("|", rows.Select(r =>
            $"{r.Name}~{r.Badge.Label}~{r.Meta}~{r.Share:0.###}"));
        return new(subtext, rows, signature);
    }

    /// <summary>The turn-in items with have/need, one per line — or the sentence that says
    /// there are none to count, for a quest the Guide tracks by its steps.</summary>
    public static string ItemsTooltip(QuestMatch m)
    {
        if (m.Quest.Collection) return "A set of quests — open it in the Guide for each one.";
        if (m.Items.Count == 0) return "No turn-in items to count — the Guide has its steps.";
        return string.Join("\n", m.Items.Select(i => $"{i.Name}: {Math.Min(i.Have, i.Need)}/{i.Need}"));
    }

    /// <summary>The "…and N more" line under a capped list. It names the header's link,
    /// because that is the control on this surface that shows the rest (trap 50).</summary>
    public static string MoreLine(int hidden) => $"…and {hidden} more — {ViewQuests} for the full list";
}
