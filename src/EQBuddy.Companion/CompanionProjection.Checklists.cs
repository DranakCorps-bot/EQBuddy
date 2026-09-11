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

    /// <summary>
    /// Epic 1.0 for the phone — grouping and ordering from <see cref="QuestChecklistLayout"/>
    /// and the guided walkthrough from <see cref="GuideChecklistProjection"/>, which are the
    /// same two calls <c>QuestsView.RenderChecklist</c> makes.
    ///
    /// <para><b>This used to hand-roll its grouping</b>, exactly as <see cref="BuildSky"/> did
    /// before #184 — same four decisions (which rows group together, in what order, what the
    /// heading reads, what the sub-line says) spelled a second time. Delivery 3 is where that
    /// stopped being free: a guided class is one group rather than one per section, and a copy
    /// of the old grouping here would have left the phone showing the classic list while the PC
    /// showed the walkthrough. Porting a feature TO the phone is the signal the logic never
    /// went through the shared layer (David, 2026-08-18) — so the logic moves rather than the
    /// feature.</para></summary>
    private static CompanionChecklistSection BuildEpics(AppSettings? settings, CompanionQuestRequest req)
    {
        var items = settings?.EpicQuestChecklist ?? [];
        // The desktop's class lens and its classic-era lens, both honored: what the
        // phone lists is what the PC's Epic tab lists.
        var scoped = items
            .Where(i => settings is not { EpicQuestClassicOnly: true } || i.AvailableInClassic)
            .Where(i => settings is not { EpicQuestClass.Length: > 0 }
                || string.Equals(i.ClassName, settings.EpicQuestClass, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var all = QuestChecklistLayout.Epic(scoped);
        if (settings is not null && req.Ledger is not null)
            all = GuideChecklistProjection.ApplyEpic(all, scoped, GuideCatalog.Default,
                settings, req.Ledger, req.CharacterKey);

        var groups = all
            .Select(g => new CompanionChecklistGroup(
                // The class lens already narrows to one class, so repeating it on every
                // heading is the redundancy it was turned on to remove.
                Heading: settings is { EpicQuestClass.Length: > 0 }
                    ? g.Title
                    : $"{g.ClassName} — {g.Title}",
                Note: g.GuideCaption.Length > 0 ? g.GuideCaption : null,
                Class: g.ClassName,
                Rows: GuideRows(g),
                Title: g.Title,
                Card: GuideCard(g),
                Collapsed: g.Collapsed,
                Reward: g.RewardSummary.Length > 0 ? g.RewardSummary : null,
                RewardCard: g.RewardCard.Length > 0 ? g.RewardCard : null,
                Fold: FoldKey(g)))
            .ToList();

        // Counted off the ROWS the phone is showing rather than off `scoped`: a guided class
        // draws one row per objective, and an objective is one row (trap 72's neighbour — the
        // count and the list have to come from the same place or the header lies about the
        // body).
        return new CompanionChecklistSection(
            all.Sum(g => g.Done), all.Sum(g => g.Total), groups);
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

        // The guide projection, from the same point in the same order QuestsView applies it.
        // This is the whole of "the phone got guides": not a port, one call to the module
        // both screens already share (David, 2026-08-18 — parity by shared module).
        if (settings is not null && req.Ledger is not null)
            all = GuideChecklistProjection.Apply(all, GuideCatalog.Default,
                settings, req.Ledger, req.CharacterKey);

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
                    // The NPC plus the already-unlocked caveat, joined in Core so the
                    // phone's sub-line and the desktops' inline note cannot drift
                    // (Hateborne, 2026-09-03).
                    QuestChecklistLayout.ReadyDetail(g, req.UnlockedClasses),
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
            // A guided group leads with its guide caption ("Guide · 0 of 3 · 1 stub") and
            // keeps the state word after it. Both, not one: the caption says how far along
            // and how honest the data is, the note says whether it can be turned in — and
            // the desktop shows both, one above the other.
            g.GuideCaption.Length > 0
                ? g.Note is { } note ? g.GuideCaption + " · " + note : g.GuideCaption
                : g.Note,
            GuideRows(g),
            Class: g.ClassName,
            Title: g.Title,
            Card: GuideCard(g),
            Collapsed: g.Collapsed,
            Reward: g.RewardSummary.Length > 0 ? g.RewardSummary : null,
            RewardCard: g.RewardCard.Length > 0 ? g.RewardCard : null,
            Fold: FoldKey(g))));

        return new CompanionChecklistSection(
            scoped.Sum(g => g.Done), scoped.Sum(g => g.Total), groups);
    }

    /// <summary>
    /// One guided group's rows on the wire — <b>the one producer</b>, which Sky, Epic and the
    /// General tab's quest guides all call.
    ///
    /// <para>These were two copies when only Sky and Epic drew guides, and they had already
    /// started to differ (the unassigned mark, the stub lead, the improve door and the facts
    /// rule are four decisions each was making separately). DRA-46 made it three, which is the
    /// count at which a hand-kept copy stops being kept — so the decisions moved here rather
    /// than the third caller gaining its own.</para></summary>
    private static List<CompanionChecklistRow> GuideRows(QuestChecklistGroup group) =>
    [
        .. group.Rows.Select(r => new CompanionChecklistRow(
            r.Id,
            r.Title,
            r.Unassigned ? r.Detail + UnassignedMark : r.Detail,
            r.Acquired,
            r.StubNote.Length > 0 ? GuidePresentation.StubLead + " " + r.StubNote : null,
            // A phone has no hover, so what the desktop hangs on one rides the row (trap 35).
            // Empty on a transcribed step, whose sentence IS the row.
            r.GuideFacts.Length > 0 && r.StubNote.Length == 0 ? r.GuideFacts : null,
            // The same share-back door the desktop's pencil opens. A plain link, because the
            // phone's browser honours it natively — no substitute needed (trap 35).
            r.GuideRowKey.Length > 0
                && GuideChecklistProjection.Resolve(GuideCatalog.Default, r.Id)
                    is var (guide, objective)
                ? GuidePresentation.ImproveUrl(guide, objective)
                : null,
            r.IsSkipped,
            // A turn-in piece's answer is the bags, and the router refuses a tick of it — so
            // the page draws the count and no checkbox rather than a box that ignores taps.
            Tickable: r.LedgerItemName.Length == 0)),
    ];

    /// <summary>What a guided group's fold is keyed on, or null when the group is not guided
    /// and its heading is therefore not a control. The desktop's own fold key, so the two
    /// surfaces fold the same thing by the same name (see <see cref="CompanionChecklistGroup.Fold"/>
    /// for why only one of them persists it).</summary>
    private static string? FoldKey(QuestChecklistGroup group) =>
        group.GuideId.Length > 0 ? GuideChecklistProjection.FoldKey(group) : null;

    /// <summary>The active-step card for the phone — the SAME card the desktop draws, from
    /// the same projection. Nothing here decides what is next; it re-labels one already-worded
    /// record into the wire shape and drops the questions this step does not answer.</summary>
    private static CompanionGuideCard? GuideCard(QuestChecklistGroup group)
    {
        if (group.GuideCard is not { } card) return null;
        static string? OrNull(string s) => s.Length > 0 ? s : null;
        return new CompanionGuideCard(
            card.RowId,
            GuidePresentation.NextLead,
            card.Instruction,
            OrNull(card.Directions), OrNull(card.Detail), OrNull(card.Why),
            OrNull(card.BeforeLeaving), OrNull(card.StubNote), OrNull(card.ImproveUrl),
            GuidePresentation.DoneLabel, GuidePresentation.SkipLabel,
            OrNull(card.Held));
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
