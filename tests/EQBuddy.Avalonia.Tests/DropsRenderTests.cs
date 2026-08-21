using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EQBuddy.Core;
using EQBuddy.UI.Shared;

namespace EQBuddy.Avalonia.Tests;

/// <summary>
/// Drops by Creature rendered headlessly: per-creature grouping with the kill
/// denominator, the quest badge, and the new-to-wiki marker fed by the host's mob-lookup
/// memo. Report/export formatting is Core's (DropsReport/WikiContribution).
///
/// It was a WINDOW until 2026-08-21 and these were its tests. It is a tab of the Kills &amp;
/// Drops theme now, and the assertions barely changed — which is the point of the surface
/// having taken IDropsHost from the start rather than the widget. What DID change is the
/// two badges: they were click-handled EMOJI, and emoji box outright under Wine (#148,
/// #166) while a bare vector only hit-tests where it is painted (#211). Both are
/// InlineIconButtons now, so the assertions look for the icon rather than the glyph.
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
        var view = new DropsCardView(host);
        view.Render(Snapshot());

        Assert.Contains("a moss snake", host.LookupsFired);
        var text = view.Body.GetLogicalDescendants().OfType<TextBlock>()
            .Select(t => t.Text).ToList();
        Assert.Contains("a moss snake — 2 kills  ·  wiki page lists no loot yet", text);
        Assert.Contains("orc pawn — 1 kill", text);
        Assert.Contains("Snake Fang", text);
        Assert.Contains("  ×1  ·  50% of 2", text);    // observed rate keeps its denominator
        Assert.Contains("  ×1", text);                  // null rate renders count only
        // The two badges, as VECTORS. A glyph assertion could not tell an icon that is
        // drawn from one that is drawn AND clickable, which is the distinction #211 was.
        var icons = view.Body.GetLogicalDescendants().OfType<Button>()
            .Select(b => (b.Content as global::Avalonia.Controls.PathIcon)?.Data?.ToString())
            .Where(d => d is not null).ToList();
        Assert.Contains(icons, d => d == Geometry("Map"));       // quest badge on Crude Stein
        Assert.Contains(icons, d => d == Geometry("Sparkle"));   // new-to-wiki marker
    }

    /// <summary>Avalonia re-serializes a parsed geometry, so comparing to IconPaths.Path
    /// directly does not work — the string coming back off a PathIcon is a normalized form
    /// of the one that went in. Parse the expected side too.</summary>
    private static string Geometry(string name) =>
        global::Avalonia.Media.StreamGeometry.Parse(IconPaths.Path(name)).ToString();

    [AvaloniaFact]
    public void FilterNarrowsAndReportsEmptiness()
    {
        var view = new DropsCardView(new FakeHost());
        view.Render(Snapshot());

        var filter = view.Body.GetLogicalDescendants().OfType<TextBox>().First();
        filter.Text = "orc";
        Dispatcher.UIThread.RunJobs();   // TextChanged lands via the dispatcher
        var text = view.Body.GetLogicalDescendants().OfType<TextBlock>()
            .Select(t => t.Text).ToList();
        Assert.Contains("orc pawn — 1 kill", text);
        Assert.DoesNotContain(text, t => t?.StartsWith("a moss snake") == true);

        filter.Text = "no such thing";
        Dispatcher.UIThread.RunJobs();
        text = view.Body.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        // The filter LEADS the signature, or an empty result hashes to the reset sentinel
        // and the stale rows stay on screen. Windows carried that bug until this fold.
        Assert.Contains("Nothing matches that filter.", text);
        Assert.DoesNotContain(text, t => t?.StartsWith("orc pawn") == true);
    }
}
