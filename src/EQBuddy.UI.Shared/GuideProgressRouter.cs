using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>Where an objective's "done" actually lives.</summary>
public enum GuideProgressHome
{
    /// <summary>The per-character guide ledger (<see cref="QuestLedgerStore.GuideProgress"/>) —
    /// the only home for a step nothing else in EQBuddy has an opinion about.</summary>
    GuideLedger,

    /// <summary>The Sky turn-in store the classic checklist, the phone and the achievements
    /// import have always shared (<c>AppSettings.SkyQuestCompleted</c>).</summary>
    SkyTurnIn,

    /// <summary>One <see cref="SkyQuestChecklistItem"/> row of the guide's own reward group -
    /// the box the classic checklist has always drawn for "do I hold the Stone Amulet".
    ///
    /// <para>An objective that is about acquiring exactly one of its group's turn-in items is
    /// not a new fact either: it is that item's <c>Acquired</c> flag, which the loot
    /// auto-tick already writes and four surfaces already read. Without this home the guide
    /// would carry a second tick beside the checklist's for one item - trap 4 on the first
    /// screen a Warrior sees - and the loot auto-tick would light one of them and not the
    /// other.</para></summary>
    SkyItem,

    /// <summary>One <see cref="EpicQuestChecklistItem"/> row — the box the Epic tab has drawn
    /// since 1.x, and the one <c>EpicLootAutoCheck</c> writes.
    ///
    /// <para><b>An epic guide's objective IS a checklist row, not a copy of one.</b> Every one
    /// of the 486 objectives is generated FROM a row and carries that row's id, so the guide
    /// keeps no tick of its own: the master "Epic complete" button, the loot auto-tick, the
    /// phone's tap and a click on the guide row all move the same boolean. Without this home
    /// the Epic tab would have looked guided and quietly stopped agreeing with itself the
    /// first time a Red Dragon Scale dropped (trap 4).</para>
    ///
    /// <para>Matched on the OBJECTIVE'S ID, not on item names: the row's own identity is what
    /// the generator carried across, and it is unique. That is why this home needs no
    /// objective-type gate the way <see cref="SkyItem"/> does — there is no guessing to
    /// constrain.</para></summary>
    EpicItem,

    /// <summary>One turn-in item of an ordinary quest, counted in the quest ledger — the
    /// "3 / 4" the General tab has drawn since 1.x.
    ///
    /// <para><b>The bags are the truth here, and a click is not.</b> A harvested guide's
    /// "Turn-in pieces" stage is the quest's own item list, and whether you hold four Orc
    /// Belts is answered by <c>QuestItemProgress</c> — loot lines, an inventory dump, or the
    /// manual count the player already sets on the quest card. A tick of its own would be a
    /// fifth answer to a question four surfaces already agree on (trap 4), so
    /// <see cref="GuideProgressRouter.SetDone"/> REFUSES a manual tick on this home rather
    /// than writing one somewhere quieter.</para>
    ///
    /// <para>Matched on the objective's single <c>ItemNames</c> entry against the quest's own
    /// needs, the same narrowing <see cref="SkyItem"/> does: exactly one match or no home.</para>
    /// </summary>
    LedgerItem,

    /// <summary>The quest's completion record — the box the General tab's "completed" toggle
    /// writes (<c>QuestLedgerStore.SetCompleted</c> / <c>RecordCompletion</c>).
    ///
    /// <para>The skeleton stage's closing "Hand the pieces to …" row IS that toggle. The
    /// guide keeps no second copy: mark the quest done on the card, on the phone, or on this
    /// row, and all three move one integer. <b>A separate home from
    /// <see cref="LedgerItem"/> because it is a different store and a different verb</b> —
    /// the pieces are counted and the hand-in is declared, and folding them would make one of
    /// the two lie.</para></summary>
    QuestCompletion,
}

/// <summary>
/// The one door between a guide's objective rows and the two stores behind them
/// (Fable plan §4, P1b).
///
/// <para><b>Why a router at all.</b> A guide objective that carries a <c>RewardKey</c> is not
/// a new fact — it is the Plane of Sky turn-in the checklist has tracked since 1.x, wearing a
/// guide row's clothes. If the guide kept its own tick for it there would be two sources for
/// one fact (trap 4), and the visible cost is specific: turn a reward in on the phone, open
/// the guide on the desktop, and the guide still asks you to do it. So the rule is
/// <b>read FROM and write THROUGH the existing path</b> — <see cref="SkyCompleteToggle"/>,
/// the same call a click on the classic checklist makes, consuming the reward's items in the
/// quest ledger exactly once — and <see cref="QuestLedgerStore.GuideProgress"/> never holds a
/// reward objective's done id.</para>
///
/// <para><b>Skip is the asymmetry, and it is deliberate.</b> "I am not doing this step" has no
/// home in the Sky store and is a different statement from "I turned this in", so it lands in
/// the guide ledger for EVERY objective. One fact, one store, still — just a different fact.
/// A skipped reward objective can perfectly well be turned in later; the two answers do not
/// contradict across stores the way done/skipped do inside one.</para>
///
/// <para><b>Nothing here infers.</b> At MVP there is no log-driven guide progress: every verb
/// on this class begins with a player's click. <c>ItemNames</c> exists on the objective for a
/// Phase-5 auto-tick and is not read by anything yet — see the plan's §4 rejected list.</para>
///
/// <para>Framework-free and in UI.Shared rather than Core because the write side needs
/// <see cref="SkyCompleteToggle"/>, which is where the "turning in acquires every item in the
/// reward" rule lives. Desktop, shell and phone all call this — parity by shared module.</para>
/// </summary>
public static class GuideProgressRouter
{
    /// <summary>The objective types whose whole content is "get hold of this item", and so
    /// the only ones that may read a checklist item's box. A Travel or TalkToNpc step that
    /// happens to mention an item name in passing is not that item's tick.</summary>
    public static readonly string[] ItemBackedObjectiveTypes = ["Loot", "Farm", "Collect"];

    /// <summary>Which store owns this objective's done state, with no Sky group in hand -
    /// the answer for a guide that does not layer on the Sky checklist at all. Never
    /// <see cref="GuideProgressHome.SkyItem"/>, because that home is a row of a specific
    /// group and there is no group here.</summary>
    public static GuideProgressHome HomeFor(GuideObjective objective) =>
        HomeFor(objective, [], [], out _, out _);

    /// <summary>Which store owns this objective's done state - the whole routing decision,
    /// pure and separately testable, and the ONE producer of it (both overloads land here).
    ///
    /// <para>A reward key means the Sky turn-in store owns it. Otherwise an acquire-shaped
    /// objective (<see cref="ItemBackedObjectiveTypes"/>) naming <b>exactly one</b> of
    /// <paramref name="groupItems"/> is that item's box - <paramref name="backingItem"/> hands
    /// it back so the caller never re-derives the match. Two matches, none, or the wrong
    /// objective type all fall to the guide ledger: an ambiguous claim on a shared store is
    /// worse than a private tick.</para>
    ///
    /// <para><paramref name="groupItems"/> is the reward group's OWN rows and nothing wider.
    /// "Wind Rune Azia" is a row for the Bard and a row for the Warrior; they are two facts
    /// about two quests and must never resolve to each other.</para></summary>
    public static GuideProgressHome HomeFor(GuideObjective objective,
        IReadOnlyList<SkyQuestChecklistItem> groupItems, out SkyQuestChecklistItem? backingItem) =>
        HomeFor(objective, groupItems, [], out backingItem, out _);

    /// <summary>Which store owns this objective's done state, with the Epic tab's rows in hand
    /// as well — <b>the one producer</b>, which both shorter overloads land in.
    ///
    /// <para><paramref name="epicRows"/> is the rows the tab is CURRENTLY showing, which is
    /// what makes the classic-era lens one producer rather than two: a row the filter dropped
    /// is not in this list, so its objective has no home here and the projection does not draw
    /// it. The guide never re-reads <c>AvailableInClassic</c> itself.</para>
    ///
    /// <para>Order matters and is stated rather than implied: a reward key wins (a Sky
    /// turn-in), then an epic row bearing this objective's id, then the single-item Sky match.
    /// The three sets are disjoint by construction — an epic objective carries no reward key
    /// and no item names, and a Sky guide's caller passes no epic rows — so the order is
    /// belt-and-braces rather than a tie-break anything relies on.</para></summary>
    public static GuideProgressHome HomeFor(GuideObjective objective,
        IReadOnlyList<SkyQuestChecklistItem> groupItems,
        IReadOnlyList<EpicQuestChecklistItem> epicRows,
        out SkyQuestChecklistItem? backingItem,
        out EpicQuestChecklistItem? backingRow)
    {
        var target = TargetFor(objective, groupItems, epicRows, null);
        backingItem = target.SkyItem;
        backingRow = target.EpicRow;
        return target.Home;
    }

    /// <summary>Which store owns this objective's done state, with the QUEST the guide walks
    /// in hand as well — <b>the one producer</b>, which every overload above lands in.
    ///
    /// <para><paramref name="quest"/> is the <see cref="QuestMatch"/> the General tab already
    /// computed for this guide's <c>QuestName</c>, or null for a guide that walks no ordinary
    /// quest. It is what turns a harvested guide's "Turn-in pieces" rows from private ticks
    /// into the counts and the completion the quest card has always drawn.</para>
    ///
    /// <para>Order: a reward key wins (a Sky turn-in), then an epic row bearing this
    /// objective's id, then the quest's own two homes, then the single-item Sky match. The
    /// sets are disjoint by construction — a harvested objective carries no reward key and no
    /// epic id, and no caller hands over both a Sky reward group and a quest — so the order is
    /// belt-and-braces rather than a tie-break anything relies on.</para></summary>
    public static GuideProgressTarget TargetFor(GuideObjective objective,
        IReadOnlyList<SkyQuestChecklistItem> groupItems,
        IReadOnlyList<EpicQuestChecklistItem> epicRows,
        QuestMatch? quest)
    {
        if (objective.RewardKey.Length > 0)
            return new GuideProgressTarget(GuideProgressHome.SkyTurnIn, null, null, null);

        foreach (var row in epicRows)
            if (string.Equals(row.Id, objective.Id, StringComparison.OrdinalIgnoreCase))
                return new GuideProgressTarget(GuideProgressHome.EpicItem, null, row, null);

        if (quest is not null)
        {
            // The hand-in row IS the quest card's completed toggle. Type-gated rather than
            // id-gated: what makes it the quest's completion is that it is the turn-in of the
            // quest this guide walks, not which generator minted its id.
            if (string.Equals(objective.ObjectiveType, "TurnIn", StringComparison.Ordinal))
                return new GuideProgressTarget(GuideProgressHome.QuestCompletion, null, null, null);

            if (ItemBackedObjectiveTypes.Contains(objective.ObjectiveType, StringComparer.Ordinal))
            {
                QuestItemProgress? onlyNeed = null;
                var ambiguous = false;
                foreach (var need in quest.Items)
                {
                    if (!objective.ItemNames.Contains(need.Name, StringComparer.OrdinalIgnoreCase))
                        continue;
                    // Two matches is an ambiguous claim on a shared count, which is worse than
                    // a private tick — the same narrowing SkyItem does below.
                    if (onlyNeed is not null) { ambiguous = true; break; }
                    onlyNeed = need;
                }
                if (!ambiguous && onlyNeed is not null)
                    return new GuideProgressTarget(GuideProgressHome.LedgerItem, null, null, onlyNeed);
                if (ambiguous)
                    return new GuideProgressTarget(GuideProgressHome.GuideLedger, null, null, null);
            }
        }

        if (!ItemBackedObjectiveTypes.Contains(objective.ObjectiveType, StringComparer.Ordinal))
            return new GuideProgressTarget(GuideProgressHome.GuideLedger, null, null, null);

        SkyQuestChecklistItem? only = null;
        foreach (var item in groupItems)
        {
            if (!objective.ItemNames.Contains(item.QuestItem, StringComparer.OrdinalIgnoreCase))
                continue;
            if (only is not null)
                return new GuideProgressTarget(GuideProgressHome.GuideLedger, null, null, null);
            only = item;
        }

        return only is null
            ? new GuideProgressTarget(GuideProgressHome.GuideLedger, null, null, null)
            : new GuideProgressTarget(GuideProgressHome.SkyItem, only, null, null);
    }

    /// <summary>
    /// May a click on this row change it? <b>False is not a bug, and it must never render as
    /// a row that silently ignores you</b> — it is the honest answer for a
    /// <see cref="GuideProgressHome.LedgerItem"/> row, whose state is how many of the item the
    /// player is carrying. A surface drawing one dims the box and says why (trap 17); it does
    /// not draw a live control over a store that will refuse the write (silent no-ops are
    /// broken).
    /// </summary>
    public static bool CanSetDone(GuideProgressHome home) =>
        home != GuideProgressHome.LedgerItem;

    /// <summary>Why that row refuses a tick, in the player's terms — the sentence the dimmed
    /// box owes them. Empty for every home that takes one.</summary>
    public static string RefusalNote(GuideProgressHome home) =>
        home == GuideProgressHome.LedgerItem
            ? "This ticks itself from what you are carrying — loot it, or set the count on the "
              + "quest card."
            : "";

    /// <summary>Has the player done this objective? Reward objectives answer from the Sky
    /// turn-in store, so a turn-in recorded anywhere — the classic checklist, the phone, the
    /// achievements import, the inventory-driven auto-complete — reads as done inside the
    /// guide the same tick.</summary>
    public static bool IsDone(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective) =>
        IsDone(settings, ledger, characterKey, guideId, objective, []);

    /// <summary>Has the player done this objective? Reward objectives answer from the Sky
    /// turn-in store and item-backed ones from their own checklist row, so a turn-in or a
    /// looted piece recorded anywhere - the classic checklist, the phone, the achievements
    /// import, the loot auto-tick - reads as done inside the guide the same tick.</summary>
    public static bool IsDone(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective,
        IReadOnlyList<SkyQuestChecklistItem> groupItems) =>
        IsDone(settings, ledger, characterKey, guideId, objective, groupItems, []);

    /// <summary>Has the player done this objective, with the Epic tab's rows in hand as well?
    /// An epic step answers from its own checklist row, so the loot auto-tick, the master
    /// "Epic complete" button and the phone's tap all read through as done the same tick.</summary>
    public static bool IsDone(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective,
        IReadOnlyList<SkyQuestChecklistItem> groupItems,
        IReadOnlyList<EpicQuestChecklistItem> epicRows) =>
        IsDone(settings, ledger, characterKey, guideId, objective, groupItems, epicRows, null);

    /// <summary>Has the player done this objective, with the ordinary QUEST the guide walks in
    /// hand as well? A "Turn-in pieces" row answers from the quest ledger — the count the loot
    /// tail, the inventory dump and the quest card's own manual number already agree on, and
    /// the completion the card's toggle writes — so the guide and the card can never disagree
    /// about whether you are holding four Orc Belts.</summary>
    public static bool IsDone(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective,
        IReadOnlyList<SkyQuestChecklistItem> groupItems,
        IReadOnlyList<EpicQuestChecklistItem> epicRows,
        QuestMatch? quest)
    {
        var target = TargetFor(objective, groupItems, epicRows, quest);
        return target.Home switch
        {
            GuideProgressHome.SkyTurnIn => SkyCompleteToggle.IsTurnedIn(settings, objective.RewardKey),
            GuideProgressHome.SkyItem => target.SkyItem!.Acquired,
            GuideProgressHome.EpicItem => target.EpicRow!.Acquired,
            GuideProgressHome.LedgerItem => target.LedgerNeed!.Have >= target.LedgerNeed.Need,
            GuideProgressHome.QuestCompletion =>
                ledger.CompletedFor(characterKey).TryGetValue(quest!.Quest.Name, out var times)
                    && times > 0,
            _ => ledger.GuideProgressFor(characterKey, guideId).DoneObjectiveIds
                .Contains(objective.Id, StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>Tick or untick an objective, into whichever store owns it.
    ///
    /// <para>The Sky path is <see cref="SkyCompleteToggle.MarkTurnedIn"/> / <see
    /// cref="SkyCompleteToggle.Reopen"/> verbatim — including the quest ledger's completion
    /// record and the item consume, which is why <paramref name="ledger"/> and
    /// <paramref name="characterKey"/> are handed over on both paths. Un-ticking a reward
    /// reopens it and leaves its item boxes alone, exactly as a mis-click on the classic
    /// checklist does; that asymmetry is the toggle's, not this class's.</para>
    ///
    /// <para>Ticking clears a skip on the same objective, whichever store the tick went to —
    /// the guide row cannot show struck-out and done at once.</para></summary>
    public static void SetDone(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective, bool done) =>
        SetDone(settings, ledger, characterKey, guideId, objective, [], done);

    /// <summary>Tick or untick an objective, into whichever of the THREE stores owns it —
    /// see the one-argument form for the Sky turn-in rules, which are unchanged.
    ///
    /// <para>An item-backed objective writes the two lines a click on the classic checklist
    /// writes: the box, and the "we guessed which class earned this" flag that the player
    /// deciding always clears. Same store, same setter, same row — so ticking the guide step
    /// and ticking the old checklist row are one action, not two that agree by luck.</para></summary>
    public static void SetDone(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective,
        IReadOnlyList<SkyQuestChecklistItem> groupItems, bool done) =>
        SetDone(settings, ledger, characterKey, guideId, objective, groupItems, [], done);

    /// <summary>Tick or untick an objective, into whichever of the FOUR stores owns it — see
    /// the shorter forms for the Sky rules, which are unchanged.
    ///
    /// <para>An EPIC step writes the two lines the Epic tab's own checkbox writes: the row's
    /// box, and the "we guessed which class earned this" flag that the player deciding always
    /// clears. Same store, same row, same setter — so ticking the guide step and ticking the
    /// classic Epic row are one action rather than two that agree by luck.</para></summary>
    public static void SetDone(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective,
        IReadOnlyList<SkyQuestChecklistItem> groupItems,
        IReadOnlyList<EpicQuestChecklistItem> epicRows, bool done) =>
        SetDone(settings, ledger, characterKey, guideId, objective, groupItems, epicRows,
            null, done);

    /// <summary>Tick or untick an objective, into whichever of the SIX stores owns it — see the
    /// shorter forms for the Sky and epic rules, which are unchanged.
    ///
    /// <para>The hand-in row writes the line the quest card's completed toggle writes
    /// (<see cref="QuestLedgerStore.SetCompleted"/>), so marking the quest done on the card and
    /// ticking this row are one action.</para>
    ///
    /// <para><b>A <see cref="GuideProgressHome.LedgerItem"/> row REFUSES the write</b>, and
    /// deliberately does not fall back to the guide ledger. Its state is how many of the item
    /// you are holding; a private tick beside that count would be a second answer that the next
    /// loot line contradicts. A surface must ask <see cref="CanSetDone"/> and dim the box — a
    /// live control over this call is a silent no-op, which is broken.</para></summary>
    public static void SetDone(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective,
        IReadOnlyList<SkyQuestChecklistItem> groupItems,
        IReadOnlyList<EpicQuestChecklistItem> epicRows,
        QuestMatch? quest, bool done)
    {
        var target = TargetFor(objective, groupItems, epicRows, quest);
        var home = target.Home;
        var item = target.SkyItem;
        var row = target.EpicRow;

        if (home == GuideProgressHome.LedgerItem) return;

        if (home == GuideProgressHome.QuestCompletion)
        {
            ledger.SetCompleted(characterKey, quest!.Quest.Name, done);
        }
        else if (home == GuideProgressHome.SkyTurnIn)
        {
            if (done)
                SkyCompleteToggle.MarkTurnedIn(settings, objective.RewardKey,
                    SkyCompleteToggle.ItemsFor(settings.SkyQuestChecklist, objective.RewardKey),
                    ledger, characterKey);
            else
                SkyCompleteToggle.Reopen(settings, objective.RewardKey);
        }
        else if (home == GuideProgressHome.SkyItem)
        {
            item!.Acquired = done;
            // The player deciding IS the resolution of an unassigned auto-tick — the same
            // line QuestsView and the phone already run on the raw checklist row.
            item.AcquiredUnassigned = false;
        }
        else if (home == GuideProgressHome.EpicItem)
        {
            row!.Acquired = done;
            row.AcquiredUnassigned = false;
        }
        else
        {
            // The objective, not its id: the store refuses a reward-keyed one and writes
            // nothing, so the routing rule is enforced on both sides of the call rather than
            // trusted on this one. Unreachable here by construction.
            ledger.SetObjectiveDone(characterKey, guideId, objective, done);
            return;
        }

        // The done fact stayed in a Sky store; only the contradicting skip is ours to clear,
        // and only when there is one — an untouched guide must not gain a row because a
        // reward was turned in, or a piece looted, on another screen.
        if (done && IsSkipped(ledger, characterKey, guideId, objective))
            ledger.SetObjectiveSkipped(characterKey, guideId, objective.Id, false);
    }

    /// <summary>Is this objective struck out? Always the guide ledger — see the class
    /// note.</summary>
    public static bool IsSkipped(QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective) =>
        ledger.GuideProgressFor(characterKey, guideId).SkippedObjectiveIds
            .Contains(objective.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Strike an objective out, or take the strike back. Striking out a NON-reward
    /// objective clears its tick (the store does that); striking out a reward objective
    /// leaves the turn-in alone, because a turn-in that happened is a fact about the past and
    /// a skip is a statement about the future.</summary>
    public static void SetSkipped(QuestLedgerStore ledger,
        string characterKey, string guideId, GuideObjective objective, bool skipped) =>
        ledger.SetObjectiveSkipped(characterKey, guideId, objective.Id, skipped);

    /// <summary>How far through a guide the player is: objectives done, objectives skipped,
    /// and the total. Counts through <see cref="IsDone"/>, so the Sky-owned rows count from
    /// the Sky store rather than from a copy that could disagree with it.</summary>
    public static GuideProgressCounts Counts(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, Guide guide) =>
        Counts(settings, ledger, characterKey, guide, []);

    /// <summary>How far through a guide the player is, with the reward group's rows in hand
    /// so item-backed steps count from their own boxes. Same <see cref="IsDone"/> the rows
    /// are drawn from, so the caption under a heading and the ticks under it cannot
    /// disagree — one producer, two readers.</summary>
    public static GuideProgressCounts Counts(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, Guide guide, IReadOnlyList<SkyQuestChecklistItem> groupItems) =>
        Counts(settings, ledger, characterKey, guide, groupItems, []);

    /// <summary>How far through a guide the player is, counting only the objectives whose rows
    /// are on screen. <paramref name="epicRows"/> is the tab's CURRENT rows, so a class under
    /// the classic-era lens counts what it draws — a caption reading "3 of 66" over 14 visible
    /// rows is the same self-contradiction the heading counts exist to prevent.</summary>
    public static GuideProgressCounts Counts(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, Guide guide, IReadOnlyList<SkyQuestChecklistItem> groupItems,
        IReadOnlyList<EpicQuestChecklistItem> epicRows) =>
        Counts(settings, ledger, characterKey, guide, groupItems, epicRows, null);

    /// <summary>How far through a guide the player is, with the ordinary quest in hand too —
    /// the same <see cref="IsDone"/> the rows are drawn from, so a "Turn-in pieces" caption and
    /// the ticks under it cannot disagree.</summary>
    public static GuideProgressCounts Counts(AppSettings settings, QuestLedgerStore ledger,
        string characterKey, Guide guide, IReadOnlyList<SkyQuestChecklistItem> groupItems,
        IReadOnlyList<EpicQuestChecklistItem> epicRows, QuestMatch? quest)
    {
        var done = 0;
        var skipped = 0;
        var total = 0;
        foreach (var objective in Drawn(guide, epicRows))
        {
            total++;
            if (IsDone(settings, ledger, characterKey, guide.Id, objective, groupItems, epicRows,
                    quest)) done++;
            else if (IsSkipped(ledger, characterKey, guide.Id, objective)) skipped++;
        }
        return new GuideProgressCounts(done, skipped, total);
    }

    /// <summary>
    /// The objectives a surface actually draws for this guide — <b>the one producer</b> of that
    /// set, so the rows, the counts, the card's "what is next" and the phone all walk the same
    /// list.
    ///
    /// <para>Everything, except for an <see cref="GuideType.EpicQuest"/> guide: there an
    /// objective is drawn only when the tab is still showing the row it was generated from.
    /// That is the whole of the classic-era lens on a guided class, and it reads the ROW's own
    /// <c>AvailableInClassic</c> flag through the rows the caller already filtered — one
    /// producer of "is this row in this era", not a second copy of the predicate.</para>
    ///
    /// <para>Keyed on the guide TYPE rather than on "were any rows handed in", because those
    /// two differ in exactly the case that matters: a class the lens has emptied must draw
    /// nothing, not everything.</para></summary>
    public static IEnumerable<GuideObjective> Drawn(
        Guide guide, IReadOnlyList<EpicQuestChecklistItem> epicRows)
    {
        if (guide.GuideType != GuideType.EpicQuest) return guide.AllObjectives;
        var ids = new HashSet<string>(epicRows.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
        return guide.AllObjectives.Where(o => ids.Contains(o.Id));
    }
}

/// <summary>
/// Where one objective's done state lives, and the row it lives in — <b>one value carrying the
/// answer and the thing the answer is about</b>, so a caller never re-derives the match the
/// router already made (the <c>out</c> pairs it replaced were one parameter away from being
/// three).
///
/// <para>Exactly one of <paramref name="SkyItem"/>, <paramref name="EpicRow"/> and
/// <paramref name="LedgerNeed"/> is non-null, and which one is decided by
/// <paramref name="Home"/>; the other three homes carry none, because a Sky turn-in is keyed by
/// the objective's own reward key, a quest completion by the quest's name, and a guide-ledger
/// row by the objective's id.</para></summary>
public readonly record struct GuideProgressTarget(
    GuideProgressHome Home,
    SkyQuestChecklistItem? SkyItem,
    EpicQuestChecklistItem? EpicRow,
    QuestItemProgress? LedgerNeed);

/// <summary>A guide's progress in three numbers. <paramref name="Skipped"/> excludes anything
/// already counted as <paramref name="Done"/>, so the two never double-count a row and
/// Done + Skipped ≤ Total.</summary>
public readonly record struct GuideProgressCounts(int Done, int Skipped, int Total)
{
    /// <summary>Rows still asking something of the player.</summary>
    public int Remaining => Total - Done - Skipped;
}
