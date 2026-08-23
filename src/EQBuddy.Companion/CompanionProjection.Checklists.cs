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
                ],
                Title: g.Key.Section))
            .ToList();

        return new CompanionChecklistSection(scoped.Count(i => i.Acquired), scoped.Count, groups);

        static string? Detail(EpicQuestChecklistItem i)
        {
            var text = i.Source.Length > 0 ? i.Source : i.QuestName;
            if (i.AcquiredUnassigned) text += UnassignedMark;
            return text.Length > 0 ? text : null;
        }
    }

    /// <summary>
    /// Plane of Sky for the phone — grouping, ordering, state and the notes all from
    /// <see cref="QuestChecklistLayout"/>, the same call the two desktop windows make.
    ///
    /// **This used to hand-roll all of it**, and that is precisely how the surfaces drift.
    /// The layout module was created for #184 because the three screens had already
    /// disagreed once; the two desktops were converted and this was not, so it stayed a
    /// fourth copy of the same four decisions — which reward groups with which, when a
    /// reward reads "ready", what the note says, and how the reward key is spelled.
    ///
    /// #210 (liminalwarmth) is what made the cost visible from the other direction: this
    /// projection had the cross-class ready list when the DESKTOP had lost it, so the
    /// phone answered a question the big window could not. David, 2026-08-18: mobile and
    /// desktop are both first-class and must work the same way, in both directions. That
    /// is only true structurally — a shared module they all call — and not as a list of
    /// features someone keeps level by hand.
    /// </summary>
    private static CompanionChecklistSection BuildSky(AppSettings? settings)
    {
        var items = settings?.SkyQuestChecklist ?? [];
        // The phone reads the player's own grouping choice too — a surface that shows the
        // same checklist a different way is the drift SurfaceParityTests exists to stop.
        var all = QuestChecklistLayout.Sky(items, settings?.SkyQuestCompleted,
            settings?.SkyStepsUnderEveryIsland ?? false);

        var groups = new List<CompanionChecklistGroup>();

        // The cross-class ready view first — every reward whose pieces are all in hand,
        // whoever it belongs to. Deliberately BEFORE the class scope below: "what can I
        // turn in right now" is the one question here with an action attached, and
        // narrowing it to one class is what makes it not worth asking.
        var ready = QuestChecklistLayout.ReadyToTurnIn(all);
        if (ready.Count > 0)
            groups.Add(new CompanionChecklistGroup(
                $"★ Ready {ready.Count}",
                "Every piece in hand — go hand them in.",
                [.. ready.Select(g => new CompanionChecklistRow(
                    // A ready ROW is a summary, not a togglable item: its id is the
                    // reward key, which no tick action accepts.
                    g.CompletionKey ?? QuestChecklistLayout.RewardKey(g.ClassName, g.Title),
                    $"{g.ClassName} — {g.Title}",
                    g.TurnInNpc,
                    // NOT done — ready is the opposite of done, and the phone strikes a
                    // done row through. bjstrange's screenshot on #212 shows all three of
                    // his ready rewards ticked and crossed out, which reads as "handed
                    // in" on the one band whose entire job is "go hand these in".
                    Done: false))],
                Tickable: false));

        // EVERY class goes to the phone, and the page's own class chips narrow it there.
        //
        // This used to scope by AppSettings.SkyQuestClass, and NOTHING IN THE CODEBASE
        // WRITES THAT SETTING — SkyLootAutoCheck already says so in as many words, from
        // fixing #193: the widget's Sky card was the only writer and the 2026-08-16
        // consolidation deleted it. So the value is whatever was last persisted before
        // that day, forever. For bjstrange (#212) it did not match any class he plays, so
        // his entire Sky list was empty below the Ready band and no control on the phone
        // could change it — "only appears to show ready items with no way to change that".
        //
        // Third instance of one signature: the DATA survived a fold and the WRITE path
        // did not (SkyQuestCompleted, EpicQuestCompleted, and now this). A filter whose
        // value no player can change is not a filter.
        var scoped = all.ToList();

        groups.AddRange(scoped.Select(g => new CompanionChecklistGroup(
            g.Heading,
            g.Note,
            [.. g.Rows.Select(r => new CompanionChecklistRow(
                r.Id,
                r.Title,
                r.Unassigned ? r.Detail + UnassignedMark : r.Detail,
                r.Acquired))],
            Class: g.ClassName,
            Title: g.Title)));

        return new CompanionChecklistSection(
            scoped.Sum(g => g.Done), scoped.Sum(g => g.Total), groups);
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

        // Sent in BOTH states, empty and populated — the phone's half of the rule the
        // desktop cards keep (the player likeliest to need the dump is the one whose
        // import has gone stale). The page decides where to draw it; the command itself
        // is never spelled on that side.
        return new CompanionChecklistSection(
            items.Count(i => i.Acquired), items.Count, groups,
            Prompt(CommandPrompts.GearInventory),
            GearChecklistPresentation.EmptyRoute);

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

    /// <summary>UI.Shared's prompt onto the wire shape. A copy rather than a reference
    /// because the envelope is the PROTOCOL and must not move when a presentation helper
    /// does — the same reason CompanionSections lives in this project at all.</summary>
    internal static CompanionCommandPrompt Prompt(CommandPrompt p) =>
        new(p.Lead, p.Command, p.Note);
}
