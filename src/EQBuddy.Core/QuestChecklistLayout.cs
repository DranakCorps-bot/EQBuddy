namespace EQBuddy.Core;

/// <summary>One row of an Epic or Plane of Sky checklist, as any UI should draw it.
/// <see cref="Detail"/> already carries the turn-in NPC and the drop location, because
/// "where does this come from" is the question a checklist row exists to answer.</summary>
public sealed record QuestChecklistRow(
    string Id,
    string ClassName,
    string Title,
    string Detail,
    bool Acquired,
    bool Unassigned,
    /// <summary>Which island heading this row sits under — <see cref="SkyIslands.Heading"/>,
    /// a multi-island heading that NAMES them ("Islands 1.5, 4, and 8"), or
    /// <see cref="SkyIslands.AnywhereHeading"/>.
    /// Empty on Epic rows, which have no islands.
    ///
    /// Rows arrive already ORDERED by it, so a surface draws a heading whenever this changes
    /// from the previous row and needs no grouping logic of its own — the same reason the
    /// rows themselves come pre-ordered (#184).</summary>
    string IslandHeading = "");

/// <summary>A group of rows under one heading, with the state of the reward as a whole.</summary>
/// <param name="Title">The reward (Sky) or section (Epic) on its own, WITHOUT the class.
/// <see cref="Heading"/> joins the two. Carried rather than parsed back out: the window
/// used to recover the reward by splitting the heading on "·", which is one fact stored
/// in one place and read out of another (trap 4) and would have broken on the first
/// reward name containing the separator.</param>
/// <param name="CompletionKey">For Sky, the key that says "this reward is turned in"
/// (<see cref="QuestChecklistLayout.RewardKey"/>). Null for Epic, whose completion is
/// per CLASS rather than per group. A view needs this to offer the turn-in control at
/// all — without it the only way to close a reward out was the achievements import.</param>
/// <param name="Completed">Turned in. Distinct from every item being acquired: holding
/// the pieces and having handed them over are different states, and the whole point of
/// the reward group is to tell them apart.</param>
/// <param name="TurnInNpc">Who takes the hand-in, when the catalog names one. The Ready
/// band shows it, because "what can I turn in right now" is only actionable with the
/// "and to whom" attached.</param>
public sealed record QuestChecklistGroup(
    string ClassName,
    string Title,
    IReadOnlyList<QuestChecklistRow> Rows,
    string? CompletionKey = null,
    bool Completed = false,
    string? TurnInNpc = null)
{
    /// <summary>"Bard · Mask of Song" — what a heading reads as.</summary>
    public string Heading => ClassName + " · " + Title;

    /// <summary>Every item in hand, and not yet turned in — the moment the turn-in
    /// control is worth offering.</summary>
    public bool ReadyToTurnIn => !Completed && Rows.Count > 0 && Rows.All(r => r.Acquired);

    /// <summary>Progress counts DISTINCT steps, not rendered rows.
    ///
    /// **This became load-bearing on 2026-08-23**, when the player gained the option to see a
    /// step under every island it can be found on: a step naming three islands renders three
    /// times, and counting rows would have turned a 6-piece reward into a 12-piece one — a
    /// checklist that reports "3/12" for a quest with six steps, silently, on the surface
    /// whose whole job is to say how far along you are. Nothing about it would look wrong in
    /// the code that renders the rows.</summary>
    public int Done => Rows.DistinctBy(r => r.Id, StringComparer.Ordinal).Count(r => r.Acquired);

    public int Total => Rows.DistinctBy(r => r.Id, StringComparer.Ordinal).Count();

    /// <summary>Which slice of the state lens this group falls in — one of
    /// <see cref="QuestChecklistLayout.States"/>, never "any state", which is the absence
    /// of a filter rather than a state a group can be in.
    ///
    /// An EPIC section has no turn-in of its own (no <see cref="CompletionKey"/>), so
    /// every piece collected IS its terminal state and reads as done. A SKY reward with
    /// every piece collected is only <em>ready</em> — the hand-in has still to happen,
    /// and telling those two apart is the entire job of this screen.</summary>
    public string State =>
        Completed ? QuestChecklistLayout.StateDone
        : Rows.Count > 0 && Rows.All(r => r.Acquired)
            ? CompletionKey is null ? QuestChecklistLayout.StateDone : QuestChecklistLayout.StateReady
        : QuestChecklistLayout.StateOpen;

    /// <summary>The word after the count on the heading. Finer than <see cref="State"/>
    /// on purpose: a group nobody has started says nothing at all, where the lens puts it
    /// in "open" alongside one that is half done. Derived from the same fields as
    /// <see cref="State"/> so the label and the filter cannot disagree.</summary>
    public string? Note =>
        Completed ? "done"
        : Rows.Count > 0 && Rows.All(r => r.Acquired) ? "ready"
        : Rows.Any(r => r.Acquired) ? "in progress"
        : null;

    /// <summary>How close to finished, for the actionability sort. Untouched is 0 and
    /// every piece held is 1.</summary>
    public double Progress => Total == 0 ? 0 : (double)Done / Total;
}

/// <summary>Done / Ready / Partial / Total for one class (#136, bjstrange).</summary>
/// <remarks>D + R + P deliberately does NOT sum to <paramref name="Total"/> — a reward
/// you have not started sits in no bucket. bjstrange read three numbers that didn't add
/// up and reasonably concluded they were wrong; showing what they are out of turns a
/// puzzle into a subtraction.</remarks>
public sealed record ChecklistClassCounts(
    string ClassName, int Done, int Ready, int Partial, int Total);

/// <summary>
/// How the Epic and Sky checklists are grouped and labelled — for every surface.
///
/// This exists because the three surfaces had already drifted (#184, bjstrange). The
/// 2026-08-16 rewrite that folded the widget's Epic and Sky cards into the Quest Tracker
/// re-grouped Sky by turn-in NPC, which puts every reward that one NPC hands out into a
/// single undifferentiated list — so "which pieces does THIS reward still need" stopped
/// being answerable, and the drop location stopped being drawn at all even though it was
/// sitting in <see cref="SkyQuestChecklistItem.Source"/> the whole time. EQBuddy Mobile
/// kept grouping by reward and kept showing the unassigned mark; the two desktop UIs
/// showed neither. Core's own comments promised "the row wears a *" and only the phone
/// was telling the truth.
///
/// Pure, so it is unit-tested rather than eyeballed, and shared, so a fix cannot reach
/// one window and miss the other two — the lesson #122 and #152 already charged us for.
/// </summary>
public static class QuestChecklistLayout
{
    /// <summary>The mark on a tick the loot auto-checker placed itself because several
    /// classes want the same item and it could not tell which of them earned it. The
    /// player moving it is the resolution; any manual toggle clears the flag.</summary>
    public const string UnassignedMark = " *";

    /// <summary>Sky, grouped by the REWARD you are working toward — the unit of "am I
    /// done", and the unit the player turns in. The NPC is still shown, on every row,
    /// where it belongs next to the drop location.
    ///
    /// Ordered by ACTIONABILITY within a class, which is the old widget card's rule
    /// restored verbatim (#205 bjstrange, #209 crydeevisions-arch, #210 liminalwarmth):
    /// unfinished first, and among the unfinished the CLOSEST TO DONE leads, because the
    /// question this screen answers is "which reward is actually in reach". Turned-in
    /// rewards sink to the bottom and read fine there as trophies. Alphabetical order
    /// interleaved all four states and buried the reward that needed one more piece
    /// wherever the alphabet happened to put it.</summary>
    /// <param name="repeatMultiIsland">How a step naming SEVERAL islands is placed (David,
    /// 2026-08-23, asked as its own question — he wanted the player to choose). <c>false</c>:
    /// it appears once, after the numbered groups, under a heading that NAMES its islands
    /// ("Islands 1.5, 4, and 8" — David, 2026-08-23). <c>true</c>: it appears under every island it names, so "what can I do on
    /// Island 4" is answered completely — at the cost of one step rendering three times.
    /// Either way the step is never counted twice; see <see cref="QuestChecklistGroup.Total"/>.</param>
    public static IReadOnlyList<QuestChecklistGroup> Sky(
        IEnumerable<SkyQuestChecklistItem> items,
        IReadOnlyCollection<string>? completedRewardKeys = null,
        bool repeatMultiIsland = false)
    {
        var completed = new HashSet<string>(completedRewardKeys ?? [], StringComparer.OrdinalIgnoreCase);
        return
        [
            .. items
                .GroupBy(i => (i.ClassName, i.Reward))
                .Select(g => new QuestChecklistGroup(
                    g.Key.ClassName,
                    g.Key.Reward,
                    [
                        .. g.OrderBy(i => i.QuestItem, StringComparer.OrdinalIgnoreCase)
                            .SelectMany(i => IslandPlacements(i, repeatMultiIsland))
                            .OrderBy(p => p.Sort)
                            // Steps sharing an island SET stay together — their heading names
                            // those islands now, so two sets are two headings.
                            .ThenBy(p => p.Tie, StringComparer.Ordinal)
                            .ThenBy(p => p.Item.QuestItem, StringComparer.OrdinalIgnoreCase)
                            .Select(p => new QuestChecklistRow(
                                p.Item.Id,
                                p.Item.ClassName,
                                p.Item.QuestItem.Length > 0 ? p.Item.QuestItem : p.Item.Reward,
                                // The island label comes off the detail when the row is
                                // already under that island's heading — see
                                // SkyIslands.WithoutIslePrefix. A multi-island row keeps every
                                // word even though its heading now names the islands: the
                                // heading says WHERE, the prose says which mob on each, and
                                // that mapping exists nowhere else.
                                Detail(p.Item.Npc, p.Tie.Length > 0
                                    // Clauses ascend, so the prose and the heading above it
                                    // name the islands in the same order (David, 2026-08-23).
                                    ? SkyIslands.OrderClausesByIsland(p.Item.Source)
                                    : SkyIslands.WithoutIslePrefix(p.Item.Source)),
                                p.Item.Acquired,
                                p.Item.AcquiredUnassigned,
                                p.Heading)),
                    ],
                    RewardKey(g.Key.ClassName, g.Key.Reward),
                    completed.Contains(RewardKey(g.Key.ClassName, g.Key.Reward)),
                    g.Select(i => i.Npc).FirstOrDefault(n => n.Trim().Length > 0)?.Trim()))
                .OrderBy(g => g.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Completed)
                .ThenByDescending(g => g.Progress)
                .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>Epic, grouped by the quest's own sections — the order the wiki walks
    /// them, which is the order the player does them.</summary>
    public static IReadOnlyList<QuestChecklistGroup> Epic(IEnumerable<EpicQuestChecklistItem> items)
    {
        return
        [
            .. items
                .GroupBy(i => (i.ClassName, Section: i.Section.Length > 0 ? i.Section : "Checklist"))
                .OrderBy(g => g.Key.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Min(i => i.Order))
                .ThenBy(g => g.Key.Section, StringComparer.OrdinalIgnoreCase)
                .Select(g => new QuestChecklistGroup(
                    g.Key.ClassName,
                    g.Key.Section,
                    [
                        .. g.OrderBy(i => i.Order)
                            .ThenBy(i => i.QuestItem, StringComparer.OrdinalIgnoreCase)
                            .Select(i => new QuestChecklistRow(
                                i.Id,
                                i.ClassName,
                                i.QuestItem.Length > 0 ? i.QuestItem : i.Reward,
                                // Source, or the quest name when there is none — the
                                // rule EQBuddy Mobile already uses. NOT both: an epic's
                                // quest name is the same on every row of the tab, so
                                // joining it to each one is noise, not detail.
                                i.Source.Length > 0 ? i.Source : i.QuestName,
                                i.Acquired,
                                i.AcquiredUnassigned)),
                    ])),
        ];
    }

    // ---- the state lens, the Ready band and the per-class counts ----
    //
    // All three came off the widget's Sky card when it became a launcher (66f6abc,
    // 2026-08-16) and none came back with the rest of it. #203, #205, #209 and #210 are
    // four people reporting the same hole from four angles, and #210 makes the argument
    // that settles where the code goes: Sky drops are random across classes, so most
    // players work all sixteen checklists at once and every question they bring to this
    // screen is cross-class and state-first. That is grouping, ordering and state — which
    // is what this file is for — so it lands HERE and not in a window, and the two
    // desktops and EQBuddy Mobile get the same answer (the #184 lesson).

    /// <summary>No filter. Not a state a group can be in — <see cref="States"/> lists it
    /// first because a lens needs a way to be switched off.</summary>
    public const string StateAny = "any state";
    /// <summary>Not turned in, and at least one piece still missing.</summary>
    public const string StateOpen = "open";
    /// <summary>Every piece in hand, hand-in outstanding. The Sky-specific prize, and the
    /// only state that names something to DO right now.</summary>
    public const string StateReady = "ready";
    /// <summary>Turned in (Sky), or finished (Epic, which has no separate hand-in).</summary>
    public const string StateDone = "done";

    /// <summary>The lens vocabulary, in the order a strip should offer it. The same four
    /// words the General tab's own state filter uses, so a player learns them once.</summary>
    public static readonly IReadOnlyList<string> States = [StateAny, StateOpen, StateReady, StateDone];

    /// <summary>Narrow to one slice of the lens. An unknown or absent state is "any" —
    /// a filter nobody set must never empty the screen.</summary>
    public static IEnumerable<QuestChecklistGroup> InState(
        IEnumerable<QuestChecklistGroup> groups, string? state) =>
        string.IsNullOrWhiteSpace(state) || state == StateAny || !States.Contains(state)
            ? groups
            : groups.Where(g => g.State == state);

    /// <summary>"What can I turn in right now, across every class" (#129, bjstrange) —
    /// every reward with all its pieces in hand and the hand-in outstanding, whoever it
    /// belongs to. The one question on this screen that names an action with a deadline,
    /// and the reason it survived on EQBuddy Mobile while the desktop lost it.
    ///
    /// Ordered by class then reward rather than by actionability: everything in here is
    /// equally actionable, so a stable, scannable order beats a ranking.</summary>
    public static IReadOnlyList<QuestChecklistGroup> ReadyToTurnIn(
        IEnumerable<QuestChecklistGroup> groups) =>
        [
            .. groups.Where(g => g.ReadyToTurnIn)
                .OrderBy(g => g.ClassName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
        ];

    /// <summary>Done / Ready / Partial / Total per class (#136, bjstrange), so "how am I
    /// doing across all sixteen" is one glance rather than a scroll. Classes come back in
    /// the order the groups arrive, which is already alphabetical.</summary>
    public static IReadOnlyList<ChecklistClassCounts> ClassCounts(
        IEnumerable<QuestChecklistGroup> groups) =>
        [
            .. groups
                .GroupBy(g => g.ClassName, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ChecklistClassCounts(
                    c.First().ClassName,
                    c.Count(g => g.State == StateDone),
                    c.Count(g => g.State == StateReady),
                    // PARTIAL is "started but not finished", which is narrower than open:
                    // a reward nobody has touched is open and is not partial, and that is
                    // exactly why D+R+P does not sum to the total.
                    c.Count(g => g.State == StateOpen && g.Done > 0),
                    c.Count()))
                .OrderBy(c => c.ClassName, StringComparer.OrdinalIgnoreCase),
        ];

    /// <summary>The desktop's reward key (class + reward), so "done" means the same
    /// thing on every screen.</summary>
    public static string RewardKey(string className, string reward) => className + "|" + reward;

    // ---- "who wants this drop?" (#108, liminalwarmth) --------------------------------
    //
    // The original ask was "clicking through each class scrolling for a drop is tedious",
    // and 1.69.0 answered it: type part of an item name and every class's matching rows
    // appear AT ONCE, grouped by ITEM, across all classes and ignoring the state filter.
    //
    // The Gate 2 rebuild kept a search box and lost both halves of that. Searching now
    // narrows rows INSIDE the per-class reward sections — so a drop three classes want
    // is three sections you still have to scroll between, which is the tedium the ask
    // named — and the query is applied AFTER the class picker, the class lens and the
    // state lens, so a match outside the current filters simply is not there. Same shape
    // as the write-only settings in trap 20: the data survived the move, the capability
    // did not, and nothing could see it.
    //
    // It lands HERE rather than in a window for the #184 reason: grouping and ordering
    // are this file's job, and the two desktops plus EQBuddy Mobile have to agree.

    /// <summary>Said on every surface that draws an item-grouped result, because the
    /// screenshot review found the gap the rule creates: the class lens and the state
    /// combo are still on screen above the results and no longer narrow them, so without
    /// this they are controls that look live and do nothing — "silent no-ops are broken",
    /// with the switch on the other side. The search box's own tooltip already promised
    /// this behaviour; the results have to say it too, where it is being relied on.</summary>
    public const string SearchScopeNote =
        "Searching every class — the class picker and the state filter don't narrow this.";

    /// <summary>One class that wants a given item, and where it wants it.</summary>
    /// <param name="RowId">The checklist row, so a view can wire the SAME tick it would
    /// wire in the normal layout — an item-grouped result is a different arrangement of
    /// the rows, not a read-only report.</param>
    public sealed record ChecklistItemWanter(
        string RowId,
        string ClassName,
        string Reward,
        string Detail,
        bool Acquired,
        bool RewardCompleted);

    /// <summary>An item, and every class whose checklist asks for it.</summary>
    public sealed record ChecklistItemMatch(
        string Title,
        IReadOnlyList<ChecklistItemWanter> Wanters)
    {
        public int Held => Wanters.Count(w => w.Acquired);
        public int Total => Wanters.Count;

        /// <summary>"3 classes want this" is the answer #108 asked for; one class is a
        /// statement about that class instead.</summary>
        public int Classes => Wanters
            .Select(w => w.ClassName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    /// <summary>Search a checklist by item, across every class.
    ///
    /// <para><b>Pass the UNFILTERED groups.</b> Search deliberately ignores the class
    /// picker, the class lens and the state lens — "filters shape the tabs, never the
    /// search", the same rule the General tab already follows and the rule 1.69.0
    /// shipped this under. A cross-class question answered inside one class's filter is
    /// not answered at all.</para>
    ///
    /// <para>A query matching a REWARD name pulls in all of that reward's rows, because
    /// "any part of an item (or reward) name" is what the search box has always
    /// promised.</para>
    /// </summary>
    /// <returns>Empty when the query is blank — the caller draws its normal layout.</returns>
    public static IReadOnlyList<ChecklistItemMatch> SearchByItem(
        IEnumerable<QuestChecklistGroup> groups, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var q = query.Trim();

        var wanters = new List<(string Title, ChecklistItemWanter Wanter)>();
        foreach (var group in groups)
        {
            var rewardHit = Hit(group.Title) || Hit(group.ClassName + " · " + group.Title);
            foreach (var row in group.Rows)
                if (rewardHit || Hit(row.Title) || Hit(row.Detail))
                    wanters.Add((row.Title, new ChecklistItemWanter(
                        row.Id, row.ClassName, group.Title, row.Detail,
                        row.Acquired, group.Completed)));
        }

        return
        [
            .. wanters
                .GroupBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ChecklistItemMatch(
                    g.First().Title,
                    [
                        .. g.Select(x => x.Wanter)
                            .OrderBy(w => w.ClassName, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(w => w.Reward, StringComparer.OrdinalIgnoreCase),
                    ]))
                // Most-wanted first: the whole point is "who wants this drop", so the
                // item three classes are queuing for outranks the one only a bard needs.
                // Then alphabetical, so a repeated search lands in the same place.
                .OrderByDescending(m => m.Classes)
                .ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase),
        ];

        bool Hit(string s) => s.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>"Cilin Spellsinger · Isle 6: Bazzt Zzzt" — whichever halves exist. The
    /// drop location is the half #184 asked for back, and the half that was never drawn.</summary>
    /// <summary>
    /// Where one step sits in the island order, and under which heading.
    ///
    /// Returns SEVERAL placements only when the player has asked for multi-island steps to be
    /// repeated. Otherwise every step yields exactly one, which is what keeps a checklist row
    /// a checklist row: one tick, one place.
    ///
    /// The sort key is the island number itself, so "numerically" is literal — and it matters,
    /// because Sky has an island 1.5. Sorted as TEXT that lands where it belongs by luck, and
    /// the luck runs out on any two-digit number.
    /// </summary>
    private static IEnumerable<(SkyQuestChecklistItem Item, double Sort, string Heading, string Tie)>
        IslandPlacements(SkyQuestChecklistItem item, bool repeatMultiIsland)
    {
        var islands = SkyIslands.Parse(item.Source);
        if (islands.Count == 0)
        {
            // No island named — and that is the truth for 95 of 223 steps, not a gap.
            // Sorted last, keeping the flat presentation these have always had.
            yield return (item, AnywhereSort, SkyIslands.AnywhereHeading, "");
            yield break;
        }
        if (islands.Count == 1)
        {
            yield return (item, islands[0], SkyIslands.Heading(islands[0]), "");
            yield break;
        }
        if (!repeatMultiIsland)
        {
            // After the numbered islands, before "anywhere": we know every one of these
            // places, so they must not fall in with the steps nobody has written down.
            //
            // The TIE key groups steps by their island SET. Since the heading names the
            // islands (David, 2026-08-23), two different sets are two different headings and
            // must not interleave — before that they shared one "Several islands" bucket
            // only because the heading could not tell them apart.
            yield return (item, SeveralSort, SkyIslands.SeveralHeading(islands), SetKey(islands));
            yield break;
        }
        foreach (var island in islands)
            yield return (item, island, SkyIslands.Heading(island), "");
    }

    /// <summary>A stable, sortable key for a set of islands: zero-padded so "10" could never
    /// sort between "1" and "2", and ordered so the set starting on the lowest island leads.
    /// Text, because it only ever has to be consistent — it is never shown.</summary>
    private static string SetKey(IReadOnlyList<double> islands) =>
        string.Join("|", islands.Select(i => i.ToString("00.0",
            System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Sorts after every real island (Sky's highest is 8) and before "anywhere".</summary>
    private const double SeveralSort = 90;

    /// <summary>Last. The steps with no location named close the list.</summary>
    private const double AnywhereSort = 99;

    private static string Detail(string npc, string source)
    {
        npc = npc.Trim();
        source = source.Trim();
        if (npc.Length == 0) return source;
        if (source.Length == 0) return npc;
        return npc + " · " + source;
    }
}
