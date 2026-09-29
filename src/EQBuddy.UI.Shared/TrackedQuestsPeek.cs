using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>WHICH list a tracked row came from — and so which writer its Untrack calls.
/// </summary>
public enum TrackedKind
{
    /// <summary>A Quests-tab quest: <see cref="QuestLedgerStore.SetTracked"/>.</summary>
    Quest,
    /// <summary>A Plane of Sky reward — also a catalog quest by name, so the SAME list and
    /// writer as <see cref="Quest"/>; its progress is the Sky tab's.</summary>
    Sky,
    /// <summary>An Epic 1.0 section: <see cref="QuestLedgerStore.SetSectionTracked"/>.</summary>
    EpicSection,
}

/// <summary>One tracked quest as the bar's peek draws it.</summary>
/// <param name="Name">What the row is CALLED — the quest name, the Sky tab's
/// "Class · Reward", or "Class Epic · Section".</param>
/// <param name="Badge">The SAME badge the Guide's quest row wears
/// (<see cref="QuestPresentation.BadgeFor"/>).</param>
/// <param name="Meta">The SAME meta line (<see cref="QuestPresentation.MetaLine"/>).</param>
/// <param name="Share">Turn-in progress 0..1 for the gauge, or null when the quest has no
/// turn-in fraction to show (a steps quest, a set of quests, one no longer in the catalog).
/// </param>
/// <param name="Tooltip">The turn-in items with have/need — the detail pane's item list,
/// folded into a hover.</param>
public sealed record TrackedQuestRow(
    string Name, QuestPresentation.Badge Badge, string Meta, double? Share, string Tooltip)
{
    /// <summary>Which list it came from — the writer Untrack calls.</summary>
    public TrackedKind Kind { get; init; } = TrackedKind.Quest;

    /// <summary>The key Untrack writes: the catalog quest name for a Quest or Sky row, the
    /// "guideId/stageId" section key for an Epic row. Defaults to <see cref="Name"/>, which
    /// is what it is for a Quests-tab quest.</summary>
    public string Key { get; init; } = "";

    /// <summary><see cref="Key"/>, or the name when no key was set.</summary>
    public string UntrackKey => Key.Length > 0 ? Key : Name;
}

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
    /// <param name="skyGroups">The Plane of Sky tab's groups (<see cref="ChecklistGroups.Sky"/>).
    /// A tracked name that is a Sky reward's quest takes ITS progress from here — the tab's
    /// own count of the reward's steps — rather than the bag-count fraction a Quests-tab row
    /// shows, because the Sky tab is where that quest is played. Null: none are.</param>
    /// <param name="trackedSections">The Epic sections tracked ("guideId/stageId").</param>
    /// <param name="epicGroups">The Epic tab's groups (<see cref="ChecklistGroups.Epic"/>).</param>
    /// <param name="guides">The guide catalog the section keys resolve against.</param>
    public static TrackedQuestsBody Build(
        QuestCatalog catalog,
        IReadOnlyDictionary<string, QuestLedgerStore.Entry> owned,
        IReadOnlySet<string> tracked,
        IReadOnlyDictionary<string, int> completed,
        Func<QuestEntry, string>? distance = null,
        IReadOnlyList<QuestChecklistGroup>? skyGroups = null,
        IReadOnlyCollection<string>? trackedSections = null,
        IReadOnlyList<QuestChecklistGroup>? epicGroups = null,
        GuideCatalog? guides = null)
    {
        trackedSections ??= [];
        if (tracked.Count == 0 && trackedSections.Count == 0) return new("", [], "quests|none");

        var trackedSet = new HashSet<string>(tracked, StringComparer.OrdinalIgnoreCase);
        var done = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, count) in completed) done[name] = count;

        var rows = new List<TrackedQuestRow>();
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in QuestMatcher.Match(catalog, owned, trackedSet).Where(m => m.Tracked))
        {
            found.Add(m.Quest.Name);
            // A Plane of Sky reward: drawn the way the Sky tab draws it.
            if (SkyTestSplit.RewardKeyFor(m.Quest.Name) is { Length: > 0 } rewardKey
                && skyGroups?.FirstOrDefault(g => string.Equals(g.CompletionKey, rewardKey,
                    StringComparison.OrdinalIgnoreCase)) is { } sky)
            {
                rows.Add(SkyRow(sky, m.Quest.Name));
                continue;
            }
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

        foreach (var key in trackedSections.Order(StringComparer.OrdinalIgnoreCase))
            rows.Add(EpicSectionRow(key, epicGroups, guides ?? GuideCatalog.Default));

        var ready = rows.Count(r => r.Badge.State == QuestPresentation.State.Ready);
        var subtext = QuestPresentation.ReadySummary(ready) is { } readyLine
            ? $"{rows.Count} tracked · {readyLine}"
            : $"{rows.Count} tracked";
        // Everything a row DRAWS goes in the signature, so a pin written anywhere else — the
        // Guide, the phone — or a loot that moves a badge repaints the panel (trap 72).
        var signature = "quests|" + string.Join("|", rows.Select(r =>
            $"{r.Kind}:{r.UntrackKey}~{r.Name}~{r.Badge.Label}~{r.Meta}~{r.Share:0.###}"));
        return new(subtext, rows, signature);
    }

    /// <summary>What the Sky section says, and the Epic section's lead.</summary>
    public const string SkyLead = "Plane of Sky";

    /// <summary>The Epic section rows' lead — the tab's own name for the quest.</summary>
    public const string EpicLead = "Epic 1.0";

    /// <summary>A tracked Epic section the guide no longer has.</summary>
    public const string SectionGone = "no longer in EQBuddy's epic guide — untrack to clear it";

    /// <summary>
    /// A Plane of Sky reward, as its heading on the Sky tab reads: "Class · Reward", its
    /// steps done of its steps, and the tab's own words for its state. The next step to do
    /// rides the meta line, because "what do I do next" is what a glance is for.
    /// </summary>
    public static TrackedQuestRow SkyRow(QuestChecklistGroup group, string questName) =>
        new(group.Heading, BadgeFor(group), MetaFor(SkyLead, group.Rows),
            group.Total > 0 ? group.Progress : null, StepsTooltip(group.Rows))
        {
            Kind = TrackedKind.Sky,
            Key = questName,
        };

    /// <summary>
    /// One Epic section: "Class Epic · Section", its steps done of its steps as the Epic tab
    /// draws them under that heading, and the next step. A key the guide no longer resolves
    /// is drawn with <see cref="SectionGone"/> so it can be untracked.
    /// </summary>
    public static TrackedQuestRow EpicSectionRow(
        string key, IReadOnlyList<QuestChecklistGroup>? epicGroups, GuideCatalog guides)
    {
        var resolved = EpicSection.Resolve(guides, key);
        var group = resolved is { } r
            ? epicGroups?.FirstOrDefault(g => g.GuideId == r.Guide.Id)
            : null;
        if (resolved is not { } found || group is null)
            return new(key, new(QuestPresentation.State.Open, "?", "DimBrush"), SectionGone, null, SectionGone)
            {
                Kind = TrackedKind.EpicSection,
                Key = key,
            };
        var stage = found.Stage;
        var steps = EpicSection.Rows(group, stage);
        var doneCount = steps.Count(r => r.Acquired);
        var badge = steps.Count > 0 && doneCount == steps.Count
            ? new QuestPresentation.Badge(QuestPresentation.State.Done, "done", "GoodBrush")
            : doneCount > 0
                ? new QuestPresentation.Badge(QuestPresentation.State.InProgress, $"{doneCount}/{steps.Count}", "AccentBrush")
                : new QuestPresentation.Badge(QuestPresentation.State.Open, $"0/{steps.Count}", "DimBrush");
        return new($"{group.ClassName} Epic · {stage.Name}", badge, MetaFor(EpicLead, steps),
            steps.Count > 0 ? (double)doneCount / steps.Count : null, StepsTooltip(steps))
        {
            Kind = TrackedKind.EpicSection,
            Key = key,
        };
    }

    /// <summary>The Sky group's badge in the tab's own terms: turned in, ready, a step count.
    /// </summary>
    private static QuestPresentation.Badge BadgeFor(QuestChecklistGroup g) =>
        g.Completed ? new(QuestPresentation.State.Done, "turned in", "GoodBrush")
        : g.ReadyToTurnIn ? new(QuestPresentation.State.Ready, "ready", "GoodBrush")
        : g.Done > 0 ? new(QuestPresentation.State.InProgress, $"{g.Done}/{g.Total}", "AccentBrush")
        : new(QuestPresentation.State.Open, $"0/{g.Total}", "DimBrush");

    /// <summary>"Plane of Sky · next: Kill X and loot Y" — the first step neither done nor
    /// struck out, in the tab's reading order; just the lead when there is none left.</summary>
    public static string MetaFor(string lead, IEnumerable<QuestChecklistRow> rows) =>
        rows.DistinctBy(r => r.Id, StringComparer.Ordinal)
            .FirstOrDefault(r => !r.Acquired && !r.IsSkipped) is { } next
            ? $"{lead} · next: {next.Title}"
            : lead;

    /// <summary>Every step with a ✓ or a dash — the section, readable in a hover.</summary>
    public static string StepsTooltip(IEnumerable<QuestChecklistRow> rows) =>
        string.Join("\n", rows.DistinctBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => (r.Acquired ? "✓ " : r.IsSkipped ? "– " : "· ") + r.Title));

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
