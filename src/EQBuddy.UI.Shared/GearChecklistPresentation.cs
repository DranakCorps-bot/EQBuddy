using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>Framework-neutral display rules for the imported Gear-card checklist.
/// WPF and Avalonia create their own controls from these groups; neither UI gets to
/// decide which rows are gear versus socketed exaltations or how their text reads.</summary>
public static class GearChecklistPresentation
{
    public sealed record Group(string Heading, IReadOnlyList<GearChecklistItem> Items);
    public sealed record ItemText(string Name, string EffectSuffix);

    /// <summary>The empty state, and the reason this file now carries prose at all.
    /// David, 2026-08-20: <i>"right now it's telling me to import it but not telling me
    /// how or giving me the tool with which to do it."</i> The old line was
    /// <c>"Import an EQ Legends Tools shopping-list HTML in Options."</c> — a task with
    /// no route, naming neither where the export comes from nor where in Options the
    /// import lives. Both halves are named here, and it is ONE string so the two UIs
    /// and the phone cannot drift into three different sets of directions.</summary>
    public const string EmptyRoute =
        "No shopping list yet — export one from EQ Legends Tools, then "
        + "Options → Cards & windows → Import gear list…";

    /// <summary>Said in BOTH states, never only the empty one (the rule
    /// <c>RaidsCardView</c> works): the player likeliest to need the dump is the one
    /// whose import has gone stale, and that player's list is populated. The ticks are
    /// the whole reason the command matters here — the checklist auto-marks what the
    /// dump says you already own, and nothing on the surface used to say so.</summary>
    public static readonly string AutoTickNote =
        $"Type {GameCommands.OutputfileInventory} in game and EQBuddy ticks off what "
        + "your bags and bank already hold.";

    /// <summary>The ⧉ button's tooltip. Says what the click does AND what happens next,
    /// because a copied command with no next step is half an instruction.</summary>
    public static readonly string AutoTickTip =
        $"Copies {GameCommands.OutputfileInventory} — paste it into the game's chat and "
        + "the game writes the dump beside its own folders; EQBuddy reads it on its own "
        + "and ticks anything on this list you already own.";

    public static IReadOnlyList<Group> BuildGroups(IReadOnlyList<GearChecklistItem> items)
    {
        var groups = new List<Group>(2);
        AddGroup(groups, "Gear", items.Where(item => !item.IsExaltation));
        AddGroup(groups, "Exaltations", items.Where(item => item.IsExaltation));
        return groups;
    }

    public static string ListName(string checklistName, IReadOnlyCollection<GearChecklistItem> items)
    {
        var done = items.Count(item => item.Acquired);
        return checklistName.Length > 0
            ? $"{checklistName} - {done}/{items.Count}"
            : $"{done}/{items.Count} imported gear pieces";
    }

    public static ItemText TextFor(GearChecklistItem item) => new(
        item.Item,
        item.IsExaltation && item.ExaltationEffect.Length > 0
            ? $" ({item.ExaltationEffect})"
            : "");

    public static string ItemLabel(GearChecklistItem item)
    {
        var text = TextFor(item);
        return text.Name + text.EffectSuffix;
    }

    public static string Tooltip(GearChecklistItem item)
    {
        var text = $"{item.Slot}: {ItemLabel(item)}";
        if (item.Source.Length > 0) text += "\n" + item.Source;
        if (item.Url.Length > 0) text += "\n" + item.Url;
        return text;
    }

    private static void AddGroup(List<Group> groups, string heading, IEnumerable<GearChecklistItem> items)
    {
        var rows = items.ToList();
        if (rows.Count > 0) groups.Add(new Group(heading, rows));
    }
}
