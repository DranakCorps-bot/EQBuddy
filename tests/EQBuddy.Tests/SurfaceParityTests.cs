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

    private static CompanionChecklistSection Sky(AppSettings s) =>
        CompanionProjection.Build(
            new CompanionInputs
            {
                Settings = s,
                Character = "Dranak",
                AppVersion = "1.93.0",
                Offered = CompanionSurfaces.All,
            },
            new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Local)).Quests!.Sky;

    private static IReadOnlyList<QuestChecklistGroup> Desktop(AppSettings s) =>
        QuestChecklistLayout.Sky(s.SkyQuestChecklist, s.SkyQuestCompleted);

    [Fact]
    public void ThePhoneGroupsSkyExactlyAsTheDesktopDoes()
    {
        var s = Settings();

        // Skip the ★ Ready band, which is a summary the desktop draws separately.
        var phone = Sky(s).Groups.Skip(1).Select(g => g.Heading);
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
            Sky(Settings()).Groups.Skip(1).Select(g => g.Heading));
    }

    [Fact]
    public void ThePhoneAndTheDesktopAgreeOnEveryStateNote()
    {
        var s = Settings();

        Assert.Equal(
            Desktop(s).Select(g => g.Note),
            Sky(s).Groups.Skip(1).Select(g => g.Note));
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
            Sky(s).Groups.Skip(1).Single(g => g.Heading.EndsWith("Mask of Song")).Note);
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

        Assert.Equal(Desktop(s).Count, Sky(s).Groups.Skip(1).Count());
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
        // is the whole point of the surface.
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
