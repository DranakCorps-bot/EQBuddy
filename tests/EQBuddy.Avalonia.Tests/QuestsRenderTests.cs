using Avalonia.Controls;
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
        /// <summary>Settable, so a test can put the Sky tab's import report on screen —
        /// null is the ordinary case (no dump has been read this session).</summary>
        public AutoImportOutcome? LastAchievementsImport { get; set; }
        public InventoryFile.Snapshot? LatestInventory(bool refresh = false) => null;
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
}
