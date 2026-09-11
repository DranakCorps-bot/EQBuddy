using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The Epic tab's guide reaches EQBuddy Mobile as the SAME guide, because both screens call
/// <see cref="GuideChecklistProjection.ApplyEpic"/> from the same point — not because somebody
/// ported it.
///
/// <para><b>The phone's Epic tab used to hand-roll its own grouping</b>, exactly as its Sky tab
/// did before #184: which rows group together, in what order, what the heading reads, what the
/// sub-line says — the same four decisions, spelled a second time. Delivery 3 is where that
/// stopped being harmless, because a guided class is ONE group where the classic tab drew one
/// per section: the copy would have left the phone on the flat list while the PC showed the
/// walkthrough, on a surface the Founder calls first-class in both directions (2026-08-18).</para>
///
/// <para>The other half is the tick. A tap on an epic guide row has to reach the checklist ROW
/// the objective was generated from — the same box the desktop click, the loot auto-tick and
/// the master "Epic complete" button write. A second store reachable only from the phone is
/// trap 4 with a LAN in the middle.</para>
/// </summary>
public sealed class EpicGuideSurfaceParityTests : IDisposable
{
    private const string Dranak = "dranak_freeport";
    private const string ClassName = "Paladin";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"epic-parity-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    /// <summary>The shipped epic checklist, seeded as <c>AppSettings</c> seeds it. The real
    /// rows, because the ids are the join between the guide and its store.</summary>
    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.EpicQuestChecklist.AddRange(EpicQuestDefaults.Items());
        return s;
    }

    private static List<EpicQuestChecklistItem> Rows(AppSettings s) =>
        [.. s.EpicQuestChecklist.Where(i =>
            i.ClassName.Equals(ClassName, StringComparison.OrdinalIgnoreCase))];

    /// <summary>What the DESKTOP draws, exactly as <c>QuestsView.RenderChecklist</c> does it.</summary>
    private IReadOnlyList<QuestChecklistGroup> Desktop(AppSettings s, QuestLedgerStore ledger)
    {
        var rows = s.EpicQuestChecklist
            .Where(i => !s.EpicQuestClassicOnly || i.AvailableInClassic).ToList();
        return GuideChecklistProjection.ApplyEpic(
            QuestChecklistLayout.Epic(rows), rows, GuideCatalog.Default, s, ledger, Dranak);
    }

    /// <summary>What the PHONE draws, through the real snapshot builder.</summary>
    private static CompanionChecklistSection Phone(AppSettings s, QuestLedgerStore ledger) =>
        CompanionProjection.Build(
            new CompanionInputs
            {
                Settings = s,
                Character = "Dranak",
                AppVersion = "2.0.0",
                Offered = CompanionSurfaces.All,
                Quests = new CompanionQuestRequest { Ledger = ledger, CharacterKey = Dranak },
            },
            new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Local)).Quests!.Epics;

    private static CompanionChecklistGroup PhoneGroup(CompanionChecklistSection epics) =>
        epics.Groups.Single(g => g.Class == ClassName);

    private static QuestChecklistGroup DesktopGroup(IReadOnlyList<QuestChecklistGroup> groups) =>
        groups.Single(g => g.ClassName == ClassName);

    [Fact]
    public void ThePhoneShowsTheEpicGuideRowsTheDesktopShows()
    {
        var settings = Settings();
        var ledger = Store();

        var desktop = DesktopGroup(Desktop(settings, ledger));
        var phone = PhoneGroup(Phone(settings, ledger));

        Assert.Equal(14, desktop.Rows.Count);
        Assert.Equal(desktop.Rows.Select(r => r.Id), phone.Rows.Select(r => r.Id));
        Assert.Equal(desktop.Rows.Select(r => r.Title), phone.Rows.Select(r => r.Text));
        Assert.Equal(desktop.Rows.Select(r => r.Acquired), phone.Rows.Select(r => r.Done));
        Assert.Equal($"{desktop.ClassName} — {desktop.Title}", phone.Heading);
    }

    /// <summary>The active-step card is the SAME card: one producer of "what is next", two
    /// renderers. Three surfaces running the selection rule separately is the drift the shared
    /// module exists to prevent.</summary>
    [Fact]
    public void ThePhoneNamesTheSameNextEpicStepAsTheDesktop()
    {
        var settings = Settings();
        var ledger = Store();

        var desktop = DesktopGroup(Desktop(settings, ledger)).GuideCard!;
        var phone = PhoneGroup(Phone(settings, ledger)).Card!;

        Assert.Equal(desktop.RowId, phone.RowId);
        Assert.Equal(desktop.Instruction, phone.Instruction);
        Assert.Equal(desktop.Why, phone.Why);
        Assert.Equal(GuidePresentation.NextLead, phone.Lead);
    }

    /// <summary>
    /// A tap on an epic guide row lands in the checklist ROW, and the desktop reads it there
    /// the same tick.
    ///
    /// <para>The id it comes back with is a guide row id, which belongs to no settings list —
    /// so <see cref="CompanionActions"/> has to resolve it before the switch goes looking for
    /// one and reports a stale tap. That resolution was keyed on the Sky surface alone until
    /// Delivery 3; on the Epic tab it would have returned "nothing matched" for every row on
    /// the tab, which is a silent no-op with a checkbox on it (#212).</para></summary>
    [Fact]
    public void AnEpicGuideStepTickedOnThePhoneIsTickedOnTheDesktop()
    {
        var settings = Settings();
        var ledger = Store();
        var row = PhoneGroup(Phone(settings, ledger)).Rows[0];
        Assert.StartsWith(GuideChecklistProjection.RowIdPrefix, row.Id, StringComparison.Ordinal);

        Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Epics, row.Id, true)));

        // The CHECKLIST row moved — the store the loot auto-tick and "Epic complete" share.
        var objectiveId = row.Id[(GuideChecklistProjection.RowIdPrefix.Length
            + "epic-paladin/".Length)..];
        Assert.True(Rows(settings).Single(r => r.Id == objectiveId).Acquired);
        // …and so did the guide row the desktop draws. Nothing landed in the guide ledger.
        Assert.True(DesktopGroup(Desktop(settings, ledger)).Rows[0].Acquired);
        Assert.Empty(ledger.GuideProgressFor(Dranak, "epic-paladin").DoneObjectiveIds);

        // Idempotent: a tap that agrees with the store is not an edit, so nothing is saved
        // and nothing repaints.
        Assert.False(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Epics, row.Id, true)));
    }

    /// <summary>A skip is the one fact with no home in a checklist store, so it lands in the
    /// guide ledger for every objective — the asymmetry the router is explicit about, reaching
    /// the Epic tab unchanged.</summary>
    [Fact]
    public void ASkipOnAnEpicStepIsTheSameSkipOnBothScreens()
    {
        var settings = Settings();
        var ledger = Store();
        var row = PhoneGroup(Phone(settings, ledger)).Rows[0];

        Assert.True(CompanionActions.Apply(settings, ledger, Dranak,
            new CompanionAction(CompanionSurfaces.Epics, CompanionActions.SkipVerb + row.Id, true)));

        Assert.True(DesktopGroup(Desktop(settings, ledger)).Rows[0].IsSkipped);
        Assert.True(PhoneGroup(Phone(settings, ledger)).Rows[0].Skipped);
        // The checklist row itself is untouched: "not doing this" and "done" are different
        // statements and live in different stores.
        Assert.False(Rows(settings)[0].Acquired);
    }

    /// <summary>
    /// <b>The fold reaches the phone as a control rather than as a verdict.</b>
    ///
    /// <para><c>CompanionChecklistGroup.Collapsed</c> has said "a tap opens it" since guides
    /// shipped and the page had no tap — so a folded quest drew its heading, its caption and
    /// its reward line and offered no route to the steps at all. On Sky that hid six quests
    /// behind a control nobody had built; Delivery 3 folds all fourteen epics, which is the
    /// whole tab. <c>Fold</c> is the key the page toggles under, and it is the DESKTOP's own
    /// fold key so the two surfaces are folding the same thing by the same name.</para>
    ///
    /// <para>Page-local and never written back, which is the house ruling on a fold twice
    /// over: "a tap on a phone must not reach across the LAN to fold something on the PC while
    /// somebody is playing at it."</para></summary>
    [Fact]
    public void AFoldedEpicGuideReachesThePhoneWithTheKeyThatOpensIt()
    {
        var settings = Settings();
        var ledger = Store();

        var phone = PhoneGroup(Phone(settings, ledger));
        Assert.True(phone.Collapsed);
        Assert.Equal("epic-paladin", phone.Fold);
        // The rows travel even while folded — the page opens it without a round trip.
        Assert.Equal(14, phone.Rows.Count);

        settings.GuideExpanded.Add("epic-paladin");
        Assert.False(PhoneGroup(Phone(settings, ledger)).Collapsed);
    }

    /// <summary>An unguided class is untouched on the phone too — the progressive cutover, from
    /// the other screen (Founder lock 5).</summary>
    [Fact]
    public void AClassWithNoEpicGuideIsUnchangedOnThePhoneToo()
    {
        var settings = new AppSettings();
        settings.EpicQuestChecklist.Add(new EpicQuestChecklistItem
        {
            Id = "made-up-1", ClassName = "Beastlord", Section = "Pieces",
            QuestItem = "Something", Source = "Somewhere", Order = 0,
        });

        var phone = Phone(settings, Store()).Groups.Single();

        Assert.Equal("Beastlord — Pieces", phone.Heading);
        Assert.Null(phone.Card);
        Assert.Null(phone.Fold);
        Assert.False(phone.Collapsed);
        Assert.Equal("made-up-1", phone.Rows.Single().Id);
    }

    /// <summary>With no ledger the phone shows the classic checklist rather than half a guide —
    /// the same floor the Sky tab has. A guide's progress is per character, so no character key
    /// means no guide.</summary>
    [Fact]
    public void WithNoLedgerThePhoneShowsTheClassicEpicChecklist()
    {
        var settings = Settings();

        var epics = CompanionProjection.Build(
            new CompanionInputs
            {
                Settings = settings,
                Character = "Dranak",
                AppVersion = "2.0.0",
                Offered = CompanionSurfaces.All,
            },
            new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Local)).Quests!.Epics;

        var paladin = epics.Groups.Where(g => g.Class == ClassName).ToList();
        Assert.Equal(2, paladin.Count);              // the two SECTIONS, as before
        Assert.All(paladin, g => Assert.Null(g.Card));
        Assert.All(paladin, g => Assert.Null(g.Fold));
    }

    /// <summary>The classic-era lens narrows both screens to the same rows — one producer (the
    /// row's own flag) read through the rows the tab was built from.</summary>
    [Fact]
    public void TheClassicEraLensNarrowsBothScreensTheSameWay()
    {
        var settings = Settings();
        settings.EpicQuestClassicOnly = true;
        var ledger = Store();

        var desktop = Desktop(settings, ledger).Single(g => g.ClassName == "Bard");
        var phone = Phone(settings, ledger).Groups.Single(g => g.Class == "Bard");

        var classic = settings.EpicQuestChecklist
            .Count(i => i.ClassName == "Bard" && i.AvailableInClassic);
        Assert.True(classic > 0 && classic < 31);
        Assert.Equal(classic, desktop.Rows.Count);
        Assert.Equal(desktop.Rows.Select(r => r.Id), phone.Rows.Select(r => r.Id));
    }
}
