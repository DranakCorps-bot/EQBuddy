namespace EQBuddy.Core;

/// <summary>Which band a leftover row belongs in. Two bands, never mixed under one
/// heading (Bevel, Helm-signed 2026-09-02): they are claims of different strength, and
/// a player freeing bag space acts on them differently.</summary>
public enum SkyLeftoverBand
{
    /// <summary>Every Sky reward in the game that takes this item is turned in, and no
    /// other catalog quest wants it. The reporter's sentence, and the only strong claim.</summary>
    NoLongerNeeded,

    /// <summary>Still wanted — but by no class you have. Weaker on purpose: a Legends
    /// character can unlock the class later, so this is "not yours", never "junk".</summary>
    OtherClassesWant,
}

/// <summary>One held item that Sky is finished with, or finished with for you.</summary>
/// <param name="Item">Base item name, as the checklist and the wiki spell it.</param>
/// <param name="Held">How many the dump says you have (stacks folded).</param>
/// <param name="Where">"bags", "bank", or "bags and bank" — the ask is bag SPACE, and an
/// item sitting in the bank is not the problem tvongaza has.</param>
/// <param name="TurnedInRewards">The rewards that used it and are done, "Class · Reward".</param>
/// <param name="OpenClasses">Band B only: the classes that still want it.</param>
public sealed record SkyLeftoverRow(
    string Item,
    int Held,
    string Where,
    SkyLeftoverBand Band,
    IReadOnlyList<string> TurnedInRewards,
    IReadOnlyList<string> OpenClasses)
{
    /// <summary>The row exactly as every surface draws it — `{Item} ×{held} · {where}`
    /// (Bevel, Helm-signed 2026-09-02). Said once here because the WPF band, the Avalonia
    /// band and the phone group are three renderers of ONE decision, and a format string
    /// hand-copied into three files is precisely what drifts (#184's lesson, and the
    /// reason <see cref="QuestChecklistLayout"/> exists at all).</summary>
    public string Line => Where.Length > 0 ? $"{Item} ×{Held} · {Where}" : $"{Item} ×{Held}";

    /// <summary>The hover: the EVIDENCE for the row's claim, which is the whole reason a
    /// player would act on it. Band A names the turned-in rewards that used the item;
    /// band B names the classes that still want it and says plainly that they may become
    /// yours — the difference between "not yours" and "junk", which is the distinction
    /// the two bands exist to keep.</summary>
    public string Detail => Band == SkyLeftoverBand.NoLongerNeeded
        ? "Every Sky reward that takes it is turned in"
            + (TurnedInRewards.Count > 0 ? ": " + string.Join(", ", TurnedInRewards) : "")
            + "."
        : "Still wanted by " + string.Join(", ", OpenClasses)
            + " — no class this character has. A Legends character can unlock one later, "
            + "so this is “not yours” rather than “junk”."
            + (TurnedInRewards.Count > 0
                ? "\n\nAlready turned in: " + string.Join(", ", TurnedInRewards) + "."
                : "");
}

/// <summary>Rows plus the one thing a list of rows cannot say: what was deliberately
/// left OUT because another quest still wants it.</summary>
/// <param name="HeldBackByOtherQuests">Item → the non-Sky quest that vetoed it. Named
/// rather than counted, because "1 more is still wanted by Blackburrow Brewers" is the
/// sentence that stops someone selling it.</param>
public sealed record SkyLeftoversResult(
    IReadOnlyList<SkyLeftoverRow> Rows,
    IReadOnlyList<(string Item, string Quest)> HeldBackByOtherQuests)
{
    public static readonly SkyLeftoversResult Empty = new([], []);

    public int NoLongerNeeded => Rows.Count(r => r.Band == SkyLeftoverBand.NoLongerNeeded);
    public int OtherClassesWant => Rows.Count(r => r.Band == SkyLeftoverBand.OtherClassesWant);

    /// <summary>Nothing to draw at all. The band's own absence rule, borrowed from the
    /// Ready band it sits under: *a permanently-present band reading "nothing" is how a
    /// player learns to stop looking at it.*</summary>
    public bool IsEmpty => Rows.Count == 0;

    public IReadOnlyList<SkyLeftoverRow> RowsIn(SkyLeftoverBand band) =>
        [.. Rows.Where(r => r.Band == band)];

    /// <summary>The two headings, verbatim and in ONE place, because the honesty of this
    /// feature is carried by its words: band B must never appear under band A's heading
    /// (Bevel's replace, Helm-signed 2026-09-02). A count in the heading and a different
    /// number of rows under it is trap 4 in miniature, so both come from the same list.</summary>
    public string NoLongerNeededHeading => $"No longer needed — {NoLongerNeeded}";
    public string OtherClassesWantHeading => $"Other classes still want — {OtherClassesWant}";

    /// <summary>The hover clause for the items a NON-Sky quest kept out of band A, or ""
    /// when there are none. They are deliberately not rows — the player is told they
    /// exist and which quest wants them, so an item missing from the band is EXPLAINED
    /// rather than simply absent. "1 more is still wanted by Blackburrow Brewers" is the
    /// sentence that stops someone selling it.</summary>
    public string HeldBackNote => HeldBackByOtherQuests.Count == 0 ? ""
        : $"{HeldBackByOtherQuests.Count} more "
            + (HeldBackByOtherQuests.Count == 1 ? "is" : "are")
            + " still wanted by another quest: "
            + string.Join(", ", HeldBackByOtherQuests.Select(h => $"{h.Item} ({h.Quest})"))
            + ".";
}

/// <summary>
/// **The join #243 asked for and nothing shipped**: the player's own inventory dump against
/// the player's own Sky turn-ins. tvongaza, 2026-08-26: *"when you do an inventory dump, it
/// could cross check which sky quests you've completed and which sky quest items you no
/// longer need as you've completed all the quests which use them. Would help with limited
/// inventory space."*
///
/// Every piece already existed — the dump (<see cref="InventoryFile.Snapshot"/>), what Sky
/// wants (<see cref="AppSettings.SkyQuestChecklist"/>), and what is done
/// (<see cref="AppSettings.SkyQuestCompleted"/>) — and no code put them together.
///
/// This is <see cref="QuestChecklistLayout.SearchByItem"/>'s complement: that one answers
/// "who wants this item", this one answers "does anyone still".
///
/// **It is a list. It ticks nothing, sells nothing and destroys nothing** — the whole value
/// is a claim the player then acts on, so the claims are graded rather than merged:
///
/// - **Band A** needs every Sky reward that takes the item to be turned in AND no other
///   catalog quest to want it. A wrong Band A row costs someone a turn-in they cannot get
///   back, which is why the non-Sky veto exists even though the reporter asked about Sky
///   alone.
/// - **Band B** is never produced without a class lens. No lens is not a wildcard — the same
///   rule <see cref="SkyLootAutoCheck"/> follows (#193); with no lens the item is simply
///   still wanted, and says nothing.
///
/// Framework-free, and deliberately pure: the desktop bands, the phone group and the import
/// report all call this one function, so the three surfaces cannot drift into three answers
/// (trap 43's shape — the parity test is the consumer check).
/// </summary>
public static class SkyLeftovers
{
    /// <param name="held">The dump. Null or empty means no dump has been read: NOTHING is
    /// leftover, because "you hold none of it" and "you were never told" look identical in
    /// a count and only one of them is a fact.</param>
    /// <param name="checklist">Every (class, reward, item) Sky row — profile-global.</param>
    /// <param name="completedRewardKeys">"Class|Reward" for each turned-in reward.</param>
    /// <param name="myClasses">The classes this character has. Empty means no lens, which
    /// suppresses Band B entirely.</param>
    /// <param name="catalog">Used only for the veto: a non-Sky quest wanting the item.
    /// Null skips the veto, which is the honest behaviour when no catalog is loaded — the
    /// list gets shorter guarantees, never longer ones, so the caller is told via
    /// <see cref="SkyLeftoversResult.HeldBackByOtherQuests"/> staying empty.</param>
    public static SkyLeftoversResult Compute(
        InventoryFile.Snapshot? held,
        IEnumerable<SkyQuestChecklistItem>? checklist,
        IEnumerable<string>? completedRewardKeys,
        IEnumerable<string>? myClasses,
        QuestCatalog? catalog)
    {
        if (held is null || checklist is null) return SkyLeftoversResult.Empty;

        var done = new HashSet<string>(completedRewardKeys ?? [], StringComparer.OrdinalIgnoreCase);
        var lens = new HashSet<string>(
            (myClasses ?? []).Where(c => !string.IsNullOrWhiteSpace(c)),
            StringComparer.OrdinalIgnoreCase);

        // Group the Sky demand by BASE item name, both sides folded the same way, or a
        // "Wind Rune Izah*" in the dump and a "Wind Rune Izah" in the checklist are two
        // different items and the feature silently lists nothing.
        var wantedBy = new Dictionary<string, List<SkyQuestChecklistItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in checklist)
        {
            var name = QuestCatalog.BaseItemName(row.QuestItem ?? "");
            if (name.Length == 0) continue;
            if (!wantedBy.TryGetValue(name, out var list)) wantedBy[name] = list = [];
            list.Add(row);
        }

        var rows = new List<SkyLeftoverRow>();
        var vetoed = new List<(string Item, string Quest)>();

        foreach (var (item, wanters) in wantedBy)
        {
            var count = held.CountOf(item);
            if (count <= 0) continue;   // you do not have it; it is not taking up space

            var open = wanters
                .Where(w => !done.Contains(QuestChecklistLayout.RewardKey(w.ClassName, w.Reward)))
                .ToList();

            SkyLeftoverBand band;
            if (open.Count == 0)
            {
                // The veto. Asking the catalog costs one lookup and is the difference
                // between a list and a mistake: "safe to free" about an item another
                // quest takes as a turn-in is the one way this feature loses someone
                // something. Split Sky Test quests are the SAME rewards under another
                // name (SkyTestSplit), so they must not veto themselves.
                var otherQuest = catalog?.QuestsWanting(item)
                    .FirstOrDefault(q => SkyTestSplit.RewardKeyFor(q.Name).Length == 0);
                if (otherQuest is not null)
                {
                    vetoed.Add((item, otherQuest.Name));
                    continue;
                }
                band = SkyLeftoverBand.NoLongerNeeded;
            }
            else if (lens.Count > 0 && !open.Any(w => lens.Contains(w.ClassName)))
            {
                band = SkyLeftoverBand.OtherClassesWant;
            }
            else
            {
                continue;   // still wanted, by you or by nobody we can speak for
            }

            rows.Add(new SkyLeftoverRow(
                item, count, WhereHeld(held, item), band,
                [.. wanters.Except(open)
                    .Select(w => w.ClassName + " · " + w.Reward)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)],
                [.. open.Select(w => w.ClassName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)]));
        }

        return new SkyLeftoversResult(
            [.. rows.OrderBy(r => r.Band)
                .ThenBy(r => r.Item, StringComparer.OrdinalIgnoreCase)],
            [.. vetoed.OrderBy(v => v.Item, StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>bags / bank / both, from the dump's own <c>Location</c> rows. The counts
    /// live in <see cref="InventoryFile.Snapshot.Counts"/> and the locations only in
    /// <c>Entries</c>, so this reads Entries and folds base names to match.</summary>
    internal static string WhereHeld(InventoryFile.Snapshot held, string item)
    {
        var bags = false;
        var bank = false;
        foreach (var e in held.Entries)
        {
            if (!QuestCatalog.BaseItemName(e.Name.TrimEnd('*'))
                    .Equals(item, StringComparison.OrdinalIgnoreCase)) continue;
            // InventoryFile.Entry.InBank owns "is this in the bank", shared bank included
            // — the same rule GearLocker now asks rather than spelling for itself.
            if (e.InBank) bank = true;
            else bags = true;
        }
        return (bags, bank) switch
        {
            (true, true) => "bags and bank",
            (false, true) => "bank",
            // Default to bags rather than "" when the dump carried counts but no matching
            // Entry row: a location we cannot prove is far likelier to be a bag than to be
            // nothing, and an empty string reads on screen as a missing word.
            _ => "bags",
        };
    }
}
