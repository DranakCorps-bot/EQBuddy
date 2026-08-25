namespace EQBuddy.Core;

/// <summary>
/// The three quest surfaces, in the order every UI shows them (David, 2026-08-15:
/// "tabs for general quests, Epic quests, Plane of Sky quests"). Ordering and labels
/// live here rather than in each window because "it should look and work the same on
/// mobile" is only true if there is one definition of what the tabs ARE — the Avalonia
/// chip stacks already proved what happens when a UI keeps its own copy (#122, #152).
/// </summary>
public enum QuestTab
{
    General,
    Epic,
    Sky,
    /// <summary>Race and class unlocks, read out of the achievements and faction dumps
    /// (Hateborne, 2026-08-25). Deity is deliberately absent: sixteen of its seventeen entries
    /// in a real dump read "Future Placeholder for &lt;God&gt; Requirements".</summary>
    Unlocks,
}

/// <summary>A tab as a UI should draw it: what to call it, and the progress badge.
/// <see cref="Done"/>/<see cref="Total"/> are null for General, which is a catalog to
/// search rather than a checklist to finish — a "0 / 900" badge there would read as
/// failure rather than as a library.</summary>
public sealed record QuestTabHeader(QuestTab Tab, string Label, string Key, int? Done, int? Total)
{
    /// <summary>"12 / 34", or null when the tab has no completion to report.</summary>
    public string? Badge => Done is { } d && Total is { } t && t > 0 ? $"{d} / {t}" : null;
}

/// <summary>
/// Builds the tab strip shared by the desktop quest window and EQBuddy Mobile. Pure:
/// takes counts, returns headers. The counts themselves come from the existing
/// checklists, so this cannot drift from what those tabs actually contain.
/// </summary>
public static class QuestSurface
{
    /// <summary>The canonical label for each tab. "Plane of Sky" is spelled the way the
    /// wiki and the game spell it, not shortened to "Sky" — the checklist inside is
    /// already called Sky, and a player searching for the zone should recognise it.</summary>
    public static string LabelFor(QuestTab tab) => tab switch
    {
        QuestTab.General => "Quests",
        QuestTab.Epic => "Epic 1.0",
        QuestTab.Sky => "Plane of Sky",
        QuestTab.Unlocks => "Unlocks",
        _ => tab.ToString(),
    };

    /// <summary>The wire/DOM key for a tab — lowercase and stable, so the mobile page's
    /// saved tab choice survives a rename of the human-facing label.</summary>
    /// <summary>Inline (Bevel, Helm-signed 2026-08-22). The two checklists are rooms and
    /// go inline CAPPED — Epic to one class, Sky to the current class. General is a search
    /// box over 1,200 quests plus a detail pane, which is Bevel's host rule verbatim: it
    /// glances as <c>{n} quests ready to turn in</c> and the tracker is one ⧉ away.</summary>
    public static InlineMode InlineModeFor(QuestTab tab) => tab switch
    {
        QuestTab.General => InlineMode.Glance,
        _ => InlineMode.Full,
    };

    /// <summary>The tab an expanded Quests card opens on. General, deliberately, though it
    /// is a Glance: "3 quests ready to turn in" is the thing a player expands the card to
    /// learn, and it is one line rather than a checklist they have to read.</summary>
    public const QuestTab DefaultInlineTab = QuestTab.General;

    public static string KeyFor(QuestTab tab) => tab switch
    {
        QuestTab.General => "general",
        QuestTab.Epic => "epic",
        QuestTab.Sky => "sky",
        QuestTab.Unlocks => "unlocks",
        _ => tab.ToString().ToLowerInvariant(),
    };

    public static QuestTab? TabForKey(string key) => key?.Trim().ToLowerInvariant() switch
    {
        "general" => QuestTab.General,
        "epic" => QuestTab.Epic,
        "sky" => QuestTab.Sky,
        "unlocks" => QuestTab.Unlocks,
        _ => null,
    };

    /// <summary>The full strip, always every tab in a fixed order. Empty checklists still
    /// get their tab: a Sky tab that vanishes when nothing is ticked is a silent no-op,
    /// and a player who has never opened Sky is exactly who needs to find it.</summary>
    public static IReadOnlyList<QuestTabHeader> Tabs(
        (int Done, int Total)? epic = null, (int Done, int Total)? sky = null,
        (int Done, int Total)? unlocks = null)
    {
        return
        [
            Header(QuestTab.General, null),
            Header(QuestTab.Epic, epic),
            Header(QuestTab.Sky, sky),
            Header(QuestTab.Unlocks, unlocks),
        ];

        static QuestTabHeader Header(QuestTab tab, (int Done, int Total)? counts) =>
            new(tab, LabelFor(tab), KeyFor(tab), counts?.Done, counts?.Total);
    }

    /// <summary>
    /// A checklist tab's badge numbers, said once.
    ///
    /// Both desktop windows and the phone each hand-rolled this identical
    /// `items.Count(i => i.Acquired) / items.Count`, three copies of one rule — and a
    /// fourth copy is exactly how #184 happened. Null for an empty checklist, because
    /// "0 / 0" is not a badge, it is a tab that looks broken.
    /// </summary>
    public static (int Done, int Total)? CountOf<T>(
        IReadOnlyCollection<T> items, Func<T, bool> done) =>
        items.Count == 0 ? null : (items.Count(done), items.Count);

    /// <summary>
    /// The Unlocks badge: races and classes together, one number.
    ///
    /// An unlock with nothing to do — Half Elf, whose only rows say it completes when
    /// Human or Wood Elf does — is not in the denominator. Counting it would put a target
    /// on the strip that no amount of play can move, and the tab would read as permanently
    /// short of finished.
    /// </summary>
    public static (int Done, int Total)? UnlockCounts(
        IReadOnlyCollection<UnlockProgress> races, IReadOnlyCollection<UnlockProgress> classes)
    {
        var all = races.Concat(classes).Where(u => u.Score is not null).ToList();
        return all.Count == 0 ? null : (all.Count(u => u.Complete), all.Count);
    }
}
