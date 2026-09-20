using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Companion;

/// <summary>
/// **The Helper, projected** (DRA-71 D9; Fable's plan P15).
///
/// <para><b>It ranks nothing.</b> The one line that produces answers is
/// <c>Recommendations.Rank</c>, called with the <see cref="HelperInputs"/> the host handed
/// over — the same object, the same engine, the same cap. Everything else in this file turns
/// records into sentences by asking <see cref="HelperPresentation"/>, which is where the
/// desktop room asks too. Nothing here decides a word, a number or an order.</para>
///
/// <para>That is the #210 rule stated as code: EQBuddy Mobile spent two days answering a
/// question the desktop had lost, because the phone built its own list. A projection that
/// called anything but the shared producer would be the same arrangement waiting for the same
/// two days.</para>
/// </summary>
public static partial class CompanionProjection
{
    /// <summary>
    /// Build the Helper screen, or null when the host has nothing to rank with.
    ///
    /// <para>Null is a real answer and not a hole: it means the widget did not gather the
    /// bundle this pass, which is what happens when the surface is gated off or no device is
    /// subscribed to it (<c>CompanionHost.Tick</c>'s lazy rule). A section built from
    /// <see cref="HelperInputs.Nothing"/> would be a screen claiming the player has no
    /// history.</para>
    /// </summary>
    internal static CompanionHelperSection? BuildHelper(CompanionHelperRequest? request)
    {
        if (request is null) return null;

        // THE shared producer, and the only call in this file that decides anything.
        var answers = Recommendations.Rank(request.Inputs, request.Goals);

        var empty = answers.Top.Count == 0
                    && answers.Gaps.Count == 0
                    && answers.NotAnsweredYet.Count == 0;

        // The vendor-price caveat is drawn from what was actually BUILT rather than from
        // which goal is ticked — the desktop room's own rule, and the reason it can never
        // appear over a list with no price in it nor be missing from one that has.
        var money = answers.Top.Any(r => r.Why.Any(w => w is SellableDropFact or CatalogValueFact));

        // DRA-149 D1, the same rule one caveat along: the sweep compares base numbers, so the
        // phone says so wherever a gear row was built — once for the block, never per row.
        var gearBase = answers.Top.Any(r => r.Why.Any(w => w is GearUpgradeFact));

        // DRA-149 D4. Built before the record so the two captions over it can be withheld with
        // it — a phone that printed "shops eqlwiki's zone maps name" over nothing would be the
        // disclosure-line rule broken one block along.
        var merchants = Merchants(request);

        // DRA-216 D4. Built before the record for the reason `merchants` above it is: the three
        // captions over it are withheld with it. Gated on the same condition the desktop room
        // draws the block on — the gear goal is picked, or nothing is, which weighs everything.
        var tracked = request.Goals.Count != 0 && !request.Goals.Contains(HelperGoal.FarmGear)
            ? new List<string>()
            : [.. request.Inputs.Tracked.Select(HelperPresentation.TrackedRow)];

        return new CompanionHelperSection(
            Question: HelperPresentation.RoomQuestion,
            PicksLead: HelperPresentation.PicksOnPc,
            DoorsLead: HelperPresentation.DoorsOnPc,
            Picks: Picks(request),
            AnswersHeading: HelperPresentation.AnswersHeading,
            // The two disclosure lines are withheld with the answers they are about: a room
            // with nothing to say must not print "every answer below is read from your own
            // log" over no answers.
            SourceNote: empty ? "" : HelperPresentation.SourceNote,
            LevelNote: empty ? "" : LevelReadout.UsedByHelper(request.Inputs.Level),
            Answers: [.. answers.Top.Select(Answer)],
            MoneyNote: money ? HelperPresentation.MoneyPriceNote : "",
            GearBaseNote: gearBase ? HelperPresentation.GearBaseClaimNote : "",
            Cap: HelperPresentation.Cap(answers.Withheld),
            GearWithheld: HelperPresentation.GearWithheld(answers.GearWithheld),
            // DRA-84 D2. Same words, same producer, same wire — a refusal the PC made and the
            // phone did not mention would be the two surfaces disagreeing about what the list
            // contains, and every sentence rides the wire rather than index.html (trap 32).
            GearBandRefused: HelperPresentation.BandRefused(
                answers.GearBandRefusals, HelperPresentation.BandRefusedUpgrades),
            // DRA-180 D2, the same discipline one axis over. The era gate runs BEFORE the band
            // gate, so these are refusals the band sentence will never mention — a phone that
            // carried only the band caption would draw a shorter list than the PC with no
            // sentence explaining the difference.
            GearEraRefused: HelperPresentation.EraRefused(
                answers.GearEraRefusals, HelperPresentation.BandRefusedUpgrades),
            MaterialEraRefused: HelperPresentation.EraRefused(
                answers.MaterialEraRefusals, HelperPresentation.BandRefusedMaterials),
            // DRA-84 D4, same rule one slice on: a drop offer the PC withheld for having no
            // creature to name is withheld on the phone too, and says so in the same words.
            GearWhoWithheld: HelperPresentation.DropOffersWithheld(answers.GearWhoWithheld),
            // DRA-219, the same discipline on the other acquisition path. The quest-source rule
            // removes rows the PC's list does not have either, and the two sweep counts are
            // about items that never reached a bucket — a phone that carried the who caption and
            // not these would leave a player reading a shorter list with three sentences' worth
            // of explanation missing.
            GearQuestWithheld: HelperPresentation.QuestOffersWithheld(answers.GearQuestWithheld),
            GearNoSource: HelperPresentation.SourcelessUpgrades(answers.GearNoSource),
            GearQuestOnly: HelperPresentation.QuestOnlyUpgrades(answers.GearQuestOnly),
            // DRA-222 D6, the same discipline one rule on. This one removes a SWAP rather than
            // a place, so its sentence is the only one on the record about the player's hands —
            // a phone drawing the four captions above and not this one would show a shorter
            // list than the PC with nothing saying why.
            GearOffHandRefused: HelperPresentation.OffHandRefused(answers.GearOffHandRefusals),
            // DRA-149 D3: the SAME two rules over the materials list, on their own two fields
            // rather than folded into the gear ones — the desktop room draws four captions here
            // and the phone must draw the same four or the two surfaces disagree about what the
            // list contains. The professions note rides too, because it is the sentence saying
            // where these rows came FROM.
            // **DRA-180 D3, and it is the reason the phone half of this slice is not optional.**
            // Every caption on this record counts PLACES; these count the player's own worn
            // items, which is the question that produced the FAIL. A phone that drew the band
            // and era sentences and not these would show the player the same list as the PC and
            // still leave the bow unexplained. Capped and worded HERE by the same producer the
            // room calls, so the projection decides no word and no number (trap 33).
            AnchorsAllRemoved: [.. answers.GearAnchorsRemoved
                .Take(HelperPresentation.GearAnchorsNamed)
                .Select(HelperPresentation.AnchorAllRemoved)],
            AnchorsNotNamed: HelperPresentation.AnchorsNotNamed(
                answers.GearAnchorsRemoved.Count
                - Math.Min(answers.GearAnchorsRemoved.Count, HelperPresentation.GearAnchorsNamed)),
            MaterialBandRefused: HelperPresentation.BandRefused(
                answers.MaterialBandRefusals, HelperPresentation.BandRefusedMaterials),
            MaterialWhoWithheld: HelperPresentation.DropOffersWithheld(answers.MaterialWhoWithheld),
            MaterialNote: answers.Top.Any(r => r.Why.Any(w => w is TradeskillMaterialFact))
                ? HelperPresentation.ProfessionsFarmNote : "",
            // DRA-149 D4, the vendor half. Both captions ride the wire rather than index.html
            // (trap 32), and the door is INTENT: the phone cannot open a browser on the PC, so
            // it gets the sentence saying what is behind it, once (trap 35).
            MerchantNote: merchants.Count == 0 ? "" : HelperPresentation.MerchantsNote,
            MerchantDoorNote: merchants.Count == 0
                ? ""
                : HelperPresentation.DoorTip(new HelperDoor(HelperDoorKind.WikiZone, "")),
            Merchants: merchants,
            // **DRA-216 D4, and the phone half is not optional.** A tracked goal is the one
            // thing in this room that OUTLIVES the answers, so a phone that drew the list and
            // not the PC — or the PC and not the phone — would have the two surfaces disagreeing
            // about what the player is working on, which is the one thing the phone half is not
            // allowed to do. It is READ-ONLY here: every control in this room writes the PC's
            // profile, so the tracking door ports as the sentence saying where it is (trap 35).
            // The three captions are withheld with the list they are about, the merchant block's
            // own rule directly above.
            TrackedHeading: tracked.Count == 0 ? "" : HelperPresentation.TrackedHeading,
            TrackedNote: tracked.Count == 0 ? "" : HelperPresentation.TrackedNote,
            TrackedOnPc: tracked.Count == 0 ? "" : HelperPresentation.TrackedOnPc,
            Tracked: tracked,
            // DRA-149 D2, and it arrives WITH its doors in the same slice — DRA-84 D5's lesson
            // was that a caption which reaches the wire and is never drawn passes every test in
            // the parity suite, so the page-side must-list gains its row here too (trap 34).
            UnreadWorn: HelperPresentation.UnreadWorn(answers.UnreadWorn),
            UnreadWornDoors: Doors([.. answers.UnreadWorn
                .Take(HelperPresentation.UnreadWornNamed)
                .Select(item => new HelperDoor(HelperDoorKind.WikiItem, item))]),
            Gaps: [.. answers.Gaps.Select(Gap)],
            Deferred: [.. answers.NotAnsweredYet.Select(Deferred)],
            Empty: empty
                ? new CompanionHelperEmpty(
                    HelperPresentation.Nothing.Heading, HelperPresentation.Nothing.Explanation)
                : null);
    }

    /// <summary>
    /// The pickers, as intent (trap 35).
    ///
    /// <para><b>Drawn on exactly the condition the desktop room draws them on</b> — while the
    /// goal is picked, or while nothing is, which weighs everything. That condition is
    /// repeated here rather than shared because it is three words and the ALTERNATIVE is
    /// worse: a shared "which blocks does this room draw" helper would be a second place for
    /// the room's layout to live, and the parity test asserts the two lists match anyway.</para>
    ///
    /// <para>The GEAR intent is not a multi-select — the desktop draws a segmented strip —
    /// and it ports the same way regardless: the face is the chosen intent's own label, and
    /// the detail is the tip that sits on the segment's hover.</para>
    /// </summary>
    private static List<CompanionHelperPick> Picks(CompanionHelperRequest r)
    {
        var goals = r.Goals;
        var picks = new List<CompanionHelperPick>
        {
            new(HelperPresentation.GoalsHeading,
                HelperPresentation.GoalFace(goals),
                HelperPresentation.GoalStripNote),
        };

        bool Wants(HelperGoal goal) => goals.Count == 0 || goals.Contains(goal);

        if (Wants(HelperGoal.FarmGear))
        {
            // The worn picker is the SECOND face on the desktop and only exists once the
            // first decision is made — so it rides this row's detail rather than becoming a
            // row of its own, which is what "one face per decision" means on a surface with
            // no faces.
            var intent = r.Inputs.GearIntent;
            var worn = intent == GearIntent.UpgradeWorn
                ? " " + HelperPresentation.WornFace(r.Inputs.WornPicks, r.Inputs.Worn.Count)
                : "";
            picks.Add(new CompanionHelperPick(
                HelperPresentation.GoalLabel(HelperGoal.FarmGear),
                HelperPresentation.GearIntentLabel(intent) + worn,
                HelperPresentation.GearIntentNote,
                HelperPresentation.GearIntentTip(intent),
                // The one pick whose own empty state asks for a file the game writes: with no
                // dump there is nothing to anchor on, and the desktop's worn picker says so
                // with a ⧉ beside it.
                r.Inputs.Worn.Count == 0 ? Prompt(CommandPrompts.HelperInventory) : null));
        }

        if (Wants(HelperGoal.WorkOnFaction))
            picks.Add(r.Inputs.Factions is { Standings.Count: > 0 }
                ? new CompanionHelperPick(
                    HelperPresentation.GoalLabel(HelperGoal.WorkOnFaction),
                    HelperPresentation.FactionFace(r.Inputs.PickedFactions),
                    HelperPresentation.FactionPickerNote)
                : new CompanionHelperPick(
                    HelperPresentation.GoalLabel(HelperGoal.WorkOnFaction),
                    "", HelperPresentation.FactionPickerNoDump,
                    Prompt: Prompt(CommandPrompts.HelperFaction)));

        if (Wants(HelperGoal.UnlockRaces) || Wants(HelperGoal.UnlockClasses))
            picks.Add(r.Inputs.HasAchievements
                ? new CompanionHelperPick(
                    HelperPresentation.UnlockPickerHeading,
                    UnlockPickReadout.Face(
                        r.Inputs.UnlockPicks, UnlockSubjects(r), HelperPresentation.FaceChars),
                    UnlockPickReadout.Note)
                : new CompanionHelperPick(
                    HelperPresentation.UnlockPickerHeading,
                    "", UnlockPickReadout.NoDump,
                    Prompt: Prompt(CommandPrompts.HelperAchievements)));

        if (Wants(HelperGoal.FarmMaterials))
            picks.Add(new CompanionHelperPick(
                HelperPresentation.GoalLabel(HelperGoal.FarmMaterials),
                HelperPresentation.ProfessionFace(
                    [.. r.Professions.Select(x => Tradeskills.For(x).Name)], r.ProfessionsOffered),
                HelperPresentation.ProfessionPickerNote,
                // The learn note is said ONCE over the list on the desktop, and it is the
                // sentence that stops eight professions at zero reading as a broken screen.
                HelperPresentation.ProfessionLearnNote));

        return picks;
    }

    /// <summary>
    /// **THE VENDOR HALF, PORTED** (DRA-149 D4, plan P5).
    ///
    /// <para>One entry per LISTED profession — <c>TradeskillPickStore.ListedFrom</c>, so "picked
    /// nothing" means the same eight here as it does on the PC — and the lines inside each are
    /// <c>HelperPresentation.MerchantsShown</c>'s, which is the desktop room's own call. The
    /// projection chooses no line, no order and no cap.</para>
    ///
    /// <para>Gated on the same condition the desktop room draws the professions block on: the
    /// goal is picked, or nothing is. An empty list is what a player who picked only Level Up
    /// gets, and it withholds the two captions with it.</para>
    /// </summary>
    private static List<CompanionHelperMerchants> Merchants(CompanionHelperRequest r)
    {
        if (r.Goals.Count != 0 && !r.Goals.Contains(HelperGoal.FarmMaterials)) return [];

        var rows = new List<CompanionHelperMerchants>();
        foreach (var skill in TradeskillPickStore.ListedFrom(r.Professions))
        {
            var shown = HelperPresentation.MerchantsShown(ZoneMerchants.Default, skill);
            rows.Add(new CompanionHelperMerchants(
                Tradeskills.For(skill).Name,
                [.. shown.Select(HelperPresentation.MerchantRow)],
                shown.Count == 0
                    ? ""
                    : HelperPresentation.MerchantsCapped(
                        shown.Count, ZoneMerchants.Default.ZonesFor(skill)),
                shown.Count == 0 ? HelperPresentation.NoMerchantsFor(skill) : ""));
        }
        return rows;
    }

    /// <summary>How many unlock subjects the picker is offering — the desktop room's own
    /// arithmetic, which is per SECTION: a player who picked only races is offered only
    /// races, and a face that counted both halves would say "of 31" over a list of 12.</summary>
    private static int UnlockSubjects(CompanionHelperRequest r)
    {
        var n = 0;
        if (r.Goals.Count == 0 || r.Goals.Contains(HelperGoal.UnlockRaces)) n += r.Inputs.Races.Count;
        if (r.Goals.Count == 0 || r.Goals.Contains(HelperGoal.UnlockClasses)) n += r.Inputs.Classes.Count;
        return n;
    }

    private static CompanionHelperAnswer Answer(Recommendation rec) => new(
        HelperPresentation.Headline(rec),
        HelperPresentation.Serves(rec),
        [.. rec.Why
            .Select(f => (Text: HelperPresentation.Why(f), f.Evidence))
            .Where(x => x.Text.Length > 0)
            .Select(x => new CompanionHelperWhy(x.Text, x.Evidence == Evidence.Personal))],
        HelperPresentation.WithheldWhy(rec.WithheldWhy),
        Doors(rec.Doors));

    /// <summary>An answerable goal that produced nothing, and what would feed it. The
    /// command pairing is the desktop room's own, one gap reason at a time — a surface that
    /// needs an in-game command SHIPS the command, and on this device that means selectable
    /// text off <see cref="GameCommands"/> rather than a ⧉ (trap 35).</summary>
    private static CompanionHelperNote Gap(GoalGap gap) => new(
        HelperPresentation.Gap(gap),
        // The two gaps whose answer is a ROOM keep the desktop's pairing; the three whose
        // answer is a control the player is already looking at get nothing, because a door
        // pointing at the block above it is furniture on any surface.
        gap.Reason switch
        {
            GoalGapReason.GearIntentNotAnsweredYet => Doors([new HelperDoor(HelperDoorKind.Wealth, "")]),
            _ => [],
        },
        gap.Reason switch
        {
            GoalGapReason.NoFactionDump => Prompt(CommandPrompts.HelperFaction),
            GoalGapReason.NoAchievementsDump => Prompt(CommandPrompts.HelperAchievements),
            GoalGapReason.NoInventoryDump => Prompt(CommandPrompts.HelperInventory),
            _ => null,
        });

    /// <summary>A goal whose engine does not exist yet, and the room that answers its
    /// question today. A deferred goal that pointed nowhere is the rail's own forbidden
    /// shape, and it is forbidden here for the same reason.</summary>
    private static CompanionHelperNote Deferred(HelperGoal goal)
    {
        var kind = HelperPresentation.NotAnsweredDoor(goal);
        return new CompanionHelperNote(
            HelperPresentation.NotAnsweredYet(goal),
            kind is { } k ? Doors([new HelperDoor(k, "")]) : []);
    }

    /// <summary>
    /// Doors, as intent.
    ///
    /// <para><b>Every door in this room is carried, including the wiki ones, and none of them
    /// is a link.</b> The page has no outbound links at all today, and giving the Helper the
    /// first one would be this slice inventing a capability rather than porting a surface —
    /// so a wiki door reads exactly like a room door: its name, and the sentence saying what
    /// is behind it. That sentence already tells the player they open the page themselves and
    /// that EQBuddy fetches nothing, which is the request-policy half, unchanged.</para>
    ///
    /// <para>The tip RIDES the row rather than a hover, because a phone has no pointer — the
    /// plan says so for the why-lines and it is the same device (trap 35).</para>
    /// </summary>
    private static List<CompanionHelperDoor> Doors(IReadOnlyList<HelperDoor> doors) =>
        [.. doors.Select(d => new CompanionHelperDoor(
            HelperPresentation.DoorLabel(d.Kind), HelperPresentation.DoorTip(d)))];

    /// <summary>The section's stable identity for push decisions. Every SENTENCE, because
    /// every sentence in this section is the output of an engine that can move without any
    /// other field moving — a re-rank that swaps two answers leaves both counts unmoved
    /// (trap 72), and nothing here ticks on a clock (trap 8): the Helper carries no
    /// countdown, no age and no "x ago".</summary>
    private static string HelperPrint(CompanionHelperSection h) => Fold(
        Join(h.Picks, p => $"{p.Heading}={p.Face}|{p.Detail}|{p.Prompt?.Command}"),
        h.LevelNote,
        Join(h.Answers, a => a.Headline + "/" + a.Serves
            + "/" + Join(a.Why, w => w.Text) + "/" + a.WithheldWhy
            + "/" + Join(a.Doors, d => d.Label)),
        // DRA-84 D2: the refusal sentence carries the level and the bands, so it moves when a
        // ding changes which zones the gate refuses even though no other field here does
        // (trap 72 — the repaint gate must see the store the feature writes).
        // DRA-149 D2: the unread sentence NAMES its items, so it moves when a new dump changes
        // which of them EQBuddy cannot read even though no count beside it does (trap 72).
        // DRA-149 D3: the materials captions ride too, and the band one for the same reason the
        // gear band one does — it quotes the level, so a ding moves it while every count here
        // stands still.
        // DRA-180 D2: the era captions fold too. They quote the WORLD's era, which is a
        // curated value a build can change without moving a single count beside it (trap 72).
        // DRA-219: the quest path's three captions fold too. Each carries a COUNT that can move
        // while every other field here stands still — flipping the include-quests toggle turns
        // one of them off and another on with the same rows on screen (trap 72).
        // DRA-222 D6: and the off-hand caption, for exactly that reason — its count moves when
        // the player's own SECONDARY changes, which alters no other field on this record. A
        // fingerprint that cannot see it is a phone still drawing the pre-dump weapon list.
        h.MoneyNote, h.GearBaseNote, h.Cap, h.GearWithheld, h.GearBandRefused,
        h.GearEraRefused, h.MaterialEraRefused,
        h.GearWhoWithheld, h.GearQuestWithheld, h.GearNoSource, h.GearQuestOnly,
        h.GearOffHandRefused, h.UnreadWorn,
        // DRA-180 D3: the per-anchor sentences fold as LINES, never as a count. They NAME the
        // worn item and carry its three cause numbers, so swapping one picked anchor for another
        // — or a ding moving which of its candidates the band gate takes — rewrites them while
        // every count on this record stands still (trap 72, and trap 8's other half: nothing in
        // them drifts on a tick).
        Join(h.AnchorsAllRemoved, a => a), h.AnchorsNotNamed,
        h.MaterialBandRefused, h.MaterialWhoWithheld, h.MaterialNote,
        // DRA-149 D4: the vendor blocks fold their LINES, not a count. The profession PICK is
        // what moves them, and a pick swapped one-for-one leaves every count here unmoved
        // (trap 72's own shape) — Jewelcrafting out and Pottery in is eight rows before and
        // eight rows after. The lines are transcribed wiki prose: no clock, no age, nothing
        // that drifts on a tick (trap 8).
        Join(h.Merchants, m => m.Profession + "=" + Join(m.Lines, l => l) + "|" + m.More
            + "|" + m.Empty),
        // DRA-216 D4: the tracked rows fold as LINES, never as a count — trap 72 is the reason
        // this store exists to be watched at all. A goal untracked and another tracked in one
        // pass leaves the count unmoved, and a paired phone would keep drawing a goal the
        // player has already dropped. The rows carry an absolute DATE and nothing relative, so
        // none of this drifts on a tick (trap 8).
        Join(h.Tracked, t => t), h.TrackedHeading,
        Join(h.Gaps, g => g.Text + "|" + g.Prompt?.Command),
        Join(h.Deferred, d => d.Text),
        h.Empty?.Heading);
}
