using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EQBuddy.Core;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// The Quest Tracker rendered headlessly: the ledger-overlap card ("mine"), the
/// whole-catalog search, and the header controls every card carries. Matching and
/// persistence are Core's (QuestMatcher/QuestLedgerStore, tested there); these tests
/// guard that the Avalonia view actually surfaces what Core computes.
/// </summary>
[Collection("avalonia")]
public sealed class QuestsRenderTests : IDisposable
{
    private readonly string _profile = Directory.CreateTempSubdirectory("eqbuddy-quests-render-").FullName;

    public QuestsRenderTests() => Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", _profile);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EQBUDDY_APPDATA", null);
        try { Directory.Delete(_profile, recursive: true); }
        catch (Exception ex) { Console.Error.WriteLine($"profile cleanup failed: {ex.Message}"); }
    }

    private sealed class FakeHost : IQuestsHost
    {
        public AppSettings Settings { get; } = new();
        public QuestCatalog QuestCatalog { get; init; } = new();
        public QuestLedgerStore? QuestLedger { get; init; }
        public ZoneGraph ZoneGraph { get; } = new();
        public string QuestCharacterKey { get; init; } = "";
        public string CurrentZoneName { get; init; } = "";
        public StatsSnapshot CurrentSnapshot() => new();

        /// <summary>Settable, so a test can put a real class list (and its SOURCE) in front
        /// of the window — the point of `CharacterClasses` is that "Warrior · Druid · Monk"
        /// reads differently depending on whether the game said it or a heuristic guessed.</summary>
        public (IReadOnlyList<string> Classes, ClassSource Source) Classes { get; set; } = ([], ClassSource.Unknown);

        public (IReadOnlyList<string> Classes, ClassSource Source) ClassSourceFor(StatsSnapshot s) => Classes;
        /// <summary>Settable, so a test can put the Sky tab's import report on screen —
        /// null is the ordinary case (no dump has been read this session).</summary>
        public AutoImportOutcome? LastAchievementsImport { get; set; }
        /// <summary>The inventory dump's own report (Hateborne, 2026-09-03) — same rule.</summary>
        public AutoImportOutcome? LastInventoryImport { get; set; }
        /// <summary>Settable, so a test can put a real `/outputfile inventory` dump in
        /// front of the window — null is the honest default (no dump has ever been read),
        /// and the #243 leftover bands have to be ABSENT in that state rather than empty:
        /// "you hold none of it" and "you were never told" look identical in a count and
        /// only one of them is a fact.</summary>
        public InventoryFile.Snapshot? Inventory { get; set; }
        public InventoryFile.Snapshot? LatestInventory(bool refresh = false) => Inventory;

        // The Unlocks tab's two dumps. Settable so a test can stage them; the default is
        // the honest "this player has never run either command" state, which is the one
        // the tab has to handle without showing sixteen races at zero.
        public IReadOnlyList<UnlockProgress> RaceUnlocks { get; set; } = [];
        public IReadOnlyList<UnlockProgress> ClassUnlocks { get; set; } = [];
        public FactionsFile.Snapshot? LatestFactions { get; set; }
        public bool HasUnlockDump { get; set; }
        public string? CachedItemStats(string itemName) => null;
        public Task<string?> FetchItemTooltip(string itemName) => Task.FromResult<string?>(null);
    }

    private static QuestCatalog Catalog() => new()
    {
        Quests =
        [
            new QuestEntry
            {
                Name = "The Falchion", Url = "https://eqlwiki.com/The_Falchion",
                StartZone = "Crushbone", QuestGiver = "Danaria Fyrestone",
                Classes = "Paladin",
                Items = [new QuestItemNeed { Name = "Blue Orc Head", Qty = 2 }],
                Rewards = ["The Falchion"],
            },
            new QuestEntry
            {
                Name = "Crude Stein Quest", Url = "https://eqlwiki.com/Crude_Stein",
                StartZone = "Qeynos",
                Items = [new QuestItemNeed { Name = "Crude Stein", Qty = 1 }],
                Rewards = ["Shiny Stein"],
            },
        ],
    };

    private static List<string> Texts(QuestsWindow window) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];

    /// <summary>Gate 2 turned a column of self-contained cards into a LIST plus a DETAIL
    /// PANE (docs/DesignSystem.md), so the assertions moved with the content: the list
    /// carries the name, the state badge and the meta line, and the pane carries the
    /// turn-ins, the rewards and the five controls. Which quests appear, and why, is
    /// unchanged — that is the half this test is really guarding.</summary>
    [AvaloniaFact]
    public void MineTabListsTheOverlapAndOpensItInTheDetailPane()
    {
        var ledger = new QuestLedgerStore(Path.Combine(_profile, "quest-ledger.json"));
        ledger.SetManual("tester_p1999", "Blue Orc Head", 1);
        var window = new QuestsWindow(new FakeHost
        {
            QuestCatalog = Catalog(),
            QuestLedger = ledger,
            QuestCharacterKey = "tester_p1999",
        });
        window.Show();

        var text = Texts(window);
        Assert.Contains("Quest Tracker — Tester", text);
        Assert.Contains("The Falchion", text);
        // The status badge, from UI.Shared's QuestPresentation so both desktops agree:
        // item TYPES with any copies, over total types.
        Assert.Contains("1/1", text);
        // The first row is selected, so its turn-ins are in the pane: name and have/need
        // as separate cells now, not one concatenated string.
        Assert.Equal("The Falchion", window.SelectedQuest);
        Assert.Contains("Blue Orc Head", text);
        Assert.Contains("1 / 2", text);
        Assert.Contains("TURN-INS", text);
        Assert.Contains("REWARDS", text);
        // Crude Stein has no owned items, so "mine" must not show it.
        Assert.DoesNotContain("Crude Stein Quest", text);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The five per-quest controls are icon BUTTONS on the pane now, not five
    /// click-handled TextBlocks on every card in the list — keyboard-reachable, with a hit
    /// area, and built once instead of once per row.</summary>
    [AvaloniaFact]
    public void TheDetailPaneCarriesTheQuestControls()
    {
        var ledger = new QuestLedgerStore(Path.Combine(_profile, "quest-ledger-controls.json"));
        ledger.SetManual("tester_p1999", "Blue Orc Head", 1);
        var window = new QuestsWindow(new FakeHost
        {
            QuestCatalog = Catalog(),
            QuestLedger = ledger,
            QuestCharacterKey = "tester_p1999",
        });
        window.Show();

        var tips = window.GetVisualDescendants().OfType<Button>()
            .Select(b => ToolTip.GetTip(b) as string ?? "").ToList();
        Assert.Contains(tips, t => t.Contains("Track this quest"));
        Assert.Contains(tips, t => t.Contains("Did this before EQBuddy"));
        Assert.Contains(tips, t => t.Contains("Not interested"));
        Assert.Contains(tips, t => t.Contains("Something wrong with this quest's data"));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void SearchReadsTheWholeCatalogAndIgnoresOverlap()
    {
        var window = new QuestsWindow(new FakeHost
        {
            QuestCatalog = Catalog(),
            QuestCharacterKey = "tester_p1999",
        });
        window.Show();
        window.FilterToItem("Crude Stein");

        var text = Texts(window);
        Assert.Contains("Crude Stein Quest", text);
        Assert.Contains(text, t => t.StartsWith("1 match in the whole catalog"));
        // Zero owned pieces still renders the turn-in row — search is for finding.
        Assert.Contains("Crude Stein", text);
        Assert.Contains("0 / 1", text);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>"Ready" is the one state worth interrupting for, so it has to be legible
    /// twice: the badge on the row, and the primary action on the pane. The hand-in used
    /// to BE the progress count — an affordance whose only signal was a tooltip.</summary>
    [AvaloniaFact]
    public void ReadyQuestShowsTheBadgeAndAPrimaryHandInAction()
    {
        var ledger = new QuestLedgerStore(Path.Combine(_profile, "quest-ledger-ready.json"));
        ledger.SetManual("tester_p1999", "Blue Orc Head", 2);
        var window = new QuestsWindow(new FakeHost
        {
            QuestCatalog = Catalog(),
            QuestLedger = ledger,
            QuestCharacterKey = "tester_p1999",
        });
        window.Show();

        var text = Texts(window);
        Assert.Contains("ready", text);
        Assert.Contains("ready to turn in", text);
        // The summary above the list answers the window's question before a row is read.
        Assert.Contains("1 quest ready to turn in", text);
        Assert.Contains("Mark as turned in", text);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    // ---- the three quest tabs (2026-08-16) ----
    //
    // These arrived on Avalonia a day after WPF, closing a real gap rather than the
    // usual parity lag: consolidating the widget's Epic and Sky cards into one launcher
    // left this build with nowhere at all to view or hand-tick those checklists.

    /// <summary>The checklist rows, told apart from the window's other checkboxes (the
    /// class picker's, and the Epic tab's classic-only lens) by carrying a TextBlock as
    /// content rather than a string. Read from Content rather than the visual tree
    /// because a headless CheckBox does not realise its ContentPresenter's children.</summary>
    private static List<CheckBox> ChecklistRows(QuestsWindow window) =>
        [.. window.GetVisualDescendants().OfType<CheckBox>().Where(c => c.Content is TextBlock)];

    private static List<string> ChecklistTitles(QuestsWindow window) =>
        [.. ChecklistRows(window).Select(c => ((TextBlock)c.Content!).Text ?? "")];

    private static AppSettings WithChecklists(AppSettings settings)
    {
        settings.EpicQuestChecklist =
        [
            new EpicQuestChecklistItem
            {
                Id = "e1", ClassName = "Bard", Section = "Pieces",
                QuestName = "Singing Short Sword", QuestItem = "Sarnak Battle Shield",
                AvailableInClassic = true,
            },
            new EpicQuestChecklistItem
            {
                Id = "e2", ClassName = "Bard", Section = "Pieces",
                QuestName = "Kunark Step", QuestItem = "Head of the Serpent",
                AvailableInClassic = false,
            },
        ];
        settings.SkyQuestChecklist =
        [
            new SkyQuestChecklistItem
            {
                Id = "s1", ClassName = "Bard", Reward = "Cape of the Wind",
                Npc = "Noble Dojorn", QuestItem = "Wind Fragment",
            },
        ];
        return settings;
    }

    [AvaloniaFact]
    public void TheTabStripComesFromCoreAndBadgesOnlyTheChecklists()
    {
        var host = new FakeHost { QuestCatalog = Catalog(), QuestCharacterKey = "tester_p1999" };
        WithChecklists(host.Settings);
        var window = new QuestsWindow(host);
        window.Show();

        var text = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        // Core's QuestSurface owns the labels, so this also guards against the two
        // desktops drifting apart from each other and from EQBuddy Mobile.
        foreach (var tab in new[] { QuestTab.General, QuestTab.Epic, QuestTab.Sky })
            Assert.Contains(QuestSurface.LabelFor(tab), text);
        // General is a catalog you search, not a checklist you finish: "0 / 1172" there
        // would read as failure rather than as a library.
        Assert.Contains("0 / 2", text);   // Epic
        Assert.Contains("0 / 1", text);   // Sky

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheEpicTabListsItsRowsAndTheyCanBeTicked()
    {
        var host = new FakeHost { QuestCatalog = Catalog(), QuestCharacterKey = "tester_p1999" };
        var settings = WithChecklists(host.Settings);
        var window = new QuestsWindow(host);
        window.Show();
        window.SetTab(QuestTab.Epic);

        var rows = ChecklistRows(window);
        Assert.Equal(2, rows.Count);

        // Hand-ticking is the whole reason these rows are checkboxes: it used to live on
        // the widget's Epic card, and this is now the only place on the desktop to say
        // "I already have that".
        rows[0].IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, settings.EpicQuestChecklist.Count(i => i.Acquired));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheClassicLensHidesTheStepsItSaysItHides()
    {
        var host = new FakeHost { QuestCatalog = Catalog(), QuestCharacterKey = "tester_p1999" };
        host.Settings.EpicQuestClassicOnly = true;
        WithChecklists(host.Settings);
        var window = new QuestsWindow(host);
        window.Show();
        window.SetTab(QuestTab.Epic);

        var titles = ChecklistTitles(window);
        Assert.Contains(titles, t => t.Contains("Singing Short Sword"));
        Assert.DoesNotContain(titles, t => t.Contains("Kunark Step"));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheSkyTabRendersItsOwnChecklistNotTheCatalog()
    {
        var host = new FakeHost { QuestCatalog = Catalog(), QuestCharacterKey = "tester_p1999" };
        WithChecklists(host.Settings);
        var window = new QuestsWindow(host);
        window.Show();
        window.SetTab(QuestTab.Sky);

        // The REWARD is the group heading and the piece is the row (#184): one turn-in
        // NPC hands out every sky reward for a class, so grouping by NPC put them all in
        // one undifferentiated list. The row carries the NPC and the drop location.
        var headings = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains(headings, t => t.Contains("Cape of the Wind"));

        var titles = ChecklistTitles(window);
        Assert.Contains(titles, t => t.Contains("Wind Fragment") && t.Contains("Noble Dojorn"));
        // A checklist tab is not the catalog: the general list's quests must not leak in.
        Assert.DoesNotContain(titles, t => t.Contains("The Falchion"));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    // ---- the two leftover bands (#243, tvongaza — 2026-09-02) ----
    //
    // The join itself is Core's and is covered by SkyLeftoversTests. What can only be
    // asserted HERE is that the bands exist on screen at all, and that each one carries
    // its own absence: an absent control photographs as an unremarkable panel (trap 29),
    // so a screenshot can confirm the band reads well and only an assertion can say it is
    // there. The bands are found by the Tag each Border carries, never by matching their
    // heading text — identity is a property you PUT on the object (trap 39).

    private static Border? Band(QuestsWindow window, string tag) =>
        window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Tag as string == tag);

    private static List<string> BandLines(QuestsWindow window, string tag) =>
        Band(window, tag) is { } band
            ? [.. band.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")]
            : [];

    /// <summary>Warrior's own Sky demand for Azure Ring is turned in; Stone Amulet's is
    /// too. Brass Knuckles is still wanted, but only by Monk and Paladin. Wind Tablet is
    /// still wanted by Warrior, which IS this character, so it is not a leftover at
    /// all.</summary>
    private static AppSettings WithLeftovers(AppSettings settings)
    {
        settings.SkyQuestChecklist =
        [
            Sky("a1", "Warrior", "Ring of Air", "Azure Ring"),
            Sky("a2", "Warrior", "Ring of Stone", "Stone Amulet"),
            Sky("b1", "Monk", "Fist of Wind", "Brass Knuckles"),
            Sky("b2", "Paladin", "Blade of Light", "Brass Knuckles"),
            Sky("c1", "Warrior", "Belt of Winds", "Wind Tablet"),
        ];
        settings.SkyQuestCompleted = ["Warrior|Ring of Air", "Warrior|Ring of Stone"];
        return settings;

        static SkyQuestChecklistItem Sky(string id, string cls, string reward, string item) =>
            new() { Id = id, ClassName = cls, Reward = reward, Npc = "Cilin", QuestItem = item };
    }

    private static InventoryFile.Snapshot Dump() =>
        new("Testchar_test-Inventory.txt", DateTime.Now, new(StringComparer.OrdinalIgnoreCase)
        {
            ["Azure Ring"] = 1,
            ["Stone Amulet"] = 1,
            ["Brass Knuckles"] = 2,
            ["Wind Tablet"] = 1,
        })
        {
            Entries =
            [
                new InventoryFile.Entry("General1-Slot3", "Azure Ring", 1),
                new InventoryFile.Entry("General1-Slot4", "Stone Amulet", 1),
                // The SHARED bank, which is a bank — the rule InventoryFile.Entry.InBank
                // now owns for this band and for the Gear tab both.
                new InventoryFile.Entry("SharedBank1", "Brass Knuckles", 2),
                new InventoryFile.Entry("General2-Slot1", "Wind Tablet", 1),
            ],
        };

    [AvaloniaFact]
    public void TheTwoLeftoverBandsAreSeparateAndSayDifferentThings()
    {
        var host = new FakeHost
        {
            QuestCatalog = Catalog(),
            QuestCharacterKey = "tester_p1999",
            Inventory = Dump(),
            Classes = (["Warrior"], ClassSource.Achievements),
        };
        WithLeftovers(host.Settings);
        var window = new QuestsWindow(host);
        window.Show();
        window.SetTab(QuestTab.Sky);

        // Band A: the reporter's own sentence, and the only strong claim.
        var a = BandLines(window, "skyLeftoverA");
        Assert.Contains("No longer needed — 2", a);
        Assert.Contains("Azure Ring ×1 · bags", a);
        Assert.Contains("Stone Amulet ×1 · bags", a);

        // Band B: a WEAKER claim, under its own honest heading. Mixing it under band A's
        // words is the one presentation Bevel's ruling exists to prevent, so the heading
        // is asserted here and its absence from band A below.
        var b = BandLines(window, "skyLeftoverB");
        Assert.Contains("Other classes still want — 1", b);
        // The shared bank reads as "bank", not as a worn slot.
        Assert.Contains("Brass Knuckles ×2 · bank", b);

        Assert.DoesNotContain(a, t => t.Contains("Brass Knuckles"));
        Assert.DoesNotContain(b, t => t.Contains("No longer needed"));
        // Still wanted by a class this character HAS, so it is not leftover in either band.
        Assert.DoesNotContain(a.Concat(b), t => t.Contains("Wind Tablet"));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void WithNoDumpNeitherBandIsDrawnAtAll()
    {
        var host = new FakeHost
        {
            QuestCatalog = Catalog(),
            QuestCharacterKey = "tester_p1999",
            Classes = (["Warrior"], ClassSource.Achievements),
        };
        WithLeftovers(host.Settings);
        var window = new QuestsWindow(host);
        window.Show();
        window.SetTab(QuestTab.Sky);

        // ABSENT, not empty. A permanently-present band reading "nothing" is how a player
        // learns to stop looking at it — the Ready band's own rule, and here it also
        // covers the state every new player is in.
        Assert.Null(Band(window, "skyLeftoverA"));
        Assert.Null(Band(window, "skyLeftoverB"));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void WithNoClassLensBandBIsSuppressedAndBandAIsNot()
    {
        var host = new FakeHost
        {
            QuestCatalog = Catalog(),
            QuestCharacterKey = "tester_p1999",
            Inventory = Dump(),
            // No picks, no resolved classes: the honest "we do not know who you are".
        };
        WithLeftovers(host.Settings);
        var window = new QuestsWindow(host);
        window.Show();
        window.SetTab(QuestTab.Sky);

        // No lens is not a wildcard (#193, one surface over): with no classes known there
        // is no "other" to be other than, so the item is simply still wanted.
        Assert.Null(Band(window, "skyLeftoverB"));
        // Band A is a claim about the GAME and is unaffected by who is playing.
        Assert.Contains("No longer needed — 2", BandLines(window, "skyLeftoverA"));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AnItemAnotherQuestWantsLeavesTheBandAndIsExplainedUnderIt()
    {
        var catalog = Catalog();
        catalog.Quests.Add(new QuestEntry
        {
            Name = "Blackburrow Brewers", Url = "https://eqlwiki.com/Blackburrow_Brewers",
            Items = [new QuestItemNeed { Name = "Azure Ring", Qty = 1 }],
            Rewards = ["Stein of Moggok"],
        });
        var host = new FakeHost
        {
            QuestCatalog = catalog,
            QuestCharacterKey = "tester_p1999",
            Inventory = Dump(),
            Classes = (["Warrior"], ClassSource.Achievements),
        };
        WithLeftovers(host.Settings);
        var window = new QuestsWindow(host);
        window.Show();
        window.SetTab(QuestTab.Sky);

        var a = BandLines(window, "skyLeftoverA");
        Assert.Contains("No longer needed — 1", a);
        Assert.DoesNotContain(a, t => t.StartsWith("Azure Ring", StringComparison.Ordinal));
        // Not merely absent: an item that vanishes with no reason reads as a bug in the
        // join, and naming the quest is the sentence that stops someone selling it.
        Assert.Contains(a, t => t.Contains("Azure Ring") && t.Contains("Blackburrow Brewers"));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    // ---- the session-only band folds (Hateborne, 2026-09-03) ----
    //
    // Fold state is a FIELD, never a setting (the ProgressCardView precedent — Bevel,
    // Helm-signed 2026-08-23): these tests prove the click works, that folding one band
    // leaves its neighbour alone, and that a fresh window opens expanded — the assertion
    // that would fail the day someone "helpfully" persists it.

    /// <summary>Rows built through Inlines have no <c>Text</c>, so the plain BandLines
    /// helper cannot see them — this one flattens Runs too.</summary>
    private static List<string> BandTexts(QuestsWindow window, string tag) =>
        Band(window, tag) is { } band
            ? [.. band.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Inlines is { Count: > 0 } inl
                    ? string.Concat(inl.OfType<global::Avalonia.Controls.Documents.Run>()
                        .Select(r => r.Text))
                    : t.Text ?? "")]
            : [];

    private static void FoldClick(QuestsWindow window, string tag)
    {
        // A control's bounds exist only after a layout pass — without this the click
        // lands at (4, 0) of an un-arranged tree and toggles nothing.
        window.UpdateLayout();
        var fold = Band(window, tag)!.GetVisualDescendants().OfType<EqFoldLabel>().Single();
        var point = global::Avalonia.VisualExtensions.TranslatePoint(
            fold, new global::Avalonia.Point(4, fold.Bounds.Height / 2), window);
        Assert.True(point.HasValue, $"{tag}'s heading is not laid out — it cannot be clicked");
        window.MouseDown(point!.Value, global::Avalonia.Input.MouseButton.Left);
        window.MouseUp(point!.Value, global::Avalonia.Input.MouseButton.Left);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static FakeHost LeftoverHost()
    {
        var host = new FakeHost
        {
            QuestCatalog = Catalog(),
            QuestCharacterKey = "tester_p1999",
            Inventory = Dump(),
            Classes = (["Warrior"], ClassSource.Achievements),
        };
        WithLeftovers(host.Settings);
        return host;
    }

    [AvaloniaFact]
    public void ClickingABandHeadingFoldsItToOneLineAndClickingAgainReopensIt()
    {
        var window = new QuestsWindow(LeftoverHost());
        window.Show();
        window.SetTab(QuestTab.Sky);

        Assert.Contains("Azure Ring ×1 · bags", BandLines(window, "skyLeftoverA"));

        FoldClick(window, "skyLeftoverA");
        var a = BandLines(window, "skyLeftoverA");
        // Collapsed is one line in the SAME box: the Border stays, the heading keeps its
        // count, and the rows are gone.
        Assert.NotNull(Band(window, "skyLeftoverA"));
        Assert.Contains("No longer needed — 2", a);
        Assert.DoesNotContain(a, t => t.Contains("Azure Ring"));
        // The neighbour is untouched — one fold per band, not one for the tab.
        Assert.Contains("Brass Knuckles ×2 · bank", BandLines(window, "skyLeftoverB"));

        FoldClick(window, "skyLeftoverA");
        Assert.Contains("Azure Ring ×1 · bags", BandLines(window, "skyLeftoverA"));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheReadyBandFoldsTheSameWayAndAFreshWindowOpensExpanded()
    {
        var host = LeftoverHost();
        // Belt of Winds' one piece in hand: a ready reward, so the Ready band draws.
        host.Settings.SkyQuestChecklist.Single(i => i.Id == "c1").Acquired = true;
        var window = new QuestsWindow(host);
        window.Show();
        window.SetTab(QuestTab.Sky);

        Assert.Contains(BandTexts(window, "skyReady"), t => t.Contains("Belt of Winds"));

        FoldClick(window, "skyReady");
        var folded = BandTexts(window, "skyReady");
        Assert.Contains("Ready to turn in — 1", folded);
        Assert.DoesNotContain(folded, t => t.Contains("Belt of Winds"));

        // Session-only, literally: the fold dies with the window, not with the session's
        // settings file — a FRESH window opens expanded.
        window.Close();
        Dispatcher.UIThread.RunJobs();
        var fresh = new QuestsWindow(host);
        fresh.Show();
        fresh.SetTab(QuestTab.Sky);
        Assert.Contains(BandTexts(fresh, "skyReady"), t => t.Contains("Belt of Winds"));

        fresh.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The already-unlocked caveat (Hateborne, 2026-09-03): a ready reward for a
    /// class whose unlock the achievements dump says is complete carries Core's one-line
    /// note, and other classes' rows do not.</summary>
    [AvaloniaFact]
    public void TheReadyBandAnnotatesAnAlreadyUnlockedClass()
    {
        var ledger = new QuestLedgerStore(Path.Combine(_profile, "quest-ledger.json"));
        ledger.SetUnlockedClasses("tester_p1999", ["Warrior"]);
        var host = new FakeHost
        {
            QuestCatalog = Catalog(),
            QuestLedger = ledger,
            QuestCharacterKey = "tester_p1999",
            Classes = (["Warrior"], ClassSource.Achievements),
        };
        WithLeftovers(host.Settings);
        host.Settings.SkyQuestChecklist.Single(i => i.Id == "c1").Acquired = true;
        var window = new QuestsWindow(host);
        window.Show();
        window.SetTab(QuestTab.Sky);

        Assert.Contains(BandTexts(window, "skyReady"), t => t.Contains("Belt of Winds")
            && t.Contains("Warrior already unlocked — turn in for the item only"));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The Sky tab names BOTH of its dumps (Hateborne, 2026-09-03): the
    /// achievements ⧉ it always had, and an inventory ⧉ in the tab body — the header's
    /// "scan bags" is chrome, not the tab saying what feeds it.</summary>
    [AvaloniaFact]
    public void TheSkyTabDrawsItsOwnInventoryPrompt()
    {
        var window = new QuestsWindow(LeftoverHost());
        window.Show();
        window.SetTab(QuestTab.Sky);
        // The ⧉ buttons' content is materialized by the layout pass, not by Children.Add.
        window.UpdateLayout();

        var labels = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text ?? "").ToList();
        Assert.Contains(UI.Shared.GameCommands.OutputfileInventory, labels);
        Assert.Contains(UI.Shared.GameCommands.OutputfileAchievements, labels);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
