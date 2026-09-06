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
    /// is named as a website rather than as a menu path.
    ///
    /// **Rewritten again on 2026-09-05 (SR-2), because the route it named stopped
    /// existing.** The three buttons moved OUT of Options and onto this surface, directly
    /// under this line — so the sentence that used to send a player two windows away now
    /// points at what is beneath it. A route line that names a place a thing no longer is
    /// is worse than no route line: it is #219's mechanism in one string.</summary>
    public const string EmptyRoute =
        "Nothing on your list yet. A gear list is a wishlist you build on the "
        + "eqlegends.tools website — pick the upgrades you want, export the page, and "
        + "EQBuddy tracks them here. " + ImportButton + " below takes the file, and "
        + OpenToolsButton + " goes to the site.";

    // -------------------------------------------------------------------------------
    // The import block (SR-2). It lived in Options → Cards & windows until 2026-09-05,
    // which is two windows away from the only surface its result ever appears on. The
    // words live HERE rather than in the view for the same reason every other string on
    // this surface does: an import workflow is a domain action, not a setting, and the
    // Evolved shell's Gear room hosts the same view.
    // -------------------------------------------------------------------------------

    /// <summary>The file picker's button. Named in <see cref="EmptyRoute"/> too, off this
    /// same const — a route line that spells a button's label itself is one rename away
    /// from pointing at nothing.</summary>
    public const string ImportButton = "Import gear list…";

    /// <summary>The way to the website the export comes from. It is on the POPULATED
    /// surface as well as the empty one, by <c>RaidsCardView</c>'s rule: the player
    /// likeliest to want a fresh export is the one whose list has gone stale.</summary>
    public const string OpenToolsButton = "Open EQ Legends Tools";

    public const string ClearButton = "Clear";

    /// <summary>Where <see cref="OpenToolsButton"/> goes. One source for the address, so
    /// the button and anything that ever describes it cannot disagree.</summary>
    public const string ToolsUrl = "https://eqlegendstools.com/char-sheet/";

    public const string ImportDialogTitle = "Import EQ Legends Tools shopping list";

    public const string ImportDialogFilter =
        "HTML files (*.html;*.htm)|*.html;*.htm|All files (*.*)|*.*";

    public const string OpenToolsTip =
        "Opens the EQ Legends Tools character sheet in your browser — the page you build "
        + "the list on and export.";

    /// <summary>Says what a re-import does to ticks, because that is the question a
    /// second import raises and the answer is reassuring: they survive
    /// (<c>GearChecklistImporter.PreserveAcquired</c>).</summary>
    public const string ImportTip =
        "Takes the exported HTML from EQ Legends Tools and shows it here as a checklist. "
        + "Re-importing replaces the list and keeps every box you have already ticked.";

    public const string ClearTip =
        "Removes the imported list from EQBuddy. The list itself stays on the website.";

    /// <summary>The Clear confirmation, and it exists BECAUSE of the move (SR-2). In
    /// Options this button sat behind two clicks on a screen nobody keeps open; here it is
    /// beside a list a player reads every session, and the thing a mis-click destroys is
    /// not the export — that is still on the website — but every box ticked since.</summary>
    public static string ClearConfirm(string name, int count) =>
        $"Remove {(name.Length > 0 ? name : "the imported gear list")} from EQBuddy?"
        + Environment.NewLine + Environment.NewLine
        + $"That drops {count} row{(count == 1 ? "" : "s")} and every box ticked against "
        + (count == 1 ? "it" : "them")
        + ". The list itself is still on the website — importing the export again brings "
        + "the rows back, but not the ticks.";

    public const string ClearConfirmTitle = "Clear gear list";

    /// <summary>What the last import DID, said out loud. A file that was read and changed
    /// nothing looks exactly like a file that was never read, and only one of those is a
    /// fault — the same rule the auto-import report is built on (David, 2026-08-20).</summary>
    public static string Imported(string name, int count) =>
        $"Imported {(name.Length > 0 ? name : "the gear list")} — {count} "
        + $"row{(count == 1 ? "" : "s")}.";

    /// <summary>The one failure that is not an exception: a well-formed page with nothing
    /// on it. It names the likely cause, because "no items found" alone reads as a bug in
    /// EQBuddy rather than as the wrong file.</summary>
    public const string NoItemsFound =
        "No gear items found in that file. It has to be the exported character-sheet page "
        + "from EQ Legends Tools, saved as HTML.";

    public static string ImportFailed(string reason) => $"Import failed: {reason}";

    public static string CouldNotOpenTools(string reason) =>
        $"Could not open EQ Legends Tools: {reason}";

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
