using EQBuddy.Companion;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **THE GUIDE-ATTACHMENT HOOKUP** (DRA-83; the DRA-70 plan's D5, and the slice its D8 held
/// <c>NoShippedGuideCarriesAnAttachmentYet</c> open for).
///
/// <para>Four claims, each asserted from both sides because a guard that can only go green is
/// vacuous coverage (trap 34):</para>
///
/// <list type="number">
/// <item><b>One producer, and the same one.</b> An answer on a guide row is a
///   <see cref="Recommendation"/> the Helper's own engine built — same facts, same evidence
///   tags, same sentences as the room.</item>
/// <item><b>Every kind is DECIDED.</b> <see cref="Recommendations.GoalFor"/> answers for all
///   three, each maps to an engine that actually runs, and an unknown kind is silent rather
///   than a throw.</item>
/// <item><b>Silence is the normal answer.</b> No evidence, no line — never an empty-state
///   sentence repeated down a checklist (trap 73).</item>
/// <item><b>Both surfaces carry it, from one field.</b> The desktop's row and the phone's wire
///   row come out of the same projection, so neither can drift (#210).</item>
/// </list>
/// </summary>
public sealed class GuideAttachmentTests : IDisposable
{
    private const string Dranak = "dranak_freeport";

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"guide-attach-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { }
        try { File.Delete(_path + ".rules"); } catch { }
    }

    private QuestLedgerStore Store() => new(_path) { TrackFilter = _ => true };

    // ---- fixtures ----------------------------------------------------------------------

    private static GuideAttachment Ref(string kind, string key) => new() { Kind = kind, Key = key };

    private static SessionRow Session(string zone, double hours, double xp, int deaths = 0) =>
        new(1, "erollisi", "Dranak", DateTime.Today, DateTime.Today.AddHours(hours),
            hours * 3600, hours * 3600, "ended", zone, 0, xp, 0, 0, deaths, 0, "", "");

    private static MobSummary Mob(string name, string zone, int kills) =>
        new(name, kills, kills, 30, 0, 0, []) { Zone = zone };

    /// <summary>Enough stored play in one zone to clear <see cref="ZoneHistory.MinHours"/>, so
    /// the Level Up engine has a rate to answer with. Two zones, because a throughput baseline
    /// that compared a zone with itself is refused by design — and some deaths, so the engine
    /// emits MORE sentences than a guide row draws and the cap has something to hold back.</summary>
    private static HelperInputs Levelled(string zone = "Plane of Sky")
    {
        IReadOnlyList<MobSummary> pool = [Mob("a sky drake", zone, 300), Mob("a gnoll", "Blackburrow", 40)];
        IReadOnlyList<SessionRow> sessions =
            [Session(zone, 6, 40, deaths: 4), Session("Blackburrow", 6, 10)];
        return new HelperInputs(
            ZoneHistory.Fold(sessions, pool), pool, null, [], [], [], [], false, [], [], null,
            ResolvedLevel.Unknown);
    }

    /// <summary>A catalog record with a creature named in every zone it drops in. DRA-84 D4
    /// withholds a drop offer that can name nobody, and the shipped catalog names one on 98.2%
    /// of its wearable (item, zone) pairs — so a fixture with none would quietly make these
    /// attachment tests about the who rule rather than about the attachment.</summary>
    private static ItemCatalog.Record Record(string name, string slot, int ac, string[] zones) =>
        new()
        {
            Name = name, StatsText = $"Slot: {slot}\nAC: {ac}", Slots = [slot], Ac = ac,
            DropZones = [.. zones],
            DropMobs = zones.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(
                z => z, z => new List<string> { $"a {z.ToLowerInvariant()} dweller" },
                StringComparer.OrdinalIgnoreCase),
        };

    /// <summary>A character wearing something worse than one catalog item, which is what makes
    /// the Farm Gear sweep answer at all.</summary>
    private static HelperInputs Geared(
        string item = "Froglok Bone Helm", string zone = "Lower Guk") =>
        new(ZoneHistory.Fold([], []), [], null, [], [], [], [], false, [], [], null,
            ResolvedLevel.Unknown)
        {
            Worn = [new WornItem("Rusty Helm", "Rusty Helm", "HEAD",
                ItemStatsBlock.Parse(["Slot: HEAD", "AC: 4"]))],
            Items = new ItemCatalog([Record(item, "HEAD", 9, [zone])]),
        };

    // ---- 1. every kind is decided, and every decision has an engine --------------------

    /// <summary>The must-list half of trap 34 for the reference kinds: a kind nobody wired up
    /// would answer nothing on screen and look exactly like a kind with no evidence.</summary>
    [Fact]
    public void EveryKnownKindHasAGoal()
    {
        Assert.NotEmpty(GuideAttachment.KnownKinds);
        foreach (var kind in GuideAttachment.KnownKinds)
            Assert.True(Recommendations.GoalFor(kind) is not null, $"{kind} maps to no goal");
    }

    /// <summary>And the goal it maps to has an ENGINE behind it. <c>Attached</c> throws on a goal
    /// with no arm rather than returning an empty list, so this is the reachable half of that
    /// decision — the throw is a test's business and never a player's (trap 78's other side: a
    /// guard aimed at nothing is green forever).</summary>
    [Fact]
    public void EveryKnownKindsGoalHasAnEngineBehindIt()
    {
        foreach (var kind in GuideAttachment.KnownKinds)
        {
            var answers = Recommendations.Attached(Levelled(), [Ref(kind, "Plane of Sky")]);
            // Whether it ANSWERS is the fixture's business; that it ran is this test's.
            Assert.NotNull(answers);
        }
    }

    /// <summary>A kind from outside the list is silence, not an exception: the schema refuses one
    /// in the curated file, and a hand-edited profile or a newer catalog must not crash a
    /// repaint.</summary>
    [Fact]
    public void AnUnknownKindIsSilent()
    {
        Assert.Null(Recommendations.GoalFor("BestInSlot"));
        Assert.Empty(Recommendations.Attached(Levelled(), [Ref("BestInSlot", "Plane of Sky")]));
        Assert.Empty(Recommendations.Attached(Levelled(), [Ref(GuideAttachment.XpFarm, "")]));
    }

    // ---- 2. the answers are the Helper's own ------------------------------------------

    /// <summary>An <c>XpFarm</c> reference gets the zone's own Level Up row — the same record,
    /// carrying the same measured fact the Helper room would print.</summary>
    [Fact]
    public void AnXpFarmReferenceGetsThatZonesLevelUpAnswer()
    {
        var answer = Assert.Single(Recommendations.Attached(
            Levelled(), [Ref(GuideAttachment.XpFarm, "Plane of Sky")]));

        Assert.Equal(HelperGoal.LevelUp, answer.Goal);
        Assert.Equal("Plane of Sky", answer.Answer.Zone);
        var rate = Assert.Single(answer.Why.OfType<ZoneXpRateFact>());
        Assert.Equal("Plane of Sky", rate.Zone);
        Assert.Equal(Evidence.Personal, rate.Evidence);

        // The SAME sentence the room draws, because it is the same call.
        var room = Recommendations.Rank(Levelled(), [HelperGoal.LevelUp]).Top
            .First(r => r.Zone == "Plane of Sky");
        Assert.Equal(
            HelperPresentation.Why(room.Why.OfType<ZoneXpRateFact>().First()),
            HelperPresentation.Why(rate));
    }

    /// <summary>The zone is matched case-insensitively, for the reason the join is: two sources
    /// capitalise a zone differently and neither spelling is wrong.</summary>
    [Fact]
    public void AZoneKeyMatchesWhateverWayItIsCapitalised()
    {
        Assert.Single(Recommendations.Attached(
            Levelled(), [Ref(GuideAttachment.XpFarm, "plane of SKY")]));
    }

    /// <summary>A <c>GearFarm</c> reference asks the Farm Gear engine about the PLACE.</summary>
    [Fact]
    public void AGearFarmReferenceGetsThatZonesGearAnswer()
    {
        var answer = Assert.Single(Recommendations.Attached(
            Geared(), [Ref(GuideAttachment.GearFarm, "Lower Guk")]));

        Assert.Equal(HelperGoal.FarmGear, answer.Goal);
        Assert.Equal("Lower Guk", answer.Answer.Zone);
        Assert.Contains(answer.Why.OfType<GearUpgradeFact>(), f => f.Item == "Froglok Bone Helm");
    }

    /// <summary>A <c>GearUpgrade</c> reference is keyed on an ITEM, so it matches a LINE — the
    /// engine's rows are places and quests, and an item is never one's subject. Only the lines
    /// about that item come back: a row naming three upgrades must not answer a step that points
    /// at one of them with all three.</summary>
    [Fact]
    public void AGearUpgradeReferenceGetsOnlyTheLinesAboutThatItem()
    {
        var inputs = Geared();
        inputs = inputs with
        {
            Items = new ItemCatalog([
                Record("Froglok Bone Helm", "HEAD", 9, ["Lower Guk"]),
                Record("Shiny Brass Helm", "HEAD", 7, ["Lower Guk"]),
            ]),
        };

        var answer = Assert.Single(Recommendations.Attached(
            inputs, [Ref(GuideAttachment.GearUpgrade, "Froglok Bone Helm")]));

        Assert.Equal("Lower Guk", answer.Answer.Zone);
        var named = Assert.Single(answer.Why.OfType<GearUpgradeFact>());
        Assert.Equal("Froglok Bone Helm", named.Item);
        // The ROW still names both — the answer is the row, narrowed to this step's subject.
        Assert.Equal(2, answer.Answer.Why.OfType<GearUpgradeFact>().Count());
    }

    /// <summary>One engine run however many references ask for it: 95 Sky guides pointing at one
    /// zone are one question. Measured through the answers rather than by counting calls — every
    /// reference gets the SAME row instance back, which is only true if the engine ran once.</summary>
    [Fact]
    public void ManyReferencesToOneSubjectShareOneEngineRun()
    {
        var answers = Recommendations.Attached(Levelled(), [
            Ref(GuideAttachment.XpFarm, "Plane of Sky"),
            Ref(GuideAttachment.XpFarm, "Plane of Sky"),
            Ref(GuideAttachment.XpFarm, "Plane of Sky"),
        ]);

        Assert.Equal(3, answers.Count);
        Assert.All(answers, a => Assert.Same(answers[0].Answer, a.Answer));
    }

    // ---- 3. silence ------------------------------------------------------------------

    /// <summary>A character with no history gets NO line — not "no history yet" printed under
    /// every step. The room owns that sentence, once, with the command that fixes it.</summary>
    [Fact]
    public void AReferenceTheHelperCannotAnswerGetsNoLine()
    {
        Assert.Empty(Recommendations.Attached(
            HelperInputs.Nothing, [Ref(GuideAttachment.XpFarm, "Plane of Sky")]));
        // Evidence, but about somewhere else.
        Assert.Empty(Recommendations.Attached(
            Levelled(), [Ref(GuideAttachment.XpFarm, "Kaesora")]));
        // And an item the sweep never named.
        Assert.Empty(Recommendations.Attached(
            Geared(), [Ref(GuideAttachment.GearUpgrade, "Cloak of Flames")]));
    }

    // ---- 4. the words ----------------------------------------------------------------

    /// <summary>The line says which reference is being answered and then the Helper's own
    /// sentences — nothing re-worded, and a catalog line still carries HOME-004's label.</summary>
    [Fact]
    public void TheLineNamesTheReferenceAndThenQuotesTheHelper()
    {
        var answer = Assert.Single(Recommendations.Attached(
            Geared(), [Ref(GuideAttachment.GearUpgrade, "Froglok Bone Helm")]));

        var line = HelperPresentation.Attached(answer);
        Assert.StartsWith("Your Helper on Froglok Bone Helm:", line);
        Assert.Contains(HelperPresentation.Why(answer.Why[0]), line);
        Assert.Contains(HelperPresentation.CatalogLabel, line);
    }

    /// <summary>The row's cap is two sentences and it SAYS what it held back, naming the room
    /// that has the rest (trap 50). A link would be a lie on the phone, which has no room to
    /// open (trap 35).</summary>
    [Fact]
    public void TheRowCapSaysWhatItHeldBackAndWhereTheRestIs()
    {
        var answer = Assert.Single(Recommendations.Attached(
            Levelled(), [Ref(GuideAttachment.XpFarm, "Plane of Sky")]));
        Assert.True(answer.Why.Count > HelperPresentation.AttachedWhyCap,
            "fixture must produce more sentences than the row draws");

        var line = HelperPresentation.Attached(answer);
        var withheld = answer.Why.Count - HelperPresentation.AttachedWhyCap
            + answer.Answer.WithheldWhy;
        Assert.Contains(HelperPresentation.AttachedWithheld(withheld), line);
        Assert.Contains("Helper room", line);
        Assert.DoesNotContain("http", line);

        // And a row inside the cap says nothing about withholding.
        Assert.Equal("", HelperPresentation.AttachedWithheld(0));
    }

    /// <summary>Each kind's lead clause names its own subject. The default arm is unreachable
    /// from the producer and still names the goal rather than nothing, so a fourth kind arriving
    /// before the switch knows it draws a headed block instead of bare numbers.</summary>
    [Fact]
    public void EveryKindsLeadClauseNamesItsSubject()
    {
        foreach (var kind in GuideAttachment.KnownKinds)
        {
            var answer = new GuideAttachmentAnswer(
                Ref(kind, "Lower Guk"), HelperGoal.FarmGear,
                new Recommendation(RecommendationKind.Zone, "Lower Guk", "Lower Guk",
                    [HelperGoal.FarmGear], [], [], 0, 0),
                []);
            Assert.Contains("Lower Guk", HelperPresentation.AttachedLead(answer));
        }

        var unknown = new GuideAttachmentAnswer(
            Ref("BestInSlot", "Lower Guk"), HelperGoal.FarmGear,
            new Recommendation(RecommendationKind.Zone, "Lower Guk", "Lower Guk",
                [HelperGoal.FarmGear], [], [], 0, 0),
            []);
        Assert.Contains(HelperPresentation.GoalLabel(HelperGoal.FarmGear),
            HelperPresentation.AttachedLead(unknown));

        // No sentences, no line at all — a lead clause on its own is a label pointing at
        // nothing.
        Assert.Equal("", HelperPresentation.Attached(unknown));
    }

    // ---- 5. the lookup ---------------------------------------------------------------

    /// <summary>The whole shipped catalog's references, answered in one pass, keyed by reference.
    /// Both halves matter: the zone reference the 95 Sky guides share resolves, and a step nobody
    /// attached anything to gets "".</summary>
    [Fact]
    public void TheLookupAnswersTheShippedReferencesAndNothingElse()
    {
        var lines = GuideAttachmentLines.Read(Levelled(), GuideCatalog.Default);

        Assert.True(lines.Answered >= 1, "the shipped Sky zone reference should answer");
        var line = lines.For([Ref(GuideAttachment.XpFarm, "Plane of Sky")]);
        Assert.Contains("Your Helper on levelling in Plane of Sky:", line);

        Assert.Equal("", lines.For([]));
        Assert.Equal("", lines.For([Ref(GuideAttachment.XpFarm, "Kaesora")]));
        // The KIND is part of the identity: an item and a zone that share a name are two
        // questions, and EQ has plenty of both.
        Assert.Equal("", lines.For([Ref(GuideAttachment.GearUpgrade, "Plane of Sky")]));
    }

    /// <summary>No inputs, no lookup — and <see cref="GuideAttachmentLines.None"/> answers ""
    /// for everything rather than making every caller branch on null.</summary>
    [Fact]
    public void WithoutInputsTheLookupIsEmptyRatherThanNull()
    {
        Assert.Equal(0, GuideAttachmentLines.Read(null, GuideCatalog.Default).Answered);
        Assert.Equal("", GuideAttachmentLines.None.For(
            [Ref(GuideAttachment.XpFarm, "Plane of Sky")]));
    }

    /// <summary>The memo re-asks when the evidence moves and NOT otherwise — the whole reason a
    /// repaint gate can call it. Both directions, because a memo that never rebuilt would pass a
    /// "does not rebuild" test perfectly.</summary>
    [Fact]
    public void TheMemoRebuildsOnlyWhenTheEvidenceMoves()
    {
        var signature = "one";
        var gathers = 0;
        var memo = new GuideAttachmentMemo(
            () => signature,
            () => { gathers++; return Levelled(); },
            () => GuideCatalog.Default);

        memo.Refresh();
        memo.Refresh();
        Assert.Equal(1, gathers);
        Assert.True(memo.Lines.Answered > 0);

        signature = "two";
        memo.Refresh();
        Assert.Equal(2, gathers);
    }

    // ---- 6. both surfaces ------------------------------------------------------------

    /// <summary>
    /// **The desktop row and the phone's wire row carry the same line, from one projection.**
    ///
    /// <para>The committed negative is the second half: with no Helper lookup the rows carry
    /// NOTHING, so this test would fail on a projection that had started spelling its own
    /// sentence — and it cannot pass by accident on a surface that draws every row the same
    /// way.</para></summary>
    [Fact]
    public void BothSurfacesDrawTheHelpersLineAndNeitherInventsIt()
    {
        var settings = new AppSettings();
        settings.SkyQuestChecklist.AddRange(SkyChecklistRows.Items.Select(i => i.Clone()));
        var ledger = Store();
        var groups = QuestChecklistLayout.Sky(settings.SkyQuestChecklist, settings.SkyQuestCompleted, false);
        var lines = GuideAttachmentLines.Read(Levelled(), GuideCatalog.Default);

        var withHelper = GuideChecklistProjection.Apply(
            groups, GuideCatalog.Default, settings, ledger, Dranak, helper: lines);
        var answered = withHelper
            .SelectMany(g => g.Rows)
            .Where(r => r.HelperAnswer.Length > 0)
            .ToList();
        Assert.NotEmpty(answered);
        Assert.All(answered, r =>
            Assert.Contains("Your Helper on levelling in Plane of Sky:", r.HelperAnswer));

        // ONE row per guided group: the zone reference sits on the open-farm step and nowhere
        // else, so a projection that had spread it over every row of the stage would fail here.
        var guided = withHelper.Where(g => g.GuideId.Length > 0).ToList();
        Assert.NotEmpty(guided);
        Assert.All(guided, g => Assert.Equal(1, g.Rows.Count(r => r.HelperAnswer.Length > 0)));

        // The phone's rows, off the same projection.
        var wire = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            AppVersion = "test",
            Settings = settings,
            Offered = [CompanionSurfaces.Quests],
            Quests = new CompanionQuestRequest { Ledger = ledger, CharacterKey = Dranak, Helper = lines },
        }, DateTime.Now);
        var phoneRows = wire.Quests!.Sky.Groups
            .SelectMany(g => g.Rows)
            .Where(r => r.Helper is { Length: > 0 })
            .ToList();
        Assert.NotEmpty(phoneRows);
        Assert.Equal(
            answered.Select(r => r.HelperAnswer).Distinct(),
            phoneRows.Select(r => r.Helper!).Distinct());

        // THE NEGATIVE: no lookup, no line — on either surface.
        var without = GuideChecklistProjection.Apply(
            groups, GuideCatalog.Default, settings, ledger, Dranak);
        Assert.DoesNotContain(without.SelectMany(g => g.Rows), r => r.HelperAnswer.Length > 0);

        var bare = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            AppVersion = "test",
            Settings = settings,
            Offered = [CompanionSurfaces.Quests],
            Quests = new CompanionQuestRequest { Ledger = ledger, CharacterKey = Dranak },
        }, DateTime.Now);
        Assert.DoesNotContain(
            bare.Quests!.Sky.Groups.SelectMany(g => g.Rows), r => r.Helper is { Length: > 0 });
    }

    /// <summary>
    /// **The wire fingerprint moves when the line does** (trap 72, on the wire).
    ///
    /// <para>Worse here than on a window: a phone has no repaint of its own to blame, so a
    /// section print that ignored this field would leave last week's rate on screen until an
    /// unrelated tick moved. The negative is the same snapshot twice.</para></summary>
    [Fact]
    public void TheQuestsFingerprintMovesWhenTheHelpersLineDoes()
    {
        var settings = new AppSettings();
        settings.SkyQuestChecklist.AddRange(SkyChecklistRows.Items.Select(i => i.Clone()));
        var ledger = Store();

        CompanionSnapshot Snap(GuideAttachmentLines lines) => CompanionProjection.Build(
            new CompanionInputs
            {
                Character = "Dranak",
                AppVersion = "test",
                Settings = settings,
                Offered = [CompanionSurfaces.Quests],
                Quests = new CompanionQuestRequest
                {
                    Ledger = ledger, CharacterKey = Dranak, Helper = lines,
                },
            }, DateTime.Now);

        var quiet = CompanionProjection.SectionFingerprints(Snap(GuideAttachmentLines.None));
        var answered = CompanionProjection.SectionFingerprints(
            Snap(GuideAttachmentLines.Read(Levelled(), GuideCatalog.Default)));
        var again = CompanionProjection.SectionFingerprints(Snap(GuideAttachmentLines.None));

        Assert.NotEqual(quiet[CompanionSurfaces.Quests], answered[CompanionSurfaces.Quests]);
        Assert.Equal(quiet[CompanionSurfaces.Quests], again[CompanionSurfaces.Quests]);
    }
}
