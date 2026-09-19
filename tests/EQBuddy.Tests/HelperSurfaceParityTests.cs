using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The Helper answers the same question the same way on both screens** (DRA-71 D9;
/// Fable's plan P15).
///
/// <para>The sibling of <see cref="SurfaceParityTests"/>, and it asserts the same thing that
/// suite does about the quest checklists: not that two lists happen to match today, but that
/// the phone is READING THE SHARED MODULE. #210 is what made that distinction worth a test
/// file — EQBuddy Mobile answered "what can I turn in right now" for two days after the
/// desktop had lost it, because the phone built its own list.</para>
///
/// <para><b>The parity claim here is stronger than a feature list, because the Helper has no
/// list to keep level.</b> Its output is prose an engine produced, so the only thing worth
/// asserting is provenance: every sentence the phone draws is a sentence
/// <see cref="HelperPresentation"/> built, in the order <see cref="Recommendations.Rank"/>
/// put it, over the <see cref="HelperInputs"/> the host handed both surfaces. A test that
/// compared two hand-written strings would pass forever and prove nothing.</para>
/// </summary>
public class HelperSurfaceParityTests
{
    // ---- fixtures -------------------------------------------------------------------

    private static SessionRow Session(string zone, double hours, double xp, long copper = 0) =>
        new(1, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(hours),
            hours * 3600, hours * 3600, "ended", zone, 0, xp, copper, 0, 0, 0, "", "");

    private static MobSummary Mob(string name, string zone, int kills) =>
        new(name, kills, kills, 30, 0, 0, []) { Zone = zone };

    /// <summary>Two zones with real history, so the ranking has something to rank and the
    /// throughput comparison has a second zone to compare against (a fold with one zone
    /// compares a zone with itself, which is D4's own caveat).</summary>
    private static HelperInputs Inputs(ResolvedLevel level = default)
    {
        IReadOnlyList<SessionRow> sessions =
        [
            Session("Lower Guk", 6, 240, 5_000) with { Id = 1 },
            Session("Befallen", 4, 80, 900) with { Id = 2 },
        ];
        IReadOnlyList<MobSummary> pool =
        [
            Mob("a froglok tad", "Lower Guk", 300),
            Mob("a skeleton", "Befallen", 120),
        ];
        return new HelperInputs(
            ZoneHistory.Fold(sessions, pool), pool, null, [], [], [], [], false,
            [], [], null, level);
    }

    private static CompanionHelperRequest Request(
        HelperInputs? inputs = null, params HelperGoal[] goals) =>
        new(inputs ?? Inputs(), goals, [], Tradeskills.All.Count);

    private static CompanionHelperSection Phone(CompanionHelperRequest request) =>
        CompanionProjection.Build(
            new CompanionInputs
            {
                Character = "Dranak",
                AppVersion = "2.0.0",
                Offered = CompanionSurfaces.All,
                Helper = request,
            },
            new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Local)).Helper!;

    // ---- the engine is the SAME engine ----------------------------------------------

    /// <summary>
    /// **The claim the whole slice rests on.** The phone's answers are
    /// <see cref="Recommendations.Rank"/>'s answers, in its order, worded by
    /// <see cref="HelperPresentation"/> — headline, "serves", every why-line, and the
    /// per-row withheld note.
    /// </summary>
    [Fact]
    public void ThePhoneDrawsExactlyWhatTheSharedEngineRanked()
    {
        var request = Request(goals: HelperGoal.LevelUp);
        var desktop = Recommendations.Rank(request.Inputs, request.Goals);
        var phone = Phone(request);

        Assert.NotEmpty(desktop.Top);
        Assert.Equal(desktop.Top.Count, phone.Answers.Count);

        foreach (var (want, got) in desktop.Top.Zip(phone.Answers))
        {
            Assert.Equal(HelperPresentation.Headline(want), got.Headline);
            Assert.Equal(HelperPresentation.Serves(want), got.Serves);
            Assert.Equal(HelperPresentation.WithheldWhy(want.WithheldWhy), got.WithheldWhy);
            Assert.Equal(
                want.Why.Select(HelperPresentation.Why).Where(t => t.Length > 0),
                got.Why.Select(w => w.Text));
        }
    }

    /// <summary>The ORDER is the engine's, which is the half a "same set of sentences" test
    /// would miss: HOME-005 makes a zone serving two goals outrank either alone, and a
    /// projection that sorted for itself would put the wrong camp first while every string
    /// still matched.</summary>
    [Fact]
    public void ThePhoneKeepsTheEnginesOrderAndNeverItsOwn()
    {
        var request = Request(goals: [HelperGoal.LevelUp, HelperGoal.MakeMoney]);
        var desktop = Recommendations.Rank(request.Inputs, request.Goals);
        var phone = Phone(request);

        Assert.Equal(
            desktop.Top.Select(r => r.Zone.Length > 0 ? r.Zone : r.Subject),
            phone.Answers.Select(a => a.Headline.Split('—')[0].Trim()));
    }

    /// <summary>A catalog line carries its estimate label IN THE TEXT (HOME-004), and the
    /// flag beside it is a second, quieter signal of the same thing. A page that had to add
    /// the label itself would be one build away from a sentence that quietly claimed to be
    /// measured.</summary>
    [Fact]
    public void EveryWhyLineIsTheOneProducersSentenceIncludingTheCatalogLabel()
    {
        var request = Request(goals: HelperGoal.LevelUp);
        var phone = Phone(request);

        var lines = phone.Answers.SelectMany(a => a.Why).ToList();
        Assert.NotEmpty(lines);
        foreach (var line in lines)
            Assert.False(!line.Personal && !line.Text.Contains(HelperPresentation.CatalogLabel),
                $"a catalog line reached the phone without its estimate label: {line.Text}");
    }

    /// <summary>The cap says what it withheld, on this screen too (trap 50): the fourth-best
    /// camp is exactly the one somebody is looking for.</summary>
    [Fact]
    public void ASurvivingCapSaysSoOnThePhoneToo()
    {
        var request = Request(goals: HelperGoal.LevelUp);
        var desktop = Recommendations.Rank(request.Inputs, request.Goals);
        var phone = Phone(request);

        Assert.Equal(HelperPresentation.Cap(desktop.Withheld), phone.Cap);
        Assert.Equal(HelperPresentation.GearWithheld(desktop.GearWithheld), phone.GearWithheld);
    }

    /// <summary>
    /// **AND SO DOES A REFUSAL** (DRA-84 D2, trap 50). A zone the PC removed from the list and
    /// the phone did not mention is the two surfaces disagreeing about what the list contains —
    /// and the phone reader has no PC in front of them to notice.
    ///
    /// <para><b>Its fixture produces a real refusal, which is the whole point of it being its
    /// own test.</b> The parity fixture above wears nothing, so Farm Gear has no anchor there
    /// and both sides would agree on an empty string: a guard aimed at nothing is green (trap
    /// 78). The assertion below is that the refusal is NON-empty first.</para>
    /// </summary>
    [Fact]
    public void ARefusedZoneSaysSoOnThePhoneToo()
    {
        var inputs = Inputs(new ResolvedLevel(30, LevelSource.Observed, DateTime.Now)) with
        {
            Worn = [new WornItem("Rusty Helm", "Rusty Helm", "HEAD",
                ItemStatsBlock.Parse(["Slot: HEAD", "AC: 4"]))],
            Items = new ItemCatalog([
                new ItemCatalog.Record
                {
                    Name = "Bone Helm", StatsText = "Slot: HEAD\nAC: 9",
                    Slots = ["HEAD"], Ac = 9, DropZones = ["Crushbone"],
                },
            ]),
            Bands = ZoneLevels.Default,
        };
        var request = Request(inputs, HelperGoal.FarmGear);
        var desktop = Recommendations.Rank(request.Inputs, request.Goals);

        var said = HelperPresentation.BandRefused(desktop.GearBandRefusals, HelperPresentation.BandRefusedUpgrades);
        Assert.NotEmpty(desktop.GearBandRefusals);
        Assert.NotEqual("", said);
        Assert.Equal(said, Phone(request).GearBandRefused);
    }

    /// <summary>
    /// **AND SO DOES A WITHHELD DROP OFFER** (DRA-84 D4, plan P3). Same shape one rule on: the
    /// PC dropped an offer because nothing could say what drops it, and a phone that listed one
    /// fewer row without a word would be the two surfaces disagreeing about the list again.
    ///
    /// <para>Its fixture makes a real withhold — a catalog record whose page names nobody, in a
    /// zone the band gate keeps — and asserts the count is non-zero BEFORE comparing the
    /// sentences, because "" == "" is what a guard aimed at nothing looks like (trap 78).</para>
    /// </summary>
    [Fact]
    public void AWithheldDropOfferSaysSoOnThePhoneToo()
    {
        var inputs = Inputs(new ResolvedLevel(30, LevelSource.Observed, DateTime.Now)) with
        {
            Worn = [new WornItem("Rusty Helm", "Rusty Helm", "HEAD",
                ItemStatsBlock.Parse(["Slot: HEAD", "AC: 4"]))],
            Items = new ItemCatalog([
                new ItemCatalog.Record
                {
                    Name = "Bone Helm", StatsText = "Slot: HEAD\nAC: 9",
                    Slots = ["HEAD"], Ac = 9, DropZones = ["Lower Guk"],
                },
            ]),
        };
        var request = Request(inputs, HelperGoal.FarmGear);
        var desktop = Recommendations.Rank(request.Inputs, request.Goals);

        var said = HelperPresentation.DropOffersWithheld(desktop.GearWhoWithheld);
        Assert.True(desktop.GearWhoWithheld > 0);
        Assert.NotEqual("", said);
        Assert.Equal(said, Phone(request).GearWhoWithheld);
    }

    /// <summary>
    /// **AND THE CREATURES THEMSELVES RIDE THE WIRE** (trap 32). The who clause is part of the
    /// why-line's own sentence rather than a field of its own, so this asserts the phone's TEXT
    /// carries the names — a projection that dropped them would still match on every field name.
    /// </summary>
    [Fact]
    public void ThePhonesRowNamesTheSameCreaturesAsThePcs()
    {
        var record = new ItemCatalog.Record
        {
            Name = "Bone Helm", StatsText = "Slot: HEAD\nAC: 9",
            Slots = ["HEAD"], Ac = 9, DropZones = ["Lower Guk"],
            DropMobs = new() { ["Lower Guk"] = ["a froglok knight", "a froglok shaman"] },
        };
        var inputs = Inputs(new ResolvedLevel(30, LevelSource.Observed, DateTime.Now)) with
        {
            Worn = [new WornItem("Rusty Helm", "Rusty Helm", "HEAD",
                ItemStatsBlock.Parse(["Slot: HEAD", "AC: 4"]))],
            Items = new ItemCatalog([record]),
        };
        var request = Request(inputs, HelperGoal.FarmGear);
        var desktop = Recommendations.Rank(request.Inputs, request.Goals);

        var fact = Assert.Single(desktop.Top[0].Why.OfType<GearUpgradeFact>());
        Assert.Equal(["a froglok knight", "a froglok shaman"], fact.Who);

        var lines = Phone(request).Answers.SelectMany(a => a.Why).Select(w => w.Text).ToList();
        Assert.Contains(lines,
            text => text.Contains("a froglok knight and a froglok shaman drop it",
                StringComparison.Ordinal));
    }

    /// <summary>The Helper names the level it used, on both screens, off the one readout.
    /// A ranking that quietly weighed a number the player disagrees with — and never said
    /// which — is the shape that makes somebody distrust a whole room.</summary>
    [Fact]
    public void ThePhoneNamesTheLevelTheEngineUsedAndWhereItCameFrom()
    {
        var known = Request(Inputs(new ResolvedLevel(30, LevelSource.Stated, DateTime.Now)),
            HelperGoal.LevelUp);
        Assert.Equal(LevelReadout.UsedByHelper(known.Inputs.Level), Phone(known).LevelNote);

        // And the unknown arm, which is a different sentence rather than a missing one.
        var unknown = Request(Inputs(ResolvedLevel.Unknown), HelperGoal.LevelUp);
        Assert.Equal(LevelReadout.UsedByHelper(ResolvedLevel.Unknown), Phone(unknown).LevelNote);
        Assert.NotEqual(Phone(known).LevelNote, Phone(unknown).LevelNote);
    }

    // ---- the pickers, ported as intent (trap 35) -------------------------------------

    /// <summary>
    /// The phone draws a pick for exactly the decisions the desktop room draws a picker for —
    /// while its goal is picked, or while nothing is, which weighs everything.
    /// </summary>
    [Theory]
    // Nothing picked weighs everything, so every sub-pick is offered.
    [InlineData(new HelperGoal[0], 5)]
    // One goal with no sub-picker of its own: the goals face, and nothing else.
    [InlineData(new[] { HelperGoal.LevelUp }, 1)]
    [InlineData(new[] { HelperGoal.FarmGear }, 2)]
    [InlineData(new[] { HelperGoal.WorkOnFaction }, 2)]
    [InlineData(new[] { HelperGoal.FarmMaterials }, 2)]
    // The two unlock goals SHARE one picker — a second copy of it would be a second writer
    // of one selection (trap 4).
    [InlineData(new[] { HelperGoal.UnlockRaces, HelperGoal.UnlockClasses }, 2)]
    public void ThePicksDrawnAreTheBlocksTheDesktopRoomDraws(HelperGoal[] goals, int expected)
        => Assert.Equal(expected, Phone(Request(goals: goals)).Picks.Count);

    /// <summary>The face is the PICKER's own answer, not a count the page made up — so
    /// "Any goal" means the same thing on both screens and a cap that counts instead of
    /// listing counts the same way.</summary>
    [Fact]
    public void TheFaceIsThePickerFacesOwnWordsOnBothScreens()
    {
        Assert.Equal(HelperPresentation.GoalFace([]), Phone(Request()).Picks[0].Face);

        var two = new[] { HelperGoal.LevelUp, HelperGoal.MakeMoney };
        Assert.Equal(HelperPresentation.GoalFace(two), Phone(Request(goals: two)).Picks[0].Face);
    }

    /// <summary>**Nothing on this screen is a control.** Every affordance in that room writes
    /// to the profile the PC is playing from, and one of its doors has a side effect behind
    /// it, so the phone may not tick anything here.</summary>
    [Fact]
    public void TheHelperIsNotTickable() =>
        Assert.False(CompanionSurfaces.AcceptsTicks(CompanionSurfaces.Helper));

    /// <summary>The screen belongs to the room it shows — the single-source join, which is
    /// the one thing that makes a rename of a shell room a COMPILE error here.</summary>
    [Fact]
    public void TheScreenBelongsToTheHelperRoom() =>
        Assert.Equal(ShellPage.Helper, CompanionSurfaces.PageFor(CompanionSurfaces.Helper));

    /// <summary>A surface the gate can offer must be in the ONE list every reader builds
    /// from — the offer checkboxes, the ⚙ picker, the per-section change detection and the
    /// subscription filter (trap 20's neighbour: a surface nothing offers is a screen no
    /// device can ever pick).</summary>
    [Fact]
    public void TheScreenIsOfferedAndDescribed()
    {
        Assert.Contains(CompanionSurfaces.Helper, CompanionSurfaces.All);
        Assert.NotEmpty(CompanionSurfaces.Label(CompanionSurfaces.Helper));
        Assert.NotEmpty(CompanionSurfaces.Describe(CompanionSurfaces.Helper));
    }

    // ---- doors, as intent, with the tip riding the row -------------------------------

    /// <summary>
    /// Every door the engine emitted reaches the phone, with the desktop's own label AND the
    /// sentence the desktop keeps on a hover — because this device has no pointer (trap 35).
    /// Dropping the tip would leave a row of bare nouns with nothing saying what they open.
    /// </summary>
    [Fact]
    public void EveryDoorArrivesWithItsLabelAndItsTipRidingTheRow()
    {
        var request = Request(goals: HelperGoal.LevelUp);
        var desktop = Recommendations.Rank(request.Inputs, request.Goals);
        var phone = Phone(request);

        var doors = desktop.Top.SelectMany(r => r.Doors).ToList();
        Assert.NotEmpty(doors);
        Assert.Equal(
            doors.Select(d => (HelperPresentation.DoorLabel(d.Kind), HelperPresentation.DoorTip(d))),
            phone.Answers.SelectMany(a => a.Doors).Select(d => (d.Label, d.Detail)));

        Assert.NotEmpty(phone.DoorsLead);
    }

    // ---- the silences ----------------------------------------------------------------

    /// <summary>
    /// **A goal with nothing to say names what would feed it, and the disclosures stay** —
    /// because a gap IS something to say. The empty-room branch is for a different state
    /// entirely (below), and a projection that fired it here would replace a sentence naming
    /// the missing file with a shrug.
    /// </summary>
    [Fact]
    public void AGoalWithNothingToSayNamesWhatWouldFeedItRatherThanGoingBlank()
    {
        var phone = Phone(new CompanionHelperRequest(
            HelperInputs.Nothing, [HelperGoal.LevelUp], [], Tradeskills.All.Count));

        Assert.Empty(phone.Answers);
        Assert.Null(phone.Empty);
        var gap = Assert.Single(phone.Gaps);
        Assert.Equal(
            HelperPresentation.Gap(new GoalGap(HelperGoal.LevelUp, GoalGapReason.NoPlayHistory)),
            gap.Text);
        Assert.NotEmpty(phone.SourceNote);
        Assert.NotEmpty(phone.LevelNote);
    }

    /// <summary>
    /// **EVERY ONE OF THE NINE GOALS ANSWERS, EXPLAINS OR DEFERS — on an empty profile.**
    ///
    /// <para>This is the committed negative behind the empty-room branch, and it is worth
    /// more than exercising that branch would be. The whole-room empty state is DEFENSIVE:
    /// no goal in the enum can reach it, because D5's must-list rule is that a selected goal
    /// either produces rows, or names the store that would feed it, or says its engine is not
    /// built — "silence beats templates, and every goal has a DECIDED shape" (trap 73's
    /// pairing). So the honest assertion is that the state is unreachable, checked against the
    /// enum rather than a list (trap 30).</para>
    ///
    /// <para>The branch stays, on both surfaces, because "the engine returned nothing at all"
    /// is a state a future goal could produce and a blank panel is the one outcome that must
    /// never ship. If this test ever fails, a goal has gone quiet — and the branch is what
    /// stops that being a blank screen while somebody works out which one.</para>
    /// </summary>
    [Fact]
    public void NoGoalCanLeaveThisScreenWithNothingToSay()
    {
        foreach (var goal in Recommendations.All)
        {
            var phone = Phone(new CompanionHelperRequest(
                HelperInputs.Nothing, [goal], [], Tradeskills.All.Count));

            Assert.True(phone.Empty is null,
                $"{goal} produced no answer, no gap and no deferred note on an empty profile — "
                + "the phone fell back on the whole-room empty state, which means that goal "
                + "has gone silent rather than saying what it needs.");
            Assert.True(
                phone.Answers.Count > 0 || phone.Gaps.Count > 0 || phone.Deferred.Count > 0,
                $"{goal} drew nothing at all.");
        }

        // And the branch itself is wired: an engine that DID return nothing draws the two
        // sentences rather than a blank panel. Proven at the boundary the projection owns —
        // the condition, which is the same one the desktop room's own `_empty` uses.
        Assert.NotEmpty(HelperPresentation.Nothing.Heading);
        Assert.NotEmpty(HelperPresentation.Nothing.Explanation);
    }

    /// <summary>An answerable goal with nothing to say names what would feed it — and where
    /// that is a file the game writes, hands the command over as SELECTABLE TEXT off
    /// <see cref="GameCommands"/> (trap 35; David, 2026-08-20).</summary>
    [Theory]
    [InlineData(HelperGoal.WorkOnFaction, GameCommands.OutputfileFaction)]
    [InlineData(HelperGoal.UnlockRaces, GameCommands.OutputfileAchievements)]
    [InlineData(HelperGoal.FarmGear, GameCommands.OutputfileInventory)]
    public void AGapThatNeedsADumpShipsTheCommand(HelperGoal goal, string command)
    {
        var phone = Phone(Request(goals: goal));
        var carried = phone.Gaps.Concat(phone.Picks
                .Where(p => p.Prompt is not null)
                .Select(p => new CompanionHelperNote(p.Note, [], p.Prompt)))
            .Select(n => n.Prompt?.Command)
            .ToList();

        Assert.Contains(command, carried);
        // "Type this" without "on your PC" is the same defect one level down: the player is
        // holding the one device in the room that cannot run it.
        Assert.All(phone.Gaps.Where(g => g.Prompt is not null),
            g => Assert.Contains("PC", g.Prompt!.Lead, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A goal whose engine does not exist yet points at the room that answers its
    /// question today. A deferred goal that pointed nowhere is the rail's own forbidden
    /// shape, and it is forbidden here for the same reason.</summary>
    [Fact]
    public void ADeferredGoalPointsSomewhere()
    {
        var phone = Phone(Request(goals: HelperGoal.Achievements));
        var deferred = Assert.Single(phone.Deferred);

        Assert.Equal(HelperPresentation.NotAnsweredYet(HelperGoal.Achievements), deferred.Text);
        Assert.NotEmpty(deferred.Doors);
    }

    // ---- trap 32: the page spells none of this ---------------------------------------

    /// <summary>
    /// **Every sentence rides the WIRE.** Trap 32 is why: EQBuddy Mobile never re-fetches
    /// itself, so a literal in <c>index.html</c> can sit on an open phone for weeks after the
    /// PC has moved on — and this screen is nothing BUT sentences.
    ///
    /// <para>The negative is paired with a positive (trap 34): a scan that found nothing
    /// would report a perfectly clean page, so the page must also be shown to DRAW the
    /// fields.</para>
    /// </summary>
    [Fact]
    public void ThePageSpellsNoneOfTheHelpersWords()
    {
        var html = File.ReadAllText(Path.Combine(
            SrcRoot(), "EQBuddy.Companion", "Web", "index.html"));

        foreach (var sentence in new[]
                 {
                     HelperPresentation.RoomQuestion,
                     HelperPresentation.AnswersHeading,
                     HelperPresentation.GoalsHeading,
                     HelperPresentation.SourceNote,
                     HelperPresentation.GoalStripNote,
                     HelperPresentation.PicksOnPc,
                     HelperPresentation.DoorsOnPc,
                     HelperPresentation.Nothing.Heading,
                     HelperPresentation.MoneyPriceNote,
                     HelperPresentation.GearBaseClaimNote,
                     HelperPresentation.CatalogLabel,
                     // DRA-149 D3: the materials block's own note. It is the one new SENTENCE
                     // this slice sends, and the page must not have learned to say it (trap 32).
                     HelperPresentation.ProfessionsFarmNote,
                     // DRA-149 D4: the vendor block's source caption and the honest empty state
                     // for a trade no zone page names. Both ride the wire; neither is in the
                     // page.
                     HelperPresentation.MerchantsNote,
                     HelperPresentation.NoMerchantsFor(Tradeskill.Fletching),
                 })
            Assert.DoesNotContain(sentence, html, StringComparison.Ordinal);

        // …and it draws what it is sent, or every field above is decoration.
        //
        // **DRA-84 D5: this list is the half that goes stale** (trap 34). It was written when
        // the screen had one gear caption, and D2 and D4 each added another without adding a
        // row here — so `h.gearWhoWithheld` reached the wire, reached the fingerprint, passed
        // every parity assertion in this file, and was never drawn: five offers vanished off
        // the phone in silence, which is the exact failure trap 50 exists to refuse. The
        // staged shot found it. Every caption the page is sent is named below; a sixth one
        // added without a row here is the same bug again.
        foreach (var field in new[]
                 {
                     "renderHelper", "h.question", "h.picksLead", "h.answersHeading",
                     "h.sourceNote", "h.levelNote", "h.moneyNote", "h.gearBaseNote", "h.cap",
                     "h.gearWithheld", "h.gearBandRefused", "h.gearWhoWithheld",
                     // DRA-149 D2's caption and its doors, added in the SAME slice as the field
                     // — which is the whole of what D5's lesson was.
                     "h.unreadWorn", "h.unreadWornDoors",
                     // DRA-149 D3's three, added in the SAME slice as the fields — D5's lesson,
                     // and the reason this half of the list exists at all. Two of them are
                     // counts the desktop room draws as its own captions; the third is the
                     // block note above.
                     "h.materialNote", "h.materialBandRefused", "h.materialWhoWithheld",
                     // DRA-149 D4's three, added in the SAME slice as the fields. `m.lines` and
                     // `m.empty` are named as well as the block: a page that drew the heading
                     // and neither of those would pass on the block's row alone, which is
                     // exactly the shape D5 got caught by.
                     "h.merchants", "h.merchantNote", "h.merchantDoorNote",
                     "m.profession", "m.lines", "m.more", "m.empty",
                     // DRA-180 D2's two era captions, added in the SAME slice as the fields.
                     // The era gate runs BEFORE the band gate, so these name refusals the band
                     // sentence never mentions: a page that drew only the band ones would show
                     // a shorter list than the PC with nothing on screen explaining why.
                     "h.gearEraRefused", "h.materialEraRefused",
                     // DRA-180 D3's per-anchor answer and its cap, added in the SAME slice as
                     // the fields. BOTH are named rather than just the list: a page that looped
                     // the sentences and dropped the cap line would pass on one row while
                     // silently swallowing every anchor past the third, which is trap 50 wearing
                     // D5's clothes.
                     "h.anchorsAllRemoved", "h.anchorsNotNamed",
                     "h.doorsLead", "h.empty", "h.gaps", "h.deferred",
                 })
            Assert.Contains(field, html, StringComparison.Ordinal);
    }

    // ---- DRA-149 D2: the unread worn rows reach the phone -----------------------------

    /// <summary>
    /// **A worn row the PC could not read is said on the phone too, in the same words.**
    ///
    /// <para>The sentence is <see cref="HelperPresentation"/>'s, not the projection's, and the
    /// doors are one wiki search per NAMED item — capped the same way the caption is, so the
    /// list under it can never be longer than the list in it.</para>
    /// </summary>
    [Fact]
    public void TheUnreadWornSentenceAndItsDoorsRideTheWire()
    {
        IReadOnlyList<string> unread =
            ["Deterioriated Ancient Faydark Longbow +2", "Lute +1", "Shiny Brass Shield +6",
             "Mystery Pauldrons"];

        var phone = Phone(Request(
            Inputs() with { UnreadWorn = unread }, HelperGoal.FarmGear));

        Assert.Equal(HelperPresentation.UnreadWorn(unread), phone.UnreadWorn);
        Assert.Equal(HelperPresentation.UnreadWornNamed, phone.UnreadWornDoors.Count);
        Assert.All(phone.UnreadWornDoors, d =>
        {
            Assert.Equal("eqlwiki", d.Label);
            // The tip rides the ROW, because a phone has no pointer (trap 35).
            Assert.Contains("never fetches", d.Detail);
        });
        // Each door is about one of the NAMED items, in the caption's own order.
        Assert.Contains("Deterioriated Ancient Faydark Longbow +2", phone.UnreadWornDoors[0].Detail);
        Assert.Contains("Lute +1", phone.UnreadWornDoors[1].Detail);
    }

    // ---- DRA-180 D2: the era gate's refusals reach the phone ---------------------------

    /// <summary>
    /// **A place the PC refused for its ERA is refused on the phone too, in the same words.**
    ///
    /// <para>The sentence is <see cref="HelperPresentation"/>'s, not the projection's — the
    /// phone decides no word (DRA-71 D9). It matters more here than for the band caption,
    /// because the era gate runs FIRST: a phone carrying only the band sentence would draw a
    /// shorter list than the PC with nothing on screen accounting for the difference, which is
    /// the DRA-84 D5 miss wearing a new field.</para>
    /// </summary>
    [Fact]
    public void TheEraRefusalSentenceRidesTheWireInThePresentationsOwnWords()
    {
        var record = new ItemCatalog.Record
        {
            Name = "Blade of Carnage",
            StatsText = "Slot: PRIMARY\nAC: 20",
            Slots = ["PRIMARY"],
            Ac = 20,
            DropZones = ["Kael Drakkel"],
            DropMobs = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Kael Drakkel"] = ["a Kromrif general"],
            },
        };

        var inputs = Inputs() with
        {
            Worn = [new WornItem("Rusty Blade", "Rusty Blade", "PRIMARY",
                ItemStatsBlock.Parse(["Slot: PRIMARY", "AC: 2"]))],
            Items = new ItemCatalog([record]),
            GearIntent = GearIntent.UpgradeWorn,
            Eras = new ZoneEras(
                new Dictionary<string, ZoneEras.Banner>
                {
                    ["Kael Drakkel"] = new("Velious", "{{Velious Era}}"),
                },
                new Dictionary<string, string>()),
            World = "Classic",
        };

        var set = Recommendations.Rank(inputs, [HelperGoal.FarmGear]);
        Assert.NotEmpty(set.GearEraRefusals);

        var phone = Phone(Request(inputs, HelperGoal.FarmGear));

        Assert.Equal(
            HelperPresentation.EraRefused(
                set.GearEraRefusals, HelperPresentation.BandRefusedUpgrades),
            phone.GearEraRefused);
        // The words themselves, so a producer that started answering "" could not pass by
        // agreeing with itself.
        Assert.Contains("Kael Drakkel (Velious)", phone.GearEraRefused);
        Assert.Contains("Classic", phone.GearEraRefused);
    }

    /// <summary>The committed negative: a character whose places are all in reach is sent no
    /// era sentence at all, so the caption can never appear over a list the gate refused
    /// nothing from.</summary>
    [Fact]
    public void APhoneWithNothingEraRefusedIsSentNoEraSentence()
    {
        var phone = Phone(Request(Inputs(), HelperGoal.FarmGear));

        Assert.Empty(phone.GearEraRefused);
        Assert.Empty(phone.MaterialEraRefused);
    }

    /// <summary>Nothing unread, nothing sent — the committed negative, so a phone with nothing
    /// wrong in its dump does not draw an empty caption and a row of doors pointing at
    /// nothing.</summary>
    [Fact]
    public void APhoneWithNothingUnreadIsSentNeitherSentenceNorDoors()
    {
        var phone = Phone(Request(Inputs(), HelperGoal.FarmGear));

        Assert.Empty(phone.UnreadWorn);
        Assert.Empty(phone.UnreadWornDoors);
    }

    /// <summary>The unread sentence is in the push key: it NAMES its items, so a new dump that
    /// changes which of them EQBuddy cannot read moves nothing else on this screen (trap
    /// 72).</summary>
    [Fact]
    public void AChangedUnreadListWakesThePairedDevice()
    {
        string Print(HelperInputs inputs) => CompanionProjection.SectionFingerprints(
            CompanionProjection.Build(
                new CompanionInputs
                {
                    Character = "Dranak", AppVersion = "2.0.0",
                    Offered = CompanionSurfaces.All,
                    Helper = Request(inputs, HelperGoal.FarmGear),
                },
                DateTime.Now))[CompanionSurfaces.Helper];

        var clean = Inputs();
        Assert.NotEqual(
            Print(clean),
            Print(clean with { UnreadWorn = ["Deterioriated Ancient Faydark Longbow +2"] }));
        // A SWAP, which a count would not see.
        Assert.NotEqual(
            Print(clean with { UnreadWorn = ["Lute +1"] }),
            Print(clean with { UnreadWorn = ["Mystery Pauldrons"] }));
    }

    // ---- DRA-149 D4: the vendor half reaches the phone --------------------------------

    /// <summary>
    /// **The phone lists the SAME shops, in the same words, for the same eight professions.**
    ///
    /// <para>The projection chooses nothing: every line is
    /// <c>HelperPresentation.MerchantsShown</c>'s, which is the desktop room's own call, and the
    /// professions are <c>TradeskillPickStore.ListedFrom</c>'s — so "picked nothing" resolves to
    /// the same eight rows on both surfaces rather than to a second reading of what empty
    /// means.</para>
    /// </summary>
    [Fact]
    public void TheVendorLinesRideTheWireForEveryListedProfession()
    {
        var phone = Phone(Request(Inputs(), HelperGoal.FarmMaterials));

        Assert.Equal(Tradeskills.All.Count, phone.Merchants.Count);
        Assert.Equal(HelperPresentation.MerchantsNote, phone.MerchantNote);
        // The door is INTENT, said once (trap 35): no link, and the sentence that says what is
        // behind it.
        Assert.Contains("never fetches it for you", phone.MerchantDoorNote);

        foreach (var skill in Enum.GetValues<Tradeskill>())
        {
            var block = phone.Merchants.Single(m => m.Profession == Tradeskills.For(skill).Name);
            var shown = HelperPresentation.MerchantsShown(ZoneMerchants.Default, skill);

            Assert.Equal([.. shown.Select(HelperPresentation.MerchantRow)], block.Lines);
            // The two states are never both set: a block either has lines or says why it has
            // none, and the page draws whichever is there rather than deciding which case it is.
            Assert.True(block.Lines.Count == 0 ^ block.Empty.Length == 0);
        }
    }

    /// <summary>The committed negative: a player who picked only Level Up gets no vendor block,
    /// and — the part that is easy to miss — no caption over it either. A source note printed
    /// above nothing is the disclosure-line rule broken one block along.</summary>
    [Fact]
    public void APhoneThatDidNotPickMaterialsIsSentNoVendorBlockAndNoCaptionForIt()
    {
        var phone = Phone(Request(Inputs(), HelperGoal.LevelUp));

        Assert.Empty(phone.Merchants);
        Assert.Empty(phone.MerchantNote);
        Assert.Empty(phone.MerchantDoorNote);
    }

    /// <summary>A profession SWAP moves the push key. It is trap 72's own shape: one pick out
    /// and one in leaves every count on this screen unmoved, so a fingerprint that folded counts
    /// would leave the phone drawing the trade the player just deselected.</summary>
    [Fact]
    public void ASwappedProfessionPickWakesThePairedDevice()
    {
        string Print(params Tradeskill[] professions) => CompanionProjection.SectionFingerprints(
            CompanionProjection.Build(
                new CompanionInputs
                {
                    Character = "Dranak", AppVersion = "2.0.0",
                    Offered = CompanionSurfaces.All,
                    Helper = new CompanionHelperRequest(
                        Inputs(), [HelperGoal.FarmMaterials], professions, Tradeskills.All.Count),
                },
                DateTime.Now))[CompanionSurfaces.Helper];

        Assert.NotEqual(Print(Tradeskill.Jewelcrafting), Print(Tradeskill.Pottery));
        Assert.NotEqual(Print(Tradeskill.Jewelcrafting), Print());
    }

    // ---- trap 72 / trap 8: the push gate ---------------------------------------------

    /// <summary>
    /// **A re-rank that swaps two answers must wake the device** (trap 72 — the Quests tab
    /// drew the moment before for a whole session because its signature carried everything
    /// except the store the feature wrote). Every sentence is in the key, because every one
    /// of them is an engine's output and a count would not move on a swap.
    /// </summary>
    [Fact]
    public void AChangedAnswerMovesTheSectionFingerprint()
    {
        string Print(CompanionHelperRequest r) => CompanionProjection.SectionFingerprints(
            CompanionProjection.Build(
                new CompanionInputs
                {
                    Character = "Dranak", AppVersion = "2.0.0",
                    Offered = CompanionSurfaces.All, Helper = r,
                },
                DateTime.Now))[CompanionSurfaces.Helper];

        var one = Request(goals: HelperGoal.LevelUp);
        Assert.Equal(Print(one), Print(one));

        // A different goal set is a different ranking, and a different level is a different
        // disclosure line over the same one.
        Assert.NotEqual(Print(one), Print(Request(goals: HelperGoal.MakeMoney)));
        Assert.NotEqual(
            Print(one),
            Print(Request(Inputs(new ResolvedLevel(30, LevelSource.Stated, DateTime.Now)),
                HelperGoal.LevelUp)));
    }

    /// <summary>And NOTHING in it ticks on a clock (trap 8): a value that drifted every
    /// second would wake every paired phone once a second forever. The Helper carries no
    /// countdown, no age and no "x ago", which is what makes a whole-string key safe here.</summary>
    [Fact]
    public void NothingInTheHelperKeyDriftsOnTheClock()
    {
        var request = Request(goals: HelperGoal.LevelUp);

        string Print(DateTime now) => CompanionProjection.SectionFingerprints(
            CompanionProjection.Build(
                new CompanionInputs
                {
                    Character = "Dranak", AppVersion = "2.0.0",
                    Offered = CompanionSurfaces.All, Helper = request,
                },
                now))[CompanionSurfaces.Helper];

        Assert.Equal(
            Print(new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Local)),
            Print(new DateTime(2026, 9, 14, 19, 43, 0, DateTimeKind.Local)));
    }

    // ---- the gate ---------------------------------------------------------------------

    /// <summary>A withheld surface does not EXIST in memory to leak, let alone send — the
    /// projection's own contract, and the Helper is the section carrying the most of a
    /// player's history.</summary>
    [Fact]
    public void AGatedHelperIsNeverBuilt()
    {
        var snap = CompanionProjection.Build(
            new CompanionInputs
            {
                Character = "Dranak", AppVersion = "2.0.0",
                Offered = [CompanionSurfaces.Session],
                Helper = Request(goals: HelperGoal.LevelUp),
            },
            DateTime.Now);

        Assert.Null(snap.Helper);
        Assert.DoesNotContain(CompanionSurfaces.Helper,
            CompanionProjection.SectionFingerprints(snap).Keys);
    }

    /// <summary>A host that gathered NOTHING is not the same claim as a player with no
    /// history, and the projection keeps them apart: no bundle means no section, rather than
    /// a screen built from <see cref="HelperInputs.Nothing"/> saying the player has never
    /// played.</summary>
    [Fact]
    public void NoBundleIsNoSectionRatherThanAnEmptyOne()
    {
        var snap = CompanionProjection.Build(
            new CompanionInputs
            {
                Character = "Dranak", AppVersion = "2.0.0",
                Offered = CompanionSurfaces.All, Helper = null,
            },
            DateTime.Now);

        Assert.Null(snap.Helper);
    }

    /// <summary>Subscription filtering reaches the new section like every other one — a
    /// device that did not ask for the Helper is not sent a ranking of its owner's play.</summary>
    [Fact]
    public void ADeviceThatDidNotAskForItIsNotSentIt()
    {
        var snap = CompanionProjection.Build(
            new CompanionInputs
            {
                Character = "Dranak", AppVersion = "2.0.0",
                Offered = CompanionSurfaces.All,
                Helper = Request(goals: HelperGoal.LevelUp),
            },
            DateTime.Now);

        Assert.NotNull(snap.Helper);
        Assert.Null(snap.ForSubscription([CompanionSurfaces.Session]).Helper);
        Assert.NotNull(snap.ForSubscription([CompanionSurfaces.Helper]).Helper);
    }

    private static string SrcRoot() =>
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src");
}
