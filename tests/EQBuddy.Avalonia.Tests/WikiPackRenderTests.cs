using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using EQBuddy.Core;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// The Wiki contribution pack window rendered headlessly (#217 Ask 1, Frankthetankk).
///
/// <c>WikiPackPresentationTests</c> holds the RULES; these hold that this window actually
/// puts them on screen. The distinction earned itself three gates running — Gate 4's Loot
/// strips were built, selected and correct, and invisible, because the host they hung in
/// was collapsed (trap 15). A unit test cannot see that; something that renders can.
///
/// The two facts worth guarding here are the two the move could have lost: the SCOPE line
/// (the reason this is a window rather than a menu command that copies silently) and the
/// disabled Copy button being visibly disabled (trap 17 — the button style carries no
/// disabled visual, so IsEnabled alone would look identical to a live one).
/// </summary>
[Collection("avalonia")]
public sealed class WikiPackRenderTests
{
    private sealed class FakeHost : IDropsHost
    {
        public AppSettings Settings { get; } = new();
        public (string Character, string Server) Identity { get; init; } = ("", "");
        public string CurrentZoneName { get; init; } = "";
        public Func<string, MobLookupResult?> MobResult { get; init; } = _ => null;
        public List<string> LookupsFired { get; } = [];
        public StatsSnapshot CurrentSnapshot() => new();
        public string? CachedItemStats(string itemName) => null;
        public Task<string?> FetchItemTooltip(string itemName) => Task.FromResult<string?>(null);
        public MobLookupResult? WikiMobResult(string name) => MobResult(name);
        public void EnsureMobLookup(string name) => LookupsFired.Add(name);
        public bool IsActiveQuestItem(string name) => false;
        public void OpenQuestInfoForItem(string itemName) { }
        public void ShowWikiPack() { }
    }

    private static StatsSnapshot Snapshot() => new()
    {
        SessionStart = new DateTime(2026, 8, 19, 18, 22, 0),
        Mobs =
        [
            new MobSummary("Chief Goonda", 3, 3, 20, 2.0, 40,
                [new MobLoot("Goonda's Club", 1, 33.3)]),
            new MobSummary("a moss snake", 2, 2, 10, 1.0, 0,
                [new MobLoot("Snake Fang", 1, 50.0)]),
        ],
    };

    private static List<string?> TextsOf(Window w) =>
        w.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

    [AvaloniaFact]
    public void ShowsRowsWithStatusAndTheScopeItPooled()
    {
        var host = new FakeHost
        {
            Identity = ("Tester", "p1999"),
            // Chief Goonda has no page at all; the moss snake's page exists but is empty.
            MobResult = name => name == "a moss snake"
                ? new MobLookupResult(new MobInfo { Name = "a moss snake" },
                    ItemLookupState.Cached, DateTime.Now)
                : new MobLookupResult(null, ItemLookupState.NotFound, null),
        };
        var window = new WikiPackWindow(host);
        window.Show();
        window.Update(Snapshot());

        Assert.Equal("EQBuddy — Wiki contribution pack", window.Title);
        Assert.Contains("Chief Goonda", host.LookupsFired);

        var text = TextsOf(window);
        Assert.Contains("2 items across 2 creatures the wiki doesn't have", text);
        Assert.Contains("Chief Goonda", text);
        Assert.Contains("a moss snake", text);
        Assert.Contains(text, t => t is not null && t.Contains("no wiki page"));
        Assert.Contains(text, t => t is not null && t.Contains("page lists no loot"));

        // The scope, on screen. Behind a menu command this was invisible, which is the
        // whole reason the move came with a window.
        Assert.Contains(text, t => t is not null
            && t.Contains("This session only")
            && t.Contains("Tester (p1999)")
            && t.Contains("18:22"));
    }

    /// <summary>Trap 17: a disabled control with no disabled visual is a silent no-op.</summary>
    [AvaloniaFact]
    public void CopyIsDisabledAndVisibly_dimmed_when_there_is_nothing_to_paste()
    {
        var host = new FakeHost
        {
            // Every drop is already on the wiki.
            MobResult = _ => new MobLookupResult(new MobInfo
            {
                Name = "x",
                Drops = [("Goonda's Club", ""), ("Snake Fang", "")],
            }, ItemLookupState.Cached, DateTime.Now),
        };
        var window = new WikiPackWindow(host);
        window.Show();
        window.Update(Snapshot());

        var copy = window.GetVisualDescendants().OfType<Button>().Single();
        Assert.False(copy.IsEnabled);
        Assert.True(copy.Opacity < 1.0, "a disabled Copy must LOOK disabled — trap 17");

        Assert.Contains(TextsOf(window), t => t is not null && t.Contains("already on eqlwiki"));
    }

    /// <summary>An unread wiki page must never render as "nothing new" — the one honesty
    /// rule the pack has always had, now on a second surface.</summary>
    [AvaloniaFact]
    public void An_unread_page_says_so_rather_than_claiming_nothing_is_new()
    {
        var host = new FakeHost { MobResult = _ => new MobLookupResult(null, ItemLookupState.Offline, null) };
        var window = new WikiPackWindow(host);
        window.Show();
        window.Update(Snapshot());

        var text = TextsOf(window);
        Assert.Contains(text, t => t is not null && t.Contains("not checked yet"));
        Assert.DoesNotContain(text, t => t is not null && t.Contains("already on eqlwiki"));

        var copy = window.GetVisualDescendants().OfType<Button>().Single();
        Assert.False(copy.IsEnabled);
    }
}
