using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EQBuddy.Core;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// Drops by Creature rendered headlessly: per-creature grouping with the kill
/// denominator, the quest badge, and the ✦ new-to-wiki marker fed by the host's
/// mob-lookup memo. Report/export formatting is Core's (DropsReport/WikiContribution).
/// </summary>
[Collection("avalonia")]
public sealed class DropsRenderTests
{
    private sealed class FakeHost : IDropsHost
    {
        public AppSettings Settings { get; } = new();
        public (string Character, string Server) Identity { get; init; } = ("", "");
        public string CurrentZoneName { get; init; } = "";
        public Func<string, MobLookupResult?> MobResult { get; init; } = _ => null;
        public HashSet<string> QuestItems { get; init; } = [];
        public List<string> LookupsFired { get; } = [];
        public StatsSnapshot CurrentSnapshot() => new();
        public string? CachedItemStats(string itemName) => null;
        public Task<string?> FetchItemTooltip(string itemName) => Task.FromResult<string?>(null);
        public MobLookupResult? WikiMobResult(string name) => MobResult(name);
        public void EnsureMobLookup(string name) => LookupsFired.Add(name);
        public bool IsActiveQuestItem(string name) => QuestItems.Contains(name);
        public void OpenQuestInfoForItem(string itemName) { }
        public int WikiPackOpened { get; private set; }
        public void ShowWikiPack() => WikiPackOpened++;
    }

    private static StatsSnapshot Snapshot() => new()
    {
        Mobs =
        [
            new MobSummary("a moss snake", 2, 2, 10, 1.0, 0,
                [new MobLoot("Snake Fang", 1, 50.0), new MobLoot("Snake Scales", 2, 100.0)]),
            new MobSummary("orc pawn", 1, 1, 8, 0.5, 12,
                [new MobLoot("Crude Stein", 1, null)]),
        ],
    };

    [AvaloniaFact]
    public void RendersCreatureGroupsWithRatesBadgesAndStars()
    {
        var host = new FakeHost
        {
            Identity = ("Tester", "p1999"),
            QuestItems = ["Crude Stein"],
            // a moss snake's page exists but lists no loot → every drop gets a ✦.
            MobResult = name => name == "a moss snake"
                ? new MobLookupResult(new MobInfo { Name = "a moss snake" },
                    ItemLookupState.Cached, DateTime.Now)
                : null,
        };
        var window = new DropsWindow(host);
        window.Show();
        window.Update(Snapshot());

        Assert.Equal("EQBuddy — Drops by Creature — Tester", window.Title);
        Assert.Contains("a moss snake", host.LookupsFired);
        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text).ToList();
        Assert.Contains("a moss snake — 2 kills  ·  wiki page lists no loot yet", text);
        Assert.Contains("orc pawn — 1 kill", text);
        Assert.Contains("Snake Fang", text);
        Assert.Contains("  ×1  ·  50% of 2", text);    // observed rate keeps its denominator
        Assert.Contains("  ×1", text);                  // null rate renders count only
        Assert.Contains(" 🗺", text);                   // quest badge on Crude Stein
        Assert.Contains(" ✦", text);                    // new-to-wiki marker

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void FilterNarrowsAndReportsEmptiness()
    {
        var window = new DropsWindow(new FakeHost());
        window.Show();
        window.Update(Snapshot());

        var filter = window.GetVisualDescendants().OfType<TextBox>().First();
        filter.Text = "orc";
        Dispatcher.UIThread.RunJobs();   // TextChanged lands via the dispatcher
        window.UpdateLayout();           // and the rebuilt rows need a layout pass to join the visual tree
        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text).ToList();
        Assert.Contains("orc pawn — 1 kill", text);
        Assert.DoesNotContain(text, t => t?.StartsWith("a moss snake") == true);

        filter.Text = "no such thing";
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        text = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Nothing matches that filter.", text);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
