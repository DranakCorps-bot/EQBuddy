namespace EQBuddy.Core;

/// <summary>Which honest claim a leftover row makes. Two bands, never mixed (Bevel,
/// Helm-signed 2026-09-02): one is a statement about the whole game, the other is a
/// statement about the classes this character does not play.</summary>
public enum SkyLeftoverBand
{
    /// <summary>Every Sky reward in the game that takes this item is turned in. The
    /// reporter's own sentence, and the only strong claim here.</summary>
    NoLongerNeeded,
    /// <summary>Some Sky reward still takes it, but none of them belongs to a class this
    /// character holds. A weaker claim — a Legends character unlocks classes later — so it
    /// gets its own heading rather than being folded under "No longer needed".</summary>
    OtherClassesWant,
}

/// <summary>One held item the Sky checklist is done with, in one of the two bands.</summary>
/// <param name="Item">The checklist's spelling of the item (the wiki's name), not the
/// dump's — an upgraded "+1" row folds to the same base name on both sides.</param>
/// <param name="Held">How many the dump says the character is carrying, stacks honoured.</param>
/// <param name="Where">"bags", "bank", "worn", or those joined with " and " — collapsed
/// from the dump's own <see cref="InventoryFile.Entry.Location"/>s. "" when the dump has no
/// row for it (the log saw it arrive after the dump was written). The ask is bag SPACE, so
/// an item sitting in the bank is not the problem he has and the row says so.</param>
/// <param name="UsedBy">The turned-in rewards that took this item, as "Class · Reward".
/// The tooltip's evidence for the claim: this is WHY it is no longer needed.</param>
/// <param name="StillWantedBy">The classes whose open rewards still take it. Band B only;
/// empty for Band A, which by definition has no open wanter left.</param>
public sealed record SkyLeftoverRow(
    string Item, int Held, string Where, SkyLeftoverBand Band,
    IReadOnlyList<string> UsedBy, IReadOnlyList<string> StillWantedBy)
{
    /// <summary>The row as all three surfaces draw it — `{Item} ×{held} · {where}`, with
    /// the location clause dropped when the dump could not say. Said once here because the
    /// WPF band, the Avalonia band and the phone group are three renderers of one decision
    /// and this is exactly the kind of formatting that drifts between them.</summary>
    public string Line => Where.Length > 0 ? $"{Item} ×{Held} · {Where}" : $"{Item} ×{Held}";
}

/// <summary>An item whose Sky demand is satisfied but which ANOTHER quest still takes as a
/// turn-in — so it is deliberately in neither band, and the count is offered for the band's
/// hover instead. Saying "safe to free" about an item another quest needs is the one way
/// this feature can cost a player something.</summary>
public sealed record SkyLeftoverHeld(string Item, int Held, IReadOnlyList<string> Quests);

/// <summary>What the join found: two bands and the vetoed remainder.</summary>
public sealed record SkyLeftoverReport(
    IReadOnlyList<SkyLeftoverRow> NoLongerNeeded,
    IReadOnlyList<SkyLeftoverRow> OtherClassesWant,
    IReadOnlyList<SkyLeftoverHeld> WantedByAnotherQuest)
{
    public static readonly SkyLeftoverReport Empty = new([], [], []);

    /// <summary>True when there is nothing at all to draw — the band's own absence rule
    /// (a permanently-present band reading "nothing" is how a player learns to stop
    /// looking at it).</summary>
    public bool IsEmpty => NoLongerNeeded.Count == 0 && OtherClassesWant.Count == 0;

    /// <summary>The two headings, verbatim and in one place, because the honesty of this
    /// feature is carried by the words: band B must never appear under band A's heading.</summary>
    public string NoLongerNeededHeading => $"No longer needed — {NoLongerNeeded.Count}";
    public string OtherClassesWantHeading => $"Other classes still want — {OtherClassesWant.Count}";

    /// <summary>The hover clause for the items a non-Sky quest vetoed, or "" when none.
    /// They are not rows: the player is told they exist and which quest wants them, so an
    /// item missing from the band is explained rather than simply absent.</summary>
    public string OtherQuestNote => WantedByAnotherQuest.Count == 0 ? ""
        : $"{WantedByAnotherQuest.Count} more "
            + (WantedByAnotherQuest.Count == 1 ? "is" : "are")
            + " still wanted by another quest: "
            + string.Join(", ", WantedByAnotherQuest.Select(h =>
                $"{h.Item} ({string.Join(", ", h.Quests)})"))
            + ".";
}

/// <summary>
/// #243 (tvongaza): *"cross check which sky quests you've completed and which sky quest
/// items you no longer need as you've completed all the quests which use them."*
///
/// Every piece already existed and the JOIN did not — the dump
/// (<see cref="InventoryFile.Snapshot"/>), what Sky wants
/// (<see cref="AppSettings.SkyQuestChecklist"/>), and what is turned in
/// (<see cref="AppSettings.SkyQuestCompleted"/>). This is the complement of
/// <see cref="QuestChecklistLayout.SearchByItem"/>, which answers "who wants this item";
/// this one answers "does anyone still".
///
/// <para><b>It is a list. Nothing here destroys, sells, ticks or unticks anything.</b> The
/// player reads it and decides — which is also why the wrong answer is only expensive in
/// one direction, and why the veto below exists.</para>
///
/// <para><b>What it deliberately does not do:</b> surplus ("you hold 3, one reward wants
/// 1") is out. It needs an allocation across classes that <see cref="SkyLootAutoCheck"/>'s
/// rule 3 already declines to guess, and a surplus claim on a shared rune is wrong by one
/// class exactly when it matters (#106).</para>
/// </summary>
public static class SkyLeftovers
{
    /// <summary>Join the dump against the Sky checklist.</summary>
    /// <param name="dump">The character's inventory dump. Null (no dump ever read) gives an
    /// empty report — the bands are absent rather than empty, and the tab's ⧉
    /// `/outputfile inventory` is already the way in.</param>
    /// <param name="myClasses">The classes this character holds — the ⚙ picks
    /// (<c>QuestLedgerStore.ClassesFor</c>) or the achievements dump's own list. <b>Empty is
    /// not a wildcard</b> (#193): with no lens, band B is never produced, because "only
    /// other classes want this" is not a claim we can make without knowing which classes are
    /// the player's. Band A is unaffected — it is true regardless of who is playing.</param>
    /// <param name="catalog">Used only for the veto: a NON-Sky quest that still takes the
    /// item keeps it out of band A. Null skips the veto, which is why every shipping caller
    /// passes one.</param>
    public static SkyLeftoverReport Compute(
        InventoryFile.Snapshot? dump,
        IReadOnlyList<SkyQuestChecklistItem>? checklist,
        IReadOnlyCollection<string>? completedRewardKeys,
        IReadOnlyList<string>? myClasses,
        QuestCatalog? catalog)
    {
        if (dump is null || checklist is null || checklist.Count == 0) return SkyLeftoverReport.Empty;

        var completed = new HashSet<string>(completedRewardKeys ?? [], StringComparer.OrdinalIgnoreCase);
        var mine = new HashSet<string>(myClasses ?? [], StringComparer.OrdinalIgnoreCase);
        var where = WhereByItem(dump);

        var noLonger = new List<SkyLeftoverRow>();
        var otherClasses = new List<SkyLeftoverRow>();
        var vetoed = new List<SkyLeftoverHeld>();

        foreach (var group in checklist
                     .Where(i => i.QuestItem.Length > 0)
                     .GroupBy(i => QuestCatalog.BaseItemName(i.QuestItem), StringComparer.OrdinalIgnoreCase))
        {
            var held = dump.CountOf(group.Key);
            if (held <= 0) continue;

            var rows = group.ToList();
            var open = rows
                .Where(i => !completed.Contains(QuestChecklistLayout.RewardKey(i.ClassName, i.Reward)))
                .ToList();

            // The checklist's spelling, not the dump's: the dump writes whatever the game
            // wrote, and the wiki name is what every other surface prints.
            var item = QuestCatalog.BaseItemName(rows[0].QuestItem);
            var usedBy = rows
                .Where(i => completed.Contains(QuestChecklistLayout.RewardKey(i.ClassName, i.Reward)))
                .Select(i => i.ClassName + " · " + i.Reward)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (open.Count == 0)
            {
                var quests = OtherQuestsWanting(catalog, item);
                if (quests.Count > 0) vetoed.Add(new SkyLeftoverHeld(item, held, quests));
                else noLonger.Add(new SkyLeftoverRow(
                    item, held, where.GetValueOrDefault(item, ""),
                    SkyLeftoverBand.NoLongerNeeded, usedBy, []));
                continue;
            }

            if (mine.Count == 0) continue;
            if (open.Any(i => mine.Contains(i.ClassName))) continue;

            otherClasses.Add(new SkyLeftoverRow(
                item, held, where.GetValueOrDefault(item, ""),
                SkyLeftoverBand.OtherClassesWant, usedBy,
                [.. open.Select(i => i.ClassName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)]));
        }

        return new SkyLeftoverReport(Sorted(noLonger), Sorted(otherClasses),
            [.. vetoed.OrderBy(h => h.Item, StringComparer.OrdinalIgnoreCase)]);

        static IReadOnlyList<SkyLeftoverRow> Sorted(List<SkyLeftoverRow> rows) =>
            [.. rows.OrderBy(r => r.Item, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Quests other than the Sky tests that take this item as a turn-in, by name.
    /// The split Sky Test quests (<see cref="SkyTestSplit"/>) ARE the checklist and would
    /// veto every row, so they are removed — the completion of those is what
    /// <paramref name="item"/> was just judged against.</summary>
    private static List<string> OtherQuestsWanting(QuestCatalog? catalog, string item) =>
        catalog is null ? []
        : [.. catalog.QuestsWanting(item)
            .Where(q => SkyTestSplit.RewardKeyFor(q.Name).Length == 0)
            .Select(q => q.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Base item name → "bags" / "bank" / "worn", joined when it is in more than
    /// one. Built from <see cref="InventoryFile.Snapshot.Entries"/> because
    /// <see cref="InventoryFile.Snapshot.Counts"/> has no location at all.</summary>
    private static Dictionary<string, string> WhereByItem(InventoryFile.Snapshot dump)
    {
        var places = new Dictionary<string, SortedSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in dump.Entries)
        {
            var name = QuestCatalog.BaseItemName(e.Name);
            if (!places.TryGetValue(name, out var set)) places[name] = set = [];
            set.Add(e.InBank ? 1 : e.InContainer || e.ContainerSlot
                .StartsWith("General", StringComparison.OrdinalIgnoreCase) ? 0 : 2);
        }
        return places.ToDictionary(p => p.Key,
            p => string.Join(" and ", p.Value.Select(i => i switch
            {
                0 => "bags",
                1 => "bank",
                _ => "worn",
            })), StringComparer.OrdinalIgnoreCase);
    }
}
