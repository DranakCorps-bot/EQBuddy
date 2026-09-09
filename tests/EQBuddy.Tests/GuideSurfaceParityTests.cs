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
            new SkyQuestChecklistItem
            {
                Id = "sky-007", ClassName = "Bard", Reward = "Mask of Song",
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

    [Fact]
    public void TheGuideCaptionReadsTheSameOnBothScreens()
    {
        var settings = Settings();
        var ledger = Store();

        var desktop = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        var phone = PhoneGroup(Phone(settings, ledger), desktop.Heading);

        Assert.NotEqual("", desktop.GuideCaption);
        Assert.StartsWith(desktop.GuideCaption, phone.Note!, StringComparison.Ordinal);
    }

    [Fact]
    public void AStubNoteReadsTheSameOnBothScreens()
    {
        var settings = Settings();
        var ledger = Store();

        var desktop = Desktop(settings, ledger).Single(g => g.CompletionKey == RewardKey);
        var phone = PhoneGroup(Phone(settings, ledger), desktop.Heading);

        var stubbed = desktop.Rows.Where(r => r.StubNote.Length > 0).ToList();
        Assert.NotEmpty(stubbed);
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

    [Fact]
    public void AClassWithNoGuideIsUnchangedOnThePhoneToo()
    {
        var settings = Settings();
        var ledger = Store();

        var desktop = Desktop(settings, ledger).Single(g => g.ClassName == "Bard");
        var phone = PhoneGroup(Phone(settings, ledger), desktop.Heading);

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
}
