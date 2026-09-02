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
    private static CompanionChecklistSection BuildSky(AppSettings? settings, CompanionQuestRequest req)
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

        // Then the two leftover bands, beside Ready and for the same reason: both are
        // cross-class summaries of what to DO with what you are holding.
        AddLeftoverBands(groups, settings, req);

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

    /// <summary>
    /// #243 (tvongaza) on the phone: *"when you do an inventory dump, it could cross check
    /// which sky quests you've completed and which sky quest items you no longer need."*
    ///
    /// TWO groups, never one (Bevel's replace, Helm-signed 2026-09-02) — the same two the
    /// desktop bands draw, with the same headings, the same row words and the same hover,
    /// because all four of those live on <see cref="SkyLeftoverRow"/> and
    /// <see cref="SkyLeftoversResult"/>. This is the third renderer of ONE decision and it
    /// invents none of it; that is the whole lesson of #184, and of the two days EQBuddy
    /// Mobile hand-rolled the ready list (#210).
    ///
    /// **No page change was needed**, which is the point of routing it through the checklist
    /// shape: <c>index.html</c> already draws a non-tickable group generically
    /// (<c>g.tickable === false</c>), heading, note, row text and the row's detail as its
    /// sub-line. Trap 32 says a page-side fix can sit unseen on an open phone for weeks, so
    /// a feature that needs none reaches every paired device the moment the PC updates.
    /// </summary>
    private static void AddLeftoverBands(
        List<CompanionChecklistGroup> into, AppSettings? settings, CompanionQuestRequest req)
    {
        // The CHARACTER's classes, exactly as the desktop captures them one line before its
        // view lens narrows to one (#193's rule, one surface over): picks when the player has
        // picked, the resolved list otherwise. Empty stays empty, which is what suppresses
        // band B — "only other classes want this" said about a class you actually play is the
        // one false claim this band exists to avoid, and no lens is not a wildcard.
        var myClasses = req.Classes.Count > 0 ? req.Classes : req.CharacterClassNames;
        var leftovers = SkyLeftovers.Compute(
            req.Inventory, settings?.SkyQuestChecklist, settings?.SkyQuestCompleted,
            myClasses, req.Catalog);
        if (leftovers.IsEmpty) return;

        // Band A first: it is the reporter's own sentence and the only strong claim.
        Band(SkyLeftoverBand.NoLongerNeeded, leftovers.NoLongerNeededHeading, leftovers.HeldBackNote);
        Band(SkyLeftoverBand.OtherClassesWant, leftovers.OtherClassesWantHeading, note: "");

        void Band(SkyLeftoverBand band, string heading, string note)
        {
            var rows = leftovers.RowsIn(band);
            if (rows.Count == 0) return;   // each band carries its own absence
            into.Add(new CompanionChecklistGroup(
                heading,
                // Band A's note names what was deliberately left OUT and which quest wants
                // it. An item simply absent from the band reads as a bug in the join.
                note.Length > 0 ? note : null,
                [.. rows.Select(r => new CompanionChecklistRow(
                    LeftoverRowId(r), r.Line, r.Detail, Done: false))],
                // NO CLASS, so the page's class chips cannot narrow these — the same
                // treatment ★ Ready gets, and here it is load-bearing: band B is a claim
                // ABOUT the classes you have, so a chip hiding it would hide the answer.
                Class: null,
                // Not items. A leftover row is a summary of something you HOLD, so a
                // checkbox on it would be a silent no-op (#212, bjstrange).
                Tickable: false));
        }
    }

    /// <summary>A leftover row's identity on the wire — the band plus the row's own words,
    /// which are its item, its held COUNT and where it is sitting.
    ///
    /// **This is how the dump reaches the phone's render signature.** The Quests section
    /// fingerprint is built from the projected groups' headings, notes and row ids
    /// (<c>SectionFingerprints</c>), so putting the count and the location in the id is what
    /// makes a fresh <c>/outputfile inventory</c> push rather than being a no-op on this tab
    /// — the same defect the desktop's <c>inv:</c> signature term prevents, closed here by
    /// the rows themselves. It is deliberately NOT the dump's timestamp: that would wake
    /// every paired phone for a dump that changed nothing on this surface (trap 8), where
    /// this moves exactly when the band's claim moves.
    ///
    /// No tick action accepts an id of this shape, which is the other half of
    /// <c>Tickable: false</c> — belt and braces, exactly as the ★ Ready band's reward keys
    /// are (#212).</summary>
    internal static string LeftoverRowId(SkyLeftoverRow row) =>
        "sky-leftover|" + row.Band + "|" + row.Line;

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
