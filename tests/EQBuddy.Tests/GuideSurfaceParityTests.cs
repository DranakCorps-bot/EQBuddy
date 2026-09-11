using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The phone and the desktop show the SAME guide, because they call the same projection
/// from the same point — not because two feature lists were kept level by hand
/// (David, 2026-08-18; #184's lesson, and #210's from the other direction).
///
/// <para>Every test here builds BOTH surfaces from one <see cref="AppSettings"/> and one
/// ledger and compares them. That is the only shape that catches the failure this guards:
/// one screen guided and the other still classic, which is exactly what "porting a feature
/// to the phone" produces.</para>
/// </summary>
public sealed class GuideSurfaceParityTests : IDisposable
{
    private const string Dranak = "dranak_freeport";
    private const string ClassName = "Warrior";
    private const string Reward = "Runed Wind Amulet";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"guide-parity-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    private static string RewardKey => QuestChecklistLayout.RewardKey(ClassName, Reward);

    /// <summary>The Warrior's two real drops, plus an unguided class so "a class with no
    /// guide is unchanged on the phone too" has something to be about.</summary>
    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.SkyQuestChecklist.AddRange(
        [
            new SkyQuestChecklistItem
            {
                Id = "sky-198", ClassName = ClassName, Reward = Reward,
                Npc = "Torgon Blademaster", QuestItem = "Stone Amulet",
                Source = "Isle 4: Keeper of Souls",
            },
            new SkyQuestChecklistItem
            {
                Id = "sky-199", ClassName = ClassName, Reward = Reward,
                Npc = "Torgon Blademaster", QuestItem = "Wind Rune Azia",
                Source = "Trash mobs",
            },
            // A reward that still has a STUB in it. The Warrior's rune stubs became authored
            // on 2026-09-09 (DRA-44 — the zone page answers where wind runes drop), so the
            // stub-parity test needs a group that still has one: the Efreeti Statuette, whose
            // page gives an isle and names no mob.
            new SkyQuestChecklistItem
            {
                Id = "sky-220", ClassName = "Wizard", Reward = "Solidate Mithril Ring",
                Npc = "Wizard Schrock", QuestItem = "Box of Winds", Source = "Isle 6: Bazzt Zzzt",
            },
            new SkyQuestChecklistItem
            {
                Id = "sky-221", ClassName = "Wizard", Reward = "Solidate Mithril Ring",
                Npc = "Wizard Schrock", QuestItem = "Efreeti Statuette",
                Source = "Isle four - griffons and pegasus",
            },
            new SkyQuestChecklistItem
            {
                Id = "sky-222", ClassName = "Wizard", Reward = "Solidate Mithril Ring",
                Npc = "Wizard Schrock", QuestItem = "Wind Rune Izah", Source = "Trash mobs",
            },
            // An UNGUIDED reward, so lock 5 still has something to be about. Every real
            // class and reward is authored as of 2026-09-09 (D7), so the fixture invents one
            // no guide can ever claim rather than picking a class that is merely unauthored
            // today and would silently stop testing anything the week it is written.
            new SkyQuestChecklistItem
            {
                Id = "sky-007", ClassName = "Bard", Reward = "A Reward Nobody Authored",
                Npc = "Cilin Spellsinger", QuestItem = "Woolen Mask", Source = "Isle 3",
            },
        ]);
        return s;
    }

    /// <summary>What the DESKTOP draws: the layout, through the projection, exactly as
    /// QuestsView.RenderChecklist does it.</summary>
    private IReadOnlyList<QuestChecklistGroup> Desktop(AppSettings s, QuestLedgerStore ledger) =>
        GuideChecklistProjection.Apply(
            QuestChecklistLayout.Sky(s.SkyQuestChecklist, s.SkyQuestCompleted),
            GuideCatalog.Default, s, ledger, Dranak);

    /// <summary>What the PHONE draws, through the real snapshot builder.</summary>
    private CompanionChecklistSection Phone(AppSettings s, QuestLedgerStore ledger) =>
        CompanionProjection.Build(
            new CompanionInputs
            {
                Settings = s,
                Character = "Dranak",
                AppVersion = "2.0.0",
                Offered = CompanionSurfaces.All,
                Quests = new CompanionQuestRequest { Ledger = ledger, CharacterKey = Dranak },
            },
            new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Local)).Quests!.Sky;

    private static CompanionChecklistGroup PhoneGroup(CompanionChecklistSection sky, string heading) =>
        sky.Groups.Single(g => g.Heading == heading && g.Class is not null);

    // The shipped catalog's Warrior seed is the fixture — if authoring renames it, these
    // fail loudly rather than quietly testing nothing.
    private static Guide WarriorSeed =>
        GuideChecklistProjection.GuideFor(GuideCatalog.Default, RewardKey)
        ?? throw new InvalidOperationException(
            $"no shipped guide claims {RewardKey} — the parity fixture needs one");

    [Fact]
    public void ThePhoneShowsTheGuideRowsTheDesktopShows()
    {
        var settings = Settings();
        var ledger = Store();

        var desktop = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        var phone = PhoneGroup(Phone(settings, ledger), desktop.Heading);

        Assert.NotEmpty(desktop.Rows);
        Assert.Equal(desktop.Rows.Select(r => r.Title), phone.Rows.Select(r => r.Text));
        Assert.Equal(desktop.Rows.Select(r => r.Id), phone.Rows.Select(r => r.Id));
        Assert.Equal(desktop.Rows.Select(r => r.Acquired), phone.Rows.Select(r => r.Done));
    }

    /// <summary>
    /// The reward item's stats block reaches BOTH screens, with the SAME words — and on the
    /// phone it arrives whether the quest is folded or open, because there the PAGE decides
    /// when to show it.
    ///
    /// <para>Trap 35, and the shape of its answer here. The desktop hangs the block on a
    /// hover, which costs a folded list nothing. A phone cannot hover at all (David,
    /// 2026-09-10: *"mouse over on mobile won't work well"*), and drawing eight lines of item
    /// stats under every heading would bury exactly the folded list folding exists to give —
    /// so the reward LINE became the control and a tap opens the block in place. That is a
    /// page decision, so the payload carries the block unconditionally and this asserts the
    /// PAYLOAD; which blocks a reader has opened is a fact about their device, not about the
    /// character, and never reaches the profile.</para>
    ///
    /// <para><b>Independent of the fold on purpose.</b> "What does this pay" and "show me the
    /// steps" are different questions, and the Founder chose to let the phone answer the
    /// first without committing to the second.</para></summary>
    [Fact]
    public void TheRewardsStatsBlockReachesBothScreensFoldedOrOpen()
    {
        var settings = Settings();
        var ledger = Store();

        var folded = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        var phoneFolded = PhoneGroup(Phone(settings, ledger), folded.Heading);

        // The block is real, and it is the ITEM's own words rather than a sentence about it.
        Assert.True(folded.Collapsed);
        Assert.Contains("Slot:", folded.RewardCard, StringComparison.Ordinal);
        Assert.DoesNotContain("Rewards the", folded.RewardCard, StringComparison.Ordinal);
        // ...and what it COSTS is on the end, which is the quest's half of the answer.
        Assert.Contains("Needs Stone Amulet", folded.RewardCard, StringComparison.Ordinal);

        // Same words on the phone, and the one-LINE summary is still there as the control
        // the reader taps — the two are different facts and both travel.
        Assert.Equal(folded.RewardCard, phoneFolded.RewardCard);
        Assert.Equal(folded.RewardSummary, phoneFolded.Reward);

        settings.GuideExpanded.Add(RewardKey);

        var open = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        var phoneOpen = PhoneGroup(Phone(settings, ledger), open.Heading);

        Assert.False(open.Collapsed);
        // NOTHING about the block moved when the steps opened. That is the independence the
        // Founder asked for, stated as behaviour rather than as a comment.
        Assert.Equal(folded.RewardCard, open.RewardCard);
        Assert.Equal(phoneFolded.RewardCard, phoneOpen.RewardCard);
    }

    [Fact]
    public void TheGuideCaptionReadsTheSameOnBothScreens()
    {
        var settings = Settings();
        var ledger = Store();

        var desktop = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        var phone = PhoneGroup(Phone(settings, ledger), desktop.Heading);

        // With nothing to add beyond the heading's own count, the caption draws on NEITHER
        // screen — parity of an absence is the half that would rot silently, because the
        // phone composes its note from the caption and would happily keep saying "Guide ·
        // 1 of 2" under a heading that already said it.
        Assert.Equal("", desktop.GuideCaption);
        Assert.Equal(desktop.Note, phone.Note);

        // Strike a step out and the caption earns its line again — same words, both screens.
        var row = desktop.Rows.First(r => !r.IsTurnIn);
        Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Sky,
                CompanionActions.SkipVerb + row.Id, Done: true)));

        var withSkip = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        var phoneWithSkip = PhoneGroup(Phone(settings, ledger), withSkip.Heading);
        Assert.Contains("1 skipped", withSkip.GuideCaption, StringComparison.Ordinal);
        Assert.StartsWith(withSkip.GuideCaption, phoneWithSkip.Note!, StringComparison.Ordinal);
    }

    [Fact]
    public void AStubNoteReadsTheSameOnBothScreens()
    {
        var settings = Settings();
        var ledger = Store();

        var stubbedKey = QuestChecklistLayout.RewardKey("Wizard", "Solidate Mithril Ring");
        var desktop = Desktop(settings, ledger).Single(g => g.CompletionKey == stubbedKey);
        var phone = PhoneGroup(Phone(settings, ledger), desktop.Heading);

        var stubbed = desktop.Rows.Where(r => r.StubNote.Length > 0).ToList();
        Assert.Single(stubbed);
        foreach (var row in stubbed)
        {
            var mirror = phone.Rows.Single(r => r.Id == row.Id);
            Assert.Equal(GuidePresentation.StubLead + " " + row.StubNote, mirror.Stub);
        }
        // And an authored row carries none on either screen.
        foreach (var row in desktop.Rows.Where(r => r.StubNote.Length == 0))
            Assert.Null(phone.Rows.Single(r => r.Id == row.Id).Stub);
    }

    [Fact]
    public void EveryGuideRowOffersTheSameShareBackDoorOnThePhone()
    {
        var settings = Settings();
        var ledger = Store();
        var guide = WarriorSeed;

        var phone = PhoneGroup(Phone(settings, ledger), $"{ClassName} · {Reward}");

        foreach (var objective in guide.AllObjectives)
        {
            var row = phone.Rows.Single(r =>
                r.Id == GuideChecklistProjection.RowId(guide.Id, objective.Id));
            Assert.Equal(GuidePresentation.ImproveUrl(guide, objective), row.Improve);
        }
    }

    [Fact]
    public void AGuideStepTickedOnEitherScreenIsTickedOnBoth()
    {
        var settings = Settings();
        var ledger = Store();
        var guide = WarriorSeed;
        var step = guide.AllObjectives.First(o => o.RewardKey.Length == 0);
        var rowId = GuideChecklistProjection.RowId(guide.Id, step.Id);

        // The PHONE taps it, through the real action path.
        Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Sky, rowId, Done: true)));

        // The DESKTOP sees it, from the same stores.
        var desktop = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        Assert.True(desktop.Rows.Single(r => r.Id == rowId).Acquired);
        Assert.True(PhoneGroup(Phone(settings, ledger), desktop.Heading)
            .Rows.Single(r => r.Id == rowId).Done);

        // And back the other way.
        Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Sky, rowId, Done: false)));
        Assert.False(Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey)
            .Rows.Single(r => r.Id == rowId).Acquired);
    }

    /// <summary>A tap that says what is already true changes nothing and reports so — the
    /// host saves and repaints on that answer, and a repaint per tick would be the cost.</summary>
    [Fact]
    public void ATapThatAgreesWithTheStoreIsNotAnEdit()
    {
        var settings = Settings();
        var ledger = Store();
        var guide = WarriorSeed;
        var rowId = GuideChecklistProjection.RowId(
            guide.Id, guide.AllObjectives.First(o => o.RewardKey.Length == 0).Id);

        Assert.False(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Sky, rowId, Done: false)));
    }

    [Fact]
    public void AGuideTapWithNoLedgerIsRefusedRatherThanWrittenSomewhereElse()
    {
        var settings = Settings();
        var guide = WarriorSeed;
        var rowId = GuideChecklistProjection.RowId(
            guide.Id, guide.AllObjectives.First(o => o.RewardKey.Length == 0).Id);

        Assert.False(CompanionActions.Apply(settings,
            new CompanionAction(CompanionSurfaces.Sky, rowId, Done: true)));
    }

    /// <summary>The card is ONE answer to "what is next", carried on the group — so the two
    /// screens cannot run the selection rule separately and disagree.</summary>
    [Fact]
    public void ThePhoneNamesTheSameNextStepAsTheDesktop()
    {
        var settings = Settings();
        var ledger = Store();

        var desktop = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        var phone = PhoneGroup(Phone(settings, ledger), desktop.Heading);

        var card = desktop.GuideCard!;
        Assert.NotEqual("", card.RowId);
        Assert.Equal(card.RowId, phone.Card!.RowId);
        Assert.Equal(card.Instruction, phone.Card.Instruction);
        Assert.Equal(card.Why, phone.Card.Why);
        Assert.Equal(GuidePresentation.NextLead, phone.Card.Lead);

        // And it MOVES together: tick the named step and both name the next one.
        Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Sky, card.RowId, Done: true)));

        var movedDesktop = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        var movedPhone = PhoneGroup(Phone(settings, ledger), desktop.Heading);
        Assert.NotEqual(card.RowId, movedDesktop.GuideCard!.RowId);
        Assert.Equal(movedDesktop.GuideCard.RowId, movedPhone.Card!.RowId);
    }

    /// <summary>A skip made on either screen is the same skip — it goes through the router
    /// into the guide ledger, which is the only store that holds "not doing this one".</summary>
    [Fact]
    public void ASkipOnEitherScreenIsTheSameSkip()
    {
        var settings = Settings();
        var ledger = Store();
        var first = Desktop(settings, ledger)
            .Single(g => g.CompletionKey == RewardKey).GuideCard!.RowId;

        // The PHONE strikes it out, through the real action path.
        Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Sky, CompanionActions.SkipVerb + first, Done: true)));

        var desktop = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        Assert.True(desktop.Rows.Single(r => r.Id == first).IsSkipped);
        // The card moved past it rather than sitting on a struck-out step.
        Assert.NotEqual(first, desktop.GuideCard!.RowId);

        var phone = PhoneGroup(Phone(settings, ledger), desktop.Heading);
        Assert.True(phone.Rows.Single(r => r.Id == first).Skipped);
        Assert.Equal(desktop.GuideCard.RowId, phone.Card!.RowId);

        // And back: taking the skip off restores it as the next step.
        Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Sky, CompanionActions.SkipVerb + first, Done: false)));
        var restored = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        Assert.False(restored.Rows.Single(r => r.Id == first).IsSkipped);
        Assert.Equal(first, restored.GuideCard!.RowId);
    }

    /// <summary>A guide with nothing left says which of the two finished states it is in, and
    /// offers no verbs — a Done button with nothing to do is a silent no-op wearing an
    /// affordance.</summary>
    [Fact]
    public void AFinishedGuidesCardNamesTheStateAndCarriesNoVerbs()
    {
        var settings = Settings();
        var ledger = Store();

        foreach (var row in Desktop(settings, ledger)
                     .Single(g => g.CompletionKey == RewardKey).Rows)
            CompanionActions.Apply(settings, ledger, Dranak,
                new CompanionAction(CompanionSurfaces.Sky, CompanionActions.SkipVerb + row.Id, Done: true));

        var card = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey).GuideCard!;
        Assert.Equal("", card.RowId);
        Assert.Equal(GuidePresentation.AllSkipped, card.Instruction);
    }

    /// <summary>
    /// A quest with nothing left but skips says so ON THE HEADING, not only on the card
    /// (Bevel, 2026-09-09, finding 2).
    ///
    /// <para>The heading's vocabulary had no word for "the player put this down", so it said
    /// "in progress" while the card said "Every step left is skipped" — the surface
    /// contradicting itself. Folding made it sharper: the heading is now often the only thing
    /// on screen for a quest.</para></summary>
    [Fact]
    public void AQuestWithNothingLeftButSkipsSaysSoOnTheHeadingAndNotOnlyOnTheCard()
    {
        var settings = Settings();
        var ledger = Store();
        var group = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);

        // Half-done, half-skipped: work remains, so "in progress" is still the true word.
        var rows = group.Rows.Where(r => !r.IsTurnIn).ToList();
        Assert.True(rows.Count >= 2);
        Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Sky, rows[0].Id, Done: true)));
        Assert.Equal("in progress",
            Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey).Note);

        // Strike the rest out and nothing is left that is neither done nor put down.
        foreach (var row in rows.Skip(1))
            Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
                new CompanionAction(CompanionSurfaces.Sky,
                    CompanionActions.SkipVerb + row.Id, Done: true)));

        var setAside = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        Assert.Equal("set aside", setAside.Note);
        // ...and the card is saying the same thing rather than a different one. On REAL Sky
        // data that sentence is the blocked one, not "every step left is skipped": the
        // turn-in is untouched and merely gated on the pieces that were struck out, which is
        // exactly the case Fable's #491 defect 1 was about. The heading says the quest was
        // put down; the card names the skip to take back to pick it up again.
        Assert.StartsWith(GuidePresentation.BlockedBySkipLead,
            setAside.GuideCard!.Instruction, StringComparison.Ordinal);
        // Named by the OBJECTIVE's title, the way "after:" and "Before leaving" name a step,
        // rather than by the instruction the row draws — one cross-referencing vocabulary.
        var blocked = GuideCatalog.Default.Guides.Single(g => g.Id == setAside.GuideId)
            .AllObjectives.Single(o => setAside.Rows[1].Id.EndsWith(o.Id, StringComparison.Ordinal));
        Assert.Contains(blocked.Title, setAside.GuideCard!.Instruction, StringComparison.Ordinal);

        // Taking one skip back puts the work — and the word — back.
        Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Sky,
                CompanionActions.SkipVerb + rows[1].Id, Done: false)));
        Assert.Equal("in progress",
            Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey).Note);
    }

    [Fact]
    public void AClassWithNoGuideIsUnchangedOnThePhoneToo()
    {
        var settings = Settings();
        var ledger = Store();

        var desktop = Desktop(settings, ledger).Single(g => g.ClassName == "Bard");
        var phone = PhoneGroup(Phone(settings, ledger), desktop.Heading);

        // Reference equality is the strong half and it lives in GuideChecklistProjectionTests;
        // this is the phone saying the same thing.
        Assert.Equal("", desktop.GuideId);
        Assert.Equal(["sky-007"], phone.Rows.Select(r => r.Id));
        Assert.Equal(["Woolen Mask"], phone.Rows.Select(r => r.Text));
        Assert.All(phone.Rows, r => Assert.Null(r.Stub));
        Assert.All(phone.Rows, r => Assert.Null(r.Improve));
    }

    /// <summary>Before a character is known there is no ledger, and the phone must show the
    /// classic checklist rather than a guide whose ticks it could not record.</summary>
    [Fact]
    public void WithNoLedgerThePhoneShowsTheClassicChecklist()
    {
        var settings = Settings();

        var sky = CompanionProjection.Build(
            new CompanionInputs
            {
                Settings = settings,
                Character = "Dranak",
                AppVersion = "2.0.0",
                Offered = CompanionSurfaces.All,
                Quests = new CompanionQuestRequest(),
            },
            new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Local)).Quests!.Sky;

        var warrior = PhoneGroup(sky, $"{ClassName} · {Reward}");
        Assert.Equal(["sky-198", "sky-199"], warrior.Rows.Select(r => r.Id).Order());
    }

    /// <summary>The item-backed rule, end to end and across both screens: the loot auto-tick
    /// writes the checklist box, and the guide row lights on the phone AND the desktop
    /// without either of them holding a second copy of that fact (trap 4).</summary>
    [Fact]
    public void TheLootAutoTicksBoxLightsTheGuideStepOnBothScreens()
    {
        var settings = Settings();
        var ledger = Store();
        var guide = WarriorSeed;

        settings.SkyQuestChecklist.Single(i => i.Id == "sky-198").Acquired = true;

        var desktop = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        var lit = desktop.Rows.Where(r => r.Acquired).ToList();
        Assert.Single(lit);

        var phone = PhoneGroup(Phone(settings, ledger), desktop.Heading);
        Assert.Equal(lit.Select(r => r.Id), phone.Rows.Where(r => r.Done).Select(r => r.Id));

        // The guide ledger holds nothing — the box is the only store for it.
        Assert.Empty(ledger.GuideProgressFor(Dranak, guide.Id).DoneObjectiveIds);
    }

    // ---- the GENERAL tab's normal-quest guides (DRA-46 / Delivery 2 N2) ----

    /// <summary>The same real shipped quest <c>QuestGuideProjectionTests</c> surveys for —
    /// three Transcribed Checklist bullets, two Collect stubs, an Authored hand-in.</summary>
    private const string FixtureQuest = "Red Dragonscale Armor Quest";

    private static readonly QuestCatalog Catalog = QuestCatalog.LoadEmbedded();

    private static QuestEntry FixtureEntry =>
        Catalog.Quests.First(q => q.Name.Equals(FixtureQuest, StringComparison.OrdinalIgnoreCase));

    /// <summary>What the DESKTOP's detail pane draws, exactly as QuestsView.BuildDetail
    /// does it.</summary>
    private QuestChecklistGroup DesktopQuestGuide(AppSettings s, QuestLedgerStore ledger) =>
        GuideChecklistProjection.ForQuest(FixtureEntry, GuideCatalog.Default, s, ledger, Dranak)!;

    /// <summary>What the PHONE's quest card draws, through the real snapshot builder — which
    /// means through the real matcher, so the quest has to be in "mine" to arrive. Holding one
    /// of its turn-in items is what puts it there, which is also how a player meets it.</summary>
    private CompanionQuestsSection PhoneQuests(AppSettings s, QuestLedgerStore ledger) =>
        CompanionProjection.Build(
            new CompanionInputs
            {
                Settings = s,
                Character = "Dranak",
                AppVersion = "2.0.0",
                Offered = CompanionSurfaces.All,
                Quests = new CompanionQuestRequest
                {
                    Ledger = ledger,
                    CharacterKey = Dranak,
                    Catalog = Catalog,
                    Owned = ledger.For(Dranak),
                    Tracked = new HashSet<string>(
                        ledger.TrackedFor(Dranak), StringComparer.OrdinalIgnoreCase),
                },
            },
            new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Local)).Quests!;

    private CompanionChecklistGroup PhoneQuestGuide(AppSettings s, QuestLedgerStore ledger) =>
        PhoneQuests(s, ledger).Guides.Single(g => g.Title == FixtureQuest);

    /// <summary>
    /// The phone's quest card shows the WALKTHROUGH ROWS the desktop's detail pane shows —
    /// same ids, same titles, same ticks — for a NORMAL quest.
    ///
    /// <para>This is the parity assertion N2 exists to make. The failure it guards is the one
    /// #184 and #210 both cost us from opposite directions: one screen guided and the other
    /// still a flat item list. Both sides here come from
    /// <c>GuideChecklistProjection.ForQuest</c>, so they cannot diverge without the shared
    /// module itself changing.</para></summary>
    [Fact]
    public void ThePhonesQuestCardShowsTheWalkthroughTheDetailPaneShows()
    {
        var settings = new AppSettings();
        var ledger = Store();
        ledger.SetManual(Dranak, "Red Dragon Scales", 1);

        var desktop = GuideChecklistProjection
            .WalkthroughRows(DesktopQuestGuide(settings, ledger)).ToList();
        var phone = PhoneQuestGuide(settings, ledger);

        Assert.NotEmpty(desktop);
        Assert.Equal(desktop.Select(r => r.Id), phone.Rows.Select(r => r.Id));
        Assert.Equal(desktop.Select(r => r.Title), phone.Rows.Select(r => r.Text));
        Assert.Equal(desktop.Select(r => r.Acquired), phone.Rows.Select(r => r.Done));
    }

    /// <summary>
    /// <b>And the Collect steps are on NEITHER as a guide row</b> — they are the quest's own
    /// turn-in item rows, which the pane and the card have both drawn since the tracker
    /// existed. A guide row for them would be one fact shown twice, once clickable and once
    /// refused (trap 4).
    ///
    /// <para>Asserted on the PAYLOAD rather than on the page, because the split is decided
    /// desktop-side on purpose: a page-side rule can sit on an open phone for weeks after the
    /// PC has moved on (trap 32).</para></summary>
    [Fact]
    public void TheCollectStepsReachNeitherScreenAsAGuideRow()
    {
        var settings = new AppSettings();
        var ledger = Store();
        ledger.SetManual(Dranak, "Red Dragon Scales", 1);

        var group = DesktopQuestGuide(settings, ledger);
        var itemRows = group.Rows.Where(r => r.LedgerItemName.Length > 0).ToList();
        Assert.Equal(2, itemRows.Count);

        var phone = PhoneQuestGuide(settings, ledger);
        foreach (var row in itemRows)
            Assert.DoesNotContain(phone.Rows, r => r.Id == row.Id);
        // …and the quest is still on the phone's list with its items, which is where those
        // rows ARE — the card computes them from `owned` against the catalog it holds.
        Assert.Contains(FixtureQuest, PhoneQuests(settings, ledger).Mine);
        Assert.True(PhoneQuests(settings, ledger).Owned["Red Dragon Scales"] > 0);
    }

    /// <summary>The card, the fold key and the caption all arrive already worded, and the
    /// phone spells none of them. The fold key is the desktop's own, so the two surfaces fold
    /// the same thing by the same name even though only one persists it.</summary>
    [Fact]
    public void ThePhonesQuestGuideCarriesTheSameCardAndFoldKey()
    {
        var settings = new AppSettings();
        var ledger = Store();
        ledger.SetManual(Dranak, "Red Dragon Scales", 1);

        var group = DesktopQuestGuide(settings, ledger);
        var phone = PhoneQuestGuide(settings, ledger);

        Assert.Equal(GuideChecklistProjection.FoldKey(group), phone.Fold);
        Assert.Equal(group.Collapsed, phone.Collapsed);
        Assert.True(phone.Collapsed);   // folded by default, like Sky
        Assert.Equal(group.GuideCard!.RowId, phone.Card!.RowId);
        Assert.Equal(group.GuideCard.Instruction, phone.Card.Instruction);
        Assert.Equal(GuidePresentation.NextLead, phone.Card.Lead);
    }

    /// <summary>A tap on the phone goes through the ROUTER with this quest in hand, which is
    /// the same door the desktop's click uses — so ticking the hand-in on the phone marks the
    /// quest completed on the PC, and a tap on a Collect step writes nothing at all.</summary>
    [Fact]
    public void ATapOnAQuestGuideRowGoesThroughTheRouter()
    {
        var settings = new AppSettings();
        var ledger = Store();
        var guide = GuideCatalog.Default.Find("harvested-red-dragonscale-armor-quest")!;
        var handIn = guide.AllObjectives.First(o => o.ObjectiveType == "TurnIn");
        var collect = guide.AllObjectives.First(o => o.ObjectiveType == "Collect");

        Assert.True(CompanionActions.ApplyQuestGuide(settings, ledger, Dranak, Catalog,
            new CompanionAction(CompanionSurfaces.Quests,
                GuideChecklistProjection.RowId(guide.Id, handIn.Id), true)));
        Assert.Equal(1, ledger.CompletedFor(Dranak)[FixtureQuest]);

        // The bags are the answer for a Collect row; a tap on one is refused, changes
        // nothing, and therefore saves and repaints nothing.
        Assert.False(CompanionActions.ApplyQuestGuide(settings, ledger, Dranak, Catalog,
            new CompanionAction(CompanionSurfaces.Quests,
                GuideChecklistProjection.RowId(guide.Id, collect.Id), true)));
        Assert.Empty(ledger.GuideProgressFor(Dranak, guide.Id).DoneObjectiveIds);
    }

    /// <summary>
    /// A step moving MOVES THE QUESTS FINGERPRINT — trap 72, one surface on. Without the
    /// guides in that key, pressing Skip on a quest card would leave the phone drawing the
    /// card it was showing a moment before for the rest of the session, because nothing else
    /// in the key reads the guide ledger.
    ///
    /// <para>Prove-failed by construction: the two fingerprints are taken from the SAME
    /// settings and the SAME ledger with one skip between them, so an assertion that they
    /// differ can only pass if the skip is in the key.</para></summary>
    [Fact]
    public void ASkipOnAQuestGuideMovesTheQuestsFingerprint()
    {
        var settings = new AppSettings();
        var ledger = Store();
        ledger.SetManual(Dranak, "Red Dragon Scales", 1);
        var guide = GuideCatalog.Default.Find("harvested-red-dragonscale-armor-quest")!;
        var step = guide.AllObjectives.First(o => o.Authoring == GuideAuthoring.Transcribed);

        string Print() => CompanionProjection.SectionFingerprints(
            CompanionProjection.Build(
                new CompanionInputs
                {
                    Settings = settings,
                    Character = "Dranak",
                    AppVersion = "2.0.0",
                    Offered = CompanionSurfaces.All,
                    Quests = new CompanionQuestRequest
                    {
                        Ledger = ledger, CharacterKey = Dranak, Catalog = Catalog,
                        Owned = ledger.For(Dranak),
                    },
                },
                new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Local)))
            [CompanionSurfaces.Quests];

        var before = Print();
        GuideProgressRouter.SetSkipped(ledger, Dranak, guide.Id, step, true);
        Assert.NotEqual(before, Print());
    }
}
