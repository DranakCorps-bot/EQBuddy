using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>Framework-neutral display rules for the imported Gear-card checklist.
/// WPF and Avalonia create their own controls from these groups; neither UI gets to
/// decide which rows are gear versus socketed exaltations or how their text reads.</summary>
public static class GearChecklistPresentation
{
    public sealed record Group(string Heading, IReadOnlyList<GearChecklistItem> Items);
    public sealed record ItemText(string Name, string EffectSuffix);

    /// <summary>The empty state, rewritten TWICE on 2026-08-20 and the second time is the
    /// one that matters.
    ///
    /// The first version named both imports in two sentences sitting side by side, and
    /// David read them as one sequence — copied the command, made the file, and then
    /// followed the OTHER sentence into Options looking for somewhere to feed it. They are
    /// unrelated: the shopping list is a website export, the dump is a game command, and
    /// only one of them is something you "import". Two sentences that must not be read as
    /// steps have to be separated by what they are FOR, not by a line break.
    ///
    /// It also said "shopping list" as though everyone knew what that was — <i>"we have no
    /// idea what that is as there is nothing to indicate it's something we can do in gear
    /// and loot"</i>. So the term is defined where it is used, and the place it comes from
    /// is named as a website rather than as a menu path.</summary>
    public const string EmptyRoute =
        "Nothing on your list yet. A gear list is a wishlist you build on the "
        + "eqlegends.tools website — pick the upgrades you want, export the page, and "
        + "EQBuddy tracks them here. Options → Cards & windows → Import gear list… "
        + "takes the file, and there is an Open EQ Legends Tools button beside it.";

    /// <summary>Said in BOTH states, never only the empty one (the rule
    /// <c>RaidsCardView</c> works): the player likeliest to need the dump is the one whose
    /// import has gone stale, and that player's list is populated.
    ///
    /// It no longer tells anyone to import anything. The dump imports ITSELF — the game
    /// announces it in the log EQBuddy already tails, and <c>OutputfileAutoImport</c> reads
    /// it (David, 2026-08-20: <i>"we should automatically read the other files we
    /// generate"</i>). The command is still offered because the player still has to type it
    /// in game; what changed is everything after that.</summary>
    public static readonly string AutoTickNote =
        $"Already own some of them? Type {GameCommands.OutputfileInventory} in game and "
        + "EQBuddy reads the file the moment it appears, ticking off whatever your bags "
        + "and bank already hold. No importing, no hunting for the file.";

    /// <summary>The ⧉ button's tooltip. It now promises the whole outcome, because the
    /// whole outcome is what happens — the old one stopped at "the game writes the dump",
    /// which was the honest description of a feature that then did nothing with it.</summary>
    public static readonly string AutoTickTip =
        $"Copies {GameCommands.OutputfileInventory} — paste it into the game's chat, and "
        + "EQBuddy picks the file up by itself within a second or two and ticks anything "
        + "on this list you already own. You never have to find the file.";

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
