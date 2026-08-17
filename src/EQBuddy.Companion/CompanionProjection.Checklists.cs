using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Companion;

// Epics, Sky and Gear: the three checklists, projected into one shape so the page has
// one renderer and one tap path. They live in settings rather than the tick snapshot,
// which is why they are rebuilt from AppSettings each pass — cheap (a few hundred
// small records) and only while a device is connected and the surface is offered.
public static partial class CompanionProjection
{
    /// <summary>A row the auto-tick placed without being able to pick a class shows
    /// the desktop's marker, so "why is that ticked" reads the same on both screens.
    /// Taken from Core rather than spelled again here: for a while this was the ONLY
    /// surface drawing it, and the desktops silently showed a bare tick (#184).</summary>
    private const string UnassignedMark = QuestChecklistLayout.UnassignedMark;

    private static CompanionChecklistSection BuildEpics(AppSettings? settings)
    {
        var items = settings?.EpicQuestChecklist ?? [];
        // The desktop's class lens and its classic-era lens, both honored: what the
        // phone lists is what the PC's Epic card lists.
        var scoped = items
            .Where(i => settings is not { EpicQuestClassicOnly: true } || i.AvailableInClassic)
            .Where(i => settings is not { EpicQuestClass.Length: > 0 }
                || string.Equals(i.ClassName, settings.EpicQuestClass, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var groups = scoped
            .GroupBy(i => (i.ClassName, Section: i.Section.Length > 0 ? i.Section : "Checklist"))
            .OrderBy(g => g.Key.ClassName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CompanionChecklistGroup(
                Heading: settings is { EpicQuestClass.Length: > 0 }
                    ? g.Key.Section
                    : $"{g.Key.ClassName} — {g.Key.Section}",
                Note: null,
                Class: g.Key.ClassName,
                Rows:
                [
                    .. g.OrderBy(i => i.Order).ThenBy(i => i.QuestItem, StringComparer.OrdinalIgnoreCase)
                        .Select(i => new CompanionChecklistRow(
                            i.Id,
                            i.QuestItem.Length > 0 ? i.QuestItem : i.Reward,
                            Detail(i),
                            i.Acquired)),
                ]))
            .ToList();

        return new CompanionChecklistSection(scoped.Count(i => i.Acquired), scoped.Count, groups);

        static string? Detail(EpicQuestChecklistItem i)
        {
            var text = i.Source.Length > 0 ? i.Source : i.QuestName;
            if (i.AcquiredUnassigned) text += UnassignedMark;
            return text.Length > 0 ? text : null;
        }
    }

    private static CompanionChecklistSection BuildSky(AppSettings? settings)
    {
        var items = settings?.SkyQuestChecklist ?? [];
        var completed = new HashSet<string>(settings?.SkyQuestCompleted ?? [], StringComparer.OrdinalIgnoreCase);
        var picked = settings?.SkyQuestClass ?? "";

        // The ★ Ready cross-class view first: every reward whose pieces are all in
        // hand and which isn't marked done — the whole point of the desktop's tab.
        var ready = items
            .GroupBy(i => (i.ClassName, i.Reward))
            .Where(g => g.All(i => i.Acquired) && !completed.Contains(RewardKey(g.Key.ClassName, g.Key.Reward)))
            .OrderBy(g => g.Key.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key.Reward, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = new List<CompanionChecklistGroup>();
        if (ready.Count > 0)
            groups.Add(new CompanionChecklistGroup(
                $"★ Ready {ready.Count}",
                "Every piece in hand — go hand them in.",
                [.. ready.Select(g => new CompanionChecklistRow(
                    // A ready ROW is a summary, not a togglable item: its id is the
                    // reward key, which no tick action accepts.
                    RewardKey(g.Key.ClassName, g.Key.Reward),
                    $"{g.Key.ClassName} — {g.Key.Reward}",
                    g.First().Npc,
                    true))]));

        var scoped = items
            .Where(i => picked.Length == 0
                || string.Equals(i.ClassName, picked, StringComparison.OrdinalIgnoreCase))
            .ToList();

        groups.AddRange(scoped
            .GroupBy(i => (i.ClassName, i.Reward))
            .OrderBy(g => g.Key.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key.Reward, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CompanionChecklistGroup(
                picked.Length > 0 ? g.Key.Reward : $"{g.Key.ClassName} — {g.Key.Reward}",
                completed.Contains(RewardKey(g.Key.ClassName, g.Key.Reward)) ? "done"
                    : g.All(i => i.Acquired) ? "ready"
                    : g.Any(i => i.Acquired) ? "in progress" : null,
                [.. g.Select(i => new CompanionChecklistRow(
                    i.Id,
                    i.QuestItem.Length > 0 ? i.QuestItem : i.Reward,
                    i.AcquiredUnassigned ? i.Npc + UnassignedMark : i.Npc,
                    i.Acquired))],
                Class: g.Key.ClassName)));

        return new CompanionChecklistSection(scoped.Count(i => i.Acquired), scoped.Count, groups);
    }

    /// <summary>The desktop's reward key (class + reward), so "done" means the same
    /// thing on both screens.</summary>
    internal static string RewardKey(string className, string reward) => className + "|" + reward;

    private static CompanionChecklistSection BuildGear(AppSettings? settings, Func<string, int?>? hops)
    {
        var items = settings?.GearChecklist ?? [];
        var groups = settings is { GearGroupByZone: true }
            // GearFarmRollup is already framework-neutral and already answers "where do
            // I farm this" — including the hop counts, when the zone graph can.
            ? GearFarmRollup.Build(items, ItemCatalog.Default.Find, hops)
                .Select(z => new CompanionChecklistGroup(
                    GearFarmRollup.Heading(z), null,
                    [.. z.Items.Select(Row)]))
                .ToList()
            : GearChecklistPresentation.BuildGroups(items)
                .Select(g => new CompanionChecklistGroup(g.Heading, null, [.. g.Items.Select(Row)]))
                .ToList();

        return new CompanionChecklistSection(items.Count(i => i.Acquired), items.Count, groups);

        static CompanionChecklistRow Row(GearChecklistItem i)
        {
            var text = GearChecklistPresentation.TextFor(i);
            return new CompanionChecklistRow(
                GearRowId(i),
                text.Name + text.EffectSuffix,
                i.Source.Length > 0 ? i.Source : null,
                i.Acquired);
        }
    }

    /// <summary>Gear rows carry no id of their own, so slot|item is the identity a tap
    /// comes back with. Stable enough: the same slot can't hold the same item twice.</summary>
    internal static string GearRowId(GearChecklistItem item) => item.Slot + "|" + item.Item;
}
