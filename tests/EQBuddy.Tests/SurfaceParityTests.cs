using EQBuddy.Companion;
using EQBuddy.Core;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The phone and the desktop must answer the same question the same way — in BOTH
/// directions.
///
/// David, 2026-08-18: mobile and desktop are each first-class, and neither is allowed to
/// be the one that quietly falls behind. #210 (liminalwarmth) is what made the cost
/// visible, and it is worth being precise about which way round it was: EQBuddy Mobile
/// still built the cross-class ready list after the DESKTOP had lost it, so for two days
/// the phone answered "what can I turn in right now" and the big window could not.
/// Restoring the desktop then created the mirror risk — four things the desktops had and
/// the phone did not.
///
/// A list of features kept level by hand drifts; that is the whole history of #184, #122
/// and #152. So parity is asserted against the SHARED module both sides call: if the
/// projection ever stops agreeing with <see cref="QuestChecklistLayout"/>, these fail,
/// rather than a player finding it.
/// </summary>
public class SurfaceParityTests
{
    private static SkyQuestChecklistItem Item(string id, string cls, string reward,
        string item, bool acquired = false, string npc = "Cilin Spellsinger") => new()
        {
            Id = id, ClassName = cls, Npc = npc, Reward = reward, QuestItem = item,
            Source = "Isle 3", Acquired = acquired,
        };

    /// <summary>One class with all four states, plus a second class holding a ready
    /// reward — so "across every class" is a real claim and the ordering has something
    /// to order.</summary>
    private static AppSettings Settings()
    {
        var s = new AppSettings();
        s.SkyQuestChecklist.AddRange([
            Item("a1", "Bard", "Amulet of the Fae", "Amulet piece", acquired: true),
            Item("b1", "Bard", "Mask of Song", "Woolen Mask", acquired: true),
            Item("b2", "Bard", "Mask of Song", "Wind Rune Meda", acquired: true),
            Item("c1", "Bard", "Mantle of the Songweaver", "Woolen Mantle", acquired: true),
            Item("c2", "Bard", "Mantle of the Songweaver", "Wind Rune Azia"),
            Item("d1", "Bard", "Spear of Harmony", "Spear shaft"),
            Item("r1", "Ranger", "Bow of Sky", "Bow stave", acquired: true,
                npc: "Efreeti Lord Djarn"),
        ]);
        s.SkyQuestCompleted.Add(QuestChecklistLayout.RewardKey("Bard", "Amulet of the Fae"));
        return s;
    }

    private static CompanionChecklistSection Sky(AppSettings s, CompanionQuestRequest? req = null) =>
        CompanionProjection.Build(
            new CompanionInputs
            {
                Settings = s,
                Character = "Dranak",
                AppVersion = "1.93.0",
                Offered = CompanionSurfaces.All,
                Quests = req,
            },
            new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Local)).Quests!.Sky;

    /// <summary>The checklist proper — every group the player can tick. The summary bands
    /// (★ Ready, and the #243 leftover bands once a dump has been read) are asserted on
    /// their own terms below, and counting them by POSITION is how a test that says
    /// "the checklist" quietly stops meaning it the day a second summary is added.</summary>
    private static IEnumerable<CompanionChecklistGroup> Checklist(CompanionChecklistSection sky) =>
        sky.Groups.Where(g => g.Tickable);

    private static IReadOnlyList<QuestChecklistGroup> Desktop(AppSettings s) =>
        QuestChecklistLayout.Sky(s.SkyQuestChecklist, s.SkyQuestCompleted);

    [Fact]
    public void ThePhoneGroupsSkyExactlyAsTheDesktopDoes()
    {
        var s = Settings();

        // Summary bands excluded — the desktop draws those separately.
        var phone = Checklist(Sky(s)).Select(g => g.Heading);
        var desktop = Desktop(s).Select(g => g.Heading);

        Assert.Equal(desktop, phone);
    }

    [Fact]
    public void ThePhoneOrdersByActionabilityBecauseTheDesktopDoes()
    {
        // Ready first, then closest-to-done, then untouched, turned-in last. This
        // reached the phone by conversion rather than by being ported, which is the
        // point: nobody had to notice it was missing.
        Assert.Equal(
            ["Bard · Mask of Song", "Bard · Mantle of the Songweaver",
             "Bard · Spear of Harmony", "Bard · Amulet of the Fae", "Ranger · Bow of Sky"],
            Checklist(Sky(Settings())).Select(g => g.Heading));
    }

    [Fact]
    public void ThePhoneAndTheDesktopAgreeOnEveryStateNote()
    {
        var s = Settings();

        Assert.Equal(
            Desktop(s).Select(g => g.Note),
            Checklist(Sky(s)).Select(g => g.Note));
    }

    [Fact]
    public void TheReadyBandSpansEveryClassOnThePhoneToo()
    {
        var band = Sky(Settings()).Groups[0];

        Assert.Equal("★ Ready 2", band.Heading);
        Assert.Equal(
            ["Bard — Mask of Song", "Ranger — Bow of Sky"],
            band.Rows.Select(r => r.Text));
    }

    [Fact]
    public void TheReadyBandNamesWhoTakesTheHandInOnThePhoneToo()
    {
        // "What can I turn in right now" is only actionable with "and to whom" — the
        // phone is the surface most likely to be read while walking to the NPC.
        Assert.Equal("Efreeti Lord Djarn",
            Sky(Settings()).Groups[0].Rows.Single(r => r.Text.StartsWith("Ranger")).Detail);
    }

    /// <summary>The already-unlocked caveat (Hateborne, 2026-09-03) reaches the phone as
    /// the SAME string Core builds for the desktops — <see
    /// cref="QuestChecklistLayout.ReadyDetail"/>, asserted against the module rather than
    /// re-spelled here. And it is opt-in: a class nobody has unlocked keeps the bare NPC,
    /// which is what keeps <see cref="TheReadyBandNamesWhoTakesTheHandInOnThePhoneToo"/>
    /// true unchanged.</summary>
    [Fact]
    public void TheReadyBandAnnotatesAnAlreadyUnlockedClassOnThePhoneToo()
    {
        var s = Settings();
        var band = Sky(s, new CompanionQuestRequest { UnlockedClasses = ["Ranger"] }).Groups[0];

        var desktop = QuestChecklistLayout.ReadyToTurnIn(Desktop(s))
            .Single(g => g.ClassName == "Ranger");
        Assert.Equal(QuestChecklistLayout.ReadyDetail(desktop, ["Ranger"]),
            band.Rows.Single(r => r.Text.StartsWith("Ranger")).Detail);
        Assert.Contains("Ranger already unlocked",
            band.Rows.Single(r => r.Text.StartsWith("Ranger")).Detail);
        Assert.Equal("Cilin Spellsinger",
            band.Rows.Single(r => r.Text.StartsWith("Bard")).Detail);
    }

    [Fact]
    public void ARewardTurnedInOnEitherScreenIsTurnedInOnBoth()
    {
        // The reward key is one spelling in one place. It used to be written out by hand
        // here as well, which is how "done" could have meant two things.
        var s = Settings();
        var key = QuestChecklistLayout.RewardKey("Bard", "Mask of Song");

        UI.Shared.SkyCompleteToggle.MarkTurnedIn(s, key,
            UI.Shared.SkyCompleteToggle.ItemsFor(s.SkyQuestChecklist, key));

        Assert.DoesNotContain(Sky(s).Groups[0].Rows, r => r.Text.Contains("Mask of Song"));
        Assert.Equal("done",
            Checklist(Sky(s)).Single(g => g.Heading.EndsWith("Mask of Song")).Note);
    }

    [Fact]
    public void ASettingNoPlayerCanChangeNeverNarrowsThePhone()
    {
        // #212 (bjstrange). The projection scoped the whole Sky list by
        // AppSettings.SkyQuestClass, and NOTHING in the codebase writes that setting —
        // the widget's Sky card was its only writer and the 2026-08-16 consolidation
        // deleted it. So a value persisted before that day filtered the phone forever,
        // and no control on it could change the answer.
        var s = Settings();
        s.SkyQuestClass = "Necromancer";   // a class with nothing in this checklist

        Assert.Equal(Desktop(s).Count, Checklist(Sky(s)).Count());
        Assert.Contains(Sky(s).Groups, g => g.Class == "Bard");
    }

    [Fact]
    public void TheReadyBandIsNotAChecklist()
    {
        // Its rows name REWARDS and their ids are reward keys no tick accepts, so the
        // checkboxes it used to draw were silent no-ops — and "ready" rendered as done,
        // which the phone strikes through. On the one band whose whole job is "go hand
        // these in", that read as "already handed in".
        var band = Sky(Settings()).Groups[0];

        Assert.False(band.Tickable);
        Assert.All(band.Rows, r => Assert.False(r.Done));
    }

    [Fact]
    public void EveryOtherGroupStaysTickable()
    {
        // The fix must not cost the phone its actual checklist — ticking an item there
        // is the whole point of the surface. With no dump in this fixture ★ Ready is the
        // only summary, so everything after it is a real, tickable group.
        Assert.Single(Sky(Settings()).Groups, g => !g.Tickable);
        Assert.All(Sky(Settings()).Groups.Skip(1), g => Assert.True(g.Tickable));
    }

    [Fact]
    public void ThePhoneCountsWhatTheDesktopCounts()
    {
        var s = Settings();
        var desktop = Desktop(s);

        Assert.Equal(desktop.Sum(g => g.Done), Sky(s).Done);
        Assert.Equal(desktop.Sum(g => g.Total), Sky(s).Total);
    }

    // ---- the Sky LEFTOVER BANDS (#243, tvongaza): the phone is the THIRD renderer ----
    //
    // PR 1 put the words in Core precisely so this one could not invent its own. The two
    // desktop bands are covered against `SkyLeftovers` by `SkyLeftoversTests`,
    // `QuestsRenderTests` (Avalonia) and `EndToEndTests` (WPF); what only a parity test can
    // say is that the PHONE draws the same rows, in the same bands, from the same join —
    // and it is the surface with the best track record of drifting in BOTH directions
    // (#210 one way, #212 the other).

    private static InventoryFile.Snapshot Dump(params (string Location, string Name, int Count)[] rows)
    {
        var entries = rows.Select(r => new InventoryFile.Entry(r.Location, r.Name, r.Count)).ToList();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
            counts[QuestCatalog.BaseItemName(e.Name)] =
                counts.GetValueOrDefault(QuestCatalog.BaseItemName(e.Name)) + e.Count;
        return new InventoryFile.Snapshot("inv.txt", new DateTime(2026, 9, 2, 8, 0, 0), counts)
        {
            Entries = entries,
        };
    }

    /// <summary>Bard's Amulet of the Fae is turned in (see <see cref="Settings"/>), so its
    /// piece is band A. The Ranger's bow stave is wanted by a class this Bard does not
    /// have, so it is band B — one row each, which is what makes "never mixed under one
    /// heading" a claim the assertions can actually check.</summary>
    private static InventoryFile.Snapshot LeftoverDump() =>
        Dump(("General1-Slot1", "Amulet piece", 2), ("Bank1", "Bow stave", 1));

    private static CompanionQuestRequest LeftoverRequest(
        InventoryFile.Snapshot? dump, QuestCatalog? catalog = null, params string[] classes) =>
        new() { Inventory = dump, Catalog = catalog, CharacterClassNames = classes };

    private static SkyLeftoversResult Core(AppSettings s, InventoryFile.Snapshot? dump,
        QuestCatalog? catalog = null, params string[] classes) =>
        SkyLeftovers.Compute(dump, s.SkyQuestChecklist, s.SkyQuestCompleted, classes, catalog);

    [Fact]
    public void ThePhonesLeftoverRowsAreCoresRowsExactly()
    {
        var s = Settings();
        var phone = Sky(s, LeftoverRequest(LeftoverDump(), null, "Bard")).Groups
            .Where(g => g.Heading.StartsWith("No longer needed")
                || g.Heading.StartsWith("Other classes still want"))
            .ToList();
        var core = Core(s, LeftoverDump(), null, "Bard");

        // The headings, the row words and the hover all come off the Core members PR 1
        // added — nothing in the projection spells any of them.
        Assert.Equal(
            [core.NoLongerNeededHeading, core.OtherClassesWantHeading],
            phone.Select(g => g.Heading));
        Assert.Equal(
            core.RowsIn(SkyLeftoverBand.NoLongerNeeded).Select(r => r.Line),
            phone[0].Rows.Select(r => r.Text));
        Assert.Equal(
            core.RowsIn(SkyLeftoverBand.OtherClassesWant).Select(r => r.Line),
            phone[1].Rows.Select(r => r.Text));
        Assert.Equal(
            core.Rows.Select(r => r.Detail),
            phone.SelectMany(g => g.Rows).Select(r => r.Detail));
    }

    [Fact]
    public void TheTwoBandsAreNeverMixedUnderOneHeadingOnThePhone()
    {
        // The whole honesty of the feature: band B is a WEAKER claim ("not yours"), and
        // under band A's heading it would read as "junk you can free".
        var phone = Sky(Settings(), LeftoverRequest(LeftoverDump(), null, "Bard")).Groups;

        var a = phone.Single(g => g.Heading.StartsWith("No longer needed"));
        var b = phone.Single(g => g.Heading.StartsWith("Other classes still want"));

        Assert.Equal("Amulet piece ×2 · bags", Assert.Single(a.Rows).Text);
        Assert.Equal("Bow stave ×1 · bank", Assert.Single(b.Rows).Text);
        Assert.Contains("Ranger", Assert.Single(b.Rows).Detail);
    }

    [Fact]
    public void NoDumpMeansNoBandsOnThePhoneEither()
    {
        // Absent rather than empty, and the state EVERY new player is in: "you hold none
        // of it" and "you were never told" look identical in a count, and only one of
        // them is a fact.
        Assert.DoesNotContain(Sky(Settings(), LeftoverRequest(null, null, "Bard")).Groups,
            g => g.Heading.StartsWith("No longer needed")
                || g.Heading.StartsWith("Other classes still want"));

        // And the surface is otherwise untouched — this is an addition, not a rewrite.
        Assert.Equal(
            Checklist(Sky(Settings())).Select(g => g.Heading),
            Checklist(Sky(Settings(), LeftoverRequest(null, null, "Bard"))).Select(g => g.Heading));
    }

    [Fact]
    public void BandBIsSuppressedWithNoClassLensOnThePhoneToo()
    {
        // #193's rule, one surface over: no lens is not a wildcard. Band A is a claim
        // about the GAME and stands on its own.
        var phone = Sky(Settings(), LeftoverRequest(LeftoverDump())).Groups;

        Assert.Contains(phone, g => g.Heading.StartsWith("No longer needed"));
        Assert.DoesNotContain(phone, g => g.Heading.StartsWith("Other classes still want"));
    }

    [Fact]
    public void ThePagesClassChipsCannotHideTheBandsThatAnswerAboutClasses()
    {
        // index.html drops a group whose `class` sits outside the chip pick. Band B is a
        // claim ABOUT which classes you have, so a chip narrowing it would hide the very
        // answer — the same reason ★ Ready carries no class.
        Assert.All(
            Sky(Settings(), LeftoverRequest(LeftoverDump(), null, "Bard")).Groups
                .Where(g => !g.Tickable),
            g => Assert.Null(g.Class));
    }

    [Fact]
    public void ALeftoverRowIsNotAChecklistItem()
    {
        // It is something you HOLD, not something you tick — a checkbox on it would be a
        // silent no-op, and `done` would strike it through as though it were dealt with.
        foreach (var g in Sky(Settings(), LeftoverRequest(LeftoverDump(), null, "Bard")).Groups
                     .Where(g => g.Heading.Contains("No longer needed")
                         || g.Heading.Contains("Other classes still want")))
        {
            Assert.False(g.Tickable);
            Assert.All(g.Rows, r => Assert.False(r.Done));
        }
    }

    [Fact]
    public void TheBandsNeverMoveTheChecklistsOwnCounts()
    {
        // The tab badge counts REWARDS. A leftover row is neither done nor outstanding on
        // that list, and folding it into the badge would make the two screens disagree
        // about how far along the player is.
        var s = Settings();
        var with = Sky(s, LeftoverRequest(LeftoverDump(), null, "Bard"));

        Assert.Equal(Desktop(s).Sum(g => g.Done), with.Done);
        Assert.Equal(Desktop(s).Sum(g => g.Total), with.Total);
    }

    [Fact]
    public void TheHeldBackNoteRidesTheBandOnThePhoneToo()
    {
        // The veto: another quest wants the item, so it is NOT band A — and the note names
        // the quest rather than letting the item vanish. "1 more is still wanted by …" is
        // the sentence that stops someone selling it, and it is Core's words verbatim.
        var s = Settings();
        var catalog = new QuestCatalog
        {
            Quests =
            [
                new QuestEntry
                {
                    Name = "Songweaver's Errand", Classes = "ALL",
                    Items = [new QuestItemNeed { Name = "Amulet piece", Qty = 1 }],
                },
            ],
        };
        var core = Core(s, LeftoverDump(), catalog, "Bard");
        var phone = Sky(s, LeftoverRequest(LeftoverDump(), catalog, "Bard")).Groups;

        Assert.DoesNotContain(phone, g => g.Heading.StartsWith("No longer needed"));
        Assert.Equal("Amulet piece", Assert.Single(core.HeldBackByOtherQuests).Item);

        // With band A gone the note has nowhere to sit, which is Core's own arrangement —
        // the note belongs to the band it explains. Band B is unaffected and still drawn.
        Assert.Contains(phone, g => g.Heading.StartsWith("Other classes still want"));
    }

    [Fact]
    public void TheHeldBackNoteIsDrawnWhenBandAIsStillThere()
    {
        var s = Settings();
        var dump = Dump(("General1-Slot1", "Amulet piece", 2),
            ("General1-Slot2", "Woolen Mask", 1), ("Bank1", "Bow stave", 1));
        s.SkyQuestCompleted.Add(QuestChecklistLayout.RewardKey("Bard", "Mask of Song"));
        var catalog = new QuestCatalog
        {
            Quests =
            [
                new QuestEntry
                {
                    Name = "Songweaver's Errand", Classes = "ALL",
                    Items = [new QuestItemNeed { Name = "Woolen Mask", Qty = 1 }],
                },
            ],
        };

        var band = Sky(s, LeftoverRequest(dump, catalog, "Bard")).Groups
            .Single(g => g.Heading.StartsWith("No longer needed"));

        Assert.Equal(Core(s, dump, catalog, "Bard").HeldBackNote, band.Note);
        Assert.Contains("Songweaver's Errand", band.Note);
    }

    // ---- the PROGRESS THEME (docs/Themes.md) ----
    //
    // Five widget cards became four tabs in a desktop window, and the phone's "XP & AA"
    // surface became the same four tabs in the same change. That simultaneity is the
    // point: the four days it would have taken to notice a divergence are exactly what
    // #210 cost. So the projection is asserted against ProgressSurface itself, which is
    // the module both windows and the page read.

    private static CompanionProgressSection Progress(StatsSnapshot? stats = null) =>
        CompanionProjection.Build(new CompanionInputs
        {
            Stats = stats ?? new StatsSnapshot(),
            Offered = [CompanionSurfaces.Progress],
        }, DateTime.Now).Progress!;

    // ---- the Level-ups list: one merge, three surfaces (#240, joeymavity) ----
    //
    // The phone is the THIRD caller of LevelHistory, and it is the one that could most
    // easily grow its own answer: the rows arrive as JSON, the page could sort them, cap
    // them or format a date itself and look entirely correct beside a window nobody had
    // open at the time. So what is asserted here is not "the phone has a list" but "the
    // phone has the SAME list", against the module both desktops draw from.

    private static readonly DateTime LevelAug21 = new(2026, 8, 21, 20, 14, 0);
    private static readonly DateTime LevelAug22 = new(2026, 8, 22, 21, 30, 0);
    private static readonly DateTime LevelAug23 = new(2026, 8, 23, 19, 5, 0);

    /// <summary>Three dings across two sessions plus the live one — enough for an oldest
    /// row with no gap, a newest row with one, and a label with a count in it.</summary>
    private static IReadOnlyList<EQBuddy.UI.Shared.LevelHistory.Row> LevelRows() =>
        EQBuddy.UI.Shared.LevelHistory.Rows(
            [new SessionRepository.ProgressPoint(LevelAug21, 0, [(LevelAug21, 22)]),
             new SessionRepository.ProgressPoint(LevelAug22, 0, [(LevelAug22, 23)])],
            new StatsSnapshot { Levels = [new TimedDetail(LevelAug23, "Level 24")] });

    private static CompanionProgressSection LevelUps() =>
        CompanionProjection.Build(new CompanionInputs
        {
            Stats = new StatsSnapshot(),
            Offered = [CompanionSurfaces.Progress],
            LevelUps = LevelRows(),
        }, DateTime.Now).Progress!;

    [Fact]
    public void ThePhoneDrawsTheSameLevelUpRowsAsTheDesktop()
    {
        var desktop = EQBuddy.UI.Shared.LevelHistory.CardRows(LevelRows()).ToList();

        Assert.Equal(desktop, LevelUps().LevelUps!.Select(r => (r.Name, r.Value)).ToList());
    }

    [Fact]
    public void ThePhoneShowsTheDesktopsFoldedLabelRatherThanCountingForItself()
    {
        // The count and the last ding's date are what the fold is closed OVER, so the
        // string is the feature rather than decoration — and a page formatting its own
        // "last Aug 23" is a second date formatter for one fact (the FormatCoin lesson,
        // one tab across).
        Assert.Equal(EQBuddy.UI.Shared.LevelHistory.FoldLabel(LevelRows()),
            LevelUps().LevelUpsLabel);
    }

    [Fact]
    public void ThePhoneCarriesTheGapAsAHoverAndNeverAsAThirdToken()
    {
        // Bevel's call, Helm-signed 2026-09-02. The negative is the half that matters:
        // "the tip is present" would pass just as happily on rows that ALSO printed the
        // gap into the value, which is the arrangement the call ruled out.
        var rows = LevelUps().LevelUps!;

        Assert.Equal(EQBuddy.UI.Shared.LevelHistory.Tooltip(LevelRows()[0]), rows[0].Tip);
        Assert.Null(rows[^1].Tip);
        Assert.All(rows, r => Assert.DoesNotContain("since the previous", r.Value));
        Assert.All(rows, r => Assert.DoesNotContain("ago", r.Value, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ThePhoneKeepsTheOLDESTLevelUpsToo()
    {
        // Every other list on this wire is capped at MaxRows, and this one must not be:
        // the rows are newest-first, so a cap silently drops the EARLIEST dings — the
        // rarest rows, and the ones somebody opens the list to find (trap 50, #234). A
        // veteran's list is bounded by the level cap, not by how long they played.
        var many = Enumerable.Range(1, 60)
            .Select(i => (Time: LevelAug21.AddDays(-i), Level: 61 - i)).ToArray();

        var section = CompanionProjection.Build(new CompanionInputs
        {
            Stats = new StatsSnapshot(),
            Offered = [CompanionSurfaces.Progress],
            LevelUps = EQBuddy.UI.Shared.LevelHistory.Rows(
                [new SessionRepository.ProgressPoint(LevelAug21, 0, [.. many])], null),
        }, DateTime.Now).Progress!;

        Assert.Equal(60, section.LevelUps!.Count);
        Assert.Equal("Level 1", section.LevelUps[^1].Name);
    }

    [Fact]
    public void NoDingsMeansNoHeadingOnThePhoneEither()
    {
        // The desktop hides the fold entirely rather than showing a heading over nothing;
        // the page draws no card without the label, so this is that same rule on the wire.
        Assert.Null(Progress().LevelUpsLabel);
        Assert.Empty(Progress().LevelUps!);
    }

    [Fact]
    public void ThePhoneOffersTheSameProgressTabsInTheSameOrder()
    {
        var expected = Enum.GetValues<ProgressTab>().Select(ProgressSurface.KeyFor).ToList();

        Assert.Equal(expected, Progress().Tabs.Select(t => t.Key).ToList());
    }

    [Fact]
    public void ThePhoneUsesTheSameTabLabelsAsTheDesktop()
    {
        // Labels, not just keys: a phone that renamed "Wealth" to "Money" would look
        // fine on its own and be a different product beside the window.
        Assert.All(Progress().Tabs, t =>
            Assert.Equal(ProgressSurface.LabelFor(ProgressSurface.TabForKey(t.Key)!.Value), t.Label));
    }

    [Fact]
    public void ThePhoneCarriesBothHalvesOfTheWealthMerge()
    {
        // Wealth is the one tab that absorbs TWO cards, so it is the one that can be half
        // built and still look finished. The badge names both halves, and the block
        // carries both bodies.
        var stats = new StatsSnapshot { Copper = 51408 };
        var wealth = Progress(stats).Wealth;

        Assert.Equal(StatsSnapshot.FormatCoin(stats.Copper), wealth.Total);
        Assert.NotNull(wealth.MotesSummary);
        Assert.Equal(EQBuddy.UI.Shared.ProgressTheme.Wealth(stats),
            Progress(stats).Tabs.Single(t => t.Key == "wealth").Badge);
    }

    [Fact]
    public void ThePhonesRaidsTabNamesEveryCatalogTarget()
    {
        // With no ledger there are no clears to show, but the TOTAL is still the
        // catalog's — a phone reporting "0 / 0" would read as "there are no raid targets"
        // rather than "you have cleared none of them".
        var raids = Progress().Raids;

        Assert.Equal(RaidTargetCatalog.Default.BossCount, raids.Total);
        Assert.Equal(0, raids.Defeated);
    }

    // ---- the character's classes: one Resolve, three lanes ----

    /// <summary>
    /// **The phone resolves a character's classes through the same `CharacterClasses.Resolve`
    /// the two desktops use** — Fable's plan asked for this case and it could not be written
    /// until 2026-08-23, because until then the phone was sent a single `InferredClass`
    /// string while the desktops had a list.
    ///
    /// This is #210's rule applied to a DECISION rather than to a list of rows: the phone
    /// receives the ANSWER, not the ingredients, so it cannot resolve precedence differently.
    /// The precedence itself is not re-asserted here (`CharacterClassesTests` owns it); what
    /// this pins is that the wire carries the resolved list and its source rather than
    /// anything the page could re-derive.
    /// </summary>
    [Fact]
    public void ThePhoneIsSentTheResolvedClassesRatherThanTheIngredients()
    {
        var (classes, source) = CharacterClasses.Resolve(
            unlocked: ["Warrior", "Druid"], inferred: ["Monk"], picks: ["Bard"]);

        var section = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            Offered = [CompanionSurfaces.Quests],
            Stats = new StatsSnapshot(),
            Settings = new AppSettings(),
            Quests = new CompanionQuestRequest
            {
                Catalog = new QuestCatalog(),
                CharacterClassNames = classes,
                ClassSource = source,
            },
        }, new DateTime(2026, 8, 23)).Quests;

        Assert.NotNull(section);
        // The same list the desktops hold, in the same order, capped the same way.
        Assert.Equal(classes, section!.CharacterClasses);
        // And the WORDS, from Core's one table — the page must not compose its own
        // (Bevel, Helm-signed 2026-08-23: "do not grow a phone-only string").
        Assert.Equal(CharacterClasses.SourceLabel(source), section.ClassSourceLabel);
        // The INGREDIENTS are not on the wire — asserted against the DTO's own properties
        // rather than its ToString(), which is trap 39's mistake (comparing a rendered
        // string that may not mention what you are looking for either way).
        //
        // If the dump list and the pick list both travelled, a page could merge them into a
        // different answer than the desktops did, which is exactly how the quest checklists
        // drifted for two days before #210. `Classes` (the picker's own state) is a
        // deliberate exception: the page draws that picker.
        var wireFields = typeof(CompanionQuestsSection).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(wireFields, n => n.Contains("Unlocked", StringComparison.Ordinal));
        Assert.Contains("CharacterClasses", wireFields);
        Assert.Contains("ClassSourceLabel", wireFields);
    }

    /// <summary>Nothing known is the same nothing on all three: an empty list and no source
    /// word, rather than a phone-side "unknown" string somebody has to keep in step.</summary>
    [Fact]
    public void AnUnknownClassListIsEmptyOnThePhoneToo()
    {
        var section = CompanionProjection.Build(new CompanionInputs
        {
            Character = "Dranak",
            Offered = [CompanionSurfaces.Quests],
            Stats = new StatsSnapshot(),
            Settings = new AppSettings(),
            Quests = new CompanionQuestRequest { Catalog = new QuestCatalog() },
        }, new DateTime(2026, 8, 23)).Quests;

        Assert.NotNull(section);
        Assert.Null(section!.CharacterClasses);
        Assert.Null(section.ClassSourceLabel);
        Assert.Equal("", CharacterClasses.SourceLabel(ClassSource.Unknown));
    }
}
