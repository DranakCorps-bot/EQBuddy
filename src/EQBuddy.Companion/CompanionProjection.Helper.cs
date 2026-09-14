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
            Cap: HelperPresentation.Cap(answers.Withheld),
            GearWithheld: HelperPresentation.GearWithheld(answers.GearWithheld),
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
        h.MoneyNote, h.Cap, h.GearWithheld,
        Join(h.Gaps, g => g.Text + "|" + g.Prompt?.Command),
        Join(h.Deferred, d => d.Text),
        h.Empty?.Heading);
}
